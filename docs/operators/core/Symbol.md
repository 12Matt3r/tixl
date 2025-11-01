# Symbol Operator - Core Framework

## Overview

The `Symbol` class serves as the fundamental blueprint or definition for all operators within the TiXL visual programming environment. It represents the immutable definition of an operator, defining its structure, inputs, outputs, and internal connections.

## Class Definition

```csharp
/// <summary>
/// Blueprint for an operator definition within the TiXL system.
/// Defines the structural composition, inputs, outputs, and internal connections.
/// </summary>
/// <remarks>
/// <para>The Symbol class is the foundational component of TiXL's operator system, implementing:</para>
/// <para>• Composite Design Pattern for hierarchical operator structures</para>
/// <para>• Factory pattern for creating and managing multiple runtime Instances</para>
/// <para>• Dynamic type integration via reflection to concrete C# implementations</para>
/// <para>• Resource management integration with IResource and IDisposable</para>
/// <para>• Concurrency control using locks and ConcurrentDictionary</para>
/// </remarks>
public sealed partial class Symbol : IDisposable, IResource
```

## Key Properties

### Input and Output Definitions
```csharp
/// <summary>
/// Defines the operator's input interface with type and connection information.
/// </summary>
public List<Symbol.InputDefinition> InputDefinitions { get; }

/// <summary>
/// Defines the operator's output interface with type and connection information.
/// </summary>
public List<Symbol.OutputDefinition> OutputDefinitions { get; }
```

### Hierarchical Structure
```csharp
/// <summary>
/// Child operators contained within this operator definition.
/// Supports nested operator structures for complex compositions.
/// </summary>
public ConcurrentDictionary<Guid, Symbol.Child> Children { get; }

/// <summary>
/// Connection definitions specifying data flow between slots.
/// Defines how operators connect to each other within the graph.
/// </summary>
public List<Symbol.Connection> Connections { get; }
```

### Type Integration
```csharp
/// <summary>
/// Runtime type of the operator implementation.
/// Links to the concrete C# class that provides operator functionality.
/// </summary>
public Type InstanceType { get; }

/// <summary>
/// Animation system integration for time-based operations.
/// </summary>
public Animator Animator { get; }

/// <summary>
/// Playback settings for animation and timing control.
/// </summary>
public PlaybackSettings PlaybackSettings { get; }
```

## Key Methods

### Instance Management
```csharp
/// <summary>
/// Creates a new runtime instance of this operator definition.
/// </summary>
/// <param name="parentPath">Parent instance path for hierarchical context</param>
/// <returns>New operator instance with proper initialization</returns>
public Instance CreateInstance(Guid[] parentPath)

/// <summary>
/// Invalidates all child instances when symbol structure changes.
/// Ensures all runtime instances reflect the updated definition.
/// </summary>
public void InvalidateInputInAllChildInstances(Guid slotId, object defaultValue)
```

### Connection Management
```summary>
/// Adds a new connection between operator slots.
/// Handles multi-input slot connections and validates compatibility.
/// </summary>
/// <param name="connection">Connection definition with source and target information</param>
/// <returns>True if connection was successfully added</returns>
public bool TryAddConnection(Symbol.Connection connection)

/// <summary>
/// Removes a connection from the symbol definition.
/// Updates all connected instances and propagates changes.
/// </summary>
/// <param name="connection">Connection to remove</param>
public void RemoveConnection(Symbol.Connection connection)
```

### Resource Management
```csharp
/// <summary>
/// Releases all resources associated with the symbol.
/// Disposes child symbols and cleans up managed resources.
/// </summary>
public void Dispose()
```

## Usage Examples

### Basic Symbol Creation
```csharp
/// <summary>
/// Example of creating a custom operator symbol definition.
/// </summary>
/// <code>
/// // Define operator inputs and outputs
/// var symbol = new Symbol("MathOperator");
/// symbol.InputDefinitions.Add(new Symbol.InputDefinition
/// {
///     Id = Guid.NewGuid(),
///     Name = "InputA",
///     ValueType = typeof(float),
///     DefaultValue = 0.0f
/// });
/// symbol.InputDefinitions.Add(new Symbol.InputDefinition
/// {
///     Id = Guid.NewGuid(),
///     Name = "InputB", 
///     ValueType = typeof(float),
///     DefaultValue = 1.0f
/// });
/// 
/// symbol.OutputDefinitions.Add(new Symbol.OutputDefinition
/// {
///     Id = Guid.NewGuid(),
///     Name = "Result",
///     ValueType = typeof(float)
/// });
/// 
/// // Create runtime instance
/// var instance = symbol.CreateInstance(new Guid[0]);
/// </code>
```

### Composite Operator Definition
```csharp
/// <summary>
/// Example of creating a composite operator with child instances.
/// </summary>
/// <code>
/// // Create parent operator symbol
/// var compositeSymbol = new Symbol("CompositeProcessor");
/// 
/// // Define input/output for composite
/// var inputDef = new Symbol.InputDefinition
/// {
///     Id = Guid.NewGuid(),
///     Name = "DataInput",
///     ValueType = typeof(byte[])
/// };
/// compositeSymbol.InputDefinitions.Add(inputDef);
/// 
/// var outputDef = new Symbol.OutputDefinition
/// {
///     Id = Guid.NewGuid(),
///     Name = "ProcessedData",
///     ValueType = typeof(byte[])
/// };
/// compositeSymbol.OutputDefinitions.Add(outputDef);
/// 
/// // Add child operators
/// var filterChild = new Symbol.Child
/// {
///     Id = Guid.NewGuid(),
///     Symbol = filterSymbol
/// };
/// compositeSymbol.Children[filterChild.Id] = filterChild;
/// 
/// var outputChild = new Symbol.Child
/// {
///     Id = Guid.NewGuid(),
///     Symbol = outputSymbol  
/// };
/// compositeSymbol.Children[outputChild.Id] = outputChild;
/// 
/// // Define connections between children
/// compositeSymbol.Connections.Add(new Symbol.Connection
/// {
///     SourceParentOrChildId = inputChild.Id,
///     SourceSlotId = inputDef.Id,
///     TargetParentOrChildId = filterChild.Id,
///     TargetSlotId = filterInputDef.Id
/// });
/// </code>
```

## Thread Safety

### Concurrency Considerations
```csharp
/// <summary>
/// Symbol class implements thread-safe operations for:
/// • Child symbol access and modification
/// • Connection management
/// • Instance creation and invalidation
/// </summary>
/// <remarks>
/// <para>Thread safety mechanisms:</para>
/// <para>• ConcurrentDictionary for child symbol storage</para>
/// <para>• Lock statements for critical sections</para>
/// <para>• Atomic operations for state changes</para>
/// <para>• Immutable design for instance definitions</para>
/// </remarks>
```

## Performance Characteristics

### Memory Usage
- **Symbol Storage**: Minimal memory footprint for definitions
- **Child References**: Efficient storage of child operator references
- **Connection Storage**: Compact representation of connection data
- **Type Information**: Cached reflection data for performance

### Creation Performance
- **Symbol Creation**: O(1) for basic symbol instantiation
- **Instance Creation**: O(n) where n is number of child operators
- **Connection Processing**: O(m) where m is number of connections
- **Memory Allocation**: Bounded by operator complexity

## Related Classes

- **[Instance](Instance.md)** - Runtime execution of operator symbols
- **[ISlot](../core/ISlot.md)** - Data connection interface
- **[Connection](../core/Connection.md)** - Connection definition
- **[Symbol.Child](../core/SymbolChild.md)** - Child operator reference

## Cross-References

### Core Framework
- **[Operator System Architecture](../TIXL-068_Operator_API_Reference.md#operator-system-architecture)**
- **[Instance Management](../TIXL-068_Operator_API_Reference.md#instance-management)**
- **[Connection System](../TIXL-068_Operator_API_Reference.md#connection-system)**

### Related Documentation
- **[Symbol Package Documentation](../resources/SymbolPackage.md)**
- **[Resource Management Guide](../TIXL-068_Operator_API_Reference.md#resource-management)**
- **[Concurrency Guidelines](../TIXL-068_Operator_API_Reference.md#thread-safety)**

## Version Information

**Version Added**: 1.0  
**Last Modified**: 2025-11-02  
**Compatibility**: TiXL Core Framework  
**API Stability**: Stable

---

**Category**: Core Framework  
**Keywords**: symbol, operator, definition, blueprint, composite  
**Related Symbols**: Instance, Connection, ISlot  
**See Also**: [Creating Custom Operators](../TIXL-068_Operator_API_Reference.md#creating-custom-operators)