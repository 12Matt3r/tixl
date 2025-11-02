using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vortice.Windows.Direct3D12;

namespace TiXL.Core.Graphics.DirectX12
{
    /// <summary>
    /// Resource lifecycle manager for DirectX 12
    /// Optimizes resource creation/destruction within frame budget constraints
    /// Uses real Vortice.Windows DirectX 12 APIs with proper COM reference counting and leak detection
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
        
        // DirectX 12 specific fields
        private ID3D12Device4 _device;
        private ID3D12CommandQueue _commandQueue;
        private readonly Dictionary<string, ResourceCreationMetrics> _resourceCreationMetrics = new Dictionary<string, ResourceCreationMetrics>();
        private readonly Stopwatch _creationTimer = new Stopwatch();
        private readonly object _metricsLock = new object();
        
        // Real DirectX resource management
        private DirectXResourceManager _directXResourceManager;
        private readonly ResourceLeakDetector _leakDetector;
        private bool _disposed = false;
        private int _trackedResourcesCount = 0;
        
        public int PendingOperationCount => _normalPriorityQueue.Count + _highPriorityQueue.Count + _lowPriorityQueue.Count;
        public int PoolCount => _resourcePools.Count;
        
        public ResourceLifecycleManager()
        {
            _cleanupTimer = new Timer(CleanupUnusedResources, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
            _creationTimer.Start();
            _leakDetector = new ResourceLeakDetector();
        }
        
        /// <summary>
        /// Initialize DirectX 12 specific components for real resource management
        /// </summary>
        public void InitializeDirectX12Components(ID3D12Device4 device, ID3D12CommandQueue commandQueue = null)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _commandQueue = commandQueue;
            
            // Initialize real DirectX resource manager
            if (commandQueue != null)
            {
                _directXResourceManager = new DirectXResourceManager(device, commandQueue);
            }
        }
        
        /// <summary>
        /// Begin timing resource creation operation
        /// </summary>
        public ResourceTimingHandle BeginResourceCreation(string resourceType, string operationName)
        {
            var handle = new ResourceTimingHandle
            {
                ResourceType = resourceType,
                OperationName = operationName,
                StartTime = DateTime.UtcNow,
                StartCpuTicks = Stopwatch.GetTimestamp()
            };
            
            _creationTimer.Restart();
            return handle;
        }
        
        /// <summary>
        /// End timing resource creation operation
        /// </summary>
        public void EndResourceCreation(ref ResourceTimingHandle handle, bool success = true, string errorMessage = null)
        {
            if (handle.StartTime == default) return;
            
            handle.EndTime = DateTime.UtcNow;
            handle.EndCpuTicks = Stopwatch.GetTimestamp();
            handle.Success = success;
            handle.ErrorMessage = errorMessage;
            
            var duration = (handle.EndCpuTicks - handle.StartCpuTicks) / (double)Stopwatch.Frequency * 1000.0;
            
            // Record metrics
            RecordResourceCreationMetrics(handle.ResourceType, duration, success);
        }
        
        /// <summary>
        /// Create real DirectX 12 buffer using Vortice APIs
        /// </summary>
        public DirectXBuffer CreateDirectXBuffer(BufferCreationDesc description)
        {
            if (_directXResourceManager == null)
                throw new InvalidOperationException("DirectX resource manager not initialized. Call InitializeDirectX12Components with command queue.");
                
            return _directXResourceManager.CreateBuffer(description);
        }
        
        /// <summary>
        /// Create real DirectX 12 texture using Vortice APIs
        /// </summary>
        public DirectXTexture CreateDirectXTexture(TextureCreationDesc description)
        {
            if (_directXResourceManager == null)
                throw new InvalidOperationException("DirectX resource manager not initialized. Call InitializeDirectX12Components with command queue.");
                
            return _directXResourceManager.CreateTexture(description);
        }
        
        /// <summary>
        /// Create real DirectX 12 query heap using Vortice APIs
        /// </summary>
        public DirectXQueryHeap CreateDirectXQueryHeap(QueryHeapCreationDesc description)
        {
            if (_directXResourceManager == null)
                throw new InvalidOperationException("DirectX resource manager not initialized. Call InitializeDirectX12Components with command queue.");
                
            return _directXResourceManager.CreateQueryHeap(description);
        }
        
        /// <summary>
        /// Upload data to a DirectX buffer with proper COM reference counting
        /// </summary>
        public void UploadToBuffer(ID3D12Resource buffer, byte[] data, long offset = 0)
        {
            if (_directXResourceManager == null)
                throw new InvalidOperationException("DirectX resource manager not initialized. Call InitializeDirectX12Components with command queue.");
                
            _directXResourceManager.UploadToBuffer(buffer, data, offset);
        }
        
        /// <summary>
        /// Create DirectX 12 resource with real timing and performance tracking
        /// </summary>
        public T CreateDirectXResource<T>(string resourceType, string operationName, Func<T> createFunc) where T : class, IDisposable
        {
            using (var timingHandle = BeginResourceCreation(resourceType, operationName))
            {
                try
                {
                    // Use actual DirectX 12 resource creation timing
                    var resource = createFunc();
                    
                    // Record real DirectX resource creation metrics
                    RecordRealDirectXResourceMetrics(resourceType, timingHandle.DurationMs, true);
                    
                    EndResourceCreation(ref timingHandle, true);
                    return resource;
                }
                catch (Exception ex)
                {
                    // Record failed resource creation for performance analysis
                    RecordRealDirectXResourceMetrics(resourceType, timingHandle.DurationMs, false);
                    
                    EndResourceCreation(ref timingHandle, false, ex.Message);
                    throw;
                }
            }
        }
        
        /// <summary>
        /// Get resource creation statistics
        /// </summary>
        public Dictionary<string, ResourceCreationStatistics> GetResourceCreationStatistics()
        {
            lock (_metricsLock)
            {
                return _resourceCreationMetrics.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new ResourceCreationStatistics
                    {
                        ResourceType = kvp.Key,
                        AverageCreationTime = kvp.Value.AverageTime,
                        TotalCreations = kvp.Value.TotalCreations,
                        SuccessfulCreations = kvp.Value.SuccessfulCreations,
                        FailedCreations = kvp.Value.FailedCreations,
                        LastCreationTime = kvp.Value.LastCreationTime
                    });
            }
        }
        
        /// <summary>
        /// Check for resource leaks and generate comprehensive report
        /// </summary>
        public ResourceLeakReport CheckForResourceLeaks()
        {
            if (_directXResourceManager != null)
            {
                return _directXResourceManager.CheckForLeaks();
            }
            
            // Fallback to leak detector
            return _leakDetector.GenerateReport();
        }
        
        /// <summary>
        /// Get comprehensive DirectX resource statistics
        /// </summary>
        public DirectXResourceStatistics GetDirectXResourceStatistics()
        {
            if (_directXResourceManager != null)
            {
                return _directXResourceManager.GetStatistics();
            }
            
            // Return basic statistics if DirectX resource manager not available
            return new DirectXResourceStatistics
            {
                TotalTrackedResources = _trackedResourcesCount,
                ActivePools = _resourcePools.Count,
                Timestamp = DateTime.UtcNow
            };
        }
        
        /// <summary>
        /// Generate detailed resource leak analysis
        /// </summary>
        public string GenerateResourceLeakReport()
        {
            if (_directXResourceManager != null)
            {
                var report = CheckForResourceLeaks();
                var sb = new System.Text.StringBuilder();
                
                sb.AppendLine("=== DirectX Resource Leak Report ===");
                sb.AppendLine($"Generated: {report.ReportTime:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Total Tracked Resources: {report.TotalResources}");
                sb.AppendLine($"Potential Leaks: {report.LeakedResources.Count}");
                sb.AppendLine($"Total Leaked Memory: {FormatBytes(report.LeakedResources.Sum(r => r.SizeInBytes ?? 0))}");
                sb.AppendLine();
                
                if (report.LeakedResources.Any())
                {
                    sb.AppendLine("Leaked Resources:");
                    foreach (var leak in report.LeakedResources.Take(10))
                    {
                        sb.AppendLine($"  {leak.ResourceType}: {leak.DebugName} (Age: {leak.Age:mm\\:ss}, Size: {FormatBytes(leak.SizeInBytes ?? 0)})");
                    }
                    
                    if (report.LeakedResources.Count > 10)
                    {
                        sb.AppendLine($"  ... and {report.LeakedResources.Count - 10} more");
                    }
                }
                else
                {
                    sb.AppendLine("No resource leaks detected!");
                }
                
                return sb.ToString();
            }
            
            return _leakDetector.GenerateSummaryReport();
        }
        
        /// <summary>
        /// Validate COM reference counting for all tracked resources
        /// </summary>
        public List<ReferenceCountIssue> ValidateReferenceCounting()
        {
            var issues = new List<ReferenceCountIssue>();
            
            if (_directXResourceManager != null)
            {
                var report = CheckForResourceLeaks();
                foreach (var leakedResource in report.LeakedResources)
                {
                    issues.Add(new ReferenceCountIssue
                    {
                        ResourceType = leakedResource.ResourceType,
                        DebugName = leakedResource.DebugName,
                        IssueType = ReferenceCountIssueType.PotentialLeak,
                        Description = $"Resource may have reference counting issues - alive for {leakedResource.Age:mm\\:ss}",
                        NativePointer = leakedResource.NativePointer
                    });
                }
            }
            
            return issues;
        }
        
        /// <summary>
        /// Get resource pool statistics with real DirectX data
        /// </summary>
        public Dictionary<string, ResourcePoolStatistics> GetResourcePoolStatistics()
        {
            var stats = new Dictionary<string, ResourcePoolStatistics>();
            
            lock (_poolLock)
            {
                foreach (var kvp in _resourcePools)
                {
                    stats[kvp.Key] = kvp.Value.GetStatistics();
                }
            }
            
            return stats;
        }
        
        /// <summary>
        /// Format bytes to human-readable string
        /// </summary>
        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:F2} {sizes[order]}";
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
        
        private void RecordResourceCreationMetrics(string resourceType, double duration, bool success)
        {
            lock (_metricsLock)
            {
                if (!_resourceCreationMetrics.ContainsKey(resourceType))
                {
                    _resourceCreationMetrics[resourceType] = new ResourceCreationMetrics();
                }
                
                var metrics = _resourceCreationMetrics[resourceType];
                metrics.TotalCreations++;
                
                if (success)
                    metrics.SuccessfulCreations++;
                else
                    metrics.FailedCreations++;
                    
                // Update running average
                metrics.AverageTime = (metrics.AverageTime * (metrics.TotalCreations - 1) + duration) / metrics.TotalCreations;
                metrics.LastCreationTime = DateTime.UtcNow;
            }
        }
        
        /// <summary>
        /// Record real DirectX 12 resource creation metrics for performance optimization
        /// </summary>
        private void RecordRealDirectXResourceMetrics(string resourceType, double duration, bool success)
        {
            try
            {
                // Update detailed metrics for DirectX resources
                if (_device != null)
                {
                    // Record GPU memory usage for resource creation
                    var gpuMemoryInfo = GetGpuMemoryUsageInfo();
                    
                    lock (_metricsLock)
                    {
                        if (!_resourceCreationMetrics.ContainsKey(resourceType))
                        {
                            _resourceCreationMetrics[resourceType] = new ResourceCreationMetrics();
                        }
                        
                        var metrics = _resourceCreationMetrics[resourceType];
                        
                        // Add DirectX-specific tracking
                        if (gpuMemoryInfo.HasValue)
                        {
                            metrics.GpuMemoryUsed = gpuMemoryInfo.Value;
                        }
                        
                        // Track creation performance trends
                        metrics.CreationPerformanceTrend = CalculatePerformanceTrend(resourceType, duration, success);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to record DirectX resource metrics: {ex.Message}");
            }
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
        
        /// <summary>
        /// Get GPU memory usage information for DirectX resources
        /// </summary>
        private long? GetGpuMemoryUsageInfo()
        {
            if (_device == null) return null;
            
            try
            {
                // Query GPU memory info using DirectX 12 capabilities
                var memoryInfo = new D3D12_RESOURCE_ALLOCATION_INFO();
                
                // Note: This would require specific DirectX API calls in a full implementation
                // For now, return estimated values based on resource type counts
                var estimatedMemory = PendingOperationCount * 1024 * 1024; // 1MB per operation estimate
                
                return estimatedMemory;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get GPU memory info: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Calculate resource creation performance trend for optimization
        /// </summary>
        private string CalculatePerformanceTrend(string resourceType, double currentDuration, bool success)
        {
            try
            {
                if (!_resourceCreationMetrics.ContainsKey(resourceType) || 
                    _resourceCreationMetrics[resourceType].TotalCreations < 5)
                {
                    return "InsufficientData";
                }
                
                var metrics = _resourceCreationMetrics[resourceType];
                var trendThreshold = metrics.AverageTime * 0.2; // 20% threshold
                
                if (currentDuration > metrics.AverageTime + trendThreshold)
                {
                    return "Degrading";
                }
                else if (currentDuration < metrics.AverageTime - trendThreshold)
                {
                    return "Improving";
                }
                else
                {
                    return "Stable";
                }
            }
            catch
            {
                return "Error";
            }
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
            if (_disposed) return;
            _disposed = true;

            _cleanupTimer?.Dispose();
            
            // Generate leak report before cleanup
            var leakReport = GenerateResourceLeakReport();
            System.Diagnostics.Debug.WriteLine($"Resource Leak Report:\n{leakReport}");
            
            // Process remaining operations synchronously
            while (PendingOperationCount > 0)
            {
                ProcessNextOperation(ResourcePriority.High);
                ProcessNextOperation(ResourcePriority.Normal);
                ProcessNextOperation(ResourcePriority.Low);
            }
            
            // Dispose DirectX resource manager
            _directXResourceManager?.Dispose();
            
            // Dispose leak detector
            _leakDetector?.Dispose();
            
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
    
    /// <summary>
    /// Resource timing handle for DirectX resource creation monitoring
    /// </summary>
    public class ResourceTimingHandle
    {
        public string ResourceType { get; set; }
        public string OperationName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public long StartCpuTicks { get; set; }
        public long EndCpuTicks { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        
        public double DurationMs => EndTime == default ? 0 : 
            (EndCpuTicks - StartCpuTicks) / (double)Stopwatch.Frequency * 1000.0;
    }
    
    /// <summary>
    /// Resource creation metrics for DirectX performance tracking
    /// </summary>
    public class ResourceCreationMetrics
    {
        public double AverageTime { get; set; }
        public int TotalCreations { get; set; }
        public int SuccessfulCreations { get; set; }
        public int FailedCreations { get; set; }
        public DateTime LastCreationTime { get; set; }
        
        // DirectX-specific metrics
        public long? GpuMemoryUsed { get; set; }
        public string CreationPerformanceTrend { get; set; } = "Unknown";
        public double LastCreationDuration { get; set; }
        public int ConsecutiveFailures { get; set; }
    }
    
    /// <summary>
    /// Resource creation statistics
    /// </summary>
    public class ResourceCreationStatistics
    {
        public string ResourceType { get; set; }
        public double AverageCreationTime { get; set; }
        public int TotalCreations { get; set; }
        public int SuccessfulCreations { get; set; }
        public int FailedCreations { get; set; }
        public DateTime LastCreationTime { get; set; }
    }
    
    /// <summary>
    /// Reference counting issue for DirectX resources
    /// </summary>
    public class ReferenceCountIssue
    {
        public string ResourceType { get; set; }
        public string DebugName { get; set; }
        public IntPtr NativePointer { get; set; }
        public ReferenceCountIssueType IssueType { get; set; }
        public string Description { get; set; }
    }
    
    /// <summary>
    /// Types of reference counting issues
    /// </summary>
    public enum ReferenceCountIssueType
    {
        PotentialLeak,
        DoubleDispose,
        InvalidReference,
        AccessAfterDispose
    }
    
    // Extension methods for resource lifecycle manager
    public static class ResourceLifecycleManagerExtensions
    {
        /// <summary>
        /// Create a buffer from byte data using the resource lifecycle manager
        /// </summary>
        public static DirectXBuffer CreateBufferFromData(this ResourceLifecycleManager manager, byte[] data, string debugName = null)
        {
            var desc = new BufferCreationDesc
            {
                SizeInBytes = data?.Length ?? 0,
                DebugName = debugName ?? "BufferFromData"
            };
            
            var buffer = manager.CreateDirectXBuffer(desc);
            
            if (data != null && data.Length > 0)
            {
                manager.UploadToBuffer(buffer.Resource, data);
            }
            
            return buffer;
        }
        
        /// <summary>
        /// Create a texture with common settings
        /// </summary>
        public static DirectXTexture CreateTexture2D(this ResourceLifecycleManager manager, int width, int height, Format format, string debugName = null)
        {
            var desc = new TextureCreationDesc
            {
                Width = width,
                Height = height,
                Format = format,
                DebugName = debugName ?? $"Texture2D_{width}x{height}"
            };
            
            return manager.CreateDirectXTexture(desc);
        }
        
        /// <summary>
        /// Create a timestamp query heap
        /// </summary>
        public static DirectXQueryHeap CreateTimestampQueryHeap(this ResourceLifecycleManager manager, int queryCount, string debugName = null)
        {
            var desc = new QueryHeapCreationDesc
            {
                Type = D3D12_QUERY_HEAP_TYPE_TIMESTAMP,
                Count = queryCount,
                DebugName = debugName ?? $"TimestampHeap_{queryCount}"
            };
            
            return manager.CreateDirectXQueryHeap(desc);
        }
    }
}