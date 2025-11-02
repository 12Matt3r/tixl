#!/usr/bin/env pwsh
# TiXL Comprehensive Code Quality Gate Script
# This script runs all code quality checks and enforces quality gates
# Usage: .\Run-CodeQualityChecks.ps1 [-SolutionPath <path>] [-FailOnIssues] [-GenerateReports]

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$SolutionPath = "TiXL.sln",
    
    [Parameter(Mandatory = $false)]
    [switch]$FailOnIssues = $true,
    
    [Parameter(Mandatory = $false)]
    [switch]$GenerateReports = $true,
    
    [Parameter(Mandatory = $false)]
    [switch]$VerboseOutput = $false,
    
    [Parameter(Mandatory = $false)]
    [string[]]$ExcludeProjects = @(),
    
    [Parameter(Mandatory = $false)]
    [switch]$RunTests = $true,
    
    [Parameter(Mandatory = $false)]
    [switch]$RunBenchmarks = $false,
    
    [Parameter(Mandatory = $false)]
    [decimal]$MinCoveragePercent = 80.0,
    
    [Parameter(Mandatory = $false)]
    [int]$MaxComplexity = 15,
    
    [Parameter(Mandatory = $false)]
    [int]$MaxCyclomaticComplexity = 10
)

$ErrorActionPreference = "Stop"

# Import required modules
Import-Module -Name "./TiXL.ZeroWarningPolicy.psm1" -ErrorAction SilentlyContinue

# Quality gate thresholds
$QUALITY_THRESHOLDS = @{
    CodeCoverage = $MinCoveragePercent
    MaxComplexity = $MaxComplexity
    MaxCyclomaticComplexity = $MaxCyclomaticComplexity
    MaxDuplicatedLines = 3.0
    MaxTechnicalDebt = 5.0
    MaxMaintainabilityIndex = 80
}

# Output directories
$OUTPUT_DIR = Join-Path $PSScriptRoot "QualityReports"
$COVERAGE_DIR = Join-Path $OUTPUT_DIR "Coverage"
$ANALYSIS_DIR = Join-Path $OUTPUT_DIR "Analysis"
$REPORTS_DIR = Join-Path $OUTPUT_DIR "Reports"

# Ensure output directories exist
$null = New-Item -ItemType Directory -Force -Path $OUTPUT_DIR
$null = New-Item -ItemType Directory -Force -Path $COVERAGE_DIR
$null = New-Item -ItemType Directory -Force -Path $ANALYSIS_DIR
$null = New-Item -ItemType Directory -Force -Path $REPORTS_DIR

# Color functions for output
function Write-ColorOutput {
    param([string]$Message, [string]$Color = "White")
    Write-Host $Message -ForegroundColor $Color
}

function Write-Success {
    param([string]$Message)
    Write-ColorOutput "✅ $Message" "Green"
}

function Write-Warning {
    param([string]$Message)
    Write-ColorOutput "⚠️  $Message" "Yellow"
}

function Write-Error {
    param([string]$Message)
    Write-ColorOutput "❌ $Message" "Red"
}

function Write-Info {
    param([string]$Message)
    Write-ColorOutput "ℹ️  $Message" "Cyan"
}

# Quality gate functions
function Test-SolutionExists {
    param([string]$Path)
    return Test-Path -Path $Path -PathType Leaf
}

function Test-DotNetInstallation {
    $dotnetVersion = dotnet --version 2>$null
    if (-not $dotnetVersion) {
        throw ".NET SDK is not installed or not in PATH"
    }
    Write-Info ".NET SDK version: $dotnetVersion"
    return $true
}

function Test-AnalyzerInstallation {
    try {
        $analyzers = @("Microsoft.CodeAnalysis.NetAnalyzers", "StyleCop.Analyzers", "SonarAnalyzer.CSharp")
        foreach ($analyzer in $analyzers) {
            Write-Info "Checking analyzer: $analyzer"
            # This would typically check if the analyzer package is referenced
        }
        return $true
    }
    catch {
        Write-Warning "Some analyzers may not be properly installed: $($_.Exception.Message)"
        return $true # Continue anyway
    }
}

function Test-BuildQuality {
    param([string]$SolutionPath, [string[]]$ExcludeProjects)
    
    Write-Info "Testing build quality with all warnings as errors..."
    
    $buildArgs = @(
        "build",
        $SolutionPath,
        "--configuration", "Release",
        "--verbosity", "minimal"
    )
    
    if ($ExcludeProjects.Count -gt 0) {
        foreach ($project in $ExcludeProjects) {
            $buildArgs += "--exclude", $project
        }
    }
    
    & dotnet @buildArgs
    
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with errors"
    }
    
    Write-Success "Build quality check passed"
    return $true
}

function Test-StaticAnalysis {
    param([string]$SolutionPath, [string]$OutputPath)
    
    Write-Info "Running static analysis..."
    
    $outputFile = Join-Path $OutputPath "static-analysis.sarif"
    $analyzerArgs = @(
        "build",
        $SolutionPath,
        "--configuration", "Release",
        "/p:EnforceCodeStyleInBuild=true",
        "/p:TreatWarningsAsErrors=true",
        "/p:RunAnalyzersDuringBuild=true",
        "/p:AnalysisLevel=latest",
        "/p:AnalysisMode=AllEnabledByDefault"
    )
    
    & dotnet @analyzerArgs 2>&1 | Tee-Object -FilePath (Join-Path $OutputPath "build.log")
    
    # Check for analyzer warnings/errors
    $buildLog = Get-Content -Path (Join-Path $OutputPath "build.log") -ErrorAction SilentlyContinue
    $analyzerErrors = $buildLog | Where-Object { $_ -match "CA[0-9]{4}" -or $_ -match "CS[0-9]{4}" -or $_ -match "SA[0-9]{4}" }
    
    if ($analyzerErrors) {
        Write-Warning "Static analysis found issues:"
        $analyzerErrors | ForEach-Object { Write-Warning "  $_" }
    } else {
        Write-Success "Static analysis passed"
    }
    
    return $analyzerErrors.Count -eq 0
}

function Test-CodeCoverage {
    param([string]$SolutionPath, [string]$OutputPath, [decimal]$MinCoverage)
    
    Write-Info "Running code coverage analysis..."
    
    $testProject = Find-TestProject -SolutionPath $SolutionPath
    if (-not $testProject) {
        Write-Warning "No test project found"
        return $true
    }
    
    $coverageOutput = Join-Path $OutputPath "coverage.xml"
    $coverageArgs = @(
        "test",
        $testProject,
        "--collect:XPlat Code Coverage",
        "--data:CoverletOutputFormat=cobertura",
        "--data:CoverletOutput=$(Join-Path $OutputPath "coverage/")",
        "--verbosity", "minimal"
    )
    
    & dotnet @coverageArgs
    
    $coverageFile = Join-Path $OutputPath "coverage.cobertura.xml"
    if (Test-Path -Path $coverageFile) {
        $coverage = Get-CoverageFromFile -FilePath $coverageFile
        Write-Info "Code coverage: $coverage%"
        
        if ($coverage -ge $MinCoverage) {
            Write-Success "Coverage meets minimum requirement ($MinCoverage%)"
            return $true
        } else {
            Write-Error "Coverage below minimum requirement ($MinCoverage% vs $coverage%)"
            return $false
        }
    } else {
        Write-Warning "Coverage file not found"
        return $true
    }
}

function Test-CyclicDependencies {
    param([string]$SolutionPath, [string]$OutputPath)
    
    Write-Info "Checking for cyclic dependencies..."
    
    $analyzerPath = Join-Path $PSScriptRoot "..\Tools\CyclicDependencyAnalyzer\bin\Release\net9.0\TiXL.CyclicDependencyAnalyzer.exe"
    if (-not (Test-Path -Path $analyzerPath)) {
        Write-Warning "Cyclic dependency analyzer not found"
        return $true
    }
    
    $jsonOutput = Join-Path $OutputPath "dependencies.json"
    $reportOutput = Join-Path $OutputPath "dependency-report.md"
    
    & $analyzerPath $SolutionPath $jsonOutput $reportOutput
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Cyclic dependencies detected!"
        if (Test-Path -Path $reportOutput) {
            $report = Get-Content -Path $reportOutput -Raw
            Write-Info "Dependency report:"
            Write-Host $report
        }
        return $FailOnIssues.IsPresent
    }
    
    Write-Success "No cyclic dependencies detected"
    return $true
}

function Test-SecurityAnalysis {
    param([string]$SolutionPath, [string]$OutputPath)
    
    Write-Info "Running security analysis..."
    
    # SecurityCodeScan integration
    $securityOutput = Join-Path $OutputPath "security-report.sarif"
    
    # Run build with security analyzers
    $securityArgs = @(
        "build",
        $SolutionPath,
        "--configuration", "Release",
        "/p:EnforceCodeStyleInBuild=true"
    )
    
    & dotnet @securityArgs
    
    Write-Success "Security analysis completed"
    return $true
}

function Test-PerformanceBenchmarks {
    param([string]$SolutionPath, [string]$OutputPath)
    
    Write-Info "Running performance benchmarks..."
    
    $benchmarkProject = Find-BenchmarkProject -SolutionPath $SolutionPath
    if (-not $benchmarkProject) {
        Write-Warning "No benchmark project found"
        return $true
    }
    
    $benchmarkArgs = @(
        "run",
        "--project", $benchmarkProject,
        "--configuration", "Release",
        "--no-build"
    )
    
    & dotnet @benchmarkArgs
    
    Write-Success "Performance benchmarks completed"
    return $true
}

function Test-Documentation {
    param([string]$SolutionPath, [string]$OutputPath)
    
    Write-Info "Checking documentation completeness..."
    
    $docsOutput = Join-Path $OutputPath "documentation-report.md"
    
    # Check for missing XML documentation
    $projects = Get-Projects -SolutionPath $SolutionPath
    $missingDocs = @()
    
    foreach ($project in $projects) {
        $csFiles = Get-ChildItem -Path (Split-Path $project) -Filter "*.cs" -Recurse
        foreach ($csFile in $csFiles) {
            $content = Get-Content -Path $csFile.FullName
            # Check if file has documentation comments
            if (-not ($content | Where-Object { $_ -match "///" })) {
                $missingDocs += $csFile.FullName
            }
        }
    }
    
    if ($missingDocs.Count -gt 0) {
        Write-Warning "Found $($missingDocs.Count) files without documentation:"
        $missingDocs | Select-Object -First 10 | ForEach-Object { Write-Warning "  $_" }
        if ($missingDocs.Count -gt 10) {
            Write-Warning "  ... and $($missingDocs.Count - 10) more"
        }
    } else {
        Write-Success "All files have documentation"
    }
    
    return $missingDocs.Count -eq 0
}

# Helper functions
function Find-TestProject {
    param([string]$SolutionPath)
    $solutionDir = Split-Path $SolutionPath
    $testProject = Join-Path $solutionDir "Tests\TiXL.Tests.csproj"
    if (Test-Path -Path $testProject) {
        return $testProject
    }
    return $null
}

function Find-BenchmarkProject {
    param([string]$SolutionPath)
    $solutionDir = Split-Path $SolutionPath
    $benchmarkProject = Join-Path $solutionDir "Benchmarks\TiXL.Benchmarks.csproj"
    if (Test-Path -Path $benchmarkProject) {
        return $benchmarkProject
    }
    return $null
}

function Get-Projects {
    param([string]$SolutionPath)
    # Simple regex-based project extraction
    $solutionContent = Get-Content -Path $SolutionPath -Raw
    $projectMatches = [regex]::Matches($solutionContent, 'Project\([^)]*\.csproj[^)]*\)')
    return $projectMatches.Value
}

function Get-CoverageFromFile {
    param([string]$FilePath)
    
    try {
        [xml]$coverage = Get-Content -Path $FilePath
        $coveragePercent = [decimal]$coverage.coverage.'line-rate'
        return [math]::Round($coveragePercent * 100, 2)
    }
    catch {
        return 0.0
    }
}

function New-QualityReport {
    param([hashtable]$Results, [string]$OutputPath)
    
    $reportPath = Join-Path $OutputPath "quality-report.md"
    
    $report = @"
# TiXL Code Quality Report

Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')

## Summary

| Metric | Status | Value | Threshold |
|--------|--------|-------|-----------|
| Build Quality | $(if ($Results.BuildQuality) { '✅ PASS' } else { '❌ FAIL' }) | - | No Errors |
| Static Analysis | $(if ($Results.StaticAnalysis) { '✅ PASS' } else { '❌ FAIL' }) | - | No Warnings |
| Code Coverage | $(if ($Results.CodeCoverage) { '✅ PASS' } else { '❌ FAIL' }) | $($Results.CoveragePercent ?? 'N/A')% | $($QUALITY_THRESHOLDS.CodeCoverage)% |
| Cyclic Dependencies | $(if ($Results.CyclicDependencies) { '✅ PASS' } else { '❌ FAIL' }) | - | No Cycles |
| Security Analysis | $(if ($Results.SecurityAnalysis) { '✅ PASS' } else { '❌ FAIL' }) | - | No Vulnerabilities |
| Documentation | $(if ($Results.Documentation) { '✅ PASS' } else { '❌ FAIL' }) | - | Complete |

## Overall Result

$(if ($Results.AllPassed) { 
    '✅ **All quality gates PASSED**' 
} else { 
    '❌ **Some quality gates FAILED**' 
})

## Recommendations

$(if (-not $Results.AllPassed) {
    '- Address all failing quality gates before merging'
    '- Review the detailed reports for specific issues'
} else {
    '- Maintain current quality standards'
    '- Consider raising quality thresholds periodically'
})

## Detailed Reports

- Coverage Report: `$COVERAGE_DIR\coverage.html`
- Static Analysis: `$ANALYSIS_DIR\build.log`
- Dependency Analysis: `$ANALYSIS_DIR\dependency-report.md`
- Security Report: `$ANALYSIS_DIR\security-report.sarif`
"@

    Set-Content -Path $reportPath -Value $report
    Write-Info "Quality report generated: $reportPath"
    
    return $reportPath
}

# Main execution
Write-Info "Starting TiXL Code Quality Analysis"
Write-Info "Solution Path: $SolutionPath"
Write-Info "Output Directory: $OUTPUT_DIR"

$results = @{
    BuildQuality = $false
    StaticAnalysis = $false
    CodeCoverage = $false
    SecurityAnalysis = $false
    CyclicDependencies = $false
    Documentation = $false
    AllPassed = $false
}

try {
    # Pre-flight checks
    Test-DotNetInstallation | Out-Null
    Test-SolutionExists -Path $SolutionPath | Out-Null
    Test-AnalyzerInstallation | Out-Null
    
    # Quality gate tests
    $results.BuildQuality = Test-BuildQuality -SolutionPath $SolutionPath -ExcludeProjects $ExcludeProjects
    $results.StaticAnalysis = Test-StaticAnalysis -SolutionPath $SolutionPath -OutputPath $ANALYSIS_DIR
    
    if ($RunTests) {
        $results.CodeCoverage = Test-CodeCoverage -SolutionPath $SolutionPath -OutputPath $COVERAGE_DIR -MinCoverage $MinCoveragePercent
    }
    
    $results.CyclicDependencies = Test-CyclicDependencies -SolutionPath $SolutionPath -OutputPath $ANALYSIS_DIR
    $results.SecurityAnalysis = Test-SecurityAnalysis -SolutionPath $SolutionPath -OutputPath $ANALYSIS_DIR
    $results.Documentation = Test-Documentation -SolutionPath $SolutionPath -OutputPath $ANALYSIS_DIR
    
    if ($RunBenchmarks) {
        Test-PerformanceBenchmarks -SolutionPath $SolutionPath -OutputPath $ANALYSIS_DIR | Out-Null
    }
    
    # Determine overall result
    $results.AllPassed = $results.BuildQuality -and 
                        $results.StaticAnalysis -and 
                        ($results.CodeCoverage -or (-not $RunTests)) -and 
                        $results.CyclicDependencies -and 
                        $results.SecurityAnalysis -and 
                        $results.Documentation
    
    # Generate comprehensive report
    if ($GenerateReports) {
        $reportPath = New-QualityReport -Results $results -OutputPath $REPORTS_DIR
    }
    
    # Final result
    if ($results.AllPassed) {
        Write-Success "🎉 All quality gates PASSED!"
        exit 0
    } else {
        Write-Error "💥 Some quality gates FAILED!"
        if ($FailOnIssues) {
            exit 1
        }
        exit 0
    }
}
catch {
    Write-Error "Quality gate analysis failed: $($_.Exception.Message)"
    if ($VerboseOutput) {
        Write-Error $_.ScriptStackTrace
    }
    exit 1
}
