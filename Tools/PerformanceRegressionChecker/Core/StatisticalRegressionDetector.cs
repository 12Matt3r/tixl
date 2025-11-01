using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TiXL.PerformanceRegressionChecker.Core
{
    public interface IRegressionDetector
    {
        Task<RegressionReport> DetectRegressions(BenchmarkResults currentResults, double threshold);
        Task<TrendAnalysis> AnalyzeTrends(string benchmarkName, int days = 30);
    }

    public class StatisticalRegressionDetector : IRegressionDetector
    {
        private readonly IBaselineStore _baselineStore;
        private readonly IAnomalyDetector _anomalyDetector;
        private readonly ILogger<StatisticalRegressionDetector> _logger;

        public StatisticalRegressionDetector(
            IBaselineStore baselineStore, 
            IAnomalyDetector anomalyDetector,
            ILogger<StatisticalRegressionDetector> logger)
        {
            _baselineStore = baselineStore;
            _anomalyDetector = anomalyDetector;
            _logger = logger;
        }

        public async Task<RegressionReport> DetectRegressions(BenchmarkResults currentResults, double threshold)
        {
            var report = new RegressionReport
            {
                Timestamp = DateTime.UtcNow,
                CurrentCommit = GetCurrentCommit(),
                Threshold = threshold,
                TotalBenchmarks = currentResults.Benchmarks.Count
            };

            foreach (var benchmark in currentResults.Benchmarks)
            {
                try
                {
                    var baseline = await _baselineStore.GetBaseline(benchmark.Name, benchmark.Scenario);

                    if (baseline == null)
                    {
                        // First run of this benchmark - save as baseline
                        await _baselineStore.SaveBaseline(benchmark);
                        report.BaselinesCreated++;
                        continue;
                    }

                    var comparison = CompareWithBaseline(benchmark, baseline, threshold);
                    report.Comparisons.Add(comparison);

                    if (comparison.IsRegression)
                    {
                        var regression = new PerformanceRegression
                        {
                            BenchmarkName = benchmark.Name,
                            RegressionDate = DateTime.UtcNow,
                            CurrentValue = benchmark.MeanExecutionTime,
                            BaselineValue = baseline.MeanExecutionTime,
                            RegressionPercentage = comparison.RegressionPercentage,
                            Severity = DetermineSeverity(comparison.RegressionPercentage),
                            CommitHash = GetCurrentCommit(),
                            Status = RegressionStatus.Open
                        };

                        report.Regressions.Add(regression);
                        _logger.LogWarning($"Performance regression detected: {benchmark.Name} - {comparison.RegressionPercentage:F2}% regression");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error comparing benchmark {benchmark.Name}");
                    report.Errors.Add(new BenchmarkError { BenchmarkName = benchmark.Name, Error = ex.Message });
                }
            }

            return report;
        }

        public async Task<TrendAnalysis> AnalyzeTrends(string benchmarkName, int days = 30)
        {
            var historicalData = await _baselineStore.GetHistoricalData(benchmarkName, days);
            
            var analysis = new TrendAnalysis
            {
                BenchmarkName = benchmarkName,
                AnalysisPeriod = days,
                DataPoints = historicalData,
                TrendDirection = CalculateTrendDirection(historicalData),
                Slope = CalculateSlope(historicalData),
                R2Score = CalculateR2(historicalData),
                Prediction = PredictNextValue(historicalData),
                Confidence = CalculateConfidence(historicalData)
            };

            return analysis;
        }

        private BenchmarkComparison CompareWithBaseline(BenchmarkResult current, BenchmarkResult baseline, double threshold)
        {
            var comparison = new BenchmarkComparison
            {
                BenchmarkName = current.Name,
                CurrentValue = current.MeanExecutionTime,
                BaselineValue = baseline.MeanExecutionTime,
                Difference = current.MeanExecutionTime - baseline.MeanExecutionTime,
                RegressionPercentage = ((current.MeanExecutionTime - baseline.MeanExecutionTime) / baseline.MeanExecutionTime) * 100,
                AbsoluteDifference = Math.Abs(current.MeanExecutionTime - baseline.MeanExecutionTime)
            };

            // Check if this is a regression
            comparison.IsRegression = comparison.RegressionPercentage > threshold;

            // Statistical significance test
            if (current.Samples?.Count > 1 && baseline.Samples?.Count > 1)
            {
                var tTest = new TTest(current.Samples, baseline.Samples);
                comparison.IsStatisticallySignificant = tTest.PValue < 0.05;
                comparison.PValue = tTest.PValue;
            }

            // Anomaly detection
            comparison.IsAnomaly = _anomalyDetector.IsAnomaly(current.MeanExecutionTime, historicalContext: await GetHistoricalContext(current.Name));

            return comparison;
        }

        private RegressionSeverity DetermineSeverity(double regressionPercentage)
        {
            return regressionPercentage switch
            {
                > 50 => RegressionSeverity.Critical,
                > 25 => RegressionSeverity.High,
                > 10 => RegressionSeverity.Medium,
                _ => RegressionSeverity.Low
            };
        }

        private TrendDirection CalculateTrendDirection(List<BenchmarkResult> data)
        {
            if (data.Count < 3) return TrendDirection.Stable;

            var correlation = CalculateCorrelation(data);
            return correlation switch
            {
                > 0.5 => TrendDirection.Worsening,
                < -0.5 => TrendDirection.Improving,
                _ => TrendDirection.Stable
            };
        }

        private double CalculateSlope(List<BenchmarkResult> data)
        {
            if (data.Count < 2) return 0;

            var n = data.Count;
            var sumX = data.Select((d, i) => i).Sum();
            var sumY = data.Sum(d => d.MeanExecutionTime);
            var sumXY = data.Select((d, i) => i * d.MeanExecutionTime).Sum();
            var sumXX = data.Select((d, i) => i * i).Sum();

            return (n * sumXY - sumX * sumY) / (n * sumXX - sumX * sumX);
        }

        private double CalculateR2(List<BenchmarkResult> data)
        {
            if (data.Count < 2) return 0;

            var meanY = data.Average(d => d.MeanExecutionTime);
            var slope = CalculateSlope(data);
            
            var ssTot = data.Sum(d => Math.Pow(d.MeanExecutionTime - meanY, 2));
            var ssRes = data.Select((d, i) => Math.Pow(d.MeanExecutionTime - (slope * i + meanY), 2)).Sum();

            return 1 - (ssRes / ssTot);
        }

        private double PredictNextValue(List<BenchmarkResult> data)
        {
            if (data.Count < 2) return data.LastOrDefault()?.MeanExecutionTime ?? 0;

            var slope = CalculateSlope(data);
            var lastValue = data.Last().MeanExecutionTime;
            return lastValue + slope;
        }

        private double CalculateConfidence(List<BenchmarkResult> data)
        {
            var r2 = CalculateR2(data);
            return Math.Max(0, Math.Min(1, r2));
        }

        private double CalculateCorrelation(List<BenchmarkResult> data)
        {
            if (data.Count < 2) return 0;

            var n = data.Count;
            var x = Enumerable.Range(0, n).Select(i => (double)i).ToArray();
            var y = data.Select(d => d.MeanExecutionTime).ToArray();

            var sumX = x.Sum();
            var sumY = y.Sum();
            var sumXY = x.Zip(y, (xi, yi) => xi * yi).Sum();
            var sumXX = x.Select(xi => xi * xi).Sum();
            var sumYY = y.Select(yi => yi * yi).Sum();

            var numerator = n * sumXY - sumX * sumY;
            var denominator = Math.Sqrt((n * sumXX - sumX * sumX) * (n * sumYY - sumY * sumY));

            return denominator == 0 ? 0 : numerator / denominator;
        }

        private async Task<List<double>> GetHistoricalContext(string benchmarkName)
        {
            var historicalData = await _baselineStore.GetHistoricalData(benchmarkName, 30);
            return historicalData.Select(d => d.MeanExecutionTime).ToList();
        }

        private string GetCurrentCommit()
        {
            // This would be replaced with actual Git commit hash retrieval
            return Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "unknown";
        }
    }

    public class TTest
    {
        public double PValue { get; }

        public TTest(List<double> sample1, List<double> sample2)
        {
            var mean1 = sample1.Average();
            var mean2 = sample2.Average();
            var variance1 = CalculateVariance(sample1);
            var variance2 = CalculateVariance(sample2);
            var pooledStandardError = Math.Sqrt(variance1 / sample1.Count + variance2 / sample2.Count);

            var tStatistic = (mean1 - mean2) / pooledStandardError;
            
            // Simplified p-value calculation (would use proper statistical distribution in real implementation)
            PValue = Math.Abs(tStatistic) > 1.96 ? 0.05 * Math.Sign(tStatistic) : 0.1;
        }

        private double CalculateVariance(List<double> values)
        {
            var mean = values.Average();
            return values.Sum(x => (x - mean) * (x - mean)) / values.Count;
        }
    }

    // Data structures
    public class RegressionReport
    {
        public DateTime Timestamp { get; set; }
        public string CurrentCommit { get; set; } = "";
        public double Threshold { get; set; }
        public int TotalBenchmarks { get; set; }
        public int BaselinesCreated { get; set; }
        public List<BenchmarkComparison> Comparisons { get; set; } = new();
        public List<PerformanceRegression> Regressions { get; set; } = new();
        public List<BenchmarkError> Errors { get; set; } = new();
        public bool HasRegressions => Regressions.Count > 0;
        public bool HasErrors => Errors.Count > 0;
    }

    public class BenchmarkComparison
    {
        public string BenchmarkName { get; set; } = "";
        public double CurrentValue { get; set; }
        public double BaselineValue { get; set; }
        public double Difference { get; set; }
        public double RegressionPercentage { get; set; }
        public double AbsoluteDifference { get; set; }
        public bool IsRegression { get; set; }
        public bool IsStatisticallySignificant { get; set; }
        public bool IsAnomaly { get; set; }
        public double PValue { get; set; }
    }

    public class PerformanceRegression
    {
        public string BenchmarkName { get; set; } = "";
        public DateTime RegressionDate { get; set; }
        public double CurrentValue { get; set; }
        public double BaselineValue { get; set; }
        public double RegressionPercentage { get; set; }
        public RegressionSeverity Severity { get; set; }
        public string CommitHash { get; set; } = "";
        public RegressionStatus Status { get; set; }
        public string? Notes { get; set; }
    }

    public class BenchmarkError
    {
        public string BenchmarkName { get; set; } = "";
        public string Error { get; set; } = "";
    }

    public class TrendAnalysis
    {
        public string BenchmarkName { get; set; } = "";
        public int AnalysisPeriod { get; set; }
        public List<BenchmarkResult> DataPoints { get; set; } = new();
        public TrendDirection TrendDirection { get; set; }
        public double Slope { get; set; }
        public double R2Score { get; set; }
        public double Prediction { get; set; }
        public double Confidence { get; set; }
    }

    public enum RegressionSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum RegressionStatus
    {
        Open,
        Investigating,
        Resolved
    }
}