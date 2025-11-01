using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
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
        
        // DirectX 12 synchronization objects
        private readonly object _fenceLock = new object();
        private readonly Queue<ID3D12Fence> _availableFences;
        private readonly Dictionary<ulong, FenceInfo> _pendingFences;
        private uint _nextFenceValue;
        private ulong _lastCompletedFence;
        
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
            PredictiveFrameScheduler frameScheduler)
        {
            _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
            _frameScheduler = frameScheduler ?? throw new ArgumentNullException(nameof(frameScheduler));
            
            _availableFences = new Queue<ID3D12Fence>();
            _pendingFences = new Dictionary<ulong, FenceInfo>();
            _gpuTimeline = new ConcurrentQueue<GpuTimelineEntry>();
            _activeQueries = new Dictionary<string, GpuQuery>();
            _frameBudgetQueue = new Queue<FrameBudgetEntry>();
            _pendingResourceOperations = new Queue<Action>();
            
            _resourceManager = new ResourceLifecycleManager();
            
            // Initialize fences (in real implementation, these would be actual D3D12 fences)
            InitializeFences();
            
            // Start metrics collection
            _metricsTimer = new Timer(UpdateMetrics, null, 0, 16); // ~60Hz
            
            // Subscribe to performance alerts
            _performanceMonitor.PerformanceAlert += OnPerformanceAlert;
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
        /// Wait for specific fence value completion
        /// </summary>
        public async Task WaitForFenceCompletionAsync(ulong fenceValue)
        {
            // In real implementation, this would use actual D3D12 fence waiting
            // For now, simulate with adaptive polling based on fence value
            
            while (_lastCompletedFence < fenceValue)
            {
                // Simulate GPU processing time
                await Task.Delay(1);
                
                // Update last completed fence (simulated)
                _lastCompletedFence++;
                
                // Trigger fence signal event
                OnFenceSignaled(new FenceSignalEventArgs
                {
                    FenceValue = _lastCompletedFence,
                    SignalTime = DateTime.UtcNow
                });
            }
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
            // In real implementation, create actual ID3D12Fence objects
            // For simulation, create mock fence objects
            for (int i = 0; i < _maxInFlightFrames * 2; i++)
            {
                _availableFences.Enqueue(new MockD3D12Fence());
            }
        }
        
        private ID3D12Fence GetAvailableFence()
        {
            lock (_fenceLock)
            {
                if (_availableFences.Count > 0)
                {
                    return _availableFences.Dequeue();
                }
                
                // Create new fence if none available
                return new MockD3D12Fence();
            }
        }
        
        private void SignalFence(FrameBudgetToken token)
        {
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
        }
        
        private async Task WaitForFenceCompletionAsyncInternal(ID3D12Fence fence, ulong value)
        {
            // In real implementation, use D3D12 fence APIs
            // Simulate fence waiting with polling
            while (true)
            {
                // Check if fence is signaled (simulated)
                if (fence.IsSignaled(value))
                    break;
                    
                await Task.Delay(1);
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
            
            // Clean up remaining operations
            while (_pendingResourceOperations.TryDequeue(out _)) { }
            
            // Clear queues
            _frameBudgetQueue.Clear();
            _availableFences.Clear();
            _pendingFences.Clear();
            
            while (_gpuTimeline.TryDequeue(out _)) { }
            
            _activeQueries.Clear();
            _resourceManager?.Dispose();
        }
    }
    
    // Supporting classes and interfaces
    
    public interface ID3D12Fence
    {
        bool IsSignaled(ulong value);
        void Signal(ulong value);
    }
    
    public class MockD3D12Fence : ID3D12Fence
    {
        private ulong _currentValue;
        
        public bool IsSignaled(ulong value)
        {
            return _currentValue >= value;
        }
        
        public void Signal(ulong value)
        {
            _currentValue = Math.Max(_currentValue, value);
        }
    }
    
    public class FrameBudgetToken : IDisposable
    {
        public ID3D12Fence Fence { get; set; }
        public ulong FenceValue { get; set; }
        public long StartTime { get; set; }
        public ulong FrameId { get; set; }
        public bool IsDisposed { get; private set; }
        
        public void Dispose()
        {
            IsDisposed = true;
        }
        
        public double ElapsedMs => (Stopwatch.GetTimestamp() - StartTime) / (double)Stopwatch.Frequency * 1000.0;
    }
    
    public class FenceInfo
    {
        public ID3D12Fence Fence { get; set; }
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