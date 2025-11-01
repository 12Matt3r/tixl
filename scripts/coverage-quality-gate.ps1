param(
    [Parameter(Mandatory=$true)]
    [string]$CoverageSummaryPath,
    
    [Parameter(Mandatory=$false)]
    [string]$ConfigPath = "docs/config/coverage-thresholds.config",
    
    [Parameter(Mandatory=$false)]
    [switch]$FailOnRegressions,
    
    [Parameter(Mandatory=$false)]
    [string]$BaselineFile,
    
    [Parameter(Mandatory=$false)]
    [switch]$GenerateGitHubComment,
    
    [Parameter(Mandatory=$false)]
    [switch]$Verbose
)

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"
    Write-Host $logMessage
}

function Get-ConfigValue {
    param([string]$Key, [string]$ConfigPath)
    if (-not (Test-Path $ConfigPath)) {
        Write-Log "Config file not found: $ConfigPath" "WARNING"
        return $null
    }
    
    $config = Get-Content $ConfigPath | Where-Object { $_ -notmatch '^\s*#' -and $_ -match '=' }
    foreach ($line in $config) {
        if ($line -match "$Key\s*=\s*(.+)") {
            return $matches[1].Trim()
        }
    }
    return $null
}

function Get-BaselineCoverage {
    param([string]$BaselineFile)
    
    if (-not $BaselineFile -or -not (Test-Path $BaselineFile)) {
        return $null
    }
    
    try {
        $baseline = Get-Content $BaselineFile | ConvertFrom-Json
        return $baseline.OverallMetrics
    }
    catch {
        Write-Log "Error loading baseline coverage: $($_.Exception.Message)" "WARNING"
        return $null
    }
}

function Test-CoverageRegressions {
    param([hashtable]$CurrentMetrics, [hashtable]$BaselineMetrics)
    
    if (-not $BaselineMetrics) {
        return @{
            HasRegressions = $false
            Regressions = @()
            Improvements = @()
        }
    }
    
    $regressions = @()
    $improvements = @()
    
    $lineRegression = $CurrentMetrics.AverageLineRate - $BaselineMetrics.AverageLineRate
    $branchRegression = $CurrentMetrics.AverageBranchRate - $BaselineMetrics.AverageBranchRate
    $methodRegression = $CurrentMetrics.AverageMethodRate - $BaselineMetrics.AverageMethodRate
    
    # Define regression thresholds (2% drop is considered a regression)
    $regressionThreshold = 2.0
    
    if ($lineRegression -lt -$regressionThreshold) {
        $regressions += @{
            Metric = "Line Coverage"
            Current = $CurrentMetrics.AverageLineRate
            Baseline = $BaselineMetrics.AverageLineRate
            Change = $lineRegression
        }
    } elseif ($lineRegression -gt $regressionThreshold) {
        $improvements += @{
            Metric = "Line Coverage"
            Current = $CurrentMetrics.AverageLineRate
            Baseline = $BaselineMetrics.AverageLineRate
            Change = $lineRegression
        }
    }
    
    if ($branchRegression -lt -$regressionThreshold) {
        $regressions += @{
            Metric = "Branch Coverage"
            Current = $CurrentMetrics.AverageBranchRate
            Baseline = $BaselineMetrics.AverageBranchRate
            Change = $branchRegression
        }
    } elseif ($branchRegression -gt $regressionThreshold) {
        $improvements += @{
            Metric = "Branch Coverage"
            Current = $CurrentMetrics.AverageBranchRate
            Baseline = $BaselineMetrics.AverageBranchRate
            Change = $branchRegression
        }
    }
    
    if ($methodRegression -lt -$regressionThreshold) {
        $regressions += @{
            Metric = "Method Coverage"
            Current = $CurrentMetrics.AverageMethodRate
            Baseline = $BaselineMetrics.AverageMethodRate
            Change = $methodRegression
        }
    } elseif ($methodRegression -gt $regressionThreshold) {
        $improvements += @{
            Metric = "Method Coverage"
            Current = $CurrentMetrics.AverageMethodRate
            Baseline = $BaselineMetrics.AverageMethodCoverage
            Change = $methodRegression
        }
    }
    
    return @{
        HasRegressions = $regressions.Count -gt 0
        Regressions = $regressions
        Improvements = $improvements
    }
}

function Test-OverallThresholds {
    param([hashtable]$CurrentMetrics)
    
    $globalLineThreshold = [double](Get-ConfigValue "Global.MinimumLineCoverage" $ConfigPath)
    $globalBranchThreshold = [double](Get-ConfigValue "Global.MinimumBranchCoverage" $ConfigPath)
    $globalMethodThreshold = [double](Get-ConfigValue "Global.MinimumMethodCoverage" $ConfigPath)
    
    $passed = $true
    $failures = @()
    
    if ($CurrentMetrics.AverageLineRate -lt $globalLineThreshold) {
        $passed = $false
        $failures += "Average line coverage ($($CurrentMetrics.AverageLineRate)%) below global threshold ($globalLineThreshold%)"
    }
    
    if ($CurrentMetrics.AverageBranchRate -lt $globalBranchThreshold) {
        $passed = $false
        $failures += "Average branch coverage ($($CurrentMetrics.AverageBranchRate)%) below global threshold ($globalBranchThreshold%)"
    }
    
    if ($CurrentMetrics.AverageMethodRate -lt $globalMethodThreshold) {
        $passed = $false
        $failures += "Average method coverage ($($CurrentMetrics.AverageMethodRate)%) below global threshold ($globalMethodThreshold%)"
    }
    
    return @{
        Passed = $passed
        Failures = $failures
        Thresholds = @{
            Line = $globalLineThreshold
            Branch = $globalBranchThreshold
            Method = $globalMethodThreshold
        }
    }
}

function Generate-GitHubComment {
    param([hashtable]$AnalysisResult)
    
    $comment = @"
## 📊 Code Coverage Analysis Results

### Overall Coverage Summary
- **Line Coverage**: $($AnalysisResult.CurrentMetrics.AverageLineRate)% $(if ($AnalysisResult.OverallThresholds.Passed) { '✅' } else { '❌' })
- **Branch Coverage**: $($AnalysisResult.CurrentMetrics.AverageBranchRate)% $(if ($AnalysisResult.OverallThresholds.Passed) { '✅' } else { '❌' })
- **Method Coverage**: $($AnalysisResult.CurrentMetrics.AverageMethodRate)% $(if ($AnalysisResult.OverallThresholds.Passed) { '✅' } else { '❌' })
- **Modules Tested**: $($AnalysisResult.TotalModules)

"@
    
    if ($AnalysisResult.BaselineMetrics) {
        $comment += @"
### Coverage Trends
"@
        foreach ($regression in $AnalysisResult.RegressionTest.Regressions) {
            $comment += "- 🔻 **Regression**: $($regression.Metric) dropped by $([math]::Abs($regression.Change))% `($($regression.Baseline)% → $($regression.Current)%)`" + [Environment]::NewLine
        }
        foreach ($improvement in $AnalysisResult.RegressionTest.Improvements) {
            $comment += "- 🚀 **Improvement**: $($improvement.Metric) increased by $($improvement.Change)% `($($improvement.Baseline)% → $($improvement.Current)%)`" + [Environment]::NewLine
        }
        $comment += [Environment]::NewLine
    }
    
    if (-not $AnalysisResult.OverallThresholds.Passed) {
        $comment += @"
### ❌ Quality Gate Failures
"@
        foreach ($failure in $AnalysisResult.OverallThresholds.Failures) {
            $comment += "- $failure" + [Environment]::NewLine
        }
        $comment += [Environment]::NewLine
    }
    
    $comment += @"
### Module Coverage Details
| Module | Line Coverage | Branch Coverage | Method Coverage | Status |
|--------|---------------|-----------------|-----------------|---------|
"@
    
    foreach ($module in $AnalysisResult.Modules) {
        $status = if ($module.ThresholdTest.Passed) { '✅ PASS' } else { '❌ FAIL' }
        $comment += "| $($module.Module) | $($module.LineRate)% | $($module.BranchRate)% | $($module.MethodRate)% | $status |" + [Environment]::NewLine
    }
    
    if (-not $AnalysisResult.OverallThresholds.Passed -or $AnalysisResult.RegressionTest.HasRegressions) {
        $comment += [Environment]::NewLine + @"
### 📋 Action Items
"@
        if (-not $AnalysisResult.OverallThresholds.Passed) {
            $comment += "- [ ] Address coverage threshold failures above" + [Environment]::NewLine
        }
        if ($AnalysisResult.RegressionTest.HasRegressions) {
            $comment += "- [ ] Investigate coverage regressions and add missing tests" + [Environment]::NewLine
        }
    }
    
    return $comment
}

# Main execution
Write-Log "Starting TiXL Coverage Quality Gate Analysis"

if (-not (Test-Path $CoverageSummaryPath)) {
    Write-Log "Coverage summary file not found: $CoverageSummaryPath" "ERROR"
    exit 1
}

# Load current coverage summary
try {
    $coverageSummary = Get-Content $CoverageSummaryPath | ConvertFrom-Json
}
catch {
    Write-Log "Error parsing coverage summary: $($_.Exception.Message)" "ERROR"
    exit 1
}

# Perform analysis
$baseline = Get-BaselineCoverage -BaselineFile $BaselineFile
$regressionTest = Test-CoverageRegressions -CurrentMetrics $coverageSummary.OverallMetrics -BaselineMetrics $baseline
$overallThresholds = Test-OverallThresholds -CurrentMetrics $coverageSummary.OverallMetrics

$analysisResult = @{
    TotalModules = $coverageSummary.TotalModules
    AllThresholdsMet = $coverageSummary.AllThresholdsMet
    CurrentMetrics = $coverageSummary.OverallMetrics
    BaselineMetrics = $baseline
    RegressionTest = $regressionTest
    OverallThresholds = $overallThresholds
    Modules = $coverageSummary.Modules
}

# Generate GitHub comment if requested
if ($GenerateGitHubComment) {
    $githubComment = Generate-GitHubComment -AnalysisResult $analysisResult
    $commentPath = Join-Path (Split-Path $CoverageSummaryPath) "github-coverage-comment.md"
    $githubComment | Out-File -FilePath $commentPath -Encoding UTF8
    Write-Log "GitHub comment generated: $commentPath"
}

# Write detailed analysis
$analysisPath = Join-Path (Split-Path $CoverageSummaryPath) "coverage-quality-analysis.json"
$analysisResult | ConvertTo-Json -Depth 10 | Out-File -FilePath $analysisPath -Encoding UTF8
Write-Log "Quality analysis saved: $analysisPath"

# Display summary
Write-Log "Coverage Quality Gate Results:"
Write-Log "  Overall Line Coverage: $($analysisResult.CurrentMetrics.AverageLineRate)%"
Write-Log "  Overall Branch Coverage: $($analysisResult.CurrentMetrics.AverageBranchRate)%"
Write-Log "  Overall Method Coverage: $($analysisResult.CurrentMetrics.AverageMethodRate)%"

if ($analysisResult.OverallThresholds.Passed) {
    Write-Log "✅ Overall coverage thresholds: PASSED"
} else {
    Write-Log "❌ Overall coverage thresholds: FAILED"
    foreach ($failure in $analysisResult.OverallThresholds.Failures) {
        Write-Log "  - $failure" "ERROR"
    }
}

if ($regressionTest.HasRegressions) {
    Write-Log "⚠️ Coverage regressions detected:"
    foreach ($regression in $regressionTest.Regressions) {
        Write-Log "  - $($regression.Metric): $($regression.Change)% drop" "WARNING"
    }
}

if ($regressionTest.Improvements.Count -gt 0) {
    Write-Log "🎉 Coverage improvements detected:"
    foreach ($improvement in $regressionTest.Improvements) {
        Write-Log "  - $($improvement.Metric): +$($improvement.Change)%" "SUCCESS"
    }
}

# Determine exit status
$shouldFail = $false

if (-not $analysisResult.OverallThresholds.Passed) {
    Write-Log "Quality gate failed: Coverage thresholds not met" "ERROR"
    $shouldFail = $true
}

if ($FailOnRegressions -and $regressionTest.HasRegressions) {
    Write-Log "Quality gate failed: Coverage regressions detected" "ERROR"
    $shouldFail = $true
}

if ($shouldFail) {
    exit 1
} else {
    Write-Log "✅ All quality gates passed"
    exit 0
}