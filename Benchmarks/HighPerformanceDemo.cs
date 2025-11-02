using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TiXL.Core.Performance;

namespace TiXL.Benchmarks
{
    /// <summary>
    /// Standalone high-performance audio-visual queue scheduling demonstration
    /// </summary>
    public class HighPerformanceDemo
    {
        private static AudioVisualQueueScheduler _scheduler;
        private static Stopwatch _stopwatch;
        
        public static async Task Main(string[] args)
        {
            Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║              TiXL High-Performance Audio-Visual Queue Scheduler           ║");
            Console.WriteLine("║                        Demonstration & Benchmark                          ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            
            try
            {
                // Initialize high-performance scheduler
                Console.WriteLine("📋 Initializing High-Performance Scheduler...");
                _scheduler = new AudioVisualQueueScheduler(
                    targetFrameRate: 60,
                    maxQueueDepth: 20000,
                    batchSize: 256,
                    targetEventsPerSecond: 70000);
                
                _scheduler.InitializeDirectXIntegration();
                
                // Subscribe to events
                _scheduler.PerformanceMetrics += OnPerformanceMetrics;
                _scheduler.SyncEvent += OnSyncEvent;
                
                Console.WriteLine("✅ Scheduler initialized successfully");
                Console.WriteLine($"   Target Events/Second: 70,000");
                Console.WriteLine($"   Queue Depth: 20,000");
                Console.WriteLine($"   Batch Size: 256");
                Console.WriteLine($"   DirectX Integration: {(_scheduler.IsDirectXInitialized ? "Active" : "Inactive")}");
                Console.WriteLine();
                
                // Run demonstrations
                await RunDemonstrations();
                
                Console.WriteLine();
                Console.WriteLine("🎉 High-performance demonstration completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                _scheduler?.Dispose();
            }
        }
        
        private static async Task RunDemonstrations()
        {
            // Demo 1: High-throughput event queueing
            await DemoHighThroughputQueueing();
            
            // Demo 2: Real-time audio-visual synchronization
            await DemoRealTimeSynchronization();
            
            // Demo 3: Frame coherence under load
            await DemoFrameCoherence();
            
            // Demo 4: Priority handling
            await DemoPriorityHandling();
            
            // Demo 5: Performance monitoring
            DemoPerformanceMonitoring();
        }
        
        private static async Task DemoHighThroughputQueueing()
        {
            Console.WriteLine("🚀 DEMO 1: High-Throughput Event Queueing");
            Console.WriteLine("──────────────────────────────────────────");
            
            const int testDurationMs = 3000;
            const int eventsPerBurst = 1000;
            const int burstCount = 30;
            var totalEvents = 0;
            var startTime = DateTime.UtcNow;
            
            _stopwatch = Stopwatch.StartNew();
            
            // Queue events in bursts to test high throughput
            for (int burst = 0; burst < burstCount; burst++)
            {
                Console.WriteLine($"   Burst {burst + 1}/{burstCount} - Queueing {eventsPerBurst:N0} events...");
                
                var burstEvents = 0;
                await Task.Run(() =>
                {
                    Parallel.For(0, eventsPerBurst, i =>
                    {
                        var audioEvent = CreateTestAudioEvent(
                            (AudioEventPriority)(i % 4),
                            (float)(i / 100.0));
                        
                        _scheduler.QueueAudioEvent(audioEvent);
                        Interlocked.Increment(ref burstEvents);
                    });
                });
                
                totalEvents += burstEvents;
                
                // Process some frames during bursts
                for (int frame = 0; frame < 3; frame++)
                {
                    _scheduler.ProcessFrame();
                }
                
                await Task.Delay(50); // Brief pause between bursts
            }
            
            // Process remaining frames
            for (int frame = 0; frame < 60; frame++)
            {
                _scheduler.ProcessFrame();
                await Task.Delay(16);
            }
            
            _stopwatch.Stop();
            var durationMs = _stopwatch.ElapsedMilliseconds;
            var eventsPerSecond = (totalEvents / durationMs) * 1000.0;
            
            var stats = _scheduler.GetHighPerformanceStatistics();
            
            Console.WriteLine($"   ✅ Total Events Processed: {totalEvents:N0}");
            Console.WriteLine($"   ⏱️  Duration: {durationMs}ms");
            Console.WriteLine($"   🚀 Throughput: {eventsPerSecond:F0} events/sec");
            Console.WriteLine($"   📊 Peak Throughput: {stats.PeakThroughput:F0} events/sec");
            Console.WriteLine($"   🎯 Target Achievement: {(eventsPerSecond >= 50000 ? "✓ PASS" : "✗ NEEDS IMPROVEMENT")}");
            Console.WriteLine($"   📈 Queue Depth: {stats.QueueDepth}");
            Console.WriteLine();
            
            // Wait a moment for final processing
            await Task.Delay(500);
        }
        
        private static async Task DemoRealTimeSynchronization()
        {
            Console.WriteLine("🎵 DEMO 2: Real-Time Audio-Visual Synchronization");
            Console.WriteLine("─────────────────────────────────────────────────");
            
            const int syncEvents = 100;
            var syncLatencies = new List<double>();
            var successfulSyncs = 0;
            
            Console.WriteLine($"   Processing {syncEvents} real-time audio events...");
            
            for (int i = 0; i < syncEvents; i++)
            {
                var startTime = DateTime.UtcNow;
                
                var audioEvent = new AudioEvent
                {
                    Timestamp = startTime,
                    Intensity = 0.8f + (float)(i / 100.0),
                    Frequency = 440.0f + (i * 20),
                    Priority = AudioEventPriority.High,
                    Type = AudioEventType.Frequency
                };
                
                // Queue for real-time processing
                _scheduler.QueueAudioEvent(audioEvent);
                
                // Process real-time event
                var syncResult = await _scheduler.ProcessRealTimeAudioEventAsync();
                
                if (syncResult.SyncTimestamp.HasValue)
                {
                    var latency = (syncResult.SyncTimestamp.Value - startTime).TotalMilliseconds;
                    syncLatencies.Add(latency);
                    successfulSyncs++;
                }
                
                // Process frame for visual updates
                _scheduler.ProcessFrame();
                
                await Task.Delay(10); // 100 Hz real-time processing
            }
            
            var avgLatency = syncLatencies.Count > 0 ? syncLatencies.Average() : 0;
            var maxLatency = syncLatencies.Count > 0 ? syncLatencies.Max() : 0;
            var minLatency = syncLatencies.Count > 0 ? syncLatencies.Min() : 0;
            
            Console.WriteLine($"   ✅ Successful Syncs: {successfulSyncs}/{syncEvents}");
            Console.WriteLine($"   ⏱️  Average Latency: {avgLatency:F2}ms");
            Console.WriteLine($"   📊 Min/Max Latency: {minLatency:F2}ms / {maxLatency:F2}ms");
            Console.WriteLine($"   🎯 DirectX Integration: {(_scheduler.IsDirectXInitialized ? "Active" : "Inactive")}");
            Console.WriteLine($"   🎯 Real-time Performance: {(avgLatency <= 5.0 ? "✓ EXCELLENT" : avgLatency <= 10.0 ? "✓ GOOD" : "✗ NEEDS IMPROVEMENT")}");
            Console.WriteLine();
            
            await Task.Delay(300);
        }
        
        private static async Task DemoFrameCoherence()
        {
            Console.WriteLine("🖼️  DEMO 3: Frame Coherence Under Load");
            Console.WriteLine("───────────────────────────────────────");
            
            const int testDurationMs = 2000;
            const int eventsPerFrame = 200;
            var frameTimes = new List<double>();
            var totalEvents = 0;
            var framesProcessed = 0;
            
            Console.WriteLine($"   Processing frames for {testDurationMs / 1000} seconds with {eventsPerFrame} events per frame...");
            
            var startTime = DateTime.UtcNow;
            
            while ((DateTime.UtcNow - startTime).TotalMilliseconds < testDurationMs)
            {
                var frameStart = Stopwatch.GetTimestamp();
                
                // Queue events for this frame
                for (int i = 0; i < eventsPerFrame; i++)
                {
                    var audioEvent = CreateTestAudioEvent(
                        (AudioEventPriority)(i % 4),
                        (float)(i / 100.0));
                    
                    _scheduler.QueueAudioEvent(audioEvent);
                    totalEvents++;
                }
                
                // Process frame
                _scheduler.ProcessFrame();
                
                var frameEnd = Stopwatch.GetTimestamp();
                var frameTime = (frameEnd - frameStart) / (double)Stopwatch.Frequency * 1000;
                frameTimes.Add(frameTime);
                
                framesProcessed++;
                await Task.Delay(16); // Target 60 FPS
            }
            
            var avgFrameTime = frameTimes.Average();
            var actualFPS = framesProcessed / (testDurationMs / 1000.0);
            var consistencyScore = CalculateFrameTimeConsistency(frameTimes);
            
            Console.WriteLine($"   ✅ Frames Processed: {framesProcessed}");
            Console.WriteLine($"   📊 Total Events: {totalEvents:N0}");
            Console.WriteLine($"   🖼️  Average Frame Time: {avgFrameTime:F2}ms");
            Console.WriteLine($"   🎯 Actual FPS: {actualFPS:F1}");
            Console.WriteLine($"   📈 Frame Consistency: {consistencyScore:F2}");
            Console.WriteLine($"   🎯 Frame Coherence: {(consistencyScore >= 2.0 ? "✓ EXCELLENT" : consistencyScore >= 1.5 ? "✓ GOOD" : "✗ NEEDS IMPROVEMENT")}");
            Console.WriteLine();
            
            await Task.Delay(300);
        }
        
        private static async Task DemoPriorityHandling()
        {
            Console.WriteLine("⚡ DEMO 4: Priority-Based Event Handling");
            Console.WriteLine("─────────────────────────────────────────");
            
            const int eventsPerPriority = 500;
            var priorityLatencies = new Dictionary<AudioEventPriority, List<double>>();
            
            foreach (AudioEventPriority priority in Enum.GetValues(typeof(AudioEventPriority)))
            {
                priorityLatencies[priority] = new List<double>();
            }
            
            Console.WriteLine("   Testing priority-based event handling...");
            
            // Queue events by priority
            var tasks = new List<Task>();
            foreach (AudioEventPriority priority in Enum.GetValues(typeof(AudioEventPriority)))
            {
                tasks.Add(Task.Run(async () =>
                {
                    for (int i = 0; i < eventsPerPriority; i++)
                    {
                        var startTime = DateTime.UtcNow;
                        
                        var audioEvent = new AudioEvent
                        {
                            Timestamp = startTime,
                            Intensity = (float)(i / 100.0),
                            Frequency = 440.0f + (i * 10),
                            Priority = priority,
                            Type = AudioEventType.Beat
                        };
                        
                        _scheduler.QueueAudioEvent(audioEvent);
                        
                        // Process frame occasionally to trigger event handling
                        if (i % 10 == 0)
                        {
                            _scheduler.ProcessFrame();
                            await Task.Delay(1);
                        }
                    }
                }));
            }
            
            await Task.WhenAll(tasks);
            
            // Final frame processing
            for (int i = 0; i < 30; i++)
            {
                _scheduler.ProcessFrame();
                await Task.Delay(16);
            }
            
            var stats = _scheduler.GetHighPerformanceStatistics();
            
            Console.WriteLine($"   ✅ Events per Priority: {eventsPerPriority:N0}");
            Console.WriteLine($"   ⏱️  Average Latency: {stats.AverageLatencyMs:F2}ms");
            Console.WriteLine($"   🎯 Priority Handling: {(stats.AverageLatencyMs <= 25.0 ? "✓ GOOD" : "✗ NEEDS IMPROVEMENT")}");
            Console.WriteLine($"   📊 Queue Depth: {stats.QueueDepth}");
            Console.WriteLine();
            
            await Task.Delay(300);
        }
        
        private static void DemoPerformanceMonitoring()
        {
            Console.WriteLine("📊 DEMO 5: Performance Monitoring & Metrics");
            Console.WriteLine("─────────────────────────────────────────────");
            
            var stats = _scheduler.GetHighPerformanceStatistics();
            
            Console.WriteLine("   High-Performance Metrics:");
            Console.WriteLine($"   🎯 Throughput:");
            Console.WriteLine($"      Peak: {stats.PeakThroughput:F0} events/sec");
            Console.WriteLine($"      Average: {stats.AverageThroughput:F0} events/sec");
            Console.WriteLine($"   ⏱️  Latency:");
            Console.WriteLine($"      Average: {stats.AverageLatencyMs:F2}ms");
            Console.WriteLine($"      Video Pipeline: {stats.VideoPipelineLatencyMs:F2}ms");
            Console.WriteLine($"   🖼️  Frame Performance:");
            Console.WriteLine($"      Target FPS: {stats.TargetFrameRate}");
            Console.WriteLine($"      Actual FPS: {stats.CurrentFrameRate:F1}");
            Console.WriteLine($"      Consistency: {stats.FrameTimeConsistency:F2}");
            Console.WriteLine($"   🔧 System Performance:");
            Console.WriteLine($"      Thread Pool: {stats.ThreadPoolUtilization:P1}");
            Console.WriteLine($"      Lock Contention: {stats.LockContentionPercentage:F1}%");
            Console.WriteLine($"      Adaptive Batch Size: {stats.AdaptiveBatchSize}");
            Console.WriteLine($"   🎵 Audio-Visual Sync:");
            Console.WriteLine($"      DirectX Integration: {(stats.IsDirectXInitialized ? "✓ Active" : "✗ Inactive")}");
            Console.WriteLine($"      Pending Audio Events: {stats.PendingAudioEvents}");
            Console.WriteLine($"      Pending Visual Updates: {stats.PendingVisualUpdates}");
            Console.WriteLine();
            
            // Configuration demo
            Console.WriteLine("   Configuring High-Performance Optimization...");
            var optimizationSettings = new HighPerformanceOptimizationSettings
            {
                AdaptiveBatchSize = 512,
                AdaptiveQueueDepth = 25000,
                TargetLatencyMs = 8.0, // Very aggressive latency target
                EnableLockFreeOptimization = true,
                EnableAdaptiveSizing = true,
                EnablePredictiveBatching = true
            };
            
            _scheduler.ConfigureHighPerformanceOptimization(optimizationSettings);
            
            var newStats = _scheduler.GetHighPerformanceStatistics();
            Console.WriteLine($"   ✅ Configuration Updated:");
            Console.WriteLine($"      New Batch Size: {newStats.AdaptiveBatchSize}");
            Console.WriteLine($"      New Queue Depth: {newStats.AdaptiveBatchSize}");
            Console.WriteLine();
        }
        
        private static AudioEvent CreateTestAudioEvent(AudioEventPriority priority, float intensity)
        {
            return new AudioEvent
            {
                Timestamp = DateTime.UtcNow,
                Intensity = intensity,
                Frequency = 440.0f + (intensity * 2000),
                Priority = priority,
                Type = AudioEventType.Beat,
                Data = new { TestId = Guid.NewGuid(), Intensity = intensity }
            };
        }
        
        private static double CalculateFrameTimeConsistency(List<double> frameTimes)
        {
            if (frameTimes.Count < 2) return 1.0;
            
            var mean = frameTimes.Average();
            var variance = frameTimes.Select(t => Math.Pow(t - mean, 2)).Average();
            var stdDev = Math.Sqrt(variance);
            
            return mean / (stdDev > 0 ? stdDev : 1);
        }
        
        private static void OnPerformanceMetrics(object sender, HighPerformanceMetricsEventArgs e)
        {
            // Track performance metrics in real-time
            var stats = e.Stats;
            
            if (stats.PeakThroughput > 50000)
            {
                Console.WriteLine($"🚀 Peak throughput achieved: {stats.PeakThroughput:F0} events/sec!");
            }
        }
        
        private static void OnSyncEvent(object sender, AudioVisualSyncEventArgs e)
        {
            // Track sync events
            if (e.AudioEvent.Priority >= AudioEventPriority.High)
            {
                // High-priority event processed
            }
        }
    }
}
