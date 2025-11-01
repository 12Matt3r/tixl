#!/usr/bin/env pwsh
# TiXL Quality Gate Configuration Script
# Configures and validates CI/CD quality gates

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ConfigPath = "quality-gates.json",
    
    [Parameter(Mandatory = $false)]
    [switch]$Validate,
    
    [Parameter(Mandatory = $false)]
    [switch]$Update,
    
    [Parameter(Mandatory = $false)]
    [string]$Environment = "development"
)

$ErrorActionPreference = "Stop"

# Default quality gate configuration
$DefaultConfig = @{
    version = "1.0.0"
    environment = $Environment
    gates = @{
        preCommit = @{
            enabled = $true
            rules = @(
                @{
                    name = "Code formatting check"
                    command = "dotnet format --verify-no-changes"
                    severity = "error"
                    timeout = "300s"
                    enabled = $true
                },
                @{
                    name = "Basic syntax validation"
                    command = "dotnet build --configuration Debug"
                    severity = "error"
                    timeout = "600s"
                    enabled = $true
                },
                @{
                    name = "Unit test smoke test"
                    command = "dotnet test --filter Category=Smoke"
                    severity = "warning"
                    threshold = "90%"
                    timeout = "300s"
                    enabled = $true
                }
            )
        }
        
        pullRequest = @{
            enabled = $true
            rules = @(
                @{
                    name = "Full test suite"
                    command = "dotnet test --configuration Release"
                    severity = "error"
                    timeout = "1800s"
                    enabled = $true
                },
                @{
                    name = "Code coverage"
                    command = "dotnet test --collect:CodeCoverage"
                    severity = "error"
                    threshold = "80%"
                    failUnder = "75%"
                    enabled = $true
                },
                @{
                    name = "Static analysis"
                    command = "dotnet build --configuration Release /p:TreatWarningsAsErrors=true"
                    severity = "error"
                    enabled = $true
                },
                @{
                    name = "Security scan"
                    command = "dotnet audit --audit-level moderate"
                    severity = "error"
                    allowedVulnerabilities = @("low")
                    enabled = $true
                },
                @{
                    name = "Performance regression"
                    command = "dotnet run --project Benchmarks -- --job short"
                    severity = "warning"
                    regressionThreshold = "10%"
                    warnThreshold = "5%"
                    enabled = $true
                }
            )
        }
        
        mergeGate = @{
            enabled = $true
            requiredChecks = @(
                "unit-tests"
                "integration-tests"
                "security-scan"
                "code-quality"
            )
            minimumReviewers = 2
            waitForAllChecks = $true
        }
        
        releaseGate = @{
            enabled = $true
            requiredChecks = @(
                "e2e-tests"
                "performance-benchmarks"
                "security-audit"
                "package-validation"
            )
            manualApproval = $true
            qualityThreshold = 95
        }
    }
    
    thresholds = @{
        codeCoverage = @{
            overall = 80
            core = 85
            operators = 80
            editor = 75
            critical = 70
        }
        
        performance = @{
            buildTime = "10 minutes"
            regressionThreshold = 10
            criticalRegressionThreshold = 25
            memoryRegressionThreshold = 5
        }
        
        security = @{
            criticalVulnerabilities = 0
            highVulnerabilities = 0
            moderateVulnerabilities = 5
            lowVulnerabilities = 10
        }
        
        quality = @{
            codeSmells = 10
            bugs = 0
            vulnerabilities = 0
            duplicatedLines = 3
        }
    }
    
    notifications = @{
        slack = @{
            enabled = $false
            webhook = ""
            channel = "#tixl-dev"
        }
        
        email = @{
            enabled = $false
            recipients = @("dev-team@tixl.com")
            smtpServer = ""
        }
        
        github = @{
            enabled = $true
            createIssues = $false
            assignReviewers = $true
        }
    }
    
    profiles = @{
        strict = @{
            description = "Maximum quality requirements"
            coverageThreshold = 85
            performanceThreshold = 5
            securityLevel = "strict"
        }
        
        standard = @{
            description = "Balanced quality requirements"
            coverageThreshold = 80
            performanceThreshold = 10
            securityLevel = "moderate"
        }
        
        relaxed = @{
            description = "Minimum quality requirements"
            coverageThreshold = 75
            performanceThreshold = 15
            securityLevel = "basic"
        }
    }
}

function Write-Quiet {
    param([string]$Message)
    if (-not $Quiet) {
        Write-Host $Message
    }
}

function Test-ConfigFile {
    param([string]$Path)
    
    if (-not (Test-Path $Path)) {
        Write-Host "Configuration file not found: $Path" -ForegroundColor Yellow
        return $false
    }
    
    try {
        $config = Get-Content $Path -Raw | ConvertFrom-Json
        return $true
    }
    catch {
        Write-Host "Invalid JSON in configuration file: $Path" -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor Red
        return $false
    }
}

function New-QualityGateConfig {
    param([string]$Path)
    
    Write-Host "Creating new quality gate configuration..." -ForegroundColor Green
    Write-Host "Environment: $Environment" -ForegroundColor Yellow
    
    # Apply environment-specific overrides
    $config = $DefaultConfig.Clone()
    $config.environment = $Environment
    
    switch ($Environment.ToLower()) {
        "production" {
            $config.gates.mergeGate.minimumReviewers = 3
            $config.gates.releaseGate.qualityThreshold = 98
            $config.thresholds.codeCoverage.overall = 85
        }
        "staging" {
            $config.gates.mergeGate.minimumReviewers = 2
            $config.gates.releaseGate.qualityThreshold = 95
        }
        "development" {
            $config.gates.mergeGate.minimumReviewers = 1
            $config.gates.releaseGate.manualApproval = $false
        }
    }
    
    # Save configuration
    $config | ConvertTo-Json -Depth 10 | Out-File -FilePath $Path -Encoding UTF8
    Write-Host "Configuration saved to: $Path" -ForegroundColor Green
}

function Test-QualityGates {
    param([string]$ConfigPath)
    
    Write-Host "Validating quality gate configuration..." -ForegroundColor Green
    
    if (-not (Test-ConfigFile $ConfigPath)) {
        exit 1
    }
    
    $config = Get-Content $ConfigPath -Raw | ConvertFrom-Json
    
    # Validate gate structure
    $requiredGates = @("preCommit", "pullRequest", "mergeGate", "releaseGate")
    foreach ($gate in $requiredGates) {
        if (-not $config.gates.$gate) {
            Write-Host "Missing required gate: $gate" -ForegroundColor Red
            exit 1
        }
        
        if (-not $config.gates.$gate.enabled) {
            Write-Host "Gate is disabled: $gate" -ForegroundColor Yellow
        }
    }
    
    # Validate thresholds
    $requiredThresholds = @("codeCoverage", "performance", "security", "quality")
    foreach ($threshold in $requiredThresholds) {
        if (-not $config.thresholds.$threshold) {
            Write-Host "Missing threshold category: $threshold" -ForegroundColor Red
            exit 1
        }
    }
    
    # Validate code coverage thresholds
    $coverageThresholds = $config.thresholds.codeCoverage
    if ($coverageThresholds.overall -lt $coverageThresholds.critical) {
        Write-Host "Code coverage threshold logic error: overall < critical" -ForegroundColor Red
        exit 1
    }
    
    # Test command syntax for each rule
    Write-Host "Testing rule commands..." -ForegroundColor Yellow
    foreach ($gateName in $config.gates.PSObject.Properties.Name) {
        $gate = $config.gates.$gateName
        if ($gate.rules) {
            foreach ($rule in $gate.rules) {
                if ($rule.enabled -and $rule.command) {
                    Write-Host "Testing rule: $($rule.name)" -ForegroundColor Gray
                    
                    # Basic command validation
                    $parts = $rule.command.Split(' ')
                    if (-not (Get-Command $parts[0] -ErrorAction SilentlyContinue)) {
                        Write-Host "Warning: Command not found: $($rule.command)" -ForegroundColor Yellow
                    }
                }
            }
        }
    }
    
    Write-Host "✅ Quality gate configuration is valid" -ForegroundColor Green
    
    # Generate summary
    Write-Host "`nConfiguration Summary:" -ForegroundColor Cyan
    Write-Host "Environment: $($config.environment)" -ForegroundColor Gray
    Write-Host "Pre-commit gates: $($config.gates.preCommit.enabled)" -ForegroundColor Gray
    Write-Host "Pull request gates: $($config.gates.pullRequest.enabled)" -ForegroundColor Gray
    Write-Host "Merge gates: $($config.gates.mergeGate.enabled)" -ForegroundColor Gray
    Write-Host "Release gates: $($config.gates.releaseGate.enabled)" -ForegroundColor Gray
    Write-Host "Code coverage threshold: $($config.thresholds.codeCoverage.overall)%" -ForegroundColor Gray
    Write-Host "Performance threshold: $($config.thresholds.performance.regressionThreshold)%" -ForegroundColor Gray
}

function Update-QualityGates {
    param([string]$ConfigPath)
    
    Write-Host "Updating quality gate configuration..." -ForegroundColor Green
    
    if (-not (Test-Path $ConfigPath)) {
        Write-Host "Configuration file not found. Creating new one..." -ForegroundColor Yellow
        New-QualityGateConfig -Path $ConfigPath
        return
    }
    
    $config = Get-Content $ConfigPath -Raw | ConvertFrom-Json
    $originalVersion = $config.version
    
    # Compare versions and update if needed
    if ([version]$config.version -lt [version]$DefaultConfig.version) {
        Write-Host "Updating configuration from version $originalVersion to $($DefaultConfig.version)" -ForegroundColor Yellow
        
        # Merge new defaults with existing configuration
        $updatedConfig = $DefaultConfig.Clone()
        
        # Preserve environment-specific settings
        $updatedConfig.environment = $config.environment
        
        # Merge gate settings (preserve custom rules)
        foreach ($gateName in $config.gates.PSObject.Properties.Name) {
            if ($updatedConfig.gates.$gateName) {
                $updatedConfig.gates.$gateName.enabled = $config.gates.$gateName.enabled
                if ($config.gates.$gateName.rules -and $updatedConfig.gates.$gateName.rules) {
                    # Merge rule configurations
                    $updatedConfig.gates.$gateName.rules = $config.gates.$gateName.rules
                }
            }
        }
        
        # Merge threshold settings
        foreach ($thresholdName in $config.thresholds.PSObject.Properties.Name) {
            if ($updatedConfig.thresholds.$thresholdName) {
                $updatedConfig.thresholds.$thresholdName = $config.thresholds.$thresholdName
            }
        }
        
        # Merge notification settings
        $updatedConfig.notifications = $config.notifications
        
        # Merge profile settings
        $updatedConfig.profiles = $config.profiles
        
        # Save updated configuration
        $updatedConfig | ConvertTo-Json -Depth 10 | Out-File -FilePath $ConfigPath -Encoding UTF8
        Write-Host "Configuration updated successfully" -ForegroundColor Green
    } else {
        Write-Host "Configuration is up to date (version $($config.version))" -ForegroundColor Green
    }
}

function Show-QualityGateStatus {
    param([string]$ConfigPath)
    
    if (-not (Test-Path $ConfigPath)) {
        Write-Host "No configuration file found. Run with -Update to create one." -ForegroundColor Yellow
        return
    }
    
    $config = Get-Content $ConfigPath -Raw | ConvertFrom-Json
    
    Write-Host "TiXL Quality Gate Status" -ForegroundColor Cyan
    Write-Host "=========================" -ForegroundColor Cyan
    
    Write-Host "Environment: $($config.environment)" -ForegroundColor Gray
    Write-Host "Version: $($config.version)" -ForegroundColor Gray
    
    Write-Host "`nGate Status:" -ForegroundColor Yellow
    foreach ($gateName in $config.gates.PSObject.Properties.Name) {
        $gate = $config.gates.$gateName
        $status = if ($gate.enabled) { "✅ Enabled" } else { "❌ Disabled" }
        $gateDisplay = $gateName -replace '([A-Z])', ' $1'
        Write-Host "$gateDisplay`: $status" -ForegroundColor $(
            if ($gate.enabled) { "Green" } else { "Red" }
        )
    }
    
    Write-Host "`nThresholds:" -ForegroundColor Yellow
    Write-Host "Code Coverage: $($config.thresholds.codeCoverage.overall)% (core: $($config.thresholds.codeCoverage.core)%, operators: $($config.thresholds.codeCoverage.operators)%)" -ForegroundColor Gray
    Write-Host "Performance Regression: $($config.thresholds.performance.regressionThreshold)%" -ForegroundColor Gray
    Write-Host "Security (Critical/High): $($config.thresholds.security.criticalVulnerabilities)/$($config.thresholds.security.highVulnerabilities)" -ForegroundColor Gray
    
    Write-Host "`nNotification Channels:" -ForegroundColor Yellow
    Write-Host "GitHub: $($config.notifications.github.enabled)" -ForegroundColor Gray
    Write-Host "Slack: $($config.notifications.slack.enabled)" -ForegroundColor Gray
    Write-Host "Email: $($config.notifications.email.enabled)" -ForegroundColor Gray
}

# Main execution
try {
    if ($Validate) {
        Test-QualityGates -ConfigPath $ConfigPath
    } elseif ($Update) {
        Update-QualityGates -ConfigPath $ConfigPath
    } else {
        Show-QualityGateStatus -ConfigPath $ConfigPath
        
        Write-Host "`nUsage:" -ForegroundColor Cyan
        Write-Host "  .\quality-gate-config.ps1                    # Show status" -ForegroundColor Gray
        Write-Host "  .\quality-gate-config.ps1 -Validate          # Validate configuration" -ForegroundColor Gray
        Write-Host "  .\quality-gate-config.ps1 -Update            # Update configuration" -ForegroundColor Gray
        Write-Host "  .\quality-gate-config.ps1 -Update -Environment production  # Update for production" -ForegroundColor Gray
    }
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
