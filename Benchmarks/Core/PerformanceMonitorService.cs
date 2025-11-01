using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TiXL.PerformanceSuite.Models;

namespace TiXL.PerformanceSuite.Core
{
    /// <summary>
    /// Service responsible for monitoring and collecting performance metrics
    /// during benchmark execution
    /// </summary>
    public class PerformanceMonitorService
    {
        private readonly ILogger<PerformanceMonitorService> _logger;
        private readonly List<PerformanceMetric> _metrics;
        private readonly PerformanceCounter _cpuCounter;
        private readonly PerformanceCounter _memoryCounter;
        private readonly Timer _metricsTimer;
        private bool _isMonitoring;
        private DateTime _startTime;

        public PerformanceMonitorService(ILogger<PerformanceMonitorService> logger)
        {
            _logger = logger;
            _metrics = new List<PerformanceMetric>();
            
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not initialize performance counters");
                _cpuCounter = null!;
                _memoryCounter = null!;
            }

            _metricsTimer = new Timer(CollectSystemMetrics, null, Timeout.Infinite, Timeout.Infinite);
        }

        public async Task StartMonitoring()
        {
            _startTime = DateTime.UtcNow;
            _isMonitoring = true;
            
            _logger.LogInformation("📊 Starting performance monitoring");
            
            // Collect initial metrics
            CollectSystemMetrics(null);
            
            // Start periodic collection (every 100ms)
            _metricsTimer.Change(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));

            // Force garbage collection before starting
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            await Task.CompletedTask;
        }

        public async Task StopMonitoring()
        {
            if (!_isMonitoring) return;

            _isMonitoring = false;
            _metricsTimer.Change(Timeout.Infinite, Timeout.Infinite);
            
            _logger.LogInformation("⏹️  Stopping performance monitoring");
            _logger.LogInformation($"📈 Collected { _metrics.Count} performance samples over {DateTime.UtcNow - _startTime:hh\\:mm\\:ss}");

            await Task.CompletedTask;
        }

        public void RecordBenchmarkMetric(string benchmarkName, string metricName, double value, string unit)
        {
            var metric = new PerformanceMetric
            {
                Timestamp = DateTime.UtcNow,
                BenchmarkName = benchmarkName,
                MetricName = metricName,
                Value = value,
                Unit = unit,
                Category = GetMetricCategory(metricName)
            };

            _metrics.Add(metric);
            _logger.LogDebug($"📊 Recorded metric: {benchmarkName}.{metricName} = {value:F2} {unit}");
        }

        public async Task<PerformanceAnalysis> AnalyzeRegressions(string baselinePath, double thresholdPercent)
        {
            _logger.LogInformation($"🔍 Analyzing regressions against baseline: {baselinePath}");

            try
            {
                if (!System.IO.File.Exists(baselinePath))
                {
                    return new PerformanceAnalysis
                    {
                        HasRegressions = false,
                        RegressionCount = 0,
                        Message = $"Baseline file not found: {baselinePath}"
                    };
                }

                var baselineJson = await System.IO.File.ReadAllTextAsync(baselinePath);
                var baseline = System.Text.Json.JsonSerializer.Deserialize<BaselineData>(baselineJson);

                if (baseline == null)
                {
                    return new PerformanceAnalysis
                    {
                        HasRegressions = false,
                        RegressionCount = 0,
                        Message = "Failed to parse baseline data"
                    };
                }

                return AnalyzeCurrentAgainstBaseline(baseline, thresholdPercent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing regressions");
                return new PerformanceAnalysis
                {
                    HasRegressions = false,
                    RegressionCount = 0,
                    Message = $"Analysis error: {ex.Message}"
                };
            }
        }

        public async Task<PerformanceReportData> GetReportData()
        {
            await Task.CompletedTask;

            var reportData = new PerformanceReportData
            {
                GeneratedAt = DateTime.UtcNow,
                TotalMetrics = _metrics.Count,
                MonitoringDuration = DateTime.UtcNow - _startTime
            };

            // Group metrics by benchmark and metric name
            var groupedMetrics = _metrics
                .GroupBy(m => new { m.BenchmarkName, m.MetricName })
                .ToList();

            foreach (var group in groupedMetrics)
            {
                var values = group.Select(m => m.Value).ToList();
                var stats = new MetricStatistics
                {
                    BenchmarkName = group.Key.BenchmarkName,
                    MetricName = group.Key.MetricName,
                    Mean = values.Average(),
                    Min = values.Min(),
                    Max = values.Max(),
                    StandardDeviation = CalculateStandardDeviation(values),
                    SampleCount = values.Count,
                    Unit = group.First().Unit,
                    Category = group.First().Category
                };

                reportData.MetricStatistics.Add(stats);
            }

            // Calculate system performance overview
            reportData.SystemOverview = CalculateSystemOverview();

            return reportData;
        }

        private void CollectSystemMetrics(object? state)
        {
            if (!_isMonitoring) return;

            try
            {
                var metric = new PerformanceMetric
                {
                    Timestamp = DateTime.UtcNow,
                    BenchmarkName = "SystemMonitoring",
                    MetricName = "CPUUsage",
                    Value = _cpuCounter?.NextValue() ?? 0,
                    Unit = "%",
                    Category = "System"
                };

                _metrics.Add(metric);

                metric = new PerformanceMetric
                {
                    Timestamp = DateTime.UtcNow,
                    BenchmarkName = "SystemMonitoring",
                    MetricName = "AvailableMemory",
                    Value = _memoryCounter?.NextValue() ?? 0,
                    Unit = "MB",
                    Category = "System"
                };

                _metrics.Add(metric);

                // Record GC metrics
                metric = new PerformanceMetric
                {
                    Timestamp = DateTime.UtcNow,
                    BenchmarkName = "SystemMonitoring",
                    MetricName = "GCGen0Collections",
                    Value = GC.CollectionCount(0),
                    Unit = "count",
                    Category = "System"
                };

                _metrics.Add(metric);

                metric = new PerformanceMetric
                {
                    Timestamp = DateTime.UtcNow,
                    BenchmarkName = "SystemMonitoring",
                    MetricName = "GCGen1Collections",
                    Value = GC.CollectionCount(1),
                    Unit = "count",
                    Category = "System"
                };

                _metrics.Add(metric);

                metric = new PerformanceMetric
                {
                    Timestamp = DateTime.UtcNow,
                    BenchmarkName = "SystemMonitoring",
                    MetricName = "GCGen2Collections",
                    Value = GC.CollectionCount(2),
                    Unit = "count",
                    Category = "System"
                };

                _metrics.Add(metric);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error collecting system metrics");
            }
        }

        private PerformanceAnalysis AnalyzeCurrentAgainstBaseline(BaselineData baseline, double thresholdPercent)
        {
            var analysis = new PerformanceAnalysis
            {
                HasRegressions = false,
                RegressionCount = 0,
                Regressions = new List<RegressionInfo>()
            };

            // Group current metrics by benchmark and metric name
            var currentStats = _metrics
                .Where(m => m.BenchmarkName != "SystemMonitoring")
                .GroupBy(m => new { m.BenchmarkName, m.MetricName })
                .ToDictionary(g => g.Key, g => g.Select(m => m.Value).Average());

            foreach (var baselineEntry in baseline.Metrics)
            {
                var currentKey = new { baselineEntry.BenchmarkName, baselineEntry.MetricName };
                
                if (currentStats.TryGetValue(currentKey, out var currentValue))
                {
                    var regressionPercent = ((currentValue - baselineEntry.Mean) / baselineEntry.Mean) * 100;
                    
                    if (regressionPercent > thresholdPercent)
                    {
                        analysis.HasRegressions = true;
                        analysis.RegressionCount++;
                        
                        analysis.Regressions.Add(new RegressionInfo
                        {
                            BenchmarkName = baselineEntry.BenchmarkName,
                            MetricName = baselineEntry.MetricName,
                            BaselineValue = baselineEntry.Mean,
                            CurrentValue = currentValue,
                            RegressionPercent = regressionPercent,
                            Unit = baselineEntry.Unit
                        });
                    }
                }
            }

            return analysis;
        }

        private SystemPerformanceOverview CalculateSystemOverview()
        {
            var cpuMetrics = _metrics.Where(m => m.MetricName == "CPUUsage").ToList();
            var memoryMetrics = _metrics.Where(m => m.MetricName == "AvailableMemory").ToList();
            var gc0Metrics = _metrics.Where(m => m.MetricName == "GCGen0Collections").ToList();
            var gc1Metrics = _metrics.Where(m => m.MetricName == "GCGen1Collections").ToList();
            var gc2Metrics = _metrics.Where(m => m.MetricName == "GCGen2Collections").ToList();

            return new SystemPerformanceOverview
            {
                AverageCpuUsage = cpuMetrics.Any() ? cpuMetrics.Average(m => m.Value) : 0,
                PeakCpuUsage = cpuMetrics.Any() ? cpuMetrics.Max(m => m.Value) : 0,
                AverageAvailableMemory = memoryMetrics.Any() ? memoryMetrics.Average(m => m.Value) : 0,
                PeakAvailableMemory = memoryMetrics.Any() ? memoryMetrics.Max(m => m.Value) : 0,
                TotalGCGen0Collections = gc0Metrics.Any() ? gc0Metrics.Last().Value - gc0Metrics.First().Value : 0,
                TotalGCGen1Collections = gc1Metrics.Any() ? gc1Metrics.Last().Value - gc1Metrics.First().Value : 0,
                TotalGCGen2Collections = gc2Metrics.Any() ? gc2Metrics.Last().Value - gc2Metrics.First().Value : 0
            };
        }

        private static string GetMetricCategory(string metricName)
        {
            return metricName.ToLowerInvariant() switch
            {
                var name when name.Contains("frame") => "FrameTime",
                var name when name.Contains("memory") || name.Contains("allocation") => "MemoryUsage",
                var name when name.Contains("load") || name.Contains("initialization") => "ProjectLoad",
                var name when name.Contains("operator") => "OperatorExec",
                var name when name.Contains("render") || name.Contains("graphics") => "GraphicsPerf",
                var name when name.Contains("audio") || name.Contains("latency") => "AudioLatency",
                var name when name.Contains("cpu") => "System",
                var name when name.Contains("gc") => "System",
                _ => "General"
            };
        }

        private static double CalculateStandardDeviation(List<double> values)
        {
            if (values.Count <= 1) return 0;
            
            var mean = values.Average();
            var variance = values.Sum(x => Math.Pow(x - mean, 2)) / values.Count;
            return Math.Sqrt(variance);
        }

        public void Dispose()
        {
            _metricsTimer?.Dispose();
            _cpuCounter?.Dispose();
            _memoryCounter?.Dispose();
        }
    }
}