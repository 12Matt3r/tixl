using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace T3.Core.Operators
{
    /// <summary>
    /// Monitors performance metrics and resource usage for operator evaluations
    /// </summary>
    internal class PerformanceMonitor : IDisposable
    {
        #region Fields

        private readonly GuardrailConfiguration _config;
        private readonly Stopwatch _monitoringStopwatch;
        private readonly ConcurrentDictionary<string, Metric> _metrics;
        private readonly ResourceTracker _resourceTracker;
        private readonly PerformanceCounter _cpuCounter;
        private readonly List<PerformanceWarning> _warnings;
        private readonly object _warningsLock = new();
        private DateTime _lastGcNotification = DateTime.MinValue;

        #endregion

        #region Constructor

        public PerformanceMonitor(GuardrailConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _monitoringStopwatch = Stopwatch.StartNew();
            _metrics = new ConcurrentDictionary<string, Metric>();
            _resourceTracker = new ResourceTracker(config);
            _warnings = new List<PerformanceWarning>();
            
            // Initialize CPU counter if available
            _cpuCounter = TryCreateCpuCounter();

            // Start periodic monitoring
            _ = StartPeriodicMonitoring();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Records a performance metric
        /// </summary>
        public void RecordMetric(string metricName, double value, string unit = "")
        {
            if (string.IsNullOrEmpty(metricName))
                return;

            var metric = _metrics.GetOrAdd(metricName, _ => new Metric(metricName, unit));
            metric.Record(value, _monitoringStopwatch.Elapsed);

            // Check for metric-based limits
            CheckMetricLimits(metricName, value);
        }

        /// <summary>
        /// Tracks resource allocation for memory limit enforcement
        /// </summary>
        public void TrackResourceAllocation(string resourceType, long bytes)
        {
            _resourceTracker.RecordAllocation(resourceType, bytes);
            CheckResourceLimits();
        }

        /// <summary>
        /// Gets current resource usage statistics
        /// </summary>
        public ResourceUsageStatistics GetResourceStatistics()
        {
            return new ResourceUsageStatistics
            {
                MemoryAllocated = GetCurrentMemoryUsage(),
                CpuUsage = GetCurrentCpuUsage(),
                ResourceCounts = _resourceTracker.GetResourceCounts(),
               GcPressure = GetGcPressure(),
                FileHandleCount = GetFileHandleCount(),
                NetworkConnectionCount = GetNetworkConnectionCount()
            };
        }

        /// <summary>
        /// Gets current guardrail status
        /// </summary>
        public GuardrailStatus GetCurrentStatus()
        {
            var memoryUsage = GetCurrentMemoryUsage();
            var cpuUsage = GetCurrentCpuUsage();
            var resourceCounts = _resourceTracker.GetResourceCounts();

            return new GuardrailStatus
            {
                IsHealthy = !HasViolations(),
                MemoryUsage = memoryUsage,
                MemoryLimit = _config.MaxMemoryBytes,
                MemoryUsagePercent = (memoryUsage / (double)_config.MaxMemoryBytes) * 100,
                CpuUsagePercent = cpuUsage,
                CpuLimitPercent = _config.MaxCpuUsagePercent,
                FileHandleCount = resourceCounts.FileHandles,
                FileHandleLimit = _config.MaxFileHandles,
                NetworkConnections = resourceCounts.NetworkConnections,
                NetworkConnectionLimit = _config.MaxNetworkConnections,
                TextureCount = resourceCounts.Textures,
                TextureLimit = _config.MaxTexturesLoaded,
                MaxTextureSize = resourceCounts.MaxTextureSize,
                TextureSizeLimit = _config.MaxTextureSize,
                ActiveWarnings = GetActiveWarningCount(),
                Violations = GetViolationCount()
            };
        }

        /// <summary>
        /// Gets current evaluation metrics
        /// </summary>
        public EvaluationMetrics GetCurrentMetrics()
        {
            return new EvaluationMetrics
            {
                ElapsedTime = _monitoringStopwatch.Elapsed,
                CpuUsagePercent = GetCurrentCpuUsage(),
                MemoryUsageBytes = GetCurrentMemoryUsage(),
                GcPressureBytes = GetGcPressure(),
                MetricsCount = _metrics.Count,
                ResourceCount = _resourceTracker.GetTotalResourceCount(),
                WarningCount = GetActiveWarningCount()
            };
        }

        /// <summary>
        /// Gets all tracked metrics
        /// </summary>
        public IReadOnlyDictionary<string, Metric> GetAllMetrics()
        {
            return _metrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <summary>
        /// Generates a comprehensive performance report
        /// </summary>
        public PerformanceReport GenerateReport()
        {
            var resourceStats = GetResourceStatistics();
            var currentStatus = GetCurrentStatus();
            var metrics = GetAllMetrics();

            return new PerformanceReport
            {
                GeneratedAt = DateTime.UtcNow,
                ElapsedTime = _monitoringStopwatch.Elapsed,
                ResourceStatistics = resourceStats,
                CurrentStatus = currentStatus,
                Metrics = metrics.Values.ToList(),
                Warnings = GetAllWarnings(),
                SystemInfo = GetSystemInfo(),
                Recommendations = GenerateRecommendations(currentStatus, resourceStats)
            };
        }

        /// <summary>
        /// Releases all tracked resources
        /// </summary>
        public void ReleaseAllResources()
        {
            _resourceTracker.ReleaseAll();
        }

        #endregion

        #region Private Methods

        private long GetCurrentMemoryUsage()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                return process.WorkingSet64;
            }
            catch
            {
                return 0;
            }
        }

        private double GetCurrentCpuUsage()
        {
            try
            {
                if (_cpuCounter != null)
                {
                    return _cpuCounter.GetNextValue();
                }
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        private long GetGcPressure()
        {
            try
            {
                // Estimate GC pressure by measuring allocated memory since last GC
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                var totalMemory = GC.GetTotalMemory(false);
                return totalMemory;
            }
            catch
            {
                return 0;
            }
        }

        private int GetFileHandleCount()
        {
            // This is platform-specific and would need implementation
            // For now, return a placeholder value
            return 0;
        }

        private int GetNetworkConnectionCount()
        {
            // This is platform-specific and would need implementation
            // For now, return a placeholder value
            return 0;
        }

        private void CheckMetricLimits(string metricName, double value)
        {
            // Check if we need to warn about too many metrics
            if (_metrics.Count > _config.MaxMetricsTracked)
            {
                RecordWarning("TooManyMetrics", $"Metric count {_metrics.Count} exceeded limit {_config.MaxMetricsTracked}", WarningLevel.Warning);
            }
        }

        private void CheckResourceLimits()
        {
            var resourceStats = GetResourceStatistics();

            // Check memory usage
            if (resourceStats.MemoryAllocated > _config.MaxMemoryBytes * _config.MemoryWarningThreshold)
            {
                RecordWarning("HighMemoryUsage", 
                    $"Memory usage {resourceStats.MemoryAllocated} bytes exceeds warning threshold {_config.MaxMemoryBytes * _config.MemoryWarningThreshold}", 
                    WarningLevel.Warning);
            }

            // Check GC pressure
            if (resourceStats.GcPressure > _config.MaxGcPressureBytes)
            {
                RecordWarning("HighGcPressure", 
                    $"GC pressure {resourceStats.GcPressure} bytes exceeds limit {_config.MaxGcPressureBytes}", 
                    WarningLevel.Warning);
            }

            // Check file handles
            if (resourceStats.FileHandleCount > _config.MaxFileHandles * 0.8)
            {
                RecordWarning("HighFileHandleUsage", 
                    $"File handle usage {resourceStats.FileHandleCount} exceeds 80% of limit {_config.MaxFileHandles}", 
                    WarningLevel.Warning);
            }
        }

        private bool HasViolations()
        {
            lock (_warningsLock)
            {
                return _warnings.Any(w => w.Level == WarningLevel.Error);
            }
        }

        private int GetActiveWarningCount()
        {
            lock (_warningsLock)
            {
                return _warnings.Count(w => w.IsActive);
            }
        }

        private int GetViolationCount()
        {
            lock (_warningsLock)
            {
                return _warnings.Count(w => w.Level == WarningLevel.Error);
            }
        }

        private void RecordWarning(string warningType, string message, WarningLevel level)
        {
            var now = DateTime.UtcNow;
            
            lock (_warningsLock)
            {
                // Check if we should throttle warnings
                var recentWarning = _warnings
                    .Where(w => w.Type == warningType)
                    .OrderByDescending(w => w.Timestamp)
                    .FirstOrDefault();

                if (recentWarning != null && (now - recentWarning.Timestamp) < _config.PerformanceWarningInterval)
                {
                    return; // Throttle the warning
                }

                var warning = new PerformanceWarning
                {
                    Id = Guid.NewGuid(),
                    Type = warningType,
                    Message = message,
                    Level = level,
                    Timestamp = now,
                    IsActive = true
                };

                _warnings.Add(warning);

                // Limit warning history
                if (_warnings.Count > 1000)
                {
                    _warnings.RemoveRange(0, _warnings.Count - 1000);
                }
            }
        }

        private List<PerformanceWarning> GetAllWarnings()
        {
            lock (_warningsLock)
            {
                return _warnings.ToList();
            }
        }

        private List<string> GenerateRecommendations(GuardrailStatus status, ResourceUsageStatistics stats)
        {
            var recommendations = new List<string>();

            if (status.MemoryUsagePercent > 80)
            {
                recommendations.Add("Consider reducing memory usage or increasing memory limits");
            }

            if (status.CpuUsagePercent > 80)
            {
                recommendations.Add("High CPU usage detected - consider optimizing operations");
            }

            if (status.FileHandleCount > status.FileHandleLimit * 0.8)
            {
                recommendations.Add("High file handle usage - ensure proper disposal of file resources");
            }

            if (stats.TextureCount > status.TextureLimit * 0.8)
            {
                recommendations.Add("High texture count - consider texture atlasing or streaming");
            }

            if (_metrics.Count > _config.MaxMetricsTracked * 0.8)
            {
                recommendations.Add("Large number of metrics - consider pruning unused metrics");
            }

            return recommendations;
        }

        private SystemInfo GetSystemInfo()
        {
            return new SystemInfo
            {
                ProcessId = Process.GetCurrentProcess().Id,
                WorkingSet = GetCurrentMemoryUsage(),
                Timestamp = DateTime.UtcNow,
                MachineName = Environment.MachineName,
                ProcessorCount = Environment.ProcessorCount,
                OsVersion = Environment.OSVersion.ToString(),
                Is64BitProcess = Environment.Is64BitProcess,
                Is64BitOperatingSystem = Environment.Is64BitOperatingSystem
            };
        }

        private async Task StartPeriodicMonitoring()
        {
            while (!_disposed)
            {
                try
                {
                    await Task.Delay(1000); // Monitor every second

                    // Check CPU usage
                    var cpuUsage = GetCurrentCpuUsage();
                    if (cpuUsage > _config.MaxCpuUsagePercent)
                    {
                        RecordWarning("HighCpuUsage", $"CPU usage {cpuUsage:F1}% exceeds limit {_config.MaxCpuUsagePercent}%", WarningLevel.Warning);
                    }

                    // Check memory usage
                    var memoryUsage = GetCurrentMemoryUsage();
                    if (memoryUsage > _config.MaxMemoryBytes)
                    {
                        RecordWarning("MemoryLimitExceeded", $"Memory usage {memoryUsage} bytes exceeds limit {_config.MaxMemoryBytes}", WarningLevel.Error);
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception)
                {
                    // Log the exception but continue monitoring
                    continue;
                }
            }
        }

        private PerformanceCounter? TryCreateCpuCounter()
        {
            try
            {
                // This would be platform-specific implementation
                // For now, return null to indicate not available
                return null;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region IDisposable

        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _cpuCounter?.Dispose();
            _resourceTracker?.Dispose();
        }

        #endregion

        #region Supporting Classes

        private class Metric
        {
            public string Name { get; }
            public string Unit { get; }
            private readonly List<double> _values = new();
            private readonly List<TimeSpan> _timestamps = new();

            public Metric(string name, string unit)
            {
                Name = name;
                Unit = unit;
            }

            public void Record(double value, TimeSpan timestamp)
            {
                _values.Add(value);
                _timestamps.Add(timestamp);

                // Limit history size
                if (_values.Count > 1000)
                {
                    _values.RemoveAt(0);
                    _timestamps.RemoveAt(0);
                }
            }

            public double GetAverage() => _values.Count > 0 ? _values.Average() : 0;
            public double GetMin() => _values.Count > 0 ? _values.Min() : 0;
            public double GetMax() => _values.Count > 0 ? _values.Max() : 0;
            public int GetCount() => _values.Count;
            public TimeSpan GetLatestTimestamp() => _timestamps.Count > 0 ? _timestamps[^1] : TimeSpan.Zero;
        }

        private class ResourceTracker : IDisposable
        {
            private readonly GuardrailConfiguration _config;
            private readonly ConcurrentDictionary<string, int> _resourceCounts = new();
            private readonly ConcurrentDictionary<string, long> _memoryAllocations = new();
            private readonly object _lock = new();

            public ResourceTracker(GuardrailConfiguration config)
            {
                _config = config;
            }

            public void RecordAllocation(string resourceType, long bytes)
            {
                _memoryAllocations.AddOrUpdate(resourceType, bytes, (key, oldValue) => oldValue + bytes);
                _resourceCounts.AddOrUpdate(resourceType, 1, (key, count) => count + 1);
            }

            public ResourceCounts GetResourceCounts()
            {
                lock (_lock)
                {
                    return new ResourceCounts
                    {
                        Textures = _resourceCounts.GetValueOrDefault("Texture", 0),
                        MaxTextureSize = _resourceCounts.GetValueOrDefault("MaxTextureSize", 0),
                        FileHandles = 0, // Would be tracked separately
                        NetworkConnections = 0 // Would be tracked separately
                    };
                }
            }

            public int GetTotalResourceCount()
            {
                return _resourceCounts.Values.Sum();
            }

            public void ReleaseAll()
            {
                lock (_lock)
                {
                    _resourceCounts.Clear();
                    _memoryAllocations.Clear();
                }
            }

            public void Dispose()
            {
                ReleaseAll();
            }
        }

        private class PerformanceCounter : IDisposable
        {
            public double GetNextValue() => 0; // Placeholder implementation
            public void Dispose() { }
        }

        #endregion
    }

    #region Supporting Data Types

    /// <summary>
    /// Current status of guardrail limits
    /// </summary>
    public class GuardrailStatus
    {
        public bool IsHealthy { get; set; }
        public long MemoryUsage { get; set; }
        public long MemoryLimit { get; set; }
        public double MemoryUsagePercent { get; set; }
        public double CpuUsagePercent { get; set; }
        public double CpuLimitPercent { get; set; }
        public int FileHandleCount { get; set; }
        public int FileHandleLimit { get; set; }
        public int NetworkConnections { get; set; }
        public int NetworkConnectionLimit { get; set; }
        public int TextureCount { get; set; }
        public int TextureLimit { get; set; }
        public int MaxTextureSize { get; set; }
        public int TextureSizeLimit { get; set; }
        public int ActiveWarnings { get; set; }
        public int Violations { get; set; }
    }

    /// <summary>
    /// Resource usage statistics
    /// </summary>
    public class ResourceUsageStatistics
    {
        public long MemoryAllocated { get; set; }
        public double CpuUsage { get; set; }
        public long GcPressure { get; set; }
        public int FileHandleCount { get; set; }
        public int NetworkConnectionCount { get; set; }
        public ResourceCounts ResourceCounts { get; set; } = new();
    }

    /// <summary>
    /// Counts of various resource types
    /// </summary>
    public class ResourceCounts
    {
        public int Textures { get; set; }
        public int MaxTextureSize { get; set; }
        public int FileHandles { get; set; }
        public int NetworkConnections { get; set; }
    }

    /// <summary>
    /// Current evaluation metrics
    /// </summary>
    public class EvaluationMetrics
    {
        public TimeSpan ElapsedTime { get; set; }
        public double CpuUsagePercent { get; set; }
        public long MemoryUsageBytes { get; set; }
        public long GcPressureBytes { get; set; }
        public int MetricsCount { get; set; }
        public int ResourceCount { get; set; }
        public int WarningCount { get; set; }
    }

    /// <summary>
    /// Comprehensive performance report
    /// </summary>
    public class PerformanceReport
    {
        public DateTime GeneratedAt { get; set; }
        public TimeSpan ElapsedTime { get; set; }
        public ResourceUsageStatistics ResourceStatistics { get; set; } = new();
        public GuardrailStatus CurrentStatus { get; set; } = new();
        public List<Metric> Metrics { get; set; } = new();
        public List<PerformanceWarning> Warnings { get; set; } = new();
        public SystemInfo SystemInfo { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    /// <summary>
    /// Performance warning information
    /// </summary>
    public class PerformanceWarning
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = "";
        public string Message { get; set; } = "";
        public WarningLevel Level { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Warning severity levels
    /// </summary>
    public enum WarningLevel
    {
        Information,
        Warning,
        Error
    }

    /// <summary>
    /// System information snapshot
    /// </summary>
    public class SystemInfo
    {
        public int ProcessId { get; set; }
        public long WorkingSet { get; set; }
        public DateTime Timestamp { get; set; }
        public string MachineName { get; set; } = "";
        public int ProcessorCount { get; set; }
        public string OsVersion { get; set; } = "";
        public bool Is64BitProcess { get; set; }
        public bool Is64BitOperatingSystem { get; set; }
    }

    #endregion
}