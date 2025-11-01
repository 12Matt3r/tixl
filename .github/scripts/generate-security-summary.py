#!/usr/bin/env python3
"""
TiXL Security Summary Generator
Generates GitHub Actions step summary and build status information
"""

import json
import sys
import argparse
from datetime import datetime, timedelta
from pathlib import Path
from typing import Dict, List, Any, Optional
import logging

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

def generate_security_summary(gate_results_path: Path, output_file: Path, github_step_summary: bool = False) -> None:
    """Generate security summary for GitHub Actions"""
    logger.info(f"Generating security summary from {gate_results_path}")
    
    # Load gate evaluation results
    gate_data = {}
    if gate_results_path.exists():
        with open(gate_results_path, 'r', encoding='utf-8') as f:
            gate_data = json.load(f)
    
    # Generate summary content
    summary_content = generate_summary_content(gate_data, github_step_summary)
    
    # Write to file
    if output_file:
        output_file.parent.mkdir(parents=True, exist_ok=True)
        with open(output_file, 'w', encoding='utf-8') as f:
            f.write(summary_content)
    
    # Print to stdout for GitHub Actions step summary
    if github_step_summary:
        print(summary_content)
    
    logger.info("Security summary generated successfully")

def generate_summary_content(gate_data: Dict[str, Any], github_format: bool = False) -> str:
    """Generate security summary content"""
    
    gate_status = gate_data.get('gate_status', 'UNKNOWN')
    critical_issues = gate_data.get('critical_issues', 0)
    high_issues = gate_data.get('high_issues', 0)
    medium_issues = gate_data.get('medium_issues', 0)
    low_issues = gate_data.get('low_issues', 0)
    total_vulnerabilities = gate_data.get('total_vulnerabilities', 0)
    scan_id = gate_data.get('scan_id', 'unknown')
    
    if github_format:
        # GitHub Actions markdown format
        content = []
        
        # Header
        if gate_status == 'PASSED':
            content.append("## 🔒 Security Scan Results - ✅ PASSED")
            content.append("")
            content.append("The security scan completed successfully with no critical security violations.")
        elif gate_status == 'FAILED':
            content.append("## 🔒 Security Scan Results - ❌ FAILED")
            content.append("")
            content.append("**Security gate failed due to critical vulnerabilities. Immediate action required.**")
        else:
            content.append("## 🔒 Security Scan Results - ⚠️ UNKNOWN")
            content.append("")
            content.append("Security scan completed with unknown status.")
        
        # Scan details
        content.extend([
            "",
            "### 📊 Scan Summary",
            "",
            f"**Scan ID**: `{scan_id}`",
            f"**Timestamp**: {gate_data.get('timestamp', 'Unknown')}",
            f"**Total Vulnerabilities**: {total_vulnerabilities}",
            "",
            "| Severity | Count |",
            "|----------|-------|",
        ])
        
        # Severity breakdown
        if critical_issues > 0:
            content.append(f"| 🔴 Critical | **{critical_issues}** |")
        else:
            content.append(f"| 🔴 Critical | 0 |")
            
        if high_issues > 0:
            content.append(f"| 🟠 High | **{high_issues}** |")
        else:
            content.append(f"| 🟠 High | 0 |")
            
        content.append(f"| 🟡 Medium | {medium_issues} |")
        content.append(f"| 🟢 Low | {low_issues} |")
        
        # Gate status details
        content.extend([
            "",
            "### 🎯 Quality Gate Status",
            ""
        ])
        
        if gate_status == 'PASSED':
            content.extend([
                "✅ **PASSED** - All security quality gates met",
                "",
                "**What this means:**",
                "- No critical vulnerabilities detected",
                "- High-severity issues within acceptable limits", 
                "- No secrets found in codebase",
                "- All required security tools completed successfully",
                ""
            ])
        else:
            content.extend([
                "❌ **FAILED** - Security quality gates not met",
                "",
                "**Issues requiring attention:**"
            ])
            
            if critical_issues > 0:
                content.append(f"- {critical_issues} **critical vulnerabilities** detected")
                
            if high_issues > 5:
                content.append(f"- {high_issues} **high-severity issues** (exceeds threshold of 5)")
                
            # Check for specific failure reasons
            failed_rules = gate_data.get('failed_rules', [])
            if failed_rules:
                content.extend([
                    "",
                    "**Failed Gate Rules:**"
                ])
                for rule in failed_rules:
                    rule_name = rule.get('name', 'Unknown Rule')
                    content.append(f"- {rule_name}")
            
            content.append("")
        
        # Action items
        content.extend([
            "### 📋 Recommended Actions",
            ""
        ])
        
        if gate_status == 'FAILED':
            if critical_issues > 0:
                content.extend([
                    "🚨 **IMMEDIATE (24 hours)**",
                    "- Stop all deployments until critical issues are resolved",
                    "- Review and fix critical vulnerabilities",
                    "- Test fixes thoroughly before deployment",
                    ""
                ])
                
            if high_issues > 0:
                content.extend([
                    "⚠️ **HIGH PRIORITY (72 hours)**",
                    "- Address high-severity security issues",
                    "- Update vulnerable dependencies",
                    "- Review and fix insecure code patterns",
                    ""
                ])
        else:
            content.extend([
                "✅ **MAINTAIN**",
                "- Continue regular security scanning",
                "- Monitor for new vulnerabilities",
                "- Consider reducing scan frequency if trend continues",
                ""
            ])
        
        # Tool coverage
        metrics = gate_data.get('metrics', {})
        tool_counts = metrics.get('tool_counts', {})
        
        if tool_counts:
            content.extend([
                "### 🔧 Security Tool Coverage",
                "",
                "| Tool | Findings |",
                "|------|----------|"
            ])
            
            for tool, count in sorted(tool_counts.items()):
                content.append(f"| {tool} | {count} |")
            
            content.append("")
        
        # Links and resources
        content.extend([
            "### 📖 Additional Resources",
            "",
            "- [Security Guidelines](docs/SECURITY_GUIDELINES.md)",
            "- [Vulnerability Management Policy](docs/SECURITY_IMPLEMENTATION_SUMMARY.md)",
            "- [OWASP Top 10](https://owasp.org/www-project-top-ten/)",
            "- [Security Dashboard](security-dashboard.html)",
            ""
        ])
        
        # Footer
        content.append("*Generated by TiXL Security Scanning Pipeline*")
        
    else:
        # Plain text format for file output
        content = [
            f"TiXL Security Scan Summary - {datetime.utcnow().strftime('%Y-%m-%d %H:%M:%S UTC')}",
            "=" * 50,
            "",
            f"Scan ID: {scan_id}",
            f"Gate Status: {gate_status}",
            f"Total Vulnerabilities: {total_vulnerabilities}",
            "",
            "Severity Breakdown:",
            f"  Critical: {critical_issues}",
            f"  High: {high_issues}",
            f"  Medium: {medium_issues}",
            f"  Low: {low_issues}",
            "",
            f"Timestamp: {gate_data.get('timestamp', 'Unknown')}",
            "",
            "Tool Coverage:"
        ]
        
        metrics = gate_data.get('metrics', {})
        tool_counts = metrics.get('tool_counts', {})
        for tool, count in sorted(tool_counts.items()):
            content.append(f"  {tool}: {count}")
        
        content.extend([
            "",
            "=" * 50,
            "Generated by TiXL Security Scanning Pipeline"
        ])
    
    return '\n'.join(content)

def main():
    parser = argparse.ArgumentParser(description='Generate TiXL Security Summary')
    parser.add_argument('--gate-results', type=Path, required=True,
                       help='Path to gate evaluation JSON file')
    parser.add_argument('--output-file', type=Path, default=None,
                       help='File to write summary (optional)')
    parser.add_argument('--github-step-summary', action='store_true',
                       help='Output in GitHub Actions step summary format')
    
    args = parser.parse_args()
    
    try:
        generate_security_summary(
            gate_results_path=args.gate_results,
            output_file=args.output_file,
            github_step_summary=args.github_step_summary
        )
        sys.exit(0)
        
    except Exception as e:
        logger.error(f"Error generating security summary: {e}")
        sys.exit(1)

if __name__ == '__main__':
    main()