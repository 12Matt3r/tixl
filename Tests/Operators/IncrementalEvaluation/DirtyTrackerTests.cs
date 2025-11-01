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
    /// Unit tests for DirtyTracker
    /// </summary>
    [Category(TestCategories.Operators)]
    [Category(TestCategories.Unit)]
    public class DirtyTrackerTests : CoreTestFixture
    {
        private readonly ITestOutputHelper _output;
        private readonly ILogger<DirtyTrackerTests> _logger;

        public DirtyTrackerTests(ITestOutputHelper output) : base()
        {
            _output = output;
            _logger = ServiceProvider.GetRequiredService<ILogger<DirtyTrackerTests>>();
        }

        [Fact]
        public void DirtyTracker_CreateInstance_InitializesCorrectly()
        {
            // Arrange & Act
            var tracker = new DirtyTracker();

            // Assert
            tracker.Should().NotBeNull();
            tracker.DirtyNodeCount.Should().Be(0);
        }

        [Fact]
        public void DirtyTracker_MarkNodeDirty_WithValidNode_MarksDirty()
        {
            // Arrange
            var tracker = new DirtyTracker();
            var nodeId = "test-node";

            // Act
            tracker.MarkNodeDirty(nodeId);

            // Assert
            tracker.IsDirty(nodeId).Should().BeTrue();
            tracker.DirtyNodeCount.Should().Be(1);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void DirtyTracker_MarkNodeDirty_WithInvalidNodeId_ThrowsArgumentException(string nodeId)
        {
            // Arrange
            var tracker = new DirtyTracker();

            // Act & Assert
            Action act = () => tracker.MarkNodeDirty(nodeId);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void DirtyTracker_MarkNodeDirty_WithSameNodeMultipleTimes_HandlesCorrectly()
        {
            // Arrange
            var tracker = new DirtyTracker();
            var nodeId = "test-node";

            // Act
            tracker.MarkNodeDirty(nodeId);
            tracker.MarkNodeDirty(nodeId);
            tracker.MarkNodeDirty(nodeId);

            // Assert - Node should still be marked as dirty (count should not triple)
            tracker.IsDirty(nodeId).Should().BeTrue();
            tracker.DirtyNodeCount.Should().Be(1); // Should only count once per node
        }

        [Fact]
        public void DirtyTracker_IsDirty_WithDirtyNode_ReturnsTrue()
        {
            // Arrange
            var tracker = new DirtyTracker();
            var nodeId = "test-node";
            tracker.MarkNodeDirty(nodeId);

            // Act
            var isDirty = tracker.IsDirty(nodeId);

            // Assert
            isDirty.Should().BeTrue();
        }

        [Fact]
        public void DirtyTracker_IsDirty_WithCleanNode_ReturnsFalse()
        {
            // Arrange
            var tracker = new DirtyTracker();
            var nodeId = "test-node";

            // Act
            var isDirty = tracker.IsDirty(nodeId);

            // Assert
            isDirty.Should().BeFalse();
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void DirtyTracker_IsDirty_WithInvalidNodeId_ThrowsArgumentException(string nodeId)
        {
            // Arrange
            var tracker = new DirtyTracker();

            // Act & Assert
            Action act = () => tracker.IsDirty(nodeId);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void DirtyTracker_MarkNodeClean_WithDirtyNode_MarksClean()
        {
            // Arrange
            var tracker = new DirtyTracker();
            var nodeId = "test-node";
            tracker.MarkNodeDirty(nodeId);

            // Act
            tracker.MarkNodeClean(nodeId);

            // Assert
            tracker.IsDirty(nodeId).Should().BeFalse();
            tracker.DirtyNodeCount.Should().Be(0);
        }

        [Fact]
        public void DirtyTracker_MarkNodeClean_WithCleanNode_HandlesGracefully()
        {
            // Arrange
            var tracker = new DirtyTracker();
            var nodeId = "test-node";

            // Act
            tracker.MarkNodeClean(nodeId);

            // Assert - Should not throw and count should remain 0
            tracker.DirtyNodeCount.Should().Be(0);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void DirtyTracker_MarkNodeClean_WithInvalidNodeId_ThrowsArgumentException(string nodeId)
        {
            // Arrange
            var tracker = new DirtyTracker();

            // Act & Assert
            Action act = () => tracker.MarkNodeClean(nodeId);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void DirtyTracker_GetDirtyNodes_WithDirtyNodes_ReturnsCorrectNodes()
        {
            // Arrange
            var tracker = new DirtyTracker();
            var node1 = "dirty-node-1";
            var node2 = "dirty-node-2";
            var cleanNode = "clean-node";
            
            tracker.MarkNodeDirty(node1);
            tracker.MarkNodeDirty(node2);
            // cleanNode remains clean

            // Act
            var dirtyNodes = tracker.GetDirtyNodes();

            // Assert
            dirtyNodes.Should().HaveCount(2);
            dirtyNodes.Should().Contain(node1);
            dirtyNodes.Should().Contain(node2);
            dirtyNodes.Should().NotContain(cleanNode);
        }

        [Fact]
        public void DirtyTracker_GetDirtyNodes_WithNoDirtyNodes_ReturnsEmptyArray()
        {
            // Arrange
            var tracker = new DirtyTracker();

            // Act
            var dirtyNodes = tracker.GetDirtyNodes();

            // Assert
            dirtyNodes.Should().BeEmpty();
        }

        [Fact]
        public void DirtyTracker_InvalidateDependentNodes_WithValidNode_InvalidatesCorrectly()
        {
            // Arrange
            var tracker = new DirtyTracker();
            var sourceNode = "source";
            var dependent1 = "dependent1";
            var dependent2 = "dependent2";
            var cleanDependent = "clean-dependent";
            var unrelatedNode = "unrelated";
            
            // Set up dependency relationships (would typically be done by DependencyGraph)
            tracker.AddDependency(sourceNode, dependent1);
            tracker.AddDependency(sourceNode, dependent2);
            tracker.AddDependency(sourceNode, cleanDependent);
            tracker.AddDependency("other-source", unrelatedNode);

            // Act
            tracker.InvalidateDependentNodes(sourceNode);

            // Assert
            tracker.IsDirty(dependent1).Should().BeTrue();
            tracker.IsDirty(dependent2).Should().BeTrue();
            tracker.IsDirty(cleanDependent).Should().BeTrue();
            tracker.IsDirty(unrelatedNode).Should().BeFalse(); // Should remain clean
            tracker.DirtyNodeCount.Should().Be(3);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void DirtyTracker_InvalidateDependentNodes_WithInvalidNodeId_ThrowsArgumentException(string nodeId)
        {
            // Arrange
            var tracker = new DirtyTracker();

            // Act & Assert
            Action act = () => tracker.InvalidateDependentNodes(nodeId);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void DirtyTracker_InvalidateDependentNodes_WithNonExistentNode_HandlesGracefully()
        {
            // Arrange
            var tracker = new DirtyTracker();
            var nodeId = "non-existent-node";

            // Act & Assert
            Action act = () => tracker.InvalidateDependentNodes(nodeId);
            act.Should().NotThrow();
        }

        [Fact]
        public void DirtyTracker_AddDependency_WithValidNodes_AddsSuccessfully()
        {
            // Arrange
            var tracker = new DirtyTracker();
            var fromNode = "source-node";
            var toNode = "dependent-node";

            // Act
            tracker.AddDependency(fromNode, toNode);

            // Assert - Dependency tracking should be set up
            // This would typically be verified through invalidation behavior
            tracker.IsDirty(toNode).Should().BeFalse(); // Initially clean
        }

        [Fact]
        public void DirtyTracker_AddDependency_WithCycle_DetectsAndHandles()
        {
            // Arrange
            var tracker = new DirtyTracker();
            var nodeA = "node-a";
            var nodeB = "node-b";
            var nodeC = "node-c";

            // Act - Add dependencies that create a cycle A -> B -> C -> A
            tracker.AddDependency(nodeA, nodeB);
            tracker.AddDependency(nodeB, nodeC);

            // Assert - Adding the cycle-completing dependency should be detected
            Action act = () => tracker.AddDependency(nodeC, nodeA);
            act.Should().Throw<InvalidOperationException>(); // Should detect cycle
        }

        [Theory]
        [InlineData("", "node-b")]
        [InlineData("node-a", "")]
        [InlineData(null, "node-b")]
        [InlineData("node-a", null)]
        public void DirtyTracker_AddDependency_WithInvalidNodeIds_ThrowsArgumentException(
            string fromNode, string toNode)
        {
            // Arrange
            var tracker = new DirtyTracker();

            // Act & Assert
            Action act = () => tracker.AddDependency(fromNode, toNode);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void DirtyTracker_RemoveDependency_WithExistingDependency_RemovesSuccessfully()
        {
            // Arrange
            var tracker = new DirtyTracker();
            var fromNode = "source";
            var toNode = "dependent";
            
            tracker.AddDependency(fromNode, toNode);

            // Act
            tracker.RemoveDependency(fromNode, toNode);

            // Assert - Dependency should be removed
            // Invalidation of fromNode should no longer affect toNode
            tracker.InvalidateDependentNodes(fromNode);
            tracker.IsDirty(toNode).Should().BeFalse();
        }

        [Theory]
        [InlineData("", "node-b")]
        [InlineData("node-a", "")]
        [InlineData(null, "node-b")]
        [InlineData("node-a", null)]
        public void DirtyTracker_RemoveDependency_WithInvalidNodeIds_ThrowsArgumentException(
            string fromNode, string toNode)
        {
            // Arrange
            var tracker = new DirtyTracker();

            // Act & Assert
            Action act = () => tracker.RemoveDependency(fromNode, toNode);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void DirtyTracker_RemoveDependency_WithNonExistentDependency_HandlesGracefully()
        {
            // Arrange
            var tracker = new DirtyTracker();
            var fromNode = "source";
            var toNode = "dependent";

            // Act & Assert
            Action act = () => tracker.RemoveDependency(fromNode, toNode);
            act.Should().NotThrow();
        }

        [Fact]
        public void DirtyTracker_Reset_WithDirtyNodes_ClearsAllDirtyFlags()
        {
            // Arrange
            var tracker = new DirtyTracker();
            var node1 = "dirty-node-1";
            var node2 = "dirty-node-2";
            var node3 = "dirty-node-3";
            
            tracker.MarkNodeDirty(node1);
            tracker.MarkNodeDirty(node2);
            tracker.MarkNodeDirty(node3);

            // Act
            tracker.Reset();

            // Assert
            tracker.DirtyNodeCount.Should().Be(0);
            tracker.IsDirty(node1).Should().BeFalse();
            tracker.IsDirty(node2).Should().BeFalse();
            tracker.IsDirty(node3).Should().BeFalse();
        }

        [Fact]
        public void DirtyTracker_Reset_WithCleanNodes_HandlesGracefully()
        {
            // Arrange
            var tracker = new DirtyTracker();

            // Act
            tracker.Reset();

            // Assert
            tracker.DirtyNodeCount.Should().Be(0);
        }

        [Fact]
        public void DirtyTracker_BatchMarkDirty_WithValidNodes_MarksAllDirty()
        {
            // Arrange
            var tracker = new DirtyTracker();
            var nodes = new[] { "node1", "node2", "node3", "node4" };

            // Act
            tracker.BatchMarkDirty(nodes);

            // Assert
            foreach (var node in nodes)
            {
                tracker.IsDirty(node).Should().BeTrue();
            }
            tracker.DirtyNodeCount.Should().Be(4);
        }

        [Fact]
        public void DirtyTracker_BatchMarkDirty_WithEmptyArray_HandlesGracefully()
        {
            // Arrange
            var tracker = new DirtyTracker();

            // Act
            tracker.BatchMarkDirty(Array.Empty<string>());

            // Assert
            tracker.DirtyNodeCount.Should().Be(0);
        }

        [Fact]
        public void DirtyTracker_BatchMarkDirty_WithNullArray_ThrowsArgumentException()
        {
            // Arrange
            var tracker = new DirtyTracker();

            // Act & Assert
            Action act = () => tracker.BatchMarkDirty(null!);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void DirtyTracker_BatchMarkClean_WithValidNodes_MarksAllClean()
        {
            // Arrange
            var tracker = new DirtyTracker();
            var node1 = "node1";
            var node2 = "node2";
            var node3 = "node3";
            
            tracker.MarkNodeDirty(node1);
            tracker.MarkNodeDirty(node2);
            tracker.MarkNodeDirty(node3);
            
            var nodesToClean = new[] { node1, node2 };

            // Act
            tracker.BatchMarkClean(nodesToClean);

            // Assert
            tracker.IsDirty(node1).Should().BeFalse();
            tracker.IsDirty(node2).Should().BeFalse();
            tracker.IsDirty(node3).Should().BeTrue(); // Should remain dirty
            tracker.DirtyNodeCount.Should().Be(1);
        }

        [Fact]
        public void DirtyTracker_BatchMarkClean_WithMixedCleanAndDirtyNodes_HandlesCorrectly()
        {
            // Arrange
            var tracker = new DirtyTracker();
            var node1 = "node1";
            var node2 = "node2"; // This one will remain dirty
            
            tracker.MarkNodeDirty(node1);
            tracker.MarkNodeDirty(node2);
            
            var nodesToClean = new[] { node1, "non-existent-node" }; // Mix of existing and non-existing

            // Act
            tracker.BatchMarkClean(nodesToClean);

            // Assert
            tracker.IsDirty(node1).Should().BeFalse();
            tracker.IsDirty(node2).Should().BeTrue();
            tracker.DirtyNodeCount.Should().Be(1);
        }

        [Fact]
        public void DirtyTracker_ComplexDependencyPropagation_WithMultipleLevels_CorrectlyPropagates()
        {
            // Arrange
            var tracker = new DirtyTracker();
            
            // Create multi-level dependency: A -> B -> C -> D
            //                               E -> C
            var nodeA = "node-a";
            var nodeB = "node-b";
            var nodeC = "node-c";
            var nodeD = "node-d";
            var nodeE = "node-e";
            
            tracker.AddDependency(nodeA, nodeB);
            tracker.AddDependency(nodeB, nodeC);
            tracker.AddDependency(nodeC, nodeD);
            tracker.AddDependency(nodeE, nodeC);

            // Act - Mark A dirty, should propagate to B, C, D
            tracker.InvalidateDependentNodes(nodeA);

            // Assert
            tracker.IsDirty(nodeA).Should().BeFalse(); // A itself should not be affected by invalidating its dependents
            tracker.IsDirty(nodeB).Should().BeTrue();
            tracker.IsDirty(nodeC).Should().BeTrue();
            tracker.IsDirty(nodeD).Should().BeTrue();
            tracker.IsDirty(nodeE).Should().BeFalse(); // E is unrelated
            tracker.DirtyNodeCount.Should().Be(3);
        }

        [Fact]
        public void DirtyTracker_ParallelInvalidation_ThreadSafeOperations_Succeeds()
        {
            // Arrange
            var tracker = new DirtyTracker();
            var tasks = new List<Task>();
            var nodeCount = 20;

            // Act - Perform concurrent operations
            for (int i = 0; i < nodeCount; i++)
            {
                var nodeIndex = i;
                tasks.Add(Task.Run(() =>
                {
                    var nodeId = $"node-{nodeIndex}";
                    tracker.MarkNodeDirty(nodeId);
                    
                    // Some nodes depend on others
                    if (nodeIndex > 0)
                    {
                        tracker.AddDependency($"node-{nodeIndex - 1}", nodeId);
                    }
                }));
            }

            // Perform concurrent invalidation
            tasks.Add(Task.Run(() =>
            {
                for (int i = 0; i < nodeCount / 2; i++)
                {
                    tracker.InvalidateDependentNodes($"node-{i}");
                }
            }));

            await Task.WhenAll(tasks);

            // Assert
            tracker.DirtyNodeCount.Should().BeGreaterThan(0);
            tracker.DirtyNodeCount.Should().BeLessThanOrEqualTo(nodeCount);
        }

        [Fact]
        public void DirtyTracker_Performance_WithLargeNodeCount_HandlesEfficiently()
        {
            // Arrange
            var tracker = new DirtyTracker();
            var nodeCount = 1000;

            // Act - Mark many nodes dirty
            for (int i = 0; i < nodeCount; i++)
            {
                tracker.MarkNodeDirty($"node-{i}");
            }

            // Act - Clean all nodes
            tracker.Reset();

            // Assert
            tracker.DirtyNodeCount.Should().Be(0);
            
            // Performance test - should complete quickly
            var startTime = DateTime.UtcNow;
            for (int i = 0; i < 100; i++)
            {
                tracker.MarkNodeDirty($"perf-node-{i % 50}");
            }
            var endTime = DateTime.UtcNow;
            var duration = endTime - startTime;
            
            duration.Should().BeLessThan(TimeSpan.FromSeconds(1)); // Should be very fast
        }

        [Fact]
        public void DirtyTracker_StateConsistency_AfterMixedOperations_RemainsConsistent()
        {
            // Arrange
            var tracker = new DirtyTracker();
            var node1 = "node-1";
            var node2 = "node-2";
            var node3 = "node-3";

            // Act - Perform mixed operations
            tracker.MarkNodeDirty(node1);
            tracker.MarkNodeClean(node2); // Clean node that was never dirty
            tracker.MarkNodeDirty(node1); // Mark already dirty node
            tracker.AddDependency(node1, node2);
            tracker.AddDependency(node2, node3);
            
            tracker.InvalidateDependentNodes(node1);
            tracker.BatchMarkClean(new[] { node1, node3 });
            
            // Mark some more
            tracker.BatchMarkDirty(new[] { node2, node3 });
            tracker.Reset();

            // Assert - Final state should be clean
            tracker.DirtyNodeCount.Should().Be(0);
            tracker.IsDirty(node1).Should().BeFalse();
            tracker.IsDirty(node2).Should().BeFalse();
            tracker.IsDirty(node3).Should().BeFalse();
        }

        [Fact]
        public void DirtyTracker_StatisticsTracking_ProvidesAccurateCounts()
        {
            // Arrange
            var tracker = new DirtyTracker();
            var node1 = "node-1";
            var node2 = "node-2";

            // Act - Perform various operations and track stats
            tracker.MarkNodeDirty(node1);
            var countAfterFirst = tracker.DirtyNodeCount;
            
            tracker.MarkNodeDirty(node2);
            var countAfterSecond = tracker.DirtyNodeCount;
            
            tracker.MarkNodeClean(node1);
            var countAfterFirstClean = tracker.DirtyNodeCount;
            
            tracker.MarkNodeClean(node2);
            var countAfterSecondClean = tracker.DirtyNodeCount;

            // Assert
            countAfterFirst.Should().Be(1);
            countAfterSecond.Should().Be(2);
            countAfterFirstClean.Should().Be(1);
            countAfterSecondClean.Should().Be(0);
        }
    }
}