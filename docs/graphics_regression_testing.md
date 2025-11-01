# TiXL Graphics Rendering Regression Testing System

## Overview

This document outlines the implementation of a comprehensive graphics rendering regression testing system for TiXL to validate visual output consistency, detect performance regressions, and ensure the DirectX 12 rendering pipeline maintains reliability across releases.

## Table of Contents

1. [System Architecture](#system-architecture)
2. [Automated Rendering Test Framework](#automated-rendering-test-framework)
3. [Visual Regression Testing](#visual-regression-testing)
4. [Shader Compilation and Execution Validation](#shader-compilation-and-execution-validation)
5. [Graphics Resource Management Testing](#graphics-resource-management-testing)
6. [Performance Regression Detection](#performance-regression-detection)
7. [Test Automation Pipeline Integration](#test-automation-pipeline-integration)
8. [Baseline Image Management](#baseline-image-management)
9. [Headless Testing Environment](#headless-testing-environment)
10. [Mock Graphics Contexts](#mock-graphics-contexts)
11. [CI/CD Integration](#cicd-integration)
12. [Test Implementation Examples](#test-implementation-examples)

## System Architecture

The graphics testing system follows a layered architecture designed for reliability and maintainability:

```
┌─────────────────────────────────────────────────────────┐
│                  Graphics Testing System                │
├─────────────────────────────────────────────────────────┤
│  Test Orchestrator     │  Performance Monitor            │
│  (Test Suite Manager)  │  (Frame-time, Memory)          │
├─────────────────────────────────────────────────────────┤
│  Visual Validator      │  Shader Tester                  │
│  (Screenshot Comparison)│  (Compilation & Execution)     │
├─────────────────────────────────────────────────────────┤
│  Resource Validator    │  Pipeline Validator             │
│  (Buffers/Textures)    │  (DirectX 12 Pipeline)         │
├─────────────────────────────────────────────────────────┤
│  Mock Graphics Context │  Headless Renderer              │
│  (Test Double Layer)   │  (Offscreen Rendering)         │
└─────────────────────────────────────────────────────────┘
```

### Core Components

1. **TestOrchestrator**: Manages test execution lifecycle and coordinates between components
2. **VisualValidator**: Handles screenshot capture and comparison operations
3. **ShaderTester**: Validates shader compilation and execution
4. **ResourceValidator**: Tests graphics resource management
5. **PerformanceMonitor**: Tracks rendering performance metrics
6. **MockGraphicsContext**: Provides test doubles for graphics operations
7. **HeadlessRenderer**: Enables rendering without display

## Automated Rendering Test Framework

### DirectX 12 Pipeline Validation Framework

```csharp
/// <summary>
/// Automated rendering test framework for DirectX 12 pipeline validation
/// </summary>
[TestFixture]
[Category("Graphics")]
public class DirectX12PipelineTests
{
    private MockGraphicsContext _mockContext;
    private HeadlessRenderer _renderer;
    private TestOrchestrator _orchestrator;

    [SetUp]
    public void Setup()
    {
        _mockContext = new MockGraphicsContext();
        _renderer = new HeadlessRenderer(_mockContext);
        _orchestrator = new TestOrchestrator(_renderer);
    }

    [Test]
    public void Pipeline_Initialization_Success()
    {
        // Arrange
        var pipelineConfig = new PipelineConfiguration
        {
            RenderTargetWidth = 1920,
            RenderTargetHeight = 1080,
            SampleCount = 4,
            EnableDepthBuffer = true
        };

        // Act
        var result = _renderer.InitializePipeline(pipelineConfig);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True, "Pipeline should initialize successfully");
            Assert.That(_renderer.Device, Is.Not.Null, "Graphics device should be created");
            Assert.That(_renderer.SwapChain, Is.Not.Null, "Swap chain should be created");
        });
    }

    [Test]
    public void Pipeline_State_Management_Validation()
    {
        // Test pipeline state consistency
        _renderer.InitializePipeline(new PipelineConfiguration());
        
        // Test depth-stencil state
        var depthState = _renderer.GetCurrentDepthStencilState();
        Assert.That(depthState.IsDepthEnabled, Is.True, "Depth should be enabled by default");
        
        // Test blend state
        var blendState = _renderer.GetCurrentBlendState();
        Assert.That(blendState.IsBlendingEnabled, Is.False, "Blending should be disabled by default");
        
        // Test rasterizer state
        var rasterState = _renderer.GetCurrentRasterizerState();
        Assert.That(rasterState.FillMode, Is.EqualTo(FillMode.Solid), "Fill mode should be solid");
        Assert.That(rasterState.CullMode, Is.EqualTo(CullMode.Back), "Back-face culling should be enabled");
    }

    [Test]
    public void Render_Target_Switching_Validation()
    {
        _renderer.InitializePipeline(new PipelineConfiguration());
        
        // Create multiple render targets
        var target1 = _renderer.CreateRenderTarget(800, 600);
        var target2 = _renderer.CreateRenderTarget(1024, 768);
        
        // Test switching between targets
        _renderer.SetRenderTarget(target1);
        var currentTarget = _renderer.GetCurrentRenderTarget();
        Assert.That(currentTarget.Dimensions, Is.EqualTo(new Size(800, 600)));
        
        _renderer.SetRenderTarget(target2);
        currentTarget = _renderer.GetCurrentRenderTarget();
        Assert.That(currentTarget.Dimensions, Is.EqualTo(new Size(1024, 768)));
    }
}
```

### Pipeline Configuration Model

```csharp
/// <summary>
/// Configuration for DirectX 12 pipeline setup
/// </summary>
public class PipelineConfiguration
{
    public int RenderTargetWidth { get; set; } = 1920;
    public int RenderTargetHeight { get; set; } = 1080;
    public int SampleCount { get; set; } = 1;
    public bool EnableDepthBuffer { get; set; } = true;
    public Format RenderTargetFormat { get; set; } = Format.R8G8B8A8UNorm;
    public Format DepthBufferFormat { get; set; } = Format.D24UNormS8UInt;
    
    // Performance settings
    public bool EnableVSync { get; set; } = false;
    public int TargetFrameRate { get; set; } = 60;
    
    // Validation settings
    public bool EnableDebugLayer { get; set; } = true;
    public bool EnableGPUValidation { get; set; } = true;
}
```

## Visual Regression Testing

### Screenshot Comparison System

```csharp
/// <summary>
/// Visual regression testing with screenshot comparison
/// </summary>
[TestFixture]
[Category("Visual")]
public class VisualRegressionTests
{
    private VisualValidator _visualValidator;
    private BaselineManager _baselineManager;
    private TestSceneFactory _sceneFactory;

    [SetUp]
    public void Setup()
    {
        _visualValidator = new VisualValidator();
        _baselineManager = new BaselineManager();
        _sceneFactory = new TestSceneFactory();
    }

    [Test]
    public void Basic_Triangle_Rendering_Visual()
    {
        // Arrange
        var scene = _sceneFactory.CreateBasicTriangleScene();
        var testCase = "basic_triangle_rendering";
        
        // Act - Render scene
        var screenshot = _renderer.RenderScene(scene);
        
        // Assert - Compare with baseline
        var comparisonResult = _visualValidator.CompareWithBaseline(
            screenshot, 
            testCase,
            new VisualComparisonSettings
            {
                Tolerance = 0.01f, // 1% pixel difference tolerance
                UseSSIM = true,
                EnableMasking = true
            });
        
        Assert.Multiple(() =>
        {
            Assert.That(comparisonResult.IsMatch, Is.True, 
                $"Visual regression detected: {comparisonResult.DifferencePercentage:F2}% difference");
            Assert.That(comparisonResult.DifferencePercentage, Is.LessThan(1.0f),
                "Difference should be less than 1%");
        });
    }

    [Test]
    public void PBR_Material_Rendering_Visual()
    {
        // Test PBR material rendering consistency
        var pbrScene = _sceneFactory.CreatePBRMaterialScene();
        var screenshot = _renderer.RenderScene(pbrScene);
        
        var result = _visualValidator.CompareWithBaseline(
            screenshot,
            "pbr_material_rendering",
            new VisualComparisonSettings { Tolerance = 0.02f });
        
        Assert.That(result.IsMatch, Is.True, "PBR material rendering should match baseline");
    }

    [Test]
    public void Lighting_Scene_Visual_Consistency()
    {
        // Test various lighting scenarios
        var lightingScenarios = new[]
        {
            new LightingSetup { LightType = LightType.Directional, Intensity = 1.0f },
            new LightingSetup { LightType = LightType.Point, Position = new Vector3(5, 5, 5) },
            new LightingSetup { LightType = LightType.Spot, Angle = 45.0f }
        };

        foreach (var lighting in lightingScenarios)
        {
            var scene = _sceneFactory.CreateLightingScene(lighting);
            var screenshot = _renderer.RenderScene(scene);
            
            var testName = $"lighting_{lighting.LightType.ToString().ToLower()}";
            var result = _visualValidator.CompareWithBaseline(screenshot, testName);
            
            Assert.That(result.IsMatch, Is.True, 
                $"Lighting scene ({lighting.LightType}) should match baseline");
        }
    }
}
```

### Visual Comparison Engine

```csharp
/// <summary>
/// Advanced visual comparison engine with multiple algorithms
/// </summary>
public class VisualValidator
{
    public VisualComparisonResult CompareWithBaseline(
        RenderTarget screenshot, 
        string testName,
        VisualComparisonSettings settings = null)
    {
        settings ??= VisualComparisonSettings.Default;
        
        var baselineImage = LoadBaselineImage(testName);
        var currentScreenshot = ConvertToImage(screenshot);
        
        // Multiple comparison algorithms
        var pixelDiff = CalculatePixelDifference(currentScreenshot, baselineImage);
        var ssimScore = CalculateSSIM(currentScreenshot, baselineImage);
        var perceptualHash = CalculatePerceptualHash(currentScreenshot);
        
        var result = new VisualComparisonResult
        {
            PixelDifference = pixelDiff,
            SSIMScore = ssimScore,
            PerceptualHash = perceptualHash,
            IsMatch = EvaluateMatch(pixelDiff, ssimScore, settings),
            DifferencePercentage = (pixelDiff * 100.0f),
            SSIMSimilarity = ssimScore
        };
        
        // Save comparison artifacts
        SaveComparisonArtifacts(testName, currentScreenshot, baselineImage, result);
        
        return result;
    }

    private float CalculatePixelDifference(Bitmap current, Bitmap baseline)
    {
        float totalDifference = 0;
        var width = Math.Min(current.Width, baseline.Width);
        var height = Math.Min(current.Height, baseline.Height);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var currentPixel = current.GetPixel(x, y);
                var baselinePixel = baseline.GetPixel(x, y);
                
                var rDiff = Math.Abs(currentPixel.R - baselinePixel.R);
                var gDiff = Math.Abs(currentPixel.G - baselinePixel.G);
                var bDiff = Math.Abs(currentPixel.B - baselinePixel.B);
                var aDiff = Math.Abs(currentPixel.A - baselinePixel.A);
                
                totalDifference += (rDiff + gDiff + bDiff + aDiff) / 4.0f;
            }
        }

        return totalDifference / (width * height * 255.0f);
    }

    private float CalculateSSIM(Bitmap current, Bitmap baseline)
    {
        // Structural Similarity Index implementation
        return SSIMCalculator.Calculate(current, baseline);
    }

    private string CalculatePerceptualHash(Bitmap image)
    {
        // Perceptual hash for quick similarity detection
        return PerceptualHash.Compute(image, 32);
    }
}
```

## Shader Compilation and Execution Validation

### Shader Testing Framework

```csharp
/// <summary>
/// Shader compilation and execution validation system
/// </summary>
[TestFixture]
[Category("Shaders")]
public class ShaderTests
{
    private ShaderTester _shaderTester;
    private MockShaderCompiler _compiler;

    [SetUp]
    public void Setup()
    {
        _compiler = new MockShaderCompiler();
        _shaderTester = new ShaderTester(_compiler);
    }

    [Test]
    public void Vertex_Shader_Compilation_Success()
    {
        // Arrange
        var vertexShaderSource = @"
            struct VSInput {
                float3 position : POSITION;
                float2 texcoord : TEXCOORD;
            };
            
            struct VSOutput {
                float4 position : SV_POSITION;
                float2 texcoord : TEXCOORD;
            };
            
            VSOutput VSMain(VSInput input) {
                VSOutput output;
                output.position = float4(input.position, 1.0);
                output.texcoord = input.texcoord;
                return output;
            }";

        // Act
        var result = _compiler.CompileShader(vertexShaderSource, ShaderType.Vertex);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True, "Vertex shader should compile successfully");
            Assert.That(result.ByteCode, Is.Not.Null, "Compiled bytecode should be generated");
            Assert.That(result.ErrorMessages, Is.Empty, "No compilation errors should occur");
        });
    }

    [Test]
    public void PBR_Fragment_Shader_Execution()
    {
        // Test PBR shader execution with various material inputs
        var shaderTestCases = new[]
        {
            new MaterialTestCase { Name = "metal_materials", Metalness = 1.0f, Roughness = 0.2f },
            new MaterialTestCase { Name = "dielectric_materials", Metalness = 0.0f, Roughness = 0.5f },
            new MaterialTestCase { Name = "rough_materials", Metalness = 0.0f, Roughness = 1.0f }
        };

        foreach (var testCase in shaderTestCases)
        {
            var result = _shaderTester.ExecutePBRShader(testCase);
            
            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True, $"PBR shader should execute for {testCase.Name}");
                Assert.That(result.OutputColor, Is.Not.EqualTo(Color.Black), 
                    "Shader should produce non-black output");
                Assert.That(result.ExecutionTime, Is.LessThan(TimeSpan.FromMilliseconds(10)), 
                    "Shader execution should be under 10ms");
            });
        }
    }

    [Test]
    public void Shader_Performance_Validation()
    {
        // Validate shader compilation and execution performance
        var performanceTests = new ShaderPerformanceTest[]
        {
            new ShaderPerformanceTest { ShaderType = ShaderType.Vertex, ExpectedCompileTime = 100 },
            new ShaderPerformanceTest { ShaderType = ShaderType.Pixel, ExpectedCompileTime = 150 },
            new ShaderPerformanceTest { ShaderType = ShaderType.Compute, ExpectedCompileTime = 200 }
        };

        foreach (var test in performanceTests)
        {
            var result = _shaderTester.MeasureShaderPerformance(test);
            
            Assert.That(result.CompileTime, Is.LessThan(test.ExpectedCompileTime), 
                $"{test.ShaderType} shader compilation should be under {test.ExpectedCompileTime}ms");
        }
    }
}
```

### Shader Compiler Mock

```csharp
/// <summary>
/// Mock shader compiler for testing without actual DirectX compilation
/// </summary>
public class MockShaderCompiler
{
    private readonly Dictionary<string, MockCompiledShader> _compiledShaders;
    private readonly List<ShaderCompileLog> _compileLogs;

    public MockShaderCompiler()
    {
        _compiledShaders = new Dictionary<string, MockCompiledShader>();
        _compileLogs = new List<ShaderCompileLog>();
    }

    public ShaderCompileResult CompileShader(string source, ShaderType shaderType)
    {
        var startTime = DateTime.Now;
        
        try
        {
            // Simulate compilation process
            var compilationResult = SimulateShaderCompilation(source, shaderType);
            
            var compileTime = DateTime.Now - startTime;
            
            var log = new ShaderCompileLog
            {
                Timestamp = DateTime.Now,
                ShaderType = shaderType,
                Success = compilationResult.IsSuccess,
                CompileTime = compileTime,
                ErrorMessages = compilationResult.ErrorMessages
            };
            
            _compileLogs.Add(log);
            
            return new ShaderCompileResult
            {
                IsSuccess = compilationResult.IsSuccess,
                ByteCode = compilationResult.IsSuccess ? GenerateMockByteCode(shaderType) : null,
                ErrorMessages = compilationResult.ErrorMessages,
                CompileTime = compileTime
            };
        }
        catch (Exception ex)
        {
            return new ShaderCompileResult
            {
                IsSuccess = false,
                ErrorMessages = new[] { ex.Message },
                CompileTime = DateTime.Now - startTime
            };
        }
    }

    private CompilationResult SimulateShaderCompilation(string source, ShaderType shaderType)
    {
        // Simulate common compilation errors
        if (source.Contains("undefined_variable"))
        {
            return new CompilationResult 
            { 
                IsSuccess = false, 
                ErrorMessages = new[] { "undefined variable: undefined_variable" }
            };
        }
        
        if (source.Contains("syntax_error"))
        {
            return new CompilationResult 
            { 
                IsSuccess = false, 
                ErrorMessages = new[] { "syntax error: unexpected token" }
            };
        }
        
        // Simulate successful compilation with realistic timing
        System.Threading.Thread.Sleep(50 + Random.Shared.Next(100));
        
        return new CompilationResult { IsSuccess = true, ErrorMessages = Array.Empty<string>() };
    }

    public IReadOnlyList<ShaderCompileLog> GetCompileLogs() => _compileLogs.AsReadOnly();
}
```

## Graphics Resource Management Testing

### Resource Management Test Suite

```csharp
/// <summary>
/// Graphics resource management testing (buffers, textures, shaders)
/// </summary>
[TestFixture]
[Category("Resources")]
public class ResourceManagementTests
{
    private MockResourceManager _resourceManager;
    private GraphicsResourceValidator _validator;

    [SetUp]
    public void Setup()
    {
        _resourceManager = new MockResourceManager();
        _validator = new GraphicsResourceValidator();
    }

    [Test]
    public void Buffer_Allocation_And_Deallocation()
    {
        // Test buffer lifecycle management
        var bufferConfig = new BufferConfiguration
        {
            SizeInBytes = 1024,
            Usage = ResourceUsage.Dynamic,
            BindFlags = BindFlag.VertexBuffer,
            CpuAccessFlags = CpuAccessFlags.Write
        };

        // Allocate buffer
        var buffer = _resourceManager.CreateBuffer(bufferConfig);
        Assert.That(buffer, Is.Not.Null, "Buffer should be allocated");
        Assert.That(buffer.SizeInBytes, Is.EqualTo(1024), "Buffer size should match configuration");

        // Test buffer writes
        var testData = new float[256]; // 1024 bytes
        for (int i = 0; i < testData.Length; i++)
            testData[i] = (float)i;

        _resourceManager.WriteToBuffer(buffer, testData);
        var readData = _resourceManager.ReadFromBuffer<float>(buffer);
        
        Assert.That(readData, Is.EqualTo(testData), "Buffer read/write should preserve data");

        // Deallocate buffer
        _resourceManager.DestroyBuffer(buffer);
        Assert.Throws<ObjectDisposedException>(() => _resourceManager.ReadFromBuffer<float>(buffer));
    }

    [Test]
    public void Texture_Resource_Lifecycle()
    {
        // Test texture resource management
        var textureConfig = new TextureConfiguration
        {
            Width = 1024,
            Height = 1024,
            Format = Format.R8G8B8A8UNorm,
            MipLevels = 0, // Generate full mip chain
            BindFlags = BindFlag.ShaderResource
        };

        // Create texture
        var texture = _resourceManager.CreateTexture(textureConfig);
        Assert.Multiple(() =>
        {
            Assert.That(texture, Is.Not.Null, "Texture should be created");
            Assert.That(texture.Width, Is.EqualTo(1024), "Texture width should match");
            Assert.That(texture.Height, Is.EqualTo(1024), "Texture height should match");
            Assert.That(texture.Format, Is.EqualTo(Format.R8G8B8A8UNorm), "Format should match");
        });

        // Test texture data upload
        var textureData = GenerateTestTextureData(1024, 1024);
        _resourceManager.UpdateTexture(texture, textureData);
        
        // Verify texture data
        var readData = _resourceManager.ReadTextureData(texture);
        Assert.That(readData, Is.EqualTo(textureData), "Texture data should be preserved");

        // Test mip map generation
        _resourceManager.GenerateMipMaps(texture);
        var mipLevelCount = _resourceManager.GetMipLevelCount(texture);
        Assert.That(mipLevelCount, Is.GreaterThan(1), "Mip maps should be generated");

        // Clean up
        _resourceManager.DestroyTexture(texture);
    }

    [Test]
    public void Resource_Leak_Detection()
    {
        var initialResourceCount = _resourceManager.GetTotalResourceCount();
        
        // Create multiple resources without proper cleanup
        var resources = new List<IGraphicsResource>();
        for (int i = 0; i < 100; i++)
        {
            var buffer = _resourceManager.CreateBuffer(new BufferConfiguration 
            { 
                SizeInBytes = 1024,
                Usage = ResourceUsage.Dynamic 
            });
            resources.Add(buffer);
        }
        
        // Simulate cleanup failure
        var afterLeakCount = _resourceManager.GetTotalResourceCount();
        Assert.That(afterLeakCount, Is.EqualTo(initialResourceCount + 100), 
            "Resources should be tracked");
        
        // Test leak detection
        var leakReport = _resourceManager.DetectResourceLeaks();
        Assert.That(leakReport.HasLeaks, Is.True, "Resource leaks should be detected");
        Assert.That(leakReport.LeakedResourceCount, Is.EqualTo(100), 
            "All leaked resources should be reported");
    }

    private byte[] GenerateTestTextureData(int width, int height)
    {
        var data = new byte[width * height * 4];
        var random = new Random(42); // Deterministic pattern
        
        for (int i = 0; i < data.Length; i += 4)
        {
            data[i] = (byte)random.Next(256);     // R
            data[i + 1] = (byte)random.Next(256); // G
            data[i + 2] = (byte)random.Next(256); // B
            data[i + 3] = 255;                    // A
        }
        
        return data;
    }
}
```

### Graphics Resource Validator

```csharp
/// <summary>
/// Validator for graphics resource state and integrity
/// </summary>
public class GraphicsResourceValidator
{
    public ValidationResult ValidateBufferState(Buffer buffer)
    {
        var issues = new List<ValidationIssue>();

        // Check if buffer is allocated
        if (buffer == null)
        {
            issues.Add(new ValidationIssue(ValidationLevel.Error, "Buffer is null"));
            return new ValidationResult { IsValid = false, Issues = issues };
        }

        // Check buffer state
        if (buffer.SizeInBytes <= 0)
        {
            issues.Add(new ValidationIssue(ValidationLevel.Error, "Buffer has invalid size"));
        }

        // Check alignment
        if (buffer.SizeInBytes % 16 != 0)
        {
            issues.Add(new ValidationIssue(ValidationLevel.Warning, 
                "Buffer size is not 16-byte aligned"));
        }

        // Validate usage flags
        if ((buffer.Usage & ResourceUsage.Dynamic) != 0 && 
            (buffer.CpuAccessFlags & CpuAccessFlags.Write) == 0)
        {
            issues.Add(new ValidationIssue(ValidationLevel.Error,
                "Dynamic buffer must have write CPU access"));
        }

        return new ValidationResult 
        { 
            IsValid = !issues.Any(i => i.Level == ValidationLevel.Error),
            Issues = issues
        };
    }

    public ValidationResult ValidateTextureState(Texture2D texture)
    {
        var issues = new List<ValidationIssue>();

        if (texture == null)
        {
            issues.Add(new ValidationIssue(ValidationLevel.Error, "Texture is null"));
            return new ValidationResult { IsValid = false, Issues = issues };
        }

        // Validate dimensions
        if (texture.Width <= 0 || texture.Height <= 0)
        {
            issues.Add(new ValidationIssue(ValidationLevel.Error, 
                "Texture has invalid dimensions"));
        }

        // Validate format
        if (!IsValidTextureFormat(texture.Format))
        {
            issues.Add(new ValidationIssue(ValidationLevel.Error,
                "Texture format is not supported"));
        }

        // Check power-of-two requirement
        if (!IsPowerOfTwo(texture.Width) || !IsPowerOfTwo(texture.Height))
        {
            if ((texture.BindFlags & BindFlag.RenderTarget) != 0)
            {
                issues.Add(new ValidationIssue(ValidationLevel.Warning,
                    "Render target texture should use power-of-two dimensions"));
            }
        }

        return new ValidationResult 
        { 
            IsValid = !issues.Any(i => i.Level == ValidationLevel.Error),
            Issues = issues
        };
    }

    private bool IsValidTextureFormat(Format format)
    {
        var validFormats = new[]
        {
            Format.R8G8B8A8UNorm, Format.B8G8R8A8UNorm, Format.R32G32B32A32Float,
            Format.R16G16B16A16UNorm, Format.D24UNormS8UInt, Format.D32Float
        };
        
        return validFormats.Contains(format);
    }

    private bool IsPowerOfTwo(int value) => (value & (value - 1)) == 0;
}
```

## Performance Regression Detection

### Performance Monitoring System

```csharp
/// <summary>
/// Performance regression detection for rendering operations
/// </summary>
[TestFixture]
[Category("Performance")]
public class PerformanceRegressionTests
{
    private PerformanceMonitor _performanceMonitor;
    private BenchmarkDatabase _benchmarkDB;

    [SetUp]
    public void Setup()
    {
        _performanceMonitor = new PerformanceMonitor();
        _benchmarkDB = new BenchmarkDatabase();
    }

    [Test]
    public void Frame_Rendering_Performance_Regression()
    {
        // Establish baseline for frame rendering performance
        var frameCount = 100;
        var warmupFrames = 10;
        
        _performanceMonitor.StartMeasurement("frame_rendering");
        
        // Render multiple frames
        for (int i = 0; i < frameCount; i++)
        {
            if (i == warmupFrames)
            {
                _performanceMonitor.ResetMeasurement();
            }
            
            RenderTestFrame();
        }
        
        var metrics = _performanceMonitor.StopMeasurement("frame_rendering");
        
        // Get historical baseline
        var baseline = _benchmarkDB.GetBaseline("frame_rendering");
        
        Assert.Multiple(() =>
        {
            // Check average frame time
            Assert.That(metrics.AverageFrameTime, Is.LessThan(baseline.AverageFrameTime * 1.1f),
                "Average frame time should not regress by more than 10%");
            
            // Check 95th percentile frame time
            Assert.That(metrics.P95FrameTime, Is.LessThan(baseline.P95FrameTime * 1.15f),
                "P95 frame time should not regress by more than 15%");
            
            // Check for frame drops
            Assert.That(metrics.DroppedFrames, Is.EqualTo(0),
                "Should not have dropped frames in test scenario");
        });
    }

    [Test]
    public void Shader_Compilation_Performance_Regression()
    {
        var shaderCompilationTests = new[]
        {
            new ShaderTestData { Name = "vertex_shader", Type = ShaderType.Vertex },
            new ShaderTestData { Name = "pixel_shader", Type = ShaderType.Pixel },
            new ShaderTestData { Name = "compute_shader", Type = ShaderType.Compute },
            new ShaderTestData { Name = "geometry_shader", Type = ShaderType.Geometry }
        };

        foreach (var test in shaderCompilationTests)
        {
            var startTime = Stopwatch.StartNew();
            var result = CompileShader(test);
            startTime.Stop();
            
            Assert.That(result.IsSuccess, Is.True, $"{test.Name} should compile successfully");
            
            // Check compilation time against baseline
            var baseline = _benchmarkDB.GetShaderCompilationBaseline(test.Name);
            var tolerance = baseline.CompileTime * 0.2f; // 20% tolerance
            
            Assert.That(startTime.ElapsedMilliseconds, 
                Is.LessThan(baseline.CompileTime + tolerance),
                $"{test.Name} compilation time should not regress significantly");
        }
    }

    [Test]
    public void Memory_Usage_Regression_Detection()
    {
        // Monitor memory usage during typical rendering workload
        _performanceMonitor.StartMemoryTracking();
        
        // Execute memory-intensive operations
        for (int i = 0; i < 50; i++)
        {
            CreateAndDestroyTestResources();
        }
        
        var memoryMetrics = _performanceMonitor.StopMemoryTracking();
        
        // Get memory baseline
        var memoryBaseline = _benchmarkDB.GetMemoryBaseline("resource_management");
        
        Assert.Multiple(() =>
        {
            Assert.That(memoryMetrics.PeakMemoryUsage, 
                Is.LessThan(memoryBaseline.PeakMemoryUsage * 1.05f),
                "Peak memory usage should not regress by more than 5%");
            
            Assert.That(memoryMetrics.AverageMemoryUsage,
                Is.LessThan(memoryBaseline.AverageMemoryUsage * 1.05f),
                "Average memory usage should not regress by more than 5%");
            
            Assert.That(memoryMetrics.MemoryLeaks, Is.EqualTo(0),
                "No memory leaks should be detected");
        });
    }

    [Test]
    public void GPU_Utilization_Monitoring()
    {
        // Test GPU utilization consistency
        var gpuMetrics = _performanceMonitor.MeasureGPUUtilization(RenderTestScene);
        
        var baseline = _benchmarkDB.GetGPUUtilizationBaseline();
        
        Assert.Multiple(() =>
        {
            Assert.That(gpuMetrics.AverageUtilization, 
                Is.GreaterThan(50).And.LessThan(95),
                "GPU utilization should be within reasonable range");
            
            Assert.That(gpuMetrics.UtilizationStability, 
                Is.GreaterThan(0.8f),
                "GPU utilization should be stable (low variance)");
        });
    }

    private void RenderTestFrame()
    {
        // Simulate rendering work
        var vertices = GenerateTestVertices(1000);
        var indices = GenerateTestIndices(1000);
        
        // Render geometry
        RenderGeometry(vertices, indices);
        
        // Apply materials and render
        RenderMaterialPass();
    }
}
```

### Performance Monitor Implementation

```csharp
/// <summary>
/// Performance monitoring and regression detection system
/// </summary>
public class PerformanceMonitor
{
    private readonly Dictionary<string, PerformanceMeasurement> _activeMeasurements;
    private readonly List<PerformanceMetric> _metricsHistory;

    public PerformanceMonitor()
    {
        _activeMeasurements = new Dictionary<string, PerformanceMeasurement>();
        _metricsHistory = new List<PerformanceMetric>();
    }

    public void StartMeasurement(string testName)
    {
        _activeMeasurements[testName] = new PerformanceMeasurement
        {
            StartTime = DateTime.Now,
            FrameTimes = new List<double>(),
            MemorySnapshots = new List<MemorySnapshot>()
        };
    }

    public void RecordFrameTime(double frameTime)
    {
        foreach (var measurement in _activeMeasurements.Values)
        {
            measurement.FrameTimes.Add(frameTime);
        }
    }

    public void RecordMemorySnapshot(MemorySnapshot snapshot)
    {
        foreach (var measurement in _activeMeasurements.Values)
        {
            measurement.MemorySnapshots.Add(snapshot);
        }
    }

    public PerformanceMetrics StopMeasurement(string testName)
    {
        if (!_activeMeasurements.TryGetValue(testName, out var measurement))
        {
            throw new InvalidOperationException($"No active measurement found for {testName}");
        }

        _activeMeasurements.Remove(testName);

        var metrics = CalculateMetrics(measurement);
        _metricsHistory.Add(metrics);

        return metrics;
    }

    private PerformanceMetrics CalculateMetrics(PerformanceMeasurement measurement)
    {
        var frameTimes = measurement.FrameTimes;
        
        if (!frameTimes.Any())
        {
            return new PerformanceMetrics();
        }

        return new PerformanceMetrics
        {
            AverageFrameTime = frameTimes.Average(),
            P95FrameTime = CalculatePercentile(frameTimes, 95),
            P99FrameTime = CalculatePercentile(frameTimes, 99),
            MinFrameTime = frameTimes.Min(),
            MaxFrameTime = frameTimes.Max(),
            FrameTimeVariance = CalculateVariance(frameTimes),
            
            PeakMemoryUsage = measurement.MemorySnapshots.Any() 
                ? measurement.MemorySnapshots.Max(s => s.WorkingSetSize) 
                : 0,
            AverageMemoryUsage = measurement.MemorySnapshots.Any()
                ? measurement.MemorySnapshots.Average(s => s.WorkingSetSize)
                : 0,
            
            Timestamp = DateTime.Now
        };
    }

    private double CalculatePercentile(IEnumerable<double> values, double percentile)
    {
        var sortedValues = values.OrderBy(v => v).ToList();
        var index = (int)Math.Ceiling(percentile / 100.0 * sortedValues.Count) - 1;
        return sortedValues[Math.Max(0, Math.Min(index, sortedValues.Count - 1))];
    }

    private double CalculateVariance(IEnumerable<double> values)
    {
        var data = values.ToList();
        var average = data.Average();
        return data.Average(v => Math.Pow(v - average, 2));
    }
}

public class PerformanceMetrics
{
    public double AverageFrameTime { get; set; }
    public double P95FrameTime { get; set; }
    public double P99FrameTime { get; set; }
    public double MinFrameTime { get; set; }
    public double MaxFrameTime { get; set; }
    public double FrameTimeVariance { get; set; }
    
    public long PeakMemoryUsage { get; set; }
    public long AverageMemoryUsage { get; set; }
    
    public DateTime Timestamp { get; set; }
}
```

## Test Automation Pipeline Integration

### Test Orchestrator

```csharp
/// <summary>
/// Test automation pipeline orchestrator
/// </summary>
public class TestOrchestrator
{
    private readonly ITestExecutionEngine _executionEngine;
    private readonly ITestResultProcessor _resultProcessor;
    private readonly ITestArtifactManager _artifactManager;

    public TestOrchestrator(ITestExecutionEngine executionEngine)
    {
        _executionEngine = executionEngine;
        _resultProcessor = new TestResultProcessor();
        _artifactManager = new TestArtifactManager();
    }

    public async Task<TestExecutionResult> ExecuteTestSuite(TestSuite testSuite)
    {
        var executionContext = new TestExecutionContext
        {
            TestSuite = testSuite,
            StartTime = DateTime.Now,
            ExecutionId = Guid.NewGuid()
        };

        try
        {
            // Initialize test environment
            await InitializeTestEnvironment(executionContext);

            // Execute test categories in parallel where possible
            var categoryTasks = testSuite.Categories
                .Select(category => ExecuteCategory(category, executionContext))
                .ToArray();

            var categoryResults = await Task.WhenAll(categoryTasks);

            // Aggregate results
            var aggregatedResult = new TestExecutionResult
            {
                ExecutionId = executionContext.ExecutionId,
                StartTime = executionContext.StartTime,
                EndTime = DateTime.Now,
                Duration = DateTime.Now - executionContext.StartTime,
                CategoryResults = categoryResults,
                OverallSuccess = categoryResults.All(r => r.Success),
                TotalTests = categoryResults.Sum(r => r.TotalTests),
                PassedTests = categoryResults.Sum(r => r.PassedTests),
                FailedTests = categoryResults.Sum(r => r.FailedTests),
                SkippedTests = categoryResults.Sum(r => r.SkippedTests)
            };

            // Process results and generate artifacts
            await _resultProcessor.ProcessResults(aggregatedResult);
            await _artifactManager.SaveArtifacts(executionContext, aggregatedResult);

            return aggregatedResult;
        }
        catch (Exception ex)
        {
            return new TestExecutionResult
            {
                ExecutionId = executionContext.ExecutionId,
                StartTime = executionContext.StartTime,
                EndTime = DateTime.Now,
                Success = false,
                Error = ex
            };
        }
        finally
        {
            // Cleanup test environment
            await CleanupTestEnvironment(executionContext);
        }
    }

    private async Task<CategoryResult> ExecuteCategory(TestCategory category, TestExecutionContext context)
    {
        var categoryResult = new CategoryResult
        {
            CategoryName = category.Name,
            StartTime = DateTime.Now
        };

        var tests = category.Tests.ToList();
        categoryResult.TotalTests = tests.Count;

        // Execute tests with optional parallelization
        if (category.AllowParallelExecution)
        {
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            var testResults = new ConcurrentBag<TestResult>();
            
            await Parallel.ForEachAsync(tests, parallelOptions, async (test, ct) =>
            {
                var result = await ExecuteSingleTest(test, context);
                testResults.Add(result);
            });

            categoryResult.TestResults = testResults.ToList();
        }
        else
        {
            var testResults = new List<TestResult>();
            foreach (var test in tests)
            {
                var result = await ExecuteSingleTest(test, context);
                testResults.Add(result);
            }
            categoryResult.TestResults = testResults;
        }

        // Calculate statistics
        categoryResult.PassedTests = categoryResult.TestResults.Count(r => r.Status == TestStatus.Passed);
        categoryResult.FailedTests = categoryResult.TestResults.Count(r => r.Status == TestStatus.Failed);
        categoryResult.SkippedTests = categoryResult.TestResults.Count(r => r.Status == TestStatus.Skipped);
        categoryResult.Success = categoryResult.FailedTests == 0;
        categoryResult.EndTime = DateTime.Now;
        categoryResult.Duration = categoryResult.EndTime - categoryResult.StartTime;

        return categoryResult;
    }
}
```

### Test Suite Configuration

```csharp
/// <summary>
/// Test suite configuration and organization
/// </summary>
public class TestSuite
{
    public string Name { get; set; }
    public string Version { get; set; }
    public List<TestCategory> Categories { get; set; } = new();
    
    public TestCategory Rendering { get; set; }
    public TestCategory Visual { get; set; }
    public TestCategory Shaders { get; set; }
    public TestCategory Resources { get; set; }
    public TestCategory Performance { get; set; }
    public TestCategory Integration { get; set; }

    public static TestSuite CreateDefaultSuite()
    {
        return new TestSuite
        {
            Name = "TiXL Graphics Test Suite",
            Version = "1.0.0",
            Rendering = new TestCategory
            {
                Name = "Rendering",
                Priority = TestPriority.High,
                AllowParallelExecution = true,
                Tests = new List<ITestCase>
                {
                    new DirectX12PipelineTests(),
                    new RenderTargetTests(),
                    new StateManagementTests()
                }
            },
            Visual = new TestCategory
            {
                Name = "Visual",
                Priority = TestPriority.High,
                AllowParallelExecution = false, // Visual tests need deterministic order
                Tests = new List<ITestCase>
                {
                    new VisualRegressionTests(),
                    new ScreenshotComparisonTests()
                }
            },
            Shaders = new TestCategory
            {
                Name = "Shaders",
                Priority = TestPriority.High,
                AllowParallelExecution = true,
                Tests = new List<ITestCase>
                {
                    new ShaderTests(),
                    new ShaderPerformanceTests()
                }
            },
            Resources = new TestCategory
            {
                Name = "Resources",
                Priority = TestPriority.Medium,
                AllowParallelExecution = true,
                Tests = new List<ITestCase>
                {
                    new ResourceManagementTests(),
                    new ResourceLeakTests()
                }
            },
            Performance = new TestCategory
            {
                Name = "Performance",
                Priority = TestPriority.Medium,
                AllowParallelExecution = true,
                Tests = new List<ITestCase>
                {
                    new PerformanceRegressionTests(),
                    new MemoryUsageTests()
                }
            },
            Integration = new TestCategory
            {
                Name = "Integration",
                Priority = TestPriority.Medium,
                AllowParallelExecution = false,
                Tests = new List<ITestCase>
                {
                    new FullPipelineIntegrationTests(),
                    new EndToEndRenderingTests()
                }
            }
        };
    }
}
```

## Baseline Image Management

### Baseline Manager System

```csharp
/// <summary>
/// Baseline image management and update procedures
/// </summary>
public class BaselineManager
{
    private readonly string _baselineDirectory;
    private readonly string _testOutputDirectory;
    private readonly Dictionary<string, BaselineImage> _loadedBaselines;
    private readonly HashSet<string> _modifiedBaselines;

    public BaselineManager(string baseDirectory = null)
    {
        _baselineDirectory = baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TiXL", "GraphicsTests", "Baselines");
        
        _testOutputDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TiXL", "GraphicsTests", "TestOutput");
        
        _loadedBaselines = new Dictionary<string, BaselineImage>();
        _modifiedBaselines = new HashSet<string>();

        InitializeDirectories();
    }

    public async Task<BaselineImage> GetBaselineAsync(string testName, bool createIfMissing = false)
    {
        if (_loadedBaselines.TryGetValue(testName, out var cached))
        {
            return cached;
        }

        var baselinePath = GetBaselinePath(testName);
        
        if (File.Exists(baselinePath))
        {
            var baseline = await LoadBaselineFromFile(baselinePath);
            _loadedBaselines[testName] = baseline;
            return baseline;
        }
        
        if (createIfMissing)
        {
            var newBaseline = new BaselineImage
            {
                Name = testName,
                CreatedDate = DateTime.Now,
                Version = "1.0"
            };
            
            _loadedBaselines[testName] = newBaseline;
            return newBaseline;
        }

        throw new FileNotFoundException($"Baseline image not found for test: {testName}");
    }

    public async Task UpdateBaselineAsync(string testName, Bitmap newImage, string updateReason = null)
    {
        var baselinePath = GetBaselinePath(testName);
        var backupPath = baselinePath + ".backup";
        
        // Create backup of existing baseline
        if (File.Exists(baselinePath))
        {
            File.Copy(baselinePath, backupPath, true);
        }

        try
        {
            // Save new baseline
            await SaveBaselineToFile(newImage, baselinePath);
            
            // Update memory cache
            var baseline = new BaselineImage
            {
                Name = testName,
                ImageData = newImage,
                CreatedDate = DateTime.Now,
                UpdatedDate = DateTime.Now,
                Version = IncrementVersion(_loadedBaselines.GetValueOrDefault(testName)?.Version ?? "1.0"),
                UpdateReason = updateReason ?? "Manual update"
            };
            
            _loadedBaselines[testName] = baseline;
            _modifiedBaselines.Add(testName);

            // Update baseline metadata
            await UpdateBaselineMetadata(baselinePath, baseline);
        }
        catch
        {
            // Restore backup on failure
            if (File.Exists(backupPath))
            {
                File.Copy(backupPath, baselinePath, true);
            }
            throw;
        }
        finally
        {
            // Clean up backup
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
        }
    }

    public async Task<List<BaselineDiff>> CompareBaselinesAsync(List<string> testNames = null)
    {
        testNames ??= _loadedBaselines.Keys.ToList();
        var diffs = new List<BaselineDiff>();

        foreach (var testName in testNames)
        {
            try
            {
                var currentImage = await LoadTestOutputImage(testName);
                var baseline = await GetBaselineAsync(testName);
                
                if (currentImage != null && baseline?.ImageData != null)
                {
                    var diff = CalculateImageDiff(currentImage, baseline.ImageData);
                    diffs.Add(new BaselineDiff
                    {
                        TestName = testName,
                        BaselineVersion = baseline.Version,
                        DifferencePercentage = diff.Difference,
                        SSIMScore = diff.SSIM,
                        HasSignificantDifference = diff.Difference > 0.05f
                    });
                }
            }
            catch (Exception ex)
            {
                diffs.Add(new BaselineDiff
                {
                    TestName = testName,
                    Error = ex.Message
                });
            }
        }

        return diffs;
    }

    public async Task GenerateBaselineReportAsync(string outputPath)
    {
        var allBaselines = Directory.GetFiles(_baselineDirectory, "*.png", SearchOption.AllDirectories);
        var report = new BaselineReport
        {
            GeneratedDate = DateTime.Now,
            TotalBaselines = allBaselines.Length,
            BaselineVersions = new Dictionary<string, string>()
        };

        foreach (var baselinePath in allBaselines)
        {
            var metadata = await LoadBaselineMetadata(baselinePath);
            if (metadata != null)
            {
                report.BaselineVersions[Path.GetFileNameWithoutExtension(baselinePath)] = metadata.Version;
            }
        }

        // Generate markdown report
        await GenerateMarkdownReport(report, outputPath);
    }

    private string GetBaselinePath(string testName)
    {
        var sanitizedName = Path.GetInvalidFileNameChars()
            .Aggregate(testName, (current, invalidChar) => current.Replace(invalidChar, '_'));
        
        return Path.Combine(_baselineDirectory, $"{sanitizedName}.png");
    }

    private void InitializeDirectories()
    {
        Directory.CreateDirectory(_baselineDirectory);
        Directory.CreateDirectory(_testOutputDirectory);
    }

    private async Task<BaselineImage> LoadBaselineFromFile(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var image = await Task.Run(() => new Bitmap(stream));
        
        var metadata = await LoadBaselineMetadata(filePath);
        
        return new BaselineImage
        {
            Name = Path.GetFileNameWithoutExtension(filePath),
            ImageData = image,
            CreatedDate = metadata?.CreatedDate ?? File.GetCreationTime(filePath),
            UpdatedDate = metadata?.UpdatedDate ?? File.GetLastWriteTime(filePath),
            Version = metadata?.Version ?? "1.0"
        };
    }

    private async Task SaveBaselineToFile(Bitmap image, string filePath)
    {
        await Task.Run(() => image.Save(filePath, ImageFormat.Png));
    }

    private async Task<BaselineMetadata> LoadBaselineMetadata(string imagePath)
    {
        var metadataPath = imagePath + ".meta";
        if (!File.Exists(metadataPath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(metadataPath);
        return JsonSerializer.Deserialize<BaselineMetadata>(json);
    }

    private async Task UpdateBaselineMetadata(string imagePath, BaselineImage baseline)
    {
        var metadataPath = imagePath + ".meta";
        var metadata = new BaselineMetadata
        {
            Name = baseline.Name,
            CreatedDate = baseline.CreatedDate,
            UpdatedDate = baseline.UpdatedDate,
            Version = baseline.Version,
            UpdateReason = baseline.UpdateReason
        };

        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(metadataPath, json);
    }

    private string IncrementVersion(string currentVersion)
    {
        if (Version.TryParse(currentVersion, out var version))
        {
            return new Version(version.Major, version.Minor + 1).ToString();
        }
        return "1.0";
    }
}
```

### Baseline Update Workflow

```csharp
/// <summary>
/// Automated baseline update procedures and approval workflow
/// </summary>
public class BaselineUpdateWorkflow
{
    private readonly BaselineManager _baselineManager;
    private readonly GitVersionControl _versionControl;

    public BaselineUpdateWorkflow(BaselineManager baselineManager, GitVersionControl versionControl)
    {
        _baselineManager = baselineManager;
        _versionControl = versionControl;
    }

    public async Task<BaselineUpdateResult> ProposeBaselineUpdatesAsync(List<string> testNames, string commitMessage)
    {
        var updateResults = new List<BaselineUpdate>();
        var allUpdatesSuccessful = true;

        foreach (var testName in testNames)
        {
            try
            {
                // Load current test output
                var currentImage = await LoadCurrentTestOutput(testName);
                if (currentImage == null)
                {
                    updateResults.Add(new BaselineUpdate
                    {
                        TestName = testName,
                        Success = false,
                        Error = "No current test output available"
                    });
                    allUpdatesSuccessful = false;
                    continue;
                }

                // Update baseline
                await _baselineManager.UpdateBaselineAsync(testName, currentImage, commitMessage);
                
                updateResults.Add(new BaselineUpdate
                {
                    TestName = testName,
                    Success = true,
                    PreviousVersion = "1.0", // Would be loaded from metadata
                    NewVersion = "1.1"
                });
            }
            catch (Exception ex)
            {
                updateResults.Add(new BaselineUpdate
                {
                    TestName = testName,
                    Success = false,
                    Error = ex.Message
                });
                allUpdatesSuccessful = false;
            }
        }

        // Create Git commit with changes
        if (allUpdatesSuccessful)
        {
            await CreateBaselineCommit(commitMessage, testNames);
        }

        return new BaselineUpdateResult
        {
            Success = allUpdatesSuccessful,
            Updates = updateResults,
            CommitHash = allUpdatesSuccessful ? await _versionControl.GetLatestCommitHash() : null
        };
    }

    private async Task<Bitmap> LoadCurrentTestOutput(string testName)
    {
        // Implementation would load the most recent test output for the given test
        var outputPath = Path.Combine(_baselineManager.TestOutputDirectory, $"{testName}_current.png");
        
        if (!File.Exists(outputPath))
        {
            return null;
        }

        using var stream = File.OpenRead(outputPath);
        return await Task.Run(() => new Bitmap(stream));
    }

    private async Task CreateBaselineCommit(string commitMessage, List<string> testNames)
    {
        var changedFiles = testNames.Select(testName => 
            Path.Combine(_baselineManager.BaselineDirectory, $"{testName}.png"));

        await _versionControl.StageFiles(changedFiles);
        await _versionControl.Commit(commitMessage, new GitAuthor
        {
            Name = "TiXL Graphics Test System",
            Email = "tests@tixl.app"
        });
    }
}
```

## Headless Testing Environment

### Headless Graphics Device

```csharp
/// <summary>
/// Headless graphics device for testing without display adapter
/// </summary>
public class HeadlessGraphicsDevice : IDisposable
{
    private readonly ID3D12Device _device;
    private readonly ID3D12CommandQueue _commandQueue;
    private readonly ID3D12CommandAllocator _commandAllocator;
    private readonly ID3D12GraphicsCommandList _commandList;
    
    private bool _disposed;

    public HeadlessGraphicsDevice()
    {
        // Initialize DirectX 12 device in headless mode
        var deviceInfo = D3D12GetHardwareAdapter();
        _device = D3D12CreateDevice(deviceInfo);
        
        // Create command queue
        var queueDesc = new D3D12_COMMAND_QUEUE_DESC
        {
            Flags = D3D12_COMMAND_QUEUE_FLAG_NONE,
            Type = D3D12_COMMAND_LIST_TYPE_DIRECT
        };
        _device.CreateCommandQueue(queueGuid, out _commandQueue);
        
        // Create command allocator and list for recording commands
        _device.CreateCommandAllocator(D3D12_COMMAND_LIST_TYPE_DIRECT, out _commandAllocator);
        _device.CreateCommandList(0, D3D12_COMMAND_LIST_TYPE_DIRECT, _commandAllocator, null, out _commandList);
    }

    public void CreateOffscreenRenderTarget(int width, int height, Format format, out ID3D12Resource resource, out CPUDescriptorHandle rtvHandle)
    {
        // Create offscreen render target texture
        var desc = new D3D12_RESOURCE_DESC
        {
            Dimension = D3D12_RESOURCE_DIMENSION_TEXTURE2D,
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            Format = format,
            SampleDesc = new DXGI_SAMPLE_DESC { Count = 1, Quality = 0 },
            Layout = D3D12_TEXTURE_LAYOUT_UNKNOWN,
            Flags = D3D12_RESOURCE_FLAG_ALLOW_RENDER_TARGET
        };

        var heapProperties = new D3D12_HEAP_PROPERTIES
        {
            Type = D3D12_HEAP_TYPE_DEFAULT,
            CPUPageProperty = D3D12_CPU_PAGE_PROPERTY_UNKNOWN,
            MemoryPoolPreference = D3D12_MEMORY_POOL_UNKNOWN
        };

        _device.CreateCommittedResource(
            heapProperties,
            D3D12_HEAP_FLAG_NONE,
            desc,
            D3D12_RESOURCE_STATE_COMMON,
            null,
            out resource);

        // Create render target view
        rtvHandle = AllocateDescriptorHandle();
        var rtvDesc = new D3D12_RENDER_TARGET_VIEW_DESC
        {
            Format = format,
            ViewDimension = D3D12_RTV_DIMENSION_TEXTURE2D,
            Texture2D = new D3D12_TEX2D_RTV { MipSlice = 0 }
        };

        _device.CreateRenderTargetView(resource, rtvDesc, rtvHandle);
    }

    public void CreateDepthStencilBuffer(int width, int height, Format format, out ID3D12Resource resource, out CPUDescriptorHandle dsvHandle)
    {
        // Similar implementation for depth-stencil buffer
        var desc = new D3D12_RESOURCE_DESC
        {
            Dimension = D3D12_RESOURCE_DIMENSION_TEXTURE2D,
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            Format = format,
            SampleDesc = new DXGI_SAMPLE_DESC { Count = 1, Quality = 0 },
            Layout = D3D12_TEXTURE_LAYOUT_UNKNOWN,
            Flags = D3D12_RESOURCE_FLAG_ALLOW_DEPTH_STENCIL
        };

        _heapProperties = new D3D12_HEAP_PROPERTIES
        {
            Type = D3D12_HEAP_TYPE_DEFAULT,
            CPUPageProperty = D3D12_CPU_PAGE_PROPERTY_UNKNOWN,
            MemoryPoolPreference = D3D12_MEMORY_POOL_UNKNOWN
        };

        _device.CreateCommittedResource(
            _heapProperties,
            D3D12_HEAP_FLAG_NONE,
            desc,
            D3D12_RESOURCE_STATE_DEPTH_WRITE,
            null,
            out resource);

        // Create depth-stencil view
        dsvHandle = AllocateDescriptorHandle();
        var dsvDesc = new D3D12_DEPTH_STENCIL_VIEW_DESC
        {
            Format = format,
            ViewDimension = D3D12_DSV_DIMENSION_TEXTURE2D,
            Flags = D3D12_DSV_FLAG_NONE,
            Texture2D = new D3D12_TEX2D_DSV { MipSlice = 0 }
        };

        _device.CreateDepthStencilView(resource, dsvDesc, dsvHandle);
    }

    public void ExecuteCommandList()
    {
        _commandList.Close();
        _commandQueue.ExecuteCommandList(_commandList);
        
        // Wait for completion
        var fenceEvent = new AutoResetEvent(false);
        var fence = _device.CreateFence(0, D3D12_FENCE_FLAG_NONE);
        _commandQueue.Signal(fence, 1);
        fence.SetEventOnCompletion(1, fenceEvent.Handle);
        fenceEvent.WaitOne();
    }

    public byte[] ReadBackTexture(ID3D12Resource resource, int width, int height, int pixelSize)
    {
        // Create readback buffer
        var readbackDesc = new D3D12_RESOURCE_DESC
        {
            Dimension = D3D12_RESOURCE_DIMENSION_BUFFER,
            Width = (uint)(width * height * pixelSize),
            Height = 1,
            DepthOrArraySize = 1,
            MipLevels = 1,
            Format = Format.Unknown,
            SampleDesc = new DXGI_SAMPLE_DESC { Count = 1, Quality = 0 },
            Layout = D3D12_TEXTURE_LAYOUT_UNKNOWN,
            Flags = D3D12_RESOURCE_FLAG_NONE
        };

        var heapProperties = new D3D12_HEAP_PROPERTIES
        {
            Type = D3D12_HEAP_TYPE_READBACK,
            CPUPageProperty = D3D12_CPU_PAGE_PROPERTY_UNKNOWN,
            MemoryPoolPreference = D3D12_MEMORY_POOL_UNKNOWN
        };

        _device.CreateCommittedResource(
            heapProperties,
            D3D12_HEAP_FLAG_NONE,
            readbackDesc,
            D3D12_RESOURCE_STATE_COPY_DEST,
            null,
            out ID3D12Resource readbackBuffer);

        // Copy resource to readback buffer
        var sourceLocation = resource.GetGPUVirtualAddress();
        var readbackLocation = readbackBuffer.GetGPUVirtualAddress();

        _commandList.Reset(_commandAllocator, null);
        
        var copyRegion = new D3D12_TEXTURE_COPY_REGION
        {
            pSrcResource = resource,
            Type = D3D12_TEXTURE_COPY_TYPE_SUBRESOURCE_INDEX,
            SubresourceIndex = 0,
            SrcOrigin = new D3D12_PLACED_SUBRESOURCE_FOOTPRINT
            {
                Offset = 0,
                Footprint = new D3D12_SUBRESOURCE_FOOTPRINT
                {
                    Format = Format.Unknown,
                    Width = (uint)width,
                    Height = (uint)height,
                    Depth = 1,
                    RowPitch = (uint)(width * pixelSize)
                }
            }
        };

        var destRegion = new D3D12_TEXTURE_COPY_LOCATION
        {
            pResource = readbackBuffer,
            Type = D3D12_TEXTURE_COPY_TYPE_PLACED_FOOTPRINT,
            PlacedFootprint = new D3D12_PLACED_SUBRESOURCE_FOOTPRINT
            {
                Offset = 0,
                Footprint = new D3D12_SUBRESOURCE_FOOTPRINT
                {
                    Format = Format.Unknown,
                    Width = (uint)width,
                    Height = (uint)height,
                    Depth = 1,
                    RowPitch = (uint)(width * pixelSize)
                }
            }
        };

        _commandList.CopyTextureRegion(destRegion, 0, 0, 0, copyRegion, null);
        _commandList.Close();

        ExecuteCommandList();

        // Read data from readback buffer
        var ptr = readbackBuffer.Map(0, null);
        var data = new byte[width * height * pixelSize];
        Marshal.Copy(ptr, data, 0, data.Length);
        readbackBuffer.Unmap(0, null);

        readbackBuffer.Dispose();

        return data;
    }

    private ID3D12Device5 D3D12GetHardwareAdapter()
    {
        // Mock implementation - in real scenario would enumerate hardware adapters
        return D3D12CreateDevice();
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _commandList?.Dispose();
                _commandAllocator?.Dispose();
                _commandQueue?.Dispose();
                _device?.Dispose();
            }

            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
```

## Mock Graphics Contexts

### Mock Graphics Device

```csharp
/// <summary>
/// Mock graphics device for comprehensive testing without DirectX dependency
/// </summary>
public class MockGraphicsContext : IGraphicsContext, IDisposable
{
    private readonly MockGraphicsDevice _device;
    private readonly List<MockResource> _resources;
    private readonly Dictionary<string, object> _state;

    public MockGraphicsContext()
    {
        _device = new MockGraphicsDevice();
        _resources = new List<MockResource>();
        _state = new Dictionary<string, object>();
    }

    public IGraphicsDevice Device => _device;

    public void CreateBuffer<T>(T[] data, BufferUsage usage, out MockBuffer buffer) where T : unmanaged
    {
        buffer = new MockBuffer
        {
            Data = data,
            Size = data.Length * Marshal.SizeOf<T>(),
            Usage = usage,
            Id = Guid.NewGuid()
        };
        
        _resources.Add(buffer);
    }

    public void CreateTexture(int width, int height, Format format, out MockTexture texture)
    {
        texture = new MockTexture
        {
            Width = width,
            Height = height,
            Format = format,
            Data = new byte[width * height * GetPixelSize(format)],
            Id = Guid.NewGuid()
        };
        
        _resources.Add(texture);
    }

    public void CompileShader(string source, ShaderType type, out MockShader shader)
    {
        // Simulate shader compilation
        var compileResult = SimulateShaderCompilation(source, type);
        
        shader = new MockShader
        {
            Source = source,
            Type = type,
            ByteCode = compileResult.IsSuccess ? GenerateMockByteCode(type) : null,
            CompilationErrors = compileResult.ErrorMessages,
            IsValid = compileResult.IsSuccess,
            Id = Guid.NewGuid()
        };
    }

    public void RenderScene(TestScene scene)
    {
        // Simulate scene rendering
        _state["LastRenderedScene"] = scene;
        _state["LastRenderTime"] = DateTime.Now;
        
        // Update any animated elements
        foreach (var renderObject in scene.Objects)
        {
            UpdateRenderObject(renderObject);
        }
    }

    public Bitmap CaptureScreenshot(int width, int height)
    {
        // Create mock screenshot
        var bitmap = new Bitmap(width, height);
        var graphics = Graphics.FromImage(bitmap);
        
        // Fill with a test pattern
        var brush = new LinearGradientBrush(
            new Rectangle(0, 0, width, height),
            Color.Red, Color.Blue, 45.0f);
        
        graphics.FillRectangle(brush, 0, 0, width, height);
        
        // Add test identifier
        var text = $"Test Render {DateTime.Now:HH:mm:ss}";
        graphics.DrawString(text, new Font("Arial", 12), Brushes.White, 10, 10);
        
        return bitmap;
    }

    public List<MockResource> GetResourceUsageReport()
    {
        return _resources.ToList();
    }

    public bool HasResourceLeaks() => _resources.Any(r => !r.IsDisposed);

    public void Reset()
    {
        _resources.Clear();
        _state.Clear();
    }

    private CompilationResult SimulateShaderCompilation(string source, ShaderType type)
    {
        // Simulate compilation timing
        var compileTime = type switch
        {
            ShaderType.Vertex => 50 + Random.Shared.Next(100),
            ShaderType.Pixel => 75 + Random.Shared.Next(150),
            ShaderType.Compute => 100 + Random.Shared.Next(200),
            _ => 60 + Random.Shared.Next(120)
        };

        System.Threading.Thread.Sleep(compileTime);

        // Simulate common compilation failures
        if (source.Contains("undefined"))
        {
            return new CompilationResult
            {
                IsSuccess = false,
                ErrorMessages = new[] { "undefined identifier" }
            };
        }

        if (source.Contains("syntax_error"))
        {
            return new CompilationResult
            {
                IsSuccess = false,
                ErrorMessages = new[] { "syntax error" }
            };
        }

        return new CompilationResult { IsSuccess = true, ErrorMessages = Array.Empty<string>() };
    }

    private byte[] GenerateMockByteCode(ShaderType type)
    {
        // Generate deterministic mock byte code
        var seed = type switch
        {
            ShaderType.Vertex => 0x56455254, // 'VERT'
            ShaderType.Pixel => 0x50495845, // 'PIXE'
            ShaderType.Compute => 0x434D5055, // 'CMPU'
            _ => 0x47454E52  // 'GENR'
        };

        var random = new Random(seed);
        var size = random.Next(1024, 4096);
        var data = new byte[size];

        random.NextBytes(data);
        
        // Set some known patterns
        Array.Copy(BitConverter.GetBytes(seed), 0, data, 0, 4);
        Array.Copy(BitConverter.GetBytes((uint)size), 0, data, 4, 4);
        
        return data;
    }

    private int GetPixelSize(Format format)
    {
        return format switch
        {
            Format.R8G8B8A8UNorm => 4,
            Format.B8G8R8A8UNorm => 4,
            Format.D24UNormS8UInt => 4,
            Format.R32G32B32A32Float => 16,
            _ => 4
        };
    }

    private void UpdateRenderObject(MockRenderObject obj)
    {
        // Simulate animation updates
        if (obj.AnimationData != null)
        {
            obj.Position = obj.AnimationData.UpdatePosition(obj.Position, DateTime.Now);
            obj.Rotation = obj.AnimationData.UpdateRotation(obj.Rotation, DateTime.Now);
        }
    }

    public void Dispose()
    {
        _resources.Clear();
        _state.Clear();
    }
}
```

### Mock Resource Classes

```csharp
/// <summary>
/// Mock graphics resource classes for testing
/// </summary>
public abstract class MockResource
{
    public Guid Id { get; set; }
    public bool IsDisposed { get; set; }
    public DateTime CreatedTime { get; set; } = DateTime.Now;
}

public class MockBuffer : MockResource
{
    public Array Data { get; set; }
    public int Size { get; set; }
    public BufferUsage Usage { get; set; }
    public BindFlags BindFlags { get; set; }
}

public class MockTexture : MockResource
{
    public int Width { get; set; }
    public int Height { get; set; }
    public Format Format { get; set; }
    public byte[] Data { get; set; }
    public int MipLevels { get; set; } = 1;
}

public class MockShader : MockResource
{
    public string Source { get; set; }
    public ShaderType Type { get; set; }
    public byte[] ByteCode { get; set; }
    public string[] CompilationErrors { get; set; }
    public bool IsValid { get; set; }
    public DateTime CompilationTime { get; set; } = DateTime.Now;
}

public class MockRenderTarget : MockResource
{
    public int Width { get; set; }
    public int Height { get; set; }
    public Format Format { get; set; }
    public MockTexture Texture { get; set; }
}

public class MockRenderObject
{
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
    public Vector3 Scale { get; set; } = Vector3.One;
    public Material Material { get; set; }
    public Mesh Mesh { get; set; }
    public AnimationData AnimationData { get; set; }
}
```

## CI/CD Integration

### GitHub Actions Workflow

```yaml
name: Graphics Regression Testing

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]
  schedule:
    # Run nightly performance tests
    - cron: '0 2 * * *'

jobs:
  graphics-tests:
    runs-on: windows-latest
    strategy:
      matrix:
        test-suite: [Rendering, Visual, Shaders, Resources, Performance]
    
    steps:
    - uses: actions/checkout@v4
      with:
        fetch-depth: 0  # Fetch full history for baseline management

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 9.0.x

    - name: Install dependencies
      run: dotnet restore

    - name: Build solution
      run: dotnet build --configuration Release --no-restore

    - name: Install NuGet packages
      run: |
        dotnet add Tests/TiXL.Tests.csproj package coverlet.collector
        dotnet add Tests/TiXL.Tests.cspackage ImageSharp.Compare

    - name: Run Graphics Tests
      run: |
        dotnet test Tests/TiXL.Tests.csproj \
          --configuration Release \
          --no-build \
          --logger trx \
          --collect:"XPlat Code Coverage" \
          -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover \
          -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.ExcludeByFile=**/Mock** \
          -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.ExcludeByAttribute=GeneratedCodeAttribute

    - name: Upload Test Results
      uses: actions/upload-artifact@v3
      if: always()
      with:
        name: test-results-${{ matrix.test-suite }}
        path: |
          Tests/TestResults/
          Tests/bin/Release/net9.0/coverage.opencover.xml

    - name: Upload Baseline Images
      if: github.event_name == 'pull_request' && contains(github.event.head_commit.message, '[update-baselines]')
      uses: actions/upload-artifact@v3
      with:
        name: baseline-images-${{ matrix.test-suite }}
        path: |
          Tests/Baselines/
          Tests/TestOutput/

    - name: Upload Performance Metrics
      if: always()
      uses: actions/upload-artifact@v3
      with:
        name: performance-metrics-${{ matrix.test-suite }}
        path: Tests/PerformanceReports/

    - name: Comment PR with Results
      if: github.event_name == 'pull_request'
      uses: actions/github-script@v6
      with:
        script: |
          const fs = require('fs');
          
          // Load test results
          const results = fs.readFileSync('Tests/TestResults/results.json', 'utf8');
          const data = JSON.parse(results);
          
          // Create comment
          const comment = `## Graphics Test Results - ${data.suite}
          
          ### Test Summary
          - **Total Tests**: ${data.totalTests}
          - **Passed**: ${data.passedTests} ✅
          - **Failed**: ${data.failedTests} ❌
          - **Skipped**: ${data.skippedTests} ⏭️
          
          ### Performance Metrics
          - **Average Frame Time**: ${data.avgFrameTime}ms
          - **P95 Frame Time**: ${data.p95FrameTime}ms
          - **Memory Usage**: ${data.memoryUsage}MB
          
          ### Visual Regression Status
          - **Visual Tests**: ${data.visualTests}
          - **Baseline Matches**: ${data.baselineMatches}
          - **Significant Differences**: ${data.significantDifferences}
          
          ${data.failedTests > 0 ? '⚠️ **Some tests failed. Please review the detailed results.**' : '✅ **All tests passed!**'}
          `;
          
          github.rest.issues.createComment({
            issue_number: context.issue.number,
            owner: context.repo.owner,
            repo: context.repo.repo,
            body: comment
          });

  baseline-update:
    runs-on: windows-latest
    needs: [graphics-tests]
    if: contains(github.event.head_commit.message, '[update-baselines]') && github.ref == 'refs/heads/main'
    
    steps:
    - uses: actions/checkout@v4
      with:
        token: ${{ secrets.GITHUB_TOKEN }}

    - name: Download Test Artifacts
      uses: actions/download-artifact@v3
      with:
        path: test-results/

    - name: Update Baselines
      run: |
        dotnet run --project Tests/TiXL.Tests.csproj update-baselines \
          --input-path test-results \
          --commit-message "Update graphics baselines - $(date)"

    - name: Commit Baseline Changes
      run: |
        git config user.name "GitHub Actions Bot"
        git config user.email "actions@github.com"
        git add Tests/Baselines/
        git commit -m "Update graphics baselines from automated testing"
        git push
```

### PowerShell Test Runner

```powershell
#!/usr/bin/env pwsh
<#
.SYNOPSIS
    TiXL Graphics Test Runner for CI/CD
#>

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("All", "Rendering", "Visual", "Shaders", "Resources", "Performance", "Integration")]
    [string]$TestSuite = "All",
    
    [Parameter(Mandatory=$false)]
    [switch]$UpdateBaselines,
    
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "TestResults",
    
    [Parameter(Mandatory=$false)]
    [switch]$GenerateReport
)

$ErrorActionPreference = "Stop"
$script:ExitCode = 0

Write-Host "TiXL Graphics Test Runner" -ForegroundColor Cyan
Write-Host "=========================" -ForegroundColor Cyan
Write-Host "Test Suite: $TestSuite"
Write-Host "Update Baselines: $UpdateBaselines"
Write-Host "Output Path: $OutputPath"
Write-Host ""

# Verify prerequisites
try {
    $null = Get-Command "dotnet" -ErrorAction Stop
    Write-Host "✓ .NET SDK found" -ForegroundColor Green
} catch {
    Write-Error "❌ .NET SDK not found. Please install .NET 9.0 SDK."
    exit 1
}

# Create output directory
New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null

# Build solution
Write-Host "Building solution..." -ForegroundColor Yellow
$buildResult = & dotnet build --configuration Release --no-restore 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Error "❌ Build failed"
    Write-Host $buildResult
    exit 1
}
Write-Host "✓ Build successful" -ForegroundColor Green

# Run tests
Write-Host "Running tests: $TestSuite" -ForegroundColor Yellow

$testArgs = @(
    "test", 
    "Tests/TiXL.Tests.csproj",
    "--configuration", "Release",
    "--no-build",
    "--logger", "trx",
    "--logger", "console;verbosity=minimal"
)

# Add test filter
if ($TestSuite -ne "All") {
    $testArgs += "--filter"
    $testArgs += "Category==$TestSuite"
}

# Add coverage
$testArgs += "--collect"
$testArgs += "\"XPlat Code Coverage\""

# Add output
$testArgs += "--results-directory"
$testArgs += $OutputPath

# Add baseline update if requested
if ($UpdateBaselines) {
    $env:TIXL_UPDATE_BASELINES = "true"
    Write-Host "⚠️  Baseline update mode enabled" -ForegroundColor Magenta
}

Write-Host "Executing: dotnet $($testArgs -join ' ')" -ForegroundColor Gray

$testResult = & dotnet @testArgs 2>&1
$testExitCode = $LASTEXITCODE

# Reset environment
$env:TIXL_UPDATE_BASELINES = $null

if ($testExitCode -eq 0) {
    Write-Host "✓ All tests passed" -ForegroundColor Green
} else {
    Write-Host "❌ Some tests failed (exit code: $testExitCode)" -ForegroundColor Red
    $script:ExitCode = $testExitCode
}

# Process results
Write-Host "Processing test results..." -ForegroundColor Yellow

# Find TRX files
$trxFiles = Get-ChildItem -Path $OutputPath -Filter "*.trx" -Recurse
if ($trxFiles.Count -gt 0) {
    Write-Host "Found $($trxFiles.Count) TRX file(s)"
    
    # Convert TRX to readable format
    foreach ($trxFile in $trxFiles) {
        $basename = $trxFile.BaseName
        $jsonPath = Join-Path $OutputPath "$basename.json"
        
        # Simple TRX to JSON conversion (basic implementation)
        $trxContent = Get-Content $trxFile.FullName -Raw
        $converted = Convert-TRXToJSON -TrxContent $trxContent
        
        $converted | Out-File -FilePath $jsonPath -Encoding UTF8
        Write-Host "Generated: $jsonPath"
    }
}

# Generate performance report if requested
if ($GenerateReport) {
    Write-Host "Generating performance report..." -ForegroundColor Yellow
    
    $reportPath = Join-Path $OutputPath "performance-report.html"
    Generate-PerformanceReport -OutputPath $reportPath -InputPath $OutputPath
    
    Write-Host "Performance report generated: $reportPath"
}

# Upload artifacts for CI
if ($env:CI -eq "true") {
    Write-Host "Preparing CI artifacts..." -ForegroundColor Yellow
    
    # Compress test results
    $zipPath = Join-Path $OutputPath "test-results.zip"
    Compress-Archive -Path "$OutputPath\*" -DestinationPath $zipPath -Force
    
    Write-Host "Artifact: $zipPath"
    
    # Set GitHub output
    if ($env:GITHUB_OUTPUT) {
        "test-results-path=$zipPath" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
        "exit-code=$script:ExitCode" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
    }
}

Write-Host ""
Write-Host "TiXL Graphics Test Runner Complete" -ForegroundColor Cyan
Write-Host "Exit Code: $script:ExitCode" -ForegroundColor $(if ($script:ExitCode -eq 0) { "Green" } else { "Red" })

exit $script:ExitCode

function Convert-TRXToJSON {
    param([string]$TrxContent)
    
    # Basic TRX to JSON conversion
    # In production, consider using a proper XML to JSON converter
    
    $result = @{
        timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        totalTests = 0
        passedTests = 0
        failedTests = 0
        skippedTests = 0
    }
    
    # Parse XML content (simplified)
    try {
        $xml = [xml]$TrxContent
        $outcome = $xml.TestRun.ResultSummary.Counters
        if ($outcome) {
            $result.totalTests = $outcome.total
            $result.passedTests = $outcome.passed
            $result.failedTests = $outcome.failed
            $result.skippedTests = $outcome.skipped
        }
    } catch {
        Write-Warning "Failed to parse TRX XML: $_"
    }
    
    return ($result | ConvertTo-Json -Depth 3)
}

function Generate-PerformanceReport {
    param(
        [string]$OutputPath,
        [string]$InputPath
    )
    
    $html = @"
<!DOCTYPE html>
<html>
<head>
    <title>TiXL Graphics Test Performance Report</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; }
        .header { background: #f0f0f0; padding: 20px; border-radius: 5px; }
        .metric { margin: 10px 0; padding: 10px; background: #f9f9f9; border-left: 4px solid #007acc; }
        .good { border-left-color: #28a745; }
        .warning { border-left-color: #ffc107; }
        .error { border-left-color: #dc3545; }
        table { width: 100%; border-collapse: collapse; margin: 20px 0; }
        th, td { padding: 8px 12px; border: 1px solid #ddd; text-align: left; }
        th { background-color: #f2f2f2; }
    </style>
</head>
<body>
    <div class="header">
        <h1>TiXL Graphics Test Performance Report</h1>
        <p>Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")</p>
    </div>
    
    <h2>Performance Summary</h2>
    <div id="performance-summary">
        <!-- Performance metrics will be inserted here -->
    </div>
    
    <h2>Test Results</h2>
    <table id="test-results">
        <thead>
            <tr>
                <th>Test Category</th>
                <th>Total Tests</th>
                <th>Passed</th>
                <th>Failed</th>
                <th>Duration</th>
            </tr>
        </thead>
        <tbody id="test-results-body">
            <!-- Test results will be inserted here -->
        </tbody>
    </table>
</body>
</html>
"@
    
    $html | Out-File -FilePath $OutputPath -Encoding UTF8
}
```

## Test Implementation Examples

### Example 1: Complete Visual Regression Test

```csharp
[TestFixture]
[Category("Visual")]
public class CompleteVisualRegressionTests
{
    private MockGraphicsContext _context;
    private TestOrchestrator _orchestrator;
    private VisualValidator _validator;
    private PerformanceMonitor _performanceMonitor;

    [SetUp]
    public void Setup()
    {
        _context = new MockGraphicsContext();
        _orchestrator = new TestOrchestrator(_context);
        _validator = new VisualValidator();
        _performanceMonitor = new PerformanceMonitor();
    }

    [Test]
    public void Complex_PBR_Scene_EndToEnd_Visual()
    {
        // This test demonstrates the complete visual regression testing workflow

        // 1. Setup
        _performanceMonitor.StartMeasurement("pbr_scene_test");
        
        var scene = CreateComplexPBScene();
        var renderSettings = new RenderSettings
        {
            Width = 1920,
            Height = 1080,
            SampleCount = 4,
            EnableShadows = true,
            ShadowQuality = ShadowQuality.High,
            EnableReflections = true,
            ReflectionQuality = ReflectionQuality.High
        };

        // 2. Execute rendering with warm-up
        // Warm-up renders to ensure shader compilation doesn't affect timing
        for (int i = 0; i < 5; i++)
        {
            _context.RenderScene(scene);
        }

        _performanceMonitor.ResetMeasurement();

        // 3. Capture test render
        var testImage = _context.CaptureScreenshot(renderSettings.Width, renderSettings.Height);
        
        // 4. Performance measurement
        _performanceMonitor.StopMeasurement("pbr_scene_test");

        // 5. Visual comparison
        var comparisonResult = _validator.CompareWithBaseline(
            testImage,
            "complex_pbr_scene",
            new VisualComparisonSettings
            {
                Tolerance = 0.015f, // 1.5% tolerance for complex scenes
                UseSSIM = true,
                EnableMasking = true,
                MaskTransparentPixels = true,
                CompareAlphaChannel = false // Ignore alpha for better comparison
            });

        // 6. Performance validation
        var perfMetrics = _performanceMonitor.GetLatestMetrics("pbr_scene_test");
        var baselinePerf = GetPerformanceBaseline("complex_pbr_scene");

        // 7. Assertions
        Assert.Multiple(() =>
        {
            // Visual assertions
            Assert.That(comparisonResult.IsMatch, Is.True, 
                $"Visual regression detected. Difference: {comparisonResult.DifferencePercentage:F2}%");
            Assert.That(comparisonResult.DifferencePercentage, Is.LessThan(1.5f),
                "Visual difference should be under 1.5%");

            // Performance assertions
            Assert.That(perfMetrics.AverageFrameTime, Is.LessThan(baselinePerf.AverageFrameTime * 1.1f),
                "Frame time should not regress by more than 10%");
            Assert.That(perfMetrics.P95FrameTime, Is.LessThan(baselinePerf.P95FrameTime * 1.15f),
                "P95 frame time should not regress by more than 15%");
            Assert.That(perfMetrics.MaxFrameTime, Is.LessThan(baselinePerf.MaxFrameTime * 1.2f),
                "Max frame time should not regress by more than 20%");
        });

        // 8. Save artifacts
        SaveTestArtifacts("complex_pbr_scene", testImage, comparisonResult, perfMetrics);
    }

    private TestScene CreateComplexPBScene()
    {
        // Create a complex PBR scene with multiple objects, materials, and lighting
        return new TestScene
        {
            Objects = new[]
            {
                new MockRenderObject
                {
                    Position = new Vector3(0, 0, 0),
                    Scale = Vector3.One,
                    Material = CreateMetalMaterial(),
                    Mesh = MeshFactory.CreateSphere()
                },
                new MockRenderObject
                {
                    Position = new Vector3(2, 0, 0),
                    Scale = new Vector3(1.5f, 1, 1.5f),
                    Material = CreateDielectricMaterial(),
                    Mesh = MeshFactory.CreateBox()
                },
                new MockRenderObject
                {
                    Position = new Vector3(-2, 0, 0),
                    Scale = Vector3.One * 0.8f,
                    Material = CreateRoughMaterial(),
                    Mesh = MeshFactory.CreateTorus()
                }
            },
            Lighting = new SceneLighting
            {
                DirectionalLight = new DirectionalLight
                {
                    Direction = new Vector3(-1, -1, -1),
                    Intensity = 3.0f,
                    Color = new Color(1.0f, 0.95f, 0.9f)
                },
                PointLights = new[]
                {
                    new PointLight
                    {
                        Position = new Vector3(5, 5, 5),
                        Intensity = 100.0f,
                        Color = Color.White
                    }
                },
                AmbientLight = new Color(0.02f, 0.02f, 0.02f)
            },
            Environment = new EnvironmentSettings
            {
                SkyboxTexture = LoadEnvironmentTexture("studio_small_08_2k.hdr"),
                EnableIBL = true,
                IBLIntensity = 1.0f
            }
        };
    }
}
```

### Example 2: Shader Performance Regression Test

```csharp
[TestFixture]
[Category("Shaders")]
public class ShaderPerformanceTests
{
    private ShaderTester _shaderTester;
    private PerformanceMonitor _performanceMonitor;

    [SetUp]
    public void Setup()
    {
        _shaderTester = new ShaderTester(new MockShaderCompiler());
        _performanceMonitor = new PerformanceMonitor();
    }

    [Test]
    public void PBR_Shader_Performance_Regression()
    {
        // Test various PBR shader compilation and execution performance
        
        var shaderTestScenarios = new[]
        {
            new PBRShaderTest
            {
                Name = "standard_pbr",
                VertexShader = GetStandardPBRVertexShader(),
                PixelShader = GetStandardPBRPixelShader(),
                ExpectedCompileTime = 100,
                ExpectedExecuteTime = 0.5 // ms per draw call
            },
            new PBRShaderTest
            {
                Name = "clear_coat_pbr",
                VertexShader = GetClearCoatPBRVertexShader(),
                PixelShader = GetClearCoatPBRPixelShader(),
                ExpectedCompileTime = 150,
                ExpectedExecuteTime = 0.8 // ms per draw call
            },
            new PBRShaderTest
            {
                Name = "subsurface_pbr",
                VertexShader = GetSubsurfacePBRVertexShader(),
                PixelShader = GetSubsurfacePBRPixelShader(),
                ExpectedCompileTime = 120,
                ExpectedExecuteTime = 0.6 // ms per draw call
            }
        };

        foreach (var scenario in shaderTestScenarios)
        {
            // Measure compilation time
            var compileStart = Stopwatch.StartNew();
            var compileResult = _shaderTester.CompileShader(scenario.VertexShader, ShaderType.Vertex);
            compileResult = _shaderTester.CompileShader(scenario.PixelShader, ShaderType.Pixel);
            compileStart.Stop();

            Assert.Multiple(() =>
            {
                Assert.That(compileResult.IsSuccess, Is.True, 
                    $"{scenario.Name} shaders should compile successfully");
                Assert.That(compileStart.ElapsedMilliseconds, Is.LessThan(scenario.ExpectedCompileTime),
                    $"{scenario.Name} compilation should complete under {scenario.ExpectedCompileTime}ms");
            });

            // Measure execution performance
            var executionStart = Stopwatch.StartNew();
            var executionResult = _shaderTester.ExecutePBRShader(scenario);
            executionStart.Stop();

            Assert.Multiple(() =>
            {
                Assert.That(executionResult.IsSuccess, Is.True, 
                    $"{scenario.Name} shader execution should succeed");
                Assert.That(executionStart.Elapsed.TotalMilliseconds, Is.LessThan(scenario.ExpectedExecuteTime),
                    $"{scenario.Name} execution should be under {scenario.ExpectedExecuteTime}ms");
            });

            // Compare against performance baseline
            var baseline = GetPerformanceBaseline($"pbr_shader_{scenario.Name}");
            Assert.Multiple(() =>
            {
                Assert.That(compileStart.ElapsedMilliseconds, Is.LessThan(baseline.CompileTime * 1.1f),
                    $"{scenario.Name} compilation time should not regress");
                Assert.That(executionStart.Elapsed.TotalMilliseconds, Is.LessThan(baseline.ExecuteTime * 1.1f),
                    $"{scenario.Name} execution time should not regress");
            });
        }
    }

    [Test]
    public void Shader_Variant_Compilation_Stress_Test()
    {
        // Test compilation performance with many shader variants
        
        var variants = GenerateShaderVariants(50); // Generate 50 variants
        var compileResults = new List<ShaderCompileResult>();
        var startTime = Stopwatch.StartNew();

        foreach (var variant in variants)
        {
            var result = _shaderTester.CompileShader(variant.Source, variant.Type);
            compileResults.Add(result);
        }

        var totalTime = startTime.Elapsed;

        Assert.Multiple(() =>
        {
            Assert.That(compileResults.All(r => r.IsSuccess), Is.True,
                "All shader variants should compile successfully");
            Assert.That(totalTime.TotalMilliseconds, Is.LessThan(5000),
                "50 shader variants should compile within 5 seconds");
            Assert.That(compileResults.Average(r => r.CompileTime.TotalMilliseconds), 
                Is.LessThan(80), 
                "Average compilation time should be reasonable");
        });

        // Validate no significant performance regressions
        var baselineMetrics = GetPerformanceBaseline("shader_variants_50");
        Assert.That(totalTime.TotalMilliseconds, 
            Is.LessThan(baselineMetrics.TotalTime * 1.2f),
            "Shader variant compilation should not regress by more than 20%");
    }

    private string GetStandardPBRVertexShader()
    {
        return @"
            struct VSInput {
                float3 position : POSITION;
                float3 normal : NORMAL;
                float2 texcoord : TEXCOORD;
                float4 tangent : TANGENT;
            };
            
            struct VSOutput {
                float4 position : SV_POSITION;
                float3 worldPos : WORLD_POSITION;
                float3 normal : NORMAL;
                float2 texcoord : TEXCOORD;
                float4 tangent : TANGENT;
            };
            
            VSOutput VSMain(VSInput input) {
                VSOutput output;
                output.position = mul(float4(input.position, 1.0), worldViewProjection);
                output.worldPos = mul(float4(input.position, 1.0), worldMatrix).xyz;
                output.normal = normalize(mul(input.normal, (float3x3)worldMatrix));
                output.texcoord = input.texcoord;
                output.tangent = input.tangent;
                return output;
            }";
    }

    private string GetStandardPBRPixelShader()
    {
        return @"
            struct PSInput {
                float4 position : SV_POSITION;
                float3 worldPos : WORLD_POSITION;
                float3 normal : NORMAL;
                float2 texcoord : TEXCOORD;
                float4 tangent : TANGENT;
            };
            
            float4 PSMain(PSInput input) : SV_TARGET {
                // Basic PBR shading implementation
                float3 N = normalize(input.normal);
                float3 V = normalize(cameraPosition - input.worldPos);
                
                // Sample textures
                float4 albedo = albedoTexture.Sample(samplerLinear, input.texcoord);
                float4 normalMap = normalTexture.Sample(samplerLinear, input.texcoord);
                float4 materialProperties = materialTexture.Sample(samplerLinear, input.texcoord);
                
                // Unpack normal map
                float3 T = normalize(input.tangent.xyz);
                float3 B = normalize(cross(N, T) * input.tangent.w);
                float3 normalTS = normalize(normalMap.xyz * 2.0 - 1.0);
                N = normalize(normalTS.x * T + normalTS.y * B + normalTS.z * N);
                
                // Calculate lighting
                float3 color = CalculatePBR(N, V, albedo.rgb, materialProperties.r, materialProperties.g, materialProperties.b);
                
                return float4(color, albedo.a);
            }";
    }
}
```

## Conclusion

This comprehensive graphics rendering regression testing system provides TiXL with robust validation capabilities for:

1. **DirectX 12 Pipeline Validation**: Automated testing of pipeline initialization, state management, and resource creation
2. **Visual Regression Testing**: Screenshot-based comparison with configurable tolerances and multiple comparison algorithms
3. **Shader Compilation and Execution**: Validation of shader compilation performance and execution correctness
4. **Resource Management Testing**: Comprehensive testing of graphics resource lifecycle and leak detection
5. **Performance Regression Detection**: Automated monitoring of frame times, memory usage, and GPU utilization
6. **Headless Testing Environment**: Complete testing capability without requiring display hardware
7. **CI/CD Integration**: Seamless integration with automated build and deployment pipelines

### Key Benefits

- **Early Detection**: Visual and performance regressions detected during development
- **Automated Workflows**: Minimal manual intervention required for routine testing
- **Comprehensive Coverage**: Tests cover all critical graphics pipeline components
- **Deterministic Results**: Mock contexts provide consistent, reproducible test results
- **Performance Baselines**: Historical tracking of performance metrics prevents regressions
- **CI/CD Integration**: Seamless integration with existing build pipelines

### Implementation Roadmap

1. **Phase 1**: Basic rendering tests and visual comparison framework
2. **Phase 2**: Shader testing and performance monitoring
3. **Phase 3**: Resource management and leak detection
4. **Phase 4**: CI/CD integration and automated baseline management
5. **Phase 5**: Advanced features like environment-based testing and distributed testing

The system provides a solid foundation for maintaining graphics rendering quality and performance as TiXL continues to evolve and add new features.
