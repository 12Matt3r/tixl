# TiXL Code Quality Tools - Quick Setup Guide

## Overview

This guide provides a streamlined setup process for implementing comprehensive code quality tools in the TiXL project. The setup includes SonarQube static analysis, code metrics, quality gates, and CI/CD integration.

## Quick Start

### 1. Prerequisites

- .NET 9.0 SDK
- Docker Desktop
- Azure DevOps access (for CI/CD integration)
- SonarQube instance (or use local Docker setup)

### 2. Automated Setup

Run the comprehensive setup script to configure everything:

```powershell
# 1. Setup SonarQube server
.\setup-sonarqube.ps1

# 2. Configure project quality settings
.\configure-quality.ps1

# 3. Update CI/CD pipeline
.\update-cicd-pipeline.ps1
```

### 3. Manual Setup (Alternative)

If you prefer manual setup, follow these steps:

#### Step 1: Deploy SonarQube
```bash
# Start SonarQube server
docker-compose -f docker-compose.sonar.yml up -d

# Access at http://localhost:9000 (admin/admin)
```

#### Step 2: Configure Project
```bash
# Copy configuration files
cp sonar-project.properties ../sonar-project.properties
cp FxCopAnalyzers.ruleset ../FxCopAnalyzers.ruleset

# Generate SonarQube token
dotnet sonarscanner begin /k:"tixl-realtime-graphics" /n:"TiXL Real-time Graphics" /v:"4.1.0"
```

#### Step 3: Update Build Configuration
```xml
<!-- Update Directory.Build.props with quality settings -->
<!-- See code_quality_tools_setup.md for full configuration -->
```

#### Step 4: Run Quality Analysis
```powershell
# Run comprehensive quality analysis
.\check-quality.ps1 -SolutionPath "..\TiXL.sln" -DetailedAnalysis

# Generate metrics report
.\run-metrics-analysis.ps1 -SolutionPath "..\TiXL.sln" -OutputPath "metrics-report.json"
```

## Configuration Files

| File | Purpose | Required |
|------|---------|----------|
| `sonar-project.properties` | SonarQube project configuration | ✅ |
| `FxCopAnalyzers.ruleset` | Code analysis rules | ✅ |
| `quality-gates-config.json` | Quality gate thresholds | ✅ |
| `azure-pipelines-enhanced.yml` | CI/CD pipeline | ✅ |
| `sonar-rules.xml` | Custom SonarQube rules | Optional |

## Quality Gates

The setup enforces the following quality gates:

### Required Gates
- **Code Coverage**: ≥ 75%
- **Duplicated Lines**: ≤ 3%
- **Maintainability Rating**: A
- **Security Rating**: A
- **Vulnerabilities**: 0
- **Code Smells**: ≤ 50
- **Technical Debt**: ≤ 4%

### Warning Thresholds
- **Coverage**: 70-75%
- **Complex Methods**: > 10% of total
- **File Size**: > 300 lines
- **Method Complexity**: > 15

## CI/CD Integration

### Azure DevOps Setup

1. **Install Extensions**:
   - SonarQube
   - Code Coverage

2. **Configure Service Connections**:
   - SonarQube server endpoint
   - Package feed

3. **Set Pipeline Variables**:
   ```
   SONAR_HOST_URL=https://your-sonarqube-instance.com
   SONAR_TOKEN=your_sonarqube_token
   ```

4. **Use Enhanced Pipeline**:
   ```yaml
   # Replace existing azure-pipelines.yml with azure-pipelines-enhanced.yml
   ```

### Quality Gate Integration

```yaml
# Quality gate step in pipeline
- task: SonarQubeQualityGate@5
  displayName: 'Check Quality Gate'
  inputs:
    SonarQube: 'SonarQube'
```

## Local Development

### Running Quality Checks

```powershell
# Comprehensive quality check
.\check-quality.ps1 -SolutionPath "..\TiXL.sln" -DetailedAnalysis

# Code metrics analysis
.\run-metrics-analysis.ps1 -SolutionPath "..\TiXL.sln"

# Warning check
.\check-warnings.ps1 -SolutionPath "..\TiXL.sln"

# All checks combined
.\run-all-quality-checks.ps1
```

### IDE Integration

1. **Visual Studio**:
   - Enable all analyzers
   - Configure code style rules
   - Set up live code analysis

2. **VS Code**:
   - Install C# extension
   - Configure omnisharp settings
   - Enable code analysis

## Monitoring and Reporting

### SonarQube Dashboard

- **URL**: `http://localhost:9000`
- **Project**: `tixl-realtime-graphics`
- **Metrics**: Coverage, Code Smells, Vulnerabilities, Duplication

### Quality Reports

Generated reports are available in:
- `metrics-report.json` - Code metrics analysis
- `duplication-report.md` - Code duplication detection
- `debt-report.json` - Technical debt analysis
- `security-report.json` - Security analysis results

### CI/CD Quality Summary

Quality gates results are published to:
- Pipeline artifacts
- Azure DevOps test results
- SonarQube quality profile

## Troubleshooting

### Common Issues

1. **SonarQube Connection Failed**
   ```bash
   # Check if Docker is running
   docker ps
   
   # Check SonarQube logs
   docker logs tixl-sonarqube
   ```

2. **Analysis Fails with Errors**
   ```bash
   # Run with verbose logging
   dotnet sonarscanner begin /k:"tixl-realtime-graphics" /v:detailed
   ```

3. **Quality Gates Fail**
   - Check the specific gate that's failing
   - Review the recommendations in the quality report
   - Consider adjusting thresholds for your context

### Getting Help

- **Documentation**: See `code_quality_tools_setup.md`
- **Standards**: See `quality-standards-templates.md`
- **Team Support**: Contact the quality engineering team

## Maintenance

### Regular Tasks

1. **Weekly**:
   - Review quality gate trends
   - Update quality rules as needed
   - Check for new security vulnerabilities

2. **Monthly**:
   - Review and update quality standards
   - Analyze technical debt trends
   - Update SonarQube plugins

3. **Quarterly**:
   - Full quality assessment
   - Update CI/CD pipeline if needed
   - Review and update documentation

### Updates

To update the quality tools setup:

```powershell
# Update setup scripts
git pull origin main

# Re-run setup to apply updates
.\setup-sonarqube.ps1 -SkipDocker
```

## Success Metrics

Track these metrics to measure success:

- **Build Success Rate**: > 95%
- **Quality Gate Pass Rate**: > 90%
- **Code Coverage**: > 80%
- **Technical Debt**: < 5%
- **Security Vulnerabilities**: 0 (critical), < 5 (minor)

## Next Steps

After completing the setup:

1. **Train Team** on new quality processes
2. **Monitor Metrics** and adjust thresholds
3. **Iterate** on quality standards based on feedback
4. **Scale** to other projects using similar setup

For detailed information, refer to:
- `code_quality_tools_setup.md` - Comprehensive setup guide
- `quality-standards-templates.md` - Quality standards and templates
- Individual script documentation for specific tools
