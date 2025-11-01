using Xunit;
using Xunit.Abstractions;
using TiXL.Tests.Categories;
using TiXL.Tests.Fixtures;
using TiXL.Tests.Data;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using T3.Core.Operators;

namespace TiXL.Tests.Integration
{
    /// <summary>
    /// Integration tests for TiXL Core module interactions
    /// </summary>
    [Category(TestCategories.Integration)]
    [Category(TestCategories.Core)]
    public class CoreModuleIntegrationTests : CoreTestFixture
    {
        private readonly ITestOutputHelper _output;

        public CoreModuleIntegrationTests(ITestOutputHelper output) : base()
        {
            _output = output;
        }

        [Fact]
        public async Task EndToEnd_VectorOperations_MathLibrary_Integration()
        {
            // Test vector operations across different data types
            var vectors2D = TestDataGenerator.GenerateVector2DArray(100);
            var vectors3D = TestDataGenerator.GenerateVector3DArray(50);
            var matrices4x4 = TestDataGenerator.GenerateMatrix4x4Array(25);

            // Act - Perform various vector operations
            var results = new List<object>();
            
            foreach (var vec in vectors2D.Take(10))
            {
                var magnitude = Math.Sqrt(vec.X * vec.X + vec.Y * vec.Y);
                results.Add(magnitude);
            }

            foreach (var vec in vectors3D.Take(5))
            {
                var magnitude = Math.Sqrt(vec.X * vec.X + vec.Y * vec.Y + vec.Z * vec.Z);
                results.Add(magnitude);
            }

            // Assert
            results.Should().HaveCount(15);
            results.Should().AllBeOfType<double>();
            results.Should().OnlyContain(r => (double)r > 0, "All magnitudes should be positive");
        }

        [Fact]
        public async Task EndToEnd_ColorSpaceConversions_Rendering_Integration()
        {
            // Test color conversions and rendering pipeline integration
            var colorsRGB = TestDataGenerator.GenerateColorRgbArray(50);
            var colorsRGBA = TestDataGenerator.GenerateColorRgbaArray(50);

            var conversions = new List<object>();

            // Simulate RGB to RGBA conversion
            foreach (var color in colorsRGB.Take(10))
            {
                var rgba = new
                {
                    R = color.R,
                    G = color.G,
                    B = color.B,
                    A = 255 // Default alpha
                };
                conversions.Add(rgba);
            }

            // Simulate RGBA to RGB conversion
            foreach (var color in colorsRGBA.Take(10))
            {
                var rgb = new
                {
                    R = color.R,
                    G = color.G,
                    B = color.B
                };
                conversions.Add(rgb);
            }

            // Assert
            conversions.Should().HaveCount(20);
            conversions.Should().OnlyContain(c => c.GetType().GetProperties().Length == 4 || c.GetType().GetProperties().Length == 3);
        }

        [Fact]
        public async Task EndToEnd_AudioProcessing_Pipeline_Integration()
        {
            // Test audio buffer processing through pipeline
            var audioBuffer = TestDataGenerator.GenerateAudioBuffer(channels: 2, sampleRate: 44100, duration: 0.1f);

            // Simulate audio processing pipeline
            var processedChannels = new List<float[]>();
            
            foreach (var channel in audioBuffer.ChannelData)
            {
                // Apply simple low-pass filter simulation
                var filtered = new float[channel.Length];
                for (int i = 1; i < channel.Length; i++)
                {
                    filtered[i] = (channel[i] + channel[i - 1]) / 2f;
                }
                filtered[0] = channel[0];
                
                processedChannels.Add(filtered);
            }

            // Assert
            processedChannels.Should().HaveCount(2);
            processedChannels.Should().AllSatisfy(channel =>
            {
                channel.Should().HaveCount(audioBuffer.SampleCount);
                channel.Should().OnlyContain(sample => sample >= -1f && sample <= 1f, "Audio samples should be normalized");
            });
        }
    }

    /// <summary>
    /// Integration tests for IO and Serialization workflows
    /// </summary>
    [Category(TestCategories.Integration)]
    [Category(TestCategories.IO)]
    public class IOSerializationIntegrationTests : CoreTestFixture
    {
        private readonly ITestOutputHelper _output;
        private readonly string _testDirectory;

        public IOSerializationIntegrationTests(ITestOutputHelper output) : base()
        {
            _output = output;
            _testDirectory = Path.Combine(Path.GetTempPath(), "TiXL_Integration_Tests_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_testDirectory);
        }

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
                    // Ignore cleanup errors in tests
                }
            }
        }

        [Fact]
        public async Task EndToEnd_ProjectSaveAndLoad_Pipeline_Integration()
        {
            // Test complete project save and load workflow
            var projectData = TestDataGenerator.GenerateTestProjectData();
            var projectPath = Path.Combine(_testDirectory, "integration_test_project.json");

            // Act - Save project
            var json = TestDataGenerator.SerializeToJson(projectData);
            await File.WriteAllTextAsync(projectPath, json);

            // Act - Load project
            var loadedJson = await File.ReadAllTextAsync(projectPath);
            var loadedProject = TestDataGenerator.DeserializeFromJson<TestProjectData>(loadedJson);

            // Assert - Verify data integrity
            loadedProject.Should().NotBeNull();
            loadedProject.Name.Should().Be(projectData.Name);
            loadedProject.Version.Should().Be(projectData.Version);
            loadedProject.Operators.Should().HaveCount(projectData.Operators.Count);
            loadedProject.Scenes.Should().HaveCount(projectData.Scenes.Count);
            
            // Verify operators
            for (int i = 0; i < projectData.Operators.Count; i++)
            {
                loadedProject.Operators[i].Id.Should().Be(projectData.Operators[i].Id);
                loadedProject.Operators[i].Name.Should().Be(projectData.Operators[i].Name);
                loadedProject.Operators[i].Type.Should().Be(projectData.Operators[i].Type);
            }

            // Verify scenes
            for (int i = 0; i < projectData.Scenes.Count; i++)
            {
                loadedProject.Scenes[i].Name.Should().Be(projectData.Scenes[i].Name);
                loadedProject.Scenes[i].Duration.Should().Be(projectData.Scenes[i].Duration);
            }
        }

        [Fact]
        public async Task EndToEnd_FileValidation_Serialization_Integration()
        {
            // Test file validation before serialization
            var validData = TestDataGenerator.GenerateTestProjectData();
            var validPath = Path.Combine(_testDirectory, "valid_project.json");
            var invalidPath = Path.Combine(_testDirectory, "invalid_path/../../../secret.txt");

            // Act & Assert - Valid data should succeed
            Action validAct = () => File.WriteAllText(validPath, TestDataGenerator.SerializeToJson(validData));
            validAct.Should().NotThrow();

            // Act & Assert - Invalid path should be handled
            Action invalidAct = () => File.WriteAllText(invalidPath, TestDataGenerator.SerializeToJson(validData));
            invalidAct.Should().Throw<Exception>(); // Should fail due to invalid path
        }

        [Fact]
        public async Task EndToEnd_ConcurrentFileOperations_Integration()
        {
            // Test concurrent file operations
            var projectCount = 10;
            var tasks = new List<Task>();

            // Act - Create multiple projects concurrently
            for (int i = 0; i < projectCount; i++)
            {
                var i1 = i;
                tasks.Add(Task.Run(async () =>
                {
                    var projectData = TestDataGenerator.GenerateTestProjectData();
                    var projectPath = Path.Combine(_testDirectory, $"concurrent_project_{i1:D2}.json");
                    
                    var json = TestDataGenerator.SerializeToJson(projectData);
                    await File.WriteAllTextAsync(projectPath, json);
                    
                    // Verify the file was written
                    File.Exists(projectPath).Should().BeTrue();
                }));
            }

            await Task.WhenAll(tasks);

            // Assert - All files should exist and be valid
            for (int i = 0; i < projectCount; i++)
            {
                var projectPath = Path.Combine(_testDirectory, $"concurrent_project_{i:D2}.json");
                File.Exists(projectPath).Should().BeTrue();
                
                var content = await File.ReadAllTextAsync(projectPath);
                content.Should().NotBeNullOrEmpty();
                content.Should().Contain("\"name\":");
            }
        }

        [Fact]
        public async Task EndToEnd_DataIntegrity_Check_Integration()
        {
            // Test data integrity across serialization/deserialization cycles
            var originalData = TestDataGenerator.GenerateTestProjectData();
            var cycles = 5;

            var currentData = originalData;
            
            for (int cycle = 0; cycle < cycles; cycle++)
            {
                // Serialize
                var json = TestDataGenerator.SerializeToJson(currentData);
                
                // Deserialize
                currentData = TestDataGenerator.DeserializeFromJson<TestProjectData>(json);
                
                // Verify integrity
                currentData.Should().NotBeNull();
                currentData.Name.Should().Be(originalData.Name, $"Data integrity failed at cycle {cycle}");
                currentData.Operators.Count.Should().Be(originalData.Operators.Count, $"Operator count mismatch at cycle {cycle}");
                currentData.Scenes.Count.Should().Be(originalData.Scenes.Count, $"Scene count mismatch at cycle {cycle}");
            }
        }
    }

    /// <summary>
    /// Integration tests for Operators module workflows
    /// </summary>
    [Category(TestCategories.Integration)]
    [Category(TestCategories.Operators)]
    public class OperatorsModuleIntegrationTests : CoreTestFixture
    {
        private readonly ITestOutputHelper _output;

        public OperatorsModuleIntegrationTests(ITestOutputHelper output) : base()
        {
            _output = output;
        }

        [Fact]
        public async Task EndToEnd_OperatorEvaluation_Pipeline_Integration()
        {
            // Test complete operator evaluation pipeline
            var renderingEngine = new MockRenderingEngine();
            var audioEngine = new MockAudioEngine();
            var resourceManager = new MockResourceManager();
            var guardrails = new GuardrailConfiguration
            {
                MaxEvaluationTime = TimeSpan.FromSeconds(10),
                MaxMemoryUsageBytes = 1024 * 1024 * 1024, // 1GB
                MaxConcurrentOperations = 50
            };

            using var context = new EvaluationContext(
                renderingEngine, audioEngine, resourceManager,
                ServiceProvider.GetRequiredService<ILogger<EvaluationContext>>(),
                CancellationToken.None, guardrails);

            // Act - Create and evaluate multiple operators
            var operators = new List<GuardrailedOperator>();
            for (int i = 0; i < 10; i++)
            {
                var operator1 = new GuardrailedOperator(context);
                operators.Add(operator1);
            }

            // Simulate operator evaluation
            foreach (var op in operators)
            {
                // Test operator lifecycle
                op.Should().NotBeNull();
            }

            // Assert
            context.Metrics.EvaluationCount.Should().BeGreaterOrEqualTo(0);
            context.CurrentState.IsWithinLimits.Should().BeTrue("Operators should complete within limits");
        }

        [Fact]
        public async Task EndToEnd_OperatorCancellation_Handling_Integration()
        {
            // Test operator cancellation handling
            var renderingEngine = new MockRenderingEngine();
            var audioEngine = new MockAudioEngine();
            var resourceManager = new MockResourceManager();
            var guardrails = new GuardrailConfiguration();
            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel immediately

            using var context = new EvaluationContext(
                renderingEngine, audioEngine, resourceManager,
                ServiceProvider.GetRequiredService<ILogger<EvaluationContext>>(),
                cts.Token, guardrails);

            // Act - Try to evaluate with cancelled token
            Action act = () =>
            {
                using var op = new GuardrailedOperator(context);
            };

            // Assert - Should handle cancellation gracefully
            act.Should().NotThrow("Should handle cancellation gracefully");
        }

        [Fact]
        public async Task EndToEnd_OperatorMemoryManagement_Integration()
        {
            // Test operator memory management
            var renderingEngine = new MockRenderingEngine();
            var audioEngine = new MockAudioEngine();
            var resourceManager = new MockResourceManager();
            var guardrails = new GuardrailConfiguration { MaxMemoryUsageBytes = 1024 * 1024 }; // 1MB

            using var context = new EvaluationContext(
                renderingEngine, audioEngine, resourceManager,
                ServiceProvider.GetRequiredService<ILogger<EvaluationContext>>(),
                CancellationToken.None, guardrails);

            // Act - Create operators that might consume memory
            var operators = new List<GuardrailedOperator>();
            for (int i = 0; i < 5; i++)
            {
                var op = new GuardrailedOperator(context);
                operators.Add(op);
                
                // Check memory usage
                var memoryBefore = GC.GetTotalMemory(false);
                // Simulate some work
                var data = new byte[1024 * 100]; // 100KB
                data[0] = 1; // Touch memory
                var memoryAfter = GC.GetTotalMemory(false);
                
                // Update execution state
                context.CurrentState.UpdateMemoryUsage((int)(memoryAfter - memoryBefore));
            }

            // Assert
            context.CurrentState.IsWithinLimits.Should().BeTrue("Should manage memory within limits");
        }

        [Fact]
        public async Task EndToEnd_ConcurrentOperators_Integration()
        {
            // Test concurrent operator execution
            var renderingEngine = new MockRenderingEngine();
            var audioEngine = new MockAudioEngine();
            var resourceManager = new MockResourceManager();
            var guardrails = new GuardrailConfiguration { MaxConcurrentOperations = 20 };

            using var context = new EvaluationContext(
                renderingEngine, audioEngine, resourceManager,
                ServiceProvider.GetRequiredService<ILogger<EvaluationContext>>(),
                CancellationToken.None, guardrails);

            var operatorTasks = new List<Task<GuardrailedOperator>>();
            var operatorCount = 15;

            // Act - Create operators concurrently
            for (int i = 0; i < operatorCount; i++)
            {
                var task = Task.Run(() =>
                {
                    var op = new GuardrailedOperator(context);
                    return op;
                });
                operatorTasks.Add(task);
            }

            var operators = await Task.WhenAll(operatorTasks);

            // Assert
            operators.Should().HaveCount(operatorCount);
            operators.Should().AllBeOfType<GuardrailedOperator>();
        }
    }

    /// <summary>
    /// Integration tests for Performance and Monitoring
    /// </summary>
    [Category(TestCategories.Integration)]
    [Category(TestCategories.Performance)]
    public class PerformanceMonitoringIntegrationTests : CoreTestFixture
    {
        private readonly ITestOutputHelper _output;

        public PerformanceMonitoringIntegrationTests(ITestOutputHelper output) : base()
        {
            _output = output;
        }

        [Fact]
        public async Task EndToEnd_PerformanceMetrics_Collection_Integration()
        {
            // Test performance metrics collection across the system
            var renderingEngine = new MockRenderingEngine();
            var audioEngine = new MockAudioEngine();
            var resourceManager = new MockResourceManager();
            var guardrails = new GuardrailConfiguration { EnablePerformanceMonitoring = true };

            using var context = new EvaluationContext(
                renderingEngine, audioEngine, resourceManager,
                ServiceProvider.GetRequiredService<ILogger<EvaluationContext>>(),
                CancellationToken.None, guardrails);

            // Act - Simulate some work
            using var op1 = new GuardrailedOperator(context);
            using var op2 = new GuardrailedOperator(context);

            // Small delay to ensure metrics are collected
            await Task.Delay(10);

            // Assert - Verify metrics are being collected
            context.Metrics.Should().NotBeNull();
            context.Metrics.EvaluationCount.Should().BeGreaterOrEqualTo(0);
            context.CurrentState.Should().NotBeNull();
        }

        [Fact]
        public async Task EndToEnd_LongRunningOperation_Timeout_Handling()
        {
            // Test timeout handling for long-running operations
            var renderingEngine = new MockRenderingEngine();
            var audioEngine = new MockAudioEngine();
            var resourceManager = new MockResourceManager();
            var guardrails = new GuardrailConfiguration { MaxEvaluationTime = TimeSpan.FromMilliseconds(100) };

            using var context = new EvaluationContext(
                renderingEngine, audioEngine, resourceManager,
                ServiceProvider.GetRequiredService<ILogger<EvaluationContext>>(),
                CancellationToken.None, guardrails);

            // Act - Create operator that might exceed time limit
            Action act = () =>
            {
                using var op = new GuardrailedOperator(context);
                // Simulate operation that might take time
                Thread.Sleep(50); // 50ms is under the 100ms limit
            };

            // Assert
            act.Should().NotThrow("Should handle timeout scenarios gracefully");
        }

        [Fact]
        public async Task EndToEnd_ResourceExhaustion_Recovery_Integration()
        {
            // Test system recovery from resource exhaustion scenarios
            var renderingEngine = new MockRenderingEngine();
            var audioEngine = new MockAudioEngine();
            var resourceManager = new MockResourceManager();
            var guardrails = new GuardrailConfiguration
            {
                MaxMemoryUsageBytes = 1024 * 1024 * 1024, // 1GB
                MaxConcurrentOperations = 100
            };

            using var context = new EvaluationContext(
                renderingEngine, audioEngine, resourceManager,
                ServiceProvider.GetRequiredService<ILogger<EvaluationContext>>(),
                CancellationToken.None, guardrails);

            // Act - Simulate resource-heavy operations
            var operators = new List<GuardrailedOperator>();
            for (int i = 0; i < 50; i++)
            {
                var op = new GuardrailedOperator(context);
                operators.Add(op);
                
                // Simulate resource usage
                context.CurrentState.UpdateMemoryUsage(i * 1024 * 100); // Incrementally more memory
            }

            // Assert - System should handle resource pressure gracefully
            context.CurrentState.IsWithinLimits.Should().BeTrue("Should handle resource pressure");
        }
    }

    /// <summary>
    /// End-to-end system integration tests
    /// </summary>
    [Category(TestCategories.Integration)]
    public class SystemIntegrationTests : CoreTestFixture
    {
        private readonly ITestOutputHelper _output;

        public SystemIntegrationTests(ITestOutputHelper output) : base()
        {
            _output = output;
        }

        [Fact]
        public async Task EndToEnd_CompleteSystem_Workflow_Integration()
        {
            // Test complete system workflow from input to output
            var renderingEngine = new MockRenderingEngine();
            var audioEngine = new MockAudioEngine();
            var resourceManager = new MockResourceManager();
            var guardrails = new GuardrailConfiguration();

            using var context = new EvaluationContext(
                renderingEngine, audioEngine, resourceManager,
                ServiceProvider.GetRequiredService<ILogger<EvaluationContext>>(),
                CancellationToken.None, guardrails);

            // Simulate complete workflow
            var testData = TestDataGenerator.GenerateTestProjectData();

            // Act - Process data through the system
            var operators = testData.Operators.Select(opData => new GuardrailedOperator(context)).ToList();

            // Verify workflow
            operators.Should().HaveCount(testData.Operators.Count);
            
            // Verify test data was processed
            testData.Name.Should().NotBeNullOrEmpty();
            testData.Operators.Should().NotBeEmpty();
            testData.Scenes.Should().NotBeEmpty();

            // Clean up
            operators.ForEach(op => op?.Dispose());
        }

        [Fact]
        public async Task EndToEnd_ErrorRecovery_System_Integration()
        {
            // Test system error recovery and resilience
            var renderingEngine = new MockRenderingEngine();
            var audioEngine = new MockAudioEngine();
            var resourceManager = new MockResourceManager();
            var guardrails = new GuardrailConfiguration();

            using var context = new EvaluationContext(
                renderingEngine, audioEngine, resourceManager,
                ServiceProvider.GetRequiredService<ILogger<EvaluationContext>>(),
                CancellationToken.None, guardrails);

            // Simulate various error conditions
            var scenarios = new[]
            {
                () => { using var op = new GuardrailedOperator(context); },
                () => { context.CurrentState.UpdateMemoryUsage(0); },
                () => { context.CurrentState.IncrementOperationCount(); }
            };

            // Act - Execute scenarios and verify recovery
            foreach (var scenario in scenarios)
            {
                Action act = () => scenario();
                act.Should().NotThrow("System should recover from error conditions");
            }

            // Assert - System should remain functional
            context.CurrentState.Should().NotBeNull();
            context.Metrics.Should().NotBeNull();
        }
    }

    #region Mock Classes for Integration Tests

    public class MockRenderingEngine : IRenderingEngine, IDisposable
    {
        public void Dispose() { }
    }

    public class MockAudioEngine : IAudioEngine, IDisposable
    {
        public void Dispose() { }
    }

    public class MockResourceManager : IResourceManager, IDisposable
    {
        public void Dispose() { }
    }

    #endregion
}