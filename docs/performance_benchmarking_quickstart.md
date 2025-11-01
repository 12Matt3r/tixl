# TiXL Performance Benchmarking Automation - Quick Start Guide

## Overview

The TiXL Performance Benchmarking Automation System provides comprehensive automated testing for real-time graphics applications with focus on detecting performance regressions and ensuring 60 FPS real-time requirements.

## 🚀 Quick Start

### Prerequisites

- .NET 9.0 SDK
- Windows (for DirectX 12 benchmarks)
- Administrator privileges (for performance optimization)

### Installation

```bash
# Clone the repository
git clone <repository-url>
cd tixl

# Run the automated setup
./scripts/Setup-PerformanceBenchmarking.ps1 -Environment Development -EnableRealtimeMonitoring

# Or manually setup
dotnet restore
dotnet build --configuration Release
```

### Running Benchmarks

```bash
# Run all benchmarks
dotnet run --project Benchmarks/TiXL.Benchmarks.csproj

# Run specific benchmark categories
dotnet run --project Benchmarks/TiXL.Benchmarks.csproj -- --filter "*RealTime*"
dotnet run --project Benchmarks/TiXL.Benchmarks.csproj -- --filter "*NodeEvaluation*"
dotnet run --project Benchmarks/TiXL.Benchmarks.csproj -- --filter "*Graphics*"
dotnet run --project Benchmarks/TiXL.Benchmarks.csproj -- --filter "*Audio*"

# Run performance validation
./scripts/run-performance-validation.sh --commit $(git rev-parse HEAD) --branch develop
```

## 📊 Key Components

### 1. Benchmark Suites

| Suite | Purpose | Targets |
|-------|---------|---------|
| **Node Evaluation** | Test node graph processing performance | <2ms for 1000 nodes |
| **Graphics Rendering** | Validate frame rate and render pipeline | 60 FPS, <2ms variance |
| **Audio Processing** | Test audio-reactive visual latency | <10ms processing latency |
| **Memory Performance** | Detect memory leaks and GC pressure | <15% growth, no leaks |

### 2. Performance Gates

| Gate Type | Threshold | Action on Failure |
|-----------|-----------|-------------------|
| **Frame Time** | 16.67ms (60 FPS) | Block CI/CD merge |
| **Frame Variance** | <2.0ms | Warning |
| **Memory Growth** | <15% | Warning |
| **Regression** | <10% degradation | Block merge |

### 3. Real-Time Validation

- **60 FPS Requirement**: Continuous frame rate monitoring
- **Frame Time Variance**: Statistical analysis of frame timing
- **Audio Latency**: Sub-10ms audio-visual sync validation
- **Memory Stability**: Leak detection and growth monitoring

## 🔧 Configuration

### Performance Thresholds

Edit `Tools/PerformanceRegressionChecker/appsettings.json`:

```json
{
  "PerformanceGates": {
    "FrameTime": {
      "MaxAverageTime": 16.67,
      "MaxVariance": 2.0,
      "MinFPS": 59.0
    },
    "Memory": {
      "MaxMemoryGrowthPercent": 15.0,
      "MaxMemoryLeakBytes": 0
    }
  }
}
```

### CI/CD Integration

```yaml
# .github/workflows/performance.yml
name: Performance Validation

on: [push, pull_request]

jobs:
  performance:
    runs-on: windows-latest
    steps:
    - uses: actions/checkout@v3
    - uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '9.0.x'
    
    - name: Run Performance Benchmarks
      run: ./scripts/run-performance-validation.sh --commit ${{ github.sha }}
    
    - name: Upload Results
      uses: actions/upload-artifact@v3
      with:
        name: performance-results
        path: BenchmarkDotNet.Artifacts/results/
```

## 📈 Dashboard

Access the performance dashboard at:
- **Development**: `http://localhost:8080/performance-dashboard`
- **Real-time Metrics**: Frame rate, memory usage, latency
- **Historical Trends**: 30-day performance analysis
- **Alert Summary**: Active performance issues

## 🔍 Regression Detection

### Automated Detection

The system automatically detects performance regressions using:

1. **Statistical Analysis**: T-test for significance
2. **Historical Comparison**: Baseline vs current performance
3. **Anomaly Detection**: Z-score based outlier detection
4. **Trend Analysis**: Performance degradation patterns

### Alert System

| Severity | Trigger | Action |
|----------|---------|--------|
| **Critical** | >50% regression | Block merge, create issue |
| **High** | >25% regression | Block merge, notify team |
| **Medium** | >15% regression | Warning, require review |
| **Low** | >10% regression | Info log, track trend |

## 📋 Monitoring

### Key Metrics

| Metric | Target | Current |
|--------|--------|---------|
| Frame Rate | 60 FPS | ✓ Pass |
| Frame Variance | <2ms | ✓ Pass |
| Memory Growth | <15% | ✓ Pass |
| Audio Latency | <10ms | ✓ Pass |

### Alerts

- **Slack**: #performance channel notifications
- **Email**: tixl-performance@team.com
- **GitHub Issues**: Automatic issue creation for critical regressions

## 🛠️ Troubleshooting

### Common Issues

1. **Benchmark Failures**
   ```bash
   # Check benchmark results
   find BenchmarkDotNet.Artifacts/results -name "*.md" -exec cat {} \;
   
   # Re-run specific benchmark
   dotnet run --project Benchmarks/TiXL.Benchmarks.csproj -- --filter "*FailedBenchmark*"
   ```

2. **High Frame Variance**
   - Check for background processes
   - Verify process priority settings
   - Review memory allocation patterns

3. **Memory Leaks**
   ```bash
   # Run memory-specific benchmarks
   dotnet run --project Benchmarks/TiXL.Benchmarks.csproj -- --filter "*Memory*"
   
   # Check GC pressure
   dotnet run --project Tools/PerformanceRegressionChecker --check-memory-leaks
   ```

### Debug Mode

```bash
# Enable verbose logging
./scripts/run-performance-validation.sh --verbose --commit <hash>

# Check logs
cat logs/performance-regression-*.log
```

## 📚 Documentation

- **[Full Documentation](docs/performance_benchmarking_automation.md)**: Complete system overview
- **[API Reference](Tools/PerformanceRegressionChecker/)**: Detailed API documentation
- **[Benchmark Results](BenchmarkDotNet.Artifacts/results/)**: Historical performance data

## 🤝 Contributing

### Adding New Benchmarks

1. Create benchmark class in `Benchmarks/` directory
2. Implement `[Benchmark]` methods with proper `[Arguments]`
3. Add to performance validation pipeline
4. Update documentation

### Example Benchmark

```csharp
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
public class MyCustomBenchmark
{
    [Benchmark]
    public void MyOperation()
    {
        // Your benchmark code here
    }
}
```

## 📞 Support

- **Performance Issues**: Create GitHub issue with `performance` label
- **Dashboard Access**: Contact DevOps team
- **Emergency**: Call #performance channel on Slack

## 🎯 Success Criteria

The system is successful when:

- ✅ All benchmarks complete without errors
- ✅ Performance gates pass for main branch
- ✅ Regression detection works within 15 minutes
- ✅ Dashboard shows real-time metrics
- ✅ CI/CD pipeline blocks performance regressions
- ✅ Team receives timely performance alerts

---

**Last Updated**: 2025-11-01  
**Version**: 1.0.0  
**Maintainer**: TiXL Performance Team