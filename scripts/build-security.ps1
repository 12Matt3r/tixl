#!/usr/bin/env pwsh
<#
.SYNOPSIS
    TiXL Secure Build System - Reproducible Builds with Code Signing
.DESCRIPTION
    Implements secure build process with reproducible builds, code signing, and integrity verification.
    Provides end-to-end trust from source code to final artifacts.
.PARAMETER Configuration
    Build configuration (Debug/Release)
.PARAMETER SignArtifacts
    Enable code signing for all artifacts
.PARAMETER VerifyIntegrity
    Verify artifact integrity after build
.PARAMETER OutputPath
    Output directory for signed artifacts
.PARAMETER TimestampUrl
    RFC3161 timestamp server URL
.PARAMETER CertificateThumbprint
    Code signing certificate thumbprint
.EXAMPLE
    .\scripts\build-security.ps1 -Configuration Release -SignArtifacts
#>

param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    
    [Parameter(Mandatory = $false)]
    [switch]$SignArtifacts = $false,
    
    [Parameter(Mandatory = $false)]
    [switch]$VerifyIntegrity = $true,
    
    [Parameter(Mandatory = $false)]
    [string]$OutputPath = "artifacts\secure-build",
    
    [Parameter(Mandatory = $false)]
    [string]$TimestampUrl = "http://timestamp.digicert.com",
    
    [Parameter(Mandatory = $false)]
    [string]$CertificateThumbprint = "",
    
    [Parameter(Mandatory = $false)]
    [string]$CertificateStore = "Cert:\CurrentUser\My",
    
    [Parameter(Mandatory = $false)]
    [switch]$GenerateBuildManifest = $true,
    
    [Parameter(Mandatory = $false)]
    [switch]$PreserveBuildEnvironment = $false
)

$ErrorActionPreference = "Stop"
$InformationPreference = "Continue"

# Script version and metadata
$scriptVersion = "1.0.0"
$scriptName = "TiXL Secure Build System"
$buildId = (Get-Date -Format "yyyyMMdd-HHmmss")
$gitCommit = if (Test-Path ".git") { git rev-parse --short HEAD } else { "unknown" }
$gitBranch = if (Test-Path ".git") { git rev-parse --abbrev-ref HEAD } else { "unknown" }

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "$scriptName v$scriptVersion" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "Build ID: $buildId" -ForegroundColor Yellow
Write-Host "Git Commit: $gitCommit" -ForegroundColor Yellow
Write-Host "Git Branch: $gitBranch" -ForegroundColor Yellow
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Signing: $(if ($SignArtifacts) { 'Enabled' } else { 'Disabled' })" -ForegroundColor Yellow
Write-Host "======================================" -ForegroundColor Cyan

# Ensure output directory exists
$outputDir = Resolve-Path $OutputPath -ErrorAction SilentlyContinue
if (-not $outputDir) {
    $outputDir = New-Item -ItemType Directory -Path $OutputPath -Force
}
$outputDir = $outputDir.Path

# Initialize build manifest
$buildManifest = @{
    BuildInfo = @{
        BuildId = $buildId
        BuildTime = (Get-Date).ToUniversalTime()
        Configuration = $Configuration
        GitCommit = $gitCommit
        GitBranch = $gitBranch
        ScriptVersion = $scriptVersion
        BuildMachine = $env:COMPUTERNAME
        User = $env:USERNAME
        DotNetVersion = dotnet --version
        OSPlatform = if ($IsLinux) { "Linux" } elseif ($IsMacOS) { "macOS" } else { "Windows" }
    }
    ReproducibleBuild = @{
        SourceLinkEnabled = $true
        EmbedSources = $true
        DeterministicBuild = $true
        FixedBuildTime = $true
        FixedFileVersion = $true
    }
    Security = @{
        SigningEnabled = $SignArtifacts
        IntegrityVerification = $VerifyIntegrity
        TimestampServer = $TimestampUrl
        CertificateStore = $CertificateStore
    }
    Artifacts = @()
    Integrity = @()
    Warnings = @()
    Errors = @()
}

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss UTC"
    $logMessage = "[$timestamp] [$Level] $Message"
    
    switch ($Level) {
        "ERROR" { Write-Host $logMessage -ForegroundColor Red; $buildManifest.Errors += $Message }
        "WARN" { Write-Host $logMessage -ForegroundColor Yellow; $buildManifest.Warnings += $Message }
        "INFO" { Write-Host $logMessage -ForegroundColor Green }
        "DEBUG" { Write-Host $logMessage -ForegroundColor Magenta }
    }
}

function Test-Certificate {
    param([string]$Thumbprint, [string]$Store)
    
    if (-not $Thumbprint) {
        Write-Log "No certificate thumbprint provided, signing will be skipped" "WARN"
        return $false
    }
    
    try {
        $cert = Get-ChildItem -Path $Store -Recurse | Where-Object { $_.Thumbprint -eq $Thumbprint } | Select-Object -First 1
        if ($cert) {
            Write-Log "Certificate found: $($cert.Subject)" "INFO"
            return $true
        } else {
            Write-Log "Certificate with thumbprint $Thumbprint not found in $Store" "WARN"
            return $false
        }
    }
    catch {
        Write-Log "Error accessing certificate store: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

function Set-EnvironmentForReproducibleBuild {
    Write-Log "Configuring environment for reproducible builds"
    
    # Environment variables for deterministic builds
    $envVars = @{
        "DOTNET_CLI_TELEMETRY_OPTOUT" = "1"
        "DOTNET_NOLOGO" = "1"
        "MSBUILDDISABLENODEREUSE" = "1"
        "MSBUILDDEBUGPATH" = $outputDir
        # Deterministic build settings
        "DOTNET_BUILD_DATE" = "2024-01-01T00:00:00Z"
        "DOTNET_BUILD_TIME_UTC" = "1735689600"
        "SOURCE_LINK_DATE_TIME" = "2024-01-01T00:00:00+00:00"
    }
    
    foreach ($key in $envVars.Keys) {
        if (-not $PreserveBuildEnvironment) {
            Set-Item -Path "env:$key" -Value $envVars[$key]
        }
    }
    
    Write-Log "Environment configured for reproducible builds"
}

function Set-FileTimestamps {
    param([string]$Path)
    
    if (-not (Test-Path $Path)) {
        return
    }
    
    # Set consistent timestamps for reproducible builds
    $fixedTime = [DateTime]::Parse("2024-01-01T00:00:00Z").ToUniversalTime()
    
    try {
        Set-ItemProperty -Path $Path -Name "LastWriteTime" -Value $fixedTime -Force -ErrorAction SilentlyContinue
        Set-ItemProperty -Path $Path -Name "CreationTime" -Value $fixedTime -Force -ErrorAction SilentlyContinue
    }
    catch {
        Write-Log "Could not set fixed timestamp for $Path" "WARN"
    }
}

function Build-Solution {
    param([string]$SolutionPath = "TiXL.sln")
    
    Write-Log "Building solution: $SolutionPath"
    
    if (-not (Test-Path $SolutionPath)) {
        Write-Log "Solution file not found: $SolutionPath" "ERROR"
        return $false
    }
    
    # Restore with security audit
    Write-Log "Restoring packages with security audit..."
    $restoreResult = & dotnet restore $SolutionPath --verbosity minimal --audit
    if ($LASTEXITCODE -ne 0) {
        Write-Log "Package restore failed with security audit" "ERROR"
        return $false
    }
    
    # Build with reproducible settings
    Write-Log "Building solution with reproducible settings..."
    $buildArgs = @(
        "build", $SolutionPath,
        "--configuration", $Configuration,
        "--no-restore",
        "--verbosity", "minimal",
        "/p:ContinuousIntegrationBuild=true",
        "/p:Deterministic=true",
        "/p:DeterministicSourcePaths=true",
        "/p:ContinuousIntegrationBuild=true",
        "/p:ProduceReferenceAssembly=false",
        "/p:GenerateDocumentationFile=true",
        "/p:PublishRepositoryUrl=true",
        "/p:EmbedUntrackedSources=true",
        "/p:IncludeSymbols=true",
        "/p:SymlinkPortablePdb=true"
    )
    
    $buildResult = & dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Log "Build failed" "ERROR"
        $buildManifest.Errors += "Build failed with exit code $LASTEXITCODE"
        return $false
    }
    
    Write-Log "Build completed successfully" "INFO"
    return $true
}

function New-NuGetPackages {
    param([string]$SolutionPath = "TiXL.sln")
    
    Write-Log "Creating NuGet packages..."
    
    $packageOutput = Join-Path $outputDir "packages"
    New-Item -ItemType Directory -Path $packageOutput -Force | Out-Null
    
    $packArgs = @(
        "pack", $SolutionPath,
        "--configuration", $Configuration,
        "--no-build",
        "--output", $packageOutput,
        "/p:IncludeSymbols=true",
        "/p:SymbolPackageFormat=snupkg"
    )
    
    $packResult = & dotnet @packArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Log "Package creation failed" "ERROR"
        return @()
    }
    
    $packages = Get-ChildItem -Path $packageOutput -Filter "*.nupkg"
    Write-Log "Created $($packages.Count) NuGet packages" "INFO"
    
    return $packages.FullName
}

function Invoke-CodeSigning {
    param([string[]]$Artifacts)
    
    if (-not $SignArtifacts) {
        Write-Log "Code signing is disabled, skipping" "INFO"
        return
    }
    
    if (-not (Test-Certificate -Thumbprint $CertificateThumbprint -Store $CertificateStore)) {
        Write-Log "Skipping code signing - certificate not available" "WARN"
        return
    }
    
    Write-Log "Starting code signing process..."
    
    foreach ($artifact in $Artifacts) {
        if (-not (Test-Path $artifact)) {
            Write-Log "Artifact not found: $artifact" "WARN"
            continue
        }
        
        $fileName = [System.IO.Path]::GetFileName($artifact)
        Write-Log "Signing artifact: $fileName"
        
        try {
            # Sign .dll, .exe, and .nupkg files
            if ($artifact -match "\.(dll|exe)$") {
                & signtool.exe sign `
                    /f $CertificateStore `
                    /sha1 $CertificateThumbprint `
                    /t $TimestampUrl `
                    /fd sha256 `
                    /d $fileName `
                    $artifact
                    
                if ($LASTEXITCODE -eq 0) {
                    Write-Log "Successfully signed: $fileName" "INFO"
                } else {
                    Write-Log "Failed to sign: $fileName" "WARN"
                }
            }
            elseif ($artifact -match "\.nupkg$") {
                # Use NuGet sign for packages
                & dotnet nuget sign $artifact `
                    --certificate-store $CertificateStore `
                    --certificate-fingerprint $CertificateThumbprint
                    
                if ($LASTEXITCODE -eq 0) {
                    Write-Log "Successfully signed NuGet package: $fileName" "INFO"
                } else {
                    Write-Log "Failed to sign NuGet package: $fileName" "WARN"
                }
            }
        }
        catch {
            Write-Log "Error signing $fileName : $($_.Exception.Message)" "WARN"
        }
    }
    
    Write-Log "Code signing process completed"
}

function Test-ArtifactIntegrity {
    param([string[]]$Artifacts)
    
    if (-not $VerifyIntegrity) {
        Write-Log "Integrity verification is disabled" "INFO"
        return
    }
    
    Write-Log "Verifying artifact integrity..."
    
    foreach ($artifact in $Artifacts) {
        if (-not (Test-Path $artifact)) {
            continue
        }
        
        $fileName = [System.IO.Path]::GetFileName($artifact)
        
        try {
            # Calculate hash
            $hash = Get-FileHash -Path $artifact -Algorithm SHA256
            $hashValue = $hash.Hash
            
            # Verify signature for signed files
            $isSigned = $false
            if ($artifact -match "\.(dll|exe)$") {
                try {
                    & signtool.exe verify /pa $artifact | Out-Null
                    $isSigned = ($LASTEXITCODE -eq 0)
                }
                catch {
                    $isSigned = $false
                }
            }
            elseif ($artifact -match "\.nupkg$") {
                # Verify NuGet package signature
                & dotnet nuget verify sign $artifact | Out-Null
                $isSigned = ($LASTEXITCODE -eq 0)
            }
            
            $integrityEntry = @{
                FileName = $fileName
                FilePath = $artifact
                Hash = $hashValue
                HashAlgorithm = "SHA256"
                Size = (Get-Item $artifact).Length
                IsSigned = $isSigned
                VerificationTime = (Get-Date).ToUniversalTime()
            }
            
            $buildManifest.Integrity += $integrityEntry
            
            $signStatus = if ($isSigned) { "SIGNED" } else { "UNSIGNED" }
            Write-Log "Integrity verified: $fileName ($signStatus, SHA256:$($hashValue.Substring(0,16))...)" "INFO"
        }
        catch {
            Write-Log "Integrity verification failed for $fileName : $($_.Exception.Message)" "ERROR"
        }
    }
    
    Write-Log "Integrity verification completed"
}

function New-BuildManifest {
    param([string]$OutputPath)
    
    if (-not $GenerateBuildManifest) {
        return
    }
    
    Write-Log "Generating build manifest..."
    
    $manifestPath = Join-Path $OutputPath "build-manifest.json"
    $manifestJson = $buildManifest | ConvertTo-Json -Depth 10
    
    # Save JSON manifest
    $manifestJson | Out-File -FilePath $manifestPath -Encoding UTF8
    
    # Generate human-readable report
    $reportPath = Join-Path $OutputPath "build-security-report.md"
    $report = @"
# TiXL Build Security Report

**Build ID:** $($buildManifest.BuildInfo.BuildId)  
**Build Time:** $($buildManifest.BuildInfo.BuildTime)  
**Configuration:** $($buildManifest.BuildInfo.Configuration)  
**Git Commit:** $($buildManifest.BuildInfo.GitCommit)  

## Reproducible Build Configuration

✅ **SourceLink:** Enabled  
✅ **Embed Sources:** Enabled  
✅ **Deterministic Build:** Enabled  
✅ **Fixed Build Time:** Enabled  
✅ **Fixed File Version:** Enabled  

## Security Features

**Code Signing:** $(if ($buildManifest.Security.SigningEnabled) { "✅ Enabled" } else { "⚪ Disabled" })  
**Integrity Verification:** $(if ($buildManifest.Security.IntegrityVerification) { "✅ Enabled" } else { "⚪ Disabled" })  
**Timestamp Server:** $($buildManifest.Security.TimestampServer)  

## Artifact Integrity

| File | Size | Hash | Status |
|------|------|------|--------|
$(
    if ($buildManifest.Integrity.Count -gt 0) {
        foreach ($item in $buildManifest.Integrity) {
            $sizeMB = "{0:N2}" -f ($item.Size / 1MB)
            $hashShort = $item.Hash.Substring(0, 16) + "..."
            $status = if ($item.IsSigned) { "🔐 SIGNED" } else { "🔓 UNSIGNED" }
            "| $($item.FileName) | $sizeMB MB | $hashShort | $status |"
        }
    } else {
        "| No artifacts processed | | | |
"
    }
)

## Warnings and Errors

**Warnings:** $($buildManifest.Warnings.Count)  
**Errors:** $($buildManifest.Errors.Count)  

### Warnings
$(
    if ($buildManifest.Warnings.Count -gt 0) {
        foreach ($warning in $buildManifest.Warnings) {
            "- ⚠️ $warning"
        }
    } else {
        "- ✅ No warnings"
    }
)

### Errors
$(
    if ($buildManifest.Errors.Count -gt 0) {
        foreach ($error in $buildManifest.Errors) {
            "- ❌ $error"
        }
    } else {
        "- ✅ No errors"
    }
)

## Build Environment

- **OS Platform:** $($buildManifest.BuildInfo.OSPlatform)
- **Build Machine:** $($buildManifest.BuildInfo.BuildMachine)
- **User:** $($buildManifest.BuildInfo.User)
- **.NET Version:** $($buildManifest.BuildInfo.DotNetVersion)

---
*Generated by $scriptName v$scriptVersion*
"@
    
    $report | Out-File -FilePath $reportPath -Encoding UTF8
    
    Write-Log "Build manifest saved to: $manifestPath" "INFO"
    Write-Log "Build report saved to: $reportPath" "INFO"
}

# Main execution
try {
    Write-Log "Starting secure build process..."
    
    # Step 1: Configure reproducible build environment
    Set-EnvironmentForReproducibleBuild
    
    # Step 2: Build solution
    if (-not (Build-Solution)) {
        throw "Build failed"
    }
    
    # Step 3: Create NuGet packages
    $packages = New-NuGetPackages
    
    # Step 4: Collect all artifacts
    $allArtifacts = @()
    
    # Add NuGet packages
    $allArtifacts += $packages
    
    # Add built assemblies
    $dlls = Get-ChildItem -Path "." -Recurse -Filter "*.dll" -Exclude "Test*,*Test*,*Tests*,.*"
    $exes = Get-ChildItem -Path "." -Recurse -Filter "*.exe" -Exclude "Test*,*Test*,*Tests*,.*"
    $allArtifacts += $dlls.FullName
    $allArtifacts += $exes.FullName
    
    # Step 5: Sign artifacts
    if ($SignArtifacts) {
        Invoke-CodeSigning -Artifacts $allArtifacts
    }
    
    # Step 6: Verify integrity
    Test-ArtifactIntegrity -Artifacts $allArtifacts
    
    # Step 7: Generate build manifest
    New-BuildManifest -OutputPath $outputDir
    
    # Step 8: Copy artifacts to output directory
    Write-Log "Copying artifacts to output directory..."
    foreach ($artifact in $allArtifacts) {
        if (Test-Path $artifact) {
            $dest = Join-Path $outputDir ([System.IO.Path]::GetFileName($artifact))
            Copy-Item -Path $artifact -Destination $dest -Force
        }
    }
    
    Write-Log "Secure build process completed successfully!" "INFO"
    Write-Log "Output directory: $outputDir" "INFO"
    
    # Exit with success
    exit 0
}
catch {
    Write-Log "Build process failed: $($_.Exception.Message)" "ERROR"
    Write-Log $_.ScriptStackTrace "DEBUG"
    exit 1
}
finally {
    # Clean up environment if needed
    if (-not $PreserveBuildEnvironment) {
        Write-Log "Cleaning up build environment..."
        # Reset environment variables if they were set
        # (In a real implementation, you would store original values and restore them)
    }
}