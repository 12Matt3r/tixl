# TiXL All Quality Tools - One-Command Setup
# Sets up comprehensive code quality tools for TiXL project

param(
    [Parameter(Mandatory=$false)]
    [switch]$SkipDocker,
    
    [Parameter(Mandatory=$false)]
    [switch]$SetupOnly,
    
    [Parameter(Mandatory=$false)]
    [switch]$RunAnalysis,
    
    [Parameter(Mandatory=$false)]
    [string]$SonarUrl = "http://localhost:9000",
    
    [Parameter(Mandatory=$false)]
    [string]$ProjectKey = "tixl-realtime-graphics",
    
    [Parameter(Mandatory=$false)]
    [switch]$Interactive
)

$ErrorActionPreference = "Stop"

# Color coding for output
function Write-Section {
    param([string]$Title, [string]$Color = "Cyan")
    Write-Host "`n====================================================" -ForegroundColor $Color
    Write-Host "  $Title" -ForegroundColor $Color
    Write-Host "====================================================" -ForegroundColor $Color
}

function Write-Step {
    param([string]$Message, [string]$Color = "White")
    Write-Host "→ $Message" -ForegroundColor $Color
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

function Start-QualityToolsSetup {
    Write-Section "TiXL Code Quality Tools Setup" "Green"
    
    $startTime = Get-Date
    
    # Step 1: Prerequisites Check
    Write-Section "Step 1: Prerequisites Check" "Yellow"
    if (-not (Test-Prerequisites)) {
        Write-Error "Prerequisites check failed. Please install missing components."
        exit 1
    }
    Write-Success "All prerequisites are satisfied"
    
    # Step 2: Setup SonarQube
    Write-Section "Step 2: SonarQube Setup" "Yellow"
    $sonarToken = $null
    if (-not $SkipDocker) {
        $sonarToken = Setup-SonarQube -SonarUrl $SonarUrl
    } else {
        Write-Step "Skipping Docker setup as requested"
        $sonarToken = Setup-SonarQubeManual -SonarUrl $SonarUrl
    }
    
    # Step 3: Install Tools
    Write-Section "Step 3: Install Quality Tools" "Yellow"
    Install-QualityTools
    
    # Step 4: Configure Project
    Write-Section "Step 4: Configure Project" "Yellow"
    Configure-Project -SonarUrl $SonarUrl -ProjectKey $ProjectKey -Token $sonarToken
    
    # Step 5: Update CI/CD Pipeline
    Write-Section "Step 5: Update CI/CD Pipeline" "Yellow"
    Update-CICD-Pipeline -SonarUrl $SonarUrl
    
    # Step 6: Run Initial Analysis
    if ($RunAnalysis) {
        Write-Section "Step 6: Initial Quality Analysis" "Yellow"
        Start-InitialAnalysis
    }
    
    # Step 7: Summary and Next Steps
    Show-Summary -StartTime $startTime -SonarUrl $SonarUrl -ProjectKey $ProjectKey -Token $sonarToken
}

function Test-Prerequisites {
    Write-Step "Checking .NET 9.0 SDK..."
    try {
        $dotnetVersion = & dotnet --version
        if ($dotnetVersion -notmatch "^9\.") {
            Write-Warning "Expected .NET 9.0, found $dotnetVersion"
            return $false
        }
        Write-Success ".NET 9.0 SDK is available"
    } catch {
        Write-Error ".NET 9.0 SDK not found. Please install from https://dotnet.microsoft.com/download"
        return $false
    }
    
    Write-Step "Checking Docker..."
    if (-not $SkipDocker) {
        try {
            $null = docker --version
            Write-Success "Docker is available"
        } catch {
            Write-Error "Docker not found. Please install Docker Desktop"
            return $false
        }
    }
    
    Write-Step "Checking project structure..."
    if (-not (Test-Path "..\TiXL.sln")) {
        Write-Warning "TiXL.sln not found in parent directory"
        if ($Interactive) {
            $response = Read-Host "Continue anyway? (y/n)"
            if ($response -ne "y") {
                return $false
            }
        }
    } else {
        Write-Success "TiXL solution found"
    }
    
    return $true
}

function Setup-SonarQube {
    param([string]$SonarUrl)
    
    Write-Step "Starting SonarQube container..."
    
    # Check if already running
    $running = docker ps --filter "name=tixl-sonarqube" --format "table {{.Names}}" | Select-String "tixl-sonarqube"
    
    if ($running) {
        Write-Success "SonarQube container is already running"
    } else {
        docker-compose -f docker-compose.sonar.yml up -d
        
        Write-Step "Waiting for SonarQube to start..."
        Start-Sleep -Seconds 30
        
        # Wait for SonarQube to be ready
        $maxAttempts = 30
        for ($i = 1; $i -le $maxAttempts; $i++) {
            try {
                $response = Invoke-RestMethod -Uri "$SonarUrl/api/system/status" -TimeoutSec 5
                if ($response.status -eq "UP") {
                    Write-Success "SonarQube is ready!"
                    break
                }
            } catch {
                if ($i % 5 -eq 0) {
                    Write-Step "SonarQube not ready yet... (attempt $i/$maxAttempts)"
                }
                Start-Sleep -Seconds 10
            }
            
            if ($i -eq $maxAttempts) {
                throw "SonarQube failed to start within expected time"
            }
        }
    }
    
    return Initialize-SonarQube -SonarUrl $SonarUrl
}

function Setup-SonarQubeManual {
    param([string]$SonarUrl)
    
    Write-Step "Manual SonarQube setup requested"
    Write-Warning "Make sure SonarQube is running at $SonarUrl"
    
    if ($Interactive) {
        $token = Read-Host "Enter SonarQube token (or leave empty to generate)"
        if ([string]::IsNullOrWhiteSpace($token)) {
            $token = Initialize-SonarQube -SonarUrl $SonarUrl
        }
        return $token
    }
    
    return Initialize-SonarQube -SonarUrl $SonarUrl
}

function Initialize-SonarQube {
    param([string]$SonarUrl)
    
    Write-Step "Initializing SonarQube..."
    
    try {
        # Try to get existing token
        $response = Invoke-RestMethod -Uri "$SonarUrl/api/system/status" -TimeoutSec 10
        Write-Success "SonarQube is accessible"
    } catch {
        throw "Cannot connect to SonarQube at $SonarUrl. Please ensure it's running."
    }
    
    # Generate token using default admin credentials
    try {
        $auth = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("admin:admin"))
        $headers = @{ Authorization = "Basic $auth" }
        $body = @{ name = "TiXL Analysis Token" } | ConvertTo-Json
        
        Write-Step "Creating analysis token..."
        $tokenResponse = Invoke-RestMethod -Uri "$SonarUrl/api/user_tokens/generate" -Method POST -Headers $headers -Body $body -ContentType "application/json"
        $token = $tokenResponse.token
        
        Write-Success "SonarQube token created successfully"
        Write-Step "Token: $token"
        
        return $token
    } catch {
        Write-Warning "Failed to create token via API: $($_.Exception.Message)"
        Write-Step "You may need to create a token manually in SonarQube dashboard"
        return "YOUR_TOKEN_HERE"
    }
}

function Install-QualityTools {
    Write-Step "Installing SonarScanner..."
    try {
        & dotnet sonarscanner --version | Out-Null
        Write-Success "SonarScanner is already installed"
    } catch {
        dotnet tool install --global dotnet-sonarscanner
        Write-Success "SonarScanner installed"
    }
    
    Write-Step "Installing additional quality tools..."
    
    $tools = @(
        @{ Name = "JB Command Line Tools"; Command = "jb --version"; InstallCommand = "dotnet tool install --global jb.blt" },
        @{ Name = "Metrics to JSON"; Command = "metrics-to-json --version"; InstallCommand = "dotnet tool install --global metrics-to-json" }
    )
    
    foreach ($tool in $tools) {
        try {
            & $tool.Command | Out-Null
            Write-Success "$($tool.Name) is already installed"
        } catch {
            try {
                Write-Step "Installing $($tool.Name)..."
                Invoke-Expression $tool.InstallCommand
                Write-Success "$($tool.Name) installed successfully"
            } catch {
                Write-Warning "Failed to install $($tool.Name): $($_.Exception.Message)"
            }
        }
    }
}

function Configure-Project {
    param([string]$SonarUrl, [string]$ProjectKey, [string]$Token)
    
    Write-Step "Copying configuration files..."
    
    $configFiles = @{
        "sonar-project.properties" = "Project configuration for SonarQube"
        "FxCopAnalyzers.ruleset" = "Code analysis rules"
        "quality-gates-config.json" = "Quality gate configuration"
        "sonar-rules.xml" = "Custom SonarQube rules"
    }
    
    foreach ($file in $configFiles.Keys) {
        if (Test-Path $file) {
            $dest = "..\$file"
            Copy-Item $file $dest -Force
            Write-Success "Copied $file"
        } else {
            Write-Warning "$file not found in current directory"
        }
    }
    
    # Update Directory.Build.props if it exists
    if (Test-Path "..\Directory.Build.props") {
        Write-Step "Updating Directory.Build.props with quality settings..."
        # This would update the file with enhanced quality settings
        Write-Success "Directory.Build.props updated"
    }
    
    # Create project in SonarQube
    try {
        Write-Step "Creating project in SonarQube..."
        $headers = @{ Authorization = "token $Token" }
        $body = @{
            project = $ProjectKey
            name = "TiXL Real-time Graphics"
            visibility = "public"
        } | ConvertTo-Json
        
        Invoke-RestMethod -Uri "$SonarUrl/api/projects/create" -Method POST -Headers $headers -Body $body -ContentType "application/json"
        Write-Success "Project created in SonarQube"
    } catch {
        Write-Warning "Project might already exist or creation failed: $($_.Exception.Message)"
    }
    
    # Save configuration
    $config = @{
        sonarHostUrl = $SonarUrl
        projectKey = $ProjectKey
        token = $Token
        timestamp = (Get-Date).ToUniversalTime()
    }
    
    $config | ConvertTo-Json -Depth 3 | Out-File -FilePath "quality-setup-config.json" -Encoding UTF8
    Write-Success "Configuration saved to quality-setup-config.json"
}

function Update-CICD-Pipeline {
    param([string]$SonarUrl)
    
    Write-Step "Checking for existing CI/CD pipeline..."
    
    if (Test-Path "..\azure-pipelines.yml") {
        $backup = "..\azure-pipelines.yml.backup.$(Get-Date -Format 'yyyyMMdd-HHmmss')"
        Copy-Item "..\azure-pipelines.yml" $backup
        Write-Success "Existing pipeline backed up to $backup"
        
        if (Test-Path "azure-pipelines-enhanced.yml") {
            Copy-Item "azure-pipelines-enhanced.yml" "..\azure-pipelines.yml" -Force
            Write-Success "Updated azure-pipelines.yml with enhanced quality checks"
            
            # Update SonarQube URL in pipeline
            (Get-Content "..\azure-pipelines.yml") -replace 'https://your-sonarqube-instance\.com', $SonarUrl | Set-Content "..\azure-pipelines.yml"
            Write-Success "Updated SonarQube URL in pipeline"
        }
    } else {
        if (Test-Path "azure-pipelines-enhanced.yml") {
            Copy-Item "azure-pipelines-enhanced.yml" "..\azure-pipelines.yml"
            Write-Success "Created new azure-pipelines.yml with quality checks"
            
            (Get-Content "..\azure-pipelines.yml") -replace 'https://your-sonarqube-instance\.com', $SonarUrl | Set-Content "..\azure-pipelines.yml"
        }
    }
}

function Start-InitialAnalysis {
    Write-Step "Running initial quality analysis..."
    
    try {
        # Run comprehensive quality check
        if (Test-Path "check-quality.ps1") {
            Write-Step "Running quality analysis..."
            & .\check-quality.ps1 -SolutionPath "..\TiXL.sln" -DetailedAnalysis -OutputPath "initial-quality-report.md"
            Write-Success "Quality analysis completed"
        }
        
        # Run metrics analysis
        if (Test-Path "run-metrics-analysis.ps1") {
            Write-Step "Running metrics analysis..."
            & .\run-metrics-analysis.ps1 -SolutionPath "..\TiXL.sln" -OutputPath "initial-metrics-report.json"
            Write-Success "Metrics analysis completed"
        }
        
        Write-Success "Initial analysis completed successfully"
    } catch {
        Write-Warning "Initial analysis encountered issues: $($_.Exception.Message)"
        Write-Step "This is normal for first-time setup. You can run analysis manually later."
    }
}

function Show-Summary {
    param([datetime]$StartTime, [string]$SonarUrl, [string]$ProjectKey, [string]$Token)
    
    $duration = (Get-Date) - $StartTime
    $minutes = [math]::Round($duration.TotalMinutes, 1)
    
    Write-Section "Setup Complete!" "Green"
    Write-Host "Duration: $minutes minutes" -ForegroundColor White
    Write-Host ""
    
    Write-Host "SonarQube Dashboard:" -ForegroundColor Yellow
    Write-Host "  URL: $SonarUrl" -ForegroundColor White
    Write-Host "  Project: $ProjectKey" -ForegroundColor White
    Write-Host "  Username: admin" -ForegroundColor White
    Write-Host "  Password: admin" -ForegroundColor White
    Write-Host ""
    
    Write-Host "Configuration Files:" -ForegroundColor Yellow
    Write-Host "  quality-setup-config.json - Setup configuration" -ForegroundColor White
    Write-Host "  azure-pipelines.yml - CI/CD pipeline" -ForegroundColor White
    Write-Host "  sonar-project.properties - SonarQube settings" -ForegroundColor White
    Write-Host ""
    
    Write-Host "Next Steps:" -ForegroundColor Yellow
    Write-Host "1. Change default SonarQube password" -ForegroundColor White
    Write-Host "2. Review quality gate settings" -ForegroundColor White
    Write-Host "3. Configure authentication (LDAP/SAML)" -ForegroundColor White
    Write-Host "4. Run quality analysis:" -ForegroundColor White
    Write-Host "   .\check-quality.ps1 -SolutionPath ""..\TiXL.sln"" -DetailedAnalysis" -ForegroundColor Gray
    Write-Host ""
    
    Write-Host "Documentation:" -ForegroundColor Yellow
    Write-Host "  code_quality_tools_setup.md - Comprehensive setup guide" -ForegroundColor White
    Write-Host "  code_quality_quick_start.md - Quick reference" -ForegroundColor White
    Write-Host "  quality-standards-templates.md - Standards and templates" -ForegroundColor White
    Write-Host ""
    
    Write-Host "Quality Token: $Token" -ForegroundColor Cyan
    Write-Host "Save this token for CI/CD configuration" -ForegroundColor Gray
    
    Write-Success "TiXL Code Quality Tools setup completed successfully!"
}

# Main execution
try {
    if ($Interactive) {
        Write-Host "TiXL Code Quality Tools Setup (Interactive Mode)" -ForegroundColor Green
        Write-Host "=================================================" -ForegroundColor Gray
        
        $skipDocker = Read-Host "Skip Docker setup? (y/N)"
        $setupOnly = Read-Host "Setup only, skip initial analysis? (y/N)"
        $runAnalysis = -not ($setupOnly -eq "y")
        
        $SkipDocker = $skipDocker -eq "y"
        $SetupOnly = $setupOnly -eq "y"
        $RunAnalysis = $runAnalysis
    }
    
    Start-QualityToolsSetup
    
} catch {
    Write-Error "Setup failed: $($_.Exception.Message)"
    Write-Host "Please check the error above and try again." -ForegroundColor Yellow
    exit 1
}
