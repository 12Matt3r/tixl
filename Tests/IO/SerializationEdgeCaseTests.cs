using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TiXL.Core.IO;
using Xunit;
using FluentAssertions;

namespace TiXL.Tests.IO
{
    /// <summary>
    /// Edge case and stress tests for TiXL serialization system.
    /// Tests boundary conditions, extreme scenarios, and system resilience.
    /// Complements the comprehensive round-trip tests in RoundTripSerializationTests.cs
    /// </summary>
    public class SerializationEdgeCaseTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly Random _random;

        public SerializationEdgeCaseTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "TiXL_EdgeCaseTests_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_testDirectory);
            _random = new Random(12345); // Different seed from main tests
        }

        #region Boundary Conditions

        [Fact]
        public async Task ExtremeProjectNames_ShouldHandleBoundaryValues()
        {
            // Test boundary cases for project names
            var boundaryNames = new[]
            {
                "", // Empty name
                new string('A', 1), // Minimum length
                new string('A', 255), // Common maximum
                new string('A', 1000), // Extended length
                "project with spaces and symbols!@#$%^&*()",
                "中文项目名称", // Unicode characters
                "Проект на русском", // Cyrillic
                "مشروع باللغة العربية", // Arabic
                "🚀🎨✨Special Characters🎯🔥💫", // Emojis
                "\t\n\r", // Control characters
                "Project\\With\\Backslashes", // Path-like
                "Project/With/Slashes", // Forward slashes
                "Project.With.Dots", // Multiple dots
                "Project-With-Dashes", // Multiple dashes
                "Project_With_Underscores", // Underscores
                "Project With 100 Special !@#$%^&*() Characters 1234567890"
            };

            foreach (var name in boundaryNames)
            {
                // Arrange
                var metadata = new ProjectMetadata
                {
                    Name = name,
                    Id = Guid.NewGuid().ToString(),
                    Version = "1.0.0",
                    CreatedUtc = DateTime.UtcNow,
                    LastModifiedUtc = DateTime.UtcNow
                };
                var projectPath = Path.Combine(_testDirectory, $"boundary_test_{Guid.NewGuid():N}.tixlproject");

                try
                {
                    // Act - Try to create project with boundary name
                    var result = await ProjectFileIOSafety.CreateProjectAsync(metadata, projectPath);
                    
                    // Assert - Should either succeed or fail gracefully
                    if (result.IsSuccess)
                    {
                        // If it succeeds, verify it can be loaded
                        var loadResult = await ProjectFileIOSafety.LoadProjectAsync(projectPath);
                        loadResult.IsSuccess.Should().BeTrue($"Should be able to load project with name: {name.Truncate(50)}...");
                        loadResult.Metadata.Name.Should().Be(name);
                    }
                    else
                    {
                        // If it fails, should have meaningful error message
                        result.ErrorMessage.Should().NotBeNullOrEmpty($"Should provide error for name: {name.Truncate(50)}...");
                    }
                }
                catch (Exception ex)
                {
                    // System should not throw exceptions for boundary cases
                    Assert.True(false, $"Unexpected exception for project name '{name.Truncate(50)}...': {ex.Message}");
                }
            }
        }

        [Fact]
        public async Task LargeDataSets_ShouldHandleSizeLimits()
        {
            // Test with data that approaches or exceeds size limits
            var testCases = new[]
            {
                // Small data
                new { Size = 1024, Description = "1KB data" },
                // Medium data  
                new { Size = 1024 * 1024, Description = "1MB data" },
                // Large data (approaching 100MB limit)
                new { Size = 50 * 1024 * 1024, Description = "50MB data" },
                // Very large data (exceeding typical limits)
                new { Size = 150 * 1024 * 1024, Description = "150MB data" }
            };

            foreach (var testCase in testCases)
            {
                // Arrange
                var largeData = new LargeDataContainer
                {
                    Data = new string('X', testCase.Size),
                    Metadata = new Dictionary<string, object>
                    {
                        { "Size", testCase.Size },
                        { "Description", testCase.Description },
                        { "Timestamp", DateTime.UtcNow }
                    }
                };
                var dataPath = Path.Combine(_testDirectory, $"large_data_{Guid.NewGuid():N}.json");

                try
                {
                    // Act - Try to serialize large data
                    var serializeResult = await SafeSerialization.SafeSerializeToJsonAsync(largeData, dataPath);

                    if (testCase.Size <= 100 * 1024 * 1024) // Within SafeSerialization limits
                    {
                        serializeResult.IsSuccess.Should().BeTrue($"Should serialize {testCase.Description}");
                        
                        // Verify it can be deserialized
                        var deserializeResult = await SafeSerialization.SafeDeserializeFromJsonAsync<LargeDataContainer>(dataPath);
                        deserializeResult.IsSuccess.Should().BeTrue($"Should deserialize {testCase.Description}");
                        deserializeResult.Data.Should().NotBeNull();
                    }
                    else
                    {
                        // Should fail gracefully for data exceeding limits
                        serializeResult.IsSuccess.Should().BeFalse($"Should fail for {testCase.Description}");
                        serializeResult.ErrorMessage.Should().Contain("too large", "Should mention size limit");
                    }
                }
                catch (Exception ex)
                {
                    // Should handle gracefully without throwing
                    Assert.True(false, $"Unexpected exception for {testCase.Description}: {ex.Message}");
                }
            }
        }

        [Fact]
        public async Task DeeplyNestedStructures_ShouldHandleComplexity()
        {
            // Test deeply nested data structures
            var nestedDepths = new[] { 10, 50, 100, 200 };

            foreach (var depth in nestedDepths)
            {
                // Arrange
                var nestedData = CreateNestedStructure(depth);
                var nestedPath = Path.Combine(_testDirectory, $"nested_{depth}_levels.json");

                try
                {
                    // Act - Serialize deeply nested structure
                    var serializeResult = await SafeSerialization.SafeSerializeToJsonAsync(nestedData, nestedPath);

                    if (depth <= 64) // Within JSON max depth limit
                    {
                        serializeResult.IsSuccess.Should().BeTrue($"Should serialize {depth} levels of nesting");
                        
                        // Verify round-trip integrity
                        var deserializeResult = await SafeSerialization.SafeDeserializeFromJsonAsync<NestedObject>(nestedPath);
                        deserializeResult.IsSuccess.Should().BeTrue($"Should deserialize {depth} levels of nesting");
                        
                        // Verify structure is preserved
                        var restoredData = deserializeResult.Data;
                        restoredData.Should().NotBeNull();
                        VerifyNestedStructure(restoredData, depth, 0);
                    }
                    else
                    {
                        // Should fail gracefully for excessive nesting
                        serializeResult.IsSuccess.Should().BeFalse($"Should fail for {depth} levels of nesting");
                    }
                }
                catch (Exception ex)
                {
                    Assert.True(false, $"Unexpected exception for {depth} levels: {ex.Message}");
                }
            }
        }

        [Fact]
        public async Task ComplexGraphs_ShouldHandleManyNodesAndConnections()
        {
            // Test with various graph sizes
            var graphSizes = new[] { 10, 100, 1000, 5000 };

            foreach (var nodeCount in graphSizes)
            {
                // Arrange
                var graph = CreateLargeGraph(nodeCount);
                var graphPath = Path.Combine(_testDirectory, $"graph_{nodeCount}_nodes.json");

                try
                {
                    // Act - Serialize large graph
                    var serializeResult = await SafeSerialization.SafeSerializeToJsonAsync(graph, graphPath);

                    if (nodeCount <= 1000) // Reasonable limit for testing
                    {
                        serializeResult.IsSuccess.Should().BeTrue($"Should serialize graph with {nodeCount} nodes");
                        
                        // Verify round-trip integrity
                        var deserializeResult = await SafeSerialization.SafeDeserializeFromJsonAsync<LargeGraphData>(graphPath);
                        deserializeResult.IsSuccess.Should().BeTrue($"Should deserialize graph with {nodeCount} nodes");
                        
                        // Verify graph structure
                        var restoredGraph = deserializeResult.Data;
                        restoredGraph.Should().NotBeNull();
                        restoredGraph.Nodes.Should().HaveCount(nodeCount);
                        restoredGraph.Connections.Should().HaveCountGreaterOrEqualTo(nodeCount - 1); // At least tree structure
                    }
                    else
                    {
                        // Large graphs might fail due to size/complexity
                        // This is expected behavior for extremely large graphs
                        serializeResult.IsSuccess.Should().BeTrue("Should handle large graphs gracefully");
                    }
                }
                catch (Exception ex)
                {
                    Assert.True(false, $"Unexpected exception for graph with {nodeCount} nodes: {ex.Message}");
                }
            }
        }

        #endregion

        #region Concurrent Access Tests

        [Fact]
        public async Task ConcurrentSerialization_ShouldHandleParallelOperations()
        {
            // Arrange
            var concurrentOperations = 20;
            var operations = new List<(string operationId, Func<Task> operation)>();

            for (int i = 0; i < concurrentOperations; i++)
            {
                var opId = $"operation_{i:D2}";
                var testData = new TestData
                {
                    OperationId = opId,
                    Timestamp = DateTime.UtcNow,
                    Data = new string('X', 1000 * (i + 1)), // Varying sizes
                    Metadata = new Dictionary<string, object>
                    {
                        { "Index", i },
                        { "Size", 1000 * (i + 1) },
                        { "Concurrent", true }
                    }
                };

                var filePath = Path.Combine(_testDirectory, $"concurrent_{opId}.json");
                
                operations.Add((opId, async () =>
                {
                    // Serialize
                    var serializeResult = await SafeSerialization.SafeSerializeToJsonAsync(testData, filePath);
                    serializeResult.IsSuccess.Should().BeTrue($"Serialization should succeed for {opId}");

                    // Small delay to increase chance of concurrent access
                    await Task.Delay(_random.Next(0, 10));

                    // Deserialize and verify
                    var deserializeResult = await SafeSerialization.SafeDeserializeFromJsonAsync<TestData>(filePath);
                    deserializeResult.IsSuccess.Should().BeTrue($"Deserialization should succeed for {opId}");
                    deserializeResult.Data.Should().NotBeNull();
                    deserializeResult.Data.OperationId.Should().Be(opId);
                }));
            }

            // Act - Execute all operations concurrently
            var tasks = operations.Select(op => op.operation());
            await Task.WhenAll(tasks);

            // Assert - All operations should complete successfully
            var verificationTasks = operations.Select(async op =>
            {
                var filePath = Path.Combine(_testDirectory, $"concurrent_{op.operationId}.json");
                var result = await SafeSerialization.SafeDeserializeFromJsonAsync<TestData>(filePath);
                result.IsSuccess.Should().BeTrue($"Verification should succeed for {op.operationId}");
                result.Data.Should().NotBeNull();
                result.Data.OperationId.Should().Be(op.operationId);
            });

            await Task.WhenAll(verificationTasks);
        }

        [Fact]
        public async Task ConcurrentFileAccess_ShouldHandleRaceConditions()
        {
            // Arrange
            var testData = new TestData
            {
                OperationId = "race_test",
                Timestamp = DateTime.UtcNow,
                Data = "Initial data",
                Metadata = new Dictionary<string, object> { { "TestType", "RaceCondition" } }
            };
            var filePath = Path.Combine(_testDirectory, "race_condition.json");

            // Create initial file
            var initialResult = await SafeSerialization.SafeSerializeToJsonAsync(testData, filePath);
            initialResult.IsSuccess.Should().BeTrue();

            var concurrentTasks = new List<Task>();

            // Act - Multiple concurrent read/write operations
            for (int i = 0; i < 10; i++)
            {
                concurrentTasks.Add(Task.Run(async () =>
                {
                    // Read operation
                    var readResult = await SafeSerialization.SafeDeserializeFromJsonAsync<TestData>(filePath);
                    
                    // Update data
                    if (readResult.IsSuccess && readResult.Data != null)
                    {
                        readResult.Data.Timestamp = DateTime.UtcNow;
                        readResult.Data.Metadata["ConcurrentUpdate"] = i;

                        // Write operation
                        var writeResult = await SafeSerialization.SafeSerializeToJsonAsync(readResult.Data, filePath);
                        writeResult.IsSuccess.Should().BeTrue("Write should succeed");
                    }
                }));

                concurrentTasks.Add(Task.Run(async () =>
                {
                    // Create backup
                    var backupResult = await SafeSerialization.CreateRollbackPointAsync(filePath);
                    // Backup might succeed or fail, but shouldn't throw
                }));
            }

            // Wait for all concurrent operations
            await Task.WhenAll(concurrentTasks);

            // Assert - File should still be valid and accessible
            var finalRead = await SafeSerialization.SafeDeserializeFromJsonAsync<TestData>(filePath);
            finalRead.IsSuccess.Should().BeTrue("Final read should succeed");
            finalRead.Data.Should().NotBeNull("Data should not be corrupted");
        }

        #endregion

        #region Data Corruption Tests

        [Fact]
        public async Task PartialWrites_ShouldHandleIncompleteData()
        {
            // Arrange
            var originalData = new TestData
            {
                OperationId = "partial_write_test",
                Timestamp = DateTime.UtcNow,
                Data = new string('A', 10000),
                Metadata = new Dictionary<string, object> { { "TestType", "PartialWrite" } }
            };
            var filePath = Path.Combine(_testDirectory, "partial_write.json");

            // Create complete file
            var writeResult = await SafeSerialization.SafeSerializeToJsonAsync(originalData, filePath);
            writeResult.IsSuccess.Should().BeTrue();

            // Act - Simulate partial write by truncating file
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite))
            {
                fileStream.SetLength(fileStream.Length / 2); // Truncate to half size
            }

            // Assert - Should handle gracefully
            var readResult = await SafeSerialization.SafeDeserializeFromJsonAsync<TestData>(filePath);
            readResult.IsSuccess.Should().BeFalse("Should fail on incomplete data");
            readResult.ErrorMessage.Should().NotBeNullOrEmpty("Should provide meaningful error");

            // Verify rollback mechanism works
            var rollbackResult = await SafeSerialization.RecoverFromErrorAsync(filePath);
            rollbackResult.IsSuccess.Should().BeTrue("Rollback should succeed");

            // Verify data is restored
            var recoveryResult = await SafeSerialization.SafeDeserializeFromJsonAsync<TestData>(filePath);
            recoveryResult.IsSuccess.Should().BeTrue("Recovered data should be valid");
        }

        [Fact]
        public async Task MalformedJson_ShouldNotAffectSystemState()
        {
            // Arrange
            var validPath = Path.Combine(_testDirectory, "valid_data.json");
            var malformedPath = Path.Combine(_testDirectory, "malformed_data.json");

            var validData = new TestData
            {
                OperationId = "system_state_test",
                Timestamp = DateTime.UtcNow,
                Data = "Valid system data",
                Metadata = new Dictionary<string, object> { { "Critical", true } }
            };

            // Create valid file
            var validResult = await SafeSerialization.SafeSerializeToJsonAsync(validData, validPath);
            validResult.IsSuccess.Should().BeTrue();

            // Create various types of malformed data
            var malformedCases = new[]
            {
                "{ invalid json",
                "[]",
                "{}",
                "{ \"unclosed\": \"string }",
                "[1, 2, 3,]", // Trailing comma
                "{ \"key\": , }", // Missing value
                "{{}}", // Double opening brace
                "null", // Just null
                "\"string\"", // Just a string
                "123.456.789", // Invalid number
                new string('a', 50000) // Very long invalid content
            };

            foreach (var (malformedContent, index) in malformedCases.Select((content, index) => (content, index)))
            {
                var testPath = Path.Combine(_testDirectory, $"malformed_{index}.json");
                await File.WriteAllTextAsync(testPath, malformedContent);

                // Act - Try to load malformed data
                var loadResult = await SafeSerialization.SafeDeserializeFromJsonAsync<TestData>(testPath);

                // Assert - Should fail gracefully
                loadResult.IsSuccess.Should().BeFalse($"Should fail gracefully on malformed case {index}");
                loadResult.ErrorMessage.Should().NotBeNullOrEmpty("Should provide error message");

                // Verify system state is preserved
                var systemStateCheck = await SafeSerialization.SafeDeserializeFromJsonAsync<TestData>(validPath);
                systemStateCheck.IsSuccess.Should().BeTrue("System state should be preserved");
            }
        }

        #endregion

        #region Resource Exhaustion Tests

        [Fact]
        public async Task DiskSpaceExhaustion_ShouldHandleGracefully()
        {
            // Note: This test simulates disk space exhaustion scenarios
            // In a real test environment, this might not be fully testable
            // but we can test the logic paths

            // Arrange
            var testData = new TestData
            {
                OperationId = "disk_space_test",
                Timestamp = DateTime.UtcNow,
                Data = new string('X', 1000),
                Metadata = new Dictionary<string, object> { { "TestType", "DiskSpace" } }
            };
            var filePath = Path.Combine(_testDirectory, "disk_space_test.json");

            // Test with various sizes to simulate potential disk space issues
            var sizes = new[] { 1024, 1024 * 1024, 10 * 1024 * 1024, 100 * 1024 * 1024 };

            foreach (var size in sizes)
            {
                testData.Data = new string('Y', size);
                var testFilePath = Path.Combine(_testDirectory, $"disk_test_{size}.json");

                try
                {
                    var result = await SafeSerialization.SafeSerializeToJsonAsync(testData, testFilePath);
                    
                    // If it succeeds, file was created
                    if (result.IsSuccess)
                    {
                        // Verify it can be read back
                        var readResult = await SafeSerialization.SafeDeserializeFromJsonAsync<TestData>(testFilePath);
                        readResult.IsSuccess.Should().BeTrue($"Should handle {size} bytes gracefully");
                    }
                }
                catch (IOException ex)
                {
                    // Expected if disk space is actually exhausted
                    // Should not throw unexpected exceptions
                    ex.Message.Should().ContainAny("disk", "space", "no space", "quota");
                }
                catch (Exception ex)
                {
                    Assert.True(false, $"Unexpected exception for {size} bytes: {ex.Message}");
                }
            }
        }

        [Fact]
        public async Task MemoryPressure_ShouldHandleLargeAllocations()
        {
            // Test serialization under memory pressure simulation
            var memoryTests = new[]
            {
                new { Size = 1024 * 1024, Description = "1MB allocation" },
                new { Size = 10 * 1024 * 1024, Description = "10MB allocation" },
                new { Size = 50 * 1024 * 1024, Description = "50MB allocation" }
            };

            foreach (var test in memoryTests)
            {
                try
                {
                    // Force garbage collection
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

                    var beforeMemory = GC.GetTotalMemory(false);

                    // Create large data structure
                    var largeData = new
                    {
                        TestType = "MemoryPressure",
                        Size = test.Size,
                        Data = new string('X', test.Size),
                        Metadata = new Dictionary<string, object>
                        {
                            { "Allocated", test.Size },
                            { "Timestamp", DateTime.UtcNow },
                            { "BeforeMemory", beforeMemory }
                        }
                    };

                    var filePath = Path.Combine(_testDirectory, $"memory_test_{test.Size}.json");
                    var result = await SafeSerialization.SafeSerializeToJsonAsync(largeData, filePath);
                    
                    if (result.IsSuccess)
                    {
                        var afterMemory = GC.GetTotalMemory(false);
                        
                        // Verify serialization worked
                        var deserializeResult = await SafeSerialization.SafeDeserializeFromJsonAsync<dynamic>(filePath);
                        deserializeResult.IsSuccess.Should().BeTrue($"Should handle {test.Description}");
                    }

                    // Force cleanup
                    GC.Collect();
                }
                catch (OutOfMemoryException)
                {
                    // Expected behavior under extreme memory pressure
                    // Test should document this expected failure mode
                }
                catch (Exception ex)
                {
                    Assert.True(false, $"Unexpected exception for {test.Description}: {ex.Message}");
                }
            }
        }

        #endregion

        #region Helper Methods

        private NestedObject CreateNestedStructure(int depth)
        {
            if (depth <= 0)
            {
                return new NestedObject
                {
                    Value = $"Leaf_{_random.Next()}",
                    Metadata = new Dictionary<string, object> { { "Depth", 0 } }
                };
            }

            return new NestedObject
            {
                Value = $"Node_{depth}",
                Metadata = new Dictionary<string, object>
                {
                    { "Depth", depth },
                    { "RandomValue", _random.NextDouble() }
                },
                Child = CreateNestedStructure(depth - 1)
            };
        }

        private void VerifyNestedStructure(NestedObject obj, int expectedDepth, int currentDepth)
        {
            obj.Should().NotBeNull();
            obj.Value.Should().NotBeNullOrEmpty();
            obj.Metadata.Should().NotBeNull();
            obj.Metadata.Should().ContainKey("Depth");

            var depth = Convert.ToInt32(obj.Metadata["Depth"]);
            depth.Should().Be(expectedDepth - currentDepth);

            if (obj.Child != null)
            {
                VerifyNestedStructure(obj.Child, expectedDepth, currentDepth + 1);
            }
        }

        private LargeGraphData CreateLargeGraph(int nodeCount)
        {
            var graphId = Guid.NewGuid().ToString();
            var nodes = new List<LargeGraphNode>();
            var connections = new List<LargeGraphConnection>();

            // Create nodes
            for (int i = 0; i < nodeCount; i++)
            {
                nodes.Add(new LargeGraphNode
                {
                    NodeId = $"{graphId}_Node_{i:D6}",
                    NodeType = i % 10 < 5 ? "Input" : (i % 10 < 8 ? "Process" : "Output"),
                    PositionX = i % 100 * 20,
                    PositionY = i / 100 * 20,
                    Properties = new Dictionary<string, object>
                    {
                        { "Index", i },
                        { "Value", _random.NextDouble() * 100 },
                        { "Label", $"Node {i}" }
                    }
                });
            }

            // Create connections (ensure connectivity)
            for (int i = 0; i < nodeCount - 1; i++)
            {
                connections.Add(new LargeGraphConnection
                {
                    ConnectionId = $"{graphId}_Conn_{i:D6}",
                    SourceNodeId = nodes[i].NodeId,
                    TargetNodeId = nodes[i + 1].NodeId,
                    Weight = _random.NextDouble()
                });

                // Add some additional random connections
                if (_random.NextDouble() < 0.1 && i + 10 < nodeCount)
                {
                    connections.Add(new LargeGraphConnection
                    {
                        ConnectionId = $"{graphId}_Conn_{i:D6}_extra",
                        SourceNodeId = nodes[i].NodeId,
                        TargetNodeId = nodes[i + 10].NodeId,
                        Weight = _random.NextDouble()
                    });
                }
            }

            return new LargeGraphData
            {
                GraphId = graphId,
                NodeCount = nodeCount,
                ConnectionCount = connections.Count,
                Nodes = nodes,
                Connections = connections,
                GraphMetadata = new Dictionary<string, object>
                {
                    { "Created", DateTime.UtcNow },
                    { "RandomSeed", _random.Next() },
                    { "TestType", "LargeGraph" }
                }
            };
        }

        #endregion

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                try
                {
                    Directory.Delete(_testDirectory, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        #region Test Data Models

        private class LargeDataContainer
        {
            public string Data { get; set; }
            public Dictionary<string, object> Metadata { get; set; }
        }

        private class NestedObject
        {
            public string Value { get; set; }
            public Dictionary<string, object> Metadata { get; set; }
            public NestedObject Child { get; set; }
        }

        private class LargeGraphData
        {
            public string GraphId { get; set; }
            public int NodeCount { get; set; }
            public int ConnectionCount { get; set; }
            public List<LargeGraphNode> Nodes { get; set; }
            public List<LargeGraphConnection> Connections { get; set; }
            public Dictionary<string, object> GraphMetadata { get; set; }
        }

        private class LargeGraphNode
        {
            public string NodeId { get; set; }
            public string NodeType { get; set; }
            public int PositionX { get; set; }
            public int PositionY { get; set; }
            public Dictionary<string, object> Properties { get; set; }
        }

        private class LargeGraphConnection
        {
            public string ConnectionId { get; set; }
            public string SourceNodeId { get; set; }
            public string TargetNodeId { get; set; }
            public double Weight { get; set; }
        }

        private class TestData
        {
            public string OperationId { get; set; }
            public DateTime Timestamp { get; set; }
            public string Data { get; set; }
            public Dictionary<string, object> Metadata { get; set; }
        }

        #endregion
    }

    // Extension methods for easier testing
    public static class StringExtensions
    {
        public static string Truncate(this string input, int maxLength)
        {
            if (string.IsNullOrEmpty(input) || input.Length <= maxLength)
                return input;

            return input.Substring(0, maxLength - 3) + "...";
        }
    }

    public static class CollectionExtensions
    {
        public static void ShouldHaveCountGreaterOrEqualTo<T>(this IEnumerable<T> collection, int count)
        {
            collection.Should().NotBeNull();
            collection.Count().Should().BeGreaterOrEqualTo(count);
        }
    }
}