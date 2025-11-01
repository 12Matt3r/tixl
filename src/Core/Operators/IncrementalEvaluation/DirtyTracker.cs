using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace T3.Core.Operators.IncrementalEvaluation
{
    /// <summary>
    /// Manages dirty flags for nodes that need re-evaluation
    /// Provides efficient tracking of which nodes have changed and need updates
    /// </summary>
    public class DirtyTracker
    {
        #region Private Fields

        private readonly Dictionary<NodeId, NodeDirtyState> _nodeStates;
        private readonly Dictionary<NodeId, HashSet<NodeId>> _dependencies;
        private readonly Dictionary<NodeId, NodeRegion> _nodeRegions;
        private readonly object _lockObject = new object();
        
        // Performance tracking
        private readonly DirtyMetrics _metrics = new();
        
        // Configuration
        private readonly int _maxRegionsPerBatch = 100;
        private readonly TimeSpan _defaultRegionTtl = TimeSpan.FromMinutes(10);

        #endregion

        #region Constructor

        public DirtyTracker()
        {
            _nodeStates = new Dictionary<NodeId, NodeDirtyState>();
            _dependencies = new Dictionary<NodeId, HashSet<NodeId>>();
            _nodeRegions = new Dictionary<NodeId, NodeRegion>();
        }

        #endregion

        #region Node Registration

        /// <summary>
        /// Registers a node for dirty tracking
        /// </summary>
        public void RegisterNode(NodeId nodeId)
        {
            if (nodeId == null)
                throw new ArgumentNullException(nameof(nodeId));

            lock (_lockObject)
            {
                if (!_nodeStates.ContainsKey(nodeId))
                {
                    _nodeStates[nodeId] = new NodeDirtyState
                    {
                        IsDirty = true, // Newly registered nodes start as dirty
                        LastModified = DateTime.UtcNow,
                        DirtyLevel = DirtyLevel.Normal
                    };
                }

                if (!_dependencies.ContainsKey(nodeId))
                {
                    _dependencies[nodeId] = new HashSet<NodeId>();
                }

                _metrics.NodeRegistered();
            }
        }

        /// <summary>
        /// Unregisters a node from dirty tracking
        /// </summary>
        public void UnregisterNode(NodeId nodeId)
        {
            if (nodeId == null)
                return;

            lock (_lockObject)
            {
                _nodeStates.Remove(nodeId);
                _dependencies.Remove(nodeId);
                _nodeRegions.Remove(nodeId);

                // Remove from all dependency lists
                foreach (var depList in _dependencies.Values)
                {
                    depList.Remove(nodeId);
                }

                _metrics.NodeUnregistered();
            }
        }

        /// <summary>
        /// Registers a dependency relationship for dirty tracking
        /// </summary>
        public void RegisterDependency(NodeId from, NodeId to)
        {
            if (from == null || to == null)
                throw new ArgumentNullException();

            lock (_lockObject)
            {
                if (!_dependencies.ContainsKey(from))
                {
                    _dependencies[from] = new HashSet<NodeId>();
                }

                _dependencies[from].Add(to);

                _metrics.DependencyRegistered();
            }
        }

        /// <summary>
        /// Unregisters a dependency relationship
        /// </summary>
        public void UnregisterDependency(NodeId from, NodeId to)
        {
            if (from == null || to == null)
                return;

            lock (_lockObject)
            {
                if (_dependencies.TryGetValue(from, out var deps))
                {
                    deps.Remove(to);
                }

                _metrics.DependencyUnregistered();
            }
        }

        #endregion

        #region Dirty Flag Management

        /// <summary>
        /// Marks a node as dirty
        /// </summary>
        public void MarkDirty(NodeId nodeId)
        {
            MarkDirty(nodeId, DirtyLevel.Normal);
        }

        /// <summary>
        /// Marks a node as dirty with a specific level
        /// </summary>
        public void MarkDirty(NodeId nodeId, DirtyLevel level)
        {
            if (nodeId == null)
                return;

            lock (_lockObject)
            {
                if (!_nodeStates.TryGetValue(nodeId, out var state))
                {
                    // Auto-register if not found
                    RegisterNode(nodeId);
                    state = _nodeStates[nodeId];
                }

                var wasDirty = state.IsDirty;
                state.IsDirty = true;
                state.LastModified = DateTime.UtcNow;
                state.DirtyLevel = level;

                if (!wasDirty)
                {
                    _metrics.NodeMarkedDirty(nodeId, level);
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

            lock (_lockObject)
            {
                var nodesList = nodeIds.ToList();
                var newlyDirty = 0;

                foreach (var nodeId in nodesList)
                {
                    if (!_nodeStates.TryGetValue(nodeId, out var state))
                    {
                        RegisterNode(nodeId);
                        state = _nodeStates[nodeId];
                    }

                    if (!state.IsDirty)
                    {
                        newlyDirty++;
                    }

                    state.IsDirty = true;
                    state.LastModified = DateTime.UtcNow;
                    state.DirtyLevel = level;
                }

                if (newlyDirty > 0)
                {
                    _metrics.BatchMarkedDirty(nodesList.Count, newlyDirty);
                }
            }
        }

        /// <summary>
        /// Marks a node and all its dependent nodes as dirty
        /// </summary>
        public void MarkDirtyWithDependents(NodeId nodeId, DirtyLevel level = DirtyLevel.Normal)
        {
            if (nodeId == null)
                return;

            MarkDirty(nodeId, level);

            // Find all transitive dependents and mark them dirty
            var dependents = FindAllDependents(nodeId);
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

            lock (_lockObject)
            {
                if (_nodeStates.TryGetValue(nodeId, out var state))
                {
                    if (state.IsDirty)
                    {
                        state.IsDirty = false;
                        state.LastEvaluated = DateTime.UtcNow;
                        _metrics.NodeCleaned(nodeId);
                    }
                }
            }
        }

        /// <summary>
        /// Clears dirty flags for multiple nodes efficiently
        /// </summary>
        public void ClearDirtyBatch(IEnumerable<NodeId> nodeIds)
        {
            if (nodeIds == null)
                return;

            lock (_lockObject)
            {
                var nodesList = nodeIds.ToList();
                var cleaned = 0;

                foreach (var nodeId in nodesList)
                {
                    if (_nodeStates.TryGetValue(nodeId, out var state) && state.IsDirty)
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
            lock (_lockObject)
            {
                var dirtyCount = GetDirtyNodeCount();
                
                foreach (var kvp in _nodeStates)
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

        #endregion

        #region Dirty State Queries

        /// <summary>
        /// Checks if a specific node is dirty
        /// </summary>
        public bool IsDirty(NodeId nodeId)
        {
            if (nodeId == null)
                return false;

            lock (_lockObject)
            {
                return _nodeStates.TryGetValue(nodeId, out var state) && state.IsDirty;
            }
        }

        /// <summary>
        /// Gets all dirty nodes
        /// </summary>
        public HashSet<NodeId> GetDirtyNodes()
        {
            lock (_lockObject)
            {
                return new HashSet<NodeId>(_nodeStates.Where(kvp => kvp.Value.IsDirty).Select(kvp => kvp.Key));
            }
        }

        /// <summary>
        /// Gets dirty nodes with their levels
        /// </summary>
        public Dictionary<NodeId, NodeDirtyState> GetDirtyNodesWithState()
        {
            lock (_lockObject)
            {
                return _nodeStates.Where(kvp => kvp.Value.IsDirty)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }
        }

        /// <summary>
        /// Gets the number of dirty nodes
        /// </summary>
        public int GetDirtyNodeCount()
        {
            lock (_lockObject)
            {
                return _nodeStates.Count(kvp => kvp.Value.IsDirty);
            }
        }

        /// <summary>
        /// Gets nodes dirty for more than a specified time
        /// </summary>
        public HashSet<NodeId> GetStaleDirtyNodes(TimeSpan maxAge)
        {
            var cutoff = DateTime.UtcNow - maxAge;
            
            lock (_lockObject)
            {
                return new HashSet<NodeId>(_nodeStates
                    .Where(kvp => kvp.Value.IsDirty && kvp.Value.LastModified < cutoff)
                    .Select(kvp => kvp.Key));
            }
        }

        /// <summary>
        /// Gets dirty nodes by level
        /// </summary>
        public HashSet<NodeId> GetDirtyNodesByLevel(DirtyLevel level)
        {
            lock (_lockObject)
            {
                return new HashSet<NodeId>(_nodeStates
                    .Where(kvp => kvp.Value.IsDirty && kvp.Value.DirtyLevel == level)
                    .Select(kvp => kvp.Key));
            }
        }

        #endregion

        #region Region Management

        /// <summary>
        /// Registers a node's visual region for dirty tracking
        /// </summary>
        public void RegisterNodeRegion(NodeId nodeId, NodeRegion region)
        {
            if (nodeId == null || region == null)
                return;

            lock (_lockObject)
            {
                _nodeRegions[nodeId] = region;
                _metrics.RegionRegistered();
            }
        }

        /// <summary>
        /// Marks a region as dirty
        /// </summary>
        public void MarkRegionDirty(NodeRegion region)
        {
            if (region == null)
                return;

            lock (_lockObject)
            {
                _metrics.RegionMarkedDirty(region.Area);
                
                // Invalidate any cached region data
                // This would be used by UI systems for selective redrawing
            }
        }

        /// <summary>
        /// Gets dirty regions that intersect with a viewport
        /// </summary>
        public List<NodeRegion> GetDirtyRegionsInViewport(Rect viewport)
        {
            var dirtyRegions = new List<NodeRegion>();

            lock (_lockObject)
            {
                foreach (var kvp in _nodeRegions)
                {
                    if (kvp.Value != null && IsNodeDirty(kvp.Key) && kvp.Value.Bounds.IntersectsWith(viewport))
                    {
                        dirtyRegions.Add(kvp.Value);
                    }
                }
            }

            _metrics.RecordRegionQuery(dirtyRegions.Count);
            return dirtyRegions;
        }

        /// <summary>
        /// Merges overlapping dirty regions for efficient rendering
        /// </summary>
        public List<NodeRegion> GetMergedDirtyRegions(Rect viewport)
        {
            var dirtyRegions = GetDirtyRegionsInViewport(viewport);
            
            if (dirtyRegions.Count <= 1)
                return dirtyRegions;

            var merged = MergeOverlappingRegions(dirtyRegions);
            _metrics.RecordRegionMerge(dirtyRegions.Count, merged.Count);
            
            return merged;
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Checks if a node is dirty (thread-safe version)
        /// </summary>
        private bool IsNodeDirty(NodeId nodeId)
        {
            return _nodeStates.TryGetValue(nodeId, out var state) && state.IsDirty;
        }

        /// <summary>
        /// Finds all transitive dependents of a node
        /// </summary>
        private HashSet<NodeId> FindAllDependents(NodeId nodeId)
        {
            var visited = new HashSet<NodeId>();
            var result = new HashSet<NodeId>();
            var queue = new Queue<NodeId>();

            queue.Enqueue(nodeId);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                
                foreach (var dependent in _dependencies.Where(d => d.Value.Contains(current)).Select(d => d.Key))
                {
                    if (!visited.Contains(dependent))
                    {
                        visited.Add(dependent);
                        result.Add(dependent);
                        queue.Enqueue(dependent);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Merges overlapping regions efficiently
        /// </summary>
        private List<NodeRegion> MergeOverlappingRegions(List<NodeRegion> regions)
        {
            if (regions.Count <= 1)
                return regions;

            var merged = new List<NodeRegion>();
            var processed = new HashSet<NodeRegion>();

            foreach (var region in regions)
            {
                if (processed.Contains(region))
                    continue;

                var currentMerged = new List<NodeRegion> { region };
                processed.Add(region);

                // Find overlapping regions
                foreach (var other in regions)
                {
                    if (processed.Contains(other))
                        continue;

                    if (currentMerged.Any(r => r.Bounds.IntersectsWith(other.Bounds)))
                    {
                        currentMerged.Add(other);
                        processed.Add(other);
                    }
                }

                // Merge into single region
                var mergedBounds = currentMerged.First().Bounds;
                foreach (var r in currentMerged)
                {
                    mergedBounds = Rect.Union(mergedBounds, r.Bounds);
                }

                merged.Add(new NodeRegion
                {
                    Bounds = mergedBounds,
                    Area = mergedBounds.Width * mergedBounds.Height,
                    Priority = currentMerged.Max(r => r.Priority)
                });
            }

            return merged;
        }

        #endregion

        #region Statistics and Monitoring

        /// <summary>
        /// Gets comprehensive statistics about dirty tracking
        /// </summary>
        public DirtyTrackerStatistics GetStatistics()
        {
            lock (_lockObject)
            {
                return new DirtyTrackerStatistics
                {
                    TotalRegisteredNodes = _nodeStates.Count,
                    DirtyNodeCount = GetDirtyNodeCount(),
                    CleanNodeCount = _nodeStates.Count - GetDirtyNodeCount(),
                    TotalDependencies = _dependencies.Values.Sum(d => d.Count),
                    AverageDependenciesPerNode = _dependencies.Any() 
                        ? (double)_dependencies.Values.Sum(d => d.Count) / _dependencies.Count 
                        : 0,
                    RegisteredRegions = _nodeRegions.Count,
                    DirtyLevelCounts = GetDirtyLevelCounts(),
                    AverageDirtyDuration = CalculateAverageDirtyDuration(),
                    StaleDirtyNodes = GetStaleDirtyNodes(TimeSpan.FromMinutes(1)).Count
                };
            }
        }

        /// <summary>
        /// Gets performance metrics
        /// </summary>
        public DirtyMetrics GetMetrics()
        {
            return _metrics;
        }

        /// <summary>
        /// Resets all statistics
        /// </summary>
        public void ResetMetrics()
        {
            _metrics.Reset();
        }

        private Dictionary<DirtyLevel, int> GetDirtyLevelCounts()
        {
            lock (_lockObject)
            {
                return _nodeStates.Where(kvp => kvp.Value.IsDirty)
                    .GroupBy(kvp => kvp.Value.DirtyLevel)
                    .ToDictionary(g => g.Key, g => g.Count());
            }
        }

        private TimeSpan CalculateAverageDirtyDuration()
        {
            lock (_lockObject)
            {
                var dirtyDurations = _nodeStates
                    .Where(kvp => kvp.Value.IsDirty)
                    .Select(kvp => DateTime.UtcNow - kvp.Value.LastModified);

                return dirtyDurations.Any() 
                    ? TimeSpan.FromTicks((long)dirtyDurations.Average(d => d.Ticks))
                    : TimeSpan.Zero;
            }
        }

        #endregion

        #region Supporting Classes

        /// <summary>
        /// Represents the dirty state of a node
        /// </summary>
        public class NodeDirtyState
        {
            public bool IsDirty { get; set; }
            public DirtyLevel DirtyLevel { get; set; }
            public DateTime LastModified { get; set; }
            public DateTime LastEvaluated { get; set; }
            public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
        }

        /// <summary>
        /// Represents a visual region for a node
        /// </summary>
        public class NodeRegion
        {
            public Rect Bounds { get; set; }
            public double Area => Bounds.Width * Bounds.Height;
            public int Priority { get; set; } = 0; // Higher priority = rendered first
        }

        /// <summary>
        /// Rectangle structure for region calculations
        /// </summary>
        public struct Rect
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }

            public static Rect Union(Rect a, Rect b)
            {
                var x = Math.Min(a.X, b.X);
                var y = Math.Min(a.Y, b.Y);
                var width = Math.Max(a.X + a.Width, b.X + b.Width) - x;
                var height = Math.Max(a.Y + a.Height, b.Y + b.Height) - y;

                return new Rect { X = x, Y = y, Width = width, Height = height };
            }

            public bool IntersectsWith(Rect other)
            {
                return !(X + Width <= other.X || other.X + other.Width <= X ||
                         Y + Height <= other.Y || other.Y + other.Height <= Y);
            }
        }

        /// <summary>
        /// Level of dirtiness for prioritization
        /// </summary>
        public enum DirtyLevel
        {
            None,       // Not dirty
            Normal,     // Standard dirty state
            High,       // High priority dirty (critical changes)
            Critical    // Critical dirty (immediate attention needed)
        }

        /// <summary>
        /// Statistics for dirty tracker performance
        /// </summary>
        public class DirtyTrackerStatistics
        {
            public int TotalRegisteredNodes { get; set; }
            public int DirtyNodeCount { get; set; }
            public int CleanNodeCount { get; set; }
            public int TotalDependencies { get; set; }
            public double AverageDependenciesPerNode { get; set; }
            public int RegisteredRegions { get; set; }
            public Dictionary<DirtyLevel, int> DirtyLevelCounts { get; set; }
            public TimeSpan AverageDirtyDuration { get; set; }
            public int StaleDirtyNodes { get; set; }
        }

        /// <summary>
        /// Performance metrics for dirty tracking operations
        /// </summary>
        public class DirtyMetrics
        {
            private int _nodesRegistered = 0;
            private int _nodesUnregistered = 0;
            private int _dependenciesRegistered = 0;
            private int _dependenciesUnregistered = 0;
            private int _nodesMarkedDirty = 0;
            private int _nodesCleaned = 0;
            private int _regionsRegistered = 0;
            private int _regionsMarkedDirty = 0;
            private int _regionQueries = 0;
            private int _regionsMerged = 0;
            private int _totalOriginalRegions = 0;
            private readonly List<double> _markTimes = new();
            private readonly List<double> _clearTimes = new();

            public void NodeRegistered() => _nodesRegistered++;
            public void NodeUnregistered() => _nodesUnregistered++;
            public void DependencyRegistered() => _dependenciesRegistered++;
            public void DependencyUnregistered() => _dependenciesUnregistered++;
            public void NodeMarkedDirty(NodeId nodeId, DirtyLevel level) => _nodesMarkedDirty++;
            public void NodeCleaned(NodeId nodeId) => _nodesCleaned++;
            public void RegionRegistered() => _regionsRegistered++;
            public void RegionMarkedDirty(double area) => _regionsMarkedDirty++;
            public void RecordRegionQuery(int regionCount) => _regionQueries++;
            public void RecordRegionMerge(int originalCount, int mergedCount)
            {
                _regionsMerged += mergedCount;
                _totalOriginalRegions += originalCount;
            }

            public void BatchMarkedDirty(int totalCount, int newlyDirtyCount)
            {
                _nodesMarkedDirty += newlyDirtyCount;
                _markTimes.Add(newlyDirtyCount);
            }

            public void BatchCleaned(int cleanedCount)
            {
                _nodesCleaned += cleanedCount;
                _clearTimes.Add(cleanedCount);
            }

            public void MarkedWithDependents(int count)
            {
                _nodesMarkedDirty += count;
            }

            public void AllDirtyCleared(int clearedCount)
            {
                _nodesCleaned += clearedCount;
                _clearTimes.Add(clearedCount);
            }

            public void Reset()
            {
                _nodesRegistered = 0;
                _nodesUnregistered = 0;
                _dependenciesRegistered = 0;
                _dependenciesUnregistered = 0;
                _nodesMarkedDirty = 0;
                _nodesCleaned = 0;
                _regionsRegistered = 0;
                _regionsMarkedDirty = 0;
                _regionQueries = 0;
                _regionsMerged = 0;
                _totalOriginalRegions = 0;
                _markTimes.Clear();
                _clearTimes.Clear();
            }

            public DirtyTrackerPerformanceMetrics GetPerformanceMetrics()
            {
                return new DirtyTrackerPerformanceMetrics
                {
                    TotalNodesRegistered = _nodesRegistered,
                    TotalNodesUnregistered = _nodesUnregistered,
                    TotalDependenciesRegistered = _dependenciesRegistered,
                    TotalDependenciesUnregistered = _dependenciesUnregistered,
                    TotalNodesMarkedDirty = _nodesMarkedDirty,
                    TotalNodesCleaned = _nodesCleaned,
                    TotalRegionsRegistered = _regionsRegistered,
                    TotalRegionsMarkedDirty = _regionsMarkedDirty,
                    TotalRegionQueries = _regionQueries,
                    TotalRegionsMerged = _regionsMerged,
                    AverageMarkBatchSize = _markTimes.Any() ? _markTimes.Average() : 0,
                    AverageClearBatchSize = _clearTimes.Any() ? _clearTimes.Average() : 0,
                    RegionMergeEfficiency = _regionsMerged > 0 ? (double)_totalOriginalRegions / _regionsMerged : 1.0
                };
            }
        }

        /// <summary>
        /// Performance metrics for dirty tracker
        /// </summary>
        public class DirtyTrackerPerformanceMetrics
        {
            public int TotalNodesRegistered { get; set; }
            public int TotalNodesUnregistered { get; set; }
            public int TotalDependenciesRegistered { get; set; }
            public int TotalDependenciesUnregistered { get; set; }
            public int TotalNodesMarkedDirty { get; set; }
            public int TotalNodesCleaned { get; set; }
            public int TotalRegionsRegistered { get; set; }
            public int TotalRegionsMarkedDirty { get; set; }
            public int TotalRegionQueries { get; set; }
            public int TotalRegionsMerged { get; set; }
            public double AverageMarkBatchSize { get; set; }
            public double AverageClearBatchSize { get; set; }
            public double RegionMergeEfficiency { get; set; }
        }

        #endregion
    }
}