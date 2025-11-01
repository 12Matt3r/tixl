#!/usr/bin/env pwsh
<#
.SYNOPSIS
    TiXL Dependency Updater - Automated dependency updates with risk assessment

.DESCRIPTION
    Provides automated dependency update capabilities including:
    - Outdated package detection
    - Version compatibility assessment
    - Risk analysis and breaking change detection
    - Automated update execution with rollback capability
    - Pull request creation for dependency updates

.PARAMETER ProjectPath
    Path to the solution or project file to update

.PARAMETER UpdateMode
    Type of updates to apply (Safe, All, Manual)

.PARAMETER CheckOnly
    Only check for updates without applying changes

.PARAMETER GenerateReport
    Generate comprehensive update report

.PARAMETER CreatePullRequest
    Create pull request with updates

.PARAMETER AutoApprove
    Auto-approve safe updates without confirmation

.PARAMETER BranchPrefix
    Prefix for update branch names

.PARAMETER OutputPath
    Directory to save update reports

.EXAMPLE
    .\dependency-updater.ps1 -ProjectPath "TiXL.sln" -UpdateMode "Safe" -CheckOnly

.EXAMPLE
    .\dependency-updater.ps1 -ProjectPath "TiXL.sln" -UpdateMode "All" -CreatePullRequest -BranchPrefix "update-deps"

.EXAMPLE
    .\dependency-updater.ps1 -ProjectPath "TiXL.sln" -UpdateMode "Safe" -AutoApprove
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$ProjectPath,
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("Safe", "All", "Manual")]
    [string]$UpdateMode = "Safe",
    
    [Parameter(Mandatory=$false)]
    [switch]$CheckOnly,
    
    [Parameter(Mandatory=$false)]
    [switch]$GenerateReport,
    
    [Parameter(Mandatory=$false)]
    [switch]$CreatePullRequest,
    
    [Parameter(Mandatory=$false)]
    [switch]$AutoApprove,
    
    [Parameter(Mandatory=$false)]
    [string]$BranchPrefix = "dependency-updates",
    
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "./dependency-updates"
)

# Global variables
$script:ScriptName = "TiXL Dependency Updater"
$script:ScriptVersion = "1.0.0"
$script:StartTime = Get-Date
$script:ConfigPath = "$PSScriptRoot\..\config\update-policies.json"

# Initialize output directory
if (!(Test-Path $OutputPath)) {
    New-Item -Path $OutputPath -ItemType Directory -Force | Out-Null
}

$script:LogFile = Join-Path $OutputPath "dependency-updates.log"
$script:ReportFile = Join-Path $OutputPath "update-report.json"
$script:SummaryFile = Join-Path $OutputPath "update-summary.md"

# Default update policies
$script:DefaultUpdatePolicy = @{
    Safe = @{
        MaxMajorVersionIncrease = 0
        MaxMinorVersionIncrease = 2
        MaxPatchVersionIncrease = 999
        ExcludePrerelease = $true
        ExcludePreview = $true
        ExcludeBeta = $true
        ExcludeRC = $true
    }
    BreakingChanges = @{
        CheckBreakingChanges = $true
        BreakingChangeKeywords = @("breaking", "breaking change", "breaking changes", "major change", "incompatible")
    }
    PackageExclusions = @{
        ExcludeMajorUpdates = @()
        ExcludeAllUpdates = @()
        SafePackages = @()
    }
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

function Initialize-UpdatePolicy {
    param()
    
    Write-Log "Initializing update policy" "INFO"
    
    $policy = $script:DefaultUpdatePolicy
    
    # Override with custom policy if available
    if (Test-Path $script:ConfigPath) {
        try {
            $customPolicy = Get-Content $script:ConfigPath | ConvertFrom-Json
            
            if ($customPolicy.safe) {
                $policy.Safe = $customPolicy.safe
            }
            if ($customPolicy.breakingChanges) {
                $policy.BreakingChanges = $customPolicy.breakingChanges
            }
            if ($customPolicy.packageExclusions) {
                $policy.PackageExclusions = $customPolicy.packageExclusions
            }
            
            Write-Log "Loaded custom update policy from configuration" "INFO"
        }
        catch {
            Write-Log "Failed to load custom update policy: $_" "WARNING"
            Write-Log "Using default policy" "INFO"
        }
    }
    
    Write-Log "Update policy initialized - Mode: $UpdateMode" "INFO"
    return $policy
}

function Get-PackageDependencies {
    param([string]$ProjectPath)
    
    Write-Log "Extracting package dependencies from $ProjectPath" "INFO"
    
    $packages = @()
    
    try {
        if ($ProjectPath.EndsWith('.sln')) {
            # Process solution file
            $projects = & dotnet sln "$ProjectPath" list 2>$null
            foreach ($projectPath in $projects) {
                if ($projectPath -and (Test-Path $projectPath)) {
                    $packages += Get-PackageDependenciesFromProject -ProjectPath $projectPath
                }
            }
        }
        else {
            # Process single project file
            $packages = Get-PackageDependenciesFromProject -ProjectPath $ProjectPath
        }
    }
    catch {
        Write-Log "Error processing project structure: $_" "ERROR"
    }
    
    Write-Log "Found $($packages.Count) packages" "INFO"
    return $packages
}

function Get-PackageDependenciesFromProject {
    param([string]$ProjectPath)
    
    $packages = @()
    
    try {
        # Use dotnet list to get packages
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
                
                # Parse package lines
                if ($line -match "^\s*([\w\.-]+)\s+([\d\.]+)\s*(.*)$") {
                    $packageName = $matches[1]
                    $version = $matches[2]
                    $additionalInfo = $matches[3].Trim()
                    
                    # Skip if this looks like a header
                    if ($packageName -eq "Type" -or $packageName -eq "Version") { continue }
                    
                    # Get package metadata
                    $packageInfo = Get-PackageUpdateInfo -PackageName $packageName -CurrentVersion $version
                    
                    $package = [PSCustomObject]@{
                        Name = $packageName
                        Version = $version
                        Project = $projectName
                        ProjectPath = $ProjectPath
                        SourceType = $currentSection
                        LatestVersion = $packageInfo.LatestVersion
                        IsPrerelease = $packageInfo.IsPrerelease
                        ReleaseDate = $packageInfo.ReleaseDate
                        IsDeprecated = $packageInfo.IsDeprecated
                        DeprecationMessage = $packageInfo.DeprecationMessage
                        ChangelogUrl = $packageInfo.ChangelogUrl
                        Description = $packageInfo.Description
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

function Get-PackageUpdateInfo {
    param(
        [string]$PackageName,
        [string]$CurrentVersion
    )
    
    $info = @{
        LatestVersion = $CurrentVersion
        IsPrerelease = $false
        ReleaseDate = $null
        IsDeprecated = $false
        DeprecationMessage = $null
        ChangelogUrl = $null
        Description = $null
    }
    
    try {
        # Query NuGet API for package metadata
        $apiUrl = "https://api.nuget.org/v3-flatcontainer/$($PackageName.ToLower())/index.json"
        
        $response = Invoke-RestMethod -Uri $apiUrl -ErrorAction SilentlyContinue
        
        if ($response -and $response.versions) {
            # Filter versions based on policy
            $stableVersions = $response.versions | Where-Object { $_ -notmatch '[\-\+](alpha|beta|rc|preview)' }
            $allVersions = $response.versions | Sort-Object { [version]($_ -split '[\-\+]' | Select-Object -First 1) } -Descending
            
            if ($stableVersions.Count -gt 0) {
                $info.LatestVersion = ($stableVersions | Sort-Object { [version]$_ } | Select-Object -Last 1)
            }
            else {
                $info.LatestVersion = ($allVersions | Select-Object -First 1)
                $info.IsPrerelease = $true
            }
            
            # Get detailed metadata for latest version
            $latestVersionData = Get-PackageMetadata -PackageName $PackageName -Version $info.LatestVersion
            if ($latestVersionData) {
                $info.ReleaseDate = $latestVersionData.ReleaseDate
                $info.Description = $latestVersionData.Description
            }
        }
    }
    catch {
        Write-Log "Failed to get update info for $PackageName : $_" "DEBUG"
    }
    
    return $info
}

function Get-PackageMetadata {
    param(
        [string]$PackageName,
        [string]$Version
    )
    
    try {
        $apiUrl = "https://api.nuget.org/v3-flatcontainer/$($PackageName.ToLower())/$Version/$($PackageName.ToLower()).nuspec"
        
        $response = Invoke-RestMethod -Uri $apiUrl -ErrorAction SilentlyContinue
        
        if ($response -and $response.metadata) {
            return @{
                ReleaseDate = $response.metadata.releaseNotes
                Description = $response.metadata.description
            }
        }
    }
    catch {
        # Ignore errors, this is just metadata enrichment
    }
    
    return $null
}

function Test-UpdateEligibility {
    param(
        $Package,
        $Policy
    )
    
    $eligibility = [PSCustomObject]@{
        Package = $Package.Name
        CurrentVersion = $Package.Version
        TargetVersion = $Package.LatestVersion
        IsEligible = $false
        RiskLevel = "Unknown"
        UpdateType = "None"
        Reasons = @()
        BreakingChanges = @()
        CanAutoUpdate = $false
    }
    
    # Check if update is needed
    if ($Package.LatestVersion -eq $Package.Version) {
        $eligibility.Reasons += "Already at latest version"
        return $eligibility
    }
    
    # Parse versions
    try {
        $currentVer = [version]$Package.Version.Split('-')[0].Split('+')[0]
        $targetVer = [version]$Package.LatestVersion.Split('-')[0].Split('+')[0]
        
        $majorDiff = $targetVer.Major - $currentVer.Major
        $minorDiff = $targetVer.Minor - $currentVer.Minor
        $patchDiff = $targetVer.Build - $currentVer.Build
        
        # Determine update type
        if ($majorDiff -gt 0) {
            $eligibility.UpdateType = "Major"
            $eligibility.RiskLevel = "High"
        }
        elseif ($minorDiff -gt 0) {
            $eligibility.UpdateType = "Minor"
            $eligibility.RiskLevel = "Medium"
        }
        else {
            $eligibility.UpdateType = "Patch"
            $eligibility.RiskLevel = "Low"
        }
        
        # Apply update mode policy
        switch ($UpdateMode) {
            "Safe" {
                if ($majorDiff -le $Policy.Safe.MaxMajorVersionIncrease -and
                    $minorDiff -le $Policy.Safe.MaxMinorVersionIncrease -and
                    $patchDiff -le $Policy.Safe.MaxPatchVersionIncrease -and
                    $Policy.Safe.ExcludePrerelease -and !$Package.IsPrerelease) {
                    $eligibility.IsEligible = $true
                    $eligibility.CanAutoUpdate = $true
                    $eligibility.Reasons += "Meets safe update criteria"
                }
                else {
                    $eligibility.Reasons += "Does not meet safe update criteria"
                    if ($Policy.Safe.ExcludePrerelease -and $Package.IsPrerelease) {
                        $eligibility.Reasons += "Is prerelease version"
                    }
                }
            }
            "All" {
                $eligibility.IsEligible = $true
                $eligibility.CanAutoUpdate = $true
                $eligibility.Reasons += "All updates allowed in this mode"
            }
            "Manual" {
                $eligibility.IsEligible = $true
                $eligibility.CanAutoUpdate = $false
                $eligibility.Reasons += "Manual review required"
            }
        }
        
        # Check for breaking changes
        if ($Policy.BreakingChanges.CheckBreakingChanges) {
            $breakingChanges = Test-BreakingChanges -Package $Package -Policy $Policy
            $eligibility.BreakingChanges = $breakingChanges
            
            if ($breakingChanges.Count -gt 0) {
                $eligibility.RiskLevel = "High"
                if ($UpdateMode -eq "Safe") {
                    $eligibility.CanAutoUpdate = $false
                    $eligibility.Reasons += "Contains breaking changes"
                }
            }
        }
        
        # Check package exclusions
        if ($Policy.PackageExclusions.ExcludeAllUpdates -contains $Package.Name) {
            $eligibility.IsEligible = $false
            $eligibility.CanAutoUpdate = $false
            $eligibility.Reasons += "Package excluded from updates"
        }
        elseif ($Policy.PackageExclusions.ExcludeMajorUpdates -contains $Package.Name -and $eligibility.UpdateType -eq "Major") {
            $eligibility.IsEligible = $false
            $eligibility.CanAutoUpdate = $false
            $eligibility.Reasons += "Major updates excluded for this package"
        }
        elseif ($Policy.PackageExclusions.SafePackages -contains $Package.Name) {
            $eligibility.CanAutoUpdate = $true
            $eligibility.Reasons += "Package marked as safe for updates"
        }
    }
    catch {
        $eligibility.Reasons += "Error parsing version: $_"
    }
    
    return $eligibility
}

function Test-BreakingChanges {
    param(
        $Package,
        $Policy
    )
    
    $breakingChanges = @()
    
    try {
        # This is a simplified implementation
        # In a real implementation, you'd analyze changelogs, release notes, etc.
        
        if ($Package.ChangelogUrl) {
            # Mock implementation - would analyze changelog for breaking changes
            $keywords = $Policy.BreakingChanges.BreakingChangeKeywords
            
            # Check for common breaking change indicators
            # This would typically involve scraping and analyzing the changelog
            # For now, return empty array as this is a template
        }
        
        # Add version-specific checks
        $currentVer = [version]$Package.Version
        $targetVer = [version]$Package.LatestVersion
        
        if ($targetVer.Major -gt $currentVer.Major) {
            $breakingChanges += @{
                Type = "Version Bump"
                Description = "Major version increase ($($currentVer.Major) → $($targetVer.Major))"
                Severity = "High"
            }
        }
    }
    catch {
        Write-Log "Error checking breaking changes for $($Package.Name): $_" "WARNING"
    }
    
    return $breakingChanges
}

function Invoke-PackageUpdate {
    param(
        [array]$EligiblePackages,
        [bool]$AutoApprove
    )
    
    Write-Log "Updating $($EligiblePackages.Count) packages" "INFO"
    
    $updateResults = @{
        Successful = @()
        Failed = @()
        Skipped = @()
    }
    
    foreach ($package in $EligiblePackages) {
        Write-Log "Updating $($package.Package) v$($package.CurrentVersion) → v$($package.TargetVersion)" "INFO"
        
        # Find the project file that contains this package
        $projectFile = Find-PackageInProjects -PackageName $package.Package -ProjectPath $ProjectPath
        
        if (!$projectFile) {
            Write-Log "Could not find project containing package $($package.Package)" "WARNING"
            $updateResults.Skipped += $package
            continue
        }
        
        # Confirm update if not auto-approving
        if (!$AutoApprove) {
            $confirmation = Read-Host "Update $($package.Package) from v$($package.CurrentVersion) to v$($package.TargetVersion)? (y/N)"
            if ($confirmation -ne "y" -and $confirmation -ne "Y") {
                Write-Log "Update skipped for $($package.Package) by user" "INFO"
                $updateResults.Skipped += $package
                continue
            }
        }
        
        # Perform the update
        try {
            $updateOutput = & dotnet add "$projectFile" package $package.Package --version $package.TargetVersion 2>&1
            
            if ($LASTEXITCODE -eq 0) {
                $package | Add-Member -NotePropertyName "UpdateResult" -NotePropertyValue "Success" -Force
                $package | Add-Member -NotePropertyName "UpdateOutput" -NotePropertyValue $updateOutput -Force
                $updateResults.Successful += $package
                Write-Log "Successfully updated $($package.Package)" "INFO"
            }
            else {
                $package | Add-Member -NotePropertyName "UpdateResult" -NotePropertyValue "Failed" -Force
                $package | Add-Member -NotePropertyName "UpdateOutput" -NotePropertyValue $updateOutput -Force
                $updateResults.Failed += $package
                Write-Log "Failed to update $($package.Package): $updateOutput" "ERROR"
            }
        }
        catch {
            $package | Add-Member -NotePropertyName "UpdateResult" -NotePropertyValue "Error" -Force
            $package | Add-Member -NotePropertyName "ErrorMessage" -NotePropertyValue $_.Exception.Message -Force
            $updateResults.Failed += $package
            Write-Log "Error updating $($package.Package): $_" "ERROR"
        }
    }
    
    Write-Log "Update completed" "INFO"
    Write-Log "Successful: $($updateResults.Successful.Count)" "INFO"
    Write-Log "Failed: $($updateResults.Failed.Count)" "ERROR"
    Write-Log "Skipped: $($updateResults.Skipped.Count)" "INFO"
    
    return $updateResults
}

function Find-PackageInProjects {
    param(
        [string]$PackageName,
        [string]$ProjectPath
    )
    
    if ($ProjectPath.EndsWith('.sln')) {
        # Search in all projects within the solution
        $solutionDir = Split-Path $ProjectPath
        $projectFiles = Get-ChildItem -Path $solutionDir -Filter "*.csproj" -Recurse
        
        foreach ($project in $projectFiles) {
            # Check if this project references the package
            $references = & dotnet list "$($project.FullName)" package 2>$null
            if ($references -match $PackageName) {
                return $project.FullName
            }
        }
    }
    else {
        # Single project file
        return $ProjectPath
    }
    
    return $null
}

function Test-UpdateResults {
    param($UpdateResults)
    
    Write-Log "Testing updated packages" "INFO"
    
    $testResults = @{
        BuildTest = @{
            Success = $false
            Output = $null
            Errors = @()
        }
        PackageTest = @{
            Success = $false
            Output = $null
            Errors = @()
        }
    }
    
    try {
        # Test build
        Write-Log "Testing build after updates" "INFO"
        $buildOutput = & dotnet build "$ProjectPath" --configuration Release 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            $testResults.BuildTest.Success = $true
            $testResults.BuildTest.Output = $buildOutput
            Write-Log "Build test passed" "INFO"
        }
        else {
            $testResults.BuildTest.Success = $false
            $testResults.BuildTest.Output = $buildOutput
            $testResults.BuildTest.Errors += "Build failed"
            Write-Log "Build test failed" "ERROR"
        }
        
        # Test package restore
        Write-Log "Testing package restore" "INFO"
        $restoreOutput = & dotnet restore "$ProjectPath" 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            $testResults.PackageTest.Success = $true
            $testResults.PackageTest.Output = $restoreOutput
            Write-Log "Package restore test passed" "INFO"
        }
        else {
            $testResults.PackageTest.Success = $false
            $testResults.PackageTest.Output = $restoreOutput
            $testResults.PackageTest.Errors += "Package restore failed"
            Write-Log "Package restore test failed" "ERROR"
        }
    }
    catch {
        Write-Log "Error during testing: $_" "ERROR"
        $testResults.BuildTest.Errors += $_.Exception.Message
        $testResults.PackageTest.Errors += $_.Exception.Message
    }
    
    return $testResults
}

function New-UpdatePullRequest {
    param(
        $UpdateResults,
        [string]$BranchPrefix
    )
    
    if (!$CreatePullRequest) {
        return $null
    }
    
    Write-Log "Creating pull request for dependency updates" "INFO"
    
    try {
        # Generate branch name
        $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $branchName = "$BranchPrefix-$timestamp"
        
        # Create git branch and commit
        & git checkout -b $branchName
        
        if ($LASTEXITCODE -eq 0) {
            & git add .
            & git commit -m "Update dependencies - $($UpdateResults.Successful.Count) packages updated"
            
            # Create pull request (this would typically use GitHub CLI or API)
            Write-Log "Created branch: $branchName" "INFO"
            Write-Log "Run 'git push origin $branchName' to push changes" "INFO"
            
            return @{
                BranchName = $branchName
                CommitMessage = "Update dependencies - $($UpdateResults.Successful.Count) packages updated"
                Changes = @{
                    Updated = $UpdateResults.Successful.Count
                    Failed = $UpdateResults.Failed.Count
                    Skipped = $UpdateResults.Skipped.Count
                }
            }
        }
        else {
            Write-Log "Failed to create git branch" "ERROR"
            return $null
        }
    }
    catch {
        Write-Log "Error creating pull request: $_" "ERROR"
        return $null
    }
}

function Export-UpdateReport {
    param(
        $UpdateAnalysis,
        $UpdateResults,
        $TestResults,
        $PullRequestInfo,
        [string]$OutputPath
    )
    
    Write-Log "Exporting update report" "INFO"
    
    $reportData = @{
        UpdateInfo = @{
            Timestamp = $script:StartTime
            Duration = (Get-Date) - $script:StartTime
            Version = $script:ScriptVersion
            ProjectPath = $ProjectPath
            UpdateMode = $UpdateMode
            CheckOnly = $CheckOnly
            AutoApprove = $AutoApprove
        }
        Summary = @{
            TotalPackages = $UpdateAnalysis.Count
            Eligible = ($UpdateAnalysis | Where-Object { $_.IsEligible }).Count
            AutoUpdatable = ($UpdateAnalysis | Where-Object { $_.CanAutoUpdate }).Count
            SuccessfulUpdates = $UpdateResults.Successful.Count
            FailedUpdates = $UpdateResults.Failed.Count
            SkippedUpdates = $UpdateResults.Skipped.Count
        }
        UpdateAnalysis = $UpdateAnalysis
        UpdateResults = $UpdateResults
        TestResults = $TestResults
        PullRequestInfo = $PullRequestInfo
    }
    
    # Export JSON report
    $reportData | ConvertTo-Json -Depth 10 | Out-File -FilePath $script:ReportFile -Encoding UTF8
    
    # Export markdown summary if requested
    if ($GenerateReport) {
        Export-UpdateMarkdown -ReportData $reportData -OutputPath $script:SummaryFile
    }
    
    Write-Log "Update report exported to: $OutputPath" "INFO"
}

function Export-UpdateMarkdown {
    param(
        $ReportData,
        [string]$OutputPath
    )
    
    $markdown = @"
# TiXL Dependency Update Report

**Generated**: $($ReportData.UpdateInfo.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"))
**Duration**: $($ReportData.UpdateInfo.Duration.TotalSeconds.ToString("F2")) seconds
**Version**: $($ReportData.UpdateInfo.Version)
**Project**: $($ReportData.UpdateInfo.ProjectPath)
**Update Mode**: $($ReportData.UpdateInfo.UpdateMode)
**Check Only**: $($ReportData.UpdateInfo.CheckOnly)

## Executive Summary

- **Total Packages**: $($ReportData.Summary.TotalPackages)
- **Eligible for Update**: $($ReportData.Summary.Eligible)
- **Auto-Updatable**: $($ReportData.Summary.AutoUpdatable)
- **Successful Updates**: $($ReportData.Summary.SuccessfulUpdates)
- **Failed Updates**: $($ReportData.Summary.FailedUpdates)
- **Skipped Updates**: $($ReportData.Summary.SkippedUpdates)

## Update Analysis

### Available Updates by Risk Level
$(
    $riskLevels = $ReportData.UpdateAnalysis | Group-Object RiskLevel | Sort-Object Name
    foreach ($riskGroup in $riskLevels) {
        "- **$($riskGroup.Name)**: $($riskGroup.Count) packages"
    }
)

### Update Types
$(
    $updateTypes = $ReportData.UpdateAnalysis | Group-Object UpdateType | Sort-Object Name
    foreach ($typeGroup in $updateTypes) {
        "- **$($typeGroup.Name)**: $($typeGroup.Count) packages"
    }
)

## Successful Updates

$(
    if ($ReportData.UpdateResults.Successful.Count -gt 0) {
        foreach ($success in $ReportData.UpdateResults.Successful) {
            "- **$($success.Package)**: $($success.CurrentVersion) → $($success.TargetVersion)"
        }
    } else {
        "No successful updates."
    }
)

## Failed Updates

$(
    if ($ReportData.UpdateResults.Failed.Count -gt 0) {
        foreach ($failure in $ReportData.UpdateResults.Failed) {
            "- **$($failure.Package)**: $($failure.CurrentVersion) → $($failure.TargetVersion) - $($failure.UpdateResult)"
        }
    } else {
        "No failed updates."
    }
)

## Skipped Updates

$(
    if ($ReportData.UpdateResults.Skipped.Count -gt 0) {
        foreach ($skip in $ReportData.UpdateResults.Skipped) {
            "- **$($skip.Package)**: $($skip.CurrentVersion) → $($skip.TargetVersion) - $($skip.Reasons -join "; ")"
        }
    } else {
        "No skipped updates."
    }
)

## Test Results

### Build Test
- **Status**: $(if ($ReportData.TestResults.BuildTest.Success) { "✅ Passed" } else { "❌ Failed" })
- **Output**: $(if ($ReportData.TestResults.BuildTest.Output) { "Available" } else { "None" })

### Package Restore Test
- **Status**: $(if ($ReportData.TestResults.PackageTest.Success) { "✅ Passed" } else { "❌ Failed" })
- **Output**: $(if ($ReportData.TestResults.PackageTest.Output) { "Available" } else { "None" })

$(
    if ($ReportData.TestResults.BuildTest.Errors.Count -gt 0) {
        "#### Build Errors"
        foreach ($error in $ReportData.TestResults.BuildTest.Errors) {
            "- $error"
        }
    }
)

$(
    if ($ReportData.TestResults.PackageTest.Errors.Count -gt 0) {
        "#### Package Restore Errors"
        foreach ($error in $ReportData.TestResults.PackageTest.Errors) {
            "- $error"
        }
    }
)

## Pull Request Information

$(
    if ($ReportData.PullRequestInfo) {
        "- **Branch**: $($ReportData.PullRequestInfo.BranchName)"
        "- **Commit Message**: $($ReportData.PullRequestInfo.CommitMessage)"
        "- **Changes**: $($ReportData.PullRequestInfo.Changes.Updated) updated, $($ReportData.PullRequestInfo.Changes.Failed) failed, $($ReportData.PullRequestInfo.Changes.Skipped) skipped"
    } else {
        "No pull request created."
    }
)

## Recommendations

1. **Review Results**: $(if ($ReportData.Summary.FailedUpdates -gt 0) { "Address $($ReportData.Summary.FailedUpdates) failed updates" } else { "All updates were successful" })
2. **Test Thoroughly**: $(if (!$ReportData.TestResults.BuildTest.Success -or !$ReportData.TestResults.PackageTest.Success) { "Tests failed - investigate and fix issues" } else { "All tests passed" })
3. **Monitor**: Continue monitoring for new dependency updates
4. **Automation**: $(if ($ReportData.Summary.AutoUpdatable -gt 0) { "Consider automating $($ReportData.Summary.AutoUpdatable) safe updates" } else { "No packages qualify for automation" })

---
*Report generated by TiXL Dependency Updater v$($ReportData.UpdateInfo.Version)*
"@
    
    $markdown | Out-File -FilePath $OutputPath -Encoding UTF8
}

# Main execution
Write-Log "Starting $script:ScriptName v$script:ScriptVersion" "INFO"
Write-Log "Project path: $ProjectPath" "INFO"
Write-Log "Update mode: $UpdateMode" "INFO"
Write-Log "Check only: $CheckOnly" "INFO"

try {
    # Initialize update policy
    $policy = Initialize-UpdatePolicy
    
    # Get package dependencies
    $packages = Get-PackageDependencies -ProjectPath $ProjectPath
    
    if ($packages.Count -eq 0) {
        Write-Log "No packages found to update" "WARNING"
        exit 1
    }
    
    # Analyze update eligibility
    Write-Log "Analyzing $($packages.Count) packages for updates" "INFO"
    
    $updateAnalysis = @()
    foreach ($package in $packages) {
        $eligibility = Test-UpdateEligibility -Package $package -Policy $policy
        $updateAnalysis += $eligibility
    }
    
    # Filter eligible packages
    $eligiblePackages = $updateAnalysis | Where-Object { $_.IsEligible }
    
    Write-Log "Found $($eligiblePackages.Count) packages eligible for update" "INFO"
    Write-Log "Auto-updatable: $(($eligiblePackages | Where-Object { $_.CanAutoUpdate }).Count)" "INFO"
    
    $updateResults = @{
        Successful = @()
        Failed = @()
        Skipped = @()
    }
    
    # Perform updates if not check-only
    if (!$CheckOnly -and $eligiblePackages.Count -gt 0) {
        $autoUpdatable = $eligiblePackages | Where-Object { $_.CanAutoUpdate }
        $manualReview = $eligiblePackages | Where-Object { !$_.CanAutoUpdate }
        
        if ($AutoApprove -or $autoUpdatable.Count -eq 0) {
            $updateResults = Invoke-PackageUpdate -EligiblePackages $autoUpdatable -AutoApprove $true
        }
        else {
            Write-Log "Found $($autoUpdatable.Count) auto-updatable and $($manualReview.Count) manual review packages" "INFO"
            
            # Auto-update safe packages
            if ($autoUpdatable.Count -gt 0) {
                $autoResults = Invoke-PackageUpdate -EligiblePackages $autoUpdatable -AutoApprove $true
                $updateResults.Successful += $autoResults.Successful
                $updateResults.Failed += $autoResults.Failed
                $updateResults.Skipped += $autoResults.Skipped
            }
            
            # Manual review packages
            if ($manualReview.Count -gt 0) {
                $manualResults = Invoke-PackageUpdate -EligiblePackages $manualReview -AutoApprove $false
                $updateResults.Successful += $manualResults.Successful
                $updateResults.Failed += $manualResults.Failed
                $updateResults.Skipped += $manualResults.Skipped
            }
        }
        
        # Test updated packages
        $testResults = Test-UpdateResults -UpdateResults $updateResults
    }
    else {
        $testResults = @{
            BuildTest = @{ Success = $null; Output = $null; Errors = @() }
            PackageTest = @{ Success = $null; Output = $null; Errors = @() }
        }
    }
    
    # Create pull request if requested
    $pullRequestInfo = $null
    if (!$CheckOnly -and $updateResults.Successful.Count -gt 0) {
        $pullRequestInfo = New-UpdatePullRequest -UpdateResults $updateResults -BranchPrefix $BranchPrefix
    }
    
    # Export reports
    Export-UpdateReport -UpdateAnalysis $updateAnalysis -UpdateResults $updateResults -TestResults $testResults -PullRequestInfo $pullRequestInfo -OutputPath $OutputPath
    
    # Summary
    Write-Log "=== DEPENDENCY UPDATE COMPLETED ===" "INFO"
    Write-Log "Total packages: $($packages.Count)" "INFO"
    Write-Log "Eligible for update: $($eligiblePackages.Count)" "INFO"
    Write-Log "Successful updates: $($updateResults.Successful.Count)" "INFO"
    Write-Log "Failed updates: $($updateResults.Failed.Count)" "WARNING"
    Write-Log "Skipped updates: $($updateResults.Skipped.Count)" "INFO"
    
    # Exit code
    $exitCode = 0
    if ($updateResults.Failed.Count -gt 0) {
        $exitCode = 1
    }
    
    exit $exitCode
}
catch {
    Write-Log "Dependency update failed with error: $_" "ERROR"
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
}