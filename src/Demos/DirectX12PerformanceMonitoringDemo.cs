using System;
using System.Threading.Tasks;
using Vortice.Windows.Direct3D12;
using TiXL.Core.Performance;
using TiXL.Core.Graphics.DirectX12;

namespace TiXL.Demos
{
    /// <summary>
    /// Demonstration of integrated DirectX 12 performance monitoring
    /// Shows real GPU performance data collection and analysis
    /// </summary>
    public class DirectX12PerformanceMonitoringDemo
    {
        private PerformanceMonitor _performanceMonitor;
        private DirectX12FramePacer _framePacer;
        private ID3D12Device5? _d3d12Device;
        private GpuTimelineProfiler _gpuProfiler;
        
        /// <summary>
        /// Initialize DirectX 12 device and performance monitoring
        /// </summary>
        public async Task InitializeAsync()
        {
            // Create DirectX 12 device (in real application, this would be from a D3D12Application)
            _d3d12Device = CreateD3D12Device();
            
            if (_d3d12Device != null)
            {
                // Initialize performance monitor with real DirectX device
                _performanceMonitor = new PerformanceMonitor(historySize: 300, d3d12Device: _d3d12Device);
                
                // Initialize frame pacer with DirectX integration
                var frameScheduler = new PredictiveFrameScheduler();
                _framePacer = new DirectX12FramePacer(_performanceMonitor, frameScheduler);
                
                // Initialize GPU profiler for detailed timeline analysis
                _gpuProfiler = new GpuTimelineProfiler();
                
                Console.WriteLine("DirectX 12 performance monitoring initialized successfully!");
                Console.WriteLine($"GPU: {_d3d12Device.Description}");
                Console.WriteLine($"Memory Budget: {GetMemoryBudget():F2} MB");
            }
            else
            {
                Console.WriteLine("DirectX 12 not available - using simulated performance metrics");
                _performanceMonitor = new PerformanceMonitor();
            }
        }
        
        /// <summary>
        /// Demonstrate real-time performance monitoring
        /// </summary>
        public async Task RunPerformanceMonitoringDemoAsync()
        {
            Console.WriteLine("\n=== DirectX 12 Performance Monitoring Demo ===");
            
            for (int frame = 0; frame < 120; frame++) // Monitor 120 frames (2 seconds at 60 FPS)
            {
                using (var frameBudget = _framePacer.BeginFrame())
                {
                    try
                    {
                        // Begin frame timing
                        _performanceMonitor.BeginFrame();
                        
                        // Simulate DirectX 12 rendering operations with real timing
                        await SimulateRenderingOperationsAsync(frameBudget);
                        
                        // End frame timing with DirectX data collection
                        await _framePacer.EndFrameAsync(frameBudget);
                        _performanceMonitor.EndFrame();
                        
                        // Output performance data every 30 frames
                        if (frame % 30 == 0)
                        {
                            await OutputPerformanceAnalysisAsync(frame);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Frame {frame} error: {ex.Message}");
                    }
                }
            }
            
            // Final performance analysis
            Console.WriteLine("\n=== Final Performance Analysis ===");
            await OutputFinalAnalysisAsync();
        }
        
        /// <summary>
        /// Demonstrate DirectX 12 operation-level profiling
        /// </summary>
        public async Task DemonstrateOperationProfilingAsync()
        {
            Console.WriteLine("\n=== DirectX Operation Profiling Demo ===");
            
            // Profile specific DirectX operations
            var renderPassHandle = _performanceMonitor.BeginD3D12Operation("Render Pass", D3D12QueryType.Timestamp);
            var cullingHandle = _performanceMonitor.BeginD3D12Operation("Frustum Culling", D3D12QueryType.PipelineStatistics);
            
            try
            {
                // Simulate DirectX rendering pipeline operations
                await SimulateFrustumCullingAsync();
                _performanceMonitor.EndD3D12Operation(ref cullingHandle);
                
                await SimulateMainRenderPassAsync();
                _performanceMonitor.EndD3D12Operation(ref renderPassHandle);
                
                // Get real performance data
                var memoryUsage = _performanceMonitor.GetCurrentGpuMemoryUsage();
                var hardwareCounters = _performanceMonitor.GetHardwareCounters();
                var presentMetrics = _performanceMonitor.GetPresentMetrics();
                
                Console.WriteLine($"GPU Memory Usage: {memoryUsage.CurrentUsage / (1024 * 1024):F2} MB");
                Console.WriteLine($"Memory Pressure: {memoryUsage.PressureLevel:F1}%");
                Console.WriteLine($"GPU Utilization: {hardwareCounters.Utilization:F1}%");
                Console.WriteLine($"Present Duration: {presentMetrics.PresentDuration:F3} ms");
                Console.WriteLine($"Frame Latency: {presentMetrics.FrameLatency:F3} ms");
            }
            finally
            {
                // Ensure operations are ended even if errors occur
                if (renderPassHandle.IsValid)
                    _performanceMonitor.EndD3D12Operation(ref renderPassHandle);
                if (cullingHandle.IsValid)
                    _performanceMonitor.EndD3D12Operation(ref cullingHandle);
            }
        }
        
        /// <summary>
        /// Demonstrate GPU memory monitoring
        /// </summary>
        public async Task DemonstrateMemoryMonitoringAsync()
        {
            Console.WriteLine("\n=== GPU Memory Monitoring Demo ===");
            
            for (int i = 0; i < 60; i++) // Monitor for 1 second
            {
                var memoryUsage = _performanceMonitor.GetCurrentGpuMemoryUsage();
                
                Console.WriteLine($"Frame {i}: Memory={memoryUsage.CurrentUsage / (1024 * 1024):F2}MB, " +
                                $"Budget={memoryUsage.Budget / (1024 * 1024):F2}MB, " +
                                $"Pressure={memoryUsage.PressureLevel:F1}%, " +
                                $"Descriptors={memoryUsage.AvailableDescriptors}");
                
                // Simulate memory allocation
                await SimulateTextureUploadAsync(memoryUsage);
                
                await Task.Delay(16); // ~60 FPS
            }
        }
        
        /// <summary>
        /// Demonstrate hardware counter monitoring
        /// </summary>
        public async Task DemonstrateHardwareCountersAsync()
        {
            Console.WriteLine("\n=== Hardware Counters Demo ===");
            
            // Clear GPU profiler timeline for clean analysis
            _gpuProfiler.ClearTimeline();
            
            for (int frame = 0; frame < 60; frame++)
            {
                using (var frameBudget = _framePacer.BeginFrame())
                {
                    _performanceMonitor.BeginFrame();
                    
                    // Profile various GPU operations
                    using (var vertexProcessing = _performanceMonitor.ProfileOperation("Vertex Processing"))
                    {
                        await SimulateVertexProcessingAsync();
                    }
                    
                    using (var pixelProcessing = _performanceMonitor.ProfileOperation("Pixel Processing"))
                    {
                        await SimulatePixelProcessingAsync();
                    }
                    
                    using (var computeOperations = _performanceMonitor.ProfileOperation("Compute Operations"))
                    {
                        await SimulateComputeOperationsAsync();
                    }
                    
                    await _framePacer.EndFrameAsync(frameBudget);
                    _performanceMonitor.EndFrame();
                }
            }
            
            // Analyze GPU timeline and hardware utilization
            var timelineAnalysis = _gpuProfiler.GetTimelineAnalysis(60);
            var utilizationAnalysis = _gpuProfiler.GetUtilizationAnalysis(60);
            var hardwareCounters = _performanceMonitor.GetHardwareCounters();
            
            Console.WriteLine($"GPU Timeline Analysis:");
            Console.WriteLine($"  Average GPU Time: {timelineAnalysis?.AverageGpuTime:F3} ms");
            Console.WriteLine($"  GPU Time Variance: {timelineAnalysis?.GpuTimeVariance:F3}");
            Console.WriteLine($"  Total GPU Time: {timelineAnalysis?.TotalGpuTime:F3} ms");
            
            Console.WriteLine($"\nGPU Utilization Analysis:");
            Console.WriteLine($"  Average Utilization: {utilizationAnalysis?.AverageUtilization:F1}%");
            Console.WriteLine($"  Min/Max Utilization: {utilizationAnalysis?.MinUtilization:F1}% / {utilizationAnalysis?.MaxUtilization:F1}%");
            Console.WriteLine($"  Utilization Spikes: {utilizationAnalysis?.Spikes}");
            
            Console.WriteLine($"\nHardware Counters:");
            Console.WriteLine($"  Vertex Throughput: {hardwareCounters.VertexThroughput:F0} verts/sec");
            Console.WriteLine($"  Pixel Throughput: {hardwareCounters.PixelThroughput:F0} pixels/sec");
            Console.WriteLine($"  Memory Bandwidth: {hardwareCounters.MemoryBandwidth / (1024 * 1024 * 1024):F2} GB/s");
            Console.WriteLine($"  IA Primitives: {hardwareCounters.IaPrimitives:F0}");
            Console.WriteLine($"  VS Invocations: {hardwareCounters.VsInvocations:F0}");
            Console.WriteLine($"  PS Invocations: {hardwareCounters.PsInvocations:F0}");
            Console.WriteLine($"  CS Invocations: {hardwareCounters.CsInvocations:F0}");
        }
        
        private async Task SimulateRenderingOperationsAsync(FrameBudgetToken frameBudget)
        {
            // Use DirectX operation profiling for real GPU timing
            var clearHandle = _performanceMonitor.BeginD3D12Operation("Clear Pass", D3D12QueryType.Timestamp);
            var shadowHandle = _performanceMonitor.BeginD3D12Operation("Shadow Pass", D3D12QueryType.PipelineStatistics);
            var mainRenderHandle = _performanceMonitor.BeginD3D12Operation("Main Render", D3D12QueryType.Timestamp);
            var postProcessHandle = _performanceMonitor.BeginD3D12Operation("Post Processing", D3D12QueryType.Timestamp);
            
            try
            {
                // Simulate clear pass
                await SimulateClearPassAsync();
                _performanceMonitor.EndD3D12Operation(ref clearHandle);
                
                // Simulate shadow map rendering
                await SimulateShadowPassAsync();
                _performanceMonitor.EndD3D12Operation(ref shadowHandle);
                
                // Simulate main render pass
                await SimulateMainRenderPassAsync();
                _performanceMonitor.EndD3D12Operation(ref mainRenderHandle);
                
                // Simulate post-processing
                await SimulatePostProcessingAsync();
                _performanceMonitor.EndD3D12Operation(ref postProcessHandle);
            }
            finally
            {
                // Ensure all operations are properly ended
                if (clearHandle.IsValid) _performanceMonitor.EndD3D12Operation(ref clearHandle);
                if (shadowHandle.IsValid) _performanceMonitor.EndD3D12Operation(ref shadowHandle);
                if (mainRenderHandle.IsValid) _performanceMonitor.EndD3D12Operation(ref mainRenderHandle);
                if (postProcessHandle.IsValid) _performanceMonitor.EndD3D12Operation(ref postProcessHandle);
            }
        }
        
        private async Task SimulateClearPassAsync()
        {
            // Simulate DirectX 12 clear operation
            await Task.Delay(1); // Simulated GPU time
        }
        
        private async Task SimulateShadowPassAsync()
        {
            // Simulate shadow map rendering
            await Task.Delay(3); // Simulated GPU time
        }
        
        private async Task SimulateMainRenderPassAsync()
        {
            // Simulate main 3D rendering
            await Task.Delay(8); // Simulated GPU time
        }
        
        private async Task SimulatePostProcessingAsync()
        {
            // Simulate post-processing effects
            await Task.Delay(2); // Simulated GPU time
        }
        
        private async Task SimulateFrustumCullingAsync()
        {
            await Task.Delay(1); // Simulated CPU time
        }
        
        private async Task SimulateVertexProcessingAsync()
        {
            await Task.Delay(4); // Simulated GPU time
        }
        
        private async Task SimulatePixelProcessingAsync()
        {
            await Task.Delay(6); // Simulated GPU time
        }
        
        private async Task SimulateComputeOperationsAsync()
        {
            await Task.Delay(3); // Simulated GPU time
        }
        
        private async Task SimulateTextureUploadAsync(D3D12MemoryUsage memoryUsage)
        {
            // Simulate texture upload based on available memory
            if (memoryUsage.PressureLevel < 80.0)
            {
                await Task.Delay(2); // Simulate upload time
            }
        }
        
        private async Task OutputPerformanceAnalysisAsync(int frame)
        {
            var analysis = _performanceMonitor.GetFrameAnalysis();
            if (analysis != null)
            {
                Console.WriteLine($"\n--- Frame {frame} Analysis ---");
                Console.WriteLine($"Frame Time: {analysis.AverageFrameTime:F3} ms ({analysis.AverageFps:F1} FPS)");
                Console.WriteLine($"Performance Grade: {analysis.PerformanceGrade}");
                Console.WriteLine($"CPU Time: {analysis.AverageCpuTime:F3} ms");
                Console.WriteLine($"GPU Time: {analysis.AverageGpuTime:F3} ms");
                Console.WriteLine($"GPU Utilization: {analysis.AverageGpuUtilization:F1}%");
                Console.WriteLine($"GPU Memory Usage: {analysis.AverageGpuMemoryUsage / (1024 * 1024):F2} MB");
                Console.WriteLine($"Present Time: {analysis.AveragePresentTime:F3} ms");
                Console.WriteLine($"Frame Latency: {analysis.AverageFrameLatency:F3} ms");
                
                if (analysis.Recommendations.Count > 0)
                {
                    Console.WriteLine("Recommendations:");
                    foreach (var recommendation in analysis.Recommendations)
                    {
                        Console.WriteLine($"  • {recommendation}");
                    }
                }
            }
        }
        
        private async Task OutputFinalAnalysisAsync()
        {
            var analysis = _performanceMonitor.GetFrameAnalysis();
            if (analysis != null)
            {
                Console.WriteLine($"Overall Performance Grade: {analysis.PerformanceGrade}");
                Console.WriteLine($"Meets Performance Target: {analysis.MeetsPerformanceTarget()}");
                Console.WriteLine($"Meets GPU Performance Target: {analysis.MeetsGpuPerformanceTarget()}");
                Console.WriteLine($"\nAverage Performance Metrics:");
                Console.WriteLine($"  Frame Time: {analysis.AverageFrameTime:F3} ms");
                Console.WriteLine($"  GPU Utilization: {analysis.AverageGpuUtilization:F1}%");
                Console.WriteLine($"  GPU Memory Pressure: {analysis.AverageGpuMemoryPressure:F1}%");
                Console.WriteLine($"  Vertex Throughput: {analysis.AverageGpuVertexThroughput:F0} verts/sec");
                Console.WriteLine($"  Pixel Throughput: {analysis.AverageGpuPixelThroughput:F0} pixels/sec");
            }
        }
        
        private ID3D12Device5? CreateD3D12Device()
        {
            try
            {
                // In a real application, this would be created from a Windows handle
                // For demo purposes, we'll return null to simulate no DirectX device
                return null;
            }
            catch
            {
                return null;
            }
        }
        
        private double GetMemoryBudget()
        {
            if (_d3d12Device != null)
            {
                try
                {
                    var budgetInfo = _d3d12Device.QueryMemoryBudget(D3D12_MEMORY_POOL.Unknown);
                    return budgetInfo.Budget / (1024.0 * 1024.0); // Convert to MB
                }
                catch
                {
                    return 1024.0; // Default 1GB
                }
            }
            return 1024.0; // Simulated 1GB
        }
        
        public void Dispose()
        {
            _performanceMonitor?.Dispose();
            _gpuProfiler?.Dispose();
            _d3d12Device?.Dispose();
        }
    }
}