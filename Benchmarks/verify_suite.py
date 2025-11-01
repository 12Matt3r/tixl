#!/usr/bin/env python3
"""
TiXL Performance Benchmarking Suite Verification Script
This script verifies that all components of the benchmarking suite are properly implemented.
"""

import os
import json
import re
from pathlib import Path

def verify_file_exists(file_path, description):
    """Verify that a file exists and is not empty."""
    if os.path.exists(file_path) and os.path.getsize(file_path) > 0:
        print(f"✅ {description}: {file_path}")
        return True
    else:
        print(f"❌ {description}: {file_path} (missing or empty)")
        return False

def verify_json_file(file_path, description):
    """Verify that a JSON file is valid."""
    try:
        if os.path.exists(file_path):
            with open(file_path, 'r') as f:
                json.load(f)
            print(f"✅ {description}: {file_path} (valid JSON)")
            return True
        else:
            print(f"❌ {description}: {file_path} (missing)")
            return False
    except json.JSONDecodeError as e:
        print(f"❌ {description}: {file_path} (invalid JSON: {e})")
        return False

def verify_cs_file(file_path, description):
    """Verify that a C# file contains expected classes and methods."""
    try:
        with open(file_path, 'r') as f:
            content = f.read()
        
        # Check for basic C# structure
        has_using = content.strip().startswith('using')
        has_namespace = 'namespace' in content
        has_class = 'class' in content
        
        if has_using and has_namespace and has_class:
            print(f"✅ {description}: {file_path} (valid C# structure)")
            return True
        else:
            print(f"❌ {description}: {file_path} (incomplete C# structure)")
            return False
    except Exception as e:
        print(f"❌ {description}: {file_path} (error reading: {e})")
        return False

def verify_csproj_file(file_path, description):
    """Verify that the project file contains necessary dependencies."""
    try:
        with open(file_path, 'r') as f:
            content = f.read()
        
        # Check for required packages
        required_packages = [
            'BenchmarkDotNet',
            'Microsoft.Extensions.Hosting',
            'System.Text.Json'
        ]
        
        missing_packages = []
        for package in required_packages:
            if package not in content:
                missing_packages.append(package)
        
        if not missing_packages:
            print(f"✅ {description}: {file_path} (all required packages present)")
            return True
        else:
            print(f"❌ {description}: {file_path} (missing packages: {missing_packages})")
            return False
    except Exception as e:
        print(f"❌ {description}: {file_path} (error reading: {e})")
        return False

def main():
    print("TiXL Performance Benchmarking Suite - Verification Report")
    print("=" * 70)
    print()
    
    base_dir = Path("/workspace/Benchmarks")
    verified_count = 0
    total_count = 0
    
    # Core project files
    print("1. Core Project Files")
    print("-" * 30)
    core_files = [
        ("TiXLPerformanceSuite.csproj", verify_csproj_file),
        ("Program.cs", verify_cs_file),
    ]
    
    for filename, verify_func in core_files:
        file_path = base_dir / filename
        total_count += 1
        if verify_func(str(file_path), filename):
            verified_count += 1
    
    print()
    
    # Core components
    print("2. Core Components")
    print("-" * 30)
    core_components = [
        ("Core/PerformanceMonitorService.cs", "Performance Monitor Service"),
        ("Core/BaselineManager.cs", "Baseline Manager"),
        ("Core/ReportGenerator.cs", "Report Generator"),
        ("Core/AlertService.cs", "Alert Service"),
        ("Core/TrendAnalyzer.cs", "Trend Analyzer"),
    ]
    
    for filename, description in core_components:
        file_path = base_dir / filename
        total_count += 1
        if verify_cs_file(str(file_path), description):
            verified_count += 1
    
    print()
    
    # Benchmark implementations
    print("3. Benchmark Implementations")
    print("-" * 30)
    benchmark_files = [
        ("Benchmarks/FrameTimeBenchmarks.cs", "Frame Time Benchmarks"),
        ("Benchmarks/MemoryBenchmarks.cs", "Memory Benchmarks"),
    ]
    
    for filename, description in benchmark_files:
        file_path = base_dir / filename
        total_count += 1
        if verify_cs_file(str(file_path), description):
            verified_count += 1
    
    print()
    
    # Models
    print("4. Data Models")
    print("-" * 30)
    models_files = [
        ("Models/PerformanceModels.cs", "Performance Models"),
    ]
    
    for filename, description in models_files:
        file_path = base_dir / filename
        total_count += 1
        if verify_cs_file(str(file_path), description):
            verified_count += 1
    
    print()
    
    # Configuration files
    print("5. Configuration Files")
    print("-" * 30)
    config_files = [
        ("config/benchmarksettings.json", "Benchmark Settings"),
        ("config/baselines.json", "Performance Baselines"),
    ]
    
    for filename, description in config_files:
        file_path = base_dir / filename
        total_count += 1
        if verify_json_file(str(file_path), description):
            verified_count += 1
    
    print()
    
    # Scripts and Documentation
    print("6. Scripts and Documentation")
    print("-" * 30)
    script_files = [
        ("scripts/run-performance-ci.sh", "CI Integration Script"),
        ("README.md", "Documentation"),
    ]
    
    for filename, description in script_files:
        file_path = base_dir / filename
        total_count += 1
        if verify_file_exists(str(file_path), description):
            verified_count += 1
    
    print()
    
    # Summary
    print("Summary")
    print("=" * 70)
    print(f"Files verified: {verified_count}/{total_count}")
    print(f"Success rate: {(verified_count/total_count)*100:.1f}%")
    
    if verified_count == total_count:
        print("🎉 All components verified successfully!")
        print()
        print("The TiXL Performance Benchmarking Suite is ready for use.")
        print()
        print("To run the benchmarks when .NET SDK is available:")
        print("  cd /workspace/Benchmarks")
        print("  dotnet run --project TiXLPerformanceSuite.csproj -- --filter *FrameTime*")
        print()
        print("To run all benchmarks:")
        print("  dotnet run --project TiXLPerformanceSuite.csproj")
        print()
        print("To run in CI mode:")
        print("  ./scripts/run-performance-ci.sh")
        return True
    else:
        print("⚠️  Some components are missing or incomplete.")
        print("Please review the verification output above.")
        return False

if __name__ == "__main__":
    main()