// Module-Specific Logging Interfaces and Implementations
// Provides structured logging interfaces for each TiXL module

using Serilog;
using Serilog.Events;

namespace TiXL.Logging.Modules
{
    /// <summary>
    /// Core module logging interface
    /// </summary>
    public interface ICoreLogger
    {
        void LogSystemInitialization(string component, TimeSpan duration, LogLevel level = LogLevel.Information);
        void LogSystemShutdown(string component, string reason = "");
        void LogConfigurationChange(string key, object? oldValue, object? newValue);
        void LogMemoryMetrics(long workingSet, long privateMemory, int gen0Collections, int gen1Collections, int gen2Collections);
        void LogThreadingOperation(string operation, int threadId, LogLevel level = LogLevel.Information);
        void LogException(Exception exception, string context, LogLevel level = LogLevel.Error);
        void LogPerformanceMetric(string metricName, double value, string unit);
        void LogSecurityEvent(string eventType, string details, LogLevel level = LogLevel.Warning);
        void LogDependencyInitialization(string dependencyName, TimeSpan duration, bool success);
        void LogHealthCheck(string component, bool healthy, string? message = null);
    }

    /// <summary>
    /// Operators module logging interface
    /// </summary>
    public interface IOperatorsLogger
    {
        void LogOperatorCreation(string operatorType, string operatorName, LogLevel level = LogLevel.Information);
        void LogOperatorExecution(string operatorName, string operation, TimeSpan duration, LogLevel level = LogLevel.Information);
        void LogOperatorError(string operatorName, Exception exception, string context);
        void LogOperatorStateChange(string operatorName, string oldState, string newState);
        void LogInputValidation(string operatorName, bool isValid, string? validationMessage = null);
        void LogOutputGeneration(string operatorName, object? output, TimeSpan generationTime);
        void LogOperatorOptimization(string operatorName, string optimizationType, double improvement);
        void LogPipelineStage(string stageName, string operatorName, TimeSpan duration);
        void LogDependencyResolution(string operatorName, string dependencyName, bool resolved);
    }

    /// <summary>
    /// Editor module logging interface
    /// </summary>
    public interface IEditorLogger
    {
        void LogUserAction(string action, string details, string? userId = null);
        void LogUIInteraction(string uiComponent, string action, object? parameters = null);
        void LogDocumentOperation(string operation, string documentName, LogLevel level = LogLevel.Information);
        void LogEditorStateChange(string stateName, object? oldValue, object? newValue);
        void LogCommandExecution(string commandName, TimeSpan duration, bool success);
        void LogKeyboardShortcut(string shortcut, string command, string? userId = null);
        void LogUndoRedoOperation(string operation, int actionCount, string documentName);
        void LogEditorError(Exception exception, string context, LogLevel level = LogLevel.Error);
        void LogPerformanceIssue(string issue, double value, string threshold, LogLevel level = LogLevel.Warning);
        void LogPluginLoad(string pluginName, string version, bool success, TimeSpan? loadTime = null);
    }

    /// <summary>
    /// Graphics module logging interface
    /// </summary>
    public interface IGraphicsLogger
    {
        void LogDeviceInitialization(string deviceType, bool success, TimeSpan duration);
        void LogResourceCreation(string resourceType, long size, bool success, string? errorMessage = null);
        void LogShaderCompilation(string shaderType, bool success, TimeSpan? compilationTime = null, string? errors = null);
        void LogRenderingPass(string passName, int triangles, int drawCalls, TimeSpan duration);
        void LogGpuMemoryUsage(long usedMemory, long totalMemory, double usagePercent);
        void LogFrameRate(double fps, TimeSpan frameTime);
        void LogGraphicsError(Exception exception, string context, LogLevel level = LogLevel.Error);
        void LogPerformanceCounter(string counterName, double value, string unit);
        void LogGraphicsPipeline(string pipelineName, bool success, string? details = null);
        void LogTextureOperation(string operation, string format, Size dimensions, bool success);
    }

    /// <summary>
    /// Performance module logging interface
    /// </summary>
    public interface IPerformanceLogger
    {
        void LogBenchmarkStart(string benchmarkName, Dictionary<string, object>? parameters = null);
        void LogBenchmarkEnd(string benchmarkName, TimeSpan duration, Dictionary<string, object>? results = null);
        void LogPerformanceRegression(string metricName, double currentValue, double baselineValue, double threshold);
        void LogOptimizationOpportunity(string component, string issue, double impact);
        void LogMemoryPressure(long currentMemory, long threshold, LogLevel level = LogLevel.Warning);
        void LogCpuUsage(double percentage, int processCount, LogLevel level = LogLevel.Information);
        void LogGcMetrics(int gen0Collections, int gen1Collections, int gen2Collections, TimeSpan gcTime);
        void LogThreadPoolMetrics(int workerThreads, int completionPortThreads, LogLevel level = LogLevel.Information);
        void LogPerformanceAlert(string alertType, string message, LogLevel level = LogLevel.Warning);
        void LogScheduledOperation(string operationName, TimeSpan scheduledTime, TimeSpan actualTime);
    }

    /// <summary>
    /// Base module logger implementation
    /// </summary>
    public class ModuleLogger : IDisposable
    {
        protected readonly ILogger _logger;
        protected readonly string _moduleName;
        protected readonly ICorrelationIdProvider _correlationIdProvider;

        public ModuleLogger(ILogger logger, string moduleName, ICorrelationIdProvider correlationIdProvider)
        {
            _logger = logger;
            _moduleName = moduleName;
            _correlationIdProvider = correlationIdProvider;
        }

        protected void Log(LogEventLevel level, string messageTemplate, params object[] args)
        {
            var correlationId = _correlationIdProvider.GetCorrelationId();
            _logger.BindMessageTemplate(messageTemplate, args, out var template, out var properties);
            
            var logEvent = new LogEvent(
                DateTimeOffset.Now,
                level,
                null,
                template!,
                properties.ToList());
            
            logEvent.AddPropertyIfAbsent(new LogEventProperty("CorrelationId", new ScalarValue(correlationId)));
            logEvent.AddPropertyIfAbsent(new LogEventProperty("Module", new ScalarValue(_moduleName)));
            
            _logger.Write(logEvent);
        }

        protected void LogException(LogEventLevel level, Exception exception, string messageTemplate, params object[] args)
        {
            var correlationId = _correlationIdProvider.GetCorrelationId();
            _logger.BindMessageTemplate(messageTemplate, args, out var template, out var properties);
            
            var logEvent = new LogEvent(
                DateTimeOffset.Now,
                level,
                exception,
                template!,
                properties.ToList());
            
            logEvent.AddPropertyIfAbsent(new LogEventProperty("CorrelationId", new ScalarValue(correlationId)));
            logEvent.AddPropertyIfAbsent(new LogEventProperty("Module", new ScalarValue(_moduleName)));
            
            _logger.Write(logEvent);
        }

        public void Dispose() { }
    }

    /// <summary>
    /// Core module logger implementation
    /// </summary>
    public class CoreLogger : ModuleLogger, ICoreLogger
    {
        public CoreLogger(ILogger logger, ICorrelationIdProvider correlationIdProvider)
            : base(logger, "Core", correlationIdProvider)
        {
        }

        public void LogSystemInitialization(string component, TimeSpan duration, LogLevel level = LogLevel.Information)
        {
            Log(level.ToSerilogLevel(), "System initialization completed for {Component} in {Duration:F3}ms", 
                component, duration.TotalMilliseconds);
        }

        public void LogSystemShutdown(string component, string reason = "")
        {
            if (string.IsNullOrEmpty(reason))
            {
                Log(LogEventLevel.Information, "System shutdown initiated for {Component}", component);
            }
            else
            {
                Log(LogEventLevel.Information, "System shutdown initiated for {Component} due to {Reason}", 
                    component, reason);
            }
        }

        public void LogConfigurationChange(string key, object? oldValue, object? newValue)
        {
            Log(LogEventLevel.Information, "Configuration changed: {Key} from {OldValue} to {NewValue}", 
                key, oldValue, newValue);
        }

        public void LogMemoryMetrics(long workingSet, long privateMemory, int gen0Collections, int gen1Collections, int gen2Collections)
        {
            Log(LogEventLevel.Information, "Memory metrics: WorkingSet={WorkingSet}MB, PrivateMemory={PrivateMemory}MB, " +
                "GC(0:{Gen0},1:{Gen1},2:{Gen2})", 
                workingSet / 1024 / 1024, privateMemory / 1024 / 1024, 
                gen0Collections, gen1Collections, gen2Collections);
        }

        public void LogThreadingOperation(string operation, int threadId, LogLevel level = LogLevel.Information)
        {
            Log(level.ToSerilogLevel(), "Threading operation: {Operation} on thread {ThreadId}", 
                operation, threadId);
        }

        public void LogException(Exception exception, string context, LogLevel level = LogLevel.Error)
        {
            LogException(level.ToSerilogLevel(), exception, "Exception in {Context}", context);
        }

        public void LogPerformanceMetric(string metricName, double value, string unit)
        {
            Log(LogEventLevel.Information, "Performance metric: {MetricName}={Value}{Unit}", 
                metricName, value, unit);
        }

        public void LogSecurityEvent(string eventType, string details, LogLevel level = LogLevel.Warning)
        {
            Log(level.ToSerilogLevel(), "Security event: {EventType} - {Details}", eventType, details);
        }

        public void LogDependencyInitialization(string dependencyName, TimeSpan duration, bool success)
        {
            var level = success ? LogEventLevel.Information : LogEventLevel.Error;
            Log(level, "Dependency {DependencyName} initialization {Success} in {Duration:F3}ms", 
                dependencyName, success ? "succeeded" : "failed", duration.TotalMilliseconds);
        }

        public void LogHealthCheck(string component, bool healthy, string? message = null)
        {
            var level = healthy ? LogEventLevel.Information : LogEventLevel.Warning;
            if (string.IsNullOrEmpty(message))
            {
                Log(level, "Health check for {Component}: {Status}", component, healthy ? "Healthy" : "Unhealthy");
            }
            else
            {
                Log(level, "Health check for {Component}: {Status} - {Message}", 
                    component, healthy ? "Healthy" : "Unhealthy", message);
            }
        }
    }

    /// <summary>
    /// Operators module logger implementation
    /// </summary>
    public class OperatorsLogger : ModuleLogger, IOperatorsLogger
    {
        public OperatorsLogger(ILogger logger, ICorrelationIdProvider correlationIdProvider)
            : base(logger, "Operators", correlationIdProvider)
        {
        }

        public void LogOperatorCreation(string operatorType, string operatorName, LogLevel level = LogLevel.Information)
        {
            Log(level.ToSerilogLevel(), "Created {OperatorType} operator '{OperatorName}'", 
                operatorType, operatorName);
        }

        public void LogOperatorExecution(string operatorName, string operation, TimeSpan duration, LogLevel level = LogLevel.Information)
        {
            Log(level.ToSerilogLevel(), "Operator {OperatorName} executed {Operation} in {Duration:F3}ms", 
                operatorName, operation, duration.TotalMilliseconds);
        }

        public void LogOperatorError(string operatorName, Exception exception, string context)
        {
            LogException(LogEventLevel.Error, exception, "Operator {OperatorName} error in {Context}", 
                operatorName, context);
        }

        public void LogOperatorStateChange(string operatorName, string oldState, string newState)
        {
            Log(LogEventLevel.Information, "Operator {OperatorName} state changed from {OldState} to {NewState}", 
                operatorName, oldState, newState);
        }

        public void LogInputValidation(string operatorName, bool isValid, string? validationMessage = null)
        {
            if (isValid)
            {
                Log(LogEventLevel.Information, "Input validation passed for operator {OperatorName}", operatorName);
            }
            else
            {
                Log(LogEventLevel.Warning, "Input validation failed for operator {OperatorName}: {Message}", 
                    operatorName, validationMessage ?? "No details provided");
            }
        }

        public void LogOutputGeneration(string operatorName, object? output, TimeSpan generationTime)
        {
            Log(LogEventLevel.Information, "Operator {OperatorName} generated output in {Duration:F3}ms", 
                operatorName, generationTime.TotalMilliseconds);
        }

        public void LogOperatorOptimization(string operatorName, string optimizationType, double improvement)
        {
            Log(LogEventLevel.Information, "Operator {OperatorName} optimized with {OptimizationType}: {Improvement:P1} improvement", 
                operatorName, optimizationType, improvement / 100.0);
        }

        public void LogPipelineStage(string stageName, string operatorName, TimeSpan duration)
        {
            Log(LogEventLevel.Information, "Pipeline stage {StageName} completed with operator {OperatorName} in {Duration:F3}ms", 
                stageName, operatorName, duration.TotalMilliseconds);
        }

        public void LogDependencyResolution(string operatorName, string dependencyName, bool resolved)
        {
            var level = resolved ? LogEventLevel.Information : LogEventLevel.Warning;
            Log(level, "Dependency resolution for operator {OperatorName}: {DependencyName} {Status}", 
                operatorName, dependencyName, resolved ? "resolved" : "failed");
        }
    }

    /// <summary>
    /// Editor module logger implementation
    /// </summary>
    public class EditorLogger : ModuleLogger, IEditorLogger
    {
        public EditorLogger(ILogger logger, ICorrelationIdProvider correlationIdProvider)
            : base(logger, "Editor", correlationIdProvider)
        {
        }

        public void LogUserAction(string action, string details, string? userId = null)
        {
            Log(LogEventLevel.Information, "User action: {Action} - {Details} {UserId}", 
                action, details, !string.IsNullOrEmpty(userId) ? $"(User: {userId})" : "");
        }

        public void LogUIInteraction(string uiComponent, string action, object? parameters = null)
        {
            if (parameters == null)
            {
                Log(LogEventLevel.Information, "UI interaction: {UIComponent} - {Action}", uiComponent, action);
            }
            else
            {
                Log(LogEventLevel.Information, "UI interaction: {UIComponent} - {Action} with parameters {Parameters}", 
                    uiComponent, action, parameters);
            }
        }

        public void LogDocumentOperation(string operation, string documentName, LogLevel level = LogLevel.Information)
        {
            Log(level.ToSerilogLevel(), "Document operation: {Operation} on {DocumentName}", 
                operation, documentName);
        }

        public void LogEditorStateChange(string stateName, object? oldValue, object? newValue)
        {
            Log(LogEventLevel.Information, "Editor state change: {StateName} from {OldValue} to {NewValue}", 
                stateName, oldValue, newValue);
        }

        public void LogCommandExecution(string commandName, TimeSpan duration, bool success)
        {
            var level = success ? LogEventLevel.Information : LogEventLevel.Warning;
            Log(level, "Command execution: {CommandName} {Success} in {Duration:F3}ms", 
                commandName, success ? "succeeded" : "failed", duration.TotalMilliseconds);
        }

        public void LogKeyboardShortcut(string shortcut, string command, string? userId = null)
        {
            Log(LogEventLevel.Information, "Keyboard shortcut: {Shortcut} -> {Command} {UserId}", 
                shortcut, command, !string.IsNullOrEmpty(userId) ? $"(User: {userId})" : "");
        }

        public void LogUndoRedoOperation(string operation, int actionCount, string documentName)
        {
            Log(LogEventLevel.Information, "{Operation} operation on {DocumentName}: {ActionCount} actions", 
                operation, documentName, actionCount);
        }

        public void LogEditorError(Exception exception, string context, LogLevel level = LogLevel.Error)
        {
            LogException(level.ToSerilogLevel(), exception, "Editor error in {Context}", context);
        }

        public void LogPerformanceIssue(string issue, double value, string threshold, LogLevel level = LogLevel.Warning)
        {
            Log(level.ToSerilogLevel(), "Performance issue: {Issue} value {Value} exceeds threshold {Threshold}", 
                issue, value, threshold);
        }

        public void LogPluginLoad(string pluginName, string version, bool success, TimeSpan? loadTime = null)
        {
            var level = success ? LogEventLevel.Information : LogEventLevel.Error;
            if (loadTime.HasValue)
            {
                Log(level, "Plugin load: {PluginName} v{Version} {Success} in {Duration:F3}ms", 
                    pluginName, version, success ? "succeeded" : "failed", loadTime.Value.TotalMilliseconds);
            }
            else
            {
                Log(level, "Plugin load: {PluginName} v{Version} {Success}", 
                    pluginName, version, success ? "succeeded" : "failed");
            }
        }
    }

    /// <summary>
    /// Graphics module logger implementation
    /// </summary>
    public class GraphicsLogger : ModuleLogger, IGraphicsLogger
    {
        public GraphicsLogger(ILogger logger, ICorrelationIdProvider correlationIdProvider)
            : base(logger, "Graphics", correlationIdProvider)
        {
        }

        public void LogDeviceInitialization(string deviceType, bool success, TimeSpan duration)
        {
            var level = success ? LogEventLevel.Information : LogEventLevel.Error;
            Log(level, "Graphics device {DeviceType} initialization {Success} in {Duration:F3}ms", 
                deviceType, success ? "succeeded" : "failed", duration.TotalMilliseconds);
        }

        public void LogResourceCreation(string resourceType, long size, bool success, string? errorMessage = null)
        {
            var level = success ? LogEventLevel.Information : LogEventLevel.Error;
            if (success)
            {
                Log(level, "Graphics resource {ResourceType} created: {Size}MB", 
                    resourceType, size / 1024 / 1024);
            }
            else
            {
                Log(level, "Graphics resource {ResourceType} creation failed: {ErrorMessage}", 
                    resourceType, errorMessage ?? "Unknown error");
            }
        }

        public void LogShaderCompilation(string shaderType, bool success, TimeSpan? compilationTime = null, string? errors = null)
        {
            var level = success ? LogEventLevel.Information : LogEventLevel.Error;
            if (compilationTime.HasValue)
            {
                Log(level, "{ShaderType} shader compilation {Success} in {Duration:F3}ms", 
                    shaderType, success ? "succeeded" : "failed", compilationTime.Value.TotalMilliseconds);
            }
            else
            {
                Log(level, "{ShaderType} shader compilation {Success}", 
                    shaderType, success ? "succeeded" : "failed");
            }

            if (!success && !string.IsNullOrEmpty(errors))
            {
                Log(level, "{ShaderType} shader compilation errors: {Errors}", shaderType, errors);
            }
        }

        public void LogRenderingPass(string passName, int triangles, int drawCalls, TimeSpan duration)
        {
            Log(LogEventLevel.Information, "Rendering pass {PassName}: {Triangles} triangles, {DrawCalls} draw calls in {Duration:F3}ms", 
                passName, triangles, drawCalls, duration.TotalMilliseconds);
        }

        public void LogGpuMemoryUsage(long usedMemory, long totalMemory, double usagePercent)
        {
            Log(LogEventLevel.Information, "GPU memory usage: {UsedMemory}MB / {TotalMemory}MB ({UsagePercent:P1})", 
                usedMemory / 1024 / 1024, totalMemory / 1024 / 1024, usagePercent);
        }

        public void LogFrameRate(double fps, TimeSpan frameTime)
        {
            var level = fps < 30 ? LogEventLevel.Warning : LogEventLevel.Information;
            Log(level, "Frame rate: {FPS:F1} FPS ({FrameTime:F3} ms)", fps, frameTime.TotalMilliseconds);
        }

        public void LogGraphicsError(Exception exception, string context, LogLevel level = LogLevel.Error)
        {
            LogException(level.ToSerilogLevel(), exception, "Graphics error in {Context}", context);
        }

        public void LogPerformanceCounter(string counterName, double value, string unit)
        {
            Log(LogEventLevel.Information, "Graphics performance counter: {CounterName}={Value}{Unit}", 
                counterName, value, unit);
        }

        public void LogGraphicsPipeline(string pipelineName, bool success, string? details = null)
        {
            var level = success ? LogEventLevel.Information : LogEventLevel.Error;
            if (string.IsNullOrEmpty(details))
            {
                Log(level, "Graphics pipeline {PipelineName} {Success}", 
                    pipelineName, success ? "succeeded" : "failed");
            }
            else
            {
                Log(level, "Graphics pipeline {PipelineName} {Success}: {Details}", 
                    pipelineName, success ? "succeeded" : "failed", details);
            }
        }

        public void LogTextureOperation(string operation, string format, Size dimensions, bool success)
        {
            var level = success ? LogEventLevel.Information : LogEventLevel.Error;
            Log(level, "Texture {Operation}: {Width}x{Height} {Format} {Success}", 
                operation, dimensions.Width, dimensions.Height, format, success ? "succeeded" : "failed");
        }
    }

    /// <summary>
    /// Performance module logger implementation
    /// </summary>
    public class PerformanceLogger : ModuleLogger, IPerformanceLogger
    {
        public PerformanceLogger(ILogger logger, ICorrelationIdProvider correlationIdProvider)
            : base(logger, "Performance", correlationIdProvider)
        {
        }

        public void LogBenchmarkStart(string benchmarkName, Dictionary<string, object>? parameters = null)
        {
            if (parameters != null && parameters.Count > 0)
            {
                Log(LogEventLevel.Information, "Benchmark started: {BenchmarkName} with parameters {Parameters}", 
                    benchmarkName, parameters);
            }
            else
            {
                Log(LogEventLevel.Information, "Benchmark started: {BenchmarkName}", benchmarkName);
            }
        }

        public void LogBenchmarkEnd(string benchmarkName, TimeSpan duration, Dictionary<string, object>? results = null)
        {
            if (results != null && results.Count > 0)
            {
                Log(LogEventLevel.Information, "Benchmark completed: {BenchmarkName} in {Duration:F3}ms with results {Results}", 
                    benchmarkName, duration.TotalMilliseconds, results);
            }
            else
            {
                Log(LogEventLevel.Information, "Benchmark completed: {BenchmarkName} in {Duration:F3}ms", 
                    benchmarkName, duration.TotalMilliseconds);
            }
        }

        public void LogPerformanceRegression(string metricName, double currentValue, double baselineValue, double threshold)
        {
            var regressionPercentage = ((currentValue - baselineValue) / baselineValue) * 100;
            var level = regressionPercentage > threshold ? LogEventLevel.Warning : LogEventLevel.Information;
            
            Log(level, "Performance regression analysis: {MetricName} current={CurrentValue} baseline={BaselineValue} regression={RegressionPercentage:F1}%", 
                metricName, currentValue, baselineValue, regressionPercentage);
        }

        public void LogOptimizationOpportunity(string component, string issue, double impact)
        {
            Log(LogEventLevel.Information, "Optimization opportunity: {Component} - {Issue} (potential {Impact:P1} improvement)", 
                component, issue, impact / 100.0);
        }

        public void LogMemoryPressure(long currentMemory, long threshold, LogLevel level = LogLevel.Warning)
        {
            var usagePercent = (double)currentMemory / threshold * 100;
            Log(level.ToSerilogLevel(), "Memory pressure: {CurrentMemory}MB / {Threshold}MB ({UsagePercent:F1}%)", 
                currentMemory / 1024 / 1024, threshold / 1024 / 1024, usagePercent);
        }

        public void LogCpuUsage(double percentage, int processCount, LogLevel level = LogLevel.Information)
        {
            Log(level.ToSerilogLevel(), "CPU usage: {Percentage:F1}% across {ProcessCount} processes", 
                percentage, processCount);
        }

        public void LogGcMetrics(int gen0Collections, int gen1Collections, int gen2Collections, TimeSpan gcTime)
        {
            Log(LogEventLevel.Information, "GC metrics: Gen0={Gen0}, Gen1={Gen1}, Gen2={Gen2}, TotalTime={GcTime:F3}ms", 
                gen0Collections, gen1Collections, gen2Collections, gcTime.TotalMilliseconds);
        }

        public void LogThreadPoolMetrics(int workerThreads, int completionPortThreads, LogLevel level = LogLevel.Information)
        {
            Log(level.ToSerilogLevel(), "Thread pool metrics: Worker threads={WorkerThreads}, IO threads={IoThreads}", 
                workerThreads, completionPortThreads);
        }

        public void LogPerformanceAlert(string alertType, string message, LogLevel level = LogLevel.Warning)
        {
            Log(level.ToSerilogLevel(), "Performance alert [{AlertType}]: {Message}", alertType, message);
        }

        public void LogScheduledOperation(string operationName, TimeSpan scheduledTime, TimeSpan actualTime)
        {
            var deviation = (actualTime - scheduledTime).TotalMilliseconds;
            Log(LogEventLevel.Information, "Scheduled operation: {OperationName} scheduled for {ScheduledTime:F3}ms, actual {ActualTime:F3}ms (deviation: {Deviation:F3}ms)", 
                operationName, scheduledTime.TotalMilliseconds, actualTime.TotalMilliseconds, deviation);
        }
    }

    // Extension methods for LogLevel to Serilog LogEventLevel conversion
    public static class LogLevelExtensions
    {
        public static LogEventLevel ToSerilogLevel(this LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Critical => LogEventLevel.Fatal,
                LogLevel.Error => LogEventLevel.Error,
                LogLevel.Warning => LogEventLevel.Warning,
                LogLevel.Information => LogEventLevel.Information,
                LogLevel.Debug => LogEventLevel.Debug,
                LogLevel.Trace => LogEventLevel.Verbose,
                _ => LogEventLevel.Information
            };
        }
    }

    // Supporting data structures
    public struct Size
    {
        public int Width { get; set; }
        public int Height { get; set; }

        public Size(int width, int height)
        {
            Width = width;
            Height = height;
        }
    }
}