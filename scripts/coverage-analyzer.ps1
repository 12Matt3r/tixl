param(
    [Parameter(Mandatory=$true)]
    [string]$SolutionPath,
    
    [Parameter(Mandatory=$true)]
    [string]$TestProjectPath,
    
    [Parameter(Mandatory=$true)]
    [string]$OutputPath,
    
    [Parameter(Mandatory=$false)]
    [string]$ConfigPath = "docs/config/coverage-thresholds.config",
    
    [Parameter(Mandatory=$false)]
    [switch]$FailOnLowCoverage,
    
    [Parameter(Mandatory=$false)]
    [switch]$GenerateHTMLReport,
    
    [Parameter(Mandatory=$false)]
    [switch]$GenerateDiffReport,
    
    [Parameter(Mandatory=$false)]
    [switch]$Verbose
)

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"
    Write-Host $logMessage
    if ($OutputPath) {
        $logFile = Join-Path $OutputPath "coverage-analysis.log"
        Add-Content -Path $logFile -Value $logMessage
    }
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

function Invoke-CoverageCollection {
    param([string]$ProjectPath, [string]$OutputPath, [string]$RunSettings)
    
    Write-Log "Starting coverage collection for $ProjectPath"
    
    $coverageResultsPath = Join-Path $OutputPath "coverage-results"
    New-Item -ItemType Directory -Path $coverageResultsPath -Force | Out-Null
    
    # Build the dotnet test command with Coverlet
    $testArgs = @(
        "test",
        "`"$ProjectPath`"",
        "--configuration", "Release",
        "--no-build",
        "--verbosity", "minimal",
        "--collect:`"XPlat Code Coverage`"",
        "-- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura,json",
        "-- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.IncludeTestAssembly=false",
        "-- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.ExcludeByFile=**/*Test*.cs,**/*Mock*.cs,**/*Benchmark*.cs,**/*Fixtures*.cs,**/*Examples*.cs,**/*Designer*.cs,**/*.g.cs"
    )
    
    if ($RunSettings -and (Test-Path $RunSettings)) {
        $testArgs += "--settings", "`"$RunSettings`""
        Write-Log "Using run settings: $RunSettings"
    }
    
    try {
        & dotnet @testArgs
        if ($LASTEXITCODE -ne 0) {
            Write-Log "Test execution failed with exit code $LASTEXITCODE" "ERROR"
            return $false
        }
    }
    catch {
        Write-Log "Test execution failed: $($_.Exception.Message)" "ERROR"
        return $false
    }
    
    # Find generated coverage files
    $testResultsPath = Join-Path $ProjectPath "TestResults"
    $coverageFiles = Get-ChildItem -Path $testResultsPath -Filter "*.cobertura.xml" -Recurse
    
    if (-not $coverageFiles) {
        Write-Log "No coverage files found" "ERROR"
        return $false
    }
    
    # Copy coverage files to output
    foreach ($file in $coverageFiles) {
        $destFile = Join-Path $coverageResultsPath $file.Name
        Copy-Item -Path $file.FullName -Destination $destFile -Force
        Write-Log "Coverage file copied: $($file.Name)"
    }
    
    return $true
}

function Get-CoverageMetrics {
    param([string]$CoverageFilePath, [string]$ModuleName)
    
    if (-not (Test-Path $CoverageFilePath)) {
        return $null
    }
    
    try {
        [xml]$coverage = Get-Content $CoverageFilePath
        $packages = $coverage.coverage.packages.package
        
        foreach ($package in $packages) {
            if ($package.name -like "*$ModuleName*" -or $ModuleName -eq "Global") {
                return @{
                    Module = $ModuleName
                    LineRate = [math]::Round([double]$package.lineRate * 100, 2)
                    BranchRate = [math]::Round([double]$package.branchRate * 100, 2)
                    MethodRate = [math]::Round([double]$package.methodRate * 100, 2)
                    LinesCovered = [int]$package.linesCovered
                    LinesUncovered = [int]$package.linesUncovered
                    BranchesCovered = [int]$package.branchesCovered
                    BranchesUncovered = [int]$package.branchesUncovered
                }
            }
        }
    }
    catch {
        Write-Log "Error parsing coverage file $CoverageFilePath: $($_.Exception.Message)" "ERROR"
        return $null
    }
    
    return $null
}

function Test-CoverageThresholds {
    param([hashtable]$Metrics, [string]$ModuleName, [string]$ConfigPath)
    
    $lineThreshold = Get-ConfigValue "$ModuleName.MinimumLineCoverage" $ConfigPath
    $branchThreshold = Get-ConfigValue "$ModuleName.MinimumBranchCoverage" $ConfigPath
    $methodThreshold = Get-ConfigValue "$ModuleName.MinimumMethodCoverage" $ConfigPath
    
    # Use global thresholds if module-specific not found
    if (-not $lineThreshold) { $lineThreshold = Get-ConfigValue "Global.MinimumLineCoverage" $ConfigPath }
    if (-not $branchThreshold) { $branchThreshold = Get-ConfigValue "Global.MinimumBranchCoverage" $ConfigPath }
    if (-not $methodThreshold) { $methodThreshold = Get-ConfigValue "Global.MinimumMethodCoverage" $ConfigPath }
    
    $passed = $true
    $issues = @()
    
    if ($Metrics.LineRate -lt [double]$lineThreshold) {
        $passed = $false
        $issues += "Line coverage ($($Metrics.LineRate)%) below threshold ($lineThreshold%)"
    }
    
    if ($Metrics.BranchRate -lt [double]$branchThreshold) {
        $passed = $false
        $issues += "Branch coverage ($($Metrics.BranchRate)%) below threshold ($branchThreshold%)"
    }
    
    if ($Metrics.MethodRate -lt [double]$methodThreshold) {
        $passed = $false
        $issues += "Method coverage ($($Metrics.MethodRate)%) below threshold ($methodThreshold%)"
    }
    
    return @{
        Passed = $passed
        Issues = $issues
        Thresholds = @{
            Line = $lineThreshold
            Branch = $branchThreshold
            Method = $methodThreshold
        }
    }
}

function Generate-HTMLReport {
    param([array]$CoverageData, [string]$OutputPath)
    
    $htmlPath = Join-Path $OutputPath "coverage-report.html"
    
    $html = @"
<!DOCTYPE html>
<html>
<head>
    <title>TiXL Code Coverage Report</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; }
        .header { background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px; }
        .summary { display: flex; gap: 20px; margin-bottom: 30px; }
        .metric-card { background: white; border: 1px solid #ddd; border-radius: 5px; padding: 15px; flex: 1; text-align: center; }
        .metric-value { font-size: 24px; font-weight: bold; }
        .good { color: #28a745; }
        .warning { color: #ffc107; }
        .bad { color: #dc3545; }
        table { width: 100%; border-collapse: collapse; margin-bottom: 20px; }
        th, td { padding: 12px; text-align: left; border-bottom: 1px solid #ddd; }
        th { background-color: #f8f9fa; font-weight: bold; }
        .status-pass { color: #28a745; font-weight: bold; }
        .status-fail { color: #dc3545; font-weight: bold; }
    </style>
</head>
<body>
    <div class="header">
        <h1>TiXL Code Coverage Report</h1>
        <p>Generated on: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")</p>
    </div>
    
    <div class="summary">
        <div class="metric-card">
            <h3>Average Line Coverage</h3>
            <div class="metric-value $(if ($CoverageData.AverageLineRate -ge 80) { 'good' } elseif ($CoverageData.AverageLineRate -ge 60) { 'warning' } else { 'bad' })">$($CoverageData.AverageLineRate)%</div>
        </div>
        <div class="metric-card">
            <h3>Average Branch Coverage</h3>
            <div class="metric-value $(if ($CoverageData.AverageBranchRate -ge 75) { 'good' } elseif ($CoverageData.AverageBranchRate -ge 55) { 'warning' } else { 'bad' })">$($CoverageData.AverageBranchRate)%</div>
        </div>
        <div class="metric-card">
            <h3>Modules Tested</h3>
            <div class="metric-value">$($CoverageData.Count)</div>
        </div>
    </div>
    
    <table>
        <thead>
            <tr>
                <th>Module</th>
                <th>Line Coverage</th>
                <th>Branch Coverage</th>
                <th>Method Coverage</th>
                <th>Status</th>
            </tr>
        </thead>
        <tbody>
"@

    foreach ($module in $CoverageData) {
        $statusClass = if ($module.ThresholdTest.Passed) { 'status-pass' } else { 'status-fail' }
        $statusText = if ($module.ThresholdTest.Passed) { 'PASS' } else { 'FAIL' }
        
        $html += @"
            <tr>
                <td>$($module.Module)</td>
                <td>$($module.LineRate)%</td>
                <td>$($module.BranchRate)%</td>
                <td>$($module.MethodRate)%</td>
                <td class="$statusClass">$statusText</td>
            </tr>
"@
    }

    $html += @"
        </tbody>
    </table>
    
    <div class="details">
        <h3>Detailed Analysis</h3>
        <h4>Coverage by Module</h4>
        <ul>
"@

    foreach ($module in $CoverageData) {
        if (-not $module.ThresholdTest.Passed) {
            $html += "<li><strong>$($module.Module):</strong> Failed thresholds<br/>"
            foreach ($issue in $module.ThresholdTest.Issues) {
                $html += "  - $issue<br/>"
            }
            $html += "</li>"
        }
    }

    $html += @"
        </ul>
    </div>
</body>
</html>
"@

    $html | Out-File -FilePath $htmlPath -Encoding UTF8
    Write-Log "HTML report generated: $htmlPath"
}

# Main execution
Write-Log "Starting TiXL Coverage Analysis"
Write-Log "Solution: $SolutionPath"
Write-Log "Test Project: $TestProjectPath"
Write-Log "Output: $OutputPath"

# Ensure output directory exists
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

# Run coverage collection
$success = Invoke-CoverageCollection -ProjectPath $TestProjectPath -OutputPath $OutputPath -RunSettings "Tests/CoverletSettings.runsettings"

if (-not $success) {
    Write-Log "Coverage collection failed" "ERROR"
    exit 1
}

# Analyze coverage results
$coverageFiles = Get-ChildItem -Path $OutputPath -Filter "coverage*.cobertura.xml" -Recurse
$coverageData = @()
$allPassed = $true

foreach ($file in $coverageFiles) {
    $fileName = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
    $moduleName = if ($fileName -match 'TiXL\.(\w+)') { $matches[1] } else { "Global" }
    
    $metrics = Get-CoverageMetrics -CoverageFilePath $file.FullName -ModuleName $moduleName
    if ($metrics) {
        $thresholdTest = Test-CoverageThresholds -Metrics $metrics -ModuleName $moduleName -ConfigPath $ConfigPath
        
        $coverageData += @{
            Module = $moduleName
            LineRate = $metrics.LineRate
            BranchRate = $metrics.BranchRate
            MethodRate = $metrics.MethodRate
            LinesCovered = $metrics.LinesCovered
            LinesUncovered = $metrics.LinesUncovered
            BranchesCovered = $metrics.BranchesCovered
            BranchesUncovered = $metrics.BranchesUncovered
            ThresholdTest = $thresholdTest
        }
        
        if (-not $thresholdTest.Passed) {
            $allPassed = $false
            foreach ($issue in $thresholdTest.Issues) {
                Write-Log "$moduleName: $issue" "ERROR"
            }
        } else {
            Write-Log "$moduleName: Coverage thresholds met" "SUCCESS"
        }
    }
}

# Generate reports
if ($GenerateHTMLReport -and $coverageData.Count -gt 0) {
    Generate-HTMLReport -CoverageData $coverageData -OutputPath $OutputPath
}

# Calculate overall metrics
if ($coverageData.Count -gt 0) {
    $averageLineRate = ($coverageData | Measure-Object -Property LineRate -Average).Average
    $averageBranchRate = ($coverageData | Measure-Object -Property BranchRate -Average).Average
    $averageMethodRate = ($coverageData | Measure-Object -Property MethodRate -Average).Average
    
    Write-Log "Overall Coverage Summary:"
    Write-Log "  Average Line Coverage: $averageLineRate%"
    Write-Log "  Average Branch Coverage: $averageBranchRate%"
    Write-Log "  Average Method Coverage: $averageMethodRate%"
    Write-Log "  Modules Analyzed: $($coverageData.Count)"
}

# Generate JSON summary
$summaryPath = Join-Path $OutputPath "coverage-summary.json"
$summary = @{
    GeneratedAt = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssZ")
    TotalModules = $coverageData.Count
    AllThresholdsMet = $allPassed
    OverallMetrics = if ($coverageData.Count -gt 0) @{
        AverageLineRate = [math]::Round($averageLineRate, 2)
        AverageBranchRate = [math]::Round($averageBranchRate, 2)
        AverageMethodRate = [math]::Round($averageMethodRate, 2)
    } else $null
    Modules = $coverageData
} | ConvertTo-Json -Depth 10

$summary | Out-File -FilePath $summaryPath -Encoding UTF8
Write-Log "Coverage summary saved: $summaryPath"

# Exit with appropriate code
if ($FailOnLowCoverage -and (-not $allPassed)) {
    Write-Log "Coverage thresholds not met. Build will fail." "ERROR"
    exit 1
}

Write-Log "Coverage analysis completed successfully"
exit 0