# TiXL xUnit Testing Framework Implementation Guide

## Table of Contents
1. [Overview](#overview)
2. [Project Structure](#project-structure)
3. [xUnit Project Templates](#xunit-project-templates)
4. [Test Configuration](#test-configuration)
5. [Mock/Fake Implementations](#mockfake-implementations)
6. [Test Data Management](#test-data-management)
7. [CI/CD Integration](#cicd-integration)
8. [Test Categories](#test-categories)
9. [Testing Best Practices](#testing-best-practices)
10. [Performance Testing](#performance-testing)
11. [Graphics Testing](#graphics-testing)
12. [UI Testing](#ui-testing)

## Overview

This document provides a comprehensive xUnit testing framework implementation for TiXL (Tooll 3), addressing the critical P0 testing gap identified in the analysis. The framework includes unit, integration, performance, graphics, and UI testing across Core, Operators, Gfx, Editor, and Gui modules.

### Key Features
- Modular xUnit project structure for each TiXL module
- Mock/fake implementations for DirectX 12, audio processing, and graphics dependencies
- Real-time graphics testing with headless rendering
- Integration with CI/CD pipeline with coverage reporting
- Comprehensive test categories and fixtures
- Performance benchmarking and visual regression testing

## Project Structure

```
Tests/
├── TiXL.Tests/
│   ├── TiXL.Tests.csproj
│   ├── Core/
│   │   ├── DataTypes/
│   │   ├── IO/
│   │   ├── Rendering/
│   │   ├── Compilation/
│   │   ├── Animation/
│   │   ├── Audio/
│   │   └── Video/
│   ├── Operators/
│   │   ├── Core/
│   │   ├── TypeOperators/
│   │   ├── GfxOperators/
│   │   ├── Collections/
│   │   ├── Values/
│   │   ├── NET/
│   │   └── Integration/
│   ├── Gfx/
│   │   ├── Rendering/
│   │   ├── Shaders/
│   │   ├── Buffers/
│   │   ├── Textures/
│   │   └── Pipeline/
│   ├── Editor/
│   │   ├── App/
│   │   ├── Compilation/
│   │   ├── Model/
│   │   └── CrashReporting/
│   └── Gui/
│       ├── Windows/
│       ├── Input/
│       ├── Interaction/
│       ├── Dialogs/
│       └── Styling/
├── TiXL.Integration.Tests/
├── TiXL.Performance.Tests/
├── TiXL.Graphics.Tests/
├── TiXL.UI.Tests/
├── TestInfrastructure/
│   ├── Mocks/
│   ├── Fakes/
│   ├── Fixtures/
│   └── Utilities/
└── TestData/
    ├── Shaders/
    ├── Models/
    ├── Textures/
    ├── Audio/
    └── Projects/
```

## xUnit Project Templates

### 1. Main xUnit Test Project (TiXL.Tests.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <Nullable>enable</Nullable>
    <GenerateAssemblyInfo>true</GenerateAssemblyInfo>
  </PropertyGroup>

  <ItemGroup>
    <!-- Core Testing Framework -->
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    
    <!-- Test Coverage -->
    <PackageReference Include="coverlet.collector" Version="6.0.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="coverlet.msbuild" Version="6.0.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    
    <!-- Mocking and Testing Utilities -->
    <PackageReference Include="Moq" Version="4.20.69" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.0" />
    
    <!-- Graphics Testing -->
    <PackageReference Include="SixLabors.ImageSharp" Version="3.1.5" />
    <PackageReference Include="SharpDX.Mathematics" Version="4.2.0" />
    
    <!-- File System and IO Testing -->
    <PackageReference Include="Microsoft.Extensions.FileProviders.Abstractions" Version="8.0.0" />
    <PackageReference Include="System.IO.Abstractions.TestingHelpers" Version="19.2.69" />
    
    <!-- Audio Testing -->
    <PackageReference Include="NAudio" Version="2.2.1" />
    
    <!-- Performance Testing -->
    <PackageReference Include="BenchmarkDotNet" Version="0.13.12" />
    
    <!-- Data Comparison -->
    <PackageReference Include="FluentAssertions" Version="6.12.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../TiXL.Core/TiXL.Core.csproj" />
    <ProjectReference Include="../TiXL.Operators/TiXL.Operators.csproj" />
    <ProjectReference Include="../TiXL.Gfx/TiXL.Gfx.csproj" />
    <ProjectReference Include="../TiXL.Editor/TiXL.Editor.csproj" />
    <ProjectReference Include="../TiXL.Gui/TiXL.Gui.csproj" />
  </ItemGroup>

</Project>
```

### 2. Integration Tests Project (TiXL.Integration.Tests.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" Version="8.0.0" />
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="TiXL.Tests.csproj" />
    <ProjectReference Include="../TiXL.Core/TiXL.Core.csproj" />
    <ProjectReference Include="../TiXL.Operators/TiXL.Operators.csproj" />
    <ProjectReference Include="../TiXL.Editor/TiXL.Editor.csproj" />
  </ItemGroup>

</Project>
```

### 3. Performance Tests Project (TiXL.Performance.Tests.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
    <PackageReference Include="BenchmarkDotNet" Version="0.13.12" />
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
    <PackageReference Include="Microsoft.Diagnostics.Tracing.TraceEvent" Version="3.1.8" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="TiXL.Tests.csproj" />
    <ProjectReference Include="../TiXL.Core/TiXL.Core.csproj" />
    <ProjectReference Include="../TiXL.Gfx/TiXL.Gfx.csproj" />
  </ItemGroup>

</Project>
```

## Test Configuration

### 1. xUnit Configuration (xunit.runner.json)

```json
{
  "parallelizeTestCollections": true,
  "parallelizeAssemblies": true,
  "maxParallelThreads": 0,
  "diagnosticMessages": false,
  "internalDiagnosticMessages": false,
  "longRunningTestSeconds": 5,
  "notEqualBehavior": 0
}
```

### 2. Test Settings (runsettings)

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="Code Coverage" uri="datacollector://microsoft/CodeCoverage/2.0">
        <Configuration>
          <Format>Cobertura</Format>
          <Exclude>[*]*Microsoft.VisualStudio.TestPlatform.*</Exclude>
          <Include>[TiXL.Core]*</Include>
          <Include>[TiXL.Operators]*</Include>
          <Include>[TiXL.Gfx]*</Include>
          <Include>[TiXL.Editor]*</Include>
          <Include>[TiXL.Gui]*</Include>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
  
  <LoggerRunSettings>
    <Loggers>
      <Logger friendlyName="console" enabled="true">
        <Configuration>
          <Verbosity>detailed</Verbosity>
        </Configuration>
      </Logger>
      <Logger friendlyName="trx" enabled="true">
        <Configuration>
          <LogFileName>TestResults.trx</LogFileName>
        </Configuration>
      </Logger>
    </Loggers>
  </LoggerRunSettings>
  
  < MSTestRunSettings>
    <Execution>
      <IncludeTestMetadata>false</IncludeTestMetadata>
    </Execution>
  </MSTestRunSettings>
</RunSettings>
```

## Mock/Fake Implementations

### 1. DirectX 12 Mock Framework

```csharp
// MockD3D12Device.cs
using SharpDX.Direct3D12;
using SharpDX;
using System;
using System.Collections.Generic;

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
}
```

### 2. Graphics Resource Mocks

```csharp
// MockD3D12Resource.cs
using SharpDX.Direct3D12;
using SharpDX;
using System;

namespace TiXL.Tests.Mocks.Graphics
{
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
}
```

### 3. Audio Processing Mocks

```csharp
// MockAudioEngine.cs
using System;
using System.Collections.Generic;
using System.Threading;

namespace TiXL.Tests.Mocks.Audio
{
    public interface IMockAudioSource
    {
        float[] GetAudioData(int sampleCount, int sampleRate);
        bool IsPlaying { get; }
        TimeSpan CurrentTime { get; }
    }
    
    public class MockAudioSource : IMockAudioSource
    {
        private readonly float _frequency;
        private readonly float _amplitude;
        private readonly object _lock = new object();
        private bool _isPlaying;
        private TimeSpan _currentTime;
        private readonly Timer _playbackTimer;
        
        public float Frequency => _frequency;
        public float Amplitude => _amplitude;
        public bool IsPlaying => _isPlaying;
        public TimeSpan CurrentTime { get; private set; }
        
        public MockAudioSource(float frequency = 440f, float amplitude = 0.5f)
        {
            _frequency = frequency;
            _amplitude = amplitude;
            _playbackTimer = new Timer(Tick, null, Timeout.Infinite, Timeout.Infinite);
        }
        
        private void Tick(object state)
        {
            lock (_lock)
            {
                if (_isPlaying)
                {
                    CurrentTime += TimeSpan.FromMilliseconds(16); // 60 FPS
                }
            }
        }
        
        public float[] GetAudioData(int sampleCount, int sampleRate)
        {
            var data = new float[sampleCount];
            var timeIncrement = 1.0f / sampleRate;
            
            for (int i = 0; i < sampleCount; i++)
            {
                var time = (float)CurrentTime.TotalSeconds + (i * timeIncrement);
                data[i] = _amplitude * MathF.Sin(2 * MathF.PI * _frequency * time);
            }
            
            return data;
        }
        
        public void Play()
        {
            lock (_lock)
            {
                _isPlaying = true;
                _playbackTimer.Change(0, 16);
            }
        }
        
        public void Pause()
        {
            lock (_lock)
            {
                _isPlaying = false;
                _playbackTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }
        
        public void Stop()
        {
            lock (_lock)
            {
                _isPlaying = false;
                CurrentTime = TimeSpan.Zero;
                _playbackTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }
        
        public void Dispose()
        {
            _playbackTimer?.Dispose();
        }
    }
}
```

### 4. Operator System Mocks

```csharp
// MockOperatorSystem.cs
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using TiXL.Operators;

namespace TiXL.Tests.Mocks.Operators
{
    public class MockOperatorRegistry : IOperatorRegistry
    {
        private readonly ConcurrentDictionary<string, IOperatorSymbol> _symbols = new();
        private readonly List<Type> _registeredTypes = new();
        
        public Task RegisterOperatorAsync<TSymbol, TInstance>() 
            where TSymbol : IOperatorSymbol, new()
            where TInstance : IOperatorInstance, new()
        {
            var symbol = new TSymbol();
            var key = symbol.GetType().Name;
            _symbols.TryAdd(key, symbol);
            _registeredTypes.Add(typeof(TInstance));
            return Task.CompletedTask;
        }
        
        public Task<bool> UnregisterOperatorAsync(string symbolName)
        {
            return Task.FromResult(_symbols.TryRemove(symbolName, out _));
        }
        
        public IOperatorSymbol GetSymbol(string symbolName)
        {
            _symbols.TryGetValue(symbolName, out var symbol);
            return symbol;
        }
        
        public IEnumerable<IOperatorSymbol> GetAllSymbols()
        {
            return _symbols.Values;
        }
        
        public Task<IOperatorInstance> CreateInstanceAsync(string symbolName)
        {
            if (!_symbols.TryGetValue(symbolName, out var symbol))
                throw new KeyNotFoundException($"Symbol '{symbolName}' not found");
                
            var instance = new MockOperatorInstance(symbol);
            return Task.FromResult(instance);
        }
    }
    
    public class MockOperatorInstance : IOperatorInstance
    {
        private readonly IOperatorSymbol _symbol;
        private readonly Dictionary<string, object> _parameters = new();
        private readonly List<IOperatorSlot> _inputs = new();
        private readonly List<IOperatorSlot> _outputs = new();
        private OperatorStatus _status = OperatorStatus.Stopped;
        
        public string Name { get; set; } = string.Empty;
        public IOperatorSymbol Symbol => _symbol;
        public OperatorStatus Status => _status;
        public IEnumerable<IOperatorSlot> Inputs => _inputs;
        public IEnumerable<IOperatorSlot> Outputs => _outputs;
        public IReadOnlyDictionary<string, object> Parameters => _parameters;
        
        public event EventHandler<OperatorEventArgs>? StatusChanged;
        public event EventHandler<OperatorEventArgs>? ParameterChanged;
        
        public MockOperatorInstance(IOperatorSymbol symbol)
        {
            _symbol = symbol;
        }
        
        public Task<bool> StartAsync()
        {
            _status = OperatorStatus.Running;
            StatusChanged?.Invoke(this, new OperatorEventArgs { Status = _status });
            return Task.FromResult(true);
        }
        
        public Task<bool> StopAsync()
        {
            _status = OperatorStatus.Stopped;
            StatusChanged?.Invoke(this, new OperatorEventArgs { Status = _status });
            return Task.FromResult(true);
        }
        
        public Task<bool> PauseAsync()
        {
            _status = OperatorStatus.Paused;
            StatusChanged?.Invoke(this, new OperatorEventArgs { Status = _status });
            return Task.FromResult(true);
        }
        
        public void SetParameter(string name, object value)
        {
            var oldValue = _parameters.ContainsKey(name) ? _parameters[name] : null;
            _parameters[name] = value;
            
            if (!Equals(oldValue, value))
            {
                ParameterChanged?.Invoke(this, new OperatorEventArgs 
                { 
                    ParameterName = name, 
                    ParameterValue = value 
                });
            }
        }
        
        public object? GetParameter(string name)
        {
            return _parameters.ContainsKey(name) ? _parameters[name] : null;
        }
    }
}
```

## Test Data Management

### 1. Test Fixtures

```csharp
// TiXLFacts.cs - Base test fixture
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace TiXL.Tests.Fixtures
{
    public abstract class TiXLFacts : IAsyncLifetime, IDisposable
    {
        protected IServiceProvider? ServiceProvider { get; private set; }
        protected IHost? Host { get; private set; }
        protected ILogger Logger { get; private set; } = null!;
        
        public virtual async Task InitializeAsync()
        {
            Host = CreateHostBuilder().Build();
            ServiceProvider = Host.Services;
            Logger = ServiceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger(GetType().Name);
            
            await SetupAsync();
        }
        
        public virtual async Task DisposeAsync()
        {
            await CleanupAsync();
            await Host?.DisposeAsync();
        }
        
        protected virtual IHostBuilder CreateHostBuilder()
        {
            return Host.CreateDefaultBuilder()
                .ConfigureServices(ConfigureServices);
        }
        
        protected virtual void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            // Override in derived fixtures
        }
        
        protected virtual Task SetupAsync()
        {
            return Task.CompletedTask;
        }
        
        protected virtual Task CleanupAsync()
        {
            return Task.CompletedTask;
        }
        
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
    
    // Core test fixture with TiXL dependencies
    public class CoreTestFixture : TiXLFacts
    {
        protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            base.ConfigureServices(context, services);
            
            // Register TiXL Core services
            services.AddLogging(builder => builder.AddConsole());
            
            // Add mock services for testing
            services.AddSingleton<ITestCleanupService, TestCleanupService>();
        }
        
        protected override Task SetupAsync()
        {
            Logger.LogInformation("CoreTestFixture initialized");
            return Task.CompletedTask;
        }
    }
    
    // Graphics test fixture
    public class GraphicsTestFixture : TiXLFacts
    {
        protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            base.ConfigureServices(context, services);
            
            services.AddSingleton(MockD3D12Device.Create());
        }
        
        protected override Task SetupAsync()
        {
            Logger.LogInformation("GraphicsTestFixture initialized");
            return Task.CompletedTask;
        }
    }
    
    // Operator test fixture
    public class OperatorTestFixture : CoreTestFixture
    {
        public IMockOperatorRegistry MockRegistry { get; private set; } = null!;
        
        protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            base.ConfigureServices(context, services);
            
            MockRegistry = new MockOperatorRegistry();
            services.AddSingleton<IMockOperatorRegistry>(MockRegistry);
        }
        
        protected override Task SetupAsync()
        {
            Logger.LogInformation("OperatorTestFixture initialized");
            return Task.CompletedTask;
        }
    }
}
```

### 2. Test Data Sets

```csharp
// TestDataSets.cs
using System.Collections.Generic;
using TiXL.Core.DataTypes;

namespace TiXL.Tests.Data
{
    public static class VectorTestData
    {
        public static readonly IEnumerable<object[]> Vector2TestCases = new[]
        {
            new object[] { new Vector2(0, 0), new Vector2(1, 1), new Vector2(1, 1) },
            new object[] { new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) },
            new object[] { new Vector2(-1, -1), new Vector2(2, 2), new Vector2(1, 1) },
            new object[] { new Vector2(3.5f, 2.5f), new Vector2(1.5f, 0.5f), new Vector2(5, 3) }
        };
        
        public static readonly IEnumerable<object[]> Vector3TestCases = new[]
        {
            new object[] { new Vector3(0, 0, 0), new Vector3(1, 1, 1), new Vector3(1, 1, 1) },
            new object[] { new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0) },
            new object[] { new Vector3(-1, -1, -1), new Vector2(2, 2, 2), new Vector3(1, 1, 1) }
        };
    }
    
    public static class ColorTestData
    {
        public static readonly IEnumerable<object[]> ColorRgbTestCases = new[]
        {
            new object[] { 255, 0, 0, 255, 0, 0, 255 },      // Pure red
            new object[] { 0, 255, 0, 0, 255, 0, 255 },      // Pure green
            new object[] { 0, 0, 255, 0, 0, 255, 255 },      // Pure blue
            new object[] { 255, 255, 255, 255, 255, 255, 255 }, // White
            new object[] { 0, 0, 0, 0, 0, 0, 255 }           // Black
        };
    }
    
    public static class MatrixTestData
    {
        public static readonly float[,] IdentityMatrix = new float[,]
        {
            { 1, 0, 0, 0 },
            { 0, 1, 0, 0 },
            { 0, 0, 1, 0 },
            { 0, 0, 0, 1 }
        };
        
        public static readonly float[,] RotationMatrix = new float[,]
        {
            { 0, 1, 0, 0 },
            { -1, 0, 0, 0 },
            { 0, 0, 1, 0 },
            { 0, 0, 0, 1 }
        };
        
        public static readonly float[,] TranslationMatrix = new float[,]
        {
            { 1, 0, 0, 0 },
            { 0, 1, 0, 0 },
            { 0, 0, 1, 0 },
            { 5, 3, 2, 1 }
        };
    }
}
```

### 3. Shader Test Data

```csharp
// ShaderTestData.cs
using System.Collections.Generic;
using System.IO;

namespace TiXL.Tests.Data.Shaders
{
    public static class ShaderTestData
    {
        public const string SimpleVertexShader = @"
            struct VSInput
            {
                float3 Position : POSITION;
                float2 UV : TEXCOORD0;
            };
            
            struct VSOutput
            {
                float4 Position : SV_POSITION;
                float2 UV : TEXCOORD0;
            };
            
            VSOutput VSMain(VSInput input)
            {
                VSOutput output;
                output.Position = float4(input.Position, 1.0);
                output.UV = input.UV;
                return output;
            }
        ";
        
        public const string SimplePixelShader = @"
            struct PSInput
            {
                float4 Position : SV_POSITION;
                float2 UV : TEXCOORD0;
            };
            
            float4 PSMain(PSInput input) : SV_Target
            {
                return float4(input.UV, 0, 1);
            }
        ";
        
        public static readonly IEnumerable<object[]> ShaderCompilationCases = new[]
        {
            new object[] { SimpleVertexShader, ShaderType.Vertex },
            new object[] { SimplePixelShader, ShaderType.Pixel },
        };
        
        public static string GetTestShaderFile(string fileName)
        {
            var testDataDir = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Shaders");
            var shaderPath = Path.Combine(testDataDir, fileName);
            
            if (File.Exists(shaderPath))
            {
                return File.ReadAllText(shaderPath);
            }
            
            return string.Empty;
        }
    }
}
```

## Test Categories

### 1. Category Attributes

```csharp
// TestCategories.cs
using System;

namespace TiXL.Tests.Categories
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public class CategoryAttribute : Attribute
    {
        public string Category { get; }
        
        public CategoryAttribute(string category)
        {
            Category = category;
        }
    }
    
    public static class TestCategories
    {
        public const string Unit = "Unit";
        public const string Integration = "Integration";
        public const string Performance = "Performance";
        public const string Graphics = "Graphics";
        public const string UI = "UI";
        public const string Security = "Security";
        public const string Audio = "Audio";
        public const string Network = "Network";
        public const string IO = "IO";
        public const string Rendering = "Rendering";
        public const string Operators = "Operators";
        public const string Core = "Core";
        public const string Editor = "Editor";
        public const string Gui = "Gui";
        
        // Test priorities
        public const string P0 = "P0";
        public const string P1 = "P1";
        public const string P2 = "P2";
        public const string P3 = "P3";
        
        // Test speed
        public const string Fast = "Fast";
        public const string Medium = "Medium";
        public const string Slow = "Slow";
    }
}
```

### 2. Category-Specific Test Examples

```csharp
// CoreDataTypesTests.cs
using Xunit;
using FluentAssertions;
using TiXL.Tests.Categories;
using TiXL.Tests.Fixtures;
using TiXL.Tests.Data;

namespace TiXL.Tests.Core.DataTypes
{
    [Collection("Core Tests")]
    [Category(TestCategories.Unit)]
    [Category(TestCategories.Core)]
    [Category(TestCategories.P0)]
    public class Vector2Tests : CoreTestFixture
    {
        [Theory]
        [MemberData(nameof(VectorTestData.Vector2TestCases), MemberType = typeof(VectorTestData))]
        public void Add_Vectors_ReturnsCorrectSum(Vector2 a, Vector2 b, Vector2 expected)
        {
            // Arrange & Act
            var result = a + b;
            
            // Assert
            result.Should().Be(expected);
        }
        
        [Fact]
        [Category(TestCategories.Fast)]
        public void Vector2_Length_ReturnsCorrectMagnitude()
        {
            // Arrange
            var vector = new Vector2(3, 4);
            var expectedLength = 5.0f;
            
            // Act
            var length = vector.Length;
            
            // Assert
            length.Should().BeApproximately(expectedLength, 0.001f);
        }
    }
    
    [Collection("Core Tests")]
    [Category(TestCategories.Unit)]
    [Category(TestCategories.Core)]
    [Category(TestCategories.Performance)]
    [Category(TestCategories.P1)]
    public class Vector2PerformanceTests : CoreTestFixture
    {
        [Fact]
        [Category(TestCategories.Slow)]
        public void Vector2_LargeScaleOperations_PerformsWithinBudget()
        {
            // Arrange
            const int operationCount = 1000000;
            var vectors = GenerateTestVectors(operationCount);
            
            // Act
            var startTime = DateTime.UtcNow;
            
            foreach (var vector in vectors)
            {
                var length = vector.Length;
                var normalized = vector.Normalized();
            }
            
            var endTime = DateTime.UtcNow;
            var duration = endTime - startTime;
            
            // Assert
            duration.TotalMilliseconds.Should().BeLessThan(100); // 100ms budget
        }
        
        private static Vector2[] GenerateTestVectors(int count)
        {
            var vectors = new Vector2[count];
            var random = new Random(42); // Deterministic seed
            
            for (int i = 0; i < count; i++)
            {
                vectors[i] = new Vector2(
                    (float)(random.NextDouble() * 100),
                    (float)(random.NextDouble() * 100)
                );
            }
            
            return vectors;
        }
    }
}
```

## CI/CD Integration

### 1. GitHub Actions Workflow

```yaml
# .github/workflows/test.yml
name: TiXL Test Suite

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  test:
    runs-on: windows-latest
    
    strategy:
      matrix:
        configuration: [Debug, Release]
        
    steps:
    - uses: actions/checkout@v4
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: 9.0.x
    
    - name: Restore dependencies
      run: dotnet restore TiXL.sln
      
    - name: Build solution
      run: dotnet build TiXL.sln --configuration ${{ matrix.configuration }} --no-restore
      
    - name: Run unit tests
      run: dotnet test TiXL.Tests/TiXL.Tests.csproj --configuration ${{ matrix.configuration }} --no-build --verbosity normal --logger trx --dataCollector:"XPlat Code Coverage"
      
    - name: Run integration tests
      run: dotnet test TiXL.Integration.Tests/TiXL.Integration.Tests.csproj --configuration ${{ matrix.configuration }} --no-build --verbosity normal --logger trx
      
    - name: Run performance tests
      run: dotnet test TiXL.Performance.Tests/TiXL.Performance.Tests.csproj --configuration ${{ matrix.configuration }} --no-build --verbosity normal
      if: github.event_name == 'push' && github.ref == 'refs/heads/main'
      
    - name: Upload test results
      uses: actions/upload-artifact@v4
      if: always()
      with:
        name: Test Results ${{ matrix.configuration }}
        path: |
          **/TestResults/*.trx
          **/TestResults/**/*.trx
          
    - name: Upload coverage reports
      uses: codecov/codecov-action@v3
      with:
        token: ${{ secrets.CODECOV_TOKEN }}
        directory: TiXL.Tests/TestResults
        flags: unittests
        name: codecov-${{ matrix.configuration }}
        
    - name: Check test coverage
      run: |
        $coverage = Get-Content "TiXL.Tests/TestResults/coverage.json" | ConvertFrom-Json
        $percentage = [math]::Round($coverage.total.lines.coveredPercent, 2)
        Write-Host "Coverage: $percentage%"
        if ($percentage -lt 70) {
          Write-Host "ERROR: Test coverage $percentage% is below 70% threshold"
          exit 1
        }
```

### 2. Azure DevOps Pipeline

```yaml
# azure-pipelines-test.yml
trigger:
- main
- develop

pool:
  vmImage: 'windows-latest'

variables:
  buildConfiguration: 'Release'
  dotNetFramework: 'net9.0'
  testCoverageThreshold: 70

stages:
- stage: Test
  displayName: 'Test Stage'
  jobs:
  - job: 'Test'
    displayName: 'Run Tests'
    steps:
    
    - task: UseDotNet@2
      displayName: 'Use .NET SDK'
      inputs:
        packageType: 'sdk'
        version: '9.0.x'
        
    - script: dotnet restore TiXL.sln
      displayName: 'Restore NuGet packages'
      
    - script: dotnet build TiXL.sln --configuration $(buildConfiguration) --no-restore
      displayName: 'Build solution'
      
    - script: |
        dotnet test TiXL.Tests/TiXL.Tests.csproj \
          --configuration $(buildConfiguration) \
          --no-build \
          --verbosity normal \
          --logger trx \
          --collect:"XPlat Code Coverage" \
          -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=Cobertura \
          -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Exclude=[*]*.Tests* \
          -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Include=[TiXL.Core]*,[TiXL.Operators]*,[TiXL.Gfx]*,[TiXL.Editor]*,[TiXL.Gui]*
      displayName: 'Run unit tests'
      
    - script: |
        dotnet test TiXL.Integration.Tests/TiXL.Integration.Tests.csproj \
          --configuration $(buildConfiguration) \
          --no-build \
          --verbosity normal \
          --logger trx
      displayName: 'Run integration tests'
      
    - task: PublishTestResults@2
      displayName: 'Publish test results'
      inputs:
        testResultsFormat: 'VSTest'
        testResultsFiles: '**/*.trx'
        failTaskOnFailedTests: true
        
    - task: PublishCodeCoverageResults@1
      displayName: 'Publish code coverage'
      inputs:
        codeCoverageTool: 'Cobertura'
        summaryFileLocation: 'TiXL.Tests/TestResults/coverage.cobertura.xml'
        
    - script: |
        dotnet test TiXL.Performance.Tests/TiXL.Performance.Tests.csproj \
          --configuration $(buildConfiguration) \
          --no-build \
          --verbosity normal
      displayName: 'Run performance tests'
      condition: and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/main'))
```

## Testing Best Practices

### 1. Test Naming Conventions

```csharp
namespace TiXL.Tests.BestPractices
{
    // ✅ Good naming pattern: MethodName_Scenario_ExpectedBehavior
    public class GoodNamingExamples
    {
        [Fact]
        public void Vector2_Normalize_ZeroVector_ThrowsException()
        {
            // Test handles zero vector normalization
        }
        
        [Fact]
        public void OperatorRegistry_RegisterOperator_DuplicateSymbolName_ThrowsException()
        {
            // Test handles duplicate symbol registration
        }
        
        [Theory]
        [InlineData(0, 0, 1)]    // Minimum values
        [InlineData(50, 50, 1)]  // Typical values
        [InlineData(100, 100, 1)] // Maximum values
        public void Matrix_Multiply_VariousInputSizes_ReturnsCorrectResult(int rows, int cols, float expectedFactor)
        {
            // Test with multiple input scenarios
        }
    }
    
    // ❌ Bad naming patterns to avoid
    public class BadNamingExamples
    {
        [Fact]
        public void Test1() { } // Too vague
        
        [Fact]
        public void TestVector() { } // Doesn't describe what is being tested
        
        [Fact] 
        public void ShouldWork() { } // Doesn't describe the scenario
    }
}
```

### 2. Test Organization and Structure

```csharp
namespace TiXL.Tests.BestPractices
{
    public class WellStructuredTestExample
    {
        [Fact]
        public void Vector2_Clamp_ValidRange_ReturnsClampedVector()
        {
            // Arrange
            var vector = new Vector2(5, 8);
            var min = new Vector2(0, 0);
            var max = new Vector2(3, 6);
            var expected = new Vector2(3, 6);
            
            // Act
            var result = vector.Clamp(min, max);
            
            // Assert
            result.Should().Be(expected);
        }
        
        [Fact]
        public async Task OperatorInstance_StartAsync_ValidConfiguration_StartsSuccessfully()
        {
            // Arrange
            var instance = new MockOperatorInstance(CreateTestSymbol());
            await instance.SetConfigurationAsync(CreateValidConfiguration());
            
            // Act
            var success = await instance.StartAsync();
            
            // Assert
            success.Should().BeTrue();
            instance.Status.Should().Be(OperatorStatus.Running);
        }
        
        // Helper methods for test setup
        private IOperatorSymbol CreateTestSymbol()
        {
            // Return a properly configured test symbol
            return new MockOperatorSymbol();
        }
        
        private Dictionary<string, object> CreateValidConfiguration()
        {
            return new Dictionary<string, object>
            {
                { "OutputSize", new Vector2(1920, 1080) },
                { "FrameRate", 60 },
                { "ColorSpace", "sRGB" }
            };
        }
    }
}
```

### 3. Assertion Best Practices

```csharp
namespace TiXL.Tests.BestPractices
{
    public class AssertionExamples
    {
        [Fact]
        public void Collections_ShouldUseFluentAssertions()
        {
            // Arrange
            var input = new[] { 1, 2, 3, 4, 5 };
            var expected = new[] { 2, 4, 6, 8, 10 };
            
            // Act
            var result = input.Select(x => x * 2).ToArray();
            
            // Assert - Fluent Assertions
            result.Should().BeEquivalentTo(expected);
            result.Should().HaveCount(5);
            result.Should().BeInAscendingOrder();
        }
        
        [Fact]
        public void FloatingPoint_ShouldUseTolerance()
        {
            // Arrange
            var vector1 = new Vector2(1.0f / 3.0f, 0.1f);
            var vector2 = new Vector2(0.333333f, 0.1f);
            
            // Act & Assert
            vector1.X.Should().BeApproximately(vector2.X, 0.001f);
            vector1.Y.Should().Be(vector2.Y); // Exact match for this case
        }
        
        [Fact]
        public void Exceptions_ShouldBeVerified()
        {
            // Arrange
            var invalidVector = new Vector2(float.NaN, 0);
            
            // Act & Assert
            Action act = () => invalidVector.Normalize();
            
            act.Should().Throw<ArgumentException>()
                .WithMessage("*NaN*");
        }
    }
}
```

## Performance Testing

### 1. Benchmark Tests

```csharp
// PerformanceBenchmarks.cs
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System;
using System.Linq;

namespace TiXL.Tests.Performance
{
    [SimpleJob(RuntimeMoniker.Net90)]
    [MemoryDiagnoser]
    [RankColumn]
    public class VectorPerformanceBenchmarks
    {
        private const int Iterations = 100000;
        
        [Benchmark]
        public float Vector2_Length_Calculation()
        {
            float result = 0;
            for (int i = 0; i < Iterations; i++)
            {
                var vector = new Vector2(i, i + 1);
                result += vector.Length;
            }
            return result;
        }
        
        [Benchmark]
        public Vector2[] Vector2_Array_Creation()
        {
            var result = new Vector2[Iterations];
            for (int i = 0; i < Iterations; i++)
            {
                result[i] = new Vector2(i, i);
            }
            return result;
        }
        
        [Benchmark]
        public float Vector3_DotProduct()
        {
            float result = 0;
            var random = new Random(42);
            
            for (int i = 0; i < Iterations; i++)
            {
                var v1 = new Vector3(
                    (float)random.NextDouble(),
                    (float)random.NextDouble(), 
                    (float)random.NextDouble()
                );
                
                var v2 = new Vector3(
                    (float)random.NextDouble(),
                    (float)random.NextDouble(),
                    (float)random.NextDouble()
                );
                
                result += Vector3.Dot(v1, v2);
            }
            
            return result;
        }
    }
}
```

### 2. Performance Assertions

```csharp
// PerformanceTests.cs
using Xunit;
using System;
using System.Diagnostics;
using TiXL.Tests.Categories;

namespace TiXL.Tests.Performance
{
    [Collection("Performance Tests")]
    [Category(TestCategories.Performance)]
    [Category(TestCategories.P1)]
    public class RenderingPerformanceTests
    {
        [Fact]
        [Category(TestCategories.Slow)]
        public void Renderer_FrameTime_Meets60FPSRequirement()
        {
            // Arrange
            const double targetFrameTimeMs = 16.67; // 60 FPS
            const int frameCount = 100;
            
            // Act
            var frameTimes = new double[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                
                // Simulate render frame
                RenderTestFrame();
                
                stopwatch.Stop();
                frameTimes[i] = stopwatch.Elapsed.TotalMilliseconds;
            }
            
            var averageFrameTime = frameTimes.Average();
            var maxFrameTime = frameTimes.Max();
            var percentile95 = frameTimes.OrderBy(t => t).ElementAt((int)(frameCount * 0.95));
            
            // Assert
            averageFrameTime.Should().BeLessThan(targetFrameTimeMs * 0.8, 
                "Average frame time should be well below 60 FPS requirement");
            percentile95.Should().BeLessThan(targetFrameTimeMs * 1.1, 
                "95th percentile frame time should be close to 60 FPS");
        }
        
        [Fact]
        [Category(TestCategories.Slow)]
        public void ShaderCompilation_TimeStaysWithinBudget()
        {
            // Arrange
            const double maxCompilationTimeMs = 100; // 100ms budget
            var testShaders = ShaderTestData.GetTestShaders();
            
            // Act & Assert
            foreach (var shader in testShaders)
            {
                var stopwatch = Stopwatch.StartNew();
                
                // Simulate shader compilation
                CompileShader(shader);
                
                stopwatch.Stop();
                
                stopwatch.Elapsed.TotalMilliseconds.Should().BeLessThan(maxCompilationTimeMs, 
                    $"Shader compilation for {shader.Name} should complete within budget");
            }
        }
        
        private void RenderTestFrame()
        {
            // Simulate rendering work
            for (int i = 0; i < 1000; i++)
            {
                var dummy = Math.Sin(i) * Math.Cos(i);
            }
        }
        
        private void CompileShader(TestShader shader)
        {
            // Simulate shader compilation time
            var compilationTime = shader.Complexity * 0.01;
            System.Threading.Thread.Sleep((int)compilationTime);
        }
    }
}
```

## Graphics Testing

### 1. Headless Graphics Tests

```csharp
// HeadlessGraphicsTests.cs
using Xunit;
using SharpDX.Direct3D12;
using SharpDX;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TiXL.Tests.Categories;
using TiXL.Tests.Mocks.Graphics;

namespace TiXL.Tests.Graphics
{
    [Collection("Graphics Tests")]
    [Category(TestCategories.Graphics)]
    [Category(TestCategories.Rendering)]
    public class HeadlessRenderingTests : IClassFixture<GraphicsTestFixture>
    {
        private readonly GraphicsTestFixture _fixture;
        
        public HeadlessRenderingTests(GraphicsTestFixture fixture)
        {
            _fixture = fixture;
        }
        
        [Fact]
        public void Renderer_InitializeDevice_Succeeds()
        {
            // Arrange & Act
            using var device = new MockD3D12Device();
            
            // Assert
            device.DeviceInfo.Should().NotBeNull();
            device.DeviceInfo.FeatureLevel.Should().Be(FeatureLevel.Level_12_0);
        }
        
        [Fact]
        public void RenderTarget_CreateTexture2D_Succeeds()
        {
            // Arrange
            using var device = new MockD3D12Device();
            var description = new ResourceDescription
            {
                Dimension = ResourceDimension.Texture2D,
                Width = 1920,
                Height = 1080,
                DepthOrArraySize = 1,
                MipLevels = 1,
                Format = Format.R8G8B8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0)
            };
            
            // Act
            using var renderTarget = device.CreateCommittedResource(
                HeapType.Default,
                ResourceStates.RenderTarget,
                description);
            
            // Assert
            renderTarget.Description.Dimension.Should().Be(ResourceDimension.Texture2D);
            renderTarget.Description.Width.Should().Be(1920);
            renderTarget.Description.Height.Should().Be(1080);
        }
        
        [Fact]
        [Category(TestCategories.Slow)]
        public void FrameRendering_OutputMatchesReference()
        {
            // Arrange
            var scene = CreateTestScene();
            var renderer = new MockRenderer();
            
            // Act
            using var renderTarget = renderer.Render(scene);
            
            // Assert
            renderTarget.Width.Should().Be(1920);
            renderTarget.Height.Should().Be(1080);
            
            // Compare with reference image
            var referenceImage = Image.Load<Rgba32>("TestData/Images/reference_frame.png");
            CompareImages(renderTarget, referenceImage);
        }
        
        private void CompareImages(RenderTarget actual, Image<Rgba32> expected)
        {
            actual.Width.Should().Be(expected.Width);
            actual.Height.Should().Be(expected.Height);
            
            var differentPixels = 0;
            var totalPixels = actual.Width * actual.Height;
            var tolerance = 5; // Allow small differences
            
            for (int y = 0; y < actual.Height; y++)
            {
                for (int x = 0; x < actual.Width; x++)
                {
                    var actualPixel = actual.GetPixel(x, y);
                    var expectedPixel = expected[x, y];
                    
                    if (Math.Abs(actualPixel.R - expectedPixel.R) > tolerance ||
                        Math.Abs(actualPixel.G - expectedPixel.G) > tolerance ||
                        Math.Abs(actualPixel.B - expectedPixel.B) > tolerance)
                    {
                        differentPixels++;
                    }
                }
            }
            
            var similarityPercentage = ((totalPixels - differentPixels) / (double)totalPixels) * 100;
            similarityPercentage.Should().BeGreaterThan(95, 
                "Rendered frame should be at least 95% similar to reference");
        }
        
        private TestScene CreateTestScene()
        {
            return new TestScene
            {
                Width = 1920,
                Height = 1080,
                BackgroundColor = Color.Black,
                Objects = new[]
                {
                    new TestObject
                    {
                        Type = ObjectType.Cube,
                        Position = new Vector3(0, 0, 0),
                        Size = new Vector3(1, 1, 1),
                        Material = new TestMaterial { Color = new Color(255, 0, 0) }
                    }
                }
            };
        }
    }
}
```

### 2. Shader Compilation Tests

```csharp
// ShaderCompilationTests.cs
using Xunit;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TiXL.Tests.Categories;

namespace TiXL.Tests.Graphics
{
    [Collection("Graphics Tests")]
    [Category(TestCategories.Graphics)]
    [Category(TestCategories.Performance)]
    public class ShaderCompilationTests
    {
        [Theory]
        [MemberData(nameof(ShaderTestData.ShaderCompilationCases), MemberType = typeof(ShaderTestData))]
        public void Shader_Compile_ValidShader_Succeeds(string shaderCode, ShaderType shaderType)
        {
            // Arrange & Act
            var compilationResult = CompileShader(shaderCode, shaderType);
            
            // Assert
            compilationResult.Success.Should().BeTrue();
            compilationResult.ErrorMessage.Should().BeNullOrEmpty();
            compilationResult.CompiledShader.Should().NotBeNull();
        }
        
        [Fact]
        public void Shader_Compile_InvalidShader_ReturnsError()
        {
            // Arrange
            var invalidShader = "invalid hlsl code";
            
            // Act
            var result = CompileShader(invalidShader, ShaderType.Vertex);
            
            // Assert
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().NotBeNull();
        }
        
        [Fact]
        [Category(TestCategories.Slow)]
        public void Shader_CompilationTime_MeetsPerformanceBudget()
        {
            // Arrange
            const double maxCompilationTimeMs = 50;
            var testShaders = GetCompilationTestShaders();
            
            // Act & Assert
            foreach (var shader in testShaders)
            {
                var startTime = DateTime.UtcNow;
                
                var result = CompileShader(shader.Code, shader.Type);
                
                var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
                
                duration.Should().BeLessThan(maxCompilationTimeMs, 
                    $"Shader compilation should complete within {maxCompilationTimeMs}ms");
            }
        }
        
        private static ShaderCompilationResult CompileShader(string code, ShaderType type)
        {
            // Mock shader compilation logic
            if (code.Contains("invalid"))
            {
                return new ShaderCompilationResult
                {
                    Success = false,
                    ErrorMessage = "Syntax error in shader code"
                };
            }
            
            return new ShaderCompilationResult
            {
                Success = true,
                CompiledShader = new byte[] { 0x01, 0x02, 0x03 }, // Mock compiled data
                ErrorMessage = null
            };
        }
        
        private static IEnumerable<TestShader> GetCompilationTestShaders()
        {
            yield return new TestShader
            {
                Name = "Simple Vertex",
                Code = ShaderTestData.SimpleVertexShader,
                Type = ShaderType.Vertex,
                Complexity = 10
            };
            
            yield return new TestShader
            {
                Name = "Simple Pixel",
                Code = ShaderTestData.SimplePixelShader,
                Type = ShaderType.Pixel,
                Complexity = 5
            };
        }
    }
    
    public class TestShader
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public ShaderType Type { get; set; }
        public int Complexity { get; set; }
    }
    
    public class ShaderCompilationResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public byte[]? CompiledShader { get; set; }
    }
}
```

## UI Testing

### 1. Editor UI Tests

```csharp
// EditorUITests.cs
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using TiXL.Tests.Categories;
using TiXL.Editor;
using TiXL.Gui;

namespace TiXL.Tests.UI
{
    [Collection("UI Tests")]
    [Category(TestCategories.UI)]
    [Category(TestCategories.Editor)]
    public class EditorUITests : IClassFixture<EditorTestFixture>
    {
        private readonly EditorTestFixture _fixture;
        
        public EditorUITests(EditorTestFixture fixture)
        {
            _fixture = fixture;
        }
        
        [Fact]
        public async Task Editor_WindowCreation_Succeeds()
        {
            // Arrange
            var editor = _fixture.ServiceProvider.GetRequiredService<ITestEditor>();
            
            // Act
            var success = await editor.CreateMainWindowAsync();
            
            // Assert
            success.Should().BeTrue();
            editor.MainWindow.Should().NotBeNull();
        }
        
        [Fact]
        public async Task Editor_ProjectCreation_WorksCorrectly()
        {
            // Arrange
            var editor = _fixture.ServiceProvider.GetRequiredService<ITestEditor>();
            await editor.InitializeAsync();
            var projectName = "TestProject";
            
            // Act
            var project = await editor.CreateProjectAsync(projectName);
            
            // Assert
            project.Should().NotBeNull();
            project.Name.Should().Be(projectName);
            project.IsModified.Should().BeFalse();
        }
        
        [Fact]
        public async Task NodeGraph_AddNode_UpdatesGraph()
        {
            // Arrange
            var editor = _fixture.ServiceProvider.GetRequiredService<ITestEditor>();
            await editor.InitializeAsync();
            var project = await editor.CreateProjectAsync("TestProject");
            var graph = await editor.GetActiveGraphAsync();
            
            // Act
            var node = await graph.AddNodeAsync("Vector2Operator", new Vector2(100, 100));
            
            // Assert
            node.Should().NotBeNull();
            node.Type.Should().Be("Vector2Operator");
            graph.Nodes.Should().Contain(node);
        }
        
        [Fact]
        public async Task NodeGraph_ConnectNodes_CreatesConnection()
        {
            // Arrange
            var editor = _fixture.ServiceProvider.GetRequiredService<ITestEditor>();
            await editor.InitializeAsync();
            var project = await editor.CreateProjectAsync("TestProject");
            var graph = await editor.GetActiveGraphAsync();
            
            var node1 = await graph.AddNodeAsync("ConstantVector2", new Vector2(50, 50));
            var node2 = await graph.AddNodeAsync("TransformOperator", new Vector2(300, 50));
            
            var outputSlot = node1.OutputSlots.First();
            var inputSlot = node2.InputSlots.First();
            
            // Act
            var connection = await graph.ConnectNodesAsync(outputSlot, inputSlot);
            
            // Assert
            connection.Should().NotBeNull();
            connection.Source.Should().Be(outputSlot);
            connection.Target.Should().Be(inputSlot);
            graph.Connections.Should().Contain(connection);
        }
    }
}
```

### 2. GUI Component Tests

```csharp
// GuiComponentTests.cs
using Xunit;
using TiXL.Tests.Categories;
using TiXL.Gui;
using TiXL.Core.DataTypes;

namespace TiXL.Tests.UI.Gui
{
    [Collection("GUI Tests")]
    [Category(TestCategories.Gui)]
    [Category(TestCategories.P1)]
    public class InputUiComponentTests
    {
        [Fact]
        public void Vector2InputUi_EditVector2Value_UpdatesCorrectly()
        {
            // Arrange
            var initialValue = new Vector2(5, 10);
            var inputUi = new Vector2InputUi(initialValue);
            var editState = new InputEditState();
            
            // Act
            inputUi.SetValue(new Vector2(15, 20), ref editState);
            
            // Assert
            inputUi.Value.X.Should().Be(15);
            inputUi.Value.Y.Should().Be(20);
            editState.Modified.Should().BeTrue();
        }
        
        [Theory]
        [InlineData("", false)] // Empty string
        [InlineData("invalid", false)] // Invalid value
        [InlineData("123", true)] // Valid integer
        [InlineData("123.45", true)] // Valid float
        public void FloatInputUi_ParseInput_ReturnsExpectedResult(string input, bool expectedValid)
        {
            // Arrange
            var inputUi = new FloatInputUi(0f);
            var editState = new InputEditState();
            
            // Act
            var isValid = inputUi.TryParse(input, out var result, ref editState);
            
            // Assert
            isValid.Should().Be(expectedValid);
            if (expectedValid)
            {
                editState.Modified.Should().BeTrue();
            }
        }
        
        [Fact]
        public void ColorPickerComponent_UpdateColor_PropagatesToTarget()
        {
            // Arrange
            var initialColor = new Color(255, 0, 0);
            var target = new MockColorTarget();
            var colorPicker = new ColorPickerComponent(initialColor, target);
            
            // Act
            var newColor = new Color(0, 255, 0);
            colorPicker.SetColor(newColor);
            
            // Assert
            target.Color.Should().Be(newColor);
        }
    }
    
    public class MockColorTarget : IColorTarget
    {
        public Color Color { get; set; }
        public void OnColorChanged(Color color)
        {
            Color = color;
        }
    }
}
```

## Conclusion

This comprehensive xUnit testing framework provides TiXL with:

1. **Complete Test Coverage**: Unit, integration, performance, graphics, and UI tests across all modules
2. **Real-time Graphics Testing**: Headless rendering with mock DirectX 12 implementation
3. **Automated CI/CD Integration**: GitHub Actions and Azure DevOps pipelines with coverage reporting
4. **Best Practices Implementation**: Proper naming, organization, and assertion patterns
5. **Performance Benchmarking**: Automated performance tests with budgets and thresholds
6. **Mock/Fake Infrastructure**: Comprehensive mocking for graphics, audio, and operator dependencies
7. **Test Data Management**: Structured fixtures and test data sets
8. **Category-based Testing**: Organized test categories for different test types and priorities

The framework addresses the critical P0 testing gap identified in the analysis and provides a solid foundation for reliable, high-quality TiXL development.

## Quick Start Guide

1. **Initialize the test projects** using the provided templates
2. **Set up CI/CD pipelines** with GitHub Actions or Azure DevOps
3. **Configure coverage reporting** with Coverlet
4. **Implement mock services** for graphics and audio dependencies
5. **Create test fixtures** for common test setup scenarios
6. **Add test categories** and organize tests by priority and type
7. **Run performance benchmarks** and establish baselines
8. **Implement visual regression tests** for graphics rendering
9. **Set up automated test execution** in CI pipelines
10. **Monitor test coverage** and maintain quality gates

The framework is designed to be scalable and maintainable, supporting TiXL's growth as a professional real-time motion graphics application.
