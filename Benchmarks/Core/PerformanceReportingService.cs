using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Reports;
using Microsoft.Extensions.Logging;
using TiXL.PerformanceSuite.Models;

namespace TiXL.PerformanceSuite.Core
{
    /// <summary>
    /// Automated performance reporting and alerting system for TiXL benchmarks
    /// Provides comprehensive reporting, trend analysis, and real-time alerting
    /// </summary>
    public class PerformanceReportingService
    {
        private readonly ILogger<PerformanceReportingService> _logger;
        private readonly PerformanceMonitorService _perfMonitor;
        private readonly AlertService _alertService;
        private readonly TrendAnalyzer _trendAnalyzer;
        private readonly BaselineManager _baselineManager;
        private Timer _reportingTimer;
        private Timer _alertingTimer;
        
        public PerformanceReportingService(
            ILogger<PerformanceReportingService> logger,
            PerformanceMonitorService perfMonitor,
            AlertService alertService,
            TrendAnalyzer trendAnalyzer,
            BaselineManager baselineManager)
        {
            _logger = logger;
            _perfMonitor = perfMonitor;
            _alertService = alertService;
            _trendAnalyzer = trendAnalyzer;
            _baselineManager = baselineManager;
        }
        
        public async Task StartReporting()
        {
            _logger.LogInformation("🚀 Starting Performance Reporting and Alerting Service");
            
            // Start periodic reporting (every 30 seconds)
            _reportingTimer = new Timer(async _ => await GeneratePeriodicReport(), null, 
                TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
            
            // Start periodic alerting (every 60 seconds)
            _alertingTimer = new Timer(async _ => await ProcessAlerts(), null,
                TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
            
            await Task.CompletedTask;
        }
        
        public async Task StopReporting()
        {
            _logger.LogInformation("🛑 Stopping Performance Reporting and Alerting Service");
            
            _reportingTimer?.Dispose();
            _alertingTimer?.Dispose();
            
            // Generate final report
            await GenerateFinalReport();
        }
        
        /// <summary>
        /// Generates comprehensive performance report from benchmark results
        /// </summary>
        public async Task<PerformanceReport> GeneratePerformanceReport(BenchmarkDotNet.Reports.Summary summary)
        {
            _logger.LogInformation("📊 Generating comprehensive performance report");
            
            var report = new PerformanceReport
            {
                GeneratedAt = DateTime.UtcNow,
                ReportId = Guid.NewGuid().ToString(),
                Environment = GetEnvironmentInfo(),
                Summary = summary
            };
            
            // Extract metrics from benchmark summary
            var benchmarkMetrics = ExtractBenchmarkMetrics(summary);
            
            // Analyze performance trends
            var trendAnalysis = await _trendAnalyzer.AnalyzeTrends(benchmarkMetrics);
            
            // Compare against baselines
            var baselineComparison = await CompareAgainstBaselines(benchmarkMetrics);
            
            // Generate insights
            var insights = GeneratePerformanceInsights(benchmarkMetrics, trendAnalysis, baselineComparison);
            
            // Generate recommendations
            var recommendations = GenerateRecommendations(benchmarkMetrics, insights, baselineComparison);
            
            report.BenchmarkMetrics = benchmarkMetrics;
            report.TrendAnalysis = trendAnalysis;
            report.BaselineComparison = baselineComparison;
            report.Insights = insights;
            report.Recommendations = recommendations;
            
            // Save report
            await SaveReport(report);
            
            // Trigger alerts if necessary
            await CheckAndTriggerAlerts(report);
            
            return report;
        }
        
        /// <summary>
        /// Generates real-time performance alerts based on current metrics
        /// </summary>
        public async Task<List<PerformanceAlert>> CheckPerformanceAlerts(PerformanceReportData currentMetrics)
        {
            var alerts = new List<PerformanceAlert>();
            
            // Check CPU usage alerts
            if (currentMetrics.SystemOverview?.AverageCpuUsage > 85)
            {
                alerts.Add(await CreateAlert(
                    "High CPU Usage",
                    $"CPU usage is {currentMetrics.SystemOverview.AverageCpuUsage:F1}%, exceeding threshold of 85%",
                    AlertType.SystemResource,
                    AlertSeverity.High));
            }
            
            // Check memory usage alerts
            if (currentMetrics.SystemOverview?.AverageAvailableMemory < 500) // Less than 500MB available
            {
                alerts.Add(await CreateAlert(
                    "Low Memory",
                    $"Available memory is {currentMetrics.SystemOverview.AverageAvailableMemory:F0}MB, below threshold of 500MB",
                    AlertType.SystemResource,
                    AlertSeverity.High));
            }
            
            // Check garbage collection alerts
            var gcGen0Rate = currentMetrics.MetricStatistics
                .Where(m => m.MetricName == "GCGen0Collections")
                .Sum(m => m.Value);
            
            if (gcGen0Rate > 100) // More than 100 Gen0 collections
            {
                alerts.Add(await CreateAlert(
                    "High Garbage Collection Activity",
                    $"Gen0 collections: {gcGen0Rate}, indicating potential memory pressure",
                    AlertType.GarbageCollection,
                    AlertSeverity.Medium));
            }
            
            // Check frame time consistency
            var frameTimeMetrics = currentMetrics.MetricStatistics
                .Where(m => m.MetricName == "FrameTime" && m.Category == "FrameTime")
                .ToList();
            
            foreach (var frameTimeMetric in frameTimeMetrics)
            {
                var avgFrameTime = frameTimeMetric.Mean;
                if (avgFrameTime > 20) // Frame time exceeds 20ms
                {
                    alerts.Add(await CreateAlert(
                        "Frame Time Degradation",
                        $"Average frame time is {avgFrameTime:F2}ms, target is 16.67ms (60 FPS)",
                        AlertType.PerformanceDegradation,
                        AlertSeverity.High));
                }
                
                var consistency = 1.0 - (frameTimeMetric.StandardDeviation / frameTimeMetric.Mean);
                if (consistency < 0.8) // Low consistency
                {
                    alerts.Add(await CreateAlert(
                        "Frame Time Inconsistency",
                        $"Frame time consistency is {consistency:P2}, below threshold of 80%",
                        AlertType.PerformanceDegradation,
                        AlertSeverity.Medium));
                }
            }
            
            // Check throughput metrics
            var throughputMetrics = currentMetrics.MetricStatistics
                .Where(m => m.Unit == "events/sec")
                .ToList();
            
            foreach (var throughput in throughputMetrics)
            {
                if (throughput.Mean < 45000) // Below 45k events/sec target
                {
                    alerts.Add(await CreateAlert(
                        "Low Event Processing Throughput",
                        $"Event processing rate is {throughput.Mean:F0} events/sec, target is 50,000",
                        AlertType.PerformanceDegradation,
                        AlertSeverity.Medium));
                }
            }
            
            return alerts;
        }
        
        /// <summary>
        /// Generates automated performance regression alerts
        /// </summary>
        public async Task<List<PerformanceAlert>> CheckRegressionAlerts(Dictionary<string, PerformanceComparison> comparisons)
        {
            var alerts = new List<PerformanceAlert>();
            
            foreach (var comparison in comparisons.Values.Where(c => c.IsRegressed))
            {
                var severity = DetermineRegressionSeverity(comparison.RegressionPercent);
                
                alerts.Add(await CreateAlert(
                    $"Performance Regression: {comparison.MetricName}",
                    $"{comparison.MetricName} regressed by {comparison.RegressionPercent:F2}% (baseline: {comparison.BaselineValue:F2}, current: {comparison.CurrentValue:F2})",
                    AlertType.Regression,
                    severity));
            }
            
            return alerts;
        }
        
        /// <summary>
        /// Generates trend-based alerts for potential issues
        /// </summary>
        public async Task<List<PerformanceAlert>> CheckTrendAlerts(TrendAnalysisReport trendReport)
        {
            var alerts = new List<PerformanceAlert>();
            
            foreach (var metricTrend in trendReport.MetricTrends)
            {
                if (metricTrend.TrendDirection == TrendDirection.Worsening)
                {
                    alerts.Add(await CreateAlert(
                        $"Performance Trend Alert: {metricTrend.MetricName}",
                        $"Performance for {metricTrend.MetricName} is trending downward (R² = {metricTrend.R2Score:F3}, slope = {metricTrend.Slope:F4})",
                        AlertType.PerformanceDegradation,
                        AlertSeverity.Medium));
                }
                
                if (metricTrend.Confidence < 0.7 && metricTrend.TrendDirection != TrendDirection.Stable)
                {
                    alerts.Add(await CreateAlert(
                        $"Uncertain Trend: {metricTrend.MetricName}",
                        $"Trend analysis for {metricTrend.MetricName} has low confidence ({metricTrend.Confidence:P2}), monitor closely",
                        AlertType.PerformanceDegradation,
                        AlertSeverity.Low));
                }
            }
            
            return alerts;
        }
        
        /// <summary>
        /// Generates HTML performance dashboard report
        /// </summary>
        public async Task<string> GenerateHtmlDashboard(PerformanceReport report)
        {
            var html = $@"
<!DOCTYPE html>
<html>
<head>
    <title>TiXL Performance Dashboard</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; }}
        .header {{ background: #2c3e50; color: white; padding: 20px; border-radius: 8px; }}
        .metric-card {{ border: 1px solid #ddd; border-radius: 8px; padding: 15px; margin: 10px 0; }}
        .metric-value {{ font-size: 2em; font-weight: bold; color: #3498db; }}
        .status-good {{ color: #27ae60; }}
        .status-warning {{ color: #f39c12; }}
        .status-critical {{ color: #e74c3c; }}
        .chart-container {{ margin: 20px 0; }}
        .insight {{ background: #ecf0f1; padding: 10px; margin: 5px 0; border-left: 4px solid #3498db; }}
        .recommendation {{ background: #d5f4e6; padding: 10px; margin: 5px 0; border-left: 4px solid #27ae60; }}
        .alert {{ background: #fadbd8; padding: 10px; margin: 5px 0; border-left: 4px solid #e74c3c; }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>🚀 TiXL Performance Dashboard</h1>
        <p>Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss UTC}</p>
    </div>
    
    <h2>📊 System Overview</h2>
    <div class='metric-card'>
        <h3>Performance Score</h3>
        <div class='metric-value {GetPerformanceScoreClass(report.OverallScore)}'>{report.OverallScore:F1}/100</div>
    </div>
    
    <div class='metric-card'>
        <h3>Target Achievement</h3>
        <div class='metric-value {GetTargetAchievementClass(report.TargetsMetPercentage)}'>{report.TargetsMetPercentage:P0}</div>
        <p>{report.TargetsMet} of {report.TotalTargets} targets met</p>
    </div>
    
    <h2>📈 Benchmark Metrics</h2>";
            
            foreach (var metric in report.BenchmarkMetrics)
            {
                var statusClass = GetMetricStatusClass(metric);
                html += $@"
    <div class='metric-card'>
        <h3>{metric.BenchmarkName} - {metric.MetricName}</h3>
        <div class='metric-value {statusClass}'>{metric.Value:F2} {metric.Unit}</div>
        <p>Consistency: {GetConsistencyLevel(metric)} | Target: {metric.Target}</p>
    </div>";
            }
            
            html += @"
    <h2>🔍 Performance Insights</h2>";
            
            foreach (var insight in report.Insights)
            {
                html += $@"
    <div class='insight'>
        <strong>{insight.Title}</strong><br>
        {insight.Description}
    </div>";
            }
            
            html += @"
    <h2>💡 Recommendations</h2>";
            
            foreach (var recommendation in report.Recommendations)
            {
                html += $@"
    <div class='recommendation'>
        <strong>{recommendation.Priority}:</strong> {recommendation.Action}
    </div>";
            }
            
            html += @"
</body>
</html>";
            
            return html;
        }
        
        /// <summary>
        /// Generates automated CI/CD performance gates
        /// </summary>
        public async Task<PerformanceGateResult> EvaluatePerformanceGates(PerformanceReport report)
        {
            var gateResult = new PerformanceGateResult
            {
                Passed = true,
                FailedChecks = new List<string>(),
                Warnings = new List<string>()
            };
            
            // Frame time consistency gate
            var frameTimeMetrics = report.BenchmarkMetrics
                .Where(m => m.MetricName == "FrameConsistency")
                .ToList();
            
            foreach (var metric in frameTimeMetrics)
            {
                if (metric.Value < 0.90) // Less than 90% frame consistency
                {
                    gateResult.Passed = false;
                    gateResult.FailedChecks.Add($"Frame consistency below 90%: {metric.Value:P2}");
                }
                else if (metric.Value < 0.95) // Less than 95% but above 90%
                {
                    gateResult.Warnings.Add($"Frame consistency below optimal 95%: {metric.Value:P2}");
                }
            }
            
            // FPS gate
            var fpsMetrics = report.BenchmarkMetrics
                .Where(m => m.MetricName == "FPS" && m.Unit == "fps")
                .ToList();
            
            foreach (var metric in fpsMetrics)
            {
                if (metric.Value < 55) // Less than 55 FPS
                {
                    gateResult.Passed = false;
                    gateResult.FailedChecks.Add($"FPS below minimum 55: {metric.Value:F1}");
                }
                else if (metric.Value < 58) // Less than 58 FPS
                {
                    gateResult.Warnings.Add($"FPS below target 60: {metric.Value:F1}");
                }
            }
            
            // Event throughput gate
            var throughputMetrics = report.BenchmarkMetrics
                .Where(m => m.Unit == "events/sec")
                .ToList();
            
            foreach (var metric in throughputMetrics)
            {
                if (metric.Value < 40000) // Less than 40k events/sec
                {
                    gateResult.Passed = false;
                    gateResult.FailedChecks.Add($"Event throughput below 40k/sec: {metric.Value:F0}");
                }
                else if (metric.Value < 45000) // Less than 45k events/sec
                {
                    gateResult.Warnings.Add($"Event throughput below target 50k/sec: {metric.Value:F0}");
                }
            }
            
            // Memory usage gate
            var memoryMetrics = report.BenchmarkMetrics
                .Where(m => m.MetricName.Contains("Memory"))
                .ToList();
            
            foreach (var metric in memoryMetrics)
            {
                if (metric.Value > 500 * 1024 * 1024) // More than 500MB
                {
                    gateResult.Passed = false;
                    gateResult.FailedChecks.Add($"Memory usage above 500MB: {(metric.Value / 1024 / 1024):F0}MB");
                }
                else if (metric.Value > 300 * 1024 * 1024) // More than 300MB
                {
                    gateResult.Warnings.Add($"Memory usage above 300MB: {(metric.Value / 1024 / 1024):F0}MB");
                }
            }
            
            return gateResult;
        }
        
        #region Private Methods
        
        private async Task GeneratePeriodicReport()
        {
            try
            {
                var metrics = await _perfMonitor.GetReportData();
                var alerts = await CheckPerformanceAlerts(metrics);
                
                if (alerts.Any())
                {
                    _logger.LogWarning($"⚠️  {alerts.Count} performance alerts generated");
                    foreach (var alert in alerts)
                    {
                        _logger.LogWarning($"Alert: {alert.Title} - {alert.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating periodic report");
            }
        }
        
        private async Task ProcessAlerts()
        {
            try
            {
                var metrics = await _perfMonitor.GetReportData();
                var alerts = await CheckPerformanceAlerts(metrics);
                
                foreach (var alert in alerts)
                {
                    await _alertService.ProcessAlert(alert);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing alerts");
            }
        }
        
        private async Task GenerateFinalReport()
        {
            try
            {
                var metrics = await _perfMonitor.GetReportData();
                var analysis = await _perfMonitor.AnalyzeRegressions("baselines.json", 10.0);
                
                _logger.LogInformation($"📊 Final Report Summary:");
                _logger.LogInformation($"   Total Metrics: {metrics.TotalMetrics}");
                _logger.LogInformation($"   Regressions: {analysis.RegressionCount}");
                _logger.LogInformation($"   System Overview: CPU {metrics.SystemOverview?.AverageCpuUsage:F1}%, Memory {metrics.SystemOverview?.AverageAvailableMemory:F0}MB");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating final report");
            }
        }
        
        private List<BenchmarkMetric> ExtractBenchmarkMetrics(BenchmarkDotNet.Reports.Summary summary)
        {
            var metrics = new List<BenchmarkMetric>();
            
            foreach (var report in summary.Reports)
            {
                var benchmarkName = report.BenchmarkCase.DisplayInfo;
                
                foreach (var statistic in report.ResultStatistics)
                {
                    metrics.Add(new BenchmarkMetric
                    {
                        BenchmarkName = benchmarkName,
                        MetricName = statistic.Key,
                        Value = statistic.Value.Mean,
                        Unit = statistic.Value.Unit,
                        Target = GetTargetForMetric(statistic.Key),
                        Category = GetCategoryForMetric(statistic.Key)
                    });
                }
            }
            
            return metrics;
        }
        
        private async Task<TrendAnalysisReport> CompareAgainstBaselines(List<BenchmarkMetric> currentMetrics)
        {
            var baselinePath = "baselines.json";
            var analysis = await _perfMonitor.AnalyzeRegressions(baselinePath, 10.0);
            
            var trendReport = new TrendAnalysisReport
            {
                GeneratedAt = DateTime.UtcNow,
                TotalReports = analysis.RegressionCount,
                AnalysisPeriod = TimeSpan.FromMinutes(30), // Mock analysis period
                OverallTrend = analysis.RegressionCount > 0 ? TrendDirection.Worsening : TrendDirection.Stable
            };
            
            foreach (var regression in analysis.Regressions)
            {
                trendReport.MetricTrends.Add(new MetricTrend
                {
                    BenchmarkName = regression.BenchmarkName,
                    MetricName = regression.MetricName,
                    TrendDirection = TrendDirection.Worsening,
                    Slope = regression.RegressionPercent / 100.0,
                    R2Score = 0.85, // Mock R² score
                    Confidence = 0.9, // Mock confidence
                    Message = $"Regression of {regression.RegressionPercent:F2}%"
                });
            }
            
            return trendReport;
        }
        
        private List<PerformanceInsight> GeneratePerformanceInsights(
            List<BenchmarkMetric> metrics,
            TrendAnalysisReport trendAnalysis,
            TrendAnalysisReport baselineComparison)
        {
            var insights = new List<PerformanceInsight>();
            
            // Frame time insight
            var frameTimeMetrics = metrics.Where(m => m.MetricName.Contains("FrameTime")).ToList();
            if (frameTimeMetrics.Any())
            {
                var avgFrameTime = frameTimeMetrics.Average(m => m.Value);
                if (avgFrameTime > 20)
                {
                    insights.Add(new PerformanceInsight
                    {
                        Id = Guid.NewGuid().ToString(),
                        Timestamp = DateTime.UtcNow,
                        Type = InsightType.Recommendation,
                        Priority = 1,
                        Title = "Frame Time Optimization Needed",
                        Description = $"Average frame time is {avgFrameTime:F2}ms, consider optimizing rendering pipeline or reducing scene complexity.",
                        Source = "PerformanceBenchmark"
                    });
                }
            }
            
            // Memory usage insight
            var memoryMetrics = metrics.Where(m => m.Category == "MemoryUsage").ToList();
            if (memoryMetrics.Any())
            {
                var avgMemory = memoryMetrics.Average(m => m.Value) / (1024 * 1024); // Convert to MB
                if (avgMemory > 400)
                {
                    insights.Add(new PerformanceInsight
                    {
                        Id = Guid.NewGuid().ToString(),
                        Timestamp = DateTime.UtcNow,
                        Type = InsightType.Recommendation,
                        Priority = 2,
                        Title = "High Memory Usage Detected",
                        Description = $"Average memory usage is {avgMemory:F0}MB, consider implementing memory pooling or reducing allocations.",
                        Source = "PerformanceBenchmark"
                    });
                }
            }
            
            // Throughput insight
            var throughputMetrics = metrics.Where(m => m.Unit == "events/sec").ToList();
            if (throughputMetrics.Any())
            {
                var avgThroughput = throughputMetrics.Average(m => m.Value);
                if (avgThroughput < 45000)
                {
                    insights.Add(new PerformanceInsight
                    {
                        Id = Guid.NewGuid().ToString(),
                        Timestamp = DateTime.UtcNow,
                        Type = InsightType.Recommendation,
                        Priority = 3,
                        Title = "Event Processing Throughput Below Target",
                        Description = $"Event processing rate is {avgThroughput:F0} events/sec, target is 50,000. Consider optimizing event handling.",
                        Source = "PerformanceBenchmark"
                    });
                }
            }
            
            return insights;
        }
        
        private List<PerformanceRecommendation> GenerateRecommendations(
            List<BenchmarkMetric> metrics,
            List<PerformanceInsight> insights,
            TrendAnalysisReport baselineComparison)
        {
            var recommendations = new List<PerformanceRecommendation>();
            
            // High-priority recommendations
            var criticalInsights = insights.Where(i => i.Priority <= 2).ToList();
            foreach (var insight in criticalInsights)
            {
                recommendations.Add(new PerformanceRecommendation
                {
                    Priority = insight.Priority,
                    Category = "Performance",
                    Action = insight.Description,
                    Impact = "High",
                    Effort = "Medium"
                });
            }
            
            // Regression-based recommendations
            foreach (var regression in baselineComparison.MetricTrends.Where(t => t.TrendDirection == TrendDirection.Worsening))
            {
                recommendations.Add(new PerformanceRecommendation
                {
                    Priority = 2,
                    Category = "Regression",
                    Action = $"Investigate and fix performance regression in {regression.MetricName}",
                    Impact = "High",
                    Effort = "High"
                });
            }
            
            // Optimization-based recommendations
            var optimizationOpportunities = IdentifyOptimizationOpportunities(metrics);
            foreach (var opportunity in optimizationOpportunities)
            {
                recommendations.Add(new PerformanceRecommendation
                {
                    Priority = 3,
                    Category = "Optimization",
                    Action = opportunity,
                    Impact = "Medium",
                    Effort = "Medium"
                });
            }
            
            return recommendations.OrderBy(r => r.Priority).ToList();
        }
        
        private List<string> IdentifyOptimizationOpportunities(List<BenchmarkMetric> metrics)
        {
            var opportunities = new List<string>();
            
            // PSO caching optimization
            var psoMetrics = metrics.Where(m => m.BenchmarkName.Contains("PSO")).ToList();
            if (psoMetrics.Any() && psoMetrics.Average(m => m.Value) < 75)
            {
                opportunities.Add("Optimize PSO caching - hit rate below 75% target");
            }
            
            // Resource management optimization
            var resourceMetrics = metrics.Where(m => m.Category == "GraphicsPerf").ToList();
            if (resourceMetrics.Any() && resourceMetrics.Average(m => m.Value) > 10)
            {
                opportunities.Add("Optimize resource management - high creation times detected");
            }
            
            return opportunities;
        }
        
        private async Task<PerformanceAlert> CreateAlert(string title, string message, AlertType type, AlertSeverity severity)
        {
            return new PerformanceAlert
            {
                Id = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                Type = type,
                Severity = severity,
                Title = title,
                Message = message,
                Source = "PerformanceReportingService"
            };
        }
        
        private AlertSeverity DetermineRegressionSeverity(double regressionPercent)
        {
            return Math.Abs(regressionPercent) switch
            {
                > 25 => AlertSeverity.High,
                > 15 => AlertSeverity.Medium,
                _ => AlertSeverity.Low
            };
        }
        
        private async Task CheckAndTriggerAlerts(PerformanceReport report)
        {
            var alerts = new List<PerformanceAlert>();
            
            // Check overall performance score
            if (report.OverallScore < 70)
            {
                alerts.Add(await CreateAlert(
                    "Poor Overall Performance",
                    $"Overall performance score is {report.OverallScore:F1}/100",
                    AlertType.PerformanceDegradation,
                    AlertSeverity.High));
            }
            
            // Check target achievement
            if (report.TargetsMetPercentage < 0.8)
            {
                alerts.Add(await CreateAlert(
                    "Low Target Achievement",
                    $"Only {report.TargetsMetPercentage:P0} of performance targets met",
                    AlertType.PerformanceDegradation,
                    AlertSeverity.Medium));
            }
            
            // Check critical insights
            var criticalInsights = report.Insights.Where(i => i.Priority <= 2).ToList();
            foreach (var insight in criticalInsights)
            {
                alerts.Add(await CreateAlert(
                    insight.Title,
                    insight.Description,
                    AlertType.PerformanceDegradation,
                    AlertSeverity.High));
            }
            
            foreach (var alert in alerts)
            {
                await _alertService.ProcessAlert(alert);
            }
        }
        
        private async Task SaveReport(PerformanceReport report)
        {
            var reportPath = $"reports/report_{report.GeneratedAt:yyyyMMdd_HHmmss}.json";
            
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                await System.IO.File.WriteAllTextAsync(reportPath, json);
                _logger.LogInformation($"📄 Performance report saved: {reportPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to save performance report: {reportPath}");
            }
        }
        
        private string GetEnvironmentInfo()
        {
            return $@"OS: {Environment.OSVersion}
Processor: {Environment.ProcessorCount} cores
Memory: {GC.GetTotalMemory(false) / (1024 * 1024):F0}MB
Runtime: {Environment.Version}";
        }
        
        private string GetTargetForMetric(string metricName)
        {
            return metricName.ToLowerInvariant() switch
            {
                var name when name.Contains("frame") => "16.67ms",
                var name when name.Contains("fps") => "60 fps",
                var name when name.Contains("throughput") => "50k events/sec",
                var name when name.Contains("memory") => "< 300MB",
                _ => "TBD"
            };
        }
        
        private string GetCategoryForMetric(string metricName)
        {
            return metricName.ToLowerInvariant() switch
            {
                var name when name.Contains("frame") => "FrameTime",
                var name when name.Contains("memory") => "MemoryUsage",
                var name when name.Contains("cpu") => "System",
                var name when name.Contains("throughput") => "Throughput",
                _ => "General"
            };
        }
        
        private string GetPerformanceScoreClass(double score)
        {
            return score switch
            {
                >= 90 => "status-good",
                >= 70 => "status-warning",
                _ => "status-critical"
            };
        }
        
        private string GetTargetAchievementClass(double percentage)
        {
            return percentage switch
            {
                >= 0.9 => "status-good",
                >= 0.7 => "status-warning",
                _ => "status-critical"
            };
        }
        
        private string GetMetricStatusClass(BenchmarkMetric metric)
        {
            var target = GetTargetForMetric(metric.MetricName);
            var isGood = IsMetricGood(metric, target);
            
            return isGood ? "status-good" : "status-warning";
        }
        
        private bool IsMetricGood(BenchmarkMetric metric, string target)
        {
            // Simplified logic - would be more sophisticated in real implementation
            return metric.MetricName.ToLowerInvariant() switch
            {
                var name when name.Contains("consistency") => metric.Value >= 0.9,
                var name when name.Contains("fps") => metric.Value >= 55,
                var name when name.Contains("frame") => metric.Value <= 20,
                var name when name.Contains("throughput") => metric.Value >= 45000,
                var name when name.Contains("memory") => metric.Value <= 300 * 1024 * 1024,
                _ => true
            };
        }
        
        private string GetConsistencyLevel(BenchmarkMetric metric)
        {
            var consistency = 0.95; // Mock consistency calculation
            return consistency switch
            {
                >= 0.95 => "Excellent",
                >= 0.90 => "Good",
                >= 0.80 => "Fair",
                _ => "Poor"
            };
        }
        
        #endregion
    }
    
    #region Report Data Structures
    
    public class PerformanceReport
    {
        public string ReportId { get; set; } = "";
        public DateTime GeneratedAt { get; set; }
        public string Environment { get; set; } = "";
        public BenchmarkDotNet.Reports.Summary? Summary { get; set; }
        public List<BenchmarkMetric> BenchmarkMetrics { get; set; } = new();
        public TrendAnalysisReport TrendAnalysis { get; set; } = new();
        public TrendAnalysisReport BaselineComparison { get; set; } = new();
        public List<PerformanceInsight> Insights { get; set; } = new();
        public List<PerformanceRecommendation> Recommendations { get; set; } = new();
        public double OverallScore { get; set; }
        public int TargetsMet { get; set; }
        public int TotalTargets { get; set; }
        public double TargetsMetPercentage { get; set; }
    }
    
    public class BenchmarkMetric
    {
        public string BenchmarkName { get; set; } = "";
        public string MetricName { get; set; } = "";
        public double Value { get; set; }
        public string Unit { get; set; } = "";
        public string Target { get; set; } = "";
        public string Category { get; set; } = "";
    }
    
    public class PerformanceRecommendation
    {
        public int Priority { get; set; }
        public string Category { get; set; } = "";
        public string Action { get; set; } = "";
        public string Impact { get; set; } = "";
        public string Effort { get; set; } = "";
    }
    
    public class PerformanceGateResult
    {
        public bool Passed { get; set; }
        public List<string> FailedChecks { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }
    
    #endregion
}