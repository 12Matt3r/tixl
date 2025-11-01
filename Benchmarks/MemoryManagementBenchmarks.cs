using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using SharpDX.Direct3D11;
using SharpDX;
using T3.Core.Memory;
using T3.Core.Rendering.Pools;
using T3.Core.Profiling;
using T3.Core.Benchmarks;
using T3.Core.Disposal;
using T3.Core.Textures;
using T3.Core.Strings;

namespace TiXL.Benchmarks
{
    /// <summary>
    /// Comprehensive benchmark suite for memory management optimizations
    /// </summary>
    public class MemoryManagementBenchmarks
    {
        private Device _device;
        private MemoryPerformanceMonitor _monitor;
        private DynamicBufferPool _bufferPool;
        private TexturePool _texturePool;
        private TextureMemoryManager _textureManager;
        private SmartResourceManager _resourceManager;
        private CompactableMemoryPool _memoryPool;

        [GlobalSetup]
        public void Setup()
        {
            // Initialize DirectX device for graphics benchmarks
            using var factory = new DXGI.Factory1();
            using var adapter = factory.Adapters1.FirstOrDefault();
            _device = new Device(adapter);

            // Initialize memory management systems
            _monitor = new MemoryPerformanceMonitor();
            _bufferPool = new DynamicBufferPool(_device, maxPoolSize: 100);
            _texturePool = new TexturePool(_device, maxPoolSize: 50);
            _textureManager = new TextureMemoryManager(_device);
            _resourceManager = new SmartResourceManager();
            _memoryPool = new CompactableMemoryPool(segmentSizeMB: 10);

            Console.WriteLine("Memory Management Benchmarks Setup Complete");
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _monitor?.Dispose();
            _device?.Dispose();
        }

        // Baseline benchmarks for comparison
        [Benchmark]
        public void Baseline_NewObjectAllocation()
        {
            for (int i = 0; i < 1000; i++)
            {
                var obj = new byte[1024];
                obj[0] = (byte)i;
            }
        }

        [Benchmark]
        public void Baseline_StringConcatenation()
        {
            string result = "";
            for (int i = 0; i < 100; i++)
            {
                result += $"Test{i}";
            }
        }

        [Benchmark]
        public void Baseline_DirectXBufferCreation()
        {
            for (int i = 0; i < 100; i++)
            {
                var desc = new BufferDescription
                {
                    Usage = ResourceUsage.Dynamic,
                    CpuAccessFlags = CpuAccessFlags.Write,
                    BindFlags = BindFlags.ConstantBuffer,
                    SizeInBytes = 1024
                };
                var buffer = new Buffer(_device, desc);
                buffer.Dispose();
            }
        }

        // Object Pooling Benchmarks
        [Benchmark]
        public void Optimized_ObjectPool_BufferAllocation()
        {
            for (int i = 0; i < 1000; i++)
            {
                using var pooledBuffer = _bufferPool.AcquireBuffer(1024);
                var buffer = pooledBuffer.Object;
                // Simulate usage
                buffer.Description.SizeInBytes = 1024;
            }
        }

        [Benchmark]
        public void Optimized_ObjectPool_TextureAllocation()
        {
            for (int i = 0; i < 100; i++)
            {
                using var pooledTexture = _texturePool.AcquireTexture(512, 512);
                var texture = pooledTexture.Object;
                // Simulate usage
            }
        }

        [Benchmark]
        public void Optimized_ArrayPool_Versus_New()
        {
            var arrayPool = System.Buffers.ArrayPool<byte>.Shared;
            
            for (int i = 0; i < 1000; i++)
            {
                var buffer = arrayPool.Rent(1024);
                buffer[0] = (byte)i;
                arrayPool.Return(buffer);
            }
        }

        // String Operation Benchmarks
        [Benchmark]
        public void Optimized_StringBuilderPool()
        {
            for (int i = 0; i < 100; i++)
            {
                var sb = StringBuilderPool.Acquire(256);
                try
                {
                    sb.Append("Performance Test ");
                    sb.Append(i.ToString());
                    var result = StringBuilderPool.ReleaseAndToString(sb);
                }
                catch
                {
                    StringBuilderPool.Release(sb);
                    throw;
                }
            }
        }

        [Benchmark]
        public void Optimized_StackAlloc_MatrixOperations()
        {
            for (int i = 0; i < 1000; i++)
            {
                using var buffer = new ScopedStackBuffer<float>(16);
                var span = buffer.AsSpan();

                // Simulate matrix operation
                for (int j = 0; j < 16; j++)
                {
                    span[j] = j * (float)i;
                }
                
                // Perform some computation
                MatrixOps.Multiply4x4(span, span, span);
            }
        }

        [Benchmark]
        public void Optimized_StackAlloc_VectorOperations()
        {
            for (int i = 0; i < 1000; i++)
            {
                using var buffer = new ScopedStackBuffer<float>(4);
                var vector = buffer.AsSpan();
                
                vector[0] = i * 0.1f;
                vector[1] = i * 0.2f;
                vector[2] = i * 0.3f;
                vector[3] = i * 0.4f;
                
                VectorOps.Normalize(vector);
            }
        }

        // Memory Management Benchmarks
        [Benchmark]
        public void Optimized_SafeDisposable_Pattern()
        {
            for (int i = 0; i < 1000; i++)
            {
                var safeObject = new SafeTestObject();
                // Simulate usage
                safeObject.Dispose();
            }
        }

        [Benchmark]
        public void Optimized_MemoryPool_Compaction()
        {
            // Allocate several objects
            var handles = new List<MemoryHandle>();
            
            for (int i = 0; i < 50; i++)
            {
                var handle = _memoryPool.Allocate(1024, $"Allocation_{i}");
                handles.Add(handle);
            }

            // Simulate some objects being freed
            for (int i = 0; i < 25; i += 2)
            {
                handles[i] = default;
            }

            // Trigger compaction
            _memoryPool.Compact();

            // Cleanup remaining
            foreach (var handle in handles.Where(h => h.AsSpan().Length > 0))
            {
                // Memory will be cleaned up when handle goes out of scope
            }
        }

        [Benchmark]
        public void Optimized_LargeObjectHeap_Avoidance()
        {
            for (int i = 0; i < 100; i++)
            {
                using var handle = _textureManager.CreateTexture(1024, 1024, 
                    DXGI.Format.R8G8B8A8_UNorm, 
                    BindFlags.RenderTarget | BindFlags.ShaderResource);
                
                // Simulate texture usage
                var data = new byte[1024 * 1024 * 4]; // 4MB
                handle.UpdateData(data);
            }
        }

        // Graphics-specific benchmarks
        [Benchmark]
        public void Optimized_BufferPool_DynamicUpdates()
        {
            var testData = new float[256];
            for (int i = 0; i < testData.Length; i++)
            {
                testData[i] = i * 0.1f;
            }

            for (int i = 0; i < 100; i++)
            {
                using var pooled = _bufferPool.AcquireBuffer(testData.Length * sizeof(float));
                _bufferPool.WriteData(pooled.Object, testData);
            }
        }

        [Benchmark]
        public void Optimized_TextureMemoryManager()
        {
            for (int i = 0; i < 50; i++)
            {
                using var texture = _textureManager.CreateTexture(512, 512, 
                    DXGI.Format.R8G8B8A8_UNorm, 
                    BindFlags.RenderTarget | BindFlags.ShaderResource,
                    $"TestTexture_{i}");
                
                // Simulate texture updates
                var data = new byte[512 * 512 * 4];
                texture.UpdateData(data);
            }
        }

        // Resource Management Benchmarks
        [Benchmark]
        public void Optimized_SmartResourceManager()
        {
            for (int i = 0; i < 100; i++)
            {
                var resource = _resourceManager.Manage($"resource_{i}", () => 
                {
                    return new TestResource { Data = new byte[1024] };
                });
                
                // Simulate usage
                resource.Resource.Data[0] = (byte)i;
            }
        }

        // Comprehensive Integration Benchmark
        [Benchmark]
        public void Integration_RealisticRenderingScenario()
        {
            const int frameCount = 10;
            const int objectCount = 100;

            for (int frame = 0; frame < frameCount; frame++)
            {
                // Simulate rendering a frame with multiple objects
                for (int obj = 0; obj < objectCount; obj++)
                {
                    // Allocate buffers
                    using var vertexBuffer = _bufferPool.AcquireBuffer(1024);
                    using var constantBuffer = _bufferPool.AcquireBuffer(256);
                    
                    // Allocate textures
                    using var diffuseMap = _texturePool.AcquireTexture(512, 512);
                    using var normalMap = _texturePool.AcquireTexture(512, 512);
                    
                    // String operations for shader compilation
                    var shaderSource = BuildShaderSource(obj, frame);
                    
                    // Memory operations
                    using var stackBuffer = new ScopedStackBuffer<float>(16);
                    PerformMatrixOperation(stackBuffer.AsSpan());
                }
            }
        }

        private static string BuildShaderSource(int obj, int frame)
        {
            var sb = StringBuilderPool.Acquire(1024);
            try
            {
                sb.Append($"// Shader for object {obj} at frame {frame}\n");
                sb.Append("struct VSInput { float3 position : POSITION; };\n");
                sb.Append("struct PSInput { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };\n");
                sb.Append("PSInput VS(VSInput input) { return CreatePSInput(input); }\n");
                sb.Append("float4 PS(PSInput input) : SV_TARGET { return float4(1,0,1,1); }\n");
                return StringBuilderPool.ReleaseAndToString(sb);
            }
            catch
            {
                StringBuilderPool.Release(sb);
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void PerformMatrixOperation(Span<float> matrix)
        {
            // Perform a simple matrix operation without allocations
            for (int i = 0; i < matrix.Length; i++)
            {
                matrix[i] += matrix[i] * 0.1f;
            }
        }

        // Memory Leak Detection Test
        [Benchmark]
        public void Test_MemoryLeakDetection()
        {
            var eventManager = new EventSubscriptionManager();
            var eventSource = new EventTestSource();

            // Subscribe to events
            for (int i = 0; i < 100; i++)
            {
                var handler = new EventHandler<EventArgs>((s, e) => { });
                eventManager.Subscribe(eventSource, "TestEvent", handler);
            }

            // Check for leaks
            var leaks = eventManager.GetUndisposedObjects();

            // Cleanup
            eventManager.UnsubscribeAll();
        }

        // Memory Fragmentation Test
        [Benchmark]
        public void Test_MemoryFragmentation()
        {
            var defragmenter = new MemoryDefragmenter();

            // Allocate many small objects
            var handles = new List<MemoryHandle>();
            for (int i = 0; i < 1000; i++)
            {
                handles.Add(defragmenter.AllocateBlock(1024, $"SmallObject_{i}"));
            }

            // Free every other object to create fragmentation
            for (int i = 0; i < handles.Count; i += 2)
            {
                handles[i] = default;
            }

            // Measure fragmentation
            var stats = defragmenter.GetStatistics();

            // Defragment
            defragmenter.Defragment();

            var newStats = defragmenter.GetStatistics();
        }

        // Performance Comparison Summary
        [Benchmark]
        public void Performance_Comparison_Summary()
        {
            _monitor.CaptureSnapshot("Before_Benchmarks");
            
            var baselineTime = MeasureExecutionTime(() =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    var obj = new byte[1024];
                    obj[0] = (byte)i;
                }
            });

            var optimizedTime = MeasureExecutionTime(() =>
            {
                var arrayPool = System.Buffers.ArrayPool<byte>.Shared;
                for (int i = 0; i < 1000; i++)
                {
                    var buffer = arrayPool.Rent(1024);
                    buffer[0] = (byte)i;
                    arrayPool.Return(buffer);
                }
            });

            _monitor.CaptureSnapshot("After_Benchmarks");
            
            Console.WriteLine($"Baseline Time: {baselineTime:F2}ms");
            Console.WriteLine($"Optimized Time: {optimizedTime:F2}ms");
            Console.WriteLine($"Improvement: {((baselineTime - optimizedTime) / baselineTime * 100):F1}%");
        }

        private static double MeasureExecutionTime(Action action)
        {
            var sw = Stopwatch.StartNew();
            action();
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }
    }

    // Supporting classes for benchmarks
    public class SafeTestObject : SafeDisposable
    {
        private readonly byte[] _data = new byte[1024];

        protected override void DisposeManagedResources()
        {
            // Cleanup managed resources
            Array.Clear(_data, 0, _data.Length);
        }
    }

    public class TestResource : IDisposable
    {
        public byte[] Data { get; set; }

        public void Dispose()
        {
            Data = null;
        }
    }

    public class EventTestSource
    {
        public event EventHandler<EventArgs> TestEvent;
        
        public void RaiseTestEvent()
        {
            TestEvent?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Main program to run all memory management benchmarks
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Starting TiXL Memory Management Benchmarks...");
            Console.WriteLine("==============================================");

            try
            {
                // Run comprehensive benchmark suite
                var benchmark = new MemoryManagementBenchmarks();
                
                // Setup
                benchmark.Setup();
                
                Console.WriteLine("\nRunning Individual Benchmarks...");
                
                // Run specific benchmarks
                benchmark.Baseline_NewObjectAllocation();
                Console.WriteLine("✓ Baseline object allocation complete");
                
                benchmark.Optimized_ObjectPool_BufferAllocation();
                Console.WriteLine("✓ Object pool buffer allocation complete");
                
                benchmark.Optimized_StringBuilderPool();
                Console.WriteLine("✓ String builder pool complete");
                
                benchmark.Optimized_StackAlloc_MatrixOperations();
                Console.WriteLine("✓ Stack allocation operations complete");
                
                benchmark.Integration_RealisticRenderingScenario();
                Console.WriteLine("✓ Integration scenario complete");
                
                benchmark.Performance_Comparison_Summary();
                Console.WriteLine("✓ Performance comparison complete");
                
                // Cleanup
                benchmark.Cleanup();
                
                Console.WriteLine("\n==============================================");
                Console.WriteLine("Memory Management Benchmarks Completed Successfully!");
                Console.WriteLine("Check the output for detailed performance metrics.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error running benchmarks: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}
