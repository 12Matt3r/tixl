using Xunit;
using Xunit.Abstractions;
using TiXL.Tests.Categories;
using TiXL.Tests.Fixtures;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using T3.Core.Operators;
using T3.Core.Operators.IncrementalEvaluation;
using Microsoft.Extensions.Logging;
using Moq;

namespace TiXL.Tests.Operators.IncrementalEvaluation
{
    /// <summary>
    /// Unit tests for IncrementalNodeGraph
    /// </summary>
    [Category(TestCategories.Operators)]
    [Category(TestCategories.Unit)]
    public class IncrementalNodeGraphTests : CoreTestFixture
    {
        private readonly ITestOutputHelper _output;
        private readonly ILogger<IncrementalNodeGraphTests> _logger;
        private readonly Mock<IEvaluationContext> _mockContext;
        private readonly Mock<IDependencyGraph> _mockDependencyGraph;
        private readonly Mock<ICacheManager> _mockCacheManager;
        private readonly Mock<IDirtyTracker> _mockDirtyTracker;
        private readonly Mock<ITopologicalEvaluator> _mockEvaluator;
        private readonly Mock<IPerformanceMonitor> _mockPerformanceMonitor;

        public IncrementalNodeGraphTests(ITestOutputHelper output) : base()
        {
            _output = output;
            _logger = ServiceProvider.GetRequiredService<ILogger<IncrementalNodeGraphTests>>();
            
            // Create mock dependencies
            _mockContext = new Mock<IEvaluationContext>();
            _mockDependencyGraph = new Mock<IDependencyGraph>();
            _mockCacheManager = new Mock<ICacheManager>();
            _mockDirtyTracker = new Mock<IDirtyTracker>();
            _mockEvaluator = new Mock<ITopologicalEvaluator>();
            _mockPerformanceMonitor = new Mock<IPerformanceMonitor>();
        }

        [Fact]
        public void IncrementalNodeGraph_CreateInstance_WithValidDependencies_Success()
        {
            // Arrange & Act
            Action act = () =>
            {
                var graph = new IncrementalNodeGraph(
                    _mockContext.Object,
                    _mockDependencyGraph.Object,
                    _mockCacheManager.Object,
                    _mockDirtyTracker.Object,
                    _mockEvaluator.Object,
                    _mockPerformanceMonitor.Object);
            };

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void IncrementalNodeGraph_AddNode_WithValidNode_AddsSuccessfully()
        {
            // Arrange
            var graph = CreateTestGraph();
            var nodeId = "test-node-1";
            var nodeData = new { Value = 42 };

            // Act
            graph.AddNode(nodeId, nodeData);

            // Assert
            _mockDependencyGraph.Verify(dg => dg.AddNode(nodeId), Times.Once);
            _mockCacheManager.Verify(cm => cm.InvalidateNode(nodeId), Times.Once);
        }

        [Fact]
        public void IncrementalNodeGraph_RemoveNode_WithExistingNode_RemovesSuccessfully()
        {
            // Arrange
            var graph = CreateTestGraph();
            var nodeId = "test-node-1";
            
            _mockDependencyGraph.Setup(dg => dg.ContainsNode(nodeId)).Returns(true);

            // Act
            graph.RemoveNode(nodeId);

            // Assert
            _mockDependencyGraph.Verify(dg => dg.RemoveNode(nodeId), Times.Once);
            _mockCacheManager.Verify(cm => cm.InvalidateNode(nodeId), Times.Once);
        }

        [Theory]
        [InlineData("node-a", "node-b")]
        [InlineData("input-node", "output-node")]
        [InlineData("source", "sink")]
        public void IncrementalNodeGraph_AddDependency_WithValidNodes_AddsDependency(
            string fromNode, string toNode)
        {
            // Arrange
            var graph = CreateTestGraph();
            
            _mockDependencyGraph.Setup(dg => dg.ContainsNode(fromNode)).Returns(true);
            _mockDependencyGraph.Setup(dg => dg.ContainsNode(toNode)).Returns(true);

            // Act
            graph.AddDependency(fromNode, toNode);

            // Assert
            _mockDependencyGraph.Verify(dg => dg.AddDependency(fromNode, toNode), Times.Once);
            _mockDirtyTracker.Verify(dt => dt.InvalidateDependentNodes(fromNode), Times.Once);
        }

        [Fact]
        public void IncrementalNodeGraph_AddDependency_WithNonExistentNodes_ThrowsArgumentException()
        {
            // Arrange
            var graph = CreateTestGraph();
            var fromNode = "non-existent-from";
            var toNode = "non-existent-to";
            
            _mockDependencyGraph.Setup(dg => dg.ContainsNode(fromNode)).Returns(false);
            _mockDependencyGraph.Setup(dg => dg.ContainsNode(toNode)).Returns(false);

            // Act & Assert
            Action act = () => graph.AddDependency(fromNode, toNode);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void IncrementalNodeGraph_RemoveDependency_WithExistingDependency_RemovesSuccessfully()
        {
            // Arrange
            var graph = CreateTestGraph();
            var fromNode = "test-from";
            var toNode = "test-to";
            
            _mockDependencyGraph.Setup(dg => dg.HasDependency(fromNode, toNode)).Returns(true);

            // Act
            graph.RemoveDependency(fromNode, toNode);

            // Assert
            _mockDependencyGraph.Verify(dg => dg.RemoveDependency(fromNode, toNode), Times.Once);
        }

        [Theory]
        [InlineData(100)]
        [InlineData(500)]
        [InlineData(1000)]
        public void IncrementalNodeGraph_UpdateNodeParameter_WithValidData_UpdatesSuccessfully(
            int nodeCount)
        {
            // Arrange
            var graph = CreateTestGraph();
            var nodeId = $"test-node-{nodeCount}";
            var parameterName = "TestParameter";
            var parameterValue = $"value-{nodeCount}";

            // Act
            graph.UpdateNodeParameter(nodeId, parameterName, parameterValue);

            // Assert
            _mockDirtyTracker.Verify(dt => dt.MarkNodeDirty(nodeId), Times.Once);
            _mockDirtyTracker.Verify(dt => dt.InvalidateDependentNodes(nodeId), Times.Once);
            _mockPerformanceMonitor.Verify(pm => pm.RecordParameterUpdate(nodeId, parameterName), Times.Once);
        }

        [Fact]
        public void IncrementalNodeGraph_UpdateNodeParameter_WithEmptyNodeId_ThrowsArgumentException()
        {
            // Arrange
            var graph = CreateTestGraph();
            var parameterName = "TestParameter";
            var parameterValue = "test-value";

            // Act & Assert
            Action act = () => graph.UpdateNodeParameter("", parameterName, parameterValue);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void IncrementalNodeGraph_UpdateNodeParameter_WithNullParameterName_ThrowsArgumentException()
        {
            // Arrange
            var graph = CreateTestGraph();
            var nodeId = "test-node";
            var parameterValue = "test-value";

            // Act & Assert
            Action act = () => graph.UpdateNodeParameter(nodeId, null!, parameterValue);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public async Task IncrementalNodeGraph_EvaluateDirtyNodes_WithDirtyNodes_EvaluatesOnlyDirty()
        {
            // Arrange
            var graph = CreateTestGraph();
            var dirtyNodes = new[] { "dirty-node-1", "dirty-node-2" };
            var evaluationResult = new EvaluationResult { Success = true, EvaluatedNodes = dirtyNodes };
            
            _mockDirtyTracker.Setup(dt => dt.GetDirtyNodes()).Returns(dirtyNodes);
            _mockEvaluator.Setup(e => e.EvaluateAsync(dirtyNodes, It.IsAny<CancellationToken>()))
                .ReturnsAsync(evaluationResult);

            // Act
            var result = await graph.EvaluateDirtyNodesAsync();

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.EvaluatedNodes.Should().Equal(dirtyNodes);
            _mockPerformanceMonitor.Verify(pm => pm.RecordEvaluationStart(), Times.Once);
            _mockPerformanceMonitor.Verify(pm => pm.RecordEvaluationComplete(dirtyNodes.Length, It.IsAny<TimeSpan>()), Times.Once);
        }

        [Fact]
        public async Task IncrementalNodeGraph_EvaluateDirtyNodes_WithNoDirtyNodes_ReturnsEmptyResult()
        {
            // Arrange
            var graph = CreateTestGraph();
            var emptyNodes = Array.Empty<string>();
            
            _mockDirtyTracker.Setup(dt => dt.GetDirtyNodes()).Returns(emptyNodes);

            // Act
            var result = await graph.EvaluateDirtyNodesAsync();

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.EvaluatedNodes.Should().BeEmpty();
        }

        [Fact]
        public async Task IncrementalNodeGraph_EvaluateDirtyNodes_WithCancellation_PropagatesCancellation()
        {
            // Arrange
            var graph = CreateTestGraph();
            var dirtyNodes = new[] { "test-node" };
            using var cts = new CancellationTokenSource();
            
            _mockDirtyTracker.Setup(dt => dt.GetDirtyNodes()).Returns(dirtyNodes);
            _mockEvaluator.Setup(e => e.EvaluateAsync(dirtyNodes, cts.Token))
                .ThrowsAsync(new OperationCanceledException());

            // Act & Assert
            await FluentActions.Awaiting(() => graph.EvaluateDirtyNodesAsync(cts.Token))
                .Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public void IncrementalNodeGraph_GetNodeCount_ReturnsCorrectCount()
        {
            // Arrange
            var graph = CreateTestGraph();
            var expectedCount = 5;
            _mockDependencyGraph.Setup(dg => dg.NodeCount).Returns(expectedCount);

            // Act
            var count = graph.NodeCount;

            // Assert
            count.Should().Be(expectedCount);
        }

        [Fact]
        public void IncrementalNodeGraph_GetDirtyNodeCount_ReturnsCorrectCount()
        {
            // Arrange
            var graph = CreateTestGraph();
            var expectedCount = 3;
            _mockDirtyTracker.Setup(dt => dt.DirtyNodeCount).Returns(expectedCount);

            // Act
            var count = graph.DirtyNodeCount;

            // Assert
            count.Should().Be(expectedCount);
        }

        [Fact]
        public void IncrementalNodeGraph_GetCacheStatistics_ReturnsValidStatistics()
        {
            // Arrange
            var graph = CreateTestGraph();
            var cacheStats = new CacheStatistics { HitRate = 0.85f, CacheSize = 1024 };
            
            _mockCacheManager.Setup(cm => cm.GetStatistics()).Returns(cacheStats);

            // Act
            var stats = graph.GetCacheStatistics();

            // Assert
            stats.Should().NotBeNull();
            stats.HitRate.Should().Be(0.85f);
            stats.CacheSize.Should().Be(1024);
        }

        [Fact]
        public void IncrementalNodeGraph_ClearCache_WithValidData_ClearsSuccessfully()
        {
            // Arrange
            var graph = CreateTestGraph();

            // Act
            graph.ClearCache();

            // Assert
            _mockCacheManager.Verify(cm => cm.Clear(), Times.Once);
        }

        [Fact]
        public void IncrementalNodeGraph_ResetDirtyFlags_WithValidData_ResetsSuccessfully()
        {
            // Arrange
            var graph = CreateTestGraph();

            // Act
            graph.ResetDirtyFlags();

            // Assert
            _mockDirtyTracker.Verify(dt => dt.ResetAll(), Times.Once);
        }

        [Fact]
        public void IncrementalNodeGraph_GetPerformanceMetrics_ReturnsValidMetrics()
        {
            // Arrange
            var graph = CreateTestGraph();
            var metrics = new PerformanceMetrics
            {
                TotalEvaluations = 100,
                AverageEvaluationTime = TimeSpan.FromMilliseconds(50),
                MemoryUsageMB = 256
            };
            
            _mockPerformanceMonitor.Setup(pm => pm.GetMetrics()).Returns(metrics);

            // Act
            var result = graph.GetPerformanceMetrics();

            // Assert
            result.Should().NotBeNull();
            result.TotalEvaluations.Should().Be(100);
            result.AverageEvaluationTime.Should().Be(TimeSpan.FromMilliseconds(50));
            result.MemoryUsageMB.Should().Be(256);
        }

        [Fact]
        public void IncrementalNodeGraph_Dispose_CompletesSuccessfully()
        {
            // Arrange
            var graph = CreateTestGraph();

            // Act & Assert
            var exception = Record.Exception(() => graph.Dispose());
            exception.Should().BeNull();
        }

        private IncrementalNodeGraph CreateTestGraph()
        {
            return new IncrementalNodeGraph(
                _mockContext.Object,
                _mockDependencyGraph.Object,
                _mockCacheManager.Object,
                _mockDirtyTracker.Object,
                _mockEvaluator.Object,
                _mockPerformanceMonitor.Object);
        }
    }

    /// <summary>
    /// Integration tests for IncrementalNodeGraph functionality
    /// </summary>
    [Category(TestCategories.Operators)]
    [Category(TestCategories.Integration)]
    public class IncrementalNodeGraphIntegrationTests : CoreTestFixture
    {
        private readonly ITestOutputHelper _output;
        private readonly ILogger<IncrementalNodeGraphIntegrationTests> _logger;

        public IncrementalNodeGraphIntegrationTests(ITestOutputHelper output) : base()
        {
            _output = output;
            _logger = ServiceProvider.GetRequiredService<ILogger<IncrementalNodeGraphIntegrationTests>>();
        }

        [Fact]
        public async Task IncrementalNodeGraph_EndToEndWorkflow_CompleteCycleSuccess()
        {
            // This test demonstrates a complete workflow with real implementations
            // Arrange
            var graph = CreateRealGraph();
            graph.AddNode("input-1", new { value = 42 });
            graph.AddNode("input-2", new { value = 84 });
            graph.AddNode("adder", new { operation = "add" });
            
            graph.AddDependency("input-1", "adder");
            graph.AddDependency("input-2", "adder");

            // Act - Update parameter and evaluate
            graph.UpdateNodeParameter("input-1", "value", 100);
            var result = await graph.EvaluateDirtyNodesAsync();

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            graph.DirtyNodeCount.Should().Be(0);
        }

        [Fact]
        public async Task IncrementalNodeGraph_ParallelEvaluations_HandlesCorrectly()
        {
            // Arrange
            var graph = CreateRealGraph();
            var tasks = new List<Task>();

            for (int i = 0; i < 5; i++)
            {
                var nodeId = $"parallel-node-{i}";
                graph.AddNode(nodeId, new { value = i });
                
                tasks.Add(Task.Run(async () =>
                {
                    graph.UpdateNodeParameter(nodeId, "value", i * 10);
                    await graph.EvaluateDirtyNodesAsync();
                }));
            }

            // Act
            await Task.WhenAll(tasks);

            // Assert
            graph.NodeCount.Should().Be(5);
            graph.DirtyNodeCount.Should().Be(0);
        }

        private IncrementalNodeGraph CreateRealGraph()
        {
            // This would use real implementations in a full integration test
            // For now, we'll create a mock-based version that demonstrates the workflow
            var mockContext = new Mock<IEvaluationContext>();
            var dependencyGraph = new DependencyGraph();
            var cacheManager = new CacheManager();
            var dirtyTracker = new DirtyTracker();
            var evaluator = new TopologicalEvaluator(mockContext.Object, cacheManager);
            var performanceMonitor = new PerformanceMonitor();

            return new IncrementalNodeGraph(
                mockContext.Object,
                dependencyGraph,
                cacheManager,
                dirtyTracker,
                evaluator,
                performanceMonitor);
        }
    }

    #region Helper Classes and Interfaces

    public interface IEvaluationContext
    {
        CancellationToken CancellationToken { get; }
    }

    public interface IDependencyGraph
    {
        void AddNode(string nodeId);
        void RemoveNode(string nodeId);
        void AddDependency(string fromNode, string toNode);
        void RemoveDependency(string fromNode, string toNode);
        bool ContainsNode(string nodeId);
        bool HasDependency(string fromNode, string toNode);
        int NodeCount { get; }
    }

    public interface ICacheManager
    {
        void InvalidateNode(string nodeId);
        CacheStatistics GetStatistics();
        void Clear();
    }

    public interface IDirtyTracker
    {
        void MarkNodeDirty(string nodeId);
        void InvalidateDependentNodes(string nodeId);
        void ResetAll();
        string[] GetDirtyNodes();
        int DirtyNodeCount { get; }
    }

    public interface ITopologicalEvaluator
    {
        Task<EvaluationResult> EvaluateAsync(string[] nodes, CancellationToken cancellationToken = default);
    }

    public interface IPerformanceMonitor
    {
        void RecordParameterUpdate(string nodeId, string parameterName);
        void RecordEvaluationStart();
        void RecordEvaluationComplete(int nodeCount, TimeSpan duration);
        PerformanceMetrics GetMetrics();
    }

    public class CacheStatistics
    {
        public float HitRate { get; set; }
        public int CacheSize { get; set; }
    }

    public class PerformanceMetrics
    {
        public int TotalEvaluations { get; set; }
        public TimeSpan AverageEvaluationTime { get; set; }
        public int MemoryUsageMB { get; set; }
    }

    public class EvaluationResult
    {
        public bool Success { get; set; }
        public string[] EvaluatedNodes { get; set; } = Array.Empty<string>();
    }

    #endregion
}