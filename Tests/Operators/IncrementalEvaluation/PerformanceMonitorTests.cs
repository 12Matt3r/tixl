using Xunit;
using Xunit.Abstractions;
using TiXL.Tests.Categories;
using TiXL.Tests.Fixtures;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using T3.Core.Operators.IncrementalEvaluation;
using Microsoft.Extensions.Logging;

namespace TiXL.Tests.Operators.IncrementalEvaluation
{
    /// <summary>
    /// Unit tests for PerformanceMonitor
    /// </summary>
    [Category(TestCategories.Operators)]
    [Category(TestCategories.Unit)]
    public class PerformanceMonitorTests : CoreTestFixture
    {
        private readonly ITestOutputHelper _output;
        private readonly ILogger<PerformanceMonitorTests> _logger;

        public PerformanceMonitorTests(ITestOutputHelper output) : base()
        {
            _output = output;
            _logger = ServiceProvider.GetRequiredService<ILogger<PerformanceMonitorTests>>();
        }

        [Fact]
        public void PerformanceMonitor_CreateInstance_InitializesCorrectly()
        {
            // Arrange & Act
            var monitor = new PerformanceMonitor();

            // Assert
            monitor.Should().NotBeNull();
            var metrics = monitor.GetMetrics();
            metrics.TotalEvaluations.Should().Be(0);
            metrics.TotalEvaluationTime.Should().Be(TimeSpan.Zero);
            metrics.AverageEvaluationTime.Should().Be(TimeSpan.Zero);
        }

        [Fact]
        public void PerformanceMonitor_RecordParameterUpdate_WithValidData_RecordsSuccessfully()
        {
            // Arrange
            var monitor = new PerformanceMonitor();
            var nodeId = "test-node";
            var parameterName = "TestParameter";

            // Act
            monitor.RecordParameterUpdate(nodeId, parameterName);

            // Assert
            var metrics = monitor.GetMetrics();
            metrics.ParameterUpdates.Should().BeGreaterThan(0);
            metrics.NodesAffectedByParameterUpdates.Should().Be(1);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void PerformanceMonitor_RecordParameterUpdate_WithInvalidNodeId_ThrowsArgumentException(string nodeId)
        {
            // Arrange
            var monitor = new PerformanceMonitor();
            var parameterName = "TestParameter";

            // Act & Assert
            Action act = () => monitor.RecordParameterUpdate(nodeId, parameterName);
            act.Should().Throw<ArgumentException>();
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void PerformanceMonitor_RecordParameterUpdate_WithInvalidParameterName_ThrowsArgumentException(string parameterName)
        {
            // Arrange
            var monitor = new PerformanceMonitor();
            var nodeId = "test-node";

            // Act & Assert
            Action act = () => monitor.RecordParameterUpdate(nodeId, parameterName);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void PerformanceMonitor_RecordEvaluationStart_StartsTimingCorrectly()
        {
            // Arrange
            var monitor = new PerformanceMonitor();

            // Act
            monitor.RecordEvaluationStart();

            // Assert - Should start internal timing
            var metrics = monitor.GetMetrics();
            metrics.IsCurrentlyMonitoring.Should().BeTrue();
        }

        [Fact]
        public void PerformanceMonitor_RecordEvaluationComplete_WithValidData_RecordsCorrectly()
        {
            // Arrange
            var monitor = new PerformanceMonitor();
            var nodeCount = 5;
            var evaluationTime = TimeSpan.FromMilliseconds(150);

            monitor.RecordEvaluationStart();

            // Act
            monitor.RecordEvaluationComplete(nodeCount, evaluationTime);

            // Assert
            var metrics = monitor.GetMetrics();
            metrics.TotalEvaluations.Should().Be(1);
            metrics.TotalEvaluationTime.Should().Be(evaluationTime);
            metrics.AverageEvaluationTime.Should().Be(evaluationTime);
            metrics.AverageNodesPerEvaluation.Should().Be(nodeCount);
            metrics.IsCurrentlyMonitoring.Should().BeFalse();
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        public void PerformanceMonitor_RecordEvaluationComplete_WithInvalidNodeCount_ThrowsArgumentException(int nodeCount)
        {
            // Arrange
            var monitor = new PerformanceMonitor();
            var evaluationTime = TimeSpan.FromMilliseconds(150);

            monitor.RecordEvaluationStart();

            // Act & Assert
            Action act = () => monitor.RecordEvaluationComplete(nodeCount, evaluationTime);
            act.Should().Throw<ArgumentException>();
        }

        [Theory]
        [InlineData(-1)]
        public void PerformanceMonitor_RecordEvaluationComplete_WithInvalidTime_ThrowsArgumentException(int milliseconds)
        {
            // Arrange
            var monitor = new PerformanceMonitor();
            var nodeCount = 5;
            var evaluationTime = TimeSpan.FromMilliseconds(milliseconds);

            monitor.RecordEvaluationStart();

            // Act & Assert
            Action act = () => monitor.RecordEvaluationComplete(nodeCount, evaluationTime);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void PerformanceMonitor_RecordEvaluationComplete_WithoutStart_HandlesGracefully()
        {
            // Arrange
            var monitor = new PerformanceMonitor();
            var nodeCount = 5;
            var evaluationTime = TimeSpan.FromMilliseconds(150);

            // Act - Complete without starting
            monitor.RecordEvaluationComplete(nodeCount, evaluationTime);

            // Assert - Should handle gracefully and record metrics
            var metrics = monitor.GetMetrics();
            metrics.TotalEvaluations.Should().Be(1);
        }

        [Fact]
        public void PerformanceMonitor_MultipleEvaluations_AccumulatesMetricsCorrectly()
        {
            // Arrange
            var monitor = new PerformanceMonitor();
            var evaluations = new[]
            {
                new { nodes = 2, time = TimeSpan.FromMilliseconds(100) },
                new { nodes = 5, time = TimeSpan.FromMilliseconds(250) },
                new { nodes = 3, time = TimeSpan.FromMilliseconds(150) }
            };

            // Act
            foreach (var eval in evaluations)
            {
                monitor.RecordEvaluationStart();
                monitor.RecordEvaluationComplete(eval.nodes, eval.time);
            }

            // Assert
            var metrics = monitor.GetMetrics();
            metrics.TotalEvaluations.Should().Be(3);
            metrics.TotalEvaluationTime.Should().Be(TimeSpan.FromMilliseconds(500));
            metrics.AverageEvaluationTime.Should().Be(TimeSpan.FromMilliseconds(500) / 3);
            metrics.AverageNodesPerEvaluation.Should().Be((2 + 5 + 3) / 3.0);
        }

        [Fact]
        public void PerformanceMonitor_RecordMemoryUsage_WithValidData_RecordsSuccessfully()
        {
            // Arrange
            var monitor = new PerformanceMonitor();
            var memoryUsage = 1024 * 1024; // 1MB

            // Act
            monitor.RecordMemoryUsage(memoryUsage);

            // Assert
            var metrics = monitor.GetMetrics();
            metrics.PeakMemoryUsageBytes.Should().Be(memoryUsage);
            metrics.CurrentMemoryUsageBytes.Should().Be(memoryUsage);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(-1024)]
        public void PerformanceMonitor_RecordMemoryUsage_WithInvalidUsage_ThrowsArgumentException(long memoryUsage)
        {
            // Arrange
            var monitor = new PerformanceMonitor();

            // Act & Assert
            Action act = () => monitor.RecordMemoryUsage(memoryUsage);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void PerformanceMonitor_RecordMemoryUsage_UpdatesPeakCorrectly()
        {
            // Arrange
            var monitor = new PerformanceMonitor();

            // Act - Record increasing memory usage
            monitor.RecordMemoryUsage(1024);      // 1KB
            monitor.RecordMemoryUsage(2048);      // 2KB
            monitor.RecordMemoryUsage(4096);      // 4KB

            // Assert - Peak should be the maximum value recorded
            var metrics = monitor.GetMetrics();
            metrics.PeakMemoryUsageBytes.Should().Be(4096);
            metrics.CurrentMemoryUsageBytes.Should().Be(4096);
        }

        [Fact]
        public void PerformanceMonitor_RecordCacheHit_UpdatesCacheStatistics()
        {
            // Arrange
            var monitor = new PerformanceMonitor();

            // Act
            monitor.RecordCacheHit();

            // Assert
            var metrics = monitor.GetMetrics();
            metrics.CacheHits.Should().BeGreaterThan(0);
        }

        [Fact]
        public void PerformanceMonitor_RecordCacheMiss_UpdatesCacheStatistics()
        {
            // Arrange
            var monitor = new PerformanceMonitor();

            // Act
            monitor.RecordCacheMiss();

            // Assert
            var metrics = monitor.GetMetrics();
            metrics.CacheMisses.Should().BeGreaterThan(0);
        }

        [Fact]
        public void PerformanceMonitor_CacheHitRate_CalculatesCorrectly()
        {
            // Arrange
            var monitor = new PerformanceMonitor();

            // Act - Record cache hits and misses
            for (int i = 0; i < 8; i++)
            {
                monitor.RecordCacheHit();
            }
            for (int i = 0; i < 2; i++)
            {
                monitor.RecordCacheMiss();
            }

            // Assert
            var metrics = monitor.GetMetrics();
            metrics.CacheHits.Should().Be(8);
            metrics.CacheMisses.Should().Be(2);
            metrics.CacheHitRate.Should().Be(8.0 / 10.0); // 80%
        }

        [Fact]
        public void PerformanceMonitor_Reset_ResetsAllMetrics()
        {
            // Arrange
            var monitor = new PerformanceMonitor();
            
            // Add some data
            monitor.RecordParameterUpdate("node1", "param1");
            monitor.RecordEvaluationStart();
            monitor.RecordEvaluationComplete(5, TimeSpan.FromMilliseconds(100));
            monitor.RecordMemoryUsage(1024);
            monitor.RecordCacheHit();
            monitor.RecordCacheMiss();

            // Act
            monitor.Reset();

            // Assert
            var metrics = monitor.GetMetrics();
            metrics.TotalEvaluations.Should().Be(0);
            metrics.TotalEvaluationTime.Should().Be(TimeSpan.Zero);
            metrics.ParameterUpdates.Should().Be(0);
            metrics.CacheHits.Should().Be(0);
            metrics.CacheMisses.Should().Be(0);
            metrics.PeakMemoryUsageBytes.Should().Be(0);
        }

        [Fact]
        public void PerformanceMonitor_GetDetailedMetrics_ReturnsComprehensiveData()
        {
            // Arrange
            var monitor = new PerformanceMonitor();

            // Act - Record various metrics
            monitor.RecordParameterUpdate("node1", "param1");
            monitor.RecordParameterUpdate("node2", "param2");
            
            monitor.RecordEvaluationStart();
            monitor.RecordEvaluationComplete(3, TimeSpan.FromMilliseconds(150));
            
            monitor.RecordMemoryUsage(2048);
            monitor.RecordCacheHit();
            monitor.RecordCacheMiss();
            monitor.RecordCacheHit();

            var detailedMetrics = monitor.GetDetailedMetrics();

            // Assert
            detailedMetrics.Should().NotBeNull();
            detailedMetrics.TotalEvaluations.Should().Be(1);
            detailedMetrics.ParameterUpdates.Should().Be(2);
            detailedMetrics.CacheHits.Should().Be(2);
            detailedMetrics.CacheMisses.Should().Be(1);
            detailedMetrics.CacheHitRate.Should().Be(2.0 / 3.0);
            detailedMetrics.PeakMemoryUsageBytes.Should().Be(2048);
            detailedMetrics.CurrentMemoryUsageBytes.Should().Be(2048);
        }

        [Fact]
        public void PerformanceMonitor_PerformanceBottlenecks_DetectsSlowEvaluations()
        {
            // Arrange
            var monitor = new PerformanceMonitor();
            var slowThreshold = TimeSpan.FromMilliseconds(100);

            // Act - Record both fast and slow evaluations
            monitor.RecordEvaluationStart();
            monitor.RecordEvaluationComplete(2, TimeSpan.FromMilliseconds(50));  // Fast
            
            monitor.RecordEvaluationStart();
            monitor.RecordEvaluationComplete(3, TimeSpan.FromMilliseconds(200)); // Slow
            
            monitor.RecordEvaluationStart();
            monitor.RecordEvaluationComplete(1, TimeSpan.FromMilliseconds(300)); // Very slow

            var bottlenecks = monitor.GetPerformanceBottlenecks(slowThreshold);

            // Assert
            bottlenecks.Should().NotBeNull();
            bottlenecks.SlowEvaluations.Should().HaveCountGreaterOrEqualTo(2); // At least 2 should be slow
            bottlenecks.AverageSlowEvaluationTime.Should().BeGreaterThan(slowThreshold);
        }

        [Fact]
        public void PerformanceMonitor_ConcurrencyMetrics_TracksParallelOperations()
        {
            // Arrange
            var monitor = new PerformanceMonitor();

            // Act - Record parallel evaluation
            monitor.RecordParallelEvaluationStart();
            monitor.RecordEvaluationStart();
            monitor.RecordEvaluationComplete(2, TimeSpan.FromMilliseconds(100));
            monitor.RecordEvaluationComplete(3, TimeSpan.FromMilliseconds(150));
            monitor.RecordParallelEvaluationComplete(5); // 5 nodes evaluated in parallel

            var metrics = monitor.GetMetrics();

            // Assert
            metrics.ParallelEvaluations.Should().Be(1);
            metrics.ParallelNodesEvaluated.Should().Be(5);
            metrics.MaxConcurrencyLevel.Should().BeGreaterThan(0);
        }

        [Fact]
        public void PerformanceMonitor_TrendAnalysis_TracksPerformanceOverTime()
        {
            // Arrange
            var monitor = new PerformanceMonitor();

            // Act - Record evaluations with varying performance
            for (int i = 0; i < 10; i++)
            {
                monitor.RecordEvaluationStart();
                var evaluationTime = TimeSpan.FromMilliseconds(100 + (i * 10)); // Increasing time
                monitor.RecordEvaluationComplete(2, evaluationTime);
            }

            var trends = monitor.GetPerformanceTrends();

            // Assert
            trends.Should().NotBeNull();
            trends.PerformanceTrend.Should().NotBe(PerformanceTrend.Stable); // Should show improvement/degradation
            trends.EvaluationsPerSecond.Should().BeGreaterThan(0);
        }

        [Fact]
        public void PerformanceMonitor_ComplexScenario_EndToEndWorkflow_TracksCorrectly()
        {
            // Arrange
            var monitor = new PerformanceMonitor();

            // Act - Simulate complex evaluation workflow
            // 1. Parameter updates
            monitor.RecordParameterUpdate("input-node", "value");
            monitor.RecordParameterUpdate("process-node", "operation");
            
            // 2. Memory usage tracking
            monitor.RecordMemoryUsage(1024 * 1024); // 1MB
            
            // 3. Cache operations
            monitor.RecordCacheHit(); // Cache hit for input-node
            monitor.RecordCacheMiss(); // Cache miss for process-node
            monitor.RecordCacheHit(); // Cache hit for result
            
            // 4. Evaluation
            monitor.RecordEvaluationStart();
            monitor.RecordEvaluationComplete(3, TimeSpan.FromMilliseconds(200));
            
            // 5. More memory tracking
            monitor.RecordMemoryUsage(2 * 1024 * 1024); // 2MB
            
            // 6. More cache operations
            monitor.RecordCacheHit();
            monitor.RecordCacheMiss();

            var metrics = monitor.GetDetailedMetrics();

            // Assert - Verify comprehensive tracking
            metrics.ParameterUpdates.Should().Be(2);
            metrics.TotalEvaluations.Should().Be(1);
            metrics.CacheHits.Should().Be(3);
            metrics.CacheMisses.Should().Be(2);
            metrics.CacheHitRate.Should().Be(3.0 / 5.0); // 60%
            metrics.PeakMemoryUsageBytes.Should().Be(2 * 1024 * 1024);
            metrics.AverageEvaluationTime.Should().Be(TimeSpan.FromMilliseconds(200));
        }

        [Fact]
        public void PerformanceMonitor_ThreadSafety_ConcurrentOperations_Succeeds()
        {
            // Arrange
            var monitor = new PerformanceMonitor();
            var tasks = new List<Task>();
            var operationsPerTask = 100;

            // Act - Perform concurrent operations
            for (int taskIndex = 0; taskIndex < 5; taskIndex++)
            {
                tasks.Add(Task.Run(() =>
                {
                    for (int i = 0; i < operationsPerTask; i++)
                    {
                        monitor.RecordParameterUpdate($"concurrent-node-{i}", $"param-{i}");
                        monitor.RecordCacheHit();
                        monitor.RecordMemoryUsage(i * 1024);
                        
                        if (i % 10 == 0)
                        {
                            monitor.RecordEvaluationStart();
                            monitor.RecordEvaluationComplete(1, TimeSpan.FromMilliseconds(i));
                        }
                    }
                }));
            }

            Task.WaitAll(tasks.ToArray());

            // Assert - All operations should complete successfully
            var metrics = monitor.GetMetrics();
            metrics.ParameterUpdates.Should().BeGreaterThan(0);
            metrics.CacheHits.Should().BeGreaterThan(0);
            metrics.TotalEvaluations.Should().BeGreaterThan(0);
        }

        [Fact]
        public void PerformanceMonitor_PerformanceDataExport_SerializesCorrectly()
        {
            // Arrange
            var monitor = new PerformanceMonitor();
            
            // Add comprehensive data
            monitor.RecordParameterUpdate("node1", "param1");
            monitor.RecordEvaluationStart();
            monitor.RecordEvaluationComplete(2, TimeSpan.FromMilliseconds(100));
            monitor.RecordMemoryUsage(1024);
            monitor.RecordCacheHit();
            monitor.RecordCacheMiss();

            // Act
            var exportData = monitor.ExportPerformanceData();

            // Assert
            exportData.Should().NotBeNull();
            exportData.Should().Contain("TotalEvaluations");
            exportData.Should().Contain("ParameterUpdates");
            exportData.Should().Contain("CacheHitRate");
            exportData.Should().Contain("PeakMemoryUsageBytes");
        }

        [Fact]
        public void PerformanceMonitor_Disposal_CompletesSuccessfully()
        {
            // Arrange
            var monitor = new PerformanceMonitor();

            // Act & Assert
            var exception = Record.Exception(() => monitor.Dispose());
            exception.Should().BeNull();
        }
    }
}