# TiXL Automated Documentation Generation Setup

## DocFX Configuration and Automation Pipeline

This document provides complete setup for automated API documentation generation using DocFX, integrated with the TiXL build system and CI/CD pipeline.

---

## 1. DocFX Project Structure

### Directory Structure
```
docs/
├── api/                          # Generated API documentation
│   ├── index.md                 # API documentation landing page
│   ├── manifest.json            # DocFX manifest
│   └── ...                      # Generated HTML files
├── templates/                    # Custom DocFX templates
│   ├── tixl/                    # TiXL-specific template
│   │   ├── partials/            # Template partials
│   │   ├── styles/              # Custom CSS
│   │   └── favicon.ico          # Site icon
├── docfx.json                    # DocFX configuration
├── docfx-project/               # DocFX project directory
│   ├── api/                     # API source files
│   ├── articles/                # Conceptual documentation
│   └── toc.yml                  # Table of contents
└── generation-scripts/          # Build and deployment scripts
```

---

## 2. DocFX Configuration (docfx.json)

```json
{
  "metadata": [
    {
      "src": [
        {
          "files": [
            "src/Core/**/*.cs",
            "src/Operators/**/*.cs", 
            "src/Editor/**/*.cs"
          ],
          "exclude": [
            "**/bin/**",
            "**/obj/**",
            "**/*.Designer.cs",
            "**/*Resources*.cs"
          ]
        }
      ],
      "dest": "docs/api",
      "disableGitFeatures": false,
      "disableDefaultFilter": false,
      "noRestore": false,
      "includeDependency": false,
      "includeNsSuffix": false,
      "onlyFirstLevelToc": false,
      "useDefaultToc": false,
      "tocMetadata": ["toc.yml"],
      "filter": "docs/filterConfig.yml"
    }
  ],
  "build": {
    "content": [
      {
        "files": [
          "api/**.yml",
          "api/index.md"
        ]
      },
      {
        "files": [
          "articles/**.md",
          "articles/**/toc.yml",
          "api/**.yml"
        ],
        "src": "docs"
      },
      {
        "files": [
          "images/**.*",
          "samples/**.*"
        ],
        "src": "docs"
      }
    ],
    "resource": [
      {
        "files": [
          "images/**.*"
        ]
      }
    ],
    "dest": "docs/api",
    "template": [
      "docs/templates/tixl",
      "default"
    ],
    "globalMetadata": {
      "_appTitle": "TiXL API Documentation",
      "_appFooter": "Copyright © 2025 TiXL Project. All rights reserved.",
      "_enableSearch": true,
      "_enableNewTab": true,
      "_layout": "_layout",
      "_appLogoPath": "images/logo.png",
      "_appFaviconPath": "images/favicon.ico",
      "_gitUrlPattern": "https://github.com/tixl3d/tixl/blob/master/{branch}/{path}",
      "_enableDocBook": false,
      "_openHost": "https://tixl3d.github.io/tixl"
    },
    "fileMetadata": {},
    "postProcessors": [
      "PrepareRegexForDocfx"
    ],
    "markdownEngineName": "markdig",
    "markdownEngineProperties": {
      "Seeded": true
    },
    "noop": false,
    "continueBuildWarning": false
  }
}
```

---

## 3. Filter Configuration (filterConfig.yml)

```yaml
apiRules:
  - include:
      uidRegex: ^TiXL\.(Core|Operators|Editor)\..*
      type: Namespace
      # Include all public namespaces in Core, Operators, and Editor modules
  
  - include:
      uidRegex: ^TiXL\..*public.*
      type: Member
      # Include all public members of TiXL types
  
  - exclude:
      # Exclude internal implementation details
      uidRegex: ^TiXL\.Internal\..*
      type: Namespace
  
  - exclude:
      # Exclude generated code
      uidRegex: .*\.Generated\..*
      type: Namespace
  
  - exclude:
      # Exclude obsolete APIs
      uidRegex: ^.*$
      type: Member
      # Condition: hasAttribute obsolescence
      excludeIfMissingComment: false
  
  - exclude:
      # Exclude private members unless explicitly documented
      uidRegex: ^.*$
      type: Member
      # Condition: isExplicitInterfaceImplementation || isPrivate
      excludeIfMissingComment: false

apiRulesNamespace:
  # Namespace-level filtering rules
  - include:
      uidRegex: ^TiXL\.(Core|Operators|Editor)\..*
      type: Namespace
  
  - exclude:
      uidRegex: ^TiXL\.Tests\..*
      type: Namespace
```

---

## 4. Table of Contents (toc.yml)

```yaml
- name: API Reference
  href: api/
  homepage: api/index.md
  
- name: Core Module
  href: api/toc.yml
  items:
    - name: TiXL.Core
      href: api/TiXL.Core.html
    - name: Graphics
      href: api/TiXL.Core.Graphics.html
    - name: Logging
      href: api/TiXL.Core.Logging.html
    - name: Performance
      href: api/TiXL.Core.Performance.html

- name: Operators Module  
  href: api/toc.yml
  items:
    - name: TiXL.Operators
      href: api/TiXL.Operators.html
    - name: Lib Operators
      href: api/TiXL.Operators.Lib.html
    - name: Custom Operators
      href: api/TiXL.Operators.Custom.html

- name: Editor Module
  href: api/toc.yml  
  items:
    - name: TiXL.Editor
      href: api/TiXL.Editor.html
    - name: UI Components
      href: api/TiXL.Editor.UI.html
    - name: Graph Editor
      href: api/TiXL.Editor.Graph.html

- name: Conceptual Documentation
  href: articles/
  items:
    - name: Getting Started
      href: getting-started/
    - name: Architecture Guide
      href: architecture/
    - name: Operator Development
      href: operator-development/
    - name: Performance Guidelines
      href: performance/
```

---

## 5. Custom Template (tixl/templates/tixl)

### Custom Styling (styles/tixl.css)

```css
/* TiXL-specific styling for API documentation */

/* TiXL Brand Colors */
:root {
  --tixl-primary: #0066cc;
  --tixl-secondary: #4a90e2;
  --tixl-accent: #ff6b35;
  --tixl-dark: #2c3e50;
  --tixl-light: #ecf0f1;
  --tixl-success: #27ae60;
  --tixl-warning: #f39c12;
  --tixl-error: #e74c3c;
}

/* Header Styling */
.topbar {
  background: linear-gradient(135deg, var(--tixl-primary), var(--tixl-secondary));
  color: white;
}

.brand {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.brand .logo {
  width: 32px;
  height: 32px;
  background: var(--tixl-accent);
  border-radius: 4px;
}

/* Navigation Styling */
.sidebar {
  background: var(--tixl-light);
  border-right: 1px solid #ddd;
}

.sidebar .nav > li > a {
  color: var(--tixl-dark);
  padding: 0.5rem 1rem;
  border-left: 3px solid transparent;
  transition: all 0.2s ease;
}

.sidebar .nav > li > a:hover {
  background: rgba(0, 102, 204, 0.1);
  border-left-color: var(--tixl-primary);
}

.sidebar .nav > li.active > a {
  background: rgba(0, 102, 204, 0.2);
  border-left-color: var(--tixl-primary);
  font-weight: 600;
}

/* Content Styling */
.main-content {
  padding: 2rem;
}

.api-name {
  color: var(--tixl-primary);
  font-weight: 600;
  font-size: 1.5rem;
}

.method-name {
  color: var(--tixl-dark);
  font-family: 'Fira Code', 'Consolas', monospace;
}

.property-name {
  color: var(--tixl-secondary);
  font-family: 'Fira Code', 'Consolas', monospace;
}

/* Code Block Styling */
code {
  background: rgba(0, 102, 204, 0.1);
  color: var(--tixl-primary);
  padding: 0.2rem 0.4rem;
  border-radius: 3px;
  font-family: 'Fira Code', 'Consolas', monospace;
}

.hljs {
  background: #f8f9fa;
  border: 1px solid #e9ecef;
  border-radius: 6px;
}

/* Operator-specific Styling */
.operator-category {
  background: linear-gradient(90deg, var(--tixl-accent), var(--tixl-warning));
  color: white;
  padding: 0.25rem 0.5rem;
  border-radius: 3px;
  font-size: 0.8rem;
  font-weight: 600;
}

.visual-example {
  border: 2px solid var(--tixl-secondary);
  border-radius: 8px;
  padding: 1rem;
  background: rgba(74, 144, 226, 0.05);
  margin: 1rem 0;
}

.performance-metric {
  background: rgba(39, 174, 96, 0.1);
  border: 1px solid var(--tixl-success);
  border-radius: 4px;
  padding: 0.5rem;
  margin: 0.5rem 0;
}

/* Search Styling */
.search {
  position: relative;
}

.search input {
  background: white;
  border: 2px solid var(--tixl-light);
  border-radius: 20px;
  padding: 0.5rem 1rem 0.5rem 2.5rem;
  transition: border-color 0.2s ease;
}

.search input:focus {
  border-color: var(--tixl-primary);
  outline: none;
}

.search::before {
  content: '🔍';
  position: absolute;
  left: 0.75rem;
  top: 50%;
  transform: translateY(-50%);
  color: var(--tixl-secondary);
}

/* Footer Styling */
.footer {
  background: var(--tixl-dark);
  color: white;
  padding: 2rem 0;
  margin-top: 2rem;
}

/* Responsive Design */
@media (max-width: 768px) {
  .main-content {
    padding: 1rem;
  }
  
  .sidebar {
    transform: translateX(-100%);
    transition: transform 0.3s ease;
  }
  
  .sidebar.mobile-open {
    transform: translateX(0);
  }
}

/* Print Styling */
@media print {
  .topbar, .sidebar, .footer {
    display: none;
  }
  
  .main-content {
    margin: 0;
    padding: 0;
  }
}
```

### Custom Header Template

```html
<!-- templates/tixl/partials/_header.html -->
<div class="topbar">
  <div class="container-fluid">
    <div class="brand">
      <div class="logo"></div>
      <h1>{{_appTitle}}</h1>
    </div>
    <div class="version-info">
      <span class="badge">v{{_appVersion}}</span>
      <a href="{{_gitUrlPattern}}" target="_blank" class="github-link">
        <i class="fab fa-github"></i> View Source
      </a>
    </div>
  </div>
</div>
```

### Custom Footer Template

```html
<!-- templates/tixl/partials/_footer.html -->
<div class="footer">
  <div class="container">
    <div class="row">
      <div class="col-md-6">
        <h5>TiXL Documentation</h5>
        <p>Real-time motion graphics platform documentation.</p>
        <p><a href="{{_openHost}}/api/" class="text-white">Browse API Reference</a></p>
      </div>
      <div class="col-md-6">
        <h5>Resources</h5>
        <ul class="list-unstyled">
          <li><a href="https://github.com/tixl3d/tixl" class="text-white">GitHub Repository</a></li>
          <li><a href="https://discord.gg/YmSyQdeH3S" class="text-white">Community Discord</a></li>
          <li><a href="https://tixl.app" class="text-white">Official Website</a></li>
          <li><a href="https://github.com/tixl3d/tixl/wiki" class="text-white">User Wiki</a></li>
        </ul>
      </div>
    </div>
    <hr class="text-white">
    <div class="text-center">
      <p>{{_appFooter}}</p>
      <p class="small">Documentation generated on {{_doc generationTime}}</p>
    </div>
  </div>
</div>
```

---

## 6. Build Scripts

### Generate Documentation Script (generate-docs.ps1)

```powershell
# scripts/generate-docs.ps1

param(
    [string]$SourcePath = "src",
    [string]$DocsPath = "docs",
    [string]$OutputPath = "docs/api",
    [switch]$Clean,
    [switch]$Verbose,
    [switch]$IncludePrivate = $false
)

Write-Host "TiXL Documentation Generation Script" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

# Configuration
$DOCFX_PATH = "$env:DOCFX_PATH"
$WORKSPACE = Get-Location
$BUILD_NUMBER = if ($env:BUILD_BUILDNUMBER) { $env:BUILD_BUILDNUMBER } else { "local" }
$BUILD_TIMESTAMP = Get-Date -Format "yyyy-MM-dd HH:mm:ss UTC"

# Validate DocFX installation
if (-not $DOCFX_PATH) {
    Write-Host "Installing DocFX..." -ForegroundColor Yellow
    dotnet tool update -g docfx --version 2.59.4
    $DOCFX_PATH = (dotnet tool list -g | Where-Object { $_ -match "docfx" } | ForEach-Object { ($_ -split '\s+')[0] }).Trim()
}

Write-Host "Using DocFX: $DOCFX_PATH" -ForegroundColor Green

# Clean previous documentation
if ($Clean) {
    Write-Host "Cleaning previous documentation..." -ForegroundColor Yellow
    if (Test-Path $OutputPath) {
        Remove-Item $OutputPath -Recurse -Force
        Write-Host "Cleaned $OutputPath" -ForegroundColor Green
    }
}

# Create build metadata
$buildInfo = @{
    buildNumber = $BUILD_NUMBER
    buildTimestamp = $BUILD_TIMESTAMP
    sourcePath = $SourcePath
    workspace = $WORKSPACE
    branch = if ($env:GITHUB_REF) { $env:GITHUB_REF } else { "local" }
    commit = if ($env:GITHUB_SHA) { $env:GITHUB_SHA } else { "unknown" }
}

# Write build info
$buildInfo | ConvertTo-Json -Depth 3 | Out-File -FilePath "$DocsPath/build-info.json" -Encoding UTF8

# Generate metadata
Write-Host "Generating API documentation metadata..." -ForegroundColor Yellow
$metadataArgs = @(
    "metadata",
    "--log", "docs/docfx-metadata.log"
)

if ($IncludePrivate) {
    $metadataArgs += "--include-private-members"
}

$metadataArgs += @(
    "$DocsPath/docfx.json",
    "--force"
)

& docfx $metadataArgs --logLevel Error

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to generate metadata"
    exit $LASTEXITCODE
}

# Build documentation
Write-Host "Building documentation..." -ForegroundColor Yellow
$buildArgs = @(
    "build",
    "$DocsPath/docfx.json",
    "--log", "docs/docfx-build.log",
    "--logLevel", "Error"
)

if ($Verbose) {
    $buildArgs += "--verbose"
}

& docfx $buildArgs

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to build documentation"
    exit $LASTEXITCODE
}

# Validate generated documentation
Write-Host "Validating generated documentation..." -ForegroundColor Yellow
$validationResults = @()

# Check for required files
$requiredFiles = @(
    "docs/api/index.html",
    "docs/api/TiXL.Core.html",
    "docs/api/TiXL.Operators.html",
    "docs/api/TiXL.Editor.html"
)

foreach ($file in $requiredFiles) {
    if (-not (Test-Path $file)) {
        $validationResults += "Missing: $file"
    }
}

# Check for generated API files count
$apiFiles = Get-ChildItem "docs/api" -Filter "*.html" | Where-Object { $_.Name -match "^TiXL\." }
$expectedApiCount = 10  # Minimum expected API files

if ($apiFiles.Count -lt $expectedApiCount) {
    $validationResults += "Low API documentation count: $($apiFiles.Count) (expected: $expectedApiCount)"
}

# Report validation results
if ($validationResults.Count -gt 0) {
    Write-Warning "Documentation validation issues found:"
    foreach ($issue in $validationResults) {
        Write-Warning "  - $issue"
    }
} else {
    Write-Host "Documentation validation passed!" -ForegroundColor Green
}

# Generate coverage report
Write-Host "Generating documentation coverage report..." -ForegroundColor Yellow
$coverageReport = @{
    generatedAt = $BUILD_TIMESTAMP
    buildNumber = $BUILD_NUMBER
    totalApiFiles = $apiFiles.Count
    validationIssues = $validationResults
    buildStatus = if ($validationResults.Count -eq 0) { "PASSED" } else { "ISSUES" }
    outputPath = $OutputPath
    docfxVersion = & docfx --version 2>$null | Select-String "docfx" | ForEach-Object { $_.Line }
}

$coverageReport | ConvertTo-Json -Depth 3 | Out-File -FilePath "$DocsPath/coverage-report.json" -Encoding UTF8

Write-Host "Documentation generation completed!" -ForegroundColor Green
Write-Host "Output directory: $OutputPath" -ForegroundColor Cyan
Write-Host "Coverage report: $DocsPath/coverage-report.json" -ForegroundColor Cyan

exit 0
```

### Build and Deploy Script (build-docs.ps1)

```powershell
# scripts/build-docs.ps1

param(
    [string]$Environment = "staging",
    [switch]$Deploy,
    [string]$TargetUrl = "",
    [switch]$Clean
)

Write-Host "TiXL Documentation Build and Deploy" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

# Configuration
$DOCS_PATH = "docs"
$BUILD_PATH = "$DOCS_PATH/api"
$DEPLOY_PATH = if ($Environment -eq "production") { "docs-api" } else { "docs-api-staging" }

# Build documentation
Write-Host "Building documentation..." -ForegroundColor Yellow
& .\scripts\generate-docs.ps1 -Clean:$Clean -Verbose

if ($LASTEXITCODE -ne 0) {
    Write-Error "Documentation build failed"
    exit $LASTEXITCODE
}

# Generate site map
Write-Host "Generating site map..." -ForegroundColor Yellow
$siteMap = @{
    urlset = @(
        @{ url = "$TargetUrl/api/"; changefreq = "daily"; priority = "0.9" }
        @{ url = "$TargetUrl/api/TiXL.Core/"; changefreq = "weekly"; priority = "0.8" }
        @{ url = "$TargetUrl/api/TiXL.Operators/"; changefreq = "weekly"; priority = "0.8" }
        @{ url = "$TargetUrl/api/TiXL.Editor/"; changefreq = "weekly"; priority = "0.8" }
    )
}

# Add API pages to sitemap
Get-ChildItem "$BUILD_PATH" -Filter "TiXL.*.html" | ForEach-Object {
    $siteMap.urlset += @{ 
        url = "$TargetUrl/api/$($_.Name)"
        changefreq = "weekly"
        priority = "0.7"
    }
}

# Deploy documentation
if ($Deploy) {
    Write-Host "Deploying to $Environment environment..." -ForegroundColor Yellow
    
    # Copy files to deployment location
    $deployFullPath = Join-Path $DOCS_PATH $DEPLOY_PATH
    if (Test-Path $deployFullPath) {
        Remove-Item $deployFullPath -Recurse -Force
    }
    
    Copy-Item -Path "$BUILD_PATH" -Destination $deployFullPath -Recurse -Force
    
    Write-Host "Documentation deployed to: $deployFullPath" -ForegroundColor Green
    if ($TargetUrl) {
        Write-Host "Live URL: $TargetUrl/$DEPLOY_PATH/" -ForegroundColor Cyan
    }
}

# Generate summary report
$report = @{
    environment = $Environment
    deployed = $Deploy
    deployedPath = if ($Deploy) { "$DOCS_PATH/$DEPLOY_PATH" } else { $null }
    targetUrl = $TargetUrl
    buildTime = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss UTC")
    filesGenerated = (Get-ChildItem $BUILD_PATH -Recurse).Count
}

$report | ConvertTo-Json -Depth 2 | Out-File -FilePath "$DOCS_PATH/deploy-report.json" -Encoding UTF8

Write-Host "Build and deployment completed!" -ForegroundColor Green
```

---

## 7. CI/CD Integration

### GitHub Actions Workflow (.github/workflows/docs.yml)

```yaml
name: Generate and Deploy Documentation

on:
  push:
    branches: [ main, develop ]
    paths:
      - 'src/**'
      - 'docs/**'
      - '.github/workflows/docs.yml'
  pull_request:
    branches: [ main ]
    paths:
      - 'src/**'
      - 'docs/**'
      - '.github/workflows/docs.yml'
  schedule:
    - cron: '0 0 * * *'  # Daily at midnight

jobs:
  generate-docs:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3
      with:
        fetch-depth: 0  # Full history for DocFX
        
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 8.0.x
        
    - name: Install DocFX
      run: dotnet tool update -g docfx --version 2.59.4
      
    - name: Generate Documentation
      run: |
        ./scripts/generate-docs.ps1 -Clean -Verbose
        
    - name: Upload Documentation Build
      uses: actions/upload-artifact@v3
      with:
        name: documentation-build
        path: docs/api/
        
    - name: Upload Coverage Report
      uses: actions/upload-artifact@v3
      with:
        name: coverage-report
        path: docs/coverage-report.json

  deploy-docs:
    needs: generate-docs
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main' && github.event_name == 'push'
    steps:
    - uses: actions/checkout@v3
    
    - name: Download Documentation Build
      uses: actions/download-artifact@v3
      with:
        name: documentation-build
        path: docs/api/
        
    - name: Deploy to GitHub Pages
      uses: peaceiris/actions-gh-pages@v3
      with:
        github_token: ${{ secrets.GITHUB_TOKEN }}
        publish_dir: docs/api/
        destination_dir: api/
        
    - name: Generate Deployment Report
      run: |
        echo '{"deploymentTime": "'$(date -u +%Y-%m-%dT%H:%M:%SZ)'", "branch": "'${{ github.ref }}'", "commit": "'${{ github.sha }}'"}' > docs/deployment-info.json
        
    - name: Upload Deployment Info
      uses: actions/upload-artifact@v3
      with:
        name: deployment-info
        path: docs/deployment-info.json
```

---

## 8. Quality Gates

### Pre-Build Validation Script (validate-docs.ps1)

```powershell
# scripts/validate-docs.ps1

Write-Host "TiXL Documentation Quality Validation" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan

$validationResults = @{
    passed = @()
    warnings = @()
    errors = @()
    score = 100
}

# Check XML documentation coverage
Write-Host "Checking XML documentation coverage..." -ForegroundColor Yellow

$sourceFiles = Get-ChildItem -Path "src" -Recurse -Filter "*.cs"
$unDocumented = @()

foreach ($file in $sourceFiles) {
    $content = Get-Content $file.FullName -Raw
    if ($content -match "<summary>") {
        # File has documentation
    } else {
        if ($content -match "public\s+(class|interface|enum|struct)")
        {
            $unDocumented += $file.Name
        }
    }
}

if ($unDocumented.Count -gt 0) {
    $validationResults.warnings += "Files missing XML documentation: $($unDocumented.Count)"
    foreach ($file in $unDocumented) {
        $validationResults.warnings += "  - $file"
    }
    $validationResults.score -= ($unDocumented.Count * 2)
} else {
    $validationResults.passed += "All public types have XML documentation"
}

# Check for example code blocks
Write-Host "Checking code examples..." -ForegroundColor Yellow

$docsWithExamples = Get-Content -Path "docs/coverage-report.json" | ConvertFrom-Json
$totalApis = $docsWithExamples.totalApiFiles

$codeExamplePattern = '<code>.*</code>'
$docsWithExamplesCount = 0

Get-ChildItem -Path "docs/api" -Filter "*.html" | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    if ($content -match $codeExamplePattern) {
        $docsWithExamplesCount++
    }
}

if ($totalApis -gt 0) {
    $exampleCoverage = ($docsWithExamplesCount / $totalApis) * 100
    if ($exampleCoverage -lt 50) {
        $validationResults.errors += "Low code example coverage: $exampleCoverage% (expected: >50%)"
        $validationResults.score -= 10
    } else {
        $validationResults.passed += "Good code example coverage: $exampleCoverage%"
    }
}

# Generate validation report
Write-Host "`nValidation Results:" -ForegroundColor Cyan
Write-Host "===================" -ForegroundColor Cyan

Write-Host "`nPASSED:" -ForegroundColor Green
foreach ($item in $validationResults.passed) {
    Write-Host "  ✓ $item" -ForegroundColor Green
}

if ($validationResults.warnings.Count -gt 0) {
    Write-Host "`nWARNINGS:" -ForegroundColor Yellow
    foreach ($item in $validationResults.warnings) {
        Write-Host "  ⚠ $item" -ForegroundColor Yellow
    }
}

if ($validationResults.errors.Count -gt 0) {
    Write-Host "`nERRORS:" -ForegroundColor Red
    foreach ($item in $validationResults.errors) {
        Write-Host "  ✗ $item" -ForegroundColor Red
    }
}

Write-Host "`nDocumentation Quality Score: $($validationResults.score)/100" -ForegroundColor $(if ($validationResults.score -ge 80) { "Green" } elseif ($validationResults.score -ge 60) { "Yellow" } else { "Red" })

# Save validation results
$validationResults | ConvertTo-Json -Depth 3 | Out-File -FilePath "docs/validation-report.json" -Encoding UTF8

if ($validationResults.errors.Count -gt 0 -or $validationResults.score -lt 70) {
    Write-Host "`nDocumentation quality validation FAILED" -ForegroundColor Red
    exit 1
} else {
    Write-Host "`nDocumentation quality validation PASSED" -ForegroundColor Green
    exit 0
}
```

---

## 9. Monitoring and Maintenance

### Documentation Health Check Script (check-docs-health.ps1)

```powershell
# scripts/check-docs-health.ps1

param(
    [string]$DocsUrl = "https://tixl3d.github.io/tixl/api/",
    [string]$ExpectedFileCount = "50"
)

Write-Host "TiXL Documentation Health Check" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan

$healthReport = @{
    timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss UTC")
    docsUrl = $DocsUrl
    checks = @()
    overall = "UNKNOWN"
}

# Check documentation site availability
Write-Host "Checking documentation site availability..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri $DocsUrl -UseBasicParsing -TimeoutSec 30
    if ($response.StatusCode -eq 200) {
        $healthReport.checks += @{
            name = "Site Availability"
            status = "PASS"
            details = "HTTP 200 OK"
        }
    } else {
        $healthReport.checks += @{
            name = "Site Availability"
            status = "FAIL"
            details = "HTTP $($response.StatusCode)"
        }
    }
} catch {
    $healthReport.checks += @{
        name = "Site Availability"
        status = "FAIL"
        details = $_.Exception.Message
    }
}

# Check for recent updates
Write-Host "Checking for recent updates..." -ForegroundColor Yellow
$siteLastModified = $response.Headers.'Last-Modified'
if ($siteLastModified) {
    $lastModified = [DateTime]::Parse($siteLastModified)
    $daysSinceUpdate = (Get-Date) - $lastModified
    if ($daysSinceUpdate.Days -le 7) {
        $healthReport.checks += @{
            name = "Recent Updates"
            status = "PASS"
            details = "Last updated $($daysSinceUpdate.Days) days ago"
        }
    } else {
        $healthReport.checks += @{
            name = "Recent Updates"
            status = "WARN"
            details = "Last updated $($daysSinceUpdate.Days) days ago"
        }
    }
}

# Check API documentation count
Write-Host "Checking API documentation count..." -ForegroundColor Yellow
$apiFiles = Invoke-WebRequest -Uri "$DocsUrl/TiXL.Core.html" -UseBasicParsing -TimeoutSec 30
if ($apiFiles.Content -match "class") {
    $classMatches = [regex]::Matches($apiFiles.Content, 'class="[^"]*"')
    $documentedClasses = $classMatches.Count
    
    if ($documentedClasses -ge [int]$ExpectedFileCount) {
        $healthReport.checks += @{
            name = "API Documentation Count"
            status = "PASS"
            details = "$documentedClasses documented classes found"
        }
    } else {
        $healthReport.checks += @{
            name = "API Documentation Count"
            status = "WARN"
            details = "Only $documentedClasses documented classes (expected: >=$ExpectedFileCount)"
        }
    }
}

# Determine overall health
$failedChecks = $healthReport.checks | Where-Object { $_.status -eq "FAIL" }
$warningChecks = $healthReport.checks | Where-Object { $_.status -eq "WARN" }

if ($failedChecks.Count -eq 0) {
    if ($warningChecks.Count -eq 0) {
        $healthReport.overall = "HEALTHY"
    } else {
        $healthReport.overall = "WARNING"
    }
} else {
    $healthReport.overall = "UNHEALTHY"
}

# Report results
Write-Host "`nHealth Check Results:" -ForegroundColor Cyan
Write-Host "======================" -ForegroundColor Cyan

foreach ($check in $healthReport.checks) {
    $statusColor = switch ($check.status) {
        "PASS" { "Green" }
        "WARN" { "Yellow" }
        "FAIL" { "Red" }
    }
    
    $statusIcon = switch ($check.status) {
        "PASS" { "✓" }
        "WARN" { "⚠" }
        "FAIL" { "✗" }
    }
    
    Write-Host "  $statusIcon $($check.name): $($check.status)" -ForegroundColor $statusColor
    Write-Host "    $($check.details)" -ForegroundColor Gray
}

Write-Host "`nOverall Health: $($healthReport.overall)" -ForegroundColor $(switch ($healthReport.overall) {
    "HEALTHY" { "Green" }
    "WARNING" { "Yellow" }
    "UNHEALTHY" { "Red" }
    default { "Gray" }
})

# Save health report
$healthReport | ConvertTo-Json -Depth 3 | Out-File -FilePath "docs/health-report.json" -Encoding UTF8

# Return appropriate exit code
if ($healthReport.overall -eq "UNHEALTHY") {
    exit 1
} else {
    exit 0
}
```

---

## 10. Configuration Reference

### Environment Variables

| Variable | Description | Required | Default |
|----------|-------------|----------|---------|
| `DOCFX_PATH` | Path to DocFX installation | No | Auto-detected |
| `BUILD_BUILDNUMBER` | CI build number for tracking | No | `local` |
| `GITHUB_REF` | Git reference for source linking | CI/CD | - |
| `GITHUB_SHA` | Commit SHA for source linking | CI/CD | - |

### Command Line Options

| Option | Description | Default |
|--------|-------------|---------|
| `-Clean` | Clean previous documentation before generation | false |
| `-Verbose` | Enable verbose logging output | false |
| `-IncludePrivate` | Include private members in documentation | false |
| `-Environment` | Target environment (staging/production) | `staging` |
| `-Deploy` | Deploy documentation after generation | false |
| `-TargetUrl` | Base URL for deployed documentation | - |

---

**Document Version**: 1.0  
**Last Updated**: 2025-11-02  
**Dependencies**: DocFX 2.59.4+, .NET 8.0+, PowerShell 7.0+