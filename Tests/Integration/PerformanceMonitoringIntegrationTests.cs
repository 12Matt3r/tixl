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
    /// Performance monitoring integration tests across all TiXL components
    /// Tests the comprehensive performance monitoring system integration
    /// </summary>
    [Category(TestCategories.Integration)]
    [Category(TestCategories.Performance)]
    public class PerformanceMonitoringIntegrationTests : CoreTestFixture
    {
        private readonly ITestOutputHelper _output;
        private readonly PerformanceMetricsCollector _metricsCollector;
        private readonly List<MonitoringTestResult> _testResults;

        public PerformanceMonitoringIntegrationTests(ITestOutputHelper output) : base()
        {
            _output = output;
            _metricsCollector = new PerformanceMetricsCollector();
            _testResults = new List<MonitoringTestResult>();
        }

        [Fact]
        public async Task PerformanceMonitoring_CompleteSystem_Integration()
        {
            _output.WriteLine("Testing performance monitoring integration across all system components");

            var (engine, scheduler, evaluator, resourceManager) = await InitializeMonitoredSystem();
            
            try
            {
                // Subscribe to all performance events
                SubscribeToAllPerformanceEvents(engine, scheduler, evaluator, resourceManager, _metricsCollector);

                var monitoringDuration = TimeSpan.FromSeconds(5);
                var frameCount = 300; // 5 seconds at 60 FPS
                var startTime = DateTime.UtcNow;

                // Act - Run comprehensive workload while monitoring
                _output.WriteLine("Running 5-second performance monitoring test...");
                
                for (int frame = 0; frame < frameCount; frame++)
                {
                    using var frameToken = engine.BeginFrame();
                    var frameStart = Stopwatch.GetTimestamp();

                    // Node evaluation workload
                    var nodeGraph = TestDataGenerator.GenerateTestNodeGraph();
                    var evaluationResult = await evaluator.EvaluateNodeGraphAsync(nodeGraph);

                    // Audio-visual workload
                    var audioEvents = GenerateWorkloadAudioEvents(frame);
                    foreach (var evt in audioEvents)
                    {
                        scheduler.QueueAudioEvent(evt);
                    }

                    var visualUpdates = GenerateWorkloadVisualUpdates(frame);
                    foreach (var update in visualUpdates)
                    {
                        scheduler.QueueVisualUpdate(update);
                    }

                    // Resource management workload
                    var resourceOps = GenerateWorkloadResourceOperations(frame);
                    foreach (var op in resourceOps)
                    {
                        await resourceManager.ExecuteResourceOperationAsync(op);
                    }

                    // GPU workload
                    await SubmitWorkloadGpuWorkAsync(engine, frame);

                    // Process frame
                    scheduler.ProcessFrame();
                    await engine.EndFrameAsync(frameToken);

                    // Collect real-time metrics
                    if (frame % 10 == 0) // Collect every 10 frames
                    {
                        CollectRealTimeMetrics(engine, scheduler, evaluator, resourceManager);
                    }

                    // Maintain frame rate
                    var frameTime = (Stopwatch.GetTimestamp() - frameStart) / (double)Stopwatch.Frequency * 1000;
                    var targetFrameTime = 16.67;
                    if (frameTime < targetFrameTime)
                    {
                        await Task.Delay((int)(targetFrameTime - frameTime));
                    }
                }

                // Wait for monitoring period to complete
                var elapsed = DateTime.UtcNow - startTime;
                if (elapsed < monitoringDuration)
                {
                    await Task.Delay(monitoringDuration - elapsed);
                }

                // Assert - Analyze collected metrics
                var collectedMetrics = _metricsCollector.GetCollectedMetrics();
                
                ValidateComprehensiveMetrics(collectedMetrics, frameCount);
                
                _output.WriteLine($"Performance Monitoring Results:");
                _output.WriteLine($"  Total Frame Samples: {collectedMetrics.FrameMetrics.Count}");
                _output.WriteLine($"  Components Monitored: {collectedMetrics.ComponentMetrics.Count}");
                _output.WriteLine($"  Performance Alerts: {collectedMetrics.PerformanceAlerts.Count}");
                _output.WriteLine($"  System Health Score: {CalculateSystemHealthScore(collectedMetrics):P2}");
                _output.WriteLine($"  Overall Performance Grade: {CalculateOverallPerformanceGrade(collectedMetrics)}");

                _testResults.Add(new MonitoringTestResult
                {
                    TestName = "CompleteSystemPerformanceMonitoring",
                    Passed = collectedMetrics.FrameMetrics.Count >= frameCount * 0.9, // Allow 10% sampling loss
                    DurationMs = monitoringDuration.TotalMilliseconds,
                    Metrics = CreateMonitoringMetricsDictionary(collectedMetrics)
                });
            }
            finally
            {
                await CleanupMonitoredSystem(engine, scheduler, evaluator, resourceManager);
            }
        }

        [Fact]
        public async Task PerformanceMonitoring_RealTime_AlertSystem()
        {
            _output.WriteLine("Testing real-time performance alert system");

            var (engine, scheduler, evaluator, resourceManager) = await InitializeMonitoredSystem();
            
            try
            {
                var alertEvents = new List<PerformanceAlert>();
                var thresholdViolations = new List<ThresholdViolation>();

                // Subscribe to alerts
                engine.EngineAlert += (s, e) => alertEvents.Add(new PerformanceAlert
                {
                    Timestamp = DateTime.UtcNow,
                    Source = "Engine",
                    Type = e.AlertType.ToString(),
                    Message = e.Message,
                    Severity = MapAlertSeverity(e.AlertType)
                });

                scheduler.PerformanceMetrics += (s, e) =>
                {
                    if (e.Stats.AverageLatencyMs > 50.0) // Latency threshold
                    {
                        alertEvents.Add(new PerformanceAlert
                        {
                            Timestamp = DateTime.UtcNow,
                            Source = "Scheduler",
                            Type = "HighLatency",
                            Message = $"Average latency: {e.Stats.AverageLatencyMs:F2}ms",
                            Severity = AlertSeverity.Warning
                        });
                    }
                };

                // Act - Generate workload that should trigger alerts
                var alertTestFrames = 120; // 2 seconds
                
                for (int frame = 0; frame < alertTestFrames; frame++)
                {
                    using var frameToken = engine.BeginFrame();

                    // Gradually increase workload to trigger performance alerts
                    var loadFactor = frame / (double)alertTestFrames;
                    var gpuWorkload = 2.0 + (loadFactor * 20.0); // Up to 22ms GPU work
                    
                    await engine.SubmitGpuWorkAsync($"HeavyWorkload_{frame}",
                        async () => await SimulateHeavyGpuWorkAsync(gpuWorkload),
                        GpuTimingType.General);

                    // Generate high-frequency audio events
                    var eventCount = (int)(10 + loadFactor * 50); // Up to 60 events per frame
                    for (int i = 0; i < eventCount; i++)
                    {
                        scheduler.QueueAudioEvent(CreateHighIntensityAudioEvent());
                    }

                    // Process frame with high load
                    scheduler.ProcessFrame();
                    
                    try
                    {
                        await engine.EndFrameAsync(frameToken);
                    }
                    catch (Exception ex)
                    {
                        // Frame budget exceeded is expected in this test
                        alertEvents.Add(new PerformanceAlert
                        {
                            Timestamp = DateTime.UtcNow,
                            Source = "Engine",
                            Type = "FrameBudgetExceeded",
                            Message = $"Frame {frame} exceeded budget: {ex.Message}",
                            Severity = AlertSeverity.Error
                        });
                    }

                    // Maintain frame rate
                    await Task.Delay(Math.Max(0, (int)(16.67 - gpuWorkload)));
                }

                // Assert - Validate alert system functionality
                alertEvents.Should().NotBeEmpty("Alert system should generate performance alerts");

                var highSeverityAlerts = alertEvents.Where(a => a.Severity >= AlertSeverity.Warning).ToList();
                highSeverityAlerts.Should().NotBeEmpty("System should generate high-severity alerts for performance issues");

                var alertResponseTime = CalculateAlertResponseTime(alertEvents);
                alertResponseTime.Should().BeLessThan(1000, "Alerts should be generated within reasonable time");

                _output.WriteLine($"Alert System Test Results:");
                _output.WriteLine($"  Total Alerts Generated: {alertEvents.Count}");
                _output.WriteLine($"  High Severity Alerts: {highSeverityAlerts.Count}");
                _output.WriteLine($"  Alert Response Time: {alertResponseTime:F2}ms");
                _output.WriteLine($"  Alert Sources: {string.Join(", ", alertEvents.Select(a => a.Source).Distinct())}");

                _testResults.Add(new MonitoringTestResult
                {
                    TestName = "RealTimeAlertSystem",
                    Passed = alertEvents.Count > 0 && alertResponseTime < 1000,
                    DurationMs = alertTestFrames * 16.67,
                    Metrics = new Dictionary<string, double>
                    {
                        { "TotalAlerts", alertEvents.Count },
                        { "HighSeverityAlerts", highSeverityAlerts.Count },
                        { "AlertResponseTime", alertResponseTime }
                    }
                });
            }
            finally
            {
                await CleanupMonitoredSystem(engine, scheduler, evaluator, resourceManager);
            }
        }

        [Fact]
        public async Task PerformanceMonitoring_Adaptive_Optimization()
        {
            _output.WriteLine("Testing performance monitoring adaptive optimization");

            var (engine, scheduler, evaluator, resourceManager) = await InitializeMonitoredSystem();
            
            try
            {
                var optimizationEvents = new List<OptimizationEvent>();
                var adaptationMetrics = new List<AdaptationMetric>();

                // Track optimization events
                scheduler.PerformanceMetrics += (s, e) =>
                {
                    var optimization = DetectOptimizationOpportunity(e.Stats);
                    if (optimization != null)
                    {
                        optimizationEvents.Add(optimization);
                    }

                    adaptationMetrics.Add(new AdaptationMetric
                    {
                        Timestamp = DateTime.UtcNow,
                        QueueDepth = e.Stats.QueueDepth,
                        AverageLatencyMs = e.Stats.AverageLatencyMs,
                        FrameRate = e.Stats.CurrentFrameRate,
                        AdaptiveBatchSize = scheduler.AdaptiveBatchSize
                    });
                };

                // Act - Run workload with varying conditions to trigger adaptations
                var adaptationPhases = new[]
                {
                    new AdaptationPhase { Name = "Normal", LoadFactor = 1.0, Duration = 60 },
                    new AdaptationPhase { Name = "HighLoad", LoadFactor = 2.0, Duration = 60 },
                    new AdaptationPhase { Name = "Burst", LoadFactor = 5.0, Duration = 30 },
                    new AdaptationPhase { Name = "Recovery", LoadFactor = 0.5, Duration = 60 }
                };

                foreach (var phase in adaptationPhases)
                {
                    _output.WriteLine($"Testing {phase.Name} phase with load factor {phase.LoadFactor}...");
                    
                    for (int frame = 0; frame < phase.Duration; frame++)
                    {
                        using var frameToken = engine.BeginFrame();

                        // Generate adaptive workload
                        var workload = GenerateAdaptiveWorkload(phase.LoadFactor, frame);
                        
                        // Submit workload components
                        await SubmitAdaptiveWorkloadAsync(engine, scheduler, evaluator, resourceManager, workload);

                        scheduler.ProcessFrame();
                        await engine.EndFrameAsync(frameToken);

                        // Maintain frame rate
                        await Task.Delay(Math.Max(0, (int)(16.67 - workload.ExpectedFrameTime)));
                    }

                    // Wait for system to stabilize
                    await Task.Delay(100);
                }

                // Assert - Validate adaptive optimization
                optimizationEvents.Should().NotBeEmpty("System should detect and attempt optimizations");

                var effectiveOptimizations = optimizationEvents.Where(o => o.EstimatedImprovement > 0).ToList();
                effectiveOptimizations.Should().NotBeEmpty("Optimizations should be effective");

                var adaptationSpeed = CalculateAdaptationSpeed(adaptationMetrics);
                adaptationSpeed.Should().BeGreaterThan(0, "System should adapt to changing conditions");

                _output.WriteLine($"Adaptive Optimization Results:");
                _output.WriteLine($"  Total Optimizations Detected: {optimizationEvents.Count}");
                _output.WriteLine($"  Effective Optimizations: {effectiveOptimizations.Count}");
                _output.WriteLine($"  Adaptation Speed Score: {adaptationSpeed:P2}");
                _output.WriteLine($"  Adaptation Metrics Samples: {adaptationMetrics.Count}");

                _testResults.Add(new MonitoringTestResult
                {
                    TestName = "AdaptiveOptimization",
                    Passed = optimizationEvents.Count > 0 && adaptationSpeed > 0.5,
                    DurationMs = adaptationPhases.Sum(p => p.Duration * 16.67),
                    Metrics = new Dictionary<string, double>
                    {
                        { "TotalOptimizations", optimizationEvents.Count },
                        { "EffectiveOptimizations", effectiveOptimizations.Count },
                        { "AdaptationSpeed", adaptationSpeed }
                    }
                });
            }
            finally
            {
                await CleanupMonitoredSystem(engine, scheduler, evaluator, resourceManager);
            }
        }

        [Fact]
        public async Task PerformanceMonitoring_Predictive_Analysis()
        {
            _output.WriteLine("Testing performance monitoring predictive analysis");

            var (engine, scheduler, evaluator, resourceManager) = await InitializeMonitoredSystem();
            
            try
            {
                var predictionEvents = new List<PredictionEvent>();
                var historicalMetrics = new List<PerformanceSnapshot>();
                var predictionAccuracy = new List<double>();

                // Collect historical data for prediction
                var trainingFrames = 300; // 5 seconds of training data
                _output.WriteLine($"Collecting {trainingFrames} frames of training data...");

                for (int frame = 0; frame < trainingFrames; frame++)
                {
                    using var frameToken = engine.BeginFrame();

                    // Generate predictable workload pattern
                    var pattern = frame % 4;
                    var baseWorkload = 5.0;
                    var patternMultiplier = pattern switch
                    {
                        0 => 1.0,  // Normal
                        1 => 1.5,  // Increased load
                        2 => 0.8,  // Reduced load
                        3 => 2.0,  // Peak load
                        _ => 1.0
                    };

                    await SubmitPredictableWorkloadAsync(engine, baseWorkload * patternMultiplier);

                    scheduler.ProcessFrame();
                    await engine.EndFrameAsync(frameToken);

                    // Collect metrics for historical analysis
                    if (frame % 10 == 0)
                    {
                        var snapshot = CollectPerformanceSnapshot(engine, scheduler);
                        historicalMetrics.Add(snapshot);
                    }

                    await Task.Delay(Math.Max(0, (int)(16.67 - baseWorkload * patternMultiplier)));
                }

                // Act - Test predictive analysis
                var testFrames = 120; // 2 seconds of prediction testing
                _output.WriteLine($"Testing predictions for {testFrames} frames...");

                for (int frame = 0; frame < testFrames; frame++)
                {
                    using var frameToken = engine.BeginFrame();

                    // Predict next frame characteristics
                    var prediction = PredictFrameCharacteristics(historicalMetrics, frame);
                    var actualPattern = frame % 4;
                    var expectedWorkload = GetExpectedWorkload(actualPattern);

                    // Submit workload
                    await SubmitPredictableWorkloadAsync(engine, expectedWorkload);

                    scheduler.ProcessFrame();
                    await engine.EndFrameAsync(frameToken);

                    // Validate prediction accuracy
                    var actualMetrics = CollectPerformanceSnapshot(engine, scheduler);
                    var accuracy = CalculatePredictionAccuracy(prediction, actualMetrics);
                    predictionAccuracy.Add(accuracy);

                    // Record prediction event
                    predictionEvents.Add(new PredictionEvent
                    {
                        Timestamp = DateTime.UtcNow,
                        PredictedFrameTime = prediction.ExpectedFrameTime,
                        ActualFrameTime = actualMetrics.AverageFrameTime,
                        PredictedCpuUsage = prediction.ExpectedCpuUsage,
                        ActualCpuUsage = actualMetrics.CpuUsage,
                        Accuracy = accuracy
                    });

                    await Task.Delay(Math.Max(0, (int)(16.67 - expectedWorkload)));
                }

                // Assert - Validate predictive analysis
                var avgPredictionAccuracy = predictionAccuracy.Average();
                var highAccuracyPredictions = predictionAccuracy.Count(a => a > 0.8);
                var predictionConsistency = CalculatePredictionConsistency(predictionAccuracy);

                avgPredictionAccuracy.Should().BeGreaterThan(0.7, "Predictions should be reasonably accurate");
                highAccuracyPredictions.Should().BeGreaterThan(testFrames * 0.6, "Most predictions should be highly accurate");
                predictionConsistency.Should().BeGreaterThan(0.8, "Predictions should be consistent");

                _output.WriteLine($"Predictive Analysis Results:");
                _output.WriteLine($"  Average Prediction Accuracy: {avgPredictionAccuracy:P2}");
                _output.WriteLine($"  High Accuracy Predictions: {highAccuracyPredictions}/{testFrames}");
                _output.WriteLine($"  Prediction Consistency: {predictionConsistency:P2}");
                _output.WriteLine($"  Historical Data Points: {historicalMetrics.Count}");

                _testResults.Add(new MonitoringTestResult
                {
                    TestName = "PredictiveAnalysis",
                    Passed = avgPredictionAccuracy > 0.7 && predictionConsistency > 0.8,
                    DurationMs = (trainingFrames + testFrames) * 16.67,
                    Metrics = new Dictionary<string, double>
                    {
                        { "AveragePredictionAccuracy", avgPredictionAccuracy },
                        { "HighAccuracyPredictions", highAccuracyPredictions },
                        { "PredictionConsistency", predictionConsistency },
                        { "HistoricalDataPoints", historicalMetrics.Count }
                    }
                });
            }
            finally
            {
                await CleanupMonitoredSystem(engine, scheduler, evaluator, resourceManager);
            }
        }

        // Helper methods

        private async Task<(DirectX12RenderingEngine, AudioVisualQueueScheduler, IncrementalNodeEvaluator, DirectXResourceManager)> 
            InitializeMonitoredSystem()
        {
            var device = CreateMockDirectXDevice();
            var commandQueue = CreateMockCommandQueue();
            var performanceMonitor = new PerformanceMonitor();
            
            var engine = new DirectX12RenderingEngine(device, commandQueue, performanceMonitor);
            var scheduler = new AudioVisualQueueScheduler(targetFrameRate: 60, maxQueueDepth: 1000, batchSize: 64);
            var evaluator = new IncrementalNodeEvaluator(performanceMonitor);
            var resourceManager = new DirectXResourceManager(device, performanceMonitor);

            await engine.InitializeAsync();
            
            return (engine, scheduler, evaluator, resourceManager);
        }

        private async Task CleanupMonitoredSystem(
            DirectX12RenderingEngine engine, 
            AudioVisualQueueScheduler scheduler, 
            IncrementalNodeEvaluator evaluator, 
            DirectXResourceManager resourceManager)
        {
            engine?.Dispose();
            scheduler?.Dispose();
            evaluator?.Dispose();
            resourceManager?.Dispose();
            
            await Task.CompletedTask;
        }

        private void SubscribeToAllPerformanceEvents(
            DirectX12RenderingEngine engine,
            AudioVisualQueueScheduler scheduler,
            IncrementalNodeEvaluator evaluator,
            DirectXResourceManager resourceManager,
            PerformanceMetricsCollector collector)
        {
            engine.EngineAlert += (s, e) => collector.RecordAlert(new PerformanceAlert
            {
                Timestamp = DateTime.UtcNow,
                Source = "Engine",
                Type = e.AlertType.ToString(),
                Message = e.Message,
                Severity = MapAlertSeverity(e.AlertType)
            });

            scheduler.PerformanceMetrics += (s, e) => collector.RecordSchedulerMetrics(e.Stats);
            scheduler.SyncEvent += (s, e) => collector.RecordSyncEvent(e);
            scheduler.QueueStatusChanged += (s, e) => collector.RecordQueueStatus(e);
        }

        private void CollectRealTimeMetrics(
            DirectX12RenderingEngine engine,
            AudioVisualQueueScheduler scheduler,
            IncrementalNodeEvaluator evaluator,
            DirectXResourceManager resourceManager)
        {
            var frameMetrics = new FrameMetrics
            {
                Timestamp = DateTime.UtcNow,
                Fps = engine.Statistics.Performance?.AverageFps ?? 0,
                FrameTime = engine.Statistics.FramePacing.CurrentFrameTime,
                CpuUsage = GetCpuUsage(),
                MemoryUsage = GC.GetTotalMemory(false) / (1024 * 1024),
                GpuFrameTime = engine.Statistics.FramePacing.GpuFrameTime
            };

            _metricsCollector.RecordFrameMetrics(frameMetrics);
        }

        private async Task SubmitWorkloadGpuWorkAsync(DirectX12RenderingEngine engine, int frame)
        {
            var workload = 3.0 + (frame % 5) * 1.5; // Variable GPU workload
            await engine.SubmitGpuWorkAsync($"Workload_{frame}",
                async () => await SimulateGpuWorkAsync(workload),
                GpuTimingType.General);
        }

        private async Task SubmitAdaptiveWorkloadAsync(
            DirectX12RenderingEngine engine,
            AudioVisualQueueScheduler scheduler,
            IncrementalNodeEvaluator evaluator,
            DirectXResourceManager resourceManager,
            AdaptiveWorkload workload)
        {
            await engine.SubmitGpuWorkAsync("AdaptiveWorkload",
                async () => await SimulateGpuWorkAsync(workload.GpuWorkload),
                GpuTimingType.General);

            for (int i = 0; i < workload.AudioEventCount; i++)
            {
                scheduler.QueueAudioEvent(CreateAdaptiveAudioEvent(workload.LoadFactor));
            }

            for (int i = 0; i < workload.VisualUpdateCount; i++)
            {
                scheduler.QueueVisualUpdate(CreateAdaptiveVisualUpdate(workload.LoadFactor));
            }
        }

        private async Task SubmitPredictableWorkloadAsync(DirectX12RenderingEngine engine, double workload)
        {
            await engine.SubmitGpuWorkAsync("PredictableWorkload",
                async () => await SimulateGpuWorkAsync(workload),
                GpuTimingType.General);
        }

        private static async Task SimulateGpuWorkAsync(double durationMs)
        {
            await Task.Delay((int)(durationMs / 10.0)); // Scale for testing
        }

        private static async Task SimulateHeavyGpuWorkAsync(double durationMs)
        {
            // Simulate heavy GPU work that may cause performance issues
            await Task.Delay((int)(durationMs / 5.0)); // Scaled for testing
        }

        // Data generation methods
        private List<AudioEvent> GenerateWorkloadAudioEvents(int frame)
        {
            var events = new List<AudioEvent>();
            var random = new Random(frame);
            var eventCount = random.Next(5, 15);

            for (int i = 0; i < eventCount; i++)
            {
                events.Add(new AudioEvent
                {
                    Timestamp = DateTime.UtcNow,
                    Intensity = (float)random.NextDouble(),
                    Frequency = 440.0f + (float)random.NextDouble() * 1000,
                    Priority = (AudioEventPriority)(random.Next(4)),
                    Type = AudioEventType.Beat,
                    Data = new { Frame = frame, EventId = i }
                });
            }

            return events;
        }

        private List<VisualParameterUpdate> GenerateWorkloadVisualUpdates(int frame)
        {
            var updates = new List<VisualParameterUpdate>();
            var random = new Random(frame * 7);
            var updateCount = random.Next(3, 10);

            for (int i = 0; i < updateCount; i++)
            {
                updates.Add(new VisualParameterUpdate
                {
                    ParameterName = $"Param_{i}",
                    Value = (float)random.NextDouble(),
                    Timestamp = DateTime.UtcNow,
                    Priority = (AudioEventPriority)(random.Next(4))
                });
            }

            return updates;
        }

        private List<ResourceOperationData> GenerateWorkloadResourceOperations(int frame)
        {
            var operations = new List<ResourceOperationData>();
            var random = new Random(frame * 13);
            var opCount = random.Next(1, 5);

            for (int i = 0; i < opCount; i++)
            {
                operations.Add(new ResourceOperationData
                {
                    ResourceId = $"Resource_{frame}_{i}",
                    Type = (ResourceType)(random.Next(4)),
                    Priority = (ResourcePriority)(random.Next(3)),
                    Data = new byte[random.Next(100, 1000)]
                });
            }

            return operations;
        }

        private AudioEvent CreateHighIntensityAudioEvent()
        {
            return new AudioEvent
            {
                Timestamp = DateTime.UtcNow,
                Intensity = 0.9f,
                Frequency = 880.0f,
                Priority = AudioEventPriority.High,
                Type = AudioEventType.Beat,
                Data = new { HighIntensity = true }
            };
        }

        private AdaptiveWorkload GenerateAdaptiveWorkload(double loadFactor, int frame)
        {
            return new AdaptiveWorkload
            {
                LoadFactor = loadFactor,
                GpuWorkload = 3.0 * loadFactor,
                AudioEventCount = (int)(5 * loadFactor),
                VisualUpdateCount = (int)(3 * loadFactor),
                ExpectedFrameTime = 16.67 * loadFactor
            };
        }

        private AudioEvent CreateAdaptiveAudioEvent(double loadFactor)
        {
            return new AudioEvent
            {
                Timestamp = DateTime.UtcNow,
                Intensity = (float)(0.5 * loadFactor),
                Frequency = 440.0f + (float)(loadFactor * 200),
                Priority = AudioEventPriority.Normal,
                Type = AudioEventType.Beat,
                Data = new { LoadFactor = loadFactor }
            };
        }

        private VisualParameterUpdate CreateAdaptiveVisualUpdate(double loadFactor)
        {
            return new VisualParameterUpdate
            {
                ParameterName = "AdaptiveParam",
                Value = (float)loadFactor,
                Timestamp = DateTime.UtcNow,
                Priority = AudioEventPriority.Normal
            };
        }

        private static double GetExpectedWorkload(int pattern)
        {
            return pattern switch
            {
                0 => 5.0,   // Normal
                1 => 7.5,   // Increased load
                2 => 4.0,   // Reduced load
                3 => 10.0,  // Peak load
                _ => 5.0
            };
        }

        // Analysis and validation methods
        private void ValidateComprehensiveMetrics(CollectedMetrics metrics, int expectedFrameCount)
        {
            metrics.FrameMetrics.Should().NotBeEmpty("Frame metrics should be collected");
            metrics.ComponentMetrics.Should().HaveCountGreaterOrEqualTo(4, "All components should report metrics");
            
            var frameCount = metrics.FrameMetrics.Count;
            frameCount.Should().BeGreaterThan(expectedFrameCount * 0.8, "Should collect most frame metrics");
        }

        private double CalculateSystemHealthScore(CollectedMetrics metrics)
        {
            if (!metrics.FrameMetrics.Any()) return 0.0;
            
            var avgFps = metrics.FrameMetrics.Average(m => m.Fps);
            var fpsScore = Math.Min(1.0, avgFps / 60.0);
            
            var avgFrameTime = metrics.FrameMetrics.Average(m => m.FrameTime);
            var frameTimeScore = Math.Max(0.0, 1.0 - (avgFrameTime - 16.67) / 16.67);
            
            var alertCount = metrics.PerformanceAlerts.Count;
            var alertScore = Math.Max(0.0, 1.0 - alertCount / 100.0);
            
            return (fpsScore + frameTimeScore + alertScore) / 3.0;
        }

        private string CalculateOverallPerformanceGrade(CollectedMetrics metrics)
        {
            var healthScore = CalculateSystemHealthScore(metrics);
            
            return healthScore switch
            {
                >= 0.9 => "A+",
                >= 0.8 => "A",
                >= 0.7 => "B+",
                >= 0.6 => "B",
                >= 0.5 => "C",
                >= 0.4 => "D",
                _ => "F"
            };
        }

        private Dictionary<string, double> CreateMonitoringMetricsDictionary(CollectedMetrics metrics)
        {
            return new Dictionary<string, double>
            {
                { "FrameMetricsCount", metrics.FrameMetrics.Count },
                { "ComponentMetricsCount", metrics.ComponentMetrics.Count },
                { "AlertCount", metrics.PerformanceAlerts.Count },
                { "SystemHealthScore", CalculateSystemHealthScore(metrics) },
                { "AverageFps", metrics.FrameMetrics.Any() ? metrics.FrameMetrics.Average(m => m.Fps) : 0 },
                { "AverageFrameTime", metrics.FrameMetrics.Any() ? metrics.FrameMetrics.Average(m => m.FrameTime) : 0 }
            };
        }

        private AlertSeverity MapAlertSeverity(EngineAlertType alertType)
        {
            return alertType switch
            {
                EngineAlertType.FrameBudgetExceeded or EngineAlertType.BudgetExceeded => AlertSeverity.Error,
                EngineAlertType.PerformanceWarning => AlertSeverity.Warning,
                _ => AlertSeverity.Info
            };
        }

        private double CalculateAlertResponseTime(List<PerformanceAlert> alerts)
        {
            if (alerts.Count < 2) return 0.0;
            
            var sortedAlerts = alerts.OrderBy(a => a.Timestamp).ToList();
            var intervals = new List<double>();
            
            for (int i = 1; i < sortedAlerts.Count; i++)
            {
                var interval = (sortedAlerts[i].Timestamp - sortedAlerts[i - 1].Timestamp).TotalMilliseconds;
                intervals.Add(interval);
            }
            
            return intervals.Average();
        }

        private OptimizationEvent DetectOptimizationOpportunity(AudioVisualSchedulerStats stats)
        {
            if (stats.AverageLatencyMs > 30.0) // High latency threshold
            {
                return new OptimizationEvent
                {
                    Timestamp = DateTime.UtcNow,
                    Type = "LatencyOptimization",
                    EstimatedImprovement = 0.2,
                    Description = "High latency detected, consider reducing batch size"
                };
            }
            
            if (stats.QueueDepth > 500) // High queue depth
            {
                return new OptimizationEvent
                {
                    Timestamp = DateTime.UtcNow,
                    Type = "QueueOptimization",
                    EstimatedImprovement = 0.15,
                    Description = "High queue depth detected, consider increasing processing rate"
                };
            }
            
            return null;
        }

        private double CalculateAdaptationSpeed(List<AdaptationMetric> metrics)
        {
            if (metrics.Count < 10) return 0.0;
            
            // Calculate how quickly the system adapts to changes
            var variations = new List<double>();
            
            for (int i = 10; i < metrics.Count; i++)
            {
                var current = metrics[i];
                var previous = metrics[i - 10];
                
                var variation = Math.Abs(current.QueueDepth - previous.QueueDepth) / Math.Max(previous.QueueDepth, 1.0);
                variations.Add(variation);
            }
            
            return 1.0 - (variations.Average() / 2.0); // Score out of 1.0
        }

        private FrameCharacteristics PredictFrameCharacteristics(List<PerformanceSnapshot> historicalMetrics, int frame)
        {
            var recentMetrics = historicalMetrics.TakeLast(50).ToList(); // Last 50 samples
            var avgFrameTime = recentMetrics.Any() ? recentMetrics.Average(m => m.AverageFrameTime) : 16.67;
            var avgCpuUsage = recentMetrics.Any() ? recentMetrics.Average(m => m.CpuUsage) : 50.0;
            
            return new FrameCharacteristics
            {
                ExpectedFrameTime = avgFrameTime,
                ExpectedCpuUsage = avgCpuUsage,
                Confidence = recentMetrics.Count / 50.0
            };
        }

        private PerformanceSnapshot CollectPerformanceSnapshot(DirectX12RenderingEngine engine, AudioVisualQueueScheduler scheduler)
        {
            return new PerformanceSnapshot
            {
                Timestamp = DateTime.UtcNow,
                AverageFrameTime = engine.Statistics.FramePacing.CurrentFrameTime,
                CpuUsage = GetCpuUsage(),
                MemoryUsage = GC.GetTotalMemory(false) / (1024 * 1024),
                QueueDepth = scheduler.PendingAudioEvents + scheduler.PendingVisualUpdates
            };
        }

        private double CalculatePredictionAccuracy(FrameCharacteristics prediction, PerformanceSnapshot actual)
        {
            var frameTimeError = Math.Abs(prediction.ExpectedFrameTime - actual.AverageFrameTime) / 16.67;
            var cpuUsageError = Math.Abs(prediction.ExpectedCpuUsage - actual.CpuUsage) / 100.0;
            
            var accuracy = 1.0 - (frameTimeError + cpuUsageError) / 2.0;
            return Math.Max(0.0, Math.Min(1.0, accuracy));
        }

        private double CalculatePredictionConsistency(List<double> accuracies)
        {
            if (accuracies.Count < 2) return 1.0;
            
            var variance = CalculateVariance(accuracies);
            var consistency = 1.0 / (1.0 + variance);
            
            return Math.Max(0.0, Math.Min(1.0, consistency));
        }

        private static double CalculateVariance(List<double> values)
        {
            if (values.Count < 2) return 0.0;
            var mean = values.Average();
            return values.Sum(x => (x - mean) * (x - mean)) / values.Count;
        }

        private static double GetCpuUsage()
        {
            // Simplified CPU usage calculation for testing
            return 50.0 + (new Random().NextDouble() * 30.0);
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

        // Data classes
        private class MonitoringTestResult
        {
            public string TestName { get; set; }
            public bool Passed { get; set; }
            public double DurationMs { get; set; }
            public Dictionary<string, double> Metrics { get; set; } = new();
        }

        private class PerformanceAlert
        {
            public DateTime Timestamp { get; set; }
            public string Source { get; set; }
            public string Type { get; set; }
            public string Message { get; set; }
            public AlertSeverity Severity { get; set; }
        }

        private enum AlertSeverity
        {
            Info,
            Warning,
            Error
        }

        private class OptimizationEvent
        {
            public DateTime Timestamp { get; set; }
            public string Type { get; set; }
            public double EstimatedImprovement { get; set; }
            public string Description { get; set; }
        }

        private class AdaptationMetric
        {
            public DateTime Timestamp { get; set; }
            public int QueueDepth { get; set; }
            public double AverageLatencyMs { get; set; }
            public double FrameRate { get; set; }
            public int AdaptiveBatchSize { get; set; }
        }

        private class AdaptiveWorkload
        {
            public double LoadFactor { get; set; }
            public double GpuWorkload { get; set; }
            public int AudioEventCount { get; set; }
            public int VisualUpdateCount { get; set; }
            public double ExpectedFrameTime { get; set; }
        }

        private class FrameCharacteristics
        {
            public double ExpectedFrameTime { get; set; }
            public double ExpectedCpuUsage { get; set; }
            public double Confidence { get; set; }
        }

        private class PerformanceSnapshot
        {
            public DateTime Timestamp { get; set; }
            public double AverageFrameTime { get; set; }
            public double CpuUsage { get; set; }
            public long MemoryUsage { get; set; }
            public int QueueDepth { get; set; }
        }

        private class PredictionEvent
        {
            public DateTime Timestamp { get; set; }
            public double PredictedFrameTime { get; set; }
            public double ActualFrameTime { get; set; }
            public double PredictedCpuUsage { get; set; }
            public double ActualCpuUsage { get; set; }
            public double Accuracy { get; set; }
        }

        private class ThresholdViolation
        {
            public string MetricName { get; set; }
            public double Threshold { get; set; }
            public double ActualValue { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
}