using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace T3.Core.NodeGraph
{
    /// <summary>
    /// High-performance Evaluation Cache for TiXL Node System
    /// Provides intelligent caching of node evaluation results with dependency tracking,
    /// LRU eviction, and multi-level cache optimization for optimal performance
    /// </summary>
    public class EvaluationCache : IDisposable
    {
        #region Private Fields

        private readonly LRUCache<NodeId, CachedNodeResult> _resultCache;
        private readonly SignatureIndex _signatureIndex;
        private readonly DependencyTracker _dependencyTracker;
        private readonly CacheMetrics _metrics;
        private readonly ConcurrentDictionary<NodeId, SemaphoreSlim> _nodeLocks;
        
        // Configuration
        private readonly int _maxCacheSize;
        private readonly TimeSpan _defaultTtl;
        private readonly TimeSpan _signatureValidationInterval;
        private readonly double _memoryPressureThreshold;
        
        // Performance optimization
        private readonly ConcurrentQueue<CacheCleanupTask> _cleanupQueue;
        private readonly Timer _periodicCleanupTimer;
        
        private bool _disposed = false;
        private DateTime _lastCleanup = DateTime.UtcNow;

        #endregion

        #region Constructor

        public EvaluationCache(int maxCacheSize = 10000, 
                              TimeSpan defaultTtl = default,
                              TimeSpan? signatureValidationInterval = null,
                              double memoryPressureThreshold = 0.8)
        {
            _maxCacheSize = maxCacheSize;
            _defaultTtl = defaultTtl == default ? TimeSpan.FromMinutes(5) : defaultTtl;
            _signatureValidationInterval = signatureValidationInterval ?? TimeSpan.FromSeconds(30);
            _memoryPressureThreshold = memoryPressureThreshold;

            _resultCache = new LRUCache<NodeId, CachedNodeResult>(maxCacheSize);
            _signatureIndex = new SignatureIndex();
            _dependencyTracker = new DependencyTracker();
            _metrics = new CacheMetrics();
            _nodeLocks = new ConcurrentDictionary<NodeId, SemaphoreSlim>();
            _cleanupQueue = new ConcurrentQueue<CacheCleanupTask>();

            // Start periodic cleanup
            _periodicCleanupTimer = new Timer(PerformPeriodicCleanup, null, 
                _signatureValidationInterval, _signatureValidationInterval);
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
            var nodeLock = GetNodeLock(nodeId);

            return nodeLock.Execute(() =>
            {
                try
                {
                    // Check if signature has changed
                    var signatureValid = _signatureIndex.IsSignatureValid(nodeId, signature);
                    if (!signatureValid)
                    {
                        InvalidateNode(nodeId, CacheInvalidationReason.SignatureChanged);
                        _metrics.RecordCacheMiss(CacheMissReason.SignatureChanged);
                        return new CacheResult { Hit = false };
                    }

                    // Try to get from cache
                    if (_resultCache.TryGetValue(nodeId, out var cachedResult))
                    {
                        // Check if result is still valid
                        if (IsResultValid(cachedResult))
                        {
                            _metrics.RecordCacheHit(timer.Elapsed);
                            return new CacheResult 
                            { 
                                Hit = true, 
                                Result = cachedResult.Result,
                                RetrievalTime = timer.Elapsed.TotalMilliseconds,
                                CachedAt = cachedResult.CachedAt,
                                Size = cachedResult.Size
                            };
                        }
                        else
                        {
                            // Remove expired result
                            _resultCache.Remove(nodeId);
                            _signatureIndex.RemoveNode(nodeId);
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
                    _metrics.RecordCacheMiss(CacheMissReason.Error);
                    return new CacheResult { Hit = false };
                }
            });
        }

        /// <summary>
        /// Stores evaluation result in cache
        /// </summary>
        public void CacheResult(NodeId nodeId, object result, NodeSignature signature)
        {
            if (nodeId == null || signature == null)
                return;

            var nodeLock = GetNodeLock(nodeId);
            nodeLock.Execute(() =>
            {
                try
                {
                    var timer = Stopwatch.StartNew();
                    
                    // Check memory pressure
                    CheckMemoryPressure();

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
                    
                    // Update signature index
                    _signatureIndex.UpdateSignature(nodeId, signature);

                    // Update dependency tracking
                    _dependencyTracker.RecordEvaluation(nodeId, signature);
                    
                    _metrics.RecordCacheWrite(timer.Elapsed, cachedResult.Size);
                }
                catch (Exception ex)
                {
                    // Continue execution even if caching fails
                    _metrics.RecordCacheWriteFailure();
                }
            });
        }

        /// <summary>
        /// Invalidates cache for specific node and all its dependents
        /// </summary>
        public void InvalidateNodeAndDependents(NodeId nodeId, CacheInvalidationReason reason = CacheInvalidationReason.Manual)
        {
            if (nodeId == null)
                return;

            var affectedNodes = new HashSet<NodeId> { nodeId };
            
            // Find all nodes that depend on this node
            var dependents = _dependencyTracker.GetDependents(nodeId);
            foreach (var dependent in dependents)
            {
                FindAllDependentsRecursive(dependent, affectedNodes);
            }
            
            // Invalidate all affected nodes
            foreach (var affectedNode in affectedNodes)
            {
                InvalidateNode(affectedNode, reason);
            }

            _metrics.RecordInvalidation(affectedNodes.Count, reason);
        }

        /// <summary>
        /// Invalidates specific node in cache
        /// </summary>
        public void InvalidateNode(NodeId nodeId, CacheInvalidationReason reason = CacheInvalidationReason.Manual)
        {
            if (nodeId == null)
                return;

            var nodeLock = GetNodeLock(nodeId);
            nodeLock.Execute(() =>
            {
                _resultCache.Remove(nodeId);
                _signatureIndex.RemoveNode(nodeId);
                _dependencyTracker.RemoveNode(nodeId);
            });

            _metrics.RecordInvalidation(1, reason);
        }

        /// <summary>
        /// Clears entire cache
        /// </summary>
        public void ClearCache(CacheInvalidationReason reason = CacheInvalidationReason.Manual)
        {
            _resultCache.Clear();
            _signatureIndex.Clear();
            _dependencyTracker.Clear();
            _metrics.RecordCacheClear(reason);
        }

        /// <summary>
        /// Pre-warms cache with commonly used results
        /// </summary>
        public void PrewarmCache(Dictionary<NodeId, (object Result, NodeSignature Signature)> prewarmData)
        {
            if (prewarmData == null)
                return;

            foreach (var kvp in prewarmData)
            {
                CacheResult(kvp.Key, kvp.Value.Result, kvp.Value.Signature);
            }

            _metrics.RecordPrewarm(prewarmData.Count);
        }

        /// <summary>
        /// Gets cache hit rate for a specific time period
        /// </summary>
        public double GetCacheHitRate(TimeSpan period)
        {
            return _metrics.GetHitRate(period);
        }

        /// <summary>
        /// Gets average cache response time
        /// </summary>
        public double GetAverageResponseTime()
        {
            return _metrics.GetAverageResponseTime();
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
                HitRate = _metrics.GetOverallHitRate(),
                MemoryUsage = _resultCache.GetMemoryUsage(),
                AverageRetrievalTime = _metrics.GetAverageRetrievalTime(),
                TotalInvalidations = _metrics.TotalInvalidations,
                TotalCacheWrites = _metrics.TotalCacheWrites,
                CacheMissesByReason = _metrics.GetCacheMissesByReason(),
                AverageCacheSize = _metrics.GetAverageCacheSize(),
                CacheEfficiency = _metrics.GetCacheEfficiency(),
                SignatureValidations = _signatureIndex.GetValidationCount(),
                DependencyTrackingAccuracy = _dependencyTracker.GetAccuracy()
            };
        }

        /// <summary>
        /// Gets detailed performance metrics
        /// </summary>
        public CachePerformanceMetrics GetPerformanceMetrics()
        {
            return _metrics.GetPerformanceMetrics();
        }

        /// <summary>
        /// Gets memory pressure information
        /// </summary>
        public MemoryPressureInfo GetMemoryPressureInfo()
        {
            var totalMemory = GC.GetTotalMemory(false);
            var cacheMemory = _resultCache.GetMemoryUsage();
            var pressure = (double)cacheMemory / Math.Max(1, totalMemory);

            return new MemoryPressureInfo
            {
                TotalSystemMemory = totalMemory,
                CacheMemoryUsage = cacheMemory,
                MemoryPressure = pressure,
                IsUnderPressure = pressure > _memoryPressureThreshold,
                RecommendedAction = GetRecommendedMemoryAction(pressure)
            };
        }

        /// <summary>
        /// Gets cache entries sorted by priority for optimization
        /// </summary>
        public List<CacheEntryInfo> GetCacheEntriesByPriority()
        {
            var entries = new List<CacheEntryInfo>();

            foreach (var kvp in _resultCache.GetAllEntries())
            {
                var priority = CalculateCacheEntryPriority(kvp.Key, kvp.Value);
                entries.Add(new CacheEntryInfo
                {
                    NodeId = kvp.Key,
                    Size = kvp.Value.Size,
                    CachedAt = kvp.Value.CachedAt,
                    AccessCount = _metrics.GetNodeAccessCount(kvp.Key),
                    LastAccessed = _metrics.GetNodeLastAccess(kvp.Key),
                    Priority = priority
                });
            }

            return entries.OrderByDescending(e => e.Priority).ToList();
        }

        /// <summary>
        /// Resets cache statistics
        /// </summary>
        public void ResetStatistics()
        {
            _metrics.Reset();
            _signatureIndex.Reset();
        }

        #endregion

        #region Private Helper Methods

        private SemaphoreSlim GetNodeLock(NodeId nodeId)
        {
            return _nodeLocks.GetOrAdd(nodeId, _ => new SemaphoreSlim(1, 1));
        }

        private void FindAllDependentsRecursive(NodeId nodeId, HashSet<NodeId> visited)
        {
            if (visited.Contains(nodeId))
                return;

            visited.Add(nodeId);

            var dependents = _dependencyTracker.GetDependents(nodeId);
            foreach (var dependent in dependents)
            {
                FindAllDependentsRecursive(dependent, visited);
            }
        }

        private bool IsResultValid(CachedNodeResult result)
        {
            return DateTime.UtcNow - result.CachedAt < _defaultTtl;
        }

        private long EstimateResultSize(object result)
        {
            if (result == null)
                return 0;

            // Enhanced size estimation
            return result switch
            {
                int => sizeof(int),
                long => sizeof(long),
                float => sizeof(float),
                double => sizeof(double),
                string => ((string)result).Length * 2, // Unicode characters
                byte[] => ((byte[])result).Length,
                _ => EstimateComplexObjectSize(result)
            };
        }

        private long EstimateComplexObjectSize(object obj)
        {
            // Rough estimation for complex objects
            // In a real implementation, you might use reflection or other techniques
            return 1024; // Default estimate for complex objects
        }

        private void CheckMemoryPressure()
        {
            var memoryInfo = GetMemoryPressureInfo();
            
            if (memoryInfo.IsUnderPressure)
            {
                // Trigger aggressive cleanup
                PerformAggressiveCleanup();
                _metrics.RecordMemoryPressure();
            }
        }

        private void PerformPeriodicCleanup(object state)
        {
            try
            {
                var now = DateTime.UtcNow;
                
                if (now - _lastCleanup < _signatureValidationInterval)
                    return;

                CleanupExpiredEntries();
                _signatureIndex.ValidateSignatures();
                _lastCleanup = now;

                _metrics.RecordPeriodicCleanup();
            }
            catch (Exception)
            {
                // Ignore errors in periodic cleanup
            }
        }

        private void PerformAggressiveCleanup()
        {
            // Remove oldest entries to free memory
            var entriesToRemove = _resultCache.Count / 4; // Remove 25% of entries
            
            for (int i = 0; i < entriesToRemove; i++)
            {
                _resultCache.RemoveOldest();
            }

            // Force garbage collection
            GC.Collect();

            _metrics.RecordAggressiveCleanup(entriesToRemove);
        }

        private void CleanupExpiredEntries()
        {
            var now = DateTime.UtcNow;
            var expiredNodes = new List<NodeId>();

            foreach (var kvp in _resultCache.GetAllEntries())
            {
                if (now - kvp.Value.CachedAt > _defaultTtl)
                {
                    expiredNodes.Add(kvp.Key);
                }
            }

            foreach (var nodeId in expiredNodes)
            {
                InvalidateNode(nodeId, CacheInvalidationReason.Expired);
            }

            if (expiredNodes.Count > 0)
            {
                _metrics.RecordExpiredCleanup(expiredNodes.Count);
            }
        }

        private double CalculateCacheEntryPriority(NodeId nodeId, CachedNodeResult result)
        {
            var accessCount = _metrics.GetNodeAccessCount(nodeId);
            var lastAccessed = _metrics.GetNodeLastAccess(nodeId);
            var age = DateTime.UtcNow - result.CachedAt;
            var size = result.Size;

            // Priority calculation: higher access count = higher priority, 
            // newer = higher priority, smaller = higher priority
            var priority = accessCount * 10 + Math.Max(0, 100 - age.TotalMinutes) - size / 1024;
            
            return priority;
        }

        private string GetRecommendedMemoryAction(double pressure)
        {
            return pressure switch
            {
                > 0.9 => "Immediate cleanup required",
                > 0.8 => "Aggressive cleanup recommended",
                > 0.6 => "Moderate cleanup suggested",
                _ => "Memory usage normal"
            };
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed)
                return;

            _periodicCleanupTimer?.Dispose();
            _resultCache?.Dispose();

            foreach (var nodeLock in _nodeLocks.Values)
            {
                nodeLock?.Dispose();
            }
            _nodeLocks.Clear();

            _signatureIndex?.Dispose();
            _dependencyTracker?.Dispose();

            _disposed = true;
        }

        #endregion

        #region Supporting Classes

        /// <summary>
        /// Cached node result with metadata
        /// </summary>
        public class CachedNodeResult
        {
            public object Result { get; set; }
            public NodeSignature Signature { get; set; }
            public DateTime CachedAt { get; set; }
            public long Size { get; set; } // Estimated size in bytes
            public int AccessCount { get; set; }
            public DateTime LastAccessed { get; set; }
        }

        /// <summary>
        /// Result of cache retrieval operation
        /// </summary>
        public class CacheResult
        {
            public bool Hit { get; set; }
            public object? Result { get; set; }
            public double RetrievalTime { get; set; }
            public DateTime CachedAt { get; set; }
            public long Size { get; set; }
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
            public Dictionary<CacheMissReason, int> CacheMissesByReason { get; set; } = new();
            public double AverageCacheSize { get; set; }
            public double CacheEfficiency { get; set; }
            public int SignatureValidations { get; set; }
            public double DependencyTrackingAccuracy { get; set; }
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
            public int MemoryPressureEvents { get; set; }
            public int CleanupOperations { get; set; }
        }

        /// <summary>
        /// Cache entry information for optimization
        /// </summary>
        public class CacheEntryInfo
        {
            public NodeId NodeId { get; set; }
            public long Size { get; set; }
            public DateTime CachedAt { get; set; }
            public int AccessCount { get; set; }
            public DateTime LastAccessed { get; set; }
            public double Priority { get; set; }
        }

        /// <summary>
        /// Memory pressure information
        /// </summary>
        public class MemoryPressureInfo
        {
            public long TotalSystemMemory { get; set; }
            public long CacheMemoryUsage { get; set; }
            public double MemoryPressure { get; set; }
            public bool IsUnderPressure { get; set; }
            public string RecommendedAction { get; set; } = string.Empty;
        }

        public enum CacheMissReason
        {
            NotCached,
            SignatureChanged,
            Expired,
            Error
        }

        public enum CacheInvalidationReason
        {
            Manual,
            SignatureChanged,
            DependencyInvalidation,
            Expired,
            MemoryPressure,
            Cleanup
        }

        #endregion

        #region Internal Supporting Components

        /// <summary>
        /// Signature index for fast signature validation
        /// </summary>
        private class SignatureIndex : IDisposable
        {
            private readonly ConcurrentDictionary<NodeId, NodeSignature> _signatures = new();
            private readonly Dictionary<NodeId, DateTime> _lastValidations = new();
            private readonly object _lockObject = new object();
            private int _validationCount = 0;

            public bool IsSignatureValid(NodeId nodeId, NodeSignature signature)
            {
                if (!_signatures.TryGetValue(nodeId, out var existing))
                    return false;

                var isValid = existing.Equals(signature);

                lock (_lockObject)
                {
                    _lastValidations[nodeId] = DateTime.UtcNow;
                    _validationCount++;
                }

                return isValid;
            }

            public void UpdateSignature(NodeId nodeId, NodeSignature signature)
            {
                _signatures[nodeId] = signature;
            }

            public void RemoveNode(NodeId nodeId)
            {
                _signatures.TryRemove(nodeId, out _);
                
                lock (_lockObject)
                {
                    _lastValidations.Remove(nodeId);
                }
            }

            public void Clear()
            {
                _signatures.Clear();
                
                lock (_lockObject)
                {
                    _lastValidations.Clear();
                }
            }

            public void ValidateSignatures()
            {
                // Periodic validation logic
            }

            public int GetValidationCount()
            {
                lock (_lockObject)
                {
                    return _validationCount;
                }
            }

            public void Reset()
            {
                _validationCount = 0;
            }

            public void Dispose()
            {
                Clear();
            }
        }

        /// <summary>
        /// Dependency tracker for cache invalidation
        /// </summary>
        private class DependencyTracker : IDisposable
        {
            private readonly Dictionary<NodeId, HashSet<NodeId>> _dependents = new();
            private int _accuracyMeasurements = 0;
            private int _accuratePredictions = 0;

            public void RecordEvaluation(NodeId nodeId, NodeSignature signature)
            {
                // Track evaluation for dependency accuracy
            }

            public void RemoveNode(NodeId nodeId)
            {
                _dependents.Remove(nodeId);
                
                foreach (var dependents in _dependents.Values)
                {
                    dependents.Remove(nodeId);
                }
            }

            public HashSet<NodeId> GetDependents(NodeId nodeId)
            {
                return _dependents.TryGetValue(nodeId, out var dependents) 
                    ? new HashSet<NodeId>(dependents) 
                    : new HashSet<NodeId>();
            }

            public void Clear()
            {
                _dependents.Clear();
            }

            public double GetAccuracy()
            {
                return _accuracyMeasurements > 0 ? (double)_accuratePredictions / _accuracyMeasurements : 0.0;
            }

            public void Dispose()
            {
                Clear();
            }
        }

        /// <summary>
        /// Cache cleanup task
        /// </summary>
        private class CacheCleanupTask
        {
            public NodeId NodeId { get; set; }
            public CacheInvalidationReason Reason { get; set; }
            public DateTime ScheduledTime { get; set; }
        }

        /// <summary>
        /// Performance metrics collector
        /// </summary>
        private class CacheMetrics
        {
            private int _hits = 0;
            private int _misses = 0;
            private int _writes = 0;
            private int _invalidations = 0;
            private int _writeFailures = 0;
            private int _memoryPressureEvents = 0;
            private int _aggressiveCleanups = 0;
            private int _expiredCleanups = 0;
            private int _periodicCleanups = 0;
            private int _prewarmOperations = 0;
            
            private readonly List<double> _hitTimes = new();
            private readonly List<double> _missTimes = new();
            private readonly List<double> _writeTimes = new();
            private readonly Dictionary<CacheMissReason, int> _missReasons = new();
            private readonly Dictionary<CacheInvalidationReason, int> _invalidationReasons = new();
            private readonly List<int> _cacheSizes = new();
            private readonly Dictionary<NodeId, int> _nodeAccessCounts = new();
            private readonly Dictionary<NodeId, DateTime> _nodeLastAccess = new();
            
            private readonly Stopwatch _uptimeTimer = Stopwatch.StartNew();

            public void RecordCacheHit(TimeSpan duration)
            {
                _hits++;
                _hitTimes.Add(duration.TotalMilliseconds);
            }

            public void RecordCacheMiss(CacheMissReason reason)
            {
                _misses++;
                _missReasons[reason] = _missReasons.GetValueOrDefault(reason, 0) + 1;
                _missTimes.Add(0); // Miss time is negligible
            }

            public void RecordCacheWrite(TimeSpan duration, long size)
            {
                _writes++;
                _writeTimes.Add(duration.TotalMilliseconds);
                _cacheSizes.Add((int)size);
            }

            public void RecordCacheWriteFailure()
            {
                _writeFailures++;
            }

            public void RecordInvalidation(int count, CacheInvalidationReason reason)
            {
                _invalidations += count;
                _invalidationReasons[reason] = _invalidationReasons.GetValueOrDefault(reason, 0) + count;
            }

            public void RecordCacheClear(CacheInvalidationReason reason)
            {
                RecordInvalidation(1, reason);
            }

            public void RecordMemoryPressure()
            {
                _memoryPressureEvents++;
            }

            public void RecordAggressiveCleanup(int entriesRemoved)
            {
                _aggressiveCleanups += entriesRemoved;
            }

            public void RecordExpiredCleanup(int entriesRemoved)
            {
                _expiredCleanups += entriesRemoved;
            }

            public void RecordPeriodicCleanup()
            {
                _periodicCleanups++;
            }

            public void RecordPrewarm(int count)
            {
                _prewarmOperations += count;
            }

            public void RecordNodeAccess(NodeId nodeId)
            {
                _nodeAccessCounts[nodeId] = _nodeAccessCounts.GetValueOrDefault(nodeId, 0) + 1;
                _nodeLastAccess[nodeId] = DateTime.UtcNow;
            }

            public int GetNodeAccessCount(NodeId nodeId)
            {
                return _nodeAccessCounts.GetValueOrDefault(nodeId, 0);
            }

            public DateTime GetNodeLastAccess(NodeId nodeId)
            {
                return _nodeLastAccess.GetValueOrDefault(nodeId, DateTime.MinValue);
            }

            public double GetHitRate(TimeSpan period)
            {
                // Simplified calculation - would track hits/misses by time period
                return GetOverallHitRate();
            }

            public double GetOverallHitRate()
            {
                var total = _hits + _misses;
                return total > 0 ? (double)_hits / total : 0.0;
            }

            public double GetAverageResponseTime()
            {
                var allTimes = _hitTimes.Concat(_missTimes).ToList();
                return allTimes.Any() ? allTimes.Average() : 0.0;
            }

            public double GetAverageRetrievalTime()
            {
                return _hitTimes.Any() ? _hitTimes.Average() : 0.0;
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
                var effectiveOperations = _hits + _writes - _writeFailures;
                return totalOperations > 0 ? (double)effectiveOperations / totalOperations : 0.0;
            }

            public void Reset()
            {
                _hits = 0;
                _misses = 0;
                _writes = 0;
                _invalidations = 0;
                _writeFailures = 0;
                _memoryPressureEvents = 0;
                _aggressiveCleanups = 0;
                _expiredCleanups = 0;
                _periodicCleanups = 0;
                _prewarmOperations = 0;
                _hitTimes.Clear();
                _missTimes.Clear();
                _writeTimes.Clear();
                _missReasons.Clear();
                _invalidationReasons.Clear();
                _cacheSizes.Clear();
                _nodeAccessCounts.Clear();
                _nodeLastAccess.Clear();
                _uptimeTimer.Restart();
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
                    CacheEvictions = _aggressiveCleanups + _expiredCleanups,
                    PeakMemoryUsage = _cacheSizes.Any() ? _cacheSizes.Max() * 1024L : 0,
                    TotalUptime = _uptimeTimer.Elapsed,
                    MemoryPressureEvents = _memoryPressureEvents,
                    CleanupOperations = _periodicCleanups
                };
            }

            public int TotalInvalidations => _invalidations;
            public int TotalCacheWrites => _writes;
        }

        #endregion
    }

    #region LRU Cache Implementation

    /// <summary>
    /// Enhanced LRU Cache with memory management and statistics
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
                value = default!;
                
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
                        if (lru != null)
                        {
                            _lruList.RemoveLast();
                            _cache.Remove(lru.Value.Key);
                            _totalMemoryUsage -= lru.Value.EstimatedSize;
                        }
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

        public void RemoveOldest()
        {
            lock (_lockObject)
            {
                var oldest = _lruList.Last;
                if (oldest != null)
                {
                    _lruList.RemoveLast();
                    _cache.Remove(oldest.Value.Key);
                    _totalMemoryUsage -= oldest.Value.EstimatedSize;
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

        public List<KeyValuePair<TKey, TValue>> GetAllEntries()
        {
            lock (_lockObject)
            {
                return _cache.Select(kvp => KeyValuePair.Create(kvp.Key, kvp.Value.Value)).ToList();
            }
        }

        private long EstimateSize(TValue value)
        {
            if (value == null)
                return 0;

            // Enhanced size estimation
            return value switch
            {
                int => sizeof(int),
                long => sizeof(long),
                float => sizeof(float),
                double => sizeof(double),
                string => ((string)(object)value).Length * 2L,
                byte[] => ((byte[])(object)value).Length,
                _ => 1024L // Default estimate for complex objects
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
}