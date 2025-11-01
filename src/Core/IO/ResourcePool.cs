using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace TiXL.Core.IO
{
    /// <summary>
    /// High-performance resource pool for I/O buffers and other resources
    /// Minimizes allocations and garbage collection pressure in real-time scenarios
    /// </summary>
    public class ResourcePool : IDisposable
    {
        private readonly ConcurrentDictionary<int, Queue<IOResourceBuffer>> _bufferPools;
        private readonly ConcurrentDictionary<string, WeakReference> _activeResources;
        private readonly SemaphoreSlim _acquisitionSemaphore;
        
        private readonly int _maxPooledBuffers = 100;
        private readonly TimeSpan _bufferCleanupInterval = TimeSpan.FromMinutes(1);
        private readonly Timer _cleanupTimer;
        
        private long _totalBuffersCreated;
        private long _totalBuffersReused;
        private long _totalBuffersDiscarded;
        
        public int TotalBuffersCreated => _totalBuffersCreated;
        public int TotalBuffersReused => _totalBuffersReused;
        public int TotalBuffersDiscarded => _totalBuffersDiscarded;
        public double ReuseRate => TotalBuffersCreated > 0 ? 
            (double)TotalBuffersReused / TotalBuffersCreated * 100 : 0;
        
        public ResourcePool(int maxPooledBuffers = 100)
        {
            _maxPooledBuffers = maxPooledBuffers;
            _bufferPools = new ConcurrentDictionary<int, Queue<IOResourceBuffer>>();
            _activeResources = new ConcurrentDictionary<string, WeakReference>();
            _acquisitionSemaphore = new SemaphoreSlim(maxPooledBuffers, maxPooledBuffers);
            
            // Start cleanup timer
            _cleanupTimer = new Timer(CleanupExpiredBuffers, null, _bufferCleanupInterval, _bufferCleanupInterval);
        }
        
        /// <summary>
        /// Get buffer of specified size from pool
        /// </summary>
        public IOResourceBuffer GetBuffer(int size)
        {
            if (size <= 0)
                throw new ArgumentException("Buffer size must be positive", nameof(size));
            
            // Round up to nearest power of 2 for better memory alignment
            var actualSize = NextPowerOfTwo(size);
            
            // Try to get buffer from pool first
            if (_bufferPools.TryGetValue(actualSize, out var pool) && pool.Count > 0)
            {
                if (pool.TryDequeue(out var buffer))
                {
                    Interlocked.Increment(ref _totalBuffersReused);
                    buffer.Reset();
                    return buffer;
                }
            }
            
            // Create new buffer if pool is empty or doesn't have this size
            Interlocked.Increment(ref _totalBuffersCreated);
            return new IOResourceBuffer(actualSize, this);
        }
        
        /// <summary>
        /// Return buffer to pool for reuse
        /// </summary>
        public void ReturnBuffer(IOResourceBuffer buffer)
        {
            if (buffer == null || buffer.IsDisposed)
                return;
            
            // Check if buffer should be kept in pool
            var bufferSize = buffer.Size;
            if (_bufferPools.TryGetValue(bufferSize, out var pool))
            {
                if (pool.Count < _maxPooledBuffers)
                {
                    buffer.Reset();
                    pool.Enqueue(buffer);
                }
                else
                {
                    // Pool is full, discard buffer
                    Interlocked.Increment(ref _totalBuffersDiscarded);
                }
            }
            else
            {
                // Create new pool for this size
                var newPool = new Queue<IOResourceBuffer>();
                newPool.Enqueue(buffer);
                _bufferPools.TryAdd(bufferSize, newPool);
            }
        }
        
        /// <summary>
        /// Create resource handle for tracking resource lifecycle
        /// </summary>
        public IOResourceHandle CreateResourceHandle(string resourceId, object resource, TimeSpan? expiration = null)
        {
            var handle = new IOResourceHandle(resourceId, resource, expiration);
            
            _activeResources.TryAdd(resourceId, new WeakReference(handle));
            
            return handle;
        }
        
        /// <summary>
        /// Get buffer pool statistics
        /// </summary>
        public IOResourcePoolStatistics GetStatistics()
        {
            return new IOResourcePoolStatistics
            {
                TotalBuffersCreated = TotalBuffersCreated,
                TotalBuffersReused = TotalBuffersReused,
                TotalBuffersDiscarded = TotalBuffersDiscarded,
                ReuseRate = ReuseRate,
                PoolSizes = _bufferPools.ToDictionary(
                    kvp => $"{kvp.Key}B", 
                    kvp => kvp.Value.Count),
                TotalActiveResources = _activeResources.Count,
                ActiveResourceCount = _activeResources.Count(r => 
                    r.Value.IsAlive && !((IOResourceHandle)r.Value.Target)?.IsExpired),
                MaxPooledBuffers = _maxPooledBuffers
            };
        }
        
        /// <summary>
        /// Create snapshot of current pool state
        /// </summary>
        public IOResourcePoolSnapshot CreateSnapshot()
        {
            return new IOResourcePoolSnapshot
            {
                Timestamp = DateTime.UtcNow,
                Statistics = GetStatistics(),
                PoolContents = _bufferPools.ToDictionary(
                    kvp => kvp.Key, 
                    kvp => kvp.Value.Select(b => b.Size).ToArray()),
                ActiveResources = _activeResources.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.IsAlive ? "Active" : "Collected")
            };
        }
        
        /// <summary>
        /// Clear all buffer pools
        /// </summary>
        public void ClearPools()
        {
            foreach (var pool in _bufferPools.Values)
            {
                pool.Clear();
            }
            
            _bufferPools.Clear();
        }
        
        /// <summary>
        /// Trim buffer pools to reduce memory usage
        /// </summary>
        public void TrimPools(int targetMaxSize)
        {
            foreach (var pool in _bufferPools.Values)
            {
                while (pool.Count > targetMaxSize)
                {
                    pool.TryDequeue(out _);
                    Interlocked.Increment(ref _totalBuffersDiscarded);
                }
            }
        }
        
        /// <summary>
        /// Perform cleanup of expired buffers and resources
        /// </summary>
        public void CleanupExpiredBuffers(object state = null)
        {
            try
            {
                var currentTime = DateTime.UtcNow;
                
                // Cleanup buffer pools - remove expired or unused buffers
                var buffersToRemove = new List<(int Size, IOResourceBuffer Buffer)>();
                
                foreach (var pool in _bufferPools)
                {
                    var bufferQueue = pool.Value;
                    var tempBuffers = new List<IOResourceBuffer>();
                    
                    while (bufferQueue.TryDequeue(out var buffer))
                    {
                        if (buffer.IsExpired(currentTime) || buffer.AccessCount == 0)
                        {
                            buffersToRemove.Add((pool.Key, buffer));
                        }
                        else
                        {
                            tempBuffers.Add(buffer);
                        }
                    }
                    
                    // Re-add non-expired buffers
                    foreach (var buffer in tempBuffers)
                    {
                        bufferQueue.Enqueue(buffer);
                    }
                }
                
                // Dispose removed buffers
                foreach (var (size, buffer) in buffersToRemove)
                {
                    buffer.Dispose();
                    Interlocked.Increment(ref _totalBuffersDiscarded);
                }
                
                // Cleanup active resources
                var expiredResourceKeys = new List<string>();
                foreach (var resourceEntry in _activeResources)
                {
                    if (!resourceEntry.Value.IsAlive)
                    {
                        expiredResourceKeys.Add(resourceEntry.Key);
                    }
                    else if (resourceEntry.Value.Target is IOResourceHandle handle && handle.IsExpired)
                    {
                        expiredResourceKeys.Add(resourceEntry.Key);
                        handle.Dispose();
                    }
                }
                
                foreach (var key in expiredResourceKeys)
                {
                    _activeResources.TryRemove(key, out _);
                }
            }
            catch (Exception ex)
            {
                // Log cleanup error but don't throw
                Debug.WriteLine($"Resource pool cleanup error: {ex.Message}");
            }
        }
        
        private static int NextPowerOfTwo(int value)
        {
            if (value <= 1) return 1;
            
            value--;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            value++;
            
            return value;
        }
        
        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            _acquisitionSemaphore?.Dispose();
            
            // Dispose all buffers in pools
            foreach (var pool in _bufferPools.Values)
            {
                while (pool.TryDequeue(out var buffer))
                {
                    buffer?.Dispose();
                }
            }
            
            _bufferPools.Clear();
            
            // Dispose active resource handles
            foreach (var resource in _activeResources.Values)
            {
                if (resource.IsAlive && resource.Target is IOResourceHandle handle)
                {
                    handle.Dispose();
                }
            }
            
            _activeResources.Clear();
        }
    }
    
    /// <summary>
    /// Resource buffer that can be reused from pool
    /// </summary>
    public class IOResourceBuffer : IDisposable
    {
        private readonly int _size;
        private readonly ResourcePool _pool;
        private readonly byte[] _data;
        private readonly DateTime _creationTime;
        
        private int _accessCount;
        private DateTime _lastAccessTime;
        private volatile bool _isDisposed;
        
        public int Size => _size;
        public byte[] Data => _data;
        public int AccessCount => _accessCount;
        public DateTime CreationTime => _creationTime;
        public DateTime LastAccessTime => _lastAccessTime;
        public bool IsDisposed => _isDisposed;
        
        internal IOResourceBuffer(int size, ResourcePool pool)
        {
            _size = size;
            _pool = pool;
            _data = new byte[size];
            _creationTime = DateTime.UtcNow;
            _lastAccessTime = _creationTime;
        }
        
        public void Reset()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(IOResourceBuffer));
            
            Array.Clear(_data, 0, _data.Length);
            _accessCount = 0;
            _lastAccessTime = DateTime.UtcNow;
        }
        
        public bool IsExpired(DateTime currentTime)
        {
            return (currentTime - _lastAccessTime) > TimeSpan.FromMinutes(5) || // 5 minutes idle
                   _accessCount > 1000; // High usage buffer
        }
        
        internal void RecordAccess()
        {
            Interlocked.Increment(ref _accessCount);
            _lastAccessTime = DateTime.UtcNow;
        }
        
        public void Dispose()
        {
            if (_isDisposed) return;
            
            _isDisposed = true;
            Array.Clear(_data, 0, _data.Length);
        }
    }
    
    /// <summary>
    /// Handle for tracking resource lifecycle
    /// </summary>
    public class IOResourceHandle : IDisposable
    {
        private readonly string _id;
        private readonly object _resource;
        private readonly DateTime _creationTime;
        private readonly DateTime? _expirationTime;
        
        private volatile bool _isDisposed;
        
        public string Id => _id;
        public object Resource => _resource;
        public DateTime CreationTime => _creationTime;
        public DateTime? ExpirationTime => _expirationTime;
        public bool IsExpired => _expirationTime.HasValue && DateTime.UtcNow > _expirationTime.Value;
        public bool IsDisposed => _isDisposed;
        
        public IOResourceHandle(string id, object resource, TimeSpan? expiration = null)
        {
            _id = id;
            _resource = resource;
            _creationTime = DateTime.UtcNow;
            _expirationTime = expiration.HasValue ? _creationTime.Add(expiration.Value) : null;
        }
        
        public void Dispose()
        {
            if (_isDisposed) return;
            
            _isDisposed = true;
            
            // Dispose the resource if it implements IDisposable
            if (_resource is IDisposable disposableResource)
            {
                try
                {
                    disposableResource.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }
        }
    }
    
    /// <summary>
    /// Statistics for resource pool
    /// </summary>
    public class IOResourcePoolStatistics
    {
        public int TotalBuffersCreated { get; set; }
        public int TotalBuffersReused { get; set; }
        public int TotalBuffersDiscarded { get; set; }
        public double ReuseRate { get; set; }
        public Dictionary<string, int> PoolSizes { get; set; } = new();
        public int TotalActiveResources { get; set; }
        public int ActiveResourceCount { get; set; }
        public int MaxPooledBuffers { get; set; }
    }
    
    /// <summary>
    /// Snapshot of resource pool state
    /// </summary>
    public class IOResourcePoolSnapshot
    {
        public DateTime Timestamp { get; set; }
        public IOResourcePoolStatistics Statistics { get; set; } = new();
        public Dictionary<int, int[]> PoolContents { get; set; } = new();
        public Dictionary<string, string> ActiveResources { get; set; } = new();
    }
}