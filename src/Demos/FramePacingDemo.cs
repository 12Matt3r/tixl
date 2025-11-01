using System;
using System.Threading.Tasks;
using TiXL.Core.Graphics.DirectX12;
using TiXL.Core.Performance;

namespace TiXL.Demos
{
    /// <summary>
    /// Demonstration of TIXL-021 Frame Pacing and CPU-GPU Synchronization
    /// Shows how to use the DirectX 12 rendering engine with frame pacing optimizations
    /// </summary>
    public class FramePacingDemo
    {
        private DirectX12RenderingEngine _engine;
        private int _demoFrameCount = 100;
        
        public static async Task Main(string[] args)
        {
            var demo = new FramePacingDemo();
            
            Console.WriteLine("=== TIXL-021 Frame Pacing Demo ===");
            Console.WriteLine("Demonstrating DirectX 12 frame pacing and CPU-GPU synchronization");
            Console.WriteLine();
            
            try
            {
                await demo.RunDemoAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Demo failed: {ex.Message}");
            }
        }
        
        public async Task RunDemoAsync()
        {
            // Initialize performance monitoring
            var performanceMonitor = new PerformanceMonitor();
            var frameScheduler = new PredictiveFrameScheduler();
            
            // Configure engine for optimal performance
            var config = new RenderingEngineConfig
            {
                EnableGpuProfiling = true,
                PrecreateResourcePools = true,
                MaxGpuBufferPoolSize = 16,
                MaxTexturePoolSize = 8,
                MaxPipelineStatePoolSize = 4,
                EnableAutoOptimization = true,
                TargetFrameTimeMs = 16.67,
                MaxInFlightFrames = 3
            };
            
            // Create engine
            _engine = new DirectX12RenderingEngine(performanceMonitor, frameScheduler, config);
            
            // Subscribe to events
            _engine.EngineAlert += OnEngineAlert;
            _engine.FrameRendered += OnFrameRendered;
            
            // Initialize engine
            Console.WriteLine("Initializing DirectX 12 Rendering Engine...");
            var initialized = await _engine.InitializeAsync();
            
            if (!initialized)
            {
                Console.WriteLine("Failed to initialize engine");
                return;
            }
            
            Console.WriteLine("Engine initialized successfully!");
            Console.WriteLine();
            
            // Run demo rendering loop
            await RunFramePacingDemoAsync();
            
            // Show final statistics
            ShowFinalStatistics();
            
            // Clean up
            _engine.Dispose();
        }
        
        private async Task RunFramePacingDemoAsync()
        {
            Console.WriteLine("Running frame pacing demo...");
            Console.WriteLine("Target: 60 FPS with strict budget management");
            Console.WriteLine();
            
            var frameTimes = new System.Collections.Generic.List<double>();
            var startTime = DateTime.UtcNow;
            
            for (int frame = 0; frame < _demoFrameCount; frame++)
            {
                using (var frameToken = _engine.BeginFrame())
                {
                    var frameStart = DateTime.UtcNow;
                    
                    try
                    {
                        // Demonstrate various GPU operations with automatic timing
                        await _engine.SubmitGpuWorkAsync("UpdateTransforms", 
                            async () => await SimulateGpuOperation("Transform Update", 1.5), 
                            GpuTimingType.General);
                        
                        await _engine.SubmitGpuWorkAsync("RenderScene", 
                            async () => await SimulateGpuOperation("Scene Rendering", 8.0), 
                            GpuTimingType.VertexProcessing);
                        
                        await _engine.SubmitGpuWorkAsync("LightingPass", 
                            async () => await SimulateGpuOperation("Lighting", 4.5), 
                            GpuTimingType.PixelProcessing);
                        
                        await _engine.SubmitGpuWorkAsync("PostProcessing", 
                            async () => await SimulateGpuOperation("Post FX", 2.0), 
                            GpuTimingType.PostProcess);
                        
                        // Queue resource operations with different priorities
                        _engine.QueueResourceOperation(() => SimulateResourceOperation("UploadTexture", ResourcePriority.High), ResourcePriority.High);
                        _engine.QueueResourceOperation(() => SimulateResourceOperation("UpdateBuffer", ResourcePriority.Normal), ResourcePriority.Normal);
                        _engine.QueueResourceOperation(() => SimulateResourceOperation("Cleanup", ResourcePriority.Low), ResourcePriority.Low);
                        
                        // End frame with fence-based synchronization
                        await _engine.EndFrameAsync(frameToken);
                        
                        var frameEnd = DateTime.UtcNow;
                        var frameTime = (frameEnd - frameStart).TotalMilliseconds;
                        frameTimes.Add(frameTime);
                        
                        // Show progress every 10 frames
                        if (frame % 10 == 0)
                        {
                            ShowFrameProgress(frame, frameTime);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Frame {frame} failed: {ex.Message}");
                    }
                }
            }
            
            var totalTime = (DateTime.UtcNow - startTime).TotalSeconds;
            var averageFrameTime = frameTimes.Average();
            var achievedFps = _demoFrameCount / totalTime;
            
            Console.WriteLine();
            Console.WriteLine("Frame pacing demo completed!");
            Console.WriteLine($"Total frames: {_demoFrameCount}");
            Console.WriteLine($"Total time: {totalTime:F2}s");
            Console.WriteLine($"Average frame time: {averageFrameTime:F2}ms");
            Console.WriteLine($"Achieved FPS: {achievedFps:F1}");
            Console.WriteLine($"Target FPS: 60");
            Console.WriteLine($"Performance: {(achievedFps >= 55 ? "GOOD" : "NEEDS IMPROVEMENT")}");
        }
        
        private async Task SimulateGpuOperation(string name, double durationMs)
        {
            // Simulate realistic GPU processing time
            var random = new Random();
            var actualDuration = durationMs * (0.8 + random.NextDouble() * 0.4); // ±20% variance
            
            // Simulate GPU work (scaled down for demo)
            await Task.Delay(TimeSpan.FromMilliseconds(actualDuration / 10.0));
        }
        
        private void SimulateResourceOperation(string name, ResourcePriority priority)
        {
            // Simulate resource operation time based on priority
            var duration = priority switch
            {
                ResourcePriority.High => 2.0,
                ResourcePriority.Normal => 1.0,
                ResourcePriority.Low => 0.5,
                _ => 1.0
            };
            
            System.Threading.Thread.Sleep(System.TimeSpan.FromMilliseconds(duration / 10.0));
        }
        
        private void ShowFrameProgress(int frame, double frameTime)
        {
            var fps = 1000.0 / frameTime;
            var budgetCompliance = frameTime <= 16.67 ? "✓" : "⚠";
            
            Console.WriteLine($"Frame {frame:D3}: {frameTime:F2}ms ({fps:F1} FPS) {budgetCompliance}");
        }
        
        private void OnFrameRendered(object sender, RenderFrameEventArgs e)
        {
            // Show detailed statistics every 25 frames
            if (e.FrameId % 25 == 0 && e.FrameId > 0)
            {
                Console.WriteLine();
                Console.WriteLine("--- Performance Statistics ---");
                Console.WriteLine($"Frame ID: {e.FrameId}");
                Console.WriteLine($"Frame Time: {e.FrameTime:F2}ms");
                Console.WriteLine($"FPS: {e.Fps:F1}");
                Console.WriteLine($"Health: {(e.IsHealthy ? "✓ Good" : "⚠ Issues")}");
                
                if (e.Statistics != null)
                {
                    Console.WriteLine($"In-Flight Frames: {e.Statistics.FramePacing.InFlightFrameCount}");
                    Console.WriteLine($"Budget Compliance: {e.Statistics.FramePacing.FrameBudgetComplianceRate:P2}");
                    Console.WriteLine($"GPU Timeline Entries: {e.Statistics.FramePacing.GpuTimelineEntryCount}");
                    Console.WriteLine($"System Health: {e.Statistics.GetSystemHealth()}");
                    
                    if (e.Statistics.Performance != null)
                    {
                        Console.WriteLine($"Performance Grade: {e.Statistics.Performance.PerformanceGrade}");
                    }
                }
                Console.WriteLine("-----------------------------");
                Console.WriteLine();
            }
        }
        
        private void OnEngineAlert(object sender, EngineAlertEventArgs e)
        {
            var timestamp = e.Timestamp.ToString("HH:mm:ss.fff");
            var severity = e.Severity.ToString().ToUpper();
            
            Console.WriteLine($"[{timestamp}] [{severity}] {e.AlertType}: {e.Message}");
            
            if (e.AlertType == EngineAlertType.FrameBudgetExceeded)
            {
                Console.WriteLine("  → Frame budget exceeded! Consider reducing workload or quality settings.");
            }
            else if (e.AlertType == EngineAlertType.PerformanceOptimization)
            {
                Console.WriteLine("  → Engine performing optimization to improve performance.");
            }
            else if (e.AlertType == EngineAlertType.OptimizationError)
            {
                Console.WriteLine("  → Optimization failed - continuing with current settings.");
            }
        }
        
        private void ShowFinalStatistics()
        {
            Console.WriteLine();
            Console.WriteLine("=== Final Engine Statistics ===");
            
            var stats = _engine.GetStatistics();
            
            if (stats != null)
            {
                Console.WriteLine("Frame Pacing:");
                Console.WriteLine($"  In-Flight Frames: {stats.FramePacing.InFlightFrameCount}");
                Console.WriteLine($"  Pending Fences: {stats.FramePacing.PendingFenceCount}");
                Console.WriteLine($"  Current Frame Time: {stats.FramePacing.CurrentFrameTime:F2}ms");
                Console.WriteLine($"  Budget Compliance: {stats.FramePacing.FrameBudgetComplianceRate:P2}");
                Console.WriteLine($"  Performance Healthy: {stats.FramePacing.IsPerformanceHealthy}");
                
                Console.WriteLine();
                Console.WriteLine("Resource Management:");
                Console.WriteLine($"  Pending Operations: {stats.ResourceManagement.PendingOperations}");
                Console.WriteLine($"  Active Pools: {stats.ResourceManagement.ActivePools}");
                Console.WriteLine($"  Total Pooled Resources: {stats.ResourceManagement.TotalPooledResources}");
                
                Console.WriteLine();
                Console.WriteLine("GPU Profiling:");
                Console.WriteLine($"  Active Queries: {stats.GpuProfiling.ActiveQueryCount}");
                Console.WriteLine($"  Timeline Entries: {stats.GpuProfiling.TimelineEntryCount}");
                Console.WriteLine($"  Average GPU Time: {stats.GpuProfiling.AverageGpuTime:F2}ms");
                Console.WriteLine($"  GPU Utilization: {stats.GpuProfiling.GpuUtilization:F1}%");
                
                Console.WriteLine();
                Console.WriteLine("Performance:");
                if (stats.Performance != null)
                {
                    Console.WriteLine($"  Average Frame Time: {stats.Performance.AverageFrameTime:F2}ms");
                    Console.WriteLine($"  Average FPS: {stats.Performance.AverageFps:F1}");
                    Console.WriteLine($"  Performance Grade: {stats.Performance.PerformanceGrade}");
                    Console.WriteLine($"  Dropped Frames: {stats.Performance.DroppedFrames}");
                }
                
                Console.WriteLine();
                Console.WriteLine($"System Health: {stats.GetSystemHealth()}");
                Console.WriteLine($"Overall FPS: {stats.OverallFps:F1}");
            }
            
            // Show GPU timeline analysis
            Console.WriteLine();
            Console.WriteLine("=== GPU Timeline Analysis ===");
            var timelineAnalysis = _engine.GetGpuTimelineAnalysis(20);
            
            if (timelineAnalysis != null)
            {
                Console.WriteLine($"Average GPU Time: {timelineAnalysis.AverageGpuTime:F2}ms");
                Console.WriteLine($"GPU Time Variance: {timelineAnalysis.GpuTimeVariance:F2}");
                Console.WriteLine($"Performance Grade: {timelineAnalysis.PerformanceGrade}");
                Console.WriteLine($"GPU Utilization: {timelineAnalysis.GpuUtilization:F1}%");
                
                Console.WriteLine("Operation Breakdown:");
                foreach (var kvp in timelineAnalysis.OperationBreakdown)
                {
                    Console.WriteLine($"  {kvp.Key}: {kvp.Value:F2}ms average");
                }
            }
            
            // Show GPU utilization analysis
            Console.WriteLine();
            Console.WriteLine("=== GPU Utilization Analysis ===");
            var utilizationAnalysis = _engine.GetGpuUtilizationAnalysis(20);
            
            if (utilizationAnalysis != null)
            {
                Console.WriteLine($"Average Utilization: {utilizationAnalysis.AverageUtilization:F1}%");
                Console.WriteLine($"Min Utilization: {utilizationAnalysis.MinUtilization:F1}%");
                Console.WriteLine($"Max Utilization: {utilizationAnalysis.MaxUtilization:F1}%");
                Console.WriteLine($"Utilization Variance: {utilizationAnalysis.UtilizationVariance:F2}");
                Console.WriteLine($"Performance Spikes: {utilizationAnalysis.Spikes}");
                
                if (utilizationAnalysis.Bottlenecks.Count > 0)
                {
                    Console.WriteLine("Detected Bottlenecks:");
                    foreach (var bottleneck in utilizationAnalysis.Bottlenecks)
                    {
                        Console.WriteLine($"  • {bottleneck}");
                    }
                }
            }
            
            Console.WriteLine("================================");
        }
    }
}
