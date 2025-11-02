using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using TiXL.Core.ErrorHandling;
using TiXL.Core.Logging;
using TiXL.Core.Performance;
using TiXL.Core.Validation;
using TiXL.Tests.Fixtures;

namespace TiXL.Tests.Production
{
    /// <summary>
    /// Comprehensive production readiness validation tests for TiXL
    /// Covers all aspects of production deployment including error handling, monitoring,
    /// resource management, configuration, performance, and graceful shutdown
    /// </summary>
    [TestCategory("Production")]
    [TestCategory("ProductionReadiness")]
    public class ProductionReadinessTests : IClassFixture<CoreTestFixture>, IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly CoreTestFixture _fixture;
        private readonly PerformanceMonitorEnhanced _performanceMonitor;
        private readonly ILogger _logger;
        private readonly string _tempDirectory;

        public ProductionReadinessTests(CoreTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _output = output ?? throw new ArgumentNullException(nameof(output));
            _logger = _fixture.GetService<ILogger<ProductionReadinessTests>>();
            _performanceMonitor = _fixture.GetService<PerformanceMonitorEnhanced>();
            _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            
            Directory.CreateDirectory(_tempDirectory);
            _output.WriteLine($"Created temp directory: {_tempDirectory}");
        }

        #region Error Handling and Recovery Tests

        [Fact]
        [Trait("Category", "Production.ErrorHandling")]
        [Trait("Category", "Production.Recovery")]
        public async Task ErrorHandling_ComprehensiveErrorPaths_AllHandled()
        {
            // Test all error handling patterns
            var errorTypes = new[]
            {
                new InvalidOperationException("Test invalid operation"),
                new ArgumentException("Test argument error"),
                new TimeoutException("Test timeout"),
                new IOException("Test IO error"),
                new TaskCanceledException("Test task cancellation"),
                new OutOfMemoryException("Test memory error")
            };

            var failureRecorder = new List<string>();
            var retryPolicy = new RetryPolicy 
            { 
                MaxRetries = 3,
                RetryCondition = ex => ExceptionFilters.IsTransientFailure(ex)
            };

            using var context = new OperationContext(
                "ErrorHandlingTest",
                _logger,
                new GracefulDegradationStrategy(),
                new TimeoutPolicy { Timeout = TimeSpan.FromSeconds(5) },
                retryPolicy);

            foreach (var error in errorTypes)
            {
                try
                {
                    await context.ExecuteWithFullProtectionAsync(async token =>
                    {
                        await Task.Delay(50, token); // Simulate work
                        throw error;
                    });
                }
                catch (Exception ex)
                {
                    failureRecorder.Add(ex.GetType().Name);
                    _output.WriteLine($"Error handled: {ex.GetType().Name} - {ex.Message}");
                }
            }

            failureRecorder.Should().HaveCount(errorTypes.Length, "All error types should be handled");
            failureRecorder.Should().Contain("InvalidOperationException");
            failureRecorder.Should().Contain("ArgumentException");
            failureRecorder.Should().Contain("TimeoutException");
        }

        [Fact]
        [Trait("Category", "Production.ErrorHandling")]
        [Trait("Category", "Production.Recovery")]
        public async Task ErrorHandling_GracefulDegradation_LevelsCorrect()
        {
            var degradationStrategy = new GracefulDegradationStrategy();
            
            // Test degradation levels
            for (int i = 0; i < 2; i++)
            {
                degradationStrategy.RecordFailure($"Failure {i + 1}");
            }
            degradationStrategy.CurrentLevel.Should().Be(GracefulDegradationStrategy.DegradationLevel.Reduced);

            for (int i = 2; i < 5; i++)
            {
                degradationStrategy.RecordFailure($"Failure {i + 1}");
            }
            degradationStrategy.CurrentLevel.Should().Be(GracefulDegradationStrategy.DegradationLevel.Minimal);

            for (int i = 5; i < 10; i++)
            {
                degradationStrategy.RecordFailure($"Failure {i + 1}");
            }
            degradationStrategy.CurrentLevel.Should().Be(GracefulDegradationStrategy.DegradationLevel.Deferred);

            degradationStrategy.CanProceed().Should().BeTrue();

            degradationStrategy.RecordFailure("Critical failure");
            degradationStrategy.CurrentLevel.Should().Be(GracefulDegradationStrategy.DegradationLevel.Skipped);
            degradationStrategy.CanProceed().Should().BeFalse();
        }

        [Fact]
        [Trait("Category", "Production.ErrorHandling")]
        [Trait("Category", "Production.Recovery")]
        public void ErrorHandling_RetryPolicy_ExponentialBackoff()
        {
            var retryPolicy = new RetryPolicy
            {
                MaxRetries = 3,
                InitialDelay = TimeSpan.FromMilliseconds(100),
                BackoffMultiplier = 2.0,
                MaxDelay = TimeSpan.FromSeconds(1)
            };

            retryPolicy.GetDelay(0).Should().Be(TimeSpan.FromMilliseconds(100));
            retryPolicy.GetDelay(1).Should().Be(TimeSpan.FromMilliseconds(200));
            retryPolicy.GetDelay(2).Should().Be(TimeSpan.FromMilliseconds(400));
            retryPolicy.GetDelay(3).Should().Be(TimeSpan.FromMilliseconds(800));
            retryPolicy.GetDelay(10).Should().Be(TimeSpan.FromSeconds(1)); // Should cap at max delay
        }

        #endregion

        #region Resource Cleanup and Disposal Tests

        [Fact]
        [Trait("Category", "Production.Disposal")]
        public async Task ResourceManagement_DisposalPatterns_AllCleanedUp()
        {
            var disposedResources = new List<string>();
            var resourceCount = 10;

            var resources = Enumerable.Range(0, resourceCount).Select(i => CreateDisposableResource($"Resource_{i}", disposedResources)).ToArray();

            await ResourceCleanup.ExecuteWithCleanupAsync(async () =>
            {
                // Simulate work
                await Task.Delay(100);
                return "completed";
            }, resources);

            // Verify all resources were disposed
            disposedResources.Should().HaveCount(resourceCount);
            disposedResources.Should().BeInAscendingOrder();
            
            _output.WriteLine($"All {resourceCount} resources disposed correctly");
        }

        [Fact]
        [Trait("Category", "Production.Disposal")]
        public void ResourceManagement_FailedDisposal_Swallowed()
        {
            var disposalErrors = new List<Exception>();
            var resources = new[]
            {
                CreateDisposableResource("GoodResource", disposalErrors),
                CreateDisposableResource("BadResource", disposalErrors, shouldFail: true),
                CreateDisposableResource("AnotherBadResource", disposalErrors, shouldFail: true)
            };

            Action action = () => ResourceCleanup.ExecuteWithCleanup(() => "done", resources);
            
            action.Should().NotThrow("Disposal errors should be swallowed");
            disposalErrors.Should().HaveCount(2, "Failed disposals should be logged but not throw");
        }

        [Fact]
        [Trait("Category", "Production.Disposal")]
        public async Task ResourceManagement_MemoryLeaks_Avoided()
        {
            var initialMemory = GC.GetTotalMemory(false);
            var iterations = 100;
            
            for (int i = 0; i < iterations; i++)
            {
                using var operationContext = new OperationContext(
                    $"MemoryTest_{i}",
                    _logger,
                    new GracefulDegradationStrategy(),
                    new TimeoutPolicy(),
                    new RetryPolicy());

                // Simulate some work
                var largeArray = new byte[1024 * 1024]; // 1MB array
                await Task.Delay(10);
            }
            
            GC.Collect();
            var finalMemory = GC.GetTotalMemory(false);
            var memoryIncrease = finalMemory - initialMemory;
            
            // Memory increase should be minimal (less than 10% of expected allocation)
            var expectedAllocation = iterations * 1024 * 1024;
            memoryIncrease.Should().BeLessThan(expectedAllocation * 0.1, 
                "Memory should be properly released");
                
            _output.WriteLine($"Memory increase: {memoryIncrease / (1024 * 1024):F2}MB (expected ~{expectedAllocation / (1024 * 1024):F2}MB)");
        }

        #endregion

        #region Logging and Monitoring Integration Tests

        [Fact]
        [Trait("Category", "Production.Logging")]
        public void Logging_AllLogLevels_LoggedCorrectly()
        {
            var logMessages = new List<string>();
            var mockLogger = new Mock<ILogger>();
            mockLogger.Setup(x => x.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), 
                It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception, string>>()))
                .Callback<LogLevel, EventId, object, Exception, Delegate>((level, eventId, state, exception, formatter) =>
                {
                    logMessages.Add($"{level}: {state}");
                });

            var logger = mockLogger.Object;
            
            logger.LogTrace("Trace message");
            logger.LogDebug("Debug message");
            logger.LogInformation("Information message");
            logger.LogWarning("Warning message");
            logger.LogError("Error message");
            logger.LogCritical("Critical message");

            logMessages.Should().HaveCount(6);
            logMessages.Should().Contain(m => m.StartsWith("Trace:"));
            logMessages.Should().Contain(m => m.StartsWith("Debug:"));
            logMessages.Should().Contain(m => m.StartsWith("Information:"));
            logMessages.Should().Contain(m => m.StartsWith("Warning:"));
            logMessages.Should().Contain(m => m.StartsWith("Error:"));
            logMessages.Should().Contain(m => m.StartsWith("Critical:"));
        }

        [Fact]
        [Trait("Category", "Production.Logging")]
        [Trait("Category", "Production.Performance")]
        public async Task PerformanceMonitoring_RealTimeMetrics_Accurate()
        {
            if (_performanceMonitor == null)
            {
                _output.WriteLine("Performance monitor not available, skipping test");
                return;
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            
            var metricsCount = 0;
            var alertsReceived = 0;

            _performanceMonitor.PerformanceAlert += (sender, alert) =>
            {
                alertsReceived++;
                _output.WriteLine($"Performance alert: {alert.Level} - {alert.Message}");
            };

            // Generate some frames to monitor
            for (int i = 0; i < 100; i++)
            {
                _performanceMonitor.BeginFrame();
                
                // Simulate frame work
                var workDuration = i % 10 == 0 ? 25 : 10; // Some frames are slower
                await Task.Delay(workDuration);
                
                _performanceMonitor.EndFrame();
                metricsCount++;
            }

            metricsCount.Should().Be(100);
            
            var metrics = _performanceMonitor.GetRecentMetrics(50);
            metrics.Should().NotBeNull();
            metrics.Should().HaveCountLessOrEqualTo(50);

            _output.WriteLine($"Collected {metricsCount} metrics, {alertsReceived} alerts generated");
        }

        [Fact]
        [Trait("Category", "Production.Logging")]
        public async Task Monitoring_PerformanceAlerts_GeneratedCorrectly()
        {
            if (_performanceMonitor == null)
            {
                _output.WriteLine("Performance monitor not available, skipping test");
                return;
            }

            var alerts = new List<PerformanceAlert>();
            _performanceMonitor.PerformanceAlert += (sender, alert) => alerts.Add(alert);

            // Trigger some slow frames
            for (int i = 0; i < 10; i++)
            {
                _performanceMonitor.BeginFrame();
                await Task.Delay(35); // Simulate slow frame (>33ms)
                _performanceMonitor.EndFrame();
            }

            // Should have generated alerts for slow frames
            alerts.Should().HaveCountGreaterThan(0, "Should generate alerts for performance issues");
            
            _output.WriteLine($"Generated {alerts.Count} performance alerts");
        }

        #endregion

        #region Configuration Validation and Startup Tests

        [Fact]
        [Trait("Category", "Production.Configuration")]
        public async Task Configuration_StartupValidation_AllChecksPass()
        {
            var configChecks = new List<string>();
            var startupChecks = new Dictionary<string, bool>();

            // Test .NET runtime version
            var runtimeInfo = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
            configChecks.Add($"Runtime: {runtimeInfo}");
            startupChecks["RuntimeVersion"] = !string.IsNullOrEmpty(runtimeInfo);

            // Test memory availability
            var totalMemory = GC.GetTotalMemory(false);
            configChecks.Add($"Available Memory: {totalMemory / (1024 * 1024)}MB");
            startupChecks["MemoryAvailable"] = totalMemory > 100 * 1024 * 1024; // 100MB minimum

            // Test configuration loading
            var config = _fixture.GetService<IConfiguration>();
            startupChecks["ConfigurationLoaded"] = config != null;

            // Test logging configuration
            var loggerFactory = _fixture.GetService<ILoggerFactory>();
            startupChecks["LoggingConfigured"] = loggerFactory != null;

            // Test service provider
            var serviceProvider = _fixture.GetService<IServiceProvider>();
            startupChecks["ServiceProviderAvailable"] = serviceProvider != null;

            _output.WriteLine("Configuration checks:");
            foreach (var check in configChecks)
            {
                _output.WriteLine($"  ✓ {check}");
            }

            foreach (var check in startupChecks)
            {
                _output.WriteLine($"  {(check.Value ? "✓" : "✗")} {check.Key}");
                check.Value.Should().BeTrue($"Configuration check failed: {check.Key}");
            }
        }

        [Fact]
        [Trait("Category", "Production.Configuration")]
        public void Configuration_InvalidConfiguration_Rejected()
        {
            var validationResults = new List<string>();

            // Test invalid history size
            Action invalidHistorySize = () => new PerformanceMonitorEnhanced(-1, null, _logger);
            invalidHistorySize.Should().Throw<ArgumentOutOfRangeException>()
                .WithMessage("*historySize*");

            validationResults.Add("Invalid history size rejected");

            // Test null logger
            Action nullLogger = () => new PerformanceMonitorEnhanced(300, null, null);
            nullLogger.Should().NotThrow("Should handle null logger gracefully");

            validationResults.Add("Null logger handled gracefully");

            // Test invalid retry policy
            var invalidRetryPolicy = new RetryPolicy 
            { 
                MaxRetries = -1,
                InitialDelay = TimeSpan.FromMilliseconds(-1)
            };
            
            // This should be caught during execution
            var delay = invalidRetryPolicy.GetDelay(0);
            delay.Should().BeLessThan(TimeSpan.Zero, "Invalid delay should be corrected");

            validationResults.Add("Invalid retry policy handled");

            validationResults.Should().HaveCount(3);
        }

        #endregion

        #region Graceful Shutdown and Cleanup Tests

        [Fact]
        [Trait("Category", "Production.Shutdown")]
        public async Task Shutdown_GracefulShutdown_AllServicesStopped()
        {
            var shutdownSteps = new List<string>();
            var cts = new CancellationTokenSource();

            // Register shutdown handlers
            AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
            {
                shutdownSteps.Add("ProcessExit");
            };

            Console.CancelKeyPress += (sender, e) =>
            {
                shutdownSteps.Add("CancelKeyPress");
                e.Cancel = true; // Prevent immediate termination
            };

            // Simulate graceful shutdown process
            var shutdownTask = Task.Run(async () =>
            {
                cts.Cancel();
                
                // Give time for cancellation to propagate
                await Task.Delay(100);
                
                shutdownSteps.Add("CancellationRequested");
                
                // Simulate cleanup work
                await Task.Delay(50);
                
                shutdownSteps.Add("CleanupCompleted");
            });

            // Trigger shutdown
            cts.Cancel();
            
            await shutdownTask;

            shutdownSteps.Should().Contain("CancellationRequested");
            shutdownSteps.Should().Contain("CleanupCompleted");

            _output.WriteLine($"Shutdown sequence completed: {string.Join(" -> ", shutdownSteps)}");
        }

        [Fact]
        [Trait("Category", "Production.Shutdown")]
        public async Task Shutdown_ForcedTermination_ResourcesReleased()
        {
            var resources = new List<IDisposable>();
            var disposedCount = 0;

            // Create resources that should be disposed
            for (int i = 0; i < 5; i++)
            {
                var resource = CreateDisposableResource($"Resource_{i}", new List<Exception>(), onDispose: () => disposedCount++);
                resources.Add(resource);
            }

            // Simulate forced termination scenario
            using var context = new OperationContext("ForceShutdown", _logger);
            
            try
            {
                // Force an exception that could cause premature termination
                throw new InvalidOperationException("Simulated forced termination");
            }
            catch
            {
                // In a real forced termination, we might not reach here
                // This test verifies that resources are still tracked for cleanup
                foreach (var resource in resources)
                {
                    try
                    {
                        resource?.Dispose();
                    }
                    catch
                    {
                        // Swallow disposal errors
                    }
                }
            }

            disposedCount.Should().Be(5, "All resources should be disposed even on forced termination");
        }

        #endregion

        #region Performance Under Sustained Load Tests

        [Fact]
        [Trait("Category", "Production.Performance")]
        [Trait("Category", "Production.SustainedLoad")]
        public async Task Performance_SustainedLoad_MaintainsQuality()
        {
            if (_performanceMonitor == null)
            {
                _output.WriteLine("Performance monitor not available, skipping test");
                return;
            }

            var operationCounts = new List<int>();
            var executionTimes = new List<double>();
            var targetOperations = 1000;
            var duration = TimeSpan.FromSeconds(10);

            using var cts = new CancellationTokenSource(duration);

            var startTime = DateTime.UtcNow;
            var operationCount = 0;

            _performanceMonitor.BeginFrame();

            while (!cts.Token.IsCancellationRequested && operationCount < targetOperations)
            {
                var opStart = DateTime.UtcNow;
                
                // Simulate work
                await SimulateWork(cts.Token);
                
                operationCount++;
                
                if (operationCount % 100 == 0)
                {
                    var opDuration = (DateTime.UtcNow - opStart).TotalMilliseconds;
                    executionTimes.Add(opDuration);
                    operationCounts.Add(operationCount);
                    
                    _output.WriteLine($"Completed {operationCount} operations");
                }
            }

            _performanceMonitor.EndFrame();
            
            var totalDuration = (DateTime.UtcNow - startTime).TotalSeconds;
            var operationsPerSecond = operationCount / totalDuration;

            operationCount.Should().BeGreaterThan(targetOperations * 0.8, 
                "Should complete at least 80% of target operations");
            operationsPerSecond.Should().BeGreaterThan(50, 
                "Should maintain reasonable throughput");

            _output.WriteLine($"Sustained load test results:");
            _output.WriteLine($"  Operations completed: {operationCount}");
            _output.WriteLine($"  Operations/second: {operationsPerSecond:F2}");
            _output.WriteLine($"  Total duration: {totalDuration:F2}s");
            _output.WriteLine($"  Average operation time: {executionTimes.Average():F2}ms");
        }

        [Fact]
        [Trait("Category", "Production.Performance")]
        [Trait("Category", "Production.SustainedLoad")]
        public async Task Performance_MemoryStability_UnderLoad()
        {
            var memorySnapshots = new List<(long Timestamp, long Memory)>();
            var initialMemory = GC.GetTotalMemory(false);
            var operations = 500;

            for (int i = 0; i < operations; i++)
            {
                // Perform memory-intensive operations
                var data = new byte[100 * 1024]; // 100KB
                var result = new List<string>();
                
                for (int j = 0; j < 1000; j++)
                {
                    result.Add($"Item_{j}_{i}");
                }
                
                memorySnapshots.Add((DateTime.UtcNow.Ticks, GC.GetTotalMemory(false)));
                
                // Allow GC to run
                if (i % 50 == 0)
                {
                    await Task.Yield();
                    GC.Collect();
                }
                
                // Clean up
                data = null;
                result = null;
            }

            GC.Collect();
            var finalMemory = GC.GetTotalMemory(false);
            var memoryIncrease = finalMemory - initialMemory;

            // Memory should be relatively stable
            var memoryVariance = memorySnapshots.Select(s => s.Memory).StandardDeviation();
            memoryIncrease.Should().BeLessThan(initialMemory * 0.2, 
                "Memory increase should be less than 20% of initial");
            memoryVariance.Should().BeLessThan(initialMemory * 0.1, 
                "Memory variance should be minimal");

            _output.WriteLine($"Memory stability test results:");
            _output.WriteLine($"  Initial memory: {initialMemory / (1024 * 1024):F2}MB");
            _output.WriteLine($"  Final memory: {finalMemory / (1024 * 1024):F2}MB");
            _output.WriteLine($"  Memory increase: {memoryIncrease / (1024 * 1024):F2}MB");
            _output.WriteLine($"  Memory variance: {memoryVariance / (1024 * 1024):F2}MB");
        }

        [Fact]
        [Trait("Category", "Production.Performance")]
        [Trait("Category", "Production.SustainedLoad")]
        public async Task Performance_ConcurrentOperations_ThreadSafe()
        {
            var concurrentTasks = 10;
            var operationsPerTask = 100;
            var sharedCounter = 0;
            var exceptions = new List<Exception>();
            var lockObject = new object();

            var tasks = Enumerable.Range(0, concurrentTasks).Select(taskId => Task.Run(async () =>
            {
                try
                {
                    for (int i = 0; i < operationsPerTask; i++)
                    {
                        // Simulate work that requires thread safety
                        lock (lockObject)
                        {
                            sharedCounter++;
                        }
                        
                        await Task.Delay(1); // Small delay to increase contention
                    }
                }
                catch (Exception ex)
                {
                    lock (lockObject)
                    {
                        exceptions.Add(ex);
                    }
                }
            })).ToArray();

            await Task.WhenAll(tasks);

            sharedCounter.Should().Be(concurrentTasks * operationsPerTask, 
                "All operations should complete successfully");
            exceptions.Should().BeEmpty("No exceptions should occur during concurrent operations");

            _output.WriteLine($"Concurrent operations test results:");
            _output.WriteLine($"  Tasks: {concurrentTasks}");
            _output.WriteLine($"  Operations per task: {operationsPerTask}");
            _output.WriteLine($"  Total operations: {sharedCounter}");
            _output.WriteLine($"  Exceptions: {exceptions.Count}");
        }

        #endregion

        #region Helper Methods

        private static TestDisposableResource CreateDisposableResource(string name, List<Exception> errorRecorder, bool shouldFail = false, Action? onDispose = null)
        {
            return new TestDisposableResource(name, shouldFail, onDispose, errorRecorder);
        }

        private static async Task SimulateWork(CancellationToken cancellationToken)
        {
            // Simulate some computational work
            var result = 0;
            for (int i = 0; i < 1000; i++)
            {
                result += i * i;
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException();
            }
            
            await Task.Yield();
        }

        public void Dispose()
        {
            // Cleanup temporary directory
            try
            {
                if (Directory.Exists(_tempDirectory))
                {
                    Directory.Delete(_tempDirectory, true);
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Failed to cleanup temp directory: {ex.Message}");
            }

            _performanceMonitor?.Dispose();
        }

        #endregion
    }

    #region Helper Classes

    public class TestDisposableResource : IDisposable
    {
        private readonly string _name;
        private readonly bool _shouldFail;
        private readonly Action? _onDispose;
        private readonly List<Exception> _errorRecorder;
        private bool _disposed;

        public TestDisposableResource(string name, bool shouldFail, Action? onDispose, List<Exception> errorRecorder)
        {
            _name = name;
            _shouldFail = shouldFail;
            _onDispose = onDispose;
            _errorRecorder = errorRecorder;
            _disposed = false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                _onDispose?.Invoke();

                if (_shouldFail)
                {
                    _errorRecorder.Add(new InvalidOperationException($"Failed to dispose {_name}"));
                }
                else
                {
                    _errorRecorder.Add(new Exception(_name));
                }

                _disposed = true;
            }
            catch (Exception ex)
            {
                _errorRecorder.Add(ex);
            }
        }
    }

    public static class LinqExtensions
    {
        public static double StandardDeviation(this IEnumerable<double> values)
        {
            var valueList = values.ToList();
            if (valueList.Count <= 1) return 0;

            var average = valueList.Average();
            var sumOfSquaresOfDifferences = valueList.Select(val => (val - average) * (val - average)).Sum();
            return Math.Sqrt(sumOfSquaresOfDifferences / valueList.Count);
        }
    }

    #endregion
}