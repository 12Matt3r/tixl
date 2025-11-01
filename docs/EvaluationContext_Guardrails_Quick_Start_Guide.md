# Quick Start Guide: EvaluationContext Guardrails

## Installation & Setup

The guardrail system is now part of the TiXL Core.Operators namespace and ready to use immediately.

### 1. Basic Setup

```csharp
using T3.Core.Operators;

// Create evaluation context with default guardrails
var context = new EvaluationContext(
    renderingEngine,
    audioEngine,
    resourceManager,
    logger
);

// Or with custom configuration
var config = GuardrailConfiguration.Default;
config.MaxEvaluationDuration = TimeSpan.FromSeconds(10);

var context = new EvaluationContext(
    renderingEngine,
    audioEngine,
    resourceManager,
    logger,
    CancellationToken.None,
    config
);
```

### 2. Using GuardrailedOperator (Recommended)

```csharp
public class MyOperator : GuardrailedOperator
{
    public MyOperator(IRenderingEngine engine, IAudioEngine audio, 
                     IResourceManager resources, ILogger logger)
        : base("MyOperator", engine, audio, resources, logger)
    {
    }

    public void ProcessShader(byte[] shaderData)
    {
        ExecuteGuarded("ProcessShader", () => {
            // Your operator code is automatically protected
            CompileShader(shaderData);
        });
    }

    protected override IDictionary<string, object> GetPreconditions()
    {
        return new Dictionary<string, object>
        {
            { "ShaderData", shaderData },
            { "MaxShaderSize", 1024 * 1024 } // 1MB
        };
    }
}
```

### 3. Quick Migration from Legacy Operators

**Before:**
```csharp
public class LegacyOperator
{
    protected override void Evaluate(EvaluationContext context)
    {
        // No protection
        ProcessData();
    }
}
```

**After:**
```csharp
public class ModernOperator : GuardrailedOperator
{
    public ModernOperator(/* ... */) : base("ModernOperator", /* ... */) { }

    protected override void Evaluate(EvaluationContext context)
    {
        // Add guardrails with minimal code change
        ExecuteGuarded("Evaluate", () => {
            ProcessData(); // Same logic, now protected
        });
    }
}
```

## Configuration Cheat Sheet

```csharp
// For real-time applications (games, 3D editors)
var realtime = GuardrailConfiguration.ForPerformance();
realtime.MaxEvaluationDuration = TimeSpan.FromMilliseconds(16); // 60 FPS

// For testing (unit tests, integration tests)
var testing = GuardrailConfiguration.ForTesting();
testing.MaxOperationDuration = TimeSpan.FromMilliseconds(10);

// For batch processing (export, rendering)
var batch = GuardrailConfiguration.Default;
batch.MaxEvaluationDuration = TimeSpan.FromMinutes(5);

// For development (debugging, development)
var dev = GuardrailConfiguration.ForDevelopment();
dev.DetailedViolationLogging = true;
dev.OnViolation = GuardrailViolationAction.LogAndContinue;
```

## Common Patterns

### Pattern 1: Simple Guarded Operation
```csharp
context.ExecuteWithGuardrails("MyOperation", () => {
    // Your operation code
    DoWork();
});
```

### Pattern 2: Resource Tracking
```csharp
context.ExecuteWithGuardrails("TextureOp", () => {
    var texture = LoadTexture(path);
    context.TrackResourceAllocation("Texture", texture.SizeInBytes);
    ProcessTexture(texture);
});
```

### Pattern 3: Error Boundaries
```csharp
if (context.TryExecuteWithErrorBoundary("RiskyOp", () => {
    // Risky operation that might fail
    RiskyOperation();
}, out var exception))
{
    // Success
    Logger.Information("Operation succeeded");
}
else
{
    // Failed but handled safely
    Logger.Warning($"Operation failed: {exception?.Message}");
}
```

### Pattern 4: Performance Monitoring
```csharp
using (var tracker = context.BeginOperation("ExpensiveOp"))
{
    tracker.RecordMetric("ProcessingTime", 250.0, "ms");
    tracker.TrackResource("Memory", 50 * 1024 * 1024); // 50MB
    
    ExpensiveOperation();
    
    var status = tracker.CheckStatus();
    if (!status.IsHealthy)
        Logger.Warning("Operation approaching limits");
}
```

## Monitoring & Diagnostics

### Check System Health
```csharp
var status = context.CheckResourceLimits();
Console.WriteLine($"Healthy: {status.IsHealthy}");
Console.WriteLine($"Memory: {status.MemoryUsagePercent:F1}%");
Console.WriteLine($"CPU: {status.CpuUsagePercent:F1}%");
```

### Generate Report
```csharp
var report = context.GetPerformanceReport();
Console.WriteLine($"Duration: {report.ElapsedTime}");
Console.WriteLine($"Warnings: {report.Warnings.Count}");

foreach (var rec in report.Recommendations)
    Console.WriteLine($"Recommendation: {rec}");
```

## Testing with Guardrails

### Unit Test Example
```csharp
[Test]
public void MyOperator_HandlesLargeData()
{
    var config = GuardrailConfiguration.ForTesting();
    config.MaxMemoryBytes = 1024 * 1024; // 1MB
    
    var context = EvaluationContext.CreateForTest(guardrails: config);
    var op = new MyOperator(/* ... */);

    Assert.DoesNotThrow(() => {
        op.ProcessLargeData(); // Will be cancelled if > 1MB
    });
    
    var status = context.CheckResourceLimits();
    Assert.That(status.IsHealthy); // Should be false if limit was hit
}
```

## Troubleshooting

### Operations Timing Out Too Quickly
**Problem:** Operations being cancelled immediately
```csharp
// Solution: Increase timeouts
var config = GuardrailConfiguration.ForTesting();
config.MaxOperationDuration = TimeSpan.FromMilliseconds(100); // Was 10ms
```

### High Memory Warnings
**Problem:** Getting memory warnings too frequently
```csharp
// Solution: Adjust thresholds
var config = GuardrailConfiguration.Default;
config.MemoryWarningThreshold = 0.9; // Warn at 90% instead of 80%
```

### Performance Issues
**Problem:** Guardrail overhead too high
```csharp
// Solution: Reduce logging
var config = GuardrailConfiguration.Default;
config.DetailedViolationLogging = false; // Disable detailed logs
config.PerformanceWarningInterval = TimeSpan.FromSeconds(30); // Less frequent warnings
```

## Best Practices Summary

### ✅ DO
- Use GuardrailedOperator base class for new operators
- Configure appropriate limits for your use case
- Track significant resource allocations (>1MB)
- Handle OperationCanceledException gracefully
- Monitor system health in production
- Use TryExecuteWithErrorBoundary for risky operations

### ❌ DON'T
- Disable guardrails in production code
- Track every single small allocation
- Ignore guardrail violations
- Use testing configurations in production
- Set limits too low for your operations
- Forget to handle cancellation exceptions

## Getting Help

### Documentation
- Full documentation: `/src/Core/Operators/Documentation/`
- Examples: `/src/Core/Operators/Examples/`
- This guide: `TIXL-014_EvaluationContext_Guardrails_Implementation_Summary.md`

### Common Error Messages

**"Maximum evaluation duration exceeded"**
- Increase `MaxEvaluationDuration` in configuration
- Or optimize your operation to run faster

**"Maximum memory usage exceeded"**
- Increase `MaxMemoryBytes` in configuration
- Or optimize memory usage in your operator

**"Maximum operations per evaluation exceeded"**
- Increase `MaxOperationsPerEvaluation` in configuration
- Or reduce operation count in your workflow

**"Precondition validation failed"**
- Check your input data with `ValidatePreconditions()`
- Ensure inputs meet size and content requirements

### Debug Mode
```csharp
// Enable comprehensive debugging
var debugConfig = GuardrailConfiguration.ForDebugging();
debugConfig.DetailedViolationLogging = true;
debugConfig.OnViolation = GuardrailViolationAction.LogAndContinue;
```

## Next Steps

1. **Start Simple**: Use `GuardrailedOperator` base class
2. **Configure Appropriately**: Choose the right configuration for your use case
3. **Monitor Health**: Regularly check system status
4. **Handle Errors**: Use error boundaries and handle cancellations
5. **Optimize Performance**: Use appropriate warning intervals and logging levels
6. **Test Thoroughly**: Use `ForTesting()` configuration in tests

The guardrail system is designed to be developer-friendly while providing comprehensive protection. Start with the basics and gradually adopt more advanced features as needed.
