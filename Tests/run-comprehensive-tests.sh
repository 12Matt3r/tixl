#!/bin/bash

# TiXL Comprehensive Test Suite Runner
# This script runs all unit tests for the TiXL project

set -e

echo "=========================================="
echo "TiXL Comprehensive Unit Test Suite"
echo "=========================================="
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Test categories
CATEGORIES=(
    "Graphics.DirectX"
    "Performance"
    "NodeGraph"
    "IO"
    "Quality"
    "Integration"
    "PerformanceRegression"
)

# Function to run tests for a category
run_category_tests() {
    local category=$1
    local filter="Category=$category"
    
    echo -e "${BLUE}Running $category tests...${NC}"
    
    # Run tests with coverage
    if dotnet test Tests/ --filter "$filter" --verbosity normal --logger "console;verbosity=detailed"; then
        echo -e "${GREEN}✓ $category tests passed${NC}"
        return 0
    else
        echo -e "${RED}✗ $category tests failed${NC}"
        return 1
    fi
}

# Function to run performance tests
run_performance_tests() {
    echo -e "${BLUE}Running Performance Regression Tests...${NC}"
    
    if dotnet test Tests/PerformanceRegression/ --verbosity normal; then
        echo -e "${GREEN}✓ Performance regression tests passed${NC}"
        return 0
    else
        echo -e "${RED}✗ Performance regression tests failed${NC}"
        return 1
    fi
}

# Function to run integration tests
run_integration_tests() {
    echo -e "${BLUE}Running Integration Tests...${NC}"
    
    if dotnet test Tests/Integration/ --verbosity normal; then
        echo -e "${GREEN}✓ Integration tests passed${NC}"
        return 0
    else
        echo -e "${RED}✗ Integration tests failed${NC}"
        return 1
    fi
}

# Function to run all tests with coverage
run_all_tests_with_coverage() {
    echo -e "${BLUE}Running All Tests with Coverage...${NC}"
    
    if dotnet test Tests/ \
        /p:CollectCoverage=true \
        /p:CoverletOutputFormat=cobertura \
        /p:CoverletOutput=./coverage/ \
        --verbosity normal; then
        echo -e "${GREEN}✓ All tests passed with coverage report${NC}"
        return 0
    else
        echo -e "${RED}✗ Some tests failed${NC}"
        return 1
    fi
}

# Function to generate coverage report
generate_coverage_report() {
    echo -e "${BLUE}Generating Coverage Report...${NC}"
    
    if [ -f "coverage/coverage.cobertura.xml" ]; then
        # Install report generator if not available
        if ! dotnet tool list -g | grep -q "dotnet-reportgenerator-globaltool"; then
            echo "Installing ReportGenerator..."
            dotnet tool install -g dotnet-reportgenerator-globaltool
        fi
        
        reportgenerator "-reports:coverage/coverage.cobertura.xml" "-targetdir:coverage/report" "-reporttypes:Html"
        echo -e "${GREEN}✓ Coverage report generated in coverage/report/index.html${NC}"
    else
        echo -e "${YELLOW}⚠ Coverage file not found, skipping report generation${NC}"
    fi
}

# Function to run specific test file
run_specific_test() {
    local test_file=$1
    echo -e "${BLUE}Running specific test: $test_file${NC}"
    
    if dotnet test "$test_file" --verbosity normal; then
        echo -e "${GREEN}✓ $test_file tests passed${NC}"
        return 0
    else
        echo -e "${RED}✗ $test_file tests failed${NC}"
        return 1
    fi
}

# Function to check test prerequisites
check_prerequisites() {
    echo "Checking prerequisites..."
    
    # Check if dotnet is available
    if ! command -v dotnet &> /dev/null; then
        echo -e "${RED}Error: dotnet CLI is not installed or not in PATH${NC}"
        exit 1
    fi
    
    # Check if test project exists
    if [ ! -f "Tests/TiXL.Tests.csproj" ]; then
        echo -e "${RED}Error: Test project not found at Tests/TiXL.Tests.csproj${NC}"
        exit 1
    fi
    
    echo -e "${GREEN}✓ Prerequisites check passed${NC}"
}

# Function to clean test results
clean_test_results() {
    echo "Cleaning previous test results..."
    rm -rf TestResults/ coverage/
    echo -e "${GREEN}✓ Cleaned test results${NC}"
}

# Function to run stress tests
run_stress_tests() {
    echo -e "${BLUE}Running Stress Tests...${NC}"
    echo -e "${YELLOW}Note: Stress tests may take several minutes${NC}"
    
    # Run with longer timeout and less verbose output
    if timeout 600 dotnet test Tests/ --filter "Category=Stress" --verbosity minimal; then
        echo -e "${GREEN}✓ Stress tests passed${NC}"
        return 0
    else
        echo -e "${RED}✗ Stress tests failed${NC}"
        return 1
    fi
}

# Main execution
main() {
    local command=${1:-"all"}
    
    case $command in
        "all")
            check_prerequisites
            clean_test_results
            
            echo "Running comprehensive test suite..."
            echo ""
            
            failed_tests=()
            
            # Run each category
            for category in "${CATEGORIES[@]}"; do
                if ! run_category_tests "$category"; then
                    failed_tests+=("$category")
                fi
                echo ""
            done
            
            # Run integration tests
            if ! run_integration_tests; then
                failed_tests+=("Integration")
            fi
            echo ""
            
            # Run performance regression tests
            if ! run_performance_tests; then
                failed_tests+=("PerformanceRegression")
            fi
            echo ""
            
            # Summary
            echo "=========================================="
            echo "Test Execution Summary"
            echo "=========================================="
            
            if [ ${#failed_tests[@]} -eq 0 ]; then
                echo -e "${GREEN}✓ All tests passed!${NC}"
                generate_coverage_report
                exit 0
            else
                echo -e "${RED}✗ The following test categories failed:${NC}"
                for failed in "${failed_tests[@]}"; do
                    echo "  - $failed"
                done
                exit 1
            fi
            ;;
            
        "coverage")
            check_prerequisites
            clean_test_results
            
            echo "Running all tests with coverage..."
            if run_all_tests_with_coverage; then
                generate_coverage_report
                exit 0
            else
                echo -e "${RED}✗ Some tests failed${NC}"
                exit 1
            fi
            ;;
            
        "performance")
            check_prerequisites
            echo "Running performance tests..."
            if run_performance_tests; then
                exit 0
            else
                exit 1
            fi
            ;;
            
        "integration")
            check_prerequisites
            echo "Running integration tests..."
            if run_integration_tests; then
                exit 0
            else
                exit 1
            fi
            ;;
            
        "stress")
            check_prerequisites
            run_stress_tests
            ;;
            
        "specific")
            if [ -z "$2" ]; then
                echo "Usage: $0 specific <test_file_path>"
                exit 1
            fi
            check_prerequisites
            run_specific_test "$2"
            ;;
            
        "clean")
            clean_test_results
            ;;
            
        "help"|"-h"|"--help")
            echo "TiXL Comprehensive Test Suite Runner"
            echo ""
            echo "Usage: $0 [command]"
            echo ""
            echo "Commands:"
            echo "  all             Run all test categories (default)"
            echo "  coverage        Run all tests with coverage report"
            echo "  performance     Run performance regression tests only"
            echo "  integration     Run integration tests only"
            echo "  stress          Run stress tests (takes several minutes)"
            echo "  specific <file> Run specific test file"
            echo "  clean           Clean test results"
            echo "  help            Show this help message"
            echo ""
            echo "Examples:"
            echo "  $0                      # Run all tests"
            echo "  $0 coverage             # Run with coverage"
            echo "  $0 specific Tests/Graphics/DirectX/AllDirectXTests.cs"
            echo ""
            ;;
            
        *)
            echo -e "${RED}Unknown command: $command${NC}"
            echo "Use '$0 help' for usage information"
            exit 1
            ;;
    esac
}

# Run main function with all arguments
main "$@"
