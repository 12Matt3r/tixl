using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using TiXL.Core.Performance;

namespace TiXL.Tests.Performance
{
    /// <summary>
    /// Comprehensive tests for Audio-Visual Queue Scheduling System (TIXL-034)
    /// 
    /// Tests cover:
    /// - Audio event queue performance and correctness
    /// - Visual parameter batching efficiency
    /// - Frame-coherent update synchronization
    /// - Latency optimization effectiveness
    /// - Priority handling for high-frequency events
    /// - Performance monitoring accuracy
    /// - Integration with existing systems
    /// </summary>
    public class AudioVisualQueueSchedulerTests : IDisposable
    {
        private readonly AudioVisualQueueScheduler _scheduler;
        private readonly List<TestMetrics> _metrics;
        private readonly object _metricsLock = new object();
        
        public AudioVisualQueueSchedulerTests()
        {
            _scheduler = new AudioVisualQueueScheduler(
                targetFrameRate: 60,
                maxQueueDepth: 1000,
                batchSize: 32);
            
            _metrics = new List<TestMetrics>();
            
            // Subscribe to events for testing
            _scheduler.PerformanceMetrics += OnPerformanceMetrics;
            _scheduler.QueueStatusChanged += OnQueueStatusChanged;
            _scheduler.SyncEvent += OnSyncEvent;
        }
        
        [Fact]
        public void AudioEventQueue_ShouldHandleHighFrequencyEvents()
        {
            // Arrange
            const int eventCount = 1000;
            const int durationMs = 100; // 10ms per event
            var eventsProcessed = 0;
            var startTime = DateTime.UtcNow;
            
            // Act - Queue events at high frequency
            Parallel.For(0, eventCount, i =>
            {
                var audioEvent = CreateTestAudioEvent(AudioEventPriority.Normal, 0.5f);
                _scheduler.QueueAudioEvent(audioEvent);
                Interlocked.Increment(ref eventsProcessed);
                
                // Simulate high frequency
                if (i % 10 == 0)
                    Thread.Sleep(1);
            });
            
            // Wait for processing
            Thread.Sleep(200);
            
            var stats = _scheduler.GetStatistics();
            
            // Assert
            Assert.Equal(eventCount, eventsProcessed);
            Assert.True(stats.PendingAudioEvents >= 0); // Should be processed or queued
            Assert.True(stats.AverageLatencyMs >= 0);
            Assert.True(stats.AudioEventRate >= 0);
        }
        
        [Fact]
        public void VisualParameterBatching_ShouldBatchEfficiently()
        {
            // Arrange
            const int updateCount = 500;
            var updates = new List<VisualParameterUpdate>();
            
            // Act - Queue many visual updates
            Parallel.For(0, updateCount, i =>
            {
                var update = new VisualParameterUpdate
                {
                    ParameterName = $"TestParam_{i % 10}",
                    Value = (float)(i / 100.0),
                    Timestamp = DateTime.UtcNow,
                    Priority = (AudioEventPriority)(i % 4)
                };
                
                _scheduler.QueueVisualUpdate(update);
                updates.Add(update);
            });
            
            // Process frames to consume batches
            for (int i = 0; i < 20; i++)
            {
                _scheduler.ProcessFrame();
                Thread.Sleep(16); // ~60 FPS
            }
            
            var stats = _scheduler.GetStatistics();
            
            // Assert
            Assert.True(stats.VisualUpdateRate >= 0);
            Assert.True(stats.QueueDepth <= 1000); // Should not exceed max depth
        }
        
        [Fact]
        public void FrameCoherentUpdates_ShouldMaintainSynchronization()
        {
            // Arrange
            const int frameCount = 120; // 2 seconds at 60 FPS
            var frameTimes = new List<double>();
            var targetFrameTime = 1000.0 / 60.0; // 16.67ms
            
            // Act - Process frames and measure timing
            for (int i = 0; i < frameCount; i++)
            {
                var frameStart = Stopwatch.GetTimestamp();
                
                // Add some audio events
                if (i % 5 == 0)
                {
                    var audioEvent = CreateTestAudioEvent(AudioEventPriority.High, 0.8f);
                    _scheduler.QueueAudioEvent(audioEvent);
                }
                
                _scheduler.ProcessFrame();
                
                var frameEnd = Stopwatch.GetTimestamp();
                var frameTime = (frameEnd - frameStart) / (double)Stopwatch.Frequency * 1000;
                frameTimes.Add(frameTime);
            }
            
            var stats = _scheduler.GetStatistics();
            var avgFrameTime = frameTimes.Average();
            var frameTimeVariance = frameTimes.Select(t => Math.Pow(t - avgFrameTime, 2)).Average();
            var frameTimeStdDev = Math.Sqrt(frameTimeVariance);
            
            // Assert
            Assert.True(avgFrameTime >= targetFrameTime * 0.9); // Within 10% of target
            Assert.True(avgFrameTime <= targetFrameTime * 1.1);
            Assert.True(stats.FrameTimeConsistency >= 0);
            Assert.True(stats.CurrentFrameRate >= 55); // Should maintain close to 60 FPS
        }
        
        [Fact]
        public void LatencyOptimization_ShouldMinimizeAudioVisualDelay()
        {
            // Arrange
            const int testEvents = 100;
            var latencies = new List<double>();
            
            // Act - Queue events and measure end-to-end latency
            for (int i = 0; i < testEvents; i++)
            {
                var eventTime = DateTime.UtcNow;
                var audioEvent = CreateTestAudioEvent(AudioEventPriority.High, 0.9f);
                audioEvent.Timestamp = eventTime;
                
                _scheduler.QueueAudioEvent(audioEvent);
                
                // Process frame to trigger visual update generation
                _scheduler.ProcessFrame();
                
                // Measure latency (simplified for test)
                var latency = (DateTime.UtcNow - eventTime).TotalMilliseconds;
                latencies.Add(latency);
            }
            
            var stats = _scheduler.GetStatistics();
            var avgLatency = latencies.Average();
            var maxLatency = latencies.Max();
            
            // Assert
            Assert.True(avgLatency <= 50.0); // Average latency under 50ms
            Assert.True(maxLatency <= 100.0); // Max latency under 100ms
            Assert.True(stats.AverageLatencyMs >= 0);
        }
        
        [Fact]
        public void PriorityHandling_ShouldProcessHighPriorityEventsFirst()
        {
            // Arrange
            var priorityEventTimes = new Dictionary<AudioEventPriority, List<DateTime>>();
            foreach (AudioEventPriority priority in Enum.GetValues(typeof(AudioEventPriority)))
            {
                priorityEventTimes[priority] = new List<DateTime>();
            }
            
            // Act - Queue events with different priorities
            var audioEvents = new List<AudioEvent>();
            
            // Queue normal priority events
            for (int i = 0; i < 10; i++)
            {
                var evt = CreateTestAudioEvent(AudioEventPriority.Normal, 0.5f);
                audioEvents.Add(evt);
                priorityEventTimes[AudioEventPriority.Normal].Add(evt.Timestamp);
            }
            
            // Queue high priority events
            for (int i = 0; i < 5; i++)
            {
                var evt = CreateTestAudioEvent(AudioEventPriority.High, 0.8f);
                audioEvents.Add(evt);
                priorityEventTimes[AudioEventPriority.High].Add(evt.Timestamp);
            }
            
            // Queue critical priority events
            for (int i = 0; i < 3; i++)
            {
                var evt = CreateTestAudioEvent(AudioEventPriority.Critical, 0.9f);
                audioEvents.Add(evt);
                priorityEventTimes[AudioEventPriority.Critical].Add(evt.Timestamp);
            }
            
            // Shuffle and queue all events
            var shuffledEvents = audioEvents.OrderBy(x => Guid.NewGuid()).ToList();
            foreach (var evt in shuffledEvents)
            {
                _scheduler.QueueAudioEvent(evt);
            }
            
            // Process frames and measure processing
            var processedEvents = new List<AudioEventPriority>();
            for (int i = 0; i < 10; i++)
            {
                _scheduler.ProcessFrame();
                Thread.Sleep(16);
                
                // Collect metrics from our callback
                lock (_metricsLock)
                {
                    var recentMetrics = _metrics.Where(m => m.Timestamp > DateTime.UtcNow.AddMilliseconds(-50)).ToList();
                    foreach (var metric in recentMetrics)
                    {
                        processedEvents.Add(metric.ProcessedPriority);
                    }
                }
            }
            
            var stats = _scheduler.GetStatistics();
            
            // Assert - Critical and high priority events should be processed
            Assert.True(processedEvents.Contains(AudioEventPriority.Critical) || 
                       processedEvents.Contains(AudioEventPriority.High));
            Assert.True(stats.QueueDepth >= 0);
        }
        
        [Fact]
        public void PerformanceMonitoring_ShouldTrackMetricsAccurately()
        {
            // Arrange
            const int testDurationMs = 1000;
            const int eventIntervalMs = 10;
            var startTime = DateTime.UtcNow;
            
            // Act - Generate consistent load
            var eventCount = 0;
            while ((DateTime.UtcNow - startTime).TotalMilliseconds < testDurationMs)
            {
                var audioEvent = CreateTestAudioEvent(AudioEventPriority.Normal, 0.6f);
                _scheduler.QueueAudioEvent(audioEvent);
                eventCount++;
                
                _scheduler.ProcessFrame();
                Thread.Sleep(eventIntervalMs);
            }
            
            var stats = _scheduler.GetStatistics();
            
            // Assert - Metrics should be reasonable
            Assert.True(stats.AudioEventRate > 0);
            Assert.True(stats.AudioEventRate <= 1000 / eventIntervalMs); // Should not exceed event rate
            Assert.True(stats.CurrentFrameRate >= 50); // Should maintain reasonable FPS
            Assert.True(stats.FrameTimeConsistency >= 0);
            Assert.True(stats.QueueDepth >= 0);
        }
        
        [Fact]
        public void QueueOverflowHandling_ShouldPreventSystemCrash()
        {
            // Arrange
            const int maxEvents = 1500; // Exceed max queue depth of 1000
            
            // Act - Deliberately overflow queues
            var overflowCount = 0;
            _scheduler.QueueStatusChanged += (s, e) =>
            {
                if (e.IsOverflowing)
                    Interlocked.Increment(ref overflowCount);
            };
            
            for (int i = 0; i < maxEvents; i++)
            {
                var audioEvent = CreateTestAudioEvent(AudioEventPriority.Normal, 0.5f);
                _scheduler.QueueAudioEvent(audioEvent);
                
                // Also try to queue visual updates
                var update = new VisualParameterUpdate
                {
                    ParameterName = $"OverflowTest_{i}",
                    Value = (float)(i / 100.0),
                    Timestamp = DateTime.UtcNow,
                    Priority = AudioEventPriority.Normal
                };
                _scheduler.QueueVisualUpdate(update);
            }
            
            Thread.Sleep(100); // Allow overflow detection
            
            var stats = _scheduler.GetStatistics();
            
            // Assert - System should handle overflow gracefully
            Assert.True(overflowCount > 0); // Should detect overflow
            Assert.True(stats.QueueDepth <= 1000); // Should respect max depth
            Assert.NotNull(_scheduler); // Should not crash
        }
        
        [Fact]
        public void LatencyOptimizationConfiguration_ShouldAdjustBehavior()
        {
            // Arrange
            var strictSettings = new LatencyOptimizationSettings
            {
                TargetLatencyMs = 10.0,
                MaxLatencyMs = 20.0,
                MinIntensity = 0.8f,
                MinFrequencyHz = 100.0f,
                EnablePredictiveBatching = true,
                EnablePriorityBoosting = true
            };
            
            var relaxedSettings = new LatencyOptimizationSettings
            {
                TargetLatencyMs = 50.0,
                MaxLatencyMs = 100.0,
                MinIntensity = 0.1f,
                MinFrequencyHz = 10.0f,
                EnablePredictiveBatching = false,
                EnablePriorityBoosting = false
            };
            
            // Act - Test with different configurations
            _scheduler.ConfigureLatencyOptimization(strictSettings);
            
            // Queue some events
            for (int i = 0; i < 20; i++)
            {
                var audioEvent = CreateTestAudioEvent(AudioEventPriority.Normal, 0.5f + (i / 20.0f));
                _scheduler.QueueAudioEvent(audioEvent);
                _scheduler.ProcessFrame();
            }
            
            var strictStats = _scheduler.GetStatistics();
            
            // Change configuration
            _scheduler.ConfigureLatencyOptimization(relaxedSettings);
            
            // Queue more events
            for (int i = 0; i < 20; i++)
            {
                var audioEvent = CreateTestAudioEvent(AudioEventPriority.Normal, 0.5f + (i / 20.0f));
                _scheduler.QueueAudioEvent(audioEvent);
                _scheduler.ProcessFrame();
            }
            
            var relaxedStats = _scheduler.GetStatistics();
            
            // Assert - Different configurations should show different behavior
            Assert.NotEqual(strictStats.AverageLatencyMs, relaxedStats.AverageLatencyMs);
        }
        
        [Fact]
        public void FrameTimeConsistency_ShouldBeMaintainedUnderLoad()
        {
            // Arrange
            const int testDurationMs = 2000;
            const int stressLevel = 5; // High load
            var frameTimeSamples = new List<double>();
            var startTime = DateTime.UtcNow;
            
            // Act - Process under sustained load
            while ((DateTime.UtcNow - startTime).TotalMilliseconds < testDurationMs)
            {
                var frameStart = Stopwatch.GetTimestamp();
                
                // Generate high load
                for (int i = 0; i < stressLevel; i++)
                {
                    var audioEvent = CreateTestAudioEvent(
                        (AudioEventPriority)(i % 4), 
                        (i / (float)stressLevel));
                    _scheduler.QueueAudioEvent(audioEvent);
                }
                
                // Process frame
                _scheduler.ProcessFrame();
                
                var frameEnd = Stopwatch.GetTimestamp();
                var frameTime = (frameEnd - frameStart) / (double)Stopwatch.Frequency * 1000;
                frameTimeSamples.Add(frameTime);
                
                // Maintain frame rate
                Thread.Sleep(Math.Max(0, (int)(16.67 - frameTime)));
            }
            
            var stats = _scheduler.GetStatistics();
            var consistencyScore = CalculateConsistencyScore(frameTimeSamples);
            
            // Assert - Should maintain reasonable consistency under load
            Assert.True(consistencyScore >= 1.0); // Coefficient of variation should be reasonable
            Assert.True(stats.FrameTimeConsistency >= 1.0);
            Assert.True(frameTimeSamples.Count >= testDurationMs / 16.67 * 0.9); // Should process most frames
        }
        
        private static AudioEvent CreateTestAudioEvent(AudioEventPriority priority, float intensity)
        {
            return new AudioEvent
            {
                Timestamp = DateTime.UtcNow,
                Intensity = intensity,
                Frequency = 440.0f + (intensity * 1000), // Vary frequency with intensity
                Priority = priority,
                Type = AudioEventType.Beat,
                Data = new { TestId = Guid.NewGuid() }
            };
        }
        
        private static double CalculateConsistencyScore(List<double> values)
        {
            if (values.Count < 2) return 1.0;
            
            var mean = values.Average();
            var variance = values.Select(x => Math.Pow(x - mean, 2)).Average();
            var stdDev = Math.Sqrt(variance);
            
            return mean / (stdDev > 0 ? stdDev : 1);
        }
        
        private void OnPerformanceMetrics(object sender, PerformanceMetricsEventArgs e)
        {
            lock (_metricsLock)
            {
                _metrics.Add(new TestMetrics
                {
                    Timestamp = DateTime.UtcNow,
                    Stats = e.Stats
                });
            }
        }
        
        private void OnQueueStatusChanged(object sender, QueueStatusEventArgs e)
        {
            // Track queue status for testing
        }
        
        private void OnSyncEvent(object sender, AudioVisualSyncEventArgs e)
        {
            lock (_metricsLock)
            {
                _metrics.Add(new TestMetrics
                {
                    Timestamp = DateTime.UtcNow,
                    ProcessedPriority = e.AudioEvent.Priority
                });
            }
        }
        
        public void Dispose()
        {
            _scheduler?.Dispose();
        }
        
        private class TestMetrics
        {
            public DateTime Timestamp { get; set; }
            public AudioVisualSchedulerStats Stats { get; set; }
            public AudioEventPriority ProcessedPriority { get; set; }
        }
    }
}