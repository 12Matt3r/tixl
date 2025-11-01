using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace TiXL.Core.Graphics.PSO
{
    /// <summary>
    /// LRU (Least Recently Used) cache implementation for PSO storage
    /// </summary>
    /// <typeparam name="TKey">Cache key type</typeparam>
    /// <typeparam name="TValue">Cache value type</typeparam>
    public class PSOCache<TKey, TValue> where TKey : IPSOKey where TValue : class
    {
        private readonly int _maxCapacity;
        private readonly TimeSpan _maxAge;
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        private readonly Dictionary<TKey, CacheEntry> _cache = new Dictionary<TKey, CacheEntry>();
        private readonly LinkedList<TKey> _lruList = new LinkedList<TKey>();
        
        // Performance counters
        private long _hits = 0;
        private long _misses = 0;
        private long _evictions = 0;
        private long _totalCacheSize = 0;
        
        private readonly Timer _cleanupTimer;
        private readonly int _initialCapacity;
        
        public PSOCache(int initialCapacity = 1000, TimeSpan? maxAge = null)
        {
            _initialCapacity = initialCapacity;
            _maxCapacity = initialCapacity;
            _maxAge = maxAge ?? TimeSpan.FromHours(1);
            
            // Setup periodic cleanup timer (every 30 seconds)
            _cleanupTimer = new Timer(CleanupExpiredEntries, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
        }
        
        /// <summary>
        /// Get or create a value in the cache
        /// </summary>
        public (TValue Value, bool WasCached) GetOrCreate(TKey key, Func<TValue> factory)
        {
            _lock.EnterReadLock();
            try
            {
                if (_cache.TryGetValue(key, out var entry))
                {
                    if (IsExpired(entry))
                    {
                        _lock.ExitReadLock();
                        _lock.EnterWriteLock();
                        try
                        {
                            // Double-check after acquiring write lock
                            if (_cache.TryGetValue(key, out var expiredEntry))
                            {
                                RemoveEntry(key, expiredEntry);
                            }
                        }
                        finally
                        {
                            _lock.ExitWriteLock();
                            _lock.EnterReadLock();
                        }
                    }
                    else
                    {
                        // Update LRU position
                        UpdateLRU(key);
                        Interlocked.Increment(ref _hits);
                        return (entry.Value, true);
                    }
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }
            
            // Cache miss or expired entry
            Interlocked.Increment(ref _misses);
            
            // Create new value
            var value = factory();
            if (value == null)
            {
                return (null, false);
            }
            
            // Cache the new value
            AddOrUpdate(key, value);
            return (value, false);
        }
        
        /// <summary>
        /// Pre-warm the cache with commonly used entries
        /// </summary>
        public void WarmUp(IEnumerable<KeyValuePair<TKey, TValue>> entries)
        {
            _lock.EnterWriteLock();
            try
            {
                foreach (var kvp in entries)
                {
                    if (_cache.Count >= _maxCapacity * 0.8) // Warm up to 80% capacity
                        break;
                        
                    if (!_cache.ContainsKey(kvp.Key))
                    {
                        var entry = new CacheEntry
                        {
                            Value = kvp.Value,
                            AccessTime = DateTime.UtcNow,
                            CreationTime = DateTime.UtcNow,
                            Size = EstimateSize(kvp.Value)
                        };
                        
                        _cache[kvp.Key] = entry;
                        _lruList.AddLast(kvp.Key);
                        _totalCacheSize += entry.Size;
                    }
                }
                
                EvictIfNeeded();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Remove an entry from the cache
        /// </summary>
        public bool Remove(TKey key)
        {
            _lock.EnterWriteLock();
            try
            {
                if (_cache.TryGetValue(key, out var entry))
                {
                    RemoveEntry(key, entry);
                    return true;
                }
                return false;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Clear the entire cache
        /// </summary>
        public void Clear()
        {
            _lock.EnterWriteLock();
            try
            {
                _cache.Clear();
                _lruList.Clear();
                _totalCacheSize = 0;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Get cache statistics
        /// </summary>
        public CacheStatistics GetStatistics()
        {
            _lock.EnterReadLock();
            try
            {
                var total = _hits + _misses;
                return new CacheStatistics
                {
                    TotalEntries = _cache.Count,
                    Capacity = _maxCapacity,
                    MemoryUsageBytes = _totalCacheSize,
                    CacheHits = _hits,
                    CacheMisses = _misses,
                    CacheHitRate = total > 0 ? (double)_hits / total : 0,
                    Evictions = _evictions,
                    AverageEntrySize = _cache.Count > 0 ? _totalCacheSize / _cache.Count : 0
                };
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        
        /// <summary>
        /// Resize the cache capacity
        /// </summary>
        public void Resize(int newCapacity)
        {
            if (newCapacity <= 0) return;
            
            _lock.EnterWriteLock();
            try
            {
                _maxCapacity = newCapacity;
                EvictIfNeeded();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Force cleanup of expired entries
        /// </summary>
        public void ForceCleanup()
        {
            CleanupExpiredEntries(null);
        }
        
        private void AddOrUpdate(TKey key, TValue value)
        {
            _lock.EnterWriteLock();
            try
            {
                var now = DateTime.UtcNow;
                var size = EstimateSize(value);
                
                if (_cache.TryGetValue(key, out var existing))
                {
                    // Update existing entry
                    _totalCacheSize -= existing.Size;
                    existing.Value = value;
                    existing.AccessTime = now;
                    existing.Size = size;
                    UpdateLRU(key);
                }
                else
                {
                    // Add new entry
                    var entry = new CacheEntry
                    {
                        Value = value,
                        AccessTime = now,
                        CreationTime = now,
                        Size = size
                    };
                    
                    _cache[key] = entry;
                    _lruList.AddLast(key);
                    _totalCacheSize += size;
                }
                
                EvictIfNeeded();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        private void UpdateLRU(TKey key)
        {
            if (_lruList.Contains(key))
            {
                _lruList.Remove(key);
            }
            _lruList.AddLast(key);
            
            if (_cache.TryGetValue(key, out var entry))
            {
                entry.AccessTime = DateTime.UtcNow;
            }
        }
        
        private void RemoveEntry(TKey key, CacheEntry entry)
        {
            _cache.Remove(key);
            _lruList.Remove(key);
            _totalCacheSize -= entry.Size;
        }
        
        private void EvictIfNeeded()
        {
            while (_cache.Count > _maxCapacity)
            {
                var lruKey = _lruList.First?.Value;
                if (lruKey == null) break;
                
                if (_cache.TryGetValue(lruKey, out var lruEntry))
                {
                    RemoveEntry(lruKey, lruEntry);
                    Interlocked.Increment(ref _evictions);
                }
            }
        }
        
        private void CleanupExpiredEntries(object state)
        {
            _lock.EnterWriteLock();
            try
            {
                var now = DateTime.UtcNow;
                var expiredKeys = new List<TKey>();
                
                foreach (var kvp in _cache)
                {
                    if (IsExpired(kvp.Value))
                    {
                        expiredKeys.Add(kvp.Key);
                    }
                }
                
                foreach (var key in expiredKeys)
                {
                    if (_cache.TryGetValue(key, out var entry))
                    {
                        RemoveEntry(key, entry);
                        Interlocked.Increment(ref _evictions);
                    }
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        private bool IsExpired(CacheEntry entry)
        {
            return DateTime.UtcNow - entry.AccessTime > _maxAge;
        }
        
        private long EstimateSize(TValue value)
        {
            // Rough estimation for PSO objects
            // In a real implementation, this would be more sophisticated
            return 8192; // Assume ~8KB per PSO
        }
        
        private class CacheEntry
        {
            public TValue Value { get; set; }
            public DateTime AccessTime { get; set; }
            public DateTime CreationTime { get; set; }
            public long Size { get; set; }
        }
    }
    
    /// <summary>
    /// Cache statistics and performance metrics
    /// </summary>
    public struct CacheStatistics
    {
        public int TotalEntries { get; set; }
        public int Capacity { get; set; }
        public long MemoryUsageBytes { get; set; }
        public long CacheHits { get; set; }
        public long CacheMisses { get; set; }
        public double CacheHitRate { get; set; }
        public long Evictions { get; set; }
        public long AverageEntrySize { get; set; }
        
        public string GetFormattedString()
        {
            return $@"PSO Cache Statistics:
    Entries: {TotalEntries}/{Capacity}
    Memory Usage: {MemoryUsageBytes / 1024 / 1024:F1}MB
    Hit Rate: {CacheHitRate:P1} ({CacheHits} hits, {CacheMisses} misses)
    Evictions: {Evictions}
    Avg Entry Size: {AverageEntrySize / 1024:F1}KB";
        }
    }
}