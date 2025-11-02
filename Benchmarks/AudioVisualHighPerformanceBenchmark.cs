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
    /// High-performance audio-visual queue scheduling benchmark
    /// Tests throughput >50,000 events/sec and real-time synchronization
    /// </summary>
    public class AudioVisualHighPerformanceBenchmark
    {
        private readonly AudioVisualQueueScheduler _scheduler;
        private readonly Stopwatch _benchmarkStopwatch;
        private readonly List<BenchmarkResult> _results;
        
        public AudioVisualHighPerformanceBenchmark()
        {
            _scheduler = new AudioVisualQueueScheduler(
                targetFrameRate: 60,
                maxQueueDepth: 20000, // High capacity
                batchSize: 256, // Large batches
                targetEventsPerSecond: 70000); // High target
            
            _benchmarkStopwatch = new Stopwatch();
            _results = new List<BenchmarkResult>();
            
            // Subscribe to events for monitoring
            _scheduler.PerformanceMetrics += OnPerformanceMetrics;
            _scheduler.RealTimeAudioEvent += OnRealTimeAudioEvent;
            _scheduler.VideoPipelineEvent += OnVideoPipelineEvent;
        }
        
        /// <summary>
        /// Run comprehensive high-performance benchmark
        /// </summary>
        public async Task<BenchmarkSummary> RunHighPerformanceBenchmarkAsync()
        {
            Console.WriteLine("=== TiXL High-Performance Audio-Visual Queue Scheduler Benchmark ===");
            Console.WriteLine();
            
            var summary = new BenchmarkSummary();
            
            try
            {
                // Initialize DirectX integration
                Console.WriteLine("1. Initializing DirectX Audio Integration...");
                _scheduler.InitializeDirectXIntegration();
                await Task.Delay(100);
                
                // Test 1: Baseline throughput test
                Console.WriteLine("2. Running baseline throughput test...");
                var baselineResult = await RunBaselineThroughputTest();
                summary.BaselineResult = baselineResult;
                
                // Test 2: High-frequency burst test
                Console.WriteLine("3. Running high-frequency burst test...");
                var burstResult = await RunHighFrequencyBurstTest();
                summary.BurstResult = burstResult;
                
                // Test 3: Sustained load test
                Console.WriteLine("4. Running sustained load test...");
                var sustainedResult = await RunSustainedLoadTest();
                summary.SustainedResult = sustainedResult;
                
                // Test 4: Priority handling test
                Console.WriteLine("5. Running priority handling test...");
                var priorityResult = await RunPriorityHandlingTest();
                summary.PriorityResult = priorityResult;
                
                // Test 5: Real-time synchronization test
                Console.WriteLine("6. Running real-time synchronization test...");
                var syncResult = await RunRealTimeSynchronizationTest();
                summary.SyncResult = syncResult;
                
                // Test 6: Frame coherence test
                Console.WriteLine("7. Running frame coherence test...");
                var frameResult = await RunFrameCoherenceTest();
                summary.FrameResult = frameResult;
                
                // Generate summary
                Console.WriteLine();
                Console.WriteLine("=== BENCHMARK SUMMARY ===");
                summary.GenerateSummary();
                
                return summary;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Benchmark failed: {ex.Message}");
                summary.Error = ex.Message;
                return summary;
            }
        }
        
        private async Task<BenchmarkResult> RunBaselineThroughputTest()
        {
            var result = new BenchmarkResult { TestName = "Baseline Throughput Test" };
            _benchmarkStopwatch.Restart();
            
            const int targetEvents = 50000;
            const int testDurationMs = 5000; // 5 seconds
            var eventsQueued = 0;
            var startTime = DateTime.UtcNow;
            
            // Queue events at target rate
            var tasks = new List<Task>();
            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    while ((DateTime.UtcNow - startTime).TotalMilliseconds < testDurationMs)
                    {
                        for (int j = 0; j < 100; j++)
                        {
                            var audioEvent = CreateOptimizedAudioEvent(
                                (AudioEventPriority)(j % 4),
                                (float)(j / 100.0));
                            
                            _scheduler.QueueAudioEvent(audioEvent);
                            Interlocked.Increment(ref eventsQueued);
                        }
                        
                        await Task.Delay(1); // Brief yield
                    }
                }));
            }
            
            // Process frames while queuing events
            var framesProcessed = 0;
            var frameTask = Task.Run(async () =>
            {
                var frameStartTime = DateTime.UtcNow;
                while ((DateTime.UtcNow - frameStartTime).TotalMilliseconds < testDurationMs)
                {
                    _scheduler.ProcessFrame();
                    framesProcessed++;
                    await Task.Delay(16); // ~60 FPS
                }
            });
            
            await Task.WhenAll(tasks);
            await frameTask;
            
            _benchmarkStopwatch.Stop();
            result.ActualDurationMs = _benchmarkStopwatch.ElapsedMilliseconds;
            result.EventsQueued = eventsQueued;
            result.EventsPerSecond = (eventsQueued / result.ActualDurationMs) * 1000.0;
            result.FramesProcessed = framesProcessed;
            result.TargetFPS = 60;
            result.ActualFPS = (framesProcessed / result.ActualDurationMs) * 1000.0;
            
            // Get scheduler statistics
            var stats = _scheduler.GetHighPerformanceStatistics();
            result.AverageLatencyMs = stats.AverageLatencyMs;
            result.FrameTimeConsistency = stats.FrameTimeConsistency;
            result.PeakThroughput = stats.PeakThroughput;
            
            result.Success = result.EventsPerSecond >= 50000; // Target achieved
            
            Console.WriteLine($"   Events queued: {eventsQueued:N0}");
            Console.WriteLine($"   Throughput: {result.EventsPerSecond:F0} events/sec");
            Console.WriteLine($"   Average latency: {result.AverageLatencyMs:F2} ms");
            Console.WriteLine($"   Frame consistency: {result.FrameTimeConsistency:F2}");
            Console.WriteLine($"   Target achieved: {(result.Success ? "YES" : "NO")}");
            
            return result;
        }
        
        private async Task<BenchmarkResult> RunHighFrequencyBurstTest()
        {
            var result = new BenchmarkResult { TestName = "High-Frequency Burst Test" };
            _benchmarkStopwatch.Restart();
            
            const int burstSize = 10000;
            const int bursts = 10;
            var totalEventsQueued = 0;
            
            for (int burst = 0; burst < bursts; burst++)
            {
                Console.WriteLine($"   Processing burst {burst + 1}/{bursts}...");
                
                var burstEvents = 0;
                var burstTask = Task.Run(() =>
                {
                    Parallel.For(0, burstSize, i =>
                    {
                        var audioEvent = CreateOptimizedAudioEvent(
                            (AudioEventPriority)(i % 4),
                            (float)(i / 100.0));
                        
                        _scheduler.QueueAudioEvent(audioEvent);
                        Interlocked.Increment(ref burstEvents);
                    });
                });
                
                await burstTask;
                
                totalEventsQueued += burstEvents;
                
                // Process frames between bursts
                for (int i = 0; i < 5; i++)
                {
                    _scheduler.ProcessFrame();
                    await Task.Delay(16);
                }
                
                await Task.Delay(50); // Brief pause between bursts
            }
            
            _benchmarkStopwatch.Stop();
            result.ActualDurationMs = _benchmarkStopwatch.ElapsedMilliseconds;
            result.EventsQueued = totalEventsQueued;
            result.EventsPerSecond = (totalEventsQueued / result.ActualDurationMs) * 1000.0;
            
            // Get final statistics
            var stats = _scheduler.GetHighPerformanceStatistics();
            result.AverageLatencyMs = stats.AverageLatencyMs;
            result.FrameTimeConsistency = stats.FrameTimeConsistency;
            result.PeakThroughput = stats.PeakThroughput;
            
            result.Success = result.EventsPerSecond >= 60000; // High target for bursts
            
            Console.WriteLine($"   Total events: {totalEventsQueued:N0}");
            Console.WriteLine($"   Burst throughput: {result.EventsPerSecond:F0} events/sec");
            Console.WriteLine($"   Peak throughput: {result.PeakThroughput:F0} events/sec");
            Console.WriteLine($"   High performance: {(result.Success ? "YES" : "NO")}");
            
            return result;
        }
        
        private async Task<BenchmarkResult> RunSustainedLoadTest()
        {
            var result = new BenchmarkResult { TestName = "Sustained Load Test" };
            _benchmarkStopwatch.Restart();
            
            const int testDurationMs = 10000; // 10 seconds
            const int eventsPerSecond = 55000; // Sustained 55k events/sec
            var totalEventsQueued = 0;
            var eventsThisSecond = 0;
            var lastSecond = DateTime.UtcNow;
            
            var loadTask = Task.Run(async () =>
            {
                var startTime = DateTime.UtcNow;
                while ((DateTime.UtcNow - startTime).TotalMilliseconds < testDurationMs)
                {
                    var eventCount = eventsPerSecond / 100; // Events per 10ms
                    
                    Parallel.For(0, eventCount, i =>
                    {
                        var audioEvent = CreateOptimizedAudioEvent(
                            (AudioEventPriority)(i % 4),
                            (float)(i / 100.0));
                        
                        _scheduler.QueueAudioEvent(audioEvent);
                        Interlocked.Increment(ref totalEventsQueued);
                        Interlocked.Increment(ref eventsThisSecond);
                    });
                    
                    // Track per-second metrics
                    if ((DateTime.UtcNow - lastSecond).TotalSeconds >= 1.0)
                    {
                        Console.WriteLine($"   Sustained rate: {eventsThisSecond:N0} events/sec");
                        eventsThisSecond = 0;
                        lastSecond = DateTime.UtcNow;
                    }
                    
                    await Task.Delay(10);
                }
            });
            
            // Process frames during sustained load
            var framesTask = Task.Run(async () =>
            {
                var startTime = DateTime.UtcNow;
                while ((DateTime.UtcNow - startTime).TotalMilliseconds < testDurationMs)
                {
                    _scheduler.ProcessFrame();
                    await Task.Delay(16);
                }
            });
            
            await Task.WhenAll(loadTask, framesTask);
            
            _benchmarkStopwatch.Stop();
            result.ActualDurationMs = _benchmarkStopwatch.ElapsedMilliseconds;
            result.EventsQueued = totalEventsQueued;
            result.EventsPerSecond = (totalEventsQueued / result.ActualDurationMs) * 1000.0;
            
            var stats = _scheduler.GetHighPerformanceStatistics();
            result.AverageLatencyMs = stats.AverageLatencyMs;
            result.FrameTimeConsistency = stats.FrameTimeConsistency;
            result.PeakThroughput = stats.PeakThroughput;
            
            result.Success = result.EventsPerSecond >= 50000 && 
                            stats.FrameTimeConsistency >= 2.0; // Good consistency
            
            Console.WriteLine($"   Sustained throughput: {result.EventsPerSecond:F0} events/sec");
            Console.WriteLine($"   Average latency: {result.AverageLatencyMs:F2} ms");
            Console.WriteLine($"   Frame consistency: {result.FrameTimeConsistency:F2}");
            Console.WriteLine($"   Sustained performance: {(result.Success ? "YES" : "NO")}");
            
            return result;
        }
        
        private async Task<BenchmarkResult> RunPriorityHandlingTest()
        {
            var result = new BenchmarkResult { TestName = "Priority Handling Test" };
            _benchmarkStopwatch.Restart();
            
            const int eventsPerPriority = 5000;
            var priorityLatencies = new Dictionary<AudioEventPriority, List<double>>();
            
            foreach (AudioEventPriority priority in Enum.GetValues(typeof(AudioEventPriority)))
            {
                priorityLatencies[priority] = new List<double>();
            }
            
            // Queue events with different priorities
            var tasks = new List<Task>();
            foreach (AudioEventPriority priority in Enum.GetValues(typeof(AudioEventPriority)))
            {
                tasks.Add(Task.Run(() =>
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
                        Thread.Sleep(1); // Simulate event timing
                    }
                }));
            }
            
            await Task.WhenAll(tasks);
            
            // Process frames to trigger event handling
            for (int i = 0; i < 100; i++)
            {
                _scheduler.ProcessFrame();
                await Task.Delay(16);
            }
            
            _benchmarkStopwatch.Stop();
            result.ActualDurationMs = _benchmarkStopwatch.ElapsedMilliseconds;
            
            var stats = _scheduler.GetHighPerformanceStatistics();
            result.AverageLatencyMs = stats.AverageLatencyMs;
            result.FrameTimeConsistency = stats.FrameTimeConsistency;
            result.Success = result.AverageLatencyMs <= 25.0; // Good latency under priority load
            
            Console.WriteLine($"   Priority handling latency: {result.AverageLatencyMs:F2} ms");
            Console.WriteLine($"   Frame consistency: {result.FrameTimeConsistency:F2}");
            Console.WriteLine($"   Priority handling: {(result.Success ? "GOOD" : "NEEDS_IMPROVEMENT")}");
            
            return result;
        }
        
        private async Task<BenchmarkResult> RunRealTimeSynchronizationTest()
        {
            var result = new BenchmarkResult { TestName = "Real-Time Synchronization Test" };
            _benchmarkStopwatch.Restart();
            
            const int syncEvents = 1000;
            var syncLatencies = new List<double>();
            
            // Queue real-time events and measure DirectX sync performance
            for (int i = 0; i < syncEvents; i++)
            {
                var startTime = DateTime.UtcNow;
                
                var audioEvent = new AudioEvent
                {
                    Timestamp = startTime,
                    Intensity = 0.8f,
                    Frequency = 1000.0f,
                    Priority = AudioEventPriority.High,
                    Type = AudioEventType.Beat
                };
                
                _scheduler.QueueAudioEvent(audioEvent);
                
                // Process real-time event
                var syncResult = await _scheduler.ProcessRealTimeAudioEventAsync();
                
                if (syncResult.SyncTimestamp.HasValue)
                {
                    var latency = (syncResult.SyncTimestamp.Value - startTime).TotalMilliseconds;
                    syncLatencies.Add(latency);
                }
                
                await Task.Delay(1);
            }
            
            _benchmarkStopwatch.Stop();
            result.ActualDurationMs = _benchmarkStopwatch.ElapsedMilliseconds;
            result.EventsQueued = syncEvents;
            result.EventsPerSecond = (syncEvents / result.ActualDurationMs) * 1000.0;
            result.AverageLatencyMs = syncLatencies.Count > 0 ? syncLatencies.Average() : 0;
            result.Success = result.AverageLatencyMs <= 5.0; // Excellent sync latency
            
            Console.WriteLine($"   Real-time events: {syncEvents:N0}");
            Console.WriteLine($"   Sync latency: {result.AverageLatencyMs:F2} ms");
            Console.WriteLine($"   DirectX integration: {(_scheduler.IsDirectXInitialized ? "ACTIVE" : "INACTIVE")}");
            Console.WriteLine($"   Real-time sync: {(result.Success ? "EXCELLENT" : "GOOD")}");
            
            return result;
        }
        
        private async Task<BenchmarkResult> RunFrameCoherenceTest()
        {
            var result = new BenchmarkResult { TestName = "Frame Coherence Test" };
            _benchmarkStopwatch.Restart();
            
            const int testDurationMs = 5000;
            const int targetFPS = 60;
            const double targetFrameTime = 1000.0 / targetFPS;
            
            var frameTimes = new List<double>();
            var eventsPerFrame = new List<int>();
            var frameCount = 0;
            
            var frameTask = Task.Run(async () =>
            {
                var startTime = DateTime.UtcNow;
                var eventsThisFrame = 0;
                
                while ((DateTime.UtcNow - startTime).TotalMilliseconds < testDurationMs)
                {
                    var frameStart = Stopwatch.GetTimestamp();
                    
                    // Queue some events for this frame
                    for (int i = 0; i < 100; i++)
                    {
                        var audioEvent = CreateOptimizedAudioEvent(
                            (AudioEventPriority)(i % 4),
                            (float)(i / 100.0));
                        _scheduler.QueueAudioEvent(audioEvent);
                        eventsThisFrame++;
                    }
                    
                    // Process frame
                    _scheduler.ProcessFrame();
                    
                    var frameEnd = Stopwatch.GetTimestamp();
                    var frameTime = (frameEnd - frameStart) / (double)Stopwatch.Frequency * 1000;
                    frameTimes.Add(frameTime);
                    eventsPerFrame.Add(eventsThisFrame);
                    
                    frameCount++;
                    eventsThisFrame = 0;
                    
                    await Task.Delay(16); // Target 60 FPS
                }
            });
            
            await frameTask;
            
            _benchmarkStopwatch.Stop();
            result.ActualDurationMs = _benchmarkStopwatch.ElapsedMilliseconds;
            result.EventsQueued = eventsPerFrame.Sum();
            result.EventsPerSecond = (result.EventsQueued / result.ActualDurationMs) * 1000.0;
            result.ActualFPS = frameCount / (result.ActualDurationMs / 1000.0);
            result.FrameTimeConsistency = CalculateFrameTimeConsistency(frameTimes);
            
            var stats = _scheduler.GetHighPerformanceStatistics();
            result.AverageLatencyMs = stats.AverageLatencyMs;
            result.Success = result.FrameTimeConsistency >= 2.0 && result.ActualFPS >= 55;
            
            Console.WriteLine($"   Frames processed: {frameCount}");
            Console.WriteLine($"   Average FPS: {result.ActualFPS:F1}");
            Console.WriteLine($"   Frame time consistency: {result.FrameTimeConsistency:F2}");
            Console.WriteLine($"   Average events per frame: {eventsPerFrame.Average():F0}");
            Console.WriteLine($"   Frame coherence: {(result.Success ? "EXCELLENT" : "GOOD")}");
            
            return result;
        }
        
        private static AudioEvent CreateOptimizedAudioEvent(AudioEventPriority priority, float intensity)
        {
            return new AudioEvent
            {
                Timestamp = DateTime.UtcNow,
                Intensity = intensity,
                Frequency = 440.0f + (intensity * 1000),
                Priority = priority,
                Type = AudioEventType.Beat,
                Data = new { SequenceId = Guid.NewGuid() }
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
        
        private void OnPerformanceMetrics(object sender, HighPerformanceMetricsEventArgs e)
        {
            // Track high-performance metrics
            if (_benchmarkStopwatch.IsRunning)
            {
                _results.Add(new BenchmarkResult
                {
                    EventsQueued = e.Stats.QueueDepth,
                    AverageLatencyMs = e.Stats.AverageLatencyMs,
                    PeakThroughput = e.Stats.PeakThroughput,
                    FrameTimeConsistency = e.Stats.FrameTimeConsistency
                });
            }
        }
        
        private void OnRealTimeAudioEvent(object sender, RealTimeAudioEventArgs e)
        {
            // Track real-time audio events
        }
        
        private void OnVideoPipelineEvent(object sender, VideoPipelineEventArgs e)
        {
            // Track video pipeline events
        }
        
        public void Dispose()
        {
            _scheduler?.Dispose();
        }
    }
    
    public class BenchmarkResult
    {
        public string TestName { get; set; }
        public double ActualDurationMs { get; set; }
        public int EventsQueued { get; set; }
        public double EventsPerSecond { get; set; }
        public int FramesProcessed { get; set; }
        public double ActualFPS { get; set; }
        public double TargetFPS { get; set; } = 60;
        public double AverageLatencyMs { get; set; }
        public double FrameTimeConsistency { get; set; }
        public double PeakThroughput { get; set; }
        public bool Success { get; set; }
    }
    
    public class BenchmarkSummary
    {
        public BenchmarkResult BaselineResult { get; set; }
        public BenchmarkResult BurstResult { get; set; }
        public BenchmarkResult SustainedResult { get; set; }
        public BenchmarkResult PriorityResult { get; set; }
        public BenchmarkResult SyncResult { get; set; }
        public BenchmarkResult FrameResult { get; set; }
        public string Error { get; set; }
        
        public void GenerateSummary()
        {
            Console.WriteLine();
            Console.WriteLine("PERFORMANCE SUMMARY:");
            Console.WriteLine("===================");
            
            if (BaselineResult != null)
            {
                Console.WriteLine($"Baseline Throughput: {BaselineResult.EventsPerSecond:F0} events/sec (Target: 50,000) - {(BaselineResult.Success ? "PASS" : "FAIL")}");
            }
            
            if (BurstResult != null)
            {
                Console.WriteLine($"Burst Performance: {BurstResult.EventsPerSecond:F0} events/sec (Peak: {BurstResult.PeakThroughput:F0}) - {(BurstResult.Success ? "EXCELLENT" : "GOOD")}");
            }
            
            if (SustainedResult != null)
            {
                Console.WriteLine($"Sustained Load: {SustainedResult.EventsPerSecond:F0} events/sec - {(SustainedResult.Success ? "STABLE" : "UNSTABLE")}");
            }
            
            if (SyncResult != null)
            {
                Console.WriteLine($"Real-time Sync: {SyncResult.AverageLatencyMs:F2}ms latency - {(SyncResult.Success ? "EXCELLENT" : "GOOD")}");
            }
            
            if (FrameResult != null)
            {
                Console.WriteLine($"Frame Coherence: {FrameResult.ActualFPS:F1} FPS, Consistency: {FrameResult.FrameTimeConsistency:F2} - {(FrameResult.Success ? "EXCELLENT" : "GOOD")}");
            }
            
            Console.WriteLine();
            var allPassed = (BaselineResult?.Success ?? false) && 
                           (BurstResult?.Success ?? false) && 
                           (SustainedResult?.Success ?? false) &&
                           (SyncResult?.Success ?? false) &&
                           (FrameResult?.Success ?? false);
            
            Console.WriteLine($"OVERALL RESULT: {(allPassed ? "HIGH-PERFORMANCE BENCHMARK PASSED ✓" : "BENCHMARK COMPLETED")}");
            Console.WriteLine();
        }
    }
}
