#!/bin/bash

# TIXL-099 Licensing Policies Verification Script
# This script verifies that all licensing policy components are properly implemented

set -e

echo "=============================================="
echo "TIXL-099 Licensing Policies Verification"
echo "=============================================="
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Counter for passed/failed tests
PASSED=0
FAILED=0

# Function to check if file exists and report status
check_file() {
    local file_path="$1"
    local description="$2"
    
    if [ -f "$file_path" ]; then
        echo -e "${GREEN}✅ $description${NC}"
        ((PASSED++))
        return 0
    else
        echo -e "${RED}❌ $description - File not found: $file_path${NC}"
        ((FAILED++))
        return 1
    fi
}

# Function to check if directory exists
check_directory() {
    local dir_path="$1"
    local description="$2"
    
    if [ -d "$dir_path" ]; then
        echo -e "${GREEN}✅ $description${NC}"
        ((PASSED++))
        return 0
    else
        echo -e "${RED}❌ $description - Directory not found: $dir_path${NC}"
        ((FAILED++))
        return 1
    fi
}

# Function to check file content
check_content() {
    local file_path="$1"
    local search_term="$2"
    local description="$3"
    
    if [ -f "$file_path" ] && grep -q "$search_term" "$file_path"; then
        echo -e "${GREEN}✅ $description${NC}"
        ((PASSED++))
        return 0
    else
        echo -e "${RED}❌ $description${NC}"
        ((FAILED++))
        return 1
    fi
}

echo "1. Checking Main Documentation Files"
echo "===================================="

check_file "docs/TIXL-099_Licensing_Policy_Framework.md" "Main licensing policy framework document"
check_file "docs/licensing-faq.md" "Licensing FAQ document"
check_content "docs/TIXL-099_Licensing_Policy_Framework.md" "MIT License" "Framework contains MIT license details"
check_content "docs/TIXL-099_Licensing_Policy_Framework.md" "Commercial Licensing" "Framework contains commercial licensing section"
check_content "docs/TIXL-099_Licensing_Policy_Framework.md" "Educational Licensing" "Framework contains educational licensing section"
check_content "docs/TIXL-099_Licensing_Policy_Framework.md" "Enterprise Licensing" "Framework contains enterprise licensing section"

echo ""
echo "2. Checking License Agreement Templates"
echo "======================================="

check_directory "docs/LICENSE_AGREEMENTS" "License agreements directory"
check_file "docs/LICENSE_AGREEMENTS/commercial-license-template.md" "Commercial license template"
check_file "docs/LICENSE_AGREEMENTS/educational-license-template.md" "Educational license template"
check_file "docs/LICENSE_AGREEMENTS/enterprise-license-template.md" "Enterprise license template"
check_file "docs/LICENSE_AGREEMENTS/individual-contributor-icla-template.md" "Individual contributor ICLA template"
check_file "docs/LICENSE_AGREEMENTS/corporate-contributor-ccla-template.md" "Corporate contributor CCLA template"
check_file "docs/LICENSE_AGREEMENTS/README.md" "License templates README"

echo ""
echo "3. Checking License Validator Tool"
echo "================================="

check_file "scripts/license-validator.py" "License validator Python script"
check_content "scripts/license-validator.py" "class LicenseValidator" "Validator contains LicenseValidator class"
check_content "scripts/license-validator.py" "def scan_files" "Validator contains file scanning method"
check_content "scripts/license-validator.py" "def scan_dependencies" "Validator contains dependency scanning"
check_content "scripts/license-validator.py" "def generate_report" "Validator contains report generation"
check_content "scripts/license-validator.py" "MIT" "Validator recognizes MIT license"
check_content "scripts/license-validator.py" "Apache-2.0" "Validator recognizes Apache 2.0 license"

echo ""
echo "4. Checking GitHub Workflow"
echo "=========================="

check_file ".github/workflows/license-compliance.yml" "License compliance GitHub workflow"
check_content ".github/workflows/license-compliance.yml" "license-validation" "Workflow contains license validation job"
check_content ".github/workflows/license-compliance.yml" "dependency-scanning" "Workflow contains dependency scanning job"
check_content ".github/workflows/license-compliance.yml" "license-compatibility" "Workflow contains license compatibility check"
check_content ".github/workflows/license-compliance.yml" "compliance-gating" "Workflow contains compliance gating"

echo ""
echo "5. Checking Implementation Summary"
echo "================================="

check_file "TIXL-099_Licensing_Policies_Implementation_Summary.md" "Implementation summary document"

echo ""
echo "6. Testing License Validator Functionality"
echo "========================================="

if [ -f "scripts/license-validator.py" ]; then
    echo "Testing license validator with help command..."
    if python3 scripts/license-validator.py --help > /dev/null 2>&1; then
        echo -e "${GREEN}✅ License validator runs successfully${NC}"
        ((PASSED++))
    else
        echo -e "${RED}❌ License validator execution failed${NC}"
        ((FAILED++))
    fi
else
    echo -e "${RED}❌ License validator not found${NC}"
    ((FAILED++))
fi

echo ""
echo "7. Checking File Sizes and Completeness"
echo "======================================="

# Check main framework document size
if [ -f "docs/TIXL-099_Licensing_Policy_Framework.md" ]; then
    framework_size=$(wc -l < "docs/TIXL-099_Licensing_Policy_Framework.md")
    if [ "$framework_size" -gt 500 ]; then
        echo -e "${GREEN}✅ Framework document is comprehensive ($framework_size lines)${NC}"
        ((PASSED++))
    else
        echo -e "${YELLOW}⚠️  Framework document might be incomplete ($framework_size lines)${NC}"
        ((FAILED++))
    fi
fi

# Check license validator size
if [ -f "scripts/license-validator.py" ]; then
    validator_size=$(wc -l < "scripts/license-validator.py")
    if [ "$validator_size" -gt 500 ]; then
        echo -e "${GREEN}✅ License validator is comprehensive ($validator_size lines)${NC}"
        ((PASSED++))
    else
        echo -e "${YELLOW}⚠️  License validator might be incomplete ($validator_size lines)${NC}"
        ((FAILED++))
    fi
fi

# Check FAQ size
if [ -f "docs/licensing-faq.md" ]; then
    faq_size=$(wc -l < "docs/licensing-faq.md")
    if [ "$faq_size" -gt 400 ]; then
        echo -e "${GREEN}✅ FAQ document is comprehensive ($faq_size lines)${NC}"
        ((PASSED++))
    else
        echo -e "${YELLOW}⚠️  FAQ document might be incomplete ($faq_size lines)${NC}"
        ((FAILED++))
    fi
fi

echo ""
echo "8. Checking Content Quality"
echo "=========================="

# Check for key sections in framework
if [ -f "docs/TIXL-099_Licensing_Policy_Framework.md" ]; then
    key_sections=("License Structure" "Commercial Licensing" "Educational Licensing" "Enterprise Licensing" "Contributor Licensing" "Intellectual Property Management" "Compliance and Enforcement" "Legal Considerations" "Liability Protection" "Export Controls")
    
    for section in "${key_sections[@]}"; do
        check_content "docs/TIXL-099_Licensing_Policy_Framework.md" "$section" "Framework contains $section section"
    done
fi

# Check for key elements in license validator
if [ -f "scripts/license-validator.py" ]; then
    validator_elements=("LicenseInfo" "FileLicenseInfo" "DependencyInfo" "ComplianceReport" "scan_files" "scan_dependencies" "export_report")
    
    for element in "${validator_elements[@]}"; do
        check_content "scripts/license-validator.py" "$element" "Validator contains $element"
    done
fi

echo ""
echo "9. Integration Checks"
echo "===================="

# Check if framework references other TIXL documents
if [ -f "docs/TIXL-099_Licensing_Policy_Framework.md" ]; then
    if grep -q "TIXL" "docs/TIXL-099_Licensing_Policy_Framework.md"; then
        echo -e "${GREEN}✅ Framework references TiXL project${NC}"
        ((PASSED++))
    else
        echo -e "${YELLOW}⚠️  Framework might not reference TiXL project${NC}"
        ((FAILED++))
    fi
fi

# Check if workflow has proper triggers
if [ -f ".github/workflows/license-compliance.yml" ]; then
    if grep -q "push:" ".github/workflows/license-compliance.yml" && \
       grep -q "pull_request:" ".github/workflows/license-compliance.yml"; then
        echo -e "${GREEN}✅ Workflow has proper triggers${NC}"
        ((PASSED++))
    else
        echo -e "${YELLOW}⚠️  Workflow triggers might be incomplete${NC}"
        ((FAILED++))
    fi
fi

echo ""
echo "=============================================="
echo "VERIFICATION SUMMARY"
echo "=============================================="
echo -e "${GREEN}✅ Tests Passed: $PASSED${NC}"
echo -e "${RED}❌ Tests Failed: $FAILED${NC}"

if [ $FAILED -eq 0 ]; then
    echo ""
    echo -e "${GREEN}🎉 All TIXL-099 Licensing Policies components are properly implemented!${NC}"
    echo ""
    echo "The following deliverables have been successfully created:"
    echo "1. Comprehensive licensing policy framework"
    echo "2. License agreement templates for different use cases"
    echo "3. Automated license compliance validator"
    echo "4. Detailed licensing FAQ"
    echo "5. Automated compliance checking workflow"
    echo ""
    echo "Next steps:"
    echo "- Review all documentation for final approval"
    echo "- Deploy the GitHub workflow to the repository"
    echo "- Train maintainers on using the license validator"
    echo "- Begin community communication about new licensing framework"
    exit 0
else
    echo ""
    echo -e "${RED}⚠️  Some components are missing or incomplete. Please review and fix the issues above.${NC}"
    exit 1
fi
