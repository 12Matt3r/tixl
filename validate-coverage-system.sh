#!/bin/bash

# TiXL Code Coverage System Validation Script
# Validates that all coverage system components are properly installed

echo "=========================================="
echo "TiXL Coverage System Validation"
echo "=========================================="
echo ""

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

total_checks=0
passed_checks=0

# Function to check file existence
check_file() {
    local file_path=$1
    local description=$2
    
    total_checks=$((total_checks + 1))
    
    if [ -f "$file_path" ]; then
        echo -e "${GREEN}✓${NC} $description"
        echo "  File: $file_path"
        passed_checks=$((passed_checks + 1))
    else
        echo -e "${RED}✗${NC} $description"
        echo "  Missing: $file_path"
    fi
}

# Function to check directory existence
check_directory() {
    local dir_path=$1
    local description=$2
    
    total_checks=$((total_checks + 1))
    
    if [ -d "$dir_path" ]; then
        echo -e "${GREEN}✓${NC} $description"
        echo "  Directory: $dir_path"
        passed_checks=$((passed_checks + 1))
    else
        echo -e "${RED}✗${NC} $description"
        echo "  Missing: $dir_path"
    fi
}

# Main validation
echo "Checking Core Components..."
echo "----------------------------------------"

# Configuration files
check_file "Tests/CoverletSettings.runsettings" "Coverlet configuration"
check_file "docs/config/coverage-thresholds.config" "Coverage thresholds configuration"
check_file "docs/config/coverage-baseline.json" "Coverage baseline data"

echo ""
echo "Checking PowerShell Scripts..."
echo "----------------------------------------"

# PowerShell automation scripts
check_file "docs/scripts/coverage-analyzer.ps1" "Coverage analysis script"
check_file "docs/scripts/coverage-quality-gate.ps1" "Coverage quality gate script"
check_file "docs/scripts/generate-coverage-report.ps1" "Coverage report generation script"
check_file "docs/scripts/comprehensive-quality-gate.ps1" "Comprehensive quality gate script"
check_file "docs/scripts/initialize-coverage-system.ps1" "System initialization script"

echo ""
echo "Checking CI/CD Pipeline Integration..."
echo "----------------------------------------"

# Pipeline configurations
check_file "docs/pipelines/enhanced-coverage-pipeline.yml" "Azure DevOps pipeline"
check_file ".github/workflows/tixl-coverage-ci.yml" "GitHub Actions workflow"

echo ""
echo "Checking Documentation..."
echo "----------------------------------------"

# Documentation files
check_file "docs/TIXL-044_CODE_COVERAGE_IMPLEMENTATION.md" "Implementation guide"
check_file "docs/TIXL-044_IMPLEMENTATION_SUMMARY.md" "Implementation summary"
check_file "docs/CODE_COVERAGE_README.md" "Quick start guide"

echo ""
echo "Checking Project Structure..."
echo "----------------------------------------"

# Test project configuration
check_file "Tests/TiXL.Tests.csproj" "Test project file"
check_file "Tests/xunit.runner.json" "xUnit runner configuration"
check_file "Tests/TestSettings.runsettings" "Test settings configuration"

# Solution file
check_file "TiXL.sln" "Solution file"

# Core directories
check_directory "src/Core" "Core module directory"
check_directory "src/Operators" "Operators module directory"
check_directory "Tests" "Tests directory"

echo ""
echo "=========================================="
echo "Validation Summary"
echo "=========================================="

echo "Total checks: $total_checks"
echo "Passed: $passed_checks"
echo "Failed: $((total_checks - passed_checks))"

if [ $passed_checks -eq $total_checks ]; then
    echo -e "${GREEN}🎉 All checks passed! Coverage system is properly implemented.${NC}"
    echo ""
    echo "Next Steps:"
    echo "1. Ensure PowerShell 7+ is installed"
    echo "2. Install ReportGenerator: dotnet tool install -g ReportGenerator"
    echo "3. Run: pwsh ./docs/scripts/initialize-coverage-system.ps1"
    echo "4. Configure CI/CD pipelines to use enhanced coverage workflow"
    exit 0
else
    echo -e "${RED}❌ Some checks failed. Please review the missing components.${NC}"
    exit 1
fi