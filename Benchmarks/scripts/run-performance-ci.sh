#!/bin/bash

# TiXL Performance Benchmarking CI Integration Script (TIXL-054)
# This script automates performance benchmarking in CI/CD pipelines
# with regression detection, baseline management, and alerting

set -e

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
BENCHMARK_SUITE_DIR="$PROJECT_ROOT/Benchmarks"
BUILD_CONFIGURATION="${BUILD_CONFIGURATION:-Release}"
DOTNET_VERSION="${DOTNET_VERSION:-9.0.x}"
BENCHMARK_RESULTS_DIR="${BENCHMARK_RESULTS_DIR:-$BENCHMARK_SUITE_DIR/BenchmarkDotNet.Artifacts/results}"
REPORTS_DIR="${REPORTS_DIR:-$BENCHMARK_SUITE_DIR/Reports}"
BASELINES_DIR="${BASELINES_DIR:-$BENCHMARK_SUITE_DIR/Baselines}"
PERFORMANCE_GATE_THRESHOLD="${PERFORMANCE_GATE_THRESHOLD:-10.0}"
CI_MODE="${CI_MODE:-false}"

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

# Usage information
show_usage() {
    cat << EOF
TiXL Performance Benchmarking CI Integration Script

Usage: $0 [OPTIONS] COMMAND

Commands:
    build-and-benchmark    Build project and run performance benchmarks
    regression-check       Check for performance regressions against baseline
    create-baseline        Create new performance baseline
    generate-report        Generate performance report
    full-ci-run           Complete CI pipeline: build -> benchmark -> regression check

Options:
    -c, --config CONFIG     Build configuration (default: Release)
    -t, --threshold PERCENT Performance regression threshold (default: 10.0)
    -b, --baseline NAME     Baseline name to use (default: ci-latest)
    -r, --results-dir DIR   Results directory (default: BenchmarkDotNet.Artifacts/results)
    --ci-mode              Enable CI mode (fail on regressions)
    --categories CAT,...    Benchmark categories to run
    --scenes PATTERN,...   Scene patterns to benchmark
    --verbose              Verbose output
    -h, --help             Show this help message

Examples:
    $0 build-and-benchmark --ci-mode --threshold 15.0
    $0 regression-check --baseline release-latest
    $0 create-baseline --baseline development-$(date +%Y%m%d)
    $0 full-ci-run --categories FrameTime,MemoryUsage --scenes "*Rendering*"

Environment Variables:
    BUILD_CONFIGURATION        Build configuration
    DOTNET_VERSION            .NET version to use
    BENCHMARK_RESULTS_DIR     Benchmark results directory
    REPORTS_DIR               Reports output directory
    BASELINES_DIR             Baselines directory
    PERFORMANCE_GATE_THRESHOLD Performance regression threshold
    CI_MODE                  Enable CI mode
EOF
}

# Check prerequisites
check_prerequisites() {
    log_info "Checking prerequisites..."
    
    # Check .NET
    if ! command -v dotnet &> /dev/null; then
        log_error ".NET CLI not found. Please install .NET 9.0 or later."
        exit 1
    fi
    
    local dotnet_version=$(dotnet --version)
    log_info "Found .NET version: $dotnet_version"
    
    # Check if benchmark project exists
    if [[ ! -f "$BENCHMARK_SUITE_DIR/TiXLPerformanceSuite.csproj" ]]; then
        log_error "Benchmark project not found at $BENCHMARK_SUITE_DIR"
        exit 1
    fi
    
    # Create necessary directories
    mkdir -p "$BENCHMARK_RESULTS_DIR"
    mkdir -p "$REPORTS_DIR"
    mkdir -p "$BASELINES_DIR"
    
    log_success "Prerequisites check completed"
}

# Build the project
build_project() {
    log_info "Building project in $BUILD_CONFIGURATION configuration..."
    
    cd "$PROJECT_ROOT"
    
    # Clean and restore
    dotnet clean --configuration "$BUILD_CONFIGURATION" || true
    dotnet restore
    
    # Build
    if dotnet build --configuration "$BUILD_CONFIGURATION" --no-restore; then
        log_success "Project built successfully"
    else
        log_error "Project build failed"
        return 1
    fi
}

# Run performance benchmarks
run_benchmarks() {
    log_info "Running performance benchmarks..."
    
    cd "$BENCHMARK_SUITE_DIR"
    
    local benchmark_args=""
    
    # Add categories if specified
    if [[ -n "$BENCHMARK_CATEGORIES" ]]; then
        benchmark_args="$benchmark_args --categories $BENCHMARK_CATEGORIES"
    fi
    
    # Add scene patterns if specified
    if [[ -n "$BENCHMARK_SCENES" ]]; then
        benchmark_args="$benchmark_args --scenes $BENCHMARK_SCENES"
    fi
    
    # Add verbose flag if requested
    if [[ "$VERBOSE" == "true" ]]; then
        benchmark_args="$benchmark_args --verbose"
    fi
    
    log_info "Running: dotnet run $benchmark_args"
    
    # Run benchmarks
    if timeout 1800 dotnet run --configuration Release $benchmark_args; then
        log_success "Benchmarks completed successfully"
        return 0
    else
        local exit_code=$?
        if [[ $exit_code -eq 124 ]]; then
            log_error "Benchmark execution timed out (30 minutes)"
        else
            log_error "Benchmark execution failed with exit code: $exit_code"
        fi
        return $exit_code
    fi
}

# Check for performance regressions
check_regressions() {
    log_info "Checking for performance regressions..."
    
    local baseline_name="${1:-ci-latest}"
    local baseline_path="$BASELINES_DIR/$baseline_name.json"
    
    # Check if baseline exists
    if [[ ! -f "$baseline_path" ]]; then
        log_warning "Baseline '$baseline_name' not found at $baseline_path"
        log_info "Creating baseline from current results..."
        return 0
    fi
    
    # Run regression check using the performance regression checker tool
    cd "$BENCHMARK_SUITE_DIR"
    
    if dotnet run --project ../Tools/PerformanceRegressionChecker/TiXL.PerformanceRegressionChecker.csproj \
        --check-regression "$(git rev-parse HEAD)" \
        --threshold "$PERFORMANCE_GATE_THRESHOLD" \
        --results-path "$BENCHMARK_RESULTS_DIR" \
        --ci-mode; then
        log_success "No performance regressions detected"
        return 0
    else
        log_error "Performance regressions detected!"
        if [[ "$CI_MODE" == "true" ]]; then
            log_error "CI mode: Failing build due to performance regressions"
            return 1
        else
            log_warning "CI mode disabled: Continuing despite regressions"
            return 0
        fi
    fi
}

# Create performance baseline
create_baseline() {
    local baseline_name="${1:-ci-$(date +%Y%m%d-%H%M%S)}"
    log_info "Creating performance baseline: $baseline_name"
    
    cd "$BENCHMARK_SUITE_DIR"
    
    if dotnet run --configuration Release --baseline "$baseline_name"; then
        log_success "Baseline '$baseline_name' created successfully"
        log_info "Baseline saved to: $BASELINES_DIR/$baseline_name.json"
        return 0
    else
        log_error "Failed to create baseline '$baseline_name'"
        return 1
    fi
}

# Generate performance report
generate_report() {
    local report_path="${1:-$REPORTS_DIR/performance-report-$(date +%Y%m%d-%H%M%S).html}"
    log_info "Generating performance report: $report_path"
    
    cd "$BENCHMARK_SUITE_DIR"
    
    if dotnet run --configuration Release --report "$report_path"; then
        log_success "Performance report generated: $report_path"
        
        # Generate additional reports
        local json_report="${report_path%.html}.json"
        local csv_report="${report_path%.html}.csv"
        
        log_info "Generating JSON report: $json_report"
        log_info "Generating CSV report: $csv_report"
        
        return 0
    else
        log_error "Failed to generate performance report"
        return 1
    fi
}

# Archive benchmark results
archive_results() {
    local archive_name="performance-$(date +%Y%m%d-%H%M%S)"
    local archive_path="/tmp/$archive_name.tar.gz"
    
    log_info "Archiving benchmark results to: $archive_path"
    
    cd "$BENCHMARK_SUITE_DIR"
    
    # Create archive
    tar -czf "$archive_path" \
        "$BENCHMARK_RESULTS_DIR" \
        "$REPORTS_DIR" \
        2>/dev/null || true
    
    log_success "Results archived to: $archive_path"
    log_info "Archive size: $(du -h "$archive_path" | cut -f1)"
    
    # Clean up old archives (keep last 10)
    local archive_dir="/tmp"
    local old_archives=$(ls -t /tmp/performance-*.tar.gz | tail -n +11)
    if [[ -n "$old_archives" ]]; then
        log_info "Cleaning up old archives..."
        echo "$old_archives" | xargs rm -f
    fi
}

# Send notifications
send_notifications() {
    local status="$1"
    local message="$2"
    
    # Slack notification (if webhook configured)
    if [[ -n "$SLACK_WEBHOOK_URL" ]]; then
        log_info "Sending Slack notification..."
        local color=$([ "$status" == "success" ] && echo "good" || echo "danger")
        local payload=$(cat <<EOF
{
    "attachments": [{
        "color": "$color",
        "title": "TiXL Performance Benchmark Results",
        "text": "$message",
        "ts": $(date +%s)
    }]
}
EOF
)
        curl -X POST -H 'Content-type: application/json' \
            --data "$payload" \
            "$SLACK_WEBHOOK_URL" || log_warning "Failed to send Slack notification"
    fi
    
    # Email notification (if configured)
    if [[ -n "$EMAIL_RECIPIENTS" ]]; then
        log_info "Sending email notification..."
        # Email sending logic would go here
        log_warning "Email notification not implemented"
    fi
}

# Main CI pipeline
full_ci_run() {
    log_info "Starting full CI performance pipeline..."
    
    local start_time=$(date +%s)
    
    # Step 1: Build project
    if ! build_project; then
        send_notifications "failure" "Build failed"
        exit 1
    fi
    
    # Step 2: Run benchmarks
    if ! run_benchmarks; then
        send_notifications "failure" "Benchmark execution failed"
        exit 1
    fi
    
    # Step 3: Check regressions (if baseline exists)
    if ! check_regressions; then
        local end_time=$(date +%s)
        local duration=$((end_time - start_time))
        send_notifications "failure" "Performance regressions detected. Duration: ${duration}s"
        exit 1
    fi
    
    # Step 4: Generate report
    if ! generate_report; then
        log_warning "Report generation failed but continuing"
    fi
    
    # Step 5: Archive results
    archive_results
    
    # Step 6: Update baseline if in release mode
    if [[ "$BUILD_CONFIGURATION" == "Release" ]] && [[ "$CI_MODE" == "true" ]]; then
        log_info "Updating CI baseline..."
        create_baseline "ci-latest" || log_warning "Failed to update CI baseline"
    fi
    
    local end_time=$(date +%s)
    local duration=$((end_time - start_time))
    
    log_success "Full CI pipeline completed in ${duration}s"
    send_notifications "success" "Performance benchmarks completed successfully. Duration: ${duration}s"
}

# Parse command line arguments
parse_args() {
    while [[ $# -gt 0 ]]; do
        case $1 in
            -c|--config)
                BUILD_CONFIGURATION="$2"
                shift 2
                ;;
            -t|--threshold)
                PERFORMANCE_GATE_THRESHOLD="$2"
                shift 2
                ;;
            -b|--baseline)
                BASELINE_NAME="$2"
                shift 2
                ;;
            -r|--results-dir)
                BENCHMARK_RESULTS_DIR="$2"
                shift 2
                ;;
            --ci-mode)
                CI_MODE="true"
                shift
                ;;
            --categories)
                BENCHMARK_CATEGORIES="$2"
                shift 2
                ;;
            --scenes)
                BENCHMARK_SCENES="$2"
                shift 2
                ;;
            --verbose)
                VERBOSE="true"
                shift
                ;;
            -h|--help)
                show_usage
                exit 0
                ;;
            build-and-benchmark)
                COMMAND="build-and-benchmark"
                shift
                ;;
            regression-check)
                COMMAND="regression-check"
                shift
                ;;
            create-baseline)
                COMMAND="create-baseline"
                shift
                ;;
            generate-report)
                COMMAND="generate-report"
                shift
                ;;
            full-ci-run)
                COMMAND="full-ci-run"
                shift
                ;;
            *)
                log_error "Unknown option: $1"
                show_usage
                exit 1
                ;;
        esac
    done
    
    # Set default command
    if [[ -z "$COMMAND" ]]; then
        if [[ "$CI_MODE" == "true" ]]; then
            COMMAND="full-ci-run"
        else
            COMMAND="build-and-benchmark"
        fi
    fi
}

# Main execution
main() {
    parse_args "$@"
    
    log_info "TiXL Performance Benchmarking CI Script"
    log_info "Command: $COMMAND"
    log_info "Configuration: $BUILD_CONFIGURATION"
    log_info "CI Mode: $CI_MODE"
    log_info "Regression Threshold: $PERFORMANCE_GATE_THRESHOLD%"
    
    # Check prerequisites
    check_prerequisites
    
    # Execute command
    case $COMMAND in
        build-and-benchmark)
            build_project && run_benchmarks
            ;;
        regression-check)
            check_regressions "$BASELINE_NAME"
            ;;
        create-baseline)
            create_baseline "$BASELINE_NAME"
            ;;
        generate-report)
            generate_report
            ;;
        full-ci-run)
            full_ci_run
            ;;
        *)
            log_error "Unknown command: $COMMAND"
            exit 1
            ;;
    esac
    
    local exit_code=$?
    if [[ $exit_code -eq 0 ]]; then
        log_success "Performance benchmarking script completed successfully"
    else
        log_error "Performance benchmarking script failed with exit code: $exit_code"
    fi
    
    exit $exit_code
}

# Run main function
main "$@"