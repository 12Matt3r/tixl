using TiXL.Tests.Fixtures;
using T3.Core.Operators.IncrementalEvaluation;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit.Abstractions;

namespace TiXL.Tests.Operators.IncrementalEvaluation
{
    /// <summary>
    /// Test fixture for IncrementalEvaluation tests providing common setup and utilities
    /// </summary>
    public class IncrementalEvaluationTestFixture : CoreTestFixture
    {
        public IncrementalEvaluationTestFixture(ITestOutputHelper output) : base(output)
        {
            Logger = ServiceProvider.GetRequiredService<ILogger<IncrementalEvaluationTestFixture>>();
        }

        public ILogger<IncrementalEvaluationTestFixture> Logger { get; }

        /// <summary>
        /// Creates a test graph with the specified number of nodes
        /// </summary>
        public IncrementalNodeGraph CreateTestGraph(int nodeCount = 10)
        {
            var mockContext = new Mock<IEvaluationContext>();
            var dependencyGraph = new DependencyGraph();
            var cacheManager = new CacheManager();
            var dirtyTracker = new DirtyTracker();
            var evaluator = new TopologicalEvaluator(mockContext.Object, cacheManager);
            var performanceMonitor = new PerformanceMonitor();

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
                graph.AddNode($"node-{i}", new { index = i });
            }

            return graph;
        }

        /// <summary>
        /// Creates a dependency graph with a simple linear chain
        /// </summary>
        public IncrementalNodeGraph CreateLinearChainGraph(int nodeCount)
        {
            var graph = CreateTestGraph(nodeCount);

            for (int i = 0; i < nodeCount - 1; i++)
            {
                graph.AddDependency($"node-{i}", $"node-{i + 1}");
            }

            return graph;
        }

        /// <summary>
        /// Creates a dependency graph with a balanced tree structure
        /// </summary>
        public IncrementalNodeGraph CreateTreeGraph(int nodeCount)
        {
            var graph = CreateTestGraph(nodeCount);

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

            return graph;
        }

        /// <summary>
        /// Creates a dependency graph with a complex interconnected structure
        /// </summary>
        public IncrementalNodeGraph CreateComplexGraph(int nodeCount)
        {
            var graph = CreateTestGraph(nodeCount);

            // Create multiple dependency patterns
            for (int i = 0; i < nodeCount - 1; i++)
            {
                // Linear dependencies
                graph.AddDependency($"node-{i}", $"node-{i + 1}");

                // Some nodes depend on multiple predecessors
                if (i > 0)
                {
                    graph.AddDependency($"node-{i - 1}", $"node-{i}");
                }

                // Some cross dependencies
                if (i % 3 == 0 && i + 2 < nodeCount)
                {
                    graph.AddDependency($"node-{i}", $"node-{i + 2}");
                }
            }

            return graph;
        }

        /// <summary>
        /// Creates a performance benchmark scenario with realistic workload
        /// </summary>
        public PerformanceBenchmark CreatePerformanceBenchmark(int nodeCount)
        {
            return new PerformanceBenchmark
            {
                NodeCount = nodeCount,
                TestName = $"Performance Test - {nodeCount} nodes",
                ExpectedImprovementThreshold = 0.70f, // 70% improvement expected
                MaxEvaluationTimeMs = nodeCount * 10, // Max 10ms per node
                MaxMemoryUsageMB = Math.Max(10, nodeCount / 100) // Scale memory with nodes
            };
        }

        /// <summary>
        /// Creates test data for various scenarios
        /// </summary>
        public TestDataSet CreateTestDataSet(int nodeCount)
        {
            return new TestDataSet
            {
                NodeIds = Enumerable.Range(0, nodeCount).Select(i => $"node-{i}").ToArray(),
                ParameterValues = Enumerable.Range(0, nodeCount).Select(i => new { value = i, name = $"param-{i}" }).ToArray(),
                TestNodes = Enumerable.Range(0, nodeCount).Select(i => new TestNode
                {
                    Id = $"node-{i}",
                    Parameters = new Dictionary<string, object>
                    {
                        { "value", i },
                        { "name", $"TestNode{i}" },
                        { "timestamp", DateTime.UtcNow }
                    }
                }).ToArray()
            };
        }
    }

    /// <summary>
    /// Performance benchmark configuration
    /// </summary>
    public class PerformanceBenchmark
    {
        public int NodeCount { get; set; }
        public string TestName { get; set; } = "";
        public float ExpectedImprovementThreshold { get; set; }
        public double MaxEvaluationTimeMs { get; set; }
        public int MaxMemoryUsageMB { get; set; }
    }

    /// <summary>
    /// Test data set for consistency across tests
    /// </summary>
    public class TestDataSet
    {
        public string[] NodeIds { get; set; } = Array.Empty<string>();
        public object[] ParameterValues { get; set; } = Array.Empty<object>();
        public TestNode[] TestNodes { get; set; } = Array.Empty<TestNode>();
    }

    /// <summary>
    /// Represents a test node with metadata
    /// </summary>
    public class TestNode
    {
        public string Id { get; set; } = "";
        public Dictionary<string, object> Parameters { get; set; } = new();
        public List<string> Dependencies { get; set; } = new();
        public List<string> Dependents { get; set; } = new();
    }

    /// <summary>
    /// Extension methods for common test operations
    /// </summary>
    public static class TestExtensions
    {
        /// <summary>
        /// Waits for the graph to reach a stable state
        /// </summary>
        public static async Task WaitForStability(this IncrementalNodeGraph graph, int maxAttempts = 10)
        {
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var result = await graph.EvaluateDirtyNodesAsync();
                if (graph.DirtyNodeCount == 0 && result.Success)
                {
                    return;
                }
                await Task.Delay(10); // Small delay between attempts
            }
        }

        /// <summary>
        /// Creates a complex dependency scenario
        /// </summary>
        public static void CreateComplexScenario(this IncrementalNodeGraph graph, int layerCount = 3)
        {
            var nodesPerLayer = 5;
            var totalNodes = layerCount * nodesPerLayer;

            // Create nodes
            for (int i = 0; i < totalNodes; i++)
            {
                graph.AddNode($"node-{i}", new { layer = i / nodesPerLayer, index = i % nodesPerLayer });
            }

            // Create layer-based dependencies
            for (int layer = 0; layer < layerCount - 1; layer++)
            {
                var currentLayerStart = layer * nodesPerLayer;
                var nextLayerStart = (layer + 1) * nodesPerLayer;

                for (int i = 0; i < nodesPerLayer; i++)
                {
                    var currentNode = $"node-{currentLayerStart + i}";
                    
                    // Each node depends on 2 nodes in next layer
                    graph.AddDependency(currentNode, $"node-{nextLayerStart + i}");
                    if (i + 1 < nodesPerLayer)
                    {
                        graph.AddDependency(currentNode, $"node-{nextLayerStart + i + 1}");
                    }
                }
            }
        }

        /// <summary>
        /// Verifies that the graph is in a consistent state
        /// </summary>
        public static void VerifyConsistency(this IncrementalNodeGraph graph)
        {
            // Check that dirty node count matches expectations
            var dirtyCount = graph.DirtyNodeCount;
            var nodeCount = graph.NodeCount;
            
            dirtyCount.Should().BeGreaterThanOrEqualTo(0);
            dirtyCount.Should().BeLessThanOrEqualTo(nodeCount);

            // Check that evaluation results are reasonable
            var metrics = graph.GetPerformanceMetrics();
            metrics.Should().NotBeNull();
            metrics.TotalEvaluations.Should().BeGreaterThanOrEqualTo(0);
        }
    }
}