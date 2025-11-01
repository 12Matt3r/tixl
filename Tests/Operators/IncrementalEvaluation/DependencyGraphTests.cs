using Xunit;
using Xunit.Abstractions;
using TiXL.Tests.Categories;
using TiXL.Tests.Fixtures;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using T3.Core.Operators.IncrementalEvaluation;
using Microsoft.Extensions.Logging;

namespace TiXL.Tests.Operators.IncrementalEvaluation
{
    /// <summary>
    /// Unit tests for DependencyGraph
    /// </summary>
    [Category(TestCategories.Operators)]
    [Category(TestCategories.Unit)]
    public class DependencyGraphTests : CoreTestFixture
    {
        private readonly ITestOutputHelper _output;
        private readonly ILogger<DependencyGraphTests> _logger;

        public DependencyGraphTests(ITestOutputHelper output) : base()
        {
            _output = output;
            _logger = ServiceProvider.GetRequiredService<ILogger<DependencyGraphTests>>();
        }

        [Fact]
        public void DependencyGraph_CreateInstance_InitializesCorrectly()
        {
            // Arrange & Act
            var graph = new DependencyGraph();

            // Assert
            graph.Should().NotBeNull();
            graph.NodeCount.Should().Be(0);
        }

        [Fact]
        public void DependencyGraph_AddNode_WithValidNodeId_AddsSuccessfully()
        {
            // Arrange
            var graph = new DependencyGraph();
            var nodeId = "test-node-1";

            // Act
            graph.AddNode(nodeId);

            // Assert
            graph.NodeCount.Should().Be(1);
            graph.ContainsNode(nodeId).Should().BeTrue();
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void DependencyGraph_AddNode_WithInvalidNodeId_ThrowsArgumentException(string nodeId)
        {
            // Arrange
            var graph = new DependencyGraph();

            // Act & Assert
            Action act = () => graph.AddNode(nodeId);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void DependencyGraph_AddNode_WithDuplicateId_ThrowsArgumentException()
        {
            // Arrange
            var graph = new DependencyGraph();
            var nodeId = "duplicate-node";
            graph.AddNode(nodeId);

            // Act & Assert
            Action act = () => graph.AddNode(nodeId);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void DependencyGraph_RemoveNode_WithExistingNode_RemovesSuccessfully()
        {
            // Arrange
            var graph = new DependencyGraph();
            var nodeId = "test-node-1";
            graph.AddNode(nodeId);

            // Act
            graph.RemoveNode(nodeId);

            // Assert
            graph.NodeCount.Should().Be(0);
            graph.ContainsNode(nodeId).Should().BeFalse();
        }

        [Fact]
        public void DependencyGraph_RemoveNode_WithNonExistentNode_ThrowsArgumentException()
        {
            // Arrange
            var graph = new DependencyGraph();
            var nodeId = "non-existent-node";

            // Act & Assert
            Action act = () => graph.RemoveNode(nodeId);
            act.Should().Throw<ArgumentException>();
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void DependencyGraph_RemoveNode_WithInvalidNodeId_ThrowsArgumentException(string nodeId)
        {
            // Arrange
            var graph = new DependencyGraph();

            // Act & Assert
            Action act = () => graph.RemoveNode(nodeId);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void DependencyGraph_AddDependency_WithValidNodes_AddsSuccessfully()
        {
            // Arrange
            var graph = new DependencyGraph();
            var fromNode = "node-a";
            var toNode = "node-b";
            
            graph.AddNode(fromNode);
            graph.AddNode(toNode);

            // Act
            graph.AddDependency(fromNode, toNode);

            // Assert
            graph.HasDependency(fromNode, toNode).Should().BeTrue();
            graph.GetDependents(toNode).Should().Contain(fromNode);
            graph.GetDependencies(fromNode).Should().Contain(toNode);
        }

        [Theory]
        [InlineData("", "node-b")]
        [InlineData("node-a", "")]
        [InlineData(null, "node-b")]
        [InlineData("node-a", null)]
        public void DependencyGraph_AddDependency_WithInvalidNodeIds_ThrowsArgumentException(
            string fromNode, string toNode)
        {
            // Arrange
            var graph = new DependencyGraph();

            // Act & Assert
            Action act = () => graph.AddDependency(fromNode, toNode);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void DependencyGraph_AddDependency_WithNonExistentFromNode_ThrowsArgumentException()
        {
            // Arrange
            var graph = new DependencyGraph();
            var fromNode = "non-existent-from";
            var toNode = "existing-to";
            
            graph.AddNode(toNode);

            // Act & Assert
            Action act = () => graph.AddDependency(fromNode, toNode);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void DependencyGraph_AddDependency_WithNonExistentToNode_ThrowsArgumentException()
        {
            // Arrange
            var graph = new DependencyGraph();
            var fromNode = "existing-from";
            var toNode = "non-existent-to";
            
            graph.AddNode(fromNode);

            // Act & Assert
            Action act = () => graph.AddDependency(fromNode, toNode);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void DependencyGraph_AddDependency_WithSelfDependency_ThrowsArgumentException()
        {
            // Arrange
            var graph = new DependencyGraph();
            var nodeId = "self-dependent-node";
            
            graph.AddNode(nodeId);

            // Act & Assert
            Action act = () => graph.AddDependency(nodeId, nodeId);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void DependencyGraph_AddDependency_WithDuplicateDependency_ThrowsArgumentException()
        {
            // Arrange
            var graph = new DependencyGraph();
            var fromNode = "node-a";
            var toNode = "node-b";
            
            graph.AddNode(fromNode);
            graph.AddNode(toNode);
            graph.AddDependency(fromNode, toNode);

            // Act & Assert
            Action act = () => graph.AddDependency(fromNode, toNode);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void DependencyGraph_AddDependency_WithCycle_ThrowsArgumentException()
        {
            // Arrange
            var graph = new DependencyGraph();
            var nodeA = "node-a";
            var nodeB = "node-b";
            var nodeC = "node-c";
            
            graph.AddNode(nodeA);
            graph.AddNode(nodeB);
            graph.AddNode(nodeC);
            
            graph.AddDependency(nodeA, nodeB);
            graph.AddDependency(nodeB, nodeC);

            // Act & Assert - Adding dependency C -> A would create a cycle A -> B -> C -> A
            Action act = () => graph.AddDependency(nodeC, nodeA);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void DependencyGraph_RemoveDependency_WithExistingDependency_RemovesSuccessfully()
        {
            // Arrange
            var graph = new DependencyGraph();
            var fromNode = "node-a";
            var toNode = "node-b";
            
            graph.AddNode(fromNode);
            graph.AddNode(toNode);
            graph.AddDependency(fromNode, toNode);

            // Act
            graph.RemoveDependency(fromNode, toNode);

            // Assert
            graph.HasDependency(fromNode, toNode).Should().BeFalse();
            graph.GetDependents(toNode).Should().NotContain(fromNode);
            graph.GetDependencies(fromNode).Should().NotContain(toNode);
        }

        [Fact]
        public void DependencyGraph_RemoveDependency_WithNonExistentDependency_ThrowsArgumentException()
        {
            // Arrange
            var graph = new DependencyGraph();
            var fromNode = "node-a";
            var toNode = "node-b";
            
            graph.AddNode(fromNode);
            graph.AddNode(toNode);

            // Act & Assert
            Action act = () => graph.RemoveDependency(fromNode, toNode);
            act.Should().Throw<ArgumentException>();
        }

        [Theory]
        [InlineData("", "node-b")]
        [InlineData("node-a", "")]
        [InlineData(null, "node-b")]
        [InlineData("node-a", null)]
        public void DependencyGraph_RemoveDependency_WithInvalidNodeIds_ThrowsArgumentException(
            string fromNode, string toNode)
        {
            // Arrange
            var graph = new DependencyGraph();

            // Act & Assert
            Action act = () => graph.RemoveDependency(fromNode, toNode);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void DependencyGraph_ContainsNode_WithExistingNode_ReturnsTrue()
        {
            // Arrange
            var graph = new DependencyGraph();
            var nodeId = "test-node";
            
            graph.AddNode(nodeId);

            // Act
            var result = graph.ContainsNode(nodeId);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void DependencyGraph_ContainsNode_WithNonExistentNode_ReturnsFalse()
        {
            // Arrange
            var graph = new DependencyGraph();
            var nodeId = "non-existent-node";

            // Act
            var result = graph.ContainsNode(nodeId);

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData("", true)]
        [InlineData(null, true)]
        public void DependencyGraph_ContainsNode_WithInvalidNodeId_ReturnsFalse(string nodeId, bool shouldThrow)
        {
            // Arrange
            var graph = new DependencyGraph();

            // Act & Assert
            Action act = () => graph.ContainsNode(nodeId);
            if (shouldThrow)
                act.Should().Throw<ArgumentException>();
            else
                act.Should().NotThrow();
        }

        [Fact]
        public void DependencyGraph_HasDependency_WithExistingDependency_ReturnsTrue()
        {
            // Arrange
            var graph = new DependencyGraph();
            var fromNode = "node-a";
            var toNode = "node-b";
            
            graph.AddNode(fromNode);
            graph.AddNode(toNode);
            graph.AddDependency(fromNode, toNode);

            // Act
            var result = graph.HasDependency(fromNode, toNode);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void DependencyGraph_HasDependency_WithNonExistentDependency_ReturnsFalse()
        {
            // Arrange
            var graph = new DependencyGraph();
            var fromNode = "node-a";
            var toNode = "node-b";
            
            graph.AddNode(fromNode);
            graph.AddNode(toNode);

            // Act
            var result = graph.HasDependency(fromNode, toNode);

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData("", "node-b")]
        [InlineData("node-a", "")]
        [InlineData(null, "node-b")]
        [InlineData("node-a", null)]
        public void DependencyGraph_HasDependency_WithInvalidNodeIds_ThrowsArgumentException(
            string fromNode, string toNode)
        {
            // Arrange
            var graph = new DependencyGraph();

            // Act & Assert
            Action act = () => graph.HasDependency(fromNode, toNode);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void DependencyGraph_GetDependencies_WithExistingNode_ReturnsDependencies()
        {
            // Arrange
            var graph = new DependencyGraph();
            var nodeA = "node-a";
            var nodeB = "node-b";
            var nodeC = "node-c";
            
            graph.AddNode(nodeA);
            graph.AddNode(nodeB);
            graph.AddNode(nodeC);
            
            graph.AddDependency(nodeA, nodeB);
            graph.AddDependency(nodeA, nodeC);

            // Act
            var dependencies = graph.GetDependencies(nodeA);

            // Assert
            dependencies.Should().HaveCount(2);
            dependencies.Should().Contain(nodeB);
            dependencies.Should().Contain(nodeC);
        }

        [Fact]
        public void DependencyGraph_GetDependencies_WithNodeWithoutDependencies_ReturnsEmpty()
        {
            // Arrange
            var graph = new DependencyGraph();
            var nodeId = "isolated-node";
            
            graph.AddNode(nodeId);

            // Act
            var dependencies = graph.GetDependencies(nodeId);

            // Assert
            dependencies.Should().BeEmpty();
        }

        [Fact]
        public void DependencyGraph_GetDependencies_WithNonExistentNode_ThrowsArgumentException()
        {
            // Arrange
            var graph = new DependencyGraph();
            var nodeId = "non-existent-node";

            // Act & Assert
            Action act = () => graph.GetDependencies(nodeId);
            act.Should().Throw<ArgumentException>();
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void DependencyGraph_GetDependencies_WithInvalidNodeId_ThrowsArgumentException(string nodeId)
        {
            // Arrange
            var graph = new DependencyGraph();

            // Act & Assert
            Action act = () => graph.GetDependencies(nodeId);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void DependencyGraph_GetDependents_WithExistingNode_ReturnsDependents()
        {
            // Arrange
            var graph = new DependencyGraph();
            var nodeA = "node-a";
            var nodeB = "node-b";
            var nodeC = "node-c";
            
            graph.AddNode(nodeA);
            graph.AddNode(nodeB);
            graph.AddNode(nodeC);
            
            graph.AddDependency(nodeB, nodeA);
            graph.AddDependency(nodeC, nodeA);

            // Act
            var dependents = graph.GetDependents(nodeA);

            // Assert
            dependents.Should().HaveCount(2);
            dependents.Should().Contain(nodeB);
            dependents.Should().Contain(nodeC);
        }

        [Fact]
        public void DependencyGraph_GetDependents_WithNodeWithoutDependents_ReturnsEmpty()
        {
            // Arrange
            var graph = new DependencyGraph();
            var nodeId = "source-node";
            
            graph.AddNode(nodeId);

            // Act
            var dependents = graph.GetDependents(nodeId);

            // Assert
            dependents.Should().BeEmpty();
        }

        [Fact]
        public void DependencyGraph_GetDependents_WithNonExistentNode_ThrowsArgumentException()
        {
            // Arrange
            var graph = new DependencyGraph();
            var nodeId = "non-existent-node";

            // Act & Assert
            Action act = () => graph.GetDependents(nodeId);
            act.Should().Throw<ArgumentException>();
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void DependencyGraph_GetDependents_WithInvalidNodeId_ThrowsArgumentException(string nodeId)
        {
            // Arrange
            var graph = new DependencyGraph();

            // Act & Assert
            Action act = () => graph.GetDependents(nodeId);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void DependencyGraph_GetTopologicalOrder_WithValidGraph_ReturnsValidOrder()
        {
            // Arrange
            var graph = new DependencyGraph();
            var nodeA = "node-a";
            var nodeB = "node-b";
            var nodeC = "node-c";
            
            graph.AddNode(nodeA);
            graph.AddNode(nodeB);
            graph.AddNode(nodeC);
            
            // A -> B -> C
            graph.AddDependency(nodeA, nodeB);
            graph.AddDependency(nodeB, nodeC);

            // Act
            var order = graph.GetTopologicalOrder();

            // Assert
            order.Should().HaveCount(3);
            var indexA = Array.IndexOf(order, nodeA);
            var indexB = Array.IndexOf(order, nodeB);
            var indexC = Array.IndexOf(order, nodeC);
            
            indexA.Should().BeLessThan(indexB);
            indexB.Should().BeLessThan(indexC);
        }

        [Fact]
        public void DependencyGraph_GetTopologicalOrder_WithEmptyGraph_ReturnsEmptyArray()
        {
            // Arrange
            var graph = new DependencyGraph();

            // Act
            var order = graph.GetTopologicalOrder();

            // Assert
            order.Should().BeEmpty();
        }

        [Fact]
        public void DependencyGraph_GetTopologicalOrder_WithCycle_ThrowsInvalidOperationException()
        {
            // Arrange
            var graph = new DependencyGraph();
            var nodeA = "node-a";
            var nodeB = "node-b";
            var nodeC = "node-c";
            
            graph.AddNode(nodeA);
            graph.AddNode(nodeB);
            graph.AddNode(nodeC);
            
            // A -> B -> C -> A (cycle)
            graph.AddDependency(nodeA, nodeB);
            graph.AddDependency(nodeB, nodeC);
            graph.AddDependency(nodeC, nodeA);

            // Act & Assert
            Action act = () => graph.GetTopologicalOrder();
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void DependencyGraph_GetAffectedNodes_WithExistingNode_ReturnsCorrectAffectedNodes()
        {
            // Arrange
            var graph = new DependencyGraph();
            var nodeA = "node-a";
            var nodeB = "node-b";
            var nodeC = "node-c";
            var nodeD = "node-d";
            
            graph.AddNode(nodeA);
            graph.AddNode(nodeB);
            graph.AddNode(nodeC);
            graph.AddNode(nodeD);
            
            // A -> B -> C
            //     -> D
            graph.AddDependency(nodeA, nodeB);
            graph.AddDependency(nodeB, nodeC);
            graph.AddDependency(nodeB, nodeD);

            // Act
            var affected = graph.GetAffectedNodes(nodeA);

            // Assert
            affected.Should().HaveCount(3);
            affected.Should().Contain(nodeB);
            affected.Should().Contain(nodeC);
            affected.Should().Contain(nodeD);
        }

        [Fact]
        public void DependencyGraph_GetAffectedNodes_WithIsolatedNode_ReturnsOnlySelf()
        {
            // Arrange
            var graph = new DependencyGraph();
            var nodeId = "isolated-node";
            
            graph.AddNode(nodeId);

            // Act
            var affected = graph.GetAffectedNodes(nodeId);

            // Assert
            affected.Should().HaveCount(1);
            affected.Should().Contain(nodeId);
        }

        [Fact]
        public void DependencyGraph_GetAffectedNodes_WithNonExistentNode_ThrowsArgumentException()
        {
            // Arrange
            var graph = new DependencyGraph();
            var nodeId = "non-existent-node";

            // Act & Assert
            Action act = () => graph.GetAffectedNodes(nodeId);
            act.Should().Throw<ArgumentException>();
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void DependencyGraph_GetAffectedNodes_WithInvalidNodeId_ThrowsArgumentException(string nodeId)
        {
            // Arrange
            var graph = new DependencyGraph();

            // Act & Assert
            Action act = () => graph.GetAffectedNodes(nodeId);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void DependencyGraph_ComplexWorkflow_MultipleNodesAndDependencies_Succeeds()
        {
            // Arrange
            var graph = new DependencyGraph();
            
            // Create a complex graph: Input1 -> Process1 -> Process3
            //                        Input2 -> Process2 ->
            //                        Input3 -> Process4 ->
            var input1 = "input1";
            var input2 = "input2";
            var input3 = "input3";
            var process1 = "process1";
            var process2 = "process2";
            var process3 = "process3";
            var process4 = "process4";
            
            // Add all nodes
            var nodes = new[] { input1, input2, input3, process1, process2, process3, process4 };
            foreach (var node in nodes)
            {
                graph.AddNode(node);
            }
            
            // Add dependencies
            graph.AddDependency(input1, process1);
            graph.AddDependency(input2, process2);
            graph.AddDependency(input3, process4);
            graph.AddDependency(process1, process3);
            graph.AddDependency(process2, process3);
            graph.AddDependency(process4, process3);

            // Act & Assert - All operations should succeed
            graph.NodeCount.Should().Be(7);
            graph.HasDependency(input1, process1).Should().BeTrue();
            graph.HasDependency(process1, process3).Should().BeTrue();
            
            var affected = graph.GetAffectedNodes(input1);
            affected.Should().Contain(process1);
            affected.Should().Contain(process3);
            
            var order = graph.GetTopologicalOrder();
            order.Should().HaveCount(7);
        }

        [Fact]
        public void DependencyGraph_MemoryUsage_ScalableWithNodeCount()
        {
            // Arrange
            var graph = new DependencyGraph();
            var nodeCount = 1000;

            // Act - Add many nodes
            for (int i = 0; i < nodeCount; i++)
            {
                graph.AddNode($"node-{i}");
            }

            // Add some dependencies
            for (int i = 0; i < 500; i++)
            {
                graph.AddDependency($"node-{i}", $"node-{i + 500}");
            }

            // Assert
            graph.NodeCount.Should().Be(nodeCount);
            graph.HasDependency("node-0", "node-500").Should().BeTrue();
            graph.HasDependency("node-499", "node-999").Should().BeTrue();
        }

        [Fact]
        public void DependencyGraph_ThreadSafety_ConcurrentOperations_Succeeds()
        {
            // Arrange
            var graph = new DependencyGraph();
            var tasks = new List<Task>();

            // Act - Add nodes concurrently
            for (int i = 0; i < 10; i++)
            {
                var nodeIndex = i;
                tasks.Add(Task.Run(() =>
                {
                    graph.AddNode($"node-{nodeIndex}");
                    
                    if (nodeIndex > 0)
                    {
                        graph.AddDependency($"node-{nodeIndex - 1}", $"node-{nodeIndex}");
                    }
                }));
            }

            // Wait for all operations to complete
            Task.WaitAll(tasks.ToArray());

            // Assert
            graph.NodeCount.Should().Be(10);
            for (int i = 1; i < 10; i++)
            {
                graph.HasDependency($"node-{i - 1}", $"node-{i}").Should().BeTrue();
            }
        }
    }
}