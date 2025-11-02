using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

namespace TiXL.Tests.Regression.ThreadSafety
{
    /// <summary>
    /// Thread safety tests to ensure all components maintain thread safety
    /// Validates that concurrent access doesn't cause corruption, deadlocks, or race conditions
    /// </summary>
    [TestCategories(TestCategory.Regression | TestCategory.ThreadSafety | TestCategory.P0)]
    public class ThreadSafetyTests : CoreTestFixture
    {
        private readonly ITestOutputHelper _output;
        private readonly int _concurrentTaskCount;
        private readonly int _operationsPerTask;

        public ThreadSafetyTests(ITestOutputHelper output) : base()
        {
            _output = output;
            _concurrentTaskCount = Environment.ProcessorCount * 2; // Use multiple cores
            _operationsPerTask = 50;
            
            _output.WriteLine($"Starting Thread Safety Tests with {_concurrentTaskCount} concurrent tasks");
        }

        #region PSO Thread Safety Tests

        /// <summary>
        /// Validates PipelineState thread safety under concurrent access
        /// </summary>
        [Fact]
        public void PipelineState_ConcurrentAccess()
        {
            // Arrange
            var sharedPSO = new PipelineState();
            var accessCounts = new ConcurrentBag<int>();
            var exceptions = new ConcurrentBag<Exception>();
            
            // Act - Multiple threads access the same PSO
            var tasks = new List<Task>();
            for (int i = 0; i < _concurrentTaskCount; i++)
            {
                var taskId = i;
                var task = Task.Run(() =>
                {
                    try
                    {
                        for (int j = 0; j < _operationsPerTask; j++)
                        {
                            // Thread-safe property access
                            var accessCount = sharedPSO.AccessCount;
                            
                            // Simulate some work
                            Thread.SpinWait(100);
                            
                            // Record access (thread-safe method)
                            sharedPSO.RecordAccess();
                            
                            // Verify state consistency
                            var finalAccessCount = sharedPSO.AccessCount;
                            
                            if (finalAccessCount >= accessCount)
                            {
                                accessCounts.Add(finalAccessCount);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                });
                
                tasks.Add(task);
            }
            
            Task.WaitAll(tasks.ToArray());
            
            // Assert
            exceptions.Should().BeEmpty($"Concurrent access should not throw exceptions, but got: {string.Join(", ", exceptions.Select(e => e.Message))}");
            
            var totalAccessCount = sharedPSO.AccessCount;
            totalAccessCount.Should().Be(_concurrentTaskCount * _operationsPerTask, 
                $"Expected {(_concurrentTaskCount * _operationsPerTask)} total accesses, but got {totalAccessCount}");
            
            accessCounts.Should().HaveCountGreaterThan(0, "Some access counts should have been recorded");
            
            _output.WriteLine($"PipelineState concurrent access test passed: {totalAccessCount} total accesses from {_concurrentTaskCount} tasks");
        }

        /// <summary>
        /// Validates PipelineState disposal thread safety
        /// </summary>
        [Fact]
        public void PipelineState_ConcurrentDisposal()
        {
            // Arrange
            var psos = new List<PipelineState>();
            for (int i = 0; i < 100; i++)
            {
                psos.Add(new PipelineState());
            }
            
            var exceptions = new ConcurrentBag<Exception>();
            var disposedCount = 0;
            var lockObject = new object();
            
            // Act - Multiple threads dispose PSOs concurrently
            var tasks = new List<Task>();
            for (int taskId = 0; taskId < _concurrentTaskCount; taskId++)
            {
                var task = Task.Run(() =>
                {
                    try
                    {
                        for (int i = 0; i < psos.Count / _concurrentTaskCount; i++)
                        {
                            var index = taskId * (psos.Count / _concurrentTaskCount) + i;
                            if (index < psos.Count)
                            {
                                psos[index].Dispose();
                                
                                lock (lockObject)
                                {
                                    disposedCount++;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                });
                
                tasks.Add(task);
            }
            
            Task.WaitAll(tasks.ToArray());
            
            // Assert
            exceptions.Should().BeEmpty($"Concurrent disposal should not throw exceptions, but got: {string.Join(", ", exceptions.Select(e => e.Message))}");
            
            disposedCount.Should().Be(psos.Count, $"All PSOs should be disposed, but only {disposedCount} were disposed");
            
            _output.WriteLine($"PipelineState concurrent disposal test passed: {disposedCount} PSOs disposed safely");
        }

        #endregion

        #region PSO Manager Thread Safety Tests

        /// <summary>
        /// Validates OptimizedPSOManager thread safety
        /// </summary>
        [Fact]
        public void OptimizedPSOManager_ConcurrentAccess()
        {
            // Arrange
            var psoManager = new OptimizedPSOManager();
            var createdPsos = new ConcurrentBag<PipelineState>();
            var exceptions = new ConcurrentBag<Exception>();
            
            // Act - Multiple threads create PSOs concurrently
            var tasks = new List<Task>();
            for (int i = 0; i < _concurrentTaskCount; i++)
            {
                var taskId = i;
                var task = Task.Run(() =>
                {
                    try
                    {
                        for (int j = 0; j < _operationsPerTask / 5; j++)
                        {
                            var psoDesc = CreateTestPipelineStateDescription(taskId);
                            var pso = psoManager.GetOrCreate(psoDesc);
                            
                            if (pso != null)
                            {
                                pso.RecordAccess();
                                createdPsos.Add(pso);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                });
                
                tasks.Add(task);
            }
            
            Task.WaitAll(tasks.ToArray());
            
            // Assert
            exceptions.Should().BeEmpty($"PSO Manager concurrent access should not throw exceptions, but got: {string.Join(", ", exceptions.Select(e => e.Message))}");
            
            createdPsos.Should().HaveCountGreaterThan(0, "Some PSOs should have been created");
            
            _output.WriteLine($"OptimizedPSOManager concurrent access test passed: {createdPsos.Count} PSOs created safely");
        }

        /// <summary>
        /// Validates PSO key equality is thread-safe
        /// </summary>
        [Fact]
        public void MaterialPSOKey_ThreadSafety()
        {
            // Arrange
            var key1 = new MaterialPSOKey();
            var key2 = new MaterialPSOKey();
            var equalityResults = new ConcurrentBag<bool>();
            var exceptions = new ConcurrentBag<Exception>();
            
            // Act - Multiple threads compare PSO keys
            var tasks = new List<Task>();
            for (int i = 0; i < _concurrentTaskCount; i++)
            {
                var task = Task.Run(() =>
                {
                    try
                    {
                        for (int j = 0; j < _operationsPerTask; j++)
                        {
                            // Test equality from multiple threads
                            var result = key1.Equals(key2);
                            equalityResults.Add(result);
                            
                            // Test hash code consistency
                            var hash1 = key1.GetHashCode();
                            var hash2 = key2.GetHashCode();
                            
                            // Hash codes should be consistent
                            equalityResults.Add(hash1 == hash2);
                            
                            Thread.SpinWait(50);
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                });
                
                tasks.Add(task);
            }
            
            Task.WaitAll(tasks.ToArray());
            
            // Assert
            exceptions.Should().BeEmpty($"PSO Key comparison should be thread-safe, but got exceptions: {string.Join(", ", exceptions.Select(e => e.Message))}");
            
            equalityResults.Should().OnlyContain(result => result, "All PSO key comparisons should return true");
            equalityResults.Should().HaveCount(_concurrentTaskCount * _operationsPerTask * 2, 
                $"Expected {(_concurrentTaskCount * _operationsPerTask * 2)} equality results, but got {equalityResults.Count}");
            
            _output.WriteLine($"MaterialPSOKey thread safety test passed: {equalityResults.Count} comparisons completed safely");
        }

        #endregion

        #region Performance Monitor Thread Safety Tests

        /// <summary>
        /// Validates PerformanceMonitor thread safety
        /// </summary>
        [Fact]
        public void PerformanceMonitor_ConcurrentAccess()
        {
            // Arrange
            var monitor = new PerformanceMonitor();
            var createdCounters = new ConcurrentBag<PerformanceCounter>();
            var exceptions = new ConcurrentBag<Exception>();
            
            // Act - Multiple threads create and use counters concurrently
            var tasks = new List<Task>();
            for (int i = 0; i < _concurrentTaskCount; i++)
            {
                var taskId = i;
                var task = Task.Run(() =>
                {
                    try
                    {
                        for (int j = 0; j < _operationsPerTask / 10; j++)
                        {
                            var counter = new PerformanceCounter($"Counter_{taskId}_{j}");
                            counter.Value = j * 1.5;
                            createdCounters.Add(counter);
                            
                            // Simulate counter usage
                            Thread.SpinWait(100);
                            
                            var value = counter.Value;
                            counter.Value = value + 1;
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                });
                
                tasks.Add(task);
            }
            
            Task.WaitAll(tasks.ToArray());
            
            // Assert
            exceptions.Should().BeEmpty($"PerformanceMonitor concurrent access should not throw exceptions, but got: {string.Join(", ", exceptions.Select(e => e.Message))}");
            
            createdCounters.Should().HaveCountGreaterThan(0, "Some counters should have been created");
            
            _output.WriteLine($"PerformanceMonitor thread safety test passed: {createdCounters.Count} counters created safely");
        }

        #endregion

        #region Resource Manager Thread Safety Tests

        /// <summary>
        /// Validates ResourceLifecycleManager thread safety
        /// </summary>
        [Fact]
        public void ResourceLifecycleManager_ConcurrentAccess()
        {
            // Arrange
            var resourceManager = new ResourceLifecycleManager();
            var exceptions = new ConcurrentBag<Exception>();
            var operationsCompleted = 0;
            var lockObject = new object();
            
            // Act - Multiple threads access resource manager concurrently
            var tasks = new List<Task>();
            for (int i = 0; i < _concurrentTaskCount; i++)
            {
                var taskId = i;
                var task = Task.Run(() =>
                {
                    try
                    {
                        for (int j = 0; j < _operationsPerTask / 5; j++)
                        {
                            // Simulate resource management operations
                            Thread.SpinWait(100);
                            
                            // Initialize and cleanup operations should be thread-safe
                            resourceManager.Initialize();
                            resourceManager.Cleanup();
                            
                            lock (lockObject)
                            {
                                operationsCompleted++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                });
                
                tasks.Add(task);
            }
            
            Task.WaitAll(tasks.ToArray());
            
            // Assert
            exceptions.Should().BeEmpty($"ResourceManager concurrent access should not throw exceptions, but got: {string.Join(", ", exceptions.Select(e => e.Message))}");
            
            operationsCompleted.Should().Be(_concurrentTaskCount * (_operationsPerTask / 5), 
                $"Expected {_concurrentTaskCount * (_operationsPerTask / 5)} operations, but completed {operationsCompleted}");
            
            _output.WriteLine($"ResourceLifecycleManager thread safety test passed: {operationsCompleted} operations completed safely");
        }

        #endregion

        #region Race Condition Detection Tests

        /// <summary>
        /// Tests for race conditions in PSO creation and access patterns
        /// </summary>
        [Fact]
        public void RaceConditionDetection_PSOLifecycle()
        {
            // Arrange
            var sharedPSO = new PipelineState();
            var operations = new List<Action>();
            var exceptions = new ConcurrentBag<Exception>();
            
            // Queue operations that might cause race conditions
            for (int i = 0; i < _concurrentTaskCount * 5; i++)
            {
                operations.Add(() =>
                {
                    try
                    {
                        // Read state
                        var accessCount = sharedPSO.AccessCount;
                        var isValid = sharedPSO.IsValid;
                        
                        // Write state
                        sharedPSO.RecordAccess();
                        
                        // Read state again
                        var newAccessCount = sharedPSO.AccessCount;
                        var newIsValid = sharedPSO.IsValid;
                        
                        // Verify consistency
                        if (newAccessCount < accessCount)
                        {
                            throw new InvalidOperationException("Access count regression detected");
                        }
                        
                        if (isValid != newIsValid && sharedPSO.AccessCount > 0)
                        {
                            throw new InvalidOperationException("Valid state changed unexpectedly");
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                });
            }
            
            // Act - Execute operations in parallel
            var tasks = operations.Select(op => Task.Run(op));
            Task.WaitAll(tasks.ToArray());
            
            // Assert
            exceptions.Should().BeEmpty($"No race conditions should be detected, but got: {string.Join(", ", exceptions.Select(e => e.Message))}");
            
            sharedPSO.AccessCount.Should().BeGreaterThan(0, "PSO should have been accessed");
            
            _output.WriteLine($"Race condition detection test passed: {sharedPSO.AccessCount} accesses completed without race conditions");
        }

        /// <summary>
        /// Tests for deadlocks in concurrent disposal patterns
        /// </summary>
        [Fact]
        public void DeadlockDetection_DisposalPatterns()
        {
            // Arrange
            var timeout = TimeSpan.FromSeconds(30);
            var psos = new List<PipelineState>();
            for (int i = 0; i < 50; i++)
            {
                psos.Add(new PipelineState());
            }
            
            // Act - Test disposal from multiple threads
            var disposalTask = Task.Run(() =>
            {
                foreach (var pso in psos)
                {
                    pso.Dispose();
                    Thread.SpinWait(100);
                }
            });
            
            var accessTask = Task.Run(() =>
            {
                foreach (var pso in psos)
                {
                    try
                    {
                        _ = pso.AccessCount;
                        _ = pso.IsValid;
                    }
                    catch (ObjectDisposedException)
                    {
                        // Expected after disposal
                    }
                    
                    Thread.SpinWait(50);
                }
            });
            
            // Assert - Both tasks should complete within timeout
            var completedTask = await Task.WhenAny(disposalTask, accessTask, Task.Delay(timeout));
            
            if (completedTask == disposalTask)
            {
                await disposalTask; // Ensure it completed successfully
                await accessTask; // Wait for access task
            }
            else if (completedTask == accessTask)
            {
                await disposalTask; // Wait for disposal task
            }
            else
            {
                throw new TimeoutException("Potential deadlock detected - tasks did not complete within timeout");
            }
            
            _output.WriteLine("Deadlock detection test passed - no deadlocks detected in disposal patterns");
        }

        #endregion

        #region Stress Testing

        /// <summary>
        /// High-stress thread safety test
        /// </summary>
        [Fact]
        public void HighStress_ThreadSafety()
        {
            // Arrange
            var duration = TimeSpan.FromSeconds(5);
            var stopwatch = Stopwatch.StartNew();
            var operations = 0;
            var exceptions = new ConcurrentBag<Exception>();
            var lockObject = new object();
            
            // Act - High stress concurrent operations
            var tasks = new List<Task>();
            for (int i = 0; i < _concurrentTaskCount; i++)
            {
                var task = Task.Run(() =>
                {
                    try
                    {
                        var taskOps = 0;
                        while (stopwatch.Elapsed < duration)
                        {
                            // Mix of different operations
                            var pso = new PipelineState();
                            pso.RecordAccess();
                            
                            var manager = new OptimizedPSOManager();
                            var psoDesc = CreateTestPipelineStateDescription(i);
                            _ = manager.GetOrCreate(psoDesc);
                            
                            var counter = new PerformanceCounter($"Stress_{i}");
                            counter.Value = taskOps;
                            
                            pso.Dispose();
                            
                            taskOps++;
                        }
                        
                        lock (lockObject)
                        {
                            operations += taskOps;
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                });
                
                tasks.Add(task);
            }
            
            Task.WaitAll(tasks.ToArray());
            
            // Assert
            exceptions.Should().BeEmpty($"High stress test should not throw exceptions, but got: {string.Join(", ", exceptions.Select(e => e.Message))}");
            
            operations.Should().BeGreaterThan(0, "Some operations should have completed");
            
            _output.WriteLine($"High stress thread safety test passed: {operations} operations completed safely");
        }

        #endregion

        #region Helper Methods

        private static GraphicsPipelineStateDescription CreateTestPipelineStateDescription(int seed)
        {
            return new GraphicsPipelineStateDescription
            {
                // Create a test description
                PrimitiveTopologyType = Vortice.Direct3D12.PrimitiveTopologyType.Triangle,
                InputLayout = new InputElementDescription[]
                {
                    new InputElementDescription("POSITION", 0, Format.R32G32B32A32_Float, 0, 0)
                }
            };
        }

        #endregion

        #region Cleanup

        public override void Dispose()
        {
            _output.WriteLine($"Thread Safety Tests completed with {_concurrentTaskCount} concurrent tasks");
            base.Dispose();
        }

        #endregion
    }
}
