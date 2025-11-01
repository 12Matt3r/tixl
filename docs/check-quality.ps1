# TiXL Enhanced Code Quality Checker
# Analyzes C# projects for comprehensive quality metrics

param(
    [Parameter(Mandatory=$false)]
    [string]$SolutionPath = "..\TiXL.sln",
    
    [Parameter(Mandatory=$false)]
    [string]$ProjectPath,
    
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "quality-analysis.md",
    
    [Parameter(Mandatory=$false)]
    [string]$BuildConfiguration = "Release",
    
    [Parameter(Mandatory=$false)]
    [switch]$DetailedAnalysis,
    
    [Parameter(Mandatory=$false)]
    [switch]$FixMode,
    
    [Parameter(Mandatory=$false)]
    [switch]$ShowProgress
)

$ErrorActionPreference = "Stop"

# Global variables for analysis
$script:TotalFiles = 0
$script:TotalLines = 0
$script:TotalMethods = 0
$script:TotalClasses = 0
$script:TotalComplexity = 0
$script:QualityIssues = @{}
$script:PerformanceIssues = @{}
$script:SecurityIssues = @{}

function Write-ProgressSafe {
    param([string]$Message)
    if ($ShowProgress) {
        Write-Progress -Activity "TiXL Quality Analysis" -Status $Message
    }
}

function Write-QualitySection {
    param([string]$Title, [string]$Description, [string]$Color = "Cyan")
    Write-Host "`n## $Title" -ForegroundColor $Color
    Write-Host $Description -ForegroundColor Gray
}

function Add-QualityIssue {
    param(
        [string]$Category,
        [string]$Severity,
        [string]$File,
        [int]$Line,
        [string]$Issue,
        [string]$Recommendation = ""
    )
    
    $issue = @{
        Category = $Category
        Severity = $Severity
        File = $File
        Line = $Line
        Issue = $Issue
        Recommendation = $Recommendation
    }
    
    switch ($Category) {
        "Quality" { $script:QualityIssues[$Category] = ($script:QualityIssues[$Category] ?? 0) + 1 }
        "Performance" { $script:PerformanceIssues[$Category] = ($script:PerformanceIssues[$Category] ?? 0) + 1 }
        "Security" { $script:SecurityIssues[$Category] = ($script:SecurityIssues[$Category] ?? 0) + 1 }
    }
    
    $emoji = switch ($Severity) {
        "Error" { "❌" }
        "Warning" { "⚠️" }
        "Info" { "ℹ️" }
        "Critical" { "🔥" }
    }
    
    Write-Host "  $emoji Line $Line`: $Issue" -ForegroundColor $(switch ($Severity) { "Error" { "Red" } "Warning" { "Yellow" } "Critical" { "Magenta" } "Info" { "Cyan" } })
    if ($Recommendation -and $Recommendation -ne "") {
        Write-Host "    → $Recommendation" -ForegroundColor Gray
    }
}

function Start-ComprehensiveAnalysis {
    Write-Host "🚀 Starting TiXL Comprehensive Quality Analysis..." -ForegroundColor Green
    
    # Validate input
    if (-not $ProjectPath -and -not (Test-Path $SolutionPath)) {
        throw "Solution file not found at: $SolutionPath"
    }
    
    if ($ProjectPath -and -not (Test-Path $ProjectPath)) {
        throw "Project file not found at: $ProjectPath"
    }
    
    # Determine analysis targets
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
        Analyze-ProjectQuality $target
    }
    
    # Generate comprehensive report
    Generate-QualityReport
    
    # Return overall status
    $totalIssues = $script:QualityIssues.Count + $script:PerformanceIssues.Count + $script:SecurityIssues.Count
    if ($totalIssues -gt 0) {
        return "ISSUES_FOUND"
    } else {
        return "PASSED"
    }
}

function Analyze-ProjectQuality {
    param([string]$ProjectPath)
    
    Write-Host "`n### Analyzing: $(Split-Path $ProjectPath -Leaf)" -ForegroundColor Magenta
    
    # Get project directory
    $projectDir = Split-Path $ProjectPath -Parent
    $csFiles = Get-ChildItem -Path $projectDir -Recurse -Filter "*.cs" -File
    
    Write-Host "Found $($csFiles.Count) C# files to analyze" -ForegroundColor Gray
    $script:TotalFiles += $csFiles.Count
    
    # Analyze each file
    foreach ($file in $csFiles) {
        Write-ProgressSafe "Analyzing $($file.Name)"
        Analyze-FileQuality $file.FullName
    }
    
    # Run build analysis for additional insights
    if ($DetailedAnalysis) {
        Write-ProgressSafe "Running build analysis"
        Invoke-BuildQualityAnalysis $ProjectPath
    }
}

function Analyze-FileQuality {
    param([string]$FilePath)
    
    try {
        $content = Get-Content $FilePath -Raw -ErrorAction Stop
        $lines = $content -split "`n"
        $script:TotalLines += $lines.Count
        
        $lineNum = 0
        foreach ($line in $lines) {
            $lineNum++
            
            # Quality Analysis
            Analyze-QualityIssues $FilePath $lineNum $line
            
            # Performance Analysis
            Analyze-PerformanceIssues $FilePath $lineNum $line
            
            # Security Analysis
            Analyze-SecurityIssues $FilePath $lineNum $line
            
            # Complexity Analysis
            Analyze-ComplexityIssues $FilePath $lineNum $line $lines
        }
    }
    catch {
        Write-Host "Error analyzing $FilePath`: $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Analyze-QualityIssues {
    param([string]$FilePath, [int]$LineNum, [string]$Line)
    
    # Missing XML documentation
    if ($line -match '^\s*(public|protected|internal)\s+(class|interface|struct|enum)\s+\w+' -or 
        $line -match '^\s*(public|protected|internal)\s+\w+\s+\w+\s*\(' -and $line -notmatch '///') {
        Add-QualityIssue "Quality" "Warning" $FilePath $LineNum "Missing XML documentation" "Add XML documentation comments for public members"
    }
    
    # TODO comments without tracking
    if ($line -match 'TODO(?!\s*\([^)]+\))') {
        Add-QualityIssue "Quality" "Warning" $FilePath $LineNum "TODO comment without tracking information" "Add issue number or task ID to TODO comments"
    }
    
    # Dead code and unused variables
    if ($line -match 'var\s+([a-zA-Z_][a-zA-Z0-9_]*)\s*=\s*[^;]+;') {
        $varName = $matches[1]
        # This is a simplified check - in real scenario, would need full AST analysis
        Add-QualityIssue "Quality" "Info" $FilePath $LineNum "Variable '$varName' declared" "Consider using explicit type for better readability"
    }
    
    # Magic numbers in real-time code
    if ($line -match '\b(60|120|144|30|1000)\b' -and $FilePath -match '(Graphics|Render|Engine|Game)') {
        Add-QualityIssue "Quality" "Warning" $FilePath $LineNum "Magic number detected" "Use named constants for magic numbers in graphics code"
    }
    
    # Long methods (potential complexity issue)
    $fileContent = Get-Content $FilePath -Raw
    $methodStart = $line
    if ($methodStart -match '^\s*(public|private|protected)\s+\w+\s+\w+\s*\(') {
        $methodName = $matches[2]
        # Count lines until method ends
        $methodLines = 1
        $braceCount = ($line | Select-String -Pattern '[{}]' -AllMatches).Matches.Count
        for ($i = $lineNum; $i -lt $lines.Count; $i++) {
            $methodLines++
            $braceCount += ($lines[$i] | Select-String -Pattern '[{}]' -AllMatches).Matches.Count
            if ($braceCount -eq 0) { break }
        }
        
        if ($methodLines -gt 50) {
            Add-QualityIssue "Quality" "Warning" $FilePath $LineNum "Method '$methodName' has $methodLines lines" "Consider breaking down large methods (>$50 lines)"
        }
    }
    
    # Inconsistent naming conventions
    if ($line -match 'public\s+(class|interface|struct)\s+[a-z]' -or 
        $line -match 'public\s+\w+\s+[a-z]') {
        Add-QualityIssue "Quality" "Error" $FilePath $LineNum "Inconsistent naming convention" "Use PascalCase for public types and members"
    }
}

function Analyze-PerformanceIssues {
    param([string]$FilePath, [int]$LineNum, [string]$Line)
    
    # Potential memory allocations in loops
    if ($line -match '\bforeach\s*\(' -and $FilePath -match '(Render|Update|Draw)') {
        Add-QualityIssue "Performance" "Warning" $FilePath $LineNum "Potential performance issue in graphics context" "Consider avoiding allocations in render/update loops"
    }
    
    # String concatenation in loops
    if ($line -match '\+\s*=' -and $FilePath -match '(Render|Update|Draw)') {
        Add-QualityIssue "Performance" "Warning" $FilePath $LineNum "String concatenation pattern" "Use StringBuilder for multiple string operations"
    }
    
    # New object creation in render methods
    if ($line -match 'new\s+\w+\s*\(' -and $FilePath -match '(Render|Update|Draw|Graphics)') {
        Add-QualityIssue "Performance" "Error" $FilePath $LineNum "Object allocation in render context" "Avoid 'new' allocations in render/update methods"
    }
    
    # Potential blocking calls
    if ($line -match '\b(Thread\.Sleep|Task\.Delay|Delay)\b' -and $FilePath -match '(Game|Engine|Render)') {
        Add-QualityIssue "Performance" "Critical" $FilePath $LineNum "Blocking call in real-time context" "Avoid blocking calls in real-time graphics code"
    }
    
    # Expensive operations in constructors
    if ($line -match 'public\s+\w+Constructor\s*\(' -and ($Line -match 'File\.|Database\.|Http' -or $Line -match '\.Load\(')) {
        Add-QualityIssue "Performance" "Warning" $FilePath $LineNum "Expensive operation in constructor" "Consider lazy initialization for expensive operations"
    }
}

function Analyze-SecurityIssues {
    param([string]$FilePath, [int]$LineNum, [string]$Line)
    
    # Hardcoded passwords or keys
    if ($line -match '(password|passwd|pwd|secret|key|token)\s*=\s*["''][^"'']+["'']' -or
        $line -match '(api_key|secret_key|private_key)\s*=') {
        Add-QualityIssue "Security" "Critical" $FilePath $LineNum "Potential hardcoded secret" "Move secrets to secure configuration or environment variables"
    }
    
    # Insecure file operations
    if ($line -match '\.ReadAllText\(' -or $line -match '\.ReadAllBytes\(') {
        Add-QualityIssue "Security" "Warning" $FilePath $LineNum "Potential insecure file operation" "Validate file paths and use secure file access methods"
    }
    
    # HTTP usage (insecure)
    if ($line -match 'http://' -and $line -notmatch 'localhost|127\.0\.0\.1') {
        Add-QualityIssue "Security" "Error" $FilePath $LineNum "Insecure HTTP protocol usage" "Use HTTPS for network communications"
    }
    
    # SQL injection potential
    if ($line -match 'string\.Format\s*\([^)]*\+[^)]*\)' -or $line -match '\bExecute\w*\([^)]*\+[^)]*\)') {
        Add-QualityIssue "Security" "Critical" $FilePath $LineNum "Potential SQL injection" "Use parameterized queries to prevent SQL injection"
    }
    
    # File path traversal
    if ($line -match '\.\.[\\/]' -or $line -match '(Directory\.GetFiles|File\.ReadAll)' -and $line -notmatch 'Path\.Combine') {
        Add-QualityIssue "Security" "Warning" $FilePath $LineNum "Potential path traversal vulnerability" "Validate and sanitize file paths"
    }
}

function Analyze-ComplexityIssues {
    param([string]$FilePath, [int]$LineNum, [string]$Line, [array]$AllLines)
    
    # High cyclomatic complexity
    $methodStart = $Line
    if ($methodStart -match '^\s*(public|private|protected)\s+\w+\s+\w+\s*\(') {
        $methodName = $matches[2]
        $complexity = 1
        
        # Count decision points
        for ($i = $LineNum - 1; $i -lt $AllLines.Count; $i++) {
            $currentLine = $AllLines[$i]
            
            # Decision points
            if ($currentLine -match '\b(if|else|while|for|foreach|case|catch|finally)\b') {
                $complexity++
            }
            if ($currentLine -match '[\?\&\|\:]') {
                $complexity++
            }
            
            # Check if method ends
            $openBraces = ($AllLines[($LineNum-1)..$i] | Select-String -Pattern '{' -AllMatches).Matches.Count
            $closeBraces = ($AllLines[($LineNum-1)..$i] | Select-String -Pattern '}' -AllMatches).Matches.Count
            
            if ($openBraces -eq $closeBraces -and $i -gt $LineNum) {
                break
            }
        }
        
        if ($complexity -gt 15) {
            Add-QualityIssue "Quality" "Error" $FilePath $LineNum "Method '$methodName' has high complexity ($complexity)" "Refactor to reduce complexity below 15"
            $script:TotalComplexity += $complexity
        } elseif ($complexity -gt 10) {
            Add-QualityIssue "Quality" "Warning" $FilePath $LineNum "Method '$methodName' has moderate complexity ($complexity)" "Consider refactoring to improve readability"
        }
        
        $script:TotalMethods++
    }
    
    # Count classes
    if ($Line -match '^\s*(public|internal|private|protected)?\s*(partial\s+)?(class|interface|struct|enum)') {
        $script:TotalClasses++
    }
}

function Invoke-BuildQualityAnalysis {
    param([string]$ProjectPath)
    
    try {
        Write-ProgressSafe "Running build analysis with detailed warnings"
        
        # Run build with all warnings enabled
        $buildOutput = & dotnet build $ProjectPath --configuration $BuildConfiguration --verbosity minimal 2>&1
        
        # Parse build output for warnings and errors
        foreach ($line in $buildOutput) {
            if ($line -match 'error\s+CS(\d+):\s*(.*)') {
                Add-QualityIssue "Build" "Error" $ProjectPath 0 "CS$($matches[1])`: $($matches[2])" "Fix compiler error"
            } elseif ($line -match 'warning\s+CS(\d+):\s*(.*)') {
                Add-QualityIssue "Build" "Warning" $ProjectPath 0 "CS$($matches[1])`: $($matches[2])" "Address compiler warning"
            }
        }
        
        # Run tests with coverage
        Write-ProgressSafe "Running tests with coverage analysis"
        $testOutput = & dotnet test $ProjectPath --configuration $BuildConfiguration --collect:"XPlat Code Coverage" --verbosity minimal 2>&1
        
        # Parse test output for coverage information
        foreach ($line in $testOutput) {
            if ($line -match 'Overall coverage:\s+(\d+\.\d+)%') {
                $coverage = [decimal]$matches[1]
                if ($coverage -lt 75) {
                    Add-QualityIssue "Quality" "Warning" $ProjectPath 0 "Code coverage is $coverage% (target: 75%)" "Increase test coverage"
                }
            }
        }
    }
    catch {
        Write-Host "Build analysis failed: $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Apply-QualityFixes {
    param([string]$FilePath)
    
    if (-not $FixMode) { return }
    
    try {
        $content = Get-Content $FilePath -Raw
        $originalContent = $content
        $fixesApplied = 0
        
        # Fix TODO comments without tracking
        $content = [regex]::Replace($content, 'TODO(?!\s*\([^)]+\))', 'TODO: Add tracking information')
        
        # Fix simple naming convention issues
        $content = [regex]::Replace($content, '(public\s+(class|interface|struct)\s+)([a-z])', { param($m) $m.Groups[1].Value + $m.Groups[3].Value.ToUpper() + $m.Groups[3].Value.Substring(1) }, 'IgnoreCase')
        
        # Save if changes were made
        if ($content -ne $originalContent) {
            $content | Set-Content $FilePath -Encoding UTF8
            $fixesApplied++
            Write-Host "Applied automatic fixes to $($FilePath)" -ForegroundColor Green
        }
        
        return $fixesApplied
    }
    catch {
        Write-Host "Failed to apply fixes to $FilePath`: $($_.Exception.Message)" -ForegroundColor Red
        return 0
    }
}

function Generate-QualityReport {
    Write-Host "`n## Generating Quality Report" -ForegroundColor Cyan
    
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss UTC'
    $totalIssues = $script:QualityIssues.Count + $script:PerformanceIssues.Count + $script:SecurityIssues.Count
    
    $report = @"
# TiXL Quality Analysis Report

**Generated:** $timestamp  
**Total Files Analyzed:** $script:TotalFiles  
**Total Lines of Code:** $script:TotalLines  
**Total Methods:** $script:TotalMethods  
**Total Classes:** $script:TotalClasses  

## Code Metrics Summary

"@
    
    if ($script:TotalMethods -gt 0) {
        $avgComplexity = [math]::Round($script:TotalComplexity / $script:TotalMethods, 2)
        $report += "**Average Method Complexity:** $avgComplexity`n"
    }
    
    $report += "`n## Quality Issues`n`n"
    
    if ($totalIssues -eq 0) {
        $report += "🎉 **Excellent!** No quality issues detected.`n`n"
    } else {
        $report += "**Total Issues Found:** $totalIssues`n`n"
        
        if ($script:QualityIssues.Count -gt 0) {
            $report += "### Quality Issues ($($script:QualityIssues.Count) issues)`n`n"
            $report += "Issues related to code quality, maintainability, and best practices.`n`n"
        }
        
        if ($script:PerformanceIssues.Count -gt 0) {
            $report += "### Performance Issues ($($script:PerformanceIssues.Count) issues)`n`n"
            $report += "Performance-related issues that may impact real-time graphics performance.`n`n"
        }
        
        if ($script:SecurityIssues.Count -gt 0) {
            $report += "### Security Issues ($($script:SecurityIssues.Count) issues)`n`n"
            $report += "Security vulnerabilities and potential risks in the codebase.`n`n"
        }
    }
    
    $report += @"
## Recommendations

### Immediate Actions
1. Fix all Critical and Error severity issues
2. Address Security vulnerabilities first
3. Fix Performance issues in render/update loops

### Code Quality Improvements
1. Add comprehensive XML documentation
2. Reduce method complexity through refactoring
3. Improve test coverage to >75%
4. Use consistent naming conventions

### Performance Optimizations
1. Avoid object allocations in render loops
2. Use StringBuilder for string concatenation
3. Implement object pooling where appropriate
4. Profile critical rendering paths

### Security Enhancements
1. Remove hardcoded secrets and passwords
2. Use secure configuration management
3. Implement proper input validation
4. Use HTTPS for all network communications

## Next Steps

1. Review detailed issues above
2. Prioritize fixes based on severity
3. Implement automated quality checks in CI/CD
4. Monitor quality metrics over time

## Getting Help

- [TiXL Coding Standards](./CONTRIBUTION_GUIDELINES.md)
- [Warning Resolution Guide](./build_warnings_resolution.md)
- Team code review process
- Performance profiling tools

"@
    
    # Save report
    $report | Out-File -FilePath $OutputPath -Encoding UTF8
    Write-Host "Quality analysis report saved to: $OutputPath" -ForegroundColor Green
    
    # Display summary
    Write-Host "`n## Quality Analysis Summary" -ForegroundColor Cyan
    Write-Host "Files Analyzed: $script:TotalFiles" -ForegroundColor White
    Write-Host "Lines of Code: $script:TotalLines" -ForegroundColor White
    Write-Host "Methods Found: $script:TotalMethods" -ForegroundColor White
    Write-Host "Classes Found: $script:TotalClasses" -ForegroundColor White
    
    if ($totalIssues -eq 0) {
        Write-Host "Quality Status: ✅ EXCELLENT - No issues found" -ForegroundColor Green
    } elseif ($script:SecurityIssues.Count -gt 0 -or $script:PerformanceIssues.Count -gt 0) {
        Write-Host "Quality Status: ❌ CRITICAL - Security or performance issues detected" -ForegroundColor Red
    } else {
        Write-Host "Quality Status: ⚠️ NEEDS IMPROVEMENT - Quality issues found" -ForegroundColor Yellow
    }
}

# Main execution
try {
    $result = Start-ComprehensiveAnalysis
    
    # Exit with appropriate code
    if ($result -eq "ISSUES_FOUND") {
        exit 1
    } else {
        exit 0
    }
} catch {
    Write-Host "Quality analysis failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 2
} finally {
    if ($ShowProgress) {
        Write-Progress -Activity "TiXL Quality Analysis" -Completed
    }
}
