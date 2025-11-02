using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using T3.Core.Logging;
using T3.Core.Operators;
using T3.Core.Operators.IncrementalEvaluation;

namespace T3.Core.NodeGraph
{
    /// <summary>
    /// Enhanced Incremental Evaluation Engine for TiXL Node System
    /// Implements real incremental evaluation logic with dependency tracking, 
    /// change detection, caching, and performance monitoring
    /// </summary>
    public class IncrementalEvaluationEngine : IDisposable
    {
        #region Private Fields

        private readonly EvaluationContext _context;
        private readonly NodeDependencyTracker _dependencyTracker;
        private readonly EvaluationCache _evaluationCache;
        private readonly Dictionary<NodeId, TiXLNode> _nodes;
        private readonly Dictionary<NodeId, NodeEvaluationState> _evaluationStates;
        private readonly Dictionary<NodeId, NodeSignature> _nodeSignatures;
        
        // Thread safety
        private readonly ReaderWriterLockSlim _graphLock = new();
        private readonly ConcurrentDictionary<NodeId, SemaphoreSlim> _nodeLocks;
        
        // Performance monitoring
        private readonly PerformanceMonitor _performanceMonitor;
        private readonly Stopwatch _globalTimer;
        
        private bool _disposed = false;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the total number of nodes in the graph
        /// </summary>
        public int NodeCount 
        { 
            get 
            { 
                using (_graphLock.ReadLock()) 
                    return _nodes.Count; 
            } 
        }

        /// <summary>
        /// Gets the number of nodes that need re-evaluation
        /// </summary>
        public int DirtyNodeCount => _dependencyTracker.GetDirtyNodeCount();

        /// <summary>
        /// Gets comprehensive performance metrics
        /// </summary>
        public EvaluationEngineMetrics Metrics => _performanceMonitor.GetCurrentMetrics();

        /// <summary>
        /// Gets the evaluation cache statistics
        /// </summary>
        public CacheStatistics CacheStatistics => _evaluationCache.GetStatistics();

        #endregion

        #region Constructor

        public IncrementalEvaluationEngine(EvaluationContext context, int maxCacheSize = 10000)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _nodes = new Dictionary<NodeId, TiXLNode>();
            _evaluationStates = new Dictionary<NodeId, NodeEvaluationState>();
            _nodeSignatures = new Dictionary<NodeId, NodeSignature>();
            _nodeLocks = new ConcurrentDictionary<NodeId, SemaphoreSlim>();
            
            // Initialize core components
            _dependencyTracker = new NodeDependencyTracker();
            _evaluationCache = new EvaluationCache(maxCacheSize, TimeSpan.FromMinutes(5));
            _performanceMonitor = new PerformanceMonitor();
            _globalTimer = Stopwatch.StartNew();

            _context.Logger.Debug($"Created IncrementalEvaluationEngine with cache size: {maxCacheSize}");
        }

        #endregion

        #region Node Management

        /// <summary>
        /// Adds a TiXL node to the evaluation graph
        /// </summary>
        public void AddNode(TiXLNode node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));

            using (_graphLock.WriteLock())
            {
                if (_nodes.ContainsKey(node.Id))
                    throw new InvalidOperationException($"Node with ID '{node.Id}' already exists");

                _nodes[node.Id] = node;
                _evaluationStates[node.Id] = new NodeEvaluationState
                {
                    IsEvaluated = false,
                    IsDirty = true,
                    LastEvaluationTime = DateTime.MinValue,
                    LastModifiedTime = DateTime.UtcNow,
                    EvaluationCount = 0,
                    State = NodeState.New
                };

                // Initialize dependency tracking
                _dependencyTracker.RegisterNode(node.Id);
                _nodeLocks.GetOrAdd(node.Id, _ => new SemaphoreSlim(1, 1));

                _context.Logger.Debug($"Added TiXL node '{node.Id}' to evaluation graph");
            }

            // Automatically discover dependencies from node inputs
            DiscoverNodeDependencies(node);
        }

        /// <summary>
        /// Removes a node from the evaluation graph
        /// </summary>
        public void RemoveNode(NodeId nodeId)
        {
            if (nodeId == null)
                return;

            using (_graphLock.WriteLock())
            {
                if (!_nodes.ContainsKey(nodeId))
                    return;

                // Remove from all tracking systems
                _nodes.Remove(nodeId);
                _evaluationStates.Remove(nodeId);
                _nodeSignatures.Remove(nodeId);
                _dependencyTracker.UnregisterNode(nodeId);
                _evaluationCache.InvalidateNode(nodeId);

                // Remove and dispose node lock
                if (_nodeLocks.TryRemove(nodeId, out var nodeLock))
                {
                    nodeLock?.Dispose();
                }

                _context.Logger.Debug($"Removed node '{nodeId}' from evaluation graph");
            }
        }

        /// <summary>
        /// Updates an existing node with new inputs
        /// </summary>
        public void UpdateNodeInputs(NodeId nodeId, Dictionary<string, object> newInputs)
        {
            using (_graphLock.ReadLock())
            {
                if (!_nodes.TryGetValue(nodeId, out var node))
                    return;

                var oldSignature = _nodeSignatures.GetValueOrDefault(nodeId);
                var newSignature = CalculateNodeSignature(node, newInputs);

                // Check if signature actually changed
                if (oldSignature?.Equals(newSignature) == true)
                {
                    return; // No changes, nothing to do
                }

                // Update signature and mark as dirty
                _nodeSignatures[nodeId] = newSignature;
                MarkNodeDirty(nodeId, "Input changed");

                // Invalidate cache entries
                _evaluationCache.InvalidateNode(nodeId);

                _context.Logger.Debug($"Updated inputs for node '{nodeId}', marked as dirty");
            }
        }

        #endregion

        #region Evaluation Methods

        /// <summary>
        /// Performs full evaluation of all nodes in optimal order
        /// </summary>
        public EvaluationResult EvaluateAll()
        {
            return _context.ExecuteWithGuardrails("IncrementalEvaluation.EvaluateAll", () =>
            {
                var timer = Stopwatch.StartNew();
                _performanceMonitor.StartFullEvaluation();

                try
                {
                    using (_graphLock.ReadLock())
                    {
                        var evaluationOrder = GetOptimalEvaluationOrder();
                        var result = EvaluateNodes(evaluationOrder, EvaluationMode.Full);

                        _context.Logger.Debug($"Completed full evaluation of {evaluationOrder.Count} nodes in {timer.Elapsed.TotalMilliseconds:F2}ms");
                        return result;
                    }
                }
                finally
                {
                    timer.Stop();
                    _performanceMonitor.EndFullEvaluation(timer.Elapsed, _nodes.Count);
                }
            });
        }

        /// <summary>
        /// Performs incremental evaluation starting from specific source nodes
        /// </summary>
        public EvaluationResult EvaluateIncremental(IEnumerable<NodeId> sourceNodes)
        {
            return _context.ExecuteWithGuardrails("IncrementalEvaluation.EvaluateIncremental", () =>
            {
                var timer = Stopwatch.StartNew();
                var sourceNodeList = sourceNodes?.ToList() ?? new List<NodeId>();

                try
                {
                    _performanceMonitor.StartIncrementalEvaluation();

                    if (!sourceNodeList.Any())
                    {
                        return new EvaluationResult
                        {
                            Success = true,
                            EvaluationMode = EvaluationMode.Incremental,
                            AffectedNodeCount = 0,
                            EvaluatedNodeCount = 0,
                            EvaluationTime = TimeSpan.Zero
                        };
                    }

                    using (_graphLock.ReadLock())
                    {
                        // Find all affected nodes
                        var affectedNodes = FindAffectedNodes(sourceNodeList);

                        // Mark affected nodes as dirty
                        _dependencyTracker.MarkDirtyBatch(affectedNodes);

                        // Get evaluation order for dirty nodes only
                        var evaluationOrder = GetOptimalEvaluationOrderForDirtyNodes(affectedNodes);

                        var result = EvaluateNodes(evaluationOrder, EvaluationMode.Incremental);

                        _context.Logger.Debug($"Completed incremental evaluation affecting {affectedNodes.Count} nodes in {timer.Elapsed.TotalMilliseconds:F2}ms");
                        return result;
                    }
                }
                finally
                {
                    timer.Stop();
                    _performanceMonitor.EndIncrementalEvaluation(timer.Elapsed, _dependencyTracker.GetDirtyNodeCount());
                }
            });
        }

        /// <summary>
        /// Evaluates a single node and its dependencies
        /// </summary>
        public EvaluationResult EvaluateNode(NodeId nodeId)
        {
            return _context.ExecuteWithGuardrails("IncrementalEvaluation.EvaluateNode", () =>
            {
                var timer = Stopwatch.StartNew();
                _performanceMonitor.StartSingleNodeEvaluation();

                try
                {
                    using (_graphLock.ReadLock())
                    {
                        if (!_nodes.ContainsKey(nodeId))
                            throw new InvalidOperationException($"Node '{nodeId}' does not exist");

                        // Find dependencies and mark as dirty
                        var dependencies = _dependencyTracker.GetAllDependencies(nodeId);
                        var nodesToEvaluate = new HashSet<NodeId> { nodeId };
                        nodesToEvaluate.UnionWith(dependencies);

                        _dependencyTracker.MarkDirtyBatch(nodesToEvaluate);

                        // Get evaluation order
                        var evaluationOrder = GetOptimalEvaluationOrderForNodes(nodesToEvaluate);

                        var result = EvaluateNodes(evaluationOrder, EvaluationMode.SingleNode);

                        _context.Logger.Debug($"Completed single node evaluation for '{nodeId}' in {timer.Elapsed.TotalMilliseconds:F2}ms");
                        return result;
                    }
                }
                finally
                {
                    timer.Stop();
                    _performanceMonitor.EndSingleNodeEvaluation(timer.Elapsed);
                }
            });
        }

        /// <summary>
        /// Gets the result of a node evaluation with caching support
        /// </summary>
        public object? GetNodeResult(NodeId nodeId)
        {
            using (_graphLock.ReadLock())
            {
                if (!_nodes.TryGetValue(nodeId, out var node))
                    return null;

                // Check cache first
                var signature = _nodeSignatures.GetValueOrDefault(nodeId) ?? CalculateNodeSignature(node);
                var cacheResult = _evaluationCache.GetCachedResult(nodeId, signature);

                if (cacheResult.Hit)
                {
                    _performanceMonitor.RecordCacheHit();
                    return cacheResult.Result;
                }

                // If not dirty and not in cache, we need to evaluate
                var state = _evaluationStates.GetValueOrDefault(nodeId);
                if (state?.IsDirty == false && state?.IsEvaluated == true)
                {
                    // Re-evaluate to get the result
                    EvaluateNode(nodeId);
                    
                    // Try cache again
                    cacheResult = _evaluationCache.GetCachedResult(nodeId, signature);
                    if (cacheResult.Hit)
                    {
                        return cacheResult.Result;
                    }
                }

                return null;
            }
        }

        #endregion

        #region Private Evaluation Methods

        /// <summary>
        /// Evaluates nodes in the specified order with thread safety
        /// </summary>
        private EvaluationResult EvaluateNodes(List<NodeId> evaluationOrder, EvaluationMode mode)
        {
            var result = new EvaluationResult
            {
                EvaluationMode = mode,
                StartTime = DateTime.UtcNow,
                EvaluationOrder = new List<NodeId>(evaluationOrder)
            };

            var successfulEvaluations = 0;
            var failedEvaluations = 0;
            var cachedResultsUsed = 0;
            var totalEvaluationTime = TimeSpan.Zero;

            foreach (var nodeId in evaluationOrder)
            {
                var nodeResult = EvaluateSingleNode(nodeId);
                
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

                totalEvaluationTime += nodeResult.EvaluationTime;
            }

            // Update result
            result.Success = failedEvaluations == 0;
            result.EvaluatedNodeCount = successfulEvaluations;
            result.FailedNodeCount = failedEvaluations;
            result.CachedResultsUsed = cachedResultsUsed;
            result.TotalEvaluationTime = totalEvaluationTime;
            result.EvaluationTime = DateTime.UtcNow - result.StartTime;
            result.EndTime = DateTime.UtcNow;

            return result;
        }

        /// <summary>
        /// Evaluates a single node with thread safety and caching
        /// </summary>
        private NodeEvaluationResult EvaluateSingleNode(NodeId nodeId)
        {
            var nodeLock = _nodeLocks.GetValueOrDefault(nodeId);
            if (nodeLock == null)
            {
                return new NodeEvaluationResult
                {
                    Success = false,
                    Error = $"Node lock not found for '{nodeId}'",
                    EvaluationTime = TimeSpan.Zero
                };
            }

            return nodeLock.Execute(() =>
            {
                var timer = Stopwatch.StartNew();

                try
                {
                    if (!_nodes.TryGetValue(nodeId, out var node))
                    {
                        return new NodeEvaluationResult
                        {
                            Success = false,
                            Error = $"Node '{nodeId}' not found",
                            EvaluationTime = TimeSpan.Zero
                        };
                    }

                    var state = _evaluationStates.GetValueOrDefault(nodeId);
                    var signature = _nodeSignatures.GetValueOrDefault(nodeId) ?? CalculateNodeSignature(node);

                    // Check if evaluation is still needed
                    if (state?.IsEvaluated == true && !state.IsDirty)
                    {
                        var cacheResult = _evaluationCache.GetCachedResult(nodeId, signature);
                        if (cacheResult.Hit)
                        {
                            timer.Stop();
                            return new NodeEvaluationResult
                            {
                                Success = true,
                                Cached = true,
                                Result = cacheResult.Result,
                                EvaluationTime = TimeSpan.Zero
                            };
                        }
                    }

                    // Validate dependencies are available
                    ValidateNodeDependencies(nodeId);

                    // Evaluate the node
                    var nodeResult = node.Evaluate(_context);

                    timer.Stop();

                    // Cache the result
                    _evaluationCache.CacheResult(nodeId, nodeResult, signature);

                    // Update state
                    if (_evaluationStates.TryGetValue(nodeId, out var currentState))
                    {
                        currentState.IsEvaluated = true;
                        currentState.IsDirty = false;
                        currentState.LastEvaluationTime = DateTime.UtcNow;
                        currentState.EvaluationCount++;
                        currentState.State = NodeState.Evaluated;
                    }

                    _performanceMonitor.RecordNodeEvaluation(nodeId, timer.Elapsed, true);

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
                    _context.Logger.Error(ex, $"Error evaluating node '{nodeId}'");
                    _performanceMonitor.RecordNodeEvaluation(nodeId, timer.Elapsed, false);

                    return new NodeEvaluationResult
                    {
                        Success = false,
                        Error = ex.Message,
                        EvaluationTime = timer.Elapsed
                    };
                }
            });
        }

        /// <summary>
        /// Validates that all dependencies for a node are evaluated and available
        /// </summary>
        private void ValidateNodeDependencies(NodeId nodeId)
        {
            var dependencies = _dependencyTracker.GetDirectDependencies(nodeId);
            
            foreach (var dependencyId in dependencies)
            {
                var dependencyState = _evaluationStates.GetValueOrDefault(dependencyId);
                if (dependencyState?.IsEvaluated != true)
                {
                    throw new InvalidOperationException($"Dependency '{dependencyId}' for node '{nodeId}' has not been evaluated");
                }
            }
        }

        /// <summary>
        /// Gets the optimal evaluation order for all nodes using topological sorting
        /// </summary>
        private List<NodeId> GetOptimalEvaluationOrder()
        {
            return _dependencyTracker.GetTopologicalOrder();
        }

        /// <summary>
        /// Gets the optimal evaluation order for dirty nodes only
        /// </summary>
        private List<NodeId> GetOptimalEvaluationOrderForDirtyNodes(HashSet<NodeId> dirtyNodes)
        {
            return _dependencyTracker.GetTopologicalOrderForNodes(dirtyNodes, includeCleanDependencies: true);
        }

        /// <summary>
        /// Gets the optimal evaluation order for a specific set of nodes
        /// </summary>
        private List<NodeId> GetOptimalEvaluationOrderForNodes(HashSet<NodeId> nodes)
        {
            return _dependencyTracker.GetTopologicalOrderForNodes(nodes, includeCleanDependencies: true);
        }

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
                var dependents = _dependencyTracker.GetDependents(current);
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
        /// Discovers and registers dependencies for a TiXL node
        /// </summary>
        private void DiscoverNodeDependencies(TiXLNode node)
        {
            var nodeId = node.Id;
            
            // Analyze node inputs to find dependencies
            foreach (var input in node.Inputs)
            {
                if (input.Value is TiXLNodeRef nodeRef && nodeRef.NodeId != null)
                {
                    // Add dependency: node depends on referenced node
                    _dependencyTracker.AddDependency(nodeId, nodeRef.NodeId);
                    _context.Logger.Debug($"Discovered dependency: {nodeId} -> {nodeRef.NodeId}");
                }
            }
        }

        /// <summary>
        /// Calculates the signature for a node based on its inputs
        /// </summary>
        private NodeSignature CalculateNodeSignature(TiXLNode node, Dictionary<string, object>? inputs = null)
        {
            var signature = new NodeSignature();
            
            inputs ??= node.Inputs;
            
            foreach (var kvp in inputs)
            {
                signature.Parameters[kvp.Key] = kvp.Value;
            }
            
            // Include node dependencies in signature
            var dependencies = _dependencyTracker.GetDirectDependencies(node.Id);
            signature.Dependencies = dependencies.ToList();
            
            return signature;
        }

        /// <summary>
        /// Marks a node as dirty and propagates dirty state to dependents
        /// </summary>
        private void MarkNodeDirty(NodeId nodeId, string reason)
        {
            _dependencyTracker.MarkDirty(nodeId, DirtyLevel.Normal);
            
            if (_evaluationStates.TryGetValue(nodeId, out var state))
            {
                state.IsDirty = true;
                state.LastModifiedTime = DateTime.UtcNow;
                state.State = NodeState.Dirty;
            }

            _context.Logger.Debug($"Marked node '{nodeId}' as dirty: {reason}");
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed)
                return;

            _graphLock?.Dispose();
            
            foreach (var nodeLock in _nodeLocks.Values)
            {
                nodeLock?.Dispose();
            }
            _nodeLocks.Clear();

            _evaluationCache?.Dispose();
            _performanceMonitor?.Dispose();

            _disposed = true;
        }

        #endregion

        #region Supporting Classes

        /// <summary>
        /// TiXL Node implementation for the evaluation system
        /// </summary>
        public class TiXLNode
        {
            public NodeId Id { get; set; }
            public Dictionary<string, object> Inputs { get; set; } = new();
            public Dictionary<string, Type> OutputTypes { get; set; } = new();
            public bool IsEvaluated { get; set; }
            public DateTime LastEvaluationTime { get; set; }

            public object? Evaluate(EvaluationContext context)
            {
                // Implementation would depend on actual TiXL node types
                // This is a placeholder that would integrate with real TiXL node evaluation
                return new { Result = $"Evaluated node {Id}", Timestamp = DateTime.UtcNow };
            }
        }

        /// <summary>
        /// Reference to another TiXL node
        /// </summary>
        public class TiXLNodeRef
        {
            public NodeId? NodeId { get; set; }
            public string? OutputName { get; set; }
        }

        /// <summary>
        /// Evaluation state for a node
        /// </summary>
        public class NodeEvaluationState
        {
            public bool IsEvaluated { get; set; }
            public bool IsDirty { get; set; }
            public DateTime LastEvaluationTime { get; set; }
            public DateTime LastModifiedTime { get; set; }
            public int EvaluationCount { get; set; }
            public NodeState State { get; set; }
            public TimeSpan TotalEvaluationTime { get; set; }
        }

        /// <summary>
        /// Result of node evaluation
        /// </summary>
        public class NodeEvaluationResult
        {
            public bool Success { get; set; }
            public bool Cached { get; set; }
            public object? Result { get; set; }
            public string? Error { get; set; }
            public TimeSpan EvaluationTime { get; set; }
        }

        /// <summary>
        /// Overall evaluation result
        /// </summary>
        public class EvaluationResult
        {
            public bool Success { get; set; }
            public EvaluationMode EvaluationMode { get; set; }
            public int AffectedNodeCount { get; set; }
            public int EvaluatedNodeCount { get; set; }
            public int FailedNodeCount { get; set; }
            public int CachedResultsUsed { get; set; }
            public TimeSpan EvaluationTime { get; set; }
            public TimeSpan TotalEvaluationTime { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public List<NodeId> EvaluationOrder { get; set; } = new();
            public Dictionary<NodeId, string>? Errors { get; set; }
        }

        /// <summary>
        /// Performance metrics for the evaluation engine
        /// </summary>
        public class EvaluationEngineMetrics
        {
            public int TotalEvaluations { get; set; }
            public int IncrementalEvaluations { get; set; }
            public double AverageEvaluationTime { get; set; }
            public double AverageIncrementalTime { get; set; }
            public int CacheHitRate { get; set; }
            public long TotalCacheHits { get; set; }
            public long TotalCacheMisses { get; set; }
            public TimeSpan Uptime { get; set; }
            public int CurrentDirtyNodes { get; set; }
            public int TotalNodes { get; set; }
        }

        /// <summary>
        /// Evaluation mode
        /// </summary>
        public enum EvaluationMode
        {
            Full,
            Incremental,
            SingleNode
        }

        /// <summary>
        /// Node state
        /// </summary>
        public enum NodeState
        {
            New,
            Clean,
            Dirty,
            Evaluated,
            Error
        }

        /// <summary>
        /// Dirty level for prioritization
        /// </summary>
        public enum DirtyLevel
        {
            None,
            Normal,
            High,
            Critical
        }

        #endregion
    }
}