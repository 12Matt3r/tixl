// MockD3D12Device.cs
using System;
using System.Collections.Generic;
using SharpDX.Direct3D12;

namespace TiXL.Tests.Mocks.Graphics
{
    public class MockD3D12Device : IDisposable
    {
        private readonly List<MockD3D12Resource> _resources = new();
        private readonly List<MockD3D12CommandQueue> _commandQueues = new();
        private bool _disposed = false;

        public MockDeviceInfo DeviceInfo { get; } = new MockDeviceInfo();
        
        public MockD3D12CommandQueue CreateCommandQueue()
        {
            var queue = new MockD3D12CommandQueue();
            _commandQueues.Add(queue);
            return queue;
        }
        
        public MockD3D12Resource CreateCommittedResource(
            HeapType heapType, 
            ResourceStates initialState,
            ResourceDescription description,
            ClearValue? clearValue = null)
        {
            var resource = new MockD3D12Resource(description, initialState);
            _resources.Add(resource);
            return resource;
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var resource in _resources)
                {
                    resource?.Dispose();
                }
                
                foreach (var queue in _commandQueues)
                {
                    queue?.Dispose();
                }
                
                _disposed = true;
            }
        }
    }
    
    public class MockDeviceInfo
    {
        public int AdapterId { get; set; } = 1;
        public string AdapterName { get; set; } = "Mock GPU";
        public long DedicatedVideoMemory { get; set; } = 8L * 1024 * 1024 * 1024; // 8GB
        public long DedicatedSystemMemory { get; set; } = 4L * 1024 * 1024 * 1024; // 4GB
        public long SharedSystemMemory { get; set; } = 8L * 1024 * 1024 * 1024; // 8GB
        public FeatureLevel FeatureLevel { get; set; } = FeatureLevel.Level_12_0;
        public Version GraphicsDriverVersion { get; set; } = new Version(31, 0, 0, 0);
    }
    
    public class MockD3D12Resource : IDisposable
    {
        private readonly ResourceDescription _description;
        private readonly ResourceStates _initialState;
        private readonly byte[] _bufferData;
        private bool _disposed = false;
        
        public ResourceStates State { get; private set; }
        public ResourceDescription Description => _description;
        
        public MockD3D12Resource(ResourceDescription description, ResourceStates initialState)
        {
            _description = description;
            _initialState = initialState;
            State = initialState;
            
            // Allocate mock buffer data
            var bufferSize = GetBufferSize(description);
            _bufferData = new byte[bufferSize];
        }
        
        public void WriteToBuffer<T>(ReadOnlySpan<T> data) where T : struct
        {
            if (_description.Dimension != ResourceDimension.Buffer)
                throw new InvalidOperationException("Can only write to buffer resources");
                
            var bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(data);
            if (bytes.Length > _bufferData.Length)
                throw new ArgumentException("Data too large for buffer");
                
            bytes.CopyTo(_bufferData);
        }
        
        public void ReadFromBuffer<T>(Span<T> data) where T : struct
        {
            if (_description.Dimension != ResourceDimension.Buffer)
                throw new InvalidOperationException("Can only read from buffer resources");
                
            var bufferBytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(_bufferData);
            var targetBytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(data);
            bufferBytes[..targetBytes.Length].CopyTo(targetBytes);
        }
        
        private static int GetBufferSize(ResourceDescription description)
        {
            return description.Width switch
            {
                > 0 => (int)description.Width,
                _ => 4096 // Default buffer size
            };
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
    
    public class MockD3D12CommandQueue : IDisposable
    {
        private bool _disposed = false;
        
        public void ExecuteCommandLists(params MockD3D12CommandList[] commandLists)
        {
            // Mock command execution
            foreach (var commandList in commandLists)
            {
                // Simulate command execution
            }
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
    
    public class MockD3D12CommandList : IDisposable
    {
        private bool _disposed = false;
        
        public void Close()
        {
            // Mock command list closure
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}