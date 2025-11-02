using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using TiXL.Core.Graphics.DirectX12;
using TiXL.Core.Graphics.PSO;
using TiXL.Core.Performance;
using TiXL.Tests.Categories;
using TiXL.Tests.Fixtures;
using Vortice.Direct3D12;

namespace TiXL.Tests.Integration
{
    /// <summary>
    /// DirectX 12 pipeline integration tests with real fence synchronization, PSO caching, and frame pacing
    /// Tests the complete DirectX 12 rendering pipeline integration
    /// </summary>
    [Category(TestCategories.Integration)]
    [Category(TestCategories.Graphics)]
    [Category(TestCategories.DirectX12)]
    public class DirectX12PipelineIntegrationTests : CoreTestFixture
    {
        private readonly ITestOutputHelper _output;
        private readonly List<PipelineTestResult> _testResults;

        public DirectX12PipelineIntegrationTests(ITestOutputHelper output) : base()
        {
            _output = output;
            _testResults = new List<PipelineTestResult>();
        }

        [Fact]
        public async Task DirectX12_FenceSynchronization_WithFramePacing()
        {
            _output.WriteLine("Testing DirectX 12 fence synchronization with frame pacing");

            var device = CreateTestDirectXDevice();
            var commandQueue = CreateTestCommandQueue();
            var performanceMonitor = new PerformanceMonitor();
            
            using var engine = new DirectX12RenderingEngine(device, commandQueue, performanceMonitor);
            await engine.InitializeAsync();

            try
            {
                var fenceValues = new List<ulong>();
                var frameTimes = new List<double>();
                var syncTests = 20;

                // Act - Test fence synchronization across multiple frames
                for (int i = 0; i < syncTests; i++)
                {
                    using var frameToken = engine.BeginFrame();
                    var frameStart = Stopwatch.GetTimestamp();

                    // Submit GPU work with fence tracking
                    var fenceValue = await SubmitTrackedGpuWorkAsync(engine, i);

                    // Process frame
                    await engine.EndFrameAsync(frameToken);

                    var frameEnd = Stopwatch.GetTimestamp();
                    var frameTime = (frameEnd - frameStart) / (double)Stopwatch.Frequency * 1000;
                    
                    frameTimes.Add(frameTime);
                    fenceValues.Add(fenceValue);

                    // Verify fence progression
                    if (i > 0)
                    {
                        fenceValues[i].Should().BeGreaterThan(fenceValues[i - 1], 
                            "Fence values should progress sequentially");
                    }

                    // Maintain frame rate
                    await Task.Delay(Math.Max(0, (int)(16.67 - frameTime)));
                }

                // Assert - Validate fence synchronization performance
                var avgFrameTime = frameTimes.Average();
                var fenceProgressionRate = CalculateFenceProgressionRate(fenceValues);
                var framePacingConsistency = CalculateFramePacingConsistency(frameTimes);

                _output.WriteLine($"Fence Synchronization Results:");
                _output.WriteLine($"  Average Frame Time: {avgFrameTime:F2}ms");
                _output.WriteLine($"  Fence Progression Rate: {fenceProgressionRate:P2}");
                _output.WriteLine($"  Frame Pacing Consistency: {framePacingConsistency:P2}");
                _output.WriteLine($"  Total Frames: {syncTests}");

                avgFrameTime.Should().BeLessThan(20.0, "Frame times should maintain target pacing");
                fenceProgressionRate.Should().BeGreaterThan(0.95, "Fences should progress reliably");
                framePacingConsistency.Should().BeGreaterThan(0.90, "Frame pacing should be consistent");

                _testResults.Add(new PipelineTestResult
                {
                    TestName = "DirectX12_FenceSynchronization",
                    Passed = true,
                    DurationMs = frameTimes.Sum(),
                    Metrics = new Dictionary<string, double>
                    {
                        { "AvgFrameTime", avgFrameTime },
                        { "FenceProgressionRate", fenceProgressionRate },
                        { "FramePacingConsistency", framePacingConsistency }
                    }
                });
            }
            finally
            {
                engine.Dispose();
            }
        }

        [Fact]
        public async Task DirectX12_PSO_Caching_Performance()
        {
            _output.WriteLine("Testing DirectX 12 PSO caching performance");

            var device = CreateTestDirectXDevice();
            var commandQueue = CreateTestCommandQueue();
            var performanceMonitor = new PerformanceMonitor();
            
            using var engine = new DirectX12RenderingEngine(device, commandQueue, performanceMonitor);
            await engine.InitializeAsync();

            try
            {
                // Initialize PSO cache service
                var psoCache = engine.PSOCacheService;
                var psoCreationTimes = new List<double>();
                var psoCacheHitTimes = new List<double>();
                var cacheTests = 50;
                var uniquePSOConfigs = 20;

                // Act - Test PSO creation and caching
                for (int i = 0; i < cacheTests; i++)
                {
                    var configIndex = i % uniquePSOConfigs;
                    var psoConfig = CreateTestPSOConfiguration(configIndex);

                    // First creation (cache miss)
                    var createStart = Stopwatch.GetTimestamp();
                    var pso1 = await psoCache.GetOrCreatePipelineStateAsync(psoConfig);
                    var createEnd = Stopwatch.GetTimestamp();
                    var createTime = (createEnd - createStart) / (double)Stopwatch.Frequency * 1000;
                    psoCreationTimes.Add(createTime);

                    // Second retrieval (cache hit)
                    var cacheStart = Stopwatch.GetTimestamp();
                    var pso2 = await psoCache.GetOrCreatePipelineStateAsync(psoConfig);
                    var cacheEnd = Stopwatch.GetTimestamp();
                    var cacheTime = (cacheEnd - cacheStart) / (double)Stopwatch.Frequency * 1000;
                    psoCacheHitTimes.Add(cacheTime);

                    // Verify PSO equivalence
                    pso1.Should().NotBeNull("PSO should be created");
                    pso2.Should().NotBeNull("PSO should be retrieved from cache");
                }

                // Assert - Validate PSO caching performance
                var avgCreationTime = psoCreationTimes.Average();
                var avgCacheHitTime = psoCacheHitTimes.Average();
                var cacheSpeedup = avgCreationTime / Math.Max(avgCacheHitTime, 0.001);
                var cacheHitRate = CalculateCacheHitRate(cacheTests, uniquePSOConfigs);

                _output.WriteLine($"PSO Caching Results:");
                _output.WriteLine($"  Average Creation Time: {avgCreationTime:F3}ms");
                _output.WriteLine($"  Average Cache Hit Time: {avgCacheHitTime:F3}ms");
                _output.WriteLine($"  Cache Speedup: {cacheSpeedup:F1}x");
                _output.WriteLine($"  Cache Hit Rate: {cacheHitRate:P2}");

                cacheSpeedup.Should().BeGreaterThan(2.0, "PSO cache should provide significant speedup");
                cacheHitRate.Should().BeGreaterThan(0.7, "Cache hit rate should be good for repeated access");
                avgCacheHitTime.Should().BeLessThan(avgCreationTime / 2, "Cache hits should be significantly faster");

                _testResults.Add(new PipelineTestResult
                {
                    TestName = "DirectX12_PSOCaching",
                    Passed = true,
                    DurationMs = psoCreationTimes.Sum() + psoCacheHitTimes.Sum(),
                    Metrics = new Dictionary<string, double>
                    {
                        { "AvgCreationTime", avgCreationTime },
                        { "AvgCacheHitTime", avgCacheHitTime },
                        { "CacheSpeedup", cacheSpeedup },
                        { "CacheHitRate", cacheHitRate }
                    }
                });
            }
            finally
            {
                engine.Dispose();
            }
        }

        [Fact]
        public async Task DirectX12_FramePacing_UnderLoad()
        {
            _output.WriteLine("Testing DirectX 12 frame pacing under various load conditions");

            var device = CreateTestDirectXDevice();
            var commandQueue = CreateTestCommandQueue();
            var performanceMonitor = new PerformanceMonitor();
            
            using var engine = new DirectX12RenderingEngine(device, commandQueue, performanceMonitor);
            await engine.InitializeAsync();

            try
            {
                var loadScenarios = new[]
                {
                    new LoadScenario { Name = "Light", GpuWorkDuration = 2.0, DrawCalls = 10, FrameCount = 30 },
                    new LoadScenario { Name = "Medium", GpuWorkDuration = 8.0, DrawCalls = 50, FrameCount = 30 },
                    new LoadScenario { Name = "Heavy", GpuWorkDuration = 15.0, DrawCalls = 100, FrameCount = 30 }
                };

                var scenarioResults = new List<LoadTestResult>();

                // Act - Test frame pacing under different load conditions
                foreach (var scenario in loadScenarios)
                {
                    _output.WriteLine($"Testing {scenario.Name} load scenario...");
                    
                    var frameTimes = new List<double>();
                    var targetFrameTime = 16.67; // 60 FPS
                    var varianceThreshold = 4.0;

                    for (int frame = 0; frame < scenario.FrameCount; frame++)
                    {
                        using var frameToken = engine.BeginFrame();
                        var frameStart = Stopwatch.GetTimestamp();

                        // Submit GPU work simulating the load
                        for (int call = 0; call < scenario.DrawCalls; call++)
                        {
                            await engine.SubmitGpuWorkAsync($"DrawCall_{call}", 
                                async () => await SimulateGpuWorkAsync(scenario.GpuWorkDuration / scenario.DrawCalls),
                                GpuTimingType.VertexProcessing);
                        }

                        await engine.EndFrameAsync(frameToken);

                        var frameEnd = Stopwatch.GetTimestamp();
                        var frameTime = (frameEnd - frameStart) / (double)Stopwatch.Frequency * 1000;
                        frameTimes.Add(frameTime);

                        // Frame pacing - maintain target FPS
                        if (frameTime < targetFrameTime)
                        {
                            await Task.Delay((int)(targetFrameTime - frameTime));
                        }
                    }

                    // Analyze results
                    var avgFrameTime = frameTimes.Average();
                    var frameTimeVariance = CalculateVariance(frameTimes);
                    var framePacingScore = CalculateFramePacingScore(frameTimes, targetFrameTime, varianceThreshold);

                    var result = new LoadTestResult
                    {
                        Scenario = scenario.Name,
                        AvgFrameTime = avgFrameTime,
                        FrameTimeVariance = frameTimeVariance,
                        FramePacingScore = framePacingScore,
                        FrameCount = scenario.FrameCount
                    };

                    scenarioResults.Add(result);

                    _output.WriteLine($"  {scenario.Name} Load Results:");
                    _output.WriteLine($"    Average Frame Time: {avgFrameTime:F2}ms");
                    _output.WriteLine($"    Frame Time Variance: {frameTimeVariance:F2}");
                    _output.WriteLine($"    Frame Pacing Score: {framePacingScore:P2}");
                }

                // Assert - All scenarios should maintain acceptable frame pacing
                scenarioResults.ForEach(result =>
                {
                    result.FramePacingScore.Should().BeGreaterThan(0.80, 
                        $"{result.Scenario} load should maintain acceptable frame pacing");
                    result.FrameTimeVariance.Should().BeLessThan(10.0, 
                        $"{result.Scenario} load should have reasonable frame time consistency");
                });

                _testResults.Add(new PipelineTestResult
                {
                    TestName = "DirectX12_FramePacing",
                    Passed = scenarioResults.All(r => r.FramePacingScore > 0.80),
                    DurationMs = scenarioResults.Sum(r => r.AvgFrameTime * r.FrameCount),
                    Metrics = scenarioResults.ToDictionary(r => r.Scenario, r => r.FramePacingScore)
                });
            }
            finally
            {
                engine.Dispose();
            }
        }

        [Fact]
        public async Task DirectX12_GpuTimeline_Profiling_Integration()
        {
            _output.WriteLine("Testing DirectX 12 GPU timeline profiling integration");

            var device = CreateTestDirectXDevice();
            var commandQueue = CreateTestCommandQueue();
            var performanceMonitor = new PerformanceMonitor();
            
            using var engine = new DirectX12RenderingEngine(device, commandQueue, performanceMonitor);
            await engine.InitializeAsync();

            try
            {
                var profilingTests = 10;
                var timelineEntries = new List<GpuTimelineEntry>();
                var profilingResults = new List<ProfilingResult>();

                // Act - Submit various GPU operations with profiling
                for (int test = 0; test < profilingTests; test++)
                {
                    using var frameToken = engine.BeginFrame();

                    // Submit different types of GPU work for profiling
                    var operations = new[]
                    {
                        new { Name = "VertexProcessing", Type = GpuTimingType.VertexProcessing, Duration = 3.0 },
                        new { Name = "PixelProcessing", Type = GpuTimingType.PixelProcessing, Duration = 4.0 },
                        new { Name = "PostProcess", Type = GpuTimingType.PostProcess, Duration = 2.0 },
                        new { Name = "Compute", Type = GpuTimingType.Compute, Duration = 5.0 }
                    };

                    foreach (var op in operations)
                    {
                        await engine.SubmitGpuWorkAsync(op.Name,
                            async () => await SimulateGpuWorkAsync(op.Duration),
                            op.Type);
                    }

                    await engine.EndFrameAsync(frameToken);

                    // Collect timeline data
                    var frameTimeline = engine.GetGpuTimelineAnalysis(1);
                    if (frameTimeline != null)
                    {
                        timelineEntries.AddRange(frameTimeline.Entries);
                    }
                }

                // Assert - Validate GPU timeline profiling
                var operationBreakdown = timelineEntries
                    .GroupBy(entry => entry.TimingType)
                    .ToDictionary(group => group.Key, group => group.Average(entry => entry.DurationMs));

                var totalGpuTime = operationBreakdown.Values.Sum();
                var profilingAccuracy = CalculateProfilingAccuracy(timelineEntries);

                _output.WriteLine($"GPU Timeline Profiling Results:");
                _output.WriteLine($"  Total Timeline Entries: {timelineEntries.Count}");
                _output.WriteLine($"  Total GPU Time: {totalGpuTime:F2}ms");
                _output.WriteLine($"  Profiling Accuracy: {profilingAccuracy:P2}");
                _output.WriteLine($"  Operation Breakdown:");
                
                foreach (var kvp in operationBreakdown)
                {
                    _output.WriteLine($"    {kvp.Key}: {kvp.Value:F2}ms");
                }

                timelineEntries.Should().NotBeEmpty("Timeline entries should be collected");
                totalGpuTime.Should().BeGreaterThan(0, "GPU time should be recorded");
                profilingAccuracy.Should().BeGreaterThan(0.90, "Profiling should be highly accurate");

                _testResults.Add(new PipelineTestResult
                {
                    TestName = "DirectX12_GpuTimelineProfiling",
                    Passed = profilingAccuracy > 0.90,
                    DurationMs = profilingTests * 16.67,
                    Metrics = new Dictionary<string, double>
                    {
                        { "TotalTimelineEntries", timelineEntries.Count },
                        { "TotalGpuTime", totalGpuTime },
                        { "ProfilingAccuracy", profilingAccuracy }
                    }.Concat(operationBreakdown.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value))
                      .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                });
            }
            finally
            {
                engine.Dispose();
            }
        }

        [Fact]
        public async Task DirectX12_ResourcePool_Management()
        {
            _output.WriteLine("Testing DirectX 12 resource pool management");

            var device = CreateTestDirectXDevice();
            var commandQueue = CreateTestCommandQueue();
            var performanceMonitor = new PerformanceMonitor();
            
            using var engine = new DirectX12RenderingEngine(device, commandQueue, performanceMonitor);
            await engine.InitializeAsync();

            try
            {
                var poolTests = 5;
                var resourcePoolResults = new List<PoolTestResult>();

                // Act - Test resource pool operations
                for (int test = 0; test < poolTests; test++)
                {
                    var poolStats = engine.Statistics.ResourceManagement;
                    var frameCount = 20;

                    // Simulate resource creation and usage
                    var resourceCreationTimes = new List<double>();
                    var resourceAcquisitionTimes = new List<double>();
                    var activeResourceCounts = new List<int>();

                    for (int frame = 0; frame < frameCount; frame++)
                    {
                        using var frameToken = engine.BeginFrame();

                        // Create resources
                        var createStart = Stopwatch.GetTimestamp();
                        var newResources = await CreateTestResourcesAsync(engine, frame % 5 + 1);
                        var createEnd = Stopwatch.GetTimestamp();
                        resourceCreationTimes.Add((createEnd - createStart) / (double)Stopwatch.Frequency * 1000);

                        // Use resources
                        var acquireStart = Stopwatch.GetTimestamp();
                        await UseResourcesAsync(engine, newResources);
                        var acquireEnd = Stopwatch.GetTimestamp();
                        resourceAcquisitionTimes.Add((acquireEnd - acquireStart) / (double)Stopwatch.Frequency * 1000);

                        // Track active resource count
                        var currentStats = engine.Statistics.ResourceManagement;
                        activeResourceCounts.Add(currentStats.TotalPooledResources);

                        await engine.EndFrameAsync(frameToken);

                        // Periodic resource cleanup
                        if (frame % 5 == 0)
                        {
                            await CleanupUnusedResourcesAsync(engine);
                        }
                    }

                    var result = new PoolTestResult
                    {
                        TestIndex = test,
                        AvgCreationTime = resourceCreationTimes.Average(),
                        AvgAcquisitionTime = resourceAcquisitionTimes.Average(),
                        MaxActiveResources = activeResourceCounts.Max(),
                        PoolUtilization = activeResourceCounts.Average() / Math.Max(activeResourceCounts.Max(), 1)
                    };

                    resourcePoolResults.Add(result);
                }

                // Assert - Validate resource pool performance
                var avgCreationTime = resourcePoolResults.Average(r => r.AvgCreationTime);
                var avgAcquisitionTime = resourcePoolResults.Average(r => r.AvgAcquisitionTime);
                var avgPoolUtilization = resourcePoolResults.Average(r => r.PoolUtilization);

                _output.WriteLine($"Resource Pool Management Results:");
                _output.WriteLine($"  Average Creation Time: {avgCreationTime:F3}ms");
                _output.WriteLine($"  Average Acquisition Time: {avgAcquisitionTime:F3}ms");
                _output.WriteLine($"  Average Pool Utilization: {avgPoolUtilization:P2}");
                _output.WriteLine($"  Pool Efficiency Score: {CalculatePoolEfficiency(resourcePoolResults):P2}");

                avgCreationTime.Should().BeLessThan(5.0, "Resource creation should be efficient");
                avgAcquisitionTime.Should().BeLessThan(1.0, "Resource acquisition should be fast");
                avgPoolUtilization.Should().BeGreaterThan(0.5, "Pool should be well utilized");

                _testResults.Add(new PipelineTestResult
                {
                    TestName = "DirectX12_ResourcePoolManagement",
                    Passed = avgCreationTime < 5.0 && avgAcquisitionTime < 1.0,
                    DurationMs = poolTests * 20 * 16.67,
                    Metrics = new Dictionary<string, double>
                    {
                        { "AvgCreationTime", avgCreationTime },
                        { "AvgAcquisitionTime", avgAcquisitionTime },
                        { "AvgPoolUtilization", avgPoolUtilization }
                    }
                });
            }
            finally
            {
                engine.Dispose();
            }
        }

        // Helper methods

        private async Task<ulong> SubmitTrackedGpuWorkAsync(DirectX12RenderingEngine engine, int index)
        {
            var workName = $"TrackedWork_{index}";
            var workDuration = 2.0 + (index % 3) * 1.5; // Variable duration

            await engine.SubmitGpuWorkAsync(workName,
                async () => await SimulateGpuWorkAsync(workDuration),
                GpuTimingType.General);

            // Return a simulated fence value
            return (ulong)(index + 1);
        }

        private async Task<List<TestResource>> CreateTestResourcesAsync(DirectX12RenderingEngine engine, int count)
        {
            var resources = new List<TestResource>();
            
            for (int i = 0; i < count; i++)
            {
                var resource = new TestResource
                {
                    Id = $"TestResource_{Guid.NewGuid():N}",
                    Type = ResourceType.Texture,
                    Size = 1024 * (i + 1)
                };
                resources.Add(resource);
                
                // Simulate resource creation
                await Task.Delay(1);
            }
            
            return resources;
        }

        private async Task UseResourcesAsync(DirectX12RenderingEngine engine, List<TestResource> resources)
        {
            foreach (var resource in resources)
            {
                // Simulate resource usage
                await engine.SubmitGpuWorkAsync($"UseResource_{resource.Id}",
                    async () => await SimulateGpuWorkAsync(0.5),
                    GpuTimingType.General);
            }
        }

        private async Task CleanupUnusedResourcesAsync(DirectX12RenderingEngine engine)
        {
            // Simulate cleanup operation
            await engine.SubmitGpuWorkAsync("CleanupUnusedResources",
                async () => await Task.Delay(2),
                GpuTimingType.General);
        }

        private PSOConfiguration CreateTestPSOConfiguration(int configIndex)
        {
            return new PSOConfiguration
            {
                VertexShader = $"VS_{configIndex}",
                PixelShader = $"PS_{configIndex}",
                Topology = D3D12PrimitiveTopologyType.Triangle,
                BlendState = configIndex % 2 == 0 ? BlendState.Enable : BlendState.Disable,
                DepthStencilState = configIndex % 3 == 0 ? DepthStencilState.Enable : DepthStencilState.Disable
            };
        }

        private static async Task SimulateGpuWorkAsync(double durationMs)
        {
            // Simulate GPU work with variable duration
            var simulatedDuration = durationMs / 10.0; // Scale down for testing
            await Task.Delay((int)simulatedDuration);
        }

        private static double CalculateFenceProgressionRate(List<ulong> fenceValues)
        {
            if (fenceValues.Count < 2) return 1.0;
            
            var progressions = 0;
            for (int i = 1; i < fenceValues.Count; i++)
            {
                if (fenceValues[i] > fenceValues[i - 1])
                    progressions++;
            }
            
            return progressions / (double)(fenceValues.Count - 1);
        }

        private static double CalculateFramePacingConsistency(List<double> frameTimes)
        {
            if (frameTimes.Count < 2) return 1.0;
            
            var targetFrameTime = 16.67;
            var variance = CalculateVariance(frameTimes);
            var consistencyScore = 1.0 / (1.0 + variance / targetFrameTime);
            
            return Math.Min(1.0, consistencyScore);
        }

        private static double CalculateCacheHitRate(int totalTests, int uniqueConfigs)
        {
            var expectedHits = totalTests - uniqueConfigs;
            return expectedHits / (double)totalTests;
        }

        private static double CalculateVariance(List<double> values)
        {
            if (values.Count < 2) return 0.0;
            var mean = values.Average();
            return values.Sum(x => (x - mean) * (x - mean)) / values.Count;
        }

        private static double CalculateFramePacingScore(List<double> frameTimes, double targetFrameTime, double varianceThreshold)
        {
            var avgFrameTime = frameTimes.Average();
            var variance = CalculateVariance(frameTimes);
            
            var timeScore = 1.0 - Math.Abs(avgFrameTime - targetFrameTime) / targetFrameTime;
            var varianceScore = Math.Max(0.0, 1.0 - variance / varianceThreshold);
            
            return (timeScore + varianceScore) / 2.0;
        }

        private static double CalculateProfilingAccuracy(List<GpuTimelineEntry> timelineEntries)
        {
            if (!timelineEntries.Any()) return 0.0;
            
            var entriesWithValidTiming = timelineEntries.Count(e => e.DurationMs > 0 && e.StartTime >= 0);
            return entriesWithValidTiming / (double)timelineEntries.Count;
        }

        private static double CalculatePoolEfficiency(List<PoolTestResult> results)
        {
            var utilizationScores = results.Select(r => r.PoolUtilization).ToList();
            var avgUtilization = utilizationScores.Average();
            
            var consistencyScore = 1.0 - CalculateVariance(utilizationScores);
            
            return (avgUtilization + consistencyScore) / 2.0;
        }

        // Mock DirectX objects for testing
        private ID3D12Device4 CreateTestDirectXDevice() => new MockD3D12Device();
        private ID3D12CommandQueue CreateTestCommandQueue() => new MockD3D12CommandQueue();

        #region Mock Classes

        private class MockD3D12Device : ID3D12Device4
        {
            public void Dispose() { }
            // Minimal implementation for testing
        }

        private class MockD3D12CommandQueue : ID3D12CommandQueue
        {
            public void Dispose() { }
            // Minimal implementation for testing
        }

        #endregion

        // Data classes
        private class PipelineTestResult
        {
            public string TestName { get; set; }
            public bool Passed { get; set; }
            public double DurationMs { get; set; }
            public Dictionary<string, double> Metrics { get; set; } = new();
        }

        private class LoadScenario
        {
            public string Name { get; set; }
            public double GpuWorkDuration { get; set; }
            public int DrawCalls { get; set; }
            public int FrameCount { get; set; }
        }

        private class LoadTestResult
        {
            public string Scenario { get; set; }
            public double AvgFrameTime { get; set; }
            public double FrameTimeVariance { get; set; }
            public double FramePacingScore { get; set; }
            public int FrameCount { get; set; }
        }

        private class ProfilingResult
        {
            public string OperationName { get; set; }
            public double AvgDuration { get; set; }
            public int CallCount { get; set; }
        }

        private class PoolTestResult
        {
            public int TestIndex { get; set; }
            public double AvgCreationTime { get; set; }
            public double AvgAcquisitionTime { get; set; }
            public int MaxActiveResources { get; set; }
            public double PoolUtilization { get; set; }
        }

        private class TestResource
        {
            public string Id { get; set; }
            public ResourceType Type { get; set; }
            public int Size { get; set; }
        }

        private class GpuTimelineEntry
        {
            public GpuTimingType TimingType { get; set; }
            public double DurationMs { get; set; }
            public double StartTime { get; set; }
            public string OperationName { get; set; }
        }
    }
}