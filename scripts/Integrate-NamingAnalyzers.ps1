#!/usr/bin/env pwsh

<#
.SYNOPSIS
TiXL Naming Convention Analyzer Integration Script

.DESCRIPTION
Integrates the TiXL naming convention analyzer into the build process.

.PARAMETER SolutionPath
Path to the TiXL solution file

.PARAMETER Mode
Integration mode: AddPackage, RemovePackage, Configure

.PARAMETER CreateGlobalConfig
Create global analyzer configuration

.EXAMPLE
.\Integrate-NamingAnalyzers.ps1 -SolutionPath "TiXL.sln" -Mode AddPackage

.EXAMPLE
.\Integrate-NamingAnalyzers.ps1 -SolutionPath "TiXL.sln" -Mode Configure -CreateGlobalConfig
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SolutionPath,
    
    [Parameter(Mandatory = $true)]
    [ValidateSet("AddPackage", "RemovePackage", "Configure")]
    [string]$Mode,
    
    [Parameter(Mandatory = $false)]
    [switch]$CreateGlobalConfig = $false,
    
    [Parameter(Mandatory = $false)]
    [switch]$Verbose = $false
)

$ErrorActionPreference = "Stop"

function Add-AnalyzerToProjects {
    param(
        [string]$SolutionPath
    )
    
    Write-Host "Adding naming convention analyzer to projects..." -ForegroundColor Cyan
    
    # Get all C# projects in the solution
    $projects = Get-ChildItem -Path (Split-Path $SolutionPath -Parent) -Recurse -Include "*.csproj"
    
    $analyzerPackage = @{
        Name = "TiXL.NamingConventions.Analyzers"
        Version = "1.0.0"
    }
    
    foreach ($project in $projects) {
        Write-Host "Processing: $($project.Name)" -ForegroundColor Yellow
        
        try {
            # Check if project already has the analyzer
            $projectContent = Get-Content $project.FullName -Raw
            if ($projectContent -match "TiXL\.NamingConventions\.Analyzers") {
                Write-Host "  Analyzer already integrated in $($project.Name)" -ForegroundColor Green
                continue
            }
            
            # Add analyzer package reference
            $xml = [xml](Get-Content $project.FullName)
            
            # Find or create ItemGroup for PackageReference
            $packageReferences = $xml.Project.ItemGroup | Where-Object { $_.PackageReference } | Select-Object -First 1
            
            if (-not $packageReferences) {
                $packageReferences = $xml.CreateElement("ItemGroup")
                $xml.Project.AppendChild($packageReferences) | Out-Null
            }
            
            # Create PackageReference element
            $packageRef = $xml.CreateElement("PackageReference")
            $packageRef.SetAttribute("Include", $analyzerPackage.Name)
            $packageRef.SetAttribute("Version", $analyzerPackage.Version)
            $packageRef.SetAttribute("PrivateAssets", "All")
            $packageReferences.AppendChild($packageRef) | Out-Null
            
            # Save the project file
            $xml.Save($project.FullName)
            
            Write-Host "  ✓ Analyzer added to $($project.Name)" -ForegroundColor Green
            
        } catch {
            Write-Error "  ✗ Failed to process $($project.Name): $_"
        }
    }
    
    Write-Host "Analyzer integration completed." -ForegroundColor Green
}

function Remove-AnalyzerFromProjects {
    param(
        [string]$SolutionPath
    )
    
    Write-Host "Removing naming convention analyzer from projects..." -ForegroundColor Cyan
    
    $projects = Get-ChildItem -Path (Split-Path $SolutionPath -Parent) -Recurse -Include "*.csproj"
    
    foreach ($project in $projects) {
        Write-Host "Processing: $($project.Name)" -ForegroundColor Yellow
        
        try {
            $xml = [xml](Get-Content $project.FullName)
            
            # Find PackageReference elements
            $packageRefs = $xml.Project.ItemGroup | Where-Object { $_.PackageReference }
            $found = $false
            
            foreach ($itemGroup in $packageRefs) {
                foreach ($packageRef in $itemGroup.PackageReference) {
                    if ($packageRef.Include -eq "TiXL.NamingConventions.Analyzers") {
                        $itemGroup.RemoveChild($packageRef) | Out-Null
                        $found = $true
                        Write-Host "  ✓ Analyzer removed from $($project.Name)" -ForegroundColor Green
                    }
                }
            }
            
            if ($found) {
                $xml.Save($project.FullName)
            } else {
                Write-Host "  Analyzer not found in $($project.Name)" -ForegroundColor Gray
            }
            
        } catch {
            Write-Error "  ✗ Failed to process $($project.Name): $_"
        }
    }
    
    Write-Host "Analyzer removal completed." -ForegroundColor Green
}

function Set-EditorConfigSettings {
    param(
        [string]$SolutionPath
    )
    
    Write-Host "Configuring EditorConfig settings..." -ForegroundColor Cyan
    
    $solutionDir = Split-Path $SolutionPath -Parent
    $editorConfigPath = Join-Path $solutionDir ".editorconfig"
    $namingConfigPath = Join-Path $solutionDir ".editorconfig.naming"
    
    # Copy naming configuration to main .editorconfig if not exists
    if ((Test-Path $namingConfigPath) -and (Test-Path $editorConfigPath)) {
        Write-Host "Found naming configuration file. Appending to main .editorconfig..." -ForegroundColor Yellow
        
        $mainConfig = Get-Content $editorConfigPath -Raw
        $namingConfig = Get-Content $namingConfigPath -Raw
        
        # Check if naming config is already included
        if ($mainConfig -match "# TiXL Naming Conventions") {
            Write-Host "Naming configuration already included in .editorconfig" -ForegroundColor Gray
        } else {
            $updatedConfig = $mainConfig + "`n`n" + $namingConfig
            Set-Content -Path $editorConfigPath -Value $updatedConfig
            Write-Host "✓ Naming configuration added to .editorconfig" -ForegroundColor Green
        }
    } elseif (Test-Path $namingConfigPath) {
        Write-Host "Creating comprehensive .editorconfig..." -ForegroundColor Yellow
        
        $namingConfig = Get-Content $namingConfigPath -Raw
        Set-Content -Path $editorConfigPath -Value $namingConfig
        Write-Host "✓ New .editorconfig created with naming conventions" -ForegroundColor Green
    } else {
        Write-Warning "Naming configuration file not found: $namingConfigPath"
    }
}

function New-GlobalAnalyzerConfig {
    param(
        [string]$SolutionPath
    )
    
    Write-Host "Creating global analyzer configuration..." -ForegroundColor Cyan
    
    $solutionDir = Split-Path $SolutionPath -Parent
    $globalConfigPath = Join-Path $solutionDir "Directory.Build.props"
    
    if (-not (Test-Path $globalConfigPath)) {
        Write-Error "Directory.Build.props not found. Cannot create global configuration."
        return $false
    }
    
    # Read existing configuration
    $config = Get-Content $globalConfigPath -Raw
    
    # Check if analyzer configuration already exists
    if ($config -match "TiXL.*Naming.*Conventions") {
        Write-Host "Global analyzer configuration already exists" -ForegroundColor Gray
        return $true
    }
    
    # Add analyzer configuration
    $analyzerConfig = @"

  <!-- TiXL Naming Convention Analyzers Configuration -->
  <PropertyGroup>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <AnalysisLevel>latest</AnalysisLevel>
    <AnalysisMode>AllEnabledByDefault</AnalysisMode>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    
    <!-- TiXL Naming Convention specific settings -->
    <WarningLevel>5</WarningLevel>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    
    <!-- Disable specific warnings if needed -->
    <!-- <NoWarn>$(NoWarn);TIXL012</NoWarn> -->
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="TiXL.NamingConventions.Analyzers" Version="1.0.0" PrivateAssets="All" />
  </ItemGroup>
"@
    
    $updatedConfig = $config + $analyzerConfig
    Set-Content -Path $globalConfigPath -Value $updatedConfig
    
    Write-Host "✓ Global analyzer configuration added to Directory.Build.props" -ForegroundColor Green
    return $true
}

function Test-BuildIntegration {
    param(
        [string]$SolutionPath
    )
    
    Write-Host "Testing build integration..." -ForegroundColor Cyan
    
    $solutionDir = Split-Path $SolutionPath -Parent
    $testProject = Get-ChildItem -Path $solutionDir -Recurse -Include "*Tests*.csproj" | Select-Object -First 1
    
    if ($testProject) {
        Write-Host "Running test build on $($testProject.Name)..." -ForegroundColor Yellow
        
        Push-Location (Split-Path $testProject.FullName)
        try {
            $result = dotnet build --no-restore 2>&1
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✓ Build integration test passed" -ForegroundColor Green
            } else {
                Write-Warning "Build integration test may have issues:"
                Write-Host $result
            }
        }
        catch {
            Write-Error "Build integration test failed: $_"
        }
        finally {
            Pop-Location
        }
    } else {
        Write-Host "No test project found for integration test" -ForegroundColor Gray
    }
}

# Main execution
try {
    Write-Host "`nTiXL Naming Convention Analyzer Integration" -ForegroundColor Magenta
    Write-Host "Mode: $Mode" -ForegroundColor White
    Write-Host "Solution: $SolutionPath" -ForegroundColor White
    Write-Host ""
    
    switch ($Mode) {
        "AddPackage" {
            Add-AnalyzerToProjects -SolutionPath $SolutionPath
            if ($CreateGlobalConfig) {
                New-GlobalAnalyzerConfig -SolutionPath $SolutionPath
            }
        }
        "RemovePackage" {
            Remove-AnalyzerFromProjects -SolutionPath $SolutionPath
        }
        "Configure" {
            Set-EditorConfigSettings -SolutionPath $SolutionPath
            if ($CreateGlobalConfig) {
                New-GlobalAnalyzerConfig -SolutionPath $SolutionPath
            }
        }
    }
    
    # Test integration if requested
    if ($Mode -in @("AddPackage", "Configure")) {
        Test-BuildIntegration -SolutionPath $SolutionPath
    }
    
    Write-Host "`nIntegration completed successfully." -ForegroundColor Green
    Write-Host "`nNext steps:" -ForegroundColor Yellow
    Write-Host "1. Build the solution to verify analyzer integration"
    Write-Host "2. Run naming convention analysis on your codebase"
    Write-Host "3. Fix any reported violations"
    Write-Host "4. Configure CI/CD to run analyzer checks"
    
    exit 0
}
catch {
    Write-Error "Integration failed: $_"
    Write-Host $_.ScriptStackTrace
    exit 1
}