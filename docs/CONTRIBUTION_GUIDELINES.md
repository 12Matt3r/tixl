# TiXL Contribution Guidelines

Thank you for your interest in contributing to TiXL (Tooll 3)! This comprehensive guide will help you understand our development workflow, coding standards, and contribution process. TiXL is a real-time motion graphics platform, and we welcome contributions from both artists and developers.

## Table of Contents

- [Getting Started](#getting-started)
- [Development Environment Setup](#development-environment-setup)
- [Project Structure](#project-structure)
- [Code Style and Standards](#code-style-and-standards)
- [Commit Message Conventions](#commit-message-conventions)
- [Pull Request Process](#pull-request-process)
- [Issue Reporting Guidelines](#issue-reporting-guidelines)
- [Testing Requirements](#testing-requirements)
- [Development Workflow](#development-workflow)
- [Module-Specific Guidelines](#module-specific-guidelines)
- [Code Review Process](#code-review-process)
- [Community Guidelines](#community-guidelines)

## Getting Started

### What is TiXL?

TiXL (Tooll 3) is an open-source platform for creating real-time motion graphics. It combines:
- Real-time rendering with DirectX 12
- Graph-based procedural content generation
- Linear keyframe animation
- Audio-reactive visual creation
- Plugin-based operator system

### Ways to Contribute

- 🐛 **Bug Reports**: Help us identify and fix issues
- 💡 **Feature Requests**: Suggest new operators or improvements
- 📝 **Documentation**: Improve guides, tutorials, and examples
- 🎨 **Operators**: Create new graphics, audio, or utility operators
- 🔧 **Core Development**: Work on engine improvements
- 🖥️ **UI/UX**: Enhance the user interface and experience
- 🧪 **Testing**: Write tests and improve code quality

## Development Environment Setup

### Prerequisites

- **Operating System**: Windows 10/11 (primary development platform)
- **.NET SDK**: .NET 9.0.0 or later
- **IDE**: Visual Studio 2022, Visual Studio Code, or JetBrains Rider
- **GPU**: DirectX 11.3 compatible (GTX 970 or later recommended)
- **Git**: Latest version with Git LFS support

### Initial Setup

1. **Fork the Repository**
   ```bash
   git clone https://github.com/tixl3d/tixl.git
   cd tixl
   git remote add upstream https://github.com/tixl3d/tixl.git
   ```

2. **Build the Solution**
   ```bash
   dotnet restore
   dotnet build --configuration Release
   ```

3. **Run Tests**
   ```bash
   dotnet test
   ```

4. **Launch Editor**
   ```bash
   cd Editor
   dotnet run
   ```

### Development Dependencies

The project uses several external libraries:
- **ImGui.NET**: Immediate-mode GUI framework
- **Silk.NET**: OpenGL/Vulkan bindings
- **Emgu CV**: Computer vision capabilities
- **SharpDX**: DirectX API bindings
- **NDI SDK**: Network Device Interface
- **Spout**: Real-time video sharing

## Project Structure

TiXL follows a modular architecture with clear separation of concerns:

```
├── Core/                    # Fundamental engine components
│   ├── Animation/          # Animation system and curves
│   ├── Audio/              # Audio processing
│   ├── Compilation/        # Build system
│   ├── DataTypes/          # Custom data structures
│   ├── IO/                 # File and network I/O
│   ├── Model/              # Data models
│   ├── Operator/           # Core operator system
│   ├── Rendering/          # 3D graphics engine
│   ├── Resource/           # Asset management
│   └── ...
├── Operators/              # Plugin-based operator system
│   ├── TypeOperators/      # Categorized operators
│   │   ├── Collections/    # Data operations
│   │   ├── Gfx/            # Graphics pipeline
│   │   ├── NET/            # .NET framework
│   │   └── Values/         # Value manipulation
│   ├── examples/           # Operator examples
│   └── user/               # User contributions
├── Editor/                 # User interface & environment
│   ├── App/                # Core application
│   ├── Compilation/        # Built-in compiler
│   ├── Gui/                # UI components
│   │   ├── Graph/          # Node graph visualization
│   │   ├── InputUi/        # Input controls
│   │   ├── OpUis/          # Operator UIs
│   │   └── ...
│   └── Properties/         # Project settings
└── Resources/              # Application resources
```

## Code Style and Standards

### C# Coding Conventions

#### Naming Conventions

```csharp
// Classes, methods, properties: PascalCase
public class RenderTarget
{
    public string Name { get; set; }
    public int Width { get; }
    
    public void Initialize()
    {
        // Method implementation
    }
}

// Fields: camelCase with underscore prefix for private fields
private readonly RenderTarget _renderTarget;
private string _textureName;

// Constants: UPPER_CASE
public const int MAX_TEXTURES = 16;
public const string DEFAULT_SHADER_NAME = "Default";

// Events: PascalCase with On prefix for handlers
public event EventHandler<RenderEventArgs> OnRender;
protected virtual void OnRender(RenderEventArgs e)
{
    OnRender?.Invoke(this, e);
}
```

#### File Organization

```csharp
// File: Core/Rendering/RenderTarget.cs
using System;
using TiXL.Core.IO;
using TiXL.Core.DataTypes;

namespace TiXL.Core.Rendering
{
    /// <summary>
    /// Manages render target resources for graphics operations.
    /// </summary>
    public class RenderTarget : IDisposable
    {
        // Fields (private, then public)
        private readonly IntPtr _nativeHandle;
        
        // Properties
        public int Width { get; }
        public int Height { get; }
        public bool IsDisposed { get; private set; }
        
        // Constructor
        public RenderTarget(int width, int height)
        {
            Width = width;
            Height = height;
            Initialize();
        }
        
        // Methods (public, then protected/private)
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                // Cleanup implementation
                IsDisposed = true;
            }
        }
        
        // Private helper methods
        private void Initialize()
        {
            // Initialization logic
        }
        
        ~RenderTarget()
        {
            Dispose(false);
        }
    }
}
```

#### XML Documentation Comments

```csharp
/// <summary>
/// Applies a shader material to render geometry with specific properties.
/// </summary>
/// <param name="geometry">The geometry to render</param>
/// <param name="material">The material containing shader and parameters</param>
/// <param name="camera">The camera view matrix for rendering</param>
/// <returns>True if rendering succeeded, false otherwise</returns>
/// <exception cref="ArgumentNullException">Thrown when geometry or material is null</exception>
/// <remarks>
/// This method supports both compute and pixel shaders. For compute shaders,
/// ensure the material's compute shader is properly configured.
/// <example>
/// <code>
/// var material = new PbrMaterial();
/// material.LoadShader("Path/To/Shader");
/// var success = renderer.ApplyMaterial(cube, material, camera);
/// </code>
/// </example>
/// </remarks>
public bool ApplyMaterial(Geometry geometry, Material material, Camera camera)
{
    // Implementation
}
```

### HLSL Shader Standards

#### Shader Organization

```hlsl
// Texture2D and SamplerState declarations at the top
Texture2D<float4> DiffuseTexture : register(t0);
SamplerState LinearSampler : register(s0);

// Constant buffers with proper alignment
cbuffer RenderConstants : register(b0)
{
    matrix World;           // 64 bytes
    matrix View;            // 64 bytes
    matrix Projection;      // 64 bytes
    float4 LightDirection;  // 16 bytes
    float4 Time;            // 16 bytes
    // Total: 224 bytes (16-byte aligned)
}

struct VertexInput
{
    float3 Position : POSITION;
    float2 TexCoord : TEXCOORD0;
    float3 Normal   : NORMAL;
};

struct PixelInput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
    float3 Normal   : NORMAL;
    float3 WorldPos : WORLDPOS;
};

// Vertex shader
PixelInput VSMain(VertexInput input)
{
    PixelInput output;
    
    matrix worldViewProj = mul(World, mul(View, Projection));
    output.Position = mul(float4(input.Position, 1.0f), worldViewProj);
    output.TexCoord = input.TexCoord;
    output.Normal = normalize(mul(input.Normal, (float3x3)World));
    output.WorldPos = mul(input.Position, World).xyz;
    
    return output;
}

// Pixel shader
float4 PSMain(PixelInput input) : SV_TARGET
{
    // Texture sampling with proper filtering
    float4 diffuse = DiffuseTexture.Sample(LinearSampler, input.TexCoord);
    
    // Simple lighting calculation
    float3 normal = normalize(input.Normal);
    float NdotL = saturate(dot(normal, -LightDirection.xyz));
    float3 lighting = float3(0.3f, 0.3f, 0.3f) + NdotL * 0.7f;
    
    return diffuse * float4(lighting, 1.0f);
}
```

### Operator Development Standards

#### Operator Interface Implementation

```csharp
using TiXL.Core.Operator;
using TiXL.Core.DataTypes;

namespace TiXL.Operators.TypeOperators.Gfx
{
    /// <summary>
    /// Renders a textured quad with configurable transformation and material properties.
    /// </summary>
    [Operator("RenderQuad", Category = "Gfx.Rendering", Description = "Renders a textured quad with configurable properties")]
    public class RenderQuadOperator : Symbol
    {
        // Input slots
        [InputSlot("Texture", Type = SlotType.Input)]
        public ISlot<Texture2D> TextureInput { get; }
        
        [InputSlot("Transform", Type = SlotType.Input)]
        public ISlot<Matrix4x4> TransformInput { get; }
        
        [InputSlot("Size", Type = SlotType.Input)]
        public ISlot<Vector2> SizeInput { get; }
        
        // Output slots
        [OutputSlot("Output", Type = SlotType.Output)]
        public ISlot<RenderTarget> OutputSlot { get; }
        
        // Property slots
        [PropertySlot("Material", Description = "Material properties for rendering")]
        public ISlot<PbrMaterial> MaterialSlot { get; }
        
        // Constructor - initialize slots
        public RenderQuadOperator()
        {
            // Initialize input slots
            TextureInput = AddSlot("Texture", SlotType.Input);
            TransformInput = AddSlot("Transform", SlotType.Input);
            SizeInput = AddSlot("Size", SlotType.Input);
            
            // Initialize output slot
            OutputSlot = AddSlot("Output", SlotType.Output);
            
            // Initialize property slots
            MaterialSlot = AddPropertySlot("Material");
            
            // Set default values
            SizeInput.SetValue(new Vector2(1.0f, 1.0f));
        }
        
        /// <summary>
        /// Creates an instance of this operator for runtime execution
        /// </summary>
        public override Instance CreateInstance()
        {
            return new RenderQuadInstance(this);
        }
    }
    
    /// <summary>
    /// Runtime instance of the RenderQuad operator
    /// </summary>
    public class RenderQuadInstance : Instance
    {
        private RenderTarget _renderTarget;
        private Texture2D _cachedTexture;
        
        public RenderQuadInstance(RenderQuadOperator symbol) : base(symbol)
        {
        }
        
        protected override void Evaluate(EvaluationContext context)
        {
            var texture = TextureInput.GetValue<Texture2D>();
            var transform = TransformInput.GetValue<Matrix4x4>();
            var size = SizeInput.GetValue<Vector2>();
            var material = MaterialSlot.GetValue<PbrMaterial>();
            
            // Validate inputs
            if (texture == null)
            {
                context.LogWarning("RenderQuad: Texture input is null");
                return;
            }
            
            // Create or update render target
            if (_renderTarget == null || _renderTarget.Size != size)
            {
                _renderTarget?.Dispose();
                _renderTarget = new RenderTarget((int)size.X, (int)size.Y);
            }
            
            // Cache texture
            _cachedTexture = texture;
            
            // Perform rendering
            RenderToTarget(texture, transform, material, _renderTarget);
            
            // Set output
            OutputSlot.SetValue(_renderTarget);
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _renderTarget?.Dispose();
            }
            
            base.Dispose(disposing);
        }
        
        private void RenderToTarget(Texture2D texture, Matrix4x4 transform, PbrMaterial material, RenderTarget target)
        {
            // Rendering implementation
            // ...
        }
    }
}
```

### Performance Guidelines

#### Memory Management

```csharp
// Use IDisposable for GPU resources
public class Texture2D : IDisposable
{
    private bool _isDisposed;
    
    public void Dispose()
    {
        if (!_isDisposed)
        {
            DisposeGPUResources();
            GC.SuppressFinalize(this);
            _isDisposed = true;
        }
    }
}

// Use object pools for frequently allocated objects
public class VertexBufferPool
{
    private readonly Queue<VertexBuffer> _pool = new();
    
    public VertexBuffer Get()
    {
        if (_pool.Count > 0)
        {
            return _pool.Dequeue();
        }
        
        return new VertexBuffer();
    }
    
    public void Return(VertexBuffer buffer)
    {
        buffer.Reset();
        _pool.Enqueue(buffer);
    }
}
```

#### Avoiding Blocking Operations

```csharp
// Bad: Blocking texture loading
public Texture2D LoadTexture(string path)
{
    var data = File.ReadAllBytes(path); // Blocking I/O
    return CreateTextureFromData(data);
}

// Good: Async texture loading
public async Task<Texture2D> LoadTextureAsync(string path)
{
    var data = await File.ReadAllBytesAsync(path); // Non-blocking I/O
    return CreateTextureFromData(data);
}

// For real-time operations, use compute shaders
public void ProcessTextureWithComputeShader(Texture2D input, Texture2D output)
{
    var computeShader = GetComputeShader("ProcessTexture");
    var dispatchGroup = new DispatchGroup(input.Width / 64, input.Height / 64);
    computeShader.Dispatch(input, output, dispatchGroup);
}
```

## Commit Message Conventions

### Format

```
<type>(<scope>): <subject>

<body>

<footer>
```

### Types

- **feat**: New feature or operator
- **fix**: Bug fix
- **docs**: Documentation changes
- **style**: Code style changes (formatting, naming)
- **refactor**: Code refactoring
- **test**: Adding or updating tests
- **chore**: Build process, tooling, dependencies
- **perf**: Performance improvements
- **revert**: Revert a previous commit

### Examples

```bash
feat(operators): add PBR material operator with metal-rough workflow

Implements a physically-based rendering material operator that supports:
- Base color, metallic, roughness inputs
- Normal mapping with tangent space support  
- Emission and AO integration
- Compatible with existing ShaderOperator framework

Closes #142
Fixes #156

feat(gfx): implement dynamic constant buffer management

- Add 16-byte alignment enforcement for constant buffers
- Implement write-discard mapping for efficient updates
- Centralize buffer state management
- Reduce CPU-GPU synchronization overhead

Closes #89

fix(core): resolve memory leak in render target disposal

- Add proper disposal pattern to Texture2D class
- Fix disposal order in GraphicsDevice
- Add unit tests for disposal scenarios

Fixes #201
```

### Commit Message Best Practices

1. **Use present tense** ("Add feature" not "Added feature")
2. **Keep subject line under 50 characters**
3. **Don't end subject line with period**
4. **Use the body to explain what and why vs. how**
5. **Reference issue numbers in the footer**
6. **Break long lines in the body at 72 characters**

## Pull Request Process

### Before Submitting

1. **Ensure your fork is up to date**
   ```bash
   git checkout main
   git pull upstream main
   git push origin main
   ```

2. **Create a feature branch**
   ```bash
   git checkout -b feature/your-feature-name
   ```

3. **Run all tests**
   ```bash
   dotnet test --configuration Release
   ```

4. **Build the solution**
   ```bash
   dotnet build --configuration Release
   ```

5. **Verify code style**
   ```bash
   dotnet format --verify-no-changes
   ```

### PR Template

When submitting a pull request, include the following information:

```markdown
## Description
Brief description of changes and motivation

## Type of Change
- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] Documentation update

## Testing
- [ ] Unit tests pass
- [ ] Integration tests pass
- [ ] Manual testing performed
- [ ] Performance impact assessed

## Checklist
- [ ] My code follows the code style of this project
- [ ] I have performed a self-review of my own code
- [ ] I have commented my code, particularly in hard-to-understand areas
- [ ] I have made corresponding changes to the documentation
- [ ] My changes generate no new warnings
- [ ] I have added tests that prove my fix is effective or that my feature works
- [ ] New and existing unit tests pass locally with my changes

## Screenshots/Videos
If applicable, add screenshots or videos demonstrating the changes

## Related Issues
Closes #(issue number)
```

### PR Review Process

1. **Automated Checks**: All PRs must pass CI/CD checks
2. **Code Review**: At least one core team member must approve
3. **Testing**: Changes must be tested on multiple configurations
4. **Documentation**: Code changes require updated documentation

#### Review Checklist

**Functionality**
- [ ] Code implements the described feature/fix
- [ ] Error handling is appropriate
- [ ] Performance impact is acceptable
- [ ] No regression in existing functionality

**Code Quality**
- [ ] Code follows style guidelines
- [ ] Proper XML documentation exists
- [ ] Memory management is correct
- [ ] No security vulnerabilities

**Testing**
- [ ] Tests cover the changes
- [ ] Test cases are meaningful
- [ ] Performance tests pass
- [ ] Integration tests validate functionality

## Issue Reporting Guidelines

### Bug Reports

Use the bug report template:

```markdown
**Describe the bug**
Clear and concise description of what the bug is

**To Reproduce**
Steps to reproduce the behavior:
1. Go to '...'
2. Click on '....'
3. Scroll down to '....'
4. See error

**Expected behavior**
Clear description of what you expected to happen

**Screenshots**
If applicable, add screenshots

**Environment (please complete):**
- OS: [e.g. Windows 11]
- TiXL Version: [e.g. v4.1.0.2]
- .NET Version: [e.g. 9.0.0]
- GPU: [e.g. RTX 3070]
- Driver Version: [e.g. 537.13]

**Additional context**
Any other context about the problem

**Log Files**
Please attach relevant log files or error messages
```

### Feature Requests

Use the feature request template:

```markdown
**Is your feature request related to a problem?**
Clear description of what the problem is

**Describe the solution you'd like**
Clear description of what you want to happen

**Describe alternatives you've considered**
Clear description of any alternative solutions you've considered

**Use Case**
Describe the specific use case this would help with

**Additional context**
Screenshots, mockups, or examples of how this might work

**Implementation Ideas**
If you have ideas about how this could be implemented, please share

**Prioritization**
How important is this feature to you?
- [ ] Nice to have
- [ ] Important for my workflow  
- [ ] Critical blocker
```

### Good Issue Reports

**Do:**
- Search existing issues first
- Provide clear reproduction steps
- Include version and system information
- Attach minimal reproduction projects
- Use descriptive titles

**Don't:**
- Open duplicates of existing issues
- Report multiple unrelated problems in one issue
- Provide insufficient detail for reproduction
- Request features without use cases
- Submit "works on my machine" reports

## Testing Requirements

### Testing Strategy

TiXL uses multiple testing levels:

1. **Unit Tests**: Individual component testing
2. **Integration Tests**: Module interaction testing
3. **End-to-End Tests**: Full application testing
4. **Performance Tests**: Benchmarking and profiling
5. **Operator Tests**: Specific operator validation

### Unit Testing

```csharp
[TestFixture]
public class RenderTargetTests
{
    [Test]
    public void Constructor_ValidSize_CreatesRenderTarget()
    {
        // Arrange
        var width = 1920;
        var height = 1080;
        
        // Act
        var renderTarget = new RenderTarget(width, height);
        
        // Assert
        Assert.That(renderTarget.Width, Is.EqualTo(width));
        Assert.That(renderTarget.Height, Is.EqualTo(height));
        Assert.That(renderTarget.IsDisposed, Is.False);
    }
    
    [Test]
    public void Dispose_CalledOnce_ReleasesResources()
    {
        // Arrange
        var renderTarget = new RenderTarget(1920, 1080);
        
        // Act
        renderTarget.Dispose();
        
        // Assert
        Assert.That(renderTarget.IsDisposed, Is.True);
        
        // Verify no exception on second dispose
        Assert.DoesNotThrow(() => renderTarget.Dispose());
    }
    
    [TestCase(0, 0)]
    [TestCase(-1, 1080)]
    [TestCase(1920, -1)]
    public void Constructor_InvalidSize_ThrowsArgumentException(int width, int height)
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => new RenderTarget(width, height));
    }
}
```

### Integration Testing

```csharp
[TestFixture]
public class OperatorPipelineTests
{
    private EvaluationContext _context;
    
    [SetUp]
    public void Setup()
    {
        _context = new EvaluationContext();
    }
    
    [Test]
    public void TextureTransformPipeline_ValidInputs_ProducesExpectedOutput()
    {
        // Arrange
        var textureOperator = new TextureLoadOperator();
        var transformOperator = new TransformOperator();
        var outputOperator = new RenderOutputOperator();
        
        // Set up connections
        transformOperator.InputTexture.Connect(textureOperator.OutputTexture);
        outputOperator.InputTexture.Connect(transformOperator.OutputTexture);
        
        // Set inputs
        textureOperator.FilePath.SetValue("test_texture.png");
        transformOperator.Rotation.SetValue(Vector3.UnitY);
        
        // Act
        textureOperator.Evaluate(_context);
        transformOperator.Evaluate(_context);
        outputOperator.Evaluate(_context);
        
        // Assert
        var result = outputOperator.OutputTexture.GetValue<Texture2D>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Width, Is.GreaterThan(0));
    }
}
```

### Operator Testing

```csharp
[TestFixture]
public class PbrMaterialOperatorTests
{
    [Test]
    public void Evaluate_ValidInputs_CreatesMaterial()
    {
        // Arrange
        var operator = new PbrMaterialOperator();
        operator.BaseColor.SetValue(Color.White);
        operator.Metallic.SetValue(0.0f);
        operator.Roughness.SetValue(0.5f);
        
        // Act
        operator.Evaluate(new EvaluationContext());
        
        // Assert
        var material = operator.Output.GetValue<PbrMaterial>();
        Assert.That(material, Is.Not.Null);
        Assert.That(material.Metallic, Is.EqualTo(0.0f).Within(0.001f));
        Assert.That(material.Roughness, Is.EqualTo(0.5f).Within(0.001f));
    }
    
    [Test]
    public void Serialize_Material_SerializesProperties()
    {
        // Arrange
        var material = new PbrMaterial();
        material.Metallic = 0.8f;
        material.Roughness = 0.2f;
        
        // Act
        var serialized = material.Serialize();
        var deserialized = PbrMaterial.Deserialize(serialized);
        
        // Assert
        Assert.That(deserialized.Metallic, Is.EqualTo(material.Metallic).Within(0.001f));
        Assert.That(deserialized.Roughness, Is.EqualTo(material.Roughness).Within(0.001f));
    }
}
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test category
dotnet test --filter Category=Unit

# Run tests with code coverage
dotnet test --collect:"XPlat Code Coverage"

# Run performance tests
dotnet test --filter Category=Performance

# Run in debug configuration
dotnet test --configuration Debug
```

## Development Workflow

### Branch Strategy

We use a GitFlow-inspired workflow:

- **main**: Production-ready code
- **develop**: Integration branch for features
- **feature/**: New features or operators
- **bugfix/**: Bug fixes
- **hotfix/**: Critical production fixes

### Creating Features

1. **Start from develop branch**
   ```bash
   git checkout develop
   git pull upstream develop
   ```

2. **Create feature branch**
   ```bash
   git checkout -b feature/add-pbr-material-operator
   ```

3. **Develop and commit**
   ```bash
   # Make changes
   git add .
   git commit -m "feat(operators): add PBR material operator"
   ```

4. **Push and create PR**
   ```bash
   git push origin feature/add-pbr-material-operator
   ```

### Code Review Workflow

1. **Self-review first**: Check your changes thoroughly
2. **Request review**: Assign reviewers on GitHub
3. **Respond to feedback**: Address comments promptly
4. **Make updates**: Push additional commits if needed
5. **Squash commits**: Before merging, squash commits into logical units
6. **Merge**: Core team merges approved PRs

### Release Process

1. **Feature freeze**: Stop accepting new features
2. **Stabilization**: Fix critical bugs and regressions
3. **Release preparation**: Update version numbers and changelog
4. **Testing**: Thorough QA on all platforms
5. **Release**: Tag and publish binaries
6. **Documentation**: Update website and wiki

## Module-Specific Guidelines

### Core Module Contributions

**Focus Areas:**
- Data types and mathematics
- Rendering engine improvements
- Resource management
- Performance optimizations

**Guidelines:**
- Follow established patterns for disposal and lifecycle management
- Use proper error handling and logging
- Document mathematical operations clearly
- Consider GPU implications of changes

**Example: Adding a new Vector3 operation**

```csharp
/// <summary>
/// Performs cubic interpolation between two vectors using ease-in-out cubic curve.
/// </summary>
/// <param name="start">Starting vector</param>
/// <param name="end">Ending vector</param>
/// <param name="t">Interpolation factor (0.0 to 1.0)</param>
/// <returns>Interpolated vector</returns>
public static Vector3 SmoothStep(Vector3 start, Vector3 end, float t)
{
    t = Mathf.Clamp01(t);
    t = t * t * (3.0f - 2.0f * t);
    
    return Vector3.Lerp(start, end, t);
}
```

### Operators Module Contributions

**Focus Areas:**
- New operators for existing categories
- Performance improvements to existing operators
- Example operators and tutorials
- Operator documentation

**Guidelines:**
- Follow established operator patterns
- Provide comprehensive XML documentation
- Include usage examples in comments
- Test with various input types
- Consider performance implications

**Example: Audio-reactive operator**

```csharp
[Operator("AudioLevel", Category = "Audio.Analysis", 
          Description = "Outputs normalized audio level for reactive visuals")]
public class AudioLevelOperator : Symbol
{
    [InputSlot("Audio Source", Description = "Audio input source")]
    public ISlot<AudioBuffer> AudioInput { get; }
    
    [PropertySlot("Smoothing", Description = "Smoothing factor for level transitions (0.0-1.0)")]
    public ISlot<float> SmoothingSlot { get; }
    
    [OutputSlot("Level", Description = "Normalized audio level (0.0-1.0)")]
    public ISlot<float> LevelOutput { get; }
    
    private float _smoothedLevel;
    
    public AudioLevelOperator()
    {
        AudioInput = AddSlot("Audio Source", SlotType.Input);
        SmoothingSlot = AddPropertySlot("Smoothing", 0.8f);
        LevelOutput = AddSlot("Level", SlotType.Output);
    }
    
    protected override void Evaluate(EvaluationContext context)
    {
        var audioBuffer = AudioInput.GetValue<AudioBuffer>();
        if (audioBuffer == null) return;
        
        var currentLevel = audioBuffer.GetLevel();
        var smoothing = Mathf.Clamp01(SmoothingSlot.GetValue());
        
        _smoothedLevel = Mathf.Lerp(_smoothedLevel, currentLevel, 1.0f - smoothing);
        LevelOutput.SetValue(_smoothedLevel);
    }
}
```

### Graphics (Gfx) Module Contributions

**Focus Areas:**
- Shader development and optimization
- Render state management
- Buffer management improvements
- New rendering features

**Guidelines:**
- Follow DirectX 12 best practices
- Ensure proper alignment in constant buffers
- Document shader performance characteristics
- Include shader debugging information
- Test with various GPU configurations

**Example: Compute shader implementation**

```hlsl
// Compute shader for particle system update
cbuffer ParticleConstants : register(b0)
{
    float deltaTime;
    float3 gravity;
    float4 boundingBox;
    float damping;
    uint particleCount;
}

StructuredBuffer<Particle> ParticleBuffer : register(t0);
RWStructuredBuffer<Particle> OutParticleBuffer : register(u0);

[numthreads(256, 1, 1)]
void CSMain(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= particleCount) return;
    
    Particle p = ParticleBuffer[id.x];
    
    // Apply gravity
    p.Velocity += gravity * deltaTime;
    
    // Apply damping
    p.Velocity *= (1.0f - damping * deltaTime);
    
    // Update position
    p.Position += p.Velocity * deltaTime;
    
    // Boundary collision (simple bounce)
    if (p.Position.x < boundingBox.x || p.Position.x > boundingBox.z)
    {
        p.Position.x = clamp(p.Position.x, boundingBox.x, boundingBox.z);
        p.Velocity.x *= -0.8f;
    }
    
    if (p.Position.y < boundingBox.y || p.Position.y > boundingBox.w)
    {
        p.Position.y = clamp(p.Position.y, boundingBox.y, boundingBox.w);
        p.Velocity.y *= -0.8f;
    }
    
    // Apply to output
    OutParticleBuffer[id.x] = p;
}
```

### Editor/GUI Module Contributions

**Focus Areas:**
- UI components and interactions
- Window management
- Graph visualization
- User experience improvements

**Guidelines:**
- Follow ImGui best practices
- Maintain performance in immediate-mode UI
- Provide keyboard shortcuts and accessibility
- Test on different screen resolutions
- Document UI interactions

**Example: Custom UI component**

```csharp
/// <summary>
/// A custom color picker component with HSV support
/// </summary>
public static class ColorPicker
{
    public static bool Draw(Vector2 size, ref Color color)
    {
        ImGui.PushItemWidth(size.x);
        
        // Convert RGB to HSV
        Color.RGBToHSV(color, out float h, out float s, out float v);
        
        // Hue slider
        bool changed = ImGui.ColorEdit3("Color", ref color);
        
        // HSV sliders
        ImGui.SliderFloat("Hue", ref h, 0.0f, 1.0f);
        ImGui.SliderFloat("Saturation", ref s, 0.0f, 1.0f);
        ImGui.SliderFloat("Value", ref v, 0.0f, 1.0f);
        
        // Convert HSV back to RGB and update color
        if (changed)
        {
            color = Color.HSVToRGB(h, s, v);
        }
        
        ImGui.PopItemWidth();
        return changed;
    }
}
```

## Architectural Governance

TiXL maintains strict architectural boundaries to ensure code maintainability, testability, and extensibility. This section covers the governance rules and enforcement mechanisms that keep the codebase well-structured.

### Understanding Module Boundaries

TiXL follows a clean, modular architecture with five primary domains. Understanding these boundaries is crucial for contributing effectively:

#### Module Responsibilities and Restrictions

**Core Module (`TiXL.Core`)**
- **Responsibilities**: Engine foundations, data types, rendering infrastructure, mathematical operations
- **Allowed Dependencies**: System, Microsoft packages
- **Forbidden Dependencies**: Operators, Gui, Editor, Gfx modules
- **Key Interfaces**: `IRenderingEngine`, `IMathLibrary`, `IResourceManager`

**Operators Module (`TiXL.Operators`)**
- **Responsibilities**: Plugin-based operator system, dataflow management, operator registry
- **Allowed Dependencies**: Core, System, Microsoft packages
- **Forbidden Dependencies**: Gui, Editor, Gfx modules
- **Key Interfaces**: `ISymbol`, `IInstance`, `ISlot`, `IOperatorRegistry`

**Graphics Module (`TiXL.Gfx`)**
- **Responsibilities**: DirectX 12 pipeline, shader management, graphics state handling
- **Allowed Dependencies**: Core, SharpDX, System, Microsoft packages
- **Forbidden Dependencies**: Operators, Gui, Editor modules
- **Key Interfaces**: `IGraphicsDevice`, `IShaderCompiler`, `IPipelineState`

**GUI Module (`TiXL.Gui`)**
- **Responsibilities**: User interface components, window management, data binding
- **Allowed Dependencies**: Core, Operators, ImGui.NET, System, Microsoft packages
- **Forbidden Dependencies**: Editor, Gfx modules
- **Key Interfaces**: `IUIComponent`, `IWindowManager`, `IOperatorUI`

**Editor Module (`TiXL.Editor`)**
- **Responsibilities**: Application orchestration, project management, integration coordination
- **Allowed Dependencies**: All modules (integration point)
- **Forbidden Dependencies**: None
- **Key Interfaces**: `IApplication`, `IProjectManager`, `ICompilationManager`

### Dependency Rules and Violations

#### Common Architectural Violations

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

**4. Namespace Mismatches**
```csharp
// ❌ VIOLATION: Namespace doesn't match directory
// File: src/Core/Math/Vector3.cs
namespace TiXL.Operators.Math { } // Wrong!

// ✅ CORRECT: Namespace matches directory structure
namespace TiXL.Core.Math { }
```

### Cross-Module Communication Patterns

#### 1. Interface-Based Communication

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

#### 2. Event-Based Communication

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

#### 3. Context-Based Communication

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
    
    public interface IOperator
    {
        void Evaluate(EvaluationContext context);
    }
}
```

### Validation and Enforcement

#### Automated Tools

TiXL provides several tools to help maintain architectural compliance:

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

#### Build-Time Validation

Architectural constraints are enforced during compilation:

```xml
<!-- Directory.Build.props enforces constraints -->
<Target Name="ValidateArchitecturalBoundaries" BeforeTargets="BeforeBuild">
  <Error Text="Architectural boundary violation detected" 
         Condition="'$(ForbiddenProjectReference)' != ''" />
</Target>
```

### Code Review Checklist for Architecture

**Reviewers must check:**

- [ ] No forbidden dependencies introduced
- [ ] Module boundaries respected
- [ ] Proper use of interfaces and abstractions
- [ ] Namespace structure maintained
- [ ] Cross-module communication follows established patterns
- [ ] No direct instantiation of cross-module implementations
- [ ] Architectural documentation updated if needed

**Common architecture review comments:**

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

**Namespace Issues:**
```csharp
// ❌ Bad: Namespace doesn't match module
namespace TiXL.Gui { } // In Operators source file

// ✅ Good: Namespace matches module
namespace TiXL.Operators { }
```

### Contributing Within Architecture

#### Adding New Cross-Module Functionality

1. **Define interfaces in appropriate module** (usually Core or Operators)
2. **Implement in target module** (follows dependency rules)
3. **Wire together in Editor** (integration point)
4. **Add validation tests** to ensure compliance

#### Creating New Modules

1. **Define module boundaries** in `docs/ARCHITECTURAL_GOVERNANCE.md`
2. **Update validation tools** with new constraints
3. **Create architectural documentation**
4. **Add to CI/CD validation pipeline**

#### Fixing Architectural Violations

1. **Identify violation type** using validation tools
2. **Apply appropriate fix pattern**:
   - Move interfaces to correct module
   - Use dependency injection instead of direct instantiation
   - Refactor using statements and namespaces
   - Replace direct dependencies with abstractions
3. **Run validation tools** to verify fixes
4. **Update tests** and documentation as needed

### Best Practices

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
- [Architectural Governance Documentation](ARCHITECTURAL_GOVERNANCE.md)
- [Architecture Tools README](ARCHITECTURAL_GOVERNANCE_TOOLS_README.md)
- Validation script help: `./scripts/validate-architecture.sh help`

**Common solutions:**
- Run `./scripts/validate-architecture.sh validate -v` for detailed output
- Check architectural patterns in existing codebase
- Use IDE refactoring tools to move interfaces
- Consult module-specific guidelines in this document

### Continuous Improvement

Architectural governance is an ongoing process:

- **Monthly reviews** of validation reports
- **Quarterly assessment** of architectural health
- **Annual major reviews** and updates to governance rules
- **Regular updates** to tools and documentation

Contributors should stay informed about architectural changes and participate in governance discussions when modules evolve.

## Code Review Process

### Review Expectations

**For Authors:**
- Keep PRs focused and atomic
- Write clear commit messages
- Include tests for new functionality
- Update documentation
- Be responsive to feedback

**For Reviewers:**
- Review code thoroughly and constructively
- Focus on architecture, design, and quality
- Test changes when possible
- Provide actionable feedback
- Balance thoroughness with efficiency

### Review Checklist

**Architecture and Design**
- [ ] Code follows SOLID principles
- [ ] Appropriate use of interfaces and abstractions
- [ ] Proper separation of concerns
- [ ] Consistent with existing patterns

**Functionality**
- [ ] Code implements the intended feature
- [ ] Error handling is appropriate
- [ ] Edge cases are handled
- [ ] No breaking changes (or properly marked)

**Performance**
- [ ] No unnecessary allocations
- [ ] Efficient algorithms and data structures
- [ ] GPU resources managed properly
- [ ] Profiling considerations

**Testing**
- [ ] Adequate test coverage
- [ ] Tests are meaningful and reliable
- [ ] Integration tests validate interactions
- [ ] Performance tests if applicable

**Documentation**
- [ ] Clear XML documentation
- [ ] Comments explain complex logic
- [ ] README/wiki updates if needed
- [ ] Examples provided

### Common Review Comments

**Performance Issues:**
```csharp
// Bad: Allocating in tight loop
for (int i = 0; i < 10000; i++)
{
    var list = new List<Vector3>(); // Creates garbage
    Process(list);
}

// Good: Reuse allocation
var list = new List<Vector3>(capacity: 10000);
for (int i = 0; i < 10000; i++)
{
    list.Clear();
    Process(list);
}
```

**Error Handling:**
```csharp
// Bad: Silent failure
public Texture2D LoadTexture(string path)
{
    return File.ReadAllBytes(path).ToTexture();
}

// Good: Proper error handling
public Texture2D LoadTexture(string path)
{
    if (string.IsNullOrEmpty(path))
        throw new ArgumentException("Path cannot be null or empty", nameof(path));
    
    if (!File.Exists(path))
        throw new FileNotFoundException("Texture file not found", path);
    
    try
    {
        var data = File.ReadAllBytes(path);
        return CreateTextureFromData(data);
    }
    catch (Exception ex)
    {
        throw new IOException($"Failed to load texture from {path}", ex);
    }
}
```

**Memory Management:**
```csharp
// Bad: Potential memory leak
public class Shader : IDisposable
{
    private IntPtr _nativeHandle;
    
    public void Dispose()
    {
        // Missing base.Dispose() call
        FreeNativeResource(_nativeHandle);
    }
}

// Good: Proper disposal pattern
public class Shader : IDisposable
{
    private bool _isDisposed;
    private IntPtr _nativeHandle;
    
    public void Dispose()
    {
        if (!_isDisposed)
        {
            FreeNativeResource(_nativeHandle);
            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
    
    ~Shader()
    {
        Dispose(false);
    }
}
```

## Community Guidelines

### Communication

- **Discord**: Primary community chat (https://discord.gg/YmSyQdeH3S)
- **GitHub Issues**: Bug reports and feature requests
- **GitHub Discussions**: General discussions and Q&A
- **YouTube**: Tutorial videos and showcases

### Code of Conduct

**Be Respectful:**
- Treat all community members with respect
- Acknowledge diverse backgrounds and perspectives
- Focus on constructive feedback and collaboration

**Be Inclusive:**
- Welcome newcomers and help them get started
- Use inclusive language in all communications
- Respect different skill levels and learning styles

**Be Professional:**
- Keep discussions on-topic and constructive
- Report security issues privately to maintainers
- Respect the time and effort of contributors

### Getting Help

**Before asking for help:**
1. Check existing documentation and wiki
2. Search through existing issues
3. Review similar operator examples
4. Test with minimal reproduction cases

**When asking for help:**
- Provide clear context and goals
- Include system information and versions
- Share relevant code or project files
- Describe what you've already tried

### Recognition

**Contributors are recognized through:**
- GitHub contributors list
- Release notes attribution
- Special mentions for significant contributions
- Community spotlight features

### Security

**Reporting Security Issues:**
- Email security issues to maintainers
- Include detailed steps to reproduce
- Allow time for fixes before public disclosure
- Don't post security details publicly

**Security Best Practices:**
- Validate all inputs from operators
- Use secure random number generation
- Implement proper resource cleanup
- Follow .NET security guidelines

## Additional Resources

### Learning Resources

- [C# Documentation](https://docs.microsoft.com/en-us/dotnet/csharp/)
- [DirectX 12 Programming Guide](https://docs.microsoft.com/en-us/windows/win32/direct3d12/direct3d-12-programming-guide)
- [ImGui Documentation](https://github.com/ocornut/imgui/wiki)
- [Shader Programming Tutorials](https://learn.microsoft.com/en-us/windows/win32/direct3dhlsl/dx-graphics-hlsl-pguide)

### Development Tools

- **Visual Studio 2022**: Primary IDE for development
- **RenderDoc**: Graphics debugging and profiling
- **dotMemory**: Memory profiling for .NET
- **BenchmarkDotNet**: Performance benchmarking
- **NUnit**: Unit testing framework

### TiXL-Specific Resources

- [Official Website](https://tixl.app)
- [Video Tutorials](https://www.youtube.com/watch?v=eH2E02U6P5Q)
- [Operator Examples Repository](https://github.com/tixl3d/Operators)
- [TiXL Resources](https://github.com/tixl3d/Resources)

---

## Conclusion

Thank you for contributing to TiXL! These guidelines help maintain code quality, ensure consistent development practices, and create a welcoming environment for all contributors. If you have questions about contributing or need clarification on any guidelines, please reach out through Discord or GitHub discussions.

Remember: Contributing to open source is a collaborative effort. Be patient, be helpful, and focus on making TiXL better for everyone in the community.

**Happy coding!** 🎨✨