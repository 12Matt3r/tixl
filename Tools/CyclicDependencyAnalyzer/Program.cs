using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace TiXL.Tools.CyclicDependencyAnalyzer
{
    public class DependencyGraph
    {
        private readonly Dictionary<string, HashSet<string>> _dependencies = new();
        private readonly Dictionary<string, ProjectInfo> _projects = new();

        public void AddProject(string name, string filePath, string[] references)
        {
            _projects[name] = new ProjectInfo { Name = name, FilePath = filePath };
            if (!_dependencies.ContainsKey(name))
                _dependencies[name] = new HashSet<string>();
            
            foreach (var reference in references)
            {
                _dependencies[name].Add(reference);
            }
        }

        public List<Cycle> FindCycles()
        {
            var cycles = new List<Cycle>();
            var visited = new HashSet<string>();
            var recursionStack = new HashSet<string>();

            foreach (var project in _dependencies.Keys)
            {
                if (!visited.Contains(project))
                {
                    FindCyclesRecursive(project, visited, recursionStack, new List<string>(), cycles);
                }
            }

            return cycles;
        }

        private void FindCyclesRecursive(string node, HashSet<string> visited, HashSet<string> recursionStack, 
            List<string> currentPath, List<Cycle> cycles)
        {
            visited.Add(node);
            recursionStack.Add(node);
            currentPath.Add(node);

            if (_dependencies.ContainsKey(node))
            {
                foreach (var neighbor in _dependencies[node])
                {
                    if (recursionStack.Contains(neighbor))
                    {
                        // Found a cycle
                        var cycleStart = currentPath.IndexOf(neighbor);
                        if (cycleStart >= 0)
                        {
                            var cyclePath = currentPath.Skip(cycleStart).Append(neighbor).ToList();
                            cycles.Add(new Cycle
                            {
                                Projects = cyclePath.ToArray(),
                                Length = cyclePath.Count - 1,
                                Severity = DetermineSeverity(cyclePath.Count - 1)
                            });
                        }
                    }
                    else if (!visited.Contains(neighbor))
                    {
                        FindCyclesRecursive(neighbor, visited, recursionStack, currentPath, cycles);
                    }
                }
            }

            recursionStack.Remove(node);
            currentPath.RemoveAt(currentPath.Count - 1);
        }

        private CycleSeverity DetermineSeverity(int cycleLength)
        {
            return cycleLength switch
            {
                <= 2 => CycleSeverity.Critical,
                <= 4 => CycleSeverity.High,
                <= 6 => CycleSeverity.Medium,
                _ => CycleSeverity.Low
            };
        }

        public string GenerateReport(List<Cycle> cycles)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# TiXL Cyclic Dependency Analysis Report");
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            if (!cycles.Any())
            {
                sb.AppendLine("✅ **No cyclic dependencies detected!**");
                sb.AppendLine("The dependency graph is healthy and follows proper architectural boundaries.");
                return sb.ToString();
            }

            sb.AppendLine($"⚠️  **Found {cycles.Count} cyclic dependency issue(s):**");
            sb.AppendLine();

            var criticalCycles = cycles.Where(c => c.Severity == CycleSeverity.Critical).ToList();
            var highCycles = cycles.Where(c => c.Severity == CycleSeverity.High).ToList();
            var mediumCycles = cycles.Where(c => c.Severity == CycleSeverity.Medium).ToList();
            var lowCycles = cycles.Where(c => c.Severity == CycleSeverity.Low).ToList();

            if (criticalCycles.Any())
            {
                sb.AppendLine("## 🔴 Critical Cycles (Immediate Action Required)");
                foreach (var cycle in criticalCycles)
                {
                    sb.AppendLine($"### Cycle Length: {cycle.Length}");
                    sb.AppendLine($"Projects: {string.Join(" → ", cycle.Projects)}");
                    sb.AppendLine();
                }
            }

            if (highCycles.Any())
            {
                sb.AppendLine("## 🟠 High Priority Cycles");
                foreach (var cycle in highCycles)
                {
                    sb.AppendLine($"### Cycle Length: {cycle.Length}");
                    sb.AppendLine($"Projects: {string.Join(" → ", cycle.Projects)}");
                    sb.AppendLine();
                }
            }

            if (mediumCycles.Any())
            {
                sb.AppendLine("## 🟡 Medium Priority Cycles");
                foreach (var cycle in mediumCycles)
                {
                    sb.AppendLine($"### Cycle Length: {cycle.Length}");
                    sb.AppendLine($"Projects: {string.Join(" → ", cycle.Projects)}");
                    sb.AppendLine();
                }
            }

            if (lowCycles.Any())
            {
                sb.AppendLine("## 🟢 Low Priority Cycles");
                foreach (var cycle in lowCycles)
                {
                    sb.AppendLine($"### Cycle Length: {cycle.Length}");
                    sb.AppendLine($"Projects: {string.Join(" → ", cycle.Projects)}");
                    sb.AppendLine();
                }
            }

            sb.AppendLine("## Recommendations");
            sb.AppendLine("1. **Review the dependency direction**: Consider if the dependencies can be inverted");
            sb.AppendLine("2. **Extract common interfaces**: Move shared interfaces to a separate project");
            sb.AppendLine("3. **Apply inversion of control**: Use dependency injection instead of direct references");
            sb.AppendLine("4. **Refactor shared functionality**: Move common code to a lower-level module");
            sb.AppendLine("5. **Use messaging/event patterns**: Replace direct calls with event-based communication");

            return sb.ToString();
        }

        public void SaveToJson(string filePath, List<Cycle> cycles)
        {
            var report = new
            {
                GeneratedAt = DateTime.Now,
                TotalCycles = cycles.Count,
                Projects = _projects.Count,
                Cycles = cycles.Select(c => new
                {
                    Projects = c.Projects,
                    Length = c.Length,
                    Severity = c.Severity.ToString(),
                    Message = $"Cycle of length {c.Length} involving {string.Join(", ", c.Projects)}"
                }).ToList()
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            File.WriteAllText(filePath, JsonSerializer.Serialize(report, options));
        }
    }

    public class ProjectInfo
    {
        public string Name { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
    }

    public class Cycle
    {
        public string[] Projects { get; set; } = Array.Empty<string>();
        public int Length { get; set; }
        public CycleSeverity Severity { get; set; }
    }

    public enum CycleSeverity
    {
        Critical,
        High,
        Medium,
        Low
    }

    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            try
            {
                var solutionPath = args.Length > 0 ? args[0] : "TiXL.sln";
                var outputPath = args.Length > 1 ? args[1] : "dependency-analysis.json";
                var reportPath = args.Length > 2 ? args[2] : "dependency-report.md";

                if (!File.Exists(solutionPath))
                {
                    Console.Error.WriteLine($"Solution file not found: {solutionPath}");
                    return 1;
                }

                Console.WriteLine($"Analyzing cyclic dependencies in: {solutionPath}");

                // Register MSBuild
                MSBuildLocator.RegisterDefaults();

                using var workspace = MSBuildWorkspace.Create();
                var solution = await workspace.OpenSolutionAsync(solutionPath);

                var dependencyGraph = new DependencyGraph();

                // Build dependency graph
                foreach (var project in solution.Projects)
                {
                    Console.WriteLine($"Processing: {project.Name}");

                    var references = project.ProjectReferences
                        .Select(pr => solution.Projects.FirstOrDefault(p => p.FilePath == pr.ProjectFilePath)?.Name ?? "Unknown")
                        .ToArray();

                    dependencyGraph.AddProject(project.Name, project.FilePath, references);
                }

                // Find cycles
                var cycles = dependencyGraph.FindCycles();

                // Generate reports
                dependencyGraph.SaveToJson(outputPath, cycles);
                var report = dependencyGraph.GenerateReport(cycles);
                File.WriteAllText(reportPath, report);

                Console.WriteLine($"\nAnalysis complete!");
                Console.WriteLine($"JSON report: {outputPath}");
                Console.WriteLine($"Markdown report: {reportPath}");

                if (cycles.Any())
                {
                    Console.WriteLine($"\n⚠️  Found {cycles.Count} cyclic dependency issue(s)");
                    
                    var criticalCount = cycles.Count(c => c.Severity == CycleSeverity.Critical);
                    if (criticalCount > 0)
                    {
                        Console.WriteLine($"🔴 {criticalCount} CRITICAL cycles require immediate attention");
                    }

                    return criticalCount > 0 ? 1 : 0;
                }
                else
                {
                    Console.WriteLine("✅ No cyclic dependencies detected!");
                    return 0;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error analyzing dependencies: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                return 1;
            }
        }
    }
}
