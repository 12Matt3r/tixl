#!/usr/bin/env pwsh

<#
.SYNOPSIS
TiXL Naming Convention Migration Script

.DESCRIPTION
This script helps migrate the TiXL codebase to follow naming conventions.
It can analyze the codebase, generate reports, and apply automated fixes.

.PARAMETER SolutionPath
Path to the TiXL solution file

.PARAMETER Action
Action to perform: Analyze, Fix, Report

.PARAMETER OutputPath
Path for output files (optional)

.PARAMETER ProjectFilter
Filter for which projects to process (default: TiXL.*)

.PARAMETER DryRun
Perform analysis only, don't apply fixes

.EXAMPLE
.\Migrate-NamingConventions.ps1 -SolutionPath "TiXL.sln" -Action Analyze

.EXAMPLE
.\Migrate-NamingConventions.ps1 -SolutionPath "TiXL.sln" -Action Fix -DryRun

.EXAMPLE
.\Migrate-NamingConventions.ps1 -SolutionPath "TiXL.sln" -Action Report -OutputPath "naming-report.json"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SolutionPath,
    
    [Parameter(Mandatory = $true)]
    [ValidateSet("Analyze", "Fix", "Report")]
    [string]$Action,
    
    [Parameter(Mandatory = $false)]
    [string]$OutputPath,
    
    [Parameter(Mandatory = $false)]
    [string]$ProjectFilter = "TiXL.*",
    
    [Parameter(Mandatory = $false)]
    [switch]$DryRun = $false,
    
    [Parameter(Mandatory = $false)]
    [switch]$Verbose = $false
)

$ErrorActionPreference = "Stop"

# Configuration
$AnalyzerToolPath = "src\Tools\TiXL.NamingConventionChecker\bin\Debug\net8.0\TiXL.NamingConventionChecker.exe"
$DefaultOutputPath = "naming-conventions"

function Test-Prerequisites {
    Write-Host "Checking prerequisites..." -ForegroundColor Cyan
    
    # Check if solution file exists
    if (-not (Test-Path $SolutionPath)) {
        Write-Error "Solution file not found: $SolutionPath"
        return $false
    }
    
    # Check if analyzer tool exists
    if (-not (Test-Path $AnalyzerToolPath)) {
        Write-Host "Building naming convention analyzer tool..." -ForegroundColor Yellow
        
        try {
            Push-Location "src\Tools\TiXL.NamingConventionChecker"
            dotnet build --configuration Debug --verbosity quiet
            Pop-Location
            
            if (-not (Test-Path $AnalyzerToolPath)) {
                Write-Error "Failed to build analyzer tool"
                return $false
            }
        }
        catch {
            Write-Error "Failed to build analyzer tool: $_"
            return $false
        }
    }
    
    Write-Host "Prerequisites check passed." -ForegroundColor Green
    return $true
}

function Build-CommandArguments {
    param(
        [string]$Action,
        [string]$SolutionPath,
        [string]$ProjectFilter,
        [string]$OutputPath,
        [switch]$DryRun
    )
    
    $args = @()
    $args += "--solution-path", "`"$SolutionPath`""
    $args += "--project-pattern", "`"$ProjectFilter`""
    
    switch ($Action) {
        "Analyze" {
            $args += "--show-fixes"
            if ($Verbose) { $args += "--verbose" }
            if ($OutputPath) { $args += "--output-file", "`"$OutputPath`"" }
        }
        "Fix" {
            if ($DryRun) {
                $args += "--show-fixes"
            } else {
                $args += "--apply-fixes"
            }
            if ($Verbose) { $args += "--verbose" }
        }
        "Report" {
            $args += "--output-format", "json"
            if ($OutputPath) { 
                $args += "--output-file", "`"$OutputPath`"" 
            } else {
                $args += "--output-file", "`"$DefaultOutputPath\report.json`""
            }
            if ($Verbose) { $args += "--verbose" }
        }
    }
    
    return $args
}

function Start-NamingConventionProcess {
    param(
        [string[]]$Arguments
    )
    
    Write-Host "Running naming convention analyzer..." -ForegroundColor Cyan
    
    $processInfo = New-Object System.Diagnostics.ProcessStartInfo
    $processInfo.FileName = $AnalyzerToolPath
    $processInfo.Arguments = $Arguments -join " "
    $processInfo.UseShellExecute = $false
    $processInfo.RedirectStandardOutput = $true
    $processInfo.RedirectStandardError = $true
    $processInfo.CreateNoWindow = $true
    
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $processInfo
    
    try {
        $process.Start()
        $output = $process.StandardOutput.ReadToEnd()
        $error = $process.StandardError.ReadToEnd()
        $process.WaitForExit()
        
        if ($process.ExitCode -eq 0) {
            Write-Host "Analysis completed successfully." -ForegroundColor Green
            if ($output) {
                Write-Host $output
            }
        } else {
            Write-Warning "Process completed with exit code: $($process.ExitCode)"
            if ($error) {
                Write-Error $error
            }
            if ($output) {
                Write-Host $output
            }
        }
        
        return $process.ExitCode
    }
    catch {
        Write-Error "Failed to run analyzer: $_"
        return 1
    }
}

function New-MigrationPlan {
    param(
        [string]$SolutionPath
    )
    
    Write-Host "Creating migration plan..." -ForegroundColor Cyan
    
    # First run analysis to get current state
    $analysisArgs = Build-CommandArguments -Action "Analyze" -SolutionPath $SolutionPath -ProjectFilter $ProjectFilter -Verbose:$Verbose
    $exitCode = Start-NamingConventionProcess $analysisArgs
    
    if ($exitCode -eq 0) {
        Write-Host "`nMigration plan created. Review the analysis results above." -ForegroundColor Yellow
        Write-Host "`nNext steps:" -ForegroundColor Yellow
        Write-Host "1. Review all violations and suggested fixes"
        Write-Host "2. Run with -Action Fix -DryRun to preview changes"
        Write-Host "3. Run with -Action Fix to apply fixes (make sure to commit first!)"
        Write-Host "4. Re-run analysis to verify compliance"
    } else {
        Write-Error "Failed to create migration plan"
        return $false
    }
    
    return $true
}

function Start-Migration {
    param(
        [string]$SolutionPath,
        [switch]$DryRun
    )
    
    Write-Host "Starting migration process..." -ForegroundColor Cyan
    
    if ($DryRun) {
        Write-Host "DRY RUN MODE: No changes will be applied." -ForegroundColor Yellow
    }
    
    # Create backup if not dry run
    if (-not $DryRun) {
        $backupPath = "naming-migration-backup-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
        Write-Host "Creating backup..." -ForegroundColor Yellow
        if (Test-Path $backupPath) {
            Remove-Item $backupPath -Recurse -Force
        }
        Copy-Item (Split-Path $SolutionPath -Parent) $backupPath -Recurse
        Write-Host "Backup created at: $backupPath" -ForegroundColor Green
    }
    
    $fixArgs = Build-CommandArguments -Action "Fix" -SolutionPath $SolutionPath -ProjectFilter $ProjectFilter -DryRun:$DryRun -Verbose:$Verbose
    $exitCode = Start-NamingConventionProcess $fixArgs
    
    if ($exitCode -eq 0) {
        if ($DryRun) {
            Write-Host "Dry run completed. Review the proposed changes above." -ForegroundColor Green
        } else {
            Write-Host "Migration completed successfully." -ForegroundColor Green
            
            # Run final verification
            Write-Host "Running verification..." -ForegroundColor Cyan
            $verifyArgs = Build-CommandArguments -Action "Analyze" -SolutionPath $SolutionPath -ProjectFilter $ProjectFilter -Verbose:$Verbose
            Start-NamingConventionProcess $verifyArgs
        }
    } else {
        Write-Error "Migration failed with exit code: $exitCode"
        return $false
    }
    
    return $true
}

function New-ComplianceReport {
    param(
        [string]$SolutionPath,
        [string]$OutputPath
    )
    
    Write-Host "Generating compliance report..." -ForegroundColor Cyan
    
    # Ensure output directory exists
    $reportDir = Split-Path $OutputPath -Parent
    if (-not (Test-Path $reportDir)) {
        New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
    }
    
    $reportArgs = Build-CommandArguments -Action "Report" -SolutionPath $SolutionPath -ProjectFilter $ProjectFilter -OutputPath $OutputPath -Verbose:$Verbose
    $exitCode = Start-NamingConventionProcess $reportArgs
    
    if ($exitCode -eq 0) {
        Write-Host "Compliance report generated at: $OutputPath" -ForegroundColor Green
    } else {
        Write-Error "Failed to generate compliance report"
        return $false
    }
    
    return $true
}

# Main execution
try {
    if (-not (Test-Prerequisites)) {
        exit 1
    }
    
    Write-Host "`nTiXL Naming Convention Migration" -ForegroundColor Magenta
    Write-Host "Action: $Action" -ForegroundColor White
    Write-Host "Solution: $SolutionPath" -ForegroundColor White
    Write-Host "Project Filter: $ProjectFilter" -ForegroundColor White
    if ($DryRun) { Write-Host "Mode: Dry Run" -ForegroundColor Yellow }
    Write-Host ""
    
    switch ($Action) {
        "Analyze" {
            $success = New-MigrationPlan -SolutionPath $SolutionPath
        }
        "Fix" {
            $success = Start-Migration -SolutionPath $SolutionPath -DryRun:$DryRun
        }
        "Report" {
            if (-not $OutputPath) {
                $OutputPath = "$DefaultOutputPath\report.json"
            }
            $success = New-ComplianceReport -SolutionPath $SolutionPath -OutputPath $OutputPath
        }
    }
    
    if ($success) {
        Write-Host "`n$Action completed successfully." -ForegroundColor Green
        exit 0
    } else {
        Write-Host "`n$Action failed." -ForegroundColor Red
        exit 1
    }
}
catch {
    Write-Error "Unexpected error: $_"
    Write-Host $_.ScriptStackTrace
    exit 1
}