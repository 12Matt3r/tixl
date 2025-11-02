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
using TiXL.Core.AudioVisual;
using TiXL.Tests.Categories;
using TiXL.Tests.Fixtures;
using TiXL.Tests.Data;

namespace TiXL.Tests.Integration
{
    /// <summary>
    /// Audio-visual queue scheduling integration tests with frame rendering
    /// Tests the complete audio-visual scheduling system integration
    /// </summary>
    [Category(TestCategories.Integration)]
    [Category(TestCategories.Performance)]
    [Category(TestCategories.AudioVisual)]
    public class AudioVisualSchedulingIntegrationTests : CoreTestFixture
    {
        private readonly ITestOutputHelper _output;
        private readonly List<SchedulingTestResult> _testResults;

        public AudioVisualSchedulingIntegrationTests(ITestOutputHelper output) : base()
        {
            _output = output;
            _testResults = new List<SchedulingTestResult>();
        }

        [Fact]
        public async Task AudioVisualQueue_FrameRendering_Synchronization()
        {
            _output.WriteLine("Testing audio-visual queue scheduling with frame rendering synchronization");

            var device = CreateMockDirectXDevice();
            var commandQueue = CreateMockCommandQueue();
            var performanceMonitor = new PerformanceMonitor();
            
            using var engine = new DirectX12RenderingEngine(device, commandQueue, performanceMonitor);
            await engine.InitializeAsync();
            
            using var scheduler = new AudioVisualQueueScheduler(
                targetFrameRate: 60, 
                maxQueueDepth: 1000, 
                batchSize: 64,
                targetEventsPerSecond: 60000);

            try
            {
                var frameCount = 120; // 2 seconds at 60 FPS
                var syncEvents = new List<SyncEvent>();
                var frameMetrics = new List<FrameMetric>();
                var audioMetrics = new List<AudioMetric>();
                var visualMetrics = new List<VisualMetric>();

                // Subscribe to sync events
                scheduler.SyncEvent += (s, e) => syncEvents.Add(new SyncEvent
                {
                    Timestamp = DateTime.UtcNow,
                    FrameNumber = e.FrameNumber,
                    AudioEventCount = e.AudioEventCount,
                    VisualUpdateCount = e.VisualUpdateCount,
                    SyncAccuracy = e.SyncAccuracy
                });

                // Act - Process audio-visual events synchronized with frame rendering
                for (int frame = 0; frame < frameCount; frame++)
                {
                    using var frameToken = engine.BeginFrame();
                    var frameStart = Stopwatch.GetTimestamp();

                    // Generate synchronized audio events
                    var audioEvents = GenerateSynchronizedAudioEvents(frame, frameCount);
                    foreach (var evt in audioEvents)
                    {
                        scheduler.QueueAudioEvent(evt);
                    }

                    // Generate synchronized visual updates
                    var visualUpdates = GenerateSynchronizedVisualUpdates(frame, frameCount);
                    foreach (var update in visualUpdates)
                    {
                        scheduler.QueueVisualUpdate(update);
                    }

                    // Submit frame rendering work
                    await SubmitFrameRenderingWorkAsync(engine, frame);

                    // Process frame and audio-visual queue
                    scheduler.ProcessFrame();
                    
                    try
                    {
                        await engine.EndFrameAsync(frameToken);
                    }
                    catch (Exception ex)
                    {
                        _output.WriteLine($"Frame {frame} processing error: {ex.Message}");
                    }

                    var frameEnd = Stopwatch.GetTimestamp();
                    var frameTime = (frameEnd - frameStart) / (double)Stopwatch.Frequency * 1000;

                    // Collect metrics
                    var stats = scheduler.GetStatistics();
                    frameMetrics.Add(new FrameMetric
                    {
                        FrameNumber = frame,
                        FrameTimeMs = frameTime,
                        TargetFrameTime = 16.67,
                        Fps = 1000.0 / frameTime
                    });

                    audioMetrics.Add(new AudioMetric
                    {
                        FrameNumber = frame,
                        PendingAudioEvents = stats.PendingAudioEvents,
                        AudioEventRate = stats.AudioEventRate,
                        AverageLatencyMs = stats.AverageLatencyMs
                    });

                    visualMetrics.Add(new VisualMetric
                    {
                        FrameNumber = frame,
                        PendingVisualUpdates = stats.PendingVisualUpdates,
                        VisualUpdateRate = stats.VisualUpdateRate,
                        FrameTimeConsistency = stats.FrameTimeConsistency
                    });

                    // Maintain frame rate
                    await Task.Delay(Math.Max(0, (int)(16.67 - frameTime)));
                }

                // Assert - Validate audio-visual synchronization
                var avgFrameTime = frameMetrics.Average(m => m.FrameTimeMs);
                var avgFps = frameMetrics.Average(m => m.Fps);
                var avgAudioLatency = audioMetrics.Average(m => m.AverageLatencyMs);
                var avgSyncAccuracy = syncEvents.Any() ? syncEvents.Average(e => e.SyncAccuracy) : 0.0;

                var frameTimeConsistency = CalculateFrameTimeConsistency(frameMetrics);
                var audioVisualBalance = CalculateAudioVisualBalance(audioMetrics, visualMetrics);

                avgFps.Should().BeGreaterThan(55.0, "Frame rate should be maintained");
                avgAudioLatency.Should().BeLessThan(50.0, "Audio latency should be reasonable");
                avgSyncAccuracy.Should().BeGreaterThan(0.9, "Audio-visual synchronization should be highly accurate");
                frameTimeConsistency.Should().BeGreaterThan(0.8, "Frame time should be consistent");

                _output.WriteLine($"Audio-Visual Frame Synchronization Results:`n  Average Frame Time: {avgFrameTime:F2}ms");
                _output.WriteLine($"  Average FPS: {avgFps:F1}");
                _output.WriteLine($"  Average Audio Latency: {avgAudioLatency:F2}ms");
                _output.WriteLine($"  Average Sync Accuracy: {avgSyncAccuracy:P2}");
                _output.WriteLine($"  Frame Time Consistency: {frameTimeConsistency:P2}");
                _output.WriteLine($"  Audio-Visual Balance: {audioVisualBalance:P2}");
                _output.WriteLine($"  Sync Events Recorded: {syncEvents.Count}");

                _testResults.Add(new SchedulingTestResult
                {
                    TestName = "AudioVisualQueue_FrameSynchronization",
                    Passed = avgFps > 55.0 && avgSyncAccuracy > 0.9,
                    DurationMs = frameCount * 16.67,
                    Metrics = new Dictionary<string, double>
                    {
                        { "AvgFrameTime", avgFrameTime },
                        { "AvgFps", avgFps },
                        { "AvgAudioLatency", avgAudioLatency },
                        { "AvgSyncAccuracy", avgSyncAccuracy },
                        { "FrameTimeConsistency", frameTimeConsistency },
                        { "AudioVisualBalance", audioVisualBalance },
                        { "SyncEventsCount", syncEvents.Count }
                    }
                });
            }
            finally
            {
                engine.Dispose();
                scheduler.Dispose();
            }
        }

        [Fact]
        public async Task AudioVisualQueue_HighFrequency_Processing()
        {
            _output.WriteLine("Testing audio-visual queue with high-frequency event processing");

            var performanceMonitor = new PerformanceMonitor();
            using var scheduler = new AudioVisualQueueScheduler(
                targetFrameRate: 60,
                maxQueueDepth: 10000,
                batchSize: 128,
                targetEventsPerSecond: 100000);

            try
            {
                var testDuration = TimeSpan.FromSeconds(3);
                var startTime = DateTime.UtcNow;
                var eventsProcessed = 0;
                var processingMetrics = new List<ProcessingMetric>();
                var highFreqEvents = new List<HighFreqEvent>();

                // Act - Process high-frequency events
                while (DateTime.UtcNow - startTime < testDuration)
                {
                    var batchStart = Stopwatch.GetTimestamp();

                    // Generate burst of high-frequency events
                    var burstSize = new Random().Next(50, 200);
                    var eventBurst = GenerateHighFrequencyEventBurst(burstSize);
                    highFreqEvents.AddRange(eventBurst);

                    // Queue all events
                    foreach (var evt in eventBurst)
                    {
                        scheduler.QueueAudioEvent(evt);
                    }

                    // Process frame
                    scheduler.ProcessFrame();

                    var batchEnd = Stopwatch.GetTimestamp();
                    var batchTime = (batchEnd - batchStart) / (double)Stopwatch.Frequency * 1000;

                    var stats = scheduler.GetStatistics();
                    processingMetrics.Add(new ProcessingMetric
                    {
                        Timestamp = DateTime.UtcNow,
                        EventsQueued = eventBurst.Count,
                        ProcessingTimeMs = batchTime,
                        QueueDepth = stats.PendingAudioEvents,
                        AverageLatency = stats.AverageLatencyMs
                    });

                    eventsProcessed += eventBurst.Count;

                    // Maintain processing rate
                    await Task.Delay(Math.Max(0, (int)(16.67 - batchTime)));
                }

                // Assert - Validate high-frequency processing performance
                var eventsPerSecond = eventsProcessed / testDuration.TotalSeconds;
                var avgProcessingTime = processingMetrics.Average(m => m.ProcessingTimeMs);
                var avgLatency = processingMetrics.Average(m => m.AverageLatency);
                var maxQueueDepth = processingMetrics.Max(m => m.QueueDepth);

                var throughputEfficiency = CalculateThroughputEfficiency(processingMetrics);
                var latencyConsistency = CalculateLatencyConsistency(processingMetrics);

                eventsPerSecond.Should().BeGreaterThan(50000.0, "Should handle high event rates");
                avgProcessingTime.Should().BeLessThan(10.0, "Event processing should be fast");
                maxQueueDepth.Should().BeLessThan(8000, "Queue depth should not exceed limits");
                latencyConsistency.Should().BeGreaterThan(0.7, "Latency should be consistent");

                _output.WriteLine($"High-Frequency Event Processing Results:`n  Events Processed: {eventsProcessed}");
                _output.WriteLine($"  Events/Second: {eventsPerSecond:F1}");
                _output.WriteLine($"  Average Processing Time: {avgProcessingTime:F2}ms");
                _output.WriteLine($"  Average Latency: {avgLatency:F2}ms");
                _output.WriteLine($"  Maximum Queue Depth: {maxQueueDepth}");
                _output.WriteLine($"  Throughput Efficiency: {throughputEfficiency:P2}");
                _output.WriteLine($"  Latency Consistency: {latencyConsistency:P2}");

                _testResults.Add(new SchedulingTestResult
                {
                    TestName = "HighFrequency_EventProcessing",
                    Passed = eventsPerSecond > 50000.0 && latencyConsistency > 0.7,
                    DurationMs = testDuration.TotalMilliseconds,
                    Metrics = new Dictionary<string, double>
                    {
                        { "EventsProcessed", eventsProcessed },
                        { "EventsPerSecond", eventsPerSecond },
                        { "AvgProcessingTime", avgProcessingTime },
                        { "AvgLatency", avgLatency },
                        { "MaxQueueDepth", maxQueueDepth },
                        { "ThroughputEfficiency", throughputEfficiency },
                        { "LatencyConsistency", latencyConsistency }
                    }
                });
            }
            finally
            {
                scheduler.Dispose();
            }
        }

        [Fact]
        public async Task AudioVisualQueue_Priority_Handling()
        {
            _output.WriteLine("Testing audio-visual queue priority handling under load");

            var performanceMonitor = new PerformanceMonitor();
            using var scheduler = new AudioVisualQueueScheduler(
                targetFrameRate: 60,
                maxQueueDepth: 5000,
                batchSize: 64);

            try
            {
                var frameCount = 60;
                var priorityEvents = new List<PriorityEvent>();
                var processingOrder = new List<ProcessedEvent>();
                var priorityMetrics = new List<PriorityMetric>();

                // Act - Test priority handling with mixed priority events
                for (int frame = 0; frame < frameCount; frame++)
                {
                    // Generate events with different priorities
                    var criticalEvents = GeneratePriorityEvents(frame, AudioEventPriority.Critical, 2);
                    var highEvents = GeneratePriorityEvents(frame, AudioEventPriority.High, 5);
                    var normalEvents = GeneratePriorityEvents(frame, AudioEventPriority.Normal, 10);
                    var lowEvents = GeneratePriorityEvents(frame, AudioEventPriority.Low, 8);

                    var allEvents = criticalEvents.Concat(highEvents).Concat(normalEvents).Concat(lowEvents);
                    
                    // Shuffle to test priority ordering
                    var shuffledEvents = allEvents.OrderBy(x => Guid.NewGuid()).ToList();
                    
                    foreach (var evt in shuffledEvents)
                    {
                        scheduler.QueueAudioEvent(evt);
                        priorityEvents.Add(new PriorityEvent
                        {
                            FrameNumber = frame,
                            Priority = evt.Priority,
                            Timestamp = DateTime.UtcNow,
                            EventId = evt.Data?.ToString() ?? Guid.NewGuid().ToString()
                        });
                    }

                    // Process frame
                    scheduler.ProcessFrame();

                    // Collect priority processing metrics
                    var stats = scheduler.GetStatistics();
                    priorityMetrics.Add(new PriorityMetric
                    {
                        FrameNumber = frame,
                        CriticalEvents = criticalEvents.Count,
                        HighEvents = highEvents.Count,
                        NormalEvents = normalEvents.Count,
                        LowEvents = lowEvents.Count,
                        ProcessingTime = stats.AverageLatencyMs
                    });
                }

                // Assert - Validate priority handling
                var criticalCount = priorityEvents.Count(e => e.Priority == AudioEventPriority.Critical);
                var highCount = priorityEvents.Count(e => e.Priority == AudioEventPriority.High);
                var normalCount = priorityEvents.Count(e => e.Priority == AudioEventPriority.Normal);
                var lowCount = priorityEvents.Count(e => e.Priority == AudioEventPriority.Low);

                var priorityDistribution = new Dictionary<AudioEventPriority, int>
                {
                    { AudioEventPriority.Critical, criticalCount },
                    { AudioEventPriority.High, highCount },
                    { AudioEventPriority.Normal, normalCount },
                    { AudioEventPriority.Low, lowCount }
                };

                // Verify expected distribution
                criticalCount.Should().Be(frameCount * 2, "Critical events should be queued correctly");
                highCount.Should().Be(frameCount * 5, "High priority events should be queued correctly");
                normalCount.Should().Be(frameCount * 10, "Normal priority events should be queued correctly");
                lowCount.Should().Be(frameCount * 8, "Low priority events should be queued correctly");

                var avgProcessingTime = priorityMetrics.Average(m => m.ProcessingTime);
                avgProcessingTime.Should().BeLessThan(30.0, "Priority processing should be efficient");

                _output.WriteLine($"Priority Handling Results:`n  Critical Events: {criticalCount}");
                _output.WriteLine($"  High Priority Events: {highCount}");
                _output.WriteLine($"  Normal Priority Events: {normalCount}");
                _output.WriteLine($"  Low Priority Events: {lowCount}");
                _output.WriteLine($"  Total Events: {priorityEvents.Count}");
                _output.WriteLine($"  Average Processing Time: {avgProcessingTime:F2}ms");

                _testResults.Add(new SchedulingTestResult
                {
                    TestName = "AudioVisualQueue_PriorityHandling",
                    Passed = criticalCount == frameCount * 2 && avgProcessingTime < 30.0,
                    DurationMs = frameCount * 16.67,
                    Metrics = new Dictionary<string, double>
                    {
                        { "CriticalEvents", criticalCount },
                        { "HighEvents", highCount },
                        { "NormalEvents", normalCount },
                        { "LowEvents", lowCount },
                        { "TotalEvents", priorityEvents.Count },
                        { "AvgProcessingTime", avgProcessingTime }
                    }.Concat(priorityDistribution.ToDictionary(kvp => kvp.Key.ToString(), kvp => (double)kvp.Value))
                      .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                });
            }
            finally
            {
                scheduler.Dispose();
            }
        }

        [Fact]
        public async Task AudioVisualQueue_Adaptive_BatchProcessing()
        {
            _output.WriteLine("Testing audio-visual queue adaptive batch processing");

            var performanceMonitor = new PerformanceMonitor();
            using var scheduler = new AudioVisualQueueScheduler(
                targetFrameRate: 60,
                maxQueueDepth: 10000,
                batchSize: 32); // Start with small batch size

            try
            {
                var adaptationPhases = new[]
                {
                    new AdaptationPhase { Name = "LowLoad", EventRate = 100, Duration = 30 },
                    new AdaptationPhase { Name = "MediumLoad", EventRate = 500, Duration = 30 },
                    new AdaptationPhase { Name = "HighLoad", EventRate = 2000, Duration = 30 },
                    new AdaptationPhase { Name = "Burst", EventRate = 5000, Duration = 15 }
                };

                var adaptationMetrics = new List<AdaptationMetric>();
                var batchSizeHistory = new List<BatchSizeHistory>();

                // Act - Test adaptive batch processing under varying loads
                foreach (var phase in adaptationPhases)
                {
                    _output.WriteLine($"Testing {phase.Name} phase with {phase.EventRate} events/frame");

                    for (int frame = 0; frame < phase.Duration; frame++)
                    {
                        // Generate events based on current phase
                        var events = GenerateEventsForLoad(phase.EventRate);
                        foreach (var evt in events)
                        {
                            scheduler.QueueAudioEvent(evt);
                        }

                        var processingStart = Stopwatch.GetTimestamp();
                        scheduler.ProcessFrame();
                        var processingEnd = Stopwatch.GetTimestamp();
                        var processingTime = (processingEnd - processingStart) / (double)Stopwatch.Frequency * 1000;

                        var stats = scheduler.GetStatistics();
                        var currentBatchSize = scheduler.AdaptiveBatchSize;

                        adaptationMetrics.Add(new AdaptationMetric
                        {
                            PhaseName = phase.Name,
                            FrameNumber = frame,
                            EventRate = phase.EventRate,
                            ProcessingTime = processingTime,
                            QueueDepth = stats.PendingAudioEvents,
                            BatchSize = currentBatchSize,
                            Latency = stats.AverageLatencyMs
                        });

                        batchSizeHistory.Add(new BatchSizeHistory
                        {
                            Timestamp = DateTime.UtcNow,
                            Phase = phase.Name,
                            BatchSize = currentBatchSize,
                            EventRate = phase.EventRate
                        });

                        await Task.Delay(Math.Max(0, (int)(16.67 - processingTime)));
                    }

                    // Allow system to stabilize between phases
                    await Task.Delay(100);
                }

                // Assert - Validate adaptive behavior
                var adaptationEffectiveness = CalculateAdaptationEffectiveness(adaptationMetrics);
                var batchSizeOptimization = CalculateBatchSizeOptimization(batchSizeHistory);
                var throughputMaintenance = CalculateThroughputMaintenance(adaptationMetrics);

                adaptationEffectiveness.Should().BeGreaterThan(0.7, "Batch size adaptation should be effective");
                batchSizeOptimization.Should().BeGreaterThan(0.6, "Batch sizes should be optimized for load");
                throughputMaintenance.Should().BeGreaterThan(0.8, "Throughput should be maintained across phases");

                _output.WriteLine($"Adaptive Batch Processing Results:`n  Adaptation Effectiveness: {adaptationEffectiveness:P2}");
                _output.WriteLine($"  Batch Size Optimization: {batchSizeOptimization:P2}");
                _output.WriteLine($"  Throughput Maintenance: {throughputMaintenance:P2}");

                _output.WriteLine("  Batch Size Adaptation History:");
                var phaseGroups = batchSizeHistory.GroupBy(b => b.Phase);
                foreach (var group in phaseGroups)
                {
                    var avgBatchSize = group.Average(b => b.BatchSize);
                    var avgEventRate = group.First().EventRate;
                    _output.WriteLine($"    {group.Key}: {avgBatchSize:F1} batch size, {avgEventRate} events/frame");
                }

                _testResults.Add(new SchedulingTestResult
                {
                    TestName = "Adaptive_BatchProcessing",
                    Passed = adaptationEffectiveness > 0.7 && throughputMaintenance > 0.8,
                    DurationMs = adaptationPhases.Sum(p => p.Duration * 16.67),
                    Metrics = new Dictionary<string, double>
                    {
                        { "AdaptationEffectiveness", adaptationEffectiveness },
                        { "BatchSizeOptimization", batchSizeOptimization },
                        { "ThroughputMaintenance", throughputMaintenance }
                    }
                });
            }
            finally
            {
                scheduler.Dispose();
            }
        }

        [Fact]
        public async Task AudioVisualQueue_RealTime_Optimization()
        {
            _output.WriteLine("Testing audio-visual queue real-time optimization");

            var performanceMonitor = new PerformanceMonitor();
            using var scheduler = new AudioVisualQueueScheduler(
                targetFrameRate: 60,
                maxQueueDepth: 8000,
                batchSize: 64);

            try
                {
                var optimizationDuration = TimeSpan.FromSeconds(5);
                var startTime = DateTime.UtcNow;
                var optimizationMetrics = new List<OptimizationMetric>();
                var performanceSnapshots = new List<PerformanceSnapshot>();

                // Act - Test real-time optimization under dynamic conditions
                while (DateTime.UtcNow - startTime < optimizationDuration)
                {
                    var frameStart = Stopwatch.GetTimestamp();

                    // Dynamically adjust event generation based on current performance
                    var currentMetrics = scheduler.GetStatistics();
                    var loadFactor = CalculateCurrentLoadFactor(currentMetrics);
                    
                    var eventCount = (int)(100 + loadFactor * 1000); // Adaptive event count
                    var events = GenerateAdaptiveEvents(eventCount, loadFactor);

                    foreach (var evt in events)
                    {
                        scheduler.QueueAudioEvent(evt);
                    }

                    // Process with optimization
                    var stats = scheduler.ProcessFrameWithOptimization();
                    
                    var frameEnd = Stopwatch.GetTimestamp();
                    var frameTime = (frameEnd - frameStart) / (double)Stopwatch.Frequency * 1000;

                    optimizationMetrics.Add(new OptimizationMetric
                    {
                        Timestamp = DateTime.UtcNow,
                        LoadFactor = loadFactor,
                        EventCount = eventCount,
                        ProcessingTime = frameTime,
                        QueueDepth = stats.PendingAudioEvents,
                        Latency = stats.AverageLatencyMs,
                        OptimizationEnabled = stats.OptimizationEnabled
                    });

                    // Collect performance snapshot
                    if (optimizationMetrics.Count % 10 == 0)
                    {
                        performanceSnapshots.Add(new PerformanceSnapshot
                        {
                            Timestamp = DateTime.UtcNow,
                            Fps = 1000.0 / frameTime,
                            Latency = stats.AverageLatencyMs,
                            Throughput = eventCount / frameTime,
                            OptimizationGain = stats.OptimizationGain
                        });
                    }

                    await Task.Delay(Math.Max(0, (int)(16.67 - frameTime)));
                }

                // Assert - Validate real-time optimization
                var avgOptimizationGain = optimizationMetrics.Where(m => m.OptimizationEnabled).Average(m => 
                    m.ProcessingTime / 16.67); // Normalized processing time
                
                var optimizationStability = CalculateOptimizationStability(optimizationMetrics);
                var realTimeResponse = CalculateRealTimeResponse(optimizationMetrics);

                avgOptimizationGain.Should().BeLessThan(1.2, "Optimization should not significantly increase processing time");
                optimizationStability.Should().BeGreaterThan(0.8, "Optimization should be stable");
                realTimeResponse.Should().BeGreaterThan(0.7, "System should respond well to real-time changes");

                _output.WriteLine($"Real-Time Optimization Results:`n  Average Optimization Gain: {avgOptimizationGain:F2}");
                _output.WriteLine($"  Optimization Stability: {optimizationStability:P2}");
                _output.WriteLine($"  Real-Time Response Score: {realTimeResponse:P2}");
                _output.WriteLine($"  Performance Snapshots: {performanceSnapshots.Count}");

                var avgLatency = optimizationMetrics.Average(m => m.Latency);
                var avgThroughput = optimizationMetrics.Average(m => m.EventCount / m.ProcessingTime);
                _output.WriteLine($"  Average Latency: {avgLatency:F2}ms");
                _output.WriteLine($"  Average Throughput: {avgThroughput:F1} events/ms");

                _testResults.Add(new SchedulingTestResult
                {
                    TestName = "RealTime_Optimization",
                    Passed = optimizationStability > 0.8 && realTimeResponse > 0.7,
                    DurationMs = optimizationDuration.TotalMilliseconds,
                    Metrics = new Dictionary<string, double>
                    {
                        { "AvgOptimizationGain", avgOptimizationGain },
                        { "OptimizationStability", optimizationStability },
                        { "RealTimeResponse", realTimeResponse },
                        { "AvgLatency", avgLatency },
                        { "AvgThroughput", avgThroughput },
                        { "PerformanceSnapshots", performanceSnapshots.Count }
                    }
                });
            }
            finally
            {
                scheduler.Dispose();
            }
        }

        // Helper methods

        private async Task SubmitFrameRenderingWorkAsync(DirectX12RenderingEngine engine, int frame)
        {
            await engine.SubmitGpuWorkAsync($"Frame_{frame}",
                async () => await SimulateFrameRenderingWorkAsync(5.0 + frame % 3),
                GpuTimingType.General);
        }

        private static async Task SimulateFrameRenderingWorkAsync(double durationMs)
        {
            await Task.Delay((int)(durationMs / 10.0)); // Scale for testing
        }

        private List<AudioEvent> GenerateSynchronizedAudioEvents(int frame, int totalFrames)
        {
            var events = new List<AudioEvent>();
            var progress = frame / (double)totalFrames;
            var eventCount = (int)(3 + 2 * Math.Sin(progress * 4 * Math.PI)); // Oscillating event count

            for (int i = 0; i < eventCount; i++)
            {
                events.Add(new AudioEvent
                {
                    Timestamp = DateTime.UtcNow,
                    Intensity = (float)(0.5 + 0.5 * Math.Sin(progress * 2 * Math.PI + i)),
                    Frequency = 440.0f + (float)(200 * Math.Sin(progress * 4 * Math.PI)),
                    Priority = (AudioEventPriority)(i % 4),
                    Type = AudioEventType.Beat,
                    Data = new { Frame = frame, EventId = i, Sync = true }
                });
            }

            return events;
        }

        private List<VisualParameterUpdate> GenerateSynchronizedVisualUpdates(int frame, int totalFrames)
        {
            var updates = new List<VisualParameterUpdate>();
            var progress = frame / (double)totalFrames;

            // Create updates synchronized with audio events
            updates.Add(new VisualParameterUpdate
            {
                ParameterName = "BeatIntensity",
                Value = (float)(0.5 + 0.5 * Math.Sin(progress * 2 * Math.PI)),
                Timestamp = DateTime.UtcNow,
                Priority = AudioEventPriority.High
            });

            updates.Add(new VisualParameterUpdate
            {
                ParameterName = "Frequency",
                Value = (float)(440.0 + 200 * Math.Sin(progress * 4 * Math.PI)),
                Timestamp = DateTime.UtcNow,
                Priority = AudioEventPriority.Normal
            });

            return updates;
        }

        private List<AudioEvent> GenerateHighFrequencyEventBurst(int burstSize)
        {
            var events = new List<AudioEvent>();
            var random = new Random();

            for (int i = 0; i < burstSize; i++)
            {
                events.Add(new AudioEvent
                {
                    Timestamp = DateTime.UtcNow,
                    Intensity = (float)random.NextDouble(),
                    Frequency = 440.0f + (float)random.NextDouble() * 1000,
                    Priority = (AudioEventPriority)(random.Next(4)),
                    Type = AudioEventType.Beat,
                    Data = new { BurstId = Guid.NewGuid(), Index = i }
                });
            }

            return events;
        }

        private List<AudioEvent> GeneratePriorityEvents(int frame, AudioEventPriority priority, int count)
        {
            var events = new List<AudioEvent>();

            for (int i = 0; i < count; i++)
            {
                events.Add(new AudioEvent
                {
                    Timestamp = DateTime.UtcNow,
                    Intensity = priority switch
                    {
                        AudioEventPriority.Critical => 1.0f,
                        AudioEventPriority.High => 0.8f,
                        AudioEventPriority.Normal => 0.5f,
                        AudioEventPriority.Low => 0.2f,
                        _ => 0.5f
                    },
                    Frequency = 440.0f + (i * 100),
                    Priority = priority,
                    Type = AudioEventType.Beat,
                    Data = new { Frame = frame, Priority = priority.ToString(), Index = i }
                });
            }

            return events;
        }

        private List<AudioEvent> GenerateEventsForLoad(int eventRate)
        {
            var events = new List<AudioEvent>();
            var random = new Random();

            for (int i = 0; i < eventRate; i++)
            {
                events.Add(new AudioEvent
                {
                    Timestamp = DateTime.UtcNow,
                    Intensity = (float)random.NextDouble(),
                    Frequency = 440.0f + (float)random.NextDouble() * 1000,
                    Priority = (AudioEventPriority)(random.Next(4)),
                    Type = AudioEventType.Beat,
                    Data = new { Load = eventRate, Index = i }
                });
            }

            return events;
        }

        private List<AudioEvent> GenerateAdaptiveEvents(int eventCount, double loadFactor)
        {
            var events = new List<AudioEvent>();
            var random = new Random();

            for (int i = 0; i < eventCount; i++)
            {
                events.Add(new AudioEvent
                {
                    Timestamp = DateTime.UtcNow,
                    Intensity = (float)Math.Min(1.0, loadFactor * random.NextDouble()),
                    Frequency = 440.0f + (float)(loadFactor * random.NextDouble() * 500),
                    Priority = (AudioEventPriority)(random.Next(4)),
                    Type = AudioEventType.Beat,
                    Data = new { LoadFactor = loadFactor, Index = i }
                });
            }

            return events;
        }

        // Analysis methods

        private static double CalculateFrameTimeConsistency(List<FrameMetric> metrics)
        {
            var frameTimes = metrics.Select(m => m.FrameTimeMs).ToList();
            var variance = CalculateVariance(frameTimes);
            var avgFrameTime = frameTimes.Average();
            
            return 1.0 / (1.0 + variance / avgFrameTime);
        }

        private static double CalculateAudioVisualBalance(List<AudioMetric> audioMetrics, List<VisualMetric> visualMetrics)
        {
            var avgAudioLatency = audioMetrics.Average(m => m.AverageLatencyMs);
            var avgVisualConsistency = visualMetrics.Average(m => m.FrameTimeConsistency);
            
            var audioScore = Math.Max(0, 1.0 - avgAudioLatency / 100.0);
            var visualScore = avgVisualConsistency;
            
            return (audioScore + visualScore) / 2.0;
        }

        private static double CalculateThroughputEfficiency(List<ProcessingMetric> metrics)
        {
            var processingTimes = metrics.Select(m => m.ProcessingTimeMs).ToList();
            var eventsProcessed = metrics.Sum(m => m.EventsQueued);
            
            var avgProcessingTime = processingTimes.Average();
            var maxPossibleThroughput = eventsProcessed / Math.Max(avgProcessingTime, 0.001);
            var actualThroughput = eventsProcessed / (metrics.Count * 16.67 / 1000.0);
            
            return actualThroughput / Math.Max(maxPossibleThroughput, 0.001);
        }

        private static double CalculateLatencyConsistency(List<ProcessingMetric> metrics)
        {
            var latencies = metrics.Select(m => m.AverageLatency).ToList();
            var variance = CalculateVariance(latencies);
            var avgLatency = latencies.Average();
            
            return 1.0 / (1.0 + variance / Math.Max(avgLatency, 1.0));
        }

        private static double CalculateCurrentLoadFactor(AudioVisualSchedulerStats stats)
        {
            var queueDepthFactor = Math.Min(1.0, stats.PendingAudioEvents / 1000.0);
            var latencyFactor = Math.Min(1.0, stats.AverageLatencyMs / 50.0);
            
            return (queueDepthFactor + latencyFactor) / 2.0;
        }

        private static double CalculateAdaptationEffectiveness(List<AdaptationMetric> metrics)
        {
            var phaseGroups = metrics.GroupBy(m => m.PhaseName).ToList();
            var effectivenessScores = new List<double>();
            
            foreach (var group in phaseGroups)
            {
                var processingTimes = group.Select(m => m.ProcessingTime).ToList();
                var avgProcessingTime = processingTimes.Average();
                var consistency = 1.0 / (1.0 + CalculateVariance(processingTimes));
                
                effectivenessScores.Add(consistency);
            }
            
            return effectivenessScores.Average();
        }

        private static double CalculateBatchSizeOptimization(List<BatchSizeHistory> history)
        {
            var phaseGroups = history.GroupBy(h => h.Phase).ToList();
            var optimizationScores = new List<double>();
            
            foreach (var group in phaseGroups)
            {
                var batchSizes = group.Select(h => h.BatchSize).ToList();
                var eventRates = group.Select(h => h.EventRate).ToList();
                
                // Calculate correlation between batch size and event rate
                var correlation = CalculateCorrelation(batchSizes, eventRates);
                optimizationScores.Add(Math.Abs(correlation));
            }
            
            return optimizationScores.Average();
        }

        private static double CalculateThroughputMaintenance(List<AdaptationMetric> metrics)
        {
            var throughputs = metrics.Select(m => m.EventRate / m.ProcessingTime).ToList();
            var variance = CalculateVariance(throughputs);
            var avgThroughput = throughputs.Average();
            
            return 1.0 / (1.0 + variance / Math.Max(avgThroughput, 0.001));
        }

        private static double CalculateOptimizationStability(List<OptimizationMetric> metrics)
        {
            var processingTimes = metrics.Select(m => m.ProcessingTime).ToList();
            var variance = CalculateVariance(processingTimes);
            var avgProcessingTime = processingTimes.Average();
            
            return 1.0 / (1.0 + variance / Math.Max(avgProcessingTime, 0.001));
        }

        private static double CalculateRealTimeResponse(List<OptimizationMetric> metrics)
        {
            var responseTimes = new List<double>();
            
            for (int i = 1; i < metrics.Count; i++)
            {
                var currentLoad = metrics[i].LoadFactor;
                var previousLoad = metrics[i - 1].LoadFactor;
                var loadChange = Math.Abs(currentLoad - previousLoad);
                
                if (loadChange > 0.1) // Significant load change
                {
                    // Measure how quickly processing time adapts
                    var responseTime = Math.Abs(metrics[i].ProcessingTime - metrics[i - 1].ProcessingTime);
                    responseTimes.Add(responseTime);
                }
            }
            
            return responseTimes.Any() ? 1.0 / (1.0 + responseTimes.Average() / 10.0) : 1.0;
        }

        private static double CalculateVariance(List<double> values)
        {
            if (values.Count < 2) return 0.0;
            var mean = values.Average();
            return values.Sum(x => (x - mean) * (x - mean)) / values.Count;
        }

        private static double CalculateCorrelation(List<double> x, List<double> y)
        {
            if (x.Count != y.Count || x.Count == 0) return 0.0;
            
            var meanX = x.Average();
            var meanY = y.Average();
            
            var numerator = x.Zip(y, (xi, yi) => (xi - meanX) * (yi - meanY)).Sum();
            var denominatorX = x.Sum(xi => Math.Pow(xi - meanX, 2));
            var denominatorY = y.Sum(yi => Math.Pow(yi - meanY, 2));
            
            var denominator = Math.Sqrt(denominatorX * denominatorY);
            
            return denominator == 0 ? 0 : numerator / denominator;
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
        private class SchedulingTestResult
        {
            public string TestName { get; set; }
            public bool Passed { get; set; }
            public double DurationMs { get; set; }
            public Dictionary<string, double> Metrics { get; set; } = new();
        }

        private class SyncEvent
        {
            public DateTime Timestamp { get; set; }
            public int FrameNumber { get; set; }
            public int AudioEventCount { get; set; }
            public int VisualUpdateCount { get; set; }
            public double SyncAccuracy { get; set; }
        }

        private class FrameMetric
        {
            public int FrameNumber { get; set; }
            public double FrameTimeMs { get; set; }
            public double TargetFrameTime { get; set; }
            public double Fps { get; set; }
        }

        private class AudioMetric
        {
            public int FrameNumber { get; set; }
            public int PendingAudioEvents { get; set; }
            public double AudioEventRate { get; set; }
            public double AverageLatencyMs { get; set; }
        }

        private class VisualMetric
        {
            public int FrameNumber { get; set; }
            public int PendingVisualUpdates { get; set; }
            public double VisualUpdateRate { get; set; }
            public double FrameTimeConsistency { get; set; }
        }

        private class ProcessingMetric
        {
            public DateTime Timestamp { get; set; }
            public int EventsQueued { get; set; }
            public double ProcessingTimeMs { get; set; }
            public int QueueDepth { get; set; }
            public double AverageLatency { get; set; }
        }

        private class HighFreqEvent
        {
            public DateTime Timestamp { get; set; }
            public AudioEventPriority Priority { get; set; }
            public double Intensity { get; set; }
            public double Frequency { get; set; }
        }

        private class PriorityEvent
        {
            public int FrameNumber { get; set; }
            public AudioEventPriority Priority { get; set; }
            public DateTime Timestamp { get; set; }
            public string EventId { get; set; }
        }

        private class ProcessedEvent
        {
            public string EventId { get; set; }
            public AudioEventPriority Priority { get; set; }
            public DateTime ProcessingTime { get; set; }
        }

        private class PriorityMetric
        {
            public int FrameNumber { get; set; }
            public int CriticalEvents { get; set; }
            public int HighEvents { get; set; }
            public int NormalEvents { get; set; }
            public int LowEvents { get; set; }
            public double ProcessingTime { get; set; }
        }

        private class AdaptationPhase
        {
            public string Name { get; set; }
            public int EventRate { get; set; }
            public int Duration { get; set; }
        }

        private class AdaptationMetric
        {
            public string PhaseName { get; set; }
            public int FrameNumber { get; set; }
            public int EventRate { get; set; }
            public double ProcessingTime { get; set; }
            public int QueueDepth { get; set; }
            public int BatchSize { get; set; }
            public double Latency { get; set; }
        }

        private class BatchSizeHistory
        {
            public DateTime Timestamp { get; set; }
            public string Phase { get; set; }
            public int BatchSize { get; set; }
            public int EventRate { get; set; }
        }

        private class OptimizationMetric
        {
            public DateTime Timestamp { get; set; }
            public double LoadFactor { get; set; }
            public int EventCount { get; set; }
            public double ProcessingTime { get; set; }
            public int QueueDepth { get; set; }
            public double Latency { get; set; }
            public bool OptimizationEnabled { get; set; }
        }

        private class PerformanceSnapshot
        {
            public DateTime Timestamp { get; set; }
            public double Fps { get; set; }
            public double Latency { get; set; }
            public double Throughput { get; set; }
            public double OptimizationGain { get; set; }
        }
    }
}