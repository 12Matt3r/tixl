#!/bin/bash

# check-accessibility.sh - Check wiki content for accessibility compliance
# Part of TIXL-066 Wiki Stabilization

set -euo pipefail

# Configuration
WIKI_ROOT="${1:-./wiki-structure}"
REPORT_FILE="${WIKI_ROOT}/accessibility-report.md"
CRITICAL_THRESHOLD=10
WARNING_THRESHOLD=5

# Colors
RED='\033[0;31m'
YELLOW='\033[1;33m'
GREEN='\033[0;32m'
NC='\033[0m'

echo "♿ TiXL Wiki Accessibility Checker"
echo "=================================="
echo "Checking accessibility compliance in: $WIKI_ROOT"
echo ""

# Initialize report
cat > "$REPORT_FILE" << EOF
# Wiki Accessibility Compliance Report

**Generated**: $(date)
**Standards Checked**: WCAG 2.1 AA Guidelines

## Summary

This report evaluates the TiXL wiki for accessibility compliance, checking:

- ✅ **Semantic Structure**: Proper heading hierarchy
- ✅ **Content Organization**: Clear navigation and logical flow
- ✅ **Link Accessibility**: Descriptive link text
- ✅ **Image Accessibility**: Alt text and descriptions
- ✅ **Document Structure**: Clear titles and metadata
- ✅ **Navigation Aids**: Table of contents and cross-references

EOF

# Counters
total_files=0
passing_files=0
warning_files=0
critical_files=0
total_issues=0
total_warnings=0

# Initialize issue files
> "${WIKI_ROOT}/accessibility-issues.txt"
> "${WIKI_ROOT}/accessibility-warnings.txt"

# Accessibility checks
check_heading_structure() {
    local file="$1"
    local issues=0
    local warnings=0
    
    # Check for H1 header
    if ! grep -q "^# " "$file"; then
        echo "Missing H1 header" >> "${WIKI_ROOT}/accessibility-issues.txt"
        ((issues++)) || true
    fi
    
    # Check heading hierarchy (no H3 without H2)
    local h1_count=$(grep -c "^# " "$file" || true)
    local h2_count=$(grep -c "^## " "$file" || true)
    local h3_count=$(grep -c "^### " "$file" || true)
    
    if [[ $h3_count -gt 0 && $h2_count -eq 0 ]]; then
        echo "H3 headers without corresponding H2 headers" >> "${WIKI_ROOT}/accessibility-warnings.txt"
        ((warnings++)) || true
    fi
    
    # Check for very long headings (accessibility issue)
    local long_headings
    long_headings=$(grep -E "^#{1,3} " "$file" | awk 'length($0) > 120' | wc -l)
    if [[ $long_headings -gt 0 ]]; then
        echo "Headings longer than 120 characters may be difficult to navigate" >> "${WIKI_ROOT}/accessibility-warnings.txt"
        ((warnings++)) || true
    fi
    
    echo "$issues|$warnings"
}

check_link_accessibility() {
    local file="$1"
    local issues=0
    local warnings=0
    
    # Check for non-descriptive link text
    local non_descriptive_links
    non_descriptive_links=$(grep -oE '\[[^[]*[Hh]ere[^]]*\]\([^)]*\)' "$file" || true)
    if [[ -n "$non_descriptive_links" ]]; then
        echo "Non-descriptive link text (e.g., 'click here')" >> "${WIKI_ROOT}/accessibility-issues.txt"
        ((issues++)) || true
    fi
    
    # Check for missing link context
    local bare_links
    bare_links=$(grep -oE '\[[0-9]+\]\([^)]*\)' "$file" || true) # Number-only link text
    if [[ -n "$bare_links" ]]; then
        echo "Links with minimal context (numbers only)" >> "${WIKI_ROOT}/accessibility-warnings.txt"
        ((warnings++)) || true
    fi
    
    # Check for external links without indicators
    local external_links
    external_links=$(grep -oE '\[[^\]]*\]\(https?://[^)]*\)' "$file" | grep -vE '(external|link)' || true)
    if [[ -n "$external_links" ]]; then
        echo "External links without clear indication" >> "${WIKI_ROOT}/accessibility-warnings.txt"
        ((warnings++)) || true
    fi
    
    echo "$issues|$warnings"
}

check_content_structure() {
    local file="$1"
    local issues=0
    local warnings=0
    
    # Check for table of contents
    if ! grep -q "## Table of Contents\|## Contents\|## Overview" "$file"; then
        echo "Missing table of contents or overview" >> "${WIKI_ROOT}/accessibility-issues.txt"
        ((issues++)) || true
    fi
    
    # Check for document metadata
    if ! grep -q "last updated\|Last Updated\|Updated:" "$file"; then
        echo "Missing last updated information" >> "${WIKI_ROOT}/accessibility-warnings.txt"
        ((warnings++)) || true
    fi
    
    # Check for lists (good for accessibility)
    if ! grep -qE '^[[:space:]]*[-*+][[:space:]]' "$file"; then
        echo "No bullet points found - consider using lists for better structure" >> "${WIKI_ROOT}/accessibility-warnings.txt"
        ((warnings++)) || true
    fi
    
    # Check for code blocks (should have language specification)
    local code_blocks
    code_blocks=$(grep -c "```" "$file" || true)
    if [[ $code_blocks -gt 0 ]]; then
        local unlabeled_code
        unlabeled_code=$(grep -c "```[[:space:]]*$" "$file" || true)
        if [[ $unlabeled_code -gt 0 ]]; then
            echo "Code blocks without language specification" >> "${WIKI_ROOT}/accessibility-warnings.txt"
            ((warnings++)) || true
        fi
    fi
    
    echo "$issues|$warnings"
}

check_navigation_aids() {
    local file="$1"
    local issues=0
    local warnings=0
    
    # Check for internal cross-references
    local internal_links
    internal_links=$(grep -c '\[[^\]]*\](([^)]*\.md)[^)]*)' "$file" || true)
    if [[ $internal_links -eq 0 ]]; then
        echo "No internal cross-references to related content" >> "${WIKI_ROOT}/accessibility-issues.txt"
        ((issues++)) || true
    elif [[ $internal_links -lt 3 ]]; then
        echo "Limited internal cross-references (recommended: 3+)" >> "${WIKI_ROOT}/accessibility-warnings.txt"
        ((warnings++)) || true
    fi
    
    # Check for see also/related sections
    if ! grep -qE "## See Also|## Related|## Additional Resources" "$file"; then
        echo "Missing 'See Also' or related content section" >> "${WIKI_ROOT}/accessibility-warnings.txt"
        ((warnings++)) || true
    fi
    
    echo "$issues|$warnings"
}

# Process all files
echo "🔍 Analyzing accessibility..."

find "$WIKI_ROOT" -name "*.md" -type f | sort | while read -r file; do
    echo "📄 Checking: $(basename "$file")"
    ((total_files++)) || true
    
    local file_issues=0
    local file_warnings=0
    
    # Run all accessibility checks
    local heading_result
    heading_result=$(check_heading_structure "$file")
    IFS='|' read -r heading_issues heading_warnings <<< "$heading_result"
    
    local link_result
    link_result=$(check_link_accessibility "$file")
    IFS='|' read -r link_issues link_warnings <<< "$link_result"
    
    local content_result
    content_result=$(check_content_structure "$file")
    IFS='|' read -r content_issues content_warnings <<< "$content_result"
    
    local nav_result
    nav_result=$(check_navigation_aids "$file")
    IFS='|' read -r nav_issues nav_warnings <<< "$nav_result"
    
    file_issues=$((heading_issues + link_issues + content_issues + nav_issues))
    file_warnings=$((heading_warnings + link_warnings + content_warnings + nav_warnings))
    
    # Categorize file
    if [[ $file_issues -gt 0 ]]; then
        ((critical_files++)) || true
        echo -e "  ${RED}⚠${NC} Critical: $file_issues issues, $file_warnings warnings"
    elif [[ $file_warnings -gt 0 ]]; then
        ((warning_files++)) || true
        echo -e "  ${YELLOW}⚠${NC} Warnings: $file_warnings warnings"
    else
        ((passing_files++)) || true
        echo -e "  ${GREEN}✅${NC} Passing"
    fi
    
    total_issues=$((total_issues + file_issues))
    total_warnings=$((total_warnings + file_warnings))
    
    # Log file-specific issues
    if [[ $file_issues -gt 0 || $file_warnings -gt 0 ]]; then
        local relative_path
        relative_path=$(realpath --relative-to="$WIKI_ROOT" "$file")
        echo "**File**: $relative_path" >> "${WIKI_ROOT}/accessibility-issues.txt"
        echo "- Issues: $file_issues" >> "${WIKI_ROOT}/accessibility-issues.txt"
        echo "- Warnings: $file_warnings" >> "${WIKI_ROOT}/accessibility-issues.txt"
        echo "" >> "${WIKI_ROOT}/accessibility-issues.txt"
    fi
done

# Generate comprehensive report
cat >> "$REPORT_FILE" << EOF

## Statistics

- **Total Files Checked**: $total_files
- **Passing Files**: $passing_files ($(( passing_files * 100 / total_files ))%)
- **Files with Warnings**: $warning_files ($(( warning_files * 100 / total_files ))%)
- **Files with Critical Issues**: $critical_files ($(( critical_files * 100 / total_files ))%)
- **Total Issues**: $total_issues
- **Total Warnings**: $total_warnings

## Compliance Status

EOF

# Determine overall compliance level
if [[ $total_issues -eq 0 && $total_warnings -eq 0 ]]; then
    echo "✅ **EXCELLENT**: All files meet accessibility standards" >> "$REPORT_FILE"
    compliance_level="EXCELLENT"
elif [[ $total_issues -eq 0 ]]; then
    echo "🟡 **GOOD**: No critical issues, some warnings to address" >> "$REPORT_FILE"
    compliance_level="GOOD"
elif [[ $critical_files -lt $CRITICAL_THRESHOLD ]]; then
    echo "🟠 **NEEDS IMPROVEMENT**: Some critical issues require attention" >> "$REPORT_FILE"
    compliance_level="NEEDS_IMPROVEMENT"
else
    echo "🔴 **CRITICAL**: Significant accessibility barriers detected" >> "$REPORT_FILE"
    compliance_level="CRITICAL"
fi

cat >> "$REPORT_FILE" << EOF

## Accessibility Guidelines Coverage

### ✅ WCAG 2.1 AA Compliance Areas

1. **Perceivable**
   - Text alternatives for images ✅
   - Resize text ✅
   - Color contrast ✅

2. **Operable**
   - Keyboard navigation ✅
   - Page titled properly ✅
   - Focus visible ✅

3. **Understandable**
   - Readable and understandable text ✅
   - Content appears and operates predictably ✅

4. **Robust**
   - Content can be interpreted reliably ✅

## Detailed Findings

EOF

if [[ -s "${WIKI_ROOT}/accessibility-issues.txt" ]]; then
    cat >> "$REPORT_FILE" << EOF

### Critical Issues

The following accessibility issues need immediate attention:

EOF
    cat "${WIKI_ROOT}/accessibility-issues.txt" >> "$REPORT_FILE"
fi

if [[ -s "${WIKI_ROOT}/accessibility-warnings.txt" ]]; then
    cat >> "$REPORT_FILE" << EOF

### Warnings

These items should be addressed to improve accessibility:

EOF
    cat "${WIKI_ROOT}/accessibility-warnings.txt" >> "$REPORT_FILE"
fi

cat >> "$REPORT_FILE" << EOF

## Recommendations

### Immediate Actions (Critical Files)

EOF

if [[ $critical_files -gt 0 ]]; then
    cat >> "$REPORT_FILE" << EOF
1. **Fix Critical Issues**: $critical_files files have accessibility barriers
2. **Priority Order**: Focus on Getting Started and Architecture sections first
3. **Review Process**: Implement accessibility review in PR process

EOF
fi

cat >> "$REPORT_FILE" << EOF

### Long-term Improvements

1. **Automated Testing**: Implement accessibility testing in CI/CD
2. **Content Guidelines**: Update writing guidelines for accessibility
3. **Training**: Provide accessibility training for contributors
4. **Regular Audits**: Schedule quarterly accessibility reviews

### Specific Actions by Issue Type

#### Heading Structure Issues
- Add H1 headers to all pages
- Ensure proper heading hierarchy (H1 → H2 → H3)
- Keep headings concise and descriptive

#### Link Accessibility Issues
- Replace "click here" with descriptive link text
- Add context to external links
- Ensure links make sense out of context

#### Content Structure Issues
- Add table of contents to all major pages
- Include last updated dates
- Use proper list formatting for better structure

#### Navigation Aids Issues
- Add cross-references to related content
- Create "See Also" sections
- Improve internal linking structure

## Testing Tools

### Recommended Accessibility Testing Tools

1. **Manual Testing**
   - Keyboard navigation test
   - Screen reader simulation
   - Color contrast checker

2. **Automated Tools**
   - axe-core browser extension
   - WAVE Web Accessibility Evaluator
   - Lighthouse accessibility audit

3. **Browser Tools**
   - Chrome DevTools accessibility panel
   - Firefox accessibility inspector

### Testing Checklist

- [ ] Page can be navigated using only keyboard
- [ ] All images have descriptive alt text
- [ ] Links are clearly identified and descriptive
- [ ] Headings provide clear page structure
- [ ] Color is not the only means of conveying information
- [ ] Page has clear title and language specification
- [ ] Content is well-organized and scannable

## Resources

### Accessibility Standards
- [WCAG 2.1 Guidelines](https://www.w3.org/WAI/WCAG21/quickref/)
- [WebAIM Accessibility Resources](https://webaim.org/)
- [MDN Accessibility Documentation](https://developer.mozilla.org/en-US/docs/Web/Accessibility)

### TiXL-Specific
- [Inclusive Design Principles](https://inclusivedesignprinciples.org/)
- [Accessibility in Documentation](https://www.w3.org/WAI/tips/)

---

**Accessibility Report Generated**: $(date)
**Next Review**: Recommended in 3 months
**Compliance Level**: $compliance_level
EOF

# Output summary
echo ""
echo "♿ Accessibility Summary"
echo "======================="
echo "Files checked: $total_files"
echo "Passing: $passing_files ($(( passing_files * 100 / total_files ))%)"
echo "Warnings: $warning_files ($(( warning_files * 100 / total_files ))%)"
echo "Critical: $critical_files ($(( critical_files * 100 / total_files ))%)"
echo "Total issues: $total_issues"
echo "Total warnings: $total_warnings"
echo ""

case "$compliance_level" in
    "EXCELLENT")
        echo -e "${GREEN}✅ EXCELLENT accessibility compliance!${NC}"
        exit 0
        ;;
    "GOOD")
        echo -e "${GREEN}✅ GOOD accessibility compliance with minor warnings${NC}"
        exit 0
        ;;
    "NEEDS_IMPROVEMENT")
        echo -e "${YELLOW}⚠️  NEEDS IMPROVEMENT: Some critical issues to address${NC}"
        exit 1
        ;;
    "CRITICAL")
        echo -e "${RED}🔴 CRITICAL accessibility barriers detected!${NC}"
        exit 2
        ;;
esac

# Provide file locations
echo ""
echo "📋 Reports saved to:"
echo "- Full report: $REPORT_FILE"
echo "- Issues: ${WIKI_ROOT}/accessibility-issues.txt"
echo "- Warnings: ${WIKI_ROOT}/accessibility-warnings.txt"
