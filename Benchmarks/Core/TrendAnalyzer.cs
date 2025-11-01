using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TiXL.PerformanceSuite.Models;

namespace TiXL.PerformanceSuite.Core
{
    /// <summary>
    /// Service responsible for analyzing performance trends over time
    /// </summary>
    public class TrendAnalyzer
    {
        private readonly ILogger<TrendAnalyzer> _logger;
        private readonly string _historicalDataPath;

        public TrendAnalyzer(ILogger<TrendAnalyzer> logger)
        {
            _logger = logger;
            _historicalDataPath = "./HistoricalData";
            
            Directory.CreateDirectory(_historicalDataPath);
        }

        /// <summary>
        /// Analyze performance trends for the last N days
        /// </summary>
        public async Task<TrendAnalysisReport> AnalyzeTrends(int daysBack = 30)
        {
            _logger.LogInformation($"📈 Analyzing performance trends for last {daysBack} days");

            try
            {
                var historicalData = await LoadHistoricalData(daysBack);
                if (!historicalData.Any())
                {
                    _logger.LogWarning("No historical data found for trend analysis");
                    return new TrendAnalysisReport
                    {
                        GeneratedAt = DateTime.UtcNow,
                        TotalReports = 0,
                        Message = "No historical data available"
                    };
                }

                var report = await AnalyzeTrendsInternal(historicalData);
                _logger.LogInformation($"✅ Trend analysis completed with {report.MetricTrends.Count} metrics");
                
                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze performance trends");
                throw;
            }
        }

        /// <summary>
        /// Save performance data for historical analysis
        /// </summary>
        public async Task SaveHistoricalData(PerformanceReportData data)
        {
            var fileName = $"performance-data-{data.GeneratedAt:yyyyMMdd-HHmmss}.json";
            var filePath = Path.Combine(_historicalDataPath, fileName);

            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(data, options);
                await File.WriteAllTextAsync(filePath, json);

                _logger.LogDebug($"Historical data saved: {filePath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to save historical data: {filePath}");
                throw;
            }
        }

        /// <summary>
        /// Get performance trends for a specific benchmark
        /// </summary>
        public async Task<BenchmarkTrend> GetBenchmarkTrend(string benchmarkName, int daysBack = 30)
        {
            _logger.LogInformation($"📊 Analyzing trend for benchmark: {benchmarkName}");

            try
            {
                var historicalData = await LoadHistoricalData(daysBack);
                var relevantData = historicalData
                    .Where(d => d.MetricStatistics.Any(m => m.BenchmarkName.Contains(benchmarkName, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (!relevantData.Any())
                {
                    return new BenchmarkTrend
                    {
                        BenchmarkName = benchmarkName,
                        TrendDirection = TrendDirection.Stable,
                        Confidence = 0,
                        DataPoints = new List<TrendDataPoint>(),
                        Message = "No data available for trend analysis"
                    };
                }

                return AnalyzeBenchmarkTrend(benchmarkName, relevantData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to analyze benchmark trend: {benchmarkName}");
                throw;
            }
        }

        /// <summary>
        /// Detect performance anomalies in historical data
        /// </summary>
        public async Task<List<PerformanceAnomaly>> DetectAnomalies(int daysBack = 7, double threshold = 2.0)
        {
            _logger.LogInformation($"🔍 Detecting anomalies in last {daysBack} days (threshold: {threshold}σ)");

            try
            {
                var historicalData = await LoadHistoricalData(daysBack);
                var anomalies = new List<PerformanceAnomaly>();

                foreach (var data in historicalData)
                {
                    foreach (var metric in data.MetricStatistics)
                    {
                        var anomaly = await DetectMetricAnomaly(metric, historicalData, threshold);
                        if (anomaly != null)
                        {
                            anomalies.Add(anomaly);
                        }
                    }
                }

                _logger.LogInformation($"✅ Detected {anomalies.Count} performance anomalies");
                return anomalies.OrderBy(a => a.Timestamp).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to detect performance anomalies");
                throw;
            }
        }

        /// <summary>
        /// Generate performance insights and recommendations
        /// </summary>
        public async Task<List<PerformanceInsight>> GenerateInsights(int daysBack = 30)
        {
            _logger.LogInformation("🧠 Generating performance insights");

            try
            {
                var historicalData = await LoadHistoricalData(daysBack);
                var insights = new List<PerformanceInsight>();

                // Analyze performance trends
                var trends = await AnalyzeTrends(daysBack);
                foreach (var trend in trends.MetricTrends)
                {
                    var insight = GenerateTrendInsight(trend);
                    if (insight != null)
                    {
                        insights.Add(insight);
                    }
                }

                // Detect performance anomalies
                var anomalies = await DetectAnomalies(daysBack);
                foreach (var anomaly in anomalies)
                {
                    var insight = GenerateAnomalyInsight(anomaly);
                    if (insight != null)
                    {
                        insights.Add(insight);
                    }
                }

                // Analyze system health trends
                var systemInsights = AnalyzeSystemHealthInsights(historicalData);
                insights.AddRange(systemInsights);

                _logger.LogInformation($"✅ Generated {insights.Count} performance insights");
                return insights.OrderByDescending(i => i.Priority).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate performance insights");
                throw;
            }
        }

        private async Task<TrendAnalysisReport> AnalyzeTrendsInternal(List<PerformanceReportData> historicalData)
        {
            var report = new TrendAnalysisReport
            {
                GeneratedAt = DateTime.UtcNow,
                TotalReports = historicalData.Count,
                AnalysisPeriod = CalculateAnalysisPeriod(historicalData)
            };

            // Group metrics by benchmark and metric name
            var metricGroups = historicalData
                .SelectMany(d => d.MetricStatistics)
                .GroupBy(m => new { m.BenchmarkName, m.MetricName })
                .ToList();

            foreach (var group in metricGroups)
            {
                var trend = AnalyzeMetricTrend(group.ToList(), historicalData);
                report.MetricTrends.Add(trend);
            }

            // Calculate overall performance trend
            report.OverallTrend = CalculateOverallTrend(historicalData);

            return report;
        }

        private MetricTrend AnalyzeMetricTrend(List<MetricStatistics> metrics, List<PerformanceReportData> historicalData)
        {
            if (metrics.Count < 2)
            {
                return new MetricTrend
                {
                    BenchmarkName = metrics.FirstOrDefault()?.BenchmarkName ?? "Unknown",
                    MetricName = metrics.FirstOrDefault()?.MetricName ?? "Unknown",
                    TrendDirection = TrendDirection.Stable,
                    Confidence = 0,
                    DataPoints = new List<TrendDataPoint>(),
                    Message = "Insufficient data for trend analysis"
                };
            }

            // Order by timestamp and extract values
            var orderedMetrics = metrics.OrderBy(m => GetMetricTimestamp(m, historicalData)).ToList();
            var values = orderedMetrics.Select(m => m.Mean).ToList();
            var timestamps = orderedMetrics.Select(m => GetMetricTimestamp(m, historicalData)).ToList();

            // Perform linear regression
            var regression = PerformLinearRegression(values, Enumerable.Range(0, values.Count).Select(i => (double)i).ToList());

            // Calculate trend direction and confidence
            var trendDirection = DetermineTrendDirection(regression.Slope);
            var confidence = CalculateConfidence(regression.R2, values.Count);

            // Create data points for visualization
            var dataPoints = new List<TrendDataPoint>();
            for (int i = 0; i < values.Count; i++)
            {
                dataPoints.Add(new TrendDataPoint
                {
                    Timestamp = timestamps[i],
                    Value = values[i],
                    BenchmarkName = orderedMetrics[i].BenchmarkName,
                    MetricName = orderedMetrics[i].MetricName
                });
            }

            return new MetricTrend
            {
                BenchmarkName = orderedMetrics.First().BenchmarkName,
                MetricName = orderedMetrics.First().MetricName,
                TrendDirection = trendDirection,
                Slope = regression.Slope,
                R2Score = regression.R2,
                Confidence = confidence,
                DataPoints = dataPoints,
                Message = $"Trend analysis: {trendDirection} (R² = {regression.R2:F3}, slope = {regression.Slope:F6})"
            };
        }

        private BenchmarkTrend AnalyzeBenchmarkTrend(string benchmarkName, List<PerformanceReportData> relevantData)
        {
            var allMetrics = relevantData
                .SelectMany(d => d.MetricStatistics)
                .Where(m => m.BenchmarkName.Contains(benchmarkName, StringComparison.OrdinalIgnoreCase))
                .GroupBy(m => m.MetricName)
                .ToList();

            var trends = new List<MetricTrend>();
            foreach (var group in allMetrics)
            {
                var trend = AnalyzeMetricTrend(group.ToList(), relevantData);
                trends.Add(trend);
            }

            // Calculate overall benchmark trend
            var overallTrend = CalculateOverallBenchmarkTrend(trends);

            return new BenchmarkTrend
            {
                BenchmarkName = benchmarkName,
                TrendDirection = overallTrend.Direction,
                Confidence = overallTrend.Confidence,
                DataPoints = trends.SelectMany(t => t.DataPoints).OrderBy(d => d.Timestamp).ToList(),
                Message = $"Overall trend for {benchmarkName}: {overallTrend.Direction}"
            };
        }

        private async Task<PerformanceAnomaly?> DetectMetricAnomaly(MetricStatistics metric, List<PerformanceReportData> historicalData, double threshold)
        {
            // Get historical values for the same metric
            var historicalValues = historicalData
                .SelectMany(d => d.MetricStatistics)
                .Where(m => m.BenchmarkName == metric.BenchmarkName && m.MetricName == metric.MetricName)
                .Select(m => m.Mean)
                .ToList();

            if (historicalValues.Count < 5) // Need at least 5 data points for anomaly detection
            {
                return null;
            }

            var mean = historicalValues.Average();
            var stdDev = CalculateStandardDeviation(historicalValues);

            var zScore = Math.Abs(metric.Mean - mean) / (stdDev > 0 ? stdDev : 1);

            if (zScore > threshold)
            {
                return new PerformanceAnomaly
                {
                    Id = Guid.NewGuid().ToString(),
                    Timestamp = DateTime.UtcNow,
                    BenchmarkName = metric.BenchmarkName,
                    MetricName = metric.MetricName,
                    CurrentValue = metric.Mean,
                    ExpectedValue = mean,
                    Deviation = Math.Abs(metric.Mean - mean),
                    ZScore = zScore,
                    Severity = DetermineAnomalySeverity(zScore),
                    Description = $"Anomalous {metric.MetricName} value detected: {metric.Mean:F3} (expected {mean:F3} ± {stdDev:F3})"
                };
            }

            return null;
        }

        private async Task<List<PerformanceReportData>> LoadHistoricalData(int daysBack)
        {
            var data = new List<PerformanceReportData>();
            var cutoffDate = DateTime.UtcNow.AddDays(-daysBack);

            try
            {
                var files = Directory.GetFiles(_historicalDataPath, "performance-data-*.json");
                
                foreach (var file in files)
                {
                    try
                    {
                        var fileName = Path.GetFileNameWithoutExtension(file);
                        if (TryParseDateFromFileName(fileName, out var fileDate) && fileDate >= cutoffDate)
                        {
                            var json = await File.ReadAllTextAsync(file);
                            var options = new JsonSerializerOptions
                            {
                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                            };

                            var reportData = JsonSerializer.Deserialize<PerformanceReportData>(json, options);
                            if (reportData != null)
                            {
                                data.Add(reportData);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to load historical data file: {file}");
                    }
                }

                data = data.OrderBy(d => d.GeneratedAt).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load historical data");
            }

            return data;
        }

        private static bool TryParseDateFromFileName(string fileName, out DateTime date)
        {
            date = DateTime.MinValue;
            
            // Extract date from "performance-data-YYYYMMDD-HHmmss.json"
            var parts = fileName.Split('-');
            if (parts.Length >= 3 && DateTime.TryParseExact($"{parts[2]}-{parts[3]}", "yyyyMMdd-HHmmss", null, System.Globalization.DateTimeStyles.None, out date))
            {
                return true;
            }

            return false;
        }

        private static DateTime GetMetricTimestamp(MetricStatistics metric, List<PerformanceReportData> historicalData)
        {
            // This is a simplified approach - in practice, you'd want to track when each metric was collected
            return historicalData.FirstOrDefault()?.GeneratedAt ?? DateTime.UtcNow;
        }

        private static (double Slope, double Intercept, double R2) PerformLinearRegression(List<double> yValues, List<double> xValues)
        {
            var n = yValues.Count;
            var sumX = xValues.Sum();
            var sumY = yValues.Sum();
            var sumXY = xValues.Zip(yValues, (x, y) => x * y).Sum();
            var sumX2 = xValues.Select(x => x * x).Sum();
            var sumY2 = yValues.Select(y => y * y).Sum();

            var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
            var intercept = (sumY - slope * sumX) / n;

            // Calculate R-squared
            var meanY = sumY / n;
            var totalSumSquares = sumY2 - n * meanY * meanY;
            var residualSumSquares = yValues.Select((y, i) => 
                Math.Pow(y - (slope * xValues[i] + intercept), 2)).Sum();
            
            var r2 = totalSumSquares > 0 ? 1 - (residualSumSquares / totalSumSquares) : 0;

            return (slope, intercept, r2);
        }

        private static TrendDirection DetermineTrendDirection(double slope)
        {
            return slope switch
            {
                > 0.01 => TrendDirection.Worsening,
                < -0.01 => TrendDirection.Improving,
                _ => TrendDirection.Stable
            };
        }

        private static double CalculateConfidence(double r2, int sampleCount)
        {
            // Combine R² score with sample size to determine confidence
            var sizeFactor = Math.Min(sampleCount / 10.0, 1.0); // Confidence increases with more samples
            return r2 * sizeFactor;
        }

        private static double CalculateStandardDeviation(List<double> values)
        {
            if (values.Count <= 1) return 0;
            
            var mean = values.Average();
            var variance = values.Sum(x => Math.Pow(x - mean, 2)) / values.Count;
            return Math.Sqrt(variance);
        }

        private static TimeSpan CalculateAnalysisPeriod(List<PerformanceReportData> historicalData)
        {
            if (!historicalData.Any()) return TimeSpan.Zero;
            
            var dates = historicalData.Select(d => d.GeneratedAt).OrderBy(d => d).ToList();
            return dates.Last() - dates.First();
        }

        private static (TrendDirection Direction, double Confidence) CalculateOverallBenchmarkTrend(List<MetricTrend> trends)
        {
            if (!trends.Any())
            {
                return (TrendDirection.Stable, 0);
            }

            // Weight by confidence and calculate overall direction
            var weightedTrend = trends.Average(t => 
                t.Confidence * (t.TrendDirection == TrendDirection.Worsening ? 1 : 
                               t.TrendDirection == TrendDirection.Improving ? -1 : 0));

            var direction = weightedTrend switch
            {
                > 0.2 => TrendDirection.Worsening,
                < -0.2 => TrendDirection.Improving,
                _ => TrendDirection.Stable
            };

            var confidence = trends.Average(t => t.Confidence);

            return (direction, confidence);
        }

        private static TrendDirection CalculateOverallTrend(List<PerformanceReportData> historicalData)
        {
            if (historicalData.Count < 2) return TrendDirection.Stable;

            // Calculate average performance across all metrics for each time point
            var performanceScores = historicalData
                .OrderBy(d => d.GeneratedAt)
                .Select(d => d.MetricStatistics.Average(m => m.Mean))
                .ToList();

            var regression = PerformLinearRegression(performanceScores, Enumerable.Range(0, performanceScores.Count).Select(i => (double)i).ToList());
            return DetermineTrendDirection(regression.Slope);
        }

        private static AnomalySeverity DetermineAnomalySeverity(double zScore)
        {
            return zScore switch
            {
                >= 4.0 => AnomalySeverity.Critical,
                >= 3.0 => AnomalySeverity.High,
                >= 2.5 => AnomalySeverity.Medium,
                _ => AnomalySeverity.Low
            };
        }

        private static PerformanceInsight? GenerateTrendInsight(MetricTrend trend)
        {
            if (trend.Confidence < 0.5) // Low confidence, skip
            {
                return null;
            }

            var priority = trend.TrendDirection switch
            {
                TrendDirection.Worsening when trend.Confidence > 0.7 => 10,
                TrendDirection.Worsening => 7,
                TrendDirection.Improving => 3,
                _ => 1
            };

            var title = trend.TrendDirection switch
            {
                TrendDirection.Worsening => $"Performance Degradation: {trend.BenchmarkName}",
                TrendDirection.Improving => $"Performance Improvement: {trend.BenchmarkName}",
                _ => $"Stable Performance: {trend.BenchmarkName}"
            };

            var description = trend.TrendDirection switch
            {
                TrendDirection.Worsening => $"Detected declining performance trend for {trend.BenchmarkName}.{trend.MetricName} (R² = {trend.R2Score:F3}). Consider investigation and optimization.",
                TrendDirection.Improving => $"Positive performance trend detected for {trend.BenchmarkName}.{trend.MetricName} (R² = {trend.R2Score:F3}). Current optimizations are working well.",
                _ => $"Performance for {trend.BenchmarkName}.{trend.MetricName} remains stable over time."
            };

            return new PerformanceInsight
            {
                Id = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                Type = InsightType.Trend,
                Priority = priority,
                Title = title,
                Description = description,
                Source = $"{trend.BenchmarkName}.{trend.MetricName}",
                Metadata = new Dictionary<string, object>
                {
                    ["TrendDirection"] = trend.TrendDirection.ToString(),
                    ["Confidence"] = trend.Confidence,
                    ["R2Score"] = trend.R2Score,
                    ["Slope"] = trend.Slope
                }
            };
        }

        private static PerformanceInsight? GenerateAnomalyInsight(PerformanceAnomaly anomaly)
        {
            var priority = anomaly.Severity switch
            {
                AnomalySeverity.Critical => 10,
                AnomalySeverity.High => 8,
                AnomalySeverity.Medium => 6,
                _ => 4
            };

            var title = $"Performance Anomaly: {anomaly.BenchmarkName}";
            var description = $"Unusual performance pattern detected in {anomaly.BenchmarkName}.{anomaly.MetricName}. " +
                            $"Current value ({anomaly.CurrentValue:F3}) deviates significantly from expected range (±{anomaly.ExpectedValue:F3}). " +
                            $"This may indicate a performance regression or system issue.";

            return new PerformanceInsight
            {
                Id = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                Type = InsightType.Anomaly,
                Priority = priority,
                Title = title,
                Description = description,
                Source = $"{anomaly.BenchmarkName}.{anomaly.MetricName}",
                Metadata = new Dictionary<string, object>
                {
                    ["CurrentValue"] = anomaly.CurrentValue,
                    ["ExpectedValue"] = anomaly.ExpectedValue,
                    ["ZScore"] = anomaly.ZScore,
                    ["Severity"] = anomaly.Severity.ToString()
                }
            };
        }

        private static List<PerformanceInsight> AnalyzeSystemHealthInsights(List<PerformanceReportData> historicalData)
        {
            var insights = new List<PerformanceInsight>();

            // Analyze GC pressure trends
            var systemData = historicalData.Where(d => d.SystemOverview != null).ToList();
            if (systemData.Any())
            {
                var avgGC0 = systemData.Average(d => d.SystemOverview!.TotalGCGen0Collections);
                var avgGC1 = systemData.Average(d => d.SystemOverview!.TotalGCGen1Collections);
                var avgGC2 = systemData.Average(d => d.SystemOverview!.TotalGCGen2Collections);

                var totalGC = avgGC0 + avgGC1 + avgGC2;

                if (totalGC > 1000)
                {
                    insights.Add(new PerformanceInsight
                    {
                        Id = Guid.NewGuid().ToString(),
                        Timestamp = DateTime.UtcNow,
                        Type = InsightType.SystemHealth,
                        Priority = 8,
                        Title = "High Garbage Collection Pressure",
                        Description = $"System shows high GC pressure with {totalGC:F0} average collections. Consider memory optimization or allocation reduction strategies.",
                        Source = "SystemMonitoring",
                        Metadata = new Dictionary<string, object>
                        {
                            ["GC0"] = avgGC0,
                            ["GC1"] = avgGC1,
                            ["GC2"] = avgGC2,
                            ["TotalGC"] = totalGC
                        }
                    });
                }
            }

            return insights;
        }
    }
}