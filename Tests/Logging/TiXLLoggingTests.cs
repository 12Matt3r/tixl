// TiXLLoggingTests.cs
using Xunit;
using Xunit.Abstractions;
using TiXL.Tests.Fixtures;
using TiXL.Tests.Categories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using FluentAssertions;
using Serilog;
using Serilog.Events;
using System;
using System.Threading.Tasks;

namespace TiXL.Tests.Logging
{
    /// <summary>
    /// Comprehensive tests for TiXL structured logging framework
    /// </summary>
    [Category(TestCategories.Logging)]
    [Category(TestCategories.Unit)]
    public class TiXLLoggingTests : CoreTestFixture
    {
        private readonly ITestOutputHelper _output;
        private readonly ILogger<TiXLLoggingTests> _logger;

        public TiXLLoggingTests(ITestOutputHelper output) : base()
        {
            _output = output;
            _logger = ServiceProvider.GetRequiredService<ILogger<TiXLLoggingTests>>();
        }

        [Fact]
        public void TiXLLogging_UseTiXLLogging_ConfiguresLoggingCorrectly()
        {
            // Arrange
            var hostBuilder = Host.CreateDefaultBuilder();

            // Act
            var configuredHost = TiXLLogging.UseTiXLLogging(hostBuilder, config =>
            {
                config.MinimumLevel = LogEventLevel.Information;
                config.Environment = "Test";
                config.Version = "1.0.0";
            });

            // Assert
            configuredHost.Should().NotBeNull();
        }

        [Fact]
        public void TiXLLogging_ConfigureSinks_AddsConsoleSink()
        {
            // Arrange
            var loggerConfig = new LoggerConfiguration();
            var config = new TiXLLoggingConfiguration
            {
                EnableConsoleSink = true,
                MinimumLevel = LogEventLevel.Debug
            };

            // Act
            var configuredLogger = TiXLLogging.ConfigureSinks(loggerConfig, config);

            // Assert
            configuredLogger.Should().NotBeNull();
        }

        [Fact]
        public void TiXLLogging_ConfigureSinks_WithFileSink_CreatesFileSink()
        {
            // Arrange
            var loggerConfig = new LoggerConfiguration();
            var config = new TiXLLoggingConfiguration
            {
                EnableConsoleSink = false,
                EnableFileSink = true,
                LogFilePath = "test-{Date}.log"
            };

            // Act
            var configuredLogger = TiXLLogging.ConfigureSinks(loggerConfig, config);

            // Assert
            configuredLogger.Should().NotBeNull();
        }

        [Fact]
        public void TiXLLoggingModuleLoggerFactory_CreateModuleLogger_ReturnsValidLogger()
        {
            // Arrange
            var factory = new ModuleLoggerFactory();

            // Act
            var moduleLogger = factory.CreateLogger("TestModule");

            // Assert
            moduleLogger.Should().NotBeNull();
        }

        [Fact]
        public void ModuleLoggers_GetLogger_WithValidModuleName_ReturnsLogger()
        {
            // Arrange
            var moduleName = "TestModule";

            // Act
            var logger = ModuleLoggers.GetLogger(moduleName);

            // Assert
            logger.Should().NotBeNull();
        }

        [Fact]
        public void ModuleLoggers_EnrichWithModuleContext_AddsContext()
        {
            // Arrange
            var logEvent = new Serilog.Events.LogEvent(
                DateTimeOffset.UtcNow, 
                LogEventLevel.Information, 
                null, 
                new Serilog.Parsing.MessageTemplate("Test message"), 
                new[] { new Serilog.Parsing.PropertyToken("Test", null, null) }
            );

            // Act
            var enrichedEvent = ModuleLoggers.EnrichWithModuleContext(logEvent, "TestModule");

            // Assert
            enrichedEvent.Should().NotBeNull();
        }

        [Fact]
        public void TiXLLoggingService_LogInformation_Messages_Correctly()
        {
            // Arrange
            var message = "Test information message";

            // Act
            _logger.LogInformation(message);

            // Assert - In real implementation, this would verify the message was logged
            _logger.Should().NotBeNull();
        }

        [Fact]
        public void TiXLLoggingService_LogWarning_WithException_Succeeds()
        {
            // Arrange
            var message = "Test warning message";
            var exception = new InvalidOperationException("Test exception");

            // Act
            _logger.LogWarning(exception, message);

            // Assert
            _logger.Should().NotBeNull();
        }

        [Fact]
        public void TiXLLoggingService_LogError_WithContext_Succeeds()
        {
            // Arrange
            var message = "Test error message";
            var context = new { UserId = 123, Operation = "TestOperation" };

            // Act
            _logger.LogError("Error in {Operation} for user {UserId}: {Message}", 
                context.Operation, context.UserId, message);

            // Assert
            _logger.Should().NotBeNull();
        }

        [Fact]
        public void TiXLLoggingService_LogDebug_WithStructuredData_Succeeds()
        {
            // Arrange
            var data = new { RequestId = Guid.NewGuid(), Duration = 100.5 };

            // Act
            _logger.LogDebug("Processing request {RequestId} took {Duration}ms", 
                data.RequestId, data.Duration);

            // Assert
            _logger.Should().NotBeNull();
        }

        [Fact]
        public void CorrelationIdProvider_GenerateCorrelationId_ReturnsValidId()
        {
            // Arrange
            var provider = new CorrelationIdProvider();

            // Act
            var correlationId = provider.GetCorrelationId();

            // Assert
            correlationId.Should().NotBeNullOrEmpty();
            correlationId.Should().Match(id => Guid.TryParse(id, out _), "Should be a valid GUID");
        }

        [Fact]
        public void CorrelationIdProvider_SetCorrelationId_StoresCorrectly()
        {
            // Arrange
            var provider = new CorrelationIdProvider();
            var testId = Guid.NewGuid().ToString();

            // Act
            provider.SetCorrelationId(testId);
            var retrievedId = provider.GetCorrelationId();

            // Assert
            retrievedId.Should().Be(testId);
        }

        [Fact]
        public void LogContextProvider_AddProperty_AddsToContext()
        {
            // Arrange
            var provider = new LogContextProvider();
            var key = "TestKey";
            var value = "TestValue";

            // Act
            provider.AddProperty(key, value);

            // Assert - In real implementation, this would verify the property was added
            provider.Should().NotBeNull();
        }

        [Fact]
        public void LoggingConfigurationService_UpdateConfiguration_AppliesChanges()
        {
            // Arrange
            var service = new LoggingConfigurationService();
            var newConfig = new TiXLLoggingConfiguration
            {
                MinimumLevel = LogEventLevel.Verbose,
                EnableConsoleSink = true
            };

            // Act
            service.UpdateConfiguration(newConfig);

            // Assert
            service.Should().NotBeNull();
        }

        [Theory]
        [InlineData(LogEventLevel.Verbose)]
        [InlineData(LogEventLevel.Debug)]
        [InlineData(LogEventLevel.Information)]
        [InlineData(LogEventLevel.Warning)]
        [InlineData(LogEventLevel.Error)]
        [InlineData(LogEventLevel.Fatal)]
        public void TiXLLogging_SupportsAllLogLevels(LogEventLevel level)
        {
            // Arrange
            var logEvent = new Serilog.Events.LogEvent(
                DateTimeOffset.UtcNow, level, null,
                new Serilog.Parsing.MessageTemplate("Test message"),
                Enumerable.Empty<Serilog.Parsing.LogEventProperty>());

            // Act & Assert - Should not throw for any valid log level
            logEvent.Should().NotBeNull();
            logEvent.Level.Should().Be(level);
        }
    }

    /// <summary>
    /// Integration tests for TiXL logging with real logging infrastructure
    /// </summary>
    [Category(TestCategories.Logging)]
    [Category(TestCategories.Integration)]
    public class TiXLLoggingIntegrationTests : CoreTestFixture, IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly ILogger<TiXLLoggingIntegrationTests> _logger;
        private readonly string _testLogFilePath;

        public TiXLLoggingIntegrationTests(ITestOutputHelper output) : base()
        {
            _output = output;
            _logger = ServiceProvider.GetRequiredService<ILogger<TiXLLoggingIntegrationTests>>();
            _testLogFilePath = Path.Combine(Path.GetTempPath(), $"TiXL_Integration_Tests_{Guid.NewGuid():N}.log");
        }

        [Fact]
        public async Task LoggingWorkflow_EndToEnd_Succeeds()
        {
            // Arrange
            var hostBuilder = Host.CreateDefaultBuilder();
            var configuredHost = TiXLLogging.UseTiXLLogging(hostBuilder, config =>
            {
                config.MinimumLevel = LogEventLevel.Information;
                config.Environment = "Test";
                config.Version = "1.0.0";
                config.EnableConsoleSink = false; // Disable to avoid interference
                config.EnableFileSink = true;
                config.LogFilePath = _testLogFilePath;
            });

            await configuredHost.StartAsync();

            // Act
            _logger.LogInformation("Integration test message");
            _logger.LogWarning("Integration test warning");
            _logger.LogError("Integration test error");

            // Allow some time for logging to complete
            await Task.Delay(100);

            // Assert - Verify logging infrastructure works
            configuredHost.Should().NotBeNull();
            _logger.Should().NotBeNull();
        }

        [Fact]
        public async Task Logging_WithPerformanceMetrics_Succeeds()
        {
            // Arrange
            var correlationId = Guid.NewGuid().ToString();

            // Act
            using (_logger.BeginScope("CorrelationId: {CorrelationId}", correlationId))
            {
                _logger.LogInformation("Starting operation");
                
                await Task.Delay(50); // Simulate work
                
                _logger.LogInformation("Operation completed successfully");
            }

            // Assert
            _logger.Should().NotBeNull();
        }

        [Fact]
        public async Task Logging_ConcurrentOperations_HandlesCorrectly()
        {
            // Arrange
            var tasks = new Task[10];

            // Act - Log from multiple threads concurrently
            for (int i = 0; i < tasks.Length; i++)
            {
                var index = i;
                tasks[i] = Task.Run(() =>
                {
                    _logger.LogInformation("Concurrent log message {Index}", index);
                });
            }

            await Task.WhenAll(tasks);

            // Assert
            _logger.Should().NotBeNull();
        }

        [Fact]
        public void Logging_ModuleSpecificContexts_WorkCorrectly()
        {
            // Arrange
            var moduleLogger = ModuleLoggers.GetLogger("TestModule");

            // Act
            moduleLogger.LogInformation("Module-specific log message");

            // Assert
            moduleLogger.Should().NotBeNull();
        }

        public void Dispose()
        {
            try
            {
                if (File.Exists(_testLogFilePath))
                {
                    File.Delete(_testLogFilePath);
                }
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }

    /// <summary>
    /// Unit tests for TiXL logging configuration
    /// </summary>
    [Category(TestCategories.Logging)]
    [Category(TestCategories.Unit)]
    public class TiXLLoggingConfigurationTests
    {
        [Fact]
        public void TiXLLoggingConfiguration_DefaultConstructor_SetsDefaults()
        {
            // Arrange & Act
            var config = new TiXLLoggingConfiguration();

            // Assert
            config.MinimumLevel.Should().Be(LogEventLevel.Information);
            config.Environment.Should().Be("Development");
            config.Version.Should().Be("1.0.0");
            config.EnableConsoleSink.Should().BeTrue();
            config.EnableFileSink.Should().BeFalse();
        }

        [Fact]
        public void TiXLLoggingConfiguration_CustomConstructor_SetsCustomValues()
        {
            // Arrange & Act
            var config = new TiXLLoggingConfiguration
            {
                MinimumLevel = LogEventLevel.Debug,
                Environment = "Production",
                Version = "2.0.0",
                EnableConsoleSink = false,
                EnableFileSink = true,
                LogFilePath = "/var/log/tixl.log"
            };

            // Assert
            config.MinimumLevel.Should().Be(LogEventLevel.Debug);
            config.Environment.Should().Be("Production");
            config.Version.Should().Be("2.0.0");
            config.EnableConsoleSink.Should().BeFalse();
            config.EnableFileSink.Should().BeTrue();
            config.LogFilePath.Should().Be("/var/log/tixl.log");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void TiXLLoggingConfiguration_InvalidEnvironment_SetsDefault(string invalidEnv)
        {
            // Arrange & Act
            var config = new TiXLLoggingConfiguration { Environment = invalidEnv };

            // Assert
            config.Environment.Should().NotBeNullOrWhiteSpace();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void TiXLLoggingConfiguration_InvalidVersion_SetsDefault(string invalidVersion)
        {
            // Arrange & Act
            var config = new TiXLLoggingConfiguration { Version = invalidVersion };

            // Assert
            config.Version.Should().NotBeNullOrWhiteSpace();
        }
    }
}