using System;
using System.Threading.Tasks;
using TiXL.Core.IO;
using TiXL.Core.Performance;

namespace TiXL.Tests.IO
{
    /// <summary>
    /// Unit tests for the TIXL-024 I/O isolation system
    /// </summary>
    public class IOIsolationSystemTests
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("=== TIXL-024 I/O Isolation System Tests ===");
            Console.WriteLine();
            
            try
            {
                await TestSystemInitialization();
                await TestAudioEventQueuing();
                await TestFileEventQueuing();
                await TestNetworkEventQueuing();
                await TestPriorityManagement();
                await TestBatchProcessing();
                
                Console.WriteLine();
                Console.WriteLine("✓ All tests passed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"✗ Test failed: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
        
        private static async Task TestSystemInitialization()
        {
            Console.WriteLine("Test 1: System Initialization");
            
            var performanceMonitor = new PerformanceMonitor();
            var ioSystem = new TiXLIOIsolationSystem(performanceMonitor);
            
            var initialized = await ioSystem.InitializeAsync();
            if (!initialized)
            {
                throw new Exception("System initialization failed");
            }
            
            Console.WriteLine("  ✓ System initialized successfully");
            
            ioSystem.Dispose();
            performanceMonitor.Dispose();
        }
        
        private static async Task TestAudioEventQueuing()
        {
            Console.WriteLine("Test 2: Audio Event Queuing");
            
            var ioSystem = new TiXLIOIsolationSystem();
            await ioSystem.InitializeAsync();
            
            // Generate test audio data
            var audioData = new byte[1024]; // 1KB of audio data
            new Random().NextBytes(audioData);
            
            // Queue audio input
            var result1 = await ioSystem.QueueAudioInputAsync(audioData);
            if (!result1.IsSuccess)
            {
                throw new Exception("Failed to queue audio input");
            }
            
            // Queue audio output
            var result2 = await ioSystem.QueueAudioOutputAsync(audioData);
            if (!result2.IsSuccess)
            {
                throw new Exception("Failed to queue audio output");
            }
            
            Console.WriteLine("  ✓ Audio events queued successfully");
            
            // Wait for processing
            await Task.Delay(100);
            
            ioSystem.Dispose();
        }
        
        private static async Task TestFileEventQueuing()
        {
            Console.WriteLine("Test 3: File Event Queuing");
            
            var ioSystem = new TiXLIOIsolationSystem();
            await ioSystem.InitializeAsync();
            
            // Test file write
            var fileData = new byte[512];
            new Random().NextBytes(fileData);
            
            var writeResult = await ioSystem.QueueFileWriteAsync("test_file.txt", fileData);
            if (!writeResult.IsSuccess)
            {
                throw new Exception("Failed to queue file write");
            }
            
            // Test file read
            var readResult = await ioSystem.QueueFileReadAsync("test_file.txt");
            if (!readResult.IsSuccess)
            {
                throw new Exception("Failed to queue file read");
            }
            
            Console.WriteLine("  ✓ File events queued successfully");
            
            await Task.Delay(200);
            
            ioSystem.Dispose();
        }
        
        private static async Task TestNetworkEventQueuing()
        {
            Console.WriteLine("Test 4: Network Event Queuing");
            
            var ioSystem = new TiXLIOIsolationSystem();
            await ioSystem.InitializeAsync();
            
            var networkData = new byte[256];
            new Random().NextBytes(networkData);
            
            var result = await ioSystem.QueueNetworkEventAsync("tcp://localhost:8080", networkData);
            if (!result.IsSuccess)
            {
                throw new Exception("Failed to queue network event");
            }
            
            Console.WriteLine("  ✓ Network events queued successfully");
            
            await Task.Delay(100);
            
            ioSystem.Dispose();
        }
        
        private static async Task TestPriorityManagement()
        {
            Console.WriteLine("Test 5: Priority Management");
            
            var ioSystem = new TiXLIOIsolationSystem();
            await ioSystem.InitializeAsync();
            
            // Create events with different priorities
            var criticalEvent = IOEvent.CreateUserInputEvent("Mouse", "Click", IOEventPriority.Critical);
            var highEvent = IOEvent.CreateAudioEvent(IOEventType.AudioInput, new byte[256], IOEventPriority.High);
            var mediumEvent = IOEvent.CreateFileEvent(IOEventType.FileRead, "test.txt", null, IOEventPriority.Medium);
            var lowEvent = IOEvent.CreateFileEvent(IOEventType.CacheUpdate, "cache.json", new byte[128], IOEventPriority.Low);
            
            var events = new[] { criticalEvent, highEvent, mediumEvent, lowEvent };
            
            // Queue all events
            foreach (var evt in events)
            {
                var result = await ioSystem.QueueEventAsync(evt);
                if (!result.IsSuccess)
                {
                    throw new Exception($"Failed to queue {evt.Priority} priority event");
                }
            }
            
            Console.WriteLine("  ✓ All priority events queued successfully");
            
            await Task.Delay(100);
            
            ioSystem.Dispose();
        }
        
        private static async Task TestBatchProcessing()
        {
            Console.WriteLine("Test 6: Batch Processing");
            
            var ioSystem = new TiXLIOIsolationSystem();
            await ioSystem.InitializeAsync();
            
            // Create batch of events
            var batchEvents = new System.Collections.Generic.List<IOEvent>();
            
            for (int i = 0; i < 5; i++)
            {
                var audioData = new byte[128];
                new Random().NextBytes(audioData);
                
                var evt = IOEvent.CreateAudioEvent(IOEventType.AudioInput, audioData, IOEventPriority.High);
                batchEvents.Add(evt);
            }
            
            var batchResult = await ioSystem.ProcessEventBatchAsync(batchEvents);
            if (!batchResult.IsSuccess)
            {
                throw new Exception("Batch processing failed");
            }
            
            Console.WriteLine($"  ✓ Batch processed: {batchResult.SuccessfulEvents}/{batchResult.TotalEvents} events");
            
            await Task.Delay(100);
            
            ioSystem.Dispose();
        }
    }
}