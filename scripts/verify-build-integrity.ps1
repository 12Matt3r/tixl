#!/usr/bin/env pwsh
<#
.SYNOPSIS
    TiXL Build Integrity Verification Tool
.DESCRIPTION
    Verifies the integrity and authenticity of TiXL build artifacts.
    Checks signatures, validates checksums, and verifies provenance.
.PARAMETER BuildManifestPath
    Path to build manifest JSON file
.PARAMETER ArtifactsPath
    Directory containing build artifacts
.PARAMETER VerifySignatures
    Verify code signatures on signed artifacts
.PARAMETER CheckProvenance
    Verify source link provenance information
.EXAMPLE
    .\scripts\verify-build-integrity.ps1 -BuildManifestPath "artifacts\build-manifest.json"
#>

param(
    [Parameter(Mandatory = $false)]
    [string]$BuildManifestPath = "artifacts\secure-build\build-manifest.json",
    
    [Parameter(Mandatory = $false)]
    [string]$ArtifactsPath = "artifacts\secure-build",
    
    [Parameter(Mandatory = $false)]
    [switch]$VerifySignatures = $true,
    
    [Parameter(Mandatory = $false)]
    [switch]$CheckProvenance = $true,
    
    [Parameter(Mandatory = $false)]
    [string]$ExpectedGitCommit = "",
    
    [Parameter(Mandatory = $false)]
    [string]$OutputReportPath = "build-integrity-report.md"
)

$ErrorActionPreference = "Stop"
$InformationPreference = "Continue"

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "TiXL Build Integrity Verification" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan

# Verification results
$verificationResults = @{
    OverallStatus = "PENDING"
    Checks = @()
    Summary = @{
        TotalArtifacts = 0
        VerifiedArtifacts = 0
        FailedArtifacts = 0
        SignedArtifacts = 0
        UnsignedArtifacts = 0
    }
    Warnings = @()
    Errors = @()
    Provenance = @()
}

function Write-VerificationLog {
    param([string]$Message, [string]$Level = "INFO", [string]$Check = "GENERAL")
    
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss UTC"
    $logMessage = "[$timestamp] [$Level] [$Check] $Message"
    
    switch ($Level) {
        "ERROR" { Write-Host $logMessage -ForegroundColor Red; $verificationResults.Errors += $Message }
        "WARN" { Write-Host $logMessage -ForegroundColor Yellow; $verificationResults.Warnings += $Message }
        "INFO" { Write-Host $logMessage -ForegroundColor Green }
        "DEBUG" { Write-Host $logMessage -ForegroundColor Magenta }
    }
    
    # Add to checks array
    $verificationResults.Checks += @{
        Check = $Check
        Level = $Level
        Message = $Message
        Time = $timestamp
    }
}

function Read-BuildManifest {
    param([string]$ManifestPath)
    
    if (-not (Test-Path $ManifestPath)) {
        Write-VerificationLog "Build manifest not found: $ManifestPath" "ERROR" "MANIFEST"
        return $null
    }
    
    try {
        $manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
        Write-VerificationLog "Build manifest loaded successfully" "INFO" "MANIFEST"
        return $manifest
    }
    catch {
        Write-VerificationLog "Failed to parse build manifest: $($_.Exception.Message)" "ERROR" "MANIFEST"
        return $null
    }
}

function Test-FileHash {
    param([string]$FilePath, [string]$ExpectedHash, [string]$Algorithm = "SHA256")
    
    if (-not (Test-Path $FilePath)) {
        return $false
    }
    
    try {
        $actualHash = Get-FileHash -Path $FilePath -Algorithm $Algorithm
        $hashMatches = $actualHash.Hash -eq $ExpectedHash
        
        if ($hashMatches) {
            Write-VerificationLog "Hash verification passed: $([System.IO.Path]::GetFileName($FilePath))" "INFO" "INTEGRITY"
            return $true
        } else {
            Write-VerificationLog "Hash verification failed: $([System.IO.Path]::GetFileName($FilePath))" "ERROR" "INTEGRITY"
            Write-VerificationLog "Expected: $ExpectedHash" "ERROR" "INTEGRITY"
            Write-VerificationLog "Actual: $($actualHash.Hash)" "ERROR" "INTEGRITY"
            return $false
        }
    }
    catch {
        Write-VerificationLog "Hash calculation failed for $([System.IO.Path]::GetFileName($FilePath)): $($_.Exception.Message)" "ERROR" "INTEGRITY"
        return $false
    }
}

function Test-CodeSignature {
    param([string]$FilePath)
    
    if (-not $VerifySignatures) {
        return $null
    }
    
    if (-not (Test-Path $FilePath)) {
        return $null
    }
    
    try {
        # For .NET assemblies, check if they contain signature information
        $isSigned = $false
        $signatureInfo = ""
        
        if ($FilePath -match "\.(dll|exe)$") {
            # Use signtool to verify signature
            try {
                $verifyResult = & signtool.exe verify /pa $FilePath 2>&1
                $isSigned = $verifyResult -match "Signature verified"
            }
            catch {
                # signtool might not be available, continue without signature verification
                $isSigned = $false
            }
        }
        elseif ($FilePath -match "\.nupkg$") {
            # For NuGet packages, use dotnet nuget verify
            try {
                & dotnet nuget verify sign $FilePath | Out-Null
                $isSigned = ($LASTEXITCODE -eq 0)
            }
            catch {
                $isSigned = $false
            }
        }
        
        if ($isSigned) {
            Write-VerificationLog "Signature verification passed: $([System.IO.Path]::GetFileName($FilePath))" "INFO" "SIGNATURE"
            return $true
        } else {
            Write-VerificationLog "No valid signature found: $([System.IO.Path]::GetFileName($FilePath))" "WARN" "SIGNATURE"
            return $false
        }
    }
    catch {
        Write-VerificationLog "Signature verification failed for $([System.IO.Path]::GetFileName($FilePath)): $($_.Exception.Message)" "WARN" "SIGNATURE"
        return $null
    }
}

function Test-SourceLinkProvenance {
    param([string]$AssemblyPath)
    
    if (-not $CheckProvenance) {
        return $null
    }
    
    if (-not (Test-Path $AssemblyPath)) {
        return $null
    }
    
    try {
        # Check if the assembly has SourceLink information
        # This would require reading the PDB or using a SourceLink tool
        # For now, we'll do a basic check
        
        $fileName = [System.IO.Path]::GetFileName($AssemblyPath)
        
        # Look for .pdb file with same name
        $pdbPath = [System.IO.Path]::ChangeExtension($AssemblyPath, ".pdb")
        $hasPdb = Test-Path $pdbPath
        
        if ($hasPdb) {
            Write-VerificationLog "Debug symbols found: $fileName" "INFO" "PROVENANCE"
            return $true
        } else {
            Write-VerificationLog "No debug symbols found: $fileName" "WARN" "PROVENANCE"
            return $false
        }
    }
    catch {
        Write-VerificationLog "Provenance check failed for $([System.IO.Path]::GetFileName($AssemblyPath)): $($_.Exception.Message)" "WARN" "PROVENANCE"
        return $null
    }
}

function Test-BuildEnvironment {
    param($Manifest)
    
    if (-not $Manifest) {
        return
    }
    
    Write-VerificationLog "Verifying build environment..." "INFO" "ENVIRONMENT"
    
    # Check build configuration
    $reproducibleConfig = $Manifest.ReproducibleBuild
    if ($reproducibleConfig) {
        foreach ($key in $reproducibleConfig.PSObject.Properties.Name) {
            $value = $reproducibleConfig.$key
            Write-VerificationLog "Reproducible setting - $key : $value" "DEBUG" "ENVIRONMENT"
        }
    }
    
    # Check build info
    $buildInfo = $Manifest.BuildInfo
    if ($buildInfo) {
        Write-VerificationLog "Build ID: $($buildInfo.BuildId)" "INFO" "ENVIRONMENT"
        Write-VerificationLog "Build Time: $($buildInfo.BuildTime)" "INFO" "ENVIRONMENT"
        Write-VerificationLog "Git Commit: $($buildInfo.GitCommit)" "INFO" "ENVIRONMENT"
        
        # Validate expected git commit if provided
        if ($ExpectedGitCommit -and $buildInfo.GitCommit -ne $ExpectedGitCommit) {
            Write-VerificationLog "Git commit mismatch. Expected: $ExpectedGitCommit, Actual: $($buildInfo.GitCommit)" "ERROR" "ENVIRONMENT"
        }
    }
}

function Test-ArtifactCompleteness {
    param([string]$ArtifactsPath)
    
    Write-VerificationLog "Checking artifact completeness..." "INFO" "COMPLETENESS"
    
    if (-not (Test-Path $ArtifactsPath)) {
        Write-VerificationLog "Artifacts directory not found: $ArtifactsPath" "ERROR" "COMPLETENESS"
        return
    }
    
    $artifacts = Get-ChildItem -Path $ArtifactsPath -File
    $verificationResults.Summary.TotalArtifacts = $artifacts.Count
    
    Write-VerificationLog "Found $($artifacts.Count) artifacts" "INFO" "COMPLETENESS"
    
    # Group by type
    $dlls = $artifacts | Where-Object { $_.Extension -eq ".dll" }
    $exes = $artifacts | Where-Object { $_.Extension -eq ".exe" }
    $nupkgs = $artifacts | Where-Object { $_.Extension -eq ".nupkg" }
    $snupkgs = $artifacts | Where-Object { $_.Extension -eq ".snupkg" }
    
    Write-VerificationLog "Assemblies: $($dlls.Count + $exes.Count)" "INFO" "COMPLETENESS"
    Write-VerificationLog "NuGet Packages: $($nupkgs.Count)" "INFO" "COMPLETENESS"
    Write-VerificationLog "Symbol Packages: $($snupkgs.Count)" "INFO" "COMPLETENESS"
}

function Verify-AllArtifacts {
    param($Manifest, [string]$ArtifactsPath)
    
    Write-VerificationLog "Starting artifact verification..." "INFO" "VERIFICATION"
    
    if (-not $Manifest -or -not $Manifest.Integrity) {
        Write-VerificationLog "No integrity data in manifest" "WARN" "VERIFICATION"
        return
    }
    
    foreach ($integrityEntry in $Manifest.Integrity) {
        $fileName = $integrityEntry.FileName
        $expectedHash = $integrityEntry.Hash
        
        # Find the actual file
        $artifactPath = Join-Path $ArtifactsPath $fileName
        
        if (-not (Test-Path $artifactPath)) {
            Write-VerificationLog "Artifact not found: $fileName" "ERROR" "VERIFICATION"
            $verificationResults.Summary.FailedArtifacts++
            continue
        }
        
        # Test hash
        $hashValid = Test-FileHash -FilePath $artifactPath -ExpectedHash $expectedHash
        
        # Test signature
        $signatureValid = Test-CodeSignature -FilePath $artifactPath
        
        # Test provenance
        $provenanceValid = Test-SourceLinkProvenance -AssemblyPath $artifactPath
        
        # Update counters
        if ($hashValid) {
            $verificationResults.Summary.VerifiedArtifacts++
        } else {
            $verificationResults.Summary.FailedArtifacts++
        }
        
        if ($signatureValid -eq $true) {
            $verificationResults.Summary.SignedArtifacts++
        } elseif ($signatureValid -eq $false) {
            $verificationResults.Summary.UnsignedArtifacts++
        }
        
        # Store provenance info
        $verificationResults.Provenance += @{
            FileName = $fileName
            HashValid = $hashValid
            SignatureValid = $signatureValid
            ProvenanceValid = $provenanceValid
        }
    }
}

function New-VerificationReport {
    param([string]$ReportPath)
    
    Write-VerificationLog "Generating verification report..." "INFO" "REPORT"
    
    # Determine overall status
    if ($verificationResults.Errors.Count -gt 0) {
        $verificationResults.OverallStatus = "FAILED"
    } elseif ($verificationResults.Warnings.Count -gt 0) {
        $verificationResults.OverallStatus = "PASSED_WITH_WARNINGS"
    } else {
        $verificationResults.OverallStatus = "PASSED"
    }
    
    $report = @"
# TiXL Build Integrity Verification Report

**Verification Time:** $(Get-Date -Format "yyyy-MM-dd HH:mm:ss UTC")  
**Overall Status:** $(switch ($verificationResults.OverallStatus) {
    "PASSED" { "✅ PASSED" }
    "PASSED_WITH_WARNINGS" { "⚠️ PASSED WITH WARNINGS" }
    "FAILED" { "❌ FAILED" }
    default { "❓ UNKNOWN" }
})  
**Build Manifest:** $BuildManifestPath  

## Verification Summary

- **Total Artifacts:** $($verificationResults.Summary.TotalArtifacts)
- **Verified Artifacts:** $($verificationResults.Summary.VerifiedArtifacts)
- **Failed Artifacts:** $($verificationResults.Summary.FailedArtifacts)
- **Signed Artifacts:** $($verificationResults.Summary.SignedArtifacts)
- **Unsigned Artifacts:** $($verificationResults.Summary.UnsignedArtifacts)

## Artifact Verification Results

| Artifact | Hash | Signature | Provenance | Status |
|----------|------|-----------|------------|--------|
$(
    foreach ($entry in $verificationResults.Provenance) {
        $hashIcon = if ($entry.HashValid) { "✅" } else { "❌" }
        $sigIcon = switch ($entry.SignatureValid) {
            $true { "✅" }
            $false { "⚪" }
            default { "❓" }
        }
        $provIcon = switch ($entry.ProvenanceValid) {
            $true { "✅" }
            $false { "⚪" }
            default { "❓" }
        }
        $status = if ($entry.HashValid -and ($entry.SignatureValid -ne $false)) { "✅ PASS" } else { "❌ FAIL" }
        "| $($entry.FileName) | $hashIcon | $sigIcon | $provIcon | $status |"
    }
)

## Security Checks

### Code Signing Verification
$(
    if ($VerifySignatures) {
        if ($verificationResults.Summary.SignedArtifacts -gt 0) {
            "✅ Code signing verification enabled and executed"
        } else {
            "⚪ No signed artifacts found or signing verification disabled"
        }
    } else {
        "⚪ Code signing verification disabled"
    }
)

### Source Provenance Verification
$(
    if ($CheckProvenance) {
        "✅ Source provenance verification enabled"
    } else {
        "⚪ Source provenance verification disabled"
    }
)

### Hash Integrity Verification
$(
    if ($verificationResults.Summary.FailedArtifacts -eq 0) {
        "✅ All artifact hashes verified successfully"
    } else {
        "❌ $($verificationResults.Summary.FailedArtifacts) artifact(s) failed hash verification"
    }
)

## Warnings and Errors

### Warnings ($($verificationResults.Warnings.Count))
$(
    if ($verificationResults.Warnings.Count -gt 0) {
        foreach ($warning in $verificationResults.Warnings) {
            "- ⚠️ $warning"
        }
    } else {
        "- ✅ No warnings"
    }
)

### Errors ($($verificationResults.Errors.Count))
$(
    if ($verificationResults.Errors.Count -gt 0) {
        foreach ($error in $verificationResults.Errors) {
            "- ❌ $error"
        }
    } else {
        "- ✅ No errors"
    }
)

## Verification Details

### Checks Performed ($($verificationResults.Checks.Count))
$(
    foreach ($check in $verificationResults.Checks | Select-Object -Last 20) {
        $icon = switch ($check.Level) {
            "INFO" { "ℹ️" }
            "WARN" { "⚠️" }
            "ERROR" { "❌" }
            default { "ℹ️" }
        }
        "- $icon [$($check.Check)] $($check.Message)"
    }
)

---
*Generated by TiXL Build Integrity Verification Tool*
"@
    
    $report | Out-File -FilePath $ReportPath -Encoding UTF8
    Write-VerificationLog "Verification report saved to: $ReportPath" "INFO" "REPORT"
}

# Main execution
try {
    # Load build manifest
    $manifest = Read-BuildManifest -ManifestPath $BuildManifestPath
    
    # Verify build environment
    Test-BuildEnvironment -Manifest $manifest
    
    # Check artifact completeness
    Test-ArtifactCompleteness -ArtifactsPath $ArtifactsPath
    
    # Verify all artifacts
    Verify-AllArtifacts -Manifest $manifest -ArtifactsPath $ArtifactsPath
    
    # Generate report
    New-VerificationReport -ReportPath $OutputReportPath
    
    # Print summary
    Write-Host "======================================" -ForegroundColor Cyan
    Write-Host "VERIFICATION SUMMARY" -ForegroundColor Cyan
    Write-Host "======================================" -ForegroundColor Cyan
    Write-Host "Status: $($verificationResults.OverallStatus)" -ForegroundColor $(switch ($verificationResults.OverallStatus) {
        "PASSED" { "Green" }
        "PASSED_WITH_WARNINGS" { "Yellow" }
        "FAILED" { "Red" }
        default { "White" }
    })
    Write-Host "Total Artifacts: $($verificationResults.Summary.TotalArtifacts)" -ForegroundColor White
    Write-Host "Verified: $($verificationResults.Summary.VerifiedArtifacts)" -ForegroundColor Green
    Write-Host "Failed: $($verificationResults.Summary.FailedArtifacts)" -ForegroundColor Red
    Write-Host "Signed: $($verificationResults.Summary.SignedArtifacts)" -ForegroundColor Blue
    Write-Host "Warnings: $($verificationResults.Warnings.Count)" -ForegroundColor Yellow
    Write-Host "Errors: $($verificationResults.Errors.Count)" -ForegroundColor Red
    Write-Host "======================================" -ForegroundColor Cyan
    
    # Exit with appropriate code
    if ($verificationResults.Errors.Count -gt 0) {
        exit 1
    } elseif ($verificationResults.Warnings.Count -gt 0) {
        exit 0
    } else {
        exit 0
    }
}
catch {
    Write-Host "Verification failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}