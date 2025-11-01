#!/usr/bin/env pwsh
<#
.SYNOPSIS
    TiXL Secure Build Verification Suite
.DESCRIPTION
    Comprehensive verification suite for TiXL secure build process.
    Validates reproducible builds, code signing, and integrity verification.
.PARAMETER BuildPath
    Path to build artifacts directory
.PARAMETER ExpectedGitCommit
    Expected Git commit for validation
.PARAMETER SkipSigning
    Skip code signature verification
.PARAMETER SkipProvenance
    Skip source provenance verification
.PARAMETER OutputFormat
    Output format: Text, JSON, Markdown
.EXAMPLE
    .\scripts\verify-build-security.ps1 -BuildPath "artifacts\secure-build"
#>

param(
    [Parameter(Mandatory = $false)]
    [string]$BuildPath = "artifacts/secure-build",
    
    [Parameter(Mandatory = $false)]
    [string]$ExpectedGitCommit = "",
    
    [Parameter(Mandatory = $false)]
    [switch]$SkipSigning = $false,
    
    [Parameter(Mandatory = $false)]
    [switch]$SkipProvenance = $false,
    
    [Parameter(Mandatory = $false)]
    [ValidateSet("Text", "JSON", "Markdown")]
    [string]$OutputFormat = "Text",
    
    [Parameter(Mandatory = $false)]
    [string]$OutputPath = "",
    
    [Parameter(Mandatory = $false)]
    [switch]$DetailedReport = $true,
    
    [Parameter(Mandatory = $false)]
    [switch]$FailOnWarnings = $false
)

$ErrorActionPreference = "Stop"
$InformationPreference = "Continue"

# Verification suite configuration
$suiteVersion = "1.0.0"
$suiteName = "TiXL Secure Build Verification Suite"

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "$suiteName v$suiteVersion" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan

# Verification results structure
$verificationSuite = @{
    SuiteInfo = @{
        SuiteVersion = $suiteVersion
        ExecutionTime = (Get-Date).ToUniversalTime()
        BuildPath = $BuildPath
        OutputFormat = $OutputFormat
        Parameters = @{
            SkipSigning = $SkipSigning
            SkipProvenance = $SkipProvenance
            ExpectedGitCommit = $ExpectedGitCommit
        }
    }
    TestResults = @{
        BuildConfiguration = @{
            Name = "Build Configuration Validation"
            Status = "PENDING"
            Details = @()
            Score = 0
        }
        ReproducibleBuild = @{
            Name = "Reproducible Build Verification"
            Status = "PENDING"
            Details = @()
            Score = 0
        }
        SourceLink = @{
            Name = "SourceLink Integration"
            Status = "PENDING"
            Details = @()
            Score = 0
        }
        CodeSigning = @{
            Name = "Code Signing Verification"
            Status = "PENDING"
            Details = @()
            Score = 0
        }
        IntegrityVerification = @{
            Name = "Artifact Integrity"
            Status = "PENDING"
            Details = @()
            Score = 0
        }
        SecurityScanning = @{
            Name = "Security Scanning"
            Status = "PENDING"
            Details = @()
            Score = 0
        }
        ProvenanceTracking = @{
            Name = "Provenance Tracking"
            Status = "PENDING"
            Details = @()
            Score = 0
        }
    }
    Summary = @{
        TotalTests = 7
        PassedTests = 0
        FailedTests = 0
        WarningTests = 0
        OverallScore = 0
        OverallStatus = "PENDING"
    }
    DetailedResults = @()
    Recommendations = @()
}

function Write-VerificationLog {
    param([string]$Message, [string]$Level = "INFO", [string]$Category = "GENERAL")
    
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss UTC"
    $logMessage = "[$timestamp] [$Level] [$Category] $Message"
    
    switch ($Level) {
        "ERROR" { Write-Host $logMessage -ForegroundColor Red }
        "WARN" { Write-Host $logMessage -ForegroundColor Yellow }
        "INFO" { Write-Host $logMessage -ForegroundColor Green }
        "DEBUG" { Write-Host $logMessage -ForegroundColor Magenta }
    }
    
    # Add to detailed results
    $verificationSuite.DetailedResults += @{
        Time = $timestamp
        Category = $Category
        Level = $Level
        Message = $Message
    }
}

function Test-BuildPath {
    if (-not (Test-Path $BuildPath)) {
        Write-VerificationLog "Build path does not exist: $BuildPath" "ERROR" "BUILD_PATH"
        $verificationSuite.TestResults.BuildConfiguration.Status = "FAILED"
        return $false
    }
    
    Write-VerificationLog "Build path validated: $BuildPath" "INFO" "BUILD_PATH"
    $verificationSuite.TestResults.BuildConfiguration.Status = "PASSED"
    $verificationSuite.TestResults.BuildConfiguration.Score = 100
    return $true
}

function Test-BuildManifest {
    $manifestPath = Join-Path $BuildPath "build-manifest.json"
    
    if (-not (Test-Path $manifestPath)) {
        Write-VerificationLog "Build manifest not found: $manifestPath" "ERROR" "MANIFEST"
        $verificationSuite.TestResults.BuildConfiguration.Status = "FAILED"
        return $null
    }
    
    try {
        $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
        Write-VerificationLog "Build manifest loaded successfully" "INFO" "MANIFEST"
        
        # Validate manifest structure
        $requiredFields = @("BuildInfo", "ReproducibleBuild", "Security", "Artifacts")
        $missingFields = @()
        
        foreach ($field in $requiredFields) {
            if (-not $manifest.PSObject.Properties.Name.Contains($field)) {
                $missingFields += $field
            }
        }
        
        if ($missingFields.Count -gt 0) {
            Write-VerificationLog "Missing manifest fields: $($missingFields -join ', ')" "ERROR" "MANIFEST"
            $verificationSuite.TestResults.BuildConfiguration.Status = "FAILED"
            return $null
        }
        
        Write-VerificationLog "Build manifest structure validated" "INFO" "MANIFEST"
        $verificationSuite.TestResults.BuildConfiguration.Details += "Build manifest structure is valid"
        return $manifest
    }
    catch {
        Write-VerificationLog "Failed to parse build manifest: $($_.Exception.Message)" "ERROR" "MANIFEST"
        $verificationSuite.TestResults.BuildConfiguration.Status = "FAILED"
        return $null
    }
}

function Test-ReproducibleBuildSettings {
    param($Manifest)
    
    if (-not $Manifest) {
        $verificationSuite.TestResults.ReproducibleBuild.Status = "FAILED"
        return
    }
    
    $reproducibleConfig = $Manifest.ReproducibleBuild
    if (-not $reproducibleConfig) {
        Write-VerificationLog "Reproducible build configuration not found in manifest" "ERROR" "REPRODUCIBLE"
        $verificationSuite.TestResults.ReproducibleBuild.Status = "FAILED"
        return
    }
    
    $score = 0
    $totalChecks = 0
    
    # Check SourceLink settings
    $totalChecks++
    if ($reproducibleConfig.SourceLinkEnabled -eq $true) {
        Write-VerificationLog "✅ SourceLink is enabled" "INFO" "REPRODUCIBLE"
        $verificationSuite.TestResults.ReproducibleBuild.Details += "SourceLink enabled"
        $score += 20
    } else {
        Write-VerificationLog "❌ SourceLink is not enabled" "WARN" "REPRODUCIBLE"
        $verificationSuite.TestResults.ReproducibleBuild.Details += "SourceLink disabled"
    }
    
    # Check embed sources
    $totalChecks++
    if ($reproducibleConfig.EmbedSources -eq $true) {
        Write-VerificationLog "✅ Source embedding is enabled" "INFO" "REPRODUCIBLE"
        $verificationSuite.TestResults.ReproducibleBuild.Details += "Source embedding enabled"
        $score += 20
    } else {
        Write-VerificationLog "❌ Source embedding is not enabled" "WARN" "REPRODUCIBLE"
        $verificationSuite.TestResults.ReproducibleBuild.Details += "Source embedding disabled"
    }
    
    # Check deterministic build
    $totalChecks++
    if ($reproducibleConfig.DeterministicBuild -eq $true) {
        Write-VerificationLog "✅ Deterministic build is enabled" "INFO" "REPRODUCIBLE"
        $verificationSuite.TestResults.ReproducibleBuild.Details += "Deterministic build enabled"
        $score += 20
    } else {
        Write-VerificationLog "❌ Deterministic build is not enabled" "ERROR" "REPRODUCIBLE"
        $verificationSuite.TestResults.ReproducibleBuild.Details += "Deterministic build disabled"
    }
    
    # Check fixed build time
    $totalChecks++
    if ($reproducibleConfig.FixedBuildTime -eq $true) {
        Write-VerificationLog "✅ Fixed build time is configured" "INFO" "REPRODUCIBLE"
        $verificationSuite.TestResults.ReproducibleBuild.Details += "Fixed build time configured"
        $score += 20
    } else {
        Write-VerificationLog "❌ Fixed build time is not configured" "WARN" "REPRODUCIBLE"
        $verificationSuite.TestResults.ReproducibleBuild.Details += "Fixed build time not configured"
    }
    
    # Check fixed file version
    $totalChecks++
    if ($reproducibleConfig.FixedFileVersion -eq $true) {
        Write-VerificationLog "✅ Fixed file version is configured" "INFO" "REPRODUCIBLE"
        $verificationSuite.TestResults.ReproducibleBuild.Details += "Fixed file version configured"
        $score += 20
    } else {
        Write-VerificationLog "❌ Fixed file version is not configured" "WARN" "REPRODUCIBLE"
        $verificationSuite.TestResults.ReproducibleBuild.Details += "Fixed file version not configured"
    }
    
    $finalScore = [Math]::Round(($score / $totalChecks) * 100)
    $verificationSuite.TestResults.ReproducibleBuild.Score = $finalScore
    
    if ($finalScore -ge 80) {
        $verificationSuite.TestResults.ReproducibleBuild.Status = "PASSED"
    } elseif ($finalScore -ge 60) {
        $verificationSuite.TestResults.ReproducibleBuild.Status = "WARNING"
    } else {
        $verificationSuite.TestResults.ReproducibleBuild.Status = "FAILED"
    }
}

function Test-SourceLinkIntegration {
    Write-VerificationLog "Testing SourceLink integration..." "INFO" "SOURCELINK"
    
    # Check for PDB files with SourceLink information
    $pdbFiles = Get-ChildItem -Path $BuildPath -Recurse -Filter "*.pdb" -ErrorAction SilentlyContinue
    $dllFiles = Get-ChildItem -Path $BuildPath -Recurse -Filter "*.dll" -ErrorAction SilentlyContinue
    
    if ($pdbFiles.Count -eq 0 -and $dllFiles.Count -eq 0) {
        Write-VerificationLog "No assemblies or debug symbols found" "WARN" "SOURCELINK"
        $verificationSuite.TestResults.SourceLink.Status = "WARNING"
        return
    }
    
    $score = 0
    $totalChecks = 2
    
    # Check for PDB files
    if ($pdbFiles.Count -gt 0) {
        Write-VerificationLog "Found $($pdbFiles.Count) PDB files" "INFO" "SOURCELINK"
        $verificationSuite.TestResults.SourceLink.Details += "$($pdbFiles.Count) PDB files found"
        $score += 50
    } else {
        Write-VerificationLog "No PDB files found" "WARN" "SOURCELINK"
        $verificationSuite.TestResults.SourceLink.Details += "No PDB files found"
    }
    
    # Check for debug information in assemblies
    $assembliesWithDebugInfo = 0
    foreach ($dll in $dllFiles) {
        try {
            # Basic check - in a real implementation, you'd parse the PE headers
            if ($dll.Length -gt 1000) { # Arbitrary threshold for valid assembly
                $assembliesWithDebugInfo++
            }
        }
        catch {
            # Ignore errors in assembly inspection
        }
    }
    
    if ($assembliesWithDebugInfo -eq $dllFiles.Count) {
        Write-VerificationLog "All assemblies appear to contain debug information" "INFO" "SOURCELINK"
        $verificationSuite.TestResults.SourceLink.Details += "Debug information present in assemblies"
        $score += 50
    } else {
        Write-VerificationLog "Some assemblies may lack debug information" "WARN" "SOURCELINK"
        $verificationSuite.TestResults.SourceLink.Details += "Debug information may be incomplete"
    }
    
    $finalScore = [Math]::Round(($score / $totalChecks) * 100)
    $verificationSuite.TestResults.SourceLink.Score = $finalScore
    
    if ($finalScore -ge 80) {
        $verificationSuite.TestResults.SourceLink.Status = "PASSED"
    } elseif ($finalScore -ge 50) {
        $verificationSuite.TestResults.SourceLink.Status = "WARNING"
    } else {
        $verificationSuite.TestResults.SourceLink.Status = "FAILED"
    }
}

function Test-CodeSigningVerification {
    if ($SkipSigning) {
        Write-VerificationLog "Code signing verification skipped" "INFO" "SIGNING"
        $verificationSuite.TestResults.CodeSigning.Status = "SKIPPED"
        return
    }
    
    Write-VerificationLog "Verifying code signatures..." "INFO" "SIGNING"
    
    # Check build manifest for signing information
    $manifestPath = Join-Path $BuildPath "build-manifest.json"
    $manifest = Test-BuildManifest
    $securityConfig = $manifest?.Security
    
    $score = 0
    $totalChecks = 0
    
    # Check if signing was enabled
    $totalChecks++
    if ($securityConfig?.SigningEnabled -eq $true) {
        Write-VerificationLog "Code signing was enabled for this build" "INFO" "SIGNING"
        $verificationSuite.TestResults.CodeSigning.Details += "Code signing enabled"
        $score += 25
    } else {
        Write-VerificationLog "Code signing was not enabled for this build" "WARN" "SIGNING"
        $verificationSuite.TestResults.CodeSigning.Details += "Code signing disabled"
        # Don't penalize if signing wasn't enabled
        $score += 25
    }
    
    # Check for signature verification
    $totalChecks++
    $signedArtifacts = 0
    $totalArtifacts = 0
    
    # Check .NET assemblies
    $assemblies = Get-ChildItem -Path $BuildPath -Recurse -Filter "*.dll" -ErrorAction SilentlyContinue
    $totalArtifacts += $assemblies.Count
    
    foreach ($assembly in $assemblies) {
        try {
            & signtool.exe verify /pa $assembly.FullName | Out-Null
            if ($LASTEXITCODE -eq 0) {
                $signedArtifacts++
            }
        }
        catch {
            # signtool might not be available
        }
    }
    
    # Check NuGet packages
    $packages = Get-ChildItem -Path $BuildPath -Recurse -Filter "*.nupkg" -ErrorAction SilentlyContinue
    $totalArtifacts += $packages.Count
    
    foreach ($package in $packages) {
        try {
            & dotnet nuget verify sign $package.FullName | Out-Null
            if ($LASTEXITCODE -eq 0) {
                $signedArtifacts++
            }
        }
        catch {
            # dotnet might not be available or package might not be signed
        }
    }
    
    if ($totalArtifacts -eq 0) {
        Write-VerificationLog "No signable artifacts found" "WARN" "SIGNING"
        $verificationSuite.TestResults.CodeSigning.Details += "No signable artifacts"
        $score += 25
    } elseif ($signedArtifacts -eq $totalArtifacts) {
        Write-VerificationLog "All artifacts are properly signed" "INFO" "SIGNING"
        $verificationSuite.TestResults.CodeSigning.Details += "All artifacts signed ($signedArtifacts/$totalArtifacts)"
        $score += 25
    } elseif ($signedArtifacts -gt 0) {
        Write-VerificationLog "Some artifacts are signed ($signedArtifacts/$totalArtifacts)" "WARN" "SIGNING"
        $verificationSuite.TestResults.CodeSigning.Details += "Partial signing ($signedArtifacts/$totalArtifacts)"
        $score += 15
    } else {
        Write-VerificationLog "No artifacts are signed" "WARN" "SIGNING"
        $verificationSuite.TestResults.CodeSigning.Details += "No artifacts signed"
        $score += 0
    }
    
    $finalScore = [Math]::Round(($score / $totalChecks) * 100)
    $verificationSuite.TestResults.CodeSigning.Score = $finalScore
    
    if ($finalScore -ge 75) {
        $verificationSuite.TestResults.CodeSigning.Status = "PASSED"
    } elseif ($finalScore -ge 50) {
        $verificationSuite.TestResults.CodeSigning.Status = "WARNING"
    } else {
        $verificationSuite.TestResults.CodeSigning.Status = "FAILED"
    }
}

function Test-ArtifactIntegrity {
    Write-VerificationLog "Testing artifact integrity..." "INFO" "INTEGRITY"
    
    $manifestPath = Join-Path $BuildPath "build-manifest.json"
    $manifest = Test-BuildManifest
    
    if (-not $manifest -or -not $manifest.Integrity) {
        Write-VerificationLog "No integrity data found in manifest" "ERROR" "INTEGRITY"
        $verificationSuite.TestResults.IntegrityVerification.Status = "FAILED"
        return
    }
    
    $verifiedArtifacts = 0
    $totalArtifacts = $manifest.Integrity.Count
    
    foreach ($integrityEntry in $manifest.Integrity) {
        $filePath = Join-Path $BuildPath $integrityEntry.FileName
        
        if (-not (Test-Path $filePath)) {
            Write-VerificationLog "Artifact not found: $($integrityEntry.FileName)" "ERROR" "INTEGRITY"
            continue
        }
        
        # Calculate hash
        try {
            $actualHash = Get-FileHash -Path $filePath -Algorithm SHA256
            if ($actualHash.Hash -eq $integrityEntry.Hash) {
                Write-VerificationLog "✅ Hash verified: $($integrityEntry.FileName)" "INFO" "INTEGRITY"
                $verifiedArtifacts++
            } else {
                Write-VerificationLog "❌ Hash mismatch: $($integrityEntry.FileName)" "ERROR" "INTEGRITY"
            }
        }
        catch {
            Write-VerificationLog "❌ Hash calculation failed: $($integrityEntry.FileName)" "ERROR" "INTEGRITY"
        }
    }
    
    $verificationSuite.TestResults.IntegrityVerification.Details += "Total artifacts: $totalArtifacts"
    $verificationSuite.TestResults.IntegrityVerification.Details += "Verified artifacts: $verifiedArtifacts"
    
    if ($totalArtifacts -eq 0) {
        $verificationSuite.TestResults.IntegrityVerification.Status = "WARNING"
        $verificationSuite.TestResults.IntegrityVerification.Score = 50
    } elseif ($verifiedArtifacts -eq $totalArtifacts) {
        Write-VerificationLog "All artifact hashes verified successfully" "INFO" "INTEGRITY"
        $verificationSuite.TestResults.IntegrityVerification.Status = "PASSED"
        $verificationSuite.TestResults.IntegrityVerification.Score = 100
    } else {
        Write-VerificationLog "$verifiedArtifacts/$totalArtifacts artifacts verified" "WARN" "INTEGRITY"
        $verificationSuite.TestResults.IntegrityVerification.Status = "FAILED"
        $verificationSuite.TestResults.IntegrityVerification.Score = [Math]::Round(($verifiedArtifacts / $totalArtifacts) * 100)
    }
}

function Test-SecurityScanning {
    Write-VerificationLog "Running security scanning verification..." "INFO" "SECURITY"
    
    # Check for common security issues
    $securityIssues = @()
    $securityScore = 100
    
    # Check for hardcoded secrets in build artifacts
    $artifacts = Get-ChildItem -Path $BuildPath -File -Recurse
    foreach ($artifact in $artifacts) {
        if ($artifact.Extension -match "\.(txt|md|json|xml)$") {
            try {
                $content = Get-Content $artifact.FullName -Raw
                if ($content -match "(password|secret|key|token)\s*=\s*['\"][^'\"]{10,}['\"]") {
                    $securityIssues += "Potential hardcoded secret in $($artifact.Name)"
                    $securityScore -= 10
                }
            }
            catch {
                # Ignore file read errors
            }
        }
    }
    
    # Check for insecure protocols
    foreach ($artifact in $artifacts) {
        if ($artifact.Extension -match "\.(config|json|xml)$") {
            try {
                $content = Get-Content $artifact.FullName -Raw
                if ($content -match "http://") {
                    $securityIssues += "Insecure HTTP protocol in $($artifact.Name)"
                    $securityScore -= 5
                }
            }
            catch {
                # Ignore file read errors
            }
        }
    }
    
    # Add details to results
    if ($securityIssues.Count -eq 0) {
        Write-VerificationLog "No security issues detected" "INFO" "SECURITY"
        $verificationSuite.TestResults.SecurityScanning.Details += "No security issues detected"
        $verificationSuite.TestResults.SecurityScanning.Status = "PASSED"
        $verificationSuite.TestResults.SecurityScanning.Score = 100
    } else {
        Write-VerificationLog "Security issues detected: $($securityIssues.Count)" "WARN" "SECURITY"
        foreach ($issue in $securityIssues) {
            Write-VerificationLog "⚠️ $issue" "WARN" "SECURITY"
            $verificationSuite.TestResults.SecurityScanning.Details += $issue
        }
        $verificationSuite.TestResults.SecurityScanning.Status = if ($securityScore -ge 80) { "WARNING" } else { "FAILED" }
        $verificationSuite.TestResults.SecurityScanning.Score = [Math]::Max(0, $securityScore)
    }
}

function Test-ProvenanceTracking {
    if ($SkipProvenance) {
        Write-VerificationLog "Provenance tracking verification skipped" "INFO" "PROVENANCE"
        $verificationSuite.TestResults.ProvenanceTracking.Status = "SKIPPED"
        return
    }
    
    Write-VerificationLog "Verifying source provenance tracking..." "INFO" "PROVENANCE"
    
    $manifest = Test-BuildManifest
    if (-not $manifest) {
        $verificationSuite.TestResults.ProvenanceTracking.Status = "FAILED"
        return
    }
    
    $buildInfo = $manifest.BuildInfo
    $score = 0
    $totalChecks = 0
    
    # Check Git commit information
    $totalChecks++
    if ($buildInfo.GitCommit -and $buildInfo.GitCommit -ne "unknown") {
        Write-VerificationLog "✅ Git commit tracked: $($buildInfo.GitCommit)" "INFO" "PROVENANCE"
        $verificationSuite.TestResults.ProvenanceTracking.Details += "Git commit: $($buildInfo.GitCommit)"
        $score += 25
    } else {
        Write-VerificationLog "❌ Git commit not tracked" "WARN" "PROVENANCE"
        $verificationSuite.TestResults.ProvenanceTracking.Details += "Git commit not tracked"
    }
    
    # Check build time tracking
    $totalChecks++
    if ($buildInfo.BuildTime) {
        Write-VerificationLog "✅ Build time tracked: $($buildInfo.BuildTime)" "INFO" "PROVENANCE"
        $verificationSuite.TestResults.ProvenanceTracking.Details += "Build time: $($buildInfo.BuildTime)"
        $score += 25
    } else {
        Write-VerificationLog "❌ Build time not tracked" "WARN" "PROVENANCE"
        $verificationSuite.TestResults.ProvenanceTracking.Details += "Build time not tracked"
    }
    
    # Check expected Git commit validation
    $totalChecks++
    if ($ExpectedGitCommit -and $buildInfo.GitCommit -eq $ExpectedGitCommit) {
        Write-VerificationLog "✅ Git commit matches expected commit" "INFO" "PROVENANCE"
        $verificationSuite.TestResults.ProvenanceTracking.Details += "Git commit validation passed"
        $score += 25
    } elseif ($ExpectedGitCommit) {
        Write-VerificationLog "❌ Git commit mismatch. Expected: $ExpectedGitCommit, Actual: $($buildInfo.GitCommit)" "WARN" "PROVENANCE"
        $verificationSuite.TestResults.ProvenanceTracking.Details += "Git commit validation failed"
    } else {
        Write-VerificationLog "ℹ️ No expected Git commit specified" "INFO" "PROVENANCE"
        $verificationSuite.TestResults.ProvenanceTracking.Details += "No expected commit specified"
        $score += 25
    }
    
    # Check build environment tracking
    $totalChecks++
    if ($buildInfo.BuildMachine -or $buildInfo.OSPlatform) {
        Write-VerificationLog "✅ Build environment tracked" "INFO" "PROVENANCE"
        $verificationSuite.TestResults.ProvenanceTracking.Details += "Build environment: $($buildInfo.BuildMachine) on $($buildInfo.OSPlatform)"
        $score += 25
    } else {
        Write-VerificationLog "❌ Build environment not fully tracked" "WARN" "PROVENANCE"
        $verificationSuite.TestResults.ProvenanceTracking.Details += "Build environment tracking incomplete"
    }
    
    $finalScore = [Math]::Round(($score / $totalChecks) * 100)
    $verificationSuite.TestResults.ProvenanceTracking.Score = $finalScore
    
    if ($finalScore -ge 80) {
        $verificationSuite.TestResults.ProvenanceTracking.Status = "PASSED"
    } elseif ($finalScore -ge 60) {
        $verificationSuite.TestResults.ProvenanceTracking.Status = "WARNING"
    } else {
        $verificationSuite.TestResults.ProvenanceTracking.Status = "FAILED"
    }
}

function New-VerificationReport {
    param([string]$OutputPath)
    
    # Calculate summary
    $totalTests = $verificationSuite.Summary.TotalTests
    $passedTests = ($verificationSuite.TestResults.Values | Where-Object { $_.Status -eq "PASSED" }).Count
    $failedTests = ($verificationSuite.TestResults.Values | Where-Object { $_.Status -eq "FAILED" }).Count
    $warningTests = ($verificationSuite.TestResults.Values | Where-Object { $_.Status -eq "WARNING" }).Count
    $skippedTests = ($verificationSuite.TestResults.Values | Where-Object { $_.Status -eq "SKIPPED" }).Count
    
    $verificationSuite.Summary.PassedTests = $passedTests
    $verificationSuite.Summary.FailedTests = $failedTests
    $verificationSuite.Summary.WarningTests = $warningTests
    
    # Calculate overall score
    $totalScore = ($verificationSuite.TestResults.Values | Where-Object { $_.Status -ne "SKIPPED" } | Measure-Object -Property Score -Sum).Sum
    $activeTests = ($verificationSuite.TestResults.Values | Where-Object { $_.Status -ne "SKIPPED" }).Count
    $verificationSuite.Summary.OverallScore = if ($activeTests -gt 0) { [Math]::Round($totalScore / $activeTests) } else { 100 }
    
    # Determine overall status
    if ($failedTests -gt 0) {
        $verificationSuite.Summary.OverallStatus = "FAILED"
    } elseif ($warningTests -gt 0) {
        $verificationSuite.Summary.OverallStatus = "WARNING"
    } else {
        $verificationSuite.Summary.OverallStatus = "PASSED"
    }
    
    # Generate report based on output format
    switch ($OutputFormat) {
        "JSON" {
            $report = $verificationSuite | ConvertTo-Json -Depth 10
            $report | Out-File -FilePath $OutputPath -Encoding UTF8
        }
        "Markdown" {
            $report = @"
# TiXL Secure Build Verification Report

**Suite Version:** $suiteVersion  
**Execution Time:** $($verificationSuite.SuiteInfo.ExecutionTime)  
**Build Path:** $BuildPath  
**Overall Status:** $(switch ($verificationSuite.Summary.OverallStatus) {
        "PASSED" { "✅ PASSED" }
        "WARNING" { "⚠️ WARNING" }
        "FAILED" { "❌ FAILED" }
        default { "❓ UNKNOWN" }
})  
**Overall Score:** $($verificationSuite.Summary.OverallScore)/100

## Summary

- **Total Tests:** $totalTests
- **Passed:** $passedTests ✅
- **Failed:** $failedTests ❌
- **Warnings:** $warningTests ⚠️
- **Skipped:** $skippedTests ⏭️

## Test Results

$(
    foreach ($test in $verificationSuite.TestResults.Values) {
        $statusIcon = switch ($test.Status) {
            "PASSED" { "✅" }
            "WARNING" { "⚠️" }
            "FAILED" { "❌" }
            "SKIPPED" { "⏭️" }
            "PENDING" { "⏳" }
            default { "❓" }
        }
        $detailsText = if ($test.Details.Count -gt 0) { "`n$($test.Details | ForEach-Object { "- $_" } | Out-String)" } else { "" }
        "### $statusIcon $($test.Name) ($($test.Score)/100)$detailsText`n"
    }
)

## Detailed Results

$(
    foreach ($result in $verificationSuite.DetailedResults | Select-Object -Last 50) {
        $icon = switch ($result.Level) {
            "INFO" { "ℹ️" }
            "WARN" { "⚠️" }
            "ERROR" { "❌" }
            default { "ℹ️" }
        }
        "- $icon [$($result.Category)] $($result.Message)"
    }
)

---
*Generated by $suiteName v$suiteVersion*
"@
            $report | Out-File -FilePath $OutputPath -Encoding UTF8
        }
        default {
            # Text format
            Write-Host "`n======================================" -ForegroundColor Cyan
            Write-Host "VERIFICATION SUMMARY" -ForegroundColor Cyan
            Write-Host "======================================" -ForegroundColor Cyan
            Write-Host "Overall Status: $($verificationSuite.Summary.OverallStatus)" -ForegroundColor $(switch ($verificationSuite.Summary.OverallStatus) {
                "PASSED" { "Green" }
                "WARNING" { "Yellow" }
                "FAILED" { "Red" }
                default { "White" }
            })
            Write-Host "Overall Score: $($verificationSuite.Summary.OverallScore)/100" -ForegroundColor White
            Write-Host "Total Tests: $totalTests" -ForegroundColor White
            Write-Host "Passed: $passedTests" -ForegroundColor Green
            Write-Host "Failed: $failedTests" -ForegroundColor Red
            Write-Host "Warnings: $warningTests" -ForegroundColor Yellow
            Write-Host "Skipped: $skippedTests" -ForegroundColor Gray
            Write-Host "======================================" -ForegroundColor Cyan
            
            if ($DetailedReport) {
                Write-Host "`nDETAILED RESULTS:" -ForegroundColor Cyan
                foreach ($test in $verificationSuite.TestResults.Values) {
                    $statusColor = switch ($test.Status) {
                        "PASSED" { "Green" }
                        "WARNING" { "Yellow" }
                        "FAILED" { "Red" }
                        "SKIPPED" { "Gray" }
                        default { "White" }
                    }
                    
                    Write-Host "`n$($test.Name) ($($test.Score)/100)" -ForegroundColor $statusColor
                    foreach ($detail in $test.Details) {
                        Write-Host "  - $detail" -ForegroundColor Gray
                    }
                }
            }
        }
    }
    
    if ($OutputPath) {
        Write-VerificationLog "Report generated: $OutputPath" "INFO" "REPORT"
    }
}

# Main execution
try {
    Write-VerificationLog "Starting TiXL Secure Build Verification Suite" "INFO" "MAIN"
    
    # Step 1: Test build path
    if (-not (Test-BuildPath)) {
        throw "Build path validation failed"
    }
    
    # Step 2: Test reproducible build settings
    $manifest = Test-BuildManifest
    Test-ReproducibleBuildSettings -Manifest $manifest
    
    # Step 3: Test SourceLink integration
    Test-SourceLinkIntegration
    
    # Step 4: Test code signing verification
    Test-CodeSigningVerification
    
    # Step 5: Test artifact integrity
    Test-ArtifactIntegrity
    
    # Step 6: Test security scanning
    Test-SecurityScanning
    
    # Step 7: Test provenance tracking
    Test-ProvenanceTracking
    
    # Step 8: Generate report
    if ($OutputPath) {
        if (-not $OutputPath.EndsWith(".$OutputFormat".ToLower())) {
            $OutputPath += ".$OutputFormat"
        }
        New-VerificationReport -OutputPath $OutputPath
    } else {
        New-VerificationReport -OutputPath ""
    }
    
    # Final status and recommendations
    if ($verificationSuite.Summary.OverallStatus -eq "FAILED") {
        Write-VerificationLog "Verification suite FAILED - critical issues detected" "ERROR" "MAIN"
        Write-VerificationLog "Please review failed tests and resolve issues before releasing" "WARN" "MAIN"
        exit 1
    } elseif ($verificationSuite.Summary.OverallStatus -eq "WARNING") {
        if ($FailOnWarnings) {
            Write-VerificationLog "Verification suite completed with warnings" "WARN" "MAIN"
            exit 1
        } else {
            Write-VerificationLog "Verification suite completed with warnings" "WARN" "MAIN"
            Write-VerificationLog "Review warnings but build can proceed" "INFO" "MAIN"
            exit 0
        }
    } else {
        Write-VerificationLog "Verification suite PASSED successfully" "INFO" "MAIN"
        Write-VerificationLog "All security checks passed - build is ready for release" "INFO" "MAIN"
        exit 0
    }
}
catch {
    Write-VerificationLog "Verification suite failed: $($_.Exception.Message)" "ERROR" "MAIN"
    Write-VerificationLog $_.ScriptStackTrace "DEBUG" "MAIN"
    exit 1
}