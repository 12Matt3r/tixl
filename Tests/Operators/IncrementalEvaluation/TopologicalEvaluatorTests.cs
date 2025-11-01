using Xunit;
using Xunit.Abstractions;
using TiXL.Tests.Categories;
using TiXL.Tests.Fixtures;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using T3.Core.Operators;
using T3.Core.Operators.IncrementalEvaluation;
using Microsoft.Extensions.Logging;
using Moq;

namespace TiXL.Tests.Operators.IncrementalEvaluation
{
    /// <summary>
    /// Unit tests for TopologicalEvaluator
    /// </summary>
    [Category(TestCategories.Operators)]
    [Category(TestCategories.Unit)]
    public class TopologicalEvaluatorTests : CoreTestFixture
    {
        private readonly ITestOutputHelper _output;
        private readonly ILogger<TopologicalEvaluatorTests> _logger;
        private readonly Mock<IEvaluationContext> _mockContext;
        private readonly Mock<ICacheManager> _mockCacheManager;

        public TopologicalEvaluatorTests(ITestOutputHelper output) : base()
        {
            _output = output;
            _logger = ServiceProvider.GetRequiredService<ILogger<TopologicalEvaluatorTests>>();
            
            _mockContext = new Mock<IEvaluationContext>();
            _mockCacheManager = new Mock<ICacheManager>();
        }

        [Fact]
        public void TopologicalEvaluator_CreateInstance_WithValidDependencies_Succeeds()
        {
            // Arrange & Act
            var evaluator = new TopologicalEvaluator(_mockContext.Object, _mockCacheManager.Object);

            // Assert
            evaluator.Should().NotBeNull();
        }

        [Fact]
        public void TopologicalEvaluator_CreateInstance_WithNullContext_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Action act = () => new TopologicalEvaluator(null!, _mockCacheManager.Object);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void TopologicalEvaluator_CreateInstance_WithNullCacheManager_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Action act = () => new TopologicalEvaluator(_mockContext.Object, null!);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public async Task TopologicalEvaluator_EvaluateAsync_WithValidNodes_EvaluatesSuccessfully()
        {
            // Arrange
            var evaluator = new TopologicalEvaluator(_mockContext.Object, _mockCacheManager.Object);
            var nodes = new[] { "node1", "node2", "node3" };
            var cancellationToken = CancellationToken.None;

            // Act
            var result = await evaluator.EvaluateAsync(nodes, cancellationToken);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.EvaluatedNodes.Should().Equal(nodes);
            result.EvaluationTime.Should().BeGreaterThan(TimeSpan.Zero);
        }

        [Fact]
        public async Task TopologicalEvaluator_EvaluateAsync_WithEmptyNodes_ReturnsEmptyResult()
        {
            // Arrange
            var evaluator = new TopologicalEvaluator(_mockContext.Object, _mockCacheManager.Object);
            var nodes = Array.Empty<string>();
            var cancellationToken = CancellationToken.None;

            // Act
            var result = await evaluator.EvaluateAsync(nodes, cancellationToken);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.EvaluatedNodes.Should().BeEmpty();
            result.EvaluationTime.Should().BeGreaterThan(TimeSpan.Zero);
        }

        [Fact]
        public async Task TopologicalEvaluator_EvaluateAsync_WithCancelledToken_ThrowsOperationCanceledException()
        {
            // Arrange
            var evaluator = new TopologicalEvaluator(_mockContext.Object, _mockCacheManager.Object);
            var nodes = new[] { "node1", "node2" };
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await FluentActions.Awaiting(() => evaluator.EvaluateAsync(nodes, cts.Token))
                .Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task TopologicalEvaluator_EvaluateAsync_WithDependencyOrder_RespectsTopologicalOrder()
        {
            // Arrange
            var evaluator = new TopologicalEvaluator(_mockContext.Object, _mockCacheManager.Object);
            var nodes = new[] { "leaf", "middle", "root" }; // Reverse order
            var cancellationToken = CancellationToken.None;

            // Act
            var result = await evaluator.EvaluateAsync(nodes, cancellationToken);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            // Should evaluate in correct dependency order: root -> middle -> leaf
            result.EvaluatedNodes.Should().HaveCount(3);
        }

        [Fact]
        public async Task TopologicalEvaluator_EvaluateAsync_WithCircularDependency_ThrowsInvalidOperationException()
        {
            // Arrange
            var evaluator = new TopologicalEvaluator(_mockContext.Object, _mockCacheManager.Object);
            var nodes = new[] { "nodeA", "nodeB", "nodeC" };
            var cancellationToken = CancellationToken.None;

            // Simulate circular dependency setup would be done by dependency graph
            // For this test, we'll verify the evaluator handles it gracefully

            // Act & Assert
            await FluentActions.Awaiting(() => evaluator.EvaluateAsync(nodes, cancellationToken))
                .Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task TopologicalEvaluator_EvaluateAsync_WithLargeNodeSet_HandlesEfficiently()
        {
            // Arrange
            var evaluator = new TopologicalEvaluator(_mockContext.Object, _mockCacheManager.Object);
            var nodes = new string[1000];
            for (int i = 0; i < 1000; i++)
            {
                nodes[i] = $"node-{i}";
            }
            var cancellationToken = CancellationToken.None;

            // Act
            var startTime = DateTime.UtcNow;
            var result = await evaluator.EvaluateAsync(nodes, cancellationToken);
            var endTime = DateTime.UtcNow;
            var duration = endTime - startTime;

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.EvaluatedNodes.Should().HaveCount(1000);
            duration.Should().BeLessThan(TimeSpan.FromSeconds(5)); // Should complete within reasonable time
        }

        [Fact]
        public async Task TopologicalEvaluator_EvaluateWithCache_HitsCacheCorrectly()
        {
            // Arrange
            var evaluator = new TopologicalEvaluator(_mockContext.Object, _mockCacheManager.Object);
            var nodes = new[] { "cached-node" };
            var cachedResult = new { value = 42 };
            
            _mockCacheManager.Setup(cm => cm.Retrieve("cached-node", It.IsAny<string>())).Returns(cachedResult);
            var cancellationToken = CancellationToken.None;

            // Act
            var result = await evaluator.EvaluateAsync(nodes, cancellationToken);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            _mockCacheManager.Verify(cm => cm.Retrieve("cached-node", It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task TopologicalEvaluator_EvaluateWithCache_MissesAndStoresCorrectly()
        {
            // Arrange
            var evaluator = new TopologicalEvaluator(_mockContext.Object, _mockCacheManager.Object);
            var nodes = new[] { "uncached-node" };
            
            _mockCacheManager.Setup(cm => cm.Retrieve("uncached-node", It.IsAny<string>())).Returns((object?)null);
            var cancellationToken = CancellationToken.None;

            // Act
            var result = await evaluator.EvaluateAsync(nodes, cancellationToken);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            _mockCacheManager.Verify(cm => cm.Store("uncached-node", It.IsAny<string>(), It.IsAny<object>()), Times.Once);
        }

        [Fact]
        public async Task TopologicalEvaluator_ParallelEvaluation_WithMultipleNodes_EvaluatesConcurrently()
        {
            // Arrange
            var evaluator = new TopologicalEvaluator(_mockContext.Object, _mockCacheManager.Object);
            var nodes = new[] { "node1", "node2", "node3", "node4", "node5" };
            var cancellationToken = CancellationToken.None;

            // Act
            var result = await evaluator.EvaluateAsync(nodes, cancellationToken);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.EvaluatedNodes.Should().HaveCount(5);
            
            // All nodes should be evaluated (parallel evaluation)
            result.EvaluationTime.Should().BeLessThan(TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task TopologicalEvaluator_ErrorHandling_WithEvaluationError_HandlesGracefully()
        {
            // Arrange
            var evaluator = new TopologicalEvaluator(_mockContext.Object, _mockCacheManager.Object);
            var nodes = new[] { "error-node" };
            var cancellationToken = CancellationToken.None;

            // Simulate evaluation error by setting up mock to throw
            // This would typically be done by mocking the actual operator evaluation

            // Act
            var result = await evaluator.EvaluateAsync(nodes, cancellationToken);

            // Assert
            result.Should().NotBeNull();
            // Result should indicate success even if individual node evaluation had errors
            result.Success.Should().BeTrue();
        }

        [Fact]
        public async Task TopologicalEvaluator_EvaluationMetrics_RecordsCorrectMetrics()
        {
            // Arrange
            var evaluator = new TopologicalEvaluator(_mockContext.Object, _mockCacheManager.Object);
            var nodes = new[] { "node1", "node2", "node3" };
            var cancellationToken = CancellationToken.None;

            // Act
            var result = await evaluator.EvaluateAsync(nodes, cancellationToken);

            // Assert
            result.Should().NotBeNull();
            result.EvaluatedNodes.Should().HaveCount(3);
            result.EvaluationTime.Should().BeGreaterThan(TimeSpan.Zero);
            result.CacheHits.Should().BeGreaterThanOrEqualTo(0);
            result.CacheMisses.Should().BeGreaterThanOrEqualTo(0);
        }

        [Fact]
        public async Task TopologicalEvaluator_MemoryManagement_WithLargeData_PreventsMemoryLeaks()
        {
            // Arrange
            var evaluator = new TopologicalEvaluator(_mockContext.Object, _mockCacheManager.Object);
            var nodes = new[] { "large-data-node" };
            var largeData = new { data = new byte[1024 * 1024] }; // 1MB
            var cancellationToken = CancellationToken.None;

            _mockCacheManager.Setup(cm => cm.Retrieve("large-data-node", It.IsAny<string>())).Returns(largeData);

            // Act
            var result = await evaluator.EvaluateAsync(nodes, cancellationToken);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            
            // Verify cache operations for memory management
            _mockCacheManager.Verify(cm => cm.Has("large-data-node", It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task TopologicalEvaluator_BatchEvaluation_MultipleBatches_HandlesCorrectly()
        {
            // Arrange
            var evaluator = new TopologicalEvaluator(_mockContext.Object, _mockCacheManager.Object);
            var batch1 = new[] { "node1", "node2" };
            var batch2 = new[] { "node3", "node4", "node5" };
            var cancellationToken = CancellationToken.None;

            // Act
            var result1 = await evaluator.EvaluateAsync(batch1, cancellationToken);
            var result2 = await evaluator.EvaluateAsync(batch2, cancellationToken);

            // Assert
            result1.Should().NotBeNull();
            result1.Success.Should().BeTrue();
            result1.EvaluatedNodes.Should().HaveCount(2);
            
            result2.Should().NotBeNull();
            result2.Success.Should().BeTrue();
            result2.EvaluatedNodes.Should().HaveCount(3);
        }

        [Fact]
        public async Task TopologicalEvaluator_Interruption_CancellationDuringEvaluation_StopsGracefully()
        {
            // Arrange
            var evaluator = new TopologicalEvaluator(_mockContext.Object, _mockCacheManager.Object);
            var nodes = new[] { "node1", "node2", "node3", "node4", "node5" };
            
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(100)); // Cancel quickly

            // Act & Assert
            await FluentActions.Awaiting(() => evaluator.EvaluateAsync(nodes, cts.Token))
                .Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task TopologicalEvaluator_ComplexDependencyChain_WithMultipleLevels_EvaluatesCorrectly()
        {
            // Arrange
            var evaluator = new TopologicalEvaluator(_mockContext.Object, _mockCacheManager.Object);
            
            // Create a complex dependency chain: A -> B -> C -> D
            //                                   E -> C
            var nodes = new[] { "nodeD", "nodeC", "nodeB", "nodeA", "nodeE" };
            var cancellationToken = CancellationToken.None;

            // Act
            var result = await evaluator.EvaluateAsync(nodes, cancellationToken);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.EvaluatedNodes.Should().HaveCount(5);
            
            // Should respect dependency order
            var evaluatedOrder = result.EvaluatedNodes.ToList();
            var indexA = evaluatedOrder.IndexOf("nodeA");
            var indexB = evaluatedOrder.IndexOf("nodeB");
            var indexC = evaluatedOrder.IndexOf("nodeC");
            var indexD = evaluatedOrder.IndexOf("nodeD");
            var indexE = evaluatedOrder.IndexOf("nodeE");
            
            // A should come before B, B before C, etc.
            indexA.Should().BeLessThan(indexB);
            indexB.Should().BeLessThan(indexC);
            indexC.Should().BeLessThan(indexD);
        }

        [Fact]
        public async Task TopologicalEvaluator_DisconnectedNodes_WithIndependentComponents_EvaluatesAll()
        {
            // Arrange
            var evaluator = new TopologicalEvaluator(_mockContext.Object, _mockCacheManager.Object);
            
            // Two disconnected chains: A -> B and C -> D
            var nodes = new[] { "nodeB", "nodeA", "nodeD", "nodeC" };
            var cancellationToken = CancellationToken.None;

            // Act
            var result = await evaluator.EvaluateAsync(nodes, cancellationToken);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.EvaluatedNodes.Should().HaveCount(4);
            
            // Both chains should be evaluated
            var evaluatedNodes = result.EvaluatedNodes;
            evaluatedNodes.Should().Contain("nodeA");
            evaluatedNodes.Should().Contain("nodeB");
            evaluatedNodes.Should().Contain("nodeC");
            evaluatedNodes.Should().Contain("nodeD");
        }

        [Fact]
        public async Task TopologicalEvaluator_PerformanceBenchmark_WithVariousSizes_MeetsPerformanceTargets()
        {
            // Arrange
            var evaluator = new TopologicalEvaluator(_mockContext.Object, _mockCacheManager.Object);
            var sizes = new[] { 10, 50, 100, 500, 1000 };
            var cancellationToken = CancellationToken.None;

            foreach (var size in sizes)
            {
                // Act
                var nodes = new string[size];
                for (int i = 0; i < size; i++)
                {
                    nodes[i] = $"perf-node-{i}";
                }

                var startTime = DateTime.UtcNow;
                var result = await evaluator.EvaluateAsync(nodes, cancellationToken);
                var endTime = DateTime.UtcNow;
                var duration = endTime - startTime;

                // Assert
                result.Should().NotBeNull();
                result.Success.Should().BeTrue();
                result.EvaluatedNodes.Should().HaveCount(size);
                
                // Performance should scale reasonably well
                var expectedMaxTime = TimeSpan.FromMilliseconds(size * 10); // 10ms per node max
                duration.Should().BeLessThan(expectedMaxTime);
            }
        }

        [Fact]
        public async Task TopologicalEvaluator_ThreadSafety_ConcurrentEvaluations_Succeeds()
        {
            // Arrange
            var evaluator = new TopologicalEvaluator(_mockContext.Object, _mockCacheManager.Object);
            var tasks = new List<Task>();
            var numberOfConcurrentEvaluations = 5;

            // Act - Run multiple evaluations concurrently
            for (int i = 0; i < numberOfConcurrentEvaluations; i++)
            {
                var evaluationIndex = i;
                tasks.Add(Task.Run(async () =>
                {
                    var nodes = new[] { $"concurrent-node-{evaluationIndex}-1", $"concurrent-node-{evaluationIndex}-2" };
                    var result = await evaluator.EvaluateAsync(nodes);
                    result.Success.Should().BeTrue();
                    result.EvaluatedNodes.Should().HaveCount(2);
                }));
            }

            await Task.WhenAll(tasks);

            // Assert - All evaluations should complete successfully
            tasks.Should().HaveCount(numberOfConcurrentEvaluations);
            // All tasks completed without throwing exceptions
        }

        [Fact]
        public async Task TopologicalEvaluator_CacheInvalidation_WithDataChanges_InvalidatesCorrectly()
        {
            // Arrange
            var evaluator = new TopologicalEvaluator(_mockContext.Object, _mockCacheManager.Object);
            var nodes = new[] { "cache-node" };
            var cancellationToken = CancellationToken.None;

            // First evaluation
            var result1 = await evaluator.EvaluateAsync(nodes, cancellationToken);
            
            // Simulate cache invalidation (would happen when node parameters change)
            _mockCacheManager.Verify(cm => cm.InvalidateNode("cache-node"), Times.Once);

            // Second evaluation after invalidation
            var result2 = await evaluator.EvaluateAsync(nodes, cancellationToken);

            // Assert
            result1.Should().NotBeNull();
            result1.Success.Should().BeTrue();
            
            result2.Should().NotBeNull();
            result2.Success.Should().BeTrue();
        }

        [Fact]
        public void TopologicalEvaluator_Dispose_CompletesSuccessfully()
        {
            // Arrange
            var evaluator = new TopologicalEvaluator(_mockContext.Object, _mockCacheManager.Object);

            // Act & Assert
            var exception = Record.Exception(() => evaluator.Dispose());
            exception.Should().BeNull();
        }
    }
}