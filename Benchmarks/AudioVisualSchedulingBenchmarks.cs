using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using TiXL.Core.Performance;

namespace TiXL.PerformanceSuite.Benchmarks
{
    /// <summary>
    /// Audio-Visual Queue Scheduling Performance Benchmarks (TIXL-034)
    /// 
    /// Benchmarks measure:
    /// - Queue throughput under high audio event loads
    /// - Visual update batching efficiency
    /// - Frame synchronization latency
    /// - Priority handling performance impact
    /// - Memory usage under sustained load
    /// - Integration overhead with existing systems
    /// </summary>
    [SimpleJob(RuntimeMoniker.Net90, launchCount: 3, iterationCount: 10, warmupCount: 3)]
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.Fastest)]
    [DisassemblyDiagnoser]
    public class AudioVisualSchedulingBenchmarks
    {
        private AudioVisualQueueScheduler _scheduler;
        private List<AudioEvent> _testAudioEvents;
        private List<VisualParameterUpdate> _testVisualUpdates;
        
        [GlobalSetup]
        public void Setup()
        {
            _scheduler = new AudioVisualQueueScheduler(
                targetFrameRate: 60,
                maxQueueDepth: 1000,
                batchSize: 32);
            
            // Generate test data
            _testAudioEvents = GenerateTestAudioEvents(1000);
            _testVisualUpdates = GenerateTestVisualUpdates(1000);
        }
        
        [GlobalCleanup]
        public void Cleanup()
        {
            _scheduler?.Dispose();
        }
        
        /// <summary>
        /// Baseline: Direct audio event processing without queueing
        /// </summary>
        [Benchmark]
        public double Baseline_DirectAudioProcessing()
        {
            var startTime = Stopwatch.GetTimestamp();
            var processedCount = 0;
            
            // Simulate direct processing without queueing
            foreach (var audioEvent in _testAudioEvents)
            {
                // Direct processing simulation
                ProcessAudioEventDirect(audioEvent);
                processedCount++;
            }
            
            var endTime = Stopwatch.GetTimestamp();
            var duration = (endTime - startTime) / (double)Stopwatch.Frequency * 1000;
            
            return processedCount / duration; // Events per ms
        }
        
        /// <summary>
        /// Audio event queue throughput test
        /// </summary>
        [Benchmark]
        public double AudioEventQueue_Throughput()
        {
            var startTime = Stopwatch.GetTimestamp();
            var queuedCount = 0;
            
            // Queue all events
            foreach (var audioEvent in _testAudioEvents)
            {
                _scheduler.QueueAudioEvent(audioEvent);
                queuedCount++;
            }
            
            var endTime = Stopwatch.GetTimestamp();
            var duration = (endTime - startTime) / (double)Stopwatch.Frequency * 1000;
            
            return queuedCount / duration; // Events per ms
        }
        
        /// <summary>
        /// Visual parameter batching performance
        /// </summary>
        [Benchmark]
        public double VisualParameterBatching_Performance()
        {
            var startTime = Stopwatch.GetTimestamp();
            var batchedCount = 0;
            
            // Queue visual updates
            foreach (var update in _testVisualUpdates)
            {
                _scheduler.QueueVisualUpdate(update);
                batchedCount++;
            }
            
            var endTime = Stopwatch.GetTimestamp();
            var duration = (endTime - startTime) / (double)Stopwatch.Frequency * 1000;
            
            return batchedCount / duration; // Updates per ms
        }
        
        /// <summary>
        /// Frame processing with mixed audio-visual load
        /// </summary>
        [Benchmark]
        public double FrameProcessing_MixedLoad()
        {
            var totalFrames = 0;
            var startTime = Stopwatch.GetTimestamp();
            
            // Queue events
            foreach (var audioEvent in _testAudioEvents)
            {
                _scheduler.QueueAudioEvent(audioEvent);
            }
            
            foreach (var update in _testVisualUpdates)
            {
                _scheduler.QueueVisualUpdate(update);
            }
            
            // Process frames
            for (int i = 0; i < 60; i++) // 1 second at 60 FPS
            {
                _scheduler.ProcessFrame();
                totalFrames++;
                Thread.Sleep(16); // Maintain frame rate
            }
            
            var endTime = Stopwatch.GetTimestamp();
            var duration = (endTime - startTime) / (double)Stopwatch.Frequency * 1000;
            
            return totalFrames / duration * 1000; // FPS
        }
        
        /// <summary>
        /// High-frequency audio event handling
        /// </summary>
        [Benchmark]
        public double HighFrequencyEventHandling()
        {
            const int eventsPerSecond = 1000; // 1kHz audio events
            const int durationSeconds = 5;
            const int totalEvents = eventsPerSecond * durationSeconds;
            
            var eventsQueued = 0;
            var startTime = Stopwatch.GetTimestamp();
            
            // Generate high-frequency events
            for (int i = 0; i < totalEvents; i++)
            {
                var audioEvent = new AudioEvent
                {
                    Timestamp = DateTime.UtcNow,
                    Intensity = (float)(Math.Sin(i * 0.1) * 0.5 + 0.5),
                    Frequency = 440.0f + (i % 1000),
                    Priority = (AudioEventPriority)(i % 4),
                    Type = AudioEventType.Frequency
                };
                
                _scheduler.QueueAudioEvent(audioEvent);
                eventsQueued++;
                
                // Control event rate
                if (i % (eventsPerSecond / 100) == 0)
                {
                    Thread.Sleep(1);
                }
            }
            
            var endTime = Stopwatch.GetTimestamp();
            var duration = (endTime - startTime) / (double)Stopwatch.Frequency * 1000;
            
            return eventsQueued / duration; // Events per ms
        }
        
        /// <summary>
        /// Priority-based event processing
        /// </summary>
        [Benchmark]
        public double PriorityBasedProcessing()
        {
            var priorityEvents = GeneratePriorityMixedEvents(2000);
            var processedCount = 0;
            var startTime = Stopwatch.GetTimestamp();
            
            // Queue mixed priority events
            foreach (var audioEvent in priorityEvents)
            {
                _scheduler.QueueAudioEvent(audioEvent);
            }
            
            // Process with priority consideration
            for (int i = 0; i < 100; i++)
            {
                _scheduler.ProcessFrame();
                processedCount += _scheduler.PendingAudioEvents > 0 ? 10 : 0; // Estimate
                Thread.Sleep(16);
            }
            
            var endTime = Stopwatch.GetTimestamp();
            var duration = (endTime - startTime) / (double)Stopwatch.Frequency * 1000;
            
            return processedCount / duration; // Processed per ms
        }
        
        /// <summary>
        /// Latency optimization overhead
        /// </summary>
        [Benchmark]
        public double LatencyOptimization_Overhead()
        {
            // Test with different latency optimization settings
            var settings = new LatencyOptimizationSettings
            {
                TargetLatencyMs = 16.67,
                MaxLatencyMs = 50.0,
                MinIntensity = 0.1f,
                EnablePredictiveBatching = true,
                EnablePriorityBoosting = true
            };
            
            _scheduler.ConfigureLatencyOptimization(settings);
            
            var optimizationRuns = 0;
            var startTime = Stopwatch.GetTimestamp();
            
            // Queue events and trigger optimizations
            for (int i = 0; i < 500; i++)
            {
                var audioEvent = CreateTestAudioEvent((AudioEventPriority)(i % 4), (i / 500.0f));
                _scheduler.QueueAudioEvent(audioEvent);
                _scheduler.ProcessFrame();
                optimizationRuns++;
            }
            
            var endTime = Stopwatch.GetTimestamp();
            var duration = (endTime - startTime) / (double)Stopwatch.Frequency * 1000;
            
            return optimizationRuns / duration; // Optimizations per ms
        }
        
        /// <summary>
        /// Memory efficiency under sustained load
        /// </summary>
        [Benchmark]
        public double MemoryEfficiency_UnderLoad()
        {
            const int iterations = 100;
            var gcCollections = GC.CollectionCount(0);
            var startTime = Stopwatch.GetTimestamp();
            
            for (int i = 0; i < iterations; i++)
            {
                // Generate load
                for (int j = 0; j < 100; j++)
                {
                    var audioEvent = CreateTestAudioEvent(AudioEventPriority.Normal, 0.5f);
                    _scheduler.QueueAudioEvent(audioEvent);
                }
                
                for (int j = 0; j < 50; j++)
                {
                    var update = new VisualParameterUpdate
                    {
                        ParameterName = $"TestParam_{j}",
                        Value = (float)(j / 50.0),
                        Timestamp = DateTime.UtcNow,
                        Priority = AudioEventPriority.Normal
                    };
                    _scheduler.QueueVisualUpdate(update);
                }
                
                _scheduler.ProcessFrame();
                
                // Periodic cleanup
                if (i % 10 == 0)
                {
                    _scheduler.FlushPendingUpdates();
                }
            }
            
            var endTime = Stopwatch.GetTimestamp();
            var duration = (endTime - startTime) / (double)Stopwatch.Frequency * 1000;
            
            var finalGcCollections = GC.CollectionCount(0);
            var gcOverhead = finalGcCollections - gcCollections;
            
            // Return operations per GC collection (lower is better for memory efficiency)
            return iterations / Math.Max(1, gcOverhead);
        }
        
        /// <summary>
        /// Frame synchronization accuracy
        /// </summary>
        [Benchmark]
        public double FrameSynchronization_Accuracy()
        {
            const int targetFrameRate = 60;
            const int testDuration = 2; // seconds
            const int totalFrames = targetFrameRate * testDuration;
            
            var frameTimeErrors = new List<double>();
            var startTime = Stopwatch.GetTimestamp();
            
            for (int frame = 0; frame < totalFrames; frame++)
            {
                var expectedFrameTime = 1000.0 / targetFrameRate;
                var frameStart = Stopwatch.GetTimestamp();
                
                // Add some audio-visual activity
                if (frame % 5 == 0)
                {
                    var audioEvent = CreateTestAudioEvent(AudioEventPriority.High, 0.8f);
                    _scheduler.QueueAudioEvent(audioEvent);
                }
                
                _scheduler.ProcessFrame();
                
                var frameEnd = Stopwatch.GetTimestamp();
                var actualFrameTime = (frameEnd - frameStart) / (double)Stopwatch.Frequency * 1000;
                
                var error = Math.Abs(actualFrameTime - expectedFrameTime);
                frameTimeErrors.Add(error);
                
                // Maintain frame timing
                var remainingTime = expectedFrameTime - actualFrameTime;
                if (remainingTime > 0)
                {
                    Thread.Sleep((int)(remainingTime));
                }
            }
            
            var endTime = Stopwatch.GetTimestamp();
            var duration = (endTime - startTime) / (double)Stopwatch.Frequency * 1000;
            
            var avgError = frameTimeErrors.Average();
            var maxError = frameTimeErrors.Max();
            
            // Return inverse of average error (higher is better for accuracy)
            return 1.0 / Math.Max(0.001, avgError);
        }
        
        /// <summary>
        /// Queue overflow handling performance
        /// </summary>
        [Benchmark]
        public double QueueOverflow_HandlingPerformance()
        {
            const int maxCapacity = 1000;
            const int overflowAttempts = 1500;
            
            var overflowsDetected = 0;
            var overflowStartTime = DateTime.MinValue;
            
            _scheduler.QueueStatusChanged += (s, e) =>
            {
                if (e.IsOverflowing)
                {
                    overflowsDetected++;
                    if (overflowStartTime == DateTime.MinValue)
                        overflowStartTime = DateTime.UtcNow;
                }
            };
            
            var startTime = Stopwatch.GetTimestamp();
            
            // Deliberately overflow the queue
            for (int i = 0; i < overflowAttempts; i++)
            {
                var audioEvent = CreateTestAudioEvent(AudioEventPriority.Normal, 0.5f);
                _scheduler.QueueAudioEvent(audioEvent);
            }
            
            // Process some frames to create room
            for (int i = 0; i < 10; i++)
            {
                _scheduler.ProcessFrame();
                Thread.Sleep(16);
            }
            
            var endTime = Stopwatch.GetTimestamp();
            var duration = (endTime - startTime) / (double)Stopwatch.Frequency * 1000;
            
            // Return handling efficiency (overflows detected per ms)
            return overflowsDetected / Math.Max(1, duration);
        }
        
        // Helper methods
        private static List<AudioEvent> GenerateTestAudioEvents(int count)
        {
            var events = new List<AudioEvent>();
            var random = new Random(42);
            
            for (int i = 0; i < count; i++)
            {
                events.Add(new AudioEvent
                {
                    Timestamp = DateTime.UtcNow,
                    Intensity = (float)random.NextDouble(),
                    Frequency = (float)(200 + random.NextDouble() * 2000),
                    Priority = (AudioEventPriority)(random.Next(4)),
                    Type = AudioEventType.Beat,
                    Data = new { Id = i }
                });
            }
            
            return events;
        }
        
        private static List<VisualParameterUpdate> GenerateTestVisualUpdates(int count)
        {
            var updates = new List<VisualParameterUpdate>();
            var random = new Random(42);
            
            for (int i = 0; i < count; i++)
            {
                updates.Add(new VisualParameterUpdate
                {
                    ParameterName = $"Param_{i % 10}",
                    Value = (float)random.NextDouble(),
                    Timestamp = DateTime.UtcNow,
                    Priority = (AudioEventPriority)(random.Next(4)),
                    Data = new { Id = i }
                });
            }
            
            return updates;
        }
        
        private static List<AudioEvent> GeneratePriorityMixedEvents(int count)
        {
            var events = new List<AudioEvent>();
            var random = new Random(42);
            
            for (int i = 0; i < count; i++)
            {
                var priority = (AudioEventPriority)(i % 4);
                events.Add(new AudioEvent
                {
                    Timestamp = DateTime.UtcNow,
                    Intensity = priority switch
                    {
                        AudioEventPriority.Critical => 0.9f + (float)random.NextDouble() * 0.1f,
                        AudioEventPriority.High => 0.7f + (float)random.NextDouble() * 0.2f,
                        AudioEventPriority.Normal => 0.4f + (float)random.NextDouble() * 0.4f,
                        AudioEventPriority.Low => 0.1f + (float)random.NextDouble() * 0.3f,
                        _ => (float)random.NextDouble()
                    },
                    Frequency = (float)(100 + random.NextDouble() * 3000),
                    Priority = priority,
                    Type = AudioEventType.Custom,
                    Data = new { Priority = priority.ToString(), Id = i }
                });
            }
            
            return events;
        }
        
        private static void ProcessAudioEventDirect(AudioEvent audioEvent)
        {
            // Simulate direct audio event processing
            var intensity = audioEvent.Intensity;
            var frequency = audioEvent.Frequency;
            
            // Simple processing simulation
            var processedValue = intensity * frequency * 0.001f;
            
            // Prevent optimization
            if (processedValue > 1000)
            {
                GC.KeepAlive(processedValue);
            }
        }
        
        private static AudioEvent CreateTestAudioEvent(AudioEventPriority priority, float intensity)
        {
            return new AudioEvent
            {
                Timestamp = DateTime.UtcNow,
                Intensity = intensity,
                Frequency = 440.0f + (intensity * 1000),
                Priority = priority,
                Type = AudioEventType.Beat,
                Data = new { Priority = priority.ToString(), Intensity = intensity }
            };
        }
    }
}