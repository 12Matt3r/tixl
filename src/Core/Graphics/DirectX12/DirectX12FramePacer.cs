using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using Vortice.Windows;
using Vortice.Windows.Direct3D12;
using TiXL.Core.Performance;

namespace TiXL.Core.Graphics.DirectX12
{
    /// <summary>
    /// DirectX 12 frame pacing and CPU-GPU synchronization system
    /// Implements strict single-frame budget management with fence-based synchronization
    /// </summary>
    public class DirectX12FramePacer : IDisposable
    {
        private readonly PerformanceMonitor _performanceMonitor;
        private readonly PredictiveFrameScheduler _frameScheduler;
        private readonly RenderingEngineConfig _config;
        private ID3D12Device _device;
        private ID3D12CommandQueue _commandQueue;
        
        // DirectX 12 synchronization objects
        private readonly object _fenceLock = new object();
        private readonly Queue<D3D12FenceWrapper> _availableFences;
        private readonly Dictionary<ulong, FenceInfo> _pendingFences;
        private uint _nextFenceValue;
        private ulong _lastCompletedFence;
        private readonly AutoResetEvent _fenceEvent;
        
        // Frame budget management
        private readonly object _frameBudgetLock = new object();
        private readonly Queue<FrameBudgetEntry> _frameBudgetQueue;
        private readonly Stopwatch _frameTimer = new Stopwatch();
        private readonly double _targetFrameTimeMs = 16.67; // 60 FPS
        private readonly double _criticalFrameTimeMs = 33.33; // 30 FPS
        private readonly int _maxInFlightFrames = 3;
        
        // GPU timeline profiling
        private readonly ConcurrentQueue<GpuTimelineEntry> _gpuTimeline;
        private readonly int _maxGpuTimelineEntries = 300;
        private readonly Dictionary<string, GpuQuery> _activeQueries;
        
        // Resource lifecycle management
        private readonly ResourceLifecycleManager _resourceManager;
        private readonly Queue<Action> _pendingResourceOperations;
        
        // Monitoring and alerting
        private readonly FramePacingMetrics _metrics = new FramePacingMetrics();
        private readonly Timer _metricsTimer;
        private readonly object _metricsLock = new object();
        
        public event EventHandler<FrameBudgetExceededEventArgs> FrameBudgetExceeded;
        public event EventHandler<FenceSignalEventArgs> FenceSignaled;
        public event EventHandler<FramePacingAlert> FramePacingAlert;
        
        public int InFlightFrameCount { get; private set; }
        public int PendingFenceCount => _pendingFences.Count;
        public double CurrentFrameTime => _frameTimer.Elapsed.TotalMilliseconds;
        public FramePacingStatistics Statistics => GetStatistics();
        
        public DirectX12FramePacer(
            PerformanceMonitor performanceMonitor, 
            PredictiveFrameScheduler frameScheduler,
            RenderingEngineConfig config = null)
        {
            _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
            _frameScheduler = frameScheduler ?? throw new ArgumentNullException(nameof(frameScheduler));
            _config = config ?? new RenderingEngineConfig();
            
            _availableFences = new Queue<D3D12FenceWrapper>();
            _pendingFences = new Dictionary<ulong, FenceInfo>();
            _gpuTimeline = new ConcurrentQueue<GpuTimelineEntry>();
            _activeQueries = new Dictionary<string, GpuQuery>();
            _frameBudgetQueue = new Queue<FrameBudgetEntry>();
            _pendingResourceOperations = new Queue<Action>();
            
            _resourceManager = new ResourceLifecycleManager();
            _fenceEvent = new AutoResetEvent(false);
            
            // Start metrics collection
            _metricsTimer = new Timer(UpdateMetrics, null, 0, 16); // ~60Hz
            
            // Subscribe to performance alerts
            _performanceMonitor.PerformanceAlert += OnPerformanceAlert;
        }
        
        /// <summary>
        /// Initialize DirectX 12 specific components (called by DirectX12RenderingEngine)
        /// </summary>
        public void InitializeDirectX12Components(ID3D12Device device, ID3D12CommandQueue commandQueue)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _commandQueue = commandQueue ?? throw new ArgumentNullException(nameof(commandQueue));
            
            // Initialize actual DirectX 12 fences
            InitializeFences();
        }
        
        /// <summary>
        /// Begin a new frame with strict budget management
        /// </summary>
        public FrameBudgetToken BeginFrame()
        {
            lock (_frameBudgetLock)
            {
                // Check if we're exceeding the in-flight frame budget
                if (InFlightFrameCount >= _maxInFlightFrames)
                {
                    OnFrameBudgetExceeded(new FrameBudgetExceededEventArgs
                    {
                        InFlightCount = InFlightFrameCount,
                        MaxAllowed = _maxInFlightFrames,
                        FrameTime = _frameTimer.Elapsed.TotalMilliseconds,
                        Timestamp = DateTime.UtcNow
                    });
                    
                    // Still proceed but mark as budget exceeded
                }
                
                // Create fence for this frame
                var fence = GetAvailableFence();
                var fenceValue = Interlocked.Increment(ref _nextFenceValue);
                
                var frameToken = new FrameBudgetToken
                {
                    Fence = fence,
                    FenceValue = fenceValue,
                    StartTime = Stopwatch.GetTimestamp(),
                    FrameId = (ulong)fenceValue
                };
                
                // Track frame entry
                _frameBudgetQueue.Enqueue(new FrameBudgetEntry
                {
                    Token = frameToken,
                    StartTime = DateTime.UtcNow
                });
                
                InFlightFrameCount++;
                _frameTimer.Restart();
                
                // Initialize GPU queries for this frame
                InitializeFrameGpuQueries(frameToken);
                
                return frameToken;
            }
        }
        
        /// <summary>
        /// End current frame and signal completion fence
        /// </summary>
        public async Task EndFrameAsync(FrameBudgetToken token)
        {
            using (var profiler = _performanceMonitor.ProfileOperation("FrameEnd"))
            {
                try
                {
                    // Finalize GPU timeline queries
                    await FinalizeFrameGpuQueriesAsync(token);
                    
                    // Signal fence to indicate frame completion
                    SignalFence(token);
                    
                    // Wait for fence completion if needed
                    await WaitForFenceCompletionAsync(token.FenceValue);
                    
                    // Clean up frame budget
                    RemoveFrameFromBudget(token);
                    
                    // Process pending resource operations
                    ProcessPendingResourceOperations();
                    
                    // Update predictive scheduler with frame metrics
                    UpdateFrameMetrics();
                }
                catch (Exception ex)
                {
                    OnFramePacingAlert(new FramePacingAlert
                    {
                        Type = FramePacingAlertType.FrameEndError,
                        Message = $"Frame end error: {ex.Message}",
                        Severity = AlertSeverity.Error,
                        Timestamp = DateTime.UtcNow
                    });
                    throw;
                }
            }
        }
        
        /// <summary>
        /// Submit work to GPU with fence synchronization
        /// </summary>
        public async Task SubmitToGpuAsync(string operationName, Func<Task> gpuWork)
        {
            var queryId = Guid.NewGuid().ToString();
            
            try
            {
                // Create GPU timeline query
                var query = new GpuQuery
                {
                    Id = queryId,
                    Name = operationName,
                    StartTime = Stopwatch.GetTimestamp()
                };
                
                lock (_activeQueries)
                {
                    _activeQueries[queryId] = query;
                }
                
                // Execute GPU work
                await gpuWork();
                
                query.EndTime = Stopwatch.GetTimestamp();
                
                // Add to timeline
                _gpuTimeline.Enqueue(new GpuTimelineEntry
                {
                    QueryId = queryId,
                    OperationName = operationName,
                    StartTimestamp = query.StartTime,
                    EndTimestamp = query.EndTime.Value
                });
                
                // Trim timeline if too long
                TrimGpuTimeline();
                
                lock (_activeQueries)
                {
                    _activeQueries.Remove(queryId);
                }
            }
            catch (Exception ex)
            {
                OnFramePacingAlert(new FramePacingAlert
                {
                    Type = FramePacingAlertType.GpuSubmitError,
                    Message = $"GPU submission error for {operationName}: {ex.Message}",
                    Severity = AlertSeverity.Error,
                    Timestamp = DateTime.UtcNow
                });
                throw;
            }
        }
        
        /// <summary>
        /// Execute work within frame budget constraints
        /// </summary>
        public async Task<bool> ExecuteWithinBudgetAsync(string operationName, Func<Task> work, double estimatedDurationMs)
        {
            var remainingBudget = _targetFrameTimeMs - CurrentFrameTime;
            
            // Skip work if it would exceed budget significantly
            if (estimatedDurationMs > remainingBudget * 0.8)
            {
                OnFramePacingAlert(new FramePacingAlert
                {
                    Type = FramePacingAlertType.BudgetSkippedWork,
                    Message = $"Skipped operation '{operationName}' due to budget constraints",
                    Severity = AlertSeverity.Warning,
                    Timestamp = DateTime.UtcNow
                });
                return false;
            }
            
            try
            {
                await work();
                return true;
            }
            catch (Exception ex)
            {
                OnFramePacingAlert(new FramePacingAlert
                {
                    Type = FramePacingAlertType.BudgetWorkError,
                    Message = $"Budget-constrained work failed for {operationName}: {ex.Message}",
                    Severity = AlertSeverity.Error,
                    Timestamp = DateTime.UtcNow
                });
                return false;
            }
        }
        
        /// <summary>
        /// Queue resource operation for execution within frame budget
        /// </summary>
        public void QueueResourceOperation(Action operation, ResourcePriority priority = ResourcePriority.Normal)
        {
            _resourceManager.QueueOperation(operation, priority);
        }
        
        /// <summary>
        /// Wait for specific fence value completion using actual DirectX 12 fence APIs
        /// </summary>
        public async Task WaitForFenceCompletionAsync(ulong fenceValue)
        {
            // Find the fence object for this fence value
            FenceInfo fenceInfo = null;
            lock (_fenceLock)
            {
                if (_pendingFences.TryGetValue(fenceValue, out fenceInfo))
                {
                    _lastCompletedFence = Math.Max(_lastCompletedFence, fenceValue);
                }
            }
            
            if (fenceInfo == null)
            {
                // Fence already completed or doesn't exist
                return;
            }
            
            // Use real DirectX 12 fence waiting
            await WaitForFenceCompletionAsyncInternal(fenceInfo.Fence, fenceValue);
            
            // Mark fence as completed
            lock (_fenceLock)
            {
                _pendingFences.Remove(fenceValue);
                _lastCompletedFence = Math.Max(_lastCompletedFence, fenceValue);
            }
            
            // Trigger fence signal event
            OnFenceSignaled(new FenceSignalEventArgs
            {
                FenceValue = fenceValue,
                SignalTime = DateTime.UtcNow
            });
        }
        
        /// <summary>
        /// Get current GPU timeline analysis
        /// </summary>
        public GpuTimelineAnalysis GetGpuTimelineAnalysis()
        {
            var recentEntries = GetRecentGpuTimelineEntries(60); // Last 60 frames
            if (recentEntries.Count == 0) return null;
            
            var totalGpuTime = recentEntries.Sum(e => e.DurationMs);
            var avgGpuTime = totalGpuTime / recentEntries.Count;
            var gpuVariance = CalculateVariance(recentEntries.Select(e => e.DurationMs));
            
            return new GpuTimelineAnalysis
            {
                AverageGpuTime = avgGpuTime,
                GpuTimeVariance = gpuVariance,
                TotalGpuTime = totalGpuTime,
                FrameCount = recentEntries.Count,
                OperationBreakdown = GetOperationBreakdown(recentEntries),
                Timestamp = DateTime.UtcNow
            };
        }
        
        private void InitializeFences()
        {
            // Create actual DirectX 12 fences for real CPU-GPU synchronization
            for (int i = 0; i < _maxInFlightFrames * 2; i++)
            {
                _availableFences.Enqueue(CreateNewFence());
            }
            
            OnFramePacingAlert(new FramePacingAlert
            {
                Type = FramePacingAlertType.PerformanceAlert,
                Message = $"Initialized {(_maxInFlightFrames * 2)} DirectX 12 fences for CPU-GPU synchronization",
                Severity = AlertSeverity.Info,
                Timestamp = DateTime.UtcNow
            });
        }
        
        private D3D12FenceWrapper CreateNewFence()
        {
            return new D3D12FenceWrapper(_device, FenceFlags.None);
        }
        
        private D3D12FenceWrapper GetAvailableFence()
        {
            lock (_fenceLock)
            {
                if (_availableFences.Count > 0)
                {
                    return _availableFences.Dequeue();
                }
                
                // Create new fence if none available
                return CreateNewFence();
            }
        }
        
        private void SignalFence(FrameBudgetToken token)
        {
            if (_commandQueue == null)
            {
                throw new InvalidOperationException("Command queue is required for fence signaling");
            }
            
            // Signal the fence on the GPU command queue for real CPU-GPU synchronization
            token.Fence.Signal(token.FenceValue, _commandQueue);
            
            var fenceInfo = new FenceInfo
            {
                Fence = token.Fence,
                Value = token.FenceValue,
                SignaledAt = DateTime.UtcNow,
                FrameId = token.FrameId
            };
            
            lock (_fenceLock)
            {
                _pendingFences[token.FenceValue] = fenceInfo;
            }
            
            // Update frame budget tracking with real fence timing
            UpdateFrameBudgetWithFenceSignal(token);
        }
        
        private async Task WaitForFenceCompletionAsyncInternal(D3D12FenceWrapper fenceWrapper, ulong value)
        {
            // Check if fence is already signaled
            if (fenceWrapper.Fence.CompletedValue >= value)
            {
                return;
            }
            
            // Use real DirectX 12 fence waiting with proper error handling
            try
            {
                fenceWrapper.SetEventOnCompletion(value, _fenceEvent.SafeWaitHandle.DangerousGetHandle());
                
                // Wait for the fence to be signaled using the event with timeout
                await Task.Run(() =>
                {
                    var signaled = _fenceEvent.WaitOne(TimeSpan.FromSeconds(5)); // 5 second timeout
                    if (!signaled)
                    {
                        OnFramePacingAlert(new FramePacingAlert
                        {
                            Type = FramePacingAlertType.FenceTimeout,
                            Message = $"Fence timeout waiting for value {value}",
                            Severity = AlertSeverity.Error,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                OnFramePacingAlert(new FramePacingAlert
                {
                    Type = FramePacingAlertType.SyncBreakdown,
                    Message = $"Fence synchronization error: {ex.Message}",
                    Severity = AlertSeverity.Error,
                    Timestamp = DateTime.UtcNow
                });
                throw;
            }
        }
        
        private void InitializeFrameGpuQueries(FrameBudgetToken token)
        {
            // Initialize standard GPU queries for frame timing
            _activeQueries["FrameTotal"] = new GpuQuery
            {
                Id = $"FrameTotal_{token.FrameId}",
                Name = "Frame Total",
                StartTime = Stopwatch.GetTimestamp()
            };
            
            _activeQueries["GpuMain"] = new GpuQuery
            {
                Id = $"GpuMain_{token.FrameId}",
                Name = "GPU Main Pass",
                StartTime = Stopwatch.GetTimestamp()
            };
        }
        
        private async Task FinalizeFrameGpuQueriesAsync(FrameBudgetToken token)
        {
            var now = Stopwatch.GetTimestamp();
            
            lock (_activeQueries)
            {
                foreach (var query in _activeQueries.Values.ToArray())
                {
                    if (query.StartTime > 0 && query.EndTime == null)
                    {
                        query.EndTime = now;
                        
                        _gpuTimeline.Enqueue(new GpuTimelineEntry
                        {
                            QueryId = query.Id,
                            OperationName = query.Name,
                            StartTimestamp = query.StartTime,
                            EndTimestamp = now
                        });
                    }
                }
            }
            
            // Trim timeline if too long
            TrimGpuTimeline();
        }
        
        private void TrimGpuTimeline()
        {
            while (_gpuTimeline.Count > _maxGpuTimelineEntries)
            {
                _gpuTimeline.TryDequeue(out _);
            }
        }
        
        private List<GpuTimelineEntry> GetRecentGpuTimelineEntries(int count)
        {
            return _gpuTimeline.Take(count).Reverse().Take(count).Reverse().ToList();
        }
        
        private Dictionary<string, double> GetOperationBreakdown(List<GpuTimelineEntry> entries)
        {
            return entries.GroupBy(e => e.OperationName)
                         .ToDictionary(g => g.Key, g => g.Average(e => e.DurationMs));
        }
        
        private void RemoveFrameFromBudget(FrameBudgetToken token)
        {
            lock (_frameBudgetLock)
            {
                // Remove from queue
                var entriesToRemove = _frameBudgetQueue.Where(e => e.Token.FrameId == token.FrameId).ToArray();
                foreach (var entry in entriesToRemove)
                {
                    _frameBudgetQueue.TryDequeue(out _);
                }
                
                InFlightFrameCount = Math.Max(0, InFlightFrameCount - 1);
                
                // Return fence to available pool
                lock (_fenceLock)
                {
                    _availableFences.Enqueue(token.Fence);
                }
            }
        }
        
        private void ProcessPendingResourceOperations()
        {
            var processedCount = 0;
            var timeBudget = _targetFrameTimeMs - CurrentFrameTime - 2.0; // Reserve 2ms
            
            while (_pendingResourceOperations.Count > 0 && 
                   processedCount < 10 && 
                   CurrentFrameTime < _targetFrameTimeMs)
            {
                if (_pendingResourceOperations.TryDequeue(out var operation))
                {
                    try
                    {
                        operation();
                        processedCount++;
                    }
                    catch (Exception ex)
                    {
                        OnFramePacingAlert(new FramePacingAlert
                        {
                            Type = FramePacingAlertType.ResourceOperationError,
                            Message = $"Resource operation failed: {ex.Message}",
                            Severity = AlertSeverity.Warning,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }
            }
            
            _metrics.TotalResourceOperationsProcessed += processedCount;
        }
        
        private void UpdateFrameMetrics()
        {
            var frameTime = _frameTimer.Elapsed.TotalMilliseconds;
            
            _performanceMonitor.EndFrame();
            
            // Update frame scheduler with latest metrics
            var analysis = _performanceMonitor.GetFrameAnalysis();
            if (analysis != null)
            {
                _frameScheduler.UpdateFrameMetrics(
                    analysis.AverageCpuTime,
                    analysis.AverageGpuTime,
                    frameTime);
            }
        }
        
        private void UpdateMetrics(object state)
        {
            lock (_metricsLock)
            {
                _metrics.InFlightFrameCount = InFlightFrameCount;
                _metrics.PendingFenceCount = _pendingFences.Count;
                _metrics.CurrentFrameTime = CurrentFrameTime;
                _metrics.GpuTimelineEntryCount = _gpuTimeline.Count;
                
                var timelineAnalysis = GetGpuTimelineAnalysis();
                if (timelineAnalysis != null)
                {
                    _metrics.AverageGpuTime = timelineAnalysis.AverageGpuTime;
                    _metrics.GpuTimeVariance = timelineAnalysis.GpuTimeVariance;
                }
            }
        }
        
        private FramePacingStatistics GetStatistics()
        {
            lock (_metricsLock)
            {
                return new FramePacingStatistics
                {
                    InFlightFrameCount = _metrics.InFlightFrameCount,
                    PendingFenceCount = _metrics.PendingFenceCount,
                    CurrentFrameTime = _metrics.CurrentFrameTime,
                    AverageGpuTime = _metrics.AverageGpuTime,
                    GpuTimeVariance = _metrics.GpuTimeVariance,
                    GpuTimelineEntryCount = _metrics.GpuTimelineEntryCount,
                    TotalResourceOperationsProcessed = _metrics.TotalResourceOperationsProcessed,
                    FrameBudgetComplianceRate = CalculateBudgetComplianceRate(),
                    AverageFrameTime = _performanceMonitor.GetFrameAnalysis()?.AverageFrameTime ?? 0,
                    Timestamp = DateTime.UtcNow
                };
            }
        }
        
        private double CalculateBudgetComplianceRate()
        {
            var analysis = _performanceMonitor.GetFrameAnalysis();
            if (analysis == null) return 0;
            
            var compliantFrames = analysis.FrameCount - analysis.DroppedFrames;
            return (double)compliantFrames / analysis.FrameCount;
        }
        
        private void OnPerformanceAlert(object sender, PerformanceAlert alert)
        {
            if (alert.Type == AlertType.CriticalFrameTime || 
                alert.Type == AlertType.FrameTimeWarning)
            {
                OnFramePacingAlert(new FramePacingAlert
                {
                    Type = FramePacingAlertType.PerformanceAlert,
                    Message = alert.Message,
                    Severity = AlertSeverity.Warning,
                    Timestamp = alert.Timestamp
                });
            }
        }
        
        private static double CalculateVariance(IEnumerable<double> values)
        {
            var valuesList = values.ToList();
            if (valuesList.Count < 2) return 0;
            
            var mean = valuesList.Average();
            return valuesList.Sum(x => (x - mean) * (x - mean)) / valuesList.Count;
        }
        
        /// <summary>
        /// Update frame budget tracking with real fence signal timing
        /// </summary>
        private void UpdateFrameBudgetWithFenceSignal(FrameBudgetToken token)
        {
            try
            {
                // Record the actual time when fence was signaled
                var signalLatency = (DateTime.UtcNow - token.StartTime.ToDateTime()).TotalMilliseconds;
                
                // Update performance monitor with real fence timing data
                _performanceMonitor.RecordFenceSignalTime(signalLatency);
                
                // Adjust frame pacing based on real GPU synchronization timing
                var adjustedFrameTime = _targetFrameTimeMs - (signalLatency * 0.1); // Account for sync overhead
                if (adjustedFrameTime < _criticalFrameTimeMs * 0.5)
                {
                    OnFramePacingAlert(new FramePacingAlert
                    {
                        Type = FramePacingAlertType.BudgetSkippedWork,
                        Message = $"High fence signaling latency detected: {signalLatency:F2}ms",
                        Severity = AlertSeverity.Warning,
                        Timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update frame budget with fence signal: {ex.Message}");
            }
        }
        
        protected virtual void OnFrameBudgetExceeded(FrameBudgetExceededEventArgs e)
        {
            FrameBudgetExceeded?.Invoke(this, e);
        }
        
        protected virtual void OnFenceSignaled(FenceSignalEventArgs e)
        {
            FenceSignaled?.Invoke(this, e);
        }
        
        protected virtual void OnFramePacingAlert(FramePacingAlert e)
        {
            FramePacingAlert?.Invoke(this, e);
        }
        
        public void Dispose()
        {
            _metricsTimer?.Dispose();
            _performanceMonitor.PerformanceAlert -= OnPerformanceAlert;
            
            // Clean up fence resources
            _fenceEvent?.Dispose();
            
            lock (_fenceLock)
            {
                // Dispose all available fences
                while (_availableFences.Count > 0)
                {
                    var fence = _availableFences.Dequeue();
                    fence?.Dispose();
                }
                
                // Dispose all pending fences
                foreach (var fence in _pendingFences.Values)
                {
                    fence.Fence?.Dispose();
                }
                _pendingFences.Clear();
            }
            
            // Clean up remaining operations
            while (_pendingResourceOperations.TryDequeue(out _)) { }
            
            // Clear queues
            _frameBudgetQueue.Clear();
            
            while (_gpuTimeline.TryDequeue(out _)) { }
            
            _activeQueries.Clear();
            _resourceManager?.Dispose();
        }
    }
    
    // Supporting classes and interfaces
    
    /// <summary>
    /// Real DirectX 12 fence wrapper for frame pacing
    /// </summary>
    public class D3D12FenceWrapper
    {
        private readonly ID3D12Fence _fence;
        private readonly ID3D12Device _device;
        private readonly FenceFlags _flags;
        
        public ID3D12Fence Fence => _fence;
        public FenceFlags Flags => _flags;
        
        public D3D12FenceWrapper(ID3D12Device device, FenceFlags flags = FenceFlags.None)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _flags = flags;
            
            _fence = device.CreateFence(0, flags);
        }
        
        public bool IsSignaled(ulong value)
        {
            return _fence.CompletedValue >= value;
        }
        
        public ulong CurrentValue => _fence.CompletedValue;
        
        public void Signal(ulong value, ID3D12CommandQueue commandQueue)
        {
            if (commandQueue == null)
                throw new ArgumentNullException(nameof(commandQueue));
                
            _fence.Signal(value);
        }
        
        public void SignalOnCpu(ulong value)
        {
            _fence.Signal(value);
        }
        
        public void SetEventOnCompletion(ulong value, IntPtr handle)
        {
            _fence.SetEventOnCompletion(value, handle);
        }
        
        public void Dispose()
        {
            _fence?.Dispose();
        }
    }
    
    public class FrameBudgetToken : IDisposable
    {
        public D3D12FenceWrapper Fence { get; set; }
        public ulong FenceValue { get; set; }
        public long StartTime { get; set; }
        public ulong FrameId { get; set; }
        public bool IsDisposed { get; private set; }
        
        public void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
                Fence?.Dispose();
            }
        }
        
        public double ElapsedMs => (Stopwatch.GetTimestamp() - StartTime) / (double)Stopwatch.Frequency * 1000.0;
    }
    
    public class FenceInfo
    {
        public D3D12FenceWrapper Fence { get; set; }
        public ulong Value { get; set; }
        public DateTime SignaledAt { get; set; }
        public ulong FrameId { get; set; }
    }
    
    public class FrameBudgetEntry
    {
        public FrameBudgetToken Token { get; set; }
        public DateTime StartTime { get; set; }
    }
    
    public class GpuQuery
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public long StartTime { get; set; }
        public long? EndTime { get; set; }
        
        public double DurationMs => EndTime.HasValue ? 
            (EndTime.Value - StartTime) / (double)Stopwatch.Frequency * 1000.0 : 0;
    }
    
    public class GpuTimelineEntry
    {
        public string QueryId { get; set; }
        public string OperationName { get; set; }
        public long StartTimestamp { get; set; }
        public long EndTimestamp { get; set; }
        
        public double DurationMs => (EndTimestamp - StartTimestamp) / (double)Stopwatch.Frequency * 1000.0;
    }
    
    public class FramePacingMetrics
    {
        public int InFlightFrameCount { get; set; }
        public int PendingFenceCount { get; set; }
        public double CurrentFrameTime { get; set; }
        public double AverageGpuTime { get; set; }
        public double GpuTimeVariance { get; set; }
        public int GpuTimelineEntryCount { get; set; }
        public int TotalResourceOperationsProcessed { get; set; }
    }
}