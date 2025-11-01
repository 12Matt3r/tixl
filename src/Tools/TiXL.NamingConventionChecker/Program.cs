using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Rename;

namespace TiXL.NamingConventionChecker
{
    /// <summary>
    /// Command-line tool for analyzing and fixing naming convention violations in TiXL codebase
    /// </summary>
    internal class Program
    {
        private static async Task<int> Main(string[] args)
        {
            // Set up MSBuild locator
            var instances = MSBuildLocator.QueryVisualStudioInstances();
            var instance = instances.OrderByDescending(x => x.Version).First();
            MSBuildLocator.RegisterInstance(instance);

            // Configure root command
            var rootCommand = new RootCommand("TiXL Naming Convention Checker - Analyze and fix naming violations")
            {
                new Option<string>("--solution-path", "Path to solution file") { IsRequired = true },
                new Option<string>("--project-pattern", "Project name pattern to filter (e.g., TiXL.*)", () => "TiXL.*"),
                new Option<bool>("--apply-fixes", "Apply fixes automatically"),
                new Option<string>("--output-format", "Output format (console, json, csv)", () => "console"),
                new Option<string>("--output-file", "Output file path for reports"),
                new Option<bool>("--include-generated", "Include generated code files"),
                new Option<bool>("--verbose", "Enable verbose output"),
                new Option<bool>("--show-fixes", "Show available fixes without applying"),
                new Option<int>("--max-violations", "Maximum violations to report", () => 1000)
            };

            rootCommand.Handler = CommandHandler.Create<string, string, bool, string, string, bool, bool, bool, int>(ExecuteAsync);

            return await rootCommand.InvokeAsync(args);
        }

        private static async Task<int> ExecuteAsync(string solutionPath, string projectPattern, bool applyFixes, 
            string outputFormat, string outputFile, bool includeGenerated, bool verbose, bool showFixes, int maxViolations)
        {
            try
            {
                if (!File.Exists(solutionPath))
                {
                    Console.WriteLine($"Error: Solution file not found: {solutionPath}");
                    return 1;
                }

                Console.WriteLine($"TiXL Naming Convention Checker");
                Console.WriteLine($"Solution: {solutionPath}");
                Console.WriteLine($"Project Pattern: {projectPattern}");
                Console.WriteLine($"Apply Fixes: {applyFixes}");
                Console.WriteLine($"Include Generated: {includeGenerated}");
                Console.WriteLine();

                // Load solution
                var workspace = MSBuildWorkspace.Create();
                var solution = await workspace.OpenSolutionAsync(solutionPath);

                var analyzer = new NamingConventionAnalyzer();
                var results = await analyzer.AnalyzeSolutionAsync(solution, projectPattern, includeGenerated);

                // Filter and limit results
                var filteredResults = results.Take(maxViolations).ToList();

                // Output results
                await OutputResultsAsync(filteredResults, outputFormat, outputFile, verbose);

                // Show fixes if requested
                if (showFixes)
                {
                    await ShowFixesAsync(filteredResults, solution);
                }

                // Apply fixes if requested
                if (applyFixes)
                {
                    var fixedCount = await ApplyFixesAsync(filteredResults, solution);
                    Console.WriteLine($"Applied fixes to {fixedCount} violations");
                }

                // Summary
                Console.WriteLine();
                Console.WriteLine($"Analysis Summary:");
                Console.WriteLine($"  Total violations found: {results.Count}");
                Console.WriteLine($"  Violations reported: {filteredResults.Count}");
                Console.WriteLine($"  Violations fixed: {applyFixes ? filteredResults.Count : 0}");

                return results.Count > 0 ? 1 : 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                if (verbose)
                {
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                }
                return 1;
            }
        }

        private static async Task OutputResultsAsync(List<Violation> violations, string format, string outputFile, bool verbose)
        {
            switch (format.ToLower())
            {
                case "json":
                    await OutputJsonAsync(violations, outputFile);
                    break;
                case "csv":
                    await OutputCsvAsync(violations, outputFile);
                    break;
                case "console":
                default:
                    OutputConsole(violations, verbose);
                    break;
            }
        }

        private static void OutputConsole(List<Violation> violations, bool verbose)
        {
            if (violations.Count == 0)
            {
                Console.WriteLine("✓ No naming convention violations found.");
                return;
            }

            Console.WriteLine($"Found {violations.Count} naming convention violations:");
            Console.WriteLine();

            var groupedBySeverity = violations.GroupBy(v => v.Severity).OrderBy(g => g.Key);

            foreach (var group in groupedBySeverity)
            {
                Console.WriteLine($"{group.Key} Violations ({group.Count()}):");
                foreach (var violation in group.Take(20)) // Limit to 20 per severity for console output
                {
                    Console.WriteLine($"  {FormatViolation(violation)}");
                    if (verbose && !string.IsNullOrEmpty(violation.SuggestedFix))
                    {
                        Console.WriteLine($"    Suggested fix: {violation.SuggestedFix}");
                    }
                }
                
                if (group.Count() > 20)
                {
                    Console.WriteLine($"    ... and {group.Count() - 20} more");
                }
                Console.WriteLine();
            }
        }

        private static async Task OutputJsonAsync(List<Violation> violations, string outputFile)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(violations, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true 
            });

            if (!string.IsNullOrEmpty(outputFile))
            {
                await File.WriteAllTextAsync(outputFile, json);
                Console.WriteLine($"Results written to: {outputFile}");
            }
            else
            {
                Console.WriteLine(json);
            }
        }

        private static async Task OutputCsvAsync(List<Violation> violations, string outputFile)
        {
            var csvLines = new List<string>
            {
                "Severity,Rule,File,Line,Column,Element,CurrentName,SuggestedFix,Description"
            };

            foreach (var violation in violations)
            {
                csvLines.Add($"{violation.Severity},{violation.RuleId},{violation.FileName},{violation.LineNumber},{violation.ColumnNumber},{violation.ElementType},{violation.CurrentName},\"{violation.SuggestedFix}\",\"{violation.Description}\"");
            }

            var csv = string.Join(Environment.NewLine, csvLines);

            if (!string.IsNullOrEmpty(outputFile))
            {
                await File.WriteAllTextAsync(outputFile, csv);
                Console.WriteLine($"Results written to: {outputFile}");
            }
            else
            {
                Console.WriteLine(csv);
            }
        }

        private static async Task ShowFixesAsync(List<Violation> violations, Solution solution)
        {
            Console.WriteLine("Available fixes:");
            Console.WriteLine();

            foreach (var violation in violations.Take(50)) // Limit for display
            {
                Console.WriteLine($"{FormatViolation(violation)}");
                if (!string.IsNullOrEmpty(violation.SuggestedFix))
                {
                    Console.WriteLine($"  → {violation.SuggestedFix}");
                }
                Console.WriteLine();
            }
        }

        private static async Task<int> ApplyFixesAsync(List<Violation> violations, Solution solution)
        {
            var fixedCount = 0;

            // Group violations by file and type for batch processing
            var violationsByFile = violations.GroupBy(v => v.FileName);

            foreach (var fileGroup in violationsByFile)
            {
                try
                {
                    var document = solution.Projects.SelectMany(p => p.Documents)
                        .FirstOrDefault(d => d.FilePath == fileGroup.Key);

                    if (document != null)
                    {
                        var semanticModel = await document.GetSemanticModelAsync();
                        var root = await document.GetSyntaxRootAsync();

                        foreach (var violation in fileGroup)
                        {
                            try
                            {
                                var node = root.FindNode(violation.Span);
                                var fixedNode = ApplyFix(node, violation);
                                
                                if (fixedNode != node)
                                {
                                    root = root.ReplaceNode(node, fixedNode);
                                    fixedCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Warning: Could not fix {FormatViolation(violation)}: {ex.Message}");
                            }
                        }

                        // Update document with fixed content
                        solution = solution.WithDocumentText(document.Id, root.GetText());
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not process file {fileGroup.Key}: {ex.Message}");
                }
            }

            return fixedCount;
        }

        private static SyntaxNode ApplyFix(SyntaxNode node, Violation violation)
        {
            return violation.ViolationType switch
            {
                ViolationType.ClassName => FixClassName(node),
                ViolationType.InterfaceName => FixInterfaceName(node),
                ViolationType.MethodName => FixMethodName(node),
                ViolationType.PropertyName => FixPropertyName(node),
                ViolationType.FieldName => FixFieldName(node),
                ViolationType.EventName => FixEventName(node),
                ViolationType.EnumName => FixEnumName(node),
                ViolationType.NamespaceName => FixNamespaceName(node),
                _ => node
            };
        }

        private static string FormatViolation(Violation violation)
        {
            return $"{Path.GetFileName(violation.FileName)}({violation.LineNumber},{violation.ColumnNumber}): " +
                   $"{violation.Severity} {violation.RuleId}: {violation.ElementType} '{violation.CurrentName}' - {violation.Description}";
        }

        #region Fix Methods

        private static SyntaxNode FixClassName(SyntaxNode node)
        {
            if (node is ClassDeclarationSyntax classDecl)
            {
                var newName = ToPascalCase(classDecl.Identifier.Text);
                return classDecl.WithIdentifier(SyntaxFactory.Identifier(newName).WithTriviaFrom(classDecl.Identifier));
            }
            return node;
        }

        private static SyntaxNode FixInterfaceName(SyntaxNode node)
        {
            if (node is InterfaceDeclarationSyntax interfaceDecl)
            {
                var newName = EnsureIPrefix(ToPascalCase(interfaceDecl.Identifier.Text));
                return interfaceDecl.WithIdentifier(SyntaxFactory.Identifier(newName).WithTriviaFrom(interfaceDecl.Identifier));
            }
            return node;
        }

        private static SyntaxNode FixMethodName(SyntaxNode node)
        {
            if (node is MethodDeclarationSyntax methodDecl)
            {
                var newName = ToPascalCase(methodDecl.Identifier.Text);
                return methodDecl.WithIdentifier(SyntaxFactory.Identifier(newName).WithTriviaFrom(methodDecl.Identifier));
            }
            return node;
        }

        private static SyntaxNode FixPropertyName(SyntaxNode node)
        {
            if (node is PropertyDeclarationSyntax propertyDecl)
            {
                var newName = ToPascalCase(propertyDecl.Identifier.Text);
                return propertyDecl.WithIdentifier(SyntaxFactory.Identifier(newName).WithTriviaFrom(propertyDecl.Identifier));
            }
            return node;
        }

        private static SyntaxNode FixFieldName(SyntaxNode node)
        {
            if (node is VariableDeclaratorSyntax varDecl)
            {
                var oldName = varDecl.Identifier.Text;
                var newName = FixFieldName(oldName);
                return varDecl.WithIdentifier(SyntaxFactory.Identifier(newName).WithTriviaFrom(varDecl.Identifier));
            }
            return node;
        }

        private static SyntaxNode FixEventName(SyntaxNode node)
        {
            if (node is EventDeclarationSyntax eventDecl)
            {
                var newName = ToPascalCase(eventDecl.Identifier.Text);
                return eventDecl.WithIdentifier(SyntaxFactory.Identifier(newName).WithTriviaFrom(eventDecl.Identifier));
            }
            return node;
        }

        private static SyntaxNode FixEnumName(SyntaxNode node)
        {
            if (node is EnumDeclarationSyntax enumDecl)
            {
                var newName = ToPascalCase(enumDecl.Identifier.Text);
                return enumDecl.WithIdentifier(SyntaxFactory.Identifier(newName).WithTriviaFrom(enumDecl.Identifier));
            }
            else if (node is EnumMemberDeclarationSyntax enumMember)
            {
                var newName = ToPascalCase(enumMember.Identifier.Text);
                return enumMember.WithIdentifier(SyntaxFactory.Identifier(newName).WithTriviaFrom(enumMember.Identifier));
            }
            return node;
        }

        private static SyntaxNode FixNamespaceName(SyntaxNode node)
        {
            if (node is NamespaceDeclarationSyntax namespaceDecl)
            {
                var newName = ToPascalCaseNamespace(namespaceDecl.Name.ToString());
                var newNamespaceName = SyntaxFactory.ParseName(newName).WithTriviaFrom(namespaceDecl.Name);
                return namespaceDecl.WithName(newNamespaceName);
            }
            return node;
        }

        #endregion

        #region Helper Methods

        private static string ToPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            // Handle underscores and hyphens
            var words = input.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < words.Length; i++)
            {
                if (!string.IsNullOrEmpty(words[i]))
                {
                    words[i] = char.ToUpperInvariant(words[i][0]) + 
                               (words[i].Length > 1 ? words[i].Substring(1).ToLowerInvariant() : "");
                }
            }

            return string.Join("", words);
        }

        private static string EnsureIPrefix(string interfaceName)
        {
            if (interfaceName.StartsWith("I", StringComparison.Ordinal) && 
                interfaceName.Length > 1 && char.IsUpper(interfaceName[1]))
            {
                return interfaceName;
            }

            return "I" + ToPascalCase(interfaceName.TrimStart('i', 'I'));
        }

        private static string ToPascalCaseNamespace(string namespaceName)
        {
            if (string.IsNullOrEmpty(namespaceName)) return namespaceName;

            if (namespaceName.StartsWith("TiXL.", StringComparison.Ordinal))
            {
                var parts = namespaceName.Split('.');
                var result = new List<string> { "TiXL" };

                for (int i = 1; i < parts.Length; i++)
                {
                    result.Add(ToPascalCase(parts[i]));
                }

                return string.Join(".", result);
            }

            return ToPascalCase(namespaceName);
        }

        private static string FixFieldName(string fieldName)
        {
            // Private fields should be _camelCase
            if (!fieldName.StartsWith("_", StringComparison.Ordinal))
            {
                return "_" + ToCamelCase(fieldName);
            }
            else
            {
                return "_" + ToCamelCase(fieldName.Substring(1));
            }
        }

        private static string ToCamelCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var pascalCase = ToPascalCase(input);
            return char.ToLowerInvariant(pascalCase[0]) + pascalCase.Substring(1);
        }

        #endregion
    }
}