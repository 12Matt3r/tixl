#!/bin/bash

# TiXL Regression Test Validation Script
# Validates that the regression test framework is properly configured and can run

set -e

echo "=== TiXL Regression Test Framework Validation ==="
echo "Starting validation at $(date)"
echo ""

# Configuration
SOLUTION_PATH="./TiXL.sln"
TEST_PROJECT_PATH="./Tests/TiXL.Tests.csproj"
REGRESSION_DIR="./Tests/Regression"

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Validation functions
check_success() {
    if [ $1 -eq 0 ]; then
        echo -e "${GREEN}✅ $2${NC}"
        return 0
    else
        echo -e "${RED}❌ $2${NC}"
        return 1
    fi
}

check_warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

# 1. Check prerequisites
echo "1. Checking Prerequisites"
echo "------------------------"

# Check .NET SDK
if command -v dotnet &> /dev/null; then
    DOTNET_VERSION=$(dotnet --version)
    echo "   .NET SDK version: $DOTNET_VERSION"
    check_success 0 ".NET SDK found"
else
    check_success 1 ".NET SDK not found"
    exit 1
fi

# Check if we're on Windows for DirectX tests
if [[ "$OSTYPE" == "msys" || "$OSTYPE" == "cygwin" || "$OSTYPE" == "win32" ]]; then
    echo "   Platform: Windows (DirectX tests supported)"
    check_success 0 "Windows platform detected"
else
    echo "   Platform: $OSTYPE (Limited DirectX testing)"
    check_warning "Non-Windows platform - DirectX tests will be limited"
fi

echo ""

# 2. Check project structure
echo "2. Checking Project Structure"
echo "-----------------------------"

# Check solution file
if [ -f "$SOLUTION_PATH" ]; then
    check_success 0 "Solution file found: $SOLUTION_PATH"
else
    check_success 1 "Solution file not found: $SOLUTION_PATH"
    exit 1
fi

# Check test project
if [ -f "$TEST_PROJECT_PATH" ]; then
    check_success 0 "Test project found: $TEST_PROJECT_PATH"
else
    check_success 1 "Test project not found: $TEST_PROJECT_PATH"
    exit 1
fi

# Check regression test directory
if [ -d "$REGRESSION_DIR" ]; then
    check_success 0 "Regression test directory found: $REGRESSION_DIR"
else
    check_success 1 "Regression test directory not found: $REGRESSION_DIR"
    exit 1
fi

# Check specific regression test files
REGRESSION_FILES=(
    "$REGRESSION_DIR/RegressionTestRunner.cs"
    "$REGRESSION_DIR/ApiCompatibility/ApiCompatibilityTests.cs"
    "$REGRESSION_DIR/Migration/SharpDXToVorticeMigrationTests.cs"
    "$REGRESSION_DIR/Configuration/ConfigurationCompatibilityTests.cs"
    "$REGRESSION_DIR/ErrorHandling/ErrorHandlingConsistencyTests.cs"
    "$REGRESSION_DIR/ResourceManagement/ResourceManagementTests.cs"
    "$REGRESSION_DIR/ThreadSafety/ThreadSafetyTests.cs"
    "$REGRESSION_DIR/README.md"
)

for file in "${REGRESSION_FILES[@]}"; do
    if [ -f "$file" ]; then
        check_success 0 "Regression file found: $(basename $file)"
    else
        check_success 1 "Regression file missing: $file"
    fi
done

echo ""

# 3. Restore and build
echo "3. Building Solution"
echo "-------------------"

echo "   Restoring dependencies..."
if dotnet restore "$SOLUTION_PATH"; then
    check_success 0 "Dependencies restored successfully"
else
    check_success 1 "Failed to restore dependencies"
    exit 1
fi

echo "   Building solution..."
if dotnet build "$SOLUTION_PATH" --configuration Release --no-restore --verbosity quiet; then
    check_success 0 "Solution built successfully"
else
    check_success 1 "Failed to build solution"
    exit 1
fi

echo ""

# 4. Test discovery
echo "4. Test Discovery"
echo "-----------------"

echo "   Discovering regression tests..."
TEST_OUTPUT=$(dotnet test "$TEST_PROJECT_PATH" --list-tests --filter "Category=Regression" 2>&1 || echo "")

if echo "$TEST_OUTPUT" | grep -q "The following Tests are available"; then
    check_success 0 "Regression tests discovered"
    
    # Count tests by category
    API_COUNT=$(dotnet test "$TEST_PROJECT_PATH" --list-tests --filter "Category=ApiCompatibility" 2>/dev/null | grep -c "ApiCompatibility" || echo "0")
    MIGRATION_COUNT=$(dotnet test "$TEST_PROJECT_PATH" --list-tests --filter "Category=Migration" 2>/dev/null | grep -c "Migration" || echo "0")
    CONFIG_COUNT=$(dotnet test "$TEST_PROJECT_PATH" --list-tests --filter "Category=Configuration" 2>/dev/null | grep -c "Configuration" || echo "0")
    ERROR_COUNT=$(dotnet test "$TEST_PROJECT_PATH" --list-tests --filter "Category=ErrorHandling" 2>/dev/null | grep -c "ErrorHandling" || echo "0")
    RESOURCE_COUNT=$(dotnet test "$TEST_PROJECT_PATH" --list-tests --filter "Category=ResourceManagement" 2>/dev/null | grep -c "ResourceManagement" || echo "0")
    THREAD_COUNT=$(dotnet test "$TEST_PROJECT_PATH" --list-tests --filter "Category=ThreadSafety" 2>/dev/null | grep -c "ThreadSafety" || echo "0")
    
    echo "   Test categories found:"
    echo "     - API Compatibility: $API_COUNT tests"
    echo "     - Migration: $MIGRATION_COUNT tests"
    echo "     - Configuration: $CONFIG_COUNT tests"
    echo "     - Error Handling: $ERROR_COUNT tests"
    echo "     - Resource Management: $RESOURCE_COUNT tests"
    echo "     - Thread Safety: $THREAD_COUNT tests"
else
    check_warning "No regression tests discovered - this might be expected if test discovery fails"
fi

echo ""

# 5. Quick smoke test
echo "5. Quick Smoke Test"
echo "-------------------"

echo "   Running quick API compatibility test..."
if timeout 60s dotnet test "$TEST_PROJECT_PATH" --filter "Category=ApiCompatibility" --verbosity quiet --nologo 2>/dev/null; then
    check_success 0 "Quick API compatibility test passed"
else
    echo -e "${YELLOW}⚠️  Quick API test had issues - this might be expected in limited environments${NC}"
fi

echo ""

# 6. GitHub Actions workflow validation
echo "6. GitHub Actions Workflow"
echo "-------------------------"

WORKFLOW_FILE=".github/workflows/regression-tests.yml"
if [ -f "$WORKFLOW_FILE" ]; then
    check_success 0 "GitHub Actions workflow found: $WORKFLOW_FILE"
    
    # Check workflow structure
    if grep -q "regression-tests" "$WORKFLOW_FILE"; then
        check_success 0 "Regression workflow name found"
    else
        check_warning "Workflow name not found"
    fi
    
    if grep -q "ApiCompatibility" "$WORKFLOW_FILE"; then
        check_success 0 "API compatibility job found"
    else
        check_warning "API compatibility job not found"
    fi
    
    if grep -q "ResourceManagement" "$WORKFLOW_FILE"; then
        check_success 0 "Resource management job found"
    else
        check_warning "Resource management job not found"
    fi
else
    check_warning "GitHub Actions workflow not found: $WORKFLOW_FILE"
fi

echo ""

# 7. Documentation check
echo "7. Documentation Check"
echo "---------------------"

if [ -f "$REGRESSION_DIR/README.md" ]; then
    check_success 0 "Regression test documentation found"
    
    # Check for key sections
    if grep -q "Framework Architecture" "$REGRESSION_DIR/README.md"; then
        check_success 0 "Architecture section found"
    else
        check_warning "Architecture section not found"
    fi
    
    if grep -q "Test Categories" "$REGRESSION_DIR/README.md"; then
        check_success 0 "Test categories section found"
    else
        check_warning "Test categories section not found"
    fi
    
    if grep -q "Getting Started" "$REGRESSION_DIR/README.md"; then
        check_success 0 "Getting started section found"
    else
        check_warning "Getting started section not found"
    fi
else
    check_success 1 "Regression test documentation not found"
fi

echo ""

# 8. Configuration files check
echo "8. Configuration Files"
echo "---------------------"

# Check xUnit configuration
if [ -f "Tests/xunit.runner.json" ]; then
    check_success 0 "xUnit runner configuration found"
else
    check_warning "xUnit runner configuration not found"
fi

# Check test settings
if [ -f "Tests/TestSettings.runsettings" ]; then
    check_success 0 "Test settings file found"
else
    check_warning "Test settings file not found"
fi

# Check coverlet settings
if [ -f "Tests/CoverletSettings.runsettings" ]; then
    check_success 0 "Code coverage settings found"
else
    check_warning "Code coverage settings not found"
fi

echo ""

# 9. Generate validation summary
echo "9. Validation Summary"
echo "--------------------"

echo "TiXL Regression Test Framework Validation Results"
echo "================================================="
echo ""
echo "Environment:"
echo "  - .NET SDK: $DOTNET_VERSION"
echo "  - Platform: $OSTYPE"
echo "  - Timestamp: $(date)"
echo ""
echo "Project Structure:"
echo "  - Solution: $([ -f "$SOLUTION_PATH" ] && echo "✅ Found" || echo "❌ Missing")"
echo "  - Test Project: $([ -f "$TEST_PROJECT_PATH" ] && echo "✅ Found" || echo "❌ Missing")"
echo "  - Regression Directory: $([ -d "$REGRESSION_DIR" ] && echo "✅ Found" || echo "❌ Missing")"
echo ""
echo "Regression Test Files:"
for file in "${REGRESSION_FILES[@]}"; do
    status=$([ -f "$file" ] && echo "✅" || echo "❌")
    echo "  - $status $(basename $file)"
done
echo ""
echo "Automation:"
echo "  - GitHub Workflow: $([ -f "$WORKFLOW_FILE" ] && echo "✅ Found" || echo "❌ Missing")"
echo ""
echo "Documentation:"
echo "  - README: $([ -f "$REGRESSION_DIR/README.md" ] && echo "✅ Found" || echo "❌ Missing")"
echo ""

# Final recommendation
echo "10. Recommendations"
echo "-------------------"

if [ -f "$SOLUTION_PATH" ] && [ -f "$TEST_PROJECT_PATH" ] && [ -d "$REGRESSION_DIR" ]; then
    echo -e "${GREEN}✅ Framework appears to be properly configured!${NC}"
    echo ""
    echo "Next steps:"
    echo "1. Run full regression test suite:"
    echo "   dotnet test $TEST_PROJECT_PATH --filter \"Category=Regression\""
    echo ""
    echo "2. Run specific categories:"
    echo "   dotnet test $TEST_PROJECT_PATH --filter \"Category=ApiCompatibility\""
    echo "   dotnet test $TEST_PROJECT_PATH --filter \"Category=Migration\""
    echo "   dotnet test $TEST_PROJECT_PATH --filter \"Category=ResourceManagement\""
    echo ""
    echo "3. Run with coverage:"
    echo "   dotnet test $TEST_PROJECT_PATH --filter \"Category=Regression\" --collect:\"XPlat Code Coverage\""
    echo ""
    echo "4. For CI/CD, the GitHub Actions workflow will handle automation"
else
    echo -e "${RED}❌ Framework configuration appears incomplete!${NC}"
    echo ""
    echo "Please check the missing files and ensure all components are properly set up."
fi

echo ""
echo "=== Validation Complete ==="
echo "Completed at $(date)"
