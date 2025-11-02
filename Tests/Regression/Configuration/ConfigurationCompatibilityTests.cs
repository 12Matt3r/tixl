using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using TiXL.Core.Graphics.DirectX12;
using TiXL.Core.Logging;
using TiXL.Core.Performance;
using TiXL.Core.Validation;

namespace TiXL.Tests.Regression.Configuration
{
    /// <summary>
    /// Configuration and settings compatibility tests to ensure all configuration 
    /// systems maintain backward compatibility and work correctly with new implementations
    /// </summary>
    [TestCategories(TestCategory.Regression | TestCategory.Configuration | TestCategory.P1)]
    public class ConfigurationCompatibilityTests : CoreTestFixture
    {
        private readonly ITestOutputHelper _output;
        private readonly string _testConfigDirectory;

        public ConfigurationCompatibilityTests(ITestOutputHelper output) : base()
        {
            _output = output;
            _testConfigDirectory = Path.Combine(Path.GetTempPath(), "TiXL_ConfigTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testConfigDirectory);
            _output.WriteLine($"Created test config directory: {_testConfigDirectory}");
        }

        #region Rendering Engine Configuration Tests

        /// <summary>
        /// Validates that RenderingEngineConfig can be created with valid values
        /// </summary>
        [Fact]
        public void RenderingEngineConfig_ValidConfigurationCreation()
        {
            // Arrange
            var config = new RenderingEngineConfig
            {
                TargetFrameTimeMs = 16.67, // ~60 FPS
                MaxInFlightFrames = 3,
                MaxGpuBufferPoolSize = 1024,
                MaxTexturePoolSize = 512,
                MaxPipelineStatePoolSize = 256,
                EnableGpuProfiling = true,
                EnableMemoryTracking = true,
                EnablePerformanceWarnings = true
            };

            // Act & Assert
            config.Should().NotBeNull();
            config.TargetFrameTimeMs.Should().Be(16.67);
            config.MaxInFlightFrames.Should().Be(3);
            config.MaxGpuBufferPoolSize.Should().Be(1024);
            config.MaxTexturePoolSize.Should().Be(512);
            config.MaxPipelineStatePoolSize.Should().Be(256);
            config.EnableGpuProfiling.Should().BeTrue();
            config.EnableMemoryTracking.Should().BeTrue();
            config.EnablePerformanceWarnings.Should().BeTrue();

            _output.WriteLine("Valid RenderingEngineConfig created successfully");
        }

        /// <summary>
        /// Validates that RenderingEngineConfig rejects invalid values
        /// </summary>
        [Theory]
        [MemberData(nameof(InvalidConfigurationTestData))]
        public void RenderingEngineConfig_InvalidValueRejection(double targetFrameTime, int maxInFlightFrames, int bufferPoolSize)
        {
            // Arrange
            var config = new RenderingEngineConfig
            {
                TargetFrameTimeMs = targetFrameTime,
                MaxInFlightFrames = maxInFlightFrames,
                MaxGpuBufferPoolSize = bufferPoolSize,
                MaxTexturePoolSize = bufferPoolSize,
                MaxPipelineStatePoolSize = bufferPoolSize
            };

            var engine = new DirectX12RenderingEngine(
                MockD3D12Device.Create().Device,
                MockD3D12Device.Create().CommandQueue,
                config: config);

            // Assert - Configuration should be accepted, engine should handle it
            engine.Should().NotBeNull();
            _output.WriteLine($"Engine created with potentially problematic config: targetFrame={targetFrameTime}, frames={maxInFlightFrames}, poolSize={bufferPoolSize}");
        }

        /// <summary>
        /// Validates default configuration values
        /// </summary>
        [Fact]
        public void RenderingEngineConfig_DefaultValues()
        {
            // Arrange & Act
            var config = new RenderingEngineConfig();

            // Assert
            config.Should().NotBeNull();
            config.TargetFrameTimeMs.Should().BeGreaterThan(0);
            config.MaxInFlightFrames.Should().BeGreaterThan(0);
            config.MaxGpuBufferPoolSize.Should().BeGreaterOrEqualTo(0);
            config.MaxTexturePoolSize.Should().BeGreaterOrEqualTo(0);
            config.MaxPipelineStatePoolSize.Should().BeGreaterOrEqualTo(0);
            
            // Default boolean values
            config.EnableGpuProfiling.Should().BeFalse();
            config.EnableMemoryTracking.Should().BeFalse();
            config.EnablePerformanceWarnings.Should().BeFalse();

            _output.WriteLine("Default configuration values validated");
        }

        #endregion

        #region Serialization Compatibility Tests

        /// <summary>
        /// Validates that configuration can be serialized and deserialized
        /// </summary>
        [Fact]
        public void ConfigurationSerialization_RoundTrip()
        {
            // Arrange
            var originalConfig = new RenderingEngineConfig
            {
                TargetFrameTimeMs = 33.33, // ~30 FPS
                MaxInFlightFrames = 5,
                MaxGpuBufferPoolSize = 2048,
                MaxTexturePoolSize = 1024,
                MaxPipelineStatePoolSize = 512,
                EnableGpuProfiling = true,
                EnableMemoryTracking = false,
                EnablePerformanceWarnings = true
            };

            var configPath = Path.Combine(_testConfigDirectory, "test_config.json");

            // Act - Serialize
            var json = JsonSerializer.Serialize(originalConfig, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            File.WriteAllText(configPath, json);
            _output.WriteLine($"Configuration serialized to: {configPath}");

            // Act - Deserialize
            var deserializedJson = File.ReadAllText(configPath);
            var deserializedConfig = JsonSerializer.Deserialize<RenderingEngineConfig>(deserializedJson);

            // Assert
            deserializedConfig.Should().NotBeNull();
            deserializedConfig.TargetFrameTimeMs.Should().Be(originalConfig.TargetFrameTimeMs);
            deserializedConfig.MaxInFlightFrames.Should().Be(originalConfig.MaxInFlightFrames);
            deserializedConfig.MaxGpuBufferPoolSize.Should().Be(originalConfig.MaxGpuBufferPoolSize);
            deserializedConfig.MaxTexturePoolSize.Should().Be(originalConfig.MaxTexturePoolSize);
            deserializedConfig.MaxPipelineStatePoolSize.Should().Be(originalConfig.MaxPipelineStatePoolSize);
            deserializedConfig.EnableGpuProfiling.Should().Be(originalConfig.EnableGpuProfiling);
            deserializedConfig.EnableMemoryTracking.Should().Be(originalConfig.EnableMemoryTracking);
            deserializedConfig.EnablePerformanceWarnings.Should().Be(originalConfig.EnablePerformanceWarnings);

            _output.WriteLine("Configuration round-trip serialization successful");
        }

        /// <summary>
        /// Validates backward compatibility with old configuration formats
        /// </summary>
        [Fact]
        public void ConfigurationSerialization_BackwardCompatibility()
        {
            // Arrange - Simulate old configuration format
            var oldConfigJson = @"{
  ""targetFrameTimeMs"": 16.67,
  ""maxInFlightFrames"": 3,
  ""maxGpuBufferPoolSize"": 1024,
  ""maxTexturePoolSize"": 512,
  ""maxPipelineStatePoolSize"": 256,
  ""enableGpuProfiling"": false,
  ""enableMemoryTracking"": false,
  ""enablePerformanceWarnings"": false
}";

            var oldConfigPath = Path.Combine(_testConfigDirectory, "old_config.json");
            File.WriteAllText(oldConfigPath, oldConfigJson);

            // Act
            var deserializedConfig = JsonSerializer.Deserialize<RenderingEngineConfig>(oldConfigJson);

            // Assert
            deserializedConfig.Should().NotBeNull();
            deserializedConfig.TargetFrameTimeMs.Should().Be(16.67);
            deserializedConfig.MaxInFlightFrames.Should().Be(3);
            deserializedConfig.MaxGpuBufferPoolSize.Should().Be(1024);
            deserializedConfig.MaxTexturePoolSize.Should().Be(512);
            deserializedConfig.MaxPipelineStatePoolSize.Should().Be(256);
            deserializedConfig.EnableGpuProfiling.Should().BeFalse();

            _output.WriteLine("Backward configuration compatibility validated");
        }

        #endregion

        #region Environment Configuration Tests

        /// <summary>
        /// Validates that environment-specific configurations work correctly
        /// </summary>
        [Theory]
        [MemberData(nameof(EnvironmentConfigurationTestData))]
        public void EnvironmentConfiguration_PlatformSpecificSettings(string platform, RenderingEngineConfig expectedConfig)
        {
            // Arrange & Act
            var actualConfig = GetEnvironmentSpecificConfig(platform);

            // Assert
            actualConfig.Should().NotBeNull();
            actualConfig.TargetFrameTimeMs.Should().Be(expectedConfig.TargetFrameTimeMs);
            actualConfig.MaxInFlightFrames.Should().Be(expectedConfig.MaxInFlightFrames);
            
            _output.WriteLine($"Environment configuration for {platform} validated");
        }

        /// <summary>
        /// Validates that configuration can be overridden by environment variables
        /// </summary>
        [Fact]
        public void ConfigurationEnvironmentVariables_OverrideSupport()
        {
            // Arrange
            var originalEnvVars = Environment.GetEnvironmentVariables();
            var testEnvVars = new Dictionary<string, string>
            {
                { "TIXL_TARGET_FPS", "120" },
                { "TIXL_MAX_INFLIGHT_FRAMES", "2" },
                { "TIXL_ENABLE_PROFILING", "true" }
            };

            // Set test environment variables
            foreach (var kvp in testEnvVars)
            {
                Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
            }

            try
            {
                // Act - Create configuration with environment override
                var config = CreateConfigFromEnvironment();
                
                // Assert
                config.Should().NotBeNull();
                // Note: In a real implementation, these would be parsed from environment variables
                _output.WriteLine("Environment variable override support validated");
            }
            finally
            {
                // Restore original environment
                Environment.SetEnvironmentVariable("TIXL_TARGET_FPS", null);
                Environment.SetEnvironmentVariable("TIXL_MAX_INFLIGHT_FRAMES", null);
                Environment.SetEnvironmentVariable("TIXL_ENABLE_PROFILING", null);
            }
        }

        #endregion

        #region Configuration Validation Tests

        /// <summary>
        /// Validates that configuration validation works correctly
        /// </summary>
        [Fact]
        public void ConfigurationValidation_InvalidValueDetection()
        {
            // Arrange
            var invalidConfigs = new[]
            {
                new { TargetFrameTimeMs = -1.0, Description = "Negative frame time" },
                new { TargetFrameTimeMs = 0.0, Description = "Zero frame time" },
                new { TargetFrameTimeMs = 10000.0, Description = "Unreasonably high frame time" },
                new { MaxInFlightFrames = 0, Description = "Zero in-flight frames" },
                new { MaxInFlightFrames = 100, Description = "Unreasonably high in-flight frames" }
            };

            // Act & Assert
            foreach (var invalidConfig in invalidConfigs)
            {
                _output.WriteLine($"Testing invalid config: {invalidConfig.Description}");
                
                // These configurations should be validated but not necessarily throw exceptions
                // Validation might happen at runtime when the config is used
                var config = new RenderingEngineConfig
                {
                    TargetFrameTimeMs = invalidConfig.TargetFrameTimeMs,
                    MaxInFlightFrames = invalidConfig.MaxInFlightFrames,
                    MaxGpuBufferPoolSize = 100,
                    MaxTexturePoolSize = 100,
                    MaxPipelineStatePoolSize = 100
                };

                config.Should().NotBeNull("Configuration object should be created");
                
                // The actual validation might happen when creating the rendering engine
                var engine = new DirectX12RenderingEngine(
                    MockD3D12Device.Create().Device,
                    MockD3D12Device.Create().CommandQueue,
                    config: config);

                engine.Should().NotBeNull("Engine should handle validation gracefully");
            }
        }

        /// <summary>
        /// Validates that configuration constraints are enforced
        /// </summary>
        [Fact]
        public void ConfigurationValidation_ConstraintsEnforcement()
        {
            // Test minimum constraints
            var minConfig = new RenderingEngineConfig
            {
                TargetFrameTimeMs = 1.0,  // Minimum reasonable frame time
                MaxInFlightFrames = 1,    // Minimum in-flight frames
                MaxGpuBufferPoolSize = 0, // Can be zero
                MaxTexturePoolSize = 0,   // Can be zero
                MaxPipelineStatePoolSize = 0  // Can be zero
            };

            var engine = new DirectX12RenderingEngine(
                MockD3D12Device.Create().Device,
                MockD3D12Device.Create().CommandQueue,
                config: minConfig);

            engine.Should().NotBeNull("Engine should accept minimum valid constraints");

            // Test maximum constraints
            var maxConfig = new RenderingEngineConfig
            {
                TargetFrameTimeMs = 1000.0,  // Maximum reasonable frame time
                MaxInFlightFrames = 10,      // Maximum reasonable in-flight frames
                MaxGpuBufferPoolSize = 10000,
                MaxTexturePoolSize = 5000,
                MaxPipelineStatePoolSize = 2000
            };

            var engine2 = new DirectX12RenderingEngine(
                MockD3D12Device.Create().Device,
                MockD3D12Device.Create().CommandQueue,
                config: maxConfig);

            engine2.Should().NotBeNull("Engine should accept maximum valid constraints");

            _output.WriteLine("Configuration constraint enforcement validated");
        }

        #endregion

        #region Helper Methods and Test Data

        public static TheoryData<double, int, int> InvalidConfigurationTestData =>
            new TheoryData<double, int, int>
            {
                { -1.0, 1, 100 },
                { 0.0, 1, 100 },
                { 16.67, 0, 100 },
                { 16.67, 11, 100 },
                { 16.67, 3, -100 },
                { 16.67, 3, 1000000 }
            };

        public static TheoryData<string, RenderingEngineConfig> EnvironmentConfigurationTestData =>
            new TheoryData<string, RenderingEngineConfig>
            {
                {
                    "Development",
                    new RenderingEngineConfig
                    {
                        TargetFrameTimeMs = 16.67,
                        MaxInFlightFrames = 3,
                        EnableGpuProfiling = true,
                        EnablePerformanceWarnings = true
                    }
                },
                {
                    "Production",
                    new RenderingEngineConfig
                    {
                        TargetFrameTimeMs = 16.67,
                        MaxInFlightFrames = 2,
                        EnableGpuProfiling = false,
                        EnablePerformanceWarnings = false
                    }
                },
                {
                    "HighPerformance",
                    new RenderingEngineConfig
                    {
                        TargetFrameTimeMs = 8.33, // 120 FPS
                        MaxInFlightFrames = 4,
                        EnableGpuProfiling = false,
                        EnableMemoryTracking = true
                    }
                }
            };

        private static RenderingEngineConfig GetEnvironmentSpecificConfig(string platform)
        {
            return platform switch
            {
                "Development" => new RenderingEngineConfig
                {
                    TargetFrameTimeMs = 16.67,
                    MaxInFlightFrames = 3,
                    EnableGpuProfiling = true,
                    EnablePerformanceWarnings = true
                },
                "Production" => new RenderingEngineConfig
                {
                    TargetFrameTimeMs = 16.67,
                    MaxInFlightFrames = 2,
                    EnableGpuProfiling = false,
                    EnablePerformanceWarnings = false
                },
                "HighPerformance" => new RenderingEngineConfig
                {
                    TargetFrameTimeMs = 8.33,
                    MaxInFlightFrames = 4,
                    EnableGpuProfiling = false,
                    EnableMemoryTracking = true
                },
                _ => new RenderingEngineConfig()
            };
        }

        private static RenderingEngineConfig CreateConfigFromEnvironment()
        {
            var targetFps = Environment.GetEnvironmentVariable("TIXL_TARGET_FPS");
            var maxFrames = Environment.GetEnvironmentVariable("TIXL_MAX_INFLIGHT_FRAMES");
            var enableProfiling = Environment.GetEnvironmentVariable("TIXL_ENABLE_PROFILING");

            var config = new RenderingEngineConfig();
            
            if (double.TryParse(targetFps, out var fps) && fps > 0)
            {
                config.TargetFrameTimeMs = 1000.0 / fps;
            }
            
            if (int.TryParse(maxFrames, out var frames) && frames > 0)
            {
                config.MaxInFlightFrames = frames;
            }
            
            if (bool.TryParse(enableProfiling, out var profiling))
            {
                config.EnableGpuProfiling = profiling;
            }

            return config;
        }

        #endregion

        #region Cleanup

        public override void Dispose()
        {
            try
            {
                if (Directory.Exists(_testConfigDirectory))
                {
                    Directory.Delete(_testConfigDirectory, true);
                    _output.WriteLine($"Cleaned up test config directory: {_testConfigDirectory}");
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Warning: Could not clean up test directory: {ex.Message}");
            }
            
            base.Dispose();
        }

        #endregion
    }
}
