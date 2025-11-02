using System;
using System.Threading.Tasks;
using Vortice.Direct3D12;
using Vortice.DXGI;
using TiXL.Core.Graphics.PSO;
using TiXL.Core.Graphics.DirectX12.PSO;
using TiXL.Core.Performance;
using Microsoft.Extensions.Logging;

namespace TiXL.Examples
{
    /// <summary>
    /// Demo application showing how to use the Real PSO Cache with DirectX 12
    /// Demonstrates PSO creation, caching, and performance optimization
    /// </summary>
    public class RealPSOCacheDemo
    {
        private readonly ILogger<RealPSOCacheDemo> _logger;
        private DirectX12RenderingEngine _renderingEngine;
        private ID3D12Device5 _device;
        private ID3D12CommandQueue _commandQueue;
        
        public RealPSOCacheDemo()
        {
            _logger = Logger.CreateLogger<RealPSOCacheDemo>();
        }
        
        /// <summary>
        /// Initialize DirectX 12 and PSO cache system
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                _logger.LogInformation("Initializing DirectX 12 PSO Cache Demo");
                
                // Create DirectX 12 device and command queue
                await CreateDirectX12ObjectsAsync();
                
                // Create rendering engine with PSO cache integration
                var performanceMonitor = new PerformanceMonitor();
                var config = new RenderingEngineConfig
                {
                    EnableGpuProfiling = true,
                    EnableAutoOptimization = true,
                    TargetFrameTimeMs = 16.67
                };
                
                _renderingEngine = new DirectX12RenderingEngine(
                    _device, 
                    _commandQueue, 
                    performanceMonitor, 
                    null, 
                    config);
                
                // Initialize rendering engine
                if (!await _renderingEngine.InitializeAsync())
                {
                    _logger.LogError("Failed to initialize rendering engine");
                    return false;
                }
                
                // Initialize PSO Cache Service
                var psoConfig = new PSOCacheServiceConfig
                {
                    CacheConfig = new PSOCacheConfig
                    {
                        MaxCacheSize = 500,
                        CacheExpiration = TimeSpan.FromHours(2),
                        EnablePrecompilation = true
                    },
                    EnableBackgroundPrecompilation = true,
                    MaxConcurrentPrecompilation = 4,
                    SlowOperationThresholdMs = 100
                };
                
                if (!await _renderingEngine.InitializePSOCacheAsync(psoConfig))
                {
                    _logger.LogError("Failed to initialize PSO cache service");
                    return false;
                }
                
                _logger.LogInformation("DirectX 12 PSO Cache Demo initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize PSO Cache Demo");
                return false;
            }
        }
        
        /// <summary>
        /// Demo PSO creation and caching
        /// </summary>
        public async Task DemoPSOCreationAndCaching()
        {
            _logger.LogInformation("=== PSO Creation and Caching Demo ===");
            
            try
            {
                // Demo 1: Create materials and register them
                var materials = await CreateDemoMaterialsAsync();
                
                foreach (var material in materials)
                {
                    _logger.LogInformation("Registering material: {MaterialName}", material.MaterialName);
                    var result = await _renderingEngine.RegisterMaterialAsync(material);
                    _logger.LogInformation("Registration result: IsNew={IsNew}, WasUpdated={WasUpdated}", 
                        result.IsNew, result.WasUpdated);
                }
                
                // Demo 2: Get PSOs for the materials (demonstrates caching)
                _logger.LogInformation("\n=== Getting PSOs (Cache Test) ===");
                
                var psoResults = new System.Collections.Generic.List<RealPipelineStateResult>();
                foreach (var material in materials)
                {
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    var psoResult = await _renderingEngine.GetMaterialPSOAsync(material.MaterialName);
                    stopwatch.Stop();
                    
                    psoResults.Add(psoResult);
                    
                    _logger.LogInformation("PSO for {MaterialName}: Cached={WasCached}, CreationTime={CreationTime}ms, LookupTime={LookupTime}ms",
                        material.MaterialName, 
                        psoResult.WasCached,
                        psoResult.CreationTime.TotalMilliseconds,
                        stopwatch.Elapsed.TotalMilliseconds);
                }
                
                // Demo 3: Re-get PSOs to demonstrate cache hits
                _logger.LogInformation("\n=== Re-getting PSOs (Cache Hit Test) ===");
                
                foreach (var material in materials)
                {
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    var psoResult = await _renderingEngine.GetMaterialPSOAsync(material.MaterialName);
                    stopwatch.Stop();
                    
                    _logger.LogInformation("PSO Cache Hit for {MaterialName}: Cached={WasCached}, LookupTime={LookupTime}ms",
                        material.MaterialName, 
                        psoResult.WasCached,
                        stopwatch.Elapsed.TotalMilliseconds);
                }
                
                // Demo 4: Create PSO from shader files
                await DemoShaderBasedPSOCreation();
                
                // Demo 5: Performance optimization
                await DemoPerformanceOptimization();
                
                // Demo 6: Statistics and monitoring
                await DemoStatisticsAndMonitoring();
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during PSO demo");
                throw;
            }
        }
        
        /// <summary>
        /// Demo shader-based PSO creation
        /// </summary>
        private async Task DemoShaderBasedPSOCreation()
        {
            _logger.LogInformation("\n=== Shader-Based PSO Creation Demo ===");
            
            var shaderPairs = new[]
            {
                ("BasicLit", "BasicLitVS.hlsl", "BasicLitPS.hlsl"),
                ("Transparent", "TransparentVS.hlsl", "TransparentPS.hlsl"),
                ("Emissive", "EmissiveVS.hlsl", "EmissivePS.hlsl")
            };
            
            foreach (var (materialName, vertexShader, pixelShader) in shaderPairs)
            {
                try
                {
                    var psoResult = await _renderingEngine.CreatePSOFromShadersAsync(
                        materialName, 
                        $"Shaders/{vertexShader}", 
                        $"Shaders/{pixelShader}");
                    
                    _logger.LogInformation("Created PSO from shaders for {MaterialName}: CreationTime={CreationTime}ms",
                        materialName, psoResult.CreationTime.TotalMilliseconds);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create PSO for {MaterialName} (this is expected in demo)", materialName);
                }
            }
        }
        
        /// <summary>
        /// Demo performance optimization features
        /// </summary>
        private async Task DemoPerformanceOptimization()
        {
            _logger.LogInformation("\n=== Performance Optimization Demo ===");
            
            // Precompile all materials
            _logger.LogInformation("Precompiling all materials...");
            await _renderingEngine.PrecompileAllMaterialsAsync();
            _logger.LogInformation("Precompilation completed");
            
            // Optimize cache
            _logger.LogInformation("Optimizing PSO cache...");
            _renderingEngine.OptimizePSOCache();
            _logger.LogInformation("Cache optimization completed");
            
            // Wait a bit to let precompilation complete
            await Task.Delay(1000);
        }
        
        /// <summary>
        /// Demo statistics and monitoring
        /// </summary>
        private async Task DemoStatisticsAndMonitoring()
        {
            _logger.LogInformation("\n=== Statistics and Monitoring Demo ===");
            
            // Get comprehensive statistics
            var engineStats = _renderingEngine.Statistics;
            var psoStats = _renderingEngine.GetPSOCacheStatistics();
            
            _logger.LogInformation("Engine Statistics:\n{EngineStats}", 
                GetFormattedEngineStats(engineStats));
            
            _logger.LogInformation("PSO Cache Statistics:\n{PsoStats}", 
                psoStats.GetFormattedReport());
            
            // Monitor for a few frames to collect performance data
            _logger.LogInformation("Monitoring performance for 5 seconds...");
            
            for (int i = 0; i < 5; i++)
            {
                var currentStats = _renderingEngine.GetPSOCacheStatistics();
                var currentEngineStats = _renderingEngine.Statistics;
                
                _logger.LogInformation("Frame {Frame}: Cache Hit Rate: {HitRate:P1}, Avg Creation Time: {AvgTime}ms",
                    i + 1, 
                    currentStats.CacheStatistics.CacheHitRate,
                    currentStats.CacheStatistics.AverageCreationTimeMs);
                
                await Task.Delay(1000);
            }
        }
        
        /// <summary>
        /// Demo cache operations (clear, remove materials)
        /// </summary>
        public async Task DemoCacheOperations()
        {
            _logger.LogInformation("\n=== Cache Operations Demo ===");
            
            // Clear specific materials
            _logger.LogInformation("Removing material 'DemoMaterial1'...");
            // Note: This would need a public method on the PSO cache service
            
            // Clear entire cache
            _logger.LogInformation("Clearing entire PSO cache...");
            _renderingEngine.ClearPSOCache();
            _logger.LogInformation("Cache cleared");
            
            // Verify cache is empty
            var statsAfterClear = _renderingEngine.GetPSOCacheStatistics();
            _logger.LogInformation("Cache entries after clear: {Entries}", statsAfterClear.CacheStatistics.CacheEntries);
        }
        
        /// <summary>
        /// Run complete demo suite
        /// </summary>
        public async Task RunCompleteDemoAsync()
        {
            _logger.LogInformation("Starting Complete PSO Cache Demo Suite");
            
            try
            {
                if (!await InitializeAsync())
                {
                    _logger.LogError("Demo initialization failed");
                    return;
                }
                
                await DemoPSOCreationAndCaching();
                await DemoPerformanceOptimization();
                await DemoStatisticsAndMonitoring();
                await DemoCacheOperations();
                
                _logger.LogInformation("PSO Cache Demo Suite completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Demo suite failed");
                throw;
            }
        }
        
        private async Task CreateDirectX12ObjectsAsync()
        {
            // Create DirectX 12 device
            var factory = DXGI.DXGI.CreateDXGIFactory1<IDXGIFactory4>();
            var adapter = factory.EnumAdapters(0)[0];
            _device = D3D12.D3D12Device.CreateD3D12Device(adapter);
            
            // Create command queue
            var commandQueueDesc = new CommandQueueDescription(D3D12_COMMAND_LIST_TYPE_DIRECT);
            _commandQueue = _device.CreateCommandQueue(commandQueueDesc);
            
            _logger.LogInformation("DirectX 12 objects created successfully");
        }
        
        private async Task<MaterialPSOKey[]> CreateDemoMaterialsAsync()
        {
            return new[]
            {
                new MaterialPSOKey
                {
                    MaterialName = "DemoMaterial1",
                    VertexShaderPath = "Shaders/BasicVS.hlsl",
                    PixelShaderPath = "BasicPS.hlsl",
                    PrimitiveTopologyType = PrimitiveTopologyType.Triangle,
                    RTVFormats = Format.R8G8B8A8_UNorm,
                    DSVFormat = Format.D24_UNorm_S8_UInt,
                    SampleDescription = new SampleDescription(1, 0),
                    ShaderMacros = new System.Collections.Generic.List<ShaderMacro>
                    {
                        new ShaderMacro { Name = "USE_NORMAL_MAPPING", Value = "1" },
                        new ShaderMacro { Name = "USE_SPECULAR", Value = "1" }
                    }
                },
                new MaterialPSOKey
                {
                    MaterialName = "DemoMaterial2",
                    VertexShaderPath = "Shaders/SkinnedVS.hlsl",
                    PixelShaderPath = "SkinnedPS.hlsl",
                    PrimitiveTopologyType = PrimitiveTopologyType.Triangle,
                    RTVFormats = Format.R8G8B8A8_UNorm,
                    DSVFormat = Format.D24_UNorm_S8_UInt,
                    SampleDescription = new SampleDescription(1, 0),
                    ShaderMacros = new System.Collections.Generic.List<ShaderMacro>
                    {
                        new ShaderMacro { Name = "ENABLE_SKINNING", Value = "1" },
                        new ShaderMacro { Name = "USE_NORMAL_MAPPING", Value = "1" }
                    }
                },
                new MaterialPSOKey
                {
                    MaterialName = "DemoMaterial3",
                    VertexShaderPath = "Shaders/TransparentVS.hlsl",
                    PixelShaderPath = "TransparentPS.hlsl",
                    PrimitiveTopologyType = PrimitiveTopologyType.Triangle,
                    RTVFormats = Format.R8G8B8A8_UNorm,
                    DSVFormat = Format.D24_UNorm_S8_UInt,
                    SampleDescription = new SampleDescription(1, 0),
                    ShaderMacros = new System.Collections.Generic.List<ShaderMacro>
                    {
                        new ShaderMacro { Name = "USE_TRANSPARENCY", Value = "1" },
                        new ShaderMacro { Name = "ENABLE_ALPHA_BLEND", Value = "1" }
                    }
                }
            };
        }
        
        private string GetFormattedEngineStats(DirectX12RenderingEngineStats stats)
        {
            return $@"DirectX 12 Rendering Engine Statistics:
    Frame ID: {stats.FrameId}
    Is Running: {stats.IsRunning}
    Frame Pacing: {(stats.FramePacing?.AverageFrameTimeMs ?? 0):F2}ms avg
    Resource Management: {stats.ResourceManagement?.OperationsPerSecond ?? 0} ops/sec
    GPU Profiling: {(stats.GpuProfiling?.AverageGpuTime ?? 0):F2}ms avg
    Performance: {stats.Performance?.GetFormattedString() ?? "N/A"}";
        }
        
        public void Dispose()
        {
            _renderingEngine?.Dispose();
            _device?.Dispose();
            _commandQueue?.Dispose();
            
            _logger.LogInformation("RealPSOCacheDemo disposed");
        }
    }
    
    /// <summary>
    /// Entry point for PSO cache demo
    /// </summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Setup logging
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            Logger.SetLoggerFactory(loggerFactory);
            
            var demo = new RealPSOCacheDemo();
            
            try
            {
                await demo.RunCompleteDemoAsync();
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Demo failed: {ex.Message}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
            finally
            {
                demo?.Dispose();
            }
        }
    }
}