using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharpDX.Direct3D11;
using T3.Core.Memory;
using T3.Core.Profiling;
using T3.Core.Rendering.Pools;
using T3.Core.Resources;

namespace TiXL.MemoryProfiler
{
    /// <summary>
    /// Advanced memory profiler tool for TiXL with real-time monitoring capabilities
    /// </summary>
    public class MemoryProfiler : IHostedService, IDisposable
    {
        private readonly ILogger<MemoryProfiler> _logger;
        private readonly MemoryPerformanceMonitor _monitor;
        private readonly DynamicBufferPool _bufferPool;
        private readonly TexturePool _texturePool;
        private readonly SmartResourceManager _resourceManager;
        private readonly Timer _collectionTimer;
        private readonly Timer _analysisTimer;
        private readonly string _outputDirectory;
        
        private readonly ConcurrentQueue<MemorySnapshot> _snapshots = new();
        private readonly Dictionary<string, List<MemorySnapshot>> _snapshotsByLabel = new();
        private long _snapshotCount;
        private readonly object _lock = new();

        public MemoryProfiler(
            ILogger<MemoryProfiler> logger,
            MemoryPerformanceMonitor monitor,
            DynamicBufferPool bufferPool,
            TexturePool texturePool,
            SmartResourceManager resourceManager,
            string outputDirectory = "memory_profiles")
        {
            _logger = logger;
            _monitor = monitor;
            _bufferPool = bufferPool;
            _texturePool = texturePool;
            _resourceManager = resourceManager;
            _outputDirectory = outputDirectory;

            // Initialize timers
            _collectionTimer = new Timer(CollectMetrics, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
            _analysisTimer = new Timer(AnalyzeTrends, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

            // Create output directory
            Directory.CreateDirectory(_outputDirectory);
        }

        /// <summary>
        /// Captures a memory snapshot with detailed analysis
        /// </summary>
        public async Task<DetailedMemorySnapshot> CaptureDetailedSnapshotAsync(string label, string category = "General")
        {
            var basicSnapshot = _monitor.CaptureSnapshot(label);

            // Get additional metrics
            var directXMetrics = await GetDirectXMetricsAsync();
            var poolStatistics = GetPoolStatistics();
            var resourceStatistics = _resourceManager.GetStatistics();
            var gcStatistics = GetGCStatistics();
            var memoryPressure = GetMemoryPressure();

            var detailedSnapshot = new DetailedMemorySnapshot
            {
                BasicSnapshot = basicSnapshot,
                Category = category,
                DirectXMetrics = directXMetrics,
                PoolStatistics = poolStatistics,
                ResourceStatistics = resourceStatistics,
                GCStatistics = gcStatistics,
                MemoryPressure = memoryPressure,
                CollectedAt = DateTime.UtcNow
            };

            StoreSnapshot(detailedSnapshot);
            return detailedSnapshot;
        }

        /// <summary>
        /// Generates a comprehensive memory report
        /// </summary>
        public async Task<MemoryReport> GenerateReportAsync(TimeSpan timeRange)
        {
            var cutoff = DateTime.UtcNow.Subtract(timeRange);
            var relevantSnapshots = _snapshots
                .Where(s => s.Timestamp >= cutoff)
                .OrderBy(s => s.Timestamp)
                .ToList();

            var analysis = new MemoryAnalysis
            {
                TimeRange = timeRange,
                SnapshotCount = relevantSnapshots.Count,
                AverageMemory = relevantSnapshots.Average(s => s.WorkingSet64),
                PeakMemory = relevantSnapshots.Max(s => s.WorkingSet64),
                MinimumMemory = relevantSnapshots.Min(s => s.WorkingSet64),
                MemoryVariance = CalculateMemoryVariance(relevantSnapshots),
                TrendDirection = CalculateTrend(relevantSnapshots)
            };

            var poolStats = GetCombinedPoolStatistics();
            var leakAnalysis = await AnalyzePotentialLeaksAsync(relevantSnapshots);
            var fragmentationAnalysis = AnalyzeMemoryFragmentation(relevantSnapshots);

            var report = new MemoryReport
            {
                Analysis = analysis,
                PoolStatistics = poolStats,
                LeakAnalysis = leakAnalysis,
                FragmentationAnalysis = fragmentationAnalysis,
                Recommendations = GenerateRecommendations(analysis, leakAnalysis, fragmentationAnalysis),
                GeneratedAt = DateTime.UtcNow,
                TimeRange = timeRange
            };

            return report;
        }

        /// <summary>
        /// Starts a continuous profiling session
        /// </summary>
        public async Task StartProfilingSessionAsync(string sessionName, TimeSpan duration)
        {
            _logger.LogInformation("Starting profiling session: {SessionName} for {Duration}", sessionName, duration);

            var sessionDir = Path.Combine(_outputDirectory, $"{sessionName}_{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(sessionDir);

            var sessionEnd = DateTime.UtcNow.Add(duration);
            var category = $"Session_{sessionName}";

            while (DateTime.UtcNow < sessionEnd)
            {
                try
                {
                    var snapshot = await CaptureDetailedSnapshotAsync($"Session_{sessionName}", category);
                    
                    // Save snapshot to file
                    var fileName = Path.Combine(sessionDir, $"snapshot_{Interlocked.Increment(ref _snapshotCount):D4}.json");
                    await SaveSnapshotToFileAsync(snapshot, fileName);
                    
                    await Task.Delay(TimeSpan.FromSeconds(5)); // Every 5 seconds
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during profiling session");
                }
            }

            _logger.LogInformation("Completed profiling session: {SessionName}", sessionName);
        }

        /// <summary>
        /// Exports all collected data to a comprehensive report
        /// </summary>
        public async Task ExportReportAsync(string outputPath, TimeSpan timeRange)
        {
            var report = await GenerateReportAsync(timeRange);
            
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(report, options);
            await File.WriteAllTextAsync(outputPath, json);

            _logger.LogInformation("Exported memory report to: {OutputPath}", outputPath);
        }

        // Implementation details...

        private async Task<DirectXMetrics> GetDirectXMetricsAsync()
        {
            // This would integrate with actual DirectX metrics
            // For now, returning placeholder data
            return new DirectXMetrics
            {
                VideoMemoryTotal = 8 * 1024 * 1024 * 1024L, // 8GB
                VideoMemoryUsed = 2 * 1024 * 1024 * 1024L,  // 2GB used
                BufferCount = _bufferPool.Statistics.TotalCreated,
                TextureCount = _texturePool.Statistics.TotalCreated
            };
        }

        private PoolStatistics GetPoolStatistics()
        {
            return new PoolStatistics
            {
                BufferPool = _bufferPool.Statistics,
                TexturePool = _texturePool.Statistics,
                TotalPooledObjects = _bufferPool.Statistics.TotalCreated + _texturePool.Statistics.TotalCreated,
                ReuseRatio = (_bufferPool.Statistics.ReuseRatio + _texturePool.Statistics.ReuseRatio) / 2.0
            };
        }

        private GCStatistics GetGCStatistics()
        {
            return new GCStatistics
            {
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2),
                TotalAllocatedBytes = GC.GetTotalMemory(false),
                Gen0Threshold = GC.MaxGeneration,
                MemoryPressure = GC.GetTotalMemory(false) / (1024.0 * 1024.0 * 1024.0) // GB
            };
        }

        private MemoryPressure GetMemoryPressure()
        {
            var process = Process.GetCurrentProcess();
            return new MemoryPressure
            {
                WorkingSetMB = process.WorkingSet64 / (1024.0 * 1024.0),
                PrivateMemoryMB = process.PrivateMemorySize64 / (1024.0 * 1024.0),
                VirtualMemoryMB = process.VirtualMemorySize64 / (1024.0 * 1024.0),
                HandleCount = process.HandleCount,
                ThreadCount = process.Threads.Count
            };
        }

        private void CollectMetrics(object state)
        {
            try
            {
                var snapshot = _monitor.CaptureSnapshot("Auto");
                _snapshots.Enqueue(snapshot);

                // Keep only last 1000 snapshots in memory
                while (_snapshots.Count > 1000)
                {
                    _snapshots.TryDequeue(out _);
                }

                // Store in category-specific lists
                lock (_lock)
                {
                    if (!_snapshotsByLabel.TryGetValue(snapshot.Label, out var list))
                    {
                        list = new List<MemorySnapshot>();
                        _snapshotsByLabel[snapshot.Label] = list;
                    }
                    list.Add(snapshot);

                    // Keep only last 100 snapshots per label
                    if (list.Count > 100)
                    {
                        list.RemoveRange(0, list.Count - 100);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting memory metrics");
            }
        }

        private void AnalyzeTrends(object state)
        {
            try
            {
                var recentSnapshots = _snapshots.TakeLast(60).ToList(); // Last minute
                if (recentSnapshots.Count > 10)
                {
                    var trend = CalculateTrend(recentSnapshots);
                    var avgMemory = recentSnapshots.Average(s => s.WorkingSet64);
                    var memoryIncrease = recentSnapshots.Last().WorkingSet64 - recentSnapshots.First().WorkingSet64;

                    if (memoryIncrease > 50 * 1024 * 1024) // 50MB increase in last minute
                    {
                        _logger.LogWarning("Rapid memory increase detected: {MemoryIncrease}MB in last minute. Trend: {Trend}",
                            memoryIncrease / (1024.0 * 1024.0), trend);
                    }

                    if (trend > 0.8) // Rapid growth
                    {
                        _logger.LogWarning("High memory growth trend detected: {Trend:F2}", trend);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing memory trends");
            }
        }

        private double CalculateMemoryVariance(List<MemorySnapshot> snapshots)
        {
            if (snapshots.Count < 2) return 0;

            var values = snapshots.Select(s => (double)s.WorkingSet64).ToArray();
            var mean = values.Average();
            var variance = values.Sum(x => Math.Pow(x - mean, 2)) / values.Length;
            return Math.Sqrt(variance);
        }

        private double CalculateTrend(List<MemorySnapshot> snapshots)
        {
            if (snapshots.Count < 2) return 0;

            var ordered = snapshots.OrderBy(s => s.Timestamp).ToArray();
            var n = ordered.Length;
            var sumX = ordered.Select((_, i) => (double)i).Sum();
            var sumY = ordered.Select(s => (double)s.WorkingSet64).Sum();
            var sumXY = ordered.Select((s, i) => (double)i * s.WorkingSet64).Sum();
            var sumXX = ordered.Select((_, i) => (double)i * i).Sum();

            var slope = (n * sumXY - sumX * sumY) / (n * sumXX - sumX * sumX);
            var normalizedSlope = slope / (ordered.Last().WorkingSet64 - ordered.First().WorkingSet64);
            return normalizedSlope;
        }

        private async Task<LeakAnalysis> AnalyzePotentialLeaksAsync(List<MemorySnapshot> snapshots)
        {
            var analysis = new LeakAnalysis
            {
                PotentialLeaks = new List<PotentialLeak>()
            };

            // Analyze each category for potential leaks
            foreach (var kvp in _snapshotsByLabel)
            {
                var label = kvp.Key;
                var labelSnapshots = kvp.Value;

                if (labelSnapshots.Count > 10)
                {
                    var trend = CalculateTrend(labelSnapshots);
                    if (trend > 0.5) // Positive trend indicates potential leak
                    {
                        var peakMemory = labelSnapshots.Max(s => s.WorkingSet64);
                        var memoryIncrease = labelSnapshots.Last().WorkingSet64 - labelSnapshots.First().WorkingSet64;

                        analysis.PotentialLeaks.Add(new PotentialLeak
                        {
                            Label = label,
                            MemoryIncreaseMB = memoryIncrease / (1024.0 * 1024.0),
                            TrendScore = trend,
                            Severity = GetSeverityLevel(trend, memoryIncrease),
                            Description = $"Memory growth trend detected for {label}"
                        });
                    }
                }
            }

            return analysis;
        }

        private FragmentationAnalysis AnalyzeMemoryFragmentation(List<MemorySnapshot> snapshots)
        {
            // This is a simplified analysis - real implementation would use detailed heap information
            var recent = snapshots.TakeLast(10).ToList();
            if (recent.Count < 2)
                return new FragmentationAnalysis { Score = 0, Description = "Insufficient data" };

            var memoryVariance = CalculateMemoryVariance(recent);
            var averageMemory = recent.Average(s => s.WorkingSet64);
            var fragmentationScore = (memoryVariance / averageMemory) * 100;

            return new FragmentationAnalysis
            {
                Score = fragmentationScore,
                Description = fragmentationScore > 20 ? "High fragmentation detected" : 
                             fragmentationScore > 10 ? "Moderate fragmentation" : "Low fragmentation",
                Recommendations = fragmentationScore > 20 ? 
                    new[] { "Consider running memory defragmentation", "Review object pooling effectiveness" } :
                    fragmentationScore > 10 ?
                    new[] { "Monitor memory usage patterns", "Consider optimization opportunities" } :
                    new[] { "Memory fragmentation is within acceptable range" }
            };
        }

        private string[] GenerateRecommendations(MemoryAnalysis analysis, LeakAnalysis leakAnalysis, FragmentationAnalysis fragmentationAnalysis)
        {
            var recommendations = new List<string>();

            if (analysis.TrendDirection > 0.7)
                recommendations.Add("Consider implementing additional memory optimizations - high growth trend detected");

            if (leakAnalysis.PotentialLeaks.Count > 0)
                recommendations.Add($"Investigate {leakAnalysis.PotentialLeaks.Count} potential memory leaks");

            if (fragmentationAnalysis.Score > 20)
                recommendations.Add("Memory fragmentation is high - consider running defragmentation");

            if (analysis.MemoryVariance > analysis.AverageMemory * 0.1)
                recommendations.Add("High memory variance detected - investigate memory spikes");

            if (recommendations.Count == 0)
                recommendations.Add("Memory usage appears stable and optimized");

            return recommendations.ToArray();
        }

        private string GetSeverityLevel(double trend, long memoryIncrease)
        {
            if (trend > 0.8 || memoryIncrease > 100 * 1024 * 1024) // 100MB
                return "High";
            if (trend > 0.6 || memoryIncrease > 50 * 1024 * 1024) // 50MB
                return "Medium";
            return "Low";
        }

        private PoolStatistics GetCombinedPoolStatistics()
        {
            return new PoolStatistics
            {
                BufferPool = _bufferPool.Statistics,
                TexturePool = _texturePool.Statistics,
                TotalPooledObjects = _bufferPool.Statistics.TotalCreated + _texturePool.Statistics.TotalCreated,
                ReuseRatio = (_bufferPool.Statistics.ReuseRatio + _texturePool.Statistics.ReuseRatio) / 2.0
            };
        }

        private void StoreSnapshot(DetailedMemorySnapshot snapshot)
        {
            lock (_lock)
            {
                var categoryList = _snapshotsByLabel.GetOrAdd(snapshot.Category, _ => new List<MemorySnapshot>());
                categoryList.Add(snapshot.BasicSnapshot);

                // Keep only last 100 snapshots per category
                if (categoryList.Count > 100)
                {
                    categoryList.RemoveRange(0, categoryList.Count - 100);
                }
            }
        }

        private async Task SaveSnapshotToFileAsync(DetailedMemorySnapshot snapshot, string fileName)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(snapshot, options);
            await File.WriteAllTextAsync(fileName, json);
        }

        // IHostedService implementation
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Memory Profiler started");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Memory Profiler stopped");
            _collectionTimer?.Dispose();
            _analysisTimer?.Dispose();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _collectionTimer?.Dispose();
            _analysisTimer?.Dispose();
            _monitor?.Dispose();
        }
    }

    // Data models for memory profiling
    public record DetailedMemorySnapshot
    {
        public MemorySnapshot BasicSnapshot { get; init; }
        public string Category { get; init; }
        public DirectXMetrics DirectXMetrics { get; init; }
        public PoolStatistics PoolStatistics { get; init; }
        public ResourceStatistics ResourceStatistics { get; init; }
        public GCStatistics GCStatistics { get; init; }
        public MemoryPressure MemoryPressure { get; init; }
        public DateTime CollectedAt { get; init; }
    }

    public record DirectXMetrics
    {
        public long VideoMemoryTotal { get; init; }
        public long VideoMemoryUsed { get; init; }
        public int BufferCount { get; init; }
        public int TextureCount { get; init; }
    }

    public record PoolStatistics
    {
        public PoolStatistics BufferPool { get; init; }
        public PoolStatistics TexturePool { get; init; }
        public int TotalPooledObjects { get; init; }
        public double ReuseRatio { get; init; }
    }

    public record GCStatistics
    {
        public int Gen0Collections { get; init; }
        public int Gen1Collections { get; init; }
        public int Gen2Collections { get; init; }
        public long TotalAllocatedBytes { get; init; }
        public int Gen0Threshold { get; init; }
        public double MemoryPressure { get; init; }
    }

    public record MemoryPressure
    {
        public double WorkingSetMB { get; init; }
        public double PrivateMemoryMB { get; init; }
        public double VirtualMemoryMB { get; init; }
        public int HandleCount { get; init; }
        public int ThreadCount { get; init; }
    }

    public record MemoryAnalysis
    {
        public TimeSpan TimeRange { get; init; }
        public int SnapshotCount { get; init; }
        public double AverageMemory { get; init; }
        public double PeakMemory { get; init; }
        public double MinimumMemory { get; init; }
        public double MemoryVariance { get; init; }
        public double TrendDirection { get; init; }
    }

    public record LeakAnalysis
    {
        public List<PotentialLeak> PotentialLeaks { get; init; }
    }

    public record PotentialLeak
    {
        public string Label { get; init; }
        public double MemoryIncreaseMB { get; init; }
        public double TrendScore { get; init; }
        public string Severity { get; init; }
        public string Description { get; init; }
    }

    public record FragmentationAnalysis
    {
        public double Score { get; init; }
        public string Description { get; init; }
        public string[] Recommendations { get; init; }
    }

    public record MemoryReport
    {
        public MemoryAnalysis Analysis { get; init; }
        public PoolStatistics PoolStatistics { get; init; }
        public LeakAnalysis LeakAnalysis { get; init; }
        public FragmentationAnalysis FragmentationAnalysis { get; init; }
        public string[] Recommendations { get; init; }
        public DateTime GeneratedAt { get; init; }
        public TimeSpan TimeRange { get; init; }
    }
}
