using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace T3.Core.Operators.IncrementalEvaluation
{
    /// <summary>
    /// Performs topological sorting and optimal evaluation order calculation
    /// for node dependencies with support for incremental updates
    /// </summary>
    public class TopologicalEvaluator
    {
        #region Private Fields

        private readonly DependencyGraph _dependencyGraph;
        private readonly object _lockObject = new object();
        private readonly EvaluationCache _evaluationCache;

        #endregion

        #region Constructor

        public TopologicalEvaluator(DependencyGraph dependencyGraph)
        {
            _dependencyGraph = dependencyGraph ?? throw new ArgumentNullException(nameof(dependencyGraph));
            _evaluationCache = new EvaluationCache();
        }

        #endregion

        #region Topological Sort Methods

        /// <summary>
        /// Gets optimal evaluation order for all nodes using topological sort
        /// </summary>
        public List<NodeId> GetEvaluationOrder()
        {
            var timer = Stopwatch.StartNew();
            
            try
            {
                // Check cache first
                var cacheKey = new EvaluationCacheKey { NodeSubset = null, IncludeClean = true };
                if (_evaluationCache.TryGetOrder(cacheKey, out var cachedOrder))
                {
                    return cachedOrder;
                }

                var order = _dependencyGraph.GetTopologicalOrder();
                
                // Cache the result
                _evaluationCache.CacheOrder(cacheKey, order);
                
                return order;
            }
            finally
            {
                timer.Stop();
            }
        }

        /// <summary>
        /// Gets topological order for dirty nodes only
        /// </summary>
        public List<NodeId> GetEvaluationOrderForDirtyNodes(HashSet<NodeId> dirtyNodes)
        {
            if (dirtyNodes == null || !dirtyNodes.Any())
                return new List<NodeId>();

            var timer = Stopwatch.StartNew();
            
            try
            {
                // Check cache first
                var cacheKey = new EvaluationCacheKey 
                { 
                    NodeSubset = new HashSet<NodeId>(dirtyNodes), 
                    IncludeClean = false 
                };
                
                if (_evaluationCache.TryGetOrder(cacheKey, out var cachedOrder))
                {
                    return cachedOrder;
                }

                var order = GetTopologicalOrderForDirtyNodesInternal(dirtyNodes);
                
                // Cache the result
                _evaluationCache.CacheOrder(cacheKey, order);
                
                return order;
            }
            finally
            {
                timer.Stop();
            }
        }

        /// <summary>
        /// Gets topological order for a specific set of nodes
        /// </summary>
        public List<NodeId> GetEvaluationOrderForSpecificNodes(HashSet<NodeId> specificNodes)
        {
            if (specificNodes == null || !specificNodes.Any())
                return new List<NodeId>();

            var timer = Stopwatch.StartNew();
            
            try
            {
                // Check cache first
                var cacheKey = new EvaluationCacheKey 
                { 
                    NodeSubset = new HashSet<NodeId>(specificNodes), 
                    IncludeClean = true 
                };
                
                if (_evaluationCache.TryGetOrder(cacheKey, out var cachedOrder))
                {
                    return cachedOrder;
                }

                var order = GetTopologicalOrderForNodesInternal(specificNodes);
                
                // Cache the result
                _evaluationCache.CacheOrder(cacheKey, order);
                
                return order;
            }
            finally
            {
                timer.Stop();
            }
        }

        /// <summary>
        /// Gets optimal evaluation order with dependency validation
        /// </summary>
        public ValidatedEvaluationOrder GetValidatedEvaluationOrder()
        {
            var timer = Stopwatch.StartNew();
            var result = new ValidatedEvaluationOrder();
            
            try
            {
                // Get base order
                var baseOrder = GetEvaluationOrder();
                
                // Validate all dependencies are satisfied in order
                var validationErrors = new List<string>();
                var nodeIndex = new Dictionary<NodeId, int>();
                
                for (int i = 0; i < baseOrder.Count; i++)
                {
                    nodeIndex[baseOrder[i]] = i;
                }
                
                foreach (var nodeId in baseOrder)
                {
                    var dependencies = _dependencyGraph.GetDependencies(nodeId);
                    foreach (var dependency in dependencies)
                    {
                        if (!nodeIndex.TryGetValue(dependency, out var depIndex))
                        {
                            validationErrors.Add($"Dependency '{dependency}' for node '{nodeId}' not found in evaluation order");
                        }
                        else if (depIndex >= nodeIndex[nodeId])
                        {
                            validationErrors.Add($"Dependency '{dependency}' comes after node '{nodeId}' in evaluation order (circular dependency)");
                        }
                    }
                }

                result.Success = !validationErrors.Any();
                result.EvaluationOrder = baseOrder;
                result.ValidationErrors = validationErrors;
                result.TotalNodes = baseOrder.Count;
                result.ValidationTime = timer.Elapsed;
                
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.EvaluationOrder = new List<NodeId>();
                result.ValidationErrors = new List<string> { ex.Message };
                result.TotalNodes = 0;
                result.ValidationTime = timer.Elapsed;
                
                return result;
            }
            finally
            {
                timer.Stop();
            }
        }

        #endregion

        #region Analysis Methods

        /// <summary>
        /// Analyzes evaluation complexity for different strategies
        /// </summary>
        public EvaluationComplexityAnalysis AnalyzeEvaluationComplexity()
        {
            var timer = Stopwatch.StartNew();
            
            try
            {
                var analysis = new EvaluationComplexityAnalysis();
                
                var stats = _dependencyGraph.GetStatistics();
                analysis.GraphStatistics = stats;
                
                // Calculate evaluation complexity
                analysis.FullEvaluationComplexity = CalculateFullEvaluationComplexity(stats);
                analysis.IncrementalEvaluationComplexity = CalculateIncrementalEvaluationComplexity(stats);
                analysis.RecommendedStrategy = DetermineRecommendedStrategy(stats, analysis);
                
                // Performance predictions
                analysis.PredictedFullEvaluationTime = EstimateEvaluationTime(stats, "full");
                analysis.PredictedIncrementalEvaluationTime = EstimateEvaluationTime(stats, "incremental");
                analysis.PerformanceGain = CalculatePredictedPerformanceGain(analysis);
                
                return analysis;
            }
            finally
            {
                timer.Stop();
            }
        }

        /// <summary>
        /// Gets evaluation pipeline statistics
        /// </summary>
        public EvaluationPipelineStatistics GetPipelineStatistics()
        {
            lock (_lockObject)
            {
                var stats = _dependencyGraph.GetStatistics();
                
                return new EvaluationPipelineStatistics
                {
                    TotalNodes = stats.TotalNodes,
                    TotalDependencies = stats.TotalDependencies,
                    DependencyComplexity = stats.AverageDependenciesPerNode,
                    HasCircularDependencies = stats.HasCircularDependencies,
                    CachedOrders = _evaluationCache.GetCachedOrderCount(),
                    CacheHitRate = _evaluationCache.GetCacheHitRate(),
                    AverageTopologicalSortTime = _evaluationCache.GetAverageSortTime(),
                    MostComplexNode = GetMostComplexNode(),
                    CriticalPath = GetCriticalPath(),
                    BottleneckNodes = GetBottleneckNodes()
                };
            }
        }

        #endregion

        #region Optimization Methods

        /// <summary>
        /// Gets parallelizable evaluation groups
        /// </summary>
        public List<HashSet<NodeId>> GetParallelizableGroups()
        {
            var timer = Stopwatch.StartNew();
            
            try
            {
                var order = GetEvaluationOrder();
                var groups = new List<HashSet<NodeId>>();
                var currentLevel = new HashSet<NodeId>();
                var processedNodes = new HashSet<NodeId>();
                
                foreach (var nodeId in order)
                {
                    if (processedNodes.Contains(nodeId))
                        continue;
                    
                    // Find all nodes that can be evaluated at this level
                    var dependencies = _dependencyGraph.GetDependencies(nodeId);
                    var allDependenciesProcessed = dependencies.All(d => processedNodes.Contains(d));
                    
                    if (allDependenciesProcessed)
                    {
                        currentLevel.Add(nodeId);
                        processedNodes.Add(nodeId);
                        
                        // Check if this completes the level
                        var hasMoreNodesAtThisLevel = false;
                        foreach (var remainingNode in order.Where(n => !processedNodes.Contains(n)))
                        {
                            var remainingDependencies = _dependencyGraph.GetDependencies(remainingNode);
                            if (remainingDependencies.All(d => processedNodes.Contains(d)))
                            {
                                hasMoreNodesAtThisLevel = true;
                                break;
                            }
                        }
                        
                        if (!hasMoreNodesAtThisLevel)
                        {
                            groups.Add(new HashSet<NodeId>(currentLevel));
                            currentLevel.Clear();
                        }
                    }
                }
                
                return groups;
            }
            finally
            {
                timer.Stop();
            }
        }

        /// <summary>
        /// Optimizes evaluation order for minimal memory usage
        /// </summary>
        public List<NodeId> GetMemoryOptimizedEvaluationOrder()
        {
            // Start with basic topological order
            var baseOrder = GetEvaluationOrder();
            
            // Apply memory-aware optimizations
            var optimizedOrder = ApplyMemoryOptimizations(baseOrder);
            
            return optimizedOrder;
        }

        /// <summary>
        /// Gets evaluation order with load balancing for parallel processing
        /// </summary>
        public List<NodeId> GetLoadBalancedEvaluationOrder(int targetGroupSize = 10)
        {
            var timer = Stopwatch.StartNew();
            
            try
            {
                var parallelGroups = GetParallelizableGroups();
                var balancedOrder = new List<NodeId>();
                
                // Interleave groups for better load balancing
                var groupIndex = 0;
                var groups = parallelGroups.ToList();
                
                while (groups.Any())
                {
                    var currentGroup = groups[groupIndex % groups.Count];
                    balancedOrder.AddRange(currentGroup);
                    groups.RemoveAt(groupIndex % groups.Count);
                    groupIndex++;
                }
                
                return balancedOrder;
            }
            finally
            {
                timer.Stop();
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Internal implementation for dirty nodes topological sort
        /// </summary>
        private List<NodeId> GetTopologicalOrderForDirtyNodesInternal(HashSet<NodeId> dirtyNodes)
        {
            var dependencies = new Dictionary<NodeId, int>();
            var result = new List<NodeId>();
            var queue = new Queue<NodeId>();
            
            // Include dependencies of dirty nodes
            var relevantNodes = new HashSet<NodeId>(dirtyNodes);
            foreach (var nodeId in dirtyNodes)
            {
                var transitiveDeps = _dependencyGraph.GetTransitiveDependencies(nodeId);
                relevantNodes.UnionWith(transitiveDeps);
            }
            
            // Calculate in-degree for relevant nodes
            foreach (var nodeId in relevantNodes)
            {
                var incomingDeps = 0;
                var dependents = _dependencyGraph.GetDependents(nodeId);
                
                foreach (var dependent in dependents)
                {
                    if (relevantNodes.Contains(dependent))
                    {
                        incomingDeps++;
                    }
                }
                
                dependencies[nodeId] = incomingDeps;
            }
            
            // Start with nodes that have no dependencies within the relevant set
            foreach (var kvp in dependencies.Where(k => k.Value == 0))
            {
                queue.Enqueue(kvp.Key);
            }
            
            // Process nodes in topological order
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                result.Add(current);
                
                var outgoingDeps = _dependencyGraph.GetDependencies(current);
                foreach (var dep in outgoingDeps.Where(d => relevantNodes.Contains(d)))
                {
                    dependencies[dep]--;
                    if (dependencies[dep] == 0)
                    {
                        queue.Enqueue(dep);
                    }
                }
            }
            
            // Filter to only include dirty nodes
            return result.Where(n => dirtyNodes.Contains(n)).ToList();
        }

        /// <summary>
        /// Internal implementation for specific nodes topological sort
        /// </summary>
        private List<NodeId> GetTopologicalOrderForNodesInternal(HashSet<NodeId> specificNodes)
        {
            return _dependencyGraph.GetTopologicalOrderForNodes(specificNodes);
        }

        /// <summary>
        /// Calculates full evaluation complexity
        /// </summary>
        private EvaluationComplexity CalculateFullEvaluationComplexity(DependencyGraph.DependencyGraphStatistics stats)
        {
            return new EvaluationComplexity
            {
                TimeComplexity = "O(V + E)", // V = vertices, E = edges
                SpaceComplexity = "O(V)",
                NodeCount = stats.TotalNodes,
                DependencyCount = stats.TotalDependencies,
                EstimatedOperations = stats.TotalNodes + stats.TotalDependencies,
                Parallelizability = CalculateParallelizability(stats)
            };
        }

        /// <summary>
        /// Calculates incremental evaluation complexity
        /// </summary>
        private EvaluationComplexity CalculateIncrementalEvaluationComplexity(DependencyGraph.DependencyGraphStatistics stats)
        {
            var avgAffectedNodes = Math.Sqrt(stats.TotalNodes); // Assuming sqrt relationship
            var avgAffectedDependencies = avgAffectedNodes * stats.AverageDependenciesPerNode;
            
            return new EvaluationComplexity
            {
                TimeComplexity = "O(k + f(k))", // k = changed nodes, f(k) = affected nodes
                SpaceComplexity = "O(k + f(k))",
                NodeCount = (int)avgAffectedNodes,
                DependencyCount = (int)avgAffectedDependencies,
                EstimatedOperations = avgAffectedNodes + avgAffectedDependencies,
                Parallelizability = CalculateParallelizability(stats) * 0.8 // Slightly less parallelizable
            };
        }

        /// <summary>
        /// Calculates parallelizability factor
        /// </summary>
        private double CalculateParallelizability(DependencyGraph.DependencyGraphStatistics stats)
        {
            // Higher value = more parallelizable
            if (stats.TotalNodes == 0) return 1.0;
            
            var avgDeps = stats.AverageDependenciesPerNode;
            var dependencyRatio = avgDeps / stats.TotalNodes;
            
            // More independent nodes = higher parallelizability
            return Math.Max(0.1, 1.0 - dependencyRatio);
        }

        /// <summary>
        /// Determines recommended evaluation strategy
        /// </summary>
        private EvaluationStrategy DetermineRecommendedStrategy(
            DependencyGraph.DependencyGraphStatistics stats, 
            EvaluationComplexityAnalysis analysis)
        {
            if (stats.TotalNodes < 50)
                return EvaluationStrategy.Full;
            
            if (stats.AverageDependenciesPerNode > stats.TotalNodes * 0.5)
                return EvaluationStrategy.Incremental;
            
            if (analysis.PerformanceGain > 0.7)
                return EvaluationStrategy.Incremental;
            
            return EvaluationStrategy.Adaptive;
        }

        /// <summary>
        /// Estimates evaluation time based on graph complexity
        /// </summary>
        private TimeSpan EstimateEvaluationTime(DependencyGraph.DependencyGraphStatistics stats, string strategy)
        {
            var baseTimePerNode = strategy == "incremental" ? 0.1 : 1.0; // milliseconds
            var nodeCount = strategy == "incremental" ? Math.Sqrt(stats.TotalNodes) : stats.TotalNodes;
            
            return TimeSpan.FromMilliseconds(nodeCount * baseTimePerNode);
        }

        /// <summary>
        /// Calculates predicted performance gain
        /// </summary>
        private double CalculatePredictedPerformanceGain(EvaluationComplexityAnalysis analysis)
        {
            var fullTime = analysis.PredictedFullEvaluationTime.TotalMilliseconds;
            var incrementalTime = analysis.PredictedIncrementalEvaluationTime.TotalMilliseconds;
            
            if (fullTime <= 0) return 0;
            
            return (fullTime - incrementalTime) / fullTime;
        }

        /// <summary>
        /// Applies memory optimizations to evaluation order
        /// </summary>
        private List<NodeId> ApplyMemoryOptimizations(List<NodeId> baseOrder)
        {
            // Simple heuristic: evaluate high-dependency nodes first to free memory sooner
            var nodeMemoryWeights = new Dictionary<NodeId, double>();
            
            foreach (var nodeId in baseOrder)
            {
                var dependencies = _dependencyGraph.GetDependencies(nodeId).Count;
                var dependents = _dependencyGraph.GetDependents(nodeId).Count;
                
                // Weight nodes that free up more memory (have many dependents) higher
                nodeMemoryWeights[nodeId] = dependents + dependencies * 0.1;
            }
            
            return baseOrder.OrderByDescending(n => nodeMemoryWeights.GetValueOrDefault(n, 0)).ToList();
        }

        /// <summary>
        /// Gets the most complex node
        /// </summary>
        private NodeId GetMostComplexNode()
        {
            var stats = _dependencyGraph.GetStatistics();
            
            if (stats.NodesWithMostDependencies.Any())
            {
                return stats.NodesWithMostDependencies.First();
            }
            
            return null;
        }

        /// <summary>
        /// Gets critical path (longest evaluation chain)
        /// </summary>
        private List<NodeId> GetCriticalPath()
        {
            var order = GetEvaluationOrder();
            var criticalPath = new List<NodeId>();
            var maxDepth = 0;
            
            foreach (var nodeId in order)
            {
                var dependencies = _dependencyGraph.GetDependencies(nodeId);
                var maxDepDepth = dependencies.Count > 0 
                    ? dependencies.Max(dep => GetNodeDepth(dep, criticalPath))
                    : 0;
                
                var currentDepth = maxDepDepth + 1;
                if (currentDepth > maxDepth)
                {
                    maxDepth = currentDepth;
                    criticalPath.Clear();
                    criticalPath.Add(nodeId);
                }
                else if (currentDepth == maxDepth)
                {
                    criticalPath.Add(nodeId);
                }
            }
            
            return criticalPath;
        }

        /// <summary>
        /// Gets bottleneck nodes (nodes with many dependents)
        /// </summary>
        private List<NodeId> GetBottleneckNodes()
        {
            var stats = _dependencyGraph.GetStatistics();
            return stats.NodesWithMostDependents ?? new List<NodeId>();
        }

        /// <summary>
        /// Calculates depth of a node in the critical path
        /// </summary>
        private int GetNodeDepth(NodeId nodeId, List<NodeId> processedNodes)
        {
            if (!processedNodes.Contains(nodeId))
                return 0;
            
            var dependencies = _dependencyGraph.GetDependencies(nodeId);
            if (!dependencies.Any())
                return 1;
            
            return dependencies.Max(dep => GetNodeDepth(dep, processedNodes)) + 1;
        }

        #endregion

        #region Supporting Classes

        /// <summary>
        /// Cache key for evaluation orders
        /// </summary>
        private class EvaluationCacheKey
        {
            public HashSet<NodeId> NodeSubset { get; set; }
            public bool IncludeClean { get; set; }

            public override bool Equals(object obj)
            {
                if (obj is not EvaluationCacheKey other)
                    return false;

                var subsetMatch = (NodeSubset == null && other.NodeSubset == null) ||
                                 (NodeSubset?.SetEquals(other.NodeSubset) == true);

                return subsetMatch && IncludeClean == other.IncludeClean;
            }

            public override int GetHashCode()
            {
                var hash = IncludeClean.GetHashCode();
                if (NodeSubset != null)
                {
                    foreach (var nodeId in NodeSubset.OrderBy(n => n.Id))
                    {
                        hash = hash * 31 + nodeId.GetHashCode();
                    }
                }
                return hash;
            }
        }

        /// <summary>
        /// Cache for evaluation orders
        /// </summary>
        private class EvaluationCache
        {
            private readonly Dictionary<EvaluationCacheKey, CachedOrder> _cache = new();
            private readonly object _lockObject = new object();

            public bool TryGetOrder(EvaluationCacheKey key, out List<NodeId> order)
            {
                lock (_lockObject)
                {
                    if (_cache.TryGetValue(key, out var cached))
                    {
                        order = cached.Order;
                        cached.LastAccessed = DateTime.UtcNow;
                        return true;
                    }
                    
                    order = null;
                    return false;
                }
            }

            public void CacheOrder(EvaluationCacheKey key, List<NodeId> order)
            {
                lock (_lockObject)
                {
                    _cache[key] = new CachedOrder 
                    { 
                        Order = new List<NodeId>(order),
                        CachedAt = DateTime.UtcNow,
                        LastAccessed = DateTime.UtcNow
                    };
                }
            }

            public int GetCachedOrderCount()
            {
                lock (_lockObject)
                {
                    return _cache.Count;
                }
            }

            public double GetCacheHitRate()
            {
                // Simplified - in real implementation would track hits/misses
                return 0.8; // Mock value
            }

            public double GetAverageSortTime()
            {
                // Mock implementation - would track actual sort times
                return 1.0; // milliseconds
            }

            private class CachedOrder
            {
                public List<NodeId> Order { get; set; }
                public DateTime CachedAt { get; set; }
                public DateTime LastAccessed { get; set; }
            }
        }

        #endregion

        #region Public Result Classes

        /// <summary>
        /// Validated evaluation order with errors
        /// </summary>
        public class ValidatedEvaluationOrder
        {
            public bool Success { get; set; }
            public List<NodeId> EvaluationOrder { get; set; } = new();
            public List<string> ValidationErrors { get; set; } = new();
            public int TotalNodes { get; set; }
            public TimeSpan ValidationTime { get; set; }
        }

        /// <summary>
        /// Evaluation complexity analysis
        /// </summary>
        public class EvaluationComplexityAnalysis
        {
            public DependencyGraph.DependencyGraphStatistics GraphStatistics { get; set; }
            public EvaluationComplexity FullEvaluationComplexity { get; set; }
            public EvaluationComplexity IncrementalEvaluationComplexity { get; set; }
            public EvaluationStrategy RecommendedStrategy { get; set; }
            public TimeSpan PredictedFullEvaluationTime { get; set; }
            public TimeSpan PredictedIncrementalEvaluationTime { get; set; }
            public double PerformanceGain { get; set; }
        }

        /// <summary>
        /// Evaluation complexity metrics
        /// </summary>
        public class EvaluationComplexity
        {
            public string TimeComplexity { get; set; }
            public string SpaceComplexity { get; set; }
            public int NodeCount { get; set; }
            public int DependencyCount { get; set; }
            public double EstimatedOperations { get; set; }
            public double Parallelizability { get; set; } // 0.0 - 1.0
        }

        /// <summary>
        /// Evaluation strategy recommendation
        /// </summary>
        public enum EvaluationStrategy
        {
            Full,
            Incremental,
            Adaptive
        }

        /// <summary>
        /// Pipeline statistics for evaluation
        /// </summary>
        public class EvaluationPipelineStatistics
        {
            public int TotalNodes { get; set; }
            public int TotalDependencies { get; set; }
            public double DependencyComplexity { get; set; }
            public bool HasCircularDependencies { get; set; }
            public int CachedOrders { get; set; }
            public double CacheHitRate { get; set; }
            public double AverageTopologicalSortTime { get; set; }
            public NodeId MostComplexNode { get; set; }
            public List<NodeId> CriticalPath { get; set; } = new();
            public List<NodeId> BottleneckNodes { get; set; } = new();
        }

        #endregion
    }
}