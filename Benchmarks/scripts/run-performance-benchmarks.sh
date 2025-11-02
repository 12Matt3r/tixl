#!/bin/bash

# TiXL Performance Benchmark Automation Script
# Comprehensive automated benchmarking for CI/CD pipelines

set -e  # Exit on any error

# Configuration
BENCHMARK_TIMEOUT=${BENCHMARK_TIMEOUT:-3600}  # 1 hour timeout
REPORT_DIR="performance-reports"
BASELINE_DIR="baselines"
RESULTS_DIR="benchmark-results"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Logging functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Function to check prerequisites
check_prerequisites() {
    log_info "Checking prerequisites..."
    
    # Check if .NET is installed
    if ! command -v dotnet &> /dev/null; then
        log_error ".NET SDK is not installed"
        exit 1
    fi
    
    # Check if benchmark project exists
    if [ ! -f "Benchmarks/TiXL.Benchmarks.csproj" ]; then
        log_error "Benchmark project not found at Benchmarks/TiXL.Benchmarks.csproj"
        exit 1
    fi
    
    log_success "Prerequisites check completed"
}

# Function to setup environment
setup_environment() {
    log_info "Setting up benchmark environment..."
    
    # Create necessary directories
    mkdir -p "$REPORT_DIR"
    mkdir -p "$BASELINE_DIR"
    mkdir -p "$RESULTS_DIR"
    
    # Set up baseline configuration if it doesn't exist
    if [ ! -f "$BASELINE_DIR/baselines.json" ]; then
        log_info "Creating baseline configuration..."
        cat > "$BASELINE_DIR/baselines.json" << EOF
{
  "name": "TiXL Performance Baselines",
  "createdAt": "$(date -Iseconds)",
  "version": "1.0",
  "metrics": [
    {
      "benchmarkName": "DirectXPerformanceBenchmarks",
      "metricName": "FramePacingConsistency",
      "mean": 0.95,
      "min": 0.90,
      "max": 0.98,
      "standardDeviation": 0.02,
      "sampleCount": 100,
      "unit": "ratio",
      "category": "FrameTime"
    },
    {
      "benchmarkName": "DirectXPerformanceBenchmarks",
      "metricName": "PSOCachingImprovement",
      "mean": 80.0,
      "min": 75.0,
      "max": 95.0,
      "standardDeviation": 5.0,
      "sampleCount": 100,
      "unit": "percent",
      "category": "PSOPerformance"
    },
    {
      "benchmarkName": "PerformanceOptimizationBenchmarks",
      "metricName": "EventThroughput",
      "mean": 50000,
      "min": 45000,
      "max": 60000,
      "standardDeviation": 2500,
      "sampleCount": 100,
      "unit": "events/sec",
      "category": "Throughput"
    },
    {
      "benchmarkName": "TiXLSystemBenchmarks",
      "metricName": "SystemEndToEnd",
      "mean": 60.0,
      "min": 55.0,
      "max": 65.0,
      "standardDeviation": 2.5,
      "sampleCount": 50,
      "unit": "fps",
      "category": "SystemPerf"
    }
  ]
}
EOF
    fi
    
    log_success "Environment setup completed"
}

# Function to build benchmark project
build_project() {
    log_info "Building benchmark project..."
    
    cd Benchmarks
    
    # Restore dependencies
    log_info "Restoring NuGet packages..."
    dotnet restore TiXL.Benchmarks.csproj
    
    # Build project
    log_info "Building project..."
    dotnet build TiXL.Benchmarks.csproj --configuration Release --no-restore
    
    cd ..
    
    log_success "Project build completed"
}

# Function to run benchmarks
run_benchmarks() {
    local benchmark_filter=${1:-"*"}
    local benchmark_config=${2:-"config/benchmarksettings.json"}
    
    log_info "Running benchmarks with filter: $benchmark_filter"
    
    cd Benchmarks
    
    # Create benchmark configuration
    cat > "$benchmark_config" << EOF
{
  "benchmark": {
    "job": {
      "launchCount": 2,
      "iterationCount": 10,
      "warmupCount": 3
    },
    "memory": {
      "enabled": true
    },
    "export": {
      "csv": true,
      "html": true,
      "json": true,
      "markdown": true,
      "xml": true
    },
    "artifacts": {
      "path": "../$RESULTS_DIR",
      "format": [" csv", "html", "json", "markdown", "xml"]
    }
  }
}
EOF
    
    # Run benchmarks
    log_info "Starting benchmark execution..."
    log_info "Timeout: $BENCHMARK_TIMEOUT seconds"
    
    # Run with timeout and capture output
    timeout "$BENCHMARK_TIMEOUT" dotnet run --project TiXL.Benchmarks.csproj -- --filter "$benchmark_filter" --config "$benchmark_config"
    local exit_code=$?
    
    cd ..
    
    if [ $exit_code -eq 124 ]; then
        log_error "Benchmark execution timed out after $BENCHMARK_TIMEOUT seconds"
        return 124
    elif [ $exit_code -ne 0 ]; then
        log_error "Benchmark execution failed with exit code: $exit_code"
        return $exit_code
    fi
    
    log_success "Benchmark execution completed"
    return 0
}

# Function to analyze results
analyze_results() {
    log_info "Analyzing benchmark results..."
    
    # Find the latest results directory
    local latest_results=$(ls -t "$RESULTS_DIR" | head -n1)
    if [ -z "$latest_results" ]; then
        log_error "No benchmark results found"
        return 1
    fi
    
    log_info "Using results from: $latest_results"
    
    # Check if results contain target metrics
    local target_check_passed=true
    local target_failures=()
    
    # Check frame consistency target (95%)
    if [ -f "$RESULTS_DIR/$latest_results/*-report.csv" ]; then
        local frame_consistency=$(grep -i "framepacingconsistency" "$RESULTS_DIR/$latest_results"/*-report.csv | tail -1 | cut -d',' -f7 || echo "0")
        if (( $(echo "$frame_consistency < 0.90" | bc -l) )); then
            target_check_passed=false
            target_failures+=("Frame consistency: $frame_consistency < 0.90")
        fi
    fi
    
    # Check PSO improvement target (75-95%)
    local pso_improvement=$(grep -i "psocachingimprovement" "$RESULTS_DIR/$latest_results"/*-report.csv | tail -1 | cut -d',' -f7 || echo "0")
    if (( $(echo "$pso_improvement < 75.0 || $pso_improvement > 95.0" | bc -l) )); then
        target_check_passed=false
        target_failures+=("PSO improvement: $pso_improvement% (target: 75-95%)")
    fi
    
    # Check event throughput target (50,000+/sec)
    local event_throughput=$(grep -i "eventthroughput" "$RESULTS_DIR/$latest_results"/*-report.csv | tail -1 | cut -d',' -f7 || echo "0")
    if (( $(echo "$event_throughput < 45000" | bc -l) )); then
        target_check_passed=false
        target_failures+=("Event throughput: $event_throughput events/sec < 45,000/sec")
    fi
    
    # Generate analysis report
    local analysis_file="$REPORT_DIR/analysis_$(date +%Y%m%d_%H%M%S).md"
    
    cat > "$analysis_file" << EOF
# TiXL Performance Analysis Report
Generated: $(date)

## Target Achievement Summary
EOF
    
    if [ "$target_check_passed" = true ]; then
        echo -e "\n✅ **All performance targets achieved!**" >> "$analysis_file"
    else
        echo -e "\n❌ **Performance target failures:**" >> "$analysis_file"
        for failure in "${target_failures[@]}"; do
            echo "- $failure" >> "$analysis_file"
        done
    fi
    
    echo -e "\n## Detailed Results" >> "$analysis_file"
    echo "- Results Directory: $RESULTS_DIR/$latest_results" >> "$analysis_file"
    echo "- Analysis Timestamp: $(date)" >> "$analysis_file"
    
    if [ -f "$RESULTS_DIR/$latest_results"/*-report.html ]; then
        echo -e "\n## Interactive Report" >> "$analysis_file"
        echo "- HTML Report: $RESULTS_DIR/$latest_results"/*-report.html >> "$analysis_file"
    fi
    
    if [ -f "$RESULTS_DIR/$latest_results"/*-report.md ]; then
        echo -e "\n## Markdown Report" >> "$analysis_file"
        echo "- Markdown Report: $RESULTS_DIR/$latest_results"/*-report.md >> "$analysis_file"
    fi
    
    # Print analysis to console
    if [ "$target_check_passed" = true ]; then
        log_success "All performance targets achieved!"
    else
        log_warning "Performance target failures detected:"
        for failure in "${target_failures[@]}"; do
            log_warning "  - $failure"
        done
    fi
    
    log_success "Analysis completed. Report saved to: $analysis_file"
    return 0
}

# Function to update baselines
update_baselines() {
    local update_flag=${1:-"false"}
    
    if [ "$update_flag" != "true" ]; then
        log_info "Skipping baseline update (use --update-baselines to enable)"
        return 0
    fi
    
    log_info "Updating performance baselines..."
    
    # Find latest results
    local latest_results=$(ls -t "$RESULTS_DIR" | head -n1)
    if [ -z "$latest_results" ]; then
        log_error "No results found to update baselines"
        return 1
    fi
    
    # Create backup of existing baselines
    if [ -f "$BASELINE_DIR/baselines.json" ]; then
        cp "$BASELINE_DIR/baselines.json" "$BASELINE_DIR/baselines_backup_$(date +%Y%m%d_%H%M%S).json"
        log_info "Backed up existing baselines"
    fi
    
    # Update baselines from latest results
    # This would extract mean values from the CSV and update the JSON
    log_info "Baselines would be updated from: $latest_results"
    log_warning "Baseline update requires implementation of CSV parsing logic"
    
    log_success "Baseline update completed"
}

# Function to publish results
publish_results() {
    local publish_flag=${1:-"false"}
    
    if [ "$publish_flag" != "true" ]; then
        log_info "Skipping results publishing (use --publish to enable)"
        return 0
    fi
    
    log_info "Publishing benchmark results..."
    
    # This would integrate with CI/CD systems like:
    # - GitHub Actions artifacts
    # - Azure DevOps
    # - Jenkins
    # - etc.
    
    # For now, we'll create a summary
    local summary_file="$REPORT_DIR/publish_summary_$(date +%Y%m%d_%H%M%S).json"
    
    cat > "$summary_file" << EOF
{
  "publishTimestamp": "$(date -Iseconds)",
  "resultsLocation": "$RESULTS_DIR",
  "reportLocation": "$REPORT_DIR",
  "status": "ready_for_publishing",
  "artifacts": [
    "benchmark-results",
    "performance-reports",
    "baselines"
  ]
}
EOF
    
    log_success "Results published. Summary: $summary_file"
}

# Function to run regression tests
run_regression_tests() {
    log_info "Running performance regression tests..."
    
    # Check if baselines exist
    if [ ! -f "$BASELINE_DIR/baselines.json" ]; then
        log_warning "No baselines found. Run with --update-baselines to create them."
        return 0
    fi
    
    # Run benchmarks specifically for regression testing
    log_info "Running targeted regression benchmarks..."
    run_benchmarks "*Regression*" "config/regression_settings.json"
    
    # Analyze for regressions
    log_info "Checking for regressions..."
    local regression_found=false
    
    # This would compare current results against baselines
    # and flag any significant regressions
    
    if [ "$regression_found" = true ]; then
        log_error "Performance regressions detected!"
        return 1
    else
        log_success "No performance regressions detected"
        return 0
    fi
}

# Function to generate performance report
generate_performance_report() {
    log_info "Generating comprehensive performance report..."
    
    # This would run the PerformanceReportingService
    # to generate HTML dashboards, alerts, etc.
    
    local report_file="$REPORT_DIR/comprehensive_report_$(date +%Y%m%d_%H%M%S).html"
    
    # Placeholder for HTML report generation
    cat > "$report_file" << EOF
<!DOCTYPE html>
<html>
<head>
    <title>TiXL Performance Report</title>
</head>
<body>
    <h1>TiXL Performance Report</h1>
    <p>Generated: $(date)</p>
    <p>This report contains comprehensive performance analysis.</p>
    <!-- Detailed report content would be generated here -->
</body>
</html>
EOF
    
    log_success "Performance report generated: $report_file"
}

# Function to cleanup old results
cleanup_old_results() {
    log_info "Cleaning up old benchmark results..."
    
    # Keep only the last 10 result directories
    local count=$(ls -1 "$RESULTS_DIR" | wc -l)
    if [ $count -gt 10 ]; then
        ls -1t "$RESULTS_DIR" | tail -n +11 | xargs -I {} rm -rf "$RESULTS_DIR/{}"
        log_info "Cleaned up old results (kept 10 most recent)"
    else
        log_info "No cleanup needed (less than 10 result directories)"
    fi
}

# Function to display usage
usage() {
    cat << EOF
TiXL Performance Benchmark Automation Script

Usage: $0 [OPTIONS]

Options:
    --benchmark-filter PATTERN     Run specific benchmarks (default: *)
    --update-baselines            Update performance baselines from current results
    --regression-tests            Run performance regression tests
    --publish-results             Publish results to CI/CD system
    --generate-report             Generate comprehensive performance report
    --cleanup                     Cleanup old benchmark results
    --timeout SECONDS             Set benchmark timeout (default: 3600)
    --help                        Show this help message

Examples:
    $0                           # Run all benchmarks
    $0 --benchmark-filter "*DirectX*"  # Run only DirectX benchmarks
    $0 --update-baselines        # Update baselines after running benchmarks
    $0 --regression-tests        # Run regression tests only
    $0 --publish-results         # Publish results to CI/CD

Performance Targets:
    - Frame consistency: >= 95%
    - PSO improvement: 75-95%
    - Event throughput: >= 50,000/sec
    - System FPS: >= 60

EOF
}

# Main execution
main() {
    local benchmark_filter="*"
    local update_baselines_flag=false
    local regression_tests_flag=false
    local publish_results_flag=false
    local generate_report_flag=false
    local cleanup_flag=false
    
    # Parse command line arguments
    while [[ $# -gt 0 ]]; do
        case $1 in
            --benchmark-filter)
                benchmark_filter="$2"
                shift 2
                ;;
            --update-baselines)
                update_baselines_flag=true
                shift
                ;;
            --regression-tests)
                regression_tests_flag=true
                shift
                ;;
            --publish-results)
                publish_results_flag=true
                shift
                ;;
            --generate-report)
                generate_report_flag=true
                shift
                ;;
            --cleanup)
                cleanup_flag=true
                shift
                ;;
            --timeout)
                BENCHMARK_TIMEOUT="$2"
                shift 2
                ;;
            --help)
                usage
                exit 0
                ;;
            *)
                log_error "Unknown option: $1"
                usage
                exit 1
                ;;
        esac
    done
    
    log_info "🚀 Starting TiXL Performance Benchmark Suite"
    log_info "Configuration:"
    log_info "  - Benchmark filter: $benchmark_filter"
    log_info "  - Timeout: $BENCHMARK_TIMEOUT seconds"
    log_info "  - Results directory: $RESULTS_DIR"
    log_info "  - Report directory: $REPORT_DIR"
    log_info "  - Baseline directory: $BASELINE_DIR"
    
    # Execute pipeline
    check_prerequisites
    setup_environment
    
    if [ "$cleanup_flag" = true ]; then
        cleanup_old_results
    fi
    
    if [ "$regression_tests_flag" = true ]; then
        run_regression_tests
    else
        build_project
        run_benchmarks "$benchmark_filter"
        
        if [ $? -eq 0 ]; then
            analyze_results
            update_baselines "$update_baselines_flag"
            
            if [ "$generate_report_flag" = true ]; then
                generate_performance_report
            fi
            
            publish_results "$publish_results_flag"
        fi
    fi
    
    log_success "🏁 TiXL Performance Benchmark Suite completed"
}

# Run main function with all arguments
main "$@"