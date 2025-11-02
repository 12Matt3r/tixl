using System;
using System.Threading.Tasks;
using Vortice.Windows.Direct3D12;
using TiXL.Core.Graphics.DirectX12;
using TiXL.Core.Performance;

namespace TiXL.Demos
{
    /// <summary>
    /// Demonstration of real DirectX 12 resource management using Vortice.Windows
    /// Shows proper resource creation, lifecycle management, leak detection, and COM reference counting
    /// </summary>
    public class DirectXResourceManagementDemo
    {
        private ID3D12Device4 _device;
        private ID3D12CommandQueue _commandQueue;
        private DirectX12RenderingEngine _renderingEngine;
        private ResourceLifecycleManager _resourceManager;

        /// <summary>
        /// Initialize DirectX 12 resource management demo
        /// </summary>
        public async Task InitializeAsync()
        {
            Console.WriteLine("Initializing DirectX 12 Resource Management Demo...");

            // Create DirectX 12 device (in real application, this would come from a D3D12Application)
            _device = CreateD3D12Device();
            
            if (_device == null)
            {
                Console.WriteLine("DirectX 12 not available - demo cannot proceed");
                return;
            }

            // Create command queue for resource operations
            var queueDesc = new CommandQueueDescription(D3D12_COMMAND_LIST_TYPE_DIRECT);
            _commandQueue = _device.CreateCommandQueue(queueDesc);

            // Initialize performance monitor
            var performanceMonitor = new PerformanceMonitor();
            
            // Create rendering engine with real DirectX components
            var config = new RenderingEngineConfig
            {
                EnableGpuProfiling = true,
                PrecreateResourcePools = true,
                MaxGpuBufferPoolSize = 8,
                MaxTexturePoolSize = 4
            };

            _renderingEngine = new DirectX12RenderingEngine(_device, _commandQueue, performanceMonitor, null, config);
            
            // Initialize the rendering engine
            var initialized = await _renderingEngine.InitializeAsync();
            if (!initialized)
            {
                throw new InvalidOperationException("Failed to initialize DirectX 12 rendering engine");
            }

            // Get the resource manager for direct access
            _resourceManager = new ResourceLifecycleManager();
            _resourceManager.InitializeDirectX12Components(_device, _commandQueue);

            Console.WriteLine($"DirectX 12 Resource Management initialized successfully!");
            Console.WriteLine($"Device: {_device.Description}");
            Console.WriteLine($"Command Queue: {_commandQueue}");
        }

        /// <summary>
        /// Demonstrate real buffer creation and management
        /// </summary>
        public async Task DemonstrateBufferManagementAsync()
        {
            Console.WriteLine("\n=== Buffer Management Demo ===");

            // Create a buffer for vertex data
            var vertexData = new float[]
            {
                // Positions (x, y, z), Normals (x, y, z), UVs (u, v)
                0.0f, 0.5f, 0.0f,   0.0f, 0.0f, 1.0f,   0.5f, 1.0f,
               -0.5f, -0.5f, 0.0f,   0.0f, 0.0f, 1.0f,   0.0f, 0.0f,
                0.5f, -0.5f, 0.0f,   0.0f, 0.0f, 1.0f,   1.0f, 0.0f
            };

            var bufferDesc = new BufferCreationDesc
            {
                SizeInBytes = vertexData.Length * sizeof(float),
                HeapProperties = new HeapProperties(HeapType.Default),
                Flags = ResourceFlags.None,
                InitialState = ResourceStates.Common,
                DebugName = "VertexBuffer_Demo"
            };

            // Create the buffer using real DirectX 12 APIs
            var buffer = _resourceManager.CreateDirectXBuffer(bufferDesc);
            Console.WriteLine($"Created vertex buffer: {buffer.DebugName} (Size: {buffer.SizeInBytes} bytes)");

            // Upload data to the buffer
            byte[] rawData = new byte[vertexData.Length * sizeof(float)];
            Buffer.BlockCopy(vertexData, 0, rawData, 0, rawData.Length);
            
            _resourceManager.UploadToBuffer(buffer.Resource, rawData);
            Console.WriteLine($"Uploaded {rawData.Length} bytes to vertex buffer");

            // Create a constant buffer
            var constantBufferDesc = new BufferCreationDesc
            {
                SizeInBytes = 256, // 256-byte aligned constant buffer
                HeapProperties = new HeapProperties(HeapType.Upload),
                Flags = ResourceFlags.None,
                InitialState = ResourceStates.GenericRead,
                DebugName = "ConstantBuffer_Demo"
            };

            var constantBuffer = _resourceManager.CreateDirectXBuffer(constantBufferDesc);
            Console.WriteLine($"Created constant buffer: {constantBuffer.DebugName} (Size: {constantBuffer.SizeInBytes} bytes)");

            // Simulate some work
            await Task.Delay(100);

            // Demonstrate resource reuse through pools
            Console.WriteLine("\n--- Resource Pool Demo ---");
            
            var statsBefore = _resourceManager.GetResourcePoolStatistics();
            foreach (var stat in statsBefore)
            {
                Console.WriteLine($"Pool '{stat.Key}': {stat.Value.AvailableResources} available, {stat.Value.InUseResources} in use");
            }

            // Dispose buffers
            buffer.Dispose();
            constantBuffer.Dispose();
            Console.WriteLine("Disposed buffers");
        }

        /// <summary>
        /// Demonstrate real texture creation and management
        /// </summary>
        public async Task DemonstrateTextureManagementAsync()
        {
            Console.WriteLine("\n=== Texture Management Demo ===");

            // Create a 2D texture
            var textureDesc = new TextureCreationDesc
            {
                Dimension = ResourceDimension.Texture2D,
                Width = 512,
                Height = 512,
                ArraySize = 1,
                MipLevels = 1,
                Format = Format.R8G8B8A8_UNorm,
                Flags = ResourceFlags.None,
                InitialState = ResourceStates.Common,
                DebugName = "Texture2D_Demo"
            };

            var texture = _resourceManager.CreateDirectXTexture(textureDesc);
            Console.WriteLine($"Created 2D texture: {texture.DebugName} ({texture.Width}x{texture.Height}, {texture.Format})");

            // Create a render target texture
            var renderTargetDesc = new TextureCreationDesc
            {
                Dimension = ResourceDimension.Texture2D,
                Width = 1920,
                Height = 1080,
                ArraySize = 1,
                MipLevels = 1,
                Format = Format.R8G8B8A8_UNorm,
                Flags = ResourceFlags.AllowRenderTarget | ResourceFlags.AllowUnorderedAccess,
                InitialState = ResourceStates.Common,
                DebugName = "RenderTarget_Demo"
            };

            var renderTarget = _resourceManager.CreateDirectXTexture(renderTargetDesc);
            Console.WriteLine($"Created render target: {renderTarget.DebugName} ({renderTarget.Width}x{renderTarget.Height})");

            // Create a depth stencil texture
            var depthStencilDesc = new TextureCreationDesc
            {
                Dimension = ResourceDimension.Texture2D,
                Width = 1920,
                Height = 1080,
                ArraySize = 1,
                MipLevels = 1,
                Format = Format.D24_UNorm_S8_UInt,
                Flags = ResourceFlags.AllowDepthStencil,
                InitialState = ResourceStates.Common,
                DebugName = "DepthStencil_Demo"
            };

            var depthStencil = _resourceManager.CreateDirectXTexture(depthStencilDesc);
            Console.WriteLine($"Created depth stencil: {depthStencil.DebugName} ({depthStencil.Width}x{depthStencil.Height})");

            // Simulate texture usage
            await Task.Delay(100);

            // Demonstrate texture pool usage
            var textureStats = _resourceManager.GetResourcePoolStatistics();
            if (textureStats.TryGetValue("Texture2D", out var texPoolStat))
            {
                Console.WriteLine($"Texture2D Pool: {texPoolStat.UtilizationRate:P1} utilization, {texPoolStat.AvailableResources} available");
            }

            // Dispose textures
            texture.Dispose();
            renderTarget.Dispose();
            depthStencil.Dispose();
            Console.WriteLine("Disposed all textures");
        }

        /// <summary>
        /// Demonstrate query heap creation and usage
        /// </summary>
        public async Task DemonstrateQueryHeapManagementAsync()
        {
            Console.WriteLine("\n=== Query Heap Management Demo ===");

            // Create a timestamp query heap
            var timestampHeapDesc = new QueryHeapCreationDesc
            {
                Type = D3D12_QUERY_HEAP_TYPE_TIMESTAMP,
                Count = 256,
                DebugName = "TimestampHeap_Demo"
            };

            var timestampHeap = _resourceManager.CreateDirectXQueryHeap(timestampHeapDesc);
            Console.WriteLine($"Created timestamp query heap: {timestampHeap.DebugName} ({timestampHeap.Count} queries)");

            // Create an occlusion query heap
            var occlusionHeapDesc = new QueryHeapCreationDesc
            {
                Type = D3D12_QUERY_HEAP_TYPE_OCCLUSION,
                Count = 128,
                DebugName = "OcclusionHeap_Demo"
            };

            var occlusionHeap = _resourceManager.CreateDirectXQueryHeap(occlusionHeapDesc);
            Console.WriteLine($"Created occlusion query heap: {occlusionHeap.DebugName} ({occlusionHeap.Count} queries)");

            // Simulate query usage
            await Task.Delay(100);

            // Demonstrate query heap statistics
            var resourceStats = _resourceManager.GetDirectXResourceStatistics();
            Console.WriteLine($"Total Query Heaps: {resourceStats.QueryHeapCount}");

            // Dispose query heaps
            timestampHeap.Dispose();
            occlusionHeap.Dispose();
            Console.WriteLine("Disposed query heaps");
        }

        /// <summary>
        /// Demonstrate resource leak detection
        /// </summary>
        public async Task DemonstrateLeakDetectionAsync()
        {
            Console.WriteLine("\n=== Resource Leak Detection Demo ===");

            // Create some resources
            var buffer = _resourceManager.CreateDirectXBuffer(new BufferCreationDesc
            {
                SizeInBytes = 1024,
                DebugName = "LeakTestBuffer"
            });

            var texture = _resourceManager.CreateDirectXTexture(new TextureCreationDesc
            {
                Width = 256,
                Height = 256,
                DebugName = "LeakTestTexture"
            });

            Console.WriteLine("Created test resources for leak detection");

            // Check for leaks immediately (should find none)
            var initialReport = _resourceManager.CheckForResourceLeaks();
            Console.WriteLine($"Initial leak check: {initialReport.LeakedResources.Count} leaks detected");

            // Simulate some work
            await Task.Delay(100);

            // Check for leaks again
            var secondReport = _resourceManager.CheckForResourceLeaks();
            Console.WriteLine($"Second leak check: {secondReport.LeakedResources.Count} leaks detected");

            // Demonstrate reference counting validation
            var refIssues = _resourceManager.ValidateReferenceCounting();
            Console.WriteLine($"Reference counting issues: {refIssues.Count}");

            // Dispose resources properly
            buffer.Dispose();
            texture.Dispose();

            // Check for leaks after disposal
            var finalReport = _resourceManager.CheckForResourceLeaks();
            Console.WriteLine($"Final leak check: {finalReport.LeakedResources.Count} leaks detected");

            if (finalReport.LeakedResources.Count == 0)
            {
                Console.WriteLine("✓ All resources properly disposed - no leaks detected!");
            }
        }

        /// <summary>
        /// Demonstrate comprehensive resource statistics
        /// </summary>
        public async Task DemonstrateResourceStatisticsAsync()
        {
            Console.WriteLine("\n=== Resource Statistics Demo ===");

            // Create various resources to populate statistics
            var buffer = _resourceManager.CreateDirectXBuffer(new BufferCreationDesc
            {
                SizeInBytes = 2048,
                DebugName = "StatsBuffer"
            });

            var texture = _resourceManager.CreateDirectXTexture(new TextureCreationDesc
            {
                Width = 1024,
                Height = 1024,
                DebugName = "StatsTexture"
            });

            var queryHeap = _resourceManager.CreateDirectXQueryHeap(new QueryHeapCreationDesc
            {
                Count = 128,
                DebugName = "StatsQueryHeap"
            });

            // Get comprehensive statistics
            var resourceStats = _resourceManager.GetDirectXResourceStatistics();
            Console.WriteLine("--- DirectX Resource Statistics ---");
            Console.WriteLine($"Total Tracked Resources: {resourceStats.TotalTrackedResources}");
            Console.WriteLine($"Buffer Count: {resourceStats.BufferCount}");
            Console.WriteLine($"Texture Count: {resourceStats.TextureCount}");
            Console.WriteLine($"Query Heap Count: {resourceStats.QueryHeapCount}");
            Console.WriteLine($"Total Buffer Memory: {FormatBytes(resourceStats.TotalBufferMemory)}");
            Console.WriteLine($"Total Texture Memory: {FormatBytes(resourceStats.TotalTextureMemory)}");
            Console.WriteLine($"Active Pools: {resourceStats.ActivePools}");

            // Get pool statistics
            var poolStats = _resourceManager.GetResourcePoolStatistics();
            Console.WriteLine("\n--- Resource Pool Statistics ---");
            foreach (var poolStat in poolStats)
            {
                Console.WriteLine($"Pool '{poolStat.Key}':");
                Console.WriteLine($"  Available: {poolStat.Value.AvailableResources}");
                Console.WriteLine($"  In Use: {poolStat.Value.InUseResources}");
                Console.WriteLine($"  Total Created: {poolStat.Value.TotalResourcesCreated}");
                Console.WriteLine($"  Utilization: {poolStat.Value.UtilizationRate:P1}");
            }

            // Get creation statistics
            var creationStats = _resourceManager.GetResourceCreationStatistics();
            Console.WriteLine("\n--- Resource Creation Statistics ---");
            foreach (var creationStat in creationStats)
            {
                Console.WriteLine($"{creationStat.Key}:");
                Console.WriteLine($"  Total Creations: {creationStat.Value.TotalCreations}");
                Console.WriteLine($"  Successful: {creationStat.Value.SuccessfulCreations}");
                Console.WriteLine($"  Failed: {creationStat.Value.FailedCreations}");
                Console.WriteLine($"  Average Time: {creationStat.Value.AverageCreationTime:F2}ms");
            }

            // Simulate some work
            await Task.Delay(200);

            // Clean up
            buffer.Dispose();
            texture.Dispose();
            queryHeap.Dispose();
        }

        /// <summary>
        /// Run the complete resource management demo
        /// </summary>
        public async Task RunCompleteDemoAsync()
        {
            try
            {
                await InitializeAsync();
                
                await DemonstrateBufferManagementAsync();
                await DemonstrateTextureManagementAsync();
                await DemonstrateQueryHeapManagementAsync();
                await DemonstrateLeakDetectionAsync();
                await DemonstrateResourceStatisticsAsync();

                Console.WriteLine("\n=== Demo Complete ===");
                Console.WriteLine("All DirectX resource management features demonstrated successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Demo failed: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        /// <summary>
        /// Cleanup resources
        /// </summary>
        public void Cleanup()
        {
            try
            {
                // Generate final leak report
                if (_resourceManager != null)
                {
                    var leakReport = _resourceManager.GenerateResourceLeakReport();
                    Console.WriteLine($"\nFinal Leak Report:\n{leakReport}");
                }

                // Dispose components
                _renderingEngine?.Dispose();
                _resourceManager?.Dispose();
                _commandQueue?.Dispose();
                _device?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cleanup error: {ex.Message}");
            }
        }

        private static ID3D12Device4 CreateD3D12Device()
        {
            try
            {
                // Try to create a hardware adapter
                if (DXGI.DXGI.CreateDXGIFactory1(out var factory).Success)
                {
                    for (int i = 0; i < factory.Adapters.Count; i++)
                    {
                        var adapter = factory.Adapters[i];
                        if (adapter.Description.HardwareCompositionLevel != HardwareCompositionLevel.None)
                        {
                            if (D3D12.D3D12CreateDevice(adapter, out var device).Success)
                            {
                                return device;
                            }
                        }
                    }
                }

                // Try WARP adapter (software)
                if (DXGI.DXGI.CreateDXGIFactory1(out var warpFactory).Success)
                {
                    var warpAdapter = warpFactory.Adapters.FirstOrDefault(a => a.Description.Description.Contains("Microsoft Basic Render Driver"));
                    if (warpAdapter != null && D3D12.D3D12CreateDevice(warpAdapter, out var warpDevice).Success)
                    {
                        return warpDevice;
                    }
                }
            }
            catch
            {
                // Fall through to null return
            }

            return null;
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:F2} {sizes[order]}";
        }
    }
}