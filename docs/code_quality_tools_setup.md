# TiXL Code Quality Tools Setup

## Overview

This document outlines the comprehensive code quality and static analysis setup for TiXL, a real-time motion graphics creation tool built on .NET 9.0. The setup includes static analysis, code complexity analysis, duplication detection, technical debt tracking, and CI/CD integration.

## Table of Contents

1. [Static Analysis Integration](#static-analysis-integration)
2. [Code Quality Tools](#code-quality-tools)
3. [CI/CD Pipeline Integration](#cicd-pipeline-integration)
4. [Quality Gates](#quality-gates)
5. [Configuration Files](#configuration-files)
6. [Quality Standards](#quality-standards)
7. [Templates and Scripts](#templates-and-scripts)
8. [Setup Instructions](#setup-instructions)

## Static Analysis Integration

### SonarQube Integration

#### Server Setup

**Docker Compose Configuration:**
```yaml
# docker-compose.sonar.yml
version: '3.8'
services:
  sonarqube:
    image: sonarqube:10.6-community
    container_name: tixl-sonarqube
    ports:
      - "9000:9000"
    environment:
      - SONAR_ES_BOOTSTRAP_CHECKS_DISABLE=true
      - sonar.forceAuthentication=true
      - sonar.security.realm=LDAP
      - sonar.auth.gitlab.enabled=true
      - sonar.auth.gitlab.allowUsersToSignUp=true
    volumes:
      - sonarqube_conf:/opt/sonarqube/conf
      - sonarqube_extensions:/opt/sonarqube/extensions
      - sonarqube_logs:/opt/sonarqube/logs
      - sonarqube_data:/opt/sonarqube/data
    restart: unless-stopped

  postgres:
    image: postgres:15
    container_name: tixl-postgres
    environment:
      - POSTGRES_USER=sonar
      - POSTGRES_PASSWORD=sonar
      - POSTGRES_DB=sonarqube
    volumes:
      - postgres_data:/var/lib/postgresql/data
    restart: unless-stopped

volumes:
  sonarqube_conf:
  sonarqube_extensions:
  sonarqube_logs:
  sonarqube_data:
  postgres_data:
```

#### SonarQube Project Configuration

**sonar-project.properties:**
```properties
# SonarQube Project Configuration for TiXL
sonar.projectKey=tixl-realtime-graphics
sonar.projectName=TiXL - Real-time Motion Graphics Engine
sonar.projectVersion=4.1.0
sonar.organization=tixl3d

# Source and Test Files
sonar.sources=.
sonar.tests=Tests
sonar.exclusions=**/*.cs,**/bin/**,**/obj/**,**/.vs/**
sonar.inclusions=**/*.cs

# Language Configuration
sonar.cs.analyzer.projectOutPaths=/
sonar.cs.analyzer.projectHomePaths=/
sonar.cs.file.suffixes=.cs
sonar.vbnet.file.suffixes=.vb

# Analysis Parameters
sonar.cs.ignoreHeaderComments=true
sonar.cs.dotcover.reportsPaths=**/*.coverage
sonar.cs.opencover.reportsPaths=**/coverage.opencover.xml
sonar.cs.vstest.reportsPaths=**/*.trx

# Quality Gate Configuration
sonar.qualitygate.wait=true

# Exclusions for Generated Code
sonar.exclusions=**/Generated/**/*.cs,**/Migrations/**/*.cs,**/Auto-generated/**/*.cs

# Dependencies Analysis
sonar.java.libraries=**/bin/Release/**/*.dll
sonar.cs.opencover.reportsPaths=**/coverage.opencover.xml
sonar.cs.dotcover.reportsPaths=**/*.coverage

# Code Smell Detection
sonar.cs.roslyn.ignoreExternalIssues=false

# Custom Rules for Real-time Graphics
sonar.cs.roslyn.ruleFile=./sonar-rules.xml

# Technical Debt Settings
sonar.scm.exclusions.disabled=true
sonar.vbnet.ignoreHeaderComments=true

# Build Configuration
sonar.buildString=Azure DevOps Build #{Build.BuildNumber}
sonar.buildConfiguration=Release
sonar.pullrequest.key=${PR_NUMBER}
sonar.pullrequest.branch=${BRANCH_NAME}
sonar.pullrequest.base=${TARGET_BRANCH}
```

#### Enhanced Directory.Build.props with SonarQube

```xml
<!-- Enhanced Directory.Build.props with code quality settings -->
<Project>
  <PropertyGroup>
    <!-- Target Framework -->
    <TargetFramework>net9.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    
    <!-- Nullable Reference Types -->
    <Nullable>enable</Nullable>
    
    <!-- Warning Configuration - Strict -->
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsAsErrors />
    <WarningsNotAsErrors>NU1701,CS1591</WarningsNotAsErrors>
    
    <!-- Code Analysis - Comprehensive -->
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <AnalysisMode>AllEnabledByDefault</AnalysisMode>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    
    <!-- Output Configuration -->
    <OutputPath>bin\$(Configuration)\$(AssemblyName)</OutputPath>
    <BaseIntermediateOutputPath>obj\$(Configuration)\$(AssemblyName)</BaseIntermediateOutputPath>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    
    <!-- Debug Configuration -->
    <DebugType>portable</DebugType>
    <DebugSymbols>true</DebugSymbols>
    
    <!-- Optimization -->
    <Optimize>true</Optimize>
    <ServerGarbageCollection>false</ServerGarbageCollection>
    
    <!-- Strong Naming -->
    <SignAssembly>false</SignAssembly>
    
    <!-- Assembly Attributes -->
    <GenerateAssemblyInfo>true</GenerateAssemblyInfo>
    <Deterministic>true</Deterministic>
    
    <!-- Source Link -->
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    
    <!-- SonarQube Integration -->
    <SonarQubeTestProject>true</SonarQubeTestProject>
    <SonarQubeExclude>false</SonarQubeExclude>
    <SonarProjectKey>$(AssemblyName)</SonarProjectKey>
  </PropertyGroup>

  <!-- Enhanced Package Configuration -->
  <PropertyGroup Condition="'$(Configuration)' == 'Release'">
    <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
    <PackageId>TiXL</PackageId>
    <PackageVersion>4.1.0</PackageVersion>
    <Authors>TiXL Team</Authors>
    <Description>TiXL - Real-time motion graphics creation tool</Description>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/tixl3d/tixl</PackageProjectUrl>
    <RepositoryUrl>https://github.com/tixl3d/tixl</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
  </PropertyGroup>

  <!-- Testing Configuration -->
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <GenerateTestingPlatform1Manifest>true</GenerateTestingPlatform1Manifest>
  </PropertyGroup>

  <!-- Enhanced Package References -->
  <ItemGroup>
    <!-- Code Analyzers - Comprehensive -->
    <PackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" Version="9.0.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
    
    <!-- SonarQube Analyzer -->
    <PackageReference Include="SonarAnalyzer.CSharp" Version="9.16.0.0" PrivateAssets="All">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
    
    <!-- Security Analyzers -->
    <PackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" Version="9.0.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
    
    <!-- Source Link -->
    <PackageReference Include="Microsoft.SourceLink.GitHub" Version="8.0.0" PrivateAssets="All" />
    <PackageReference Include="Microsoft.Build.NoTargets" Version="3.7.0" PrivateAssets="All" Condition="'$(MSBuildProjectName)' != 'TiXL' AND '$(MSBuildProjectName)' != 'TiXL.Desktop'" />
    
    <!-- Performance Analysis -->
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.0" PrivateAssets="All" />
  </ItemGroup>

  <!-- Test Project Specific -->
  <ItemGroup Condition="$(IsTestProject)">
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
    <PackageReference Include="coverlet.collector" Version="6.0.2">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
    <PackageReference Include="FluentAssertions" Version="6.12.2" />
    <PackageReference Include="Moq" Version="4.20.72" />
    <PackageReference Include="BenchmarkDotNet" Version="0.13.12" PrivateAssets="All" />
  </ItemGroup>

  <!-- Debug-specific Package References -->
  <ItemGroup Condition="'$(Configuration)' == 'Debug'">
    <PackageReference Include="Microsoft.Extensions.Logging.Debug" Version="9.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="9.0.0" />
  </ItemGroup>

  <!-- Build Targets for Quality Analysis -->
  <Target Name="RunSonarAnalysis" AfterTargets="Build" Condition="'$(CI)' == 'true'">
    <Exec Command="dotnet sonarscanner begin /k:$(AssemblyName) /n:$(AssemblyName) /v:$(PackageVersion) /d:sonar.host.url=$(SONAR_HOST_URL) /d:sonar.login=$(SONAR_TOKEN) /d:sonar.cs.opencover.reportsPaths=$(MSBuildProjectDirectory)/**/coverage.opencover.xml" />
  </Target>

  <Target Name="EndSonarAnalysis" AfterTargets="Pack" Condition="'$(CI)' == 'true'">
    <Exec Command="dotnet sonarscanner end /d:sonar.login=$(SONAR_TOKEN)" />
  </Target>
</Project>
```

## Code Quality Tools

### 1. Code Metrics and Complexity Analysis

**CodeMetrics.targets:**
```xml
<!-- CodeMetrics.targets -->
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <ItemGroup>
    <CodeMetricsTools Include="Microsoft.CodeAnalysis.Metrics" />
    <CodeMetricsTools Include="NDepend" Condition="'$(IncludeNDepend)' == 'true'" />
  </ItemGroup>

  <Target Name="CalculateCodeMetrics" AfterTargets="Build">
    <PropertyGroup>
      <MetricsOutputFile>$(MSBuildProjectDirectory)/metrics-report.json</MetricsOutputFile>
      <MetricsHtmlOutput>$(MSBuildProjectDirectory)/metrics-report.html</MetricsHtmlOutput>
    </PropertyGroup>

    <!-- Run code metrics analysis -->
    <Exec Command="dotnet tool run metrics-to-json --project $(MSBuildProjectFullPath) --output $(MetricsOutputFile) --format json" 
          Condition="Exists('$(MSBuildProjectFullPath)')" />
    
    <!-- Generate HTML report -->
    <Exec Command="dotnet tool run metrics-to-json --project $(MSBuildProjectFullPath) --output $(MetricsHtmlOutput) --format html" 
          Condition="Exists('$(MSBuildProjectFullPath)')" />
  </Target>
</Project>
```

### 2. Code Duplication Detection

**DuplicateCode.targets:**
```xml
<!-- DuplicateCode.targets -->
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Target Name="DetectCodeDuplication" AfterTargets="Build">
    <PropertyGroup>
      <DuplicateOutputFile>$(MSBuildProjectDirectory)/duplication-report.md</DuplicateOutputFile>
    </PropertyGroup>

    <!-- Run duplication analysis -->
    <Exec Command="dotnet tool run jb duplicatesfinder $(MSBuildProjectDirectory) --solution=$(SolutionPath) --format markdown --output=$(DuplicateOutputFile)" 
          Condition="Exists('$(SolutionPath)')" />
  </Target>
</Project>
```

### 3. Technical Debt Tracking

**TechnicalDebt.targets:**
```xml
<!-- TechnicalDebt.targets -->
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Target Name="CalculateTechnicalDebt" AfterTargets="Build">
    <PropertyGroup>
      <DebtOutputFile>$(MSBuildProjectDirectory)/technical-debt-report.json</DebtOutputFile>
      <DebtSummaryFile>$(MSBuildProjectDirectory)/technical-debt-summary.md</DebtSummaryFile>
    </PropertyGroup>

    <!-- Run technical debt analysis -->
    <Exec Command="dotnet tool run tech-debt-analyzer --project $(MSBuildProjectFullPath) --output $(DebtOutputFile) --format json" />
    
    <!-- Generate debt summary -->
    <Exec Command="dotnet tool run tech-debt-analyzer --project $(MSBuildProjectFullPath) --output $(DebtSummaryFile) --format markdown" />
  </Target>
</Project>
```

## CI/CD Pipeline Integration

### Enhanced Azure DevOps Pipeline

```yaml
# azure-pipelines.yml - Enhanced with comprehensive quality checks
trigger:
- main
- develop

pr:
- main
- develop

pool:
  vmImage: 'windows-latest'

variables:
  buildConfiguration: 'Release'
  dotnetVersion: '9.0.x'
  solutionPath: 'TiXL.sln'
  sonarHostUrl: 'https://your-sonarqube-instance.com'
  sonarProjectKey: 'tixl-realtime-graphics'

stages:
- stage: CodeQuality
  displayName: 'Code Quality Analysis'
  jobs:
  - job: SonarAnalysis
    displayName: 'SonarQube Analysis'
    steps:
    - task: UseDotNet@2
      displayName: 'Use .NET 9.0'
      inputs:
        packageType: 'sdk'
        version: $(dotnetVersion)

    - task: SonarQubePrepare@5
      displayName: 'Prepare SonarQube Analysis'
      inputs:
        SonarQube: 'SonarQube'
        scannerMode: 'MSBuild'
        projectKey: 'tixl-realtime-graphics'
        projectName: 'TiXL Real-time Graphics'
        extraProperties: |
          # Additional properties for C# analysis
          sonar.cs.analyzer.projectOutPaths=/
          sonar.cs.analyzer.projectHomePaths=/
          sonar.exclusions=**/bin/**,**/obj/**,**/.vs/**,**/Generated/**/*.cs
          sonar.inclusions=**/*.cs
          sonar.cs.dotcover.reportsPaths=**/*.coverage
          sonar.cs.opencover.reportsPaths=**/coverage.opencover.xml
          sonar.qualitygate.wait=true

    - task: DotNetCoreCLI@2
      displayName: 'Restore dependencies'
      inputs:
        command: 'restore'
        projects: '$(solutionPath)'

    - task: PowerShell@2
      displayName: 'Pre-build quality check'
      inputs:
        targetType: 'filePath'
        filePath: '$(System.DefaultWorkingDirectory)/docs/check-quality.ps1'
        arguments: '-SolutionPath "$(solutionPath)" -BuildConfiguration "$(buildConfiguration)"'
        errorActionPreference: 'continue'

    - task: DotNetCoreCLI@2
      displayName: 'Build with SonarQube analysis'
      inputs:
        command: 'build'
        projects: '$(solutionPath)'
        arguments: '--configuration $(buildConfiguration) /p:TreatWarningsAsErrors=true /p:EnforceCodeStyleInBuild=true /p:SonarQube=true /v:minimal'
      continueOnError: false

    - task: DotNetCoreCLI@2
      displayName: 'Publish build artifacts'
      inputs:
        command: 'publish'
        projects: '$(solutionPath)'
        arguments: '--configuration $(buildConfiguration) --output $(Build.ArtifactStagingDirectory)'

    - task: SonarQubeAnalyze@5
      displayName: 'Run SonarQube Analysis'
      inputs:
        SonarQube: 'SonarQube'

    - task: SonarQubeQualityGate@5
      displayName: 'Check Quality Gate'
      inputs:
        SonarQube: 'SonarQube'

- stage: StaticAnalysis
  displayName: 'Static Analysis'
  dependsOn: CodeQuality
  jobs:
  - job: ComprehensiveStaticAnalysis
    displayName: 'Comprehensive Analysis'
    steps:
    - task: UseDotNet@2
      displayName: 'Use .NET 9.0'
      inputs:
        packageType: 'sdk'
        version: $(dotnetVersion)

    - task: DotNetCoreCLI@2
      displayName: 'Restore dependencies'
      inputs:
        command: 'restore'
        projects: '$(solutionPath)'

    - task: PowerShell@2
      displayName: 'Run Code Metrics Analysis'
      inputs:
        targetType: 'filePath'
        filePath: '$(System.DefaultWorkingDirectory)/docs/run-metrics-analysis.ps1'
        arguments: '-SolutionPath "$(solutionPath)" -OutputPath "$(Build.ArtifactStagingDirectory)/metrics-report.json"'
        errorActionPreference: 'continue'

    - task: PowerShell@2
      displayName: 'Detect Code Duplication'
      inputs:
        targetType: 'filePath'
        filePath: '$(System.DefaultWorkingDirectory)/docs/run-duplication-analysis.ps1'
        arguments: '-SolutionPath "$(solutionPath)" -OutputPath "$(Build.ArtifactStagingDirectory)/duplication-report.md"'
        errorActionPreference: 'continue'

    - task: PowerShell@2
      displayName: 'Calculate Technical Debt'
      inputs:
        targetType: 'filePath'
        filePath: '$(System.DefaultWorkingDirectory)/docs/run-debt-analysis.ps1'
        arguments: '-SolutionPath "$(solutionPath)" -OutputPath "$(Build.ArtifactStagingDirectory)/debt-report.json"'
        errorActionPreference: 'continue'

    - task: PowerShell@2
      displayName: 'Security Analysis'
      inputs:
        targetType: 'filePath'
        filePath: '$(System.DefaultWorkingDirectory)/docs/run-security-analysis.ps1'
        arguments: '-SolutionPath "$(solutionPath)" -OutputPath "$(Build.ArtifactStagingDirectory)/security-report.json"'
        errorActionPreference: 'continue'

    - task: PublishBuildArtifacts@1
      displayName: 'Publish Analysis Reports'
      inputs:
        PathtoPublish: '$(Build.ArtifactStagingDirectory)'
        ArtifactName: 'quality-analysis'
        publishLocation: 'Container'

- stage: Test
  displayName: 'Run Tests'
  dependsOn: StaticAnalysis
  jobs:
  - job: RunTests
    displayName: 'Unit Tests with Coverage'
    steps:
    - task: UseDotNet@2
      displayName: 'Use .NET 9.0'
      inputs:
        packageType: 'sdk'
        version: $(dotnetVersion)

    - task: DotNetCoreCLI@2
      displayName: 'Restore test dependencies'
      inputs:
        command: 'restore'
        projects: '**/*Tests.csproj'

    - task: DotNetCoreCLI@2
      displayName: 'Run tests with coverage'
      inputs:
        command: 'test'
        projects: '**/*Tests.csproj'
        arguments: '--configuration $(buildConfiguration) --no-build --collect:"XPlat Code Coverage" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura'
        publishTestResults: true

    - task: PublishCodeCoverageResults@1
      displayName: 'Publish code coverage'
      inputs:
        codeCoverageTool: 'Cobertura'
        summaryFileLocation: '**/coverage.cobertura.xml'

- stage: Performance
  displayName: 'Performance Analysis'
  dependsOn: Test
  condition: succeeded()
  jobs:
  - job: PerformanceAnalysis
    displayName: 'Performance Benchmarks'
    steps:
    - task: UseDotNet@2
      displayName: 'Use .NET 9.0'
      inputs:
        packageType: 'sdk'
        version: $(dotnetVersion)

    - task: DotNetCoreCLI@2
      displayName: 'Restore benchmark dependencies'
      inputs:
        command: 'restore'
        projects: '**/*Benchmark.csproj'

    - task: DotNetCoreCLI@2
      displayName: 'Run performance benchmarks'
      inputs:
        command: 'run'
        projects: '**/*Benchmark.csproj'
        arguments: '--configuration $(buildConfiguration) -- --exporters json --output $(Build.ArtifactStagingDirectory)/benchmark-results.json'
      continueOnError: true

    - task: PublishBuildArtifacts@1
      displayName: 'Publish benchmark results'
      inputs:
        PathtoPublish: '$(Build.ArtifactStagingDirectory)'
        ArtifactName: 'performance-results'
        publishLocation: 'Container'

- stage: QualityGates
  displayName: 'Quality Gates'
  dependsOn:
  - CodeQuality
  - StaticAnalysis
  - Test
  - Performance
  condition: always()
  jobs:
  - job: QualityGateCheck
    displayName: 'Final Quality Gate Check'
    steps:
    - task: PowerShell@2
      displayName: 'Comprehensive Quality Check'
      inputs:
        targetType: 'filePath'
        filePath: '$(System.DefaultWorkingDirectory)/docs/run-quality-gates.ps1'
        arguments: '-BuildArtifactPath "$(Build.ArtifactStagingDirectory)" -OutputPath "$(Build.ArtifactStagingDirectory)/quality-gate-report.md"'
        errorActionPreference: 'stop'

    - task: PublishBuildArtifacts@1
      displayName: 'Publish quality gate report'
      inputs:
        PathtoPublish: '$(Build.ArtifactStagingDirectory)'
        ArtifactName: 'quality-gate-reports'
        publishLocation: 'Container'

- stage: Package
  displayName: 'Create Packages'
  dependsOn: QualityGates
  condition: and(succeeded(), in(variables['Build.SourceBranch'], 'refs/heads/main', 'refs/heads/develop'))
  jobs:
  - job: CreatePackages
    displayName: 'NuGet Packages'
    steps:
    - task: UseDotNet@2
      displayName: 'Use .NET 9.0'
      inputs:
        packageType: 'sdk'
        version: $(dotnetVersion)

    - task: DotNetCoreCLI@2
      displayName: 'Restore dependencies'
      inputs:
        command: 'restore'
        projects: '$(solutionPath)'

    - task: DotNetCoreCLI@2
      displayName: 'Create NuGet packages'
      inputs:
        command: 'pack'
        projects: '$(solutionPath)'
        arguments: '--configuration $(buildConfiguration) --no-build /p:TreatWarningsAsErrors=true /p:PackageOutputPath=$(Build.ArtifactStagingDirectory)'

    - task: NuGetCommand@2
      displayName: 'Push packages to internal feed'
      condition: and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/main'))
      inputs:
        command: 'push'
        packagesToPush: '$(Build.ArtifactStagingDirectory)/**/*.nupkg'
        nuGetFeedType: 'internal'
        publishVstsFeed: '$(System.TeamProject)/$(Build.Repository.Name)/_packaging/$(System.TeamProject)/nuget/v3/index.json'
        allowPackageConflicts: false

    - task: PublishBuildArtifacts@1
      displayName: 'Publish packages'
      inputs:
        PathtoPublish: '$(Build.ArtifactStagingDirectory)'
        ArtifactName: 'packages'
        publishLocation: 'Container'
```

## Quality Gates

### Quality Gate Configuration

**quality-gates-config.json:**
```json
{
  "qualityGates": [
    {
      "name": "SonarQube Quality Gate",
      "type": "SonarQube",
      "thresholds": {
        "coverage": 80,
        "duplicated_lines_density": 3,
        "maintainability_rating": "A",
        "reliability_rating": "A",
        "security_rating": "A",
        "vulnerabilities": 0,
        "code_smells": 50,
        "bugs": 0,
        "technical_debt": 4
      },
      "required": true
    },
    {
      "name": "Code Coverage Gate",
      "type": "Custom",
      "thresholds": {
        "line_coverage": 75,
        "branch_coverage": 70,
        "method_coverage": 80
      },
      "required": true
    },
    {
      "name": "Technical Debt Gate",
      "type": "Custom",
      "thresholds": {
        "debt_ratio": 5,
        "sustainability_index": 0.8
      },
      "required": true
    },
    {
      "name": "Performance Gate",
      "type": "Custom",
      "thresholds": {
        "benchmark_execution_time": 5000,
        "memory_allocation": 100
      },
      "required": false
    },
    {
      "name": "Security Gate",
      "type": "Custom",
      "thresholds": {
        "vulnerabilities": 0,
        "security_hotspots": 5
      },
      "required": true
    }
  ],
  "failureActions": [
    {
      "action": "failBuild",
      "condition": "requiredGateFailed"
    },
    {
      "action": "notifyTeam",
      "condition": "anyGateFailed"
    },
    {
      "action": "createIssue",
      "condition": "criticalGateFailed"
    }
  ]
}
```

## Configuration Files

### 1. SonarQube Rules Configuration

**sonar-rules.xml:**
```xml
<?xml version="1.0" encoding="UTF-8"?>
<rules xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
       xmlns="http://www.sonarsource.com/msft/CTRs"
       xsi:schemaLocation="http://www.sonarsource.com/msft/CTRs 
                           https://raw.githubusercontent.com/SonarSource/sonarlint-visualstudio/master/src/RuleKeys/RuleKeys.xsd">
  
  <!-- C# Specific Rules for Real-time Graphics Applications -->
  <Rule Key="S1185" Action="Info" Description="Remove unnecessary 'ToString' calls in rendering code"/>
  <Rule Key="S2953" Action="Warning" Description="Remove unused private fields in graphics components"/>
  <Rule Key="S2223" Action="Warning" Description="Initialize static members in graphics engine properly"/>
  <Rule Key="S1449" Action="Info" Description="Normalize string comparisons in shader loading"/>
  <Rule Key="S122" Action="Error" Description="File header required for all files"/>
  <Rule Key="S110" Action="Warning" Description="Inheritance depth should not exceed 5 levels"/>
  <Rule Key="S109" Action="Warning" Description="Magic numbers should not be used in real-time code"/>
  <Rule Key="S125" Action="Warning" Description="Remove commented code from graphics shaders"/>
  <Rule Key="S1066" Action="Warning" Description="Collapsible "if" statements should be merged"/>
  <Rule Key="S1905" Action="Info" Description="Remove unnecessary casts in graphics operations"/>
  
  <!-- Performance Critical Rules -->
  <Rule Key="S2674" Action="Error" Description="Avoid memory allocations in render loops"/>
  <Rule Key="S1940" Action="Warning" Description="Remove unnecessary boolean conversions"/>
  <Rule Key="S3235" Action="Warning" Description="Remove unnecessary parentheses"/>
  <Rule Key="S3358" Action="Warning" Description="Extract ternary operators to reduce complexity"/>
  
  <!-- Security Rules for Graphics Applications -->
  <Rule Key="S5332" Action="Error" Description="Forbid use of insecure protocols (HTTP) in shader loading"/>
  <Rule Key="S2638" Action="Error" Description="Remove unused named parameters in shader interfaces"/>
  <Rule Key="S3449" Action="Warning" Description="Verify dependencies are secure"/>
  
</rules>
```

### 2. Code Analysis Rules

**FxCopAnalyzers.ruleset:**
```xml
<?xml version="1.0" encoding="utf-8"?>
<RuleSet Name="TiXL Real-time Graphics Analysis" Description="Rules for TiXL real-time graphics applications" ToolsVersion="17.0">
  <Rules AnalyzerId="Microsoft.Analyzers.ManagedCodeAnalysis" RuleNamespace="Microsoft.Rules.Managed">
    <!-- Basic Design Rules -->
    <Rule Id="CA1000" Action="Warning" />
    <Rule Id="CA1001" Action="Warning" />
    <Rule Id="CA1002" Action="Warning" />
    <Rule Id="CA1003" Action="Error" />
    <Rule Id="CA1004" Action="Warning" />
    <Rule Id="CA1005" Action="Warning" />
    <Rule Id="CA1006" Action="Warning" />
    <Rule Id="CA1007" Action="Error" />
    <Rule Id="CA1008" Action="Warning" />
    <Rule Id="CA1009" Action="Error" />
    
    <!-- Performance Rules -->
    <Rule Id="CA1800" Action="Error" />
    <Rule Id="CA1801" Action="Error" />
    <Rule Id="CA1802" Action="Error" />
    <Rule Id="CA1804" Action="Error" />
    <Rule Id="CA1806" Action="Error" />
    <Rule Id="CA1810" Action="Error" />
    <Rule Id="CA1811" Action="Warning" />
    <Rule Id="CA1812" Action="Error" />
    <Rule Id="CA1813" Action="Warning" />
    <Rule Id="CA1814" Action="Warning" />
    <Rule Id="CA1815" Action="Warning" />
    
    <!-- Security Rules -->
    <Rule Id="CA2000" Action="Error" />
    <Rule Id="CA2001" Action="Warning" />
    <Rule Id="CA2002" Action="Error" />
    <Rule Id="CA2003" Action="Error" />
    <Rule Id="CA2004" Action="Error" />
    <Rule Id="CA2005" Action="Error" />
    <Rule Id="CA2006" Action="Warning" />
    
    <!-- Reliability Rules -->
    <Rule Id="CA2007" Action="Warning" />
    <Rule Id="CA2008" Action="Warning" />
    <Rule Id="CA2009" Action="Warning" />
    <Rule Id="CA2010" Action="Error" />
    
    <!-- Usage Rules -->
    <Rule Id="CA2100" Action="Error" />
    <Rule Id="CA2101" Action="Error" />
    <Rule Id="CA2102" Action="Error" />
    <Rule Id="CA2103" Action="Error" />
    <Rule Id="CA2104" Action="Warning" />
    <Rule Id="CA2105" Action="Error" />
    <Rule Id="CA2106" Action="Error" />
    <Rule Id="CA2107" Action="Error" />
    <Rule Id="CA2108" Action="Error" />
    <Rule Id="CA2109" Action="Error" />
    <Rule Id="CA2110" Action="Error" />
    <Rule Id="CA2111" Action="Error" />
    <Rule Id="CA2112" Action="Error" />
    <Rule Id="CA2113" Action="Error" />
    <Rule Id="CA2114" Action="Error" />
    <Rule Id="CA2115" Action="Error" />
    <Rule Id="CA2116" Action="Error" />
    <Rule Id="CA2117" Action="Error" />
    <Rule Id="CA2118" Action="Error" />
    <Rule Id="CA2119" Action="Error" />
    <Rule Id="CA2120" Action="Error" />
    <Rule Id="CA2121" Action="Error" />
    <Rule Id="CA2122" Action="Error" />
    <Rule Id="CA2123" Action="Error" />
    <Rule Id="CA2124" Action="Error" />
    <Rule Id="CA2125" Action="Error" />
    <Rule Id="CA2126" Action="Error" />
    <Rule Id="CA2127" Action="Error" />
    <Rule Id="CA2128" Action="Error" />
    <Rule Id="CA2129" Action="Error" />
    <Rule Id="CA2130" Action="Error" />
    <Rule Id="CA2131" Action="Error" />
    <Rule Id="CA2132" Action="Error" />
    <Rule Id="CA2133" Action="Error" />
    <Rule Id="CA2134" Action="Error" />
    <Rule Id="CA2135" Action="Error" />
    <Rule Id="CA2136" Action="Error" />
    <Rule Id="CA2137" Action="Error" />
    <Rule Id="CA2138" Action="Error" />
    <Rule Id="CA2139" Action="Error" />
    <Rule Id="CA2140" Action="Error" />
    <Rule Id="CA2141" Action="Error" />
    <Rule Id="CA2142" Action="Error" />
    <Rule Id="CA2143" Action="Error" />
    <Rule Id="CA2144" Action="Error" />
    <Rule Id="CA2145" Action="Error" />
    <Rule Id="CA2146" Action="Error" />
    <Rule Id="CA2147" Action="Error" />
    <Rule Id="CA2148" Action="Error" />
    <Rule Id="CA2149" Action="Error" />
  </Rules>
</RuleSet>
```

## Quality Standards

### 1. Code Coverage Standards

**Coverage Standards:**
- **Line Coverage:** Minimum 75% for all modules
- **Branch Coverage:** Minimum 70% for complex logic
- **Method Coverage:** Minimum 80% for public APIs
- **Critical Path Coverage:** 100% for rendering pipelines

### 2. Complexity Standards

**Cyclomatic Complexity:**
- **Methods:** Maximum 15 per method
- **Classes:** Maximum 10 per class (excluding generated code)
- **Nested Depth:** Maximum 4 levels
- **File Length:** Maximum 500 lines per file

### 3. Technical Debt Standards

**Debt Ratios:**
- **Critical:** >20% debt ratio (FAIL)
- **High:** 10-20% debt ratio (WARNING)
- **Medium:** 5-10% debt ratio (ACCEPTABLE)
- **Low:** <5% debt ratio (EXCELLENT)

### 4. Performance Standards

**Benchmark Targets:**
- **Render Loop:** <16ms per frame (60 FPS)
- **Shader Compilation:** <100ms per shader
- **Memory Allocation:** <1MB per frame
- **Thread Response:** <1ms for UI interactions

## Templates and Scripts

### 1. Comprehensive Quality Check Script

**run-quality-gates.ps1:**
```powershell
<#
.SYNOPSIS
    TiXL Comprehensive Quality Gates Checker
.DESCRIPTION
    Runs all quality gates and generates a comprehensive report.
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$BuildArtifactPath = "artifacts",
    
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "quality-gate-report.md",
    
    [Parameter(Mandatory=$false)]
    [switch]$FailOnQualityGateFailure
)

$ErrorActionPreference = "Stop"

function Test-QualityGate {
    param(
        [string]$Name,
        [string]$Type,
        [hashtable]$Thresholds,
        [hashtable]$ActualValues,
        [bool]$Required = $true
    )
    
    $result = @{
        Name = $Name
        Type = $Type
        Required = $Required
        Status = "PASS"
        Details = @()
    }
    
    foreach ($threshold in $Thresholds.Keys) {
        $expected = $Thresholds[$threshold]
        $actual = $ActualValues[$threshold]
        
        if ($null -ne $actual) {
            if ($threshold -eq "coverage" -or $threshold -eq "line_coverage" -or $threshold -eq "branch_coverage") {
                if ($actual -lt $expected) {
                    $result.Status = $Required ? "FAIL" : "WARNING"
                    $result.Details += "$threshold: $actual% < $expected% (Required: $Required)"
                } else {
                    $result.Details += "$threshold: $actual% >= $expected% ✓"
                }
            } elseif ($threshold -eq "duplicated_lines_density" -or $threshold -eq "debt_ratio") {
                if ($actual -gt $expected) {
                    $result.Status = $Required ? "FAIL" : "WARNING"
                    $result.Details += "$threshold: $actual% > $expected% (Required: $Required)"
                } else {
                    $result.Details += "$threshold: $actual% <= $expected% ✓"
                }
            } else {
                if ($actual -ne $expected) {
                    $result.Status = $Required ? "FAIL" : "WARNING"
                    $result.Details += "$threshold: $actual != $expected (Required: $Required)"
                } else {
                    $result.Details += "$threshold: $actual == $expected ✓"
                }
            }
        } else {
            $result.Details += "$threshold: No data available"
        }
    }
    
    return $result
}

function Start-QualityGateAnalysis {
    $qualityGateResults = @()
    $overallStatus = "PASS"
    
    Write-Host "Starting TiXL Quality Gate Analysis..." -ForegroundColor Green
    
    # Load quality gate configuration
    $configPath = Join-Path $PSScriptRoot "quality-gates-config.json"
    if (Test-Path $configPath) {
        $config = Get-Content $configPath -Raw | ConvertFrom-Json
    } else {
        $config = @{
            qualityGates = @(
                @{
                    name = "Basic Coverage Gate"
                    type = "Coverage"
                    required = $true
                    thresholds = @{ coverage = 75 }
                }
            )
        }
    }
    
    # Check each quality gate
    foreach ($gate in $config.qualityGates) {
        $actualValues = @{}
        
        # Load actual values from artifact files
        switch ($gate.type) {
            "SonarQube" {
                # Load SonarQube analysis results
                $sonarFiles = Get-ChildItem -Path $BuildArtifactPath -Filter "*sonar*.json" -Recurse
                foreach ($file in $sonarFiles) {
                    $content = Get-Content $file.FullName -Raw | ConvertFrom-Json
                    if ($content.metrics) {
                        foreach ($metric in $content.metrics) {
                            $actualValues[$metric.key] = [decimal]$metric.value
                        }
                    }
                }
            }
            "Coverage" {
                # Load coverage results
                $coverageFiles = Get-ChildItem -Path $BuildArtifactPath -Filter "*coverage*.xml" -Recurse
                foreach ($file in $coverageFiles) {
                    [xml]$coverage = Get-Content $file.FullName
                    $actualValues["coverage"] = [decimal]$coverage.coverage.sessionTrees.coverage.packages.package.classes.class.'@line-rate' * 100
                }
            }
        }
        
        # Test the quality gate
        $result = Test-QualityGate -Name $gate.name -Type $gate.type -Thresholds $gate.thresholds -ActualValues $actualValues -Required $gate.required
        $qualityGateResults += $result
        
        if ($result.Status -eq "FAIL" -and $result.Required) {
            $overallStatus = "FAIL"
        } elseif ($result.Status -eq "WARNING" -and $overallStatus -eq "PASS") {
            $overallStatus = "WARNING"
        }
    }
    
    # Generate report
    Generate-Report -Results $qualityGateResults -OverallStatus $overallStatus
    
    # Return appropriate exit code
    if ($FailOnQualityGateFailure -and $overallStatus -ne "PASS") {
        exit 1
    } elseif ($overallStatus -eq "FAIL") {
        exit 1
    }
    
    return $overallStatus
}

function Generate-Report {
    param(
        [array]$Results,
        [string]$OverallStatus
    )
    
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss UTC'
    
    $report = @"
# TiXL Quality Gate Report

**Generated:** $timestamp  
**Overall Status:** $OverallStatus  
**Total Gates:** $($Results.Count)

## Quality Gate Results

"@
    
    foreach ($result in $Results) {
        $emoji = switch ($result.Status) {
            "PASS" { "✅" }
            "WARNING" { "⚠️" }
            "FAIL" { "❌" }
        }
        
        $report += "### $emoji $($result.Name) ($($result.Type))

"
        $report += "**Status:** $($result.Status)"
        if ($result.Required) { $report += " (Required)" }
        $report += "`n`n"
        
        foreach ($detail in $result.Details) {
            $report += "- $detail`n"
        }
        $report += "`n"
    }
    
    $report += @"
## Summary

"@
    
    $passCount = ($Results | Where-Object { $_.Status -eq "PASS" }).Count
    $warningCount = ($Results | Where-Object { $_.Status -eq "WARNING" }).Count
    $failCount = ($Results | Where-Object { $_.Status -eq "FAIL" }).Count
    
    $report += "- **Passed:** $passCount gates`n"
    $report += "- **Warnings:** $warningCount gates`n"
    $report += "- **Failed:** $failCount gates`n`n"
    
    if ($OverallStatus -eq "PASS") {
        $report += "🎉 **Excellent!** All quality gates passed.`n`n"
    } elseif ($OverallStatus -eq "WARNING") {
        $report += "⚠️ **Attention Required** - Some quality gates have warnings.`n`n"
    } else {
        $report += "❌ **Quality Gate Failed** - Critical quality issues detected.`n`n"
    }
    
    $report += @"
## Next Steps

1. Review failed/warning gates above
2. Address critical issues before deployment
3. Monitor trends in quality metrics over time
4. Consider adjusting thresholds based on project evolution

## Tools and Resources

- [SonarQube Dashboard](https://your-sonarqube-instance.com/dashboard?id=tixl-realtime-graphics)
- [Code Coverage Reports](./coverage/index.html)
- [Performance Benchmarks](./performance/benchmark-results.json)
- [Technical Debt Analysis](./technical-debt-summary.md)

"@
    
    $report | Out-File -FilePath $OutputPath -Encoding UTF8
    Write-Host "Quality gate report saved to: $OutputPath" -ForegroundColor Green
    
    Write-Host "`n## Overall Quality Gate Status: $OverallStatus" -ForegroundColor $(switch ($OverallStatus) { "PASS" { "Green" } "WARNING" { "Yellow" } "FAIL" { "Red" } })
}

# Main execution
try {
    Start-QualityGateAnalysis
} catch {
    Write-Host "Quality gate analysis failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 2
}
```

### 2. Code Metrics Analysis Script

**run-metrics-analysis.ps1:**
```powershell
<#
.SYNOPSIS
    TiXL Code Metrics Analysis
.DESCRIPTION
    Analyzes code complexity, maintainability, and other metrics.
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$SolutionPath = "..\TiXL.sln",
    
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "metrics-report.json"
)

$ErrorActionPreference = "Stop"

function Calculate-CodeMetrics {
    param([string]$ProjectPath)
    
    $metrics = @{
        Project = (Split-Path $ProjectPath -Leaf)
        Metrics = @{}
        Files = @()
    }
    
    # Get all C# files in the project
    $projectDir = Split-Path $ProjectPath -Parent
    $csFiles = Get-ChildItem -Path $projectDir -Recurse -Filter "*.cs" -File
    
    foreach ($file in $csFiles) {
        $fileMetrics = Analyze-FileMetrics $file.FullName
        $metrics.Files += $fileMetrics
    }
    
    # Calculate aggregate metrics
    $metrics.Metrics = Calculate-AggregateMetrics $metrics.Files
    
    return $metrics
}

function Analyze-FileMetrics {
    param([string]$FilePath)
    
    try {
        $content = Get-Content $FilePath -Raw
        $lines = $content -split "`n"
        
        $fileMetrics = @{
            File = $FilePath
            LinesOfCode = ($lines | Where-Object { $_.Trim() -ne "" }).Count
            Methods = 0
            Classes = 0
            Complexity = 0
            ComplexityByMethod = @{}
        }
        
        $methodNames = @()
        $currentMethod = ""
        $methodComplexity = 0
        
        for ($i = 0; $i -lt $lines.Count; $i++) {
            $line = $lines[$i]
            $lineNumber = $i + 1
            
            # Count classes
            if ($line -match '^\s*(public|internal|private|protected)?\s*(partial\s+)?(class|interface|struct|enum)') {
                $fileMetrics.Classes++
            }
            
            # Count methods and calculate complexity
            if ($line -match '^\s*(public|internal|private|protected|static|\s)*(async\s+)?(\w+)\s+(\w+)\s*\(') {
                if ($currentMethod -ne "") {
                    $fileMetrics.ComplexityByMethod[$currentMethod] = $methodComplexity
                }
                $currentMethod = $matches[4]
                $methodComplexity = 1 # Base complexity for method declaration
                $fileMetrics.Methods++
                $methodNames += $currentMethod
            }
            
            # Calculate complexity for current method
            if ($currentMethod -ne "") {
                # Decision points
                if ($line -match '\b(if|else|while|for|foreach|case|catch|finally|&&|\|\||\?)\b') {
                    $methodComplexity++
                }
                # Logical operators in complex expressions
                if ($line -match '[\(\)\{\}]') {
                    $methodComplexity += [regex]::Matches($line, '[\(\)\{\}]').Count
                }
            }
        }
        
        if ($currentMethod -ne "") {
            $fileMetrics.ComplexityByMethod[$currentMethod] = $methodComplexity
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
    
    $totalLOC = 0
    $totalMethods = 0
    $totalClasses = 0
    $totalComplexity = 0
    $maxComplexity = 0
    $complexMethods = 0
    
    foreach ($file in $FileMetrics) {
        if ($file.Error) { continue }
        
        $totalLOC += $file.LinesOfCode
        $totalMethods += $file.Methods
        $totalClasses += $file.Classes
        $totalComplexity += $file.Complexity
        
        # Track maximum complexity
        foreach ($complexity in $file.ComplexityByMethod.Values) {
            if ($complexity -gt $maxComplexity) {
                $maxComplexity = $complexity
            }
            
            if ($complexity -gt 15) {
                $complexMethods++
            }
        }
    }
    
    return @{
        TotalLinesOfCode = $totalLOC
        TotalMethods = $totalMethods
        TotalClasses = $totalClasses
        AverageComplexity = if ($totalMethods -gt 0) { [math]::Round($totalComplexity / $totalMethods, 2) } else { 0 }
        MaxComplexity = $maxComplexity
        ComplexMethods = $complexMethods
        ComplexityRatio = if ($totalLOC -gt 0) { [math]::Round($totalComplexity / $totalLOC, 3) } else { 0 }
    }
}

function Start-MetricsAnalysis {
    Write-Host "Starting TiXL Code Metrics Analysis..." -ForegroundColor Green
    
    if (-not (Test-Path $SolutionPath)) {
        throw "Solution file not found at: $SolutionPath"
    }
    
    $allMetrics = @{
        Timestamp = (Get-Date).ToUniversalTime()
        Solution = $SolutionPath
        Projects = @()
        Summary = @{}
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
    $overallMetrics = Calculate-AggregateMetrics $allMetrics.Projects.Files
    $allMetrics.Summary = $overallMetrics
    
    # Generate recommendations
    $recommendations = @()
    
    if ($overallMetrics.AverageComplexity -gt 10) {
        $recommendations += "High average method complexity detected. Consider refactoring complex methods."
    }
    
    if ($overallMetrics.ComplexMethods -gt 0) {
        $recommendations += "$($overallMetrics.ComplexMethods) methods exceed complexity threshold of 15."
    }
    
    if ($overallMetrics.ComplexityRatio -gt 2.0) {
        $recommendations += "High complexity-to-LOC ratio. Consider breaking down complex functions."
    }
    
    $allMetrics.Recommendations = $recommendations
    
    # Save results
    $allMetrics | ConvertTo-Json -Depth 10 | Out-File -FilePath $OutputPath -Encoding UTF8
    Write-Host "Metrics report saved to: $OutputPath" -ForegroundColor Green
    
    # Display summary
    Write-Host "`n## Code Metrics Summary" -ForegroundColor Cyan
    Write-Host "Total Lines of Code: $($overallMetrics.TotalLinesOfCode)" -ForegroundColor White
    Write-Host "Total Methods: $($overallMetrics.TotalMethods)" -ForegroundColor White
    Write-Host "Total Classes: $($overallMetrics.TotalClasses)" -ForegroundColor White
    Write-Host "Average Complexity: $($overallMetrics.AverageComplexity)" -ForegroundColor White
    Write-Host "Complex Methods: $($overallMetrics.ComplexMethods)" -ForegroundColor $(if ($overallMetrics.ComplexMethods -gt 0) { "Yellow" } else { "Green" })
    
    if ($recommendations.Count -gt 0) {
        Write-Host "`n## Recommendations" -ForegroundColor Yellow
        foreach ($rec in $recommendations) {
            Write-Host "- $rec" -ForegroundColor Yellow
        }
    }
    
    return $allMetrics
}

# Main execution
try {
    Start-MetricsAnalysis
} catch {
    Write-Host "Metrics analysis failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 2
}
```

## Setup Instructions

### 1. Initial Setup

1. **Deploy SonarQube Server:**
   ```bash
   docker-compose -f docker-compose.sonar.yml up -d
   ```

2. **Access SonarQube Dashboard:**
   - URL: http://localhost:9000
   - Default credentials: admin/admin

3. **Configure Quality Profiles:**
   - Import the FxCopAnalyzers.ruleset file
   - Configure custom rules for real-time graphics

### 2. Development Environment Setup

1. **Install Required Tools:**
   ```powershell
   dotnet tool install --global dotnet-sonarscanner
   dotnet tool install --global jb.blt
   dotnet tool install --global metrics-to-json
   ```

2. **Configure IDE Settings:**
   - Enable all analyzers in Visual Studio
   - Configure code style rules
   - Set up live code analysis

### 3. CI/CD Pipeline Configuration

1. **Configure Azure DevOps:**
   - Install SonarQube extensions
   - Configure service connections
   - Set up quality gates

2. **Update Pipeline Variables:**
   - `SONAR_HOST_URL`: Your SonarQube instance URL
   - `SONAR_TOKEN`: Authentication token

### 4. Quality Gate Thresholds

The quality gates enforce the following standards:

| Metric | Threshold | Action |
|--------|-----------|---------|
| Code Coverage | 75% | Required |
| Duplicated Lines | 3% | Required |
| Maintainability Rating | A | Required |
| Security Rating | A | Required |
| Vulnerabilities | 0 | Required |
| Code Smells | 50 | Required |
| Technical Debt | 4% | Required |

### 5. Monitoring and Reporting

Quality reports are generated and available at:
- **Quality Dashboard**: Azure DevOps pipeline summary
- **SonarQube**: Detailed analysis and trends
- **Coverage Reports**: Code coverage breakdown
- **Performance Benchmarks**: Execution time metrics
- **Technical Debt**: Debt tracking and recommendations

## Conclusion

This comprehensive code quality setup ensures TiXL maintains high standards for real-time graphics applications through:

- **Static Analysis**: SonarQube integration with custom rules
- **Code Metrics**: Complexity and maintainability tracking
- **Quality Gates**: Automated quality enforcement
- **CI/CD Integration**: Seamless pipeline integration
- **Documentation**: Clear standards and templates

The setup provides early detection of issues, enforces coding standards, and maintains code quality throughout the development lifecycle.
