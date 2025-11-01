#!/bin/bash
# TiXL Architectural Governance Validation Script
# This script can be used to validate architectural boundaries and set up development environment

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Script configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="$(git rev-parse --show-toplevel 2>/dev/null || echo "$SCRIPT_DIR")"
VALIDATOR_PROJECT="$REPO_DIR/Tools/ArchitecturalValidator/TiXL.ArchitecturalValidator.csproj"

# Help function
show_help() {
    cat << EOF
TiXL Architectural Governance Validation Script

Usage: $0 [OPTIONS] [COMMAND]

Commands:
    validate           Run architectural validation
    setup-hooks        Set up Git pre-commit hooks
    build-validator    Build the architectural validator tool
    check-deps         Check for architectural violations in dependencies
    generate-report    Generate architectural compliance report
    help              Show this help message

Options:
    -v, --verbose     Enable verbose output
    -s, --solution    Path to TiXL solution (default: auto-detect)
    -h, --help        Show this help message

Examples:
    $0 validate                              # Run validation on current codebase
    $0 setup-hooks                           # Set up Git hooks for architectural validation
    $0 build-validator                       # Build the validator tool
    $0 validate -v                           # Run validation with verbose output
    $0 check-deps                            # Check dependency violations

For more information, see docs/ARCHITECTURAL_GOVERNANCE.md
EOF
}

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

# Check prerequisites
check_prerequisites() {
    log_info "Checking prerequisites..."
    
    if ! command -v dotnet &> /dev/null; then
        log_error ".NET SDK is required but not installed"
        exit 1
    fi
    
    if ! command -v git &> /dev/null; then
        log_error "Git is required but not installed"
        exit 1
    fi
    
    # Check if we're in a git repository
    if [ ! -d "$REPO_DIR/.git" ]; then
        log_warning "Not in a Git repository, some features may not work"
    fi
    
    log_success "Prerequisites check passed"
}

# Auto-detect solution path
auto_detect_solution() {
    local solution="$REPO_DIR/TiXL.sln"
    
    if [ ! -f "$solution" ]; then
        # Try to find solution file
        local found_solution=$(find "$REPO_DIR" -name "TiXL.sln" -type f | head -1)
        if [ -n "$found_solution" ]; then
            solution="$found_solution"
        else
            log_error "Could not find TiXL.sln"
            log_info "Please specify solution path with -s option"
            exit 1
        fi
    fi
    
    echo "$solution"
}

# Build architectural validator
build_validator() {
    log_info "Building architectural validator tool..."
    
    cd "$REPO_DIR"
    
    if [ ! -f "$VALIDATOR_PROJECT" ]; then
        log_error "Validator project not found at $VALIDATOR_PROJECT"
        exit 1
    fi
    
    dotnet build "$VALIDATOR_PROJECT" --configuration Release --verbosity minimal
    
    if [ $? -eq 0 ]; then
        log_success "Architectural validator built successfully"
    else
        log_error "Failed to build architectural validator"
        exit 1
    fi
}

# Run architectural validation
run_validation() {
    local solution_path=${1:-$(auto_detect_solution)}
    local verbose=${2:-false}
    
    log_info "Running architectural validation..."
    log_info "Solution path: $solution_path"
    
    # Ensure validator is built
    if [ ! -f "$REPO_DIR/Tools/ArchitecturalValidator/bin/Release/net9.0/TiXL.ArchitecturalValidator" ]; then
        log_info "Validator not found, building..."
        build_validator
    fi
    
    cd "$REPO_DIR"
    
    if [ "$verbose" = true ]; then
        dotnet run --project "$VALIDATOR_PROJECT" -- "$solution_path"
    else
        dotnet run --project "$VALIDATOR_PROJECT" -- "$solution_path" --verbosity quiet
    fi
    
    local exit_code=$?
    
    if [ $exit_code -eq 0 ]; then
        log_success "Architectural validation passed!"
    else
        log_error "Architectural validation failed!"
        log_info "Please fix the violations and try again"
        log_info "For help, see docs/ARCHITECTURAL_GOVERNANCE.md"
    fi
    
    return $exit_code
}

# Set up Git pre-commit hooks
setup_hooks() {
    log_info "Setting up Git pre-commit hooks..."
    
    local hooks_dir="$REPO_DIR/.git/hooks"
    local hook_script="$REPO_DIR/.githooks/pre-commit"
    
    if [ ! -d "$hooks_dir" ]; then
        log_error "Git hooks directory not found. Are you in a Git repository?"
        exit 1
    fi
    
    # Make the hook executable
    chmod +x "$hook_script"
    
    # Install the hook
    cp "$hook_script" "$hooks_dir/pre-commit"
    
    log_success "Git pre-commit hook installed successfully"
    log_info "The hook will run architectural validation before each commit"
    log_info "To skip validation, use: git commit --no-verify"
}

# Check dependency violations
check_dependencies() {
    log_info "Checking for dependency violations..."
    
    # Define forbidden patterns
    declare -A forbidden_patterns=(
        ["Core -> Operators"]="TiXL\.Core.*TiXL\.Operators"
        ["Core -> Gui"]="TiXL\.Core.*TiXL\.Gui"
        ["Core -> Editor"]="TiXL\.Core.*TiXL\.Editor"
        ["Operators -> Gui"]="TiXL\.Operators.*TiXL\.Gui"
        ["Operators -> Editor"]="TiXL\.Operators.*TiXL\.Editor"
        ["Gfx -> Operators"]="TiXL\.Gfx.*TiXL\.Operators"
        ["Gfx -> Gui"]="TiXL\.Gfx.*TiXL\.Gui"
        ["Gfx -> Editor"]="TiXL\.Gfx.*TiXL\.Editor"
        ["Gui -> Editor"]="TiXL\.Gui.*TiXL\.Editor"
    )
    
    local violations=0
    
    # Check for using statements
    for pattern_name in "${!forbidden_patterns[@]}"; do
        local pattern="${forbidden_patterns[$pattern_name]}"
        local count=$(grep -r "$pattern" "$REPO_DIR/src" 2>/dev/null | wc -l || true)
        
        if [ "$count" -gt 0 ]; then
            log_error "Found $count forbidden dependency violations for: $pattern_name"
            violations=$((violations + 1))
        fi
    done
    
    # Check project files
    log_info "Checking project reference violations..."
    local project_violations=0
    
    while IFS= read -r project_file; do
        if [ -f "$project_file" ]; then
            local project_name=$(basename "$project_file" .csproj)
            local content=$(cat "$project_file")
            
            # Define forbidden references per project
            case "$project_name" in
                "TiXL.Core")
                    if echo "$content" | grep -q "ProjectReference.*TiXL\."; then
                        log_error "Core project has forbidden project references"
                        project_violations=$((project_violations + 1))
                    fi
                    ;;
                "TiXL.Operators")
                    if echo "$content" | grep -q "ProjectReference.*TiXL\.[GF]"; then
                        log_error "Operators project has forbidden project references"
                        project_violations=$((project_violations + 1))
                    fi
                    ;;
            esac
        fi
    done < <(find "$REPO_DIR/src" -name "*.csproj")
    
    if [ $violations -eq 0 ] && [ $project_violations -eq 0 ]; then
        log_success "No dependency violations found!"
    else
        log_error "Found dependency violations that need to be addressed"
        log_info "Total violations: $((violations + project_violations))"
    fi
    
    return $((violations + project_violations))
}

# Generate architectural compliance report
generate_report() {
    local solution_path=${1:-$(auto_detect_solution)}
    
    log_info "Generating architectural compliance report..."
    
    # Create a temporary report file
    local report_file="$REPO_DIR/architectural-compliance-report-$(date +%Y%m%d-%H%M%S).md"
    
    {
        echo "# TiXL Architectural Compliance Report"
        echo ""
        echo "Generated: $(date)"
        echo "Solution: $solution_path"
        echo ""
        
        echo "## Summary"
        echo ""
        
        # Run validation and capture output
        if run_validation "$solution_path" false > /dev/null 2>&1; then
            echo "✅ **PASS**: Architectural validation successful"
            echo ""
            echo "All modules follow established architectural boundaries."
        else
            echo "❌ **FAIL**: Architectural validation failed"
            echo ""
            echo "The following violations were found:"
        fi
        
        echo ""
        echo "## Module Boundaries"
        echo ""
        echo "| Module | Can Reference | Forbidden References |"
        echo "|--------|---------------|----------------------|"
        echo "| Core | System, Microsoft | Operators, Gui, Editor, Gfx |"
        echo "| Operators | Core, System, Microsoft | Gui, Editor, Gfx |"
        echo "| Gfx | Core, System, Microsoft | Operators, Gui, Editor |"
        echo "| Gui | Core, Operators, System, Microsoft | Editor, Gfx |"
        echo "| Editor | All modules | None |"
        echo ""
        
        echo "## Recommendations"
        echo ""
        echo "1. **Use Interface-Based Communication**: Define interfaces in Core, implement in appropriate modules"
        echo "2. **Follow Dependency Inversion**: High-level modules should depend on abstractions"
        echo "3. **Use Events and Callbacks**: For loose coupling between modules"
        echo "4. **Implement Dependency Injection**: For better testability and flexibility"
        echo ""
        
        echo "For detailed information, see [docs/ARCHITECTURAL_GOVERNANCE.md](docs/ARCHITECTURAL_GOVERNANCE.md)"
        
    } > "$report_file"
    
    log_success "Compliance report generated: $report_file"
    
    # Open the report if possible
    if command -v code &> /dev/null; then
        code "$report_file"
    elif command -v xdg-open &> /dev/null; then
        xdg-open "$report_file"
    fi
}

# Main script logic
main() {
    local command=""
    local verbose=false
    local solution_path=""
    
    # Parse command line arguments
    while [[ $# -gt 0 ]]; do
        case $1 in
            -v|--verbose)
                verbose=true
                shift
                ;;
            -s|--solution)
                solution_path="$2"
                shift 2
                ;;
            -h|--help)
                show_help
                exit 0
                ;;
            validate)
                command="validate"
                shift
                ;;
            setup-hooks)
                command="setup-hooks"
                shift
                ;;
            build-validator)
                command="build-validator"
                shift
                ;;
            check-deps)
                command="check-deps"
                shift
                ;;
            generate-report)
                command="generate-report"
                shift
                ;;
            help)
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
    
    # Default command if none specified
    if [ -z "$command" ]; then
        command="validate"
    fi
    
    # Check prerequisites
    check_prerequisites
    
    # Execute command
    case "$command" in
        validate)
            run_validation "$solution_path" "$verbose"
            ;;
        setup-hooks)
            setup_hooks
            ;;
        build-validator)
            build_validator
            ;;
        check-deps)
            check_dependencies
            ;;
        generate-report)
            generate_report "$solution_path"
            ;;
        *)
            log_error "Unknown command: $command"
            show_help
            exit 1
            ;;
    esac
}

# Run main function
main "$@"