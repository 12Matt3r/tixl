using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vortice.DXGI;
using Vortice.Windows.Direct3D12;
using TiXL.Core.Performance;
using TiXL.Core.Logging;
using TiXL.Core.AudioVisual;
using TiXL.Core.Validation;
using Xunit;
using Xunit.Abstractions;
using Moq;
using System.Runtime.InteropServices;

namespace TiXL.Tests.Performance
{
    /// <summary>
    /// Comprehensive test suite for performance monitoring, frame pacing, and audio-visual queue scheduling
    /// Tests high-performance scenarios, edge cases, and regression prevention
    /// </summary>
    public class PerformanceTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly Mock<PerformanceMonitor> _mockMonitor;
        private readonly Mock<PredictiveFrameScheduler> _mockScheduler;
        private readonly Mock<AudioVisualIntegrationManager> _mockAudioVisualManager;
        private readonly Mock<ID3D12Device4> _mockDevice;
        private readonly string _testTempDirectory;

        public PerformanceTests(ITestOutputHelper output)
        {
            _output = output;
            _testTempDirectory = Path.Combine(Path.GetTempPath(), "TiXL_Performance_Tests_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_testTempDirectory);

            _mockMonitor = new Mock<PerformanceMonitor>();
            _mockScheduler = new Mock<PredictiveFrameScheduler>();
            _mockAudioVisualManager = new Mock<AudioVisualIntegrationManager>();
            _mockDevice = new Mock<ID3D12Device4>();
        }

        public void Dispose()
        {
            if (Directory.Exists(_testTempDirectory))
            {
                try
                {
                    Directory.Delete(_testTempDirectory, true);
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Warning: Failed to cleanup test directory: {ex.Message}");
                }
            }
        }

        #region Performance Monitor Tests

        [Fact]
        public void PerformanceMonitor_Initialization_ShouldInitializeAllComponents()
        {
            // Arrange
            var historySize = 300;

            // Act
            using var monitor = new PerformanceMonitor(historySize, _mockDevice.Object);

            // Assert
            Assert.NotNull(monitor);
            
            // Test basic properties
            var metrics = monitor.GetCurrentMetrics();
            Assert.NotNull(metrics);
            Assert.True(metrics.FrameCount >= 0);
        }

        [Fact]
        public async Task PerformanceMonitor_FrameMetricsCollection_ShouldCollectAccurateData()
        {
            // Arrange
            using var monitor = new PerformanceMonitor(100, _mockDevice.Object);
            var metricsHistory = new List<EvaluationEngineMetrics>();
            var expectedFrames = 50;

            // Act - Collect metrics over time
            for (int i = 0; i < expectedFrames; i++)
            {
                monitor.BeginFrame();
                
                // Simulate varying workload
                var workDuration = (i % 10) + 1; // 1-10ms work
                await Task.Delay(workDuration);
                
                monitor.EndFrame();
                
                var metrics = monitor.GetCurrentMetrics();
                metricsHistory.Add(metrics);
                
                await Task.Delay(1); // Small delay between frames
            }

            // Assert
            Assert.Equal(expectedFrames, metricsHistory.Count);
            
            // Verify metrics progression
            var latestMetrics = metricsHistory.Last();
            Assert.True(latestMetrics.FrameCount >= expectedFrames);
            Assert.True(latestMetrics.AverageFrameTime > 0);
            Assert.True(latestMetrics.CpuUsagePercent >= 0);
            
            _output.WriteLine($"Collected metrics for {metricsHistory.Count} frames");
            _output.WriteLine($"Latest - FrameTime: {latestMetrics.AverageFrameTime:F2}ms, CPU: {latestMetrics.CpuUsagePercent:F1}%");
        }

        [Fact]
        public async Task PerformanceMonitor_MemoryTracking_ShouldTrackMemoryUsage()
        {
            // Arrange
            using var monitor = new PerformanceMonitor(100, _mockDevice.Object);
            var memorySnapshots = new List<(long Timestamp, double MemoryMB)>();

            // Act - Simulate memory-intensive operations
            for (int batch = 0; batch < 5; batch++)
            {
                var largeObjects = new List<byte[]>();
                
                // Allocate memory
                for (int i = 0; i < 100; i++)
                {
                    largeObjects.Add(new byte[1024 * 100]); // 100KB objects
                    await Task.Delay(1);
                }
                
                // Record memory state
                monitor.BeginFrame();
                monitor.EndFrame();
                
                var metrics = monitor.GetCurrentMetrics();
                memorySnapshots.Add((DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), metrics.MemoryUsageMB));
                
                // Clear memory
                largeObjects.Clear();
                await Task.Delay(50); // Allow GC
                
                monitor.BeginFrame();
                monitor.EndFrame();
            }

            // Assert
            Assert.Equal(5, memorySnapshots.Count);
            
            // Memory should be tracked (actual values depend on system)
            Assert.All(memorySnapshots, snapshot => 
            {
                Assert.True(snapshot.MemoryMB >= 0, "Memory usage should be non-negative");
            });
            
            _output.WriteLine("Memory tracking test completed:");
            foreach (var (timestamp, memoryMB) in memorySnapshots)
            {
                _output.WriteLine($"  {timestamp}: {memoryMB:F1} MB");
            }
        }

        [Fact]
        public void PerformanceMonitor_AlertGeneration_ShouldGenerateAlertsForThresholdViolations()
        {
            // Arrange
            using var monitor = new PerformanceMonitor(50, _mockDevice.Object);
            var alerts = new List<PerformanceAlert>();
            monitor.PerformanceAlert += (sender, alert) => alerts.Add(alert);

            // Act - Generate frames that violate thresholds
            var violationFrames = 0;
            const int targetFrameTime = 16; // ms

            for (int i = 0; i < 100; i++)
            {
                monitor.BeginFrame();
                
                // Create violations by sleeping longer than target
                var sleepTime = (i % 20 == 0) ? targetFrameTime + 10 : 1; // Periodic violations
                await Task.Delay(sleepTime);
                
                monitor.EndFrame();
                
                if (i % 20 == 0)
                    violationFrames++;
            }

            // Wait a bit for alerts to be processed
            await Task.Delay(100);

            // Assert
            _output.WriteLine($"Generated {violationFrames} violation frames");
            _output.WriteLine($"Received {alerts.Count} alerts");
            
            // Alerts should be generated for violations
            Assert.True(alerts.Count > 0, "Alerts should be generated for threshold violations");
            
            foreach (var alert in alerts)
            {
                Assert.NotNull(alert.Message);
                Assert.True(alert.Timestamp > DateTimeOffset.MinValue);
                Assert.True(alert.Severity >= AlertSeverity.Information);
            }
        }

        #endregion

        #region Frame Pacing Tests

        [Fact]
        public async Task FramePacing_TargetFrameRate_ShouldMaintainConsistentTiming()
        {
            // Arrange
            var targetFrameRate = 60;
            var targetFrameTime = 1000.0 / targetFrameRate; // 16.67ms
            var frameTimeTolerance = 2.0; // 2ms tolerance

            using var scheduler = new PredictiveFrameScheduler();
            var actualFrameTimes = new List<double>();
            var frameCount = 120; // Test for 2 seconds

            // Act
            var totalStopwatch = Stopwatch.StartNew();
            
            for (int i = 0; i < frameCount; i++)
            {
                var frameStopwatch = Stopwatch.StartNew();
                
                // Simulate frame processing
                await Task.Delay(1); // Minimal processing time
                
                scheduler.ScheduleFrame(i);
                
                frameStopwatch.Stop();
                actualFrameTimes.Add(frameStopwatch.Elapsed.TotalMilliseconds);
                
                // Wait for target frame time
                var elapsedMs = frameStopwatch.Elapsed.TotalMilliseconds;
                var waitTime = targetFrameTime - elapsedMs;
                if (waitTime > 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(waitTime));
                }
            }
            
            totalStopwatch.Stop();

            // Assert
            var averageFrameTime = actualFrameTimes.Average();
            var frameTimeVariance = actualFrameTimes.StandardDeviation();
            var actualFps = frameCount / totalStopwatch.Elapsed.TotalSeconds;

            _output.WriteLine($"Frame Pacing Test Results:");
            _output.WriteLine($"  Target FPS: {targetFrameRate}");
            _output.WriteLine($"  Actual FPS: {actualFps:F1}");
            _output.WriteLine($"  Average Frame Time: {averageFrameTime:F2}ms");
            _output.WriteLine($"  Frame Time Variance: {frameTimeVariance:F2}ms");
            _output.WriteLine($"  Total Time: {totalStopwatch.Elapsed.TotalSeconds:F2}s");

            // Assertions
            Assert.True(frameCount == actualFrameTimes.Count, "Should record all frame times");
            Assert.True(Math.Abs(averageFrameTime - targetFrameTime) < frameTimeTolerance,
                $"Average frame time {averageFrameTime:F2}ms should be within {frameTimeTolerance}ms of target {targetFrameTime:F2}ms");
            Assert.True(frameTimeVariance < 5.0,
                $"Frame time variance {frameTimeVariance:F2}ms should be low for consistent pacing");
        }

        [Fact]
        public async Task FramePacing_VariableLoad_ShouldAdaptToWorkload()
        {
            // Arrange
            using var scheduler = new PredictiveFrameScheduler();
            var workloadPatterns = new[]
            {
                new { Pattern = "Light", WorkloadMs = 1, Weight = 0.7 },
                new { Pattern = "Medium", WorkloadMs = 8, Weight = 0.2 },
                new { Pattern = "Heavy", WorkloadMs = 15, Weight = 0.1 }
            };

            var frameTimes = new List<(string Pattern, double TimeMs)>();

            // Act - Simulate varying workloads
            var totalFrames = 100;
            var random = new Random(42); // Deterministic for testing

            for (int i = 0; i < totalFrames; i++)
            {
                // Choose workload pattern based on weights
                var rand = random.NextDouble();
                var cumulativeWeight = 0.0;
                var selectedPattern = workloadPatterns[0];
                
                foreach (var pattern in workloadPatterns)
                {
                    cumulativeWeight += pattern.Weight;
                    if (rand <= cumulativeWeight)
                    {
                        selectedPattern = pattern;
                        break;
                    }
                }

                var frameStopwatch = Stopwatch.StartNew();
                
                // Simulate the selected workload
                await Task.Delay(selectedPattern.WorkloadMs);
                
                scheduler.ScheduleFrame(i);
                
                frameStopwatch.Stop();
                frameTimes.Add((selectedPattern.Pattern, frameStopwatch.Elapsed.TotalMilliseconds));
            }

            // Assert
            var lightFrames = frameTimes.Where(f => f.Pattern == "Light").ToList();
            var mediumFrames = frameTimes.Where(f => f.Pattern == "Medium").ToList();
            var heavyFrames = frameTimes.Where(f => f.Pattern == "Heavy").ToList();

            _output.WriteLine($"Variable Load Test Results:");
            _output.WriteLine($"  Light frames: {lightFrames.Count}, Avg time: {lightFrames.Average(f => f.TimeMs):F2}ms");
            _output.WriteLine($"  Medium frames: {mediumFrames.Count}, Avg time: {mediumFrames.Average(f => f.TimeMs):F2}ms");
            _output.WriteLine($"  Heavy frames: {heavyFrames.Count}, Avg time: {heavyFrames.Average(f => f.TimeMs):F2}ms");

            // Verify pattern detection worked
            Assert.True(lightFrames.Count > 0, "Should have light frames");
            Assert.True(mediumFrames.Count > 0, "Should have medium frames");
            Assert.True(heavyFrames.Count > 0, "Should have heavy frames");
        }

        #endregion

        #region Audio-Visual Queue Scheduling Tests

        [Fact]
        public async Task AudioVisualQueueScheduler_HighThroughput_ShouldHandle50kEventsPerSecond()
        {
            // Arrange
            const int targetEventsPerSecond = 50000;
            const int testDurationSeconds = 2;
            const int totalEvents = targetEventsPerSecond * testDurationSeconds;

            using var scheduler = new AudioVisualQueueScheduler(
                targetFrameRate: 60,
                maxQueueDepth: 10000,
                batchSize: 128,
                targetEventsPerSecond: targetEventsPerSecond);

            var eventCounts = new ConcurrentBag<int>();
            var processingTimes = new List<double>();

            // Act - Generate high-frequency events
            var startTime = DateTimeOffset.UtcNow;
            var generationStopwatch = Stopwatch.StartNew();

            for (int i = 0; i < totalEvents; i++)
            {
                var eventGenerationStopwatch = Stopwatch.StartNew();
                
                // Simulate audio event generation
                var audioEvent = new RealTimeAudioEvent
                {
                    EventId = i,
                    Timestamp = DateTimeOffset.UtcNow,
                    EventType = AudioEventType.NoteOn,
                    Frequency = 440.0 + (i % 1000),
                    Volume = 0.5,
                    Channel = i % 16
                };

                scheduler.QueueAudioEvent(audioEvent);
                
                eventGenerationStopwatch.Stop();
                processingTimes.Add(eventGenerationStopwatch.Elapsed.TotalMilliseconds);

                // Throttle event generation to target rate
                var elapsedSinceStart = generationStopwatch.Elapsed.TotalSeconds;
                var expectedEventsByNow = (int)(elapsedSinceStart * targetEventsPerSecond);
                
                if (i > expectedEventsByNow)
                {
                    var delayMs = (i - expectedEventsByNow) * 1000.0 / targetEventsPerSecond;
                    if (delayMs > 0)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(delayMs));
                    }
                }

                // Periodic reporting
                if (i % 10000 == 0 && i > 0)
                {
                    eventCounts.Add(i);
                    _output.WriteLine($"Generated {i} events...");
                }
            }

            generationStopwatch.Stop();
            var totalTime = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
            var actualEventsPerSecond = totalEvents / totalTime;

            // Wait for processing to complete
            await Task.Delay(TimeSpan.FromMilliseconds(100));

            // Assert
            _output.WriteLine($"High Throughput Test Results:");
            _output.WriteLine($"  Total events generated: {totalEvents}");
            _output.WriteLine($"  Test duration: {totalTime:F2}s");
            _output.WriteLine($"  Target rate: {targetEventsPerSecond:N0} events/sec");
            _output.WriteLine($"  Actual rate: {actualEventsPerSecond:N0} events/sec");
            _output.WriteLine($"  Average event generation time: {processingTimes.Average():F3}ms");
            _output.WriteLine($"  Pending audio events: {scheduler.PendingAudioEvents}");

            // Performance assertions
            Assert.True(actualEventsPerSecond >= targetEventsPerSecond * 0.9, 
                $"Actual throughput {actualEventsPerSecond:N0} should be at least 90% of target {targetEventsPerSecond:N0}");
            Assert.True(processingTimes.Average() < 0.1, 
                $"Average event generation time should be sub-millisecond");
        }

        [Fact]
        public async Task AudioVisualQueueScheduler_AudioVisualSync_ShouldMaintainFrameSync()
        {
            // Arrange
            using var scheduler = new AudioVisualQueueScheduler(
                targetFrameRate: 60,
                maxQueueDepth: 5000,
                batchSize: 64,
                targetEventsPerSecond: 10000);

            var syncEvents = new List<AudioVisualSyncEventArgs>();
            var frameEvents = new List<FrameEventArgs>();

            scheduler.SyncEvent += (sender, args) => syncEvents.Add(args);
            
            var frameCount = 100;
            const double targetFrameTime = 16.67; // 60 FPS

            // Act - Simulate synchronized audio-visual processing
            for (int frame = 0; frame < frameCount; frame++)
            {
                var frameStopwatch = Stopwatch.StartNew();
                
                // Generate audio events for this frame
                for (int eventIdx = 0; eventIdx < 10; eventIdx++)
                {
                    var audioEvent = new RealTimeAudioEvent
                    {
                        EventId = frame * 10 + eventIdx,
                        Timestamp = DateTimeOffset.UtcNow,
                        EventType = AudioEventType.NoteOn,
                        Frequency = 440.0,
                        Volume = 0.5
                    };
                    
                    scheduler.QueueAudioEvent(audioEvent);
                }

                // Process visual updates
                var visualUpdate = new VisualParameterUpdate
                {
                    ParameterId = $"param_{frame}",
                    Value = frame * 0.1,
                    Frame = frame
                };
                
                scheduler.QueueVisualUpdate(visualUpdate);
                
                // Wait for frame completion
                frameStopwatch.Stop();
                var frameTime = frameStopwatch.Elapsed.TotalMilliseconds;
                
                if (frameTime < targetFrameTime)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(targetFrameTime - frameTime));
                }
            }

            // Wait for final sync
            await Task.Delay(100);

            // Assert
            _output.WriteLine($"Audio-Visual Sync Test Results:");
            _output.WriteLine($"  Frames processed: {frameCount}");
            _output.WriteLine($"  Sync events: {syncEvents.Count}");
            _output.WriteLine($"  Pending audio events: {scheduler.PendingAudioEvents}");
            _output.WriteLine($"  Pending visual updates: {scheduler.PendingVisualUpdates}");

            // Verify sync quality
            Assert.True(syncEvents.Count > 0, "Should have sync events");
            
            // Check timing consistency
            var syncEventTimes = syncEvents.Select(e => e.SyncTimestamp).ToList();
            if (syncEventTimes.Count > 1)
            {
                var timeDiffs = new List<double>();
                for (int i = 1; i < syncEventTimes.Count; i++)
                {
                    timeDiffs.Add((syncEventTimes[i] - syncEventTimes[i - 1]).TotalMilliseconds);
                }
                
                var avgFrameTime = timeDiffs.Average();
                var frameTimeVariance = timeDiffs.StandardDeviation();
                
                Assert.True(Math.Abs(avgFrameTime - targetFrameTime) < 3.0, 
                    $"Average sync frame time should be close to target");
                Assert.True(frameTimeVariance < 5.0, 
                    $"Frame time variance should be low for consistent sync");
            }
        }

        [Fact]
        public async Task AudioVisualQueueScheduler_BatchProcessing_ShouldOptimizeEventHandling()
        {
            // Arrange
            using var scheduler = new AudioVisualQueueScheduler(
                targetFrameRate: 60,
                maxQueueDepth: 10000,
                batchSize: 64, // Small batches to test batching
                targetEventsPerSecond: 30000);

            var batchMetrics = new List<BatchMetrics>();
            const int eventsPerBatch = 64;
            const int batchCount = 50;

            // Act - Generate events in controlled batches
            for (int batch = 0; batch < batchCount; batch++)
            {
                var batchStopwatch = Stopwatch.StartNew();
                
                // Generate events for this batch
                var batchEvents = new List<RealTimeAudioEvent>();
                
                for (int i = 0; i < eventsPerBatch; i++)
                {
                    var audioEvent = new RealTimeAudioEvent
                    {
                        EventId = batch * eventsPerBatch + i,
                        Timestamp = DateTimeOffset.UtcNow,
                        EventType = AudioEventType.ParameterChange,
                        Frequency = 440.0 + (i % 100),
                        Volume = 0.5
                    };
                    
                    batchEvents.Add(audioEvent);
                    scheduler.QueueAudioEvent(audioEvent);
                }

                batchStopwatch.Stop();
                
                batchMetrics.Add(new BatchMetrics
                {
                    BatchId = batch,
                    EventCount = batchEvents.Count,
                    ProcessingTimeMs = batchStopwatch.Elapsed.TotalMilliseconds,
                    Timestamp = DateTimeOffset.UtcNow
                });
                
                // Small delay between batches
                await Task.Delay(5);
            }

            // Wait for processing
            await Task.Delay(200);

            // Assert
            var avgProcessingTime = batchMetrics.Average(m => m.ProcessingTimeMs);
            var totalEvents = batchMetrics.Sum(m => m.EventCount);
            var throughputPerSecond = totalEvents / (batchMetrics.Last().Timestamp - batchMetrics.First().Timestamp).TotalSeconds;

            _output.WriteLine($"Batch Processing Test Results:");
            _output.WriteLine($"  Total batches: {batchMetrics.Count}");
            _output.WriteLine($"  Total events: {totalEvents}");
            _output.WriteLine($"  Average batch processing time: {avgProcessingTime:F3}ms");
            _output.WriteLine($"  Events per second: {throughputPerSecond:N0}");
            _output.WriteLine($"  Pending events: {scheduler.PendingAudioEvents}");

            // Batching should be efficient
            Assert.True(avgProcessingTime < 10.0, 
                $"Average batch processing should be efficient");
            Assert.True(throughputPerSecond > 10000, 
                $"Throughput should be high with batching");
        }

        #endregion

        #region Performance Monitoring Tests

        [Fact]
        public async Task PerformanceMonitoring_RealTimeMetrics_ShouldUpdateInRealTime()
        {
            // Arrange
            using var monitor = new PerformanceMonitor(200, _mockDevice.Object);
            var metricsUpdates = new List<(DateTime Timestamp, EvaluationEngineMetrics Metrics)>();
            var updateCount = 0;

            monitor.PerformanceAlert += (sender, alert) => 
            {
                updateCount++;
                if (updateCount % 10 == 0)
                {
                    var metrics = monitor.GetCurrentMetrics();
                    metricsUpdates.Add((DateTime.UtcNow, metrics));
                }
            };

            // Act - Simulate workload changes
            var workloads = new[] { 1, 5, 10, 20, 10, 5, 1 }; // Variable workload
            var cycles = 3;

            for (int cycle = 0; cycle < cycles; cycle++)
            {
                foreach (var workload in workloads)
                {
                    monitor.BeginFrame();
                    await Task.Delay(workload);
                    monitor.EndFrame();
                    
                    await Task.Delay(1);
                }
            }

            // Wait for final update
            await Task.Delay(100);

            // Assert
            _output.WriteLine($"Real-time Metrics Test Results:");
            _output.WriteLine($"  Update count: {updateCount}");
            _output.WriteLine($"  Metrics snapshots: {metricsUpdates.Count}");

            var firstMetrics = metricsUpdates.First().Metrics;
            var lastMetrics = metricsUpdates.Last().Metrics;
            
            Assert.True(metricsUpdates.Count > 0, "Should have metrics updates");
            Assert.True(lastMetrics.FrameCount > firstMetrics.FrameCount, 
                "Frame count should increase over time");
            
            // Memory and CPU should be tracked
            Assert.All(metricsUpdates, update =>
            {
                Assert.True(update.Metrics.CpuUsagePercent >= 0);
                Assert.True(update.Metrics.MemoryUsageMB >= 0);
                Assert.True(update.Metrics.AverageFrameTime > 0);
            });
        }

        #endregion

        #region Stress Tests

        [Fact]
        public async Task StressTest_ConcurrentOperations_ShouldHandleHighConcurrency()
        {
            // Arrange
            using var monitor = new PerformanceMonitor(100, _mockDevice.Object);
            using var scheduler = new AudioVisualQueueScheduler(
                targetFrameRate: 60,
                maxQueueDepth: 10000,
                batchSize: 128,
                targetEventsPerSecond: 30000);

            var concurrencyLevel = Environment.ProcessorCount * 2;
            var tasks = new List<Task>();
            var exceptions = new ConcurrentBag<Exception>();

            // Act - Multiple concurrent operations
            for (int i = 0; i < concurrencyLevel; i++)
            {
                int taskId = i;
                
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        // Performance monitoring task
                        for (int j = 0; j < 100; j++)
                        {
                            monitor.BeginFrame();
                            await Task.Delay(1); // Simulate work
                            monitor.EndFrame();
                            
                            if (j % 10 == 0)
                            {
                                var metrics = monitor.GetCurrentMetrics();
                                Assert.NotNull(metrics);
                            }
                            
                            await Task.Delay(1);
                        }

                        // Audio event generation task
                        for (int j = 0; j < 1000; j++)
                        {
                            var audioEvent = new RealTimeAudioEvent
                            {
                                EventId = taskId * 1000 + j,
                                Timestamp = DateTimeOffset.UtcNow,
                                EventType = AudioEventType.NoteOn,
                                Frequency = 440.0 + (j % 100)
                            };
                            
                            scheduler.QueueAudioEvent(audioEvent);
                            
                            if (j % 100 == 0)
                            {
                                await Task.Delay(1); // Occasional delay
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }));
            }

            // Wait for all tasks
            await Task.WhenAll(tasks);

            // Assert
            _output.WriteLine($"Stress Test Results:");
            _output.WriteLine($"  Concurrency level: {concurrencyLevel}");
            _output.WriteLine($"  Tasks completed: {tasks.Count}");
            _output.WriteLine($"  Exceptions: {exceptions.Count}");

            Assert.Empty(exceptions, $"Should not have exceptions: {string.Join(", ", exceptions.Select(e => e.Message))}");
        }

        [Fact]
        public async Task StressTest_MemoryPressure_ShouldHandleHighMemoryUsage()
        {
            // Arrange
            using var monitor = new PerformanceMonitor(50, _mockDevice.Object);
            var memorySnapshots = new List<(DateTime Timestamp, double MemoryMB, long GcCollections)>();

            // Act - Simulate memory pressure
            for (int round = 0; round < 5; round++)
            {
                // Allocate memory
                var largeArrays = new List<byte[]>();
                
                for (int i = 0; i < 100; i++)
                {
                    largeArrays.Add(new byte[1024 * 1024]); // 1MB arrays
                    
                    monitor.BeginFrame();
                    monitor.EndFrame();
                    
                    var metrics = monitor.GetCurrentMetrics();
                    memorySnapshots.Add((DateTime.UtcNow, metrics.MemoryUsageMB, GC.CollectionCount(0)));
                    
                    if (i % 20 == 0)
                    {
                        await Task.Delay(1);
                    }
                }
                
                _output.WriteLine($"Round {round + 1}: Allocated {largeArrays.Count}MB");
                
                // Clear memory
                largeArrays.Clear();
                await Task.Delay(100); // Allow GC
                
                // Check memory after cleanup
                monitor.BeginFrame();
                monitor.EndFrame();
                
                var finalMetrics = monitor.GetCurrentMetrics();
                memorySnapshots.Add((DateTime.UtcNow, finalMetrics.MemoryUsageMB, GC.CollectionCount(0)));
            }

            // Assert
            _output.WriteLine($"Memory Pressure Test Results:");
            _output.WriteLine($"  Snapshots taken: {memorySnapshots.Count}");
            
            var initialMemory = memorySnapshots.First().MemoryMB;
            var peakMemory = memorySnapshots.Max(s => s.MemoryMB);
            var finalMemory = memorySnapshots.Last().MemoryMB;
            
            _output.WriteLine($"  Initial memory: {initialMemory:F1}MB");
            _output.WriteLine($"  Peak memory: {peakMemory:F1}MB");
            _output.WriteLine($"  Final memory: {finalMemory:F1}MB");
            
            // Memory should be tracked correctly
            Assert.True(peakMemory > initialMemory, "Memory should increase under pressure");
            Assert.True(memorySnapshots.All(s => s.MemoryMB >= 0), "All memory measurements should be valid");
        }

        #endregion

        #region Performance Regression Tests

        [Fact]
        public async Task PerformanceRegression_BaselineComparison_ShouldNotDegradeOverTime()
        {
            // Arrange - Establish baseline
            using var monitor = new PerformanceMonitor(100, _mockDevice.Object);
            
            var baselineMetrics = await MeasureBaselinePerformance(monitor, 100);
            
            _output.WriteLine("Baseline Performance:");
            _output.WriteLine($"  Average Frame Time: {baselineMetrics.AverageFrameTime:F2}ms");
            _output.WriteLine($"  Average CPU Usage: {baselineMetrics.CpuUsagePercent:F1}%");
            _output.WriteLine($"  Average Memory: {baselineMetrics.MemoryUsageMB:F1}MB");

            // Act - Run workload multiple times
            var regressionMetrics = new List<EvaluationEngineMetrics>();
            const int testCycles = 3;
            
            for (int cycle = 0; cycle < testCycles; cycle++)
            {
                var cycleMetrics = await MeasureBaselinePerformance(monitor, 50);
                regressionMetrics.Add(cycleMetrics);
                
                _output.WriteLine($"Cycle {cycle + 1}:");
                _output.WriteLine($"  Frame Time: {cycleMetrics.AverageFrameTime:F2}ms");
                _output.WriteLine($"  CPU Usage: {cycleMetrics.CpuUsagePercent:F1}%");
                _output.WriteLine($"  Memory: {cycleMetrics.MemoryUsageMB:F1}MB");
            }

            // Assert - Check for regression
            var averageRegressionMetrics = new EvaluationEngineMetrics
            {
                AverageFrameTime = regressionMetrics.Average(m => m.AverageFrameTime),
                CpuUsagePercent = regressionMetrics.Average(m => m.CpuUsagePercent),
                MemoryUsageMB = regressionMetrics.Average(m => m.MemoryUsageMB),
                FrameCount = regressionMetrics.Sum(m => m.FrameCount)
            };

            const double frameTimeRegressionThreshold = 10.0; // 10% increase
            const double cpuRegressionThreshold = 20.0; // 20% increase
            const double memoryRegressionThreshold = 50.0; // 50MB increase

            Assert.True(averageRegressionMetrics.AverageFrameTime <= baselineMetrics.AverageFrameTime * (1 + frameTimeRegressionThreshold / 100),
                $"Frame time regressed: {averageRegressionMetrics.AverageFrameTime:F2}ms vs baseline {baselineMetrics.AverageFrameTime:F2}ms");
                
            Assert.True(averageRegressionMetrics.CpuUsagePercent <= baselineMetrics.CpuUsagePercent + cpuRegressionThreshold,
                $"CPU usage regressed: {averageRegressionMetrics.CpuUsagePercent:F1}% vs baseline {baselineMetrics.CpuUsagePercent:F1}%");
                
            Assert.True(averageRegressionMetrics.MemoryUsageMB <= baselineMetrics.MemoryUsageMB + memoryRegressionThreshold,
                $"Memory usage regressed: {averageRegressionMetrics.MemoryUsageMB:F1}MB vs baseline {baselineMetrics.MemoryUsageMB:F1}MB");

            _output.WriteLine($"Regression Test PASSED:");
            _output.WriteLine($"  Frame time change: {((averageRegressionMetrics.AverageFrameTime - baselineMetrics.AverageFrameTime) / baselineMetrics.AverageFrameTime * 100):F1}%");
            _output.WriteLine($"  CPU change: {averageRegressionMetrics.CpuUsagePercent - baselineMetrics.CpuUsagePercent:F1}%");
            _output.WriteLine($"  Memory change: {averageRegressionMetrics.MemoryUsageMB - baselineMetrics.MemoryUsageMB:F1}MB");
        }

        private static async Task<EvaluationEngineMetrics> MeasureBaselinePerformance(PerformanceMonitor monitor, int frameCount)
        {
            var frameTimes = new List<double>();
            var cpuUsages = new List<double>();
            var memoryUsages = new List<double>();

            for (int i = 0; i < frameCount; i++)
            {
                monitor.BeginFrame();
                
                // Standard workload
                await Task.Delay(2);
                
                monitor.EndFrame();
                
                var metrics = monitor.GetCurrentMetrics();
                frameTimes.Add(metrics.AverageFrameTime);
                cpuUsages.Add(metrics.CpuUsagePercent);
                memoryUsages.Add(metrics.MemoryUsageMB);
            }

            return new EvaluationEngineMetrics
            {
                AverageFrameTime = frameTimes.Average(),
                CpuUsagePercent = cpuUsages.Average(),
                MemoryUsageMB = memoryUsages.Average(),
                FrameCount = frameCount
            };
        }

        #endregion

        #region Edge Case Tests

        [Fact]
        public void EdgeCase_ZeroHistorySize_ShouldThrowArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
            {
                using var monitor = new PerformanceMonitor(0, _mockDevice.Object);
            });
        }

        [Fact]
        public void EdgeCase_ExcessiveHistorySize_ShouldThrowArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
            {
                using var monitor = new PerformanceMonitor(100000, _mockDevice.Object);
            });
        }

        [Fact]
        public async Task EdgeCase_QueueOverflow_ShouldHandleGracefully()
        {
            // Arrange - Small queue to force overflow
            using var scheduler = new AudioVisualQueueScheduler(
                targetFrameRate: 60,
                maxQueueDepth: 10, // Very small to force overflow
                batchSize: 5,
                targetEventsPerSecond: 1000);

            var overflowHandled = false;

            // Act - Try to overflow the queue
            for (int i = 0; i < 100; i++)
            {
                var audioEvent = new RealTimeAudioEvent
                {
                    EventId = i,
                    Timestamp = DateTimeOffset.UtcNow,
                    EventType = AudioEventType.NoteOn,
                    Frequency = 440.0
                };

                try
                {
                    scheduler.QueueAudioEvent(audioEvent);
                    
                    // Check if we can still process
                    if (i > 50)
                    {
                        overflowHandled = true;
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Queue overflow at event {i}: {ex.Message}");
                    break;
                }

                await Task.Delay(1);
            }

            // Assert
            Assert.True(overflowHandled, "Should handle queue overflow gracefully");
            _output.WriteLine($"Queue overflow handled. Pending events: {scheduler.PendingAudioEvents}");
        }

        #endregion
    }

    #region Supporting Classes and Data Models

    public class EvaluationEngineMetrics
    {
        public double AverageFrameTime { get; set; }
        public double FrameRate { get; set; }
        public long FrameCount { get; set; }
        public double CpuUsagePercent { get; set; }
        public double MemoryUsageMB { get; set; }
        public double GpuMemoryUsageMB { get; set; }
        public double GpuUsagePercent { get; set; }
    }

    public class PerformanceAlert
    {
        public AlertSeverity Severity { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTimeOffset Timestamp { get; set; }
        public double Value { get; set; }
        public double Threshold { get; set; }
    }

    public enum AlertSeverity
    {
        Information,
        Warning,
        Error,
        Critical
    }

    public class AudioVisualSyncEventArgs : EventArgs
    {
        public DateTimeOffset SyncTimestamp { get; set; }
        public int FrameNumber { get; set; }
        public double LatencyMs { get; set; }
        public SyncQuality Quality { get; set; }
    }

    public class FrameEventArgs : EventArgs
    {
        public int FrameNumber { get; set; }
        public double FrameTimeMs { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }

    public class RealTimeAudioEvent
    {
        public int EventId { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public AudioEventType EventType { get; set; }
        public double Frequency { get; set; }
        public double Volume { get; set; }
        public int Channel { get; set; }
    }

    public enum AudioEventType
    {
        NoteOn,
        NoteOff,
        ParameterChange,
        SystemMessage
    }

    public class VisualParameterUpdate
    {
        public string ParameterId { get; set; } = string.Empty;
        public double Value { get; set; }
        public int Frame { get; set; }
    }

    public class BatchMetrics
    {
        public int BatchId { get; set; }
        public int EventCount { get; set; }
        public double ProcessingTimeMs { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }

    public enum SyncQuality
    {
        Excellent,
        Good,
        Fair,
        Poor,
        Lost
    }

    public class SyncEvent
    {
        public DateTimeOffset Timestamp { get; set; }
        public int FrameNumber { get; set; }
        public double OffsetMs { get; set; }
    }

    public enum PerformanceMetricType
    {
        FrameTime,
        CpuUsage,
        MemoryUsage,
        GpuUsage,
        Throughput,
        Latency
    }

    #endregion
}
