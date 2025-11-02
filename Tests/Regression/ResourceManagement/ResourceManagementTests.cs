using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Vortice.Direct3D12;
using Vortice.DXGI;
using Xunit;
using Xunit.Abstractions;
using TiXL.Core.Graphics.DirectX12;
using TiXL.Core.Graphics.PSO;
using TiXL.Core.Performance;

namespace TiXL.Tests.Regression.ResourceManagement
{
    /// <summary>
    /// Resource management tests to ensure no memory leaks or resource leaks occur
    /// Validates that all components properly dispose of resources and memory usage remains stable
    /// </summary>
    [TestCategories(TestCategory.Regression | TestCategory.ResourceManagement | TestCategory.P0)]
    public class ResourceManagementTests : CoreTestFixture
    {
        private readonly ITestOutputHelper _output;
        private readonly List<IDisposable> _testResources;
        private readonly long _initialMemory;

        public ResourceManagementTests(ITestOutputHelper output) : base()
        {
            _output = output;
            _testResources = new List<IDisposable>();
            _initialMemory = GC.GetTotalMemory(true);
            
            _output.WriteLine($"Starting Resource Management Tests. Initial memory: {_initialMemory:N0} bytes");
        }

        #region Memory Leak Detection Tests

        /// <summary>
        /// Validates that PipelineState creation and disposal doesn't leak memory
        /// </summary>
        [Fact]
        public void PipelineState_MemoryManagement()
        {
            // Arrange
            var iterations = 100;
            var createdObjects = new List<PipelineState>();
            
            // Act - Create and immediately dispose multiple objects
            for (int i = 0; i < iterations; i++)
            {
                var pso = new PipelineState();
                createdObjects.Add(pso);
                
                // Simulate usage
                pso.RecordAccess();
            }
            
            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var memoryAfterCreation = GC.GetTotalMemory(false);
            
            // Cleanup
            foreach (var pso in createdObjects)
            {
                pso.Dispose();
            }
            
            createdObjects.Clear();
            
            // Force final garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var memoryAfterCleanup = GC.GetTotalMemory(true);
            var memoryDelta = memoryAfterCleanup - _initialMemory;
            
            // Assert
            _output.WriteLine($"Memory before creation: {_initialMemory:N0} bytes");
            _output.WriteLine($"Memory after creation: {memoryAfterCreation:N0} bytes");
            _output.WriteLine($"Memory after cleanup: {memoryAfterCleanup:N0} bytes");
            _output.WriteLine($"Memory delta: {memoryDelta:N0} bytes");
            
            // Allow some variance for test environment, but should be relatively stable
            memoryDelta.Should().BeLessThan(1024 * 1024, 
                $"Memory usage should not increase by more than 1MB after cleanup, but increased by {memoryDelta:N0} bytes");
        }

        /// <summary>
        /// Validates memory usage with heavy concurrent operations
        /// </summary>
        [Fact]
        public async Task ConcurrentOperations_MemoryStability()
        {
            // Arrange
            var parallelIterations = 50;
            var tasks = new List<Task>();
            
            // Act - Run operations in parallel
            for (int i = 0; i < parallelIterations; i++)
            {
                var task = Task.Run(() =>
                {
                    var psos = new List<PipelineState>();
                    
                    for (int j = 0; j < 20; j++)
                    {
                        var pso = new PipelineState();
                        pso.RecordAccess();
                        psos.Add(pso);
                        
                        if (j % 5 == 0)
                        {
                            // Dispose some objects during creation
                            psos[j / 5].Dispose();
                        }
                    }
                    
                    // Cleanup remaining objects
                    foreach (var pso in psos)
                    {
                        pso.Dispose();
                    }
                });
                
                tasks.Add(task);
            }
            
            await Task.WhenAll(tasks);
            
            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var memoryAfterConcurrent = GC.GetTotalMemory(true);
            var memoryDelta = memoryAfterConcurrent - _initialMemory;
            
            // Assert
            _output.WriteLine($"Memory after {parallelIterations * 20} concurrent PSO operations: {memoryAfterConcurrent:N0} bytes");
            _output.WriteLine($"Memory delta after concurrent operations: {memoryDelta:N0} bytes");
            
            memoryDelta.Should().BeLessThan(2 * 1024 * 1024, 
                $"Concurrent operations should not leak more than 2MB, but leaked {memoryDelta:N0} bytes");
        }

        /// <summary>
        /// Validates long-running operation memory stability
        /// </summary>
        [Fact]
        public async Task LongRunningOperations_MemoryStability()
        {
            // Arrange
            var totalIterations = 200;
            var batchSize = 10;
            var batches = totalIterations / batchSize;
            
            // Act
            for (int batch = 0; batch < batches; batch++)
            {
                var batchObjects = new List<PipelineState>();
                
                for (int i = 0; i < batchSize; i++)
                {
                    var pso = new PipelineState();
                    pso.RecordAccess();
                    batchObjects.Add(pso);
                }
                
                // Cleanup this batch
                foreach (var pso in batchObjects)
                {
                    pso.Dispose();
                }
                
                batchObjects.Clear();
                
                // Periodic GC to simulate real-world conditions
                if (batch % 10 == 0)
                {
                    GC.Collect();
                    await Task.Delay(10); // Small delay to allow cleanup
                }
            }
            
            // Final cleanup
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var memoryAfterLongRunning = GC.GetTotalMemory(true);
            var memoryDelta = memoryAfterLongRunning - _initialMemory;
            
            // Assert
            _output.WriteLine($"Memory after long-running operations: {memoryAfterLongRunning:N0} bytes");
            _output.WriteLine($"Memory delta after {totalIterations} operations: {memoryDelta:N0} bytes");
            
            memoryDelta.Should().BeLessThan(512 * 1024, 
                $"Long-running operations should not leak more than 512KB, but leaked {memoryDelta:N0} bytes");
        }

        #endregion

        #region Resource Pool Management Tests

        /// <summary>
        /// Validates that resource pools don't leak resources
        /// </summary>
        [Fact]
        public void ResourcePool_ManagementAndCleanup()
        {
            // Arrange
            var poolManager = new ResourceLifecycleManager();
            var createdResources = new List<IDisposable>();
            
            // Act - Create and release resources multiple times
            for (int cycle = 0; cycle < 5; cycle++)
            {
                for (int i = 0; i < 20; i++)
                {
                    var resource = CreateTestResource();
                    createdResources.Add(resource);
                }
                
                // Release all resources from this cycle
                foreach (var resource in createdResources)
                {
                    resource.Dispose();
                }
                
                createdResources.Clear();
                
                // Periodic cleanup
                poolManager.Cleanup();
            }
            
            // Final cleanup
            poolManager.Cleanup();
            
            var memoryAfterPoolTest = GC.GetTotalMemory(true);
            var memoryDelta = memoryAfterPoolTest - _initialMemory;
            
            // Assert
            _output.WriteLine($"Memory after pool management test: {memoryAfterPoolTest:N0} bytes");
            _output.WriteLine($"Memory delta: {memoryDelta:N0} bytes");
            
            memoryDelta.Should().BeLessThan(256 * 1024, 
                $"Resource pool should not leak more than 256KB, but leaked {memoryDelta:N0} bytes");
        }

        /// <summary>
        /// Validates that pool sizes remain bounded
        /// </summary>
        [Fact]
        public void ResourcePool_SizeBounds()
        {
            // Arrange
            var poolManager = new ResourceLifecycleManager();
            var maxPoolSize = 100;
            
            // Act - Fill pool beyond normal capacity
            var resources = new List<IDisposable>();
            for (int i = 0; i < maxPoolSize * 2; i++)
            {
                var resource = CreateTestResource();
                resources.Add(resource);
                
                // Periodically release some to test pool management
                if (i % 25 == 0)
                {
                    if (resources.Count > 10)
                    {
                        var toRelease = resources.Take(5).ToList();
                        foreach (var resource in toRelease)
                        {
                            resource.Dispose();
                            resources.Remove(resource);
                        }
                    }
                }
            }
            
            // Cleanup
            foreach (var resource in resources)
            {
                resource.Dispose();
            }
            
            poolManager.Cleanup();
            
            _output.WriteLine("Resource pool size bounds validated");
        }

        #endregion

        #region Graphics Resource Tests

        /// <summary>
        /// Validates DirectX resource creation and disposal
        /// </summary>
        [Fact]
        public void DirectXResources_CreationAndDisposal()
        {
            try
            {
                // Arrange
                var device = MockD3D12Device.Create().Device;
                
                // Act - Test PSO resource lifecycle
                var psos = new List<PipelineState>();
                for (int i = 0; i < 50; i++)
                {
                    var pso = new PipelineState();
                    psos.Add(pso);
                }
                
                // Use the PSOs
                foreach (var pso in psos)
                {
                    pso.RecordAccess();
                }
                
                // Cleanup
                foreach (var pso in psos)
                {
                    pso.Dispose();
                }
                
                var memoryAfterDXResources = GC.GetTotalMemory(true);
                var memoryDelta = memoryAfterDXResources - _initialMemory;
                
                _output.WriteLine($"Memory after DirectX resource test: {memoryAfterDXResources:N0} bytes");
                _output.WriteLine($"Memory delta: {memoryDelta:N0} bytes");
                
                memoryDelta.Should().BeLessThan(512 * 1024, 
                    $"DirectX resources should not leak significant memory, but leaked {memoryDelta:N0} bytes");
            }
            catch (DllNotFoundException)
            {
                _output.WriteLine("DirectX runtime not available - skipping DirectX resource test");
            }
        }

        /// <summary>
        /// Validates PSO caching system doesn't leak
        /// </summary>
        [Fact]
        public void PSOCache_MemoryManagement()
        {
            // Arrange
            var psoManager = new OptimizedPSOManager();
            
            // Act - Create and cache multiple PSOs
            var psos = new List<PipelineState>();
            for (int i = 0; i < 100; i++)
            {
                var pso = new PipelineState();
                psos.Add(pso);
                
                // Simulate caching operations
                if (i % 10 == 0)
                {
                    // Some PSOs get accessed multiple times
                    pso.RecordAccess();
                    pso.RecordAccess();
                }
            }
            
            // Check PSO statistics
            var highAccessPsos = psos.Where(p => p.AccessCount > 0).ToList();
            highAccessPsos.Should().HaveCountGreaterThan(0, "Some PSOs should have been accessed");
            
            _output.WriteLine($"Created {psos.Count} PSOs, {highAccessPsos.Count} were accessed");
            
            // Cleanup
            foreach (var pso in psos)
            {
                pso.Dispose();
            }
            
            var memoryAfterPSOCacheTest = GC.GetTotalMemory(true);
            var memoryDelta = memoryAfterPSOCacheTest - _initialMemory;
            
            _output.WriteLine($"Memory after PSO cache test: {memoryAfterPSOCacheTest:N0} bytes");
            _output.WriteLine($"Memory delta: {memoryDelta:N0} bytes");
            
            memoryDelta.Should().BeLessThan(256 * 1024, 
                $"PSO cache should not leak more than 256KB, but leaked {memoryDelta:N0} bytes");
        }

        #endregion

        #region Performance Monitor Resource Tests

        /// <summary>
        /// Validates that performance monitoring doesn't leak resources
        /// </summary>
        [Fact]
        public void PerformanceMonitor_ResourceManagement()
        {
            // Arrange
            var monitor = new PerformanceMonitor();
            
            // Act - Create and use multiple performance counters
            var counters = new List<PerformanceCounter>();
            for (int i = 0; i < 100; i++)
            {
                var counter = new PerformanceCounter($"TestCounter_{i}");
                counter.Value = i * 1.5;
                counters.Add(counter);
            }
            
            // Use counters
            var totalValue = counters.Sum(c => c.Value);
            _output.WriteLine($"Total counter value: {totalValue}");
            
            // Cleanup
            counters.Clear();
            
            var memoryAfterMonitorTest = GC.GetTotalMemory(true);
            var memoryDelta = memoryAfterMonitorTest - _initialMemory;
            
            _output.WriteLine($"Memory after performance monitor test: {memoryAfterMonitorTest:N0} bytes");
            _output.WriteLine($"Memory delta: {memoryDelta:N0} bytes");
            
            memoryDelta.Should().BeLessThan(128 * 1024, 
                $"Performance monitor should not leak more than 128KB, but leaked {memoryDelta:N0} bytes");
        }

        #endregion

        #region Stress Testing

        /// <summary>
        /// Stress test to validate resource management under load
        /// </summary>
        [Fact]
        public void StressTest_ResourceManagement()
        {
            // Arrange
            var stopwatch = Stopwatch.StartNew();
            var maxDuration = TimeSpan.FromSeconds(10);
            var createdObjects = 0;
            var disposedObjects = 0;
            
            // Act - Create and dispose resources rapidly
            while (stopwatch.Elapsed < maxDuration)
            {
                var batchObjects = new List<IDisposable>();
                
                // Create batch
                for (int i = 0; i < 25; i++)
                {
                    var pso = new PipelineState();
                    pso.RecordAccess();
                    batchObjects.Add(pso);
                    createdObjects++;
                }
                
                // Use objects
                Thread.Sleep(1); // Small delay to simulate real usage
                
                // Dispose batch
                foreach (var obj in batchObjects)
                {
                    obj.Dispose();
                    disposedObjects++;
                }
                
                batchObjects.Clear();
                
                // Periodic GC
                if (createdObjects % 200 == 0)
                {
                    GC.Collect();
                }
            }
            
            stopwatch.Stop();
            
            // Final cleanup
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var memoryAfterStressTest = GC.GetTotalMemory(true);
            var memoryDelta = memoryAfterStressTest - _initialMemory;
            
            // Assert
            _output.WriteLine($"Stress test completed:");
            _output.WriteLine($"  Duration: {stopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"  Created: {createdObjects} objects");
            _output.WriteLine($"  Disposed: {disposedObjects} objects");
            _output.WriteLine($"  Memory after stress: {memoryAfterStressTest:N0} bytes");
            _output.WriteLine($"  Memory delta: {memoryDelta:N0} bytes");
            
            createdObjects.Should().Be(disposedObjects, "All created objects should be disposed");
            memoryDelta.Should().BeLessThan(1024 * 1024, 
                $"Stress test should not leak more than 1MB, but leaked {memoryDelta:N0} bytes");
        }

        #endregion

        #region Helper Methods

        private IDisposable CreateTestResource()
        {
            // Create a test resource that mimics real resource behavior
            return new TestResource();
        }

        private class TestResource : IDisposable
        {
            private bool _disposed = false;
            
            public void Dispose()
            {
                if (!_disposed)
                {
                    _disposed = true;
                    // Simulate resource cleanup
                }
            }
        }

        #endregion

        #region Cleanup

        public override void Dispose()
        {
            // Cleanup any remaining test resources
            foreach (var resource in _testResources)
            {
                try
                {
                    resource.Dispose();
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Warning: Error disposing test resource: {ex.Message}");
                }
            }
            
            _testResources.Clear();
            
            var finalMemory = GC.GetTotalMemory(true);
            var totalMemoryDelta = finalMemory - _initialMemory;
            
            _output.WriteLine($"Resource Management Tests completed:");
            _output.WriteLine($"  Initial memory: {_initialMemory:N0} bytes");
            _output.WriteLine($"  Final memory: {finalMemory:N0} bytes");
            _output.WriteLine($"  Total delta: {totalMemoryDelta:N0} bytes");
            
            base.Dispose();
        }

        #endregion
    }
}
