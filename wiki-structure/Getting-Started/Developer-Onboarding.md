# Developer Onboarding

Welcome to the TiXL developer community! This comprehensive guide will help you get started with developing and contributing to TiXL, a real-time motion graphics platform.

## Table of Contents

1. [Development Environment Setup](#development-environment-setup)
2. [Project Structure Explained](#project-structure-explained)
3. [Building and Running TiXL](#building-and-running-tixl)
4. [Understanding the Architecture](#understanding-the-architecture)
5. [Your First Contribution](#your-first-contribution)
6. [Development Workflow](#development-workflow)
7. [Debugging and Profiling](#debugging-and-profiling)
8. [Common Issues and Troubleshooting](#common-issues-and-troubleshooting)
9. [Extending TiXL](#extending-tixl)
10. [Resources and Learning](#resources-and-learning)

## Development Environment Setup

### System Requirements

**Primary Development Platform:**
- **OS**: Windows 10/11 (64-bit)
- **GPU**: DirectX 11.3 compatible (GTX 970 or later recommended)
- **RAM**: 8GB minimum, 16GB recommended
- **Storage**: 2GB free space

### Required Software

1. **.NET 9.0.0 SDK**
   - Download from: https://dotnet.microsoft.com/download
   - Verify installation: `dotnet --version`

2. **IDE (Choose One)**
   - **Visual Studio 2022** (Community/Professional/Enterprise)
     - Install with ".NET desktop development" workload
     - Include "Game development with C++" for better DirectX support
   - **JetBrains Rider** (Commercial, but excellent for .NET)
   - **Visual Studio Code** (Lightweight, requires extensions)

3. **Git**
   - Git for Windows: https://git-scm.com/download/win
   - Configure with: `git config --global user.name "Your Name"` and `git config --global user.email "your.email@example.com"`

4. **Optional but Recommended**
   - **RenderDoc** (for graphics debugging)
   - **GitHub Desktop** (or your preferred Git GUI)
   - **Windows Terminal** (enhanced command line experience)

### Project Dependencies

TiXL uses these key external libraries (automatically restored via NuGet):
- **ImGui.NET** - Immediate mode GUI framework
- **Silk.NET** - Modern OpenGL/Vulkan bindings  
- **Emgu CV** - Computer vision capabilities
- **SharpDX** - DirectX API bindings
- **NDI SDK** - Network Device Interface
- **Spout** - Real-time video sharing

## Project Structure Explained

TiXL follows a modular architecture with clear separation of concerns:

```
tixl/
├── Core/                    # Fundamental engine components
├── Operators/              # Plugin-based operator system
├── Editor/                 # User interface & development environment
├── Resources/              # Application resources and assets
└── Dependencies/           # External libraries and packages
```

### Core Module Deep Dive

The **Core** module is the heart of TiXL's engine:
- **Animation/** - Keyframe animation and curve system
- **Audio/** - Real-time audio processing and synchronization  
- **Compilation/** - Shader and code compilation logic
- **DataTypes/** - Custom data structures and mathematics
- **IO/** - File and network I/O operations
- **Operator/** - Core operator system (Symbols, Instances, Slots)
- **Rendering/** - DirectX 12 graphics pipeline

### Operators Module Deep Dive

The **Operators** module enables TiXL's extensibility:
- **TypeOperators/Collections/** - Data collection and manipulation
- **TypeOperators/Gfx/** - Graphics pipeline operators
- **TypeOperators/NET/** - .NET framework integration
- **TypeOperators/Values/** - Value and parameter operators
- **examples/** - Example operators and tutorials

### Editor Module Deep Dive

The **Editor** module provides the development environment:
- **App/** - Core application logic and orchestration
- **Gui/** - User interface components and rendering
  - **Graph/** - Node graph visualization  
  - **InputUi/** - Input control components
  - **OpUis/** - Operator-specific UI components

## Building and Running TiXL

### Building the Solution

**Debug Build (Development):**
```bash
dotnet build --configuration Debug
```

**Release Build (Production):**
```bash
dotnet build --configuration Release
```

**Expected Build Output:**
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### Running TiXL

**From Command Line:**
```bash
# Run the Editor in Debug mode
dotnet run --project Editor/Editor.csproj --configuration Debug
```

**From Visual Studio:**
1. Set **Editor** as the startup project
2. Press **F5** to build and run

**Expected Startup:**
- TiXL splash screen appears
- Main editor window loads with default workspace
- GPU compatibility check occurs
- Ready to create your first project!

## Understanding the Architecture

### Operator System Architecture

TiXL uses a sophisticated operator-based system with these key concepts:

**Core Components:**
- **Symbol** - Operator definition (type, parameters, metadata)
- **Instance** - Runtime execution of an operator
- **Slots** - Typed input/output connections
- **Registry** - Central discovery and management
- **EvaluationContext** - Execution environment

**Data Flow:**
```
User Input → Gui → Operator Instances → EvaluationContext → Gfx Pipeline → Render Output
```

### Graphics Pipeline

TiXL implements a modern DirectX 12 pipeline:
- **Compute Shaders** - GPU-accelerated processing
- **Pixel Shaders** - Per-pixel rendering operations
- **Geometry Shaders** - Vertex processing and manipulation
- **Pipeline States** - Blend, depth-stencil, rasterizer, sampler states

## Your First Contribution

### Recommended First Tasks

**Beginner-Friendly Issues:**
1. **Documentation improvements** - Fix typos, improve examples
2. **Test case additions** - Add missing unit or integration tests  
3. **Operator parameter validation** - Add error checking
4. **UI consistency fixes** - Align styling or behavior
5. **Build warnings cleanup** - Remove compiler warnings

### Setting Up Your Development Branch

```bash
# Create a new branch for your contribution
git checkout -b feature/your-feature-name

# Or for bug fixes
git checkout -b bugfix/issue-number-description
```

### Creating Your First Operator

Here's a simple example operator to get you started:

```csharp
// In Operators/TypeOperators/Values/ExampleValueOperator.cs
using Core.Operator;
using Core.Operator.Slots;

namespace Operators.Values
{
    [OperatorClass("ExampleValue")]
    public class ExampleValueOperator : Instance
    {
        // Define input/output slots
        [InputSlot("Input")]
        public ISlot InputSlot { get; set; }
        
        [OutputSlot("Output")] 
        public ISlot OutputSlot { get; set; }
        
        [Parameter("Multiplier")]
        public float Multiplier { get; set; } = 1.0f;
        
        public override void Evaluate(EvaluationContext context)
        {
            // Get input value
            var inputValue = InputSlot.GetValue<float>(context);
            
            // Process value
            var result = inputValue * Multiplier;
            
            // Set output
            OutputSlot.SetValue(context, result);
        }
    }
}
```

## Development Workflow

### Branch Strategy

TiXL follows a simplified GitFlow model:
- **main** - Production-ready code
- **develop** - Integration branch for features
- **feature/feature-name** - Individual feature development
- **bugfix/issue-number** - Bug fix development

### Commit Conventions

Use **Conventional Commits** format:
```
type(scope): description
```

**Types:**
- `feat:` - New features
- `fix:` - Bug fixes  
- `docs:` - Documentation changes
- `test:` - Test additions/modifications
- `chore:` - Maintenance tasks

## Debugging and Profiling

### Debugging in Visual Studio

**Breakpoints and Stepping:**
```csharp
// Set breakpoints on operator evaluation
public override void Evaluate(EvaluationContext context)
{
    var inputValue = InputSlot.GetValue<float>(context); // ← Breakpoint here
    // Step through your code
}
```

### Performance Profiling

**Built-in Statistics:**
```csharp
// Access TiXL's performance stats
using Core.Stats;

var stats = StatsManager.GetInstance();
stats.LogFrameTime("Operator.Evaluate");
stats.LogMemoryUsage("Buffer.Allocation");
```

## Common Issues and Troubleshooting

### Installation and Setup Issues

**Problem: "NuGet package restore failed"**
```bash
# Clear NuGet cache
dotnet nuget locals all --clear

# Restore with force
dotnet restore --force

# Clean solution
dotnet clean
rm -rf bin/ obj/
dotnet restore
dotnet build
```

**Problem: "DirectX initialization failed"**
```
Error: Graphics adapter not found
Solution:
1. Update GPU drivers to latest version
2. Enable DirectX 11.3 features
3. Check Windows Graphics Tools installation
4. Try running TiXL in compatibility mode
```

### Development Issues

**Problem: "Build warnings about nullable references"**
```csharp
// Fix nullable warnings
public override void Evaluate(EvaluationContext context)
{
    var inputValue = InputSlot?.GetValue<float>(context) ?? 0f;
    
    if (OutputSlot != null)
    {
        OutputSlot.SetValue(context, inputValue * 2.0f);
    }
}
```

## Getting Help

**Community Resources:**
1. **Discord**: https://discord.gg/YmSyQdeH3S (Primary support channel)
2. **GitHub Issues**: https://github.com/tixl3d/tixl/issues
3. **YouTube Tutorials**: https://www.youtube.com/@Tooll3

## Resources and Learning

### Essential Documentation

- **Operator Reference** - Comprehensive operator catalog
- **API Documentation** - Detailed API reference
- **Coding Conventions** - Code style and patterns
- **Operator Conventions** - Operator development standards

### Video Tutorials

- **TiXL Overview** - 15-minute introduction
- **Shader Graph Deep Dive** - Advanced graphics programming
- **HLSL Compute Shader Tutorial** - Custom shader development
- **C# Operator Development** - Extending TiXL with operators

### Developer Tools

- **RenderDoc** - Graphics debugging and profiling
- **dotTrace** - .NET performance profiling
- **NUnit** - Unit testing framework

---

**Next Steps:**
1. **Set up your development environment** using this guide
2. **Build and run TiXL** to familiarize yourself with the platform
3. **Pick a beginner-friendly issue** to work on
4. **Join the Discord community** to connect with other developers

**Happy coding!** 🎨✨
