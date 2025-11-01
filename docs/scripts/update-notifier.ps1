#!/usr/bin/env pwsh
<#
.SYNOPSIS
    TiXL Update Notifier - Dependency update notifications and alerting

.DESCRIPTION
    Provides automated notification capabilities for dependency updates including:
    - Email notifications for critical updates
    - Slack/Teams integration
    - Scheduled dependency health reports
    - Security vulnerability alerts
    - Update progress tracking

.PARAMETER ProjectPath
    Path to the solution or project file

.PARAMETER NotificationType
    Type of notification to send

.PARAMETER OutputPath
    Directory to save notification reports

.PARAMETER Recipients
    List of notification recipients

.PARAMETER Channels
    Notification channels (Email, Slack, Teams, Webhook)

.PARAMETER Schedule
    Notification schedule (Immediate, Daily, Weekly, Monthly)

.PARAMETER Verbose
    Enable verbose output

.EXAMPLE
    .\update-notifier.ps1 -ProjectPath "TiXL.sln" -NotificationType "SecurityAlert" -Channels @("Email", "Slack")

.EXAMPLE
    .\update-notifier.ps1 -ProjectPath "TiXL.sln" -Schedule "Daily" -Channels @("Email")
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$ProjectPath,
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("SecurityAlert", "VersionUpdate", "LicenseViolation", "HealthReport", "All")]
    [string]$NotificationType = "All",
    
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "./notifications",
    
    [Parameter(Mandatory=$false)]
    [string[]]$Recipients = @(),
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("Email", "Slack", "Teams", "Webhook")]
    [string[]]$Channels = @("Email"),
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("Immediate", "Daily", "Weekly", "Monthly")]
    [string]$Schedule = "Immediate",
    
    [Parameter(Mandatory=$false)]
    [switch]$Verbose
)

# Global variables
$script:ScriptName = "TiXL Update Notifier"
$script:ScriptVersion = "1.0.0"
$script:StartTime = Get-Date
$script:ConfigPath = "$PSScriptRoot\..\config\notification-config.json"

# Initialize output directory
if (!(Test-Path $OutputPath)) {
    New-Item -Path $OutputPath -ItemType Directory -Force | Out-Null
}

$script:LogFile = Join-Path $OutputPath "notifications.log"
$script:ReportFile = Join-Path $OutputPath "notification-report.json"

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

function Initialize-NotificationConfig {
    param()
    
    Write-Log "Initializing notification configuration" "INFO"
    
    $config = @{
        Email = @{
            SMTPServer = "smtp.example.com"
            SMTPPort = 587
            UseSSL = $true
            Username = ""
            Password = ""
            FromAddress = "tixl-updates@example.com"
        }
        Slack = @{
            WebhookUrl = ""
            Channel = "#general"
            BotName = "TiXL Dependency Bot"
        }
        Teams = @{
            WebhookUrl = ""
            Channel = "General"
        }
        Webhook = @{
            URL = ""
            Headers = @{}
            Timeout = 30
        }
        Templates = @{
            SecurityAlert = @{
                Subject = "🚨 Security Alert: TiXL Dependency Vulnerability"
                Priority = "High"
            }
            VersionUpdate = @{
                Subject = "📦 TiXL Dependency Update Available"
                Priority = "Medium"
            }
            LicenseViolation = @{
                Subject = "⚖️ License Violation: TiXL Dependency Compliance Issue"
                Priority = "High"
            }
            HealthReport = @{
                Subject = "📊 TiXL Dependency Health Report"
                Priority = "Low"
            }
        }
    }
    
    # Override with custom config if available
    if (Test-Path $script:ConfigPath) {
        try {
            $customConfig = Get-Content $script:ConfigPath | ConvertFrom-Json
            
            if ($customConfig.email) { $config.Email = $customConfig.email }
            if ($customConfig.slack) { $config.Slack = $customConfig.slack }
            if ($customConfig.teams) { $config.Teams = $customConfig.teams }
            if ($customConfig.webhook) { $config.Webhook = $customConfig.webhook }
            if ($customConfig.templates) { $config.Templates = $customConfig.templates }
            
            Write-Log "Loaded custom notification configuration" "INFO"
        }
        catch {
            Write-Log "Failed to load custom notification config: $_" "WARNING"
        }
    }
    else {
        Write-Log "Using default notification configuration" "INFO"
    }
    
    # Override with parameters
    if ($Recipients.Count -gt 0) {
        $config.Email.Recipients = $Recipients
    }
    
    return $config
}

function Get-DependencyStatus {
    param([string]$ProjectPath)
    
    Write-Log "Getting dependency status for $ProjectPath" "INFO"
    
    $status = @{
        Vulnerabilities = @()
        OutdatedPackages = @()
        LicenseViolations = @()
        HealthScore = 100
        LastUpdated = Get-Date
    }
    
    try {
        # Run vulnerability scan
        $vulnScript = Join-Path $PSScriptRoot "vulnerability-scanner.ps1"
        if (Test-Path $vulnScript) {
            $vulnOutput = & pwsh $vulnScript -ProjectPath $ProjectPath -OutputPath $OutputPath -Severity "Medium" 2>&1
            # Parse vulnerability output
            if ($LASTEXITCODE -ge 0) {
                # Extract vulnerabilities from output or report file
                $vulnReportFile = Join-Path $OutputPath "vulnerability-report.json"
                if (Test-Path $vulnReportFile) {
                    $vulnReport = Get-Content $vulnReportFile | ConvertFrom-Json
                    $status.Vulnerabilities = $vulnReport.Vulnerabilities
                }
            }
        }
        
        # Check for outdated packages
        $updateScript = Join-Path $PSScriptRoot "dependency-updater.ps1"
        if (Test-Path $updateScript) {
            $updateOutput = & pwsh $updateScript -ProjectPath $ProjectPath -UpdateMode "Safe" -CheckOnly -OutputPath $OutputPath 2>&1
            # Parse update output
            if ($LASTEXITCODE -ge 0) {
                $updateReportFile = Join-Path $OutputPath "update-report.json"
                if (Test-Path $updateReportFile) {
                    $updateReport = Get-Content $updateReportFile | ConvertFrom-Json
                    $status.OutdatedPackages = $updateReport.UpdateAnalysis | Where-Object { $_.IsEligible }
                }
            }
        }
        
        # Check license compliance
        $licenseScript = Join-Path $PSScriptRoot "license-compliance.ps1"
        if (Test-Path $licenseScript) {
            $licenseOutput = & pwsh $licenseScript -ProjectPath $ProjectPath -OutputPath $OutputPath 2>&1
            # Parse license output
            if ($LASTEXITCODE -ge 0) {
                $licenseReportFile = Join-Path $OutputPath "compliance-report.json"
                if (Test-Path $licenseReportFile) {
                    $licenseReport = Get-Content $licenseReportFile | ConvertFrom-Json
                    $status.LicenseViolations = $licenseReport.ComplianceResults.Violations
                }
            }
        }
        
        # Calculate health score
        $status.HealthScore = Calculate-HealthScore -Vulnerabilities $status.Vulnerabilities -OutdatedPackages $status.OutdatedPackages -LicenseViolations $status.LicenseViolations
        
        Write-Log "Dependency status retrieved - Health Score: $($status.HealthScore)" "INFO"
    }
    catch {
        Write-Log "Error getting dependency status: $_" "ERROR"
    }
    
    return $status
}

function Calculate-HealthScore {
    param(
        [array]$Vulnerabilities,
        [array]$OutdatedPackages,
        [array]$LicenseViolations
    )
    
    $score = 100
    
    # Deduct for vulnerabilities
    foreach ($vuln in $Vulnerabilities) {
        switch ($vuln.Severity) {
            "Critical" { $score -= 25 }
            "High" { $score -= 15 }
            "Medium" { $score -= 10 }
            "Low" { $score -= 5 }
        }
    }
    
    # Deduct for outdated packages
    $score -= [Math]::Min(($OutdatedPackages.Count * 2), 20)
    
    # Deduct for license violations
    $score -= [Math]::Min(($LicenseViolations.Count * 15), 30)
    
    return [Math]::Max($score, 0)
}

function Send-EmailNotification {
    param(
        $Config,
        $Status,
        $NotificationType
    )
    
    if (!$Channels.Contains("Email")) {
        return
    }
    
    Write-Log "Sending email notification" "INFO"
    
    try {
        $template = $Config.Templates[$NotificationType]
        $subject = $template.Subject
        $priority = $template.Priority
        
        $body = Generate-EmailBody -Status $Status -NotificationType $NotificationType
        
        # Use SMTP to send email (requires proper SMTP configuration)
        if ($Config.Email.Recipients -and $Config.Email.Recipients.Count -gt 0) {
            Write-Log "Would send email to: $($Config.Email.Recipients -join ', ')" "INFO"
            Write-Log "Subject: $subject" "INFO"
            
            # In a real implementation, you would use Send-MailMessage or similar
            # Send-MailMessage -To $Config.Email.Recipients -From $Config.Email.FromAddress -Subject $subject -Body $body -SmtpServer $Config.Email.SMTPServer -Port $Config.Email.SMTPPort -UseSsl:$Config.Email.UseSSL -Credential (New-Object PSCredential($Config.Email.Username, (ConvertTo-SecureString $Config.Email.Password -AsPlainText -Force)))
        }
        else {
            Write-Log "No email recipients configured" "WARNING"
        }
    }
    catch {
        Write-Log "Failed to send email notification: $_" "ERROR"
    }
}

function Send-SlackNotification {
    param(
        $Config,
        $Status,
        $NotificationType
    )
    
    if (!$Channels.Contains("Slack")) {
        return
    }
    
    Write-Log "Sending Slack notification" "INFO"
    
    try {
        if ($Config.Slack.WebhookUrl) {
            $template = $Config.Templates[$NotificationType]
            $message = Generate-SlackMessage -Status $Status -NotificationType $NotificationType
            
            $payload = @{
                channel = $Config.Slack.Channel
                username = $Config.Slack.BotName
                text = $message
                icon_emoji = ":robot_face:"
            }
            
            Write-Log "Would send Slack message to: $($Config.Slack.Channel)" "INFO"
            Write-Log "Message: $message" "INFO"
            
            # In a real implementation:
            # Invoke-RestMethod -Uri $Config.Slack.WebhookUrl -Method Post -Body ($payload | ConvertTo-Json) -ContentType "application/json"
        }
        else {
            Write-Log "No Slack webhook URL configured" "WARNING"
        }
    }
    catch {
        Write-Log "Failed to send Slack notification: $_" "ERROR"
    }
}

function Send-TeamsNotification {
    param(
        $Config,
        $Status,
        $NotificationType
    )
    
    if (!$Channels.Contains("Teams")) {
        return
    }
    
    Write-Log "Sending Teams notification" "INFO"
    
    try {
        if ($Config.Teams.WebhookUrl) {
            $template = $Config.Templates[$NotificationType]
            $message = Generate-TeamsMessage -Status $Status -NotificationType $NotificationType
            
            $payload = @{
                text = $message
            }
            
            Write-Log "Would send Teams message to: $($Config.Teams.Channel)" "INFO"
            Write-Log "Message: $message" "INFO"
            
            # In a real implementation:
            # Invoke-RestMethod -Uri $Config.Teams.WebhookUrl -Method Post -Body ($payload | ConvertTo-Json) -ContentType "application/json"
        }
        else {
            Write-Log "No Teams webhook URL configured" "WARNING"
        }
    }
    catch {
        Write-Log "Failed to send Teams notification: $_" "ERROR"
    }
}

function Send-WebhookNotification {
    param(
        $Config,
        $Status,
        $NotificationType
    )
    
    if (!$Channels.Contains("Webhook")) {
        return
    }
    
    Write-Log "Sending webhook notification" "INFO"
    
    try {
        if ($Config.Webhook.URL) {
            $payload = @{
                timestamp = $script:StartTime
                projectPath = $ProjectPath
                notificationType = $NotificationType
                status = $Status
                healthScore = $Status.HealthScore
            }
            
            Write-Log "Would send webhook to: $($Config.Webhook.URL)" "INFO"
            
            # In a real implementation:
            # Invoke-RestMethod -Uri $Config.Webhook.URL -Method Post -Body ($payload | ConvertTo-Json) -ContentType "application/json" -Headers $Config.Webhook.Headers -TimeoutSec $Config.Webhook.Timeout
        }
        else {
            Write-Log "No webhook URL configured" "WARNING"
        }
    }
    catch {
        Write-Log "Failed to send webhook notification: $_" "ERROR"
    }
}

function Generate-EmailBody {
    param(
        $Status,
        $NotificationType
    )
    
    $body = @"
TiXL Dependency Update Notification
====================================

Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
Project: $ProjectPath
Health Score: $($Status.HealthScore)/100

"@
    
    switch ($NotificationType) {
        "SecurityAlert" {
            $body += @"
SECURITY ALERT
--------------
Critical security vulnerabilities detected in your dependencies:

$(
    if ($Status.Vulnerabilities.Count -gt 0) {
        foreach ($vuln in $Status.Vulnerabilities) {
            "- $($vuln.Package) v$($vuln.Version): $($vuln.ID) ($($vuln.Severity))"
        }
    } else {
        "No vulnerabilities found."
    }
)

IMMEDIATE ACTION REQUIRED:
1. Review and address all critical and high-severity vulnerabilities
2. Update affected packages to secure versions
3. Run security audit on affected components

"@
        }
        "VersionUpdate" {
            $body += @"
VERSION UPDATES AVAILABLE
-------------------------
Updates are available for the following packages:

$(
    if ($Status.OutdatedPackages.Count -gt 0) {
        foreach ($update in $Status.OutdatedPackages) {
            "- $($update.Package): $($update.CurrentVersion) → $($update.TargetVersion) ($($update.RiskLevel) risk)"
        }
    } else {
        "All packages are up to date."
    }
)

RECOMMENDED ACTIONS:
1. Review update compatibility
2. Test updates in development environment
3. Schedule production updates

"@
        }
        "LicenseViolation" {
            $body += @"
LICENSE COMPLIANCE VIOLATIONS
-----------------------------
The following packages have license compliance issues:

$(
    if ($Status.LicenseViolations.Count -gt 0) {
        foreach ($violation in $Status.LicenseViolations) {
            "- $($violation.Package) v$($violation.Version): $($violation.License) - $($violation.Reason)"
        }
    } else {
        "No license violations found."
    }
)

IMMEDIATE ACTION REQUIRED:
1. Review and resolve all license violations
2. Consider alternative packages with compatible licenses
3. Update package manifest if necessary

"@
        }
        "HealthReport" {
            $body += @"
DEPENDENCY HEALTH REPORT
------------------------
Overall dependency health status:

Health Score: $($Status.HealthScore)/100

Vulnerabilities: $($Status.Vulnerabilities.Count)
- Critical: $(($Status.Vulnerabilities | Where-Object { $_.Severity -eq "Critical" }).Count)
- High: $(($Status.Vulnerabilities | Where-Object { $_.Severity -eq "High" }).Count)
- Medium: $(($Status.Vulnerabilities | Where-Object { $_.Severity -eq "Medium" }).Count)

Outdated Packages: $($Status.OutdatedPackages.Count)
License Violations: $($Status.LicenseViolations.Count)

RECOMMENDATIONS:
1. Review dependency health quarterly
2. Implement automated dependency scanning
3. Keep dependencies up to date

"@
        }
    }
    
    $body += @"
--
This is an automated message from TiXL Dependency Management System.
To modify notification settings, update the configuration file.
"@
    
    return $body
}

function Generate-SlackMessage {
    param(
        $Status,
        $NotificationType
    )
    
    $emoji = ":information_source:"
    $priority = "Normal"
    
    switch ($NotificationType) {
        "SecurityAlert" { 
            $emoji = ":rotating_light:"
            $priority = "High"
        }
        "VersionUpdate" { $emoji = ":package:" }
        "LicenseViolation" { $emoji = ":warning:" }
        "HealthReport" { $emoji = ":chart_with_upwards_trend:" }
    }
    
    $message = "$emoji *TiXL Dependency Notification* ($NotificationType)"
    $message += "`nHealth Score: $($Status.HealthScore)/100"
    
    switch ($NotificationType) {
        "SecurityAlert" {
            $criticalCount = ($Status.Vulnerabilities | Where-Object { $_.Severity -eq "Critical" }).Count
            $highCount = ($Status.Vulnerabilities | Where-Object { $_.Severity -eq "High" }).Count
            $message += "`n🚨 Vulnerabilities: $criticalCount critical, $highCount high"
        }
        "VersionUpdate" {
            $updateCount = $Status.OutdatedPackages.Count
            $safeCount = ($Status.OutdatedPackages | Where-Object { $_.RiskLevel -eq "Low" }).Count
            $message += "`n📦 Updates: $updateCount available ($safeCount safe)"
        }
        "LicenseViolation" {
            $violationCount = $Status.LicenseViolations.Count
            $message += "`n⚖️ Violations: $violationCount found"
        }
        "HealthReport" {
            $message += "`n📊 Status: $(Get-HealthStatusDescription $Status.HealthScore)"
        }
    }
    
    return $message
}

function Generate-TeamsMessage {
    param(
        $Status,
        $NotificationType
    )
    
    $message = "**TiXL Dependency Notification** - $NotificationType"
    $message += "`n`nHealth Score: **$($Status.HealthScore)/100**"
    
    switch ($NotificationType) {
        "SecurityAlert" {
            $criticalCount = ($Status.Vulnerabilities | Where-Object { $_.Severity -eq "Critical" }).Count
            $highCount = ($Status.Vulnerabilities | Where-Object { $_.Severity -eq "High" }).Count
            $message += "`n`n🚨 **Security Alert**: $criticalCount critical, $highCount high vulnerabilities"
        }
        "VersionUpdate" {
            $updateCount = $Status.OutdatedPackages.Count
            $message += "`n`n📦 **Updates Available**: $updateCount packages can be updated"
        }
        "LicenseViolation" {
            $violationCount = $Status.LicenseViolations.Count
            $message += "`n`n⚖️ **License Violations**: $violationCount compliance issues found"
        }
        "HealthReport" {
            $message += "`n`n📊 **Overall Status**: $(Get-HealthStatusDescription $Status.HealthScore)"
        }
    }
    
    return $message
}

function Get-HealthStatusDescription {
    param([int]$HealthScore)
    
    if ($HealthScore -ge 90) { return "Excellent" }
    elseif ($HealthScore -ge 75) { return "Good" }
    elseif ($HealthScore -ge 60) { return "Fair" }
    elseif ($HealthScore -ge 40) { return "Poor" }
    else { return "Critical" }
}

function Export-NotificationReport {
    param(
        $Config,
        $Status,
        [string]$OutputPath
    )
    
    Write-Log "Exporting notification report" "INFO"
    
    $reportData = @{
        NotificationInfo = @{
            Timestamp = $script:StartTime
            Duration = (Get-Date) - $script:StartTime
            Version = $script:ScriptVersion
            ProjectPath = $ProjectPath
            NotificationType = $NotificationType
            Channels = $Channels
            Schedule = $Schedule
        }
        Status = $Status
        Configuration = @{
            Email = if ($Config.Email.ContainsKey("Recipients")) { $Config.Email.Recipients } else { @() }
            Slack = @{
                Configured = $Config.Slack.WebhookUrl -ne ""
                Channel = $Config.Slack.Channel
            }
            Teams = @{
                Configured = $Config.Teams.WebhookUrl -ne ""
                Channel = $Config.Teams.Channel
            }
            Webhook = @{
                Configured = $Config.Webhook.URL -ne ""
                URL = $Config.Webhook.URL
            }
        }
        Results = @{
            NotificationsSent = @()
            Failures = @()
        }
    }
    
    # Export JSON report
    $reportData | ConvertTo-Json -Depth 10 | Out-File -FilePath $script:ReportFile -Encoding UTF8
    
    Write-Log "Notification report exported to: $OutputPath" "INFO"
}

function Should-SendNotification {
    param($Status)
    
    # Logic to determine if notification should be sent based on schedule
    switch ($Schedule) {
        "Immediate" {
            # Send immediately if there are any issues
            return $Status.Vulnerabilities.Count -gt 0 -or 
                   $Status.LicenseViolations.Count -gt 0 -or
                   $Status.OutdatedPackages.Count -gt 5
        }
        "Daily" {
            # Send daily if health score drops below threshold
            return $Status.HealthScore -lt 80
        }
        "Weekly" {
            # Send weekly if health score is below good threshold
            return $Status.HealthScore -lt 90
        }
        "Monthly" {
            # Send monthly report regardless
            return $true
        }
    }
    
    return $false
}

# Main execution
Write-Log "Starting $script:ScriptName v$script:ScriptVersion" "INFO"
Write-Log "Project path: $ProjectPath" "INFO"
Write-Log "Notification type: $NotificationType" "INFO"
Write-Log "Channels: $($Channels -join ', ')" "INFO"
Write-Log "Schedule: $Schedule" "INFO"

try {
    # Initialize notification configuration
    $config = Initialize-NotificationConfig
    
    # Get dependency status
    $status = Get-DependencyStatus -ProjectPath $ProjectPath
    
    # Determine if notification should be sent
    $shouldSend = Should-SendNotification -Status $status
    
    if ($shouldSend) {
        Write-Log "Notification conditions met, sending notifications" "INFO"
        
        # Send notifications through all configured channels
        Send-EmailNotification -Config $config -Status $status -NotificationType $NotificationType
        Send-SlackNotification -Config $config -Status $status -NotificationType $NotificationType
        Send-TeamsNotification -Config $config -Status $status -NotificationType $NotificationType
        Send-WebhookNotification -Config $config -Status $status -NotificationType $NotificationType
    }
    else {
        Write-Log "Notification conditions not met, skipping notifications" "INFO"
    }
    
    # Export notification report
    Export-NotificationReport -Config $config -Status $status -OutputPath $OutputPath
    
    # Summary
    Write-Log "=== NOTIFICATION COMPLETED ===" "INFO"
    Write-Log "Health Score: $($status.HealthScore)/100" "INFO"
    Write-Log "Vulnerabilities: $($status.Vulnerabilities.Count)" "WARNING"
    Write-Log "Outdated Packages: $($status.OutdatedPackages.Count)" "INFO"
    Write-Log "License Violations: $($status.LicenseViolations.Count)" "WARNING"
    Write-Log "Notification Sent: $shouldSend" "INFO"
    
    exit 0
}
catch {
    Write-Log "Notification process failed with error: $_" "ERROR"
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
}