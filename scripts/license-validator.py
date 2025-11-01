#!/usr/bin/env python3
"""
TiXL License Validator

Automated tool for validating license compliance and generating comprehensive
license reports for the TiXL project. This tool helps ensure adherence to
licensing policies and provides detailed compliance information.

Author: TiXL Contributors
Version: 1.0
Date: 2025-11-02
"""

import os
import sys
import json
import re
import hashlib
import argparse
import logging
from datetime import datetime, timedelta
from pathlib import Path
from typing import Dict, List, Optional, Tuple, Set
from dataclasses import dataclass, asdict
from collections import defaultdict, Counter
import subprocess
import yaml


# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler('license_validator.log'),
        logging.StreamHandler(sys.stdout)
    ]
)
logger = logging.getLogger(__name__)


@dataclass
class LicenseInfo:
    """Information about a specific license."""
    name: str
    spdx_id: str
    is_osi_approved: bool
    is_fsf_libre: bool
    commercial_use: bool
    modifications: bool
    distribution: bool
    patent_use: bool
    private_use: bool
    text: str = ""
    url: str = ""
    compatibility_score: int = 0  # 0-10 scale


@dataclass
class FileLicenseInfo:
    """License information for a specific file."""
    file_path: str
    license_type: str
    copyright_holder: str
    year: str
    license_text: str
    is_compliant: bool
    issues: List[str]
    suggestions: List[str]
    hash_value: str


@dataclass
class DependencyInfo:
    """Information about a dependency license."""
    name: str
    version: str
    license_type: str
    license_file: Optional[str]
    homepage: str
    repository: str
    is_compliant: bool
    compatibility_notes: str
    risk_level: str  # LOW, MEDIUM, HIGH, CRITICAL


@dataclass
class ComplianceReport:
    """Complete compliance report."""
    timestamp: str
    project_name: str
    total_files: int
    compliant_files: int
    non_compliant_files: int
    total_dependencies: int
    compliant_dependencies: int
    non_compliant_dependencies: int
    overall_compliance_score: float
    critical_issues: List[str]
    warnings: List[str]
    recommendations: List[str]
    file_details: List[FileLicenseInfo]
    dependency_details: List[DependencyInfo]


class LicenseValidator:
    """Main license validation engine."""
    
    def __init__(self, project_path: str = "."):
        """Initialize the license validator.
        
        Args:
            project_path: Path to the project directory
        """
        self.project_path = Path(project_path).resolve()
        self.license_database = self._load_license_database()
        self.license_patterns = self._load_license_patterns()
        self.dependency_scanners = self._init_dependency_scanners()
        
    def _load_license_database(self) -> Dict[str, LicenseInfo]:
        """Load the license database with known licenses."""
        licenses = {}
        
        # MIT License
        licenses['MIT'] = LicenseInfo(
            name='MIT License',
            spdx_id='MIT',
            is_osi_approved=True,
            is_fsf_libre=True,
            commercial_use=True,
            modifications=True,
            distribution=True,
            patent_use=True,
            private_use=True,
            compatibility_score=10
        )
        
        # Apache 2.0 License
        licenses['Apache-2.0'] = LicenseInfo(
            name='Apache License 2.0',
            spdx_id='Apache-2.0',
            is_osi_approved=True,
            is_fsf_libre=True,
            commercial_use=True,
            modifications=True,
            distribution=True,
            patent_use=True,
            private_use=True,
            compatibility_score=9
        )
        
        # BSD 2-Clause License
        licenses['BSD-2-Clause'] = LicenseInfo(
            name='BSD 2-Clause License',
            spdx_id='BSD-2-Clause',
            is_osi_approved=True,
            is_fsf_libre=True,
            commercial_use=True,
            modifications=True,
            distribution=True,
            patent_use=False,
            private_use=True,
            compatibility_score=8
        )
        
        # BSD 3-Clause License
        licenses['BSD-3-Clause'] = LicenseInfo(
            name='BSD 3-Clause License',
            spdx_id='BSD-3-Clause',
            is_osi_approved=True,
            is_fsf_libre=True,
            commercial_use=True,
            modifications=True,
            distribution=True,
            patent_use=False,
            private_use=True,
            compatibility_score=8
        )
        
        # GPL v3 License
        licenses['GPL-3.0'] = LicenseInfo(
            name='GNU General Public License v3.0',
            spdx_id='GPL-3.0',
            is_osi_approved=True,
            is_fsf_libre=True,
            commercial_use=True,
            modifications=True,
            distribution=True,
            patent_use=True,
            private_use=True,
            compatibility_score=6  # Lower due to copyleft
        )
        
        # LGPL v3 License
        licenses['LGPL-3.0'] = LicenseInfo(
            name='GNU Lesser General Public License v3.0',
            spdx_id='LGPL-3.0',
            is_osi_approved=True,
            is_fsf_libre=True,
            commercial_use=True,
            modifications=True,
            distribution=True,
            patent_use=True,
            private_use=True,
            compatibility_score=7
        )
        
        # ISC License
        licenses['ISC'] = LicenseInfo(
            name='ISC License',
            spdx_id='ISC',
            is_osi_approved=True,
            is_fsf_libre=True,
            commercial_use=True,
            modifications=True,
            distribution=True,
            patent_use=False,
            private_use=True,
            compatibility_score=8
        )
        
        # Unlicense
        licenses['Unlicense'] = LicenseInfo(
            name='The Unlicense',
            spdx_id='Unlicense',
            is_osi_approved=True,
            is_fsf_libre=True,
            commercial_use=True,
            modifications=True,
            distribution=True,
            patent_use=False,
            private_use=True,
            compatibility_score=10
        )
        
        return licenses
    
    def _load_license_patterns(self) -> Dict[str, List[str]]:
        """Load regex patterns for license detection."""
        patterns = {}
        
        # MIT License patterns
        patterns['MIT'] = [
            r'Permission is hereby granted.*MIT.*license',
            r'MIT License.*Permission is hereby granted',
            r'Copyright.*MIT.*License',
            r'Licensed under the MIT License'
        ]
        
        # Apache 2.0 patterns
        patterns['Apache-2.0'] = [
            r'Licensed under the Apache License.*Version 2\.0',
            r'Apache License.*Version 2\.0.*January 2004',
            r'Copyright.*Apache.*License.*2\.0'
        ]
        
        # BSD patterns
        patterns['BSD'] = [
            r'Redistribution and use in source and binary forms.*BSD',
            r'BSD License.*Redistribution and use',
            r'Copyright.*BSD.*License'
        ]
        
        # GPL patterns
        patterns['GPL'] = [
            r'GNU GENERAL PUBLIC LICENSE.*Version 3',
            r'GPL.*General Public License.*version 3',
            r'Copyright.*GPL.*license'
        ]
        
        # LGPL patterns
        patterns['LGPL'] = [
            r'GNU LESSER GENERAL PUBLIC LICENSE.*Version 3',
            r'LGPL.*Lesser General Public License.*version 3',
            r'Copyright.*LGPL.*license'
        ]
        
        # ISC patterns
        patterns['ISC'] = [
            r'ISC License.*Permission to use',
            r'Copyright.*ISC.*License',
            r'Licensed under ISC'
        ]
        
        # Unlicense patterns
        patterns['Unlicense'] = [
            r'This is free and unencumbered software.*public domain',
            r'The Unlicense.*public domain',
            r'Unlicense.*This is free and unencumbered'
        ]
        
        return patterns
    
    def _init_dependency_scanners(self) -> Dict[str, callable]:
        """Initialize dependency scanning methods."""
        scanners = {}
        
        # NuGet packages.config and .csproj scanning
        scanners['nuget'] = self._scan_nuget_dependencies
        scanners['dotnet'] = self._scan_dotnet_dependencies
        
        # NPM package.json scanning
        scanners['npm'] = self._scan_npm_dependencies
        
        # Python requirements.txt and setup.py scanning
        scanners['python'] = self._scan_python_dependencies
        
        # Generic file-based scanning
        scanners['file'] = self._scan_file_dependencies
        
        return scanners
    
    def scan_files(self, file_extensions: List[str] = None) -> List[FileLicenseInfo]:
        """Scan files in the project for license information.
        
        Args:
            file_extensions: List of file extensions to scan
            
        Returns:
            List of FileLicenseInfo objects
        """
        if file_extensions is None:
            file_extensions = ['.cs', '.js', '.ts', '.py', '.java', '.cpp', '.h', '.c']
        
        file_licenses = []
        
        for ext in file_extensions:
            for file_path in self.project_path.rglob(f'*{ext}'):
                if self._should_exclude_file(file_path):
                    continue
                    
                try:
                    license_info = self._analyze_file(file_path)
                    file_licenses.append(license_info)
                except Exception as e:
                    logger.error(f"Error analyzing file {file_path}: {e}")
        
        return file_licenses
    
    def _should_exclude_file(self, file_path: Path) -> bool:
        """Check if file should be excluded from scanning."""
        exclude_patterns = [
            '.git', '.svn', '.hg',
            'node_modules', 'bower_components',
            'bin', 'obj', 'dist', 'build',
            '.vs', '.vscode', '.idea',
            '__pycache__', '.pytest_cache',
            '.nuget', 'packages'
        ]
        
        for pattern in exclude_patterns:
            if pattern in str(file_path):
                return True
        
        return False
    
    def _analyze_file(self, file_path: Path) -> FileLicenseInfo:
        """Analyze a single file for license information.
        
        Args:
            file_path: Path to the file to analyze
            
        Returns:
            FileLicenseInfo object with license details
        """
        try:
            with open(file_path, 'r', encoding='utf-8', errors='ignore') as f:
                content = f.read()
        except Exception as e:
            logger.error(f"Could not read file {file_path}: {e}")
            return self._create_error_file_info(str(file_path), str(e))
        
        # Detect license type
        license_type = self._detect_license_type(content)
        
        # Extract copyright information
        copyright_holder, year = self._extract_copyright_info(content)
        
        # Extract license text
        license_text = self._extract_license_text(content)
        
        # Check compliance
        is_compliant, issues, suggestions = self._check_compliance(
            file_path, license_type, copyright_holder
        )
        
        # Calculate file hash
        hash_value = hashlib.md5(content.encode()).hexdigest()
        
        return FileLicenseInfo(
            file_path=str(file_path.relative_to(self.project_path)),
            license_type=license_type,
            copyright_holder=copyright_holder,
            year=year,
            license_text=license_text[:500] + "..." if len(license_text) > 500 else license_text,
            is_compliant=is_compliant,
            issues=issues,
            suggestions=suggestions,
            hash_value=hash_value
        )
    
    def _detect_license_type(self, content: str) -> str:
        """Detect the license type from file content.
        
        Args:
            content: File content to analyze
            
        Returns:
            Detected license type
        """
        content_upper = content.upper()
        
        # Check for specific license patterns
        for license_type, patterns in self.license_patterns.items():
            for pattern in patterns:
                if re.search(pattern, content, re.IGNORECASE | re.MULTILINE):
                    return license_type
        
        # Check for generic indicators
        if 'MIT' in content_upper and 'LICENSE' in content_upper:
            return 'MIT'
        elif 'APACHE' in content_upper and 'LICENSE' in content_upper:
            return 'Apache-2.0'
        elif 'BSD' in content_upper and 'LICENSE' in content_upper:
            return 'BSD'
        elif 'GPL' in content_upper and 'LICENSE' in content_upper:
            return 'GPL'
        elif 'LGPL' in content_upper and 'LICENSE' in content_upper:
            return 'LGPL'
        
        return 'UNKNOWN'
    
    def _extract_copyright_info(self, content: str) -> Tuple[str, str]:
        """Extract copyright information from file content.
        
        Args:
            content: File content to analyze
            
        Returns:
            Tuple of (copyright_holder, year)
        """
        # Copyright patterns
        copyright_patterns = [
            r'Copyright\s+\(c\)\s+(\d{4})(?:\s*-\s*\d{4})?\s+(.+)',
            r'Copyright\s+\d{4}(?:\s*-\s*\d{4})?\s+(.+)',
            r'©\s*(\d{4})(?:\s*-\s*\d{4})?\s+(.+)'
        ]
        
        for pattern in copyright_patterns:
            match = re.search(pattern, content, re.IGNORECASE)
            if match:
                year = match.group(1)
                holder = match.group(2).strip() if len(match.groups()) > 1 else ""
                return holder, year
        
        return "", ""
    
    def _extract_license_text(self, content: str) -> str:
        """Extract license text from file content.
        
        Args:
            content: File content to analyze
            
        Returns:
            License text
        """
        # Common license text markers
        license_markers = [
            'MIT License',
            'Apache License',
            'BSD License',
            'GPL License',
            'GNU GENERAL PUBLIC LICENSE',
            'License Agreement',
            'Permission is hereby granted'
        ]
        
        lines = content.split('\n')
        license_lines = []
        in_license = False
        
        for line in lines:
            line_stripped = line.strip()
            
            # Check if we're entering license text
            for marker in license_markers:
                if marker in line_stripped.upper():
                    in_license = True
                    break
            
            if in_license:
                license_lines.append(line)
                
                # Check if we're exiting license text
                if ('END OF TERMS AND CONDITIONS' in line_stripped.upper() or
                    'END OF LICENSE' in line_stripped.upper() or
                    (line_stripped == "" and len(license_lines) > 10)):
                    break
        
        return '\n'.join(license_lines)
    
    def _check_compliance(self, file_path: Path, license_type: str, 
                         copyright_holder: str) -> Tuple[bool, List[str], List[str]]:
        """Check compliance for a file.
        
        Args:
            file_path: Path to the file
            license_type: Detected license type
            copyright_holder: Copyright holder
            
        Returns:
            Tuple of (is_compliant, issues, suggestions)
        """
        issues = []
        suggestions = []
        is_compliant = True
        
        # Check for unknown license
        if license_type == 'UNKNOWN':
            issues.append("License type could not be determined")
            suggestions.append("Add explicit license header")
            is_compliant = False
        
        # Check for missing copyright
        if not copyright_holder:
            issues.append("Missing copyright information")
            suggestions.append("Add copyright notice")
            is_compliant = False
        
        # Check copyright format
        if copyright_holder:
            if 'TiXL' not in copyright_holder and 'Contributor' not in copyright_holder:
                issues.append("Copyright holder may not be TiXL project")
                suggestions.append("Verify copyright attribution")
        
        # Check for proper license type
        if license_type not in self.license_database:
            issues.append(f"Unknown license type: {license_type}")
            suggestions.append("Use approved license types")
            is_compliant = False
        
        return is_compliant, issues, suggestions
    
    def _create_error_file_info(self, file_path: str, error: str) -> FileLicenseInfo:
        """Create FileLicenseInfo for files that couldn't be processed."""
        return FileLicenseInfo(
            file_path=file_path,
            license_type="ERROR",
            copyright_holder="",
            year="",
            license_text="",
            is_compliant=False,
            issues=[f"File processing error: {error}"],
            suggestions=["Check file encoding and permissions"],
            hash_value=""
        )
    
    def scan_dependencies(self) -> List[DependencyInfo]:
        """Scan project dependencies for license information.
        
        Returns:
            List of DependencyInfo objects
        """
        dependencies = []
        
        # Scan different dependency sources
        for scanner_name, scanner_func in self.dependency_scanners.items():
            try:
                deps = scanner_func()
                dependencies.extend(deps)
            except Exception as e:
                logger.warning(f"Dependency scanner '{scanner_name}' failed: {e}")
        
        return dependencies
    
    def _scan_nuget_dependencies(self) -> List[DependencyInfo]:
        """Scan NuGet dependencies."""
        dependencies = []
        
        # Look for packages.config
        packages_config = self.project_path / 'packages.config'
        if packages_config.exists():
            try:
                import xml.etree.ElementTree as ET
                tree = ET.parse(packages_config)
                root = tree.getroot()
                
                for package in root.findall('.//package'):
                    name = package.get('id', '')
                    version = package.get('version', '')
                    
                    dep_info = self._create_dependency_info(name, version, 'nuget')
                    dependencies.append(dep_info)
                    
            except Exception as e:
                logger.error(f"Error parsing packages.config: {e}")
        
        # Look for .csproj files
        for csproj in self.project_path.rglob('*.csproj'):
            try:
                with open(csproj, 'r', encoding='utf-8') as f:
                    content = f.read()
                    
                # Extract PackageReference elements
                import re
                package_refs = re.findall(r'<PackageReference Include="([^"]+)" Version="([^"]+)"', content)
                
                for name, version in package_refs:
                    dep_info = self._create_dependency_info(name, version, 'nuget')
                    dependencies.append(dep_info)
                    
            except Exception as e:
                logger.error(f"Error parsing {csproj}: {e}")
        
        return dependencies
    
    def _scan_dotnet_dependencies(self) -> List[DependencyInfo]:
        """Scan .NET project dependencies."""
        return self._scan_nuget_dependencies()  # Same as NuGet for now
    
    def _scan_npm_dependencies(self) -> List[DependencyInfo]:
        """Scan NPM dependencies."""
        dependencies = []
        
        package_json = self.project_path / 'package.json'
        if package_json.exists():
            try:
                with open(package_json, 'r', encoding='utf-8') as f:
                    package_data = json.load(f)
                
                # Scan dependencies
                for dep_type in ['dependencies', 'devDependencies']:
                    if dep_type in package_data:
                        for name, version in package_data[dep_type].items():
                            dep_info = self._create_dependency_info(name, version, 'npm')
                            dependencies.append(dep_info)
                            
            except Exception as e:
                logger.error(f"Error parsing package.json: {e}")
        
        return dependencies
    
    def _scan_python_dependencies(self) -> List[DependencyInfo]:
        """Scan Python dependencies."""
        dependencies = []
        
        # Look for requirements.txt
        requirements_file = self.project_path / 'requirements.txt'
        if requirements_file.exists():
            try:
                with open(requirements_file, 'r', encoding='utf-8') as f:
                    for line in f:
                        line = line.strip()
                        if line and not line.startswith('#'):
                            # Parse requirements line
                            match = re.match(r'^([a-zA-Z0-9_-]+)([=<>!]+.*)?', line)
                            if match:
                                name = match.group(1)
                                version = match.group(2) if match.group(2) else ""
                                dep_info = self._create_dependency_info(name, version, 'python')
                                dependencies.append(dep_info)
                                
            except Exception as e:
                logger.error(f"Error parsing requirements.txt: {e}")
        
        return dependencies
    
    def _scan_file_dependencies(self) -> List[DependencyInfo]:
        """Scan generic file-based dependencies."""
        dependencies = []
        
        # Look for license files
        for license_file in self.project_path.rglob('LICENSE*'):
            if license_file.is_file():
                try:
                    with open(license_file, 'r', encoding='utf-8') as f:
                        content = f.read()
                    
                    # Try to identify license type from file content
                    license_type = self._detect_license_type(content)
                    
                    dep_info = DependencyInfo(
                        name=license_file.parent.name,
                        version="unknown",
                        license_type=license_type,
                        license_file=str(license_file),
                        homepage="",
                        repository="",
                        is_compliant=license_type in self.license_database,
                        compatibility_notes=self._get_compatibility_notes(license_type),
                        risk_level=self._assess_risk_level(license_type)
                    )
                    dependencies.append(dep_info)
                    
                except Exception as e:
                    logger.error(f"Error processing license file {license_file}: {e}")
        
        return dependencies
    
    def _create_dependency_info(self, name: str, version: str, source: str) -> DependencyInfo:
        """Create DependencyInfo for a dependency.
        
        Args:
            name: Dependency name
            version: Dependency version
            source: Source of dependency information
            
        Returns:
            DependencyInfo object
        """
        # Get license information (simplified)
        license_type = "UNKNOWN"
        is_compliant = False
        compatibility_notes = ""
        risk_level = "MEDIUM"
        
        # For demo purposes, assign some common licenses
        if any(keyword in name.lower() for keyword in ['microsoft', 'system', 'runtime']):
            license_type = "MIT"
            is_compliant = True
            compatibility_notes = "Common Microsoft runtime libraries"
            risk_level = "LOW"
        elif any(keyword in name.lower() for keyword in ['newtonsoft', 'json']):
            license_type = "MIT"
            is_compliant = True
            compatibility_notes = "MIT licensed JSON library"
            risk_level = "LOW"
        
        return DependencyInfo(
            name=name,
            version=version,
            license_type=license_type,
            license_file=None,
            homepage="",
            repository="",
            is_compliant=is_compliant,
            compatibility_notes=compatibility_notes,
            risk_level=risk_level
        )
    
    def _get_compatibility_notes(self, license_type: str) -> str:
        """Get compatibility notes for a license type."""
        if license_type in self.license_database:
            license_info = self.license_database[license_type]
            return f"OSI Approved: {license_info.is_osi_approved}, Compatibility Score: {license_info.compatibility_score}/10"
        return "License not recognized in database"
    
    def _assess_risk_level(self, license_type: str) -> str:
        """Assess risk level for a license type."""
        if license_type not in self.license_database:
            return "HIGH"
        
        license_info = self.license_database[license_type]
        score = license_info.compatibility_score
        
        if score >= 9:
            return "LOW"
        elif score >= 7:
            return "MEDIUM"
        else:
            return "HIGH"
    
    def generate_report(self, file_licenses: List[FileLicenseInfo], 
                       dependencies: List[DependencyInfo]) -> ComplianceReport:
        """Generate a comprehensive compliance report.
        
        Args:
            file_licenses: List of file license information
            dependencies: List of dependency information
            
        Returns:
            ComplianceReport object
        """
        timestamp = datetime.now().isoformat()
        
        # Calculate summary statistics
        total_files = len(file_licenses)
        compliant_files = sum(1 for f in file_licenses if f.is_compliant)
        non_compliant_files = total_files - compliant_files
        
        total_deps = len(dependencies)
        compliant_deps = sum(1 for d in dependencies if d.is_compliant)
        non_compliant_deps = total_deps - compliant_deps
        
        # Calculate overall compliance score
        file_score = (compliant_files / total_files * 100) if total_files > 0 else 100
        dep_score = (compliant_deps / total_deps * 100) if total_deps > 0 else 100
        overall_score = (file_score + dep_score) / 2
        
        # Collect issues and recommendations
        critical_issues = []
        warnings = []
        recommendations = []
        
        # File-related issues
        for file_info in file_licenses:
            if not file_info.is_compliant:
                critical_issues.extend([f"File: {file_info.file_path} - {issue}" 
                                      for issue in file_info.issues])
                recommendations.extend([f"File: {file_info.file_path} - {suggestion}" 
                                      for suggestion in file_info.suggestions])
            else:
                warnings.extend([f"File: {file_info.file_path} - {issue}" 
                               for issue in file_info.issues if 'may not' in issue.lower()])
        
        # Dependency-related issues
        for dep_info in dependencies:
            if not dep_info.is_compliant:
                critical_issues.append(f"Dependency: {dep_info.name} - {dep_info.compatibility_notes}")
                recommendations.append(f"Dependency: {dep_info.name} - Review license compatibility")
            elif dep_info.risk_level == "HIGH":
                warnings.append(f"Dependency: {dep_info.name} - High risk license: {dep_info.license_type}")
        
        # General recommendations
        if non_compliant_files > 0:
            recommendations.append("Consider implementing automated license header insertion")
        if total_deps > 0:
            recommendations.append("Regular dependency license audits recommended")
        if overall_score < 90:
            recommendations.append("Overall compliance score below 90% - review needed")
        
        return ComplianceReport(
            timestamp=timestamp,
            project_name=self.project_path.name,
            total_files=total_files,
            compliant_files=compliant_files,
            non_compliant_files=non_compliant_files,
            total_dependencies=total_deps,
            compliant_dependencies=compliant_deps,
            non_compliant_dependencies=non_compliant_deps,
            overall_compliance_score=overall_score,
            critical_issues=critical_issues,
            warnings=warnings,
            recommendations=recommendations,
            file_details=file_licenses,
            dependency_details=dependencies
        )
    
    def export_report(self, report: ComplianceReport, output_format: str = 'json', 
                     output_file: str = None) -> str:
        """Export compliance report to file.
        
        Args:
            report: ComplianceReport object
            output_format: Output format ('json', 'html', 'csv', 'markdown')
            output_file: Output file path
            
        Returns:
            Path to output file
        """
        if output_file is None:
            timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
            output_file = f"license_compliance_report_{timestamp}.{output_format}"
        
        if output_format.lower() == 'json':
            return self._export_json_report(report, output_file)
        elif output_format.lower() == 'html':
            return self._export_html_report(report, output_file)
        elif output_format.lower() == 'csv':
            return self._export_csv_report(report, output_file)
        elif output_format.lower() == 'markdown':
            return self._export_markdown_report(report, output_file)
        else:
            raise ValueError(f"Unsupported output format: {output_format}")
    
    def _export_json_report(self, report: ComplianceReport, output_file: str) -> str:
        """Export report as JSON."""
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(asdict(report), f, indent=2, default=str)
        return output_file
    
    def _export_html_report(self, report: ComplianceReport, output_file: str) -> str:
        """Export report as HTML."""
        html_content = f"""
<!DOCTYPE html>
<html>
<head>
    <title>TiXL License Compliance Report</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; }}
        .header {{ background-color: #f0f0f0; padding: 20px; border-radius: 5px; }}
        .summary {{ display: flex; justify-content: space-between; margin: 20px 0; }}
        .metric {{ text-align: center; padding: 10px; border: 1px solid #ddd; border-radius: 5px; }}
        .critical {{ color: red; }}
        .warning {{ color: orange; }}
        .good {{ color: green; }}
        table {{ border-collapse: collapse; width: 100%; }}
        th, td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
        th {{ background-color: #f2f2f2; }}
    </style>
</head>
<body>
    <div class="header">
        <h1>TiXL License Compliance Report</h1>
        <p>Generated: {report.timestamp}</p>
        <p>Project: {report.project_name}</p>
    </div>
    
    <div class="summary">
        <div class="metric">
            <h3>Overall Score</h3>
            <p class="{'good' if report.overall_compliance_score >= 90 else 'warning' if report.overall_compliance_score >= 70 else 'critical'}">
                {report.overall_compliance_score:.1f}%
            </p>
        </div>
        <div class="metric">
            <h3>Files</h3>
            <p>{report.compliant_files}/{report.total_files} Compliant</p>
        </div>
        <div class="metric">
            <h3>Dependencies</h3>
            <p>{report.compliant_dependencies}/{report.total_dependencies} Compliant</p>
        </div>
        <div class="metric">
            <h3>Critical Issues</h3>
            <p class="critical">{len(report.critical_issues)}</p>
        </div>
    </div>
    
    <h2>Critical Issues</h2>
    <ul>
        {''.join(f'<li class="critical">{issue}</li>' for issue in report.critical_issues)}
    </ul>
    
    <h2>Recommendations</h2>
    <ul>
        {''.join(f'<li>{rec}</li>' for rec in report.recommendations)}
    </ul>
    
    <h2>File Details</h2>
    <table>
        <tr>
            <th>File</th>
            <th>License</th>
            <th>Copyright</th>
            <th>Compliant</th>
            <th>Issues</th>
        </tr>
        {''.join(f'''<tr>
            <td>{file_info.file_path}</td>
            <td>{file_info.license_type}</td>
            <td>{file_info.copyright_holder}</td>
            <td class="{'good' if file_info.is_compliant else 'critical'}">
                {'Yes' if file_info.is_compliant else 'No'}
            </td>
            <td>{', '.join(file_info.issues) if file_info.issues else 'None'}</td>
        </tr>''' for file_info in report.file_details)}
    </table>
</body>
</html>
        """
        
        with open(output_file, 'w', encoding='utf-8') as f:
            f.write(html_content)
        return output_file
    
    def _export_csv_report(self, report: ComplianceReport, output_file: str) -> str:
        """Export report as CSV."""
        import csv
        
        with open(output_file, 'w', newline='', encoding='utf-8') as f:
            writer = csv.writer(f)
            
            # Write summary
            writer.writerow(['TiXL License Compliance Report'])
            writer.writerow(['Generated', report.timestamp])
            writer.writerow(['Project', report.project_name])
            writer.writerow([])
            writer.writerow(['Overall Score', f"{report.overall_compliance_score:.1f}%"])
            writer.writerow(['Total Files', report.total_files])
            writer.writerow(['Compliant Files', report.compliant_files])
            writer.writerow(['Total Dependencies', report.total_dependencies])
            writer.writerow(['Compliant Dependencies', report.compliant_dependencies])
            writer.writerow([])
            
            # Write critical issues
            writer.writerow(['Critical Issues'])
            for issue in report.critical_issues:
                writer.writerow([issue])
            writer.writerow([])
            
            # Write file details
            writer.writerow(['File Details'])
            writer.writerow(['File Path', 'License Type', 'Copyright Holder', 'Year', 'Compliant', 'Issues'])
            for file_info in report.file_details:
                writer.writerow([
                    file_info.file_path,
                    file_info.license_type,
                    file_info.copyright_holder,
                    file_info.year,
                    'Yes' if file_info.is_compliant else 'No',
                    '; '.join(file_info.issues)
                ])
        
        return output_file
    
    def _export_markdown_report(self, report: ComplianceReport, output_file: str) -> str:
        """Export report as Markdown."""
        markdown_content = f"""# TiXL License Compliance Report

**Generated:** {report.timestamp}  
**Project:** {report.project_name}

## Summary

| Metric | Value |
|--------|-------|
| Overall Compliance Score | {report.overall_compliance_score:.1f}% |
| Files | {report.compliant_files}/{report.total_files} compliant |
| Dependencies | {report.compliant_dependencies}/{report.total_dependencies} compliant |
| Critical Issues | {len(report.critical_issues)} |

## Critical Issues

"""
        
        for issue in report.critical_issues:
            markdown_content += f"- ⚠️ {issue}\n"
        
        markdown_content += "\n## Recommendations\n\n"
        
        for rec in report.recommendations:
            markdown_content += f"- {rec}\n"
        
        markdown_content += "\n## File Details\n\n"
        markdown_content += "| File | License | Copyright | Compliant | Issues |\n"
        markdown_content += "|------|---------|-----------|-----------|--------|\n"
        
        for file_info in report.file_details:
            compliant_status = "✅" if file_info.is_compliant else "❌"
            issues = ", ".join(file_info.issues) if file_info.issues else "None"
            markdown_content += f"| {file_info.file_path} | {file_info.license_type} | {file_info.copyright_holder} | {compliant_status} | {issues} |\n"
        
        markdown_content += "\n## Dependencies\n\n"
        markdown_content += "| Name | Version | License | Compliant | Risk Level |\n"
        markdown_content += "|------|---------|---------|-----------|------------|\n"
        
        for dep_info in report.dependency_details:
            compliant_status = "✅" if dep_info.is_compliant else "❌"
            risk_emoji = "🟢" if dep_info.risk_level == "LOW" else "🟡" if dep_info.risk_level == "MEDIUM" else "🔴"
            markdown_content += f"| {dep_info.name} | {dep_info.version} | {dep_info.license_type} | {compliant_status} | {risk_emoji} {dep_info.risk_level} |\n"
        
        with open(output_file, 'w', encoding='utf-8') as f:
            f.write(markdown_content)
        return output_file


def main():
    """Main entry point for the license validator."""
    parser = argparse.ArgumentParser(description='TiXL License Compliance Validator')
    parser.add_argument('--project-path', '-p', default='.', 
                       help='Path to the project directory')
    parser.add_argument('--output-format', '-f', choices=['json', 'html', 'csv', 'markdown'], 
                       default='markdown', help='Output format for the report')
    parser.add_argument('--output-file', '-o', help='Output file path')
    parser.add_argument('--extensions', '-e', nargs='+', 
                       help='File extensions to scan (default: common source files)')
    parser.add_argument('--verbose', '-v', action='store_true', 
                       help='Enable verbose logging')
    parser.add_argument('--quiet', '-q', action='store_true', 
                       help='Suppress non-critical output')
    
    args = parser.parse_args()
    
    if args.verbose:
        logging.getLogger().setLevel(logging.DEBUG)
    elif args.quiet:
        logging.getLogger().setLevel(logging.WARNING)
    
    try:
        # Initialize validator
        validator = LicenseValidator(args.project_path)
        
        # Scan files
        logger.info("Scanning files for license information...")
        file_licenses = validator.scan_files(args.extensions)
        logger.info(f"Found {len(file_licenses)} files")
        
        # Scan dependencies
        logger.info("Scanning dependencies...")
        dependencies = validator.scan_dependencies()
        logger.info(f"Found {len(dependencies)} dependencies")
        
        # Generate report
        logger.info("Generating compliance report...")
        report = validator.generate_report(file_licenses, dependencies)
        
        # Export report
        output_file = validator.export_report(report, args.output_format, args.output_file)
        
        # Print summary
        print(f"\n{'='*50}")
        print(f"TiXL License Compliance Report")
        print(f"{'='*50}")
        print(f"Overall Compliance Score: {report.overall_compliance_score:.1f}%")
        print(f"Files: {report.compliant_files}/{report.total_files} compliant")
        print(f"Dependencies: {report.compliant_dependencies}/{report.total_dependencies} compliant")
        print(f"Critical Issues: {len(report.critical_issues)}")
        print(f"Report saved to: {output_file}")
        
        if report.critical_issues:
            print(f"\nCritical Issues Found:")
            for issue in report.critical_issues[:5]:  # Show first 5
                print(f"  - {issue}")
            if len(report.critical_issues) > 5:
                print(f"  ... and {len(report.critical_issues) - 5} more")
        
        return 0 if report.overall_compliance_score >= 90 else 1
        
    except Exception as e:
        logger.error(f"Validation failed: {e}")
        if args.verbose:
            raise
        return 1


if __name__ == '__main__':
    sys.exit(main())
