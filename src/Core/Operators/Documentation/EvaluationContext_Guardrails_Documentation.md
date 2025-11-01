# EvaluationContext Guardrails Implementation (TIXL-014)

## Executive Summary

This document describes the comprehensive guardrail system implemented for the TiXL EvaluationContext to prevent runaway operator evaluations, resource exhaustion, and ensure stable operation. The system provides robust protection mechanisms while maintaining high performance and developer-friendly APIs.

## Overview

The guardrail system is designed to protect the TiXL operator execution environment from:
- **Resource exhaustion** (memory, CPU, file handles, network connections)
- **Infinite loops** and runaway computations
- **Timeout violations** in long-running operations
- **Invalid precondition states** before execution
- **Uncaught exceptions** that could crash the system
- **Performance degradation** through excessive resource usage

## Architecture

### Core Components

#### 1. **EvaluationContext** - Main guardrail coordinator
- Orchestrates all guardrail operations
- Provides safe execution wrappers
- Tracks execution state and metrics
- Manages resource limits and timeouts

#### 2. **GuardrailConfiguration** - Configurable limits and thresholds
- Time limits (evaluation duration, operation timeout)
- Memory limits (total memory, single allocation limits)
- Performance limits (CPU usage, operation count)
- Resource limits (file handles, network connections, textures)
- Safety modes (strict mode, debugging mode)

#### 3. **ExecutionState** - Runtime state tracking
- Monitors current execution status
- Enforces recursive operation limits
- Tracks operation counts and memory usage
- Manages execution mode (Normal, Safe, Panic)

#### 4. **PerformanceMonitor** - Metrics and monitoring
- Tracks resource usage in real-time
- Records performance metrics
- Generates health reports
- Provides optimization recommendations

#### 5. **PreconditionValidator** - Input validation
- Validates operator inputs before execution
- Checks precondition data sizes and counts
- Identifies dangerous or invalid inputs
- Provides safe input sanitization

#### 6. **OperationTracker** - Operation-level protection
- Tracks individual operations
- Provides detailed operation metrics
- Handles operation lifecycle management
- Enables granular performance monitoring

### Key Features

#### **Multi-layered Protection**
```csharp
// Level 1: Configuration validation
var config = GuardrailConfiguration.ForTesting();
config.Validate();

// Level 2: Runtime enforcement
context.ValidateCanProceed("OperationName");

// Level 3: Operation tracking
using (var tracker = context.BeginOperation("OperationName"))
{
    // Operation code here
}
```

#### **Automatic Resource Tracking**
```csharp
// Memory allocation tracking
context.TrackResourceAllocation("Texture", 1024 * 1024); // 1MB

// Automatic limit checking
context.ExecuteWithGuardrails("TextureOperation", () => {
    // Texture processing code
});
```

#### **Comprehensive Error Handling**
```csharp
// Error boundary protection
bool success = context.TryExecuteWithErrorBoundary("RiskyOperation", () => {
    // Potentially failing code
}, out var exception);

if (!success)
{
    Logger.Warning($"Operation failed: {exception.Message}");
}
```

## Configuration

### Predefined Configurations

#### **Default Configuration (Production)**
```csharp
var config = GuardrailConfiguration.Default;
// MaxEvaluationDuration: 5 seconds
// MaxMemoryBytes: 100MB
// MaxOperationsPerEvaluation: 1000
// StrictMode: false
```

#### **Testing Configuration**
```csharp
var config = GuardrailConfiguration.ForTesting();
// MaxEvaluationDuration: 1 second
// MaxMemoryBytes: 10MB
// MaxOperationsPerEvaluation: 100
// StrictMode: true
```

#### **Performance Configuration**
```csharp
var config = GuardrailConfiguration.ForPerformance();
// MaxEvaluationDuration: 30 seconds
// MaxMemoryBytes: 512MB
// MaxOperationsPerEvaluation: 10000
// EnablePreconditionValidation: false
```

#### **Development Configuration**
```csharp
var config = GuardrailConfiguration.ForDevelopment();
// MaxEvaluationDuration: 10 seconds
// MaxMemoryBytes: 256MB
// DetailedViolationLogging: true
// EnableAutoRecovery: true
```

### Custom Configuration
```csharp
var customConfig = new GuardrailConfiguration
{
    MaxEvaluationDuration = TimeSpan.FromSeconds(10),
    MaxMemoryBytes = 256 * 1024 * 1024, // 256MB
    MaxOperationsPerEvaluation = 5000,
    OnViolation = GuardrailViolationAction.LogAndContinue,
    StrictMode = true
};

customConfig.Validate(); // Ensures valid configuration
```

## Usage Patterns

### Basic Usage

#### **Simple Guarded Operation**
```csharp
context.ExecuteWithGuardrails("SimpleOperation", () => {
    // Your operator code here
    ProcessData();
});
```

#### **Async Operation with Guardrails**
```csharp
await context.ExecuteWithGuardrailsAsync("AsyncOperation", async (ct) => {
    // Async operator code
    await ProcessDataAsync(ct);
    return result;
});
```

#### **Error Boundary Pattern**
```csharp
if (context.TryExecuteWithErrorBoundary("RiskyOperation", () => {
    // Potentially failing code
    RiskyOperation();
}, out var exception))
{
    // Operation succeeded
    Logger.Information("Risky operation completed successfully");
}
else
{
    // Operation failed but system is protected
    Logger.Warning($"Operation failed safely: {exception?.Message}");
}
```

### Advanced Usage

#### **Custom Guarded Operator**
```csharp
public class MyOperator : GuardrailedOperator
{
    public MyOperator(IRenderingEngine renderingEngine, 
                     IAudioEngine audioEngine, 
                     IResourceManager resourceManager, 
                     ILogger logger)
        : base("MyOperator", renderingEngine, audioEngine, resourceManager, logger)
    {
    }

    public void ProcessShader(byte[] shaderData)
    {
        ExecuteGuarded("ProcessShader", () => {
            // Validate inputs
            if (shaderData.Length > 1024 * 1024) // 1MB limit
                throw new ArgumentException("Shader data too large");

            // Track resource usage
            TrackResource("ShaderData", shaderData.Length);
            RecordMetric("ShadersProcessed", 1, "count");

            // Process shader
            CompileShader(shaderData);
        });
    }

    protected override IDictionary<string, object> GetPreconditions()
    {
        return new Dictionary<string, object>
        {
            { "ShaderCompiler", "Available" },
            { "CompilationTimeout", 5000 },
            { "MaxShaderSize", 1024 * 1024 }
        };
    }
}
```

#### **Resource-Intensive Operations**
```csharp
context.ExecuteWithGuardrails("ResourceIntensiveOp", () => {
    // Large texture processing
    var texture = LoadLargeTexture("4k_texture.png");
    
    // System automatically tracks this allocation
    context.TrackResourceAllocation("Texture", texture.SizeInBytes);
    
    // Process texture with automatic timeout protection
    ProcessTexture(texture);
    
    // Get real-time status
    var status = context.CheckResourceLimits();
    if (!status.IsHealthy)
    {
        Logger.Warning("Resource limits approaching");
    }
});
```

#### **Performance Monitoring**
```csharp
using (var tracker = context.BeginOperation("ExpensiveOperation"))
{
    // Record custom metrics
    tracker.RecordMetric("ProcessingTime", 250.0, "ms");
    tracker.RecordMetric("ItemsProcessed", 1000, "count");
    
    // Track resources
    tracker.TrackResource("Memory", 50 * 1024 * 1024); // 50MB
    
    // Perform operation
    ExpensiveOperation();
    
    // Check status
    var status = tracker.CheckStatus();
    if (status.MemoryUsagePercent > 80)
    {
        Logger.Warning("High memory usage detected");
    }
    
    // Get operation summary
    var summary = tracker.GetSummary();
    Logger.Information($"Operation completed in {summary.Duration}");
}
```

## Monitoring and Diagnostics

### Real-time Status Monitoring
```csharp
// Get current guardrail status
var status = context.CheckResourceLimits();

Console.WriteLine($"System Health: {status.IsHealthy}");
Console.WriteLine($"Memory Usage: {status.MemoryUsagePercent:F1}%");
Console.WriteLine($"CPU Usage: {status.CpuUsagePercent:F1}%");
Console.WriteLine($"Active Warnings: {status.ActiveWarnings}");
```

### Performance Reporting
```csharp
// Generate comprehensive performance report
var report = context.GetPerformanceReport();

Console.WriteLine("=== Performance Report ===");
Console.WriteLine($"Duration: {report.ElapsedTime}");
Console.WriteLine($"Memory Usage: {report.ResourceStatistics.MemoryAllocated:N0} bytes");
Console.WriteLine($"Warnings: {report.Warnings.Count}");

if (report.Recommendations.Any())
{
    Console.WriteLine("Recommendations:");
    foreach (var rec in report.Recommendations)
    {
        Console.WriteLine($"  - {rec}");
    }
}
```

### Execution State Tracking
```csharp
// Get detailed execution state
var state = context.CurrentState.GetSummary();

Console.WriteLine($"Operations Executed: {state.OperationCount}");
Console.WriteLine($"Current Recursion Depth: {state.RecursionDepth}");
Console.WriteLine($"Memory Allocated: {state.MemoryAllocated:N0} bytes");
Console.WriteLine($"Execution Mode: {state.CurrentMode}");
```

## Error Handling

### Guardrail Violations

#### **Timeout Violations**
```csharp
try
{
    context.ExecuteWithGuardrails("LongOperation", () => {
        // This will throw OperationCanceledException if timeout exceeded
        Thread.Sleep(10000); // 10 seconds
    });
}
catch (OperationCanceledException ex)
{
    Logger.Warning($"Operation timed out: {ex.Message}");
    // System remains stable
}
```

#### **Memory Limit Violations**
```csharp
var success = context.TryExecuteWithErrorBoundary("MemoryOperation", () => {
    // This operation might exceed memory limits
    AllocateLargeBuffers();
}, out var exception);

if (!success && exception is OperationCanceledException)
{
    Logger.Warning("Operation cancelled due to memory limits");
    // System automatically recovers
}
```

### Custom Exception Handling
```csharp
var config = new GuardrailConfiguration
{
    ViolationExceptionFactory = (message) => new CustomGuardrailException(message)
};

var context = new EvaluationContext(/* ... */, config);
```

## Integration Guidelines

### For Operator Developers

#### **1. Use GuardrailedOperator Base Class**
```csharp
public class MyOperator : GuardrailedOperator
{
    // Gets automatic guardrail protection
    public void ProcessData(Data input)
    {
        ExecuteGuarded("ProcessData", () => {
            // Your operator implementation
        });
    }
}
```

#### **2. Implement Precondition Validation**
```csharp
protected override IDictionary<string, object> GetPreconditions()
{
    return new Dictionary<string, object>
    {
        { "InputData", input },
        { "MaxProcessingTime", 5000 },
        { "ResourceRequirements", GetResourceRequirements() }
    };
}
```

#### **3. Track Resource Usage**
```csharp
ExecuteGuarded("ProcessTexture", () => {
    var texture = LoadTexture(path);
    TrackResource("Texture", texture.SizeInBytes);
    // Process texture...
});
```

#### **4. Record Performance Metrics**
```csharp
ExecuteGuarded("CompileShader", () => {
    var stopwatch = Stopwatch.StartNew();
    
    // Compile shader
    var shader = CompileShader(source);
    
    // Record metrics
    RecordMetric("ShaderCompileTime", stopwatch.ElapsedMilliseconds, "ms");
    RecordMetric("ShaderSize", shader.Size, "bytes");
});
```

### For System Integrators

#### **1. Configure Appropriate Limits**
```csharp
// For real-time applications
var realtimeConfig = GuardrailConfiguration.ForPerformance();
realtimeConfig.MaxEvaluationDuration = TimeSpan.FromMilliseconds(16); // 60 FPS

// For batch processing
var batchConfig = GuardrailConfiguration.ForDevelopment();
batchConfig.MaxEvaluationDuration = TimeSpan.FromMinutes(5);
```

#### **2. Monitor System Health**
```csharp
// Regular health checks
var healthTimer = new Timer(_ => {
    var status = context.CheckResourceLimits();
    if (!status.IsHealthy)
    {
        AlertSystem("Guardrail health degradation detected");
    }
}, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
```

#### **3. Implement Custom Recovery**
```csharp
var config = new GuardrailConfiguration
{
    OnViolation = GuardrailViolationAction.SwitchToSafeMode,
    EnableAutoRecovery = true
};
```

## Performance Considerations

### **Minimal Overhead**
- Guardrail checks are highly optimized
- No allocation overhead in fast-path operations
- Efficient lock-free data structures where possible
- Configurable monitoring frequency

### **Memory Efficiency**
- Circular buffers for metric history
- Automatic cleanup of expired data
- Low-memory monitoring modes available
- Bounded collection sizes

### **CPU Efficiency**
- Minimal CPU usage for monitoring
- Optional detailed metrics collection
- Configurable warning frequencies
- Efficient state tracking

## Security Features

### **Input Validation**
- Precondition validation prevents malicious inputs
- Size limits prevent buffer overflow attacks
- Content scanning for dangerous patterns
- Safe input sanitization

### **Resource Protection**
- File handle limits prevent resource exhaustion
- Network connection limits prevent DoS attacks
- Memory limits prevent heap exhaustion
- CPU limits prevent computation hijacking

### **Execution Isolation**
- Operation timeouts prevent infinite loops
- Recursion limits prevent stack overflow
- Concurrent operation limits prevent resource contention
- Panic mode for emergency situations

## Testing and Validation

### **Unit Tests**
```csharp
[Test]
public void Guardrail_EnforcesMemoryLimits()
{
    var config = GuardrailConfiguration.ForTesting();
    config.MaxMemoryBytes = 1024 * 1024; // 1MB
    
    var context = CreateTestContext(config);
    
    Assert.Throws<OperationCanceledException>(() => {
        context.ExecuteWithGuardrails("LargeAllocation", () => {
            context.TrackResourceAllocation("LargeBuffer", 10 * 1024 * 1024); // 10MB
        });
    });
}
```

### **Integration Tests**
```csharp
[Test]
public void Operator_HandlesGuardrailViolations()
{
    var operator = new MyOperator(/* ... */);
    
    // Test that operator gracefully handles resource exhaustion
    Assert.DoesNotThrow(() => {
        operator.ProcessLargeData();
    });
    
    // Verify operator remains in valid state
    Assert.That(operator.GetOperatorStatus().IsHealthy);
}
```

### **Performance Tests**
```csharp
[Test]
public void Guardrail_PerformanceImpact()
{
    var stopwatch = Stopwatch.StartNew();
    
    // Execute many guarded operations
    for (int i = 0; i < 10000; i++)
    {
        context.ExecuteWithGuardrails($"Op{i}", () => {
            // Simple operation
        });
    }
    
    stopwatch.Stop();
    
    // Guardrail overhead should be minimal
    Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(1000));
}
```

## Migration Guide

### **From Legacy EvaluationContext**

#### **Before (Legacy)**
```csharp
public class OldOperator
{
    protected override void Evaluate(EvaluationContext context)
    {
        // No guardrail protection
        ProcessData();
    }
}
```

#### **After (With Guardrails)**
```csharp
public class NewOperator : GuardrailedOperator
{
    public NewOperator(IRenderingEngine renderingEngine, 
                      IAudioEngine audioEngine, 
                      IResourceManager resourceManager, 
                      ILogger logger)
        : base("NewOperator", renderingEngine, audioEngine, resourceManager, logger)
    {
    }

    protected override void Evaluate(EvaluationContext context)
    {
        ExecuteGuarded("Evaluate", () => {
            ProcessData();
        });
    }
}
```

### **Configuration Migration**
```csharp
// Old: No configuration
var oldContext = new EvaluationContext(renderingEngine, audioEngine, resourceManager, logger);

// New: With explicit configuration
var config = GuardrailConfiguration.Default;
var newContext = new EvaluationContext(renderingEngine, audioEngine, resourceManager, logger, 
                                      CancellationToken.None, config);
```

## Best Practices

### **1. Choose Appropriate Configuration**
- Use `ForTesting()` for unit tests
- Use `ForPerformance()` for real-time applications  
- Use `ForDevelopment()` during development
- Use `Default()` for production

### **2. Track Resources Appropriately**
- Track significant allocations (>1MB)
- Use descriptive resource types
- Track both memory and non-memory resources
- Avoid over-tracking (impacts performance)

### **3. Handle Gracefully**
- Expect `OperationCanceledException` in guarded operations
- Use `TryExecuteWithErrorBoundary` for risky operations
- Log guardrail violations for debugging
- Monitor system health regularly

### **4. Optimize Performance**
- Use fast-path operations when possible
- Disable detailed logging in production
- Configure appropriate warning intervals
- Use circular buffers for metrics

### **5. Security Considerations**
- Validate all inputs with precondition validation
- Set appropriate size limits
- Monitor for unusual usage patterns
- Use strict mode in security-critical applications

## Troubleshooting

### **Common Issues**

#### **Operations Canceled Too Quickly**
```csharp
// Problem: Operations timing out too fast
var config = GuardrailConfiguration.ForTesting();
config.MaxOperationDuration = TimeSpan.FromMilliseconds(10); // Too short

// Solution: Increase timeout
config.MaxOperationDuration = TimeSpan.FromMilliseconds(100);
```

#### **High Memory Usage Warnings**
```csharp
// Problem: Frequent memory warnings
var config = GuardrailConfiguration.Default;
config.MemoryWarningThreshold = 0.9; // 90% instead of 80%

// Solution: Adjust threshold or increase memory limit
config.MaxMemoryBytes = 512 * 1024 * 1024; // 512MB
```

#### **Performance Degradation**
```csharp
// Problem: Guardrail overhead too high
var config = GuardrailConfiguration.Default;
config.DetailedViolationLogging = true; // Slow
config.PerformanceWarningInterval = TimeSpan.FromSeconds(1); // Too frequent

// Solution: Reduce logging and increase warning intervals
config.DetailedViolationLogging = false;
config.PerformanceWarningInterval = TimeSpan.FromSeconds(30);
```

### **Debug Mode**
```csharp
// Enable debug logging
var debugConfig = GuardrailConfiguration.ForDebugging();
debugConfig.DetailedViolationLogging = true;
debugConfig.OnViolation = GuardrailViolationAction.LogAndContinue;
```

## Future Enhancements

### **Planned Features**
- Distributed guardrail coordination
- Machine learning-based limit optimization
- Advanced resource prediction
- Cross-process guardrail sharing
- Real-time guardrail analytics dashboard

### **Extension Points**
- Custom violation handlers
- External monitoring integration
- Custom resource trackers
- Extended validation rules
- Performance optimization hooks

## Conclusion

The EvaluationContext Guardrails system provides comprehensive protection for TiXL operator execution while maintaining excellent performance and developer experience. The system is designed to be:

- **Safe**: Prevents all common runaway evaluation scenarios
- **Fast**: Minimal performance overhead
- **Flexible**: Highly configurable for different use cases
- **Observable**: Rich monitoring and diagnostics
- **Maintainable**: Clean architecture and comprehensive testing

The implementation successfully addresses all requirements in TIXL-014 while providing a robust foundation for future enhancements.
