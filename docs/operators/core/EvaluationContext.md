# EvaluationContext - Safe Execution Environment

## Overview

The `EvaluationContext` class provides a comprehensive execution environment for TiXL operators with built-in guardrails for safe operation. It prevents runaway evaluations, resource exhaustion, infinite loops, and provides structured error handling and performance monitoring.

## Class Definition

```csharp
/// <summary>
/// Enhanced EvaluationContext with comprehensive guardrails for safe operator execution.
/// Prevents runaway evaluations, resource exhaustion, and infinite loops.
/// </summary>
/// <remarks>
/// <para>The EvaluationContext provides:</para>
/// <para>• Guardrail protection against resource exhaustion and infinite loops</para>
/// <para>• Comprehensive error boundary handling for robust operation</para>
/// <para>• Performance monitoring and metrics collection</para>
/// <para>• Resource tracking and management integration</para>
/// <para>• Structured logging and debugging capabilities</para>
/// <para>• Cancellation support for graceful termination</para>
/// </remarks>
public class EvaluationContext : IDisposable
```

## Core Properties

### System Integration
```csharp
/// <summary>
/// Access to graphics rendering engine for visual operators.
/// Provides GPU resources, shader compilation, and rendering capabilities.
/// </summary>
public IRenderingEngine RenderingEngine { get; }

/// <summary>
/// Audio processing engine for sound synthesis and effects.
/// Handles real-time audio processing and playback.
/// </summary>
public IAudioEngine AudioEngine { get; }

/// <summary>
/// Resource manager for loading and managing external dependencies.
/// Provides access to textures, shaders, audio files, and other resources.
/// </summary>
public IResourceManager ResourceManager { get; }

/// <summary>
/// Structured logger for debugging and monitoring operator execution.
/// Provides different log levels and contextual information.
/// </summary>
public ILogger Logger { get; }

/// <summary>
/// Cancellation token for graceful operation termination.
/// Allows operators to respond to cancellation requests.
/// </summary>
public CancellationToken CancellationToken { get; }
```

### Guardrail Configuration
```csharp
/// <summary>
/// Current execution metrics for performance tracking and analysis.
/// Includes timing, memory usage, CPU utilization, and operation counts.
/// </summary>
public EvaluationMetrics Metrics => _performanceMonitor.GetCurrentMetrics();

/// <summary>
/// Current execution state including resource limits and violation status.
/// Tracks whether execution should continue or be terminated.
/// </summary>
public ExecutionState CurrentState => _executionState;

/// <summary>
/// Configuration for guardrail limits and safety thresholds.
/// Controls memory limits, timeouts, operation counts, and other constraints.
/// </summary>
public GuardrailConfiguration Configuration => _guardrails;
```

## Constructor and Factory Methods

### Standard Constructor
```csharp
/// <summary>
/// Creates a new EvaluationContext with specified components and guardrails.
/// </summary>
/// <param name="renderingEngine">Graphics rendering engine for visual operations</param>
/// <param name="audioEngine">Audio engine for sound processing</param>
/// <param name="resourceManager">Resource manager for file and asset loading</param>
/// <param name="logger">Logger for structured output and debugging</param>
/// <param name="cancellationToken">Cancellation token for graceful shutdown</param>
/// <param name="guardrails">Optional guardrail configuration, uses defaults if null</param>
/// <exception cref="ArgumentNullException">Thrown when required components are null</exception>
public EvaluationContext(
    IRenderingEngine renderingEngine,
    IAudioEngine audioEngine,
    IResourceManager resourceManager,
    ILogger logger,
    CancellationToken cancellationToken = default,
    GuardrailConfiguration? guardrails = null)
```

### Test Factory Method
```csharp
/// <summary>
/// Creates a new EvaluationContext with default guardrails for test scenarios.
/// Provides mock implementations for components to enable isolated testing.
/// </summary>
/// <param name="renderingEngine">Optional mock rendering engine</param>
/// <param name="audioEngine">Optional mock audio engine</param>
/// <param name="resourceManager">Optional mock resource manager</param>
/// <param name="logger">Optional mock logger</param>
/// <param name="guardrails">Test-specific guardrail configuration</param>
/// <returns>New EvaluationContext configured for testing</returns>
public static EvaluationContext CreateForTest(
    IRenderingEngine? renderingEngine = null,
    IAudioEngine? audioEngine = null,
    IResourceManager? resourceManager = null,
    ILogger? logger = null,
    GuardrailConfiguration? guardrails = null)
```

## Guardrail Methods

### Validation and Control
```csharp
/// <summary>
/// Validates that evaluation can proceed with current resource limits.
/// Throws OperationCanceledException if limits are exceeded.
/// </summary>
/// <param name="operationName">Name of the operation being validated (for logging)</param>
/// <exception cref="OperationCanceledException">Thrown when guardrail limits are exceeded</exception>
public void ValidateCanProceed([CallerMemberName] string operationName = "")

/// <summary>
/// Begins tracking a new operation for guardrail monitoring.
/// Returns disposable tracker that automatically ends operation on disposal.
/// </summary>
/// <param name="operationName">Descriptive name for the operation being tracked</param>
/// <returns>Disposable tracker for operation management</returns>
public IDisposable BeginOperation(string operationName)
```

### Protected Execution
```csharp
/// <summary>
/// Executes an action with automatic guardrail protection.
/// Ensures safe execution with proper cleanup and error handling.
/// </summary>
/// <param name="operationName">Descriptive name for the operation</param>
/// <param name="action">Action to execute with guardrail protection</param>
public void ExecuteWithGuardrails(string operationName, Action action)

/// <summary>
/// Executes a function with automatic guardrail protection.
/// Returns the function result while maintaining safety guarantees.
/// </summary>
/// <param name="operationName">Descriptive name for the operation</param>
/// <param name="func">Function to execute with guardrail protection</param>
/// <returns>Result of the protected function execution</returns>
public T ExecuteWithGuardrails<T>(string operationName, Func<T> func)

/// <summary>
/// Executes an async operation with guardrail protection.
/// Handles asynchronous operations while maintaining safety guarantees.
/// </summary>
/// <param name="operationName">Descriptive name for the operation</param>
/// <param name="asyncFunc">Async function to execute with guardrail protection</param>
/// <returns>Task representing the protected async operation</returns>
public async Task<T> ExecuteWithGuardrailsAsync<T>(
    string operationName, 
    Func<CancellationToken, Task<T>> asyncFunc)
```

### Resource Limit Checking
```csharp
/// <summary>
/// Checks if the current operation respects resource limits.
/// Returns detailed status about resource usage and limit compliance.
/// </summary>
/// <returns>Guardrail status including current usage and limit compliance</returns>
public GuardrailStatus CheckResourceLimits()

/// <summary>
/// Gets pre-validation check results for operator inputs.
/// Validates inputs against configuration constraints and rules.
/// </summary>
/// <param name="preconditions">Dictionary of input values to validate</param>
/// <returns>Validation result with errors, warnings, and compliance status</returns>
public PreconditionValidationResult ValidatePreconditions(IDictionary<string, object> preconditions)
```

## Error Boundary Methods

### Safe Execution Patterns
```csharp
/// <summary>
/// Executes an action with comprehensive error boundary protection.
/// Returns success/failure status without throwing exceptions.
/// </summary>
/// <param name="operationName">Descriptive name for the operation</param>
/// <param name="action">Action to execute with error boundary protection</param>
/// <param name="exception">Exception caught during execution, if any</param>
/// <returns>True if execution succeeded, false if exception occurred</returns>
public bool TryExecuteWithErrorBoundary(
    string operationName, 
    Action action, 
    out Exception? exception)

/// <summary>
/// Executes an async operation with error boundary protection.
/// Handles asynchronous operations with comprehensive error handling.
/// </summary>
/// <param name="operationName">Descriptive name for the operation</param>
/// <param name="asyncAction">Async action to execute with error boundary protection</param>
/// <param name="exception">Exception caught during execution, if any</param>
/// <returns>Task representing the error-bounded async operation</returns>
public async Task<bool> TryExecuteWithErrorBoundaryAsync(
    string operationName,
    Func<CancellationToken, Task> asyncAction,
    out Exception? exception)
```

## Resource Management

### Resource Tracking
```csharp
/// <summary>
/// Tracks resource allocation for memory limit enforcement.
/// Enables monitoring of operator resource usage for guardrail compliance.
/// </summary>
/// <param name="resourceType">Type of resource being allocated (e.g., "Texture", "Buffer")</param>
/// <param name="bytes">Number of bytes allocated</param>
public void TrackResourceAllocation(string resourceType, long bytes)

/// <summary>
/// Gets current resource usage statistics for monitoring and analysis.
/// Provides detailed breakdown of resource consumption by type.
/// </summary>
/// <returns>Current resource usage statistics</returns>
public ResourceUsageStatistics GetResourceUsage()

/// <summary>
/// Releases all tracked resources.
/// Used for cleanup and memory management during disposal.
/// </summary>
public void ReleaseTrackedResources()
```

## Performance Monitoring

### Metrics and Reporting
```csharp
/// <summary>
/// Records a performance metric for operator execution analysis.
/// Enables detailed performance tracking and optimization.
/// </summary>
/// <param name="metricName">Name of the metric being recorded</param>
/// <param name="value">Numeric value of the metric</param>
/// <param name="unit">Unit of measurement for the metric</param>
public void RecordMetric(string metricName, double value, string unit = "")

/// <summary>
/// Gets detailed performance report for analysis and optimization.
/// Provides comprehensive performance data for operator tuning.
/// </summary>
/// <returns>Detailed performance report with metrics and analysis</returns>
public PerformanceReport GetPerformanceReport()
```

## Usage Examples

### Basic Safe Execution
```csharp
/// <summary>
/// Example of using EvaluationContext for safe operator execution.
/// </summary>
/// <code>
/// // Create evaluation context with guardrails
/// var context = new EvaluationContext(
///     renderingEngine,
///     audioEngine,
///     resourceManager,
///     logger,
///     cancellationToken,
///     customGuardrails
/// );
/// 
/// try
/// {
///     // Execute operation with guardrail protection
///     var result = context.ExecuteWithGuardrails("ImageProcessing", () =>
///     {
///         // Safe execution context
///         var texture = LoadTexture("input.jpg");
///         context.TrackResourceAllocation("Texture", texture.MemorySize);
///         context.RecordMetric("TextureSize", texture.Width * texture.Height, "pixels");
///         
///         var processed = ApplyFilter(texture);
///         return processed;
///     });
///     
///     Console.WriteLine($"Processing completed successfully");
/// }
/// catch (OperationCanceledException)
/// {
///     Console.WriteLine("Operation cancelled due to guardrail limits");
/// }
/// catch (Exception ex)
/// {
///     Console.WriteLine($"Operation failed: {ex.Message}");
/// }
/// finally
/// {
///     context.Dispose();
/// }
/// </code>
```

### Error Boundary Pattern
```csharp
/// <summary>
/// Example of using error boundaries for robust error handling.
/// </summary>
/// <code>
/// // Execute with error boundary (no exceptions thrown)
/// if (context.TryExecuteWithErrorBoundary("DataProcessing", () =>
/// {
///     // Process potentially unstable data
///     var data = ParseData(inputString);
///     var result = TransformData(data);
///     SaveResult(result);
/// }, out var exception))
/// {
///     Console.WriteLine("Data processing completed successfully");
/// }
/// else
/// {
///     // Handle error gracefully
///     Logger.Warning(exception, "Data processing failed, using fallback");
///     var fallbackResult = ProcessWithFallback(inputString);
/// }
/// 
/// // Check guardrail status
/// var status = context.CheckResourceLimits();
/// if (!status.IsHealthy)
/// {
///     Console.WriteLine($"Guardrail violation: {status.ViolationType}");
/// }
/// </code>
```

### Performance Monitoring
```csharp
/// <summary>
/// Example of comprehensive performance monitoring.
/// </summary>
/// <code>
/// // Create context with performance monitoring
/// var context = new EvaluationContext(
///     renderingEngine, audioEngine, resourceManager, logger,
///     guardrails: GuardrailConfiguration.ForPerformance()
/// );
/// 
/// // Execute with detailed tracking
/// using (context.BeginOperation("ComplexCalculation"))
/// {
///     // Record operation start
///     context.RecordMetric("OperationStart", Environment.TickCount, "ms");
///     
///     // Execute calculation with intermediate metrics
///     var result = context.ExecuteWithGuardrails("Calculation", () =>
///     {
///         for (int i = 0; i < 1000; i++)
///         {
///             // Intermediate calculation
///             var stepResult = ExpensiveCalculation(i);
///             
///             // Record step metrics
///             context.RecordMetric($"Step{i}_Duration", stepResult.Duration, "ms");
///             context.TrackResourceAllocation("TempData", stepResult.MemoryUsed);
///             
///             // Check guardrails periodically
///             if (i % 100 == 0)
///             {
///                 var status = context.CheckResourceLimits();
///                 if (!status.IsHealthy)
///                     throw new OperationCanceledException("Resource limits exceeded");
///             }
///         }
///         
///         return finalResult;
///     });
///     
///     // Get comprehensive performance report
///     var report = context.GetPerformanceReport();
///     Logger.Info($"Performance Report: {report}");
/// }
/// </code>
```

### Testing with Mock Components
```csharp
/// <summary>
/// Example of using the test factory for isolated operator testing.
/// </summary>
/// <code>
/// [Test]
/// public void Operator_TestWithEvaluationContext()
/// {
///     // Create test context with mock components
///     var context = EvaluationContext.CreateForTest(
///         guardrails: GuardrailConfiguration.ForTesting()
///     );
///     
///     try
///     {
///         // Test operator with controlled environment
///         var operator = new TestOperator("TestOp", context);
///         
///         // Execute with safety guarantees
///         var result = context.ExecuteWithGuardrails("TestOperation", () =>
///         {
///             return operator.Evaluate(testInput);
///         });
///         
///         // Verify results
///         Assert.That(result, Is.EqualTo(expectedResult));
///         
///         // Check that no guardrail violations occurred
///         var status = context.CheckResourceLimits();
///         Assert.That(status.IsHealthy, Is.True);
///     }
///     finally
///     {
///         context.Dispose();
///     }
/// }
/// </code>
```

## Guardrail Configuration

### Default Configuration
```csharp
/// <summary>
/// Default guardrail configuration for normal operation.
/// </summary>
public static GuardrailConfiguration Default => new()
{
    MaxEvaluationDuration = TimeSpan.FromSeconds(30),
    MaxMemoryBytes = 512 * 1024 * 1024, // 512 MB
    MaxOperationsPerEvaluation = 10000,
    MaxConcurrentOperations = 100,
    EnablePerformanceMonitoring = true,
    EnableResourceTracking = true,
    LogLevel = LogLevel.Warning
};
```

### Performance Configuration
```csharp
/// <summary>
/// Guardrail configuration optimized for performance monitoring.
/// </summary>
public static GuardrailConfiguration ForPerformance() => new()
{
    MaxEvaluationDuration = TimeSpan.FromMinutes(5),
    MaxMemoryBytes = 1024 * 1024 * 1024, // 1 GB
    MaxOperationsPerEvaluation = 50000,
    MaxConcurrentOperations = 500,
    EnablePerformanceMonitoring = true,
    EnableResourceTracking = true,
    DetailedMetrics = true,
    LogLevel = LogLevel.Information
};
```

### Testing Configuration
```csharp
/// <summary>
/// Guardrail configuration optimized for testing scenarios.
/// </summary>
public static GuardrailConfiguration ForTesting() => new()
{
    MaxEvaluationDuration = TimeSpan.FromSeconds(1),
    MaxMemoryBytes = 64 * 1024 * 1024, // 64 MB
    MaxOperationsPerEvaluation = 1000,
    MaxConcurrentOperations = 10,
    EnablePerformanceMonitoring = false,
    EnableResourceTracking = false,
    LogLevel = LogLevel.Error,
    StrictMode = false // Allow more lenient behavior for testing
};
```

## Thread Safety

### Thread Safety Guarantees
```csharp
/// <summary>
/// EvaluationContext is designed for safe concurrent access.
/// </summary>
/// <remarks>
/// <para>Thread safety characteristics:</para>
/// <para>• Thread-safe read operations for most properties and methods</para>
/// <para>• Synchronized writes during context creation and disposal</para>
/// <para>• Atomic operation tracking via BeginOperation/Dispose pattern</para>
/// <para>• Guarded access to shared state and resources</para>
/// <para>• Safe cancellation token propagation</para>
/// </remarks>
```

### Best Practices
- **Single Instance**: Use one context per operator or operation scope
- **Thread Affinity**: Ensure operators respect thread requirements
- **Resource Isolation**: Don't share resources between contexts
- **Cancellation**: Propagate cancellation tokens properly

## Performance Characteristics

### Memory Usage
- **Context Footprint**: Minimal overhead for evaluation context
- **Resource Tracking**: Bounded memory for tracked resources
- **Metrics Storage**: Circular buffer for performance metrics
- **State Management**: Efficient state tracking with minimal overhead

### Execution Performance
- **Guardrail Check**: O(1) operation for limit validation
- **Resource Tracking**: O(1) for simple allocations, O(log n) for complex tracking
- **Error Boundary**: Minimal overhead for exception handling setup
- **Cancellation**: Immediate response to cancellation requests

### Optimization Tips
- **Context Reuse**: Reuse contexts for related operations when safe
- **Batch Tracking**: Group resource allocations to reduce tracking overhead
- **Selective Monitoring**: Enable/disable monitoring based on performance requirements
- **Async Patterns**: Use async methods for I/O-bound operations

## Related Classes

- **[GuardrailedOperator](GuardrailedOperator.md)** - Base class for operators using evaluation context
- **[ExecutionState](ExecutionState.md)** - State tracking for execution limits
- **[PerformanceMonitor](PerformanceMonitor.md)** - Performance tracking implementation
- **[GuardrailConfiguration](GuardrailConfiguration.md)** - Configuration for safety limits

## Cross-References

### Core Framework
- **[Guardrail System](../TIXL-068_Operator_API_Reference.md#guardrail-system)**
- **[Error Handling Patterns](../TIXL-068_Operator_API_Reference.md#error-handling)**
- **[Performance Monitoring](../TIXL-068_Operator_API_Reference.md#performance-monitoring)**

### Safety Guidelines
- **[Resource Management](../TIXL-068_Operator_API_Reference.md#resource-management)**
- **[Thread Safety](../TIXL-068_Operator_API_Reference.md#thread-safety)**
- **[Best Practices](../TIXL-068_Operator_API_Reference.md#best-practices)**

### Testing Guide
- **[Test Patterns](../TIXL-068_Operator_API_Reference.md#testing-patterns)**
- **[Error Case Testing](../TIXL-068_Operator_API_Reference.md#error-case-testing)**
- **[Performance Testing](../TIXL-068_Operator_API_Reference.md#performance-testing)**

## Version Information

**Version Added**: 1.0  
**Last Modified**: 2025-11-02  
**Compatibility**: TiXL Core Framework  
**API Stability**: Stable

---

**Category**: Core Framework  
**Keywords**: evaluation, context, guardrails, safety, performance, error-handling  
**Related Symbols**: GuardrailedOperator, ExecutionState, PerformanceMonitor  
**See Also**: [Guardrail System Overview](../TIXL-068_Operator_API_Reference.md#guardrail-system)