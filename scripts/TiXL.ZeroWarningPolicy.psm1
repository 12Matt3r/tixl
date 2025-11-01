# TiXL Zero-Warning Policy PowerShell Module
# Module for integrating TiXL warning detection and fixing into other workflows

using namespace System.Management.Automation

function Invoke-TiXLWarningCheck {
    <#
    .SYNOPSIS
        Runs TiXL zero-warning policy analysis
    
    .DESCRIPTION
        This cmdlet integrates with the TiXL zero-warning policy to check for compiler
        warnings and optionally apply automatic fixes. It's designed for use in CI/CD
        pipelines and automated workflows.
    
    .PARAMETER SolutionPath
        Path to the .sln file
    
    .PARAMETER ProjectPath
        Path to a specific .csproj file
    
    .PARAMETER AutoFix
        Enable automatic fixes for supported warning types
    
    .PARAMETER BuildAnalysis
        Run build analysis to detect compiler warnings
    
    .PARAMETER FailOnWarnings
        Throw an exception if warnings are found
    
    .EXAMPLE
        Invoke-TiXLWarningCheck -SolutionPath "TiXL.sln" -FailOnWarnings
    
    .EXAMPLE
        Invoke-TiXLWarningCheck -ProjectPath "Tests\TiXL.Tests.csproj" -AutoFix
    #>
    
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [string]$SolutionPath = "TiXL.sln",
        
        [Parameter(Mandatory = $false)]
        [string]$ProjectPath,
        
        [Parameter(Mandatory = $false)]
        [switch]$AutoFix,
        
        [Parameter(Mandatory = $false)]
        [switch]$BuildAnalysis,
        
        [Parameter(Mandatory = $false)]
        [switch]$FailOnWarnings,
        
        [Parameter(Mandatory = $false)]
        [switch]$VerboseOutput
    )
    
    begin {
        $scriptPath = Split-Path -Parent $PSCommandPath
        $warningScript = Join-Path $scriptPath "detect-and-fix-warnings.ps1"
        
        if (-not (Test-Path $warningScript)) {
            throw "Warning analysis script not found at: $warningScript"
        }
    }
    
    process {
        $arguments = @()
        
        if ($SolutionPath) { $arguments += "-SolutionPath"; $arguments += "`"$SolutionPath`"" }
        if ($ProjectPath) { $arguments += "-ProjectPath"; $arguments += "`"$ProjectPath`"" }
        if ($AutoFix) { $arguments += "-AutoFix" }
        if ($BuildAnalysis) { $arguments += "-BuildAnalysis" }
        if ($VerboseOutput) { $arguments += "-ShowDetails" }
        
        try {
            if ($VerboseOutput) {
                Write-Host "Running TiXL zero-warning analysis..." -ForegroundColor Green
            }
            
            $result = & pwsh -File $warningScript @arguments 2>&1
            
            if ($LASTEXITCODE -eq 0) {
                if ($VerboseOutput) {
                    Write-Host "✅ Zero warnings achieved!" -ForegroundColor Green
                }
                return $true
            }
            elseif ($LASTEXITCODE -eq 1) {
                if ($VerboseOutput) {
                    Write-Host "❌ Warnings found during analysis" -ForegroundColor Red
                }
                
                if ($FailOnWarnings) {
                    throw "TiXL zero-warning policy violation: warnings detected"
                }
                
                return $false
            }
            else {
                throw "Warning analysis failed with exit code: $LASTEXITCODE"
            }
        }
        catch {
            Write-Error "Failed to run warning analysis: $_"
            if ($FailOnWarnings) {
                throw
            }
            return $false
        }
    }
}

function Test-TiXLBuildQuality {
    <#
    .SYNOPSIS
        Tests if a TiXL build meets zero-warning quality standards
    
    .DESCRIPTION
        This cmdlet performs a complete quality check including build and warning analysis.
        It's designed for CI/CD pipeline integration.
    
    .PARAMETER SolutionPath
        Path to the solution file
    
    .PARAMETER ProjectPath
        Path to specific project (optional)
    
    .PARAMETER Configuration
        Build configuration to test
    
    .EXAMPLE
        Test-TiXLBuildQuality -SolutionPath "TiXL.sln" -Configuration "Release"
    #>
    
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [string]$SolutionPath = "TiXL.sln",
        
        [Parameter(Mandatory = $false)]
        [string]$ProjectPath,
        
        [Parameter(Mandatory = $false)]
        [ValidateSet("Debug", "Release")]
        [string]$Configuration = "Release"
    )
    
    begin {
        $buildTarget = if ($ProjectPath) { $ProjectPath } else { $SolutionPath }
    }
    
    process {
        try {
            Write-Host "Building $buildTarget with $Configuration configuration..." -ForegroundColor Yellow
            
            # Perform the build
            $buildResult = & dotnet build $buildTarget --configuration $Configuration --verbosity minimal 2>&1
            
            if ($LASTEXITCODE -ne 0) {
                Write-Host "Build failed!" -ForegroundColor Red
                return $false
            }
            
            Write-Host "Build successful. Checking warnings..." -ForegroundColor Green
            
            # Run warning analysis
            $warningCheck = Invoke-TiXLWarningCheck -SolutionPath $SolutionPath -ProjectPath $ProjectPath -FailOnWarnings
            
            if ($warningCheck) {
                Write-Host "✅ Build quality check passed: Zero warnings achieved!" -ForegroundColor Green
                return $true
            }
            else {
                Write-Host "❌ Build quality check failed: Warnings detected" -ForegroundColor Red
                return $false
            }
        }
        catch {
            Write-Error "Build quality check failed: $_"
            return $false
        }
    }
}

function Get-TiXLWarningReport {
    <#
    .SYNOPSIS
        Generates a comprehensive warning report for TiXL codebase
    
    .DESCRIPTION
        This cmdlet generates a detailed markdown report of all warnings found in the
        TiXL codebase, suitable for team review and progress tracking.
    
    .PARAMETER SolutionPath
        Path to the solution file
    
    .PARAMETER OutputPath
        Path for the generated report
    
    .PARAMETER IncludeBuildAnalysis
        Include build-based warning detection
    
    .EXAMPLE
        Get-TiXLWarningReport -SolutionPath "TiXL.sln" -OutputPath "weekly-warning-report.md"
    #>
    
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [string]$SolutionPath = "TiXL.sln",
        
        [Parameter(Mandatory = $false)]
        [string]$OutputPath = "TiXL-warning-report-$(Get-Date -Format 'yyyyMMdd-HHmmss').md",
        
        [Parameter(Mandatory = $false)]
        [switch]$IncludeBuildAnalysis
    )
    
    process {
        $arguments = @("-SolutionPath", "`"$SolutionPath`"", "-OutputPath", "`"$OutputPath`"")
        
        if ($IncludeBuildAnalysis) {
            $arguments += "-BuildAnalysis"
        }
        
        try {
            $scriptPath = Split-Path -Parent $PSCommandPath
            $warningScript = Join-Path $scriptPath "detect-and-fix-warnings.ps1"
            
            Write-Host "Generating warning report: $OutputPath" -ForegroundColor Yellow
            & pwsh -File $warningScript @arguments | Out-Null
            
            if (Test-Path $OutputPath) {
                Write-Host "✅ Warning report generated: $OutputPath" -ForegroundColor Green
                return $OutputPath
            }
            else {
                throw "Report file was not generated"
            }
        }
        catch {
            Write-Error "Failed to generate warning report: $_"
            throw
        }
    }
}

function Enable-TiXLPreCommitHook {
    <#
    .SYNOPSIS
        Sets up pre-commit hook for TiXL zero-warning policy
    
    .DESCRIPTION
        This cmdlet creates or updates the git pre-commit hook to enforce the
        TiXL zero-warning policy before each commit.
    
    .EXAMPLE
        Enable-TiXLPreCommitHook
    #>
    
    process {
        $gitHooksPath = Join-Path (Get-Location) ".git\hooks"
        $preCommitScript = Join-Path $gitHooksPath "pre-commit"
        
        if (-not (Test-Path $gitHooksPath)) {
            Write-Warning "Not in a git repository or .git/hooks not found"
            return
        }
        
        $hookContent = @"
#!/usr/bin/env pwsh
# TiXL Zero-Warning Policy Pre-Commit Hook

`$ErrorActionPreference = "Stop"

Write-Host "Running TiXL zero-warning policy checks..." -ForegroundColor Green

# Check for staged C# files
`$stagedFiles = git diff --cached --name-only --diff-filter=ACM | Where-Object { `$_ -match '\.cs$`' }

if (`$stagedFiles.Count -eq 0) {
    Write-Host "No C# files to check." -ForegroundColor Yellow
    exit 0
}

# Run warning analysis
Write-Host "Checking staged changes for warnings..." -ForegroundColor Yellow
`$scriptPath = Split-Path -Parent `$PSCommandPath
`$warningScript = Join-Path (Split-Path (Split-Path `$PSCommandPath -Parent)) "scripts\detect-and-fix-warnings.ps1"

if (Test-Path `$warningScript) {
    & pwsh -File `$warningScript -AutoFix
    if (`$LASTEXITCODE -ne 0) {
        Write-Host "❌ TiXL zero-warning policy violation detected!" -ForegroundColor Red
        Write-Host "Please fix warnings before committing." -ForegroundColor Yellow
        Write-Host "Run: .\scripts\detect-and-fix-warnings.ps1 -AutoFix" -ForegroundColor Cyan
        exit 1
    }
} else {
    Write-Host "Warning analysis script not found, skipping checks." -ForegroundColor Yellow
}

Write-Host "✅ Pre-commit checks passed!" -ForegroundColor Green
"@
        
        try {
            $hookContent | Out-File -FilePath $preCommitScript -Encoding UTF8
            # Make executable on Unix-like systems
            if ($IsLinux -or $IsMacOS) {
                chmod +x $preCommitScript
            }
            
            Write-Host "✅ Pre-commit hook installed successfully!" -ForegroundColor Green
            Write-Host "TiXL zero-warning policy will now be enforced on every commit." -ForegroundColor Cyan
        }
        catch {
            Write-Error "Failed to install pre-commit hook: $_"
        }
    }
}

# Export functions
Export-ModuleMember -Function @(
    'Invoke-TiXLWarningCheck',
    'Test-TiXLBuildQuality',
    'Get-TiXLWarningReport',
    'Enable-TiXLPreCommitHook'
)

# Module metadata
$script:ModuleData = @{
    ModuleName = 'TiXL.ZeroWarningPolicy'
    Version = '2.0.0'
    Description = 'PowerShell module for TiXL Zero-Warning Policy enforcement'
    Author = 'TiXL Team'
    SupportedWarningCodes = @(
        'CS8600','CS8601','CS8602','CS8603','CS8604','CS8605','CS8606','CS8607','CS8608','CS8609',
        'CS8610','CS8611','CS8612','CS8613','CS8614','CS8615','CS8616','CS8617','CS8618','CS8619',
        'CS8620','CS8621','CS8622','CS8623','CS8624','CS8625','CS8626','CS8627','CS8628','CS8629',
        'CS8630','CS8631','CS8632','CS8633','CS8634','CS8635','CS8636','CS8637','CS8638','CS8639',
        'CS8640','CS8641','CS8642','CS8643','CS8644','CS8645','CS8646','CS8647','CS8648','CS8649',
        'CS8650','CS8651','CS8652','CS8653','CS8654','CS8655','CS8656','CS8657','CS8658','CS8659',
        'CS8660','CS8661','CS8662','CS8663','CS8664','CS8665','CS8666','CS8667','CS8668','CS8669',
        'CS0168','CS0219','CS0618','CS1591','CS1998','CS4014'
    )
}

Write-Host "TiXL Zero-Warning Policy PowerShell Module loaded successfully!" -ForegroundColor Green
Write-Host "Use Get-Command -Module TiXL.ZeroWarningPolicy to see available commands." -ForegroundColor Cyan