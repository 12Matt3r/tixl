// Debugging Utilities and Performance Analysis Implementation
// Provides comprehensive debugging and performance analysis capabilities for test failures

using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;
using System.Management;
using System.Runtime.InteropServices;

namespace TiXL.Tests.Utilities
{
    /// <summary>
    /// Comprehensive debugging utilities for test failure analysis
    /// </summary>
    public class TestDebuggingUtilities
    {
        private readonly ILogger<TestDebuggingUtilities> _logger;
        private readonly TestExecutionLogger _executionLogger;
        private readonly PerformanceAnalyzer _performanceAnalyzer;
        
        public TestDebuggingUtilities(
            ILogger<TestDebuggingUtilities> logger,
            TestExecutionLogger executionLogger,
            PerformanceAnalyzer performanceAnalyzer)
        {
            _logger = logger;
            _executionLogger = executionLogger;
            _performanceAnalyzer = performanceAnalyzer;
        }
        
        /// <summary>
        /// Captures a comprehensive system snapshot for debugging
        /// </summary>
        public async Task<string> CaptureSystemSnapshot(string testExecutionId)
        {
            var snapshotId = Guid.NewGuid().ToString("N")[..8];
            
            _logger.LogInformation("DEBUG_SNAPSHOT_START {{SnapshotId}} {{TestExecutionId}} {{Timestamp}}",
                snapshotId, testExecutionId, DateTime.UtcNow);
            
            try
            {
                var snapshot = new SystemSnapshot
                {
                    SnapshotId = snapshotId,
                    TestExecutionId = testExecutionId,
                    Timestamp = DateTime.UtcNow,
                    SystemInfo = await CaptureDetailedSystemInfo(),
                    MemoryInfo = CaptureDetailedMemoryInfo(),
                    ThreadInfo = CaptureDetailedThreadInfo(),
                    GraphicsInfo = CaptureDetailedGraphicsInfo(),
                    FileSystemInfo = CaptureDetailedFileSystemInfo(),
                    ProcessInfo = CaptureProcessInfo(),
                    EnvironmentInfo = CaptureEnvironmentInfo()
                };
                
                _logger.LogInformation("DEBUG_SNAPSHOT {{SnapshotId}} {{Snapshot}} {{Timestamp}}",
                    snapshotId, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions 
                    { 
                        WriteIndented = true,
                        DefaultIgnoreCondition = JsonIgnoreCondition.Never
                    }), DateTime.UtcNow);
                
                return snapshotId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DEBUG_SNAPSHOT_ERROR {{SnapshotId}} {{TestExecutionId}} {{Timestamp}}",
                    snapshotId, testExecutionId, DateTime.UtcNow);
                return snapshotId;
            }
        }
        
        /// <summary>
        /// Captures detailed stack trace information with source mapping
        /// </summary>
        public void LogDetailedStackTrace(string testExecutionId, Exception exception)
        {
            _logger.LogError("DEBUG_STACKTRACE_START {{TestExecutionId}} {{ExceptionType}} {{Timestamp}}",
                testExecutionId, exception.GetType().Name, DateTime.UtcNow);
            
            try
            {
                // Capture stack trace with full debugging information
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
                        var ilOffset = frame.GetILOffset();
                        
                        var methodInfo = method != null ? new
                        {
                            Name = method.Name,
                            DeclaringType = method.DeclaringType?.FullName,
                            Module = method.Module?.Name,
                            IsStatic = method.IsStatic,
                            IsPublic = method.IsPublic,
                            IsPrivate = method.IsPrivate
                        } : null;
                        
                        _logger.LogError("DEBUG_STACKFRAME {{TestExecutionId}} {{FrameIndex}} {{MethodInfo}} {{FileName}} {{LineNumber}} {{ColumnNumber}} {{ILOffset}} {{Timestamp}}",
                            testExecutionId, i, JsonSerializer.Serialize(methodInfo), fileName, 
                            lineNumber, columnNumber, ilOffset, DateTime.UtcNow);
                    }
                }
                else
                {
                    _logger.LogError("DEBUG_NO_STACKFRAMES {{TestExecutionId}} {{ExceptionType}} {{ExceptionMessage}} {{Timestamp}}",
                        testExecutionId, exception.GetType().Name, exception.Message, DateTime.UtcNow);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DEBUG_STACKTRACE_ERROR {{TestExecutionId}} {{Timestamp}}",
                    testExecutionId, DateTime.UtcNow);
            }
            
            _logger.LogError("DEBUG_STACKTRACE_END {{TestExecutionId}} {{Timestamp}}",
                testExecutionId, DateTime.UtcNow);
        }
        
        /// <summary>
        /// Captures comprehensive graphics diagnostics
        /// </summary>
        public async Task LogGraphicsDiagnostics(string testExecutionId)
        {
            _logger.LogInformation("DEBUG_GRAPHICS_START {{TestExecutionId}} {{Timestamp}}",
                testExecutionId, DateTime.UtcNow);
            
            try
            {
                var graphicsInfo = new GraphicsDiagnosticInfo
                {
                    TestExecutionId = testExecutionId,
                    Timestamp = DateTime.UtcNow,
                    AdapterInfo = await CaptureGraphicsAdapterInfo(),
                    DeviceInfo = CaptureGraphicsDeviceInfo(),
                    ResourceInfo = CaptureGraphicsResourceInfo(),
                    D3D12Info = CaptureD3D12Info(),
                    DirectXInfo = CaptureDirectXInfo(),
                    GpuMemoryInfo = CaptureGpuMemoryInfo(),
                    DriverInfo = CaptureGraphicsDriverInfo()
                };
                
                _logger.LogInformation("DEBUG_GRAPHICS_INFO {{TestExecutionId}} {{GraphicsInfo}} {{Timestamp}}",
                    testExecutionId, JsonSerializer.Serialize(graphicsInfo, new JsonSerializerOptions { WriteIndented = true }), DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DEBUG_GRAPHICS_ERROR {{TestExecutionId}} {{Timestamp}}",
                    testExecutionId, DateTime.UtcNow);
            }
            
            _logger.LogInformation("DEBUG_GRAPHICS_END {{TestExecutionId}} {{Timestamp}}",
                testExecutionId, DateTime.UtcNow);
        }
        
        /// <summary>
        /// Enables verbose logging for a specific duration
        /// </summary>
        public void EnableVerboseLogging(string testExecutionId, TimeSpan duration)
        {
            _logger.LogInformation("DEBUG_VERBOSE_START {{TestExecutionId}} {{Duration}} {{Timestamp}}",
                testExecutionId, duration, DateTime.UtcNow);
            
            // In a real implementation, this would configure logging providers
            // to increase verbosity for the specified duration
            // This is a placeholder demonstrating the concept
            
            // For now, we'll just log that verbose mode would be enabled
            _logger.LogDebug("DEBUG_VERBOSE_ENABLED {{TestExecutionId}} {{Duration}} {{Timestamp}}",
                testExecutionId, duration, DateTime.UtcNow);
            
            // Simulate verbose mode for demonstration
            Task.Delay(duration).ContinueWith(_ =>
            {
                _logger.LogInformation("DEBUG_VERBOSE_END {{TestExecutionId}} {{Timestamp}}",
                    testExecutionId, DateTime.UtcNow);
            });
        }
        
        /// <summary>
        /// Analyzes memory patterns for potential leaks
        /// </summary>
        public void AnalyzeMemoryPatterns(string testExecutionId)
        {
            _logger.LogInformation("DEBUG_MEMORY_ANALYSIS_START {{TestExecutionId}} {{Timestamp}}",
                testExecutionId, DateTime.UtcNow);
            
            try
            {
                var process = Process.GetCurrentProcess();
                var memoryAnalysis = new MemoryPatternAnalysis
                {
                    TestExecutionId = testExecutionId,
                    Timestamp = DateTime.UtcNow,
                    
                    // Current memory usage
                    WorkingSetMB = process.WorkingSet64 / (1024 * 1024),
                    PrivateMemoryMB = process.PrivateMemorySize64 / (1024 * 1024),
                    VirtualMemoryMB = process.VirtualMemorySize64 / (1024 * 1024),
                    
                    // Garbage collection info
                    TotalGCMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024),
                    Gen0Collections = GC.CollectionCount(0),
                    Gen1Collections = GC.CollectionCount(1),
                    Gen2Collections = GC.CollectionCount(2),
                    
                    // Process information
                    HandleCount = process.HandleCount,
                    ThreadCount = process.Threads.Count,
                    
                    // Analysis results
                    IsHighMemoryUsage = process.WorkingSet64 > 1024 * 1024 * 1024, // 1GB
                    IsMemoryLeakSuspected = process.WorkingSet64 > 512 * 1024 * 1024 && process.PrivateMemorySize64 > process.WorkingSet64 * 0.8, // 512MB with high private memory
                    RecommendedActions = GenerateMemoryRecommendations(process)
                };
                
                _logger.LogInformation("DEBUG_MEMORY_ANALYSIS {{TestExecutionId}} {{MemoryAnalysis}} {{Timestamp}}",
                    testExecutionId, JsonSerializer.Serialize(memoryAnalysis, new JsonSerializerOptions { WriteIndented = true }), DateTime.UtcNow);
                
                if (memoryAnalysis.IsHighMemoryUsage)
                {
                    _logger.LogWarning("HIGH_MEMORY_USAGE_DETECTED {{TestExecutionId}} {{WorkingSetMB}} {{RecommendedActions}}",
                        testExecutionId, memoryAnalysis.WorkingSetMB, string.Join(", ", memoryAnalysis.RecommendedActions));
                }
                
                if (memoryAnalysis.IsMemoryLeakSuspected)
                {
                    _logger.LogError("MEMORY_LEAK_SUSPECTED {{TestExecutionId}} {{WorkingSetMB}} {{PrivateMemoryMB}}",
                        testExecutionId, memoryAnalysis.WorkingSetMB, memoryAnalysis.PrivateMemoryMB);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DEBUG_MEMORY_ANALYSIS_ERROR {{TestExecutionId}} {{Timestamp}}",
                    testExecutionId, DateTime.UtcNow);
            }
            
            _logger.LogInformation("DEBUG_MEMORY_ANALYSIS_END {{TestExecutionId}} {{Timestamp}}",
                testExecutionId, DateTime.UtcNow);
        }
        
        /// <summary>
        /// Captures process and thread information for debugging
        /// </summary>
        public void CaptureProcessDiagnostics(string testExecutionId)
        {
            _logger.LogInformation("DEBUG_PROCESS_DIAGNOSTICS_START {{TestExecutionId}} {{Timestamp}}",
                testExecutionId, DateTime.UtcNow);
            
            try
            {
                var process = Process.GetCurrentProcess();
                var processInfo = new ProcessDiagnosticInfo
                {
                    TestExecutionId = testExecutionId,
                    Timestamp = DateTime.UtcNow,
                    
                    ProcessId = process.Id,
                    ProcessName = process.ProcessName,
                    StartTime = process.StartTime,
                    TotalProcessorTime = process.TotalProcessorTime.TotalMilliseconds,
                    UserProcessorTime = process.UserProcessorTime.TotalMilliseconds,
                    PrivilegedProcessorTime = process.PrivilegedProcessorTime.TotalMilliseconds,
                    
                    WorkingSetMB = process.WorkingSet64 / (1024 * 1024),
                    PagedMemorySizeMB = process.PagedMemorySize64 / (1024 * 1024),
                    PagedSystemMemorySizeMB = process.PagedSystemMemorySize64 / (1024 * 1024),
                    NonPagedSystemMemorySizeMB = process.NonPagedSystemMemorySize64 / (1024 * 1024),
                    
                    HandleCount = process.HandleCount,
                    ThreadCount = process.Threads.Count,
                    
                    Threads = process.Threads.Cast<ProcessThread>().Select(thread => new ThreadInfo
                    {
                        Id = thread.Id,
                        State = thread.ThreadState.ToString(),
                        Priority = thread.PriorityLevel.ToString(),
                        StartAddress = thread.StartAddress?.ToString(),
                        TotalProcessorTime = thread.TotalProcessorTime.TotalMilliseconds
                    }).ToList()
                };
                
                _logger.LogInformation("DEBUG_PROCESS_INFO {{TestExecutionId}} {{ProcessInfo}} {{Timestamp}}",
                    testExecutionId, JsonSerializer.Serialize(processInfo, new JsonSerializerOptions { WriteIndented = true }), DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DEBUG_PROCESS_DIAGNOSTICS_ERROR {{TestExecutionId}} {{Timestamp}}",
                    testExecutionId, DateTime.UtcNow);
            }
            
            _logger.LogInformation("DEBUG_PROCESS_DIAGNOSTICS_END {{TestExecutionId}} {{Timestamp}}",
                testExecutionId, DateTime.UtcNow);
        }
        
        // Private helper methods
        
        private async Task<SystemSnapshot.SystemInformation> CaptureDetailedSystemInfo()
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
                SystemPageSize = Environment.SystemPageSize,
                CommandLine = Environment.CommandLine,
                CurrentDirectory = Directory.GetCurrentDirectory()
            };
        }
        
        private SystemSnapshot.MemoryInformation CaptureDetailedMemoryInfo()
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
                GCGen2Collections = GC.CollectionCount(2),
                PagedMemorySize = process.PagedMemorySize64,
                PagedSystemMemorySize = process.PagedSystemMemorySize64,
                NonPagedSystemMemorySize = process.NonPagedSystemMemorySize64
            };
        }
        
        private SystemSnapshot.ThreadInformation CaptureDetailedThreadInfo()
        {
            var threads = Process.GetCurrentProcess().Threads;
            return new SystemSnapshot.ThreadInformation
            {
                ThreadCount = threads.Count,
                ActiveThreads = threads.Cast<ProcessThread>().Count(t => t.ThreadState == ThreadState.Running),
                TotalThreadTime = threads.Cast<ProcessThread>().Sum(t => t.TotalProcessorTime.TotalMilliseconds)
            };
        }
        
        private SystemSnapshot.GraphicsInformation CaptureDetailedGraphicsInfo()
        {
            return new SystemSnapshot.GraphicsInformation
            {
                AdapterCount = 1, // Would be populated by actual graphics enumeration
                PrimaryAdapterName = "Mock D3D12 Adapter",
                FeatureLevel = "12.0",
                DedicatedMemory = 1073741824 // 1GB
            };
        }
        
        private SystemSnapshot.FileSystemInfo CaptureDetailedFileSystemInfo()
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
                    DriveType = d.DriveType.ToString(),
                    IsReady = d.IsReady
                }).ToArray()
            };
        }
        
        private ProcessDiagnosticInfo CaptureProcessInfo()
        {
            var process = Process.GetCurrentProcess();
            return new ProcessDiagnosticInfo
            {
                ProcessId = process.Id,
                ProcessName = process.ProcessName,
                StartTime = process.StartTime,
                TotalProcessorTime = process.TotalProcessorTime.TotalMilliseconds,
                HandleCount = process.HandleCount,
                ThreadCount = process.Threads.Count
            };
        }
        
        private EnvironmentDiagnosticInfo CaptureEnvironmentInfo()
        {
            return new EnvironmentDiagnosticInfo
            {
                MachineName = Environment.MachineName,
                OSVersion = Environment.OSVersion.ToString(),
                ProcessorCount = Environment.ProcessorCount,
                WorkingSet = Environment.WorkingSet,
                DotNetVersion = Environment.Version.ToString(),
                UserDomainName = Environment.UserDomainName,
                UserName = Environment.UserName,
                Is64BitProcess = Environment.Is64BitProcess,
                Is64BitOperatingSystem = Environment.Is64BitOperatingSystem,
                SystemPageSize = Environment.SystemPageSize,
                CommandLine = Environment.CommandLine,
                CurrentDirectory = Directory.GetCurrentDirectory()
            };
        }
        
        private async Task<GraphicsDiagnosticInfo.AdapterInformation[]> CaptureGraphicsAdapterInfo()
        {
            // Placeholder for actual graphics adapter enumeration
            return new[]
            {
                new GraphicsDiagnosticInfo.AdapterInformation
                {
                    Name = "Mock D3D12 Adapter",
                    DeviceId = "MockDevice123",
                    VendorId = 0x1234,
                    Description = "Mock Graphics Adapter",
                    DedicatedSystemMemory = 1073741824, // 1GB
                    DedicatedVideoMemory = 2147483648, // 2GB
                    SharedSystemMemory = 4294967296, // 4GB
                    FeatureLevel = "12.0",
                    OutputCount = 1
                }
            };
        }
        
        private GraphicsDiagnosticInfo.DeviceInformation CaptureGraphicsDeviceInfo()
        {
            return new GraphicsDiagnosticInfo.DeviceInformation
            {
                DeviceId = "MockDevice123",
                Description = "Mock D3D12 Device",
                FeatureLevel = "12.0",
                NodeCount = 1,
                CommandQueueCount = 1,
                RootSignatureVersion = "1.1",
                TileMappingTier = "Tier2"
            };
        }
        
        private GraphicsDiagnosticInfo.ResourceInformation CaptureGraphicsResourceInfo()
        {
            return new GraphicsDiagnosticInfo.ResourceInformation
            {
                TotalTextures = 0,
                TotalBuffers = 0,
                TotalMemory = 0,
                LiveResources = 0,
                PendingDestruction = 0
            };
        }
        
        private GraphicsDiagnosticInfo.D3D12Information CaptureD3D12Info()
        {
            return new GraphicsDiagnosticInfo.D3D12Information
            {
                FactoryVersion = "12.0",
                DebugLayerEnabled = true,
                SupportedFeatureLevels = new[] { "12.0", "11.1", "11.0" },
                MinShaderModelVersion = "6.0",
                GraphicsQueueCount = 1,
                ComputeQueueCount = 1,
                CopyQueueCount = 1
            };
        }
        
        private GraphicsDiagnosticInfo.DirectXInformation CaptureDirectXInfo()
        {
            return new GraphicsDiagnosticInfo.DirectXInformation
            {
                Version = "12",
                RuntimeVersion = "12.0",
                DebugLayerAvailable = true,
                AgilitySDKVersion = "latest",
                SupportedFormats = new[] { "R8G8B8A8_UNorm", "B8G8R8A8_UNorm", "R32G32B32A32_Float" }
            };
        }
        
        private GraphicsDiagnosticInfo.GpuMemoryInformation CaptureGpuMemoryInfo()
        {
            return new GraphicsDiagnosticInfo.GpuMemoryInformation
            {
                TotalDedicatedMemory = 2147483648, // 2GB
                AvailableDedicatedMemory = 1073741824, // 1GB
                TotalSharedMemory = 4294967296, // 4GB
                AvailableSharedMemory = 2147483648, // 2GB
                MemoryUsage = 0.5, // 50% usage
                AllocationCount = 0
            };
        }
        
        private GraphicsDiagnosticInfo.DriverInformation CaptureGraphicsDriverInfo()
        {
            return new GraphicsDiagnosticInfo.DriverInformation
            {
                DriverVersion = "30.0.14.7111",
                DriverDate = "2023-11-15",
                VendorId = 0x1234,
                DeviceId = 0x1234,
                SubSystemId = 0x1234,
                RevisionId = 0x1234,
                WHQLLevel = "WHQL"
            };
        }
        
        private List<string> GenerateMemoryRecommendations(Process process)
        {
            var recommendations = new List<string>();
            
            if (process.WorkingSet64 > 1024 * 1024 * 1024) // 1GB
                recommendations.Add("High memory usage detected - investigate potential memory leaks");
            
            if (process.PrivateMemorySize64 > process.WorkingSet64 * 0.8)
                recommendations.Add("High private memory usage - check for unmanaged resources");
            
            if (GC.CollectionCount(2) > 10) // High Gen2 collections
                recommendations.Add("Frequent Gen2 collections - optimize object lifetime patterns");
            
            if (process.HandleCount > 10000)
                recommendations.Add("High handle count - check for resource leaks");
            
            return recommendations;
        }
    }
    
    /// <summary>
    /// Enhanced performance analyzer with trend analysis and regression detection
    /// </summary>
    public class PerformanceAnalyzer
    {
        private readonly ILogger<PerformanceAnalyzer> _logger;
        private readonly ConcurrentDictionary<string, List<PerformanceMetric>> _testMetrics = new();
        private readonly ConcurrentDictionary<string, BenchmarkTrend> _benchmarkTrends = new();
        private readonly List<TestPerformanceSnapshot> _performanceHistory = new();
        private readonly Timer _trendAnalysisTimer;
        
        public PerformanceAnalyzer(ILogger<PerformanceAnalyzer> logger)
        {
            _logger = logger;
            
            // Schedule trend analysis every 30 seconds
            _trendAnalysisTimer = new Timer(PerformScheduledTrendAnalysis, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
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
            
            // Record performance snapshot
            RecordPerformanceSnapshot(executionId, metricName, value);
            
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
                MetricsByTest = new Dictionary<string, List<PerformanceMetric>>(),
                PerformanceSummary = GeneratePerformanceSummary(),
                RegressionAnalysis = PerformRegressionAnalysis(),
                TrendAnalysis = GenerateTrendAnalysis()
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
            
            // Log performance summary
            _logger.LogInformation("PERFORMANCE_SUMMARY {{Summary}}",
                JsonSerializer.Serialize(report.PerformanceSummary, new JsonSerializerOptions { WriteIndented = true }));
        }
        
        public PerformanceSnapshot CreateSnapshot(string testName)
        {
            var process = Process.GetCurrentProcess();
            return new PerformanceSnapshot
            {
                TestName = testName,
                Timestamp = DateTime.UtcNow,
                
                // CPU metrics
                CpuUsage = process.TotalProcessorTime.TotalMilliseconds,
                UserCpuTime = process.UserProcessorTime.TotalMilliseconds,
                PrivilegedCpuTime = process.PrivilegedProcessorTime.TotalMilliseconds,
                
                // Memory metrics
                WorkingSetMB = process.WorkingSet64 / (1024 * 1024),
                PrivateMemoryMB = process.PrivateMemorySize64 / (1024 * 1024),
                VirtualMemoryMB = process.VirtualMemorySize64 / (1024 * 1024),
                GCMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024),
                
                // GC metrics
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2),
                
                // Process metrics
                HandleCount = process.HandleCount,
                ThreadCount = process.Threads.Count
            };
        }
        
        public void CompareSnapshots(PerformanceSnapshot before, PerformanceSnapshot after)
        {
            var comparison = new PerformanceSnapshotComparison
            {
                BeforeSnapshot = before,
                AfterSnapshot = after,
                Timestamp = DateTime.UtcNow,
                
                DeltaCpuUsage = after.CpuUsage - before.CpuUsage,
                DeltaWorkingSetMB = after.WorkingSetMB - before.WorkingSetMB,
                DeltaPrivateMemoryMB = after.PrivateMemoryMB - before.PrivateMemoryMB,
                DeltaVirtualMemoryMB = after.VirtualMemoryMB - before.VirtualMemoryMB,
                DeltaGCMemoryMB = after.GCMemoryMB - before.GCMemoryMB,
                DeltaHandleCount = after.HandleCount - before.HandleCount,
                DeltaThreadCount = after.ThreadCount - before.ThreadCount,
                
                IsMemoryLeakSuspected = after.WorkingSetMB - before.WorkingSetMB > 100, // 100MB increase
                IsCpuIntensive = after.CpuUsage - before.CpuUsage > 1000, // 1 second CPU time
                IsResourceLeakSuspected = after.HandleCount - before.HandleCount > 100 // Handle leak
            };
            
            _logger.LogInformation("PERF_SNAPSHOT_COMPARISON {{Comparison}}",
                JsonSerializer.Serialize(comparison, new JsonSerializerOptions { WriteIndented = true }));
            
            if (comparison.IsMemoryLeakSuspected)
            {
                _logger.LogWarning("MEMORY_LEAK_SUSPECTED {{DeltaWorkingSetMB}} {{DeltaPrivateMemoryMB}}",
                    comparison.DeltaWorkingSetMB, comparison.DeltaPrivateMemoryMB);
            }
            
            if (comparison.IsCpuIntensive)
            {
                _logger.LogWarning("CPU_INTENSIVE_OPERATION_DETECTED {{DeltaCpuUsage}}",
                    comparison.DeltaCpuUsage);
            }
            
            if (comparison.IsResourceLeakSuspected)
            {
                _logger.LogError("RESOURCE_LEAK_SUSPECTED {{DeltaHandleCount}}",
                    comparison.DeltaHandleCount);
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
        
        private void RecordPerformanceSnapshot(string executionId, string metricName, double value)
        {
            var snapshot = CreateSnapshot($"{executionId}.{metricName}");
            lock (_performanceHistory)
            {
                _performanceHistory.Add(new TestPerformanceSnapshot
                {
                    TestName = $"{executionId}.{metricName}",
                    Value = value,
                    Unit = "ms",
                    Timestamp = DateTime.UtcNow,
                    MemoryMB = snapshot.WorkingSetMB,
                    CpuUsage = snapshot.CpuUsage
                });
                
                // Keep only last 1000 snapshots
                if (_performanceHistory.Count > 1000)
                {
                    _performanceHistory.RemoveRange(0, _performanceHistory.Count - 1000);
                }
            }
        }
        
        private PerformanceSummary GeneratePerformanceSummary()
        {
            lock (_performanceHistory)
            {
                var recentSnapshots = _performanceHistory.OrderByDescending(s => s.Timestamp).Take(100).ToList();
                
                return new PerformanceSummary
                {
                    TotalSnapshots = _performanceHistory.Count,
                    RecentSnapshots = recentSnapshots.Count,
                    AverageMemoryMB = recentSnapshots.Average(s => s.MemoryMB),
                    AverageCpuUsage = recentSnapshots.Average(s => s.CpuUsage),
                    MaxMemoryMB = recentSnapshots.Max(s => s.MemoryMB),
                    MinMemoryMB = recentSnapshots.Min(s => s.MemoryMB),
                    IsMemoryTrendIncreasing = AnalyzeMemoryTrend(recentSnapshots),
                    IsCpuTrendIncreasing = AnalyzeCpuTrend(recentSnapshots)
                };
            }
        }
        
        private RegressionAnalysisResult PerformRegressionAnalysis()
        {
            var results = new List<MetricRegressionAnalysis>();
            
            foreach (var kvp in _testMetrics)
            {
                var metricName = kvp.Key;
                var metrics = kvp.Value.OrderBy(m => m.Timestamp).ToList();
                
                if (metrics.Count < 5) continue;
                
                var recentMetrics = metrics.TakeLast(5).ToList();
                var olderMetrics = metrics.Take(5).ToList();
                
                var recentAvg = recentMetrics.Average(m => m.Value);
                var olderAvg = olderMetrics.Average(m => m.Value);
                
                var regressionThreshold = olderAvg * 1.2; // 20% increase threshold
                var isRegressed = recentAvg > regressionThreshold;
                
                results.Add(new MetricRegressionAnalysis
                {
                    MetricName = metricName,
                    RecentAverage = recentAvg,
                    OlderAverage = olderAvg,
                    RegressionPercentage = ((recentAvg - olderAvg) / olderAvg) * 100,
                    IsRegressed = isRegressed,
                    DataPoints = metrics.Count
                });
            }
            
            return new RegressionAnalysisResult
            {
                AnalysisTimestamp = DateTime.UtcNow,
                TotalMetricsAnalyzed = results.Count,
                RegressedMetrics = results.Count(r => r.IsRegressed),
                Results = results.OrderByDescending(r => r.RegressionPercentage).ToList()
            };
        }
        
        private TrendAnalysisResult GenerateTrendAnalysis()
        {
            var trends = new List<IndividualTrendAnalysis>();
            
            foreach (var kvp in _benchmarkTrends)
            {
                var trend = kvp.Value;
                var analysis = trend.AnalyzeForRegression();
                
                trends.Add(new IndividualTrendAnalysis
                {
                    MetricName = kvp.Key,
                    DataPoints = trend.DataPoints.Count,
                    CurrentTrend = analysis.IsRegressed ? "Increasing" : "Stable",
                    TrendAnalysis = analysis.Analysis,
                    IsRegressed = analysis.IsRegressed
                });
            }
            
            return new TrendAnalysisResult
            {
                AnalysisTimestamp = DateTime.UtcNow,
                TotalTrendsAnalyzed = trends.Count,
                RegressedTrends = trends.Count(t => t.IsRegressed),
                StableTrends = trends.Count(t => !t.IsRegressed),
                Trends = trends
            };
        }
        
        private void PerformScheduledTrendAnalysis(object? state)
        {
            try
            {
                _logger.LogDebug("SCHEDULED_TREND_ANALYSIS_START {{Timestamp}}", DateTime.UtcNow);
                
                var regressionAnalysis = PerformRegressionAnalysis();
                var trendAnalysis = GenerateTrendAnalysis();
                
                if (regressionAnalysis.RegressedMetrics > 0)
                {
                    _logger.LogWarning("SCHEDULED_REGRESSION_ANALYSIS {{RegressedMetrics}} {{TotalMetrics}} {{Timestamp}}",
                        regressionAnalysis.RegressedMetrics, regressionAnalysis.TotalMetricsAnalyzed, DateTime.UtcNow);
                }
                
                if (trendAnalysis.RegressedTrends > 0)
                {
                    _logger.LogWarning("SCHEDULED_TREND_ANALYSIS {{RegressedTrends}} {{TotalTrends}} {{Timestamp}}",
                        trendAnalysis.RegressedTrends, trendAnalysis.TotalTrendsAnalyzed, DateTime.UtcNow);
                }
                
                _logger.LogDebug("SCHEDULED_TREND_ANALYSIS_END {{Timestamp}}", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SCHEDULED_TREND_ANALYSIS_ERROR {{Timestamp}}", DateTime.UtcNow);
            }
        }
        
        private bool AnalyzeMemoryTrend(List<TestPerformanceSnapshot> snapshots)
        {
            if (snapshots.Count < 5) return false;
            
            var firstHalf = snapshots.Take(snapshots.Count / 2).Average(s => s.MemoryMB);
            var secondHalf = snapshots.Skip(snapshots.Count / 2).Average(s => s.MemoryMB);
            
            return secondHalf > firstHalf * 1.1; // 10% increase threshold
        }
        
        private bool AnalyzeCpuTrend(List<TestPerformanceSnapshot> snapshots)
        {
            if (snapshots.Count < 5) return false;
            
            var firstHalf = snapshots.Take(snapshots.Count / 2).Average(s => s.CpuUsage);
            var secondHalf = snapshots.Skip(snapshots.Count / 2).Average(s => s.CpuUsage);
            
            return secondHalf > firstHalf * 1.1; // 10% increase threshold
        }
        
        public void Dispose()
        {
            _trendAnalysisTimer?.Dispose();
        }
    }
}

// Supporting data structures for debugging and performance analysis

public class GraphicsDiagnosticInfo
{
    public string TestExecutionId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public AdapterInformation[] AdapterInfo { get; set; } = Array.Empty<AdapterInformation>();
    public DeviceInformation DeviceInfo { get; set; } = new();
    public ResourceInformation ResourceInfo { get; set; } = new();
    public D3D12Information D3D12Info { get; set; } = new();
    public DirectXInformation DirectXInfo { get; set; } = new();
    public GpuMemoryInformation GpuMemoryInfo { get; set; } = new();
    public DriverInformation DriverInfo { get; set; } = new();
    
    public class AdapterInformation
    {
        public string Name { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public int VendorId { get; set; }
        public string Description { get; set; } = string.Empty;
        public long DedicatedSystemMemory { get; set; }
        public long DedicatedVideoMemory { get; set; }
        public long SharedSystemMemory { get; set; }
        public string FeatureLevel { get; set; } = string.Empty;
        public int OutputCount { get; set; }
    }
    
    public class DeviceInformation
    {
        public string DeviceId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string FeatureLevel { get; set; } = string.Empty;
        public int NodeCount { get; set; }
        public int CommandQueueCount { get; set; }
        public string RootSignatureVersion { get; set; } = string.Empty;
        public string TileMappingTier { get; set; } = string.Empty;
    }
    
    public class ResourceInformation
    {
        public int TotalTextures { get; set; }
        public int TotalBuffers { get; set; }
        public long TotalMemory { get; set; }
        public int LiveResources { get; set; }
        public int PendingDestruction { get; set; }
    }
    
    public class D3D12Information
    {
        public string FactoryVersion { get; set; } = string.Empty;
        public bool DebugLayerEnabled { get; set; }
        public string[] SupportedFeatureLevels { get; set; } = Array.Empty<string>();
        public string MinShaderModelVersion { get; set; } = string.Empty;
        public int GraphicsQueueCount { get; set; }
        public int ComputeQueueCount { get; set; }
        public int CopyQueueCount { get; set; }
    }
    
    public class DirectXInformation
    {
        public string Version { get; set; } = string.Empty;
        public string RuntimeVersion { get; set; } = string.Empty;
        public bool DebugLayerAvailable { get; set; }
        public string AgilitySDKVersion { get; set; } = string.Empty;
        public string[] SupportedFormats { get; set; } = Array.Empty<string>();
    }
    
    public class GpuMemoryInformation
    {
        public long TotalDedicatedMemory { get; set; }
        public long AvailableDedicatedMemory { get; set; }
        public long TotalSharedMemory { get; set; }
        public long AvailableSharedMemory { get; set; }
        public double MemoryUsage { get; set; }
        public int AllocationCount { get; set; }
    }
    
    public class DriverInformation
    {
        public string DriverVersion { get; set; } = string.Empty;
        public string DriverDate { get; set; } = string.Empty;
        public int VendorId { get; set; }
        public int DeviceId { get; set; }
        public int SubSystemId { get; set; }
        public int RevisionId { get; set; }
        public string WHQLLevel { get; set; } = string.Empty;
    }
}

public class MemoryPatternAnalysis
{
    public string TestExecutionId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public long WorkingSetMB { get; set; }
    public long PrivateMemoryMB { get; set; }
    public long VirtualMemoryMB { get; set; }
    public long TotalGCMemoryMB { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
    public int HandleCount { get; set; }
    public int ThreadCount { get; set; }
    public bool IsHighMemoryUsage { get; set; }
    public bool IsMemoryLeakSuspected { get; set; }
    public List<string> RecommendedActions { get; set; } = new();
}

public class ProcessDiagnosticInfo
{
    public string TestExecutionId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public double TotalProcessorTime { get; set; }
    public double UserProcessorTime { get; set; }
    public double PrivilegedProcessorTime { get; set; }
    public long WorkingSetMB { get; set; }
    public long PagedMemorySizeMB { get; set; }
    public long PagedSystemMemorySizeMB { get; set; }
    public long NonPagedSystemMemorySizeMB { get; set; }
    public int HandleCount { get; set; }
    public int ThreadCount { get; set; }
    public List<ThreadInfo> Threads { get; set; } = new();
}

public class EnvironmentDiagnosticInfo
{
    public string MachineName { get; set; } = string.Empty;
    public string OSVersion { get; set; } = string.Empty;
    public int ProcessorCount { get; set; }
    public long WorkingSet { get; set; }
    public string DotNetVersion { get; set; } = string.Empty;
    public string UserDomainName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public bool Is64BitProcess { get; set; }
    public bool Is64BitOperatingSystem { get; set; }
    public int SystemPageSize { get; set; }
    public string CommandLine { get; set; } = string.Empty;
    public string CurrentDirectory { get; set; } = string.Empty;
}

public class ThreadInfo
{
    public int Id { get; set; }
    public string State { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string StartAddress { get; set; } = string.Empty;
    public double TotalProcessorTime { get; set; }
}

public class PerformanceSnapshot
{
    public string TestName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public double CpuUsage { get; set; }
    public double UserCpuTime { get; set; }
    public double PrivilegedCpuTime { get; set; }
    public long WorkingSetMB { get; set; }
    public long PrivateMemoryMB { get; set; }
    public long VirtualMemoryMB { get; set; }
    public long GCMemoryMB { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
    public int HandleCount { get; set; }
    public int ThreadCount { get; set; }
}

public class PerformanceSnapshotComparison
{
    public PerformanceSnapshot BeforeSnapshot { get; set; } = new();
    public PerformanceSnapshot AfterSnapshot { get; set; } = new();
    public DateTime Timestamp { get; set; }
    public double DeltaCpuUsage { get; set; }
    public long DeltaWorkingSetMB { get; set; }
    public long DeltaPrivateMemoryMB { get; set; }
    public long DeltaVirtualMemoryMB { get; set; }
    public long DeltaGCMemoryMB { get; set; }
    public int DeltaHandleCount { get; set; }
    public int DeltaThreadCount { get; set; }
    public bool IsMemoryLeakSuspected { get; set; }
    public bool IsCpuIntensive { get; set; }
    public bool IsResourceLeakSuspected { get; set; }
}

public class TestPerformanceSnapshot
{
    public string TestName { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public long MemoryMB { get; set; }
    public double CpuUsage { get; set; }
}

public class PerformanceSummary
{
    public int TotalSnapshots { get; set; }
    public int RecentSnapshots { get; set; }
    public double AverageMemoryMB { get; set; }
    public double AverageCpuUsage { get; set; }
    public long MaxMemoryMB { get; set; }
    public long MinMemoryMB { get; set; }
    public bool IsMemoryTrendIncreasing { get; set; }
    public bool IsCpuTrendIncreasing { get; set; }
}

public class RegressionAnalysisResult
{
    public DateTime AnalysisTimestamp { get; set; }
    public int TotalMetricsAnalyzed { get; set; }
    public int RegressedMetrics { get; set; }
    public List<MetricRegressionAnalysis> Results { get; set; } = new();
}

public class MetricRegressionAnalysis
{
    public string MetricName { get; set; } = string.Empty;
    public double RecentAverage { get; set; }
    public double OlderAverage { get; set; }
    public double RegressionPercentage { get; set; }
    public bool IsRegressed { get; set; }
    public int DataPoints { get; set; }
}

public class TrendAnalysisResult
{
    public DateTime AnalysisTimestamp { get; set; }
    public int TotalTrendsAnalyzed { get; set; }
    public int RegressedTrends { get; set; }
    public int StableTrends { get; set; }
    public List<IndividualTrendAnalysis> Trends { get; set; } = new();
}

public class IndividualTrendAnalysis
{
    public string MetricName { get; set; } = string.Empty;
    public int DataPoints { get; set; }
    public string CurrentTrend { get; set; } = string.Empty;
    public string TrendAnalysis { get; set; } = string.Empty;
    public bool IsRegressed { get; set; }
}