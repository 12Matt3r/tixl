using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;

namespace TiXL.NamingConventions.Analyzers.CodeFixes
{
    /// <summary>
    /// Code fix provider for TiXL naming conventions
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TiXLNamingConventionsCodeFixProvider))]
    [Shared]
    public class TiXLNamingConventionsCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => 
            ImmutableArray.Create(
                "TiXL012001", "TiXL012002", "TiXL012003", "TiXL012004",
                "TiXL012005", "TiXL012006", "TiXL012007", "TiXL012008", 
                "TiXL012009", "TiXL012010");

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            foreach (var diagnostic in context.Diagnostics)
            {
                var diagnosticSpan = diagnostic.Location.SourceSpan;

                // Find the syntax node that triggered the diagnostic
                var node = root.FindNode(diagnosticSpan);

                // Register a code action that will invoke the fix
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: $"Fix {GetTitle(diagnostic.Id)}",
                        createChangedDocument: c => FixNamingViolation(context.Document, node, c),
                        equivalenceKey: $"Fix_{diagnostic.Id}"),
                    diagnostic);
            }
        }

        private static async Task<Document> FixNamingViolation(Document document, SyntaxNode node, CancellationToken cancellationToken)
        {
            var documentId = document.Id;
            var solution = document.Project.Solution;

            SyntaxNode newNode = node;

            switch (node.Kind())
            {
                case SyntaxKind.NamespaceDeclaration:
                    newNode = FixNamespaceName((NamespaceDeclarationSyntax)node);
                    break;

                case SyntaxKind.ClassDeclaration:
                    newNode = await FixClassNameAsync((ClassDeclarationSyntax)node, solution, cancellationToken);
                    break;

                case SyntaxKind.InterfaceDeclaration:
                    newNode = await FixInterfaceNameAsync((InterfaceDeclarationSyntax)node, solution, cancellationToken);
                    break;

                case SyntaxKind.MethodDeclaration:
                    newNode = await FixMethodNameAsync((MethodDeclarationSyntax)node, solution, cancellationToken);
                    break;

                case SyntaxKind.PropertyDeclaration:
                    newNode = await FixPropertyNameAsync((PropertyDeclarationSyntax)node, solution, cancellationToken);
                    break;

                case SyntaxKind.FieldDeclaration:
                    newNode = await FixFieldNameAsync((FieldDeclarationSyntax)node, solution, cancellationToken);
                    break;

                case SyntaxKind.EventDeclaration:
                    newNode = await FixEventNameAsync((EventDeclarationSyntax)node, solution, cancellationToken);
                    break;

                case SyntaxKind.EnumDeclaration:
                    newNode = await FixEnumNameAsync((EnumDeclarationSyntax)node, solution, cancellationToken);
                    break;

                case SyntaxKind.EnumMemberDeclaration:
                    newNode = await FixEnumMemberNameAsync((EnumMemberDeclarationSyntax)node, solution, cancellationToken);
                    break;
            }

            // Replace the old node with the new one
            var oldRoot = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = oldRoot.ReplaceNode(node, newNode);

            return document.WithSyntaxRoot(newRoot);
        }

        private static SyntaxNode FixNamespaceName(NamespaceDeclarationSyntax namespaceDeclaration)
        {
            var oldName = namespaceDeclaration.Name.ToString();
            var newName = ToPascalCaseNamespace(oldName);

            if (oldName == newName) return namespaceDeclaration;

            var newNamespace = namespaceDeclaration.Name.Update(
                SyntaxFactory.ParseName(newName).WithTriviaFrom(namespaceDeclaration.Name));

            return newNamespace;
        }

        private static async Task<SyntaxNode> FixClassNameAsync(ClassDeclarationSyntax classDeclaration, Solution solution, CancellationToken cancellationToken)
        {
            var oldName = classDeclaration.Identifier.Text;
            var newName = ToPascalCase(oldName);

            if (oldName == newName) return classDeclaration;

            // Use Roslyn's rename service to handle all references
            var document = solution.GetDocument(classDeclaration.SyntaxTree);
            if (document != null)
            {
                var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
                var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken);

                if (classSymbol != null)
                {
                    var newSolution = await Renamer.RenameSymbolAsync(
                        solution, classSymbol, newName, cancellationToken);

                    // Get the updated document
                    return newSolution.GetDocument(document.Id).GetSyntaxRootAsync(cancellationToken)
                        .ContinueWith(t => t.Result.FindNode(classDeclaration.Span), cancellationToken).Result;
                }
            }

            // Fallback to simple identifier replacement
            return classDeclaration.WithIdentifier(
                SyntaxFactory.Identifier(newName).WithTriviaFrom(classDeclaration.Identifier));
        }

        private static async Task<SyntaxNode> FixInterfaceNameAsync(InterfaceDeclarationSyntax interfaceDeclaration, Solution solution, CancellationToken cancellationToken)
        {
            var oldName = interfaceDeclaration.Identifier.Text;
            var newName = EnsureIPrefix(ToPascalCase(oldName));

            if (oldName == newName) return interfaceDeclaration;

            var document = solution.GetDocument(interfaceDeclaration.SyntaxTree);
            if (document != null)
            {
                var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
                var interfaceSymbol = semanticModel.GetDeclaredSymbol(interfaceDeclaration, cancellationToken);

                if (interfaceSymbol != null)
                {
                    var newSolution = await Renamer.RenameSymbolAsync(
                        solution, interfaceSymbol, newName, cancellationToken);

                    return newSolution.GetDocument(document.Id).GetSyntaxRootAsync(cancellationToken)
                        .ContinueWith(t => t.Result.FindNode(interfaceDeclaration.Span), cancellationToken).Result;
                }
            }

            return interfaceDeclaration.WithIdentifier(
                SyntaxFactory.Identifier(newName).WithTriviaFrom(interfaceDeclaration.Identifier));
        }

        private static async Task<SyntaxNode> FixMethodNameAsync(MethodDeclarationSyntax methodDeclaration, Solution solution, CancellationToken cancellationToken)
        {
            var oldName = methodDeclaration.Identifier.Text;
            var newName = ToPascalCase(oldName);

            if (oldName == newName) return methodDeclaration;

            var document = solution.GetDocument(methodDeclaration.SyntaxTree);
            if (document != null)
            {
                var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
                var methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration, cancellationToken);

                if (methodSymbol != null)
                {
                    var newSolution = await Renamer.RenameSymbolAsync(
                        solution, methodSymbol, newName, cancellationToken);

                    return newSolution.GetDocument(document.Id).GetSyntaxRootAsync(cancellationToken)
                        .ContinueWith(t => t.Result.FindNode(methodDeclaration.Span), cancellationToken).Result;
                }
            }

            return methodDeclaration.WithIdentifier(
                SyntaxFactory.Identifier(newName).WithTriviaFrom(methodDeclaration.Identifier));
        }

        private static async Task<SyntaxNode> FixPropertyNameAsync(PropertyDeclarationSyntax propertyDeclaration, Solution solution, CancellationToken cancellationToken)
        {
            var oldName = propertyDeclaration.Identifier.Text;
            var newName = ToPascalCase(oldName);

            if (oldName == newName) return propertyDeclaration;

            var document = solution.GetDocument(propertyDeclaration.SyntaxTree);
            if (document != null)
            {
                var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
                var propertySymbol = semanticModel.GetDeclaredSymbol(propertyDeclaration, cancellationToken);

                if (propertySymbol != null)
                {
                    var newSolution = await Renamer.RenameSymbolAsync(
                        solution, propertySymbol, newName, cancellationToken);

                    return newSolution.GetDocument(document.Id).GetSyntaxRootAsync(cancellationToken)
                        .ContinueWith(t => t.Result.FindNode(propertyDeclaration.Span), cancellationToken).Result;
                }
            }

            return propertyDeclaration.WithIdentifier(
                SyntaxFactory.Identifier(newName).WithTriviaFrom(propertyDeclaration.Identifier));
        }

        private static async Task<SyntaxNode> FixFieldNameAsync(FieldDeclarationSyntax fieldDeclaration, Solution solution, CancellationToken cancellationToken)
        {
            var newFields = new List<VariableDeclarationSyntax>();

            foreach (var variable in fieldDeclaration.Declaration.Variables)
            {
                var oldName = variable.Identifier.Text;
                var newName = FixFieldName(oldName, fieldDeclaration);

                if (oldName != newName)
                {
                    var newVariable = variable.WithIdentifier(
                        SyntaxFactory.Identifier(newName).WithTriviaFrom(variable.Identifier));
                    newFields.Add(fieldDeclaration.Declaration.WithVariables(
                        fieldDeclaration.Declaration.Variables.Replace(variable, newVariable)));
                }
            }

            return newFields.Count > 0 ? 
                fieldDeclaration.WithDeclaration(newFields[0]) : 
                fieldDeclaration;
        }

        private static async Task<SyntaxNode> FixEventNameAsync(EventDeclarationSyntax eventDeclaration, Solution solution, CancellationToken cancellationToken)
        {
            var oldName = eventDeclaration.Identifier.Text;
            var newName = ToPascalCase(oldName);

            if (oldName == newName) return eventDeclaration;

            var document = solution.GetDocument(eventDeclaration.SyntaxTree);
            if (document != null)
            {
                var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
                var eventSymbol = semanticModel.GetDeclaredSymbol(eventDeclaration, cancellationToken);

                if (eventSymbol != null)
                {
                    var newSolution = await Renamer.RenameSymbolAsync(
                        solution, eventSymbol, newName, cancellationToken);

                    return newSolution.GetDocument(document.Id).GetSyntaxRootAsync(cancellationToken)
                        .ContinueWith(t => t.Result.FindNode(eventDeclaration.Span), cancellationToken).Result;
                }
            }

            return eventDeclaration.WithIdentifier(
                SyntaxFactory.Identifier(newName).WithTriviaFrom(eventDeclaration.Identifier));
        }

        private static async Task<SyntaxNode> FixEnumNameAsync(EnumDeclarationSyntax enumDeclaration, Solution solution, CancellationToken cancellationToken)
        {
            var oldName = enumDeclaration.Identifier.Text;
            var newName = ToPascalCase(oldName);

            if (oldName == newName) return enumDeclaration;

            var document = solution.GetDocument(enumDeclaration.SyntaxTree);
            if (document != null)
            {
                var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
                var enumSymbol = semanticModel.GetDeclaredSymbol(enumDeclaration, cancellationToken);

                if (enumSymbol != null)
                {
                    var newSolution = await Renamer.RenameSymbolAsync(
                        solution, enumSymbol, newName, cancellationToken);

                    return newSolution.GetDocument(document.Id).GetSyntaxRootAsync(cancellationToken)
                        .ContinueWith(t => t.Result.FindNode(enumDeclaration.Span), cancellationToken).Result;
                }
            }

            return enumDeclaration.WithIdentifier(
                SyntaxFactory.Identifier(newName).WithTriviaFrom(enumDeclaration.Identifier));
        }

        private static async Task<SyntaxNode> FixEnumMemberNameAsync(EnumMemberDeclarationSyntax enumMemberDeclaration, Solution solution, CancellationToken cancellationToken)
        {
            var oldName = enumMemberDeclaration.Identifier.Text;
            var newName = ToPascalCase(oldName);

            if (oldName == newName) return enumMemberDeclaration;

            var document = solution.GetDocument(enumMemberDeclaration.SyntaxTree);
            if (document != null)
            {
                var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
                var enumMemberSymbol = semanticModel.GetDeclaredSymbol(enumMemberDeclaration, cancellationToken);

                if (enumMemberSymbol != null)
                {
                    var newSolution = await Renamer.RenameSymbolAsync(
                        solution, enumMemberSymbol, newName, cancellationToken);

                    return newSolution.GetDocument(document.Id).GetSyntaxRootAsync(cancellationToken)
                        .ContinueWith(t => t.Result.FindNode(enumMemberDeclaration.Span), cancellationToken).Result;
                }
            }

            return enumMemberDeclaration.WithIdentifier(
                SyntaxFactory.Identifier(newName).WithTriviaFrom(enumMemberDeclaration.Identifier));
        }

        #region Helper Methods for Naming Conversion

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

        private static string FixFieldName(string fieldName, FieldDeclarationSyntax fieldDeclaration)
        {
            var isConst = fieldDeclaration.Modifiers.Any(SyntaxKind.ConstKeyword);

            if (isConst)
            {
                // Constants should be PascalCase
                return ToPascalCase(fieldName);
            }
            else
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
        }

        private static string GetTitle(string diagnosticId)
        {
            return diagnosticId switch
            {
                "TiXL012001" => "Namespace Naming",
                "TiXL012002" => "Class Naming", 
                "TiXL012003" => "Interface Naming",
                "TiXL012004" => "Method Naming",
                "TiXL012005" => "Property Naming",
                "TiXL012006" => "Private Field Naming",
                "TiXL012007" => "Event Naming",
                "TiXL012008" => "Enum Naming",
                "TiXL012009" => "Constants Naming",
                "TiXL012010" => "TiXL Module Naming",
                _ => "Naming Convention"
            };
        }

        #endregion
    }
}