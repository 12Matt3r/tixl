# TiXL Operator API Reference (TIXL-068)

## Overview

The TiXL Operator API Reference provides comprehensive documentation for all operators available in the TiXL visual programming environment. This living document serves as the definitive guide for developers and users working with TiXL operators.

**Document Version**: 1.0  
**Last Updated**: 2025-11-02  
**Maintenance Status**: Active  

---

## Table of Contents

### Core Framework
- [Operator System Architecture](#operator-system-architecture)
- [Instance Management](#instance-management)
- [Connection System](#connection-system)
- [Evaluation Context](#evaluation-context)
- [Guardrail System](#guardrail-system)

### Operator Categories
- [Mathematical Operators](#mathematical-operators)
- [Graphics & Rendering](#graphics--rendering)
- [Audio Processing](#audio-processing)
- [Data Manipulation](#data-manipulation)
- [Resource Management](#resource-management)
- [UI & Interaction](#ui--interaction)
- [File I/O Operations](#file-io-operations)
- [Control Flow](#control-flow)

### Development Guide
- [Creating Custom Operators](#creating-custom-operators)
- [Best Practices](#best-practices)
- [Performance Guidelines](#performance-guidelines)
- [Testing Operators](#testing-operators)

### Search & Navigation
- [Operator Search](#operator-search)
- [Cross-References](#cross-references)
- [Usage Examples](#usage-examples)

---

## Operator System Architecture

### Overview

TiXL operators are the fundamental building blocks of visual programming within the TiXL environment. Each operator represents a specific function or transformation that can be connected to other operators to create complex visual programs through a node-based graph interface.

### Core Components

#### 1. Symbol (Operator Definition)
```csharp
/// <summary>
/// Blueprint for an operator definition within the TiXL system.
/// Defines the structural composition, inputs, outputs, and internal connections.
/// </summary>
/// <remarks>
/// <para>Key characteristics:</para>
/// <para>• Sealed partial class for foundational operator definition</para>
/// <para>• Implements IDisposable and IResource for lifecycle management</para>
/// <para>• Supports composite design pattern with nested child operators</para>
/// <para>• Manages multiple runtime Instances with change propagation</para>
/// <para>• Dynamic type integration via reflection to concrete C# implementations</para>
/// </remarks>
public sealed partial class Symbol : IDisposable, IResource
```

**Key Features:**
- **Composite Design Pattern**: Operators can contain other operators as children
- **Instance Management**: Creates and manages multiple runtime instances
- **Dynamic Type Integration**: Links to concrete C# operator implementations
- **Connection Management**: Handles data flow between operator slots
- **Resource Integration**: Supports resource packages and file dependencies

#### 2. Instance (Operator Runtime)
```csharp
/// <summary>
/// Runtime representation of an operator instance within the visual programming graph.
/// Links a Symbol.Child (operator definition) to concrete execution context.
/// </summary>
/// <remarks>
/// <para>Core responsibilities:</para>
/// <para>• Manages operator lifecycle (initialization, execution, disposal)</para>
/// <para>• Handles hierarchical positioning within operator graphs</para>
/// <para>• Facilitates data flow through Inputs and Outputs</para>
/// <para>• Integrates with guardrail system for safe execution</para>
/// <para>• Provides resource management and connection handling</para>
/// </remarks>
public abstract partial class Instance : IDisposable
```

**Runtime Behavior:**
- **Lifecycle Management**: Initialize → Execute → Dispose
- **Data Flow**: Connects to other instances via ISlot interfaces
- **State Tracking**: Uses InstanceStatus flags for state management
- **Resource Management**: Implements IResourceConsumer for resource access
- **Error Handling**: Integrated with guardrail and error boundary systems

#### 3. ISlot Interface (Data Connection Points)
```csharp
/// <summary>
/// Core interface for data connection points between operators.
/// Provides the mechanism for data flow and type-specific connections.
/// </summary>
public interface ISlot
```

**Slot Types:**
- **Input Slots**: Accept data from other operators
- **Output Slots**: Provide data to connected operators
- **Multi-Input Slots**: Support multiple input connections
- **Typed Slots**: Enforce type safety for connected data

---

## Instance Management

### Instance Lifecycle

#### 1. Initialization
```csharp
/// <summary>
/// Initializes the operator instance, setting up input/output slots,
/// establishing connections, and preparing for execution.
/// </summary>
/// <remarks>
/// <para>Initialization sequence:</para>
/// <para>1. Sort input slots by definition order</para>
/// <para>2. Mark resource folders as dirty for resource loading</para>
/// <para>3. Recursively initialize child instances</para>
/// <para>4. Reconnect parent if needed</para>
/// <para>5. Set InstanceStatus.Initialized flag</para>
/// </remarks>
public void Initialize()
```

#### 2. Execution
```csharp
/// <summary>
/// Executes the operator with guardrail protection and context validation.
/// </summary>
/// <param name="operationName">Name of the operation being executed</param>
/// <param name="operation">The operation to execute</param>
/// <returns>Result of the operation</returns>
protected T ExecuteGuarded<T>(string operationName, Func<T> operation)
```

#### 3. Disposal
```csharp
/// <summary>
/// Gracefully disposes the instance, cleaning up resources and connections.
/// </summary>
public void Dispose()
```

### State Management

#### InstanceStatus Flags
```csharp
[Flags]
public enum InstanceStatus
{
    None = 0,
    ResourceFoldersDirty = 1 << 0,
    Initialized = 1 << 1,
    Active = 1 << 2,
    ConnectedInternally = 1 << 3,
    IsReconnecting = 1 << 4,
    Bypassed = 1 << 5,
    Disposed = 1 << 6
}
```

---

## Connection System

### Connection Management

#### Creating Connections
```csharp
/// <summary>
/// Attempts to establish a connection between a source slot and target slot
/// based on a Symbol.Connection definition.
/// </summary>
/// <param name="connection">Connection definition with source and target information</param>
/// <returns>True if connection was successful, false otherwise</returns>
public bool TryAddConnection(Symbol.Connection connection)
```

#### Connection Types
- **Direct Connections**: Simple input → output connections
- **Multi-Input Connections**: Multiple outputs → single input
- **Bypass Connections**: Direct pass-through when operator is disabled
- **Resource Connections**: Resource package dependencies

#### Data Flow Patterns
```csharp
/// <summary>
/// Supports various data flow patterns in the operator graph:
/// </summary>
/// <remarks>
/// <para>1. Sequential Processing: A → B → C</para>
/// <para>2. Parallel Processing: A → [B, C] (multi-input)</para>
/// <para>3. Feedback Loops: A → B → C → A</para>
/// <para>4. Conditional Flow: A → [B ? C : D]</para>
/// <para>5. Resource Sharing: [A, B] → Resource</para>
/// </remarks>
```

---

## Evaluation Context

### Context Management

```csharp
/// <summary>
/// Enhanced EvaluationContext with comprehensive guardrails for safe operator execution.
/// Prevents runaway evaluations, resource exhaustion, and infinite loops.
/// </summary>
public class EvaluationContext : IDisposable
```

#### Core Properties
- **RenderingEngine**: Access to graphics rendering capabilities
- **AudioEngine**: Audio processing and playback functionality
- **ResourceManager**: Resource loading and management system
- **Logger**: Structured logging for debugging and monitoring
- **CancellationToken**: Graceful cancellation support

#### Guardrail Protection
```csharp
/// <summary>
/// Executes an action with automatic guardrail protection and error boundaries.
/// </summary>
/// <param name="operationName">Descriptive name for the operation</param>
/// <param name="action">Action to execute with protection</param>
public void ExecuteWithGuardrails(string operationName, Action action)
```

---

## Guardrail System

### Protection Mechanisms

#### Resource Limits
- **Memory Limits**: Prevents excessive memory allocation
- **Time Limits**: Prevents infinite loops and runaway calculations
- **Operation Limits**: Caps maximum operations per evaluation cycle

#### Error Boundaries
```csharp
/// <summary>
/// Executes an action with comprehensive error boundary protection.
/// Returns success/failure without throwing exceptions.
/// </summary>
public bool TryExecuteWithErrorBoundary(
    string operationName, 
    Action action, 
    out Exception? exception)
```

#### Performance Monitoring
```csharp
/// <summary>
/// Records performance metrics for operator execution analysis.
/// </summary>
public void RecordMetric(string metricName, double value, string unit = "")
```

---

## Operator Categories

### Mathematical Operators

#### Overview
Mathematical operators provide computational capabilities including arithmetic, trigonometric, and vector operations.

#### Common Patterns
- **Vector Operations**: Transform, normalize, dot product
- **Scalar Math**: Arithmetic, logarithmic, trigonometric
- **Matrix Operations**: Multiplication, inversion, transformation
- **Random Generation**: Pseudo-random number generation

### Graphics & Rendering

#### Overview
Graphics operators handle visual processing, rendering, and texture manipulation.

#### Common Operations
- **Texture Processing**: Filtering, sampling, transformation
- **Material Operations**: Shader parameters, material properties
- **Geometry Processing**: Mesh operations, vertex manipulation
- **Lighting**: Light sources, shadows, illumination models

### Audio Processing

#### Overview
Audio operators provide sound synthesis, effects, and real-time audio processing.

#### Capabilities
- **Synthesis**: Oscillators, noise generation, sample playback
- **Effects**: Filters, delay, reverb, distortion
- **Analysis**: FFT, spectral analysis, peak detection
- **Routing**: Mixing, panning, level control

### Data Manipulation

#### Overview
Data operators handle information processing, transformation, and storage.

#### Functions
- **Type Conversion**: Format transformation, serialization
- **Data Structuring**: Arrays, objects, property access
- **Logic Operations**: Conditional logic, comparison, boolean operations
- **Aggregation**: Summation, averaging, statistical operations

### Resource Management

#### Overview
Resource operators manage external dependencies including files, textures, and configurations.

#### Resource Types
- **File I/O**: Reading, writing, file system operations
- **Texture Loading**: Image formats, texture formats, compression
- **Configuration**: Settings, parameters, preferences
- **Network Operations**: HTTP requests, data fetching

### UI & Interaction

#### Overview
User interface operators provide interaction capabilities and visual feedback.

#### Interaction Types
- **Input Handling**: Mouse, keyboard, touch, MIDI
- **Visual Feedback**: Display, indicators, status updates
- **User Controls**: Sliders, buttons, selectors, parameter inputs
- **Layout Management**: Positioning, sizing, organization

### File I/O Operations

#### Overview
File I/O operators provide safe and secure file system access with built-in protections.

#### Safety Features
- **Path Validation**: Prevent directory traversal attacks
- **Size Limits**: Prevent excessive file operations
- **Format Validation**: Ensure safe file formats
- **Error Recovery**: Graceful handling of I/O failures

### Control Flow

#### Overview
Control flow operators manage the execution sequence and conditional logic within operator graphs.

#### Control Structures
- **Conditional Execution**: If/then/else logic
- **Loop Operations**: Iteration and recursion
- **State Management**: Persistent variables and state tracking
- **Event Handling**: Triggered execution and callbacks

---

## Creating Custom Operators

### Development Process

#### 1. Define the Operator Class
```csharp
/// <summary>
/// Custom operator example demonstrating TiXL operator patterns.
/// </summary>
/// <remarks>
/// <para>Key requirements for custom operators:</para>
/// <para>• Inherit from appropriate base class</para>
/// <para>• Define input/output slots with proper types</para>
/// <para>• Implement evaluation logic with guardrail protection</para>
/// <para>• Handle resource management and disposal</para>
/// <para>• Provide comprehensive documentation</para>
/// </remarks>
public class CustomOperator : GuardrailedOperator
{
    // Operator implementation
}
```

#### 2. Define Input/Output Slots
```csharp
/// <summary>
/// Define operator interface with typed input and output slots.
/// </summary>
[InputSlot("Input", Type = typeof(float))]
[OutputSlot("Output", Type = typeof(float))]
public class MathOperator : GuardrailedOperator
{
    // Input/Output slot definitions
}
```

#### 3. Implement Evaluation Logic
```csharp
/// <summary>
/// Main evaluation method with guardrail protection.
/// </summary>
protected override float Evaluate(float input)
{
    return ExecuteGuarded("MathOperation", () =>
    {
        // Implementation with resource tracking
        TrackResource("Calculation", 1024);
        RecordMetric("Operations", 1.0);
        
        // Actual computation logic
        return ProcessInput(input);
    });
}
```

#### 4. Resource Management
```csharp
/// <summary>
/// Proper resource management and disposal pattern.
/// </summary>
protected override void DisposeManagedResources()
{
    // Release managed resources
    _computationContext?.Dispose();
    
    // Clean up temporary files
    _temporaryResources.Clear();
}
```

---

## Best Practices

### Operator Design Guidelines

#### 1. Performance Optimization
- **Lazy Evaluation**: Only compute when inputs change
- **Caching**: Cache expensive computations
- **Resource Pooling**: Reuse resources when possible
- **Streaming**: Process large data sets in chunks

#### 2. Error Handling
- **Graceful Degradation**: Provide fallback behavior on errors
- **User Feedback**: Clear error messages and status indicators
- **Recovery Mechanisms**: Automatic retry and error correction
- **Logging**: Comprehensive logging for debugging

#### 3. Memory Management
- **Resource Tracking**: Monitor memory usage with guardrails
- **Disposal Patterns**: Implement proper disposal methods
- **Weak References**: Avoid memory leaks with circular dependencies
- **Large Data Handling**: Use streaming for large datasets

#### 4. Thread Safety
- **Thread Affinity**: Specify threading requirements
- **Synchronization**: Use appropriate locking mechanisms
- **Concurrent Access**: Handle multiple simultaneous requests
- **State Isolation**: Ensure thread-local state independence

### Code Quality Standards

#### Documentation Requirements
- **Comprehensive XML Documentation**: All public APIs documented
- **Usage Examples**: Practical code examples included
- **Cross-References**: Related operators and patterns linked
- **Version Information**: Track API changes and additions

#### Testing Standards
- **Unit Tests**: Comprehensive test coverage for logic
- **Integration Tests**: Test operator interactions
- **Performance Tests**: Benchmark critical operations
- **Error Case Tests**: Validate error handling behavior

---

## Performance Guidelines

### Optimization Strategies

#### 1. Evaluation Optimization
```csharp
/// <summary>
/// Implement dirty flag pattern for efficient re-evaluation.
/// Only recalculate when inputs actually change.
/// </summary>
public class OptimizedOperator : GuardrailedOperator
{
    private bool _inputsDirty = true;
    private float _cachedResult;
    
    public override float Evaluate()
    {
        if (!_inputsDirty)
            return _cachedResult;
            
        return ExecuteGuarded("OptimizedEvaluation", () =>
        {
            _cachedResult = ExpensiveCalculation();
            _inputsDirty = false;
            return _cachedResult;
        });
    }
}
```

#### 2. Memory Management
```csharp
/// <summary>
/// Efficient memory usage patterns.
/// </summary>
public class MemoryEfficientOperator : GuardrailedOperator
{
    private readonly ObjectPool<CalculationContext> _contextPool;
    
    protected override void ExecuteOperation()
    {
        var context = _contextPool.Get();
        try
        {
            // Use context for computation
            ProcessWithContext(context);
        }
        finally
        {
            _contextPool.Return(context);
        }
    }
}
```

#### 3. Parallel Processing
```csharp
/// <summary>
/// Parallel processing for suitable operations.
/// </summary>
public class ParallelOperator : GuardrailedOperator
{
    protected override float[] EvaluateParallel(float[] inputs)
    {
        return ExecuteGuarded("ParallelProcessing", () =>
        {
            var results = new float[inputs.Length];
            Parallel.For(0, inputs.Length, i =>
            {
                results[i] = ProcessInput(inputs[i]);
            });
            return results;
        });
    }
}
```

---

## Testing Operators

### Test Framework Integration

#### 1. Unit Testing
```csharp
[TestFixture]
public class CustomOperatorTests
{
    private EvaluationContext _context;
    private CustomOperator _operator;
    
    [SetUp]
    public void Setup()
    {
        _context = EvaluationContext.CreateForTest();
        _operator = new CustomOperator("TestOperator", _context);
    }
    
    [Test]
    public void Evaluate_WithValidInput_ReturnsExpectedResult()
    {
        // Arrange
        var input = 5.0f;
        var expected = 25.0f;
        
        // Act
        var result = _operator.Evaluate(input);
        
        // Assert
        Assert.That(result, Is.EqualTo(expected).Within(0.001));
    }
    
    [Test]
    public void Evaluate_WithGuardrailProtection_HandlesLargeInputs()
    {
        // Test guardrail behavior with extreme inputs
    }
}
```

#### 2. Integration Testing
```csharp
[TestFixture]
public class OperatorIntegrationTests
{
    [Test]
    public void OperatorGraph_ComplexCalculation_ProducesCorrectResult()
    {
        // Test complete operator graphs
    }
    
    [Test]
    public void ResourceManagement_MultipleOperators_ProperlyCleansUp()
    {
        // Test resource cleanup in complex scenarios
    }
}
```

---

## Operator Search

### Search Integration

#### Searchable Attributes
- **Operator Name**: Primary search term
- **Category Tags**: Functional groupings
- **Input/Output Types**: Data type compatibility
- **Keywords**: Descriptive terms and synonyms
- **Usage Patterns**: Common application scenarios

#### Search API
```csharp
/// <summary>
/// Search interface for finding operators by various criteria.
/// </summary>
public interface IOperatorSearch
{
    IEnumerable<OperatorInfo> SearchOperators(string query);
    IEnumerable<OperatorInfo> SearchByCategory(string category);
    IEnumerable<OperatorInfo> SearchByType(Type inputType, Type outputType);
}
```

### Cross-References

#### Related Operators
- **Similar Functionality**: Operators with overlapping capabilities
- **Common Patterns**: Frequently used combinations
- **Performance Comparisons**: Alternative implementations
- **Upgrade Paths**: Migration from deprecated operators

#### Usage Patterns
- **Basic Usage**: Simple, direct usage examples
- **Advanced Patterns**: Complex, powerful combinations
- **Best Practices**: Recommended usage approaches
- **Common Pitfalls**: Things to avoid

---

## Usage Examples

### Basic Operator Usage

#### Simple Data Flow
```csharp
/// <summary>
/// Basic example showing operator connection and evaluation.
/// </summary>
/// <code>
/// // Create operator instances
/// var inputOperator = new InputOperator("UserInput");
/// var mathOperator = new MathOperator("Calculation");
/// var outputOperator = new OutputOperator("Result");
/// 
/// // Connect operators
/// inputOperator.Output.Connect(mathOperator.Input);
/// mathOperator.Output.Connect(outputOperator.Input);
/// 
/// // Execute graph
/// inputOperator.SetValue(10.0f);
/// var result = outputOperator.Evaluate();
/// // result = 100.0f
/// </code>
```

#### Complex Graph Processing
```csharp
/// <summary>
/// Example of complex operator graph with multiple data paths.
/// </summary>
/// <code>
/// // Create complex processing pipeline
/// var audioInput = new AudioInputOperator("MicInput");
/// var filter = new FilterOperator("LowPass");
/// var analyzer = new SpectrumAnalyzer("FrequencyAnalysis");
/// var visualizer = new AudioVisualizer("Display");
/// 
/// // Set up connections
/// audioInput.Output.Connect(filter.Input);
/// filter.Output.Connect(analyzer.Input);
/// analyzer.SpectrumOutput.Connect(visualizer.SpectrumInput);
/// 
/// // Process in real-time
/// while (applicationRunning)
/// {
///     var spectrum = analyzer.GetSpectrum();
///     visualizer.UpdateDisplay(spectrum);
/// }
/// </code>
```

### Performance Optimization Examples

#### Caching Strategy
```csharp
/// <summary>
/// Demonstrates efficient caching strategy for expensive operations.
/// </summary>
/// <code>
public class CachedImageProcessor : GuardrailedOperator
{
    private readonly Dictionary<string, Texture2D> _textureCache = new();
    
    public Texture2D ProcessImage(string imagePath, ProcessingOptions options)
    {
        var cacheKey = $"{imagePath}_{options.GetHashCode()}";
        
        if (_textureCache.TryGetValue(cacheKey, out var cached))
            return cached;
            
        return ExecuteGuarded("ImageProcessing", () =>
        {
            var texture = LoadAndProcessTexture(imagePath, options);
            TrackResource("Texture", texture.MemorySize);
            
            // Cache with size limit
            if (_textureCache.Count > 100)
            {
                var oldest = _textureCache.First();
                oldest.Value.Dispose();
                _textureCache.Remove(oldest.Key);
            }
            
            _textureCache[cacheKey] = texture;
            return texture;
        });
    }
}
</code>
```

#### Parallel Processing
```csharp
/// <summary>
/// Example of parallel processing for batch operations.
/// </summary>
/// <code>
public class BatchProcessor : GuardrailedOperator
{
    public ProcessingResult[] ProcessBatch(ProcessingTask[] tasks)
    {
        return ExecuteGuarded("BatchProcessing", () =>
        {
            var results = new ConcurrentBag<ProcessingResult>();
            
            Parallel.ForEach(tasks, task =>
            {
                try
                {
                    var result = ProcessSingleTask(task);
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    // Handle individual task failures
                    results.Add(new ProcessingResult { Error = ex.Message });
                }
            });
            
            return results.ToArray();
        });
    }
}
</code>
```

---

## Maintenance Process

### Documentation Maintenance

#### Update Procedures
1. **Code Changes**: Update documentation when modifying operator APIs
2. **New Operators**: Document all new operators immediately
3. **Deprecations**: Mark deprecated operators with migration guidance
4. **Versioning**: Track API changes with version information

#### Quality Assurance
- **Documentation Review**: Regular review of documentation accuracy
- **Example Testing**: Verify all code examples compile and run
- **Cross-Reference Validation**: Ensure links and references are current
- **User Feedback**: Incorporate user questions and issues

### Process Automation

#### Documentation Generation
```csharp
/// <summary>
/// Automated documentation generation from operator metadata.
/// </summary>
public class DocumentationGenerator
{
    public void GenerateOperatorDocs()
    {
        // Scan all operator assemblies
        // Extract metadata and XML documentation
        // Generate reference documentation
        // Build search index
    }
}
```

#### Link Validation
```csharp
/// <summary>
/// Validates cross-references and links in documentation.
/// </summary>
public class LinkValidator
{
    public ValidationResult ValidateLinks()
    {
        // Check internal cross-references
        // Validate external links
        // Report broken references
    }
}
```

### Community Contributions

#### Contribution Guidelines
- **Documentation Standards**: Follow established templates and patterns
- **Review Process**: Submit changes for community review
- **Testing Requirements**: Include tests for new documentation examples
- **Quality Checks**: Pass automated validation before acceptance

#### Contribution Workflow
1. **Fork Documentation**: Create branch for documentation updates
2. **Make Changes**: Update documentation following templates
3. **Test Changes**: Verify examples and cross-references
4. **Submit Pull Request**: Request review and integration
5. **Address Feedback**: Incorporate reviewer comments
6. **Merge**: Integrate approved changes

---

## Version History

| Version | Date | Changes | Author |
|---------|------|---------|--------|
| 1.0 | 2025-11-02 | Initial operator API reference documentation | Documentation Team |

---

## Support and Feedback

### Getting Help
- **Documentation Issues**: Report problems with this reference
- **Operator Questions**: Ask about specific operator functionality
- **Feature Requests**: Suggest new documentation features
- **Bug Reports**: Report inaccuracies or missing information

### Contact Information
- **Documentation Team**: [Contact information]
- **Community Forum**: [Forum URL]
- **GitHub Issues**: [Repository issues URL]
- **Email Support**: [Support email]

---

**Document Status**: Complete  
**Next Review Date**: 2025-12-02  
**Priority**: High  
**Applies To**: TiXL 1.0+