# Example Submission Guidelines

Thank you for your interest in contributing to the TiXL Examples Gallery! This guide will walk you through everything you need to know to create and submit high-quality examples that inspire and educate the community.

---

## 📋 Quick Checklist

Before submitting your example, ensure you have:

- [ ] ✅ **TiXL-compatible code** using TiXL 2.1.0+
- [ ] ✅ **Complete documentation** with clear explanations
- [ ] ✅ **Working example project** that builds and runs
- [ ] ✅ **Quality code** following TiXL coding standards
- [ ] ✅ **Tests** covering main functionality
- [ ] ✅ **Screenshots/video** demonstrating the example
- [ ] ✅ **README.md** with setup and usage instructions
- [ ] ✅ **License** clearly specified (MIT recommended)

---

## 🎯 What Makes a Great Example

### Essential Qualities

**🎓 Educational Value**
- Clear learning objectives
- Step-by-step explanations
- Progressive complexity
- Real-world applicability

**💻 Code Quality**
- Clean, readable code
- Comprehensive comments
- Proper error handling
- Performance considerations

**🎨 User Experience**
- Easy setup and installation
- Intuitive controls and interface
- Helpful documentation
- Error messages and troubleshooting

**⚡ Performance**
- Optimized for real-time operation
- Memory-efficient implementation
- Cross-platform compatibility
- Graceful degradation

### Example Categories

**📊 By Complexity**

| Level | Description | Requirements |
|-------|-------------|--------------|
| 🟢 **Beginner** | Simple concepts, well-documented | Clear explanations, basic operations |
| 🟡 **Intermediate** | Moderate complexity, building concepts | Advanced features, optimization hints |
| 🔴 **Advanced** | Complex systems, cutting-edge techniques | Performance tuning, architectural decisions |
| ⚡ **Expert** | Novel techniques, research applications | Innovation documentation, benchmarks |

**🏷️ By Type**

- **Graphics & Rendering**: Shaders, materials, lighting, post-processing
- **Audio Processing**: Synthesis, effects, analysis, spatial audio
- **Mathematical & Data**: Algorithms, visualizations, transformations
- **UI & Interaction**: Interfaces, controls, animations, responsiveness
- **Performance & Optimization**: Profiling, memory management, GPU techniques
- **Integration & Tools**: Plugins, APIs, testing, deployment

---

## 📁 Project Structure

### Required Files

```
my-awesome-example/
├── README.md                    # Main documentation
├── LICENSE                      # License file
├── my-awesome-example.csproj    # Project file
├── src/                         # Source code
│   ├── Program.cs              # Entry point
│   ├── MyExample.cs            # Main example class
│   └── README.md               # Module documentation
├── tests/                       # Test project
│   ├── MyExampleTests.cs       # Unit tests
│   └── README.md               # Test documentation
├── assets/                      # Resources and data
│   ├── images/                 # Screenshots and artwork
│   ├── videos/                 # Demo videos
│   └── data/                   # Sample data files
├── docs/                        # Additional documentation
│   ├── CHANGELOG.md            # Version history
│   ├── ARCHITECTURE.md         # Technical details
│   └── TUTORIAL.md             # Step-by-step guide
├── scripts/                     # Build and automation
│   ├── build.sh               # Build script
│   ├── run.sh                 # Run script
│   └── test.sh                # Test script
└── .tixl-example.json         # Example metadata
```

### File Templates

#### README.md Template

```markdown
# My Awesome TiXL Example

Brief description of what this example demonstrates.

## 🎯 What You'll Learn

- Learning objective 1
- Learning objective 2
- Learning objective 3

## 🚀 Quick Start

### Prerequisites

- TiXL 2.1.0 or later
- .NET 9.0 or later
- DirectX 12 compatible GPU

### Installation

```bash
git clone <your-repo-url>
cd my-awesome-example
dotnet restore
dotnet run
```

## 📖 Detailed Tutorial

[Link to detailed tutorial or include tutorial content here]

## 🎮 Controls

- **Key/Action**: Description of what it does
- **Mouse/Interface**: Interaction methods
- **Parameters**: Configurable options

## 📊 Performance Notes

- Target frame rate: 60 FPS
- GPU requirements: DirectX 12
- Memory usage: ~100MB
- CPU impact: Low/Medium/High

## 🔧 Customization

[Explanation of how to modify and extend the example]

## 📝 Code Highlights

[Key parts of the code with explanations]

## 🐛 Troubleshooting

[Common issues and solutions]

## 📄 License

[Your license choice and attribution requirements]

## 🙏 Acknowledgments

[Credits to contributors, libraries, inspiration sources]
```

#### .tixl-example.json Metadata

```json
{
  "id": "my-awesome-example",
  "name": "My Awesome Example",
  "version": "1.0.0",
  "description": "A brief description of the example",
  "category": "graphics",
  "subcategory": "shaders",
  "complexity": "intermediate",
  "tixlVersion": "2.1.0",
  "tags": ["shader", "graphics", "post-processing"],
  "author": {
    "name": "Your Name",
    "email": "your.email@example.com",
    "github": "https://github.com/yourusername"
  },
  "license": "MIT",
  "repository": {
    "type": "git",
    "url": "https://github.com/yourusername/my-awesome-example.git"
  },
  "documentation": {
    "readme": "README.md",
    "tutorial": "docs/TUTORIAL.md",
    "architecture": "docs/ARCHITECTURE.md"
  },
  "resources": {
    "screenshots": ["assets/images/screenshot1.png"],
    "videos": ["assets/videos/demo.mp4"],
    "data": ["assets/data/sample.txt"]
  },
  "dependencies": {
    "required": [
      {
        "name": "TiXL.Core",
        "version": "2.1.0",
        "source": "nuget"
      }
    ],
    "optional": [
      {
        "name": "TiXL.Graphics",
        "version": "2.1.0",
        "source": "nuget"
      }
    ]
  },
  "targets": {
    "frameworks": ["net9.0"],
    "platforms": ["windows", "linux", "macos"],
    "gpu": ["directx12", "vulkan", "metal"]
  },
  "features": [
    {
      "name": "Real-time Rendering",
      "description": "Demonstrates real-time graphics rendering"
    }
  ]
}
```

---

## 💻 Code Standards

### C# Coding Conventions

**File Organization**

```csharp
using System;
using TiXL.Core;
using TiXL.Graphics;

// Namespace should match project structure
namespace MyAwesomeExample.Graphics
{
    /// <summary>
    /// Brief description of the class.
    /// </summary>
    /// <remarks>
    /// Additional details and usage examples.
    /// </remarks>
    public class MyExampleRenderer : IDisposable
    {
        // Constants and static fields
        public const string DefaultShaderName = "MyShader";
        private static readonly Random _random = new();

        // Instance fields (camelCase with underscore)
        private readonly TiXLEngine _engine;
        private readonly ILogger<MyExampleRenderer> _logger;
        private bool _isDisposed;

        // Properties (PascalCase)
        public bool IsRendering { get; private set; }
        public int FrameCount { get; private set; }

        // Constructor
        public MyExampleRenderer(TiXLEngine engine, ILogger<MyExampleRenderer> logger)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _logger.LogInformation("MyExampleRenderer initialized");
        }

        // Public methods (PascalCase)
        public async Task StartRenderingAsync()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(MyExampleRenderer));

            _logger.LogInformation("Starting rendering...");
            
            try
            {
                IsRendering = true;
                await RenderLoopAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during rendering");
                throw;
            }
        }

        // Private methods (PascalCase with underscore prefix)
        private async Task RenderLoopAsync()
        {
            var frameToken = _engine.BeginFrame();
            
            try
            {
                while (IsRendering && !_isDisposed)
                {
                    await RenderFrameAsync();
                    FrameCount++;
                    
                    await _engine.EndFrameAsync(frameToken);
                    frameToken = _engine.BeginFrame();
                }
            }
            finally
            {
                await _engine.EndFrameAsync(frameToken);
            }
        }

        // Dispose pattern implementation
        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                IsRendering = false;
                GC.SuppressFinalize(this);
            }
        }

        // Finalizer (if needed)
        ~MyExampleRenderer()
        {
            Dispose();
        }
    }
}
```

### Documentation Standards

**XML Documentation**

```csharp
/// <summary>
/// Creates a new instance of MyShaderMaterial with specified parameters.
/// </summary>
/// <param name="engine">The TiXL engine instance for shader compilation.</param>
/// <param name="shaderSource">GLSL source code for the shader.</param>
/// <param name="parameters">Initial shader parameters.</param>
/// <exception cref="ArgumentNullException">
/// Thrown when <paramref name="engine"/> or <paramref name="shaderSource"/> is null.
/// </exception>
/// <exception cref="ShaderCompilationException">
/// Thrown when shader compilation fails.
/// </exception>
/// <returns>A new <see cref="MyShaderMaterial"/> instance.</returns>
/// <example>
/// <code>
/// var material = new MyShaderMaterial(engine, shaderSource, new ShaderParameters {
///     TimeScale = 1.0f,
///     ColorIntensity = 0.8f
/// });
/// </code>
/// </example>
public MyShaderMaterial CreateMaterial(TiXLEngine engine, string shaderSource, ShaderParameters parameters)
```

### Error Handling

**Comprehensive Error Handling**

```csharp
public async Task<Result<RenderTarget>> CreateRenderTargetAsync(int width, int height)
{
    try
    {
        // Validation
        if (width <= 0 || height <= 0)
            return Result<RenderTarget>.Failure("Invalid dimensions");
            
        if (width > MaxTextureSize || height > MaxTextureSize)
            return Result<RenderTarget>.Failure($"Dimensions exceed maximum ({MaxTextureSize})");

        // Create render target
        var renderTarget = new RenderTarget(width, height);
        
        // Initialize
        var initialized = await renderTarget.InitializeAsync(_engine);
        if (!initialized)
            return Result<RenderTarget>.Failure("Failed to initialize render target");

        _logger.LogInformation("Created render target: {Width}x{Height}", width, height);
        return Result<RenderTarget>.Success(renderTarget);
    }
    catch (OutOfMemoryException ex)
    {
        _logger.LogError(ex, "Out of memory while creating render target");
        return Result<RenderTarget>.Failure("Insufficient memory for render target");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error creating render target");
        return Result<RenderTarget>.Failure($"Creation failed: {ex.Message}");
    }
}
```

---

## 🧪 Testing Requirements

### Test Structure

```csharp
using Xunit;
using TiXL.Core;
using MyAwesomeExample;

namespace MyAwesomeExample.Tests
{
    public class MyExampleRendererTests
    {
        private readonly TiXLEngine _engine;
        private readonly ILogger<MyExampleRendererTests> _logger;
        private readonly MyExampleRenderer _renderer;

        public MyExampleRendererTests()
        {
            _engine = TiXLEngine.CreateTestEngine();
            _logger = NullLogger<MyExampleRendererTests>.Instance;
            _renderer = new MyExampleRenderer(_engine, _logger);
        }

        [Fact]
        public void Constructor_ValidEngine_CreatesInstance()
        {
            // Arrange & Act
            var renderer = new MyExampleRenderer(_engine, _logger);
            
            // Assert
            Assert.NotNull(renderer);
            Assert.False(renderer.IsRendering);
            Assert.Equal(0, renderer.FrameCount);
        }

        [Theory]
        [InlineData(1920, 1080)]
        [InlineData(3840, 2160)]
        [InlineData(1280, 720)]
        public void CreateRenderTarget_ValidDimensions_CreatesSuccessfully(int width, int height)
        {
            // Act
            var result = _renderer.CreateRenderTarget(width, height);
            
            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.Equal(width, result.Value.Width);
            Assert.Equal(height, result.Value.Height);
        }

        [Fact]
        public async Task StartRendering_ValidInstance_ChangesState()
        {
            // Act
            var task = _renderer.StartRenderingAsync();
            
            // Assert
            Assert.True(_renderer.IsRendering);
            
            // Cleanup
            _renderer.StopRendering();
            await task;
        }

        [Fact]
        public void CreateRenderTarget_InvalidWidth_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => 
                _renderer.CreateRenderTarget(-1, 1080));
        }
    }
}
```

### Performance Testing

```csharp
[Fact]
public void RenderFrame_PerformanceMeetsTarget()
{
    // Arrange
    const int targetFrameTime = 16; // 60 FPS
    var stopwatch = Stopwatch.StartNew();
    
    // Act
    _renderer.RenderFrame();
    stopwatch.Stop();
    
    // Assert
    Assert.True(stopwatch.ElapsedMilliseconds <= targetFrameTime,
        $"Frame time {stopwatch.ElapsedMilliseconds}ms exceeded target {targetFrameTime}ms");
}

[Theory]
[InlineData(1000)]
[InlineData(10000)]
[InlineData(100000)]
public void ProcessData_LargeDataset_PerformsWithinBudget(int itemCount)
{
    // Arrange
    var data = Enumerable.Range(0, itemCount).Select(i => new DataPoint(i, i * 0.1f)).ToArray();
    var stopwatch = Stopwatch.StartNew();
    
    // Act
    var result = _renderer.ProcessData(data);
    stopwatch.Stop();
    
    // Assert
    Assert.True(stopwatch.ElapsedMilliseconds < 100,
        $"Processing {itemCount} items took {stopwatch.ElapsedMilliseconds}ms, exceeding 100ms budget");
}
```

---

## 🎨 Visual Assets

### Screenshot Requirements

**📸 Required Screenshots**
- Main application/window screenshot
- Feature demonstration screenshot
- Settings/configuration screenshot
- Performance/metrics screenshot (if applicable)

**📏 Technical Specifications**
- Resolution: 1920x1080 or higher
- Format: PNG with transparency where appropriate
- Quality: Lossless compression
- Naming: descriptive filenames (e.g., `main-interface.png`)

### Video Demo

**🎬 Video Specifications**
- Duration: 30-60 seconds
- Resolution: 1920x1080 or higher
- Format: MP4 (H.264 codec)
- Audio: Optional, but recommended
- Compression: Balanced quality/size

**🎯 Video Content Checklist**
- [ ] Show installation process
- [ ] Demonstrate key features
- [ ] Highlight interesting interactions
- [ ] Show performance metrics
- [ ] Include text overlays with key information

### Asset Organization

```
assets/
├── images/
│   ├── screenshots/
│   │   ├── main-interface.png
│   │   ├── feature-demo.png
│   │   └── performance-metrics.png
│   └── artwork/
│       ├── logo.png
│       └── banner.png
├── videos/
│   ├── demo-30s.mp4
│   └── tutorial-5min.mp4
└── data/
    ├── sample-input.txt
    └── configuration.json
```

---

## 📚 Documentation

### Tutorial Structure

```markdown
# Tutorial: [Tutorial Name]

## 🎯 Learning Objectives

By the end of this tutorial, you will:

1. Understand [concept 1]
2. Be able to [task 1]
3. Implement [feature 1]

## 📋 Prerequisites

- [Prerequisite 1 with version]
- [Prerequisite 2 with version]
- Previous tutorial completion (if applicable)

## 🏗️ Project Setup

### Step 1: Create Project

[Detailed steps with code]

### Step 2: Add Dependencies

[Package installation and configuration]

## 🎯 Implementation

### Step 1: Basic Structure

Start by creating the main class:

```csharp
// Your code here with explanations
```

**Explanation**: What this code does and why.

### Step 2: Core Functionality

[Continue with progressive implementation]

## 🔍 Understanding the Code

[Detailed explanation of key concepts]

## 🎮 Testing and Validation

[How to test the implementation]

## 🔧 Customization

[Options for extending and customizing]

## 🚀 Next Steps

[What to explore next]

## 📝 Summary

[Recap of what was learned]
```

---

## 📦 Submission Process

### 1. Preparation Phase

**Code Review Checklist**

- [ ] Code follows TiXL coding standards
- [ ] All tests pass
- [ ] Documentation is complete
- [ ] Assets are optimized
- [ ] Performance is acceptable
- [ ] Cross-platform compatibility verified

**Quality Assurance**

```bash
# Run comprehensive checks
./scripts/quality-check.sh

# This should output:
# ✅ Code formatting: PASS
# ✅ Static analysis: PASS  
# ✅ Unit tests: PASS (95%+ coverage)
# ✅ Integration tests: PASS
# ✅ Performance benchmarks: PASS
# ✅ Documentation: PASS
# ✅ Asset optimization: PASS
```

### 2. Submission Phase

**Via GitHub**

1. **Fork the examples repository**
   ```bash
   git clone https://github.com/tixl3d/examples-gallery.git
   cd examples-gallery
   ```

2. **Create your example directory**
   ```bash
   mkdir -p examples/my-awesome-example
   cd examples/my-awesome-example
   ```

3. **Add your project files**
   - Follow the required structure
   - Include all documentation
   - Add visual assets

4. **Test your submission**
   ```bash
   ./scripts/test-submission.sh my-awesome-example
   ```

5. **Submit pull request**
   - Use descriptive title: "feat: Add My Awesome Example"
   - Include detailed description
   - Reference any related issues

**Via Discord (Alternative)**

- Share your project in the #examples channel
- Include GitHub link or attachment
- Request community feedback
- Maintainers will help with submission

### 3. Review Phase

**Timeline**
- **Community Review**: 1 week
- **Technical Review**: 1 week  
- **Documentation Review**: 3 days
- **Final Approval**: 2 days

**What to Expect**
- Detailed feedback from reviewers
- Requests for improvements
- Questions about implementation
- Integration testing

**Response Expectations**
- Address feedback within 1 week
- Reply to all questions
- Implement requested changes
- Update documentation as needed

### 4. Publication Phase

**Upon Approval**
- Example added to gallery
- Featured projects consideration
- Announcement on social media
- Inclusion in newsletter

**Post-Publication**
- Monitor for issues
- Respond to user questions
- Update for TiXL version changes
- Maintain and enhance

---

## 🏆 Recognition & Rewards

### Contributor Recognition

**🌟 Featured Contributor Program**
- Monthly featured contributor spotlight
- Exclusive Discord roles and channels
- Direct communication with core team
- Early access to new TiXL features

**📜 Certificate of Contribution**
- Digital certificate for high-quality submissions
- LinkedIn/Resume highlighting
- Portfolio showcase
- Academic credit consideration

### Community Awards

**🏅 Quarterly Awards**
- **Best Educational Value**: Most effective learning example
- **Technical Excellence**: Most impressive technical implementation
- **Community Choice**: Most popular community-voted example
- **Innovation Award**: Most novel or creative approach

**🎯 Annual TiXL Awards**
- **Example of the Year**: Overall best example
- **Rising Star**: New contributor with exceptional work
- **Community Champion**: Most helpful in community
- **Documentation Master**: Best documentation and tutorials

---

## ❓ Frequently Asked Questions

### General Questions

**Q: Do I need to know advanced TiXL to contribute?**
A: No! We welcome examples at all complexity levels. Focus on clear documentation and good educational value.

**Q: Can I submit a work-in-progress example?**
A: Yes, but it should be functional and well-documented. Use the WIP label and update regularly.

**Q: What if my example doesn't get approved?**
A: Don't be discouraged! We'll provide detailed feedback. You can revise and resubmit, or contribute to existing examples.

### Technical Questions

**Q: Which TiXL version should I target?**
A: Use the latest stable version (TiXL 2.1.0+) and specify compatibility in your metadata.

**Q: Can I use third-party libraries?**
A: Yes, but ensure they are compatible with TiXL's licensing and provide proper attribution.

**Q: How do I handle platform-specific code?**
A: Use conditional compilation and provide fallbacks. Document platform requirements clearly.

### Legal Questions

**Q: What license should I use?**
A: MIT is recommended for maximum flexibility. Avoid GPL for TiXL ecosystem compatibility.

**Q: Can I include copyrighted content?**
A: Only with proper attribution and within fair use. Provide source links and licenses for all assets.

**Q: Do I retain ownership of my examples?**
A: Yes, you retain full copyright. By submitting, you grant permission for gallery inclusion and distribution.

---

## 📞 Get Help

### During Development

**💬 Discord Community**
- [#examples-general](https://discord.gg/tixl-examples): General discussion
- [#examples-help](https://discord.gg/tixl-help): Technical help
- [#examples-review](https://discord.gg/tixl-review): Pre-submission review

**📖 Documentation**
- [TiXL Documentation](https://docs.tixl3d.com)
- [Examples Gallery Wiki](https://github.com/tixl3d/examples-gallery/wiki)
- [API Reference](https://docs.tixl3d.com/api)

**🐛 Issue Reporting**
- [GitHub Issues](https://github.com/tixl3d/tixl/issues)
- Use labels: `question`, `help wanted`, `example-contribution`

### Before Submission

**🔍 Code Review**
- Use our GitHub Discussions for pre-submission review
- Share screenshots and videos for feedback
- Ask specific technical questions

**📋 Submission Checklist**
- Download and use our [submission template](https://github.com/tixl3d/examples-template)
- Run our [quality checker script](https://github.com/tixl3d/quality-checker)
- Get community feedback in Discord

---

<div align="center">

### 🚀 **Ready to Share Your Amazing Example?** 🚀

**[Start Building](https://github.com/tixl3d/examples-template)** | **[Get Community Help](https://discord.gg/YmSyQdeH3S)** | **[Submit Your Example](https://github.com/tixl3d/examples-gallery/compare)**

---

*Submission Guidelines | Last Updated: November 2, 2025 | Version: 2.1.0*

</div>
