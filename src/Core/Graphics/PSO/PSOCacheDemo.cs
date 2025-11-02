using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Vortice.Direct3D12;
using Vortice.DXGI;

namespace TiXL.Examples.Graphics
{
    /// <summary>
    /// Example application demonstrating PSO caching system usage
    /// Shows performance benefits and integration patterns
    /// </summary>
    class PSOCacheDemo
    {
        private static Device _device;
        private static PSO.OptimizedPSOManager _psoManager;
        private static PSO.PSODebugTools _debugTools;
        
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== TiXL PSO Caching System Demo ===");
            Console.WriteLine();
            
            // Initialize DirectX 12 device
            await InitializeDevice();
            
            // Initialize PSO manager
            InitializePSOManager();
            
            // Run demo scenarios
            await RunBasicCachingDemo();
            await RunPerformanceBenchmark();
            await RunMaterialIntegrationDemo();
            await RunDebugToolsDemo();
            
            // Cleanup
            _psoManager.Dispose();
            _device.Dispose();
            
            Console.WriteLine();
            Console.WriteLine("Demo completed. Press any key to exit...");
            Console.ReadKey();
        }
        
        private static async Task InitializeDevice()
        {
            Console.WriteLine("Initializing DirectX 12 device...");
            
            var factory = new Factory4();
            var adapter = factory.GetAdapter(0);
            _device = new Device(adapter);
            
            Console.WriteLine($"Device created: {adapter.Description1.Description}");
            Console.WriteLine($"Feature Level: {_device.FeatureLevel}");
            Console.WriteLine();
        }
        
        private static void InitializePSOManager()
        {
            Console.WriteLine("Initializing PSO caching system...");
            
            _psoManager = new PSO.OptimizedPSOManager(_device, initialCapacity: 50);
            _debugTools = new PSO.PSODebugTools(_psoManager);
            
            Console.WriteLine("PSO manager initialized with 50 entry cache capacity");
            Console.WriteLine();
        }
        
        private static async Task RunBasicCachingDemo()
        {
            Console.WriteLine("=== Basic Caching Demo ===");
            Console.WriteLine();
            
            // Create a test material
            var materialKey = CreateTestMaterial("DemoMaterial");
            
            Console.WriteLine("Step 1: Creating PSO for the first time (cache miss)...");
            var stopwatch = Stopwatch.StartNew();
            var pso1 = await _psoManager.GetOrCreatePSOAsync(materialKey);
            stopwatch.Stop();
            
            Console.WriteLine($"PSO created in {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
            Console.WriteLine($"PSO is valid: {pso1.PipelineState.IsValid}");
            Console.WriteLine();
            
            Console.WriteLine("Step 2: Accessing the same material again (cache hit)...");
            stopwatch.Restart();
            var pso2 = await _psoManager.GetOrCreatePSOAsync(materialKey);
            stopwatch.Stop();
            
            Console.WriteLine($"PSO retrieved in {stopwatch.Elapsed.TotalMilliseconds:F4}ms");
            Console.WriteLine($"Cache hit achieved: {pso1 == pso2}");
            Console.WriteLine($"Performance improvement: {(50 - stopwatch.Elapsed.TotalMilliseconds) / 50 * 100:F1}%");
            Console.WriteLine();
            
            var stats = _psoManager.GetDetailedStatistics();
            Console.WriteLine($"Cache Statistics: Hit Rate = {stats.CacheHitRate:P2}");
            Console.WriteLine();
        }
        
        private static async Task RunPerformanceBenchmark()
        {
            Console.WriteLine("=== Performance Benchmark ===");
            Console.WriteLine();
            
            var materials = new List<PSO.MaterialPSOKey>();
            
            // Create 20 different materials
            for (int i = 0; i < 20; i++)
            {
                materials.Add(CreateTestMaterial($"BenchmarkMaterial{i}"));
            }
            
            Console.WriteLine("Creating 20 unique PSOs...");
            var creationTimes = new List<double>();
            
            foreach (var material in materials)
            {
                var sw = Stopwatch.StartNew();
                await _psoManager.GetOrCreatePSOAsync(material);
                sw.Stop();
                creationTimes.Add(sw.Elapsed.TotalMilliseconds);
                Console.WriteLine($"  {material.MaterialName}: {sw.Elapsed.TotalMilliseconds:F2}ms");
            }
            
            var avgCreationTime = creationTimes.Average();
            Console.WriteLine($"Average PSO creation time: {avgCreationTime:F2}ms");
            Console.WriteLine();
            
            Console.WriteLine("Re-accessing random materials 100 times...");
            var accessTimes = new List<double>();
            
            var random = new Random();
            for (int i = 0; i < 100; i++)
            {
                var material = materials[random.Next(materials.Count)];
                var sw = Stopwatch.StartNew();
                await _psoManager.GetOrCreatePSOAsync(material);
                sw.Stop();
                accessTimes.Add(sw.Elapsed.TotalMilliseconds);
            }
            
            var avgAccessTime = accessTimes.Average();
            Console.WriteLine($"Average access time: {avgAccessTime:F4}ms");
            Console.WriteLine($"Cache hit rate: {_psoManager.GetDetailedStatistics().CacheHitRate:P2}");
            Console.WriteLine($"Performance improvement: {(avgCreationTime - avgAccessTime) / avgCreationTime * 100:F1}%");
            Console.WriteLine();
        }
        
        private static async Task RunMaterialIntegrationDemo()
        {
            Console.WriteLine("=== Material Integration Demo ===");
            Console.WriteLine();
            
            // Create material integration
            var integration = new PSO.PSOMaterialIntegration(_device);
            
            // Register materials
            Console.WriteLine("Registering materials for integration...");
            
            var pbrMaterial = CreateMaterialDescription("PBRMaterial", "Shaders/PBRVS.hlsl", "Shaders/PBRPS.hlsl");
            await integration.RegisterMaterialAsync("PBR", pbrMaterial);
            
            var transparentMaterial = CreateMaterialDescription("TransparentMaterial", "Shaders/TransparentVS.hlsl", "Shaders/TransparentPS.hlsl");
            transparentMaterial.BlendMode = PSO.MaterialBlendMode.AlphaBlend;
            await integration.RegisterMaterialAsync("Transparent", transparentMaterial);
            
            var emissiveMaterial = CreateMaterialDescription("EmissiveMaterial", "Shaders/EmissiveVS.hlsl", "Shaders/EmissivePS.hlsl");
            emissiveMaterial.BlendMode = PSO.MaterialBlendMode.Additive;
            await integration.RegisterMaterialAsync("Emissive", emissiveMaterial);
            
            Console.WriteLine("Materials registered successfully");
            Console.WriteLine();
            
            // Access materials multiple times
            Console.WriteLine("Accessing registered materials...");
            
            for (int i = 0; i < 10; i++)
            {
                var pbrPSO = await integration.GetMaterialPSOAsync("PBR");
                var transparentPSO = await integration.GetMaterialPSOAsync("Transparent");
                var emissivePSO = await integration.GetMaterialPSOAsync("Emissive");
                
                if (i % 3 == 0)
                {
                    Console.WriteLine($"  Iteration {i}: All materials accessed successfully");
                }
            }
            
            var integrationStats = integration.GetStatistics();
            Console.WriteLine($"Integration cache statistics:");
            Console.WriteLine($"  Hit Rate: {integrationStats.CacheHitRate:P2}");
            Console.WriteLine($"  Total Entries: {integrationStats.CacheStatistics.TotalEntries}");
            Console.WriteLine();
            
            integration.Dispose();
        }
        
        private static async Task RunDebugToolsDemo()
        {
            Console.WriteLine("=== Debug Tools Demo ===");
            Console.WriteLine();
            
            // Generate various debug reports
            Console.WriteLine("Generating cache analysis report...");
            var analysisReport = _debugTools.GenerateCacheAnalysisReport();
            Console.WriteLine(analysisReport);
            
            Console.WriteLine("Generating material analysis...");
            var materialAnalysis = _debugTools.GenerateMaterialAnalysis();
            Console.WriteLine(materialAnalysis);
            
            Console.WriteLine("Generating memory breakdown...");
            var memoryBreakdown = _debugTools.GenerateMemoryBreakdown();
            Console.WriteLine(memoryBreakdown);
            
            // Simulate monitoring
            Console.WriteLine("Starting real-time monitoring (5 seconds)...");
            var monitor = new PSO.PSOCacheMonitor(_psoManager, TimeSpan.FromSeconds(1));
            
            var monitoringEndTime = DateTime.UtcNow.AddSeconds(5);
            
            monitor.OnPerformanceUpdate += (sender, entry) =>
            {
                Console.WriteLine($"[{entry.Timestamp:HH:mm:ss}] {entry.GetFormattedString()}");
                
                if (DateTime.UtcNow >= monitoringEndTime)
                {
                    monitor.Dispose();
                }
            };
            
            // Keep the demo running
            await Task.Delay(6000);
            
            Console.WriteLine();
            Console.WriteLine("Benchmark report:");
            var benchmarkReport = _debugTools.GenerateBenchmarkReport(50);
            Console.WriteLine(benchmarkReport);
        }
        
        private static PSO.MaterialPSOKey CreateTestMaterial(string name)
        {
            return new PSO.MaterialPSOKey
            {
                MaterialName = name,
                VertexShaderPath = $"Shaders/{name}VS.hlsl",
                PixelShaderPath = $"Shaders/{name}PS.hlsl",
                PrimitiveTopologyType = PrimitiveTopologyType.Triangle,
                RTVFormats = Format.R8G8B8A8_UNorm,
                DSVFormat = Format.D24_UNorm_S8_UInt,
                SampleDescription = new SampleDescription(1, 0),
                ShaderMacros = new List<PSO.ShaderMacro>
                {
                    new PSO.ShaderMacro { Name = "USE_NORMAL_MAPPING", Value = "1" },
                    new PSO.ShaderMacro { Name = "USE_SPECULAR", Value = "1" },
                    new PSO.ShaderMacro { Name = "DEMO_MODE", Value = "1" }
                },
                MaterialParameters = new Dictionary<string, object>
                {
                    ["BaseColor"] = new Vector4(0.8f, 0.2f, 0.2f, 1.0f),
                    ["Roughness"] = 0.8f,
                    ["Metalness"] = 0.0f,
                    ["EmissiveStrength"] = 1.0f
                }
            };
        }
        
        private static PSO.MaterialDescription CreateMaterialDescription(string name, string vs, string ps)
        {
            return new PSO.MaterialDescription
            {
                VertexShaderPath = vs,
                PixelShaderPath = ps,
                ShaderMacros = new List<PSO.MaterialShaderMacro>
                {
                    new PSO.MaterialShaderMacro { Name = "USE_NORMAL_MAPPING", Value = "1" },
                    new PSO.MaterialShaderMacro { Name = "DEMO", Value = "1" }
                },
                Parameters = new Dictionary<string, object>
                {
                    ["BaseColor"] = new Vector4(0.8f, 0.2f, 0.2f, 1.0f),
                    ["Roughness"] = 0.8f,
                    ["Metalness"] = 0.0f
                },
                BlendMode = PSO.MaterialBlendMode.Opaque,
                DepthMode = PSO.MaterialDepthMode.Default,
                RasterizerMode = PSO.MaterialRasterizerMode.Default
            };
        }
    }
}