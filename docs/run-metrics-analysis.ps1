# TiXL Code Metrics Analysis Script
# Analyzes code complexity, maintainability, and other metrics

param(
    [Parameter(Mandatory=$false)]
    [string]$SolutionPath = "..\TiXL.sln",
    
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "metrics-report.json",
    
    [Parameter(Mandatory=$false)]
    [string]$Format = "json",
    
    [Parameter(Mandatory=$false)]
    [switch]$ShowProgress
)

$ErrorActionPreference = "Stop"

$global:ProjectMetrics = @()
$global:FileMetrics = @()

function Write-ProgressSafe {
    param([string]$Message)
    if ($ShowProgress) {
        Write-Progress -Activity "Code Metrics Analysis" -Status $Message
    }
}

function Calculate-CodeMetrics {
    param([string]$ProjectPath)
    
    $metrics = @{
        Project = (Split-Path $ProjectPath -Leaf)
        ProjectPath = $ProjectPath
        Timestamp = (Get-Date).ToUniversalTime()
        Files = @()
        Summary = @{}
        Issues = @()
    }
    
    # Get all C# files in the project
    $projectDir = Split-Path $ProjectPath -Parent
    $csFiles = Get-ChildItem -Path $projectDir -Recurse -Filter "*.cs" -File
    
    Write-Host "Found $($csFiles.Count) C# files to analyze" -ForegroundColor Yellow
    
    foreach ($file in $csFiles) {
        Write-ProgressSafe "Analyzing $($file.Name)"
        $fileMetrics = Analyze-FileMetrics $file.FullName
        $metrics.Files += $fileMetrics
    }
    
    # Calculate aggregate metrics
    $metrics.Summary = Calculate-AggregateMetrics $metrics.Files
    $metrics.Issues = Detect-MetricsIssues $metrics.Summary
    
    return $metrics
}

function Analyze-FileMetrics {
    param([string]$FilePath)
    
    try {
        $content = Get-Content $FilePath -Raw
        $lines = $content -split "`n"
        
        $fileMetrics = @{
            File = $FilePath
            FileName = (Split-Path $FilePath -Leaf)
            LinesOfCode = ($lines | Where-Object { $_.Trim() -ne "" -and $_.Trim() -notmatch '^\s*//' }).Count
            TotalLines = $lines.Count
            CommentLines = ($lines | Where-Object { $_.Trim() -match '^\s*//' }).Count
            BlankLines = ($lines | Where-Object { $_.Trim() -eq "" }).Count
            Methods = @()
            Classes = 0
            Interfaces = 0
            Structs = 0
            Enums = 0
            Properties = 0
            Fields = 0
            Complexity = 0
            ComplexityByMethod = @{}
            LongMethods = @()
            HighComplexityMethods = @()
            NestingDepth = 0
            MaxNestingDepth = 0
            Dependencies = @()
        }
        
        $currentMethod = ""
        $methodComplexity = 0
        $currentNesting = 0
        $lineNum = 0
        
        foreach ($line in $lines) {
            $lineNum++
            
            # Count types
            if ($line -match '^\s*public\s+class\s+\w+') { $fileMetrics.Classes++ }
            if ($line -match '^\s*public\s+interface\s+\w+') { $fileMetrics.Interfaces++ }
            if ($line -match '^\s*public\s+struct\s+\w+') { $fileMetrics.Structs++ }
            if ($line -match '^\s*public\s+enum\s+\w+') { $fileMetrics.Enums++ }
            
            # Count properties and fields
            if ($line -match '^\s*(public|private|protected)\s+(get|set)') { $fileMetrics.Properties++ }
            if ($line -match '^\s*(public|private|protected)\s+(static\s+)?\w+\s+\w+;') { $fileMetrics.Fields++ }
            
            # Track nesting depth
            $currentNesting += ([regex]::Matches($line, '\{').Count - [regex]::Matches($line, '\}').Count)
            if ($currentNesting -gt $fileMetrics.MaxNestingDepth) {
                $fileMetrics.MaxNestingDepth = $currentNesting
            }
            
            # Detect method start
            if ($line -match '^\s*(public|private|protected|internal|static|\s)*(async\s+)?(\w+)\s+(\w+)\s*\(') {
                # Save previous method metrics
                if ($currentMethod -ne "") {
                    $fileMetrics.ComplexityByMethod[$currentMethod] = $methodComplexity
                    
                    if ($methodLines -gt 50) {
                        $fileMetrics.LongMethods += @{
                            Name = $currentMethod
                            Lines = $methodLines
                        }
                    }
                    
                    if ($methodComplexity -gt 15) {
                        $fileMetrics.HighComplexityMethods += @{
                            Name = $currentMethod
                            Complexity = $methodComplexity
                        }
                    }
                }
                
                # Start new method
                $currentMethod = $matches[4]
                $methodComplexity = 1
                $methodLines = 1
                $methodNestingStart = $currentNesting
                
                $methodInfo = @{
                    Name = $currentMethod
                    StartLine = $lineNum
                    Lines = 0
                    Complexity = 0
                    Parameters = $matches[0] -replace '\s+', ' '
                }
                
                $fileMetrics.Methods += $methodInfo
            }
            
            # Calculate complexity for current method
            if ($currentMethod -ne "" -and $methodLines -gt 0) {
                $methodLines++
                
                # Decision points that increase complexity
                if ($line -match '\b(if|else|while|for|foreach|case|catch|finally|throw|return|try)\b') {
                    $methodComplexity++
                }
                # Logical operators
                if ($line -match '(&&|\|\||\?)') {
                    $methodComplexity++
                }
                # Complex conditions
                if ($line -match '[\(\)\{\}\[\]]') {
                    $methodComplexity += [regex]::Matches($line, '[\(\)\{\}\[\]]').Count / 2
                }
                # Switch statements
                if ($line -match '^\s*case\s+\w+:') {
                    $methodComplexity++
                }
            }
            
            # Track dependencies
            if ($line -match 'using\s+(\w+(\.\w+)*)') {
                $namespace = $matches[1]
                if ($fileMetrics.Dependencies -notcontains $namespace) {
                    $fileMetrics.Dependencies += $namespace
                }
            }
            
            if ($line -match 'new\s+(\w+)') {
                $type = $matches[1]
                if ($fileMetrics.Dependencies -notcontains $type -and $type -notmatch '(int|string|bool|float|double|decimal|object)') {
                    $fileMetrics.Dependencies += $type
                }
            }
        }
        
        # Finalize last method
        if ($currentMethod -ne "") {
            $fileMetrics.ComplexityByMethod[$currentMethod] = $methodComplexity
            $fileMetrics.Methods[$fileMetrics.Methods.Count - 1].Lines = $methodLines
            $fileMetrics.Methods[$fileMetrics.Methods.Count - 1].Complexity = $methodComplexity
        }
        
        # Calculate total complexity
        foreach ($complexity in $fileMetrics.ComplexityByMethod.Values) {
            $fileMetrics.Complexity += $complexity
        }
        
        return $fileMetrics
    }
    catch {
        Write-Host "Error analyzing $FilePath`: $($_.Exception.Message)" -ForegroundColor Red
        return @{
            File = $FilePath
            Error = $_.Exception.Message
        }
    }
}

function Calculate-AggregateMetrics {
    param([array]$FileMetrics)
    
    $aggregate = @{
        TotalFiles = $FileMetrics.Count
        TotalLinesOfCode = 0
        TotalMethods = 0
        TotalClasses = 0
        AverageFileSize = 0
        AverageMethodsPerFile = 0
        AverageComplexityPerMethod = 0
        MaxMethodComplexity = 0
        ComplexMethods = 0
        LongMethods = 0
        HighNestingFiles = 0
        MostComplexMethods = @()
        LargestFiles = @()
        DependencyAnalysis = @{}
    }
    
    $complexMethods = @()
    $largeFiles = @()
    $allDependencies = @{}
    
    foreach ($file in $FileMetrics) {
        if ($file.Error) { continue }
        
        $aggregate.TotalLinesOfCode += $file.LinesOfCode
        $aggregate.TotalMethods += $file.Methods.Count
        $aggregate.TotalClasses += $file.Classes
        
        # Track complex methods
        foreach ($method in $file.Methods) {
            if ($method.Complexity -gt $aggregate.MaxMethodComplexity) {
                $aggregate.MaxMethodComplexity = $method.Complexity
            }
            
            if ($method.Complexity -gt 15) {
                $complexMethods += @{
                    Method = $method.Name
                    File = $file.FileName
                    Complexity = $method.Complexity
                }
                $aggregate.ComplexMethods++
            }
            
            if ($method.Lines -gt 50) {
                $aggregate.LongMethods++
            }
        }
        
        # Track large files
        if ($file.LinesOfCode -gt 300) {
            $largeFiles += @{
                File = $file.FileName
                Lines = $file.LinesOfCode
            }
        }
        
        # Track high nesting
        if ($file.MaxNestingDepth -gt 4) {
            $aggregate.HighNestingFiles++
        }
        
        # Track dependencies
        foreach ($dep in $file.Dependencies) {
            if ($allDependencies.ContainsKey($dep)) {
                $allDependencies[$dep]++
            } else {
                $allDependencies[$dep] = 1
            }
        }
    }
    
    # Calculate averages
    if ($aggregate.TotalFiles -gt 0) {
        $aggregate.AverageFileSize = [math]::Round($aggregate.TotalLinesOfCode / $aggregate.TotalFiles, 2)
        $aggregate.AverageMethodsPerFile = [math]::Round($aggregate.TotalMethods / $aggregate.TotalFiles, 2)
    }
    
    if ($aggregate.TotalMethods -gt 0) {
        $totalComplexity = ($FileMetrics | Where-Object { -not $_.Error } | ForEach-Object { $_.Complexity } | Measure-Object -Sum).Sum
        $aggregate.AverageComplexityPerMethod = [math]::Round($totalComplexity / $aggregate.TotalMethods, 2)
    }
    
    # Get top complex methods
    $aggregate.MostComplexMethods = ($complexMethods | Sort-Object Complexity -Descending | Select-Object -First 10)
    
    # Get largest files
    $aggregate.LargestFiles = ($largeFiles | Sort-Object Lines -Descending | Select-Object -First 10)
    
    # Dependency analysis
    $sortedDeps = $allDependencies.GetEnumerator() | Sort-Object Value -Descending
    $aggregate.DependencyAnalysis = @{
        TotalUniqueDependencies = $allDependencies.Count
        MostUsedDependencies = ($sortedDeps | Select-Object -First 20)
        UnusedDependencies = @() # Would need more sophisticated analysis
    }
    
    return $aggregate
}

function Detect-MetricsIssues {
    param([hashtable]$Metrics)
    
    $issues = @()
    
    # High complexity issues
    if ($Metrics.AverageComplexityPerMethod -gt 10) {
        $issues += @{
            Type = "Complexity"
            Severity = "Warning"
            Message = "Average method complexity ($($Metrics.AverageComplexityPerMethod)) is high"
            Recommendation = "Consider refactoring complex methods to improve maintainability"
        }
    }
    
    if ($Metrics.MaxMethodComplexity -gt 20) {
        $issues += @{
            Type = "Complexity"
            Severity = "Error"
            Message = "Maximum method complexity ($($Metrics.MaxMethodComplexity)) is very high"
            Recommendation = "Immediately refactor the most complex methods"
        }
    }
    
    # File size issues
    if ($Metrics.AverageFileSize -gt 200) {
        $issues += @{
            Type = "Maintainability"
            Severity = "Warning"
            Message = "Average file size ($($Metrics.AverageFileSize) LOC) is large"
            Recommendation = "Consider breaking large files into smaller, focused components"
        }
    }
    
    # Complexity distribution
    if ($Metrics.ComplexMethods -gt $Metrics.TotalMethods * 0.1) {
        $issues += @{
            Type = "Quality"
            Severity = "Warning"
            Message = "$($Metrics.ComplexMethods) methods ($([math]::Round($Metrics.ComplexMethods * 100 / $Metrics.TotalMethods)))% exceed complexity threshold"
            Recommendation = "Refactor complex methods to reduce cognitive load"
        }
    }
    
    # Dependency issues
    if ($Metrics.DependencyAnalysis.TotalUniqueDependencies -gt 50) {
        $issues += @{
            Type = "Architecture"
            Severity = "Info"
            Message = "High number of unique dependencies ($($Metrics.DependencyAnalysis.TotalUniqueDependencies))"
            Recommendation = "Consider dependency injection and modularization to reduce coupling"
        }
    }
    
    return $issues
}

function Generate-Report {
    param([hashtable]$Metrics, [string]$OutputPath, [string]$Format)
    
    if ($Format -eq "json") {
        $report = $Metrics | ConvertTo-Json -Depth 10
        $report | Out-File -FilePath $OutputPath -Encoding UTF8
        Write-Host "JSON report saved to: $OutputPath" -ForegroundColor Green
    } elseif ($Format -eq "html") {
        $html = Generate-HtmlReport $Metrics
        $html | Out-File -FilePath $OutputPath -Encoding UTF8
        Write-Host "HTML report saved to: $OutputPath" -ForegroundColor Green
    } elseif ($Format -eq "markdown") {
        $md = Generate-MarkdownReport $Metrics
        $md | Out-File -FilePath $OutputPath -Encoding UTF8
        Write-Host "Markdown report saved to: $OutputPath" -ForegroundColor Green
    }
}

function Generate-MarkdownReport {
    param([hashtable]$Metrics)
    
    $report = @"
# TiXL Code Metrics Report

**Generated:** $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss UTC')  
**Project:** $($Metrics.Project)

## Summary Metrics

| Metric | Value |
|--------|-------|
| Total Files | $($Metrics.Summary.TotalFiles) |
| Total Lines of Code | $($Metrics.Summary.TotalLinesOfCode) |
| Total Methods | $($Metrics.Summary.TotalMethods) |
| Total Classes | $($Metrics.Summary.TotalClasses) |
| Average File Size | $($Metrics.Summary.AverageFileSize) LOC |
| Average Methods per File | $($Metrics.Summary.AverageMethodsPerFile) |
| Average Complexity per Method | $($Metrics.Summary.AverageComplexityPerMethod) |
| Maximum Method Complexity | $($Metrics.Summary.MaxMethodComplexity) |
| Complex Methods (>15) | $($Metrics.Summary.ComplexMethods) |
| Long Methods (>50 lines) | $($Metrics.Summary.LongMethods) |
| High Nesting Files (>4) | $($Metrics.Summary.HighNestingFiles) |

## Most Complex Methods

"@
    
    if ($Metrics.Summary.MostComplexMethods.Count -gt 0) {
        foreach ($method in $Metrics.Summary.MostComplexMethods) {
            $report += "- **$($method.Method)** ($($method.File)): Complexity $($method.Complexity)`n"
        }
    } else {
        $report += "No methods exceed the complexity threshold.`n"
    }
    
    $report += "`n## Largest Files`n`n"
    
    if ($Metrics.Summary.LargestFiles.Count -gt 0) {
        foreach ($file in $Metrics.Summary.LargestFiles) {
            $report += "- **$($file.File)**: $($file.Lines) lines`n"
        }
    } else {
        $report += "No files exceed the size threshold.`n"
    }
    
    $report += "`n## Dependency Analysis`n`n"
    $report += "- **Total Unique Dependencies:** $($Metrics.Summary.DependencyAnalysis.TotalUniqueDependencies)`n"
    
    if ($Metrics.Summary.DependencyAnalysis.MostUsedDependencies.Count -gt 0) {
        $report += "`n### Most Used Dependencies`n`n"
        foreach ($dep in $Metrics.Summary.DependencyAnalysis.MostUsedDependencies) {
            $report += "- $($dep.Key): $($dep.Value) references`n"
        }
    }
    
    if ($Metrics.Issues.Count -gt 0) {
        $report += "`n## Issues Detected`n`n"
        foreach ($issue in $Metrics.Issues) {
            $emoji = switch ($issue.Severity) {
                "Error" { "❌" }
                "Warning" { "⚠️" }
                "Info" { "ℹ️" }
            }
            $report += "$emoji **$($issue.Type):** $($issue.Message)`n"
            $report += "   → $($issue.Recommendation)`n`n"
        }
    } else {
        $report += "`n## Issues Detected`n`n🎉 No major issues detected in the metrics analysis!`n`n"
    }
    
    $report += @"
## Recommendations

### Code Quality
1. **Refactor High Complexity Methods**: Focus on methods with complexity > 15
2. **Break Down Large Files**: Files with > 300 lines should be split
3. **Reduce Nesting**: Limit nesting depth to 4 levels maximum
4. **Improve Test Coverage**: Target 75%+ code coverage

### Architecture
1. **Dependency Management**: Review and minimize external dependencies
2. **Modularization**: Consider breaking monolithic files into focused components
3. **Interface Segregation**: Reduce coupling through proper abstractions

### Performance
1. **Method Optimization**: Focus on frequently called complex methods
2. **Memory Management**: Review large data structures and allocations
3. **Caching Strategy**: Implement caching for expensive operations

## Next Steps

1. Review the most complex methods and plan refactoring
2. Prioritize fixes based on method usage and impact
3. Implement monitoring to track metrics over time
4. Set up automated metrics analysis in CI/CD

"@
    
    return $report
}

function Generate-HtmlReport {
    param([hashtable]$Metrics)
    
    $html = @"
<!DOCTYPE html>
<html>
<head>
    <title>TiXL Code Metrics Report</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; }
        .header { background: #2c3e50; color: white; padding: 20px; border-radius: 5px; }
        .section { margin: 20px 0; padding: 15px; border: 1px solid #ddd; border-radius: 5px; }
        .metric { display: inline-block; margin: 10px; padding: 10px; background: #ecf0f1; border-radius: 3px; }
        .error { color: #e74c3c; }
        .warning { color: #f39c12; }
        .info { color: #3498db; }
        table { width: 100%; border-collapse: collapse; }
        th, td { padding: 8px; text-align: left; border-bottom: 1px solid #ddd; }
        th { background-color: #f2f2f2; }
    </style>
</head>
<body>
    <div class="header">
        <h1>TiXL Code Metrics Report</h1>
        <p>Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss UTC')</p>
        <p>Project: $($Metrics.Project)</p>
    </div>

    <div class="section">
        <h2>Summary Metrics</h2>
        <div class="metric">Total Files: $($Metrics.Summary.TotalFiles)</div>
        <div class="metric">Total LOC: $($Metrics.Summary.TotalLinesOfCode)</div>
        <div class="metric">Total Methods: $($Metrics.Summary.TotalMethods)</div>
        <div class="metric">Avg Complexity: $($Metrics.Summary.AverageComplexityPerMethod)</div>
        <div class="metric">Complex Methods: $($Metrics.Summary.ComplexMethods)</div>
    </div>

    <div class="section">
        <h2>Most Complex Methods</h2>
        <table>
            <tr><th>Method</th><th>File</th><th>Complexity</th></tr>
"@
    
    foreach ($method in $Metrics.Summary.MostComplexMethods) {
        $complexityClass = if ($method.Complexity -gt 20) { "error" } elseif ($method.Complexity -gt 15) { "warning" } else { "info" }
        $html += "            <tr><td>$($method.Method)</td><td>$($method.File)</td><td class=""$complexityClass"">$($method.Complexity)</td></tr>`n"
    }
    
    $html += @"
        </table>
    </div>

    <div class="section">
        <h2>Issues Detected</h2>
"@
    
    if ($Metrics.Issues.Count -gt 0) {
        foreach ($issue in $Metrics.Issues) {
            $severityClass = $issue.Severity.ToLower()
            $html += "        <div class=""$severityClass"">$($issue.Type): $($issue.Message)</div>`n"
            $html += "        <div class=""recommendation"">→ $($issue.Recommendation)</div>`n"
        }
    } else {
        $html += "        <p>🎉 No major issues detected!</p>`n"
    }
    
    $html += @"
    </div>

    <div class="section">
        <h2>Dependency Analysis</h2>
        <p>Total Unique Dependencies: $($Metrics.Summary.DependencyAnalysis.TotalUniqueDependencies)</p>
        <h3>Most Used Dependencies</h3>
        <ul>
"@
    
    foreach ($dep in $Metrics.Summary.DependencyAnalysis.MostUsedDependencies) {
        $html += "            <li>$($dep.Key): $($dep.Value) references</li>`n"
    }
    
    $html += @"
        </ul>
    </div>

</body>
</html>
"@
    
    return $html
}

# Main execution
try {
    Write-Host "📊 Starting TiXL Code Metrics Analysis..." -ForegroundColor Green
    
    if (-not (Test-Path $SolutionPath)) {
        throw "Solution file not found at: $SolutionPath"
    }
    
    $allMetrics = @{
        Timestamp = (Get-Date).ToUniversalTime()
        Solution = $SolutionPath
        Projects = @()
        OverallSummary = @{}
        OverallIssues = @()
    }
    
    # Parse solution file to find projects
    $solution = Get-Content $SolutionPath -Raw
    $projects = [regex]::Matches($solution, 'Project\(".*?"\)\s*=\s*"([^"]+)"\s*,\s*"([^"]+)"')
    
    foreach ($project in $projects) {
        $projectName = $project.Groups[1].Value
        $projectFile = $project.Groups[2].Value
        
        if (Test-Path $projectFile) {
            Write-Host "Analyzing project: $projectName" -ForegroundColor Yellow
            $projectMetrics = Calculate-CodeMetrics $projectFile
            $allMetrics.Projects += $projectMetrics
        }
    }
    
    # Calculate overall summary
    $allProjectSummaries = $allMetrics.Projects | ForEach-Object { $_.Summary }
    $overallSummary = Calculate-AggregateMetrics $allMetrics.Projects.Files
    $overallIssues = @()
    
    foreach ($project in $allMetrics.Projects) {
        foreach ($issue in $project.Issues) {
            $overallIssues += $issue
        }
    }
    
    $allMetrics.OverallSummary = $overallSummary
    $allMetrics.OverallIssues = $overallIssues
    
    # Generate report
    Generate-Report -Metrics $allMetrics -OutputPath $OutputPath -Format $Format
    
    # Display summary
    Write-Host "`n## Code Metrics Summary" -ForegroundColor Cyan
    Write-Host "Projects Analyzed: $($allMetrics.Projects.Count)" -ForegroundColor White
    Write-Host "Total Files: $($overallSummary.TotalFiles)" -ForegroundColor White
    Write-Host "Total LOC: $($overallSummary.TotalLinesOfCode)" -ForegroundColor White
    Write-Host "Average Complexity: $($overallSummary.AverageComplexityPerMethod)" -ForegroundColor White
    Write-Host "Complex Methods: $($overallSummary.ComplexMethods)" -ForegroundColor $(if ($overallSummary.ComplexMethods -gt 0) { "Yellow" } else { "Green" })
    
    if ($overallIssues.Count -eq 0) {
        Write-Host "Analysis Result: ✅ No critical issues detected" -ForegroundColor Green
    } else {
        Write-Host "Analysis Result: ⚠️ $($overallIssues.Count) issues found" -ForegroundColor Yellow
    }
    
} catch {
    Write-Host "Metrics analysis failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 2
} finally {
    if ($ShowProgress) {
        Write-Progress -Activity "Code Metrics Analysis" -Completed
    }
}
