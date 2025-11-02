using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Vortice.Windows.Direct3D12;

namespace TiXL.Core.Graphics.DirectX12
{
    /// <summary>
    /// Resource pool for DirectX 12 resources with proper COM reference counting
    /// Manages the lifecycle of DirectX resources for reuse and performance optimization
    /// </summary>
    public class ResourcePool : IDisposable
    {
        private readonly string _name;
        private readonly Func<ID3D12Resource> _createResourceFunc;
        private readonly Stack<ID3D12Resource> _availableResources = new();
        private readonly HashSet<ID3D12Resource> _inUseResources = new();
        private readonly object _poolLock = new();
        private readonly int _initialSize;
        private readonly int _maxSize;
        private int _totalResourcesCreated = 0;
        private bool _disposed = false;

        public string Name => _name;
        public int AvailableResources => _availableResources.Count;
        public int InUseResources => _inUseResources.Count;
        public int TotalResources => _totalResourcesCreated;
        public int Capacity => _maxSize;

        public ResourcePool(string name, Func<ID3D12Resource> createResourceFunc, int initialSize = 0, int maxSize = 10)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _createResourceFunc = createResourceFunc ?? throw new ArgumentNullException(nameof(createResourceFunc));
            _initialSize = Math.Max(0, initialSize);
            _maxSize = Math.Max(1, maxSize);

            // Pre-create resources if initial size specified
            for (int i = 0; i < _initialSize; i++)
            {
                CreateAndAddResource();
            }
        }

        /// <summary>
        /// Get a resource from the pool or create a new one if pool is empty
        /// </summary>
        public ID3D12Resource GetResource()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ResourcePool));

            lock (_poolLock)
            {
                // Try to get from available resources first
                if (_availableResources.Count > 0)
                {
                    var resource = _availableResources.Pop();
                    if (IsResourceValid(resource))
                    {
                        _inUseResources.Add(resource);
                        return resource;
                    }
                    else
                    {
                        // Resource is invalid, dispose it and try again
                        DisposeResource(resource);
                        _totalResourcesCreated--;
                        return GetResource(); // Recursive call to try again
                    }
                }

                // Pool is empty or no valid resources, create new one if under capacity
                if (_totalResourcesCreated < _maxSize)
                {
                    var resource = CreateAndAddResource();
                    _inUseResources.Add(resource);
                    return resource;
                }

                // Pool is at capacity, try to get oldest available resource
                if (_availableResources.Count > 0)
                {
                    var resource = _availableResources.Pop();
                    _inUseResources.Add(resource);
                    return resource;
                }

                // Pool is full, create without pooling as fallback
                return _createResourceFunc();
            }
        }

        /// <summary>
        /// Return a resource to the pool for reuse
        /// </summary>
        public void ReturnResource(ID3D12Resource resource)
        {
            if (resource == null) return;

            lock (_poolLock)
            {
                if (_inUseResources.Contains(resource))
                {
                    _inUseResources.Remove(resource);

                    // Reset resource if it implements IResettable
                    if (resource is IResettable resettable)
                    {
                        try
                        {
                            resettable.Reset();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to reset resource in pool {_name}: {ex.Message}");
                            // Dispose the resource if reset fails
                            DisposeResource(resource);
                            _totalResourcesCreated--;
                            return;
                        }
                    }

                    // Limit pool size to prevent memory bloat
                    if (_availableResources.Count < _maxSize * 2)
                    {
                        _availableResources.Push(resource);
                    }
                    else
                    {
                        // Pool is too large, dispose the resource
                        DisposeResource(resource);
                        _totalResourcesCreated--;
                    }
                }
            }
        }

        /// <summary>
        /// Remove and dispose of invalid resources from the pool
        /// </summary>
        public void CleanupInvalidResources()
        {
            lock (_poolLock)
            {
                var validResources = new Stack<ID3D12Resource>();

                while (_availableResources.Count > 0)
                {
                    var resource = _availableResources.Pop();

                    if (IsResourceValid(resource))
                    {
                        validResources.Push(resource);
                    }
                    else
                    {
                        DisposeResource(resource);
                        _totalResourcesCreated--;
                    }
                }

                // Restore valid resources
                while (validResources.Count > 0)
                {
                    _availableResources.Push(validResources.Pop());
                }
            }
        }

        /// <summary>
        /// Pre-warm the pool by creating resources
        /// </summary>
        public void PreWarm(int count)
        {
            if (_disposed) return;

            count = Math.Min(count, _maxSize - _totalResourcesCreated);
            
            for (int i = 0; i < count; i++)
            {
                var resource = CreateAndAddResource();
                _availableResources.Push(resource);
            }
        }

        /// <summary>
        /// Clear the pool and dispose all resources
        /// </summary>
        public void Clear()
        {
            lock (_poolLock)
            {
                // Dispose all available resources
                while (_availableResources.Count > 0)
                {
                    var resource = _availableResources.Pop();
                    DisposeResource(resource);
                    _totalResourcesCreated--;
                }

                // Note: We don't dispose in-use resources here as they may still be in use
                // They will be disposed when returned or when the pool is disposed
            }
        }

        /// <summary>
        /// Get statistics about the pool state
        /// </summary>
        public ResourcePoolStatistics GetStatistics()
        {
            lock (_poolLock)
            {
                return new ResourcePoolStatistics
                {
                    PoolName = _name,
                    AvailableResources = _availableResources.Count,
                    InUseResources = _inUseResources.Count,
                    TotalResourcesCreated = _totalResourcesCreated,
                    Capacity = _maxSize,
                    UtilizationRate = _totalResourcesCreated > 0 ? (double)_inUseResources.Count / _totalResourcesCreated : 0,
                    FillRate = _maxSize > 0 ? (double)_availableResources.Count / _maxSize : 0,
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        private ID3D12Resource CreateAndAddResource()
        {
            try
            {
                var resource = _createResourceFunc();
                _totalResourcesCreated++;
                return resource;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create resource for pool {_name}: {ex.Message}", ex);
            }
        }

        private bool IsResourceValid(ID3D12Resource resource)
        {
            if (resource == null) return false;

            try
            {
                // Check if the resource pointer is still valid
                if (resource.NativePointer == IntPtr.Zero) return false;

                // Try to access a basic property to verify the COM object is still alive
                var desc = resource.Description;
                
                // Additional validation could be added here based on resource type
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Resource validation failed in pool {_name}: {ex.Message}");
                return false;
            }
        }

        private void DisposeResource(ID3D12Resource resource)
        {
            if (resource == null) return;

            try
            {
                // Ensure we're on the correct thread for COM operations
                if (System.Threading.Thread.CurrentThread.GetApartmentState() != System.Threading.ApartmentState.MTA)
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Disposing COM resource {_name} from non-MTA thread");
                }

                resource.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disposing resource in pool {_name}: {ex.Message}");
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                lock (_poolLock)
                {
                    // Dispose all available resources
                    while (_availableResources.Count > 0)
                    {
                        var resource = _availableResources.Pop();
                        DisposeResource(resource);
                        _totalResourcesCreated--;
                    }

                    // Dispose all in-use resources
                    foreach (var resource in _inUseResources)
                    {
                        DisposeResource(resource);
                        _totalResourcesCreated--;
                    }
                    _inUseResources.Clear();
                }
            }

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
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
    /// Statistics about resource pool state
    /// </summary>
    public class ResourcePoolStatistics
    {
        public string PoolName { get; set; }
        public int AvailableResources { get; set; }
        public int InUseResources { get; set; }
        public int TotalResourcesCreated { get; set; }
        public int Capacity { get; set; }
        public double UtilizationRate { get; set; } // 0.0 to 1.0
        public double FillRate { get; set; } // 0.0 to 1.0
        public DateTime Timestamp { get; set; }

        public bool IsFull => AvailableResources == 0 && InUseResources >= Capacity;
        public bool IsEmpty => AvailableResources == 0 && InUseResources == 0;
        public bool HasCapacity => TotalResourcesCreated < Capacity;
    }
}