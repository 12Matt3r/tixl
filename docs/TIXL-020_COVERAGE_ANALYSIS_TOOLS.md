# TiXL Documentation Coverage Analysis Tools

## Automated Tools for Identifying Undocumented Public APIs

This document provides comprehensive tooling to analyze, track, and report documentation coverage across the TiXL codebase, with specific focus on Core, Operators, and Editor modules.

---

## 1. Coverage Analysis Framework

### Coverage Metrics Overview

- **API Coverage**: Percentage of public APIs with XML documentation
- **Module Coverage**: Coverage breakdown by module (Core, Operators, Editor)
- **Type Coverage**: Coverage by type (classes, interfaces, enums, structs)
- **Member Coverage**: Coverage by member type (methods, properties, events, fields)
- **Example Coverage**: APIs with code examples
- **Cross-Reference Coverage**: APIs with cross-references to related types

### Analysis Scope

#### Included in Analysis
- All public classes, interfaces, enums, and structs
- All public methods, properties, events, and fields
- Public APIs in TiXL.Core, TiXL.Operators, and TiXL.Editor namespaces
- Extension methods on public types

#### Excluded from Analysis
- Internal and private types and members
- Generated code (e.g., designer files)
- Obsolete APIs (analyzed separately)
- Test assemblies

---

## 2. Coverage Analysis Tool (C# Implementation)

### Main Coverage Analyzer

```csharp
// Tools/CoverageAnalysis/TiXLDocCoverageAnalyzer.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TiXL.Tools.CoverageAnalysis
{
    public class DocumentationCoverageAnalyzer
    {
        private readonly string[] _sourcePaths;
        private readonly HashSet<string> _excludedPatterns = new()
        {
            "**/bin/**",
            "**/obj/**", 
            "**/*.Designer.cs",
            "**/Resources/*.cs",
            "**/Test*.cs"
        };

        public DocumentationCoverageAnalyzer(params string[] sourcePaths)
        {
            _sourcePaths = sourcePaths ?? throw new ArgumentNullException(nameof(sourcePaths));
        }

        public CoverageReport Analyze()
        {
            var report = new CoverageReport { GeneratedAt = DateTime.UtcNow };
            var publicApis = new List<ApiElement>();

            foreach (var path in _sourcePaths)
            {
                var fileApis = AnalyzeFile(path);
                publicApis.AddRange(fileApis);
            }

            report.PublicApis = publicApis;
            report.CalculateMetrics();
            return report;
        }

        private List<ApiElement> AnalyzeFile(string filePath)
        {
            if (IsExcluded(filePath)) return new List<ApiElement>();

            var apis = new List<ApiElement>();
            var tree = CSharpSyntaxTree.ParseFile(filePath);
            var root = tree.GetCompilationUnitRoot();

            foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (IsPublic(classDecl))
                {
                    apis.Add(AnalyzeClass(classDecl, filePath));
                }
            }

            foreach (var interfaceDecl in root.DescendantNodes().OfType<InterfaceDeclarationSyntax>())
            {
                if (IsPublic(interfaceDecl))
                {
                    apis.Add(AnalyzeInterface(interfaceDecl, filePath));
                }
            }

            foreach (var enumDecl in root.DescendantNodes().OfType<EnumDeclarationSyntax>())
            {
                if (IsPublic(enumDecl))
                {
                    apis.Add(AnalyzeEnum(enumDecl, filePath));
                }
            }

            foreach (var structDecl in root.DescendantNodes().OfType<StructDeclarationSyntax>())
            {
                if (IsPublic(structDecl))
                {
                    apis.Add(AnalyzeStruct(structDecl, filePath));
                }
            }

            return apis;
        }

        private ApiElement AnalyzeClass(ClassDeclarationSyntax classDecl, string filePath)
        {
            var api = new ApiElement
            {
                Name = classDecl.Identifier.Text,
                Type = ApiType.Class,
                FilePath = filePath,
                Namespace = GetNamespace(classDecl),
                LineNumber = classDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                IsPublic = true
            };

            var documentation = GetDocumentation(classDecl);
            api.HasDocumentation = !string.IsNullOrWhiteSpace(documentation);
            api.DocumentationType = GetDocumentationType(documentation);
            api.HasExamples = HasCodeExamples(documentation);
            api.HasCrossReferences = HasCrossReferences(documentation);

            // Analyze members
            foreach (var member in classDecl.Members)
            {
                if (IsPublic(member) && ShouldDocument(member))
                {
                    var memberApi = AnalyzeMember(member, filePath);
                    api.Members.Add(memberApi);
                }
            }

            return api;
        }

        private ApiElement AnalyzeMember(MemberDeclarationSyntax member, string filePath)
        {
            var api = new ApiElement
            {
                Name = GetMemberName(member),
                Type = GetMemberType(member),
                FilePath = filePath,
                Namespace = GetNamespace(member),
                LineNumber = member.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                IsPublic = true
            };

            var documentation = GetDocumentation(member);
            api.HasDocumentation = !string.IsNullOrWhiteSpace(documentation);
            api.DocumentationType = GetDocumentationType(documentation);
            api.HasExamples = HasCodeExamples(documentation);
            api.HasCrossReferences = HasCrossReferences(documentation);

            return api;
        }

        private bool IsPublic(BaseTypeDeclarationSyntax typeDecl)
        {
            return typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
        }

        private bool IsPublic(MemberDeclarationSyntax member)
        {
            return member.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
        }

        private bool ShouldDocument(MemberDeclarationSyntax member)
        {
            // Don't document indexers, explicit interface implementations in some cases
            if (member is IndexerDeclarationSyntax) return false;
            
            // Only document meaningful members
            return member is MethodDeclarationSyntax ||
                   member is PropertyDeclarationSyntax ||
                   member is EventDeclarationSyntax ||
                   member is FieldDeclarationSyntax ||
                   member is ConstructorDeclarationSyntax;
        }

        private string GetDocumentation(SyntaxNode node)
        {
            var trivia = node.GetLeadingTrivia()
                .FirstOrDefault(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                                   t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));
            
            return trivia?.ToFullString() ?? string.Empty;
        }

        private string GetDocumentationType(string documentation)
        {
            if (string.IsNullOrWhiteSpace(documentation)) return "None";
            
            var patterns = new Dictionary<string, string>
            {
                { @"<summary>", "Summary" },
                { @"<remarks>", "Detailed" },
                { @"<example>", "WithExamples" },
                { @"<see\s+cref=", "CrossReferenced" },
                { @"<param\s+name=", "Parameterized" },
                { @"<returns>", "WithReturns" }
            };

            var types = new List<string>();
            foreach (var pattern in patterns)
            {
                if (Regex.IsMatch(documentation, pattern.Key, RegexOptions.IgnoreCase))
                {
                    types.Add(pattern.Value);
                }
            }

            return types.Count > 0 ? string.Join(",", types) : "Basic";
        }

        private bool HasCodeExamples(string documentation)
        {
            return Regex.IsMatch(documentation, @"<example>.*</example>", 
                               RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }

        private bool HasCrossReferences(string documentation)
        {
            return Regex.IsMatch(documentation, @"<see\s+cref=", RegexOptions.IgnoreCase) ||
                   Regex.IsMatch(documentation, @"<seealso\s+cref=", RegexOptions.IgnoreCase);
        }

        private string GetNamespace(SyntaxNode node)
        {
            var namespaceDecl = node.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
            return namespaceDecl?.Name.ToString() ?? "Global";
        }

        private string GetMemberName(MemberDeclarationSyntax member)
        {
            return member switch
            {
                MethodDeclarationSyntax m => m.Identifier.Text,
                PropertyDeclarationSyntax p => p.Identifier.Text,
                EventDeclarationSyntax e => e.Identifier.Text,
                FieldDeclarationSyntax f => f.Declaration.Variables.First().Identifier.Text,
                ConstructorDeclarationSyntax c => ".ctor",
                _ => member.ToString()
            };
        }

        private ApiType GetMemberType(MemberDeclarationSyntax member)
        {
            return member switch
            {
                MethodDeclarationSyntax => ApiType.Method,
                PropertyDeclarationSyntax => ApiType.Property,
                EventDeclarationSyntax => ApiType.Event,
                FieldDeclarationSyntax => ApiType.Field,
                ConstructorDeclarationSyntax => ApiType.Constructor,
                _ => ApiType.Other
            };
        }

        private bool IsExcluded(string filePath)
        {
            var normalizedPath = filePath.Replace('\\', '/');
            return _excludedPatterns.Any(pattern => 
                MatchPattern(normalizedPath, pattern));
        }

        private bool MatchPattern(string path, string pattern)
        {
            // Simple wildcard matching - can be enhanced
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace(@"\*\*", ".*")
                .Replace(@"\*", "[^/]*")
                .Replace(@"\?", ".") + "$";
            
            return Regex.IsMatch(path, regexPattern, RegexOptions.IgnoreCase);
        }
    }

    public class CoverageReport
    {
        public DateTime GeneratedAt { get; set; }
        public List<ApiElement> PublicApis { get; set; } = new();
        public CoverageMetrics Metrics { get; set; } = new();

        public void CalculateMetrics()
        {
            Metrics.TotalApis = PublicApis.Count;
            Metrics.DocumentedApis = PublicApis.Count(api => api.HasDocumentation);
            Metrics.ApisWithExamples = PublicApis.Count(api => api.HasExamples);
            Metrics.ApisWithCrossReferences = PublicApis.Count(api => api.HasCrossReferences);
            
            Metrics.CoveragePercentage = Metrics.TotalApis > 0 
                ? (double)Metrics.DocumentedApis / Metrics.TotalApis * 100 
                : 0;

            Metrics.ExampleCoveragePercentage = Metrics.TotalApis > 0
                ? (double)Metrics.ApisWithExamples / Metrics.TotalApis * 100
                : 0;

            Metrics.CrossReferenceCoveragePercentage = Metrics.TotalApis > 0
                ? (double)Metrics.ApisWithCrossReferences / Metrics.TotalApis * 100
                : 0;

            // Module breakdown
            Metrics.ModuleCoverage = CalculateModuleCoverage();
            
            // Type breakdown
            Metrics.TypeCoverage = CalculateTypeCoverage();
        }

        private Dictionary<string, ModuleCoverage> CalculateModuleCoverage()
        {
            var moduleCoverage = new Dictionary<string, ModuleCoverage>();
            
            foreach (var module in new[] { "Core", "Operators", "Editor" })
            {
                var moduleApis = PublicApis.Where(api => 
                    api.Namespace.StartsWith($"TiXL.{module}", StringComparison.OrdinalIgnoreCase)).ToList();
                
                if (moduleApis.Any())
                {
                    moduleCoverage[module] = new ModuleCoverage
                    {
                        ModuleName = module,
                        TotalApis = moduleApis.Count,
                        DocumentedApis = moduleApis.Count(api => api.HasDocumentation),
                        CoveragePercentage = (double)moduleApis.Count(api => api.HasDocumentation) / moduleApis.Count * 100
                    };
                }
            }

            return moduleCoverage;
        }

        private Dictionary<string, TypeCoverage> CalculateTypeCoverage()
        {
            var typeCoverage = new Dictionary<string, TypeCoverage>();
            
            foreach (ApiType type in Enum.GetValues(typeof(ApiType)))
            {
                var typeApis = PublicApis.Where(api => api.Type == type).ToList();
                
                if (typeApis.Any())
                {
                    typeCoverage[type.ToString()] = new TypeCoverage
                    {
                        TypeName = type.ToString(),
                        TotalApis = typeApis.Count,
                        DocumentedApis = typeApis.Count(api => api.HasDocumentation),
                        CoveragePercentage = (double)typeApis.Count(api => api.HasDocumentation) / typeApis.Count * 100
                    };
                }
            }

            return typeCoverage;
        }
    }

    public class ApiElement
    {
        public string Name { get; set; } = string.Empty;
        public ApiType Type { get; set; }
        public string Namespace { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public int LineNumber { get; set; }
        public bool IsPublic { get; set; }
        public bool HasDocumentation { get; set; }
        public string DocumentationType { get; set; } = string.Empty;
        public bool HasExamples { get; set; }
        public bool HasCrossReferences { get; set; }
        public List<ApiElement> Members { get; set; } = new();
    }

    public enum ApiType
    {
        Class,
        Interface,
        Enum,
        Struct,
        Method,
        Property,
        Event,
        Field,
        Constructor,
        Other
    }

    public class CoverageMetrics
    {
        public int TotalApis { get; set; }
        public int DocumentedApis { get; set; }
        public int ApisWithExamples { get; set; }
        public int ApisWithCrossReferences { get; set; }
        public double CoveragePercentage { get; set; }
        public double ExampleCoveragePercentage { get; set; }
        public double CrossReferenceCoveragePercentage { get; set; }
        public Dictionary<string, ModuleCoverage> ModuleCoverage { get; set; } = new();
        public Dictionary<string, TypeCoverage> TypeCoverage { get; set; } = new();
    }

    public class ModuleCoverage
    {
        public string ModuleName { get; set; } = string.Empty;
        public int TotalApis { get; set; }
        public int DocumentedApis { get; set; }
        public double CoveragePercentage { get; set; }
    }

    public class TypeCoverage
    {
        public string TypeName { get; set; } = string.Empty;
        public int TotalApis { get; set; }
        public int DocumentedApis { get; set; }
        public double CoveragePercentage { get; set; }
    }
}
```

---

## 3. PowerShell Coverage Analysis Script

### Main Analysis Script

```powershell
# scripts/analyze-coverage.ps1

param(
    [string]$SourcePath = "src",
    [string]$OutputPath = "docs/coverage",
    [switch]$Detailed,
    [switch]$GenerateHtml,
    [string]$Threshold = "80"
)

Write-Host "TiXL Documentation Coverage Analysis" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

# Create output directory
if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
}

# Find all C# source files
Write-Host "Scanning source files..." -ForegroundColor Yellow
$sourceFiles = Get-ChildItem -Path $SourcePath -Recurse -Filter "*.cs" | Where-Object {
    $_.FullName -notmatch '\\bin\\' -and 
    $_.FullName -notmatch '\\obj\\' -and
    $_.Name -notmatch '\.Designer\.cs$' -and
    $_.Name -notmatch 'Resources.*\.cs$'
}

Write-Host "Found $($sourceFiles.Count) source files" -ForegroundColor Green

# Analyze coverage
Write-Host "Analyzing documentation coverage..." -ForegroundColor Yellow
$coverageResults = @()

foreach ($file in $sourceFiles) {
    $result = Analyze-FileCoverage -FilePath $file.FullName
    $coverageResults += $result
}

# Calculate overall metrics
$totalApis = $coverageResults.Count
$documentedApis = ($coverageResults | Where-Object { $_.HasDocumentation }).Count
$coveragePercentage = if ($totalApis -gt 0) { ($documentedApis / $totalApis) * 100 } else { 0 }

# Generate report
$report = @{
    timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss UTC")
    sourcePath = $SourcePath
    totalApis = $totalApis
    documentedApis = $documentedApis
    coveragePercentage = [math]::Round($coveragePercentage, 2)
    threshold = [int]$Threshold
    passed = $coveragePercentage -ge [int]$Threshold
    files = $coverageResults
    modules = @{
        Core = @{
            total = ($coverageResults | Where-Object { $_.Namespace -match 'Core' }).Count
            documented = ($coverageResults | Where-Object { $_.Namespace -match 'Core' -and $_.HasDocumentation }).Count
        }
        Operators = @{
            total = ($coverageResults | Where-Object { $_.Namespace -match 'Operators' }).Count
            documented = ($coverageResults | Where-Object { $_.Namespace -match 'Operators' -and $_.HasDocumentation }).Count
        }
        Editor = @{
            total = ($coverageResults | Where-Object { $_.Namespace -match 'Editor' }).Count
            documented = ($coverageResults | Where-Object { $_.Namespace -match 'Editor' -and $_.HasDocumentation }).Count
        }
    }
}

# Save JSON report
$reportJson = $report | ConvertTo-Json -Depth 3
$reportJson | Out-File -FilePath "$OutputPath/coverage-report.json" -Encoding UTF8

# Generate console report
Write-Host "`nCoverage Analysis Results:" -ForegroundColor Cyan
Write-Host "==========================" -ForegroundColor Cyan
Write-Host "Total APIs: $totalApis" -ForegroundColor White
Write-Host "Documented APIs: $documentedApis" -ForegroundColor Green
Write-Host "Coverage: $([math]::Round($coveragePercentage, 2))%" -ForegroundColor $(if ($coveragePercentage -ge [int]$Threshold) { "Green" } else { "Red" })

# Module breakdown
Write-Host "`nModule Coverage:" -ForegroundColor Yellow
foreach ($module in $report.modules.Keys) {
    $total = $report.modules[$module].total
    $documented = $report.modules[$module].documented
    $percentage = if ($total -gt 0) { ($documented / $total) * 100 } else { 0 }
    $color = if ($percentage -ge [int]$Threshold) { "Green" } else { "Red" }
    Write-Host "  $module`: $documented/$total ($( [math]::Round($percentage, 1) )%)" -ForegroundColor $color
}

# Undocumented APIs report
$undocumented = $coverageResults | Where-Object { -not $_.HasDocumentation } | Sort-Object Namespace, Name
if ($undocumented.Count -gt 0) {
    Write-Host "`nUndocumented Public APIs ($($undocumented.Count)):" -ForegroundColor Red
    $groupedUndocumented = $undocumented | Group-Object Namespace | Sort-Object Name
    
    foreach ($group in $groupedUndocumented) {
        Write-Host "  $($group.Name):" -ForegroundColor Yellow
        foreach ($api in $group.Group | Sort-Object Name) {
            Write-Host "    - $($api.Name) ($($api.Type)) at line $($api.LineNumber)" -ForegroundColor Red
        }
    }
}

# Detailed report if requested
if ($Detailed) {
    Write-Host "`nDetailed Coverage Analysis:" -ForegroundColor Yellow
    
    # Coverage by type
    $byType = $coverageResults | Group-Object Type | Sort-Object Name
    foreach ($group in $byType) {
        $documented = ($group.Group | Where-Object { $_.HasDocumentation }).Count
        $percentage = ($documented / $group.Count) * 100
        Write-Host "  $($group.Name): $documented/$($group.Count) ($( [math]::Round($percentage, 1) )%)" -ForegroundColor $(if ($percentage -ge [int]$Threshold) { "Green" } else { "Yellow" })
    }
    
    # Coverage by documentation type
    $docTypes = $coverageResults | Where-Object { $_.HasDocumentation } | Group-Object DocumentationType | Sort-Object Name
    if ($docTypes.Count -gt 0) {
        Write-Host "  Documentation Types:" -ForegroundColor Yellow
        foreach ($group in $docTypes) {
            Write-Host "    $($group.Name): $($group.Count)" -ForegroundColor Cyan
        }
    }
}

# HTML report generation
if ($GenerateHtml) {
    Write-Host "Generating HTML coverage report..." -ForegroundColor Yellow
    Generate-HtmlCoverageReport -Report $report -OutputPath $OutputPath
}

# Return appropriate exit code
$exitCode = if ($report.passed) { 0 } else { 1 }
if (-not $report.passed) {
    Write-Host "`nCoverage analysis FAILED - Below $($Threshold)% threshold" -ForegroundColor Red
} else {
    Write-Host "`nCoverage analysis PASSED - Above $($Threshold)% threshold" -ForegroundColor Green
}

exit $exitCode

function Analyze-FileCoverage {
    param([string]$FilePath)
    
    $content = Get-Content $FilePath -Raw
    
    # Find all public APIs using regex
    $publicApis = @()
    
    # Public classes
    $classMatches = [regex]::Matches($content, 'public\s+(?:partial\s+)?class\s+(\w+)', 'IgnoreCase')
    foreach ($match in $classMatches) {
        $publicApis += @{
            Name = $match.Groups[1].Value
            Type = "Class"
            HasDocumentation = $content -match "<summary>.*?</summary>" -and $content -match "public\s+class\s+$($match.Groups[1].Value)"
            Namespace = Get-NamespaceFromContent -Content $content -TypeName $match.Groups[1].Value
            LineNumber = $content.Substring(0, $match.Index).Split("`n").Length
        }
    }
    
    # Public interfaces
    $interfaceMatches = [regex]::Matches($content, 'public\s+(?:partial\s+)?interface\s+(\w+)', 'IgnoreCase')
    foreach ($match in $interfaceMatches) {
        $publicApis += @{
            Name = $match.Groups[1].Value
            Type = "Interface"
            HasDocumentation = $content -match "<summary>.*?</summary>" -and $content -match "public\s+interface\s+$($match.Groups[1].Value)"
            Namespace = Get-NamespaceFromContent -Content $content -TypeName $match.Groups[1].Value
            LineNumber = $content.Substring(0, $match.Index).Split("`n").Length
        }
    }
    
    # Public methods
    $methodMatches = [regex]::Matches($content, 'public\s+(?:\w+(?:<[^>]+>)?)\s+(\w+)\s*\([^)]*\)', 'IgnoreCase')
    foreach ($match in $methodMatches) {
        # Skip property accessors and special methods
        $methodName = $match.Groups[1].Value
        if ($methodName -notmatch '^(get_|set_|add_|remove_)' -and $methodName -ne 'Equals' -and $methodName -ne 'GetHashCode' -and $methodName -ne 'ToString') {
            $publicApis += @{
                Name = $methodName
                Type = "Method"
                HasDocumentation = $content -match "<summary>.*?</summary>" -and $content -match "public\s+\w+\s+$methodName"
                Namespace = Get-NamespaceFromContent -Content $content -TypeName $methodName
                LineNumber = $content.Substring(0, $match.Index).Split("`n").Length
            }
        }
    }
    
    # Public properties
    $propertyMatches = [regex]::Matches($content, 'public\s+(?:\w+(?:<[^>]+>)?)\s+(\w+)\s*{', 'IgnoreCase')
    foreach ($match in $propertyMatches) {
        $propertyName = $match.Groups[1].Value
        # Skip indexers
        if ($propertyName -ne "this") {
            $publicApis += @{
                Name = $propertyName
                Type = "Property"
                HasDocumentation = $content -match "<summary>.*?</summary>" -and $content -match "public\s+\w+\s+$propertyName"
                Namespace = Get-NamespaceFromContent -Content $Content -TypeName $propertyName
                LineNumber = $content.Substring(0, $match.Index).Split("`n").Length
            }
        }
    }
    
    return $publicApis
}

function Get-NamespaceFromContent {
    param([string]$Content, [string]$TypeName)
    
    $namespaceMatch = [regex]::Match($Content, 'namespace\s+([^\s{]+)')
    if ($namespaceMatch.Success) {
        return $namespaceMatch.Groups[1].Value
    }
    return "Global"
}

function Generate-HtmlCoverageReport {
    param([object]$Report, [string]$OutputPath)
    
    $html = @"
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>TiXL Documentation Coverage Report</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; background: #f5f5f5; }
        .container { max-width: 1200px; margin: 0 auto; background: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }
        .header { background: linear-gradient(135deg, #0066cc, #4a90e2); color: white; padding: 20px; border-radius: 8px; margin-bottom: 20px; }
        .metric { display: inline-block; margin: 10px; padding: 15px; background: #ecf0f1; border-radius: 6px; text-align: center; }
        .metric-value { font-size: 24px; font-weight: bold; }
        .metric-label { font-size: 12px; color: #666; }
        .pass { color: #27ae60; }
        .fail { color: #e74c3c; }
        .warning { color: #f39c12; }
        .section { margin: 20px 0; }
        .section h3 { color: #2c3e50; border-bottom: 2px solid #3498db; padding-bottom: 5px; }
        table { width: 100%; border-collapse: collapse; margin: 10px 0; }
        th, td { padding: 8px; text-align: left; border-bottom: 1px solid #ddd; }
        th { background-color: #f8f9fa; }
        .undocumented { background-color: #ffe6e6; }
        .progress-bar { width: 100%; height: 20px; background: #ecf0f1; border-radius: 10px; overflow: hidden; }
        .progress-fill { height: 100%; background: linear-gradient(90deg, #27ae60, #2ecc71); transition: width 0.3s ease; }
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <h1>TiXL Documentation Coverage Report</h1>
            <p>Generated: $($Report.timestamp)</p>
            <p>Source Path: $($Report.sourcePath)</p>
        </div>
        
        <div class="section">
            <h2>Overall Coverage</h2>
            <div class="metric">
                <div class="metric-value">$($Report.coveragePercentage)%</div>
                <div class="metric-label">Overall Coverage</div>
            </div>
            <div class="metric">
                <div class="metric-value">$($Report.documentedApis)</div>
                <div class="metric-label">Documented APIs</div>
            </div>
            <div class="metric">
                <div class="metric-value">$($Report.totalApis)</div>
                <div class="metric-label">Total APIs</div>
            </div>
            <div class="metric">
                <div class="metric-value $(if ($Report.passed) { 'pass' } else { 'fail' })">$($Report.passed ? 'PASS' : 'FAIL')</div>
                <div class="metric-label">Quality Gate</div>
            </div>
        </div>
        
        <div class="section">
            <h2>Module Coverage</h2>
            <table>
                <thead>
                    <tr><th>Module</th><th>Total APIs</th><th>Documented</th><th>Coverage</th><th>Status</th></tr>
                </thead>
                <tbody>
"@
    
    foreach ($module in $Report.modules.Keys) {
        $total = $Report.modules[$module].total
        $documented = $Report.modules[$module].documented
        $percentage = if ($total -gt 0) { ($documented / $total) * 100 } else { 0 }
        $status = if ($percentage -ge 80) { 'pass' } elseif ($percentage -ge 60) { 'warning' } else { 'fail' }
        
        $html += @"
                    <tr>
                        <td>$module</td>
                        <td>$total</td>
                        <td>$documented</td>
                        <td>
                            <div class="progress-bar">
                                <div class="progress-fill" style="width: $percentage%"></div>
                            </div>
                            $([math]::Round($percentage, 1))%
                        </td>
                        <td class="$status">$([math]::Round($percentage, 1))%</td>
                    </tr>
"@
    }
    
    $html += @"
                </tbody>
            </table>
        </div>
        
        <div class="section">
            <h2>Undocumented Public APIs</h2>
"@
    
    $undocumentedCount = ($Report.files | Where-Object { -not $_.HasDocumentation }).Count
    if ($undocumentedCount -gt 0) {
        $html += "<p><strong>$undocumentedCount undocumented APIs found:</strong></p>"
        $html += @"
            <table>
                <thead>
                    <tr><th>Type</th><th>Name</th><th>Namespace</th><th>File</th><th>Line</th></tr>
                </thead>
                <tbody>
"@
        
        foreach ($api in $Report.files | Where-Object { -not $_.HasDocumentation } | Sort-Object Namespace, Type, Name) {
            $fileName = Split-Path $api.FilePath -Leaf
            $html += @"
                    <tr class="undocumented">
                        <td>$($api.Type)</td>
                        <td>$($api.Name)</td>
                        <td>$($api.Namespace)</td>
                        <td>$fileName</td>
                        <td>$($api.LineNumber)</td>
                    </tr>
"@
        }
        
        $html += @"
                </tbody>
            </table>
"@
    } else {
        $html += "<p class='pass'>🎉 All public APIs are documented!</p>"
    }
    
    $html += @"
        </div>
        
        <div class="section">
            <h2>Coverage by Type</h2>
            <table>
                <thead>
                    <tr><th>Type</th><th>Total</th><th>Documented</th><th>Coverage</th></tr>
                </thead>
                <tbody>
"@
    
    $byType = $Report.files | Group-Object Type | Sort-Object Name
    foreach ($group in $byType) {
        $documented = ($group.Group | Where-Object { $_.HasDocumentation }).Count
        $percentage = ($documented / $group.Count) * 100
        
        $html += @"
                    <tr>
                        <td>$($group.Name)</td>
                        <td>$($group.Count)</td>
                        <td>$documented</td>
                        <td>$([math]::Round($percentage, 1))%</td>
                    </tr>
"@
    }
    
    $html += @"
                </tbody>
            </table>
        </div>
        
        <div class="section">
            <p><small>Report generated by TiXL Documentation Coverage Analysis Tool</small></p>
        </div>
    </div>
</body>
</html>
"@
    
    $html | Out-File -FilePath "$OutputPath/coverage-report.html" -Encoding UTF8
    Write-Host "HTML coverage report saved to: $OutputPath/coverage-report.html" -ForegroundColor Green
}
```

---

## 4. Coverage Monitoring and Reporting

### Daily Coverage Tracking Script

```powershell
# scripts/track-coverage-daily.ps1

param(
    [string]$DataPath = "docs/coverage-data"
)

Write-Host "TiXL Documentation Coverage Daily Tracking" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

# Ensure data directory exists
if (-not (Test-Path $DataPath)) {
    New-Item -ItemType Directory -Path $DataPath -Force | Out-Null
}

# Get current date for file naming
$date = Get-Date -Format "yyyy-MM-dd"
$outputFile = "$DataPath/coverage-$date.json"

# Run coverage analysis
Write-Host "Running daily coverage analysis..." -ForegroundColor Yellow
try {
    $result = & .\scripts\analyze-coverage.ps1 -SourcePath "src" -OutputPath "docs/temp-coverage"
    
    # Load the generated report
    $report = Get-Content "docs/temp-coverage/coverage-report.json" | ConvertFrom-Json
    
    # Add historical context
    $historicalData = @()
    if (Test-Path "$DataPath/coverage-history.json") {
        $historicalData = Get-Content "$DataPath/coverage-history.json" | ConvertFrom-Json
    }
    
    # Add today's data
    $dailyRecord = @{
        date = $date
        totalApis = $report.totalApis
        documentedApis = $report.documentedApis
        coveragePercentage = $report.coveragePercentage
        modules = $report.modules
        undocumentedCount = ($report.files | Where-Object { -not $_.HasDocumentation }).Count
    }
    
    $historicalData += $dailyRecord
    $historicalData | ConvertTo-Json -Depth 3 | Out-File -FilePath "$DataPath/coverage-history.json" -Encoding UTF8
    
    # Save today's detailed report
    $report | ConvertTo-Json -Depth 3 | Out-File -FilePath $outputFile -Encoding UTF8
    
    Write-Host "Coverage tracking completed!" -ForegroundColor Green
    Write-Host "Today's coverage: $([math]::Round($report.coveragePercentage, 2))%" -ForegroundColor Cyan
    
    # Generate trends analysis
    if ($historicalData.Count -gt 1) {
        $trend = $historicalData[-1].coveragePercentage - $historicalData[-2].coveragePercentage
        $trendText = if ($trend -gt 0) { "improving" } elseif ($trend -lt 0) { "declining" } else { "stable" }
        Write-Host "Coverage trend: $trendText ($(if ($trend -gt 0) { '+' })$([math]::Round($trend, 2))%)" -ForegroundColor $(if ($trend -gt 0) { "Green" } elseif ($trend -lt 0) { "Red" } else { "Yellow" })
    }
    
} catch {
    Write-Error "Coverage analysis failed: $($_.Exception.Message)"
    exit 1
}
```

---

## 5. Coverage Quality Gates

### GitHub Actions Integration

```yaml
# .github/workflows/coverage-analysis.yml

name: Documentation Coverage Analysis

on:
  push:
    branches: [ main, develop ]
    paths: [ 'src/**', 'docs/**' ]
  pull_request:
    branches: [ main ]
    paths: [ 'src/**', 'docs/**' ]
  schedule:
    - cron: '0 6 * * *'  # Daily at 6 AM UTC

jobs:
  coverage-analysis:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3
      with:
        fetch-depth: 0
        
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 8.0.x
        
    - name: Install Dependencies
      run: |
        dotnet restore Tools/CoverageAnalysis/TiXL.CoverageAnalysis.csproj
        
    - name: Run Coverage Analysis
      run: |
        dotnet run --project Tools/CoverageAnalysis/TiXL.CoverageAnalysis.csproj analyze --source src --output docs/coverage --threshold 80
        
    - name: Upload Coverage Reports
      uses: actions/upload-artifact@v3
      with:
        name: coverage-reports
        path: docs/coverage/
        
    - name: Check Coverage Threshold
      run: |
        # Parse coverage report and check against threshold
        COVERAGE=$(jq -r '.coveragePercentage' docs/coverage/coverage-report.json)
        THRESHOLD=80
        
        echo "Coverage: $COVERAGE%"
        echo "Threshold: $THRESHOLD%"
        
        if (( $(echo "$COVERAGE >= $THRESHOLD" | bc -l) )); then
          echo "Coverage check PASSED"
          exit 0
        else
          echo "Coverage check FAILED - Coverage $COVERAGE% is below threshold $THRESHOLD%"
          exit 1
        fi
        
    - name: Comment PR with Coverage
      if: github.event_name == 'pull_request'
      uses: actions/github-script@v6
      with:
        script: |
          const fs = require('fs');
          const coverage = JSON.parse(fs.readFileSync('docs/coverage/coverage-report.json', 'utf8'));
          
          const comment = `## 📊 Documentation Coverage Report
          
          **Generated:** ${coverage.timestamp}
          **Coverage:** ${coverage.coveragePercentage}% (${coverage.documentedApis}/${coverage.totalApis} APIs)
          
          ### Module Coverage:
          ${Object.entries(coverage.modules).map(([module, data]) => 
            `- **${module}:** ${data.documented}/${data.total} (${Math.round((data.documented/data.total)*100)}%)`
          ).join('\n')}
          
          ### Status: ${coverage.passed ? '✅ PASSED' : '❌ FAILED'}
          
          ${!coverage.passed ? `> ⚠️ Coverage is below the 80% threshold. Please add documentation to undocumented APIs.` : ''}
          `;
          
          github.rest.issues.createComment({
            issue_number: context.issue.number,
            owner: context.repo.owner,
            repo: context.repo.repo,
            body: comment
          });
```

---

## 6. Visual Coverage Dashboard

### Coverage Metrics Visualization

```html
<!-- docs/coverage-dashboard.html -->

<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>TiXL Documentation Coverage Dashboard</title>
    <script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; background: #f5f5f5; }
        .dashboard { max-width: 1400px; margin: 0 auto; }
        .header { background: linear-gradient(135deg, #0066cc, #4a90e2); color: white; padding: 20px; border-radius: 8px; margin-bottom: 20px; }
        .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(250px, 1fr)); gap: 20px; margin-bottom: 30px; }
        .metric-card { background: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); text-align: center; }
        .metric-value { font-size: 2.5rem; font-weight: bold; margin-bottom: 5px; }
        .metric-label { color: #666; font-size: 0.9rem; }
        .chart-container { background: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); margin-bottom: 20px; }
        .progress-bar { width: 100%; height: 20px; background: #ecf0f1; border-radius: 10px; overflow: hidden; margin: 10px 0; }
        .progress-fill { height: 100%; background: linear-gradient(90deg, #27ae60, #2ecc71); transition: width 0.3s ease; }
    </style>
</head>
<body>
    <div class="dashboard">
        <div class="header">
            <h1>📊 TiXL Documentation Coverage Dashboard</h1>
            <p>Real-time documentation coverage metrics and trends</p>
        </div>
        
        <div class="metrics-grid">
            <div class="metric-card">
                <div class="metric-value" id="totalCoverage">--</div>
                <div class="metric-label">Overall Coverage</div>
            </div>
            <div class="metric-card">
                <div class="metric-value" id="documentedApis">--</div>
                <div class="metric-label">Documented APIs</div>
            </div>
            <div class="metric-card">
                <div class="metric-value" id="totalApis">--</div>
                <div class="metric-label">Total APIs</div>
            </div>
            <div class="metric-card">
                <div class="metric-value" id="trend">--</div>
                <div class="metric-label">30-Day Trend</div>
            </div>
        </div>
        
        <div class="chart-container">
            <h3>Coverage by Module</h3>
            <canvas id="moduleChart" width="400" height="200"></canvas>
        </div>
        
        <div class="chart-container">
            <h3>Coverage Trend (Last 30 Days)</h3>
            <canvas id="trendChart" width="400" height="200"></canvas>
        </div>
        
        <div class="chart-container">
            <h3>Coverage by Type</h3>
            <canvas id="typeChart" width="400" height="200"></canvas>
        </div>
    </div>
    
    <script>
        // Load coverage data and render dashboard
        fetch('coverage-history.json')
            .then(response => response.json())
            .then(data => {
                updateDashboard(data);
            })
            .catch(error => {
                console.error('Error loading coverage data:', error);
                document.getElementById('totalCoverage').textContent = 'Error';
            });
        
        function updateDashboard(data) {
            if (data.length === 0) return;
            
            const latest = data[data.length - 1];
            
            // Update main metrics
            document.getElementById('totalCoverage').textContent = latest.coveragePercentage.toFixed(1) + '%';
            document.getElementById('documentedApis').textContent = latest.documentedApis;
            document.getElementById('totalApis').textContent = latest.totalApis;
            
            // Calculate trend
            const thirtyDaysAgo = data.find(d => 
                new Date(d.date) >= new Date(Date.now() - 30 * 24 * 60 * 60 * 1000)
            );
            if (thirtyDaysAgo) {
                const trend = latest.coveragePercentage - thirtyDaysAgo.coveragePercentage;
                const trendElement = document.getElementById('trend');
                trendElement.textContent = (trend > 0 ? '+' : '') + trend.toFixed(1) + '%';
                trendElement.style.color = trend >= 0 ? '#27ae60' : '#e74c3c';
            }
            
            // Render charts
            renderModuleChart(latest.modules);
            renderTrendChart(data);
            renderTypeChart(latest.typeBreakdown);
        }
        
        function renderModuleChart(modules) {
            const ctx = document.getElementById('moduleChart').getContext('2d');
            new Chart(ctx, {
                type: 'bar',
                data: {
                    labels: Object.keys(modules),
                    datasets: [{
                        label: 'Coverage %',
                        data: Object.values(modules).map(m => 
                            m.total > 0 ? (m.documented / m.total) * 100 : 0
                        ),
                        backgroundColor: ['#3498db', '#e74c3c', '#f39c12'],
                        borderColor: ['#2980b9', '#c0392b', '#e67e22'],
                        borderWidth: 1
                    }]
                },
                options: {
                    scales: {
                        y: {
                            beginAtZero: true,
                            max: 100,
                            ticks: {
                                callback: function(value) {
                                    return value + '%';
                                }
                            }
                        }
                    }
                }
            });
        }
        
        function renderTrendChart(data) {
            const ctx = document.getElementById('trendChart').getContext('2d');
            new Chart(ctx, {
                type: 'line',
                data: {
                    labels: data.map(d => d.date),
                    datasets: [{
                        label: 'Coverage %',
                        data: data.map(d => d.coveragePercentage),
                        borderColor: '#3498db',
                        backgroundColor: 'rgba(52, 152, 219, 0.1)',
                        fill: true,
                        tension: 0.1
                    }]
                },
                options: {
                    scales: {
                        y: {
                            beginAtZero: true,
                            max: 100,
                            ticks: {
                                callback: function(value) {
                                    return value + '%';
                                }
                            }
                        }
                    }
                }
            });
        }
    </script>
</body>
</html>
```

---

## 7. Configuration and Usage

### Configuration File

```json
{
  "coverage": {
    "sourcePaths": [
      "src/Core",
      "src/Operators", 
      "src/Editor"
    ],
    "excludedPatterns": [
      "**/bin/**",
      "**/obj/**",
      "**/*.Designer.cs",
      "**/Resources/*.cs",
      "**/Test*.cs"
    ],
    "thresholds": {
      "overall": 80,
      "perModule": 70,
      "exampleCoverage": 50,
      "crossReferenceCoverage": 60
    },
    "output": {
      "jsonReport": "docs/coverage/coverage-report.json",
      "htmlReport": "docs/coverage/coverage-report.html",
      "dashboard": "docs/coverage/coverage-dashboard.html",
      "historicalData": "docs/coverage-data/coverage-history.json"
    },
    "alerts": {
      "emailOnFailure": true,
      "slackWebhook": "",
      "githubIssueThreshold": 50
    }
  }
}
```

### Usage Examples

```powershell
# Basic coverage analysis
.\scripts\analyze-coverage.ps1

# Detailed analysis with HTML report
.\scripts\analyze-coverage.ps1 -Detailed -GenerateHtml -Threshold 85

# Track daily coverage for monitoring
.\scripts\track-coverage-daily.ps1

# Check coverage in CI/CD
dotnet run --project Tools/CoverageAnalysis analyze --source src --threshold 80 --fail-on-threshold
```

---

**Document Version**: 1.0  
**Last Updated**: 2025-11-02  
**Dependencies**: .NET 8.0+, Roslyn SDK, PowerShell 7.0+