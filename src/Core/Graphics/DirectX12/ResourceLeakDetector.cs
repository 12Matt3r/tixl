using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Vortice.Windows.Direct3D12;

namespace TiXL.Core.Graphics.DirectX12
{
    /// <summary>
    /// Resource leak detection and debugging system for DirectX 12 resources
    /// Tracks resource creation, usage, and lifetime to identify memory leaks
    /// </summary>
    public class ResourceLeakDetector : IDisposable
    {
        private readonly ConcurrentDictionary<IntPtr, ResourceTrackingInfo> _trackedResources = new();
        private readonly ConcurrentQueue<CreationEvent> _creationEvents = new();
        private readonly object _trackingLock = new();
        private readonly Dictionary<string, CreationStatistics> _creationStats = new();
        private readonly object _statsLock = new object();
        private bool _disposed = false;
        
        // Configuration
        private readonly TimeSpan _maxResourceAge = TimeSpan.FromMinutes(30); // Resources older than this are considered leaks
        private readonly int _maxEvents = 10000; // Maximum events to keep in memory
        private readonly bool _enableDetailedTracking = true;

        public ResourceLeakDetector()
        {
            // Subscribe to process exit to dump leak report
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        }

        /// <summary>
        /// Record creation of a new DirectX resource
        /// </summary>
        public void RecordResourceCreation(TrackedResource trackedResource)
        {
            if (_disposed || trackedResource?.Resource == null) return;

            var trackingInfo = new ResourceTrackingInfo
            {
                Resource = trackedResource.Resource,
                NativePointer = trackedResource.Resource.NativePointer,
                ResourceType = trackedResource.ResourceType,
                DebugName = trackedResource.DebugName,
                CreationTime = trackedResource.CreationTime,
                SizeInBytes = trackedResource.SizeInBytes,
                Width = trackedResource.Width,
                Height = trackedResource.Height,
                Format = trackedResource.Format,
                QueryCount = trackedResource.QueryCount,
                AllocationStack = _enableDetailedTracking ? new StackTrace(true) : null,
                LastAccessTime = DateTime.UtcNow
            };

            _trackedResources[trackingInfo.NativePointer] = trackingInfo;

            // Record creation event
            RecordCreationEvent(trackingInfo);

            // Update statistics
            UpdateCreationStatistics(trackedResource.ResourceType, trackingInfo.CreationTime);

            System.Diagnostics.Debug.WriteLine($"[ResourceLeakDetector] Created {trackedResource.ResourceType}: {trackedResource.DebugName} (0x{trackedResource.Resource.NativePointer:X})");
        }

        /// <summary>
        /// Record usage/access to a tracked resource
        /// </summary>
        public void RecordResourceAccess(ID3D12Resource resource)
        {
            if (_disposed || resource == null) return;

            var nativePtr = resource.NativePointer;
            if (_trackedResources.TryGetValue(nativePtr, out var trackingInfo))
            {
                trackingInfo.LastAccessTime = DateTime.UtcNow;
                trackingInfo.AccessCount++;
            }
        }

        /// <summary>
        /// Record disposal of a tracked resource
        /// </summary>
        public void RecordResourceDisposal(ID3D12Resource resource)
        {
            if (_disposed || resource == null) return;

            var nativePtr = resource.NativePointer;
            if (_trackedResources.TryRemove(nativePtr, out var trackingInfo))
            {
                var age = DateTime.UtcNow - trackingInfo.CreationTime;
                var accessRate = trackingInfo.AccessCount / age.TotalSeconds;

                System.Diagnostics.Debug.WriteLine($"[ResourceLeakDetector] Disposed {trackingInfo.ResourceType}: {trackingInfo.DebugName} " +
                    $"(Age: {age:mm\\:ss}, Accesses: {trackingInfo.AccessCount}, Rate: {accessRate:F2}/s)");

                // Record disposal event
                RecordDisposalEvent(trackingInfo);
            }
        }

        /// <summary>
        /// Generate comprehensive leak report
        /// </summary>
        public ResourceLeakReport GenerateReport(Dictionary<IntPtr, TrackedResource> currentResources = null)
        {
            if (_disposed) return new ResourceLeakReport();

            lock (_trackingLock)
            {
                var report = new ResourceLeakReport
                {
                    ReportTime = DateTime.UtcNow,
                    TotalResources = _trackedResources.Count
                };

                foreach (var kvp in _trackedResources)
                {
                    var trackingInfo = kvp.Value;
                    var age = report.ReportTime - trackingInfo.CreationTime;

                    var leakedResource = new LeakedResource
                    {
                        ResourceType = trackingInfo.ResourceType,
                        DebugName = trackingInfo.DebugName ?? "Unnamed",
                        Age = age,
                        CreationTime = trackingInfo.CreationTime,
                        NativePointer = trackingInfo.NativePointer,
                        AccessCount = trackingInfo.AccessCount,
                        LastAccessTime = trackingInfo.LastAccessTime,
                        SizeInBytes = trackingInfo.SizeInBytes,
                        Dimensions = trackingInfo.Width.HasValue && trackingInfo.Height.HasValue ? 
                            $"{trackingInfo.Width}x{trackingInfo.Height}" : null,
                        Format = trackingInfo.Format?.ToString(),
                        IsLeak = age > _maxResourceAge || trackingInfo.AccessCount == 0
                    };

                    report.LeakedResources.Add(leakedResource);

                    if (leakedResource.IsLeak)
                    {
                        report.LeakCount++;
                        report.TotalLeakedMemory += trackingInfo.SizeInBytes ?? 0;
                    }
                }

                // Add resource type breakdown
                var typeBreakdown = report.LeakedResources.GroupBy(r => r.ResourceType)
                    .ToDictionary(g => g.Key, g => g.Count());
                report.ResourcesByType = typeBreakdown;

                // Add memory breakdown
                var memoryBreakdown = report.LeakedResources.Where(r => r.SizeInBytes.HasValue)
                    .GroupBy(r => r.ResourceType)
                    .ToDictionary(g => g.Key, g => g.Sum(r => r.SizeInBytes.Value));
                report.MemoryByType = memoryBreakdown;

                return report;
            }
        }

        /// <summary>
        /// Get creation statistics for resource types
        /// </summary>
        public Dictionary<string, CreationStatistics> GetCreationStatistics()
        {
            lock (_statsLock)
            {
                return _creationStats.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Clone());
            }
        }

        /// <summary>
        /// Check for recent resource leaks
        /// </summary>
        public List<ResourceLeakAlert> CheckForRecentLeaks(TimeSpan timeWindow)
        {
            var alerts = new List<ResourceLeakAlert>();
            var cutoffTime = DateTime.UtcNow - timeWindow;

            lock (_trackingLock)
            {
                foreach (var kvp in _trackedResources)
                {
                    var trackingInfo = kvp.Value;
                    var age = DateTime.UtcNow - trackingInfo.CreationTime;

                    // Check for various leak conditions
                    if (age > _maxResourceAge)
                    {
                        alerts.Add(new ResourceLeakAlert
                        {
                            AlertType = ResourceLeakAlertType.OldResource,
                            ResourceType = trackingInfo.ResourceType,
                            DebugName = trackingInfo.DebugName,
                            NativePointer = trackingInfo.NativePointer,
                            Age = age,
                            Message = $"Resource {trackingInfo.DebugName} has been alive for {age:mm\\:ss}, exceeding max age of {_maxResourceAge:mm\\:ss}"
                        });
                    }
                    else if (trackingInfo.AccessCount == 0)
                    {
                        alerts.Add(new ResourceLeakAlert
                        {
                            AlertType = ResourceLeakAlertType.UnusedResource,
                            ResourceType = trackingInfo.ResourceType,
                            DebugName = trackingInfo.DebugName,
                            NativePointer = trackingInfo.NativePointer,
                            Age = age,
                            Message = $"Resource {trackingInfo.DebugName} has never been accessed after {age:mm\\:ss}"
                        });
                    }
                    else if (trackingInfo.AccessCount > 1000 && age.TotalMinutes < 1)
                    {
                        alerts.Add(new ResourceLeakAlert
                        {
                            AlertType = ResourceLeakAlertType.HighAccessRate,
                            ResourceType = trackingInfo.ResourceType,
                            DebugName = trackingInfo.DebugName,
                            NativePointer = trackingInfo.NativePointer,
                            Age = age,
                            AccessCount = trackingInfo.AccessCount,
                            Message = $"Resource {trackingInfo.DebugName} has very high access rate: {trackingInfo.AccessCount / age.TotalSeconds:F2}/s"
                        });
                    }
                }
            }

            return alerts;
        }

        /// <summary>
        /// Record creation time for performance metrics
        /// </summary>
        public void RecordCreationTime(string resourceType, double durationMs, bool success)
        {
            lock (_statsLock)
            {
                if (!_creationStats.TryGetValue(resourceType, out var stats))
                {
                    stats = new CreationStatistics();
                    _creationStats[resourceType] = stats;
                }

                stats.TotalCreations++;
                if (success) stats.SuccessfulCreations++;
                else stats.FailedCreations++;

                // Update running average
                var newAverage = ((stats.AverageCreationTime * (stats.TotalCreations - 1)) + durationMs) / stats.TotalCreations;
                stats.AverageCreationTime = newAverage;

                if (success)
                {
                    stats.SuccessfulCreationTimes.Add(durationMs);
                    if (stats.SuccessfulCreationTimes.Count > 1000) // Keep last 1000 times
                    {
                        stats.SuccessfulCreationTimes.RemoveAt(0);
                    }
                }
                else
                {
                    stats.FailedCreationTimes.Add(durationMs);
                }
            }
        }

        /// <summary>
        /// Generate a summary report for debugging
        /// </summary>
        public string GenerateSummaryReport()
        {
            var report = GenerateReport();
            var sb = new StringBuilder();

            sb.AppendLine("=== DirectX Resource Leak Detection Report ===");
            sb.AppendLine($"Generated: {report.ReportTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Total Tracked Resources: {report.TotalResources}");
            sb.AppendLine($"Potential Leaks: {report.LeakCount}");
            sb.AppendLine($"Total Leaked Memory: {FormatBytes(report.TotalLeakedMemory)}");
            sb.AppendLine();

            if (report.ResourcesByType.Any())
            {
                sb.AppendLine("Resources by Type:");
                foreach (var kvp in report.ResourcesByType)
                {
                    sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
                }
                sb.AppendLine();
            }

            if (report.MemoryByType.Any())
            {
                sb.AppendLine("Memory by Type:");
                foreach (var kvp in report.MemoryByType)
                {
                    sb.AppendLine($"  {kvp.Key}: {FormatBytes(kvp.Value)}");
                }
                sb.AppendLine();
            }

            if (report.LeakedResources.Any())
            {
                sb.AppendLine("Potential Leaks:");
                foreach (var leak in report.LeakedResources.Take(10)) // Show first 10
                {
                    sb.AppendLine($"  {leak.ResourceType}: {leak.DebugName} (Age: {leak.Age:mm\\:ss}, Accesses: {leak.AccessCount})");
                }

                if (report.LeakedResources.Count > 10)
                {
                    sb.AppendLine($"  ... and {report.LeakedResources.Count - 10} more");
                }
            }
            else
            {
                sb.AppendLine("No leaks detected!");
            }

            return sb.ToString();
        }

        private void RecordCreationEvent(ResourceTrackingInfo trackingInfo)
        {
            var creationEvent = new CreationEvent
            {
                EventType = "Creation",
                ResourceType = trackingInfo.ResourceType,
                DebugName = trackingInfo.DebugName,
                NativePointer = trackingInfo.NativePointer,
                Timestamp = DateTime.UtcNow
            };

            _creationEvents.Enqueue(creationEvent);

            // Limit event queue size
            while (_creationEvents.Count > _maxEvents)
            {
                _creationEvents.TryDequeue(out _);
            }
        }

        private void RecordDisposalEvent(ResourceTrackingInfo trackingInfo)
        {
            var disposalEvent = new CreationEvent
            {
                EventType = "Disposal",
                ResourceType = trackingInfo.ResourceType,
                DebugName = trackingInfo.DebugName,
                NativePointer = trackingInfo.NativePointer,
                Timestamp = DateTime.UtcNow
            };

            _creationEvents.Enqueue(disposalEvent);

            // Limit event queue size
            while (_creationEvents.Count > _maxEvents)
            {
                _creationEvents.TryDequeue(out _);
            }
        }

        private void UpdateCreationStatistics(string resourceType, DateTime creationTime)
        {
            lock (_statsLock)
            {
                if (!_creationStats.TryGetValue(resourceType, out var stats))
                {
                    stats = new CreationStatistics();
                    _creationStats[resourceType] = stats;
                }

                stats.TotalCreations++;
                stats.LastCreationTime = creationTime;
            }
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:F2} {sizes[order]}";
        }

        private void OnProcessExit(object sender, EventArgs e)
        {
            if (!_disposed)
            {
                var report = GenerateSummaryReport();
                System.Diagnostics.Debug.WriteLine(report);
                
                // Also write to a file if possible
                try
                {
                    var logPath = $"resource_leak_report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt";
                    System.IO.File.WriteAllText(logPath, report);
                    System.Diagnostics.Debug.WriteLine($"Resource leak report written to: {logPath}");
                }
                catch
                {
                    // Ignore file write errors
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            // Generate final report
            var report = GenerateSummaryReport();
            System.Diagnostics.Debug.WriteLine(report);

            // Clean up tracking data
            _trackedResources.Clear();
            while (_creationEvents.TryDequeue(out _)) { }

            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        }
    }

    // Supporting classes and structures

    public class ResourceTrackingInfo
    {
        public ID3D12Resource Resource { get; set; }
        public IntPtr NativePointer { get; set; }
        public string ResourceType { get; set; }
        public string DebugName { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime LastAccessTime { get; set; }
        public StackTrace AllocationStack { get; set; }
        public int AccessCount { get; set; }
        public long? SizeInBytes { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public Format? Format { get; set; }
        public int? QueryCount { get; set; }
    }

    public class CreationEvent
    {
        public string EventType { get; set; }
        public string ResourceType { get; set; }
        public string DebugName { get; set; }
        public IntPtr NativePointer { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class CreationStatistics
    {
        public int TotalCreations { get; set; }
        public int SuccessfulCreations { get; set; }
        public int FailedCreations { get; set; }
        public double AverageCreationTime { get; set; }
        public List<double> SuccessfulCreationTimes { get; set; } = new();
        public List<double> FailedCreationTimes { get; set; } = new();
        public DateTime LastCreationTime { get; set; }

        public CreationStatistics Clone()
        {
            return new CreationStatistics
            {
                TotalCreations = TotalCreations,
                SuccessfulCreations = SuccessfulCreations,
                FailedCreations = FailedCreations,
                AverageCreationTime = AverageCreationTime,
                SuccessfulCreationTimes = new List<double>(SuccessfulCreationTimes),
                FailedCreationTimes = new List<double>(FailedCreationTimes),
                LastCreationTime = LastCreationTime
            };
        }
    }

    public enum ResourceLeakAlertType
    {
        OldResource,
        UnusedResource,
        HighAccessRate,
        MemoryThresholdExceeded
    }

    public class ResourceLeakAlert
    {
        public ResourceLeakAlertType AlertType { get; set; }
        public string ResourceType { get; set; }
        public string DebugName { get; set; }
        public IntPtr NativePointer { get; set; }
        public TimeSpan Age { get; set; }
        public int AccessCount { get; set; }
        public DateTime LastAccessTime { get; set; }
        public string Message { get; set; }
    }
}