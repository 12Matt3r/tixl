#!/usr/bin/env pwsh
# TiXL Local Quality Check Script
# This script validates code quality locally before committing
# Mirrors the CI/CD quality gates for faster feedback

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [switch]$Quick,
    
    [Parameter(Mandatory = $false)]
    [switch]$Comprehensive,
    
    [Parameter(Mandatory = $false)]
    [switch]$Fix,
    
    [Parameter(Mandatory = $false)]
    [string]$Target = "."
)

$ErrorActionPreference = "Stop"

# Configuration
$Config = @{
    SolutionPath = "TiXL.sln"
    TestTimeout = 300 # 5 minutes
    CoverageThreshold = 80
    MaxWarnings = 0
}

Write-Host "🔍 TiXL Local Quality Check" -ForegroundColor Cyan
Write-Host "===========================" -ForegroundColor Cyan
Write-Host ""

# Function to write colored output
function Write-Quiet {
    param([string]$Message, [ConsoleColor]$Color = "White")
    if (-not $Quiet) {
        Write-Host $Message -ForegroundColor $Color
    }
}

function Write-Success {
    param([string]$Message)
    Write-Host "✅ $Message" -ForegroundColor Green
}

function Write-Warning {
    param([string]$Message)
    Write-Host "⚠️  $Message" -ForegroundColor Yellow
}

function Write-Error {
    param([string]$Message)
    Write-Host "❌ $Message" -ForegroundColor Red
}

function Write-Info {
    param([string]$Message)
    Write-Host "ℹ️  $Message" -ForegroundColor Blue
}

# Step 1: Validate solution file
function Test-SolutionExists {
    Write-Info "Step 1: Validating solution file..."
    
    if (-not (Test-Path $Config.SolutionPath)) {
        Write-Error "Solution file not found: $($Config.SolutionPath)"
        return $false
    }
    
    Write-Success "Solution file found"
    return $true
}

# Step 2: Check code formatting
function Test-CodeFormatting {
    Write-Info "Step 2: Checking code formatting..."
    
    try {
        $formatResult = & dotnet format --verify-no-changes --verbosity quiet 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Code formatting is correct"
            return $true
        } else {
            Write-Warning "Code formatting issues detected"
            if ($Fix) {
                Write-Info "Attempting to fix formatting issues..."
                & dotnet format --fix-style usings --verbosity quiet
                & dotnet format --fix-style analyzers --verbosity quiet
                Write-Success "Formatting issues fixed"
                return $true
            } else {
                Write-Info "Run with -Fix to automatically fix formatting issues"
                return $false
            }
        }
    }
    catch {
        Write-Error "Failed to check formatting: $_"
        return $false
    }
}

# Step 3: Build solution
function Test-BuildSolution {
    Write-Info "Step 3: Building solution..."
    
    try {
        $buildOutput = & dotnet build $Config.SolutionPath --configuration Debug --no-restore --verbosity minimal 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Build completed successfully"
            
            # Check for warnings in output
            $warnings = $buildOutput | Where-Object { $_ -match "warning\s+CS\d+:" }
            if ($warnings) {
                Write-Warning "Build warnings detected: $($warnings.Count)"
                foreach ($warning in $warnings) {
                    Write-Host "  $warning" -ForegroundColor Yellow
                }
            } else {
                Write-Success "No build warnings detected"
            }
            return $true
        } else {
            Write-Error "Build failed"
            return $false
        }
    }
    catch {
        Write-Error "Build failed: $_"
        return $false
    }
}

# Step 4: Run PowerShell warning check
function Test-WarningAnalysis {
    Write-Info "Step 4: Running warning analysis..."
    
    $warningScript = "docs/check-warnings.ps1"
    if (Test-Path $warningScript) {
        try {
            & .\$warningScript -SolutionPath $Config.SolutionPath -OutputPath "warning-analysis.md" -ShowProgress
            if ($LASTEXITCODE -eq 0) {
                Write-Success "No warnings detected in code analysis"
                return $true
            } else {
                Write-Warning "Warnings detected in code analysis"
                if (Test-Path "warning-analysis.md") {
                    Write-Info "See warning-analysis.md for details"
                }
                return $false
            }
        }
        catch {
            Write-Error "Warning analysis failed: $_"
            return $false
        }
    } else {
        Write-Warning "Warning check script not found, skipping..."
        return $true
    }
}

# Step 5: Run tests
function Test-RunTests {
    if ($Quick) {
        Write-Info "Step 5: Skipping tests (Quick mode)"
        return $true
    }
    
    Write-Info "Step 5: Running test suite..."
    
    try {
        # Find test projects
        $testProjects = Get-ChildItem -Path $Target -Recurse -Filter "*Tests.csproj" -File
        
        if (-not $testProjects) {
            Write-Warning "No test projects found"
            return $true
        }
        
        Write-Info "Found $($testProjects.Count) test project(s)"
        
        $allTestsPassed = $true
        $totalTests = 0
        $failedTests = 0
        
        foreach ($project in $testProjects) {
            Write-Info "Running tests in $($project.Name)..."
            
            $testResult = & dotnet test $project.FullName --configuration Debug --no-build --collect:"XPlat Code Coverage" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura --verbosity minimal 2>&1
            
            if ($LASTEXITCODE -eq 0) {
                Write-Success "Tests passed: $($project.Name)"
                
                # Try to extract test count from output
                if ($testResult -match "Passed!\s+(\d+)\s+test") {
                    $testCount = [int]$matches[1]
                    $totalTests += $testCount
                    Write-Info "  Tests executed: $testCount"
                }
            } else {
                Write-Error "Tests failed: $($project.Name)"
                $allTestsPassed = $false
                $failedTests++
            }
        }
        
        if ($allTestsPassed) {
            Write-Success "All test suites passed ($totalTests tests total)"
            return $true
        } else {
            Write-Error "$failedTests test project(s) failed"
            return $false
        }
    }
    catch {
        Write-Error "Test execution failed: $_"
        return $false
    }
}

# Step 6: Check code coverage
function Test-CodeCoverage {
    if ($Quick) {
        Write-Info "Step 6: Skipping coverage analysis (Quick mode)"
        return $true
    }
    
    Write-Info "Step 6: Analyzing code coverage..."
    
    try {
        $coverageFiles = Get-ChildItem -Path $Target -Recurse -Filter "coverage.cobertura.xml" -File
        
        if (-not $coverageFiles) {
            Write-Warning "No coverage files found"
            return $true
        }
        
        Write-Info "Found $($coverageFiles.Count) coverage file(s)"
        
        $totalCoverage = 0
        $fileCount = 0
        
        foreach ($file in $coverageFiles) {
            # Simple coverage extraction (in real implementation, would use proper XML parser)
            $content = Get-Content $file.FullName -Raw
            
            # Look for coverage percentage in cobertura format
            if ($content -match 'line-rate="([\d.]+)"') {
                $coverage = [math]::Round([double]$matches[1] * 100, 2)
                $totalCoverage += $coverage
                $fileCount++
                Write-Info "Coverage: $coverage% ($($file.Name))"
            }
        }
        
        if ($fileCount -gt 0) {
            $averageCoverage = [math]::Round($totalCoverage / $fileCount, 2)
            Write-Info "Average coverage: $averageCoverage%"
            
            if ($averageCoverage -ge $Config.CoverageThreshold) {
                Write-Success "Code coverage meets threshold ($($Config.CoverageThreshold)%)"
                return $true
            } else {
                Write-Warning "Code coverage below threshold ($($Config.CoverageThreshold)%)"
                return $false
            }
        } else {
            Write-Warning "Could not extract coverage information"
            return $true
        }
    }
    catch {
        Write-Error "Coverage analysis failed: $_"
        return $false
    }
}

# Step 7: Static analysis
function Test-StaticAnalysis {
    if ($Quick) {
        Write-Info "Step 7: Skipping static analysis (Quick mode)"
        return $true
    }
    
    Write-Info "Step 7: Running static analysis..."
    
    try {
        # Build with treat warnings as errors
        $analysisOutput = & dotnet build $Config.SolutionPath --configuration Release /p:TreatWarningsAsErrors=true /p:EnforceCodeStyleInBuild=true --verbosity minimal 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Static analysis passed"
            return $true
        } else {
            Write-Warning "Static analysis detected issues"
            return $false
        }
    }
    catch {
        Write-Error "Static analysis failed: $_"
        return $false
    }
}

# Step 8: Performance validation (optional)
function Test-PerformanceValidation {
    if ($Quick -or -not (Test-Path "Benchmarks")) {
        Write-Info "Step 8: Skipping performance validation"
        return $true
    }
    
    Write-Info "Step 8: Running performance validation..."
    
    try {
        # Run quick benchmark suite
        Write-Info "Running quick performance benchmarks..."
        
        # Install BenchmarkDotNet if not available
        $null = & dotnet tool update -g BenchmarkDotNet.Cli 2>$null
        
        $benchmarkResult = & dotnet run --project Benchmarks --configuration Release -- --job short --filter "*" --exporters json --artifacts ./local-benchmark-results 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Performance benchmarks completed"
            return $true
        } else {
            Write-Warning "Performance validation had issues"
            return $false
        }
    }
    catch {
        Write-Warning "Performance validation failed: $_"
        return $false
    }
}

# Step 9: Generate summary
function Show-QualitySummary {
    param([bool]$AllPassed)
    
    Write-Host ""
    Write-Host "📊 Quality Check Summary" -ForegroundColor Cyan
    Write-Host "=======================" -ForegroundColor Cyan
    
    if ($AllPassed) {
        Write-Success "🎉 All quality checks passed!"
        Write-Info "Your code is ready for commit and CI/CD"
    } else {
        Write-Warning "⚠️  Some quality checks failed"
        Write-Info "Please review and fix the issues before committing"
    }
    
    # Cleanup
    if (Test-Path "warning-analysis.md") {
        Remove-Item "warning-analysis.md" -Force
    }
    
    if (Test-Path "local-benchmark-results") {
        Remove-Item "local-benchmark-results" -Recurse -Force
    }
}

# Main execution
function Main {
    $startTime = Get-Date
    $results = @()
    
    # Run all checks
    $checks = @(
        @{ Name = "Solution Validation"; Script = ${function:Test-SolutionExists} }
        @{ Name = "Code Formatting"; Script = ${function:Test-CodeFormatting} }
        @{ Name = "Build Solution"; Script = ${function:Test-BuildSolution} }
        @{ Name = "Warning Analysis"; Script = ${function:Test-WarningAnalysis} }
        @{ Name = "Test Execution"; Script = ${function:Test-RunTests} }
        @{ Name = "Code Coverage"; Script = ${function:Test-CodeCoverage} }
        @{ Name = "Static Analysis"; Script = ${function:Test-StaticAnalysis} }
        @{ Name = "Performance Validation"; Script = ${function:Test-PerformanceValidation} }
    )
    
    if ($Comprehensive) {
        Write-Info "Running comprehensive quality check..."
    } elseif ($Quick) {
        Write-Info "Running quick quality check..."
    }
    
    foreach ($check in $checks) {
        if ($Quick -and $check.Name -in @("Test Execution", "Code Coverage", "Static Analysis", "Performance Validation")) {
            continue
        }
        
        try {
            $result = & $check.Script
            $results += [PSCustomObject]@{
                Check = $check.Name
                Passed = $result
            }
            
            if (-not $result) {
                Write-Error "$($check.Name) failed"
            }
        }
        catch {
            Write-Error "$($check.Name) failed with exception: $_"
            $results += [PSCustomObject]@{
                Check = $check.Name
                Passed = $false
            }
        }
    }
    
    # Generate final result
    $allPassed = $results | Where-Object { $_.Passed } | Measure-Object | Select-Object -ExpandProperty Count
    $totalChecks = $results.Count
    $passedChecks = $allPassed
    
    $endTime = Get-Date
    $duration = $endTime - $startTime
    
    Write-Host ""
    Write-Info "Quality check completed in $($duration.TotalSeconds.ToString('F1')) seconds"
    Write-Info "Results: $passedChecks/$totalChecks checks passed"
    
    Show-QualitySummary -AllPassed ($passedChecks -eq $totalChecks)
    
    # Return exit code
    if ($passedChecks -eq $totalChecks) {
        exit 0
    } else {
        exit 1
    }
}

# Execute main function
Main
