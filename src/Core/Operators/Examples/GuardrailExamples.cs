using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using T3.Core.Logging;
using T3.Core.Rendering;
using T3.Core.Resource;

namespace T3.Core.Operators.Examples
{
    #region Basic Usage Examples

    /// <summary>
    /// Example showing basic guardrail usage
    /// </summary>
    public class BasicGuardrailExample
    {
        private readonly ILogger _logger;

        public BasicGuardrailExample(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Demonstrates basic guardrail functionality
        /// </summary>
        public void RunBasicExample()
        {
            _logger.Information("Starting basic guardrail example");

            // Create a custom configuration for testing
            var config = GuardrailConfiguration.ForTesting();
            config.MaxEvaluationDuration = TimeSpan.FromSeconds(2);
            config.MaxMemoryBytes = 10 * 1024 * 1024; // 10MB
            config.StrictMode = true;

            // Create evaluation context with custom config
            var context = CreateTestContext(config);

            try
            {
                // Example 1: Simple guarded operation
                context.ExecuteWithGuardrails("SimpleOperation", () =>
                {
                    _logger.Information("Executing simple operation with guardrails");
                    Thread.Sleep(100); // Simulate work
                });

                // Example 2: Operation with resource tracking
                context.ExecuteWithGuardrails("ResourceOperation", () =>
                {
                    _logger.Information("Executing operation with resource tracking");
                    
                    // Track memory allocation
                    context.TrackResourceAllocation("Memory", 1024 * 1024); // 1MB
                    context.RecordMetric("CustomMetric", 42.5, "units");
                    
                    Thread.Sleep(50);
                });

                // Example 3: Operation with precondition validation
                var preconditions = new Dictionary<string, object>
                {
                    { "InputData", new byte[1024] },
                    { "Timeout", 1000 },
                    { "UserId", Guid.NewGuid() }
                };

                context.ExecuteWithGuardrails("ValidatedOperation", () =>
                {
                    var validation = context.ValidatePreconditions(preconditions);
                    if (!validation.IsValid)
                    {
                        _logger.Warning($"Precondition validation failed: {string.Join(", ", validation.Errors.Select(e => e.Message))}");
                    }

                    _logger.Information("Executing validated operation");
                    Thread.Sleep(25);
                });

                // Example 4: Error boundary usage
                if (context.TryExecuteWithErrorBoundary("ErrorProneOperation", () =>
                {
                    _logger.Information("Attempting error-prone operation");
                    throw new InvalidOperationException("This operation intentionally fails");
                }, out var exception))
                {
                    _logger.Information("Error-prone operation completed successfully");
                }
                else
                {
                    _logger.Warning($"Error-prone operation failed as expected: {exception?.Message}");
                }

                // Print final status
                PrintStatus(context);
            }
            finally
            {
                context.Dispose();
            }
        }

        /// <summary>
        /// Demonstrates async guardrail usage
        /// </summary>
        public async Task RunAsyncExample()
        {
            _logger.Information("Starting async guardrail example");

            var config = GuardrailConfiguration.Default;
            config.MaxEvaluationDuration = TimeSpan.FromSeconds(5);

            var context = CreateTestContext(config);

            try
            {
                await context.ExecuteWithGuardrailsAsync("AsyncOperation", async (ct) =>
                {
                    _logger.Information("Executing async operation with guardrails");
                    
                    // Simulate async work
                    await Task.Delay(100, ct);
                    
                    // Track some resources
                    context.TrackResourceAllocation("AsyncMemory", 512 * 1024); // 512KB
                    context.RecordMetric("AsyncMetric", 123.4, "async_units");
                    
                    return "Async result";
                });

                _logger.Information("Async operation completed");
                PrintStatus(context);
            }
            finally
            {
                context.Dispose();
            }
        }

        /// <summary>
        /// Demonstrates operation tracking
        /// </summary>
        public void RunTrackingExample()
        {
            _logger.Information("Starting operation tracking example");

            var context = CreateTestContext();

            try
            {
                using (var tracker = context.BeginOperation("TrackedOperation"))
                {
                    _logger.Information("Starting tracked operation");

                    // Record custom metrics
                    tracker.RecordMetric("ProcessingTime", 250.0, "ms");
                    tracker.RecordMetric("ItemsProcessed", 100, "count");

                    // Track resources
                    tracker.TrackResource("Texture", 2048 * 2048 * 4); // 16MB RGBA texture

                    // Check status mid-operation
                    var status = tracker.CheckStatus();
                    _logger.Information($"Mid-operation status: Health={status.IsHealthy}, Memory={status.MemoryUsage}");

                    // Simulate work
                    Thread.Sleep(100);

                    // Get final summary
                    var summary = tracker.GetSummary();
                    _logger.Information($"Operation completed: {summary.OperationName}, Duration={summary.Duration}, Success={summary.Completed}");
                }

                PrintStatus(context);
            }
            finally
            {
                context.Dispose();
            }
        }

        #endregion

        #region Complex Examples

        /// <summary>
        /// Example of a custom guarded operator
        /// </summary>
        public class CustomGuardedOperator : GuardrailedOperator
        {
            public CustomGuardedOperator(IRenderingEngine renderingEngine, IAudioEngine audioEngine, IResourceManager resourceManager, ILogger logger)
                : base("CustomGuardedOperator", renderingEngine, audioEngine, resourceManager, logger)
            {
            }

            public void ProcessData(byte[] data)
            {
                // Validate inputs first
                if (data == null)
                    throw new ArgumentNullException(nameof(data));

                ExecuteGuarded("ProcessData", () =>
                {
                    Logger.Information($"Processing {data.Length} bytes of data");

                    // Track memory usage
                    TrackResource("DataBuffer", data.Length);

                    // Validate preconditions for this specific operation
                    var preconditions = new Dictionary<string, object>
                    {
                        { "DataSize", data.Length },
                        { "ProcessingTime", DateTime.UtcNow },
                        { "BufferAllocated", true }
                    };

                    var validation = Context.ValidatePreconditions(preconditions);
                    if (!validation.IsValid)
                    {
                        throw new InvalidOperationException($"Data processing preconditions failed: {string.Join(", ", validation.Errors.Select(e => e.Message))}");
                    }

                    // Simulate data processing
                    Thread.Sleep(50);

                    // Record processing metrics
                    RecordMetric("DataProcessed", data.Length, "bytes");
                    RecordMetric("ProcessingDuration", 50.0, "ms");

                    Logger.Information("Data processing completed");
                });
            }

            public async Task ProcessDataAsync(byte[] data, CancellationToken cancellationToken = default)
            {
                if (data == null)
                    throw new ArgumentNullException(nameof(data));

                await ExecuteGuardedAsync("ProcessDataAsync", async (ct) =>
                {
                    Logger.Information($"Processing {data.Length} bytes of data asynchronously");

                    // Track memory usage
                    TrackResource("AsyncDataBuffer", data.Length);

                    // Simulate async data processing
                    await Task.Delay(100, ct);

                    // Record async processing metrics
                    RecordMetric("AsyncDataProcessed", data.Length, "bytes");
                    RecordMetric("AsyncProcessingDuration", 100.0, "ms");

                    Logger.Information("Async data processing completed");
                    return data.Length;
                });
            }

            protected override IDictionary<string, object> GetPreconditions()
            {
                var basePreconditions = base.GetPreconditions();
                basePreconditions["OperatorType"] = "CustomGuardedOperator";
                basePreconditions["SupportsAsync"] = true;
                basePreconditions["ResourceTracking"] = true;
                return basePreconditions;
            }

            protected override void HandleOperationException(string operationName, Exception exception)
            {
                base.HandleOperationException(operationName, exception);
                
                // Custom exception handling
                var status = GetOperatorStatus();
                if (!status.IsHealthy)
                {
                    Logger.Error($"Operator health degraded: {string.Join(", ", status.Warnings)}");
                }
            }
        }

        /// <summary>
        /// Demonstrates custom operator usage
        /// </summary>
        public void RunCustomOperatorExample()
        {
            _logger.Information("Starting custom operator example");

            var context = CreateTestContext();
            var operatorInstance = new CustomGuardedOperator(
                context.RenderingEngine,
                context.AudioEngine,
                context.ResourceManager,
                context.Logger);

            try
            {
                // Process some data
                var testData = new byte[1024 * 1024]; // 1MB
                operatorInstance.ProcessData(testData);

                // Process data asynchronously
                operatorInstance.ProcessDataAsync(testData).Wait();

                // Get operator status
                var status = operatorInstance.GetOperatorStatus();
                _logger.Information($"Operator status: Healthy={status.IsHealthy}, Warnings={status.Warnings.Length}");

                PrintStatus(context);
            }
            finally
            {
                operatorInstance.Dispose();
                context.Dispose();
            }
        }

        #endregion

        #region Helper Methods

        private EvaluationContext CreateTestContext(GuardrailConfiguration? config = null)
        {
            var renderingEngine = new MockRenderingEngine();
            var audioEngine = new MockAudioEngine();
            var resourceManager = new MockResourceManager();
            var logger = _logger;

            return new EvaluationContext(
                renderingEngine,
                audioEngine,
                resourceManager,
                logger,
                CancellationToken.None,
                config ?? GuardrailConfiguration.ForTesting()
            );
        }

        private void PrintStatus(EvaluationContext context)
        {
            var metrics = context.Metrics;
            var status = context.CheckResourceLimits();
            var report = context.GetPerformanceReport();

            _logger.Information("=== Guardrail Status Report ===");
            _logger.Information($"Healthy: {status.IsHealthy}");
            _logger.Information($"Elapsed Time: {metrics.ElapsedTime}");
            _logger.Information($"Memory Usage: {metrics.MemoryUsageBytes:N0} bytes ({metrics.MemoryUsageBytes / (1024.0 * 1024.0):F2} MB)");
            _logger.Information($"CPU Usage: {metrics.CpuUsagePercent:F1}%");
            _logger.Information($"GC Pressure: {metrics.GcPressureBytes:N0} bytes");
            _logger.Information($"Active Warnings: {metrics.WarningCount}");
            _logger.Information($"Metrics Tracked: {metrics.MetricsCount}");
            _logger.Information($"Total Resources: {metrics.ResourceCount}");
            _logger.Information("=================================");
        }

        #endregion

        #region Mock Implementations

        private class MockRenderingEngine : IRenderingEngine { }
        private class MockAudioEngine : IAudioEngine { }
        private class MockResourceManager : IResourceManager { }

        #endregion
    }

    #region Performance Testing Examples

    /// <summary>
    /// Example showing performance testing with guardrails
    /// </summary>
    public class PerformanceTestExample
    {
        private readonly ILogger _logger;

        public PerformanceTestExample(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Runs performance stress tests with guardrails
        /// </summary>
        public void RunPerformanceTests()
        {
            _logger.Information("Starting performance tests");

            var performanceConfig = GuardrailConfiguration.ForPerformance();
            performanceConfig.MaxEvaluationDuration = TimeSpan.FromSeconds(30);
            performanceConfig.MaxMemoryBytes = 512 * 1024 * 1024; // 512MB

            var context = CreateTestContext(performanceConfig);

            try
            {
                // Test 1: Memory allocation stress test
                StressTestMemoryAllocation(context);

                // Test 2: Operation count stress test
                StressTestOperationCount(context);

                // Test 3: Concurrent operations test
                StressTestConcurrentOperations(context);

                // Test 4: Timeout handling test
                TestTimeoutHandling(context);

                PrintPerformanceReport(context);
            }
            finally
            {
                context.Dispose();
            }
        }

        private void StressTestMemoryAllocation(EvaluationContext context)
        {
            _logger.Information("Running memory allocation stress test");

            context.ExecuteWithGuardrails("MemoryStressTest", () =>
            {
                var allocationSize = 1024 * 1024; // 1MB
                var allocations = 0;

                try
                {
                    while (allocations < 50 && context.CheckResourceLimits().IsHealthy)
                    {
                        context.TrackResourceAllocation("StressTestMemory", allocationSize);
                        allocations++;
                        
                        if (allocations % 10 == 0)
                        {
                            _logger.Information($"Completed {allocations} memory allocations");
                            Thread.Sleep(10); // Small delay to prevent overwhelming the system
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.Information($"Memory allocation test stopped at {allocations} allocations due to guardrails");
                }

                RecordMetric("MemoryAllocationsCompleted", allocations, "count");
            });
        }

        private void StressTestOperationCount(EvaluationContext context)
        {
            _logger.Information("Running operation count stress test");

            var operations = 0;
            var startTime = DateTime.UtcNow;

            try
            {
                context.ExecuteWithGuardrails("OperationStressTest", () =>
                {
                    while (context.CheckResourceLimits().IsHealthy && operations < 10000)
                    {
                        // Simulate a small operation
                        var dummy = new byte[1024];
                        operations++;

                        if (operations % 1000 == 0)
                        {
                            _logger.Information($"Completed {operations} operations");
                        }
                    }
                });
            }
            catch (OperationCanceledException)
            {
                _logger.Information($"Operation count test stopped at {operations} operations due to guardrails");
            }

            var duration = DateTime.UtcNow - startTime;
            RecordMetric("OperationsCompleted", operations, "count");
            RecordMetric("OperationsPerSecond", operations / duration.TotalSeconds, "ops/sec");
        }

        private void StressTestConcurrentOperations(EvaluationContext context)
        {
            _logger.Information("Running concurrent operations test");

            var tasks = new List<Task>();

            for (int i = 0; i < 20; i++)
            {
                var taskId = i;
                var task = Task.Run(() =>
                {
                    try
                    {
                        context.ExecuteWithGuardrails($"ConcurrentTask{taskId}", () =>
                        {
                            Thread.Sleep(50); // Simulate work
                            context.TrackResourceAllocation($"ConcurrentTask{taskId}", 1024);
                        });
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.Information($"Concurrent task {taskId} was cancelled");
                    }
                });

                tasks.Add(task);
            }

            Task.WaitAll(tasks.ToArray());
            _logger.Information("Concurrent operations test completed");
        }

        private void TestTimeoutHandling(EvaluationContext context)
        {
            _logger.Information("Running timeout handling test");

            // Create a very short timeout configuration
            var shortTimeoutConfig = GuardrailConfiguration.ForTesting();
            shortTimeoutConfig.MaxOperationDuration = TimeSpan.FromMilliseconds(50);

            var shortTimeoutContext = CreateTestContext(shortTimeoutConfig);

            try
            {
                var succeeded = shortTimeoutContext.TryExecuteWithErrorBoundary("LongOperation", () =>
                {
                    Thread.Sleep(100); // This should timeout
                }, out var exception);

                if (!succeeded && exception is OperationCanceledException)
                {
                    _logger.Information("Timeout handling test passed - operation was correctly cancelled");
                }
                else
                {
                    _logger.Warning("Timeout handling test failed - operation should have been cancelled");
                }
            }
            finally
            {
                shortTimeoutContext.Dispose();
            }
        }

        private void PrintPerformanceReport(EvaluationContext context)
        {
            var report = context.GetPerformanceReport();

            _logger.Information("=== Performance Test Report ===");
            _logger.Information($"Test Duration: {report.ElapsedTime}");
            _logger.Information($"Final Status: {(report.CurrentStatus.IsHealthy ? "Healthy" : "Unhealthy")}");
            _logger.Information($"Memory Usage: {report.ResourceStatistics.MemoryAllocated:N0} bytes");
            _logger.Information($"CPU Usage: {report.ResourceStatistics.CpuUsage:F1}%");
            _logger.Information($"Warnings Generated: {report.Warnings.Count}");
            _logger.Information($"Recommendations: {report.Recommendations.Count}");

            if (report.Recommendations.Any())
            {
                _logger.Information("Recommendations:");
                foreach (var recommendation in report.Recommendations)
                {
                    _logger.Information($"  - {recommendation}");
                }
            }
        }

        private void RecordMetric(string metricName, double value, string unit)
        {
            _logger.Information($"Metric: {metricName} = {value} {unit}");
        }

        private EvaluationContext CreateTestContext(GuardrailConfiguration config)
        {
            return EvaluationContext.CreateForTest(
                guardrails: config
            );
        }
    }

    #endregion
}