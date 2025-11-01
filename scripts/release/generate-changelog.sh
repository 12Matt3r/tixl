#!/usr/bin/env bash
# scripts/release/generate-changelog.sh
# Generate automated changelog from Git history

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

print_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Parse command line arguments
PREV_TAG="${1:-}"
CURR_TAG="${2:-}"
OUTPUT_FILE="${3:-CHANGELOG.md}"
INCLUDE_BREAKING="${4:-false}"
INCLUDE_PERFORMANCE="${5:-true}"

# Show usage
show_usage() {
    cat << EOF
TiXL Changelog Generator

Usage: $0 [OPTIONS] PREV_TAG CURR_TAG [OUTPUT_FILE]

Arguments:
    PREV_TAG           Previous version tag (e.g., v1.0.0)
    CURR_TAG           Current version tag (e.g., v1.1.0)
    OUTPUT_FILE        Output file path (default: CHANGELOG.md)

Options:
    --include-breaking      Include breaking changes section
    --no-performance        Exclude performance changes
    --help                  Show this help

Examples:
    $0 v1.0.0 v1.1.0
    $0 v1.0.0 v1.1.0 CHANGELOG.md
    $0 --include-breaking v1.0.0 v2.0.0

EOF
}

# Parse optional flags
while [[ $# -gt 0 ]]; do
    case $1 in
        --include-breaking)
            INCLUDE_BREAKING="true"
            shift
            ;;
        --no-performance)
            INCLUDE_PERFORMANCE="false"
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
if [[ -z "$PREV_TAG" || -z "$CURR_TAG" ]]; then
    print_error "Both PREV_TAG and CURR_TAG are required"
    show_usage
    exit 1
fi

# Check if tags exist
if ! git rev-parse "$PREV_TAG" >/dev/null 2>&1; then
    print_error "Tag $PREV_TAG does not exist"
    exit 1
fi

if ! git rev-parse "$CURR_TAG" >/dev/null 2>&1; then
    print_error "Tag $CURR_TAG does not exist"
    exit 1
fi

print_info "Generating changelog from $PREV_TAG to $CURR_TAG"

# Generate release date
RELEASE_DATE=$(date +%Y-%m-%d)
RELEASE_TIME=$(date +%H:%M:%S%z)

# Get commit range
COMMIT_RANGE="$PREV_TAG..$CURR_TAG"
COMMIT_COUNT=$(git rev-list --count "$COMMIT_RANGE")
CONTRIBUTORS=$(git log "$COMMIT_RANGE" --format='%aE' | sort -u | wc -l)

print_info "Found $COMMIT_COUNT commits from $CONTRIBUTORS contributors"

# Start generating changelog
{
    echo "# Changelog"
    echo ""
    echo "All notable changes to TiXL will be documented in this file."
    echo ""
    echo "The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),"
    echo "and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html)."
    echo ""
    echo "## [$CURR_TAG] - $RELEASE_DATE"
    echo ""
    
    # Download statistics
    echo "### Download Statistics"
    echo "- Total commits: $COMMIT_COUNT"
    echo "- Contributors: $CONTRIBUTORS"
    echo "- Release date: $RELEASE_DATE $RELEASE_TIME"
    echo ""
    
    # Parse conventional commits
    echo "### Added"
    echo ""
    git log "$COMMIT_RANGE" --grep="^feat" --pretty=format:"- %s" | while read -r line; do
        if [[ -n "$line" ]]; then
            echo "$line"
        fi
    done
    if [[ -z "$(git log "$COMMIT_RANGE" --grep="^feat" --pretty=format:"- %s" | grep -v '^$')" ]]; then
        echo "_No new features in this release._"
    fi
    echo ""
    
    echo "### Fixed"
    echo ""
    git log "$COMMIT_RANGE" --grep="^fix" --pretty=format:"- %s" | while read -r line; do
        if [[ -n "$line" ]]; then
            echo "$line"
        fi
    done
    if [[ -z "$(git log "$COMMIT_RANGE" --grep="^fix" --pretty=format="- %s" | grep -v '^$')" ]]; then
        echo "_No bug fixes in this release._"
    fi
    echo ""
    
    echo "### Changed"
    echo ""
    git log "$COMMIT_RANGE" --grep="^chore\|^refactor" --pretty=format:"- %s" | while read -r line; do
        if [[ -n "$line" ]]; then
            echo "$line"
        fi
    done
    if [[ -z "$(git log "$COMMIT_RANGE" --grep="^chore\|^refactor" --pretty=format="- %s" | grep -v '^$')" ]]; then
        echo "_No changes in this release._"
    fi
    echo ""
    
    if [[ "$INCLUDE_PERFORMANCE" == "true" ]]; then
        echo "### Performance"
        echo ""
        git log "$COMMIT_RANGE" --grep="^perf" --pretty=format:"- %s" | while read -r line; do
            if [[ -n "$line" ]]; then
                echo "$line"
            fi
        done
        if [[ -z "$(git log "$COMMIT_RANGE" --grep="^perf" --pretty=format="- %s" | grep -v '^$')" ]]; then
            echo "_No performance improvements in this release._"
        fi
        echo ""
    fi
    
    if [[ "$INCLUDE_BREAKING" == "true" ]]; then
        echo "### Breaking Changes"
        echo ""
        git log "$COMMIT_RANGE" --grep -i "BREAKING\|breaking" --pretty=format:"- %s" | while read -r line; do
            if [[ -n "$line" ]]; then
                echo "$line"
            fi
        done
        if [[ -z "$(git log "$COMMIT_RANGE" --grep -i "BREAKING\|breaking" --pretty=format="- %s" | grep -v '^$')" ]]; then
            echo "_No breaking changes in this release._"
        fi
        echo ""
    fi
    
    echo "### Documentation"
    echo ""
    git log "$COMMIT_RANGE" --grep="^docs" --pretty=format:"- %s" | while read -r line; do
        if [[ -n "$line" ]]; then
            echo "$line"
        fi
    done
    if [[ -z "$(git log "$COMMIT_RANGE" --grep="^docs" --pretty=format="- %s" | grep -v '^$')" ]]; then
        echo "_No documentation changes in this release._"
    fi
    echo ""
    
    echo "### Testing"
    echo ""
    git log "$COMMIT_RANGE" --grep="^test" --pretty=format="- %s" | while read -r line; do
        if [[ -n "$line" ]]; then
            echo "$line"
        fi
    done
    if [[ -z "$(git log "$COMMIT_RANGE" --grep="^test" --pretty=format="- %s" | grep -v '^$')" ]]; then
        echo "_No test changes in this release._"
    fi
    echo ""
    
    echo "### Security"
    echo ""
    git log "$COMMIT_RANGE" --grep="^security\|^fix.*vulnerability\|^fix.*CVE" --pretty=format:"- %s" | while read -r line; do
        if [[ -n "$line" ]]; then
            echo "$line"
        fi
    done
    if [[ -z "$(git log "$COMMIT_RANGE" --grep="^security\|^fix.*vulnerability\|^fix.*CVE" --pretty=format="- %s" | grep -v '^$')" ]]; then
        echo "_No security changes in this release._"
    fi
    echo ""
    
    # Dependency updates
    echo "### Dependencies"
    echo ""
    git log "$COMMIT_RANGE" --grep="^deps" --pretty=format:"- %s" | while read -r line; do
        if [[ -n "$line" ]]; then
            echo "$line"
        fi
    done
    if [[ -z "$(git log "$COMMIT_RANGE" --grep="^deps" --pretty=format="- %s" | grep -v '^$')" ]]; then
        echo "_No dependency updates in this release._"
    fi
    echo ""
    
    # Contributors section
    echo "### Contributors"
    echo ""
    echo "We thank all contributors who made this release possible:"
    echo ""
    git log "$COMMIT_RANGE" --format='- %aN (%aE)' | sort -u | while read -r contributor; do
        echo "$contributor"
    done
    echo ""
    
    # Detailed commit log (for developers)
    echo "### Detailed Changes"
    echo ""
    echo "Full commit history:"
    echo ""
    git log "$COMMIT_RANGE" --pretty=format:"- %h %s (%aN, %aD)" | while read -r commit; do
        echo "$commit"
    done
    echo ""
    
    # Known issues (if any)
    echo "### Known Issues"
    echo ""
    echo "_No known issues in this release._"
    echo ""
    
    # Upgrade guide for major releases
    if [[ "$CURR_TAG" =~ ^v[2-9]\. ]]; then
        echo "### Upgrade Guide"
        echo ""
        echo "For major version upgrades, see our [Migration Guide](../MIGRATION.md)."
        echo ""
    fi
    
} > "$OUTPUT_FILE"

# Generate release notes for GitHub
GITHUB_OUTPUT="${OUTPUT_FILE%.*}.github.md"
{
    echo "# $CURR_TAG - Release Notes"
    echo ""
    echo "**Release Date:** $RELEASE_DATE"
    echo "**Total Commits:** $COMMIT_COUNT"
    echo "**Contributors:** $CONTRIBUTORS"
    echo ""
    
    # Add download links
    echo "## Downloads"
    echo ""
    echo "[GitHub Release](https://github.com/tixl/tixl/releases/tag/$CURR_TAG)"
    echo ""
    
    # Highlight key features
    FEATURE_COUNT=$(git log "$COMMIT_RANGE" --grep="^feat" --pretty=format: | wc -l)
    BUGFIX_COUNT=$(git log "$COMMIT_RANGE" --grep="^fix" --pretty=format: | wc -l)
    
    echo "## Highlights"
    echo ""
    echo "- **$FEATURE_COUNT** new features and improvements"
    echo "- **$BUGFIX_COUNT** bug fixes and stability improvements"
    echo "- Enhanced performance and reliability"
    echo ""
    
    echo "## What's Changed"
    echo ""
    git log "$COMMIT_RANGE" --pretty=format:"- %s" | while read -r line; do
        if [[ -n "$line" ]]; then
            echo "$line"
        fi
    done
    echo ""
    
} > "$GITHUB_OUTPUT"

print_success "Changelog generated successfully!"
print_info "Main changelog: $OUTPUT_FILE"
print_info "GitHub release notes: $GITHUB_OUTPUT"
print_info "Release date: $RELEASE_DATE"
print_info "Commits: $COMMIT_COUNT"
print_info "Contributors: $CONTRIBUTORS"