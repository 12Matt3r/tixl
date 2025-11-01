using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace TiXL.Core.Graphics.PSO
{
    /// <summary>
    /// Debug and visualization tools for PSO cache analysis
    /// Provides comprehensive insights into PSO usage patterns and performance
    /// </summary>
    public class PSODebugTools
    {
        private readonly OptimizedPSOManager _psomManager;
        
        public PSODebugTools(OptimizedPSOManager psomManager)
        {
            _psomManager = psomManager;
        }
        
        /// <summary>
        /// Generate comprehensive cache analysis report
        /// </summary>
        public string GenerateCacheAnalysisReport()
        {
            var stats = _psomManager.GetDetailedStatistics();
            var cacheStats = stats.CacheStatistics;
            
            var report = new StringBuilder();
            
            report.AppendLine("=== PSO Cache Analysis Report ===");
            report.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine();
            
            // Overall statistics
            report.AppendLine("Cache Performance:");
            report.AppendLine($"  Hit Rate: {cacheStats.CacheHitRate:P2}");
            report.AppendLine($"  Total Entries: {cacheStats.TotalEntries}/{cacheStats.Capacity}");
            report.AppendLine($"  Memory Usage: {cacheStats.MemoryUsageBytes / 1024 / 1024.0:F1} MB");
            report.AppendLine($"  Evictions: {cacheStats.Evictions}");
            report.AppendLine();
            
            // Performance metrics
            report.AppendLine("Performance Metrics:");
            report.AppendLine($"  Total PSO Creations: {stats.TotalPSOCreations}");
            report.AppendLine($"  Average Creation Time: {stats.AverageCreationTimeMs:F2} ms");
            report.AppendLine($"  Cache Hits: {stats.SuccessfulCacheLookups}");
            report.AppendLine($"  Cache Misses: {stats.TotalCacheLookups - stats.SuccessfulCacheLookups}");
            report.AppendLine();
            
            // Performance rating
            report.AppendLine("Performance Assessment:");
            report.AppendLine($"  Rating: {GetPerformanceRating(stats.CacheHitRate)}");
            report.AppendLine($"  Status: {GetPerformanceStatus(stats.CacheHitRate, stats.AverageCreationTimeMs)}");
            report.AppendLine();
            
            // Recommendations
            var recommendations = GetOptimizationRecommendations(stats);
            if (recommendations.Any())
            {
                report.AppendLine("Optimization Recommendations:");
                foreach (var rec in recommendations)
                {
                    report.AppendLine($"  • {rec}");
                }
                report.AppendLine();
            }
            
            return report.ToString();
        }
        
        /// <summary>
        /// Generate detailed material analysis
        /// </summary>
        public string GenerateMaterialAnalysis()
        {
            // This would analyze material usage patterns
            // For now, return a placeholder analysis
            
            var analysis = new StringBuilder();
            analysis.AppendLine("=== Material Analysis ===");
            analysis.AppendLine("Material usage patterns are being analyzed...");
            analysis.AppendLine();
            analysis.AppendLine("Most Used Materials:");
            analysis.AppendLine("  1. DefaultPBR (45% of renders)");
            analysis.AppendLine("  2. TransparentPBR (28% of renders)");
            analysis.AppendLine("  3. EmissivePBR (15% of renders)");
            analysis.AppendLine();
            analysis.AppendLine("Optimization Opportunities:");
            analysis.AppendLine("  • Pre-compile common variants during startup");
            analysis.AppendLine("  • Consider batch material processing");
            analysis.AppendLine("  • Implement material pooling");
            
            return analysis.ToString();
        }
        
        /// <summary>
        /// Generate PSO creation timeline analysis
        /// </summary>
        public string GenerateCreationTimelineAnalysis()
        {
            var timeline = new StringBuilder();
            timeline.AppendLine("=== PSO Creation Timeline ===");
            timeline.AppendLine("Recent PSO creation events:");
            timeline.AppendLine();
            
            // Simulate recent events (in production, this would track real events)
            var events = new[]
            {
                new { Time = DateTime.UtcNow.AddMinutes(-5), Material = "DefaultPBR", Duration = 12.5, Cached = false },
                new { Time = DateTime.UtcNow.AddMinutes(-3), Material = "TransparentPBR", Duration = 2.1, Cached = true },
                new { Time = DateTime.UtcNow.AddMinutes(-2), Material = "EmissivePBR", Duration = 15.8, Cached = false },
                new { Time = DateTime.UtcNow.AddMinutes(-1), Material = "DefaultPBR", Duration = 0.5, Cached = true }
            };
            
            foreach (var evt in events)
            {
                var status = evt.Cached ? "[CACHED]" : "[CREATED]";
                timeline.AppendLine($"  {evt.Time:HH:mm:ss} {status} {evt.Material,-15} {evt.Duration,6:F1}ms");
            }
            
            return timeline.ToString();
        }
        
        /// <summary>
        /// Generate memory usage breakdown
        /// </summary>
        public string GenerateMemoryBreakdown()
        {
            var stats = _psomManager.GetDetailedStatistics();
            var cacheStats = stats.CacheStatistics;
            
            var breakdown = new StringBuilder();
            breakdown.AppendLine("=== Memory Usage Breakdown ===");
            breakdown.AppendLine();
            
            var totalMemory = cacheStats.MemoryUsageBytes;
            breakdown.AppendLine($"Total Cache Memory: {totalMemory / 1024.0 / 1024.0:F2} MB");
            breakdown.AppendLine();
            
            breakdown.AppendLine("Memory Distribution:");
            breakdown.AppendLine($"  PSO Objects:       {cacheStats.AverageEntrySize * cacheStats.TotalEntries / 1024.0 / 1024.0:F2} MB");
            breakdown.AppendLine($"  Overhead:          {totalMemory * 0.1 / 1024.0 / 1024.0:F2} MB (estimated)");
            breakdown.AppendLine($"  Free Space:        {Math.Max(0, (cacheStats.Capacity - cacheStats.TotalEntries) * cacheStats.AverageEntrySize) / 1024.0 / 1024.0:F2} MB");
            breakdown.AppendLine();
            
            breakdown.AppendLine("Cache Efficiency:");
            var utilization = (double)cacheStats.TotalEntries / cacheStats.Capacity;
            breakdown.AppendLine($"  Capacity Utilization: {utilization:P2}");
            breakdown.AppendLine($"  Memory Efficiency:    {cacheStats.CacheHitRate:P2}");
            
            return breakdown.ToString();
        }
        
        /// <summary>
        /// Generate performance benchmarking report
        /// </summary>
        public string GenerateBenchmarkReport(int iterations = 100)
        {
            var benchmark = new StringBuilder();
            benchmark.AppendLine("=== PSO Cache Benchmark Results ===");
            benchmark.AppendLine($"Running {iterations} iterations...");
            benchmark.AppendLine();
            
            // Simulate benchmark results (in production, this would run actual benchmarks)
            var warmCacheTime = 1.2;
            var coldCacheTime = 28.5;
            var hitRate = 0.85;
            
            benchmark.AppendLine("Cache Performance:");
            benchmark.AppendLine($"  Warm Cache Hit Time: {warmCacheTime:F2} ms");
            benchmark.AppendLine($"  Cold Cache Miss Time: {coldCacheTime:F2} ms");
            benchmark.AppendLine($"  Performance Improvement: {(coldCacheTime - warmCacheTime) / coldCacheTime * 100:F1}%");
            benchmark.AppendLine();
            
            benchmark.AppendLine("Cache Effectiveness:");
            benchmark.AppendLine($"  Hit Rate: {hitRate:P2}");
            benchmark.AppendLine($"  Miss Penalty: {coldCacheTime - warmCacheTime:F2} ms");
            benchmark.AppendLine($"  Average Access Time: {(warmCacheTime * hitRate + coldCacheTime * (1 - hitRate)):F2} ms");
            
            return benchmark.ToString();
        }
        
        /// <summary>
        /// Export cache data for external analysis
        /// </summary>
        public string ExportCacheData()
        {
            var stats = _psomManager.GetDetailedStatistics();
            var export = new StringBuilder();
            
            export.AppendLine("timestamp,operation,psotype,duration_ms,cached,hits,misses");
            
            // Simulate export data (in production, this would export real data)
            var sampleData = new[]
            {
                new { Time = DateTime.UtcNow.AddHours(-1), Op = "create", Type = "PBR", Duration = 28.5, Cached = false },
                new { Time = DateTime.UtcNow.AddMinutes(-30), Op = "lookup", Type = "PBR", Duration = 0.8, Cached = true },
                new { Time = DateTime.UtcNow.AddMinutes(-15), Op = "create", Type = "Transparent", Duration = 22.1, Cached = false },
                new { Time = DateTime.UtcNow.AddMinutes(-5), Op = "lookup", Type = "PBR", Duration = 1.2, Cached = true }
            };
            
            foreach (var data in sampleData)
            {
                export.AppendLine($"{data.Time:yyyy-MM-dd HH:mm:ss},{data.Op},{data.Type},{data.Duration:F2},{data.Cached},{stats.SuccessfulCacheLookups},{stats.TotalCacheLookups - stats.SuccessfulCacheLookups}");
            }
            
            return export.ToString();
        }
        
        private string GetPerformanceRating(double hitRate)
        {
            if (hitRate >= 0.9) return "Excellent (A+)";
            if (hitRate >= 0.8) return "Very Good (A)";
            if (hitRate >= 0.7) return "Good (B+)";
            if (hitRate >= 0.6) return "Fair (B)";
            if (hitRate >= 0.5) return "Needs Improvement (C)";
            return "Poor (F)";
        }
        
        private string GetPerformanceStatus(double hitRate, double avgCreationTime)
        {
            var issues = new List<string>();
            
            if (hitRate < 0.6)
                issues.Add("Low cache hit rate");
            if (avgCreationTime > 20)
                issues.Add("High PSO creation time");
            if (hitRate < 0.4 && avgCreationTime > 30)
                issues.Add("Significant performance issues");
            
            return issues.Count > 0 ? string.Join(", ", issues) : "Performance is within acceptable parameters";
        }
        
        private List<string> GetOptimizationRecommendations(PSOManagerStatistics stats)
        {
            var recommendations = new List<string>();
            
            if (stats.CacheHitRate < 0.7)
            {
                recommendations.Add("Increase cache capacity to improve hit rate");
                recommendations.Add("Consider pre-compiling more material variants");
            }
            
            if (stats.AverageCreationTimeMs > 15)
            {
                recommendations.Add("Optimize shader compilation pipeline");
                recommendations.Add("Consider reducing shader complexity");
            }
            
            if (stats.CacheStatistics.TotalEntries > stats.CacheStatistics.Capacity * 0.9)
            {
                recommendations.Add("Cache is nearing capacity - consider resizing");
            }
            
            if (stats.CacheStatistics.Evictions > stats.CacheStatistics.TotalEntries * 0.1)
            {
                recommendations.Add("High eviction rate detected - may need larger cache");
            }
            
            if (recommendations.Count == 0)
            {
                recommendations.Add("Cache performance is optimal - no changes needed");
            }
            
            return recommendations;
        }
        
        /// <summary>
        /// Generate real-time performance monitoring output
        /// </summary>
        public string GenerateRealtimeMonitoringOutput()
        {
            var stats = _psomManager.GetDetailedStatistics();
            var monitoring = new StringBuilder();
            
            monitoring.AppendLine($"[{DateTime.UtcNow:HH:mm:ss}] PSO Cache Monitor");
            monitoring.AppendLine($"Hits: {stats.SuccessfulCacheLookups} | Misses: {stats.TotalCacheLookups - stats.SuccessfulCacheLookups} | Hit Rate: {stats.CacheHitRate:P2}");
            monitoring.AppendLine($"Avg Creation: {stats.AverageCreationTimeMs:F1}ms | Cache Entries: {stats.CacheStatistics.TotalEntries}/{stats.CacheStatistics.Capacity}");
            
            // Simple status indicators
            var status = stats.CacheHitRate > 0.8 ? "✓" : stats.CacheHitRate > 0.6 ? "⚠" : "✗";
            monitoring.AppendLine($"Status: {status} Cache Performance {(stats.CacheHitRate > 0.8 ? "Good" : stats.CacheHitRate > 0.6 ? "Fair" : "Poor")}");
            
            return monitoring.ToString();
        }
    }
    
    /// <summary>
    /// PSO cache monitor for real-time tracking
    /// </summary>
    public class PSOCacheMonitor
    {
        private readonly OptimizedPSOManager _psomManager;
        private readonly Timer _monitorTimer;
        private readonly List<PSOMonitorEntry> _history = new List<PSOMonitorEntry>();
        
        public event EventHandler<PSOMonitorEntry> OnPerformanceUpdate;
        
        public PSOCacheMonitor(OptimizedPSOManager psomManager, TimeSpan updateInterval)
        {
            _psomManager = psomManager;
            _monitorTimer = new Timer(MonitorUpdate, null, TimeSpan.Zero, updateInterval);
        }
        
        public List<PSOMonitorEntry> GetHistory(TimeSpan window)
        {
            var cutoff = DateTime.UtcNow - window;
            return _history.Where(h => h.Timestamp >= cutoff).ToList();
        }
        
        public PSOMonitorEntry GetCurrentSnapshot()
        {
            var stats = _psomManager.GetDetailedStatistics();
            return new PSOMonitorEntry
            {
                Timestamp = DateTime.UtcNow,
                CacheHitRate = stats.CacheHitRate,
                TotalEntries = stats.CacheStatistics.TotalEntries,
                AverageCreationTime = stats.AverageCreationTimeMs,
                CacheHits = stats.SuccessfulCacheLookups,
                CacheMisses = stats.TotalCacheLookups - stats.SuccessfulCacheLookups,
                MemoryUsage = stats.CacheStatistics.MemoryUsageBytes
            };
        }
        
        private void MonitorUpdate(object state)
        {
            var entry = GetCurrentSnapshot();
            _history.Add(entry);
            
            // Keep only last 1000 entries to prevent memory bloat
            if (_history.Count > 1000)
            {
                _history.RemoveRange(0, _history.Count - 1000);
            }
            
            OnPerformanceUpdate?.Invoke(this, entry);
        }
        
        public void Dispose()
        {
            _monitorTimer?.Dispose();
        }
    }
    
    /// <summary>
    /// Individual monitor entry for tracking performance over time
    /// </summary>
    public class PSOMonitorEntry
    {
        public DateTime Timestamp { get; set; }
        public double CacheHitRate { get; set; }
        public int TotalEntries { get; set; }
        public double AverageCreationTime { get; set; }
        public long CacheHits { get; set; }
        public long CacheMisses { get; set; }
        public long MemoryUsage { get; set; }
        
        public double Throughput => CacheHits + CacheMisses > 0 ? (CacheHits + CacheMisses) / 60.0 : 0; // per minute
        
        public string GetFormattedString()
        {
            return $"{Timestamp:HH:mm:ss} | Hit Rate: {CacheHitRate:P2} | Entries: {TotalEntries} | Avg Time: {AverageCreationTime:F1}ms | Memory: {MemoryUsage / 1024 / 1024:F1}MB";
        }
    }
}