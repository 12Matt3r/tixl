using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using TiXL.PerformanceSuite.Core;

namespace TiXL.PerformanceSuite.Benchmarks
{
    /// <summary>
    /// Memory usage performance benchmarks for TiXL
    /// 
    /// These benchmarks measure:
    /// - Memory allocation patterns
    /// - GC pressure and collection performance
    /// - Memory leak detection
    /// - Object pooling effectiveness
    /// - Large object heap usage
    /// - Memory fragmentation
    /// </summary>
    [SimpleJob(RuntimeMoniker.Net90, launchCount: 3, iterationCount: 10, warmupCount: 3)]
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.Fastest)]
    public class MemoryBenchmarks
    {
        private readonly PerformanceMonitorService _monitor;
        private readonly List<MemoryMeasurement> _measurements;
        private readonly Random _random;
        
        // Memory test parameters
        private const int SMALL_ALLOCATION_SIZE = 1024; // 1KB
        private const int MEDIUM_ALLOCATION_SIZE = 10240; // 10KB
        private const int LARGE_ALLOCATION_SIZE = 102400; // 100KB
        private const int VERY_LARGE_ALLOCATION_SIZE = 1048576; // 1MB
        private const int ALLOCATION_COUNT = 1000;

        public MemoryBenchmarks()
        {
            _monitor = new PerformanceMonitorService(LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<PerformanceMonitorService>());
            _measurements = new List<MemoryMeasurement>();
            _random = new Random(42);
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
        /// Baseline: Naive memory allocation without pooling
        /// </summary>
        [Benchmark]
        public long Allocation_Baseline_Naive()
        {
            var initialMemory = GC.GetTotalMemory(false);
            var allocatedObjects = new List<object>();
            
            // Allocate many small objects
            for (int i = 0; i < ALLOCATION_COUNT; i++)
            {
                var obj = new byte[SMALL_ALLOCATION_SIZE];
                obj[0] = (byte)i;
                allocatedObjects.Add(obj);
                
                // Simulate some work with the object
                ProcessSmallObject(obj);
            }
            
            var finalMemory = GC.GetTotalMemory(false);
            var memoryIncrease = finalMemory - initialMemory;
            
            _monitor.RecordBenchmarkMetric("MemoryUsage", "Baseline_Allocations", ALLOCATION_COUNT, "count");
            _monitor.RecordBenchmarkMetric("MemoryUsage", "Baseline_MemoryIncrease", memoryIncrease, "bytes");
            _monitor.RecordBenchmarkMetric("MemoryUsage", "Baseline_AvgAllocationSize", memoryIncrease / (double)ALLOCATION_COUNT, "bytes");
            
            // Cleanup
            allocatedObjects.Clear();
            allocatedObjects = null;
            
            return memoryIncrease;
        }

        /// <summary>
        /// Optimized: Using ArrayPool for temporary allocations
        /// </summary>
        [Benchmark]
        public long Allocation_Optimized_ArrayPool()
        {
            var initialMemory = GC.GetTotalMemory(false);
            var arrayPool = System.Buffers.ArrayPool<byte>.Shared;
            var allocationsCount = 0;
            
            // Use array pool for temporary allocations
            for (int i = 0; i < ALLOCATION_COUNT; i++)
            {
                var buffer = arrayPool.Rent(SMALL_ALLOCATION_SIZE);
                buffer[0] = (byte)i;
                
                // Simulate work with the buffer
                ProcessSmallObject(buffer);
                
                arrayPool.Return(buffer);
                allocationsCount++;
            }
            
            var finalMemory = GC.GetTotalMemory(false);
            var memoryIncrease = finalMemory - initialMemory;
            
            _monitor.RecordBenchmarkMetric("MemoryUsage", "ArrayPool_Allocations", allocationsCount, "count");
            _monitor.RecordBenchmarkMetric("MemoryUsage", "ArrayPool_MemoryIncrease", memoryIncrease, "bytes");
            _monitor.RecordBenchmarkMetric("MemoryUsage", "ArrayPool_AvgAllocationSize", memoryIncrease / (double)allocationsCount, "bytes");
            
            return memoryIncrease;
        }

        /// <summary>
        /// Custom object pool implementation
        /// </summary>
        [Benchmark]
        public long Allocation_Optimized_CustomPool()
        {
            var initialMemory = GC.GetTotalMemory(false);
            var objectPool = new CustomObjectPool<PooledObject>(() => new PooledObject());
            var allocationsCount = 0;
            
            // Use custom object pool
            for (int i = 0; i < ALLOCATION_COUNT; i++)
            {
                using var pooledObj = objectPool.Get();
                var obj = pooledObj.Object;
                obj.Data[0] = (byte)i;
                
                // Simulate work with the object
                ProcessPooledObject(obj);
                allocationsCount++;
            }
            
            var finalMemory = GC.GetTotalMemory(false);
            var memoryIncrease = finalMemory - initialMemory;
            
            _monitor.RecordBenchmarkMetric("MemoryUsage", "CustomPool_Allocations", allocationsCount, "count");
            _monitor.RecordBenchmarkMetric("MemoryUsage", "CustomPool_MemoryIncrease", memoryIncrease, "bytes");
            _monitor.RecordBenchmarkMetric("MemoryUsage", "CustomPool_AvgAllocationSize", memoryIncrease / (double)allocationsCount, "bytes");
            
            return memoryIncrease;
        }

        /// <summary>
        /// Memory allocation with stack allocation for small objects
        /// </summary>
        [Benchmark]
        public long Allocation_Optimized_StackAlloc()
        {
            var initialMemory = GC.GetTotalMemory(false);
            var allocationsCount = 0;
            
            // Use stack allocation for very small objects
            for (int i = 0; i < ALLOCATION_COUNT / 10; i++)
            {
                Span<byte> stackBuffer = stackalloc byte[256];
                
                // Initialize the stack buffer
                for (int j = 0; j < stackBuffer.Length; j++)
                {
                    stackBuffer[j] = (byte)_random.Next(256);
                }
                
                // Process the stack buffer
                ProcessStackBuffer(stackBuffer);
                allocationsCount++;
            }
            
            var finalMemory = GC.GetTotalMemory(false);
            var memoryIncrease = finalMemory - initialMemory;
            
            _monitor.RecordBenchmarkMetric("MemoryUsage", "StackAlloc_Allocations", allocationsCount, "count");
            _monitor.RecordBenchmarkMetric("MemoryUsage", "StackAlloc_MemoryIncrease", memoryIncrease, "bytes");
            _monitor.RecordBenchmarkMetric("MemoryUsage", "StackAlloc_AvgAllocationSize", memoryIncrease / (double)allocationsCount, "bytes");
            
            return memoryIncrease;
        }

        /// <summary>
        /// GC pressure test with large number of allocations
        /// </summary>
        [Benchmark]
        public long GC_Pressure_LargeAllocations()
        {
            var initialGen0 = GC.CollectionCount(0);
            var initialGen1 = GC.CollectionCount(1);
            var initialGen2 = GC.CollectionCount(2);
            var initialMemory = GC.GetTotalMemory(false);
            
            // Create memory pressure with various sized allocations
            for (int i = 0; i < ALLOCATION_COUNT / 10; i++)
            {
                var smallObj = new byte[SMALL_ALLOCATION_SIZE];
                var mediumObj = new byte[MEDIUM_ALLOCATION_SIZE];
                var largeObj = new byte[LARGE_ALLOCATION_SIZE];
                
                // Fill with data to ensure allocation
                FillWithData(smallObj);
                FillWithData(mediumObj);
                FillWithData(largeObj);
                
                // Process objects
                ProcessAllocation(smallObj);
                ProcessAllocation(mediumObj);
                ProcessAllocation(largeObj);
            }
            
            // Force GC collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var finalGen0 = GC.CollectionCount(0);
            var finalGen1 = GC.CollectionCount(1);
            var finalGen2 = GC.CollectionCount(2);
            var finalMemory = GC.GetTotalMemory(false);
            
            var gen0Collections = finalGen0 - initialGen0;
            var gen1Collections = finalGen1 - initialGen1;
            var gen2Collections = finalGen2 - initialGen2;
            var memoryIncrease = finalMemory - initialMemory;
            
            _monitor.RecordBenchmarkMetric("MemoryUsage", "GCPressure_Gen0Collections", gen0Collections, "count");
            _monitor.RecordBenchmarkMetric("MemoryUsage", "GCPressure_Gen1Collections", gen1Collections, "count");
            _monitor.RecordBenchmarkMetric("MemoryUsage", "GCPressure_Gen2Collections", gen2Collections, "count");
            _monitor.RecordBenchmarkMetric("MemoryUsage", "GCPressure_MemoryIncrease", memoryIncrease, "bytes");
            
            return memoryIncrease;
        }

        /// <summary>
        /// Memory leak detection test
        /// </summary>
        [Benchmark]
        public long MemoryLeak_Detection()
        {
            var initialMemory = GC.GetTotalMemory(false);
            var leakedObjects = new List<object>();
            
            // Simulate potential memory leaks
            for (int i = 0; i < 100; i++)
            {
                var leakObj = CreateLeakingObject();
                leakedObjects.Add(leakObj);
                
                // Simulate work that might hold references
                SimulateEventSubscription(leakObj);
            }
            
            // Simulate cleanup that doesn't properly dispose
            foreach (var obj in leakedObjects)
            {
                SimulateIncompleteCleanup(obj);
            }
            
            // leakedObjects list still holds references
            var finalMemory = GC.GetTotalMemory(false);
            var memoryIncrease = finalMemory - initialMemory;
            
            _monitor.RecordBenchmarkMetric("MemoryUsage", "LeakDetection_PotentialLeaks", leakedObjects.Count, "count");
            _monitor.RecordBenchmarkMetric("MemoryUsage", "LeakDetection_MemoryIncrease", memoryIncrease, "bytes");
            
            // Cleanup for next test
            leakedObjects.Clear();
            
            return memoryIncrease;
        }

        /// <summary>
        /// Large Object Heap (LOH) allocation test
        /// </summary>
        [Benchmark]
        public long LargeObjectHeap_Allocations()
        {
            var initialMemory = GC.GetTotalMemory(false);
            var lohAllocations = new List<byte[]>();
            
            // Allocate large objects that go to LOH (>85KB)
            for (int i = 0; i < 10; i++)
            {
                var largeObj = new byte[VERY_LARGE_ALLOCATION_SIZE];
                FillWithData(largeObj);
                lohAllocations.Add(largeObj);
                
                // Process large object
                ProcessLargeObject(largeObj);
            }
            
            var finalMemory = GC.GetTotalMemory(false);
            var memoryIncrease = finalMemory - initialMemory;
            
            _monitor.RecordBenchmarkMetric("MemoryUsage", "LOH_Allocations", lohAllocations.Count, "count");
            _monitor.RecordBenchmarkMetric("MemoryUsage", "LOH_MemoryIncrease", memoryIncrease, "bytes");
            _monitor.RecordBenchmarkMetric("MemoryUsage", "LOH_AvgObjectSize", memoryIncrease / (double)lohAllocations.Count, "bytes");
            
            // Cleanup
            lohAllocations.Clear();
            
            return memoryIncrease;
        }

        /// <summary>
        /// Memory fragmentation test
        /// </summary>
        [Benchmark]
        public long MemoryFragmentation_Test()
        {
            var initialMemory = GC.GetTotalMemory(false);
            var smallAllocations = new List<byte[]>();
            var mediumAllocations = new List<byte[]>();
            var largeAllocations = new List<byte[]>();
            
            // Allocate in pattern that creates fragmentation
            for (int i = 0; i < 100; i++)
            {
                // Small allocation
                var small = new byte[1024];
                FillWithData(small);
                smallAllocations.Add(small);
                
                // Medium allocation
                var medium = new byte[8192];
                FillWithData(medium);
                mediumAllocations.Add(medium);
                
                // Large allocation
                var large = new byte[65536];
                FillWithData(large);
                largeAllocations.Add(large);
            }
            
            // Free every other allocation to create fragmentation
            for (int i = 0; i < smallAllocations.Count; i += 2)
            {
                smallAllocations[i] = null;
            }
            
            for (int i = 0; i < mediumAllocations.Count; i += 3)
            {
                mediumAllocations[i] = null;
            }
            
            for (int i = 0; i < largeAllocations.Count; i += 4)
            {
                largeAllocations[i] = null;
            }
            
            var afterFreeMemory = GC.GetTotalMemory(false);
            
            // Force compaction attempt
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var finalMemory = GC.GetTotalMemory(false);
            var memoryIncrease = finalMemory - initialMemory;
            
            _monitor.RecordBenchmarkMetric("MemoryUsage", "Fragmentation_SmallAllocations", smallAllocations.Count, "count");
            _monitor.RecordBenchmarkMetric("MemoryUsage", "Fragmentation_MediumAllocations", mediumAllocations.Count, "count");
            _monitor.RecordBenchmarkMetric("MemoryUsage", "Fragmentation_LargeAllocations", largeAllocations.Count, "count");
            _monitor.RecordBenchmarkMetric("MemoryUsage", "Fragmentation_MemoryIncrease", memoryIncrease, "bytes");
            
            // Cleanup
            smallAllocations.Clear();
            mediumAllocations.Clear();
            largeAllocations.Clear();
            
            return memoryIncrease;
        }

        /// <summary>
        /// Memory pooling with different pool sizes
        /// </summary>
        [Benchmark]
        public long MemoryPooling_Performance()
        {
            var initialMemory = GC.GetTotalMemory(false);
            var pools = new[]
            {
                System.Buffers.ArrayPool<byte>.Shared,
                System.Buffers.ArrayPool<byte>.Create(1024, 50),
                System.Buffers.ArrayPool<byte>.Create(4096, 25)
            };
            
            var totalAllocations = 0;
            
            // Use different pool sizes for different allocation patterns
            for (int i = 0; i < ALLOCATION_COUNT; i++)
            {
                var poolIndex = i % pools.Length;
                var pool = pools[poolIndex];
                
                int bufferSize = poolIndex == 0 ? 1024 : poolIndex == 1 ? 2048 : 4096;
                var buffer = pool.Rent(bufferSize);
                
                // Fill and process buffer
                for (int j = 0; j < buffer.Length; j++)
                {
                    buffer[j] = (byte)_random.Next(256);
                }
                
                ProcessBuffer(buffer);
                
                pool.Return(buffer);
                totalAllocations++;
            }
            
            var finalMemory = GC.GetTotalMemory(false);
            var memoryIncrease = finalMemory - initialMemory;
            
            _monitor.RecordBenchmarkMetric("MemoryUsage", "Pooling_TotalAllocations", totalAllocations, "count");
            _monitor.RecordBenchmarkMetric("MemoryUsage", "Pooling_MemoryIncrease", memoryIncrease, "bytes");
            _monitor.RecordBenchmarkMetric("MemoryUsage", "Pooling_AvgAllocationSize", memoryIncrease / (double)totalAllocations, "bytes");
            
            return memoryIncrease;
        }

        /// <summary>
        /// Memory allocation pattern analysis
        /// </summary>
        [Benchmark]
        public MemoryAllocationPattern AllocationPattern_Analysis()
        {
            var initialMemory = GC.GetTotalMemory(false);
            var allocations = new List<AllocationRecord>();
            
            // Simulate different allocation patterns
            var patterns = new[]
            {
                () => AllocateSmallObject(),
                () => AllocateMediumObject(),
                () => AllocateLargeObject(),
                () => AllocateArray(),
                () => AllocateString()
            };
            
            for (int i = 0; i < ALLOCATION_COUNT; i++)
            {
                var patternIndex = i % patterns.Length;
                var allocation = patterns[patternIndex]();
                allocations.Add(allocation);
                
                // Process allocation
                ProcessAllocationRecord(allocation);
            }
            
            var finalMemory = GC.GetTotalMemory(false);
            var memoryIncrease = finalMemory - initialMemory;
            
            // Analyze allocation patterns
            var patternAnalysis = AnalyzeAllocationPatterns(allocations);
            
            _monitor.RecordBenchmarkMetric("MemoryUsage", "PatternAnalysis_TotalAllocations", allocations.Count, "count");
            _monitor.RecordBenchmarkMetric("MemoryUsage", "PatternAnalysis_MemoryIncrease", memoryIncrease, "bytes");
            _monitor.RecordBenchmarkMetric("MemoryUsage", "PatternAnalysis_SmallObjects", patternAnalysis.SmallObjectCount, "count");
            _monitor.RecordBenchmarkMetric("MemoryUsage", "PatternAnalysis_MediumObjects", patternAnalysis.MediumObjectCount, "count");
            _monitor.RecordBenchmarkMetric("MemoryUsage", "PatternAnalysis_LargeObjects", patternAnalysis.LargeObjectCount, "count");
            
            // Cleanup
            allocations.Clear();
            
            return patternAnalysis;
        }

        // Helper methods
        private void ProcessSmallObject(object obj)
        {
            var bytes = (byte[])obj;
            var checksum = 0;
            for (int i = 0; i < bytes.Length; i += 64)
            {
                checksum += bytes[i];
            }
        }

        private void ProcessPooledObject(PooledObject obj)
        {
            var checksum = 0;
            for (int i = 0; i < obj.Data.Length; i += 64)
            {
                checksum += obj.Data[i];
            }
        }

        private void ProcessStackBuffer(Span<byte> buffer)
        {
            var checksum = 0;
            for (int i = 0; i < buffer.Length; i += 64)
            {
                checksum += buffer[i];
            }
        }

        private void ProcessAllocation(byte[] allocation)
        {
            var checksum = 0;
            for (int i = 0; i < allocation.Length; i += 128)
            {
                checksum += allocation[i];
            }
        }

        private void ProcessLargeObject(byte[] obj)
        {
            var checksum = 0;
            for (int i = 0; i < obj.Length; i += 256)
            {
                checksum += obj[i];
            }
        }

        private void ProcessBuffer(byte[] buffer)
        {
            var checksum = 0;
            for (int i = 0; i < buffer.Length; i += 128)
            {
                checksum += buffer[i];
            }
        }

        private static void FillWithData(byte[] array)
        {
            var random = new Random(42);
            random.NextBytes(array);
        }

        private object CreateLeakingObject()
        {
            return new LeakingObject
            {
                Data = new byte[1024],
                Timestamp = DateTime.UtcNow
            };
        }

        private void SimulateEventSubscription(object obj)
        {
            // Simulate event subscription that creates strong references
            var leakingObj = (LeakingObject)obj;
            leakingObj.OnDispose += () => { /* This creates a strong reference */ };
        }

        private void SimulateIncompleteCleanup(object obj)
        {
            // Simulate cleanup that doesn't properly dispose resources
            var leakingObj = (LeakingObject)obj;
            // Missing: leakingObj.Dispose();
        }

        private AllocationRecord AllocateSmallObject()
        {
            return new AllocationRecord
            {
                Type = AllocationType.SmallObject,
                Size = SMALL_ALLOCATION_SIZE,
                Timestamp = DateTime.UtcNow
            };
        }

        private AllocationRecord AllocateMediumObject()
        {
            return new AllocationRecord
            {
                Type = AllocationType.MediumObject,
                Size = MEDIUM_ALLOCATION_SIZE,
                Timestamp = DateTime.UtcNow
            };
        }

        private AllocationRecord AllocateLargeObject()
        {
            return new AllocationRecord
            {
                Type = AllocationType.LargeObject,
                Size = LARGE_ALLOCATION_SIZE,
                Timestamp = DateTime.UtcNow
            };
        }

        private AllocationRecord AllocateArray()
        {
            return new AllocationRecord
            {
                Type = AllocationType.Array,
                Size = 2048,
                Timestamp = DateTime.UtcNow
            };
        }

        private AllocationRecord AllocateString()
        {
            return new AllocationRecord
            {
                Type = AllocationType.String,
                Size = 100,
                Timestamp = DateTime.UtcNow
            };
        }

        private void ProcessAllocationRecord(AllocationRecord record)
        {
            // Simulate processing the allocation record
            var dummy = record.Size * record.Type.ToString().Length;
        }

        private MemoryAllocationPattern AnalyzeAllocationPatterns(List<AllocationRecord> allocations)
        {
            return new MemoryAllocationPattern
            {
                SmallObjectCount = allocations.Count(a => a.Type == AllocationType.SmallObject),
                MediumObjectCount = allocations.Count(a => a.Type == AllocationType.MediumObject),
                LargeObjectCount = allocations.Count(a => a.Type == AllocationType.LargeObject),
                ArrayCount = allocations.Count(a => a.Type == AllocationType.Array),
                StringCount = allocations.Count(a => a.Type == AllocationType.String),
                TotalAllocations = allocations.Count
            };
        }

        // Supporting data structures
        private class PooledObject : IDisposable
        {
            public byte[] Data { get; } = new byte[1024];
            
            public void Dispose()
            {
                // Pool will reset/reuse this object
            }
        }

        private class CustomObjectPool<T> where T : class, IDisposable
        {
            private readonly Stack<T> _pool = new Stack<T>();
            private readonly Func<T> _factory;
            private readonly object _lock = new object();

            public CustomObjectPool(Func<T> factory)
            {
                _factory = factory;
            }

            public PooledDisposable<T> Get()
            {
                lock (_lock)
                {
                    var item = _pool.Count > 0 ? _pool.Pop() : _factory();
                    return new PooledDisposable<T>(item, this);
                }
            }

            public void Return(T item)
            {
                lock (_lock)
                {
                    if (_pool.Count < 100) // Max pool size
                    {
                        _pool.Push(item);
                    }
                }
            }
        }

        private class PooledDisposable<T> : IDisposable where T : class
        {
            private readonly CustomObjectPool<T> _pool;

            public T Object { get; }

            public PooledDisposable(T item, CustomObjectPool<T> pool)
            {
                Object = item;
                _pool = pool;
            }

            public void Dispose()
            {
                _pool.Return(Object);
            }
        }

        private class LeakingObject
        {
            public byte[] Data { get; set; } = Array.Empty<byte>();
            public DateTime Timestamp { get; set; }
            public event Action OnDispose;
            
            public void Dispose()
            {
                OnDispose?.Invoke();
            }
        }

        private class MemoryMeasurement
        {
            public DateTime Timestamp { get; set; }
            public long MemoryUsed { get; set; }
            public int Gen0Collections { get; set; }
            public int Gen1Collections { get; set; }
            public int Gen2Collections { get; set; }
        }

        private class AllocationRecord
        {
            public AllocationType Type { get; set; }
            public int Size { get; set; }
            public DateTime Timestamp { get; set; }
        }

        private class MemoryAllocationPattern
        {
            public int SmallObjectCount { get; set; }
            public int MediumObjectCount { get; set; }
            public int LargeObjectCount { get; set; }
            public int ArrayCount { get; set; }
            public int StringCount { get; set; }
            public int TotalAllocations { get; set; }
        }

        private enum AllocationType
        {
            SmallObject,
            MediumObject,
            LargeObject,
            Array,
            String
        }
    }
}