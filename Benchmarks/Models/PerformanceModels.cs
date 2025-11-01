using System;
using System.Collections.Generic;

namespace TiXL.PerformanceSuite.Models
{
    /// <summary>
    /// Represents a single performance metric measurement
    /// </summary>
    public class PerformanceMetric
    {
        public DateTime Timestamp { get; set; }
        public string BenchmarkName { get; set; } = string.Empty;
        public string MetricName { get; set; } = string.Empty;
        public double Value { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }

    /// <summary>
    /// Statistical data for a performance metric
    /// </summary>
    public class MetricStatistics
    {
        public string BenchmarkName { get; set; } = string.Empty;
        public string MetricName { get; set; } = string.Empty;
        public double Mean { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public double StandardDeviation { get; set; }
        public int SampleCount { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }

    /// <summary>
    /// System performance overview data
    /// </summary>
    public class SystemPerformanceOverview
    {
        public double AverageCpuUsage { get; set; }
        public double PeakCpuUsage { get; set; }
        public double AverageAvailableMemory { get; set; }
        public double PeakAvailableMemory { get; set; }
        public int TotalGCGen0Collections { get; set; }
        public int TotalGCGen1Collections { get; set; }
        public int TotalGCGen2Collections { get; set; }
    }

    /// <summary>
    /// Complete performance report data
    /// </summary>
    public class PerformanceReportData
    {
        public DateTime GeneratedAt { get; set; }
        public int TotalMetrics { get; set; }
        public TimeSpan MonitoringDuration { get; set; }
        public List<MetricStatistics> MetricStatistics { get; set; } = new();
        public SystemPerformanceOverview? SystemOverview { get; set; }
    }

    /// <summary>
    /// Baseline performance data
    /// </summary>
    public class BaselineData
    {
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string Version { get; set; } = "1.0";
        public string? GitCommit { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        public List<BaselineMetric> Metrics { get; set; } = new();
    }

    /// <summary>
    /// Individual metric in a baseline
    /// </summary>
    public class BaselineMetric
    {
        public string BenchmarkName { get; set; } = string.Empty;
        public string MetricName { get; set; } = string.Empty;
        public double Mean { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public double StandardDeviation { get; set; }
        public int SampleCount { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }

    /// <summary>
    /// Information about a baseline
    /// </summary>
    public class BaselineInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public int MetricCount { get; set; }
        public string Version { get; set; } = "1.0";
    }

    /// <summary>
    /// Performance analysis results
    /// </summary>
    public class PerformanceAnalysis
    {
        public bool HasRegressions { get; set; }
        public int RegressionCount { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<RegressionInfo> Regressions { get; set; } = new();
    }

    /// <summary>
    /// Information about a performance regression
    /// </summary>
    public class RegressionInfo
    {
        public string BenchmarkName { get; set; } = string.Empty;
        public string MetricName { get; set; } = string.Empty;
        public double BaselineValue { get; set; }
        public double CurrentValue { get; set; }
        public double RegressionPercent { get; set; }
        public string Unit { get; set; } = string.Empty;
    }

    /// <summary>
    /// Performance alert data
    /// </summary>
    public class PerformanceAlert
    {
        public string Id { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public AlertType Type { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Trend analysis report
    /// </summary>
    public class TrendAnalysisReport
    {
        public DateTime GeneratedAt { get; set; }
        public int TotalReports { get; set; }
        public TimeSpan AnalysisPeriod { get; set; }
        public List<MetricTrend> MetricTrends { get; set; } = new();
        public TrendDirection OverallTrend { get; set; }
        public string? Message { get; set; }
    }

    /// <summary>
    /// Trend analysis for a specific metric
    /// </summary>
    public class MetricTrend
    {
        public string BenchmarkName { get; set; } = string.Empty;
        public string MetricName { get; set; } = string.Empty;
        public TrendDirection TrendDirection { get; set; }
        public double Slope { get; set; }
        public double R2Score { get; set; }
        public double Confidence { get; set; }
        public List<TrendDataPoint> DataPoints { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Performance trend for a benchmark
    /// </summary>
    public class BenchmarkTrend
    {
        public string BenchmarkName { get; set; } = string.Empty;
        public TrendDirection TrendDirection { get; set; }
        public double Confidence { get; set; }
        public List<TrendDataPoint> DataPoints { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Individual data point in a trend
    /// </summary>
    public class TrendDataPoint
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string BenchmarkName { get; set; } = string.Empty;
        public string MetricName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Detected performance anomaly
    /// </summary>
    public class PerformanceAnomaly
    {
        public string Id { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string BenchmarkName { get; set; } = string.Empty;
        public string MetricName { get; set; } = string.Empty;
        public double CurrentValue { get; set; }
        public double ExpectedValue { get; set; }
        public double Deviation { get; set; }
        public double ZScore { get; set; }
        public AnomalySeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Generated performance insight
    /// </summary>
    public class PerformanceInsight
    {
        public string Id { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public InsightType Type { get; set; }
        public int Priority { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    // Enums
    public enum TrendDirection
    {
        Improving,
        Stable,
        Worsening
    }

    public enum AlertType
    {
        Regression,
        ThresholdExceeded,
        SystemResource,
        GarbageCollection,
        PerformanceDegradation
    }

    public enum AlertSeverity
    {
        Information,
        Low,
        Medium,
        High,
        Critical
    }

    public enum AnomalySeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum InsightType
    {
        Trend,
        Anomaly,
        SystemHealth,
        Recommendation
    }
}