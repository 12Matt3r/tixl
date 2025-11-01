#!/usr/bin/env pwsh

param(
    [switch]$Verbose,
    [switch]$FailOnWarning,
    [string]$OutputPath = "security-report.json"
)

Write-Host "🔍 Starting TiXL Security Scan..." -ForegroundColor Green

# Check prerequisites
Write-Host "Checking prerequisites..." -ForegroundColor Yellow
$prereqChecks = @()

# Check .NET SDK
try {
    $dotnetVersion = dotnet --version
    Write-Host "✅ .NET SDK: $dotnetVersion" -ForegroundColor Green
    $prereqChecks += $true
} catch {
    Write-Host "❌ .NET SDK not found" -ForegroundColor Red
    $prereqChecks += $false
}

# Check GitHub CLI
try {
    $ghVersion = gh --version | Select-String "version"
    Write-Host "✅ GitHub CLI: $ghVersion" -ForegroundColor Green
    $prereqChecks += $true
} catch {
    Write-Host "⚠️  GitHub CLI not found (optional)" -ForegroundColor Yellow
    $prereqChecks += $false
}

if ($prereqChecks -contains $false) {
    Write-Error "Prerequisites check failed. Please install required tools."
    exit 1
}

# Initialize results
$results = @{
    timestamp = Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ"
    project = "TiXL"
    scans = @{}
    summary = @{}
}

Write-Host "`n🔍 Running NuGet Package Audit..." -ForegroundColor Yellow

# NuGet Audit
try {
    Write-Host "Restoring packages with audit..." -ForegroundColor Blue
    $auditOutput = dotnet restore --verbosity minimal 2>&1 | Out-String
    
    Write-Host "Checking for vulnerable packages..." -ForegroundColor Blue
    $vulnerableOutput = dotnet list package --vulnerable --include-transitive 2>&1 | Out-String
    
    $nugetVulnerabilities = if ($vulnerableOutput -match "has the following vulnerable packages") {
        $vulnerableOutput
    } else {
        "No vulnerabilities detected"
    }
    
    $results.scans.nugetAudit = @{
        status = if ($nugetVulnerabilities -eq "No vulnerabilities detected") { "PASS" } else { "FAIL" }
        output = $nugetVulnerabilities
        timestamp = Get-Date
    }
    
    Write-Host "✅ NuGet Audit completed" -ForegroundColor Green
} catch {
    Write-Host "❌ NuGet Audit failed: $_" -ForegroundColor Red
    $results.scans.nugetAudit = @{
        status = "ERROR"
        error = $_.ToString()
        timestamp = Get-Date
    }
}

# dotnet-retire scan
Write-Host "`n🔍 Running dotnet-retire scan..." -ForegroundColor Yellow

try {
    # Install dotnet-retire if not present
    $retireInstalled = dotnet tool list -g | Select-String "dotnet-retire"
    if (-not $retireInstalled) {
        Write-Host "Installing dotnet-retire..." -ForegroundColor Blue
        dotnet tool install --global dotnet-retire --version 3.1.0
    }
    
    $retireOutput = dotnet retire --ignore-urls "https://localhost/**" 2>&1 | Out-String
    
    $results.scans.dotnetRetire = @{
        status = if ($retireOutput -match "No known vulnerabilities") { "PASS" } else { "FAIL" }
        output = $retireOutput
        timestamp = Get-Date
    }
    
    Write-Host "✅ dotnet-retire scan completed" -ForegroundColor Green
} catch {
    Write-Host "❌ dotnet-retire scan failed: $_" -ForegroundColor Red
    $results.scans.dotnetRetire = @{
        status = "ERROR"
        error = $_.ToString()
        timestamp = Get-Date
    }
}

# Package analysis
Write-Host "`n📦 Analyzing packages..." -ForegroundColor Yellow

try {
    $outdatedOutput = dotnet list package --outdated --include-transitive 2>&1 | Out-String
    $deprecatedOutput = dotnet list package --deprecated --include-transitive 2>&1 | Out-String
    
    $results.scans.packageAnalysis = @{
        outdated = $outdatedOutput
        deprecated = $deprecatedOutput
        timestamp = Get-Date
    }
    
    Write-Host "✅ Package analysis completed" -ForegroundColor Green
} catch {
    Write-Host "❌ Package analysis failed: $_" -ForegroundColor Red
    $results.scans.packageAnalysis = @{
        status = "ERROR"
        error = $_.ToString()
        timestamp = Get-Date
    }
}

# Generate summary
$totalScans = $results.scans.Count
$passedScans = ($results.scans.Values | Where-Object { $_.status -eq "PASS" }).Count
$failedScans = ($results.scans.Values | Where-Object { $_.status -eq "FAIL" }).Count
$errorScans = ($results.scans.Values | Where-Object { $_.status -eq "ERROR" }).Count

$results.summary = @{
    total = $totalScans
    passed = $passedScans
    failed = $failedScans
    errors = $errorScans
    overallStatus = if ($errorScans -eq 0 -and $failedScans -eq 0) { "PASS" } elseif ($errorScans -gt 0) { "ERROR" } else { "FAIL" }
}

# Output results
Write-Host "`n📊 Security Scan Summary" -ForegroundColor Cyan
Write-Host "=========================" -ForegroundColor Cyan
Write-Host "Total Scans: $totalScans" -ForegroundColor White
Write-Host "Passed: $passedScans" -ForegroundColor Green
Write-Host "Failed: $failedScans" -ForegroundColor Red
Write-Host "Errors: $errorScans" -ForegroundColor Yellow
Write-Host "Overall Status: $($results.summary.overallStatus)" -ForegroundColor $(if ($results.summary.overallStatus -eq "PASS") { "Green" } elseif ($results.summary.overallStatus -eq "ERROR") { "Yellow" } else { "Red" })

# Save results to file
$results | ConvertTo-Json -Depth 10 | Out-File -FilePath $OutputPath -Encoding UTF8
Write-Host "`n💾 Results saved to: $OutputPath" -ForegroundColor Blue

# Exit with appropriate code
if ($results.summary.overallStatus -eq "PASS") {
    exit 0
} elseif ($FailOnWarning -and $results.summary.overallStatus -ne "PASS") {
    exit 1
} elseif ($results.summary.overallStatus -eq "ERROR") {
    exit 2
} else {
    exit 1
}