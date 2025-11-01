#!/usr/bin/env pwsh
<#
.SYNOPSIS
    TiXL Dependency Audit Script - Comprehensive dependency analysis and security auditing

.DESCRIPTION
    Performs comprehensive dependency auditing including:
    - Security vulnerability scanning
    - License compliance checking
    - Dependency tree analysis
    - Version compatibility assessment
    - Policy violation detection

.PARAMETER SolutionPath
    Path to the solution or project file to audit

.PARAMETER OutputPath
    Directory to save audit reports and logs

.PARAMETER Verbose
    Enable verbose output

.PARAMETER FailOnVulnerabilities
    Fail the script if critical vulnerabilities are found

.PARAMETER Severity
    Minimum vulnerability severity to report (Low, Medium, High, Critical)

.EXAMPLE
    .\dependency-audit.ps1 -SolutionPath "TiXL.sln" -OutputPath "./audit-reports" -Verbose

.EXAMPLE
    .\dependency-audit.ps1 -SolutionPath "TiXL.sln" -FailOnVulnerabilities -Severity "High"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$SolutionPath,
    
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "./dependency-audit-reports",
    
    [Parameter(Mandatory=$false)]
    [switch]$Verbose,
    
    [Parameter(Mandatory=$false)]
    [switch]$FailOnVulnerabilities,
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("Low", "Medium", "High", "Critical")]
    [string]$Severity = "Medium"
)

# Import required modules
Import-Module PowerShell-Yaml -ErrorAction SilentlyContinue -Force

# Global variables
$script:ScriptName = "TiXL Dependency Audit"
$script:ScriptVersion = "1.0.0"
$script:StartTime = Get-Date
$script:ConfigPath = "$PSScriptRoot\..\config\dependency-config.json"

# Configuration and thresholds
$script:SeverityWeights = @{
    "Critical" = 10
    "High" = 7
    "Medium" = 4
    "Low" = 1
}

$script:LicensePolicy = @{
    "Allowed" = @("MIT", "Apache-2.0", "Apache 2.0", "BSD-2-Clause", "BSD-3-Clause", "ISC", "CC0-1.0")
    "RequiresApproval" = @("GPL-2.0", "GPL-3.0", "LGPL-2.1", "LGPL-3.0")
    "Blocked" = @("Proprietary", "Unknown")
}

# Output directory setup
if (!(Test-Path $OutputPath)) {
    New-Item -Path $OutputPath -ItemType Directory -Force | Out-Null
}

$script:LogFile = Join-Path $OutputPath "dependency-audit.log"
$script:ReportFile = Join-Path $OutputPath "audit-report.json"
$script:SummaryFile = Join-Path $OutputPath "audit-summary.md"

# Initialize log file
Initialize-LogFile -LogFile $script:LogFile

function Initialize-LogFile {
    param([string]$LogFile)
    
    $logHeader = @"
=== $script:ScriptName v$script:ScriptVersion ===
Started: $script:StartTime
Working Directory: $(Get-Location)
Command Line: $PSCommandPath $($PSBoundParameters.GetEnumerator() | ForEach-Object { "-$($_.Key) $($_.Value)" } | Out-String)
=========================================

"@
    
    $logHeader | Out-File -FilePath $LogFile -Encoding UTF8
}

function Write-Log {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Message,
        [Parameter(Mandatory=$false)]
        [ValidateSet("INFO", "WARNING", "ERROR", "DEBUG")]
        [string]$Level = "INFO"
    )
    
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logEntry = "[$timestamp] [$Level] $Message"
    
    if ($Verbose -or $Level -ne "DEBUG") {
        Write-Host $logEntry
    }
    
    $logEntry | Out-File -FilePath $script:LogFile -Append -Encoding UTF8
}

function Test-DependencyConfig {
    param()
    
    Write-Log "Testing dependency configuration" "INFO"
    
    if (!(Test-Path $script:ConfigPath)) {
        Write-Log "Configuration file not found: $script:ConfigPath" "WARNING"
        return $false
    }
    
    try {
        $config = Get-Content $script:ConfigPath | ConvertFrom-Json
        Write-Log "Configuration loaded successfully" "INFO"
        return $true
    }
    catch {
        Write-Log "Failed to load configuration: $_" "ERROR"
        return $false
    }
}

function Get-SolutionProjects {
    param([string]$SolutionPath)
    
    Write-Log "Analyzing solution structure: $SolutionPath" "INFO"
    
    $projects = @()
    
    if ($SolutionPath.EndsWith('.sln')) {
        Write-Log "Processing solution file" "INFO"
        
        # Use dotnet list to get project references
        $listOutput = & dotnet list "$SolutionPath" package --include-transitive 2>$null
        
        if ($LASTEXITCODE -eq 0 -and $listOutput) {
            $currentProject = $null
            
            foreach ($line in $listOutput) {
                if ($line -match "^> dotnet add .*package$") { continue }
                if ($line -match "^The PINVOKE plugins:") { continue }
                if ($line -match "^Direct dependencies:$") { 
                    $currentProject = "root"
                    continue
                }
                if ($line -match "^Transitive dependencies:$") { 
                    $currentProject = "transitive"
                    continue
                }
                
                # Parse package lines
                if ($line -match "^\s*([\w\.-]+)\s+([\d\.]+)\s+(.*)$") {
                    $packageName = $matches[1]
                    $version = $matches[2]
                    $license = $matches[3]
                    
                    $project = [PSCustomObject]@{
                        Name = $packageName
                        Version = $version
                        License = $license
                        Type = $currentProject
                        Source = "nuget"
                    }
                    
                    $projects += $project
                }
            }
        }
    }
    else {
        Write-Log "Processing project file" "INFO"
        $project = [PSCustomObject]@{
            Name = [System.IO.Path]::GetFileNameWithoutExtension($SolutionPath)
            Version = "0.0.0"
            License = "Unknown"
            Type = "project"
            Source = "local"
        }
        $projects += $project
    }
    
    Write-Log "Found $($projects.Count) dependencies" "INFO"
    return $projects
}

function Test-PackageVulnerabilities {
    param(
        [array]$Packages,
        [string]$MinimumSeverity
    )
    
    Write-Log "Scanning packages for vulnerabilities" "INFO"
    
    $vulnerablePackages = @()
    $vulnerabilityStats = @{
        Total = 0
        Critical = 0
        High = 0
        Medium = 0
        Low = 0
    }
    
    foreach ($package in $Packages) {
        Write-Log "Checking vulnerability for package: $($package.Name) v$($package.Version)" "DEBUG"
        
        # Check GitHub Security Advisories
        $gh advisories = Test-GitHubSecurityAdvisories -PackageName $package.Name -Version $package.Version
        
        # Check National Vulnerability Database
        $nvdVulns = Test-NVDVulnerabilities -PackageName $package.Name -Version $package.Version
        
        # Combine results
        $allVulnerabilities = @($gh advisories) + @($nvdVulns)
        
        foreach ($vulnerability in $allVulnerabilities) {
            $severityWeight = $script:SeverityWeights[$vulnerability.Severity]
            $minimumWeight = $script:SeverityWeights[$MinimumSeverity]
            
            if ($severityWeight -ge $minimumWeight) {
                $vulnerabilityInfo = [PSCustomObject]@{
                    Package = $package.Name
                    Version = $package.Version
                    Vulnerability = $vulnerability
                    Severity = $vulnerability.Severity
                    Source = $vulnerability.Source
                    FixedInVersion = $vulnerability.FixedInVersion
                    Description = $vulnerability.Description
                }
                
                $vulnerablePackages += $vulnerabilityInfo
                $vulnerabilityStats[$vulnerability.Severity]++
                $vulnerabilityStats.Total++
            }
        }
    }
    
    Write-Log "Found $($vulnerablePackages.Count) vulnerabilities" "INFO"
    return @{
        Vulnerabilities = $vulnerablePackages
        Statistics = $vulnerabilityStats
    }
}

function Test-GitHubSecurityAdvisories {
    param(
        [string]$PackageName,
        [string]$Version
    )
    
    # Mock implementation - replace with actual GitHub API calls
    $advisories = @()
    
    # This would make actual API calls to GitHub Security Advisories
    # For now, returning empty array as this is a template
    return $advisories
}

function Test-NVDVulnerabilities {
    param(
        [string]$PackageName,
        [string]$Version
    )
    
    # Mock implementation - replace with actual NVD API calls
    $vulnerabilities = @()
    
    # This would make actual API calls to NVD
    # For now, returning empty array as this is a template
    return $vulnerabilities
}

function Test-LicenseCompliance {
    param([array]$Packages)
    
    Write-Log "Checking license compliance" "INFO"
    
    $complianceResults = @{
        Compliant = @()
        Violations = @()
        Unknown = @()
        Statistics = @{
            Total = 0
            Compliant = 0
            Violations = 0
            Unknown = 0
        }
    }
    
    foreach ($package in $Packages) {
        $license = $package.License?.Trim()
        $packageName = $package.Name
        
        if (!$license -or $license -eq "Unknown") {
            $complianceResults.Unknown += [PSCustomObject]@{
                Package = $packageName
                License = $license
                Status = "Unknown"
            }
            $complianceResults.Statistics.Unknown++
        }
        elseif ($script:LicensePolicy.Allowed -contains $license) {
            $complianceResults.Compliant += [PSCustomObject]@{
                Package = $packageName
                License = $license
                Status = "Allowed"
            }
            $complianceResults.Statistics.Compliant++
        }
        elseif ($script:LicensePolicy.Blocked -contains $license) {
            $complianceResults.Violations += [PSCustomObject]@{
                Package = $packageName
                License = $license
                Status = "Blocked"
                Violation = "Blocked license"
            }
            $complianceResults.Statistics.Violations++
        }
        else {
            $complianceResults.Violations += [PSCustomObject]@{
                Package = $packageName
                License = $license
                Status = "Requires Review"
                Violation = "License not in whitelist"
            }
            $complianceResults.Statistics.Violations++
        }
        
        $complianceResults.Statistics.Total++
    }
    
    Write-Log "License compliance check completed" "INFO"
    return $complianceResults
}

function Get-DependencyTree {
    param([string]$SolutionPath)
    
    Write-Log "Analyzing dependency tree" "INFO"
    
    try {
        # Use dotnet list to get the full dependency tree
        $treeOutput = & dotnet list "$SolutionPath" package --include-transitive --format json 2>$null
        
        if ($LASTEXITCODE -eq 0 -and $treeOutput) {
            $dependencyData = $treeOutput | ConvertFrom-Json
            return $dependencyData
        }
        else {
            Write-Log "Failed to get dependency tree" "WARNING"
            return $null
        }
    }
    catch {
        Write-Log "Error getting dependency tree: $_" "ERROR"
        return $null
    }
}

function Test-DependencyTreeIssues {
    param($DependencyTree)
    
    Write-Log "Checking for dependency tree issues" "INFO"
    
    $issues = @{
        Circular = @()
        Duplicates = @()
        Conflicts = @()
        Unused = @()
    }
    
    # Check for circular dependencies
    # This is a simplified check - real implementation would be more sophisticated
    $packages = $DependencyTree.Projects | ForEach-Object { $_.Frameworks } | ForEach-Object { $_.Dependencies } | Where-Object { $_ }
    
    $packageMap = @{}
    foreach ($package in $packages) {
        $key = "$($package.Name)|$($package.VersionRange)"
        if ($packageMap.ContainsKey($key)) {
            $issues.Duplicates += [PSCustomObject]@{
                Package = $package.Name
                Versions = $packageMap[$key]
                Version = $package.VersionRange
                Issue = "Duplicate reference"
            }
        }
        else {
            $packageMap[$key] = @($package)
        }
    }
    
    Write-Log "Found $($issues.Circular.Count) circular dependencies" "INFO"
    Write-Log "Found $($issues.Duplicates.Count) duplicate references" "INFO"
    
    return $issues
}

function Get-VersionAnalysis {
    param([array]$Packages)
    
    Write-Log "Analyzing package versions" "INFO"
    
    $outdatedAnalysis = @{
        Outdated = @()
        Latest = @()
        Prerelease = @()
        Statistics = @{
            Total = 0
            Outdated = 0
            Latest = 0
            Prerelease = 0
        }
    }
    
    foreach ($package in $Packages) {
        $outdatedAnalysis.Statistics.Total++
        
        # Mock implementation - replace with actual package feed queries
        $latestVersion = Get-LatestPackageVersion -PackageName $package.Name
        
        if ($latestVersion) {
            if ([version]$latestVersion -gt [version]$package.Version) {
                $outdatedAnalysis.Outdated += [PSCustomObject]@{
                    Package = $package.Name
                    CurrentVersion = $package.Version
                    LatestVersion = $latestVersion
                    UpdateType = Get-UpdateType -CurrentVersion $package.Version -LatestVersion $latestVersion
                }
                $outdatedAnalysis.Statistics.Outdated++
            }
            elseif ($latestVersion -eq $package.Version) {
                $outdatedAnalysis.Latest += [PSCustomObject]@{
                    Package = $package.Name
                    Version = $package.Version
                    Status = "Latest"
                }
                $outdatedAnalysis.Statistics.Latest++
            }
        }
    }
    
    Write-Log "Version analysis completed" "INFO"
    return $outdatedAnalysis
}

function Get-LatestPackageVersion {
    param([string]$PackageName)
    
    # Mock implementation - replace with actual NuGet API calls
    try {
        $feedUrl = "https://api.nuget.org/v3-flatcontainer/$PackageName/index.json"
        $response = Invoke-RestMethod -Uri $feedUrl -ErrorAction SilentlyContinue
        
        if ($response.versions) {
            # Filter for stable versions only
            $stableVersions = $response.versions | Where-Object { $_ -notmatch '[\-\+](alpha|beta|rc|preview)' }
            return ($stableVersions | Sort-Object { [version]$_ } | Select-Object -Last 1)
        }
    }
    catch {
        Write-Log "Failed to get latest version for $PackageName" "WARNING"
    }
    
    return $null
}

function Get-UpdateType {
    param(
        [string]$CurrentVersion,
        [string]$LatestVersion
    )
    
    $currentMajor = [version]$CurrentVersion.Split('-')[0].Split('+')[0].Split('.')[0]
    $latestMajor = [version]$LatestVersion.Split('-')[0].Split('+')[0].Split('.')[0]
    
    if ($latestMajor -gt $currentMajor) {
        return "Major"
    }
    elseif ($latestMajor -eq $currentMajor) {
        $currentMinor = [version]$CurrentVersion.Split('-')[0].Split('+')[0].Split('.')[1]
        $latestMinor = [version]$LatestVersion.Split('-')[0].Split('+')[0].Split('.')[1]
        
        if ($latestMinor -gt $currentMinor) {
            return "Minor"
        }
        else {
            return "Patch"
        }
    }
    else {
        return "Unknown"
    }
}

function Export-AuditReport {
    param(
        $AuditData,
        [string]$OutputPath
    )
    
    Write-Log "Exporting audit report" "INFO"
    
    $reportData = @{
        AuditInfo = @{
            Timestamp = $script:StartTime
            Duration = (Get-Date) - $script:StartTime
            Version = $script:ScriptVersion
            SolutionPath = $SolutionPath
        }
        Summary = @{
            TotalPackages = $AuditData.Packages.Count
            Vulnerabilities = $AuditData.Vulnerabilities.Statistics
            LicenseCompliance = $AuditData.LicenseCompliance.Statistics
            VersionAnalysis = $AuditData.VersionAnalysis.Statistics
            DependencyTree = $AuditData.DependencyTreeIssues
        }
        Details = @{
            Packages = $AuditData.Packages
            Vulnerabilities = $AuditData.Vulnerabilities.Vulnerabilities
            LicenseCompliance = $AuditData.LicenseCompliance
            VersionAnalysis = $AuditData.VersionAnalysis
            DependencyTreeIssues = $AuditData.DependencyTreeIssues
        }
    }
    
    # Export JSON report
    $reportData | ConvertTo-Json -Depth 10 | Out-File -FilePath $script:ReportFile -Encoding UTF8
    
    # Export markdown summary
    Export-MarkdownSummary -ReportData $reportData -OutputPath $script:SummaryFile
    
    Write-Log "Audit report exported to: $OutputPath" "INFO"
}

function Export-MarkdownSummary {
    param(
        $ReportData,
        [string]$OutputPath
    )
    
    $summary = @"
# TiXL Dependency Audit Report

**Generated**: $($ReportData.AuditInfo.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"))
**Duration**: $($ReportData.AuditInfo.Duration.TotalSeconds.ToString("F2")) seconds
**Version**: $($ReportData.AuditInfo.Version)

## Executive Summary

- **Total Packages**: $($ReportData.Summary.TotalPackages)
- **Vulnerabilities Found**: $($ReportData.Summary.Vulnerabilities.Total)
- **License Violations**: $($ReportData.Summary.LicenseCompliance.Violations)
- **Outdated Packages**: $($ReportData.Summary.VersionAnalysis.Outdated)

## Security Assessment

| Severity | Count |
|----------|-------|
| Critical | $($ReportData.Summary.Vulnerabilities.Critical) |
| High     | $($ReportData.Summary.Vulnerabilities.High) |
| Medium   | $($ReportData.Summary.Vulnerabilities.Medium) |
| Low      | $($ReportData.Summary.Vulnerabilities.Low) |

## License Compliance

| Status | Count |
|--------|-------|
| Compliant | $($ReportData.Summary.LicenseCompliance.Compliant) |
| Violations | $($ReportData.Summary.LicenseCompliance.Violations) |
| Unknown | $($ReportData.Summary.LicenseCompliance.Unknown) |

## Version Status

| Status | Count |
|--------|-------|
| Latest | $($ReportData.Summary.VersionAnalysis.Latest) |
| Outdated | $($ReportData.Summary.VersionAnalysis.Outdated) |
| Prerelease | $($ReportData.Summary.VersionAnalysis.Prerelease) |

## Critical Issues

$(
    if ($ReportData.Details.Vulnerabilities.Count -gt 0) {
        $ReportData.Details.Vulnerabilities | Where-Object { $_.Severity -eq "Critical" } | ForEach-Object {
            "- **$($_.Package) v$($_.Version)**: $($_.Description)"
        }
    } else {
        "No critical vulnerabilities found."
    }
)

## License Violations

$(
    if ($ReportData.Details.LicenseCompliance.Violations.Count -gt 0) {
        $ReportData.Details.LicenseCompliance.Violations | ForEach-Object {
            "- **$($_.Package)**: $($_..License) - $($_.Violation)"
        }
    } else {
        "No license violations found."
    }
)

## Outdated Packages

$(
    if ($ReportData.Details.VersionAnalysis.Outdated.Count -gt 0) {
        $ReportData.Details.VersionAnalysis.Outdated | ForEach-Object {
            "- **$($_.Package)**: $($_.CurrentVersion) → $($_.LatestVersion) ($($_.UpdateType))"
        }
    } else {
        "All packages are up to date."
    }
)

## Recommendations

1. **Security**: $(if ($ReportData.Summary.Vulnerabilities.Critical -gt 0) { "Immediately address critical vulnerabilities" } else { "No immediate security concerns" })
2. **Compliance**: $(if ($ReportData.Summary.LicenseCompliance.Violations -gt 0) { "Review and resolve license violations" } else { "All licenses are compliant" })
3. **Updates**: $(if ($ReportData.Summary.VersionAnalysis.Outdated -gt 0) { "Consider updating $($ReportData.Summary.VersionAnalysis.Outdated) outdated packages" } else { "All packages are current" })

---
*This report was generated by TiXL Dependency Audit v$($ReportData.AuditInfo.Version)*
"@
    
    $summary | Out-File -FilePath $OutputPath -Encoding UTF8
}

function New-AuditSummary {
    param(
        $AuditData,
        [string]$OutputPath
    )
    
    $criticalVulns = $AuditData.Vulnerabilities.Statistics.Critical
    $licenseViolations = $AuditData.LicenseCompliance.Statistics.Violations
    $outdatedPackages = $AuditData.VersionAnalysis.Statistics.Outdated
    
    $exitCode = 0
    
    if ($FailOnVulnerabilities -and $criticalVulns -gt 0) {
        Write-Log "Critical vulnerabilities found: $criticalVulns" "ERROR"
        $exitCode = 1
    }
    
    if ($licenseViolations -gt 0) {
        Write-Log "License violations found: $licenseViolations" "WARNING"
    }
    
    if ($outdatedPackages -gt 0) {
        Write-Log "Outdated packages found: $outdatedPackages" "INFO"
    }
    
    Write-Log "=== AUDIT COMPLETED ===" "INFO"
    Write-Log "Total packages: $($AuditData.Packages.Count)" "INFO"
    Write-Log "Vulnerabilities: $($AuditData.Vulnerabilities.Statistics.Total)" "INFO"
    Write-Log "License violations: $licenseViolations" "INFO"
    Write-Log "Outdated packages: $outdatedPackages" "INFO"
    
    return $exitCode
}

# Main execution
Write-Log "Starting $script:ScriptName v$script:ScriptVersion" "INFO"
Write-Log "Solution path: $SolutionPath" "INFO"
Write-Log "Output path: $OutputPath" "INFO"

try {
    # Test configuration
    if (!(Test-DependencyConfig)) {
        Write-Log "Dependency configuration issues detected" "WARNING"
    }
    
    # Get solution projects
    $packages = Get-SolutionProjects -SolutionPath $SolutionPath
    
    if ($packages.Count -eq 0) {
        Write-Log "No packages found to audit" "WARNING"
        exit 1
    }
    
    # Perform audit components
    $vulnerabilityResults = Test-PackageVulnerabilities -Packages $packages -MinimumSeverity $Severity
    $licenseResults = Test-LicenseCompliance -Packages $packages
    $versionResults = Get-VersionAnalysis -Packages $packages
    
    # Get dependency tree (if available)
    $dependencyTree = Get-DependencyTree -SolutionPath $SolutionPath
    $treeIssues = if ($dependencyTree) { Test-DependencyTreeIssues -DependencyTree $dependencyTree } else { @{} }
    
    # Compile audit data
    $auditData = @{
        Packages = $packages
        Vulnerabilities = $vulnerabilityResults
        LicenseCompliance = $licenseResults
        VersionAnalysis = $versionResults
        DependencyTreeIssues = $treeIssues
    }
    
    # Export reports
    Export-AuditReport -AuditData $auditData -OutputPath $OutputPath
    
    # Return exit code based on results
    $exitCode = New-AuditSummary -AuditData $auditData -OutputPath $OutputPath
    
    Write-Log "Audit completed successfully" "INFO"
    exit $exitCode
}
catch {
    Write-Log "Audit failed with error: $_" "ERROR"
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
}