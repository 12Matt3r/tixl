using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using T3.Core.Logging;

namespace T3.Core.Operators.IncrementalEvaluation
{
    /// <summary>
    /// Multi-level caching system for node evaluation results
    /// Provides intelligent caching with dependency tracking and LRU eviction
    /// </summary>
    public class CacheManager : IDisposable
    {
        #region Private Fields

        private readonly LRUCache<NodeId, CachedNodeResult> _resultCache;
        private readonly Dictionary<NodeId, NodeSignature> _nodeSignatures;
        private readonly DependencyTracker _dependencyTracker;
        private readonly CacheMetrics _metrics;
        
        // Configuration
        private readonly int _maxCacheSize;
        private readonly TimeSpan _defaultTtl;
        private bool _disposed = false;

        #endregion

        #region Constructor

        public CacheManager(int maxCacheSize = 10000, TimeSpan defaultTtl = default)
        {
            _maxCacheSize = maxCacheSize;
            _defaultTtl = defaultTtl == default ? TimeSpan.FromMinutes(5) : defaultTtl;
            
            _resultCache = new LRUCache<NodeId, CachedNodeResult>(maxCacheSize);
            _nodeSignatures = new Dictionary<NodeId, NodeSignature>();
            _dependencyTracker = new DependencyTracker();
            _metrics = new CacheMetrics();
        }

        #endregion

        #region Cache Operations

        /// <summary>
        /// Gets cached result for a node if available and valid
        /// </summary>
        public CacheResult GetCachedResult(NodeId nodeId, NodeSignature signature)
        {
            if (nodeId == null || signature == null)
                return new CacheResult { Hit = false };

            var timer = Stopwatch.StartNew();
            
            try
            {
                // Check if signature has changed
                if (HasSignatureChanged(nodeId, signature))
                {
                    InvalidateNode(nodeId);
                    _metrics.RecordCacheMiss(CacheMissReason.SignatureChanged);
                    return new CacheResult { Hit = false };
                }

                // Try to get from cache
                if (_resultCache.TryGetValue(nodeId, out var cachedResult))
                {
                    // Check if result is still valid
                    if (IsResultValid(cachedResult))
                    {
                        _metrics.RecordCacheHit();
                        timer.Stop();
                        
                        return new CacheResult 
                        { 
                            Hit = true, 
                            Result = cachedResult.Result,
                            RetrievalTime = timer.Elapsed.TotalMilliseconds,
                            CachedAt = cachedResult.CachedAt
                        };
                    }
                    else
                    {
                        // Remove expired result
                        _resultCache.Remove(nodeId);
                        _metrics.RecordCacheMiss(CacheMissReason.Expired);
                    }
                }
                else
                {
                    _metrics.RecordCacheMiss(CacheMissReason.NotCached);
                }

                return new CacheResult { Hit = false };
            }
            catch (Exception ex)
            {
                // Log error and treat as cache miss
                Console.WriteLine($"Error retrieving cached result for node {nodeId}: {ex.Message}");
                _metrics.RecordCacheMiss(CacheMissReason.Error);
                return new CacheResult { Hit = false };
            }
            finally
            {
                timer.Stop();
            }
        }

        /// <summary>
        /// Stores evaluation result in cache
        /// </summary>
        public void CacheResult(NodeId nodeId, object result, NodeSignature signature)
        {
            if (nodeId == null || signature == null)
                return;

            var timer = Stopwatch.StartNew();
            
            try
            {
                // Update node signature
                _nodeSignatures[nodeId] = signature;
                
                // Create cached result
                var cachedResult = new CachedNodeResult
                {
                    Result = result,
                    Signature = signature,
                    CachedAt = DateTime.UtcNow,
                    Size = EstimateResultSize(result)
                };

                // Add to cache
                _resultCache.Add(nodeId, cachedResult);
                
                // Update dependency tracking
                _dependencyTracker.RecordEvaluation(nodeId, signature);
                
                _metrics.RecordCacheWrite();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error caching result for node {nodeId}: {ex.Message}");
                // Continue execution even if caching fails
            }
            finally
            {
                timer.Stop();
            }
        }

        /// <summary>
        /// Invalidates cache for specific node and all its dependents
        /// </summary>
        public void InvalidateNodeAndDependents(NodeId nodeId)
        {
            if (nodeId == null)
                return;

            var timer = Stopwatch.StartNew();
            
            try
            {
                var affectedNodes = new HashSet<NodeId> { nodeId };
                
                // Find all nodes that depend on this node
                var dependents = _dependencyTracker.GetDependents(nodeId);
                foreach (var dependent in dependents)
                {
                    InvalidateNodeAndDependentsInternal(dependent, affectedNodes);
                }
                
                _metrics.RecordInvalidation(affectedNodes.Count);
            }
            finally
            {
                timer.Stop();
            }
        }

        /// <summary>
        /// Invalidates specific node in cache
        /// </summary>
        public void InvalidateNode(NodeId nodeId)
        {
            if (nodeId == null)
                return;

            _resultCache.Remove(nodeId);
            _nodeSignatures.Remove(nodeId);
            _dependencyTracker.RemoveNode(nodeId);
            _metrics.RecordInvalidation(1);
        }

        /// <summary>
        /// Clears entire cache
        /// </summary>
        public void ClearCache()
        {
            _resultCache.Clear();
            _nodeSignatures.Clear();
            _dependencyTracker.Clear();
            _metrics.RecordCacheClear();
        }

        #endregion

        #region Statistics and Monitoring

        /// <summary>
        /// Gets comprehensive cache statistics
        /// </summary>
        public CacheStatistics GetStatistics()
        {
            return new CacheStatistics
            {
                CacheSize = _resultCache.Count,
                MaxCacheSize = _maxCacheSize,
                CacheUtilization = (double)_resultCache.Count / _maxCacheSize,
                HitRate = _metrics.GetHitRate(),
                MemoryUsage = _resultCache.GetMemoryUsage(),
                AverageRetrievalTime = _metrics.GetAverageRetrievalTime(),
                TotalInvalidations = _metrics.TotalInvalidations,
                TotalCacheWrites = _metrics.TotalCacheWrites,
                CacheMissesByReason = _metrics.GetCacheMissesByReason(),
                AverageCacheSize = _metrics.GetAverageCacheSize(),
                CacheEfficiency = _metrics.GetCacheEfficiency()
            };
        }

        /// <summary>
        /// Gets performance metrics for cache operations
        /// </summary>
        public CachePerformanceMetrics GetPerformanceMetrics()
        {
            return _metrics.GetPerformanceMetrics();
        }

        /// <summary>
        /// Resets cache statistics
        /// </summary>
        public void ResetStatistics()
        {
            _metrics.Reset();
        }

        #endregion

        #region Private Helper Methods

        private void InvalidateNodeAndDependentsInternal(NodeId nodeId, HashSet<NodeId> visited)
        {
            if (nodeId == null || visited.Contains(nodeId))
                return;
                
            visited.Add(nodeId);
            
            // Invalidate the node
            InvalidateNode(nodeId);
            
            // Recursively invalidate dependents
            var dependents = _dependencyTracker.GetDependents(nodeId);
            foreach (var dependent in dependents)
            {
                InvalidateNodeAndDependentsInternal(dependent, visited);
            }
        }

        private bool HasSignatureChanged(NodeId nodeId, NodeSignature signature)
        {
            return !_nodeSignatures.TryGetValue(nodeId, out var existing) || 
                   !existing.Equals(signature);
        }

        private bool IsResultValid(CachedNodeResult result)
        {
            return DateTime.UtcNow - result.CachedAt < _defaultTtl;
        }

        private long EstimateResultSize(object result)
        {
            if (result == null)
                return 0;

            // Simple size estimation based on object type
            return result switch
            {
                int => sizeof(int),
                long => sizeof(long),
                float => sizeof(float),
                double => sizeof(double),
                string => ((string)result).Length * 2, // Unicode characters
                byte[] => ((byte[])result).Length,
                _ => 1024 // Default estimate for complex objects
            };
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed)
                return;

            _resultCache?.Dispose();
            _dependencyTracker?.Dispose();
            
            _disposed = true;
        }

        #endregion

        #region Supporting Classes

        /// <summary>
        /// Represents a cached node result with metadata
        /// </summary>
        public class CachedNodeResult
        {
            public object Result { get; set; }
            public NodeSignature Signature { get; set; }
            public DateTime CachedAt { get; set; }
            public long Size { get; set; } // Estimated size in bytes
        }

        /// <summary>
        /// Result of cache retrieval operation
        /// </summary>
        public class CacheResult
        {
            public bool Hit { get; set; }
            public object Result { get; set; }
            public double RetrievalTime { get; set; }
            public DateTime CachedAt { get; set; }
        }

        /// <summary>
        /// Comprehensive cache statistics
        /// </summary>
        public class CacheStatistics
        {
            public int CacheSize { get; set; }
            public int MaxCacheSize { get; set; }
            public double CacheUtilization { get; set; }
            public double HitRate { get; set; }
            public long MemoryUsage { get; set; }
            public double AverageRetrievalTime { get; set; }
            public int TotalInvalidations { get; set; }
            public int TotalCacheWrites { get; set; }
            public Dictionary<CacheMissReason, int> CacheMissesByReason { get; set; }
            public double AverageCacheSize { get; set; }
            public double CacheEfficiency { get; set; }
        }

        /// <summary>
        /// Performance metrics for cache operations
        /// </summary>
        public class CachePerformanceMetrics
        {
            public int TotalCacheHits { get; set; }
            public int TotalCacheMisses { get; set; }
            public double AverageHitTime { get; set; }
            public double AverageMissTime { get; set; }
            public double AverageCacheWriteTime { get; set; }
            public int CacheEvictions { get; set; }
            public long PeakMemoryUsage { get; set; }
            public TimeSpan TotalUptime { get; set; }
        }

        public enum CacheMissReason
        {
            NotCached,
            SignatureChanged,
            Expired,
            Error
        }

        #endregion
    }

    #region LRU Cache Implementation

    /// <summary>
    /// LRU Cache implementation with memory management
    /// </summary>
    public class LRUCache<TKey, TValue> : IDisposable where TKey : notnull
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _cache;
        private readonly LinkedList<CacheItem> _lruList;
        private readonly object _lockObject = new object();
        private long _totalMemoryUsage = 0;
        private bool _disposed = false;

        public LRUCache(int capacity)
        {
            _capacity = capacity;
            _cache = new Dictionary<TKey, LinkedListNode<CacheItem>>(capacity);
            _lruList = new LinkedList<CacheItem>();
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            lock (_lockObject)
            {
                value = default;
                
                if (!_cache.TryGetValue(key, out var node))
                    return false;
                
                // Move to front (most recently used)
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                
                value = node.Value.Value;
                return true;
            }
        }

        public void Add(TKey key, TValue value)
        {
            lock (_lockObject)
            {
                if (_cache.TryGetValue(key, out var existingNode))
                {
                    // Update existing
                    _totalMemoryUsage -= existingNode.Value.EstimatedSize;
                    existingNode.Value.Value = value;
                    existingNode.Value.EstimatedSize = EstimateSize(value);
                    _lruList.Remove(existingNode);
                    _lruList.AddFirst(existingNode);
                }
                else
                {
                    // Add new
                    var newItem = new CacheItem 
                    { 
                        Key = key, 
                        Value = value,
                        EstimatedSize = EstimateSize(value)
                    };

                    if (_cache.Count >= _capacity)
                    {
                        // Remove least recently used
                        var lru = _lruList.Last;
                        _lruList.RemoveLast();
                        _cache.Remove(lru.Value.Key);
                        _totalMemoryUsage -= lru.Value.EstimatedSize;
                    }
                    
                    var newNode = new LinkedListNode<CacheItem>(newItem);
                    _lruList.AddFirst(newNode);
                    _cache[key] = newNode;
                }

                _totalMemoryUsage += EstimateSize(value);
            }
        }

        public void Remove(TKey key)
        {
            lock (_lockObject)
            {
                if (_cache.TryGetValue(key, out var node))
                {
                    _lruList.Remove(node);
                    _cache.Remove(key);
                    _totalMemoryUsage -= node.Value.EstimatedSize;
                }
            }
        }

        public void Clear()
        {
            lock (_lockObject)
            {
                _cache.Clear();
                _lruList.Clear();
                _totalMemoryUsage = 0;
            }
        }

        public int Count 
        { 
            get { lock (_lockObject) return _cache.Count; } 
        }

        public long GetMemoryUsage()
        {
            lock (_lockObject)
            {
                return _totalMemoryUsage;
            }
        }

        private long EstimateSize(TValue value)
        {
            if (value == null)
                return 0;

            // Simple size estimation
            return value switch
            {
                int => sizeof(int),
                long => sizeof(long),
                float => sizeof(float),
                double => sizeof(double),
                string => ((string)(object)value).Length * 2L,
                byte[] => ((byte[])(object)value).Length,
                _ => 1024L // Default estimate
            };
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Clear();
            _disposed = true;
        }

        private class CacheItem
        {
            public TKey Key { get; set; }
            public TValue Value { get; set; }
            public long EstimatedSize { get; set; }
        }
    }

    #endregion

    #region Dependency Tracker

    /// <summary>
    /// Tracks node dependencies for cache invalidation
    /// </summary>
    public class DependencyTracker : IDisposable
    {
        private readonly Dictionary<NodeId, HashSet<NodeId>> _dependents = new();
        private readonly Dictionary<NodeId, NodeSignature> _nodeSignatures = new();
        private readonly object _lockObject = new object();
        private bool _disposed = false;

        public void RecordEvaluation(NodeId nodeId, NodeSignature signature)
        {
            lock (_lockObject)
            {
                _nodeSignatures[nodeId] = signature;
            }
        }

        public void RemoveNode(NodeId nodeId)
        {
            lock (_lockObject)
            {
                _dependents.Remove(nodeId);
                _nodeSignatures.Remove(nodeId);
                
                // Remove from all dependent lists
                foreach (var dependents in _dependents.Values)
                {
                    dependents.Remove(nodeId);
                }
            }
        }

        public HashSet<NodeId> GetDependents(NodeId nodeId)
        {
            lock (_lockObject)
            {
                return _dependents.TryGetValue(nodeId, out var dependents) 
                    ? new HashSet<NodeId>(dependents) 
                    : new HashSet<NodeId>();
            }
        }

        public void Clear()
        {
            lock (_lockObject)
            {
                _dependents.Clear();
                _nodeSignatures.Clear();
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Clear();
            _disposed = true;
        }
    }

    #endregion

    #region Cache Metrics

    /// <summary>
    /// Tracks cache performance metrics
    /// </summary>
    public class CacheMetrics
    {
        private int _hits = 0;
        private int _misses = 0;
        private int _writes = 0;
        private int _invalidations = 0;
        private int _cacheClears = 0;
        private readonly List<double> _hitTimes = new();
        private readonly List<double> _missTimes = new();
        private readonly List<double> _writeTimes = new();
        private readonly Dictionary<CacheMissReason, int> _missReasons = new();
        private readonly List<int> _cacheSizes = new();
        private readonly Stopwatch _uptimeTimer = Stopwatch.StartNew();

        public void RecordCacheHit()
        {
            _hits++;
        }

        public void RecordCacheMiss(CacheMissReason reason)
        {
            _misses++;
            _missReasons[reason] = _missReasons.GetValueOrDefault(reason, 0) + 1;
        }

        public void RecordCacheWrite()
        {
            _writes++;
            _cacheSizes.Add(0); // Will be updated by caller
        }

        public void RecordInvalidation(int count)
        {
            _invalidations += count;
        }

        public void RecordCacheClear()
        {
            _cacheClears++;
        }

        public void RecordCacheSize(int size)
        {
            if (_cacheSizes.Count > 0)
            {
                _cacheSizes[_cacheSizes.Count - 1] = size;
            }
        }

        public void RecordHitTime(double time)
        {
            _hitTimes.Add(time);
        }

        public void RecordMissTime(double time)
        {
            _missTimes.Add(time);
        }

        public void RecordWriteTime(double time)
        {
            _writeTimes.Add(time);
        }

        public void Reset()
        {
            _hits = 0;
            _misses = 0;
            _writes = 0;
            _invalidations = 0;
            _cacheClears = 0;
            _hitTimes.Clear();
            _missTimes.Clear();
            _writeTimes.Clear();
            _missReasons.Clear();
            _cacheSizes.Clear();
            _uptimeTimer.Restart();
        }

        public double GetHitRate()
        {
            var total = _hits + _misses;
            return total > 0 ? (double)_hits / total : 0.0;
        }

        public double GetAverageRetrievalTime()
        {
            var allTimes = _hitTimes.Concat(_missTimes).ToList();
            return allTimes.Any() ? allTimes.Average() : 0.0;
        }

        public Dictionary<CacheMissReason, int> GetCacheMissesByReason()
        {
            return new Dictionary<CacheMissReason, int>(_missReasons);
        }

        public double GetAverageCacheSize()
        {
            return _cacheSizes.Any() ? _cacheSizes.Average() : 0.0;
        }

        public double GetCacheEfficiency()
        {
            var totalOperations = _hits + _misses + _writes;
            var effectiveOperations = _hits + _writes;
            return totalOperations > 0 ? (double)effectiveOperations / totalOperations : 0.0;
        }

        public CachePerformanceMetrics GetPerformanceMetrics()
        {
            return new CachePerformanceMetrics
            {
                TotalCacheHits = _hits,
                TotalCacheMisses = _misses,
                AverageHitTime = _hitTimes.Any() ? _hitTimes.Average() : 0,
                AverageMissTime = _missTimes.Any() ? _missTimes.Average() : 0,
                AverageCacheWriteTime = _writeTimes.Any() ? _writeTimes.Average() : 0,
                CacheEvictions = _invalidations,
                PeakMemoryUsage = _cacheSizes.Any() ? _cacheSizes.Max() * 1024L : 0, // Rough estimate
                TotalUptime = _uptimeTimer.Elapsed
            };
        }

        public int TotalInvalidations => _invalidations;
        public int TotalCacheWrites => _writes;
    }

    #endregion
}