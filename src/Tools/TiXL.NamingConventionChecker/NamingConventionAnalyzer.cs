using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;

namespace TiXL.NamingConventionChecker
{
    /// <summary>
    /// Analyzes C# code for naming convention violations
    /// </summary>
    public class NamingConventionAnalyzer
    {
        private readonly Dictionary<string, Func<SyntaxNode, Violation?>> _analyzers;

        public NamingConventionAnalyzer()
        {
            _analyzers = new Dictionary<string, Func<SyntaxNode, Violation?>>
            {
                [SyntaxKind.ClassDeclaration] = AnalyzeClass,
                [SyntaxKind.InterfaceDeclaration] = AnalyzeInterface,
                [SyntaxKind.MethodDeclaration] = AnalyzeMethod,
                [SyntaxKind.PropertyDeclaration] = AnalyzeProperty,
                [SyntaxKind.FieldDeclaration] = AnalyzeField,
                [SyntaxKind.EventDeclaration] = AnalyzeEvent,
                [SyntaxKind.EnumDeclaration] = AnalyzeEnum,
                [SyntaxKind.EnumMemberDeclaration] = AnalyzeEnumMember,
                [SyntaxKind.NamespaceDeclaration] = AnalyzeNamespace
            };
        }

        /// <summary>
        /// Analyzes a solution for naming convention violations
        /// </summary>
        public async Task<List<Violation>> AnalyzeSolutionAsync(Solution solution, string projectPattern, bool includeGenerated)
        {
            var violations = new List<Violation>();

            foreach (var project in solution.Projects)
            {
                // Filter projects by pattern
                if (!System.Text.RegularExpressions.Regex.IsMatch(project.Name, projectPattern))
                    continue;

                foreach (var document in project.Documents)
                {
                    // Skip generated files if not requested
                    if (!includeGenerated && IsGeneratedFile(document))
                        continue;

                    try
                    {
                        var docViolations = await AnalyzeDocumentAsync(document);
                        violations.AddRange(docViolations);
                    }
                    catch (Exception ex)
                    {
                        // Log warning but continue with other documents
                        Console.WriteLine($"Warning: Could not analyze {document.FilePath}: {ex.Message}");
                    }
                }
            }

            return violations;
        }

        /// <summary>
        /// Analyzes a document for naming convention violations
        /// </summary>
        public async Task<List<Violation>> AnalyzeDocumentAsync(Document document)
        {
            var violations = new List<Violation>();

            try
            {
                var syntaxTree = await document.GetSyntaxTreeAsync();
                var root = await syntaxTree.GetRootAsync();

                foreach (var node in root.DescendantNodes())
                {
                    if (_analyzers.TryGetValue(node.Kind().ToString(), out var analyzer))
                    {
                        var violation = analyzer(node);
                        if (violation != null)
                        {
                            violation.FileName = document.FilePath;
                            violations.Add(violation);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to analyze document {document.FilePath}", ex);
            }

            return violations;
        }

        private Violation? AnalyzeClass(SyntaxNode node)
        {
            if (node is not ClassDeclarationSyntax classDecl) return null;

            var className = classDecl.Identifier.Text;
            if (string.IsNullOrEmpty(className) || IsPascalCase(className))
                return null;

            return new Violation
            {
                RuleId = "TiXL012002",
                Description = "Class name should use PascalCase",
                CurrentName = className,
                SuggestedFix = ToPascalCase(className),
                Span = classDecl.Identifier.Span,
                LineNumber = classDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                ColumnNumber = classDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Character + 1,
                Severity = "Warning",
                ElementType = "Class",
                ViolationType = ViolationType.ClassName
            };
        }

        private Violation? AnalyzeInterface(SyntaxNode node)
        {
            if (node is not InterfaceDeclarationSyntax interfaceDecl) return null;

            var interfaceName = interfaceDecl.Identifier.Text;
            if (string.IsNullOrEmpty(interfaceName)) return null;

            var issues = new List<string>();

            // Check for I prefix
            if (!interfaceName.StartsWith("I", StringComparison.Ordinal) || 
                (interfaceName.Length > 1 && !char.IsUpper(interfaceName[1])))
            {
                issues.Add("Interface should be prefixed with 'I'");
            }

            // Check PascalCase for rest of name
            if (interfaceName.Length > 1 && !IsPascalCase(interfaceName.Substring(1)))
            {
                issues.Add("Interface name should use PascalCase");
            }

            if (issues.Count > 0)
            {
                return new Violation
                {
                    RuleId = "TiXL012003",
                    Description = string.Join("; ", issues),
                    CurrentName = interfaceName,
                    SuggestedFix = EnsureIPrefix(ToPascalCase(interfaceName)),
                    Span = interfaceDecl.Identifier.Span,
                    LineNumber = interfaceDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    ColumnNumber = interfaceDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Character + 1,
                    Severity = "Error",
                    ElementType = "Interface",
                    ViolationType = ViolationType.InterfaceName
                };
            }

            return null;
        }

        private Violation? AnalyzeMethod(SyntaxNode node)
        {
            if (node is not MethodDeclarationSyntax methodDecl) return null;

            var methodName = methodDecl.Identifier.Text;
            if (string.IsNullOrEmpty(methodName) || methodName == "Main") return null;

            if (!IsPascalCase(methodName))
            {
                return new Violation
                {
                    RuleId = "TiXL012004",
                    Description = "Method name should use PascalCase with verb-object structure",
                    CurrentName = methodName,
                    SuggestedFix = ToPascalCase(methodName),
                    Span = methodDecl.Identifier.Span,
                    LineNumber = methodDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    ColumnNumber = methodDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Character + 1,
                    Severity = "Warning",
                    ElementType = "Method",
                    ViolationType = ViolationType.MethodName
                };
            }

            return null;
        }

        private Violation? AnalyzeProperty(SyntaxNode node)
        {
            if (node is not PropertyDeclarationSyntax propertyDecl) return null;

            var propertyName = propertyDecl.Identifier.Text;
            if (string.IsNullOrEmpty(propertyName)) return null;

            if (!IsPascalCase(propertyName))
            {
                return new Violation
                {
                    RuleId = "TiXL012005",
                    Description = "Property name should use PascalCase",
                    CurrentName = propertyName,
                    SuggestedFix = ToPascalCase(propertyName),
                    Span = propertyDecl.Identifier.Span,
                    LineNumber = propertyDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    ColumnNumber = propertyDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Character + 1,
                    Severity = "Warning",
                    ElementType = "Property",
                    ViolationType = ViolationType.PropertyName
                };
            }

            return null;
        }

        private Violation? AnalyzeField(SyntaxNode node)
        {
            if (node is not FieldDeclarationSyntax fieldDecl) return null;

            var violations = new List<Violation>();

            foreach (var variable in fieldDecl.Declaration.Variables)
            {
                var fieldName = variable.Identifier.Text;
                if (string.IsNullOrEmpty(fieldName)) continue;

                var isConst = fieldDecl.Modifiers.Any(SyntaxKind.ConstKeyword);
                var isPrivate = fieldDecl.Modifiers.Any(SyntaxKind.PrivateKeyword) ||
                               (!fieldDecl.Modifiers.Any(SyntaxKind.PublicKeyword) && 
                                !fieldDecl.Modifiers.Any(SyntaxKind.InternalKeyword));

                if (isConst)
                {
                    // Constants should be PascalCase
                    if (!IsPascalCase(fieldName))
                    {
                        violations.Add(new Violation
                        {
                            RuleId = "TiXL012009",
                            Description = "Constants should use PascalCase",
                            CurrentName = fieldName,
                            SuggestedFix = ToPascalCase(fieldName),
                            Span = variable.Identifier.Span,
                            LineNumber = variable.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                            ColumnNumber = variable.Identifier.GetLocation().GetLineSpan().StartLinePosition.Character + 1,
                            Severity = "Error",
                            ElementType = "Constant",
                            ViolationType = ViolationType.ConstantsName
                        });
                    }
                }
                else if (isPrivate)
                {
                    // Private fields should be _camelCase
                    if (!IsValidPrivateFieldName(fieldName))
                    {
                        violations.Add(new Violation
                        {
                            RuleId = "TiXL012006",
                            Description = "Private field should use _camelCase with underscore prefix",
                            CurrentName = fieldName,
                            SuggestedFix = FixFieldName(fieldName),
                            Span = variable.Identifier.Span,
                            LineNumber = variable.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                            ColumnNumber = variable.Identifier.GetLocation().GetLineSpan().StartLinePosition.Character + 1,
                            Severity = "Error",
                            ElementType = "Field",
                            ViolationType = ViolationType.FieldName
                        });
                    }
                }
            }

            return violations.FirstOrDefault();
        }

        private Violation? AnalyzeEvent(SyntaxNode node)
        {
            if (node is not EventDeclarationSyntax eventDecl) return null;

            var eventName = eventDecl.Identifier.Text;
            if (string.IsNullOrEmpty(eventName)) return null;

            if (!IsPascalCase(eventName))
            {
                return new Violation
                {
                    RuleId = "TiXL012007",
                    Description = "Event name should use PascalCase and follow event naming conventions",
                    CurrentName = eventName,
                    SuggestedFix = ToPascalCase(eventName),
                    Span = eventDecl.Identifier.Span,
                    LineNumber = eventDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    ColumnNumber = eventDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Character + 1,
                    Severity = "Warning",
                    ElementType = "Event",
                    ViolationType = ViolationType.EventName
                };
            }

            return null;
        }

        private Violation? AnalyzeEnum(SyntaxNode node)
        {
            if (node is not EnumDeclarationSyntax enumDecl) return null;

            var enumName = enumDecl.Identifier.Text;
            if (string.IsNullOrEmpty(enumName)) return null;

            if (!IsPascalCase(enumName))
            {
                return new Violation
                {
                    RuleId = "TiXL012008",
                    Description = "Enum name should use PascalCase",
                    CurrentName = enumName,
                    SuggestedFix = ToPascalCase(enumName),
                    Span = enumDecl.Identifier.Span,
                    LineNumber = enumDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    ColumnNumber = enumDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition.Character + 1,
                    Severity = "Warning",
                    ElementType = "Enum",
                    ViolationType = ViolationType.EnumName
                };
            }

            return null;
        }

        private Violation? AnalyzeEnumMember(SyntaxNode node)
        {
            if (node is not EnumMemberDeclarationSyntax enumMember) return null;

            var memberName = enumMember.Identifier.Text;
            if (string.IsNullOrEmpty(memberName)) return null;

            if (!IsPascalCase(memberName))
            {
                return new Violation
                {
                    RuleId = "TiXL012008",
                    Description = "Enum member should use PascalCase",
                    CurrentName = memberName,
                    SuggestedFix = ToPascalCase(memberName),
                    Span = enumMember.Identifier.Span,
                    LineNumber = enumMember.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    ColumnNumber = enumMember.Identifier.GetLocation().GetLineSpan().StartLinePosition.Character + 1,
                    Severity = "Warning",
                    ElementType = "Enum Member",
                    ViolationType = ViolationType.EnumMemberName
                };
            }

            return null;
        }

        private Violation? AnalyzeNamespace(SyntaxNode node)
        {
            if (node is not NamespaceDeclarationSyntax namespaceDecl) return null;

            var namespaceName = namespaceDecl.Name.ToString();
            if (string.IsNullOrEmpty(namespaceName)) return null;

            // Check TiXL namespace pattern
            if (namespaceName.StartsWith("TiXL.", StringComparison.Ordinal) && !IsValidTiXLNamespace(namespaceName))
            {
                return new Violation
                {
                    RuleId = "TiXL012001",
                    Description = "TiXL namespace should follow pattern 'TiXL.{Module}.{Feature}.{SubFeature}' and use PascalCase",
                    CurrentName = namespaceName,
                    SuggestedFix = ToPascalCaseNamespace(namespaceName),
                    Span = namespaceDecl.Name.Span,
                    LineNumber = namespaceDecl.Name.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    ColumnNumber = namespaceDecl.Name.GetLocation().GetLineSpan().StartLinePosition.Character + 1,
                    Severity = "Error",
                    ElementType = "Namespace",
                    ViolationType = ViolationType.NamespaceName
                };
            }

            return null;
        }

        #region Helper Methods

        private static bool IsPascalCase(string name)
        {
            return !string.IsNullOrEmpty(name) && 
                   char.IsUpper(name[0]) && 
                   !name.Contains("_") && 
                   !name.Contains("-");
        }

        private static bool IsCamelCase(string name)
        {
            return !string.IsNullOrEmpty(name) && 
                   char.IsLower(name[0]) && 
                   !name.Contains("_") && 
                   !name.Contains("-");
        }

        private static bool IsValidPrivateFieldName(string fieldName)
        {
            return fieldName.StartsWith("_", StringComparison.Ordinal) && 
                   IsCamelCase(fieldName.Substring(1));
        }

        private static bool IsValidTiXLNamespace(string namespaceName)
        {
            if (!namespaceName.StartsWith("TiXL.", StringComparison.Ordinal))
                return false;

            var parts = namespaceName.Split('.');
            
            // Must have at least TiXL.Module format
            if (parts.Length < 3)
                return false;

            // Check that all parts use PascalCase
            return parts.Skip(1).All(part => !string.IsNullOrEmpty(part) && IsPascalCase(part));
        }

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

        private static string ToCamelCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var pascalCase = ToPascalCase(input);
            return char.ToLowerInvariant(pascalCase[0]) + pascalCase.Substring(1);
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

        private static bool IsGeneratedFile(Document document)
        {
            var fileName = document.FilePath;
            return fileName.Contains(".Designer.") ||
                   fileName.Contains(".g.") ||
                   fileName.Contains(".AssemblyAttributes.") ||
                   fileName.EndsWith(".Generated.cs") ||
                   fileName.EndsWith(".Designer.cs");
        }

        #endregion
    }
}