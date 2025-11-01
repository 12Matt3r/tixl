using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using T3.Core.Logging;
using T3.Core.Rendering;
using T3.Core.Resource;

namespace T3.Core.Operators.IncrementalEvaluation
{
    /// <summary>
    /// Incremental Node Graph Evaluation System (TIXL-025)
    /// Provides efficient evaluation of large node graphs through dirty tracking,
    /// dependency analysis, and selective re-evaluation with memoization.
    /// </summary>
    public class IncrementalNodeGraph : IDisposable
    {
        #region Private Fields

        private readonly EvaluationContext _context;
        private readonly Dictionary<NodeId, IIncrementalNode> _nodes;
        private readonly DependencyGraph _dependencyGraph;
        private readonly CacheManager _cacheManager;
        private readonly DirtyTracker _dirtyTracker;
        private readonly TopologicalEvaluator _evaluator;
        private readonly PerformanceMonitor _performanceMonitor;
        
        private readonly object _lockObject = new object();
        private bool _disposed = false;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the total number of nodes in the graph
        /// </summary>
        public int NodeCount => _nodes.Count;

        /// <summary>
        /// Gets the number of dirty nodes that need re-evaluation
        /// </summary>
        public int DirtyNodeCount => _dirtyTracker.GetDirtyNodeCount();

        /// <summary>
        /// Gets the cache statistics
        /// </summary>
        public CacheStatistics CacheStatistics => _cacheManager.GetStatistics();

        /// <summary>
        /// Gets the performance metrics for incremental evaluation
        /// </summary>
        public IncrementalEvaluationMetrics PerformanceMetrics => _performanceMonitor.GetMetrics();

        #endregion

        #region Constructor

        public IncrementalNodeGraph(
            EvaluationContext context,
            int maxCacheSize = 10000,
            TimeSpan? cacheTtl = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _nodes = new Dictionary<NodeId, IIncrementalNode>();
            _dependencyGraph = new DependencyGraph();
            _cacheManager = new CacheManager(maxCacheSize, cacheTtl ?? TimeSpan.FromMinutes(5));
            _dirtyTracker = new DirtyTracker();
            _evaluator = new TopologicalEvaluator(_dependencyGraph);
            _performanceMonitor = new PerformanceMonitor();

            _context.Logger.Debug($"Created IncrementalNodeGraph with cache size: {maxCacheSize}, TTL: {cacheTtl ?? TimeSpan.FromMinutes(5)}");
        }

        #endregion

        #region Node Management

        /// <summary>
        /// Adds a node to the graph
        /// </summary>
        public void AddNode(IIncrementalNode node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            lock (_lockObject)
            {
                if (_nodes.ContainsKey(node.Id))
                    throw new InvalidOperationException($"Node with ID '{node.Id}' already exists in the graph");

                _nodes[node.Id] = node;
                _dependencyGraph.AddNode(node.Id);
                _dirtyTracker.RegisterNode(node.Id);

                _context.Logger.Debug($"Added node '{node.Id}' to incremental graph. Total nodes: {_nodes.Count}");
            }
        }

        /// <summary>
        /// Removes a node from the graph
        /// </summary>
        public void RemoveNode(NodeId nodeId)
        {
            if (nodeId == null)
                throw new ArgumentNullException(nameof(nodeId));

            lock (_lockObject)
            {
                if (!_nodes.ContainsKey(nodeId))
                    return;

                // Remove all dependencies involving this node
                _dependencyGraph.RemoveNode(nodeId);
                _dirtyTracker.UnregisterNode(nodeId);
                _cacheManager.InvalidateNode(nodeId);

                _nodes.Remove(nodeId);

                _context.Logger.Debug($"Removed node '{nodeId}' from incremental graph. Remaining nodes: {_nodes.Count}");
            }
        }

        /// <summary>
        /// Adds a dependency relationship between nodes
        /// </summary>
        public void AddDependency(NodeId from, NodeId to)
        {
            if (from == null || to == null)
                throw new ArgumentNullException();

            lock (_lockObject)
            {
                if (!_nodes.ContainsKey(from) || !_nodes.ContainsKey(to))
                    throw new InvalidOperationException("Both nodes must exist in the graph");

                _dependencyGraph.AddDependency(from, to);
                _dirtyTracker.RegisterDependency(from, to);

                _context.Logger.Debug($"Added dependency: {from} -> {to}");
            }
        }

        /// <summary>
        /// Removes a dependency relationship
        /// </summary>
        public void RemoveDependency(NodeId from, NodeId to)
        {
            if (from == null || to == null)
                throw new ArgumentNullException();

            lock (_lockObject)
            {
                _dependencyGraph.RemoveDependency(from, to);
                _dirtyTracker.UnregisterDependency(from, to);

                _context.Logger.Debug($"Removed dependency: {from} -> {to}");
            }
        }

        #endregion

        #region Evaluation Methods

        /// <summary>
        /// Performs full evaluation of the entire graph
        /// </summary>
        public IncrementalEvaluationResult EvaluateAll()
        {
            return _context.ExecuteWithGuardrails("Incremental.EvaluateAll", () =>
            {
                var timer = Stopwatch.StartNew();
                
                try
                {
                    _performanceMonitor.StartEvaluation();
                    _context.Logger.Debug($"Starting full evaluation of {_nodes.Count} nodes");

                    // Get evaluation order using topological sort
                    var evaluationOrder = _evaluator.GetEvaluationOrder();
                    var result = EvaluateNodes(evaluationOrder, "Full");

                    _context.Logger.Debug($"Completed full evaluation in {timer.Elapsed.TotalMilliseconds:F2}ms");
                    return result;
                }
                finally
                {
                    timer.Stop();
                    _performanceMonitor.EndEvaluation(timer.Elapsed);
                }
            });
        }

        /// <summary>
        /// Performs incremental evaluation starting from specific nodes
        /// </summary>
        public IncrementalEvaluationResult EvaluateIncremental(IEnumerable<NodeId> sourceNodes)
        {
            return _context.ExecuteWithGuardrails("Incremental.EvaluateIncremental", () =>
            {
                var timer = Stopwatch.StartNew();
                var sourceNodeList = sourceNodes?.ToList() ?? throw new ArgumentNullException(nameof(sourceNodes));
                
                try
                {
                    _performanceMonitor.StartIncrementalEvaluation();
                    
                    if (!sourceNodeList.Any())
                    {
                        return new IncrementalEvaluationResult
                        {
                            Success = true,
                            AffectedNodeCount = 0,
                            EvaluatedNodeCount = 0,
                            EvaluationTime = 0,
                            CachedResultsUsed = 0
                        };
                    }

                    _context.Logger.Debug($"Starting incremental evaluation from {sourceNodeList.Count} source nodes");

                    // Find all nodes affected by the source nodes
                    var affectedNodes = FindAffectedNodes(sourceNodeList);
                    
                    // Get topological order for dirty nodes only
                    var evaluationOrder = _evaluator.GetEvaluationOrderForDirtyNodes(affectedNodes);
                    
                    // Mark affected nodes as dirty
                    foreach (var nodeId in affectedNodes)
                    {
                        MarkNodeDirty(nodeId);
                    }

                    var result = EvaluateNodes(evaluationOrder, "Incremental");

                    _context.Logger.Debug($"Completed incremental evaluation in {timer.Elapsed.TotalMilliseconds:F2}ms, affected {affectedNodes.Count} nodes");
                    return result;
                }
                finally
                {
                    timer.Stop();
                    _performanceMonitor.EndIncrementalEvaluation(timer.Elapsed, _dirtyTracker.GetDirtyNodeCount());
                }
            });
        }

        /// <summary>
        /// Evaluates a single node and its dependencies
        /// </summary>
        public IncrementalEvaluationResult EvaluateNode(NodeId nodeId)
        {
            if (nodeId == null)
                throw new ArgumentNullException(nameof(nodeId));

            return _context.ExecuteWithGuardrails("Incremental.EvaluateNode", () =>
            {
                var timer = Stopwatch.StartNew();
                
                try
                {
                    _context.Logger.Debug($"Evaluating single node: {nodeId}");

                    if (!_nodes.ContainsKey(nodeId))
                        throw new InvalidOperationException($"Node '{nodeId}' does not exist in the graph");

                    // Find nodes that need evaluation (node + dependencies)
                    var nodesToEvaluate = new HashSet<NodeId> { nodeId };
                    var dependencies = _dependencyGraph.GetDependencies(nodeId);
                    
                    foreach (var dependency in dependencies)
                    {
                        var transitiveDeps = FindAffectedNodes(new[] { dependency });
                        nodesToEvaluate.UnionWith(transitiveDeps);
                    }

                    // Get evaluation order
                    var evaluationOrder = _evaluator.GetEvaluationOrderForSpecificNodes(nodesToEvaluate);

                    // Mark all as dirty
                    foreach (var id in nodesToEvaluate)
                    {
                        MarkNodeDirty(id);
                    }

                    var result = EvaluateNodes(evaluationOrder, "Single");

                    _context.Logger.Debug($"Completed single node evaluation in {timer.Elapsed.TotalMilliseconds:F2}ms");
                    return result;
                }
                finally
                {
                    timer.Stop();
                    _performanceMonitor.EndSingleNodeEvaluation(timer.Elapsed);
                }
            });
        }

        #endregion

        #region Caching Methods

        /// <summary>
        /// Gets cached result for a node if available and valid
        /// </summary>
        public CacheResult GetCachedResult(NodeId nodeId)
        {
            if (nodeId == null)
                return new CacheResult { Hit = false };

            var node = _nodes.GetValueOrDefault(nodeId);
            if (node == null)
                return new CacheResult { Hit = false };

            var signature = node.CalculateSignature();
            return _cacheManager.GetCachedResult(nodeId, signature);
        }

        /// <summary>
        /// Invalidates cache for a specific node and all its dependents
        /// </summary>
        public void InvalidateNodeCache(NodeId nodeId)
        {
            if (nodeId == null)
                return;

            _cacheManager.InvalidateNodeAndDependents(nodeId);
            MarkNodeDirty(nodeId);
        }

        /// <summary>
        /// Clears the entire cache
        /// </summary>
        public void ClearCache()
        {
            _cacheManager.ClearCache();
        }

        #endregion

        #region Dirty Management

        /// <summary>
        /// Marks a node as dirty (needs re-evaluation)
        /// </summary>
        public void MarkNodeDirty(NodeId nodeId)
        {
            if (nodeId == null)
                return;

            _dirtyTracker.MarkDirty(nodeId);
            
            // Invalidate cache for this node
            _cacheManager.InvalidateNode(nodeId);
        }

        /// <summary>
        /// Marks multiple nodes as dirty
        /// </summary>
        public void MarkNodesDirty(IEnumerable<NodeId> nodeIds)
        {
            if (nodeIds == null)
                return;

            foreach (var nodeId in nodeIds)
            {
                MarkNodeDirty(nodeId);
            }
        }

        /// <summary>
        /// Gets all dirty nodes
        /// </summary>
        public HashSet<NodeId> GetDirtyNodes()
        {
            return _dirtyTracker.GetDirtyNodes();
        }

        /// <summary>
        /// Clears all dirty flags
        /// </summary>
        public void ClearDirtyFlags()
        {
            _dirtyTracker.ClearAllDirty();
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Finds all nodes affected by changes in the source nodes
        /// </summary>
        private HashSet<NodeId> FindAffectedNodes(List<NodeId> sourceNodes)
        {
            var affected = new HashSet<NodeId>();
            var queue = new Queue<NodeId>(sourceNodes);
            
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                
                if (affected.Contains(current))
                    continue;
                    
                affected.Add(current);
                
                // Find all nodes that depend on current (reverse dependencies)
                var dependents = _dependencyGraph.GetDependents(current);
                foreach (var dependent in dependents)
                {
                    if (!affected.Contains(dependent))
                    {
                        queue.Enqueue(dependent);
                    }
                }
            }
            
            return affected;
        }

        /// <summary>
        /// Evaluates nodes in the specified order
        /// </summary>
        private IncrementalEvaluationResult EvaluateNodes(List<NodeId> evaluationOrder, string evaluationType)
        {
            var result = new IncrementalEvaluationResult
            {
                EvaluationType = evaluationType,
                StartTime = DateTime.UtcNow
            };

            var evaluationTimer = Stopwatch.StartNew();
            var cachedResultsUsed = 0;
            var successfulEvaluations = 0;
            var failedEvaluations = 0;

            foreach (var nodeId in evaluationOrder)
            {
                try
                {
                    if (!_nodes.TryGetValue(nodeId, out var node))
                    {
                        _context.Logger.Warning($"Node '{nodeId}' not found during evaluation");
                        failedEvaluations++;
                        continue;
                    }

                    var nodeResult = EvaluateSingleNode(node);
                    
                    if (nodeResult.Success)
                    {
                        successfulEvaluations++;
                        if (nodeResult.Cached)
                        {
                            cachedResultsUsed++;
                        }
                    }
                    else
                    {
                        failedEvaluations++;
                    }
                }
                catch (Exception ex)
                {
                    _context.Logger.Error(ex, $"Error evaluating node '{nodeId}'");
                    failedEvaluations++;
                }
            }

            evaluationTimer.Stop();
            result.Success = failedEvaluations == 0;
            result.EvaluatedNodeCount = successfulEvaluations;
            result.FailedNodeCount = failedEvaluations;
            result.EvaluationTime = evaluationTimer.Elapsed.TotalMilliseconds;
            result.CachedResultsUsed = cachedResultsUsed;
            result.EndTime = DateTime.UtcNow;

            // Clear dirty flags for successfully evaluated nodes
            foreach (var nodeId in evaluationOrder)
            {
                _dirtyTracker.ClearDirty(nodeId);
            }

            return result;
        }

        /// <summary>
        /// Evaluates a single node with caching support
        /// </summary>
        private NodeEvaluationResult EvaluateSingleNode(IIncrementalNode node)
        {
            var nodeId = node.Id;
            var signature = node.CalculateSignature();

            // Check cache first
            var cacheResult = _cacheManager.GetCachedResult(nodeId, signature);
            
            if (cacheResult.Hit)
            {
                _context.Logger.Debug($"Using cached result for node '{nodeId}'");
                return new NodeEvaluationResult
                {
                    Success = true,
                    Cached = true,
                    Result = cacheResult.Result,
                    EvaluationTime = TimeSpan.Zero
                };
            }

            // Evaluate the node
            var timer = Stopwatch.StartNew();
            
            try
            {
                // Validate dependencies are available
                ValidateDependencies(node);

                // Perform evaluation
                var nodeResult = node.Evaluate();
                
                timer.Stop();
                
                // Cache the result
                _cacheManager.CacheResult(nodeId, nodeResult, signature);
                
                // Mark node as evaluated
                node.IsEvaluated = true;
                node.LastEvaluationTime = DateTime.UtcNow;

                return new NodeEvaluationResult
                {
                    Success = true,
                    Cached = false,
                    Result = nodeResult,
                    EvaluationTime = timer.Elapsed
                };
            }
            catch (Exception ex)
            {
                timer.Stop();
                _context.Logger.Error(ex, $"Error during evaluation of node '{nodeId}'");
                
                return new NodeEvaluationResult
                {
                    Success = false,
                    Cached = false,
                    Error = ex.Message,
                    EvaluationTime = timer.Elapsed
                };
            }
        }

        /// <summary>
        /// Validates that all dependencies for a node are available
        /// </summary>
        private void ValidateDependencies(IIncrementalNode node)
        {
            var dependencies = _dependencyGraph.GetDependencies(node.Id);
            
            foreach (var dependencyId in dependencies)
            {
                if (!_nodes.TryGetValue(dependencyId, out var dependencyNode))
                {
                    throw new InvalidOperationException($"Dependency '{dependencyId}' for node '{node.Id}' does not exist");
                }

                if (!dependencyNode.IsEvaluated)
                {
                    throw new InvalidOperationException($"Dependency '{dependencyId}' for node '{node.Id}' has not been evaluated");
                }
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed)
                return;

            lock (_lockObject)
            {
                _cacheManager?.Dispose();
                _performanceMonitor?.Dispose();
                
                _disposed = true;
            }
        }

        #endregion

        #region Supporting Classes

        /// <summary>
        /// Metrics for incremental evaluation performance
        /// </summary>
        public class IncrementalEvaluationMetrics
        {
            public int TotalEvaluations { get; set; }
            public int IncrementalEvaluations { get; set; }
            public double AverageEvaluationTime { get; set; }
            public double AverageIncrementalTime { get; set; }
            public int CacheHitRate { get; set; }
            public long TotalCacheHits { get; set; }
            public long TotalCacheMisses { get; set; }
        }

        /// <summary>
        /// Result of node evaluation
        /// </summary>
        public class NodeEvaluationResult
        {
            public bool Success { get; set; }
            public bool Cached { get; set; }
            public object Result { get; set; }
            public string Error { get; set; }
            public TimeSpan EvaluationTime { get; set; }
        }

        #endregion
    }

    #region Interface Definitions

    /// <summary>
    /// Interface for nodes that support incremental evaluation
    /// </summary>
    public interface IIncrementalNode
    {
        /// <summary>
        /// Unique identifier for this node
        /// </summary>
        NodeId Id { get; }

        /// <summary>
        /// Whether this node needs re-evaluation
        /// </summary>
        bool IsDirty { get; set; }

        /// <summary>
        /// Whether this node has been evaluated
        /// </summary>
        bool IsEvaluated { get; set; }

        /// <summary>
        /// Time of last evaluation
        /// </summary>
        DateTime LastEvaluationTime { get; set; }

        /// <summary>
        /// Calculates a signature representing the node's current state
        /// </summary>
        NodeSignature CalculateSignature();

        /// <summary>
        /// Performs the actual evaluation of this node
        /// </summary>
        object Evaluate();
    }

    /// <summary>
    /// Represents the signature of a node's current state
    /// </summary>
    public class NodeSignature
    {
        public Dictionary<string, object> Parameters { get; set; } = new();
        public List<NodeId> Dependencies { get; set; } = new();

        public bool Equals(NodeSignature other)
        {
            if (Parameters.Count != other.Parameters.Count)
                return false;

            foreach (var kvp in Parameters)
            {
                if (!other.Parameters.TryGetValue(kvp.Key, out var otherValue) ||
                    !Equals(kvp.Value, otherValue))
                    return false;
            }

            return Dependencies.SequenceEqual(other.Dependencies);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                foreach (var kvp in Parameters)
                {
                    hash = hash * 31 + kvp.Key.GetHashCode();
                    hash = hash * 31 + (kvp.Value?.GetHashCode() ?? 0);
                }
                return hash;
            }
        }
    }

    /// <summary>
    /// Unique identifier for a node
    /// </summary>
    public class NodeId : IEquatable<NodeId>
    {
        public string Id { get; }

        public NodeId(string id)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
        }

        public bool Equals(NodeId other) => Id == other?.Id;
        public override bool Equals(object obj) => Equals(obj as NodeId);
        public override int GetHashCode() => Id.GetHashCode();
        public override string ToString() => Id;

        public static implicit operator string(NodeId nodeId) => nodeId?.Id;
    }

    #endregion
}