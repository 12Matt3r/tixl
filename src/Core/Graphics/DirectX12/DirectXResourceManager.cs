using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Vortice.Windows.Direct3D12;
using Vortice.Windows;

namespace TiXL.Core.Graphics.DirectX12
{
    /// <summary>
    /// Real DirectX 12 resource management system using Vortice.Windows APIs
    /// Provides proper COM reference counting, resource leak detection, and resource pooling
    /// </summary>
    public class DirectXResourceManager : IDisposable
    {
        private readonly ID3D12Device4 _device;
        private readonly ID3D12CommandQueue _commandQueue;
        private readonly ResourceLeakDetector _leakDetector;
        private readonly Dictionary<string, ResourcePool> _resourcePools = new();
        private readonly object _poolLock = new object();
        private readonly Dictionary<IntPtr, TrackedResource> _trackedResources = new();
        private readonly object _trackingLock = new object();
        private bool _disposed = false;

        public DirectXResourceManager(ID3D12Device4 device, ID3D12CommandQueue commandQueue)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _commandQueue = commandQueue ?? throw new ArgumentNullException(nameof(commandQueue));
            _leakDetector = new ResourceLeakDetector();
            
            // Initialize default resource pools
            InitializeResourcePools();
        }

        /// <summary>
        /// Create a real DirectX 12 buffer using Vortice APIs
        /// </summary>
        public DirectXBuffer CreateBuffer(BufferCreationDesc description)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DirectXResourceManager));

            using (var timingHandle = BeginResourceCreation("Buffer", description.DebugName))
            {
                try
                {
                    // Create resource description for buffer
                    var resourceDesc = new ResourceDescription1
                    {
                        Dimension = ResourceDimension.Buffer,
                        Width = description.SizeInBytes,
                        Height = 1,
                        DepthOrArraySize = 1,
                        MipLevels = 1,
                        Format = Format.Unknown,
                        Layout = TextureLayout.RowMajor,
                        Flags = description.Flags,
                        SampleDescription = new SampleDescription(1, 0)
                    };

                    // Create the actual buffer using DirectX 12
                    var buffer = _device.CreateCommittedResource(
                        description.HeapProperties,
                        description.HeapFlags,
                        resourceDesc,
                        description.InitialState);

                    // Set debug name for leak detection
                    if (!string.IsNullOrEmpty(description.DebugName))
                    {
                        buffer.Name = description.DebugName;
                    }

                    // Track the resource for leak detection
                    var trackedResource = new TrackedResource
                    {
                        Resource = buffer,
                        CreationTime = DateTime.UtcNow,
                        ResourceType = "Buffer",
                        DebugName = description.DebugName,
                        SizeInBytes = description.SizeInBytes
                    };

                    TrackResource(buffer, trackedResource);
                    
                    // Add to appropriate pool
                    AddToResourcePool(buffer, description);

                    EndResourceCreation(ref timingHandle, true);
                    return new DirectXBuffer(buffer, description.SizeInBytes, description.DebugName);
                }
                catch (Exception ex)
                {
                    EndResourceCreation(ref timingHandle, false, ex.Message);
                    throw new DirectXResourceCreationException($"Failed to create buffer: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// Create a real DirectX 12 texture using Vortice APIs
        /// </summary>
        public DirectXTexture CreateTexture(TextureCreationDesc description)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DirectXResourceManager));

            using (var timingHandle = BeginResourceCreation("Texture", description.DebugName))
            {
                try
                {
                    // Create resource description for texture
                    var resourceDesc = new ResourceDescription1
                    {
                        Dimension = description.Dimension,
                        Width = description.Width,
                        Height = description.Height,
                        DepthOrArraySize = description.ArraySize,
                        MipLevels = description.MipLevels,
                        Format = description.Format,
                        Layout = TextureLayout.Unknown,
                        Flags = description.Flags,
                        SampleDescription = description.SampleDescription
                    };

                    // Create the actual texture using DirectX 12
                    var texture = _device.CreateCommittedResource(
                        description.HeapProperties,
                        description.HeapFlags,
                        resourceDesc,
                        description.InitialState);

                    // Set debug name for leak detection
                    if (!string.IsNullOrEmpty(description.DebugName))
                    {
                        texture.Name = description.DebugName;
                    }

                    // Track the resource for leak detection
                    var trackedResource = new TrackedResource
                    {
                        Resource = texture,
                        CreationTime = DateTime.UtcNow,
                        ResourceType = "Texture",
                        DebugName = description.DebugName,
                        Width = description.Width,
                        Height = description.Height,
                        Format = description.Format
                    };

                    TrackResource(texture, trackedResource);
                    
                    // Add to appropriate pool
                    AddToResourcePool(texture, description);

                    EndResourceCreation(ref timingHandle, true);
                    return new DirectXTexture(texture, description.Width, description.Height, description.Format, description.DebugName);
                }
                catch (Exception ex)
                {
                    EndResourceCreation(ref timingHandle, false, ex.Message);
                    throw new DirectXResourceCreationException($"Failed to create texture: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// Create a real DirectX 12 query heap for performance monitoring
        /// </summary>
        public DirectXQueryHeap CreateQueryHeap(QueryHeapCreationDesc description)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DirectXResourceManager));

            using (var timingHandle = BeginResourceCreation("QueryHeap", description.DebugName))
            {
                try
                {
                    // Create the actual query heap using DirectX 12
                    var queryHeap = _device.CreateQueryHeap(new D3D12_QUERY_HEAP_DESC
                    {
                        Type = description.Type,
                        Count = description.Count
                    });

                    // Set debug name for leak detection
                    if (!string.IsNullOrEmpty(description.DebugName))
                    {
                        queryHeap.Name = description.DebugName;
                    }

                    // Track the resource for leak detection
                    var trackedResource = new TrackedResource
                    {
                        Resource = queryHeap,
                        CreationTime = DateTime.UtcNow,
                        ResourceType = "QueryHeap",
                        DebugName = description.DebugName,
                        QueryCount = description.Count
                    };

                    TrackResource(queryHeap, trackedResource);

                    EndResourceCreation(ref timingHandle, true);
                    return new DirectXQueryHeap(queryHeap, description.Type, description.Count, description.DebugName);
                }
                catch (Exception ex)
                {
                    EndResourceCreation(ref timingHandle, false, ex.Message);
                    throw new DirectXResourceCreationException($"Failed to create query heap: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// Upload data to a DirectX buffer with proper COM reference counting
        /// </summary>
        public void UploadToBuffer(ID3D12Resource buffer, byte[] data, long offset = 0)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (offset < 0 || offset + data.Length > buffer.Description.Width)
                throw new ArgumentOutOfRangeException(nameof(offset));

            try
            {
                // Create upload buffer
                var uploadBufferDesc = new ResourceDescription1
                {
                    Dimension = ResourceDimension.Buffer,
                    Width = data.Length,
                    Height = 1,
                    DepthOrArraySize = 1,
                    MipLevels = 1,
                    Format = Format.Unknown,
                    Layout = TextureLayout.RowMajor,
                    Flags = ResourceFlags.None,
                    SampleDescription = new SampleDescription(1, 0)
                };

                var uploadBuffer = _device.CreateCommittedResource(
                    new HeapProperties(HeapType.Upload),
                    HeapFlags.None,
                    uploadBufferDesc,
                    ResourceStates.GenericRead);

                try
                {
                    // Upload data to upload buffer
                    var uploadData = uploadBuffer.Map<byte>(0);
                    Array.Copy(data, 0, uploadData, 0, data.Length);
                    uploadBuffer.Unmap(0);

                    // Copy from upload buffer to target buffer using command list
                    var commandList = _device.CreateCommandList<D3D12_COMMAND_LIST_TYPE_COPY>(0);
                    try
                    {
                        commandList.CopyBufferRegion(buffer, offset, uploadBuffer, 0, data.Length);
                        commandList.Close();

                        // Execute the copy operation
                        _commandQueue.ExecuteCommandList(commandList);
                    }
                    finally
                    {
                        commandList.Dispose();
                    }
                }
                finally
                {
                    uploadBuffer.Dispose();
                }
            }
            catch (Exception ex)
            {
                throw new DirectXResourceException($"Failed to upload data to buffer: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Check for resource leaks and generate report
        /// </summary>
        public ResourceLeakReport CheckForLeaks()
        {
            lock (_trackingLock)
            {
                return _leakDetector.GenerateReport(_trackedResources);
            }
        }

        /// <summary>
        /// Get comprehensive resource statistics
        /// </summary>
        public DirectXResourceStatistics GetStatistics()
        {
            lock (_trackingLock)
            {
                lock (_poolLock)
                {
                    var stats = new DirectXResourceStatistics
                    {
                        TotalTrackedResources = _trackedResources.Count,
                        ActivePools = _resourcePools.Count,
                        Timestamp = DateTime.UtcNow
                    };

                    // Categorize resources by type
                    foreach (var tracked in _trackedResources.Values)
                    {
                        switch (tracked.ResourceType)
                        {
                            case "Buffer":
                                stats.BufferCount++;
                                stats.TotalBufferMemory += tracked.SizeInBytes ?? 0;
                                break;
                            case "Texture":
                                stats.TextureCount++;
                                stats.TotalTextureMemory += CalculateTextureMemorySize(tracked);
                                break;
                            case "QueryHeap":
                                stats.QueryHeapCount++;
                                break;
                        }
                    }

                    // Add pool statistics
                    foreach (var pool in _resourcePools.Values)
                    {
                        stats.TotalPooledResources += pool.TotalResources;
                        stats.AvailablePooledResources += pool.AvailableResources;
                    }

                    return stats;
                }
            }
        }

        private void InitializeResourcePools()
        {
            lock (_poolLock)
            {
                // Create default resource pools
                _resourcePools["SmallBuffer"] = new ResourcePool("SmallBuffer", () => 
                    CreateBuffer(new BufferCreationDesc
                    {
                        SizeInBytes = 1024,
                        HeapProperties = new HeapProperties(HeapType.Default),
                        Flags = ResourceFlags.None,
                        InitialState = ResourceStates.Common,
                        DebugName = "PooledSmallBuffer"
                    }), 4, 16);

                _resourcePools["MediumBuffer"] = new ResourcePool("MediumBuffer", () => 
                    CreateBuffer(new BufferCreationDesc
                    {
                        SizeInBytes = 4096,
                        HeapProperties = new HeapProperties(HeapType.Default),
                        Flags = ResourceFlags.None,
                        InitialState = ResourceStates.Common,
                        DebugName = "PooledMediumBuffer"
                    }), 2, 8);

                _resourcePools["Texture2D"] = new ResourcePool("Texture2D", () => 
                    CreateTexture(new TextureCreationDesc
                    {
                        Dimension = ResourceDimension.Texture2D,
                        Width = 512,
                        Height = 512,
                        ArraySize = 1,
                        MipLevels = 1,
                        Format = Format.R8G8B8A8_UNorm,
                        Flags = ResourceFlags.None,
                        InitialState = ResourceStates.Common,
                        DebugName = "PooledTexture2D"
                    }), 2, 8);
            }
        }

        private void TrackResource(ID3D12Resource resource, TrackedResource trackedResource)
        {
            lock (_trackingLock)
            {
                _trackedResources[resource.NativePointer] = trackedResource;
                _leakDetector.RecordResourceCreation(trackedResource);
            }
        }

        private void AddToResourcePool(ID3D12Resource resource, object description)
        {
            lock (_poolLock)
            {
                var poolName = GetPoolName(description);
                if (!string.IsNullOrEmpty(poolName) && _resourcePools.TryGetValue(poolName, out var pool))
                {
                    pool.AddResource(resource);
                }
            }
        }

        private string GetPoolName(object description)
        {
            return description switch
            {
                BufferCreationDesc bufferDesc => GetBufferPoolName(bufferDesc.SizeInBytes),
                TextureCreationDesc textureDesc => "Texture2D", // Default to Texture2D pool
                _ => null
            };
        }

        private string GetBufferPoolName(long sizeInBytes)
        {
            return sizeInBytes switch
            {
                <= 1024 => "SmallBuffer",
                <= 16384 => "MediumBuffer",
                _ => null // Don't pool large buffers
            };
        }

        private long CalculateTextureMemorySize(TrackedResource tracked)
        {
            if (tracked.Width == null || tracked.Height == null || tracked.Format == null)
                return 0;

            // Calculate texture memory size based on format and dimensions
            var bytesPerPixel = GetBytesPerPixel(tracked.Format.Value);
            return tracked.Width.Value * tracked.Height.Value * bytesPerPixel;
        }

        private int GetBytesPerPixel(Format format)
        {
            return format switch
            {
                Format.R8G8B8A8_UNorm or Format.B8G8R8A8_UNorm => 4,
                Format.R32G32B32A32_Float => 16,
                Format.R32G32_Float => 8,
                Format.R32_Float => 4,
                Format.D24_UNorm_S8_UInt => 4,
                _ => 4 // Default assumption
            };
        }

        private ResourceTimingHandle BeginResourceCreation(string resourceType, string operationName)
        {
            return new ResourceTimingHandle
            {
                ResourceType = resourceType,
                OperationName = operationName,
                StartTime = DateTime.UtcNow,
                StartCpuTicks = System.Diagnostics.Stopwatch.GetTimestamp()
            };
        }

        private void EndResourceCreation(ref ResourceTimingHandle handle, bool success, string errorMessage = null)
        {
            if (handle.StartTime == default) return;

            handle.EndTime = DateTime.UtcNow;
            handle.EndCpuTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            handle.Success = success;
            handle.ErrorMessage = errorMessage;

            // Record metrics for performance tracking
            var duration = (handle.EndCpuTicks - handle.StartCpuTicks) / (double)System.Diagnostics.Stopwatch.Frequency * 1000.0;
            _leakDetector.RecordCreationTime(handle.ResourceType, duration, success);
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            // Check for leaks before cleanup
            var leakReport = CheckForLeaks();
            if (leakReport.LeakedResources.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"Resource leaks detected: {leakReport.LeakedResources.Count}");
                foreach (var leak in leakReport.LeakedResources)
                {
                    System.Diagnostics.Debug.WriteLine($"  - {leak.ResourceType}: {leak.DebugName} (Age: {leak.Age})");
                }
            }

            // Clean up all tracked resources
            lock (_trackingLock)
            {
                foreach (var tracked in _trackedResources.Values)
                {
                    try
                    {
                        tracked.Resource?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error disposing resource {tracked.DebugName}: {ex.Message}");
                    }
                }
                _trackedResources.Clear();
            }

            // Clean up resource pools
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

    // Supporting classes and structures

    public class BufferCreationDesc
    {
        public long SizeInBytes { get; set; }
        public HeapProperties HeapProperties { get; set; } = new HeapProperties(HeapType.Default);
        public HeapFlags HeapFlags { get; set; } = HeapFlags.None;
        public ResourceFlags Flags { get; set; } = ResourceFlags.None;
        public ResourceStates InitialState { get; set; } = ResourceStates.Common;
        public string DebugName { get; set; }
    }

    public class TextureCreationDesc
    {
        public ResourceDimension Dimension { get; set; } = ResourceDimension.Texture2D;
        public int Width { get; set; } = 512;
        public int Height { get; set; } = 512;
        public int ArraySize { get; set; } = 1;
        public int MipLevels { get; set; } = 1;
        public Format Format { get; set; } = Format.R8G8B8A8_UNorm;
        public ResourceFlags Flags { get; set; } = ResourceFlags.None;
        public ResourceStates InitialState { get; set; } = ResourceStates.Common;
        public SampleDescription SampleDescription { get; set; } = new SampleDescription(1, 0);
        public string DebugName { get; set; }
    }

    public class QueryHeapCreationDesc
    {
        public D3D12_QUERY_HEAP_TYPE Type { get; set; } = D3D12_QUERY_HEAP_TYPE_TIMESTAMP;
        public int Count { get; set; } = 256;
        public string DebugName { get; set; }
    }

    public class DirectXBuffer : IDisposable
    {
        private readonly ID3D12Resource _resource;
        private readonly long _sizeInBytes;
        private readonly string _debugName;
        private bool _disposed = false;

        public ID3D12Resource Resource => _resource;
        public long SizeInBytes => _sizeInBytes;
        public string DebugName => _debugName;

        public DirectXBuffer(ID3D12Resource resource, long sizeInBytes, string debugName = null)
        {
            _resource = resource ?? throw new ArgumentNullException(nameof(resource));
            _sizeInBytes = sizeInBytes;
            _debugName = debugName;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _resource?.Dispose();
                _disposed = true;
            }
        }
    }

    public class DirectXTexture : IDisposable
    {
        private readonly ID3D12Resource _resource;
        private readonly int _width;
        private readonly int _height;
        private readonly Format _format;
        private readonly string _debugName;
        private bool _disposed = false;

        public ID3D12Resource Resource => _resource;
        public int Width => _width;
        public int Height => _height;
        public Format Format => _format;
        public string DebugName => _debugName;

        public DirectXTexture(ID3D12Resource resource, int width, int height, Format format, string debugName = null)
        {
            _resource = resource ?? throw new ArgumentNullException(nameof(resource));
            _width = width;
            _height = height;
            _format = format;
            _debugName = debugName;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _resource?.Dispose();
                _disposed = true;
            }
        }
    }

    public class DirectXQueryHeap : IDisposable
    {
        private readonly ID3D12QueryHeap _heap;
        private readonly D3D12_QUERY_HEAP_TYPE _type;
        private readonly int _count;
        private readonly string _debugName;
        private bool _disposed = false;

        public ID3D12QueryHeap Heap => _heap;
        public D3D12_QUERY_HEAP_TYPE Type => _type;
        public int Count => _count;
        public string DebugName => _debugName;

        public DirectXQueryHeap(ID3D12QueryHeap heap, D3D12_QUERY_HEAP_TYPE type, int count, string debugName = null)
        {
            _heap = heap ?? throw new ArgumentNullException(nameof(heap));
            _type = type;
            _count = count;
            _debugName = debugName;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _heap?.Dispose();
                _disposed = true;
            }
        }
    }

    public class TrackedResource
    {
        public ID3D12Resource Resource { get; set; }
        public DateTime CreationTime { get; set; }
        public string ResourceType { get; set; }
        public string DebugName { get; set; }
        public long? SizeInBytes { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public Format? Format { get; set; }
        public int? QueryCount { get; set; }
    }

    public class ResourceLeakReport
    {
        public List<LeakedResource> LeakedResources { get; set; } = new();
        public DateTime ReportTime { get; set; } = DateTime.UtcNow;
        public int TotalResources { get; set; }
        public TimeSpan ReportAge => DateTime.UtcNow - ReportTime;
    }

    public class LeakedResource
    {
        public string ResourceType { get; set; }
        public string DebugName { get; set; }
        public TimeSpan Age { get; set; }
        public DateTime CreationTime { get; set; }
    }

    public class DirectXResourceStatistics
    {
        public int TotalTrackedResources { get; set; }
        public int BufferCount { get; set; }
        public int TextureCount { get; set; }
        public int QueryHeapCount { get; set; }
        public long TotalBufferMemory { get; set; }
        public long TotalTextureMemory { get; set; }
        public int ActivePools { get; set; }
        public int TotalPooledResources { get; set; }
        public int AvailablePooledResources { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class DirectXResourceCreationException : Exception
    {
        public DirectXResourceCreationException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class DirectXResourceException : Exception
    {
        public DirectXResourceException(string message, Exception innerException) : base(message, innerException) { }
    }
}