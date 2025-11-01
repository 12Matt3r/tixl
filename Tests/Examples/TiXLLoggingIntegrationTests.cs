// TiXL Structured Logging Framework Integration Test
// Demonstrates the complete logging framework working together

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using System.Diagnostics;
using TiXL.Logging;
using TiXL.Logging.Modules;
using TiXL.Logging.Correlation;
using TiXL.Logging.Setup;
using Xunit;

namespace TiXL.Tests.Examples
{
    /// <summary>
    /// Integration test demonstrating the complete TiXL structured logging framework
    /// </summary>
    public class TiXLLoggingIntegrationTests
    {
        [Fact]
        public void CompleteLoggingFramework_DemonstratesEndToEndFunctionality()
        {
            // Create a test host with TiXL logging
            var host = Host.CreateDefaultBuilder()
                .SetupConsoleLogging(config =>
                {
                    config.MinimumLevel = LogEventLevel.Information;
                    config.ModuleLevels = new Dictionary<string, LogEventLevel>
                    {
                        ["Core"] = LogEventLevel.Debug,
                        ["Graphics"] = LogEventLevel.Information,
                        ["Performance"] = LogEventLevel.Debug
                    };
                })
                .ConfigureServices(services =>
                {
                    services.AddTransient<TestLoggingService>();
                })
                .Build();

            var services = host.Services;
            var correlationIdProvider = services.GetRequiredService<ICorrelationIdProvider>();
            var coreLogger = services.GetRequiredService<ICoreLogger>();
            var graphicsLogger = services.GetRequiredService<IGraphicsLogger>();
            var performanceLogger = services.GetRequiredService<IPerformanceLogger>();
            var operationTracker = services.GetRequiredService<IOperationTracker>();
            var loggerFactory = services.GetRequiredService<ILoggerFactory>();

            // Test basic logging functionality
            var correlationId = correlationIdProvider.CreateCorrelationId();
            correlationIdProvider.SetCorrelationId(correlationId);

            var mainOperationId = operationTracker.StartOperation("IntegrationTest", null, new Dictionary<string, object?>
            {
                ["TestType"] = "CompleteFramework",
                ["CorrelationId"] = correlationId
            });

            try
            {
                // Test Core module logging
                coreLogger.LogSystemInitialization("TestComponent", TimeSpan.FromMilliseconds(125.5));
                coreLogger.LogConfigurationChange("Setting1", "Value1", "Value2");
                coreLogger.LogMemoryMetrics(
                    workingSet: 1024 * 1024 * 100,
                    privateMemory: 1024 * 1024 * 80,
                    gen0Collections: 5,
                    gen1Collections: 2,
                    gen2Collections: 1);

                // Test Graphics module logging
                var graphicsOperationId = operationTracker.StartOperation("GraphicsTest", mainOperationId);
                graphicsLogger.LogDeviceInitialization("TestDevice", true, TimeSpan.FromMilliseconds(150));
                graphicsLogger.LogResourceCreation("Texture2D", 1024 * 1024 * 4, true);
                graphicsLogger.LogShaderCompilation("TestShader", true, TimeSpan.FromMilliseconds(75));
                graphicsLogger.LogRenderingPass("TestPass", 1000, 25, TimeSpan.FromMilliseconds(16.7));
                graphicsLogger.LogFrameRate(60.0, TimeSpan.FromMilliseconds(16.67));
                _ = operationTracker.EndOperation(graphicsOperationId, true);

                // Test Performance module logging
                var performanceOperationId = operationTracker.StartOperation("PerformanceTest", mainOperationId);
                performanceLogger.LogBenchmarkStart("TestBenchmark");
                Thread.Sleep(100); // Simulate some work
                performanceLogger.LogBenchmarkEnd("TestBenchmark", TimeSpan.FromMilliseconds(100));
                performanceLogger.LogMemoryPressure(1024 * 1024 * 500, 1024 * 1024 * 1024);
                performanceLogger.LogCpuUsage(45.5, 1);
                performanceLogger.LogGcMetrics(10, 3, 1, TimeSpan.FromMilliseconds(15.5));
                _ = operationTracker.EndOperation(performanceOperationId, true);

                // Test service integration
                var testService = services.GetRequiredService<TestLoggingService>();
                testService.PerformComplexOperation();

                // Verify operations were tracked
                var allOperations = operationTracker.GetOperations(correlationId);
                Assert.True(allOperations.Count >= 3, $"Expected at least 3 operations, got {allOperations.Count}");

                // Verify all operations have the correct correlation ID
                Assert.All(allOperations, op => 
                {
                    Assert.Equal(correlationId, op.CorrelationId);
                    Assert.NotNull(op.OperationId);
                    Assert.NotNull(op.Name);
                });

                // Test transaction logging
                var transactionLogger = services.GetRequiredService<ITransactionLogger>();
                var transactionId = transactionLogger.StartTransaction("TestTransaction", mainOperationId);
                transactionLogger.LogTransactionEvent(transactionId, "Event1", new Dictionary<string, object?> { ["Key"] = "Value" });
                transactionLogger.LogTransactionEvent(transactionId, "Event2");
                transactionLogger.EndTransaction(transactionId, true, "Success");

                operationTracker.EndOperation(mainOperationId, true);

                // Verify correlation ID was maintained throughout
                Assert.Equal(correlationId, correlationIdProvider.GetCorrelationId());
            }
            catch (Exception ex)
            {
                operationTracker.EndOperation(mainOperationId, false, ex.Message);
                throw;
            }
            finally
            {
                host.Dispose();
            }
        }
    }

    /// <summary>
    /// Test service that demonstrates integrated logging across multiple modules
    /// </summary>
    public class TestLoggingService
    {
        private readonly ICoreLogger _coreLogger;
        private readonly IGraphicsLogger _graphicsLogger;
        private readonly IPerformanceLogger _performanceLogger;
        private readonly IOperationTracker _operationTracker;
        private readonly ILogger<TestLoggingService> _logger;

        public TestLoggingService(
            ICoreLogger coreLogger,
            IGraphicsLogger graphicsLogger,
            IPerformanceLogger performanceLogger,
            IOperationTracker operationTracker,
            ILogger<TestLoggingService> logger)
        {
            _coreLogger = coreLogger;
            _graphicsLogger = graphicsLogger;
            _performanceLogger = performanceLogger;
            _operationTracker = operationTracker;
            _logger = logger;
        }

        public void PerformComplexOperation()
        {
            var operationId = _operationTracker.StartOperation("ComplexTestOperation", null, new Dictionary<string, object?>
            {
                ["OperationType"] = "IntegrationTest",
                ["Complexity"] = "High"
            });

            try
            {
                _logger.LogInformation("Starting complex test operation");

                // Simulate multiple steps with different module logging
                var step1Id = _operationTracker.StartOperation("Step1_CoreOperation", operationId);
                _coreLogger.LogSystemInitialization("TestStep1", TimeSpan.FromMilliseconds(50));
                _operationTracker.EndOperation(step1Id, true);

                var step2Id = _operationTracker.StartOperation("Step2_GraphicsOperation", operationId);
                _graphicsLogger.LogDeviceInitialization("TestDevice2", true, TimeSpan.FromMilliseconds(75));
                _operationTracker.EndOperation(step2Id, true);

                var step3Id = _operationTracker.StartOperation("Step3_PerformanceOperation", operationId);
                _performanceLogger.LogBenchmarkStart("InnerBenchmark");
                Thread.Sleep(50); // Simulate work
                _performanceLogger.LogBenchmarkEnd("InnerBenchmark", TimeSpan.FromMilliseconds(50));
                _operationTracker.EndOperation(step3Id, true);

                // Simulate a performance issue for testing
                _performanceLogger.LogPerformanceAlert("TestAlert", "This is a test performance alert", LogLevel.Warning);

                _logger.LogInformation("Complex test operation completed successfully");
                _operationTracker.EndOperation(operationId, true);
            }
            catch (Exception ex)
            {
                _coreLogger.LogException(ex, "ComplexTestOperation", LogLevel.Error);
                _operationTracker.EndOperation(operationId, false, ex.Message);
                throw;
            }
        }
    }
}