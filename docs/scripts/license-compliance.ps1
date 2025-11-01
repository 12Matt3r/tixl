#!/usr/bin/env pwsh
<#
.SYNOPSIS
    TiXL License Compliance Checker - Ensures all dependencies comply with license policies

.DESCRIPTION
    Checks all NuGet packages for license compliance including:
    - Automated license detection from NuGet metadata
    - Policy enforcement against whitelist/blacklist
    - License compatibility analysis
    - Compliance reporting and violations tracking

.PARAMETER ProjectPath
    Path to the solution or project file to check

.PARAMETER OutputPath
    Directory to save compliance reports

.PARAMETER FailOnViolation
    Exit with error code if license violations are found

.PARAMETER GenerateReport
    Generate comprehensive compliance report

.PARAMETER StrictMode
    Enable strict license checking (fewer allowed licenses)

.EXAMPLE
    .\license-compliance.ps1 -ProjectPath "TiXL.sln" -GenerateReport

.EXAMPLE
    .\license-compliance.ps1 -ProjectPath "TiXL.sln" -FailOnViolation -StrictMode
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$ProjectPath,
    
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "./license-compliance-reports",
    
    [Parameter(Mandatory=$false)]
    [switch]$FailOnViolation,
    
    [Parameter(Mandatory=$false)]
    [switch]$GenerateReport,
    
    [Parameter(Mandatory=$false)]
    [switch]$StrictMode
)

# Global variables
$script:ScriptName = "TiXL License Compliance Checker"
$script:ScriptVersion = "1.0.0"
$script:StartTime = Get-Date
$script:ConfigPath = "$PSScriptRoot\..\config\license-whitelist.json"

# Initialize output directory
if (!(Test-Path $OutputPath)) {
    New-Item -Path $OutputPath -ItemType Directory -Force | Out-Null
}

$script:LogFile = Join-Path $OutputPath "license-compliance.log"
$script:ReportFile = Join-Path $OutputPath "compliance-report.json"
$script:SummaryFile = Join-Path $OutputPath "compliance-summary.md"

# Default license policies
$script:DefaultLicensePolicy = @{
    Allowed = @(
        "MIT", "MIT License", "MIT*"
        "Apache-2.0", "Apache 2.0", "Apache 2.0 License", "Apache-2.0*"
        "BSD-2-Clause", "BSD 2-Clause License", "BSD-2-Clause*"
        "BSD-3-Clause", "BSD 3-Clause License", "BSD-3-Clause*"
        "ISC", "ISC License", "ISC*"
        "CC0-1.0", "CC0 1.0 Universal", "CC0*"
        "Unlicense", "The Unlicense", "Unlicense*"
    )
    RequiresApproval = @(
        "GPL-2.0", "GPL 2.0", "GPL-2.0*"
        "GPL-3.0", "GPL 3.0", "GPL-3.0*"
        "LGPL-2.1", "LGPL 2.1", "LGPL-2.1*"
        "LGPL-3.0", "LGPL 3.0", "LGPL-3.0*"
        "MPL-2.0", "MPL 2.0", "MPL-2.0*"
        "EPL-2.0", "EPL 2.0", "EPL-2.0*"
    )
    Blocked = @(
        "Proprietary", "Commercial", "All Rights Reserved"
        "Unknown", "Unspecified", "Custom"
    )
}

# Strict mode policy (more restrictive)
$script:StrictLicensePolicy = @{
    Allowed = @(
        "MIT", "MIT License", "MIT*"
        "Apache-2.0", "Apache 2.0", "Apache-2.0*"
        "BSD-2-Clause", "BSD-3-Clause", "ISC"
        "CC0-1.0", "Unlicense"
    )
    RequiresApproval = @()
    Blocked = @(
        "GPL-2.0", "GPL-3.0", "LGPL-2.1", "LGPL-3.0"
        "MPL-2.0", "EPL-2.0", "Proprietary", "Commercial"
        "Unknown", "Unspecified", "Custom"
    )
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
    
    Write-Host $logEntry
    $logEntry | Out-File -FilePath $script:LogFile -Append -Encoding UTF8
}

function Initialize-LicensePolicy {
    param()
    
    Write-Log "Initializing license policy" "INFO"
    
    $policy = $script:DefaultLicensePolicy
    
    # Override with custom policy if available
    if (Test-Path $script:ConfigPath) {
        try {
            $customPolicy = Get-Content $script:ConfigPath | ConvertFrom-Json
            
            if ($customPolicy.allowed) {
                $policy.Allowed = $customPolicy.allowed
            }
            if ($customPolicy.requiresApproval) {
                $policy.RequiresApproval = $customPolicy.requiresApproval
            }
            if ($customPolicy.blocked) {
                $policy.Blocked = $customPolicy.blocked
            }
            
            Write-Log "Loaded custom license policy from configuration" "INFO"
        }
        catch {
            Write-Log "Failed to load custom license policy: $_" "WARNING"
            Write-Log "Using default policy" "INFO"
        }
    }
    
    # Apply strict mode if requested
    if ($StrictMode) {
        $policy = $script:StrictLicensePolicy
        Write-Log "Applied strict mode policy" "INFO"
    }
    
    Write-Log "License policy initialized" "INFO"
    Write-Log "Allowed licenses: $($policy.Allowed.Count)" "DEBUG"
    Write-Log "Requires approval: $($policy.RequiresApproval.Count)" "DEBUG"
    Write-Log "Blocked licenses: $($policy.Blocked.Count)" "DEBUG"
    
    return $policy
}

function Get-PackageLicenses {
    param([string]$ProjectPath)
    
    Write-Log "Extracting package licenses from $ProjectPath" "INFO"
    
    $packages = @()
    
    try {
        if ($ProjectPath.EndsWith('.sln')) {
            # Process solution file
            $projects = & dotnet sln "$ProjectPath" list 2>$null
            foreach ($projectPath in $projects) {
                if ($projectPath -and (Test-Path $projectPath)) {
                    $packages += Get-PackageLicensesFromProject -ProjectPath $projectPath
                }
            }
        }
        else {
            # Process single project file
            $packages = Get-PackageLicensesFromProject -ProjectPath $ProjectPath
        }
    }
    catch {
        Write-Log "Error processing project structure: $_" "ERROR"
    }
    
    Write-Log "Found $($packages.Count) packages with licenses" "INFO"
    return $packages
}

function Get-PackageLicensesFromProject {
    param([string]$ProjectPath)
    
    $packages = @()
    
    try {
        # Use dotnet list to get packages with licenses
        $listOutput = & dotnet list "$ProjectPath" package --include-transitive 2>$null
        
        if ($LASTEXITCODE -eq 0 -and $listOutput) {
            $projectName = [System.IO.Path]::GetFileNameWithoutExtension($ProjectPath)
            $currentSection = $null
            
            foreach ($line in $listOutput) {
                # Skip header lines
                if ($line -match "^\s*$" -or $line -match "^The PINVOKE plugins:" -or $line -match "^> dotnet add") { continue }
                
                # Track sections
                if ($line -match "Direct dependencies:") { 
                    $currentSection = "Direct"
                    continue
                }
                if ($line -match "Transitive dependencies:") { 
                    $currentSection = "Transitive"
                    continue
                }
                
                # Parse package lines: PackageName Version LicenseInfo
                if ($line -match "^\s*([\w\.-]+)\s+([\d\.]+)\s*(.*)$") {
                    $packageName = $matches[1]
                    $version = $matches[2]
                    $licenseInfo = $matches[3].Trim()
                    
                    # Skip if this looks like a header or system package
                    if ($packageName -eq "Type" -or $packageName -eq "Version") { continue }
                    
                    # Get detailed license information
                    $licenseDetails = Get-PackageLicenseDetails -PackageName $packageName -Version $version
                    
                    $package = [PSCustomObject]@{
                        Name = $packageName
                        Version = $version
                        Project = $projectName
                        ProjectPath = $ProjectPath
                        SourceType = $currentSection
                        License = $licenseDetails.License
                        LicenseExpression = $licenseDetails.LicenseExpression
                        LicenseUrl = $licenseDetails.LicenseUrl
                        Authors = $licenseDetails.Authors
                        Description = $licenseDetails.Description
                        LicenseSource = $licenseDetails.Source
                        MetadataComplete = $licenseDetails.MetadataComplete
                    }
                    
                    $packages += $package
                }
            }
        }
        else {
            Write-Log "Failed to get packages from $ProjectPath" "WARNING"
        }
    }
    catch {
        Write-Log "Error reading packages from $ProjectPath : $_" "ERROR"
    }
    
    return $packages
}

function Get-PackageLicenseDetails {
    param(
        [string]$PackageName,
        [string]$Version
    )
    
    $details = @{
        License = "Unknown"
        LicenseExpression = $null
        LicenseUrl = $null
        Authors = @()
        Description = $null
        Source = "nuget-api"
        MetadataComplete = $false
    }
    
    try {
        # Query NuGet API for detailed package information
        $apiUrl = "https://api.nuget.org/v3-flatcontainer/$($PackageName.ToLower())/$Version/$($PackageName.ToLower()).nuspec"
        
        $response = Invoke-RestMethod -Uri $apiUrl -ErrorAction SilentlyContinue
        
        if ($response) {
            # Parse license information from nuspec
            if ($response.metadata.license) {
                $details.License = $response.metadata.license
                $details.LicenseExpression = $response.metadata.license
                $details.MetadataComplete = $true
            }
            elseif ($response.metadata.licenseUrl) {
                $details.LicenseUrl = $response.metadata.licenseUrl
                $details.License = Get-LicenseNameFromUrl $response.metadata.licenseUrl
                $details.MetadataComplete = $true
            }
            
            # Get other metadata
            if ($response.metadata.authors) {
                $details.Authors = @($response.metadata.authors -split ",\s*")
            }
            
            if ($response.metadata.description) {
                $details.Description = $response.metadata.description
            }
        }
    }
    catch {
        Write-Log "Failed to get license details for $PackageName v$Version : $_" "DEBUG"
    }
    
    # Fallback to common license patterns
    if ($details.License -eq "Unknown" -and !$details.LicenseUrl) {
        $commonLicense = Get-CommonLicensePattern -PackageName $packageName
        if ($commonLicense) {
            $details.License = $commonLicense
            $details.Source = "pattern-match"
        }
    }
    
    return $details
}

function Get-LicenseNameFromUrl {
    param([string]$LicenseUrl)
    
    switch ($LicenseUrl.ToLower()) {
        { $_ -match "mit\.org|opensource\.org/licenses/mit" } { return "MIT" }
        { $_ -match "apache\.org/licenses" } { return "Apache-2.0" }
        { $_ -match "creativecommons\.org/licenses" } { return "CC0-1.0" }
        { $_ -match "gnu\.org/licenses" -and $_ -match "lgpl" } { return "LGPL" }
        { $_ -match "gnu\.org/licenses" } { return "GPL" }
        { $_ -match "mozillafoundation\.org" } { return "MPL-2.0" }
        default { return "Unknown" }
    }
}

function Get-CommonLicensePattern {
    param([string]$PackageName)
    
    # Some packages have well-known licenses based on their name/author
    $knownPackages = @{
        "Microsoft.*" = "MIT"
        "System.*" = "MIT"
        "Newtonsoft.Json" = "MIT"
        " Newtonsoft.Json" = "MIT"
        "xunit" = "Apache-2.0"
        "NUnit" = "MIT"
        "Moq" = "BSD-3-Clause"
    }
    
    foreach ($pattern in $knownPackages.Keys) {
        if ($PackageName -match $pattern) {
            return $knownPackages[$pattern]
        }
    }
    
    return $null
}

function Test-LicenseCompliance {
    param(
        [array]$Packages,
        [hashtable]$Policy
    )
    
    Write-Log "Testing license compliance against policy" "INFO"
    
    $complianceResults = @{
        Packages = $Packages
        Compliant = @()
        Violations = @()
        Unknown = @()
        RequiresApproval = @()
        Statistics = @{
            Total = 0
            Compliant = 0
            Violations = 0
            Unknown = 0
            RequiresApproval = 0
        }
    }
    
    foreach ($package in $Packages) {
        $complianceResults.Statistics.Total++
        
        $license = Normalize-LicenseString $package.License
        $packageName = $package.Name
        
        Write-Log "Checking $packageName license: $license" "DEBUG"
        
        $complianceResult = Test-PackageLicenseCompliance -License $license -Policy $Policy
        
        switch ($complianceResult.Status) {
            "Allowed" {
                $complianceResults.Compliant += $complianceResult
                $complianceResults.Statistics.Compliant++
            }
            "Blocked" {
                $complianceResults.Violations += $complianceResult
                $complianceResults.Statistics.Violations++
            }
            "RequiresApproval" {
                $complianceResults.RequiresApproval += $complianceResult
                $complianceResults.Statistics.RequiresApproval++
            }
            "Unknown" {
                $complianceResults.Unknown += $complianceResult
                $complianceResults.Statistics.Unknown++
            }
        }
    }
    
    Write-Log "License compliance check completed" "INFO"
    Write-Log "Compliant: $($complianceResults.Statistics.Compliant)" "INFO"
    Write-Log "Violations: $($complianceResults.Statistics.Violations)" "WARNING"
    Write-Log "Unknown: $($complianceResults.Statistics.Unknown)" "INFO"
    Write-Log "Requires approval: $($complianceResults.Statistics.RequiresApproval)" "INFO"
    
    return $complianceResults
}

function Normalize-LicenseString {
    param([string]$License)
    
    if (!$License -or $License.Trim() -eq "") {
        return "Unknown"
    }
    
    $normalized = $License.Trim()
    
    # Common license name normalization
    $normalizationMap = @{
        "Apache 2.0" = "Apache-2.0"
        "Apache License 2.0" = "Apache-2.0"
        "Apache License, Version 2.0" = "Apache-2.0"
        "MIT License" = "MIT"
        "BSD 2-Clause License" = "BSD-2-Clause"
        "BSD 3-Clause License" = "BSD-3-Clause"
        "The MIT License" = "MIT"
        "The BSD 3-Clause License" = "BSD-3-Clause"
        "The Unlicense" = "Unlicense"
    }
    
    foreach ($from in $normalizationMap.Keys) {
        if ($normalized -eq $from) {
            return $normalizationMap[$from]
        }
    }
    
    return $normalized
}

function Test-PackageLicenseCompliance {
    param(
        [string]$License,
        [hashtable]$Policy
    )
    
    $result = [PSCustomObject]@{
        Package = $null
        License = $License
        Status = "Unknown"
        Policy = $null
        Notes = @()
        Source = $null
    }
    
    # Check against policy
    $isAllowed = $false
    $isRequiresApproval = $false
    $isBlocked = $false
    
    # Check allowed licenses (with wildcard support)
    foreach ($allowedLicense in $Policy.Allowed) {
        if (Test-LicenseMatch -License $License -Pattern $allowedLicense) {
            $isAllowed = $true
            $result.Status = "Allowed"
            $result.Policy = "Allowed"
            break
        }
    }
    
    # Check requires approval licenses
    if (!$isAllowed) {
        foreach ($approvalLicense in $Policy.RequiresApproval) {
            if (Test-LicenseMatch -License $License -Pattern $approvalLicense) {
                $isRequiresApproval = $true
                $result.Status = "RequiresApproval"
                $result.Policy = "RequiresApproval"
                $result.Notes += "This license requires team approval"
                break
            }
        }
    }
    
    # Check blocked licenses
    if (!$isAllowed -and !$isRequiresApproval) {
        foreach ($blockedLicense in $Policy.Blocked) {
            if (Test-LicenseMatch -License $License -Pattern $blockedLicense) {
                $isBlocked = $true
                $result.Status = "Blocked"
                $result.Policy = "Blocked"
                $result.Notes += "This license is not allowed"
                break
            }
        }
    }
    
    # If no match found, it's unknown
    if (!$isAllowed -and !$isRequiresApproval -and !$isBlocked) {
        $result.Status = "Unknown"
        $result.Policy = "Unknown"
        $result.Notes += "License not found in policy whitelist/blacklist"
    }
    
    return $result
}

function Test-LicenseMatch {
    param(
        [string]$License,
        [string]$Pattern
    )
    
    # Direct match
    if ($License -eq $Pattern) {
        return $true
    }
    
    # Wildcard match (e.g., "MIT*" matches "MIT License")
    if ($Pattern.EndsWith("*")) {
        $prefix = $Pattern.TrimEnd("*")
        return $License.StartsWith($prefix, "InvariantCultureIgnoreCase")
    }
    
    # Case-insensitive match
    return $License.Equals($Pattern, "InvariantCultureIgnoreCase")
}

function Get-LicenseCompatibilityMatrix {
    param([array]$Packages)
    
    Write-Log "Analyzing license compatibility" "INFO"
    
    $compatibilityMatrix = @{
        Licenses = @()
        Conflicts = @()
        Incompatibilities = @()
    }
    
    # Get unique licenses
    $uniqueLicenses = $Packages | ForEach-Object { Normalize-LicenseString $_.License } | Sort-Object | Get-Unique
    
    $compatibilityMatrix.Licenses = $uniqueLicenses
    
    # Check for known incompatible combinations
    $knownConflicts = @(
        @{ Licenses = @("MIT", "GPL-3.0"); Reason = "GPL-3.0 is incompatible with MIT" }
        @{ Licenses = @("Apache-2.0", "GPL-3.0"); Reason = "Apache-2.0 and GPL-3.0 are incompatible" }
        @{ Licenses = @("BSD-3-Clause", "GPL-3.0"); Reason = "BSD-3-Clause is incompatible with GPL-3.0" }
    )
    
    foreach ($conflict in $knownConflicts) {
        $hasConflict = $false
        foreach ($license in $conflict.Licenses) {
            if ($uniqueLicenses -contains $license) {
                $hasConflict = $true
                break
            }
        }
        
        if ($hasConflict) {
            $compatibilityMatrix.Conflicts += @{
                Licenses = $conflict.Licenses
                Reason = $conflict.Reason
                AffectedPackages = $Packages | Where-Object { 
                    $conflict.Licenses -contains (Normalize-LicenseString $_.License) 
                }
            }
        }
    }
    
    Write-Log "License compatibility analysis completed" "INFO"
    return $compatibilityMatrix
}

function Export-ComplianceReport {
    param(
        $ComplianceResults,
        $CompatibilityMatrix,
        [string]$OutputPath
    )
    
    Write-Log "Exporting compliance report" "INFO"
    
    $reportData = @{
        ScanInfo = @{
            Timestamp = $script:StartTime
            Duration = (Get-Date) - $script:StartTime
            Version = $script:ScriptVersion
            ProjectPath = $ProjectPath
            StrictMode = $StrictMode
        }
        Policy = @{
            Allowed = $script:DefaultLicensePolicy.Allowed
            RequiresApproval = $script:DefaultLicensePolicy.RequiresApproval
            Blocked = $script:DefaultLicensePolicy.Blocked
        }
        Summary = @{
            TotalPackages = $ComplianceResults.Statistics.Total
            Compliant = $ComplianceResults.Statistics.Compliant
            Violations = $ComplianceResults.Statistics.Violations
            Unknown = $ComplianceResults.Statistics.Unknown
            RequiresApproval = $ComplianceResults.Statistics.RequiresApproval
        }
        ComplianceResults = $ComplianceResults
        CompatibilityMatrix = $CompatibilityMatrix
    }
    
    # Export JSON report
    $reportData | ConvertTo-Json -Depth 10 | Out-File -FilePath $script:ReportFile -Encoding UTF8
    
    # Export markdown summary if requested
    if ($GenerateReport) {
        Export-ComplianceMarkdown -ReportData $reportData -OutputPath $script:SummaryFile
    }
    
    Write-Log "Compliance report exported to: $OutputPath" "INFO"
}

function Export-ComplianceMarkdown {
    param(
        $ReportData,
        [string]$OutputPath
    )
    
    $markdown = @"
# TiXL License Compliance Report

**Generated**: $($ReportData.ScanInfo.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"))
**Duration**: $($ReportData.ScanInfo.Duration.TotalSeconds.ToString("F2")) seconds
**Version**: $($ReportData.ScanInfo.Version)
**Project**: $($ReportData.ScanInfo.ProjectPath)
**Strict Mode**: $($ReportData.ScanInfo.StrictMode)

## Executive Summary

- **Total Packages**: $($ReportData.Summary.TotalPackages)
- **Compliant**: $($ReportData.Summary.Compliant)
- **Violations**: $($ReportData.Summary.Violations)
- **Unknown**: $($ReportData.Summary.Unknown)
- **Requires Approval**: $($ReportData.Summary.RequiresApproval)

## License Distribution

$(
    $licenseCounts = $ReportData.ComplianceResults.Packages | Group-Object { Normalize-LicenseString $_.License } | Sort-Object Count -Descending
    foreach ($group in $licenseCounts) {
        "- **$($group.Name)**: $($group.Count) packages"
    }
)

## Policy Compliance

### Allowed Licenses
$(
    foreach ($allowed in $ReportData.Policy.Allowed) {
        "- $allowed"
    }
)

### Licenses Requiring Approval
$(
    foreach ($approval in $ReportData.Policy.RequiresApproval) {
        "- $approval"
    }
)

### Blocked Licenses
$(
    foreach ($blocked in $ReportData.Policy.Blocked) {
        "- $blocked"
    }
)

## Violations

$(
    if ($ReportData.ComplianceResults.Violations.Count -gt 0) {
        foreach ($violation in $ReportData.ComplianceResults.Violations) {
            "### $("*" * $violation.Package.Name) v$($violation.Package.Version)"
            ""
            "- **License**: $($violation.License)"
            "- **Project**: $($violation.Package.Project)"
            "- **Reason**: $($violation.Notes -join "; ")"
            ""
        }
    } else {
        "No license violations found."
    }
)

## Unknown Licenses

$(
    if ($ReportData.ComplianceResults.Unknown.Count -gt 0) {
        foreach ($unknown in $ReportData.ComplianceResults.Unknown) {
            "- **$($unknown.Package.Name) v$($unknown.Package.Version)**: $($unknown.License) - $($unknown.Notes -join "; ")"
        }
    } else {
        "All licenses are identified."
    }
)

## Requires Approval

$(
    if ($ReportData.ComplianceResults.RequiresApproval.Count -gt 0) {
        foreach ($approval in $ReportData.ComplianceResults.RequiresApproval) {
            "- **$($approval.Package.Name) v$($approval.Package.Version)**: $($approval.License)"
        }
    } else {
        "No packages require approval."
    }
)

## License Compatibility

$(
    if ($ReportData.CompatibilityMatrix.Conflicts.Count -gt 0) {
        "### Detected Conflicts"
        foreach ($conflict in $ReportData.CompatibilityMatrix.Conflicts) {
            "#### $($conflict.Licenses -join " vs ")"
            "- **Reason**: $($conflict.Reason)"
            "- **Affected Packages**: $($conflict.AffectedPackages.Count)"
        }
    } else {
        "No license compatibility issues detected."
    }
)

## Recommendations

1. **Resolve Violations**: $(if ($ReportData.Summary.Violations -gt 0) { "Address $($ReportData.Summary.Violations) license violations immediately" } else { "No violations to resolve" })
2. **License Documentation**: $(if ($ReportData.Summary.Unknown -gt 0) { "Document $($ReportData.Summary.Unknown) unknown licenses" } else { "All licenses are documented" })
3. **Approval Process**: $(if ($ReportData.Summary.RequiresApproval -gt 0) { "Review and approve $($ReportData.Summary.RequiresApproval) packages requiring approval" } else { "All packages have approved licenses" })
4. **Policy Review**: Regular review of license policy to ensure it aligns with project requirements

---
*Report generated by TiXL License Compliance Checker v$($ReportData.ScanInfo.Version)*
"@
    
    $markdown | Out-File -FilePath $OutputPath -Encoding UTF8
}

# Main execution
Write-Log "Starting $script:ScriptName v$script:ScriptVersion" "INFO"
Write-Log "Project path: $ProjectPath" "INFO"

try {
    # Initialize license policy
    $policy = Initialize-LicensePolicy
    
    # Get package licenses
    $packages = Get-PackageLicenses -ProjectPath $ProjectPath
    
    if ($packages.Count -eq 0) {
        Write-Log "No packages found to check" "WARNING"
        exit 1
    }
    
    # Test license compliance
    $complianceResults = Test-LicenseCompliance -Packages $packages -Policy $policy
    
    # Get license compatibility analysis
    $compatibilityMatrix = Get-LicenseCompatibilityMatrix -Packages $packages
    
    # Export reports
    Export-ComplianceReport -ComplianceResults $complianceResults -CompatibilityMatrix $compatibilityMatrix -OutputPath $OutputPath
    
    # Check for violations
    $exitCode = 0
    if ($complianceResults.Statistics.Violations -gt 0) {
        Write-Log "License violations found: $($complianceResults.Statistics.Violations)" "ERROR"
        
        if ($FailOnViolation) {
            Write-Log "Failing due to license violations" "ERROR"
            $exitCode = 1
        }
    }
    
    if ($complianceResults.Statistics.Unknown -gt 0) {
        Write-Log "Unknown licenses found: $($complianceResults.Statistics.Unknown)" "WARNING"
    }
    
    if ($complianceResults.Statistics.RequiresApproval -gt 0) {
        Write-Log "Packages requiring approval: $($complianceResults.Statistics.RequiresApproval)" "INFO"
    }
    
    Write-Log "=== LICENSE COMPLIANCE CHECK COMPLETED ===" "INFO"
    Write-Log "Total packages: $($complianceResults.Statistics.Total)" "INFO"
    Write-Log "Compliant: $($complianceResults.Statistics.Compliant)" "INFO"
    Write-Log "Violations: $($complianceResults.Statistics.Violations)" "WARNING"
    Write-Log "Unknown: $($complianceResults.Statistics.Unknown)" "INFO"
    
    exit $exitCode
}
catch {
    Write-Log "License compliance check failed with error: $_" "ERROR"
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
}