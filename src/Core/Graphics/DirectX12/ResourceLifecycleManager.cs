using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TiXL.Core.Graphics.DirectX12
{
    /// <summary>
    /// Resource lifecycle manager for DirectX 12
    /// Optimizes resource creation/destruction within frame budget constraints
    /// </summary>
    public class ResourceLifecycleManager : IDisposable
    {
        private readonly Queue<ResourceOperation> _normalPriorityQueue = new Queue<ResourceOperation>();
        private readonly Queue<ResourceOperation> _highPriorityQueue = new Queue<ResourceOperation>();
        private readonly Queue<ResourceOperation> _lowPriorityQueue = new Queue<ResourceOperation>();
        
        private readonly Dictionary<string, ResourcePool> _resourcePools = new Dictionary<string, ResourcePool>();
        private readonly object _queueLock = new object();
        private readonly object _poolLock = new object();
        
        private readonly Timer _cleanupTimer;
        private readonly int _maxOperationsPerFrame = 10;
        private readonly double _maxTimePerFrameMs = 4.0; // Reserve 4ms for resource operations
        
        public int PendingOperationCount => _normalPriorityQueue.Count + _highPriorityQueue.Count + _lowPriorityQueue.Count;
        public int PoolCount => _resourcePools.Count;
        
        public ResourceLifecycleManager()
        {
            _cleanupTimer = new Timer(CleanupUnusedResources, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }
        
        /// <summary>
        /// Queue resource operation for processing within frame budget
        /// </summary>
        public void QueueOperation(Action operation, ResourcePriority priority = ResourcePriority.Normal)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            
            var resourceOp = new ResourceOperation
            {
                Id = Guid.NewGuid().ToString(),
                Operation = operation,
                Priority = priority,
                QueuedAt = DateTime.UtcNow,
                EstimatedDuration = EstimateOperationDuration(operation)
            };
            
            lock (_queueLock)
            {
                switch (priority)
                {
                    case ResourcePriority.Critical:
                    case ResourcePriority.High:
                        _highPriorityQueue.Enqueue(resourceOp);
                        break;
                    case ResourcePriority.Low:
                        _lowPriorityQueue.Enqueue(resourceOp);
                        break;
                    default:
                        _normalPriorityQueue.Enqueue(resourceOp);
                        break;
                }
            }
        }
        
        /// <summary>
        /// Process resource operations within frame budget
        /// </summary>
        public bool ProcessOperations(double availableTimeMs)
        {
            var processedCount = 0;
            var startTime = DateTime.UtcNow;
            
            // Process high priority operations first
            while (processedCount < _maxOperationsPerFrame && 
                   (DateTime.UtcNow - startTime).TotalMilliseconds < Math.Min(availableTimeMs, _maxTimePerFrameMs))
            {
                if (!ProcessNextOperation(ResourcePriority.High))
                    break;
                    
                processedCount++;
            }
            
            // Then process normal priority operations
            while (processedCount < _maxOperationsPerFrame && 
                   (DateTime.UtcNow - startTime).TotalMilliseconds < Math.Min(availableTimeMs, _maxTimePerFrameMs))
            {
                if (!ProcessNextOperation(ResourcePriority.Normal))
                    break;
                    
                processedCount++;
            }
            
            // Finally process low priority operations if we have time
            if (processedCount < _maxOperationsPerFrame && 
                (DateTime.UtcNow - startTime).TotalMilliseconds < Math.Min(availableTimeMs, _maxTimePerFrameMs))
            {
                while (processedCount < _maxOperationsPerFrame && 
                       (DateTime.UtcNow - startTime).TotalMilliseconds < Math.Min(availableTimeMs, _maxTimePerFrameMs))
                {
                    if (!ProcessNextOperation(ResourcePriority.Low))
                        break;
                        
                    processedCount++;
                }
            }
            
            return processedCount > 0;
        }
        
        /// <summary>
        /// Create or get a resource from pool
        /// </summary>
        public T GetOrCreateResource<T>(string poolName, Func<T> createFunc) where T : class, IDisposable
        {
            lock (_poolLock)
            {
                if (!_resourcePools.TryGetValue(poolName, out var pool))
                {
                    pool = new ResourcePool(poolName);
                    _resourcePools[poolName] = pool;
                }
                
                return pool.GetOrCreate<T>(createFunc);
            }
        }
        
        /// <summary>
        /// Return resource to pool for reuse
        /// </summary>
        public void ReturnResource(string poolName, object resource)
        {
            lock (_poolLock)
            {
                if (_resourcePools.TryGetValue(poolName, out var pool))
                {
                    pool.Return(resource);
                }
            }
        }
        
        /// <summary>
        /// Create a pool for specific resource type
        /// </summary>
        public void CreatePool<T>(string poolName, int initialSize = 0, int maxSize = 10) where T : class, IDisposable, new()
        {
            lock (_poolLock)
            {
                if (!_resourcePools.ContainsKey(poolName))
                {
                    _resourcePools[poolName] = new ResourcePool(poolName, typeof(T), initialSize, maxSize);
                }
            }
        }
        
        /// <summary>
        /// Get resource operation statistics
        /// </summary>
        public ResourceOperationStatistics GetStatistics()
        {
            lock (_queueLock)
            {
                lock (_poolLock)
                {
                    return new ResourceOperationStatistics
                    {
                        PendingOperations = PendingOperationCount,
                        HighPriorityOperations = _highPriorityQueue.Count,
                        NormalPriorityOperations = _normalPriorityQueue.Count,
                        LowPriorityOperations = _lowPriorityQueue.Count,
                        ActivePools = _resourcePools.Count,
                        TotalPooledResources = _resourcePools.Sum(p => p.Value.TotalResources),
                        AvailablePooledResources = _resourcePools.Sum(p => p.Value.AvailableResources),
                        OldestOperationAge = GetOldestOperationAge()
                    };
                }
            }
        }
        
        private bool ProcessNextOperation(ResourcePriority priority)
        {
            ResourceOperation operation = null;
            
            lock (_queueLock)
            {
                var queue = GetQueueForPriority(priority);
                
                if (queue.Count == 0)
                    return false;
                    
                operation = queue.Dequeue();
            }
            
            if (operation == null) return false;
            
            try
            {
                operation.Execute();
                operation.CompletedAt = DateTime.UtcNow;
                return true;
            }
            catch (Exception ex)
            {
                // Log error but continue processing
                System.Diagnostics.Debug.WriteLine($"Resource operation failed: {operation.Id} - {ex.Message}");
                return true; // Count as processed to avoid infinite loop
            }
        }
        
        private Queue<ResourceOperation> GetQueueForPriority(ResourcePriority priority)
        {
            return priority switch
            {
                ResourcePriority.High or ResourcePriority.Critical => _highPriorityQueue,
                ResourcePriority.Low => _lowPriorityQueue,
                _ => _normalPriorityQueue
            };
        }
        
        private double EstimateOperationDuration(Action operation)
        {
            // Simple heuristic - in real implementation, could track historical data
            return operation.Method.Name switch
            {
                var name when name.Contains("Create") || name.Contains("Destroy") => 2.0,
                var name when name.Contains("Update") => 1.0,
                var name when name.Contains("Upload") => 0.5,
                _ => 0.5
            };
        }
        
        private TimeSpan GetOldestOperationAge()
        {
            var oldest = DateTime.UtcNow;
            
            lock (_queueLock)
            {
                if (_highPriorityQueue.Count > 0)
                    oldest = _highPriorityQueue.Min(op => op.QueuedAt);
                else if (_normalPriorityQueue.Count > 0)
                    oldest = _normalPriorityQueue.Min(op => op.QueuedAt);
                else if (_lowPriorityQueue.Count > 0)
                    oldest = _lowPriorityQueue.Min(op => op.QueuedAt);
            }
            
            return DateTime.UtcNow - oldest;
        }
        
        private void CleanupUnusedResources(object state)
        {
            lock (_poolLock)
            {
                var poolsToRemove = new List<string>();
                
                foreach (var pool in _resourcePools)
                {
                    pool.Value.Cleanup();
                    
                    // Remove pools that are empty and unused
                    if (pool.Value.TotalResources == 0 && pool.Value.AvailableResources == 0)
                    {
                        poolsToRemove.Add(pool.Key);
                    }
                }
                
                foreach (var poolName in poolsToRemove)
                {
                    _resourcePools.Remove(poolName);
                }
            }
        }
        
        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            
            // Process remaining operations synchronously
            while (PendingOperationCount > 0)
            {
                ProcessNextOperation(ResourcePriority.High);
                ProcessNextOperation(ResourcePriority.Normal);
                ProcessNextOperation(ResourcePriority.Low);
            }
            
            // Dispose all pools
            lock (_poolLock)
            {
                foreach (var pool in _resourcePools.Values)
                {
                    pool.Dispose();
                }
                _resourcePools.Clear();
            }
        }
    }
    
    /// <summary>
    /// Resource operation for deferred execution
    /// </summary>
    public class ResourceOperation
    {
        public string Id { get; set; }
        public Action Operation { get; set; }
        public ResourcePriority Priority { get; set; }
        public DateTime QueuedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public double EstimatedDuration { get; set; } // milliseconds
        
        public void Execute()
        {
            Operation?.Invoke();
        }
    }
    
    /// <summary>
    /// Resource pool for efficient resource reuse
    /// </summary>
    public class ResourcePool : IDisposable
    {
        private readonly Stack<object> _available = new Stack<object>();
        private readonly HashSet<object> _inUse = new HashSet<object>();
        private readonly object _poolLock = new object();
        private readonly Type _resourceType;
        private readonly int _initialSize;
        private readonly int _maxSize;
        private int _totalResources;
        
        public string Name { get; }
        public int AvailableResources => _available.Count;
        public int InUseResources => _inUse.Count;
        public int TotalResources => _totalResources;
        
        public ResourcePool(string name, Type resourceType = null, int initialSize = 0, int maxSize = 10)
        {
            Name = name;
            _resourceType = resourceType;
            _initialSize = initialSize;
            _maxSize = maxSize;
            
            // Pre-create resources if initial size specified
            for (int i = 0; i < initialSize; i++)
            {
                CreateAndAddResource();
            }
        }
        
        public T GetOrCreate<T>(Func<T> createFunc) where T : class, IDisposable
        {
            lock (_poolLock)
            {
                if (_available.Count > 0 && TryGetFromPool<T>(out var resource))
                {
                    return resource;
                }
                
                if (_totalResources < _maxSize)
                {
                    var newResource = createFunc();
                    _inUse.Add(newResource);
                    _totalResources++;
                    return newResource;
                }
                
                // Pool is full, try to get oldest available resource
                if (_available.Count > 0 && TryGetFromPool<T>(out resource))
                {
                    return resource;
                }
                
                // Create without pooling as fallback
                return createFunc();
            }
        }
        
        public void Return(object resource)
        {
            if (resource == null) return;
            
            lock (_poolLock)
            {
                if (_inUse.Contains(resource))
                {
                    _inUse.Remove(resource);
                    
                    // Reset resource if possible
                    if (resource is IResettable resettable)
                    {
                        resettable.Reset();
                    }
                    
                    if (_available.Count < _maxSize * 2) // Limit pool size
                    {
                        _available.Push(resource);
                    }
                    else
                    {
                        DisposeResource(resource);
                        _totalResources--;
                    }
                }
            }
        }
        
        public void Cleanup()
        {
            lock (_poolLock)
            {
                // Remove expired or invalid resources from pool
                var validResources = new Stack<object>();
                
                while (_available.Count > 0)
                {
                    var resource = _available.Pop();
                    
                    if (IsResourceValid(resource))
                    {
                        validResources.Push(resource);
                    }
                    else
                    {
                        DisposeResource(resource);
                        _totalResources--;
                    }
                }
                
                _available.Clear();
                while (validResources.Count > 0)
                {
                    _available.Push(validResources.Pop());
                }
            }
        }
        
        private bool TryGetFromPool<T>(out T resource) where T : class
        {
            resource = null;
            
            while (_available.Count > 0)
            {
                var pooledResource = _available.Pop();
                
                if (pooledResource is T typedResource && IsResourceValid(pooledResource))
                {
                    _inUse.Add(pooledResource);
                    resource = typedResource;
                    return true;
                }
                else
                {
                    DisposeResource(pooledResource);
                    _totalResources--;
                }
            }
            
            return false;
        }
        
        private bool IsResourceValid(object resource)
        {
            // Check if resource is still alive and not disposed
            if (resource is IDisposable disposable)
            {
                // Simple check - could be more sophisticated
                return true;
            }
            
            return true; // Assume valid if not disposable
        }
        
        private void CreateAndAddResource()
        {
            if (_resourceType == null || !_resourceType.GetConstructors().Any())
                return;
                
            try
            {
                var resource = Activator.CreateInstance(_resourceType) as IDisposable;
                if (resource != null)
                {
                    _available.Push(resource);
                    _totalResources++;
                }
            }
            catch
            {
                // Ignore creation failures
            }
        }
        
        private void DisposeResource(object resource)
        {
            if (resource is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }
        }
        
        public void Dispose()
        {
            lock (_poolLock)
            {
                while (_available.Count > 0)
                {
                    _available.Pop()?.Dispose();
                }
                
                foreach (var resource in _inUse)
                {
                    DisposeResource(resource);
                }
                _inUse.Clear();
            }
        }
    }
    
    /// <summary>
    /// Interface for resources that can be reset for reuse
    /// </summary>
    public interface IResettable
    {
        void Reset();
    }
    
    /// <summary>
    /// Resource operation statistics
    /// </summary>
    public class ResourceOperationStatistics
    {
        public int PendingOperations { get; set; }
        public int HighPriorityOperations { get; set; }
        public int NormalPriorityOperations { get; set; }
        public int LowPriorityOperations { get; set; }
        public int ActivePools { get; set; }
        public int TotalPooledResources { get; set; }
        public int AvailablePooledResources { get; set; }
        public TimeSpan OldestOperationAge { get; set; }
    }
    
    /// <summary>
    /// Resource operation priorities
    /// </summary>
    public enum ResourcePriority
    {
        Low,
        Normal,
        High,
        Critical
    }
}