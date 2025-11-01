#!/usr/bin/env python3
"""
TiXL Triage Summary Generator
Generates vulnerability triage summaries and GitHub issue summaries
"""

import json
import sys
import argparse
from datetime import datetime
from pathlib import Path
from typing import Dict, List, Any, Optional
import logging
import re

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

def generate_triage_summary(input_dir: Path, output_file: Path, github_token: str = None, repo: str = None) -> None:
    """Generate vulnerability triage summary"""
    logger.info(f"Generating triage summary from {input_dir}")
    
    # Load vulnerability data
    vulnerabilities = []
    analysis_data = {}
    
    vuln_file = input_dir / "vulnerabilities.json"
    if vuln_file.exists():
        with open(vuln_file, 'r', encoding='utf-8') as f:
            vulnerabilities = json.load(f)
    
    analysis_file = input_dir / "analysis.json"
    if analysis_file.exists():
        with open(analysis_file, 'r', encoding='utf-8') as f:
            analysis_data = json.load(f)
    
    # Generate triage summary
    triage_content = generate_triage_content(vulnerabilities, analysis_data, github_token, repo)
    
    # Write to file
    output_file.parent.mkdir(parents=True, exist_ok=True)
    with open(output_file, 'w', encoding='utf-8') as f:
        f.write(triage_content)
    
    logger.info(f"Triage summary written to {output_file}")

def generate_triage_content(vulnerabilities: List[Dict], analysis_data: Dict, github_token: str, repo: str) -> str:
    """Generate triage summary content"""
    
    # Calculate statistics
    severity_counts = {}
    tool_counts = {}
    category_counts = {}
    
    for vuln in vulnerabilities:
        severity = vuln.get('severity', 'unknown')
        tool = vuln.get('tool', 'unknown')
        category = vuln.get('category', 'unknown')
        
        severity_counts[severity] = severity_counts.get(severity, 0) + 1
        tool_counts[tool] = tool_counts.get(tool, 0) + 1
        category_counts[category] = category_counts.get(category, 0) + 1
    
    # Generate triage summary
    content = []
    content.extend([
        "# TiXL Security Vulnerability Triage Summary",
        "",
        f"**Generated**: {datetime.utcnow().strftime('%Y-%m-%d %H:%M:%S UTC')}",
        f"**Total Vulnerabilities**: {len(vulnerabilities)}",
        "",
        "## 📊 Summary Statistics",
        "",
        "### By Severity"
    ])
    
    for severity in ['critical', 'high', 'medium', 'low']:
        count = severity_counts.get(severity, 0)
        if count > 0:
            emoji = {'critical': '🔴', 'high': '🟠', 'medium': '🟡', 'low': '🟢'}.get(severity, '⚪')
            content.append(f"- {emoji} **{severity.title()}**: {count}")
    
    content.extend([
        "",
        "### By Category"
    ])
    
    for category, count in sorted(category_counts.items()):
        if count > 0:
            content.append(f"- **{category}**: {count}")
    
    content.extend([
        "",
        "### By Tool"
    ])
    
    for tool, count in sorted(tool_counts.items()):
        if count > 0:
            content.append(f"- **{tool}**: {count}")
    
    # Top priority vulnerabilities
    content.extend([
        "",
        "## 🎯 Top Priority Vulnerabilities",
        ""
    ])
    
    if vulnerabilities:
        # Sort by severity priority
        severity_priority = {'critical': 4, 'high': 3, 'medium': 2, 'low': 1}
        sorted_vulns = sorted(vulnerabilities, key=lambda v: severity_priority.get(v.get('severity', 'low'), 0), reverse=True)
        
        for i, vuln in enumerate(sorted_vulns[:10], 1):
            severity = vuln.get('severity', 'unknown')
            title = vuln.get('title', 'Unknown vulnerability')
            tool = vuln.get('tool', 'unknown')
            category = vuln.get('category', 'unknown')
            file_path = vuln.get('file_path')
            line_number = vuln.get('line_number')
            
            location = f"{file_path}:{line_number}" if file_path and line_number else (file_path or "N/A")
            
            content.extend([
                f"### {i}. {title}",
                f"",
                f"- **Severity**: {severity}",
                f"- **Tool**: {tool}",
                f"- **Category**: {category}",
                f"- **Location**: {location}",
                ""
            ])
    
    # Remediation guidance
    content.extend([
        "## 🔧 Remediation Guidance",
        ""
    ])
    
    # Critical vulnerabilities
    critical_count = severity_counts.get('critical', 0)
    if critical_count > 0:
        content.extend([
            f"### 🚨 Critical Issues ({critical_count} found)",
            "",
            "**Action Required**: Immediate remediation",
            "",
            "1. **Assess Impact**: Evaluate exploitability and potential damage",
            "2. **Apply Patches**: Update affected packages or fix code issues",
            "3. **Test Fixes**: Verify fixes in development and staging environments",
            "4. **Deploy**: Roll out fixes to production with rollback plan",
            "5. **Monitor**: Watch for signs of exploitation post-deployment",
            ""
        ])
    
    # High severity vulnerabilities
    high_count = severity_counts.get('high', 0)
    if high_count > 0:
        content.extend([
            f"### ⚠️ High Severity Issues ({high_count} found)",
            "",
            "**Action Required**: Address within 72 hours",
            "",
            "1. **Review Details**: Understand the specific vulnerability",
            "2. **Plan Fix**: Determine the best remediation approach",
            "3. **Apply Fix**: Update packages or modify code",
            "4. **Test**: Verify fix doesn't break functionality",
            "5. **Deploy**: Include in next release cycle",
            ""
        ])
    
    # Medium severity vulnerabilities
    medium_count = severity_counts.get('medium', 0)
    if medium_count > 0:
        content.extend([
            f"### ⚡ Medium Severity Issues ({medium_count} found)",
            "",
            "**Action Required**: Plan for next sprint",
            "",
            "1. **Prioritize**: Evaluate against other development work",
            "2. **Schedule**: Include in upcoming sprint planning",
            "3. **Implement**: Apply fixes during scheduled maintenance",
            "4. **Verify**: Test fixes thoroughly",
            ""
        ])
    
    # Dependencies requiring updates
    sca_vulns = [v for v in vulnerabilities if v.get('category') == 'SCA']
    if sca_vulns:
        content.extend([
            "## 📦 Dependency Updates Required",
            "",
            "The following dependencies require updates to address vulnerabilities:",
            ""
        ])
        
        # Group by package
        packages = {}
        for vuln in sca_vulns:
            package_name = vuln.get('package_name', 'Unknown')
            if package_name not in packages:
                packages[package_name] = []
            packages[package_name].append(vuln)
        
        for package, vulns in packages.items():
            content.extend([
                f"### {package}",
                ""
            ])
            
            for vuln in vulns[:3]:  # Show top 3 issues per package
                current_version = vuln.get('package_version', 'Unknown')
                fixed_version = vuln.get('fixed_version', 'Latest')
                severity = vuln.get('severity', 'unknown')
                
                content.append(f"- **Version**: {current_version} → {fixed_version} (Severity: {severity})")
            
            if len(vulns) > 3:
                content.append(f"- ... and {len(vulns) - 3} more issues")
            
            content.append("")
    
    # Code issues requiring attention
    sast_vulns = [v for v in vulnerabilities if v.get('category') == 'SAST']
    if sast_vulns:
        content.extend([
            "## 💻 Code Issues Requiring Attention",
            "",
            "The following code patterns require review and potential fixes:",
            ""
        ])
        
        # Group by file
        files = {}
        for vuln in sast_vulns:
            file_path = vuln.get('file_path', 'Unknown')
            if file_path not in files:
                files[file_path] = []
            files[file_path].append(vuln)
        
        for file_path, vulns in files.items():
            content.extend([
                f"### {file_path}",
                ""
            ])
            
            for vuln in vulns[:5]:  # Show top 5 issues per file
                line_number = vuln.get('line_number', 'N/A')
                title = vuln.get('title', 'Code issue')
                tool = vuln.get('tool', 'unknown')
                
                content.append(f"- Line {line_number}: {title} (via {tool})")
            
            if len(vulns) > 5:
                content.append(f"- ... and {len(vulns) - 5} more issues")
            
            content.append("")
    
    # Next steps
    content.extend([
        "## 📋 Next Steps",
        "",
        "1. **Review Triage Summary**: Understand the scope of vulnerabilities",
        "2. **Prioritize Issues**: Focus on critical and high-severity items",
        "3. **Create Tasks**: Add security issues to project management system",
        "4. **Assign Resources**: Ensure adequate developer time for remediation",
        "5. **Track Progress**: Monitor remediation status and timeline",
        "6. **Verify Fixes**: Test all applied fixes thoroughly",
        "7. **Update Documentation**: Record lessons learned and process improvements",
        "",
        "## 🔍 Additional Resources",
        "",
        "- [OWASP Top 10](https://owasp.org/www-project-top-ten/)",
        "- [NIST Cybersecurity Framework](https://www.nist.gov/cyberframework)",
        "- [SANS Secure Coding Practices](https://www.sans.org/white-papers/2390/)",
        "- [TiXL Security Guidelines](docs/SECURITY_GUIDELINES.md)",
        "",
        "---",
        f"*Triage summary generated on {datetime.utcnow().strftime('%Y-%m-%d %H:%M:%S UTC')}*"
    ])
    
    return '\n'.join(content)

def main():
    parser = argparse.ArgumentParser(description='Generate TiXL Security Triage Summary')
    parser.add_argument('--input-dir', type=Path, required=True,
                       help='Directory containing vulnerability analysis results')
    parser.add_argument('--output-file', type=Path, required=True,
                       help='File to write triage summary')
    parser.add_argument('--github-token', type=str, default=None,
                       help='GitHub token for API access')
    parser.add_argument('--repo', type=str, default=None,
                       help='Repository identifier (owner/repo)')
    
    args = parser.parse_args()
    
    try:
        generate_triage_summary(
            input_dir=args.input_dir,
            output_file=args.output_file,
            github_token=args.github_token,
            repo=args.repo
        )
        print("Triage summary generated successfully")
        sys.exit(0)
        
    except Exception as e:
        logger.error(f"Error generating triage summary: {e}")
        sys.exit(1)

if __name__ == '__main__':
    main()