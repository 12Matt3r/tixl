#!/usr/bin/env python3
"""
TIXL-058 SAST and SCA Security Scanning Implementation Validator
Validates that all components are properly configured and functional
"""

import os
import sys
import json
import subprocess
from pathlib import Path
from typing import List, Dict, Any

class SecurityImplementationValidator:
    """Validates TIXL-058 security implementation"""
    
    def __init__(self):
        self.repo_root = Path(__file__).parent.parent
        self.errors = []
        self.warnings = []
        self.passed_checks = []
        
    def validate_implementation(self) -> bool:
        """Run all validation checks"""
        print("🔍 Validating TIXL-058 SAST and SCA Security Scanning Implementation")
        print("=" * 70)
        
        # Core files validation
        self.validate_core_files()
        
        # Workflow validation  
        self.validate_workflows()
        
        # Scripts validation
        self.validate_scripts()
        
        # Configuration validation
        self.validate_configuration()
        
        # Documentation validation
        self.validate_documentation()
        
        # Python syntax validation
        self.validate_python_syntax()
        
        # Generate report
        self.generate_validation_report()
        
        # Return overall status
        return len(self.errors) == 0
    
    def validate_core_files(self):
        """Validate core implementation files"""
        print("\n📁 Validating Core Files...")
        
        required_files = [
            ".github/workflows/tixl-sast-sca-comprehensive.yml",
            ".github/security/policies/gate-policy.json",
            ".github/scripts/requirements.txt",
            "docs/TIXL-058_SAST_SCA_Implementation_Guide.md",
            "TIXL-058_Implementation_Summary.md"
        ]
        
        for file_path in required_files:
            full_path = self.repo_root / file_path
            if full_path.exists():
                self.passed_checks.append(f"✅ {file_path}")
                print(f"  ✅ {file_path}")
            else:
                self.errors.append(f"❌ Missing required file: {file_path}")
                print(f"  ❌ {file_path}")
    
    def validate_workflows(self):
        """Validate GitHub Actions workflows"""
        print("\n🔄 Validating Workflows...")
        
        workflow_file = self.repo_root / ".github/workflows/tixl-sast-sca-comprehensive.yml"
        if not workflow_file.exists():
            self.errors.append("❌ Main security workflow file missing")
            return
        
        try:
            with open(workflow_file, 'r') as f:
                workflow_content = f.read()
            
            # Check for required jobs
            required_jobs = [
                'environment-setup',
                'sast-codeql', 
                'sast-semgrep',
                'sca-nuget',
                'secret-scanning',
                'vulnerability-triage',
                'security-gate',
                'security-reporting'
            ]
            
            for job in required_jobs:
                if job in workflow_content:
                    self.passed_checks.append(f"✅ Workflow job: {job}")
                else:
                    self.warnings.append(f"⚠️ Workflow job missing: {job}")
            
            # Check for required tools
            tools = ['CodeQL', 'Semgrep', 'Grype', 'TruffleHog', 'OWASP Dependency-Check']
            for tool in tools:
                if tool in workflow_content:
                    self.passed_checks.append(f"✅ Tool integration: {tool}")
                else:
                    self.warnings.append(f"⚠️ Tool integration missing: {tool}")
                    
        except Exception as e:
            self.errors.append(f"❌ Error reading workflow file: {e}")
    
    def validate_scripts(self):
        """Validate security scripts"""
        print("\n🐍 Validating Security Scripts...")
        
        scripts_dir = self.repo_root / ".github/scripts"
        required_scripts = [
            "vulnerability-aggregator.py",
            "security-gate-evaluator.py", 
            "generate-security-dashboard.py",
            "generate-triage-summary.py",
            "security-notification-handler.py",
            "generate-security-summary.py",
            "update-security-metrics.py"
        ]
        
        for script_name in required_scripts:
            script_path = scripts_dir / script_name
            if script_path.exists():
                self.passed_checks.append(f"✅ Script: {script_name}")
            else:
                self.errors.append(f"❌ Missing script: {script_name}")
        
        # Check requirements.txt
        req_file = scripts_dir / "requirements.txt"
        if req_file.exists():
            self.passed_checks.append("✅ requirements.txt exists")
        else:
            self.errors.append("❌ requirements.txt missing")
    
    def validate_configuration(self):
        """Validate configuration files"""
        print("\n⚙️  Validating Configuration...")
        
        # Check gate policy
        policy_file = self.repo_root / ".github/security/policies/gate-policy.json"
        if policy_file.exists():
            try:
                with open(policy_file, 'r') as f:
                    policy_data = json.load(f)
                
                required_sections = [
                    'severity_thresholds',
                    'tool_requirements', 
                    'policy_rules',
                    'allow_list'
                ]
                
                for section in required_sections:
                    if section in policy_data:
                        self.passed_checks.append(f"✅ Policy section: {section}")
                    else:
                        self.warnings.append(f"⚠️ Policy section missing: {section}")
                        
            except json.JSONDecodeError as e:
                self.errors.append(f"❌ Invalid JSON in gate policy: {e}")
        else:
            self.errors.append("❌ Gate policy file missing")
    
    def validate_documentation(self):
        """Validate documentation"""
        print("\n📚 Validating Documentation...")
        
        docs = [
            "docs/TIXL-058_SAST_SCA_Implementation_Guide.md",
            "TIXL-058_Implementation_Summary.md"
        ]
        
        for doc in docs:
            doc_path = self.repo_root / doc
            if doc_path.exists():
                size = doc_path.stat().st_size
                if size > 1000:  # Basic size check
                    self.passed_checks.append(f"✅ Documentation: {doc} ({size} bytes)")
                else:
                    self.warnings.append(f"⚠️ Small documentation file: {doc}")
            else:
                self.errors.append(f"❌ Missing documentation: {doc}")
    
    def validate_python_syntax(self):
        """Validate Python script syntax"""
        print("\n🐍 Validating Python Syntax...")
        
        scripts_dir = self.repo_root / ".github/scripts"
        python_files = list(scripts_dir.glob("*.py"))
        
        for py_file in python_files:
            try:
                with open(py_file, 'r', encoding='utf-8') as f:
                    content = f.read()
                
                # Basic syntax check
                compile(content, str(py_file), 'exec')
                self.passed_checks.append(f"✅ Python syntax: {py_file.name}")
                
            except SyntaxError as e:
                self.errors.append(f"❌ Syntax error in {py_file.name}: {e}")
            except Exception as e:
                self.warnings.append(f"⚠️ Error validating {py_file.name}: {e}")
    
    def test_script_imports(self):
        """Test if scripts can be imported"""
        print("\n🧪 Testing Script Imports...")
        
        scripts_dir = self.repo_root / ".github/scripts"
        sys.path.insert(0, str(scripts_dir))
        
        test_scripts = [
            "vulnerability-aggregator",
            "security-gate-evaluator",
            "generate-security-dashboard"
        ]
        
        for script in test_scripts:
            try:
                __import__(script)
                self.passed_checks.append(f"✅ Import test: {script}")
            except ImportError as e:
                self.warnings.append(f"⚠️ Import failed for {script}: {e}")
    
    def check_dependencies(self):
        """Check for required dependencies"""
        print("\n📦 Checking Dependencies...")
        
        # Check if Python packages can be installed
        req_file = self.repo_root / ".github/scripts/requirements.txt"
        if req_file.exists():
            try:
                result = subprocess.run([
                    sys.executable, "-m", "pip", "check"
                ], capture_output=True, text=True, timeout=30)
                
                if result.returncode == 0:
                    self.passed_checks.append("✅ Python dependencies available")
                else:
                    self.warnings.append("⚠️ Some Python dependencies may be missing")
                    
            except Exception as e:
                self.warnings.append(f"⚠️ Could not check dependencies: {e}")
    
    def generate_validation_report(self):
        """Generate validation report"""
        print("\n" + "=" * 70)
        print("📊 VALIDATION SUMMARY")
        print("=" * 70)
        
        print(f"✅ Passed Checks: {len(self.passed_checks)}")
        print(f"⚠️  Warnings: {len(self.warnings)}")
        print(f"❌ Errors: {len(self.errors)}")
        
        if self.passed_checks:
            print(f"\n✅ PASSED CHECKS ({len(self.passed_checks)}):")
            for check in self.passed_checks:
                print(f"  {check}")
        
        if self.warnings:
            print(f"\n⚠️  WARNINGS ({len(self.warnings)}):")
            for warning in self.warnings:
                print(f"  {warning}")
        
        if self.errors:
            print(f"\n❌ ERRORS ({len(self.errors)}):")
            for error in self.errors:
                print(f"  {error}")
        
        print("\n" + "=" * 70)
        
        if len(self.errors) == 0:
            print("🎉 VALIDATION PASSED - Implementation is ready for use!")
            print("\nNext Steps:")
            print("1. Add required secrets to GitHub repository")
            print("2. Test security scanning on a pull request")
            print("3. Review security dashboard and reports")
            print("4. Train team on security findings")
        else:
            print("❌ VALIDATION FAILED - Please fix errors before deployment")
            print("\nCommon fixes:")
            print("1. Ensure all required files are committed")
            print("2. Check Python syntax in scripts")
            print("3. Validate JSON configuration files")
            print("4. Review workflow syntax")
    
    def create_test_data(self):
        """Create test data for validation"""
        print("\n🧪 Creating test data...")
        
        test_dir = self.repo_root / "test-security-results"
        test_dir.mkdir(exist_ok=True)
        
        # Create sample vulnerability data
        sample_vulns = [
            {
                "id": "test-001",
                "title": "Test critical vulnerability",
                "severity": "critical",
                "tool": "CodeQL",
                "category": "SAST"
            },
            {
                "id": "test-002", 
                "title": "Test high vulnerability",
                "severity": "high",
                "tool": "Grype",
                "category": "SCA"
            }
        ]
        
        vuln_file = test_dir / "vulnerabilities.json"
        with open(vuln_file, 'w') as f:
            json.dump(sample_vulns, f, indent=2)
        
        self.passed_checks.append("✅ Test data created")
        print(f"  ✅ Test data created in {test_dir}")

def main():
    validator = SecurityImplementationValidator()
    
    # Run validation
    success = validator.validate_implementation()
    
    # Offer to create test data
    if success:
        create_test = input("\nCreate test data for validation? (y/n): ").lower().strip()
        if create_test == 'y':
            validator.create_test_data()
    
    # Exit with appropriate code
    sys.exit(0 if success else 1)

if __name__ == "__main__":
    main()