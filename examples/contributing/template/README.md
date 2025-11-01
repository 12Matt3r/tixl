# TiXL Example Template

A comprehensive template for creating high-quality TiXL examples that meet our quality standards and provide excellent educational value.

---

## 🚀 Quick Start

### Using the Template

```bash
# Option 1: Use the template directly
npx degit tixl3d/examples-template my-awesome-example
cd my-awesome-example

# Option 2: Download and customize
git clone https://github.com/tixl3d/examples-template.git my-awesome-example
cd my-awesome-example
rm -rf .git  # Remove template's git history

# Option 3: Copy template files manually
cp -r /path/to/template/* /path/to/new/example/
```

### Template Structure

```
my-awesome-example/
├── README.md                    # Main documentation (REQUIRED)
├── LICENSE                      # License file (REQUIRED)
├── my-awesome-example.csproj    # Project file (REQUIRED)
├── .tixl-example.json          # Example metadata (REQUIRED)
├── .editorconfig               # Code formatting rules
├── src/                        # Source code (REQUIRED)
│   ├── Program.cs              # Entry point (REQUIRED)
│   ├── MyExample.cs            # Main example class
│   ├── README.md               # Module documentation
│   └── Utils/                  # Utility classes
├── tests/                      # Test project (REQUIRED)
│   ├── MyExampleTests.cs       # Unit tests (REQUIRED)
│   ├── PerformanceTests.cs     # Performance tests
│   └── README.md               # Test documentation
├── assets/                     # Visual assets
│   ├── images/                 # Screenshots
│   │   ├── main-interface.png
│   │   ├── feature-demo.png
│   │   └── performance-metrics.png
│   ├── videos/                 # Demo videos
│   │   └── demo-30s.mp4
│   └── data/                   # Sample data
│       └── sample-config.json
├── docs/                       # Additional documentation
│   ├── TUTORIAL.md             # Step-by-step tutorial (REQUIRED)
│   ├── ARCHITECTURE.md         # Technical architecture (REQUIRED)
│   └── CHANGELOG.md            # Version history (REQUIRED)
├── scripts/                    # Build and automation
│   ├── build.sh               # Build script
│   ├── run.sh                 # Run script
│   ├── test.sh                # Test script
│   └── validate.sh            # Validation script
└── .gitignore                 # Git ignore rules
```

---

## 📝 Template Files

### README.md Template

```markdown
# My Awesome TiXL Example

A [complexity] example demonstrating [key features] using the TiXL real-time motion graphics platform.

<div align="center">

![Example Preview](assets/images/main-interface.png)

**Complexity**: [Beginner/Intermediate/Advanced/Expert] | **Category**: [Graphics/Audio/Math/UI/Performance/Integration]

</div>

## 🎯 What You'll Learn

By completing this example, you will:

- 🎨 Learn [specific concept 1]
- 🚀 Understand [specific concept 2] 
- ⚡ Master [specific concept 3]
- 🔧 Apply [specific concept 4] to real-world scenarios

## 📋 Prerequisites

- **TiXL**: Version 2.1.0 or later
- **.NET**: 9.0 SDK or later
- **Platform**: Windows 10/11, Linux, or macOS 11+
- **GPU**: DirectX 12, Vulkan, or Metal compatible graphics card
- **Knowledge**: [List required skills/knowledge]

## 🚀 Quick Start

### 1. Clone and Setup

```bash
git clone https://github.com/yourusername/my-awesome-example.git
cd my-awesome-example
dotnet restore
```

### 2. Build and Run

```bash
# Build the example
dotnet build --configuration Release

# Run the example
dotnet run --configuration Release

# Or use the provided script
./scripts/run.sh
```

### 3. Verify Installation

The example should:
- ✅ Launch without errors
- ✅ Display the main interface
- ✅ Respond to input controls
- ✅ Run at target frame rate

## 🎮 Controls

| Control | Action |
|---------|--------|
| **Mouse Left** | [Action description] |
| **Mouse Right** | [Action description] |
| **Mouse Wheel** | [Action description] |
| **Space** | [Action description] |
| **Escape** | [Action description] |
| **F1** | Toggle help overlay |
| **F2** | Toggle performance metrics |
| **R** | Reset to default state |

## 📖 Detailed Tutorial

This example is organized into progressive learning modules:

### 🔰 Module 1: Basic Setup (10 minutes)

Learn the fundamental setup and structure...

```csharp
// Code example with detailed explanation
var engine = new TiXLEngine();
await engine.InitializeAsync();
```

**What this does**: [Detailed explanation of the code]

### 🔰 Module 2: Core Concepts (15 minutes)

Explore the core concepts with hands-on examples...

```csharp
// Advanced code example
var result = await RenderFrameAsync(scene);
Console.WriteLine($"Rendered frame in {result.RenderTime}ms");
```

**Key learning points**:
- Point 1 explanation
- Point 2 explanation  
- Point 3 explanation

### 🔰 Module 3: Advanced Features (20 minutes)

Master advanced techniques and optimization...

[Continue with additional modules...]

## 🔧 Customization

### Modifying Parameters

```csharp
// Create custom configuration
var config = new MyExampleConfig
{
    ParticleCount = 10000,
    RenderQuality = QualityLevel.High,
    EnablePostProcessing = true
};

var example = new MyAwesomeExample(config);
```

### Adding New Features

1. **Create new module**: Add classes to `src/Modules/`
2. **Update configuration**: Extend the config class
3. **Add tests**: Create tests in `tests/Modules/`
4. **Document changes**: Update documentation

### Performance Tuning

```csharp
// Optimize for different performance targets
var performanceProfile = PerformanceProfile.Laptop; // Desktop, Mobile, etc.
example.ApplyPerformanceProfile(performanceProfile);
```

## 📊 Performance Information

| Target Platform | FPS | Frame Time | Memory Usage | CPU Usage |
|-----------------|-----|------------|--------------|-----------|
| **High-end GPU** | 60+ | <16.67ms | <512MB | <30% |
| **Mid-range GPU** | 60 | ≈16.67ms | <256MB | <40% |
| **Low-end GPU** | 30 | <33.33ms | <128MB | <50% |

**Optimization Tips**:
- Reduce particle count for better performance on older GPUs
- Enable level-of-detail for complex scenes
- Use object pooling to minimize memory allocation

## 🧪 Testing

### Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run performance tests only
dotnet test --filter "Category=Performance"

# Run specific test class
dotnet test --filter "FullyQualifiedName~MyExampleTests"
```

### Test Coverage

- **Unit Tests**: Core functionality verification
- **Integration Tests**: Component interaction testing
- **Performance Tests**: Benchmark and regression testing
- **UI Tests**: User interface interaction testing

### Manual Testing

Use the provided testing checklist:

```bash
# Run manual testing script
./scripts/manual-test.sh

# This will test:
# ✅ Launch and basic functionality
# ✅ All user controls and interactions  
# ✅ Error handling and recovery
# ✅ Performance under different conditions
# ✅ Cross-platform compatibility
```

## 🔍 Troubleshooting

### Common Issues

**Issue**: Example fails to launch
```
Solution: 
1. Check TiXL installation: `tixl --version`
2. Verify GPU compatibility
3. Check log files in `logs/` directory
```

**Issue**: Poor performance (low FPS)
```
Solution:
1. Reduce quality settings
2. Close other GPU-intensive applications
3. Check GPU driver updates
```

**Issue**: Controls not responding
```
Solution:
1. Ensure window has focus
2. Check for conflicting input handlers
3. Verify platform-specific requirements
```

### Getting Help

- **💬 Discord**: [Join our community](https://discord.gg/YmSyQdeH3S)
- **🐛 Issues**: [Report problems](https://github.com/yourusername/my-awesome-example/issues)
- **📚 Documentation**: [TiXL Documentation](https://docs.tixl3d.com)
- **💡 Discussions**: [Ask questions](https://github.com/yourusername/my-awesome-example/discussions)

## 📄 License

This example is released under the [MIT License](LICENSE).

**Third-party Content**:
- [List any third-party assets and their licenses]

## 🙏 Acknowledgments

- **TiXL Team**: For the amazing platform
- **Community**: For feedback and suggestions
- **Contributors**: [List any contributors]
- **Inspiration**: [Sources of inspiration]

## 📈 Related Examples

**Beginner Level**:
- [Basic Graphics Rendering](https://github.com/tixl3d/examples-basic-graphics) - Learn fundamental graphics concepts
- [Simple Audio Processing](https://github.com/tixl3d/examples-simple-audio) - Audio basics

**Intermediate Level**:  
- [Advanced Shader Effects](https://github.com/tixl3d/examples-advanced-shaders) - Build on shader concepts
- [Performance Optimization](https://github.com/tixl3d/examples-performance) - Optimization techniques

**Advanced Level**:
- [Real-time Ray Tracing](https://github.com/tixl3d/examples-ray-tracing) - Cutting-edge rendering
- [Machine Learning Integration](https://github.com/tixl3d/examples-ml-integration) - AI-powered effects

## 📝 Changelog

See [CHANGELOG.md](docs/CHANGELOG.md) for version history and updates.

---

**Happy Learning! 🎨✨**

*Last updated: [Date] | Example Version: [Version] | TiXL Version: 2.1.0*
```

### .tixl-example.json Metadata

```json
{
  "id": "my-awesome-example",
  "name": "My Awesome Example",
  "version": "1.0.0",
  "description": "A detailed description of what this example demonstrates and why it's valuable for learners",
  "category": "graphics",
  "subcategory": "shaders",
  "complexity": "intermediate",
  "estimatedDuration": "45 minutes",
  "tixlVersion": "2.1.0",
  "tags": [
    "shader",
    "graphics", 
    "post-processing",
    "real-time",
    "performance"
  ],
  "learningObjectives": [
    "Understand real-time shader programming",
    "Implement post-processing effects",
    "Optimize for performance",
    "Apply graphics best practices"
  ],
  "author": {
    "name": "Your Name",
    "email": "your.email@example.com",
    "github": "https://github.com/yourusername",
    "discord": "YourDiscord#1234"
  },
  "license": "MIT",
  "repository": {
    "type": "git",
    "url": "https://github.com/yourusername/my-awesome-example.git",
    "issues": "https://github.com/yourusername/my-awesome-example/issues",
    "discussions": "https://github.com/yourusername/my-awesome-example/discussions"
  },
  "documentation": {
    "readme": "README.md",
    "tutorial": "docs/TUTORIAL.md",
    "architecture": "docs/ARCHITECTURE.md",
    "changelog": "docs/CHANGELOG.md"
  },
  "resources": {
    "screenshots": [
      "assets/images/main-interface.png",
      "assets/images/feature-demo.png",
      "assets/images/performance-metrics.png"
    ],
    "videos": [
      "assets/videos/demo-30s.mp4"
    ],
    "data": [
      "assets/data/sample-config.json"
    ]
  },
  "dependencies": {
    "required": [
      {
        "name": "TiXL.Core",
        "version": "2.1.0",
        "source": "nuget",
        "purpose": "Core TiXL functionality"
      },
      {
        "name": "TiXL.Graphics",
        "version": "2.1.0", 
        "source": "nuget",
        "purpose": "Graphics rendering capabilities"
      }
    ],
    "optional": [
      {
        "name": "TiXL.Audio",
        "version": "2.1.0",
        "source": "nuget",
        "purpose": "Audio processing features"
      }
    ]
  },
  "targets": {
    "frameworks": ["net9.0"],
    "platforms": ["windows", "linux", "macos"],
    "gpu": ["directx12", "vulkan", "metal"],
    "minimumRequirements": {
      "ram": "4GB",
      "gpu": "DirectX 12 compatible",
      "storage": "500MB"
    }
  },
  "performance": {
    "targetFps": 60,
    "targetFrameTime": 16.67,
    "maximumMemoryUsage": "256MB",
    "maximumCpuUsage": 30,
    "profiles": {
      "highEnd": {
        "description": "High-end GPUs (RTX 3070+, RX 6700 XT+)",
        "features": ["ultra-quality", "ray-tracing", "4K-support"]
      },
      "midRange": {
        "description": "Mid-range GPUs (GTX 1660, RX 580)",
        "features": ["high-quality", "1080p", "optimized-shaders"]
      },
      "lowEnd": {
        "description": "Integrated graphics and older GPUs",
        "features": ["medium-quality", "720p", "performance-mode"]
      }
    }
  },
  "features": [
    {
      "name": "Real-time Rendering",
      "description": "Demonstrates real-time graphics rendering with 60+ FPS performance",
      "technicalDetails": "Uses compute shaders for parallel processing",
      "learningValue": "Understands performance optimization techniques"
    },
    {
      "name": "Interactive Controls", 
      "description": "User can interact with parameters in real-time",
      "technicalDetails": "Implements immediate mode GUI with parameter sliders",
      "learningValue": "Shows how to create responsive user interfaces"
    },
    {
      "name": "Performance Monitoring",
      "description": "Built-in performance metrics and visualization",
      "technicalDetails": "Real-time frame time and memory usage tracking",
      "learningValue": "Demonstrates performance analysis techniques"
    }
  ],
  "testing": {
    "unitTestCoverage": 90,
    "integrationTests": true,
    "performanceTests": true,
    "manualTesting": true
  },
  "quality": {
    "educationalValue": 9,
    "codeQuality": 8,
    "documentationQuality": 9,
    "userExperience": 8,
    "innovation": 7
  },
  "submission": {
    "date": "2025-11-02",
    "version": "1.0.0",
    "changelog": "Initial release with core functionality"
  }
}
```

### Project File Template (.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    
    <!-- Assembly Information -->
    <AssemblyTitle>My Awesome TiXL Example</AssemblyTitle>
    <AssemblyDescription>A comprehensive example demonstrating advanced TiXL graphics capabilities</AssemblyDescription>
    <AssemblyCompany>TiXL Examples Gallery</AssemblyCompany>
    <AssemblyProduct>My Awesome Example</AssemblyProduct>
    <AssemblyCopyright>Copyright © 2025</AssemblyCopyright>
    <AssemblyVersion>1.0.0.0</AssemblyVersion>
    <FileVersion>1.0.0.0</FileVersion>
    
    <!-- TiXL Configuration -->
    <TiXLVersion>2.1.0</TiXLVersion>
    <ExampleComplexity>Intermediate</ExampleComplexity>
    <ExampleCategory>Graphics</ExampleCategory>
    
    <!-- Build Configuration -->
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsAsErrors />
    <AnalysisLevel>latest</AnalysisLevel>
    <AnalysisMode>Default</AnalysisMode>
    
    <!-- Publishing Configuration -->
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  </PropertyGroup>

  <ItemGroup>
    <!-- Required TiXL Packages -->
    <PackageReference Include="TiXL.Core" Version="2.1.0" />
    <PackageReference Include="TiXL.Graphics" Version="2.1.0" />
    <PackageReference Include="TiXL.Audio" Version="2.1.0" />
    
    <!-- Testing Frameworks -->
    <PackageReference Include="xunit" Version="2.6.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    
    <!-- Code Quality -->
    <PackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" Version="8.0.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="StyleCop.Analyzers" Version="1.2.0-beta.507">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    
    <!-- Performance Testing -->
    <PackageReference Include="BenchmarkDotNet" Version="0.13.10" />
  </ItemGroup>

  <ItemGroup>
    <!-- Include documentation files -->
    <None Include="README.md" />
    <None Include="docs\**\*.md" />
    
    <!-- Include assets -->
    <Content Include="assets\**\*.*">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
    
    <!-- Include configuration files -->
    <None Include="*.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>

  <ItemGroup>
    <!-- Project reference to test project -->
    <ProjectReference Include="..\tests\MyAwesomeExample.Tests.csproj" />
  </ItemGroup>

  <ItemGroup>
    <!-- NuGet configuration -->
    <None Include="..\NuGet.config" Link="NuGet.config" />
  </ItemGroup>

</Project>
```

### Basic Example Class Template

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TiXL.Core;
using TiXL.Core.Graphics;
using TiXL.Core.IO;

namespace MyAwesomeExample
{
    /// <summary>
    /// Main example class demonstrating TiXL graphics capabilities.
    /// </summary>
    /// <remarks>
    /// This class provides a complete example of how to use TiXL for real-time
    /// graphics rendering with interactive controls and performance monitoring.
    /// </remarks>
    public class MyAwesomeExample : IDisposable
    {
        private readonly ILogger<MyAwesomeExample> _logger;
        private readonly TiXLEngine _engine;
        private readonly ExampleConfiguration _config;
        private bool _isDisposed;

        /// <summary>
        /// Gets whether the example is currently running.
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Gets the current frame count.
        /// </summary>
        public long FrameCount { get; private set; }

        /// <summary>
        /// Gets the current frames per second.
        /// </summary>
        public float CurrentFps { get; private set; }

        /// <summary>
        /// Initializes a new instance of the MyAwesomeExample class.
        /// </summary>
        /// <param name="engine">The TiXL engine instance.</param>
        /// <param name="config">Configuration for the example.</param>
        /// <param name="logger">Logger for diagnostic information.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when engine, config, or logger is null.
        /// </exception>
        public MyAwesomeExample(TiXLEngine engine, ExampleConfiguration config, ILogger<MyAwesomeExample> logger)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Starts the example rendering loop.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the example is already running.
        /// </exception>
        public async Task StartAsync()
        {
            if (IsRunning)
                throw new InvalidOperationException("Example is already running");

            try
            {
                _logger.LogInformation("Starting MyAwesomeExample...");

                // Initialize example-specific resources
                await InitializeResourcesAsync();

                // Start the main rendering loop
                IsRunning = true;
                await RunMainLoopAsync();

                _logger.LogInformation("MyAwesomeExample started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting MyAwesomeExample");
                IsRunning = false;
                throw;
            }
        }

        /// <summary>
        /// Stops the example rendering loop.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task StopAsync()
        {
            if (!IsRunning)
                return;

            _logger.LogInformation("Stopping MyAwesomeExample...");

            IsRunning = false;

            // Cleanup resources
            await CleanupResourcesAsync();

            _logger.LogInformation("MyAwesomeExample stopped");
        }

        /// <summary>
        /// Main rendering loop.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task RunMainLoopAsync()
        {
            var frameTime = new Stopwatch();

            while (IsRunning)
            {
                frameTime.Restart();

                try
                {
                    // Begin frame
                    using var frameToken = _engine.BeginFrame();

                    // Update simulation
                    await UpdateAsync();

                    // Render frame
                    await RenderAsync();

                    // End frame
                    await _engine.EndFrameAsync(frameToken);

                    // Update statistics
                    UpdateStatistics(frameTime.Elapsed);

                    FrameCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in main rendering loop");
                    await HandleFrameErrorAsync(ex);
                }
            }
        }

        /// <summary>
        /// Updates the simulation state.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        protected virtual async Task UpdateAsync()
        {
            // Example update logic
            // - Handle user input
            // - Update simulation parameters
            // - Manage resource lifecycle

            await Task.CompletedTask;
        }

        /// <summary>
        /// Renders the current frame.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        protected virtual async Task RenderAsync()
        {
            // Example rendering logic
            // - Clear render targets
            // - Draw scene objects
            // - Apply post-processing effects
            // - Present to screen

            await Task.CompletedTask;
        }

        /// <summary>
        /// Initializes example-specific resources.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        protected virtual async Task InitializeResourcesAsync()
        {
            _logger.LogInformation("Initializing example resources...");

            // Load textures, shaders, models, etc.
            await LoadAssetsAsync();

            // Create render resources
            await CreateRenderResourcesAsync();

            _logger.LogInformation("Example resources initialized");
        }

        /// <summary>
        /// Cleans up example-specific resources.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        protected virtual async Task CleanupResourcesAsync()
        {
            _logger.LogInformation("Cleaning up example resources...");

            // Dispose of render resources
            await DisposeRenderResourcesAsync();

            // Unload assets
            await UnloadAssetsAsync();

            _logger.LogInformation("Example resources cleaned up");
        }

        /// <summary>
        /// Handles frame rendering errors.
        /// </summary>
        /// <param name="error">The error that occurred.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        protected virtual async Task HandleFrameErrorAsync(Exception error)
        {
            // Log error details
            _logger.LogError(error, "Frame rendering error");

            // Implement error recovery strategies
            // - Retry frame
            // - Fall back to simplified rendering
            // - Graceful shutdown

            await Task.CompletedTask;
        }

        /// <summary>
        /// Updates performance statistics.
        /// </summary>
        /// <param name="frameTime">The time taken to render the frame.</param>
        protected virtual void UpdateStatistics(TimeSpan frameTime)
        {
            // Calculate FPS
            CurrentFps = 1000f / (float)frameTime.TotalMilliseconds;

            // Log performance metrics periodically
            if (FrameCount % 60 == 0) // Every 60 frames
            {
                _logger.LogDebug("Frame {FrameCount}: {FrameTime}ms ({Fps:F1} FPS)",
                    FrameCount, frameTime.TotalMilliseconds, CurrentFps);
            }
        }

        // Placeholder methods for derived classes to implement
        protected virtual Task LoadAssetsAsync() => Task.CompletedTask;
        protected virtual Task CreateRenderResourcesAsync() => Task.CompletedTask;
        protected virtual Task DisposeRenderResourcesAsync() => Task.CompletedTask;
        protected virtual Task UnloadAssetsAsync() => Task.CompletedTask;

        /// <summary>
        /// Disposes of resources used by the example.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            StopAsync().Wait(TimeSpan.FromSeconds(5)); // Wait up to 5 seconds

            _isDisposed = true;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finalizer to ensure cleanup.
        /// </summary>
        ~MyAwesomeExample()
        {
            Dispose();
        }
    }
}
```

### Unit Test Template

```csharp
using Xunit;
using Microsoft.Extensions.Logging;
using TiXL.Core;
using MyAwesomeExample;

namespace MyAwesomeExample.Tests
{
    /// <summary>
    /// Unit tests for MyAwesomeExample.
    /// </summary>
    public class MyAwesomeExampleTests : IDisposable
    {
        private readonly TiXLEngine _engine;
        private readonly ExampleConfiguration _config;
        private readonly ILogger<MyAwesomeExampleTests> _logger;
        private readonly MyAwesomeExample _example;

        public MyAwesomeExampleTests()
        {
            _engine = TiXLEngine.CreateTestEngine();
            _config = new ExampleConfiguration
            {
                Width = 1920,
                Height = 1080,
                TargetFps = 60
            };
            _logger = NullLogger<MyAwesomeExampleTests>.Instance;
            _example = new MyAwesomeExample(_engine, _config, _logger);
        }

        [Fact]
        public void Constructor_ValidParameters_CreatesInstance()
        {
            // Arrange & Act
            var example = new MyAwesomeExample(_engine, _config, _logger);

            // Assert
            Assert.NotNull(example);
            Assert.False(example.IsRunning);
        }

        [Fact]
        public void Constructor_NullEngine_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MyAwesomeExample(null!, _config, _logger));
        }

        [Fact]
        public void Constructor_NullConfig_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MyAwesomeExample(_engine, null!, _logger));
        }

        [Fact]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MyAwesomeExample(_engine, _config, null!));
        }

        [Fact]
        public async Task StartAsync_NotRunning_SetsRunningState()
        {
            // Act
            await _example.StartAsync();

            // Assert
            Assert.True(_example.IsRunning);
        }

        [Fact]
        public async Task StartAsync_AlreadyRunning_ThrowsInvalidOperationException()
        {
            // Arrange
            await _example.StartAsync();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _example.StartAsync());
        }

        [Fact]
        public async Task StopAsync_Running_SetsRunningStateToFalse()
        {
            // Arrange
            await _example.StartAsync();
            Assert.True(_example.IsRunning);

            // Act
            await _example.StopAsync();

            // Assert
            Assert.False(_example.IsRunning);
        }

        [Fact]
        public async Task StartAndStop_Lifecycle_WorksCorrectly()
        {
            // Act - Start
            await _example.StartAsync();
            Assert.True(_example.IsRunning);

            // Act - Stop
            await _example.StopAsync();
            Assert.False(_example.IsRunning);
        }

        [Fact]
        public async Task FrameRendering_PerformanceMeetsTarget()
        {
            // Arrange
            const int targetFrameTime = 16; // 60 FPS
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            await _example.StartAsync();

            // Wait a few frames
            await Task.Delay(100);

            stopwatch.Stop();

            // Cleanup
            await _example.StopAsync();

            // Assert - Should be running and responsive
            Assert.True(_example.IsRunning);
            Assert.True(_example.FrameCount > 0);
        }

        [Fact]
        public void Dispose_DisposesResources()
        {
            // Act
            _example.Dispose();

            // Assert - Should not throw and should be disposed
            _example.Dispose(); // Should be safe to call multiple times
        }

        public void Dispose()
        {
            _example?.Dispose();
        }
    }
}
```

### Performance Test Template

```csharp
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.Logging;
using TiXL.Core;
using MyAwesomeExample;

namespace MyAwesomeExample.Tests.Performance
{
    /// <summary>
    /// Performance benchmarks for MyAwesomeExample.
    /// </summary>
    [MemoryDiagnoser]
    public class MyAwesomeExampleBenchmarks
    {
        private TiXLEngine _engine = null!;
        private ExampleConfiguration _config = null!;
        private ILogger<MyAwesomeExampleBenchmarks> _logger = null!;

        [GlobalSetup]
        public void Setup()
        {
            _engine = TiXLEngine.CreateTestEngine();
            _config = new ExampleConfiguration
            {
                Width = 1920,
                Height = 1080,
                TargetFps = 60
            };
            _logger = NullLogger<MyAwesomeExampleBenchmarks>.Instance;
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _engine?.Dispose();
        }

        [Benchmark]
        public async Task ExampleInitialization()
        {
            var example = new MyAwesomeExample(_engine, _config, _logger);
            await example.StartAsync();
            await example.StopAsync();
        }

        [Benchmark]
        public async Task FrameUpdate()
        {
            var example = new MyAwesomeExample(_engine, _config, _logger);
            await example.StartAsync();

            // Simulate a few frames
            for (int i = 0; i < 10; i++)
            {
                // Simulate frame update
                await Task.Delay(1);
            }

            await example.StopAsync();
        }

        [Benchmark]
        public async Task FrameRender()
        {
            var example = new MyAwesomeExample(_engine, _config, _logger);
            await example.StartAsync();

            // Simulate rendering
            for (int i = 0; i < 10; i++)
            {
                // Simulate frame rendering
                await Task.Delay(1);
            }

            await example.StopAsync();
        }

        [Benchmark]
        public async Task MemoryAllocationTest()
        {
            // Test memory allocation patterns
            var example = new MyAwesomeExample(_engine, _config, _logger);
            await example.StartAsync();

            // Create and destroy objects to test allocation patterns
            for (int i = 0; i < 100; i++)
            {
                var obj = new PerformanceTestObject();
                // Use the object
                obj.Dispose();
            }

            await example.StopAsync();
        }

        [MemoryDiagnoser]
        public class MemoryAllocationBenchmarks
        {
            [Benchmark]
            public void ObjectPoolUsage()
            {
                var pool = new ObjectPool<PerformanceTestObject>(() => new PerformanceTestObject());

                // Allocate and release objects
                for (int i = 0; i < 1000; i++)
                {
                    var obj = pool.Get();
                    // Use the object
                    pool.Return(obj);
                }
            }

            [Benchmark]
            public void DirectAllocation()
            {
                // Direct allocation without pooling
                for (int i = 0; i < 1000; i++)
                {
                    using var obj = new PerformanceTestObject();
                    // Use the object
                }
            }
        }
    }

    /// <summary>
    /// Test object for performance testing.
    /// </summary>
    public class PerformanceTestObject : IDisposable
    {
        private readonly byte[] _data = new byte[1024]; // 1KB test data

        public void Dispose()
        {
            // Cleanup
        }
    }

    /// <summary>
    /// Simple object pool for benchmarking.
    /// </summary>
    public class ObjectPool<T> where T : class, IDisposable
    {
        private readonly Func<T> _factory;
        private readonly Queue<T> _pool = new();

        public ObjectPool(Func<T> factory)
        {
            _factory = factory;
        }

        public T Get()
        {
            return _pool.Count > 0 ? _pool.Dequeue() : _factory();
        }

        public void Return(T item)
        {
            _pool.Enqueue(item);
        }
    }

    /// <summary>
    /// Entry point for running benchmarks.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<MyAwesomeExampleBenchmarks>();
            Console.WriteLine(summary);
        }
    }
}
```

---

## 🛠️ Build and Automation Scripts

### Build Script

```bash
#!/bin/bash
# scripts/build.sh

set -e

echo "🔨 Building MyAwesomeExample..."

# Check prerequisites
echo "Checking prerequisites..."
if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET SDK not found. Please install .NET 9.0 SDK."
    exit 1
fi

# Restore dependencies
echo "📦 Restoring dependencies..."
dotnet restore

# Run tests before building
echo "🧪 Running tests..."
dotnet test --configuration Debug --verbosity quiet

# Build the project
echo "🏗️ Building project..."
dotnet build --configuration Release --no-restore

# Run validation
echo "✅ Running validation..."
./scripts/validate.sh

echo "✅ Build completed successfully!"

# Show build artifacts
echo ""
echo "📁 Build artifacts:"
echo "  - Executable: bin/Release/net9.0/MyAwesomeExample"
echo "  - PDB files: bin/Release/net9.0/"
echo "  - Documentation: docs/"
```

### Validation Script

```bash
#!/bin/bash
# scripts/validate.sh

echo "🔍 Running validation checks..."

# Code formatting
echo "Checking code formatting..."
if ! dotnet format --verify-no-changes --verbosity quiet; then
    echo "❌ Code formatting issues found. Run 'dotnet format' to fix."
    exit 1
fi
echo "✅ Code formatting: OK"

# Static analysis
echo "Running static analysis..."
if ! dotnet build --configuration Release /p:EnforceCodeStyleInBuild=true; then
    echo "❌ Static analysis failed."
    exit 1
fi
echo "✅ Static analysis: OK"

# Test coverage
echo "Checking test coverage..."
COVERAGE=$(dotnet test --configuration Release --collect:"XPlat Code Coverage" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover | grep -o "Line coverage: [0-9.]*" | cut -d' ' -f3)
if (( $(echo "$COVERAGE < 80" | bc -l) )); then
    echo "❌ Test coverage too low: $COVERAGE% (minimum: 80%)"
    exit 1
fi
echo "✅ Test coverage: $COVERAGE%"

# Documentation validation
echo "Validating documentation..."
if [ ! -f "README.md" ]; then
    echo "❌ README.md missing"
    exit 1
fi
if [ ! -f "docs/TUTORIAL.md" ]; then
    echo "❌ Tutorial.md missing"
    exit 1
fi
echo "✅ Documentation: OK"

echo "✅ All validation checks passed!"
```

---

## 🎯 Quick Customization Guide

### 1. Replace Template Content

Search and replace these placeholders in all files:

- `MyAwesomeExample` → Your example name
- `my-awesome-example` → Your example ID
- `Your Name` → Your name
- `your.email@example.com` → Your email
- `yourusername` → Your GitHub username

### 2. Update Configuration

Edit `.tixl-example.json` with your specific details:
- Category and complexity level
- Dependencies and requirements
- Performance targets
- Feature descriptions

### 3. Implement Your Logic

Replace the template methods in `src/MyExample.cs`:
- `UpdateAsync()` - Your update logic
- `RenderAsync()` - Your rendering logic
- `InitializeResourcesAsync()` - Your resource loading

### 4. Add Your Tests

Extend the test templates in `tests/MyExampleTests.cs`:
- Unit tests for your logic
- Performance benchmarks
- Integration tests

### 5. Create Documentation

Fill in the documentation templates:
- Detailed tutorial in `docs/TUTORIAL.md`
- Technical architecture in `docs/ARCHITECTURE.md`
- Update `README.md` with your example content

---

<div align="center">

### 🎨 **Ready to Create an Amazing Example?** 🎨

**[Download Template](https://github.com/tixl3d/examples-template/archive/main.zip)** | **[View Examples](https://github.com/tixl3d/examples-gallery)** | **[Get Help](https://discord.gg/YmSyQdeH3S)**

---

*Example Template | Last Updated: November 2, 2025 | Version: 2.1.0*

</div>
