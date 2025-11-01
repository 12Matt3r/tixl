# TiXL Operator Documentation Guide

## Comprehensive Guide for Documenting TiXL Operators

This guide provides specific standards and examples for documenting operators, which are the core building blocks of TiXL's visual programming system.

---

## Operator Documentation Overview

Operators in TiXL represent visual transformations, mathematical operations, and data processing nodes within the node graph system. Each operator has:

- **Input parameters** (connections to other nodes)
- **Control parameters** (user-adjustable values)
- **Output connections** (results passed to other nodes)
- **Context dependencies** (special variables and runtime state)
- **Visual effects** (rendered output or data transformation)

---

## 1. Operator Class Documentation Structure

### Standard Operator Template

```csharp
/// <summary>
/// [One-sentence description of the operator's visual/transformation purpose]
/// </summary>
/// <remarks>
/// [Detailed explanation including:]<br/>
/// - What visual effect or transformation it produces<br/>
/// - How it fits into common workflows<br/>
/// - Performance characteristics and real-time considerations<br/>
/// - Context variable dependencies<br/>
/// - Special usage patterns and tips<br/>
/// </remarks>
/// <example>
/// <code>
/// // Basic operator usage example
/// var transformOp = new TransformOperator();
/// transformOp.SetInput("Source", imageNode.Output);
/// transformOp.SetParameter("Rotation", 45.0f);
/// var output = transformOp.GetOutput();
/// </code>
/// </example>
/// <category>Lib.CategoryName</category> for operator categorization.
/// <see cref="SpecialVariableName"/> for context variable requirements.
/// <see cref="RelatedOperator"/> for similar operators.
/// <version added="1.0">Initial operator implementation</version>
public class OperatorName
{
    // Operator implementation
}
```

---

## 2. Input Parameter Documentation

### Standard Input Parameter Template

```csharp
/// <summary>
/// [Brief description of the input and its purpose]
/// </summary>
/// <param name="inputName">Description of the input parameter including:<br/>
/// - Data type and format<br/>
/// - Accepted connection types<br/>
/// - Required vs. optional status<br/>
/// - Default behavior when not connected<br/>
/// - Validation rules and constraints<br/>
/// </param>
public void SetInput(string inputName, ConnectionType inputValue)
{
    // Implementation
}
```

### Example: Image Input Parameter

```csharp
/// <summary>
/// Set the source image for the filter operation
/// </summary>
/// <param name="sourceImage">Source image to filter. Accepts:<br/>
/// - <see cref="ImageNode"/> outputs<br/>
/// - <see cref="RenderTarget"/> outputs<br/>
/// - Texture data from <see cref="Lib.Texture"/> operators<br/>
/// Required: Always required for operation.<br/>
/// Format: RGBA 8-bit or 16-bit floating point.<br/>
/// Size: Automatically scales to match connected input.
/// </param>
public void SetInput(string inputName, IImageSource sourceImage)
{
    // Implementation
}
```

---

## 3. Control Parameter Documentation

### Standard Control Parameter Template

```csharp
/// <summary>
/// Set or get the value of a control parameter
/// </summary>
/// <param name="parameterName">Control parameter to modify including:<br/>
/// - Parameter type and range<br/>
/// - Default value and units<br/>
/// - Validation rules and constraints<br/>
/// - Real-time update behavior<br/>
/// - UI control mapping (sliders, inputs, etc.)<br/>
/// </param>
/// <param name="value">Parameter value to set including:<br/>
/// - Type conversion behavior<br/>
/// - Clamping and validation<br/>
/// - Performance impact of changes<br/>
/// </param>
public void SetParameter(string parameterName, ParameterType value)
{
    // Implementation
}
```

### Example: Transform Parameter

```csharp
/// <summary>
/// Set the transformation parameters for the operator
/// </summary>
/// <param name="parameterName">Transformation parameter:<br/>
/// - "Translation": <see cref="Vector2"/> - Position offset in pixels<br/>
/// - "Rotation": <see cref="float"/> - Rotation angle in degrees (-360 to 360)<br/>
/// - "Scale": <see cref="Vector2"/> - Scale factors (0.1 to 10.0)<br/>
/// - "Pivot": <see cref="Vector2"/> - Pivot point for rotation/scaling<br/>
/// Default values are set per parameter type.
/// </param>
/// <param name="value">Parameter value with automatic conversion:<br/>
/// - Vector parameters accept Vector2, Point, or Size<br/>
/// - Float parameters accept numeric types with unit conversion<br/>
/// - Values outside valid range are automatically clamped<br/>
/// - Changes update in real-time during rendering<br/>
/// </param>
public void SetParameter(string parameterName, object value)
{
    // Implementation
}
```

---

## 4. Output Documentation

### Standard Output Template

```csharp
/// <summary>
/// Get the operator's output connection
/// </summary>
/// <param name="outputName">Output name to retrieve:<br/>
/// - Description of output type and format<br/>
/// - Connection compatibility with other operators<br/>
/// - Real-time update behavior<br/>
/// - Performance characteristics<br/>
/// </param>
/// <returns>[Return type] representing the operator output including:<br/>
/// - Data type and format specifications<br/>
/// - Connection points for chaining<br/>
/// - Lifecycle and memory management<br/>
/// </returns>
public IOutputConnection GetOutput(string outputName = "Output")
{
    // Implementation
}
```

### Example: Filter Output

```csharp
/// <summary>
/// Get the filtered image output
/// </summary>
/// <param name="outputName">Output specification:<br/>
/// - "Output": Filtered image in RGBA format<br/>
/// - "Preview": Lower resolution preview for UI feedback<br/>
/// - "Debug": Intermediate processing data (development only)<br/>
/// </param>
/// <returns><see cref="IImageOutput"/> containing:<br/>
/// - Processed RGBA image data<br/>
/// - Resolution matching source input<br/>
/// - Format: 8-bit or 16-bit based on source<br/>
/// - Alpha channel preservation from source<br/>
/// - Real-time updates when inputs change<br/>
/// </returns>
public IImageOutput GetOutput(string outputName = "Output")
{
    // Implementation
}
```

---

## 5. Context Variable Documentation

### Context Variable Dependencies Template

```csharp
/// <summary>
/// Operator context variable requirements
/// </summary>
/// <context variables="[Special variables used by this operator]">
/// <context variables>
/// <remarks>
/// [Context variable details including:]<br/>
/// - Which special variables are required/optional<br/>
/// - How context variables affect operator behavior<br/>
/// - Default values when context variables are unavailable<br/>
/// - Performance implications of context variable usage<br/>
/// </remarks>
```

### Example: Transform Context Dependencies

```csharp
/// <summary>
/// Apply transformation based on context variables
/// </summary>
/// <context variables="Used in operator execution:">
/// - <see cref="GlobalTime"/> - Current time for time-based animations<br/>
/// - <see cref="Resolution"/> - Target resolution for scaling calculations<br/>
/// - <see cref="AspectRatio"/> - Aspect ratio for proper transformations<br/>
/// - <see cref="CameraPosition"/> - World position for 3D transformations<br/>
/// </context variables>
/// <remarks>
/// The operator automatically adapts to context variables when available:<br/>
/// - Without GlobalTime: Uses static transformation values<br/>
/// - With GlobalTime: Enables time-based animation and effects<br/>
/// - Resolution affects scaling calculations and output quality<br/>
/// - CameraPosition required for 3D spatial transformations<br/>
/// Context variables are cached and updated per frame for performance.<br/>
/// </remarks>
public class TransformOperator
{
    // Implementation
}
```

---

## 6. Visual Effect Documentation

### Visual Output Description Template

```csharp
/// <summary>
/// Description of the operator's visual output
/// </summary>
/// <visual output="[Visual effect details]">
/// - Description of visual transformation<br/>
/// - Quality and resolution considerations<br/>
/// - Performance impact on rendering<br/>
/// - Special effects and post-processing<br/>
/// - Compatibility with different render targets<br/>
/// </visual output>
```

### Example: Blur Operator Visual Effect

```csharp
/// <summary>
/// Apply Gaussian blur effect to input image
/// </summary>
/// <visual output="Visual effect specifications:">
/// - Blur radius: 1-50 pixels with customizable kernel size<br/>
/// - Quality levels: Fast (5-tap), Standard (9-tap), High (17-tap)<br/>
/// - Edge handling: Clamp, Wrap, Mirror, or Edge detection<br/>
/// - Alpha handling: Blur alpha channel separately or together<br/>
/// - Performance: ~0.5ms per megapixel at Standard quality<br/>
/// - Memory usage: ~2x input image size for intermediate processing<br/>
/// </visual output>
public class BlurOperator
{
    // Implementation
}
```

---

## 7. Performance Documentation

### Performance Characteristics Template

```csharp
/// <summary>
/// Performance and real-time considerations
/// </summary>
/// <performance considerations="[Performance impact analysis]">
/// - CPU usage: [CPU time per operation]<br/>
/// - Memory usage: [Memory allocation patterns]<br/>
/// - GPU utilization: [Graphics processing requirements]<br/>
/// - Real-time capability: [Frames per second impact]<br/>
/// - Scaling behavior: [Performance vs. input size]<br/>
/// - Optimization tips: [Best practices for performance]<br/>
/// </performance considerations>
```

### Example: Complex Filter Performance

```csharp
/// <summary>
/// Advanced image processing operator with multiple stages
/// </summary>
/// <performance considerations="Performance impact analysis:">
/// - CPU usage: 2-5ms per megapixel depending on quality settings<br/>
/// - Memory usage: 3x input image size (multiple processing stages)<br/>
/// - GPU utilization: Moderate - can be GPU-accelerated with appropriate backend<br/>
/// - Real-time capability: 60 FPS at 1080p with quality level 2<br/>
/// - Scaling: Linear performance increase with input resolution<br/>
/// - Optimization: Use preview output (50% resolution) for UI feedback<br/>
/// - Memory pooling: Built-in pooling reduces garbage collection pressure<br/>
/// </performance considerations>
```

---

## 8. Integration Documentation

### Module Integration Template

```csharp
/// <summary>
/// Integration with TiXL modules
/// </summary>
/// <integration points="[Module integration details]">
/// - <see cref="TiXL.Core.Graphics"/> - Rendering and graphics context<br/>
/// - <see cref="TiXL.Operators.Lib"/> - Operator library integration<br/>
/// - <see cref="TiXL.Editor.UI"/> - User interface and parameter editing<br/>
/// - <see cref="TiXL.Core.Logging"/> - Logging and debugging support<br/>
/// </integration points>
```

---

## 9. Category and Classification Documentation

### Operator Categorization Template

```csharp
/// <summary>
/// Operator category and classification
/// </summary>
/// <category>[Lib.Category]</category> for organization within the operator library.
/// <subcategory>[SubCategory]</subcategory> for fine-grained organization.
/// <tags>[tag1, tag2, tag3]</tags> for search and filtering.
/// <complexity>[Beginner/Intermediate/Advanced]</complexity> indicating required expertise.
/// <use cases="[Common usage scenarios]">
/// - Primary use case 1<br/>
/// - Primary use case 2<br/>
/// - Advanced use case<br/>
/// </use cases>
```

### Example: Complete Operator Documentation

```csharp
/// <summary>
/// Apply perspective transformation to 2D images
/// </summary>
/// <remarks>
/// The PerspectiveTransform operator warps images using 4-point perspective mapping.
/// Common uses include:<br/>
/// - Correcting camera perspective distortions<br/>
/// - Creating 3D-like depth effects<br/>
/// - Texture mapping onto 3D surfaces<br/>
/// - Dynamic perspective animations<br/>
/// 
/// Performance is optimized for real-time usage with quality scaling options.<br/>
/// Supports GPU acceleration when available through DirectCompute.<br/>
/// </remarks>
/// <example>
/// <code>
/// // Create perspective transform
/// var perspectiveOp = new PerspectiveTransformOperator();
/// 
/// // Set source image
/// perspectiveOp.SetInput("Source", cameraImage.Output);
/// 
/// // Define 4 corner points for perspective mapping
/// var corners = new[]
/// {
///     new Vector2(100, 50),   // Top-left
///     new Vector2(600, 80),   // Top-right  
///     new Vector2(550, 400),  // Bottom-right
///     new Vector2(150, 420)   // Bottom-left
/// };
/// 
/// // Set transformation parameters
/// perspectiveOp.SetParameter("CornerPoints", corners);
/// perspectiveOp.SetParameter("Quality", QualityLevel.Standard);
/// perspectiveOp.SetParameter("AntiAliasing", true);
/// 
/// // Get transformed output
/// var transformedOutput = perspectiveOp.GetOutput("Output");
/// </code>
/// </example>
/// <category>Lib.Image.Transform</category> for image transformation operators.
/// <subcategory>Geometry</subcategory> for geometric transformations.
/// <tags>perspective, transform, warp, geometry, 2d</tags>
/// <complexity>Intermediate</complexity>
/// <use cases="Common usage scenarios:">
/// - Correcting skewed or perspective-distorted images<br/>
/// - Creating dynamic 3D depth effects in 2D workflows<br/>
/// - Texturing 3D objects with 2D textures<br/>
/// - Creating animated perspective effects<br/>
/// </use cases>
/// <context variables="Context dependencies:">
/// - <see cref="GlobalTime"/> for animated transformations<br/>
/// - <see cref="Resolution"/> for quality scaling<br/>
/// - <see cref="CameraMatrix"/> for 3D integration<br/>
/// </context variables>
/// <visual output="Transformation specifications:">
/// - Preserves image quality with configurable sampling<br/>
/// - Supports alpha channel with proper edge handling<br/>
/// - Quality levels: Fast (bilinear), Standard (bicubic), High (lanczos)<br/>
/// - Output size matches source image bounds<br/>
/// - Performance: ~1-3ms per megapixel at Standard quality<br/>
/// </visual output>
/// <performance considerations="Performance characteristics:">
/// - CPU usage: 1-3ms per megapixel depending on quality<br/>
/// - Memory usage: ~2x input size for transformation buffer<br/>
/// - GPU acceleration: Available through DirectCompute backend<br/>
/// - Real-time: 60 FPS achievable at 1080p with Quality Level 2<br/>
/// - Optimization tips: Use lower quality for real-time preview<br/>
/// </performance considerations>
/// <integration points="Module integration:">
/// - <see cref="TiXL.Core.Graphics"/> - Rendering context and texture support<br/>
/// - <see cref="TiXL.Operators.Lib.Image"/> - Image processing pipeline<br/>
/// - <see cref="TiXL.Editor.UI"/> - Parameter editing and preview<br/>
/// - <see cref="TiXL.Core.Logging"/> - Debug logging and performance monitoring<br/>
/// </integration points>
/// <version added="1.0">Initial implementation</version>
public class PerspectiveTransformOperator
{
    // Implementation
}
```

---

## 10. Cross-Reference Guidelines

### Related Operator Documentation

```csharp
/// <see cref="RelatedOperator1"/> for [brief description of relationship].
/// <see cref="RelatedOperator2"/> for [brief description of relationship].
/// <example>
/// <code>
/// // Combining with related operators
/// var edgeOp = new EdgeDetectionOperator();
/// var blurOp = new BlurOperator();
/// var finalOp = new PerspectiveTransformOperator();
/// 
/// edgeOp.SetInput("Source", source.Output);
/// blurOp.SetInput("Source", edgeOp.GetOutput());
/// finalOp.SetInput("Source", blurOp.GetOutput());
/// </code>
/// </example>
```

---

## 11. Quality Checklist for Operator Documentation

### Pre-Release Checklist

- [ ] **Class documentation** complete with purpose and usage context
- [ ] **Input parameters** documented with types and validation rules
- [ ] **Control parameters** documented with ranges and defaults
- [ ] **Output specifications** complete with format and compatibility
- [ ] **Context variables** identified and documented
- [ ] **Visual output** described with quality and performance details
- [ ] **Performance characteristics** documented with metrics
- [ ] **Integration points** documented across modules
- [ ] **Category and classification** properly assigned
- [ ] **Cross-references** to related operators and APIs included
- [ ] **Code examples** compile and demonstrate common usage
- [ ] **Special variables** documentation links working
- [ ] **Version information** included for tracking changes

### Documentation Quality Standards

- **Completeness**: All public APIs documented
- **Accuracy**: Examples compile and match implementation
- **Clarity**: Technical explanations accessible to target audience
- **Consistency**: Terminology and structure consistent across operators
- **Actionability**: Examples and guidance enable immediate usage

---

**Document Version**: 1.0  
**Last Updated**: 2025-11-02  
**Scope**: All TiXL operators in Core, Operators, and Editor modules