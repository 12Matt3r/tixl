# TiXL Migration Guide

## v1.x → v2.0 Migration Guide

This guide provides detailed instructions for migrating your TiXL applications from v1.x to v2.0. This major version includes breaking changes that require code modifications.

### Table of Contents
- [Overview](#overview)
- [Breaking Changes](#breaking-changes)
- [Migration Steps](#migration-steps)
- [Code Examples](#code-examples)
- [Testing Your Migration](#testing-your-migration)
- [Troubleshooting](#troubleshooting)
- [Additional Resources](#additional-resources)

---

## Overview

### What's New in v2.0
- **Async/Await Patterns**: Complete migration to async/await patterns
- **Performance**: 40% improvement in real-time rendering performance
- **API Modernization**: Updated API surface with better type safety
- **Security**: Enhanced security model with built-in validation
- **Documentation**: Improved API documentation and examples

### Migration Benefits
- Better performance and memory management
- Improved error handling and debugging
- Enhanced type safety and compile-time checks
- Future-proof architecture for upcoming features
- Better integration with modern .NET features

### Migration Timeline
- **Current Version**: v1.x (final release v1.8.0)
- **New Version**: v2.0.0
- **Support Period**: v1.x will receive security updates for 12 months
- **Migration Deadline**: Recommended by v2.1.0 release (6 months)

---

## Breaking Changes

### 1. Async/Await Required

**Change**: All I/O operations now require async/await patterns

**Old API (v1.x)**:
```csharp
var renderer = new GraphicsRenderer();
renderer.Initialize(device, width, height); // Synchronous
renderer.Render(scene); // Synchronous
```

**New API (v2.0)**:
```csharp
var renderer = new GraphicsRenderer();
await renderer.InitializeAsync(device, width, height); // Async
await renderer.RenderAsync(scene); // Async
```

**Migration Steps**:
1. Add `async`/`await` keywords to methods using TiXL
2. Update return types from `void` to `Task` or `Task<T>`
3. Handle potential exceptions with `try/catch`
4. Update calling code to use `await`

### 2. Initialization Sequence Changes

**Change**: Renderer initialization now requires explicit async initialization

**Old API (v1.x)**:
```csharp
var renderer = new GraphicsRenderer();
renderer.Initialize(device, width, height);
renderer.Start(); // Implicit start
```

**New API (v2.0)**:
```csharp
var renderer = new GraphicsRenderer();
await renderer.InitializeAsync(device, width, height);
await renderer.StartAsync(); // Explicit async start
```

### 3. Event Handling Changes

**Change**: Events now use event-based asynchronous pattern (EAP)

**Old API (v1.x)**:
```csharp
renderer.FrameRendered += (sender, e) => {
    // Handle frame
};
```

**New API (v2.0)**:
```csharp
renderer.FrameRendered += async (sender, e) => {
    await HandleFrameAsync(e.FrameData);
};
```

### 4. Exception Handling

**Change**: More specific exception types and enhanced error information

**Old API (v1.x)**:
```csharp
try {
    renderer.Render(scene);
} catch (Exception ex) {
    // Generic exception handling
}
```

**New API (v2.0)**:
```csharp
try {
    await renderer.RenderAsync(scene);
} catch (GraphicsDeviceLostException ex) {
    // Specific exception handling
    await HandleDeviceLostAsync(ex);
} catch (InsufficientMemoryException ex) {
    // Handle memory issues
    await CleanupResourcesAsync();
} catch (ArgumentException ex) {
    // Handle invalid arguments
    LogInvalidArgument(ex);
}
```

### 5. Configuration API Changes

**Change**: Configuration moved to typed configuration objects

**Old API (v1.x)**:
```csharp
renderer.Configuration["EnableVSync"] = "true";
renderer.Configuration["MaxFrameRate"] = "60";
```

**New API (v2.0)**:
```csharp
renderer.Configuration.EnableVSync = true;
renderer.Configuration.MaxFrameRate = 60;
```

### 6. Resource Management

**Change**: Enhanced disposal pattern with async disposal support

**Old API (v1.x)**:
```csharp
var texture = new Texture2D(device, width, height);
texture.Dispose(); // Synchronous disposal
```

**New API (v2.0)**:
```csharp
var texture = new Texture2D(device, width, height);
await texture.DisposeAsync(); // Async disposal
```

---

## Migration Steps

### Step 1: Environment Setup

1. **Update .NET SDK**
   ```bash
   # Check current version
   dotnet --version
   
   # Install .NET 9.0+ if not present
   # Download from https://dotnet.microsoft.com/download
   ```

2. **Update TiXL Package References**
   ```xml
   <!-- In your .csproj file -->
   <PackageReference Include="TiXL.Core" Version="2.0.0" />
   <PackageReference Include="TiXL.Operators" Version="2.0.0" />
   <PackageReference Include="TiXL.Editor" Version="2.0.0" />
   ```

3. **Update Project File**
   ```xml
   <PropertyGroup>
     <TargetFramework>net9.0</TargetFramework>
     <LangVersion>latest</LangVersion>
     <Nullable>enable</Nullable>
   </PropertyGroup>
   ```

### Step 2: Code Migration

#### Update using statements
```csharp
// Add new using statements
using TiXL.Core;
using TiXL.Core.Rendering;
using TiXL.Operators;
using TiXL.Utilities;
```

#### Convert synchronous methods to async
```csharp
// Old method signature
public void InitializeRenderer(GraphicsDevice device)
{
    var renderer = new GraphicsRenderer();
    renderer.Initialize(device, 1920, 1080);
    renderer.Start();
}

// New method signature
public async Task InitializeRendererAsync(GraphicsDevice device)
{
    var renderer = new GraphicsRenderer();
    await renderer.InitializeAsync(device, 1920, 1080);
    await renderer.StartAsync();
}
```

#### Update event handlers
```csharp
// Old event handler
renderer.FrameRendered += (sender, e) =>
{
    ProcessFrame(e.FrameData);
};

// New async event handler
renderer.FrameRendered += async (sender, e) =>
{
    await ProcessFrameAsync(e.FrameData);
};
```

### Step 3: Configuration Migration

#### Old configuration approach
```csharp
// v1.x configuration
renderer.Configuration["EnableVSync"] = "true";
renderer.Configuration["MaxFrameRate"] = "60";
renderer.Configuration["MemoryBudget"] = "1024";
```

#### New configuration approach
```csharp
// v2.0 configuration
renderer.Configuration.EnableVSync = true;
renderer.Configuration.MaxFrameRate = 60;
renderer.Configuration.MemoryBudget = 1024;
```

### Step 4: Error Handling Migration

#### Enhanced error handling
```csharp
// v2.0 error handling
try
{
    await renderer.RenderAsync(scene);
}
catch (GraphicsDeviceLostException ex)
{
    await LogErrorAsync("Graphics device lost", ex);
    await RecreateDeviceAsync();
}
catch (InsufficientMemoryException ex)
{
    await LogErrorAsync("Insufficient memory", ex);
    await FreeUnusedResourcesAsync();
}
catch (OperationCanceledException ex)
{
    // Operation was canceled gracefully
    LogInformation("Render operation canceled");
}
```

---

## Code Examples

### Complete Application Migration

#### v1.x Application
```csharp
public class GraphicsApp
{
    private GraphicsRenderer _renderer;
    
    public void Initialize(GraphicsDevice device)
    {
        _renderer = new GraphicsRenderer();
        _renderer.Initialize(device, 1920, 1080);
        _renderer.FrameRendered += OnFrameRendered;
        _renderer.Start();
    }
    
    private void OnFrameRendered(object sender, FrameEventArgs e)
    {
        ProcessFrame(e.FrameData);
    }
    
    public void Render(Scene scene)
    {
        _renderer.Render(scene);
    }
    
    public void Cleanup()
    {
        _renderer?.Dispose();
    }
}
```

#### v2.0 Application
```csharp
public class GraphicsApp
{
    private GraphicsRenderer _renderer;
    
    public async Task InitializeAsync(GraphicsDevice device)
    {
        _renderer = new GraphicsRenderer();
        await _renderer.InitializeAsync(device, 1920, 1080);
        _renderer.FrameRendered += OnFrameRenderedAsync;
        await _renderer.StartAsync();
    }
    
    private async void OnFrameRenderedAsync(object sender, FrameEventArgs e)
    {
        await ProcessFrameAsync(e.FrameData);
    }
    
    public async Task RenderAsync(Scene scene)
    {
        await _renderer.RenderAsync(scene);
    }
    
    public async Task CleanupAsync()
    {
        if (_renderer != null)
        {
            await _renderer.DisposeAsync();
        }
    }
}
```

### Operator Migration

#### v1.x Operator
```csharp
public class CustomOperator : IOperator
{
    public void Execute(ExecutionContext context)
    {
        var input = context.GetInput("Input");
        var output = ProcessData(input);
        context.SetOutput("Output", output);
    }
    
    public void Dispose()
    {
        // Cleanup resources
    }
}
```

#### v2.0 Operator
```csharp
public class CustomOperator : IOperator
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    
    public async Task ExecuteAsync(ExecutionContext context)
    {
        await _semaphore.WaitAsync();
        try
        {
            var input = await context.GetInputAsync("Input");
            var output = await ProcessDataAsync(input);
            await context.SetOutputAsync("Output", output);
        }
        finally
        {
            _semaphore.Release();
        }
    }
    
    public async ValueTask DisposeAsync()
    {
        await _semaphore.DisposeAsync();
    }
}
```

---

## Testing Your Migration

### 1. Unit Test Updates

#### Update test methods to async
```csharp
// v1.x test
[Test]
public void Render_Scene_RendersCorrectly()
{
    var renderer = new GraphicsRenderer();
    renderer.Initialize(TestDevice, 800, 600);
    var scene = CreateTestScene();
    
    renderer.Render(scene);
    
    Assert.That(renderer.FrameCount, Is.EqualTo(1));
}

// v2.0 test
[Test]
public async Task RenderAsync_Scene_RendersCorrectly()
{
    var renderer = new GraphicsRenderer();
    await renderer.InitializeAsync(TestDevice, 800, 600);
    var scene = CreateTestScene();
    
    await renderer.RenderAsync(scene);
    
    Assert.That(renderer.FrameCount, Is.EqualTo(1));
}
```

### 2. Integration Test Updates

```csharp
[Test]
public async Task EndToEnd_RenderPipeline_WorksCorrectly()
{
    // Arrange
    var app = new GraphicsApp();
    var device = CreateTestDevice();
    
    // Act
    await app.InitializeAsync(device);
    await app.RenderAsync(CreateTestScene());
    
    // Assert
    Assert.That(app.IsInitialized, Is.True);
    Assert.That(app.FrameCount, Is.EqualTo(1));
    
    // Cleanup
    await app.CleanupAsync();
}
```

### 3. Performance Testing

```csharp
[Benchmark]
public async Task RenderPerformance_MigratedCode_PerformsWell()
{
    var renderer = new GraphicsRenderer();
    await renderer.InitializeAsync(TestDevice, 1920, 1080);
    var scene = CreateComplexScene();
    
    var sw = Stopwatch.StartNew();
    await renderer.RenderAsync(scene);
    sw.Stop();
    
    Assert.That(sw.ElapsedMilliseconds, Is.LessThan(16)); // 60 FPS target
}
```

---

## Troubleshooting

### Common Issues and Solutions

#### Issue 1: 'GraphicsRenderer' does not contain a definition for 'Initialize'

**Problem**: Using old synchronous API  
**Solution**: 
```csharp
// Wrong
renderer.Initialize(device, width, height);

// Correct
await renderer.InitializeAsync(device, width, height);
```

#### Issue 2: Cannot await 'void' method

**Problem**: Trying to await synchronous method  
**Solution**: Ensure all TiXL methods use async/await pattern

#### Issue 3: Event handler does not await async operations

**Problem**: Event handlers not updated for async  
**Solution**:
```csharp
// Wrong
renderer.FrameRendered += (sender, e) => {
    await ProcessFrameAsync(e.Data);
};

// Correct
renderer.FrameRendered += async (sender, e) => {
    await ProcessFrameAsync(e.Data);
};
```

#### Issue 4: Configuration property not found

**Problem**: Using old string-based configuration  
**Solution**:
```csharp
// Wrong
renderer.Configuration["EnableVSync"] = "true";

// Correct
renderer.Configuration.EnableVSync = true;
```

#### Issue 5: Resource disposal hanging

**Problem**: Using synchronous disposal  
**Solution**:
```csharp
// Wrong
renderer.Dispose();

// Correct
await renderer.DisposeAsync();
```

### Performance Issues

#### Issue: Slower performance after migration

**Possible Causes**:
1. Not using `ConfigureAwait(false)` appropriately
2. Blocking on async operations
3. Excessive async/await overhead

**Solutions**:
```csharp
// Use ConfigureAwait(false) in non-UI code
await renderer.RenderAsync(scene).ConfigureAwait(false);

// Avoid blocking on async operations
// Wrong:
renderer.RenderAsync(scene).Wait();

// Correct:
await renderer.RenderAsync(scene);
```

### Memory Issues

#### Issue: Higher memory usage

**Possible Causes**:
1. Not properly disposing async resources
2. Event handlers not removed
3. Unawaited async operations

**Solutions**:
```csharp
// Properly dispose resources
public async ValueTask DisposeAsync()
{
    await _renderer?.DisposeAsync();
    _eventHandler = null;
}

// Remove event handlers
renderer.FrameRendered -= _eventHandler;
```

---

## Compatibility Aids

### Adapter Library

For applications that need immediate compatibility, use the v1.x adapter:

```csharp
// Install TiXL.Compatibility.V1 package
<PackageReference Include="TiXL.Compatibility.V1" Version="2.0.0" />

// Use adapter for gradual migration
using TiXL.Compatibility.V1;

var adapter = new GraphicsRendererAdapter(new GraphicsRenderer());
adapter.Initialize(device, width, height); // Synchronous wrapper
adapter.Render(scene); // Synchronous wrapper
```

### Dependency Analysis Tool

Use the migration assistant to analyze your codebase:

```bash
# Install migration assistant
dotnet tool install -g TiXL.MigrationAssistant

# Analyze your project
tixl-migration analyze --project-path ./MyProject.csproj --output report.json

# Generate migration suggestions
tixl-migration suggest --report-path report.json
```

---

## Additional Resources

### Documentation
- [TiXL v2.0 API Reference](https://docs.tixl.io/v2.0/api)
- [Async/Await Best Practices](https://docs.tixl.io/v2.0/async-patterns)
- [Performance Guidelines](https://docs.tixl.io/v2.0/performance)

### Tools
- [Migration Assistant Tool](https://github.com/tixl/migration-assistant)
- [Compatibility Adapter Library](https://github.com/tixl/compatibility-adapter)
- [Code Analysis Rules](https://github.com/tixl/roslyn-analyzers)

### Community Support
- [Migration Discord Channel](https://discord.gg/tixl-migration)
- [Migration Issues](https://github.com/tixl/tixl/issues?q=is%3Aissue+migration)
- [Stack Overflow](https://stackoverflow.com/questions/tagged/tixl+migration)

### Training Materials
- [Video Tutorial Series](https://youtube.com/playlist?list=migration-v2)
- [Code Examples Repository](https://github.com/tixl/migration-examples)
- [Live Coding Sessions](https://twitch.tv/tixlcommunity)

---

## Support and Feedback

### Getting Help
- **Documentation Issues**: [GitHub Documentation](https://github.com/tixl/docs/issues)
- **Migration Problems**: [GitHub Migration Support](https://github.com/tixl/tixl/discussions/migration)
- **Community Discord**: [#migration-help](https://discord.gg/tixl-migration)
- **Email Support**: migration@tixl.io

### Reporting Issues
When reporting migration issues, please include:
1. Original v1.x code snippet
2. Error message
3. Stack trace (if applicable)
4. Project configuration
5. Environment details (.NET version, OS, etc.)

---

*This migration guide is a living document. Please check our [documentation repository](https://github.com/tixl/docs) for the latest updates and additional resources.*

**Last Updated**: November 2, 2024  
**Version**: 1.0  
**Next Review**: February 2025