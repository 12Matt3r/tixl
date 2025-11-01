# Architectural Governance

TiXL follows a clean, modular architecture with explicit boundaries between five primary domains. This governance framework ensures maintainability, testability, and extensibility.

## Table of Contents

1. [Architectural Overview](#architectural-overview)
2. [Module Boundaries and Responsibilities](#module-boundaries-and-responsibilities)
3. [Dependency Rules and Restrictions](#dependency-rules-and-restrictions)
4. [Cross-Module Communication Patterns](#cross-module-communication-patterns)
5. [Validation and Enforcement](#validation-and-enforcement)
6. [Best Practices](#best-practices)

## Architectural Overview

### Core Principles

1. **Separation of Concerns**: Each module has clearly defined responsibilities
2. **Dependency Inversion**: High-level modules depend on abstractions, not implementations
3. **Single Responsibility**: Each component has one reason to change
4. **Interface Segregation**: Clients depend only on interfaces they use
5. **Open/Closed**: Extension through interfaces and plugins, not modification

## Module Boundaries and Responsibilities

### 1. Core Module (`TiXL.Core`)

**Primary Responsibilities:**
- Fundamental engine capabilities and data types
- Rendering engine foundation and DirectX 12 integration
- Resource management and disposal patterns
- Mathematical operations and transformations
- Audio/video processing foundations

**Allowed Dependencies:** System, Microsoft packages
**Forbidden Dependencies:** Operators, Gui, Editor, Gfx modules

**Key Interfaces:**
- `IRenderingEngine` - Core rendering capabilities
- `IResourceManager` - Asset management
- `IMathLibrary` - Mathematical operations
- `IAudioProcessor` - Audio processing
- `IPerformanceMonitor` - Performance tracking

### 2. Operators Module (`TiXL.Operators`)

**Primary Responsibilities:**
- Plugin-based operator system and extensibility
- Symbol/Instance separation for operator definitions
- Typed slot system for dataflow connections
- Registry management for operator discovery
- Evaluation context and execution management

**Allowed Dependencies:** Core, System, Microsoft packages
**Forbidden Dependencies:** Gui, Editor, Gfx modules

**Key Interfaces:**
- `ISymbol` - Operator definition interface
- `IInstance` - Runtime execution interface
- `ISlot` - Typed connection interface
- `IOperatorRegistry` - Discovery and management

### 3. Graphics Module (`TiXL.Gfx`)

**Primary Responsibilities:**
- DirectX 12 pipeline implementation
- Shader management and compilation
- Graphics state handling and optimization
- GPU resource management

**Allowed Dependencies:** Core, SharpDX, System, Microsoft packages
**Forbidden Dependencies:** Operators, Gui, Editor modules

**Key Interfaces:**
- `IGraphicsDevice` - Graphics device abstraction
- `IShaderCompiler` - Shader compilation interface
- `IPipelineState` - Graphics state management

### 4. GUI Module (`TiXL.Gui`)

**Primary Responsibilities:**
- User interface components and interactions
- Window management and docking
- Data binding between operators and UI
- Immediate-mode UI implementation

**Allowed Dependencies:** Core, Operators, ImGui.NET, System, Microsoft packages
**Forbidden Dependencies:** Editor, Gfx modules

**Key Interfaces:**
- `IUIComponent` - UI component abstraction
- `IWindowManager` - Window management
- `IOperatorUI` - Operator UI interface

### 5. Editor Module (`TiXL.Editor`)

**Primary Responsibilities:**
- Application orchestration and lifecycle
- Project management and file operations
- Integration coordination between modules
- Development environment features

**Allowed Dependencies:** All modules (integration point)
**Forbidden Dependencies:** None

**Key Interfaces:**
- `IApplication` - Application orchestration
- `IProjectManager` - Project management
- `ICompilationManager` - Build system integration

## Dependency Rules and Restrictions

### Common Architectural Violations

**1. Forbidden Project References**
```xml
<!-- ❌ VIOLATION: Core project referencing Operators -->
<!-- TiXL.Core.csproj -->
<ProjectReference Include="..\Operators\TiXL.Operators.csproj" />

<!-- ✅ CORRECT: Use interfaces instead -->
<!-- Define interface in Core, implement in Operators -->
```

**2. Forbidden Using Statements**
```csharp
// ❌ VIOLATION: Operators importing Gui types
using TiXL.Gui.Components; // Not allowed

// ✅ CORRECT: Use abstractions defined in Operators
using TiXL.Operators.Interfaces;
```

**3. Direct Instantiation Across Modules**
```csharp
// ❌ VIOLATION: Direct dependency on implementation
var renderer = new DirectXRenderer(); // Not allowed in Operators

// ✅ CORRECT: Use dependency injection
var renderer = serviceProvider.GetService<IRenderingEngine>();
```

## Cross-Module Communication Patterns

### 1. Interface-Based Communication

Use interfaces to define contracts between modules:

```csharp
// Core defines abstraction
namespace TiXL.Core
{
    public interface IRenderingService
    {
        void RenderFrame(RenderContext context);
        bool IsInitialized { get; }
    }
}

// Gfx provides implementation
namespace TiXL.Gfx
{
    public class DirectXRenderingService : IRenderingService
    {
        public void RenderFrame(RenderContext context) { /* ... */ }
        public bool IsInitialized { get; private set; }
    }
}
```

### 2. Event-Based Communication

Use events for loose coupling:

```csharp
// Core defines event contracts
namespace TiXL.Core
{
    public class ResourceLoadedEventArgs : EventArgs
    {
        public IResource Resource { get; }
        public string ResourcePath { get; }
    }
    
    public interface IResourceManager
    {
        event EventHandler<ResourceLoadedEventArgs> ResourceLoaded;
        T LoadResource<T>(string path) where T : IResource;
    }
}
```

### 3. Context-Based Communication

Use evaluation contexts for operator execution:

```csharp
// Operators define evaluation context
namespace TiXL.Operators
{
    public class EvaluationContext
    {
        public IRenderingEngine RenderingEngine { get; }
        public IAudioEngine AudioEngine { get; }
        public IResourceManager ResourceManager { get; }
        public ILogger Logger { get; }
        public CancellationToken CancellationToken { get; }
    }
}
```

## Validation and Enforcement

### Automated Tools

**1. Architectural Validator Tool**
```bash
# Build and run the validator
dotnet build Tools/ArchitecturalValidator/TiXL.ArchitecturalValidator.csproj
dotnet run --project Tools/ArchitecturalValidator -- /path/to/TiXL.sln
```

**2. Validation Script**
```bash
# Run comprehensive validation
./scripts/validate-architecture.sh validate

# Set up Git hooks for automatic validation
./scripts/validate-architecture.sh setup-hooks

# Generate compliance report
./scripts/validate-architecture.sh generate-report
```

**3. Pre-commit Hooks**
Git hooks automatically validate architectural constraints before each commit. Violations will prevent commits from being created.

### Code Review Checklist for Architecture

**Reviewers must check:**

- [ ] No forbidden dependencies introduced
- [ ] Module boundaries respected
- [ ] Proper use of interfaces and abstractions
- [ ] Namespace structure maintained
- [ ] Cross-module communication follows established patterns
- [ ] No direct instantiation of cross-module implementations
- [ ] Architectural documentation updated if needed

### Common architecture review comments:

**Dependency Violations:**
```csharp
// ❌ Bad: Direct cross-module dependency
public class MyOperator
{
    private readonly DirectXRenderer _renderer; // Not allowed in Operators
}

// ✅ Good: Use abstraction
public class MyOperator
{
    private readonly IRenderingService _renderer; // Interface-based
}
```

## Best Practices

**Do:**
- ✅ Use interfaces for cross-module communication
- ✅ Follow established dependency patterns
- ✅ Keep modules focused on their responsibilities
- ✅ Use events and callbacks for loose coupling
- ✅ Run validation tools before submitting changes

**Don't:**
- ❌ Create circular dependencies
- ❌ Reference forbidden modules
- ❌ Use direct instantiation across modules
- ❌ Bypass established communication patterns
- ❌ Ignore architectural validation warnings

### Getting Help

**Resources:**
- [Architecture Tools README](Tools-Automation/Architecture-Tools)
- Validation script help: `./scripts/validate-architecture.sh help`

**Common solutions:**
- Run `./scripts/validate-architecture.sh validate -v` for detailed output
- Check architectural patterns in existing codebase
- Use IDE refactoring tools to move interfaces
- Consult module-specific guidelines

---

## Continuous Improvement

Architectural governance is an ongoing process:

- **Monthly reviews** of validation reports
- **Quarterly assessment** of architectural health
- **Annual major reviews** and updates to governance rules
- **Regular updates** to tools and documentation

Contributors should stay informed about architectural changes and participate in governance discussions when modules evolve.
