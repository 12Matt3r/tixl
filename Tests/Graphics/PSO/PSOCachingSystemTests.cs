using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using SharpDX.Direct3D12;
using SharpDX.DXGI;
using Xunit;
using Xunit.Abstractions;

namespace TiXL.Tests.Graphics.PSO
{
    /// <summary>
    /// Comprehensive test suite for the PSO caching system
    /// Validates caching performance, memory management, and integration
    /// </summary>
    public class PSOCachingSystemTests
    {
        private readonly ITestOutputHelper _output;
        private readonly Device _device;
        private readonly PSO.OptimizedPSOManager _psoManager;
        
        public PSOCachingSystemTests(ITestOutputHelper output)
        {
            _output = output;
            
            // Initialize test device
            var factory = new Factory4();
            var adapter = factory.GetAdapter(0);
            _device = new Device(adapter);
            _psoManager = new PSO.OptimizedPSOManager(_device, 100);
        }
        
        [Fact]
        public async Task PSO_CacheHit_ImprovesPerformance()
        {
            // Arrange
            var key = CreateTestMaterialKey("TestMaterial1");
            var creationTimes = new List<double>();
            
            // Force creation (cache miss)
            var stopwatch = Stopwatch.StartNew();
            var pso1 = await _psoManager.GetOrCreatePSOAsync(key);
            stopwatch.Stop();
            creationTimes.Add(stopwatch.Elapsed.TotalMilliseconds);
            
            // Cache hit
            stopwatch.Restart();
            var pso2 = await _psoManager.GetOrCreatePSOAsync(key);
            stopwatch.Stop();
            creationTimes.Add(stopwatch.Elapsed.TotalMilliseconds);
            
            // Assert
            Assert.NotNull(pso1);
            Assert.NotNull(pso2);
            Assert.Equal(pso1, pso2); // Should be same instance
            
            var stats = _psoManager.GetDetailedStatistics();
            Assert.True(stats.CacheHitRate > 0, "Should have cache hits");
            
            _output.WriteLine($"Cache miss: {creationTimes[0]:F2}ms");
            _output.WriteLine($"Cache hit: {creationTimes[1]:F4}ms");
            _output.WriteLine($"Performance improvement: {(creationTimes[0] - creationTimes[1]) / creationTimes[0] * 100:F1}%");
        }
        
        [Fact]
        public async Task PSO_UniqueKeys_GenerateDifferentPSOs()
        {
            // Arrange
            var key1 = CreateTestMaterialKey("Material1");
            var key2 = CreateTestMaterialKey("Material2");
            
            // Act
            var pso1 = await _psoManager.GetOrCreatePSOAsync(key1);
            var pso2 = await _psoManager.GetOrCreatePSOAsync(key2);
            
            // Assert
            Assert.NotNull(pso1);
            Assert.NotNull(pso2);
            Assert.NotEqual(pso1, pso2);
        }
        
        [Fact]
        public async Task PSO_CacheCapacity_EnforcesLimit()
        {
            // Arrange
            var capacity = 50;
            var manager = new PSO.OptimizedPSOManager(_device, capacity);
            
            // Act - Create more PSOs than capacity
            var tasks = new List<Task>();
            for (int i = 0; i < capacity + 20; i++)
            {
                var key = CreateTestMaterialKey($"CapacityTest{i}");
                tasks.Add(manager.GetOrCreatePSOAsync(key));
            }
            
            await Task.WhenAll(tasks);
            
            // Assert
            var stats = manager.GetDetailedStatistics();
            Assert.True(stats.CacheStatistics.TotalEntries <= capacity, $"Cache should not exceed capacity {capacity}");
            
            _output.WriteLine($"Created {capacity + 20} PSOs, cache contains {stats.CacheStatistics.TotalEntries}");
        }
        
        [Fact]
        public async Task PSO_MaterialIntegration_WorksCorrectly()
        {
            // Arrange
            var integration = new PSOMaterialIntegration(_device);
            var materialDesc = CreateTestMaterialDescription("IntegrationTest");
            
            // Act
            await integration.RegisterMaterialAsync("TestMaterial", materialDesc);
            var pso1 = await integration.GetMaterialPSOAsync("TestMaterial");
            var pso2 = await integration.GetMaterialPSOAsync("TestMaterial");
            
            // Assert
            Assert.NotNull(pso1);
            Assert.Equal(pso1, pso2);
            
            var stats = integration.GetStatistics();
            Assert.True(stats.CacheHitRate > 0, "Should have cache hits");
            
            integration.Dispose();
        }
        
        [Fact]
        public async Task PSO_Performance_Benchmark()
        {
            // Arrange
            var iterations = 100;
            var materials = new List<PSO.MaterialPSOKey>();
            
            // Create test materials
            for (int i = 0; i < 10; i++)
            {
                materials.Add(CreateTestMaterialKey($"Benchmark{i}"));
            }
            
            // Act - Measure cache performance
            var cache = new List<double>();
            var creation = new List<double>();
            
            for (int iter = 0; iter < iterations; iter++)
            {
                var key = materials[iter % materials.Count];
                
                var stopwatch = Stopwatch.StartNew();
                var pso = await _psoManager.GetOrCreatePSOAsync(key);
                stopwatch.Stop();
                
                if (stopwatch.Elapsed.TotalMilliseconds < 5) // Likely cache hit
                    cache.Add(stopwatch.Elapsed.TotalMilliseconds);
                else
                    creation.Add(stopwatch.Elapsed.TotalMilliseconds);
            }
            
            // Assert
            var stats = _psoManager.GetDetailedStatistics();
            
            _output.WriteLine($"Benchmark Results ({iterations} iterations):");
            _output.WriteLine($"Cache hits: {cache.Count}");
            _output.WriteLine($"Cache misses: {creation.Count}");
            _output.WriteLine($"Hit rate: {stats.CacheHitRate:P2}");
            
            if (cache.Any())
                _output.WriteLine($"Average cache hit time: {cache.Average():F4}ms");
            
            if (creation.Any())
                _output.WriteLine($"Average creation time: {creation.Average():F2}ms");
            
            Assert.True(stats.CacheHitRate > 0.5, "Cache hit rate should be significant");
        }
        
        [Fact]
        public async Task PSO_CacheCleanup_RemovesExpiredEntries()
        {
            // Arrange
            var shortExpirationManager = new PSO.OptimizedPSOManager(_device, 10, TimeSpan.FromMilliseconds(100));
            var key = CreateTestMaterialKey("CleanupTest");
            
            // Act
            var pso = await shortExpirationManager.GetOrCreatePSOAsync(key);
            Assert.NotNull(pso);
            
            var stats1 = shortExpirationManager.GetDetailedStatistics();
            Assert.Equal(1, stats1.CacheStatistics.TotalEntries);
            
            // Wait for expiration
            await Task.Delay(150);
            
            // Force cleanup
            shortExpirationManager.ForceCacheCleanup();
            
            var stats2 = shortExpirationManager.GetDetailedStatistics();
            
            // Assert
            _output.WriteLine($"Entries before cleanup: {stats1.CacheStatistics.TotalEntries}");
            _output.WriteLine($"Entries after cleanup: {stats2.CacheStatistics.TotalEntries}");
            
            shortExpirationManager.Dispose();
        }
        
        [Fact]
        public async Task PSO_DebugTools_GenerateUsefulReports()
        {
            // Arrange
            var debugTools = new PSO.PSODebugTools(_psoManager);
            
            // Act
            await _psoManager.GetOrCreatePSOAsync(CreateTestMaterialKey("DebugTest1"));
            await _psoManager.GetOrCreatePSOAsync(CreateTestMaterialKey("DebugTest2"));
            
            var analysisReport = debugTools.GenerateCacheAnalysisReport();
            var materialReport = debugTools.GenerateMaterialAnalysis();
            var memoryReport = debugTools.GenerateMemoryBreakdown();
            
            // Assert
            Assert.NotEmpty(analysisReport);
            Assert.NotEmpty(materialReport);
            Assert.NotEmpty(memoryReport);
            
            _output.WriteLine("Debug Reports Generated:");
            _output.WriteLine(analysisReport);
            _output.WriteLine(materialReport);
            _output.WriteLine(memoryReport);
        }
        
        [Fact]
        public async Task PSO_MaterialUpdate_InvalidatesCache()
        {
            // Arrange
            var integration = new PSOMaterialIntegration(_device);
            var originalDesc = CreateTestMaterialDescription("UpdateTest");
            var updatedDesc = CreateTestMaterialDescription("UpdateTest");
            updatedDesc.ShaderMacros.Add(new MaterialShaderMacro { Name = "UPDATED", Value = "1" });
            
            // Act
            await integration.RegisterMaterialAsync("UpdateTest", originalDesc);
            var pso1 = await integration.GetMaterialPSOAsync("UpdateTest");
            
            await integration.UpdateMaterialPSOAsync("UpdateTest", updatedDesc);
            var pso2 = await integration.GetMaterialPSOAsync("UpdateTest");
            
            // Assert
            Assert.NotEqual(pso1, pso2, "Updated material should create new PSO");
            
            _output.WriteLine("Material update successfully invalidated cache");
            integration.Dispose();
        }
        
        [Fact]
        public async Task PSO_Monitor_TracksPerformance()
        {
            // Arrange
            var monitor = new PSO.PSOCacheMonitor(_psoManager, TimeSpan.FromMilliseconds(100));
            var updates = new List<PSO.PSOMonitorEntry>();
            
            monitor.OnPerformanceUpdate += (sender, entry) => updates.Add(entry);
            
            // Act
            await _psoManager.GetOrCreatePSOAsync(CreateTestMaterialKey("MonitorTest1"));
            await _psoManager.GetOrCreatePSOAsync(CreateTestMaterialKey("MonitorTest2"));
            
            // Wait for monitor update
            await Task.Delay(200);
            
            var history = monitor.GetHistory(TimeSpan.FromSeconds(1));
            
            // Assert
            Assert.True(history.Any(), "Monitor should have recorded updates");
            var latestUpdate = history.Last();
            
            _output.WriteLine("Monitor Updates:");
            foreach (var update in history)
            {
                _output.WriteLine(update.GetFormattedString());
            }
            
            monitor.Dispose();
        }
        
        [Fact]
        public async Task PSO_StressTest_HighVolumeCreation()
        {
            // Arrange
            var stressManager = new PSO.OptimizedPSOManager(_device, 500);
            var stressKeys = new List<PSO.MaterialPSOKey>();
            
            for (int i = 0; i < 200; i++)
            {
                stressKeys.Add(CreateTestMaterialKey($"Stress{i}"));
            }
            
            // Act - Create all PSOs
            var stopwatch = Stopwatch.StartNew();
            var creationTasks = stressKeys.Select(key => stressManager.GetOrCreatePSOAsync(key));
            await Task.WhenAll(creationTasks);
            stopwatch.Stop();
            
            // Re-access random PSOs to test caching
            var accessTasks = new List<Task>();
            for (int i = 0; i < 1000; i++)
            {
                var key = stressKeys[new Random().Next(stressKeys.Count)];
                accessTasks.Add(stressManager.GetOrCreatePSOAsync(key));
            }
            await Task.WhenAll(accessTasks);
            
            stopwatch.Stop();
            
            // Assert
            var stats = stressManager.GetDetailedStatistics();
            
            _output.WriteLine($"Stress Test Results:");
            _output.WriteLine($"Total creation time: {stopwatch.Elapsed.TotalSeconds:F2}s");
            _output.WriteLine($"Cache hit rate: {stats.CacheHitRate:P2}");
            _output.WriteLine($"Final cache size: {stats.CacheStatistics.TotalEntries}");
            _output.WriteLine($"Memory usage: {stats.CacheStatistics.MemoryUsageBytes / 1024 / 1024:F1}MB");
            
            Assert.True(stats.CacheHitRate > 0.8, "Should maintain high hit rate under stress");
            
            stressManager.Dispose();
        }
        
        #region Helper Methods
        
        private PSO.MaterialPSOKey CreateTestMaterialKey(string name)
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
                    new PSO.ShaderMacro { Name = "TEST_MODE", Value = "1" },
                    new PSO.ShaderMacro { Name = "DEBUG", Value = "1" }
                },
                MaterialParameters = new Dictionary<string, object>
                {
                    ["BaseColor"] = new SharpDX.Vector4(1, 0, 0, 1),
                    ["Roughness"] = 0.8f,
                    ["Metalness"] = 0.0f
                }
            };
        }
        
        private MaterialDescription CreateTestMaterialDescription(string name)
        {
            return new MaterialDescription
            {
                VertexShaderPath = $"Shaders/{name}VS.hlsl",
                PixelShaderPath = $"Shaders/{name}PS.hlsl",
                ShaderMacros = new List<MaterialShaderMacro>
                {
                    new MaterialShaderMacro { Name = "TEST_MODE", Value = "1" }
                },
                Parameters = new Dictionary<string, object>
                {
                    ["BaseColor"] = new SharpDX.Vector4(1, 0, 0, 1),
                    ["Roughness"] = 0.8f
                }
            };
        }
        
        #endregion
    }
}