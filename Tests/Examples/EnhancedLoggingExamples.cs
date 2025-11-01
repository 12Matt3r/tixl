// Example tests demonstrating enhanced logging integration for TiXL
// This file shows practical usage of the logging framework in real test scenarios

using Xunit;
using FluentAssertions;
using TiXL.Tests.Categories;
using TiXL.Tests.Fixtures;
using TiXL.Tests.Mocks.Graphics;
using SharpDX.Direct3D12;
using SharpDX;
using System.Diagnostics;
using System.Text.Json;

namespace TiXL.Tests.Graphics
{
    /// <summary>
    /// Enhanced graphics tests with comprehensive logging
    /// </summary>
    [Collection("Graphics Tests")]
    [Category(TestCategories.Graphics)]
    [Category(TestCategories.Rendering)]
    [Category(TestCategories.P0)]
    public class EnhancedGraphicsTests : CoreTestFixture
    {
        [Fact]
        [Category(TestCategories.Fast)]
        public void EnhancedMockDevice_CreateDevice_WithDetailedLogging()
        {
            // Log test parameters
            var testConfig = new
            {
                DeviceType = "MockD3D12Device",
                ExpectedFeatureLevel = "12.0",
                ExpectedMemoryBytes = 1073741824,
                TestCategory = TestCategories.Graphics
            };
            
            ExecutionLogger.LogTestData("TestConfiguration", testConfig);
            ExecutionLogger.LogInformation("Starting enhanced graphics device creation test");
            
            var creationStart = Stopwatch.GetTimestamp();
            var memoryBefore = GC.GetTotalMemory(false);
            
            try
            {
                using var device = new MockD3D12Device();
                
                var creationDuration = (Stopwatch.GetTimestamp() - creationStart) * 1000.0 / Stopwatch.Frequency;
                ExecutionLogger.LogPhase("DeviceCreation", creationDuration);
                ExecutionLogger.LogPerformanceMetric("DeviceCreationTime", creationDuration, "ms");
                
                var memoryAfter = GC.GetTotalMemory(false);
                var memoryDelta = memoryAfter - memoryBefore;
                ExecutionLogger.LogResourceUsage("GC_Memory", memoryDelta, "DeviceCreation");
                
                ExecutionLogger.LogInformation("Device created successfully in {Duration:F2}ms", creationDuration);
                ExecutionLogger.LogTestData("DeviceInfo", new
                {
                    FeatureLevel = device.DeviceInfo.FeatureLevel.ToString(),
                    DedicatedVideoMemory = device.DeviceInfo.DedicatedVideoMemory,
                    CreationDurationMs = creationDuration,
                    MemoryDeltaBytes = memoryDelta
                });
                
                // Verify results with detailed logging
                try
                {
                    device.DeviceInfo.Should().NotBeNull("Device info should be available");
                    device.DeviceInfo.FeatureLevel.Should().Be(FeatureLevel.Level_12_0, 
                        "Should support Direct3D 12.0 feature level");
                    device.DeviceInfo.DedicatedVideoMemory.Should().BeGreaterThan(0, 
                        "Should have dedicated video memory");
                }
                catch (Exception assertionEx)
                {
                    ExecutionLogger.LogError(assertionEx, "Assertion failed during device verification");
                    throw;
                }
                
                ExecutionLogger.LogPhase("Verification", 0);
                ExecutionLogger.LogInformation("Device creation test completed successfully");
            }
            catch (Exception ex)
            {
                var failureContext = new Dictionary<string, object>
                {
                    ["TestPhase"] = "DeviceCreation",
                    ["GraphicsAPI"] = "D3D12",
                    ["TestCategory"] = TestCategories.Graphics,
                    ["Configuration"] = testConfig
                };
                
                FailureAnalyzer.AnalyzeFailure(ex, GetType().Name, 
                    nameof(EnhancedMockDevice_CreateDevice_WithDetailedLogging), failureContext);
                
                ExecutionLogger.LogError(ex, "Device creation test failed");
                throw;
            }
        }
        
        [Fact]
        [Category(TestCategories.Medium)]
        public void EnhancedRenderTarget_CreateTexture2D_ComprehensiveAnalysis()
        {
            ExecutionLogger.LogInformation("Starting enhanced texture creation test");
            
            var textureConfig = new
            {
                Width = 1920,
                Height = 1080,
                Format = "R8G8B8A8_UNorm",
                MipLevels = 1,
                ArraySize = 1
            };
            
            ExecutionLogger.LogTestData("TextureConfiguration", textureConfig);
            
            var memoryBefore = GC.GetTotalMemory(false);
            var resourcesCreated = 0;
            var totalCreationTime = 0.0;
            
            try
            {
                using var device = new MockD3D12Device();
                
                for (int i = 0; i < 5; i++) // Create multiple textures for comprehensive testing
                {
                    var textureStart = Stopwatch.GetTimestamp();
                    
                    var description = new ResourceDescription
                    {
                        Dimension = ResourceDimension.Texture2D,
                        Width = textureConfig.Width,
                        Height = textureConfig.Height,
                        DepthOrArraySize = 1,
                        MipLevels = 1,
                        Format = Format.R8G8B8A8_UNorm,
                        SampleDescription = new SampleDescription(1, 0)
                    };
                    
                    using var renderTarget = device.CreateCommittedResource(
                        HeapType.Default,
                        ResourceStates.RenderTarget,
                        description);
                    
                    var textureDuration = (Stopwatch.GetTimestamp() - textureStart) * 1000.0 / Stopwatch.Frequency;
                    totalCreationTime += textureDuration;
                    resourcesCreated++;
                    
                    ExecutionLogger.LogPerformanceMetric($"TextureCreation_{i}", textureDuration, "ms");
                    ExecutionLogger.LogTestData($"TextureInfo_{i}", new
                    {
                        Width = renderTarget.Description.Width,
                        Height = renderTarget.Description.Height,
                        Format = renderTarget.Description.Format.ToString(),
                        Dimension = renderTarget.Description.Dimension.ToString(),
                        CreationTimeMs = textureDuration
                    });
                    
                    // Verify each texture
                    renderTarget.Description.Dimension.Should().Be(ResourceDimension.Texture2D);
                    renderTarget.Description.Width.Should().Be(1920);
                    renderTarget.Description.Height.Should().Be(1080);
                    renderTarget.Description.Format.Should().Be(Format.R8G8B8A8_UNorm);
                }
                
                var memoryAfter = GC.GetTotalMemory(false);
                var memoryDelta = memoryAfter - memoryBefore;
                
                ExecutionLogger.LogPhase("ResourceCreation", totalCreationTime);
                ExecutionLogger.LogPerformanceMetric("TotalTextureCreationTime", totalCreationTime, "ms");
                ExecutionLogger.LogPerformanceMetric("AverageTextureCreationTime", totalCreationTime / resourcesCreated, "ms");
                ExecutionLogger.LogResourceUsage("TotalMemory", memoryDelta, "TextureCreation");
                ExecutionLogger.LogTestData("TestSummary", new
                {
                    ResourcesCreated = resourcesCreated,
                    TotalCreationTimeMs = totalCreationTime,
                    MemoryDeltaBytes = memoryDelta,
                    AverageTimePerResourceMs = totalCreationTime / resourcesCreated
                });
                
                resourcesCreated.Should().Be(5);
                totalCreationTime.Should().BeLessThan(1000, "Texture creation should complete within reasonable time");
                
                ExecutionLogger.LogInformation("Texture creation test completed successfully");
            }
            catch (Exception ex)
            {
                var failureContext = new Dictionary<string, object>
                {
                    ["TestPhase"] = "TextureCreation",
                    ["ResourcesCreated"] = resourcesCreated,
                    ["TotalCreationTime"] = totalCreationTime,
                    ["TextureConfiguration"] = textureConfig,
                    ["MemoryDelta"] = memoryDelta
                };
                
                FailureAnalyzer.AnalyzeFailure(ex, GetType().Name, 
                    nameof(EnhancedRenderTarget_CreateTexture2D_ComprehensiveAnalysis), failureContext);
                
                throw;
            }
        }
        
        [Fact]
        [Category(TestCategories.Slow)]
        public async Task EnhancedMemoryUsage_StressTest_WithDebugging()
        {
            const int resourceCount = 100;
            ExecutionLogger.LogInformation("Starting stress test with {ResourceCount} resources", resourceCount);
            
            var stressTestConfig = new
            {
                ResourceCount = resourceCount,
                StartTime = DateTime.UtcNow,
                ExpectedDurationMs = 5000
            };
            
            ExecutionLogger.LogTestData("StressTestConfiguration", stressTestConfig);
            
            // Capture system snapshot before test
            var snapshotId = await DebuggingUtils.CaptureSystemSnapshot(ExecutionLogger.GetType().Name);
            ExecutionLogger.LogInformation("Captured system snapshot: {SnapshotId}", snapshotId);
            
            var resources = new List<MockD3D12Resource>();
            var creationTimings = new List<double>();
            var memorySnapshots = new List<MemorySnapshot>();
            
            try
            {
                var memoryBefore = GC.GetTotalMemory(false);
                ExecutionLogger.LogMemorySnapshot("BeforeTest");
                
                using var device = new MockD3D12Device()
                {
                    // Simulate actual device creation with logging
                    Logger = Logger // Pass the logger to the mock device
                };
                
                // Create resources with detailed timing and memory tracking
                for (int i = 0; i < resourceCount; i++)
                {
                    var resourceStart = Stopwatch.GetTimestamp();
                    
                    var description = new ResourceDescription
                    {
                        Dimension = i % 2 == 0 ? ResourceDimension.Texture2D : ResourceDimension.Buffer,
                        Width = 1024 * (i + 1),
                        Height = 1,
                        DepthOrArraySize = 1,
                        MipLevels = 1,
                        Format = Format.R8G8B8A8_UNorm
                    };
                    
                    var resource = device.CreateCommittedResource(
                        HeapType.Default,
                        ResourceStates.GenericRead,
                        description);
                    
                    resources.Add(resource);
                    
                    var resourceDuration = (Stopwatch.GetTimestamp() - resourceStart) * 1000.0 / Stopwatch.Frequency;
                    creationTimings.Add(resourceDuration);
                    
                    // Log memory usage every 10 resources
                    if (i % 10 == 0)
                    {
                        var currentMemory = GC.GetTotalMemory(false);
                        var memorySnapshot = new MemorySnapshot
                        {
                            ResourceIndex = i,
                            MemoryBytes = currentMemory,
                            DeltaFromStart = currentMemory - memoryBefore,
                            Timestamp = DateTime.UtcNow
                        };
                        memorySnapshots.Add(memorySnapshot);
                        
                        ExecutionLogger.LogPerformanceMetric($"ResourceCreation_{i}", resourceDuration, "ms");
                        ExecutionLogger.LogResourceUsage("GC_Memory", currentMemory, $"After_{i}_Resources");
                        ExecutionLogger.LogInformation("Created {ResourceIndex}/{TotalResources} resources", i + 1, resourceCount);
                    }
                }
                
                ExecutionLogger.LogPhase("ResourceCreation", creationTimings.Sum());
                ExecutionLogger.LogPerformanceMetric("TotalCreationTime", creationTimings.Sum(), "ms");
                ExecutionLogger.LogPerformanceMetric("AverageCreationTime", creationTimings.Average(), "ms");
                ExecutionLogger.LogPerformanceMetric("MaxCreationTime", creationTimings.Max(), "ms");
                ExecutionLogger.LogPerformanceMetric("MinCreationTime", creationTimings.Min(), "ms");
                
                // Verify all resources
                ExecutionLogger.LogInformation("Verifying all created resources");
                var verificationStart = Stopwatch.GetTimestamp();
                
                var verificationErrors = new List<string>();
                for (int i = 0; i < resources.Count; i++)
                {
                    var resource = resources[i];
                    var expectedWidth = 1024 * (i + 1);
                    var resourceType = i % 2 == 0 ? "Texture2D" : "Buffer";
                    
                    try
                    {
                        resource.Description.Width.Should().Be(expectedWidth, 
                            $"Resource {i} ({resourceType}) should have width {expectedWidth}");
                    }
                    catch (Exception ex)
                    {
                        var error = $"Resource {i}: Expected width {expectedWidth}, got {resource.Description.Width}";
                        verificationErrors.Add(error);
                        ExecutionLogger.LogError("ResourceVerificationError {{ResourceIndex}} {{ResourceType}} {{ExpectedWidth}} {{ActualWidth}} {{Error}}",
                            i, resourceType, expectedWidth, resource.Description.Width, ex.Message);
                    }
                }
                
                var verificationTime = (Stopwatch.GetTimestamp() - verificationStart) * 1000.0 / Stopwatch.Frequency;
                ExecutionLogger.LogPhase("ResourceVerification", verificationTime);
                
                if (verificationErrors.Any())
                {
                    ExecutionLogger.LogError("ResourceVerificationFailed {{ErrorCount}} {{Errors}}",
                        verificationErrors.Count, JsonSerializer.Serialize(verificationErrors));
                }
                
            } // Dispose device and all resources
            finally
            {
                // Ensure proper disposal
                foreach (var resource in resources)
                {
                    try
                    {
                        resource.Dispose();
                    }
                    catch (Exception ex)
                    {
                        ExecutionLogger.LogError("ResourceDisposalError {{ResourceHandle}} {{Error}}", 
                            resource.Handle, ex.Message);
                    }
                }
            }
            
            var memoryAfterCleanup = GC.GetTotalMemory(true);
            ExecutionLogger.LogMemorySnapshot("AfterCleanup");
            ExecutionLogger.LogResourceUsage("FinalMemory", memoryAfterCleanup, "AfterTest");
            
            // Analyze creation time distribution
            var creationStats = new CreationStatistics
            {
                TotalResources = resources.Count,
                AverageCreationTime = creationTimings.Average(),
                MedianCreationTime = CalculateMedian(creationTimings),
                P95CreationTime = CalculatePercentile(creationTimings, 95),
                P99CreationTime = CalculatePercentile(creationTimings, 99),
                StandardDeviation = CalculateStandardDeviation(creationTimings)
            };
            
            ExecutionLogger.LogTestData("CreationStatistics", creationStats);
            
            // Verify test results
            resources.Should().HaveCount(resourceCount);
            creationStats.AverageCreationTime.Should().BeLessThan(10, "Average creation time should be reasonable");
            creationStats.P95CreationTime.Should().BeLessThan(50, "95th percentile should be under 50ms");
            
            ExecutionLogger.LogInformation("Stress test completed successfully");
            
            // Helper methods for statistical calculations
            static double CalculateMedian(List<double> values)
            {
                var sorted = values.OrderBy(x => x).ToList();
                var mid = sorted.Count / 2;
                return sorted.Count % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2.0 : sorted[mid];
            }
            
            static double CalculatePercentile(List<double> values, double percentile)
            {
                var sorted = values.OrderBy(x => x).ToList();
                var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
                return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
            }
            
            static double CalculateStandardDeviation(List<double> values)
            {
                var average = values.Average();
                var squaredDifferences = values.Select(x => Math.Pow(x - average, 2));
                return Math.Sqrt(squaredDifferences.Average());
            }
        }
        
        [Fact]
        [Category(TestCategories.Performance)]
        public void PerformanceBenchmark_WithTrendAnalysis()
        {
            ExecutionLogger.LogInformation("Starting performance benchmark with trend analysis");
            
            const int iterations = 1000;
            const double targetTimeMs = 0.1; // Target: 0.1ms per operation
            
            var benchmarkResults = new List<BenchmarkResult>();
            
            // Run benchmark multiple times for trend analysis
            for (int run = 0; run < 5; run++)
            {
                ExecutionLogger.LogInformation("Running benchmark iteration {Run}/5", run + 1);
                
                var runStart = Stopwatch.GetTimestamp();
                var operationTimes = new List<double>();
                
                for (int i = 0; i < iterations; i++)
                {
                    var operationStart = Stopwatch.GetTimestamp();
                    
                    // Simulate a complex graphics operation
                    var result = SimulateComplexGraphicsOperation();
                    
                    var operationTime = (Stopwatch.GetTimestamp() - operationStart) * 1000.0 / Stopwatch.Frequency;
                    operationTimes.Add(operationTime);
                    
                    // Log performance metrics periodically
                    if (i % 100 == 0)
                    {
                        ExecutionLogger.LogPerformanceMetric($"Operation_{run}_{i}", operationTime, "ms");
                    }
                }
                
                var runTime = (Stopwatch.GetTimestamp() - runStart) * 1000.0 / Stopwatch.Frequency;
                
                var result = new BenchmarkResult
                {
                    Run = run,
                    TotalTimeMs = runTime,
                    AverageTimeMs = operationTimes.Average(),
                    MedianTimeMs = operationTimes.OrderBy(x => x).Skip(operationTimes.Count / 2).First(),
                    P95TimeMs = operationTimes.OrderBy(x => x).Skip((int)(operationTimes.Count * 0.95)).First(),
                    MinTimeMs = operationTimes.Min(),
                    MaxTimeMs = operationTimes.Max(),
                    StdDev = operationTimes.Select(x => Math.Pow(x - operationTimes.Average(), 2)).Average()
                };
                
                benchmarkResults.Add(result);
                
                ExecutionLogger.LogTestData($"BenchmarkRun_{run}", result);
                ExecutionLogger.LogPerformanceMetric($"BenchmarkTotalTime_{run}", runTime, "ms");
                
                // Check if this run meets performance criteria
                result.MeetsTarget = result.AverageTimeMs <= targetTimeMs;
                result.TargetAchievement = (targetTimeMs / result.AverageTimeMs) * 100;
                
                if (result.MeetsTarget)
                {
                    ExecutionLogger.LogInformation("Run {Run} meets performance target ({Target:F2}ms achieved {Average:F2}ms)", 
                        run + 1, targetTimeMs, result.AverageTimeMs);
                }
                else
                {
                    ExecutionLogger.LogWarning("Run {Run} failed performance target (target: {Target:F2}ms, actual: {Average:F2}ms)", 
                        run + 1, targetTimeMs, result.AverageTimeMs);
                }
            }
            
            // Analyze trend across runs
            var trendAnalysis = new TrendAnalysis
            {
                Runs = benchmarkResults,
                AveragePerformance = benchmarkResults.Average(r => r.AverageTimeMs),
                PerformanceVariance = benchmarkResults.Select(r => r.AverageTimeMs).Select(x => Math.Pow(x - benchmarkResults.Average(r => r.AverageTimeMs), 2)).Average(),
                IsPerformanceStable = benchmarkResults.Select(r => r.AverageTimeMs).Max() / benchmarkResults.Select(r => r.AverageTimeMs).Min() < 1.5, // 50% variance threshold
                TargetAchievement = (targetTimeMs / benchmarkResults.Average(r => r.AverageTimeMs)) * 100
            };
            
            ExecutionLogger.LogTestData("TrendAnalysis", trendAnalysis);
            ExecutionLogger.LogInformation("Benchmark trend analysis: Average {Average:F2}ms, Target achievement {Achievement:F1}%", 
                trendAnalysis.AveragePerformance, trendAnalysis.TargetAchievement);
            
            // Verify results
            benchmarkResults.Should().HaveCount(5);
            trendAnalysis.AveragePerformance.Should().BeLessThan(targetTimeMs * 2, "Average performance should be within reasonable bounds");
            trendAnalysis.IsPerformanceStable.Should().BeTrue("Performance should be stable across runs");
        }
    }
    
    /// <summary>
    /// Example showing failure analysis with debugging utilities
    /// </summary>
    [Collection("Graphics Tests")]
    [Category(TestCategories.Graphics)]
    [Category(TestCategories.P1)]
    public class FailureAnalysisExampleTests : CoreTestFixture
    {
        [Fact]
        public void IntentionalFailure_DemonstratesFailureAnalysis()
        {
            // This test intentionally fails to demonstrate failure analysis capabilities
            var testData = new { ExpectedValue = 42, ActualValue = 24 };
            ExecutionLogger.LogTestData("TestData", testData);
            
            try
            {
                throw new InvalidOperationException("Intentional failure for demonstration");
            }
            catch (Exception ex)
            {
                var failureContext = new Dictionary<string, object>
                {
                    ["TestPhase"] = "IntentionalFailure",
                    ["ExpectedValue"] = testData.ExpectedValue,
                    ["ActualValue"] = testData.ActualValue,
                    ["TestCategory"] = TestCategories.Graphics
                };
                
                // This would normally be called automatically in a production scenario
                // FailureAnalyzer.AnalyzeFailure(ex, GetType().Name, nameof(IntentionalFailure_DemonstratesFailureAnalysis), failureContext);
                
                ExecutionLogger.LogError(ex, "This is an intentional failure for demonstration");
                throw;
            }
        }
        
        [Fact]
        public async Task FailureWithGraphicsDebugging_ComprehensiveAnalysis()
        {
            try
            {
                // Capture graphics diagnostics before failure
                await DebuggingUtils.LogGraphicsDiagnostics("FailureDemo");
                
                // Intentionally cause a graphics-related failure
                throw new D3D12Exception("Mock graphics device failure for testing");
            }
            catch (Exception ex)
            {
                var failureContext = new Dictionary<string, object>
                {
                    ["TestPhase"] = "GraphicsFailure",
                    ["GraphicsAPI"] = "D3D12",
                    ["FailureType"] = "DeviceFailure"
                };
                
                FailureAnalyzer.AnalyzeFailure(ex, GetType().Name, nameof(FailureWithGraphicsDebugging_ComprehensiveAnalysis), failureContext);
                ExecutionLogger.LogDetailedStackTrace("FailureDemo", ex);
                
                throw;
            }
        }
    }
}

// Supporting data structures for the examples
public class MemorySnapshot
{
    public int ResourceIndex { get; set; }
    public long MemoryBytes { get; set; }
    public long DeltaFromStart { get; set; }
    public DateTime Timestamp { get; set; }
}

public class CreationStatistics
{
    public int TotalResources { get; set; }
    public double AverageCreationTime { get; set; }
    public double MedianCreationTime { get; set; }
    public double P95CreationTime { get; set; }
    public double P99CreationTime { get; set; }
    public double StandardDeviation { get; set; }
}

public class BenchmarkResult
{
    public int Run { get; set; }
    public double TotalTimeMs { get; set; }
    public double AverageTimeMs { get; set; }
    public double MedianTimeMs { get; set; }
    public double P95TimeMs { get; set; }
    public double MinTimeMs { get; set; }
    public double MaxTimeMs { get; set; }
    public double StdDev { get; set; }
    public bool MeetsTarget { get; set; }
    public double TargetAchievement { get; set; }
}

public class TrendAnalysis
{
    public List<BenchmarkResult> Runs { get; set; } = new();
    public double AveragePerformance { get; set; }
    public double PerformanceVariance { get; set; }
    public bool IsPerformanceStable { get; set; }
    public double TargetAchievement { get; set; }
}

// Mock D3D12 exception for testing
public class D3D12Exception : Exception
{
    public D3D12Exception(string message) : base(message) { }
}

// Simulated complex graphics operation for benchmarking
public static class GraphicsOperationSimulator
{
    public static object SimulateComplexGraphicsOperation()
    {
        // Simulate complex graphics pipeline operation
        var matrix = new float[4, 4];
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                matrix[i, j] = (float)Math.Sin(i * j) * (float)Math.Cos(i + j);
            }
        }
        
        // Simulate additional processing
        var vertices = new float[1000 * 3];
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = (float)Math.Sin(i * 0.01) * 100.0f;
        }
        
        return new { Matrix = matrix, Vertices = vertices, Timestamp = DateTime.UtcNow };
    }
}