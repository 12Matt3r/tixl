using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TiXL.Core.IO;
using TiXL.Core.Performance;

namespace TiXL.Core.IO.Examples
{
    /// <summary>
    /// Comprehensive example demonstrating the TiXL I/O Isolation System
    /// Shows how to implement all I/O types with proper isolation from the render thread
    /// </summary>
    public class IOIsolationDemo
    {
        private TiXLIOIsolationSystem _ioSystem;
        private PerformanceMonitor _performanceMonitor;
        private Stopwatch _demoStopwatch;
        
        public async Task RunDemoAsync()
        {
            Console.WriteLine("=== TiXL I/O Isolation System Demo ===");
            Console.WriteLine();
            
            // Initialize the system
            await InitializeSystem();
            
            // Run different I/O scenarios
            await DemoAudioIO();
            await DemoMidiIO();
            await DemoFileIO();
            await DemoNetworkIO();
            await DemoSpoutIO();
            await DemoBatchProcessing();
            await DemoPriorityManagement();
            await DemoErrorRecovery();
            
            // Show final statistics
            ShowFinalStatistics();
            
            // Cleanup
            Cleanup();
        }
        
        private async Task InitializeSystem()
        {
            Console.WriteLine("1. Initializing I/O Isolation System...");
            
            _performanceMonitor = new PerformanceMonitor();
            _ioSystem = new TiXLIOIsolationSystem(_performanceMonitor);
            
            var initialized = await _ioSystem.InitializeAsync();
            if (initialized)
            {
                Console.WriteLine("   ✓ System initialized successfully");
            }
            else
            {
                Console.WriteLine("   ✗ System initialization failed");
                throw new Exception("Failed to initialize I/O system");
            }
            
            _demoStopwatch = Stopwatch.StartNew();
            
            // Subscribe to events for monitoring
            _ioSystem.EventQueued += (sender, e) => 
                Console.WriteLine($"   Event queued: {e.EventType} (Priority: {e.Priority})");
            
            _ioSystem.EventCompleted += (sender, e) => 
                Console.WriteLine($"   Event completed: {e.EventId} - {e.Status}");
            
            _ioSystem.SystemAlert += (sender, alert) => 
                Console.WriteLine($"   System Alert: {alert.Message}");
            
            Console.WriteLine();
        }
        
        private async Task DemoAudioIO()
        {
            Console.WriteLine("2. Demonstrating Audio I/O...");
            
            // Simulate audio input
            var audioInputData = GenerateAudioData(44100, 2, 16, 1.0); // 1 second of audio
            var inputResult = await _ioSystem.QueueAudioInputAsync(audioInputData);
            Console.WriteLine($"   Audio input queued: {inputResult.IsSuccess}");
            
            // Simulate audio output
            var audioOutputData = GenerateAudioData(44100, 2, 16, 0.8); // 1 second at 80% volume
            var outputResult = await _ioSystem.QueueAudioOutputAsync(audioOutputData);
            Console.WriteLine($"   Audio output queued: {outputResult.IsSuccess}");
            
            // Wait for processing
            await Task.Delay(100);
            
            Console.WriteLine();
        }
        
        private async Task DemoMidiIO()
        {
            Console.WriteLine("3. Demonstrating MIDI I/O...");
            
            // Simulate MIDI input
            var midiInputData = GenerateMidiData(10); // 10 MIDI messages
            var midiInputResult = await _ioSystem.QueueMidiInputAsync(midiInputData);
            Console.WriteLine($"   MIDI input queued: {midiInputResult.IsSuccess}");
            
            // Simulate MIDI output
            var midiOutputData = GenerateMidiData(5); // 5 MIDI messages
            var midiOutputResult = await _ioSystem.QueueMidiOutputAsync(midiOutputData);
            Console.WriteLine($"   MIDI output queued: {midiOutputResult.IsSuccess}");
            
            await Task.Delay(50);
            
            Console.WriteLine();
        }
        
        private async Task DemoFileIO()
        {
            Console.WriteLine("4. Demonstrating File I/O...");
            
            // Create temporary test files
            var testFiles = new List<string>();
            
            try
            {
                // File write operations
                for (int i = 0; i < 3; i++)
                {
                    var filePath = Path.Combine(Path.GetTempPath(), $"tixl_test_{i}.txt");
                    var fileData = GenerateTextData(1024); // 1KB of text
                    var writeResult = await _ioSystem.QueueFileWriteAsync(filePath, fileData);
                    Console.WriteLine($"   File write queued: {Path.GetFileName(filePath)} - {writeResult.IsSuccess}");
                    testFiles.Add(filePath);
                }
                
                // File read operations
                foreach (var filePath in testFiles)
                {
                    var readResult = await _ioSystem.QueueFileReadAsync(filePath);
                    Console.WriteLine($"   File read queued: {Path.GetFileName(filePath)} - {readResult.IsSuccess}");
                }
                
                await Task.Delay(200);
                
                // Cleanup
                foreach (var filePath in testFiles)
                {
                    try { File.Delete(filePath); } catch { }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   File I/O error: {ex.Message}");
            }
            
            Console.WriteLine();
        }
        
        private async Task DemoNetworkIO()
        {
            Console.WriteLine("5. Demonstrating Network I/O...");
            
            // Simulate network events
            var endpoints = new[] { "tcp://localhost:8080", "udp://localhost:8081", "http://localhost:8082" };
            
            foreach (var endpoint in endpoints)
            {
                var networkData = GenerateNetworkData(512); // 512 bytes
                var result = await _ioSystem.QueueNetworkEventAsync(endpoint, networkData);
                Console.WriteLine($"   Network event queued: {endpoint} - {result.IsSuccess}");
            }
            
            await Task.Delay(150);
            
            Console.WriteLine();
        }
        
        private async Task DemoSpoutIO()
        {
            Console.WriteLine("6. Demonstrating Spout/Texture Sharing...");
            
            // Simulate texture sharing
            var spoutTextures = new[] { "MainOutput", "SecondaryView", "UIOverlay" };
            
            foreach (var textureName in spoutTextures)
            {
                var textureData = GenerateTextureData(1920, 1080, 32); // Full HD RGBA texture
                var result = await _ioSystem.QueueSpoutEventAsync(textureName, textureData);
                Console.WriteLine($"   Spout event queued: {textureName} - {result.IsSuccess}");
            }
            
            await Task.Delay(100);
            
            Console.WriteLine();
        }
        
        private async Task DemoBatchProcessing()
        {
            Console.WriteLine("7. Demonstrating Batch Processing...");
            
            var batchEvents = new List<IOEvent>();
            
            // Create a batch of mixed events
            for (int i = 0; i < 10; i++)
            {
                IOEvent evt;
                
                if (i % 3 == 0)
                {
                    evt = IOEvent.CreateAudioEvent(IOEventType.AudioInput, GenerateAudioData(44100, 2, 16, 0.5), IOEventPriority.High);
                }
                else if (i % 3 == 1)
                {
                    evt = IOEvent.CreateFileEvent(IOEventType.FileRead, $"batch_test_{i}.txt", GenerateTextData(256), IOEventPriority.Medium);
                }
                else
                {
                    evt = IOEvent.CreateUserInputEvent("Keyboard", $"Key_{i}", IOEventPriority.Critical);
                }
                
                batchEvents.Add(evt);
            }
            
            Console.WriteLine($"   Processing batch of {batchEvents.Count} events...");
            var batchResult = await _ioSystem.ProcessEventBatchAsync(batchEvents);
            
            Console.WriteLine($"   Batch processed: {batchResult.SuccessfulEvents}/{batchResult.TotalEvents} successful");
            Console.WriteLine($"   Processing time: {batchResult.TotalProcessingTime.TotalMilliseconds:F2}ms");
            
            await Task.Delay(100);
            
            Console.WriteLine();
        }
        
        private async Task DemoPriorityManagement()
        {
            Console.WriteLine("8. Demonstrating Priority Management...");
            
            // Create events with different priorities
            var criticalEvent = IOEvent.CreateUserInputEvent("Mouse", "Click", IOEventPriority.Critical);
            var highEvent = IOEvent.CreateAudioEvent(IOEventType.AudioInput, GenerateAudioData(44100, 2, 16, 0.3), IOEventPriority.High);
            var mediumEvent = IOEvent.CreateFileEvent(IOEventType.FileWrite, "priority_test.txt", GenerateTextData(512), IOEventPriority.Medium);
            var lowEvent = IOEvent.CreateFileEvent(IOEventType.CacheUpdate, "cache_file.json", GenerateTextData(128), IOEventPriority.Low);
            
            var priorityEvents = new[] { lowEvent, mediumEvent, highEvent, criticalEvent };
            
            Console.WriteLine("   Queuing events in random priority order...");
            
            foreach (var evt in priorityEvents.OrderBy(_ => Guid.NewGuid()))
            {
                var result = await _ioSystem.QueueEventAsync(evt);
                Console.WriteLine($"   {evt.Priority} priority event queued: {result.IsSuccess}");
            }
            
            await Task.Delay(200);
            
            Console.WriteLine();
        }
        
        private async Task DemoErrorRecovery()
        {
            Console.WriteLine("9. Demonstrating Error Recovery...");
            
            try
            {
                // Queue some valid events first
                var validEvent = IOEvent.CreateAudioEvent(IOEventType.AudioInput, GenerateAudioData(44100, 2, 16, 0.5));
                var validResult = await _ioSystem.QueueAudioInputAsync(validEvent.Data);
                Console.WriteLine($"   Valid event queued: {validResult.IsSuccess}");
                
                // Try to queue an event that might fail (nonexistent file)
                var invalidEvent = IOEvent.CreateFileEvent(IOEventType.FileRead, "/nonexistent/path/file.txt");
                var invalidResult = await _ioSystem.QueueEventAsync(invalidEvent);
                Console.WriteLine($"   Invalid event queued: {invalidResult.IsSuccess}");
                
                await Task.Delay(100);
                
                // Show error recovery in action
                Console.WriteLine("   Error recovery system will handle failures gracefully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   Error recovery demo: {ex.Message}");
            }
            
            Console.WriteLine();
        }
        
        private void ShowFinalStatistics()
        {
            Console.WriteLine("10. Final System Statistics:");
            Console.WriteLine();
            
            var stats = _ioSystem.GetSystemStatistics();
            
            Console.WriteLine($"   System Initialized: {stats.SystemInitialized}");
            Console.WriteLine($"   Total Events Processed: {stats.IsolationManagerStats.TotalEventsProcessed}");
            Console.WriteLine($"   Total Events Batched: {stats.IsolationManagerStats.TotalEventsBatched}");
            Console.WriteLine($"   Frame Time Saved: {stats.IsolationManagerStats.TotalFrameSavingsMs}ms");
            Console.WriteLine($"   Average Frame Savings: {stats.IsolationManagerStats.AverageFrameSavingsMs:F2}ms");
            Console.WriteLine();
            
            Console.WriteLine("   Queue Statistics:");
            Console.WriteLine($"     High Priority Queue: {stats.IsolationManagerStats.HighPriorityQueueStats.CurrentSize}");
            Console.WriteLine($"     Medium Priority Queue: {stats.IsolationManagerStats.MediumPriorityQueueStats.CurrentSize}");
            Console.WriteLine($"     Low Priority Queue: {stats.IsolationManagerStats.LowPriorityQueueStats.CurrentSize}");
            Console.WriteLine();
            
            Console.WriteLine("   Handler Statistics:");
            Console.WriteLine($"     Active File Operations: {stats.FileHandlerStats.ActiveOperations}");
            Console.WriteLine($"     Active Network Connections: {stats.NetworkHandlerStats.ActiveConnections}");
            Console.WriteLine($"     Shared Spout Textures: {stats.SpoutHandlerStats.SharedTextures}");
            Console.WriteLine();
            
            Console.WriteLine($"   Demo Duration: {_demoStopwatch.Elapsed.TotalSeconds:F2} seconds");
        }
        
        private void Cleanup()
        {
            Console.WriteLine();
            Console.WriteLine("Cleanup...");
            
            _ioSystem?.Dispose();
            _performanceMonitor?.Dispose();
            
            Console.WriteLine("Demo completed successfully!");
        }
        
        // Helper methods for generating test data
        
        private byte[] GenerateAudioData(int sampleRate, int channels, int bitDepth, double volume)
        {
            var bytesPerSample = bitDepth / 8;
            var frameCount = sampleRate * channels; // 1 second
            var totalBytes = frameCount * bytesPerSample;
            var data = new byte[totalBytes];
            
            var random = new Random();
            for (int i = 0; i < frameCount; i++)
            {
                var sample = (short)(random.Next(-32768, 32767) * volume);
                var bytes = BitConverter.GetBytes(sample);
                
                for (int j = 0; j < bytesPerSample && (i * bytesPerSample + j) < totalBytes; j++)
                {
                    data[i * bytesPerSample + j] = bytes[j];
                }
            }
            
            return data;
        }
        
        private byte[] GenerateMidiData(int messageCount)
        {
            var data = new List<byte>();
            var random = new Random();
            
            for (int i = 0; i < messageCount; i++)
            {
                var status = (byte)(0x90 + random.Next(0, 16)); // Note on messages
                var note = (byte)random.Next(60, 84); // Middle octave notes
                var velocity = (byte)random.Next(64, 127);
                
                data.Add(status);
                data.Add(note);
                data.Add(velocity);
            }
            
            return data.ToArray();
        }
        
        private byte[] GenerateTextData(int size)
        {
            var data = new char[size];
            var random = new Random();
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 ";
            
            for (int i = 0; i < size; i++)
            {
                data[i] = chars[random.Next(chars.Length)];
            }
            
            return System.Text.Encoding.UTF8.GetBytes(data);
        }
        
        private byte[] GenerateNetworkData(int size)
        {
            var data = new byte[size];
            new Random().NextBytes(data);
            return data;
        }
        
        private byte[] GenerateTextureData(int width, int height, int bitsPerPixel)
        {
            var bytesPerPixel = bitsPerPixel / 8;
            var size = width * height * bytesPerPixel;
            var data = new byte[size];
            
            // Generate a simple gradient texture
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var index = (y * width + x) * bytesPerPixel;
                    
                    if (index + bytesPerPixel <= size)
                    {
                        var r = (byte)((x * 255) / width);
                        var g = (byte)((y * 255) / height);
                        var b = (byte)(((x + y) * 255) / (width + height));
                        var a = (byte)255;
                        
                        if (bytesPerPixel >= 4)
                        {
                            data[index] = b;
                            data[index + 1] = g;
                            data[index + 2] = r;
                            data[index + 3] = a;
                        }
                        else
                        {
                            data[index] = b;
                            data[index + 1] = g;
                            data[index + 2] = r;
                        }
                    }
                }
            }
            
            return data;
        }
    }
    
    /// <summary>
    /// Entry point for running the I/O isolation demo
    /// </summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("TiXL I/O Isolation System - Comprehensive Demo");
            Console.WriteLine("==============================================");
            Console.WriteLine();
            
            try
            {
                var demo = new IOIsolationDemo();
                await demo.RunDemoAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Demo failed: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}