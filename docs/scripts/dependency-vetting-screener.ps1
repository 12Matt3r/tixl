#!/usr/bin/env pwsh
<#
.SYNOPSIS
    TiXL Dependency Vetting Screener - Initial automated screening for dependency requests

.DESCRIPTION
    Performs initial automated screening of dependency requests to filter out
    obviously unsuitable packages before manual review begins.

.PARAMETER PackageName
    Name of the NuGet package to screen

.PARAMETER Version
    Version of the package to screen

.PARAMETER Source
    Source of the package (nuget, github, etc.)

.PARAMETER OutputPath
    Path to save screening results

.PARAMETER Verbose
    Enable verbose output

.EXAMPLE
    .\dependency-vetting-screener.ps1 -PackageName "Newtonsoft.Json" -Version "13.0.3" -Verbose

.EXAMPLE
    .\dependency-vetting-screener.ps1 -PackageName "Custom.Package" -Source "github" -OutputPath "./screening-results"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$PackageName,
    
    [Parameter(Mandatory=$false)]
    [string]$Version,
    
    [Parameter(Mandatory=$false)]
    [string]$Source = "nuget",
    
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "./dependency-vetting-screening",
    
    [Parameter(Mandatory=$false)]
    [switch]$Verbose
)

# Import required modules
Import-Module PowerShellGet -ErrorAction SilentlyContinue
Import-Module PowerShell-YAML -ErrorAction SilentlyContinue

# Load configuration
$configPath = Join-Path $PSScriptRoot "../config/dependency-vetting-config.json"
if (Test-Path $configPath) {
    $config = Get-Content $configPath | ConvertFrom-Json
} else {
    Write-Warning "Configuration file not found. Using default settings."
    $config = @{
        screening = @{
            minimumDownloads = 100
            maxAgeMonths = 24
            maxPackageSizeMB = 50
            requiredSemVer = $true
            requiredOfficialRepo = $true
        }
    }
}

# Initialize output directory
$outputDir = Join-Path (Resolve-Path $OutputPath) "screening-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

function Write-ScreeningLog {
    param([string]$Message, [string]$Level = "INFO")
    
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"
    
    switch ($Level) {
        "ERROR" { Write-Error $logMessage }
        "WARNING" { Write-Warning $logMessage }
        "INFO" { Write-Host $logMessage -ForegroundColor Green }
        "DEBUG" { if ($Verbose) { Write-Host $logMessage -ForegroundColor Cyan } }
    }
    
    # Append to log file
    $logFile = Join-Path $outputDir "screening.log"
    Add-Content -Path $logFile -Value $logMessage
}

function Test-PackageExists {
    param([string]$PackageName, [string]$Version, [string]$Source)
    
    try {
        switch ($Source.ToLower()) {
            "nuget" {
                $result = Find-Package -Name $PackageName -Source nuget.org -ErrorAction SilentlyContinue
                if ($Version) {
                    $result = $result | Where-Object { $_.Version.ToString() -eq $Version }
                }
                return $result.Count -gt 0
            }
            "github" {
                # Basic GitHub package existence check
                $apiUrl = "https://api.github.com/repos/$PackageName"
                $response = Invoke-RestMethod -Uri $apiUrl -ErrorAction SilentlyContinue
                return $response -ne $null
            }
            default {
                Write-ScreeningLog "Unsupported source: $Source" "WARNING"
                return $false
            }
        }
    }
    catch {
        Write-ScreeningLog "Error checking package existence: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

function Get-PackageDownloadStats {
    param([string]$PackageName, [string]$Source)
    
    try {
        if ($Source.ToLower() -eq "nuget") {
            # Use NuGet API to get package statistics
            $apiUrl = "https://api.nuget.org/v3-flatcontainer/$($PackageName.ToLower())/index.json"
            $response = Invoke-RestMethod -Uri $apiUrl -ErrorAction Stop
            
            if ($response.versions) {
                # Get the latest version info
                $latestVersion = $response.versions | Sort-Object -Descending | Select-Object -First 1
                $versionInfoUrl = "https://api.nuget.org/v3-flatcontainer/$($PackageName.ToLower())/$latestVersion/$($PackageName.ToLower()).nuspec"
                
                # Parse .nuspec for download count (approximate)
                $nuspec = Invoke-RestMethod -Uri $versionInfoUrl -ErrorAction SilentlyContinue
                return @{
                    Versions = $response.versions.Count
                    LatestVersion = $latestVersion
                    EstimatedDownloads = 1000 # Placeholder - would need actual API
                }
            }
        }
        return @{ Versions = 0; LatestVersion = $null; EstimatedDownloads = 0 }
    }
    catch {
        Write-ScreeningLog "Error getting download stats: $($_.Exception.Message)" "WARNING"
        return @{ Versions = 0; LatestVersion = $null; EstimatedDownloads = 0 }
    }
}

function Test-SemanticVersioning {
    param([string]$Version)
    
    if (-not $Version) {
        return $false
    }
    
    try {
        # Simple semantic versioning check
        $versionPattern = '^\d+\.\d+\.\d+(-[A-Za-z0-9\-\.]+)?$'
        return $Version -match $versionPattern
    }
    catch {
        return $false
    }
}

function Test-PackageRecency {
    param([string]$PackageName, [string]$Source)
    
    try {
        if ($Source.ToLower() -eq "nuget") {
            # Check when package was last updated
            $searchUrl = "https://api.nuget.org/v3-flatcontainer/$($PackageName.ToLower())/index.json"
            $response = Invoke-RestMethod -Uri $searchUrl -ErrorAction Stop
            
            if ($response.versions) {
                # Assume versions are listed in chronological order
                $latestVersion = $response.versions | Select-Object -Last 1
                
                # For now, return true - would need actual API to get update dates
                return $true
            }
        }
        return $false
    }
    catch {
        Write-ScreeningLog "Error checking package recency: $($_.Exception.Message)" "WARNING"
        return $false
    }
}

function Get-PackageSize {
    param([string]$PackageName, [string]$Version, [string]$Source)
    
    try {
        if ($Source.ToLower() -eq "nuget") {
            # Get package size from NuGet API
            $packageId = $PackageName.ToLower()
            $versionUrl = "https://api.nuget.org/v3-flatcontainer/$packageId/index.json"
            $response = Invoke-RestMethod -Uri $versionUrl -ErrorAction Stop
            
            if ($response.versions -and $Version) {
                $packageUrl = "https://api.nuget.org/v3-flatcontainer/$packageId/$Version/$packageId.$Version.nupkg.sha512"
                $headResponse = Invoke-WebRequest -Uri $packageUrl -Method Head -ErrorAction SilentlyContinue
                
                if ($headResponse.Headers['Content-Length']) {
                    return [math]::Round([int]$headResponse.Headers['Content-Length'] / 1MB, 2)
                }
            }
        }
        return 0
    }
    catch {
        Write-ScreeningLog "Error getting package size: $($_.Exception.Message)" "WARNING"
        return 0
    }
}

function Test-DependencyConflicts {
    param([string]$PackageName, [string]$Source)
    
    try {
        # Check if package conflicts with existing dependencies
        # This would integrate with the project's current dependencies
        
        $solutionPath = Get-Location
        $csprojFiles = Get-ChildItem -Path $solutionPath -Filter "*.csproj" -Recurse
        
        foreach ($csproj in $csprojFiles) {
            [xml]$csprojContent = Get-Content $csproj.FullName
            
            # Check if package already exists
            $existingPackage = $csprojContent.Project.ItemGroup.PackageReference | 
                              Where-Object { $_.Include -eq $PackageName }
            
            if ($existingPackage) {
                return @{
                    HasConflict = $true
                    ExistingVersion = $existingPackage.Version
                    Location = $csproj.FullName
                }
            }
        }
        
        return @{
            HasConflict = $false
            ExistingVersion = $null
            Location = $null
        }
    }
    catch {
        Write-ScreeningLog "Error checking dependencies: $($_.Exception.Message)" "WARNING"
        return @{ HasConflict = $false; ExistingVersion = $null; Location = $null }
    }
}

# Main screening process
Write-ScreeningLog "Starting dependency vetting screening for: $PackageName" "INFO"

# Initialize results object
$screeningResults = @{
    packageName = $PackageName
    version = $Version
    source = $Source
    screeningDate = Get-Date
    checks = @{}
    overallStatus = "PENDING"
    score = 0
    issues = @()
    recommendations = @()
}

# Run all screening checks
Write-ScreeningLog "Running package existence check..." "DEBUG"
$packageExists = Test-PackageExists -PackageName $PackageName -Version $Version -Source $Source
$screeningResults.checks.packageExists = @{
    passed = $packageExists
    required = $config.screening.requiredOfficialRepo
    score = if ($packageExists) { 100 } else { 0 }
}
Write-ScreeningLog "Package exists: $packageExists" "INFO"

Write-ScreeningLog "Checking download statistics..." "DEBUG"
$downloadStats = Get-PackageDownloadStats -PackageName $PackageName -Source $Source
$screeningResults.checks.downloadThreshold = @{
    downloads = $downloadStats.EstimatedDownloads
    required = $config.screening.minimumDownloads
    passed = $downloadStats.EstimatedDownloads -ge $config.screening.minimumDownloads
    score = if ($downloadStats.EstimatedDownloads -ge $config.screening.minimumDownloads) { 100 } else { 0 }
}
Write-ScreeningLog "Download threshold met: $($downloadStats.EstimatedDownloads -ge $config.screening.minimumDownloads)" "INFO"

Write-ScreeningLog "Checking semantic versioning..." "DEBUG"
$isSemVer = Test-SemanticVersioning -Version $Version
$screeningResults.checks.semanticVersioning = @{
    passed = $isSemVer
    required = $config.screening.requiredSemVer
    score = if ($isSemVer) { 100 } else { 0 }
}
Write-ScreeningLog "Semantic versioning: $isSemVer" "INFO"

Write-ScreeningLog "Checking package recency..." "DEBUG"
$isRecent = Test-PackageRecency -PackageName $PackageName -Source $Source
$screeningResults.checks.packageRecency = @{
    passed = $isRecent
    required = $true
    score = if ($isRecent) { 100 } else { 0 }
}
Write-ScreeningLog "Package recency: $isRecent" "INFO"

Write-ScreeningLog "Checking package size..." "DEBUG"
$packageSize = Get-PackageSize -PackageName $PackageName -Version $Version -Source $Source
$screeningResults.checks.packageSize = @{
    sizeMB = $packageSize
    requiredMaxMB = $config.screening.maxPackageSizeMB
    passed = $packageSize -le $config.screening.maxPackageSizeMB
    score = if ($packageSize -le $config.screening.maxPackageSizeMB) { 100 } else { 50 }
}
Write-ScreeningLog "Package size: ${packageSize}MB (max: $($config.screening.maxPackageSizeMB)MB)" "INFO"

Write-ScreeningLog "Checking for dependency conflicts..." "DEBUG"
$conflicts = Test-DependencyConflicts -PackageName $PackageName -Source $Source
$screeningResults.checks.dependencyConflicts = @{
    hasConflicts = $conflicts.HasConflict
    conflicts = $conflicts
    passed = -not $conflicts.HasConflict
    score = if (-not $conflicts.HasConflict) { 100 } else { 0 }
}
Write-ScreeningLog "No conflicts: $(-not $conflicts.HasConflict)" "INFO"

# Calculate overall score and status
$checkScores = $screeningResults.checks.PSObject.Properties.Value | Where-Object { $_.score -ne $null } | Select-Object -ExpandProperty score
$screeningResults.score = [math]::Round(($checkScores | Measure-Object -Average).Average, 2)

# Determine overall status
$criticalChecks = @($screeningResults.checks.packageExists, $screeningResults.checks.semanticVersioning)
$nonCriticalChecks = @($screeningResults.checks.downloadThreshold, $screeningResults.checks.packageSize, $screeningResults.checks.dependencyConflicts)

$criticalPass = $criticalChecks.All { $_.passed }
$nonCriticalFail = $nonCriticalChecks.Where { -not $_.passed }.Count

if (-not $criticalPass) {
    $screeningResults.overallStatus = "FAILED"
    $screeningResults.issues += "Critical screening checks failed"
} elseif ($nonCriticalFail -gt 2) {
    $screeningResults.overallStatus = "FAILED"
    $screeningResults.issues += "Too many non-critical checks failed ($nonCriticalFail/3)"
} elseif ($screeningResults.score -lt 70) {
    $screeningResults.overallStatus = "FAILED"
    $screeningResults.issues += "Overall screening score too low ($($screeningResults.score)/100)"
} elseif ($nonCriticalFail -gt 0) {
    $screeningResults.overallStatus = "WARNING"
    $screeningResults.issues += "$nonCriticalFail non-critical checks failed"
    foreach ($check in $nonCriticalChecks.Where { -not $_.passed }) {
        if ($check -eq $screeningResults.checks.downloadThreshold) {
            $screeningResults.recommendations += "Consider more popular packages with higher download counts"
        }
        if ($check -eq $screeningResults.checks.packageSize) {
            $screeningResults.recommendations += "Large package size may impact application performance"
        }
        if ($check -eq $screeningResults.checks.dependencyConflicts) {
            $screeningResults.recommendations += "Resolve dependency conflicts before integration"
        }
    }
} else {
    $screeningResults.overallStatus = "PASSED"
    $screeningResults.recommendations += "Package passed initial screening"
}

# Generate summary
Write-ScreeningLog "=== SCREENING SUMMARY ===" "INFO"
Write-ScreeningLog "Package: $PackageName" "INFO"
Write-ScreeningLog "Version: $Version" "INFO"
Write-ScreeningLog "Source: $Source" "INFO"
Write-ScreeningLog "Overall Status: $($screeningResults.overallStatus)" "INFO"
Write-ScreeningLog "Score: $($screeningResults.score)/100" "INFO"

if ($screeningResults.issues.Count -gt 0) {
    Write-ScreeningLog "Issues Found:" "WARNING"
    foreach ($issue in $screeningResults.issues) {
        Write-ScreeningLog "- $issue" "WARNING"
    }
}

if ($screeningResults.recommendations.Count -gt 0) {
    Write-ScreeningLog "Recommendations:" "INFO"
    foreach ($rec in $screeningResults.recommendations) {
        Write-ScreeningLog "- $rec" "INFO"
    }
}

# Save detailed results
$resultsFile = Join-Path $outputDir "screening-results.json"
$screeningResults | ConvertTo-Json -Depth 10 | Out-File -FilePath $resultsFile -Encoding UTF8

# Generate markdown report
$reportContent = @"
# Dependency Vetting Screening Report

**Package**: $PackageName  
**Version**: $Version  
**Source**: $Source  
**Screening Date**: $($screeningResults.screeningDate)  
**Status**: $($screeningResults.overallStatus)  
**Score**: $($screeningResults.score)/100

## Screening Results

| Check | Status | Required | Score |
|-------|--------|----------|-------|
| Package Exists | $(if ($screeningResults.checks.packageExists.passed) { '✅ PASS' } else { '❌ FAIL' }) | Yes | $($screeningResults.checks.packageExists.score) |
| Download Threshold | $(if ($screeningResults.checks.downloadThreshold.passed) { '✅ PASS' } else { '❌ FAIL' }) | $(if ($config.screening.minimumDownloads) { 'Yes' } else { 'No' }) | $($screeningResults.checks.downloadThreshold.score) |
| Semantic Versioning | $(if ($screeningResults.checks.semanticVersioning.passed) { '✅ PASS' } else { '❌ FAIL' }) | Yes | $($screeningResults.checks.semanticVersioning.score) |
| Package Recency | $(if ($screeningResults.checks.packageRecency.passed) { '✅ PASS' } else { '⚠️ UNKNOWN' }) | Yes | $($screeningResults.checks.packageRecency.score) |
| Package Size | $(if ($screeningResults.checks.packageSize.passed) { '✅ PASS' } else { '⚠️ WARNING' }) | < $($config.screening.maxPackageSizeMB)MB | $($screeningResults.checks.packageSize.score) |
| No Conflicts | $(if ($screeningResults.checks.dependencyConflicts.passed) { '✅ PASS' } else { '⚠️ CONFLICT' }) | Yes | $($screeningResults.checks.dependencyConflicts.score) |

## Issues

$(
    if ($screeningResults.issues.Count -gt 0) {
        foreach ($issue in $screeningResults.issues) {
            "- $issue"
        }
    } else {
        "No issues found."
    }
)

## Recommendations

$(
    if ($screeningResults.recommendations.Count -gt 0) {
        foreach ($rec in $screeningResults.recommendations) {
            "- $rec"
        }
    } else {
        "No specific recommendations."
    }
)

## Next Steps

$(
    switch ($screeningResults.overallStatus) {
        "PASSED" { "Proceed to security assessment stage." }
        "WARNING" { "Address warnings and proceed to security assessment." }
        "FAILED" { "Package failed initial screening. Consider alternatives." }
    }
)

---
Generated by TiXL Dependency Vetting Screener v1.0
"@

$reportFile = Join-Path $outputDir "screening-report.md"
$reportContent | Out-File -FilePath $reportFile -Encoding UTF8

Write-ScreeningLog "Screening complete. Results saved to: $outputDir" "INFO"

# Return results
$screeningResults
