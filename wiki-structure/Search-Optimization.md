# Wiki Search Optimization Guide

This guide covers how TiXL's wiki is optimized for search functionality and discoverability.

## Search Strategy Overview

The TiXL wiki uses multiple search optimization techniques:

1. **Semantic Content Structure**
2. **SEO-Friendly Headers**
3. **Internal Link Mapping**
4. **Metadata and Tags**
5. **Content Organization**

## Content Structure for Search

### Header Optimization

```markdown
<!-- ✅ Good: Descriptive, keyword-rich headers -->
# Developer Onboarding Guide
## Development Environment Setup
### System Requirements
#### Windows Development Setup
##### .NET SDK Installation

<!-- ❌ Avoid: Generic or unclear headers -->
# Introduction
# Setup
# Getting Started
```

### Content Organization

```markdown
# Good Content Structure
## Table of Contents
1. [Quick Start](#quick-start)
2. [Detailed Setup](#detailed-setup)
3. [Troubleshooting](#troubleshooting)

## Quick Start (2-3 sentences summary)
Brief overview of what users will learn...

## Detailed Setup
### Prerequisites
- List of requirements

### Step-by-Step Instructions
1. First step
2. Second step
3. Third step

## Troubleshooting
### Common Issue 1
Solution description...

### Common Issue 2
Solution description...
```

## Search-Optimized Content Types

### 1. FAQ-Style Questions

```markdown
## Frequently Asked Questions

### How do I set up my development environment?
[Link to detailed setup guide]

### What are the system requirements?
- Windows 10/11
- .NET 9.0.0 SDK
- Visual Studio 2022
- DirectX 11.3 compatible GPU

### How do I contribute to TiXL?
1. Fork the repository
2. Follow our [Contribution Guidelines](Getting-Started/Contribution-Guidelines)
3. Submit a pull request
```

### 2. Problem-Solution Format

```markdown
## Common Problems and Solutions

### Problem: Build Fails with "Unable to locate .NET SDK"
**Solution:**
```bash
# Check installed versions
dotnet --list-sdks

# Install .NET 9.0 SDK from dotnet.microsoft.com
```

### Problem: TiXL Editor Won't Start
**Symptoms:**
- Application crashes on startup
- DirectX initialization error

**Solution:**
1. Update graphics drivers
2. Enable DirectX 11.3 features
3. Check Windows Graphics Tools installation
```

### 3. Step-by-Step Tutorials

```markdown
## Creating Your First Custom Operator

### Prerequisites
- TiXL development environment setup
- Basic C# knowledge
- Understanding of TiXL's operator system

### Step 1: Create Operator Class
[Code example with detailed comments]

### Step 2: Define Input/Output Slots
[Code example]

### Step 3: Implement Evaluation Logic
[Code example]

### Step 4: Test Your Operator
[Testing instructions]
```

## Internal Link Optimization

### Link Mapping Strategy

1. **Hub Pages**: Central pages that link to related content
2. **Spoke Pages**: Detailed content that links back to hubs
3. **Cross-References**: Related content in other sections

### Example Link Hierarchy

```
Home (Hub)
├── Getting Started (Hub)
│   ├── Quick Start Guide (Spoke)
│   ├── Developer Onboarding (Spoke)
│   └── Contribution Guidelines (Spoke)
│
├── Architecture (Hub)
│   ├── Architectural Governance (Spoke)
│   ├── Technical Architecture (Spoke)
│   └── Module Dependencies (Spoke)
│
└── Development (Hub)
    ├── Build System (Spoke)
    ├── Coding Standards (Spoke)
    └── Testing Guide (Spoke)
```

### Link Text Best Practices

```markdown
<!-- ✅ Good: Descriptive link text -->
- Read the [comprehensive developer onboarding guide](Getting-Started/Developer-Onboarding)
- Follow our [security guidelines](Security/Security-Guidelines)
- Check the [API reference documentation](API-Reference/API-Documentation)

<!-- ❌ Avoid: Generic link text -->
- Click [here](Getting-Started/Developer-Onboarding)
- Read [this guide](Security/Security-Guidelines)
- See [documentation](API-Reference/API-Documentation)
```

## Metadata and Tags

### Category Tags

Each wiki page includes category tags for quick filtering:

```markdown
---
categories: [development, getting-started, csharp, operators]
tags: [beginner, setup, tutorial]
difficulty: beginner
last-updated: 2024-11-01
version: TiXL-4.1.0
---
```

### Content Tags

**Difficulty Levels:**
- `beginner` - New to TiXL development
- `intermediate` - Some TiXL experience
- `advanced` - Experienced TiXL developer

**Content Types:**
- `tutorial` - Step-by-step learning content
- `reference` - Technical reference material
- `guide` - Best practices and guidelines
- `troubleshooting` - Problem-solving content

**Technology Tags:**
- `csharp` - C# related content
- `directx12` - DirectX 12 graphics
- `operators` - Operator development
- `ui` - User interface development

## Search-Friendly Content Formatting

### Code Blocks with Language Spec

```markdown
```csharp
public class MyOperator : Instance
{
    public override void Evaluate(EvaluationContext context)
    {
        // Operator implementation
    }
}
```

```bash
# Install command
dotnet new operator --template TiXL
```
```

### Bullet Points and Lists

```markdown
## Key Features
- ✅ Real-time rendering with DirectX 12
- ✅ Graph-based procedural composition
- ✅ Plugin-based operator system
- ✅ Audio-reactive visual creation
- ✅ Timeline-based animation

## System Requirements
1. **Operating System**: Windows 10/11 (64-bit)
2. **.NET SDK**: Version 9.0.0 or later
3. **IDE**: Visual Studio 2022 or JetBrains Rider
4. **GPU**: DirectX 11.3 compatible (GTX 970 or later)
5. **RAM**: 8GB minimum, 16GB recommended
```

### Tables for Structured Data

```markdown
## Operator Categories

| Category | Description | Examples |
|----------|-------------|----------|
| Values | Basic value manipulation | Add, Multiply, Sine |
| Collections | Data structure operations | Array, List, Filter |
| Graphics | Rendering operations | Texture, Shader, Render |
| Audio | Audio processing | FFT, Level, Filter |
| Utilities | Helper operators | Debug, Logic, Math |
```

## Content Freshness

### Update Indicators

```markdown
## Document Information
- **Last Updated**: November 1, 2024
- **Version**: TiXL 4.1.0
- **Author**: TiXL Documentation Team
- **Review Status**: Reviewed and Approved
```

### Stale Content Detection

Use automation to detect outdated content:

```yaml
# .github/workflows/wiki-maintenance.yml
name: Wiki Content Health Check
on:
  schedule:
    - cron: '0 0 * * 1' # Weekly on Monday

jobs:
  check-content-freshness:
    runs-on: ubuntu-latest
    steps:
      - name: Check for stale content
        run: |
          # Check for content older than 6 months
          # Generate freshness report
          # Alert on outdated critical documentation
```

## Search Analytics and Optimization

### Tracking Search Success

Monitor what users search for:

```bash
# Log popular search terms
# Track navigation patterns
# Identify content gaps
# Monitor search success rates
```

### Content Gap Analysis

```markdown
## Search Analytics Summary

### Top Search Terms (Last Month)
1. "operator development" - 156 searches
2. "build system" - 89 searches  
3. "security guidelines" - 67 searches
4. "troubleshooting" - 54 searches

### Content Gaps Identified
- Advanced operator patterns (mentioned in 23 searches, no content)
- Performance optimization (mentioned in 18 searches, minimal content)
- Custom UI components (mentioned in 12 searches, no content)

### Actions Taken
- Created Advanced Operator Patterns guide
- Expanded Performance Optimization section
- Added Custom UI Components tutorial
```

## Search Optimization Checklist

### For New Content

- [ ] Include clear, descriptive H1 header
- [ ] Add comprehensive table of contents
- [ ] Use semantic HTML structure (H1 → H2 → H3)
- [ ] Include frequently asked questions
- [ ] Add relevant internal links
- [ ] Use descriptive link text
- [ ] Include code examples with language specification
- [ ] Add difficulty level and category tags
- [ ] Include last updated date
- [ ] Test searchability with common search terms

### For Existing Content

- [ ] Review and improve header structure
- [ ] Add missing table of contents
- [ ] Fix broken internal links
- [ ] Improve link text descriptions
- [ ] Add category and difficulty tags
- [ ] Update content freshness indicators
- [ ] Remove or update outdated information
- [ ] Add cross-references to related content

## Tools and Automation

### Search Optimization Tools

```bash
# Link validation script
./scripts/validate-wiki-links.sh

# Content freshness checker
./scripts/check-content-freshness.sh

# Search term analyzer
./scripts/analyze-search-terms.sh

# Link mapping generator
./scripts/generate-link-map.sh
```

### Automation Examples

```powershell
# validate-wiki-links.ps1
param(
    [string]$WikiPath = "./wiki-structure"
)

Write-Host "Validating wiki links..." -ForegroundColor Green

# Find all markdown files
$files = Get-ChildItem -Path $WikiPath -Filter "*.md" -Recurse

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    
    # Check for common issues
    if ($content -notmatch "^#") {
        Write-Warning "$($file.Name): Missing H1 header"
    }
    
    if ($content -notmatch "## Table of Contents|## Contents") {
        Write-Warning "$($file.Name): Missing table of contents"
    }
    
    # Validate internal links
    $internalLinks = [regex]::Matches($content, '\[[^\]]*\]\([^)]*\)')
    foreach ($link in $internalLinks) {
        # Check if linked file exists
        # Validate link text is descriptive
    }
}
```

---

**Search Optimization Goals:**
- Users find information within 3 clicks
- Search results are relevant and comprehensive
- Content is easily discoverable through multiple pathways
- Related content is properly cross-referenced
- Content freshness is maintained

**Success Metrics:**
- Average time to find information < 2 minutes
- Search success rate > 95%
- User satisfaction with search functionality > 90%
