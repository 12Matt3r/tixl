param(
    [Parameter(Mandatory=$true)]
    [string]$CoveragePath,
    
    [Parameter(Mandatory=$true)]
    [string]$OutputPath
)

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"
    Write-Host $logMessage
}

# Ensure output directory exists
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

Write-Log "Generating HTML coverage reports from $CoveragePath"

# Find cobertura coverage files
$coberturaFiles = Get-ChildItem -Path $CoveragePath -Filter "*.cobertura.xml" -Recurse

if (-not $coberturaFiles) {
    Write-Log "No cobertura coverage files found" "WARNING"
    exit 0
}

foreach ($file in $coberturaFiles) {
    $fileName = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
    $outputDir = Join-Path $OutputPath $fileName
    
    Write-Log "Processing coverage file: $($file.Name)"
    
    try {
        # Generate HTML report using ReportGenerator
        $reportArgs = @(
            "-reports:",
            "`"$($file.FullName)`"",
            "-targetdir:",
            "`"$outputDir`"",
            "-reporttypes:Html;HtmlSummary;HtmlInline",
            "-verbosity:Verbose"
        )
        
        & reportgenerator @reportArgs
        
        if ($LASTEXITCODE -eq 0) {
            Write-Log "HTML report generated for $($file.Name) in $outputDir"
        } else {
            Write-Log "Failed to generate HTML report for $($file.Name)" "ERROR"
        }
    }
    catch {
        Write-Log "Error generating HTML report for $($file.Name): $($_.Exception.Message)" "ERROR"
    }
}

# Create a consolidated HTML report if multiple files exist
if ($coberturaFiles.Count -gt 1) {
    Write-Log "Creating consolidated coverage report"
    
    $reportArgs = @(
        "-reports:",
        "`"$($coberturaFiles.FullName -join ';')`"",
        "-targetdir:",
        "`"$OutputPath/consolidated`"",
        "-reporttypes:Html;HtmlSummary;HtmlInline;Badges",
        "-verbosity:Verbose"
    )
    
    try {
        & reportgenerator @reportArgs
        
        if ($LASTEXITCODE -eq 0) {
            Write-Log "Consolidated HTML report generated in $OutputPath/consolidated"
        }
    }
    catch {
        Write-Log "Error generating consolidated HTML report: $($_.Exception.Message)" "ERROR"
    }
}

Write-Log "HTML coverage report generation completed"
exit 0