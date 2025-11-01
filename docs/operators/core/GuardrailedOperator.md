# GuardrailedOperator - Safe Operator Base Class

## Overview

The `GuardrailedOperator` abstract base class provides a foundation for implementing TiXL operators with built-in safety features, guardrail protection, and resource management. It encapsulates common operational patterns for safe, monitored, and resilient operator execution.

## Class Definition

```csharp
/// <summary>
/// Base class for operators with built-in guardrail protection.
/// Provides comprehensive safety features, resource tracking, and error handling.
/// </summary>
/// <remarks>
/// <para>The GuardrailedOperator provides:</para>
/// <para>• Automatic guardrail protection for all operator operations</para>
/// <para>• Resource tracking and management integration</para>
/// <para>• Comprehensive error handling with recovery mechanisms</para>
/// <para>• Performance monitoring and metrics collection</para>
/// <para>• Lifecycle management with proper disposal patterns</para>
/// <para>• Precondition and postcondition validation</para>
/// </remarks>
public abstract class GuardrailedOperator : IDisposable
```

## Protected Fields

### Core Components
```csharp
/// <summary>
/// Evaluation context providing guardrail protection and system integration.
/// Access to rendering engine, audio engine, resource manager, and logger.
/// </summary>
protected readonly EvaluationContext Context;

/// <summary>
/// Structured logger for operator-specific logging and debugging.
/// Configured with operator-specific context and naming.
/// </summary>
protected readonly ILogger Logger;

/// <summary>
/// Human-readable name of the operator for logging and identification.
/// Used in error messages, metrics, and status reporting.
/// </summary>
protected readonly string OperatorName;
```

## Constructor

### Standard Constructor
```csharp
/// <summary>
/// Creates a new guarded operator with specified dependencies and guardrails.
/// </summary>
/// <param name="operatorName">Human-readable name for the operator instance</param>
/// <param name="renderingEngine">Graphics rendering engine for visual operations</param>
/// <param name="audioEngine">Audio processing engine for sound operations</param>
/// <param name="resourceManager">Resource manager for file and asset loading</param>
/// <param name="logger">Logger for structured output and debugging</param>
/// <param name="guardrails">Optional guardrail configuration, uses defaults if null</param>
/// <exception cref="ArgumentNullException">Thrown when required parameters are null</exception>
protected GuardrailedOperator(
    string operatorName,
    IRenderingEngine renderingEngine,
    IAudioEngine audioEngine,
    IResourceManager resourceManager,
    ILogger logger,
    GuardrailConfiguration? guardrails = null)
```

## Protected Methods

### Safe Execution Methods
```csharp
/// <summary>
/// Executes an operation with full guardrail protection and error handling.
/// Provides comprehensive safety guarantees for operator operations.
/// </summary>
/// <param name="operationName">Descriptive name for the operation being executed</param>
/// <param name="operation">Operation to execute with protection</param>
/// <returns>Result of the protected operation</returns>
/// <exception cref="InvalidOperationException">Thrown when precondition validation fails</exception>
/// <exception cref="OperationCanceledException">Thrown when guardrail limits are exceeded</exception>
protected T ExecuteGuarded<T>(string operationName, Func<T> operation)

/// <summary>
/// Executes an async operation with full guardrail protection.
/// Handles asynchronous operations while maintaining safety guarantees.
/// </summary>
/// <param name="operationName">Descriptive name for the operation being executed</param>
/// <param name="asyncOperation">Async operation to execute with protection</param>
/// <returns>Task representing the protected async operation</returns>
protected async Task<T> ExecuteGuardedAsync<T>(string operationName, Func<CancellationToken, Task<T>> asyncOperation)
```

### Error Boundary Methods
```csharp
/// <summary>
/// Executes an operation with error boundary protection (doesn't throw exceptions).
/// Provides safe execution with graceful error handling and recovery.
/// </summary>
/// <param name="operationName">Descriptive name for the operation</param>
/// <param name="operation">Operation to execute with error boundary protection</param>
/// <param name="exception">Exception caught during execution, if any</param>
/// <returns>True if execution succeeded, false if exception occurred</returns>
protected bool TryExecuteGuarded(string operationName, Action operation, out Exception? exception)

/// <summary>
/// Executes an async operation with error boundary protection (doesn't throw exceptions).
/// Handles async operations with comprehensive error boundary protection.
/// </summary>
/// <param name="operationName">Descriptive name for the operation</param>
/// <param name="asyncOperation">Async operation to execute with error boundary protection</param>
/// <param name="exception">Exception caught during execution, if any</param>
/// <returns>Task representing the error-bounded async operation</returns>
protected async Task<bool> TryExecuteGuardedAsync(
    string operationName, 
    Func<CancellationToken, Task> asyncOperation, 
    out Exception? exception)
```

### Resource Management Methods
```csharp
/// <summary>
/// Tracks resource allocation within the operator for guardrail compliance.
/// Enables monitoring of resource usage for memory limit enforcement.
/// </summary>
/// <param name="resourceType">Type of resource being allocated (e.g., "Texture", "Buffer")</param>
/// <param name="bytes">Number of bytes allocated</param>
protected void TrackResource(string resourceType, long bytes)

/// <summary>
/// Records a performance metric for operator execution analysis.
/// Enables detailed performance tracking and optimization identification.
/// </summary>
/// <param name="metricName">Name of the metric being recorded</param>
/// <param name="value">Numeric value of the metric</param>
/// <param name="unit">Unit of measurement for the metric</param>
protected void RecordMetric(string metricName, double value, string unit = "")
```

### Validation Methods
```csharp
/// <summary>
/// Validates preconditions before operation execution.
/// Ensures operator state and inputs are valid before proceeding.
/// </summary>
/// <exception cref="InvalidOperationException">Thrown when precondition validation fails</exception>
protected virtual void ValidatePreconditions()

/// <summary>
/// Validates postconditions after operation execution.
/// Ensures operator state is consistent and resources are properly managed.
/// </summary>
/// <param name="result">Result of the operation to validate</param>
protected virtual void ValidatePostconditions(object? result)

/// <summary>
/// Gets preconditions for validation by the guardrail system.
/// Override to specify custom precondition requirements.
/// </summary>
/// <returns>Dictionary of precondition name/value pairs for validation</returns>
protected virtual IDictionary<string, object> GetPreconditions()
```

### Error Handling Methods
```summary>
/// Handles exceptions that occur during operation execution.
/// Provides structured error reporting and recovery guidance.
/// </summary>
/// <param name="operationName">Name of the operation that failed</param>
/// <param name="exception">Exception that occurred during execution</param>
protected virtual void HandleOperationException(string operationName, Exception exception)
```

### Status and Health Methods
```csharp
/// <summary>
/// Gets current operator status including guardrail health and execution metrics.
/// Provides comprehensive status information for monitoring and debugging.
/// </summary>
/// <returns>Complete operator status including health, metrics, and warnings</returns>
protected OperatorStatus GetOperatorStatus()
```

## Supporting Types

### OperatorStatus Class
```csharp
/// <summary>
/// Status information for a guarded operator including health, metrics, and warnings.
/// </summary>
public class OperatorStatus
{
    /// <summary>Name of the operator this status represents</summary>
    public string OperatorName { get; set; }
    
    /// <summary>Overall health status of the operator</summary>
    public bool IsHealthy { get; set; }
    
    /// <summary>Guardrail status including resource usage and limits</summary>
    public GuardrailStatus GuardrailStatus { get; set; }
    
    /// <summary>Execution metrics including timing and resource usage</summary>
    public EvaluationMetrics ExecutionMetrics { get; set; }
    
    /// <summary>Summary of current execution state</summary>
    public ExecutionStateSummary ExecutionState { get; set; }
    
    /// <summary>Time of the last status validation</summary>
    public DateTime LastValidationTime { get; set; }
    
    /// <summary>Array of warnings and issues detected during execution</summary>
    public string[] Warnings { get; set; }
}
```

## Usage Examples

### Basic Operator Implementation
```csharp
/// <summary>
/// Example of implementing a custom operator using GuardrailedOperator.
/// </summary>
public class MathOperator : GuardrailedOperator
{
    private float _cachedResult;
    private bool _resultValid;
    
    public MathOperator(string name, IRenderingEngine renderingEngine, 
        IAudioEngine audioEngine, IResourceManager resourceManager, ILogger logger)
        : base(name, renderingEngine, audioEngine, resourceManager, logger)
    {
    }
    
    /// <summary>
    /// Main evaluation method with automatic guardrail protection.
    /// </summary>
    public float Evaluate(float inputA, float inputB, string operation)
    {
        return ExecuteGuarded("MathEvaluation", () =>
        {
            // Track resource usage
            TrackResource("Calculation", 1024);
            RecordMetric("Inputs", 2.0);
            
            // Perform calculation with safety checks
            float result = operation switch
            {
                "Add" => inputA + inputB,
                "Subtract" => inputA - inputB,
                "Multiply" => inputA * inputB,
                "Divide" when inputB != 0 => inputA / inputB,
                "Divide" => throw new InvalidOperationException("Division by zero"),
                _ => throw new ArgumentException($"Unknown operation: {operation}")
            };
            
            // Record additional metrics
            RecordMetric("Result", result);
            RecordMetric("OperationComplexity", operation == "Multiply" ? 2.0 : 1.0);
            
            return result;
        });
    }
    
    /// <summary>
    /// Async evaluation with error boundary protection.
    /// </summary>
    public async Task<float> EvaluateAsync(float inputA, float inputB, string operation)
    {
        return await ExecuteGuardedAsync("AsyncMathEvaluation", async (ct) =>
        {
            // Simulate async operation
            await Task.Delay(10, ct);
            
            return Evaluate(inputA, inputB, operation);
        });
    }
    
    /// <summary>
    /// Safe evaluation with error boundary protection (no exceptions thrown).
    /// </summary>
    public bool TryEvaluate(float inputA, float inputB, string operation, out float result, out string error)
    {
        error = null;
        result = 0;
        
        if (TryExecuteGuarded("SafeMathEvaluation", () =>
        {
            result = Evaluate(inputA, inputB, operation);
        }, out var exception))
        {
            return true;
        }
        else
        {
            error = exception?.Message ?? "Unknown error";
            return false;
        }
    }
    
    /// <summary>
    /// Custom precondition validation.
    /// </summary>
    protected override void ValidatePreconditions()
    {
        base.ValidatePreconditions(); // Call base validation
        
        // Add custom validation
        var validation = GetPreconditions();
        if (!validation.ContainsKey("OperatorName") || 
            string.IsNullOrEmpty(validation["OperatorName"]?.ToString()))
        {
            throw new InvalidOperationException("Operator must have a valid name");
        }
    }
}
```

### Resource-Intensive Operator
```csharp
/// <summary>
/// Example of implementing a resource-intensive operator with proper tracking.
/// </summary>
public class ImageProcessor : GuardrailedOperator
{
    private readonly Dictionary<string, Texture2D> _textureCache = new();
    
    public ImageProcessor(string name, IRenderingEngine renderingEngine,
        IAudioEngine audioEngine, IResourceManager resourceManager, ILogger logger)
        : base(name, renderingEngine, audioEngine, resourceManager, logger)
    {
    }
    
    /// <summary>
    /// Process image with comprehensive resource tracking.
    /// </summary>
    public Texture2D ProcessImage(string imagePath, ProcessingOptions options)
    {
        return ExecuteGuarded("ImageProcessing", () =>
        {
            // Check cache first
            var cacheKey = $"{imagePath}_{options.GetHashCode()}";
            if (_textureCache.TryGetValue(cacheKey, out var cached))
            {
                RecordMetric("CacheHit", 1.0);
                return cached;
            }
            
            // Load and process image
            var image = LoadImage(imagePath);
            TrackResourceAllocation("ImageData", image.MemorySize);
            RecordMetric("ImageWidth", image.Width);
            RecordMetric("ImageHeight", image.Height);
            
            // Apply processing operations
            var processed = ProcessWithOptions(image, options);
            TrackResourceAllocation("ProcessedImage", processed.MemorySize);
            RecordMetric("ProcessingTime", processed.ProcessingDuration);
            
            // Manage cache size
            if (_textureCache.Count > 50)
            {
                var oldest = _textureCache.First();
                oldest.Value.Dispose();
                TrackResourceAllocation("CacheCleanup", -oldest.Value.MemorySize);
                _textureCache.Remove(oldest.Key);
            }
            
            _textureCache[cacheKey] = processed;
            return processed;
        });
    }
    
    /// <summary>
    /// Batch processing with error boundary protection.
    /// </summary>
    public ProcessingResult[] ProcessBatch(string[] imagePaths, ProcessingOptions options)
    {
        return ExecuteGuarded("BatchImageProcessing", () =>
        {
            var results = new List<ProcessingResult>();
            var totalMemory = 0L;
            
            foreach (var path in imagePaths)
            {
                try
                {
                    var result = ProcessImage(path, options);
                    results.Add(new ProcessingResult { Path = path, Success = true, Result = result });
                    totalMemory += result.MemorySize;
                    
                    // Check memory limits periodically
                    if (results.Count % 10 == 0)
                    {
                        var status = Context.CheckResourceLimits();
                        if (!status.IsHealthy)
                        {
                            Logger.Warning("Memory limits approaching, reducing batch size");
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    results.Add(new ProcessingResult { Path = path, Success = false, Error = ex.Message });
                    Logger.Warning(ex, $"Failed to process image: {path}");
                }
            }
            
            TrackResource("BatchTotalMemory", totalMemory);
            RecordMetric("BatchSize", results.Count);
            
            return results.ToArray();
        });
    }
    
    /// <summary>
    /// Get comprehensive operator status.
    /// </summary>
    public ImageProcessorStatus GetStatus()
    {
        var baseStatus = GetOperatorStatus();
        
        return new ImageProcessorStatus
        {
            OperatorName = baseStatus.OperatorName,
            IsHealthy = baseStatus.IsHealthy,
            GuardrailStatus = baseStatus.GuardrailStatus,
            ExecutionMetrics = baseStatus.ExecutionMetrics,
            CacheSize = _textureCache.Count,
            TotalCachedMemory = _textureCache.Values.Sum(t => t.MemorySize)
        };
    }
}
```

### Async Operator with Cancellation
```csharp
/// <summary>
/// Example of async operator with proper cancellation handling.
/// </summary>
public class StreamingProcessor : GuardrailedOperator
{
    public StreamingProcessor(string name, IRenderingEngine renderingEngine,
        IAudioEngine audioEngine, IResourceManager resourceManager, ILogger logger)
        : base(name, renderingEngine, audioEngine, resourceManager, logger)
    {
    }
    
    /// <summary>
    /// Streaming processing with cancellation support.
    /// </summary>
    public async IAsyncEnumerable<ProcessingResult> ProcessStreamAsync(
        IEnumerable<DataChunk> dataStream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var chunk in dataStream.WithCancellation(cancellationToken))
        {
            var result = await ExecuteGuardedAsync("StreamingProcessing", async (ct) =>
            {
                // Check cancellation before processing each chunk
                ct.ThrowIfCancellationRequested();
                
                // Process chunk with progress tracking
                var processed = await ProcessChunkAsync(chunk, ct);
                RecordMetric("ChunkSize", chunk.Size);
                RecordMetric("ProcessingProgress", chunk.Index);
                
                // Periodically check guardrails
                if (chunk.Index % 100 == 0)
                {
                    var status = Context.CheckResourceLimits();
                    if (!status.IsHealthy)
                    {
                        Logger.Warning("Guardrail limits exceeded, throttling processing");
                        await Task.Delay(100, ct); // Throttle to respect limits
                    }
                }
                
                return processed;
            });
            
            yield return result;
        }
    }
    
    /// <summary>
    /// Safe async processing with detailed error handling.
    /// </summary>
    public async Task<bool> TryProcessAsync(DataChunk chunk, out ProcessingResult result)
    {
        return await TryExecuteGuardedAsync("SafeStreamingProcessing", async (ct) =>
        {
            result = await ProcessChunkAsync(chunk, ct);
            return true;
        }, out var exception);
    }
}
```

## Custom Precondition Validation

### Advanced Validation Example
```csharp
/// <summary>
/// Example of implementing custom preconditions and postconditions.
/// </summary>
public class ValidatedOperator : GuardrailedOperator
{
    private int _operationCount;
    
    public ValidatedOperator(string name, IRenderingEngine renderingEngine,
        IAudioEngine audioEngine, IResourceManager resourceManager, ILogger logger)
        : base(name, renderingEngine, audioEngine, resourceManager, logger)
    {
    }
    
    /// <summary>
    /// Custom precondition validation.
    /// </summary>
    protected override void ValidatePreconditions()
    {
        base.ValidatePreconditions(); // Always call base implementation
        
        var preconditions = GetPreconditions();
        
        // Validate operator state
        if (_operationCount > 1000)
        {
            throw new InvalidOperationException("Operator has exceeded maximum operation count");
        }
        
        // Validate input parameters
        if (preconditions.ContainsKey("InputData"))
        {
            var inputData = preconditions["InputData"];
            if (inputData is not byte[] data || data.Length == 0)
            {
                throw new ArgumentException("Input data must be non-empty byte array");
            }
        }
    }
    
    /// <summary>
    /// Custom postcondition validation.
    /// </summary>
    protected override void ValidatePostconditions(object? result)
    {
        base.ValidatePostconditions(result); // Always call base implementation
        
        // Validate result consistency
        if (result is ProcessingResult pr)
        {
            if (pr.Success && pr.OutputData == null)
            {
                throw new InvalidOperationException("Successful operation must produce output data");
            }
            
            if (!pr.Success && !string.IsNullOrEmpty(pr.Error))
            {
                // This is expected for failed operations
            }
        }
        
        // Update operation count
        _operationCount++;
        RecordMetric("TotalOperations", _operationCount);
    }
    
    /// <summary>
    /// Provide custom preconditions for validation.
    /// </summary>
    protected override IDictionary<string, object> GetPreconditions()
    {
        return new Dictionary<string, object>
        {
            { "OperatorName", OperatorName },
            { "ExecutionTime", DateTime.UtcNow },
            { "OperationCount", _operationCount },
            { "ContextState", Context.CurrentState.GetSummary() }
        };
    }
}
```

## Performance Characteristics

### Resource Usage
- **Instance Overhead**: Minimal memory overhead for guardrail integration
- **Tracking Overhead**: Low overhead for resource and metric tracking
- **Validation Cost**: O(1) precondition checking, O(n) postcondition validation
- **Error Handling**: Minimal overhead for error boundary setup

### Execution Performance
- **Guardrail Check**: O(1) for basic operations
- **Resource Tracking**: O(1) for simple allocations, configurable batching
- **Error Boundary**: Near-zero overhead for successful operations
- **Cancellation**: Immediate response to cancellation requests

### Optimization Strategies
- **Selective Tracking**: Enable/disable tracking based on performance requirements
- **Batch Validation**: Group multiple operations for efficient validation
- **Async Patterns**: Use async methods for I/O-bound operations
- **Resource Pooling**: Reuse validation contexts when appropriate

## Thread Safety

### Thread Safety Guarantees
```csharp
/// <summary>
/// GuardrailedOperator provides thread-safe operation for:
/// • Read operations when not being disposed
/// • Concurrent execution with separate contexts
/// • Safe disposal from any thread
/// </summary>
```

### Best Practices
- **Context Isolation**: Each operator instance should have its own evaluation context
- **Thread Affinity**: Respect any thread requirements for rendering/audio operations
- **Resource Sharing**: Avoid sharing resources between operator instances
- **Disposal**: Ensure proper disposal even in multi-threaded scenarios

## Related Classes

- **[EvaluationContext](EvaluationContext.md)** - Core execution environment with guardrails
- **[ExecutionState](ExecutionState.md)** - State tracking for execution limits
- **[PerformanceMonitor](PerformanceMonitor.md)** - Performance tracking implementation
- **[PreconditionValidator](PreconditionValidator.md)** - Validation logic for preconditions

## Cross-References

### Core Framework
- **[Evaluation Context Integration](../TIXL-068_Operator_API_Reference.md#evaluation-context)**
- **[Guardrail System](../TIXL-068_Operator_API_Reference.md#guardrail-system)**
- **[Error Handling Patterns](../TIXL-068_Operator_API_Reference.md#error-handling)**

### Implementation Guide
- **[Creating Custom Operators](../TIXL-068_Operator_API_Reference.md#creating-custom-operators)**
- **[Best Practices](../TIXL-068_Operator_API_Reference.md#best-practices)**
- **[Performance Guidelines](../TIXL-068_Operator_API_Reference.md#performance-guidelines)**

### Testing Patterns
- **[Operator Testing](../TIXL-068_Operator_API_Reference.md#testing-operators)**
- **[Error Case Testing](../TIXL-068_Operator_API_Reference.md#error-case-testing)**
- **[Performance Testing](../TIXL-068_Operator_API_Reference.md#performance-testing)**

## Version Information

**Version Added**: 1.0  
**Last Modified**: 2025-11-02  
**Compatibility**: TiXL Core Framework  
**API Stability**: Stable

---

**Category**: Core Framework  
**Keywords**: guardrails, safety, resource-tracking, error-handling, async, performance  
**Related Symbols**: EvaluationContext, GuardrailConfiguration, PreconditionValidator  
**See Also**: [Creating Custom Operators](../TIXL-068_Operator_API_Reference.md#creating-custom-operators)