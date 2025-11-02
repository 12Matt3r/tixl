using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TiXL.Core.IO;
using TiXL.Core.Performance;

namespace TiXL.Core.IO.Examples
{
    /// <summary>
    /// Comprehensive examples demonstrating I/O thread isolation in the TiXL I/O system
    /// Shows real async/await patterns, thread isolation, progress tracking, and error recovery
    /// </summary>
    public class IOThreadIsolationExamples
    {
        private readonly IOIsolationManager _isolationManager;
        private readonly AsyncFileOperations _asyncFileOps;
        private readonly SafeFileIO _safeFileIO;
        private readonly List<string> _testFiles = new();
        
        public IOThreadIsolationExamples()
        {
            _isolationManager = new IOIsolationManager();
            _asyncFileOps = _isolationManager.AsyncFileOperations;
            _safeFileIO = _isolationManager.SafeFileIO;
            
            // Set up event handlers for monitoring
            _isolationManager.IOAlert += OnIOAlert;
            _asyncFileOps.ProgressUpdated += OnAsyncFileProgress;
            _asyncFileOps.OperationError += OnAsyncFileError;
        }
        
        /// <summary>
        /// Example 1: Basic async file operations with thread isolation
        /// </summary>
        public async Task Example1_BasicAsyncFileOperations()
        {
            Console.WriteLine("\n=== Example 1: Basic Async File Operations ===");
            
            // Create test file
            var testFile = Path.Combine(Path.GetTempPath(), $"TiXL_Test_{Guid.NewGuid():N}.txt");
            var testData = System.Text.Encoding.UTF8.GetBytes("Hello, TiXL I/O Thread Isolation!");
            _testFiles.Add(testFile);
            
            try
            {
                // Write file with async operations and progress tracking
                Console.WriteLine("Writing file with thread isolation...");
                var writeResult = await _asyncFileOps.WriteFileAsync(testFile, testData, 
                    createBackup: true, cancellationToken: CancellationToken.None);
                
                if (writeResult.Success)
                {
                    Console.WriteLine($"✓ File written successfully: {writeResult.BytesWritten} bytes in {writeResult.ElapsedTime.TotalMilliseconds:F2}ms");
                }
                else
                {
                    Console.WriteLine($"✗ File write failed: {writeResult.ErrorMessage}");
                }
                
                // Read file with async operations
                Console.WriteLine("Reading file with thread isolation...");
                var readResult = await _asyncFileOps.ReadFileAsync(testFile);
                
                if (readResult.Success)
                {
                    var content = System.Text.Encoding.UTF8.GetString(readResult.Data);
                    Console.WriteLine($"✓ File read successfully: {readResult.BytesRead} bytes in {readResult.ElapsedTime.TotalMilliseconds:F2}ms");
                    Console.WriteLine($"  Content: {content}");
                }
                else
                {
                    Console.WriteLine($"✗ File read failed: {readResult.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Example 1 failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Example 2: Using IOIsolationManager for event-driven I/O
        /// </summary>
        public async Task Example2_IOIsolationManagerEvents()
        {
            Console.WriteLine("\n=== Example 2: IOIsolationManager Event Processing ===");
            
            var testFile = Path.Combine(Path.GetTempPath(), $"TiXL_Event_{Guid.NewGuid():N}.txt");
            var testData = System.Text.Encoding.UTF8.GetBytes("Event-driven I/O operation!");
            _testFiles.Add(testFile);
            
            try
            {
                // Create file I/O events
                var writeEvent = IOEvent.CreateFileEvent(IOEventType.FileWrite, testFile, testData, IOEventPriority.Medium);
                writeEvent.Metadata["CreateBackup"] = "true";
                writeEvent.Metadata["ProgressCallback"] = "Example2_WriteCallback";
                
                var readEvent = IOEvent.CreateFileEvent(IOEventType.FileRead, testFile, null, IOEventPriority.Medium);
                readEvent.Metadata["VerifyContent"] = "true";
                readEvent.Metadata["ProgressCallback"] = "Example2_ReadCallback";
                
                // Queue events through isolation manager
                Console.WriteLine("Queueing file write event...");
                var writeResult = await _isolationManager.QueueEventAsync(writeEvent);
                Console.WriteLine($"Write event queued: {writeResult.IsSuccess}");
                
                Console.WriteLine("Queueing file read event...");
                var readResult = await _isolationManager.QueueEventAsync(readEvent);
                Console.WriteLine($"Read event queued: {readResult.IsSuccess}");
                
                // Batch process multiple events
                var batchEvents = new List<IOEvent>
                {
                    IOEvent.CreateFileEvent(IOEventType.FileRead, testFile, null, IOEventPriority.High),
                    IOEvent.CreateFileEvent(IOEventType.FileWrite, testFile, System.Text.Encoding.UTF8.GetBytes("Batch operation data"), IOEventPriority.Medium)
                };
                
                Console.WriteLine("Processing batch of events...");
                var batchResult = await _isolationManager.ProcessBatchAsync(batchEvents);
                Console.WriteLine($"Batch processed: {batchResult.Results.Count} events, {batchResult.Results.Count(r => r.IsSuccess)} successful");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Example 2 failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Example 3: Large file operations with progress tracking and cancellation
        /// </summary>
        public async Task Example3_LargeFileOperations()
        {
            Console.WriteLine("\n=== Example 3: Large File Operations with Progress Tracking ===");
            
            var largeFile = Path.Combine(Path.GetTempPath(), $"TiXL_Large_{Guid.NewGuid():N}.dat");
            _testFiles.Add(largeFile);
            
            try
            {
                // Create large test data (10MB)
                var largeData = new byte[10 * 1024 * 1024];
                var random = new Random(42);
                random.NextBytes(largeData);
                
                Console.WriteLine($"Creating {largeData.Length / 1024 / 1024}MB test file...");
                
                using var cts = new CancellationTokenSource();
                
                // Write large file with progress tracking
                var writeTask = _asyncFileOps.WriteFileAsync(largeFile, largeData, 
                    createBackup: false, cancellationToken: cts.Token);
                
                // Monitor progress
                var progressMonitor = Task.Run(async () =>
                {
                    while (!writeTask.IsCompleted)
                    {
                        await Task.Delay(500);
                        var progress = _asyncFileOps.GetOperationProgress(writeTask.Id);
                        if (progress != null)
                        {
                            Console.WriteLine($"  Progress: {progress.Percentage}% - {progress.Status}");
                        }
                    }
                });
                
                var writeResult = await writeTask;
                await progressMonitor;
                
                if (writeResult.Success)
                {
                    Console.WriteLine($"✓ Large file written: {writeResult.BytesWritten} bytes in {writeResult.ElapsedTime.TotalSeconds:F2}s");
                }
                else
                {
                    Console.WriteLine($"✗ Large file write failed: {writeResult.ErrorMessage}");
                }
                
                // Test cancellation with another large file
                var cancelFile = Path.Combine(Path.GetTempPath(), $"TiXL_Cancel_{Guid.NewGuid():N}.dat");
                _testFiles.Add(cancelFile);
                
                using var cancelCts = new CancellationTokenSource();
                
                var cancelTask = _asyncFileOps.WriteFileAsync(cancelFile, largeData, cancellationToken: cancelCts.Token);
                
                // Cancel after 1 second
                cancelCts.CancelAfter(TimeSpan.FromSeconds(1));
                
                try
                {
                    var cancelResult = await cancelTask;
                    Console.WriteLine($"✗ Cancellation test failed - operation completed: {cancelResult.Success}");
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("✓ Cancellation test passed - operation was cancelled");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Example 3 failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Example 4: Thread isolation with dedicated I/O threads
        /// </summary>
        public async Task Example4_ThreadIsolation()
        {
            Console.WriteLine("\n=== Example 4: Thread Isolation with Dedicated I/O Threads ===");
            
            try
            {
                // Execute operations on dedicated I/O thread pool
                var stopwatch = Stopwatch.StartNew();
                
                Console.WriteLine("Executing operation on dedicated I/O thread pool...");
                var result = await _isolationManager.ExecuteOnIOThreadPoolAsync(async () =>
                {
                    // This runs on a dedicated I/O thread
                    var threadId = Thread.CurrentThread.ManagedThreadId;
                    Console.WriteLine($"  Running on I/O thread ID: {threadId}");
                    
                    // Simulate some I/O work
                    await Task.Delay(100);
                    
                    return new { ThreadId = threadId, Message = "I/O thread operation completed" };
                });
                
                stopwatch.Stop();
                Console.WriteLine($"✓ Dedicated thread operation completed in {stopwatch.ElapsedMilliseconds}ms");
                Console.WriteLine($"  Result: {result.Message} on thread {result.ThreadId}");
                
                // Execute multiple operations in parallel on dedicated threads
                Console.WriteLine("Executing multiple operations on dedicated I/O threads...");
                var parallelTasks = Enumerable.Range(0, 5).Select(async i =>
                {
                    return await _isolationManager.ExecuteOnIOThreadPoolAsync(async () =>
                    {
                        var threadId = Thread.CurrentThread.ManagedThreadId;
                        await Task.Delay(50 + i * 10); // Simulate different I/O times
                        return new { Index = i, ThreadId = threadId, Elapsed = 50 + i * 10 };
                    });
                });
                
                var parallelResults = await Task.WhenAll(parallelTasks);
                
                Console.WriteLine("✓ Parallel thread-isolated operations completed:");
                foreach (var r in parallelResults)
                {
                    Console.WriteLine($"  Task {r.Index}: Thread {r.ThreadId}, {r.Elapsed}ms");
                }
                
                // Get thread isolation statistics
                var threadStats = _isolationManager.GetThreadIsolationStatistics();
                Console.WriteLine($"Thread Isolation Stats:");
                Console.WriteLine($"  Total I/O Operations: {threadStats.TotalIOOperations}");
                Console.WriteLine($"  Active I/O Threads: {threadStats.ActiveIOThreadPoolThreads}/{threadStats.MaxIOThreadPoolThreads}");
                Console.WriteLine($"  Thread Pool Utilization: {threadStats.IOThreadPoolUtilization:F1}%");
                Console.WriteLine($"  Render Thread Isolated: {threadStats.RenderThreadIsolated}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Example 4 failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Example 5: Batch file operations with error recovery
        /// </summary>
        public async Task Example5_BatchOperationsWithErrorRecovery()
        {
            Console.WriteLine("\n=== Example 5: Batch Operations with Error Recovery ===");
            
            try
            {
                // Create multiple test files
                var testFiles = new List<string>();
                for (int i = 0; i < 5; i++)
                {
                    var file = Path.Combine(Path.GetTempPath(), $"TiXL_Batch_{i}_{Guid.NewGuid():N}.txt");
                    testFiles.Add(file);
                    _testFiles.Add(file);
                }
                
                // Batch write operations
                var writeTasks = testFiles.Select(async (file, index) =>
                {
                    var data = System.Text.Encoding.UTF8.GetBytes($"Batch file {index} content");
                    var result = await _asyncFileOps.WriteFileAsync(file, data, cancellationToken: CancellationToken.None);
                    return new { File = file, Index = index, Result = result };
                });
                
                Console.WriteLine("Executing batch write operations...");
                var writeResults = await Task.WhenAll(writeTasks);
                Console.WriteLine($"Batch write completed: {writeResults.Count(r => r.Result.Success)}/{writeResults.Length} successful");
                
                // Batch read operations with some invalid files
                var readTasks = testFiles.Concat(new[] { "nonexistent.txt" }).Select(async (file, index) =>
                {
                    var result = await _asyncFileOps.ReadFileAsync(file, cancellationToken: CancellationToken.None);
                    return new { File = file, Index = index, Result = result };
                });
                
                Console.WriteLine("Executing batch read operations (including invalid file)...");
                var readResults = await Task.WhenAll(readTasks);
                Console.WriteLine($"Batch read completed: {readResults.Count(r => r.Result.Success)}/{readResults.Length} successful");
                
                // Show error recovery
                foreach (var failed in readResults.Where(r => !r.Result.Success))
                {
                    Console.WriteLine($"  Recovery needed for {failed.File}: {failed.Result.ErrorMessage}");
                }
                
                // Copy operations
                var copyPairs = testFiles.Take(3).Select((file, i) => new { Source = file, Destination = file + ".copy" });
                foreach (var pair in copyPairs)
                {
                    _testFiles.Add(pair.Destination);
                }
                
                var copyTasks = copyPairs.Select(async pair =>
                {
                    var result = await _asyncFileOps.CopyFileAsync(pair.Source, pair.Destination, 
                        overwrite: true, cancellationToken: CancellationToken.None);
                    return new { pair.Source, pair.Destination, Result = result };
                });
                
                Console.WriteLine("Executing batch copy operations...");
                var copyResults = await Task.WhenAll(copyTasks);
                Console.WriteLine($"Batch copy completed: {copyResults.Count(r => r.Result.Success)}/{copyResults.Length} successful");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Example 5 failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Example 6: Performance monitoring and statistics
        /// </summary>
        public async Task Example6_PerformanceMonitoring()
        {
            Console.WriteLine("\n=== Example 6: Performance Monitoring and Statistics ===");
            
            try
            {
                var perfMonitor = new PerformanceMonitor();
                
                // Perform various I/O operations while monitoring performance
                var operations = new List<(string Name, Func<Task> Operation)>
                {
                    ("Small File Write", async () =>
                    {
                        var data = System.Text.Encoding.UTF8.GetBytes("Small data");
                        await _asyncFileOps.WriteFileAsync(Path.Combine(Path.GetTempPath(), $"TiXL_Small_{Guid.NewGuid():N}.txt"), data);
                    }),
                    
                    ("Medium File Write", async () =>
                    {
                        var data = new byte[1024 * 1024]; // 1MB
                        new Random().NextBytes(data);
                        await _asyncFileOps.WriteFileAsync(Path.Combine(Path.GetTempPath(), $"TiXL_Medium_{Guid.NewGuid():N}.dat"), data);
                    }),
                    
                    ("Thread Isolated Operation", async () =>
                    {
                        await _isolationManager.ExecuteOnIOThreadPoolAsync(async () =>
                        {
                            await Task.Delay(100);
                            return "Thread isolated operation completed";
                        });
                    }),
                    
                    ("Event Queue Processing", async () =>
                    {
                        var event1 = IOEvent.CreateFileEvent(IOEventType.FileWrite, 
                            Path.Combine(Path.GetTempPath(), $"TiXL_Event_{Guid.NewGuid():N}.txt"), 
                            System.Text.Encoding.UTF8.GetBytes("Event data"));
                        await _isolationManager.QueueEventAsync(event1);
                    })
                };
                
                // Run operations with performance tracking
                foreach (var (name, operation) in operations)
                {
                    using var perfTracker = perfMonitor.ProfileOperation(name);
                    await operation();
                }
                
                // Get comprehensive statistics
                Console.WriteLine("Performance Statistics:");
                Console.WriteLine($"  I/O Isolation Manager: {_isolationManager.GetStatistics().ToString()}");
                
                var ioStats = _isolationManager.GetThreadIsolationStatistics();
                Console.WriteLine($"  Thread Isolation: {ioStats.TotalIOOperations} operations, {ioStats.ActiveIOThreadPoolThreads} active threads");
                
                Console.WriteLine("  Top Operations by Time:");
                var topOperations = perfMonitor.GetTopOperations(3);
                foreach (var op in topOperations)
                {
                    Console.WriteLine($"    {op.Name}: {op.TotalTimeMs:F2}ms total, {op.AverageTimeMs:F2}ms average, {op.CallCount} calls");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Example 6 failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Run all examples
        /// </summary>
        public async Task RunAllExamples()
        {
            Console.WriteLine("Starting TiXL I/O Thread Isolation Examples");
            Console.WriteLine("===========================================");
            
            try
            {
                await Example1_BasicAsyncFileOperations();
                await Example2_IOIsolationManagerEvents();
                await Example3_LargeFileOperations();
                await Example4_ThreadIsolation();
                await Example5_BatchOperationsWithErrorRecovery();
                await Example6_PerformanceMonitoring();
                
                Console.WriteLine("\n=== All Examples Completed ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Examples failed: {ex.Message}");
            }
            finally
            {
                // Cleanup test files
                await CleanupTestFiles();
            }
        }
        
        private async Task CleanupTestFiles()
        {
            Console.WriteLine("\nCleaning up test files...");
            
            foreach (var file in _testFiles)
            {
                try
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
            
            _testFiles.Clear();
            Console.WriteLine("Cleanup completed");
        }
        
        // Event handlers
        private void OnIOAlert(object sender, IOIsolationAlert alert)
        {
            if (alert.Type == AlertType.FileOperationProgress || 
                alert.Type == AlertType.AsyncFileOperationCompleted)
            {
                Console.WriteLine($"  [Alert] {alert.Type}: {alert.Message}");
            }
        }
        
        private void OnAsyncFileProgress(object sender, AsyncFileProgress progress)
        {
            if (progress.Percentage % 25 == 0 && progress.Percentage > 0) // Print every 25%
            {
                Console.WriteLine($"  [Progress] {progress.Percentage}%: {progress.Status}");
            }
        }
        
        private void OnAsyncFileError(object sender, AsyncFileError error)
        {
            Console.WriteLine($"  [Error] {error.ErrorMessage}");
        }
        
        public void Dispose()
        {
            _isolationManager?.Dispose();
        }
    }
}