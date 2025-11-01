using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using TiXL.PerformanceSuite.Core;

namespace TiXL.PerformanceSuite.Benchmarks
{
    /// <summary>
    /// Frame time performance benchmarks for TiXL real-time rendering
    /// 
    /// These benchmarks measure:
    /// - Frame rate consistency
    /// - Frame time variance
    /// - Predictive frame scheduling effectiveness
    /// - Multi-threaded rendering performance
    /// - Audio-visual synchronization
    /// </summary>
    [SimpleJob(RuntimeMoniker.Net90, launchCount: 3, iterationCount: 10, warmupCount: 3)]
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.Fastest)]
    public class FrameTimeBenchmarks
    {
        private readonly PerformanceMonitorService _monitor;
        private readonly List<FrameMeasurement> _frameMeasurements;
        private readonly Random _random;
        
        // Simulation parameters
        private const int TARGET_FPS = 60;
        private const double TARGET_FRAME_TIME = 1000.0 / TARGET_FPS; // 16.67ms
        private const int FRAME_COUNT = 300; // 5 seconds at 60 FPS
        private readonly double[] _workLoadPattern;

        public FrameTimeBenchmarks()
        {
            _monitor = new PerformanceMonitorService(LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<PerformanceMonitorService>());
            _frameMeasurements = new List<FrameMeasurement>();
            _random = new Random(42);
            
            // Create realistic workload pattern
            _workLoadPattern = CreateRealisticWorkloadPattern();
        }

        [GlobalSetup]
        public void Setup()
        {
            _monitor.StartMonitoring().Wait();
            
            // Force garbage collection before benchmarks
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _monitor.StopMonitoring().Wait();
            _monitor.Dispose();
        }

        /// <summary>
        /// Baseline: Single-threaded frame rendering with fixed workload
        /// </summary>
        [Benchmark]
        public double SingleThreaded_Rendering_Baseline()
        {
            var totalFrameTime = 0.0;
            var frameCount = 0;

            for (int frame = 0; frame < FRAME_COUNT; frame++)
            {
                var frameStart = Stopwatch.GetTimestamp();
                
                // Simulate rendering work
                PerformRenderingWork(_workLoadPattern[frame % _workLoadPattern.Length]);
                
                var frameEnd = Stopwatch.GetTimestamp();
                var frameTime = (frameEnd - frameStart) / (double)Stopwatch.Frequency * 1000;
                
                totalFrameTime += frameTime;
                frameCount++;
                
                // Record measurement
                _frameMeasurements.Add(new FrameMeasurement
                {
                    FrameNumber = frame,
                    FrameTime = frameTime,
                    ThreadId = Thread.CurrentThread.ManagedThreadId
                });
            }

            var averageFrameTime = totalFrameTime / frameCount;
            
            // Record metrics
            _monitor.RecordBenchmarkMetric("FrameTime", "SingleThreaded_Average", averageFrameTime, "ms");
            _monitor.RecordBenchmarkMetric("FrameTime", "SingleThreaded_FPS", 1000.0 / averageFrameTime, "fps");
            
            return averageFrameTime;
        }

        /// <summary>
        /// Optimized: Multi-threaded frame rendering
        /// </summary>
        [Benchmark]
        public double MultiThreaded_Rendering_Optimized()
        {
            var totalFrameTime = 0.0;
            var frameCount = 0;
            var lockObject = new object();

            Parallel.For(0, FRAME_COUNT, frame =>
            {
                var frameStart = Stopwatch.GetTimestamp();
                
                // Multi-threaded rendering work
                var workLoad = _workLoadPattern[frame % _workLoadPattern.Length];
                PerformMultiThreadedRenderingWork(workLoad);
                
                var frameEnd = Stopwatch.GetTimestamp();
                var frameTime = (frameEnd - frameStart) / (double)Stopwatch.Frequency * 1000;
                
                lock (lockObject)
                {
                    totalFrameTime += frameTime;
                    frameCount++;
                    
                    _frameMeasurements.Add(new FrameMeasurement
                    {
                        FrameNumber = frame,
                        FrameTime = frameTime,
                        ThreadId = Thread.CurrentThread.ManagedThreadId
                    });
                }
            });

            var averageFrameTime = totalFrameTime / frameCount;
            
            _monitor.RecordBenchmarkMetric("FrameTime", "MultiThreaded_Average", averageFrameTime, "ms");
            _monitor.RecordBenchmarkMetric("FrameTime", "MultiThreaded_FPS", 1000.0 / averageFrameTime, "fps");
            
            return averageFrameTime;
        }

        /// <summary>
        /// Predictive frame scheduling for consistent FPS
        /// </summary>
        [Benchmark]
        public double Predictive_Scheduling_ConsistentFPS()
        {
            var scheduler = new PredictiveFrameScheduler(TARGET_FRAME_TIME);
            var totalFrameTime = 0.0;
            var frameCount = 0;

            for (int frame = 0; frame < FRAME_COUNT; frame++)
            {
                var frameStart = Stopwatch.GetTimestamp();
                
                // Get predicted work time from scheduler
                var predictedWorkTime = scheduler.GetPredictedWorkTime(frame);
                var workLoad = _workLoadPattern[frame % _workLoadPattern.Length];
                
                // Perform rendering work
                PerformRenderingWork(workLoad);
                
                var frameEnd = Stopwatch.GetTimestamp();
                var frameTime = (frameEnd - frameStart) / (double)Stopwatch.Frequency * 1000;
                
                // Adaptive scheduling
                scheduler.UpdateActualFrameTime(frame, frameTime);
                
                totalFrameTime += frameTime;
                frameCount++;
                
                _frameMeasurements.Add(new FrameMeasurement
                {
                    FrameNumber = frame,
                    FrameTime = frameTime,
                    ThreadId = Thread.CurrentThread.ManagedThreadId
                });
            }

            var averageFrameTime = totalFrameTime / frameCount;
            var frameVariance = CalculateFrameVariance(_frameMeasurements);
            
            _monitor.RecordBenchmarkMetric("FrameTime", "Predictive_Average", averageFrameTime, "ms");
            _monitor.RecordBenchmarkMetric("FrameTime", "Predictive_FPS", 1000.0 / averageFrameTime, "fps");
            _monitor.RecordBenchmarkMetric("FrameTime", "Predictive_Variance", frameVariance, "ms");
            
            return averageFrameTime;
        }

        /// <summary>
        /// Frame time consistency test with adaptive load balancing
        /// </summary>
        [Benchmark]
        public double Adaptive_LoadBalancing_Consistency()
        {
            const int workerCount = 4;
            var workers = new List<Task<double>>();
            var totalFrameTime = 0.0;
            var frameCount = 0;

            for (int frame = 0; frame < FRAME_COUNT; frame += workerCount)
            {
                var frameBatch = new List<Task<double>>();
                
                for (int worker = 0; worker < workerCount && frame + worker < FRAME_COUNT; worker++)
                {
                    var currentFrame = frame + worker;
                    var workLoad = _workLoadPattern[currentFrame % _workLoadPattern.Length];
                    
                    var task = Task.Run(() =>
                    {
                        var frameStart = Stopwatch.GetTimestamp();
                        PerformAdaptiveRenderingWork(workLoad, currentFrame);
                        var frameEnd = Stopwatch.GetTimestamp();
                        return (frameEnd - frameStart) / (double)Stopwatch.Frequency * 1000;
                    });
                    
                    frameBatch.Add(task);
                }

                var completedFrames = Task.WhenAll(frameBatch).Result;
                foreach (var frameTime in completedFrames)
                {
                    totalFrameTime += frameTime;
                    frameCount++;
                }
            }

            var averageFrameTime = totalFrameTime / frameCount;
            var consistencyScore = CalculateConsistencyScore(_frameMeasurements);
            
            _monitor.RecordBenchmarkMetric("FrameTime", "AdaptiveLoadBalancing_Average", averageFrameTime, "ms");
            _monitor.RecordBenchmarkMetric("FrameTime", "AdaptiveLoadBalancing_FPS", 1000.0 / averageFrameTime, "fps");
            _monitor.RecordBenchmarkMetric("FrameTime", "AdaptiveLoadBalancing_Consistency", consistencyScore, "score");
            
            return averageFrameTime;
        }

        /// <summary>
        /// Audio-visual synchronization performance
        /// </summary>
        [Benchmark]
        public double AudioVisual_Sync_Performance()
        {
            const int sampleRate = 48000;
            const int bufferSize = 1024;
            const int totalBuffers = 100;
            
            var syncErrors = new List<double>();
            var totalProcessingTime = 0.0;

            for (int buffer = 0; buffer < totalBuffers; buffer++)
            {
                var bufferStart = Stopwatch.GetTimestamp();
                
                // Simulate audio processing
                var audioData = new float[bufferSize];
                ProcessAudioBuffer(audioData, sampleRate);
                
                // Simulate video frame sync
                var syncTime = PerformVideoFrameSync(buffer, sampleRate, bufferSize);
                syncErrors.Add(syncTime);
                
                var bufferEnd = Stopwatch.GetTimestamp();
                var processingTime = (bufferEnd - bufferStart) / (double)Stopwatch.Frequency * 1000;
                totalProcessingTime += processingTime;
            }

            var averageSyncError = syncErrors.Average();
            var maxSyncError = syncErrors.Max();
            var averageProcessingTime = totalProcessingTime / totalBuffers;
            
            _monitor.RecordBenchmarkMetric("FrameTime", "AudioVisual_AvgSyncError", averageSyncError, "ms");
            _monitor.RecordBenchmarkMetric("FrameTime", "AudioVisual_MaxSyncError", maxSyncError, "ms");
            _monitor.RecordBenchmarkMetric("FrameTime", "AudioVisual_ProcessingTime", averageProcessingTime, "ms");
            
            return averageSyncError;
        }

        /// <summary>
        /// Frame time stability under stress
        /// </summary>
        [Benchmark]
        public double Stress_Test_FrameStability()
        {
            const int stressFrameCount = 500;
            var frameTimeHistory = new CircularBuffer<double>(100); // Keep last 100 frame times
            var stabilityScore = 0.0;

            for (int frame = 0; frame < stressFrameCount; frame++)
            {
                var frameStart = Stopwatch.GetTimestamp();
                
                // Variable workload to simulate stress
                var stressLevel = 0.5 + 0.5 * Math.Sin(frame * 0.1); // Oscillating stress
                PerformStressRenderingWork(stressLevel);
                
                var frameEnd = Stopwatch.GetTimestamp();
                var frameTime = (frameEnd - frameStart) / (double)Stopwatch.Frequency * 1000;
                
                frameTimeHistory.Add(frameTime);
                
                // Calculate stability over last 100 frames
                if (frame >= 100)
                {
                    var recentFrameTimes = frameTimeHistory.ToArray();
                    var mean = recentFrameTimes.Average();
                    var stdDev = CalculateStandardDeviation(recentFrameTimes);
                    stabilityScore = mean / (stdDev > 0 ? stdDev : 1); // Coefficient of variation
                }
            }

            var finalStabilityScore = stabilityScore;
            var averageFrameTime = _frameMeasurements.Skip(_frameMeasurements.Count - 100).Average(f => f.FrameTime);
            
            _monitor.RecordBenchmarkMetric("FrameTime", "StressTest_StabilityScore", finalStabilityScore, "score");
            _monitor.RecordBenchmarkMetric("FrameTime", "StressTest_Average", averageFrameTime, "ms");
            
            return finalStabilityScore;
        }

        // Helper methods for rendering simulation
        private void PerformRenderingWork(double complexity)
        {
            // Simulate vertex processing
            var vertexCount = (int)(1000 * complexity);
            ProcessVertices(vertexCount);
            
            // Simulate pixel processing
            var pixelCount = (int)(500 * complexity);
            ProcessPixels(pixelCount);
            
            // Simulate texture operations
            PerformTextureOperations((int)(10 * complexity));
            
            // Simulate shader execution
            ExecuteShaders((int)(50 * complexity));
        }

        private void PerformMultiThreadedRenderingWork(double complexity)
        {
            var vertexCount = (int)(1000 * complexity);
            var pixelCount = (int)(500 * complexity);
            
            Parallel.Invoke(
                () => ProcessVertices(vertexCount),
                () => ProcessPixels(pixelCount),
                () => PerformTextureOperations((int)(10 * complexity)),
                () => ExecuteShaders((int)(50 * complexity))
            );
        }

        private void PerformAdaptiveRenderingWork(double complexity, int frameNumber)
        {
            // Adaptive quality based on frame time history
            var quality = CalculateAdaptiveQuality(frameNumber);
            var adjustedComplexity = complexity * quality;
            
            PerformRenderingWork(adjustedComplexity);
        }

        private void PerformStressRenderingWork(double stressLevel)
        {
            // Simulate varying stress conditions
            var baseWork = 1000 + (int)(stressLevel * 5000);
            PerformRenderingWork(baseWork / 1000.0);
            
            // Add memory pressure
            if (stressLevel > 0.8)
            {
                AllocateTemporaryMemory((int)(stressLevel * 1000));
            }
        }

        private void ProcessVertices(int count)
        {
            var vertices = new float[count * 3];
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = _random.NextSingle() * 100;
            }
            
            // Simulate vertex shader operations
            for (int i = 0; i < count; i++)
            {
                var x = vertices[i * 3];
                var y = vertices[i * 3 + 1];
                var z = vertices[i * 3 + 2];
                
                // Transform
                var transformedX = x * 0.8f + y * 0.2f;
                var transformedY = y * 0.9f + z * 0.1f;
                var transformedZ = z * 0.7f + x * 0.3f;
                
                // Store result (optimized out by compiler but simulates work)
                var result = transformedX + transformedY + transformedZ;
            }
        }

        private void ProcessPixels(int count)
        {
            var pixels = new float[count * 4]; // RGBA
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = _random.NextSingle();
            }
            
            // Simulate pixel shader operations
            for (int i = 0; i < count; i++)
            {
                var r = pixels[i * 4];
                var g = pixels[i * 4 + 1];
                var b = pixels[i * 4 + 2];
                var a = pixels[i * 4 + 3];
                
                // Color calculation
                var luminance = 0.299f * r + 0.587f * g + 0.114f * b;
                var finalR = r * a + (1 - a) * luminance;
                var finalG = g * a + (1 - a) * luminance;
                var finalB = b * a + (1 - a) * luminance;
                
                var result = finalR + finalG + finalB;
            }
        }

        private void PerformTextureOperations(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var width = 256 + _random.Next(512);
                var height = 256 + _random.Next(512);
                var texels = width * height;
                
                // Simulate texture sampling
                var sampleX = _random.Next(width);
                var sampleY = _random.Next(height);
                var texelIndex = sampleY * width + sampleX;
                
                // Bilinear interpolation simulation
                var x1 = sampleX > 0 ? sampleX - 1 : sampleX;
                var y1 = sampleY > 0 ? sampleY - 1 : sampleY;
                var x2 = sampleX < width - 1 ? sampleX + 1 : sampleX;
                var y2 = sampleY < height - 1 ? sampleY + 1 : sampleY;
                
                var result = (x1 + y1 + x2 + y2) / 4.0f;
            }
        }

        private void ExecuteShaders(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var input = _random.NextSingle() * 10;
                var output = Math.Sin(input) * Math.Cos(input) + Math.Tan(input * 0.1);
                var normalized = (float)(output / Math.PI);
            }
        }

        private void ProcessAudioBuffer(float[] buffer, int sampleRate)
        {
            var bufferDuration = buffer.Length / (double)sampleRate;
            
            for (int i = 0; i < buffer.Length; i++)
            {
                var time = i / (double)sampleRate;
                buffer[i] = (float)(Math.Sin(2 * Math.PI * 440 * time) * 0.5 + 
                                   Math.Sin(2 * Math.PI * 880 * time) * 0.3 +
                                   Math.Sin(2 * Math.PI * 1320 * time) * 0.2);
            }
        }

        private double PerformVideoFrameSync(int bufferNumber, int sampleRate, int bufferSize)
        {
            var expectedFrameTime = bufferSize / (double)sampleRate * 1000; // ms
            var actualFrameTime = 16.67; // Simulated actual frame time
            
            return Math.Abs(actualFrameTime - expectedFrameTime);
        }

        private void AllocateTemporaryMemory(int size)
        {
            var tempData = new byte[size];
            for (int i = 0; i < size; i += 1024)
            {
                tempData[i] = (byte)_random.Next(256);
            }
            // Let GC handle cleanup
        }

        // Helper calculation methods
        private static double[] CreateRealisticWorkloadPattern()
        {
            var pattern = new double[120]; // 2 seconds at 60 FPS
            var random = new Random(42);
            
            for (int i = 0; i < pattern.Length; i++)
            {
                // Create realistic pattern with varying complexity
                var baseLoad = 0.7 + 0.3 * Math.Sin(i * 0.1); // Main variation
                var noise = random.NextDouble() * 0.1; // Random variation
                var spikes = i % 30 == 0 ? 0.2 : 0; // Occasional spikes
                
                pattern[i] = Math.Min(1.0, Math.Max(0.1, baseLoad + noise + spikes));
            }
            
            return pattern;
        }

        private static double CalculateFrameVariance(List<FrameMeasurement> measurements)
        {
            if (measurements.Count < 2) return 0;
            
            var frameTimes = measurements.Select(m => m.FrameTime).ToArray();
            var mean = frameTimes.Average();
            var variance = frameTimes.Select(t => Math.Pow(t - mean, 2)).Average();
            return Math.Sqrt(variance);
        }

        private static double CalculateConsistencyScore(List<FrameMeasurement> measurements)
        {
            if (measurements.Count < 2) return 0;
            
            var frameTimes = measurements.Select(m => m.FrameTime).ToArray();
            var mean = frameTimes.Average();
            var stdDev = CalculateStandardDeviation(frameTimes);
            
            // Higher score = more consistent (lower coefficient of variation)
            return mean / (stdDev > 0 ? stdDev : 1);
        }

        private static double CalculateStandardDeviation(double[] values)
        {
            if (values.Length <= 1) return 0;
            
            var mean = values.Average();
            var variance = values.Sum(x => Math.Pow(x - mean, 2)) / values.Length;
            return Math.Sqrt(variance);
        }

        private double CalculateAdaptiveQuality(int frameNumber)
        {
            // Simple adaptive quality based on recent performance
            var recentFrames = _frameMeasurements.TakeLast(10).ToList();
            if (recentFrames.Count < 5) return 1.0;
            
            var avgRecentFrameTime = recentFrames.Average(f => f.FrameTime);
            var targetFrameTime = 16.67;
            
            if (avgRecentFrameTime > targetFrameTime * 1.1)
            {
                return 0.9; // Reduce quality slightly
            }
            else if (avgRecentFrameTime < targetFrameTime * 0.8)
            {
                return 1.1; // Increase quality slightly
            }
            
            return 1.0;
        }

        // Supporting data structures
        private class FrameMeasurement
        {
            public int FrameNumber { get; set; }
            public double FrameTime { get; set; }
            public int ThreadId { get; set; }
        }

        private class PredictiveFrameScheduler
        {
            private readonly double _targetFrameTime;
            private readonly Queue<double> _recentFrameTimes;
            private const int HistorySize = 30;

            public PredictiveFrameScheduler(double targetFrameTime)
            {
                _targetFrameTime = targetFrameTime;
                _recentFrameTimes = new Queue<double>();
            }

            public double GetPredictedWorkTime(int frameNumber)
            {
                return _recentFrameTimes.Count > 0 ? _recentFrameTimes.Average() : _targetFrameTime;
            }

            public void UpdateActualFrameTime(int frameNumber, double actualFrameTime)
            {
                _recentFrameTimes.Enqueue(actualFrameTime);
                if (_recentFrameTimes.Count > HistorySize)
                {
                    _recentFrameTimes.Dequeue();
                }
            }
        }

        private class CircularBuffer<T>
        {
            private readonly T[] _buffer;
            private int _head;
            private int _count;

            public CircularBuffer(int size)
            {
                _buffer = new T[size];
                _head = 0;
                _count = 0;
            }

            public void Add(T item)
            {
                _buffer[_head] = item;
                _head = (_head + 1) % _buffer.Length;
                
                if (_count < _buffer.Length)
                    _count++;
            }

            public T[] ToArray()
            {
                var result = new T[_count];
                
                for (int i = 0; i < _count; i++)
                {
                    var index = (_head - _count + i + _buffer.Length) % _buffer.Length;
                    result[i] = _buffer[index];
                }
                
                return result;
            }
        }
    }
}