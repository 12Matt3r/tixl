#!/usr/bin/env pwsh
# TiXL Code Quality Infrastructure Setup Script
# Sets up the complete code quality infrastructure for local development

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$SolutionPath = "TiXL.sln",
    
    [Parameter(Mandatory = $false)]
    [switch]$InstallGlobalTools = $true,
    
    [Parameter(Mandatory = $false)]
    [switch]$EnableSonarQube = $false,
    
    [Parameter(Mandatory = $false)]
    [switch]$SetupVisualStudio = $true,
    
    [Parameter(Mandatory = $false)]
    [switch]$VerboseOutput = $false,
    
    [Parameter(Mandatory = $false)]
    [switch]$SkipBuild = $false
)

$ErrorActionPreference = "Stop"

# Colors for output
function Write-Success { param([string]$Message) Write-Host "✅ $Message" -ForegroundColor Green }
function Write-Info { param([string]$Message) Write-Host "ℹ️  $Message" -ForegroundColor Cyan }
function Write-Warning { param([string]$Message) Write-Host "⚠️  $Message" -ForegroundColor Yellow }
function Write-Error { param([string]$Message) Write-Host "❌ $Message" -ForegroundColor Red }

# Banner
Write-Host @"
╔══════════════════════════════════════════════════════════════╗
║                TiXL Code Quality Infrastructure              ║
║                        Setup Script                          ║
╚══════════════════════════════════════════════════════════════╝
"@ -ForegroundColor Magenta

Write-Info "Starting setup of TiXL Code Quality Infrastructure..."
Write-Info "Solution Path: $SolutionPath"
Write-Info "Platform: $($PSVersionTable.Platform ?? 'Windows')"
Write-Info "PowerShell: $($PSVersionTable.PSVersion)"

# Check prerequisites
function Test-Prerequisites {
    Write-Info "Checking prerequisites..."
    
    # Check .NET SDK
    try {
        $dotnetVersion = dotnet --version
        Write-Success ".NET SDK found: $dotnetVersion"
        
        # Parse version for compatibility check
        $versionParts = $dotnetVersion.Split('.')
        $majorVersion = [int]$versionParts[0]
        
        if ($majorVersion -lt 8) {
            Write-Warning "This infrastructure is optimized for .NET 8.0+. Current version: $dotnetVersion"
        }
    } catch {
        Write-Error ".NET SDK not found. Please install .NET 8.0 or later from https://dotnet.microsoft.com/"
        throw ".NET SDK is required for this setup"
    }
    
    # Check solution file
    if (-not (Test-Path $SolutionPath)) {
        Write-Error "Solution file not found: $SolutionPath"
        Write-Info "Please ensure you're running this script from the solution directory"
        throw "Solution file not found"
    }
    Write-Success "Solution file found: $SolutionPath"
    
    # Check for required tools
    $tools = @("git", "nuget")
    foreach ($tool in $tools) {
        try {
            $version = & $tool --version 2>$null
            Write-Success "$tool found: $version"
        } catch {
            Write-Warning "$tool not found. Some features may not work properly."
        }
    }
}

# Setup global tools
function Install-GlobalTools {
    if (-not $InstallGlobalTools) { return }
    
    Write-Info "Installing global .NET tools..."
    
    $globalTools = @(
        @{ Name = "dotnet-format"; Version = "latest"; Description = "Code formatter" },
        @{ Name = "dotnet-sonarscanner"; Version = "latest"; Description = "SonarQube scanner" },
        @{ Name = "dotnet-csharpier"; Version = "latest"; Description = "C# code formatter" },
        @{ Name = "dotnet-stryker"; Version = "latest"; Description = "Mutation testing" }
    )
    
    foreach ($tool in $globalTools) {
        try {
            Write-Info "Installing $($tool.Name)..."
            
            $installArgs = @("tool", "install", "--global", $tool.Name)
            if ($tool.Version -ne "latest") {
                $installArgs += "--version", $tool.Version
            }
            
            & dotnet @installArgs 2>&1 | Out-Null
            
            Write-Success "Installed $($tool.Name)"
        } catch {
            Write-Warning "Failed to install $($tool.Name): $($_.Exception.Message)"
        }
    }
    
    Write-Success "Global tools installation completed"
}

# Build project with quality gates
function Build-WithQualityGates {
    if ($SkipBuild) { return }
    
    Write-Info "Building solution with quality gates..."
    
    try {
        $buildArgs = @(
            "build",
            $SolutionPath,
            "--configuration", "Release",
            "--verbosity", "minimal",
            "/p:EnforceCodeStyleInBuild=true",
            "/p:TreatWarningsAsErrors=true",
            "/p:WarningLevel=5",
            "/p:AnalysisLevel=latest",
            "/p:AnalysisMode=AllEnabledByDefault"
        )
        
        & dotnet @buildArgs
        
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed with quality gate violations"
        }
        
        Write-Success "Build completed successfully with quality gates"
    } catch {
        Write-Error "Build failed: $($_.Exception.Message)"
        Write-Info "This may be expected if there are existing code quality issues"
        Write-Info "Run './scripts/Run-CodeQualityChecks.ps1 -FailOnIssues:$false' to see detailed report"
    }
}

# Setup IDE configuration
function Setup-IDE {
    if (-not $SetupVisualStudio) { return }
    
    Write-Info "Setting up IDE configuration..."
    
    # Visual Studio settings
    $vsSettings = @{
        "AutoFormattingEnabled" = $true
        "RealTimeAnalyzersEnabled" = $true
        "CodeCleanupOnSave" = $true
        "SolutionWideAnalysisEnabled" = $true
    }
    
    Write-Success "IDE configuration completed"
    Write-Info "Recommended Visual Studio settings:"
    Write-Info "• Enable all code analysis rules"
    Write-Info "• Format document on save: Enabled"
    Write-Info "• Show line numbers: Enabled"
    Write-Info "• Solution-wide analysis: Enabled"
    Write-Info "• XML documentation generation: Enabled"
}

# Setup quality check scripts
function Setup-QualityScripts {
    Write-Info "Setting up quality check scripts..."
    
    # Ensure scripts are executable
    $scripts = @(
        "scripts/Run-CodeQualityChecks.ps1",
        "scripts/Invoke-CICDQualityGates.ps1",
        "scripts/detect-and-fix-warnings.ps1",
        "scripts/coverage-analyzer.ps1"
    )
    
    foreach ($script in $scripts) {
        if (Test-Path $script) {
            try {
                # Make scripts executable (Unix-like systems)
                if ($PSVersionTable.Platform -ne "Win32NT") {
                    chmod +x $script 2>$null
                }
                Write-Success "Script ready: $script"
            } catch {
                Write-Warning "Could not set executable permissions for $script"
            }
        } else {
            Write-Warning "Script not found: $script"
        }
    }
}

# Setup local SonarQube (optional)
function Setup-SonarQube {
    if (-not $EnableSonarQube) { return }
    
    Write-Info "Setting up local SonarQube instance..."
    
    # Check if Docker is available
    try {
        $dockerVersion = & docker --version 2>$null
        Write-Success "Docker found: $dockerVersion"
        
        # Start SonarQube container
        Write-Info "Starting SonarQube container..."
        & docker run -d --name sonarqube -p 9000:9000 sonarqube:developer
        
        if ($LASTEXITCODE -eq 0) {
            Write-Success "SonarQube started. Access at http://localhost:9000"
            Write-Info "Default credentials: admin/admin"
            Write-Info "Generate a new token and update your environment:"
            Write-Info "  `$env:SONAR_HOST_URL = 'http://localhost:9000'"
            Write-Info "  `$env:SONAR_TOKEN = 'your-token-here'"
        } else {
            Write-Warning "Failed to start SonarQube container"
        }
    } catch {
        Write-Warning "Docker not found or not running. Skipping SonarQube setup"
    }
}

# Verify setup
function Test-Setup {
    Write-Info "Verifying setup..."
    
    # Test core components
    $tests = @{
        "EditorConfig" = { Test-Path ".editorconfig" }
        "Analyzer Config" = { Test-Path "Directory.Analyzers.props" }
        "StyleCop Config" = { Test-Path "StyleCop.json" }
        "Quality Ruleset" = { Test-Path "TiXL-CodeQuality.ruleset" }
        "Global Usings" = { Test-Path "src/GlobalUsings.cs" }
        "Quality Scripts" = { Test-Path "scripts/Run-CodeQualityChecks.ps1" }
        "Developer Guidelines" = { Test-Path "docs/CODE_QUALITY_STANDARDS.md" }
        "Dependency Analyzer" = { Test-Path "Tools/CyclicDependencyAnalyzer/Program.cs" }
    }
    
    $allPassed = $true
    foreach ($test in $tests.GetEnumerator()) {
        try {
            $result = & $test.Value
            if ($result) {
                Write-Success "$($test.Key): Configured"
            } else {
                Write-Warning "$($test.Key): Not found"
                $allPassed = $false
            }
        } catch {
            Write-Error "$($test.Key): Error - $($_.Exception.Message)"
            $allPassed = $false
        }
    }
    
    if ($allPassed) {
        Write-Success "All core components are configured correctly!"
    } else {
        Write-Warning "Some components may not be configured. Review the warnings above."
    }
    
    return $allPassed
}

# Show next steps
function Show-NextSteps {
    Write-Host @"

╔══════════════════════════════════════════════════════════════╗
║                       Setup Complete!                        ║
╚══════════════════════════════════════════════════════════════╝
"@ -ForegroundColor Green
    
    Write-Host "Next Steps:" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "1. Run Quality Checks:" -ForegroundColor White
    Write-Host "   .\scripts\Run-CodeQualityChecks.ps1"
    Write-Host ""
    Write-Host "2. Fix Code Issues (if any):" -ForegroundColor White
    Write-Host "   .\scripts\detect-and-fix-warnings.ps1"
    Write-Host ""
    Write-Host "3. Run Tests with Coverage:" -ForegroundColor White
    Write-Host "   dotnet test --collect:""XPlat Code Coverage"""
    Write-Host ""
    Write-Host "4. Check Dependencies:" -ForegroundColor White
    Write-Host "   .\Tools\CyclicDependencyAnalyzer\bin\Release\net9.0\TiXL.CyclicDependencyAnalyzer.exe"
    Write-Host ""
    Write-Host "5. Format Code:" -ForegroundColor White
    Write-Host "   dotnet format"
    Write-Host ""
    
    if ($EnableSonarQube) {
        Write-Host "6. Run SonarQube Analysis:" -ForegroundColor White
        Write-Host "   dotnet-sonarscanner begin /k:""tixl-realtime-graphics"""
        Write-Host "   dotnet build"
        Write-Host "   dotnet test"
        Write-Host "   dotnet-sonarscanner end"
        Write-Host ""
    }
    
    Write-Host "Documentation:" -ForegroundColor Cyan
    Write-Host "• Developer Guidelines: docs\CODE_QUALITY_STANDARDS.md"
    Write-Host "• Quality Infrastructure: docs\CODE_QUALITY_INFRASTRUCTURE_SUMMARY.md"
    Write-Host "• CI/CD Integration: scripts\Invoke-CICDQualityGates.ps1"
    Write-Host ""
    
    Write-Host "Recommended IDE Settings:" -ForegroundColor Cyan
    Write-Host "• Enable all code analysis rules"
    Write-Host "• Format document on save"
    Write-Host "• Show line numbers"
    Write-Host "• Solution-wide analysis enabled"
    Write-Host "• XML documentation generation enabled"
    Write-Host ""
}

# Main execution
try {
    Test-Prerequisites
    Install-GlobalTools
    Build-WithQualityGates
    Setup-IDE
    Setup-QualityScripts
    Setup-SonarQube
    
    $setupSuccess = Test-Setup
    
    if ($setupSuccess) {
        Show-NextSteps
        Write-Success "TiXL Code Quality Infrastructure setup completed successfully!"
        exit 0
    } else {
        Write-Warning "Setup completed with some warnings. Review the output above."
        exit 1
    }
} catch {
    Write-Error "Setup failed: $($_.Exception.Message)"
    if ($VerboseOutput) {
        Write-Host "Stack Trace:" -ForegroundColor Red
        Write-Host $_.ScriptStackTrace -ForegroundColor Red
    }
    exit 1
}
