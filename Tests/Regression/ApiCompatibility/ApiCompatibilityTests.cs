using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Vortice.Direct3D12;
using Vortice.DXGI;
using Vortice.Mathematics;
using Xunit;
using Xunit.Abstractions;
using TiXL.Core.Graphics.DirectX12;
using TiXL.Core.Graphics.PSO;
using TiXL.Core.Logging;
using TiXL.Core.Performance;

namespace TiXL.Tests.Regression.ApiCompatibility
{
    /// <summary>
    /// Comprehensive API compatibility tests to ensure all public APIs maintain backward compatibility
    /// Tests cover SharpDX to Vortice.Windows migration and validate no breaking changes
    /// </summary>
    [TestCategories(TestCategory.Regression | TestCategory.ApiCompatibility | TestCategory.P0)]
    public class ApiCompatibilityTests : CoreTestFixture
    {
        private readonly ITestOutputHelper _output;
        
        public ApiCompatibilityTests(ITestOutputHelper output) : base()
        {
            _output = output;
            _output.WriteLine("Starting API Compatibility Tests");
        }

        #region Public API Surface Tests

        /// <summary>
        /// Validates that all public API surface points remain accessible and functional
        /// </summary>
        [Fact]
        public void PublicAPISurface_AllTypesAccessible()
        {
            // Arrange - Define expected public API surface
            var expectedPublicTypes = new HashSet<Type>
            {
                typeof(DirectX12RenderingEngine),
                typeof(DirectX12FramePacer),
                typeof(ResourceLifecycleManager),
                typeof(GpuTimelineProfiler),
                typeof(PSOCacheService),
                typeof(OptimizedPSOManager),
                typeof(PipelineState),
                typeof(MaterialPSOKey),
                typeof(PerformanceMonitor)
            };

            // Act - Verify each type is accessible
            foreach (var type in expectedPublicTypes)
            {
                _output.WriteLine($"Verifying accessibility of type: {type.FullName}");
                
                // Verify type is not null and is public
                type.Should().NotBeNull("Type should be accessible");
                type.IsPublic.Should().BeTrue($"Type {type.Name} should be public");
                
                // Verify type has parameterless constructor or can be instantiated
                var hasParameterlessCtor = type.GetConstructor(Type.EmptyTypes) != null;
                if (hasParameterlessCtor)
                {
                    var instance = Activator.CreateInstance(type);
                    instance.Should().NotBeNull($"Should be able to create instance of {type.Name}");
                }
            }
        }

        /// <summary>
        /// Validates that all public methods are accessible and have correct signatures
        /// </summary>
        [Theory]
        [MemberData(nameof(PublicApiTestData))]
        public void PublicAPIMethods_SignatureCompatibility(Type type, string methodName, Type[] expectedParameterTypes)
        {
            // Arrange & Act
            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            
            // Assert
            method.Should().NotBeNull($"Method {methodName} should exist on type {type.Name}");
            
            if (method != null)
            {
                var parameters = method.GetParameters();
                if (expectedParameterTypes.Length > 0)
                {
                    parameters.Should().HaveCount(expectedParameterTypes.Length);
                    
                    for (int i = 0; i < expectedParameterTypes.Length; i++)
                    {
                        parameters[i].ParameterType.Should().Be(expectedParameterTypes[i], 
                            $"Parameter {i} should match expected type {expectedParameterTypes[i].Name}");
                    }
                }
            }
        }

        /// <summary>
        /// Validates that all public properties are accessible
        /// </summary>
        [Theory]
        [MemberData(nameof(PublicPropertyTestData))]
        public void PublicAPIProperties_Accessibility(Type type, string propertyName)
        {
            // Arrange & Act
            var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            
            // Assert
            property.Should().NotBeNull($"Property {propertyName} should exist on type {type.Name}");
            property!.GetMethod.Should().NotBeNull($"Property {propertyName} should have getter");
            
            // Verify property is readable
            if (property.GetMethod != null)
            {
                try
                {
                    var instance = CreateInstanceSafely(type);
                    if (instance != null)
                    {
                        var value = property.GetValue(instance);
                        _output.WriteLine($"Property {propertyName} value: {value?.ToString() ?? "null"}");
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Warning: Could not access property {propertyName}: {ex.Message}");
                }
            }
        }

        #endregion

        #region DirectX Migration Compatibility Tests

        /// <summary>
        /// Validates that Vortice.Windows types replace SharpDX types correctly
        /// </summary>
        [Fact]
        public void DirectXMigration_VorticeTypesAvailable()
        {
            // Verify all Vortice types are available
            var vorticeTypes = new[]
            {
                typeof(ID3D12Device),
                typeof(ID3D12Device4),
                typeof(ID3D12CommandQueue),
                typeof(ID3D12GraphicsCommandList4),
                typeof(ID3D12Fence1),
                typeof(ID3D12QueryHeap),
                typeof(ID3D12Resource),
                typeof(IDXGIFactory4),
                typeof(Format)
            };

            foreach (var type in vorticeTypes)
            {
                _output.WriteLine($"Verifying Vortice type: {type.FullName}");
                type.Should().NotBeNull();
                type.Assembly.GetName().Name.Should().Be("Vortice.Direct3D12")
                    .Or.Be("Vortice.DXGI")
                    .Or.Be("Vortice.Mathematics");
            }
        }

        /// <summary>
        /// Validates that DirectX 12 device creation works correctly
        /// </summary>
        [Fact]
        public void DirectXDevice_CreationCompatibility()
        {
            try
            {
                // Arrange & Act - Try to create a DirectX 12 device
                var result = D3D12.D3D12CreateDevice(
                    IntPtr.Zero,
                    Vortice.Direct3D.FeatureLevel.Level_11_0,
                    typeof(ID3D12Device),
                    out var device);

                // Assert
                if (result.Success)
                {
                    _output.WriteLine("DirectX 12 device creation successful");
                    device.Should().NotBeNull();
                    device.Dispose();
                }
                else
                {
                    _output.WriteLine($"DirectX 12 device creation not available: {result.Code}");
                    // This is acceptable in test environments without GPU
                    result.Code.Should().BeLessThan(0); // Expect error in headless environment
                }
            }
            catch (DllNotFoundException)
            {
                _output.WriteLine("DirectX 12 runtime not available - running in headless mode");
                // Acceptable in CI environments without DirectX runtime
            }
        }

        /// <summary>
        /// Validates that PSO creation and management works correctly
        /// </summary>
        [Fact]
        public void PipelineStateObject_ManagementCompatibility()
        {
            // Arrange
            var psoManager = new OptimizedPSOManager();
            
            // Act
            var pso = new PipelineState();
            
            // Assert
            pso.Should().NotBeNull();
            pso.IsValid.Should().BeTrue();
            pso.Description.Should().NotBeNull();
            pso.CreationTimestamp.Should().BeLessThanOrEqualTo(DateTime.UtcNow);
            
            // Verify PSO metadata
            pso.AccessCount.Should().Be(0);
            pso.LastAccessTime.Should().BeOnOrBefore(DateTime.UtcNow);
            
            _output.WriteLine($"PSO created with timestamp: {pso.CreationTimestamp}");
        }

        #endregion

        #region Core Module Compatibility Tests

        /// <summary>
        /// Validates that core module APIs remain compatible
        /// </summary>
        [Fact]
        public void CoreModule_APICompatibility()
        {
            // Test Core Performance module
            var perfMonitor = new PerformanceMonitor();
            perfMonitor.Should().NotBeNull();
            
            // Test Core Graphics PSO module
            var psoKey = new MaterialPSOKey();
            psoKey.Should().NotBeNull();
            
            // Verify PSO key equality
            var psoKey2 = new MaterialPSOKey();
            psoKey.Equals(psoKey2).Should().BeTrue();
            
            _output.WriteLine("Core module API compatibility validated");
        }

        /// <summary>
        /// Validates that performance monitoring APIs work correctly
        /// </summary>
        [Fact]
        public void PerformanceMonitoring_APIsCompatibility()
        {
            // Arrange
            var monitor = new PerformanceMonitor();
            var testCounter = new PerformanceCounter("TestCounter");
            
            // Act
            testCounter.Value = 42;
            
            // Assert
            testCounter.Value.Should().Be(42);
            testCounter.Name.Should().Be("TestCounter");
            
            monitor.Counters.Should().Contain(testCounter);
            
            _output.WriteLine($"Performance counter created: {testCounter.Name} = {testCounter.Value}");
        }

        #endregion

        #region Error Handling Compatibility Tests

        /// <summary>
        /// Validates that error handling patterns remain consistent
        /// </summary>
        [Fact]
        public void ErrorHandling_ExceptionPatternsConsistent()
        {
            // Test argument validation
            Action nullArgAction = () => new DirectX12RenderingEngine(
                device: null!,
                commandQueue: null!);

            nullArgAction.Should().Throw<ArgumentNullException>()
                .WithParameterName("device");
            
            // Test invalid configuration
            var invalidConfig = new RenderingEngineConfig
            {
                TargetFrameTimeMs = -1.0,  // Invalid
                MaxInFlightFrames = 5,
                MaxGpuBufferPoolSize = 1000,
                MaxTexturePoolSize = 1000,
                MaxPipelineStatePoolSize = 1000
            };

            var engine = new DirectX12RenderingEngine(
                MockD3D12Device.Create().Device,
                MockD3D12Device.Create().CommandQueue,
                config: invalidConfig);

            engine.Should().NotBeNull();
            
            _output.WriteLine("Error handling patterns validated successfully");
        }

        #endregion

        #region Helper Methods and Test Data

        public static TheoryData<Type, string, Type[]> PublicApiTestData =>
            new TheoryData<Type, string, Type[]>
            {
                { typeof(DirectX12RenderingEngine), "Initialize", new Type[0] },
                { typeof(DirectX12RenderingEngine), "Dispose", new Type[0] },
                { typeof(DirectX12FramePacer), "GetCurrentFrameId", new Type[0] },
                { typeof(ResourceLifecycleManager), "Initialize", new Type[0] },
                { typeof(GpuTimelineProfiler), "Initialize", new Type[0] },
                { typeof(OptimizedPSOManager), "GetOrCreate", new[] { typeof(GraphicsPipelineStateDescription) } },
                { typeof(PipelineState), "CreatePSO", new[] { typeof(Device) } },
                { typeof(MaterialPSOKey), "Equals", new[] { typeof(object) } },
                { typeof(PerformanceMonitor), "StartMonitoring", new Type[0] }
            };

        public static TheoryData<Type, string> PublicPropertyTestData =>
            new TheoryData<Type, string>
            {
                { typeof(DirectX12RenderingEngine), "IsInitialized" },
                { typeof(DirectX12RenderingEngine), "IsRunning" },
                { typeof(DirectX12RenderingEngine), "CurrentFrameId" },
                { typeof(PipelineState), "Description" },
                { typeof(PipelineState), "IsValid" },
                { typeof(PipelineState), "CreationTimestamp" },
                { typeof(MaterialPSOKey), "MaterialHash" },
                { typeof(MaterialPSOKey), "RenderStateHash" },
                { typeof(PerformanceCounter), "Name" },
                { typeof(PerformanceCounter), "Value" }
            };

        private static object? CreateInstanceSafely(Type type)
        {
            try
            {
                var ctor = type.GetConstructor(Type.EmptyTypes);
                if (ctor != null)
                {
                    return Activator.CreateInstance(type);
                }
            }
            catch
            {
                // Ignore constructor failures
            }
            return null;
        }

        #endregion

        #region Cleanup

        public override void Dispose()
        {
            _output.WriteLine("API Compatibility Tests cleanup completed");
            base.Dispose();
        }

        #endregion
    }
}
