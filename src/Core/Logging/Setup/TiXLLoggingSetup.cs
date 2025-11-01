// TiXL Logging Setup and Initialization Helpers
// Provides convenient setup methods for different application types

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using TiXL.Logging.Enrichers;
using TiXL.Logging.Correlation;

namespace TiXL.Logging.Setup
{
    /// <summary>
    /// TiXL Logging Setup Configuration
    /// Provides centralized configuration for different application types
    /// </summary>
    public static class TiXLLoggingSetup
    {
        /// <summary>
        /// Setup TiXL logging for console applications
        /// </summary>
        public static HostApplicationBuilder SetupConsoleLogging(this HostApplicationBuilder builder, Action<ConsoleLoggingOptions>? configure = null)
        {
            var options = new ConsoleLoggingOptions();
            configure?.Invoke(options);

            var config = new TiXLLoggingConfiguration
            {
                Environment = options.Environment ?? Environments.Development,
                Version = options.Version ?? "1.0.0",
                MinimumLevel = options.MinimumLevel ?? LogEventLevel.Information,
                EnableConsole = true,
                EnableDebug = options.EnableDebug,
                EnableFile = options.EnableFile,
                EnableStructuredLog = options.EnableStructuredLog
            };

            // Configure module levels
            if (options.ModuleLevels != null)
            {
                foreach (var moduleLevel in options.ModuleLevels)
                {
                    config.ConfigureModuleLevel(moduleLevel.Key, moduleLevel.Value);
                }
            }

            builder.Host.UseTiXLLogging(config);

            // Add core logging services
            builder.Services.AddTiXLLoggingEnrichers();
            builder.Services.AddTiXLCorrelation();

            // Add module-specific loggers
            builder.Services.AddTransient<ICoreLogger, CoreLogger>();
            builder.Services.AddTransient<IGraphicsLogger, GraphicsLogger>();
            builder.Services.AddTransient<IEditorLogger, EditorLogger>();
            builder.Services.AddTransient<IOperatorsLogger, OperatorsLogger>();
            builder.Services.AddTransient<IPerformanceLogger, PerformanceLogger>();

            return builder;
        }

        /// <summary>
        /// Setup TiXL logging for web applications (ASP.NET Core)
        /// </summary>
        public static IServiceCollection SetupWebLogging(this IServiceCollection services, Action<WebLoggingOptions>? configure = null)
        {
            var options = new WebLoggingOptions();
            configure?.Invoke(options);

            // Add correlation services
            services.AddTiXLCorrelation(configureOptions =>
            {
                configureOptions.HeaderName = options.CorrelationHeaderName;
                configureOptions.ResponseHeaderName = options.CorrelationResponseHeaderName;
                configureOptions.QueryStringParameterName = options.CorrelationQueryStringName;
                configureOptions.IncludeInResponse = options.IncludeCorrelationIdInResponse;
            });

            // Add logging enrichers
            services.AddTiXLLoggingEnrichers();

            // Add operation context support
            services.AddOperationContext();

            // Add module-specific loggers
            services.TryAddTransient<ICoreLogger, CoreLogger>();
            services.TryAddTransient<IGraphicsLogger, GraphicsLogger>();
            services.TryAddTransient<IEditorLogger, EditorLogger>();
            services.TryAddTransient<IOperatorsLogger, OperatorsLogger>();
            services.TryAddTransient<IPerformanceLogger, PerformanceLogger>();

            // Add operation tracker and transaction logger
            services.AddSingleton<IOperationTracker, OperationTracker>();
            services.AddSingleton<ITransactionLogger, TransactionLogger>();

            // Add logging configuration service
            services.AddSingleton<ILoggingConfigurationService, LoggingConfigurationService>();

            return services;
        }

        /// <summary>
        /// Setup TiXL logging middleware for web applications
        /// </summary>
        public static IApplicationBuilder UseTiXLWebLogging(this IApplicationBuilder app)
        {
            // Add correlation ID middleware
            app.UseTiXLCorrelation();
            app.UseWebSocketCorrelation();

            // Add operation context middleware
            app.UseMiddleware<OperationContextMiddleware>();
            app.UseMiddleware<UserContextMiddleware>();

            return app;
        }

        /// <summary>
        /// Setup TiXL logging for Windows Services
        /// </summary>
        public static HostBuilder SetupWindowsServiceLogging(this HostBuilder builder, Action<WindowsServiceLoggingOptions>? configure = null)
        {
            var options = new WindowsServiceLoggingOptions();
            configure?.Invoke(options);

            var config = new TiXLLoggingConfiguration
            {
                Environment = options.Environment ?? Environments.Production,
                Version = options.Version ?? "1.0.0",
                MinimumLevel = options.MinimumLevel ?? LogEventLevel.Information,
                EnableConsole = false, // No console for Windows Services
                EnableFile = true,
                EnableStructuredLog = true,
                EnableDebug = false
            };

            // Configure file logging for Windows Services
            config.FilePath = options.LogFilePath ?? "C:\\ProgramData\\TiXL\\Logs\\tixl-{Date}.log";
            config.StructuredLogPath = options.StructuredLogPath ?? "C:\\ProgramData\\TiXL\\Logs\\tixl-structured-{Date}.log";
            config.RollingInterval = RollingInterval.Day;
            config.RetainedFileCount = options.RetainedFileCount ?? 30;

            builder.UseWindowsService();
            builder.UseTiXLLogging(config);

            return builder;
        }

        /// <summary>
        /// Setup TiXL logging for unit tests
        /// </summary>
        public static IServiceCollection SetupTestLogging(this IServiceCollection services, Action<TestLoggingOptions>? configure = null)
        {
            var options = new TestLoggingOptions();
            configure?.Invoke(options);

            // Configure test-specific logging
            var config = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Debug()
                .WriteTo.TestOutput()
                .Enrich.FromLogContext()
                .Enrich.WithProperty("TestEnvironment", "UnitTest");

            var logger = config.CreateLogger();
            
            services.AddSingleton(logger);
            services.AddSingleton<ILoggerFactory>(provider => LoggerFactory.Create(builder =>
            {
                builder.AddSerilog(logger, dispose: false);
                builder.AddXUnit(); // For test output integration
            }));

            // Add test logging services
            services.AddTiXLLoggingEnrichers();
            services.AddTiXLCorrelation();

            // Add test-specific module loggers
            services.AddTransient<ICoreLogger, CoreLogger>();
            services.AddTransient<IGraphicsLogger, GraphicsLogger>();
            services.AddTransient<IEditorLogger, EditorLogger>();
            services.AddTransient<IOperatorsLogger, OperatorsLogger>();
            services.AddTransient<IPerformanceLogger, PerformanceLogger>();

            return services;
        }
    }

    /// <summary>
    /// Configuration options for console application logging
    /// </summary>
    public class ConsoleLoggingOptions
    {
        public string? Environment { get; set; } = Environments.Development;
        public string? Version { get; set; } = "1.0.0";
        public LogEventLevel? MinimumLevel { get; set; } = LogEventLevel.Information;
        public bool EnableDebug { get; set; } = true;
        public bool EnableFile { get; set; } = true;
        public bool EnableStructuredLog { get; set; } = false;
        public Dictionary<string, LogEventLevel>? ModuleLevels { get; set; }
    }

    /// <summary>
    /// Configuration options for web application logging
    /// </summary>
    public class WebLoggingOptions
    {
        public string CorrelationHeaderName { get; set; } = "X-Correlation-Id";
        public string CorrelationResponseHeaderName { get; set; } = "X-Correlation-Id";
        public string CorrelationQueryStringName { get; set; } = "correlationId";
        public bool IncludeCorrelationIdInResponse { get; set; } = true;
        public Dictionary<string, LogEventLevel>? ModuleLevels { get; set; }
        public List<Action<LoggerConfiguration>>? CustomSinks { get; set; }
    }

    /// <summary>
    /// Configuration options for Windows Service logging
    /// </summary>
    public class WindowsServiceLoggingOptions
    {
        public string? Environment { get; set; } = Environments.Production;
        public string? Version { get; set; } = "1.0.0";
        public LogEventLevel? MinimumLevel { get; set; } = LogEventLevel.Information;
        public string? LogFilePath { get; set; }
        public string? StructuredLogPath { get; set; }
        public int? RetainedFileCount { get; set; } = 30;
        public Dictionary<string, LogEventLevel>? ModuleLevels { get; set; }
    }

    /// <summary>
    /// Configuration options for test logging
    /// </summary>
    public class TestLoggingOptions
    {
        public bool EnableDetailedLogging { get; set; } = true;
        public bool EnableCorrelationTracking { get; set; } = true;
        public bool EnablePerformanceTracking { get; set; } = true;
        public Dictionary<string, LogEventLevel>? ModuleLevels { get; set; }
    }

    /// <summary>
    /// Operation context middleware for web applications
    /// </summary>
    public class OperationContextMiddleware
    {
        private readonly RequestDelegate _next;

        public OperationContextMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var operationName = $"{context.Request.Method} {context.Request.Path}";
            
            // Set operation context for logging
            using (Serilog.Context.LogContext.PushProperty("OperationName", operationName))
            using (Serilog.Context.LogContext.PushProperty("RequestMethod", context.Request.Method))
            using (Serilog.Context.LogContext.PushProperty("RequestPath", context.Request.Path))
            {
                await _next(context);
            }
        }
    }

    /// <summary>
    /// User context middleware for web applications
    /// </summary>
    public class UserContextMiddleware
    {
        private readonly RequestDelegate _next;

        public UserContextMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                // Add user context
                using (Serilog.Context.LogContext.PushProperty("UserId", context.User.FindFirst("sub")?.Value))
                using (Serilog.Context.LogContext.PushProperty("UserRole", context.User.FindFirst("role")?.Value))
                using (Serilog.Context.LogContext.PushProperty("UserName", context.User.Identity.Name))
                {
                    await _next(context);
                }
            }
            else
            {
                // Anonymous user context
                using (Serilog.Context.LogContext.PushProperty("UserId", "Anonymous"))
                using (Serilog.Context.LogContext.PushProperty("UserRole", "Guest"))
                {
                    await _next(context);
                }
            }
        }
    }

    /// <summary>
    /// Performance monitoring middleware for web applications
    /// </summary>
    public class PerformanceMonitoringMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<PerformanceMonitoringMiddleware> _logger;
        private readonly IOperationTracker _operationTracker;

        public PerformanceMonitoringMiddleware(
            RequestDelegate next,
            ILogger<PerformanceMonitoringMiddleware> logger,
            IOperationTracker operationTracker)
        {
            _next = next;
            _logger = logger;
            _operationTracker = operationTracker;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var operationId = _operationTracker.StartOperation("HttpRequest", null, new Dictionary<string, object?>
            {
                ["Method"] = context.Request.Method,
                ["Path"] = context.Request.Path,
                ["QueryString"] = context.Request.QueryString.ToString()
            });

            try
            {
                await _next(context);
            }
            finally
            {
                stopwatch.Stop();
                var statusCode = context.Response.StatusCode;
                var isSuccess = statusCode >= 200 && statusCode < 300;
                
                _logger.LogInformation("HTTP {Method} {Path} responded {StatusCode} in {Duration:F3}ms",
                    context.Request.Method, context.Request.Path, statusCode, stopwatch.Elapsed.TotalMilliseconds);

                _operationTracker.EndOperation(operationId, isSuccess, 
                    isSuccess ? null : $"HTTP {statusCode}");
            }
        }
    }

    /// <summary>
    /// Service collection extensions for adding TiXL logging middleware
    /// </summary>
    public static class MiddlewareServiceExtensions
    {
        public static IServiceCollection AddTiXLWebMiddleware(this IServiceCollection services)
        {
            services.AddTransient<OperationContextMiddleware>();
            services.AddTransient<UserContextMiddleware>();
            services.AddTransient<PerformanceMonitoringMiddleware>();

            return services;
        }
    }

    /// <summary>
    /// Application builder extensions for TiXL middleware
    /// </summary>
    public static class MiddlewareApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseTiXLOperationContext(this IApplicationBuilder app)
        {
            return app.UseMiddleware<OperationContextMiddleware>();
        }

        public static IApplicationBuilder UseTiXLUserContext(this IApplicationBuilder app)
        {
            return app.UseMiddleware<UserContextMiddleware>();
        }

        public static IApplicationBuilder UseTiXLPerformanceMonitoring(this IApplicationBuilder app)
        {
            return app.UseMiddleware<PerformanceMonitoringMiddleware>();
        }
    }
}