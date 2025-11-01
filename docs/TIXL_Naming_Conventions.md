# TiXL Naming Conventions Standard

**Document Version:** 1.0  
**Task ID:** TIXL-012  
**Last Updated:** 2025-11-02  
**Status:** Implementation Ready  

## Overview

This document establishes comprehensive naming conventions for the TiXL codebase to ensure consistency, readability, and maintainability across all modules. The conventions align with .NET best practices while accommodating TiXL-specific patterns and requirements.

## Core Principles

### 1. Consistency First
All code within the TiXL ecosystem must follow these conventions consistently, regardless of module or developer preference.

### 2. Readability Over Brevity
Names should clearly communicate intent and purpose, even if slightly longer than alternative abbreviations.

### 3. Domain-Specific Clarity
TiXL-specific terms and concepts should use established naming patterns that reflect the domain (operators, slots, nodes, etc.).

### 4. Tooling Integration
All conventions are designed to be automatically enforced through IDE configurations, analyzers, and build processes.

## Naming Rules by Element Type

### 1. Namespaces

**Pattern:** `TiXL.{Module}.{Feature}.{SubFeature}`

**Rules:**
- Use PascalCase for all segments
- Group related functionality under clear module boundaries
- Avoid abbreviations and acronyms (except well-known ones like `Gfx`, `UI`)

**Examples:**
```csharp
namespace TiXL.Core.Graphics.Shaders
namespace TiXL.Operators.Mathematical
namespace TiXL.Gui.Controls
namespace TiXL.Editor.Nodes
```

**Conventions:**
- `TiXL.Core` - Base framework and foundational systems
- `TiXL.Operators` - Custom operator definitions and implementations
- `TiXL.Gfx` - Graphics rendering and processing
- `TiXL.Gui` - User interface components and controls
- `TiXL.Editor` - Editor-specific functionality and tooling

### 2. Classes

**Pattern:** PascalCase with descriptive, domain-specific names

**Rules:**
- Use nouns or noun phrases that clearly describe the class purpose
- Avoid abbreviations unless industry-standard (e.g., `Vector2`, `Matrix4`)
- Prefix interfaces with `I` (see Interfaces section)
- Use singular form unless the class represents a collection or multiple entities

**Examples:**
```csharp
public class AsyncShaderCompiler { }
public class PerformanceMonitor { }
public class FrameTimer { }
public class OperationProfiler { }
public class ShaderCompilationStatistics { }
```

**TiXL-Specific Patterns:**
```csharp
// Operators
public class MathematicalOperator { }
public class ComparisonOperator { }

// Nodes and Graph Elements
public class NodeEditor { }
public class NodeDefinition { }

// Slots and Connections
public class InputSlot { }
public class OutputSlot { }
public class Connection { }
```

### 3. Interfaces

**Pattern:** PascalCase with `I` prefix

**Rules:**
- Always prefix with capital `I`
- Use adjective or noun form (e.g., `IDisposable`, `IEquatable<T>`)
- Name interfaces by their capability or contract, not implementation

**Examples:**
```csharp
public interface IOperator { }
public interface IOperator<T> : IOperator { }
public interface INode { }
public interface IGraph { }
public interface IRenderable { }
public interface IConfigurable { }
```

### 4. Methods

**Pattern:** PascalCase with clear verb-object structure

**Rules:**
- Begin with verbs that clearly describe the action
- Use full words, avoid abbreviations
- Method names should read like imperative sentences
- For boolean methods, use positive naming (e.g., `IsValid()` not `IsInvalid()`)

**Common Verb Patterns:**
- **Query Methods:** `Get()`, `Calculate()`, `Determine()`, `Find()`, `Search()`
- **Action Methods:** `Execute()`, `Process()`, `Render()`, `Compile()`, `Validate()`
- **State Management:** `Begin()`, `End()`, `Start()`, `Stop()`, `Initialize()`, `Dispose()`
- **Conversion Methods:** `ToString()`, `Convert()`, `Transform()`, `Parse()`

**Examples:**
```csharp
public ShaderProgram CompileShaderAsync(string shaderName, ShaderMacro[] defines = null)
public void BeginFrame()
public void EndFrame()
public FrameAnalysis GetFrameAnalysis()
public void ClearCache()
public bool IsConfigurationValid()
public Task<ShaderProgram> PrecompileCommonVariants(string shaderName)
```

**TiXL-Specific Method Patterns:**
```csharp
// Graph Operations
public void ConnectSlots(ISlot source, ISlot target)
public void DisconnectSlots(ISlot source, ISlot target)
public IEnumerable<INode> GetConnectedNodes(INode node)

// Operator Operations
public T ApplyOperator<T>(IOperator<T> op, T value)
public bool CanEvaluate(IOperator op, IReadOnlyList<object> inputs)

// Node Operations
public void Evaluate(INode node, ExecutionContext context)
public void InitializeNode(INode node)
```

### 5. Properties

**Pattern:** PascalCase with noun or adjective form

**Rules:**
- Use nouns for simple data properties
- Use adjectives for boolean properties that indicate state
- Avoid abbreviations
- Follow property type conventions

**Boolean Properties:**
- Use `Is`, `Can`, `Has`, `Should`, or similar prefixes
- Always return positive state
- Use `true` for enabled/default state

**Examples:**
```csharp
public int CacheHitRate { get; private set; }
public int TotalCompilations { get; private set; }
public bool IsValid { get; set; }
public bool IsEnabled { get; set; }
public bool CanConnect { get; set; }
public string ShaderName { get; }
```

**TiXL-Specific Properties:**
```csharp
public class INode
{
    string Name { get; }
    IEnumerable<IInputSlot> InputSlots { get; }
    IEnumerable<IOutputSlot> OutputSlots { get; }
    bool IsEvaluated { get; }
    object Value { get; }
}

public class IOperator
{
    string DisplayName { get; }
    string Category { get; }
    Type InputType { get; }
    Type OutputType { get; }
}
```

### 6. Fields

**Pattern:** Private fields use `_camelCase` with underscore prefix, constants use `PascalCase`

**Rules:**
- **Private Fields:** Prefix with underscore and use camelCase (`_shaderCache`, `_maxConcurrentCompilations`)
- **Constants:** Use PascalCase (`MaxConcurrentCompilations`, `DefaultFrameTime`)
- **Static Fields:** Use PascalCase or `_camelCase` if private
- **Readonly Fields:** Use `_camelCase` with underscore prefix

**Examples:**
```csharp
public class AsyncShaderCompiler
{
    // Private fields
    private readonly ConcurrentDictionary<ShaderKey, ShaderProgram> _shaderCache;
    private readonly TaskFactory _compilationTaskFactory;
    private readonly int _maxConcurrentCompilations;
    
    // Constants
    public const int DefaultMaxConcurrentCompilations = 2;
    public const string DefaultShaderModel = "6.0";
    
    // Static fields
    private static readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(30);
}
```

### 7. Events

**Pattern:** PascalCase with clear event semantics

**Rules:**
- Use event names that clearly indicate what happened
- Use past tense for events (e.g., `Completed`, `Changed`, `Error`)
- Consider adding event arguments prefix (e.g., `PerformanceAlert`)
- Delegate signatures should follow method naming conventions

**Event Patterns:**
- **Completion Events:** `Completed`, `Finished`, `Ready`
- **State Change Events:** `Changed`, `Updated`, `Modified`
- **Error Events:** `Error`, `Failed`, `ExceptionOccurred`
- **Action Events:** `Clicked`, `Selected`, `Activated`

**Examples:**
```csharp
public event EventHandler<PerformanceAlert> PerformanceAlert;
public event EventHandler CompilationCompleted;
public event EventHandler<ValidationResult> ValidationFailed;
public event EventHandler<NodeEvaluationEventArgs> NodeEvaluated;
```

### 8. Events Delegates and Arguments

**Pattern:** Descriptive names following method conventions

**Rules:**
- Delegate types: PascalCase with descriptive suffix (e.g., `EventHandler<T>`, `Action<T>`)
- Event argument classes: End with `EventArgs`
- Use `sender` and `e` parameter names consistently

**Examples:**
```csharp
public class PerformanceAlert : EventArgs
{
    public AlertType Type { get; set; }
    public string Message { get; set; }
    public double Value { get; set; }
    public DateTime Timestamp { get; set; }
}

public class NodeEvaluationEventArgs : EventArgs
{
    public INode Node { get; set; }
    public object Result { get; set; }
    public TimeSpan EvaluationTime { get; set; }
}

public delegate void CompilationProgressHandler(object sender, CompilationProgressEventArgs e);
```

## TiXL-Specific Naming Conventions

### 9. Operators

**Pattern:** Descriptive names that indicate mathematical or logical operation

**Rules:**
- Use full operation names (`Addition`, `Multiplication`) rather than symbols
- Include type parameters when relevant (`AddOperator<T>`, `CompareOperator<T>`)
- Use consistent suffixes: `Operator`, `Logic`, `Filter`, `Transform`

**Examples:**
```csharp
public interface IAdditionOperator : IArithmeticOperator
public class AdditionOperator : IAdditionOperator
public class ComparisonOperator : IComparisonOperator
public class BooleanLogicOperator : IBooleanOperator
public class VectorMathOperator : IVectorOperator
```

### 10. Slots and Connections

**Pattern:** Use domain-specific terminology

**Rules:**
- Use `Slot` suffix for connection points (`InputSlot`, `OutputSlot`)
- Use `Connection` for established links
- Use descriptive names that indicate data type or purpose

**Examples:**
```csharp
public interface IInputSlot : ISlot
public interface IOutputSlot : ISlot
public class DataSlot : IDataSlot
public class ControlSlot : IControlSlot

public class Connection
{
    public IInputSlot Input { get; }
    public IOutputSlot Output { get; }
    public bool IsValid { get; }
}
```

### 11. Nodes and Graphs

**Pattern:** Use descriptive names that indicate node function

**Rules:**
- Nodes should be named by their function (`MathNode`, `LogicNode`)
- Use `Node` suffix consistently
- Use descriptive prefixes for related functionality

**Examples:**
```csharp
public interface INode
{
    string Name { get; }
    INodeDefinition Definition { get; }
    IReadOnlyList<ISlot> Slots { get; }
}

public class MathNode : INode
public class ConditionalNode : INode
public class LoopNode : INode
```

### 12. Enums

**Pattern:** PascalCase with clear, descriptive values

**Rules:**
- Use singular names for enum types (except bit flags)
- Use descriptive value names that indicate meaning
- Consider prefixing related values (e.g., `AlertType.FrameTimeWarning`)

**Examples:**
```csharp
public enum AlertType
{
    FrameTimeWarning,
    CriticalFrameTime,
    MemoryPressure,
    GcPressure,
    ThreadPoolExhaustion
}

public enum ShaderType
{
    VertexShader,
    PixelShader,
    ComputeShader,
    GeometryShader
}

[Flags]
public enum NodeExecutionFlags
{
    None = 0,
    ParallelExecution = 1,
    CacheResults = 2,
    ValidateInputs = 4
}
```

## File and Project Naming

### 13. File Names

**Pattern:** PascalCase matching primary class or interface

**Rules:**
- File names should match the primary public type they contain
- Use descriptive names that reflect the main purpose
- Group related types in appropriately named files

**Examples:**
```
AsyncShaderCompiler.cs
PerformanceMonitor.cs
FrameTimer.cs
ShaderCompilationStatistics.cs
```

### 14. Project Names

**Pattern:** `TiXL.{Module}` following system boundaries

**Projects:**
- `TiXL.Core` - Core framework
- `TiXL.Operators` - Operator definitions
- `TiXL.Gfx` - Graphics system
- `TiXL.Gui` - User interface
- `TiXL.Editor` - Editor tooling

## Configuration and Constants

### 15. Constants and Configuration

**Pattern:** Descriptive PascalCase names

**Rules:**
- Use full words that describe the value's purpose
- Group related constants under descriptive class names
- Use appropriate units in names where relevant

**Examples:**
```csharp
public static class PerformanceThresholds
{
    public const double TargetFrameTime = 16.67; // milliseconds
    public const double CriticalFrameTime = 33.33; // milliseconds
    public const int MaxConcurrentCompilations = 2;
    public const long MaxMemoryUsage = 512 * 1024 * 1024; // bytes
}

public static class ShaderDefaults
{
    public const string DefaultShaderModel = "6.0";
    public const int DefaultPixelShaderVersion = 50;
}
```

## Generic Type Parameters

### 16. Generic Constraints

**Pattern:** Descriptive PascalCase names

**Rules:**
- Use single letters (T, U, V) for simple generic types
- Use descriptive names for domain-specific generics
- Use `TResult`, `TInput`, `TOutput` for transformation patterns
- Use `TKey`, `TValue` for dictionary-like patterns

**Examples:**
```csharp
public interface IOperator<T>
public interface ITransformation<TInput, TResult>
public interface IComparableNode<T> where T : IComparable
public interface IEnumerable<T> 
```

## Abbreviation Guidelines

### 17. Acceptable Abbreviations

**General Rule:** Avoid abbreviations unless they're widely recognized or provide clear value

**Acceptable Abbreviations:**
- Common computer science terms: `IO`, `UI`, `API`, `HTTP`, `URL`, `XML`, `JSON`
- Mathematical terms: `Vector2`, `Vector3`, `Matrix4`
- Graphics terms: `Gfx`, `GPU`, `CPU`, `FPS`, `PBR`
- Time units: `Ms`, `Sec` (only when part of property names like `FrameTimeMs`)

**Never Abbreviate:**
- Variable or property names
- Method names in public APIs
- Class names except as specified above
- Comments or documentation

## Migration and Enforcement

### Implementation Strategy

1. **Phase 1: Documentation and Analysis**
   - Complete this specification document
   - Create automated tools for compliance checking
   - Establish baseline code analysis

2. **Phase 2: Tool Integration**
   - Configure Roslyn analyzers for enforcement
   - Update .editorconfig with naming rules
   - Integrate with CI/CD pipeline

3. **Phase 3: Gradual Migration**
   - Apply conventions to new code
   - Migrate existing code in batches
   - Provide automated fixes where possible

4. **Phase 4: Full Compliance**
   - Complete migration of all existing code
   - Enforce conventions through build process
   - Monitor and adjust as needed

## Enforcement Mechanisms

### Automatic Enforcement Tools

1. **EditorConfig Integration**
   - IDE-level enforcement of naming conventions
   - Real-time feedback during development

2. **Roslyn Analyzers**
   - Custom analyzers for TiXL-specific patterns
   - Build-time validation and automatic fixes

3. **Build System Integration**
   - Fail builds on naming convention violations
   - Continuous integration checks

4. **IDE Templates and Snippets**
   - Provide templates that follow conventions
   - Code completion that respects naming rules

### Compliance Checking

Use the provided tools to check compliance:
```bash
# Check naming convention compliance
dotnet run --project Tools/NamingConventionChecker -- check

# Auto-fix violations
dotnet run --project Tools/NamingConventionChecker -- fix --apply
```

## Examples

### Before and After Comparison

**Before (Inconsistent):**
```csharp
public class shaderCompiler 
{
    private readonly ConcurrentDictionary<string, object> cache;
    public int totalCompilations { get; set; }
    public void compile_shader(string name) { }
}
```

**After (Compliant):**
```csharp
public class ShaderCompiler
{
    private readonly ConcurrentDictionary<string, object> _cache;
    public int TotalCompilations { get; private set; }
    public void CompileShader(string shaderName) { }
}
```

### TiXL-Specific Example

**Before:**
```csharp
public class mathNode 
{
    public IInputSlot input1, input2;
    public IOutputSlot result;
    
    public void eval() { }
}
```

**After:**
```csharp
public class MathNode : INode
{
    public IReadOnlyList<IInputSlot> InputSlots { get; }
    public IReadOnlyList<IOutputSlot> OutputSlots { get; }
    
    public void EvaluateNode(INodeEvaluationContext context)
    {
        // Implementation
    }
}
```

## Conclusion

These naming conventions ensure that the TiXL codebase remains consistent, readable, and maintainable across all modules and contributors. By following these guidelines and utilizing the provided tools, developers can create high-quality code that aligns with .NET best practices and TiXL-specific patterns.

For questions or suggestions regarding these conventions, please refer to the project's contribution guidelines or create an issue in the repository.