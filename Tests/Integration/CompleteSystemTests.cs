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
using TiXL.Core.Operators;
using TiXL.Core.AudioVisual;
using TiXL.Core.IO;
using TiXL.Tests.Categories;
using TiXL.Tests.Fixtures;
using TiXL.Tests.Data;

namespace TiXL.Tests.Integration
{
    /// <summary>
    /// Complete system integration tests for TiXL rendering pipeline
    /// Tests the entire workflow from node evaluation to final frame output
    /// </summary>
    [Category(TestCategories.Integration)]
    [Category(TestCategories.System)]
    public class CompleteSystemTests : CoreTestFixture
    {
        private readonly ITestOutputHelper _output;
        private readonly List<TestResult> _testResults;

        public CompleteSystemTests(ITestOutputHelper output) : base()
        {
            _output = output;
            _testResults = new List<TestResult>();
        }

        [Fact]
        public async Task EndToEnd_CompleteRenderingPipeline_FromNodeToFrame()
        {
            _output.WriteLine("Testing complete rendering pipeline from node evaluation to frame output");

            // Arrange - Initialize all major system components
            var (engine, scheduler, evaluator, resourceManager) = await InitializeCompleteSystem();
            
            try
            {
                // Create a sample node graph
                var nodeGraph = TestDataGenerator.GenerateTestNodeGraph();
                var frameCount = 60; // Test for 1 second at 60 FPS
                var frameResults = new List<FrameResult>();

                // Act - Execute complete pipeline for multiple frames
                for (int frame = 0; frame < frameCount; frame++)
                {
                    using var frameToken = engine.BeginFrame();
                    var frameStart = Stopwatch.GetTimestamp();

                    // 1. Node evaluation phase
                    var evaluationResult = await evaluator.EvaluateNodeGraphAsync(nodeGraph);
                    
                    // 2. Audio-visual scheduling
                    var audioEvents = GenerateAudioEventsForFrame(frame);
                    foreach (var evt in audioEvents)
                    {
                        scheduler.QueueAudioEvent(evt);
                    }

                    // 3. Visual parameter updates
                    var visualUpdates = GenerateVisualUpdatesForFrame(frame);
                    foreach (var update in visualUpdates)
                    {
                        scheduler.QueueVisualUpdate(update);
                    }

                    // 4. Resource management
                    var resourceOperations = GenerateResourceOperations(frame);
                    foreach (var operation in resourceOperations)
                    {
                        resourceManager.QueueResourceOperation(operation);
                    }

                    // 5. Frame processing
                    scheduler.ProcessFrame();
                    
                    // 6. GPU work submission
                    await SubmitGpuWorkAsync(engine, frame);

                    // 7. Frame completion
                    await engine.EndFrameAsync(frameToken);

                    var frameEnd = Stopwatch.GetTimestamp();
                    var frameTime = (frameEnd - frameStart) / (double)Stopwatch.Frequency * 1000;

                    frameResults.Add(new FrameResult
                    {
                        FrameNumber = frame,
                        FrameTimeMs = frameTime,
                        EvaluationTimeMs = evaluationResult.EvaluationTimeMs,
                        AudioEventsProcessed = audioEvents.Count,
                        VisualUpdatesProcessed = visualUpdates.Count,
                        ResourceOperationsCompleted = resourceOperations.Count
                    });
                }

                // Assert - Validate pipeline performance and correctness
                var avgFrameTime = frameResults.Average(r => r.FrameTimeMs);
                var avgEvaluationTime = frameResults.Average(r => r.EvaluationTimeMs);
                var totalAudioEvents = frameResults.Sum(r => r.AudioEventsProcessed);
                var totalVisualUpdates = frameResults.Sum(r => r.VisualUpdatesProcessed);

                _output.WriteLine($"Pipeline Results:");
                _output.WriteLine($"  Average Frame Time: {avgFrameTime:F2}ms");
                _output.WriteLine($"  Average Evaluation Time: {avgEvaluationTime:F2}ms");
                _output.WriteLine($"  Target FPS: 60 (Actual: {1000.0 / avgFrameTime:F1})");
                _output.WriteLine($"  Total Audio Events: {totalAudioEvents}");
                _output.WriteLine($"  Total Visual Updates: {totalVisualUpdates}");

                // Validation criteria
                avgFrameTime.Should().BeLessThan(20.0, "Frame time should maintain 60 FPS target");
                avgEvaluationTime.Should().BeLessThan(5.0, "Node evaluation should be efficient");
                totalAudioEvents.Should().BeGreaterThan(0, "Audio events should be processed");
                totalVisualUpdates.Should().BeGreaterThan(0, "Visual updates should be processed");

                // Check frame time consistency
                var frameTimeVariance = CalculateVariance(frameResults.Select(r => r.FrameTimeMs).ToList());
                frameTimeVariance.Should().BeLessThan(4.0, "Frame times should be consistent");

                _testResults.Add(new TestResult
                {
                    TestName = "CompleteRenderingPipeline",
                    Passed = true,
                    DurationMs = frameResults.Count * avgFrameTime
                });
            }
            finally
            {
                await CleanupCompleteSystem(engine, scheduler, evaluator, resourceManager);
            }
        }

        [Fact]
        public async Task EndToEnd_NodeEvaluation_WithDirectXResourceBinding()
        {
            _output.WriteLine("Testing node evaluation with DirectX resource binding");

            var (engine, scheduler, evaluator, resourceManager) = await InitializeCompleteSystem();
            
            try
            {
                // Create node graph with resource requirements
                var nodeGraph = TestDataGenerator.GenerateTestNodeGraphWithResources();
                var nodeCount = nodeGraph.Nodes.Count;

                // Act - Evaluate nodes with resource binding
                var resourceBindings = new List<ResourceBinding>();
                var evaluationTimes = new List<double>();

                for (int i = 0; i < nodeCount; i++)
                {
                    var node = nodeGraph.Nodes[i];
                    var startTime = Stopwatch.GetTimestamp();

                    // Evaluate node
                    var result = await evaluator.EvaluateNodeAsync(node);

                    // Bind resources
                    var binding = await resourceManager.BindResourcesAsync(node, result.OutputData);
                    resourceBindings.Add(binding);

                    var endTime = Stopwatch.GetTimestamp();
                    evaluationTimes.Add((endTime - startTime) / (double)Stopwatch.Frequency * 1000);
                }

                // Assert
                resourceBindings.Should().HaveCount(nodeCount);
                resourceBindings.Should().OnlyContain(b => b.IsSuccess, "All resource bindings should succeed");
                
                var avgEvalTime = evaluationTimes.Average();
                avgEvalTime.Should().BeLessThan(10.0, "Node evaluation with resource binding should be efficient");

                _testResults.Add(new TestResult
                {
                    TestName = "NodeEvaluationWithDirectXResourceBinding",
                    Passed = true,
                    DurationMs = evaluationTimes.Sum()
                });
            }
            finally
            {
                await CleanupCompleteSystem(engine, scheduler, evaluator, resourceManager);
            }
        }

        [Fact]
        public async Task EndToEnd_AudioVisualSynchronization_WithFrameRendering()
        {
            _output.WriteLine("Testing audio-visual synchronization with frame rendering");

            var (engine, scheduler, evaluator, resourceManager) = await InitializeCompleteSystem();
            
            try
            {
                var syncEvents = new List<AudioVisualSyncEvent>();
                var frameCount = 120; // 2 seconds of testing
                var targetFrameTime = 1000.0 / 60.0; // 60 FPS

                // Subscribe to sync events
                scheduler.SyncEvent += (s, e) => syncEvents.Add(e);

                // Act - Generate synchronized audio-visual content
                for (int frame = 0; frame < frameCount; frame++)
                {
                    using var frameToken = engine.BeginFrame();

                    // Generate audio events with specific timing
                    var audioEvents = GenerateSynchronizedAudioEvents(frame, frameCount);
                    foreach (var evt in audioEvents)
                    {
                        scheduler.QueueAudioEvent(evt);
                    }

                    // Generate corresponding visual updates
                    var visualUpdates = GenerateSynchronizedVisualUpdates(frame, frameCount);
                    foreach (var update in visualUpdates)
                    {
                        scheduler.QueueVisualUpdate(update);
                    }

                    // Process frame
                    scheduler.ProcessFrame();
                    
                    // Submit GPU work
                    await SubmitSynchronizedGpuWorkAsync(engine, frame);

                    await engine.EndFrameAsync(frameToken);

                    // Maintain frame rate
                    var delay = Math.Max(0, (int)(targetFrameTime - (Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency * 1000)));
                    if (delay > 0)
                    {
                        await Task.Delay(delay);
                    }
                }

                // Assert
                var syncAccuracy = CalculateSyncAccuracy(syncEvents, frameCount);
                var avgFrameTime = syncEvents.Count > 0 ? 
                    syncEvents.Average(e => e.FrameRenderTime) : 0;

                _output.WriteLine($"Synchronization Results:");
                _output.WriteLine($"  Sync Events: {syncEvents.Count}");
                _output.WriteLine($"  Sync Accuracy: {syncAccuracy:P2}");
                _output.WriteLine($"  Average Frame Time: {avgFrameTime:F2}ms");

                syncEvents.Should().NotBeEmpty("Sync events should be generated");
                syncAccuracy.Should().BeGreaterThan(0.95, "Audio-visual synchronization should be highly accurate");

                _testResults.Add(new TestResult
                {
                    TestName = "AudioVisualSynchronizationWithFrameRendering",
                    Passed = true,
                    DurationMs = frameCount * targetFrameTime
                });
            }
            finally
            {
                await CleanupCompleteSystem(engine, scheduler, evaluator, resourceManager);
            }
        }

        [Fact]
        public async Task EndToEnd_ResourceLifecycle_Management_Integration()
        {
            _output.WriteLine("Testing resource lifecycle management integration");

            var (engine, scheduler, evaluator, resourceManager) = await InitializeCompleteSystem();
            
            try
            {
                var resourceOperations = new List<ResourceOperation>();
                var frameCount = 30;
                
                // Act - Exercise complete resource lifecycle
                for (int frame = 0; frame < frameCount; frame++)
                {
                    using var frameToken = engine.BeginFrame();

                    // Resource creation
                    var createOps = GenerateResourceCreationOperations(frame);
                    foreach (var op in createOps)
                    {
                        var result = await resourceManager.CreateResourceAsync(op);
                        resourceOperations.Add(new ResourceOperation
                        {
                            Type = ResourceOperationType.Create,
                            ResourceId = result.ResourceId,
                            Success = result.IsSuccess
                        });
                    }

                    // Resource usage
                    var useOps = GenerateResourceUsageOperations(frame);
                    foreach (var op in useOps)
                    {
                        var result = await resourceManager.UseResourceAsync(op.ResourceId);
                        resourceOperations.Add(new ResourceOperation
                        {
                            Type = ResourceOperationType.Use,
                            ResourceId = op.ResourceId,
                            Success = result.IsSuccess
                        });
                    }

                    // Resource updates
                    var updateOps = GenerateResourceUpdateOperations(frame);
                    foreach (var op in updateOps)
                    {
                        var result = await resourceManager.UpdateResourceAsync(op.ResourceId, op.Data);
                        resourceOperations.Add(new ResourceOperation
                        {
                            Type = ResourceOperationType.Update,
                            ResourceId = op.ResourceId,
                            Success = result.IsSuccess
                        });
                    }

                    scheduler.ProcessFrame();
                    await engine.EndFrameAsync(frameToken);

                    // Periodic cleanup
                    if (frame % 10 == 0)
                    {
                        var cleanupResult = await resourceManager.CleanupUnusedResourcesAsync();
                        resourceOperations.Add(new ResourceOperation
                        {
                            Type = ResourceOperationType.Cleanup,
                            ResourceId = cleanupResult.CleanedCount,
                            Success = cleanupResult.IsSuccess
                        });
                    }
                }

                // Assert
                var createSuccessRate = resourceOperations
                    .Where(op => op.Type == ResourceOperationType.Create)
                    .SuccessRate();
                var usageSuccessRate = resourceOperations
                    .Where(op => op.Type == ResourceOperationType.Use)
                    .SuccessRate();
                var updateSuccessRate = resourceOperations
                    .Where(op => op.Type == ResourceOperationType.Update)
                    .SuccessRate();

                _output.WriteLine($"Resource Management Results:");
                _output.WriteLine($"  Create Success Rate: {createSuccessRate:P2}");
                _output.WriteLine($"  Usage Success Rate: {usageSuccessRate:P2}");
                _output.WriteLine($"  Update Success Rate: {updateSuccessRate:P2}");
                _output.WriteLine($"  Total Operations: {resourceOperations.Count}");

                createSuccessRate.Should().BeGreaterThan(0.95, "Resource creation should be highly reliable");
                usageSuccessRate.Should().BeGreaterThan(0.98, "Resource usage should be highly reliable");
                updateSuccessRate.Should().BeGreaterThan(0.95, "Resource updates should be highly reliable");

                _testResults.Add(new TestResult
                {
                    TestName = "ResourceLifecycleManagementIntegration",
                    Passed = true,
                    DurationMs = frameCount * (1000.0 / 60.0)
                });
            }
            finally
            {
                await CleanupCompleteSystem(engine, scheduler, evaluator, resourceManager);
            }
        }

        [Fact]
        public async Task EndToEnd_PerformanceMonitoring_Integration()
        {
            _output.WriteLine("Testing performance monitoring integration across all components");

            var (engine, scheduler, evaluator, resourceManager) = await InitializeCompleteSystem();
            var metricsCollector = new PerformanceMetricsCollector();
            
            try
            {
                // Subscribe to performance events from all components
                SubscribeToPerformanceEvents(engine, scheduler, evaluator, resourceManager, metricsCollector);

                var frameCount = 60;
                var monitoringDuration = TimeSpan.FromSeconds(2);

                // Act - Run workload while collecting comprehensive metrics
                var startTime = DateTime.UtcNow;
                for (int frame = 0; frame < frameCount; frame++)
                {
                    using var frameToken = engine.BeginFrame();

                    // Simulate realistic workload
                    var nodeGraph = TestDataGenerator.GenerateTestNodeGraph();
                    await evaluator.EvaluateNodeGraphAsync(nodeGraph);

                    var audioEvents = GenerateAudioEventsForFrame(frame);
                    foreach (var evt in audioEvents)
                    {
                        scheduler.QueueAudioEvent(evt);
                    }

                    var visualUpdates = GenerateVisualUpdatesForFrame(frame);
                    foreach (var update in visualUpdates)
                    {
                        scheduler.QueueVisualUpdate(update);
                    }

                    scheduler.ProcessFrame();
                    await SubmitGpuWorkAsync(engine, frame);
                    await engine.EndFrameAsync(frameToken);

                    // Collect frame metrics
                    metricsCollector.CollectFrameMetrics(engine.Statistics, scheduler.GetStatistics());
                }

                // Wait for monitoring period
                var elapsed = DateTime.UtcNow - startTime;
                if (elapsed < monitoringDuration)
                {
                    await Task.Delay(monitoringDuration - elapsed);
                }

                // Assert - Validate comprehensive monitoring
                var collectedMetrics = metricsCollector.GetCollectedMetrics();
                
                collectedMetrics.FrameMetrics.Should().NotBeEmpty("Frame metrics should be collected");
                collectedMetrics.ComponentMetrics.Should().HaveCount(4, "All components should report metrics");
                collectedMetrics.PerformanceAlerts.Should().NotBeNull("Performance monitoring should be active");

                var avgFps = collectedMetrics.FrameMetrics.Average(m => m.Fps);
                avgFps.Should().BeGreaterThan(55.0, "System should maintain good performance");

                _output.WriteLine($"Performance Monitoring Results:");
                _output.WriteLine($"  Collected Metrics: {collectedMetrics.FrameMetrics.Count} frame samples");
                _output.WriteLine($"  Average FPS: {avgFps:F1}");
                _output.WriteLine($"  Components Monitored: {collectedMetrics.ComponentMetrics.Count}");
                _output.WriteLine($"  Performance Alerts: {collectedMetrics.PerformanceAlerts.Count}");

                _testResults.Add(new TestResult
                {
                    TestName = "PerformanceMonitoringIntegration",
                    Passed = true,
                    DurationMs = monitoringDuration.TotalMilliseconds
                });
            }
            finally
            {
                await CleanupCompleteSystem(engine, scheduler, evaluator, resourceManager);
            }
        }

        // Helper methods for system initialization and cleanup

        private async Task<(DirectX12RenderingEngine, AudioVisualQueueScheduler, IncrementalNodeEvaluator, DirectXResourceManager)> 
            InitializeCompleteSystem()
        {
            var device = CreateMockDirectXDevice();
            var commandQueue = CreateMockCommandQueue();
            var performanceMonitor = new PerformanceMonitor();
            
            var engine = new DirectX12RenderingEngine(device, commandQueue, performanceMonitor);
            var scheduler = new AudioVisualQueueScheduler(targetFrameRate: 60, maxQueueDepth: 1000, batchSize: 64);
            var evaluator = new IncrementalNodeEvaluator(performanceMonitor);
            var resourceManager = new DirectXResourceManager(device, performanceMonitor);

            await engine.InitializeAsync();
            
            return (engine, scheduler, evaluator, resourceManager);
        }

        private async Task CleanupCompleteSystem(
            DirectX12RenderingEngine engine, 
            AudioVisualQueueScheduler scheduler, 
            IncrementalNodeEvaluator evaluator, 
            DirectXResourceManager resourceManager)
        {
            engine?.Dispose();
            scheduler?.Dispose();
            evaluator?.Dispose();
            resourceManager?.Dispose();
            
            await Task.CompletedTask;
        }

        // Mock object creation methods
        private ID3D12Device4 CreateMockDirectXDevice() => new MockD3D12Device();
        private ID3D12CommandQueue CreateMockCommandQueue() => new MockD3D12CommandQueue();

        // Data generation methods
        private List<AudioEvent> GenerateAudioEventsForFrame(int frameNumber)
        {
            var events = new List<AudioEvent>();
            var random = new Random(frameNumber);
            
            // Generate 2-5 audio events per frame
            var eventCount = random.Next(2, 6);
            for (int i = 0; i < eventCount; i++)
            {
                events.Add(new AudioEvent
                {
                    Timestamp = DateTime.UtcNow,
                    Intensity = (float)random.NextDouble(),
                    Frequency = 440.0f + (float)random.NextDouble() * 1000,
                    Priority = (AudioEventPriority)(random.Next(4)),
                    Type = AudioEventType.Beat,
                    Data = new { Frame = frameNumber, EventId = i }
                });
            }
            
            return events;
        }

        private List<VisualParameterUpdate> GenerateVisualUpdatesForFrame(int frameNumber)
        {
            var updates = new List<VisualParameterUpdate>();
            var random = new Random(frameNumber * 7);
            
            // Generate 1-3 visual updates per frame
            var updateCount = random.Next(1, 4);
            for (int i = 0; i < updateCount; i++)
            {
                updates.Add(new VisualParameterUpdate
                {
                    ParameterName = $"Param_{i}",
                    Value = (float)random.NextDouble(),
                    Timestamp = DateTime.UtcNow,
                    Priority = (AudioEventPriority)(random.Next(4))
                });
            }
            
            return updates;
        }

        private List<ResourceOperationData> GenerateResourceOperations(int frameNumber)
        {
            var operations = new List<ResourceOperationData>();
            var random = new Random(frameNumber * 13);
            
            // Generate 0-2 resource operations per frame
            var opCount = random.Next(0, 3);
            for (int i = 0; i < opCount; i++)
            {
                operations.Add(new ResourceOperationData
                {
                    ResourceId = $"Resource_{frameNumber}_{i}",
                    Type = (ResourceType)(random.Next(4)),
                    Priority = (ResourcePriority)(random.Next(3)),
                    Data = new byte[random.Next(100, 1000)]
                });
            }
            
            return operations;
        }

        // Synchronized content generation
        private List<AudioEvent> GenerateSynchronizedAudioEvents(int frame, int totalFrames)
        {
            var events = new List<AudioEvent>();
            var progress = frame / (double)totalFrames;
            
            // Create a beat that aligns with frame boundaries
            if (frame % 4 == 0) // Every 4 frames
            {
                events.Add(new AudioEvent
                {
                    Timestamp = DateTime.UtcNow,
                    Intensity = (float)(0.5 + 0.5 * Math.Sin(progress * 2 * Math.PI)),
                    Frequency = 440.0f,
                    Priority = AudioEventPriority.High,
                    Type = AudioEventType.Beat,
                    Data = new { Frame = frame, Sync = true }
                });
            }
            
            return events;
        }

        private List<VisualParameterUpdate> GenerateSynchronizedVisualUpdates(int frame, int totalFrames)
        {
            var updates = new List<VisualParameterUpdate>();
            var progress = frame / (double)totalFrames;
            
            // Create visual updates synchronized with audio
            updates.Add(new VisualParameterUpdate
            {
                ParameterName = "BeatIntensity",
                Value = (float)(0.5 + 0.5 * Math.Sin(progress * 2 * Math.PI)),
                Timestamp = DateTime.UtcNow,
                Priority = AudioEventPriority.High
            });
            
            return updates;
        }

        // Resource operation generators
        private List<ResourceCreationData> GenerateResourceCreationOperations(int frame)
        {
            return new List<ResourceCreationData>
            {
                new ResourceCreationData
                {
                    ResourceId = $"Tex_{frame}",
                    Type = ResourceType.Texture,
                    Dimensions = new { Width = 256, Height = 256 },
                    Format = "RGBA8"
                }
            };
        }

        private List<ResourceUsageData> GenerateResourceUsageOperations(int frame)
        {
            return new List<ResourceUsageData>
            {
                new ResourceUsageData
                {
                    ResourceId = $"Tex_{frame}",
                    Usage = ResourceUsage.Read
                }
            };
        }

        private List<ResourceUpdateData> GenerateResourceUpdateOperations(int frame)
        {
            return new List<ResourceUpdateData>
            {
                new ResourceUpdateData
                {
                    ResourceId = $"Tex_{frame}",
                    Data = new byte[1024],
                    Offset = 0,
                    Size = 1024
                }
            };
        }

        // GPU work submission methods
        private async Task SubmitGpuWorkAsync(DirectX12RenderingEngine engine, int frame)
        {
            await engine.SubmitGpuWorkAsync($"FrameWork_{frame}", 
                async () => await Task.Delay(1), // Simulate GPU work
                GpuTimingType.General);
        }

        private async Task SubmitSynchronizedGpuWorkAsync(DirectX12RenderingEngine engine, int frame)
        {
            await engine.SubmitGpuWorkAsync($"SyncFrameWork_{frame}", 
                async () => 
                {
                    // Simulate synchronized GPU work
                    await Task.Delay(1);
                },
                GpuTimingType.General);
        }

        // Performance monitoring setup
        private void SubscribeToPerformanceEvents(
            DirectX12RenderingEngine engine,
            AudioVisualQueueScheduler scheduler,
            IncrementalNodeEvaluator evaluator,
            DirectXResourceManager resourceManager,
            PerformanceMetricsCollector collector)
        {
            engine.EngineAlert += (s, e) => collector.RecordAlert(e);
            scheduler.PerformanceMetrics += (s, e) => collector.RecordSchedulerMetrics(e.Stats);
            scheduler.SyncEvent += (s, e) => collector.RecordSyncEvent(e);
        }

        // Utility methods
        private static double CalculateVariance(List<double> values)
        {
            if (values.Count < 2) return 0.0;
            var mean = values.Average();
            return values.Sum(x => (x - mean) * (x - mean)) / values.Count;
        }

        private static double CalculateSyncAccuracy(List<AudioVisualSyncEvent> events, int expectedFrameCount)
        {
            if (!events.Any()) return 0.0;
            
            var expectedEvents = expectedFrameCount / 4.0; // Expecting sync every 4 frames
            var actualEvents = events.Count;
            return Math.Min(1.0, actualEvents / expectedEvents);
        }

        // Data classes
        private class TestResult
        {
            public string TestName { get; set; }
            public bool Passed { get; set; }
            public double DurationMs { get; set; }
        }

        private class FrameResult
        {
            public int FrameNumber { get; set; }
            public double FrameTimeMs { get; set; }
            public double EvaluationTimeMs { get; set; }
            public int AudioEventsProcessed { get; set; }
            public int VisualUpdatesProcessed { get; set; }
            public int ResourceOperationsCompleted { get; set; }
        }

        private class ResourceOperation
        {
            public ResourceOperationType Type { get; set; }
            public string ResourceId { get; set; }
            public bool Success { get; set; }
        }

        private enum ResourceOperationType
        {
            Create,
            Use,
            Update,
            Cleanup
        }

        #region Mock Classes

        private class MockD3D12Device : ID3D12Device4
        {
            public void Dispose() { }
            // Implement required interface members with minimal functionality for testing
        }

        private class MockD3D12CommandQueue : ID3D12CommandQueue
        {
            public void Dispose() { }
            // Implement required interface members with minimal functionality for testing
        }

        #endregion
    }

    // Extension method for calculating success rate
    internal static class ResourceOperationExtensions
    {
        public static double SuccessRate(this IEnumerable<CompleteSystemTests.ResourceOperation> operations)
        {
            var ops = operations.ToList();
            if (!ops.Any()) return 1.0;
            return ops.Count(op => op.Success) / (double)ops.Count;
        }
    }
}