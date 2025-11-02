# TiXL Regression Testing Framework Documentation

## Overview

The TiXL Regression Testing Framework is a comprehensive testing system designed to ensure no breaking changes occur during development and to validate that all existing functionality is preserved while improvements are added. The framework specifically focuses on validating the SharpDX to Vortice.Windows migration and maintaining backward compatibility.

## Table of Contents

1. [Framework Architecture](#framework-architecture)
2. [Test Categories](#test-categories)
3. [Getting Started](#getting-started)
4. [Running Tests](#running-tests)
5. [Test Implementation Details](#test-implementation-details)
6. [Automation and CI/CD](#automation-and-cicd)
7. [Performance Guidelines](#performance-guidelines)
8. [Troubleshooting](#troubleshooting)
9. [Contributing](#contributing)

## Framework Architecture

### Core Components

```
Tests/Regression/
├── RegressionTestRunner.cs          # Main orchestration and reporting
├── ApiCompatibility/
│   └── ApiCompatibilityTests.cs     # Public API validation
├── Migration/
│   └── SharpDXToVorticeMigrationTests.cs  # Migration validation
├── Configuration/
│   └── ConfigurationCompatibilityTests.cs # Settings compatibility
├── ErrorHandling/
│   └── ErrorHandlingConsistencyTests.cs   # Error patterns validation
├── ResourceManagement/
│   └── ResourceManagementTests.cs         # Memory and resource leak detection
└── ThreadSafety/
    └── ThreadSafetyTests.cs               # Concurrent access validation
```

### Test Philosophy

The regression testing framework follows these core principles:

1. **Speed**: All tests complete quickly to catch issues early in development
2. **Reliability**: Tests are deterministic and don't have flaky behavior
3. **Comprehensive Coverage**: Validates all critical functionality and edge cases
4. **Backward Compatibility**: Ensures no breaking changes to existing APIs
5. **Migration Safety**: Validates SharpDX to Vortice.Windows migration
6. **Resource Safety**: Detects memory leaks and resource leaks
7. **Thread Safety**: Ensures concurrent access patterns work correctly

## Test Categories

### 1. API Compatibility Tests

**Purpose**: Validate that all public APIs maintain backward compatibility

**Scope**:
- Public method signatures
- Property accessibility
- Constructor parameters
- Return types
- Event signatures

**Key Tests**:
- `PublicAPISurface_AllTypesAccessible()` - Ensures all public types remain accessible
- `PublicAPIMethods_SignatureCompatibility()` - Validates method signatures
- `PublicAPIProperties_Accessibility()` - Tests property access
- `DirectXMigration_VorticeTypesAvailable()` - Verifies Vortice types are accessible

### 2. Migration Tests (SharpDX to Vortice.Windows)

**Purpose**: Ensure no functionality is lost during SharpDX to Vortice.Windows migration

**Scope**:
- Type mapping validation
- API signature compatibility
- Resource management patterns
- Performance characteristics
- Backward compatibility

**Key Tests**:
- `TypeMapping_SharpDXToVorticeExists()` - Validates type mappings
- `TypeSignatures_CompatibilityMaintained()` - Ensures signatures remain compatible
- `MethodSignatures_CompatibilityMaintained()` - Tests method compatibility
- `MemoryLayout_StructCompatibility()` - Validates memory layout

### 3. Configuration Compatibility Tests

**Purpose**: Validate configuration and settings compatibility

**Scope**:
- Configuration object creation
- Serialization/deserialization
- Environment-specific settings
- Backward compatibility
- Validation constraints

**Key Tests**:
- `RenderingEngineConfig_ValidConfigurationCreation()` - Validates config creation
- `ConfigurationSerialization_RoundTrip()` - Tests serialization
- `ConfigurationValidation_InvalidValueDetection()` - Tests validation
- `ConfigurationEnvironmentVariables_OverrideSupport()` - Tests environment overrides

### 4. Error Handling Consistency Tests

**Purpose**: Ensure error handling patterns are consistent across all modules

**Scope**:
- Null argument validation
- Invalid state handling
- Exception type consistency
- Error recovery mechanisms
- Logging integration

**Key Tests**:
- `NullArgumentValidation_ConsistentBehavior()` - Tests null validation
- `InvalidStateHandling_ConsistentPatterns()` - Validates state handling
- `ExceptionTypes_ConsistentUsage()` - Ensures consistent exception usage
- `SystemResilience_CanContinueAfterFailures()` - Tests error recovery

### 5. Resource Management Tests

**Purpose**: Detect memory leaks and resource leaks

**Scope**:
- Memory allocation patterns
- Resource disposal
- Long-running operation stability
- Concurrent resource usage
- Pool management

**Key Tests**:
- `PipelineState_MemoryManagement()` - Tests PSO memory management
- `ConcurrentOperations_MemoryStability()` - Tests concurrent usage
- `ResourcePool_ManagementAndCleanup()` - Validates pool cleanup
- `StressTest_ResourceManagement()` - High-stress testing

### 6. Thread Safety Tests

**Purpose**: Ensure thread safety across all components

**Scope**:
- Concurrent access patterns
- Lock-free operations
- Race condition detection
- Deadlock prevention
- Thread-safe collections

**Key Tests**:
- `PipelineState_ConcurrentAccess()` - Tests concurrent PSO access
- `MaterialPSOKey_ThreadSafety()` - Validates key thread safety
- `PerformanceMonitor_ConcurrentAccess()` - Tests monitor thread safety
- `RaceConditionDetection_PSOLifecycle()` - Detects race conditions

## Getting Started

### Prerequisites

1. **.NET 8.0 SDK** or later
2. **Visual Studio 2022** or **VS Code** with C# extension
3. **Git** for version control
4. **Windows** (for DirectX-related tests)

### Initial Setup

1. **Clone the repository**:
   ```bash
   git clone <repository-url>
   cd tixl
   ```

2. **Restore dependencies**:
   ```bash
   dotnet restore
   ```

3. **Build the solution**:
   ```bash
   dotnet build --configuration Release
   ```

4. **Verify test discovery**:
   ```bash
   dotnet test --list-tests Tests/TiXL.Tests.csproj
   ```

### Test Configuration

The test framework uses several configuration files:

- `xunit.runner.json` - Test runner configuration
- `TestSettings.runsettings` - Test execution settings
- `CoverletSettings.runsettings` - Code coverage settings

## Running Tests

### Command Line

#### Run All Regression Tests
```bash
# Run all regression tests
dotnet test Tests/TiXL.Tests.csproj --filter "Category=Regression"

# Run with detailed output
dotnet test Tests/TiXL.Tests.csproj --filter "Category=Regression" --logger "console;verbosity=detailed"

# Run with coverage
dotnet test Tests/TiXL.Tests.csproj --filter "Category=Regression" --collect:"XPlat Code Coverage"
```

#### Run Specific Categories
```bash
# API compatibility tests only
dotnet test Tests/TiXL.Tests.csproj --filter "Category=ApiCompatibility"

# Migration tests only
dotnet test Tests/TiXL.Tests.csproj --filter "Category=Migration"

# Resource management tests only
dotnet test Tests/TiXL.Tests.csproj --filter "Category=ResourceManagement"

# Thread safety tests only
dotnet test Tests/TiXL.Tests.csproj --filter "Category=ThreadSafety"
```

#### Run Specific Test Classes
```bash
# Run specific test class
dotnet test Tests/TiXL.Tests.csproj --filter "ClassName=ApiCompatibilityTests"

# Run specific test method
dotnet test Tests/TiXL.Tests.csproj --filter "MethodName=PublicAPISurface_AllTypesAccessible"
```

#### Performance Testing
```bash
# Run performance-sensitive tests
dotnet test Tests/TiXL.Tests.csproj --filter "Category=Performance & Category=Regression"

# Run with extended timeout
dotnet test Tests/TiXL.Tests.csproj --filter "Category=ThreadSafety" --timeout 300000
```

### Using the Regression Test Runner

```csharp
// Program.cs or test entry point
var options = new RegressionTestOptions
{
    Verbose = true,
    Categories = new List<string>
    {
        "ApiCompatibility",
        "Migration",
        "Configuration",
        "ErrorHandling",
        "ResourceManagement",
        "ThreadSafety"
    },
    OutputPath = "regression-test-report.json",
    TimeoutSeconds = 300
};

var output = new TestOutputHelper(); // Your ITestOutputHelper implementation
var logger = serviceProvider.GetRequiredService<ILogger<RegressionTestRunner>>();
var runner = new RegressionTestRunner(output, logger);

var report = await runner.RunAllRegressionTestsAsync(options);

// Check results
if (report.AllTestsPassed)
{
    Console.WriteLine("✅ All regression tests passed!");
}
else
{
    Console.WriteLine($"❌ {report.TotalTestsFailed} test(s) failed");
    foreach (var error in report.ErrorDetails)
    {
        Console.WriteLine($"  - {error}");
    }
}
```

### Visual Studio Integration

1. **Open Test Explorer**:
   - Go to `Test` → `Windows` → `Test Explorer`
   - Build the solution to discover tests

2. **Filter by Category**:
   - Right-click on test categories in Test Explorer
   - Select "Run" to execute specific categories

3. **Run Individual Tests**:
   - Click on any test in Test Explorer
   - Use "Run" button or right-click context menu

### Continuous Integration

The regression tests are automatically run in CI/CD pipelines:

- **GitHub Actions**: `.github/workflows/regression-tests.yml`
- **Triggered on**: Push, Pull Request, Daily Schedule
- **Platform**: Windows (for DirectX tests)
- **Parallel Execution**: Test categories run in parallel for speed

## Test Implementation Details

### Writing New Regression Tests

#### 1. Test Class Structure

```csharp
[TestCategories(TestCategory.Regression | TestCategory.YourCategory | TestCategory.P1)]
public class YourRegressionTests : CoreTestFixture
{
    private readonly ITestOutputHelper _output;
    
    public YourRegressionTests(ITestOutputHelper output) : base()
    {
        _output = output;
        _output.WriteLine("Starting Your Regression Tests");
    }
    
    [Fact]
    public void YourTestMethod_ExpectedBehavior()
    {
        // Arrange
        var component = CreateComponent();
        
        // Act
        var result = component.SomeOperation();
        
        // Assert
        result.Should().NotBeNull("Expected operation to succeed");
        result.Should().Be(expectedValue, "Expected specific value");
        
        _output.WriteLine($"Test completed successfully with result: {result}");
    }
}
```

#### 2. Test Categories

Use appropriate categories:

```csharp
[TestCategories(
    TestCategory.Regression,        // Always include
    TestCategory.ApiCompatibility,   // Specific category
    TestCategory.P0)]               // Priority level
```

Category hierarchy:
- **Functional**: `ApiCompatibility`, `Migration`, `Configuration`, `ErrorHandling`, `ResourceManagement`, `ThreadSafety`
- **Priority**: `P0` (Critical), `P1` (High), `P2` (Medium), `P3` (Low)
- **Performance**: `Fast` (<100ms), `Medium` (100ms-1s), `Slow` (>1s)
- **Type**: `Unit`, `Integration`, `Performance`

#### 3. Test Data Management

```csharp
public static TheoryData<Type, string, Type[]> YourTestData =>
    new TheoryData<Type, string, Type[]>
    {
        { typeof(YourComponent), "MethodName", new[] { typeof(string), typeof(int) } },
        // Add more test cases
    };
```

#### 4. Async Testing Patterns

```csharp
[Fact]
public async Task AsyncOperation_Test()
{
    // Arrange
    var asyncComponent = new AsyncComponent();
    
    // Act
    var result = await asyncComponent.PerformAsync();
    
    // Assert
    result.Should().Be(expectedResult);
}

[Theory]
[MemberData(nameof(AsyncTestData))]
public async Task AsyncTheory_Test(string scenario, int expectedValue)
{
    // Test implementation
}
```

### Mock Objects and Test Utilities

#### MockD3D12Device Usage

```csharp
// Create mock DirectX device for testing
using var device = MockD3D12Device.Create();
var engine = new DirectX12RenderingEngine(
    device.Device,
    device.CommandQueue);
```

#### Custom Mocks

```csharp
public class MockGraphicsComponent : IGraphicsComponent
{
    public bool IsDisposed { get; private set; }
    
    public void Dispose()
    {
        IsDisposed = true;
    }
    
    public void PerformOperation()
    {
        // Mock implementation
    }
}
```

### Assertions and Error Messages

#### Using FluentAssertions

```csharp
// Good assertions with clear messages
actualObject.Should().NotBeNull("Object should be created successfully");
actualValue.Should().Be(expectedValue, "Expected specific calculation result");
collection.Should().HaveCount(5, "Expected 5 items in collection");
action.Should().Throw<ArgumentNullException>().WithParameterName("parameter");

// Avoid vague assertions
// Bad:
Assert.True(result);
// Good:
result.Should().BeTrue("Operation should succeed");
```

#### Test Output

```csharp
_output.WriteLine($"Processing item: {item.Name}");
_output.WriteLine($"Operation took {stopwatch.ElapsedMilliseconds}ms");
_output.WriteLine($"Memory usage: {GC.GetTotalMemory(false)} bytes");
```

## Automation and CI/CD

### GitHub Actions Workflow

The regression testing pipeline automatically:

1. **Detects Changes**: Only runs tests when core code changes
2. **Runs Test Categories**: Parallel execution of all test categories
3. **Performance Validation**: Validates performance doesn't regress
4. **Memory Leak Detection**: Runs memory-specific tests
5. **Cross-Platform Testing**: Validates compatibility across platforms
6. **API Compatibility**: Ensures no breaking API changes
7. **Generates Reports**: Creates comprehensive test reports

### Pipeline Stages

1. **Pre-build Checks**: Determine if tests should run
2. **Regression Tests**: Core functionality validation
3. **Performance Tests**: Performance regression detection
4. **Memory Tests**: Memory leak detection
5. **Cross-Platform Tests**: Multi-platform compatibility
6. **API Validation**: API compatibility checking
7. **Report Generation**: Comprehensive reporting

### Manual Trigger

The pipeline can be manually triggered with custom parameters:

```yaml
workflow_dispatch:
  inputs:
    test_categories:
      description: 'Test categories to run'
      required: false
      default: 'ApiCompatibility,Migration'
    timeout_minutes:
      description: 'Timeout in minutes'
      required: false
      default: '30'
```

## Performance Guidelines

### Test Execution Speed

- **Unit Tests**: < 100ms per test
- **Integration Tests**: < 1s per test
- **Performance Tests**: < 10s per test
- **Full Regression Suite**: < 30 minutes total

### Memory Usage

- **Per Test**: < 10MB allocation
- **Full Suite**: < 100MB growth allowed
- **Cleanup**: Must dispose all resources

### Concurrent Testing

- **Parallel Execution**: Use `Parallel.ForEach` for independent tests
- **Thread Safety**: All shared resources must be thread-safe
- **Race Conditions**: Test for and prevent race conditions

### Performance Benchmarks

```csharp
[Benchmark]
public void PerformanceTest()
{
    var stopwatch = Stopwatch.StartNew();
    
    // Code to benchmark
    
    stopwatch.Stop();
    _output.WriteLine($"Operation took {stopwatch.ElapsedMilliseconds}ms");
    
    // Assert performance is within acceptable bounds
    stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, "Should complete quickly");
}
```

## Troubleshooting

### Common Issues

#### Tests Not Running

**Symptoms**: No tests appear in Test Explorer

**Solutions**:
1. Ensure project builds successfully: `dotnet build`
2. Check xUnit configuration in `xunit.runner.json`
3. Verify test discovery: `dotnet test --list-tests`
4. Check for compilation errors in test code

#### Flaky Tests

**Symptoms**: Tests sometimes pass, sometimes fail

**Solutions**:
1. Remove external dependencies (use mocks)
2. Add proper synchronization for async operations
3. Use deterministic test data (seeded random)
4. Add proper cleanup in `Dispose()`

#### Memory Leaks in Tests

**Symptoms**: Test suite gets slower over time or fails with OOM

**Solutions**:
1. Always dispose IDisposable objects
2. Use `using` statements for disposable resources
3. Force garbage collection between tests
4. Monitor memory usage in tests

#### Thread Safety Issues

**Symptoms**: Intermittent test failures in concurrent scenarios

**Solutions**:
1. Use thread-safe collections
2. Add proper locking for shared state
3. Test concurrent access patterns
4. Use `ConcurrentBag` for thread-safe data collection

### Debugging Tests

#### Enable Diagnostic Output

```bash
dotnet test --logger "console;verbosity=diagnostic"
```

#### Test-Specific Logging

```csharp
public class DebugTestFixture : CoreTestFixture
{
    private readonly ITestOutputHelper _output;
    
    [Fact]
    public void DebugTest()
    {
        _output.WriteLine("Debug information here");
        
        // Test logic
        
        _output.WriteLine($"Test state: {state}");
    }
}
```

#### Breakpoint Debugging

1. Set breakpoints in test code
2. Use "Debug" instead of "Run" in Test Explorer
3. Step through test execution
4. Inspect variables and state

### Performance Issues

#### Slow Test Execution

**Diagnosis**:
1. Profile test execution: `dotnet-trace collect`
2. Identify bottlenecks with diagnostic logging
3. Check for unnecessary waits or sleeps
4. Analyze memory allocation patterns

**Solutions**:
1. Reduce test data size
2. Mock expensive operations
3. Parallelize independent tests
4. Optimize test setup/teardown

## Contributing

### Adding New Regression Tests

1. **Follow Naming Conventions**:
   - Test classes: `[Component]RegressionTests`
   - Test methods: `[Operation]_[ExpectedResult]_[Condition]`

2. **Use Appropriate Categories**:
   ```csharp
   [TestCategories(TestCategory.Regression | TestCategory.YourCategory | TestCategory.P1)]
   ```

3. **Write Descriptive Tests**:
   - Clear test names that explain what is being tested
   - Comprehensive assertions with meaningful messages
   - Proper setup and cleanup

4. **Update Documentation**:
   - Update this README if adding new categories
   - Document any special test requirements
   - Add examples for new test patterns

### Test Review Checklist

- [ ] Tests follow naming conventions
- [ ] Appropriate test categories used
- [ ] Tests are deterministic and not flaky
- [ ] Tests handle cleanup properly
- [ ] Performance is within guidelines
- [ ] Tests are properly documented
- [ ] All tests pass locally
- [ ] No external dependencies in unit tests
- [ ] Thread safety considerations addressed
- [ ] Memory usage is appropriate

### Code Quality Standards

1. **Test Independence**: Tests should not depend on each other
2. **Clear Assertions**: Use descriptive assertion messages
3. **Proper Cleanup**: Dispose all resources in `Dispose()`
4. **Error Handling**: Test both success and failure scenarios
5. **Edge Cases**: Test boundary conditions and error cases

### Performance Standards

1. **Speed**: Tests should run quickly
2. **Memory**: Avoid unnecessary allocations
3. **Concurrency**: Test concurrent scenarios safely
4. **Reliability**: Tests should be deterministic

## Support

For questions or issues with the regression testing framework:

1. **Check this documentation** first
2. **Review existing test examples** for patterns
3. **Ask in development team channels**
4. **Create issues** for framework problems

### Getting Help

- **Framework Issues**: Create GitHub issue with `regression-tests` label
- **Test Failures**: Check CI logs and local reproduction steps
- **Performance Issues**: Include timing and memory usage data
- **Thread Safety Issues**: Provide minimal reproduction case

### Reporting Bugs

When reporting test framework bugs:

1. **Reproduce locally** with detailed steps
2. **Include system information**: OS, .NET version, Visual Studio version
3. **Provide logs**: Console output and test results
4. **Minimal example**: Simplest code that demonstrates the issue

---

*This documentation is part of the TiXL Regression Testing Framework. For the latest updates, see the repository wiki or GitHub issues.*
