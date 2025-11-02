# Azure DevOps / GitHub Actions CI/CD Integration for TiXL Production Readiness

# ==============================================
# Azure DevOps Pipeline Configuration
# ==============================================

# azure-pipelines-production-readiness.yml
trigger:
  branches:
    include:
      - main
      - develop
  paths:
    include:
      - src/**
      - Tests/**
      - scripts/**

pool:
  vmImage: 'ubuntu-latest'

variables:
  solution: '**/*.sln'
  buildPlatform: 'Any CPU'
  buildConfiguration: 'Release'
  dotNetVersion: '9.0.x'

stages:
  - stage: ProductionReadinessValidation
    displayName: 'Production Readiness Validation'
    jobs:
      - job: ProductionReadinessTests
        displayName: 'Run Production Readiness Tests'
        steps:
          - task: UseDotNet@2
            displayName: 'Use .NET SDK'
            inputs:
              packageType: 'sdk'
              version: '$(dotNetVersion)'

          - task: DotNetCoreCLI@2
            displayName: 'Restore NuGet packages'
            inputs:
              command: 'restore'
              projects: '**/*.csproj'

          - task: DotNetCoreCLI@2
            displayName: 'Build solution'
            inputs:
              command: 'build'
              projects: '$(solution)'
              arguments: '--configuration $(buildConfiguration) --no-restore'

          - task: DotNetCoreCLI@2
            displayName: 'Run Production Readiness Tests'
            inputs:
              command: 'test'
              projects: 'Tests/TiXL.Tests.csproj'
              arguments: '--configuration $(buildConfiguration) --filter "Category=Production" --logger "trx" --results-directory $(Agent.TempDirectory)'

          - task: PublishTestResults@2
            displayName: 'Publish Test Results'
            inputs:
              testResultsFormat: 'VSTest'
              testResultsFiles: '$(Agent.TempDirectory)/*.trx'
              failTaskOnFailedTests: true

          - script: |
              chmod +x scripts/validate-production-readiness.sh
              ./scripts/validate-production-readiness.sh --tests-only
            displayName: 'Run Production Validation Script'

          - task: PublishBuildArtifacts@1
            displayName: 'Publish Validation Reports'
            inputs:
              pathToPublish: 'validation-reports'
              artifactName: 'production-readiness-reports'
              publishLocation: 'Container'

      - job: PerformanceBenchmarks
        displayName: 'Run Performance Benchmarks'
        dependsOn: ProductionReadinessTests
        condition: succeeded()
        steps:
          - task: UseDotNet@2
            displayName: 'Use .NET SDK'
            inputs:
              packageType: 'sdk'
              version: '$(dotNetVersion)'

          - script: |
              chmod +x scripts/validate-production-readiness.sh
              ./scripts/validate-production-readiness.sh --benchmarks-only
            displayName: 'Run Performance Benchmarks'

          - task: PublishBuildArtifacts@1
            displayName: 'Publish Benchmark Results'
            inputs:
              pathToPublish: 'validation-reports'
              artifactName: 'performance-benchmarks'

      - job: SecurityValidation
        displayName: 'Security Validation'
        steps:
          - task: UseDotNet@2
            displayName: 'Use .NET SDK'
            inputs:
              packageType: 'sdk'
              version: '$(dotNetVersion)'

          - script: |
              chmod +x scripts/validate-production-readiness.sh
              ./scripts/validate-production-readiness.sh --security-only
            displayName: 'Run Security Validation'

  - stage: ProductionDeployment
    displayName: 'Production Deployment'
    dependsOn: ProductionReadinessValidation
    condition: and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/main'))
    jobs:
      - deployment: Production
        displayName: 'Deploy to Production'
        environment: 'Production'
        strategy:
          runOnce:
            deploy:
              steps:
                - script: |
                    echo "Production deployment would happen here"
                    # Add actual deployment commands
                  displayName: 'Deploy to Production'

# ==============================================
# GitHub Actions Configuration
# ==============================================

# .github/workflows/production-readiness.yml
name: Production Readiness Validation

on:
  push:
    branches: [ main, develop ]
    paths:
      - 'src/**'
      - 'Tests/**'
      - 'scripts/**'
  pull_request:
    branches: [ main ]
    paths:
      - 'src/**'
      - 'Tests/**'
      - 'scripts/**'

env:
  DOTNET_VERSION: '9.0.x'

jobs:
  production-readiness:
    runs-on: ubuntu-latest
    
    steps:
    - name: Checkout code
      uses: actions/checkout@v4

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: ${{ env.DOTNET_VERSION }}

    - name: Cache NuGet packages
      uses: actions/cache@v4
      with:
        path: ~/.nuget/packages
        key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}

    - name: Restore dependencies
      run: dotnet restore

    - name: Build
      run: dotnet build --configuration Release --no-restore

    - name: Run Production Readiness Tests
      run: dotnet test Tests/TiXL.Tests.csproj --configuration Release --filter "Category=Production" --logger trx --results-directory TestResults

    - name: Publish Test Results
      uses: dorny/test-reporter@v1
      if: always()
      with:
        name: Production Readiness Test Results
        path: TestResults/*.trx
        reporter: trx

    - name: Run Production Validation Script
      run: chmod +x scripts/validate-production-readiness.sh && ./scripts/validate-production-readiness.sh

    - name: Upload Validation Reports
      uses: actions/upload-artifact@v4
      if: always()
      with:
        name: production-readiness-reports
        path: validation-reports/

  security-validation:
    runs-on: ubuntu-latest
    needs: production-readiness
    
    steps:
    - name: Checkout code
      uses: actions/checkout@v4

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: ${{ env.DOTNET_VERSION }}

    - name: Run Security Validation
      run: chmod +x scripts/validate-production-readiness.sh && ./scripts/validate-production-readiness.sh --security-only

    - name: Upload Security Reports
      uses: actions/upload-artifact@v4
      if: always()
      with:
        name: security-validation-reports
        path: validation-reports/security-*.txt

  performance-benchmarks:
    runs-on: ubuntu-latest
    needs: production-readiness
    
    steps:
    - name: Checkout code
      uses: actions/checkout@v4

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: ${{ env.DOTNET_VERSION }}

    - name: Run Performance Benchmarks
      run: chmod +x scripts/validate-production-readiness.sh && ./scripts/validate-production-readiness.sh --benchmarks-only

    - name: Upload Benchmark Results
      uses: actions/upload-artifact@v4
      if: always()
      with:
        name: performance-benchmarks
        path: validation-reports/benchmarks-*.json

  deployment-check:
    runs-on: ubuntu-latest
    needs: [production-readiness, security-validation, performance-benchmarks]
    if: github.ref == 'refs/heads/main'
    
    steps:
    - name: Checkout code
      uses: actions/checkout@v4

    - name: Generate Deployment Checklist
      run: chmod +x scripts/validate-production-readiness.sh && ./scripts/validate-production-readiness.sh --generate-reports

    - name: Upload Deployment Documentation
      uses: actions/upload-artifact@v4
      with:
        name: deployment-documentation
        path: |
          validation-reports/deployment-checklist-*.md
          validation-reports/validation-summary-*.md

# ==============================================
# Jenkins Pipeline Configuration
# ==============================================

# Jenkinsfile
pipeline {
    agent any
    
    environment {
        DOTNET_VERSION = '9.0'
        BUILD_CONFIGURATION = 'Release'
    }
    
    stages {
        stage('Checkout') {
            steps {
                checkout scm
            }
        }
        
        stage('Setup') {
            steps {
                sh '''
                    curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 9.0
                    export PATH="$HOME/.dotnet:$PATH"
                    echo "dotnet version: $(dotnet --version)"
                '''
            }
        }
        
        stage('Build') {
            steps {
                sh '''
                    export PATH="$HOME/.dotnet:$PATH"
                    dotnet restore
                    dotnet build --configuration $BUILD_CONFIGURATION
                '''
            }
        }
        
        stage('Production Readiness Tests') {
            steps {
                sh '''
                    export PATH="$HOME/.dotnet:$PATH"
                    dotnet test Tests/TiXL.Tests.csproj --configuration $BUILD_CONFIGURATION --filter "Category=Production" --logger trx
                '''
            }
            
            post {
                always {
                    publishTRX testResultsPattern: '**/*.trx'
                }
            }
        }
        
        stage('Validation Script') {
            steps {
                sh '''
                    export PATH="$HOME/.dotnet:$PATH"
                    chmod +x scripts/validate-production-readiness.sh
                    ./scripts/validate-production-readiness.sh
                '''
            }
        }
        
        stage('Security Validation') {
            steps {
                sh '''
                    export PATH="$HOME/.dotnet:$PATH"
                    chmod +x scripts/validate-production-readiness.sh
                    ./scripts/validate-production-readiness.sh --security-only
                '''
            }
        }
        
        stage('Performance Benchmarks') {
            steps {
                sh '''
                    export PATH="$HOME/.dotnet:$PATH"
                    chmod +x scripts/validate-production-readiness.sh
                    ./scripts/validate-production-readiness.sh --benchmarks-only
                '''
            }
        }
        
        stage('Generate Reports') {
            steps {
                archiveArtifacts artifacts: 'validation-reports/**/*', fingerprint: true
            }
        }
    }
    
    post {
        always {
            cleanWs()
        }
        success {
            echo 'Production readiness validation PASSED'
        }
        failure {
            echo 'Production readiness validation FAILED'
            emailext (
                subject: "Production Readiness Validation Failed",
                body: "The production readiness validation failed for build ${BUILD_NUMBER}",
                to: "${env.CHANGE_AUTHOR_EMAIL}"
            )
        }
    }
}

# ==============================================
# Quality Gates Configuration
# ==============================================

# quality-gates.yml
quality_gates:
  test_coverage:
    minimum: 80
    measurement: line_coverage
    fail_on_below: true
    
  performance:
    max_regression_percentage: 5
    baseline_metric: operations_per_second
    
  security:
    vulnerabilities_allowed: 0
    severity_threshold: high
    
  reliability:
    test_success_rate: 100
    required_categories:
      - Production.Readiness
      - Production.ErrorHandling
      - Production.Security
      
  memory:
    max_memory_leak_mb: 10
    measurement_window: 1_hour

# ==============================================
# Monitoring Configuration
# ==============================================

# monitoring-alerts.yml
alerts:
  deployment_failed:
    condition: deployment_status == "failed"
    severity: critical
    notification_channels:
      - email: devops@company.com
      - slack: "#devops-alerts"
      
  production_tests_failed:
    condition: test_success_rate < 100
    severity: high
    notification_channels:
      - email: development@company.com
      
  performance_regression:
    condition: performance_regression > 5%
    severity: medium
    notification_channels:
      - slack: "#performance-alerts"
      
  security_scan_failed:
    condition: security_vulnerabilities > 0
    severity: critical
    notification_channels:
      - email: security@company.com
      - slack: "#security-alerts"

# ==============================================
# Deployment Approval Workflow
# ==============================================

# deployment-approval.yml
approval_workflow:
  required_approvers:
    - role: "Technical Lead"
    - role: "DevOps Lead" 
    - role: "Security Lead"
    
  automated_checks:
    - production_readiness_tests
    - security_validation
    - performance_benchmarks
    - code_coverage
    
  manual_checks:
    - stakeholder_review
    - change_management_approval
    - rollback_plan_verification
    
  notification_channels:
    email: deployment-approvals@company.com
    slack: "#deployments"
    
  escalation_timeout: 4_hours

# ==============================================
# Usage Instructions
# ==============================================

"""
To use these CI/CD configurations:

1. Azure DevOps:
   - Copy azure-pipelines-production-readiness.yml to your repository root
   - Configure the 'Production' environment in Azure DevOps
   - Update variable groups as needed

2. GitHub Actions:
   - Copy .github/workflows/production-readiness.yml to your repository
   - Update environment-specific configurations
   - Configure required secrets and permissions

3. Jenkins:
   - Copy Jenkinsfile to your repository root
   - Configure Jenkins pipeline triggers
   - Update email notifications and recipients

4. Quality Gates:
   - Customize quality gates based on your requirements
   - Update thresholds and measurement criteria
   - Configure notification channels

5. Monitoring:
   - Set up alerts based on your monitoring system
   - Configure notification channels (email, Slack, etc.)
   - Customize escalation procedures

Key Integration Points:
- All scripts check for production readiness before deployment
- Automated validation prevents deployment of non-production-ready code
- Performance benchmarks detect regressions
- Security validation ensures compliance
- Deployment approval workflow adds manual oversight
- Comprehensive reporting for audit trails
"""