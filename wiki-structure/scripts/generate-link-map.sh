#!/bin/bash

# generate-link-map.sh - Generate comprehensive internal link mapping
# Part of TIXL-066 Wiki Stabilization

set -euo pipefail

# Configuration
WIKI_ROOT="${1:-./wiki-structure}"
OUTPUT_FILE="${WIKI_ROOT}/internal-link-map.md"
WIKI_BASE_URL="https://github.com/tixl3d/tixl/wiki"

# Colors
BLUE='\033[0;34m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo "🗺️  TiXL Wiki Internal Link Map Generator"
echo "========================================="
echo "Generating link map for: $WIKI_ROOT"
echo ""

# Initialize output file
cat > "$OUTPUT_FILE" << EOF
# TiXL Wiki Internal Link Map

**Generated**: $(date)
**Total Wiki Pages**: $(find "$WIKI_ROOT" -name "*.md" -type f | wc -l)

## Overview

This document provides a comprehensive map of all internal links within the TiXL wiki. It helps identify:
- Content discoverability paths
- Cross-references between sections
- Orphaned pages (no incoming links)
- Popular reference pages (many incoming links)
- Potential navigation improvements

## Table of Contents

EOF

# Function to get page title from H1
get_page_title() {
    local file="$1"
    local title
    title=$(grep "^# " "$file" | sed 's/^# //' | head -1)
    if [[ -z "$title" ]]; then
        title=$(basename "$file" .md)
    fi
    echo "$title"
}

# Function to get page description from first paragraph
get_page_description() {
    local file="$1"
    local description
    description=$(grep -A 5 "^# " "$file" | grep -v "^#" | grep -v "^$" | head -1 | sed 's/^[*-] //')
    if [[ -z "$description" ]]; then
        description="No description available"
    fi
    echo "$description"
}

# Function to get wiki URL from file path
get_wiki_url() {
    local file="$1"
    local relative_path
    relative_path=$(realpath --relative-to="$WIKI_ROOT" "$file")
    local wiki_path
    wiki_path=$(echo "$relative_path" | sed 's/\.md$//' | sed 's/ /-/g')
    echo "$WIKI_BASE_URL/$wiki_path"
}

# Function to count links
count_links() {
    local file="$1"
    grep -o '\[[^\]]*\]([^)]*)' "$file" | wc -l
}

# Function to extract all internal links
extract_internal_links() {
    local file="$1"
    grep -o '\[[^\]]*\](([^)]*\.md)[^)]*)' "$file" 2>/dev/null | sed 's/\[[^\]]*\]((\([^)]*\)[^)]*)/\1/' || true
}

# Function to get file category
get_category() {
    local file="$1"
    local relative_path
    relative_path=$(realpath --relative-to="$WIKI_ROOT" "$file")
    
    case "$relative_path" in
        Home.md) echo "🏠 Home" ;;
        Search-Optimization.md) echo "🔧 Maintenance" ;;
        */Getting-Started/*) echo "🚀 Getting Started" ;;
        */Architecture/*) echo "🏗️ Architecture" ;;
        */Development/*) echo "👨‍💻 Development" ;;
        */Security/*) echo "🔒 Security" ;;
        */Operators/*) echo "🎛️ Operators" ;;
        */User-Guide/*) echo "📖 User Guide" ;;
        */Community/*) echo "👥 Community" ;;
        */Research-Analysis/*) echo "📊 Research & Analysis" ;;
        */Implementation/*) echo "📋 Implementation" ;;
        */Tools-Automation/*) echo "🛠️ Tools & Automation" ;;
        */Best-Practices/*) echo "⭐ Best Practices" ;;
        *) echo "📄 Other" ;;
    esac
}

echo "🔍 Analyzing pages..."

# First pass: collect all page information
declare -A page_info
declare -A outgoing_links
declare -A incoming_links

# Process all pages
while IFS= read -r file; do
    local relative_path
    relative_path=$(realpath --relative-to="$WIKI_ROOT" "$file")
    local page_id
    page_id=$(echo "$relative_path" | sed 's/\.md$//')
    
    local title
    title=$(get_page_title "$file")
    local description
    description=$(get_page_description "$file")
    local category
    category=$(get_category "$file")
    local link_count
    link_count=$(count_links "$file")
    local wiki_url
    wiki_url=$(get_wiki_url "$file")
    
    page_info["$page_id"]="$title|$description|$category|$link_count|$wiki_url|$file"
    
    # Extract outgoing links
    while IFS= read -r link; do
        if [[ -n "$link" ]]; then
            local link_file
            link_file=$(echo "$link" | sed 's/.*(\([^)]*\.md\).*/\1/')
            local link_page_id
            link_page_id=$(echo "$link_file" | sed 's/\.md$//')
            
            outgoing_links["$page_id"]="${outgoing_links[$page_id]}$link_page_id "
            incoming_links["$link_page_id"]="${incoming_links[$link_page_id]}$page_id "
        fi
    done < <(extract_internal_links "$file")
    
    echo "  ✓ Processed: $title"
    
done < <(find "$WIKI_ROOT" -name "*.md" -type f | sort)

echo ""
echo "📈 Generating link analysis..."

# Generate table of contents
cat >> "$OUTPUT_FILE" << EOF
- [Overview](#overview)
- [Page Directory](#page-directory)
- [Link Analysis](#link-analysis)
  - [Most Referenced Pages](#most-referenced-pages)
  - [Most Linking Pages](#most-linking-pages)
  - [Orphaned Pages](#orphaned-pages)
- [Category Map](#category-map)
- [Cross-Reference Matrix](#cross-reference-matrix)

EOF

# Generate page directory
cat >> "$OUTPUT_FILE" << EOF
## Page Directory

Total pages: $(echo "${!page_info[@]}" | wc -w)

| Page | Category | Links Out | Links In | Status |
|------|----------|-----------|----------|--------|
EOF

for page_id in $(echo "${!page_info[@]}" | tr ' ' '\n' | sort); do
    IFS='|' read -r title description category link_count wiki_url file <<< "${page_info[$page_id]}"
    
    local incoming_count
    incoming_count=$(echo "${incoming_links[$page_id]}" | wc -w)
    
    local status="✅"
    if [[ $incoming_count -eq 0 ]]; then
        status="🔴 Orphaned"
    elif [[ $incoming_count -lt 3 ]]; then
        status="🟡 Isolated"
    elif [[ $link_count -lt 2 ]]; then
        status="🟠 Minimal Links"
    fi
    
    echo "| [$title]($wiki_url) | $category | $link_count | $incoming_count | $status |" >> "$OUTPUT_FILE"
done

# Generate link analysis
echo "" >> "$OUTPUT_FILE"
cat >> "$OUTPUT_FILE" << EOF

## Link Analysis

### Most Referenced Pages
*Pages that other pages link to most frequently*

EOF

# Find most referenced pages
declare -A link_frequency
for page_id in $(echo "${!incoming_links[@]}"); do
    local count
    count=$(echo "${incoming_links[$page_id]}" | wc -w)
    link_frequency["$page_id"]=$count
done

sorted_pages=($(for page_id in "${!link_frequency[@]}"; do
    echo "$page_id ${link_frequency[$page_id]}"
done | sort -k2 -nr | cut -d' ' -f1))

for i in {0..9}; do
    if [[ $i -lt ${#sorted_pages[@]} ]]; then
        page_id="${sorted_pages[$i]}"
        if [[ -n "${link_frequency[$page_id]}" && "${link_frequency[$page_id]}" -gt 0 ]]; then
            IFS='|' read -r title description category link_count wiki_url file <<< "${page_info[$page_id]}"
            echo "$((i+1)). **[$title]($wiki_url)** - Referenced ${link_frequency[$page_id]} times" >> "$OUTPUT_FILE"
        fi
    fi
done

echo "" >> "$OUTPUT_FILE"
cat >> "$OUTPUT_FILE" << EOF

### Most Linking Pages
*Pages that link to other pages most frequently*

EOF

# Find most linking pages
declare -A link_out_frequency
for page_id in $(echo "${!outgoing_links[@]}"); do
    local count
    count=$(echo "${outgoing_links[$page_id]}" | wc -w)
    link_out_frequency["$page_id"]=$count
done

sorted_out_pages=($(for page_id in "${!link_out_frequency[@]}"; do
    echo "$page_id ${link_out_frequency[$page_id]}"
done | sort -k2 -nr | cut -d' ' -f1))

for i in {0..9}; do
    if [[ $i -lt ${#sorted_out_pages[@]} ]]; then
        page_id="${sorted_out_pages[$i]}"
        if [[ -n "${link_out_frequency[$page_id]}" && "${link_out_frequency[$page_id]}" -gt 0 ]]; then
            IFS='|' read -r title description category link_count wiki_url file <<< "${page_info[$page_id]}"
            echo "$((i+1)). **[$title]($wiki_url)** - Links to ${link_out_frequency[$page_id]} pages" >> "$OUTPUT_FILE"
        fi
    fi
done

echo "" >> "$OUTPUT_FILE"
cat >> "$OUTPUT_FILE" << EOF

### Orphaned Pages
*Pages with no incoming links (may need better discoverability)*

EOF

orphaned_count=0
for page_id in "${!incoming_links[@]}"; do
    local count
    count=$(echo "${incoming_links[$page_id]}" | wc -w)
    if [[ $count -eq 0 ]]; then
        IFS='|' read -r title description category link_count wiki_url file <<< "${page_info[$page_id]}"
        echo "- **[$title]($wiki_url)** - $category" >> "$OUTPUT_FILE"
        ((orphaned_count++)) || true
    fi
done

if [[ $orphaned_count -eq 0 ]]; then
    echo "✅ No orphaned pages found!" >> "$OUTPUT_FILE"
fi

echo "" >> "$OUTPUT_FILE"
cat >> "$OUTPUT_FILE" << EOF

## Category Map

Link distribution across categories:

EOF

# Generate category statistics
declare -A category_stats
for page_id in "${!page_info[@]}"; do
    IFS='|' read -r title description category link_count wiki_url file <<< "${page_info[$page_id]}"
    
    category_stats["${category}_count"]=$((${category_stats["${category}_count"]:-0} + 1))
    category_stats["${category}_links"]=$((${category_stats["${category}_links"]:-0} + link_count))
done

for category_key in $(echo "${!category_stats[@]}" | tr ' ' '\n' | grep '_count$' | sed 's/_count$//' | sort -u); do
    local count=${category_stats["${category_key}_count"]}
    local links=${category_stats["${category_key}_links"]}
    echo "- **$category_key**: $count pages, $links total outbound links" >> "$OUTPUT_FILE"
done

echo "" >> "$OUTPUT_FILE"
cat >> "$OUTPUT_FILE" << EOF

## Cross-Reference Matrix

Inter-category linking patterns:

EOF

# Generate cross-reference matrix
declare -A category_cross_refs
for page_id in "${!page_info[@]}"; do
    IFS='|' read -r title description page_category link_count wiki_url file <<< "${page_info[$page_id]}"
    
    local outgoing
    outgoing="${outgoing_links[$page_id]}"
    for target_page in $outgoing; do
        if [[ -n "${page_info[$target_page]:-}" ]]; then
            IFS='|' read -r target_title target_description target_category target_link_count target_wiki_url target_file <<< "${page_info[$target_page]}"
            
            if [[ "$page_category" != "$target_category" ]]; then
                local ref_key="${page_category}->${target_category}"
                category_cross_refs["$ref_key"]=$((${category_cross_refs["$ref_key"]:-0} + 1))
            fi
        fi
    done
done

for ref_key in $(echo "${!category_cross_refs[@]}" | sort); then
    local count=${category_cross_refs[$ref_key]}
    if [[ $count -gt 0 ]]; then
        echo "- **$ref_key**: $count cross-references" >> "$OUTPUT_FILE"
    fi
done

# Add recommendations
echo "" >> "$OUTPUT_FILE"
cat >> "$OUTPUT_FILE" << EOF

## Recommendations

### Content Discoverability
EOF

if [[ $orphaned_count -gt 0 ]]; then
    cat >> "$OUTPUT_FILE" << EOF
- **Fix Orphaned Pages**: $orphaned_count pages have no incoming links
- **Improve Navigation**: Add links from hub pages to isolated content
- **Cross-Reference**: Add related content sections to orphaned pages

EOF
fi

# Find under-linked categories
declare -A category_link_density
for page_id in "${!page_info[@]}"; do
    IFS='|' read -r title description category link_count wiki_url file <<< "${page_info[$page_id]}"
    if [[ $link_count -lt 3 ]]; then
        category_link_density["$category"]=$((${category_link_density["$category"]:-0} + 1))
    fi
done

underlinked_categories=0
for category in "${!category_link_density[@]}"; do
    if [[ ${category_link_density[$category]} -gt 0 ]]; then
        underlinked_categories=$((underlinked_categories + 1))
        cat >> "$OUTPUT_FILE" << EOF
- **Improve $category Links**: ${category_link_density[$category]} pages need more internal links
- **Add Navigation Menus**: Create better category navigation
- **Cross-Link Related Content**: Increase discoverability within category

EOF
    fi
done

cat >> "$OUTPUT_FILE" << EOF

### Navigation Improvements
1. **Hub Page Creation**: Create central pages for major topics
2. **Breadcrumb Navigation**: Add clear navigation paths
3. **Related Content Sections**: Auto-generate "Related Pages" sections
4. **Search Optimization**: Ensure all key terms are covered

### Maintenance
- Update this map monthly to track link health
- Monitor orphaned pages and fix discoverability
- Track cross-category linking patterns
- Use this data to improve content organization

---

**Link Map Generation Complete**
**Total Links Mapped**: $(grep -o '\[[^\]]*\]([^)]*)' "$WIKI_ROOT"/*.md "$WIKI_ROOT"/*/*.md 2>/dev/null | wc -l)
**Generated**: $(date)
EOF

# Output summary
echo ""
echo "📈 Link Map Summary"
echo "==================="
echo "Total pages: $(echo "${!page_info[@]}" | wc -w)"
echo "Total links: $(grep -o '\[[^\]]*\]([^)]*)' "$WIKI_ROOT"/*.md "$WIKI_ROOT"/*/*.md 2>/dev/null | wc -l)"
echo "Orphaned pages: $orphaned_count"
echo "Categories: $(echo "${!category_stats[@]}" | tr ' ' '\n' | grep '_count$' | wc -l)"
echo ""

if [[ $orphaned_count -eq 0 ]]; then
    echo -e "${GREEN}✅ No orphaned pages found!${NC}"
else
    echo -e "${YELLOW}⚠️  Found $orphaned_count orphaned pages${NC}"
fi

echo ""
echo "📋 Link map saved to: $OUTPUT_FILE"

if [[ $orphaned_count -gt 0 ]]; then
    echo "🔴 Priority: Fix orphaned pages for better discoverability"
    exit 1
else
    echo "✅ Link mapping complete with good internal connectivity"
    exit 0
fi
