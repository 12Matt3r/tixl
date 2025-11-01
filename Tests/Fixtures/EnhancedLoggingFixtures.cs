// Enhanced Logging-Aware Test Fixtures for TiXL
// This file demonstrates the integration of logging with the testing framework

using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace TiXL.Tests.Fixtures
{
    /// <summary>
    /// Enhanced test fixture with comprehensive logging capabilities
    /// </summary>
    public abstract class TiXLFacts : IAsyncLifetime, IDisposable
    {
        protected IServiceProvider? ServiceProvider { get; private set; }
        protected IHost? Host { get; private set; }
        protected ILogger Logger { get; private set; } = null!;
        protected TestExecutionLogger ExecutionLogger { get; private set; } = null!;
        protected PerformanceAnalyzer PerformanceAnalyzer { get; private set; } = null!;
        protected FailureAnalyzer FailureAnalyzer { get; private set; } = null!;
        protected TestDebuggingUtilities DebuggingUtils { get; private set; } = null!;
        
        public virtual async Task InitializeAsync()
        {
            var startTime = Stopwatch.GetTimestamp();
            
            Host = CreateHostBuilder().Build();
            ServiceProvider = Host.Services;
            
            // Enhanced logging setup
            Logger = ServiceProvider!.GetRequiredService<ILoggerFactory>()
                .CreateLogger(GetType().Name);
                
            // Initialize test execution logging
            ExecutionLogger = ServiceProvider.GetRequiredService<TestExecutionLogger>();
            var currentMethod = new StackFrame(1)?.GetMethod();
            var className = currentMethod?.DeclaringType?.Name ?? "Unknown";
            ExecutionLogger.InitializeTest(className, GetType().Name);
            
            await SetupAsync();
            
            var setupDuration = (Stopwatch.GetTimestamp() - startTime) * 1000.0 / Stopwatch.Frequency;
            ExecutionLogger.LogPhase("Setup", setupDuration);
            ExecutionLogger.LogInformation($"Test fixture initialized in {setupDuration:F2}ms");
        }
        
        public virtual async Task DisposeAsync()
        {
            var cleanupStart = Stopwatch.GetTimestamp();
            
            await CleanupAsync();
            
            if (Host != null)
            {
                await Host.DisposeAsync();
            }
            
            var cleanupDuration = (Stopwatch.GetTimestamp() - cleanupStart) * 1000.0 / Stopwatch.Frequency;
            ExecutionLogger?.LogPhase("Cleanup", cleanupDuration);
            ExecutionLogger?.FinalizeTest();
            
            // Generate performance report
            PerformanceAnalyzer?.GenerateTestReport();
        }
        
        protected virtual IHostBuilder CreateHostBuilder()
        {
            return Host.CreateDefaultBuilder()
                .ConfigureServices(ConfigureServices);
        }
        
        protected virtual void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            // Enhanced logging configuration for tests
            services.AddLogging(builder =>
            {
                builder.AddConsole(options =>
                {
                    options.TimestampFormat = "HH:mm:ss.fff ";
                    options.IncludeScopes = true;
                });
                builder.AddDebug();
                builder.SetMinimumLevel(LogLevel.Debug);
                
                // Add file logging for test execution logs
                var logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "test-logs");
                Directory.CreateDirectory(logDirectory);
                
                builder.AddFile(Path.Combine(logDirectory, "test-execution-{Date}.log"), options =>
                {
                    options.AppendToExistingFile = true;
                    options.MinLevel = LogLevel.Information;
                    options.MaxSizeInMB = 100;
                });
            });
            
            // Register test-specific services
            services.AddSingleton<TestExecutionLogger>();
            services.AddSingleton<PerformanceAnalyzer>();
            services.AddSingleton<FailureAnalyzer>();
            services.AddSingleton<TestDebuggingUtilities>();
            services.AddSingleton<TestContextLogger>();
            services.AddSingleton<TestResultCorrelator>();
        }
        
        protected virtual Task SetupAsync() => Task.CompletedTask;
        protected virtual Task CleanupAsync() => Task.CompletedTask;
        
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
    
    /// <summary>
    /// Core test fixture with TiXL dependencies and logging
    /// </summary>
    public class CoreTestFixture : TiXLFacts
    {
        protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            base.ConfigureServices(context, services);
            
            // Register TiXL Core services
            services.AddSingleton<ITestCleanupService, TestCleanupService>();
            
            // Add performance counters for test monitoring
            services.AddSingleton<TestPerformanceCounter>();
        }
        
        protected override Task SetupAsync()
        {
            Logger.LogInformation("CoreTestFixture initialized with logging capabilities");
            
            // Log system information for debugging
            LogSystemInformation();
            
            return Task.CompletedTask;
        }
        
        private void LogSystemInformation()
        {
            var systemInfo = new
            {
                MachineName = Environment.MachineName,
                OSVersion = Environment.OSVersion.ToString(),
                ProcessorCount = Environment.ProcessorCount,
                WorkingSet = Environment.WorkingSet,
                DotNetVersion = Environment.Version.ToString(),
                Is64BitProcess = Environment.Is64BitProcess,
                Is64BitOperatingSystem = Environment.Is64BitOperatingSystem,
                CurrentDirectory = Directory.GetCurrentDirectory(),
                Timestamp = DateTime.UtcNow
            };
            
            Logger.LogInformation("SYSTEM_INFO {{Info}}", JsonSerializer.Serialize(systemInfo));
        }
    }
}

/// <summary>
/// Test execution logger for comprehensive test lifecycle tracking
/// </summary>
public class TestExecutionLogger
{
    private readonly ILogger<TestExecutionLogger> _logger;
    private readonly PerformanceAnalyzer _performanceAnalyzer;
    
    private TestExecutionContext? _currentContext;
    private readonly ConcurrentDictionary<string, List<TestPhase>> _testPhases = new();
    private readonly List<PerformanceMetric> _performanceMetrics = new();
    
    public TestExecutionLogger(ILogger<TestExecutionLogger> logger, PerformanceAnalyzer performanceAnalyzer)
    {
        _logger = logger;
        _performanceAnalyzer = performanceAnalyzer;
    }
    
    public void InitializeTest(string testClass, string testMethod)
    {
        _currentContext = new TestExecutionContext
        {
            TestClass = testClass,
            TestMethod = testMethod,
            StartTime = DateTime.UtcNow,
            ExecutionId = Guid.NewGuid().ToString("N")[..8]
        };
        
        _logger.LogInformation("TEST_EXEC_START {{ExecutionId}} {{TestClass}} {{TestMethod}} {{Timestamp}}",
            _currentContext.ExecutionId, testClass, testMethod, _currentContext.StartTime);
    }
    
    public void LogPhase(string phaseName, double durationMs, LogLevel level = LogLevel.Information)
    {
        if (_currentContext == null) return;
        
        var phase = new TestPhase
        {
            Name = phaseName,
            Duration = durationMs,
            Timestamp = DateTime.UtcNow
        };
        
        var key = $"{_currentContext.TestClass}.{_currentContext.TestMethod}";
        _testPhases.GetOrAdd(key, _ => new List<TestPhase>()).Add(phase);
        
        _logger.Log(level, "TEST_PHASE {{ExecutionId}} {{Phase}} {{Duration}}ms {{Timestamp}}",
            _currentContext.ExecutionId, phaseName, durationMs, phase.Timestamp);
    }
    
    public void LogInformation(string message, params object[] args)
    {
        Log(LogLevel.Information, message, args);
    }
    
    public void LogWarning(string message, params object[] args)
    {
        Log(LogLevel.Warning, message, args);
    }
    
    public void LogError(string message, params object[] args)
    {
        Log(LogLevel.Error, message, args);
    }
    
    public void LogError(Exception exception, string message, params object[] args)
    {
        if (_currentContext == null) return;
        
        var logMessage = $"TEST_ERROR {{ExecutionId}} {message}";
        var fullArgs = new object[] { _currentContext.ExecutionId }.Concat(args).ToArray();
        
        _logger.LogError(exception, logMessage, fullArgs);
        
        // Log structured error data
        _logger.LogError("TEST_ERROR_DETAILS {{ExecutionId}} {{ExceptionType}} {{ExceptionMessage}} {{StackTrace}} {{Timestamp}}",
            _currentContext.ExecutionId, exception.GetType().Name, exception.Message, 
            exception.StackTrace?.Replace("\n", "\\n"), DateTime.UtcNow);
    }
    
    public void LogPerformanceMetric(string metricName, double value, string unit, LogLevel level = LogLevel.Information)
    {
        if (_currentContext == null) return;
        
        _logger.Log(level, "TEST_PERF_METRIC {{ExecutionId}} {{MetricName}} {{Value}} {{Unit}} {{Timestamp}}",
            _currentContext.ExecutionId, metricName, value, unit, DateTime.UtcNow);
            
        _performanceAnalyzer.RecordMetric(_currentContext.ExecutionId, metricName, value, unit);
    }
    
    public void LogResourceUsage(string resourceType, long bytes, string operation)
    {
        if (_currentContext == null) return;
        
        _logger.LogInformation("TEST_RESOURCE {{ExecutionId}} {{ResourceType}} {{Bytes}} {{Operation}} {{Timestamp}}",
            _currentContext.ExecutionId, resourceType, bytes, operation, DateTime.UtcNow);
    }
    
    public void LogTestData(string dataType, object data)
    {
        if (_currentContext == null) return;
        
        var jsonData = JsonSerializer.Serialize(data);
        _logger.LogInformation("TEST_DATA {{ExecutionId}} {{DataType}} {{Data}} {{Timestamp}}",
            _currentContext.ExecutionId, dataType, jsonData, DateTime.UtcNow);
    }
    
    public void LogMemorySnapshot(string snapshotName)
    {
        if (_currentContext == null) return;
        
        var memoryBefore = GC.GetTotalMemory(false);
        var memoryAllocated = GC.GetTotalAllocatedBytes(true);
        
        _logger.LogInformation("TEST_MEMORY_SNAPSHOT {{ExecutionId}} {{SnapshotName}} {{TotalMemory}} {{AllocatedBytes}} {{Timestamp}}",
            _currentContext.ExecutionId, snapshotName, memoryBefore, memoryAllocated, DateTime.UtcNow);
    }
    
    public void FinalizeTest()
    {
        if (_currentContext == null) return;
        
        var totalDuration = (DateTime.UtcNow - _currentContext.StartTime).TotalMilliseconds;
        
        _logger.LogInformation("TEST_EXEC_END {{ExecutionId}} {{TotalDuration}}ms {{EndTime}}",
            _currentContext.ExecutionId, totalDuration, DateTime.UtcNow);
            
        // Generate performance summary
        GeneratePerformanceSummary();
    }
    
    public double GetElapsedTime()
    {
        return _currentContext == null ? 0 : (DateTime.UtcNow - _currentContext.StartTime).TotalMilliseconds;
    }
    
    private void Log(LogLevel level, string message, object[] args)
    {
        if (_currentContext == null)
        {
            _logger.Log(level, message, args);
            return;
        }
        
        var fullMessage = "{{@ExecutionId}} " + message;
        var fullArgs = new object[] { _currentContext.ExecutionId }.Concat(args).ToArray();
        
        _logger.Log(level, fullMessage, fullArgs);
    }
    
    private void GeneratePerformanceSummary()
    {
        if (_currentContext == null) return;
        
        var key = $"{_currentContext.TestClass}.{_currentContext.TestMethod}";
        if (_testPhases.TryGetValue(key, out var phases))
        {
            var totalDuration = phases.Sum(p => p.Duration);
            var slowestPhase = phases.OrderByDescending(p => p.Duration).FirstOrDefault();
            
            _logger.LogInformation("TEST_PERF_SUMMARY {{ExecutionId}} {{TotalDuration}}ms {{PhaseCount}} {{SlowestPhase}} {{SlowestDuration}}ms",
                _currentContext.ExecutionId, totalDuration, phases.Count, 
                slowestPhase?.Name, slowestPhase?.Duration);
        }
    }
}

/// <summary>
/// Performance analyzer for test performance tracking and analysis
/// </summary>
public class PerformanceAnalyzer
{
    private readonly ILogger<PerformanceAnalyzer> _logger;
    private readonly ConcurrentDictionary<string, List<PerformanceMetric>> _testMetrics = new();
    private readonly ConcurrentDictionary<string, BenchmarkTrend> _benchmarkTrends = new();
    
    public PerformanceAnalyzer(ILogger<PerformanceAnalyzer> logger)
    {
        _logger = logger;
    }
    
    public void RecordMetric(string executionId, string metricName, double value, string unit)
    {
        var metric = new PerformanceMetric
        {
            ExecutionId = executionId,
            MetricName = metricName,
            Value = value,
            Unit = unit,
            Timestamp = DateTime.UtcNow
        };
        
        var key = $"{executionId}.{metricName}";
        _testMetrics.GetOrAdd(key, _ => new List<PerformanceMetric>()).Add(metric);
        
        // Update benchmark trends
        UpdateBenchmarkTrend(metricName, value);
        
        _logger.LogDebug("PERF_METRIC_RECORDED {{ExecutionId}} {{MetricName}} {{Value}} {{Unit}}",
            executionId, metricName, value, unit);
    }
    
    public void AnalyzeTestPerformance(string executionId, double thresholdMs)
    {
        var keyPrefix = $"{executionId}.";
        var relevantMetrics = _testMetrics.Where(kvp => kvp.Key.StartsWith(keyPrefix))
            .SelectMany(kvp => kvp.Value).ToList();
            
        if (!relevantMetrics.Any()) return;
        
        var slowMetrics = relevantMetrics.Where(m => m.Value > thresholdMs).ToList();
        
        if (slowMetrics.Any())
        {
            _logger.LogWarning("PERF_THRESHOLD_EXCEEDED {{ExecutionId}} {{Threshold}}ms {{SlowMetrics}}",
                executionId, thresholdMs, JsonSerializer.Serialize(slowMetrics));
                
            // Generate performance report
            GeneratePerformanceReport(executionId, relevantMetrics, slowMetrics);
        }
    }
    
    public void GenerateTestReport()
    {
        var report = new TestPerformanceReport
        {
            GeneratedAt = DateTime.UtcNow,
            TotalTests = _testMetrics.Count,
            MetricsByTest = new Dictionary<string, List<PerformanceMetric>>()
        };
        
        foreach (var kvp in _testMetrics)
        {
            report.MetricsByTest[kvp.Key] = kvp.Value;
        }
        
        _logger.LogInformation("TEST_PERF_REPORT {{Tests}} {{Timestamp}}",
            report.TotalTests, report.GeneratedAt);
            
        // Log top 10 slowest operations
        var slowestOperations = _testMetrics
            .SelectMany(kvp => kvp.Value)
            .OrderByDescending(m => m.Value)
            .Take(10)
            .ToList();
            
        foreach (var operation in slowestOperations)
        {
            _logger.LogInformation("SLOW_OPERATION {{Operation}} {{Duration}}ms {{Unit}}",
                operation.MetricName, operation.Value, operation.Unit);
        }
    }
    
    private void UpdateBenchmarkTrend(string metricName, double value)
    {
        var trend = _benchmarkTrends.GetOrAdd(metricName, _ => new BenchmarkTrend { MetricName = metricName });
        trend.AddDataPoint(value);
        
        // Analyze trend for regressions
        if (trend.DataPoints.Count >= 10)
        {
            var regression = trend.AnalyzeForRegression();
            if (regression.IsRegressed)
            {
                _logger.LogWarning("PERFORMANCE_REGRESSION {{MetricName}} {{CurrentValue}} {{ExpectedRange}} {{TrendAnalysis}}",
                    metricName, value, regression.ExpectedRange, regression.Analysis);
            }
        }
    }
}

/// <summary>
/// Failure analyzer for comprehensive test failure analysis
/// </summary>
public class FailureAnalyzer
{
    private readonly ILogger<FailureAnalyzer> _logger;
    private readonly TestContextLogger _contextLogger;
    
    public FailureAnalyzer(ILogger<FailureAnalyzer> logger, TestContextLogger contextLogger)
    {
        _logger = logger;
        _contextLogger = contextLogger;
    }
    
    public void AnalyzeFailure(Exception exception, string testClass, string testMethod, Dictionary<string, object> testContext)
    {
        var failureId = Guid.NewGuid().ToString("N")[..8];
        
        _logger.LogError("TEST_FAILURE_START {{FailureId}} {{TestClass}} {{TestMethod}} {{ExceptionType}}",
            failureId, testClass, testMethod, exception.GetType().Name);
            
        // Analyze failure pattern
        var failurePattern = AnalyzeFailurePattern(exception, testClass, testMethod);
        
        // Log structured failure analysis
        _logger.LogError("FAILURE_ANALYSIS {{FailureId}} {{Pattern}} {{Category}} {{Severity}} {{Timestamp}}",
            failureId, failurePattern.Pattern, failurePattern.Category, failurePattern.Severity, DateTime.UtcNow);
            
        // Log context information
        LogFailureContext(failureId, testContext);
            
        // Log environment information
        LogEnvironmentInfo(failureId);
            
        // Suggest debugging actions
        SuggestDebuggingActions(failureId, failurePattern);
            
        _logger.LogError("TEST_FAILURE_END {{FailureId}} {{AnalysisComplete}}",
            failureId, DateTime.UtcNow);
    }
    
    private FailurePattern AnalyzeFailurePattern(Exception exception, string testClass, string testMethod)
    {
        var pattern = new FailurePattern
        {
            TestClass = testClass,
            TestMethod = testMethod,
            ExceptionType = exception.GetType().Name,
            ExceptionMessage = exception.Message,
            StackTrace = exception.StackTrace ?? "No stack trace",
            Timestamp = DateTime.UtcNow
        };
        
        // Classify failure pattern
        pattern.Pattern = ClassifyFailurePattern(exception, testClass, testMethod);
        pattern.Category = DetermineFailureCategory(exception);
        pattern.Severity = DetermineSeverity(exception);
        pattern.LikelyCauses = IdentifyLikelyCauses(exception, testClass, testMethod);
        
        return pattern;
    }
    
    private string ClassifyFailurePattern(Exception exception, string testClass, string testMethod)
    {
        if (exception is TimeoutException)
            return "TIMEOUT";
            
        if (exception is OutOfMemoryException)
            return "MEMORY";
            
        if (exception is InvalidOperationException)
            return "STATE_INVALID";
            
        if (exception is ArgumentException)
            return "INVALID_INPUT";
            
        if (exception is NotImplementedException)
            return "FEATURE_MISSING";
            
        if (exception.StackTrace?.Contains("Render") == true || testClass.Contains("Graphics"))
            return "GRAPHICS_RENDERING";
            
        return "GENERAL_ERROR";
    }
    
    private string DetermineFailureCategory(Exception exception)
    {
        return exception switch
        {
            TimeoutException => "PERFORMANCE",
            OutOfMemoryException => "RESOURCE",
            InvalidOperationException => "STATE",
            ArgumentException => "INPUT_VALIDATION",
            NotImplementedException => "IMPLEMENTATION",
            _ => "UNKNOWN"
        };
    }
    
    private string DetermineSeverity(Exception exception)
    {
        return exception switch
        {
            OutOfMemoryException => "CRITICAL",
            TimeoutException => "HIGH",
            InvalidOperationException => "MEDIUM",
            ArgumentException => "LOW",
            _ => "MEDIUM"
        };
    }
    
    private List<string> IdentifyLikelyCauses(Exception exception, string testClass, string testMethod)
    {
        var causes = new List<string>();
        
        if (exception.Message.Contains("null"))
            causes.Add("Null reference - check object initialization");
            
        if (exception.Message.Contains("timeout"))
            causes.Add("Operation took too long - check performance");
            
        if (exception.Message.Contains("memory"))
            causes.Add("Memory allocation failure - check resource cleanup");
            
        if (testClass.Contains("Graphics") && exception.StackTrace?.Contains("D3D12") == true)
            causes.Add("Graphics API issue - check Direct3D12 configuration");
            
        if (testMethod.Contains("Performance"))
            causes.Add("Performance regression - compare with baseline metrics");
            
        return causes;
    }
    
    private void LogFailureContext(string failureId, Dictionary<string, object> testContext)
    {
        _logger.LogError("FAILURE_CONTEXT {{FailureId}} {{Context}} {{Timestamp}}",
            failureId, JsonSerializer.Serialize(testContext), DateTime.UtcNow);
            
        // Log individual context values
        foreach (var kvp in testContext)
        {
            _logger.LogError("FAILURE_CONTEXT_ITEM {{FailureId}} {{Key}} {{Value}}",
                failureId, kvp.Key, kvp.Value);
        }
    }
    
    private void LogEnvironmentInfo(string failureId)
    {
        var environmentInfo = new
        {
            MachineName = Environment.MachineName,
            OSVersion = Environment.OSVersion.ToString(),
            ProcessorCount = Environment.ProcessorCount,
            WorkingSet = Environment.WorkingSet,
            DotNetVersion = Environment.Version.ToString(),
            Timestamp = DateTime.UtcNow
        };
        
        _logger.LogError("FAILURE_ENVIRONMENT {{FailureId}} {{Environment}}",
            failureId, JsonSerializer.Serialize(environmentInfo));
    }
    
    private void SuggestDebuggingActions(string failureId, FailurePattern pattern)
    {
        var suggestions = new List<string>();
        
        switch (pattern.Pattern)
        {
            case "TIMEOUT":
                suggestions.Add("Increase test timeout duration");
                suggestions.Add("Analyze recent performance metrics for degradation");
                suggestions.Add("Check for resource leaks causing slowdowns");
                break;
                
            case "MEMORY":
                suggestions.Add("Add memory profiling to test");
                suggestions.Add("Check for proper disposal of graphics resources");
                suggestions.Add("Analyze memory allocation patterns");
                break;
                
            case "GRAPHICS_RENDERING":
                suggestions.Add("Verify graphics device initialization");
                suggestions.Add("Check graphics resource creation and disposal");
                suggestions.Add("Validate shader compilation results");
                suggestions.Add("Test with different graphics configurations");
                break;
                
            case "STATE_INVALID":
                suggestions.Add("Review object lifecycle and state management");
                suggestions.Add("Check for race conditions in concurrent tests");
                suggestions.Add("Verify proper cleanup between test cases");
                break;
        }
        
        _logger.LogError("FAILURE_SUGGESTIONS {{FailureId}} {{Suggestions}} {{Timestamp}}",
            failureId, JsonSerializer.Serialize(suggestions), DateTime.UtcNow);
            
        foreach (var suggestion in suggestions)
        {
            _logger.LogError("FAILURE_SUGGESTION {{FailureId}} {{Suggestion}}",
                failureId, suggestion);
        }
    }
}

/// <summary>
/// Test debugging utilities for comprehensive failure analysis
/// </summary>
public class TestDebuggingUtilities
{
    private readonly ILogger<TestDebuggingUtilities> _logger;
    private readonly TestExecutionLogger _executionLogger;
    
    public TestDebuggingUtilities(ILogger<TestDebuggingUtilities> logger, TestExecutionLogger executionLogger)
    {
        _logger = logger;
        _executionLogger = executionLogger;
    }
    
    public async Task<string> CaptureSystemSnapshot(string testExecutionId)
    {
        var snapshotId = Guid.NewGuid().ToString("N")[..8];
        
        _logger.LogInformation("DEBUG_SNAPSHOT_START {{SnapshotId}} {{TestExecutionId}} {{Timestamp}}",
            snapshotId, testExecutionId, DateTime.UtcNow);
            
        var snapshot = new SystemSnapshot
        {
            SnapshotId = snapshotId,
            TestExecutionId = testExecutionId,
            Timestamp = DateTime.UtcNow,
            SystemInfo = CaptureSystemInfo(),
            MemoryInfo = CaptureMemoryInfo(),
            ThreadInfo = CaptureThreadInfo(),
            GraphicsInfo = CaptureGraphicsInfo(),
            FileSystemInfo = CaptureFileSystemInfo()
        };
        
        _logger.LogInformation("DEBUG_SNAPSHOT {{SnapshotId}} {{Snapshot}} {{Timestamp}}",
            snapshotId, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }), 
            DateTime.UtcNow);
            
        return snapshotId;
    }
    
    public void LogDetailedStackTrace(string testExecutionId, Exception exception)
    {
        _logger.LogError("DEBUG_STACKTRACE_START {{TestExecutionId}} {{ExceptionType}} {{Timestamp}}",
            testExecutionId, exception.GetType().Name, DateTime.UtcNow);
            
        // Capture stack trace with file and line information
        var stackTrace = new StackTrace(exception, true);
        var frames = stackTrace.GetFrames();
        
        if (frames != null)
        {
            for (int i = 0; i < frames.Length; i++)
            {
                var frame = frames[i];
                var method = frame.GetMethod();
                var fileName = frame.GetFileName();
                var lineNumber = frame.GetFileLineNumber();
                var columnNumber = frame.GetFileColumnNumber();
                
                _logger.LogError("DEBUG_STACKFRAME {{TestExecutionId}} {{FrameIndex}} {{MethodName}} {{FileName}} {{LineNumber}} {{ColumnNumber}} {{ILOffset}}",
                    testExecutionId, i, method?.Name, fileName, lineNumber, columnNumber, frame.GetILOffset());
            }
        }
        
        _logger.LogError("DEBUG_STACKTRACE_END {{TestExecutionId}} {{Timestamp}}",
            testExecutionId, DateTime.UtcNow);
    }
    
    public async Task LogGraphicsDiagnostics(string testExecutionId)
    {
        _logger.LogInformation("DEBUG_GRAPHICS_START {{TestExecutionId}} {{Timestamp}}",
            testExecutionId, DateTime.UtcNow);
            
        try
        {
            // Capture graphics diagnostics
            var graphicsInfo = new
            {
                TestExecutionId = testExecutionId,
                AdapterInfo = CaptureGraphicsAdapterInfo(),
                DeviceInfo = CaptureGraphicsDeviceInfo(),
                ResourceInfo = CaptureGraphicsResourceInfo(),
                D3D12Info = CaptureD3D12Info(),
                Timestamp = DateTime.UtcNow
            };
            
            _logger.LogInformation("DEBUG_GRAPHICS_INFO {{TestExecutionId}} {{GraphicsInfo}} {{Timestamp}}",
                testExecutionId, JsonSerializer.Serialize(graphicsInfo), DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DEBUG_GRAPHICS_ERROR {{TestExecutionId}} {{Timestamp}}",
                testExecutionId, DateTime.UtcNow);
        }
        
        _logger.LogInformation("DEBUG_GRAPHICS_END {{TestExecutionId}} {{Timestamp}}",
            testExecutionId, DateTime.UtcNow);
    }
    
    private SystemSnapshot.SystemInformation CaptureSystemInfo()
    {
        return new SystemSnapshot.SystemInformation
        {
            MachineName = Environment.MachineName,
            OSVersion = Environment.OSVersion.ToString(),
            ProcessorCount = Environment.ProcessorCount,
            WorkingSet = Environment.WorkingSet,
            DotNetVersion = Environment.Version.ToString(),
            UserDomainName = Environment.UserDomainName,
            UserName = Environment.UserName,
            TickCount = Environment.TickCount,
            Is64BitProcess = Environment.Is64BitProcess,
            Is64BitOperatingSystem = Environment.Is64BitOperatingSystem,
            SystemPageSize = Environment.SystemPageSize
        };
    }
    
    private SystemSnapshot.MemoryInformation CaptureMemoryInfo()
    {
        var process = Process.GetCurrentProcess();
        return new SystemSnapshot.MemoryInformation
        {
            WorkingSet = process.WorkingSet64,
            PrivateMemory = process.PrivateMemorySize64,
            VirtualMemory = process.VirtualMemorySize64,
            GCMemory = GC.GetTotalMemory(false),
            GCGen0Collections = GC.CollectionCount(0),
            GCGen1Collections = GC.CollectionCount(1),
            GCGen2Collections = GC.CollectionCount(2)
        };
    }
    
    private SystemSnapshot.ThreadInformation CaptureThreadInfo()
    {
        var threads = Process.GetCurrentProcess().Threads;
        return new SystemSnapshot.ThreadInformation
        {
            ThreadCount = threads.Count,
            ActiveThreads = threads.Cast<ProcessThread>().Count(t => t.ThreadState == ThreadState.Running)
        };
    }
    
    private SystemSnapshot.GraphicsInformation CaptureGraphicsInfo()
    {
        // Placeholder for graphics information
        return new SystemSnapshot.GraphicsInformation
        {
            AdapterCount = 1,
            PrimaryAdapterName = "Mock D3D12 Adapter",
            FeatureLevel = "12.0",
            DedicatedMemory = 1073741824 // 1GB
        };
    }
    
    private SystemSnapshot.GraphicsInformation CaptureGraphicsAdapterInfo() => CaptureGraphicsInfo();
    private SystemSnapshot.GraphicsInformation CaptureGraphicsDeviceInfo() => CaptureGraphicsInfo();
    private SystemSnapshot.GraphicsResourceInfo CaptureGraphicsResourceInfo() => new();
    private SystemSnapshot.D3D12Info CaptureD3D12Info() => new()
    {
        FactoryVersion = "12.0",
        DebugLayerEnabled = true,
        SupportedFeatureLevels = new[] { "12.0", "11.1", "11.0" }
    };
    
    private SystemSnapshot.FileSystemInfo CaptureFileSystemInfo()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var drives = DriveInfo.GetDrives();
        
        return new SystemSnapshot.FileSystemInfo
        {
            CurrentDirectory = currentDir,
            Drives = drives.Select(d => new
            {
                Name = d.Name,
                TotalSize = d.TotalSize,
                AvailableFreeSpace = d.AvailableFreeSpace,
                DriveType = d.DriveType.ToString()
            }).ToArray()
        };
    }
}

// Supporting data structures
public class TestExecutionContext
{
    public string ExecutionId { get; set; } = string.Empty;
    public string TestClass { get; set; } = string.Empty;
    public string TestMethod { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
}

public class TestPhase
{
    public string Name { get; set; } = string.Empty;
    public double Duration { get; set; }
    public DateTime Timestamp { get; set; }
}

public class PerformanceMetric
{
    public string ExecutionId { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class BenchmarkTrend
{
    public string MetricName { get; set; } = string.Empty;
    private readonly List<double> _dataPoints = new();
    
    public IReadOnlyList<double> DataPoints => _dataPoints.AsReadOnly();
    
    public void AddDataPoint(double value)
    {
        _dataPoints.Add(value);
        
        // Keep only last 100 data points to prevent memory growth
        if (_dataPoints.Count > 100)
        {
            _dataPoints.RemoveAt(0);
        }
    }
    
    public RegressionAnalysis AnalyzeForRegression()
    {
        if (_dataPoints.Count < 10)
        {
            return new RegressionAnalysis { IsRegressed = false, Analysis = "Insufficient data" };
        }
        
        var recent = _dataPoints.TakeLast(10).ToList();
        var baseline = _dataPoints.Take(10).ToList();
        
        var recentAvg = recent.Average();
        var baselineAvg = baseline.Average();
        var regressionThreshold = baselineAvg * 1.2; // 20% regression threshold
        
        return new RegressionAnalysis
        {
            IsRegressed = recentAvg > regressionThreshold,
            CurrentValue = recentAvg,
            ExpectedRange = $"{baselineAvg * 0.9:F2}-{baselineAvg * 1.1:F2}",
            Analysis = $"Recent avg: {recentAvg:F2}, Baseline avg: {baselineAvg:F2}"
        };
    }
}

public class RegressionAnalysis
{
    public bool IsRegressed { get; set; }
    public double CurrentValue { get; set; }
    public string ExpectedRange { get; set; } = string.Empty;
    public string Analysis { get; set; } = string.Empty;
}

public class TestPerformanceReport
{
    public DateTime GeneratedAt { get; set; }
    public int TotalTests { get; set; }
    public Dictionary<string, List<PerformanceMetric>> MetricsByTest { get; set; } = new();
}

public class FailurePattern
{
    public string TestClass { get; set; } = string.Empty;
    public string TestMethod { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string ExceptionType { get; set; } = string.Empty;
    public string ExceptionMessage { get; set; } = string.Empty;
    public string StackTrace { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public List<string> LikelyCauses { get; set; } = new();
}

public class SystemSnapshot
{
    public string SnapshotId { get; set; } = string.Empty;
    public string TestExecutionId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public SystemInformation? SystemInfo { get; set; }
    public MemoryInformation? MemoryInfo { get; set; }
    public ThreadInformation? ThreadInfo { get; set; }
    public GraphicsInformation? GraphicsInfo { get; set; }
    public FileSystemInfo? FileSystemInfo { get; set; }
    
    public class SystemInformation
    {
        public string MachineName { get; set; } = string.Empty;
        public string OSVersion { get; set; } = string.Empty;
        public int ProcessorCount { get; set; }
        public long WorkingSet { get; set; }
        public string DotNetVersion { get; set; } = string.Empty;
        public string UserDomainName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public int TickCount { get; set; }
        public bool Is64BitProcess { get; set; }
        public bool Is64BitOperatingSystem { get; set; }
        public int SystemPageSize { get; set; }
    }
    
    public class MemoryInformation
    {
        public long WorkingSet { get; set; }
        public long PrivateMemory { get; set; }
        public long VirtualMemory { get; set; }
        public long GCMemory { get; set; }
        public int GCGen0Collections { get; set; }
        public int GCGen1Collections { get; set; }
        public int GCGen2Collections { get; set; }
    }
    
    public class ThreadInformation
    {
        public int ThreadCount { get; set; }
        public int ActiveThreads { get; set; }
    }
    
    public class GraphicsInformation
    {
        public int AdapterCount { get; set; }
        public string PrimaryAdapterName { get; set; } = string.Empty;
        public string FeatureLevel { get; set; } = string.Empty;
        public long DedicatedMemory { get; set; }
    }
    
    public class GraphicsResourceInfo
    {
        public int TotalTextures { get; set; }
        public int TotalBuffers { get; set; }
        public long TotalMemory { get; set; }
    }
    
    public class D3D12Info
    {
        public string FactoryVersion { get; set; } = string.Empty;
        public bool DebugLayerEnabled { get; set; }
        public string[] SupportedFeatureLevels { get; set; } = Array.Empty<string>();
    }
    
    public class FileSystemInfo
    {
        public string CurrentDirectory { get; set; } = string.Empty;
        public object[] Drives { get; set; } = Array.Empty<object>();
    }
}

// Additional supporting interfaces and services
public interface ITestCleanupService
{
    void RegisterForCleanup(IDisposable disposable);
    void Cleanup();
}

public class TestCleanupService : ITestCleanupService
{
    private readonly List<IDisposable> _disposables = new();
    
    public void RegisterForCleanup(IDisposable disposable)
    {
        _disposables.Add(disposable);
    }
    
    public void Cleanup()
    {
        foreach (var disposable in _disposables)
        {
            try
            {
                disposable?.Dispose();
            }
            catch
            {
                // Ignore disposal errors in tests
            }
        }
        _disposables.Clear();
    }
}

public class TestContextLogger
{
    private readonly ILogger<TestContextLogger> _logger;
    
    public TestContextLogger(ILogger<TestContextLogger> logger)
    {
        _logger = logger;
    }
    
    public void LogContextChange(string testExecutionId, string contextName, object oldValue, object newValue)
    {
        _logger.LogInformation("TEST_CONTEXT_CHANGE {{ExecutionId}} {{ContextName}} {{OldValue}} {{NewValue}} {{Timestamp}}",
            testExecutionId, contextName, oldValue, newValue, DateTime.UtcNow);
    }
}

public class TestResultCorrelator
{
    private readonly ILogger<TestResultCorrelator> _logger;
    
    public TestResultCorrelator(ILogger<TestResultCorrelator> logger)
    {
        _logger = logger;
    }
    
    public void CorrelateTestResult(string testExecutionId, string applicationLogEntry)
    {
        _logger.LogInformation("TEST_RESULT_CORRELATION {{TestExecutionId}} {{ApplicationLogEntry}} {{Timestamp}}",
            testExecutionId, applicationLogEntry, DateTime.UtcNow);
    }
}

public class TestPerformanceCounter
{
    private readonly ILogger<TestPerformanceCounter> _logger;
    private readonly ConcurrentDictionary<string, PerformanceCounter> _counters = new();
    
    public TestPerformanceCounter(ILogger<TestPerformanceCounter> logger)
    {
        _logger = logger;
    }
    
    public void IncrementCounter(string counterName, string testExecutionId)
    {
        var counter = _counters.GetOrAdd(counterName, name => new PerformanceCounter(name, false));
        counter.Increment();
        
        _logger.LogDebug("TEST_COUNTER {{ExecutionId}} {{CounterName}} {{Value}}",
            testExecutionId, counterName, counter.NextValue());
    }
}