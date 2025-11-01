# TiXL Code Coverage System

Comprehensive code coverage analysis and reporting system for TiXL using Coverlet.

## Quick Start

### Prerequisites

- .NET 9.0 SDK
- PowerShell 7+
- ReportGenerator tool (`dotnet tool install -g ReportGenerator`)

### Local Usage

```bash
# Run complete coverage analysis
pwsh ./docs/scripts/coverage-analyzer.ps1 \
  -SolutionPath "TiXL.sln" \
  -TestProjectPath "Tests/TiXL.Tests.csproj" \
  -OutputPath "./coverage-reports" \
  -GenerateHTMLReport

# Run quality gate check
pwsh ./docs/scripts/coverage-quality-gate.ps1 \
  -CoverageSummaryPath "./coverage-reports/coverage-summary.json" \
  -ConfigPath "./docs/config/coverage-thresholds.config"
```

### CI/CD Integration

#### Azure DevOps

Use the enhanced pipeline:
```yaml
# .azure-pipelines/enhanced-coverage-pipeline.yml
extends: /docs/pipelines/enhanced-coverage-pipeline.yml
```

#### GitHub Actions

Use the workflow:
```yaml
# .github/workflows/tixl-coverage-ci.yml
name: TiXL CI/CD with Code Coverage
```

## System Components

### Core Scripts

- **`coverage-analyzer.ps1`**: Main coverage collection and analysis
- **`coverage-quality-gate.ps1`**: Quality gate validation
- **`generate-coverage-report.ps1`**: HTML report generation
- **`comprehensive-quality-gate.ps1`**: Overall quality assessment

### Configuration Files

- **`coverage-thresholds.config`**: Coverage thresholds by module
- **`coverage-baseline.json`**: Historical coverage baseline
- **`CoverletSettings.runsettings`**: Coverlet configuration

### Pipeline Files

- **`enhanced-coverage-pipeline.yml`**: Azure DevOps pipeline
- **`tixl-coverage-ci.yml`**: GitHub Actions workflow

## Coverage Thresholds

| Module | Line | Branch | Method |
|--------|------|--------|---------|
| Core | 80% | 75% | 85% |
| Operators | 75% | 70% | 80% |
| Gfx | 70% | 65% | 75% |
| Gui | 65% | 60% | 70% |
| Editor | 70% | 65% | 75% |
| Resources | 85% | 80% | 90% |

## Output Reports

### Coverage Summary (`coverage-summary.json`)

```json
{
  "GeneratedAt": "2025-11-02T03:33:34Z",
  "OverallMetrics": {
    "AverageLineRate": 78.5,
    "AverageBranchRate": 72.3,
    "AverageMethodRate": 83.1
  },
  "Modules": [...]
}
```

### HTML Reports

- `coverage-report.html`: Main coverage report
- `html-reports/`: Detailed module reports
- `consolidated/`: Combined report for all modules

### Quality Reports

- `coverage-quality-analysis.json`: Detailed quality analysis
- `github-coverage-comment.md`: PR comment template

## Quality Gates

The system implements multiple quality checks:

1. **Coverage Thresholds**: Ensure minimum coverage levels
2. **Regression Detection**: Compare against historical baselines
3. **Security Scanning**: Validate no critical vulnerabilities
4. **Overall Quality Score**: Comprehensive assessment

### Fail Conditions

Build fails when:
- Any module below minimum thresholds
- Coverage regression detected (>2% drop)
- Critical security issues found
- Overall quality score <80

## Troubleshooting

### Common Issues

**No coverage files generated:**
```bash
# Verify Coverlet installation
dotnet list package --project Tests/TiXL.Tests.csproj | findstr coverlet

# Check test execution
dotnet test Tests/TiXL.Tests.csproj --no-build
```

**Coverage below threshold:**
- Review HTML reports for gaps
- Add tests for uncovered critical paths
- Consider threshold adjustment if justified

**Slow coverage collection:**
- Enable parallel execution in `xunit.runner.json`
- Exclude slow integration tests
- Use `[ExcludeFromCodeCoverage]` attribute

### Debug Mode

```powershell
# Enable verbose logging
./coverage-analyzer.ps1 -Verbose

# Check specific module coverage
./coverage-analyzer.ps1 -ModuleFilter "Core"
```

## Performance

### Typical Execution Times

- Coverage Collection: 2-5 minutes
- HTML Report Generation: 30-60 seconds
- Quality Gate Analysis: 10-30 seconds
- Total Pipeline Impact: 3-7 minutes

### Optimization Tips

1. Use `--no-build` flag for test runs
2. Enable parallel test execution
3. Exclude long-running integration tests
4. Cache test results when possible

## Contributing

### Adding New Modules

1. Update `coverage-thresholds.config`
2. Modify `CoverletSettings.runsettings` include patterns
3. Update baseline data in `coverage-baseline.json`
4. Add module-specific test categories

### Extending Quality Gates

```powershell
function Test-CustomQualityCriteria {
    param([hashtable]$CoverageData)
    
    # Your custom validation logic
    return @{ Passed = $true }
}

# Integrate in comprehensive quality gate
$customResult = Test-CustomQualityCriteria -CoverageData $coverageData
```

## Best Practices

### Test Organization

- Separate unit and integration tests
- Use `[ExcludeFromCodeCoverage]` appropriately
- Focus on behavior, not implementation

### Coverage Goals

- Prioritize business-critical code
- Balance coverage with test quality
- Maintain test suite maintainability

### CI/CD Integration

- Fail fast on coverage issues
- Provide actionable failure reasons
- Track coverage trends over time

## Support

- 📖 Full documentation: [`docs/TIXL-044_CODE_COVERAGE_IMPLEMENTATION.md`](docs/TIXL-044_CODE_COVERAGE_IMPLEMENTATION.md)
- 🐛 Issues: Report via GitHub Issues
- 💬 Discussions: Use GitHub Discussions
- 📧 Questions: Contact DevOps team

## License

This coverage system is part of TiXL and follows the same licensing terms.