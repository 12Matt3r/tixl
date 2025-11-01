using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TiXL.PerformanceRegressionChecker.Core
{
    // Core data interfaces
    public interface IDataStore
    {
        Task SaveBaseline(BenchmarkResult result);
        Task<BenchmarkResult?> GetBaseline(string benchmarkName, string scenario);
        Task<List<BenchmarkResult>> GetHistoricalData(string benchmarkName, int days);
        Task SaveRegression(PerformanceRegression regression);
        Task<List<PerformanceRegression>> GetRegressions(int days = 30);
    }

    public interface IBaselineStore
    {
        Task SaveBaseline(BenchmarkResult result);
        Task<BenchmarkResult?> GetBaseline(string benchmarkName, string scenario);
        Task UpdateBaseline(string benchmarkName, BenchmarkResult newResult);
        Task<List<BenchmarkResult>> GetAllBaselines();
    }

    public interface IAnomalyDetector
    {
        bool IsAnomaly(double currentValue, List<double> historicalContext);
        bool IsAnomaly(double currentValue, List<double> historicalContext, double threshold = 2.0);
    }

    public interface IReportGenerator
    {
        Task GenerateReport(RegressionReport report, string outputPath);
        Task GenerateDailyReport(DailyPerformanceReport report, string outputPath);
        string GenerateHtmlReport(RegressionReport report);
    }

    public interface INotificationService
    {
        Task SendSlackNotification(string message, string channel = "#performance");
        Task SendEmailNotification(string subject, string body, List<string> recipients);
        Task CreateGitHubIssue(string title, string body, List<string> labels);
    }

    public interface IPerformanceGate
    {
        GateResult EvaluateGates(RegressionReport report, double threshold);
    }

    // Core data models
    public class BenchmarkResults
    {
        public List<BenchmarkResult> Benchmarks { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string CommitHash { get; set; } = "";
        public string Branch { get; set; } = "";
    }

    public class BenchmarkResult
    {
        public string Name { get; set; } = "";
        public string Scenario { get; set; } = "";
        public double MeanExecutionTime { get; set; }
        public double StdDev { get; set; }
        public double MedianExecutionTime { get; set; }
        public double P99ExecutionTime { get; set; }
        public long MemoryAllocated { get; set; }
        public int GCCollectionsGen0 { get; set; }
        public int GCCollectionsGen1 { get; set; }
        public int GCCollectionsGen2 { get; set; }
        public int SamplesCount { get; set; }
        public List<double> Samples { get; set; } = new();
        public string FrameworkVersion { get; set; } = "";
        public string OperatingSystem { get; set; } = "";
        public string ProcessorName { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class GateResult
    {
        public bool HasFailures { get; set; }
        public List<GateFailure> Failures { get; set; } = new();
        public List<GateWarning> Warnings { get; set; } = new();
        public Dictionary<string, GateStatus> GateStatuses { get; set; } = new();
    }

    public class GateFailure
    {
        public string BenchmarkName { get; set; } = "";
        public string GateType { get; set; } = "";
        public string Message { get; set; } = "";
        public double CurrentValue { get; set; }
        public double Threshold { get; set; }
    }

    public class GateWarning
    {
        public string BenchmarkName { get; set; } = "";
        public string GateType { get; set; } = "";
        public string Message { get; set; } = "";
        public double CurrentValue { get; set; }
        public double Threshold { get; set; }
    }

    public enum GateStatus
    {
        Pass,
        Warning,
        Failure,
        NotEvaluated
    }

    // Z-Score Anomaly Detector Implementation
    public class ZScoreAnomalyDetector : IAnomalyDetector
    {
        private readonly double _defaultThreshold;

        public ZScoreAnomalyDetector(double defaultThreshold = 2.0)
        {
            _defaultThreshold = defaultThreshold;
        }

        public bool IsAnomaly(double currentValue, List<double> historicalContext)
        {
            return IsAnomaly(currentValue, historicalContext, _defaultThreshold);
        }

        public bool IsAnomaly(double currentValue, List<double> historicalContext, double threshold)
        {
            if (historicalContext.Count < 3) return false;

            var mean = historicalContext.Average();
            var stdDev = CalculateStandardDeviation(historicalContext, mean);

            if (stdDev == 0) return false;

            var zScore = Math.Abs(currentValue - mean) / stdDev;
            return zScore > threshold;
        }

        private static double CalculateStandardDeviation(List<double> values, double mean)
        {
            var variance = values.Sum(x => Math.Pow(x - mean, 2)) / values.Count;
            return Math.Sqrt(variance);
        }
    }

    // Performance Gate Implementation
    public class PerformanceGate : IPerformanceGate
    {
        private readonly IConfiguration _configuration;

        public PerformanceGate(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public GateResult EvaluateGates(RegressionReport report, double threshold)
        {
            var result = new GateResult();

            foreach (var comparison in report.Comparisons)
            {
                // Frame time gate
                if (comparison.BenchmarkName.Contains("Frame") || comparison.BenchmarkName.Contains("Render"))
                {
                    EvaluateFrameTimeGate(comparison, result);
                }

                // Memory gate
                if (comparison.BenchmarkName.Contains("Memory"))
                {
                    EvaluateMemoryGate(comparison, result);
                }

                // Audio gate
                if (comparison.BenchmarkName.Contains("Audio") || comparison.BenchmarkName.Contains("Latency"))
                {
                    EvaluateAudioGate(comparison, result);
                }

                // Node evaluation gate
                if (comparison.BenchmarkName.Contains("Node") || comparison.BenchmarkName.Contains("Evaluation"))
                {
                    EvaluateNodeEvaluationGate(comparison, result);
                }

                // Graphics gate
                if (comparison.BenchmarkName.Contains("Graphics") || comparison.BenchmarkName.Contains("Texture") || comparison.BenchmarkName.Contains("Buffer"))
                {
                    EvaluateGraphicsGate(comparison, result);
                }

                // General regression gate
                EvaluateGeneralRegressionGate(comparison, result, threshold);
            }

            result.HasFailures = result.Failures.Any();
            return result;
        }

        private void EvaluateFrameTimeGate(BenchmarkComparison comparison, GateResult result)
        {
            const double maxFrameTime = 16.67; // 60 FPS
            const double maxVariance = 2.0;

            if (comparison.CurrentValue > maxFrameTime)
            {
                result.Failures.Add(new GateFailure
                {
                    BenchmarkName = comparison.BenchmarkName,
                    GateType = "FrameTime",
                    Message = $"Frame time {comparison.CurrentValue:F2}ms exceeds 60 FPS requirement ({maxFrameTime:F2}ms)",
                    CurrentValue = comparison.CurrentValue,
                    Threshold = maxFrameTime
                });
            }

            result.GateStatuses["FrameTime"] = comparison.CurrentValue <= maxFrameTime ? GateStatus.Pass : GateStatus.Failure;
        }

        private void EvaluateMemoryGate(BenchmarkComparison comparison, GateResult result)
        {
            const double maxGrowthPercent = 15.0;

            if (comparison.RegressionPercentage > maxGrowthPercent)
            {
                result.Failures.Add(new GateFailure
                {
                    BenchmarkName = comparison.BenchmarkName,
                    GateType = "Memory",
                    Message = $"Memory growth {comparison.RegressionPercentage:F1}% exceeds threshold ({maxGrowthPercent:F1}%)",
                    CurrentValue = comparison.RegressionPercentage,
                    Threshold = maxGrowthPercent
                });
            }

            result.GateStatuses["Memory"] = comparison.RegressionPercentage <= maxGrowthPercent ? GateStatus.Pass : GateStatus.Failure;
        }

        private void EvaluateAudioGate(BenchmarkComparison comparison, GateResult result)
        {
            const double maxLatency = 10.0;

            if (comparison.CurrentValue > maxLatency)
            {
                result.Failures.Add(new GateFailure
                {
                    BenchmarkName = comparison.BenchmarkName,
                    GateType = "AudioLatency",
                    Message = $"Audio latency {comparison.CurrentValue:F2}ms exceeds requirement ({maxLatency:F2}ms)",
                    CurrentValue = comparison.CurrentValue,
                    Threshold = maxLatency
                });
            }

            result.GateStatuses["AudioLatency"] = comparison.CurrentValue <= maxLatency ? GateStatus.Pass : GateStatus.Failure;
        }

        private void EvaluateNodeEvaluationGate(BenchmarkComparison comparison, GateResult result)
        {
            const double maxEvalTime = 2.0;

            if (comparison.CurrentValue > maxEvalTime)
            {
                result.Warnings.Add(new GateWarning
                {
                    BenchmarkName = comparison.BenchmarkName,
                    GateType = "NodeEvaluation",
                    Message = $"Node evaluation time {comparison.CurrentValue:F2}ms exceeds target ({maxEvalTime:F2}ms)",
                    CurrentValue = comparison.CurrentValue,
                    Threshold = maxEvalTime
                });
            }

            result.GateStatuses["NodeEvaluation"] = comparison.CurrentValue <= maxEvalTime ? GateStatus.Pass : GateStatus.Warning;
        }

        private void EvaluateGraphicsGate(BenchmarkComparison comparison, GateResult result)
        {
            const double maxTextureUploadTime = 5.0;
            const double maxBufferCreationTime = 1.0;

            if (comparison.BenchmarkName.Contains("Texture") && comparison.CurrentValue > maxTextureUploadTime)
            {
                result.Warnings.Add(new GateWarning
                {
                    BenchmarkName = comparison.BenchmarkName,
                    GateType = "TextureUpload",
                    Message = $"Texture upload time {comparison.CurrentValue:F2}ms exceeds target ({maxTextureUploadTime:F2}ms)",
                    CurrentValue = comparison.CurrentValue,
                    Threshold = maxTextureUploadTime
                });
            }

            if (comparison.BenchmarkName.Contains("Buffer") && comparison.CurrentValue > maxBufferCreationTime)
            {
                result.Warnings.Add(new GateWarning
                {
                    BenchmarkName = comparison.BenchmarkName,
                    GateType = "BufferCreation",
                    Message = $"Buffer creation time {comparison.CurrentValue:F2}ms exceeds target ({maxBufferCreationTime:F2}ms)",
                    CurrentValue = comparison.CurrentValue,
                    Threshold = maxBufferCreationTime
                });
            }
        }

        private void EvaluateGeneralRegressionGate(BenchmarkComparison comparison, GateResult result, double threshold)
        {
            if (comparison.IsRegression)
            {
                result.Failures.Add(new GateFailure
                {
                    BenchmarkName = comparison.BenchmarkName,
                    GateType = "Regression",
                    Message = $"Performance regression detected: {comparison.RegressionPercentage:F2}% degradation",
                    CurrentValue = comparison.RegressionPercentage,
                    Threshold = threshold
                });
            }

            result.GateStatuses["Regression"] = !comparison.IsRegression ? GateStatus.Pass : GateStatus.Failure;
        }
    }
}