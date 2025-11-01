#!/usr/bin/env python3
"""
TiXL Security Dashboard Generator
Generates comprehensive security reports and dashboards from scan results
"""

import json
import os
import sys
import argparse
from datetime import datetime, timedelta
from pathlib import Path
from typing import Dict, List, Any, Optional
import logging
from collections import defaultdict, Counter
import base64
import hashlib

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

class SecurityDashboardGenerator:
    """Generates security dashboards and reports"""
    
    def __init__(self, template_dir: Path):
        self.template_dir = template_dir
        self.scan_data = {}
        self.vulnerability_data = []
        self.metrics = {}
        
        # Load templates
        self.templates = self._load_templates()
    
    def _load_templates(self) -> Dict[str, str]:
        """Load HTML templates"""
        templates = {}
        
        if self.template_dir.exists():
            for template_file in self.template_dir.glob("*.html"):
                try:
                    with open(template_file, 'r', encoding='utf-8') as f:
                        templates[template_file.stem] = f.read()
                except Exception as e:
                    logger.warning(f"Failed to load template {template_file}: {e}")
        
        # Use default templates if none found
        if not templates:
            templates = self._get_default_templates()
        
        return templates
    
    def _get_default_templates(self) -> Dict[str, str]:
        """Get default HTML templates"""
        return {
            'dashboard': '''<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>TiXL Security Dashboard</title>
    <style>
        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 0; padding: 20px; background-color: #f5f5f5; }
        .container { max-width: 1200px; margin: 0 auto; background: white; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
        .header { background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; border-radius: 8px 8px 0 0; }
        .metrics { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 20px; padding: 30px; }
        .metric-card { background: #f8f9fa; padding: 20px; border-radius: 8px; text-align: center; border-left: 4px solid; }
        .metric-card.critical { border-color: #dc3545; }
        .metric-card.high { border-color: #fd7e14; }
        .metric-card.medium { border-color: #ffc107; }
        .metric-card.low { border-color: #28a745; }
        .metric-value { font-size: 2em; font-weight: bold; margin: 10px 0; }
        .section { padding: 30px; border-top: 1px solid #e9ecef; }
        .vulnerability-table { width: 100%; border-collapse: collapse; margin-top: 20px; }
        .vulnerability-table th, .vulnerability-table td { padding: 12px; text-align: left; border-bottom: 1px solid #ddd; }
        .severity-badge { padding: 4px 8px; border-radius: 4px; font-size: 0.8em; font-weight: bold; color: white; }
        .severity-critical { background-color: #dc3545; }
        .severity-high { background-color: #fd7e14; }
        .severity-medium { background-color: #ffc107; color: #000; }
        .severity-low { background-color: #28a745; }
        .charts { display: grid; grid-template-columns: 1fr 1fr; gap: 30px; margin-top: 20px; }
        .chart-container { background: #f8f9fa; padding: 20px; border-radius: 8px; }
        .footer { padding: 20px; text-align: center; color: #6c757d; border-top: 1px solid #e9ecef; }
    </style>
    <script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
</head>
<body>
    <div class="container">
        <div class="header">
            <h1>🔒 TiXL Security Dashboard</h1>
            <p>Comprehensive Security Scan Results</p>
            <p>Generated: {{generation_time}}</p>
        </div>
        
        <div class="metrics">
            <div class="metric-card critical">
                <h3>Critical</h3>
                <div class="metric-value">{{critical_count}}</div>
                <p>Immediate attention required</p>
            </div>
            <div class="metric-card high">
                <h3>High</h3>
                <div class="metric-value">{{high_count}}</div>
                <p>Address promptly</p>
            </div>
            <div class="metric-card medium">
                <h3>Medium</h3>
                <div class="metric-value">{{medium_count}}</div>
                <p>Plan for resolution</p>
            </div>
            <div class="metric-card low">
                <h3>Low</h3>
                <div class="metric-value">{{low_count}}</div>
                <p>Monitor and address</p>
            </div>
        </div>
        
        <div class="section">
            <h2>📊 Vulnerability Trends</h2>
            <div class="charts">
                <div class="chart-container">
                    <canvas id="severityChart"></canvas>
                </div>
                <div class="chart-container">
                    <canvas id="toolChart"></canvas>
                </div>
            </div>
        </div>
        
        <div class="section">
            <h2>🔍 Top Security Findings</h2>
            <table class="vulnerability-table">
                <thead>
                    <tr>
                        <th>Severity</th>
                        <th>Title</th>
                        <th>Tool</th>
                        <th>Category</th>
                        <th>Location</th>
                    </tr>
                </thead>
                <tbody>
                    {% for vuln in top_vulnerabilities %}
                    <tr>
                        <td><span class="severity-badge severity-{{vuln.severity}}">{{vuln.severity}}</span></td>
                        <td>{{vuln.title}}</td>
                        <td>{{vuln.tool}}</td>
                        <td>{{vuln.category}}</td>
                        <td>{{vuln.location}}</td>
                    </tr>
                    {% endfor %}
                </tbody>
            </table>
        </div>
        
        <div class="footer">
            <p>TiXL Security Dashboard - Last updated: {{generation_time}}</p>
            <p>For detailed reports, see the security analysis artifacts</p>
        </div>
    </div>
    
    <script>
        // Severity Distribution Chart
        const severityCtx = document.getElementById('severityChart').getContext('2d');
        new Chart(severityCtx, {
            type: 'doughnut',
            data: {
                labels: ['Critical', 'High', 'Medium', 'Low'],
                datasets: [{
                    data: [{{critical_count}}, {{high_count}}, {{medium_count}}, {{low_count}}],
                    backgroundColor: ['#dc3545', '#fd7e14', '#ffc107', '#28a745']
                }]
            },
            options: {
                responsive: true,
                plugins: {
                    title: { display: true, text: 'Vulnerabilities by Severity' }
                }
            }
        });
        
        // Tool Distribution Chart
        const toolCtx = document.getElementById('toolChart').getContext('2d');
        new Chart(toolCtx, {
            type: 'bar',
            data: {
                labels: {{tool_labels|safe}},
                datasets: [{
                    label: 'Vulnerabilities',
                    data: {{tool_counts|safe}},
                    backgroundColor: '#667eea'
                }]
            },
            options: {
                responsive: true,
                plugins: {
                    title: { display: true, text: 'Vulnerabilities by Tool' }
                }
            }
        });
    </script>
</body>
</html>''',
            
            'summary_report': '''# TiXL Security Scan Summary Report

**Generated**: {{generation_time}}  
**Scan ID**: {{scan_id}}  
**Total Vulnerabilities**: {{total_vulnerabilities}}

## Executive Summary

{{executive_summary}}

## Vulnerability Breakdown

| Severity | Count | Percentage |
|----------|-------|------------|
| 🔴 Critical | {{critical_count}} | {{critical_percentage}}% |
| 🟠 High | {{high_count}} | {{high_percentage}}% |
| 🟡 Medium | {{medium_count}} | {{medium_percentage}}% |
| 🟢 Low | {{low_count}} | {{low_percentage}}% |

## Tool Coverage

{{tool_coverage_table}}

## Security Metrics

- **Mean Time to Detection**: {{mttr}} hours
- **Security Debt Score**: {{security_debt_score}}
- **Compliance Status**: {{compliance_status}}

## Recommendations

{{recommendations}}

## Next Steps

1. **Immediate**: Address all critical vulnerabilities
2. **Short-term**: Remediate high-severity issues within 72 hours
3. **Medium-term**: Plan resolution of medium-severity issues
4. **Long-term**: Implement preventive measures and monitoring

---
*This report was generated by the TiXL Security Scanning Pipeline*'''
        }
    
    def load_scan_results(self, scan_results_dir: Path) -> None:
        """Load all security scan results"""
        logger.info(f"Loading scan results from {scan_results_dir}")
        
        # Load gate evaluation results
        gate_eval_files = list(scan_results_dir.rglob("gate-evaluation*.json"))
        for gate_eval_file in gate_eval_files:
            try:
                with open(gate_eval_file, 'r', encoding='utf-8') as f:
                    self.scan_data['gate_evaluation'] = json.load(f)
                logger.info(f"Loaded gate evaluation from {gate_eval_file}")
            except Exception as e:
                logger.error(f"Error loading gate evaluation: {e}")
        
        # Load vulnerability analysis results
        vuln_analysis_dirs = list(scan_results_dir.glob("vulnerability-analysis-*"))
        for vuln_analysis_dir in vuln_analysis_dirs:
            vuln_file = vuln_analysis_dir / "vulnerabilities.json"
            if vuln_file.exists():
                try:
                    with open(vuln_file, 'r', encoding='utf-8') as f:
                        self.vulnerability_data = json.load(f)
                    logger.info(f"Loaded {len(self.vulnerability_data)} vulnerabilities")
                except Exception as e:
                    logger.error(f"Error loading vulnerabilities: {e}")
        
        # Load aggregated analysis
        analysis_files = list(scan_results_dir.rglob("analysis.json"))
        for analysis_file in analysis_files:
            try:
                with open(analysis_file, 'r', encoding='utf-8') as f:
                    analysis_data = json.load(f)
                self.scan_data['analysis'] = analysis_data
                logger.info(f"Loaded analysis data from {analysis_file}")
            except Exception as e:
                logger.error(f"Error loading analysis: {e}")
    
    def calculate_metrics(self) -> Dict[str, Any]:
        """Calculate security metrics and statistics"""
        logger.info("Calculating security metrics...")
        
        # Basic counts
        total_vulns = len(self.vulnerability_data)
        severity_counts = Counter(vuln.get('severity', 'unknown') for vuln in self.vulnerability_data)
        
        # Tool counts
        tool_counts = Counter(vuln.get('tool', 'unknown') for vuln in self.vulnerability_data)
        
        # Category counts
        category_counts = Counter(vuln.get('category', 'unknown') for vuln in self.vulnerability_data)
        
        # Gate status
        gate_status = self.scan_data.get('gate_evaluation', {}).get('gate_status', 'UNKNOWN')
        
        # Security debt score (weighted by severity)
        security_debt_score = (
            severity_counts.get('critical', 0) * 10 +
            severity_counts.get('high', 0) * 5 +
            severity_counts.get('medium', 0) * 2 +
            severity_counts.get('low', 0) * 1
        )
        
        # Calculate percentages
        percentages = {}
        for severity, count in severity_counts.items():
            percentages[f"{severity}_percentage"] = round((count / total_vulns * 100), 1) if total_vulns > 0 else 0
        
        # Create tool coverage table
        tool_coverage = []
        for tool, count in tool_counts.most_common():
            percentage = round((count / total_vulns * 100), 1) if total_vulns > 0 else 0
            tool_coverage.append(f"- **{tool}**: {count} findings ({percentage}%)")
        
        # Generate recommendations
        recommendations = self._generate_recommendations(severity_counts, security_debt_score, gate_status)
        
        # Executive summary
        executive_summary = self._generate_executive_summary(severity_counts, security_debt_score, gate_status)
        
        self.metrics = {
            'total_vulnerabilities': total_vulns,
            'severity_counts': dict(severity_counts),
            'tool_counts': dict(tool_counts),
            'category_counts': dict(category_counts),
            'gate_status': gate_status,
            'security_debt_score': security_debt_score,
            'percentages': percentages,
            'tool_coverage': tool_coverage,
            'recommendations': recommendations,
            'executive_summary': executive_summary,
            'top_vulnerabilities': self._get_top_vulnerabilities(10)
        }
        
        return self.metrics
    
    def _generate_recommendations(self, severity_counts: Counter, security_debt_score: int, gate_status: str) -> str:
        """Generate security recommendations"""
        recommendations = []
        
        # Critical recommendations
        if severity_counts.get('critical', 0) > 0:
            recommendations.append("🚨 **CRITICAL**: Immediate action required for critical vulnerabilities")
            recommendations.append("- Stop all deployments until critical issues are resolved")
            recommendations.append("- Activate incident response procedures")
        
        # High severity recommendations
        if severity_counts.get('high', 0) > 0:
            recommendations.append("⚠️ **HIGH**: Address high-severity issues within 72 hours")
            recommendations.append("- Schedule emergency security patch deployment")
            recommendations.append("- Review and update security policies")
        
        # Medium severity recommendations
        if severity_counts.get('medium', 0) > 10:
            recommendations.append("⚡ **MEDIUM**: Plan remediation for medium-severity issues")
            recommendations.append("- Include in next sprint planning")
            recommendations.append("- Consider automated remediation where possible")
        
        # Gate status recommendations
        if gate_status == "FAILED":
            recommendations.append("❌ **GATE FAILURE**: Security quality gates not met")
            recommendations.append("- Review and update security policies if needed")
            recommendations.append("- Consider temporary risk acceptance procedures")
        
        # General recommendations
        if security_debt_score > 50:
            recommendations.append("📈 **HIGH SECURITY DEBT**: Consider dedicated security sprint")
            recommendations.append("- Allocate additional resources for security remediation")
        
        # Positive reinforcement
        if severity_counts.get('critical', 0) == 0 and severity_counts.get('high', 0) <= 5:
            recommendations.append("✅ **GOOD SECURITY POSTURE**: Maintain current practices")
            recommendations.append("- Continue regular security scanning")
            recommendations.append("- Consider reducing scan frequency if trend continues")
        
        return '\n'.join(recommendations)
    
    def _generate_executive_summary(self, severity_counts: Counter, security_debt_score: int, gate_status: str) -> str:
        """Generate executive summary"""
        total_vulns = sum(severity_counts.values())
        
        if gate_status == "PASSED" and severity_counts.get('critical', 0) == 0:
            summary = f"The TiXL security scan completed successfully with {total_vulns} total findings. "
            summary += f"No critical vulnerabilities were detected, and the security quality gate passed. "
            if severity_counts.get('high', 0) <= 5:
                summary += "The security posture is strong with minimal high-severity issues."
            else:
                summary += "While the gate passed, there are notable high-severity issues that require attention."
        elif gate_status == "FAILED":
            summary = f"⚠️ SECURITY GATE FAILURE: The security scan identified {total_vulns} vulnerabilities, "
            summary += f"including {severity_counts.get('critical', 0)} critical and {severity_counts.get('high', 0)} high-severity issues. "
            summary += "Immediate action is required to meet security quality standards."
        else:
            summary = f"The security scan identified {total_vulns} vulnerabilities across multiple categories. "
            summary += f"Security debt score: {security_debt_score}. "
            summary += "Review the detailed findings and recommendations for remediation guidance."
        
        return summary
    
    def _get_top_vulnerabilities(self, limit: int = 10) -> List[Dict[str, Any]]:
        """Get top vulnerabilities by severity"""
        severity_order = {'critical': 4, 'high': 3, 'medium': 2, 'low': 1}
        
        sorted_vulns = sorted(
            self.vulnerability_data,
            key=lambda v: (severity_order.get(v.get('severity', 'low'), 0), v.get('title', '')),
            reverse=True
        )[:limit]
        
        # Format for display
        formatted_vulns = []
        for vuln in sorted_vulns:
            formatted_vuln = {
                'severity': vuln.get('severity', 'unknown'),
                'title': vuln.get('title', 'Unknown vulnerability'),
                'tool': vuln.get('tool', 'unknown'),
                'category': vuln.get('category', 'unknown'),
                'location': self._format_location(vuln)
            }
            formatted_vulns.append(formatted_vuln)
        
        return formatted_vulns
    
    def _format_location(self, vuln: Dict[str, Any]) -> str:
        """Format vulnerability location for display"""
        file_path = vuln.get('file_path', '')
        line_number = vuln.get('line_number')
        
        if file_path and line_number:
            return f"{file_path}:{line_number}"
        elif file_path:
            return file_path
        else:
            return "N/A"
    
    def generate_html_dashboard(self, output_dir: Path) -> Path:
        """Generate HTML security dashboard"""
        logger.info("Generating HTML security dashboard...")
        
        output_dir.mkdir(parents=True, exist_ok=True)
        dashboard_file = output_dir / "security-dashboard.html"
        
        # Prepare template data
        template_data = {
            'generation_time': datetime.utcnow().strftime('%Y-%m-%d %H:%M:%S UTC'),
            'scan_id': self.scan_data.get('gate_evaluation', {}).get('scan_id', 'unknown'),
            'total_vulnerabilities': self.metrics['total_vulnerabilities'],
            'critical_count': self.metrics['severity_counts'].get('critical', 0),
            'high_count': self.metrics['severity_counts'].get('high', 0),
            'medium_count': self.metrics['severity_counts'].get('medium', 0),
            'low_count': self.metrics['severity_counts'].get('low', 0),
            'top_vulnerabilities': self.metrics['top_vulnerabilities'],
            'tool_labels': json.dumps(list(self.metrics['tool_counts'].keys())),
            'tool_counts': json.dumps(list(self.metrics['tool_counts'].values()))
        }
        
        # Render template
        html_content = self._render_template('dashboard', template_data)
        
        # Write to file
        with open(dashboard_file, 'w', encoding='utf-8') as f:
            f.write(html_content)
        
        logger.info(f"HTML dashboard generated: {dashboard_file}")
        return dashboard_file
    
    def generate_pdf_report(self, output_dir: Path) -> Optional[Path]:
        """Generate PDF security report (requires wkhtmltopdf)"""
        logger.info("Generating PDF security report...")
        
        try:
            # Generate HTML first
            html_file = self.generate_html_dashboard(output_dir)
            
            # Try to convert to PDF using weasyprint (if available)
            try:
                import weasyprint
                pdf_file = output_dir / "security-report.pdf"
                weasyprint.HTML(filename=str(html_file)).write_pdf(str(pdf_file))
                logger.info(f"PDF report generated: {pdf_file}")
                return pdf_file
            except ImportError:
                logger.warning("WeasyPrint not available, skipping PDF generation")
                return None
            except Exception as e:
                logger.error(f"Error generating PDF: {e}")
                return None
                
        except Exception as e:
            logger.error(f"Error generating PDF report: {e}")
            return None
    
    def generate_json_report(self, output_dir: Path) -> Path:
        """Generate JSON security report"""
        logger.info("Generating JSON security report...")
        
        output_dir.mkdir(parents=True, exist_ok=True)
        json_file = output_dir / "security-report.json"
        
        report_data = {
            'scan_info': {
                'generated_at': datetime.utcnow().isoformat(),
                'scan_id': self.scan_data.get('gate_evaluation', {}).get('scan_id', 'unknown'),
                'total_vulnerabilities': self.metrics['total_vulnerabilities']
            },
            'metrics': self.metrics,
            'gate_evaluation': self.scan_data.get('gate_evaluation', {}),
            'raw_data': {
                'vulnerabilities': self.vulnerability_data,
                'analysis': self.scan_data.get('analysis', {})
            }
        }
        
        with open(json_file, 'w', encoding='utf-8') as f:
            json.dump(report_data, f, indent=2)
        
        logger.info(f"JSON report generated: {json_file}")
        return json_file
    
    def generate_markdown_summary(self, output_dir: Path) -> Path:
        """Generate Markdown summary report"""
        logger.info("Generating Markdown summary...")
        
        output_dir.mkdir(parents=True, exist_ok=True)
        markdown_file = output_dir / "security-summary.md"
        
        # Prepare template data
        template_data = {
            'generation_time': datetime.utcnow().strftime('%Y-%m-%d %H:%M:%S UTC'),
            'scan_id': self.scan_data.get('gate_evaluation', {}).get('scan_id', 'unknown'),
            'total_vulnerabilities': self.metrics['total_vulnerabilities'],
            'critical_count': self.metrics['severity_counts'].get('critical', 0),
            'high_count': self.metrics['severity_counts'].get('high', 0),
            'medium_count': self.metrics['severity_counts'].get('medium', 0),
            'low_count': self.metrics['severity_counts'].get('low', 0),
            'critical_percentage': self.metrics['percentages'].get('critical_percentage', 0),
            'high_percentage': self.metrics['percentages'].get('high_percentage', 0),
            'medium_percentage': self.metrics['percentages'].get('medium_percentage', 0),
            'low_percentage': self.metrics['percentages'].get('low_percentage', 0),
            'tool_coverage_table': '\n'.join(self.metrics['tool_coverage']),
            'mttr': '2.5',  # Mock data - would calculate from historical data
            'security_debt_score': self.metrics['security_debt_score'],
            'compliance_status': 'COMPLIANT' if self.metrics['gate_status'] == 'PASSED' else 'NON-COMPLIANT',
            'recommendations': self.metrics['recommendations'],
            'executive_summary': self.metrics['executive_summary']
        }
        
        # Render template
        markdown_content = self._render_template('summary_report', template_data)
        
        # Write to file
        with open(markdown_file, 'w', encoding='utf-8') as f:
            f.write(markdown_content)
        
        logger.info(f"Markdown summary generated: {markdown_file}")
        return markdown_file
    
    def _render_template(self, template_name: str, data: Dict[str, Any]) -> str:
        """Render template with data"""
        template = self.templates.get(template_name, '')
        if not template:
            return f"<p>Template '{template_name}' not found</p>"
        
        try:
            from jinja2 import Template
            jinja_template = Template(template)
            return jinja_template.render(**data)
        except ImportError:
            # Simple string replacement fallback
            rendered = template
            for key, value in data.items():
                rendered = rendered.replace(f"{{{{{key}}}}}", str(value))
            return rendered
        except Exception as e:
            logger.error(f"Error rendering template {template_name}: {e}")
            return f"<p>Error rendering template: {e}</p>"
    
    def generate_all_reports(self, output_dir: Path, formats: List[str] = None) -> List[Path]:
        """Generate all security reports"""
        if formats is None:
            formats = ['html', 'json', 'markdown']
        
        output_dir.mkdir(parents=True, exist_ok=True)
        generated_files = []
        
        # Calculate metrics
        self.calculate_metrics()
        
        # Generate reports based on requested formats
        if 'html' in formats:
            generated_files.append(self.generate_html_dashboard(output_dir))
        
        if 'json' in formats:
            generated_files.append(self.generate_json_report(output_dir))
        
        if 'markdown' in formats:
            generated_files.append(self.generate_markdown_summary(output_dir))
        
        if 'pdf' in formats:
            pdf_file = self.generate_pdf_report(output_dir)
            if pdf_file:
                generated_files.append(pdf_file)
        
        logger.info(f"Generated {len(generated_files)} security reports")
        return generated_files

def main():
    parser = argparse.ArgumentParser(description='TiXL Security Dashboard Generator')
    parser.add_argument('--scan-results', type=Path, required=True,
                       help='Directory containing security scan results')
    parser.add_argument('--output-dir', type=Path, required=True,
                       help='Directory to save generated reports')
    parser.add_argument('--template-dir', type=Path, default=None,
                       help='Directory containing HTML templates')
    parser.add_argument('--format', type=str, nargs='+', 
                       choices=['html', 'json', 'markdown', 'pdf'],
                       default=['html', 'json', 'markdown'],
                       help='Output formats to generate')
    
    args = parser.parse_args()
    
    try:
        # Initialize dashboard generator
        template_dir = args.template_dir or Path(__file__).parent.parent / "security" / "templates"
        generator = SecurityDashboardGenerator(template_dir)
        
        # Load scan results
        generator.load_scan_results(args.scan_results)
        
        # Generate reports
        generated_files = generator.generate_all_reports(args.output_dir, args.format)
        
        # Print summary
        print(f"Generated {len(generated_files)} security reports:")
        for file_path in generated_files:
            print(f"  - {file_path}")
        
        logger.info("Security dashboard generation completed successfully")
        sys.exit(0)
        
    except Exception as e:
        logger.error(f"Error generating security dashboard: {e}")
        sys.exit(1)

if __name__ == '__main__':
    main()