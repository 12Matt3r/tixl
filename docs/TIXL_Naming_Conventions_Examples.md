# TiXL Naming Conventions - Practical Examples

This document provides practical examples of applying TiXL naming conventions to real code, showing before/after comparisons and explaining the rationale behind each change.

## Table of Contents

1. [Basic Naming Patterns](#basic-naming-patterns)
2. [TiXL-Specific Examples](#tixl-specific-examples)
3. [Common Violations and Fixes](#common-violations-and-fixes)
4. [Migration Scenarios](#migration-scenarios)
5. [Best Practices](#best-practices)

## Basic Naming Patterns

### 1. Class Names

**Before (Violations):**
```csharp
public class shader_compiler 
public class mathNode 
public class performance_monitor 
public class shaderProgram 
```

**After (Compliant):**
```csharp
public class ShaderCompiler 
public class MathNode 
public class PerformanceMonitor 
public class ShaderProgram 
```

**Explanation:**
- All class names use PascalCase (first letter uppercase, each word capitalized)
- Underscores are removed and replaced with PascalCase
- Abbreviated names are expanded to full words for clarity

---

### 2. Interface Names

**Before (Violations):**
```csharp
public interface operator 
public interface node_interface 
public interface iShaderCompiler 
```

**After (Compliant):**
```csharp
public interface IOperator 
public interface INode 
public interface IShaderCompiler 
```

**Explanation:**
- All interfaces must start with capital "I"
- Interface names must be PascalCase
- Generic interfaces should follow the same pattern: `IOperator<T>`

---

### 3. Method Names

**Before (Violations):**
```csharp
public void compile_shader(string name) 
public bool isvalid() 
public object getdata() 
public void process_node(node n) 
```

**After (Compliant):**
```csharp
public void CompileShader(string shaderName) 
public bool IsValid() 
public object GetData() 
public void ProcessNode(INode node) 
```

**Explanation:**
- Method names use PascalCase with verb-object structure
- Common verbs: `Get`, `Set`, `Process`, `Execute`, `Validate`, `Calculate`
- Use full words rather than abbreviations
- Parameters should use PascalCase for complex types

---

### 4. Property Names

**Before (Violations):**
```csharp
public string shader_name { get; set; }
public bool is_valid { get; set; }
public int total_compilations { get; set; }
public List<Shader> shaders { get; set; }
```

**After (Compliant):**
```csharp
public string ShaderName { get; set; }
public bool IsValid { get; set; }
public int TotalCompilations { get; set; }
public IReadOnlyList<Shader> Shaders { get; set; }
```

**Explanation:**
- Properties use PascalCase
- Boolean properties use positive naming: `IsValid`, `CanConnect`, `HasResult`
- Use `IReadOnlyList<T>` for collections to prevent external modification

---

### 5. Field Names

**Before (Violations):**
```csharp
public class ShaderCompiler 
{
    private ConcurrentDictionary<string, ShaderProgram> shaderCache;
    private int maxConcurrentCompilations;
    private readonly TaskFactory compilationTaskFactory;
    
    public const int DEFAULT_MAX_COMPILATIONS = 2;
}
```

**After (Compliant):**
```csharp
public class ShaderCompiler 
{
    private readonly ConcurrentDictionary<string, ShaderProgram> _shaderCache;
    private readonly int _maxConcurrentCompilations;
    private readonly TaskFactory _compilationTaskFactory;
    
    public const int DefaultMaxConcurrentCompilations = 2;
}
```

**Explanation:**
- Private fields use `_camelCase` (underscore prefix + camelCase)
- Constants use PascalCase
- Readonly fields also use the private field pattern

---

### 6. Event Names

**Before (Violations):**
```csharp
public event EventHandler<Shader> shader_compiled;
public event EventHandler compilation_error;
public event EventHandler on_node_evaluated;
```

**After (Compliant):**
```csharp
public event EventHandler<Shader> ShaderCompiled;
public event EventHandler<CompilationErrorEventArgs> CompilationFailed;
public event EventHandler<NodeEvaluationEventArgs> NodeEvaluated;
```

**Explanation:**
- Events use PascalCase
- Use past tense for event names (indicating what happened)
- Use descriptive argument types with `EventArgs` suffix

---

### 7. Enum Names and Values

**Before (Violations):**
```csharp
public enum shader_type 
{
    vertex_shader,
    pixel_shader,
    compute_shader,
    geometry_shader
}
```

**After (Compliant):**
```csharp
public enum ShaderType
{
    VertexShader,
    PixelShader,
    ComputeShader,
    GeometryShader
}
```

**Explanation:**
- Enum type names use PascalCase
- Enum values use PascalCase with descriptive names
- Use clear, meaningful names that indicate the value's purpose

---

## TiXL-Specific Examples

### 8. Operator Classes

**Before (Violations):**
```csharp
public class add_operator : IOperator 
public class vector_math_op 
public class compare_logic 
```

**After (Compliant):**
```csharp
public class AdditionOperator : IOperator 
public class VectorMathOperator : IVectorOperator 
public class ComparisonOperator : IComparisonOperator 
```

**Explanation:**
- Operators follow consistent naming: `[Operation]Operator`
- Use descriptive names that clearly indicate the operation
- Implement appropriate interfaces (`IOperator<T>`, `IVectorOperator`, etc.)

---

### 9. Node Classes

**Before (Violations):**
```csharp
public class mathNode 
public class conditionalNode 
public class loop_node 
```

**After (Compliant):**
```csharp
public class MathNode : INode 
public class ConditionalNode : INode 
public class LoopNode : INode 
```

**Explanation:**
- Node classes use PascalCase with `[Function]Node` pattern
- Implement `INode` interface consistently
- Use descriptive names that indicate the node's behavior

---

### 10. Slot Classes

**Before (Violations):**
```csharp
public class input_slot 
public class outputSlot 
public class data_connection_point 
```

**After (Compliant):**
```csharp
public class InputSlot : IInputSlot 
public class OutputSlot : IOutputSlot 
public class DataSlot : IDataSlot 
```

**Explanation:**
- Slot classes use consistent `[Type]Slot` naming pattern
- Implement appropriate interfaces (`IInputSlot`, `IDataSlot`)
- Use domain-specific terminology that reflects TiXL's slot system

---

### 11. Namespace Structure

**Before (Violations):**
```csharp
namespace tixl.graphics.shaders 
namespace TIXL.Operators.Math 
namespace tixl.ui.controls 
```

**After (Compliant):**
```csharp
namespace TiXL.Core.Graphics.Shaders 
namespace TiXL.Operators.Mathematical 
namespace TiXL.Gui.Controls 
namespace TiXL.Editor.Nodes 
```

**Explanation:**
- All namespace segments use PascalCase
- Follow TiXL module structure: `TiXL.{Module}.{Feature}.{SubFeature}`
- Use consistent module names: `Core`, `Operators`, `Gfx`, `Gui`, `Editor`

---

## Common Violations and Fixes

### 12. Method Naming Issues

**Scenario:** Inconsistent verb usage and missing verb-object structure

**Before:**
```csharp
public class PerformanceMonitor 
{
    public void frame_start() { }
    public double get_current_frame_time() { }
    public void record_custom_metric(string name, double value) { }
    public bool is_configuration_valid() { }
}
```

**Issues:**
- Uses snake_case instead of PascalCase
- Missing verb-object structure
- Inconsistent verb usage

**After:**
```csharp
public class PerformanceMonitor 
{
    public void BeginFrame() { }
    public double GetCurrentFrameTime() { }
    public void RecordCustomMetric(string name, double value) { }
    public bool IsConfigurationValid() { }
}
```

**Fixed Issues:**
- All methods use PascalCase
- Clear verb-object structure
- Consistent verb usage

---

### 13. Property Naming Issues

**Scenario:** Boolean properties using negative naming

**Before:**
```csharp
public class Node 
{
    public bool IsNotEvaluated { get; set; }
    public bool IsInvalid { get; set; }
    public bool CantConnect { get; set; }
}
```

**Issues:**
- Negative boolean names are confusing
- Hard to understand when reading if-conditions

**After:**
```csharp
public class Node 
{
    public bool IsEvaluated { get; set; }
    public bool IsValid { get; set; }
    public bool CanConnect { get; set; }
}
```

**Fixed Issues:**
- All boolean properties use positive naming
- More readable in conditional statements

---

### 14. Field Naming Issues

**Scenario:** Inconsistent field naming patterns

**Before:**
```csharp
public class AsyncShaderCompiler 
{
    private ConcurrentDictionary<ShaderKey, ShaderProgram> shaderCache;
    private TaskFactory compilationTaskFactory;
    private int maxConcurrentCompilations;
    private readonly Queue<CompilationRequest> pendingCompilations;
    public const int DEFAULT_MAX_CONCURRENT_COMPILATIONS = 2;
}
```

**Issues:**
- Mix of camelCase and PascalCase for private fields
- Constants not following PascalCase convention
- Inconsistent access modifiers for readonly fields

**After:**
```csharp
public class AsyncShaderCompiler 
{
    private readonly ConcurrentDictionary<ShaderKey, ShaderProgram> _shaderCache;
    private readonly TaskFactory _compilationTaskFactory;
    private readonly int _maxConcurrentCompilations;
    private readonly Queue<CompilationRequest> _pendingCompilations;
    public const int DefaultMaxConcurrentCompilations = 2;
}
```

**Fixed Issues:**
- All private fields use `_camelCase` pattern
- Constants use PascalCase
- Readonly fields marked as `readonly`

---

## Migration Scenarios

### 15. Large-Scale Migration

**Challenge:** Migrating a large codebase with hundreds of files

**Strategy:**
1. **Analysis Phase**
   ```bash
   # Run analysis to get baseline
   dotnet run --project Tools/NamingConventionChecker -- \
       --solution-path TiXL.sln --action analyze --output-format json > baseline.json
   ```

2. **Prioritization**
   - Focus on public APIs first (classes, interfaces, public methods)
   - Address critical violations that affect compilation
   - Fix high-impact areas (core modules, interfaces)

3. **Incremental Fixes**
   ```bash
   # Fix one module at a time
   dotnet run --project Tools/NamingConventionChecker -- \
       --solution-path TiXL.sln --project-pattern "TiXL.Core.*" --action fix --apply
   ```

4. **Verification**
   - Run tests after each module migration
   - Check for breaking changes in public APIs
   - Update documentation and examples

---

### 16. Backwards Compatibility

**Challenge:** Maintaining backwards compatibility while fixing naming

**Approach:**
1. **Gradual Migration**
   - Add new properly named members alongside old ones
   - Mark old members as `[Obsolete]`
   - Provide migration path in documentation

2. **Alias Patterns**
   ```csharp
   [Obsolete("Use ShaderCompiler instead")]
   public class AsyncShaderCompiler : IShaderCompiler 
   {
       // Implementation
   }
   
   // Prefer this:
   public class ShaderCompiler : IShaderCompiler 
   {
       // Same implementation
   }
   ```

3. **Build Warnings**
   - Use `#pragma warning disable` temporarily for known violations
   - Document technical debt in TODO comments
   - Create tracking issues for remaining violations

---

### 17. Public API Migration

**Challenge:** Changing public API names without breaking existing code

**Solution:** Deprecation Strategy
```csharp
/// <summary>
/// Shader compilation system with intelligent caching
/// </summary>
/// <remarks>
/// This class has been renamed to <see cref="ShaderCompiler"/>.
/// This alias will be removed in a future version.
/// </remarks>
[Obsolete("Use ShaderCompiler instead")]
public class AsyncShaderCompiler : IShaderCompiler 
{
    private readonly ShaderCompiler _inner;
    
    public AsyncShaderCompiler() 
    {
        _inner = new ShaderCompiler();
    }
    
    // Delegate all calls to inner implementation
    public Task<ShaderProgram> CompileShaderAsync(string shaderName, ShaderMacro[] defines = null) 
    {
        return _inner.CompileShaderAsync(shaderName, defines);
    }
}
```

---

## Best Practices

### 18. Performance-Critical Code

**Rule:** In performance-critical code, naming should remain consistent even for internal structures

**Example:**
```csharp
public class HighPerformanceRenderer 
{
    // Even internal hot-path structures follow naming conventions
    private readonly struct RenderKey 
    {
        public readonly int Priority;
        public readonly int Layer;
        
        public RenderKey(int priority, int layer) 
        {
            Priority = priority;
            Layer = layer;
        }
    }
    
    private readonly ConcurrentQueue<RenderCommand> _renderQueue;
    
    public void AddRenderCommand(RenderCommand command) 
    {
        _renderQueue.Enqueue(command);
    }
}
```

**Rationale:**
- Consistent naming improves code maintainability even in performance-critical paths
- The performance impact of naming conventions is negligible
- Better developer experience and reduced cognitive load

---

### 19. Domain-Specific Abbreviations

**Rule:** Only use well-known abbreviations that improve readability

**Acceptable in TiXL context:**
```csharp
// Graphics-related (commonly abbreviated)
public class GfxContext 
public interface IFrame 
public int Fps { get; } // Frames per second

// Mathematical (standard abbreviations)
public struct Vector2 
public struct Vector3 
public struct Matrix4 

// Time (standard units)
public double FrameTimeMs { get; }
public TimeSpan ProcessingTime { get; }
```

**Avoid:**
```csharp
// Too abbreviated
public class PerfMonitor 
public class MathNode 
public int AvgCalcTime { get; }
```

---

### 20. Test Naming

**Rule:** Test method names should be descriptive and follow naming conventions

**Before:**
```csharp
[Test]
public void test_shader_compilation() 
{
    // Test implementation
}

[Test]
public void test_addition_operator() 
{
    // Test implementation
}
```

**After:**
```csharp
[Test]
public void ShaderCompilation_Success_ReturnsCompiledProgram() 
{
    // Test implementation
}

[Test]
public void AdditionOperator_TwoIntegers_ReturnsSum() 
{
    // Test implementation
}
```

**Pattern:** `MethodName_Condition_ExpectedResult`

**Benefits:**
- Test names are self-documenting
- Failures are easier to understand
- Follows .NET testing best practices

---

### 21. Configuration and Constants

**Rule:** Group related constants in well-named classes

**Before:**
```csharp
public class ShaderCompiler 
{
    public const int DEFAULT_TIMEOUT = 30;
    public const int MAX_CONCURRENT_COMPILATIONS = 2;
    public const string DEFAULT_SHADER_MODEL = "6.0";
    public const bool ENABLE_DEBUG_LOGGING = false;
}
```

**After:**
```csharp
public static class ShaderDefaults 
{
    public const int DefaultTimeoutSeconds = 30;
    public const int MaxConcurrentCompilations = 2;
    public const string DefaultShaderModel = "6.0";
    public const bool EnableDebugLogging = false;
}

public static class ShaderLimits 
{
    public const long MaxShaderCacheSize = 1024 * 1024 * 1024; // 1GB
    public const int MaxShaderMacros = 64;
}
```

**Benefits:**
- Constants are organized by purpose
- Names clearly indicate their use and type
- Easier to find related configuration values

---

## Conclusion

Consistent naming conventions are crucial for maintainable, professional code. The TiXL naming conventions ensure that:

1. **Readability** - Code is easy to understand at a glance
2. **Maintainability** - Consistent patterns reduce cognitive load
3. **Professionalism** - Code meets enterprise development standards
4. **Tooling** - IDE features work optimally with consistent naming
5. **Team Collaboration** - Shared vocabulary and patterns

These examples demonstrate the practical application of TiXL naming conventions across various scenarios, providing a reference for developers working on the TiXL codebase.