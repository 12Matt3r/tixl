# TiXL Testing Suite Documentation

## Overview

This document provides comprehensive guidance for the TiXL baseline testing suite (TIXL-041). The testing framework is built on **xUnit** and provides robust unit, integration, and performance testing for all TiXL core functionality.

## Project Structure

```
Tests/
├── TiXL.Tests.csproj              # Main test project configuration
├── xunit.runner.json              # xUnit configuration
├── TestSettings.runsettings       # Test run configuration
├── CoreTests.cs                   # Core functionality tests
├── TestCategories.cs              # Test categorization system
├── Data/
│   └── TestDataGenerator.cs       # Standardized test data generation
├── Fixtures/
│   ├── CoreTestFixture.cs         # Base test fixture with DI
│   └── EnhancedLoggingFixtures.cs # Logging test configurations
├── Core/
│   └── SampleVectorTests.cs       # Vector/mathematical tests
├── IO/
│   └── SafeFileIOTests.cs         # File I/O safety tests
├── Operators/
│   └── EvaluationContextTests.cs  # Operator system tests
├── Graphics/
│   └── HeadlessRenderingTests.cs  # Graphics/rendering tests
├── Integration/
│   └── CoreModuleIntegrationTests.cs # End-to-end tests
├── Examples/
│   ├── TiXLLoggingIntegrationTests.cs
│   └── EnhancedLoggingExamples.cs
├── Mocks/
│   └── Graphics/
│       └── MockD3D12Device.cs     # Mock graphics devices
└── Utilities/
    └── TestDebuggingUtilities.cs  # Test utility functions
```

## Test Categories

Tests are organized using the `TestCategories` class with the following categories:

### Core Categories
- `Unit` - Fast, isolated unit tests
- `Integration` - Tests that verify module interactions
- `Performance` - Performance and timing tests
- `Smoke` - Quick validation tests

### Functional Categories
- `Core` - Core TiXL functionality
- `Operators` - Operator system tests
- `Graphics` - Graphics/rendering pipeline tests
- `IO` - File I/O and serialization tests
- `Audio` - Audio processing tests
- `UI` - User interface tests

### Priority Categories
- `P0` - Critical functionality (must pass)
- `P1` - High importance
- `P2` - Medium importance
- `P3` - Low importance

### Performance Categories
- `Fast` - < 100ms per test
- `Medium` - 100ms - 1s per test
- `Slow` - > 1s per test

## Test Execution

### Command Line

#### Run All Tests
```bash
# Run all tests with coverage
dotnet test Tests/TiXL.Tests.csproj --collect:"XPlat Code Coverage" --settings Tests/TestSettings.runsettings

# Run tests with detailed output
dotnet test Tests/TiXL.Tests.csproj --logger "console;verbosity=detailed"
```

#### Run Specific Categories
```bash
# Run only unit tests
dotnet test Tests/TiXL.Tests.csproj --filter "Category=Unit"

# Run unit and integration tests
dotnet test Tests/TiXL.Tests.csproj --filter "Category=Unit|Category=Integration"

# Run P0 tests only
dotnet test Tests/TiXL.Tests.csproj --filter "Category=P0"

# Run fast tests only
dotnet test Tests/TiXL.Tests.csproj --filter "Category=Fast"
```

#### Run Specific Test Classes
```bash
# Run specific test class
dotnet test Tests/TiXL.Tests.csproj --filter "ClassName=EvaluationContextTests"

# Run specific test method
dotnet test Tests/TiXL.Tests.csproj --filter "MethodName=Vector2_CreateWithComponents_ReturnsCorrectValues"
```

#### Performance Testing
```bash
# Run performance tests
dotnet test Tests/TiXL.Tests.csproj --filter "Category=Performance"

# Run with extended timeout
dotnet test Tests/TiXL.Tests.csproj --filter "Category=Performance" --timeout 300000
```

### IDE Integration

#### Visual Studio
1. Open the Test Explorer (Test → Windows → Test Explorer)
2. Build the solution (Ctrl+Shift+B)
3. Tests will appear in the Test Explorer
4. Right-click on test categories to filter
5. Run tests individually or in groups

#### Visual Studio Code
1. Install the .NET Test Explorer extension
2. Open the Command Palette (Ctrl+Shift+P)
3. Run "Test: Discover Tests"
4. Use the Test Explorer view to run tests

## Test Data Management

### Test Data Generator

The `TestDataGenerator` class provides standardized test data:

```csharp
// Generate test vectors
var vectors2D = TestDataGenerator.GenerateVector2DArray(100);
var vectors3D = TestDataGenerator.GenerateVector3DArray(50);

// Generate test projects
var projectData = TestDataGenerator.GenerateTestProjectData();

// Generate audio samples
var audioSamples = TestDataGenerator.GenerateAudioSamples(44100, 44100, 440f);

// Generate valid/invalid file paths
var validPaths = TestDataGenerator.GenerateValidFilePaths(20);
var invalidPaths = TestDataGenerator.GenerateInvalidFilePaths(10);
```

### Custom Test Data

For specialized test scenarios, create test data in the `TestData` folder:

```csharp
// Example custom test data
public static class CustomTestData
{
    public static readonly Vector2D[] StandardVectors = {
        new Vector2D(0, 0),      // Zero vector
        new Vector2D(1, 0),      // Unit X
        new Vector2D(0, 1),      // Unit Y
        new Vector2D(1, 1),      // 45-degree
        new Vector2D(3, 4),      // 3-4-5 triangle
    };
}
```

## Test Fixtures and Setup

### Base Test Fixture

All tests inherit from `CoreTestFixture` which provides:

- Dependency injection container
- Logging configuration
- Test cleanup services
- Async lifecycle management

```csharp
public class MyTests : CoreTestFixture
{
    public MyTests(ITestOutputHelper output) : base()
    {
        // Tests can access ServiceProvider for dependencies
        _logger = ServiceProvider.GetRequiredService<ILogger<MyTests>>();
    }
}
```

### Custom Fixtures

For specialized test scenarios, create custom fixtures:

```csharp
public class GraphicsTestFixture : CoreTestFixture
{
    protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        base.ConfigureServices(context, services);
        services.AddSingleton<IGraphicsDevice, MockGraphicsDevice>();
    }
}
```

## Mock Objects

### Graphics Mocks

For graphics testing without actual GPU dependencies:

```csharp
// Use mock devices for headless testing
using var device = new MockD3D12Device();
using var resource = device.CreateCommittedResource(
    HeapType.Default,
    ResourceStates.RenderTarget,
    description);
```

### Custom Mocks

Create mocks for interfaces that need testing:

```csharp
public class MockOperator : IOperator
{
    public EvaluationResult Evaluate(EvaluationContext context)
    {
        // Mock implementation for testing
        return EvaluationResult.Success;
    }
}
```

## Performance Testing

### Performance Benchmarks

Use the `BenchmarkDotNet` for performance measurements:

```csharp
public class PerformanceBenchmarks
{
    [Benchmark]
    public void VectorOperations_Benchmark()
    {
        var vectors = TestDataGenerator.GenerateVector2DArray(1000);
        foreach (var vector in vectors)
        {
            var magnitude = Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y);
        }
    }
}
```

### Memory Testing

Monitor memory usage in performance tests:

```csharp
[Fact]
public void MemoryUsage_Test()
{
    var initialMemory = GC.GetTotalMemory(false);
    
    // Perform operations
    
    var finalMemory = GC.GetTotalMemory(true);
    var memoryDelta = finalMemory - initialMemory;
    
    Assert.True(memoryDelta < 1024 * 1024, "Memory usage should be under 1MB");
}
```

## CI/CD Integration

### GitHub Actions

The testing pipeline includes:

1. **Smoke Tests** - Quick validation (< 10 minutes)
2. **Unit Tests** - Full unit test suite (< 30 minutes)
3. **Integration Tests** - End-to-end workflows (< 45 minutes)
4. **Performance Tests** - Performance validation (< 60 minutes)
5. **Security Tests** - Security validation (< 30 minutes)
6. **Cross-Platform Tests** - Multi-platform validation

### Coverage Thresholds

Maintain minimum coverage requirements:
- **Unit Tests**: > 80% coverage
- **Integration Tests**: > 70% coverage
- **Critical Paths**: > 90% coverage

### Test Reporting

Test results are automatically:
- Published to GitHub Actions
- Uploaded to Codecov for coverage
- Archived as artifacts
- Reported via test summary

## Best Practices

### Test Naming

Use descriptive test names that clearly indicate:
- **What** is being tested
- **Given** the conditions
- **Expected** behavior

```csharp
// Good test names
[Fact]
public void Vector2_Addition_ReturnsCorrectSum()

[Theory]
[InlineData(3, 4, 5)]  // 3-4-5 triangle
public void Vector2_Length_CalculatesCorrectMagnitude(float x, float y, float expected)

// Bad test names
[Fact]
public void Test1()
[Fact]
public void VectorTest()
```

### Test Organization

- One test class per functionality area
- Group related tests in same class
- Use test categories for filtering
- Keep tests independent and isolated

### Assert Patterns

Use meaningful assertions with clear messages:

```csharp
// Good assertions
Assert.Equal(expectedVector, actualVector, "Vector addition should produce correct result");
Assert.True(context.IsWithinLimits, "Execution should stay within guardrail limits");

// Avoid vague assertions
Assert.True(result);
Assert.NotNull(object);
```

### Async Testing

Properly handle async operations:

```csharp
[Fact]
public async Task FileOperations_Test()
{
    var result = await _fileIO.WriteTextAsync("test.txt", "content");
    
    Assert.True(result.IsSuccess);
    Assert.NotNull(result.Content);
}

[Fact]
public void SynchronousOperations_Test()
{
    // Use regular Assert for synchronous code
    var result = _calculator.Add(2, 3);
    Assert.Equal(5, result);
}
```

### Test Data Management

- Use `TestDataGenerator` for consistent data
- Avoid hardcoded values in tests
- Parameterize tests with `[Theory]`
- Use setup/teardown for common test state

## Troubleshooting

### Common Issues

#### Tests Not Running
1. Ensure test project builds successfully
2. Check xUnit.runner.json configuration
3. Verify test discovery is working

#### Flaky Tests
1. Add proper synchronization for async tests
2. Use deterministic test data (seeded random)
3. Avoid external dependencies in unit tests
4. Use mocks for unstable dependencies

#### Performance Test Failures
1. Check test environment consistency
2. Allow for timing variance in assertions
3. Use average metrics over single measurements
4. Consider CPU/memory load during tests

#### Memory Leaks
1. Properly dispose test resources
2. Use `using` statements for IDisposable objects
3. Monitor GC memory in long-running tests
4. Clear test data between test runs

### Debugging Tests

#### Enable Diagnostic Output
```bash
dotnet test Tests/TiXL.Tests.csproj --logger "console;verbosity=diagnostic"
```

#### Test-Specific Logging
```csharp
public class MyTests : CoreTestFixture
{
    private readonly ITestOutputHelper _output;
    
    public MyTests(ITestOutputHelper output) : base()
    {
        _output = output;
    }
    
    [Fact]
    public void Debugging_Test()
    {
        _output.WriteLine("Debug information: {0}", someValue);
        // Test logic
    }
}
```

## Contributing

### Adding New Tests

1. Follow existing naming conventions
2. Use appropriate categories
3. Add test data if needed
4. Update documentation
5. Run full test suite before submitting

### Test Review Checklist

- [ ] Tests follow naming conventions
- [ ] Appropriate test categories used
- [ ] Tests are deterministic
- [ ] Test data is properly managed
- [ ] Performance tests are reasonable
- [ ] Documentation is updated
- [ ] All tests pass locally

### Performance Guidelines

- **Unit Tests**: < 100ms per test
- **Integration Tests**: < 1s per test  
- **Performance Tests**: < 10s per test
- **Memory Tests**: < 50MB allocation
- **Overall Suite**: < 30 minutes total

## Support

For questions about the testing framework:
1. Check this documentation
2. Review existing test examples
3. Ask in development team channels
4. Create issues for test framework problems