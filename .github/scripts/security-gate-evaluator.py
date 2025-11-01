#!/usr/bin/env python3
"""
TiXL Security Gate Evaluator
Evaluates security scan results against defined policies and quality gates
"""

import json
import os
import sys
import argparse
from datetime import datetime
from pathlib import Path
from typing import Dict, List, Any, Optional
import logging
from dataclasses import dataclass
from collections import defaultdict

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

@dataclass
class SecurityGateConfig:
    """Configuration for security quality gates"""
    severity_thresholds: Dict[str, int]
    tool_requirements: Dict[str, Dict[str, Any]]
    policy_rules: List[Dict[str, Any]]
    allow_list: List[str]
    fail_conditions: List[str]

class SecurityGateEvaluator:
    """Evaluates security findings against quality gate policies"""
    
    def __init__(self, gate_policy_path: Path):
        self.gate_policy_path = gate_policy_path
        self.config = self._load_gate_policy()
        self.scan_results = {}
        self.vulnerabilities = []
        self.gate_status = "UNKNOWN"
        self.gate_details = {}
        
        # Severity scoring
        self.severity_scores = {
            'critical': 4,
            'high': 3,
            'medium': 2,
            'low': 1,
            'info': 0
        }
    
    def _load_gate_policy(self) -> SecurityGateConfig:
        """Load security gate policy configuration"""
        if not self.gate_policy_path.exists():
            logger.warning(f"Gate policy file not found: {self.gate_policy_path}")
            logger.info("Using default security gate policy")
            return self._get_default_policy()
        
        try:
            with open(self.gate_policy_path, 'r', encoding='utf-8') as f:
                policy_data = json.load(f)
            
            return SecurityGateConfig(
                severity_thresholds=policy_data.get('severity_thresholds', {}),
                tool_requirements=policy_data.get('tool_requirements', {}),
                policy_rules=policy_data.get('policy_rules', []),
                allow_list=policy_data.get('allow_list', []),
                fail_conditions=policy_data.get('fail_conditions', [])
            )
        except Exception as e:
            logger.error(f"Error loading gate policy: {e}")
            return self._get_default_policy()
    
    def _get_default_policy(self) -> SecurityGateConfig:
        """Get default security gate policy"""
        return SecurityGateConfig(
            severity_thresholds={
                'critical': 0,      # Fail on any critical issues
                'high': 10,         # Fail if more than 10 high severity issues
                'medium': 50,       # Fail if more than 50 medium severity issues
                'low': 100          # Fail if more than 100 low severity issues
            },
            tool_requirements={
                'CodeQL': {'required': True, 'min_score': 0},
                'Grype': {'required': True, 'min_score': 0},
                'Semgrep': {'required': False, 'min_score': 0}
            },
            policy_rules=[
                {
                    'name': 'No critical vulnerabilities',
                    'condition': 'critical_count == 0',
                    'action': 'fail'
                },
                {
                    'name': 'Limited high-severity issues',
                    'condition': 'high_count <= 10',
                    'action': 'fail'
                },
                {
                    'name': 'No secrets in code',
                    'condition': 'secret_count == 0',
                    'action': 'fail'
                },
                {
                    'name': 'All required tools must pass',
                    'condition': 'required_tools_passed',
                    'action': 'fail'
                }
            ],
            allow_list=[
                'CWE-999',  # Test code exclusions
                'info-only',
                'documentation'
            ],
            fail_conditions=[
                'critical_vulnerabilities_exist',
                'excessive_high_vulnerabilities',
                'secrets_detected',
                'required_tools_failed'
            ]
        )
    
    def load_scan_results(self, scan_results_dir: Path) -> None:
        """Load all security scan results"""
        logger.info(f"Loading scan results from {scan_results_dir}")
        
        # Load vulnerability analysis results
        vuln_analysis_dir = scan_results_dir / "vulnerability-analysis"
        if vuln_analysis_dir.exists():
            self._load_vulnerability_analysis(vuln_analysis_dir)
        
        # Load individual tool results
        self._load_tool_results(scan_results_dir)
        
        logger.info(f"Loaded scan results for {len(self.vulnerabilities)} vulnerabilities")
    
    def _load_vulnerability_analysis(self, vuln_analysis_dir: Path) -> None:
        """Load vulnerability analysis results"""
        # Load vulnerabilities.json
        vuln_file = vuln_analysis_dir / "vulnerabilities.json"
        if vuln_file.exists():
            try:
                with open(vuln_file, 'r', encoding='utf-8') as f:
                    vuln_data = json.load(f)
                
                for vuln_data_item in vuln_data:
                    vulnerability = self._create_vulnerability_from_data(vuln_data_item)
                    if vulnerability:
                        self.vulnerabilities.append(vulnerability)
                
                logger.info(f"Loaded {len(vuln_data)} vulnerabilities from analysis")
            except Exception as e:
                logger.error(f"Error loading vulnerability analysis: {e}")
        
        # Load analysis.json for aggregated data
        analysis_file = vuln_analysis_dir / "analysis.json"
        if analysis_file.exists():
            try:
                with open(analysis_file, 'r', encoding='utf-8') as f:
                    analysis_data = json.load(f)
                
                self.scan_results['analysis'] = analysis_data
            except Exception as e:
                logger.error(f"Error loading analysis data: {e}")
    
    def _load_tool_results(self, scan_results_dir: Path) -> None:
        """Load individual tool results"""
        # Look for specific tool result directories
        tool_dirs = [
            'codeql-results-',
            'semgrep-results-', 
            'sca-results-',
            'sonarqube-results-',
            'secret-scanning-',
            'container-security-',
            'iac-security-'
        ]
        
        for tool_dir_pattern in tool_dirs:
            matching_dirs = list(scan_results_dir.glob(f"{tool_dir_pattern}*"))
            for tool_dir in matching_dirs:
                tool_name = tool_dir_pattern.rstrip('-')
                self._process_tool_directory(tool_dir, tool_name)
    
    def _process_tool_directory(self, tool_dir: Path, tool_name: str) -> None:
        """Process individual tool result directory"""
        logger.debug(f"Processing {tool_name} results from {tool_dir}")
        
        # Load SARIF files
        for sarif_file in tool_dir.rglob("*.sarif"):
            try:
                with open(sarif_file, 'r', encoding='utf-8') as f:
                    sarif_data = json.load(f)
                
                # Process SARIF results based on tool
                results = self._process_sarif_for_tool(sarif_data, tool_name)
                self.vulnerabilities.extend(results)
                
            except Exception as e:
                logger.error(f"Error processing {tool_name} SARIF file {sarif_file}: {e}")
        
        # Load JSON result files
        for json_file in tool_dir.rglob("*.json"):
            if 'sarif' in json_file.name.lower():
                continue  # Skip SARIF files already processed
            
            try:
                with open(json_file, 'r', encoding='utf-8') as f:
                    json_data = json.load(f)
                
                results = self._process_json_for_tool(json_data, tool_name)
                self.vulnerabilities.extend(results)
                
            except Exception as e:
                logger.error(f"Error processing {tool_name} JSON file {json_file}: {e}")
    
    def _process_sarif_for_tool(self, sarif_data: Dict[str, Any], tool_name: str) -> List[Dict[str, Any]]:
        """Process SARIF data for a specific tool"""
        results = []
        
        for run in sarif_data.get('runs', []):
            for result in run.get('results', []):
                if result.get('level') in ['error', 'warning', 'note']:
                    processed_result = {
                        'id': f"{tool_name}-{result.get('ruleId', 'unknown')}",
                        'tool': tool_name,
                        'severity': self._map_sarif_level_to_severity(result.get('level')),
                        'title': result.get('message', {}).get('text', f'{tool_name} finding'),
                        'description': result.get('message', {}).get('text', ''),
                        'file_path': None,
                        'line_number': None,
                        'category': 'SAST'
                    }
                    
                    # Extract location
                    if result.get('locations'):
                        location = result['locations'][0]
                        if 'physicalLocation' in location:
                            pl = location['physicalLocation']
                            processed_result['file_path'] = pl.get('artifactLocation', {}).get('uri')
                            processed_result['line_number'] = pl.get('region', {}).get('startLine')
                    
                    results.append(processed_result)
        
        return results
    
    def _process_json_for_tool(self, json_data: Any, tool_name: str) -> List[Dict[str, Any]]:
        """Process JSON data for a specific tool"""
        results = []
        
        # Handle Grype results
        if tool_name.lower() == 'grype' or 'sca' in tool_name.lower():
            if isinstance(json_data, dict) and 'matches' in json_data:
                for match in json_data['matches']:
                    vuln = match.get('vulnerability', {})
                    artifact = match.get('artifact', {})
                    
                    results.append({
                        'id': f"grype-{vuln.get('id', 'unknown')}",
                        'tool': 'Grype',
                        'severity': vuln.get('severity', 'low').lower(),
                        'title': vuln.get('description', 'Vulnerable dependency'),
                        'description': vuln.get('description', ''),
                        'file_path': None,
                        'line_number': None,
                        'category': 'SCA',
                        'package_name': artifact.get('name'),
                        'package_version': artifact.get('version')
                    })
        
        # Handle dotnet-retire results
        elif tool_name.lower() == 'retire':
            if isinstance(json_data, list):
                for finding in json_data:
                    results.append({
                        'id': f"retire-{hash(finding.get('file', ''))}",
                        'tool': 'dotnet-retire',
                        'severity': 'high',
                        'title': 'Known vulnerable .NET package',
                        'description': finding.get('recommendation', ''),
                        'file_path': finding.get('file'),
                        'line_number': None,
                        'category': 'SCA',
                        'package_name': finding.get('packageName'),
                        'package_version': finding.get('version')
                    })
        
        # Handle TruffleHog results
        elif tool_name.lower() == 'trufflehog':
            if isinstance(json_data, dict) and 'SourceMetadata' in json_data:
                source_metadata = json_data.get('SourceMetadata', {})
                git = source_metadata.get('Data', {}).get('Filesystem', {})
                
                results.append({
                    'id': f"secret-{hash(str(json_data))}",
                    'tool': 'TruffleHog',
                    'severity': 'high',
                    'title': f"Secret detected: {json_data.get('DetectorName', 'Unknown')}",
                    'description': f"Potential secret detected: {json_data.get('Redacted', 'REDACTED')}",
                    'file_path': git.get('file'),
                    'line_number': git.get('line'),
                    'category': 'Secret Scanning',
                    'cwe_id': 'CWE-798'
                })
        
        return results
    
    def _map_sarif_level_to_severity(self, level: str) -> str:
        """Map SARIF level to severity"""
        mapping = {
            'error': 'high',
            'warning': 'medium',
            'note': 'low'
        }
        return mapping.get(level, 'low')
    
    def _create_vulnerability_from_data(self, vuln_data: Dict[str, Any]) -> Optional[Dict[str, Any]]:
        """Create vulnerability object from analysis data"""
        try:
            return {
                'id': vuln_data.get('id', 'unknown'),
                'title': vuln_data.get('title', ''),
                'description': vuln_data.get('description', ''),
                'severity': vuln_data.get('severity', 'low'),
                'tool': vuln_data.get('tool', 'unknown'),
                'category': vuln_data.get('category', 'unknown'),
                'file_path': vuln_data.get('file_path'),
                'line_number': vuln_data.get('line_number'),
                'cve_id': vuln_data.get('cve_id'),
                'cwe_id': vuln_data.get('cwe_id'),
                'package_name': vuln_data.get('package_name'),
                'package_version': vuln_data.get('package_version'),
                'is_false_positive': vuln_data.get('is_false_positive', False),
                'status': vuln_data.get('status', 'open')
            }
        except Exception as e:
            logger.error(f"Error creating vulnerability from data: {e}")
            return None
    
    def evaluate_security_gates(self) -> None:
        """Evaluate all security gates"""
        logger.info("Evaluating security gates...")
        
        # Calculate vulnerability metrics
        metrics = self._calculate_vulnerability_metrics()
        
        # Evaluate individual gate rules
        gate_results = []
        
        for rule in self.config.policy_rules:
            result = self._evaluate_rule(rule, metrics)
            gate_results.append(result)
        
        # Determine overall gate status
        failed_rules = [r for r in gate_results if not r['passed']]
        
        if failed_rules:
            self.gate_status = "FAILED"
            self.gate_details = {
                'status': 'FAILED',
                'failed_rules': failed_rules,
                'metrics': metrics,
                'timestamp': datetime.utcnow().isoformat()
            }
            logger.warning(f"Security gate failed with {len(failed_rules)} failed rules")
        else:
            self.gate_status = "PASSED"
            self.gate_details = {
                'status': 'PASSED',
                'passed_rules': [r['name'] for r in gate_results],
                'metrics': metrics,
                'timestamp': datetime.utcnow().isoformat()
            }
            logger.info("Security gate passed")
        
        # Check required tools
        required_tools_passed = self._evaluate_required_tools()
        if not required_tools_passed:
            self.gate_status = "FAILED"
            self.gate_details['failed_rules'].append({
                'name': 'Required tools evaluation',
                'passed': False,
                'reason': 'One or more required security tools failed'
            })
    
    def _calculate_vulnerability_metrics(self) -> Dict[str, Any]:
        """Calculate vulnerability metrics for gate evaluation"""
        # Count by severity
        severity_counts = defaultdict(int)
        tool_counts = defaultdict(int)
        category_counts = defaultdict(int)
        secret_count = 0
        cve_count = 0
        
        for vuln in self.vulnerabilities:
            # Skip false positives
            if vuln.get('is_false_positive', False):
                continue
            
            # Skip allowed issues
            if self._is_allowed_vulnerability(vuln):
                continue
            
            severity = vuln.get('severity', 'low')
            tool = vuln.get('tool', 'unknown')
            category = vuln.get('category', 'unknown')
            
            severity_counts[severity] += 1
            tool_counts[tool] += 1
            category_counts[category] += 1
            
            # Special counts
            if category == 'Secret Scanning':
                secret_count += 1
            if vuln.get('cve_id'):
                cve_count += 1
        
        return {
            'total_vulnerabilities': len([v for v in self.vulnerabilities if not v.get('is_false_positive', False)]),
            'critical_count': severity_counts['critical'],
            'high_count': severity_counts['high'],
            'medium_count': severity_counts['medium'],
            'low_count': severity_counts['low'],
            'secret_count': secret_count,
            'cve_count': cve_count,
            'tool_counts': dict(tool_counts),
            'category_counts': dict(category_counts),
            'critical_vulnerabilities_exist': severity_counts['critical'] > 0,
            'excessive_high_vulnerabilities': severity_counts['high'] > self.config.severity_thresholds['high'],
            'secrets_detected': secret_count > 0,
            'required_tools_passed': self._evaluate_required_tools()
        }
    
    def _is_allowed_vulnerability(self, vuln: Dict[str, Any]) -> bool:
        """Check if vulnerability is in the allow list"""
        cwe_id = vuln.get('cwe_id')
        if cwe_id and cwe_id in self.config.allow_list:
            return True
        
        # Check if title contains allow-list items
        title = vuln.get('title', '').lower()
        for allowed_item in self.config.allow_list:
            if allowed_item.lower() in title:
                return True
        
        return False
    
    def _evaluate_required_tools(self) -> bool:
        """Evaluate if all required tools passed"""
        tool_results = self.scan_results.get('tool_results', {})
        
        for tool, requirements in self.config.tool_requirements.items():
            if requirements.get('required', False):
                tool_result = tool_results.get(tool, 'unknown')
                if tool_result not in ['passed', 'success']:
                    logger.warning(f"Required tool {tool} did not pass: {tool_result}")
                    return False
        
        return True
    
    def _evaluate_rule(self, rule: Dict[str, Any], metrics: Dict[str, Any]) -> Dict[str, Any]:
        """Evaluate a specific gate rule"""
        try:
            condition = rule.get('condition', '')
            
            # Simple condition evaluation (can be enhanced)
            if 'critical_count == 0' in condition:
                passed = metrics['critical_count'] == 0
                reason = "Critical vulnerabilities found" if not passed else "No critical vulnerabilities"
            elif 'high_count <= 10' in condition:
                passed = metrics['high_count'] <= 10
                reason = f"High severity count ({metrics['high_count']}) exceeds threshold (10)" if not passed else "High severity within limits"
            elif 'secret_count == 0' in condition:
                passed = metrics['secret_count'] == 0
                reason = "Secrets detected in codebase" if not passed else "No secrets detected"
            elif 'required_tools_passed' in condition:
                passed = metrics['required_tools_passed']
                reason = "Required security tools failed" if not passed else "All required tools passed"
            else:
                # Default: pass if no specific evaluation
                passed = True
                reason = "Rule condition not recognized"
            
            return {
                'name': rule.get('name', 'Unknown Rule'),
                'passed': passed,
                'condition': condition,
                'reason': reason,
                'action': rule.get('action', 'fail')
            }
        except Exception as e:
            logger.error(f"Error evaluating rule {rule}: {e}")
            return {
                'name': rule.get('name', 'Unknown Rule'),
                'passed': False,
                'condition': rule.get('condition', ''),
                'reason': f"Evaluation error: {str(e)}",
                'action': rule.get('action', 'fail')
            }
    
    def save_evaluation_results(self, output_file: Path) -> None:
        """Save gate evaluation results"""
        # Add scan-specific information
        self.gate_details['scan_id'] = f"scan-{datetime.utcnow().strftime('%Y%m%d-%H%M%S')}"
        self.gate_details['gate_status'] = self.gate_status
        self.gate_details['total_vulnerabilities'] = sum([
            self.gate_details['metrics'].get('critical_count', 0),
            self.gate_details['metrics'].get('high_count', 0),
            self.gate_details['metrics'].get('medium_count', 0),
            self.gate_details['metrics'].get('low_count', 0)
        ])
        self.gate_details['critical_issues'] = self.gate_details['metrics'].get('critical_count', 0)
        self.gate_details['high_issues'] = self.gate_details['metrics'].get('high_count', 0)
        self.gate_details['medium_issues'] = self.gate_details['metrics'].get('medium_count', 0)
        self.gate_details['low_issues'] = self.gate_details['metrics'].get('low_count', 0)
        self.gate_details['timestamp'] = datetime.utcnow().isoformat()
        
        # Save to file
        output_file.parent.mkdir(parents=True, exist_ok=True)
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(self.gate_details, f, indent=2)
        
        logger.info(f"Gate evaluation results saved to {output_file}")

def main():
    parser = argparse.ArgumentParser(description='TiXL Security Gate Evaluator')
    parser.add_argument('--scan-results', type=Path, required=True,
                       help='Directory containing security scan results')
    parser.add_argument('--severity-threshold', type=str, default='medium',
                       choices=['low', 'medium', 'high', 'critical'],
                       help='Minimum severity threshold')
    parser.add_argument('--gate-policy', type=Path, required=True,
                       help='Path to gate policy JSON file')
    parser.add_argument('--output-file', type=Path, required=True,
                       help='File to save gate evaluation results')
    
    args = parser.parse_args()
    
    try:
        # Initialize evaluator
        evaluator = SecurityGateEvaluator(args.gate_policy)
        
        # Load scan results
        evaluator.load_scan_results(args.scan_results)
        
        # Evaluate gates
        evaluator.evaluate_security_gates()
        
        # Save results
        evaluator.save_evaluation_results(args.output_file)
        
        # Print results for CI consumption
        if evaluator.gate_status == "FAILED":
            print(f"SECURITY_GATE_STATUS=FAILED")
            print(f"CRITICAL_ISSUES={evaluator.gate_details.get('critical_issues', 0)}")
            print(f"HIGH_ISSUES={evaluator.gate_details.get('high_issues', 0)}")
            logger.error("Security gate evaluation: FAILED")
            sys.exit(1)
        else:
            print(f"SECURITY_GATE_STATUS=PASSED")
            logger.info("Security gate evaluation: PASSED")
            sys.exit(0)
            
    except Exception as e:
        logger.error(f"Error during gate evaluation: {e}")
        print(f"SECURITY_GATE_STATUS=ERROR")
        sys.exit(1)

if __name__ == '__main__':
    main()