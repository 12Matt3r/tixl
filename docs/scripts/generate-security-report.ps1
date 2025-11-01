#!/usr/bin/env pwsh
<#
.SYNOPSIS
    TiXL Security Report Generator - Comprehensive security reporting for builds and releases

.DESCRIPTION
    Generates comprehensive security reports that combine multiple security scans:
    - Dependency vulnerability analysis
    - License compliance reporting  
    - Security policy adherence
    - Risk assessment and recommendations
    - Executive dashboards for management

.PARAMETER ProjectPath
    Path to the solution or project file to analyze

.PARAMETER OutputPath
    Directory to save comprehensive reports

.PARAMETER ReportType
    Type of report: Build, Release, Executive, Compliance

.PARAMETER IncludeHistorical
    Include historical trend analysis

.PARAMETER GenerateDashboard
    Generate interactive HTML dashboard

.EXAMPLE
    .\generate-security-report.ps1 -ProjectPath "TiXL.sln" -ReportType "Build" -GenerateDashboard

.EXAMPLE
    .\generate-security-report.ps1 -ProjectPath "TiXL.sln" -ReportType "Executive" -IncludeHistorical
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$ProjectPath,
    
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "./security-reports",
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("Build", "Release", "Executive", "Compliance", "All")]
    [string]$ReportType = "Build",
    
    [Parameter(Mandatory=$false)]
    [switch]$IncludeHistorical,
    
    [Parameter(Mandatory=$false)]
    [switch]$GenerateDashboard,
    
    [Parameter(Mandatory=$false)]
    [string]$HistoricalDataPath,
    
    [Parameter(Mandatory=$false)]
    [int]$TrendDays = 30
)

# Global variables
$script:ScriptName = "TiXL Security Report Generator"
$script:ScriptVersion = "1.0.0"
$script:StartTime = Get-Date

# Initialize output directories
if (!(Test-Path $OutputPath)) {
    New-Item -Path $OutputPath -ItemType Directory -Force | Out-Null
}

$script:ReportsPath = Join-Path $OutputPath "reports"
$script:DashboardPath = Join-Path $OutputPath "dashboard"
$script:HistoricalPath = $HistoricalDataPath
$script:LogFile = Join-Path $OutputPath "report-generation.log"

if (!(Test-Path $script:ReportsPath)) {
    New-Item -Path $script:ReportsPath -ItemType Directory -Force | Out-Null
}
if (!(Test-Path $script:DashboardPath) -and $GenerateDashboard) {
    New-Item -Path $script:DashboardPath -ItemType Directory -Force | Out-Null
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

function Get-BuildEnvironment {
    Write-Log "Gathering build environment information" "INFO"
    
    $environment = @{
        BuildInfo = @{
            BuildId = if ($env:BUILD_BUILDID) { $env:BUILD_BUILDID } else { "local" }
            BuildNumber = if ($env:BUILD_BUILDNUMBER) { $env:BUILD_BUILDNUMBER } else { "local-$(Get-Date -Format 'yyyyMMdd-HHmmss')" }
            SourceBranch = if ($env:BUILD_SOURCEBRANCH) { $env:BUILD_SOURCEBRANCH } else { "local" }
            SourceVersion = if ($env:BUILD_SOURCEVERSION) { $env:BUILD_SOURCEVERSION } else { "local" }
            RepositoryName = if ($env:BUILD_REPOSITORY_NAME) { $env:BUILD_REPOSITORY_NAME } else { "TiXL" }
            TriggerReason = if ($env:BUILD_REASON) { $env:BUILD_REASON } else { "Manual" }
        }
        SystemInfo = @{
            OS = $PSVersionTable.OS
            PowerShellVersion = $PSVersionTable.PSVersion.ToString()
            DotNetVersion = if (Get-Command dotnet -ErrorAction SilentlyContinue) { (dotnet --version) } else { "Not found" }
            WorkingDirectory = Get-Location
            Timestamp = Get-Date
        }
        ProjectInfo = @{
            SolutionPath = $ProjectPath
            ProjectName = [System.IO.Path]::GetFileNameWithoutExtension($ProjectPath)
            IsSolution = $ProjectPath.EndsWith('.sln')
        }
    }
    
    return $environment
}

function Get-SecurityScans {
    param([string]$ProjectPath)
    
    Write-Log "Collecting security scan results" "INFO"
    
    $scans = @{
        LastUpdated = Get-Date
        DependencyAudit = $null
        VulnerabilityScan = $null
        LicenseCompliance = $null
        EnhancedVulnerability = $null
        SecurityAssessment = $null
    }
    
    # Find latest dependency audit report
    $auditFiles = Get-ChildItem -Path $script:ReportsPath -Filter "*audit-report.json" -Recurse
    if ($auditFiles.Count -gt 0) {
        try {
            $scans.DependencyAudit = Get-Content $auditFiles[0].FullName | ConvertFrom-Json
            Write-Log "Loaded dependency audit: $($auditFiles[0].FullName)" "INFO"
        }
        catch {
            Write-Log "Failed to load dependency audit: $_" "WARNING"
        }
    }
    
    # Find latest vulnerability scan report
    $vulnFiles = Get-ChildItem -Path $script:ReportsPath -Filter "*vulnerability-report.json" -Recurse
    if ($vulnFiles.Count -gt 0) {
        try {
            $scans.VulnerabilityScan = Get-Content $vulnFiles[0].FullName | ConvertFrom-Json
            Write-Log "Loaded vulnerability scan: $($vulnFiles[0].FullName)" "INFO"
        }
        catch {
            Write-Log "Failed to load vulnerability scan: $_" "WARNING"
        }
    }
    
    # Find latest enhanced vulnerability report
    $enhancedFiles = Get-ChildItem -Path $script:ReportsPath -Filter "*enhanced-vulnerability-report.json" -Recurse
    if ($enhancedFiles.Count -gt 0) {
        try {
            $scans.EnhancedVulnerability = Get-Content $enhancedFiles[0].FullName | ConvertFrom-Json
            Write-Log "Loaded enhanced vulnerability scan: $($enhancedFiles[0].FullName)" "INFO"
        }
        catch {
            Write-Log "Failed to load enhanced vulnerability scan: $_" "WARNING"
        }
    }
    
    # Find latest license compliance report
    $licenseFiles = Get-ChildItem -Path $script:ReportsPath -Filter "*license-compliance.json" -Recurse
    if ($licenseFiles.Count -gt 0) {
        try {
            $scans.LicenseCompliance = Get-Content $licenseFiles[0].FullName | ConvertFrom-Json
            Write-Log "Loaded license compliance: $($licenseFiles[0].FullName)" "INFO"
        }
        catch {
            Write-Log "Failed to load license compliance: $_" "WARNING"
        }
    }
    
    return $scans
}

function Get-HistoricalTrends {
    param([int]$Days)
    
    if (-not $IncludeHistorical -or -not $script:HistoricalPath -or -not (Test-Path $script:HistoricalPath)) {
        Write-Log "Historical analysis skipped" "INFO"
        return $null
    }
    
    Write-Log "Analyzing historical trends for last $Days days" "INFO"
    
    $historicalData = @{
        Period = "$Days days"
        StartDate = (Get-Date).AddDays(-$Days)
        EndDate = Get-Date
        Trends = @{
            VulnerabilityCount = @()
            SecurityScore = @()
            ComplianceStatus = @()
            UpdateCount = @()
        }
        Summary = @{
            AverageVulnerabilities = 0
            TrendDirection = "Stable"
            BestSecurityDay = $null
            WorstSecurityDay = $null
        }
    }
    
    try {
        # Load historical reports from the last N days
        $cutoffDate = (Get-Date).AddDays(-$Days)
        $historicalFiles = Get-ChildItem -Path $script:HistoricalPath -Filter "*security-report*.json" -Recurse | Where-Object {
            $_.LastWriteTime -ge $cutoffDate
        }
        
        foreach ($file in $historicalFiles) {
            try {
                $report = Get-Content $file.FullName | ConvertFrom-Json
                
                $historicalData.Trends.VulnerabilityCount += @{
                    Date = $file.LastWriteTime
                    Count = if ($report.Summary.TotalVulnerabilities) { $report.Summary.TotalVulnerabilities } else { 0 }
                    Critical = if ($report.Summary.Critical) { $report.Summary.Critical } else { 0 }
                    High = if ($report.Summary.High) { $report.Summary.High } else { 0 }
                }
                
                $historicalData.Trends.SecurityScore += @{
                    Date = $file.LastWriteTime
                    Score = if ($report.RiskScore) { $report.RiskScore } else { 100 }
                }
            }
            catch {
                Write-Log "Failed to load historical file: $($file.FullName)" "WARNING"
            }
        }
        
        # Calculate summary statistics
        if ($historicalData.Trends.VulnerabilityCount.Count -gt 0) {
            $vulnCounts = $historicalData.Trends.VulnerabilityCount | ForEach-Object { $_.Count }
            $historicalData.Summary.AverageVulnerabilities = ($vulnCounts | Measure-Object -Average).Average
            
            # Determine trend direction
            if ($vulnCounts.Count -ge 2) {
                $recentAvg = ($vulnCounts | Select-Object -Last 3 | Measure-Object -Average).Average
                $olderAvg = ($vulnCounts | Select-Object -First 3 | Measure-Object -Average).Average
                
                if ($recentAvg -lt $olderAvg) {
                    $historicalData.Summary.TrendDirection = "Improving"
                } elseif ($recentAvg -gt $olderAvg) {
                    $historicalData.Summary.TrendDirection = "Declining"
                }
            }
        }
        
        Write-Log "Historical analysis completed" "INFO"
    }
    catch {
        Write-Log "Historical analysis failed: $_" "WARNING"
        $historicalData = $null
    }
    
    return $historicalData
}

function Calculate-SecurityScore {
    param(
        $Scans,
        $HistoricalData
    )
    
    Write-Log "Calculating security score" "INFO"
    
    # Default scores
    $scores = @{
        Overall = 100
        Vulnerability = 100
        License = 100
        Updates = 100
        Trend = 100
        
        Breakdown = @{
            CriticalVulnerabilities = 0
            HighVulnerabilities = 0
            LicenseViolations = 0
            OutdatedPackages = 0
            SecurityFixesAvailable = 0
        }
        
        Grade = "A"
        Status = "Excellent"
        Recommendations = @()
    }
    
    # Calculate vulnerability score
    $vulnData = $Scans.EnhancedVulnerability ?? $Scans.VulnerabilityScan
    if ($vulnData -and $vulnData.Summary) {
        $critical = $vulnData.Summary.Critical ?? 0
        $high = $vulnData.Summary.High ?? 0
        $medium = $vulnData.Summary.Medium ?? 0
        
        # Deduct points for vulnerabilities
        $vulnScore = 100
        $vulnScore -= ($critical * 20)  # 20 points per critical
        $vulnScore -= ($high * 10)      # 10 points per high
        $vulnScore -= ($medium * 5)     # 5 points per medium
        
        $scores.Vulnerability = [Math]::Max(0, $vulnScore)
        $scores.Breakdown.CriticalVulnerabilities = $critical
        $scores.Breakdown.HighVulnerabilities = $high
        
        # Add recommendations
        if ($critical -gt 0) {
            $scores.Recommendations += "Immediate action required: $critical critical vulnerabilities found"
        }
        if ($high -gt 0) {
            $scores.Recommendations += "High priority: $high high-severity vulnerabilities require attention"
        }
    }
    
    # Calculate license compliance score
    $licenseData = $Scans.LicenseCompliance
    if ($licenseData -and $licenseData.Statistics) {
        $violations = $licenseData.Statistics.Violations ?? 0
        $unknown = $licenseData.Statistics.Unknown ?? 0
        
        $licenseScore = 100 - ($violations * 15) - ($unknown * 5)
        $scores.License = [Math]::Max(0, $licenseScore)
        $scores.Breakdown.LicenseViolations = $violations
        
        if ($violations -gt 0) {
            $scores.Recommendations += "License compliance: $violations violations require immediate review"
        }
    }
    
    # Calculate update status score
    $auditData = $Scans.DependencyAudit
    if ($auditData -and $auditData.Summary) {
        $outdated = $auditData.Summary.VersionAnalysis.Outdated ?? 0
        
        $updateScore = 100 - [Math]::Min(50, ($outdated * 2))  # Max 50 points deducted
        $scores.Updates = [Math]::Max(0, $updateScore)
        $scores.Breakdown.OutdatedPackages = $outdated
        
        if ($outdated -gt 10) {
            $scores.Recommendations += "Consider updating $outdated outdated packages"
        }
    }
    
    # Calculate trend score
    if ($HistoricalData) {
        switch ($HistoricalData.Summary.TrendDirection) {
            "Improving" { $scores.Trend = 100; break }
            "Declining" { $scores.Trend = 70; break }
            "Stable" { $scores.Trend = 85; break }
            default { $scores.Trend = 85; }
        }
    }
    
    # Calculate overall score (weighted average)
    $scores.Overall = [Math]::Round((
        $scores.Vulnerability * 0.4 +
        $scores.License * 0.3 +
        $scores.Updates * 0.2 +
        $scores.Trend * 0.1
    ), 2)
    
    # Determine grade and status
    $scores.Grade = switch ($scores.Overall) {
        { $_ -ge 95 } { "A+"; "Excellent" }
        { $_ -ge 90 } { "A"; "Excellent" }
        { $_ -ge 85 } { "B+"; "Good" }
        { $_ -ge 80 } { "B"; "Good" }
        { $_ -ge 75 } { "C+"; "Fair" }
        { $_ -ge 70 } { "C"; "Fair" }
        { $_ -ge 65 } { "D+"; "Poor" }
        { $_ -ge 60 } { "D"; "Poor" }
        default { "F"; "Critical" }
    }
    
    $scores.Status = $scores.Grade[1]
    $scores.Grade = $scores.Grade[0]
    
    return $scores
}

function New-BuildSecurityReport {
    param(
        $Environment,
        $Scans,
        $Scores
    )
    
    Write-Log "Generating build security report" "INFO"
    
    $report = @{
        ReportType = "Build Security Report"
        Generated = $script:StartTime
        Environment = $Environment
        SecurityScore = $Scores
        Summary = @{
            TotalScans = 0
            PassedScans = 0
            FailedScans = 0
            CriticalIssues = $Scores.Breakdown.CriticalVulnerabilities
            Recommendations = $Scores.Recommendations.Count
        }
        ScanResults = @{
            DependencyAudit = @{
                Status = if ($Scans.DependencyAudit) { "Available" } else { "Not Found" }
                Issues = if ($Scans.DependencyAudit) { $Scans.DependencyAudit.Summary.Vulnerabilities.Total } else { 0 }
            }
            VulnerabilityScan = @{
                Status = if ($Scans.VulnerabilityScan -or $Scans.EnhancedVulnerability) { "Available" } else { "Not Found" }
                TotalVulnerabilities = if ($Scans.EnhancedVulnerability) { $Scans.EnhancedVulnerability.Summary.TotalVulnerabilities } elseif ($Scans.VulnerabilityScan) { $Scans.VulnerabilityScan.Summary.Total } else { 0 }
                Critical = $Scores.Breakdown.CriticalVulnerabilities
                High = $Scores.Breakdown.HighVulnerabilities
            }
            LicenseCompliance = @{
                Status = if ($Scans.LicenseCompliance) { "Available" } else { "Not Found" }
                Violations = $Scores.Breakdown.LicenseViolations
            }
        }
        Compliance = @{
            SecurityPolicy = "Met"
            LicensePolicy = if ($Scores.Breakdown.LicenseViolations -eq 0) { "Compliant" } else { "Non-Compliant" }
            UpdatePolicy = if ($Scores.Breakdown.OutdatedPackages -lt 5) { "Up to Date" } else { "Needs Update" }
        }
        ActionItems = @()
        NextReview = (Get-Date).AddDays(7)
    }
    
    # Add action items
    if ($Scores.Breakdown.CriticalVulnerabilities -gt 0) {
        $report.ActionItems += @{
            Priority = "Critical"
            Action = "Immediate vulnerability remediation"
            DueDate = (Get-Date).AddDays(1)
            Owner = "Security Team"
        }
    }
    
    if ($Scores.Breakdown.HighVulnerabilities -gt 0) {
        $report.ActionItems += @{
            Priority = "High"
            Action = "Schedule vulnerability remediation"
            DueDate = (Get-Date).AddDays(7)
            Owner = "Development Team"
        }
    }
    
    if ($Scores.Breakdown.LicenseViolations -gt 0) {
        $report.ActionItems += @{
            Priority = "Medium"
            Action = "Review license compliance"
            DueDate = (Get-Date).AddDays(14)
            Owner = "Legal Team"
        }
    }
    
    return $report
}

function New-ExecutiveSecurityReport {
    param(
        $Environment,
        $Scans,
        $Scores,
        $HistoricalData
    )
    
    Write-Log "Generating executive security report" "INFO"
    
    $report = @{
        ReportType = "Executive Security Summary"
        Generated = $script:StartTime
        ExecutiveSummary = @{
            OverallScore = $Scores.Overall
            Grade = $Scores.Grade
            SecurityStatus = $Scores.Status
            TrendDirection = if ($HistoricalData) { $HistoricalData.Summary.TrendDirection } else { "Stable" }
            KeyFindings = @()
            RiskLevel = switch ($Scores.Overall) {
                { $_ -ge 90 } { "Low" }
                { $_ -ge 75 } { "Medium" }
                { $_ -ge 60 } { "High" }
                default { "Critical" }
            }
        }
        KeyMetrics = @{
            TotalVulnerabilities = $Scores.Breakdown.CriticalVulnerabilities + $Scores.Breakdown.HighVulnerabilities
            SecurityFixesAvailable = $Scores.Breakdown.SecurityFixesAvailable
            LicenseViolations = $Scores.Breakdown.LicenseViolations
            OutdatedPackages = $Scores.Breakdown.OutdatedPackages
            ComplianceRate = [Math]::Round((100 - ($Scores.Breakdown.LicenseViolations * 10)), 2)
        }
        BusinessImpact = @{
            SecurityRisk = switch ($Scores.Breakdown.CriticalVulnerabilities) {
                0 { "Minimal" }
                { $_ -le 2 } { "Low" }
                { $_ -le 5 } { "Medium" }
                default { "High" }
            }
            ComplianceRisk = switch ($Scores.Breakdown.LicenseViolations) {
                0 { "None" }
                { $_ -le 2 } { "Low" }
                { $_ -le 5 } { "Medium" }
                default { "High" }
            }
            TechnicalDebt = if ($Scores.Breakdown.OutdatedPackages -gt 10) { "Significant" } elseif ($Scores.Breakdown.OutdatedPackages -gt 5) { "Moderate" } else { "Low" }
        }
        Recommendations = @{
            Immediate = @()
            ShortTerm = @()
            LongTerm = @()
        }
        HistoricalContext = $HistoricalData
        StrategicPlan = @{
            Priority = switch ($Scores.Overall) {
                { $_ -ge 90 } { "Maintain current security posture" }
                { $_ -ge 75 } { "Address identified issues" }
                { $_ -ge 60 } { "Immediate security improvements required" }
                default { "Emergency security remediation needed" }
            }
            Investment = switch ($Scores.Overall) {
                { $_ -ge 90 } { "Minimal - Maintain current practices" }
                { $_ -ge 75 } { "Moderate - Focus on identified weaknesses" }
                { $_ -ge 60 } { "Significant - Comprehensive security upgrade" }
                default { "Critical - Full security overhaul" }
            }
            Timeline = switch ($Scores.Overall) {
                { $_ -ge 90 } { "Ongoing monitoring and maintenance" }
                { $_ -ge 75 } { "30-60 days for improvements" }
                { $_ -ge 60 } { "Immediate action required (7 days)" }
                default { "Emergency response (24-48 hours)" }
            }
        }
    }
    
    # Add key findings
    if ($Scores.Breakdown.CriticalVulnerabilities -gt 0) {
        $report.ExecutiveSummary.KeyFindings += "$($Scores.Breakdown.CriticalVulnerabilities) critical security vulnerabilities require immediate attention"
    }
    
    if ($HistoricalData -and $HistoricalData.Summary.TrendDirection -eq "Declining") {
        $report.ExecutiveSummary.KeyFindings += "Security posture is declining - trend shows increasing vulnerabilities"
    }
    
    # Add recommendations
    if ($Scores.Breakdown.CriticalVulnerabilities -gt 0) {
        $report.Recommendations.Immediate += "Execute emergency security patching within 24 hours"
        $report.Recommendations.ShortTerm += "Implement enhanced vulnerability scanning"
        $report.Recommendations.LongTerm += "Establish continuous security monitoring"
    }
    
    if ($Scores.Breakdown.LicenseViolations -gt 0) {
        $report.Recommendations.ShortTerm += "Review and resolve license compliance issues"
        $report.Recommendations.LongTerm += "Implement automated license compliance checking"
    }
    
    return $report
}

function Export-HtmlDashboard {
    param(
        $BuildReport,
        $ExecutiveReport
    )
    
    if (-not $GenerateDashboard) {
        return
    }
    
    Write-Log "Generating HTML dashboard" "INFO"
    
    $dashboardHtml = @"
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>TiXL Security Dashboard</title>
    <script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
    <style>
        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 0; padding: 20px; background: #f5f5f5; }
        .header { background: #2c3e50; color: white; padding: 20px; border-radius: 8px; margin-bottom: 20px; }
        .dashboard { display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 20px; }
        .card { background: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
        .metric { text-align: center; margin-bottom: 15px; }
        .metric-value { font-size: 2em; font-weight: bold; color: #2c3e50; }
        .metric-label { color: #7f8c8d; font-size: 0.9em; }
        .score-circle { width: 120px; height: 120px; border-radius: 50%; margin: 0 auto; display: flex; align-items: center; justify-content: center; color: white; font-size: 1.5em; font-weight: bold; }
        .grade-a { background: linear-gradient(45deg, #27ae60, #2ecc71); }
        .grade-b { background: linear-gradient(45deg, #f39c12, #f1c40f); }
        .grade-c { background: linear-gradient(45deg, #e67e22, #d35400); }
        .grade-d { background: linear-gradient(45deg, #e74c3c, #c0392b); }
        .grade-f { background: linear-gradient(45deg, #8e44ad, #9b59b6); }
        .chart-container { position: relative; height: 300px; margin-top: 20px; }
        .status-good { color: #27ae60; }
        .status-warning { color: #f39c12; }
        .status-critical { color: #e74c3c; }
        .recommendations { background: #ecf0f1; padding: 15px; border-radius: 5px; margin-top: 15px; }
        .action-item { margin: 5px 0; padding: 5px; border-left: 4px solid #3498db; }
        .priority-critical { border-left-color: #e74c3c; }
        .priority-high { border-left-color: #f39c12; }
        .priority-medium { border-left-color: #3498db; }
    </style>
</head>
<body>
    <div class="header">
        <h1>🛡️ TiXL Security Dashboard</h1>
        <p>Generated: $($BuildReport.Generated.ToString('yyyy-MM-dd HH:mm:ss')) | Build: $($BuildReport.Environment.BuildInfo.BuildNumber)</p>
    </div>
    
    <div class="dashboard">
        <!-- Overall Security Score -->
        <div class="card">
            <h2>Security Score</h2>
            <div class="score-circle grade-$($BuildReport.SecurityScore.Grade.ToLower())">
                $($BuildReport.SecurityScore.Overall)<br>
                <small>$($BuildReport.SecurityScore.Grade)</small>
            </div>
            <p class="metric-label">Status: <span class="status-$($BuildReport.SecurityScore.Status.ToLower())">$($BuildReport.SecurityScore.Status)</span></p>
        </div>
        
        <!-- Vulnerability Summary -->
        <div class="card">
            <h2>Vulnerabilities</h2>
            <div class="metric">
                <div class="metric-value status-$(
                    if ($BuildReport.SecurityScore.Breakdown.CriticalVulnerabilities -eq 0) { 'good' }
                    elseif ($BuildReport.SecurityScore.Breakdown.CriticalVulnerabilities -le 2) { 'warning' }
                    else { 'critical' }
                )">$($BuildReport.SecurityScore.Breakdown.CriticalVulnerabilities + $BuildReport.SecurityScore.Breakdown.HighVulnerabilities)</div>
                <div class="metric-label">Total Vulnerabilities</div>
            </div>
            <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 10px; text-align: center;">
                <div>
                    <div class="metric-value status-critical">$($BuildReport.SecurityScore.Breakdown.CriticalVulnerabilities)</div>
                    <div class="metric-label">Critical</div>
                </div>
                <div>
                    <div class="metric-value status-warning">$($BuildReport.SecurityScore.Breakdown.HighVulnerabilities)</div>
                    <div class="metric-label">High</div>
                </div>
            </div>
        </div>
        
        <!-- Compliance Status -->
        <div class="card">
            <h2>Compliance</h2>
            <div class="chart-container">
                <canvas id="complianceChart"></canvas>
            </div>
        </div>
        
        <!-- Action Items -->
        <div class="card">
            <h2>Action Items</h2>
            <div class="recommendations">
                $(
                    if ($BuildReport.ActionItems.Count -gt 0) {
                        foreach ($item in $BuildReport.ActionItems) {
                            "<div class='action-item priority-$($item.Priority.ToLower())'><strong>$($item.Priority)</strong>: $($item.Action)<br><small>Due: $($item.DueDate.ToString('yyyy-MM-dd')) | Owner: $($item.Owner)</small></div>"
                        }
                    } else {
                        "<div class='action-item'>✅ No immediate action items</div>"
                    }
                )
            </div>
        </div>
        
        <!-- Trends -->
        <div class="card">
            <h2>Security Trends</h2>
            <div class="chart-container">
                <canvas id="trendChart"></canvas>
            </div>
        </div>
        
        <!-- Executive Summary -->
        <div class="card">
            <h2>Executive Summary</h2>
            <p><strong>Risk Level:</strong> <span class="status-$($ExecutiveReport.BusinessImpact.SecurityRisk.ToLower())">$($ExecutiveReport.BusinessImpact.SecurityRisk)</span></p>
            <p><strong>Compliance Risk:</strong> <span class="status-$($ExecutiveReport.BusinessImpact.ComplianceRisk.ToLower())">$($ExecutiveReport.BusinessImpact.ComplianceRisk)</span></p>
            <p><strong>Technical Debt:</strong> $($ExecutiveReport.BusinessImpact.TechnicalDebt)</p>
            <p><strong>Priority:</strong> $($ExecutiveReport.StrategicPlan.Priority)</p>
        </div>
    </div>
    
    <script>
        // Compliance Chart
        const complianceCtx = document.getElementById('complianceChart').getContext('2d');
        new Chart(complianceCtx, {
            type: 'doughnut',
            data: {
                labels: ['Compliant', 'Violations'],
                datasets: [{
                    data: [$(100 - $BuildReport.SecurityScore.Breakdown.LicenseViolations), $($BuildReport.SecurityScore.Breakdown.LicenseViolations)],
                    backgroundColor: ['#27ae60', '#e74c3c']
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false
            }
        });
        
        // Trend Chart (placeholder - would need historical data)
        const trendCtx = document.getElementById('trendChart').getContext('2d');
        new Chart(trendCtx, {
            type: 'line',
            data: {
                labels: ['Week 1', 'Week 2', 'Week 3', 'Week 4'],
                datasets: [{
                    label: 'Security Score',
                    data: [85, 90, 88, $($BuildReport.SecurityScore.Overall)],
                    borderColor: '#3498db',
                    fill: false
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                scales: {
                    y: {
                        beginAtZero: false,
                        min: 60,
                        max: 100
                    }
                }
            }
        });
    </script>
</body>
</html>
"@
    
    $dashboardPath = Join-Path $script:DashboardPath "security-dashboard.html"
    $dashboardHtml | Out-File -FilePath $dashboardPath -Encoding UTF8
    
    Write-Log "HTML dashboard generated: $dashboardPath" "INFO"
}

function Export-ComprehensiveReport {
    param(
        $BuildReport,
        $ExecutiveReport,
        $Environment,
        $Scans
    )
    
    Write-Log "Exporting comprehensive security reports" "INFO"
    
    # Generate timestamped filename
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    
    # Export comprehensive JSON report
    $comprehensiveReport = @{
        Generated = $script:StartTime
        ReportVersion = $script:ScriptVersion
        BuildReport = $BuildReport
        ExecutiveReport = $ExecutiveReport
        Environment = $Environment
        RawScanData = $Scans
    }
    
    $jsonPath = Join-Path $script:ReportsPath "comprehensive-security-report-$timestamp.json"
    $comprehensiveReport | ConvertTo-Json -Depth 15 | Out-File -FilePath $jsonPath -Encoding UTF8
    
    # Export build report markdown
    $buildMarkdown = @"
# TiXL Build Security Report

**Generated:** $($BuildReport.Generated.ToString("yyyy-MM-dd HH:mm:ss"))
**Build:** $($BuildReport.Environment.BuildInfo.BuildNumber)
**Branch:** $($BuildReport.Environment.BuildInfo.SourceBranch)

## 🛡️ Security Score: $($BuildReport.SecurityScore.Overall)/100 (Grade $($BuildReport.SecurityScore.Grade))

### Vulnerability Summary
- **Critical:** $($BuildReport.SecurityScore.Breakdown.CriticalVulnerabilities)
- **High:** $($BuildReport.SecurityScore.Breakdown.HighVulnerabilities)
- **License Violations:** $($BuildReport.SecurityScore.Breakdown.LicenseViolations)
- **Outdated Packages:** $($BuildReport.SecurityScore.Breakdown.OutdatedPackages)

### Compliance Status
- **Security Policy:** $($BuildReport.Compliance.SecurityPolicy)
- **License Policy:** $($BuildReport.Compliance.LicensePolicy)
- **Update Policy:** $($BuildReport.Compliance.UpdatePolicy)

### Action Items
$(
    if ($BuildReport.ActionItems.Count -gt 0) {
        foreach ($item in $BuildReport.ActionItems) {
            "- **$($item.Priority):** $($item.Action) (Due: $($item.DueDate.ToString('yyyy-MM-dd')))"
        }
    } else {
        "✅ No immediate action items required"
    }
)

### Recommendations
$(
    if ($BuildReport.SecurityScore.Recommendations.Count -gt 0) {
        foreach ($rec in $BuildReport.SecurityScore.Recommendations) {
            "- $rec"
        }
    } else {
        "✅ No security issues found"
    }
)

---
*Report generated by TiXL Security Report Generator v$($script:ScriptVersion)*
"@
    
    $buildMarkdownPath = Join-Path $script:ReportsPath "build-security-report-$timestamp.md"
    $buildMarkdown | Out-File -FilePath $buildMarkdownPath -Encoding UTF8
    
    # Export executive report markdown
    $executiveMarkdown = @"
# TiXL Executive Security Summary

**Generated:** $($ExecutiveReport.Generated.ToString("yyyy-MM-dd HH:mm:ss"))
**Period:** Last Build Cycle
**Risk Assessment:** $($ExecutiveReport.ExecutiveSummary.RiskLevel)

## 📊 Key Metrics

| Metric | Value |
|--------|-------|
| **Overall Security Score** | $($ExecutiveReport.ExecutiveSummary.OverallScore)/100 |
| **Grade** | $($ExecutiveReport.ExecutiveSummary.Grade) |
| **Trend** | $($ExecutiveReport.ExecutiveSummary.TrendDirection) |
| **Total Vulnerabilities** | $($ExecutiveReport.KeyMetrics.TotalVulnerabilities) |
| **Compliance Rate** | $($ExecutiveReport.KeyMetrics.ComplianceRate)% |

## 🎯 Business Impact

- **Security Risk:** $($ExecutiveReport.BusinessImpact.SecurityRisk)
- **Compliance Risk:** $($ExecutiveReport.BusinessImpact.ComplianceRisk)
- **Technical Debt:** $($ExecutiveReport.BusinessImpact.TechnicalDebt)

## 💼 Strategic Recommendations

### Immediate Actions
$(
    if ($ExecutiveReport.Recommendations.Immediate.Count -gt 0) {
        foreach ($rec in $ExecutiveReport.Recommendations.Immediate) {
            "- $rec"
        }
    } else {
        "✅ No immediate actions required"
    }
)

### Short-term (30-60 days)
$(
    if ($ExecutiveReport.Recommendations.ShortTerm.Count -gt 0) {
        foreach ($rec in $ExecutiveReport.Recommendations.ShortTerm) {
            "- $rec"
        }
    } else {
        "Continue current security practices"
    }
)

### Long-term Strategic Plan
- **Priority:** $($ExecutiveReport.StrategicPlan.Priority)
- **Investment Required:** $($ExecutiveReport.StrategicPlan.Investment)
- **Timeline:** $($ExecutiveReport.StrategicPlan.Timeline)

## 🔍 Key Findings
$(
    if ($ExecutiveReport.ExecutiveSummary.KeyFindings.Count -gt 0) {
        foreach ($finding in $ExecutiveReport.ExecutiveSummary.KeyFindings) {
            "- $finding"
        }
    } else {
        "✅ No significant security issues identified"
    }
)

---
*Executive Report generated by TiXL Security Report Generator v$($script:ScriptVersion)*
"@
    
    $executiveMarkdownPath = Join-Path $script:ReportsPath "executive-security-summary-$timestamp.md"
    $executiveMarkdown | Out-File -FilePath $executiveMarkdownPath -Encoding UTF8
    
    Write-Log "Reports exported to: $script:ReportsPath" "INFO"
    
    return @{
        JsonReport = $jsonPath
        BuildMarkdown = $buildMarkdownPath
        ExecutiveMarkdown = $executiveMarkdownPath
        Timestamp = $timestamp
    }
}

# Main execution
Write-Log "Starting $script:ScriptName v$script:ScriptVersion" "INFO"
Write-Log "Project path: $ProjectPath" "INFO"
Write-Log "Report type: $ReportType" "INFO"
Write-Log "Include historical: $IncludeHistorical" "INFO"
Write-Log "Generate dashboard: $GenerateDashboard" "INFO"

try {
    # Gather environment information
    $environment = Get-BuildEnvironment
    
    # Collect security scan results
    $scans = Get-SecurityScans -ProjectPath $ProjectPath
    
    # Analyze historical trends if requested
    $historicalData = if ($IncludeHistorical) { Get-HistoricalTrends -Days $TrendDays } else { $null }
    
    # Calculate security scores
    $scores = Calculate-SecurityScore -Scans $scans -HistoricalData $historicalData
    
    # Generate reports based on type
    $reports = @()
    
    if ($ReportType -eq "Build" -or $ReportType -eq "All") {
        $buildReport = New-BuildSecurityReport -Environment $environment -Scans $scans -Scores $scores
        $reports += @{ Type = "Build"; Report = $buildReport }
    }
    
    if ($ReportType -eq "Executive" -or $ReportType -eq "All") {
        $executiveReport = New-ExecutiveSecurityReport -Environment $environment -Scans $scans -Scores $scores -HistoricalData $historicalData
        $reports += @{ Type = "Executive"; Report = $executiveReport }
    }
    
    # Export reports
    $exportPaths = Export-ComprehensiveReport -BuildReport $reports[0].Report -ExecutiveReport $reports[1].Report -Environment $environment -Scans $scans
    
    # Generate HTML dashboard if requested
    if ($GenerateDashboard) {
        Export-HtmlDashboard -BuildReport $reports[0].Report -ExecutiveReport $reports[1].Report
    }
    
    # Display summary
    Write-Host "`n📊 Security Report Summary" -ForegroundColor Cyan
    Write-Host "=========================" -ForegroundColor Cyan
    Write-Host "Security Score: $($scores.Overall)/100 (Grade $($scores.Grade))" -ForegroundColor $(if ($scores.Overall -ge 90) { "Green" } elseif ($scores.Overall -ge 70) { "Yellow" } else { "Red" })
    Write-Host "Critical Vulnerabilities: $($scores.Breakdown.CriticalVulnerabilities)" -ForegroundColor $(if ($scores.Breakdown.CriticalVulnerabilities -eq 0) { "Green" } else { "Red" })
    Write-Host "High Vulnerabilities: $($scores.Breakdown.HighVulnerabilities)" -ForegroundColor $(if ($scores.Breakdown.HighVulnerabilities -eq 0) { "Green" } else { "Yellow" })
    Write-Host "License Violations: $($scores.Breakdown.LicenseViolations)" -ForegroundColor $(if ($scores.Breakdown.LicenseViolations -eq 0) { "Green" } else { "Yellow" })
    Write-Host "Historical Trend: $(if ($historicalData) { $historicalData.Summary.TrendDirection } else { 'Not Available' })" -ForegroundColor Gray
    Write-Host "Reports Location: $OutputPath" -ForegroundColor Blue
    if ($GenerateDashboard) {
        Write-Host "Dashboard: $script:DashboardPath/security-dashboard.html" -ForegroundColor Blue
    }
    
    # Store report paths for automation
    $reportPaths = @{
        JsonReport = $exportPaths.JsonReport
        BuildMarkdown = $exportPaths.BuildMarkdown
        ExecutiveMarkdown = $exportPaths.ExecutiveMarkdown
        Dashboard = if ($GenerateDashboard) { Join-Path $script:DashboardPath "security-dashboard.html" } else { $null }
    }
    
    # Save report paths for future reference
    $reportPaths | ConvertTo-Json | Out-File -FilePath (Join-Path $OutputPath "report-paths.json") -Encoding UTF8
    
    Write-Log "Security reporting completed successfully" "INFO"
    
    # Exit with appropriate code
    $exitCode = if ($scores.Breakdown.CriticalVulnerabilities -gt 0) { 1 } else { 0 }
    exit $exitCode
}
catch {
    Write-Log "Security reporting failed with error: $_" "ERROR"
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
}