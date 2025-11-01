# Incremental Evaluation System Test Suite

## Overview

This test suite provides comprehensive testing for the Incremental Node Graph Evaluation System (TIXL-025), including unit tests for individual components and integration tests that validate the complete system against performance and correctness requirements.

## Test Structure

```
/workspace/Tests/Operators/IncrementalEvaluation/
├── IncrementalEvaluationTestFixture.cs          # Common test utilities and setup
├── IncrementalNodeGraphTests.cs                 # Tests for main orchestrator
├── DependencyGraphTests.cs                      # Tests for dependency tracking
├── CacheManagerTests.cs                         # Tests for caching system
├── DirtyTrackerTests.cs                         # Tests for dirty flag system
├── TopologicalEvaluatorTests.cs                 # Tests for evaluation engine
├── PerformanceMonitorTests.cs                   # Tests for performance tracking
└── IncrementalEvaluationIntegrationTests.cs     # Integration and performance tests
```

## Test Categories

### Unit Tests

Each component has comprehensive unit tests covering:

- **IncrementalNodeGraphTests**: Node management, parameter changes, evaluation triggering
- **DependencyGraphTests**: Dependency tracking, topological sorting, cycle detection
- **CacheManagerTests**: Cache operations, LRU eviction, memory management
- **DirtyTrackerTests**: Dirty flag propagation, batch operations, state queries
- **TopologicalEvaluatorTests**: Evaluation order, error handling, cancellation
- **PerformanceMonitorTests**: Metrics collection, statistics, bottleneck detection

### Integration Tests

- **Performance Comparison**: Incremental vs full evaluation for 100, 500, and 1000+ node graphs
- **Correctness Validation**: Ensures incremental evaluation produces same results as full evaluation
- **Edge Cases**: Cycles, disconnected nodes, complex dependency chains
- **Memory Usage**: Cache effectiveness and memory management validation
- **Performance Targets**: Validates 95% evaluation time reduction target

## Running Tests

### Command Line
```bash
# Run all incremental evaluation tests
dotnet test Tests/TiXL.Tests.csproj --filter "Category=Operators"

# Run only unit tests
dotnet test Tests/TiXL.Tests.csproj --filter "Category=Unit"

# Run only integration tests
dotnet test Tests/TiXL.Tests.csproj --filter "Category=Integration"

# Run specific test class
dotnet test Tests/TiXL.Tests.csproj --filter "FullyQualifiedName~IncrementalNodeGraphTests"

# Run with detailed output
dotnet test Tests/TiXL.Tests.csproj --logger "console;verbosity=detailed"
```

### Visual Studio
1. Open Test Explorer
2. Filter by category: "Operators"
3. Run desired test groups

## Performance Benchmarks

The integration tests validate performance targets:

### Target Improvements
- **95% evaluation time reduction** for large graphs (1000+ nodes)
- **90% UI responsiveness improvement** for interactive editing
- **Linear scalability** with graph size
- **Memory efficiency** with configurable cache limits

### Test Scenarios
- **Small graphs** (100 nodes): Basic functionality validation
- **Medium graphs** (500 nodes): Performance characteristic validation
- **Large graphs** (1000+ nodes): Target performance validation
- **Edge cases**: Complex dependencies, cycles, disconnected components

## Test Data and Utilities

### IncrementalEvaluationTestFixture
Provides common setup and utilities:

```csharp
// Create test graphs with various structures
var linearGraph = fixture.CreateLinearChainGraph(50);
var treeGraph = fixture.CreateTreeGraph(100);
var complexGraph = fixture.CreateComplexGraph(200);

// Performance benchmark configuration
var benchmark = fixture.CreatePerformanceBenchmark(1000);

// Test data set
var testData = fixture.CreateTestDataSet(100);
```

### Test Extensions
Helper methods for common operations:

```csharp
// Wait for graph stability
await graph.WaitForStability();

// Create complex scenarios
graph.CreateComplexScenario(3);

// Verify consistency
graph.VerifyConsistency();
```

## Mock Dependencies

For unit testing, each component uses carefully designed mocks:

```csharp
// Example: Mock context and dependencies
var mockContext = new Mock<IEvaluationContext>();
var dependencyGraph = new DependencyGraph();
var cacheManager = new CacheManager();
var dirtyTracker = new DirtyTracker();
var evaluator = new TopologicalEvaluator(mockContext.Object, cacheManager);
var performanceMonitor = new PerformanceMonitor();
```

## Coverage Areas

### IncrementalNodeGraph
- [x] Node addition and removal
- [x] Dependency management
- [x] Parameter change handling
- [x] Evaluation triggering
- [x] Cache integration
- [x] Performance metrics collection
- [x] Error handling and cancellation

### DependencyGraph
- [x] Node management operations
- [x] Dependency addition/removal
- [x] Topological sorting
- [x] Cycle detection
- [x] Affected node queries
- [x] Performance with large node counts
- [x] Thread safety validation

### CacheManager
- [x] Data storage and retrieval
- [x] LRU eviction policy
- [x] Memory limit enforcement
- [x] Cache invalidation
- [x] Statistics tracking
- [x] Serialization/deserialization
- [x] Memory leak prevention

### DirtyTracker
- [x] Dirty flag management
- [x] Dependency-based propagation
- [x] Batch operations
- [x] State queries
- [x] Circular dependency detection
- [x] Performance optimization

### TopologicalEvaluator
- [x] Evaluation order correctness
- [x] Cache integration
- [x] Error handling
- [x] Cancellation support
- [x] Parallel evaluation
- [x] Performance metrics

### PerformanceMonitor
- [x] Metrics collection
- [x] Performance tracking
- [x] Statistics calculation
- [x] Bottleneck detection
- [x] Trend analysis
- [x] Data export functionality

## Integration Test Scenarios

### Performance Validation
Tests validate performance targets through realistic scenarios:

1. **Linear Chains**: Simple dependency chains
2. **Balanced Trees**: Realistic node editor structures
3. **Complex Networks**: Highly interconnected graphs
4. **Mixed Patterns**: Combination of different structures

### Correctness Validation
Ensures incremental evaluation maintains correctness:

1. **Result Equivalence**: Same outputs as full evaluation
2. **Dependency Respect**: Proper evaluation order
3. **Cache Consistency**: Correct cache usage and invalidation
4. **State Consistency**: Valid internal state throughout operations

### Edge Case Handling
Tests various problematic scenarios:

1. **Circular Dependencies**: Detection and handling
2. **Disconnected Components**: Independent evaluation
3. **Large Scale Operations**: Memory and performance limits
4. **Concurrent Access**: Thread safety validation

## Continuous Integration

Tests are designed for CI/CD integration:

- **Fast Unit Tests**: Complete in seconds
- **Automated Performance Tests**: Validate performance targets
- **Deterministic Results**: Consistent test outcomes
- **Comprehensive Coverage**: All code paths tested

## Extending Tests

### Adding New Test Cases
1. Follow existing naming conventions
2. Use appropriate test categories
3. Include both positive and negative test cases
4. Add performance considerations where relevant

### Performance Test Additions
1. Define performance targets
2. Create realistic test scenarios
3. Include baseline measurements
4. Validate against established thresholds

## Test Dependencies

### Required NuGet Packages
- xunit
- xunit.runner.visualstudio
- FluentAssertions
- Moq
- Microsoft.Extensions.Logging

### Project References
- TiXL.Core (main implementation)
- TiXL.Tests (test infrastructure)

## Troubleshooting

### Common Issues
1. **Test Timeouts**: Increase timeout for large graph tests
2. **Memory Issues**: Adjust cache sizes for memory-intensive tests
3. **Concurrency Issues**: Ensure proper thread safety in parallel tests
4. **Performance Variability**: Use appropriate tolerance ranges

### Debug Techniques
1. Enable detailed logging for test failures
2. Use incremental test running for debugging
3. Monitor memory usage during performance tests
4. Validate intermediate states in complex scenarios

## Success Criteria

Tests pass when:
- All unit tests complete successfully
- Performance targets are met for large graphs
- Correctness is maintained across all scenarios
- Edge cases are handled gracefully
- Memory usage remains within acceptable bounds

## Maintenance

Regular maintenance includes:
- Performance benchmark updates
- New edge case coverage
- Performance target adjustments
- Test data refresh for scalability testing