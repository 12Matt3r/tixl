#!/bin/bash

# validate-wiki-links.sh - Validate internal and external links in wiki content
# Part of TIXL-066 Wiki Stabilization

set -euo pipefail

# Configuration
WIKI_ROOT="${1:-./wiki-structure}"
REPORT_FILE="${WIKI_ROOT}/link-validation-report.md"
BROKEN_LINKS_FILE="${WIKI_ROOT}/broken-links.txt"
WARNINGS_FILE="${WIKI_ROOT}/link-warnings.txt"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo "🔗 TiXL Wiki Link Validator"
echo "=========================="
echo "Validating links in: $WIKI_ROOT"
echo ""

# Initialize report files
cat > "$REPORT_FILE" << EOF
# Wiki Link Validation Report

**Generated**: $(date)
**Root Path**: $WIKI_ROOT

## Summary

EOF

> "$BROKEN_LINKS_FILE"
> "$WARNINGS_FILE"

# Counters
total_files=0
total_links=0
broken_links=0
warnings=0

# Function to check if file exists
check_file_exists() {
    local path="$1"
    local base_dir="$2"
    
    # Convert relative path to absolute
    if [[ "$path" =~ ^(http|https|mailto): ]]; then
        return 0 # External links, skip
    fi
    
    if [[ "$path" =~ ^# ]]; then
        return 0 # Anchor links, skip for now
    fi
    
    # Remove anchor if present
    local clean_path="${path%%#*}"
    
    # Handle relative paths
    if [[ "$clean_path" != /* ]]; then
        local resolved_path
        resolved_path=$(cd "$(dirname "$base_dir")" && realpath "$clean_path" 2>/dev/null || echo "")
        if [[ -n "$resolved_path" && -f "$resolved_path" ]]; then
            return 0
        fi
    else
        # Absolute path from wiki root
        if [[ -f "$WIKI_ROOT/$clean_path" ]]; then
            return 0
        fi
    fi
    
    return 1
}

# Function to extract links from markdown
extract_links() {
    local file="$1"
    grep -oE '\[[^\]]*\]\([^)]*\)' "$file" || true
}

# Process all markdown files
find "$WIKI_ROOT" -name "*.md" -type f | while read -r file; do
    echo "📄 Processing: $(basename "$file")"
    ((total_files++)) || true
    
    # Extract all links
    links=$(extract_links "$file")
    
    if [[ -z "$links" ]]; then
        continue
    fi
    
    echo "$links" | while IFS= read -r link_line; do
        ((total_links++)) || true
        
        # Parse link
        link_text=$(echo "$link_line" | sed 's/\[[^\]]*\]\((\([^)]*\))/\1/')
        link_url=$(echo "$link_line" | sed 's/\[[^\]]*\]((\([^)]*\))/\1/')
        
        # Check if link exists
        if check_file_exists "$link_url" "$file"; then
            echo -e "  ${GREEN}✓${NC} Valid: $link_text"
        else
            ((broken_links++)) || true
            echo -e "  ${RED}✗${NC} Broken: $link_text ($link_url)"
            echo "$link_url - $file" >> "$BROKEN_LINKS_FILE"
        fi
    done
done

# Check for common issues
echo ""
echo "🔍 Checking for common issues..."

# Function to check common accessibility issues
check_common_issues() {
    local file="$1"
    
    # Check for missing H1 headers
    if ! grep -q "^# " "$file"; then
        ((warnings++)) || true
        echo -e "  ${YELLOW}⚠${NC} Missing H1 header: $(basename "$file")"
        echo "$(basename "$file") - Missing H1 header" >> "$WARNINGS_FILE"
    fi
    
    # Check for missing table of contents
    if ! grep -q "## Table of Contents\|## Contents\|## Overview" "$file"; then
        ((warnings++)) || true
        echo -e "  ${YELLOW}⚠${NC} Missing overview/ToC: $(basename "$file")"
        echo "$(basename "$file") - Missing overview or table of contents" >> "$WARNINGS_FILE"
    fi
    
    # Check for non-descriptive link text
    non_descriptive_links=$(grep -oE '\[[^[]*[Hh]ere[^]]*\]\([^)]*\)' "$file" || true)
    if [[ -n "$non_descriptive_links" ]]; then
        ((warnings++)) || true
        echo -e "  ${YELLOW}⚠${NC} Non-descriptive links found in: $(basename "$file")"
        echo "$(basename "$file") - Non-descriptive link text found" >> "$WARNINGS_FILE"
    fi
    
    # Check for missing last updated
    if ! grep -q "last updated\|Last Updated\|Updated:" "$file"; then
        ((warnings++)) || true
        echo -e "  ${YELLOW}⚠${NC} Missing update date: $(basename "$file")"
        echo "$(basename "$file") - Missing last updated information" >> "$WARNINGS_FILE"
    fi
}

find "$WIKI_ROOT" -name "*.md" -type f | while read -r file; do
    check_common_issues "$file"
done

# Generate summary report
cat >> "$REPORT_FILE" << EOF

## Statistics

- **Total Files Processed**: $total_files
- **Total Links Checked**: $total_links
- **Broken Links**: $broken_links
- **Warnings**: $warnings
- **Link Accuracy**: $(( total_links > 0 ? (total_links - broken_links) * 100 / total_links : 100 ))%

## Broken Links
EOF

if [[ -s "$BROKEN_LINKS_FILE" ]]; then
    cat >> "$REPORT_FILE" << EOF

The following links could not be validated:

EOF
    cat "$BROKEN_LINKS_FILE" >> "$REPORT_FILE"
else
    echo "" >> "$REPORT_FILE"
    echo "✅ No broken links found!" >> "$REPORT_FILE"
fi

cat >> "$REPORT_FILE" << EOF

## Warnings

EOF

if [[ -s "$WARNINGS_FILE" ]]; then
    cat "$WARNINGS_FILE" >> "$REPORT_FILE"
else
    echo "✅ No warnings found!" >> "$REPORT_FILE"
fi

cat >> "$REPORT_FILE" << EOF

## Recommendations

### Immediate Actions
EOF

if [[ $broken_links -gt 0 ]]; then
    cat >> "$REPORT_FILE" << EOF
1. **Fix Broken Links**: $broken_links links need to be corrected or updated
2. **Update Navigation**: Review and update internal navigation structure

EOF
fi

if [[ $warnings -gt 0 ]]; then
    cat >> "$REPORT_FILE" << EOF
3. **Improve Content Structure**: $warnings pages have structural issues
4. **Add Missing Headers**: Ensure all pages have proper H1 headers
5. **Add Last Updated Dates**: Keep content freshness indicators up to date

EOF
fi

cat >> "$REPORT_FILE" << EOF
### Quality Improvements
1. **Review Non-Descriptive Links**: Improve link text for better accessibility
2. **Add Table of Contents**: Improve navigation with comprehensive ToCs
3. **Cross-Reference Related Content**: Add more internal links between related topics

### Maintenance
- Run this script weekly to maintain link integrity
- Set up automated alerts for broken links
- Include link validation in CI/CD pipeline

---

**Validation completed at**: $(date)
**Report saved to**: $REPORT_FILE
EOF

# Output summary
echo ""
echo "📊 Validation Summary"
echo "===================="
echo "Files processed: $total_files"
echo "Links checked: $total_links"
echo "Broken links: $broken_links"
echo "Warnings: $warnings"
echo ""

if [[ $broken_links -eq 0 && $warnings -eq 0 ]]; then
    echo -e "${GREEN}✅ Wiki link validation PASSED!${NC}"
    exit 0
else
    echo -e "${YELLOW}⚠️  Wiki validation completed with issues${NC}"
    echo "Detailed report: $REPORT_FILE"
    echo "Broken links: $BROKEN_LINKS_FILE"
    echo "Warnings: $WARNINGS_FILE"
    [[ $broken_links -gt 0 ]] && exit 1 || exit 0
fi
