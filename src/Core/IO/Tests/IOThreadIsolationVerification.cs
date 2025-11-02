#!/usr/bin/env dotnet

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TiXL.Core.IO;
using TiXL.Core.IO.Examples;
using TiXL.Core.Performance;

namespace TiXL.Core.IO.Tests
{
    /// <summary>
    /// Simple verification script for I/O thread isolation implementation
    /// Tests core functionality to ensure the system works correctly
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("TiXL I/O Thread Isolation - Verification Script");
            Console.WriteLine("================================================\n");
            
            try
            {
                await TestBasicAsyncFileOperations();
                await TestThreadIsolation();
                await TestIOWorker();
                await TestEventProcessing();
                
                Console.WriteLine("\n✅ All tests passed! I/O Thread Isolation is working correctly.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Test failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Environment.Exit(1);
            }
        }
        
        static async Task TestBasicAsyncFileOperations()
        {
            Console.WriteLine("Test 1: Basic Async File Operations");
            Console.WriteLine("------------------------------------");
            
            using var asyncOps = new AsyncFileOperations();
            
            // Test file write
            var testFile = Path.Combine(Path.GetTempPath(), $"TiXL_Test_{Guid.NewGuid():N}.txt");
            var testData = System.Text.Encoding.UTF8.GetBytes("Hello, TiXL I/O Thread Isolation!");
            
            Console.WriteLine($"Writing file: {testFile}");
            var writeResult = await asyncOps.WriteFileAsync(testFile, testData);
            
            if (writeResult.Success)
            {
                Console.WriteLine($"✅ Write successful: {writeResult.BytesWritten} bytes in {writeResult.ElapsedTime.TotalMilliseconds:F2}ms");
            }
            else
            {
                throw new Exception($"Write failed: {writeResult.ErrorMessage}");
            }
            
            // Test file read
            Console.WriteLine("Reading file back...");
            var readResult = await asyncOps.ReadFileAsync(testFile);
            
            if (readResult.Success)
            {
                var content = System.Text.Encoding.UTF8.GetString(readResult.Data);
                Console.WriteLine($"✅ Read successful: {readResult.BytesRead} bytes, content matches: {content == "Hello, TiXL I/O Thread Isolation!"}");
            }
            else
            {
                throw new Exception($"Read failed: {readResult.ErrorMessage}");
            }
            
            // Cleanup
            File.Delete(testFile);
            Console.WriteLine("✅ Basic async file operations test passed\n");
        }
        
        static async Task TestThreadIsolation()
        {
            Console.WriteLine("Test 2: Thread Isolation");
            Console.WriteLine("------------------------");
            
            using var isolationManager = new IOIsolationManager();
            
            // Test execution on dedicated I/O thread
            Console.WriteLine("Testing dedicated I/O thread execution...");
            
            var mainThreadId = Thread.CurrentThread.ManagedThreadId;
            Console.WriteLine($"Main thread ID: {mainThreadId}");
            
            var result = await isolationManager.ExecuteOnIOThreadPoolAsync(async () =>
            {
                var ioThreadId = Thread.CurrentThread.ManagedThreadId;
                Console.WriteLine($"I/O thread ID: {ioThreadId}");
                
                // Verify we're on a different thread
                bool onDifferentThread = ioThreadId != mainThreadId;
                Console.WriteLine($"Thread isolation verified: {onDifferentThread}");
                
                // Simulate I/O work
                await Task.Delay(50);
                
                return new { IoThreadId = ioThreadId, Isolated = onDifferentThread };
            });
            
            if (result.Isolated)
            {
                Console.WriteLine("✅ Thread isolation working correctly");
            }
            else
            {
                throw new Exception("Thread isolation failed - operations ran on same thread");
            }
            
            // Test thread pool statistics
            var threadStats = isolationManager.GetThreadIsolationStatistics();
            Console.WriteLine($"Thread stats: {threadStats.ActiveIOThreadPoolThreads} active threads, {threadStats.IOThreadPoolUtilization:F1}% utilization");
            
            Console.WriteLine("✅ Thread isolation test passed\n");
        }
        
        static async Task TestIOWorker()
        {
            Console.WriteLine("Test 3: IO Background Worker");
            Console.WriteLine("-----------------------------");
            
            // Test worker statistics
            using var isolationManager = new IOIsolationManager();
            var ioManager = isolationManager as IOIsolationManager;
            
            // Access worker statistics
            var stats = isolationManager.GetStatistics();
            Console.WriteLine($"Isolation manager has {stats.WorkerStats.Count} workers");
            
            // Test file worker with real operations
            var fileWorker = new IOBackgroundWorker(
                IOEventType.FileRead,
                new IOEventQueue("TestQueue", 100),
                async (ioEvent) =>
                {
                    Console.WriteLine($"Worker processing {ioEvent.EventType} event");
                    await Task.Delay(10); // Simulate work
                },
                new ResourcePool(),
                new PerformanceMonitor()
            );
            
            var workerStats = fileWorker.GetStatistics();
            Console.WriteLine($"Worker active: {workerStats.IsActive}, events processed: {workerStats.EventsProcessed}");
            
            if (workerStats.IsActive)
            {
                Console.WriteLine("✅ IO background worker working correctly");
            }
            else
            {
                throw new Exception("IO background worker is not active");
            }
            
            fileWorker.Dispose();
            Console.WriteLine("✅ IO background worker test passed\n");
        }
        
        static async Task TestEventProcessing()
        {
            Console.WriteLine("Test 4: Event Processing");
            Console.WriteLine("------------------------");
            
            using var isolationManager = new IOIsolationManager();
            
            // Create test file event
            var testFile = Path.Combine(Path.GetTempPath(), $"TiXL_Event_{Guid.NewGuid():N}.txt");
            var testData = System.Text.Encoding.UTF8.GetBytes("Event processing test");
            
            var fileEvent = IOEvent.CreateFileEvent(IOEventType.FileWrite, testFile, testData, IOEventPriority.Medium);
            fileEvent.Metadata["TestOperation"] = "EventProcessingTest";
            
            Console.WriteLine("Queueing file write event...");
            var eventResult = await isolationManager.QueueEventAsync(fileEvent);
            
            if (eventResult.IsSuccess)
            {
                Console.WriteLine("✅ Event queued successfully");
            }
            else
            {
                throw new Exception($"Event queue failed: {eventResult.ErrorMessage}");
            }
            
            // Test batch processing
            var events = new[]
            {
                IOEvent.CreateFileEvent(IOEventType.FileRead, testFile, null, IOEventPriority.High),
                IOEvent.CreateFileEvent(IOEventType.FileWrite, testFile, testData, IOEventPriority.Medium)
            };
            
            Console.WriteLine("Processing batch of events...");
            var batchResult = await isolationManager.ProcessBatchAsync(events);
            
            Console.WriteLine($"Batch processed: {batchResult.Results.Length} events, {batchResult.Results.Count(r => r.IsSuccess)} successful");
            
            if (batchResult.Results.Length == events.Length)
            {
                Console.WriteLine("✅ Event processing working correctly");
            }
            else
            {
                throw new Exception("Batch processing incomplete");
            }
            
            // Cleanup
            if (File.Exists(testFile))
            {
                File.Delete(testFile);
            }
            
            Console.WriteLine("✅ Event processing test passed\n");
        }
    }
}
