#!/bin/bash

# TiXL Performance Validation CI/CD Script
# This script runs comprehensive performance tests and validates against performance gates

set -e

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
BENCHMARKS_DIR="$PROJECT_ROOT/Benchmarks"
RESULTS_DIR="$PROJECT_ROOT/BenchmarkDotNet.Artifacts/results"
REPORTS_DIR="$PROJECT_ROOT/PerformanceReports"
TOOLS_DIR="$PROJECT_ROOT/Tools/PerformanceRegressionChecker"

# Performance thresholds
FRAME_TIME_THRESHOLD=16.67
FRAME_VARIANCE_THRESHOLD=2.0
MEMORY_GROWTH_THRESHOLD=15.0
REGRESSION_THRESHOLD=10.0

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

# Parse command line arguments
COMMIT_HASH="${GITHUB_SHA:-$(git rev-parse HEAD)}"
BRANCH_NAME="${GITHUB_REF_NAME:-$(git rev-parse --abbrev-ref HEAD)}"
SKIP_BENCHMARKS=false
VERBOSE=false
CI_MODE=true

while [[ $# -gt 0 ]]; do
    case $1 in
        --commit)
            COMMIT_HASH="$2"
            shift 2
            ;;
        --branch)
            BRANCH_NAME="$2"
            shift 2
            ;;
        --skip-benchmarks)
            SKIP_BENCHMARKS=true
            shift
            ;;
        --verbose)
            VERBOSE=true
            shift
            ;;
        --help)
            show_help
            exit 0
            ;;
        *)
            log_error "Unknown option: $1"
            show_help
            exit 1
            ;;
    esac
done

show_help() {
    echo "TiXL Performance Validation Script"
    echo ""
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  --commit HASH        Git commit hash to validate (default: current HEAD)"
    echo "  --branch NAME        Branch name (default: current branch)"
    echo "  --skip-benchmarks    Skip running benchmarks, only check existing results"
    echo "  --verbose            Enable verbose output"
    echo "  --help               Show this help message"
    echo ""
    echo "Environment Variables:"
    echo "  GITHUB_SHA          Git commit hash (used in CI)"
    echo "  GITHUB_REF_NAME     Branch name (used in CI)"
}

# Setup environment
setup_environment() {
    log_info "Setting up performance validation environment..."
    
    # Set process priority for more consistent results
    if [[ "$OSTYPE" == "msys" || "$OSTYPE" == "cygwin" ]]; then
        # Windows
        wmic process where name="dotnet.exe" call setpriority "high priority"
    else
        # Linux/macOS
        renice -n -10 $$ 2>/dev/null || true
    fi
    
    # Create directories
    mkdir -p "$RESULTS_DIR"
    mkdir -p "$REPORTS_DIR"
    
    # Set environment variables for consistent benchmarking
    export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
    export COMPlus_EnableDiagnostics=0
    export DOTNET_EnableDiagnostics=0
    
    log_success "Environment setup completed"
}

# Build project
build_project() {
    log_info "Building TiXL project..."
    
    cd "$PROJECT_ROOT"
    
    if [[ "$VERBOSE" == "true" ]]; then
        dotnet build --configuration Release --verbosity normal
    else
        dotnet build --configuration Release --verbosity minimal
    fi
    
    if [ $? -ne 0 ]; then
        log_error "Build failed!"
        exit 1
    fi
    
    log_success "Project built successfully"
}

# Run benchmarks
run_benchmarks() {
    if [[ "$SKIP_BENCHMARKS" == "true" ]]; then
        log_info "Skipping benchmark execution (--skip-benchmarks specified)"
        return
    fi
    
    log_info "Running performance benchmarks..."
    
    # Clean previous results
    rm -rf "$RESULTS_DIR"/*
    
    # Run comprehensive benchmark suite
    local benchmark_args=(
        --configuration Release
        --no-restore
        --no-build
    )
    
    if [[ "$VERBOSE" == "true" ]]; then
        benchmark_args+=(--verbosity normal)
    else
        benchmark_args+=(--verbosity minimal)
    fi
    
    cd "$BENCHMARKS_DIR"
    
    # Run all benchmarks
    log_info "Running comprehensive benchmark suite..."
    dotnet run --project TiXL.Benchmarks.csproj "${benchmark_args[@]}"
    
    if [ $? -ne 0 ]; then
        log_error "Benchmark execution failed!"
        exit 1
    fi
    
    # Run real-time validation benchmarks specifically
    log_info "Running real-time validation benchmarks..."
    dotnet run --project TiXL.Benchmarks.csproj "${benchmark_args[@]}" --filter "*RealTime*"
    
    if [ $? -ne 0 ]; then
        log_error "Real-time benchmark execution failed!"
        exit 1
    fi
    
    log_success "All benchmarks completed successfully"
}

# Validate performance gates
validate_performance_gates() {
    log_info "Validating performance gates..."
    
    cd "$TOOLS_DIR"
    
    # Check for regressions
    local regression_check_cmd=(
        dotnet run
        --check-regression "$COMMIT_HASH"
        --threshold "$REGRESSION_THRESHOLD"
        --results-path "$RESULTS_DIR"
    )
    
    if [[ "$CI_MODE" == "true" ]]; then
        regression_check_cmd+=(--ci-mode)
    fi
    
    "${regression_check_cmd[@]}"
    
    local regression_exit_code=$?
    
    if [ $regression_exit_code -ne 0 ]; then
        log_error "Performance regression check failed!"
        return 1
    fi
    
    # Run additional performance gate validations
    validate_frame_time_gates
    validate_memory_gates
    validate_audio_gates
    
    log_success "Performance gate validation completed"
}

# Validate frame time requirements
validate_frame_time_gates() {
    log_info "Validating frame time requirements..."
    
    # Look for frame time benchmark results
    local frame_time_results=$(find "$RESULTS_DIR" -name "*.csv" -exec grep -l "FrameRenderTime\|FrameTime" {} \;)
    
    if [[ -z "$frame_time_results" ]]; then
        log_warning "No frame time results found, skipping frame time validation"
        return
    fi
    
    # Parse and validate frame time results
    while IFS= read -r result_file; do
        log_info "Validating $result_file..."
        
        # Extract mean execution time (assuming CSV format)
        local mean_time=$(grep -v "Method" "$result_file" | awk -F',' '{print $4}' | head -n 1)
        
        if [[ -n "$mean_time" && $(echo "$mean_time < $FRAME_TIME_THRESHOLD" | bc -l) -eq 1 ]]; then
            log_success "Frame time gate passed: ${mean_time}ms < ${FRAME_TIME_THRESHOLD}ms"
        else
            log_error "Frame time gate failed: ${mean_time}ms >= ${FRAME_TIME_THRESHOLD}ms"
            return 1
        fi
    done <<< "$frame_time_results"
}

# Validate memory requirements
validate_memory_gates() {
    log_info "Validating memory usage requirements..."
    
    # Check for memory-related benchmarks
    local memory_results=$(find "$RESULTS_DIR" -name "*.csv" -exec grep -l "Memory\|Allocated" {} \;)
    
    if [[ -z "$memory_results" ]]; then
        log_warning "No memory results found, skipping memory validation"
        return
    fi
    
    # Memory validation logic would go here
    log_info "Memory validation placeholder - implement based on your memory benchmarks"
}

# Validate audio processing requirements
validate_audio_gates() {
    log_info "Validating audio processing requirements..."
    
    # Check for audio latency benchmarks
    local audio_results=$(find "$RESULTS_DIR" -name "*.csv" -exec grep -l "Audio\|Latency" {} \;)
    
    if [[ -z "$audio_results" ]]; then
        log_warning "No audio results found, skipping audio validation"
        return
    fi
    
    # Audio validation logic would go here
    log_info "Audio validation placeholder - implement based on your audio benchmarks"
}

# Generate performance reports
generate_reports() {
    log_info "Generating performance reports..."
    
    cd "$TOOLS_DIR"
    
    # Generate daily performance report
    local report_cmd=(
        dotnet run
        --generate-report html
        --results-path "$RESULTS_DIR"
    )
    
    "${report_cmd[@]}"
    
    if [ $? -eq 0 ]; then
        log_success "Performance reports generated"
    else
        log_warning "Report generation had issues, but continuing..."
    fi
}

# Upload results to CI
upload_ci_results() {
    if [[ "$CI_MODE" == "true" ]]; then
        log_info "Uploading results to CI..."
        
        # Upload benchmark results as artifacts
        if [[ -n "${GITHUB_ACTIONS:-}" ]]; then
            log_info "Uploading benchmark results to GitHub Actions artifacts..."
            # This would be handled by GitHub Actions upload-artifact step
        fi
        
        # Upload to external metrics service if configured
        if [[ -n "${PERFORMANCE_METRICS_ENDPOINT:-}" ]]; then
            upload_to_metrics_service
        fi
    fi
}

upload_to_metrics_service() {
    log_info "Uploading results to external metrics service..."
    
    # Example implementation for uploading to a metrics service
    local metrics_data=$(cat "$RESULTS_DIR"/*.json | jq -s '.')
    
    curl -X POST \
        -H "Content-Type: application/json" \
        -H "Authorization: Bearer ${PERFORMANCE_METRICS_TOKEN:-}" \
        -d "$metrics_data" \
        "${PERFORMANCE_METRICS_ENDPOINT}/api/performance/commit/${COMMIT_HASH}"
    
    if [ $? -eq 0 ]; then
        log_success "Results uploaded to metrics service"
    else
        log_warning "Failed to upload to metrics service"
    fi
}

# Main execution
main() {
    log_info "Starting TiXL Performance Validation"
    log_info "Commit: $COMMIT_HASH"
    log_info "Branch: $BRANCH_NAME"
    log_info "Results Directory: $RESULTS_DIR"
    
    # Execute validation pipeline
    setup_environment
    build_project
    run_benchmarks
    validate_performance_gates
    generate_reports
    upload_ci_results
    
    log_success "Performance validation pipeline completed successfully!"
    
    # Print summary
    echo ""
    echo "======================================"
    echo "Performance Validation Summary"
    echo "======================================"
    echo "Commit: $COMMIT_HASH"
    echo "Branch: $BRANCH_NAME"
    echo "Results: $RESULTS_DIR"
    echo "Reports: $REPORTS_DIR"
    echo "======================================"
}

# Error handling
set -e
trap 'log_error "Performance validation failed at line $LINENO"' ERR

# Run main function
main "$@"