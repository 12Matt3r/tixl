# Instance Operator - Runtime Execution

## Overview

The `Instance` class represents the runtime execution context for an operator within the TiXL visual programming graph. It serves as the concrete manifestation of a `Symbol` (operator definition), managing the operator's lifecycle, connections, and data flow.

## Class Definition

```csharp
/// <summary>
/// Runtime representation of an operator instance within the visual programming graph.
/// Links a Symbol.Child (operator definition) to concrete execution context.
/// </summary>
/// <remarks>
/// <para>The Instance class provides:</para>
/// <para>• Runtime execution environment for operator logic</para>
/// <para>• Connection management between input and output slots</para>
/// <para>• Lifecycle management (initialization, execution, disposal)</para>
/// <para>• Guardrail integration for safe execution</para>
/// <para>• Resource management and hierarchy traversal</para>
/// <para>• State tracking via InstanceStatus flags</para>
/// </remarks>
public abstract partial class Instance : IDisposable
```

## Core Properties

### Hierarchical Context
```csharp
/// <summary>
/// Path from root to this instance in the operator graph hierarchy.
/// Provides unique identification within nested structures.
/// </summary>
public Guid[] InstancePath { get; }

/// <summary>
/// Reference to the parent instance path for hierarchy navigation.
/// </summary>
public Guid[] ParentPath { get; }

/// <summary>
/// Parent symbol child reference providing operator definition context.
/// </summary>
public Symbol.Child SymbolChild { get; }
```

### Input and Output Management
```csharp
/// <summary>
/// Input slots accepting data from other operators.
/// Each slot represents a connection point for incoming data.
/// </summary>
public List<IInputSlot> Inputs { get; }

/// <summary>
/// Output slots providing data to connected operators.
/// Each slot represents a data source for downstream operators.
/// </summary>
public List<ISlot> Outputs { get; }

/// <summary>
/// Child instances managed by this operator instance.
/// Supports hierarchical operator compositions.
/// </summary>
public ChildrenCollection Children { get; }
```

### Status and State
```csharp
/// <summary>
/// Current instance status flags for state tracking.
/// Controls initialization, connection, and lifecycle states.
/// </summary>
private InstanceStatus _status;

/// <summary>
/// Hash for optimized path-based lookups.
/// Performance optimization for hierarchical queries.
/// </summary>
private int _pathHash;

/// <summary>
/// Resource management integration for external dependencies.
/// </summary>
public IEnumerable<SymbolPackage> AvailableResourcePackages { get; }
```

## Key Methods

### Lifecycle Management
```csharp
/// <summary>
/// Initializes the operator instance with proper setup sequence.
/// </summary>
/// <remarks>
/// <para>Initialization process:</para>
/// <para>1. Sort input slots by definition order</para>
/// <para>2. Mark resource folders as dirty for loading</para>
/// <para>3. Recursively initialize all child instances</para>
/// <para>4. Reconnect to parent if parent is already initialized</para>
/// <para>5. Set InstanceStatus.Initialized flag</para>
/// </remarks>
public void Initialize()

/// <summary>
/// Gracefully disposes the instance and releases all resources.
/// </summary>
/// <remarks>
/// <para>Disposal sequence:</para>
/// <para>1. Set InstanceStatus.Disposed flag</para>
/// <para>2. Disconnect all external input connections</para>
/// <para>3. Dispose all child instances</para>
/// <para>4. Unregister from SymbolChild</para>
/// <para>5. Release managed and unmanaged resources</para>
/// </remarks>
public void Dispose()
```

### Connection Management
```csharp
/// <summary>
/// Disconnects all external input connections from this instance.
/// Used during reconnection and disposal operations.
/// </summary>
public void DisconnectInputs()

/// <summary>
/// Recursively reconnects all child instances in the hierarchy.
/// Called after symbol structure changes to restore connections.
/// </summary>
public void ReconnectChildren()

/// <summary>
/// Marks this instance and all parent instances as needing reconnection.
/// Propagates connection update requirements up the hierarchy.
/// </summary>
public void MarkNeedsConnections()

/// <summary>
/// Retrieves a specific input slot by its unique identifier.
/// </summary>
/// <param name="guid">Unique identifier of the input slot</param>
/// <returns>Input slot if found, null otherwise</returns>
public IInputSlot? GetInput(Guid guid)
```

### Resource Management
```csharp
/// <summary>
/// Gathers resource packages from this instance and all parent instances.
/// Provides hierarchical resource access for operator execution.
/// </summary>
/// <remarks>
/// <para>Resource gathering process:</para>
/// <para>1. Return own resource packages if not dirty</para>
/// <para>2. If dirty, gather from entire parent hierarchy</para>
/// <para>3. Aggregate all accessible resource packages</para>
/// <para>4. Update AvailableResourcePackages property</para>
/// </remarks>
public void GatherResourcePackages()
```

## Usage Examples

### Basic Instance Usage
```csharp
/// <summary>
/// Example of creating and initializing an operator instance.
/// </summary>
/// <code>
/// // Create operator instance from symbol
/// var mathSymbol = GetMathOperatorSymbol();
/// var instance = mathSymbol.CreateInstance(new Guid[0]);
/// 
/// // Initialize with proper setup
/// instance.Initialize();
/// 
/// // Connect to other operators
/// var inputInstance = CreateInputInstance();
/// inputInstance.Outputs[0].Connect(instance.Inputs[0]);
/// 
/// // Execute operator
/// var result = instance.Evaluate();
/// 
/// // Clean up when done
/// instance.Dispose();
/// </code>
```

### Hierarchical Operator Usage
```csharp
/// <summary>
/// Example of working with hierarchical operator structures.
/// </summary>
/// <code>
/// // Create composite operator instance
/// var compositeSymbol = GetCompositeOperatorSymbol();
/// var compositeInstance = compositeSymbol.CreateInstance(new Guid[0]);
/// 
/// // Initialize triggers child initialization
/// compositeInstance.Initialize();
/// 
/// // Access child instances through hierarchy
/// foreach (var child in compositeInstance.Children)
/// {
///     var childInstance = child.Value;
///     Console.WriteLine($"Child: {childInstance.SymbolChild.Symbol.Name}");
///     
///     // Each child can be independently manipulated
///     childInstance.Inputs[0].SetValue(defaultValue);
/// }
/// 
/// // Gather resources from entire hierarchy
/// var allResources = compositeInstance.AvailableResourcePackages;
/// </code>
```

### Connection Management
```csharp
/// <summary>
/// Example of dynamic connection management and reconnection.
/// </summary>
/// <code>
/// // Create instances with initial connections
/// var sourceInstance = CreateSourceInstance();
/// var targetInstance = CreateTargetInstance();
/// 
/// // Establish initial connection
/// sourceInstance.Outputs[0].Connect(targetInstance.Inputs[0]);
/// 
/// // Modify symbol structure (e.g., add new input)
/// ModifyTargetOperatorDefinition(targetInstance.SymbolChild.Symbol);
/// 
/// // Mark for reconnection
/// targetInstance.MarkNeedsConnections();
/// 
/// // Force reconnection
/// targetInstance.ReconnectChildren();
/// 
/// // Verify connections are restored
/// var inputSlot = targetInstance.GetInput(connectionGuid);
/// if (inputSlot != null && inputSlot.IsConnected)
/// {
///     Console.WriteLine("Connection successfully restored");
/// }
/// </code>
```

## State Management

### InstanceStatus Flags
```csharp
[Flags]
public enum InstanceStatus
{
    /// <summary>No special status</summary>
    None = 0,
    
    /// <summary>Resource folders need to be refreshed</summary>
    ResourceFoldersDirty = 1 << 0,
    
    /// <summary>Instance has been initialized</summary>
    Initialized = 1 << 1,
    
    /// <summary>Instance is actively executing</summary>
    Active = 1 << 2,
    
    /// <summary>Internal connections have been established</summary>
    ConnectedInternally = 1 << 3,
    
    /// <summary>Instance is currently reconnecting</summary>
    IsReconnecting = 1 << 4,
    
    /// <summary>Operator has been bypassed</summary>
    Bypassed = 1 << 5,
    
    /// <summary>Instance has been disposed</summary>
    Disposed = 1 << 6
}
```

### Status Checking
```csharp
/// <summary>
/// Common status checking patterns for safe operator interaction.
/// </summary>
/// <code>
/// // Check if instance is ready for operation
/// public bool CanExecute(Instance instance)
/// {
///     return instance.IsInitialized && 
///            !instance.IsDisposed && 
///            !instance.IsReconnecting;
/// }
/// 
/// // Safe execution with status validation
/// public object SafeExecute(Instance instance)
/// {
///     if (!CanExecute(instance))
///         throw new InvalidOperationException("Instance not ready");
///         
///     try
///     {
///         instance.Status |= InstanceStatus.Active;
///         return instance.Evaluate();
///     }
///     finally
///     {
///         instance.Status &= ~InstanceStatus.Active;
///     }
/// }
/// </code>
```

## Thread Safety

### Concurrent Access
```csharp
/// <summary>
/// Instance class is designed for thread-safe concurrent access.
/// </summary>
/// <remarks>
/// <para>Thread safety mechanisms:</para>
/// <para>• Lock-free reads for most operations</para>
/// <para>• Synchronized writes during initialization/disposal</para>
/// <para>• Atomic status flag operations</para>
/// <para>• Safe hierarchical traversal</para>
/// </remarks>
```

### Best Practices
- **Read Operations**: Multiple threads can safely read from initialized instances
- **Write Protection**: Use locks or synchronization for modifications
- **Lifecycle Control**: Ensure proper disposal in multi-threaded scenarios
- **Status Validation**: Check status flags before concurrent operations

## Performance Characteristics

### Memory Usage
- **Instance Footprint**: Minimal overhead per instance
- **Connection Storage**: Efficient storage of connection references
- **Child Management**: Optimized collection for child instances
- **Status Tracking**: Lightweight flag-based state management

### Execution Performance
- **Initialization**: O(n) where n is number of child instances
- **Connection Lookup**: O(1) average case for GUID-based lookups
- **Status Checking**: O(1) bit flag operations
- **Disposal**: O(n + m) where n is children, m is connections

### Optimization Tips
- **Lazy Initialization**: Use ResourceFoldersDirty flag to defer expensive setup
- **Connection Caching**: Cache frequently accessed connections
- **Status Caching**: Avoid repeated status flag checks
- **Hierarchical Batching**: Process multiple children together

## Error Handling

### Common Failure Scenarios
```csharp
/// <summary>
/// Common error conditions and handling strategies.
/// </summary>
try
{
    instance.Initialize();
}
catch (InvalidOperationException ex)
{
    // Instance already initialized
    if (ex.Message.Contains("already initialized"))
        return;
    throw;
}
catch (ObjectDisposedException ex)
{
    // Instance disposed during initialization
    Logger.Warning($"Instance {instance.SymbolChild.Symbol.Name} disposed during initialization");
}
catch (Exception ex)
{
    // Unexpected error - log and handle gracefully
    Logger.Error(ex, $"Failed to initialize instance {instance.SymbolChild.Symbol.Name}");
    instance.MarkNeedsConnections(); // Mark for retry
}
```

### Recovery Patterns
```csharp
/// <summary>
/// Robust error recovery patterns for instance management.
/// </summary>
public void RobustInitialize(Instance instance, int maxRetries = 3)
{
    for (int attempt = 0; attempt < maxRetries; attempt++)
    {
        try
        {
            if (instance.CanInitialize())
            {
                instance.Initialize();
                return; // Success
            }
        }
        catch (Exception ex) when (attempt < maxRetries - 1)
        {
            Logger.Warning(ex, $"Initialization attempt {attempt + 1} failed, retrying...");
            Thread.Sleep(100 * (attempt + 1)); // Exponential backoff
        }
    }
    
    // Final attempt or give up
    throw new InvalidOperationException($"Failed to initialize instance after {maxRetries} attempts");
}
```

## Related Classes

- **[Symbol](Symbol.md)** - Operator definition and blueprint
- **[ISlot](../core/ISlot.md)** - Slot interface for data connections
- **[EvaluationContext](../core/EvaluationContext.md)** - Runtime execution context
- **[GuardrailedOperator](../core/GuardrailedOperator.md)** - Base class for safe operator execution

## Cross-References

### Core Framework
- **[Operator System Architecture](../TIXL-068_Operator_API_Reference.md#operator-system-architecture)**
- **[Instance Management](../TIXL-068_Operator_API_Reference.md#instance-management)**
- **[Connection System](../TIXL-068_Operator_API_Reference.md#connection-system)**

### Performance Guidelines
- **[Performance Optimization](../TIXL-068_Operator_API_Reference.md#performance-guidelines)**
- **[Memory Management](../TIXL-068_Operator_API_Reference.md#memory-management)**
- **[Thread Safety](../TIXL-068_Operator_API_Reference.md#thread-safety)**

### Testing Guide
- **[Testing Operators](../TIXL-068_Operator_API_Reference.md#testing-operators)**
- **[Integration Testing](../TIXL-068_Operator_API_Reference.md#integration-testing)**
- **[Error Handling Patterns](../TIXL-068_Operator_API_Reference.md#error-handling)**

## Version Information

**Version Added**: 1.0  
**Last Modified**: 2025-11-02  
**Compatibility**: TiXL Core Framework  
**API Stability**: Stable

---

**Category**: Core Framework  
**Keywords**: instance, runtime, execution, lifecycle, hierarchy  
**Related Symbols**: Symbol, ISlot, EvaluationContext  
**See Also**: [Creating Custom Operators](../TIXL-068_Operator_API_Reference.md#creating-custom-operators)