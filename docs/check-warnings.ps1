<#
.SYNOPSIS
    TiXL Warning Checker - Analyzes C# projects for compiler warnings and code quality issues.

.DESCRIPTION
    This script analyzes the TiXL codebase for common warning patterns and provides
    recommendations for fixing them. It can be used locally or in CI/CD pipelines.

.PARAMETER SolutionPath
    Path to the .sln file or directory containing the solution.

.PARAMETER ProjectPath
    Path to a specific .csproj file to analyze.

.PARAMETER OutputPath
    Path to save the analysis report.

.PARAMETER FixMode
    Enable automatic fixes for certain warning types.

.PARAMETER DetailedAnalysis
    Perform deep analysis including static analysis and code metrics.

.EXAMPLE
    .\check-warnings.ps1 -SolutionPath "..\TiXL.sln"

.EXAMPLE
    .\check-warnings.ps1 -ProjectPath "Core\TiXL.Core.csproj" -FixMode -OutputPath "warning-report.html"
#>

param(
    [Parameter(Mandatory = $false)]
    [string]$SolutionPath = "..\TiXL.sln",
    
    [Parameter(Mandatory = $false)]
    [string]$ProjectPath,
    
    [Parameter(Mandatory = $false)]
    [string]$OutputPath = "warning-analysis.md",
    
    [Parameter(Mandatory = $false)]
    [switch]$FixMode,
    
    [Parameter(Mandatory = $false)]
    [switch]$DetailedAnalysis,
    
    [Parameter(Mandatory = $false)]
    [switch]$ShowProgress
)

$ErrorActionPreference = "Stop"

# Script configuration
$script:WarningCount = 0
$script:WarningCategories = @{}
$script:FixedWarnings = 0

function Write-ProgressSafe {
    param([string]$Message)
    if ($ShowProgress) {
        Write-Progress -Activity "TiXL Warning Analysis" -Status $Message
    }
}

function Write-WarningSection {
    param([string]$Title, [string]$Description)
    Write-Host "`n## $Title" -ForegroundColor Cyan
    Write-Host $Description -ForegroundColor Gray
}

function Add-Warning {
    param(
        [string]$Category,
        [string]$File,
        [int]$Line,
        [string]$Warning,
        [string]$Severity = "Warning"
    )
    
    $script:WarningCount++
    $script:WarningCategories[$Category] = ($script:WarningCategories[$Category] ?? 0) + 1
    
    if ($Severity -eq "Error") {
        Write-Host "  ❌ Line $Line`: $Warning" -ForegroundColor Red
    } else {
        Write-Host "  ⚠️  Line $Line`: $Warning" -ForegroundColor Yellow
    }
}

function Start-WarningAnalysis {
    Write-Host "Starting TiXL Warning Analysis..." -ForegroundColor Green
    
    # Check if files exist
    if (-not $ProjectPath -and -not (Test-Path $SolutionPath)) {
        throw "Solution file not found at: $SolutionPath"
    }
    
    if ($ProjectPath -and -not (Test-Path $ProjectPath)) {
        throw "Project file not found at: $ProjectPath"
    }
    
    # Determine what to analyze
    $targets = @()
    if ($ProjectPath) {
        $targets += $ProjectPath
    } else {
        $solution = Get-Content $SolutionPath -Raw
        $projects = [regex]::Matches($solution, 'Project\(".*?"\)\s*=\s*"([^"]+)"\s*,\s*"([^"]+)"')
        foreach ($project in $projects) {
            $projectName = $project.Groups[1].Value
            $projectFile = $project.Groups[2].Value
            if (Test-Path $projectFile) {
                $targets += $projectFile
            }
        }
    }
    
    Write-Host "Found $($targets.Count) projects to analyze" -ForegroundColor Yellow
    
    # Analyze each project
    foreach ($target in $targets) {
        Write-ProgressSafe "Analyzing $(Split-Path $target -Leaf)"
        Analyze-ProjectWarnings $target
    }
    
    # Generate summary
    Generate-Summary
}

function Analyze-ProjectWarnings {
    param([string]$ProjectPath)
    
    Write-Host "`n### Analyzing: $(Split-Path $ProjectPath -Leaf)" -ForegroundColor Magenta
    
    # Get all C# files in the project directory
    $projectDir = Split-Path $ProjectPath -Parent
    $csFiles = Get-ChildItem -Path $projectDir -Recurse -Filter "*.cs" -File
    
    Write-Host "Found $($csFiles.Count) C# files" -ForegroundColor Gray
    
    # Analyze each file
    foreach ($file in $csFiles) {
        Write-ProgressSafe "Analyzing $($file.Name)"
        Analyze-FileWarnings $file.FullName
    }
    
    # Run build to catch compiler warnings
    if ($DetailedAnalysis) {
        Write-ProgressSafe "Running build analysis"
        Invoke-BuildAnalysis $ProjectPath
    }
}

function Analyze-FileWarnings {
    param([string]$FilePath)
    
    try {
        $content = Get-Content $FilePath -Raw -ErrorAction Stop
        $lines = $content -split "`n"
        
        $lineNum = 0
        foreach ($line in $lines) {
            $lineNum++
            
            # Check for common warning patterns
            
            # CS1591: Missing XML documentation
            if ($line -match '^\s*(public|protected)\s+(class|interface|struct|enum)\s+\w+' -or 
                $line -match '^\s*(public|protected|internal)\s+\w+\s+\w+\s*\(' -and $line -notmatch '///') {
                Add-Warning "Documentation" $FilePath $lineNum "Missing XML documentation"
            }
            
            # TODO comments without tracking
            if ($line -match 'TODO(?!\s*\([^)]+\))') {
                Add-Warning "TODO" $FilePath $lineNum "TODO comment should include tracking information"
            }
            
            # Potential null reference usage
            if ($line -match '\!\s*[a-zA-Z_][a-zA-Z0-9_]*\s*(?!\s*\?)') {
                Add-Warning "Nullability" $FilePath $lineNum "Potential null reference usage"
            }
            
            # Unused variables (simplified check)
            if ($line -match 'var\s+([a-zA-Z_][a-zA-Z0-9_]*)\s*=\s*[^;]+;') {
                $varName = $matches[1]
                $remainingContent = $lines[($lineNum)..$($lines.Count-1)] -join "`n"
                if ($remainingContent -notmatch $varName) {
                    Add-Warning "UnusedVariable" $FilePath $lineNum "Variable '$varName' might be unused"
                }
            }
            
            # Obsolete API usage
            if ($line -match '\[Obsolete\]' -or $line -match 'Thread\.Sleep' -or $line -match 'DateTime\.Now') {
                Add-Warning "ObsoleteAPI" $FilePath $lineLine "Potential use of obsolete API or deprecated pattern"
            }
            
            # Async without await
            if ($line -match 'async\s+\w+\s+\w+\s*\(' -and $lines[$lineNum] -notmatch 'await\s+') {
                Add-Warning "Async" $FilePath $lineNum "Async method might not need async keyword"
            }
        }
    }
    catch {
        Write-Host "Error analyzing $FilePath`: $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Invoke-BuildAnalysis {
    param([string]$ProjectPath)
    
    try {
        # Run build with detailed output
        $buildOutput = & dotnet build $ProjectPath --configuration Release --verbosity minimal 2>&1
        
        # Parse build output for warnings
        foreach ($line in $buildOutput) {
            if ($line -match 'warning\s+CS(\d+):\s*(.*)') {
                $warningCode = $matches[1]
                $warningMessage = $matches[2]
                Add-Warning "Compiler" $ProjectPath 0 "CS$warningCode`: $warningMessage"
            }
        }
    }
    catch {
        Write-Host "Build analysis failed: $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Apply-Fixes {
    param([string]$FilePath)
    
    if (-not $FixMode) { return }
    
    try {
        $content = Get-Content $FilePath -Raw
        $originalContent = $content
        
        # Fix TODO comments
        $content = [regex]::Replace($content, 'TODO(?!\s*\([^)]+\))', 'TODO: Add tracking information')
        
        # Fix simple nullability issues
        $content = [regex]::Replace($content, '(\w+)\s+(\w+)\s*=\s*null;', '$1? $2 = null;')
        
        # Save if changes were made
        if ($content -ne $originalContent) {
            $content | Set-Content $FilePath -Encoding UTF8
            $script:FixedWarnings++
            Write-Host "Applied automatic fixes to $($FilePath)" -ForegroundColor Green
        }
    }
    catch {
        Write-Host "Failed to apply fixes to $FilePath`: $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Generate-Summary {
    Write-Host "`n## Summary" -ForegroundColor Cyan
    
    Write-Host "Total warnings found: $script:WarningCount" -ForegroundColor $(if ($script:WarningCount -eq 0) { "Green" } else { "Red" })
    
    if ($script:WarningCategories.Count -gt 0) {
        Write-Host "`nWarning categories:" -ForegroundColor Gray
        foreach ($category in $script:WarningCategories.Keys) {
            $count = $script:WarningCategories[$category]
            $emoji = switch ($category) {
                "Documentation" { "📚" }
                "Nullability" { "🔍" }
                "UnusedVariable" { "🗑️" }
                "ObsoleteAPI" { "⚠️" }
                "Async" { "⚡" }
                "Compiler" { "🔧" }
                default { "📋" }
            }
            Write-Host "  $emoji $category`: $count" -ForegroundColor Yellow
        }
    }
    
    if ($FixMode -and $script:FixedWarnings -gt 0) {
        Write-Host "`nAutomatic fixes applied: $script:FixedWarnings" -ForegroundColor Green
    }
    
    # Generate detailed report
    if ($OutputPath) {
        Generate-ReportFile
    }
}

function Generate-ReportFile {
    $report = @"
# TiXL Warning Analysis Report

**Generated:** $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss UTC')
**Total Warnings:** $script:WarningCount

## Warning Categories

"@
    
    foreach ($category in $script:WarningCategories.Keys) {
        $count = $script:WarningCategories[$category]
        $report += "### $category` ($count warnings)`n`n"
    }
    
    if ($script:WarningCount -eq 0) {
        $report += "🎉 **Excellent!** No warnings found in the codebase.`n`n"
    } else {
        $report += "Please review and fix the warnings listed above.`n`n"
    }
    
    $report += @"
## Next Steps

1. Review the warning categories above
2. Prioritize fixes based on severity and impact
3. Use the code examples in the main documentation for reference
4. Re-run this analysis after making fixes

## Getting Help

If you need assistance with specific warnings, refer to:
- [TiXL Warning Resolution Guide](build_warnings_resolution.md)
- Project documentation
- Team coding standards
"@
    
    $report | Out-File -FilePath $OutputPath -Encoding UTF8
    Write-Host "`nDetailed report saved to: $OutputPath" -ForegroundColor Green
}

# Main execution
try {
    Start-WarningAnalysis
    
    # Exit with error code if warnings found
    if ($script:WarningCount -gt 0) {
        exit 1
    }
} catch {
    Write-Host "Analysis failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 2
} finally {
    if ($ShowProgress) {
        Write-Progress -Activity "TiXL Warning Analysis" -Completed
    }
}
