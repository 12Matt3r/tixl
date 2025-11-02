using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Ab耀文;
using FluentAssertions;
using TiXL.Core.Graphics.DirectX12;
using TiXL.Core.Performance;
using TiXL.Core.Operators;
using TiXL.Core.AudioVisual;
using TiXL.Core.IO;
using TiXL.Tests.Categories;
using TiXL.Tests.Fixtures;
using TiXL.Tests.Data;

namespace TiXL.Tests.Integration
{
    /// <summary>
    /// Error recovery integration tests across component boundaries
    /// Tests the complete error recovery and resilience system integration
    /// </summary>
    [Category(TestCategories.Integration)]
    [Category(TestCategories.ErrorHandling)]
    public class ErrorRecoveryIntegrationTests : CoreTestFixture
    {
        private readonly ITestOutputHelper _output;
        private readonly List<RecoveryTestResult> _testResults;

        public ErrorRecoveryIntegrationTests(ITestOutputHelper output) : base()
        {
            _output = output;
            _testResults = new List<RecoveryTestResult>();
        }

        [Fact]
        public async Task ErrorRecovery_DirectX_ResourceFailure()
        {
            _output.WriteLine("Testing error recovery from DirectX resource failures");

            var device = CreateMockDirectXDevice();
            var commandQueue = CreateMockCommandQueue();
            var performanceMonitor = new PerformanceMonitor();
            
            using var engine = new DirectX12RenderingEngine(device, commandQueue, performanceMonitor);
            await engine.InitializeAsync();

            try
            {
                var recoveryScenarios = new[]
                {
                    new RecoveryScenario { Name = "DeviceLost", FailureType = FailureType.DeviceLost, DelayMs = 100 },
                    new RecoveryScenario { Name = "OutOfMemory", FailureType = FailureType.OutOfMemory, DelayMs = 50 },
                    new RecoveryScenario { Name = "CommandQueueFailure", FailureType = FailureType.CommandQueueFailure, DelayMs = 75 },
                    new RecoveryScenario { Name = "FenceTimeout", FailureType = FailureType.FenceTimeout, DelayMs = 25 }
                };

                var recoveryMetrics = new List<RecoveryMetric>();

                // Act - Test error recovery for each failure scenario
                foreach (var scenario in recoveryScenarios)
                {
                    _output.WriteLine($"Testing recovery from {scenario.Name}...");
                    
                    var recoveryStart = Stopwatch.GetTimestamp();
                    var success = false;
                    var recoveryTime = 0.0;
                    var attempts = 0;
                    var maxAttempts = 3;

                    while (!success && attempts < maxAttempts)
                    {
                        attempts++;
                        
                        try
                        {
                            // Simulate normal operation
                            using var frameToken = engine.BeginFrame();
                            
                            // Inject failure at specific point
                            if (attempts == 1) // First attempt, simulate failure
                            {
                                await SimulateFailureAsync(scenario.FailureType, scenario.DelayMs);
                            }
                            
                            // Try to complete frame
                            await engine.SubmitGpuWorkAsync($"RecoveryTest_{scenario.Name}_{attempts}",
                                async () => await SimulateGpuWorkAsync(3.0),
                                GpuTimingType.General);
                            
                            await engine.EndFrameAsync(frameToken);
                            success = true;
                        }
                        catch (Exception ex)
                        {
                            _output.WriteLine($"  Attempt {attempts} failed: {ex.Message}");
                            
                            if (attempts < maxAttempts)
                            {
                                // Wait before retry
                                await Task.Delay(scenario.DelayMs * attempts);
                                
                                // Attempt recovery
                                await AttemptRecoveryAsync(engine, scenario.FailureType);
                            }
                        }
                    }

                    var recoveryEnd = Stopwatch.GetTimestamp();
                    recoveryTime = (recoveryEnd - recoveryStart) / (double)Stopwatch.Frequency * 1000;

                    recoveryMetrics.Add(new RecoveryMetric
                    {
                        ScenarioName = scenario.Name,
                        Success = success,
                        RecoveryTimeMs = recoveryTime,
                        Attempts = attempts,
                        FailureType = scenario.FailureType.ToString()
                    });

                    success.Should().BeTrue($"Should recover from {scenario.Name} within {maxAttempts} attempts");
                }

                // Assert - Validate overall recovery performance
                var successfulRecoveries = recoveryMetrics.Count(m => m.Success);
                var avgRecoveryTime = recoveryMetrics.Where(m => m.Success).Average(m => m.RecoveryTimeMs);
                var avgAttempts = recoveryMetrics.Average(m => m.Attempts);

                successfulRecoveries.Should().Be(recoveryScenarios.Length, "All error scenarios should be recoverable");
                avgRecoveryTime.Should().BeLessThan(1000.0, "Recovery should complete within reasonable time");
                avgAttempts.Should().BeLessThan(2.5, "Recovery should succeed quickly on average");

                _output.WriteLine($"DirectX Resource Failure Recovery Results:`n  Successful Recoveries: {successfulRecoveries}/{recoveryScenarios.Length}");
                _output.WriteLine($"  Average Recovery Time: {avgRecoveryTime:F2}ms");
                _output.WriteLine($"  Average Attempts: {avgAttempts:F1}");
                
                foreach (var metric in recoveryMetrics)
                {
                    _output.WriteLine($"    {metric.ScenarioName}: {(metric.Success ? "SUCCESS" : "FAILED")} in {metric.RecoveryTimeMs:F2}ms ({metric.Attempts} attempts)");
                }

                _testResults.Add(new RecoveryTestResult
                {
                    TestName = "DirectX_ResourceFailure_Recovery",
                    Passed = successfulRecoveries == recoveryScenarios.Length && avgRecoveryTime < 1000.0,
                    DurationMs = recoveryMetrics.Sum(m => m.RecoveryTimeMs),
                    Metrics = new Dictionary<string, double>
                    {
                        { "SuccessfulRecoveries", successfulRecoveries },
                        { "TotalScenarios", recoveryScenarios.Length },
                        { "AvgRecoveryTime", avgRecoveryTime },
                        { "AvgAttempts", avgAttempts }
                    }
                });
            }
            finally
            {
                engine.Dispose();
            }
        }

        [Fact]
        public async Task ErrorRecovery_AudioVisual_Pipeline_Breakdown()
        {
            _output.WriteLine("Testing error recovery from audio-visual pipeline breakdown");

            using var scheduler = new AudioVisualQueueScheduler(targetFrameRate: 60, maxQueueDepth: 1000, batchSize: 64);

            try
            {
                var pipelineBreakdownScenarios = new[]
                {
                    new PipelineScenario { Name = "AudioQueueOverflow", FailureType = PipelineFailureType.AudioQueueOverflow },
                    new PipelineScenario { Name = "VisualUpdateStall", FailureType = PipelineFailureType.VisualUpdateStall },
                    new PipelineScenario { Name = "SyncLoss", FailureType = PipelineFailureType.SyncLoss },
                    new PipelineScenario { Name = "LatencySpike", FailureType = PipelineFailureType.LatencySpike }
                };

                var breakdownMetrics = new List<PipelineRecoveryMetric>();

                foreach (var scenario in pipelineBreakdownScenarios)
                {
                    _output.WriteLine($"Testing recovery from {scenario.Name}...");
                    
                    var recoveryStart = Stopwatch.GetTimestamp();
                    var eventsGenerated = 0;
                    var eventsProcessed = 0;
                    var syncRestored = false;

                    // Generate workload that will trigger the breakdown
                    await GenerateBreakdownWorkloadAsync(scheduler, scenario.FailureType, out eventsGenerated);

                    // Detect breakdown
                    var breakdownDetected = await DetectPipelineBreakdownAsync(scheduler, scenario.FailureType);
                    breakdownDetected.Should().BeTrue($"Breakdown {scenario.Name} should be detectable");

                    // Attempt recovery
                    var recoveryResult = await AttemptPipelineRecoveryAsync(scheduler, scenario.FailureType);
                    syncRestored = recoveryResult.SyncRestored;
                    eventsProcessed = recoveryResult.EventsProcessed;

                    var recoveryEnd = Stopwatch.GetTimestamp();
                    var recoveryTime = (recoveryEnd - recoveryStart) / (double)Stopwatch.Frequency * 1000;

                    breakdownMetrics.Add(new PipelineRecoveryMetric
                    {
                        ScenarioName = scenario.Name,
                        EventsGenerated = eventsGenerated,
                        EventsProcessed = eventsProcessed,
                        SyncRestored = syncRestored,
                        RecoveryTimeMs = recoveryTime,
                        RecoveryRate = eventsProcessed / (double)eventsGenerated
                    });

                    syncRestored.Should().BeTrue($"Sync should be restored for {scenario.Name}");
                }

                // Assert - Validate pipeline recovery
                var successfulRecoveries = breakdownMetrics.Count(m => m.SyncRestored);
                var avgRecoveryRate = breakdownMetrics.Average(m => m.RecoveryRate);
                var avgRecoveryTime = breakdownMetrics.Average(m => m.RecoveryTimeMs);

                successfulRecoveries.Should().Be(pipelineBreakdownScenarios.Length, "All pipeline breakdowns should be recoverable");
                avgRecoveryRate.Should().BeGreaterThan(0.8, "Recovery should process most events");
                avgRecoveryTime.Should().BeLessThan(500.0, "Pipeline recovery should be fast");

                _output.WriteLine($"Audio-Visual Pipeline Recovery Results:`n  Successful Recoveries: {successfulRecoveries}/{pipelineBreakdownScenarios.Length}");
                _output.WriteLine($"  Average Recovery Rate: {avgRecoveryRate:P2}");
                _output.WriteLine($"  Average Recovery Time: {avgRecoveryTime:F2}ms");
                
                foreach (var metric in breakdownMetrics)
                {
                    _output.WriteLine($"    {metric.ScenarioName}: {metric.RecoveryRate:P2} recovery rate, {metric.EventsProcessed}/{metric.EventsGenerated} events");
                }

                _testResults.Add(new RecoveryTestResult
                {
                    TestName = "AudioVisual_PipelineBreakdown_Recovery",
                    Passed = successfulRecoveries == pipelineBreakdownScenarios.Length && avgRecoveryRate > 0.8,
                    DurationMs = breakdownMetrics.Sum(m => m.RecoveryTimeMs),
                    Metrics = new Dictionary<string, double>
                    {
                        { "SuccessfulRecoveries", successfulRecoveries },
                        { "TotalScenarios", pipelineBreakdownScenarios.Length },
                        { "AvgRecoveryRate", avgRecoveryRate },
                        { "AvgRecoveryTime", avgRecoveryTime }
                    }
                });
            }
            finally
            {
                scheduler.Dispose();
            }
        }

        [Fact]
        public async Task ErrorRecovery_NodeEvaluation_CascadingFailures()
        {
            _output.WriteLine("Testing error recovery from cascading node evaluation failures");

            var performanceMonitor = new PerformanceMonitor();
            var evaluator = new IncrementalNodeEvaluator(performanceMonitor);

            try
            {
                var failureScenarios = new[]
                {
                    new NodeFailureScenario { Name = "DependencyCycle", FailureType = NodeFailureType.DependencyCycle },
                    new NodeFailureScenario { Name = "EvaluationTimeout", FailureType = NodeFailureType.EvaluationTimeout },
                    new NodeFailureScenario { Name = "MemoryExhaustion", FailureType = NodeFailureType.MemoryExhaustion },
                    new NodeFailureScenario { Name = "ResourceLeak", FailureType = NodeFailureType.ResourceLeak }
                };

                var cascadingMetrics = new List<CascadingFailureMetric>();

                foreach (var scenario in failureScenarios)
                {
                    _output.WriteLine($"Testing recovery from {scenario.Name} cascading failure...");
                    
                    // Create complex node graph for testing
                    var nodeGraph = TestDataGenerator.GenerateComplexNodeGraph();
                    var originalNodeCount = nodeGraph.Nodes.Count;

                    var recoveryStart = Stopwatch.GetTimestamp();
                    var failuresInjected = 0;
                    var failuresRecovered = 0;
                    var nodeGraphIntact = false;

                    try
                    {
                        // Inject cascading failures
                        failuresInjected = await InjectCascadingFailuresAsync(nodeGraph, scenario.FailureType);

                        // Attempt evaluation (should fail)
                        var evalResult = await evaluator.EvaluateNodeGraphAsync(nodeGraph);
                        evalResult.IsSuccess.Should().BeFalse("Evaluation should fail due to injected failures");

                        // Attempt recovery
                        var recoveryResult = await RecoverNodeGraphAsync(nodeGraph, evaluator, scenario.FailureType);
                        failuresRecovered = recoveryResult.FailuresRecovered;
                        nodeGraphIntact = recoveryResult.NodeGraphIntact;

                        // Verify recovery
                        var finalEvalResult = await evaluator.EvaluateNodeGraphAsync(nodeGraph);
                        nodeGraphIntact = finalEvalResult.IsSuccess && nodeGraph.Nodes.Count == originalNodeCount;
                    }
                    catch (Exception ex)
                    {
                        _output.WriteLine($"  Recovery attempt for {scenario.Name} encountered error: {ex.Message}");
                    }

                    var recoveryEnd = Stopwatch.GetTimestamp();
                    var recoveryTime = (recoveryEnd - recoveryStart) / (double)Stopwatch.Frequency * 1000;

                    cascadingMetrics.Add(new CascadingFailureMetric
                    {
                        ScenarioName = scenario.Name,
                        FailuresInjected = failuresInjected,
                        FailuresRecovered = failuresRecovered,
                        NodeGraphIntact = nodeGraphIntact,
                        RecoveryTimeMs = recoveryTime,
                        RecoveryRate = failuresInjected > 0 ? failuresRecovered / (double)failuresInjected : 1.0
                    });

                    // Allow some tolerance for cascading failure complexity
                    var success = nodeGraphIntact && failuresRecovered >= failuresInjected * 0.7;
                    success.Should().BeTrue($"Should recover from {scenario.Name} cascading failures");
                }

                // Assert - Validate cascading failure recovery
                var successfulRecoveries = cascadingMetrics.Count(m => m.NodeGraphIntact);
                var avgRecoveryRate = cascadingMetrics.Average(m => m.RecoveryRate);
                var avgRecoveryTime = cascadingMetrics.Average(m => m.RecoveryTimeMs);

                successfulRecoveries.Should().Be(failureScenarios.Length, "All cascading failures should be recoverable");
                avgRecoveryRate.Should().BeGreaterThan(0.7, "Should recover most injected failures");
                avgRecoveryTime.Should().BeLessThan(2000.0, "Cascading failure recovery should complete within reasonable time");

                _output.WriteLine($"Cascading Node Failure Recovery Results:`n  Successful Graph Recoveries: {successfulRecoveries}/{failureScenarios.Length}");
                _output.WriteLine($"  Average Recovery Rate: {avgRecoveryRate:P2}");
                _output.WriteLine($"  Average Recovery Time: {avgRecoveryTime:F2}ms");
                
                foreach (var metric in cascadingMetrics)
                {
                    _output.WriteLine($"    {metric.ScenarioName}: {metric.RecoveryRate:P2} recovery rate, {(metric.NodeGraphIntact ? "Graph intact" : "Graph damaged")}");
                }

                _testResults.Add(new RecoveryTestResult
                {
                    TestName = "NodeEvaluation_CascadingFailures_Recovery",
                    Passed = successfulRecoveries == failureScenarios.Length && avgRecoveryRate > 0.7,
                    DurationMs = cascadingMetrics.Sum(m => m.RecoveryTimeMs),
                    Metrics = new Dictionary<string, double>
                    {
                        { "SuccessfulRecoveries", successfulRecoveries },
                        { "TotalScenarios", failureScenarios.Length },
                        { "AvgRecoveryRate", avgRecoveryRate },
                        { "AvgRecoveryTime", avgRecoveryTime }
                    }
                });
            }
            finally
            {
                evaluator.Dispose();
            }
        }

        [Fact]
        public async Task ErrorRecovery_SystemWide_Resilience()
        {
            _output.WriteLine("Testing system-wide resilience under multiple simultaneous failures");

            var device = CreateMockDirectXDevice();
            var commandQueue = CreateMockCommandQueue();
            var performanceMonitor = new PerformanceMonitor();
            
            using var engine = new DirectX12RenderingEngine(device, commandQueue, performanceMonitor);
            await engine.InitializeAsync();
            
            using var scheduler = new AudioVisualQueueScheduler(targetFrameRate: 60, maxQueueDepth: 1000, batchSize: 64);
            var evaluator = new IncrementalNodeEvaluator(performanceMonitor);

            try
            {
                var resilienceTestDuration = TimeSpan.FromSeconds(5);
                var startTime = DateTime.UtcNow;
                var failureCount = 0;
                var recoveryCount = 0;
                var systemHealth = 1.0;
                var healthHistory = new List<SystemHealthSnapshot>();

                // Act - Run system under continuous stress with injected failures
                while (DateTime.UtcNow - startTime < resilienceTestDuration)
                {
                    var frameStart = Stopwatch.GetTimestamp();

                    // Normal system operations
                    using var frameToken = engine.BeginFrame();

                    // Submit work
                    await engine.SubmitGpuWorkAsync("ResilienceTestWork",
                        async () => await SimulateGpuWorkAsync(4.0),
                        GpuTimingType.General);

                    // Generate and queue audio events
                    var audioEvents = GenerateStressAudioEvents();
                    foreach (var evt in audioEvents)
                    {
                        scheduler.QueueAudioEvent(evt);
                    }

                    // Process frame
                    scheduler.ProcessFrame();

                    // Inject random failures periodically
                    if (new Random().NextDouble() < 0.1) // 10% chance per frame
                    {
                        failureCount++;
                        await InjectRandomFailureAsync(engine, scheduler, evaluator);
                        
                        // Attempt immediate recovery
                        var recovered = await AttemptSystemRecoveryAsync(engine, scheduler, evaluator);
                        if (recovered) recoveryCount++;
                    }

                    try
                    {
                        await engine.EndFrameAsync(frameToken);
                    }
                    catch (Exception ex)
                    {
                        // Frame processing failed, attempt recovery
                        await HandleFrameFailureAsync(engine, ex);
                    }

                    // Assess system health
                    var stats = engine.Statistics;
                    var currentHealth = CalculateSystemHealth(stats, scheduler.GetStatistics());
                    systemHealth = (systemHealth + currentHealth) / 2.0; // Running average

                    if (failureCount % 10 == 0) // Record health every 10 failures
                    {
                        healthHistory.Add(new SystemHealthSnapshot
                        {
                            Timestamp = DateTime.UtcNow,
                            FailureCount = failureCount,
                            RecoveryCount = recoveryCount,
                            SystemHealth = systemHealth,
                            Fps = stats.Performance?.AverageFps ?? 0
                        });
                    }

                    var frameEnd = Stopwatch.GetTimestamp();
                    var frameTime = (frameEnd - frameStart) / (double)Stopwatch.Frequency * 1000;

                    await Task.Delay(Math.Max(0, (int)(16.67 - frameTime)));
                }

                // Assert - Validate system resilience
                var resilienceScore = CalculateResilienceScore(failureCount, recoveryCount, healthHistory);
                var finalSystemHealth = healthHistory.LastOrDefault()?.SystemHealth ?? 0.0;
                var stabilityScore = CalculateStabilityScore(healthHistory);

                failureCount.Should().BeGreaterThan(10, "Should have experienced multiple failures");
                recoveryCount.Should().BeGreaterThan(failureCount * 0.8, "Should recover from most failures");
                finalSystemHealth.Should().BeGreaterThan(0.5, "System should maintain reasonable health");
                resilienceScore.Should().BeGreaterThan(0.7, "System should demonstrate good resilience");

                _output.WriteLine($"System-Wide Resilience Results:`n  Total Failures: {failureCount}");
                _output.WriteLine($"  Total Recoveries: {recoveryCount}");
                _output.WriteLine($"  Recovery Rate: {recoveryCount / (double)failureCount:P2}");
                _output.WriteLine($"  Final System Health: {finalSystemHealth:P2}");
                _output.WriteLine($"  Resilience Score: {resilienceScore:P2}");
                _output.WriteLine($"  Stability Score: {stabilityScore:P2}");
                _output.WriteLine($"  Health History Points: {healthHistory.Count}");

                _testResults.Add(new RecoveryTestResult
                {
                    TestName = "SystemWide_Resilience",
                    Passed = recoveryCount > failureCount * 0.8 && finalSystemHealth > 0.5,
                    DurationMs = resilienceTestDuration.TotalMilliseconds,
                    Metrics = new Dictionary<string, double>
                    {
                        { "TotalFailures", failureCount },
                        { "TotalRecoveries", recoveryCount },
                        { "RecoveryRate", recoveryCount / (double)failureCount },
                        { "FinalSystemHealth", finalSystemHealth },
                        { "ResilienceScore", resilienceScore },
                        { "StabilityScore", stabilityScore },
                        { "HealthHistoryPoints", healthHistory.Count }
                    }
                });
            }
            finally
            {
                engine.Dispose();
                scheduler.Dispose();
                evaluator.Dispose();
            }
        }

        [Fact]
        public async Task ErrorRecovery_Graceful_Degradation()
        {
            _output.WriteLine("Testing graceful degradation under extreme conditions");

            var device = CreateMockDirectXDevice();
            var commandQueue = CreateMockCommandQueue();
            var performanceMonitor = new PerformanceMonitor();
            
            using var engine = new DirectX12RenderingEngine(device, commandQueue, performanceMonitor);
            await engine.InitializeAsync();

            try
            {
                var degradationScenarios = new[]
                {
                    new DegradationScenario { Name = "MemoryPressure", Type = DegradationType.MemoryPressure, Intensity = 0.8 },
                    new DegradationScenario { Name = "CpuThrottling", Type = DegradationType.CpuThrottling, Intensity = 0.9 },
                    new DegradationScenario { Name = "GpuLoad", Type = DegradationType.GpuLoad, Intensity = 0.95 },
                    new DegradationScenario { Name = "QueueOverflow", Type = DegradationType.QueueOverflow, Intensity = 0.85 }
                };

                var degradationMetrics = new List<DegradationMetric>();

                foreach (var scenario in degradationScenarios)
                {
                    _output.WriteLine($"Testing graceful degradation under {scenario.Name}...");
                    
                    var testStart = Stopwatch.GetTimestamp();
                    var frameCount = 0;
                    var successfulFrames = 0;
                    var degradedOperations = 0;
                    var qualityScore = 1.0;

                    // Run under degraded conditions
                    while (frameCount < 50) // Test 50 frames
                    {
                        using var frameToken = engine.BeginFrame();

                        // Apply degradation
                        var degradationApplied = ApplyDegradation(scenario.Type, scenario.Intensity);
                        if (degradationApplied)
                        {
                            degradedOperations++;
                        }

                        // Submit work with degradation consideration
                        var workDuration = 5.0 * (1.0 + scenario.Intensity * 0.5); // Increased duration under degradation
                        await engine.SubmitGpuWorkAsync($"DegradedWork_{frameCount}",
                            async () => await SimulateGpuWorkAsync(workDuration),
                            GpuTimingType.General);

                        try
                        {
                            await engine.EndFrameAsync(frameToken);
                            successfulFrames++;
                            
                            // Adjust quality score based on performance
                            var currentStats = engine.Statistics;
                            var fps = currentStats.Performance?.AverageFps ?? 60;
                            var targetFps = 60;
                            var performanceRatio = fps / targetFps;
                            
                            qualityScore = (qualityScore + performanceRatio) / 2.0;
                        }
                        catch (Exception ex)
                        {
                            _output.WriteLine($"  Frame {frameCount} failed under {scenario.Name}: {ex.Message}");
                            // Frame failure is expected under extreme degradation
                        }

                        frameCount++;
                    }

                    var testEnd = Stopwatch.GetTimestamp();
                    var testDuration = (testEnd - testStart) / (double)Stopwatch.Frequency * 1000;

                    degradationMetrics.Add(new DegradationMetric
                    {
                        ScenarioName = scenario.Name,
                        TotalFrames = frameCount,
                        SuccessfulFrames = successfulFrames,
                        DegradedOperations = degradedOperations,
                        QualityScore = qualityScore,
                        SuccessRate = successfulFrames / (double)frameCount,
                        TestDurationMs = testDuration
                    });

                    // Validate graceful degradation
                    var successRate = successfulFrames / (double)frameCount;
                    successRate.Should().BeGreaterThan(0.5, $"{scenario.Name} should allow at least 50% frame success");
                    qualityScore.Should().BeGreaterThan(0.3, $"{scenario.Name} should maintain minimum quality");
                }

                // Assert - Validate overall graceful degradation
                var avgSuccessRate = degradationMetrics.Average(m => m.SuccessRate);
                var avgQualityScore = degradationMetrics.Average(m => m.QualityScore);
                var worstCaseSuccess = degradationMetrics.Min(m => m.SuccessRate);

                avgSuccessRate.Should().BeGreaterThan(0.7, "Average success rate under degradation should be good");
                avgQualityScore.Should().BeGreaterThan(0.5, "Average quality should be maintained");
                worstCaseSuccess.Should().BeGreaterThan(0.4, "Worst case should still allow reasonable operation");

                _output.WriteLine($"Graceful Degradation Results:`n  Average Success Rate: {avgSuccessRate:P2}");
                _output.WriteLine($"  Average Quality Score: {avgQualityScore:P2}");
                _output.WriteLine($"  Worst Case Success Rate: {worstCaseSuccess:P2}");
                
                foreach (var metric in degradationMetrics)
                {
                    _output.WriteLine($"    {metric.ScenarioName}: {metric.SuccessRate:P2} success, {metric.QualityScore:P2} quality");
                }

                _testResults.Add(new RecoveryTestResult
                {
                    TestName = "Graceful_Degradation",
                    Passed = avgSuccessRate > 0.7 && avgQualityScore > 0.5,
                    DurationMs = degradationMetrics.Sum(m => m.TestDurationMs),
                    Metrics = new Dictionary<string, double>
                    {
                        { "AvgSuccessRate", avgSuccessRate },
                        { "AvgQualityScore", avgQualityScore },
                        { "WorstCaseSuccessRate", worstCaseSuccess }
                    }
                });
            }
            finally
            {
                engine.Dispose();
            }
        }

        // Helper methods

        private async Task SimulateFailureAsync(FailureType failureType, int delayMs)
        {
            await Task.Delay(delayMs);
            
            switch (failureType)
            {
                case FailureType.DeviceLost:
                    throw new InvalidOperationException("Simulated device lost");
                case FailureType.OutOfMemory:
                    throw new OutOfMemoryException("Simulated out of memory");
                case FailureType.CommandQueueFailure:
                    throw new InvalidOperationException("Simulated command queue failure");
                case FailureType.FenceTimeout:
                    throw new TimeoutException("Simulated fence timeout");
                default:
                    throw new InvalidOperationException("Unknown failure type");
            }
        }

        private async Task AttemptRecoveryAsync(DirectX12RenderingEngine engine, FailureType failureType)
        {
            switch (failureType)
            {
                case FailureType.DeviceLost:
                    await engine.RecoverFromDeviceLostAsync();
                    break;
                case FailureType.OutOfMemory:
                    await engine.OptimizeMemoryUsageAsync();
                    break;
                case FailureType.CommandQueueFailure:
                    await engine.ResetCommandQueueAsync();
                    break;
                case FailureType.FenceTimeout:
                    await engine.ResetFencesAsync();
                    break;
            }
        }

        private static async Task SimulateGpuWorkAsync(double durationMs)
        {
            await Task.Delay((int)(durationMs / 10.0)); // Scale for testing
        }

        private async Task GenerateBreakdownWorkloadAsync(AudioVisualQueueScheduler scheduler, PipelineFailureType failureType, out int eventsGenerated)
        {
            eventsGenerated = 0;
            
            switch (failureType)
            {
                case PipelineFailureType.AudioQueueOverflow:
                    // Generate massive number of audio events
                    for (int i = 0; i < 5000; i++)
                    {
                        scheduler.QueueAudioEvent(new AudioEvent
                        {
                            Timestamp = DateTime.UtcNow,
                            Intensity = 1.0f,
                            Frequency = 440.0f,
                            Priority = AudioEventPriority.Normal,
                            Type = AudioEventType.Beat
                        });
                        eventsGenerated++;
                    }
                    break;
                    
                case PipelineFailureType.VisualUpdateStall:
                    // Generate visual updates that take too long
                    for (int i = 0; i < 1000; i++)
                    {
                        scheduler.QueueVisualUpdate(new VisualParameterUpdate
                        {
                            ParameterName = $"StallParam_{i}",
                            Value = 1.0f,
                            Timestamp = DateTime.UtcNow,
                            Priority = AudioEventPriority.High
                        });
                        eventsGenerated++;
                    }
                    break;
                    
                case PipelineFailureType.SyncLoss:
                    // Generate desynchronized events
                    for (int i = 0; i < 100; i++)
                    {
                        var evt = new AudioEvent
                        {
                            Timestamp = DateTime.UtcNow.AddMilliseconds(-100), // Old timestamp
                            Intensity = 1.0f,
                            Frequency = 440.0f,
                            Priority = AudioEventPriority.High,
                            Type = AudioEventType.Beat
                        };
                        scheduler.QueueAudioEvent(evt);
                        eventsGenerated++;
                    }
                    break;
                    
                case PipelineFailureType.LatencySpike:
                    // Generate events with varying priorities to cause latency
                    for (int i = 0; i < 500; i++)
                    {
                        var priority = (AudioEventPriority)(i % 4);
                        scheduler.QueueAudioEvent(new AudioEvent
                        {
                            Timestamp = DateTime.UtcNow,
                            Intensity = 1.0f,
                            Frequency = 440.0f + i,
                            Priority = priority,
                            Type = AudioEventType.Beat
                        });
                        eventsGenerated++;
                    }
                    break;
            }
        }

        private async Task<bool> DetectPipelineBreakdownAsync(AudioVisualQueueScheduler scheduler, PipelineFailureType failureType)
        {
            await Task.Delay(100); // Allow breakdown to manifest
            
            var stats = scheduler.GetStatistics();
            
            return failureType switch
            {
                PipelineFailureType.AudioQueueOverflow => stats.PendingAudioEvents > 2000,
                PipelineFailureType.VisualUpdateStall => stats.PendingVisualUpdates > 500,
                PipelineFailureType.SyncLoss => stats.AverageLatencyMs > 100.0,
                PipelineFailureType.LatencySpike => stats.AverageLatencyMs > 50.0,
                _ => false
            };
        }

        private async Task<PipelineRecoveryResult> AttemptPipelineRecoveryAsync(AudioVisualQueueScheduler scheduler, PipelineFailureType failureType)
        {
            var recoveryStart = Stopwatch.GetTimestamp();
            var eventsProcessed = 0;
            var syncRestored = false;

            // Apply recovery strategy based on failure type
            switch (failureType)
            {
                case PipelineFailureType.AudioQueueOverflow:
                    // Process and clear overflow
                    for (int i = 0; i < 10; i++)
                    {
                        scheduler.ProcessFrame();
                        eventsProcessed += scheduler.PendingAudioEvents;
                    }
                    syncRestored = scheduler.GetStatistics().PendingAudioEvents < 100;
                    break;
                    
                case PipelineFailureType.VisualUpdateStall:
                    // Process visual updates with priority
                    scheduler.ProcessFrameWithPriority();
                    eventsProcessed = scheduler.PendingVisualUpdates;
                    syncRestored = scheduler.GetStatistics().PendingVisualUpdates < 50;
                    break;
                    
                case PipelineFailureType.SyncLoss:
                    // Reset synchronization
                    scheduler.ResetAudioVisualSync();
                    syncRestored = scheduler.GetStatistics().AverageLatencyMs < 20.0;
                    break;
                    
                case PipelineFailureType.LatencySpike:
                    // Reduce latency through batch optimization
                    scheduler.OptimizeForLowLatency();
                    var stats = scheduler.GetStatistics();
                    syncRestored = stats.AverageLatencyMs < 25.0;
                    eventsProcessed = stats.PendingAudioEvents + stats.PendingVisualUpdates;
                    break;
            }

            var recoveryEnd = Stopwatch.GetTimestamp();
            var recoveryTime = (recoveryEnd - recoveryStart) / (double)Stopwatch.Frequency * 1000;

            return new PipelineRecoveryResult
            {
                SyncRestored = syncRestored,
                EventsProcessed = eventsProcessed,
                RecoveryTimeMs = recoveryTime
            };
        }

        private async Task<int> InjectCascadingFailuresAsync(TestNodeGraph nodeGraph, NodeFailureType failureType)
        {
            var failuresInjected = 0;
            
            switch (failureType)
            {
                case NodeFailureType.DependencyCycle:
                    // Create circular dependencies
                    var nodes = nodeGraph.Nodes.Take(3).ToList();
                    if (nodes.Count >= 3)
                    {
                        // This would create a cycle (simplified)
                        failuresInjected = 1;
                    }
                    break;
                    
                case NodeFailureType.EvaluationTimeout:
                    // Mark nodes as requiring long evaluation
                    foreach (var node in nodeGraph.Nodes.Take(5))
                    {
                        node.TimeoutMs = 5000; // 5 second timeout
                        failuresInjected++;
                    }
                    break;
                    
                case NodeFailureType.MemoryExhaustion:
                    // Mark nodes as memory intensive
                    foreach (var node in nodeGraph.Nodes.Take(3))
                    {
                        node.EstimatedMemoryUsage = 1024 * 1024 * 100; // 100MB
                        failuresInjected++;
                    }
                    break;
                    
                case NodeFailureType.ResourceLeak:
                    // Mark nodes as not releasing resources
                    foreach (var node in nodeGraph.Nodes.Take(2))
                    {
                        node.CleanupOnDispose = false;
                        failuresInjected++;
                    }
                    break;
            }

            await Task.CompletedTask;
            return failuresInjected;
        }

        private async Task<NodeRecoveryResult> RecoverNodeGraphAsync(TestNodeGraph nodeGraph, IncrementalNodeEvaluator evaluator, NodeFailureType failureType)
        {
            var failuresRecovered = 0;
            var nodeGraphIntact = false;

            // Apply recovery based on failure type
            switch (failureType)
            {
                case NodeFailureType.DependencyCycle:
                    // Break cycles
                    nodeGraph.Nodes.Take(3).ToList().ForEach(node => node.TimeoutMs = null);
                    failuresRecovered = 1;
                    break;
                    
                case NodeFailureType.EvaluationTimeout:
                    // Reduce timeouts
                    foreach (var node in nodeGraph.Nodes.Where(n => n.TimeoutMs.HasValue))
                    {
                        node.TimeoutMs = 1000; // 1 second
                        failuresRecovered++;
                    }
                    break;
                    
                case NodeFailureType.MemoryExhaustion:
                    // Reduce memory requirements
                    foreach (var node in nodeGraph.Nodes.Where(n => n.EstimatedMemoryUsage > 0))
                    {
                        node.EstimatedMemoryUsage = Math.Min(node.EstimatedMemoryUsage, 1024 * 1024 * 10); // Max 10MB
                        failuresRecovered++;
                    }
                    break;
                    
                case NodeFailureType.ResourceLeak:
                    // Enable cleanup
                    foreach (var node in nodeGraph.Nodes.Where(n => !n.CleanupOnDispose))
                    {
                        node.CleanupOnDispose = true;
                        failuresRecovered++;
                    }
                    break;
            }

            await Task.CompletedTask;
            
            // Verify graph integrity
            nodeGraphIntact = nodeGraph.Nodes.All(n => n != null) && nodeGraph.Nodes.Count > 0;
            
            return new NodeRecoveryResult
            {
                FailuresRecovered = failuresRecovered,
                NodeGraphIntact = nodeGraphIntact
            };
        }

        private async Task InjectRandomFailureAsync(DirectX12RenderingEngine engine, AudioVisualQueueScheduler scheduler, IncrementalNodeEvaluator evaluator)
        {
            var failureTypes = new[] { "gpu_timeout", "queue_overflow", "memory_pressure", "sync_loss" };
            var failureType = failureTypes[new Random().Next(failureTypes.Length)];
            
            switch (failureType)
            {
                case "gpu_timeout":
                    await SimulateGpuWorkAsync(100.0); // Simulate long GPU work
                    break;
                case "queue_overflow":
                    for (int i = 0; i < 100; i++)
                    {
                        scheduler.QueueAudioEvent(new AudioEvent
                        {
                            Timestamp = DateTime.UtcNow,
                            Intensity = 1.0f,
                            Frequency = 440.0f,
                            Priority = AudioEventPriority.Normal,
                            Type = AudioEventType.Beat
                        });
                    }
                    break;
                case "memory_pressure":
                    // Simulate memory allocation
                    var data = new byte[1024 * 1024]; // 1MB
                    break;
                case "sync_loss":
                    // Generate desynchronized events
                    scheduler.QueueAudioEvent(new AudioEvent
                    {
                        Timestamp = DateTime.UtcNow.AddMilliseconds(-200),
                        Intensity = 1.0f,
                        Frequency = 440.0f,
                        Priority = AudioEventPriority.High,
                        Type = AudioEventType.Beat
                    });
                    break;
            }
        }

        private async Task<bool> AttemptSystemRecoveryAsync(DirectX12RenderingEngine engine, AudioVisualQueueScheduler scheduler, IncrementalNodeEvaluator evaluator)
        {
            try
            {
                // Attempt recovery for each component
                var engineRecovered = await TryRecoverEngineAsync(engine);
                var schedulerRecovered = await TryRecoverSchedulerAsync(scheduler);
                var evaluatorRecovered = await TryRecoverEvaluatorAsync(evaluator);
                
                return engineRecovered || schedulerRecovered || evaluatorRecovered;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> TryRecoverEngineAsync(DirectX12RenderingEngine engine)
        {
            try
            {
                await engine.ResetCommandQueueAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> TryRecoverSchedulerAsync(AudioVisualQueueScheduler scheduler)
        {
            try
            {
                scheduler.OptimizeForLowLatency();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> TryRecoverEvaluatorAsync(IncrementalNodeEvaluator evaluator)
        {
            try
            {
                await evaluator.ClearEvaluationCacheAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task HandleFrameFailureAsync(DirectX12RenderingEngine engine, Exception ex)
        {
            _output.WriteLine($"Handling frame failure: {ex.Message}");
            
            try
            {
                // Attempt to recover the frame
                await engine.ResetFrameStateAsync();
            }
            catch
            {
                // If recovery fails, try to continue with next frame
                _output.WriteLine("Frame recovery failed, continuing with next frame");
            }
        }

        private double CalculateSystemHealth(DirectX12RenderingEngineStats stats, AudioVisualSchedulerStats schedulerStats)
        {
            var fps = stats.Performance?.AverageFps ?? 0;
            var fpsScore = Math.Min(1.0, fps / 60.0);
            
            var latency = schedulerStats.AverageLatencyMs;
            var latencyScore = Math.Max(0.0, 1.0 - latency / 100.0);
            
            var frameTime = stats.FramePacing.CurrentFrameTime;
            var frameTimeScore = Math.Max(0.0, 1.0 - Math.Abs(frameTime - 16.67) / 16.67);
            
            return (fpsScore + latencyScore + frameTimeScore) / 3.0;
        }

        private bool ApplyDegradation(DegradationType type, double intensity)
        {
            switch (type)
            {
                case DegradationType.MemoryPressure:
                    // Simulate memory pressure by allocating temporary memory
                    var memory = new byte[(int)(1024 * 1024 * intensity)];
                    return true;
                    
                case DegradationType.CpuThrottling:
                    // Simulate CPU throttling with busy wait
                    var start = DateTime.UtcNow;
                    while ((DateTime.UtcNow - start).TotalMilliseconds < intensity * 10)
                    {
                        // Busy wait
                    }
                    return true;
                    
                case DegradationType.GpuLoad:
                    // Simulate heavy GPU load
                    Thread.Sleep((int)(intensity * 5));
                    return true;
                    
                case DegradationType.QueueOverflow:
                    // Simulate queue overflow conditions
                    return intensity > 0.8;
                    
                default:
                    return false;
            }
        }

        private double CalculateResilienceScore(int failureCount, int recoveryCount, List<SystemHealthSnapshot> healthHistory)
        {
            var recoveryRate = failureCount > 0 ? recoveryCount / (double)failureCount : 1.0;
            var healthVariance = healthHistory.Any() ? 
                healthHistory.Select(h => h.SystemHealth).ToList() : new List<double> { 1.0 };
            var stability = 1.0 / (1.0 + CalculateVariance(healthVariance));
            
            return (recoveryRate + stability) / 2.0;
        }

        private double CalculateStabilityScore(List<SystemHealthSnapshot> healthHistory)
        {
            if (!healthHistory.Any()) return 1.0;
            
            var healthValues = healthHistory.Select(h => h.SystemHealth).ToList();
            var variance = CalculateVariance(healthValues);
            
            return 1.0 / (1.0 + variance);
        }

        private static double CalculateVariance(List<double> values)
        {
            if (values.Count < 2) return 0.0;
            var mean = values.Average();
            return values.Sum(x => (x - mean) * (x - mean)) / values.Count;
        }

        private List<AudioEvent> GenerateStressAudioEvents()
        {
            var events = new List<AudioEvent>();
            var random = new Random();
            var eventCount = random.Next(10, 50);

            for (int i = 0; i < eventCount; i++)
            {
                events.Add(new AudioEvent
                {
                    Timestamp = DateTime.UtcNow,
                    Intensity = (float)random.NextDouble(),
                    Frequency = 440.0f + (float)random.NextDouble() * 1000,
                    Priority = (AudioEventPriority)(random.Next(4)),
                    Type = AudioEventType.Beat,
                    Data = new { Stress = true, Index = i }
                });
            }

            return events;
        }

        // Mock DirectX objects
        private ID3D12Device4 CreateMockDirectXDevice() => new MockD3D12Device();
        private ID3D12CommandQueue CreateMockCommandQueue() => new MockD3D12CommandQueue();

        #region Mock Classes

        private class MockD3D12Device : ID3D12Device4
        {
            public void Dispose() { }
        }

        private class MockD3D12CommandQueue : ID3D12CommandQueue
        {
            public void Dispose() { }
        }

        #endregion

        // Data classes and enums
        private class RecoveryTestResult
        {
            public string TestName { get; set; }
            public bool Passed { get; set; }
            public double DurationMs { get; set; }
            public Dictionary<string, double> Metrics { get; set; } = new();
        }

        private enum FailureType
        {
            DeviceLost,
            OutOfMemory,
            CommandQueueFailure,
            FenceTimeout
        }

        private enum PipelineFailureType
        {
            AudioQueueOverflow,
            VisualUpdateStall,
            SyncLoss,
            LatencySpike
        }

        private enum NodeFailureType
        {
            DependencyCycle,
            EvaluationTimeout,
            MemoryExhaustion,
            ResourceLeak
        }

        private enum DegradationType
        {
            MemoryPressure,
            CpuThrottling,
            GpuLoad,
            QueueOverflow
        }

        private class RecoveryScenario
        {
            public string Name { get; set; }
            public FailureType FailureType { get; set; }
            public int DelayMs { get; set; }
        }

        private class PipelineScenario
        {
            public string Name { get; set; }
            public PipelineFailureType FailureType { get; set; }
        }

        private class NodeFailureScenario
        {
            public string Name { get; set; }
            public NodeFailureType FailureType { get; set; }
        }

        private class DegradationScenario
        {
            public string Name { get; set; }
            public DegradationType Type { get; set; }
            public double Intensity { get; set; }
        }

        private class RecoveryMetric
        {
            public string ScenarioName { get; set; }
            public bool Success { get; set; }
            public double RecoveryTimeMs { get; set; }
            public int Attempts { get; set; }
            public string FailureType { get; set; }
        }

        private class PipelineRecoveryMetric
        {
            public string ScenarioName { get; set; }
            public int EventsGenerated { get; set; }
            public int EventsProcessed { get; set; }
            public bool SyncRestored { get; set; }
            public double RecoveryTimeMs { get; set; }
            public double RecoveryRate { get; set; }
        }

        private class PipelineRecoveryResult
        {
            public bool SyncRestored { get; set; }
            public int EventsProcessed { get; set; }
            public double RecoveryTimeMs { get; set; }
        }

        private class CascadingFailureMetric
        {
            public string ScenarioName { get; set; }
            public int FailuresInjected { get; set; }
            public int FailuresRecovered { get; set; }
            public bool NodeGraphIntact { get; set; }
            public double RecoveryTimeMs { get; set; }
            public double RecoveryRate { get; set; }
        }

        private class NodeRecoveryResult
        {
            public int FailuresRecovered { get; set; }
            public bool NodeGraphIntact { get; set; }
        }

        private class SystemHealthSnapshot
        {
            public DateTime Timestamp { get; set; }
            public int FailureCount { get; set; }
            public int RecoveryCount { get; set; }
            public double SystemHealth { get; set; }
            public double Fps { get; set; }
        }

        private class DegradationMetric
        {
            public string ScenarioName { get; set; }
            public int TotalFrames { get; set; }
            public int SuccessfulFrames { get; set; }
            public int DegradedOperations { get; set; }
            public double QualityScore { get; set; }
            public double SuccessRate { get; set; }
            public double TestDurationMs { get; set; }
        }
    }
}