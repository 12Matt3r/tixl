# Download & Setup Guide

Complete guide to downloading, setting up, and running TiXL examples on your local machine. Get started with high-quality examples in minutes.

---

## 🚀 Quick Start Guide

### Option 1: Example Collection Download

**📦 Complete Examples Package**

```bash
# Download the complete examples collection (recommended)
curl -L https://github.com/tixl3d/examples-collection/archive/main.zip -o examples-collection.zip

# Extract and setup
unzip examples-collection.zip
cd examples-collection-main
chmod +x setup.sh
./setup.sh
```

**⚡ Automated Setup Script**

```bash
#!/bin/bash
# setup.sh - Automated TiXL Examples setup

set -e

echo "🚀 Setting up TiXL Examples Collection..."

# Check prerequisites
echo "Checking prerequisites..."

# Check .NET
if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET SDK not found. Installing .NET 9.0..."
    wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
    sudo dpkg -i packages-microsoft-prod.deb
    sudo apt-get update
    sudo apt-get install -y dotnet-sdk-9.0
else
    echo "✅ .NET SDK found: $(dotnet --version)"
fi

# Check Git
if ! command -v git &> /dev/null; then
    echo "❌ Git not found. Installing Git..."
    sudo apt-get install -y git
else
    echo "✅ Git found: $(git --version)"
fi

# Install TiXL CLI tools
echo "Installing TiXL CLI tools..."
dotnet tool install -g TiXL.SDK
dotnet tool install -g TiXL.Examples.CLI

# Restore all examples
echo "Restoring example dependencies..."
find examples -name "*.csproj" -exec dotnet restore {} \;

# Build all examples
echo "Building all examples..."
find examples -name "*.csproj" -exec dotnet build --configuration Release {} \;

# Setup VS Code workspace (optional)
if command -v code &> /dev/null; then
    echo "Setting up VS Code workspace..."
    code --install-extension ms-dotnettools.csharp
    code --install-extension ms-vscode.vscode-json
fi

echo "✅ TiXL Examples Collection setup complete!"
echo ""
echo "🎯 Next steps:"
echo "1. cd examples/graphics/pbr-materials"
echo "2. dotnet run"
echo "3. Explore more examples in examples/ directory"
```

### Option 2: Individual Examples

**🎯 Download Specific Examples**

```bash
# Download and setup a specific example
git clone https://github.com/tixl3d/example-pbr-materials.git
cd example-pbr-materials
dotnet restore
dotnet run
```

**📥 Download Manager CLI**

```bash
# Use TiXL CLI to download examples
tixl examples download pbr-materials --category graphics
tixl examples download audio-synthesizer --category audio
tixl examples download data-visualizer --category data

# List available examples
tixl examples list --category graphics
tixl examples search "shader" --difficulty intermediate

# Download with dependencies
tixl examples download advanced-rendering --with-dependencies
```

---

## 📋 System Requirements

### Minimum Requirements

**💻 Hardware Requirements**

| Component | Minimum | Recommended |
|-----------|---------|-------------|
| **CPU** | Intel i5 / AMD Ryzen 5 | Intel i7 / AMD Ryzen 7 |
| **RAM** | 8 GB | 16 GB |
| **GPU** | DirectX 12 compatible | NVIDIA GTX 1660 / AMD RX 580 |
| **Storage** | 2 GB free space | 5 GB SSD |
| **OS** | Windows 10, Ubuntu 20.04+, macOS 11+ | Windows 11, Ubuntu 22.04+, macOS 12+ |

**🔧 Software Requirements**

| Software | Minimum Version | Purpose |
|----------|----------------|---------|
| **.NET SDK** | 9.0 | Runtime and development |
| **Git** | 2.30+ | Version control |
| **Visual Studio** | 2022 | IDE (optional) |
| **VS Code** | 1.70+ | Lightweight editor |
| **TiXL SDK** | 2.1.0 | TiXL platform |

### Platform-Specific Setup

**🪟 Windows Setup**

```powershell
# Install .NET 9.0 SDK
winget install Microsoft.DotNet.SDK.9

# Install Git for Windows
winget install --id Git.Git -e --source winget

# Install TiXL CLI
dotnet tool install -g TiXL.SDK
dotnet tool install -g TiXL.Examples.CLI

# Clone examples repository
git clone https://github.com/tixl3d/examples-collection.git
cd examples-collection

# Run setup
.\scripts\setup.ps1
```

**🐧 Linux Setup (Ubuntu/Debian)**

```bash
# Install dependencies
sudo apt update
sudo apt install -y wget apt-transport-https

# Add Microsoft package repository
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb

# Install .NET SDK
sudo apt update
sudo apt install -y dotnet-sdk-9.0

# Install Git
sudo apt install -y git

# Install TiXL CLI tools
dotnet tool install -g TiXL.SDK
dotnet tool install -g TiXL.Examples.CLI

# Setup examples
git clone https://github.com/tixl3d/examples-collection.git
cd examples-collection
chmod +x setup.sh
./setup.sh
```

**🍎 macOS Setup**

```bash
# Install Homebrew (if not installed)
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"

# Install .NET SDK
brew install --cask dotnet

# Install Git
brew install git

# Install TiXL CLI tools
dotnet tool install -g TiXL.SDK
dotnet tool install -g TiXL.Examples.CLI

# Setup examples
git clone https://github.com/tixl3d/examples-collection.git
cd examples-collection
chmod +x setup.sh
./setup.sh
```

---

## 🎯 Example Categories & Downloads

### Graphics & Rendering Examples

**🎨 Complete Graphics Collection**

```bash
# Download all graphics examples
git clone https://github.com/tixl3d/examples-graphics.git

# Individual graphics examples
git clone https://github.com/tixl3d/example-pbr-materials.git
git clone https://github.com/tixl3d/example-dynamic-lighting.git  
git clone https://github.com/tixl3d/example-post-processing.git
git clone https://github.com/tixl3d/example-procedural-geometry.git
```

| Example | Description | Difficulty | Size | Features |
|---------|-------------|------------|------|----------|
| **PBR Materials** | Physically-based rendering | Intermediate | 45MB | PBR, IBL, normal mapping |
| **Dynamic Lighting** | Real-time shadows & lighting | Advanced | 62MB | Shadow mapping, multiple lights |
| **Post Processing** | Real-time visual effects | Intermediate | 38MB | Bloom, tone mapping, filters |
| **Procedural Geometry** | Dynamic mesh generation | Advanced | 51MB | Noise generation, tessellation |

### Audio Processing Examples

**🎵 Complete Audio Collection**

```bash
# Download all audio examples
git clone https://github.com/tixl3d/examples-audio.git

# Individual audio examples
git clone https://github.com/tixl3d/example-live-synthesizer.git
git clone https://github.com/tixl3d/example-spectrum-analyzer.git
git clone https://github.com/tixl3d/example-audio-effects.git
git clone https://github.com/tixl3d/example-spatial-audio.git
```

| Example | Description | Difficulty | Size | Features |
|---------|-------------|------------|------|----------|
| **Live Synthesizer** | Real-time sound generation | Beginner | 28MB | Oscillators, filters, MIDI |
| **Spectrum Analyzer** | Visual audio analysis | Intermediate | 32MB | FFT, real-time visualization |
| **Audio Effects Rack** | Multi-effect processing | Intermediate | 41MB | Reverb, delay, distortion |
| **Spatial Audio** | 3D audio positioning | Advanced | 35MB | HRTF, room acoustics |

### Data & Mathematical Examples

**📊 Complete Data Collection**

```bash
# Download all data examples
git clone https://github.com/tixl3d/examples-data.git

# Individual data examples
git clone https://github.com/tixl3d/example-data-visualization.git
git clone https://github.com/tixl3d/example-matrix-operations.git
git clone https://github.com/tixl3d/example-monte-carlo.git
git clone https://github.com/tixl3d/example-dsp.git
```

| Example | Description | Difficulty | Size | Features |
|---------|-------------|------------|------|----------|
| **Data Visualization** | Real-time data plotting | Beginner | 33MB | Charts, graphs, animations |
| **Matrix Operations** | Advanced linear algebra | Intermediate | 29MB | Matrix math, transformations |
| **Monte Carlo Simulation** | Statistical simulations | Advanced | 37MB | Random sampling, statistics |
| **Digital Signal Processing** | Signal analysis algorithms | Advanced | 42MB | Filters, convolution, FFT |

---

## 🛠️ Development Environment Setup

### IDE Configuration

**🔧 Visual Studio Setup**

```json
{
  "omnisharp.enableRoslynAnalyzers": true,
  "omnisharp.enableEditorConfigSupport": true,
  "csharp.referencesCodeLens.enabled": true,
  "files.exclude": {
    "**/bin": true,
    "**/obj": true,
    "**/.vs": true,
    "**/*.user": true
  },
  "search.exclude": {
    "**/bin": true,
    "**/obj": true,
    "**/.git": true
  },
  "files.watcherExclude": {
    "**/bin/**": true,
    "**/obj/**": true
  }
}
```

**⚙️ VS Code Setup**

```json
{
  "editor.formatOnSave": true,
  "editor.rulers": [100, 120],
  "editor.tabSize": 4,
  "editor.insertSpaces": true,
  "files.trimTrailingWhitespace": true,
  "files.insertFinalNewline": true,
  "files.trimFinalNewlines": true,
  
  "[csharp]": {
    "editor.defaultFormatter": "ms-dotnettools.csharp",
    "editor.formatOnSave": true,
    "editor.codeActionsOnSave": {
      "source.fixAll": true
    }
  },
  
  "csharp.referencesCodeLens.enabled": true,
  "csharp.suppressDotnetInstallWarning": false,
  
  "dotnet.server.useOmnisharp": true,
  "dotnet.server.path": "latest",
  
  "extensions.showRecommendationsOnlyOnDemand": false,
  "extensions.autoUpdate": true,
  
  "terminal.integrated.defaultProfile.windows": "PowerShell",
  "terminal.integrated.profiles.windows": {
    "PowerShell": {
      "source": "PowerShell",
      "path": [
        "C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe"
      ]
    }
  }
}
```

### Recommended Extensions

**🔌 Visual Studio Extensions**

- **TiXL Language Support**: Syntax highlighting and IntelliSense
- **Git Extensions**: Enhanced Git integration
- **Productivity Power Tools**: Code analysis and formatting
- **Error Helper**: Real-time error detection
- **Performance Profiler**: Built-in performance analysis

**🔌 VS Code Extensions**

```bash
# Install recommended extensions
code --install-extension ms-dotnettools.csharp
code --install-extension ms-vscode.vscode-json
code --install-extension ms-vscode.vscode-typescript-next
code --install-extension ms-vscode.cmake-tools
code --install-extension twxs.cmake
code --install-extension ms-vscode.remote-containers
code --install-extension ms-vscode-remote-ssh
code --install-extension ms-vscode-remote-wsl
code --install-extension ms-vscode-remote-containers
```

### Build Configuration

**🔨 Global Build Settings**

```xml
<!-- Directory.Build.props -->
<Project>
  <PropertyGroup>
    <TiXLVersion>2.1.0</TiXLVersion>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    
    <!-- Build Configuration -->
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsAsErrors />
    <AnalysisLevel>latest</AnalysisLevel>
    <AnalysisMode>Default</AnalysisMode>
    
    <!-- Output Configuration -->
    <OutputPath>bin\$(Configuration)\$(TargetFramework)\</OutputPath>
    <BaseIntermediateOutputPath>obj\$(Configuration)\$(TargetFramework)\</BaseIntermediateOutputPath>
    
    <!-- Package Configuration -->
    <GeneratePackageOnBuild>false</GeneratePackageOnBuild>
    <PackageTags>TiXL;Example;Graphics;Audio</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/tixl3d/examples</PackageProjectUrl>
    <RepositoryUrl>https://github.com/tixl3d/examples</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
  </PropertyGroup>
</Project>
```

---

## 🚀 Running Examples

### Basic Execution

**⚡ Quick Run Commands**

```bash
# Navigate to example directory
cd examples/graphics/pbr-materials

# Restore dependencies
dotnet restore

# Build example
dotnet build --configuration Release

# Run example
dotnet run

# Or with specific parameters
dotnet run -- --fullscreen --quality=high --vsync=true

# Debug mode
dotnet run --configuration Debug
```

**📁 Example Project Structure**

```
pbr-materials/
├── src/
│   ├── Program.cs              # Entry point
│   ├── PbrMaterialRenderer.cs  # Main renderer
│   ├── Materials/              # Material definitions
│   ├── Shaders/                # Shader files
│   └── Utils/                  # Utility classes
├── assets/
│   ├── textures/              # Texture files
│   ├── models/                # 3D models
│   └── config/                # Configuration files
├── tests/
│   ├── PbrMaterialTests.cs    # Unit tests
│   └── PerformanceTests.cs    # Performance tests
├── docs/
│   ├── README.md              # Documentation
│   ├── TUTORIAL.md            # Tutorial
│   └── ARCHITECTURE.md        # Architecture docs
├── scripts/
│   ├── build.sh              # Build script
│   ├── run.sh                # Run script
│   └── test.sh               # Test script
├── pbr-materials.csproj       # Project file
├── .tixl-example.json        # Example metadata
└── LICENSE                   # License file
```

### Command Line Options

**🎮 Example CLI Parameters**

```csharp
// Program.cs - CLI argument parsing
using System.CommandLine;

var rootCommand = new RootCommand("TiXL PBR Materials Example");

var fullscreenOption = new Option<bool>("--fullscreen", "Run in fullscreen mode");
var qualityOption = new Option<QualityLevel>("--quality", "Set rendering quality") { 
    IsRequired = false 
};
qualityOption.AddAlias("-q");
qualityOption.SetDefaultValue(QualityLevel.Medium);

var vsyncOption = new Option<bool>("--vsync", "Enable VSync");
var vsyncOptionAdd = new Option<bool>("--no-vsync", "Disable VSync");

var debugOption = new Option<bool>("--debug", "Enable debug mode");
debugOption.AddAlias("-d");

rootCommand.AddOption(fullscreenOption);
rootCommand.AddOption(qualityOption);
rootCommand.AddOption(vsyncOption);
rootCommand.AddOption(debugOption);

rootCommand.SetHandler(async (fullscreen, quality, vsync, debug) =>
{
    var config = new PbrMaterialConfig
    {
        Fullscreen = fullscreen,
        Quality = quality,
        VSync = vsync,
        DebugMode = debug
    };

    using var app = new PbrMaterialApp(config);
    await app.RunAsync();
}, fullscreenOption, qualityOption, vsyncOption, debugOption);

return await rootCommand.InvokeAsync(args);
```

**💻 Usage Examples**

```bash
# Basic usage
dotnet run

# Fullscreen high quality
dotnet run -- --fullscreen --quality=high

# Debug mode with verbose output
dotnet run -- --debug -q medium

# Windowed mode, no VSync
dotnet run -- --no-vsync

# Custom configuration file
dotnet run -- --config custom-config.json

# Benchmark mode (headless)
dotnet run -- --benchmark --duration=30
```

### Headless Execution

**🤖 Automated Testing & Benchmarking**

```bash
# Run benchmarks (headless)
dotnet run -- --benchmark --duration=60 --output=benchmark-results.json

# Automated testing
dotnet run -- --test-mode --validate-all

# Performance profiling
dotnet run -- --profile --save-results

# CI/CD integration
dotnet run -- --ci-mode --strict-validation
```

```csharp
// Benchmark mode implementation
public class BenchmarkMode
{
    public async Task RunBenchmarkAsync(TimeSpan duration)
    {
        var stopwatch = Stopwatch.StartNew();
        var frameCount = 0;
        var frameTimes = new List<double>();
        
        while (stopwatch.Elapsed < duration)
        {
            var frameStart = Stopwatch.StartNew();
            
            // Run one frame
            await RenderFrameAsync();
            
            frameStart.Stop();
            frameTimes.Add(frameStart.Elapsed.TotalMilliseconds);
            frameCount++;
        }
        
        // Generate report
        GenerateBenchmarkReport(frameTimes, frameCount);
    }
}
```

---

## 🔧 Troubleshooting

### Common Issues

**❌ Build Errors**

```bash
# Error: TiXL package not found
# Solution: Check TiXL SDK installation
dotnet tool update -g TiXL.SDK
tixl --version

# Error: Version conflict
# Solution: Clear NuGet cache
dotnet nuget locals all --clear
dotnet restore --force

# Error: Missing dependencies
# Solution: Update packages
dotnet list package --outdated
dotnet add package TiXL.Graphics --version latest
```

**❌ Runtime Errors**

```bash
# Error: GPU not supported
# Check GPU compatibility
dotnet run -- --list-gpu-info

# Error: Out of memory
# Reduce quality settings
dotnet run -- --quality=low --texture-size=1024

# Error: Missing assets
# Check assets directory
ls assets/
```

### Performance Issues

**🐌 Low FPS Troubleshooting**

```bash
# Check performance metrics
dotnet run -- --show-stats

# Reduce quality for testing
dotnet run -- --quality=low --no-post-processing

# Enable performance mode
dotnet run -- --performance-mode --log-level=info

# Monitor GPU usage
dotnet run -- --gpu-monitor
```

**🔍 Performance Analysis**

```csharp
// Built-in performance profiler
public class PerformanceProfiler
{
    public void StartFrame()
    {
        _frameTimer.Restart();
    }
    
    public void EndFrame()
    {
        _frameTimer.Stop();
        _frameTimes.Add(_frameTimer.Elapsed.TotalMilliseconds);
        
        if (_frameTimes.Count % 60 == 0)
        {
            ReportPerformance();
        }
    }
}
```

### Debugging Tools

**🐛 Debug Mode Features**

```bash
# Enable debug mode
dotnet run -- --debug

# Verbose logging
dotnet run -- --debug --log-level=debug

# GPU debugging info
dotnet run -- --debug --gpu-info

# Memory analysis
dotnet run -- --debug --memory-profile

# Shader debugging
dotnet run -- --debug --shader-debug
```

**🔍 Debug Console**

```csharp
// In-example debug console
public class DebugConsole
{
    private readonly Dictionary<string, Command> _commands;
    
    public void RegisterCommand(string name, Command command)
    {
        _commands[name] = command;
    }
    
    public void ExecuteCommand(string commandLine)
    {
        var parts = commandLine.Split(' ');
        var commandName = parts[0];
        var args = parts.Skip(1).ToArray();
        
        if (_commands.TryGetValue(commandName, out var command))
        {
            command.Execute(args);
        }
        else
        {
            Console.WriteLine($"Unknown command: {commandName}");
            ShowHelp();
        }
    }
    
    private void ShowHelp()
    {
        Console.WriteLine("Available commands:");
        foreach (var kvp in _commands)
        {
            Console.WriteLine($"  {kvp.Key} - {kvp.Value.Description}");
        }
    }
}
```

---

## 📚 Learning Resources

### Tutorial Videos

**🎥 Getting Started Series**

<div align="center">

| Video | Duration | Description | Link |
|-------|----------|-------------|------|
| **Setup & Installation** | 15 min | Complete setup guide | [📹 Watch](https://youtube.com/watch?v=setup-guide) |
| **First Graphics Example** | 25 min | PBR materials tutorial | [📹 Watch](https://youtube.com/watch?v=pbr-tutorial) |
| **Audio Synthesis Basics** | 30 min | Building a synthesizer | [📹 Watch](https://youtube.com/watch?v=audio-synthesis) |
| **Data Visualization** | 20 min | Real-time charts | [📹 Watch](https://youtube.com/watch?v=data-viz) |
| **Performance Optimization** | 35 min | Advanced techniques | [📹 Watch](https://youtube.com/watch?v=performance) |

</div>

### Documentation

**📖 Essential Documentation**

- **[TiXL Core Documentation](https://docs.tixl3d.com/core/)** - Core platform features
- **[Graphics Programming Guide](https://docs.tixl3d.com/graphics/)** - Graphics and rendering
- **[Audio Processing Guide](https://docs.tixl3d.com/audio/)** - Audio development
- **[Performance Tuning](https://docs.tixl3d.com/performance/)** - Optimization techniques
- **[API Reference](https://docs.tixl3d.com/api/)** - Complete API documentation

### Community Support

**💬 Getting Help**

- **Discord**: [Join TiXL Community](https://discord.gg/YmSyQdeH3S)
- **GitHub Issues**: [Report problems](https://github.com/tixl3d/examples/issues)
- **Stack Overflow**: [Ask questions](https://stackoverflow.com/questions/tagged/tixl)
- **Reddit**: [r/TiXL community](https://reddit.com/r/tixl)

---

## 🎯 Next Steps

### After Setup

**🚀 Quick Start Path**

1. **Run Your First Example** (10 minutes)
   ```bash
   cd examples/graphics/hello-world
   dotnet run
   ```

2. **Explore Graphics Examples** (30 minutes)
   ```bash
   cd examples/graphics
   ls
   # Try: pbr-materials, dynamic-lighting, post-processing
   ```

3. **Try Audio Examples** (20 minutes)
   ```bash
   cd examples/audio
   # Try: live-synthesizer, spectrum-analyzer
   ```

4. **Build Your Own** (60+ minutes)
   ```bash
   tixl examples create my-first-project --template beginner
   ```

### Advanced Usage

**🔧 Customization**

- Modify example parameters
- Combine multiple examples
- Create your own variations
- Share with the community

**📈 Contribution**

- Submit your examples
- Improve existing ones
- Help with documentation
- Join the community

---

<div align="center">

### 🎉 **You're All Set! Start Exploring TiXL Examples!** 🎉

**[Browse Examples](featured-showcases.md)** | **[Join Community](https://discord.gg/YmSyQdeH3S)** | **[Create Your Own](../contributing/submission-guidelines.md)**

---

*Download & Setup Guide | Last Updated: November 2, 2025 | Total Examples: 75+ | Average Setup Time: 15 minutes*

</div>
