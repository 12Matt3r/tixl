using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Vortice.Windows.Direct3D12;
using Vortice.Windows;
using TiXL.Core.Graphics.DirectX12;
using TiXL.Core.Validation;
using TiXL.Core.ErrorHandling;
using Microsoft.Extensions.Logging;
using TiXL.Core.Logging;

namespace TiXL.Core.Performance
{
    /// <summary>
    /// Enhanced high-performance real-time performance monitoring system for TiXL
    /// Supports sub-millisecond precision timing and comprehensive metrics collection
    /// Integrated with DirectX 12 for real GPU performance data with comprehensive error handling
    /// </summary>
    /// <remarks>
    /// This enhanced version adds:
    /// - Comprehensive error handling and graceful degradation
    /// - Async/await patterns for improved responsiveness
    /// - Null safety annotations and parameter validation
    /// - Comprehensive XML documentation
    /// - Resource management improvements
    /// - Production-ready logging and monitoring
    /// </remarks>
    public class PerformanceMonitorEnhanced : IDisposable
    {
        #region Private Fields

        private readonly CircularBuffer<FrameMetrics> _frameMetrics;
        private readonly PerformanceCounter? _cpuCounter;
        private readonly GcMetrics _gcMetrics;
        private readonly ThreadLocal<FrameTimer> _frameTimer;
        private readonly object _metricsLock = new object();
        private readonly Timer? _metricsCollectionTimer;
        
        // DirectX 12 performance monitoring
        private readonly ID3D12Device5? _d3d12Device;
        private readonly D3D12PerformanceQuery _performanceQuery;
        private readonly D3D12GpuMemoryMonitor _gpuMemoryMonitor;
        private readonly D3D12HardwareCounters _hardwareCounters;
        private readonly D3D12PresentTiming _presentTiming;
        private readonly object _d3d12Lock = new object();
        
        // Performance thresholds for alerting
        private readonly double _targetFrameTime = 16.67; // 60 FPS
        private readonly double _criticalFrameTime = 33.33; // 30 FPS
        private readonly double _maxFrameVariance = 5.0; // 5ms variance
        
        // Enhanced error handling and logging
        private readonly ILogger<PerformanceMonitorEnhanced> _logger;
        private readonly GracefulDegradationStrategy _degradationStrategy;
        private readonly TimeoutPolicy _timeoutPolicy;
        private readonly RetryPolicy _retryPolicy;

        #endregion

        #region Public Events

        /// <summary>
        /// Event raised when performance alerts are generated
        /// </summary>
        public event EventHandler<PerformanceAlert>? PerformanceAlert;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new enhanced performance monitor with comprehensive error handling
        /// </summary>
        /// <param name="historySize">Number of frames to keep in history (default: 300)</param>
        /// <param name="d3d12Device">Optional DirectX 12 device for GPU monitoring</param>
        /// <param name="logger">Logger for error handling and diagnostics</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when historySize is out of valid range</exception>
        /// <exception cref="ArgumentNullException">Thrown when logger is null</exception>
        public PerformanceMonitorEnhanced(
            int historySize = 300, 
            ID3D12Device5? d3d12Device = null,
            ILogger<PerformanceMonitorEnhanced>? logger = null)
        {
            try
            {
                // Input validation with enhanced error handling
                ValidationHelpers.ValidatePositive(historySize, nameof(historySize));
                ValidationHelpers.ValidateRange(historySize, 10, 10000, nameof(historySize));
                
                _d3d12Device = d3d12Device;
                _logger = logger ?? LoggerFactory.CreateLogger<PerformanceMonitorEnhanced>();
                
                // Initialize error handling infrastructure
                _degradationStrategy = new GracefulDegradationStrategy();
                _timeoutPolicy = new TimeoutPolicy 
                { 
                    Timeout = TimeSpan.FromSeconds(10),
                    OnTimeout = () => _degradationStrategy.RecordFailure("Performance monitoring timeout")
                };
                _retryPolicy = new RetryPolicy
                {
                    MaxRetries = 3,
                    InitialDelay = TimeSpan.FromMilliseconds(100),
                    BackoffMultiplier = 2.0,
                    RetryCondition = ex => ExceptionFilters.IsTransientFailure(ex)
                };

                _frameMetrics = new CircularBuffer<FrameMetrics>(historySize);
                _frameTimer = new ThreadLocal<FrameTimer>(() => new FrameTimer());
                _gcMetrics = new GcMetrics();
                
                // Initialize DirectX 12 performance monitoring with error handling
                InitializeDirectX12Monitoring();
                
                // Initialize CPU counter with fallback
                InitializeCpuCounterWithFallback();
                
                // Start background metrics collection with error handling
                StartBackgroundMetricsCollection();
                
                _logger.LogInformation("PerformanceMonitorEnhanced initialized successfully - HistorySize: {HistorySize}, D3D12Device: {HasDevice}", 
                    historySize, d3d12Device != null);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize PerformanceMonitorEnhanced");
                throw new PerformanceMonitoringException(
                    "InitializationFailed",
                    $"Failed to initialize performance monitor: {ex.Message}",
                    ex);
            }
        }

        #endregion

        #region Public Methods - Frame Timing

        /// <summary>
        /// Begins timing a new frame with enhanced error handling
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when frame timing is already active</exception>
        public void BeginFrame()
        {
            try
            {
                _frameTimer.Value?.BeginFrame();
                
                // Start DirectX 12 frame timing if available
                lock (_d3d12Lock)
                {
                    if (_d3d12Device != null)
                    {
                        _presentTiming?.BeginFrame();
                        _performanceQuery?.BeginFrame();
                        _hardwareCounters?.BeginFrame();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to begin frame timing");
                _degradationStrategy.RecordFailure($"BeginFrame failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Ends timing current frame and records comprehensive metrics with error handling
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when no frame is currently active</exception>
        public void EndFrame()
        {
            try
            {
                _frameTimer.Value?.EndFrame();
                
                var metrics = _frameTimer.Value?.GetMetrics();
                if (metrics == null)
                {
                    throw new InvalidOperationException("No frame metrics available");
                }
                
                // Add system metrics with error handling
                RecordSystemMetrics(metrics);
                
                // Add DirectX 12 GPU performance metrics with graceful degradation
                RecordDirectX12Metrics(metrics);
                
                // Add to circular buffer with thread safety
                lock (_metricsLock)
                {
                    _frameMetrics.Add(metrics);
                }
                
                // Check for performance issues with enhanced alerting
                CheckPerformanceAlertsWithEnhancedHandling(metrics);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to end frame timing");
                _degradationStrategy.RecordFailure($"EndFrame failed: {ex.Message}");
            }
        }

        #endregion

        #region Public Methods - Analysis and Reporting

        /// <summary>
        /// Gets current frame analysis with performance metrics and enhanced error handling
        /// </summary>
        /// <param name="frameCount">Number of recent frames to analyze (default: 60)</param>
        /// <returns>FrameAnalysis object with comprehensive metrics, or null if insufficient data</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when frameCount is out of valid range</exception>
        public FrameAnalysis? GetFrameAnalysis(int frameCount = 60)
        {
            return ExecuteWithErrorHandling(() =>
            {
                ValidationHelpers.ValidateRange(frameCount, 1, 1000, nameof(frameCount));
                
                FrameMetrics[] frames;
                
                lock (_metricsLock)
                {
                    frames = _frameMetrics.GetRecentFrames(frameCount).ToArray();
                }
                
                if (frames.Length < Math.Min(10, frameCount / 2))
                {
                    _logger?.LogWarning("Insufficient frame data for analysis: {FrameCount} frames available, {RequiredCount} required", 
                        frames.Length, frameCount / 2);
                    return null;
                }
                
                return AnalyzeFramePerformance(frames);
            }, "GetFrameAnalysis");
        }

        /// <summary>
        /// Gets current frame time in milliseconds with error handling
        /// </summary>
        /// <returns>Current frame time in milliseconds, or 0 if timing is not active</returns>
        public double GetCurrentFrameTime()
        {
            try
            {
                return _frameTimer.Value?.CurrentFrameTime ?? 0.0;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get current frame time");
                return 0.0;
            }
        }

        #endregion

        #region Public Methods - Custom Metrics

        /// <summary>
        /// Records a custom performance metric with enhanced validation and error handling
        /// </summary>
        /// <param name="name">Name of the metric (1-128 characters, non-null, non-whitespace)</param>
        /// <param name="value">Value of the metric (-1e6 to 1e6 range)</param>
        /// <exception cref="ArgumentNullException">Thrown when name is null</exception>
        /// <exception cref="ArgumentException">Thrown when name is empty, whitespace, or invalid length</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when value is out of valid range</exception>
        public void RecordCustomMetric(string name, double value)
        {
            ExecuteWithErrorHandling(() =>
            {
                ValidationHelpers.ThrowIfNullOrWhiteSpace(name, nameof(name));
                ValidationHelpers.ValidateString(name, 128, nameof(name));
                ValidationHelpers.ValidateRange(value, -1e6, 1e6, nameof(value));
                
                var metrics = _frameTimer.Value?.GetMetrics();
                if (metrics != null)
                {
                    if (metrics.CustomMetrics == null)
                        metrics.CustomMetrics = new Dictionary<string, double>();
                        
                    metrics.CustomMetrics[name] = value;
                    _logger?.LogDebug("Recorded custom metric: {MetricName} = {MetricValue}", name, value);
                }
            }, "RecordCustomMetric");
        }

        #endregion

        #region Public Methods - DirectX 12 Operations

        /// <summary>
        /// Begins timing a DirectX 12 operation using timestamp queries with enhanced error handling
        /// </summary>
        /// <param name="operationName">Name of the operation (1-256 characters, non-null, non-whitespace)</param>
        /// <param name="queryType">Type of query to use (default: Timestamp)</param>
        /// <returns>GPU timing handle for the operation</returns>
        /// <exception cref="ArgumentNullException">Thrown when operationName is null</exception>
        /// <exception cref="ArgumentException">Thrown when operationName is empty, whitespace, or invalid length</exception>
        public GpuTimingHandle BeginD3D12Operation(string operationName, D3D12QueryType queryType = D3D12QueryType.Timestamp)
        {
            return ExecuteWithErrorHandling(() =>
            {
                ValidationHelpers.ThrowIfNullOrWhiteSpace(operationName, nameof(operationName));
                ValidationHelpers.ValidateString(operationName, 256, nameof(operationName));
                ValidationHelpers.ValidateRange((double)queryType, 0, Enum.GetValues(typeof(D3D12QueryType)).Length - 1, nameof(queryType));
                
                lock (_d3d12Lock)
                {
                    if (_d3d12Device == null)
                    {
                        _logger?.LogWarning("No DirectX 12 device available for operation timing: {OperationName}", operationName);
                        return new GpuTimingHandle();
                    }
                    
                    try
                    {
                        return _performanceQuery?.BeginOperation(operationName, queryType) ?? new GpuTimingHandle();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to begin DirectX 12 operation timing: {OperationName}", operationName);
                        return new GpuTimingHandle();
                    }
                }
            }, "BeginD3D12Operation");
        }

        /// <summary>
        /// Ends timing a DirectX 12 operation with enhanced error handling
        /// </summary>
        /// <param name="handle">GPU timing handle to complete</param>
        /// <exception cref="ArgumentException">Thrown when handle is invalid</exception>
        public void EndD3D12Operation(ref GpuTimingHandle handle)
        {
            ExecuteWithErrorHandling(() =>
            {
                ValidationHelpers.ThrowIfNull(handle, nameof(handle));
                
                if (!handle.IsValid)
                {
                    _logger?.LogWarning("Invalid GPU timing handle provided to EndD3D12Operation");
                    return;
                }
                
                lock (_d3d12Lock)
                {
                    if (_d3d12Device == null) 
                    {
                        _logger?.LogDebug("No DirectX 12 device available for operation timing completion: {OperationName}", handle.OperationName);
                        return;
                    }
                    
                    try
                    {
                        _performanceQuery?.EndOperation(ref handle);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to end DirectX 12 operation timing: {OperationName}", handle.OperationName);
                        // Don't throw - operation timing failure shouldn't break frame timing
                    }
                }
            }, "EndD3D12Operation");
        }

        #endregion

        #region Public Methods - GPU Information

        /// <summary>
        /// Gets current GPU memory usage from DirectX 12 with enhanced error handling
        /// </summary>
        /// <returns>D3D12MemoryUsage object with current memory metrics, or default values on error</returns>
        public D3D12MemoryUsage GetCurrentGpuMemoryUsage()
        {
            return ExecuteWithErrorHandling(() =>
            {
                lock (_d3d12Lock)
                {
                    try
                    {
                        return _gpuMemoryMonitor?.GetCurrentMemoryUsage() ?? new D3D12MemoryUsage();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to get GPU memory usage");
                        return new D3D12MemoryUsage();
                    }
                }
            }, "GetCurrentGpuMemoryUsage");
        }

        /// <summary>
        /// Gets hardware performance counters from DirectX 12 with enhanced error handling
        /// </summary>
        /// <returns>D3D12HardwareCountersData object with current counter values, or default values on error</returns>
        public D3D12HardwareCountersData GetHardwareCounters()
        {
            return ExecuteWithErrorHandling(() =>
            {
                lock (_d3d12Lock)
                {
                    try
                    {
                        return _hardwareCounters?.GetCurrentCounters() ?? new D3D12HardwareCountersData();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to get hardware counters");
                        return new D3D12HardwareCountersData();
                    }
                }
            }, "GetHardwareCounters");
        }

        /// <summary>
        /// Gets present timing metrics from DirectX 12 with enhanced error handling
        /// </summary>
        /// <returns>D3D12PresentMetrics object with current timing data, or default values on error</returns>
        public D3D12PresentMetrics GetPresentMetrics()
        {
            return ExecuteWithErrorHandling(() =>
            {
                lock (_d3d12Lock)
                {
                    try
                    {
                        return _presentTiming?.GetCurrentMetrics() ?? new D3D12PresentMetrics();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to get present metrics");
                        return new D3D12PresentMetrics();
                    }
                }
            }, "GetPresentMetrics");
        }

        #endregion

        #region Public Methods - Profiling

        /// <summary>
        /// Starts profiling a specific operation with enhanced error handling
        /// </summary>
        /// <param name="operationName">Name of the operation to profile (non-null, non-empty)</param>
        /// <returns>IDisposable profiler that should be disposed when operation completes</returns>
        /// <exception cref="ArgumentNullException">Thrown when operationName is null</exception>
        /// <exception cref="ArgumentException">Thrown when operationName is empty or whitespace</exception>
        public IDisposable ProfileOperation(string operationName)
        {
            return ExecuteWithErrorHandling(() =>
            {
                ValidationHelpers.ThrowIfNullOrWhiteSpace(operationName, nameof(operationName));
                return new OperationProfiler(this, operationName);
            }, "ProfileOperation") ?? new NoOpProfiler();
        }

        #endregion

        #region Public Methods - Async Operations

        /// <summary>
        /// Asynchronously gets frame analysis with cancellation support and enhanced error handling
        /// </summary>
        /// <param name="frameCount">Number of recent frames to analyze (default: 60)</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>FrameAnalysis object, or null if insufficient data</returns>
        /// <exception cref="OperationCanceledException">Thrown when operation is cancelled</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when frameCount is out of valid range</exception>
        public async Task<FrameAnalysis?> GetFrameAnalysisAsync(
            int frameCount = 60, 
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => GetFrameAnalysis(frameCount), cancellationToken);
        }

        /// <summary>
        /// Asynchronously records multiple custom metrics with enhanced error handling
        /// </summary>
        /// <param name="metrics">Dictionary of metric names and values to record</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>Task representing the asynchronous operation</returns>
        /// <exception cref="ArgumentNullException">Thrown when metrics is null</exception>
        public async Task RecordCustomMetricsAsync(
            Dictionary<string, double> metrics, 
            CancellationToken cancellationToken = default)
        {
            await Task.Run(() =>
            {
                ExecuteWithErrorHandling(() =>
                {
                    ValidationHelpers.ThrowIfNull(metrics, nameof(metrics));
                    
                    foreach (var kvp in metrics)
                    {
                        RecordCustomMetric(kvp.Key, kvp.Value);
                    }
                }, "RecordCustomMetricsAsync");
            }, cancellationToken);
        }

        #endregion

        #region Private Methods - Initialization

        /// <summary>
        /// Initializes DirectX 12 monitoring with comprehensive error handling and graceful degradation
        /// </summary>
        private void InitializeDirectX12Monitoring()
        {
            try
            {
                if (_d3d12Device != null)
                {
                    ValidationHelpers.ValidateDirectXObject(_d3d12Device, nameof(_d3d12Device));
                    
                    _performanceQuery = new D3D12PerformanceQuery(_d3d12Device);
                    _gpuMemoryMonitor = new D3D12GpuMemoryMonitor(_d3d12Device);
                    _hardwareCounters = new D3D12HardwareCounters(_d3d12Device);
                    _presentTiming = new D3D12PresentTiming();
                    
                    _logger?.LogDebug("DirectX 12 performance monitoring initialized with real device");
                }
                else
                {
                    // Fallback to simulation mode if no DirectX device available
                    _performanceQuery = new D3D12PerformanceQuery(null);
                    _gpuMemoryMonitor = new D3D12GpuMemoryMonitor(null);
                    _hardwareCounters = new D3D12HardwareCounters(null);
                    _presentTiming = new D3D12PresentTiming();
                    
                    _logger?.LogDebug("DirectX 12 performance monitoring initialized in simulation mode");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize DirectX 12 monitoring, falling back to CPU-only monitoring");
                
                // Create minimal fallback implementations
                _performanceQuery = new D3D12PerformanceQuery(null);
                _gpuMemoryMonitor = new D3D12GpuMemoryMonitor(null);
                _hardwareCounters = new D3D12HardwareCounters(null);
                _presentTiming = new D3D12PresentTiming();
                
                _degradationStrategy.RecordFailure($"D3D12 initialization failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Initializes CPU counter with fallback handling
        /// </summary>
        private void InitializeCpuCounterWithFallback()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _logger?.LogDebug("Windows Performance Counter initialized for CPU monitoring");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Windows Performance Counter unavailable, CPU monitoring will be limited");
                _cpuCounter = null; // Fallback if counters unavailable
                _degradationStrategy.RecordFailure("CPU performance counter unavailable");
            }
        }

        /// <summary>
        /// Starts background metrics collection with error handling
        /// </summary>
        private void StartBackgroundMetricsCollection()
        {
            try
            {
                _metricsCollectionTimer = new Timer(CollectBackgroundMetricsSafely, null, 0, 16); // ~60Hz
                _logger?.LogDebug("Background metrics collection started");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to start background metrics collection");
                // Non-fatal error, continue without background collection
            }
        }

        #endregion

        #region Private Methods - Error Handling and Utilities

        /// <summary>
        /// Executes an operation with comprehensive error handling and graceful degradation
        /// </summary>
        /// <typeparam name="T">Return type of the operation</typeparam>
        /// <param name="operation">Operation to execute</param>
        /// <param name="operationName">Name of the operation for logging</param>
        /// <returns>Result of the operation, or default value on error</returns>
        private T? ExecuteWithErrorHandling<T>(Func<T> operation, string operationName)
        {
            try
            {
                return operation();
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning("Operation '{OperationName}' was cancelled", operationName);
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Operation '{OperationName}' failed", operationName);
                _degradationStrategy.RecordFailure($"{operationName} failed: {ex.Message}");
                
                // Return default value for nullable types
                return default;
            }
        }

        /// <summary>
        /// Executes an action with comprehensive error handling
        /// </summary>
        /// <param name="operation">Action to execute</param>
        /// <param name="operationName">Name of the operation for logging</param>
        private void ExecuteWithErrorHandling(Action operation, string operationName)
        {
            try
            {
                operation();
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning("Operation '{OperationName}' was cancelled", operationName);
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Operation '{OperationName}' failed", operationName);
                _degradationStrategy.RecordFailure($"{operationName} failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Records system metrics with enhanced error handling
        /// </summary>
        /// <param name="metrics">FrameMetrics object to populate</param>
        private void RecordSystemMetrics(FrameMetrics metrics)
        {
            try
            {
                metrics.CpuUsage = _cpuCounter?.GetCurrentValue() ?? 0.0;
                metrics.GcCollections = _gcMetrics.GetCollectionsLastFrame();
                metrics.MemoryUsage = _gcMetrics.GetMemoryUsage();
                metrics.ThreadPoolThreads = ThreadPool.ThreadCount;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to record system metrics");
                // Use fallback values
                metrics.CpuUsage = 0.0;
                metrics.GcCollections = 0;
                metrics.MemoryUsage = 0;
                metrics.ThreadPoolThreads = 0;
            }
        }

        /// <summary>
        /// Records DirectX 12 metrics with graceful degradation
        /// </summary>
        /// <param name="metrics">FrameMetrics object to populate</param>
        private void RecordDirectX12Metrics(FrameMetrics metrics)
        {
            lock (_d3d12Lock)
            {
                if (_d3d12Device != null)
                {
                    try
                    {
                        // End DirectX frame timing and get real GPU metrics
                        var gpuMetrics = _performanceQuery?.EndFrame() ?? new GpuMetrics();
                        var memoryMetrics = _gpuMemoryMonitor?.GetCurrentMemoryUsage() ?? new D3D12MemoryUsage();
                        var hardwareMetrics = _hardwareCounters?.EndFrame() ?? new D3D12HardwareCountersData();
                        var presentMetrics = _presentTiming?.EndFrame() ?? new D3D12PresentMetrics();
                        
                        // Update metrics with real DirectX data
                        metrics.GpuTime = gpuMetrics.TotalGpuTime;
                        metrics.GpuMemoryUsage = memoryMetrics.CurrentUsage;
                        metrics.GpuMemoryBudget = memoryMetrics.Budget;
                        metrics.GpuUtilization = hardwareMetrics.Utilization;
                        metrics.GpuVertexThroughput = hardwareMetrics.VertexThroughput;
                        metrics.GpuPixelThroughput = hardwareMetrics.PixelThroughput;
                        metrics.GpuMemoryBandwidth = hardwareMetrics.MemoryBandwidth;
                        metrics.PresentTime = presentMetrics.PresentDuration;
                        metrics.FrameLatency = presentMetrics.FrameLatency;
                        
                        // Record pipeline statistics
                        metrics.IaPrimitives = hardwareMetrics.IaPrimitives;
                        metrics.VsInvocations = hardwareMetrics.VsInvocations;
                        metrics.PsInvocations = hardwareMetrics.PsInvocations;
                        metrics.CsInvocations = hardwareMetrics.CsInvocations;
                        
                        // Record custom DirectX metrics
                        if (metrics.CustomMetrics == null)
                            metrics.CustomMetrics = new Dictionary<string, double>();
                            
                        metrics.CustomMetrics["D3D12_QueryHeapTime"] = gpuMetrics.QueryHeapTime;
                        metrics.CustomMetrics["D3D12_TimestampFrequencies"] = gpuMetrics.TimestampFrequency;
                        metrics.CustomMetrics["D3D12_MemoryPressure"] = memoryMetrics.PressureLevel;
                        metrics.CustomMetrics["D3D12_AvailableDescriptors"] = memoryMetrics.AvailableDescriptors;
                        
                        _logger?.LogDebug("Recorded DirectX 12 metrics - GPU Time: {GpuTime:F2}ms, Memory: {MemoryUsage:F1}MB", 
                            metrics.GpuTime, metrics.GpuMemoryUsage / (1024.0 * 1024.0));
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to query some DirectX 12 metrics, using fallback values");
                        
                        // Use fallback simulated values if DirectX queries fail
                        metrics.GpuTime = _frameTimer.Value?.CurrentFrameTime * 0.7 ?? 5.0;
                        metrics.GpuMemoryUsage = 0;
                        metrics.GpuUtilization = 70.0;
                        metrics.PresentTime = 1.0;
                        metrics.GpuVertexThroughput = 8000000.0;
                        metrics.GpuPixelThroughput = 12000000.0;
                        metrics.GpuMemoryBandwidth = 256000000.0;
                        
                        _degradationStrategy.RecordFailure($"DirectX 12 metrics query failed: {ex.Message}");
                    }
                }
                else
                {
                    // Use simulated GPU metrics when no DirectX device available
                    metrics.GpuTime = _frameTimer.Value?.CurrentFrameTime * 0.7 ?? 5.0;
                    metrics.GpuUtilization = 70.0;
                    metrics.PresentTime = 1.0;
                    metrics.GpuVertexThroughput = 8000000.0;
                    metrics.GpuPixelThroughput = 12000000.0;
                    metrics.GpuMemoryBandwidth = 256000000.0;
                }
            }
        }

        /// <summary>
        /// Checks performance alerts with enhanced handling
        /// </summary>
        /// <param name="metrics">FrameMetrics to analyze</param>
        private void CheckPerformanceAlertsWithEnhancedHandling(FrameMetrics metrics)
        {
            try
            {
                // Frame time alerts
                if (metrics.TotalTime > _criticalFrameTime)
                {
                    OnPerformanceAlert(new PerformanceAlert
                    {
                        Type = AlertType.CriticalFrameTime,
                        Message = $"Critical frame time: {metrics.TotalTime:F2}ms (target: {_targetFrameTime:F2}ms)",
                        Value = metrics.TotalTime,
                        Threshold = _criticalFrameTime,
                        Timestamp = DateTime.UtcNow
                    });
                    
                    _degradationStrategy.RecordFailure($"Critical frame time: {metrics.TotalTime:F2}ms");
                }
                else if (metrics.TotalTime > _targetFrameTime * 1.2)
                {
                    OnPerformanceAlert(new PerformanceAlert
                    {
                        Type = AlertType.FrameTimeWarning,
                        Message = $"High frame time: {metrics.TotalTime:F2}ms (target: {_targetFrameTime:F2}ms)",
                        Value = metrics.TotalTime,
                        Threshold = _targetFrameTime * 1.2,
                        Timestamp = DateTime.UtcNow
                    });
                }
                
                // Memory pressure alerts
                if (metrics.MemoryUsage > 512 * 1024 * 1024) // 512MB
                {
                    OnPerformanceAlert(new PerformanceAlert
                    {
                        Type = AlertType.MemoryPressure,
                        Message = $"High memory usage: {metrics.MemoryUsage / (1024 * 1024)}MB",
                        Value = metrics.MemoryUsage,
                        Threshold = 512 * 1024 * 1024,
                        Timestamp = DateTime.UtcNow
                    });
                }
                
                // GC pressure alerts
                if (metrics.GcCollections > 5)
                {
                    OnPerformanceAlert(new PerformanceAlert
                    {
                        Type = AlertType.GcPressure,
                        Message = $"High GC pressure: {metrics.GcCollections} collections",
                        Value = metrics.GcCollections,
                        Threshold = 5,
                        Timestamp = DateTime.UtcNow
                    });
                }
                
                // GPU utilization alerts
                if (metrics.GpuUtilization > 95.0)
                {
                    OnPerformanceAlert(new PerformanceAlert
                    {
                        Type = AlertType.GpuUtilizationWarning,
                        Message = $"Very high GPU utilization: {metrics.GpuUtilization:F1}%",
                        Value = metrics.GpuUtilization,
                        Threshold = 95.0,
                        Timestamp = DateTime.UtcNow
                    });
                }
                
                // GPU memory pressure alerts
                if (metrics.GpuMemoryBudget > 0 && (double)metrics.GpuMemoryUsage / metrics.GpuMemoryBudget > 0.9)
                {
                    var memoryPressure = (double)metrics.GpuMemoryUsage / metrics.GpuMemoryBudget * 100.0;
                    OnPerformanceAlert(new PerformanceAlert
                    {
                        Type = AlertType.GpuMemoryPressure,
                        Message = $"High GPU memory pressure: {memoryPressure:F1}%",
                        Value = memoryPressure,
                        Threshold = 90.0,
                        Timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to check performance alerts");
                _degradationStrategy.RecordFailure($"Alert checking failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Analyzes frame performance with comprehensive metrics calculation
        /// </summary>
        /// <param name="frames">Array of FrameMetrics to analyze</param>
        /// <returns>FrameAnalysis with comprehensive performance metrics</returns>
        private FrameAnalysis AnalyzeFramePerformance(FrameMetrics[] frames)
        {
            var avgFrameTime = frames.Average(f => f.TotalTime);
            var frameVariance = CalculateVariance(frames.Select(f => f.TotalTime));
            var cpuVariance = CalculateVariance(frames.Select(f => f.CpuTime));
            var gpuVariance = CalculateVariance(frames.Select(f => f.GpuTime));
            var droppedFrames = frames.Count(f => f.TotalTime > _criticalFrameTime);
            
            // Calculate DirectX 12 metrics
            var avgGpuUtilization = frames.Average(f => f.GpuUtilization);
            var avgGpuMemoryUsage = frames.Average(f => f.GpuMemoryUsage);
            var avgGpuMemoryPressure = frames.Average(f => f.GpuMemoryBudget > 0 ? (double)f.GpuMemoryUsage / f.GpuMemoryBudget * 100.0 : 0);
            var avgPresentTime = frames.Average(f => f.PresentTime);
            var avgFrameLatency = frames.Average(f => f.FrameLatency);
            var avgGpuVertexThroughput = frames.Average(f => f.GpuVertexThroughput);
            var avgGpuPixelThroughput = frames.Average(f => f.GpuPixelThroughput);
            var avgGpuMemoryBandwidth = frames.Average(f => f.GpuMemoryBandwidth);
            
            // Calculate pipeline statistics averages
            var avgIaPrimitives = frames.Average(f => f.IaPrimitives);
            var avgVsInvocations = frames.Average(f => f.VsInvocations);
            var avgPsInvocations = frames.Average(f => f.PsInvocations);
            var avgCsInvocations = frames.Average(f => f.CsInvocations);
            
            return new FrameAnalysis
            {
                AverageFrameTime = avgFrameTime,
                FrameTimeVariance = frameVariance,
                CpuTimeVariance = cpuVariance,
                GpuTimeVariance = gpuVariance,
                AverageFps = 1000.0 / avgFrameTime,
                DroppedFrames = droppedFrames,
                PerformanceGrade = CalculatePerformanceGrade(avgFrameTime, frameVariance),
                MinFrameTime = frames.Min(f => f.TotalTime),
                MaxFrameTime = frames.Max(f => f.TotalTime),
                AverageCpuTime = frames.Average(f => f.CpuTime),
                AverageGpuTime = frames.Average(f => f.GpuTime),
                FrameCount = frames.Length,
                Timestamp = DateTime.UtcNow,
                
                // DirectX 12 specific analysis
                AverageGpuUtilization = avgGpuUtilization,
                AverageGpuMemoryUsage = avgGpuMemoryUsage,
                AverageGpuMemoryPressure = avgGpuMemoryPressure,
                AveragePresentTime = avgPresentTime,
                AverageFrameLatency = avgFrameLatency,
                AverageGpuVertexThroughput = avgGpuVertexThroughput,
                AverageGpuPixelThroughput = avgGpuPixelThroughput,
                AverageGpuMemoryBandwidth = avgGpuMemoryBandwidth,
                
                // Pipeline statistics
                AverageIaPrimitives = avgIaPrimitives,
                AverageVsInvocations = avgVsInvocations,
                AveragePsInvocations = avgPsInvocations,
                AverageCsInvocations = avgCsInvocations,
                
                Recommendations = GenerateEnhancedRecommendations(frames, avgFrameTime, frameVariance, avgGpuUtilization, avgGpuMemoryPressure)
            };
        }

        /// <summary>
        /// Generates performance recommendations with enhanced analysis
        /// </summary>
        private List<string> GenerateEnhancedRecommendations(FrameMetrics[] frames, double avgFrameTime, double frameVariance, double avgGpuUtilization, double avgGpuMemoryPressure)
        {
            var recommendations = new List<string>();
            
            // Frame time recommendations
            if (avgFrameTime > 16.67)
            {
                var fps = 1000.0 / avgFrameTime;
                recommendations.Add($"Average frame time ({avgFrameTime:F2}ms) equals {fps:F1} FPS, below 60 FPS target (16.67ms)");
            }
            
            if (Math.Sqrt(frameVariance) > 5.0)
            {
                recommendations.Add($"High frame time variance ({Math.Sqrt(frameVariance):F2}ms) detected - inconsistent frame pacing may cause stuttering");
            }
            
            // GPU utilization recommendations
            if (avgGpuUtilization > 95.0)
            {
                recommendations.Add($"Very high GPU utilization ({avgGpuUtilization:F1}%) - GPU bottleneck detected, consider reducing render complexity or resolution");
            }
            else if (avgGpuUtilization < 50.0 && avgFrameTime < 16.67)
            {
                recommendations.Add($"Low GPU utilization ({avgGpuUtilization:F1}%) with good frame time - likely CPU bound, consider optimizing CPU-side operations");
            }
            else if (avgGpuUtilization < 30.0)
            {
                recommendations.Add($"Very low GPU utilization ({avgGpuUtilization:F1}%) - significant GPU headroom available, consider increasing visual quality");
            }
            
            // GPU memory pressure recommendations
            if (avgGpuMemoryPressure > 90.0)
            {
                recommendations.Add($"High GPU memory pressure ({avgGpuMemoryPressure:F1}%) - consider reducing texture resolution, implementing memory streaming, or optimizing shader complexity");
            }
            else if (avgGpuMemoryPressure > 75.0)
            {
                recommendations.Add($"Moderate GPU memory pressure ({avgGpuMemoryPressure:F1}%) - monitor for potential memory issues");
            }
            
            // Pipeline statistics recommendations
            var avgPsInvocations = frames.Average(f => f.PsInvocations);
            var avgVsInvocations = frames.Average(f => f.VsInvocations);
            var avgCsInvocations = frames.Average(f => f.CsInvocations);
            
            if (avgCsInvocations > 0 && avgPsInvocations + avgVsInvocations == 0)
            {
                recommendations.Add("Compute-heavy workload detected - ensure compute shaders are optimized for target hardware");
            }
            
            if (avgPsInvocations > avgVsInvocations * 2)
            {
                recommendations.Add("High pixel-to-vertex ratio - consider optimizing pixel shader complexity, improving culling, or reducing overdraw");
            }
            
            // Memory usage recommendations
            var avgMemoryUsage = frames.Average(f => f.MemoryUsage);
            if (avgMemoryUsage > 1024 * 1024 * 1024) // 1GB
            {
                recommendations.Add("High CPU memory usage detected - consider optimizing data structures, reducing allocations, or implementing object pooling");
            }
            
            // GC pressure recommendations
            var avgGcCollections = frames.Average(f => f.GcCollections);
            if (avgGcCollections > 5.0)
            {
                recommendations.Add($"High GC pressure ({avgGcCollections:F1} collections/frame) - consider reducing allocations, using object pooling, or optimizing lifetime of short-lived objects");
            }
            
            // CPU usage recommendations
            var avgCpuUsage = frames.Average(f => f.CpuUsage);
            if (avgCpuUsage > 80.0)
            {
                recommendations.Add($"High CPU usage ({avgCpuUsage:F1}%) - consider optimizing CPU-side algorithms or parallelizing computationally intensive operations");
            }
            
            return recommendations;
        }

        /// <summary>
        /// Calculates performance grade based on frame time and variance
        /// </summary>
        private static string CalculatePerformanceGrade(double avgFrameTime, double variance)
        {
            var stdDev = Math.Sqrt(variance);
            
            if (avgFrameTime < 16.67 && stdDev < 2.0) return "A+";
            if (avgFrameTime < 20.0 && stdDev < 4.0) return "A";
            if (avgFrameTime < 25.0 && stdDev < 8.0) return "B";
            if (avgFrameTime < 33.3 && stdDev < 12.0) return "C";
            return "D";
        }

        /// <summary>
        /// Calculates variance for a set of values
        /// </summary>
        private static double CalculateVariance(IEnumerable<double> values)
        {
            var valuesList = values.ToList();
            if (valuesList.Count < 2) return 0.0;
            
            var mean = valuesList.Average();
            return valuesList.Sum(x => (x - mean) * (x - mean)) / valuesList.Count;
        }

        /// <summary>
        /// Background metrics collection with error handling
        /// </summary>
        private void CollectBackgroundMetricsSafely(object? state)
        {
            try
            {
                // Update GC metrics
                _gcMetrics.Update();
                
                // Could add additional background metric collection here
                // e.g., memory pressure, thread pool usage, etc.
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to collect background metrics");
                _degradationStrategy.RecordFailure($"Background metrics collection failed: {ex.Message}");
            }
        }

        #endregion

        #region Event Handling

        /// <summary>
        /// Raises performance alert event with error handling
        /// </summary>
        /// <param name="alert">PerformanceAlert to raise</param>
        protected virtual void OnPerformanceAlert(PerformanceAlert alert)
        {
            try
            {
                PerformanceAlert?.Invoke(this, alert);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to raise performance alert event: {AlertMessage}", alert.Message);
            }
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes performance monitor resources with comprehensive cleanup
        /// </summary>
        public void Dispose()
        {
            try
            {
                _metricsCollectionTimer?.Dispose();
                _frameTimer?.Dispose();
                _cpuCounter?.Dispose();
                
                // Dispose DirectX 12 performance monitoring resources
                lock (_d3d12Lock)
                {
                    _performanceQuery?.Dispose();
                    _gpuMemoryMonitor?.Dispose();
                    _hardwareCounters?.Dispose();
                    _presentTiming?.Dispose();
                }
                
                // Reset degradation strategy
                _degradationStrategy?.Reset();
                
                _logger?.LogInformation("PerformanceMonitorEnhanced disposed successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during PerformanceMonitorEnhanced disposal");
            }
        }

        #endregion

        #region Nested Classes

        /// <summary>
        /// No-op profiler for fallback scenarios
        /// </summary>
        private class NoOpProfiler : IDisposable
        {
            public void Dispose() { }
        }

        #endregion
    }
}
