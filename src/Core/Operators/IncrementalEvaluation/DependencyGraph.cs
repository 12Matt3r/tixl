using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace T3.Core.Operators.IncrementalEvaluation
{
    /// <summary>
    /// Manages dependency relationships between nodes using topological analysis
    /// Provides efficient queries for dependency tracking and cycle detection
    /// </summary>
    public class DependencyGraph
    {
        #region Private Fields

        private readonly Dictionary<NodeId, HashSet<NodeId>> _dependencies = new();
        private readonly Dictionary<NodeId, HashSet<NodeId>> _dependents = new();
        private readonly object _lockObject = new object();

        #endregion

        #region Node Management

        /// <summary>
        /// Adds a node to the dependency graph
        /// </summary>
        public void AddNode(NodeId nodeId)
        {
            if (nodeId == null)
                throw new ArgumentNullException(nameof(nodeId));

            lock (_lockObject)
            {
                if (!_dependencies.ContainsKey(nodeId))
                {
                    _dependencies[nodeId] = new HashSet<NodeId>();
                }
                
                if (!_dependents.ContainsKey(nodeId))
                {
                    _dependents[nodeId] = new HashSet<NodeId>();
                }
            }
        }

        /// <summary>
        /// Removes a node and all its dependencies from the graph
        /// </summary>
        public void RemoveNode(NodeId nodeId)
        {
            if (nodeId == null)
                return;

            lock (_lockObject)
            {
                if (!_dependencies.ContainsKey(nodeId))
                    return;

                // Remove all incoming dependencies (nodes that this node depends on)
                foreach (var dependency in _dependencies[nodeId])
                {
                    if (_dependents.TryGetValue(dependency, out var dependents))
                    {
                        dependents.Remove(nodeId);
                    }
                }

                // Remove all outgoing dependencies (nodes that depend on this node)
                foreach (var dependent in _dependents[nodeId])
                {
                    if (_dependencies.TryGetValue(dependent, out var dependencies))
                    {
                        dependencies.Remove(nodeId);
                    }
                }

                _dependencies.Remove(nodeId);
                _dependents.Remove(nodeId);
            }
        }

        /// <summary>
        /// Adds a dependency relationship (from depends on to)
        /// </summary>
        public void AddDependency(NodeId from, NodeId to)
        {
            if (from == null || to == null)
                throw new ArgumentNullException();

            if (from.Equals(to))
                throw new InvalidOperationException("Node cannot depend on itself");

            lock (_lockObject)
            {
                // Ensure both nodes exist in the graph
                AddNode(from);
                AddNode(to);

                // Check for circular dependencies
                if (WouldCreateCircularDependency(from, to))
                {
                    throw new InvalidOperationException($"Adding dependency {from} -> {to} would create a circular dependency");
                }

                _dependencies[from].Add(to);
                _dependents[to].Add(from);
            }
        }

        /// <summary>
        /// Removes a dependency relationship
        /// </summary>
        public void RemoveDependency(NodeId from, NodeId to)
        {
            if (from == null || to == null)
                return;

            lock (_lockObject)
            {
                _dependencies[from]?.Remove(to);
                _dependents[to]?.Remove(from);
            }
        }

        #endregion

        #region Dependency Queries

        /// <summary>
        /// Gets all direct dependencies of a node
        /// </summary>
        public HashSet<NodeId> GetDependencies(NodeId nodeId)
        {
            if (nodeId == null)
                return new HashSet<NodeId>();

            lock (_lockObject)
            {
                return _dependencies.TryGetValue(nodeId, out var dependencies) 
                    ? new HashSet<NodeId>(dependencies) 
                    : new HashSet<NodeId>();
            }
        }

        /// <summary>
        /// Gets all nodes that directly depend on this node
        /// </summary>
        public HashSet<NodeId> GetDependents(NodeId nodeId)
        {
            if (nodeId == null)
                return new HashSet<NodeId>();

            lock (_lockObject)
            {
                return _dependents.TryGetValue(nodeId, out var dependents) 
                    ? new HashSet<NodeId>(dependents) 
                    : new HashSet<NodeId>();
            }
        }

        /// <summary>
        /// Gets all transitive dependencies of a node (recursive)
        /// </summary>
        public HashSet<NodeId> GetTransitiveDependencies(NodeId nodeId)
        {
            var visited = new HashSet<NodeId>();
            var result = new HashSet<NodeId>();
            var stack = new Stack<NodeId>();

            if (nodeId == null)
                return result;

            stack.Push(nodeId);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                
                if (visited.Contains(current))
                    continue;

                visited.Add(current);

                var dependencies = GetDependencies(current);
                foreach (var dependency in dependencies)
                {
                    if (!visited.Contains(dependency))
                    {
                        stack.Push(dependency);
                    }
                    
                    result.Add(dependency);
                }
            }

            return result;
        }

        /// <summary>
        /// Gets all transitive dependents of a node (recursive)
        /// </summary>
        public HashSet<NodeId> GetTransitiveDependents(NodeId nodeId)
        {
            var visited = new HashSet<NodeId>();
            var result = new HashSet<NodeId>();
            var stack = new Stack<NodeId>();

            if (nodeId == null)
                return result;

            stack.Push(nodeId);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                
                if (visited.Contains(current))
                    continue;

                visited.Add(current);

                var dependents = GetDependents(current);
                foreach (var dependent in dependents)
                {
                    if (!visited.Contains(dependent))
                    {
                        stack.Push(dependent);
                    }
                    
                    result.Add(dependent);
                }
            }

            return result;
        }

        #endregion

        #region Topological Analysis

        /// <summary>
        /// Gets the optimal evaluation order using Kahn's topological sort algorithm
        /// </summary>
        public List<NodeId> GetTopologicalOrder()
        {
            var timer = Stopwatch.StartNew();
            
            try
            {
                lock (_lockObject)
                {
                    var inDegree = new Dictionary<NodeId, int>();
                    var queue = new Queue<NodeId>();
                    var result = new List<NodeId>();

                    // Initialize in-degree counts
                    foreach (var nodeId in _dependencies.Keys)
                    {
                        inDegree[nodeId] = _dependents.TryGetValue(nodeId, out var dependents) ? dependents.Count : 0;
                    }

                    // Add nodes with no dependencies to queue
                    foreach (var kvp in inDegree.Where(k => k.Value == 0))
                    {
                        queue.Enqueue(kvp.Key);
                    }

                    // Kahn's algorithm
                    while (queue.Count > 0)
                    {
                        var current = queue.Dequeue();
                        result.Add(current);

                        // Reduce in-degree for nodes that depend on current
                        if (_dependencies.TryGetValue(current, out var dependencies))
                        {
                            foreach (var dependency in dependencies)
                            {
                                inDegree[dependency]--;
                                if (inDegree[dependency] == 0)
                                {
                                    queue.Enqueue(dependency);
                                }
                            }
                        }
                    }

                    // Check for cycles
                    if (result.Count != _dependencies.Count)
                    {
                        var remainingNodes = inDegree.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList();
                        var cycleInfo = FormatCycleInfo(remainingNodes);
                        throw new InvalidOperationException($"Circular dependency detected: {cycleInfo}");
                    }

                    return result;
                }
            }
            finally
            {
                timer.Stop();
            }
        }

        /// <summary>
        /// Gets topological order for a specific subset of nodes
        /// </summary>
        public List<NodeId> GetTopologicalOrderForNodes(HashSet<NodeId> nodes)
        {
            if (nodes == null || !nodes.Any())
                return new List<NodeId>();

            var timer = Stopwatch.StartNew();
            
            try
            {
                lock (_lockObject)
                {
                    var inDegree = new Dictionary<NodeId, int>();
                    var queue = new Queue<NodeId>();
                    var result = new List<NodeId>();
                    var filteredNodes = nodes.Where(n => _dependencies.ContainsKey(n)).ToHashSet();

                    // Initialize in-degree counts for filtered nodes
                    foreach (var nodeId in filteredNodes)
                    {
                        inDegree[nodeId] = _dependents.TryGetValue(nodeId, out var dependents) 
                            ? dependents.Count(n => filteredNodes.Contains(n)) 
                            : 0;
                    }

                    // Add nodes with no dependencies within the set to queue
                    foreach (var kvp in inDegree.Where(k => k.Value == 0))
                    {
                        queue.Enqueue(kvp.Key);
                    }

                    // Kahn's algorithm for subset
                    while (queue.Count > 0)
                    {
                        var current = queue.Dequeue();
                        result.Add(current);

                        // Reduce in-degree for filtered dependencies
                        if (_dependencies.TryGetValue(current, out var dependencies))
                        {
                            foreach (var dependency in dependencies.Where(d => filteredNodes.Contains(d)))
                            {
                                inDegree[dependency]--;
                                if (inDegree[dependency] == 0)
                                {
                                    queue.Enqueue(dependency);
                                }
                            }
                        }
                    }

                    return result;
                }
            }
            finally
            {
                timer.Stop();
            }
        }

        /// <summary>
        /// Detects if adding a dependency would create a circular dependency
        /// </summary>
        public bool WouldCreateCircularDependency(NodeId from, NodeId to)
        {
            if (from == null || to == null)
                return false;

            // Check if 'to' is already a dependency of 'from'
            var fromDependencies = GetTransitiveDependencies(from);
            return fromDependencies.Contains(to);
        }

        /// <summary>
        /// Detects all circular dependencies in the graph
        /// </summary>
        public CircularDependencyResult DetectCircularDependencies()
        {
            var result = new CircularDependencyResult();
            var visited = new HashSet<NodeId>();
            var recursionStack = new HashSet<NodeId>();
            var cycles = new List<List<NodeId>>();

            lock (_lockObject)
            {
                foreach (var nodeId in _dependencies.Keys)
                {
                    if (!visited.Contains(nodeId))
                    {
                        DetectCycleDFS(nodeId, visited, recursionStack, cycles, new List<NodeId>());
                    }
                }
            }

            result.HasCircularDependencies = cycles.Any();
            result.Cycles = cycles;
            result.CycleCount = cycles.Count;

            return result;
        }

        #endregion

        #region Private Helper Methods

        private void DetectCycleDFS(NodeId nodeId, HashSet<NodeId> visited, 
            HashSet<NodeId> recursionStack, List<List<NodeId>> cycles, List<NodeId> currentPath)
        {
            visited.Add(nodeId);
            recursionStack.Add(nodeId);
            currentPath.Add(nodeId);

            var dependencies = GetDependencies(nodeId);
            foreach (var dependency in dependencies)
            {
                if (!visited.Contains(dependency))
                {
                    DetectCycleDFS(dependency, visited, recursionStack, cycles, currentPath);
                }
                else if (recursionStack.Contains(dependency))
                {
                    // Found a cycle
                    var cycleStartIndex = currentPath.IndexOf(dependency);
                    var cycle = currentPath.Skip(cycleStartIndex).Append(dependency).ToList();
                    cycles.Add(cycle);
                }
            }

            recursionStack.Remove(nodeId);
            currentPath.RemoveAt(currentPath.Count - 1);
        }

        private string FormatCycleInfo(List<NodeId> remainingNodes)
        {
            if (!remainingNodes.Any())
                return "Unknown cycle";

            return $"Nodes involved in cycle: {string.Join(", ", remainingNodes.Take(10).Select(n => n.Id))}" +
                   (remainingNodes.Count > 10 ? "..." : "");
        }

        #endregion

        #region Statistics and Validation

        /// <summary>
        /// Gets statistics about the dependency graph
        /// </summary>
        public DependencyGraphStatistics GetStatistics()
        {
            lock (_lockObject)
            {
                var stats = new DependencyGraphStatistics
                {
                    TotalNodes = _dependencies.Count,
                    TotalDependencies = _dependencies.Values.Sum(d => d.Count),
                    AverageDependenciesPerNode = _dependencies.Count > 0 
                        ? (double)_dependencies.Values.Sum(d => d.Count) / _dependencies.Count 
                        : 0
                };

                // Find nodes with most dependencies
                if (_dependencies.Any())
                {
                    var maxDeps = _dependencies.Values.Max(d => d.Count);
                    stats.NodesWithMostDependencies = _dependencies.Where(kvp => kvp.Value.Count == maxDeps)
                        .Select(kvp => kvp.Key)
                        .ToList();
                    stats.MaxDependencies = maxDeps;
                }

                // Find nodes with most dependents
                if (_dependents.Any())
                {
                    var maxDependents = _dependents.Values.Max(d => d.Count);
                    stats.NodesWithMostDependents = _dependents.Where(kvp => kvp.Value.Count == maxDependents)
                        .Select(kvp => kvp.Key)
                        .ToList();
                    stats.MaxDependents = maxDependents;
                }

                // Check for cycles
                var circularResult = DetectCircularDependencies();
                stats.HasCircularDependencies = circularResult.HasCircularDependencies;
                stats.CycleCount = circularResult.CycleCount;

                return stats;
            }
        }

        /// <summary>
        /// Validates the integrity of the dependency graph
        /// </summary>
        public DependencyValidationResult ValidateIntegrity()
        {
            var result = new DependencyValidationResult();
            var issues = new List<string>();

            lock (_lockObject)
            {
                // Check for orphaned nodes
                foreach (var nodeId in _dependencies.Keys)
                {
                    if (!_dependents.ContainsKey(nodeId))
                    {
                        issues.Add($"Node '{nodeId}' exists in dependencies but not in dependents");
                    }
                }

                foreach (var nodeId in _dependents.Keys)
                {
                    if (!_dependencies.ContainsKey(nodeId))
                    {
                        issues.Add($"Node '{nodeId}' exists in dependents but not in dependencies");
                    }
                }

                // Check for self-dependencies
                foreach (var kvp in _dependencies)
                {
                    if (kvp.Value.Contains(kvp.Key))
                    {
                        issues.Add($"Node '{kvp.Key}' depends on itself");
                    }
                }

                // Check for consistency between dependencies and dependents
                foreach (var kvp in _dependencies)
                {
                    var nodeId = kvp.Key;
                    var dependencies = kvp.Value;

                    foreach (var dependency in dependencies)
                    {
                        if (!_dependents.TryGetValue(dependency, out var dependents) || 
                            !dependents.Contains(nodeId))
                        {
                            issues.Add($"Inconsistent relationship: {nodeId} depends on {dependency} but {dependency} doesn't have {nodeId} as dependent");
                        }
                    }
                }
            }

            result.IsValid = !issues.Any();
            result.Issues = issues;
            result.IssueCount = issues.Count;

            return result;
        }

        #endregion

        #region Supporting Classes

        /// <summary>
        /// Statistics about the dependency graph structure
        /// </summary>
        public class DependencyGraphStatistics
        {
            public int TotalNodes { get; set; }
            public int TotalDependencies { get; set; }
            public double AverageDependenciesPerNode { get; set; }
            public int MaxDependencies { get; set; }
            public int MaxDependents { get; set; }
            public List<NodeId> NodesWithMostDependencies { get; set; } = new();
            public List<NodeId> NodesWithMostDependents { get; set; } = new();
            public bool HasCircularDependencies { get; set; }
            public int CycleCount { get; set; }
        }

        /// <summary>
        /// Result of circular dependency detection
        /// </summary>
        public class CircularDependencyResult
        {
            public bool HasCircularDependencies { get; set; }
            public List<List<NodeId>> Cycles { get; set; } = new();
            public int CycleCount { get; set; }
        }

        /// <summary>
        /// Result of dependency graph validation
        /// </summary>
        public class DependencyValidationResult
        {
            public bool IsValid { get; set; }
            public List<string> Issues { get; set; } = new();
            public int IssueCount { get; set; }
        }

        #endregion
    }
}