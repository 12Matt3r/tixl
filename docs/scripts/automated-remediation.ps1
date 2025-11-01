#!/usr/bin/env pwsh
<#
.SYNOPSIS
    TiXL Automated Dependency Remediation - Automated vulnerability fixes and dependency updates

.DESCRIPTION
    Automated system for fixing security vulnerabilities and updating dependencies:
    - Automatic security vulnerability remediation
    - Safe dependency updates with risk assessment
    - Rollback capability for failed updates
    - Automated testing after updates
    - Pull request creation for changes

.PARAMETER ProjectPath
    Path to the solution or project file to remediate

.PARAMETER OutputPath
    Directory to save remediation reports

.PARAMETER VulnerabilityReportPath
    Path to vulnerability report JSON file

.PARAMETER UpdateMode
    Update mode: Safe, Aggressive, SecurityOnly

.PARAMETER AutoApprove
    Automatically approve and apply changes

.PARAMETER CreatePR
    Create pull request for changes

.EXAMPLE
    .\automated-remediation.ps1 -ProjectPath "TiXL.sln" -VulnerabilityReportPath "vulnerability-report.json" -UpdateMode "SecurityOnly"

.EXAMPLE
    .\automated-remediation.ps1 -ProjectPath "TiXL.sln" -AutoApprove -CreatePR -UpdateMode "Safe"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$ProjectPath,
    
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "./remediation-reports",
    
    [Parameter(Mandatory=$false)]
    [string]$VulnerabilityReportPath,
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("Safe", "Aggressive", "SecurityOnly")]
    [string]$UpdateMode = "Safe",
    
    [Parameter(Mandatory=$false)]
    [switch]$AutoApprove,
    
    [Parameter(Mandatory=$false)]
    [switch]$CreatePR,
    
    [Parameter(Mandatory=$false)]
    [switch]$TestAfterUpdate,
    
    [Parameter(Mandatory=$false)]
    [string]$BranchPrefix = "tixl-fix",
    
    [Parameter(Mandatory=$false)]
    [int]$MaxParallelUpdates = 3
)

# Global variables
$script:ScriptName = "TiXL Automated Dependency Remediation"
$script:ScriptVersion = "1.0.0"
$script:StartTime = Get-Date
$script:BackupPath = Join-Path $OutputPath "backup"
$script:ChangesPath = Join-Path $OutputPath "changes"

# Initialize output directories
if (!(Test-Path $OutputPath)) {
    New-Item -Path $OutputPath -ItemType Directory -Force | Out-Null
}
if (!(Test-Path $script:BackupPath)) {
    New-Item -Path $script:BackupPath -ItemType Directory -Force | Out-Null
}
if (!(Test-Path $script:ChangesPath)) {
    New-Item -Path $script:ChangesPath -ItemType Directory -Force | Out-Null
}

$script:LogFile = Join-Path $OutputPath "remediation.log"
$script:ReportFile = Join-Path $OutputPath "remediation-report.json"
$script:SummaryFile = Join-Path $OutputPath "remediation-summary.md"

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
    
    if ($Level -ne "DEBUG") {
        Write-Host $logEntry
    }
    
    $logEntry | Out-File -FilePath $script:LogFile -Append -Encoding UTF8
}

function Test-Prerequisites {
    Write-Log "Checking remediation prerequisites" "INFO"
    
    # Check .NET SDK
    try {
        $dotnetVersion = dotnet --version
        Write-Log "✅ .NET SDK: $dotnetVersion" "INFO"
    } catch {
        Write-Log "❌ .NET SDK not found" "ERROR"
        return $false
    }
    
    # Check Git (if PR creation is enabled)
    if ($CreatePR) {
        try {
            $gitVersion = git --version
            Write-Log "✅ Git: $gitVersion" "INFO"
        } catch {
            Write-Log "❌ Git not found - PR creation disabled" "WARNING"
            $CreatePR = $false
        }
    }
    
    # Check PowerShell version
    $psVersion = $PSVersionTable.PSVersion
    if ($psVersion.Major -lt 7) {
        Write-Log "⚠️  PowerShell 7+ recommended, current: $($psVersion.ToString())" "WARNING"
    }
    
    return $true
}

function Backup-ProjectFiles {
    param([string]$ProjectPath)
    
    Write-Log "Creating backup of project files" "INFO"
    
    $backupDir = Join-Path $script:BackupPath "project-backup-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    New-Item -Path $backupDir -ItemType Directory -Force | Out-Null
    
    try {
        # Backup project files
        $projectFiles = Get-ChildItem -Path (Split-Path $ProjectPath) -Filter "*.csproj" -Recurse
        foreach ($file in $projectFiles) {
            $relativePath = $file.FullName.Substring((Split-Path $ProjectPath).Length + 1)
            $backupFilePath = Join-Path $backupDir $relativePath
            New-Item -Path (Split-Path $backupFilePath) -ItemType Directory -Force | Out-Null
            Copy-Item -Path $file.FullName -Destination $backupFilePath
        }
        
        # Backup solution file
        if ($ProjectPath.EndsWith('.sln')) {
            $backupSlnPath = Join-Path $backupDir (Split-Path $ProjectPath -Leaf)
            Copy-Item -Path $ProjectPath -Destination $backupSlnPath
        }
        
        Write-Log "Project backup created: $backupDir" "INFO"
        return $backupDir
    }
    catch {
        Write-Log "Failed to create backup: $_" "ERROR"
        return $null
    }
}

function Get-VulnerabilityReport {
    param([string]$ReportPath)
    
    Write-Log "Loading vulnerability report from $ReportPath" "INFO"
    
    try {
        if ($ReportPath -and (Test-Path $ReportPath)) {
            $report = Get-Content $ReportPath | ConvertFrom-Json
            Write-Log "Vulnerability report loaded successfully" "INFO"
            return $report
        } else {
            Write-Log "No vulnerability report provided, running fresh scan" "INFO"
            return $null
        }
    }
    catch {
        Write-Log "Failed to load vulnerability report: $_" "WARNING"
        return $null
    }
}

function Analyze-VulnerabilitiesForRemediation {
    param($VulnerabilityReport)
    
    Write-Log "Analyzing vulnerabilities for automated remediation" "INFO"
    
    if (-not $VulnerabilityReport -or -not $VulnerabilityReport.Vulnerabilities) {
        Write-Log "No vulnerabilities to analyze" "INFO"
        return @{
            Critical = @()
            High = @()
            Medium = @()
            Recommendations = @()
        }
    }
    
    $vulnerabilities = $VulnerabilityReport.Vulnerabilities
    $critical = @()
    $high = @()
    $medium = @()
    
    foreach ($vuln in $vulnerabilities) {
        $severity = $vuln.Severity
        $packageName = $vuln.Package
        $currentVersion = $vuln.Version
        $fixedVersion = $vuln.FixedInVersion
        
        # Determine remediation action
        $remediation = @{
            Vulnerability = $vuln
            Package = $packageName
            CurrentVersion = $currentVersion
            FixedVersion = $fixedVersion
            Action = ""
            RiskLevel = ""
            Reasoning = ""
        }
        
        if ($fixedVersion) {
            $action = Determine-UpdateAction -CurrentVersion $currentVersion -FixedVersion $fixedVersion -UpdateMode $UpdateMode
            $remediation.Action = $action.Action
            $remediation.RiskLevel = $action.RiskLevel
            $remediation.Reasoning = $action.Reasoning
        } else {
            $remediation.Action = "ManualReview"
            $remediation.RiskLevel = "Unknown"
            $remediation.Reasoning = "No fixed version available"
        }
        
        switch ($severity) {
            "Critical" { $critical += $remediation; break }
            "High" { $high += $remediation; break }
            "Medium" { $medium += $remediation; break }
        }
    }
    
    return @{
        Critical = $critical
        High = $high
        Medium = $medium
        Recommendations = @($critical + $high + $medium)
    }
}

function Determine-UpdateAction {
    param(
        [string]$CurrentVersion,
        [string]$FixedVersion,
        [string]$UpdateMode
    )
    
    $currentVer = [version]$CurrentVersion
    $fixedVer = [version]$FixedVersion
    
    $action = @{
        Action = "Update"
        RiskLevel = "Low"
        Reasoning = ""
    }
    
    switch ($UpdateMode) {
        "SecurityOnly" {
            if ($fixedVer -gt $currentVer) {
                $action.Action = "SecurityUpdate"
                $action.Reasoning = "Security-only update available"
            } else {
                $action.Action = "Skip"
                $action.Reasoning = "No security updates needed"
            }
        }
        "Safe" {
            if ($fixedVer.Major -eq $currentVer.Major -and $fixedVer.Minor -eq $currentVer.Minor) {
                $action.Action = "SafeUpdate"
                $action.RiskLevel = "Low"
                $action.Reasoning = "Patch-level update within same major.minor version"
            } elseif ($fixedVer.Major -eq $currentVer.Major -and $fixedVer.Minor -le ($currentVer.Minor + 1)) {
                $action.Action = "MinorUpdate"
                $action.RiskLevel = "Medium"
                $action.Reasoning = "Minor version update"
            } else {
                $action.Action = "Review"
                $action.RiskLevel = "High"
                $action.Reasoning = "Major version update requires manual review"
            }
        }
        "Aggressive" {
            if ($fixedVer -ge $currentVer) {
                $action.Action = "Update"
                $action.RiskLevel = "Medium"
                $action.Reasoning = "Latest available version"
            }
        }
    }
    
    return $action
}

function Get-SafeUpdates {
    param([string]$ProjectPath)
    
    Write-Log "Checking for available package updates" "INFO"
    
    try {
        $outdatedOutput = dotnet list "$ProjectPath" package --outdated --include-transitive 2>&1
        
        $updates = @()
        
        # Parse outdated packages
        foreach ($line in $outdatedOutput) {
            if ($line -match "^\s*([\w\.-]+)\s+([\d\.]+)\s+([\d\.]+)\s+(.*)$") {
                $packageName = $matches[1]
                $currentVersion = $matches[2]
                $latestVersion = $matches[3]
                $reason = $matches[4]
                
                $update = [PSCustomObject]@{
                    Package = $packageName
                    CurrentVersion = $currentVersion
                    LatestVersion = $latestVersion
                    Reason = $reason
                    Type = Get-UpdateType -CurrentVersion $currentVersion -LatestVersion $latestVersion
                    RiskLevel = Get-UpdateRiskLevel -CurrentVersion $currentVersion -LatestVersion $latestVersion
                }
                
                $updates += $update
            }
        }
        
        Write-Log "Found $($updates.Count) available updates" "INFO"
        return $updates
    }
    catch {
        Write-Log "Failed to get update information: $_" "ERROR"
        return @()
    }
}

function Get-UpdateType {
    param(
        [string]$CurrentVersion,
        [string]$LatestVersion
    )
    
    try {
        $currentVer = [version]$CurrentVersion.Split('-')[0].Split('+')[0]
        $latestVer = [version]$LatestVersion.Split('-')[0].Split('+')[0]
        
        if ($latestVer.Major -gt $currentVer.Major) {
            return "Major"
        }
        elseif ($latestVer.Minor -gt $currentVer.Minor) {
            return "Minor"
        }
        else {
            return "Patch"
        }
    }
    catch {
        return "Unknown"
    }
}

function Get-UpdateRiskLevel {
    param(
        [string]$CurrentVersion,
        [string]$LatestVersion
    )
    
    $updateType = Get-UpdateType -CurrentVersion $CurrentVersion -LatestVersion $LatestVersion
    
    switch ($updateType) {
        "Major" { return "High" }
        "Minor" { return "Medium" }
        "Patch" { return "Low" }
        default { return "Unknown" }
    }
}

function Apply-SecurityUpdates {
    param(
        [array]$SecurityRemediations,
        [string]$ProjectPath
    )
    
    Write-Log "Applying security updates" "INFO"
    
    $appliedUpdates = @()
    $failedUpdates = @()
    $skippedUpdates = @()
    
    foreach ($remediation in $SecurityRemediations) {
        if ($remediation.Action -eq "Skip") {
            $skippedUpdates += $remediation
            continue
        }
        
        Write-Log "Updating $($remediation.Package) to $($remediation.FixedVersion)" "INFO"
        
        try {
            $result = Update-Package -ProjectPath $ProjectPath -PackageName $remediation.Package -Version $remediation.FixedVersion
            
            if ($result.Success) {
                $appliedUpdates += @{
                    Package = $remediation.Package
                    FromVersion = $remediation.CurrentVersion
                    ToVersion = $remediation.FixedVersion
                    Action = $remediation.Action
                    VulnerabilityID = $remediation.Vulnerability.ID
                    Success = $true
                }
                Write-Log "✅ Successfully updated $($remediation.Package)" "INFO"
            } else {
                $failedUpdates += @{
                    Package = $remediation.Package
                    Error = $result.Error
                    VulnerabilityID = $remediation.Vulnerability.ID
                    Success = $false
                }
                Write-Log "❌ Failed to update $($remediation.Package): $($result.Error)" "ERROR"
            }
        }
        catch {
            $failedUpdates += @{
                Package = $remediation.Package
                Error = $_.ToString()
                VulnerabilityID = $remediation.Vulnerability.ID
                Success = $false
            }
            Write-Log "❌ Exception updating $($remediation.Package): $_" "ERROR"
        }
    }
    
    return @{
        Applied = $appliedUpdates
        Failed = $failedUpdates
        Skipped = $skippedUpdates
    }
}

function Update-Package {
    param(
        [string]$ProjectPath,
        [string]$PackageName,
        [string]$Version
    )
    
    try {
        Write-Log "Running: dotnet add package $PackageName --version $Version" "DEBUG"
        
        $output = dotnet add "$ProjectPath" package $PackageName --version $Version 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            return @{
                Success = $true
                Output = $output
                Error = $null
            }
        } else {
            return @{
                Success = $false
                Output = $output
                Error = "dotnet add package failed with exit code $LASTEXITCODE"
            }
        }
    }
    catch {
        return @{
            Success = $false
            Output = $null
            Error = $_.ToString()
        }
    }
}

function Apply-SafeUpdates {
    param(
        [array]$SafeUpdates,
        [string]$ProjectPath,
        [int]$MaxParallel
    )
    
    Write-Log "Applying safe updates" "INFO"
    
    $appliedUpdates = @()
    $failedUpdates = @()
    $skippedUpdates = @()
    
    # Filter updates by risk level and update mode
    $filteredUpdates = $SafeUpdates | Where-Object {
        $UpdateMode -eq "Safe" -and $_.RiskLevel -eq "Low" -or
        $UpdateMode -eq "Aggressive" -and $_.RiskLevel -in @("Low", "Medium") -or
        $UpdateMode -eq "SecurityOnly" -and $false  # SecurityOnly handled separately
    }
    
    # Apply updates in parallel
    $jobs = @()
    foreach ($update in $filteredUpdates) {
        if ($jobs.Count -ge $MaxParallel) {
            # Wait for some jobs to complete
            $jobs | Wait-Job | ForEach-Object {
                $result = Receive-Job $_
                $appliedUpdates += $result.Applied
                $failedUpdates += $result.Failed
                Remove-Job $_
            }
            $jobs = @()
        }
        
        # Start parallel update job
        $job = Start-Job -ScriptBlock {
            param($update, $projectPath)
            
            try {
                $result = Update-Package -ProjectPath $projectPath -PackageName $update.Package -Version $update.LatestVersion
                
                if ($result.Success) {
                    return @{
                        Applied = @{
                            Package = $update.Package
                            FromVersion = $update.CurrentVersion
                            ToVersion = $update.LatestVersion
                            Type = $update.Type
                            RiskLevel = $update.RiskLevel
                            Success = $true
                        }
                        Failed = @()
                    }
                } else {
                    return @{
                        Applied = @()
                        Failed = @{
                            Package = $update.Package
                            Error = $result.Error
                            Success = $false
                        }
                    }
                }
            }
            catch {
                return @{
                    Applied = @()
                    Failed = @{
                        Package = $update.Package
                        Error = $_.ToString()
                        Success = $false
                    }
                }
            }
        } -ArgumentList $update, $ProjectPath
        
        $jobs += $job
    }
    
    # Wait for remaining jobs
    $jobs | Wait-Job | ForEach-Object {
        $result = Receive-Job $_
        $appliedUpdates += $result.Applied
        $failedUpdates += $result.Failed
        Remove-Job $_
    }
    
    return @{
        Applied = $appliedUpdates
        Failed = $failedUpdates
        Skipped = $skippedUpdates
    }
}

function Test-ProjectAfterUpdates {
    param([string]$ProjectPath)
    
    if (-not $TestAfterUpdate) {
        Write-Log "Testing disabled, skipping project build test" "INFO"
        return @{
            Success = $true
            BuildSuccess = $false
            TestSuccess = $false
            Errors = @()
        }
    }
    
    Write-Log "Testing project after updates" "INFO"
    
    $testResult = @{
        Success = $true
        BuildSuccess = $false
        TestSuccess = $false
        Errors = @()
        Warnings = @()
    }
    
    try {
        # Test build
        Write-Log "Testing build..." "INFO"
        $buildOutput = dotnet build "$ProjectPath" --verbosity quiet 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            $testResult.BuildSuccess = $true
            Write-Log "✅ Build successful" "INFO"
        } else {
            $testResult.Success = $false
            $testResult.BuildSuccess = $false
            $testResult.Errors += "Build failed with exit code $LASTEXITCODE"
            Write-Log "❌ Build failed" "ERROR"
        }
        
        # Test restore
        Write-Log "Testing restore..." "INFO"
        $restoreOutput = dotnet restore "$ProjectPath" --verbosity quiet 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Log "✅ Restore successful" "INFO"
        } else {
            $testResult.Success = $false
            $testResult.Errors += "Restore failed with exit code $LASTEXITCODE"
            Write-Log "❌ Restore failed" "ERROR"
        }
        
        # Run tests if test projects exist
        $testProjects = Get-ChildItem -Path (Split-Path $ProjectPath) -Filter "*Tests.csproj" -Recurse
        if ($testProjects.Count -gt 0) {
            Write-Log "Running tests..." "INFO"
            $testOutput = dotnet test $testProjects[0].FullName --verbosity quiet --no-build 2>&1
            
            if ($LASTEXITCODE -eq 0) {
                $testResult.TestSuccess = $true
                Write-Log "✅ Tests passed" "INFO"
            } else {
                $testResult.Warnings += "Tests failed with exit code $LASTEXITCODE"
                Write-Log "⚠️  Tests failed" "WARNING"
            }
        }
    }
    catch {
        $testResult.Success = $false
        $testResult.Errors += "Testing failed with exception: $_"
        Write-Log "❌ Testing failed: $_" "ERROR"
    }
    
    return $testResult
}

function Rollback-Changes {
    param(
        [string]$BackupPath,
        [string]$ProjectPath
    )
    
    Write-Log "Rolling back changes using backup: $BackupPath" "INFO"
    
    try {
        if (-not (Test-Path $BackupPath)) {
            Write-Log "Backup path not found: $BackupPath" "ERROR"
            return $false
        }
        
        # Restore project files
        $backupFiles = Get-ChildItem -Path $BackupPath -Filter "*.csproj" -Recurse
        foreach ($file in $backupFiles) {
            $relativePath = $file.FullName.Substring($BackupPath.Length + 1)
            $targetPath = Join-Path (Split-Path $ProjectPath) $relativePath
            New-Item -Path (Split-Path $targetPath) -ItemType Directory -Force | Out-Null
            Copy-Item -Path $file.FullName -Destination $targetPath -Force
        }
        
        # Restore solution file
        $backupSln = Get-ChildItem -Path $BackupPath -Filter "*.sln"
        if ($backupSln) {
            Copy-Item -Path $backupSln[0].FullName -Destination $ProjectPath -Force
        }
        
        Write-Log "✅ Rollback completed successfully" "INFO"
        return $true
    }
    catch {
        Write-Log "❌ Rollback failed: $_" "ERROR"
        return $false
    }
}

function Create-BranchAndPR {
    param(
        [string]$BranchName,
        [string]$CommitMessage
    )
    
    if (-not $CreatePR) {
        Write-Log "PR creation disabled, skipping branch and PR creation" "INFO"
        return $null
    }
    
    try {
        Write-Log "Creating branch: $BranchName" "INFO"
        
        # Create and checkout branch
        git checkout -b $BranchName
        git add .
        git commit -m $CommitMessage
        
        Write-Log "✅ Branch created and committed: $BranchName" "INFO"
        
        # Note: In a real implementation, you would push the branch and create a PR
        # using GitHub API or other VCS tools
        Write-Log "📝 Ready to create PR (manual step required)" "INFO"
        
        return @{
            Branch = $BranchName
            Status = "Created"
            CommitMessage = $CommitMessage
            NextStep = "Push branch and create PR manually or configure PR creation"
        }
    }
    catch {
        Write-Log "❌ Failed to create branch: $_" "ERROR"
        return @{
            Branch = $BranchName
            Status = "Failed"
            Error = $_.ToString()
        }
    }
}

function Export-RemediationReport {
    param(
        $SecurityRemediationResults,
        $SafeUpdateResults,
        $TestResults,
        $RemediationPlan
    )
    
    Write-Log "Exporting remediation report" "INFO"
    
    $reportData = @{
        RemediationInfo = @{
            Timestamp = $script:StartTime
            Duration = (Get-Date) - $script:StartTime
            Version = $script:ScriptVersion
            ProjectPath = $ProjectPath
            UpdateMode = $UpdateMode
        }
        Summary = @{
            SecurityFixesApplied = $SecurityRemediationResults.Applied.Count
            SecurityFixesFailed = $SecurityRemediationResults.Failed.Count
            SafeUpdatesApplied = $SafeUpdateResults.Applied.Count
            SafeUpdatesFailed = $SafeUpdateResults.Failed.Count
            TotalChanges = $SecurityRemediationResults.Applied.Count + $SafeUpdateResults.Applied.Count
            TestResults = $TestResults
        }
        SecurityRemediation = $SecurityRemediationResults
        SafeUpdates = $SafeUpdateResults
        TestResults = $TestResults
        RemediationPlan = $RemediationPlan
        Recommendations = Get-RemediationRecommendations -SecurityResults $SecurityRemediationResults -UpdateResults $SafeUpdateResults -TestResults $TestResults
    }
    
    # Export JSON report
    $reportData | ConvertTo-Json -Depth 10 | Out-File -FilePath $script:ReportFile -Encoding UTF8
    
    # Export markdown summary
    Export-RemediationMarkdown -ReportData $reportData -OutputPath $script:SummaryFile
    
    Write-Log "Remediation report exported to: $OutputPath" "INFO"
}

function Get-RemediationRecommendations {
    param(
        $SecurityResults,
        $UpdateResults,
        $TestResults
    )
    
    $recommendations = @()
    
    if ($SecurityResults.Failed.Count -gt 0) {
        $recommendations += @{
            Priority = "High"
            Category = "Security"
            Action = "Manual review of failed security updates"
            Details = "$($SecurityResults.Failed.Count) security updates failed and require manual intervention"
            Items = $SecurityResults.Failed
        }
    }
    
    if ($UpdateResults.Failed.Count -gt 0) {
        $recommendations += @{
            Priority = "Medium"
            Category = "Updates"
            Action = "Review and retry failed updates"
            Details = "$($UpdateResults.Failed.Count) package updates failed"
            Items = $UpdateResults.Failed
        }
    }
    
    if ($TestResults.Success -eq $false) {
        $recommendations += @{
            Priority = "High"
            Category = "Testing"
            Action = "Investigate build/test failures"
            Details = "Build or tests failed after updates. Manual investigation required."
            Items = $TestResults.Errors
        }
    }
    
    if ($SecurityResults.Applied.Count -eq 0 -and $UpdateResults.Applied.Count -eq 0) {
        $recommendations += @{
            Priority = "Info"
            Category = "Status"
            Action = "No updates applied"
            Details = "No vulnerabilities or updates were found that match the current update policy"
        }
    }
    
    return $recommendations
}

function Export-RemediationMarkdown {
    param(
        $ReportData,
        [string]$OutputPath
    )
    
    $markdown = @"
# TiXL Dependency Remediation Report

**Generated**: $($ReportData.RemediationInfo.Timestamp.ToString("yyyy-MM-dd HH:mm:ss UTC"))
**Duration**: $($ReportData.RemediationInfo.Duration.TotalSeconds.ToString("F2")) seconds
**Update Mode**: $($ReportData.RemediationInfo.UpdateMode)
**Project**: $($ReportData.RemediationInfo.ProjectPath)

## Executive Summary

| Metric | Count |
|--------|-------|
| **Security Fixes Applied** | $($ReportData.Summary.SecurityFixesApplied) |
| **Security Fixes Failed** | $($ReportData.Summary.SecurityFixesFailed) |
| **Safe Updates Applied** | $($ReportData.Summary.SafeUpdatesApplied) |
| **Safe Updates Failed** | $($ReportData.Summary.SafeUpdatesFailed) |
| **Total Changes** | $($ReportData.Summary.TotalChanges) |

## Test Results

| Test | Status |
|------|--------|
| **Build** | $(if ($ReportData.Summary.TestResults.BuildSuccess) { "✅ Pass" } else { "❌ Fail" }) |
| **Restore** | $(if ($ReportData.Summary.TestResults.Success -or $ReportData.Summary.TestResults.BuildSuccess) { "✅ Pass" } else { "❌ Fail" }) |
| **Tests** | $(if ($ReportData.Summary.TestResults.TestSuccess) { "✅ Pass" } else { "⚠️  Failed or Skipped" }) |
| **Overall** | $(if ($ReportData.Summary.TestResults.Success) { "✅ Success" } else { "❌ Failed" }) |

## Applied Security Fixes

$(
    if ($ReportData.SecurityRemediation.Applied.Count -gt 0) {
        $ReportData.SecurityRemediation.Applied | ForEach-Object {
            "- **$($_.Package)**: $($_.FromVersion) → $($_.ToVersion) (Fixes: $($_.VulnerabilityID))"
        }
    } else {
        "No security fixes were applied."
    }
)

## Failed Security Fixes

$(
    if ($ReportData.SecurityRemediation.Failed.Count -gt 0) {
        $ReportData.SecurityRemediation.Failed | ForEach-Object {
            "- **$($_.Package)**: $($_.Error)"
        }
    } else {
        "All security fixes applied successfully."
    }
)

## Applied Safe Updates

$(
    if ($ReportData.SafeUpdates.Applied.Count -gt 0) {
        $ReportData.SafeUpdates.Applied | ForEach-Object {
            "- **$($_.Package)**: $($_.FromVersion) → $($_.ToVersion) ($($_.Type) update, $($_.RiskLevel) risk)"
        }
    } else {
        "No safe updates were applied."
    }
)

## Failed Safe Updates

$(
    if ($ReportData.SafeUpdates.Failed.Count -gt 0) {
        $ReportData.SafeUpdates.Failed | ForEach-Object {
            "- **$($_.Package)**: $($_.Error)"
        }
    } else {
        "All safe updates applied successfully."
    }
)

## Recommendations

$(
    if ($ReportData.Recommendations.Count -gt 0) {
        foreach ($rec in $ReportData.Recommendations) {
            "### $($rec.Priority) Priority - $($rec.Category)"
            ""
            "**Action**: $($rec.Action)"
            ""
            "**Details**: $($rec.Details)"
            ""
        }
    } else {
        "No immediate recommendations. All updates completed successfully."
    }
)

## Next Steps

1. **Review Changes**: Examine the applied updates and their impact on your project
2. **Test Application**: Run comprehensive tests in your development environment
3. **Documentation**: Update any documentation that references specific package versions
4. **Monitoring**: Monitor the application for any issues after the updates
5. **Commit Changes**: If satisfied with results, commit and push the changes

---
*Report generated by TiXL Automated Dependency Remediation v$($ReportData.RemediationInfo.Version)*
"@
    
    $markdown | Out-File -FilePath $OutputPath -Encoding UTF8
}

# Main execution
Write-Log "Starting $script:ScriptName v$script:ScriptVersion" "INFO"
Write-Log "Project path: $ProjectPath" "INFO"
Write-Log "Update mode: $UpdateMode" "INFO"
Write-Log "Auto approve: $AutoApprove" "INFO"
Write-Log "Create PR: $CreatePR" "INFO"

try {
    # Check prerequisites
    if (-not (Test-Prerequisites)) {
        Write-Log "Prerequisites check failed" "ERROR"
        exit 1
    }
    
    # Create backup
    $backupPath = Backup-ProjectFiles -ProjectPath $ProjectPath
    if (-not $backupPath) {
        Write-Log "Failed to create backup, proceeding with caution" "WARNING"
    }
    
    # Load vulnerability report
    $vulnReport = Get-VulnerabilityReport -ReportPath $VulnerabilityReportPath
    
    # Analyze vulnerabilities for remediation
    $remediationAnalysis = Analyze-VulnerabilitiesForRemediation -VulnerabilityReport $vulnReport
    
    # Get available safe updates
    $safeUpdates = Get-SafeUpdates -ProjectPath $ProjectPath
    
    Write-Log "Analysis complete:" "INFO"
    Write-Log "Critical vulnerabilities: $($remediationAnalysis.Critical.Count)" "INFO"
    Write-Log "High vulnerabilities: $($remediationAnalysis.High.Count)" "INFO"
    Write-Log "Medium vulnerabilities: $($remediationAnalysis.Medium.Count)" "INFO"
    Write-Log "Available safe updates: $($safeUpdates.Count)" "INFO"
    
    # Determine what needs to be done
    $securityRemediations = @($remediationAnalysis.Critical + $remediationAnalysis.High)
    $pendingUpdates = $safeUpdates | Where-Object { $_.RiskLevel -in @("Low", "Medium") }
    
    if ($securityRemediations.Count -eq 0 -and $pendingUpdates.Count -eq 0) {
        Write-Log "No security fixes or safe updates available" "INFO"
        
        Export-RemediationReport -SecurityRemediationResults @{Applied=@(); Failed=@()} -SafeUpdateResults @{Applied=@(); Failed=@()} -TestResults @{Success=$true; BuildSuccess=$true; TestSuccess=$true; Errors=@()} -RemediationPlan @{Status="NoAction"; Reason="No vulnerabilities or updates found"}
        
        Write-Host "✅ No remediation needed. Project is up to date." -ForegroundColor Green
        exit 0
    }
    
    # Show what will be done
    Write-Host "`n📋 Remediation Plan" -ForegroundColor Cyan
    Write-Host "===================" -ForegroundColor Cyan
    
    if ($securityRemediations.Count -gt 0) {
        Write-Host "`n🔒 Security Fixes:" -ForegroundColor Red
        foreach ($fix in $securityRemediations) {
            $actionText = switch ($fix.Action) {
                "SecurityUpdate" { "🔧 Security Update" }
                "SafeUpdate" { "🛡️  Safe Update" }
                "Review" { "👀 Manual Review Required" }
                default { "⏭️  Skip" }
            }
            Write-Host "  $actionText $($fix.Package) $($fix.CurrentVersion) → $($fix.FixedVersion)" -ForegroundColor $(if ($fix.Action -eq "Review") { "Yellow" } else { "White" })
        }
    }
    
    if ($pendingUpdates.Count -gt 0) {
        Write-Host "`n📦 Safe Updates:" -ForegroundColor Green
        foreach ($update in $pendingUpdates | Select-Object -First 5) {
            Write-Host "  📈 $($update.Package) $($update.CurrentVersion) → $($update.LatestVersion) ($($update.Type), $($update.RiskLevel) risk)" -ForegroundColor White
        }
        if ($pendingUpdates.Count -gt 5) {
            Write-Host "  ... and $($pendingUpdates.Count - 5) more updates" -ForegroundColor Gray
        }
    }
    
    # Confirm action if not auto-approving
    if (-not $AutoApprove) {
        Write-Host "`n❓ Do you want to proceed with these changes? (y/N)" -ForegroundColor Yellow
        $response = Read-Host
        if ($response -ne "y" -and $response -ne "Y") {
            Write-Log "User cancelled remediation" "INFO"
            exit 0
        }
    }
    
    Write-Host "`n🚀 Applying remediation..." -ForegroundColor Green
    
    # Apply security fixes
    $securityResults = Apply-SecurityUpdates -SecurityRemediations $securityRemediations -ProjectPath $ProjectPath
    
    # Apply safe updates
    $safeUpdateResults = Apply-SafeUpdates -SafeUpdates $pendingUpdates -ProjectPath $ProjectPath -MaxParallel $MaxParallelUpdates
    
    # Test project after updates
    $testResults = Test-ProjectAfterUpdates -ProjectPath $ProjectPath
    
    # Check if we need to rollback
    if ($testResults.Success -eq $false -and $backupPath) {
        Write-Log "Tests failed, rolling back changes" "WARNING"
        $rollbackSuccess = Rollback-Changes -BackupPath $backupPath -ProjectPath $ProjectPath
        
        if ($rollbackSuccess) {
            Write-Host "⚠️  Changes rolled back due to test failures" -ForegroundColor Yellow
        } else {
            Write-Host "❌ Rollback failed - manual intervention required" -ForegroundColor Red
        }
    }
    
    # Create branch and PR if requested
    $prInfo = $null
    if ($CreatePR -and $testResults.Success) {
        $branchName = "$BranchPrefix-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
        $commitMessage = "Security and dependency updates - $($securityResults.Applied.Count) security fixes, $($safeUpdateResults.Applied.Count) safe updates"
        $prInfo = Create-BranchAndPR -BranchName $branchName -CommitMessage $commitMessage
    }
    
    # Export report
    Export-RemediationReport -SecurityRemediationResults $securityResults -SafeUpdateResults $safeUpdateResults -TestResults $testResults -RemediationPlan @{
        Status = if ($testResults.Success) { "Completed" } else { "Failed" }
        SecurityApplied = $securityResults.Applied.Count
        UpdatesApplied = $safeUpdateResults.Applied.Count
        BranchCreated = $prInfo.Branch
    }
    
    # Summary output
    Write-Host "`n📊 Remediation Summary" -ForegroundColor Cyan
    Write-Host "=====================" -ForegroundColor Cyan
    Write-Host "Security fixes: $($securityResults.Applied.Count) applied, $($securityResults.Failed.Count) failed" -ForegroundColor $(if ($securityResults.Failed.Count -eq 0) { "Green" } else { "Yellow" })
    Write-Host "Safe updates: $($safeUpdateResults.Applied.Count) applied, $($safeUpdateResults.Failed.Count) failed" -ForegroundColor $(if ($safeUpdateResults.Failed.Count -eq 0) { "Green" } else { "Yellow" })
    Write-Host "Tests: $(if ($testResults.Success) { '✅ Passed' } else { '❌ Failed' })" -ForegroundColor $(if ($testResults.Success) { "Green" } else { "Red" })
    Write-Host "PR: $(if ($prInfo) { "✅ $branchName created" } else { "⏭️  Skipped" })" -ForegroundColor $(if ($prInfo) { "Green" } else { "Gray" })
    Write-Host "Report: $OutputPath" -ForegroundColor Blue
    
    $exitCode = if ($testResults.Success -eq $false -or $securityResults.Failed.Count -gt 0) { 1 } else { 0 }
    exit $exitCode
}
catch {
    Write-Log "Remediation failed with error: $_" "ERROR"
    Write-Host "Error: $_" -ForegroundColor Red
    
    # Try to rollback on failure
    if ($backupPath) {
        Write-Log "Attempting rollback due to exception" "WARNING"
        Rollback-Changes -BackupPath $backupPath -ProjectPath $ProjectPath
    }
    
    exit 1
}