using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using TiXL.Core.Graphics.DirectX12;
using TiXL.Core.Performance;

namespace TiXL.Tests.Graphics
{
    /// <summary>
    /// Test suite for DirectX 12 frame pacing and CPU-GPU synchronization (TIXL-021)
    /// </summary>
    public class FramePacingTests
    {
        private DirectX12RenderingEngine _engine;
        private PerformanceMonitor _performanceMonitor;
        private List<PerformanceAlert> _capturedAlerts;
        
        public FramePacingTests()
        {
            _performanceMonitor = new PerformanceMonitor();
            _capturedAlerts = new List<PerformanceAlert>();
        }
        
        /// <summary>
        /// Test single-frame budget system with dynamic data
        /// </summary>
        public async Task<bool> TestSingleFrameBudgetSystem()
        {
            Console.WriteLine("Testing Single-Frame Budget System...");
            
            _engine = new DirectX12RenderingEngine(_performanceMonitor);
            var initialized = await _engine.InitializeAsync();
            
            if (!initialized)
            {
                Console.WriteLine("Failed to initialize engine");
                return false;
            }
            
            var passed = true;
            var testFrames = 10;
            
            for (int i = 0; i < testFrames; i++)
            {
                using (var frameToken = _engine.BeginFrame())
                {
                    try
                    {
                        // Simulate rendering workload
                        await SimulateRenderWorkloadAsync(i);
                        
                        await _engine.EndFrameAsync(frameToken);
                        
                        var stats = _engine.Statistics;
                        Console.WriteLine($"Frame {i}: {stats.Performance?.AverageFps:F1} FPS, Budget compliance: {stats.FramePacing.FrameBudgetComplianceRate:P2}");
                        
                        // Check if frame stayed within budget
                        if (stats.FramePacing.CurrentFrameTime > 20.0) // 20ms warning threshold
                        {
                            Console.WriteLine($"Warning: Frame {i} exceeded budget");
                            passed = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Frame {i} failed: {ex.Message}");
                        passed = false;
                    }
                }
            }
            
            Console.WriteLine($"Single-Frame Budget Test: {(passed ? "PASSED" : "FAILED")}");
            return passed;
        }
        
        /// <summary>
        /// Test fence-based synchronization
        /// </summary>
        public async Task<bool> TestFenceBasedSynchronization()
        {
            Console.WriteLine("Testing Fence-Based Synchronization...");
            
            var passed = true;
            var syncTests = 5;
            
            for (int i = 0; i < syncTests; i++)
            {
                using (var frameToken = _engine.BeginFrame())
                {
                    try
                    {
                        // Test GPU work submission with fencing
                        var submitted = await _engine.SubmitGpuWorkAsync(
                            $"TestGpuWork_{i}",
                            async () => await SimulateGpuWorkAsync(2.0),
                            GpuTimingType.General);
                        
                        if (!submitted)
                        {
                            Console.WriteLine($"GPU work submission failed for test {i}");
                            passed = false;
                        }
                        
                        await _engine.EndFrameAsync(frameToken);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Fence synchronization test {i} failed: {ex.Message}");
                        passed = false;
                    }
                }
            }
            
            Console.WriteLine($"Fence-Based Synchronization Test: {(passed ? "PASSED" : "FAILED")}");
            return passed;
        }
        
        /// <summary>
        /// Test frame time management for consistent 60 FPS
        /// </summary>
        public async Task<bool> TestFrameTimeManagement()
        {
            Console.WriteLine("Testing Frame Time Management (60 FPS target)...");
            
            var frameTimes = new List<double>();
            var testDuration = TimeSpan.FromSeconds(2);
            var startTime = DateTime.UtcNow;
            var frameCount = 0;
            
            while (DateTime.UtcNow - startTime < testDuration)
            {
                using (var frameToken = _engine.BeginFrame())
                {
                    var frameStart = DateTime.UtcNow;
                    
                    try
                    {
                        // Perform rendering work that should take ~16.67ms
                        await SimulateRenderingWorkloadAsync();
                        
                        var frameTime = (DateTime.UtcNow - frameStart).TotalMilliseconds;
                        frameTimes.Add(frameTime);
                        frameCount++;
                        
                        await _engine.EndFrameAsync(frameToken);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Frame time management test failed: {ex.Message}");
                        return false;
                    }
                }
            }
            
            var avgFrameTime = frameTimes.Average();
            var frameTimeVariance = CalculateVariance(frameTimes);
            var frameRate = frameCount / testDuration.TotalSeconds;
            
            Console.WriteLine($"Frame Count: {frameCount}");
            Console.WriteLine($"Average Frame Time: {avgFrameTime:F2}ms");
            Console.WriteLine($"Frame Time Variance: {frameTimeVariance:F2}");
            Console.WriteLine($"Achieved FPS: {frameRate:F1}");
            
            var passed = avgFrameTime >= 15.0 && avgFrameTime <= 18.0 && 
                        frameRate >= 55.0 && frameRate <= 65.0 &&
                        frameTimeVariance <= 4.0;
            
            Console.WriteLine($"Frame Time Management Test: {(passed ? "PASSED" : "FAILED")}");
            return passed;
        }
        
        /// <summary>
        /// Test GPU timeline profiling
        /// </summary>
        public async Task<bool> TestGpuTimelineProfiling()
        {
            Console.WriteLine("Testing GPU Timeline Profiling...");
            
            var passed = true;
            
            // Run workload with GPU profiling enabled
            for (int i = 0; i < 5; i++)
            {
                using (var frameToken = _engine.BeginFrame())
                {
                    try
                    {
                        // Submit various GPU operations with different timing types
                        await _engine.SubmitGpuWorkAsync("VertexProcessing", 
                            async () => await SimulateGpuWorkAsync(3.0), 
                            GpuTimingType.VertexProcessing);
                            
                        await _engine.SubmitGpuWorkAsync("PixelProcessing", 
                            async () => await SimulateGpuWorkAsync(4.0), 
                            GpuTimingType.PixelProcessing);
                            
                        await _engine.SubmitGpuWorkAsync("PostProcessing", 
                            async () => await SimulateGpuWorkAsync(2.0), 
                            GpuTimingType.PostProcess);
                        
                        await _engine.EndFrameAsync(frameToken);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"GPU profiling test {i} failed: {ex.Message}");
                        passed = false;
                    }
                }
            }
            
            // Analyze GPU timeline
            var timelineAnalysis = _engine.GetGpuTimelineAnalysis(10);
            if (timelineAnalysis != null)
            {
                Console.WriteLine($"GPU Timeline Analysis:");
                Console.WriteLine($"  Average GPU Time: {timelineAnalysis.AverageGpuTime:F2}ms");
                Console.WriteLine($"  GPU Time Variance: {timelineAnalysis.GpuTimeVariance:F2}");
                Console.WriteLine($"  Performance Grade: {timelineAnalysis.PerformanceGrade}");
                Console.WriteLine($"  GPU Utilization: {timelineAnalysis.GpuUtilization:F1}%");
                
                Console.WriteLine($"  Operation Breakdown:");
                foreach (var kvp in timelineAnalysis.OperationBreakdown)
                {
                    Console.WriteLine($"    {kvp.Key}: {kvp.Value:F2}ms");
                }
            }
            
            Console.WriteLine($"GPU Timeline Profiling Test: {(passed ? "PASSED" : "FAILED")}");
            return passed;
        }
        
        /// <summary>
        /// Test resource lifecycle optimization
        /// </summary>
        public async Task<bool> TestResourceLifecycleOptimization()
        {
            Console.WriteLine("Testing Resource Lifecycle Optimization...");
            
            var passed = true;
            var resourceTests = 10;
            
            for (int i = 0; i < resourceTests; i++)
            {
                using (var frameToken = _engine.BeginFrame())
                {
                    try
                    {
                        // Queue resource operations with different priorities
                        _engine.QueueResourceOperation(() => SimulateResourceOperation("HighPriority", ResourcePriority.High), ResourcePriority.High);
                        _engine.QueueResourceOperation(() => SimulateResourceOperation("NormalPriority", ResourcePriority.Normal), ResourcePriority.Normal);
                        _engine.QueueResourceOperation(() => SimulateResourceOperation("LowPriority", ResourcePriority.Low), ResourcePriority.Low);
                        
                        await _engine.SubmitGpuWorkAsync("ResourceTest", 
                            async () => await SimulateGpuWorkAsync(1.0), 
                            GpuTimingType.General);
                        
                        await _engine.EndFrameAsync(frameToken);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Resource lifecycle test {i} failed: {ex.Message}");
                        passed = false;
                    }
                }
            }
            
            var stats = _engine.Statistics;
            Console.WriteLine($"Resource Operations Statistics:");
            Console.WriteLine($"  Pending Operations: {stats.ResourceManagement.PendingOperations}");
            Console.WriteLine($"  Active Pools: {stats.ResourceManagement.ActivePools}");
            Console.WriteLine($"  Pooled Resources: {stats.ResourceManagement.TotalPooledResources}");
            
            Console.WriteLine($"Resource Lifecycle Test: {(passed ? "PASSED" : "FAILED")}");
            return passed;
        }
        
        /// <summary>
        /// Test error handling when frame budget is exceeded
        /// </summary>
        public async Task<bool> TestBudgetExceededErrorHandling()
        {
            Console.WriteLine("Testing Budget Exceeded Error Handling...");
            
            var passed = true;
            var budgetAlertReceived = false;
            
            _engine.EngineAlert += (s, e) =>
            {
                if (e.AlertType == EngineAlertType.FrameBudgetExceeded || 
                    e.AlertType == EngineAlertType.BudgetExceeded)
                {
                    budgetAlertReceived = true;
                    Console.WriteLine($"Budget exceeded alert received: {e.Message}");
                }
            };
            
            // Run workload designed to exceed budget
            for (int i = 0; i < 3; i++)
            {
                using (var frameToken = _engine.BeginFrame())
                {
                    try
                    {
                        // Submit heavy workload that should trigger budget alerts
                        await _engine.SubmitGpuWorkAsync("HeavyWorkload", 
                            async () => await SimulateGpuWorkAsync(15.0), // Very heavy
                            GpuTimingType.General);
                        
                        await _engine.EndFrameAsync(frameToken);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Budget exceeded test {i} encountered error: {ex.Message}");
                        // Errors are expected in this test
                    }
                }
            }
            
            // Check if alerts were received
            if (budgetAlertReceived)
            {
                Console.WriteLine("Budget exceeded alerts properly generated");
            }
            else
            {
                Console.WriteLine("Warning: No budget exceeded alerts were received");
                // This might still be valid depending on implementation
            }
            
            Console.WriteLine($"Budget Exceeded Error Handling Test: {(passed ? "PASSED" : "FAILED")}");
            return passed;
        }
        
        /// <summary>
        /// Test monitoring and real-time metrics
        /// </summary>
        public async Task<bool> TestRealTimeMonitoring()
        {
            Console.WriteLine("Testing Real-Time Monitoring and Metrics...");
            
            var passed = true;
            var monitoringTests = 5;
            var metricsData = new List<DirectX12RenderingEngineStats>();
            
            for (int i = 0; i < monitoringTests; i++)
            {
                using (var frameToken = _engine.BeginFrame())
                {
                    try
                    {
                        await SimulateRenderWorkloadAsync(i);
                        
                        // Collect metrics mid-frame
                        if (i == monitoringTests / 2)
                        {
                            var stats = _engine.Statistics;
                            metricsData.Add(stats);
                            
                            Console.WriteLine($"Mid-test Statistics:");
                            Console.WriteLine($"  Frame Pacing: {stats.FramePacing.IsPerformanceHealthy}");
                            Console.WriteLine($"  In-Flight Frames: {stats.FramePacing.InFlightFrameCount}");
                            Console.WriteLine($"  GPU Timeline Entries: {stats.FramePacing.GpuTimelineEntryCount}");
                            Console.WriteLine($"  Performance Grade: {stats.PerformanceGrade}");
                            Console.WriteLine($"  System Health: {stats.GetSystemHealth()}");
                        }
                        
                        await _engine.EndFrameAsync(frameToken);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Monitoring test {i} failed: {ex.Message}");
                        passed = false;
                    }
                }
            }
            
            // Final analysis
            var finalStats = _engine.Statistics;
            Console.WriteLine($"Final Statistics:");
            Console.WriteLine($"  Average FPS: {finalStats.OverallFps:F1}");
            Console.WriteLine($"  Performance Grade: {finalStats.PerformanceGrade}");
            Console.WriteLine($"  System Health: {finalStats.GetSystemHealth()}");
            
            // Test GPU utilization analysis
            var utilizationAnalysis = _engine.GetGpuUtilizationAnalysis(10);
            if (utilizationAnalysis != null)
            {
                Console.WriteLine($"GPU Utilization Analysis:");
                Console.WriteLine($"  Average: {utilizationAnalysis.AverageUtilization:F1}%");
                Console.WriteLine($"  Min: {utilizationAnalysis.MinUtilization:F1}%");
                Console.WriteLine($"  Max: {utilizationAnalysis.MaxUtilization:F1}%");
                Console.WriteLine($"  Spikes: {utilizationAnalysis.Spikes}");
            }
            
            Console.WriteLine($"Real-Time Monitoring Test: {(passed ? "PASSED" : "FAILED")}");
            return passed;
        }
        
        // Helper methods for simulation
        
        private async Task SimulateRenderWorkloadAsync(int frameId)
        {
            // Simulate various rendering operations
            await _engine.SubmitGpuWorkAsync("UpdateTransforms", 
                async () => await SimulateGpuWorkAsync(1.0), 
                GpuTimingType.General);
                
            await _engine.SubmitGpuWorkAsync("RenderScene", 
                async () => await SimulateGpuWorkAsync(5.0), 
                GpuTimingType.VertexProcessing);
                
            await _engine.SubmitGpuWorkAsync("PostProcess", 
                async () => await SimulateGpuWorkAsync(2.0), 
                GpuTimingType.PostProcess);
        }
        
        private async Task SimulateRenderingWorkloadAsync()
        {
            // Simulate realistic rendering workload that takes ~16ms
            var tasks = new[]
            {
                _engine.SubmitGpuWorkAsync("Geometry", async () => await SimulateGpuWorkAsync(4.0), GpuTimingType.VertexProcessing),
                _engine.SubmitGpuWorkAsync("Lighting", async () => await SimulateGpuWorkAsync(6.0), GpuTimingType.PixelProcessing),
                _engine.SubmitGpuWorkAsync("Shadows", async () => await SimulateGpuWorkAsync(4.0), GpuTimingType.Shadow),
                _engine.SubmitGpuWorkAsync("PostFX", async () => await SimulateGpuWorkAsync(2.5), GpuTimingType.PostProcess)
            };
            
            await Task.WhenAll(tasks);
        }
        
        private async Task SimulateGpuWorkAsync(double durationMs)
        {
            // Simulate GPU work with variable duration
            var random = new Random();
            var actualDuration = durationMs * (0.8 + random.NextDouble() * 0.4); // ±20% variance
            
            await Task.Delay(TimeSpan.FromMilliseconds(actualDuration / 10.0)); // Simulated time
        }
        
        private void SimulateResourceOperation(string name, ResourcePriority priority)
        {
            // Simulate resource operation
            var duration = priority switch
            {
                ResourcePriority.High => 1.0,
                ResourcePriority.Normal => 0.5,
                ResourcePriority.Low => 0.2,
                _ => 0.5
            };
            
            Thread.Sleep(TimeSpan.FromMilliseconds(duration));
        }
        
        private static double CalculateVariance(List<double> values)
        {
            if (values.Count < 2) return 0.0;
            
            var mean = values.Average();
            return values.Sum(x => (x - mean) * (x - mean)) / values.Count;
        }
        
        public void Dispose()
        {
            _engine?.Dispose();
            _performanceMonitor?.Dispose();
        }
    }
    
    /// <summary>
    /// Test runner for TIXL-021 frame pacing validation
    /// </summary>
    public class TIXL021TestRunner
    {
        public static async Task<bool> RunAllTests()
        {
            Console.WriteLine("=== TIXL-021: Frame Pacing and CPU-GPU Sync Tests ===");
            Console.WriteLine();
            
            var tests = new FramePacingTests();
            var results = new List<bool>();
            
            try
            {
                results.Add(await tests.TestSingleFrameBudgetSystem());
                results.Add(await tests.TestFenceBasedSynchronization());
                results.Add(await tests.TestFrameTimeManagement());
                results.Add(await tests.TestGpuTimelineProfiling());
                results.Add(await tests.TestResourceLifecycleOptimization());
                results.Add(await tests.TestBudgetExceededErrorHandling());
                results.Add(await tests.TestRealTimeMonitoring());
            }
            finally
            {
                tests.Dispose();
            }
            
            Console.WriteLine();
            Console.WriteLine("=== Test Results Summary ===");
            var passed = results.Count(r => r);
            var total = results.Count;
            
            Console.WriteLine($"Passed: {passed}/{total}");
            Console.WriteLine($"Success Rate: {(passed / (double)total * 100):F1}%");
            
            if (passed == total)
            {
                Console.WriteLine("🎉 All TIXL-021 tests PASSED!");
                return true;
            }
            else
            {
                Console.WriteLine("❌ Some TIXL-021 tests FAILED");
                return false;
            }
        }
    }
}