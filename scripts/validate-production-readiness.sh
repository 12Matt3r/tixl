#!/bin/bash

###############################################################################
# TiXL Production Readiness Validation Script
# 
# This script validates that all TiXL components are production-ready by running
# comprehensive tests across all critical areas:
# - Error handling and recovery mechanisms
# - Resource cleanup and disposal patterns  
# - Logging and monitoring integration
# - Configuration validation and startup scenarios
# - Graceful shutdown and cleanup procedures
# - Performance under sustained load
# - Security and input validation
# - Configuration management
###############################################################################

set -e  # Exit on any error

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORKSPACE_DIR="$(dirname "$SCRIPT_DIR")"
TEST_RESULTS_DIR="$WORKSPACE_DIR/TestResults"
VALIDATION_REPORT_DIR="$WORKSPACE_DIR/validation-reports"
TIMESTAMP=$(date '+%Y%m%d_%H%M%S')

# Validation counters
TOTAL_TESTS=0
PASSED_TESTS=0
FAILED_TESTS=0
WARNING_TESTS=0

###############################################################################
# Utility Functions
###############################################################################

print_header() {
    echo -e "${BLUE}========================================${NC}"
    echo -e "${BLUE} TiXL Production Readiness Validation ${NC}"
    echo -e "${BLUE}========================================${NC}"
    echo "Started at: $(date)"
    echo "Workspace: $WORKSPACE_DIR"
    echo ""
}

print_section() {
    echo -e "${YELLOW}>>> $1${NC}"
}

print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠ $1${NC}"
}

log_result() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1" | tee -a "$VALIDATION_REPORT_DIR/validation-$TIMESTAMP.log"
}

setup_directories() {
    mkdir -p "$TEST_RESULTS_DIR"
    mkdir -p "$VALIDATION_REPORT_DIR"
    print_success "Setup validation directories"
}

check_prerequisites() {
    print_section "Checking Prerequisites"
    
    local missing_tools=()
    
    # Check for dotnet
    if ! command -v dotnet &> /dev/null; then
        missing_tools+=("dotnet")
    else
        print_success "dotnet CLI found: $(dotnet --version)"
    fi
    
    # Check for git
    if ! command -v git &> /dev/null; then
        missing_tools+=("git")
    else
        print_success "git found: $(git --version)"
    fi
    
    # Check for required environment variables
    if [[ -z "$DOTNET_ROOT" ]]; then
        print_warning "DOTNET_ROOT not set, may cause issues with some operations"
    fi
    
    if [[ ${#missing_tools[@]} -gt 0 ]]; then
        print_error "Missing required tools: ${missing_tools[*]}"
        exit 1
    fi
    
    print_success "All prerequisites satisfied"
}

###############################################################################
# Test Execution Functions  
###############################################################################

run_production_readiness_tests() {
    print_section "Running Production Readiness Tests"
    
    cd "$WORKSPACE_DIR"
    
    # Build the test project
    print_header "Building Test Project"
    if dotnet build Tests/TiXL.Tests.csproj --configuration Release --no-restore > "$VALIDATION_REPORT_DIR/build-$TIMESTAMP.log" 2>&1; then
        print_success "Test project built successfully"
    else
        print_error "Test project build failed"
        cat "$VALIDATION_REPORT_DIR/build-$TIMESTAMP.log"
        return 1
    fi
    
    # Run production readiness tests
    print_header "Executing Production Tests"
    local test_output="$TEST_RESULTS_DIR/production-tests-$TIMESTAMP.xml"
    local test_results="$TEST_RESULTS_DIR/production-tests-results-$TIMESTAMP.txt"
    
    if dotnet test Tests/TiXL.Tests.csproj \
        --configuration Release \
        --no-build \
        --filter "Category=Production" \
        --logger "trx;LogFileName=$test_output" \
        --logger "console;verbosity=detailed" \
        --results-directory "$TEST_RESULTS_DIR" \
        > "$test_results" 2>&1; then
        print_success "Production tests completed successfully"
        parse_test_results "$test_results"
    else
        print_error "Production tests failed"
        parse_test_results "$test_results"
        return 1
    fi
}

run_performance_benchmarks() {
    print_section "Running Performance Benchmarks"
    
    cd "$WORKSPACE_DIR"
    
    # Run performance benchmarks
    local benchmark_results="$VALIDATION_REPORT_DIR/benchmarks-$TIMESTAMP.json"
    
    if dotnet run --project Benchmarks/TiXL.Benchmarks.csproj \
        --configuration Release \
        --filter "*Production*" \
        --exporters json \
        --output "$benchmark_results" > /dev/null 2>&1; then
        print_success "Performance benchmarks completed"
        analyze_benchmark_results "$benchmark_results"
    else
        print_warning "Performance benchmarks had issues"
        analyze_benchmark_results "$benchmark_results"
    fi
}

run_security_validation() {
    print_section "Running Security Validation"
    
    local security_results="$VALIDATION_REPORT_DIR/security-$TIMESTAMP.txt"
    
    # Run security tests
    if dotnet test Tests/TiXL.Tests.csproj \
        --configuration Release \
        --filter "Category=Security" \
        --logger "console;verbosity=detailed" > "$security_results" 2>&1; then
        print_success "Security validation completed"
        grep -E "(Passed|Failed|Error)" "$security_results" | head -10
    else
        print_warning "Security validation had issues"
        cat "$security_results"
    fi
}

run_resource_cleanup_validation() {
    print_section "Validating Resource Cleanup"
    
    local cleanup_results="$VALIDATION_REPORT_DIR/cleanup-$TIMESTAMP.txt"
    
    # Run resource cleanup tests
    if dotnet test Tests/TiXL.Tests.csproj \
        --configuration Release \
        --filter "Category=Production.Disposal" \
        --logger "console;verbosity=detailed" > "$cleanup_results" 2>&1; then
        print_success "Resource cleanup validation completed"
        grep -E "(Passed|Failed)" "$cleanup_results"
    else
        print_warning "Resource cleanup validation had issues"
        cat "$cleanup_results"
    fi
}

run_configuration_validation() {
    print_section "Validating Configuration"
    
    local config_results="$VALIDATION_REPORT_DIR/config-$TIMESTAMP.txt"
    
    # Test configuration loading and validation
    if dotnet test Tests/TiXL.Tests.csproj \
        --configuration Release \
        --filter "Category=Production.Configuration" \
        --logger "console;verbosity=detailed" > "$config_results" 2>&1; then
        print_success "Configuration validation completed"
        grep -E "(Passed|Failed)" "$config_results"
    else
        print_warning "Configuration validation had issues"
        cat "$config_results"
    fi
}

###############################################################################
# Analysis Functions
###############################################################################

parse_test_results() {
    local results_file="$1"
    
    if [[ ! -f "$results_file" ]]; then
        return 1
    fi
    
    # Count test results (basic parsing)
    local passed=$(grep -c "Passed" "$results_file" || echo "0")
    local failed=$(grep -c "Failed" "$results_file" || echo "0")
    local total=$(grep -c "test.*:" "$results_file" || echo "0")
    
    TOTAL_TESTS=$((TOTAL_TESTS + total))
    PASSED_TESTS=$((PASSED_TESTS + passed))
    FAILED_TESTS=$((FAILED_TESTS + failed))
    
    echo "  Tests: $total, Passed: $passed, Failed: $failed"
}

analyze_benchmark_results() {
    local benchmark_file="$1"
    
    if [[ ! -f "$benchmark_file" ]]; then
        return 1
    fi
    
    # Parse benchmark results (basic analysis)
    echo "  Benchmark analysis saved to: $benchmark_file"
    
    # Check for performance regressions
    if grep -q "Regression" "$benchmark_file" 2>/dev/null; then
        print_warning "Performance regressions detected"
    else
        print_success "No performance regressions detected"
    fi
}

###############################################################################
# Validation Framework Functions
###############################################################################

create_validation_summary() {
    print_section "Creating Validation Summary"
    
    local summary_file="$VALIDATION_REPORT_DIR/validation-summary-$TIMESTAMP.md"
    
    cat > "$summary_file" << EOF
# TiXL Production Readiness Validation Report

**Generated:** $(date)
**Timestamp:** $TIMESTAMP
**Workspace:** $WORKSPACE_DIR

## Test Results Summary

- **Total Tests:** $TOTAL_TESTS
- **Passed:** $PASSED_TESTS
- **Failed:** $FAILED_TESTS
- **Warnings:** $WARNING_TESTS

## Validation Areas

### Error Handling and Recovery
- [ ] All error paths properly handled
- [ ] Retry mechanisms working correctly
- [ ] Graceful degradation functioning
- [ ] Exception filters accurate

### Resource Management
- [ ] All resources properly disposed
- [ ] No memory leaks detected
- [ ] Cleanup errors handled gracefully
- [ ] Memory stability under load

### Logging and Monitoring
- [ ] All log levels working
- [ ] Performance monitoring active
- [ ] Alert systems functional
- [ ] Metrics collection accurate

### Configuration
- [ ] Startup validation passes
- [ ] Invalid configurations rejected
- [ ] Configuration loading works
- [ ] Environment settings applied

### Performance
- [ ] Sustained load performance acceptable
- [ ] Memory stability maintained
- [ ] Concurrent operations thread-safe
- [ ] No performance regressions

### Security
- [ ] Input validation working
- [ ] No security vulnerabilities
- [ ] Secure random generation
- [ ] Error information not leaked

## Recommendations

EOF

    print_success "Validation summary created: $summary_file"
}

generate_deployment_checklist() {
    print_section "Generating Deployment Checklist"
    
    local checklist_file="$VALIDATION_REPORT_DIR/deployment-checklist-$TIMESTAMP.md"
    
    cat > "$checklist_file" << EOF
# TiXL Production Deployment Checklist

**Generated:** $(date)
**For Environment:** [DEPLOYMENT_ENVIRONMENT]
**Version:** [VERSION_NUMBER]

## Pre-Deployment Validation

### Code Quality
- [ ] All production readiness tests pass
- [ ] Code coverage meets minimum requirements (80%)
- [ ] No security vulnerabilities detected
- [ ] Performance benchmarks within acceptable ranges
- [ ] Memory leak tests pass
- [ ] Resource cleanup tests pass

### Security
- [ ] Input validation implemented
- [ ] No hardcoded secrets or credentials
- [ ] Secure configuration practices followed
- [ ] Error messages don't leak sensitive information
- [ ] Proper exception handling for security-critical operations

### Performance
- [ ] Sustained load performance tested
- [ ] Memory usage stable under load
- [ ] Concurrent operations tested
- [ ] Frame rate targets met
- [ ] GPU utilization monitored

### Error Handling
- [ ] All error paths tested
- [ ] Retry mechanisms validated
- [ ] Graceful degradation working
- [ ] Timeout policies configured
- [ ] Exception filters accurate

### Resource Management
- [ ] All disposable resources properly disposed
- [ ] Memory leaks checked
- [ ] File handles properly closed
- [ ] Database connections cleaned up
- [ ] Network resources released

### Logging and Monitoring
- [ ] Logging configured for production
- [ ] Performance monitoring active
- [ ] Alert systems configured
- [ ] Log levels appropriate for production
- [ ] Monitoring dashboards ready

### Configuration
- [ ] Environment-specific configurations validated
- [ ] Secrets properly managed
- [ ] Configuration loading tested
- [ ] Default values appropriate
- [ ] Configuration validation working

## Deployment Steps

### 1. Pre-Deployment
- [ ] Backup current production state
- [ ] Verify deployment environment
- [ ] Check system resources
- [ ] Review change log

### 2. Deployment
- [ ] Stop production services
- [ ] Deploy new version
- [ ] Run smoke tests
- [ ] Start services gradually
- [ ] Monitor initial operations

### 3. Post-Deployment
- [ ] Run full test suite
- [ ] Monitor performance metrics
- [ ] Check error rates
- [ ] Verify all features working
- [ ] Update monitoring dashboards

### 4. Rollback Plan
- [ ] Rollback procedure documented
- [ ] Previous version backups available
- [ ] Rollback testing completed
- [ ] Communication plan ready

## Monitoring Points

### Performance Metrics
- [ ] CPU usage < 80%
- [ ] Memory usage < 85%
- [ ] Frame rate >= 60 FPS
- [ ] Response time < 100ms
- [ ] Error rate < 0.1%

### Operational Metrics
- [ ] Log file sizes manageable
- [ ] Disk space adequate
- [ ] Network connectivity stable
- [ ] Dependencies responsive
- [ ] Database performance acceptable

## Troubleshooting Guide

### Common Issues
1. **High Memory Usage**
   - Check for memory leaks
   - Verify resource cleanup
   - Monitor garbage collection

2. **Performance Degradation**
   - Review performance metrics
   - Check for resource contention
   - Validate concurrent operations

3. **Error Rate Increase**
   - Review error logs
   - Check configuration changes
   - Validate external dependencies

## Contact Information

- **Technical Lead:** [NAME]
- **DevOps Lead:** [NAME]  
- **Security Lead:** [NAME]
- **On-Call Engineer:** [CONTACT_INFO]

EOF

    print_success "Deployment checklist generated: $checklist_file"
}

###############################################################################
# Main Execution
###############################################################################

main() {
    print_header
    setup_directories
    check_prerequisites
    
    log_result "Starting TiXL production readiness validation"
    
    # Run validation tests
    run_production_readiness_tests || {
        log_result "Production readiness tests failed"
        print_error "Production readiness validation failed"
    }
    
    run_performance_benchmarks
    run_security_validation  
    run_resource_cleanup_validation
    run_configuration_validation
    
    # Generate reports
    create_validation_summary
    generate_deployment_checklist
    
    # Final results
    print_header "Validation Complete"
    echo "Test Results:"
    echo "  Total: $TOTAL_TESTS"
    echo "  Passed: $PASSED_TESTS"  
    echo "  Failed: $FAILED_TESTS"
    echo "  Warnings: $WARNING_TESTS"
    echo ""
    echo "Reports generated in: $VALIDATION_REPORT_DIR"
    echo "Timestamp: $TIMESTAMP"
    
    if [[ $FAILED_TESTS -eq 0 ]]; then
        print_success "Production readiness validation PASSED"
        exit 0
    else
        print_error "Production readiness validation FAILED with $FAILED_TESTS failures"
        exit 1
    fi
}

# Parse command line arguments
case "${1:-}" in
    --help|-h)
        echo "TiXL Production Readiness Validation Script"
        echo ""
        echo "Usage: $0 [OPTIONS]"
        echo ""
        echo "Options:"
        echo "  --help, -h          Show this help message"
        echo "  --tests-only        Run only production readiness tests"
        echo "  --benchmarks-only   Run only performance benchmarks"
        echo "  --security-only     Run only security validation"
        echo "  --cleanup-only      Run only resource cleanup validation"
        echo "  --config-only       Run only configuration validation"
        echo "  --quiet             Suppress verbose output"
        echo ""
        echo "This script validates that all TiXL components are production-ready"
        echo "by running comprehensive tests across critical areas."
        exit 0
        ;;
    --tests-only)
        check_prerequisites
        run_production_readiness_tests
        exit 0
        ;;
    --benchmarks-only) 
        check_prerequisites
        run_performance_benchmarks
        exit 0
        ;;
    --security-only)
        check_prerequisites
        run_security_validation
        exit 0
        ;;
    --cleanup-only)
        check_prerequisites
        run_resource_cleanup_validation
        exit 0
        ;;
    --config-only)
        check_prerequisites
        run_configuration_validation
        exit 0
        ;;
    --quiet)
        exec 1>/dev/null  # Suppress output if quiet mode
        ;;
esac

# Run main validation
main "$@"