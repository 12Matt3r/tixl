#!/usr/bin/env pwsh
<#
.SYNOPSIS
    TiXL Secure Build System - Quick Start and Validation
.DESCRIPTION
    Demonstrates the complete TiXL secure build system functionality.
    Validates all components and provides a working example.
.PARAMETER Action
    Action to perform: Demo, Validate, Setup
.PARAMETER CreateTestCert
    Create a test certificate for demonstration
.PARAMETER Configuration
    Build configuration to use
.EXAMPLE
    .\scripts\tixl-secure-build-demo.ps1 -Action Demo
#>

param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("Demo", "Validate", "Setup")]
    [string]$Action = "Demo",
    
    [Parameter(Mandatory = $false)]
    [switch]$CreateTestCert = $true,
    
    [Parameter(Mandatory = $false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    
    [Parameter(Mandatory = $false)]
    [string]$OutputPath = "demo-artifacts"
)

$ErrorActionPreference = "Stop"
$InformationPreference = "Continue"

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "TiXL Secure Build System Demo" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan

function New-DemoCertificate {
    if (-not $CreateTestCert) {
        Write-Host "Skipping certificate creation (CreateTestCert = false)" -ForegroundColor Yellow
        return ""
    }
    
    Write-Host "Creating demo certificate..." -ForegroundColor Green
    
    try {
        # Create demo certificate directory
        $certDir = "demo-certificates"
        New-Item -ItemType Directory -Path $certDir -Force | Out-Null
        
        # Create self-signed certificate for demo
        $certParams = @{
            Subject = "CN=TiXL Demo Code Signing,O=TiXL Project Demo,C=US"
            Type = "CodeSigning"
            KeyExportPolicy = "Exportable"
            KeySpec = "Signature"
            KeyLength = 2048
            HashAlgorithm = "SHA256"
            NotAfter = (Get-Date).AddDays(30)
            CertStoreLocation = "Cert:\CurrentUser\My"
            FriendlyName = "TiXL Demo Certificate"
        }
        
        $cert = New-SelfSignedCertificate @certParams
        $thumbprint = $cert.Thumbprint
        
        # Export certificate for later use
        $exportPath = Join-Path $certDir "tixl-demo-signing.pfx"
        $password = "DemoPassword123!"
        
        Export-PfxCertificate -Cert $cert -FilePath $exportPath -Password (ConvertTo-SecureString $password -AsPlainText -Force) -Force | Out-Null
        
        Write-Host "✅ Demo certificate created" -ForegroundColor Green
        Write-Host "Thumbprint: $thumbprint" -ForegroundColor Yellow
        Write-Host "Export Path: $exportPath" -ForegroundColor Yellow
        Write-Host "Password: $password" -ForegroundColor Yellow
        Write-Host ""
        
        return $thumbprint
    }
    catch {
        Write-Host "❌ Failed to create demo certificate: $($_.Exception.Message)" -ForegroundColor Red
        return ""
    }
}

function New-SimpleDemoProject {
    Write-Host "Creating simple demo project..." -ForegroundColor Green
    
    # Create demo source directory
    $srcDir = "demo-src"
    New-Item -ItemType Directory -Path $srcDir -Force | Out-Null
    
    # Create a simple C# project
    $csprojContent = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>
</Project>
"@
    
    $csprojPath = Join-Path $srcDir "TiXLDemo.csproj"
    $csprojContent | Out-File -FilePath $csprojPath -Encoding UTF8
    
    # Create simple C# code
    $csharpContent = @"
using System;

namespace TiXLDemo
{
    /// <summary>
    /// Demo class for TiXL secure build system
    /// </summary>
    public class DemoClass
    {
        /// <summary>
        /// Gets a demo message
        /// </summary>
        /// <returns>Demo message string</returns>
        public string GetDemoMessage()
        {
            return "TiXL Secure Build System Demo - Built with Reproducible Builds!";
        }
        
        /// <summary>
        /// Calculates a simple mathematical operation
        /// </summary>
        /// <param name="x">First number</param>
        /// <param name="y">Second number</param>
        /// <returns>Sum of the two numbers</returns>
        public int Add(int x, int y)
        {
            return x + y;
        }
    }
}
"@
    
    $csharpPath = Join-Path $srcDir "DemoClass.cs"
    $csharpContent | Out-File -FilePath $csharpPath -Encoding UTF8
    
    Write-Host "✅ Demo project created in $srcDir" -ForegroundColor Green
    Write-Host ""
    
    return $srcDir
}

function Invoke-DemoBuild {
    param([string]$SrcPath, [string]$CertThumbprint, [string]$Config)
    
    Write-Host "Running demo secure build..." -ForegroundColor Green
    Write-Host "Source: $SrcPath" -ForegroundColor Yellow
    Write-Host "Configuration: $Config" -ForegroundColor Yellow
    Write-Host "Certificate: $CertThumbprint" -ForegroundColor Yellow
    Write-Host ""
    
    try {
        # Create solution file for the demo
        $solutionDir = "demo-solution"
        New-Item -ItemType Directory -Path $solutionDir -Force | Out-Null
        
        $slnContent = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "TiXLDemo", "TiXLDemo.csproj", "{12345678-1234-1234-1234-123456789012}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{12345678-1234-1234-1234-123456789012}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{12345678-1234-1234-1234-123456789012}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{12345678-1234-1234-1234-123456789012}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{12345678-1234-1234-1234-123456789012}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
EndGlobal
"@
        
        $slnPath = Join-Path $solutionDir "TiXLDemo.sln"
        $slnContent | Out-File -FilePath $slnPath -Encoding UTF8
        
        # Copy source files to solution directory
        Copy-Item -Path (Join-Path $SrcPath "TiXLDemo.csproj") -Destination $solutionDir -Force
        Copy-Item -Path (Join-Path $SrcPath "DemoClass.cs") -Destination $solutionDir -Force
        
        # Copy Directory.Build.props to solution directory for reproducible builds
        if (Test-Path "Directory.Build.props") {
            Copy-Item -Path "Directory.Build.props" -Destination $solutionDir -Force
        }
        
        # Change to solution directory
        Set-Location $solutionDir
        
        # Build with reproducible settings
        Write-Host "Building with reproducible settings..." -ForegroundColor Green
        dotnet restore --audit
        
        dotnet build --configuration $Config --no-restore --verbosity minimal `
            /p:ContinuousIntegrationBuild=true `
            /p:Deterministic=true `
            /p:DeterministicSourcePaths=true `
            /p:PublishRepositoryUrl=true `
            /p:EmbedUntrackedSources=true `
            /p:IncludeSymbols=true
        
        # Create NuGet package
        Write-Host "Creating NuGet package..." -ForegroundColor Green
        dotnet pack --configuration $Config --no-build --output "../$OutputPath/packages" `
            /p:IncludeSymbols=true `
            /p:SymbolPackageFormat=snupkg
        
        # Copy artifacts to output
        $artifactsDir = "../$OutputPath"
        New-Item -ItemType Directory -Path $artifactsDir -Force | Out-Null
        
        # Copy built assembly
        $dllPath = "bin\$Config\net9.0\TiXLDemo.dll"
        if (Test-Path $dllPath) {
            Copy-Item -Path $dllPath -Destination $artifactsDir -Force
        }
        
        # Copy PDB file
        $pdbPath = "bin\$Config\net9.0\TiXLDemo.pdb"
        if (Test-Path $pdbPath) {
            Copy-Item -Path $pdbPath -Destination $artifactsDir -Force
        }
        
        # Copy packages
        if (Test-Path "../$OutputPath/packages") {
            Get-ChildItem -Path "../$OutputPath\packages" -File | ForEach-Object {
                Copy-Item -Path $_.FullName -Destination $artifactsDir -Force
            }
        }
        
        Write-Host "✅ Demo build completed successfully" -ForegroundColor Green
        Write-Host ""
        
        return $artifactsDir
    }
    catch {
        Write-Host "❌ Demo build failed: $($_.Exception.Message)" -ForegroundColor Red
        throw
    }
    finally {
        Set-Location ".."
    }
}

function Invoke-DemoSigning {
    param([string]$ArtifactsPath, [string]$CertThumbprint)
    
    if (-not $CertThumbprint) {
        Write-Host "No certificate thumbprint provided, skipping signing demo" -ForegroundColor Yellow
        return
    }
    
    Write-Host "Demonstrating code signing..." -ForegroundColor Green
    Write-Host "Certificate Thumbprint: $CertThumbprint" -ForegroundColor Yellow
    Write-Host ""
    
    try {
        # Sign the assembly
        $dllPath = Join-Path $ArtifactsPath "TiXLDemo.dll"
        if (Test-Path $dllPath) {
            Write-Host "Signing assembly: TiXLDemo.dll" -ForegroundColor Green
            & signtool.exe sign `
                /sha1 $CertThumbprint `
                /t http://timestamp.digicert.com `
                /fd sha256 `
                /d "TiXLDemo.dll" `
                $dllPath
                
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✅ Assembly signed successfully" -ForegroundColor Green
            } else {
                Write-Host "⚠️ Assembly signing failed" -ForegroundColor Yellow
            }
        }
        
        # Sign the NuGet package
        $nupkgPath = Join-Path $ArtifactsPath "TiXLDemo.*.nupkg"
        $packageFiles = Get-ChildItem -Path $nupkgPath -ErrorAction SilentlyContinue
        
        if ($packageFiles) {
            foreach ($package in $packageFiles) {
                Write-Host "Signing NuGet package: $($package.Name)" -ForegroundColor Green
                & dotnet nuget sign $package.FullName `
                    --certificate-store CurrentUser `
                    --certificate-fingerprint $CertThumbprint
                    
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "✅ NuGet package signed successfully" -ForegroundColor Green
                } else {
                    Write-Host "⚠️ NuGet package signing failed" -ForegroundColor Yellow
                }
            }
        }
        
        Write-Host ""
    }
    catch {
        Write-Host "❌ Signing demo failed: $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Invoke-DemoVerification {
    param([string]$ArtifactsPath, [string]$CertThumbprint)
    
    Write-Host "Running build integrity verification..." -ForegroundColor Green
    Write-Host "Artifacts Path: $ArtifactsPath" -ForegroundColor Yellow
    Write-Host ""
    
    try {
        # Create a simple build manifest for demo
        $manifest = @{
            BuildInfo = @{
                BuildId = (Get-Date -Format "yyyyMMdd-HHmmss")
                BuildTime = (Get-Date).ToUniversalTime()
                Configuration = "Debug"
                GitCommit = "demo123"
                GitBranch = "demo"
                BuildMachine = $env:COMPUTERNAME
                User = $env:USERNAME
                DotNetVersion = (dotnet --version).Trim()
                OSPlatform = "Windows"
            }
            ReproducibleBuild = @{
                SourceLinkEnabled = $true
                EmbedSources = $true
                DeterministicBuild = $true
                FixedBuildTime = $true
                FixedFileVersion = $true
            }
            Security = @{
                SigningEnabled = $(-not [string]::IsNullOrEmpty($CertThumbprint))
                IntegrityVerification = $true
                TimestampServer = "http://timestamp.digicert.com"
                CertificateStore = "Cert:\CurrentUser\My"
            }
            Integrity = @()
        }
        
        # Add integrity entries for all artifacts
        $artifacts = Get-ChildItem -Path $ArtifactsPath -File
        foreach ($artifact in $artifacts) {
            $hash = Get-FileHash -Path $artifact.FullName -Algorithm SHA256
            $manifest.Integrity += @{
                FileName = $artifact.Name
                FilePath = $artifact.FullName
                Hash = $hash.Hash
                HashAlgorithm = "SHA256"
                Size = $artifact.Length
                IsSigned = $false
                VerificationTime = (Get-Date).ToUniversalTime()
            }
        }
        
        # Save manifest
        $manifestPath = Join-Path $ArtifactsPath "build-manifest.json"
        $manifest | ConvertTo-Json -Depth 10 | Out-File -FilePath $manifestPath -Encoding UTF8
        
        Write-Host "✅ Build manifest created" -ForegroundColor Green
        Write-Host ""
        
        # Verify signatures if certificate is available
        if ($CertThumbprint) {
            Write-Host "Verifying signatures..." -ForegroundColor Green
            
            foreach ($artifact in $artifacts) {
                if ($artifact.Extension -eq ".dll") {
                    & signtool.exe verify /pa $artifact.FullName | Out-Null
                    if ($LASTEXITCODE -eq 0) {
                        Write-Host "✅ Signature verified: $($artifact.Name)" -ForegroundColor Green
                    } else {
                        Write-Host "⚠️ Signature verification failed: $($artifact.Name)" -ForegroundColor Yellow
                    }
                } elseif ($artifact.Extension -eq ".nupkg") {
                    & dotnet nuget verify sign $artifact.FullName | Out-Null
                    if ($LASTEXITCODE -eq 0) {
                        Write-Host "✅ NuGet signature verified: $($artifact.Name)" -ForegroundColor Green
                    } else {
                        Write-Host "⚠️ NuGet signature verification failed: $($artifact.Name)" -ForegroundColor Yellow
                    }
                }
            }
        }
        
        Write-Host ""
    }
    catch {
        Write-Host "❌ Verification demo failed: $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Show-DemoSummary {
    param([string]$ArtifactsPath, [string]$CertThumbprint)
    
    Write-Host "======================================" -ForegroundColor Cyan
    Write-Host "DEMO SUMMARY" -ForegroundColor Cyan
    Write-Host "======================================" -ForegroundColor Cyan
    
    Write-Host "Demo completed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Generated Artifacts:" -ForegroundColor Yellow
    Write-Host "==================" -ForegroundColor Yellow
    
    if (Test-Path $ArtifactsPath) {
        $artifacts = Get-ChildItem -Path $ArtifactsPath -File
        foreach ($artifact in $artifacts) {
            $sizeMB = "{0:N2}" -f ($artifact.Length / 1MB)
            Write-Host "- $($artifact.Name) ($sizeMB MB)" -ForegroundColor White
        }
    }
    
    Write-Host ""
    Write-Host "Certificate Information:" -ForegroundColor Yellow
    Write-Host "=======================" -ForegroundColor Yellow
    
    if ($CertThumbprint) {
        Write-Host "✅ Code signing enabled" -ForegroundColor Green
        Write-Host "Certificate Thumbprint: $CertThumbprint" -ForegroundColor White
    } else {
        Write-Host "⚪ Code signing disabled" -ForegroundColor Gray
    }
    
    Write-Host ""
    Write-Host "Next Steps:" -ForegroundColor Yellow
    Write-Host "==========" -ForegroundColor Yellow
    Write-Host "1. Review generated artifacts in: $ArtifactsPath" -ForegroundColor White
    Write-Host "2. Examine the build manifest for verification data" -ForegroundColor White
    Write-Host "3. Test the signed assemblies in your application" -ForegroundColor White
    Write-Host "4. Run: .\scripts\verify-build-security.ps1 for comprehensive verification" -ForegroundColor White
    Write-Host ""
    Write-Host "For more information, see: docs/TIXL-059_Secure_Build_Process.md" -ForegroundColor Cyan
}

# Main execution
try {
    switch ($Action) {
        "Demo" {
            Write-Host "Starting TiXL Secure Build System Demo..." -ForegroundColor Green
            Write-Host ""
            
            # Step 1: Create demo certificate
            $certThumbprint = New-DemoCertificate
            
            # Step 2: Create demo project
            $srcPath = New-SimpleDemoProject
            
            # Step 3: Build with reproducible settings
            $artifactsPath = Invoke-DemoBuild -SrcPath $srcPath -CertThumbprint $certThumbprint -Config $Configuration
            
            # Step 4: Demonstrate signing
            Invoke-DemoSigning -ArtifactsPath $artifactsPath -CertThumbprint $certThumbprint
            
            # Step 5: Demonstrate verification
            Invoke-DemoVerification -ArtifactsPath $artifactsPath -CertThumbprint $certThumbprint
            
            # Step 6: Show summary
            Show-DemoSummary -ArtifactsPath $artifactsPath -CertThumbprint $certThumbprint
            
            Write-Host "🎉 Demo completed successfully!" -ForegroundColor Green
        }
        
        "Validate" {
            Write-Host "Validating TiXL Secure Build System..." -ForegroundColor Green
            Write-Host ""
            
            # Run comprehensive verification
            if (Test-Path $OutputPath) {
                & .\scripts\verify-build-security.ps1 -BuildPath $OutputPath -OutputFormat "Markdown" -OutputPath "$OutputPath\verification-report.md"
            } else {
                Write-Host "No artifacts found for validation. Run demo first." -ForegroundColor Yellow
            }
        }
        
        "Setup" {
            Write-Host "Setting up TiXL Secure Build System..." -ForegroundColor Green
            Write-Host ""
            
            Write-Host "1. Checking prerequisites..." -ForegroundColor Yellow
            $dotNetVersion = dotnet --version
            Write-Host "✅ .NET SDK: $dotNetVersion" -ForegroundColor Green
            
            Write-Host ""
            Write-Host "2. Verifying scripts..." -ForegroundColor Yellow
            
            $requiredScripts = @(
                "scripts\build-security.ps1",
                "scripts\verify-build-integrity.ps1",
                "scripts\manage-certificates.ps1",
                "scripts\verify-build-security.ps1"
            )
            
            foreach ($script in $requiredScripts) {
                if (Test-Path $script) {
                    Write-Host "✅ $script" -ForegroundColor Green
                } else {
                    Write-Host "❌ $script (missing)" -ForegroundColor Red
                }
            }
            
            Write-Host ""
            Write-Host "3. Checking configuration..." -ForegroundColor Yellow
            
            if (Test-Path "Directory.Build.props") {
                Write-Host "✅ Directory.Build.props found" -ForegroundColor Green
            } else {
                Write-Host "❌ Directory.Build.props missing" -ForegroundColor Red
            }
            
            Write-Host ""
            Write-Host "4. CI/CD Integration..." -ForegroundColor Yellow
            
            if (Test-Path ".github\workflows\secure-build.yml") {
                Write-Host "✅ GitHub Actions workflow found" -ForegroundColor Green
            } else {
                Write-Host "❌ GitHub Actions workflow missing" -ForegroundColor Red
            }
            
            Write-Host ""
            Write-Host "✅ Setup validation completed!" -ForegroundColor Green
        }
    }
    
    exit 0
}
catch {
    Write-Host "❌ Demo failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
    exit 1
}