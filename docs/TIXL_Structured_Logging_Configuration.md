// TiXL Structured Logging Framework - Configuration Documentation
// Comprehensive guide for configuring and using the TiXL logging system

/*
TIXL STRUCTURED LOGGING FRAMEWORK (TIXL-005)
==============================================

OVERVIEW
--------
The TiXL Structured Logging Framework provides comprehensive logging capabilities with:
- Serilog-based structured logging
- Correlation ID propagation across modules
- Module-specific logging interfaces
- Performance monitoring integration
- Rich contextual enrichment

ARCHITECTURE
------------
1. Core Components:
   - TiXLLogging: Main configuration and setup
   - ModuleLoggers: Module-specific logging interfaces (Core, Graphics, Editor, Operators, Performance)
   - Enrichers: Context enrichment (Operation, User, Performance, Environment)
   - Correlation: Cross-module operation tracking and correlation ID propagation

2. Logging Flow:
   Request → Correlation ID → Module Logger → Structured Output → Sinks

SETUP AND CONFIGURATION
-----------------------

1. BASIC SETUP (Console Application):
----------------------------------------

using TiXL.Logging;
using Microsoft.Extensions.Hosting;

// In Program.cs or startup
Host.CreateDefaultBuilder(args)
    .UseTiXLLogging(config =>
    {
        config.ForDevelopment()
               .ConfigureModuleLevel("Graphics", LogEventLevel.Debug)
               .ConfigureModuleLevel("Core", LogEventLevel.Information);
    })
    .ConfigureServices(services =>
    {
        // Your services here
    })
    .Build()
    .Run();

2. ASP.NET CORE SETUP:
------------------------

using TiXL.Logging;
using TiXL.Logging.Correlation;
using Microsoft.AspNetCore.Builder;

// In Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    services.AddTiXLCorrelation(); // Add correlation functionality
    services.AddTiXLLoggingEnrichers(); // Add enrichers
    
    // Add module-specific loggers
    services.AddTransient<ICoreLogger, CoreLogger>();
    services.AddTransient<IGraphicsLogger, GraphicsLogger>();
    services.AddTransient<IEditorLogger, EditorLogger>();
    services.AddTransient<IOperatorsLogger, OperatorsLogger>();
    services.AddTransient<IPerformanceLogger, PerformanceLogger>();
}

public void Configure(IApplicationBuilder app, IHostEnvironment env)
{
    // Add correlation middleware
    app.UseTiXLCorrelation();
    app.UseWebSocketCorrelation();
    
    // Other middleware...
}

3. ADVANCED CONFIGURATION:
---------------------------

// Custom configuration with multiple sinks
Host.CreateDefaultBuilder(args)
    .UseTiXLLogging(config =>
    {
        // Environment-specific settings
        if (env.IsDevelopment())
        {
            config.ForDevelopment()
                  .ConfigureModuleLevel("Debug", LogEventLevel.Verbose)
                  .ConfigureSourceContextLevel("Microsoft", LogEventLevel.Warning);
        }
        else
        {
            config.ForProduction()
                  .ConfigureModuleLevel("Core", LogEventLevel.Warning)
                  .AddEnricher<CustomEnricher>();
        }

        // Custom sinks
        config.AddSink(sinkConfig =>
        {
            sinkConfig.WriteTo.Seq("http://localhost:5341");
        });

        // Module-specific minimum levels
        config.ConfigureModuleLevel("Core", LogEventLevel.Debug)
              .ConfigureModuleLevel("Graphics", LogEventLevel.Information)
              .ConfigureModuleLevel("Editor", LogEventLevel.Information)
              .ConfigureModuleLevel("Operators", LogEventLevel.Information)
              .ConfigureModuleLevel("Performance", LogEventLevel.Debug);
    });

MODULE-SPECIFIC LOGGING
------------------------

1. CORE MODULE LOGGING:
------------------------
public class CoreService
{
    private readonly ICoreLogger _logger;
    
    public CoreService(ICoreLogger logger)
    {
        _logger = logger;
    }
    
    public void Initialize()
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Initialize components
            InitializeComponents();
            
            stopwatch.Stop();
            _logger.LogSystemInitialization("CoreService", stopwatch.Elapsed);
            
            // Log configuration changes
            _logger.LogConfigurationChange("MaxConnections", 100, 200);
            
            // Monitor memory
            _logger.LogMemoryMetrics(
                workingSet: Environment.WorkingSet,
                privateMemory: Process.GetCurrentProcess().PrivateMemorySize64,
                gen0Collections: GC.CollectionCount(0),
                gen1Collections: GC.CollectionCount(1),
                gen2Collections: GC.CollectionCount(2));
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "CoreService initialization", LogLevel.Critical);
            throw;
        }
    }
}

2. GRAPHICS MODULE LOGGING:
-----------------------------
public class GraphicsDeviceService
{
    private readonly IGraphicsLogger _logger;
    
    public GraphicsDeviceService(IGraphicsLogger logger)
    {
        _logger = logger;
    }
    
    public async Task<Device> CreateDeviceAsync()
    {
        var operationId = _operationTracker.StartOperation("CreateGraphicsDevice");
        
        try
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Device initialization
            var device = await InitializeDeviceAsync();
            
            stopwatch.Stop();
            _logger.LogDeviceInitialization("D3D12", true, stopwatch.Elapsed);
            
            // Resource creation with tracking
            await CreateInitialResources(device);
            
            _operationTracker.EndOperation(operationId, true);
            return device;
        }
        catch (Exception ex)
        {
            _operationTracker.EndOperation(operationId, false, ex.Message);
            _logger.LogGraphicsError(ex, "Device creation", LogLevel.Error);
            throw;
        }
    }
    
    private async Task CreateInitialResources(Device device)
    {
        var resourceOperationId = _operationTracker.StartOperation("CreateInitialResources");
        
        try
        {
            // Create textures
            var texture = await CreateTextureAsync(device, 1920, 1080);
            _logger.LogResourceCreation("Texture2D", 1920 * 1080 * 4, true); // 4 bytes per pixel
            
            // Compile shaders
            _logger.LogShaderCompilation("VertexShader", true, TimeSpan.FromMilliseconds(50));
            _logger.LogShaderCompilation("PixelShader", true, TimeSpan.FromMilliseconds(45));
            
            _operationTracker.EndOperation(resourceOperationId, true);
        }
        catch (Exception ex)
        {
            _operationTracker.EndOperation(resourceOperationId, false, ex.Message);
            throw;
        }
    }
}

CORRELATION ID PROPAGATION
---------------------------

1. AUTOMATIC PROPAGATION (ASP.NET Core):
------------------------------------------
app.UseTiXLCorrelation(); // Automatically adds X-Correlation-Id header

// Client request:
GET /api/graphics/device HTTP/1.1
X-Correlation-Id: TXL-20240101-143052-1234

// All log entries will include:
// {"CorrelationId":"TXL-20240101-143052-1234","Module":"Graphics",...}

2. MANUAL CORRELATION:
-----------------------
public class GraphicsPipelineService
{
    private readonly ICorrelationIdProvider _correlationIdProvider;
    private readonly IOperationTracker _operationTracker;
    
    public async Task ProcessFrameAsync(string correlationId)
    {
        // Set correlation ID for this frame
        _correlationIdProvider.SetCorrelationId(correlationId);
        
        var frameOperationId = _operationTracker.StartOperation("ProcessFrame", null, new Dictionary<string, object?>
        {
            ["FrameNumber"] = currentFrameNumber,
            ["CorrelationId"] = correlationId
        });
        
        try
        {
            await RenderFrameAsync();
            _operationTracker.EndOperation(frameOperationId, true);
        }
        catch (Exception ex)
        {
            _operationTracker.EndOperation(frameOperationId, false, ex.Message);
            throw;
        }
    }
}

3. CROSS-SERVICE CORRELATION:
------------------------------
public class GraphicsOrchestratorService
{
    private readonly GraphicsDeviceService _deviceService;
    private readonly GraphicsRendererService _rendererService;
    private readonly IOperationTracker _operationTracker;
    
    public async Task RenderSceneAsync(Scene scene)
    {
        var orchestrationId = _operationTracker.StartOperation("RenderScene");
        
        try
        {
            // Device operations (separate service)
            var deviceOpId = _operationTracker.StartOperation("EnsureDeviceReady", orchestrationId);
            await _deviceService.EnsureDeviceReadyAsync();
            _operationTracker.EndOperation(deviceOpId, true);
            
            // Rendering operations (separate service)
            var renderOpId = _operationTracker.StartOperation("RenderScene", orchestrationId);
            await _rendererService.RenderAsync(scene);
            _operationTracker.EndOperation(renderOpId, true);
            
            _operationTracker.EndOperation(orchestrationId, true);
        }
        catch (Exception ex)
        {
            _operationTracker.EndOperation(orchestrationId, false, ex.Message);
            throw;
        }
    }
}

PERFORMANCE MONITORING
-----------------------

1. AUTOMATIC PERFORMANCE LOGGING:
-----------------------------------
public class PerformanceMonitor
{
    private readonly IPerformanceLogger _logger;
    
    public void LogBenchmark(string benchmarkName, Action benchmarkAction)
    {
        var benchmarkOperationId = _operationTracker.StartOperation($"Benchmark_{benchmarkName}");
        
        _logger.LogBenchmarkStart(benchmarkName);
        
        var stopwatch = Stopwatch.StartNew();
        var memoryBefore = GC.GetTotalMemory(false);
        
        try
        {
            benchmarkAction();
            
            stopwatch.Stop();
            var memoryAfter = GC.GetTotalMemory(false);
            
            _logger.LogBenchmarkEnd(benchmarkName, stopwatch.Elapsed, new Dictionary<string, object?>
            {
                ["MemoryDelta"] = memoryAfter - memoryBefore,
                ["GCCollections"] = GC.CollectionCount(0)
            });
            
            _operationTracker.EndOperation(benchmarkOperationId, true);
        }
        catch (Exception ex)
        {
            _operationTracker.EndOperation(benchmarkOperationId, false, ex.Message);
            throw;
        }
    }
}

2. CUSTOM PERFORMANCE METRICS:
--------------------------------
public class GraphicsPerformanceTracker
{
    private readonly IPerformanceLogger _logger;
    
    public void TrackFrameRate(double fps)
    {
        _logger.LogPerformanceAlert(
            "FrameRate", 
            fps < 30 ? $"Low frame rate detected: {fps:F1} FPS" : null,
            fps < 30 ? LogLevel.Warning : LogLevel.Information);
    }
    
    public void TrackMemoryUsage()
    {
        var process = Process.GetCurrentProcess();
        _logger.LogMemoryPressure(
            process.WorkingSet64,
            1024L * 1024 * 1024, // 1GB threshold
            LogLevel.Warning);
    }
    
    public void TrackGcPerformance()
    {
        _logger.LogGcMetrics(
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2),
            GetGcTime());
    }
}

ENRICHMENT AND CONTEXT
-----------------------

1. OPERATION CONTEXT:
----------------------
public class OperationContextMiddleware
{
    private readonly RequestDelegate _next;
    
    public async Task InvokeAsync(HttpContext context)
    {
        var operationName = $"{context.Request.Method} {context.Request.Path}";
        
        // Set operation context
        using (Serilog.Context.LogContext.PushProperty("OperationName", operationName))
        {
            await _next(context);
        }
    }
}

2. USER CONTEXT:
-----------------
public class UserContextMiddleware
{
    private readonly RequestDelegate _next;
    
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            // Add user context
            using (Serilog.Context.LogContext.PushProperty("UserId", context.User.FindFirst("sub")?.Value))
            using (Serilog.Context.LogContext.PushProperty("UserRole", context.User.FindFirst("role")?.Value))
            {
                await _next(context);
            }
        }
        else
        {
            await _next(context);
        }
    }
}

LOGGING PATTERNS
----------------

1. SUCCESS PATTERN:
-------------------
public async Task<Result> ProcessAsync(Data input)
{
    var operationId = _operationTracker.StartOperation("ProcessData");
    
    try
    {
        // Validate
        if (!Validate(input))
        {
            _logger.LogWarning("Validation failed for operation {OperationId}", operationId);
            return Result.Failed("Validation failed");
        }
        
        // Process
        var result = await DoProcessAsync(input);
        
        _logger.LogInformation("Successfully processed {DataType} with {DataId}", 
            input.GetType().Name, input.Id);
        
        _operationTracker.EndOperation(operationId, true);
        return Result.Success(result);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to process {DataType} with {DataId}", 
            input.GetType().Name, input.Id);
        
        _operationTracker.EndOperation(operationId, false, ex.Message);
        return Result.Failed(ex.Message);
    }
}

2. PERFORMANCE PATTERN:
------------------------
public async Task<T> WithPerformanceLogging<T>(string operationName, Func<Task<T>> operation)
{
    var stopwatch = Stopwatch.StartNew();
    
    try
    {
        var result = await operation();
        
        stopwatch.Stop();
        _logger.LogPerformanceMetric(operationName, stopwatch.Elapsed.TotalMilliseconds, "ms");
        
        return result;
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        _logger.LogError(ex, "Operation {OperationName} failed after {Duration}ms", 
            operationName, stopwatch.Elapsed.TotalMilliseconds);
        throw;
    }
}

3. RESOURCE PATTERN:
---------------------
public async Task<T> WithResourceLogging<T>(string resourceType, Func<Task<T>> operation)
{
    var resourceOperationId = _operationTracker.StartOperation($"Create{resourceType}");
    
    try
    {
        var result = await operation();
        
        _logger.LogInformation("Successfully created {ResourceType}", resourceType);
        
        _operationTracker.EndOperation(resourceOperationId, true);
        return result;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to create {ResourceType}", resourceType);
        _operationTracker.EndOperation(resourceOperationId, false, ex.Message);
        throw;
    }
}

TESTING WITH STRUCTURED LOGGING
--------------------------------

1. INJECTION TESTING:
----------------------
public class GraphicsServiceTests
{
    private readonly ITestOutputHelper _output;
    private readonly IGraphicsLogger _graphicsLogger;
    
    public GraphicsServiceTests(ITestOutputHelper output)
    {
        _output = output;
        
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddSerilog();
            builder.AddXUnit(_output);
        });
        
        _graphicsLogger = new GraphicsLogger(
            loggerFactory.CreateLogger<GraphicsLogger>(),
            new CorrelationIdProvider());
    }
    
    [Fact]
    public void DeviceInitialization_LogsCorrectly()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        
        // Act
        _graphicsLogger.LogDeviceInitialization("MockDevice", true, TimeSpan.FromMilliseconds(100));
        
        // Assert
        _output.Should().Contain("MockDevice");
        _output.Should().Contain("succeeded");
        _output.Should().Contain("100");
    }
}

2. MOCK LOGGING:
-----------------
public class MockGraphicsLogger : IGraphicsLogger
{
    public List<LogEntry> LogEntries { get; } = new();
    
    public void LogDeviceInitialization(string deviceType, bool success, TimeSpan duration)
    {
        LogEntries.Add(new LogEntry
        {
            Method = nameof(LogDeviceInitialization),
            Parameters = new { deviceType, success, duration = duration.TotalMilliseconds },
            Timestamp = DateTime.UtcNow
        });
    }
    
    // Implement other interface methods...
}

LOGGING BEST PRACTICES
-----------------------

1. STRUCTURED LOGGING:
   - Use properties instead of string concatenation
   - Include correlation IDs in all operations
   - Log at appropriate levels (Debug, Information, Warning, Error)

2. PERFORMANCE:
   - Use Stopwatch for timing operations
   - Log memory metrics regularly
   - Monitor GC performance

3. ERROR HANDLING:
   - Log exceptions with context
   - Include operation IDs for tracking
   - Provide actionable error information

4. SECURITY:
   - Never log sensitive information (passwords, tokens)
   - Use structured logging for user actions
   - Log security events appropriately

5. MONITORING:
   - Set up alerts for error rates
   - Monitor performance metrics
   - Track operation success rates

This framework provides a comprehensive, structured logging solution for TiXL applications,
enabling better observability, debugging, and performance monitoring across all modules.
*/