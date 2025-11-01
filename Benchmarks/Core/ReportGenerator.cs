using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TiXL.PerformanceSuite.Models;

namespace TiXL.PerformanceSuite.Core
{
    /// <summary>
    /// Service responsible for generating performance reports
    /// </summary>
    public class ReportGenerator
    {
        private readonly ILogger<ReportGenerator> _logger;
        private readonly string _reportsDirectory;

        public ReportGenerator(ILogger<ReportGenerator> logger)
        {
            _logger = logger;
            _reportsDirectory = "./Reports";
            
            Directory.CreateDirectory(_reportsDirectory);
        }

        /// <summary>
        /// Generate an HTML performance report
        /// </summary>
        public async Task GenerateHtmlReport(PerformanceReportData data, string outputPath)
        {
            _logger.LogInformation($"📄 Generating HTML report: {outputPath}");

            try
            {
                var html = GenerateHtmlContent(data);
                await File.WriteAllTextAsync(outputPath, html);

                _logger.LogInformation($"✅ HTML report generated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to generate HTML report: {outputPath}");
                throw;
            }
        }

        /// <summary>
        /// Generate a JSON performance report
        /// </summary>
        public async Task GenerateJsonReport(PerformanceReportData data, string outputPath)
        {
            _logger.LogInformation($"📄 Generating JSON report: {outputPath}");

            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(data, options);
                await File.WriteAllTextAsync(outputPath, json);

                _logger.LogInformation($"✅ JSON report generated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to generate JSON report: {outputPath}");
                throw;
            }
        }

        /// <summary>
        /// Generate a CSV performance report
        /// </summary>
        public async Task GenerateCsvReport(PerformanceReportData data, string outputPath)
        {
            _logger.LogInformation($"📄 Generating CSV report: {outputPath}");

            try
            {
                var csv = GenerateCsvContent(data);
                await File.WriteAllTextAsync(outputPath, csv);

                _logger.LogInformation($"✅ CSV report generated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to generate CSV report: {outputPath}");
                throw;
            }
        }

        /// <summary>
        /// Generate a trend analysis report
        /// </summary>
        public async Task<TrendAnalysisReport> GenerateTrendReport(List<PerformanceReportData> historicalData)
        {
            _logger.LogInformation("📈 Generating trend analysis report");

            try
            {
                var trendReport = new TrendAnalysisReport
                {
                    GeneratedAt = DateTime.UtcNow,
                    TotalReports = historicalData.Count,
                    AnalysisPeriod = CalculateAnalysisPeriod(historicalData)
                };

                // Analyze trends for each metric
                var metricGroups = historicalData
                    .SelectMany(d => d.MetricStatistics)
                    .GroupBy(m => new { m.BenchmarkName, m.MetricName })
                    .ToList();

                foreach (var group in metricGroups)
                {
                    var trend = AnalyzeTrend(group.ToList());
                    trendReport.MetricTrends.Add(trend);
                }

                // Calculate overall performance trend
                trendReport.OverallTrend = CalculateOverallTrend(historicalData);

                _logger.LogInformation($"✅ Trend analysis completed with {trendReport.MetricTrends.Count} metrics");
                return trendReport;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate trend analysis");
                throw;
            }
        }

        private string GenerateHtmlContent(PerformanceReportData data)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset=\"UTF-8\">");
            sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine("    <title>TiXL Performance Report</title>");
            sb.AppendLine("    <style>");
            sb.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; background-color: #f5f5f5; }");
            sb.AppendLine("        .container { max-width: 1200px; margin: 0 auto; background: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }");
            sb.AppendLine("        .header { text-align: center; margin-bottom: 30px; padding: 20px; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; border-radius: 8px; }");
            sb.AppendLine("        .metric-card { margin: 15px 0; padding: 20px; border: 1px solid #ddd; border-radius: 8px; background: #fafafa; }");
            sb.AppendLine("        .metric-title { font-size: 18px; font-weight: bold; color: #333; margin-bottom: 10px; }");
            sb.AppendLine("        .metric-stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 10px; }");
            sb.AppendLine("        .stat-item { text-align: center; padding: 10px; background: white; border-radius: 4px; }");
            sb.AppendLine("        .stat-value { font-size: 20px; font-weight: bold; color: #2c3e50; }");
            sb.AppendLine("        .stat-label { font-size: 12px; color: #666; text-transform: uppercase; }");
            sb.AppendLine("        .system-overview { background: #e8f4fd; border-left: 4px solid #3498db; padding: 20px; margin: 20px 0; }");
            sb.AppendLine("        .performance-grade { display: inline-block; padding: 5px 15px; border-radius: 20px; font-weight: bold; color: white; }");
            sb.AppendLine("        .grade-a { background-color: #27ae60; }");
            sb.AppendLine("        .grade-b { background-color: #f39c12; }");
            sb.AppendLine("        .grade-c { background-color: #e67e22; }");
            sb.AppendLine("        .grade-d { background-color: #e74c3c; }");
            sb.AppendLine("        .trend-arrow { margin-left: 5px; }");
            sb.AppendLine("        .trend-up { color: #e74c3c; }");
            sb.AppendLine("        .trend-down { color: #27ae60; }");
            sb.AppendLine("        .trend-stable { color: #95a5a6; }");
            sb.AppendLine("        .category-section { margin: 30px 0; }");
            sb.AppendLine("        .category-title { font-size: 24px; color: #2c3e50; border-bottom: 2px solid #3498db; padding-bottom: 10px; margin-bottom: 20px; }");
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("    <div class=\"container\">");

            // Header
            sb.AppendLine("        <div class=\"header\">");
            sb.AppendLine($"            <h1>🚀 TiXL Performance Report</h1>");
            sb.AppendLine($"            <p>Generated: {data.GeneratedAt:yyyy-MM-dd HH:mm:ss UTC}</p>");
            sb.AppendLine($"            <p>Total Metrics: {data.TotalMetrics} | Duration: {data.MonitoringDuration:hh\\:mm\\:ss}</p>");
            sb.AppendLine("        </div>");

            // Performance Grade
            var grade = CalculatePerformanceGrade(data);
            sb.AppendLine("        <div class=\"system-overview\">");
            sb.AppendLine("            <h2>📊 Performance Grade</h2>");
            sb.AppendLine($"            <span class=\"performance-grade grade-{grade.Letter.ToLower()}\">{grade.Letter}</span>");
            sb.AppendLine($"            <p>Overall Performance: {grade.Description}</p>");
            sb.AppendLine("        </div>");

            // System Overview
            if (data.SystemOverview != null)
            {
                sb.AppendLine("        <div class=\"system-overview\">");
                sb.AppendLine("            <h2>💻 System Performance Overview</h2>");
                sb.AppendLine("            <div class=\"metric-stats\">");
                sb.AppendLine($"                <div class=\"stat-item\"><div class=\"stat-value\">{data.SystemOverview.AverageCpuUsage:F1}%</div><div class=\"stat-label\">Avg CPU</div></div>");
                sb.AppendLine($"                <div class=\"stat-item\"><div class=\"stat-value\">{data.SystemOverview.PeakCpuUsage:F1}%</div><div class=\"stat-label\">Peak CPU</div></div>");
                sb.AppendLine($"                <div class=\"stat-item\"><div class=\"stat-value\">{data.SystemOverview.AverageAvailableMemory:F0} MB</div><div class=\"stat-label\">Avg Memory</div></div>");
                sb.AppendLine($"                <div class=\"stat-item\"><div class=\"stat-value\">{data.SystemOverview.TotalGCGen0Collections}</div><div class=\"stat-label\">GC Gen 0</div></div>");
                sb.AppendLine($"                <div class=\"stat-item\"><div class=\"stat-value\">{data.SystemOverview.TotalGCGen1Collections}</div><div class=\"stat-label\">GC Gen 1</div></div>");
                sb.AppendLine($"                <div class=\"stat-item\"><div class=\"stat-value\">{data.SystemOverview.TotalGCGen2Collections}</div><div class=\"stat-label\">GC Gen 2</div></div>");
                sb.AppendLine("            </div>");
                sb.AppendLine("        </div>");
            }

            // Metric Statistics by Category
            var categories = data.MetricStatistics.Select(m => m.Category).Distinct().ToList();
            
            foreach (var category in categories.OrderBy(c => c))
            {
                var categoryMetrics = data.MetricStatistics.Where(m => m.Category == category).ToList();
                
                sb.AppendLine($"        <div class=\"category-section\">");
                sb.AppendLine($"            <h2 class=\"category-title\">{GetCategoryIcon(category)} {category}</h2>");
                
                foreach (var metric in categoryMetrics)
                {
                    sb.AppendLine("            <div class=\"metric-card\">");
                    sb.AppendLine($"                <div class=\"metric-title\">{metric.BenchmarkName}.{metric.MetricName}</div>");
                    sb.AppendLine("                <div class=\"metric-stats\">");
                    sb.AppendLine($"                    <div class=\"stat-item\"><div class=\"stat-value\">{metric.Mean:F3}</div><div class=\"stat-label\">Mean ({metric.Unit})</div></div>");
                    sb.AppendLine($"                    <div class=\"stat-item\"><div class=\"stat-value\">{metric.Min:F3}</div><div class=\"stat-label\">Min</div></div>");
                    sb.AppendLine($"                    <div class=\"stat-item\"><div class=\"stat-value\">{metric.Max:F3}</div><div class=\"stat-label\">Max</div></div>");
                    sb.AppendLine($"                    <div class=\"stat-item\"><div class=\"stat-value\">{metric.StandardDeviation:F3}</div><div class=\"stat-label\">Std Dev</div></div>");
                    sb.AppendLine($"                    <div class=\"stat-item\"><div class=\"stat-value\">{metric.SampleCount}</div><div class=\"stat-label\">Samples</div></div>");
                    sb.AppendLine("                </div>");
                    sb.AppendLine("            </div>");
                }
                
                sb.AppendLine("        </div>");
            }

            // Footer
            sb.AppendLine("        <div style=\"text-align: center; margin-top: 40px; padding: 20px; background: #f8f9fa; border-radius: 8px; color: #666;\">");
            sb.AppendLine("            <p>TiXL Performance Benchmarking Suite (TIXL-054)</p>");
            sb.AppendLine("            <p>Generated by Performance Monitor Service</p>");
            sb.AppendLine("        </div>");

            sb.AppendLine("    </div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        private string GenerateCsvContent(PerformanceReportData data)
        {
            var sb = new StringBuilder();
            
            // Header
            sb.AppendLine("Category,Benchmark,Metric,Mean,Min,Max,StdDev,SampleCount,Unit");
            
            foreach (var metric in data.MetricStatistics.OrderBy(m => m.Category).ThenBy(m => m.BenchmarkName))
            {
                sb.AppendLine($"{metric.Category},{metric.BenchmarkName},{metric.MetricName},{metric.Mean:F6},{metric.Min:F6},{metric.Max:F6},{metric.StandardDeviation:F6},{metric.SampleCount},{metric.Unit}");
            }

            return sb.ToString();
        }

        private PerformanceGrade CalculatePerformanceGrade(PerformanceReportData data)
        {
            // Simple grading algorithm based on key performance indicators
            var frameTimeMetrics = data.MetricStatistics.Where(m => m.Category == "FrameTime").ToList();
            var memoryMetrics = data.MetricStatistics.Where(m => m.Category == "MemoryUsage").ToList();
            var systemMetrics = data.SystemOverview;

            var totalScore = 0;
            var maxScore = 0;

            // Frame time scoring (lower is better)
            if (frameTimeMetrics.Any())
            {
                maxScore += 40;
                var avgFrameTime = frameTimeMetrics.Average(m => m.Mean);
                if (avgFrameTime <= 16.67) totalScore += 40; // 60 FPS
                else if (avgFrameTime <= 20) totalScore += 30; // 50 FPS
                else if (avgFrameTime <= 25) totalScore += 20; // 40 FPS
                else if (avgFrameTime <= 33.33) totalScore += 10; // 30 FPS
            }

            // Memory scoring (lower allocation is better)
            if (memoryMetrics.Any())
            {
                maxScore += 30;
                var totalAllocations = memoryMetrics.Sum(m => m.Mean);
                if (totalAllocations < 1000000) totalScore += 30; // < 1MB
                else if (totalAllocations < 10000000) totalScore += 20; // < 10MB
                else if (totalAllocations < 50000000) totalScore += 10; // < 50MB
            }

            // System scoring
            if (systemMetrics != null)
            {
                maxScore += 30;
                
                // CPU usage (lower is better)
                if (systemMetrics.AverageCpuUsage < 50) totalScore += 15;
                else if (systemMetrics.AverageCpuUsage < 75) totalScore += 10;
                else if (systemMetrics.AverageCpuUsage < 90) totalScore += 5;

                // GC pressure (lower is better)
                var totalGC = systemMetrics.TotalGCGen0Collections + systemMetrics.TotalGCGen1Collections + systemMetrics.TotalGCGen2Collections;
                if (totalGC < 100) totalScore += 15;
                else if (totalGC < 500) totalScore += 10;
                else if (totalGC < 1000) totalScore += 5;
            }

            var percentage = maxScore > 0 ? (double)totalScore / maxScore * 100 : 0;

            return percentage switch
            {
                >= 90 => new PerformanceGrade { Letter = 'A', Description = "Excellent performance" },
                >= 80 => new PerformanceGrade { Letter = 'B', Description = "Good performance with minor optimizations possible" },
                >= 70 => new PerformanceGrade { Letter = 'C', Description = "Acceptable performance, optimizations recommended" },
                >= 60 => new PerformanceGrade { Letter = 'D', Description = "Below average performance, optimizations required" },
                _ => new PerformanceGrade { Letter = 'F', Description = "Poor performance, immediate action needed" }
            };
        }

        private static string GetCategoryIcon(string category)
        {
            return category.ToLowerInvariant() switch
            {
                "frametime" => "⏱️",
                "memoryusage" => "💾",
                "projectload" => "📁",
                "operatorexec" => "⚙️",
                "graphicsperf" => "🎨",
                "audiolatency" => "🎵",
                "system" => "💻",
                _ => "📊"
            };
        }

        private TrendAnalysis AnalyzeTrend(List<MetricStatistics> dataPoints)
        {
            if (dataPoints.Count < 2)
            {
                return new TrendAnalysis
                {
                    TrendDirection = TrendDirection.Stable,
                    Confidence = 0,
                    Description = "Insufficient data for trend analysis"
                };
            }

            // Simple linear regression to determine trend
            var orderedData = dataPoints.OrderBy(d => d.SampleCount).ToList(); // Assuming SampleCount represents time
            var n = orderedData.Count;
            var sumX = orderedData.Sum(d => d.SampleCount);
            var sumY = orderedData.Sum(d => d.Mean);
            var sumXY = orderedData.Sum(d => d.SampleCount * d.Mean);
            var sumX2 = orderedData.Sum(d => d.SampleCount * d.SampleCount);

            var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
            var r2 = CalculateR2(orderedData, slope);

            var trendDirection = slope switch
            {
                > 0.01 => TrendDirection.Worsening,
                < -0.01 => TrendDirection.Improving,
                _ => TrendDirection.Stable
            };

            return new TrendAnalysis
            {
                TrendDirection = trendDirection,
                Slope = slope,
                R2Score = r2,
                Confidence = Math.Abs(r2),
                Description = $"Trend analysis: {trendDirection} (R² = {r2:F3})"
            };
        }

        private static double CalculateR2(List<MetricStatistics> data, double slope)
        {
            if (data.Count < 2) return 0;

            var meanY = data.Average(d => d.Mean);
            var totalSumSquares = data.Sum(d => Math.Pow(d.Mean - meanY, 2));
            var residualSumSquares = data.Sum(d => Math.Pow(d.Mean - (slope * d.SampleCount + (meanY - slope * data.Average(p => p.SampleCount))), 2));

            return totalSumSquares > 0 ? 1 - (residualSumSquares / totalSumSquares) : 0;
        }

        private static TimeSpan CalculateAnalysisPeriod(List<PerformanceReportData> historicalData)
        {
            if (!historicalData.Any()) return TimeSpan.Zero;
            
            var dates = historicalData.Select(d => d.GeneratedAt).OrderBy(d => d).ToList();
            return dates.Last() - dates.First();
        }

        private static TrendDirection CalculateOverallTrend(List<PerformanceReportData> historicalData)
        {
            if (historicalData.Count < 2) return TrendDirection.Stable;

            // Calculate average performance across all metrics for each time point
            var performanceScores = historicalData
                .OrderBy(d => d.GeneratedAt)
                .Select(d => d.MetricStatistics.Average(m => m.Mean))
                .ToList();

            return AnalyzeOverallTrend(performanceScores);
        }

        private static TrendDirection AnalyzeOverallTrend(List<double> scores)
        {
            if (scores.Count < 2) return TrendDirection.Stable;

            // Simple slope calculation
            var n = scores.Count;
            var sumX = Enumerable.Range(0, n).Sum(i => i);
            var sumY = scores.Sum();
            var sumXY = scores.Select((score, i) => i * score).Sum();
            var sumX2 = Enumerable.Range(0, n).Sum(i => i * i);

            var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);

            return slope switch
            {
                > 0.01 => TrendDirection.Worsening,  // Performance getting worse (higher values)
                < -0.01 => TrendDirection.Improving, // Performance getting better (lower values)
                _ => TrendDirection.Stable
            };
        }
    }

    // Supporting data structures
    public class PerformanceGrade
    {
        public char Letter { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}