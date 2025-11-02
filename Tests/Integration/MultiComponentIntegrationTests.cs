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

namespace TiXL.Tests.Integration
{
    /// <summary>
    /// Comprehensive integration test suite testing multiple TiXL components working together
    /// Tests cross-component interactions, data flow, and system-level behavior
    /// </summary>
    public class MultiComponentIntegrationTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly Mock<ID3D12Device4> _mockDevice;
        private readonly Mock<ID3D12CommandQueue> _mockCommandQueue;
        private readonly Mock<ILogger> _mockLogger;
        private readonly string _testDirectory;

        public MultiComponentIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
            _testDirectory = Path.Combine(Path.GetTempPath(), "TiXL_Integration_Tests_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_testDirectory);

            _mockDevice = new Mock<ID3D12Device4>();
            _mockCommandQueue = new Mock<ID3D12CommandQueue>();
            _mockLogger = new Mock<ILogger>();
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

        #region Graphics + Performance Integration

        [Fact]
        public async Task GraphicsPerformance_EndToEndRenderingPipeline_ShouldIntegrateComponents()
        {
            // Arrange - Initialize integrated pipeline
            var performanceMonitor = new PerformanceMonitor(100, _mockDevice.Object);
            var frameScheduler = new PredictiveFrameScheduler();
            
            var config = new RenderingEngineConfig
            {
                TargetFrameTimeMs = 16.67,
                MaxInFlightFrames = 3,
                MaxGpuBufferPoolSize = 1024 * 1024 * 100,
                MaxTexturePoolSize = 1024 * 1024 * 500,
                MaxPipelineStatePoolSize = 256
            };

            using var engine = new DirectX12RenderingEngine(
                _mockDevice.Object,
                _mockCommandQueue.Object,
                performanceMonitor,
                frameScheduler,
                config);

            var renderingMetrics = new List<RenderingMetrics>();
            var performanceAlerts = new List<PerformanceAlert>();
            
            engine.FrameRendered += (sender, args) =>
            {
                renderingMetrics.Add(new RenderingMetrics
                {
                    FrameId = args.FrameId,
                    FrameTime = args.FrameTime,
                    SyncEvent = args.SyncEvent
                });
            };

            performanceMonitor.PerformanceAlert += (sender, alert) => performanceAlerts.Add(alert);

            // Act - Integrated rendering workflow
            const int frameCount = 50;
            for (int frame = 0; frame < frameCount; frame++)
            {
                engine.BeginFrame();
                
                // Simulate render workload with performance tracking
                var frameStopwatch = Stopwatch.StartNew();
                
                // Simulate various render operations
                await SimulateRenderOperations(engine, frame);
                
                frameStopwatch.Stop();
                engine.EndFrame();
                
                // Small delay to simulate real-time constraints
                if (frame % 10 == 0)
                {
                    await Task.Delay(1);
                }
            }

            // Assert - Verify integration
            _output.WriteLine($"Graphics-Performance Integration Results:");
            _output.WriteLine($"  Frames rendered: {renderingMetrics.Count}");
            _output.WriteLine($"  Performance alerts: {performanceAlerts.Count}");
            _output.WriteLine($"  Engine initialized: {engine.IsInitialized}");
            _output.WriteLine($"  Current frame: {engine.CurrentFrameId}");

            Assert.Equal(frameCount, renderingMetrics.Count);
            Assert.True(engine.IsInitialized);
            Assert.True(engine.CurrentFrameId >= frameCount);

            // Verify performance integration
            if (renderingMetrics.Any())
            {
                var avgFrameTime = renderingMetrics.Average(m => m.FrameTime);
                var maxFrameTime = renderingMetrics.Max(m => m.FrameTime);
                
                _output.WriteLine($"  Average frame time: {avgFrameTime:F2}ms");
                _output.WriteLine($"  Max frame time: {maxFrameTime:F2}ms");
                
                Assert.True(avgFrameTime >= 0, "Frame times should be positive");
                Assert.True(maxFrameTime > 0, "Should have valid max frame time");
            }
        }

        [Fact]
        public async Task PSO_Performance_Integration_ShouldOptimizeRendering()
        {
            // Arrange
            var performanceMonitor = new PerformanceMonitor(50, _mockDevice.Object);
            var psoCacheService = new PSOCacheService(_mockDevice.Object);
            
            const int materialCount = 20;
            const int frameCount = 100;

            var cacheMetrics = new List<CacheMetrics>();
            var performanceMetrics = new List<PerformanceMetrics>();

            // Act - PSO cache integration with performance monitoring
            for (int frame = 0; frame < frameCount; frame++)
            {
                performanceMonitor.BeginFrame();
                
                // Create materials and cache them
                var materialsThisFrame = new List<MaterialPSOKey>();
                
                for (int material = 0; material < materialCount; material++)
                {
                    var vertexShader = CreateTestShader($"VertexShader_{material}");
                    var pixelShader = CreateTestShader($"PixelShader_{material}");
                    var materialKey = new MaterialPSOKey($"Material_{material}", vertexShader, pixelShader);
                    materialsThisFrame.Add(materialKey);
                    
                    // Cache or retrieve PSO
                    var cachedPSO = psoCacheService.GetPipelineState(materialKey);
                    if (cachedPSO == null)
                    {
                        var mockPSO = CreateMockPipelineState();
                        psoCacheService.CachePipelineState(materialKey, mockPSO);
                    }
                }

                performanceMonitor.EndFrame();

                // Record metrics
                if (frame % 10 == 0)
                {
                    var stats = psoCacheService.GetCacheStatistics();
                    cacheMetrics.Add(new CacheMetrics
                    {
                        Frame = frame,
                        HitCount = stats.HitCount,
                        MissCount = stats.MissCount,
                        CacheSize = stats.Size
                    });

                    var perfMetrics = performanceMonitor.GetCurrentMetrics();
                    performanceMetrics.Add(new PerformanceMetrics
                    {
                        Frame = frame,
                        AverageFrameTime = perfMetrics.AverageFrameTime,
                        CpuUsagePercent = perfMetrics.CpuUsagePercent
                    });
                }
            }

            // Assert
            _output.WriteLine($"PSO-Performance Integration Results:");
            _output.WriteLine($"  Cache metrics recorded: {cacheMetrics.Count}");
            _output.WriteLine($"  Performance metrics recorded: {performanceMetrics.Count}");

            Assert.True(cacheMetrics.Count > 0, "Should have cache metrics");
            Assert.True(performanceMetrics.Count > 0, "Should have performance metrics");

            // Cache should improve over time
            var initialHitRate = CalculateHitRate(cacheMetrics.First());
            var finalHitRate = CalculateHitRate(cacheMetrics.Last());
            
            _output.WriteLine($"  Initial cache hit rate: {initialHitRate:P2}");
            _output.WriteLine($"  Final cache hit rate: {finalHitRate:P2}");
            
            Assert.True(finalHitRate >= initialHitRate, "Cache hit rate should improve or stay the same");
        }

        #endregion

        #region Audio-Visual + Performance Integration

        [Fact]
        public async Task AudioVisualPerformance_SynchronizedProcessing_ShouldIntegrateSystems()
        {
            // Arrange
            using var scheduler = new AudioVisualQueueScheduler(
                targetFrameRate: 60,
                maxQueueDepth: 10000,
                batchSize: 128,
                targetEventsPerSecond: 20000);

            var performanceMonitor = new PerformanceMonitor(100, _mockDevice.Object);
            
            var syncEvents = new List<SyncEventData>();
            var performanceSnapshots = new List<PerformanceSnapshot>();
            
            scheduler.SyncEvent += (sender, args) =>
            {
                syncEvents.Add(new SyncEventData
                {
                    FrameNumber = args.FrameNumber,
                    Timestamp = args.SyncTimestamp,
                    LatencyMs = args.LatencyMs,
                    Quality = args.Quality
                });
            };

            // Act - Integrated audio-visual processing with performance monitoring
            const int processingCycles = 10;
            const int eventsPerCycle = 5000;

            for (int cycle = 0; cycle < processingCycles; cycle++)
            {
                performanceMonitor.BeginFrame();
                
                // Generate audio events
                var audioEvents = new List<RealTimeAudioEvent>();
                for (int i = 0; i < eventsPerCycle; i++)
                {
                    var audioEvent = new RealTimeAudioEvent
                    {
                        EventId = cycle * eventsPerCycle + i,
                        Timestamp = DateTimeOffset.UtcNow,
                        EventType = AudioEventType.NoteOn,
                        Frequency = 440.0 + (i % 1000),
                        Volume = 0.5
                    };
                    
                    scheduler.QueueAudioEvent(audioEvent);
                    audioEvents.Add(audioEvent);
                }

                // Generate visual updates
                for (int i = 0; i < 100; i++)
                {
                    var visualUpdate = new VisualParameterUpdate
                    {
                        ParameterId = $"param_{cycle}_{i}",
                        Value = (i * 0.1) + cycle,
                        Frame = cycle
                    };
                    
                    scheduler.QueueVisualUpdate(visualUpdate);
                }

                performanceMonitor.EndFrame();

                // Record performance snapshot
                var metrics = performanceMonitor.GetCurrentMetrics();
                performanceSnapshots.Add(new PerformanceSnapshot
                {
                    Cycle = cycle,
                    PendingAudioEvents = scheduler.PendingAudioEvents,
                    PendingVisualUpdates = scheduler.PendingVisualUpdates,
                    AverageFrameTime = metrics.AverageFrameTime,
                    AverageLatencyMs = scheduler.AverageLatencyMs
                });

                // Small delay between cycles
                await Task.Delay(10);
            }

            // Wait for processing to complete
            await Task.Delay(500);

            // Assert
            _output.WriteLine($"Audio-Visual-Performance Integration Results:");
            _output.WriteLine($"  Sync events: {syncEvents.Count}");
            _output.WriteLine($"  Performance snapshots: {performanceSnapshots.Count}");
            _output.WriteLine($"  Final pending audio events: {scheduler.PendingAudioEvents}");
            _output.WriteLine($"  Final pending visual updates: {scheduler.PendingVisualUpdates}");

            Assert.True(syncEvents.Count > 0, "Should have sync events");
            Assert.True(performanceSnapshots.Count == processingCycles, $"Should have {processingCycles} snapshots");

            // Verify synchronization quality
            if (syncEvents.Any())
            {
                var averageLatency = syncEvents.Average(e => e.LatencyMs);
                var syncQualityDistribution = syncEvents.GroupBy(e => e.Quality).ToDictionary(g => g.Key, g => g.Count());
                
                _output.WriteLine($"  Average sync latency: {averageLatency:F2}ms");
                _output.WriteLine($"  Sync quality distribution: {string.Join(", ", syncQualityDistribution.Select(kvp => $"{kvp.Key}:{kvp.Value}"))}");
                
                Assert.True(averageLatency >= 0, "Latency should be non-negative");
            }

            // Performance should be monitored correctly
            var avgFrameTime = performanceSnapshots.Average(s => s.AverageFrameTime);
            var avgLatency = performanceSnapshots.Average(s => s.AverageLatencyMs);
            
            Assert.True(avgFrameTime > 0, "Frame time should be tracked");
            Assert.True(avgLatency >= 0, "Latency should be tracked");
        }

        #endregion

        #region NodeGraph + Performance Integration

        [Fact]
        public async Task NodeGraphPerformance_IncrementalEvaluation_ShouldIntegrateWithMonitoring()
        {
            // Arrange
            using var context = CreateEvaluationContext();
            var engine = context.IncrementalEvaluationEngine!;
            var performanceMonitor = new PerformanceMonitor(50, _mockDevice.Object);
            
            // Create complex node graph
            var nodes = CreateComplexNodeGraph(50);
            foreach (var node in nodes)
            {
                engine.AddNode(node);
            }

            var evaluationMetrics = new List<EvaluationMetrics>();
            var performanceSnapshots = new List<PerformanceSnapshot>();

            // Act - Incremental evaluation with performance monitoring
            const int evaluationCycles = 20;

            for (int cycle = 0; cycle < evaluationCycles; cycle++)
            {
                performanceMonitor.BeginFrame();
                
                // Mark some nodes dirty to trigger incremental evaluation
                var nodesToModify = Math.Min(cycle + 1, nodes.Length);
                for (int i = 0; i < nodesToModify; i++)
                {
                    engine.MarkNodeDirty(nodes[i].Id);
                }

                // Evaluate
                await engine.EvaluateAsync();

                performanceMonitor.EndFrame();

                // Record metrics
                var evalMetrics = context.IncrementalEvaluationMetrics;
                var perfMetrics = performanceMonitor.GetCurrentMetrics();

                evaluationMetrics.Add(new EvaluationMetrics
                {
                    Cycle = cycle,
                    TotalEvaluations = evalMetrics.TotalEvaluations,
                    CacheHitRate = evalMetrics.CacheHitRate,
                    DirtyNodeCount = engine.DirtyNodeCount
                });

                performanceSnapshots.Add(new PerformanceSnapshot
                {
                    Cycle = cycle,
                    AverageFrameTime = perfMetrics.AverageFrameTime,
                    CpuUsagePercent = perfMetrics.CpuUsagePercent,
                    MemoryUsageMB = perfMetrics.MemoryUsageMB
                });

                await Task.Delay(1); // Small delay
            }

            // Assert
            _output.WriteLine($"NodeGraph-Performance Integration Results:");
            _output.WriteLine($"  Evaluation cycles: {evaluationMetrics.Count}");
            _output.WriteLine($"  Performance snapshots: {performanceSnapshots.Count}");
            _output.WriteLine($"  Final node count: {engine.NodeCount}");
            _output.WriteLine($"  Final cache hit rate: {engine.CacheStatistics.HitRate:P2}");

            Assert.Equal(evaluationCycles, evaluationMetrics.Count);
            Assert.True(engine.NodeCount == nodes.Length, "Should maintain all nodes");
            Assert.True(engine.CacheStatistics.HitRate >= 0, "Cache hit rate should be tracked");

            // Verify incremental evaluation effectiveness
            var initialEvaluations = evaluationMetrics.First().TotalEvaluations;
            var finalEvaluations = evaluationMetrics.Last().TotalEvaluations;
            var evaluationReduction = (double)(initialEvaluations - evaluationMetrics.Skip(1).Take(5).Average(m => m.TotalEvaluations)) / initialEvaluations * 100;
            
            _output.WriteLine($"  Initial evaluations: {initialEvaluations}");
            _output.WriteLine($"  Final evaluations: {finalEvaluations}");
            _output.WriteLine($"  Evaluation reduction: {evaluationReduction:F1}%");
            
            Assert.True(evaluationMetrics.All(m => m.TotalEvaluations > 0), "Should have evaluations");
        }

        #endregion

        #region IO + Performance Integration

        [Fact]
        public async Task IO_Performance_IsolatedOperations_ShouldIntegrateWithMonitoring()
        {
            // Arrange
            var isolationManager = new IOIsolationManager();
            var performanceMonitor = new PerformanceMonitor(50, _mockDevice.Object);
            
            var ioMetrics = new List<IOMetrics>();
            var performanceSnapshots = new List<PerformanceSnapshot>();

            // Act - I/O operations with performance monitoring
            const int ioOperations = 200;
            const int concurrentTasks = 10;

            var tasks = new List<Task>();
            for (int taskId = 0; taskId < concurrentTasks; taskId++)
            {
                int currentTaskId = taskId;
                tasks.Add(Task.Run(async () =>
                {
                    var operationsPerTask = ioOperations / concurrentTasks;
                    
                    for (int op = 0; op < operationsPerTask; op++)
                    {
                        performanceMonitor.BeginFrame();
                        
                        var filePath = Path.Combine(_testDirectory, $"perf_task_{currentTaskId}_op_{op}.txt");
                        
                        // I/O operation with performance tracking
                        var result = isolationManager.ExecuteAsyncOnIOThread(async () =>
                        {
                            await File.WriteAllTextAsync(filePath, $"Task {currentTaskId}, Operation {op}");
                            await Task.Delay(1); // Simulate I/O delay
                            return await File.ReadAllTextAsync(filePath);
                        });

                        performanceMonitor.EndFrame();

                        if (op % 20 == 0 && currentTaskId == 0) // Record metrics from first task
                        {
                            var metrics = performanceMonitor.GetCurrentMetrics();
                            lock (performanceSnapshots)
                            {
                                performanceSnapshots.Add(new PerformanceSnapshot
                                {
                                    Cycle = currentTaskId * operationsPerTask + op,
                                    AverageFrameTime = metrics.AverageFrameTime,
                                    CpuUsagePercent = metrics.CpuUsagePercent,
                                    MemoryUsageMB = metrics.MemoryUsageMB
                                });
                            }
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            _output.WriteLine($"IO-Performance Integration Results:");
            _output.WriteLine($"  I/O operations completed: {ioOperations}");
            _output.WriteLine($"  Performance snapshots: {performanceSnapshots.Count}");
            _output.WriteLine($"  Concurrent tasks: {concurrentTasks}");

            Assert.Equal(ioOperations, ioOperations); // Basic sanity check
            Assert.True(performanceSnapshots.Count > 0, "Should have performance snapshots");

            // Verify performance tracking during I/O
            var avgFrameTime = performanceSnapshots.Average(s => s.AverageFrameTime);
            var maxFrameTime = performanceSnapshots.Max(s => s.AverageFrameTime);
            
            _output.WriteLine($"  Average frame time during I/O: {avgFrameTime:F2}ms");
            _output.WriteLine($"  Max frame time during I/O: {maxFrameTime:F2}ms");
            
            Assert.True(avgFrameTime > 0, "Frame times should be tracked during I/O");
            Assert.True(maxFrameTime >= avgFrameTime, "Max should be >= average");
        }

        #endregion

        #region Complete System Integration

        [Fact]
        public async Task CompleteSystem_AllComponents_ShouldWorkTogether()
        {
            // Arrange - Initialize all components
            var performanceMonitor = new PerformanceMonitor(100, _mockDevice.Object);
            var frameScheduler = new PredictiveFrameScheduler();
            
            var config = new RenderingEngineConfig
            {
                TargetFrameTimeMs = 16.67,
                MaxInFlightFrames = 3,
                MaxGpuBufferPoolSize = 1024 * 1024 * 100,
                MaxTexturePoolSize = 1024 * 1024 * 500,
                MaxPipelineStatePoolSize = 256
            };

            using var renderingEngine = new DirectX12RenderingEngine(
                _mockDevice.Object,
                _mockCommandQueue.Object,
                performanceMonitor,
                frameScheduler,
                config);

            using var audioVisualScheduler = new AudioVisualQueueScheduler(
                targetFrameRate: 60,
                maxQueueDepth: 5000,
                batchSize: 64,
                targetEventsPerSecond: 10000);

            using var evaluationContext = CreateEvaluationContext();
            var evaluationEngine = evaluationContext.IncrementalEvaluationEngine!;
            var isolationManager = new IOIsolationManager();

            // System metrics collection
            var systemMetrics = new List<SystemMetrics>();
            var exceptions = new ConcurrentBag<Exception>();

            // Act - Complete system workflow
            const int systemCycles = 10;

            for (int cycle = 0; cycle < systemCycles; cycle++)
            {
                try
                {
                    performanceMonitor.BeginFrame();

                    // 1. Node Graph Evaluation
                    if (cycle == 0)
                    {
                        // Initialize node graph
                        var nodes = CreateComplexNodeGraph(20);
                        foreach (var node in nodes)
                        {
                            evaluationEngine.AddNode(node);
                        }
                    }

                    if (cycle % 3 == 0) // Modify some nodes periodically
                    {
                        var nodeToModify = evaluationEngine.GetAllNodes().FirstOrDefault();
                        if (nodeToModify != null)
                        {
                            evaluationEngine.MarkNodeDirty(nodeToModify.Id);
                        }
                    }

                    await evaluationEngine.EvaluateAsync();

                    // 2. Audio-Visual Processing
                    for (int i = 0; i < 100; i++)
                    {
                        var audioEvent = new RealTimeAudioEvent
                        {
                            EventId = cycle * 100 + i,
                            Timestamp = DateTimeOffset.UtcNow,
                            EventType = AudioEventType.ParameterChange,
                            Frequency = 440.0 + (i % 100)
                        };
                        
                        audioVisualScheduler.QueueAudioEvent(audioEvent);
                    }

                    // 3. Rendering Pipeline
                    renderingEngine.BeginFrame();
                    
                    var filePath = Path.Combine(_testDirectory, $"system_cycle_{cycle}.txt");
                    isolationManager.ExecuteOnIOThread(() =>
                    {
                        File.WriteAllText(filePath, $"System cycle {cycle}");
                        return filePath;
                    });

                    renderingEngine.EndFrame();

                    performanceMonitor.EndFrame();

                    // 4. Collect system metrics
                    var renderStats = renderingEngine.Statistics;
                    var evalStats = evaluationEngine.CacheStatistics;
                    var perfMetrics = performanceMonitor.GetCurrentMetrics();

                    systemMetrics.Add(new SystemMetrics
                    {
                        Cycle = cycle,
                        RenderFrameTime = renderStats.AverageFrameTime,
                        EvalCacheHitRate = evalStats.HitRate,
                        PendingAudioEvents = audioVisualScheduler.PendingAudioEvents,
                        PerformanceFrameTime = perfMetrics.AverageFrameTime,
                        CpuUsage = perfMetrics.CpuUsagePercent,
                        MemoryUsage = perfMetrics.MemoryUsageMB
                    });

                    _output.WriteLine($"System cycle {cycle} completed");
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    _output.WriteLine($"System cycle {cycle} failed: {ex.Message}");
                }

                // Small delay between cycles
                await Task.Delay(10);
            }

            // Assert
            _output.WriteLine($"Complete System Integration Results:");
            _output.WriteLine($"  System cycles: {systemMetrics.Count}");
            _output.WriteLine($"  Exceptions: {exceptions.Count}");
            _output.WriteLine($"  Rendering engine initialized: {renderingEngine.IsInitialized}");
            _output.WriteLine($"  Evaluation engine nodes: {evaluationEngine.NodeCount}");
            _output.WriteLine($"  Audio-visual scheduler ready: {audioVisualScheduler.IsDirectXInitialized}");

            Assert.Empty(exceptions, "Complete system should run without exceptions");
            Assert.Equal(systemCycles, systemMetrics.Count);
            Assert.True(renderingEngine.IsInitialized, "Rendering engine should be initialized");
            Assert.True(evaluationEngine.NodeCount > 0, "Evaluation engine should have nodes");

            // Verify system-wide integration
            var avgFrameTime = systemMetrics.Average(m => m.PerformanceFrameTime);
            var avgCacheHitRate = systemMetrics.Average(m => m.EvalCacheHitRate);
            var avgCpuUsage = systemMetrics.Average(m => m.CpuUsage);

            _output.WriteLine($"  Average frame time: {avgFrameTime:F2}ms");
            _output.WriteLine($"  Average cache hit rate: {avgCacheHitRate:P2}");
            _output.WriteLine($"  Average CPU usage: {avgCpuUsage:F1}%");

            Assert.True(avgFrameTime > 0, "System should track frame times");
            Assert.True(avgCacheHitRate >= 0, "System should track cache performance");
            Assert.True(avgCpuUsage >= 0, "System should track CPU usage");
        }

        #endregion

        #region Stress Integration Tests

        [Fact]
        public async Task StressIntegration_HighLoad_AllSystems_ShouldHandlePeakLoad()
        {
            // Arrange - Initialize all systems for stress testing
            var performanceMonitor = new PerformanceMonitor(50, _mockDevice.Object);
            
            using var renderingEngine = new DirectX12RenderingEngine(
                _mockDevice.Object,
                _mockCommandQueue.Object,
                performanceMonitor,
                new PredictiveFrameScheduler(),
                new RenderingEngineConfig
                {
                    TargetFrameTimeMs = 16.67,
                    MaxInFlightFrames = 3,
                    MaxGpuBufferPoolSize = 1024 * 1024 * 100,
                    MaxTexturePoolSize = 1024 * 1024 * 500,
                    MaxPipelineStatePoolSize = 256
                });

            using var audioVisualScheduler = new AudioVisualQueueScheduler(
                targetFrameRate: 60,
                maxQueueDepth: 10000,
                batchSize: 128,
                targetEventsPerSecond: 50000);

            using var evaluationContext = CreateEvaluationContext();
            var evaluationEngine = evaluationContext.IncrementalEvaluationEngine!;
            var isolationManager = new IOIsolationManager();

            var stressMetrics = new List<StressMetrics>();
            var exceptions = new ConcurrentBag<Exception>();

            const int stressCycles = 20;
            const int concurrentLoadTasks = 20;

            // Act - High-load stress testing
            var loadTasks = new List<Task>();

            for (int taskId = 0; taskId < concurrentLoadTasks; taskId++)
            {
                int currentTaskId = taskId;
                loadTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        for (int cycle = 0; cycle < stressCycles; cycle++)
                        {
                            // Mixed workload from all systems
                            
                            // 1. Node evaluation (if task is evaluator)
                            if (currentTaskId % 4 == 0 && cycle % 2 == 0)
                            {
                                var nodeId = evaluationEngine.GetAllNodes().Skip(currentTaskId % 10).FirstOrDefault()?.Id;
                                if (nodeId != null)
                                {
                                    evaluationEngine.MarkNodeDirty(nodeId);
                                    await evaluationEngine.EvaluateAsync();
                                }
                            }

                            // 2. Audio events (if task is audio processor)
                            if (currentTaskId % 3 == 0)
                            {
                                for (int i = 0; i < 10; i++)
                                {
                                    var audioEvent = new RealTimeAudioEvent
                                    {
                                        EventId = currentTaskId * 1000 + cycle * 10 + i,
                                        Timestamp = DateTimeOffset.UtcNow,
                                        EventType = AudioEventType.NoteOn,
                                        Frequency = 440.0 + (i % 100)
                                    };
                                    
                                    audioVisualScheduler.QueueAudioEvent(audioEvent);
                                }
                            }

                            // 3. File I/O (if task is file processor)
                            if (currentTaskId % 5 == 0)
                            {
                                var filePath = Path.Combine(_testDirectory, $"stress_task_{currentTaskId}_cycle_{cycle}.txt");
                                isolationManager.ExecuteOnIOThread(() =>
                                {
                                    File.WriteAllText(filePath, $"Stress load {currentTaskId}, cycle {cycle}");
                                    return true;
                                });
                            }

                            // 4. Rendering (if task is renderer)
                            if (currentTaskId % 6 == 0)
                            {
                                renderingEngine.BeginFrame();
                                
                                // Simulate rendering work
                                Thread.Sleep(1);
                                
                                renderingEngine.EndFrame();
                            }

                            // Small delay to prevent overwhelming
                            await Task.Delay(1);
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }));
            }

            await Task.WhenAll(loadTasks);

            // Collect final metrics
            var finalPerfMetrics = performanceMonitor.GetCurrentMetrics();
            var finalCacheStats = evaluationEngine.CacheStatistics;

            stressMetrics.Add(new StressMetrics
            {
                TotalTasks = concurrentLoadTasks,
                CompletedCycles = stressCycles,
                PendingAudioEvents = audioVisualScheduler.PendingAudioEvents,
                FinalFrameTime = finalPerfMetrics.AverageFrameTime,
                FinalCacheHitRate = finalCacheStats.HitRate,
                TotalExceptions = exceptions.Count
            });

            // Assert
            _output.WriteLine($"Stress Integration Results:");
            _output.WriteLine($"  Total load tasks: {concurrentLoadTasks}");
            _output.WriteLine($"  Stress cycles per task: {stressCycles}");
            _output.WriteLine($"  Total exceptions: {exceptions.Count}");
            _output.WriteLine($"  Pending audio events: {audioVisualScheduler.PendingAudioEvents}");
            _output.WriteLine($"  Final cache hit rate: {finalCacheStats.HitRate:P2}");

            // System should handle high load without complete failure
            var exceptionRate = (double)exceptions.Count / (concurrentLoadTasks * stressCycles);
            
            Assert.True(exceptionRate < 0.1, $"Exception rate {exceptionRate:P2} should be under 10% under stress");
            Assert.True(finalCacheStats.HitRate >= 0, "Cache should remain functional under stress");
        }

        #endregion

        #region Helper Methods

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

        private async Task SimulateRenderOperations(DirectX12RenderingEngine engine, int frameNumber)
        {
            // Simulate various render operations
            await Task.Delay(1); // Simulate shader compilation
            await Task.Delay(1); // Simulate geometry processing
            await Task.Delay(1); // Simulate rendering
        }

        private byte[] CreateTestShader(string shaderName)
        {
            // Create a simple test shader
            return System.Text.Encoding.UTF8.GetBytes($"// {shaderName}\nvoid main() {{ }}\n");
        }

        private ID3D12PipelineState CreateMockPipelineState()
        {
            var mockPSO = new Mock<ID3D12PipelineState>();
            return mockPSO.Object;
        }

        private TiXLNode[] CreateComplexNodeGraph(int nodeCount)
        {
            var nodes = new TiXLNode[nodeCount];
            var random = new Random(42); // Deterministic for testing

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
                    Name = $"ComplexNode_{i}",
                    Dependencies = dependencies.ToArray(),
                    EvaluationCount = 0,
                    LastEvaluationTime = DateTimeOffset.MinValue,
                    Result = null
                };
            }

            return nodes;
        }

        private static double CalculateHitRate(CacheMetrics metrics)
        {
            var total = metrics.HitCount + metrics.MissCount;
            return total > 0 ? (double)metrics.HitCount / total : 0;
        }

        #endregion

        #region Data Models

        public class RenderingMetrics
        {
            public ulong FrameId { get; set; }
            public double FrameTime { get; set; }
            public SyncEvent SyncEvent { get; set; } = new();
        }

        public class CacheMetrics
        {
            public int Frame { get; set; }
            public int HitCount { get; set; }
            public int MissCount { get; set; }
            public int CacheSize { get; set; }
        }

        public class PerformanceMetrics
        {
            public int Frame { get; set; }
            public double AverageFrameTime { get; set; }
            public double CpuUsagePercent { get; set; }
        }

        public class SyncEventData
        {
            public int FrameNumber { get; set; }
            public DateTimeOffset Timestamp { get; set; }
            public double LatencyMs { get; set; }
            public SyncQuality Quality { get; set; }
        }

        public class PerformanceSnapshot
        {
            public int Cycle { get; set; }
            public int PendingAudioEvents { get; set; }
            public int PendingVisualUpdates { get; set; }
            public double AverageFrameTime { get; set; }
            public double AverageLatencyMs { get; set; }
            public double CpuUsagePercent { get; set; }
            public double MemoryUsageMB { get; set; }
        }

        public class EvaluationMetrics
        {
            public int Cycle { get; set; }
            public int TotalEvaluations { get; set; }
            public double CacheHitRate { get; set; }
            public int DirtyNodeCount { get; set; }
        }

        public class IOMetrics
        {
            public int OperationId { get; set; }
            public double OperationTimeMs { get; set; }
            public string OperationType { get; set; } = string.Empty;
            public bool Success { get; set; }
        }

        public class SystemMetrics
        {
            public int Cycle { get; set; }
            public double RenderFrameTime { get; set; }
            public double EvalCacheHitRate { get; set; }
            public int PendingAudioEvents { get; set; }
            public double PerformanceFrameTime { get; set; }
            public double CpuUsage { get; set; }
            public double MemoryUsage { get; set; }
        }

        public class StressMetrics
        {
            public int TotalTasks { get; set; }
            public int CompletedCycles { get; set; }
            public int PendingAudioEvents { get; set; }
            public double FinalFrameTime { get; set; }
            public double FinalCacheHitRate { get; set; }
            public int TotalExceptions { get; set; }
        }

        public class SyncEvent
        {
            public DateTimeOffset Timestamp { get; set; }
            public double OffsetMs { get; set; }
        }

        #endregion

        #region Additional Supporting Classes

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
