using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using TiXL.Core.IO;
using TiXL.Core.Graphics.DirectX12;
using TiXL.Core.Performance;
using TiXL.Tests.Categories;
using TiXL.Tests.Fixtures;
using TiXL.Tests.Data;

namespace TiXL.Tests.Integration
{
    /// <summary>
    /// I/O isolation integration tests - ensures I/O operations don't interfere with DirectX operations
    /// Tests the complete I/O isolation system integration
    /// </summary>
    [Category(TestCategories.Integration)]
    [Category(TestCategories.IO)]
    public class IOIsolationIntegrationTests : CoreTestFixture
    {
        private readonly ITestOutputHelper _output;
        private readonly List<IOTestResult> _testResults;
        private readonly string _testDirectory;

        public IOIsolationIntegrationTests(ITestOutputHelper output) : base()
        {
            _output = output;
            _testResults = new List<IOTestResult>();
            _testDirectory = Path.Combine(Path.GetTempPath(), "TiXL_IO_Integration_Tests_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_testDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                try
                {
                    Directory.Delete(_testDirectory, true);
                }
                catch
                {
                    // Ignore cleanup errors in tests
                }
            }
        }

        [Fact]
        public async Task IOIsolation_DirectX_Concurrent_Operations()
        {
            _output.WriteLine("Testing I/O operations concurrent with DirectX operations");

            var device = CreateMockDirectXDevice();
            var commandQueue = CreateMockCommandQueue();
            var performanceMonitor = new PerformanceMonitor();
            
            using var engine = new DirectX12RenderingEngine(device, commandQueue, performanceMonitor);
            await engine.InitializeAsync();
            
            using var ioSystem = new TiXLIOIsolationSystem(performanceMonitor);
            await ioSystem.InitializeAsync();

            try
            {
                var frameCount = 60;
                var ioOperationsCompleted = 0;
                var directxOperationsCompleted = 0;
                var interferenceDetected = false;

                // Act - Run I/O and DirectX operations concurrently
                for (int frame = 0; frame < frameCount; frame++)
                {
                    using var frameToken = engine.BeginFrame();
                    var frameStart = Stopwatch.GetTimestamp();

                    // Start DirectX work
                    var directxTask = Task.Run(async () =>
                    {
                        await engine.SubmitGpuWorkAsync($"DX_Frame_{frame}",
                            async () => await SimulateDirectXWorkAsync(3.0 + frame % 5),
                            GpuTimingType.General);
                        Interlocked.Increment(ref directxOperationsCompleted);
                    });

                    // Start I/O work concurrently
                    var ioTask = Task.Run(async () =>
                    {
                        await PerformIOOperationsAsync(ioSystem, frame);
                        Interlocked.Increment(ref ioOperationsCompleted);
                    });

                    // Process frame while I/O operations run
                    await Task.Delay(2); // Small delay to let concurrent operations start

                    try
                    {
                        await engine.EndFrameAsync(frameToken);
                    }
                    catch (Exception ex)
                    {
                        _output.WriteLine($"Frame {frame} DirectX error: {ex.Message}");
                        interferenceDetected = true;
                    }

                    // Wait for concurrent operations
                    await Task.WhenAll(directxTask, ioTask);

                    // Measure frame time
                    var frameTime = (Stopwatch.GetTimestamp() - frameStart) / (double)Stopwatch.Frequency * 1000;
                    
                    // Check for interference (frame times significantly higher than expected)
                    if (frameTime > 25.0) // 25ms threshold vs 16.67ms target
                    {
                        interferenceDetected = true;
                        _output.WriteLine($"Potential interference detected at frame {frame}: {frameTime:F2}ms");
                    }

                    // Maintain frame rate
                    await Task.Delay(Math.Max(0, (int)(16.67 - frameTime)));
                }

                // Assert - Validate isolation
                var isolationScore = CalculateIsolationScore(interferenceDetected, directxOperationsCompleted, ioOperationsCompleted);
                
                ioOperationsCompleted.Should().Be(frameCount, "All I/O operations should complete");
                directxOperationsCompleted.Should().Be(frameCount, "All DirectX operations should complete");
                isolationScore.Should().BeGreaterThan(0.9, "I/O and DirectX operations should be well isolated");

                _output.WriteLine($"I/O-DirectX Isolation Results:");
                _output.WriteLine($"  I/O Operations Completed: {ioOperationsCompleted}/{frameCount}");
                _output.WriteLine($"  DirectX Operations Completed: {directxOperationsCompleted}/{frameCount}");
                _output.WriteLine($"  Interference Detected: {interferenceDetected}");
                _output.WriteLine($"  Isolation Score: {isolationScore:P2}");

                _testResults.Add(new IOTestResult
                {
                    TestName = "IO_DirectX_Concurrent_Operations",
                    Passed = !interferenceDetected && isolationScore > 0.9,
                    DurationMs = frameCount * 16.67,
                    Metrics = new Dictionary<string, double>
                    {
                        { "IOOperationsCompleted", ioOperationsCompleted },
                        { "DirectXOperationsCompleted", directxOperationsCompleted },
                        { "IsolationScore", isolationScore },
                        { "InterferenceDetected", interferenceDetected ? 1.0 : 0.0 }
                    }
                });
            }
            finally
            {
                engine.Dispose();
                ioSystem.Dispose();
            }
        }

        [Fact]
        public async Task IOIsolation_FileOperations_WithFrameRendering()
        {
            _output.WriteLine("Testing file I/O operations during frame rendering");

            var device = CreateMockDirectXDevice();
            var commandQueue = CreateMockCommandQueue();
            var performanceMonitor = new PerformanceMonitor();
            
            using var engine = new DirectX12RenderingEngine(device, commandQueue, performanceMonitor);
            await engine.InitializeAsync();
            
            using var ioSystem = new TiXLIOIsolationSystem(performanceMonitor);
            await ioSystem.InitializeAsync();

            try
            {
                var frameCount = 30;
                var fileOperations = new List<FileOperationResult>();
                var frameMetrics = new List<FrameMetric>();
                var fileOperationLatencies = new List<double>();

                // Act - Perform file operations during frame rendering
                for (int frame = 0; frame < frameCount; frame++)
                {
                    using var frameToken = engine.BeginFrame();
                    var frameStart = Stopwatch.GetTimestamp();

                    // Start frame rendering
                    var renderTask = engine.SubmitGpuWorkAsync($"Render_{frame}",
                        async () => await SimulateDirectXWorkAsync(4.0),
                        GpuTimingType.General);

                    // Start file I/O operations
                    if (frame % 3 == 0) // Every third frame, perform file operations
                    {
                        var fileOpTask = Task.Run(async () =>
                        {
                            var fileOpStart = Stopwatch.GetTimestamp();
                            
                            var writeResult = await ioSystem.QueueFileWriteAsync($"frame_{frame}.dat", GenerateTestData(frame * 1024));
                            var readResult = await ioSystem.QueueFileReadAsync($"frame_{frame - 1}.dat");
                            
                            var fileOpEnd = Stopwatch.GetTimestamp();
                            var latency = (fileOpEnd - fileOpStart) / (double)Stopwatch.Frequency * 1000;
                            
                            lock (fileOperations)
                            {
                                fileOperations.AddRange(new[] { writeResult, readResult });
                                fileOperationLatencies.Add(latency);
                            }
                        });
                    }

                    await renderTask;

                    try
                    {
                        await engine.EndFrameAsync(frameToken);
                    }
                    catch (Exception ex)
                    {
                        _output.WriteLine($"Frame {frame} error during file I/O: {ex.Message}");
                    }

                    var frameEnd = Stopwatch.GetTimestamp();
                    var frameTime = (frameEnd - frameStart) / (double)Stopwatch.Frequency * 1000;
                    
                    frameMetrics.Add(new FrameMetric
                    {
                        FrameNumber = frame,
                        FrameTimeMs = frameTime,
                        HasFileIO = frame % 3 == 0
                    });

                    await Task.Delay(Math.Max(0, (int)(16.67 - frameTime)));
                }

                // Assert - Validate file I/O doesn't impact frame rendering
                var framesWithIO = frameMetrics.Where(f => f.HasFileIO).ToList();
                var framesWithoutIO = frameMetrics.Where(f => !f.HasFileIO).ToList();

                var avgFrameTimeWithIO = framesWithIO.Any() ? framesWithIO.Average(f => f.FrameTimeMs) : 0;
                var avgFrameTimeWithoutIO = framesWithoutIO.Any() ? framesWithoutIO.Average(f => f.FrameTimeMs) : 0;
                var frameTimeImpact = (avgFrameTimeWithIO - avgFrameTimeWithoutIO) / avgFrameTimeWithoutIO;

                var successfulFileOps = fileOperations.Count(op => op.IsSuccess);
                var avgFileOpLatency = fileOperationLatencies.Any() ? fileOperationLatencies.Average() : 0;

                frameTimeImpact.Should().BeLessThan(0.1, "File I/O should not increase frame time by more than 10%");
                successfulFileOps.Should().BeGreaterThan(fileOperations.Count * 0.9, "Most file operations should succeed");
                avgFileOpLatency.Should().BeLessThan(50.0, "File operations should complete within reasonable time");

                _output.WriteLine($"File I/O During Rendering Results:`n  Average Frame Time (with I/O): {avgFrameTimeWithIO:F2}ms");
                _output.WriteLine($"  Average Frame Time (without I/O): {avgFrameTimeWithoutIO:F2}ms");
                _output.WriteLine($"  Frame Time Impact: {frameTimeImpact:P2}");
                _output.WriteLine($"  Successful File Operations: {successfulFileOps}/{fileOperations.Count}");
                _output.WriteLine($"  Average File Operation Latency: {avgFileOpLatency:F2}ms");

                _testResults.Add(new IOTestResult
                {
                    TestName = "FileOperations_WithFrameRendering",
                    Passed = frameTimeImpact < 0.1 && successfulFileOps > fileOperations.Count * 0.9,
                    DurationMs = frameCount * 16.67,
                    Metrics = new Dictionary<string, double>
                    {
                        { "AvgFrameTimeWithIO", avgFrameTimeWithIO },
                        { "AvgFrameTimeWithoutIO", avgFrameTimeWithoutIO },
                        { "FrameTimeImpact", frameTimeImpact },
                        { "SuccessfulFileOps", successfulFileOps },
                        { "TotalFileOps", fileOperations.Count },
                        { "AvgFileOpLatency", avgFileOpLatency }
                    }
                });
            }
            finally
            {
                engine.Dispose();
                ioSystem.Dispose();
            }
        }

        [Fact]
        public async Task IOIsolation_AudioIO_WithVisualRendering()
        {
            _output.WriteLine("Testing audio I/O operations concurrent with visual rendering");

            var device = CreateMockDirectXDevice();
            var commandQueue = CreateMockCommandQueue();
            var performanceMonitor = new PerformanceMonitor();
            
            using var engine = new DirectX12RenderingEngine(device, commandQueue, performanceMonitor);
            await engine.InitializeAsync();
            
            using var ioSystem = new TiXLIOIsolationSystem(performanceMonitor);
            await ioSystem.InitializeAsync();

            try
            {
                var testDuration = TimeSpan.FromSeconds(3);
                var startTime = DateTime.UtcNow;
                var audioOpsCompleted = 0;
                var visualFramesCompleted = 0;
                var syncAccuracy = 0.0;

                // Act - Generate audio and visual content concurrently
                while (DateTime.UtcNow - startTime < testDuration)
                {
                    using var frameToken = engine.BeginFrame();
                    var frameStart = Stopwatch.GetTimestamp();

                    // Visual rendering work
                    await engine.SubmitGpuWorkAsync("VisualRender",
                        async () => await SimulateDirectXWorkAsync(3.0),
                        GpuTimingType.VertexProcessing);

                    await engine.SubmitGpuWorkAsync("PostProcess",
                        async () => await SimulateDirectXWorkAsync(2.0),
                        GpuTimingType.PostProcess);

                    // Audio I/O work
                    var audioData = GenerateAudioBuffer();
                    var audioResult = await ioSystem.QueueAudioInputAsync(audioData);
                    
                    if (audioResult.IsSuccess)
                    {
                        Interlocked.Increment(ref audioOpsCompleted);
                    }

                    try
                    {
                        await engine.EndFrameAsync(frameToken);
                        Interlocked.Increment(ref visualFramesCompleted);
                    }
                    catch (Exception ex)
                    {
                        _output.WriteLine($"Frame error during audio I/O: {ex.Message}");
                    }

                    // Measure sync accuracy (simplified)
                    var frameTime = (Stopwatch.GetTimestamp() - frameStart) / (double)Stopwatch.Frequency * 1000;
                    var targetFrameTime = 16.67;
                    var syncError = Math.Abs(frameTime - targetFrameTime) / targetFrameTime;
                    
                    if (syncAccuracy == 0.0)
                    {
                        syncAccuracy = 1.0 - syncError;
                    }
                    else
                    {
                        syncAccuracy = (syncAccuracy + (1.0 - syncError)) / 2.0;
                    }

                    await Task.Delay(Math.Max(0, (int)(targetFrameTime - frameTime)));
                }

                // Assert - Validate audio-visual synchronization under I/O load
                var audioOpsPerSecond = audioOpsCompleted / testDuration.TotalSeconds;
                var visualFps = visualFramesCompleted / testDuration.TotalSeconds;
                var audioVisualBalance = CalculateAudioVisualBalance(audioOpsPerSecond, visualFps);

                visualFps.Should().BeGreaterThan(55.0, "Visual rendering should maintain good FPS");
                audioOpsPerSecond.Should().BeGreaterThan(100.0, "Audio operations should maintain good throughput");
                syncAccuracy.Should().BeGreaterThan(0.8, "Audio-visual synchronization should be maintained");
                audioVisualBalance.Should().BeGreaterThan(0.7, "Audio and visual processing should be balanced");

                _output.WriteLine($"Audio I/O with Visual Rendering Results:");
                _output.WriteLine($"  Visual FPS: {visualFps:F1}");
                _output.WriteLine($"  Audio Operations/Second: {audioOpsPerSecond:F1}");
                _output.WriteLine($"  Audio-Visual Sync Accuracy: {syncAccuracy:P2}");
                _output.WriteLine($"  Audio-Visual Balance Score: {audioVisualBalance:P2}");

                _testResults.Add(new IOTestResult
                {
                    TestName = "AudioIO_WithVisualRendering",
                    Passed = visualFps > 55.0 && syncAccuracy > 0.8,
                    DurationMs = testDuration.TotalMilliseconds,
                    Metrics = new Dictionary<string, double>
                    {
                        { "VisualFps", visualFps },
                        { "AudioOpsPerSecond", audioOpsPerSecond },
                        { "SyncAccuracy", syncAccuracy },
                        { "AudioVisualBalance", audioVisualBalance },
                        { "AudioOpsCompleted", audioOpsCompleted },
                        { "VisualFramesCompleted", visualFramesCompleted }
                    }
                });
            }
            finally
            {
                engine.Dispose();
                ioSystem.Dispose();
            }
        }

        [Fact]
        public async Task IOIsolation_NetworkIO_DuringFrameProcessing()
        {
            _output.WriteLine("Testing network I/O operations during frame processing");

            var device = CreateMockDirectXDevice();
            var commandQueue = CreateMockCommandQueue();
            var performanceMonitor = new PerformanceMonitor();
            
            using var engine = new DirectX12RenderingEngine(device, commandQueue, performanceMonitor);
            await engine.InitializeAsync();
            
            using var ioSystem = new TiXLIOIsolationSystem(performanceMonitor);
            await ioSystem.InitializeAsync();

            try
            {
                var frameCount = 40;
                var networkOpsCompleted = 0;
                var frameProcessingTimes = new List<double>();
                var networkLatencies = new List<double>();

                // Act - Perform network I/O during frame processing
                for (int frame = 0; frame < frameCount; frame++)
                {
                    using var frameToken = engine.BeginFrame();
                    var frameStart = Stopwatch.GetTimestamp();

                    // Start frame processing
                    var processingTask = engine.SubmitGpuWorkAsync($"Process_{frame}",
                        async () => await SimulateDirectXWorkAsync(5.0),
                        GpuTimingType.General);

                    // Start network I/O operations
                    if (frame % 2 == 0) // Every other frame, perform network I/O
                    {
                        var networkOpStart = Stopwatch.GetTimestamp();
                        
                        var networkData = GenerateNetworkData();
                        var networkResult = await ioSystem.QueueNetworkEventAsync("tcp://localhost:8080", networkData);
                        
                        var networkOpEnd = Stopwatch.GetTimestamp();
                        var latency = (networkOpEnd - networkOpStart) / (double)Stopwatch.Frequency * 1000;
                        
                        if (networkResult.IsSuccess)
                        {
                            Interlocked.Increment(ref networkOpsCompleted);
                        }
                        
                        networkLatencies.Add(latency);
                    }

                    await processingTask;

                    try
                    {
                        await engine.EndFrameAsync(frameToken);
                    }
                    catch (Exception ex)
                    {
                        _output.WriteLine($"Frame {frame} error during network I/O: {ex.Message}");
                    }

                    var frameEnd = Stopwatch.GetTimestamp();
                    var frameTime = (frameEnd - frameStart) / (double)Stopwatch.Frequency * 1000;
                    frameProcessingTimes.Add(frameTime);

                    await Task.Delay(Math.Max(0, (int)(16.67 - frameTime)));
                }

                // Assert - Validate network I/O doesn't disrupt frame processing
                var avgFrameTime = frameProcessingTimes.Average();
                var frameTimeVariance = CalculateVariance(frameProcessingTimes);
                var avgNetworkLatency = networkLatencies.Any() ? networkLatencies.Average() : 0;
                var networkThroughput = networkOpsCompleted / (frameCount * 16.67 / 1000.0);

                avgFrameTime.Should().BeLessThan(20.0, "Frame processing should maintain target frame time");
                frameTimeVariance.Should().BeLessThan(4.0, "Frame times should be consistent");
                avgNetworkLatency.Should().BeLessThan(100.0, "Network operations should have reasonable latency");
                networkThroughput.Should().BeGreaterThan(10.0, "Network throughput should be reasonable");

                _output.WriteLine($"Network I/O During Frame Processing Results:");
                _output.WriteLine($"  Average Frame Time: {avgFrameTime:F2}ms");
                _output.WriteLine($"  Frame Time Variance: {frameTimeVariance:F2}");
                _output.WriteLine($"  Network Operations Completed: {networkOpsCompleted}");
                _output.WriteLine($"  Average Network Latency: {avgNetworkLatency:F2}ms");
                _output.WriteLine($"  Network Throughput: {networkThroughput:F1} ops/sec");

                _testResults.Add(new IOTestResult
                {
                    TestName = "NetworkIO_DuringFrameProcessing",
                    Passed = avgFrameTime < 20.0 && avgNetworkLatency < 100.0,
                    DurationMs = frameCount * 16.67,
                    Metrics = new Dictionary<string, double>
                    {
                        { "AvgFrameTime", avgFrameTime },
                        { "FrameTimeVariance", frameTimeVariance },
                        { "NetworkOpsCompleted", networkOpsCompleted },
                        { "AvgNetworkLatency", avgNetworkLatency },
                        { "NetworkThroughput", networkThroughput }
                    }
                });
            }
            finally
            {
                engine.Dispose();
                ioSystem.Dispose();
            }
        }

        [Fact]
        public async Task IOIsolation_StressTest_HighConcurrency()
        {
            _output.WriteLine("Testing I/O isolation under high concurrency stress");

            var device = CreateMockDirectXDevice();
            var commandQueue = CreateMockCommandQueue();
            var performanceMonitor = new PerformanceMonitor();
            
            using var engine = new DirectX12RenderingEngine(device, commandQueue, performanceMonitor);
            await engine.InitializeAsync();
            
            using var ioSystem = new TiXLIOIsolationSystem(performanceMonitor);
            await ioSystem.InitializeAsync();

            try
            {
                var stressTestDuration = TimeSpan.FromSeconds(5);
                var startTime = DateTime.UtcNow;
                var concurrentFileOps = 10;
                var concurrentAudioOps = 20;
                var concurrentNetworkOps = 5;
                var concurrentDirectXOps = 5;

                var totalOperations = 0;
                var successfulOperations = 0;
                var operationResults = new List<OperationResult>();

                // Act - Run high concurrency stress test
                while (DateTime.UtcNow - startTime < stressTestDuration)
                {
                    var batchStart = Stopwatch.GetTimestamp();

                    // Create concurrent operation tasks
                    var tasks = new List<Task>();

                    // File operations
                    for (int i = 0; i < concurrentFileOps; i++)
                    {
                        tasks.Add(Task.Run(async () =>
                        {
                            var result = await ioSystem.QueueFileWriteAsync($"stress_file_{Guid.NewGuid():N}.dat", GenerateTestData(1024));
                            RecordOperationResult(operationResults, "FileWrite", result.IsSuccess);
                        }));
                    }

                    // Audio operations
                    for (int i = 0; i < concurrentAudioOps; i++)
                    {
                        tasks.Add(Task.Run(async () =>
                        {
                            var result = await ioSystem.QueueAudioInputAsync(GenerateAudioBuffer());
                            RecordOperationResult(operationResults, "AudioInput", result.IsSuccess);
                        }));
                    }

                    // Network operations
                    for (int i = 0; i < concurrentNetworkOps; i++)
                    {
                        tasks.Add(Task.Run(async () =>
                        {
                            var result = await ioSystem.QueueNetworkEventAsync("tcp://localhost:8080", GenerateNetworkData());
                            RecordOperationResult(operationResults, "Network", result.IsSuccess);
                        }));
                    }

                    // DirectX operations
                    for (int i = 0; i < concurrentDirectXOps; i++)
                    {
                        tasks.Add(Task.Run(async () =>
                        {
                            await engine.SubmitGpuWorkAsync($"StressDX_{Guid.NewGuid():N}",
                                async () => await SimulateDirectXWorkAsync(2.0),
                                GpuTimingType.General);
                            RecordOperationResult(operationResults, "DirectX", true);
                        }));
                    }

                    // Wait for all concurrent operations
                    await Task.WhenAll(tasks);

                    totalOperations += tasks.Count;
                    successfulOperations += operationResults.Count(r => r.Success && r.Timestamp > batchStart);

                    // Small delay between batches
                    await Task.Delay(10);
                }

                // Assert - Validate system stability under stress
                var successRate = successfulOperations / (double)totalOperations;
                var operationBreakdown = operationResults.GroupBy(r => r.Type).ToDictionary(g => g.Key, g => g.Count());
                var performanceDegradation = CalculatePerformanceDegradation(operationResults);

                successRate.Should().BeGreaterThan(0.85, "System should maintain high success rate under stress");
                performanceDegradation.Should().BeLessThan(0.3, "Performance should not degrade more than 30%");
                operationBreakdown.Should().HaveCountGreaterOrEqualTo(4, "All operation types should be represented");

                _output.WriteLine($"High Concurrency Stress Test Results:`n  Total Operations: {totalOperations}");
                _output.WriteLine($"  Successful Operations: {successfulOperations}");
                _output.WriteLine($"  Success Rate: {successRate:P2}");
                _output.WriteLine($"  Performance Degradation: {performanceDegradation:P2}");
                _output.WriteLine($"  Operation Breakdown:`n{string.Join(Environment.NewLine, operationBreakdown.Select(kvp => $"    {kvp.Key}: {kvp.Value}"))}");

                _testResults.Add(new IOTestResult
                {
                    TestName = "HighConcurrency_StressTest",
                    Passed = successRate > 0.85 && performanceDegradation < 0.3,
                    DurationMs = stressTestDuration.TotalMilliseconds,
                    Metrics = new Dictionary<string, double>
                    {
                        { "TotalOperations", totalOperations },
                        { "SuccessfulOperations", successfulOperations },
                        { "SuccessRate", successRate },
                        { "PerformanceDegradation", performanceDegradation }
                    }.Concat(operationBreakdown.ToDictionary(kvp => kvp.Key, kvp => (double)kvp.Value))
                      .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                });
            }
            finally
            {
                engine.Dispose();
                ioSystem.Dispose();
            }
        }

        // Helper methods

        private async Task PerformIOOperationsAsync(TiXLIOIsolationSystem ioSystem, int frame)
        {
            // File operations
            var fileResult = await ioSystem.QueueFileWriteAsync($"test_{frame}.dat", GenerateTestData(512));
            
            // Audio operations
            var audioResult = await ioSystem.QueueAudioInputAsync(GenerateAudioBuffer());
            
            // Network operations (every few frames)
            if (frame % 5 == 0)
            {
                var networkResult = await ioSystem.QueueNetworkEventAsync("tcp://localhost:8080", GenerateNetworkData());
            }
        }

        private static async Task SimulateDirectXWorkAsync(double durationMs)
        {
            await Task.Delay((int)(durationMs / 10.0)); // Scale for testing
        }

        private byte[] GenerateTestData(int size)
        {
            var data = new byte[size];
            new Random().NextBytes(data);
            return data;
        }

        private byte[] GenerateAudioBuffer()
        {
            // Generate 1KB of audio data
            return GenerateTestData(1024);
        }

        private byte[] GenerateNetworkData()
        {
            // Generate 256 bytes of network data
            return GenerateTestData(256);
        }

        private void RecordOperationResult(List<OperationResult> results, string type, bool success)
        {
            results.Add(new OperationResult
            {
                Type = type,
                Success = success,
                Timestamp = Stopwatch.GetTimestamp()
            });
        }

        private double CalculateIsolationScore(bool interferenceDetected, int directxOps, int ioOps)
        {
            if (interferenceDetected) return 0.0;
            
            var operationBalance = 1.0 - Math.Abs(directxOps - ioOps) / (double)Math.Max(directxOps, ioOps);
            return operationBalance;
        }

        private static double CalculateVariance(List<double> values)
        {
            if (values.Count < 2) return 0.0;
            var mean = values.Average();
            return values.Sum(x => (x - mean) * (x - mean)) / values.Count;
        }

        private static double CalculateAudioVisualBalance(double audioOpsPerSecond, double visualFps)
        {
            // Normalize both metrics to 0-1 range
            var normalizedAudio = Math.Min(1.0, audioOpsPerSecond / 200.0); // 200 ops/sec = 1.0
            var normalizedVisual = Math.Min(1.0, visualFps / 60.0); // 60 FPS = 1.0
            
            return (normalizedAudio + normalizedVisual) / 2.0;
        }

        private double CalculatePerformanceDegradation(List<OperationResult> operationResults)
        {
            // Simplified performance degradation calculation
            var lateOperations = operationResults.Count(r => r.Timestamp > 50000000); // Simplified threshold
            return lateOperations / (double)operationResults.Count;
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
        private class IOTestResult
        {
            public string TestName { get; set; }
            public bool Passed { get; set; }
            public double DurationMs { get; set; }
            public Dictionary<string, double> Metrics { get; set; } = new();
        }

        private class FrameMetric
        {
            public int FrameNumber { get; set; }
            public double FrameTimeMs { get; set; }
            public bool HasFileIO { get; set; }
        }

        private class OperationResult
        {
            public string Type { get; set; }
            public bool Success { get; set; }
            public long Timestamp { get; set; }
        }
    }
}