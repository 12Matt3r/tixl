# TiXL Developer Onboarding Guide

Welcome to the TiXL (Tooll 3) developer community! This comprehensive guide will help you get started with developing and contributing to TiXL, a real-time motion graphics platform that combines procedural graph-based composition with timeline editing and live-performance features.

## Quick Start for Experienced Developers

If you're already familiar with modern .NET development, here's what you need to know to get TiXL running quickly:

### Prerequisites
- **Windows 10/11** (primary development platform)
- **.NET 9.0.0 SDK** or later
- **Visual Studio 2022** or **JetBrains Rider** 
- **Git**

### 5-Minute Setup
```bash
# Clone the repository
git clone https://github.com/tixl3d/tixl.git
cd tixl

# Restore packages and build
dotnet restore
dotnet build --configuration Release

# Run the Editor
dotnet run --project Editor/Editor.csproj --configuration Release
```

**That's it!** You should now have TiXL running. Continue reading for detailed setup and contribution guidelines.

---

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

**Cross-Platform Notes:**
- TiXL primarily targets Windows, but community wrappers exist for Linux/macOS
- See [Non-Windows Development](#non-windows-development) below

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

### Cloning and Initial Setup

```bash
# Clone the main repository
git clone https://github.com/tixl3d/tixl.git
cd tixl

# List available branches
git branch -a

# Check out the latest development branch (usually main)
git checkout main

# View remote information
git remote -v
```

### Project Dependencies

TiXL uses these key external libraries (automatically restored via NuGet):
- **ImGui.NET** - Immediate mode GUI framework
- **Silk.NET** - Modern OpenGL/Vulkan bindings  
- **Emgu CV** - Computer vision capabilities
- **SharpDX** - DirectX API bindings
- **NDI SDK** - Network Device Interface
- **Spout** - Real-time video sharing

## Project Structure Explained

TiXL follows a modular architecture with clear separation of concerns. Here's what each major directory contains:

```
tixl/
├── Core/                    # Fundamental engine components
├── Operators/              # Plugin-based operator system
├── Editor/                 # User interface & development environment
├── Resources/              # Application resources and assets
├── Dependencies/           # External libraries and packages
├── ImguiWindows/           # Immediate mode GUI integration
├── Player/                 # Standalone playback application
├── Serialization/          # Data persistence system
└── Windows/                # Windows Forms integration
```

### Core Module Deep Dive

The **Core** module is the heart of TiXL's engine:

- **Animation/** - Keyframe animation and curve system
- **Audio/** - Real-time audio processing and synchronization  
- **Compilation/** - Shader and code compilation logic
- **DataTypes/** - Custom data structures and mathematics
- **IO/** - File and network I/O operations
- **Model/** - Domain models and business logic
- **Operator/** - Core operator system (Symbols, Instances, Slots)
- **Rendering/** - DirectX 12 graphics pipeline
- **Resource/** - Asset management and loading
- **Stats/** - Performance monitoring and statistics
- **Video/** - Video processing and screenshot capabilities

![TiXL Core Directory Structure](/workspace/browser/screenshots/tixl_core_directory.png)

### Operators Module Deep Dive

The **Operators** module enables TiXL's extensibility:

- **TypeOperators/Collections/** - Data collection and manipulation
- **TypeOperators/Gfx/** - Graphics pipeline operators
- **TypeOperators/NET/** - .NET framework integration
- **TypeOperators/Values/** - Value and parameter operators
- **Lib/** - Third-party library integrations
- **Ndi/** - Network video streaming
- **Spout/** - Inter-application video sharing
- **examples/** - Example operators and tutorials

![TiXL Operators Directory](/workspace/browser/screenshots/tixl_operators_directory.png)
![TypeOperators Structure](/workspace/browser/screenshots/tixl_typeoperators_directory.png)
![Graphics Operators](/workspace/browser/screenshots/tixl_gfx_operators.png)

### Editor Module Deep Dive

The **Editor** module provides the development environment:

- **App/** - Core application logic and orchestration
- **Compilation/** - Built-in compilation system
- **Gui/** - User interface components and rendering
  - **Graph/** - Node graph visualization  
  - **InputUi/** - Input control components
  - **Interaction/** - Advanced interaction handling
  - **OpUis/** - Operator-specific UI components
  - **Styling/** - UI theming and styling
  - **Windows/** - Docking and window management
- **Properties/** - Project configuration
- **SplashScreen/** - Application startup

![TiXL Editor Directory](/workspace/browser/screenshots/tixl_editor_directory.png)

### Gui Module Architecture

The **Gui** module implements TiXL's immediate-mode UI system:

![TiXL Gui Directory](/workspace/browser/screenshots/tixl_gui_directory.png)

## Building and Running TiXL

### Building the Solution

**Debug Build (Development):**
```bash
# From the solution root
dotnet build --configuration Debug

# Or build specific projects
dotnet build Core/Core.csproj --configuration Debug
dotnet build Editor/Editor.csproj --configuration Debug
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

# Run with additional arguments
dotnet run --project Editor/Editor.csproj -- --help
```

**From Visual Studio:**
1. Set **Editor** as the startup project
2. Press **F5** to build and run
3. Or use **Ctrl+F5** to run without debugging

**Expected Startup:**
- TiXL splash screen appears
- Main editor window loads with default workspace
- GPU compatibility check occurs
- Ready to create your first project!

### Troubleshooting Build Issues

**Common Build Errors:**

1. **"Unable to locate .NET SDK"**
   ```bash
   # Check installed versions
   dotnet --list-sdks
   
   # Install .NET 9.0 SDK from dotnet.microsoft.com
   ```

2. **"NuGet package restore failed"**
   ```bash
   # Clean and restore
   dotnet clean
   dotnet restore
   ```

3. **"DirectX compatibility issues"**
   - Ensure you have DirectX 11.3+ capable GPU
   - Update graphics drivers
   - Check Windows Graphics Tools installation

4. **"Missing runtime dependencies"**
   - Install Visual C++ Redistributable
   - Install Windows Graphics Tools via Windows Features

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
- **Buffer Management** - Constant, structured, and indirect buffers
- **Texture System** - 2D textures, render targets, multi-target rendering

### Memory Architecture

**Key Principles:**
- **16-byte alignment** for DirectX constant buffers
- **Write-discard mapping** for efficient CPU→GPU transfers
- **Resource lifetime management** via IDisposable patterns
- **State caching** to minimize pipeline reconstruction

## Your First Contribution

### Recommended First Tasks

**Beginner-Friendly Issues:**
1. **Documentation improvements** - Fix typos, improve examples
2. **Test case additions** - Add missing unit or integration tests  
3. **Operator parameter validation** - Add error checking
4. **UI consistency fixes** - Align styling or behavior
5. **Build warnings cleanup** - Remove compiler warnings

**Intermediate Tasks:**
1. **New operator implementation** - Add missing functionality
2. **Performance optimizations** - Profile and improve hot paths
3. **Bug fixes** - Address reported issues with clear reproduction steps

### Setting Up Your Development Branch

```bash
# Create a new branch for your contribution
git checkout -b feature/your-feature-name

# Or for bug fixes
git checkout -b bugfix/issue-number-description

# Verify you're on the correct branch
git branch
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

### Testing Your Operator

1. **Build the project** - Ensure your code compiles
2. **Run TiXL** - Launch the editor
3. **Add your operator** - Use the operator search/creation interface
4. **Test inputs/outputs** - Verify correct behavior
5. **Create test cases** - Add unit tests for edge cases

### Common Contribution Mistakes to Avoid

1. **Don't modify unrelated files** - Keep changes focused
2. **Don't skip testing** - Verify your changes work
3. **Don't ignore coding conventions** - Follow established patterns
4. **Don't make massive commits** - Break changes into logical chunks
5. **Don't forget documentation** - Update relevant docs

## Development Workflow

### Branch Strategy

TiXL follows a simplified GitFlow model:

- **main** - Production-ready code
- **develop** - Integration branch for features
- **feature/feature-name** - Individual feature development
- **bugfix/issue-number** - Bug fix development
- **hotfix/description** - Urgent production fixes

### Commit Conventions

Use **Conventional Commits** format:

```
type(scope): description

[optional body]

[optional footer]
```

**Types:**
- `feat:` - New features
- `fix:` - Bug fixes  
- `docs:` - Documentation changes
- `style:` - Code style changes (formatting)
- `refactor:` - Code refactoring
- `test:` - Test additions/modifications
- `chore:` - Maintenance tasks

**Examples:**
```bash
feat(operator): add sine wave generator operator
fix(gui): resolve parameter panel crash when updating values
docs(readme: add troubleshooting section for build failures
refactor(core): simplify buffer allocation pattern
```

### Pull Request Process

1. **Before Submitting:**
   - [ ] Code builds without warnings
   - [ ] Tests pass
   - [ ] Documentation updated
   - [ ] Commit messages follow conventions

2. **PR Template Checklist:**
   - [ ] Description explains what and why
   - [ ] Screenshots/videos for UI changes
   - [ ] Related issues linked
   - [ ] Breaking changes documented

3. **Code Review Process:**
   - [ ] At least one maintainer approval
   - [ ] CI/CD pipeline passes
   - [ ] Address reviewer feedback
   - [ ] Squash commits if requested

### Issue Reporting

**When Filing Issues:**

1. **Use descriptive titles** - "Cannot create project with custom shader operators"

2. **Include reproduction steps:**
   ```
   1. Open TiXL
   2. Click "New Project"  
   3. Select "Custom Shader" template
   4. See error
   
   Expected: Project creates successfully
   Actual: Application crashes
   ```

3. **Provide system information:**
   - TiXL version (Help → About)
   - Windows version
   - GPU and driver version
   - Relevant log files

4. **Attach supporting files:**
   - Screenshots/videos
   - Project files (.tixl)
   - Log files
   - Stack traces

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

**Debugging the Graphics Pipeline:**
```csharp
// In your rendering code
if (System.Diagnostics.Debugger.IsAttached)
{
    // This code only runs when debugger is attached
    System.Diagnostics.Debug.WriteLine($"Buffer size: {buffer.Size}");
}
```

### Using RenderDoc for Graphics Debugging

1. **Install RenderDoc** from renderdoc.org
2. **Configure TiXL integration:**
   - Add `RenderDoc.dll` to TiXL directory
   - Or use RenderDoc's injection mode
3. **Capture frames:**
   - Launch TiXL with RenderDoc
   - Press **F12** to capture current frame
   - Analyze in RenderDoc's interface

### Performance Profiling

**Built-in Statistics:**
```csharp
// Access TiXL's performance stats
using Core.Stats;

var stats = StatsManager.GetInstance();
stats.LogFrameTime("Operator.Evaluate");
stats.LogMemoryUsage("Buffer.Allocation");
```

**Profiling Tips:**
1. **Profile in Release mode** - Debug builds are not representative
2. **Use realistic test data** - Small datasets don't show real performance
3. **Measure multiple frames** - Single frame measurements can be misleading
4. **Focus on hot paths** - Optimize what's actually slow

### Common Debugging Scenarios

**1. Operator Not Executing**
- Check if operator is properly registered
- Verify input connections are valid
- Ensure operator is in the active graph

**2. Memory Leaks**
- Verify IDisposable patterns are followed
- Check for unreleased DirectX resources
- Use memory profiling tools

**3. Performance Issues**
- Profile operator evaluation time
- Check for excessive garbage collection
- Verify buffer allocation patterns

## Common Issues and Troubleshooting

### Installation and Setup Issues

**Problem: "Cannot create a project" (Issue #738)**
```
Error: Application crashes when creating new project
Solution: 
1. Ensure TiXL is running as administrator
2. Check write permissions to user documents folder
3. Verify all dependencies are installed
4. Try creating project in different location
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

**Problem: "Operator not appearing in editor"**
1. Check namespace and using statements
2. Verify OperatorClass attribute is correct
3. Ensure proper registration
4. Check for compilation errors in operator code

**Problem: "Performance degradation after changes"**
1. Profile your changes specifically
2. Check for unnecessary allocations
3. Verify buffer reuse patterns
4. Test with Release builds

### Non-Windows Development

**Linux/macOS Development Notes:**
- TiXL primarily targets Windows
- Community wrappers exist but may have limitations
- Consider using Windows VM or dual-boot setup
- Some features may not work on non-Windows platforms

**Virtual Machine Setup:**
- Install Windows 11 in VirtualBox or VMware
- Allocate sufficient RAM (8GB+ recommended)
- Enable GPU passthrough for DirectX support
- Install Visual Studio Community Edition

### Getting Help

**Community Resources:**
1. **Discord**: https://discord.gg/YmSyQdeH3S (Primary support channel)
2. **GitHub Issues**: https://github.com/tixl3d/tixl/issues
3. **YouTube Tutorials**: https://www.youtube.com/@Tooll3

**Getting Effective Help:**
1. Search existing issues first
2. Provide clear reproduction steps
3. Include system information
4. Attach supporting files (screenshots, logs)
5. Be patient and polite

## Extending TiXL

### Creating Custom Operators

**Step-by-Step Guide:**

1. **Choose your category:**
   - Values (math, constants, transformations)
   - Collections (arrays, lists, data structures)
   - Gfx (rendering, shaders, textures)
   - Lib (third-party integrations)

2. **Create operator class:**
```csharp
using Core.Operator;
using Core.Operator.Slots;

namespace Operators.Values
{
    [OperatorClass("MyCustomOperator")]
    public class MyCustomOperator : Instance
    {
        [InputSlot("InputValue")]
        public ISlot InputSlot { get; set; }
        
        [OutputSlot("Result")]
        public ISlot OutputSlot { get; set; }
        
        [Parameter("Scale")]
        public float Scale { get; set; } = 1.0f;
        
        public override void Evaluate(EvaluationContext context)
        {
            var input = InputSlot.GetValue<float>(context);
            var result = input * Scale;
            OutputSlot.SetValue(context, result);
        }
    }
}
```

3. **Test your operator:**
   - Build and run TiXL
   - Find your operator in the operator browser
   - Create a test graph
   - Verify correct behavior

### Creating Custom Shaders

**HLSL Shader Development:**

1. **Create shader file:**
```hlsl
// In Operators/TypeOperators/Gfx/Shaders/MyShader.compute
[numthreads(8, 8, 1)]
void CSMain(uint3 id : SV_DispatchThreadID)
{
    // Your compute shader code here
    float2 uv = (float2)id.xy / 8.0;
    // Process pixels...
}
```

2. **Create operator to load shader:**
```csharp
public class MyComputeOperator : Instance
{
    private ComputeShader _computeShader;
    
    public override void Initialize(InitializationContext context)
    {
        _computeShader = context.LoadComputeShader("Shaders/MyShader.compute");
    }
    
    public override void Evaluate(EvaluationContext context)
    {
        // Use your compute shader
        context.DispatchComputeShader(_computeShader, width, height, 1);
    }
}
```

### Contributing Back

**Ready to share your work?**

1. **Create feature branch:**
```bash
git checkout -b feature/my-operator-name
```

2. **Test thoroughly:**
   - Unit tests
   - Integration tests  
   - Manual testing

3. **Prepare documentation:**
   - Operator usage examples
   - Parameter descriptions
   - Screenshots/videos if applicable

4. **Submit pull request:**
   - Follow contribution guidelines
   - Include comprehensive description
   - Link any related issues

## Resources and Learning

### Essential Documentation

- **Operator Reference** (WIP) - Comprehensive operator catalog
- **Special Variables** - Context and system variables reference  
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
- **GPU PerfStudio** - AMD GPU profiling
- **NVIDIA Nsight** - NVIDIA GPU debugging
- **dotTrace** - .NET performance profiling

### Books and References

**Graphics Programming:**
- "Real-Time Rendering" by Möller & Haines
- "GPU Gems" series by NVIDIA
- "Mathematics for 3D Game Programming" by Eric Lengyel

**C# and .NET:**
- "C# 9.0 in a Nutshell" by Joseph Albahari
- "Pro .NET Performance" by Sasha Goldshtein

**Game Development Patterns:**
- "Game Programming Patterns" by Robert Nystrom
- "Architecture Patterns with Python" (applicable concepts)

### Community and Support

- **Discord Server** - Real-time community support
- **GitHub Discussions** - Feature discussions and Q&A
- **Stack Overflow** - General programming questions (tag: tixl)
- **Reddit** - r/GraphicsProgramming, r/gamedev communities

---

## Conclusion

Welcome to the TiXL development community! This guide covers the essentials to get you started, but there's always more to learn. The best way to learn is by doing:

1. **Start small** - Fix a typo, add a test, optimize a small function
2. **Ask questions** - The community is welcoming and helpful
3. **Read code** - Learn from existing implementations
4. **Experiment** - Try new approaches and operator types
5. **Share your work** - Contribute back to help others

TiXL is a powerful platform for real-time graphics, and your contributions help make it even better. We're excited to see what you'll build!

### Next Steps

1. **Set up your development environment** using this guide
2. **Build and run TiXL** to familiarize yourself with the platform
3. **Pick a beginner-friendly issue** to work on
4. **Join the Discord community** to connect with other developers
5. **Start contributing** and sharing your progress!

**Happy coding, and welcome to TiXL!**

---

*This guide is a living document. If you find gaps, errors, or outdated information, please contribute improvements back to the project.*