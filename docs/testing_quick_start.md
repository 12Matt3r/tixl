# TiXL Testing Framework - Quick Start Guide

This guide provides step-by-step instructions for implementing and using the comprehensive xUnit testing framework for TiXL.

## 🚀 Quick Setup

### 1. Install Dependencies

```bash
# Install required .NET SDK
dotnet --version  # Should be 9.0.x

# Restore NuGet packages
dotnet restore TiXL.sln
```

### 2. Build the Solution

```bash
dotnet build TiXL.sln --configuration Debug
```

### 3. Run Tests

```bash
# Run all tests
dotnet test Tests/TiXL.Tests.csproj

# Run tests with coverage
dotnet test Tests/TiXL.Tests.csproj --collect:"XPlat Code Coverage"

# Run tests with specific category
dotnet test Tests/TiXL.Tests.csproj --filter "Category=P0"

# Run tests with performance tag
dotnet test Tests/TiXL.Tests.csproj --filter "Category=Fast"
```

## 📁 Project Structure

```
Tests/
├── TiXL.Tests.csproj              # Main test project
├── xunit.runner.json              # xUnit configuration
├── TestSettings.runsettings       # Test execution settings
├── TestCategories.cs              # Test category definitions
├── Core/                          # Core module tests
├── Graphics/                      # Graphics module tests
├── Fixtures/                      # Test fixtures
├── Mocks/                         # Mock implementations
└── TestData/                      # Test data files
```

## 🏷️ Test Categories

Tests are organized using categories for easy filtering and execution:

### By Type
- **Unit**: Fast, isolated tests for individual components
- **Integration**: Tests for component interactions
- **Performance**: Tests for performance requirements
- **Graphics**: Tests for graphics rendering
- **UI**: Tests for user interface functionality

### By Priority
- **P0**: Critical tests that must always pass
- **P1**: Important tests that should pass in CI
- **P2**: Normal priority tests
- **P3**: Nice-to-have tests

### By Speed
- **Fast**: Complete in < 100ms
- **Medium**: Complete in < 1 second
- **Slow**: May take longer

## ✍️ Writing Tests

### Basic Test Structure

```csharp
using Xunit;
using FluentAssertions;
using TiXL.Tests.Categories;
using TiXL.Tests.Fixtures;

namespace TiXL.Tests.YourModule
{
    [Collection("Your Test Collection")]
    [Category(TestCategories.Unit)]
    [Category(TestCategories.Core)]
    public class YourClassTests : CoreTestFixture
    {
        [Fact]
        [Category(TestCategories.Fast)]
        public void Method_Scenario_ExpectedBehavior()
        {
            // Arrange
            var input = 42;
            var expected = 43;
            
            // Act
            var result = SomeClass.Method(input);
            
            // Assert
            result.Should().Be(expected);
        }
        
        [Theory]
        [InlineData(1, 2, 3)]      // Test case 1
        [InlineData(5, 7, 12)]     // Test case 2
        public void Method_Theory_VariousInputs_ReturnsExpected(int a, int b, int expected)
        {
            // Arrange & Act
            var result = SomeClass.Method(a, b);
            
            // Assert
            result.Should().Be(expected);
        }
    }
}
```

### Using Test Fixtures

```csharp
public class MyGraphicsTests : GraphicsTestFixture
{
    [Fact]
    public void GraphicsOperation_Scenario_WorksCorrectly()
    {
        // The fixture automatically provides:
        // - IServiceProvider with registered services
        // - ILogger for test logging
        // - Mock graphics device (for graphics tests)
        // - Proper cleanup in DisposeAsync
    }
}
```

### Mocking Dependencies

```csharp
public class MyTests
{
    [Fact]
    public void TestWithMock_DependencyInjection_Works()
    {
        // Use Moq for mocking interfaces
        var mockService = new Mock<IMyService>();
        mockService.Setup(s => s.Method()).Returns("mocked result");
        
        // Use IoC container for injection
        var service = fixture.ServiceProvider.GetRequiredService<IMyService>();
    }
}
```

## 🔧 Running Specific Tests

### By Category

```bash
# Run only P0 tests (critical)
dotnet test --filter "Category=P0"

# Run only fast tests
dotnet test --filter "Category=Fast"

# Run only graphics tests
dotnet test --filter "Category=Graphics"

# Combine filters (AND)
dotnet test --filter "Category=Unit && Category=Fast"

# Combine filters (OR)
dotnet test --filter "Category=P0 || Category=P1"
```

### By Collection

```bash
# Run tests in a specific collection
dotnet test --filter "Collection=Core Tests"

# Exclude specific collection
dotnet test --filter "Collection!=Slow Tests"
```

### Performance Testing

```bash
# Run only performance tests
dotnet test --filter "Category=Performance"

# Run with detailed timing
dotnet test --filter "Category=Performance" --verbosity normal
```

## 📊 Coverage Reports

### Generate Coverage

```bash
# Generate coverage report
dotnet test Tests/TiXL.Tests.csproj --collect:"XPlat Code Coverage"

# Generate HTML report
reportgenerator -reports:Tests/TestResults/coverage.cobertura.xml -targetdir:Tests/TestResults/HTML -reporttypes:Html
```

### View Coverage

- Open `Tests/TestResults/HTML/index.html` in browser
- Check coverage in CI/CD pipeline logs
- Use SonarQube for detailed analysis

## 🚀 CI/CD Integration

### GitHub Actions

1. Copy `.github/workflows/test.yml` to your repository
2. Configure secrets:
   - `CODECOV_TOKEN`: Codecov token for coverage reporting
3. Push to trigger tests automatically

### Azure DevOps

1. Copy `azure-pipelines-test.yml` to your repository
2. Configure in Azure DevOps pipeline
3. Set up coverage thresholds
4. Enable test result publishing

## 🎯 Best Practices

### Test Naming

✅ **Good Names**
- `Vector2_Addition_ReturnsCorrectSum`
- `OperatorRegistry_RegisterOperator_DuplicateName_ThrowsException`
- `Renderer_FrameTime_Meets60FPSRequirement`

❌ **Bad Names**
- `Test1`
- `TestVector2`
- `ShouldWork`

### Test Structure

```csharp
[Fact]
public void Method_Scenario_ExpectedBehavior()
{
    // Arrange - Setup test data, mocks, etc.
    var input = CreateTestInput();
    var expected = CreateExpectedOutput();
    
    // Act - Execute the method being tested
    var actual = ClassUnderTest.Method(input);
    
    // Assert - Verify the results
    actual.Should().Be(expected);
}
```

### Assertions

```csharp
// Use FluentAssertions
result.Should().NotBeNull();
result.Items.Should().HaveCount(5);
result.Status.Should().Be(Success);

// Use approximate comparison for floating point
value.Should().BeApproximately(expectedValue, 0.001f);

// Use collection assertions
collection.Should().BeEquivalentTo(expectedCollection);
collection.Should().Contain(item);
collection.Should().HaveCountGreaterThan(0);

// Use exception assertions
Action act = () => problematicMethod();
act.Should().Throw<ArgumentException>()
    .WithMessage("*invalid parameter*");
```

### Test Data

```csharp
// Use Theory with InlineData for multiple test cases
[Theory]
[InlineData(1, 2, 3)]
[InlineData(0, 0, 0)]
[InlineData(-1, 1, 0)]
public void Add_TwoNumbers_ReturnsSum(int a, int b, int expected)
{
    var result = a + b;
    result.Should().Be(expected);
}

// Use MemberData for complex test data
[Theory]
[MemberData(nameof(VectorTestData.Vector2TestCases), MemberType = typeof(VectorTestData))]
public void Vector_Operations_VariousInputs_WorksCorrectly(Vector2 v1, Vector2 v2, Vector2 expected)
{
    var result = v1 + v2;
    result.Should().Be(expected);
}
```

## 🔧 Troubleshooting

### Common Issues

1. **Test not running**: Check category filter
2. **Coverage not generated**: Ensure `--collect:"XPlat Code Coverage"` flag
3. **Mock not working**: Check service registration in fixture
4. **Performance test failing**: Adjust timeout values

### Debugging

```csharp
// Add logging to tests
public void TestWithLogging()
{
    Logger.LogInformation("Starting test");
    
    // Test implementation
    
    Logger.LogInformation("Test completed");
}

// Use assertion logs
var result = method();
Logger.LogInformation("Result: {Result}", result);
result.Should().NotBeNull();
```

## 📈 Extending the Framework

### Adding New Test Categories

1. Add constant to `TestCategories.cs`
2. Use `[Category("YourCategory")]` on tests
3. Update CI/CD filters as needed

### Creating New Test Fixtures

1. Inherit from `TiXLFacts`
2. Override `ConfigureServices` for dependency injection
3. Use `[Collection("Your Collection")]` attribute

### Adding Mock Implementations

1. Create mock classes in `Mocks/` directory
2. Follow existing mock patterns
3. Implement IDisposable for cleanup
4. Add tests for mock implementations

## 📚 Additional Resources

- [xUnit Documentation](https://xunit.net/)
- [FluentAssertions Guide](https://fluentassertions.com/)
- [Moq Quickstart](https://github.com/Moq/moq4/wiki/Quickstart)
- [Coverlet Documentation](https://github.com/coverlet-coverage/coverlet)
- [BenchmarkDotNet Guide](https://benchmarkdotnet.org/)

## 🆘 Support

If you encounter issues:

1. Check the troubleshooting section above
2. Review existing test examples
3. Check CI/CD logs for detailed error messages
4. Create issues with reproduction steps and logs

---

**Happy Testing! 🎉**