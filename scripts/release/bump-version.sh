#!/usr/bin/env bash
# scripts/release/bump-version.sh
# Automated version bumping script for TiXL releases

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
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

# Function to show usage
show_usage() {
    cat << EOF
TiXL Version Bumping Script

Usage: $0 [OPTIONS] VERSION_TYPE [--release-branch BRANCH]

VERSION_TYPE:
    major        Increment major version (x.0.0)
    minor        Increment minor version (x.y.0)
    patch        Increment patch version (x.y.z)
    auto         Auto-detect version type from commits

OPTIONS:
    --target VER       Target version (overrides auto-detection)
    --release-branch   Create version bump on release branch
    --dry-run         Show changes without applying
    --help            Show this help message

EXAMPLES:
    $0 minor                          # Bump minor version
    $0 major --target 2.0.0           # Bump to specific major version
    $0 auto --release-branch release/v1.2.0
    $0 patch --dry-run                # Preview changes

EOF
}

# Parse command line arguments
VERSION_TYPE=""
TARGET_VERSION=""
RELEASE_BRANCH=""
DRY_RUN=false

while [[ $# -gt 0 ]]; do
    case $1 in
        major|minor|patch|auto)
            VERSION_TYPE="$1"
            shift
            ;;
        --target)
            TARGET_VERSION="$2"
            shift 2
            ;;
        --release-branch)
            RELEASE_BRANCH="$2"
            shift 2
            ;;
        --dry-run)
            DRY_RUN=true
            shift
            ;;
        --help)
            show_usage
            exit 0
            ;;
        *)
            print_error "Unknown option: $1"
            show_usage
            exit 1
            ;;
    esac
done

# Validate inputs
if [[ -z "$VERSION_TYPE" && -z "$TARGET_VERSION" ]]; then
    print_error "VERSION_TYPE or --target is required"
    show_usage
    exit 1
fi

if [[ "$VERSION_TYPE" == "auto" && -z "$TARGET_VERSION" ]]; then
    print_info "Auto-detecting version bump type..."
    
    # Check for breaking changes in recent commits
    if git log --oneline --since='2 weeks ago' | grep -qi 'breaking\|BREAKING'; then
        VERSION_TYPE="minor"
        print_info "Breaking changes detected - will bump minor version"
    else
        VERSION_TYPE="patch"
        print_info "No breaking changes - will bump patch version"
    fi
fi

# Get current version from Directory.Build.props
CURRENT_VERSION=$(grep -oP '(?<=<Version>)[^<]+' Directory.Build.props 2>/dev/null || echo "1.0.0")
print_info "Current version: $CURRENT_VERSION"

# Parse current version
IFS='.' read -ra VERSION_PARTS <<< "$CURRENT_VERSION"
MAJOR=${VERSION_PARTS[0]:-1}
MINOR=${VERSION_PARTS[1]:-0}
PATCH=${VERSION_PARTS[2]:-0}

# Calculate new version
if [[ -n "$TARGET_VERSION" ]]; then
    NEW_VERSION="$TARGET_VERSION"
    print_info "Using target version: $NEW_VERSION"
else
    case $VERSION_TYPE in
        major)
            MAJOR=$((MAJOR + 1))
            MINOR=0
            PATCH=0
            ;;
        minor)
            MINOR=$((MINOR + 1))
            PATCH=0
            ;;
        patch)
            PATCH=$((PATCH + 1))
            ;;
    esac
    
    NEW_VERSION="$MAJOR.$MINOR.$PATCH"
fi

print_info "New version: $NEW_VERSION"

# Check if we're on the right branch
CURRENT_BRANCH=$(git branch --show-current)
if [[ -n "$RELEASE_BRANCH" ]]; then
    if [[ "$CURRENT_BRANCH" != "$RELEASE_BRANCH" ]]; then
        print_warning "Currently on branch '$CURRENT_BRANCH', but targeting '$RELEASE_BRANCH'"
        print_info "Switching to release branch..."
        git checkout "$RELEASE_BRANCH" || {
            print_error "Failed to switch to branch '$RELEASE_BRANCH'"
            exit 1
        }
    fi
elif [[ "$VERSION_TYPE" == "major" && "$CURRENT_BRANCH" == "main" ]]; then
    print_warning "Bumping major version on main branch - ensure this is intentional"
fi

if [[ "$DRY_RUN" == true ]]; then
    print_info "DRY RUN MODE - No changes will be applied"
    print_info "Would update Directory.Build.props with version $NEW_VERSION"
    exit 0
fi

# Update Directory.Build.props
print_info "Updating version in Directory.Build.props..."

# Update Version element
if grep -q "<Version>" Directory.Build.props; then
    sed -i.tmp "s|<Version>[^<]*</Version>|<Version>$NEW_VERSION</Version>|g" Directory.Build.props
else
    # Add Version element if it doesn't exist
    sed -i.tmp "/<PropertyGroup>/a\    <Version>$NEW_VERSION</Version>" Directory.Build.props
fi

# Update AssemblyVersion
sed -i.tmp "s|<AssemblyVersion>[^<]*</AssemblyVersion>|<AssemblyVersion>$MAJOR.$MINOR.0.0</AssemblyVersion>|g" Directory.Build.props

# Update FileVersion
sed -i.tmp "s|<FileVersion>[^<]*</FileVersion>|<FileVersion>$NEW_VERSION.0</FileVersion>|g" Directory.Build.props

# Clean up temporary file
rm -f Directory.Build.props.tmp

# Update InformationalVersion to include build info
BUILD_DATE=$(date +%Y%m%d)
BUILD_COMMIT=$(git rev-parse --short HEAD)
sed -i.tmp "s|<InformationalVersion>[^<]*</InformationalVersion>|<InformationalVersion>$NEW_VERSION+${BUILD_DATE}.${BUILD_COMMIT}</InformationalVersion>|g" Directory.Build.props
rm -f Directory.Build.props.tmp

# Update all .csproj files in the solution
print_info "Updating version in all project files..."
find . -name "*.csproj" -not -path "./Tests/*" -not -path "./Benchmarks/*" | while read -r project_file; do
    if grep -q "<Version>" "$project_file"; then
        sed -i.tmp "s|<Version>[^<]*</Version>|<Version>$NEW_VERSION</Version>|g" "$project_file"
        rm -f "${project_file}.tmp"
    fi
done

# Create version bump commit
COMMIT_MESSAGE="chore: bump version to $NEW_VERSION

Bump type: $VERSION_TYPE
Previous version: $CURRENT_VERSION
Build date: $(date -Iseconds)
Git commit: $(git rev-parse --short HEAD)
Branch: $(git branch --show-current)
"

print_info "Creating commit with message:"
echo "$COMMIT_MESSAGE"

git add Directory.Build.props
git add -u  # Stage modified .csproj files

if git diff --cached --quiet; then
    print_warning "No changes to commit - version may already be at target"
    exit 0
fi

git commit -m "$COMMIT_MESSAGE"

# Tag the commit
TAG_NAME="v$NEW_VERSION"
print_info "Creating tag: $TAG_NAME"

if git tag -l | grep -q "^$TAG_NAME$"; then
    print_warning "Tag $TAG_NAME already exists - skipping tag creation"
else
    git tag -a "$TAG_NAME" -m "Release $NEW_VERSION

Features:
- $(git log --oneline --since="2 weeks ago" | grep "^feat" | wc -l) new features
- $(git log --oneline --since="2 weeks ago" | grep "^fix" | wc -l) bug fixes
- $(git log --oneline --since="2 weeks ago" | grep "^perf" | wc -l) performance improvements

Breaking Changes:
- $(git log --oneline --since="2 weeks ago" | grep -i "breaking" | wc -l) breaking changes
"
fi

print_success "Version bumped from $CURRENT_VERSION to $NEW_VERSION"
print_info "Tag created: $TAG_NAME"
print_info "Next steps:"
echo "  1. Push changes: git push origin $(git branch --show-current)"
echo "  2. Push tag: git push origin $TAG_NAME"
echo "  3. Trigger release pipeline"

# Show summary
echo ""
echo "Version Bump Summary:"
echo "  Current: $CURRENT_VERSION"
echo "  New:     $NEW_VERSION"
echo "  Type:    $VERSION_TYPE"
echo "  Branch:  $(git branch --show-current)"
echo "  Commit:  $(git rev-parse --short HEAD)"
echo "  Tag:     $TAG_NAME"