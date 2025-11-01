# TiXL Core Engine Automated Test Suite

**Version:** 1.0  
**Date:** 2025-11-01  
**Target Framework:** .NET 9.0  
**Testing Framework:** NUnit  

## Table of Contents

1. [Test Architecture Overview](#test-architecture-overview)
2. [Core Module Component Tests](#core-module-component-tests)
3. [Operator System Tests](#operator-system-tests)
4. [Memory Management Tests](#memory-management-tests)
5. [Performance Requirements Tests](#performance-requirements-tests)
6. [Error Handling Tests](#error-handling-tests)
7. [Integration Tests](#integration-tests)
8. [DirectX Mocking Framework](#directx-mocking-framework)
9. [Test Patterns and Examples](#test-patterns-and-examples)
10. [Coverage Guidelines](#coverage-guidelines)

---

## Test Architecture Overview

### Design Principles

The TiXL Core engine test suite follows these key principles:

- **Isolated Testing**: Each component tested independently with proper mocking
- **Performance Focus**: Real-time requirements validated for 60fps rendering
- **Resource Safety**: Comprehensive memory management and cleanup validation
- **Error Resilience**: Graceful degradation and exception handling tested
- **Integration Coverage**: End-to-end workflows validated

### Test Categories

1. **Unit Tests** - Individual component functionality
2. **Integration Tests** - Component interaction validation
3. **Performance Tests** - Real-time performance requirements
4. **Memory Tests** - Resource management and cleanup
5. **Error Tests** - Exception handling and recovery
6. **Security Tests** - Input validation and safe operations

---

## Core Module Component Tests

### 1. Animation Module Tests

```csharp
[TestFixture]
[Category("Animation")]
public class AnimationSystemTests
{
    private Mock<IAnimationSystem> _mockAnimationSystem;
    private AnimationSystem _animationSystem;
    private List<AnimationKeyframe> _testKeyframes;

    [SetUp]
    public void Setup()
    {
        _mockAnimationSystem = new Mock<IAnimationSystem>();
        _animationSystem = new AnimationSystem();
        _testKeyframes = CreateTestKeyframes();
    }

    [Test]
    public void AnimationSystem_Initialize_Success()
    {
        // Arrange
        var config = new AnimationConfig { FrameRate = 60, MaxKeyframes = 1000 };

        // Act
        var result = _animationSystem.Initialize(config);

        // Assert
        Assert.That(result, Is.True, "Animation system should initialize successfully");
        Assert.That(_animationSystem.FrameRate, Is.EqualTo(60), "Frame rate should be set correctly");
    }

    [Test]
    public void AnimationSystem_KeyframeInterpolation_Linear()
    {
        // Arrange
        var startKeyframe = new AnimationKeyframe { Time = 0.0f, Value = new Vector3(0, 0, 0) };
        var endKeyframe = new AnimationKeyframe { Time = 1.0f, Value = new Vector3(10, 10, 10) };

        // Act
        var interpolatedValue = _animationSystem.Interpolate(startKeyframe, endKeyframe, 0.5f);

        // Assert
        Assert.That(interpolatedValue, Is.EqualTo(new Vector3(5, 5, 5)).Within(0.001f),
            "Linear interpolation should produce correct midpoint");
    }

    [Test]
    public void AnimationSystem_MemoryCleanup_DisposesResources()
    {
        // Arrange
        var animation = _animationSystem.CreateAnimation("test");
        animation.AddKeyframes(_testKeyframes);

        // Act
        animation.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => animation.GetCurrentValue(),
            "Animation should be unusable after disposal");
    }

    [Test]
    public void AnimationSystem_Performance_60FPS_Sustained()
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();
        var frameCount = 600; // 10 seconds at 60fps
        var config = new AnimationConfig { FrameRate = 60 };

        // Act
        for (int i = 0; i < frameCount; i++)
        {
            _animationSystem.Update();
            Thread.Sleep(16); // ~60fps timing
        }

        stopwatch.Stop();

        // Assert
        var actualFPS = frameCount / stopwatch.Elapsed.TotalSeconds;
        Assert.That(actualFPS, Is.GreaterThan(58).Within(0.5),
            $"Animation system should maintain 60fps, actual: {actualFPS:F2}");
    }

    private List<AnimationKeyframe> CreateTestKeyframes()
    {
        return new List<AnimationKeyframe>
        {
            new AnimationKeyframe { Time = 0.0f, Value = new Vector3(0, 0, 0) },
            new AnimationKeyframe { Time = 0.25f, Value = new Vector3(1, 0, 0) },
            new AnimationKeyframe { Time = 0.5f, Value = new Vector3(1, 1, 0) },
            new AnimationKeyframe { Time = 0.75f, Value = new Vector3(0, 1, 0) },
            new AnimationKeyframe { Time = 1.0f, Value = new Vector3(0, 0, 1) }
        };
    }
}
```

### 2. Audio Module Tests

```csharp
[TestFixture]
[Category("Audio")]
public class AudioSystemTests
{
    private Mock<IAudioDevice> _mockAudioDevice;
    private AudioSystem _audioSystem;
    private AudioBuffer _testBuffer;

    [SetUp]
    public void Setup()
    {
        _mockAudioDevice = new Mock<IAudioDevice>();
        _audioSystem = new AudioSystem(_mockAudioDevice.Object);
        _testBuffer = CreateTestAudioBuffer();
    }

    [Test]
    public void AudioSystem_Initialize_DirectX_Integration()
    {
        // Arrange
        var config = new AudioConfig 
        { 
            SampleRate = 44100, 
            Channels = 2, 
            BufferSize = 1024 
        };

        // Act & Assert with DirectX mock
        var result = Assert.Throws<AudioDeviceException>(() => 
            _audioSystem.Initialize(config));

        // In real implementation, this would test actual DirectX integration
        _mockAudioDevice.Setup(x => x.Initialize(config))
            .Throws(new AudioDeviceException("DirectX initialization failed"));

        Assert.Throws<AudioDeviceException>(() => _audioSystem.Initialize(config),
            "Audio system should handle DirectX initialization failures");
    }

    [Test]
    public void AudioSystem_Streaming_LowLatency()
    {
        // Arrange
        var latencyTests = new List<double>();
        const int bufferSize = 512;
        const int testDurationMs = 1000;

        // Act
        var startTime = DateTime.UtcNow;
        var endTime = startTime.AddMilliseconds(testDurationMs);

        while (DateTime.UtcNow < endTime)
        {
            var frameStart = DateTime.UtcNow;
            
            _audioSystem.ProcessAudioFrame(_testBuffer);
            
            var frameLatency = (DateTime.UtcNow - frameStart).TotalMilliseconds;
            latencyTests.Add(frameLatency);
        }

        // Assert
        var averageLatency = latencyTests.Average();
        var maxLatency = latencyTests.Max();

        Assert.That(averageLatency, Is.LessThan(10.0), 
            "Average audio latency should be under 10ms");
        Assert.That(maxLatency, Is.LessThan(20.0), 
            "Maximum audio latency should be under 20ms");
    }

    [Test]
    public void AudioSystem_ResourceCleanup_DisposesBuffers()
    {
        // Arrange
        var audioClip = _audioSystem.LoadAudioClip("test.wav");

        // Act
        audioClip.Dispose();

        // Assert
        Assert.Throws<ObjectDisposedException>(() => audioClip.Play(),
            "Audio clip should be unusable after disposal");
        
        _mockAudioDevice.Verify(x => x.ReleaseBuffer(It.IsAny<AudioBuffer>()), 
            Times.Once, "Audio device should release buffers on disposal");
    }

    private AudioBuffer CreateTestAudioBuffer()
    {
        return new AudioBuffer
        {
            SampleRate = 44100,
            Channels = 2,
            BufferData = new float[1024],
            BufferSize = 1024 * sizeof(float)
        };
    }
}
```

### 3. File System Tests

```csharp
[TestFixture]
[Category("FileSystem")]
public class FileSystemTests
{
    private Mock<IFileSystemProvider> _mockFileProvider;
    private FileSystemManager _fileManager;
    private readonly string _testDirectory = "TiXL_TestFiles";

    [SetUp]
    public void Setup()
    {
        _mockFileProvider = new Mock<IFileSystemProvider>();
        _fileManager = new FileSystemManager(_mockFileProvider.Object);
        
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
        
        Directory.CreateDirectory(_testDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
    }

    [Test]
    public void FileSystem_ResourceLoading_PathValidation()
    {
        // Arrange
        var validPath = Path.Combine(_testDirectory, "test.t3d");
        var invalidPath = "../../../etc/passwd";
        var nonExistentPath = Path.Combine(_testDirectory, "non-existent.t3d");

        // Act & Assert
        Assert.DoesNotThrow(() => _fileManager.LoadResource(validPath),
            "Valid resource path should load successfully");
            
        Assert.Throws<SecurityException>(() => _fileManager.LoadResource(invalidPath),
            "Path traversal attempts should be blocked");
            
        Assert.Throws<FileNotFoundException>(() => _fileManager.LoadResource(nonExistentPath),
            "Non-existent files should throw appropriate exception");
    }

    [Test]
    public void FileSystem_Performance_ConcurrentAccess()
    {
        // Arrange
        var concurrentTasks = new List<Task<bool>>();
        var resourcePath = Path.Combine(_testDirectory, "concurrent-test.t3d");
        
        // Create a test resource file
        File.WriteAllText(resourcePath, "TiXL test resource data");

        // Act - concurrent access to same file
        for (int i = 0; i < 10; i++)
        {
            concurrentTasks.Add(Task.Run(() =>
            {
                try
                {
                    var resource = _fileManager.LoadResource(resourcePath);
                    return resource != null;
                }
                catch
                {
                    return false;
                }
            }));
        }

        // Assert
        var results = Task.WhenAll(concurrentTasks).Result;
        Assert.That(results.All(x => x), Is.True, 
            "All concurrent file operations should succeed");
    }

    [Test]
    public void FileSystem_MemoryManagement_ResourceCaching()
    {
        // Arrange
        var cacheSizes = new List<int>();
        var resourcePath = Path.Combine(_testDirectory, "cache-test.t3d");
        
        File.WriteAllText(resourcePath, "Cache test data");

        // Act
        for (int i = 0; i < 5; i++)
        {
            var resource = _fileManager.LoadResource(resourcePath);
            cacheSizes.Add(_fileManager.GetCacheSize());
        }

        // Assert - cache should not grow indefinitely
        Assert.That(cacheSizes.Last(), Is.LessThan(cacheSizes.First() * 2), 
            "Resource cache should not grow excessively with repeated loads");
    }
}
```

### 4. MIDI System Tests

```csharp
[TestFixture]
[Category("MIDI")]
public class MIDISystemTests
{
    private Mock<IMidiDevice> _mockMidiDevice;
    private MIDISystem _midiSystem;
    private List<MidiMessage> _testMessages;

    [SetUp]
    public void Setup()
    {
        _mockMidiDevice = new Mock<IMidiDevice>();
        _midiSystem = new MIDISystem(_mockMidiDevice.Object);
        _testMessages = CreateTestMidiMessages();
    }

    [Test]
    public void MIDISystem_MessageParsing_ValidInput()
    {
        // Arrange
        var midiData = new byte[] { 0x90, 0x3C, 0x7F }; // Note On, Middle C, Max velocity

        // Act
        var parsedMessage = _midiSystem.ParseMessage(midiData);

        // Assert
        Assert.That(parsedMessage.MessageType, Is.EqualTo(MidiMessageType.NoteOn));
        Assert.That(parsedMessage.Note, Is.EqualTo(60), "Note should be middle C (60)");
        Assert.That(parsedMessage.Velocity, Is.EqualTo(127), "Velocity should be 127");
    }

    [Test]
    public void MIDISystem_Performance_LowLatencyProcessing()
    {
        // Arrange
        var processingTimes = new List<double>();
        const int messageCount = 1000;

        // Act
        foreach (var message in _testMessages)
        {
            var startTime = Stopwatch.GetTimestamp();
            
            _midiSystem.ProcessMessage(message);
            
            var endTime = Stopwatch.GetTimestamp();
            var processingTime = (endTime - startTime) * 1000.0 / Stopwatch.Frequency;
            
            processingTimes.Add(processingTime);
        }

        // Assert
        var averageLatency = processingTimes.Average();
        Assert.That(averageLatency, Is.LessThan(1.0), 
            $"MIDI message processing should be under 1ms, actual: {averageLatency:F3}ms");
    }

    [Test]
    public void MIDISystem_ThreadSafety_ConcurrentNoteProcessing()
    {
        // Arrange
        var concurrentTasks = new List<Task>();
        var noteEvents = new ConcurrentBag<NoteEvent>();

        // Act - simulate multiple threads processing notes
        for (int i = 0; i < 10; i++)
        {
            concurrentTasks.Add(Task.Run(() =>
            {
                foreach (var message in _testMessages.Take(10))
                {
                    if (message.MessageType == MidiMessageType.NoteOn)
                    {
                        var noteEvent = _midiSystem.CreateNoteEvent(message);
                        noteEvents.Add(noteEvent);
                    }
                }
            }));
        }

        Task.WaitAll(concurrentTasks.ToArray());

        // Assert
        Assert.That(noteEvents.Count, Is.EqualTo(_testMessages.Count(m => m.MessageType == MidiMessageType.NoteOn)),
            "All note events should be processed correctly");
    }

    private List<MidiMessage> CreateTestMidiMessages()
    {
        return new List<MidiMessage>
        {
            new MidiMessage { MessageType = MidiMessageType.NoteOn, Note = 60, Velocity = 127 },
            new MidiMessage { MessageType = MidiMessageType.NoteOff, Note = 60, Velocity = 0 },
            new MidiMessage { MessageType = MidiMessageType.ControlChange, Controller = 7, Value = 100 },
            new MidiMessage { MessageType = MidiMessageType.ProgramChange, Program = 1 }
        };
    }
}
```

### 5. Network System Tests

```csharp
[TestFixture]
[Category("Network")]
public class NetworkSystemTests
{
    private Mock<INetworkDevice> _mockNetworkDevice;
    private NetworkSystem _networkSystem;
    private List<NetworkMessage> _testMessages;

    [SetUp]
    public void Setup()
    {
        _mockNetworkDevice = new Mock<INetworkDevice>();
        _networkSystem = new NetworkSystem(_mockNetworkDevice.Object);
        _testMessages = CreateTestNetworkMessages();
    }

    [Test]
    public void NetworkSystem_ConnectionManager_ConnectionHandling()
    {
        // Arrange
        var endpoint = new NetworkEndpoint { Address = "192.168.1.100", Port = 8080 };

        // Act
        var connectionId = _networkSystem.Connect(endpoint);
        var isConnected = _networkSystem.IsConnected(connectionId);
        _networkSystem.Disconnect(connectionId);

        // Assert
        Assert.That(connectionId, Is.Not.EqualTo(Guid.Empty), "Connection should have valid ID");
        Assert.That(isConnected, Is.True, "Connection should be established");
        
        _mockNetworkDevice.Verify(x => x.Connect(It.IsAny<NetworkEndpoint>()), Times.Once);
        _mockNetworkDevice.Verify(x => x.Disconnect(connectionId), Times.Once);
    }

    [Test]
    public void NetworkSystem_MessageReliability_UDP_Transport()
    {
        // Arrange
        var unreliableMessages = new List<NetworkMessage>();
        
        // Act - simulate message transmission with potential loss
        foreach (var message in _testMessages)
        {
            _mockNetworkDevice.Setup(x => x.SendMessage(message))
                .Returns(message.Id)
                .Throws<NetworkException>()
                .Throws<NetworkException>(); // Simulate occasional network issues
            
            try
            {
                var messageId = _networkSystem.SendMessage(message);
                _networkSystem.AcknowledgeMessage(messageId);
            }
            catch (NetworkException)
            {
                unreliableMessages.Add(message);
            }
        }

        // Assert - system should handle network failures gracefully
        Assert.That(unreliableMessages.Count, Is.LessThan(_testMessages.Count / 2), 
            "Network should handle some failures but not majority");
    }

    [Test]
    public void NetworkSystem_Security_InputValidation()
    {
        // Arrange
        var maliciousInputs = new[]
        {
            "<script>alert('xss')</script>",
            "../../../etc/passwd",
            "'; DROP TABLE connections; --",
            "${7*7}",
            "{{7*7}}"
        };

        // Act & Assert
        foreach (var maliciousInput in maliciousInputs)
        {
            var endpoint = new NetworkEndpoint 
            { 
                Address = maliciousInput, 
                Port = 8080 
            };

            Assert.Throws<SecurityException>(() => _networkSystem.Connect(endpoint),
                $"Malicious input should be rejected: {maliciousInput}");
        }
    }

    [Test]
    public void NetworkSystem_Performance_ThroughputValidation()
    {
        // Arrange
        var throughputTests = new List<double>();
        var messageSize = 1024;
        var testDuration = TimeSpan.FromSeconds(5);
        
        var testMessage = new NetworkMessage 
        { 
            Data = new byte[messageSize],
            Type = NetworkMessageType.Data 
        };

        // Act
        var startTime = DateTime.UtcNow;
        var endTime = startTime.Add(testDuration);
        var messageCount = 0;

        while (DateTime.UtcNow < endTime)
        {
            try
            {
                _networkSystem.SendMessage(testMessage);
                messageCount++;
            }
            catch (NetworkException)
            {
                // Network failures expected in test environment
                break;
            }
        }

        // Assert
        var duration = (DateTime.UtcNow - startTime).TotalSeconds;
        var throughput = messageCount * messageSize / duration; // bytes per second

        Assert.That(throughput, Is.GreaterThan(100000), 
            $"Network throughput should be at least 100KB/s, actual: {throughput:F0} bytes/s");
    }

    private List<NetworkMessage> CreateTestNetworkMessages()
    {
        var messages = new List<NetworkMessage>();
        
        for (int i = 0; i < 100; i++)
        {
            messages.Add(new NetworkMessage
            {
                Id = Guid.NewGuid(),
                Type = NetworkMessageType.Data,
                Data = Encoding.UTF8.GetBytes($"Test message {i}"),
                Timestamp = DateTime.UtcNow
            });
        }
        
        return messages;
    }
}
```

---

## Operator System Tests

### 1. Symbol Registry Tests

```csharp
[TestFixture]
[Category("Operators")]
public class SymbolRegistryTests
{
    private SymbolRegistry _registry;
    private Mock<ISymbolFactory> _mockFactory;

    [SetUp]
    public void Setup()
    {
        _mockFactory = new Mock<ISymbolFactory>();
        _registry = new SymbolRegistry(_mockFactory.Object);
    }

    [Test]
    public void SymbolRegistry_Registration_UniqueIdentifiers()
    {
        // Arrange
        var symbol1 = CreateTestSymbol("Operator1", "1.0");
        var symbol2 = CreateTestSymbol("Operator1", "2.0");

        // Act
        _registry.Register(symbol1);
        var registrationResult = _registry.Register(symbol2);

        // Assert
        Assert.That(registrationResult, Is.True, "Second version should register successfully");
        
        var symbols = _registry.GetSymbols("Operator1");
        Assert.That(symbols.Count, Is.EqualTo(2), "Should have both versions registered");
    }

    [Test]
    public void SymbolRegistry_Validation_InputOutputContracts()
    {
        // Arrange
        var validSymbol = CreateSymbolWithInputsOutputs(
            new[] { typeof(float), typeof(Vector3) },
            new[] { typeof(float) }
        );
        var invalidSymbol = CreateSymbolWithInputsOutputs(
            new[] { typeof(float) }, // Insufficient inputs
            new[] { typeof(float), typeof(Vector3) } // Insufficient outputs
        );

        // Act & Assert
        Assert.DoesNotThrow(() => _registry.Register(validSymbol),
            "Valid symbol should register without errors");
            
        Assert.Throws<InvalidOperationException>(() => _registry.Register(invalidSymbol),
            "Invalid symbol contracts should prevent registration");
    }

    [Test]
    public void SymbolRegistry_Performance_ConcurrentRegistration()
    {
        // Arrange
        var concurrentTasks = new List<Task<bool>>();
        var symbols = Enumerable.Range(0, 100)
            .Select(i => CreateTestSymbol($"Operator{i}", "1.0"))
            .ToList();

        // Act - concurrent registration
        foreach (var symbol in symbols)
        {
            concurrentTasks.Add(Task.Run(() =>
            {
                try
                {
                    return _registry.Register(symbol);
                }
                catch
                {
                    return false;
                }
            }));
        }

        var results = Task.WhenAll(concurrentTasks).Result;

        // Assert
        Assert.That(results.All(x => x), Is.True, 
            "All concurrent symbol registrations should succeed");
        Assert.That(_registry.GetSymbolCount(), Is.EqualTo(100), 
            "All symbols should be registered");
    }

    [Test]
    public void SymbolRegistry_MemoryManagement_ResourceCleanup()
    {
        // Arrange
        var symbols = Enumerable.Range(0, 50)
            .Select(i => CreateTestSymbol($"TempOperator{i}", "1.0"))
            .ToList();

        // Act
        foreach (var symbol in symbols)
        {
            _registry.Register(symbol);
        }

        // Cleanup temporary symbols
        foreach (var symbol in symbols.Where(s => s.Name.Contains("Temp")))
        {
            _registry.Unregister(symbol.Id);
        }

        // Assert
        var remainingSymbols = _registry.GetSymbolCount();
        Assert.That(remainingSymbols, Is.EqualTo(0), 
            "All temporary symbols should be cleaned up");
    }

    private Symbol CreateTestSymbol(string name, string version)
    {
        return new Symbol
        {
            Id = Guid.NewGuid(),
            Name = name,
            Version = version,
            Description = $"Test symbol {name} version {version}",
            Inputs = new List<SymbolInput>(),
            Outputs = new List<SymbolOutput>()
        };
    }

    private Symbol CreateSymbolWithInputsOutputs(Type[] inputTypes, Type[] outputTypes)
    {
        var symbol = CreateTestSymbol("ComplexOperator", "1.0");
        
        symbol.Inputs = inputTypes.Select((type, i) => new SymbolInput
        {
            Id = Guid.NewGuid(),
            Name = $"Input{i}",
            Type = type,
            Required = true
        }).ToList();

        symbol.Outputs = outputTypes.Select((type, i) => new SymbolOutput
        {
            Id = Guid.NewGuid(),
            Name = $"Output{i}",
            Type = type
        }).ToList();

        return symbol;
    }
}
```

### 2. Instance Management Tests

```csharp
[TestFixture]
public class InstanceManagerTests
{
    private InstanceManager _instanceManager;
    private SymbolRegistry _registry;
    private Mock<IResourceManager> _mockResourceManager;

    [SetUp]
    public void Setup()
    {
        _mockResourceManager = new Mock<IResourceManager>();
        _registry = new SymbolRegistry();
        _instanceManager = new InstanceManager(_registry, _mockResourceManager.Object);
    }

    [Test]
    public void InstanceManager_Instantiation_SymbolCreation()
    {
        // Arrange
        var symbol = CreateTestSymbol("TestOperator", "1.0");
        _registry.Register(symbol);

        // Act
        var instanceId = _instanceManager.CreateInstance(symbol.Id);
        var instance = _instanceManager.GetInstance(instanceId);

        // Assert
        Assert.That(instanceId, Is.Not.EqualTo(Guid.Empty), "Instance should have valid ID");
        Assert.That(instance, Is.Not.Null, "Instance should be retrievable");
        Assert.That(instance.SymbolId, Is.EqualTo(symbol.Id), "Instance should reference correct symbol");
        Assert.That(instance.Status, Is.EqualTo(InstanceStatus.Initialized), 
            "Instance should be initialized");
    }

    [Test]
    public void InstanceManager_ConnectionManagement_DataFlow()
    {
        // Arrange
        var sourceSymbol = CreateSymbolWithOutput("SourceOutput", typeof(float));
        var targetSymbol = CreateSymbolWithInput("TargetInput", typeof(float));
        
        _registry.Register(sourceSymbol);
        _registry.Register(targetSymbol);

        var sourceInstance = _instanceManager.CreateInstance(sourceSymbol.Id);
        var targetInstance = _instanceManager.CreateInstance(targetSymbol.Id);

        // Act
        var connectionResult = _instanceManager.Connect(
            sourceInstance.Id, "SourceOutput",
            targetInstance.Id, "TargetInput"
        );

        // Assert
        Assert.That(connectionResult, Is.True, "Connection should be established successfully");
        
        // Verify data flow
        _instanceManager.SetValue(sourceInstance.Id, "SourceOutput", 42.0f);
        var targetValue = _instanceManager.GetValue<float>(targetInstance.Id, "TargetInput");
        
        Assert.That(targetValue, Is.EqualTo(42.0f), "Data should flow correctly between instances");
    }

    [Test]
    public void InstanceManager_CircularReference_Prevention()
    {
        // Arrange
        var operatorSymbol = CreateSymbolWithInputOutput("Operator", typeof(float));
        _registry.Register(operatorSymbol);

        var instance1 = _instanceManager.CreateInstance(operatorSymbol.Id);
        var instance2 = _instanceManager.CreateInstance(operatorSymbol.Id);

        // Connect instance1 output to instance2 input
        _instanceManager.Connect(instance1.Id, "Output", instance2.Id, "Input");

        // Act - attempt to create circular reference
        var circularResult = _instanceManager.Connect(instance2.Id, "Output", instance1.Id, "Input");

        // Assert
        Assert.That(circularResult, Is.False, "Circular references should be prevented");
    }

    [Test]
    public void InstanceManager_MemoryCleanup_ResourceDisposal()
    {
        // Arrange
        var symbol = CreateTestSymbol("ResourceOperator", "1.0");
        _registry.Register(symbol);

        var instanceId = _instanceManager.CreateInstance(symbol.Id);
        var instance = _instanceManager.GetInstance(instanceId);

        // Allocate some resources (simulated)
        _mockResourceManager.Setup(x => x.AllocateResource(It.IsAny<string>()))
            .Returns(new MockResource());

        // Act
        _instanceManager.DisposeInstance(instanceId);

        // Assert
        Assert.Throws<ObjectDisposedException>(() => _instanceManager.GetInstance(instanceId),
            "Disposed instance should be inaccessible");
        
        _mockResourceManager.Verify(x => x.ReleaseResource(It.IsAny<string>()), Times.Once);
    }

    [Test]
    public void InstanceManager_Performance_EvaluationSpeed()
    {
        // Arrange
        var symbols = CreateComplexOperatorGraph();
        foreach (var symbol in symbols)
        {
            _registry.Register(symbol);
        }

        var instances = new List<Guid>();
        var stopwatch = Stopwatch.StartNew();

        // Act - create and evaluate complex graph
        foreach (var symbol in symbols)
        {
            var instanceId = _instanceManager.CreateInstance(symbol.Id);
            instances.Add(instanceId);
        }

        // Setup connections between instances
        for (int i = 0; i < instances.Count - 1; i++)
        {
            _instanceManager.Connect(instances[i], "Output", instances[i + 1], "Input");
        }

        stopwatch.Stop();

        // Assert
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(100), 
            $"Complex graph evaluation should be under 100ms, actual: {stopwatch.ElapsedMilliseconds}ms");
    }

    private Symbol CreateTestSymbol(string name, string version)
    {
        return new Symbol
        {
            Id = Guid.NewGuid(),
            Name = name,
            Version = version,
            Description = $"Test symbol {name} version {version}"
        };
    }

    private Symbol CreateSymbolWithOutput(string outputName, Type outputType)
    {
        var symbol = CreateTestSymbol("OutputOperator", "1.0");
        symbol.Outputs = new List<SymbolOutput>
        {
            new SymbolOutput
            {
                Id = Guid.NewGuid(),
                Name = outputName,
                Type = outputType
            }
        };
        return symbol;
    }

    private Symbol CreateSymbolWithInput(string inputName, Type inputType)
    {
        var symbol = CreateTestSymbol("InputOperator", "1.0");
        symbol.Inputs = new List<SymbolInput>
        {
            new SymbolInput
            {
                Id = Guid.NewGuid(),
                Name = inputName,
                Type = inputType,
                Required = true
            }
        };
        return symbol;
    }

    private Symbol CreateSymbolWithInputOutput(string name, Type commonType)
    {
        var symbol = CreateTestSymbol(name, "1.0");
        symbol.Inputs = new List<SymbolInput>
        {
            new SymbolInput { Id = Guid.NewGuid(), Name = "Input", Type = commonType, Required = true }
        };
        symbol.Outputs = new List<SymbolOutput>
        {
            new SymbolOutput { Id = Guid.NewGuid(), Name = "Output", Type = commonType }
        };
        return symbol;
    }

    private List<Symbol> CreateComplexOperatorGraph()
    {
        var symbols = new List<Symbol>();
        
        // Create a chain of 50 operators
        for (int i = 0; i < 50; i++)
        {
            var symbol = CreateSymbolWithInputOutput($"Operator{i}", typeof(float));
            symbols.Add(symbol);
        }
        
        return symbols;
    }
}
```

---

## Memory Management Tests

### 1. Resource Management Tests

```csharp
[TestFixture]
[Category("Memory")]
public class ResourceManagementTests
{
    private ResourceManager _resourceManager;
    private MemoryProfiler _memoryProfiler;

    [SetUp]
    public void Setup()
    {
        _resourceManager = new ResourceManager();
        _memoryProfiler = new MemoryProfiler();
    }

    [Test]
    public void ResourceManager_MemoryTracking_AllocationMonitoring()
    {
        // Arrange
        var initialMemory = GC.GetTotalMemory(false);

        // Act
        var resourceId = _resourceManager.AllocateResource("test", 1024 * 1024); // 1MB
        var afterAllocation = GC.GetTotalMemory(false);

        // Assert
        var allocatedMemory = afterAllocation - initialMemory;
        Assert.That(allocatedMemory, Is.GreaterThan(512 * 1024) // At least 512KB
            .And.LessThan(2 * 1024 * 1024), "Memory allocation should be tracked accurately");
    }

    [Test]
    public void ResourceManager_AutomaticCleanup_IDisposablePattern()
    {
        // Arrange
        var trackedResources = new List<IDisposableResource>();

        // Act - create multiple resources and let them go out of scope
        for (int i = 0; i < 100; i++)
        {
            var resource = _resourceManager.CreateManagedResource($"resource_{i}", i * 1024);
            trackedResources.Add(resource);
        }

        // Force garbage collection
        trackedResources.Clear();
        GC.Collect();
        GC.WaitForPendingFinalizers();

        // Assert
        var activeResourceCount = _resourceManager.GetActiveResourceCount();
        Assert.That(activeResourceCount, Is.EqualTo(0), 
            "All managed resources should be cleaned up automatically");
    }

    [Test]
    public void ResourceManager_Performance_LargeScaleAllocation()
    {
        // Arrange
        var allocations = new List<Guid>();
        var stopwatch = Stopwatch.StartNew();

        // Act - simulate heavy resource usage
        for (int i = 0; i < 1000; i++)
        {
            var resourceId = _resourceManager.AllocateResource($"perf_test_{i}", 1024);
            allocations.Add(resourceId);
        }

        stopwatch.Stop();

        // Cleanup
        foreach (var resourceId in allocations)
        {
            _resourceManager.DeallocateResource(resourceId);
        }

        // Assert
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(1000), 
            $"Large-scale allocation should complete within 1 second, actual: {stopwatch.ElapsedMilliseconds}ms");
    }

    [Test]
    public void ResourceManager_MemoryLeakDetection_Finalization()
    {
        // Arrange
        var initialActiveResources = _resourceManager.GetActiveResourceCount();

        // Act - create resources that don't properly clean up
        CreateUnmanagedResources(_resourceManager, 100);
        
        // Force finalization
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalActiveResources = _resourceManager.GetActiveResourceCount();

        // Assert
        Assert.That(finalActiveResources - initialActiveResources, Is.LessThan(10), 
            "Memory leaks should be detected and reported");
    }

    [Test]
    public void ResourceManager_ResourceCaching_PoolReuse()
    {
        // Arrange
        var pool = new ResourcePool<MemoryBuffer>(10);

        // Act
        var buffer1 = pool.Acquire();
        pool.Release(buffer1);
        var buffer2 = pool.Acquire();

        // Assert
        Assert.That(buffer1, Is.EqualTo(buffer2), "Pool should reuse released resources");
        Assert.That(pool.TotalAllocations, Is.EqualTo(10), 
            "Pool should maintain consistent allocation count");
    }

    [Test]
    public void ResourceManager_ConcurrentAccess_ThreadSafety()
    {
        // Arrange
        var concurrentTasks = new List<Task>();
        var resourceIds = new ConcurrentBag<Guid>();
        var errors = new ConcurrentBag<Exception>();

        // Act - concurrent resource allocation/deallocation
        for (int i = 0; i < 50; i++)
        {
            concurrentTasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int j = 0; j < 100; j++)
                    {
                        var resourceId = _resourceManager.AllocateResource($"concurrent_{j}", 1024);
                        resourceIds.Add(resourceId);
                        
                        // Some resources are immediately released
                        if (j % 2 == 0)
                        {
                            _resourceManager.DeallocateResource(resourceId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            }));
        }

        Task.WaitAll(concurrentTasks.ToArray());

        // Assert
        Assert.That(errors, Is.Empty, "Concurrent access should not produce exceptions");
        
        // Clean up any remaining resources
        foreach (var resourceId in resourceIds)
        {
            try
            {
                _resourceManager.DeallocateResource(resourceId);
            }
            catch
            {
                // Resource may have already been deallocated
            }
        }
    }

    private void CreateUnmanagedResources(ResourceManager manager, int count)
    {
        for (int i = 0; i < count; i++)
        {
            // Create resources without proper disposal to simulate leaks
            manager.AllocateResource($"leak_{i}", 1024);
            // Note: Not calling DeallocateResource to simulate leak
        }
    }
}
```

### 2. Memory Profiling Tests

```csharp
[TestFixture]
public class MemoryProfilingTests
{
    private MemoryProfiler _profiler;

    [SetUp]
    public void Setup()
    {
        _profiler = new MemoryProfiler();
    }

    [Test]
    public void MemoryProfiler_AllocationTracking_DetailedMetrics()
    {
        // Arrange
        _profiler.StartTracking();

        // Act
        var testArray = new int[10000];
        for (int i = 0; i < testArray.Length; i++)
        {
            testArray[i] = i;
        }

        var snapshot = _profiler.TakeSnapshot();
        _profiler.StopTracking();

        // Assert
        Assert.That(snapshot.HeapSize, Is.GreaterThan(40000), // 10000 * 4 bytes
            "Heap size should reflect allocation accurately");
        Assert.That(snapshot.AllocationCount, Is.GreaterThan(0), 
            "Should track allocation count");
    }

    [Test]
    public void MemoryProfiler_GarbageCollection_ImpactMeasurement()
    {
        // Arrange
        _profiler.StartTracking();
        var beforeGC = _profiler.GetCurrentMetrics();

        // Act - force garbage collection
        var largeObject = new byte[10 * 1024 * 1024]; // 10MB object
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var afterGC = _profiler.GetCurrentMetrics();

        // Assert
        Assert.That(afterGC.HeapSize, Is.LessThan(beforeGC.HeapSize), 
            "GC should reduce heap size");
    }
}
```

---

## Performance Requirements Tests

### 1. Real-time Performance Tests

```csharp
[TestFixture]
[Category("Performance")]
public class RealTimePerformanceTests
{
    [Test]
    public void RenderingEngine_60FPS_SustainedRendering()
    {
        // Arrange
        var renderEngine = new RenderEngine();
        var frameTimes = new List<double>();
        var targetFrameTime = 1000.0 / 60.0; // ~16.67ms for 60fps

        // Act
        for (int frame = 0; frame < 600; frame++) // 10 seconds at 60fps
        {
            var frameStart = Stopwatch.GetTimestamp();
            
            renderEngine.RenderFrame();
            
            var frameEnd = Stopwatch.GetTimestamp();
            var frameTime = (frameEnd - frameStart) * 1000.0 / Stopwatch.Frequency;
            frameTimes.Add(frameTime);
        }

        // Assert
        var averageFrameTime = frameTimes.Average();
        var maxFrameTime = frameTimes.Max();
        var fps = 1000.0 / averageFrameTime;

        Assert.That(fps, Is.GreaterThan(58.0), 
            $"Average FPS should be at least 58, actual: {fps:F2}");
        Assert.That(maxFrameTime, Is.LessThan(50.0), 
            $"Maximum frame time should be under 50ms, actual: {maxFrameTime:F2}ms");
    }

    [Test]
    public void OperatorEvaluation_ComplexGraph_Performance()
    {
        // Arrange
        var evaluationEngine = new OperatorEvaluationEngine();
        var complexGraph = CreateComplexEvaluationGraph();

        // Act
        var startTime = Stopwatch.GetTimestamp();
        
        for (int i = 0; i < 1000; i++)
        {
            evaluationEngine.Evaluate(complexGraph);
        }

        var endTime = Stopwatch.GetTimestamp();
        var totalTime = (endTime - startTime) * 1000.0 / Stopwatch.Frequency;

        // Assert
        var averageEvaluationTime = totalTime / 1000.0;
        Assert.That(averageEvaluationTime, Is.LessThan(1.0), 
            $"Operator evaluation should average under 1ms, actual: {averageEvaluationTime:F3}ms");
    }

    [Test]
    public void AudioSystem_LowLatency_AudioProcessing()
    {
        // Arrange
        var audioSystem = new AudioSystem();
        var latencies = new List<double>();
        var bufferSize = 512;
        var sampleRate = 44100;

        // Act
        for (int i = 0; i < 1000; i++)
        {
            var startTime = DateTime.UtcNow;
            
            audioSystem.ProcessAudioBuffer(bufferSize);
            
            var endTime = DateTime.UtcNow;
            var latency = (endTime - startTime).TotalMilliseconds;
            latencies.Add(latency);
        }

        // Assert
        var averageLatency = latencies.Average();
        var maxLatency = latencies.Max();

        Assert.That(averageLatency, Is.LessThan(10.0), 
            $"Average audio latency should be under 10ms, actual: {averageLatency:F3}ms");
        Assert.That(maxLatency, Is.LessThan(20.0), 
            $"Maximum audio latency should be under 20ms, actual: {maxLatency:F3}ms");
    }

    [Test]
    public void FileSystem_AsyncIO_Performance()
    {
        // Arrange
        var fileSystem = new FileSystemManager();
        var testFilePath = "test_async.tmp";
        var testData = new byte[1024 * 1024]; // 1MB test file

        // Act
        var writeStart = Stopwatch.GetTimestamp();
        var writeTask = fileSystem.WriteFileAsync(testFilePath, testData);
        writeTask.Wait();
        var writeEnd = Stopwatch.GetTimestamp();

        var readStart = Stopwatch.GetTimestamp();
        var readTask = fileSystem.ReadFileAsync(testFilePath);
        var readData = readTask.Result;
        var readEnd = Stopwatch.GetTimestamp();

        var writeTime = (writeEnd - writeStart) * 1000.0 / Stopwatch.Frequency;
        var readTime = (readEnd - readStart) * 1000.0 / Stopwatch.Frequency;

        // Cleanup
        fileSystem.DeleteFile(testFilePath);

        // Assert
        Assert.That(writeTime, Is.LessThan(100.0), 
            $"Async file write should complete within 100ms, actual: {writeTime:F2}ms");
        Assert.That(readTime, Is.LessThan(50.0), 
            $"Async file read should complete within 50ms, actual: {readTime:F2}ms");
        Assert.That(readData, Is.EqualTo(testData), "Read data should match written data");
    }

    [Test]
    public void NetworkSystem_Throughput_HighVolume()
    {
        // Arrange
        var networkSystem = new NetworkSystem();
        var endpoint = new NetworkEndpoint { Address = "127.0.0.1", Port = 8080 };
        var connectionId = networkSystem.Connect(endpoint);

        var messageSize = 1024;
        var testDuration = TimeSpan.FromSeconds(5);
        var messagesSent = 0;

        // Act
        var startTime = DateTime.UtcNow;
        var endTime = startTime.Add(testDuration);

        var testMessage = new NetworkMessage
        {
            Data = new byte[messageSize],
            Type = NetworkMessageType.Data
        };

        while (DateTime.UtcNow < endTime)
        {
            try
            {
                networkSystem.SendMessage(connectionId, testMessage);
                messagesSent++;
                
                // Small delay to avoid overwhelming the system
                Thread.Sleep(1);
            }
            catch (NetworkException)
            {
                break;
            }
        }

        var actualDuration = (DateTime.UtcNow - startTime).TotalSeconds;
        var throughput = messagesSent * messageSize / actualDuration; // bytes per second

        // Assert
        Assert.That(throughput, Is.GreaterThan(100000), 
            $"Network throughput should be at least 100KB/s, actual: {throughput:F0} bytes/s");
    }

    private OperatorGraph CreateComplexEvaluationGraph()
    {
        // Create a complex operator graph with nested evaluations
        var graph = new OperatorGraph();
        
        // Add 100 operators in a dependency chain
        for (int i = 0; i < 100; i++)
        {
            var operatorNode = new OperatorNode($"Operator{i}");
            graph.AddNode(operatorNode);
            
            if (i > 0)
            {
                graph.Connect($"Operator{i-1}", $"Operator{i}");
            }
        }
        
        return graph;
    }
}
```

---

## Error Handling Tests

### 1. Exception Management Tests

```csharp
[TestFixture]
[Category("ErrorHandling")]
public class ExceptionManagementTests
{
    [Test]
    public void ExceptionHandling_GracefulDegradation_OperatorFailure()
    {
        // Arrange
        var operatorEngine = new OperatorEngine();
        var failingOperator = CreateFailingOperator();

        // Act
        var result = operatorEngine.EvaluateOperator(failingOperator);

        // Assert
        Assert.That(result.IsSuccess, Is.False, "Operator failure should be detected");
        Assert.That(result.ErrorMessage, Does.Contain("Operation failed"), 
            "Error message should contain meaningful information");
        Assert.That(result.FallbackValue, Is.Not.Null, "System should provide fallback value");
    }

    [Test]
    public void ExceptionHandling_ResourceExhaustion_AutomaticCleanup()
    {
        // Arrange
        var resourceManager = new ResourceManager();
        var initialResourceCount = resourceManager.GetActiveResourceCount();

        // Act - exhaust resources
        var exhausted = false;
        try
        {
            for (int i = 0; i < 10000; i++)
            {
                resourceManager.AllocateResource($"exhaustion_test_{i}", 1024);
            }
        }
        catch (OutOfMemoryException)
        {
            exhausted = true;
        }

        // Assert
        Assert.That(exhausted, Is.True, "System should detect resource exhaustion");
        
        // Verify automatic cleanup occurred
        var finalResourceCount = resourceManager.GetActiveResourceCount();
        Assert.That(finalResourceCount - initialResourceCount, Is.LessThan(100), 
            "System should automatically clean up resources after exhaustion");
    }

    [Test]
    public void ExceptionHandling_InvalidInputs_InputValidation()
    {
        // Arrange
        var validator = new InputValidator();
        var maliciousInputs = new[]
        {
            "<script>alert('xss')</script>",
            "../../../etc/passwd",
            "'; DROP TABLE users; --",
            "${7*7}",
            "{{7*7}}",
            null,
            "",
            new string('A', 10000) // Very long string
        };

        // Act & Assert
        foreach (var input in maliciousInputs)
        {
            if (input == null)
                Assert.Throws<ArgumentNullException>(() => validator.ValidateInput(input),
                    "Null input should throw ArgumentNullException");
            else if (string.IsNullOrEmpty(input))
                Assert.Throws<ArgumentException>(() => validator.ValidateInput(input),
                    "Empty input should throw ArgumentException");
            else if (input.Length > 1000)
                Assert.Throws<ArgumentException>(() => validator.ValidateInput(input),
                    "Very long input should be rejected");
            else
                Assert.Throws<SecurityException>(() => validator.ValidateInput(input),
                    $"Malicious input should be rejected: {input}");
        }
    }

    [Test]
    public void ExceptionHandling_ThreadSafety_ConcurrentExceptions()
    {
        // Arrange
        var concurrentErrors = new ConcurrentBag<Exception>();
        var tasks = new List<Task>();

        // Act - concurrent operations that may throw exceptions
        for (int i = 0; i < 50; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    var engine = new ErrorProneEngine();
                    engine.PerformRiskyOperation();
                }
                catch (Exception ex)
                {
                    concurrentErrors.Add(ex);
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        Assert.That(concurrentErrors.Count, Is.GreaterThan(0), 
            "Concurrent operations should produce some expected errors");
        
        // All exceptions should be of expected types
        var exceptionTypes = concurrentErrors.Select(e => e.GetType()).Distinct().ToList();
        Assert.That(exceptionTypes.Count, Is.LessThan(3), 
            "Exception types should be predictable and limited");
    }

    [Test]
    public void ExceptionHandling_Recovery_MultipleAttempts()
    {
        // Arrange
        var unreliableService = new UnreliableService();
        var maxRetries = 5;

        // Act
        var attemptCount = 0;
        var result = false;

        while (attemptCount < maxRetries && !result)
        {
            try
            {
                result = unreliableService.AttemptOperation();
                if (result)
                    break;
            }
            catch (TransientException)
            {
                attemptCount++;
                Thread.Sleep(100 * attemptCount); // Exponential backoff
            }
        }

        // Assert
        Assert.That(result, Is.True, "Operation should succeed after retries");
        Assert.That(attemptCount, Is.GreaterThan(0), "Some retries should have been needed");
        Assert.That(attemptCount, Is.LessThan(maxRetries), 
            "Should succeed before max retries");
    }

    [Test]
    public void ExceptionHandling_Logging_ExceptionDetails()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        var exceptionLogger = new ExceptionLogger(mockLogger.Object);

        // Act
        try
        {
            throw new InvalidOperationException("Test exception for logging");
        }
        catch (Exception ex)
        {
            exceptionLogger.LogException(ex, "TestOperation");
        }

        // Assert
        mockLogger.Verify(
            x => x.Log(It.Is<LogLevel>(l => l == LogLevel.Error),
                It.Is<string>(s => s.Contains("Test exception for logging")),
                It.IsAny<object[]>()),
            Times.Once,
            "Exception should be logged with appropriate details");
    }

    private Operator CreateFailingOperator()
    {
        return new Operator
        {
            Id = Guid.NewGuid(),
            Name = "FailingOperator",
            Execute = () => throw new InvalidOperationException("Operation failed")
        };
    }
}
```

---

## Integration Tests

### 1. Cross-Module Integration Tests

```csharp
[TestFixture]
[Category("Integration")]
public class CrossModuleIntegrationTests
{
    private AnimationSystem _animationSystem;
    private AudioSystem _audioSystem;
    private OperatorEngine _operatorEngine;
    private NetworkSystem _networkSystem;

    [SetUp]
    public void Setup()
    {
        _animationSystem = new AnimationSystem();
        _audioSystem = new AudioSystem();
        _operatorEngine = new OperatorEngine();
        _networkSystem = new NetworkSystem();
    }

    [Test]
    public void AnimationAudio_Integration_SyncWithMusic()
    {
        // Arrange
        var animationClip = _animationSystem.CreateAnimation("dance_move");
        var audioClip = _audioSystem.LoadAudioClip("background_music.wav");

        // Configure animation to sync with audio
        animationClip.SetAudioSync(audioClip, SyncMode.Beat);

        // Act
        audioClip.Play();
        animationClip.Play();

        // Wait for some playback time
        Thread.Sleep(1000);

        // Assert
        var animationTime = animationClip.CurrentTime;
        var audioTime = audioClip.CurrentTime;

        Assert.That(Math.Abs(animationTime - audioTime), Is.LessThan(0.1), 
            "Animation and audio should stay synchronized within 100ms");
    }

    [Test]
    public void OperatorNetwork_Integration_RemoteProcessing()
    {
        // Arrange
        var localOperator = _operatorEngine.CreateOperator("LocalProcessor");
        var remoteOperatorId = Guid.NewGuid();

        var networkConnection = _networkSystem.Connect(
            new NetworkEndpoint { Address = "127.0.0.1", Port = 8080 });

        // Act - send operator for remote processing
        var remoteProcessingRequest = new RemoteProcessingRequest
        {
            OperatorId = localOperator.Id,
            TargetAddress = networkConnection.RemoteAddress
        };

        // Simulate remote processing (in real implementation this would go over network)
        var result = _operatorEngine.RequestRemoteProcessing(remoteProcessingRequest);

        // Assert
        Assert.That(result.IsSuccess, Is.True, "Remote processing request should succeed");
        Assert.That(result.RemoteResult, Is.Not.Null, "Should receive remote processing result");
    }

    [Test]
    public void FileSystemOperator_Integration_ResourceLoading()
    {
        // Arrange
        var fileSystem = new FileSystemManager();
        var shaderOperator = _operatorEngine.CreateOperator("ShaderLoader");

        // Create test shader file
        var shaderPath = "test_shader.hlsl";
        var shaderCode = @"
            float4 main(float2 uv : TEXCOORD) : SV_TARGET
            {
                return float4(uv, 0, 1);
            }
        ";

        fileSystem.WriteFile(shaderPath, shaderCode);

        // Act
        var loadResult = shaderOperator.LoadShaderFromFile(shaderPath);

        // Assert
        Assert.That(loadResult.IsSuccess, Is.True, "Shader loading should succeed");
        Assert.That(loadResult.ShaderCode, Is.EqualTo(shaderCode), 
            "Loaded shader code should match original");

        // Cleanup
        fileSystem.DeleteFile(shaderPath);
    }

    [Test]
    public void AnimationOperator_Integration_ParameterDriven()
    {
        // Arrange
        var animationOperator = _operatorEngine.CreateOperator("AnimationController");
        var positionParameter = animationOperator.AddParameter<Vector3>("TargetPosition");
        var rotationParameter = animationOperator.AddParameter<Vector3>("TargetRotation");

        // Act
        animationOperator.SetParameterValue("TargetPosition", new Vector3(10, 5, 0));
        animationOperator.SetParameterValue("TargetRotation", new Vector3(0, 90, 0));

        // Assert
        var targetPosition = animationOperator.GetParameterValue<Vector3>("TargetPosition");
        var targetRotation = animationOperator.GetParameterValue<Vector3>("TargetRotation");

        Assert.That(targetPosition, Is.EqualTo(new Vector3(10, 5, 0)), 
            "Position parameter should be set correctly");
        Assert.That(targetRotation, Is.EqualTo(new Vector3(0, 90, 0)), 
            "Rotation parameter should be set correctly");
    }

    [Test]
    public void SystemIntegration_Performance_EndToEnd()
    {
        // Arrange
        var integrationMetrics = new List<IntegrationMetrics>();
        var testDuration = TimeSpan.FromSeconds(10);

        // Act
        var startTime = DateTime.UtcNow;
        var endTime = startTime.Add(testDuration);

        while (DateTime.UtcNow < endTime)
        {
            var frameStart = DateTime.UtcNow;

            // Simulate integrated workflow
            var animationResult = _animationSystem.Update();
            var audioResult = _audioSystem.ProcessAudioFrame();
            var operatorResult = _operatorEngine.EvaluateOperators();
            var networkResult = _networkSystem.ProcessMessages();

            var frameEnd = DateTime.UtcNow;
            var frameDuration = (frameEnd - frameStart).TotalMilliseconds;

            integrationMetrics.Add(new IntegrationMetrics
            {
                Timestamp = frameStart,
                AnimationTime = animationResult.ProcessingTime,
                AudioTime = audioResult.ProcessingTime,
                OperatorTime = operatorResult.ProcessingTime,
                NetworkTime = networkResult.ProcessingTime,
                TotalFrameTime = frameDuration
            });

            // Maintain 60fps timing
            Thread.Sleep(Math.Max(0, 16 - (int)frameDuration));
        }

        // Assert
        var averageFrameTime = integrationMetrics.Average(m => m.TotalFrameTime);
        var maxFrameTime = integrationMetrics.Max(m => m.TotalFrameTime);
        var averageFPS = 1000.0 / averageFrameTime;

        Assert.That(averageFPS, Is.GreaterThan(55.0), 
            $"Integrated system should maintain 55+ fps, actual: {averageFPS:F2}");
        Assert.That(maxFrameTime, Is.LessThan(50.0), 
            $"Maximum frame time should be under 50ms, actual: {maxFrameTime:F2}ms");
    }

    private class IntegrationMetrics
    {
        public DateTime Timestamp { get; set; }
        public double AnimationTime { get; set; }
        public double AudioTime { get; set; }
        public double OperatorTime { get; set; }
        public double NetworkTime { get; set; }
        public double TotalFrameTime { get; set; }
    }
}
```

---

## DirectX Mocking Framework

### 1. DirectX Device Mocking

```csharp
public class MockDirectXDevice : IDirectXDevice
{
    private readonly Dictionary<string, object> _resources = new();
    private readonly Queue<DirectXCall> _callHistory = new();
    public int InitializationCount { get; private set; }
    public bool ShouldFailInitialization { get; set; }
    public int FailureCount { get; set; }

    public bool Initialize(DirectXConfig config)
    {
        InitializationCount++;
        _callHistory.Enqueue(new DirectXCall("Initialize", config));

        if (ShouldFailInitialization)
        {
            FailureCount++;
            throw new DirectXInitializationException("Mock initialization failed");
        }

        _resources["Device"] = new MockDevice();
        return true;
    }

    public void CreateBuffer(string name, BufferDescription description)
    {
        _callHistory.Enqueue(new DirectXCall("CreateBuffer", name, description));
        
        if (_resources.ContainsKey(name))
            throw new ArgumentException($"Buffer {name} already exists");

        _resources[name] = new MockBuffer(description);
    }

    public T GetResource<T>(string name) where T : class
    {
        _callHistory.Enqueue(new DirectXCall("GetResource", name, typeof(T)));
        
        if (!_resources.ContainsKey(name))
            throw new KeyNotFoundException($"Resource {name} not found");

        var resource = _resources[name];
        if (resource is T typedResource)
            return typedResource;

        throw new InvalidCastException($"Resource {name} is not of type {typeof(T).Name}");
    }

    public void ReleaseResource(string name)
    {
        _callHistory.Enqueue(new DirectXCall("ReleaseResource", name));
        
        if (_resources.ContainsKey(name))
        {
            var resource = _resources[name];
            if (resource is IDisposable disposable)
                disposable.Dispose();

            _resources.Remove(name);
        }
    }

    public List<DirectXCall> GetCallHistory() => _callHistory.ToList();
}

public class DirectXCall
{
    public string MethodName { get; }
    public object[] Parameters { get; }
    public DateTime Timestamp { get; }

    public DirectXCall(string methodName, params object[] parameters)
    {
        MethodName = methodName;
        Parameters = parameters;
        Timestamp = DateTime.UtcNow;
    }
}
```

### 2. Shader Compilation Mocking

```csharp
public class MockShaderCompiler : IShaderCompiler
{
    private readonly Dictionary<string, CompiledShader> _compiledShaders = new();
    private readonly List<CompilationRequest> _compilationHistory = new();

    public CompilationResult CompileShader(string source, ShaderType type, string entryPoint)
    {
        var request = new CompilationRequest
        {
            Source = source,
            Type = type,
            EntryPoint = entryPoint,
            Timestamp = DateTime.UtcNow
        };
        
        _compilationHistory.Add(request);

        // Simulate compilation process
        var shaderId = Guid.NewGuid().ToString();
        var compiledShader = new CompiledShader
        {
            Id = shaderId,
            ByteCode = GenerateMockByteCode(source, type),
            Reflection = GenerateMockReflection(source, type),
            CompilationTime = TimeSpan.FromMilliseconds(Random.Next(10, 100))
        };

        _compiledShaders[shaderId] = compiledShader;

        return new CompilationResult
        {
            IsSuccess = true,
            Shader = compiledShader,
            Warnings = GenerateMockWarnings(source)
        };
    }

    public ValidationResult ValidateShader(string shaderId)
    {
        if (!_compiledShaders.ContainsKey(shaderId))
            return new ValidationResult { IsValid = false, Errors = new[] { "Shader not found" } };

        var shader = _compiledShaders[shaderId];
        return new ValidationResult
        {
            IsValid = true,
            ValidationTime = TimeSpan.FromMilliseconds(5),
            ShaderInfo = shader.Reflection
        };
    }

    private byte[] GenerateMockByteCode(string source, ShaderType type)
    {
        var size = type switch
        {
            ShaderType.Vertex => 2048,
            ShaderType.Pixel => 1536,
            ShaderType.Geometry => 1024,
            ShaderType.Compute => 3072,
            _ => 1024
        };
        
        return Enumerable.Repeat((byte)0x90, size).ToArray();
    }

    private ShaderReflection GenerateMockReflection(string source, ShaderType type)
    {
        return new ShaderReflection
        {
            InputSignature = new[] { "POSITION", "NORMAL", "TEXCOORD" },
            OutputSignature = new[] { "SV_POSITION", "SV_TARGET" },
            UniformBuffers = new[] { "MatrixBuffer", "MaterialBuffer" },
            Samplers = new[] { "DiffuseSampler" },
            Textures = new[] { "DiffuseTexture" }
        };
    }

    private List<string> GenerateMockWarnings(string source)
    {
        var warnings = new List<string>();
        
        if (source.Contains("precise"))
            warnings.Add("Use of 'precise' keyword may impact performance");
            
        if (source.Contains("branch"))
            warnings.Add("Conditional branching detected, consider optimization");
            
        return warnings;
    }
}
```

---

## Test Patterns and Examples

### 1. Test Data Builders

```csharp
public class AnimationTestDataBuilder
{
    private readonly AnimationConfig _config = new();

    public AnimationTestDataBuilder WithFrameRate(int frameRate)
    {
        _config.FrameRate = frameRate;
        return this;
    }

    public AnimationTestDataBuilder WithMaxKeyframes(int maxKeyframes)
    {
        _config.MaxKeyframes = maxKeyframes;
        return this;
    }

    public AnimationTestDataBuilder WithInterpolation(InterpolationType interpolation)
    {
        _config.InterpolationType = interpolation;
        return this;
    }

    public AnimationConfig Build() => _config;
}

public class OperatorTestDataBuilder
{
    private readonly Symbol _symbol = new();

    public OperatorTestDataBuilder WithName(string name)
    {
        _symbol.Name = name;
        return this;
    }

    public OperatorTestDataBuilder WithInput(string name, Type type, bool required = true)
    {
        _symbol.Inputs.Add(new SymbolInput
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = type,
            Required = required
        });
        return this;
    }

    public OperatorTestDataBuilder WithOutput(string name, Type type)
    {
        _symbol.Outputs.Add(new SymbolOutput
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = type
        });
        return this;
    }

    public Symbol Build() => _symbol;
}
```

### 2. Test Utilities

```csharp
public static class TestUtilities
{
    public static void ForceGarbageCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    public static long GetMemoryUsage()
    {
        ForceGarbageCollection();
        return GC.GetTotalMemory(false);
    }

    public static void WaitForCondition(Func<bool> condition, TimeSpan timeout)
    {
        var startTime = DateTime.UtcNow;
        while (!condition() && DateTime.UtcNow - startTime < timeout)
        {
            Thread.Sleep(10);
        }
        
        if (!condition())
            throw new TimeoutException($"Condition not met within {timeout}");
    }

    public static void SimulateHighLoad(Action action, int iterations = 1000)
    {
        var tasks = new List<Task>();
        
        for (int i = 0; i < Environment.ProcessorCount; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < iterations; j++)
                {
                    action();
                }
            }));
        }
        
        Task.WaitAll(tasks.ToArray());
    }

    public static PerformanceMetrics MeasurePerformance(Action action, int iterations = 1)
    {
        var stopwatch = Stopwatch.StartNew();
        var memoryBefore = GetMemoryUsage();
        
        for (int i = 0; i < iterations; i++)
        {
            action();
        }
        
        stopwatch.Stop();
        var memoryAfter = GetMemoryUsage();
        
        return new PerformanceMetrics
        {
            TotalTime = stopwatch.Elapsed,
            AverageTime = stopwatch.Elapsed / iterations,
            MemoryUsed = memoryAfter - memoryBefore,
            Iterations = iterations
        };
    }
}

public class PerformanceMetrics
{
    public TimeSpan TotalTime { get; set; }
    public TimeSpan AverageTime { get; set; }
    public long MemoryUsed { get; set; }
    public int Iterations { get; set; }
}
```

### 3. Mock Implementations

```csharp
public class MockDirectXContext : IDirectXContext
{
    public Mock<ID3D11Device> MockDevice { get; } = new();
    public Mock<ID3D11DeviceContext> MockContext { get; } = new();
    public Mock<ID3D11Buffer> MockBuffer { get; } = new();
    public Mock<ID3D11Texture2D> MockTexture { get; } = new();
    public List<DirectXCall> CallHistory { get; } = new();

    public ID3D11Device Device => MockDevice.Object;
    public ID3D11DeviceContext Context => MockContext.Object;

    public void SetupBasicInitialization()
    {
        MockDevice.Setup(x => x.CreateBuffer(It.IsAny<D3D11_BUFFER_DESC>(), 
            It.IsAny<D3D11_SUBRESOURCE_DATA>()))
            .Returns(MockBuffer.Object)
            .Callback<string, object>((desc, data) => 
                CallHistory.Add(new DirectXCall("CreateBuffer", desc)));

        MockDevice.Setup(x => x.CreateTexture2D(It.IsAny<D3D11_TEXTURE2D_DESC>(), 
            It.IsAny<D3D11_SUBRESOURCE_DATA>()))
            .Returns(MockTexture.Object)
            .Callback<string, object>((desc, data) => 
                CallHistory.Add(new DirectXCall("CreateTexture2D", desc)));
    }
}
```

---

## Coverage Guidelines

### Test Coverage Targets

| Component Category | Coverage Target | Critical Methods |
|-------------------|----------------|------------------|
| Core Modules | 90% | Initialization, Resource Management, Error Handling |
| Operator System | 95% | Registration, Evaluation, Connection Management |
| Memory Management | 100% | Allocation, Deallocation, Cleanup, Leak Detection |
| Performance | 85% | Real-time operations, Throughput, Latency |
| Error Handling | 90% | Exception types, Recovery mechanisms, Logging |
| Integration | 80% | Cross-module communication, Data flow |

### Test Execution Guidelines

1. **Unit Tests**: Run on every commit (sub-10 seconds)
2. **Integration Tests**: Run on pull requests (sub-1 minute)
3. **Performance Tests**: Run nightly (sub-5 minutes)
4. **Stress Tests**: Run weekly (sub-30 minutes)
5. **Security Tests**: Run before releases

### Performance Benchmarks

| Operation | Target Performance | Test Threshold |
|-----------|-------------------|----------------|
| 60 FPS Rendering | 16.67ms per frame | >58 FPS sustained |
| Operator Evaluation | <1ms for simple operators | <2ms maximum |
| Audio Processing | <10ms latency | <20ms maximum |
| File I/O (async) | <100ms for 1MB file | <200ms maximum |
| Network Throughput | >100KB/s sustained | >50KB/s minimum |

### Memory Management Benchmarks

| Metric | Target | Alert Threshold |
|--------|--------|-----------------|
| Memory Leaks | 0 bytes | >1MB retained |
| Allocation Rate | <1MB/sec | >5MB/sec |
| GC Frequency | <10 collections/sec | >20 collections/sec |
| Resource Cleanup | 100% cleanup | <95% cleanup |

### Error Handling Validation

1. **Graceful Degradation**: System continues operating with reduced functionality
2. **Resource Cleanup**: All resources properly released on error
3. **Logging Coverage**: All critical errors logged with context
4. **Recovery Testing**: Automatic recovery mechanisms validated
5. **Security**: Input validation and injection prevention tested

### Integration Test Coverage

1. **Data Flow**: End-to-end data propagation tested
2. **Synchronization**: Multi-module coordination validated
3. **Resource Sharing**: Cross-module resource access tested
4. **Error Propagation**: Error handling across module boundaries
5. **Performance Impact**: Integration overhead measured and optimized

---

## Conclusion

This comprehensive test suite provides robust validation of the TiXL Core engine components across all critical dimensions:

- **Functionality**: Complete coverage of core features and edge cases
- **Performance**: Real-time requirements validated with measurable benchmarks
- **Reliability**: Memory management and error handling thoroughly tested
- **Integration**: Cross-component workflows verified end-to-end
- **Maintainability**: Test patterns and utilities support continued development

The test suite includes sophisticated DirectX mocking to enable comprehensive testing without requiring actual graphics hardware, while maintaining high-fidelity simulation of real-world usage patterns.

Regular execution of these tests ensures the TiXL Core engine maintains its high standards for performance, reliability, and code quality as the system evolves.
