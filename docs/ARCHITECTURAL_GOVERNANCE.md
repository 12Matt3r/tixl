# TiXL Architectural Governance Documentation

## Table of Contents

1. [Architectural Overview](#architectural-overview)
2. [Module Boundaries and Responsibilities](#module-boundaries-and-responsibilities)
3. [Dependency Rules and Restrictions](#dependency-rules-and-restrictions)
4. [Cross-Module Communication Patterns](#cross-module-communication-patterns)
5. [Static Analysis and Enforcement](#static-analysis-and-enforcement)
6. [Code Review Checklists](#code-review-checklists)
7. [Governance Implementation](#governance-implementation)

## Architectural Overview

TiXL follows a clean, modular architecture with explicit boundaries between five primary domains. This governance framework ensures maintainability, testability, and extensibility while preventing architectural drift.

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
- Performance monitoring and metrics

**IS Allowed To:**
- Define core interfaces and abstractions
- Manage GPU resources (textures, buffers, shaders)
- Implement mathematical libraries and data types
- Handle file I/O and resource loading
- Provide performance monitoring utilities
- Define exception types and error handling patterns

**IS NOT Allowed To:**
- Depend on Operator types or instances
- Reference Editor or Gui components directly
- Access UI-specific concepts or controls
- Import external plugin systems
- Know about application-specific workflows

**Key Classes and Interfaces:**
```csharp
// Core public API
namespace TiXL.Core
{
    public interface IRenderingEngine { }
    public interface IResourceManager { }
    public interface IMathLibrary { }
    public interface IAudioProcessor { }
    public interface IVideoProcessor { }
    public interface IPerformanceMonitor { }
    
    // Exception types
    public class TiXLException : Exception { }
    public class RenderingException : TiXLException { }
    public class ResourceException : TiXLException { }
}
```

### 2. Operators Module (`TiXL.Operators`)

**Primary Responsibilities:**
- Plugin-based operator system and extensibility
- Symbol/Instance separation for operator definitions
- Typed slot system for dataflow connections
- Registry management for operator discovery
- Evaluation context and execution management

**IS Allowed To:**
- Define operator interfaces and abstract base classes
- Implement concrete operator implementations
- Manage symbol registry and lifecycle
- Handle evaluation context and execution
- Provide operator metadata and attributes
- Define slot types and connection patterns

**IS NOT Allowed To:**
- Depend on Core rendering implementations
- Reference Editor or Gui components
- Access UI-specific concepts or controls
- Import external graphics libraries
- Know about application-specific workflows

**Key Classes and Interfaces:**
```csharp
// Operators public API
namespace TiXL.Operators
{
    public interface IOperator { }
    public interface ISymbol { }
    public interface IInstance { }
    public interface ISlot { }
    public interface IOperatorRegistry { }
    public interface IEvaluationContext { }
    
    // Attribute types
    [AttributeUsage(AttributeTargets.Class)]
    public class OperatorAttribute : Attribute { }
    
    [AttributeUsage(AttributeTargets.Property)]
    public class InputSlotAttribute : Attribute { }
    
    [AttributeUsage(AttributeTargets.Property)]
    public class OutputSlotAttribute : Attribute { }
}
```

### 3. Gfx Module (`TiXL.Gfx`)

**Primary Responsibilities:**
- DirectX 12 pipeline management and configuration
- Shader compilation and optimization
- Buffer management and state handling
- Render target and texture management
- Graphics-specific utility functions

**IS Allowed To:**
- Implement DirectX 12 specific functionality
- Manage graphics pipeline states
- Handle shader compilation and optimization
- Create and manage graphics resources
- Define graphics utility functions
- Provide graphics performance optimizations

**IS NOT Allowed To:**
- Depend on Editor or Gui components
- Access UI-specific concepts or controls
- Reference application-specific workflows
- Import operator or UI types

**Key Classes and Interfaces:**
```csharp
// Graphics public API
namespace TiXL.Gfx
{
    public interface IGraphicsDevice { }
    public interface IShaderCompiler { }
    public interface IPipelineState { }
    public interface IRenderTarget { }
    public interface IBufferManager { }
    
    // Graphics types
    public struct ShaderCompileResult { }
    public struct RenderStateConfig { }
}
```

### 4. Editor Module (`TiXL.Editor`)

**Primary Responsibilities:**
- Application orchestration and lifecycle management
- Project management and serialization
- Compilation workflows and build processes
- Crash reporting and diagnostics
- Integration point for all subsystems

**IS Allowed To:**
- Orchestrate application startup and shutdown
- Manage project files and serialization
- Coordinate between Core, Operators, Gfx, and Gui
- Provide crash reporting and diagnostics
- Implement compilation workflows
- Handle application configuration and settings

**IS NOT Allowed To:**
- Bypass established interfaces between modules
- Access internal implementation details of other modules
- Create circular dependencies between subsystems

**Key Classes and Interfaces:**
```csharp
// Editor public API
namespace TiXL.Editor
{
    public interface IApplication { }
    public interface IProjectManager { }
    public interface ICompilationManager { }
    public interface ICrashReporter { }
    public interface ISerializationManager { }
}
```

### 5. Gui Module (`TiXL.Gui`)

**Primary Responsibilities:**
- Immediate-mode UI framework (ImGui-based)
- User interface components and controls
- Window management and layout
- Input handling and interaction
- Operator UI integration and data binding

**IS Allowed To:**
- Implement ImGui-based UI components
- Manage window lifecycle and docking
- Handle user input and interactions
- Provide UI styling and theming
- Bind UI data to operator slots
- Implement dialogs and modal workflows

**IS NOT Allowed To:**
- Depend on Core rendering implementation details
- Access operator internal structure directly
- Bypass the evaluation context system
- Create hard dependencies on specific operator types

**Key Classes and Interfaces:**
```csharp
// GUI public API
namespace TiXL.Gui
{
    public interface IUIComponent { }
    public interface IWindowManager { }
    public interface IInputHandler { }
    public interface IOperatorUI { }
    public interface IDataBinder { }
    public interface IStylingEngine { }
}
```

## Dependency Rules and Restrictions

### Allowed Dependencies (A → B means A depends on B)

```mermaid
graph TD
    Editor --> Core
    Editor --> Operators
    Editor --> Gfx
    Editor --> Gui
    
    Gui --> Core
    Gui --> Operators
    
    Gfx --> Core
    
    Operators --> Core
    
    Core --> [Independent]
```

### Dependency Rules Matrix

| From/To | Core | Operators | Gfx | Editor | Gui |
|---------|------|-----------|-----|--------|-----|
| Core | - | ❌ | ❌ | ❌ | ❌ |
| Operators | ✅ | - | ❌ | ❌ | ❌ |
| Gfx | ✅ | ❌ | - | ❌ | ❌ |
| Editor | ✅ | ✅ | ✅ | - | ✅ |
| Gui | ✅ | ✅ | ❌ | ❌ | - |

### Forbidden Dependencies

1. **Core → Operators**: Core must never depend on operator-specific concepts
2. **Core → Gui**: Core must not know about UI implementations
3. **Core → Editor**: Core should not depend on application logic
4. **Gfx → Operators**: Graphics should not know about operator systems
5. **Gfx → Gui**: Graphics should not depend on UI frameworks
6. **Operators → Gui**: Operators should not depend on UI implementations
7. **Operators → Gfx**: Operators should work with abstraction layers, not implementations

### Import Restrictions by Module

#### Core Module
```xml
<!-- ALLOWED -->
<PackageReference Include="System.Numerics.Vectors" />
<PackageReference Include="Microsoft.Extensions.Logging" />

<!-- FORBIDDEN -->
<PackageReference Include="ImGui.NET" />
<PackageReference Include="Silk.NET" />
<PackageReference Include="TiXL.Operators" />
<PackageReference Include="TiXL.Gui" />
```

#### Operators Module
```xml
<!-- ALLOWED -->
<PackageReference Include="TiXL.Core" />
<PackageReference Include="System.ComponentModel" />

<!-- FORBIDDEN -->
<PackageReference Include="ImGui.NET" />
<PackageReference Include="TiXL.Gfx" />
<PackageReference Include="TiXL.Editor" />
<PackageReference Include="TiXL.Gui" />
```

#### Gfx Module
```xml
<!-- ALLOWED -->
<PackageReference Include="TiXL.Core" />
<PackageReference Include="SharpDX" />
<PackageReference Include="Microsoft.Extensions.Logging" />

<!-- FORBIDDEN -->
<PackageReference Include="TiXL.Operators" />
<PackageReference Include="TiXL.Gui" />
<PackageReference Include="TiXL.Editor" />
```

#### Editor Module
```xml
<!-- ALLOWED (all modules) -->
<PackageReference Include="TiXL.Core" />
<PackageReference Include="TiXL.Operators" />
<PackageReference Include="TiXL.Gfx" />
<PackageReference Include="TiXL.Gui" />
```

#### Gui Module
```xml
<!-- ALLOWED -->
<PackageReference Include="TiXL.Core" />
<PackageReference Include="TiXL.Operators" />
<PackageReference Include="ImGui.NET" />

<!-- FORBIDDEN -->
<PackageReference Include="TiXL.Gfx" />
<PackageReference Include="TiXL.Editor" />
```

## Cross-Module Communication Patterns

### 1. Interface-Based Communication

All inter-module communication must use interfaces to maintain loose coupling:

```csharp
// Core defines abstraction
namespace TiXL.Core
{
    public interface IRenderingService
    {
        void RenderFrame(RenderContext context);
    }
}

// Gfx provides implementation
namespace TiXL.Gfx
{
    public class DirectXRenderingService : IRenderingService
    {
        public void RenderFrame(RenderContext context) { }
    }
}

// Editor wires them together
namespace TiXL.Editor
{
    public class Application
    {
        private readonly IRenderingService _renderingService;
        
        public Application(IRenderingService renderingService)
        {
            _renderingService = renderingService;
        }
    }
}
```

### 2. Event-Based Communication

Use events and callbacks for loose coupling:

```csharp
// Core defines event contracts
namespace TiXL.Core
{
    public class RenderingEventArgs : EventArgs
    {
        public RenderTarget Target { get; }
        public Camera Camera { get; }
    }
    
    public interface IRenderingEngine
    {
        event EventHandler<RenderingEventArgs> Rendering;
    }
}

// Operators subscribe to events
namespace TiXL.Operators
{
    public class RenderOperator : Symbol
    {
        public RenderOperator(IRenderingEngine renderingEngine)
        {
            renderingEngine.Rendering += OnRendering;
        }
        
        private void OnRendering(object sender, RenderingEventArgs e) { }
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
    }
    
    public interface IOperator
    {
        void Evaluate(EvaluationContext context);
    }
}
```

### 4. Plugin System Communication

Use plugins for extensibility:

```csharp
// Operators define plugin interface
namespace TiXL.Operators
{
    public interface IOperatorPlugin
    {
        IEnumerable<ISymbol> CreateOperators();
        string Name { get; }
        Version Version { get; }
    }
}
```

## Static Analysis and Enforcement

### 1. Architectural Constraint Rules

#### Microsoft FxCop Rules Configuration

```xml
<?xml version="1.0" encoding="utf-8"?>
<RuleSet Name="TiXL Architectural Constraints" Description="Enforce architectural boundaries" ToolsVersion="17.0">
  
  <!-- Prevent circular dependencies -->
  <Rules AnalyzerId="Microsoft.Analyzers.ManagedCodeAnalysis" RuleNamespace="Microsoft.Rules.Managed">
    <Rule Id="CA2009" Action="Error" /> <!-- Don't call ToImmutableArray on ImmutableArray -->
    
    <!-- Custom architectural rules would go here -->
    <Rule Id="CA1000" Action="Error" /> <!-- Don't declare static members on generic types -->
    <Rule Id="CA1060" Action="Error" /> <!-- Move P/Invokes to native methods class -->
    <Rule Id="CA2210" Action="Error" /> <!-- Assemblies should have valid strong names -->
  </Rules>
  
  <!-- Security rules -->
  <Rules AnalyzerId="Microsoft.Analyzers.ManagedCodeAnalysis" RuleNamespace="Microsoft.Rules.Security">
    <Rule Id="CA2100" Action="Error" /> <!-- Review SQL queries for security vulnerabilities -->
    <Rule Id="CA2114" Action="Error" /> <!-- Method should be callable only through base interfaces -->
    <Rule Id="CA2130" Action="Error" /> <!-- Security critical types should be security critical -->
  </Rules>
  
</RuleSet>
```

#### Roslyn Analyzers for Architecture

Create custom analyzers to enforce module boundaries:

```csharp
// Architectural constraint analyzer
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ModuleDependencyAnalyzer : DiagnosticAnalyzer
{
    private const string DiagnosticId = "TiXL001";
    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Module Dependency Violation",
        "Module '{0}' cannot depend on '{1}'",
        "Architecture",
        DiagnosticSeverity.Error,
        true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => 
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context)
    {
        var namedType = (INamedTypeSymbol)context.Symbol;
        
        // Check for forbidden dependencies
        var forbiddenDependencies = GetForbiddenDependencies(namedType.ContainingNamespace?.Name);
        
        foreach (var dependency in forbiddenDependencies)
        {
            if (HasDependency(namedType, dependency))
            {
                var diagnostic = Diagnostic.Create(Rule, namedType.Locations[0], 
                    namedType.ContainingNamespace?.Name, dependency);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static IEnumerable<string> GetForbiddenDependencies(string moduleName)
    {
        // Return forbidden dependencies for each module
        return moduleName switch
        {
            "TiXL.Core" => new[] { "TiXL.Operators", "TiXL.Gui", "TiXL.Editor" },
            "TiXL.Operators" => new[] { "TiXL.Gui", "TiXL.Gfx", "TiXL.Editor" },
            "TiXL.Gfx" => new[] { "TiXL.Operators", "TiXL.Gui", "TiXL.Editor" },
            "TiXL.Gui" => new[] { "TiXL.Gfx", "TiXL.Editor" },
            _ => Enumerable.Empty<string>()
        };
    }
}
```

### 2. Project File Constraints

#### Directory.Build.props Enforcement

```xml
<!-- /workspace/Directory.Build.props -->
<Project>
  <PropertyGroup>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsAsErrors />
    <WarningsNotAsErrors>CS1591</WarningsNotAsErrors> <!-- Allow missing XML docs in some cases -->
  </PropertyGroup>

  <!-- Core module constraints -->
  <PropertyGroup Condition="'$(MSBuildProjectName)' == 'TiXL.Core'">
    <AllowedProjectReference Include="System.*" />
    <AllowedProjectReference Include="Microsoft.*" />
    <ForbiddenProjectReference Include="TiXL.Operators" />
    <ForbiddenProjectReference Include="TiXL.Gui" />
    <ForbiddenProjectReference Include="TiXL.Gfx" />
    <ForbiddenProjectReference Include="TiXL.Editor" />
  </PropertyGroup>

  <!-- Operators module constraints -->
  <PropertyGroup Condition="'$(MSBuildProjectName)' == 'TiXL.Operators'">
    <AllowedProjectReference Include="TiXL.Core" />
    <AllowedProjectReference Include="System.*" />
    <ForbiddenProjectReference Include="TiXL.Gui" />
    <ForbiddenProjectReference Include="TiXL.Gfx" />
    <ForbiddenProjectReference Include="TiXL.Editor" />
  </PropertyGroup>

  <!-- Gfx module constraints -->
  <PropertyGroup Condition="'$(MSBuildProjectName)' == 'TiXL.Gfx'">
    <AllowedProjectReference Include="TiXL.Core" />
    <AllowedProjectReference Include="System.*" />
    <ForbiddenProjectReference Include="TiXL.Operators" />
    <ForbiddenProjectReference Include="TiXL.Gui" />
    <ForbiddenProjectReference Include="TiXL.Editor" />
  </PropertyGroup>

  <!-- Gui module constraints -->
  <PropertyGroup Condition="'$(MSBuildProjectName)' == 'TiXL.Gui'">
    <AllowedProjectReference Include="TiXL.Core" />
    <AllowedProjectReference Include="TiXL.Operators" />
    <AllowedProjectReference Include="System.*" />
    <ForbiddenProjectReference Include="TiXL.Gfx" />
    <ForbiddenProjectReference Include="TiXL.Editor" />
  </PropertyGroup>

  <!-- Editor module constraints (can reference all) -->
  <PropertyGroup Condition="'$(MSBuildProjectName)' == 'TiXL.Editor'">
    <AllowedProjectReference Include="TiXL.Core" />
    <AllowedProjectReference Include="TiXL.Operators" />
    <AllowedProjectReference Include="TiXL.Gfx" />
    <AllowedProjectReference Include="TiXL.Gui" />
    <AllowedProjectReference Include="System.*" />
  </PropertyGroup>
</Project>
```

### 3. Git Hooks for Enforcement

#### Pre-commit Hook

```yaml
# .githooks/pre-commit
#!/bin/bash

# Run architectural constraint check
dotnet build --configuration Release --verbosity quiet

if [ $? -ne 0 ]; then
    echo "Build failed. Please fix architectural violations."
    exit 1
fi

# Check for forbidden dependencies
forbidden_patterns=(
    "TiXL\.Core.*TiXL\.Operators"
    "TiXL\.Core.*TiXL\.Gui"
    "TiXL\.Operators.*TiXL\.Gui"
    "TiXL\.Gfx.*TiXL\.Operators"
)

for pattern in "${forbidden_patterns[@]}"; do
    if grep -r "using.*$pattern" src/; then
        echo "Forbidden dependency found: $pattern"
        echo "Please review module boundaries."
        exit 1
    fi
done
```

## Code Review Checklists

### Module-Specific Review Criteria

#### Core Module Changes
- [ ] No dependencies on Operators, Gui, Editor, or Gfx modules
- [ ] Proper IDisposable patterns for resource management
- [ ] Thread safety considerations for concurrent access
- [ ] Performance implications of API changes
- [ ] Proper XML documentation for public APIs
- [ ] Memory management patterns follow Core conventions

#### Operators Module Changes
- [ ] Symbol/Instance separation is maintained
- [ ] No direct dependencies on Gui or Gfx implementations
- [ ] Slot types are properly defined and typed
- [ ] Evaluation context usage is correct
- [ ] Plugin registration patterns are followed
- [ ] Operator metadata is comprehensive

#### Gfx Module Changes
- [ ] DirectX 12 best practices are followed
- [ ] No dependencies on Operator or Gui types
- [ ] Proper GPU resource disposal patterns
- [ ] Buffer alignment and layout rules are followed
- [ ] Shader compilation and error handling is robust
- [ ] Performance considerations for real-time rendering

#### Gui Module Changes
- [ ] No direct dependencies on Gfx implementations
- [ ] Immediate-mode patterns are properly implemented
- [ ] Input handling is robust and responsive
- [ ] Data binding follows established patterns
- [ ] Window management is properly handled
- [ ] Performance considerations for UI rendering

#### Editor Module Changes
- [ ] Integration patterns follow established conventions
- [ ] No bypass of module boundaries
- [ ] Crash reporting is comprehensive
- [ ] Project serialization is robust
- [ ] Application lifecycle is properly managed
- [ ] Configuration management is consistent

### Cross-Cutting Concerns Review

#### Architectural Compliance
- [ ] No circular dependencies introduced
- [ ] Module boundaries are respected
- [ ] Interface-based communication is used
- [ ] Dependency inversion principle is followed
- [ ] SOLID principles are maintained

#### Code Quality
- [ ] Code style follows project conventions
- [ ] XML documentation is comprehensive
- [ ] Error handling is appropriate
- [ ] Logging is implemented correctly
- [ ] Testing coverage is adequate
- [ ] Performance impact is assessed

## Governance Implementation

### 1. Continuous Integration Integration

#### Azure Pipelines Configuration

```yaml
# azure-pipelines-architectural-governance.yml
trigger:
  branches:
    include:
    - main
    - develop

jobs:
- job: ArchitecturalValidation
  displayName: 'Architectural Boundary Validation'
  
  steps:
  - task: UseDotNet@2
    inputs:
      packageType: 'sdk'
      version: '9.0.x'
  
  - script: dotnet restore
    displayName: 'Restore packages'
  
  - script: dotnet build --configuration Release --verbosity normal
    displayName: 'Build solution'
  
  - script: dotnet test --configuration Release --no-build --verbosity normal --logger trx
    displayName: 'Run tests'
  
  - script: dotnet run --project Tools/ArchitecturalValidator -- --solution $(Agent.BuildDirectory)/s/TiXL.sln
    displayName: 'Validate architectural constraints'
  
  - script: dotnet format --verify-no-changes --verbosity detailed
    displayName: 'Check code formatting'
```

### 2. Automated Validation Tools

#### Architectural Validator Tool

```csharp
// Tools/ArchitecturalValidator/Program.cs
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace TiXL.ArchitecturalValidator
{
    public class Program
    {
        private static readonly string[] AllowedDependencies = new[]
        {
            "TiXL.Core",
            "TiXL.Operators", 
            "TiXL.Gfx",
            "TiXL.Gui",
            "TiXL.Editor",
            "System",
            "Microsoft"
        };

        private static readonly (string Module, string[] Forbidden)[] ForbiddenModuleDeps = new[]
        {
            ("TiXL.Core", new[] { "TiXL.Operators", "TiXL.Gui", "TiXL.Gfx", "TiXL.Editor" }),
            ("TiXL.Operators", new[] { "TiXL.Gui", "TiXL.Gfx", "TiXL.Editor" }),
            ("TiXL.Gfx", new[] { "TiXL.Operators", "TiXL.Gui", "TiXL.Editor" }),
            ("TiXL.Gui", new[] { "TiXL.Gfx", "TiXL.Editor" })
        };

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: ArchitecturalValidator <solution-path>");
                Environment.Exit(1);
            }

            var solutionPath = args[0];
            var violations = ValidateArchitecturalConstraints(solutionPath);

            if (violations.Any())
            {
                Console.WriteLine($"Found {violations.Count} architectural violations:");
                foreach (var violation in violations)
                {
                    Console.WriteLine($"  ERROR: {violation}");
                }
                Environment.Exit(1);
            }

            Console.WriteLine("No architectural violations found.");
        }

        private static string[] ValidateArchitecturalConstraints(string solutionPath)
        {
            var violations = new List<string>();
            
            // Check project references
            foreach (var projectFile in Directory.GetFiles(solutionPath, "*.csproj", SearchOption.AllDirectories))
            {
                violations.AddRange(ValidateProjectReferences(projectFile));
            }

            // Check using statements in source files
            foreach (var sourceFile in Directory.GetFiles(solutionPath, "*.cs", SearchOption.AllDirectories))
            {
                violations.AddRange(ValidateUsingStatements(sourceFile));
            }

            return violations.ToArray();
        }

        private static string[] ValidateProjectReferences(string projectFile)
        {
            var violations = new List<string>();
            var content = File.ReadAllText(projectFile);
            
            var moduleName = Path.GetFileNameWithoutExtension(projectFile);
            var forbiddenDeps = ForbiddenModuleDeps.FirstOrDefault(d => d.Module == moduleName).Forbidden;

            if (forbiddenDeps?.Any() == true)
            {
                foreach (var forbiddenDep in forbiddenDeps)
                {
                    if (content.Contains($"<ProjectReference Include=\".*{Regex.Escape(forbiddenDep)}\""))
                    {
                        violations.Add($"Project {moduleName} cannot reference {forbiddenDep}");
                    }
                }
            }

            return violations.ToArray();
        }

        private static string[] ValidateUsingStatements(string sourceFile)
        {
            var violations = new List<string>();
            var content = File.ReadAllText(sourceFile);
            
            var moduleName = GetModuleFromPath(sourceFile);
            var forbiddenDeps = ForbiddenModuleDeps.FirstOrDefault(d => d.Module == moduleName).Forbidden;

            if (forbiddenDeps?.Any() == true)
            {
                foreach (var forbiddenDep in forbiddenDeps)
                {
                    var pattern = $@"using\s+{Regex.Escape(forbiddenDep)}";
                    if (Regex.IsMatch(content, pattern))
                    {
                        violations.Add($"File {sourceFile} contains forbidden using statement for {forbiddenDep}");
                    }
                }
            }

            return violations.ToArray();
        }

        private static string GetModuleFromPath(string filePath)
        {
            var parts = filePath.Split(Path.DirectorySeparatorChar);
            var srcIndex = Array.IndexOf(parts, "src");
            
            if (srcIndex >= 0 && srcIndex + 1 < parts.Length)
            {
                return parts[srcIndex + 1];
            }
            
            return "Unknown";
        }
    }
}
```

### 3. Documentation and Training

#### Developer Onboarding

1. **Architectural Overview Training**
   - Module boundaries and responsibilities
   - Dependency rules and restrictions
   - Communication patterns

2. **Code Review Training**
   - Architectural checklist usage
   - Common violation patterns
   - Fix strategies

3. **Tool Usage Training**
   - IDE configuration for architectural hints
   - Command-line validation tools
   - CI/CD integration understanding

#### Continuous Education

1. **Monthly Architecture Reviews**
   - Recent architectural decisions
   - Violation pattern analysis
   - Process improvements

2. **Quarterly Architecture Assessment**
   - Overall architectural health
   - Dependency graph analysis
   - Boundary enforcement effectiveness

### 4. Governance Metrics

#### Key Performance Indicators

1. **Architectural Compliance Rate**
   - Target: 100% compliance
   - Measurement: Automated validation passes

2. **Dependency Violation Count**
   - Target: 0 violations per release
   - Measurement: Pre-commit and CI validation

3. **Code Review Quality**
   - Target: 95% checklist completion
   - Measurement: Review template usage

4. **Architecture Documentation Currency**
   - Target: Documentation updated within 1 week of architectural changes
   - Measurement: Documentation change tracking

#### Reporting Dashboard

- **Architectural Health Score**
- **Module Dependency Visualization**
- **Violation Trend Analysis**
- **Compliance Rate Tracking**

## Enforcement Mechanisms

### 1. Build-Time Enforcement

- Static analysis rules in project files
- Custom MSBuild tasks for dependency checking
- Roslyn analyzers for architectural constraints
- Code coverage requirements by module

### 2. Runtime Enforcement

- Interface validation at module boundaries
- Dependency injection container validation
- Runtime architecture tests
- Integration testing for module interactions

### 3. Process Enforcement

- Git hooks for pre-commit validation
- CI/CD pipeline architectural gates
- Code review checklist requirements
- Automated architectural documentation updates

## Conclusion

This architectural governance framework provides comprehensive enforcement mechanisms to maintain TiXL's architectural integrity while supporting evolution and extensibility. The combination of automated validation, process requirements, and developer education ensures consistent adherence to architectural boundaries and patterns.

Regular review and refinement of these governance rules will ensure they remain effective as the project evolves and grows.