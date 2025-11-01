# TiXL Performance Benchmarking Suite (TIXL-054)

A comprehensive performance benchmarking system for TiXL that tracks performance over time, detects regressions, and provides actionable insights for optimization.

## 🎯 Overview

This suite provides automated performance monitoring and regression detection for TiXL, covering:

- **Frame Time Performance** - Real-time rendering metrics and FPS consistency
- **Memory Usage Analysis** - Allocation patterns, GC pressure, and leak detection
- **Project Load Times** - Initialization and loading performance
- **Operator Execution** - Node graph evaluation performance
- **Graphics Pipeline** - Rendering and shader compilation metrics
- **Audio Latency** - Audio processing and synchronization performance

## 🚀 Quick Start

### Running All Benchmarks
```bash
cd Benchmarks
dotnet run
```

### Running Specific Categories
```bash
# Frame time and memory benchmarks only
dotnet run -- --categories FrameTime,MemoryUsage

# Graphics and audio performance
dotnet run -- --categories GraphicsPerf,AudioLatency
```

### CI Integration
```bash
# Full CI pipeline with regression detection
./scripts/run-performance-ci.sh full-ci-run --ci-mode --threshold 15.0

# Build and benchmark
./scripts/run-performance-ci.sh build-and-benchmark

# Create performance baseline
./scripts/run-performance-ci.sh create-baseline --baseline release-v1.0
```

## 📊 Benchmark Categories

### FrameTime Benchmarks
Measures real-time rendering performance:
- **Single-threaded Rendering Baseline** - Basic performance reference
- **Multi-threaded Rendering** - Parallel processing optimization
- **Predictive Frame Scheduling** - Consistent FPS targeting
- **Adaptive Load Balancing** - Dynamic workload distribution
- **Audio-Visual Sync** - Latency and synchronization accuracy
- **Stress Test Stability** - Performance under high load

**Key Metrics:**
- Average frame time (ms)
- Frames per second (FPS)
- Frame time variance
- Consistency score
- Sync error (ms)

### MemoryUsage Benchmarks
Analyzes memory allocation and management:
- **Baseline Naive Allocation** - Unoptimized memory usage
- **ArrayPool Optimization** - Shared buffer allocation
- **Custom Object Pooling** - Specialized object reuse
- **Stack Allocation** - Stack-based temporary objects
- **GC Pressure Testing** - Garbage collection impact
- **Memory Leak Detection** - Reference and resource leaks
- **Large Object Heap** - LOH allocation patterns
- **Memory Fragmentation** - Allocation pattern analysis

**Key Metrics:**
- Memory allocation rate (bytes/second)
- GC collection count (Gen0/Gen1/Gen2)
- Allocation overhead
- Memory leak detection
- Pool efficiency

### ProjectLoad Benchmarks
Measures project initialization performance:
- **Project File Parsing** - XML/JSON deserialization
- **Scene Graph Loading** - Node and connection creation
- **Resource Loading** - Textures, shaders, audio files
- **Dependency Resolution** - Operator and node dependencies
- **Plugin Loading** - Extension and module loading
- **Configuration Loading** - Settings and preferences

**Key Metrics:**
- Load time (ms)
- File I/O throughput
- Parse performance
- Memory usage during load

### OperatorExec Benchmarks
Tests node operator execution performance:
- **Sequential Evaluation** - Linear operator processing
- **Parallel Evaluation** - Concurrent operator execution
- **Incremental Updates** - Change propagation efficiency
- **Operator Chaining** - Pipeline processing performance
- **Operator Creation** - Dynamic operator instantiation
- **Pipeline Processing** - Batch operation efficiency

**Key Metrics:**
- Evaluation time (ms)
- Throughput (operators/second)
- Parallel efficiency
- Memory usage per operator

### GraphicsPerf Benchmarks
Rendering pipeline performance:
- **Vertex Processing** - Geometry transformation
- **Pixel Processing** - Fragment shader execution
- **Texture Operations** - Sampling and filtering
- **Shader Compilation** - Runtime compilation performance
- **Draw Call Batching** - Rendering efficiency
- **Buffer Management** - GPU buffer operations

**Key Metrics:**
- Processing time (ms)
- Shader compilation time (ms)
- Fill rate (pixels/second)
- Draw call efficiency

### AudioLatency Benchmarks
Audio processing performance:
- **Buffer Processing** - Audio sample processing
- **Real-time Synthesis** - On-the-fly audio generation
- **Effect Processing** - Filters and effects
- **Mixing Performance** - Multi-channel audio mixing
- **Synchronization** - Audio-video sync accuracy
- **Latency Measurement** - End-to-end audio latency

**Key Metrics:**
- Processing latency (ms)
- Buffer underrun count
- Sync accuracy (%)
- Processing throughput

## 🔧 Configuration

### Benchmark Settings (`config/benchmarksettings.json`)
```json
{
  "BenchmarkSettings": {
    "Execution": {
      "WarmupIterations": 3,
      "MeasurementIterations": 10,
      "JobLaunchCount": 1
    },
    "Categories": {
      "FrameTime": {
        "TargetFrameRate": 60,
        "MaxFrameTime": 16.67,
        "Enabled": true
      },
      "MemoryUsage": {
        "MaxAllocationPerFrame": 1048576,
        "MaxGCCollectionsPerSecond": 10,
        "Enabled": true
      }
    },
    "Thresholds": {
      "RegressionThreshold": 10.0,
      "CriticalRegressionThreshold": 25.0
    }
  }
}
```

### Baseline Configuration (`config/baselines.json`)
```json
{
  "Baselines": {
    "Default": {
      "Description": "Default performance baseline",
      "Thresholds": {
        "FrameTime": {
          "TargetMs": 16.67,
          "MaxMs": 20.0
        }
      }
    }
  }
}
```

## 📈 Performance Analysis

### Regression Detection
The system automatically compares current performance against established baselines:

```bash
# Check for regressions against default baseline
dotnet run --regression

# Use custom baseline and threshold
dotnet run --regression --baseline release-v1.0 --threshold 15.0
```

### Trend Analysis
Historical performance trends are tracked and analyzed:

```csharp
// Generate trend report for last 30 days
var trendAnalyzer = serviceProvider.GetRequiredService<TrendAnalyzer>();
var trends = await trendAnalyzer.AnalyzeTrends(30);
```

### Performance Insights
Automated insights and recommendations:

- **Trend Analysis** - Performance direction over time
- **Anomaly Detection** - Unusual performance patterns
- **System Health** - Resource usage trends
- **Optimization Suggestions** - Performance improvement recommendations

## 📊 Reports

### HTML Report
Interactive performance report with charts and metrics:
```bash
dotnet run --report ./reports/monthly-report.html
```

### JSON Report
Machine-readable performance data:
```bash
dotnet run --report ./reports/data.json --format json
```

### CSV Report
Spreadsheet-compatible performance data:
```bash
dotnet run --report ./reports/metrics.csv --format csv
```

## 🚨 Alert System

### Alert Types
- **Performance Regression** - Performance degradation vs baseline
- **Threshold Exceeded** - Performance beyond acceptable limits
- **System Resource** - High CPU, memory, or GC pressure
- **Anomaly Detection** - Unusual performance patterns

### Alert Channels
- **Console** - Always enabled for immediate feedback
- **Email** - Configurable recipients for critical alerts
- **Slack** - Team notifications via webhook
- **GitHub** - Automatic issue creation for regressions

### Alert Configuration
```json
{
  "Alerts": {
    "Enabled": true,
    "EmailNotifications": true,
    "SlackNotifications": false,
    "GitHubIssues": true,
    "AlertCooldownMinutes": 30,
    "MaxAlertsPerHour": 10
  }
}
```

## 🎛️ CI/CD Integration

### Azure DevOps Pipeline
```yaml
- task: PowerShell@2
  displayName: 'Run Performance Benchmarks'
  inputs:
    targetType: 'filePath'
    filePath: 'Benchmarks/scripts/run-performance-ci.sh'
    arguments: 'full-ci-run --ci-mode --threshold 15.0 --categories FrameTime,MemoryUsage'
```

### GitHub Actions
```yaml
- name: Run Performance Benchmarks
  run: |
    cd Benchmarks
    chmod +x scripts/run-performance-ci.sh
    ./scripts/run-performance-ci.sh full-ci-run --ci-mode --threshold 10.0
```

### Jenkins Pipeline
```groovy
stage('Performance Benchmarks') {
    steps {
        sh '''
            cd Benchmarks
            chmod +x scripts/run-performance-ci.sh
            ./scripts/run-performance-ci.sh build-and-benchmark --ci-mode
        '''
    }
    post {
        always {
            archiveArtifacts artifacts: 'Benchmarks/Reports/**/*', allowEmptyArchive: true
        }
    }
}
```

## 📁 Project Structure

```
Benchmarks/
├── TiXLPerformanceSuite.csproj      # Benchmark suite project
├── Program.cs                        # Main entry point
├── Benchmarks/                       # Individual benchmark categories
│   ├── FrameTimeBenchmarks.cs
│   ├── MemoryBenchmarks.cs
│   ├── ProjectLoadBenchmarks.cs
│   ├── OperatorExecutionBenchmarks.cs
│   ├── GraphicsBenchmarks.cs
│   └── AudioBenchmarks.cs
├── Core/                            # Core services
│   ├── PerformanceMonitorService.cs
│   ├── BaselineManager.cs
│   ├── ReportGenerator.cs
│   ├── AlertService.cs
│   └── TrendAnalyzer.cs
├── Models/                          # Data models
│   └── PerformanceModels.cs
├── config/                          # Configuration files
│   ├── benchmarksettings.json
│   └── baselines.json
├── Scripts/                         # CI integration scripts
│   └── run-performance-ci.sh
└── Reports/                         # Generated reports
```

## 🛠️ Advanced Usage

### Custom Benchmarks
Create custom benchmarks by extending the base classes:

```csharp
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
public class CustomBenchmark
{
    private readonly PerformanceMonitorService _monitor;
    
    [Benchmark]
    public void MyCustomBenchmark()
    {
        var result = PerformCustomWork();
        _monitor.RecordBenchmarkMetric("Custom", "MyMetric", result, "units");
    }
}
```

### Custom Alert Rules
Implement custom alert conditions:

```csharp
public class CustomAlertRule : IAlertRule
{
    public bool ShouldTrigger(PerformanceAnalysis analysis)
    {
        // Custom logic
        return analysis.HasRegressions && analysis.RegressionCount > 5;
    }
}
```

### Integration with External Tools
The suite provides APIs for integration with external monitoring tools:

```csharp
// Export to Prometheus format
var prometheusMetrics = await reportGenerator.ExportPrometheusFormat(data);

// Export to InfluxDB
await reportGenerator.ExportInfluxDB(data, "http://influxdb:8086", "tixl");
```

## 📚 Performance Best Practices

### 1. Regular Baseline Updates
- Update baselines after significant optimizations
- Create environment-specific baselines (dev, staging, prod)
- Version control baseline files

### 2. Regression Detection
- Set appropriate thresholds (10-15% for most cases)
- Monitor trends over time, not just absolute values
- Consider seasonal patterns in performance data

### 3. CI/CD Integration
- Run benchmarks on every PR for critical paths
- Block merges on significant regressions
- Provide performance budget enforcement

### 4. Alert Management
- Configure appropriate alert channels
- Set up alert fatigue prevention (cooldowns, rate limiting)
- Regular review and tuning of alert thresholds

## 🔍 Troubleshooting

### Common Issues

**Benchmark Timeout**
```bash
# Increase timeout for long-running benchmarks
export BENCHMARK_TIMEOUT=3600  # 1 hour
dotnet run --timeout $BENCHMARK_TIMEOUT
```

**Memory Issues**
```bash
# Increase heap size for memory-intensive benchmarks
export DOTNET_GCHeapCount=4
dotnet run --categories MemoryUsage
```

**Baseline Not Found**
```bash
# Create baseline from current results
dotnet run --baseline custom-baseline
```

### Debug Mode
```bash
# Enable verbose logging
dotnet run --verbose

# Run specific benchmark with debugging
dotnet run --categories FrameTime --scenes "*Simple*"
```

## 📖 API Reference

### Core Services

**PerformanceMonitorService**
- `StartMonitoring()` - Begin performance monitoring
- `StopMonitoring()` - End monitoring and collect results
- `RecordBenchmarkMetric()` - Record individual metric
- `AnalyzeRegressions()` - Compare against baseline

**BaselineManager**
- `SaveCurrentResultsAsBaseline()` - Save current results as baseline
- `LoadBaseline()` - Load existing baseline
- `ListBaselines()` - List available baselines
- `UpdateBaseline()` - Update existing baseline

**ReportGenerator**
- `GenerateHtmlReport()` - Create HTML report
- `GenerateJsonReport()` - Create JSON report
- `GenerateCsvReport()` - Create CSV report

### Command Line Interface

```bash
TiXL Performance Benchmarking Suite (TIXL-054)
===============================================

Usage: TiXL.PerformanceSuite [options]

Options:
  --scenes, -s <patterns>       Benchmark specific scene patterns
  --categories, -c <categories> Benchmark specific categories
  --baseline, -b [name]         Create performance baseline
  --regression, -r             Run regression detection
  --report, -p [path]          Generate performance report
  --threshold <percent>        Regression threshold (default: 10.0%)
  --ci, --ci-mode              CI/CD mode (fail on regressions)
  --verbose, -v                Verbose output
  --help, -h                   Show help

Categories:
  FrameTime      - Real-time frame rate performance
  MemoryUsage    - Memory allocation and GC pressure
  ProjectLoad    - Project loading and initialization
  OperatorExec   - Operator execution performance
  GraphicsPerf   - Graphics pipeline performance
  AudioLatency   - Audio processing latency
```

## 🤝 Contributing

When adding new benchmarks:

1. Follow the existing naming convention
2. Include comprehensive documentation
3. Add appropriate metrics recording
4. Update configuration templates
5. Add integration tests

### Benchmark Development Guidelines

- Use realistic workloads that reflect actual TiXL usage
- Include both baseline and optimized implementations
- Provide clear metric descriptions and units
- Consider different hardware configurations
- Test with various data sizes and complexity levels

## 📝 Changelog

### v1.0.0 (TIXL-054)
- Initial release of comprehensive performance benchmarking suite
- Frame time, memory, project load, and operator execution benchmarks
- Automated regression detection and alerting
- CI/CD integration scripts
- HTML, JSON, and CSV reporting
- Baseline management system
- Trend analysis and insights

---

**TiXL Performance Benchmarking Suite** - Ensuring consistent, optimized performance through comprehensive monitoring and automated regression detection.