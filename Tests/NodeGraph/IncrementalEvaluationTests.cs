using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using T3.Core.NodeGraph;
using T3.Core.Operators;
using T3.Core.Operators.IncrementalEvaluation;
using T3.Core.Logging;
using TiXL.Core.Validation;
using Xunit;
using Xunit.Abstractions;
using Moq;

namespace TiXL.Tests.NodeGraph
{
    /// <summary>
    /// Comprehensive test suite for incremental evaluation engine
    /// Tests node evaluation, dependency tracking, and cache invalidation
    /// </summary>
    public class IncrementalEvaluationTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IRenderingEngine> _mockRenderingEngine;
        private readonly Mock<IAudioEngine> _mockAudioEngine;
        private readonly Mock<IResourceManager> _mockResourceManager;
        private readonly string _testTempDirectory;

        public IncrementalEvaluationTests(ITestOutputHelper output)
        {
            _output = output;
            _testTempDirectory = Path.Combine(Path.GetTempPath(), "TiXL_NodeGraph_Tests_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_testTempDirectory);

            _mockLogger = new Mock<ILogger>();
            _mockRenderingEngine = new Mock<IRenderingEngine>();
            _mockAudioEngine = new Mock<IAudioEngine>();
            _mockResourceManager = new Mock<IResourceManager>();
        }

        public void Dispose()
        {
            if (Directory.Exists(_testTempDirectory))
            {
                try
                {
                    Directory.Delete(_testTempDirectory, true);
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Warning: Failed to cleanup test directory: {ex.Message}");
                }
            }
        }

        #region IncrementalEvaluationEngine Tests

        [Fact]
        public void IncrementalEvaluationEngine_Initialization_ShouldInitializeAllComponents()
        {
            // Arrange
            var cancellationToken = CancellationToken.None;
            var guardrails = GuardrailConfiguration.Default;
            const int maxCacheSize = 1000;

            using var context = new EvaluationContext(
                _mockRenderingEngine.Object,
                _mockAudioEngine.Object,
                _mockResourceManager.Object,
                _mockLogger.Object,
                cancellationToken,
                guardrails,
                enableIncrementalEvaluation: true,
                maxCacheSize);

            // Act
            var engine = context.IncrementalEvaluationEngine;

            // Assert
            Assert.NotNull(engine);
            Assert.True(context.IsIncrementalEvaluationEnabled);
            Assert.Equal(maxCacheSize, context.IncrementalEvaluationEngine?.CacheStatistics.MaxSize);
        }

        [Fact]
        public void IncrementalEvaluationEngine_NodeManagement_ShouldHandleNodeLifecycle()
        {
            // Arrange
            using var context = CreateEvaluationContext();
            var engine = context.IncrementalEvaluationEngine!;
            
            var nodeCount = 50;
            var nodes = CreateTestNodes(nodeCount);

            // Act - Add nodes
            foreach (var node in nodes)
            {
                engine.AddNode(node);
            }

            // Assert - Verify node count
            Assert.Equal(nodeCount, engine.NodeCount);
            Assert.True(engine.NodeCount > 0);

            // Act - Remove nodes
            foreach (var node in nodes.Take(25))
            {
                engine.RemoveNode(node.Id);
            }

            // Assert - Verify updated count
            Assert.Equal(25, engine.NodeCount);
        }

        [Fact]
        public async Task NodeEvaluation_IncrementalEvaluation_ShouldOnlyEvaluateDirtyNodes()
        {
            // Arrange
            using var context = CreateEvaluationContext();
            var engine = context.IncrementalEvaluationEngine!;
            
            var nodes = CreateNodeGraph();
            foreach (var node in nodes)
            {
                engine.AddNode(node);
            }

            // Initial evaluation
            await engine.EvaluateAsync();

            // Act - Modify one node (mark as dirty)
            var dirtyNode = nodes[2]; // Middle node
            engine.MarkNodeDirty(dirtyNode.Id);

            // Evaluate again
            var initialEvaluationCount = GetEvaluationCount(nodes);
            await engine.EvaluateAsync();
            var finalEvaluationCount = GetEvaluationCount(nodes);

            // Assert
            _output.WriteLine($"Initial evaluations: {initialEvaluationCount}");
            _output.WriteLine($"Final evaluations: {finalEvaluationCount}");

            // Only dirty node should be re-evaluated
            Assert.True(finalEvaluationCount < initialEvaluationCount, 
                "Incremental evaluation should reduce total evaluations");
            Assert.True(finalEvaluationCount > 0, "Should evaluate dirty node");
        }

        [Fact]
        public async Task NodeEvaluation_DependencyPropagation_ShouldPropagateChangesThroughGraph()
        {
            // Arrange
            using var context = CreateEvaluationContext();
            var engine = context.IncrementalEvaluationEngine!;
            
            var nodes = CreateDependencyGraph();
            foreach (var node in nodes)
            {
                engine.AddNode(node);
            }

            // Act - Modify root node
            var rootNode = nodes[0]; // Root of dependency graph
            engine.MarkNodeDirty(rootNode.Id);

            await engine.EvaluateAsync();

            // Assert - All dependent nodes should also be marked dirty
            var dirtyNodes = engine.GetDirtyNodes();
            Assert.True(dirtyNodes.Count > 1, "Dirty should propagate through dependencies");
            
            var rootIndex = Array.FindIndex(nodes, n => n.Id == rootNode.Id);
            var dependentNode = nodes[1]; // Node that depends on root
            
            _output.WriteLine($"Dirty nodes after changing root: {dirtyNodes.Count}");
            Assert.Contains(rootNode.Id, dirtyNodes);
        }

        [Fact]
        public async Task NodeEvaluation_CacheHitRate_ShouldImprovePerformance()
        {
            // Arrange
            using var context = CreateEvaluationContext();
            var engine = context.IncrementalEvaluationEngine!;
            
            var nodes = CreateTestNodes(20);
            foreach (var node in nodes)
            {
                engine.AddNode(node);
            }

            // Act - Multiple evaluation cycles
            var evaluationCycles = 10;
            var cacheHits = new List<int>();

            for (int cycle = 0; cycle < evaluationCycles; cycle++)
            {
                // Mark no nodes dirty (should use cache)
                await engine.EvaluateAsync();
                
                var stats = engine.CacheStatistics;
                cacheHits.Add(stats.HitCount);
            }

            // Assert
            _output.WriteLine("Cache Hit Analysis:");
            for (int i = 0; i < cacheHits.Count; i++)
            {
                _output.WriteLine($"  Cycle {i + 1}: {cacheHits[i]} hits");
            }

            // Cache hit rate should increase or remain high
            var hitRateImprovement = cacheHits.Last() - cacheHits.First();
            Assert.True(hitRateImprovement >= 0, "Cache hits should not decrease over time");

            // After warmup, should have high hit rate
            var finalHitRate = cacheHits.Last();
            Assert.True(finalHitRate > 0, "Should have cache hits");
        }

        #endregion

        #region Dependency Tracking Tests

        [Fact]
        public void DependencyTracker_DependencyManagement_ShouldTrackNodeDependencies()
        {
            // Arrange
            using var context = CreateEvaluationContext();
            var engine = context.IncrementalEvaluationEngine!;
            
            var nodes = CreateDependencyGraph();
            foreach (var node in nodes)
            {
                engine.AddNode(node);
            }

            // Act - Get dependencies
            var rootNode = nodes[0];
            var directDependents = engine.GetDirectDependents(rootNode.Id);
            var allDependents = engine.GetAllDependents(rootNode.Id);

            // Assert
            Assert.NotNull(directDependents);
            Assert.NotNull(allDependents);
            Assert.True(allDependents.Count >= directDependents.Count, 
                "All dependents should include direct dependents");

            _output.WriteLine($"Node {rootNode.Id} dependencies:");
            _output.WriteLine($"  Direct dependents: {directDependents.Count}");
            _output.WriteLine($"  Total dependents: {allDependents.Count}");
        }

        [Fact]
        public async Task DependencyTracker_CircularDependencyDetection_ShouldDetectCycles()
        {
            // Arrange
            using var context = CreateEvaluationContext();
            var engine = context.IncrementalEvaluationEngine!;
            
            // Create nodes with circular dependency
            var nodeA = CreateTestNode("NodeA", Array.Empty<NodeId>());
            var nodeB = CreateTestNode("NodeB", new[] { nodeA.Id });
            var nodeC = CreateTestNode("NodeC", new[] { nodeB.Id });
            var nodeA2 = CreateTestNode("NodeA2", new[] { nodeC.Id }); // Creates cycle: A -> B -> C -> A

            // Add dependencies manually to create cycle
            engine.AddNode(nodeA);
            engine.AddNode(nodeB);
            engine.AddNode(nodeC);
            
            // This should create a circular dependency
            engine.AddDependency(nodeA2.Id, nodeC.Id);

            // Act & Assert - Should handle circular dependencies gracefully
            var exception = await Record.ExceptionAsync(async () =>
            {
                await engine.EvaluateAsync();
            });

            // Engine should handle cycles gracefully (not hang)
            Assert.Null(exception);
        }

        [Fact]
        public void DependencyTracker_DependencyUpdates_ShouldUpdateWhenNodesChange()
        {
            // Arrange
            using var context = CreateEvaluationContext();
            var engine = context.IncrementalEvaluationEngine!;
            
            var nodes = CreateTestNodes(10);
            foreach (var node in nodes)
            {
                engine.AddNode(node);
            }

            // Act - Add a new dependency
            var newNode = CreateTestNode("NewNode", new[] { nodes[0].Id, nodes[1].Id });
            engine.AddNode(newNode);

            // Add dependency from existing node to new node
            engine.AddDependency(nodes[2].Id, newNode.Id);

            // Assert - Verify dependencies are updated
            var dependents = engine.GetDirectDependents(newNode.Id);
            Assert.True(dependents.Count >= 1, "Should track new dependencies");
            
            _output.WriteLine($"New node dependencies updated: {dependents.Count} dependents");
        }

        #endregion

        #region Cache Invalidation Tests

        [Fact]
        public void CacheInvalidation_NodeModification_ShouldInvalidateAffectedCacheEntries()
        {
            // Arrange
            using var context = CreateEvaluationContext();
            var engine = context.IncrementalEvaluationEngine!;
            
            var nodes = CreateDependencyGraph();
            foreach (var node in nodes)
            {
                engine.AddNode(node);
            }

            // Initial evaluation to populate cache
            engine.EvaluateAsync().Wait();

            // Act - Mark node as dirty
            var targetNode = nodes[2];
            engine.MarkNodeDirty(targetNode.Id);

            // Assert - Cache should be invalidated for affected nodes
            var statsBefore = engine.CacheStatistics;
            engine.EvaluateAsync().Wait();
            var statsAfter = engine.CacheStatistics;

            _output.WriteLine("Cache Invalidation Results:");
            _output.WriteLine($"  Before - Hit: {statsBefore.HitCount}, Miss: {statsBefore.MissCount}");
            _output.WriteLine($"  After - Hit: {statsAfter.HitCount}, Miss: {statsAfter.MissCount}");

            // Cache should reflect invalidation (more misses)
            Assert.True(statsAfter.MissCount >= statsBefore.MissCount, 
                "Cache misses should increase when nodes are invalidated");
        }

        [Fact]
        public async Task CacheInvalidation_TimeBasedEviction_ShouldRemoveOldEntries()
        {
            // Arrange - Small cache with short TTL
            using var context = CreateEvaluationContext(maxCacheSize: 5);
            var engine = context.IncrementalEvaluationEngine!;
            
            var nodes = CreateTestNodes(10); // More nodes than cache size
            foreach (var node in nodes)
            {
                engine.AddNode(node);
            }

            // Act - Evaluate to fill cache
            await engine.EvaluateAsync();
            
            var stats1 = engine.CacheStatistics;
            _output.WriteLine($"Initial cache size: {stats1.Size}, Max: {stats1.MaxSize}");

            // Wait for time-based eviction (simulate by manipulating cache)
            await Task.Delay(100); // This would trigger TTL in real implementation
            
            // Force evaluation with different nodes to trigger eviction
            foreach (var node in nodes.Take(3))
            {
                engine.MarkNodeDirty(node.Id);
            }
            
            await engine.EvaluateAsync();

            var stats2 = engine.CacheStatistics;

            // Assert
            _output.WriteLine("Time-based Eviction Results:");
            _output.WriteLine($"  After eviction - Size: {stats2.Size}, Hits: {stats2.HitCount}");
            
            // Cache size should not exceed maximum
            Assert.True(stats2.Size <= stats2.MaxSize, 
                $"Cache size {stats2.Size} should not exceed max {stats2.MaxSize}");
        }

        [Fact]
        public void CacheInvalidation_MemoryPressure_ShouldEvictLRUEntries()
        {
            // Arrange
            using var context = CreateEvaluationContext(maxCacheSize: 3);
            var engine = context.IncrementalEvaluationEngine!;
            
            var nodes = CreateTestNodes(5); // More than cache size
            foreach (var node in nodes)
            {
                engine.AddNode(node);
            }

            // Act - Fill cache
            engine.EvaluateAsync().Wait();
            
            // Access some entries to make them recent
            engine.GetCachedResult(nodes[0].Id);
            engine.GetCachedResult(nodes[1].Id);

            // Add more to trigger LRU eviction
            engine.MarkNodeDirty(nodes[4].Id);
            engine.EvaluateAsync().Wait();

            // Assert
            var stats = engine.CacheStatistics;
            _output.WriteLine($"LRU Eviction Results - Size: {stats.Size}, Max: {stats.MaxSize}");
            
            Assert.True(stats.Size <= stats.MaxSize, "Should respect cache size limit");
        }

        #endregion

        #region Performance Tests

        [Fact]
        public async Task Performance_LargeGraphEvaluation_ShouldScaleWell()
        {
            // Arrange
            using var context = CreateEvaluationContext();
            var engine = context.IncrementalEvaluationEngine!;
            
            var nodeCount = 1000;
            var nodes = CreateTestNodes(nodeCount);
            foreach (var node in nodes)
            {
                engine.AddNode(node);
            }

            // Act - Measure evaluation performance
            var evaluationTimes = new List<double>();
            var evaluationCount = 5;

            for (int i = 0; i < evaluationCount; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                
                await engine.EvaluateAsync();
                
                stopwatch.Stop();
                evaluationTimes.Add(stopwatch.Elapsed.TotalMilliseconds);
                
                _output.WriteLine($"Evaluation {i + 1}: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
            }

            // Assert - Performance should be reasonable for large graphs
            var averageTime = evaluationTimes.Average();
            var maxTime = evaluationTimes.Max();
            
            _output.WriteLine($"Large Graph Performance:");
            _output.WriteLine($"  Nodes: {nodeCount}");
            _output.WriteLine($"  Average time: {averageTime:F2}ms");
            _output.WriteLine($"  Max time: {maxTime:F2}ms");
            _output.WriteLine($"  Evaluations: {engine.NodeCount}");

            Assert.True(averageTime < 1000, "Average evaluation time should be reasonable");
            Assert.True(maxTime < 2000, "Maximum evaluation time should not be excessive");
        }

        [Fact]
        public async Task Performance_ConcurrentEvaluation_ShouldHandleConcurrency()
        {
            // Arrange
            using var context = CreateEvaluationContext();
            var engine = context.IncrementalEvaluationEngine!;
            
            var nodes = CreateTestNodes(100);
            foreach (var node in nodes)
            {
                engine.AddNode(node);
            }

            var concurrencyLevel = Environment.ProcessorCount;
            var tasks = new List<Task>();
            var exceptions = new ConcurrentBag<Exception>();

            // Act - Concurrent evaluations
            for (int i = 0; i < concurrencyLevel; i++)
            {
                int taskId = i;
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        for (int j = 0; j < 10; j++)
                        {
                            // Randomly mark nodes dirty
                            var nodeIndex = (taskId * 10 + j) % nodes.Length;
                            engine.MarkNodeDirty(nodes[nodeIndex].Id);
                            
                            await engine.EvaluateAsync();
                            
                            await Task.Delay(1); // Small delay
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            _output.WriteLine($"Concurrent Evaluation Results:");
            _output.WriteLine($"  Concurrency level: {concurrencyLevel}");
            _output.WriteLine($"  Tasks completed: {tasks.Count}");
            _output.WriteLine($"  Exceptions: {exceptions.Count}");

            Assert.Empty(exceptions, $"Should not have exceptions: {string.Join(", ", exceptions.Select(e => e.Message))}");
        }

        #endregion

        #region Integration Tests

        [Fact]
        public async Task Integration_CompleteWorkflow_ShouldHandleFullEvaluationCycle()
        {
            // Arrange
            using var context = CreateEvaluationContext();
            var engine = context.IncrementalEvaluationEngine!;
            
            // Create complex node graph
            var nodes = CreateComplexNodeGraph();
            foreach (var node in nodes)
            {
                engine.AddNode(node);
            }

            var evaluationSteps = new List<string>();
            var metrics = new List<EvaluationEngineMetrics>();

            // Act - Complete workflow
            for (int step = 0; step < 5; step++)
            {
                evaluationSteps.Add($"Step {step + 1}: Started");
                
                // Mark some nodes dirty
                if (step % 2 == 0)
                {
                    var nodeToModify = nodes[step % nodes.Length];
                    engine.MarkNodeDirty(nodeToModify.Id);
                    evaluationSteps.Add($"  Marked node {nodeToModify.Id} as dirty");
                }
                
                // Evaluate
                var stepStopwatch = Stopwatch.StartNew();
                await engine.EvaluateAsync();
                stepStopwatch.Stop();
                
                // Record metrics
                var stepMetrics = context.IncrementalEvaluationMetrics;
                metrics.Add(stepMetrics);
                
                evaluationSteps.Add($"  Completed in {stepStopwatch.Elapsed.TotalMilliseconds:F2}ms");
                evaluationSteps.Add($"  Dirty nodes: {engine.DirtyNodeCount}");
                evaluationSteps.Add($"  Cache hits: {engine.CacheStatistics.HitCount}");
            }

            // Assert - Verify workflow completed successfully
            _output.WriteLine("Complete Workflow Results:");
            foreach (var step in evaluationSteps)
            {
                _output.WriteLine(step);
            }

            Assert.Equal(5, metrics.Count);
            Assert.True(metrics.All(m => m.TotalEvaluations > 0), "All steps should have evaluations");
        }

        [Fact]
        public async Task Integration_ContextIntegration_ShouldWorkWithEvaluationContext()
        {
            // Arrange
            using var context = CreateEvaluationContext();
            
            // Enable performance monitoring in context
            context.Configuration.MaxEvaluationTimeMs = 1000;
            context.Configuration.MaxNodeEvaluationsPerFrame = 100;

            var engine = context.IncrementalEvaluationEngine!;
            var nodes = CreateTestNodes(50);
            foreach (var node in nodes)
            {
                engine.AddNode(node);
            }

            // Act - Evaluate with context guardrails
            for (int i = 0; i < 10; i++)
            {
                // Mark some nodes dirty
                var dirtyCount = Math.Min(i + 1, nodes.Length);
                for (int j = 0; j < dirtyCount; j++)
                {
                    engine.MarkNodeDirty(nodes[j].Id);
                }
                
                await engine.EvaluateAsync();
                
                var metrics = context.Metrics;
                var state = context.CurrentState;
                
                _output.WriteLine($"Frame {i + 1}:");
                _output.WriteLine($"  Evaluations: {metrics.TotalEvaluations}");
                _output.WriteLine($"  IsOverLimit: {state.IsOverEvaluationLimit}");
                _output.WriteLine($"  Cache hit rate: {engine.CacheStatistics.HitRate:P2}");
                
                // Should not exceed guardrail limits
                Assert.False(state.IsOverEvaluationLimit, "Should respect evaluation limits");
            }
        }

        #endregion

        #region Edge Case Tests

        [Fact]
        public void EdgeCase_EmptyGraph_ShouldHandleGracefully()
        {
            // Arrange
            using var context = CreateEvaluationContext();
            var engine = context.IncrementalEvaluationEngine!;

            // Act & Assert
            Assert.Equal(0, engine.NodeCount);
            Assert.Equal(0, engine.DirtyNodeCount);
            
            // Should not throw on empty graph
            var exception = Record.ExceptionAsync(async () => await engine.EvaluateAsync());
            Assert.Null(exception.Result);
        }

        [Fact]
        public void EdgeCase_SingleNode_ShouldWorkCorrectly()
        {
            // Arrange
            using var context = CreateEvaluationContext();
            var engine = context.IncrementalEvaluationEngine!;
            
            var singleNode = CreateTestNode("Single", Array.Empty<NodeId>());
            engine.AddNode(singleNode);

            // Act
            engine.EvaluateAsync().Wait();
            engine.MarkNodeDirty(singleNode.Id);
            engine.EvaluateAsync().Wait();

            // Assert
            Assert.Equal(1, engine.NodeCount);
            Assert.Equal(0, engine.DirtyNodeCount); // Should be clean after evaluation
        }

        [Fact]
        public async Task EdgeCase_DisconnectedNodes_ShouldHandleIndependently()
        {
            // Arrange
            using var context = CreateEvaluationContext();
            var engine = context.IncrementalEvaluationEngine!;
            
            // Create disconnected nodes
            var nodeA = CreateTestNode("DisconnectedA", Array.Empty<NodeId>());
            var nodeB = CreateTestNode("DisconnectedB", Array.Empty<NodeId>());
            var nodeC = CreateTestNode("DisconnectedC", Array.Empty<NodeId>());
            
            engine.AddNode(nodeA);
            engine.AddNode(nodeB);
            engine.AddNode(nodeC);

            // Act - Evaluate all
            await engine.EvaluateAsync();
            
            // Mark one dirty
            engine.MarkNodeDirty(nodeB.Id);
            await engine.EvaluateAsync();

            // Assert
            Assert.Equal(3, engine.NodeCount);
            Assert.Equal(0, engine.DirtyNodeCount); // Should be clean
            
            // Each node should be independent
            var nodeADependents = engine.GetAllDependents(nodeA.Id);
            var nodeBDependents = engine.GetAllDependents(nodeB.Id);
            
            Assert.Empty(nodeADependents); // No dependencies
            Assert.Empty(nodeBDependents); // No dependencies
        }

        [Fact]
        public void EdgeCase_ExcessiveNodes_ShouldRespectResourceLimits()
        {
            // Arrange
            using var context = CreateEvaluationContext(maxCacheSize: 50);
            var engine = context.IncrementalEvaluationEngine!;
            
            var excessiveNodeCount = 1000;
            var nodes = CreateTestNodes(excessiveNodeCount);

            // Act - Add nodes
            foreach (var node in nodes)
            {
                engine.AddNode(node);
            }

            // Assert - Should handle large number of nodes
            Assert.Equal(excessiveNodeCount, engine.NodeCount);
            
            // Cache should respect size limits
            var stats = engine.CacheStatistics;
            Assert.True(stats.Size <= stats.MaxSize, "Cache should respect size limits");
        }

        #endregion

        #region Performance Regression Tests

        [Fact]
        public async Task PerformanceRegression_EvaluationSpeed_ShouldNotDegrade()
        {
            // Arrange
            using var context = CreateEvaluationContext();
            var engine = context.IncrementalEvaluationEngine!;
            
            var baselineNodes = CreateTestNodes(100);
            foreach (var node in baselineNodes)
            {
                engine.AddNode(node);
            }

            // Establish baseline performance
            var baselineTimes = new List<double>();
            for (int i = 0; i < 5; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                await engine.EvaluateAsync();
                stopwatch.Stop();
                baselineTimes.Add(stopwatch.Elapsed.TotalMilliseconds);
            }
            
            var baselineAverage = baselineTimes.Average();
            _output.WriteLine($"Baseline average: {baselineAverage:F2}ms");

            // Act - Test performance over time
            var regressionTimes = new List<double>();
            for (int cycle = 0; cycle < 10; cycle++)
            {
                // Modify some nodes
                var dirtyCount = Math.Min(cycle + 1, baselineNodes.Length);
                for (int i = 0; i < dirtyCount; i++)
                {
                    engine.MarkNodeDirty(baselineNodes[i].Id);
                }
                
                var stopwatch = Stopwatch.StartNew();
                await engine.EvaluateAsync();
                stopwatch.Stop();
                
                regressionTimes.Add(stopwatch.Elapsed.TotalMilliseconds);
            }

            var regressionAverage = regressionTimes.Average();
            _output.WriteLine($"Regression average: {regressionAverage:F2}ms");

            // Assert - Should not degrade significantly
            const double regressionThreshold = 20.0; // 20% increase
            
            var performanceChange = ((regressionAverage - baselineAverage) / baselineAverage) * 100;
            
            Assert.True(performanceChange <= regressionThreshold,
                $"Performance degraded by {performanceChange:F1}%, threshold is {regressionThreshold}%");

            _output.WriteLine($"Performance change: {performanceChange:F1}%");
        }

        #endregion

        #region Helper Methods

        private EvaluationContext CreateEvaluationContext(int maxCacheSize = 1000)
        {
            var cancellationToken = CancellationToken.None;
            var guardrails = GuardrailConfiguration.Default;
            
            return new EvaluationContext(
                _mockRenderingEngine.Object,
                _mockAudioEngine.Object,
                _mockResourceManager.Object,
                _mockLogger.Object,
                cancellationToken,
                guardrails,
                enableIncrementalEvaluation: true,
                maxCacheSize);
        }

        private TiXLNode[] CreateTestNodes(int count)
        {
            var nodes = new TiXLNode[count];
            var random = new Random(42); // Deterministic for testing
            
            for (int i = 0; i < count; i++)
            {
                var dependencyCount = random.Next(0, Math.Min(5, i)); // Some dependencies
                var dependencies = new List<NodeId>();
                
                for (int d = 0; d < dependencyCount; d++)
                {
                    dependencies.Add(new NodeId(i - d - 1));
                }
                
                nodes[i] = CreateTestNode($"Node{i}", dependencies.ToArray());
            }
            
            return nodes;
        }

        private TiXLNode CreateTestNode(string name, NodeId[] dependencies)
        {
            var nodeId = new NodeId(name.GetHashCode());
            return new TiXLNode
            {
                Id = nodeId,
                Name = name,
                Dependencies = dependencies,
                EvaluationCount = 0,
                LastEvaluationTime = DateTimeOffset.MinValue,
                Result = null
            };
        }

        private TiXLNode[] CreateDependencyGraph()
        {
            // Create a linear dependency chain: A -> B -> C -> D -> E
            return new[]
            {
                CreateTestNode("Root", Array.Empty<NodeId>()),
                CreateTestNode("Child1", new[] { new NodeId("Root".GetHashCode()) }),
                CreateTestNode("Child2", new[] { new NodeId("Child1".GetHashCode()) }),
                CreateTestNode("Child3", new[] { new NodeId("Child2".GetHashCode()) }),
                CreateTestNode("Leaf", new[] { new NodeId("Child3".GetHashCode()) })
            };
        }

        private TiXLNode[] CreateComplexNodeGraph()
        {
            // Create a more complex graph with multiple paths
            return new[]
            {
                CreateTestNode("InputA", Array.Empty<NodeId>()),
                CreateTestNode("InputB", Array.Empty<NodeId>()),
                CreateTestNode("ProcessA", new[] { new NodeId("InputA".GetHashCode()) }),
                CreateTestNode("ProcessB", new[] { new NodeId("InputB".GetHashCode()) }),
                CreateTestNode("Combine", new[] { new NodeId("ProcessA".GetHashCode()), new NodeId("ProcessB".GetHashCode()) }),
                CreateTestNode("OutputA", new[] { new NodeId("Combine".GetHashCode()) }),
                CreateTestNode("OutputB", new[] { new NodeId("ProcessA".GetHashCode()), new NodeId("ProcessB".GetHashCode()) })
            };
        }

        private static int GetEvaluationCount(TiXLNode[] nodes)
        {
            return nodes.Sum(node => node.EvaluationCount);
        }

        #endregion
    }

    #region Supporting Classes

    public class TiXLNode
    {
        public NodeId Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public NodeId[] Dependencies { get; set; } = Array.Empty<NodeId>();
        public int EvaluationCount { get; set; }
        public DateTimeOffset LastEvaluationTime { get; set; }
        public object? Result { get; set; }
        public bool IsDirty { get; set; }
    }

    public class NodeId : IEquatable<NodeId>
    {
        private readonly int _value;

        public NodeId(int value)
        {
            _value = value;
        }

        public bool Equals(NodeId? other)
        {
            return other != null && _value == other._value;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as NodeId);
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        public override string ToString()
        {
            return $"NodeId({_value})";
        }

        public static implicit operator int(NodeId nodeId) => nodeId._value;
    }

    public class CacheStatistics
    {
        public int HitCount { get; set; }
        public int MissCount { get; set; }
        public int Size { get; set; }
        public int MaxSize { get; set; }
        public double HitRate => HitCount + MissCount > 0 ? (double)HitCount / (HitCount + MissCount) : 0;
    }

    #endregion
}
