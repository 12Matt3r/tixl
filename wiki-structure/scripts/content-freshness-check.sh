#!/bin/bash

# content-freshness-check.sh - Check for outdated wiki content
# Part of TIXL-066 Wiki Stabilization

set -euo pipefail

# Configuration
WIKI_ROOT="${1:-./wiki-structure}"
REPORT_FILE="${WIKI_ROOT}/content-freshness-report.md"
STALE_THRESHOLD_DAYS=180  # 6 months
WARNING_THRESHOLD_DAYS=90 # 3 months

# Colors
RED='\033[0;31m'
YELLOW='\033[1;33m'
GREEN='\033[0;32m'
NC='\033[0m'

echo "📅 TiXL Wiki Content Freshness Checker"
echo "======================================"
echo "Checking content freshness in: $WIKI_ROOT"
echo "Stale threshold: $STALE_THRESHOLD_DAYS days"
echo ""

# Initialize report
cat > "$REPORT_FILE" << EOF
# Wiki Content Freshness Report

**Generated**: $(date)
**Threshold**: $STALE_THRESHOLD_DAYS days since last update

## Summary

EOF

> "${WIKI_ROOT}/stale-content.txt"
> "${WIKI_ROOT}/recent-content.txt"
> "${WIKI_ROOT}/update-recommendations.txt"

total_files=0
stale_files=0
recent_files=0

# Function to extract last updated date
extract_last_updated() {
    local file="$1"
    
    # Try multiple patterns
    local patterns=(
        "last updated: ([0-9]{4}-[0-9]{2}-[0-9]{2})"
        "Last Updated: ([0-9]{4}-[0-9]{2}-[0-9]{2})"
        "Updated: ([0-9]{4}-[0-9]{2}-[0-9]{2})"
        "\*\*Last Updated\*\*: ([0-9]{4}-[0-9]{2}-[0-9]{2})"
        "## Document Information"
        "## Document History"
    )
    
    # Read file and check for patterns
    local content
    content=$(cat "$file")
    
    for pattern in "${patterns[@]}"; do
        if [[ "$pattern" == "## Document"* ]]; then
            # Special handling for document information sections
            if echo "$content" | grep -q "$pattern"; then
                # Extract date from the section
                local date_match
                date_match=$(echo "$content" | sed -n "/$pattern/,/^##/p" | grep -E "([0-9]{4}-[0-9]{2}-[0-9]{2})" | head -1 | sed -E 's/.*([0-9]{4}-[0-9]{2}-[0-9]{2}).*/\1/')
                if [[ -n "$date_match" ]]; then
                    echo "$date_match"
                    return
                fi
            fi
        else
            local date_match
            date_match=$(echo "$content" | grep -iE "$pattern" | sed -E 's/.*([0-9]{4}-[0-9]{2}-[0-9]{2}).*/\1/' | head -1)
            if [[ -n "$date_match" ]]; then
                echo "$date_match"
                return
            fi
        fi
    done
    
    # Fall back to file modification time
    stat -c %y "$file" | cut -d' ' -f1
}

# Function to check if content is stale
is_stale() {
    local file="$1"
    local last_updated="$2"
    
    if [[ ! "$last_updated" =~ ^[0-9]{4}-[0-9]{2}-[0-9]{2}$ ]]; then
        # Fallback to file modification time
        last_updated=$(stat -c %y "$file" | cut -d' ' -f1)
    fi
    
    local days_diff
    days_diff=$(( ($(date +%s) - $(date -d "$last_updated" +%s)) / 86400 ))
    
    if [[ $days_diff -gt $STALE_THRESHOLD_DAYS ]]; then
        return 0  # Is stale
    else
        return 1  # Not stale
    fi
}

# Function to get file category
get_file_category() {
    local file="$1"
    local relative_path
    relative_path=$(realpath --relative-to="$WIKI_ROOT" "$file")
    
    case "$relative_path" in
        Home.md) echo "Home" ;;
        Search-Optimization.md) echo "Maintenance" ;;
        */Getting-Started/*) echo "Getting Started" ;;
        */Architecture/*) echo "Architecture" ;;
        */Development/*) echo "Development" ;;
        */Security/*) echo "Security" ;;
        */Operators/*) echo "Operators" ;;
        */User-Guide/*) echo "User Guide" ;;
        */Community/*) echo "Community" ;;
        */Research-Analysis/*) echo "Research" ;;
        */Implementation/*) echo "Implementation" ;;
        */Tools-Automation/*) echo "Tools" ;;
        */Best-Practices/*) echo "Best Practices" ;;
        *) echo "Other" ;;
    esac
}

# Process all markdown files
find "$WIKI_ROOT" -name "*.md" -type f | sort | while read -r file; do
    echo "📄 Checking: $(basename "$file")"
    ((total_files++)) || true
    
    local last_updated
    last_updated=$(extract_last_updated "$file")
    
    local is_stale_result=0
    is_stale "$file" "$last_updated"
    is_stale_result=$?
    
    local category
    category=$(get_file_category "$file")
    
    if [[ $is_stale_result -eq 0 ]]; then
        ((stale_files++)) || true
        echo -e "  ${RED}⚠${NC} Stale ($last_updated) - Category: $category"
        echo "$file|$last_updated|$category|Stale" >> "${WIKI_ROOT}/stale-content.txt"
        
        # Generate recommendation
        local base_name
        base_name=$(basename "$file" .md)
        echo "- Review and update **$base_name** (last updated: $last_updated)" >> "${WIKI_ROOT}/update-recommendations.txt"
        
    else
        ((recent_files++)) || true
        echo -e "  ${GREEN}✓${NC} Recent ($last_updated) - Category: $category"
        echo "$file|$last_updated|$category|Recent" >> "${WIKI_ROOT}/recent-content.txt"
    fi
done

# Generate category summary
echo ""
echo "📊 Generating category summary..."

cat > "${WIKI_ROOT}/category-summary.txt" << EOF
Category Breakdown:
EOF

awk -F'|' '
{
    category[$2]++
    status[$3][$2]++
}
END {
    for (cat in category) {
        total = category[cat]
        stale = 0
        recent = 0
        if ("Stale" in status) stale = status["Stale"][cat] + 0
        if ("Recent" in status) recent = status["Recent"][cat] + 0
        
        printf "  %-20s Total: %2d  Recent: %2d  Stale: %2d\n", cat ":", total, recent, stale
    }
}' "${WIKI_ROOT}/stale-content.txt" "${WIKI_ROOT}/recent-content.txt" >> "${WIKI_ROOT}/category-summary.txt"

# Build final report
cat >> "$REPORT_FILE" << EOF

## Statistics

- **Total Files**: $total_files
- **Recent Content**: $recent_files
- **Stale Content**: $stale_files
- **Stale Percentage**: $(( stale_files * 100 / total_files ))%

## Content Categories

EOF

cat "${WIKI_ROOT}/category-summary.txt" >> "$REPORT_FILE"

cat >> "$REPORT_FILE" << EOF

## Stale Content Requiring Attention

EOF

if [[ -s "${WIKI_ROOT}/stale-content.txt" ]]; then
    cat >> "$REPORT_FILE" << EOF

The following content is older than $STALE_THRESHOLD_DAYS days and may need updating:

EOF
    
    while IFS='|' read -r file last_updated category status; do
        local display_path
        display_path=$(realpath --relative-to="$WIKI_ROOT" "$file")
        local base_name
        base_name=$(basename "$file" .md)
        echo "- **$base_name** ($category) - Last updated: $last_updated" >> "$REPORT_FILE"
        echo "  - Path: \`$display_path\`" >> "$REPORT_FILE"
        echo "" >> "$REPORT_FILE"
    done < "${WIKI_ROOT}/stale-content.txt"
else
    echo "✅ All content is recent!" >> "$REPORT_FILE"
fi

cat >> "$REPORT_FILE" << EOF

## Update Recommendations

EOF

if [[ -s "${WIKI_ROOT}/update-recommendations.txt" ]]; then
    cat "${WIKI_ROOT}/update-recommendations.txt" >> "$REPORT_FILE"
else
    echo "✅ No immediate update recommendations!" >> "$REPORT_FILE"
fi

cat >> "$REPORT_FILE" << EOF

## Action Items

### Immediate Actions (Next Sprint)
EOF

if [[ $stale_files -gt 0 ]]; then
    cat >> "$REPORT_FILE" << EOF
1. **Review Stale Content**: $stale_files files need review
2. **Update Critical Documentation**: Prioritize Getting Started and Architecture sections
3. **Schedule Content Refresh**: Plan systematic updates for old content

EOF
fi

cat >> "$REPORT_FILE" << EOF

### Long-term Improvements
1. **Implement Update Reminders**: Automatic notifications for content owners
2. **Add Update Tracking**: Better metadata for tracking content freshness
3. **Set Content Review Schedule**: Regular review cycles for different content types
4. **Add Content Owner Assignment**: Assign ownership for systematic updates

### Automation Opportunities
1. **Weekly Freshness Checks**: Automated reporting
2. **GitHub Actions Integration**: Automatic alerts for stale content
3. **Dashboard Creation**: Visual representation of content health

---

**Generated**: $(date)
**Report Location**: $REPORT_FILE
**Next Check**: Recommended in 1 week
EOF

# Output summary
echo ""
echo "📅 Freshness Check Summary"
echo "========================="
echo "Total files: $total_files"
echo "Recent: $recent_files"
echo "Stale: $stale_files"
echo ""

if [[ $stale_files -eq 0 ]]; then
    echo -e "${GREEN}✅ All content is fresh!${NC}"
    exit 0
else
    echo -e "${YELLOW}⚠️  Found $stale_files stale files${NC}"
    echo "Full report: $REPORT_FILE"
    echo "Stale content list: ${WIKI_ROOT}/stale-content.txt"
    echo "Update recommendations: ${WIKI_ROOT}/update-recommendations.txt"
    
    # Calculate priority
    local stale_percentage=$(( stale_files * 100 / total_files ))
    if [[ $stale_percentage -gt 25 ]]; then
        echo -e "${RED}🚨 High priority: $stale_percentage% of content is stale${NC}"
        exit 2
    else
        echo -e "${YELLOW}📋 Medium priority: $stale_percentage% of content is stale${NC}"
        exit 1
    fi
fi
