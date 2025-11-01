#!/usr/bin/env pwsh
<#
.SYNOPSIS
    TiXL Dependency Registry Manager - Manages approved dependency registry and monitoring

.DESCRIPTION
    Maintains the registry of approved dependencies, monitors their health status,
    and provides tools for dependency lifecycle management.

.PARAMETER Action
    Action to perform (add, remove, update, list, monitor, health-check)

.PARAMETER PackageName
    Name of the package to manage

.PARAMETER Version
    Version of the package

.PARAMETER RegistryPath
    Path to the dependency registry file

.PARAMETER OutputPath
    Path to save reports and outputs

.PARAMETER Verbose
    Enable verbose output

.EXAMPLE
    .\dependency-registry-manager.ps1 -Action add -PackageName "Newtonsoft.Json" -Version "13.0.3"

.EXAMPLE
    .\dependency-registry-manager.ps1 -Action monitor -Verbose

.EXAMPLE
    .\dependency-registry-manager.ps1 -Action health-check -OutputPath "./health-reports"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("add", "remove", "update", "list", "monitor", "health-check", "export", "import")]
    [string]$Action,
    
    [Parameter(Mandatory=$false)]
    [string]$PackageName,
    
    [Parameter(Mandatory=$false)]
    [string]$Version,
    
    [Parameter(Mandatory=$false)]
    [string]$RegistryPath = "./config/dependency-registry.json",
    
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "./dependency-registry-reports",
    
    [Parameter(Mandatory=$false)]
    [switch]$Verbose
)

# Import required modules
Import-Module PowerShellGet -ErrorAction SilentlyContinue
Import-Module PSUtil -ErrorAction SilentlyContinue

# Load or initialize registry
function Initialize-Registry {
    param([string]$RegistryPath)
    
    $defaultRegistry = @{
        version = "1.0"
        lastUpdated = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
        dependencies = @{}
        metadata = @{
            totalCount = 0
            lastHealthCheck = $null
            nextHealthCheck = (Get-Date).AddDays(7).ToString("yyyy-MM-dd HH:mm:ss")
        }
    }
    
    if (Test-Path $RegistryPath) {
        try {
            $registry = Get-Content $RegistryPath | ConvertFrom-Json
            Write-Verbose "Loaded existing registry from: $RegistryPath"
            return $registry
        }
        catch {
            Write-Warning "Error loading registry: $($_.Exception.Message). Creating new registry."
        }
    }
    
    # Create new registry
    Write-Verbose "Creating new dependency registry at: $RegistryPath"
    $defaultRegistry | ConvertTo-Json -Depth 10 | Out-File -FilePath $RegistryPath -Encoding UTF8
    return $defaultRegistry
}

function Save-Registry {
    param([object]$Registry, [string]$RegistryPath)
    
    $Registry.lastUpdated = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    $Registry.metadata.totalCount = $Registry.dependencies.Count
    
    $Registry | ConvertTo-Json -Depth 10 | Out-File -FilePath $RegistryPath -Encoding UTF8
    Write-Verbose "Registry saved to: $RegistryPath"
}

function Add-DependencyToRegistry {
    param(
        [object]$Registry,
        [string]$PackageName,
        [string]$Version,
        [hashtable]$Metadata = @{}
    )
    
    $dependency = @{
        name = $PackageName
        version = $Version
        approvedDate = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
        approvedBy = $env:USERNAME ?? "System"
        status = "active"
        metadata = $Metadata
        healthCheck = @{
            lastCheck = $null
            score = $null
            issues = @()
            vulnerabilities = @()
        }
        monitoring = @{
            enabled = $true
            frequency = "weekly"
            lastNotification = $null
            alerts = @()
        }
    }
    
    $Registry.dependencies[$PackageName] = $dependency
    Write-Host "✅ Added dependency: $PackageName v$Version" -ForegroundColor Green
    
    return $dependency
}

function Remove-DependencyFromRegistry {
    param(
        [object]$Registry,
        [string]$PackageName
    )
    
    if ($Registry.dependencies.ContainsKey($PackageName)) {
        $Registry.dependencies.Remove($PackageName)
        Write-Host "✅ Removed dependency: $PackageName" -ForegroundColor Green
        return $true
    } else {
        Write-Warning "Dependency not found in registry: $PackageName"
        return $false
    }
}

function Update-DependencyVersion {
    param(
        [object]$Registry,
        [string]$PackageName,
        [string]$NewVersion
    )
    
    if ($Registry.dependencies.ContainsKey($PackageName)) {
        $Registry.dependencies[$PackageName].version = $NewVersion
        $Registry.dependencies[$PackageName].updatedDate = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
        $Registry.dependencies[$PackageName].updatedBy = $env:USERNAME ?? "System"
        Write-Host "✅ Updated $PackageName to version $NewVersion" -ForegroundColor Green
        return $true
    } else {
        Write-Warning "Dependency not found in registry: $PackageName"
        return $false
    }
}

function Get-DependencyHealthStatus {
    param(
        [string]$PackageName,
        [string]$Version,
        [string]$RegistryPath
    )
    
    Write-Verbose "Checking health status for: $PackageName v$Version"
    
    $healthReport = @{
        packageName = $PackageName
        version = $Version
        checkDate = Get-Date
        score = 0
        status = "unknown"
        checks = @{
            security = @{ score = 0; status = "unknown"; issues = @() }
            maintenance = @{ score = 0; status = "unknown"; issues = @() }
            performance = @{ score = 0; status = "unknown"; issues = @() }
            license = @{ score = 0; status = "unknown"; issues = @() }
        }
        overallStatus = "healthy"
        recommendations = @()
        criticalIssues = @()
    }
    
    # Security vulnerability check
    try {
        Write-Verbose "Checking security vulnerabilities..."
        # This would integrate with actual security scanning tools
        $securityScore = 85 # Placeholder - would use actual vulnerability scanner
        $healthReport.checks.security.score = $securityScore
        $healthReport.checks.security.status = if ($securityScore -ge 90) { "excellent" } elseif ($securityScore -ge 70) { "good" } elseif ($securityScore -ge 50) { "warning" } else { "critical" }
    }
    catch {
        Write-Verbose "Security check failed: $($_.Exception.Message)"
        $healthReport.checks.security.status = "error"
    }
    
    # Maintenance status check
    try {
        Write-Verbose "Checking maintenance status..."
        # This would check GitHub activity, last update, etc.
        $maintenanceScore = 78 # Placeholder
        $healthReport.checks.maintenance.score = $maintenanceScore
        $healthReport.checks.maintenance.status = if ($maintenanceScore -ge 80) { "active" } elseif ($maintenanceScore -ge 60) { "moderate" } else { "stale" }
    }
    catch {
        Write-Verbose "Maintenance check failed: $($_.Exception.Message)"
        $healthReport.checks.maintenance.status = "error"
    }
    
    # Performance impact check
    try {
        Write-Verbose "Checking performance impact..."
        # This would measure performance metrics
        $performanceScore = 82 # Placeholder
        $healthReport.checks.performance.score = $performanceScore
        $healthReport.checks.performance.status = if ($performanceScore -ge 85) { "excellent" } elseif ($performanceScore -ge 70) { "acceptable" } else { "poor" }
    }
    catch {
        Write-Verbose "Performance check failed: $($_.Exception.Message)"
        $healthReport.checks.performance.status = "error"
    }
    
    # License compliance check
    try {
        Write-Verbose "Checking license compliance..."
        # This would verify license hasn't changed
        $licenseScore = 95 # Placeholder
        $healthReport.checks.license.score = $licenseScore
        $healthReport.checks.license.status = if ($licenseScore -eq 100) { "compliant" } elseif ($licenseScore -ge 80) { "minor-issues" } else { "non-compliant" }
    }
    catch {
        Write-Verbose "License check failed: $($_.Exception.Message)"
        $healthReport.checks.license.status = "error"
    }
    
    # Calculate overall score
    $scores = @(
        $healthReport.checks.security.score,
        $healthReport.checks.maintenance.score,
        $healthReport.checks.performance.score,
        $healthReport.checks.license.score
    ) | Where-Object { $_ -gt 0 }
    
    if ($scores.Count -gt 0) {
        $healthReport.score = [math]::Round(($scores | Measure-Object -Average).Average, 2)
    }
    
    # Determine overall health status
    if ($healthReport.checks.security.status -eq "critical" -or $healthReport.checks.license.status -eq "non-compliant") {
        $healthReport.overallStatus = "critical"
        $healthReport.criticalIssues += "Security vulnerabilities or license compliance issues detected"
    } elseif ($healthReport.checks.security.status -eq "warning" -or $healthReport.checks.maintenance.status -eq "stale") {
        $healthReport.overallStatus = "warning"
    } elseif ($healthReport.score -ge 80) {
        $healthReport.overallStatus = "healthy"
    } elseif ($healthReport.score -ge 60) {
        $healthReport.overallStatus = "warning"
    } else {
        $healthReport.overallStatus = "critical"
    }
    
    # Generate recommendations
    if ($healthReport.checks.security.score -lt 90) {
        $healthReport.recommendations += "Review security vulnerabilities and apply updates"
    }
    if ($healthReport.checks.maintenance.score -lt 70) {
        $healthReport.recommendations += "Consider replacing with more actively maintained alternative"
    }
    if ($healthReport.checks.performance.score -lt 70) {
        $healthReport.recommendations += "Evaluate performance impact and consider optimization"
    }
    
    return $healthReport
}

function Start-DependencyMonitoring {
    param([object]$Registry, [string]$OutputPath)
    
    Write-Host "🔍 Starting dependency monitoring for $($Registry.dependencies.Count) packages..." -ForegroundColor Cyan
    
    $monitoringResults = @{
        startTime = Get-Date
        totalPackages = $Registry.dependencies.Count
        healthyPackages = 0
        warningPackages = 0
        criticalPackages = 0
        errors = 0
        packageReports = @()
    }
    
    foreach ($packageKey in $Registry.dependencies.Keys) {
        $package = $Registry.dependencies[$packageKey]
        
        Write-Host "Checking: $($package.name) v$($package.version)" -ForegroundColor Yellow
        
        try {
            $healthReport = Get-DependencyHealthStatus -PackageName $package.name -Version $package.version -RegistryPath $PSScriptRoot
            $monitoringResults.packageReports += $healthReport
            
            # Update package health status in registry
            $Registry.dependencies[$packageKey].healthCheck = @{
                lastCheck = $healthReport.checkDate.ToString("yyyy-MM-dd HH:mm:ss")
                score = $healthReport.score
                status = $healthReport.overallStatus
                issues = $healthReport.criticalIssues
            }
            
            # Update counters
            switch ($healthReport.overallStatus) {
                "healthy" { $monitoringResults.healthyPackages++ }
                "warning" { $monitoringResults.warningPackages++ }
                "critical" { $monitoringResults.criticalPackages++ }
            }
        }
        catch {
            Write-Warning "Error checking $($package.name): $($_.Exception.Message)"
            $monitoringResults.errors++
        }
    }
    
    $monitoringResults.endTime = Get-Date
    $monitoringResults.duration = ($monitoringResults.endTime - $monitoringResults.startTime).TotalSeconds
    
    # Save monitoring results
    $monitoringPath = Join-Path $OutputPath "monitoring-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    New-Item -ItemType Directory -Path $monitoringPath -Force | Out-Null
    
    $monitoringResults | ConvertTo-Json -Depth 10 | Out-File -FilePath (Join-Path $monitoringPath "monitoring-results.json") -Encoding UTF8
    
    # Generate monitoring summary
    $summaryContent = @"
# Dependency Monitoring Summary

**Monitoring Date**: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  
**Duration**: $($monitoringResults.duration) seconds  
**Total Packages**: $($monitoringResults.totalPackages)

## Status Overview

- 🟢 **Healthy**: $($monitoringResults.healthyPackages) packages
- 🟡 **Warning**: $($monitoringResults.warningPackages) packages  
- 🔴 **Critical**: $($monitoringResults.criticalPackages) packages
- ❌ **Errors**: $($monitoringResults.errors) packages

## Package Details

$(
    foreach ($report in $monitoringResults.packageReports) {
        $statusIcon = switch ($report.overallStatus) {
            "healthy" { "🟢" }
            "warning" { "🟡" }
            "critical" { "🔴" }
            default { "❓" }
        }
        
        "| $($statusIcon) | $($report.packageName) v$($report.version) | $($report.score)/100 | $($report.overallStatus) |"
    }
)

## Critical Issues

$(
    $criticalIssues = $monitoringResults.packageReports | Where-Object { $_.criticalIssues.Count -gt 0 }
    if ($criticalIssues.Count -gt 0) {
        foreach ($issue in $criticalIssues) {
            foreach ($criticalIssue in $issue.criticalIssues) {
                "- **$($issue.packageName)**: $criticalIssue"
            }
        }
    } else {
        "No critical issues detected."
    }
)

## Recommendations

$(
    $allRecommendations = $monitoringResults.packageReports | ForEach-Object { $_.recommendations } | Select-Object -Unique
    if ($allRecommendations.Count -gt 0) {
        foreach ($rec in $allRecommendations) {
            "- $rec"
        }
    } else {
        "No immediate action required."
    }
)

---
Generated by TiXL Dependency Registry Manager v1.0
"@
    
    $summaryContent | Out-File -FilePath (Join-Path $monitoringPath "monitoring-summary.md") -Encoding UTF8
    
    Write-Host "`n📊 Monitoring Results:" -ForegroundColor Cyan
    Write-Host "  Total: $($monitoringResults.totalPackages)" -ForegroundColor White
    Write-Host "  Healthy: $($monitoringResults.healthyPackages)" -ForegroundColor Green
    Write-Host "  Warning: $($monitoringResults.warningPackages)" -ForegroundColor Yellow
    Write-Host "  Critical: $($monitoringResults.criticalPackages)" -ForegroundColor Red
    Write-Host "  Errors: $($monitoringResults.errors)" -ForegroundColor Magenta
    Write-Host "`nResults saved to: $monitoringPath" -ForegroundColor Cyan
    
    return $monitoringResults
}

# Main execution
$ErrorActionPreference = "Continue"

Write-Host "🔧 TiXL Dependency Registry Manager" -ForegroundColor Cyan
Write-Host "Action: $Action" -ForegroundColor White

# Initialize registry
$registry = Initialize-Registry -RegistryPath $RegistryPath

switch ($Action) {
    "add" {
        if (-not $PackageName) {
            Write-Error "PackageName is required for add action"
            exit 1
        }
        
        if ($registry.dependencies.ContainsKey($PackageName)) {
            Write-Warning "Package already exists in registry. Use update action to modify."
            exit 1
        }
        
        $metadata = @{
            source = "manual"
            reason = "Added via registry manager"
            priority = "normal"
        }
        
        $dependency = Add-DependencyToRegistry -Registry $registry -PackageName $PackageName -Version $Version -Metadata $metadata
        Save-Registry -Registry $registry -RegistryPath $RegistryPath
    }
    
    "remove" {
        if (-not $PackageName) {
            Write-Error "PackageName is required for remove action"
            exit 1
        }
        
        $removed = Remove-DependencyFromRegistry -Registry $registry -PackageName $PackageName
        if ($removed) {
            Save-Registry -Registry $registry -RegistryPath $RegistryPath
        }
    }
    
    "update" {
        if (-not $PackageName -or -not $Version) {
            Write-Error "PackageName and Version are required for update action"
            exit 1
        }
        
        $updated = Update-DependencyVersion -Registry $registry -PackageName $PackageName -NewVersion $Version
        if ($updated) {
            Save-Registry -Registry $registry -RegistryPath $RegistryPath
        }
    }
    
    "list" {
        Write-Host "`n📋 Registered Dependencies ($($registry.dependencies.Count) total):" -ForegroundColor Cyan
        Write-Host "Name".PadRight(40) + "Version".PadRight(15) + "Status".PadRight(12) + "Health Score".PadRight(15) + "Approved Date" -ForegroundColor White
        
        foreach ($packageKey in $registry.dependencies.Keys) {
            $pkg = $registry.dependencies[$packageKey]
            $healthScore = if ($pkg.healthCheck.score) { "$($pkg.healthCheck.score)/100" } else { "N/A" }
            $status = $pkg.status.PadRight(12)
            
            Write-Host $pkg.name.PadRight(40) + $pkg.version.PadRight(15) + $status + $healthScore.PadRight(15) + $pkg.approvedDate -ForegroundColor Green
        }
    }
    
    "monitor" {
        $monitoringResults = Start-DependencyMonitoring -Registry $registry -OutputPath $OutputPath
        Save-Registry -Registry $registry -RegistryPath $RegistryPath
        
        # Return monitoring results for pipeline use
        $monitoringResults
    }
    
    "health-check" {
        if (-not $PackageName) {
            Write-Error "PackageName is required for health-check action"
            exit 1
        }
        
        if ($registry.dependencies.ContainsKey($PackageName)) {
            $package = $registry.dependencies[$PackageName]
            $healthReport = Get-DependencyHealthStatus -PackageName $PackageName -Version $package.version -RegistryPath $RegistryPath
            
            # Save health report
            $healthPath = Join-Path $OutputPath "health-check-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
            New-Item -ItemType Directory -Path $healthPath -Force | Out-Null
            
            $healthReport | ConvertTo-Json -Depth 10 | Out-File -FilePath (Join-Path $healthPath "health-report.json") -Encoding UTF8
            
            Write-Host "`n🏥 Health Check Results for $PackageName:" -ForegroundColor Cyan
            Write-Host "  Overall Score: $($healthReport.score)/100" -ForegroundColor White
            Write-Host "  Status: $($healthReport.overallStatus)" -ForegroundColor $(switch ($healthReport.overallStatus) { "healthy" { "Green" } "warning" { "Yellow" } "critical" { "Red" } default { "White" } })
            
            if ($healthReport.recommendations.Count -gt 0) {
                Write-Host "`nRecommendations:" -ForegroundColor Yellow
                foreach ($rec in $healthReport.recommendations) {
                    Write-Host "  - $rec" -ForegroundColor Yellow
                }
            }
            
            return $healthReport
        } else {
            Write-Warning "Package not found in registry: $PackageName"
        }
    }
    
    "export" {
        $exportPath = Join-Path $OutputPath "registry-export-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
        New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
        
        $export = @{
            exportDate = Get-Date
            version = $registry.version
            totalDependencies = $registry.dependencies.Count
            dependencies = $registry.dependencies
        }
        
        $export | ConvertTo-Json -Depth 10 | Out-File -FilePath $exportPath -Encoding UTF8
        Write-Host "✅ Registry exported to: $exportPath" -ForegroundColor Green
    }
    
    "import" {
        Write-Warning "Import functionality not yet implemented"
    }
    
    default {
        Write-Error "Unknown action: $Action"
        exit 1
    }
}

Write-Host "`n✅ Dependency registry operation completed." -ForegroundColor Green
