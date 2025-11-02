using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using T3.Core.Logging;
using T3.Core.Operators;

namespace T3.Core.NodeGraph
{
    /// <summary>
    /// Performance Monitor for Incremental Node Graph Evaluation
    /// Provides detailed performance tracking and CPU reduction measurements
    /// for the incremental evaluation system
    /// </summary>
    public class PerformanceMonitor : IDisposable
    {
        #region Private Fields

        private readonly EvaluationContext _context;
        private readonly PerformanceMetrics _metrics;
        private readonly List<PerformanceSnapshot> _snapshots;
        private readonly PerformanceThresholdChecker _thresholdChecker;
        private readonly object _lockObject = new object();
        private readonly Timer _periodicSnapshotTimer;
        
        private bool _disposed = false;
        private DateTime _startTime;
        private int _totalEvaluations = 0;
        private int _totalIncrementalEvaluations = 0;
        private int _totalSingleNodeEvaluations = 0;

        // Performance baselines for comparison
        private readonly Dictionary<string, double> _baselineMetrics = new();
        private readonly List<double> _evaluationTimes = new();
        private readonly List<double> _incrementalTimes = new();

        #endregion

        #region Constructor

        public PerformanceMonitor(EvaluationContext context, TimeSpan? snapshotInterval = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _metrics = new PerformanceMetrics();
            _snapshots = new List<PerformanceSnapshot>();
            _thresholdChecker = new PerformanceThresholdChecker();
            _startTime = DateTime.UtcNow;

            // Setup periodic snapshots
            var interval = snapshotInterval ?? TimeSpan.FromSeconds(30);
            _periodicSnapshotTimer = new Timer(_ => TakePeriodicSnapshot(), null, interval, interval);

            InitializeBaselineMetrics();
            InitializeDefaultThresholds();
        }

        #endregion

        #region Performance Tracking

        /// <summary>
        /// Starts tracking a full evaluation operation
        /// </summary>
        public void StartFullEvaluation()
        {
            var operationId = Guid.NewGuid().ToString();
            var metric = new EvaluationMetric
            {
                OperationId = operationId,
                OperationType = EvaluationOperationType.Full,
                StartTime = DateTime.UtcNow,
                ThreadId = Thread.CurrentThread.ManagedThreadId
            };

            lock (_lockObject)
            {
                _metrics.ActiveMetrics[operationId] = metric;
                _totalEvaluations++;
            }
        }

        /// <summary>
        /// Ends tracking a full evaluation operation
        /// </summary>
        public void EndFullEvaluation(TimeSpan duration, int nodeCount)
        {
            var operationId = Guid.NewGuid().ToString();
            
            lock (_lockObject)
            {
                if (_metrics.ActiveMetrics.TryGetValue(operationId, out var metric))
                {
                    metric.EndTime = DateTime.UtcNow;
                    metric.Duration = duration;
                    metric.Success = true;
                    metric.NodeCount = nodeCount;
                    RecordMetric(metric);
                    _metrics.ActiveMetrics.Remove(operationId);
                }
            }

            _evaluationTimes.Add(duration.TotalMilliseconds);
            _context.Logger.Debug($"Full evaluation completed: {nodeCount} nodes in {duration.TotalMilliseconds:F2}ms");
        }

        /// <summary>
        /// Starts tracking an incremental evaluation operation
        /// </summary>
        public void StartIncrementalEvaluation()
        {
            var operationId = Guid.NewGuid().ToString();
            var metric = new EvaluationMetric
            {
                OperationId = operationId,
                OperationType = EvaluationOperationType.Incremental,
                StartTime = DateTime.UtcNow,
                ThreadId = Thread.CurrentThread.ManagedThreadId
            };

            lock (_lockObject)
            {
                _metrics.ActiveMetrics[operationId] = metric;
                _totalIncrementalEvaluations++;
            }
        }

        /// <summary>
        /// Ends tracking an incremental evaluation operation
        /// </summary>
        public void EndIncrementalEvaluation(TimeSpan duration, int dirtyNodeCount)
        {
            var operationId = Guid.NewGuid().ToString();
            
            lock (_lockObject)
            {
                if (_metrics.ActiveMetrics.TryGetValue(operationId, out var metric))
                {
                    metric.EndTime = DateTime.UtcNow;
                    metric.Duration = duration;
                    metric.Success = true;
                    metric.DirtyNodeCount = dirtyNodeCount;
                    RecordMetric(metric);
                    _metrics.ActiveMetrics.Remove(operationId);
                }
            }

            _incrementalTimes.Add(duration.TotalMilliseconds);
            _context.Logger.Debug($"Incremental evaluation completed: {dirtyNodeCount} dirty nodes in {duration.TotalMilliseconds:F2}ms");
        }

        /// <summary>
        /// Starts tracking a single node evaluation
        /// </summary>
        public void StartSingleNodeEvaluation()
        {
            var operationId = Guid.NewGuid().ToString();
            var metric = new EvaluationMetric
            {
                OperationId = operationId,
                OperationType = EvaluationOperationType.SingleNode,
                StartTime = DateTime.UtcNow,
                ThreadId = Thread.CurrentThread.ManagedThreadId
            };

            lock (_lockObject)
            {
                _metrics.ActiveMetrics[operationId] = metric;
                _totalSingleNodeEvaluations++;
            }
        }

        /// <summary>
        /// Ends tracking a single node evaluation
        /// </summary>
        public void EndSingleNodeEvaluation(TimeSpan duration)
        {
            var operationId = Guid.NewGuid().ToString();
            
            lock (_lockObject)
            {
                if (_metrics.ActiveMetrics.TryGetValue(operationId, out var metric))
                {
                    metric.EndTime = DateTime.UtcNow;
                    metric.Duration = duration;
                    metric.Success = true;
                    RecordMetric(metric);
                    _metrics.ActiveMetrics.Remove(operationId);
                }
            }
        }

        /// <summary>
        /// Records cache hit for performance tracking
        /// </summary>
        public void RecordCacheHit()
        {
            _metrics.CacheHits++;
        }

        /// <summary>
        /// Records cache miss for performance tracking
        /// </summary>
        public void RecordCacheMiss()
        {
            _metrics.CacheMisses++;
        }

        /// <summary>
        /// Records node evaluation for detailed tracking
        /// </summary>
        public void RecordNodeEvaluation(NodeId nodeId, TimeSpan duration, bool success)
        {
            var metric = new NodeMetric
            {
                NodeId = nodeId,
                EvaluationTime = duration,
                Success = success,
                Timestamp = DateTime.UtcNow
            };

            lock (_lockObject)
            {
                _metrics.NodeMetrics.Add(metric);
                
                // Keep only recent metrics to prevent memory growth
                if (_metrics.NodeMetrics.Count > 10000)
                {
                    _metrics.NodeMetrics.RemoveRange(0, 5000);
                }
            }
        }

        /// <summary>
        /// Records memory allocation for tracking
        /// </summary>
        public void RecordMemoryAllocation(long bytes)
        {
            _metrics.TotalMemoryAllocated += bytes;
            _metrics.MemoryAllocations++;
        }

        /// <summary>
        /// Records memory deallocation for tracking
        /// </summary>
        public void RecordMemoryDeallocation(long bytes)
        {
            _metrics.TotalMemoryDeallocated += bytes;
            _metrics.MemoryDeallocations++;
        }

        #endregion

        #region Performance Analysis

        /// <summary>
        /// Gets comprehensive performance metrics
        /// </summary>
        public EvaluationEngineMetrics GetCurrentMetrics()
        {
            lock (_lockObject)
            {
                var recentMetrics = GetRecentMetrics(TimeSpan.FromMinutes(5));
                
                return new EvaluationEngineMetrics
                {
                    TotalEvaluations = _totalEvaluations,
                    IncrementalEvaluations = _totalIncrementalEvaluations,
                    AverageEvaluationTime = CalculateAverageDuration(recentMetrics, EvaluationOperationType.Full),
                    AverageIncrementalTime = CalculateAverageDuration(recentMetrics, EvaluationOperationType.Incremental),
                    CacheHitRate = CalculateCacheHitRate(),
                    TotalCacheHits = _metrics.CacheHits,
                    TotalCacheMisses = _metrics.CacheMisses,
                    Uptime = DateTime.UtcNow - _startTime,
                    CurrentDirtyNodes = CalculateCurrentDirtyNodes(),
                    TotalNodes = CalculateTotalNodes()
                };
            }
        }

        /// <summary>
        /// Gets detailed CPU reduction analysis
        /// </summary>
        public CpuReductionAnalysis GetCpuReductionAnalysis()
        {
            lock (_lockObject)
            {
                var analysis = new CpuReductionAnalysis
                {
                    BaselineEvaluationsPerSecond = _baselineMetrics.GetValueOrDefault("EvaluationsPerSecond", 0.0),
                    CurrentEvaluationsPerSecond = CalculateEvaluationsPerSecond(),
                    BaselineAverageTime = _baselineMetrics.GetValueOrDefault("AverageTime", 0.0),
                    CurrentAverageTime = GetCurrentAverageTime(),
                    BaselineCpuUtilization = _baselineMetrics.GetValueOrDefault("CpuUtilization", 0.0),
                    CurrentCpuUtilization = EstimateCurrentCpuUtilization()
                };

                analysis.CpuReductionPercentage = CalculateCpuReduction(analysis);
                analysis.EstimatedTimeSaved = CalculateEstimatedTimeSaved(analysis);
                analysis.PerformanceImprovementFactor = CalculatePerformanceImprovementFactor(analysis);
                analysis.Recommendation = GenerateOptimizationRecommendation(analysis);

                return analysis;
            }
        }

        /// <summary>
        /// Gets real-time performance snapshot
        /// </summary>
        public RealTimePerformanceSnapshot GetRealTimeSnapshot()
        {
            lock (_lockObject)
            {
                var recentMetrics = GetRecentMetrics(TimeSpan.FromSeconds(30));
                
                return new RealTimePerformanceSnapshot
                {
                    Timestamp = DateTime.UtcNow,
                    ActiveEvaluations = _metrics.ActiveMetrics.Count,
                    RecentEvaluations = recentMetrics.Count,
                    AverageResponseTime = CalculateAverageDuration(recentMetrics),
                    CurrentThroughput = CalculateCurrentThroughput(),
                    ErrorCount = recentMetrics.Count(m => !m.Success),
                    CacheHitRate = CalculateCacheHitRate(),
                    MemoryUsage = GC.GetTotalMemory(false),
                    CpuUtilization = EstimateCurrentCpuUtilization(),
                    DirtyNodePercentage = CalculateDirtyNodePercentage(),
                    PerformanceTrend = CalculatePerformanceTrend()
                };
            }
        }

        /// <summary>
        /// Gets comprehensive performance report
        /// </summary>
        public IncrementalEvaluationPerformanceReport GetPerformanceReport()
        {
            lock (_lockObject)
            {
                var report = new IncrementalEvaluationPerformanceReport
                {
                    IsEnabled = true,
                    GeneratedAt = DateTime.UtcNow,
                    Uptime = DateTime.UtcNow - _startTime,
                    TotalEvaluations = _totalEvaluations,
                    TotalIncrementalEvaluations = _totalIncrementalEvaluations
                };

                // Calculate various performance metrics
                var fullEvaluations = GetMetricsByOperationType(EvaluationOperationType.Full);
                var incrementalEvaluations = GetMetricsByOperationType(EvaluationOperationType.Incremental);
                var singleNodeEvaluations = GetMetricsByOperationType(EvaluationOperationType.SingleNode);

                report.FullEvaluationStats = CalculateEvaluationStatistics(fullEvaluations);
                report.IncrementalEvaluationStats = CalculateEvaluationStatistics(incrementalEvaluations);
                report.SingleNodeEvaluationStats = CalculateEvaluationStatistics(singleNodeEvaluations);
                
                // Calculate CPU reduction metrics
                var cpuAnalysis = GetCpuReductionAnalysis();
                report.CpuReductionPercentage = cpuAnalysis.CpuReductionPercentage;
                report.EstimatedCpuTimeSaved = cpuAnalysis.EstimatedTimeSaved;
                
                // Cache statistics
                var cacheStats = CalculateCacheStatistics();
                report.CacheUtilization = cacheStats.HitRate;
                report.MemoryUsage = cacheStats.TotalMemoryUsage;
                
                // Performance benchmarks
                report.PerformanceImprovementPercentage = cpuAnalysis.PerformanceImprovementFactor;
                report.AverageEvaluationTime = report.FullEvaluationStats.AverageDuration;
                report.AverageIncrementalTime = report.IncrementalEvaluationStats.AverageDuration;
                
                // Generate recommendations
                report.Recommendation = cpuAnalysis.Recommendation;

                return report;
            }
        }

        /// <summary>
        /// Gets performance comparison between full and incremental evaluation
        /// </summary>
        public EvaluationComparisonReport GetEvaluationComparisonReport()
        {
            lock (_lockObject)
            {
                var fullEvaluations = GetMetricsByOperationType(EvaluationOperationType.Full);
                var incrementalEvaluations = GetMetricsByOperationType(EvaluationOperationType.Incremental);

                var report = new EvaluationComparisonReport
                {
                    ReportGenerated = DateTime.UtcNow,
                    TotalFullEvaluations = fullEvaluations.Count,
                    TotalIncrementalEvaluations = incrementalEvaluations.Count,
                    FullEvaluationAverageTime = CalculateAverageDuration(fullEvaluations),
                    IncrementalEvaluationAverageTime = CalculateAverageDuration(incrementalEvaluations),
                    FullEvaluationMedianTime = CalculateMedianDuration(fullEvaluations),
                    IncrementalEvaluationMedianTime = CalculateMedianDuration(incrementalEvaluations)
                };

                if (report.FullEvaluationAverageTime > 0)
                {
                    report.SpeedImprovementFactor = report.FullEvaluationAverageTime / Math.Max(report.IncrementalEvaluationAverageTime, 0.001);
                    report.PercentageImprovement = (1.0 - report.IncrementalEvaluationAverageTime / report.FullEvaluationAverageTime) * 100;
                }

                report.CpuEfficiencyGain = CalculateCpuEfficiencyGain();
                report.MemoryEfficiencyGain = CalculateMemoryEfficiencyGain();
                report.OverallRecommendation = GenerateOverallRecommendation(report);

                return report;
            }
        }

        #endregion

        #region Private Helper Methods

        private void InitializeBaselineMetrics()
        {
            // Establish baseline performance metrics for comparison
            _baselineMetrics["EvaluationsPerSecond"] = 10.0; // Baseline: 10 evaluations per second
            _baselineMetrics["AverageTime"] = 100.0; // Baseline: 100ms average evaluation time
            _baselineMetrics["CpuUtilization"] = 80.0; // Baseline: 80% CPU utilization
        }

        private void InitializeDefaultThresholds()
        {
            _thresholdChecker.SetThreshold("AverageEvaluationTime", TimeSpan.FromMilliseconds(200));
            _thresholdChecker.SetThreshold("CpuReductionPercentage", 20.0); // 20% minimum improvement
            _thresholdChecker.SetThreshold("CacheHitRate", 0.6); // 60% minimum hit rate
            _thresholdChecker.SetThreshold("Throughput", 5.0); // 5 evaluations per second minimum
        }

        private void RecordMetric(EvaluationMetric metric)
        {
            lock (_lockObject)
            {
                _metrics.CompletedMetrics.Add(metric);
                
                // Clean up old metrics
                if (_metrics.CompletedMetrics.Count > 1000)
                {
                    var cutoff = DateTime.UtcNow.AddMinutes(-10);
                    _metrics.CompletedMetrics.RemoveAll(m => m.StartTime < cutoff);
                }
            }
        }

        private List<EvaluationMetric> GetRecentMetrics(TimeSpan timeSpan)
        {
            var cutoff = DateTime.UtcNow - timeSpan;
            return _metrics.CompletedMetrics.Where(m => m.StartTime >= cutoff).ToList();
        }

        private List<EvaluationMetric> GetMetricsByOperationType(EvaluationOperationType operationType)
        {
            return _metrics.CompletedMetrics.Where(m => m.OperationType == operationType).ToList();
        }

        private double CalculateAverageDuration(List<EvaluationMetric> metrics)
        {
            if (!metrics.Any())
                return 0;

            return metrics.Average(m => m.Duration.TotalMilliseconds);
        }

        private double CalculateMedianDuration(List<EvaluationMetric> metrics)
        {
            if (!metrics.Any())
                return 0;

            var durations = metrics.Select(m => m.Duration.TotalMilliseconds).OrderBy(d => d).ToList();
            var count = durations.Count;
            return count % 2 == 0 
                ? (durations[count / 2 - 1] + durations[count / 2]) / 2.0 
                : durations[count / 2];
        }

        private double CalculateCacheHitRate()
        {
            var total = _metrics.CacheHits + _metrics.CacheMisses;
            return total > 0 ? (double)_metrics.CacheHits / total : 0.0;
        }

        private double CalculateEvaluationsPerSecond()
        {
            var recentMetrics = GetRecentMetrics(TimeSpan.FromMinutes(1));
            return recentMetrics.Count / 60.0;
        }

        private double GetCurrentAverageTime()
        {
            var recentMetrics = GetRecentMetrics(TimeSpan.FromMinutes(5));
            return CalculateAverageDuration(recentMetrics);
        }

        private double EstimateCurrentCpuUtilization()
        {
            // Simple estimation based on evaluation frequency and duration
            var throughput = CalculateEvaluationsPerSecond();
            var avgTime = GetCurrentAverageTime();
            var estimatedCpu = (throughput * avgTime) / 1000.0; // Convert to percentage
            return Math.Min(estimatedCpu, 100.0);
        }

        private double CalculateCpuReduction(CpuReductionAnalysis analysis)
        {
            if (analysis.BaselineCpuUtilization <= 0)
                return 0.0;

            var reduction = (analysis.BaselineCpuUtilization - analysis.CurrentCpuUtilization) / analysis.BaselineCpuUtilization * 100;
            return Math.Max(reduction, 0.0);
        }

        private TimeSpan CalculateEstimatedTimeSaved(CpuReductionAnalysis analysis)
        {
            var savedPerEvaluation = analysis.BaselineAverageTime - analysis.CurrentAverageTime;
            var totalEvaluations = _totalEvaluations + _totalIncrementalEvaluations;
            return TimeSpan.FromMilliseconds(Math.Max(savedPerEvaluation * totalEvaluations, 0));
        }

        private double CalculatePerformanceImprovementFactor(CpuReductionAnalysis analysis)
        {
            if (analysis.BaselineAverageTime <= 0 || analysis.CurrentAverageTime <= 0)
                return 1.0;

            return analysis.BaselineAverageTime / analysis.CurrentAverageTime;
        }

        private int CalculateCurrentDirtyNodes()
        {
            // This would be connected to the actual dirty node tracking
            return _metrics.NodeMetrics.Count(m => !m.Success || (DateTime.UtcNow - m.Timestamp).TotalMinutes < 5);
        }

        private int CalculateTotalNodes()
        {
            // This would be connected to the actual node count
            return _metrics.NodeMetrics.Select(m => m.NodeId).Distinct().Count();
        }

        private double CalculateCurrentThroughput()
        {
            var recentMetrics = GetRecentMetrics(TimeSpan.FromSeconds(30));
            return recentMetrics.Count / 30.0;
        }

        private double CalculateDirtyNodePercentage()
        {
            var totalNodes = CalculateTotalNodes();
            var dirtyNodes = CalculateCurrentDirtyNodes();
            return totalNodes > 0 ? (double)dirtyNodes / totalNodes : 0.0;
        }

        private PerformanceTrend CalculatePerformanceTrend()
        {
            var recent = GetRecentMetrics(TimeSpan.FromMinutes(2));
            var older = GetRecentMetrics(TimeSpan.FromMinutes(5)).Take(recent.Count).ToList();

            if (recent.Count < 10 || older.Count < 10)
                return PerformanceTrend.Stable;

            var recentAvg = CalculateAverageDuration(recent);
            var olderAvg = CalculateAverageDuration(older);

            var change = (recentAvg - olderAvg) / olderAvg;

            return change switch
            {
                > 0.1 => PerformanceTrend.Degrading,
                < -0.1 => PerformanceTrend.Improving,
                _ => PerformanceTrend.Stable
            };
        }

        private EvaluationStatistics CalculateEvaluationStatistics(List<EvaluationMetric> metrics)
        {
            if (!metrics.Any())
                return new EvaluationStatistics();

            return new EvaluationStatistics
            {
                TotalCount = metrics.Count,
                AverageDuration = CalculateAverageDuration(metrics),
                MinDuration = metrics.Min(m => m.Duration.TotalMilliseconds),
                MaxDuration = metrics.Max(m => m.Duration.TotalMilliseconds),
                P50Duration = CalculatePercentileDuration(metrics, 50),
                P95Duration = CalculatePercentileDuration(metrics, 95),
                P99Duration = CalculatePercentileDuration(metrics, 99),
                SuccessRate = 1.0 - (double)metrics.Count(m => !m.Success) / metrics.Count,
                Throughput = CalculateEvaluationsPerSecond()
            };
        }

        private double CalculatePercentileDuration(List<EvaluationMetric> metrics, int percentile)
        {
            if (!metrics.Any())
                return 0;

            var sortedDurations = metrics.Select(m => m.Duration.TotalMilliseconds).OrderBy(d => d).ToList();
            var index = (int)Math.Ceiling((percentile / 100.0) * sortedDurations.Count) - 1;
            return sortedDurations[Math.Max(0, Math.Min(index, sortedDurations.Count - 1))];
        }

        private CacheStatistics CalculateCacheStatistics()
        {
            return new CacheStatistics
            {
                HitRate = CalculateCacheHitRate(),
                TotalMemoryUsage = _metrics.TotalMemoryAllocated - _metrics.TotalMemoryDeallocated,
                CacheOperations = _metrics.CacheHits + _metrics.CacheMisses
            };
        }

        private double CalculateCpuEfficiencyGain()
        {
            // Calculate CPU efficiency based on work done per CPU time
            var currentEfficiency = CalculateEvaluationsPerSecond() / Math.Max(EstimateCurrentCpuUtilization(), 1.0);
            var baselineEfficiency = _baselineMetrics.GetValueOrDefault("EvaluationsPerSecond", 10.0) / 80.0; // Baseline: 10/sec at 80% CPU
            
            return baselineEfficiency > 0 ? (currentEfficiency / baselineEfficiency - 1.0) * 100 : 0.0;
        }

        private double CalculateMemoryEfficiencyGain()
        {
            // Calculate memory efficiency based on memory usage vs evaluations
            var memoryPerEvaluation = _metrics.TotalMemoryAllocated / Math.Max(_totalEvaluations + _totalIncrementalEvaluations, 1);
            var baselineMemoryPerEvaluation = 1024 * 1024; // 1MB baseline estimate
            
            return baselineMemoryPerEvaluation > 0 ? (1.0 - (double)memoryPerEvaluation / baselineMemoryPerEvaluation) * 100 : 0.0;
        }

        private string GenerateOptimizationRecommendation(CpuReductionAnalysis analysis)
        {
            if (analysis.CpuReductionPercentage < 10)
                return "Low CPU reduction detected. Consider optimizing node dependencies or increasing cache size";

            if (analysis.PerformanceImprovementFactor < 1.5)
                return "Performance improvement below optimal. Consider adjusting evaluation strategy";

            if (analysis.CurrentCpuUtilization > 50)
                return "High CPU utilization. Consider load balancing or parallel evaluation";

            return "CPU reduction is optimal with current configuration";
        }

        private string GenerateOverallRecommendation(EvaluationComparisonReport report)
        {
            if (report.PercentageImprovement > 30)
                return "Incremental evaluation provides significant benefits. Continue using current strategy";

            if (report.SpeedImprovementFactor < 2.0)
                return "Consider optimizing incremental evaluation strategy for better performance";

            return "Performance is acceptable with current evaluation strategy";
        }

        private void TakePeriodicSnapshot()
        {
            try
            {
                var snapshot = GetRealTimeSnapshot();
                
                lock (_lockObject)
                {
                    _snapshots.Add(snapshot);
                    
                    // Keep only last 100 snapshots
                    if (_snapshots.Count > 100)
                    {
                        _snapshots.RemoveAt(0);
                    }
                }
            }
            catch (Exception)
            {
                // Ignore errors in periodic snapshots
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed)
                return;

            _periodicSnapshotTimer?.Dispose();
            _thresholdChecker?.Dispose();
            
            _disposed = true;
        }

        #endregion

        #region Supporting Classes

        /// <summary>
        /// Performance metric for an evaluation operation
        /// </summary>
        public class EvaluationMetric
        {
            public string OperationId { get; set; } = string.Empty;
            public EvaluationOperationType OperationType { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime? EndTime { get; set; }
            public TimeSpan Duration { get; set; }
            public bool Success { get; set; } = true;
            public int ThreadId { get; set; }
            public int? NodeCount { get; set; }
            public int? DirtyNodeCount { get; set; }
            public string? ErrorMessage { get; set; }
        }

        /// <summary>
        /// Performance metric for individual node evaluation
        /// </summary>
        public class NodeMetric
        {
            public NodeId NodeId { get; set; } = new(string.Empty);
            public TimeSpan EvaluationTime { get; set; }
            public bool Success { get; set; }
            public DateTime Timestamp { get; set; }
        }

        /// <summary>
        /// Core performance metrics collection
        /// </summary>
        private class PerformanceMetrics
        {
            public Dictionary<string, EvaluationMetric> ActiveMetrics { get; } = new();
            public List<EvaluationMetric> CompletedMetrics { get; } = new();
            public List<NodeMetric> NodeMetrics { get; } = new();
            
            public int CacheHits { get; set; }
            public int CacheMisses { get; set; }
            
            public long TotalMemoryAllocated { get; set; }
            public long TotalMemoryDeallocated { get; set; }
            public int MemoryAllocations { get; set; }
            public int MemoryDeallocations { get; set; }
        }

        /// <summary>
        /// Performance threshold checker
        /// </summary>
        private class PerformanceThresholdChecker
        {
            private readonly Dictionary<string, object> _thresholds = new();

            public void SetThreshold<T>(string name, T value)
            {
                _thresholds[name] = value;
            }

            public T GetThreshold<T>(string name)
            {
                return _thresholds.TryGetValue(name, out var value) ? (T)value : default!;
            }

            public bool IsThresholdExceeded(string name, object actual)
            {
                if (!_thresholds.TryGetValue(name, out var threshold))
                    return false;

                // Simple comparison logic - would be more sophisticated in real implementation
                return false;
            }

            public void Dispose()
            {
                _thresholds.Clear();
            }
        }

        /// <summary>
        /// Real-time performance snapshot
        /// </summary>
        public class RealTimePerformanceSnapshot
        {
            public DateTime Timestamp { get; set; }
            public int ActiveEvaluations { get; set; }
            public int RecentEvaluations { get; set; }
            public double AverageResponseTime { get; set; }
            public double CurrentThroughput { get; set; }
            public int ErrorCount { get; set; }
            public double CacheHitRate { get; set; }
            public long MemoryUsage { get; set; }
            public double CpuUtilization { get; set; }
            public double DirtyNodePercentage { get; set; }
            public PerformanceTrend PerformanceTrend { get; set; }
        }

        /// <summary>
        /// CPU reduction analysis results
        /// </summary>
        public class CpuReductionAnalysis
        {
            public double BaselineEvaluationsPerSecond { get; set; }
            public double CurrentEvaluationsPerSecond { get; set; }
            public double BaselineAverageTime { get; set; }
            public double CurrentAverageTime { get; set; }
            public double BaselineCpuUtilization { get; set; }
            public double CurrentCpuUtilization { get; set; }
            public double CpuReductionPercentage { get; set; }
            public TimeSpan EstimatedTimeSaved { get; set; }
            public double PerformanceImprovementFactor { get; set; }
            public string Recommendation { get; set; } = string.Empty;
        }

        /// <summary>
        /// Evaluation comparison report
        /// </summary>
        public class EvaluationComparisonReport
        {
            public DateTime ReportGenerated { get; set; }
            public int TotalFullEvaluations { get; set; }
            public int TotalIncrementalEvaluations { get; set; }
            public double FullEvaluationAverageTime { get; set; }
            public double IncrementalEvaluationAverageTime { get; set; }
            public double FullEvaluationMedianTime { get; set; }
            public double IncrementalEvaluationMedianTime { get; set; }
            public double SpeedImprovementFactor { get; set; }
            public double PercentageImprovement { get; set; }
            public double CpuEfficiencyGain { get; set; }
            public double MemoryEfficiencyGain { get; set; }
            public string OverallRecommendation { get; set; } = string.Empty;
        }

        /// <summary>
        /// Cache statistics
        /// </summary>
        public class CacheStatistics
        {
            public double HitRate { get; set; }
            public long TotalMemoryUsage { get; set; }
            public int CacheOperations { get; set; }
        }

        public enum EvaluationOperationType
        {
            Full,
            Incremental,
            SingleNode
        }

        public enum PerformanceTrend
        {
            Improving,
            Stable,
            Degrading
        }

        #endregion
    }
}