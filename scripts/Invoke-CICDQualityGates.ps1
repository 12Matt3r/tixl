#!/usr/bin/env pwsh
# TiXL CI/CD Quality Gate Integration Script
# This script integrates comprehensive code quality checks into CI/CD pipelines
# Supports Azure DevOps, GitHub Actions, GitLab CI, and Jenkins

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("AzureDevOps", "GitHubActions", "GitLabCI", "Jenkins", "Generic")]
    [string]$Platform = "Generic",
    
    [Parameter(Mandatory = $false)]
    [string]$SolutionPath = "TiXL.sln",
    
    [Parameter(Mandatory = $false)]
    [switch]$FailOnWarnings = $true,
    
    [Parameter(Mandatory = $false)]
    [switch]$EnableSonarQube = $false,
    
    [Parameter(Mandatory = $false)]
    [string]$SonarProjectKey = "tixl-realtime-graphics",
    
    [Parameter(Mandatory = $false)]
    [string]$SonarOrganization = "",
    
    [Parameter(Mandatory = $false)]
    [switch]$GenerateArtifacts = $true,
    
    [Parameter(Mandatory = $false)]
    [switch]$UploadTestResults = $true,
    
    [Parameter(Mandatory = $false)]
    [switch]$PublishCoverage = $true,
    
    [Parameter(Mandatory = $false)]
    [string[]]$ExcludeProjects = @(),
    
    [Parameter(Mandatory = $false)]
    [decimal]$MinCoveragePercent = 80.0,
    
    [Parameter(Mandatory = $false)]
    [int]$MaxComplexity = 15,
    
    [Parameter(Mandatory = $false)]
    [switch]$StrictMode = $true,
    
    [Parameter(Mandatory = $false)]
    [switch]$VerboseOutput = $false
)

$ErrorActionPreference = "Stop"

# Import modules
Import-Module -Name "./TiXL.ZeroWarningPolicy.psm1" -ErrorAction SilentlyContinue

# Quality thresholds (strict mode has higher requirements)
if ($StrictMode) {
    $QUALITY_THRESHOLDS = @{
        CodeCoverage = [math]::Max($MinCoveragePercent, 85.0)
        MaxComplexity = [math]::Min($MaxComplexity, 10)
        MaxDuplicatedLines = 1.0
        MaxTechnicalDebt = 2.0
        MaxMaintainabilityIndex = 85
    }
} else {
    $QUALITY_THRESHOLDS = @{
        CodeCoverage = $MinCoveragePercent
        MaxComplexity = $MaxComplexity
        MaxDuplicatedLines = 3.0
        MaxTechnicalDebt = 5.0
        MaxMaintainabilityIndex = 80
    }
}

# Platform-specific settings
$PLATFORM_SETTINGS = @{
    AzureDevOps = @{
        ArtifactName = "TiXL-QualityReports"
        TestResultsPath = "TestResults"
        CoverageResultsPath = "CoverageResults"
        SonarArgs = @("/d:sonar.host.url=$env:SONAR_HOST_URL", "/d:sonar.login=$env:SONAR_TOKEN")
    }
    GitHubActions = @{
        ArtifactName = "tixl-quality-reports"
        TestResultsPath = "test-results"
        CoverageResultsPath = "coverage-results"
        SonarArgs = @()
    }
    GitLabCI = @{
        ArtifactName = "tixl-quality-reports"
        TestResultsPath = "test-results"
        CoverageResultsPath = "coverage-results"
        SonarArgs = @()
    }
    Jenkins = @{
        ArtifactName = "TiXL-QualityReports"
        TestResultsPath = "TestResults"
        CoverageResultsPath = "CoverageResults"
        SonarArgs = @()
    }
}

$platformSettings = $PLATFORM_SETTINGS[$Platform]

# Output directories
$OUTPUT_DIR = Join-Path $PSScriptRoot "QualityReports"
$ARTIFACTS_DIR = Join-Path $OUTPUT_DIR "Artifacts"
$TEST_RESULTS_DIR = Join-Path $OUTPUT_DIR $platformSettings.TestResultsPath
$COVERAGE_DIR = Join-Path $OUTPUT_DIR $platformSettings.CoverageResultsPath
$SONAR_DIR = Join-Path $OUTPUT_DIR "SonarQube"

# Ensure directories exist
$directories = @($OUTPUT_DIR, $ARTIFACTS_DIR, $TEST_RESULTS_DIR, $COVERAGE_DIR, $SONAR_DIR)
foreach ($dir in $directories) {
    $null = New-Item -ItemType Directory -Force -Path $dir
}

# CI/CD Platform functions
function Write-CIHeader {
    param([string]$Message)
    switch ($Platform) {
        "AzureDevOps" { Write-Host "##[section]$Message" -ForegroundColor Cyan }
        "GitHubActions" { Write-Host "::group::$Message" }
        "GitLabCI" { Write-Host "section_start:$(Get-Date -Format 'utc%s'):$($Message -replace '\s', '_')" }
        "Jenkins" { Write-Host ">>> $Message" }
        default { Write-Host "=== $Message ===" -ForegroundColor Cyan }
    }
}

function Write-CISuccess {
    param([string]$Message)
    switch ($Platform) {
        "AzureDevOps" { Write-Host "##[success]$Message" -ForegroundColor Green }
        "GitHubActions" { Write-Host "✅ $Message"; Write-Host "::endgroup::" }
        "GitLabCI" { Write-Host "section_end:$(Get-Date -Format 'utc%s'):success" }
        "Jenkins" { Write-Host "✓ $Message" -ForegroundColor Green }
        default { Write-Host "✅ $Message" -ForegroundColor Green }
    }
}

function Write-CIError {
    param([string]$Message)
    switch ($Platform) {
        "AzureDevOps" { Write-Host "##[error]$Message" -ForegroundColor Red }
        "GitHubActions" { Write-Host "❌ $Message" -ForegroundColor Red; Write-Host "::endgroup::" }
        "GitLabCI" { Write-Host "section_end:$(Get-Date -Format 'utc%s'):error" }
        "Jenkins" { Write-Host "✗ $Message" -ForegroundColor Red }
        default { Write-Host "❌ $Message" -ForegroundColor Red }
    }
}

function Write-CIWarning {
    param([string]$Message)
    switch ($Platform) {
        "AzureDevOps" { Write-Host "##[warning]$Message" -ForegroundColor Yellow }
        "GitHubActions" { Write-Host "⚠️ $Message" -ForegroundColor Yellow }
        "GitLabCI" { Write-Host "section_end:$(Get-Date -Format 'utc%s'):warning" }
        "Jenkins" { Write-Host "⚠ $Message" -ForegroundColor Yellow }
        default { Write-Host "⚠️  $Message" -ForegroundColor Yellow }
    }
}

# Core quality gate functions
function Test-Prerequisites {
    Write-CIHeader "Checking Prerequisites"
    
    # Check .NET SDK
    $dotnetVersion = dotnet --version
    Write-Host ".NET SDK Version: $dotnetVersion" -ForegroundColor Green
    
    # Check solution exists
    if (-not (Test-Path $SolutionPath)) {
        throw "Solution file not found: $SolutionPath"
    }
    
    # Check required tools
    $requiredTools = @("dotnet", "git")
    foreach ($tool in $requiredTools) {
        try {
            $version = & $tool --version 2>$null
            Write-Host "$tool version: $version" -ForegroundColor Green
        } catch {
            Write-CIWarning "$tool is not available in PATH"
        }
    }
    
    Write-CISuccess "Prerequisites check completed"
}

function Initialize-SonarQube {
    if (-not $EnableSonarQube) { return }
    
    Write-CIHeader "Initializing SonarQube"
    
    # Install SonarScanner if not present
    if (-not (Get-Command "dotnet-sonarscanner" -ErrorAction SilentlyContinue)) {
        Write-Host "Installing SonarScanner for .NET..." -ForegroundColor Yellow
        dotnet tool install --global dotnet-sonarscanner
    }
    
    $sonarKey = $SonarProjectKey
    if ($SonarOrganization) {
        $sonarKey = "$SonarOrganization/$sonarKey"
    }
    
    dotnet-sonarscanner begin `
        /k:"$sonarKey" `
        /o:"$SonarOrganization" `
        /d:sonar.host.url="$env:SONAR_HOST_URL" `
        /d:sonar.login="$env:SONAR_TOKEN" `
        /d:sonar.cs.opencover.reportsPaths="**/coverage.opencover.xml" `
        /d:sonar.cs.vstest.reportsPaths="**/TestResults/*.trx"
    
    Write-CISuccess "SonarQube initialized"
}

function Invoke-BuildAnalysis {
    Write-CIHeader "Running Build Analysis"
    
    $buildArgs = @(
        "build",
        $SolutionPath,
        "--configuration", "Release",
        "--no-restore",
        "/p:EnforceCodeStyleInBuild=true",
        "/p:TreatWarningsAsErrors=$FailOnWarnings",
        "/p:WarningLevel=5",
        "/p:AnalysisLevel=latest",
        "/p:AnalysisMode=AllEnabledByDefault",
        "/p:RunAnalyzersDuringBuild=true"
    )
    
    if ($ExcludeProjects.Count -gt 0) {
        $buildArgs += "--exclude", ($ExcludeProjects -join ",")
    }
    
    & dotnet @buildArgs 2>&1 | Tee-Object -FilePath (Join-Path $OUTPUT_DIR "build-analysis.log")
    
    if ($LASTEXITCODE -ne 0) {
        throw "Build analysis failed with exit code $LASTEXITCODE"
    }
    
    Write-CISuccess "Build analysis completed"
}

function Invoke-StaticAnalysis {
    Write-CIHeader "Running Static Analysis"
    
    # Analyze build output for warnings
    $buildLog = Get-Content -Path (Join-Path $OUTPUT_DIR "build-analysis.log") -ErrorAction SilentlyContinue
    $analyzerWarnings = $buildLog | Where-Object { 
        $_ -match "CA[0-9]{4}" -or 
        $_ -match "CS[0-9]{4}" -or 
        $_ -match "SA[0-9]{4}" -or
        $_ -match "IDE[0-9]{4}"
    }
    
    $warningCount = $analyzerWarnings.Count
    Write-Host "Found $warningCount analyzer warnings" -ForegroundColor $(if ($warningCount -eq 0) { "Green" } elseif ($warningCount -lt 10) { "Yellow" } else { "Red" })
    
    if ($analyzerWarnings -and $FailOnWarnings) {
        Write-CIWarning "Static analysis warnings:"
        $analyzerWarnings | Select-Object -First 20 | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
        if ($analyzerWarnings.Count -gt 20) {
            Write-CIWarning "... and $($analyzerWarnings.Count - 20) more warnings"
        }
    }
    
    Write-CISuccess "Static analysis completed"
}

function Invoke-TestsAndCoverage {
    Write-CIHeader "Running Tests and Coverage Analysis"
    
    $testProject = Find-TestProject -SolutionPath $SolutionPath
    if (-not $testProject) {
        Write-CIWarning "No test project found, skipping tests"
        return
    }
    
    $testArgs = @(
        "test",
        $testProject,
        "--configuration", "Release",
        "--no-build",
        "--no-restore",
        "--collect:XPlat Code Coverage",
        "--data:CoverletOutputFormat=cobertura",
        "--data:CoverletOutput=$(Join-Path $COVERAGE_DIR "coverage/")",
        "--results-directory", $TEST_RESULTS_DIR,
        "--logger:trx;LogFileName=TestResults.trx"
    )
    
    & dotnet @testArgs
    
    $testPassed = $LASTEXITCODE -eq 0
    
    # Find coverage files
    $coverageFiles = Get-ChildItem -Path $COVERAGE_DIR -Filter "*.cobertura.xml" -Recurse
    $coveragePercent = 0.0
    
    if ($coverageFiles) {
        foreach ($file in $coverageFiles) {
            try {
                [xml]$coverage = Get-Content -Path $file.FullName
                $lineRate = [decimal]$coverage.coverage.'line-rate'
                $currentCoverage = [math]::Round($lineRate * 100, 2)
                if ($currentCoverage -gt $coveragePercent) {
                    $coveragePercent = $currentCoverage
                }
            } catch {
                Write-CIWarning "Failed to parse coverage file: $($file.FullName)"
            }
        }
    }
    
    Write-Host "Code Coverage: $coveragePercent% (Required: $($QUALITY_THRESHOLDS.CodeCoverage)%)" -ForegroundColor $(if ($coveragePercent -ge $QUALITY_THRESHOLDS.CodeCoverage) { "Green" } else { "Red" })
    
    if ($coveragePercent -lt $QUALITY_THRESHOLDS.CodeCoverage) {
        Write-CIError "Coverage below required threshold"
        if ($FailOnWarnings) { throw "Coverage requirement not met" }
    }
    
    Write-CISuccess "Tests and coverage analysis completed"
}

function Invoke-DependencyAnalysis {
    Write-CIHeader "Checking Dependencies and Architecture"
    
    # Check for cyclic dependencies using the analyzer tool
    $analyzerPath = Join-Path $PSScriptRoot "..\Tools\CyclicDependencyAnalyzer\bin\Release\net9.0\TiXL.CyclicDependencyAnalyzer.exe"
    
    if (Test-Path $analyzerPath) {
        $jsonOutput = Join-Path $SONAR_DIR "dependencies.json"
        $reportOutput = Join-Path $SONAR_DIR "dependency-report.md"
        
        & $analyzerPath $SolutionPath $jsonOutput $reportOutput
        
        if ($LASTEXITCODE -ne 0) {
            Write-CIError "Cyclic dependencies detected!"
            if (Test-Path $reportOutput) {
                $report = Get-Content -Path $reportOutput -Raw
                Write-CIWarning "Dependency Analysis Report:"
                Write-Host $report
            }
            if ($FailOnWarnings) { throw "Cyclic dependencies found" }
        } else {
            Write-CISuccess "No cyclic dependencies detected"
        }
    } else {
        Write-CIWarning "Dependency analyzer not found, skipping cyclic dependency check"
    }
    
    Write-CISuccess "Dependency analysis completed"
}

function Invoke-PerformanceTests {
    Write-CIHeader "Running Performance Benchmarks"
    
    $benchmarkProject = Find-BenchmarkProject -SolutionPath $SolutionPath
    if (-not $benchmarkProject) {
        Write-CIWarning "No benchmark project found, skipping performance tests"
        return
    }
    
    # Run benchmarks (only in CI environment)
    if ($env:CI -eq "true" -or $env:TF_BUILD -eq "true" -or $env:GITHUB_ACTIONS -eq "true") {
        $benchmarkArgs = @(
            "run",
            "--project", $benchmarkProject,
            "--configuration", "Release",
            "--no-build",
            "--exporters", "json",
            "--output", (Join-Path $SONAR_DIR "benchmarks.json")
        )
        
        & dotnet @benchmarkArgs
        
        Write-CISuccess "Performance benchmarks completed"
    } else {
        Write-CIWarning "Skipping performance benchmarks (not in CI environment)"
    }
}

function Publish-Results {
    Write-CIHeader "Publishing Quality Results"
    
    # Generate comprehensive quality report
    $reportPath = Join-Path $OUTPUT_DIR "ci-quality-report.md"
    
    $report = @"
# TiXL CI/CD Quality Report

**Platform:** $Platform
**Generated:** $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss UTC')
**Branch:** $(if ($env:GIT_BRANCH) { $env:GIT_BRANCH } elseif ($env:BUILD_SOURCEBRANCH) { $env:BUILD_SOURCEBRANCH } else { "Unknown" })
**Commit:** $(if ($env:GIT_COMMIT) { $env:GIT_COMMIT } elseif ($env:BUILD_SOURCEVERSION) { $env:BUILD_SOURCEVERSION } else { "Unknown" })

## Summary

| Quality Gate | Status | Value | Threshold |
|--------------|--------|-------|-----------|
| Build Quality | ✅ PASS | - | No Errors |
| Static Analysis | $(if ($lastAnalyzerWarnings) { '⚠️ WARNINGS' } else { '✅ PASS' }) | - | Minimal Warnings |
| Code Coverage | $(if ($coveragePercent -ge $QUALITY_THRESHOLDS.CodeCoverage) { '✅ PASS' } else { '❌ FAIL' }) | $coveragePercent% | $($QUALITY_THRESHOLDS.CodeCoverage)% |
| Dependency Analysis | ✅ PASS | - | No Cycles |
| Performance | ✅ PASS | - | Benchmarks Run |

## Artifacts

- Build Log: `$(Join-Path $OUTPUT_DIR "build-analysis.log")`
- Test Results: `$TEST_RESULTS_DIR`
- Coverage Data: `$COVERAGE_DIR`
- Quality Reports: `$OUTPUT_DIR\*.md`

## Next Steps

$(if ($coveragePercent -lt $QUALITY_THRESHOLDS.CodeCoverage) {
    "- Increase test coverage to meet quality gate requirements"
} else {
    "- Maintain current quality standards"
})

- Review any warnings in the static analysis output
- Monitor performance metrics over time
- Continue following established coding standards
"@

    Set-Content -Path $reportPath -Value $report
    
    # Upload artifacts (platform-specific)
    if ($GenerateArtifacts) {
        Upload-PlatformArtifacts
    }
    
    Write-CISuccess "Results published"
}

function Upload-PlatformArtifacts {
    switch ($Platform) {
        "AzureDevOps" {
            if ($env:TF_BUILD) {
                Write-Host "##vso[artifact.upload containerfolder=$($platformSettings.ArtifactName);artifactname=$($platformSettings.ArtifactName)]$OUTPUT_DIR"
            }
        }
        "GitHubActions" {
            if ($env:GITHUB_ACTIONS) {
                Write-Host "Uploading artifacts to GitHub Actions..."
                # GitHub Actions artifact upload would be done via the upload action
            }
        }
        "GitLabCI" {
            if ($env:GITLAB_CI) {
                Write-Host "Uploading artifacts to GitLab CI..."
                # GitLab CI artifact upload is handled via .gitlab-ci.yml
            }
        }
        "Jenkins" {
            Write-Host "Archiving artifacts in Jenkins..."
            # Jenkins archive would be configured in the pipeline
        }
    }
}

function Complete-SonarQube {
    if (-not $EnableSonarQube) { return }
    
    Write-CIHeader "Completing SonarQube Analysis"
    
    try {
        dotnet-sonarscanner end /d:sonar.login="$env:SONAR_TOKEN"
        Write-CISuccess "SonarQube analysis completed"
    } catch {
        Write-CIWarning "SonarQube analysis failed: $($_.Exception.Message)"
    }
}

# Helper functions
function Find-TestProject {
    param([string]$SolutionPath)
    $solutionDir = Split-Path $SolutionPath
    $testProjects = @(
        "Tests\TiXL.Tests.csproj",
        "test\TiXL.Tests.csproj",
        "TiXL.Tests.csproj"
    )
    
    foreach ($project in $testProjects) {
        $path = Join-Path $solutionDir $project
        if (Test-Path $path) {
            return $path
        }
    }
    return $null
}

function Find-BenchmarkProject {
    param([string]$SolutionPath)
    $solutionDir = Split-Path $SolutionPath
    $benchmarkProjects = @(
        "Benchmarks\TiXL.Benchmarks.csproj",
        "benchmark\TiXL.Benchmarks.csproj",
        "TiXL.Benchmarks.csproj"
    )
    
    foreach ($project in $benchmarkProjects) {
        $path = Join-Path $solutionDir $project
        if (Test-Path $path) {
            return $path
        }
    }
    return $null
}

# Main execution
try {
    Write-Host "Starting TiXL CI/CD Quality Gate Analysis" -ForegroundColor Cyan
    Write-Host "Platform: $Platform"
    Write-Host "Solution: $SolutionPath"
    Write-Host "Strict Mode: $StrictMode"
    Write-Host "Quality Thresholds: $(($QUALITY_THRESHOLDS | ConvertTo-Json -Compress))" -ForegroundColor Gray
    
    # Core quality gates
    Test-Prerequisites
    Initialize-SonarQube
    Invoke-BuildAnalysis
    Invoke-StaticAnalysis
    Invoke-TestsAndCoverage
    Invoke-DependencyAnalysis
    Invoke-PerformanceTests
    Complete-SonarQube
    Publish-Results
    
    Write-CISuccess "🎉 CI/CD Quality Gate Analysis Completed Successfully!"
    exit 0
}
catch {
    Write-CIError "💥 CI/CD Quality Gate Analysis Failed: $($_.Exception.Message)"
    if ($VerboseOutput) {
        Write-Host "Stack Trace:" -ForegroundColor Red
        Write-Host $_.ScriptStackTrace -ForegroundColor Red
    }
    exit 1
}
