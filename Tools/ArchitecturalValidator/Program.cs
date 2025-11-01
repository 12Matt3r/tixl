using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Locator;
using Microsoft.Build.Project;
using Microsoft.Build.Evaluation;

namespace TiXL.ArchitecturalValidator
{
    /// <summary>
    /// Validates architectural boundaries and dependencies in the TiXL codebase.
    /// Ensures modules follow the established architectural patterns and don't create forbidden dependencies.
    /// </summary>
    public class ArchitecturalValidator
    {
        private static readonly (string Module, string[] Forbidden)[] ForbiddenModuleDependencies = new[]
        {
            ("TiXL.Core", new[] { "TiXL.Operators", "TiXL.Gui", "TiXL.Gfx", "TiXL.Editor" }),
            ("TiXL.Operators", new[] { "TiXL.Gui", "TiXL.Gfx", "TiXL.Editor" }),
            ("TiXL.Gfx", new[] { "TiXL.Operators", "TiXL.Gui", "TiXL.Editor" }),
            ("TiXL.Gui", new[] { "TiXL.Gfx", "TiXL.Editor" }),
            ("TiXL.Editor", Array.Empty<string>()) // Editor can reference all modules
        };

        private static readonly string[] AllowedNamespaces = new[]
        {
            "TiXL.Core",
            "TiXL.Operators",
            "TiXL.Gfx",
            "TiXL.Gui",
            "TiXL.Editor",
            "System",
            "Microsoft",
            "Xunit",
            "NUnit",
            "Moq",
            "SharpDX",
            "Silk.NET"
        };

        private readonly List<string> _violations = new();

        public bool Validate(string solutionPath)
        {
            Console.WriteLine($"Starting architectural validation for: {solutionPath}");
            
            // Load solution
            var projectFiles = GetProjectFiles(solutionPath);
            Console.WriteLine($"Found {projectFiles.Count} project files");

            // Validate each project
            foreach (var projectFile in projectFiles)
            {
                ValidateProject(projectFile);
            }

            // Validate source code dependencies
            ValidateSourceDependencies(solutionPath);

            // Validate namespace usage
            ValidateNamespaceUsage(solutionPath);

            ReportViolations();
            return _violations.Count == 0;
        }

        private List<string> GetProjectFiles(string solutionPath)
        {
            var projects = new List<string>();
            
            foreach (var file in Directory.GetFiles(solutionPath, "*.csproj", SearchOption.AllDirectories))
            {
                // Skip test and tools projects
                if (IsTestOrToolProject(file))
                    continue;
                    
                projects.Add(file);
            }

            return projects;
        }

        private bool IsTestOrToolProject(string projectFile)
        {
            var fileName = Path.GetFileNameWithoutExtension(projectFile);
            return fileName.Contains("Tests") || 
                   fileName.Contains("Benchmark") ||
                   fileName.Contains("Profiler") ||
                   fileName.Contains("Tools");
        }

        private void ValidateProject(string projectFile)
        {
            Console.WriteLine($"Validating project: {Path.GetFileNameWithoutExtension(projectFile)}");
            
            try
            {
                // Register MSBuild
                MSBuildLocator.RegisterDefaults();
                
                var project = new Project(projectFile);
                var moduleName = GetModuleFromProjectPath(projectFile);
                var forbiddenDeps = GetForbiddenDependencies(moduleName);
                
                // Check project references
                foreach (var reference in project.AllEvaluatedItems.Where(i => i.ItemType == "ProjectReference"))
                {
                    var referencedProject = reference.EvaluatedInclude;
                    if (IsForbiddenReference(moduleName, referencedProject))
                    {
                        _violations.Add($"Project {moduleName} has forbidden project reference to {referencedProject}");
                    }
                }

                // Check package references
                foreach (var reference in project.AllEvaluatedItems.Where(i => i.ItemType == "PackageReference"))
                {
                    var packageName = reference.EvaluatedInclude;
                    if (IsForbiddenPackageReference(moduleName, packageName))
                    {
                        _violations.Add($"Project {moduleName} has forbidden package reference to {packageName}");
                    }
                }

                project.Dispose();
            }
            catch (Exception ex)
            {
                _violations.Add($"Error validating project {projectFile}: {ex.Message}");
            }
        }

        private string GetModuleFromProjectPath(string projectPath)
        {
            var directory = Path.GetDirectoryName(projectPath);
            var directoryName = Path.GetFileName(directory);
            
            return directoryName switch
            {
                "Core" => "TiXL.Core",
                "Operators" => "TiXL.Operators", 
                "Gfx" => "TiXL.Gfx",
                "Gui" => "TiXL.Gui",
                "Editor" => "TiXL.Editor",
                _ => $"Unknown.{directoryName}"
            };
        }

        private string[] GetForbiddenDependencies(string moduleName)
        {
            var moduleConstraint = ForbiddenModuleDependencies.FirstOrDefault(m => m.Module == moduleName);
            return moduleConstraint.Forbidden ?? Array.Empty<string>();
        }

        private bool IsForbiddenReference(string moduleName, string referencedProject)
        {
            var forbidden = GetForbiddenDependencies(moduleName);
            
            // Check if the referenced project matches any forbidden pattern
            return forbidden.Any(forbidden => 
                referencedProject.Contains(forbidden) || 
                Path.GetFileNameWithoutExtension(referencedProject) == forbidden);
        }

        private bool IsForbiddenPackageReference(string moduleName, string packageName)
        {
            // Define forbidden packages per module
            var forbiddenPackages = new Dictionary<string, string[]>
            {
                { "TiXL.Core", new[] { "ImGui.NET", "TiXL.Operators", "TiXL.Gui", "TiXL.Gfx", "TiXL.Editor" } },
                { "TiXL.Operators", new[] { "ImGui.NET", "TiXL.Gui", "TiXL.Gfx", "TiXL.Editor" } },
                { "TiXL.Gfx", new[] { "TiXL.Operators", "TiXL.Gui", "TiXL.Editor" } },
                { "TiXL.Gui", new[] { "TiXL.Gfx", "TiXL.Editor" } }
            };

            if (forbiddenPackages.TryGetValue(moduleName, out var packages))
            {
                return packages.Any(forbidden => packageName.Contains(forbidden));
            }

            return false;
        }

        private void ValidateSourceDependencies(string solutionPath)
        {
            Console.WriteLine("Validating source code dependencies...");
            
            var sourceFiles = Directory.GetFiles(solutionPath, "*.cs", SearchOption.AllDirectories);
            
            foreach (var sourceFile in sourceFiles)
            {
                if (IsTestOrToolFile(sourceFile))
                    continue;
                    
                ValidateSourceFile(sourceFile);
            }
        }

        private bool IsTestOrToolFile(string filePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            return fileName.Contains("Test") ||
                   fileName.Contains("Benchmark") ||
                   fileName.Contains("Mock") ||
                   filePath.Contains("Tests\\") ||
                   filePath.Contains("Tools\\") ||
                   filePath.Contains("Benchmarks\\");
        }

        private void ValidateSourceFile(string sourceFile)
        {
            try
            {
                var content = File.ReadAllText(sourceFile);
                var moduleName = GetModuleFromSourcePath(sourceFile);
                var forbidden = GetForbiddenDependencies(moduleName);

                // Check for forbidden using statements
                var usingStatements = GetUsingStatements(content);
                foreach (var usingStatement in usingStatements)
                {
                    if (IsForbiddenUsingStatement(moduleName, usingStatement))
                    {
                        _violations.Add($"Source file {sourceFile} contains forbidden using statement: {usingStatement}");
                    }
                }

                // Check for namespace declarations
                var namespaceDeclaration = GetNamespaceDeclaration(content);
                if (!string.IsNullOrEmpty(namespaceDeclaration) && !IsAllowedNamespace(namespaceDeclaration))
                {
                    _violations.Add($"Source file {sourceFile} uses disallowed namespace: {namespaceDeclaration}");
                }

                // Check for class references
                var classReferences = GetClassReferences(content);
                foreach (var classRef in classReferences)
                {
                    if (IsForbiddenClassReference(moduleName, classRef))
                    {
                        _violations.Add($"Source file {sourceFile} contains forbidden class reference: {classRef}");
                    }
                }
            }
            catch (Exception ex)
            {
                _violations.Add($"Error validating source file {sourceFile}: {ex.Message}");
            }
        }

        private string GetModuleFromSourcePath(string sourcePath)
        {
            var directories = sourcePath.Split(Path.DirectorySeparatorChar);
            var srcIndex = Array.IndexOf(directories, "src");
            
            if (srcIndex >= 0 && srcIndex + 1 < directories.Length)
            {
                return directories[srcIndex + 1] switch
                {
                    "Core" => "TiXL.Core",
                    "Operators" => "TiXL.Operators",
                    "Gfx" => "TiXL.Gfx",
                    "Gui" => "TiXL.Gui",
                    "Editor" => "TiXL.Editor",
                    _ => $"Unknown.{directories[srcIndex + 1]}"
                };
            }
            
            return "Unknown";
        }

        private List<string> GetUsingStatements(string content)
        {
            var usingStatements = new List<string>();
            var usingPattern = @"using\s+([^;]+);";
            
            var matches = Regex.Matches(content, usingPattern);
            foreach (Match match in matches)
            {
                if (match.Success && match.Groups.Count > 1)
                {
                    usingStatements.Add(match.Groups[1].Value.Trim());
                }
            }
            
            return usingStatements;
        }

        private string GetNamespaceDeclaration(string content)
        {
            var namespacePattern = @"namespace\s+([^\s{]+)";
            var match = Regex.Match(content, namespacePattern);
            
            return match.Success && match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : string.Empty;
        }

        private List<string> GetClassReferences(string content)
        {
            var classReferences = new List<string>();
            
            // Match class instantiations, base classes, and interface implementations
            var patterns = new[]
            {
                @":\s*([A-Za-z]\w*(?:\.[A-Za-z]\w*)*)", // Base classes and interfaces
                @"new\s+([A-Za-z]\w*(?:\.[A-Za-z]\w*)*)", // Constructor calls
                @"typeof\s*\(\s*([A-Za-z]\w*(?:\.[A-Za-z]\w*)*)" // Typeof expressions
            };
            
            foreach (var pattern in patterns)
            {
                var matches = Regex.Matches(content, pattern);
                foreach (Match match in matches)
                {
                    if (match.Success && match.Groups.Count > 1)
                    {
                        classReferences.Add(match.Groups[1].Value.Trim());
                    }
                }
            }
            
            return classReferences;
        }

        private bool IsForbiddenUsingStatement(string moduleName, string usingStatement)
        {
            var forbidden = GetForbiddenDependencies(moduleName);
            
            // Remove system namespaces for comparison
            var cleanUsing = usingStatement.Replace("global::", "");
            
            return forbidden.Any(forbiddenModule => 
                cleanUsing.StartsWith(forbiddenModule + ".") ||
                cleanUsing == forbiddenModule);
        }

        private bool IsAllowedNamespace(string namespaceDeclaration)
        {
            // Check if namespace is one of the allowed ones
            if (AllowedNamespaces.Contains(namespaceDeclaration))
                return true;
                
            // Check if it's a sub-namespace of an allowed one
            return AllowedNamespaces.Any(allowed => namespaceDeclaration.StartsWith(allowed + "."));
        }

        private bool IsForbiddenClassReference(string moduleName, string classReference)
        {
            var forbidden = GetForbiddenDependencies(moduleName);
            var cleanClassRef = classReference.Replace("global::", "");
            
            return forbidden.Any(forbiddenModule => 
                cleanClassRef.StartsWith(forbiddenModule + ".") ||
                cleanClassRef == forbiddenModule);
        }

        private void ValidateNamespaceUsage(string solutionPath)
        {
            Console.WriteLine("Validating namespace usage patterns...");
            
            var sourceFiles = Directory.GetFiles(solutionPath, "*.cs", SearchOption.AllDirectories);
            
            foreach (var sourceFile in sourceFiles)
            {
                if (IsTestOrToolFile(sourceFile))
                    continue;
                    
                ValidateNamespaceUsageInFile(sourceFile);
            }
        }

        private void ValidateNamespaceUsageInFile(string sourceFile)
        {
            try
            {
                var content = File.ReadAllText(sourceFile);
                var moduleName = GetModuleFromSourcePath(sourceFile);
                
                // Check for proper namespace structure
                var fileNamespace = GetNamespaceDeclaration(content);
                if (!string.IsNullOrEmpty(fileNamespace) && !fileNamespace.StartsWith(moduleName))
                {
                    _violations.Add($"Source file {sourceFile} has mismatched namespace. File is in {moduleName} but declares namespace {fileNamespace}");
                }
                
                // Check for using alias conflicts
                var usingAliases = GetUsingAliases(content);
                foreach (var alias in usingAliases)
                {
                    if (IsConflictingAlias(moduleName, alias.Key, alias.Value))
                    {
                        _violations.Add($"Source file {sourceFile} has potentially conflicting using alias {alias.Key} -> {alias.Value}");
                    }
                }
            }
            catch (Exception ex)
            {
                _violations.Add($"Error validating namespace usage in {sourceFile}: {ex.Message}");
            }
        }

        private Dictionary<string, string> GetUsingAliases(string content)
        {
            var aliases = new Dictionary<string, string>();
            var aliasPattern = @"using\s+(\w+)\s*=\s*([^;]+);";
            
            var matches = Regex.Matches(content, aliasPattern);
            foreach (Match match in matches)
            {
                if (match.Success && match.Groups.Count > 2)
                {
                    var alias = match.Groups[1].Value.Trim();
                    var target = match.Groups[2].Value.Trim();
                    aliases[alias] = target;
                }
            }
            
            return aliases;
        }

        private bool IsConflictingAlias(string moduleName, string alias, string target)
        {
            // Check if alias could cause confusion with module types
            var targetType = target.Split('.').LastOrDefault();
            
            if (!string.IsNullOrEmpty(targetType) && alias == targetType)
            {
                // This could be confusing - developer might think they're using module-specific type
                return true;
            }
            
            return false;
        }

        private void ReportViolations()
        {
            if (_violations.Count == 0)
            {
                Console.WriteLine("✅ No architectural violations found!");
                return;
            }

            Console.WriteLine($"❌ Found {_violations.Count} architectural violations:");
            Console.WriteLine();
            
            var groupedViolations = _violations.GroupBy(v => GetViolationCategory(v));
            
            foreach (var group in groupedViolations)
            {
                Console.WriteLine($"📋 {group.Key}:");
                foreach (var violation in group)
                {
                    Console.WriteLine($"   • {violation}");
                }
                Console.WriteLine();
            }
        }

        private string GetViolationCategory(string violation)
        {
            return violation.ToLowerInvariant() switch
            {
                var v when v.Contains("project reference") => "Project Reference Violations",
                var v when v.Contains("package reference") => "Package Reference Violations",
                var v when v.Contains("using statement") => "Using Statement Violations",
                var v when v.Contains("namespace") => "Namespace Violations",
                var v when v.Contains("class reference") => "Class Reference Violations",
                _ => "Other Violations"
            };
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("TiXL Architectural Validator");
                Console.WriteLine("Usage: ArchitecturalValidator <solution-path>");
                Console.WriteLine();
                Console.WriteLine("This tool validates that the TiXL codebase follows established architectural boundaries.");
                return;
            }

            var solutionPath = args[0];
            
            if (!Directory.Exists(solutionPath))
            {
                Console.WriteLine($"Error: Solution path '{solutionPath}' does not exist.");
                Environment.Exit(1);
            }

            var validator = new ArchitecturalValidator();
            var isValid = validator.Validate(solutionPath);
            
            if (!isValid)
            {
                Environment.Exit(1);
            }
        }
    }
}