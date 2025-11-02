using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TiXL.Core.ErrorHandling;
using TiXL.Core.Performance;

namespace TiXL.Tests.Production
{
    /// <summary>
    /// Comprehensive stress testing framework for production readiness validation
    /// Includes error injection, resource stress testing, and performance degradation simulation
    /// </summary>
    public class StressTestRunner
    {
        private readonly ILogger _logger;
        private readonly PerformanceMonitorEnhanced _performanceMonitor;
        private readonly Random _random = new Random();
        private readonly CancellationTokenSource _globalCts = new();
        
        public StressTestRunner(ILogger logger, PerformanceMonitorEnhanced? performanceMonitor = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _performanceMonitor = performanceMonitor;
        }

        /// <summary>
        /// Runs comprehensive stress tests with error injection
        /// </summary>
        public async Task<StressTestResults> RunComprehensiveStressTestAsync(
            int durationMinutes = 5,
            int concurrencyLevel = 10,
            double errorInjectionRate = 0.1,
            CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;
            var results = new StressTestResults
            {
                TestStartTime = startTime,
                TargetDuration = TimeSpan.FromMinutes(durationMinutes),
                ConcurrencyLevel = concurrencyLevel,
                ErrorInjectionRate = errorInjectionRate
            };

            _logger.LogInformation("Starting comprehensive stress test: {Duration} minutes, {Concurrency} concurrent operations, {ErrorRate:P2} error injection",
                durationMinutes, concurrencyLevel, errorInjectionRate);

            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(_globalCts.Token, cancellationToken);
            var testTasks = new List<Task>();

            // Start performance monitoring if available
            var monitoringTask = Task.Run(async () =>
            {
                while (!combinedCts.Token.IsCancellationRequested)
                {
                    _performanceMonitor?.BeginFrame();
                    await Task.Delay(16, combinedCts.Token); // ~60 FPS
                    _performanceMonitor?.EndFrame();
                }
            }, combinedCts.Token);

            // Start various stress test scenarios
            var scenarios = new[]
            {
                () => RunMemoryStressTestAsync(combinedCts.Token),
                () => RunConcurrencyStressTestAsync(concurrencyLevel, combinedCts.Token),
                () => RunErrorInjectionTestAsync(errorInjectionRate, combinedCts.Token),
                () => RunResourceContentionTestAsync(combinedCts.Token),
                () => RunIOWriteStressTestAsync(combinedCts.Token)
            };

            foreach (var scenario in scenarios)
            {
                testTasks.Add(scenario());
            }

            // Wait for completion or timeout
            try
            {
                await Task.WhenAll(testTasks);
                results.AllScenariosCompleted = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "One or more stress test scenarios failed");
                results.ExceptionCount++;
            }

            // Stop monitoring
            combinedCts.Cancel();
            await monitoringTask;

            var endTime = DateTime.UtcNow;
            results.TestEndTime = endTime;
            results.ActualDuration = endTime - startTime;

            // Collect final metrics
            results = await CollectFinalMetricsAsync(results);

            _logger.LogInformation("Comprehensive stress test completed in {Duration}. Operations: {Ops}, Errors: {Errors}, Avg Latency: {Latency}ms",
                results.ActualDuration, results.TotalOperations, results.ErrorCount, results.AverageLatencyMs);

            return results;
        }

        /// <summary>
        /// Memory stress test with controlled allocation and garbage collection
        /// </summary>
        private async Task RunMemoryStressTestAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting memory stress test");
            var stopwatch = Stopwatch.StartNew();
            var allocationCount = 0;
            var deallocationCount = 0;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Allocate memory in various patterns
                    var allocations = new List<object>();
                    
                    // Small allocations
                    for (int i = 0; i < 100; i++)
                    {
                        allocations.Add(new byte[1024]); // 1KB
                        allocationCount++;
                    }

                    // Medium allocations
                    for (int i = 0; i < 50; i++)
                    {
                        allocations.Add(new byte[10 * 1024]); // 10KB
                        allocationCount++;
                    }

                    // Large allocations (trigger LOH)
                    for (int i = 0; i < 10; i++)
                    {
                        allocations.Add(new byte[1024 * 1024]); // 1MB
                        allocationCount++;
                    }

                    // Simulate processing
                    await Task.Delay(10, cancellationToken);

                    // Clean up
                    allocations.Clear();
                    allocations = null;
                    deallocationCount++;

                    // Force GC periodically
                    if (deallocationCount % 100 == 0)
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during cancellation
            }
            finally
            {
                stopwatch.Stop();
                _logger.LogInformation("Memory stress test completed: {Allocations} allocations, {Deallocations} deallocations in {Duration}",
                    allocationCount, deallocationCount, stopwatch.Elapsed);
            }
        }

        /// <summary>
        /// Concurrency stress test with high thread contention
        /// </summary>
        private async Task RunConcurrencyStressTestAsync(int concurrencyLevel, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting concurrency stress test with {Level} concurrent operations", concurrencyLevel);
            
            var semaphore = new SemaphoreSlim(concurrencyLevel);
            var operationCount = 0;
            var errorCount = 0;
            var lockObject = new object();
            var sharedState = new ConcurrentDictionary<int, int>();

            var tasks = new List<Task>();

            for (int i = 0; i < concurrencyLevel; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    
                    try
                    {
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            // Simulate contention by incrementing shared counters
                            for (int j = 0; j < 1000; j++)
                            {
                                lock (lockObject)
                                {
                                    operationCount++;
                                }
                                
                                // Also use concurrent collections
                                sharedState.AddOrUpdate(j, 1, (key, value) => value + 1);
                                
                                // Introduce random delays
                                if (_random.Next(100) == 0)
                                {
                                    await Task.Delay(_random.Next(1, 10), cancellationToken);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref errorCount);
                        _logger.LogWarning(ex, "Concurrency stress test operation failed");
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken));
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(2), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during cancellation
            }
            finally
            {
                await Task.WhenAll(tasks);
                _logger.LogInformation("Concurrency stress test completed: {Operations} operations, {Errors} errors", operationCount, errorCount);
            }
        }

        /// <summary>
        /// Error injection test to validate error handling and recovery
        /// </summary>
        private async Task RunErrorInjectionTestAsync(double errorInjectionRate, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting error injection test with {Rate:P2} error rate", errorInjectionRate);
            
            var operationCount = 0;
            var successfulRecoveries = 0;
            var failedRecoveries = 0;

            try
            {
                using var context = new OperationContext(
                    "StressErrorInjection",
                    _logger,
                    new GracefulDegradationStrategy(),
                    new TimeoutPolicy { Timeout = TimeSpan.FromSeconds(5) },
                    new RetryPolicy { MaxRetries = 3 });

                while (!cancellationToken.IsCancellationRequested)
                {
                    operationCount++;
                    
                    try
                    {
                        await context.ExecuteWithFullProtectionAsync(async token =>
                        {
                            // Simulate work
                            await Task.Delay(_random.Next(1, 50), token);
                            
                            // Inject random errors
                            if (_random.NextDouble() < errorInjectionRate)
                            {
                                await InjectRandomErrorAsync(token);
                            }
                            
                            return "success";
                        }, cancellationToken);
                        
                        successfulRecoveries++;
                    }
                    catch (Exception ex)
                    {
                        failedRecoveries++;
                        _logger.LogDebug(ex, "Error injection test caught expected error");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during cancellation
            }
            
            _logger.LogInformation("Error injection test completed: {Operations} operations, {Successful} successful recoveries, {Failed} failed recoveries",
                operationCount, successfulRecoveries, failedRecoveries);
        }

        /// <summary>
        /// Resource contention test to validate resource management
        /// </summary>
        private async Task RunResourceContentionTestAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting resource contention test");
            
            var resourcePool = new List<IDisposable>();
            var contentionCount = 0;
            var timeoutCount = 0;

            try
            {
                // Create resource pool
                for (int i = 0; i < 10; i++)
                {
                    resourcePool.Add(new MockResource($"Resource_{i}"));
                }

                var tasks = new List<Task>();

                for (int i = 0; i < 20; i++)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        using var resourceWait = new SemaphoreSlim(resourcePool.Count, resourcePool.Count);
                        
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            try
                            {
                                await resourceWait.WaitAsync(cancellationToken);
                                
                                // Simulate resource usage
                                await Task.Delay(_random.Next(10, 100), cancellationToken);
                                
                                lock (resourcePool)
                                {
                                    contentionCount++;
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                timeoutCount++;
                                break;
                            }
                            finally
                            {
                                resourceWait.Release();
                            }
                        }
                    }, cancellationToken));
                }

                await Task.Delay(TimeSpan.FromMinutes(2), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during cancellation
            }
            finally
            {
                foreach (var resource in resourcePool)
                {
                    resource?.Dispose();
                }
                
                _logger.LogInformation("Resource contention test completed: {Contention} contention events, {Timeouts} timeouts",
                    contentionCount, timeoutCount);
            }
        }

        /// <summary>
        /// I/O stress test for file system operations
        /// </summary>
        private async Task RunIOWriteStressTestAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting I/O stress test");
            
            var tempDir = Path.Combine(Path.GetTempPath(), $"TiXL_StressTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var fileCount = 0;
                var totalBytes = 0L;

                while (!cancellationToken.IsCancellationRequested)
                {
                    var fileName = $"stress_test_{fileCount}.dat";
                    var filePath = Path.Combine(tempDir, fileName);
                    
                    // Write data in various sizes
                    var fileSize = _random.Next(1024, 1024 * 1024); // 1KB to 1MB
                    var buffer = new byte[fileSize];
                    _random.NextBytes(buffer);

                    try
                    {
                        await File.WriteAllBytesAsync(filePath, buffer, cancellationToken);
                        fileCount++;
                        totalBytes += fileSize;
                        
                        // Periodic cleanup
                        if (fileCount % 100 == 0)
                        {
                            var files = Directory.GetFiles(tempDir);
                            foreach (var file in files.Take(50)) // Delete half the files
                            {
                                try
                                {
                                    File.Delete(file);
                                }
                                catch
                                {
                                    // Ignore deletion errors
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "I/O stress test file write failed: {File}", fileName);
                    }

                    await Task.Delay(_random.Next(1, 10), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during cancellation
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
                
                _logger.LogInformation("I/O stress test completed");
            }
        }

        private async Task InjectRandomErrorAsync(CancellationToken cancellationToken)
        {
            var errorType = _random.Next(7);
            
            await Task.Delay(1, cancellationToken); // Small delay before error
            
            switch (errorType)
            {
                case 0:
                    throw new IOException("Simulated IO error");
                case 1:
                    throw new TimeoutException("Simulated timeout error");
                case 2:
                    throw new InvalidOperationException("Simulated invalid operation");
                case 3:
                    throw new ArgumentException("Simulated argument error");
                case 4:
                    throw new TaskCanceledException("Simulated task cancellation");
                case 5:
                    throw new OutOfMemoryException("Simulated out of memory");
                default:
                    throw new Exception("Simulated generic error");
            }
        }

        private async Task<StressTestResults> CollectFinalMetricsAsync(StressTestResults results)
        {
            if (_performanceMonitor != null)
            {
                var metrics = _performanceMonitor.GetRecentMetrics(100);
                if (metrics.Any())
                {
                    results.AverageFrameTimeMs = metrics.Average(m => m.FrameTimeMs);
                    results.MaxFrameTimeMs = metrics.Max(m => m.FrameTimeMs);
                    results.MinFrameTimeMs = metrics.Min(m => m.FrameTimeMs);
                    results.FrameTimeVariance = CalculateVariance(metrics.Select(m => m.FrameTimeMs));
                }
            }

            // Collect memory metrics
            results.FinalMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024);
            results.GcCollectionsGen0 = GC.CollectionCount(0);
            results.GcCollectionsGen1 = GC.CollectionCount(1);
            results.GcCollectionsGen2 = GC.CollectionCount(2);

            return results;
        }

        private static double CalculateVariance(IEnumerable<double> values)
        {
            var valueList = values.ToList();
            if (valueList.Count <= 1) return 0;

            var average = valueList.Average();
            var sumOfSquaresOfDifferences = valueList.Select(val => (val - average) * (val - average)).Sum();
            return Math.Sqrt(sumOfSquaresOfDifferences / valueList.Count);
        }

        /// <summary>
        /// Cancel all running stress tests
        /// </summary>
        public void CancelAllTests()
        {
            _globalCts.Cancel();
            _logger.LogInformation("All stress tests cancelled");
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            _globalCts?.Dispose();
        }
    }

    #region Supporting Classes

    public class StressTestResults
    {
        public DateTime TestStartTime { get; set; }
        public DateTime TestEndTime { get; set; }
        public TimeSpan TargetDuration { get; set; }
        public TimeSpan ActualDuration { get; set; }
        public int ConcurrencyLevel { get; set; }
        public double ErrorInjectionRate { get; set; }
        
        public int TotalOperations { get; set; }
        public int ErrorCount { get; set; }
        public int ExceptionCount { get; set; }
        public double AverageLatencyMs { get; set; }
        
        public double AverageFrameTimeMs { get; set; }
        public double MaxFrameTimeMs { get; set; }
        public double MinFrameTimeMs { get; set; }
        public double FrameTimeVariance { get; set; }
        
        public long FinalMemoryMB { get; set; }
        public int GcCollectionsGen0 { get; set; }
        public int GcCollectionsGen1 { get; set; }
        public int GcCollectionsGen2 { get; set; }
        
        public bool AllScenariosCompleted { get; set; }
        
        public bool IsSuccessful => ExceptionCount == 0 && ErrorCount < TotalOperations * 0.1;
        
        public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
    }

    public class MockResource : IDisposable
    {
        private readonly string _name;
        private bool _disposed;

        public MockResource(string name)
        {
            _name = name;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                // Simulate cleanup work
                Thread.Sleep(1);
            }
        }
    }

    #endregion
}