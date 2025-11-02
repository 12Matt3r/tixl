using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using TiXL.Core.Graphics.DirectX12;
using TiXL.Core.NodeGraph;
using TiXL.Core.Operators;
using TiXL.Core.Performance;
using TiXL.Tests.Categories;
using TiXL.Tests.Fixtures;
using TiXL.Tests.Data;

namespace TiXL.Tests.Integration
{
    /// <summary>
    /// Incremental node evaluation integration tests with DirectX resource management
    /// Tests the complete incremental evaluation system integration
    /// </summary>
    [Category(TestCategories.Integration)]
    [Category(TestCategories.Operators)]
    [Category(TestCategories.NodeGraph)]
    public class IncrementalNodeEvaluationIntegrationTests : CoreTestFixture
    {
        private readonly ITestOutputHelper _output;
        private readonly List<EvaluationTestResult> _testResults;

        public IncrementalNodeEvaluationIntegrationTests(ITestOutputHelper output) : base()
        {
            _output = output;
            _testResults = new List<EvaluationTestResult>();
        }

        [Fact]
        public async Task IncrementalEvaluation_DirectXResourceBinding()
        {
            _output.WriteLine("Testing incremental node evaluation with DirectX resource binding");

            var device = CreateMockDirectXDevice();
            var commandQueue = CreateMockCommandQueue();
            var performanceMonitor = new PerformanceMonitor();
            
            using var engine = new DirectX12RenderingEngine(device, commandQueue, performanceMonitor);
            await engine.InitializeAsync();
            
            var incrementalEngine = new IncrementalNodeGraph(performanceMonitor);
            var evaluator = new IncrementalNodeEvaluator(performanceMonitor);
            var resourceManager = new DirectXResourceManager(device, performanceMonitor);

            try
            {
                // Create complex node graph
                var nodeGraph = TestDataGenerator.GenerateComplexNodeGraph();
                var nodeCount = nodeGraph.Nodes.Count;
                var evaluationFrames = 50;

                var resourceBindings = new List<ResourceBindingResult>();
                var evaluationMetrics = new List<EvaluationMetric>();
                var cacheHitRates = new List<double>();

                // Act - Execute incremental evaluation with resource binding
                for (int frame = 0; frame < evaluationFrames; frame++)
                {
                    var frameStart = Stopwatch.GetTimestamp();

                    // Modify some nodes to trigger incremental evaluation
                    if (frame % 10 == 0)
                    {
                        // Mark some nodes as dirty
                        var dirtyNodes = nodeGraph.Nodes.Take(5).ToList();
                        foreach (var node in dirtyNodes)
                        {
                            incrementalEngine.MarkNodeDirty(node.Id);
                        }
                    }

                    // Execute incremental evaluation
                    var evalStart = Stopwatch.GetTimestamp();
                    var evaluationResult = await evaluator.EvaluateIncrementalAsync(nodeGraph);
                    var evalEnd = Stopwatch.GetTimestamp();
                    var evalTime = (evalEnd - evalStart) / (double)Stopwatch.Frequency * 1000;

                    // Bind resources for evaluated nodes
                    var bindingTasks = evaluationResult.EvaluatedNodes.Select(async evaluatedNode =>
                    {
                        var bindingResult = await resourceManager.BindResourcesAsync(evaluatedNode.Node, evaluatedNode.OutputData);
                        return new ResourceBindingResult
                        {
                            NodeId = evaluatedNode.Node.Id,
                            Success = bindingResult.IsSuccess,
                            BindingTime = bindingResult.BindingTimeMs,
                            ResourceCount = bindingResult.ResourceCount
                        };
                    });

                    var frameBindings = await Task.WhenAll(bindingTasks);
                    resourceBindings.AddRange(frameBindings);

                    // Record metrics
                    evaluationMetrics.Add(new EvaluationMetric
                    {
                        FrameNumber = frame,
                        EvaluationTimeMs = evalTime,
                        NodesEvaluated = evaluationResult.EvaluatedNodes.Count,
                        NodesCached = evaluationResult.CachedNodes.Count,
                        CacheHitRate = evaluationResult.CacheHitRate
                    });

                    // Track cache performance
                    cacheHitRates.Add(evaluationResult.CacheHitRate);

                    var frameEnd = Stopwatch.GetTimestamp();
                    var frameTime = (frameEnd - frameStart) / (double)Stopwatch.Frequency * 1000;

                    // Maintain frame rate
                    await Task.Delay(Math.Max(0, (int)(16.67 - frameTime)));
                }

                // Assert - Validate incremental evaluation performance
                var avgEvalTime = evaluationMetrics.Average(m => m.EvaluationTimeMs);
                var avgNodesEvaluated = evaluationMetrics.Average(m => m.NodesEvaluated);
                var avgCacheHitRate = cacheHitRates.Average();
                var avgBindingTime = resourceBindings.Where(b => b.Success).Average(b => b.BindingTime);

                avgEvalTime.Should().BeLessThan(5.0, "Incremental evaluation should be efficient");
                avgCacheHitRate.Should().BeGreaterThan(0.6, "Cache hit rate should be good for incremental updates");
                avgBindingTime.Should().BeLessThan(2.0, "Resource binding should be fast");

                var successfulBindings = resourceBindings.Count(b => b.Success);
                successfulBindings.Should().BeGreaterThan(resourceBindings.Count * 0.9, "Most resource bindings should succeed");

                _output.WriteLine($"Incremental Evaluation with Resource Binding Results:");
                _output.WriteLine($"  Average Evaluation Time: {avgEvalTime:F2}ms");
                _output.WriteLine($"  Average Nodes Evaluated: {avgNodesEvaluated:F1}");
                _output.WriteLine($"  Average Cache Hit Rate: {avgCacheHitRate:P2}");
                _output.WriteLine($"  Average Resource Binding Time: {avgBindingTime:F2}ms");
                _output.WriteLine($"  Successful Bindings: {successfulBindings}/{resourceBindings.Count}");
                _output.WriteLine($"  Total Evaluations: {evaluationMetrics.Count}");

                _testResults.Add(new EvaluationTestResult
                {
                    TestName = "IncrementalEvaluation_DirectXResourceBinding",
                    Passed = avgEvalTime < 5.0 && avgCacheHitRate > 0.6,
                    DurationMs = evaluationFrames * 16.67,
                    Metrics = new Dictionary<string, double>
                    {
                        { "AvgEvalTime", avgEvalTime },
                        { "AvgNodesEvaluated", avgNodesEvaluated },
                        { "AvgCacheHitRate", avgCacheHitRate },
                        { "AvgBindingTime", avgBindingTime },
                        { "SuccessfulBindings", successfulBindings },
                        { "TotalBindings", resourceBindings.Count }
                    }
                });
            }
            finally
            {
                engine.Dispose();
                incrementalEngine.Dispose();
                evaluator.Dispose();
                resourceManager.Dispose();
            }
        }

        [Fact]
        public async Task IncrementalEvaluation_NodeDependencyTracking()
        {
            _output.WriteLine("Testing incremental evaluation with node dependency tracking");

            var performanceMonitor = new PerformanceMonitor();
            var incrementalEngine = new IncrementalNodeGraph(performanceMonitor);
            var evaluator = new IncrementalNodeEvaluator(performanceMonitor);

            try
            {
                // Create node graph with complex dependencies
                var nodeGraph = TestDataGenerator.GenerateTestNodeGraphWithDependencies();
                var dependencyTracker = new NodeDependencyTracker();

                var dependencyMetrics = new List<DependencyMetric>();
                var updatePropagationTests = 30;

                // Act - Test dependency tracking and update propagation
                for (int test = 0; test < updatePropagationTests; test++)
                {
                    var testStart = Stopwatch.GetTimestamp();

                    // Select a random node to modify
                    var targetNode = nodeGraph.Nodes[new Random().Next(nodeGraph.Nodes.Count)];
                    var affectedNodes = dependencyTracker.GetAffectedNodes(targetNode.Id, nodeGraph);

                    // Mark target node as dirty
                    incrementalEngine.MarkNodeDirty(targetNode.Id);

                    // Execute incremental evaluation
                    var evalResult = await evaluator.EvaluateIncrementalAsync(nodeGraph);

                    var testEnd = Stopwatch.GetTimestamp();
                    var testTime = (testEnd - testStart) / (double)Stopwatch.Frequency * 1000;

                    // Validate dependency tracking
                    var expectedAffectedNodes = dependencyTracker.GetExpectedAffectedNodes(targetNode.Id);
                    var actualAffectedNodes = evalResult.EvaluatedNodes.Select(n => n.Node.Id).ToList();

                    var dependencyAccuracy = CalculateDependencyAccuracy(expectedAffectedNodes, actualAffectedNodes);

                    dependencyMetrics.Add(new DependencyMetric
                    {
                        TestNumber = test,
                        TargetNodeId = targetNode.Id,
                        ExpectedAffectedNodes = expectedAffectedNodes.Count,
                        ActualAffectedNodes = actualAffectedNodes.Count,
                        DependencyAccuracy = dependencyAccuracy,
                        TestTimeMs = testTime
                    });

                    // Update dependency tracker with actual results
                    dependencyTracker.UpdateDependencies(targetNode.Id, actualAffectedNodes);
                }

                // Assert - Validate dependency tracking accuracy
                var avgDependencyAccuracy = dependencyMetrics.Average(m => m.DependencyAccuracy);
                var avgTestTime = dependencyMetrics.Average(m => m.TestTimeMs);
                var accuracyConsistency = CalculateDependencyConsistency(dependencyMetrics);

                avgDependencyAccuracy.Should().BeGreaterThan(0.8, "Dependency tracking should be accurate");
                avgTestTime.Should().BeLessThan(10.0, "Dependency analysis should be efficient");
                accuracyConsistency.Should().BeGreaterThan(0.7, "Dependency tracking should be consistent");

                var highAccuracyTests = dependencyMetrics.Count(m => m.DependencyAccuracy > 0.8);
                highAccuracyTests.Should().BeGreaterThan(updatePropagationTests * 0.7, "Most dependency tracking should be highly accurate");

                _output.WriteLine($"Node Dependency Tracking Results:");
                _output.WriteLine($"  Average Dependency Accuracy: {avgDependencyAccuracy:P2}");
                _output.WriteLine($"  Average Test Time: {avgTestTime:F2}ms");
                _output.WriteLine($"  Accuracy Consistency: {accuracyConsistency:P2}");
                _output.WriteLine($"  High Accuracy Tests: {highAccuracyTests}/{updatePropagationTests}");

                _testResults.Add(new EvaluationTestResult
                {
                    TestName = "IncrementalEvaluation_NodeDependencyTracking",
                    Passed = avgDependencyAccuracy > 0.8 && avgTestTime < 10.0,
                    DurationMs = updatePropagationTests * 10.0,
                    Metrics = new Dictionary<string, double>
                    {
                        { "AvgDependencyAccuracy", avgDependencyAccuracy },
                        { "AvgTestTime", avgTestTime },
                        { "AccuracyConsistency", accuracyConsistency },
                        { "HighAccuracyTests", highAccuracyTests },
                        { "TotalTests", updatePropagationTests }
                    }
                });
            }
            finally
            {
                incrementalEngine.Dispose();
                evaluator.Dispose();
            }
        }

        [Fact]
        public async Task IncrementalEvaluation_CacheManagement()
        {
            _output.WriteLine("Testing incremental evaluation with cache management");

            var performanceMonitor = new PerformanceMonitor();
            var incrementalEngine = new IncrementalNodeGraph(performanceMonitor);
            var evaluator = new IncrementalNodeEvaluator(performanceMonitor);
            var cacheManager = new CacheManager(performanceMonitor);

            try
            {
                var nodeGraph = TestDataGenerator.GenerateTestNodeGraph();
                var cacheMetrics = new List<CacheMetric>();
                var evaluationCount = 100;

                // Act - Test cache management over many evaluations
                for (int eval = 0; eval < evaluationCount; eval++)
                {
                    // Periodically modify nodes to test cache invalidation
                    if (eval % 20 == 0)
                    {
                        var nodesToModify = nodeGraph.Nodes.Take(3).ToList();
                        foreach (var node in nodesToModify)
                        {
                            incrementalEngine.MarkNodeDirty(node.Id);
                        }
                    }

                    var evalStart = Stopwatch.GetTimestamp();
                    var evalResult = await evaluator.EvaluateIncrementalAsync(nodeGraph);
                    var evalEnd = Stopwatch.GetTimestamp();
                    var evalTime = (evalEnd - evalStart) / (double)Stopwatch.Frequency * 1000;

                    // Analyze cache performance
                    var cacheStats = cacheManager.GetCacheStatistics();
                    var cacheHitRate = evalResult.CacheHitRate;
                    var cacheSize = cacheStats.CacheSize;
                    var cacheEfficiency = cacheManager.CalculateCacheEfficiency();

                    cacheMetrics.Add(new CacheMetric
                    {
                        EvaluationNumber = eval,
                        CacheHitRate = cacheHitRate,
                        CacheSize = cacheSize,
                        CacheEfficiency = cacheEfficiency,
                        EvaluationTimeMs = evalTime,
                        NodesEvaluated = evalResult.EvaluatedNodes.Count
                    });

                    // Periodic cache maintenance
                    if (eval % 25 == 0)
                    {
                        cacheManager.PerformMaintenance();
                    }
                }

                // Assert - Validate cache management performance
                var avgCacheHitRate = cacheMetrics.Average(m => m.CacheHitRate);
                var avgCacheSize = cacheMetrics.Average(m => m.CacheSize);
                var avgCacheEfficiency = cacheMetrics.Average(m => m.CacheEfficiency);
                var avgEvalTime = cacheMetrics.Average(m => m.EvaluationTimeMs);

                var performanceGain = CalculateCachePerformanceGain(cacheMetrics);

                avgCacheHitRate.Should().BeGreaterThan(0.5, "Cache should provide good hit rate");
                avgCacheEfficiency.Should().BeGreaterThan(0.7, "Cache should be efficient");
                avgEvalTime.Should().BeLessThan(5.0, "Cached evaluations should be fast");
                performanceGain.Should().BeGreaterThan(1.5, "Cache should provide significant performance gain");

                var highHitRateEvaluations = cacheMetrics.Count(m => m.CacheHitRate > 0.7);
                highHitRateEvaluations.Should().BeGreaterThan(evaluationCount * 0.6, "Most evaluations should have good cache hit rate");

                _output.WriteLine($"Cache Management Results:");
                _output.WriteLine($"  Average Cache Hit Rate: {avgCacheHitRate:P2}");
                _output.WriteLine($"  Average Cache Size: {avgCacheSize:F0} entries");
                _output.WriteLine($"  Average Cache Efficiency: {avgCacheEfficiency:P2}");
                _output.WriteLine($"  Average Evaluation Time: {avgEvalTime:F2}ms");
                _output.WriteLine($"  Performance Gain: {performanceGain:F1}x");
                _output.WriteLine($"  High Hit Rate Evaluations: {highHitRateEvaluations}/{evaluationCount}");

                _testResults.Add(new EvaluationTestResult
                {
                    TestName = "IncrementalEvaluation_CacheManagement",
                    Passed = avgCacheHitRate > 0.5 && performanceGain > 1.5,
                    DurationMs = evaluationCount * 5.0,
                    Metrics = new Dictionary<string, double>
                    {
                        { "AvgCacheHitRate", avgCacheHitRate },
                        { "AvgCacheSize", avgCacheSize },
                        { "AvgCacheEfficiency", avgCacheEfficiency },
                        { "AvgEvalTime", avgEvalTime },
                        { "PerformanceGain", performanceGain },
                        { "HighHitRateEvaluations", highHitRateEvaluations }
                    }
                });
            }
            finally
            {
                incrementalEngine.Dispose();
                evaluator.Dispose();
                cacheManager.Dispose();
            }
        }

        [Fact]
        public async Task IncrementalEvaluation_TopologicalSorting()
        {
            _output.WriteLine("Testing incremental evaluation with topological sorting");

            var performanceMonitor = new PerformanceMonitor();
            var incrementalEngine = new IncrementalNodeGraph(performanceMonitor);
            var evaluator = new IncrementalNodeEvaluator(performanceMonitor);
            var topologicalEvaluator = new TopologicalEvaluator();

            try
            {
                var nodeGraph = TestDataGenerator.GenerateComplexNodeGraphWithLoops();
                var topologicalTests = 50;
                var sortMetrics = new List<TopologicalMetric>();

                // Act - Test topological sorting for various node modifications
                for (int test = 0; test < topologicalTests; test++)
                {
                    var testStart = Stopwatch.GetTimestamp();

                    // Modify random nodes
                    var nodesToModify = nodeGraph.Nodes.Skip(test % nodeGraph.Nodes.Count).Take(3).ToList();
                    foreach (var node in nodesToModify)
                    {
                        incrementalEngine.MarkNodeDirty(node.Id);
                    }

                    // Get topological order for dirty nodes
                    var dirtyNodeIds = nodesToModify.Select(n => n.Id).ToList();
                    var topologicalOrder = topologicalEvaluator.GetTopologicalOrder(nodeGraph, dirtyNodeIds);

                    // Validate topological order
                    var orderValid = topologicalEvaluator.ValidateTopologicalOrder(nodeGraph, topologicalOrder);

                    // Execute incremental evaluation
                    var evalResult = await evaluator.EvaluateIncrementalAsync(nodeGraph);

                    var testEnd = Stopwatch.GetTimestamp();
                    var testTime = (testEnd - testStart) / (double)Stopwatch.Frequency * 1000;

                    sortMetrics.Add(new TopologicalMetric
                    {
                        TestNumber = test,
                        TopologicalOrderValid = orderValid,
                        OrderLength = topologicalOrder.Count,
                        EvaluationTimeMs = testTime,
                        NodesProcessed = evalResult.EvaluatedNodes.Count
                    });
                }

                // Assert - Validate topological sorting
                var validOrders = sortMetrics.Count(m => m.TopologicalOrderValid);
                var validityRate = validOrders / (double)topologicalTests;
                var avgOrderLength = sortMetrics.Average(m => m.OrderLength);
                var avgSortTime = sortMetrics.Average(m => m.EvaluationTimeMs);

                validityRate.Should().BeGreaterThan(0.95, "Topological orders should almost always be valid");
                avgSortTime.Should().BeLessThan(3.0, "Topological sorting should be fast");

                var processingEfficiency = CalculateTopologicalProcessingEfficiency(sortMetrics);

                _output.WriteLine($"Topological Sorting Results:");
                _output.WriteLine($"  Validity Rate: {validityRate:P2}");
                _output.WriteLine($"  Average Order Length: {avgOrderLength:F1}");
                _output.WriteLine($"  Average Processing Time: {avgSortTime:F2}ms");
                _output.WriteLine($"  Processing Efficiency: {processingEfficiency:P2}");
                _output.WriteLine($"  Valid Orders: {validOrders}/{topologicalTests}");

                _testResults.Add(new EvaluationTestResult
                {
                    TestName = "IncrementalEvaluation_TopologicalSorting",
                    Passed = validityRate > 0.95 && avgSortTime < 3.0,
                    DurationMs = topologicalTests * 3.0,
                    Metrics = new Dictionary<string, double>
                    {
                        { "ValidityRate", validityRate },
                        { "AvgOrderLength", avgOrderLength },
                        { "AvgSortTime", avgSortTime },
                        { "ProcessingEfficiency", processingEfficiency },
                        { "ValidOrders", validOrders },
                        { "TotalTests", topologicalTests }
                    }
                });
            }
            finally
            {
                incrementalEngine.Dispose();
                evaluator.Dispose();
                topologicalEvaluator.Dispose();
            }
        }

        [Fact]
        public async Task IncrementalEvaluation_LargeGraph_Scaling()
        {
            _output.WriteLine("Testing incremental evaluation scaling with large node graphs");

            var performanceMonitor = new PerformanceMonitor();
            var incrementalEngine = new IncrementalNodeGraph(performanceMonitor);
            var evaluator = new IncrementalNodeEvaluator(performanceMonitor);

            try
            {
                var graphSizes = new[] { 100, 500, 1000, 2000 };
                var scalingMetrics = new List<ScalingMetric>();

                foreach (var graphSize in graphSizes)
                {
                    _output.WriteLine($"Testing with graph size: {graphSize} nodes");

                    // Create large node graph
                    var largeGraph = TestDataGenerator.GenerateLargeNodeGraph(graphSize);
                    var evaluationFrames = 20;

                    var frameMetrics = new List<FrameMetric>();

                    for (int frame = 0; frame < evaluationFrames; frame++)
                    {
                        var frameStart = Stopwatch.GetTimestamp();

                        // Modify a small percentage of nodes to simulate realistic updates
                        var nodesToModify = largeGraph.Nodes.Take(Math.Max(1, graphSize / 100)).ToList();
                        foreach (var node in nodesToModify)
                        {
                            incrementalEngine.MarkNodeDirty(node.Id);
                        }

                        var evalStart = Stopwatch.GetTimestamp();
                        var evalResult = await evaluator.EvaluateIncrementalAsync(largeGraph);
                        var evalEnd = Stopwatch.GetTimestamp();
                        var evalTime = (evalEnd - evalStart) / (double)Stopwatch.Frequency * 1000;

                        var frameEnd = Stopwatch.GetTimestamp();
                        var frameTime = (frameEnd - frameStart) / (double)Stopwatch.Frequency * 1000;

                        frameMetrics.Add(new FrameMetric
                        {
                            GraphSize = graphSize,
                            FrameNumber = frame,
                            EvaluationTimeMs = evalTime,
                            NodesEvaluated = evalResult.EvaluatedNodes.Count,
                            CacheHitRate = evalResult.CacheHitRate
                        });

                        await Task.Delay(Math.Max(0, (int)(16.67 - frameTime)));
                    }

                    // Aggregate metrics for this graph size
                    var avgEvalTime = frameMetrics.Average(m => m.EvaluationTimeMs);
                    var avgNodesEvaluated = frameMetrics.Average(m => m.NodesEvaluated);
                    var avgCacheHitRate = frameMetrics.Average(m => m.CacheHitRate);
                    var scalabilityScore = CalculateScalabilityScore(frameMetrics);

                    scalingMetrics.Add(new ScalingMetric
                    {
                        GraphSize = graphSize,
                        AvgEvaluationTimeMs = avgEvalTime,
                        AvgNodesEvaluated = avgNodesEvaluated,
                        AvgCacheHitRate = avgCacheHitRate,
                        ScalabilityScore = scalabilityScore
                    });

                    _output.WriteLine($"  Average Evaluation Time: {avgEvalTime:F2}ms");
                    _output.WriteLine($"  Average Nodes Evaluated: {avgNodesEvaluated:F1}");
                    _output.WriteLine($"  Average Cache Hit Rate: {avgCacheHitRate:P2}");
                    _output.WriteLine($"  Scalability Score: {scalabilityScore:P2}");
                }

                // Assert - Validate scaling performance
                var scalabilityTrend = CalculateScalabilityTrend(scalingMetrics);
                var maxEvalTime = scalingMetrics.Max(m => m.AvgEvaluationTimeMs);

                scalabilityTrend.Should().BeGreaterThan(0.5, "System should scale reasonably well");
                maxEvalTime.Should().BeLessThan(20.0, "Even large graphs should evaluate within reasonable time");

                var smallGraphMetrics = scalingMetrics.First(m => m.GraphSize == 100);
                var largeGraphMetrics = scalingMetrics.Last(m => m.GraphSize == 2000);
                var scalingEfficiency = smallGraphMetrics.AvgEvaluationTimeMs / largeGraphMetrics.AvgEvaluationTimeMs;

                scalingEfficiency.Should().BeGreaterThan(0.1, "Large graphs should not be orders of magnitude slower");

                _output.WriteLine($"Large Graph Scaling Results:");
                _output.WriteLine($"  Scalability Trend: {scalabilityTrend:P2}");
                _output.WriteLine($"  Maximum Evaluation Time: {maxEvalTime:F2}ms");
                _output.WriteLine($"  Scaling Efficiency: {scalingEfficiency:F2}");
                
                _output.WriteLine($"  Scaling Breakdown:");
                foreach (var metric in scalingMetrics)
                {
                    _output.WriteLine($"    {metric.GraphSize} nodes: {metric.AvgEvaluationTimeMs:F2}ms eval, {metric.ScalabilityScore:P2} score");
                }

                _testResults.Add(new EvaluationTestResult
                {
                    TestName = "LargeGraph_Scaling",
                    Passed = scalabilityTrend > 0.5 && maxEvalTime < 20.0,
                    DurationMs = graphSizes.Sum() * 20 * 16.67 / 100, // Approximate total test time
                    Metrics = scalingMetrics.ToDictionary(m => $"GraphSize_{m.GraphSize}", m => m.ScalabilityScore)
                });
            }
            finally
            {
                incrementalEngine.Dispose();
                evaluator.Dispose();
            }
        }

        // Helper methods

        private static double CalculateDependencyAccuracy(List<string> expected, List<string> actual)
        {
            if (!expected.Any() && !actual.Any()) return 1.0;
            
            var commonNodes = expected.Intersect(actual).Count();
            var totalExpected = expected.Count;
            var totalActual = actual.Count;
            
            var precision = totalActual > 0 ? commonNodes / (double)totalActual : 1.0;
            var recall = totalExpected > 0 ? commonNodes / (double)totalExpected : 1.0;
            
            return totalExpected + totalActual > 0 ? (precision + recall) / 2.0 : 0.0;
        }

        private static double CalculateDependencyConsistency(List<DependencyMetric> metrics)
        {
            if (metrics.Count < 2) return 1.0;
            
            var accuracies = metrics.Select(m => m.DependencyAccuracy).ToList();
            var variance = CalculateVariance(accuracies);
            
            return 1.0 / (1.0 + variance);
        }

        private static double CalculateCachePerformanceGain(List<CacheMetric> metrics)
        {
            var cachedEvals = metrics.Where(m => m.CacheHitRate > 0.5).ToList();
            var uncachedEvals = metrics.Where(m => m.CacheHitRate <= 0.5).ToList();
            
            if (!cachedEvals.Any() || !uncachedEvals.Any()) return 1.0;
            
            var avgCachedTime = cachedEvals.Average(m => m.EvaluationTimeMs);
            var avgUncachedTime = uncachedEvals.Average(m => m.EvaluationTimeMs);
            
            return avgUncachedTime / Math.Max(avgCachedTime, 0.001);
        }

        private static double CalculateVariance(List<double> values)
        {
            if (values.Count < 2) return 0.0;
            var mean = values.Average();
            return values.Sum(x => (x - mean) * (x - mean)) / values.Count;
        }

        private static double CalculateTopologicalProcessingEfficiency(List<TopologicalMetric> metrics)
        {
            var processingTimes = metrics.Select(m => m.EvaluationTimeMs).ToList();
            var nodeCounts = metrics.Select(m => m.NodesProcessed).ToList();
            
            if (!processingTimes.Any() || !nodeCounts.Any()) return 0.0;
            
            var avgTime = processingTimes.Average();
            var avgNodes = nodeCounts.Average();
            
            // Efficiency score based on nodes processed per millisecond
            var processingRate = avgNodes / Math.Max(avgTime, 0.001);
            return Math.Min(1.0, processingRate / 100.0); // Normalize to 0-1 scale
        }

        private static double CalculateScalabilityScore(List<FrameMetric> metrics)
        {
            var evalTimes = metrics.Select(m => m.EvaluationTimeMs).ToList();
            var cacheHitRates = metrics.Select(m => m.CacheHitRate).ToList();
            
            var timeScore = 1.0 / (1.0 + evalTimes.Average() / 10.0); // Normalize evaluation time
            var cacheScore = cacheHitRates.Average(); // Cache hit rate is already 0-1
            
            return (timeScore + cacheScore) / 2.0;
        }

        private static double CalculateScalabilityTrend(List<ScalingMetric> metrics)
        {
            if (metrics.Count < 2) return 1.0;
            
            // Calculate how well performance scales with graph size
            var sizes = metrics.Select(m => m.GraphSize).ToList();
            var times = metrics.Select(m => m.AvgEvaluationTimeMs).ToList();
            
            // Linear regression to find trend
            var n = sizes.Count;
            var sumX = sizes.Sum();
            var sumY = times.Sum();
            var sumXY = sizes.Zip(times, (x, y) => x * y).Sum();
            var sumXX = sizes.Sum(x => x * x);
            
            var slope = (n * sumXY - sumX * sumY) / (n * sumXX - sumX * sumX);
            
            // Score based on how gradual the slope is
            return 1.0 / (1.0 + Math.Abs(slope) / 100.0);
        }

        // Mock DirectX objects
        private ID3D12Device4 CreateMockDirectXDevice() => new MockD3D12Device();
        private ID3D12CommandQueue CreateMockCommandQueue() => new MockD3D12CommandQueue();

        #region Mock Classes

        private class MockD3D12Device : ID3D12Device4
        {
            public void Dispose() { }
        }

        private class MockD3D12CommandQueue : ID3D12CommandQueue
        {
            public void Dispose() { }
        }

        #endregion

        // Data classes
        private class EvaluationTestResult
        {
            public string TestName { get; set; }
            public bool Passed { get; set; }
            public double DurationMs { get; set; }
            public Dictionary<string, double> Metrics { get; set; } = new();
        }

        private class ResourceBindingResult
        {
            public string NodeId { get; set; }
            public bool Success { get; set; }
            public double BindingTime { get; set; }
            public int ResourceCount { get; set; }
        }

        private class EvaluationMetric
        {
            public int FrameNumber { get; set; }
            public double EvaluationTimeMs { get; set; }
            public int NodesEvaluated { get; set; }
            public int NodesCached { get; set; }
            public double CacheHitRate { get; set; }
        }

        private class DependencyMetric
        {
            public int TestNumber { get; set; }
            public string TargetNodeId { get; set; }
            public int ExpectedAffectedNodes { get; set; }
            public int ActualAffectedNodes { get; set; }
            public double DependencyAccuracy { get; set; }
            public double TestTimeMs { get; set; }
        }

        private class CacheMetric
        {
            public int EvaluationNumber { get; set; }
            public double CacheHitRate { get; set; }
            public int CacheSize { get; set; }
            public double CacheEfficiency { get; set; }
            public double EvaluationTimeMs { get; set; }
            public int NodesEvaluated { get; set; }
        }

        private class TopologicalMetric
        {
            public int TestNumber { get; set; }
            public bool TopologicalOrderValid { get; set; }
            public int OrderLength { get; set; }
            public double EvaluationTimeMs { get; set; }
            public int NodesProcessed { get; set; }
        }

        private class ScalingMetric
        {
            public int GraphSize { get; set; }
            public double AvgEvaluationTimeMs { get; set; }
            public double AvgNodesEvaluated { get; set; }
            public double AvgCacheHitRate { get; set; }
            public double ScalabilityScore { get; set; }
        }

        private class FrameMetric
        {
            public int GraphSize { get; set; }
            public int FrameNumber { get; set; }
            public double EvaluationTimeMs { get; set; }
            public int NodesEvaluated { get; set; }
            public double CacheHitRate { get; set; }
        }
    }
}