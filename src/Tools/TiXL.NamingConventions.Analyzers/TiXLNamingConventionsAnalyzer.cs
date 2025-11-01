using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TiXL.NamingConventions.Analyzers
{
    /// <summary>
    /// Main analyzer for TiXL naming conventions
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class TiXLNamingConventionsAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "TiXL012";

        private static readonly DiagnosticDescriptor NamespaceRule = new(
            DiagnosticId + "001",
            "Invalid namespace naming",
            "Namespace '{0}' should follow pattern 'TiXL.{{Module}}.{{Feature}}.{{SubFeature}}' and use PascalCase",
            "Naming",
            DiagnosticSeverity.Error,
            true);

        private static readonly DiagnosticDescriptor ClassRule = new(
            DiagnosticId + "002",
            "Invalid class naming",
            "Class '{0}' should use PascalCase with descriptive name",
            "Naming",
            DiagnosticSeverity.Warning,
            true);

        private static readonly DiagnosticDescriptor InterfaceRule = new(
            DiagnosticId + "003",
            "Invalid interface naming",
            "Interface '{0}' should be prefixed with 'I' and use PascalCase",
            "Naming",
            DiagnosticSeverity.Error,
            true);

        private static readonly DiagnosticDescriptor MethodRule = new(
            DiagnosticId + "004",
            "Invalid method naming",
            "Method '{0}' should use PascalCase with verb-object structure",
            "Naming",
            DiagnosticSeverity.Warning,
            true);

        private static readonly DiagnosticDescriptor PropertyRule = new(
            DiagnosticId + "005",
            "Invalid property naming",
            "Property '{0}' should use PascalCase",
            "Naming",
            DiagnosticSeverity.Warning,
            true);

        private static readonly DiagnosticDescriptor PrivateFieldRule = new(
            DiagnosticId + "006",
            "Invalid private field naming",
            "Private field '{0}' should use _camelCase with underscore prefix",
            "Naming",
            DiagnosticSeverity.Error,
            true);

        private static readonly DiagnosticDescriptor EventRule = new(
            DiagnosticId + "007",
            "Invalid event naming",
            "Event '{0}' should use PascalCase and follow event naming conventions",
            "Naming",
            DiagnosticSeverity.Warning,
            true);

        private static readonly DiagnosticDescriptor EnumRule = new(
            DiagnosticId + "008",
            "Invalid enum naming",
            "Enum '{0}' and its values should use PascalCase",
            "Naming",
            DiagnosticSeverity.Warning,
            true);

        private static readonly DiagnosticDescriptor ConstantsRule = new(
            DiagnosticId + "009",
            "Invalid constants naming",
            "Constants should use PascalCase with descriptive names",
            "Naming",
            DiagnosticSeverity.Error,
            true);

        private static readonly DiagnosticDescriptor TiXLModuleRule = new(
            DiagnosticId + "010",
            "TiXL module naming violation",
            "Code in TiXL project '{0}' should follow TiXL-specific naming patterns",
            "Naming",
            DiagnosticSeverity.Info,
            true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => 
            ImmutableArray.Create(
                NamespaceRule, ClassRule, InterfaceRule, MethodRule, 
                PropertyRule, PrivateFieldRule, EventRule, 
                EnumRule, ConstantsRule, TiXLModuleRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // Register syntax node analysis
            context.RegisterSyntaxNodeAction(AnalyzeNamespace, SyntaxKind.NamespaceDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeInterface, SyntaxKind.InterfaceDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeProperty, SyntaxKind.PropertyDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeField, SyntaxKind.FieldDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeEvent, SyntaxKind.EventDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeEnum, SyntaxKind.EnumDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeEnumMember, SyntaxKind.EnumMemberDeclaration);
        }

        private void AnalyzeNamespace(SyntaxNodeAnalysisContext context)
        {
            var namespaceDeclaration = (NamespaceDeclarationSyntax)context.Node;
            var namespaceName = namespaceDeclaration.Name.ToString();

            // Check TiXL namespace pattern
            if (IsInTiXLProject(context))
            {
                if (!IsValidTiXLNamespace(namespaceName))
                {
                    var diagnostic = Diagnostic.Create(NamespaceRule, namespaceDeclaration.Name.GetLocation(), namespaceName);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private void AnalyzeClass(SyntaxNodeAnalysisContext context)
        {
            var classDeclaration = (ClassDeclarationSyntax)context.Node;
            var className = classDeclaration.Identifier.Text;

            if (string.IsNullOrEmpty(className)) return;

            // Check PascalCase
            if (!IsPascalCase(className))
            {
                var diagnostic = Diagnostic.Create(ClassRule, classDeclaration.Identifier.GetLocation(), className);
                context.ReportDiagnostic(diagnostic);
            }

            // Check TiXL-specific patterns for classes in TiXL projects
            if (IsInTiXLProject(context))
            {
                AnalyzeTiXLSpecificClassPatterns(context, classDeclaration, className);
            }
        }

        private void AnalyzeInterface(SyntaxNodeAnalysisContext context)
        {
            var interfaceDeclaration = (InterfaceDeclarationSyntax)context.Node;
            var interfaceName = interfaceDeclaration.Identifier.Text;

            if (string.IsNullOrEmpty(interfaceName)) return;

            // Check I prefix
            if (!interfaceName.StartsWith("I", StringComparison.Ordinal) || !char.IsUpper(interfaceName[1]))
            {
                var diagnostic = Diagnostic.Create(InterfaceRule, interfaceDeclaration.Identifier.GetLocation(), interfaceName);
                context.ReportDiagnostic(diagnostic);
            }

            // Check PascalCase for rest of name
            if (interfaceName.Length > 1 && !IsPascalCase(interfaceName.Substring(1)))
            {
                var diagnostic = Diagnostic.Create(InterfaceRule, interfaceDeclaration.Identifier.GetLocation(), interfaceName);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            var methodDeclaration = (MethodDeclarationSyntax)context.Node;

            // Skip methods in generated code or partial methods
            if (methodDeclaration.Parent is not CompilationUnitSyntax && 
                methodDeclaration.Parent?.Parent is not null) return;

            var methodName = methodDeclaration.Identifier.Text;

            if (string.IsNullOrEmpty(methodName) || methodName == "Main") return;

            // Check PascalCase
            if (!IsPascalCase(methodName))
            {
                var diagnostic = Diagnostic.Create(MethodRule, methodDeclaration.Identifier.GetLocation(), methodName);
                context.ReportDiagnostic(diagnostic);
            }

            // Check for verb-object pattern for public methods
            if (methodDeclaration.Modifiers.Any(SyntaxKind.PublicKeyword))
            {
                if (!IsDescriptiveMethodName(methodName))
                {
                    var diagnostic = Diagnostic.Create(MethodRule, methodDeclaration.Identifier.GetLocation(), methodName);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private void AnalyzeProperty(SyntaxNodeAnalysisContext context)
        {
            var propertyDeclaration = (PropertyDeclarationSyntax)context.Node;
            var propertyName = propertyDeclaration.Identifier.Text;

            if (string.IsNullOrEmpty(propertyName)) return;

            // Check PascalCase
            if (!IsPascalCase(propertyName))
            {
                var diagnostic = Diagnostic.Create(PropertyRule, propertyDeclaration.Identifier.GetLocation(), propertyName);
                context.ReportDiagnostic(diagnostic);
            }

            // Check boolean property naming
            if (IsBooleanProperty(propertyDeclaration))
            {
                if (!IsValidBooleanPropertyName(propertyName))
                {
                    var diagnostic = Diagnostic.Create(PropertyRule, propertyDeclaration.Identifier.GetLocation(), propertyName);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private void AnalyzeField(SyntaxNodeAnalysisContext context)
        {
            var fieldDeclaration = (FieldDeclarationSyntax)context.Node;

            foreach (var variable in fieldDeclaration.Declaration.Variables)
            {
                var fieldName = variable.Identifier.Text;

                if (string.IsNullOrEmpty(fieldName)) continue;

                // Check if it's a constant
                if (fieldDeclaration.Modifiers.Any(SyntaxKind.ConstKeyword))
                {
                    if (!IsPascalCase(fieldName))
                    {
                        var diagnostic = Diagnostic.Create(ConstantsRule, variable.Identifier.GetLocation(), fieldName);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
                else
                {
                    // Check private field naming
                    var isPrivate = fieldDeclaration.Modifiers.Any(SyntaxKind.PrivateKeyword) ||
                                  (!fieldDeclaration.Modifiers.Any(SyntaxKind.PublicKeyword) && 
                                   !fieldDeclaration.Modifiers.Any(SyntaxKind.InternalKeyword));

                    if (isPrivate)
                    {
                        if (!IsValidPrivateFieldName(fieldName))
                        {
                            var diagnostic = Diagnostic.Create(PrivateFieldRule, variable.Identifier.GetLocation(), fieldName);
                            context.ReportDiagnostic(diagnostic);
                        }
                    }
                }
            }
        }

        private void AnalyzeEvent(SyntaxNodeAnalysisContext context)
        {
            var eventDeclaration = (EventDeclarationSyntax)context.Node;
            var eventName = eventDeclaration.Identifier.Text;

            if (string.IsNullOrEmpty(eventName)) return;

            // Check PascalCase
            if (!IsPascalCase(eventName))
            {
                var diagnostic = Diagnostic.Create(EventRule, eventDeclaration.Identifier.GetLocation(), eventName);
                context.ReportDiagnostic(diagnostic);
            }

            // Check event naming pattern (should indicate what happened)
            if (!IsValidEventName(eventName))
            {
                var diagnostic = Diagnostic.Create(EventRule, eventDeclaration.Identifier.GetLocation(), eventName);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private void AnalyzeEnum(SyntaxNodeAnalysisContext context)
        {
            var enumDeclaration = (EnumDeclarationSyntax)context.Node;
            var enumName = enumDeclaration.Identifier.Text;

            if (string.IsNullOrEmpty(enumName)) return;

            // Check PascalCase
            if (!IsPascalCase(enumName))
            {
                var diagnostic = Diagnostic.Create(EnumRule, enumDeclaration.Identifier.GetLocation(), enumName);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private void AnalyzeEnumMember(SyntaxNodeAnalysisContext context)
        {
            var enumMember = (EnumMemberDeclarationSyntax)context.Node;
            var memberName = enumMember.Identifier.Text;

            if (string.IsNullOrEmpty(memberName)) return;

            // Check PascalCase
            if (!IsPascalCase(memberName))
            {
                var diagnostic = Diagnostic.Create(EnumRule, enumMember.Identifier.GetLocation(), memberName);
                context.ReportDiagnostic(diagnostic);
            }
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

        private static bool IsValidBooleanPropertyName(string propertyName)
        {
            return propertyName.StartsWith("Is", StringComparison.Ordinal) ||
                   propertyName.StartsWith("Can", StringComparison.Ordinal) ||
                   propertyName.StartsWith("Has", StringComparison.Ordinal) ||
                   propertyName.StartsWith("Should", StringComparison.Ordinal) ||
                   propertyName.StartsWith("Will", StringComparison.Ordinal);
        }

        private static bool IsValidEventName(string eventName)
        {
            // Event names should indicate what happened (past tense or state)
            var eventNameLower = eventName.ToLowerInvariant();
            return eventNameLower.EndsWith("ed") || // Completed, Updated, etc.
                   eventNameLower.EndsWith("ing") || // Starting, Running, etc.
                   eventNameLower.Contains("alert") ||
                   eventNameLower.Contains("changed") ||
                   eventNameLower.Contains("clicked") ||
                   eventNameLower.Contains("selected");
        }

        private static bool IsBooleanProperty(PropertyDeclarationSyntax property)
        {
            // Check if the property type is boolean
            if (property.Type is PredefinedTypeSyntax predefinedType)
            {
                return predefinedType.Keyword.Kind() == SyntaxKind.BoolKeyword;
            }

            // Could be expanded to handle Nullable<bool> and other boolean-like types
            return false;
        }

        private static bool IsDescriptiveMethodName(string methodName)
        {
            // Check if method name starts with common verbs
            var commonVerbs = new[]
            {
                "Get", "Set", "Add", "Remove", "Create", "Delete", "Update",
                "Validate", "Calculate", "Process", "Execute", "Perform",
                "Begin", "End", "Start", "Stop", "Initialize", "Dispose",
                "Convert", "Transform", "Parse", "Format",
                "Connect", "Disconnect", "Evaluate", "Compile",
                "Render", "Display", "Show", "Hide"
            };

            return commonVerbs.Any(verb => methodName.StartsWith(verb, StringComparison.Ordinal));
        }

        private static bool IsInTiXLProject(SyntaxNodeAnalysisContext context)
        {
            // Check if the code is in a TiXL project
            var projectName = context.Compilation.Options.MetadataImportOptions.ToString();
            return projectName.Contains("TiXL", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsValidTiXLNamespace(string namespaceName)
        {
            // Check if namespace follows TiXL pattern
            if (!namespaceName.StartsWith("TiXL.", StringComparison.Ordinal))
                return false;

            var parts = namespaceName.Split('.');
            
            // Must have at least TiXL.Module format
            if (parts.Length < 3)
                return false;

            // Check that all parts use PascalCase
            return parts.Skip(1).All(part => !string.IsNullOrEmpty(part) && IsPascalCase(part));
        }

        private static void AnalyzeTiXLSpecificClassPatterns(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDeclaration, string className)
        {
            // Analyze TiXL-specific patterns
            var classNameLower = className.ToLowerInvariant();

            // Check for common TiXL patterns that should be enforced
            if (classNameLower.Contains("node") && !classNameLower.EndsWith("node"))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    TiXLModuleRule, 
                    classDeclaration.Identifier.GetLocation(), 
                    className));
            }

            if (classNameLower.Contains("operator") && !classNameLower.EndsWith("operator"))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    TiXLModuleRule, 
                    classDeclaration.Identifier.GetLocation(), 
                    className));
            }

            if (classNameLower.Contains("slot") && !classNameLower.EndsWith("slot"))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    TiXLModuleRule, 
                    classDeclaration.Identifier.GetLocation(), 
                    className));
            }
        }

        #endregion
    }
}