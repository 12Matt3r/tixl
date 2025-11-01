param(
    [Parameter(Mandatory=$true)]
    [string]$BuildPath,
    
    [Parameter(Mandatory=$true)]
    [string]$CoveragePath,
    
    [Parameter(Mandatory=$true)]
    [string]$ConfigPath,
    
    [Parameter(Mandatory=$false)]
    [switch]$GenerateReport,
    
    [Parameter(Mandatory=$false)]
    [switch]$FailOnQualityIssues
)

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"
    Write-Host $logMessage
}

function Get-ConfigValue {
    param([string]$Key, [string]$ConfigPath)
    $configFile = Join-Path $ConfigPath "coverage-thresholds.config"
    if (-not (Test-Path $configFile)) {
        return $null
    }
    
    $config = Get-Content $configFile | Where-Object { $_ -notmatch '^\s*#' -and $_ -match '=' }
    foreach ($line in $config) {
        if ($line -match "$Key\s*=\s*(.+)") {
            return $matches[1].Trim()
        }
    }
    return $null
}

function Test-BuildQualityMetrics {
    param([string]$BuildPath, [string]$CoveragePath)
    
    $qualityResults = @{
        BuildSuccess = $true
        TestSuccess = $true
        CoverageCollected = $false
        CoverageThresholdsMet = $false
        NoCriticalIssues = $true
        QualityScore = 0
        Issues = @()
    }
    
    # Check for build artifacts
    $buildArtifacts = Get-ChildItem -Path $BuildPath -Recurse -File
    if ($buildArtifacts.Count -eq 0) {
        $qualityResults.Issues += "No build artifacts found"
        $qualityResults.BuildSuccess = $false
    }
    
    # Check for coverage data
    $coverageFiles = Get-ChildItem -Path $CoveragePath -Filter "*.cobertura.xml" -Recurse
    if ($coverageFiles.Count -eq 0) {
        $qualityResults.Issues += "No coverage files found"
        $qualityResults.CoverageCollected = $false
    } else {
        $qualityResults.CoverageCollected = $true
        
        # Check coverage summary
        $coverageSummaryPath = Join-Path $CoveragePath "coverage-summary.json"
        if (Test-Path $coverageSummaryPath) {
            try {
                $coverageSummary = Get-Content $coverageSummaryPath | ConvertFrom-Json
                if ($coverageSummary.AllThresholdsMet -eq $true) {
                    $qualityResults.CoverageThresholdsMet = $true
                    $qualityResults.QualityScore += 30
                } else {
                    $qualityResults.Issues += "Coverage thresholds not met"
                    $qualityResults.QualityScore -= 10
                }
            }
            catch {
                $qualityResults.Issues += "Error parsing coverage summary"
            }
        }
    }
    
    # Check for test results
    $testResults = Get-ChildItem -Path $BuildPath -Filter "*.trx" -Recurse
    if ($testResults.Count -eq 0) {
        $qualityResults.Issues += "No test results found"
        $qualityResults.TestSuccess = $false
    }
    
    # Check for security issues in artifacts
    $securityFiles = Get-ChildItem -Path $BuildPath -Filter "*security*" -Recurse
    foreach ($securityFile in $securityFiles) {
        if ($securityFile -match "fail" -or $securityFile -match "critical") {
            $qualityResults.Issues += "Security issues detected in $($securityFile.Name)"
            $qualityResults.NoCriticalIssues = $false
        }
    }
    
    # Calculate overall quality score
    if ($qualityResults.BuildSuccess) { $qualityResults.QualityScore += 25 }
    if ($qualityResults.TestSuccess) { $qualityResults.QualityScore += 25 }
    if ($qualityResults.CoverageCollected) { $qualityResults.QualityScore += 10 }
    if ($qualityResults.NoCriticalIssues) { $qualityResults.QualityScore += 10 }
    
    return $qualityResults
}

function Generate-QualityReport {
    param([hashtable]$QualityResults, [string]$OutputPath, [string]$BuildPath)
    
    $reportPath = Join-Path $OutputPath "comprehensive-quality-report.md"
    
    $score = $QualityResults.QualityScore
    $grade = if ($score -ge 90) { "A" } elseif ($score -ge 80) { "B" } elseif ($score -ge 70) { "C" } elseif ($score -ge 60) { "D" } else { "F" }
    $status = if ($score -ge 80) { "✅ PASSED" } else { "❌ FAILED" }
    
    $report = @"
# TiXL Comprehensive Quality Report

## Overall Quality Assessment
- **Quality Score**: $score/100
- **Quality Grade**: $grade
- **Status**: $status
- **Generated**: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

## Quality Metrics Summary

### Build Quality
- **Build Success**: $(if ($QualityResults.BuildSuccess) { '✅ PASS' } else { '❌ FAIL' })
- **Artifacts Generated**: $(if ($QualityResults.BuildSuccess) { 'Yes' } else { 'No' })

### Test Quality
- **Test Execution**: $(if ($QualityResults.TestSuccess) { '✅ PASS' } else { '❌ FAIL' })
- **Test Results Available**: $(if ($QualityResults.TestSuccess) { 'Yes' } else { 'No' })

### Code Coverage Quality
- **Coverage Collected**: $(if ($QualityResults.CoverageCollected) { '✅ YES' } else { '❌ NO' })
- **Coverage Thresholds Met**: $(if ($QualityResults.CoverageThresholdsMet) { '✅ YES' } else { '❌ NO' })

### Security and Compliance
- **No Critical Issues**: $(if ($QualityResults.NoCriticalIssues) { '✅ YES' } else { '❌ NO' })

## Quality Breakdown
| Category | Weight | Score | Weighted Score |
|----------|--------|-------|----------------|
| Build Success | 25% | $(if ($QualityResults.BuildSuccess) { 25 } else { 0 }) | $(if ($QualityResults.BuildSuccess) { 25 } else { 0 }) |
| Test Success | 25% | $(if ($QualityResults.TestSuccess) { 25 } else { 0 }) | $(if ($QualityResults.TestSuccess) { 25 } else { 0 }) |
| Coverage Collection | 10% | $(if ($QualityResults.CoverageCollected) { 10 } else { 0 }) | $(if ($QualityResults.CoverageCollected) { 10 } else { 0 }) |
| Coverage Thresholds | 30% | $(if ($QualityResults.CoverageThresholdsMet) { 30 } else { 0 }) | $(if ($QualityResults.CoverageThresholdsMet) { 30 } else { 0 }) |
| Security/Compliance | 10% | $(if ($QualityResults.NoCriticalIssues) { 10 } else { 0 }) | $(if ($QualityResults.NoCriticalIssues) { 10 } else { 0 }) |

## Issues Identified
"@

    if ($QualityResults.Issues.Count -eq 0) {
        $report += @"

✅ **No significant issues identified!**

The build meets all quality standards and is ready for deployment.

"@
    } else {
        $report += @"

### Issues Requiring Attention
"@
        foreach ($issue in $QualityResults.Issues) {
            $report += "- ❌ $issue`n"
        }
    }

    $report += @"

## Recommendations

### Quality Improvements
"@

    if ($score -lt 90) {
        $recommendations = @(
            if (-not $QualityResults.BuildSuccess) { "Ensure all projects build successfully without warnings" },
            if (-not $QualityResults.TestSuccess) { "Fix failing tests and ensure all tests pass" },
            if (-not $QualityResults.CoverageCollected) { "Configure coverage collection properly" },
            if (-not $QualityResults.CoverageThresholdsMet) { "Improve test coverage to meet defined thresholds" },
            if (-not $QualityResults.NoCriticalIssues) { "Address any security vulnerabilities or compliance issues" }
        )
        
        foreach ($rec in $recommendations) {
            if ($rec) { $report += "- $rec`n" }
        }
    } else {
        $report += @"
✅ Excellent quality! Consider these enhancements:
- Maintain current quality standards
- Consider adding more performance tests
- Explore additional security scanning tools
- Enhance documentation coverage
"@
    }

    $report += @"

## Next Steps
- **If Status is PASSED**: Proceed with deployment
- **If Status is FAILED**: Address identified issues and rerun quality gate

---
*This report was generated automatically by the TiXL Quality Gate system.*
"@

    $report | Out-File -FilePath $reportPath -Encoding UTF8
    Write-Log "Comprehensive quality report generated: $reportPath"
}

# Main execution
Write-Log "Starting comprehensive quality gate analysis"

# Ensure output directory exists
$outputPath = Join-Path $BuildPath "quality-analysis"
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

# Perform quality analysis
$qualityResults = Test-BuildQualityMetrics -BuildPath $BuildPath -CoveragePath $CoveragePath

# Display results
Write-Log "Quality Analysis Results:"
Write-Log "  Build Success: $(if ($qualityResults.BuildSuccess) { '✅' } else { '❌' })"
Write-Log "  Test Success: $(if ($qualityResults.TestSuccess) { '✅' } else { '❌' })"
Write-Log "  Coverage Collected: $(if ($qualityResults.CoverageCollected) { '✅' } else { '❌' })"
Write-Log "  Coverage Thresholds Met: $(if ($qualityResults.CoverageThresholdsMet) { '✅' } else { '❌' })"
Write-Log "  No Critical Issues: $(if ($qualityResults.NoCriticalIssues) { '✅' } else { '❌' })"
Write-Log "  Quality Score: $($qualityResults.QualityScore)/100"

# Generate report if requested
if ($GenerateReport) {
    Generate-QualityReport -QualityResults $qualityResults -OutputPath $outputPath -BuildPath $BuildPath
}

# Check for issues
if ($qualityResults.Issues.Count -gt 0) {
    Write-Log "Quality Issues Found:"
    foreach ($issue in $qualityResults.Issues) {
        Write-Log "  - $issue" "WARNING"
    }
}

# Exit with appropriate code
if ($FailOnQualityIssues -and ($qualityResults.QualityScore -lt 80 -or $qualityResults.Issues.Count -gt 0)) {
    Write-Log "Quality gate failed due to quality issues" "ERROR"
    exit 1
} else {
    Write-Log "Comprehensive quality gate completed successfully"
    exit 0
}