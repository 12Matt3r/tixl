using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TiXL.Core.IO;
using Xunit;
using FluentAssertions;

namespace TiXL.Tests.IO
{
    /// <summary>
    /// Comprehensive round-trip serialization tests for TiXL projects and data structures.
    /// TIXL-047: Verify perfect state preservation across serialization cycles
    /// </summary>
    public class RoundTripSerializationTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly string _backupDirectory;
        private readonly Random _random;

        public RoundTripSerializationTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "TiXL_SerializationTests_" + Guid.NewGuid().ToString("N")[..8]);
            _backupDirectory = Path.Combine(_testDirectory, "backups");
            Directory.CreateDirectory(_testDirectory);
            Directory.CreateDirectory(_backupDirectory);
            _random = new Random(42); // Deterministic random for reproducible tests
        }

        #region Project Serialization Tests

        [Fact]
        public async Task Project_RoundTrip_ShouldPreserveAllMetadata()
        {
            // Arrange
            var original = CreateComplexProjectMetadata();
            var projectPath = Path.Combine(_testDirectory, "metadata_test.tixlproject");
            var iterations = 5;

            var originalStates = new List<ProjectMetadata>();
            var restoredStates = new List<ProjectMetadata>();

            // Act - Multiple round-trips
            for (int i = 0; i < iterations; i++)
            {
                // Create or update project
                var createResult = await ProjectFileIOSafety.CreateProjectAsync(original, projectPath);
                createResult.IsSuccess.Should().BeTrue($"Iteration {i + 1}: Project creation should succeed");

                // Load project
                var loadResult = await ProjectFileIOSafety.LoadProjectAsync(projectPath);
                loadResult.IsSuccess.Should().BeTrue($"Iteration {i + 1}: Project loading should succeed");
                loadResult.Metadata.Should().NotBeNull($"Iteration {i + 1}: Metadata should not be null");

                originalStates.Add(original);
                restoredStates.Add(loadResult.Metadata);

                // Modify metadata for next iteration
                original = original with { LastModifiedUtc = DateTime.UtcNow.AddMinutes(i + 1) };
            }

            // Assert - All round-trips should preserve data integrity
            restoredStates.Count.Should().Be(iterations);

            for (int i = 0; i < iterations; i++)
            {
                var originalState = originalStates[i];
                var restoredState = restoredStates[i];

                restoredState.Should().NotBeNull();
                restoredState.Name.Should().Be(originalState.Name, $"Iteration {i + 1}: Name should match");
                restoredState.Id.Should().Be(originalState.Id, $"Iteration {i + 1}: Id should match");
                restoredState.Version.Should().Be(originalState.Version, $"Iteration {i + 1}: Version should match");
                restoredState.Description.Should().Be(originalState.Description, $"Iteration {i + 1}: Description should match");
                restoredState.CreatedUtc.Should().Be(originalState.CreatedUtc, $"Iteration {i + 1}: CreatedUtc should match");
                restoredState.Tags.Should().BeEquivalentTo(originalState.Tags, $"Iteration {i + 1}: Tags should match");
                restoredState.Properties.Should().Equal(originalState.Properties, $"Iteration {i + 1}: Properties should match");
            }
        }

        [Fact]
        public async Task Project_RoundTrip_ShouldHandleComplexSettings()
        {
            // Arrange
            var metadata = CreateComplexProjectMetadata();
            var projectData = CreateComplexProjectData();
            var projectPath = Path.Combine(_testDirectory, "complex_settings_test.tixlproject");

            // Act - First save
            var saveResult = await ProjectFileIOSafety.SaveProjectAsync(metadata, projectData, projectPath);
            saveResult.IsSuccess.Should().BeTrue("Initial save should succeed");

            // Load and verify
            var loadResult = await ProjectFileIOSafety.LoadProjectAsync(projectPath);
            loadResult.IsSuccess.Should().BeTrue("Load should succeed");
            var loadedData = loadResult.ProjectData;

            // Second save with modifications
            loadedData.Settings["ModifiedSetting"] = "Modified Value";
            loadedData.Settings["NewSetting"] = 42;
            loadedData.Dependencies.Add("New Dependency");

            var secondSave = await ProjectFileIOSafety.SaveProjectAsync(loadResult.Metadata, loadedData, projectPath);
            secondSave.IsSuccess.Should().BeTrue("Second save should succeed");

            // Final load
            var finalLoad = await ProjectFileIOSafety.LoadProjectAsync(projectPath);
            finalLoad.IsSuccess.Should().BeTrue("Final load should succeed");
            var finalData = finalLoad.ProjectData;

            // Assert - Verify complete data preservation
            finalData.Should().NotBeNull();
            finalData.Metadata.Should().NotBeNull();
            finalData.Version.Should().Be(projectData.Version);
            finalData.SchemaVersion.Should().Be(projectData.SchemaVersion);
            
            // Verify settings preservation and modification
            finalData.Settings.Should().ContainKey("ModifiedSetting").WhoseValue.Should().Be("Modified Value");
            finalData.Settings.Should().ContainKey("NewSetting").WhoseValue.Should().Be(42);
            finalData.Dependencies.Should().Contain("New Dependency");
            
            // Verify original settings are preserved
            foreach (var kvp in projectData.Settings)
            {
                finalData.Settings.Should().ContainKey(kvp.Key).WhoseValue.Should().Be(kvp.Value);
            }
        }

        [Fact]
        public async Task Project_RoundTrip_ShouldMaintainVersioning()
        {
            // Arrange
            var metadata = CreateComplexProjectMetadata();
            var projectPath = Path.Combine(_testDirectory, "versioning_test.tixlproject");
            var versions = new[] { "1.0.0", "1.1.0", "2.0.0", "2.1.0-beta" };

            // Act - Test each version
            foreach (var version in versions)
            {
                var versionedMetadata = metadata with { Version = version };
                var createResult = await ProjectFileIOSafety.CreateProjectAsync(versionedMetadata, projectPath);
                createResult.IsSuccess.Should().BeTrue($"Version {version} should be created successfully");

                var loadResult = await ProjectFileIOSafety.LoadProjectAsync(projectPath);
                loadResult.IsSuccess.Should().BeTrue($"Version {version} should be loadable");
                loadResult.Metadata.Version.Should().Be(version, $"Version {version} should be preserved");
            }
        }

        #endregion

        #region Operator Serialization Tests

        [Fact]
        public async Task Operator_RoundTrip_ShouldPreserveExecutionState()
        {
            // Arrange
            var operatorState = CreateComplexOperatorState();
            var operatorPath = Path.Combine(_testDirectory, "operator_state.json");
            var iterations = 10;

            for (int i = 0; i < iterations; i++)
            {
                // Serialize operator state
                var serializeResult = await SafeSerialization.SafeSerializeToJsonAsync(operatorState, operatorPath);
                serializeResult.IsSuccess.Should().BeTrue($"Iteration {i + 1}: Operator serialization should succeed");

                // Deserialize operator state
                var deserializeResult = await SafeSerialization.SafeDeserializeFromJsonAsync<TestOperatorState>(operatorPath);
                deserializeResult.IsSuccess.Should().BeTrue($"Iteration {i + 1}: Operator deserialization should succeed");
                var restoredState = deserializeResult.Data;

                // Verify state preservation
                restoredState.Should().NotBeNull();
                restoredState.OperatorId.Should().Be(operatorState.OperatorId);
                restoredState.OperatorType.Should().Be(operatorState.OperatorType);
                restoredState.IsActive.Should().Be(operatorState.IsActive);
                restoredState.ExecutionCount.Should().Be(operatorState.ExecutionCount);
                restoredState.LastExecutedUtc.Should().Be(operatorState.LastExecutedUtc);
                
                // Verify input parameters
                restoredState.InputParameters.Should().HaveCount(operatorState.InputParameters.Count);
                foreach (var param in operatorState.InputParameters)
                {
                    restoredState.InputParameters.Should().ContainKey(param.Key);
                }

                // Verify output values
                restoredState.OutputValues.Should().HaveCount(operatorState.OutputValues.Count);
                foreach (var output in operatorState.OutputValues)
                {
                    restoredState.OutputValues.Should().ContainKey(output.Key);
                }
            }
        }

        [Fact]
        public async Task Operator_RoundTrip_ShouldHandlePerformanceMetrics()
        {
            // Arrange
            var metrics = CreatePerformanceMetrics();
            var metricsPath = Path.Combine(_testDirectory, "performance_metrics.json");
            var roundTrips = 15;

            for (int i = 0; i < roundTrips; i++)
            {
                // Update metrics for this iteration
                metrics.LastUpdatedUtc = DateTime.UtcNow;
                metrics.EvaluationDuration = TimeSpan.FromMilliseconds(100 + i * 10);
                metrics.MemoryUsageBytes += 1024 * i;

                // Serialize
                var serializeResult = await SafeSerialization.SafeSerializeToJsonAsync(metrics, metricsPath);
                serializeResult.IsSuccess.Should().BeTrue($"Round-trip {i + 1}: Metrics serialization should succeed");

                // Deserialize
                var deserializeResult = await SafeSerialization.SafeDeserializeFromJsonAsync<TestPerformanceMetrics>(metricsPath);
                deserializeResult.IsSuccess.Should().BeTrue($"Round-trip {i + 1}: Metrics deserialization should succeed");
                var restoredMetrics = deserializeResult.Data;

                // Verify metrics preservation
                restoredMetrics.Should().NotBeNull();
                restoredMetrics.OperatorId.Should().Be(metrics.OperatorId);
                restoredMetrics.ExecutionCount.Should().Be(metrics.ExecutionCount);
                restoredMetrics.AverageEvaluationTime.Should().Be(metrics.AverageEvaluationTime);
                restoredMetrics.MemoryUsageBytes.Should().Be(metrics.MemoryUsageBytes);
                restoredMetrics.ResourceUsage.Should().Equal(metrics.ResourceUsage);
                restoredMetrics.PerformanceThresholds.Should().Equal(metrics.PerformanceThresholds);
            }
        }

        #endregion

        #region Graph Serialization Tests

        [Fact]
        public async Task Graph_RoundTrip_ShouldMaintainNodeConnections()
        {
            // Arrange
            var graph = CreateComplexNodeGraph();
            var graphPath = Path.Combine(_testDirectory, "node_graph.json");
            var iterations = 8;

            for (int i = 0; i < iterations; i++)
            {
                // Serialize graph
                var serializeResult = await SafeSerialization.SafeSerializeToJsonAsync(graph, graphPath);
                serializeResult.IsSuccess.Should().BeTrue($"Iteration {i + 1}: Graph serialization should succeed");

                // Deserialize graph
                var deserializeResult = await SafeSerialization.SafeDeserializeFromJsonAsync<TestNodeGraph>(graphPath);
                deserializeResult.IsSuccess.Should().BeTrue($"Iteration {i + 1}: Graph deserialization should succeed");
                var restoredGraph = deserializeResult.Data;

                // Verify graph structure
                restoredGraph.Should().NotBeNull();
                restoredGraph.GraphId.Should().Be(graph.GraphId);
                restoredGraph.Nodes.Should().HaveCount(graph.Nodes.Count);
                restoredGraph.Connections.Should().HaveCount(graph.Connections.Count);

                // Verify each node
                foreach (var originalNode in graph.Nodes)
                {
                    var restoredNode = restoredGraph.Nodes.FirstOrDefault(n => n.NodeId == originalNode.NodeId);
                    restoredNode.Should().NotBeNull($"Node {originalNode.NodeId} should be restored");

                    restoredNode.NodeType.Should().Be(originalNode.NodeType);
                    restoredNode.PositionX.Should().Be(originalNode.PositionX);
                    restoredNode.PositionY.Should().Be(originalNode.PositionY);
                    restoredNode.IsEnabled.Should().Be(originalNode.IsEnabled);
                    restoredNode.Properties.Should().Equal(originalNode.Properties);
                }

                // Verify each connection
                foreach (var originalConnection in graph.Connections)
                {
                    var restoredConnection = restoredGraph.Connections.FirstOrDefault(c => 
                        c.ConnectionId == originalConnection.ConnectionId);
                    restoredConnection.Should().NotBeNull($"Connection {originalConnection.ConnectionId} should be restored");

                    restoredConnection.SourceNodeId.Should().Be(originalConnection.SourceNodeId);
                    restoredConnection.TargetNodeId.Should().Be(originalConnection.TargetNodeId);
                    restoredConnection.SourceOutput.Should().Be(originalConnection.SourceOutput);
                    restoredConnection.TargetInput.Should().Be(originalConnection.TargetInput);
                }
            }
        }

        [Fact]
        public async Task Graph_RoundTrip_ShouldPreserveEvaluationOrder()
        {
            // Arrange
            var graph = CreateComplexNodeGraph();
            var graphPath = Path.Combine(_testDirectory, "evaluation_order.json");

            // Perform multiple round-trips
            for (int i = 0; i < 5; i++)
            {
                // Serialize and deserialize
                var serializeResult = await SafeSerialization.SafeSerializeToJsonAsync(graph, graphPath);
                serializeResult.IsSuccess.Should().BeTrue();

                var deserializeResult = await SafeSerialization.SafeDeserializeFromJsonAsync<TestNodeGraph>(graphPath);
                deserializeResult.IsSuccess.Should().BeTrue();
                var restoredGraph = deserializeResult.Data;

                // Verify evaluation order preservation
                restoredGraph.EvaluationOrder.Should().HaveCount(graph.EvaluationOrder.Count);
                restoredGraph.EvaluationOrder.Should().BeEquivalentTo(graph.EvaluationOrder);

                // Verify node dependencies
                for (int j = 0; j < graph.Nodes.Count; j++)
                {
                    var originalNode = graph.Nodes[j];
                    var restoredNode = restoredGraph.Nodes[j];

                    restoredNode.Dependencies.Should().BeEquivalentTo(originalNode.Dependencies);
                }

                // Update graph for next iteration
                foreach (var node in graph.Nodes)
                {
                    node.PositionX += 10;
                    node.PositionY += 5;
                }
            }
        }

        #endregion

        #region Resource Serialization Tests

        [Fact]
        public async Task Resource_RoundTrip_ShouldPreserveAssetMetadata()
        {
            // Arrange
            var resource = CreateComplexResourceAsset();
            var resourcePath = Path.Combine(_testDirectory, "resource_metadata.json");
            var iterations = 12;

            for (int i = 0; i < iterations; i++)
            {
                // Update resource data for this iteration
                resource.LastModifiedUtc = DateTime.UtcNow.AddHours(i);
                resource.FileSizeBytes += (long)(1024 * i);

                // Serialize resource
                var serializeResult = await SafeSerialization.SafeSerializeToJsonAsync(resource, resourcePath);
                serializeResult.IsSuccess.Should().BeTrue($"Iteration {i + 1}: Resource serialization should succeed");

                // Deserialize resource
                var deserializeResult = await SafeSerialization.SafeDeserializeFromJsonAsync<TestResourceAsset>(resourcePath);
                deserializeResult.IsSuccess.Should().BeTrue($"Iteration {i + 1}: Resource deserialization should succeed");
                var restoredResource = deserializeResult.Data;

                // Verify resource preservation
                restoredResource.Should().NotBeNull();
                restoredResource.AssetId.Should().Be(resource.AssetId);
                restoredResource.AssetName.Should().Be(resource.AssetName);
                restoredResource.AssetType.Should().Be(resource.AssetType);
                restoredResource.SourcePath.Should().Be(resource.SourcePath);
                restoredResource.ContentHash.Should().Be(resource.ContentHash);
                restoredResource.Dependencies.Should().HaveCount(resource.Dependencies.Count);
                restoredResource.Metadata.Should().Equal(resource.Metadata);

                // Verify version history
                restoredResource.VersionHistory.Should().HaveCount(resource.VersionHistory.Count);
                for (int j = 0; j < resource.VersionHistory.Count; j++)
                {
                    var originalVersion = resource.VersionHistory[j];
                    var restoredVersion = restoredResource.VersionHistory[j];
                    
                    restoredVersion.VersionNumber.Should().Be(originalVersion.VersionNumber);
                    restoredVersion.CreatedUtc.Should().Be(originalVersion.CreatedUtc);
                    restoredVersion.ChangeLog.Should().Be(originalVersion.ChangeLog);
                    restoredVersion.IsActive.Should().Be(originalVersion.IsActive);
                }
            }
        }

        [Fact]
        public async Task Resource_RoundTrip_ShouldHandleShaderPrograms()
        {
            // Arrange
            var shader = CreateShaderProgram();
            var shaderPath = Path.Combine(_testDirectory, "shader_program.json");
            var iterations = 6;

            for (int i = 0; i < iterations; i++)
            {
                // Modify shader for this iteration
                shader.CompilationTimestampUtc = DateTime.UtcNow.AddMinutes(i);
                shader.ParameterValues["Time"] = i * 0.1f;

                // Serialize shader
                var serializeResult = await SafeSerialization.SafeSerializeToJsonAsync(shader, shaderPath);
                serializeResult.IsSuccess.Should().BeTrue($"Iteration {i + 1}: Shader serialization should succeed");

                // Deserialize shader
                var deserializeResult = await SafeSerialization.SafeDeserializeFromJsonAsync<TestShaderProgram>(shaderPath);
                deserializeResult.IsSuccess.Should().BeTrue($"Iteration {i + 1}: Shader deserialization should succeed");
                var restoredShader = deserializeResult.Data;

                // Verify shader preservation
                restoredShader.Should().NotBeNull();
                restoredShader.ProgramId.Should().Be(shader.ProgramId);
                restoredShader.ShaderType.Should().Be(shader.ShaderType);
                restoredShader.SourceCode.Should().Be(shader.SourceCode);
                restoredShader.IsCompiled.Should().Be(shader.IsCompiled);
                restoredShader.CompilationErrors.Should().HaveCount(shader.CompilationErrors.Count);
                restoredShader.Uniforms.Should().Equal(shader.Uniforms);

                // Verify parameter values
                foreach (var param in shader.ParameterValues)
                {
                    restoredShader.ParameterValues.Should().ContainKey(param.Key).WhoseValue.Should().Be(param.Value);
                }
            }
        }

        #endregion

        #region Version Compatibility Tests

        [Fact]
        public async Task BackwardCompatibility_ShouldHandleOlderProjectVersions()
        {
            // Arrange
            var currentMetadata = CreateComplexProjectMetadata();
            var projectPath = Path.Combine(_testDirectory, "backward_compat_test.tixlproject");

            // Create project with current version
            var createResult = await ProjectFileIOSafety.CreateProjectAsync(currentMetadata, projectPath);
            createResult.IsSuccess.Should().BeTrue();

            // Simulate loading older version by modifying the stored version
            var projectContent = await File.ReadAllTextAsync(projectPath);
            var jsonDoc = JsonDocument.Parse(projectContent);
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream);

            writer.WriteStartObject();
            writer.WriteString("metadata", JsonSerializer.SerializeToElement(currentMetadata));
            writer.WriteString("version", "0.9.0"); // Simulate older version
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("createdUtc", currentMetadata.CreatedUtc.ToString("O"));
            writer.WriteString("lastModifiedUtc", currentMetadata.LastModifiedUtc.ToString("O"));
            writer.WriteEndObject();
            writer.Flush();

            await File.WriteAllBytesAsync(projectPath, stream.ToArray());

            // Act - Try to load the "older" version
            var loadResult = await ProjectFileIOSafety.LoadProjectAsync(projectPath);

            // Assert - Should handle gracefully (either load or give meaningful error)
            loadResult.IsSuccess.Should().BeTrue("Should handle version compatibility gracefully");
        }

        [Fact]
        public async Task ForwardCompatibility_ShouldHandleFutureSchemaVersions()
        {
            // Arrange
            var metadata = CreateComplexProjectMetadata();
            var projectPath = Path.Combine(_testDirectory, "forward_compat_test.tixlproject");

            // Create project file with future schema version
            var projectData = new ProjectData
            {
                Metadata = metadata,
                Version = "1.0.0",
                SchemaVersion = 999, // Future schema version
                CreatedUtc = DateTime.UtcNow,
                LastModifiedUtc = DateTime.UtcNow,
                Settings = new Dictionary<string, object> { { "FutureFeature", true } }
            };

            var saveResult = await SafeSerialization.SafeSerializeToJsonAsync(projectData, projectPath);
            saveResult.IsSuccess.Should().BeTrue();

            // Act - Try to load future schema version
            var loadResult = await ProjectFileIOSafety.LoadProjectAsync(projectPath);

            // Assert - Should handle gracefully
            // Note: This test documents expected behavior for forward compatibility
            loadResult.IsSuccess.Should().BeTrue("Should handle future schema versions gracefully");
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task MalformedData_ShouldNotCorruptSerializationSystem()
        {
            // Arrange
            var validPath = Path.Combine(_testDirectory, "valid_project.tixlproject");
            var malformedPath = Path.Combine(_testDirectory, "malformed_project.tixlproject");

            var metadata = CreateComplexProjectMetadata();
            var createResult = await ProjectFileIOSafety.CreateProjectAsync(metadata, validPath);
            createResult.IsSuccess.Should().BeTrue();

            // Act - Corrupt the file
            await File.WriteAllTextAsync(malformedPath, "{ invalid json content []");

            // Act - Try to load corrupted file
            var corruptedLoadResult = await ProjectFileIOSafety.LoadProjectAsync(malformedPath);

            // Assert - Should handle gracefully without corrupting system
            corruptedLoadResult.IsSuccess.Should().BeFalse("Should fail gracefully on malformed data");
            corruptedLoadResult.ErrorMessage.Should().NotBeNullOrEmpty("Should provide meaningful error message");

            // Verify valid file still works
            var validLoadResult = await ProjectFileIOSafety.LoadProjectAsync(validPath);
            validLoadResult.IsSuccess.Should().BeTrue("Valid file should still be loadable after system error");
        }

        [Fact]
        public async Task ConcurrentCorruption_ShouldHandleRaceConditions()
        {
            // Arrange
            var projectPath = Path.Combine(_testDirectory, "race_condition_test.tixlproject");
            var metadata = CreateComplexProjectMetadata();

            // Create valid project
            var createResult = await ProjectFileIOSafety.CreateProjectAsync(metadata, projectPath);
            createResult.IsSuccess.Should().BeTrue();

            var tasks = new List<Task>();

            // Act - Multiple concurrent operations with potential corruption
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    // Try to load the file
                    var loadResult = await ProjectFileIOSafety.LoadProjectAsync(projectPath);
                    
                    // Try to create backup
                    var backupResult = await ProjectFileIOSafety.CreateBackupAsync(projectPath);
                    
                    // Create rollback point
                    var rollbackResult = await SafeSerialization.CreateRollbackPointAsync(projectPath);
                }));

                tasks.Add(Task.Run(async () =>
                {
                    // Occasionally corrupt the file
                    if (i % 2 == 0)
                    {
                        await File.WriteAllTextAsync(projectPath, "{ corrupted }");
                    }
                }));
            }

            // Wait for all operations
            await Task.WhenAll(tasks);

            // Assert - System should still be functional
            var finalLoadResult = await ProjectFileIOSafety.LoadProjectAsync(projectPath);
            finalLoadResult.IsSuccess.Should().BeTrue("System should recover from concurrent corruption");
        }

        #endregion

        #region Performance Tests

        [Fact]
        public async Task SerializationPerformance_ShouldMeetPerformanceTargets()
        {
            // Arrange
            var testData = CreateLargeProjectData();
            var testPath = Path.Combine(_testDirectory, "performance_test.json");
            var iterations = 100;
            var maxSerializationTime = TimeSpan.FromMilliseconds(50);
            var maxDeserializationTime = TimeSpan.FromMilliseconds(50);

            var serializationTimes = new List<TimeSpan>();
            var deserializationTimes = new List<TimeSpan>();

            // Act - Measure serialization performance
            for (int i = 0; i < iterations; i++)
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var serializeResult = await SafeSerialization.SafeSerializeToJsonAsync(testData, testPath);
                stopwatch.Stop();

                serializeResult.IsSuccess.Should().BeTrue($"Iteration {i + 1}: Serialization should succeed");
                serializationTimes.Add(stopwatch.Elapsed);

                // Measure deserialization performance
                stopwatch.Restart();
                var deserializeResult = await SafeSerialization.SafeDeserializeFromJsonAsync<TestLargeProjectData>(testPath);
                stopwatch.Stop();

                deserializeResult.IsSuccess.Should().BeTrue($"Iteration {i + 1}: Deserialization should succeed");
                deserializationTimes.Add(stopwatch.Elapsed);

                // Verify data integrity on every 10th iteration
                if (i % 10 == 0)
                {
                    var restoredData = deserializeResult.Data;
                    restoredData.Should().NotBeNull();
                    restoredData.ProjectName.Should().Be(testData.ProjectName);
                    restoredData.Nodes.Should().HaveCount(testData.Nodes.Count);
                }
            }

            // Assert - Performance targets
            var avgSerializationTime = TimeSpan.FromTicks((long)serializationTimes.Average(t => t.Ticks));
            var avgDeserializationTime = TimeSpan.FromTicks((long)deserializationTimes.Average(t => t.Ticks));

            avgSerializationTime.Should().BeLessThan(maxSerializationTime, 
                $"Average serialization time should be under {maxSerializationTime.TotalMilliseconds}ms");
            avgDeserializationTime.Should().BeLessThan(maxDeserializationTime, 
                $"Average deserialization time should be under {maxDeserializationTime.TotalMilliseconds}ms");

            // Performance should be consistent (no significant degradation)
            var serializationStdDev = CalculateStandardDeviation(serializationTimes.Select(t => t.TotalMilliseconds));
            var deserializationStdDev = CalculateStandardDeviation(deserializationTimes.Select(t => t.TotalMilliseconds));

            serializationStdDev.Should().BeLessThan(10, "Serialization performance should be consistent");
            deserializationStdDev.Should().BeLessThan(10, "Deserialization performance should be consistent");
        }

        #endregion

        #region Helper Methods

        private ProjectMetadata CreateComplexProjectMetadata()
        {
            return new ProjectMetadata
            {
                Name = $"Test Project {_random.Next(1000, 9999)}",
                Id = Guid.NewGuid().ToString(),
                Version = "1.0.0",
                Description = "A complex test project for round-trip serialization testing",
                CreatedUtc = DateTime.UtcNow.AddDays(-30),
                LastModifiedUtc = DateTime.UtcNow,
                Tags = new List<string> { "test", "serialization", "round-trip", "TiXL-047" },
                Properties = new Dictionary<string, string>
                {
                    { "Author", "TiXL Test Suite" },
                    { "Complexity", "High" },
                    { "TestType", "RoundTripSerialization" },
                    { "Environment", "Test" },
                    { "RandomSeed", _random.Next().ToString() }
                }
            };
        }

        private ProjectData CreateComplexProjectData()
        {
            return new ProjectData
            {
                Metadata = CreateComplexProjectMetadata(),
                Version = "1.0.0",
                SchemaVersion = 1,
                CreatedUtc = DateTime.UtcNow.AddDays(-30),
                LastModifiedUtc = DateTime.UtcNow,
                Settings = new Dictionary<string, object>
                {
                    { "AutoSave", true },
                    { "AutoSaveInterval", 300 },
                    { "Theme", "Dark" },
                    { "Language", "en-US" },
                    { "ShowGrid", true },
                    { "GridSize", 20 },
                    { "SnapToGrid", false },
                    { "ShowRulers", true },
                    { "EnableUndo", true },
                    { "MaxUndoLevels", 100 },
                    { "PerformanceMode", "Balanced" }
                },
                Dependencies = new List<string>
                {
                    "TiXL.Core.dll",
                    "TiXL.Graphics.dll",
                    "TiXL.Audio.dll",
                    "TiXL.UI.dll"
                }
            };
        }

        private TestOperatorState CreateComplexOperatorState()
        {
            return new TestOperatorState
            {
                OperatorId = Guid.NewGuid().ToString(),
                OperatorType = "TestOperator",
                IsActive = _random.NextDouble() > 0.5,
                ExecutionCount = _random.Next(0, 1000),
                LastExecutedUtc = DateTime.UtcNow.AddMinutes(-_random.Next(0, 1000)),
                InputParameters = new Dictionary<string, object>
                {
                    { "InputA", _random.NextDouble() * 100 },
                    { "InputB", _random.NextDouble() * 100 },
                    { "InputC", _random.Next().ToString() },
                    { "InputD", new List<int> { 1, 2, 3, 4, 5 } },
                    { "InputE", CreateComplexObject() }
                },
                OutputValues = new Dictionary<string, object>
                {
                    { "OutputA", _random.NextDouble() * 200 },
                    { "OutputB", _random.Next().ToString("X8") },
                    { "OutputC", new[] { 1.0, 2.0, 3.0, 4.0, 5.0 } },
                    { "OutputD", new Dictionary<string, int> { { "Key1", 1 }, { "Key2", 2 } } }
                }
            };
        }

        private TestPerformanceMetrics CreatePerformanceMetrics()
        {
            return new TestPerformanceMetrics
            {
                OperatorId = Guid.NewGuid().ToString(),
                ExecutionCount = _random.Next(0, 10000),
                AverageEvaluationTime = TimeSpan.FromMilliseconds(_random.NextDouble() * 100),
                MemoryUsageBytes = _random.Next(1024, 1024 * 1024),
                LastUpdatedUtc = DateTime.UtcNow,
                ResourceUsage = new Dictionary<string, double>
                {
                    { "CPU", _random.NextDouble() * 100 },
                    { "Memory", _random.NextDouble() * 100 },
                    { "GPU", _random.NextDouble() * 100 },
                    { "DiskIO", _random.NextDouble() * 100 }
                },
                PerformanceThresholds = new Dictionary<string, TimeSpan>
                {
                    { "MaxEvaluationTime", TimeSpan.FromMilliseconds(1000) },
                    { "WarningThreshold", TimeSpan.FromMilliseconds(500) },
                    { "CriticalThreshold", TimeSpan.FromMilliseconds(2000) }
                }
            };
        }

        private TestNodeGraph CreateComplexNodeGraph()
        {
            var graphId = Guid.NewGuid().ToString();
            var nodes = new List<TestNode>();
            var connections = new List<TestConnection>();

            // Create nodes
            for (int i = 0; i < 20; i++)
            {
                nodes.Add(new TestNode
                {
                    NodeId = $"{graphId}_Node_{i:D3}",
                    NodeType = $"TestNodeType_{i % 5}",
                    PositionX = _random.Next(0, 1920),
                    PositionY = _random.Next(0, 1080),
                    IsEnabled = _random.NextDouble() > 0.2,
                    Properties = new Dictionary<string, object>
                    {
                        { "Width", _random.Next(100, 500) },
                        { "Height", _random.Next(50, 300) },
                        { "Opacity", _random.NextDouble() },
                        { "Rotation", _random.NextDouble() * 360 },
                        { "ScaleX", _random.NextDouble() * 2 },
                        { "ScaleY", _random.NextDouble() * 2 }
                    },
                    Dependencies = i > 0 ? new List<string> { $"{graphId}_Node_{i - 1:D3}" } : new List<string>()
                });
            }

            // Create connections
            for (int i = 0; i < 30; i++)
            {
                var sourceIndex = _random.Next(0, nodes.Count - 1);
                var targetIndex = _random.Next(sourceIndex + 1, nodes.Count);

                connections.Add(new TestConnection
                {
                    ConnectionId = $"{graphId}_Conn_{i:D3}",
                    SourceNodeId = nodes[sourceIndex].NodeId,
                    TargetNodeId = nodes[targetIndex].NodeId,
                    SourceOutput = $"Output_{_random.Next(1, 5)}",
                    TargetInput = $"Input_{_random.Next(1, 5)}",
                    IsActive = _random.NextDouble() > 0.3,
                    ConnectionType = _random.NextDouble() > 0.5 ? "Data" : "Control"
                });
            }

            return new TestNodeGraph
            {
                GraphId = graphId,
                Nodes = nodes,
                Connections = connections,
                EvaluationOrder = nodes.Select(n => n.NodeId).ToList(),
                GraphProperties = new Dictionary<string, object>
                {
                    { "Width", 1920 },
                    { "Height", 1080 },
                    { "BackgroundColor", "#1E1E1E" },
                    { "ShowGrid", true },
                    { "GridSize", 20 }
                }
            };
        }

        private TestResourceAsset CreateComplexResourceAsset()
        {
            return new TestResourceAsset
            {
                AssetId = Guid.NewGuid().ToString(),
                AssetName = $"TestAsset_{_random.Next(1000, 9999)}",
                AssetType = "Texture",
                SourcePath = $"/Textures/TestAsset_{_random.Next(1000, 9999)}.png",
                FileSizeBytes = _random.Next(1024, 10 * 1024 * 1024),
                ContentHash = CreateContentHash(),
                LastModifiedUtc = DateTime.UtcNow.AddDays(-_random.Next(0, 365)),
                Dependencies = new List<string>
                {
                    "d3d11.dll",
                    "opengl32.dll",
                    "TiXL.Graphics.dll"
                },
                Metadata = new Dictionary<string, object>
                {
                    { "Width", _random.Next(64, 4096) },
                    { "Height", _random.Next(64, 4096) },
                    { "Format", "RGBA8" },
                    { "Compression", "None" },
                    { "MipLevels", _random.Next(1, 12) },
                    { "Anisotropy", 16 },
                    { "Quality", "High" }
                },
                VersionHistory = new List<TestResourceVersion>
                {
                    new TestResourceVersion
                    {
                        VersionNumber = "1.0.0",
                        CreatedUtc = DateTime.UtcNow.AddDays(-30),
                        ChangeLog = "Initial version",
                        IsActive = true
                    },
                    new TestResourceVersion
                    {
                        VersionNumber = "1.1.0",
                        CreatedUtc = DateTime.UtcNow.AddDays(-15),
                        ChangeLog = "Added mipmaps",
                        IsActive = false
                    }
                }
            };
        }

        private TestShaderProgram CreateShaderProgram()
        {
            return new TestShaderProgram
            {
                ProgramId = Guid.NewGuid().ToString(),
                ShaderType = _random.NextDouble() > 0.5 ? "Vertex" : "Fragment",
                SourceCode = GenerateShaderSource(),
                IsCompiled = _random.NextDouble() > 0.2,
                CompilationTimestampUtc = DateTime.UtcNow.AddMinutes(-_random.Next(0, 1000)),
                CompilationErrors = new List<string>
                {
                    "Warning: Variable 'temp' declared but not used",
                    "Note: Function 'main' called from vertex shader"
                },
                Uniforms = new Dictionary<string, TestUniform>
                {
                    { "uTime", new TestUniform { Type = "float", Value = 0.0f, Location = 0 } },
                    { "uResolution", new TestUniform { Type = "vec2", Value = new float[] { 1920, 1080 }, Location = 1 } },
                    { "uColor", new TestUniform { Type = "vec3", Value = new float[] { 1.0f, 0.5f, 0.25f }, Location = 2 } }
                },
                ParameterValues = new Dictionary<string, object>
                {
                    { "Time", _random.NextDouble() * 1000 },
                    { "Fade", _random.NextDouble() },
                    { "Intensity", _random.NextDouble() * 2.0 },
                    { "BlendMode", _random.Next(0, 5) }
                }
            };
        }

        private TestLargeProjectData CreateLargeProjectData()
        {
            return new TestLargeProjectData
            {
                ProjectName = $"Large Test Project {_random.Next(1000, 9999)}",
                ProjectId = Guid.NewGuid().ToString(),
                CreatedUtc = DateTime.UtcNow.AddDays(-365),
                LastModifiedUtc = DateTime.UtcNow,
                Nodes = Enumerable.Range(0, 1000).Select(i => new TestLargeNode
                {
                    NodeId = $"Node_{i:D4}",
                    NodeType = i % 10 < 5 ? "Input" : "Process",
                    PositionX = (i % 50) * 40,
                    PositionY = (i / 50) * 30,
                    Properties = new Dictionary<string, object>
                    {
                        { "Value", _random.NextDouble() * 100 },
                        { "Enabled", _random.NextDouble() > 0.1 },
                        { "Label", $"Node {i}" },
                        { "Data", Enumerable.Range(0, 100).Select(x => _random.NextDouble()).ToArray() }
                    }
                }).ToList(),
                Connections = Enumerable.Range(0, 2000).Select(i => new TestLargeConnection
                {
                    ConnectionId = $"Conn_{i:D4}",
                    SourceNodeId = $"Node_{i % 1000:D4}",
                    TargetNodeId = $"Node_{(i + 1) % 1000:D4}",
                    Weight = _random.NextDouble()
                }).ToList()
            };
        }

        private object CreateComplexObject()
        {
            return new
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"ComplexObject_{_random.Next(1000, 9999)}",
                Value = _random.NextDouble() * 1000,
                Values = Enumerable.Range(0, 10).Select(_ => _random.NextDouble()).ToArray(),
                Properties = new Dictionary<string, object>
                {
                    { "Active", _random.NextDouble() > 0.5 },
                    { "Level", _random.Next(0, 10) },
                    { "Description", $"Test description {_random.Next(1, 100)}" }
                },
                CreatedUtc = DateTime.UtcNow.AddDays(-_random.Next(0, 100)),
                Tags = new List<string> { "test", "complex", "serialization" }
            };
        }

        private string CreateContentHash()
        {
            var bytes = new byte[32];
            _random.NextBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        private string GenerateShaderSource()
        {
            return $@"
                #version 330 core
                
                uniform float uTime;
                uniform vec2 uResolution;
                uniform vec3 uColor;
                
                out vec4 FragColor;
                
                void main() {{
                    vec2 uv = gl_FragCoord.xy / uResolution.xy;
                    float time = uTime + uv.x * 0.1;
                    
                    vec3 color = uColor * (0.5 + 0.5 * sin(time));
                    FragColor = vec4(color, 1.0);
                }}
            ";
        }

        private double CalculateStandardDeviation(IEnumerable<double> values)
        {
            var valuesList = values.ToList();
            if (valuesList.Count < 2) return 0;

            var average = valuesList.Average();
            var sumOfSquaresOfDifferences = valuesList.Select(val => (val - average) * (val - average)).Sum();
            return Math.Sqrt(sumOfSquaresOfDifferences / valuesList.Count);
        }

        #endregion

        public void Dispose()
        {
            // Cleanup test directory
            if (Directory.Exists(_testDirectory))
            {
                try
                {
                    Directory.Delete(_testDirectory, true);
                }
                catch
                {
                    // Ignore cleanup errors in tests
                }
            }
        }

        #region Test Data Models

        private class TestOperatorState
        {
            public string OperatorId { get; set; }
            public string OperatorType { get; set; }
            public bool IsActive { get; set; }
            public int ExecutionCount { get; set; }
            public DateTime LastExecutedUtc { get; set; }
            public Dictionary<string, object> InputParameters { get; set; }
            public Dictionary<string, object> OutputValues { get; set; }
        }

        private class TestPerformanceMetrics
        {
            public string OperatorId { get; set; }
            public int ExecutionCount { get; set; }
            public TimeSpan AverageEvaluationTime { get; set; }
            public long MemoryUsageBytes { get; set; }
            public DateTime LastUpdatedUtc { get; set; }
            public Dictionary<string, double> ResourceUsage { get; set; }
            public Dictionary<string, TimeSpan> PerformanceThresholds { get; set; }
        }

        private class TestNode
        {
            public string NodeId { get; set; }
            public string NodeType { get; set; }
            public int PositionX { get; set; }
            public int PositionY { get; set; }
            public bool IsEnabled { get; set; }
            public Dictionary<string, object> Properties { get; set; }
            public List<string> Dependencies { get; set; }
        }

        private class TestConnection
        {
            public string ConnectionId { get; set; }
            public string SourceNodeId { get; set; }
            public string TargetNodeId { get; set; }
            public string SourceOutput { get; set; }
            public string TargetInput { get; set; }
            public bool IsActive { get; set; }
            public string ConnectionType { get; set; }
        }

        private class TestNodeGraph
        {
            public string GraphId { get; set; }
            public List<TestNode> Nodes { get; set; }
            public List<TestConnection> Connections { get; set; }
            public List<string> EvaluationOrder { get; set; }
            public Dictionary<string, object> GraphProperties { get; set; }
        }

        private class TestResourceAsset
        {
            public string AssetId { get; set; }
            public string AssetName { get; set; }
            public string AssetType { get; set; }
            public string SourcePath { get; set; }
            public long FileSizeBytes { get; set; }
            public string ContentHash { get; set; }
            public DateTime LastModifiedUtc { get; set; }
            public List<string> Dependencies { get; set; }
            public Dictionary<string, object> Metadata { get; set; }
            public List<TestResourceVersion> VersionHistory { get; set; }
        }

        private class TestResourceVersion
        {
            public string VersionNumber { get; set; }
            public DateTime CreatedUtc { get; set; }
            public string ChangeLog { get; set; }
            public bool IsActive { get; set; }
        }

        private class TestShaderProgram
        {
            public string ProgramId { get; set; }
            public string ShaderType { get; set; }
            public string SourceCode { get; set; }
            public bool IsCompiled { get; set; }
            public DateTime CompilationTimestampUtc { get; set; }
            public List<string> CompilationErrors { get; set; }
            public Dictionary<string, TestUniform> Uniforms { get; set; }
            public Dictionary<string, object> ParameterValues { get; set; }
        }

        private class TestUniform
        {
            public string Type { get; set; }
            public object Value { get; set; }
            public int Location { get; set; }
        }

        private class TestLargeProjectData
        {
            public string ProjectName { get; set; }
            public string ProjectId { get; set; }
            public DateTime CreatedUtc { get; set; }
            public DateTime LastModifiedUtc { get; set; }
            public List<TestLargeNode> Nodes { get; set; }
            public List<TestLargeConnection> Connections { get; set; }
        }

        private class TestLargeNode
        {
            public string NodeId { get; set; }
            public string NodeType { get; set; }
            public int PositionX { get; set; }
            public int PositionY { get; set; }
            public Dictionary<string, object> Properties { get; set; }
        }

        private class TestLargeConnection
        {
            public string ConnectionId { get; set; }
            public string SourceNodeId { get; set; }
            public string TargetNodeId { get; set; }
            public double Weight { get; set; }
        }

        #endregion
    }
}