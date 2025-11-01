// TiXL Structured Logging Framework
// Comprehensive logging infrastructure with Serilog, correlation IDs, and module-specific contexts

using Serilog;
using Serilog.Events;
using Serilog.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TiXL.Logging
{
    /// <summary>
    /// TiXL Structured Logging Configuration
    /// Provides centralized configuration for the entire TiXL logging infrastructure
    /// </summary>
    public static class TiXLLogging
    {
        /// <summary>
        /// Configure TiXL structured logging with Serilog
        /// </summary>
        public static IHostBuilder UseTiXLLogging(this IHostBuilder builder, Action<TiXLLoggingConfiguration>? configure = null)
        {
            var config = new TiXLLoggingConfiguration();
            configure?.Invoke(config);

            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Is(config.MinimumLevel)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "TiXL")
                .Enrich.WithProperty("Environment", config.Environment)
                .Enrich.WithProperty("Version", config.Version)
                .Enrich.WithExceptionDetails();

            // Configure sinks based on configuration
            ConfigureSinks(loggerConfig, config);

            Log.Logger = loggerConfig.CreateLogger();

            return builder
                .UseSerilog()
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton(Log.Logger);
                    services.AddSingleton<ICorrelationIdProvider, CorrelationIdProvider>();
                    services.AddSingleton<ILogContextProvider, LogContextProvider>();
                    services.AddSingleton<IModuleLoggerFactory, ModuleLoggerFactory>();
                    services.AddSingleton<ILoggingConfigurationService, LoggingConfigurationService>();
                    
                    // Add module-specific logging services
                    services.TryAddSingleton<ICoreLogger, CoreLogger>();
                    services.TryAddSingleton<IOperatorsLogger, OperatorsLogger>();
                    services.TryAddSingleton<IEditorLogger, EditorLogger>();
                    services.TryAddSingleton<IGraphicsLogger, GraphicsLogger>();
                    services.TryAddSingleton<IPerformanceLogger, PerformanceLogger>();

                    services.AddSingleton(config);
                    services.AddSingleton(config.Enrichers);
                });
        }

        /// <summary>
        /// Configure logging for TiXL applications
        /// </summary>
        public static ILoggingBuilder ConfigureTiXLLogging(this ILoggingBuilder builder, Action<TiXLLoggingConfiguration>? configure = null)
        {
            var config = new TiXLLoggingConfiguration();
            configure?.Invoke(config);

            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Is(config.MinimumLevel)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "TiXL")
                .Enrich.WithProperty("Environment", config.Environment)
                .Enrich.WithProperty("Version", config.Version)
                .Enrich.WithExceptionDetails();

            // Configure sinks
            ConfigureSinks(loggerConfig, config);

            builder.ClearProviders();
            builder.AddSerilog(loggerConfig.CreateLogger(), dispose: true);

            return builder;
        }

        private static void ConfigureSinks(LoggerConfiguration loggerConfig, TiXLLoggingConfiguration config)
        {
            // Console sink for development
            if (config.EnableConsole)
            {
                loggerConfig.WriteTo.Console(
                    outputTemplate: config.OutputTemplate,
                    theme: config.ConsoleTheme);
            }

            // File sink for persistent logging
            if (config.EnableFile)
            {
                loggerConfig.WriteTo.File(
                    path: config.FilePath,
                    rollingInterval: config.RollingInterval,
                    retainedFileCountLimit: config.RetainedFileCount,
                    fileSizeLimitBytes: config.FileSizeLimit,
                    outputTemplate: config.OutputTemplate,
                    rollOnFileSizeLimit: config.RollOnFileSizeLimit);
            }

            // Structured file sink for analytics
            if (config.EnableStructuredLog)
            {
                loggerConfig.WriteTo.File(
                    path: config.StructuredLogPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: config.RetainedFileCount,
                    fileSizeLimitBytes: config.FileSizeLimit,
                    formatter: new Serilog.Formatting.Json.JsonFormatter(renderMessage: true),
                    rollOnFileSizeLimit: config.RollOnFileSizeLimit);
            }

            // Debug sink for development
            if (config.EnableDebug)
            {
                loggerConfig.WriteTo.Debug();
            }

            // Add custom sinks
            foreach (var sinkConfig in config.CustomSinks)
            {
                loggerConfig.WriteTo.Logger(lc => sinkConfig(lc));
            }

            // Configure minimum level overrides per module
            foreach (var moduleLevel in config.ModuleMinimumLevels)
            {
                loggerConfig.MinimumLevel.Override(moduleLevel.Key, moduleLevel.Value);
            }

            // Configure minimum level overrides per source context
            foreach (var contextLevel in config.SourceContextMinimumLevels)
            {
                loggerConfig.MinimumLevel.Override(contextLevel.Key, contextLevel.Value);
            }
        }
    }

    /// <summary>
    /// TiXL Logging Configuration
    /// Centralized configuration for all TiXL logging infrastructure
    /// </summary>
    public class TiXLLoggingConfiguration
    {
        public LogEventLevel MinimumLevel { get; set; } = LogEventLevel.Information;
        public string Environment { get; set; } = Environments.Development;
        public string Version { get; set; } = "1.0.0";
        public string OutputTemplate { get; set; } = 
            "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {CorrelationId} {Module} {SourceContext}: {Message:lj}{NewLine}{Exception}";

        // Console configuration
        public bool EnableConsole { get; set; } = true;
        public Serilog.Sinks.Console.Themes.ConsoleTheme? ConsoleTheme { get; set; }

        // File configuration
        public bool EnableFile { get; set; } = true;
        public string FilePath { get; set; } = "logs/tixl-{Date}.log";
        public RollingInterval RollingInterval { get; set; } = RollingInterval.Day;
        public int RetainedFileCount { get; set; } = 30;
        public long FileSizeLimit { get; set; } = 1L * 1024 * 1024 * 1024; // 1GB
        public bool RollOnFileSizeLimit { get; set; } = true;

        // Structured logging
        public bool EnableStructuredLog { get; set; } = true;
        public string StructuredLogPath { get; set; } = "logs/tixl-structured-{Date}.log";

        // Development options
        public bool EnableDebug { get; set; } = true;

        // Module-specific configuration
        public Dictionary<string, LogEventLevel> ModuleMinimumLevels { get; set; } = new();
        public Dictionary<string, LogEventLevel> SourceContextMinimumLevels { get; set; } = new();

        // Enrichers
        public List<ILogEnricher> Enrichers { get; set; } = new();

        // Custom sinks
        public List<Action<LoggerConfiguration>> CustomSinks { get; set; } = new();

        /// <summary>
        /// Configure module-specific logging levels
        /// </summary>
        public TiXLLoggingConfiguration ConfigureModuleLevel(string module, LogEventLevel level)
        {
            ModuleMinimumLevels[$"TiXL.{module}"] = level;
            return this;
        }

        /// <summary>
        /// Configure source context-specific logging levels
        /// </summary>
        public TiXLLoggingConfiguration ConfigureSourceContextLevel(string sourceContext, LogEventLevel level)
        {
            SourceContextMinimumLevels[sourceContext] = level;
            return this;
        }

        /// <summary>
        /// Add custom enricher
        /// </summary>
        public TiXLLoggingConfiguration AddEnricher(ILogEnricher enricher)
        {
            Enrichers.Add(enricher);
            return this;
        }

        /// <summary>
        /// Add custom sink
        /// </summary>
        public TiXLLoggingConfiguration AddSink(Action<LoggerConfiguration> sinkConfiguration)
        {
            CustomSinks.Add(sinkConfiguration);
            return this;
        }

        /// <summary>
        /// Configure for development environment
        /// </summary>
        public TiXLLoggingConfiguration ForDevelopment()
        {
            Environment = Environments.Development;
            MinimumLevel = LogEventLevel.Debug;
            EnableDebug = true;
            EnableConsole = true;
            return this;
        }

        /// <summary>
        /// Configure for production environment
        /// </summary>
        public TiXLLoggingConfiguration ForProduction()
        {
            Environment = Environments.Production;
            MinimumLevel = LogEventLevel.Information;
            EnableDebug = false;
            EnableConsole = false;
            return this;
        }
    }

    /// <summary>
    /// Correlation ID Provider
    /// Manages correlation IDs for request tracing across modules
    /// </summary>
    public interface ICorrelationIdProvider
    {
        string GetCorrelationId();
        string CreateCorrelationId();
        void SetCorrelationId(string correlationId);
        bool HasCorrelationId { get; }
    }

    public class CorrelationIdProvider : ICorrelationIdProvider
    {
        private readonly AsyncLocal<string?> _correlationId = new();
        private static readonly Random _random = new();

        public string GetCorrelationId()
        {
            return _correlationId.Value ?? CreateCorrelationId();
        }

        public string CreateCorrelationId()
        {
            var correlationId = $"TXL-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{_random.Next(1000, 9999)}";
            _correlationId.Value = correlationId;
            return correlationId;
        }

        public void SetCorrelationId(string correlationId)
        {
            _correlationId.Value = correlationId;
        }

        public bool HasCorrelationId => !string.IsNullOrEmpty(_correlationId.Value);
    }

    /// <summary>
    /// Log Context Provider
    /// Provides contextual information for structured logging
    /// </summary>
    public interface ILogContextProvider
    {
        string ModuleName { get; }
        string OperationName { get; set; }
        Dictionary<string, object?> Properties { get; }
        void AddProperty(string key, object? value);
        void ClearProperties();
    }

    public class LogContextProvider : ILogContextProvider
    {
        public string ModuleName { get; set; }
        public string OperationName { get; set; } = string.Empty;
        public Dictionary<string, object?> Properties { get; } = new();

        public LogContextProvider(string moduleName)
        {
            ModuleName = moduleName;
        }

        public void AddProperty(string key, object? value)
        {
            Properties[key] = value;
        }

        public void ClearProperties()
        {
            Properties.Clear();
        }
    }

    /// <summary>
    /// Module Logger Factory
    /// Creates module-specific loggers with proper contexts
    /// </summary>
    public interface IModuleLoggerFactory
    {
        ILogger CreateLogger(string moduleName);
        ICoreLogger CreateCoreLogger();
        IOperatorsLogger CreateOperatorsLogger();
        IEditorLogger CreateEditorLogger();
        IGraphicsLogger CreateGraphicsLogger();
        IPerformanceLogger CreatePerformanceLogger();
    }

    public class ModuleLoggerFactory : IModuleLoggerFactory
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ICorrelationIdProvider _correlationIdProvider;

        public ModuleLoggerFactory(ILoggerFactory loggerFactory, ICorrelationIdProvider correlationIdProvider)
        {
            _loggerFactory = loggerFactory;
            _correlationIdProvider = correlationIdProvider;
        }

        public ILogger CreateLogger(string moduleName)
        {
            var logger = _loggerFactory.CreateLogger($"TiXL.{moduleName}");
            return new ModuleLogger(logger, moduleName, _correlationIdProvider);
        }

        public ICoreLogger CreateCoreLogger()
        {
            return new CoreLogger(_loggerFactory.CreateLogger("TiXL.Core"), _correlationIdProvider);
        }

        public IOperatorsLogger CreateOperatorsLogger()
        {
            return new OperatorsLogger(_loggerFactory.CreateLogger("TiXL.Operators"), _correlationIdProvider);
        }

        public IEditorLogger CreateEditorLogger()
        {
            return new EditorLogger(_loggerFactory.CreateLogger("TiXL.Editor"), _correlationIdProvider);
        }

        public IGraphicsLogger CreateGraphicsLogger()
        {
            return new GraphicsLogger(_loggerFactory.CreateLogger("TiXL.Graphics"), _correlationIdProvider);
        }

        public IPerformanceLogger CreatePerformanceLogger()
        {
            return new PerformanceLogger(_loggerFactory.CreateLogger("TiXL.Performance"), _correlationIdProvider);
        }
    }

    /// <summary>
    /// Logging Configuration Service
    /// Provides runtime access to logging configuration
    /// </summary>
    public interface ILoggingConfigurationService
    {
        TiXLLoggingConfiguration Configuration { get; }
        void UpdateConfiguration(Action<TiXLLoggingConfiguration> updateAction);
        event EventHandler<TiXLLoggingConfiguration>? ConfigurationChanged;
    }

    public class LoggingConfigurationService : ILoggingConfigurationService
    {
        public TiXLLoggingConfiguration Configuration { get; }
        public event EventHandler<TiXLLoggingConfiguration>? ConfigurationChanged;

        public LoggingConfigurationService(TiXLLoggingConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void UpdateConfiguration(Action<TiXLLoggingConfiguration> updateAction)
        {
            updateAction(Configuration);
            ConfigurationChanged?.Invoke(this, Configuration);
        }
    }
}