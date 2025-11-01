#!/usr/bin/env pwsh
<#
.SYNOPSIS
    TiXL Dependency Management System Initialization Script

.DESCRIPTION
    Initializes the TiXL dependency management system by:
    - Setting up configuration files
    - Installing required tools
    - Creating necessary directories
    - Setting up CI/CD integration
    - Configuring pre-commit hooks
    - Validating the installation

.PARAMETER ProjectPath
    Path to the TiXL solution or project (defaults to current directory)

.PARAMETER Force
    Overwrite existing configuration files

.PARAMETER InstallTools
    Install required .NET tools

.PARAMETER SetupCI
    Set up CI/CD integration

.PARAMETER SetupHooks
    Set up pre-commit hooks

.EXAMPLE
    .\Initialize-DependencyManagement.ps1

.EXAMPLE
    .\Initialize-DependencyManagement.ps1 -ProjectPath "C:\Projects\TiXL" -InstallTools -SetupCI
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$false)]
    [string]$ProjectPath = $PWD.Path,
    
    [Parameter(Mandatory=$false)]
    [switch]$Force,
    
    [Parameter(Mandatory=$false)]
    [switch]$InstallTools,
    
    [Parameter(Mandatory=$false)]
    [switch]$SetupCI,
    
    [Parameter(Mandatory=$false)]
    [switch]$SetupHooks
)

$script:ScriptName = "TiXL Dependency Management Initializer"
$script:ScriptVersion = "1.0.0"
$script:StartTime = Get-Date

# Configuration paths
$script:SolutionPath = $ProjectPath
$script:ConfigPath = Join-Path $ProjectPath "docs\config"
$script:ScriptsPath = Join-Path $ProjectPath "docs\scripts"
$script:PipelinesPath = Join-Path $ProjectPath "docs\pipelines"
$script:HooksPath = Join-Path $ProjectPath ".git\hooks"

# Required .NET tools
$script:RequiredTools = @(
    @{ Name = "dotnet-outdated-tool"; Description = "NuGet package update checker" },
    @{ Name = "dotnet-tools-audit"; Description = "Security vulnerability scanner" },
    @{ Name = "cve-url-parse"; Description = "CVE information parser" }
)

function Write-Header {
    param([string]$Message)
    
    $separator = "=" * $Message.Length
    Write-Host "`n$separator" -ForegroundColor Cyan
    Write-Host $Message -ForegroundColor Cyan -NoNewline
    Write-Host "`n$separator" -ForegroundColor Cyan
}

function Write-Step {
    param([string]$Message, [int]$Step, [int]$Total)
    
    Write-Host "[$Step/$Total] $Message" -ForegroundColor Yellow
}

function Write-Success {
    param([string]$Message)
    
    Write-Host "✅ $Message" -ForegroundColor Green
}

function Write-Warning {
    param([string]$Message)
    
    Write-Host "⚠️  $Message" -ForegroundColor Yellow
}

function Write-Error {
    param([string]$Message)
    
    Write-Host "❌ $Message" -ForegroundColor Red
}

function Test-Prerequisites {
    Write-Header "Checking Prerequisites"
    
    # Check PowerShell version
    if ($PSVersionTable.PSVersion.Major -lt 5) {
        Write-Error "PowerShell 5.0 or later is required. Current version: $($PSVersionTable.PSVersion)"
        return $false
    }
    Write-Success "PowerShell version: $($PSVersionTable.PSVersion)"
    
    # Check .NET SDK
    try {
        $dotnetVersion = & dotnet --version 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Success ".NET SDK version: $dotnetVersion"
        }
        else {
            Write-Error ".NET SDK is not installed or not in PATH"
            return $false
        }
    }
    catch {
        Write-Error ".NET SDK is not installed or not in PATH"
        return $false
    }
    
    # Check project structure
    if (!(Test-Path $script:SolutionPath)) {
        Write-Error "Project path not found: $script:SolutionPath"
        return $false
    }
    
    $hasSolution = Test-Path (Join-Path $script:SolutionPath "*.sln")
    $hasProject = Test-Path (Join-Path $script:SolutionPath "*.csproj")
    
    if (!$hasSolution -and !$hasProject) {
        Write-Error "No .sln or .csproj files found in $script:SolutionPath"
        return $false
    }
    Write-Success "Project structure validated"
    
    return $true
}

function Initialize-DirectoryStructure {
    Write-Header "Creating Directory Structure"
    
    $directories = @(
        $script:ConfigPath,
        $script:ScriptsPath,
        $script:PipelinesPath,
        (Join-Path $script:SolutionPath ".git\hooks")
    )
    
    foreach ($dir in $directories) {
        if (!(Test-Path $dir)) {
            New-Item -Path $dir -ItemType Directory -Force | Out-Null
            Write-Success "Created directory: $dir"
        }
        else {
            Write-Warning "Directory already exists: $dir"
        }
    }
}

function Install-RequiredTools {
    if (!$InstallTools) {
        Write-Header "Skipping Tool Installation"
        Write-Host "Use -InstallTools flag to install required .NET tools" -ForegroundColor Gray
        return
    }
    
    Write-Header "Installing Required .NET Tools"
    
    foreach ($tool in $script:RequiredTools) {
        Write-Step "Installing $($tool.Name)" 1 $script:RequiredTools.Count
        
        try {
            $output = & dotnet tool install --global $tool.Name 2>&1
            if ($LASTEXITCODE -eq 0) {
                Write-Success "Installed $($tool.Name) - $($tool.Description)"
            }
            else {
                Write-Warning "Failed to install $($tool.Name): $output"
            }
        }
        catch {
            Write-Warning "Error installing $($tool.Name): $_"
        }
    }
    
    # Verify installations
    Write-Header "Verifying Tool Installations"
    foreach ($tool in $script:RequiredTools) {
        try {
            $installed = & dotnet tool list --global 2>$null | Where-Object { $_ -match $tool.Name }
            if ($installed) {
                Write-Success "$($tool.Name) is installed"
            }
            else {
                Write-Warning "$($tool.Name) may not be installed correctly"
            }
        }
        catch {
            Write-Warning "Unable to verify $($tool.Name) installation"
        }
    }
}

function Copy-ConfigurationFiles {
    Write-Header "Setting Up Configuration Files"
    
    $templatePath = Join-Path $PSScriptRoot "..\config"
    
    if (!(Test-Path $templatePath)) {
        Write-Error "Configuration templates not found: $templatePath"
        return
    }
    
    $configFiles = Get-ChildItem -Path $templatePath -Filter "*.json"
    
    foreach ($configFile in $configFiles) {
        $targetPath = Join-Path $script:ConfigPath $configFile.Name
        
        if (Test-Path $targetPath -and !$Force) {
            Write-Warning "Configuration file already exists: $targetPath (use -Force to overwrite)"
            continue
        }
        
        Copy-Item -Path $configFile.FullName -Destination $targetPath -Force
        Write-Success "Created configuration file: $($configFile.Name)"
    }
}

function Copy-Scripts {
    Write-Header "Setting Up Scripts"
    
    $templatePath = Join-Path $PSScriptRoot "..\scripts"
    
    if (!(Test-Path $templatePath)) {
        Write-Error "Scripts not found: $templatePath"
        return
    }
    
    $scriptFiles = Get-ChildItem -Path $templatePath -Filter "*.ps1"
    
    foreach ($scriptFile in $scriptFiles) {
        $targetPath = Join-Path $script:ScriptsPath $scriptFile.Name
        
        if (Test-Path $targetPath -and !$Force) {
            Write-Warning "Script already exists: $targetPath (use -Force to overwrite)"
            continue
        }
        
        Copy-Item -Path $scriptFile.FullName -Destination $targetPath -Force
        Write-Success "Created script: $($scriptFile.Name)"
    }
}

function Setup-CICDIntegration {
    if (!$SetupCI) {
        Write-Header "Skipping CI/CD Integration"
        Write-Host "Use -SetupCI flag to set up CI/CD integration" -ForegroundColor Gray
        return
    }
    
    Write-Header "Setting Up CI/CD Integration"
    
    # Check if Azure DevOps is being used
    if ((Test-Path ".azure\pipelines") -or (Test-Path "azure-pipelines.yml")) {
        Write-Step "Integrating with Azure DevOps" 1 2
        
        $pipelineFile = Join-Path $script:PipelinesPath "enhanced-azure-pipelines.yml"
        $pipelineTemplate = Join-Path $PSScriptRoot "..\pipelines\enhanced-azure-pipelines.yml"
        
        if (Test-Path $pipelineTemplate) {
            Copy-Item -Path $pipelineTemplate -Destination $pipelineFile -Force
            Write-Success "Created enhanced Azure DevOps pipeline"
        }
        
        $validationFile = Join-Path $script:PipelinesPath "dependency-validation-stage.yml"
        $validationTemplate = Join-Path $PSScriptRoot "..\pipelines\dependency-validation-stage.yml"
        
        if (Test-Path $validationTemplate) {
            Copy-Item -Path $validationTemplate -Destination $validationFile -Force
            Write-Success "Created standalone dependency validation stage"
        }
    }
    else {
        Write-Warning "No Azure DevOps pipeline detected. Manual CI/CD integration may be required."
    }
    
    Write-Step "Updating existing pipeline configuration" 2 2
    
    # If there's an existing azure-pipelines.yml, suggest adding the dependency validation stage
    $existingPipeline = Get-ChildItem -Path $script:SolutionPath -Filter "azure-pipelines.yml" -Recurse
    if ($existingPipeline) {
        Write-Host "`nTo integrate dependency validation into your existing Azure DevOps pipeline:" -ForegroundColor Cyan
        Write-Host "1. Add the dependency validation stage from docs/pipelines/dependency-validation-stage.yml" -ForegroundColor Gray
        Write-Host "2. Reference it in your existing pipeline after the build stage" -ForegroundColor Gray
        Write-Host "3. Configure notification settings in docs/config/notification-config.json" -ForegroundColor Gray
    }
}

function Setup-PreCommitHooks {
    if (!$SetupHooks) {
        Write-Header "Skipping Pre-commit Hooks Setup"
        Write-Host "Use -SetupHooks flag to set up pre-commit hooks" -ForegroundColor Gray
        return
    }
    
    Write-Header "Setting Up Pre-commit Hooks"
    
    # Check if .git directory exists
    if (!(Test-Path ".git")) {
        Write-Warning "No .git directory found. Pre-commit hooks require a Git repository."
        Write-Host "Initialize Git repository first: git init" -ForegroundColor Gray
        return
    }
    
    # Check if pre-commit is installed
    try {
        & pre-commit --version 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Success "pre-commit is installed"
        }
        else {
            Write-Error "pre-commit is not installed. Install it first: pip install pre-commit"
            return
        }
    }
    catch {
        Write-Error "pre-commit is not installed. Install it first: pip install pre-commit"
        return
    }
    
    # Create .pre-commit-config.yaml
    $hookConfig = Join-Path $script:SolutionPath ".pre-commit-config.yaml"
    $hookTemplate = Join-Path $PSScriptRoot "..\hooks\pre-commit-config.yml"
    
    if (Test-Path $hookTemplate) {
        Copy-Item -Path $hookTemplate -Destination $hookConfig -Force
        Write-Success "Created .pre-commit-config.yaml"
    }
    
    # Install pre-commit hooks
    try {
        & pre-commit install 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Pre-commit hooks installed"
        }
        else {
            Write-Warning "Failed to install pre-commit hooks"
        }
    }
    catch {
        Write-Warning "Error installing pre-commit hooks: $_"
    }
    
    # Run initial pre-commit check
    try {
        Write-Host "`nRunning initial pre-commit check..." -ForegroundColor Cyan
        & pre-commit run --all-files 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Pre-commit checks passed"
        }
        else {
            Write-Warning "Pre-commit checks found issues (this is normal for first run)"
        }
    }
    catch {
        Write-Warning "Error running pre-commit checks: $_"
    }
}

function Validate-Installation {
    Write-Header "Validating Installation"
    
    $validationSteps = @{
        "Configuration files" = { Test-Path (Join-Path $script:ConfigPath "*.json") }
        "Scripts" = { Test-Path (Join-Path $script:ScriptsPath "*.ps1") }
        "Pipeline templates" = { Test-Path (Join-Path $script:PipelinesPath "*.yml") }
    }
    
    $validationResults = @{}
    
    foreach ($stepName in $validationSteps.Keys) {
        try {
            $result = & $validationSteps[$stepName]
            if ($result) {
                Write-Success "$stepName: OK"
                $validationResults[$stepName] = $true
            }
            else {
                Write-Error "$stepName: Missing"
                $validationResults[$stepName] = $false
            }
        }
        catch {
            Write-Error "$stepName: Error - $_"
            $validationResults[$stepName] = $false
        }
    }
    
    return ($validationResults.Values | Where-Object { $_ }).Count -eq $validationResults.Count
}

function Generate-UsageDocumentation {
    Write-Header "Generating Usage Documentation"
    
    $docPath = Join-Path $script:SolutionPath "docs\dependency-management-usage.md"
    
    $documentation = @"
# TiXL Dependency Management Usage Guide

## Quick Start

### Running Individual Checks

```powershell
# Security vulnerability scan
pwsh docs/scripts/vulnerability-scanner.ps1 -ProjectPath "TiXL.sln" -Severity "High"

# License compliance check
pwsh docs/scripts/license-compliance.ps1 -ProjectPath "TiXL.sln" -GenerateReport

# Dependency audit
pwsh docs/scripts/dependency-audit.ps1 -SolutionPath "TiXL.sln" -Verbose

# Check for updates
pwsh docs/scripts/dependency-updater.ps1 -ProjectPath "TiXL.sln" -UpdateMode "Safe" -CheckOnly

# Dependency analysis
pwsh docs/scripts/dependency-analyzer.ps1 -SolutionPath "TiXL.sln" -GenerateVisualization

# Send notifications
pwsh docs/scripts/update-notifier.ps1 -ProjectPath "TiXL.sln" -NotificationType "HealthReport"
```

### CI/CD Integration

The enhanced dependency management is integrated into your Azure DevOps pipeline:

1. **Automated Vulnerability Scanning**: Runs in build stage
2. **License Compliance Checks**: Validates all package licenses
3. **Dependency Tree Analysis**: Analyzes and optimizes dependency structure
4. **Automated Updates**: Safe dependency updates (manual trigger)
5. **Notifications**: Health reports and alerts

### Configuration

Update these files to customize behavior:
- `docs/config/dependency-config.json` - General settings
- `docs/config/vulnerability-rules.json` - Security scanning rules
- `docs/config/license-whitelist.json` - License policy
- `docs/config/update-policies.json` - Update behavior
- `docs/config/notification-config.json` - Notification settings

### Pre-commit Hooks

If pre-commit hooks are enabled, the following checks run automatically:
- Security vulnerability quick scan
- License compliance check
- Project file validation
- Dependency tree validation

## Features

### Security Monitoring
- Real-time vulnerability detection
- CVE database integration
- Custom vulnerability rules
- Severity-based alerting

### License Management
- Automated license detection
- Policy enforcement
- Compliance reporting
- Compatibility analysis

### Dependency Optimization
- Circular dependency detection
- Unused dependency identification
- Version conflict resolution
- Performance impact analysis

### Automated Updates
- Safe update detection
- Risk assessment
- Automated testing
- Rollback capability

### Notifications
- Multi-channel notifications (Email, Slack, Teams, Webhook)
- Scheduled health reports
- Real-time security alerts
- Update status notifications

## Troubleshooting

### Common Issues

1. **Permission errors**: Run PowerShell as administrator
2. **Tool not found**: Ensure .NET tools are installed globally
3. **Network issues**: Configure proxy settings if behind firewall
4. **Build failures**: Check dependency compatibility

### Getting Help

1. Check log files in output directories
2. Validate configuration files
3. Run with -Verbose flag for detailed output
4. Review documentation in docs/

---
Generated by TiXL Dependency Management Initializer v$script:ScriptVersion
"@
    
    $documentation | Out-File -FilePath $docPath -Encoding UTF8
    Write-Success "Usage documentation created: $docPath"
}

function Show-Summary {
    Write-Header "Installation Summary"
    
    Write-Host "TiXL Dependency Management System has been initialized!" -ForegroundColor Green
    Write-Host ""
    Write-Host "📁 Configuration files: docs/config/" -ForegroundColor Cyan
    Write-Host "🔧 Scripts: docs/scripts/" -ForegroundColor Cyan
    Write-Host "🚀 CI/CD: docs/pipelines/" -ForegroundColor Cyan
    Write-Host "🪝 Pre-commit: .pre-commit-config.yaml" -ForegroundColor Cyan
    Write-Host ""
    
    $totalDuration = (Get-Date) - $script:StartTime
    Write-Host "⏱️  Setup completed in $($totalDuration.TotalSeconds.ToString("F1")) seconds" -ForegroundColor Green
    
    Write-Host "`n🎯 Next Steps:" -ForegroundColor Yellow
    Write-Host "1. Review and customize configuration files" -ForegroundColor Gray
    Write-Host "2. Run your first dependency audit: pwsh docs/scripts/dependency-audit.ps1 -SolutionPath 'TiXL.sln'" -ForegroundColor Gray
    Write-Host "3. Set up notification channels in docs/config/notification-config.json" -ForegroundColor Gray
    Write-Host "4. Update your CI/CD pipeline to include dependency validation" -ForegroundColor Gray
    Write-Host "5. Train your team on the new dependency management processes" -ForegroundColor Gray
    
    Write-Host "`n📚 Documentation: docs/dependency-management-usage.md" -ForegroundColor Cyan
}

# Main execution
try {
    Write-Host "$script:ScriptName v$script:ScriptVersion" -ForegroundColor Cyan
    Write-Host "Project: $script:SolutionPath"
    Write-Host "Started: $script:StartTime"
    
    # Check prerequisites
    if (!(Test-Prerequisites)) {
        Write-Error "Prerequisites check failed. Please resolve issues and retry."
        exit 1
    }
    
    # Initialize directory structure
    Initialize-DirectoryStructure
    
    # Install tools if requested
    Install-RequiredTools
    
    # Copy configuration files
    Copy-ConfigurationFiles
    
    # Copy scripts
    Copy-Scripts
    
    # Setup CI/CD integration
    Setup-CICDIntegration
    
    # Setup pre-commit hooks
    Setup-PreCommitHooks
    
    # Validate installation
    $isValid = Validate-Installation
    
    # Generate documentation
    Generate-UsageDocumentation
    
    # Show summary
    Show-Summary
    
    if ($isValid) {
        Write-Success "Installation completed successfully!"
        exit 0
    }
    else {
        Write-Warning "Installation completed with warnings. Please review the output above."
        exit 1
    }
}
catch {
    Write-Error "Installation failed: $_"
    Write-Host "Error details: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}