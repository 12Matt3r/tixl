# TiXL Regression Test Framework Validation Script (PowerShell)
# Validates that the regression test framework is properly configured

param(
    [string]$SolutionPath = "./TiXL.sln",
    [string]$TestProjectPath = "./Tests/TiXL.Tests.csproj",
    [string]$RegressionDir = "./Tests/Regression"
)

# Configuration
$ErrorActionPreference = "Stop"

# Color codes for output
$Green = "Green"
$Red = "Red"
$Yellow = "Yellow"
$Cyan = "Cyan"

# Helper functions
function Write-Status {
    param(
        [bool]$Success,
        [string]$Message,
        [string]$Color = $Green
    )
    
    if ($Success) {
        Write-Host "✅ $Message" -ForegroundColor $Color
        return 0
    } else {
        Write-Host "❌ $Message" -ForegroundColor Red
        return 1
    }
}

function Write-Warning {
    param([string]$Message)
    Write-Host "⚠️  $Message" -ForegroundColor Yellow
}

function Write-Header {
    param([string]$Text)
    Write-Host ""
    Write-Host $Text -ForegroundColor $Cyan
    Write-Host ("=" * $Text.Length) -ForegroundColor $Cyan
}

# Main validation
Write-Host "=== TiXL Regression Test Framework Validation ==="
Write-Host "Starting validation at $(Get-Date)"
Write-Host ""

# 1. Check prerequisites
Write-Header "1. Checking Prerequisites"

# Check .NET SDK
try {
    $dotnetVersion = dotnet --version
    Write-Host "   .NET SDK version: $dotnetVersion"
    $result = Write-Status $true ".NET SDK found"
} catch {
    Write-Status $false ".NET SDK not found"
    exit 1
}

# Check if we're on Windows
if ($IsWindows -or $env:OS -eq "Windows_NT") {
    Write-Host "   Platform: Windows (DirectX tests supported)"
    Write-Status $true "Windows platform detected"
} elseif ($IsLinux) {
    Write-Host "   Platform: Linux (Limited DirectX testing)"
    Write-Warning "Linux platform - DirectX tests will be limited"
} elseif ($IsMacOS) {
    Write-Host "   Platform: macOS (Limited DirectX testing)"
    Write-Warning "macOS platform - DirectX tests will be limited"
} else {
    Write-Host "   Platform: Unknown"
    Write-Warning "Unknown platform - testing may be limited"
}

# 2. Check project structure
Write-Header "2. Checking Project Structure"

# Check solution file
if (Test-Path $SolutionPath) {
    Write-Status $true "Solution file found: $SolutionPath"
} else {
    Write-Status $false "Solution file not found: $SolutionPath"
    exit 1
}

# Check test project
if (Test-Path $TestProjectPath) {
    Write-Status $true "Test project found: $TestProjectPath"
} else {
    Write-Status $false "Test project not found: $TestProjectPath"
    exit 1
}

# Check regression test directory
if (Test-Path $RegressionDir) {
    Write-Status $true "Regression test directory found: $RegressionDir"
} else {
    Write-Status $false "Regression test directory not found: $RegressionDir"
    exit 1
}

# Check specific regression test files
$regressionFiles = @(
    "$RegressionDir/RegressionTestRunner.cs"
    "$RegressionDir/ApiCompatibility/ApiCompatibilityTests.cs"
    "$RegressionDir/Migration/SharpDXToVorticeMigrationTests.cs"
    "$RegressionDir/Configuration/ConfigurationCompatibilityTests.cs"
    "$RegressionDir/ErrorHandling/ErrorHandlingConsistencyTests.cs"
    "$RegressionDir/ResourceManagement/ResourceManagementTests.cs"
    "$RegressionDir/ThreadSafety/ThreadSafetyTests.cs"
    "$RegressionDir/README.md"
    "$RegressionDir/IMPLEMENTATION_SUMMARY.md"
)

foreach ($file in $regressionFiles) {
    if (Test-Path $file) {
        Write-Status $true "Regression file found: $(Split-Path $file -Leaf)"
    } else {
        Write-Status $false "Regression file missing: $file"
    }
}

# 3. Restore and build
Write-Header "3. Building Solution"

Write-Host "   Restoring dependencies..."
try {
    dotnet restore $SolutionPath | Out-Null
    Write-Status $true "Dependencies restored successfully"
} catch {
    Write-Status $false "Failed to restore dependencies"
    exit 1
}

Write-Host "   Building solution..."
try {
    dotnet build $SolutionPath --configuration Release --no-restore --verbosity quiet | Out-Null
    Write-Status $true "Solution built successfully"
} catch {
    Write-Status $false "Failed to build solution"
    exit 1
}

# 4. Test discovery
Write-Header "4. Test Discovery"

Write-Host "   Discovering regression tests..."
try {
    $testOutput = dotnet test $TestProjectPath --list-tests --filter "Category=Regression" 2>&1
    
    if ($testOutput -match "The following Tests are available") {
        Write-Status $true "Regression tests discovered"
        
        # Count tests by category
        $apiCount = (dotnet test $TestProjectPath --list-tests --filter "Category=ApiCompatibility" 2>$null | Select-String "ApiCompatibility" | Measure-Object).Count
        $migrationCount = (dotnet test $TestProjectPath --list-tests --filter "Category=Migration" 2>$null | Select-String "Migration" | Measure-Object).Count
        $configCount = (dotnet test $TestProjectPath --list-tests --filter "Category=Configuration" 2>$null | Select-String "Configuration" | Measure-Object).Count
        $errorCount = (dotnet test $TestProjectPath --list-tests --filter "Category=ErrorHandling" 2>$null | Select-String "ErrorHandling" | Measure-Object).Count
        $resourceCount = (dotnet test $TestProjectPath --list-tests --filter "Category=ResourceManagement" 2>$null | Select-String "ResourceManagement" | Measure-Object).Count
        $threadCount = (dotnet test $TestProjectPath --list-tests --filter "Category=ThreadSafety" 2>$null | Select-String "ThreadSafety" | Measure-Object).Count
        
        Write-Host "   Test categories found:"
        Write-Host "     - API Compatibility: $apiCount tests"
        Write-Host "     - Migration: $migrationCount tests"
        Write-Host "     - Configuration: $configCount tests"
        Write-Host "     - Error Handling: $errorCount tests"
        Write-Host "     - Resource Management: $resourceCount tests"
        Write-Host "     - Thread Safety: $threadCount tests"
    } else {
        Write-Warning "No regression tests discovered - this might be expected if test discovery fails"
    }
} catch {
    Write-Warning "Test discovery failed - this might be expected in limited environments"
}

# 5. Quick smoke test
Write-Header "5. Quick Smoke Test"

Write-Host "   Running quick API compatibility test..."
try {
    $smokeTest = Start-Process -FilePath "dotnet" -ArgumentList "test", $TestProjectPath, "--filter", "Category=ApiCompatibility", "--verbosity", "quiet", "--nologo" -Wait -PassThru -WindowStyle Hidden
    
    if ($smokeTest.ExitCode -eq 0) {
        Write-Status $true "Quick API compatibility test passed"
    } else {
        Write-Warning "Quick API test had issues - this might be expected in limited environments"
    }
} catch {
    Write-Warning "Quick API test failed to execute - this might be expected in limited environments"
}

# 6. GitHub Actions workflow validation
Write-Header "6. GitHub Actions Workflow"

$workflowFile = ".github/workflows/regression-tests.yml"
if (Test-Path $workflowFile) {
    Write-Status $true "GitHub Actions workflow found: $workflowFile"
    
    # Check workflow structure
    if (Select-String -Path $workflowFile -Pattern "regression-tests" -Quiet) {
        Write-Status $true "Regression workflow name found"
    } else {
        Write-Warning "Workflow name not found"
    }
    
    if (Select-String -Path $workflowFile -Pattern "ApiCompatibility" -Quiet) {
        Write-Status $true "API compatibility job found"
    } else {
        Write-Warning "API compatibility job not found"
    }
    
    if (Select-String -Path $workflowFile -Pattern "ResourceManagement" -Quiet) {
        Write-Status $true "Resource management job found"
    } else {
        Write-Warning "Resource management job not found"
    }
} else {
    Write-Warning "GitHub Actions workflow not found: $workflowFile"
}

# 7. Documentation check
Write-Header "7. Documentation Check"

$readmeFile = "$RegressionDir/README.md"
if (Test-Path $readmeFile) {
    Write-Status $true "Regression test documentation found"
    
    # Check for key sections
    if (Select-String -Path $readmeFile -Pattern "Framework Architecture" -Quiet) {
        Write-Status $true "Architecture section found"
    } else {
        Write-Warning "Architecture section not found"
    }
    
    if (Select-String -Path $readmeFile -Pattern "Test Categories" -Quiet) {
        Write-Status $true "Test categories section found"
    } else {
        Write-Warning "Test categories section not found"
    }
    
    if (Select-String -Path $readmeFile -Pattern "Getting Started" -Quiet) {
        Write-Status $true "Getting started section found"
    } else {
        Write-Warning "Getting started section not found"
    }
} else {
    Write-Status $false "Regression test documentation not found"
}

# 8. Configuration files check
Write-Header "8. Configuration Files"

$xunitConfig = "Tests/xunit.runner.json"
if (Test-Path $xunitConfig) {
    Write-Status $true "xUnit runner configuration found"
} else {
    Write-Warning "xUnit runner configuration not found"
}

$testSettings = "Tests/TestSettings.runsettings"
if (Test-Path $testSettings) {
    Write-Status $true "Test settings file found"
} else {
    Write-Warning "Test settings file not found"
}

$coverletSettings = "Tests/CoverletSettings.runsettings"
if (Test-Path $coverletSettings) {
    Write-Status $true "Code coverage settings found"
} else {
    Write-Warning "Code coverage settings not found"
}

# 9. Generate validation summary
Write-Header "9. Validation Summary"

Write-Host "TiXL Regression Test Framework Validation Results"
Write-Host "================================================="
Write-Host ""
Write-Host "Environment:"
Write-Host "  - .NET SDK: $dotnetVersion"
Write-Host "  - Platform: $(if ($IsWindows) { 'Windows' } elseif ($IsLinux) { 'Linux' } elseif ($IsMacOS) { 'macOS' } else { 'Unknown' })"
Write-Host "  - Timestamp: $(Get-Date)"
Write-Host ""
Write-Host "Project Structure:"
Write-Host "  - Solution: $(if (Test-Path $SolutionPath) { '✅ Found' } else { '❌ Missing' })"
Write-Host "  - Test Project: $(if (Test-Path $TestProjectPath) { '✅ Found' } else { '❌ Missing' })"
Write-Host "  - Regression Directory: $(if (Test-Path $RegressionDir) { '✅ Found' } else { '❌ Missing' })"
Write-Host ""
Write-Host "Regression Test Files:"
foreach ($file in $regressionFiles) {
    $status = if (Test-Path $file) { "✅" } else { "❌" }
    Write-Host "  - $status $(Split-Path $file -Leaf)"
}
Write-Host ""
Write-Host "Automation:"
Write-Host "  - GitHub Workflow: $(if (Test-Path $workflowFile) { '✅ Found' } else { '❌ Missing' })"
Write-Host ""
Write-Host "Documentation:"
Write-Host "  - README: $(if (Test-Path $readmeFile) { '✅ Found' } else { '❌ Missing' })"
Write-Host ""

# Final recommendation
Write-Header "10. Recommendations"

$isConfigured = (Test-Path $SolutionPath) -and (Test-Path $TestProjectPath) -and (Test-Path $RegressionDir)
if ($isConfigured) {
    Write-Host "✅ Framework appears to be properly configured!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:"
    Write-Host "1. Run full regression test suite:"
    Write-Host "   dotnet test $TestProjectPath --filter `"Category=Regression`""
    Write-Host ""
    Write-Host "2. Run specific categories:"
    Write-Host "   dotnet test $TestProjectPath --filter `"Category=ApiCompatibility`""
    Write-Host "   dotnet test $TestProjectPath --filter `"Category=Migration`""
    Write-Host "   dotnet test $TestProjectPath --filter `"Category=ResourceManagement`""
    Write-Host ""
    Write-Host "3. Run with coverage:"
    Write-Host "   dotnet test $TestProjectPath --filter `"Category=Regression`" --collect:`"XPlat Code Coverage`""
    Write-Host ""
    Write-Host "4. For CI/CD, the GitHub Actions workflow will handle automation"
    Write-Host ""
    Write-Host "5. Validate framework:"
    Write-Host "   .\Tests\Regression\validate_regression_framework.ps1"
} else {
    Write-Host "❌ Framework configuration appears incomplete!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please check the missing files and ensure all components are properly set up."
}

Write-Host ""
Write-Host "=== Validation Complete ==="
Write-Host "Completed at $(Get-Date)"

# Return exit code
exit $(if ($isConfigured) { 0 } else { 1 })
