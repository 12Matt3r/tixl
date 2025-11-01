using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TiXL.Core.Graphics.DirectX12
{
    /// <summary>
    /// GPU Timeline Profiler for DirectX 12
    /// Provides detailed GPU timing queries for synchronization optimization
    /// </summary>
    public class GpuTimelineProfiler : IDisposable
    {
        private readonly ConcurrentQueue<TimelineEntry> _timelineEntries = new ConcurrentQueue<TimelineEntry>();
        private readonly Dictionary<string, GpuQueryPool> _queryPools = new Dictionary<string, GpuQueryPool>();
        private readonly object _poolLock = new object();
        
        private readonly int _maxTimelineEntries = 1000;
        private readonly int _maxEntriesPerFrame = 100;
        
        // Performance tracking
        private readonly CircularBuffer<double> _frameGpuTimes = new CircularBuffer<double>(300);
        private readonly CircularBuffer<double> _gpuUtilizationHistory = new CircularBuffer<double>(300);
        private readonly object _metricsLock = new object();
        
        // Profiling state
        private readonly Stopwatch _globalTimer = new Stopwatch();
        private bool _isEnabled = true;
        
        public int ActiveQueryCount { get; private set; }
        public int TimelineEntryCount => _timelineEntries.Count;
        public GpuProfilerStatistics CurrentStatistics { get; private set; }
        
        public GpuTimelineProfiler()
        {
            _globalTimer.Start();
            
            // Initialize standard query pools
            InitializeStandardPools();
        }
        
        /// <summary>
        /// Begin GPU timing for an operation
        /// </summary>
        public GpuTimingHandle BeginTiming(string operationName, GpuTimingType timingType = GpuTimingType.General)
        {
            if (!_isEnabled) return new GpuTimingHandle();
            
            var handle = GetQueryHandle(operationName, timingType);
            
            // Record CPU-side timing start
            handle.CpuStartTimestamp = _globalTimer.Elapsed.TotalMilliseconds;
            handle.GpuStartTimestamp = QueryGpuTimestamp(); // Simulated GPU timestamp
            
            ActiveQueryCount++;
            
            return handle;
        }
        
        /// <summary>
        /// End GPU timing for an operation and record results
        /// </summary>
        public void EndTiming(ref GpuTimingHandle handle)
        {
            if (!_isEnabled || !handle.IsValid) return;
            
            try
            {
                handle.CpuEndTimestamp = _globalTimer.Elapsed.TotalMilliseconds;
                handle.GpuEndTimestamp = QueryGpuTimestamp();
                
                // Create timeline entry
                var entry = new TimelineEntry
                {
                    Id = handle.QueryId,
                    OperationName = handle.OperationName,
                    TimingType = handle.TimingType,
                    CpuStartMs = handle.CpuStartTimestamp,
                    CpuEndMs = handle.CpuEndTimestamp,
                    GpuStartMs = handle.GpuStartTimestamp,
                    GpuEndMs = handle.GpuEndTimestamp,
                    FrameId = GetCurrentFrameId(),
                    Timestamp = _globalTimer.Elapsed.TotalMilliseconds
                };
                
                // Add to timeline
                _timelineEntries.Enqueue(entry);
                TrimTimelineIfNeeded();
                
                // Update statistics
                UpdateStatistics(entry);
                
                // Return query to pool
                ReturnQueryHandle(handle);
                
                ActiveQueryCount--;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GPU timing end failed: {ex.Message}");
                ActiveQueryCount = Math.Max(0, ActiveQueryCount - 1);
            }
        }
        
        /// <summary>
        /// Begin GPU timing for an operation (async pattern)
        /// </summary>
        public async Task<GpuTimingHandle> BeginTimingAsync(string operationName, GpuTimingType timingType = GpuTimingType.General)
        {
            return await Task.Run(() => BeginTiming(operationName, timingType));
        }
        
        /// <summary>
        /// Profile a GPU operation with automatic timing
        /// </summary>
        public async Task ProfileOperationAsync(string operationName, Func<Task> gpuOperation, GpuTimingType timingType = GpuTimingType.General)
        {
            var handle = BeginTiming(operationName, timingType);
            
            try
            {
                await gpuOperation();
            }
            finally
            {
                EndTiming(ref handle);
            }
        }
        
        /// <summary>
        /// Get GPU timeline analysis for recent frames
        /// </summary>
        public GpuTimelineAnalysis GetTimelineAnalysis(int frameCount = 60)
        {
            var recentEntries = GetRecentTimelineEntries(frameCount);
            if (recentEntries.Count == 0) return null;
            
            // Calculate metrics
            var gpuTimes = recentEntries.Select(e => e.GpuDurationMs).ToArray();
            var cpuTimes = recentEntries.Select(e => e.CpuDurationMs).ToArray();
            
            var avgGpuTime = gpuTimes.Average();
            var avgCpuTime = cpuTimes.Average();
            var gpuVariance = CalculateVariance(gpuTimes);
            var cpuVariance = CalculateVariance(cpuTimes);
            
            var operationBreakdown = recentEntries
                .GroupBy(e => e.OperationName)
                .ToDictionary(g => g.Key, g => g.Average(e => e.GpuDurationMs));
            
            var timingTypeBreakdown = recentEntries
                .GroupBy(e => e.TimingType)
                .ToDictionary(g => g.Key.ToString(), g => g.Average(e => e.GpuDurationMs));
            
            return new GpuTimelineAnalysis
            {
                AverageGpuTime = avgGpuTime,
                GpuTimeVariance = gpuVariance,
                AverageCpuTime = avgCpuTime,
                CpuTimeVariance = cpuVariance,
                TotalGpuTime = gpuTimes.Sum(),
                TotalCpuTime = cpuTimes.Sum(),
                FrameCount = recentEntries.Count,
                OperationBreakdown = operationBreakdown,
                TimingTypeBreakdown = timingTypeBreakdown,
                Timestamp = DateTime.UtcNow
            };
        }
        
        /// <summary>
        /// Get GPU utilization analysis
        /// </summary>
        public GpuUtilizationAnalysis GetUtilizationAnalysis(int frameCount = 60)
        {
            var recentFrames = GetRecentTimelineEntries(frameCount);
            if (recentFrames.Count == 0) return null;
            
            var frameGroups = recentFrames
                .GroupBy(e => e.FrameId)
                .OrderBy(g => g.Key)
                .ToArray();
            
            var utilizationData = new List<double>();
            var bottlenecks = new List<string>();
            
            foreach (var frameGroup in frameGroups)
            {
                var totalGpuTime = frameGroup.Sum(e => e.GpuDurationMs);
                var frameDuration = 16.67; // Target 60 FPS
                var utilization = Math.Min(100.0, (totalGpuTime / frameDuration) * 100.0);
                utilizationData.Add(utilization);
            }
            
            // Identify bottlenecks
            var avgUtilization = utilizationData.Average();
            if (avgUtilization > 90.0) bottlenecks.Add("High GPU Utilization");
            
            var utilVariance = CalculateVariance(utilizationData);
            if (utilVariance > 100.0) bottlenecks.Add("Inconsistent GPU Usage");
            
            var spikes = utilizationData.Count(u => u > 95.0);
            if (spikes > utilizationData.Count * 0.1) bottlenecks.Add("GPU Performance Spikes");
            
            return new GpuUtilizationAnalysis
            {
                AverageUtilization = avgUtilization,
                MinUtilization = utilizationData.Min(),
                MaxUtilization = utilizationData.Max(),
                UtilizationVariance = utilVariance,
                Spikes = spikes,
                Bottlenecks = bottlenecks,
                SampleCount = utilizationData.Count,
                Timestamp = DateTime.UtcNow
            };
        }
        
        /// <summary>
        /// Enable or disable profiling
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
        }
        
        /// <summary>
        /// Clear all timeline data
        /// </summary>
        public void ClearTimeline()
        {
            while (_timelineEntries.TryDequeue(out _)) { }
            _frameGpuTimes.Clear();
            _gpuUtilizationHistory.Clear();
            
            lock (_metricsLock)
            {
                CurrentStatistics = new GpuProfilerStatistics();
            }
        }
        
        /// <summary>
        /// Get current profiling statistics
        /// </summary>
        public GpuProfilerStatistics GetStatistics()
        {
            lock (_metricsLock)
            {
                return new GpuProfilerStatistics
                {
                    ActiveQueryCount = ActiveQueryCount,
                    TimelineEntryCount = _timelineEntries.Count,
                    AverageGpuTime = _frameGpuTimes.GetAverage(),
                    GpuUtilization = _gpuUtilizationHistory.GetAverage(),
                    QueryPoolCount = _queryPools.Count,
                    IsEnabled = _isEnabled,
                    TotalTimelineEntries = _timelineEntries.Count,
                    Timestamp = DateTime.UtcNow
                };
            }
        }
        
        private GpuTimingHandle GetQueryHandle(string operationName, GpuTimingType timingType)
        {
            var queryId = Guid.NewGuid().ToString();
            
            lock (_poolLock)
            {
                if (!_queryPools.TryGetValue(operationName, out var pool))
                {
                    pool = new GpuQueryPool(operationName);
                    _queryPools[operationName] = pool;
                }
                
                return pool.GetHandle(queryId, operationName, timingType);
            }
        }
        
        private void ReturnQueryHandle(GpuTimingHandle handle)
        {
            if (string.IsNullOrEmpty(handle.QueryId)) return;
            
            lock (_poolLock)
            {
                if (_queryPools.TryGetValue(handle.QueryPoolName, out var pool))
                {
                    pool.ReturnHandle(handle);
                }
            }
        }
        
        private void InitializeStandardPools()
        {
            // Initialize query pools for common operations
            var standardOperations = new[]
            {
                "Frame Total",
                "Vertex Processing",
                "Pixel Processing",
                "Texture Upload",
                "Buffer Upload",
                "Clear Pass",
                "Shadow Pass",
                "Lighting Pass",
                "Post Processing"
            };
            
            foreach (var operation in standardOperations)
            {
                _queryPools[operation] = new GpuQueryPool(operation);
            }
        }
        
        private double QueryGpuTimestamp()
        {
            // Simulate GPU timestamp query
            // In real implementation, this would use DirectX 12 timestamp queries
            var cpuTime = _globalTimer.Elapsed.TotalMilliseconds;
            
            // Add some realistic GPU processing delay simulation
            return cpuTime * 0.95; // GPU typically runs slightly behind CPU
        }
        
        private ulong GetCurrentFrameId()
        {
            // In real implementation, this would come from the frame counter
            return (ulong)(_globalTimer.Elapsed.TotalSeconds * 60); // ~60 FPS assumption
        }
        
        private List<TimelineEntry> GetRecentTimelineEntries(int count)
        {
            return _timelineEntries.Take(count).Reverse().Take(count).Reverse().ToList();
        }
        
        private void TrimTimelineIfNeeded()
        {
            while (_timelineEntries.Count > _maxTimelineEntries)
            {
                _timelineEntries.TryDequeue(out _);
            }
        }
        
        private void UpdateStatistics(TimelineEntry entry)
        {
            lock (_metricsLock)
            {
                // Update frame GPU times
                _frameGpuTimes.Add(entry.GpuDurationMs);
                
                // Update GPU utilization
                var frameUtilization = Math.Min(100.0, (entry.GpuDurationMs / 16.67) * 100.0);
                _gpuUtilizationHistory.Add(frameUtilization);
                
                // Update current statistics
                var stats = GetStatistics();
                CurrentStatistics = stats;
            }
        }
        
        private static double CalculateVariance(double[] values)
        {
            if (values.Length < 2) return 0.0;
            
            var mean = values.Average();
            return values.Sum(x => (x - mean) * (x - mean)) / values.Length;
        }
        
        public void Dispose()
        {
            _globalTimer?.Stop();
            
            // Clear all data
            ClearTimeline();
            
            // Dispose query pools
            lock (_poolLock)
            {
                foreach (var pool in _queryPools.Values)
                {
                    pool?.Dispose();
                }
                _queryPools.Clear();
            }
        }
    }
    
    /// <summary>
    /// GPU timing handle for profiling operations
    /// </summary>
    public struct GpuTimingHandle
    {
        public string QueryId { get; set; }
        public string QueryPoolName { get; set; }
        public string OperationName { get; set; }
        public GpuTimingType TimingType { get; set; }
        public double CpuStartTimestamp { get; set; }
        public double CpuEndTimestamp { get; set; }
        public double GpuStartTimestamp { get; set; }
        public double GpuEndTimestamp { get; set; }
        
        public bool IsValid => !string.IsNullOrEmpty(QueryId);
        public double CpuDurationMs => CpuEndTimestamp - CpuStartTimestamp;
        public double GpuDurationMs => GpuEndTimestamp - GpuStartTimestamp;
    }
    
    /// <summary>
    /// Timeline entry for GPU operations
    /// </summary>
    public class TimelineEntry
    {
        public string Id { get; set; }
        public string OperationName { get; set; }
        public GpuTimingType TimingType { get; set; }
        public double CpuStartMs { get; set; }
        public double CpuEndMs { get; set; }
        public double GpuStartMs { get; set; }
        public double GpuEndMs { get; set; }
        public ulong FrameId { get; set; }
        public double Timestamp { get; set; }
        
        public double CpuDurationMs => CpuEndMs - CpuStartMs;
        public double GpuDurationMs => GpuEndMs - GpuStartMs;
    }
    
    /// <summary>
    /// GPU timing types
    /// </summary>
    public enum GpuTimingType
    {
        General,
        VertexProcessing,
        PixelProcessing,
        Compute,
        Copy,
        Present,
        Clear,
        PostProcess,
        Shadow,
        Lighting
    }
    
    /// <summary>
    /// GPU query pool for efficient handle management
    /// </summary>
    public class GpuQueryPool : IDisposable
    {
        private readonly string _operationName;
        private readonly Stack<GpuTimingHandle> _availableHandles = new Stack<GpuTimingHandle>();
        private readonly object _poolLock = new object();
        
        public string OperationName => _operationName;
        public int AvailableHandles => _availableHandles.Count;
        
        public GpuQueryPool(string operationName)
        {
            _operationName = operationName;
            
            // Pre-allocate some handles
            for (int i = 0; i < 4; i++)
            {
                _availableHandles.Push(new GpuTimingHandle());
            }
        }
        
        public GpuTimingHandle GetHandle(string queryId, string operationName, GpuTimingType timingType)
        {
            lock (_poolLock)
            {
                GpuTimingHandle handle;
                
                if (_availableHandles.Count > 0)
                {
                    handle = _availableHandles.Pop();
                }
                else
                {
                    handle = new GpuTimingHandle();
                }
                
                handle.QueryId = queryId;
                handle.QueryPoolName = _operationName;
                handle.OperationName = operationName;
                handle.TimingType = timingType;
                
                return handle;
            }
        }
        
        public void ReturnHandle(GpuTimingHandle handle)
        {
            lock (_poolLock)
            {
                // Reset handle and return to pool
                handle.QueryId = string.Empty;
                handle.CpuStartTimestamp = 0;
                handle.CpuEndTimestamp = 0;
                handle.GpuStartTimestamp = 0;
                handle.GpuEndTimestamp = 0;
                
                _availableHandles.Push(handle);
            }
        }
        
        public void Dispose()
        {
            lock (_poolLock)
            {
                _availableHandles.Clear();
            }
        }
    }
    
    /// <summary>
    /// GPU profiler statistics
    /// </summary>
    public class GpuProfilerStatistics
    {
        public int ActiveQueryCount { get; set; }
        public int TimelineEntryCount { get; set; }
        public double AverageGpuTime { get; set; }
        public double GpuUtilization { get; set; }
        public int QueryPoolCount { get; set; }
        public bool IsEnabled { get; set; }
        public int TotalTimelineEntries { get; set; }
        public DateTime Timestamp { get; set; }
        
        public double Fps => AverageGpuTime > 0 ? 1000.0 / AverageGpuTime : 0;
    }
}