using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TiXL.Core.Performance
{
    /// <summary>
    /// High-performance real-time performance monitoring system for TiXL
    /// Supports sub-millisecond precision timing and comprehensive metrics collection
    /// </summary>
    public class PerformanceMonitor : IDisposable
    {
        private readonly CircularBuffer<FrameMetrics> _frameMetrics;
        private readonly PerformanceCounter _cpuCounter;
        private readonly GcMetrics _gcMetrics;
        private readonly ThreadLocal<FrameTimer> _frameTimer;
        private readonly object _metricsLock = new object();
        private readonly Timer _metricsCollectionTimer;
        
        // Performance thresholds for alerting
        private readonly double _targetFrameTime = 16.67; // 60 FPS
        private readonly double _criticalFrameTime = 33.33; // 30 FPS
        private readonly double _maxFrameVariance = 5.0; // 5ms variance
        
        public event EventHandler<PerformanceAlert> PerformanceAlert;
        
        public PerformanceMonitor(int historySize = 300)
        {
            _frameMetrics = new CircularBuffer<FrameMetrics>(historySize);
            _frameTimer = new ThreadLocal<FrameTimer>(() => new FrameTimer());
            _gcMetrics = new GcMetrics();
            
            // Initialize CPU counter (Windows Performance Counter)
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            }
            catch
            {
                _cpuCounter = null; // Fallback if counters unavailable
            }
            
            // Start background metrics collection
            _metricsCollectionTimer = new Timer(CollectBackgroundMetrics, null, 0, 16); // ~60Hz
        }
        
        /// <summary>
        /// Begin timing a new frame
        /// </summary>
        public void BeginFrame()
        {
            _frameTimer.Value.BeginFrame();
        }
        
        /// <summary>
        /// End timing current frame and record metrics
        /// </summary>
        public void EndFrame()
        {
            _frameTimer.Value.EndFrame();
            
            var metrics = _frameTimer.Value.GetMetrics();
            
            // Add system metrics
            metrics.CpuUsage = _cpuCounter?.GetCurrentValue() ?? 0;
            metrics.GcCollections = _gcMetrics.GetCollectionsLastFrame();
            metrics.MemoryUsage = _gcMetrics.GetMemoryUsage();
            metrics.ThreadPoolThreads = ThreadPool.ThreadCount;
            
            // Add to circular buffer
            lock (_metricsLock)
            {
                _frameMetrics.Add(metrics);
            }
            
            // Check for performance issues
            CheckPerformanceAlerts(metrics);
        }
        
        /// <summary>
        /// Get current frame analysis with performance metrics
        /// </summary>
        public FrameAnalysis GetFrameAnalysis()
        {
            FrameMetrics[] frames;
            
            lock (_metricsLock)
            {
                frames = _frameMetrics.GetRecentFrames(60).ToArray(); // Last 60 frames
            }
            
            if (frames.Length < 10)
                return null;
            
            var avgFrameTime = frames.Average(f => f.TotalTime);
            var frameVariance = CalculateVariance(frames.Select(f => f.TotalTime));
            var cpuVariance = CalculateVariance(frames.Select(f => f.CpuTime));
            var gpuVariance = CalculateVariance(frames.Select(f => f.GpuTime));
            var droppedFrames = frames.Count(f => f.TotalTime > _criticalFrameTime);
            
            return new FrameAnalysis
            {
                AverageFrameTime = avgFrameTime,
                FrameTimeVariance = frameVariance,
                CpuTimeVariance = cpuVariance,
                GpuTimeVariance = gpuVariance,
                AverageFps = 1000.0 / avgFrameTime,
                DroppedFrames = droppedFrames,
                PerformanceGrade = CalculatePerformanceGrade(avgFrameTime, frameVariance),
                MinFrameTime = frames.Min(f => f.TotalTime),
                MaxFrameTime = frames.Max(f => f.TotalTime),
                AverageCpuTime = frames.Average(f => f.CpuTime),
                AverageGpuTime = frames.Average(f => f.GpuTime),
                FrameCount = frames.Length,
                Timestamp = DateTime.UtcNow
            };
        }
        
        /// <summary>
        /// Get current frame time in milliseconds
        /// </summary>
        public double GetCurrentFrameTime()
        {
            return _frameTimer.Value?.CurrentFrameTime ?? 0;
        }
        
        /// <summary>
        /// Record custom performance metric
        /// </summary>
        public void RecordCustomMetric(string name, double value)
        {
            var metrics = _frameTimer.Value?.GetMetrics();
            if (metrics != null)
            {
                if (metrics.CustomMetrics == null)
                    metrics.CustomMetrics = new Dictionary<string, double>();
                    
                metrics.CustomMetrics[name] = value;
            }
        }
        
        /// <summary>
        /// Start profiling a specific operation
        /// </summary>
        public IDisposable ProfileOperation(string operationName)
        {
            return new OperationProfiler(this, operationName);
        }
        
        private void CollectBackgroundMetrics(object state)
        {
            // Collect system metrics in background
            // This runs on a timer thread, not the render thread
            
            // Update GC metrics
            _gcMetrics.Update();
            
            // Could add additional background metric collection here
            // e.g., memory pressure, thread pool usage, etc.
        }
        
        private void CheckPerformanceAlerts(FrameMetrics metrics)
        {
            if (metrics.TotalTime > _criticalFrameTime)
            {
                OnPerformanceAlert(new PerformanceAlert
                {
                    Type = AlertType.CriticalFrameTime,
                    Message = $"Critical frame time: {metrics.TotalTime:F2}ms",
                    Value = metrics.TotalTime,
                    Threshold = _criticalFrameTime,
                    Timestamp = DateTime.UtcNow
                });
            }
            else if (metrics.TotalTime > _targetFrameTime * 1.2)
            {
                OnPerformanceAlert(new PerformanceAlert
                {
                    Type = AlertType.FrameTimeWarning,
                    Message = $"High frame time: {metrics.TotalTime:F2}ms",
                    Value = metrics.TotalTime,
                    Threshold = _targetFrameTime * 1.2,
                    Timestamp = DateTime.UtcNow
                });
            }
            
            // Check memory pressure
            if (metrics.MemoryUsage > 512 * 1024 * 1024) // 512MB
            {
                OnPerformanceAlert(new PerformanceAlert
                {
                    Type = AlertType.MemoryPressure,
                    Message = $"High memory usage: {metrics.MemoryUsage / (1024 * 1024)}MB",
                    Value = metrics.MemoryUsage,
                    Threshold = 512 * 1024 * 1024,
                    Timestamp = DateTime.UtcNow
                });
            }
        }
        
        private string CalculatePerformanceGrade(double avgFrameTime, double variance)
        {
            var stdDev = Math.Sqrt(variance);
            
            if (avgFrameTime < 16.67 && stdDev < 2.0) return "A+";
            if (avgFrameTime < 20.0 && stdDev < 4.0) return "A";
            if (avgFrameTime < 25.0 && stdDev < 8.0) return "B";
            if (avgFrameTime < 33.3 && stdDev < 12.0) return "C";
            return "D";
        }
        
        private static double CalculateVariance(IEnumerable<double> values)
        {
            var valuesList = values.ToList();
            if (valuesList.Count < 2) return 0.0;
            
            var mean = valuesList.Average();
            return valuesList.Sum(x => (x - mean) * (x - mean)) / valuesList.Count;
        }
        
        protected virtual void OnPerformanceAlert(PerformanceAlert alert)
        {
            PerformanceAlert?.Invoke(this, alert);
        }
        
        public void Dispose()
        {
            _metricsCollectionTimer?.Dispose();
            _frameTimer?.Dispose();
            _cpuCounter?.Dispose();
        }
    }
    
    /// <summary>
    /// Frame-level performance timer
    /// </summary>
    public class FrameTimer
    {
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private double _cpuTime;
        private double _gpuTime;
        private double _ioTime;
        private readonly ConcurrentDictionary<string, double> _operationTimes = new ConcurrentDictionary<string, double>();
        
        public double CurrentFrameTime { get; private set; }
        
        public void BeginFrame()
        {
            _stopwatch.Restart();
            _cpuTime = 0;
            _gpuTime = 0;
            _ioTime = 0;
            _operationTimes.Clear();
        }
        
        public void EndFrame()
        {
            _stopwatch.Stop();
            CurrentFrameTime = _stopwatch.Elapsed.TotalMilliseconds;
        }
        
        public void RecordCpuTime(double timeMs)
        {
            _cpuTime = timeMs;
        }
        
        public void RecordGpuTime(double timeMs)
        {
            _gpuTime = timeMs;
        }
        
        public void RecordIoTime(double timeMs)
        {
            _ioTime = timeMs;
        }
        
        public void RecordOperationTime(string operationName, double timeMs)
        {
            _operationTimes.TryAdd(operationName, timeMs);
        }
        
        public FrameMetrics GetMetrics()
        {
            return new FrameMetrics
            {
                CpuTime = _cpuTime,
                GpuTime = _gpuTime,
                IoTime = _ioTime,
                TotalTime = CurrentFrameTime,
                Timestamp = Stopwatch.GetTimestamp(),
                OperationTimes = new Dictionary<string, double>(_operationTimes)
            };
        }
    }
    
    /// <summary>
    /// Performance metrics for a single frame
    /// </summary>
    public class FrameMetrics
    {
        public double CpuTime { get; set; }
        public double GpuTime { get; set; }
        public double IoTime { get; set; }
        public double TotalTime { get; set; }
        public long Timestamp { get; set; }
        public double CpuUsage { get; set; }
        public int GcCollections { get; set; }
        public long MemoryUsage { get; set; }
        public int ThreadPoolThreads { get; set; }
        public Dictionary<string, double> OperationTimes { get; set; } = new Dictionary<string, double>();
        public Dictionary<string, double> CustomMetrics { get; set; } = new Dictionary<string, double>();
    }
    
    /// <summary>
    /// Analysis of recent frame performance
    /// </summary>
    public class FrameAnalysis
    {
        public double AverageFrameTime { get; set; }
        public double FrameTimeVariance { get; set; }
        public double CpuTimeVariance { get; set; }
        public double GpuTimeVariance { get; set; }
        public double AverageFps { get; set; }
        public int DroppedFrames { get; set; }
        public string PerformanceGrade { get; set; }
        public double MinFrameTime { get; set; }
        public double MaxFrameTime { get; set; }
        public double AverageCpuTime { get; set; }
        public double AverageGpuTime { get; set; }
        public int FrameCount { get; set; }
        public DateTime Timestamp { get; set; }
        
        public List<string> Recommendations { get; set; } = new List<string>();
        
        public bool MeetsPerformanceTarget()
        {
            return AverageFrameTime <= 16.67 && Math.Sqrt(FrameTimeVariance) <= 2.0;
        }
    }
    
    /// <summary>
    /// Performance alert for real-time issues
    /// </summary>
    public class PerformanceAlert : EventArgs
    {
        public AlertType Type { get; set; }
        public string Message { get; set; }
        public double Value { get; set; }
        public double Threshold { get; set; }
        public DateTime Timestamp { get; set; }
    }
    
    public enum AlertType
    {
        FrameTimeWarning,
        CriticalFrameTime,
        MemoryPressure,
        GcPressure,
        ThreadPoolExhaustion
    }
    
    /// <summary>
    /// Profiler for measuring operation performance
    /// </summary>
    public class OperationProfiler : IDisposable
    {
        private readonly PerformanceMonitor _monitor;
        private readonly string _operationName;
        private readonly Stopwatch _stopwatch = new Stopwatch();
        
        public OperationProfiler(PerformanceMonitor monitor, string operationName)
        {
            _monitor = monitor;
            _operationName = operationName;
            _stopwatch.Start();
        }
        
        public void Dispose()
        {
            _stopwatch.Stop();
            _monitor._frameTimer.Value?.RecordOperationTime(_operationName, _stopwatch.Elapsed.TotalMilliseconds);
        }
    }
}
