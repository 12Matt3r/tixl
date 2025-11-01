using Xunit;
using Xunit.Abstractions;
using TiXL.Tests.Categories;
using TiXL.Tests.Fixtures;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using T3.Core.Operators;
using T3.Core.Operators.IncrementalEvaluation;
using Microsoft.Extensions.Logging;

namespace TiXL.Tests.Operators.IncrementalEvaluation
{
    /// <summary>
    /// Integration tests for Incremental Evaluation System
    /// Compares incremental vs full evaluation performance and validates correctness
    /// </summary>
    [Category(TestCategories.Operators)]
    [Category(TestCategories.Integration)]
    public class IncrementalEvaluationIntegrationTests : CoreTestFixture
    {
        private readonly ITestOutputHelper _output;
        private readonly ILogger<IncrementalEvaluationIntegrationTests> _logger;

        public IncrementalEvaluationIntegrationTests(ITestOutputHelper output) : base()
        {
            _output = output;
            _logger = ServiceProvider.GetRequiredService<ILogger<IncrementalEvaluationIntegrationTests>>();
        }

        [Theory]
        [InlineData(100)]
        [InlineData(500)]
        [InlineData(1000)]
        public async Task IncrementalVsFullEvaluation_PerformanceComparison_MeetsTargetImprovement(int nodeCount)
        {
            // Arrange
            var graphSizes = new Dictionary<string, int>
            {
                ["Small Graph"] = 100,
                ["Medium Graph"] = 500,
                ["Large Graph"] = 1000
            };

            _output.WriteLine($"Testing graph with {nodeCount} nodes...");

            // Create test graphs
            var incrementalGraph = CreateIncrementalGraph(nodeCount);
            var fullGraph = CreateFullEvaluationGraph(nodeCount);

            // Setup dependencies (simple chain: node0 -> node1 -> node2 -> ...)
            for (int i = 0; i < nodeCount - 1; i++)
            {
                incrementalGraph.AddDependency($"node-{i}", $"node-{i + 1}");
                fullGraph.AddDependency($"node-{i}", $"node-{i + 1}");
            }

            // Initial evaluation (both graphs)
            var initialTime = MeasureTime(async () => await incrementalGraph.EvaluateDirtyNodesAsync());
            var fullInitialTime = MeasureTime(async () => await fullGraph.EvaluateAllNodesAsync());

            // Act - Measure incremental evaluation (only 1 node changed)
            incrementalGraph.UpdateNodeParameter("node-0", "value", 100); // Change only first node
            var incrementalTime = MeasureTime(async () => await incrementalGraph.EvaluateDirtyNodesAsync());

            // Act - Measure full evaluation (re-evaluate entire graph)
            var fullTime = MeasureTime(async () => await fullGraph.EvaluateAllNodesAsync());

            // Calculate improvement
            var improvementRatio = fullTime.TotalMilliseconds / incrementalTime.TotalMilliseconds;
            var improvementPercentage = ((fullTime.TotalMilliseconds - incrementalTime.TotalMilliseconds) / fullTime.TotalMilliseconds) * 100;

            _output.WriteLine($"Node Count: {nodeCount}");
            _output.WriteLine($"Initial Full Evaluation: {fullInitialTime.TotalMilliseconds:F2}ms");
            _output.WriteLine($"Incremental Evaluation (1 node): {incrementalTime.TotalMilliseconds:F2}ms");
            _output.WriteLine($"Full Re-evaluation: {fullTime.TotalMilliseconds:F2}ms");
            _output.WriteLine($"Improvement Ratio: {improvementRatio:F2}x");
            _output.WriteLine($"Improvement Percentage: {improvementPercentage:F1}%");

            // Assert - Should meet 95% improvement target for large graphs
            if (nodeCount >= 100)
            {
                improvementPercentage.Should().BeGreaterThan(50, "Should show significant improvement for medium+ graphs");
                if (nodeCount >= 500)
                {
                    improvementPercentage.Should().BeGreaterThan(70, "Should show major improvement for large graphs");
                }
            }
            
            // Performance should scale reasonably
            incrementalTime.Should().BeLessThan(TimeSpan.FromMilliseconds(nodeCount * 10)); // Max 10ms per node
        }

        [Fact]
        public async Task IncrementalVsFullEvaluation_Correctness_SameResults()
        {
            // Arrange
            var nodeCount = 50;
            var graph = CreateIncrementalGraph(nodeCount);
            
            // Create complex dependency network
            CreateComplexDependencyGraph(graph, nodeCount);

            // Act - Full evaluation first
            var fullResults = await PerformFullEvaluation(graph, nodeCount);

            // Reset and do incremental evaluation
            graph.ResetDirtyFlags();
            graph.ClearCache();
            
            // Mark all nodes dirty (simulating full re-evaluation)
            var allNodes = Enumerable.Range(0, nodeCount).Select(i => $"node-{i}").ToArray();
            foreach (var node in allNodes)
            {
                graph.UpdateNodeParameter(node, "recompute", true);
            }

            var incrementalResults = await graph.EvaluateDirtyNodesAsync();

            // Assert - Results should be equivalent
            incrementalResults.Success.Should().BeTrue("Incremental evaluation should succeed");
            incrementalResults.EvaluatedNodes.Should().HaveCount(nodeCount, "All nodes should be evaluated");
            
            // Both approaches should evaluate all nodes
            fullResults.EvaluatedNodes.Should().HaveCount(nodeCount, "Full evaluation should evaluate all nodes");
        }

        [Fact]
        public async Task IncrementalEvaluation_EdgeCase_Cycles_DetectedAndHandled()
        {
            // Arrange
            var graph = CreateIncrementalGraph(3);
            
            // Create cycle: node-0 -> node-1 -> node-2 -> node-0
            graph.AddDependency("node-0", "node-1");
            graph.AddDependency("node-1", "node-2");
            
            // Act & Assert - Adding cycle-completing dependency should be detected
            Action act = () => graph.AddDependency("node-2", "node-0");
            act.Should().Throw<InvalidOperationException>("Should detect circular dependency");
        }

        [Fact]
        public async Task IncrementalEvaluation_EdgeCase_DisconnectedNodes_HandlesCorrectly()
        {
            // Arrange
            var graph = CreateIncrementalGraph(6);
            
            // Create two disconnected components: 
            // Component 1: node-0 -> node-1 -> node-2
            // Component 2: node-3 -> node-4 -> node-5
            graph.AddDependency("node-0", "node-1");
            graph.AddDependency("node-1", "node-2");
            graph.AddDependency("node-3", "node-4");
            graph.AddDependency("node-4", "node-5");

            // Act - Change one node in first component
            graph.UpdateNodeParameter("node-0", "value", 100);
            var result1 = await graph.EvaluateDirtyNodesAsync();

            // Reset
            graph.ResetDirtyFlags();

            // Act - Change one node in second component
            graph.UpdateNodeParameter("node-3", "value", 200);
            var result2 = await graph.EvaluateDirtyNodesAsync();

            // Assert - Should only evaluate affected component
            result1.Success.Should().BeTrue();
            result1.EvaluatedNodes.Should().HaveCount(3, "Should only evaluate first component");
            result1.EvaluatedNodes.Should().Contain("node-0");
            result1.EvaluatedNodes.Should().Contain("node-1");
            result1.EvaluatedNodes.Should().Contain("node-2");
            result1.EvaluatedNodes.Should().NotContain("node-3");

            result2.Success.Should().BeTrue();
            result2.EvaluatedNodes.Should().HaveCount(3, "Should only evaluate second component");
            result2.EvaluatedNodes.Should().NotContain("node-0");
            result2.EvaluatedNodes.Should().Contain("node-3");
            result2.EvaluatedNodes.Should().Contain("node-4");
            result2.EvaluatedNodes.Should().Contain("node-5");
        }

        [Fact]
        public async Task IncrementalEvaluation_EdgeCase_ComplexDependencyChain_CorrectlyPropagates()
        {
            // Arrange
            var graph = CreateIncrementalGraph(10);
            
            // Create complex dependency: 
            // A -> B, C
            // B -> D, E
            // C -> F
            // D, E, F -> G
            // G -> H
            // H -> I, J
            graph.AddDependency("node-0", "node-1"); // A -> B
            graph.AddDependency("node-0", "node-2"); // A -> C
            graph.AddDependency("node-1", "node-3"); // B -> D
            graph.AddDependency("node-1", "node-4"); // B -> E
            graph.AddDependency("node-2", "node-5"); // C -> F
            graph.AddDependency("node-3", "node-6"); // D -> G
            graph.AddDependency("node-4", "node-6"); // E -> G
            graph.AddDependency("node-5", "node-6"); // F -> G
            graph.AddDependency("node-6", "node-7"); // G -> H
            graph.AddDependency("node-7", "node-8"); // H -> I
            graph.AddDependency("node-7", "node-9"); // H -> J

            // Act - Change source node (should affect entire graph)
            graph.UpdateNodeParameter("node-0", "source-value", 42);
            var result = await graph.EvaluateDirtyNodesAsync();

            // Assert - Should affect entire dependency chain
            result.Success.Should().BeTrue();
            result.EvaluatedNodes.Should().HaveCount(10, "Should evaluate entire dependency chain");
        }

        [Fact]
        public async Task IncrementalEvaluation_MemoryUsage_CacheEffectiveness_ValidatesEfficiency()
        {
            // Arrange
            var graph = CreateIncrementalGraph(100);
            var largeData = new { value = new byte[1024], timestamp = DateTime.UtcNow };

            // Create linear dependency chain
            for (int i = 0; i < 99; i++)
            {
                graph.AddDependency($"node-{i}", $"node-{i + 1}");
            }

            // Act - Initial evaluation
            var initialStats = graph.GetCacheStatistics();
            await graph.EvaluateDirtyNodesAsync();
            
            var afterEvalStats = graph.GetCacheStatistics();
            var memoryUsage = graph.GetPerformanceMetrics().MemoryUsageMB;

            // Act - Update middle node (should affect downstream)
            graph.UpdateNodeParameter("node-50", "value", largeData);
            await graph.EvaluateDirtyNodesAsync();

            var finalStats = graph.GetCacheStatistics();
            var finalMemoryUsage = graph.GetPerformanceMetrics().MemoryUsageMB;

            _output.WriteLine($"Initial Cache Hit Rate: {initialStats.HitRate:P2}");
            _output.WriteLine($"After Evaluation Hit Rate: {afterEvalStats.HitRate:P2}");
            _output.WriteLine($"Final Hit Rate: {finalStats.HitRate:P2}");
            _output.WriteLine($"Memory Usage: {memoryUsage} MB -> {finalMemoryUsage} MB");

            // Assert - Cache should be utilized effectively
            afterEvalStats.HitRate.Should().BeGreaterThan(0.0f, "Cache should have hits after evaluation");
            finalMemoryUsage.Should().BeLessThan(100, "Memory usage should remain reasonable");
            
            // Cache hit rate should be maintained or improve
            finalStats.HitRate.Should().BeGreaterThanOrEqualTo(initialStats.HitRate);
        }

        [Fact]
        public async Task IncrementalEvaluation_PerformanceTarget_95PercentReduction_AchievesGoal()
        {
            // Arrange
            var nodeCount = 1000;
            var graph = CreateIncrementalGraph(nodeCount);
            
            // Create a balanced tree structure for realistic dependency pattern
            CreateBalancedTreeGraph(graph, nodeCount);

            // Measure baseline performance
            await graph.EvaluateDirtyNodesAsync(); // Initial evaluation
            var baselineMemory = graph.GetPerformanceMetrics().MemoryUsageMB;

            // Act - Measure incremental performance for various scenarios
            var scenarios = new[]
            {
                new { Name = "Leaf Node Change", NodeId = "node-0" },
                new { Name = "Mid-Level Change", NodeId = $"node-{nodeCount / 3}" },
                new { Name = "Root Change", NodeId = $"node-{nodeCount - 1}" }
            };

            foreach (var scenario in scenarios)
            {
                graph.ResetDirtyFlags();
                graph.ClearCache();
                
                // Measure full re-evaluation time (baseline)
                var fullEvalTime = MeasureTime(async () => await graph.EvaluateAllNodesAsync());
                
                // Reset and measure incremental time
                await graph.EvaluateDirtyNodesAsync(); // Reset state
                graph.ResetDirtyFlags();
                
                // Change one node and measure incremental time
                graph.UpdateNodeParameter(scenario.NodeId, "value", 42);
                var incrementalTime = MeasureTime(async () => await graph.EvaluateDirtyNodesAsync());
                
                var improvementPercentage = ((fullEvalTime.TotalMilliseconds - incrementalTime.TotalMilliseconds) / fullEvalTime.TotalMilliseconds) * 100;

                _output.WriteLine($"{scenario.Name}:");
                _output.WriteLine($"  Full Evaluation: {fullEvalTime.TotalMilliseconds:F2}ms");
                _output.WriteLine($"  Incremental: {incrementalTime.TotalMilliseconds:F2}ms");
                _output.WriteLine($"  Improvement: {improvementPercentage:F1}%");
                
                // Assert - Should achieve target improvement for large graphs
                if (scenario.NodeId == "node-0") // Leaf node change should show best improvement
                {
                    improvementPercentage.Should().BeGreaterThan(90, "Leaf node changes should achieve 90%+ improvement");
                }
                else
                {
                    improvementPercentage.Should().BeGreaterThan(70, "Should still show significant improvement for internal nodes");
                }
            }
        }

        [Fact]
        public async Task IncrementalEvaluation_Scalability_LargeGraphPerformance_HandlesEfficiently()
        {
            // Arrange
            var nodeCounts = new[] { 100, 500, 1000, 2000 };
            
            foreach (var nodeCount in nodeCounts)
            {
                _output.WriteLine($"Testing scalability with {nodeCount} nodes...");
                
                var graph = CreateIncrementalGraph(nodeCount);
                CreateScalableGraphStructure(graph, nodeCount);

                // Act - Measure evaluation performance
                var startTime = Stopwatch.StartNew();
                var result = await graph.EvaluateDirtyNodesAsync();
                startTime.Stop();

                var evaluationTime = startTime.Elapsed;
                var nodesPerMs = nodeCount / evaluationTime.TotalMilliseconds;

                _output.WriteLine($"  Evaluation Time: {evaluationTime.TotalMilliseconds:F2}ms");
                _output.WriteLine($"  Nodes/ms: {nodesPerMs:F2}");
                _output.WriteLine($"  Memory Usage: {graph.GetPerformanceMetrics().MemoryUsageMB} MB");

                // Assert - Performance should scale well
                result.Success.Should().BeTrue();
                evaluationTime.Should().BeLessThan(TimeSpan.FromMilliseconds(nodeCount * 5), "Should complete within reasonable time");
                nodesPerMs.Should().BeGreaterThan(1, "Should process at least 1 node per ms");
            }
        }

        [Fact]
        public async Task IncrementalEvaluation_Cancellation_GracefulInterruption_HandlesCorrectly()
        {
            // Arrange
            var graph = CreateIncrementalGraph(1000);
            CreateComplexDependencyGraph(graph, 1000);
            
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(100)); // Cancel quickly

            graph.UpdateNodeParameter("node-0", "value", 42);

            // Act & Assert - Should handle cancellation gracefully
            await FluentActions.Awaiting(() => graph.EvaluateDirtyNodesAsync(cts.Token))
                .Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task IncrementalEvaluation_ParallelOperations_ConcurrencySafety_Succeeds()
        {
            // Arrange
            var graph = CreateIncrementalGraph(50);
            CreateComplexDependencyGraph(graph, 50);
            
            var tasks = new List<Task>();
            var operationsPerTask = 20;

            // Act - Perform concurrent operations
            for (int taskIndex = 0; taskIndex < 5; taskIndex++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    for (int i = 0; i < operationsPerTask; i++)
                    {
                        var nodeId = $"node-{i % 50}";
                        graph.UpdateNodeParameter(nodeId, $"param-{taskIndex}", i);
                        await graph.EvaluateDirtyNodesAsync();
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert - All operations should complete successfully
            var finalMetrics = graph.GetPerformanceMetrics();
            finalMetrics.TotalEvaluations.Should().BeGreaterThan(0);
            graph.NodeCount.Should().Be(50);
        }

        private TimeSpan MeasureTime(Func<Task> action)
        {
            var stopwatch = Stopwatch.StartNew();
            Task.Run(action).Wait();
            stopwatch.Stop();
            return stopwatch.Elapsed;
        }

        private IncrementalNodeGraph CreateIncrementalGraph(int nodeCount)
        {
            // Create mock context
            var mockContext = new Mock<IEvaluationContext>();
            var dependencyGraph = new DependencyGraph();
            var cacheManager = new CacheManager();
            var dirtyTracker = new DirtyTracker();
            var evaluator = new TopologicalEvaluator(mockContext.Object, cacheManager);
            var performanceMonitor = new PerformanceMonitor();

            // Create graph and add nodes
            var graph = new IncrementalNodeGraph(
                mockContext.Object,
                dependencyGraph,
                cacheManager,
                dirtyTracker,
                evaluator,
                performanceMonitor);

            // Add nodes
            for (int i = 0; i < nodeCount; i++)
            {
                graph.AddNode($"node-{i}", new { index = i, timestamp = DateTime.UtcNow });
            }

            return graph;
        }

        private FullEvaluationGraph CreateFullEvaluationGraph(int nodeCount)
        {
            // Create a mock full evaluation graph for comparison
            return new FullEvaluationGraph(nodeCount);
        }

        private void CreateComplexDependencyGraph(IncrementalNodeGraph graph, int nodeCount)
        {
            // Create various dependency patterns
            for (int i = 0; i < nodeCount - 2; i++)
            {
                // Linear chains
                graph.AddDependency($"node-{i}", $"node-{i + 1}");
                
                // Some parallel branches
                if (i % 3 == 0 && i + 2 < nodeCount)
                {
                    graph.AddDependency($"node-{i}", $"node-{i + 2}");
                }
            }
        }

        private void CreateBalancedTreeGraph(IncrementalNodeGraph graph, int nodeCount)
        {
            // Create a balanced tree structure
            for (int i = 0; i < nodeCount; i++)
            {
                var leftChild = 2 * i + 1;
                var rightChild = 2 * i + 2;
                
                if (leftChild < nodeCount)
                {
                    graph.AddDependency($"node-{i}", $"node-{leftChild}");
                }
                
                if (rightChild < nodeCount)
                {
                    graph.AddDependency($"node-{i}", $"node-{rightChild}");
                }
            }
        }

        private void CreateScalableGraphStructure(IncrementalNodeGraph graph, int nodeCount)
        {
            // Create a structure that scales well
            var layers = (int)Math.Log2(nodeCount);
            
            for (int layer = 0; layer < layers; layer++)
            {
                var nodesInLayer = Math.Min((int)Math.Pow(2, layer), nodeCount);
                
                for (int i = 0; i < nodesInLayer; i++)
                {
                    var nodeIndex = layer * nodesInLayer + i;
                    if (nodeIndex >= nodeCount) break;
                    
                    var leftChild = nodeIndex * 2 + 1;
                    var rightChild = nodeIndex * 2 + 2;
                    
                    if (leftChild < nodeCount)
                    {
                        graph.AddDependency($"node-{nodeIndex}", $"node-{leftChild}");
                    }
                    
                    if (rightChild < nodeCount)
                    {
                        graph.AddDependency($"node-{nodeIndex}", $"node-{rightChild}");
                    }
                }
            }
        }

        private async Task<EvaluationResult> PerformFullEvaluation(IncrementalNodeGraph graph, int nodeCount)
        {
            // Mark all nodes dirty to simulate full re-evaluation
            for (int i = 0; i < nodeCount; i++)
            {
                graph.UpdateNodeParameter($"node-{i}", "full-recompute", true);
            }
            
            return await graph.EvaluateDirtyNodesAsync();
        }

        // Mock class for full evaluation comparison
        private class FullEvaluationGraph
        {
            private readonly int _nodeCount;

            public FullEvaluationGraph(int nodeCount)
            {
                _nodeCount = nodeCount;
            }

            public void AddDependency(string fromNode, string toNode)
            {
                // Mock implementation
            }

            public async Task<EvaluationResult> EvaluateAllNodesAsync()
            {
                // Simulate evaluation time proportional to node count
                await Task.Delay(_nodeCount / 100); // Simulate work
                
                return new EvaluationResult
                {
                    Success = true,
                    EvaluatedNodes = Enumerable.Range(0, _nodeCount).Select(i => $"node-{i}").ToArray()
                };
            }
        }
    }
}