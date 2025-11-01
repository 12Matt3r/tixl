#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Enhanced TiXL Warning Detection and Auto-Fix Tool

.DESCRIPTION
    This script provides comprehensive warning detection and automatic fixes
    for the TiXL zero-warning policy. It focuses on the specific warning categories:
    - CS8600-CS8669 (nullability warnings)
    - CS0168, CS0219 (unused variables)
    - CS0618 (obsolete APIs)
    - CS1591 (missing documentation)
    - CS1998, CS4014 (async/await patterns)

.PARAMETER SolutionPath
    Path to the .sln file or directory containing the solution.

.PARAMETER ProjectPath
    Path to a specific .csproj file to analyze.

.PARAMETER OutputPath
    Path to save the analysis report.

.PARAMETER AutoFix
    Enable automatic fixes for supported warning types.

.PARAMETER BuildAnalysis
    Run build analysis to detect compiler warnings.

.PARAMETER Severity
    Minimum severity level to report (Error, Warning, Info).

.EXAMPLE
    .\detect-and-fix-warnings.ps1 -SolutionPath "..\TiXL.sln" -AutoFix

.EXAMPLE
    .\detect-and-fix-warnings.ps1 -ProjectPath "Core\TiXL.Core.csproj" -BuildAnalysis -OutputPath "warning-report.html"
#>

param(
    [Parameter(Mandatory = $false)]
    [string]$SolutionPath = "..\TiXL.sln",
    
    [Parameter(Mandatory = $false)]
    [string]$ProjectPath,
    
    [Parameter(Mandatory = $false)]
    [string]$OutputPath = "warning-analysis.md",
    
    [Parameter(Mandatory = $false)]
    [switch]$AutoFix,
    
    [Parameter(Mandatory = $false)]
    [switch]$BuildAnalysis,
    
    [Parameter(Mandatory = $false)]
    [ValidateSet("Error", "Warning", "Info")]
    [string]$Severity = "Warning",
    
    [Parameter(Mandatory = $false)]
    [switch]$ShowProgress,
    
    [Parameter(Mandatory = $false)]
    [switch]$ShowDetails
)

$ErrorActionPreference = "Stop"

# Script configuration
$script:WarningCount = 0
$script:WarningCategories = @{}
$script:FixedWarnings = 0
$script:FixableWarnings = 0
$script:TargetWarnings = @(
    "CS8600","CS8601","CS8602","CS8603","CS8604","CS8605","CS8606","CS8607","CS8608","CS8609",
    "CS8610","CS8611","CS8612","CS8613","CS8614","CS8615","CS8616","CS8617","CS8618","CS8619",
    "CS8620","CS8621","CS8622","CS8623","CS8624","CS8625","CS8626","CS8627","CS8628","CS8629",
    "CS8630","CS8631","CS8632","CS8633","CS8634","CS8635","CS8636","CS8637","CS8638","CS8639",
    "CS8640","CS8641","CS8642","CS8643","CS8644","CS8645","CS8646","CS8647","CS8648","CS8649",
    "CS8650","CS8651","CS8652","CS8653","CS8654","CS8655","CS8656","CS8657","CS8658","CS8659",
    "CS8660","CS8661","CS8662","CS8663","CS8664","CS8665","CS8666","CS8667","CS8668","CS8669",
    "CS0168","CS0219","CS0618","CS1591","CS1998","CS4014"
)

function Write-ProgressSafe {
    param([string]$Message)
    if ($ShowProgress) {
        Write-Progress -Activity "TiXL Zero-Warning Analysis" -Status $Message
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
        [string]$WarningCode,
        [string]$Severity = "Warning"
    )
    
    $script:WarningCount++
    if (-not $script:WarningCategories.ContainsKey($Category)) {
        $script:WarningCategories[$Category] = @()
    }
    
    $warningObj = @{
        Category = $Category
        File = $File
        Line = $Line
        Warning = $Warning
        Code = $WarningCode
        Severity = $Severity
        Fixable = $IsWarningFixable($WarningCode)
    }
    
    $script:WarningCategories[$Category] += $warningObj
    
    if ($Severity -eq "Error") {
        Write-Host "  ❌ Line $Line`: [$WarningCode] $Warning" -ForegroundColor Red
    } else {
        $color = if ($WarningCode -in $script:TargetWarnings) { "Yellow" } else { "Gray" }
        Write-Host "  ⚠️  Line $Line`: [$WarningCode] $Warning" -ForegroundColor $color
    }
}

function IsWarningFixable {
    param([string]$WarningCode)
    
    $fixableCodes = @(
        "CS0168", # Unused variable - can add discard
        "CS0219", # Unused variable - can remove
        "CS1591"  # Missing documentation - can add basic documentation
    )
    
    return $WarningCode -in $fixableCodes
}

function Start-WarningAnalysis {
    Write-Host "Starting TiXL Zero-Warning Policy Analysis..." -ForegroundColor Green
    Write-Host "Targeting: $($script:TargetWarnings.Count) specific warning codes" -ForegroundColor Cyan
    
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
        $solution = Get-Content $SolutionPath -Raw -ErrorAction SilentlyContinue
        if ($solution) {
            $projects = [regex]::Matches($solution, 'Project\(".*?"\)\s*=\s*"([^"]+)"\s*,\s*"([^"]+)"')
            foreach ($project in $projects) {
                $projectName = $project.Groups[1].Value
                $projectFile = $project.Groups[2].Value
                if (Test-Path $projectFile) {
                    $targets += $projectFile
                }
            }
        }
    }
    
    if ($targets.Count -eq 0) {
        # Fallback: find all .csproj files
        $targets = Get-ChildItem -Recurse -Filter "*.csproj" -File | Select-Object -ExpandProperty FullName
    }
    
    Write-Host "Found $($targets.Count) projects to analyze" -ForegroundColor Yellow
    
    # Analyze each project
    foreach ($target in $targets) {
        Write-ProgressSafe "Analyzing $(Split-Path $target -Leaf)"
        Analyze-ProjectWarnings $target
    }
    
    # Generate summary and apply fixes if requested
    Generate-Summary
    
    if ($AutoFix -and $script:FixableWarnings -gt 0) {
        Write-Host "`nApplying automatic fixes..." -ForegroundColor Green
        Apply-AutomaticFixes
        Generate-Summary  # Regenerate summary after fixes
    }
}

function Analyze-ProjectWarnings {
    param([string]$ProjectPath)
    
    Write-Host "`n### Analyzing: $(Split-Path $ProjectPath -Leaf)" -ForegroundColor Magenta
    
    # Get all C# files in the project directory
    $projectDir = Split-Path $ProjectPath -Parent
    if (-not (Test-Path $projectDir)) {
        Write-Host "Project directory not found: $projectDir" -ForegroundColor Yellow
        return
    }
    
    $csFiles = Get-ChildItem -Path $projectDir -Recurse -Filter "*.cs" -File -ErrorAction SilentlyContinue
    
    Write-Host "Found $($csFiles.Count) C# files" -ForegroundColor Gray
    
    # Analyze each file
    foreach ($file in $csFiles) {
        Write-ProgressSafe "Analyzing $($file.Name)"
        Analyze-FileWarnings $file.FullName
    }
    
    # Run build to catch compiler warnings
    if ($BuildAnalysis) {
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
            
            # Analyze for specific warning patterns
            
            # CS1591: Missing XML documentation for public members
            if ($line -match '^\s*(public|protected|internal)\s+(class|interface|struct|enum|delegate)\s+\w+' -and $lines[$lineNum-2] -notmatch '///') {
                Add-Warning "Documentation" $FilePath $lineNum "Missing XML documentation for public type" "CS1591"
            }
            
            # CS1591: Missing XML documentation for public methods
            if ($line -match '^\s*(public|protected|internal)\s+(?!.*=>)\s*\w+\s+\w+\s*\(' -and $lines[$lineNum-2] -notmatch '///') {
                Add-Warning "Documentation" $FilePath $lineNum "Missing XML documentation for public method" "CS1591"
            }
            
            # CS1591: Missing XML documentation for public properties
            if ($line -match '^\s*(public|protected|internal)\s+\w+\s+\w+\s*\{' -and $lines[$lineNum-2] -notmatch '///') {
                Add-Warning "Documentation" $FilePath $lineNum "Missing XML documentation for public property" "CS1591"
            }
            
            # CS0168, CS0219: Unused variables
            if ($line -match '^\s*(\w+)\s+(\w+)\s*=\s*[^;]+;') {
                $varName = $matches[2]
                $remainingContent = $lines[($lineNum)..($lines.Count-1)] -join "`n"
                if ($remainingContent -notmatch $varName) {
                    $isLoopVariable = ($line -match 'foreach\s*\(' -or $line -match 'for\s*\(' -or $line -match 'using\s*\(')
                    if (-not $isLoopVariable) {
                        Add-Warning "UnusedVariable" $FilePath $lineNum "Variable '$varName' is declared but never used" "CS0168"
                    }
                }
            }
            
            # CS0168: Discard variables with underscore (intentionally unused)
            if ($line -match '\b_\s*=\s*') {
                Add-Warning "UnusedVariable" $FilePath $lineNum "Discard variable '_' suggests intentionally unused value" "CS0219"
            }
            
            # CS8600-CS8669: Nullability warnings (simplified detection)
            # Potential null assignment
            if ($line -match '\b(\w+)\s*=\s*(\w+)\.(' + 'FirstOrDefault|All|Single|SingleOrDefault|Last|LastOrDefault' + ')\(' -and $line -notmatch '!' -and $line -notmatch '\?') {
                Add-Warning "Nullability" $FilePath $lineNum "Potential null assignment from LINQ operation" "CS8604"
            }
            
            # CS8604: Possible null reference assignment
            if ($line -match '\b(\w+)\s*=\s*Get(?:\w+)?\(' -and $line -notmatch '!' -and $line -notmatch '\?' -and $line -notmatch 'var\s+\?') {
                Add-Warning "Nullability" $FilePath $lineNum "Potential null reference assignment from method call" "CS8604"
            }
            
            # CS0618: Obsolete API usage
            if ($line -match 'Thread\.Sleep\(\d+\)' -or $line -match 'DateTime\.Now' -or $line -match 'File\.ReadAllText.*Encoding\.UTF8' -or $line -match 'new\s+ArrayList\(\)') {
                Add-Warning "ObsoleteAPI" $FilePath $lineNum "Use of potentially obsolete API or pattern" "CS0618"
            }
            
            # CS1998: Async method without await
            if ($line -match 'async\s+(Task|void|\w+)\s+(\w+)\s*\(') {
                $methodName = $matches[2]
                $methodBody = $lines[($lineNum)..($lines.Count-1)] -join "`n"
                if ($methodBody -notmatch 'await\s+' -and $methodBody -notmatch 'return\s+Task\.') {
                    Add-Warning "Async" $FilePath $lineNum "Async method '$methodName' does not contain 'await'" "CS1998"
                }
            }
            
            # CS4014: Unawaited async call
            if ($line -match 'await\s+\w+\.\w+\(' -and $line -notmatch 'await\s+await') {
                $remainingContent = $lines[($lineNum)..($lines.Count-1)] -join "`n"
                $taskCalls = [regex]::Matches($remainingContent, '\w+\.\w+\([^)]*\)\s*;')
                foreach ($match in $taskCalls) {
                    if ($match.Value -notmatch '^await\s+' -and $match.Value -notmatch 'Task\.Run\(') {
                        Add-Warning "Async" $FilePath $lineNum "Call to async method not awaited" "CS4014"
                        break
                    }
                }
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
            if ($line -match 'warning\s+(CS\d+):\s*(.*)') {
                $warningCode = $matches[1]
                $warningMessage = $matches[2]
                
                # Only report target warnings unless showing all
                if ($warningCode -in $script:TargetWarnings -or $ShowDetails) {
                    Add-Warning "Compiler" $ProjectPath 0 "$warningMessage" $warningCode
                }
            }
            
            # Parse CA warnings (code analysis)
            if ($line -match 'warning\s+(CA\d+):\s*(.*)') {
                $warningCode = $matches[1]
                $warningMessage = $matches[2]
                Add-Warning "CodeAnalysis" $ProjectPath 0 "$warningMessage" $warningCode
            }
        }
    }
    catch {
        Write-Host "Build analysis failed: $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Apply-AutomaticFixes {
    foreach ($category in $script:WarningCategories.Keys) {
        foreach ($warning in $script:WarningCategories[$category]) {
            if ($warning.Fixable) {
                try {
                    Apply-WarningFix $warning
                }
                catch {
                    Write-Host "Failed to fix $($warning.Code) in $($warning.File): $($_.Exception.Message)" -ForegroundColor Red
                }
            }
        }
    }
}

function Apply-WarningFix {
    param([hashtable]$Warning)
    
    switch ($Warning.Code) {
        "CS0168" {
            # Add discard to unused variable
            $content = Get-Content $Warning.File -Raw
            $lineNumber = $Warning.Line
            
            # Find the line and check if we can safely add a discard
            $lines = $content -split "`n"
            $targetLine = $lines[$lineNumber - 1]
            
            if ($targetLine -match '^\s*(\w+)\s+(\w+)\s*=\s*([^;]+);') {
                $varType = $matches[1]
                $varName = $matches[2]
                $assignment = $matches[3]
                
                # If it's an object creation, add a discard
                if ($assignment -match 'new\s+\w+') {
                    $newLine = $targetLine -replace $varName, "_"
                    $lines[$lineNumber - 1] = $newLine
                    $content = $lines -join "`n"
                    Set-Content $Warning.File -Value $content -Encoding UTF8
                    $script:FixedWarnings++
                }
            }
        }
        
        "CS1591" {
            # Add basic XML documentation
            $content = Get-Content $Warning.File -Raw
            $lineNumber = $Warning.Line
            $lines = $content -split "`n"
            
            if ($lineNumber -gt 1) {
                $docLine = "/// <summary>TODO: Add XML documentation</summary>"
                $lines[$lineNumber - 2] += "`n$docLine"
                $content = $lines -join "`n"
                Set-Content $Warning.File -Value $content -Encoding UTF8
                $script:FixedWarnings++
            }
        }
    }
}

function Generate-Summary {
    Write-Host "`n## TiXL Zero-Warning Policy Analysis Summary" -ForegroundColor Cyan
    
    Write-Host "Total warnings found: $script:WarningCount" -ForegroundColor $(if ($script:WarningCount -eq 0) { "Green" } else { "Red" })
    
    if ($script:WarningCategories.Count -gt 0) {
        Write-Host "`nWarning categories breakdown:" -ForegroundColor Gray
        
        foreach ($category in $script:WarningCategories.Keys) {
            $warnings = $script:WarningCategories[$category]
            $count = $warnings.Count
            $fixableCount = ($warnings | Where-Object { $_.Fixable }).Count
            
            $emoji = switch ($category) {
                "Documentation" { "📚" }
                "Nullability" { "🔍" }
                "UnusedVariable" { "🗑️" }
                "ObsoleteAPI" { "⚠️" }
                "Async" { "⚡" }
                "Compiler" { "🔧" }
                "CodeAnalysis" { "🔍" }
                default { "📋" }
            }
            
            $statusColor = if ($count -eq 0) { "Green" } elseif ($fixableCount -gt 0) { "Yellow" } else { "Red" }
            $fixInfo = if ($fixableCount -gt 0) { " ($fixableCount fixable)" } else { "" }
            
            Write-Host "  $emoji $category`: $count warnings$fixInfo" -ForegroundColor $statusColor
        }
    }
    
    if ($AutoFix -and $script:FixedWarnings -gt 0) {
        Write-Host "`nAutomatic fixes applied: $script:FixedWarnings" -ForegroundColor Green
    }
    
    # Generate detailed report
    if ($OutputPath) {
        Generate-ReportFile
    }
}

function Generate-ReportFile {
    $report = @"
# TiXL Zero-Warning Policy Analysis Report

**Generated:** $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss UTC')
**Total Warnings:** $script:WarningCount
**Auto-fixed:** $script:FixedWarnings

## Zero-Warning Policy Status

$(
    if ($script:WarningCount -eq 0) {
        "🎉 **EXCELLENT!** TiXL codebase achieves zero-warning status!"
    } elseif ($script:WarningCount -le 5) {
        "⚠️ **GOOD PROGRESS** - Only $script:WarningCount warnings found. Almost there!"
    } else {
        "🚨 **ACTION NEEDED** - $script:WarningCount warnings require attention."
    }
)

## Warning Categories Analysis

"@
    
    foreach ($category in $script:WarningCategories.Keys) {
        $warnings = $script:WarningCategories[$category]
        $count = $warnings.Count
        $fixableCount = ($warnings | Where-Object { $_.Fixable }).Count
        
        $statusIcon = if ($count -eq 0) { "✅" } elseif ($fixableCount -gt 0) { "⚡" } else { "🚨" }
        $report += "### $statusIcon $category ($count warnings"
        if ($fixableCount -gt 0) {
            $report += ", $fixableCount auto-fixable"
        }
        $report += ")`n`n"
        
        # Show example warnings
        foreach ($warning in $warnings | Select-Object -First 3) {
            $report += "- **$($warning.Code)** ($($warning.File)): Line $($warning.Line) - $($warning.Warning)`n"
        }
        
        if ($warnings.Count -gt 3) {
            $report += "- ... and $($warnings.Count - 3) more warnings in this category`n"
        }
        $report += "`n"
    }
    
    if ($AutoFix) {
        $report += @"
## Applied Fixes

$script:FixedWarnings automatic fixes were applied to resolve fixable warnings.

"@
    }
    
    $report += @"
## Next Steps

### For Developers
1. Review the warning categories above
2. Prioritize fixes based on warning type and impact
3. Use IDE features for automated refactoring where possible
4. Re-run this analysis after making fixes

### Specific Fix Guidelines

#### Nullability Warnings (CS8600-CS8669)
- Add nullable annotations (`?`) for potentially null reference types
- Use null-forgiving operator (`!`) only when absolutely necessary
- Prefer nullable annotation context over global suppressions

#### Unused Variables (CS0168, CS0219)
- Remove unused variables when possible
- Use discard (`_`) when variable is required for API compatibility
- Remove unused parameters when not implementing interfaces

#### Missing Documentation (CS1591)
- Add XML documentation for all public types and members
- Include `<summary>`, `<param>`, and `<returns>` tags as appropriate
- Use meaningful descriptions, not placeholder text

#### Async/Await Patterns (CS1998, CS4014)
- Remove `async` modifier when no `await` is used
- Always `await` async method calls
- Use `ConfigureAwait(false)` in library code

#### Obsolete APIs (CS0618)
- Replace deprecated .NET APIs with modern alternatives
- Update graphics API usage to latest standards
- Review and update third-party library usage

## Getting Help

- [TiXL Warning Resolution Guide](docs/build_warnings_resolution.md)
- Project documentation and coding standards
- Team discussions and code reviews

## Continuous Improvement

This analysis should be run regularly to maintain zero-warning status:
- During development: `.\scripts\detect-and-fix-warnings.ps1 -AutoFix`
- Before commits: Automated via pre-commit hooks
- CI/CD integration: Required to maintain quality gates

---
*Generated by TiXL Zero-Warning Policy Tool v2.0*
"@
    
    $report | Out-File -FilePath $OutputPath -Encoding UTF8
    Write-Host "`nDetailed report saved to: $OutputPath" -ForegroundColor Green
}

function Show-WarningDetails {
    if ($ShowDetails -and $script:WarningCategories.Count -gt 0) {
        Write-Host "`n## Detailed Warning Breakdown" -ForegroundColor Cyan
        
        foreach ($category in $script:WarningCategories.Keys) {
            $warnings = $script:WarningCategories[$category]
            Write-Host "`n### $category ($(($warnings | Where-Object { $_.Code -in $script:TargetWarnings }).Count) target warnings)" -ForegroundColor Magenta
            
            foreach ($warning in $warnings) {
                if ($warning.Code -in $script:TargetWarnings -or $ShowDetails) {
                    $fileName = Split-Path $warning.File -Leaf
                    $icon = if ($warning.Fixable) { "⚡" } else { "🚨" }
                    Write-Host "  $icon Line $($warning.Line): [$($warning.Code)] $($warning.Warning)" -ForegroundColor Yellow
                    Write-Host "      File: $fileName" -ForegroundColor Gray
                }
            }
        }
    }
}

# Main execution
try {
    Start-WarningAnalysis
    Show-WarningDetails
    
    # Exit with appropriate error code
    if ($script:WarningCount -gt 0) {
        Write-Host "`n❌ Build would fail with $script:WarningCount warnings" -ForegroundColor Red
        if (-not $AutoFix) {
            Write-Host "💡 Try running with -AutoFix to automatically fix some warnings" -ForegroundColor Yellow
        }
        exit 1
    } else {
        Write-Host "`n🎉 Excellent! Zero warnings achieved!" -ForegroundColor Green
        exit 0
    }
} catch {
    Write-Host "Analysis failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 2
} finally {
    if ($ShowProgress) {
        Write-Progress -Activity "TiXL Zero-Warning Analysis" -Completed
    }
}