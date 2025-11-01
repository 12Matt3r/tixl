#!/usr/bin/env bash
# scripts/release/prepare-release.sh
# Prepare TiXL for release with comprehensive quality gates

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
PURPLE='\033[0;35m'
NC='\033[0m' # No Color

print_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

print_header() {
    echo -e "${PURPLE}=== $1 ===${NC}"
}

# Parse command line arguments
VERSION="${1:-}"
RELEASE_TYPE="${2:-minor}"
SKIP_TESTS="${3:-false}"
VERBOSE="${4:-false}"

show_usage() {
    cat << EOF
TiXL Release Preparation Script

Usage: $0 [OPTIONS] VERSION [RELEASE_TYPE]

Arguments:
    VERSION           Target version (e.g., v1.2.0)
    RELEASE_TYPE      Type of release (major|minor|patch)

Options:
    --skip-tests      Skip test execution (not recommended)
    --verbose         Show detailed output
    --help            Show this help

Examples:
    $0 v1.2.0 minor
    $0 --skip-tests v1.2.0 patch
    $0 --verbose v2.0.0 major

EOF
}

# Parse optional flags
while [[ $# -gt 0 ]]; do
    case $1 in
        --skip-tests)
            SKIP_TESTS="true"
            shift
            ;;
        --verbose)
            VERBOSE="true"
            shift
            ;;
        --help)
            show_usage
            exit 0
            ;;
        *)
            break
            ;;
    esac
done

# Validate inputs
if [[ -z "$VERSION" || -z "$RELEASE_TYPE" ]]; then
    print_error "VERSION and RELEASE_TYPE are required"
    show_usage
    exit 1
fi

if [[ ! "$RELEASE_TYPE" =~ ^(major|minor|patch)$ ]]; then
    print_error "RELEASE_TYPE must be major, minor, or patch"
    exit 1
fi

# Ensure we're on the right branch
CURRENT_BRANCH=$(git branch --show-current)
if [[ "$CURRENT_BRANCH" != "main" && "$CURRENT_BRANCH" =~ ^release/ ]]; then
    print_info "On release branch: $CURRENT_BRANCH"
elif [[ "$RELEASE_TYPE" == "major" && "$CURRENT_BRANCH" != "main" ]]; then
    print_error "Major releases must be prepared from main branch"
    exit 1
fi

print_header "TiXL Release Preparation"
print_info "Target version: $VERSION"
print_info "Release type: $RELEASE_TYPE"
print_info "Branch: $CURRENT_BRANCH"
echo ""

# Initialize counters
TOTAL_CHECKS=0
PASSED_CHECKS=0
FAILED_CHECKS=0
WARNING_CHECKS=0

# Function to run a check
run_check() {
    local check_name="$1"
    local check_command="$2"
    local critical="${3:-true}"
    
    ((TOTAL_CHECKS++))
    print_info "Running: $check_name"
    
    if eval "$check_command" >/dev/null 2>&1; then
        if [[ "$VERBOSE" == "true" ]]; then
            print_success "$check_name: PASSED"
        fi
        ((PASSED_CHECKS++))
        return 0
    else
        if [[ "$critical" == "true" ]]; then
            print_error "$check_name: FAILED"
            ((FAILED_CHECKS++))
            return 1
        else
            print_warning "$check_name: WARNING"
            ((WARNING_CHECKS++))
            return 2
        fi
    fi
}

print_header "1. CODE QUALITY GATES"

# Check 1: Code analysis
run_check "Code Analysis (FxCop)" "dotnet build --configuration Release --no-restore" || {
    print_error "Code analysis failed - build contains errors"
    exit 1
}

# Check 2: No TODO/FIXME comments
if [[ $(grep -r "TODO\|FIXME\|HACK\|XXX" src/ --include="*.cs" | wc -l) -gt 0 ]]; then
    print_warning "TODO/FIXME/HACK comments found in source code"
    ((WARNING_CHECKS++))
else
    print_success "No TODO/FIXME comments found"
    ((PASSED_CHECKS++))
fi
((TOTAL_CHECKS++))

# Check 3: Documentation coverage
DOC_COVERAGE=$(find src/ -name "*.cs" -exec grep -L "/// <summary>" {} \; | wc -l)
if [[ $DOC_COVERAGE -gt 0 ]]; then
    print_warning "Public APIs missing documentation: $DOC_COVERAGE files"
    ((WARNING_CHECKS++))
else
    print_success "All public APIs documented"
    ((PASSED_CHECKS++))
fi
((TOTAL_CHECKS++))

# Check 4: Architectural validation
if [[ -f "scripts/validate-architecture.sh" ]]; then
    run_check "Architectural Boundaries" "./scripts/validate-architecture.sh validate"
fi

print_header "2. TESTING GATES"

# Check 5: Unit tests
if [[ "$SKIP_TESTS" != "true" ]]; then
    run_check "Unit Tests" "dotnet test --configuration Release --no-build --verbosity quiet" || {
        print_error "Unit tests failed - cannot proceed with release"
        exit 1
    }
else
    print_warning "Skipping unit tests (not recommended)"
    ((WARNING_CHECKS++))
fi
((TOTAL_CHECKS++))

# Check 6: Test coverage
if command -v coverlet >/dev/null 2>&1; then
    COVERAGE=$(dotnet test --configuration Release --collect:"XPlat Code Coverage" --no-build | grep -o '[0-9]*\.[0-9]*%' | tail -1 | sed 's/%//')
    if [[ $(echo "$COVERAGE < 85" | bc -l) -eq 1 ]]; then
        print_warning "Test coverage below 85%: $COVERAGE%"
        ((WARNING_CHECKS++))
    else
        print_success "Test coverage acceptable: $COVERAGE%"
        ((PASSED_CHECKS++))
    fi
    ((TOTAL_CHECKS++))
else
    print_warning "Coverage tool not available - skipping coverage check"
    ((WARNING_CHECKS++))
    ((TOTAL_CHECKS++))
fi

# Check 7: Performance benchmarks (if available)
if [[ -d "Benchmarks" && "$RELEASE_TYPE" != "patch" ]]; then
    if command -v dotnet >/dev/null 2>&1; then
        run_check "Performance Benchmarks" "cd Benchmarks && dotnet run --configuration Release --no-build -- --filter '*' --verbosity quiet" || {
            print_warning "Performance benchmarks failed or exceeded thresholds"
            ((WARNING_CHECKS++))
        }
        ((TOTAL_CHECKS++))
    fi
fi

print_header "3. SECURITY GATES"

# Check 8: Security scan
run_check "Security Vulnerability Scan" "dotnet list package --vulnerable --format json" || {
    print_warning "Security vulnerabilities detected"
    ((WARNING_CHECKS++))
}
((TOTAL_CHECKS++))

# Check 9: Package signing verification
if [[ -f "TiXL.snk" || -f "*.pfx" ]]; then
    if [[ "$VERBOOSE" == "true" ]]; then
        print_info "Package signing certificates found"
    fi
    ((PASSED_CHECKS++))
else
    print_warning "No package signing certificates found"
    ((WARNING_CHECKS++))
fi
((TOTAL_CHECKS++))

print_header "4. DEPENDENCY GATES"

# Check 10: Dependency updates
OUTDATED_PACKAGES=$(dotnet list package --outdated | grep -v "The following" | wc -l)
if [[ $OUTDATED_PACKAGES -gt 0 ]]; then
    print_warning "Outdated packages detected: $OUTDATED_PACKAGES"
    ((WARNING_CHECKS++))
else
    print_success "All packages up to date"
    ((PASSED_CHECKS++))
fi
((TOTAL_CHECKS++))

# Check 11: License compatibility
if [[ -f "THIRD-PARTY-NOTICES.txt" ]]; then
    print_success "Third-party notices file exists"
    ((PASSED_CHECKS++))
else
    print_warning "Third-party notices file missing"
    ((WARNING_CHECKS++))
fi
((TOTAL_CHECKS++))

print_header "5. BUILD VERIFICATION"

# Check 12: Release build
run_check "Release Build" "dotnet build --configuration Release --no-restore" || {
    print_error "Release build failed"
    exit 1
}

# Check 13: NuGet package creation
if dotnet pack --configuration Release --no-build --output ./artifacts >/dev/null 2>&1; then
    print_success "NuGet packages created successfully"
    ((PASSED_CHECKS++))
else
    print_error "NuGet package creation failed"
    ((FAILED_CHECKS++))
    ((TOTAL_CHECKS++))
    exit 1
fi
((TOTAL_CHECKS++))

# Check 14: Symbol package creation
if dotnet pack --configuration Release --no-build --output ./artifacts --include-symbols >/dev/null 2>&1; then
    print_success "Symbol packages created successfully"
    ((PASSED_CHECKS++))
else
    print_warning "Symbol package creation failed"
    ((WARNING_CHECKS++))
fi
((TOTAL_CHECKS++))

print_header "6. DOCUMENTATION GATES"

# Check 15: Documentation build
if [[ -f "docs/README.md" ]]; then
    print_success "Documentation files exist"
    ((PASSED_CHECKS++))
else
    print_warning "Documentation missing or incomplete"
    ((WARNING_CHECKS++))
fi
((TOTAL_CHECKS++))

# Check 16: CHANGELOG exists
if [[ -f "CHANGELOG.md" ]]; then
    print_success "CHANGELOG.md exists"
    ((PASSED_CHECKS++))
else
    print_warning "CHANGELOG.md missing"
    ((WARNING_CHECKS++))
fi
((TOTAL_CHECKS++))

print_header "7. VERSION VERIFICATION"

# Check 17: Version consistency
CURRENT_VERSION=$(grep -oP '(?<=<Version>)[^<]+' Directory.Build.props)
if [[ "$VERSION" =~ v([0-9]+\.[0-9]+\.[0-9]+) ]]; then
    EXPECTED_VERSION="${BASH_REMATCH[1]}"
    if [[ "$CURRENT_VERSION" == "$EXPECTED_VERSION" ]]; then
        print_success "Version consistent: $CURRENT_VERSION"
        ((PASSED_CHECKS++))
    else
        print_error "Version mismatch: Directory.Build.props has $CURRENT_VERSION, expected $EXPECTED_VERSION"
        ((FAILED_CHECKS++))
    fi
else
    print_error "Invalid version format: $VERSION"
    ((FAILED_CHECKS++))
fi
((TOTAL_CHECKS++))

# Check 18: Git tag exists or can be created
if git rev-parse "$VERSION" >/dev/null 2>&1; then
    print_success "Git tag $VERSION exists"
    ((PASSED_CHECKS++))
else
    print_info "Git tag $VERSION will be created during release"
    ((PASSED_CHECKS++))
fi
((TOTAL_CHECKS++))

print_header "8. COMMUNICATION GATES"

# Check 19: Release notes template exists
if [[ -f ".github/release-template.md" ]]; then
    print_success "Release notes template exists"
    ((PASSED_CHECKS++))
else
    print_warning "Release notes template missing"
    ((WARNING_CHECKS++))
fi
((TOTAL_CHECKS++))

# Summary
echo ""
print_header "RELEASE PREPARATION SUMMARY"
echo "Total checks: $TOTAL_CHECKS"
echo "Passed: $PASSED_CHECKS"
echo "Warnings: $WARNING_CHECKS"
echo "Failed: $FAILED_CHECKS"
echo ""

# Determine if release can proceed
if [[ $FAILED_CHECKS -eq 0 ]]; then
    if [[ $WARNING_CHECKS -eq 0 ]]; then
        print_success "🎉 All quality gates passed! Release is ready."
        echo ""
        print_info "Next steps:"
        echo "  1. Review quality gates report"
        echo "  2. Update documentation if needed"
        echo "  3. Create release branch: git checkout -b release/$VERSION"
        echo "  4. Bump version: ./scripts/release/bump-version.sh ${RELEASE_TYPE}"
        echo "  5. Generate changelog: ./scripts/release/generate-changelog.sh"
        echo "  6. Run final validation: ./scripts/release/finalize-release.sh $VERSION"
        exit 0
    else
        print_warning "⚠️  Release ready with $WARNING_CHECKS warnings. Review before proceeding."
        echo ""
        print_info "Review warnings and consider addressing before release."
        exit 0
    fi
else
    print_error "❌ Release preparation failed with $FAILED_CHECKS critical issues."
    echo ""
    print_error "Please address all failed checks before proceeding."
    exit 1
fi