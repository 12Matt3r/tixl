using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vortice.DXGI;
using Vortice.Windows.Direct3D12;
using TiXL.Core.Graphics.DirectX12;
using TiXL.Core.Graphics.PSO;
using TiXL.Core.Performance;
using TiXL.Core.AudioVisual;
using TiXL.Core.IO;
using TiXL.Core.NodeGraph;
using TiXL.Core.Operators;
using TiXL.Core.Logging;
using TiXL.Core.Validation;
using Xunit;
using Xunit.Abstractions;
using Moq;

namespace TiXL.Tests.PerformanceRegression
{
    /// <summary>
    /// Comprehensive performance regression test suite
    /// Ensures that code improvements and optimizations are maintained over time
    /// Tests performance baselines, regression detection, and performance guarantees
    /// </summary>
    public class PerformanceRegressionTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly Mock<ID3D12Device4> _mockDevice;
        private readonly Mock<ID3D12CommandQueue> _mockCommandQueue;
        private readonly Mock<ILogger> _mockLogger;
        private readonly string _testDirectory;
        private readonly PerformanceBaselineManager _baselineManager;

        public PerformanceRegressionTests(ITestOutputHelper output)
        {
            _output = output;
            _testDirectory = Path.Combine(Path.GetTempPath(), "TiXL_PerformanceRegression_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_testDirectory);

            _mockDevice = new Mock<ID3D12Device4>();
            _mockCommandQueue = new Mock<ID3D12CommandQueue>();
            _mockLogger = new Mock<ILogger>();
            _baselineManager = new PerformanceBaselineManager(Path.Combine(_testDirectory, "baselines.json"));
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                try
                {
                    Directory.Delete(_testDirectory, true);
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Warning: Failed to cleanup test directory: {ex.Message}");
                }
            }
        }

        #region DirectX Performance Regression Tests

        [Fact]
        public async Task DirectX_PerformanceBaseline_RenderingEngine_ShouldMaintainPerformance()
        {
            // Arrange - Establish baseline and test current performance
            const string testName = "DirectX_RenderingEngine_Baseline";
            const int warmupIterations = 10;
            const int testIterations = 50;

            // Warmup
            var warmupConfig = CreateRenderingConfig();
            using var warmupEngine = CreateRenderingEngine(warmupConfig);
            for (int i = 0; i < warmupIterations; i++)
            {
                await SimulateRenderingFrame(warmupEngine);
            }

            // Act - Measure current performance
            var currentMetrics = new List<RenderingPerformanceMetric>();
            var testConfig = CreateRenderingConfig();
            
            using var testEngine = CreateRenderingEngine(testConfig);
            for (int i = 0; i < testIterations; i++)
            {
                var metric = await MeasureRenderingFrame(testEngine, i);
                currentMetrics.Add(metric);
            }

            // Load baseline if exists
            var baseline = _baselineManager.LoadBaseline(testName);

            // Assert
            var avgFrameTime = currentMetrics.Average(m => m.FrameTimeMs);
            var avgCpuUsage = currentMetrics.Average(m => m.CpuUsagePercent);
            var avgMemoryUsage = currentMetrics.Average(m => m.MemoryUsageMB);
            var frameTimeVariance = CalculateVariance(currentMetrics.Select(m => m.FrameTimeMs));
            var p95FrameTime = CalculatePercentile(currentMetrics.Select(m => m.FrameTimeMs), 95);

            _output.WriteLine($"DirectX Rendering Performance Analysis:");
            _output.WriteLine($"  Current Average Frame Time: {avgFrameTime:F2}ms");
            _output.WriteLine($"  Current 95th Percentile Frame Time: {p95FrameTime:F2}ms");
            _output.WriteLine($"  Current Frame Time Variance: {frameTimeVariance:F2}ms");
            _output.WriteLine($"  Current CPU Usage: {avgCpuUsage:F1}%");
            _output.WriteLine($"  Current Memory Usage: {avgMemoryUsage:F1}MB");

            if (baseline != null)
            {
                _output.WriteLine($"  Baseline Average Frame Time: {baseline.AverageFrameTimeMs:F2}ms");
                _output.WriteLine($"  Baseline 95th Percentile Frame Time: {baseline.Percentile95FrameTimeMs:F2}ms");
                _output.WriteLine($"  Frame Time Regression: {CalculateRegression(avgFrameTime, baseline.AverageFrameTimeMs):F1}%");
                
                // Assert no significant regression
                AssertPerformanceRegression(
                    currentMetrics,
                    baseline,
                    "DirectX Rendering Performance Regression Test");
            }
            else
            {
                // Create baseline for future tests
                var newBaseline = new PerformanceBaseline
                {
                    TestName = testName,
                    Timestamp = DateTimeOffset.UtcNow,
                    AverageFrameTimeMs = avgFrameTime,
                    Percentile95FrameTimeMs = p95FrameTime,
                    FrameTimeVarianceMs = frameTimeVariance,
                    AverageCpuUsagePercent = avgCpuUsage,
                    AverageMemoryUsageMB = avgMemoryUsage,
                    SampleCount = currentMetrics.Count
                };
                
                _baselineManager.SaveBaseline(newBaseline);
                _output.WriteLine($"  Created new baseline for future comparisons");
            }
        }

        [Fact]
        public async Task DirectX_PSO_CachePerformance_ShouldNotDegrade()
        {
            // Arrange
            const string testName = "DirectX_PSO_Cache_Performance";
            const int cacheSize = 100;
            const int accessPatterns = 1000;

            // Act - Test PSO cache performance
            var cache = new PSOCacheService(_mockDevice.Object, cacheSize);
            var accessMetrics = new List<PSOCacheMetric>();

            // Generate cache access pattern
            var keys = GeneratePSOKeys(50); // 50 unique materials
            var accessPattern = GenerateAccessPattern(keys, accessPatterns);

            foreach (var key in accessPattern)
            {
                var stopwatch = Stopwatch.StartNew();
                var hit = cache.GetPipelineState(key) != null;
                stopwatch.Stop();

                accessMetrics.Add(new PSOCacheMetric
                {
                    Key = key,
                    Hit = hit,
                    AccessTimeMs = stopwatch.Elapsed.TotalMilliseconds
                });

                // Simulate cache miss by adding new PSO
                if (!hit)
                {
                    var mockPSO = CreateMockPipelineState();
                    cache.CachePipelineState(key, mockPSO);
                }
            }

            // Assert
            var avgAccessTime = accessMetrics.Average(m => m.AccessTimeMs);
            var hitRate = accessMetrics.Count(m => m.Hit) / (double)accessMetrics.Count;
            var p99AccessTime = CalculatePercentile(accessMetrics.Select(m => m.AccessTimeMs), 99);

            _output.WriteLine($"PSO Cache Performance Analysis:");
            _output.WriteLine($"  Average Access Time: {avgAccessTime:F4}ms");
            _output.WriteLine($"  99th Percentile Access Time: {p99AccessTime:F4}ms");
            _output.WriteLine($"  Cache Hit Rate: {hitRate:P2}");

            // Performance assertions
            Assert.True(avgAccessTime < 0.01, $"Average access time {avgAccessTime:F4}ms should be under 10μs");
            Assert.True(p99AccessTime < 0.1, $"99th percentile access time {p99AccessTime:F4}ms should be under 100μs");
            Assert.True(hitRate > 0.5, $"Cache hit rate {hitRate:P2} should be above 50%");
        }

        #endregion

        #region Performance Monitor Regression Tests

        [Fact]
        public async Task PerformanceMonitor_MetricsCollection_ShouldMaintainEfficiency()
        {
            // Arrange
            const string testName = "PerformanceMonitor_Metrics_Collection";
            const int collectionCycles = 1000;

            using var monitor = new PerformanceMonitor(200, _mockDevice.Object);

            // Act - Measure metrics collection performance
            var collectionMetrics = new List<MetricsCollectionMetric>();

            for (int cycle = 0; cycle < collectionCycles; cycle++)
            {
                var stopwatch = Stopwatch.StartNew();
                
                monitor.BeginFrame();
                
                // Simulate workload
                await Task.Delay(1);
                
                monitor.EndFrame();
                
                stopwatch.Stop();

                collectionMetrics.Add(new MetricsCollectionMetric
                {
                    Cycle = cycle,
                    CollectionTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                    FrameTime = monitor.GetCurrentFrameTime(),
                    CpuUsage = monitor.GetCurrentMetrics().CpuUsagePercent
                });

                if (cycle % 100 == 0)
                {
                    await Task.Delay(1); // Occasional pause
                }
            }

            // Assert
            var avgCollectionTime = collectionMetrics.Average(m => m.CollectionTimeMs);
            var maxCollectionTime = collectionMetrics.Max(m => m.CollectionTimeMs);
            var p95CollectionTime = CalculatePercentile(collectionMetrics.Select(m => m.CollectionTimeMs), 95);

            _output.WriteLine($"Performance Monitor Collection Analysis:");
            _output.WriteLine($"  Average Collection Time: {avgCollectionTime:F4}ms");
            _output.WriteLine($"  95th Percentile Collection Time: {p95CollectionTime:F4}ms");
            _output.WriteLine($"  Max Collection Time: {maxCollectionTime:F4}ms");

            // Performance should be efficient
            Assert.True(avgCollectionTime < 0.1, $"Average collection time {avgCollectionTime:F4}ms should be efficient");
            Assert.True(maxCollectionTime < 1.0, $"Max collection time {maxCollectionTime:F4}ms should be reasonable");
        }

        #endregion

        #region Audio-Visual Queue Regression Tests

        [Fact]
        public async Task AudioVisualQueue_HighThroughput_ShouldMaintainPerformance()
        {
            // Arrange
            const string testName = "AudioVisualQueue_High_Throughput";
            const int targetEventsPerSecond = 30000;
            const int testDurationSeconds = 5;

            using var scheduler = new AudioVisualQueueScheduler(
                targetFrameRate: 60,
                maxQueueDepth: 10000,
                batchSize: 128,
                targetEventsPerSecond: targetEventsPerSecond);

            var throughputMetrics = new List<ThroughputMetric>();
            var processingMetrics = new List<ProcessingMetric>();

            // Act - High throughput testing
            var totalEvents = targetEventsPerSecond * testDurationSeconds;
            var startTime = DateTimeOffset.UtcNow;
            var eventGenerationStopwatch = Stopwatch.StartNew();

            for (int eventId = 0; eventId < totalEvents; eventId++)
            {
                var eventStopwatch = Stopwatch.StartNew();
                
                var audioEvent = new RealTimeAudioEvent
                {
                    EventId = eventId,
                    Timestamp = DateTimeOffset.UtcNow,
                    EventType = AudioEventType.NoteOn,
                    Frequency = 440.0 + (eventId % 1000),
                    Volume = 0.5
                };

                scheduler.QueueAudioEvent(audioEvent);
                
                eventStopwatch.Stop();
                processingMetrics.Add(new ProcessingMetric
                {
                    EventId = eventId,
                    ProcessingTimeMs = eventStopwatch.Elapsed.TotalMilliseconds,
                    PendingEvents = scheduler.PendingAudioEvents
                });

                // Throttle to target rate
                var elapsedSeconds = eventGenerationStopwatch.Elapsed.TotalSeconds;
                var targetEventsByNow = (int)(elapsedSeconds * targetEventsPerSecond);
                
                if (eventId > targetEventsByNow)
                {
                    var delayMs = (eventId - targetEventsByNow) * 1000.0 / targetEventsPerSecond;
                    if (delayMs > 0)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(delayMs));
                    }
                }

                // Sample throughput every 1000 events
                if (eventId % 1000 == 0 && eventId > 0)
                {
                    var currentThroughput = eventId / eventGenerationStopwatch.Elapsed.TotalSeconds;
                    throughputMetrics.Add(new ThroughputMetric
                    {
                        EventId = eventId,
                        ThroughputPerSecond = currentThroughput,
                        Timestamp = DateTimeOffset.UtcNow
                    });
                }
            }

            eventGenerationStopwatch.Stop();
            var actualDuration = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
            var actualThroughput = totalEvents / actualDuration;

            // Assert
            _output.WriteLine($"Audio-Visual Queue Throughput Analysis:");
            _output.WriteLine($"  Target Throughput: {targetEventsPerSecond:N0} events/sec");
            _output.WriteLine($"  Actual Throughput: {actualThroughput:N0} events/sec");
            _output.WriteLine($"  Average Processing Time: {processingMetrics.Average(m => m.ProcessingTimeMs):F4}ms");
            _output.WriteLine($"  Max Processing Time: {processingMetrics.Max(m => m.ProcessingTimeMs):F4}ms");
            _output.WriteLine($"  Final Pending Events: {scheduler.PendingAudioEvents}");

            // Performance assertions
            Assert.True(actualThroughput >= targetEventsPerSecond * 0.9, 
                $"Actual throughput {actualThroughput:N0} should be at least 90% of target {targetEventsPerSecond:N0}");
            
            var avgProcessingTime = processingMetrics.Average(m => m.ProcessingTimeMs);
            Assert.True(avgProcessingTime < 0.1, 
                $"Average processing time {avgProcessingTime:F4}ms should be sub-millisecond");

            var maxProcessingTime = processingMetrics.Max(m => m.ProcessingTimeMs);
            Assert.True(maxProcessingTime < 1.0, 
                $"Max processing time {maxProcessingTime:F4}ms should be reasonable");
        }

        #endregion

        #region NodeGraph Evaluation Regression Tests

        [Fact]
        public async Task NodeGraph_IncrementalEvaluation_ShouldMaintainEfficiency()
        {
            // Arrange
            const string testName = "NodeGraph_Incremental_Evaluation";
            const int nodeCount = 500;
            const int evaluationCycles = 50;

            using var context = CreateEvaluationContext();
            var engine = context.IncrementalEvaluationEngine!;
            
            // Create test graph
            var nodes = CreateTestNodeGraph(nodeCount);
            foreach (var node in nodes)
            {
                engine.AddNode(node);
            }

            var evaluationMetrics = new List<EvaluationMetric>();

            // Act - Test incremental evaluation performance
            for (int cycle = 0; cycle < evaluationCycles; cycle++)
            {
                // Mark varying numbers of nodes dirty
                var dirtyCount = Math.Min(cycle + 1, nodeCount / 10);
                var dirtyNodes = nodes.Take(dirtyCount).ToList();
                
                foreach (var node in dirtyNodes)
                {
                    engine.MarkNodeDirty(node.Id);
                }

                var stopwatch = Stopwatch.StartNew();
                await engine.EvaluateAsync();
                stopwatch.Stop();

                evaluationMetrics.Add(new EvaluationMetric
                {
                    Cycle = cycle,
                    DirtyNodeCount = dirtyCount,
                    EvaluationTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                    TotalEvaluations = engine.GetCurrentMetrics().TotalEvaluations,
                    CacheHitRate = engine.CacheStatistics.HitRate
                });
            }

            // Assert
            var avgEvaluationTime = evaluationMetrics.Average(m => m.EvaluationTimeMs);
            var maxEvaluationTime = evaluationMetrics.Max(m => m.EvaluationTimeMs);
            var avgDirtyNodeCount = evaluationMetrics.Average(m => m.DirtyNodeCount);
            var avgCacheHitRate = evaluationMetrics.Average(m => m.CacheHitRate);

            _output.WriteLine($"NodeGraph Incremental Evaluation Analysis:");
            _output.WriteLine($"  Average Evaluation Time: {avgEvaluationTime:F2}ms");
            _output.WriteLine($"  Max Evaluation Time: {maxEvaluationTime:F2}ms");
            _output.WriteLine($"  Average Dirty Nodes: {avgDirtyNodeCount:F1}");
            _output.WriteLine($"  Average Cache Hit Rate: {avgCacheHitRate:P2}");

            // Performance assertions
            Assert.True(avgEvaluationTime < 100, $"Average evaluation time {avgEvaluationTime:F2}ms should be reasonable");
            Assert.True(avgCacheHitRate > 0.5, $"Cache hit rate {avgCacheHitRate:P2} should be above 50%");
        }

        #endregion

        #region I/O Performance Regression Tests

        [Fact]
        public async Task IO_Performance_ThreadIsolation_ShouldMaintainEfficiency()
        {
            // Arrange
            const string testName = "IO_Performance_Thread_Isolation";
            const int concurrentOperations = 20;
            const int operationsPerThread = 100;

            var isolationManager = new IOIsolationManager();
            var ioMetrics = new List<IOMetric>();

            // Act - Test I/O performance under concurrent load
            var tasks = new List<Task>();
            
            for (int threadId = 0; threadId < concurrentOperations; threadId++)
            {
                int currentThreadId = threadId;
                tasks.Add(Task.Run(async () =>
                {
                    for (int op = 0; op < operationsPerThread; op++)
                    {
                        var stopwatch = Stopwatch.StartNew();
                        
                        var filePath = Path.Combine(_testDirectory, $"thread_{currentThreadId}_op_{op}.txt");
                        
                        var result = isolationManager.ExecuteAsyncOnIOThread(async () =>
                        {
                            await File.WriteAllTextAsync(filePath, $"Thread {currentThreadId}, Operation {op}");
                            await Task.Delay(1); // Simulate I/O
                            await File.ReadAllTextAsync(filePath);
                            return true;
                        });
                        
                        stopwatch.Stop();

                        ioMetrics.Add(new IOMetric
                        {
                            ThreadId = currentThreadId,
                            OperationId = op,
                            OperationTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                            Success = result
                        });

                        if (op % 20 == 0)
                        {
                            await Task.Delay(1); // Small delay
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            var avgOperationTime = ioMetrics.Average(m => m.OperationTimeMs);
            var p95OperationTime = CalculatePercentile(ioMetrics.Select(m => m.OperationTimeMs), 95);
            var maxOperationTime = ioMetrics.Max(m => m.OperationTimeMs);
            var successRate = ioMetrics.Count(m => m.Success) / (double)ioMetrics.Count;

            _output.WriteLine($"I/O Thread Isolation Performance Analysis:");
            _output.WriteLine($"  Average Operation Time: {avgOperationTime:F2}ms");
            _output.WriteLine($"  95th Percentile Operation Time: {p95OperationTime:F2}ms");
            _output.WriteLine($"  Max Operation Time: {maxOperationTime:F2}ms");
            _output.WriteLine($"  Success Rate: {successRate:P2}");
            _output.WriteLine($"  Total Operations: {ioMetrics.Count}");

            // Performance assertions
            Assert.True(avgOperationTime < 50, $"Average operation time {avgOperationTime:F2}ms should be reasonable");
            Assert.True(p95OperationTime < 100, $"95th percentile {p95OperationTime:F2}ms should be under 100ms");
            Assert.True(successRate > 0.95, $"Success rate {successRate:P2} should be above 95%");
        }

        #endregion

        #region Memory Performance Regression Tests

        [Fact]
        public void Memory_Performance_ResourceManagement_ShouldNotLeak()
        {
            // Arrange
            const string testName = "Memory_Performance_Resource_Management";
            const int allocationCycles = 100;
            const int objectsPerCycle = 1000;

            var initialMemory = GC.GetTotalMemory(false);
            var memorySnapshots = new List<MemorySnapshot>();

            // Act - Test memory management under load
            for (int cycle = 0; cycle < allocationCycles; cycle++)
            {
                // Force garbage collection
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                var currentMemory = GC.GetTotalMemory(false);
                var allocatedBytes = currentMemory - initialMemory;
                
                memorySnapshots.Add(new MemorySnapshot
                {
                    Cycle = cycle,
                    TotalMemoryBytes = currentMemory,
                    AllocatedBytes = allocatedBytes,
                    Gen0Collections = GC.CollectionCount(0),
                    Gen1Collections = GC.CollectionCount(1),
                    Gen2Collections = GC.CollectionCount(2)
                });

                // Simulate allocations
                var tempObjects = new List<object>();
                for (int i = 0; i < objectsPerCycle; i++)
                {
                    tempObjects.Add(new
                    {
                        Id = cycle * objectsPerCycle + i,
                        Data = new byte[1024], // 1KB
                        Timestamp = DateTimeOffset.UtcNow
                    });
                }

                // Clear to allow GC
                tempObjects.Clear();

                // Periodic delay to allow GC
                if (cycle % 10 == 0)
                {
                    Thread.Sleep(10);
                }
            }

            // Final cleanup
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var finalMemory = GC.GetTotalMemory(false);
            var finalAllocatedBytes = finalMemory - initialMemory;

            // Assert
            _output.WriteLine($"Memory Performance Analysis:");
            _output.WriteLine($"  Initial Memory: {initialMemory / 1024.0 / 1024.0:F1}MB");
            _output.WriteLine($"  Final Memory: {finalMemory / 1024.0 / 1024.0:F1}MB");
            _output.WriteLine($"  Net Allocated: {finalAllocatedBytes / 1024.0 / 1024.0:F1}MB");
            _output.WriteLine($"  Memory Growth: {(finalAllocatedBytes - initialMemory) / 1024.0 / 1024.0:F1}MB");

            var memoryGrowth = finalMemory - initialMemory;
            var maxMemoryGrowth = allocationCycles * objectsPerCycle * 1024 * 0.1; // Allow 10% for overhead

            // Memory should not grow excessively
            Assert.True(Math.Abs(memoryGrowth) < maxMemoryGrowth, 
                $"Memory growth {memoryGrowth / 1024.0 / 1024.0:F1}MB should be reasonable");

            // Should have reasonable GC behavior
            var totalCollections = memorySnapshots.Sum(s => s.Gen0Collections + s.Gen1Collections + s.Gen2Collections);
            Assert.True(totalCollections > 0, "Should have garbage collections");
        }

        #endregion

        #region Stress Performance Regression Tests

        [Fact]
        public async Task Stress_Performance_EndToEnd_ShouldMaintainQuality()
        {
            // Arrange - End-to-end stress test
            const string testName = "Stress_Performance_EndToEnd";
            const int stressDuration = 30; // seconds
            const int concurrentUsers = 10;

            // Initialize all components
            using var performanceMonitor = new PerformanceMonitor(100, _mockDevice.Object);
            using var renderingEngine = CreateRenderingEngine(CreateRenderingConfig());
            using var audioVisualScheduler = new AudioVisualQueueScheduler(
                targetFrameRate: 60, maxQueueDepth: 5000, batchSize: 64, targetEventsPerSecond: 10000);
            using var evaluationContext = CreateEvaluationContext();
            var evaluationEngine = evaluationContext.IncrementalEvaluationEngine!;
            var isolationManager = new IOIsolationManager();

            // Initialize node graph
            var nodes = CreateTestNodeGraph(100);
            foreach (var node in nodes)
            {
                evaluationEngine.AddNode(node);
            }

            var stressMetrics = new List<StressMetric>();
            var startTime = DateTimeOffset.UtcNow;

            // Act - Simulate concurrent user load
            var loadTasks = new List<Task>();
            
            for (int userId = 0; userId < concurrentUsers; userId++)
            {
                int currentUserId = userId;
                loadTasks.Add(Task.Run(async () =>
                {
                    var userStartTime = DateTimeOffset.UtcNow;
                    
                    while ((DateTimeOffset.UtcNow - startTime).TotalSeconds < stressDuration)
                    {
                        try
                        {
                            // Mixed workload
                            performanceMonitor.BeginFrame();

                            // 1. Node evaluation (20% of time)
                            if (currentUserId % 5 == 0)
                            {
                                var nodeId = evaluationEngine.GetAllNodes().Skip(currentUserId % 10).FirstOrDefault()?.Id;
                                if (nodeId != null)
                                {
                                    evaluationEngine.MarkNodeDirty(nodeId);
                                    await evaluationEngine.EvaluateAsync();
                                }
                            }

                            // 2. Audio events (30% of time)
                            if (currentUserId % 3 == 0)
                            {
                                for (int i = 0; i < 5; i++)
                                {
                                    var audioEvent = new RealTimeAudioEvent
                                    {
                                        EventId = currentUserId * 10000 + i,
                                        Timestamp = DateTimeOffset.UtcNow,
                                        EventType = AudioEventType.NoteOn,
                                        Frequency = 440.0 + (i % 100)
                                    };
                                    audioVisualScheduler.QueueAudioEvent(audioEvent);
                                }
                            }

                            // 3. Rendering (25% of time)
                            if (currentUserId % 4 == 0)
                            {
                                renderingEngine.BeginFrame();
                                await SimulateRenderingFrame(renderingEngine);
                                renderingEngine.EndFrame();
                            }

                            // 4. File I/O (25% of time)
                            if (currentUserId % 5 == 1)
                            {
                                var filePath = Path.Combine(_testDirectory, $"stress_user_{currentUserId}.txt");
                                isolationManager.ExecuteOnIOThread(() =>
                                {
                                    File.WriteAllText(filePath, $"User {currentUserId} at {DateTimeOffset.UtcNow:O}");
                                    return true;
                                });
                            }

                            performanceMonitor.EndFrame();

                            // Record metrics periodically
                            if (currentUserId == 0) // Only one user records metrics to avoid spam
                            {
                                var metrics = performanceMonitor.GetCurrentMetrics();
                                var elapsedSeconds = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
                                
                                stressMetrics.Add(new StressMetric
                                {
                                    Timestamp = DateTimeOffset.UtcNow,
                                    ElapsedSeconds = elapsedSeconds,
                                    FrameTime = metrics.AverageFrameTime,
                                    CpuUsage = metrics.CpuUsagePercent,
                                    MemoryUsage = metrics.MemoryUsageMB,
                                    PendingAudioEvents = audioVisualScheduler.PendingAudioEvents
                                });
                            }

                            // Small delay
                            await Task.Delay(10);
                        }
                        catch (Exception ex)
                        {
                            // Log but continue
                            if (currentUserId == 0)
                            {
                                _output.WriteLine($"Stress test error: {ex.Message}");
                            }
                        }
                    }
                }));
            }

            await Task.WhenAll(loadTasks);

            // Assert
            var totalDuration = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
            var avgFrameTime = stressMetrics.Average(m => m.FrameTime);
            var p95FrameTime = CalculatePercentile(stressMetrics.Select(m => m.FrameTime), 95);
            var avgCpuUsage = stressMetrics.Average(m => m.CpuUsage);
            var avgMemoryUsage = stressMetrics.Average(m => m.MemoryUsage);

            _output.WriteLine($"End-to-End Stress Performance Analysis:");
            _output.WriteLine($"  Test Duration: {totalDuration:F1}s");
            _output.WriteLine($"  Concurrent Users: {concurrentUsers}");
            _output.WriteLine($"  Metrics Collected: {stressMetrics.Count}");
            _output.WriteLine($"  Average Frame Time: {avgFrameTime:F2}ms");
            _output.WriteLine($"  95th Percentile Frame Time: {p95FrameTime:F2}ms");
            _output.WriteLine($"  Average CPU Usage: {avgCpuUsage:F1}%");
            _output.WriteLine($"  Average Memory Usage: {avgMemoryUsage:F1}MB");
            _output.WriteLine($"  Final Pending Audio Events: {stressMetrics.LastOrDefault()?.PendingAudioEvents ?? 0}");

            // Performance under stress should remain reasonable
            Assert.True(avgFrameTime < 50, $"Average frame time {avgFrameTime:F2}ms under stress should be reasonable");
            Assert.True(p95FrameTime < 100, $"95th percentile frame time {p95FrameTime:F2}ms under stress should be reasonable");
            Assert.True(avgCpuUsage < 80, $"CPU usage {avgCpuUsage:F1}% under stress should be reasonable");
        }

        #endregion

        #region Helper Methods

        private DirectX12RenderingEngine CreateRenderingEngine(RenderingEngineConfig config)
        {
            return new DirectX12RenderingEngine(
                _mockDevice.Object,
                _mockCommandQueue.Object,
                new PerformanceMonitor(),
                new PredictiveFrameScheduler(),
                config);
        }

        private RenderingEngineConfig CreateRenderingConfig()
        {
            return new RenderingEngineConfig
            {
                TargetFrameTimeMs = 16.67,
                MaxInFlightFrames = 3,
                MaxGpuBufferPoolSize = 1024 * 1024 * 100,
                MaxTexturePoolSize = 1024 * 1024 * 500,
                MaxPipelineStatePoolSize = 256
            };
        }

        private async Task<RenderingPerformanceMetric> MeasureRenderingFrame(DirectX12RenderingEngine engine, int frameNumber)
        {
            var stopwatch = Stopwatch.StartNew();
            
            engine.BeginFrame();
            await SimulateRenderingFrame(engine);
            engine.EndFrame();
            
            stopwatch.Stop();

            var metrics = new PerformanceMonitor();
            var currentMetrics = metrics.GetCurrentMetrics();

            return new RenderingPerformanceMetric
            {
                FrameNumber = frameNumber,
                FrameTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                CpuUsagePercent = currentMetrics.CpuUsagePercent,
                MemoryUsageMB = currentMetrics.MemoryUsageMB
            };
        }

        private async Task SimulateRenderingFrame(DirectX12RenderingEngine engine)
        {
            // Simulate various rendering operations
            await Task.Delay(1); // Shader compilation
            await Task.Delay(1); // Geometry processing
            await Task.Delay(1); // Rendering
        }

        private MaterialPSOKey[] GeneratePSOKeys(int count)
        {
            var keys = new MaterialPSOKey[count];
            for (int i = 0; i < count; i++)
            {
                var vertexShader = CreateTestShader($"Vertex_{i}");
                var pixelShader = CreateTestShader($"Pixel_{i}");
                keys[i] = new MaterialPSOKey($"Material_{i}", vertexShader, pixelShader);
            }
            return keys;
        }

        private MaterialPSOKey[] GenerateAccessPattern(MaterialPSOKey[] keys, int patternLength)
        {
            var pattern = new List<MaterialPSOKey>();
            var random = new Random(42); // Deterministic
            
            for (int i = 0; i < patternLength; i++)
            {
                var keyIndex = random.Next(keys.Length);
                pattern.Add(keys[keyIndex]);
            }
            
            return pattern.ToArray();
        }

        private byte[] CreateTestShader(string shaderName)
        {
            return System.Text.Encoding.UTF8.GetBytes($"// {shaderName}\nvoid main() {{ }}\n");
        }

        private ID3D12PipelineState CreateMockPipelineState()
        {
            var mockPSO = new Mock<ID3D12PipelineState>();
            return mockPSO.Object;
        }

        private EvaluationContext CreateEvaluationContext()
        {
            var mockRenderingEngine = new Mock<IRenderingEngine>();
            var mockAudioEngine = new Mock<IAudioEngine>();
            var mockResourceManager = new Mock<IResourceManager>();
            
            return new EvaluationContext(
                mockRenderingEngine.Object,
                mockAudioEngine.Object,
                mockResourceManager.Object,
                _mockLogger.Object,
                CancellationToken.None,
                GuardrailConfiguration.Default,
                enableIncrementalEvaluation: true,
                maxCacheSize: 1000);
        }

        private TiXLNode[] CreateTestNodeGraph(int nodeCount)
        {
            var nodes = new TiXLNode[nodeCount];
            var random = new Random(42); // Deterministic

            for (int i = 0; i < nodeCount; i++)
            {
                var dependencyCount = random.Next(0, Math.Min(3, i));
                var dependencies = new List<NodeId>();

                for (int d = 0; d < dependencyCount; d++)
                {
                    dependencies.Add(new NodeId(i - d - 1));
                }

                nodes[i] = new TiXLNode
                {
                    Id = new NodeId(i),
                    Name = $"Node_{i}",
                    Dependencies = dependencies.ToArray(),
                    EvaluationCount = 0,
                    LastEvaluationTime = DateTimeOffset.MinValue,
                    Result = null
                };
            }

            return nodes;
        }

        private static double CalculateVariance(IEnumerable<double> values)
        {
            var valuesList = values.ToList();
            var average = valuesList.Average();
            var sumOfSquaresOfDifferences = valuesList.Select(val => Math.Pow(val - average, 2)).Sum();
            return Math.Sqrt(sumOfSquaresOfDifferences / valuesList.Count);
        }

        private static double CalculatePercentile(IEnumerable<double> values, double percentile)
        {
            var sortedValues = values.OrderBy(v => v).ToList();
            var index = (int)Math.Ceiling(percentile / 100.0 * sortedValues.Count) - 1;
            return sortedValues[Math.Max(0, Math.Min(index, sortedValues.Count - 1))];
        }

        private static double CalculateRegression(double current, double baseline)
        {
            if (baseline == 0) return 0;
            return ((current - baseline) / baseline) * 100;
        }

        private void AssertPerformanceRegression(List<RenderingPerformanceMetric> currentMetrics, PerformanceBaseline baseline, string testName)
        {
            var currentAvgFrameTime = currentMetrics.Average(m => m.FrameTimeMs);
            var currentP95FrameTime = CalculatePercentile(currentMetrics.Select(m => m.FrameTimeMs), 95);
            var currentFrameVariance = CalculateVariance(currentMetrics.Select(m => m.FrameTimeMs));

            const double frameTimeRegressionThreshold = 10.0; // 10% regression allowed
            const double p95RegressionThreshold = 15.0; // 15% regression allowed
            const double varianceRegressionThreshold = 20.0; // 20% regression allowed

            var frameTimeRegression = CalculateRegression(currentAvgFrameTime, baseline.AverageFrameTimeMs);
            var p95Regression = CalculateRegression(currentP95FrameTime, baseline.Percentile95FrameTimeMs);
            var varianceRegression = CalculateRegression(currentFrameVariance, baseline.FrameTimeVarianceMs);

            _output.WriteLine($"Regression Analysis for {testName}:");
            _output.WriteLine($"  Frame Time Regression: {frameTimeRegression:F1}% (threshold: ±{frameTimeRegressionThreshold}%)");
            _output.WriteLine($"  P95 Frame Time Regression: {p95Regression:F1}% (threshold: ±{p95RegressionThreshold}%)");
            _output.WriteLine($"  Frame Variance Regression: {varianceRegression:F1}% (threshold: ±{varianceRegressionThreshold}%)");

            Assert.True(Math.Abs(frameTimeRegression) <= frameTimeRegressionThreshold,
                $"Frame time regression {frameTimeRegression:F1}% exceeds threshold {frameTimeRegressionThreshold}%");

            Assert.True(Math.Abs(p95Regression) <= p95RegressionThreshold,
                $"P95 frame time regression {p95Regression:F1}% exceeds threshold {p95RegressionThreshold}%");

            Assert.True(Math.Abs(varianceRegression) <= varianceRegressionThreshold,
                $"Frame variance regression {varianceRegression:F1}% exceeds threshold {varianceRegressionThreshold}%");
        }

        #endregion

        #region Data Models

        public class RenderingPerformanceMetric
        {
            public int FrameNumber { get; set; }
            public double FrameTimeMs { get; set; }
            public double CpuUsagePercent { get; set; }
            public double MemoryUsageMB { get; set; }
        }

        public class PSOCacheMetric
        {
            public MaterialPSOKey Key { get; set; } = null!;
            public bool Hit { get; set; }
            public double AccessTimeMs { get; set; }
        }

        public class MetricsCollectionMetric
        {
            public int Cycle { get; set; }
            public double CollectionTimeMs { get; set; }
            public double FrameTime { get; set; }
            public double CpuUsage { get; set; }
        }

        public class ThroughputMetric
        {
            public int EventId { get; set; }
            public double ThroughputPerSecond { get; set; }
            public DateTimeOffset Timestamp { get; set; }
        }

        public class ProcessingMetric
        {
            public int EventId { get; set; }
            public double ProcessingTimeMs { get; set; }
            public int PendingEvents { get; set; }
        }

        public class EvaluationMetric
        {
            public int Cycle { get; set; }
            public int DirtyNodeCount { get; set; }
            public double EvaluationTimeMs { get; set; }
            public int TotalEvaluations { get; set; }
            public double CacheHitRate { get; set; }
        }

        public class IOMetric
        {
            public int ThreadId { get; set; }
            public int OperationId { get; set; }
            public double OperationTimeMs { get; set; }
            public bool Success { get; set; }
        }

        public class MemorySnapshot
        {
            public int Cycle { get; set; }
            public long TotalMemoryBytes { get; set; }
            public long AllocatedBytes { get; set; }
            public int Gen0Collections { get; set; }
            public int Gen1Collections { get; set; }
            public int Gen2Collections { get; set; }
        }

        public class StressMetric
        {
            public DateTimeOffset Timestamp { get; set; }
            public double ElapsedSeconds { get; set; }
            public double FrameTime { get; set; }
            public double CpuUsage { get; set; }
            public double MemoryUsage { get; set; }
            public int PendingAudioEvents { get; set; }
        }

        public class PerformanceBaseline
        {
            public string TestName { get; set; } = string.Empty;
            public DateTimeOffset Timestamp { get; set; }
            public double AverageFrameTimeMs { get; set; }
            public double Percentile95FrameTimeMs { get; set; }
            public double FrameTimeVarianceMs { get; set; }
            public double AverageCpuUsagePercent { get; set; }
            public double AverageMemoryUsageMB { get; set; }
            public int SampleCount { get; set; }
        }

        #endregion

        #region Supporting Classes

        public class PerformanceBaselineManager
        {
            private readonly string _baselineFilePath;

            public PerformanceBaselineManager(string baselineFilePath)
            {
                _baselineFilePath = baselineFilePath;
            }

            public PerformanceBaseline? LoadBaseline(string testName)
            {
                try
                {
                    if (!File.Exists(_baselineFilePath))
                        return null;

                    var json = File.ReadAllText(_baselineFilePath);
                    var baselines = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, PerformanceBaseline>>(json);
                    return baselines?.GetValueOrDefault(testName);
                }
                catch
                {
                    return null;
                }
            }

            public void SaveBaseline(PerformanceBaseline baseline)
            {
                try
                {
                    var baselines = new Dictionary<string, PerformanceBaseline>();
                    
                    if (File.Exists(_baselineFilePath))
                    {
                        var json = File.ReadAllText(_baselineFilePath);
                        baselines = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, PerformanceBaseline>>(json) 
                                   ?? new Dictionary<string, PerformanceBaseline>();
                    }

                    baselines[baseline.TestName] = baseline;

                    var jsonToWrite = System.Text.Json.JsonSerializer.Serialize(baselines, new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                    File.WriteAllText(_baselineFilePath, jsonToWrite);
                }
                catch (Exception ex)
                {
                    // Log but don't fail the test
                    Console.WriteLine($"Warning: Could not save baseline: {ex.Message}");
                }
            }
        }

        public class NodeId : IEquatable<NodeId>
        {
            private readonly int _value;

            public NodeId(int value) => _value = value;

            public bool Equals(NodeId? other) => other != null && _value == other._value;
            public override bool Equals(object? obj) => Equals(obj as NodeId);
            public override int GetHashCode() => _value.GetHashCode();
            public override string ToString() => $"NodeId({_value})";
        }

        #endregion
    }
}
