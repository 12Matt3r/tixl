using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace T3.Core.NodeGraph
{
    /// <summary>
    /// Enhanced Node Dependency Tracker for TiXL Node System
    /// Monitors node dependencies, input changes, and provides efficient 
    /// dependency analysis with topological sorting capabilities
    /// </summary>
    public class NodeDependencyTracker : IDisposable
    {
        #region Private Fields

        private readonly Dictionary<NodeId, HashSet<NodeId>> _dependencies = new();
        private readonly Dictionary<NodeId, HashSet<NodeId>> _dependents = new();
        private readonly Dictionary<NodeId, DirtyState> _dirtyStates;
        private readonly Dictionary<NodeId, NodeMetadata> _nodeMetadata;
        private readonly ReaderWriterLockSlim _lockObject = new();
        private readonly DependencyMetrics _metrics = new();

        // Performance optimization
        private readonly Dictionary<string, TopologicalOrderCache> _topologicalCache = new();
        private readonly TimeSpan _cacheTimeout = TimeSpan.FromSeconds(30);

        private bool _disposed = false;

        #endregion

        #region Constructor

        public NodeDependencyTracker(int expectedNodeCount = 1000)
        {
            _dirtyStates = new Dictionary<NodeId, DirtyState>();
            _nodeMetadata = new Dictionary<NodeId, NodeMetadata>();

            // Pre-allocate collections for performance
            for (int i = 0; i < Math.Min(expectedNodeCount / 10, 100); i++)
            {
                _dependencies.Add(new NodeId($"prealloc_{i}"), new HashSet<NodeId>());
                _dependents.Add(new NodeId($"prealloc_{i}"), new HashSet<NodeId>());
            }
        }

        #endregion

        #region Node Registration

        /// <summary>
        /// Registers a node for dependency tracking
        /// </summary>
        public void RegisterNode(NodeId nodeId)
        {
            if (nodeId == null)
                throw new ArgumentNullException(nameof(nodeId));

            using (_lockObject.WriteLock())
            {
                // Initialize node in dependency tracking
                if (!_dependencies.ContainsKey(nodeId))
                {
                    _dependencies[nodeId] = new HashSet<NodeId>();
                }

                if (!_dependents.ContainsKey(nodeId))
                {
                    _dependents[nodeId] = new HashSet<NodeId>();
                }

                // Initialize dirty state
                if (!_dirtyStates.ContainsKey(nodeId))
                {
                    _dirtyStates[nodeId] = new DirtyState
                    {
                        IsDirty = true, // New nodes start as dirty
                        DirtyLevel = DirtyLevel.Normal,
                        LastModified = DateTime.UtcNow,
                        LastEvaluated = DateTime.MinValue
                    };
                }

                // Initialize metadata
                if (!_nodeMetadata.ContainsKey(nodeId))
                {
                    _nodeMetadata[nodeId] = new NodeMetadata
                    {
                        RegisteredAt = DateTime.UtcNow,
                        InputSignatureHash = 0,
                        DependencyCount = 0,
                        DependentCount = 0
                    };
                }

                _metrics.NodeRegistered();
                InvalidateCache();
            }
        }

        /// <summary>
        /// Unregisters a node from dependency tracking
        /// </summary>
        public void UnregisterNode(NodeId nodeId)
        {
            if (nodeId == null)
                return;

            using (_lockObject.WriteLock())
            {
                if (!_dependencies.ContainsKey(nodeId))
                    return;

                // Remove all incoming dependencies
                var dependencies = _dependencies[nodeId].ToList();
                foreach (var dependency in dependencies)
                {
                    _dependents[dependency]?.Remove(nodeId);
                    UpdateNodeMetadata(dependency, -1, 0); // -1 dependent
                }

                // Remove all outgoing dependencies (nodes that depend on this node)
                var dependents = _dependents[nodeId].ToList();
                foreach (var dependent in dependents)
                {
                    _dependencies[dependent]?.Remove(nodeId);
                    UpdateNodeMetadata(dependent, 0, -1); // -1 dependency
                }

                // Clean up tracking data
                _dependencies.Remove(nodeId);
                _dependents.Remove(nodeId);
                _dirtyStates.Remove(nodeId);
                _nodeMetadata.Remove(nodeId);

                _metrics.NodeUnregistered();
                InvalidateCache();
            }
        }

        #endregion

        #region Dependency Management

        /// <summary>
        /// Adds a dependency relationship between nodes
        /// </summary>
        public void AddDependency(NodeId from, NodeId to)
        {
            if (from == null || to == null)
                throw new ArgumentNullException();

            if (from.Equals(to))
                throw new InvalidOperationException("Node cannot depend on itself");

            using (_lockObject.WriteLock())
            {
                // Ensure both nodes exist
                RegisterNode(from);
                RegisterNode(to);

                // Check for circular dependencies
                if (WouldCreateCircularDependency(from, to))
                {
                    throw new InvalidOperationException($"Adding dependency {from} -> {to} would create circular dependency");
                }

                // Add the dependency
                if (_dependencies[from].Add(to))
                {
                    _dependents[to].Add(from);

                    // Update metadata
                    UpdateNodeMetadata(from, 0, 1); // +1 dependency
                    UpdateNodeMetadata(to, 1, 0);   // +1 dependent

                    _metrics.DependencyAdded();
                    InvalidateCache();
                }
            }
        }

        /// <summary>
        /// Removes a dependency relationship
        /// </summary>
        public void RemoveDependency(NodeId from, NodeId to)
        {
            if (from == null || to == null)
                return;

            using (_lockObject.WriteLock())
            {
                if (_dependencies[from]?.Remove(to) == true)
                {
                    _dependents[to]?.Remove(from);

                    // Update metadata
                    UpdateNodeMetadata(from, 0, -1); // -1 dependency
                    UpdateNodeMetadata(to, -1, 0);   // -1 dependent

                    _metrics.DependencyRemoved();
                    InvalidateCache();
                }
            }
        }

        /// <summary>
        /// Batch adds multiple dependencies efficiently
        /// </summary>
        public void AddDependencies(NodeId from, IEnumerable<NodeId> to)
        {
            if (from == null || to == null)
                return;

            using (_lockObject.WriteLock())
            {
                RegisterNode(from);
                
                foreach (var dependency in to)
                {
                    RegisterNode(dependency);
                    
                    if (!_dependencies[from].Contains(dependency) && !from.Equals(dependency))
                    {
                        if (!WouldCreateCircularDependency(from, dependency))
                        {
                            _dependencies[from].Add(dependency);
                            _dependents[dependency].Add(from);
                            
                            UpdateNodeMetadata(from, 0, 1);
                            UpdateNodeMetadata(dependency, 1, 0);
                            _metrics.DependencyAdded();
                        }
                    }
                }
                
                InvalidateCache();
            }
        }

        #endregion

        #region Dependency Queries

        /// <summary>
        /// Gets all direct dependencies of a node
        /// </summary>
        public HashSet<NodeId> GetDirectDependencies(NodeId nodeId)
        {
            if (nodeId == null)
                return new HashSet<NodeId>();

            using (_lockObject.ReadLock())
            {
                return _dependencies.TryGetValue(nodeId, out var deps) 
                    ? new HashSet<NodeId>(deps) 
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

            using (_lockObject.ReadLock())
            {
                return _dependents.TryGetValue(nodeId, out var dependents) 
                    ? new HashSet<NodeId>(dependents) 
                    : new HashSet<NodeId>();
            }
        }

        /// <summary>
        /// Gets all transitive dependencies of a node (recursive)
        /// </summary>
        public HashSet<NodeId> GetAllDependencies(NodeId nodeId)
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

                var dependencies = GetDirectDependencies(current);
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
        public HashSet<NodeId> GetAllDependents(NodeId nodeId)
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

        /// <summary>
        /// Gets the dependency depth (longest chain) from this node
        /// </summary>
        public int GetDependencyDepth(NodeId nodeId)
        {
            if (nodeId == null)
                return 0;

            var visited = new HashSet<NodeId>();
            var stack = new Stack<(NodeId node, int depth)>();

            stack.Push((nodeId, 0));
            var maxDepth = 0;

            while (stack.Count > 0)
            {
                var (current, depth) = stack.Pop();
                
                if (visited.Contains(current))
                    continue;

                visited.Add(current);
                maxDepth = Math.Max(maxDepth, depth);

                var dependencies = GetDirectDependencies(current);
                foreach (var dependency in dependencies)
                {
                    if (!visited.Contains(dependency))
                    {
                        stack.Push((dependency, depth + 1));
                    }
                }
            }

            return maxDepth;
        }

        /// <summary>
        /// Gets nodes that have no dependencies (leaf nodes)
        /// </summary>
        public HashSet<NodeId> GetLeafNodes()
        {
            using (_lockObject.ReadLock())
            {
                var leafNodes = new HashSet<NodeId>();
                
                foreach (var kvp in _dependencies)
                {
                    if (kvp.Value.Count == 0)
                    {
                        leafNodes.Add(kvp.Key);
                    }
                }
                
                return leafNodes;
            }
        }

        /// <summary>
        /// Gets nodes that no other nodes depend on (source nodes)
        /// </summary>
        public HashSet<NodeId> GetSourceNodes()
        {
            using (_lockObject.ReadLock())
            {
                var sourceNodes = new HashSet<NodeId>();
                
                foreach (var kvp in _dependents)
                {
                    if (kvp.Value.Count == 0)
                    {
                        sourceNodes.Add(kvp.Key);
                    }
                }
                
                return sourceNodes;
            }
        }

        #endregion

        #region Topological Sorting

        /// <summary>
        /// Gets optimal evaluation order for all nodes using topological sort
        /// </summary>
        public List<NodeId> GetTopologicalOrder()
        {
            var cacheKey = "all_nodes";
            
            // Check cache first
            if (TryGetCachedOrder(cacheKey, out var cachedOrder))
            {
                return cachedOrder;
            }

            var timer = Stopwatch.StartNew();
            
            try
            {
                using (_lockObject.ReadLock())
                {
                    var inDegree = new Dictionary<NodeId, int>();
                    var queue = new Queue<NodeId>();
                    var result = new List<NodeId>();

                    // Initialize in-degree counts
                    foreach (var nodeId in _dependencies.Keys)
                    {
                        inDegree[nodeId] = _dependents.TryGetValue(nodeId, out var dependents) ? dependents.Count : 0;
                    }

                    // Add nodes with no dependents (sources) to queue
                    foreach (var kvp in inDegree.Where(k => k.Value == 0))
                    {
                        queue.Enqueue(kvp.Key);
                    }

                    // Process nodes in topological order
                    while (queue.Count > 0)
                    {
                        var current = queue.Dequeue();
                        result.Add(current);

                        // Process dependencies
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
                        throw new InvalidOperationException($"Circular dependency detected. Nodes involved: {string.Join(", ", remainingNodes.Take(10).Select(n => n.Id))}");
                    }

                    // Cache the result
                    CacheOrder(cacheKey, result);

                    return result;
                }
            }
            finally
            {
                timer.Stop();
                _metrics.RecordTopologicalSort(timer.Elapsed, "full");
            }
        }

        /// <summary>
        /// Gets topological order for a specific set of nodes
        /// </summary>
        public List<NodeId> GetTopologicalOrderForNodes(HashSet<NodeId> nodes, bool includeCleanDependencies = false)
        {
            if (nodes == null || !nodes.Any())
                return new List<NodeId>();

            var nodesHash = string.Join(",", nodes.OrderBy(n => n.Id).Select(n => n.Id));
            var cacheKey = $"nodes_{nodesHash}_clean_{includeCleanDependencies}";
            
            // Check cache first
            if (TryGetCachedOrder(cacheKey, out var cachedOrder))
            {
                return cachedOrder;
            }

            var timer = Stopwatch.StartNew();
            
            try
            {
                using (_lockObject.ReadLock())
                {
                    var relevantNodes = new HashSet<NodeId>(nodes);
                    
                    // Optionally include clean dependencies
                    if (includeCleanDependencies)
                    {
                        foreach (var nodeId in nodes)
                        {
                            relevantNodes.UnionWith(GetAllDependencies(nodeId));
                        }
                    }

                    var inDegree = new Dictionary<NodeId, int>();
                    var queue = new Queue<NodeId>();
                    var result = new List<NodeId>();

                    // Initialize in-degree counts for relevant nodes
                    foreach (var nodeId in relevantNodes)
                    {
                        inDegree[nodeId] = _dependents.TryGetValue(nodeId, out var dependents) 
                            ? dependents.Count(n => relevantNodes.Contains(n)) 
                            : 0;
                    }

                    // Add nodes with no dependencies within the set to queue
                    foreach (var kvp in inDegree.Where(k => k.Value == 0))
                    {
                        queue.Enqueue(kvp.Key);
                    }

                    // Process nodes in topological order
                    while (queue.Count > 0)
                    {
                        var current = queue.Dequeue();
                        result.Add(current);

                        // Process dependencies within the relevant set
                        if (_dependencies.TryGetValue(current, out var dependencies))
                        {
                            foreach (var dependency in dependencies.Where(d => relevantNodes.Contains(d)))
                            {
                                inDegree[dependency]--;
                                if (inDegree[dependency] == 0)
                                {
                                    queue.Enqueue(dependency);
                                }
                            }
                        }
                    }

                    // Cache the result
                    CacheOrder(cacheKey, result);

                    return result;
                }
            }
            finally
            {
                timer.Stop();
                _metrics.RecordTopologicalSort(timer.Elapsed, "filtered");
            }
        }

        #endregion

        #region Dirty State Management

        /// <summary>
        /// Marks a node as dirty
        /// </summary>
        public void MarkDirty(NodeId nodeId, DirtyLevel level = DirtyLevel.Normal)
        {
            if (nodeId == null)
                return;

            using (_lockObject.WriteLock())
            {
                if (!_dirtyStates.TryGetValue(nodeId, out var state))
                {
                    RegisterNode(nodeId);
                    state = _dirtyStates[nodeId];
                }

                var wasDirty = state.IsDirty;
                state.IsDirty = true;
                state.DirtyLevel = level;
                state.LastModified = DateTime.UtcNow;

                if (!wasDirty)
                {
                    _metrics.NodeMarkedDirty();
                }
            }
        }

        /// <summary>
        /// Marks multiple nodes as dirty efficiently
        /// </summary>
        public void MarkDirtyBatch(IEnumerable<NodeId> nodeIds, DirtyLevel level = DirtyLevel.Normal)
        {
            if (nodeIds == null)
                return;

            using (_lockObject.WriteLock())
            {
                var nodesList = nodeIds.ToList();
                var newlyDirty = 0;

                foreach (var nodeId in nodesList)
                {
                    if (!_dirtyStates.TryGetValue(nodeId, out var state))
                    {
                        RegisterNode(nodeId);
                        state = _dirtyStates[nodeId];
                    }

                    if (!state.IsDirty)
                    {
                        newlyDirty++;
                        state.IsDirty = true;
                        state.DirtyLevel = level;
                        state.LastModified = DateTime.UtcNow;
                    }
                }

                if (newlyDirty > 0)
                {
                    _metrics.BatchMarkedDirty(newlyDirty);
                }
            }
        }

        /// <summary>
        /// Marks a node and all its dependents as dirty
        /// </summary>
        public void MarkDirtyWithDependents(NodeId nodeId, DirtyLevel level = DirtyLevel.Normal)
        {
            if (nodeId == null)
                return;

            MarkDirty(nodeId, level);

            // Find all dependents and mark them dirty
            var dependents = GetAllDependents(nodeId);
            foreach (var dependent in dependents)
            {
                MarkDirty(dependent, level);
            }

            _metrics.MarkedWithDependents(dependents.Count + 1);
        }

        /// <summary>
        /// Clears dirty flag for a specific node
        /// </summary>
        public void ClearDirty(NodeId nodeId)
        {
            if (nodeId == null)
                return;

            using (_lockObject.WriteLock())
            {
                if (_dirtyStates.TryGetValue(nodeId, out var state) && state.IsDirty)
                {
                    state.IsDirty = false;
                    state.LastEvaluated = DateTime.UtcNow;
                    _metrics.NodeCleaned();
                }
            }
        }

        /// <summary>
        /// Clears dirty flags for multiple nodes
        /// </summary>
        public void ClearDirtyBatch(IEnumerable<NodeId> nodeIds)
        {
            if (nodeIds == null)
                return;

            using (_lockObject.WriteLock())
            {
                var nodesList = nodeIds.ToList();
                var cleaned = 0;

                foreach (var nodeId in nodesList)
                {
                    if (_dirtyStates.TryGetValue(nodeId, out var state) && state.IsDirty)
                    {
                        state.IsDirty = false;
                        state.LastEvaluated = DateTime.UtcNow;
                        cleaned++;
                    }
                }

                if (cleaned > 0)
                {
                    _metrics.BatchCleaned(cleaned);
                }
            }
        }

        /// <summary>
        /// Clears all dirty flags
        /// </summary>
        public void ClearAllDirty()
        {
            using (_lockObject.WriteLock())
            {
                var dirtyCount = GetDirtyNodeCount();
                
                foreach (var kvp in _dirtyStates)
                {
                    kvp.Value.IsDirty = false;
                    kvp.Value.LastEvaluated = DateTime.UtcNow;
                }

                if (dirtyCount > 0)
                {
                    _metrics.AllDirtyCleared(dirtyCount);
                }
            }
        }

        /// <summary>
        /// Checks if a node is dirty
        /// </summary>
        public bool IsDirty(NodeId nodeId)
        {
            if (nodeId == null)
                return false;

            using (_lockObject.ReadLock())
            {
                return _dirtyStates.TryGetValue(nodeId, out var state) && state.IsDirty;
            }
        }

        /// <summary>
        /// Gets all dirty nodes
        /// </summary>
        public HashSet<NodeId> GetDirtyNodes()
        {
            using (_lockObject.ReadLock())
            {
                return new HashSet<NodeId>(_dirtyStates.Where(kvp => kvp.Value.IsDirty).Select(kvp => kvp.Key));
            }
        }

        /// <summary>
        /// Gets the number of dirty nodes
        /// </summary>
        public int GetDirtyNodeCount()
        {
            using (_lockObject.ReadLock())
            {
                return _dirtyStates.Count(kvp => kvp.Value.IsDirty);
            }
        }

        /// <summary>
        /// Gets dirty nodes by level
        /// </summary>
        public Dictionary<DirtyLevel, HashSet<NodeId>> GetDirtyNodesByLevel()
        {
            using (_lockObject.ReadLock())
            {
                return _dirtyStates.Where(kvp => kvp.Value.IsDirty)
                    .GroupBy(kvp => kvp.Value.DirtyLevel)
                    .ToDictionary(g => g.Key, g => new HashSet<NodeId>(g.Select(kvp => kvp.Key)));
            }
        }

        #endregion

        #region Analysis and Validation

        /// <summary>
        /// Detects if adding a dependency would create a circular dependency
        /// </summary>
        public bool WouldCreateCircularDependency(NodeId from, NodeId to)
        {
            if (from == null || to == null)
                return false;

            // Check if 'to' is already a dependency of 'from'
            var fromDependencies = GetAllDependencies(from);
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

            using (_lockObject.ReadLock())
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

        /// <summary>
        /// Gets comprehensive dependency graph statistics
        /// </summary>
        public DependencyTrackerStatistics GetStatistics()
        {
            using (_lockObject.ReadLock())
            {
                var stats = new DependencyTrackerStatistics
                {
                    TotalNodes = _dependencies.Count,
                    TotalDependencies = _dependencies.Values.Sum(d => d.Count),
                    TotalDependents = _dependents.Values.Sum(d => d.Count),
                    AverageDependenciesPerNode = _dependencies.Count > 0 
                        ? (double)_dependencies.Values.Sum(d => d.Count) / _dependencies.Count 
                        : 0,
                    AverageDependentsPerNode = _dependents.Count > 0 
                        ? (double)_dependents.Values.Sum(d => d.Count) / _dependents.Count 
                        : 0,
                    DirtyNodeCount = GetDirtyNodeCount(),
                    SourceNodes = GetSourceNodes().Count,
                    LeafNodes = GetLeafNodes().Count,
                    HasCircularDependencies = DetectCircularDependencies().HasCircularDependencies
                };

                // Find most complex nodes
                if (_dependencies.Any())
                {
                    var maxDeps = _dependencies.Values.Max(d => d.Count);
                    stats.MaxDependencies = maxDeps;
                    stats.NodesWithMostDependencies = _dependencies.Where(kvp => kvp.Value.Count == maxDeps)
                        .Select(kvp => kvp.Key)
                        .ToList();
                }

                if (_dependents.Any())
                {
                    var maxDependents = _dependents.Values.Max(d => d.Count);
                    stats.MaxDependents = maxDependents;
                    stats.NodesWithMostDependents = _dependents.Where(kvp => kvp.Value.Count == maxDependents)
                        .Select(kvp => kvp.Key)
                        .ToList();
                }

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

            using (_lockObject.ReadLock())
            {
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

                // Check for self-dependencies
                foreach (var kvp in _dependencies)
                {
                    if (kvp.Value.Contains(kvp.Key))
                    {
                        issues.Add($"Node '{kvp.Key}' depends on itself");
                    }
                }
            }

            result.IsValid = !issues.Any();
            result.Issues = issues;
            result.IssueCount = issues.Count;

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

            var dependencies = GetDirectDependencies(nodeId);
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

        private void UpdateNodeMetadata(NodeId nodeId, int dependentChange, int dependencyChange)
        {
            if (_nodeMetadata.TryGetValue(nodeId, out var metadata))
            {
                metadata.DependentCount += dependentChange;
                metadata.DependencyCount += dependencyChange;
            }
        }

        private void InvalidateCache()
        {
            _topologicalCache.Clear();
        }

        private bool TryGetCachedOrder(string key, out List<NodeId> order)
        {
            if (_topologicalCache.TryGetValue(key, out var cached) && 
                DateTime.UtcNow - cached.CachedAt < _cacheTimeout)
            {
                order = new List<NodeId>(cached.Order);
                return true;
            }

            order = null;
            return false;
        }

        private void CacheOrder(string key, List<NodeId> order)
        {
            _topologicalCache[key] = new TopologicalOrderCache
            {
                Order = new List<NodeId>(order),
                CachedAt = DateTime.UtcNow
            };
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed)
                return;

            _lockObject?.Dispose();
            
            _topologicalCache.Clear();
            _disposed = true;
        }

        #endregion

        #region Supporting Classes

        /// <summary>
        /// Dirty state information for a node
        /// </summary>
        public class DirtyState
        {
            public bool IsDirty { get; set; }
            public DirtyLevel DirtyLevel { get; set; }
            public DateTime LastModified { get; set; }
            public DateTime LastEvaluated { get; set; }
        }

        /// <summary>
        /// Metadata for a node
        /// </summary>
        public class NodeMetadata
        {
            public DateTime RegisteredAt { get; set; }
            public int InputSignatureHash { get; set; }
            public int DependencyCount { get; set; }
            public int DependentCount { get; set; }
            public TimeSpan TotalEvaluationTime { get; set; }
        }

        /// <summary>
        /// Cached topological order
        /// </summary>
        private class TopologicalOrderCache
        {
            public List<NodeId> Order { get; set; }
            public DateTime CachedAt { get; set; }
        }

        /// <summary>
        /// Dependency tracker statistics
        /// </summary>
        public class DependencyTrackerStatistics
        {
            public int TotalNodes { get; set; }
            public int TotalDependencies { get; set; }
            public int TotalDependents { get; set; }
            public double AverageDependenciesPerNode { get; set; }
            public double AverageDependentsPerNode { get; set; }
            public int MaxDependencies { get; set; }
            public int MaxDependents { get; set; }
            public List<NodeId> NodesWithMostDependencies { get; set; } = new();
            public List<NodeId> NodesWithMostDependents { get; set; } = new();
            public int DirtyNodeCount { get; set; }
            public int SourceNodes { get; set; }
            public int LeafNodes { get; set; }
            public bool HasCircularDependencies { get; set; }
        }

        /// <summary>
        /// Circular dependency detection result
        /// </summary>
        public class CircularDependencyResult
        {
            public bool HasCircularDependencies { get; set; }
            public List<List<NodeId>> Cycles { get; set; } = new();
            public int CycleCount { get; set; }
        }

        /// <summary>
        /// Dependency validation result
        /// </summary>
        public class DependencyValidationResult
        {
            public bool IsValid { get; set; }
            public List<string> Issues { get; set; } = new();
            public int IssueCount { get; set; }
        }

        /// <summary>
        /// Performance metrics for dependency tracker
        /// </summary>
        private class DependencyMetrics
        {
            private int _nodesRegistered = 0;
            private int _nodesUnregistered = 0;
            private int _dependenciesAdded = 0;
            private int _dependenciesRemoved = 0;
            private int _nodesMarkedDirty = 0;
            private int _nodesCleaned = 0;
            private int _batchMarkedDirty = 0;
            private int _batchCleaned = 0;
            private int _allDirtyCleared = 0;
            private int _markedWithDependents = 0;
            private readonly List<double> _topologicalSortTimes = new();

            public void NodeRegistered() => _nodesRegistered++;
            public void NodeUnregistered() => _nodesUnregistered++;
            public void DependencyAdded() => _dependenciesAdded++;
            public void DependencyRemoved() => _dependenciesRemoved++;
            public void NodeMarkedDirty() => _nodesMarkedDirty++;
            public void NodeCleaned() => _nodesCleaned++;
            public void BatchMarkedDirty(int count) => _batchMarkedDirty += count;
            public void BatchCleaned(int count) => _batchCleaned += count;
            public void AllDirtyCleared(int count) => _allDirtyCleared += count;
            public void MarkedWithDependents(int count) => _markedWithDependents += count;

            public void RecordTopologicalSort(TimeSpan duration, string type)
            {
                _topologicalSortTimes.Add(duration.TotalMilliseconds);
            }

            public DependencyTrackerPerformanceMetrics GetMetrics()
            {
                return new DependencyTrackerPerformanceMetrics
                {
                    TotalNodesRegistered = _nodesRegistered,
                    TotalNodesUnregistered = _nodesUnregistered,
                    TotalDependenciesAdded = _dependenciesAdded,
                    TotalDependenciesRemoved = _dependenciesRemoved,
                    TotalNodesMarkedDirty = _nodesMarkedDirty,
                    TotalNodesCleaned = _nodesCleaned,
                    TotalBatchMarkedDirty = _batchMarkedDirty,
                    TotalBatchCleaned = _batchCleaned,
                    TotalAllDirtyCleared = _allDirtyCleared,
                    TotalMarkedWithDependents = _markedWithDependents,
                    AverageTopologicalSortTime = _topologicalSortTimes.Any() ? _topologicalSortTimes.Average() : 0,
                    CacheHitRate = 0.8, // Mock implementation
                    TotalCacheEntries = 0 // Mock implementation
                };
            }
        }

        /// <summary>
        /// Performance metrics for dependency tracker
        /// </summary>
        public class DependencyTrackerPerformanceMetrics
        {
            public int TotalNodesRegistered { get; set; }
            public int TotalNodesUnregistered { get; set; }
            public int TotalDependenciesAdded { get; set; }
            public int TotalDependenciesRemoved { get; set; }
            public int TotalNodesMarkedDirty { get; set; }
            public int TotalNodesCleaned { get; set; }
            public int TotalBatchMarkedDirty { get; set; }
            public int TotalBatchCleaned { get; set; }
            public int TotalAllDirtyCleared { get; set; }
            public int TotalMarkedWithDependents { get; set; }
            public double AverageTopologicalSortTime { get; set; }
            public double CacheHitRate { get; set; }
            public int TotalCacheEntries { get; set; }
        }

        #endregion
    }
}