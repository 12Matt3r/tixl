# SonarQube Setup Script for TiXL
# Automates the initial setup and configuration

param(
    [Parameter(Mandatory=$false)]
    [string]$SonarUrl = "http://localhost:9000",
    
    [Parameter(Mandatory=$false)]
    [string]$ProjectKey = "tixl-realtime-graphics",
    
    [Parameter(Mandatory=$false)]
    [string]$ProjectName = "TiXL Real-time Graphics",
    
    [Parameter(Mandatory=$false)]
    [string]$Organization = "tixl3d",
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipDocker
)

$ErrorActionPreference = "Stop"

function Test-DockerAvailability {
    try {
        $null = docker --version
        return $true
    } catch {
        Write-Host "Docker is not available. Please install Docker first." -ForegroundColor Red
        return $false
    }
}

function Start-SonarQubeServer {
    Write-Host "Starting SonarQube server..." -ForegroundColor Green
    
    if (-not $SkipDocker) {
        # Check if SonarQube is already running
        $running = docker ps --filter "name=tixl-sonarqube" --format "table {{.Names}}" | Select-String "tixl-sonarqube"
        
        if ($running) {
            Write-Host "SonarQube container is already running" -ForegroundColor Yellow
        } else {
            # Start containers
            docker-compose -f docker-compose.sonar.yml up -d
            
            Write-Host "Waiting for SonarQube to start..." -ForegroundColor Yellow
            Start-Sleep -Seconds 30
            
            # Wait for SonarQube to be ready
            $maxAttempts = 20
            for ($i = 1; $i -le $maxAttempts; $i++) {
                try {
                    $response = Invoke-RestMethod -Uri "$SonarUrl/api/system/health" -TimeoutSec 10
                    if ($response.health -eq "GREEN") {
                        Write-Host "SonarQube is ready!" -ForegroundColor Green
                        break
                    }
                } catch {
                    Write-Host "Waiting for SonarQube... (attempt $i/$maxAttempts)" -ForegroundColor Yellow
                    Start-Sleep -Seconds 15
                }
            }
        }
    }
}

function Initialize-SonarQube {
    Write-Host "Initializing SonarQube configuration..." -ForegroundColor Green
    
    # Wait for SonarQube to be fully ready
    Write-Host "Checking SonarQube availability..." -ForegroundColor Yellow
    $maxAttempts = 30
    for ($i = 1; $i -le $maxAttempts; $i++) {
        try {
            $response = Invoke-RestMethod -Uri "$SonarUrl/api/system/status" -TimeoutSec 10
            if ($response.status -eq "UP") {
                Write-Host "SonarQube is available!" -ForegroundColor Green
                break
            }
        } catch {
            Write-Host "SonarQube not ready yet... (attempt $i/$maxAttempts)" -ForegroundColor Yellow
            Start-Sleep -Seconds 10
        }
        
        if ($i -eq $maxAttempts) {
            throw "SonarQube failed to start within expected time"
        }
    }
    
    # Generate authentication token
    Write-Host "Setting up authentication..." -ForegroundColor Yellow
    $token = & dotnet tool run sonarscanner begin /k:"$ProjectKey" /n:"$ProjectName" /v:"4.1.0" /d:sonar.host.url="$SonarUrl" 2>&1 | Where-Object { $_ -match 'sonar\.login=([a-zA-Z0-9]+)' } | ForEach-Object { $matches[1] }
    
    if (-not $token) {
        Write-Host "Token generation failed. Attempting manual setup..." -ForegroundColor Yellow
        
        # Create token via API (admin/admin credentials)
        $auth = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("admin:admin"))
        $headers = @{ Authorization = "Basic $auth" }
        $body = @{ name = "TiXL Analysis Token" } | ConvertTo-Json
        
        $tokenResponse = Invoke-RestMethod -Uri "$SonarUrl/api/user_tokens/generate" -Method POST -Headers $headers -Body $body -ContentType "application/json"
        $token = $tokenResponse.token
    }
    
    Write-Host "SonarQube token: $token" -ForegroundColor Green
    Write-Host "Save this token for future use in your CI/CD pipeline" -ForegroundColor Yellow
    
    return $token
}

function Create-Project {
    param([string]$Token, [string]$SonarUrl)
    
    Write-Host "Creating SonarQube project..." -ForegroundColor Green
    
    # Check if project already exists
    try {
        $headers = @{ Authorization = "token $Token" }
        $existing = Invoke-RestMethod -Uri "$SonarUrl/api/projects/search?projects=$ProjectKey" -Headers $headers
        
        if ($existing.components.Count -gt 0) {
            Write-Host "Project $ProjectKey already exists" -ForegroundColor Yellow
            return
        }
    } catch {
        Write-Host "Project check failed, proceeding with creation..." -ForegroundColor Yellow
    }
    
    # Create project
    try {
        $headers = @{ Authorization = "token $Token" }
        $body = @{
            project = $ProjectKey
            name = $ProjectName
            visibility = "public"
        } | ConvertTo-Json
        
        Invoke-RestMethod -Uri "$SonarUrl/api/projects/create" -Method POST -Headers $headers -Body $body -ContentType "application/json"
        Write-Host "Project created successfully" -ForegroundColor Green
    } catch {
        Write-Host "Project creation failed: $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Configure-QualityProfile {
    param([string]$Token, [string]$SonarUrl)
    
    Write-Host "Configuring quality profile..." -ForegroundColor Green
    
    $headers = @{ Authorization = "token $Token" }
    
    # Get C# quality profiles
    $profiles = Invoke-RestMethod -Uri "$SonarUrl/api/qualityprofiles/search?language=cs" -Headers $headers
    
    if ($profiles.profiles.Count -eq 0) {
        Write-Host "No C# quality profiles found" -ForegroundColor Red
        return
    }
    
    # Use the recommended profile
    $recommendedProfile = $profiles.profiles | Where-Object { $_.isDefault } | Select-Object -First 1
    
    if (-not $recommendedProfile) {
        $recommendedProfile = $profiles.profiles[0]
    }
    
    Write-Host "Using quality profile: $($recommendedProfile.name)" -ForegroundColor Green
    
    # Activate custom rules if sonar-rules.xml exists
    $rulesFile = "sonar-rules.xml"
    if (Test-Path $rulesFile) {
        Write-Host "Loading custom rules from $rulesFile" -ForegroundColor Yellow
        
        # Note: This is a simplified approach. In practice, you would need to
        # parse the XML and activate each rule individually via API
        Write-Host "Custom rules configuration would be applied here" -ForegroundColor Yellow
    }
    
    # Set as default profile for the project
    try {
        $body = @{
            projectKey = $ProjectKey
            profile = $recommendedProfile.key
        } | ConvertTo-Json
        
        Invoke-RestMethod -Uri "$SonarUrl/api/qualityprofiles/add_project" -Method POST -Headers $headers -Body $body -ContentType "application/json"
        Write-Host "Quality profile assigned to project" -ForegroundColor Green
    } catch {
        Write-Host "Failed to assign quality profile: $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Setup-ProjectPermissions {
    param([string]$Token, [string]$SonarUrl)
    
    Write-Host "Setting up project permissions..." -ForegroundColor Green
    
    $headers = @{ Authorization = "token $Token" }
    
    # Grant all permissions to "Anyone" (for public projects)
    $permissions = @("scan", "admin", "user", "issueadmin", "dashboardadmin")
    
    foreach ($permission in $permissions) {
        try {
            $body = @{
                projectKey = $ProjectKey
                login = "anyone"
                permission = $permission
            } | ConvertTo-Json
            
            Invoke-RestMethod -Uri "$SonarUrl/api/permissions/add_project_member" -Method POST -Headers $headers -Body $body -ContentType "application/json"
        } catch {
            Write-Host "Failed to grant $permission permission: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
    
    Write-Host "Project permissions configured" -ForegroundColor Green
}

function Install-RequiredTools {
    Write-Host "Checking and installing required tools..." -ForegroundColor Green
    
    # Check SonarScanner
    try {
        & dotnet sonarscanner --version | Out-Null
        Write-Host "SonarScanner is already installed" -ForegroundColor Green
    } catch {
        Write-Host "Installing SonarScanner..." -ForegroundColor Yellow
        dotnet tool install --global dotnet-sonarscanner
    }
    
    # Check other tools
    $tools = @(
        @{ Name = "JB Command Line Tools"; Command = "jb --version"; InstallCommand = "dotnet tool install --global jb.blt" },
        @{ Name = "Metrics to JSON"; Command = "metrics-to-json --version"; InstallCommand = "dotnet tool install --global metrics-to-json" }
    )
    
    foreach ($tool in $tools) {
        try {
            & $tool.Command | Out-Null
            Write-Host "$($tool.Name) is already installed" -ForegroundColor Green
        } catch {
            Write-Host "Installing $($tool.Name)..." -ForegroundColor Yellow
            try {
                Invoke-Expression $tool.InstallCommand
                Write-Host "$($tool.Name) installed successfully" -ForegroundColor Green
            } catch {
                Write-Host "Failed to install $($tool.Name): $($_.Exception.Message)" -ForegroundColor Red
            }
        }
    }
}

function Show-SetupInstructions {
    Write-Host "`n" -NoNewline
    Write-Host "====================================================" -ForegroundColor Cyan
    Write-Host "           TiXL SonarQube Setup Complete!" -ForegroundColor Green
    Write-Host "====================================================" -ForegroundColor Cyan
    Write-Host "`nSonarQube Dashboard:" -ForegroundColor White
    Write-Host "  URL: $SonarUrl" -ForegroundColor Yellow
    Write-Host "  Username: admin" -ForegroundColor Yellow
    Write-Host "  Password: admin" -ForegroundColor Yellow
    Write-Host "`nProject Information:" -ForegroundColor White
    Write-Host "  Project Key: $ProjectKey" -ForegroundColor Yellow
    Write-Host "  Project Name: $ProjectName" -ForegroundColor Yellow
    Write-Host "`nNext Steps:" -ForegroundColor White
    Write-Host "1. Change the default admin password" -ForegroundColor Yellow
    Write-Host "2. Configure LDAP/SAML authentication (optional)" -ForegroundColor Yellow
    Write-Host "3. Set up quality gates and notifications" -ForegroundColor Yellow
    Write-Host "4. Run your first analysis using:" -ForegroundColor Yellow
    Write-Host "   dotnet sonarscanner begin /k:""$ProjectKey"" /d:sonar.host.url=""$SonarUrl"" /d:sonar.login=""YOUR_TOKEN""" -ForegroundColor Gray
    Write-Host "`nQuality Gate Configuration:" -ForegroundColor White
    Write-Host "1. Go to Quality Gates in SonarQube dashboard" -ForegroundColor Yellow
    Write-Host "2. Create a new quality gate or use 'Sonar way'" -ForegroundColor Yellow
    Write-Host "3. Set thresholds (see quality-gates-config.json)" -ForegroundColor Yellow
    Write-Host "4. Associate the quality gate with your project" -ForegroundColor Yellow
    Write-Host "`nCI/CD Integration:" -ForegroundColor White
    Write-Host "1. Add SONAR_HOST_URL and SONAR_TOKEN to pipeline variables" -ForegroundColor Yellow
    Write-Host "2. Use the enhanced azure-pipelines.yml configuration" -ForegroundColor Yellow
    Write-Host "3. Configure quality gate checks to fail builds" -ForegroundColor Yellow
    Write-Host "`nFor detailed setup instructions, see:" -ForegroundColor White
    Write-Host "  docs/code_quality_tools_setup.md" -ForegroundColor Cyan
}

# Main execution
try {
    Write-Host "🚀 TiXL SonarQube Setup Script" -ForegroundColor Green
    Write-Host "================================" -ForegroundColor Gray
    
    # Check prerequisites
    if (-not $SkipDocker) {
        if (-not (Test-DockerAvailability)) {
            exit 1
        }
    }
    
    # Install required tools
    Install-RequiredTools
    
    # Start SonarQube server
    if (-not $SkipDocker) {
        Start-SonarQubeServer
    }
    
    # Initialize SonarQube
    $token = Initialize-SonarQube
    
    # Create project
    Create-Project -Token $token -SonarUrl $SonarUrl
    
    # Configure quality profile
    Configure-QualityProfile -Token $token -SonarUrl $SonarUrl
    
    # Setup permissions
    Setup-ProjectPermissions -Token $token -SonarUrl $SonarUrl
    
    # Show instructions
    Show-SetupInstructions
    
    # Save configuration
    $config = @{
        sonarHostUrl = $SonarUrl
        projectKey = $ProjectKey
        projectName = $ProjectName
        organization = $Organization
        token = $token
        timestamp = (Get-Date).ToUniversalTime()
    }
    
    $config | ConvertTo-Json -Depth 5 | Out-File -FilePath "sonar-setup-config.json" -Encoding UTF8
    Write-Host "`nConfiguration saved to: sonar-setup-config.json" -ForegroundColor Green
    
} catch {
    Write-Host "Setup failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
