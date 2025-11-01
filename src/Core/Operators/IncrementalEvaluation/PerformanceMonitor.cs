using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace T3.Core.Operators.IncrementalEvaluation
{
    /// <summary>
    /// Monitors and tracks performance metrics for incremental node evaluation
    /// Provides comprehensive analytics for optimization and debugging
    /// </summary>
    public class PerformanceMonitor : IDisposable
    {
        #region Private Fields

        private readonly Dictionary<string, EvaluationMetric> _metrics;
        private readonly List<PerformanceSnapshot> _snapshots;
        private readonly PerformanceThresholdChecker _thresholdChecker;
        private readonly object _lockObject = new object();
        private readonly Timer _periodicSnapshotTimer;
        
        private bool _disposed = false;
        private DateTime _startTime;
        private int _totalEvaluations = 0;
        private int _totalIncrementalEvaluations = 0;

        #endregion

        #region Constructor

        public PerformanceMonitor(TimeSpan? snapshotInterval = null)
        {
            _metrics = new Dictionary<string, EvaluationMetric>();
            _snapshots = new List<PerformanceSnapshot>();
            _thresholdChecker = new PerformanceThresholdChecker();
            _startTime = DateTime.UtcNow;

            // Setup periodic snapshots
            var interval = snapshotInterval ?? TimeSpan.FromSeconds(30);
            _periodicSnapshotTimer = new Timer(_ => TakePeriodicSnapshot(), null, interval, interval);

            InitializeDefaultMetrics();
        }

        #endregion

        #region Metric Tracking

        /// <summary>
        /// Starts tracking a new evaluation operation
        /// </summary>
        public void StartEvaluation()
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
                _metrics[operationId] = metric;
                _totalEvaluations++;
            }
        }

        /// <summary>
        /// Ends tracking an evaluation operation
        /// </summary>
        public void EndEvaluation(TimeSpan duration)
        {
            var operationId = Guid.NewGuid().ToString();
            
            lock (_lockObject)
            {
                if (_metrics.TryGetValue(operationId, out var metric))
                {
                    metric.EndTime = DateTime.UtcNow;
                    metric.Duration = duration;
                    metric.Success = true;
                    RecordMetric(metric);
                }
            }
        }

        /// <summary>
        /// Starts tracking an incremental evaluation
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
                _metrics[operationId] = metric;
                _totalIncrementalEvaluations++;
            }
        }

        /// <summary>
        /// Ends tracking an incremental evaluation
        /// </summary>
        public void EndIncrementalEvaluation(TimeSpan duration, int dirtyNodeCount)
        {
            var operationId = Guid.NewGuid().ToString();
            
            lock (_lockObject)
            {
                if (_metrics.TryGetValue(operationId, out var metric))
                {
                    metric.EndTime = DateTime.UtcNow;
                    metric.Duration = duration;
                    metric.Success = true;
                    metric.DirtyNodeCount = dirtyNodeCount;
                    RecordMetric(metric);
                }
            }
        }

        /// <summary>
        /// Starts tracking a single node evaluation
        /// </summary>
        public void StartSingleNodeEvaluation(NodeId nodeId)
        {
            var operationId = Guid.NewGuid().ToString();
            var metric = new EvaluationMetric
            {
                OperationId = operationId,
                OperationType = EvaluationOperationType.SingleNode,
                StartTime = DateTime.UtcNow,
                ThreadId = Thread.CurrentThread.ManagedThreadId,
                NodeId = nodeId
            };

            lock (_lockObject)
            {
                _metrics[operationId] = metric;
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
                if (_metrics.TryGetValue(operationId, out var metric))
                {
                    metric.EndTime = DateTime.UtcNow;
                    metric.Duration = duration;
                    metric.Success = true;
                    RecordMetric(metric);
                }
            }
        }

        /// <summary>
        /// Records a failed evaluation
        /// </summary>
        public void RecordFailedEvaluation(string errorMessage, EvaluationOperationType operationType)
        {
            var operationId = Guid.NewGuid().ToString();
            var metric = new EvaluationMetric
            {
                OperationId = operationId,
                OperationType = operationType,
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow,
                Duration = TimeSpan.Zero,
                Success = false,
                ErrorMessage = errorMessage
            };

            RecordMetric(metric);
        }

        /// <summary>
        /// Records cache hit/miss statistics
        /// </summary>
        public void RecordCacheOperation(CacheOperationType operation, TimeSpan duration, double hitRate)
        {
            var metric = new EvaluationMetric
            {
                OperationId = Guid.NewGuid().ToString(),
                OperationType = EvaluationOperationType.CacheOperation,
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow,
                Duration = duration,
                CacheHitRate = hitRate,
                Success = true
            };

            RecordMetric(metric);
        }

        #endregion

        #region Performance Analysis

        /// <summary>
        /// Gets comprehensive performance metrics
        /// </summary>
        public IncrementalEvaluationMetrics GetMetrics()
        {
            lock (_lockObject)
            {
                var recentMetrics = GetRecentMetrics(TimeSpan.FromMinutes(5));
                
                return new IncrementalEvaluationMetrics
                {
                    TotalEvaluations = _totalEvaluations,
                    IncrementalEvaluations = _totalIncrementalEvaluations,
                    AverageEvaluationTime = CalculateAverageDuration(recentMetrics, EvaluationOperationType.Full),
                    AverageIncrementalTime = CalculateAverageDuration(recentMetrics, EvaluationOperationType.Incremental),
                    CacheHitRate = CalculateAverageCacheHitRate(recentMetrics),
                    TotalCacheHits = CalculateCacheHits(recentMetrics),
                    TotalCacheMisses = CalculateCacheMisses(recentMetrics),
                    FailureRate = CalculateFailureRate(recentMetrics),
                    ThroughputPerSecond = CalculateThroughputPerSecond(recentMetrics),
                    AverageNodesPerEvaluation = CalculateAverageNodesPerEvaluation(recentMetrics),
                    PerformanceTrend = CalculatePerformanceTrend(recentMetrics)
                };
            }
        }

        /// <summary>
        /// Gets detailed performance report
        /// </summary>
        public PerformanceReport GetDetailedReport()
        {
            lock (_lockObject)
            {
                var report = new PerformanceReport
                {
                    GeneratedAt = DateTime.UtcNow,
                    Uptime = DateTime.UtcNow - _startTime,
                    TotalEvaluations = _totalEvaluations,
                    TotalIncrementalEvaluations = _totalIncrementalEvaluations
                };

                // Calculate various performance metrics
                var fullEvaluations = GetMetricsByOperationType(EvaluationOperationType.Full);
                var incrementalEvaluations = GetMetricsByOperationType(EvaluationOperationType.Incremental);
                var cacheOperations = GetMetricsByOperationType(EvaluationOperationType.CacheOperation);

                report.FullEvaluationStats = CalculateEvaluationStatistics(fullEvaluations);
                report.IncrementalEvaluationStats = CalculateEvaluationStatistics(incrementalEvaluations);
                report.CacheOperationStats = CalculateCacheStatistics(cacheOperations);
                report.BottleneckAnalysis = AnalyzeBottlenecks(fullEvaluations.Concat(incrementalEvaluations));
                report.OptimizationRecommendations = GenerateOptimizationRecommendations(report);

                // Add performance alerts
                report.Alerts = _thresholdChecker.CheckThresholds(report);

                return report;
            }
        }

        /// <summary>
        /// Gets real-time performance snapshot
        /// </summary>
        public PerformanceSnapshot GetRealTimeSnapshot()
        {
            lock (_lockObject)
            {
                var recentMetrics = GetRecentMetrics(TimeSpan.FromSeconds(30));
                
                return new PerformanceSnapshot
                {
                    Timestamp = DateTime.UtcNow,
                    ActiveEvaluations = GetActiveEvaluationCount(),
                    RecentEvaluations = recentMetrics.Count,
                    AverageResponseTime = CalculateAverageDuration(recentMetrics),
                    CurrentThroughput = CalculateCurrentThroughput(recentMetrics),
                    ErrorCount = recentMetrics.Count(m => !m.Success),
                    CacheHitRate = CalculateAverageCacheHitRate(recentMetrics),
                    MemoryUsage = GC.GetTotalMemory(false),
                    ThreadPoolUsage = GetThreadPoolUsage()
                };
            }
        }

        /// <summary>
        /// Gets performance metrics for a specific time range
        /// </summary>
        public List<EvaluationMetric> GetMetricsInRange(DateTime startTime, DateTime endTime)
        {
            lock (_lockObject)
            {
                return _metrics.Values
                    .Where(m => m.StartTime >= startTime && m.StartTime <= endTime)
                    .OrderBy(m => m.StartTime)
                    .ToList();
            }
        }

        #endregion

        #region Optimization and Analysis

        /// <summary>
        /// Analyzes performance patterns and suggests optimizations
        /// </summary>
        public OptimizationAnalysis AnalyzeOptimizationOpportunities()
        {
            var report = GetDetailedReport();
            
            return new OptimizationAnalysis
            {
                PerformanceReport = report,
                BottleneckNodes = IdentifyPerformanceBottlenecks(),
                CacheOptimizationOpportunities = AnalyzeCacheOptimization(),
                EvaluationStrategyRecommendations = RecommendEvaluationStrategy(),
                MemoryOptimizationSuggestions = AnalyzeMemoryUsage(),
                ParallelizationOpportunities = AnalyzeParallelizationOpportunities()
            };
        }

        /// <summary>
        /// Gets performance benchmarks for comparison
        /// </summary>
        public PerformanceBenchmarks GetBenchmarks()
        {
            lock (_lockObject)
            {
                var fullEvaluations = GetMetricsByOperationType(EvaluationOperationType.Full);
                var incrementalEvaluations = GetMetricsByOperationType(EvaluationOperationType.Incremental);
                
                return new PerformanceBenchmarks
                {
                    AverageFullEvaluationTime = CalculateAverageDuration(fullEvaluations),
                    AverageIncrementalEvaluationTime = CalculateAverageDuration(incrementalEvaluations),
                    P95FullEvaluationTime = CalculatePercentileDuration(fullEvaluations, 95),
                    P95IncrementalEvaluationTime = CalculatePercentileDuration(incrementalEvaluations, 95),
                    P99FullEvaluationTime = CalculatePercentileDuration(fullEvaluations, 99),
                    P99IncrementalEvaluationTime = CalculatePercentileDuration(incrementalEvaluations, 99),
                    ThroughputMetrics = CalculateThroughputMetrics(),
                    ScalabilityMetrics = CalculateScalabilityMetrics()
                };
            }
        }

        #endregion

        #region Private Helper Methods

        private void InitializeDefaultMetrics()
        {
            // Set up default threshold values
            _thresholdChecker.SetThreshold("AverageEvaluationTime", TimeSpan.FromMilliseconds(100));
            _thresholdChecker.SetThreshold("FailureRate", 0.05); // 5%
            _thresholdChecker.SetThreshold("CacheHitRate", 0.7); // 70%
            _thresholdChecker.SetThreshold("Throughput", 10.0); // 10 evaluations per second
        }

        private void RecordMetric(EvaluationMetric metric)
        {
            lock (_lockObject)
            {
                _metrics[metric.OperationId] = metric;
                
                // Clean up old metrics (keep last 1000)
                if (_metrics.Count > 1000)
                {
                    var oldestMetrics = _metrics.Values
                        .OrderBy(m => m.StartTime)
                        .Take(_metrics.Count - 1000);
                    
                    foreach (var oldMetric in oldestMetrics)
                    {
                        _metrics.Remove(oldMetric.OperationId);
                    }
                }
            }
        }

        private List<EvaluationMetric> GetRecentMetrics(TimeSpan timeSpan)
        {
            var cutoff = DateTime.UtcNow - timeSpan;
            return _metrics.Values.Where(m => m.StartTime >= cutoff).ToList();
        }

        private List<EvaluationMetric> GetMetricsByOperationType(EvaluationOperationType operationType)
        {
            return _metrics.Values.Where(m => m.OperationType == operationType).ToList();
        }

        private double CalculateAverageDuration(List<EvaluationMetric> metrics)
        {
            if (!metrics.Any())
                return 0;

            return metrics.Average(m => m.Duration.TotalMilliseconds);
        }

        private double CalculateAverageDuration(List<EvaluationMetric> metrics, EvaluationOperationType operationType)
        {
            var filtered = metrics.Where(m => m.OperationType == operationType).ToList();
            return CalculateAverageDuration(filtered);
        }

        private double CalculateAverageCacheHitRate(List<EvaluationMetric> metrics)
        {
            var cacheMetrics = metrics.Where(m => m.CacheHitRate.HasValue).ToList();
            return cacheMetrics.Any() ? cacheMetrics.Average(m => m.CacheHitRate.Value) : 0;
        }

        private int CalculateCacheHits(List<EvaluationMetric> metrics)
        {
            // Simplified calculation
            return metrics.Count(m => m.CacheHitRate.HasValue && m.CacheHitRate.Value > 0.5);
        }

        private int CalculateCacheMisses(List<EvaluationMetric> metrics)
        {
            return metrics.Count - CalculateCacheHits(metrics);
        }

        private double CalculateFailureRate(List<EvaluationMetric> metrics)
        {
            if (!metrics.Any())
                return 0;

            return (double)metrics.Count(m => !m.Success) / metrics.Count;
        }

        private double CalculateThroughputPerSecond(List<EvaluationMetric> metrics)
        {
            if (!metrics.Any())
                return 0;

            var timeSpan = metrics.Max(m => m.StartTime) - metrics.Min(m => m.StartTime);
            if (timeSpan.TotalSeconds <= 0)
                return 0;

            return metrics.Count / timeSpan.TotalSeconds;
        }

        private double CalculateAverageNodesPerEvaluation(List<EvaluationMetric> metrics)
        {
            var nodeMetrics = metrics.Where(m => m.DirtyNodeCount.HasValue).ToList();
            return nodeMetrics.Any() ? nodeMetrics.Average(m => m.DirtyNodeCount.Value) : 0;
        }

        private PerformanceTrend CalculatePerformanceTrend(List<EvaluationMetric> metrics)
        {
            if (metrics.Count < 10)
                return PerformanceTrend.Stable;

            var recent = metrics.Take(metrics.Count / 2).ToList();
            var older = metrics.Skip(metrics.Count / 2).ToList();

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
                SuccessRate = 1.0 - CalculateFailureRate(metrics),
                Throughput = CalculateThroughputPerSecond(metrics)
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

        private CacheStatistics CalculateCacheStatistics(List<EvaluationMetric> metrics)
        {
            return new CacheStatistics
            {
                TotalOperations = metrics.Count,
                AverageHitRate = CalculateAverageCacheHitRate(metrics),
                AverageOperationTime = CalculateAverageDuration(metrics),
                HitRateTrend = CalculateCacheHitRateTrend(metrics)
            };
        }

        private PerformanceTrend CalculateCacheHitRateTrend(List<EvaluationMetric> metrics)
        {
            // Simplified implementation
            return PerformanceTrend.Stable;
        }

        private List<Bottleneck> AnalyzeBottlenecks(IEnumerable<EvaluationMetric> metrics)
        {
            var bottlenecks = new List<Bottleneck>();
            
            // Analyze evaluation duration bottlenecks
            var slowEvaluations = metrics.Where(m => m.Duration.TotalMilliseconds > 100).ToList();
            if (slowEvaluations.Any())
            {
                bottlenecks.Add(new Bottleneck
                {
                    Type = BottleneckType.SlowEvaluation,
                    Severity = slowEvaluations.Count > 100 ? Severity.High : Severity.Medium,
                    Description = $"{slowEvaluations.Count} evaluations took longer than 100ms",
                    AffectedNodes = slowEvaluations.Select(m => m.NodeId).Where(n => n != null).ToList(),
                    AverageDuration = slowEvaluations.Average(m => m.Duration.TotalMilliseconds)
                });
            }

            // Analyze failure rate bottlenecks
            var failureRate = CalculateFailureRate(metrics.ToList());
            if (failureRate > 0.05)
            {
                bottlenecks.Add(new Bottleneck
                {
                    Type = BottleneckType.HighFailureRate,
                    Severity = failureRate > 0.2 ? Severity.High : Severity.Medium,
                    Description = $"Failure rate of {failureRate:P2} exceeds threshold",
                    Metrics = new { FailureRate = failureRate }
                });
            }

            return bottlenecks;
        }

        private List<OptimizationRecommendation> GenerateOptimizationRecommendations(PerformanceReport report)
        {
            var recommendations = new List<OptimizationRecommendation>();

            // Cache optimization
            if (report.CacheOperationStats.AverageHitRate < 0.7)
            {
                recommendations.Add(new OptimizationRecommendation
                {
                    Type = OptimizationType.IncreaseCacheSize,
                    Priority = Priority.High,
                    Description = "Increase cache size to improve hit rate",
                    ExpectedImprovement = "20-30% reduction in evaluation time"
                });
            }

            // Evaluation strategy optimization
            if (report.FullEvaluationStats.AverageDuration > report.IncrementalEvaluationStats.AverageDuration * 5)
            {
                recommendations.Add(new OptimizationRecommendation
                {
                    Type = OptimizationType.UseIncrementalEvaluation,
                    Priority = Priority.Medium,
                    Description = "Consider using incremental evaluation more frequently",
                    ExpectedImprovement = "Significant performance improvement for small changes"
                });
            }

            return recommendations;
        }

        private int GetActiveEvaluationCount()
        {
            return _metrics.Values.Count(m => m.EndTime == null);
        }

        private double CalculateCurrentThroughput(List<EvaluationMetric> recentMetrics)
        {
            if (!recentMetrics.Any())
                return 0;

            var timeSpan = DateTime.UtcNow - recentMetrics.Min(m => m.StartTime);
            return timeSpan.TotalSeconds > 0 ? recentMetrics.Count / timeSpan.TotalSeconds : 0;
        }

        private double GetThreadPoolUsage()
        {
            // Simplified thread pool usage calculation
            ThreadPool.GetAvailableThreads(out var workerThreads, out var completionPortThreads);
            ThreadPool.GetMaxThreads(out var maxWorkerThreads, out var maxCompletionPortThreads);
            
            return (double)(maxWorkerThreads - workerThreads) / maxWorkerThreads;
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

        #region Optimization Analysis Methods

        private List<string> IdentifyPerformanceBottlenecks()
        {
            var report = GetDetailedReport();
            return report.Alerts.Select(alert => $"{alert.Type}: {alert.Message}").ToList();
        }

        private List<CacheOptimizationOpportunity> AnalyzeCacheOptimization()
        {
            var opportunities = new List<CacheOptimizationOpportunity>();
            
            // Analyze cache hit rates by operation type
            var cacheMetrics = GetMetricsByOperationType(EvaluationOperationType.CacheOperation);
            var avgHitRate = CalculateAverageCacheHitRate(cacheMetrics);
            
            if (avgHitRate < 0.8)
            {
                opportunities.Add(new CacheOptimizationOpportunity
                {
                    Type = CacheOptimizationType.IncreaseCacheSize,
                    Description = "Current cache hit rate is below optimal threshold",
                    PotentialImprovement = "Increase cache size to improve hit rate",
                    CurrentHitRate = avgHitRate,
                    TargetHitRate = 0.9
                });
            }

            return opportunities;
        }

        private EvaluationStrategyRecommendation RecommendEvaluationStrategy()
        {
            var metrics = GetMetrics();
            
            if (metrics.TotalEvaluations == 0)
                return new EvaluationStrategyRecommendation { Strategy = EvaluationStrategy.Adaptive };

            var incrementalRatio = (double)metrics.IncrementalEvaluations / metrics.TotalEvaluations;
            
            return new EvaluationStrategyRecommendation
            {
                Strategy = incrementalRatio > 0.5 ? EvaluationStrategy.Incremental : EvaluationStrategy.Adaptive,
                Reasoning = incrementalRatio > 0.5 
                    ? "Incremental evaluation is used frequently and should be preferred"
                    : "Mixed usage pattern suggests adaptive strategy"
            };
        }

        private List<MemoryOptimizationSuggestion> AnalyzeMemoryUsage()
        {
            var suggestions = new List<MemoryOptimizationSuggestion>();
            
            var currentMemory = GC.GetTotalMemory(false);
            var threshold = 100 * 1024 * 1024; // 100MB threshold
            
            if (currentMemory > threshold)
            {
                suggestions.Add(new MemoryOptimizationSuggestion
                {
                    Type = MemoryOptimizationType.ReduceCacheSize,
                    Description = "Memory usage is high, consider reducing cache size",
                    CurrentUsage = currentMemory,
                    SuggestedAction = "Reduce cache size or implement more aggressive eviction"
                });
            }

            return suggestions;
        }

        private List<ParallelizationOpportunity> AnalyzeParallelizationOpportunities()
        {
            var opportunities = new List<ParallelizationOpportunity>();
            
            // Simple heuristic: if average evaluation time > 50ms, consider parallelization
            var avgTime = GetMetrics().AverageEvaluationTime;
            
            if (avgTime > 50)
            {
                opportunities.Add(new ParallelizationOpportunity
                {
                    Type = ParallelizationType.NodeLevelParallelization,
                    Description = "Long evaluation times suggest opportunity for parallel processing",
                    PotentialGain = avgTime > 100 ? "High" : "Medium",
                    Prerequisites = new[] { "Thread-safe node implementations", "Dependency analysis for parallel execution" }
                });
            }

            return opportunities;
        }

        private ThroughputMetrics CalculateThroughputMetrics()
        {
            var metrics = GetMetrics();
            
            return new ThroughputMetrics
            {
                EvaluationsPerSecond = metrics.ThroughputPerSecond,
                CacheHitsPerSecond = metrics.TotalCacheHits / Math.Max(1, (DateTime.UtcNow - _startTime).TotalSeconds),
                NodeEvaluationsPerSecond = metrics.AverageNodesPerEvaluation * metrics.ThroughputPerSecond
            };
        }

        private ScalabilityMetrics CalculateScalabilityMetrics()
        {
            return new ScalabilityMetrics
            {
                LinearScalability = true, // Simplified
                ScalingFactor = 1.2, // 20% overhead per 10x nodes
                BottleneckIdentified = false
            };
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
        /// Represents a single evaluation metric
        /// </summary>
        public class EvaluationMetric
        {
            public string OperationId { get; set; }
            public EvaluationOperationType OperationType { get; set; }
            public NodeId NodeId { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime? EndTime { get; set; }
            public TimeSpan Duration { get; set; }
            public bool Success { get; set; } = true;
            public string ErrorMessage { get; set; }
            public int? DirtyNodeCount { get; set; }
            public double? CacheHitRate { get; set; }
            public int ThreadId { get; set; }
        }

        /// <summary>
        /// Type of evaluation operation
        /// </summary>
        public enum EvaluationOperationType
        {
            Full,
            Incremental,
            SingleNode,
            CacheOperation
        }

        /// <summary>
        /// Performance snapshot for real-time monitoring
        /// </summary>
        public class PerformanceSnapshot
        {
            public DateTime Timestamp { get; set; }
            public int ActiveEvaluations { get; set; }
            public int RecentEvaluations { get; set; }
            public double AverageResponseTime { get; set; }
            public double CurrentThroughput { get; set; }
            public int ErrorCount { get; set; }
            public double CacheHitRate { get; set; }
            public long MemoryUsage { get; set; }
            public double ThreadPoolUsage { get; set; }
        }

        /// <summary>
        /// Comprehensive performance report
        /// </summary>
        public class PerformanceReport
        {
            public DateTime GeneratedAt { get; set; }
            public TimeSpan Uptime { get; set; }
            public int TotalEvaluations { get; set; }
            public int TotalIncrementalEvaluations { get; set; }
            public EvaluationStatistics FullEvaluationStats { get; set; } = new();
            public EvaluationStatistics IncrementalEvaluationStats { get; set; } = new();
            public CacheStatistics CacheOperationStats { get; set; } = new();
            public List<Bottleneck> BottleneckAnalysis { get; set; } = new();
            public List<OptimizationRecommendation> OptimizationRecommendations { get; set; } = new();
            public List<PerformanceAlert> Alerts { get; set; } = new();
        }

        /// <summary>
        /// Statistics for evaluation operations
        /// </summary>
        public class EvaluationStatistics
        {
            public int TotalCount { get; set; }
            public double AverageDuration { get; set; }
            public double MinDuration { get; set; }
            public double MaxDuration { get; set; }
            public double P50Duration { get; set; }
            public double P95Duration { get; set; }
            public double P99Duration { get; set; }
            public double SuccessRate { get; set; }
            public double Throughput { get; set; }
        }

        /// <summary>
        /// Cache operation statistics
        /// </summary>
        public class CacheStatistics
        {
            public int TotalOperations { get; set; }
            public double AverageHitRate { get; set; }
            public double AverageOperationTime { get; set; }
            public PerformanceTrend HitRateTrend { get; set; }
        }

        /// <summary>
        /// Performance bottleneck
        /// </summary>
        public class Bottleneck
        {
            public BottleneckType Type { get; set; }
            public Severity Severity { get; set; }
            public string Description { get; set; }
            public List<NodeId> AffectedNodes { get; set; } = new();
            public object Metrics { get; set; }
            public double AverageDuration { get; set; }
        }

        /// <summary>
        /// Optimization recommendation
        /// </summary>
        public class OptimizationRecommendation
        {
            public OptimizationType Type { get; set; }
            public Priority Priority { get; set; }
            public string Description { get; set; }
            public string ExpectedImprovement { get; set; }
        }

        /// <summary>
        /// Performance alert
        /// </summary>
        public class PerformanceAlert
        {
            public AlertType Type { get; set; }
            public Severity Severity { get; set; }
            public string Message { get; set; }
            public object Data { get; set; }
        }

        /// <summary>
        /// Optimization analysis result
        /// </summary>
        public class OptimizationAnalysis
        {
            public PerformanceReport PerformanceReport { get; set; }
            public List<string> BottleneckNodes { get; set; } = new();
            public List<CacheOptimizationOpportunity> CacheOptimizationOpportunities { get; set; } = new();
            public EvaluationStrategyRecommendation EvaluationStrategyRecommendations { get; set; }
            public List<MemoryOptimizationSuggestion> MemoryOptimizationSuggestions { get; set; } = new();
            public List<ParallelizationOpportunity> ParallelizationOpportunities { get; set; } = new();
        }

        /// <summary>
        /// Performance benchmarks
        /// </summary>
        public class PerformanceBenchmarks
        {
            public double AverageFullEvaluationTime { get; set; }
            public double AverageIncrementalEvaluationTime { get; set; }
            public double P95FullEvaluationTime { get; set; }
            public double P95IncrementalEvaluationTime { get; set; }
            public double P99FullEvaluationTime { get; set; }
            public double P99IncrementalEvaluationTime { get; set; }
            public ThroughputMetrics ThroughputMetrics { get; set; } = new();
            public ScalabilityMetrics ScalabilityMetrics { get; set; } = new();
        }

        // Additional supporting classes for optimization analysis
        public class CacheOptimizationOpportunity
        {
            public CacheOptimizationType Type { get; set; }
            public string Description { get; set; }
            public string PotentialImprovement { get; set; }
            public double CurrentHitRate { get; set; }
            public double TargetHitRate { get; set; }
        }

        public class EvaluationStrategyRecommendation
        {
            public EvaluationStrategy Strategy { get; set; }
            public string Reasoning { get; set; }
        }

        public class MemoryOptimizationSuggestion
        {
            public MemoryOptimizationType Type { get; set; }
            public string Description { get; set; }
            public long CurrentUsage { get; set; }
            public string SuggestedAction { get; set; }
        }

        public class ParallelizationOpportunity
        {
            public ParallelizationType Type { get; set; }
            public string Description { get; set; }
            public string PotentialGain { get; set; }
            public string[] Prerequisites { get; set; }
        }

        public class ThroughputMetrics
        {
            public double EvaluationsPerSecond { get; set; }
            public double CacheHitsPerSecond { get; set; }
            public double NodeEvaluationsPerSecond { get; set; }
        }

        public class ScalabilityMetrics
        {
            public bool LinearScalability { get; set; }
            public double ScalingFactor { get; set; }
            public bool BottleneckIdentified { get; set; }
        }

        // Enums for optimization types
        public enum BottleneckType { SlowEvaluation, HighFailureRate, MemoryPressure, CacheInefficiency }
        public enum Severity { Low, Medium, High, Critical }
        public enum OptimizationType { IncreaseCacheSize, UseIncrementalEvaluation, OptimizeDependencies, ReduceMemoryUsage }
        public enum AlertType { Performance, Memory, Cache, Throughput }
        public enum Priority { Low, Medium, High, Critical }
        public enum PerformanceTrend { Improving, Stable, Degrading }
        public enum CacheOptimizationType { IncreaseCacheSize, OptimizeEvictionPolicy, ImproveHashing }
        public enum MemoryOptimizationType { ReduceCacheSize, ImplementCompression, OptimizeDataStructures }
        public enum ParallelizationType { NodeLevelParallelization, BatchProcessing, PipelineOptimization }

        #endregion
    }
}