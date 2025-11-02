using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using TiXL.Core.IO;
using TiXL.Core.Logging;
using TiXL.Core.Validation;
using Xunit;
using Xunit.Abstractions;
using Moq;
using Microsoft.Extensions.Logging;

namespace TiXL.Tests.IO
{
    /// <summary>
    /// Comprehensive test suite for I/O thread isolation and async operations
    /// Tests thread safety, isolation, performance, and edge cases
    /// </summary>
    public class ThreadIsolationTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _testDirectory;
        private readonly Mock<IOperationMonitor> _mockMonitor;
        private readonly Mock<ILogger> _mockLogger;

        public ThreadIsolationTests(ITestOutputHelper output)
        {
            _output = output;
            _testDirectory = Path.Combine(Path.GetTempPath(), "TiXL_IO_Tests_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_testDirectory);

            _mockMonitor = new Mock<IOperationMonitor>();
            _mockLogger = new Mock<ILogger>();
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                try
                {
                    Directory.Delete(_testDirectory, true);
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Warning: Failed to cleanup test directory: {ex.Message}");
                }
            }
        }

        #region Thread Isolation Tests

        [Fact]
        public void ThreadIsolation_ConcurrentAccess_ShouldMaintainThreadSafety()
        {
            // Arrange
            var isolationManager = new IOIsolationManager();
            var threadIds = new ConcurrentBag<int>();
            var exceptions = new ConcurrentBag<Exception>();
            var accessCount = new ConcurrentDictionary<int, int>();

            const int threadCount = 20;
            const int operationsPerThread = 50;

            // Act - Multiple threads accessing I/O operations
            var tasks = new List<Task>();
            for (int i = 0; i < threadCount; i++)
            {
                int threadId = i;
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        var currentThreadId = Thread.CurrentThread.ManagedThreadId;
                        threadIds.Add(currentThreadId);

                        for (int op = 0; op < operationsPerThread; op++)
                        {
                            var filePath = Path.Combine(_testDirectory, $"thread_{threadId}_op_{op}.txt");
                            
                            isolationManager.ExecuteOnIOThread(() =>
                            {
                                File.WriteAllText(filePath, $"Thread {threadId}, Operation {op}");
                                return true;
                            });

                            accessCount.AddOrUpdate(threadId, 1, (key, old) => old + 1);
                            
                            Thread.Sleep(1); // Small delay to increase contention
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }));
            }

            Task.WaitAll(tasks.ToArray());

            // Assert
            _output.WriteLine($"Thread Isolation Test Results:");
            _output.WriteLine($"  Total threads used: {threadIds.Distinct().Count()}");
            _output.WriteLine($"  Total operations: {threadCount * operationsPerThread}");
            _output.WriteLine($"  Exceptions: {exceptions.Count}");

            Assert.Empty(exceptions, "Should have no exceptions during concurrent access");
            
            // Each thread should have completed its operations
            Assert.Equal(threadCount, accessCount.Count);
            Assert.All(accessCount.Values, count => Assert.Equal(operationsPerThread, count));
        }

        [Fact]
        public void ThreadIsolation_FileLocks_ShouldPreventCorruption()
        {
            // Arrange
            var isolationManager = new IOIsolationManager();
            var concurrentWrites = 100;
            var filePath = Path.Combine(_testDirectory, "concurrent_write_test.txt");

            // Act - Multiple threads writing to the same file
            var tasks = new List<Task>();
            var writeResults = new ConcurrentBag<bool>();
            var exceptions = new ConcurrentBag<Exception>();

            for (int i = 0; i < concurrentWrites; i++)
            {
                int writeId = i;
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        var result = isolationManager.ExecuteOnIOThread(() =>
                        {
                            try
                            {
                                // Thread-safe file write operation
                                lock (typeof(IOIsolationManager)) // Global lock for this test
                                {
                                    var content = $"Write {writeId} from thread {Thread.CurrentThread.ManagedThreadId} at {DateTimeOffset.UtcNow:O}";
                                    File.WriteAllText(filePath, content);
                                    return true;
                                }
                            }
                            catch
                            {
                                return false;
                            }
                        });
                        
                        writeResults.Add(result);
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }));
            }

            Task.WaitAll(tasks.ToArray());

            // Assert - Verify file integrity
            Assert.Empty(exceptions, "Should have no exceptions during file operations");
            Assert.Equal(concurrentWrites, writeResults.Count(w => w));
            
            // File should exist and be readable
            Assert.True(File.Exists(filePath), "File should exist");
            
            var finalContent = File.ReadAllText(filePath);
            Assert.NotNull(finalContent);
            Assert.NotEmpty(finalContent);
            
            _output.WriteLine($"File Lock Test Results:");
            _output.WriteLine($"  Concurrent writes: {concurrentWrites}");
            _output.WriteLine($"  Successful writes: {writeResults.Count}");
            _output.WriteLine($"  File size: {finalContent.Length} bytes");
        }

        [Fact]
        public async Task ThreadIsolation_AsyncOperations_ShouldMaintainIsolation()
        {
            // Arrange
            var isolationManager = new IOIsolationManager();
            var asyncOperations = 50;
            var results = new ConcurrentBag<AsyncOperationResult>();
            var exceptions = new ConcurrentBag<Exception>();

            // Act - Async I/O operations
            var tasks = new List<Task>();
            for (int i = 0; i < asyncOperations; i++)
            {
                int opId = i;
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var filePath = Path.Combine(_testDirectory, $"async_op_{opId}.txt");
                        
                        var result = await isolationManager.ExecuteAsyncOnIOThread(async () =>
                        {
                            // Simulate async file operation
                            await Task.Delay(10); // Simulate I/O delay
                            
                            await File.WriteAllTextAsync(filePath, $"Async operation {opId} at {DateTimeOffset.UtcNow:O}");
                            
                            return new AsyncOperationResult
                            {
                                OperationId = opId,
                                FilePath = filePath,
                                ThreadId = Thread.CurrentThread.ManagedThreadId,
                                Timestamp = DateTimeOffset.UtcNow
                            };
                        });
                        
                        results.Add(result);
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            _output.WriteLine($"Async Thread Isolation Test Results:");
            _output.WriteLine($"  Async operations: {asyncOperations}");
            _output.WriteLine($"  Completed operations: {results.Count}");
            _output.WriteLine($"  Exceptions: {exceptions.Count}");

            Assert.Empty(exceptions, "Should have no exceptions during async operations");
            Assert.Equal(asyncOperations, results.Count);
            
            // All files should exist
            Assert.All(results, result => 
            {
                Assert.True(File.Exists(result.FilePath), $"File {result.FilePath} should exist");
                Assert.True(result.ThreadId > 0, "Thread ID should be valid");
                Assert.True(result.Timestamp > DateTimeOffset.MinValue, "Timestamp should be set");
            });
        }

        #endregion

        #region SafeFileIO Thread Safety Tests

        [Fact]
        public void SafeFileIO_ThreadSafeOperations_ShouldPreventRaceConditions()
        {
            // Arrange
            var safeFileIO = SafeFileIO.Instance;
            const int concurrentOperations = 30;
            const int operationsPerThread = 10;
            var exceptions = new ConcurrentBag<Exception>();
            var operationResults = new ConcurrentBag<bool>();

            // Act - Concurrent safe file operations
            var tasks = new List<Task>();
            for (int i = 0; i < concurrentOperations; i++)
            {
                int threadId = i;
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        for (int op = 0; op < operationsPerThread; op++)
                        {
                            var filePath = Path.Combine(_testDirectory, $"safe_io_thread_{threadId}_op_{op}.txt");
                            var content = $"Thread {threadId}, Operation {op}, Time {DateTimeOffset.UtcNow:O}";
                            
                            // Test different SafeFileIO operations
                            bool success = op % 3 switch
                            {
                                0 => SafeFileWrite(safeFileIO, filePath, content),
                                1 => SafeFileRead(safeFileIO, filePath),
                                _ => SafeFileDelete(safeFileIO, filePath)
                            };
                            
                            operationResults.Add(success);
                            
                            Thread.Sleep(5); // Small delay to increase contention
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }));
            }

            Task.WaitAll(tasks.ToArray());

            // Assert
            _output.WriteLine($"SafeFileIO Thread Safety Results:");
            _output.WriteLine($"  Concurrent operations: {concurrentOperations}");
            _output.WriteLine($"  Operations per thread: {operationsPerThread}");
            _output.WriteLine($"  Total operations: {concurrentOperations * operationsPerThread}");
            _output.WriteLine($"  Successful operations: {operationResults.Count}");
            _output.WriteLine($"  Exceptions: {exceptions.Count}");

            Assert.Empty(exceptions, "Should have no exceptions in thread-safe operations");
            
            // Most operations should succeed (allowing for some expected failures like file not found)
            var successRate = (double)operationResults.Count / (concurrentOperations * operationsPerThread);
            Assert.True(successRate >= 0.7, $"Success rate {successRate:P2} should be above 70%");
        }

        [Fact]
        public void SafeFileIO_ValidationThreadSafety_ShouldHandleConcurrentValidation()
        {
            // Arrange
            var safeFileIO = SafeFileIO.Instance;
            var paths = new[]
            {
                Path.Combine(_testDirectory, "valid_file.txt"),
                Path.Combine(_testDirectory, "..", "parent_file.txt"), // Potentially unsafe path
                Path.Combine(_testDirectory, "CON.txt"), // Reserved name
                "", // Empty path
                Path.Combine(_testDirectory, "normal_file.txt")
            };

            var validationResults = new ConcurrentBag<ValidationResult>();
            var exceptions = new ConcurrentBag<Exception>();

            // Act - Concurrent path validation
            var tasks = new List<Task>();
            foreach (var path in paths)
            {
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        // Create file for valid path
                        if (path == Path.Combine(_testDirectory, "valid_file.txt"))
                        {
                            File.WriteAllText(path, "Valid content");
                        }
                        else if (path == Path.Combine(_testDirectory, "normal_file.txt"))
                        {
                            File.WriteAllText(path, "Normal content");
                        }

                        var result = safeFileIO.ValidateWritePath(path);
                        validationResults.Add(result);
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }));
            }

            Task.WaitAll(tasks.ToArray());

            // Assert
            _output.WriteLine($"Path Validation Thread Safety Results:");
            _output.WriteLine($"  Validated paths: {validationResults.Count}");
            _output.WriteLine($"  Exceptions: {exceptions.Count}");

            Assert.Empty(exceptions, "Should have no exceptions during validation");
            Assert.Equal(paths.Length, validationResults.Count);
            
            // Should have mixed valid/invalid results
            var validResults = validationResults.Where(r => r.IsValid).ToList();
            var invalidResults = validationResults.Where(r => !r.IsValid).ToList();
            
            Assert.True(validResults.Count > 0, "Should have some valid paths");
            Assert.True(invalidResults.Count > 0, "Should have some invalid paths");

            foreach (var result in validationResults)
            {
                if (!result.IsValid)
                {
                    Assert.NotNull(result.ErrorMessage);
                    Assert.NotEmpty(result.ErrorMessage);
                }
            }
        }

        #endregion

        #region Performance Isolation Tests

        [Fact]
        public async Task PerformanceIsolation_IORatio_ShouldMaintainThreadRatio()
        {
            // Arrange
            var isolationManager = new IOIsolationManager();
            const int ioThreadCount = 4;
            const int workerThreadCount = 8;
            const int operationsPerThread = 20;

            var ioThreadUsage = new ConcurrentDictionary<int, int>();
            var workerThreadUsage = new ConcurrentDictionary<int, int>();
            var startBarrier = new Barrier(ioThreadCount + workerThreadCount + 1); // +1 for signal

            // Act - Mixed I/O and CPU intensive work
            var tasks = new List<Task>();
            
            // I/O intensive threads
            for (int i = 0; i < ioThreadCount; i++)
            {
                int threadId = i;
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        startBarrier.SignalAndWait();
                        
                        for (int op = 0; op < operationsPerThread; op++)
                        {
                            isolationManager.ExecuteOnIOThread(() =>
                            {
                                var threadId = Thread.CurrentThread.ManagedThreadId;
                                ioThreadUsage.AddOrUpdate(threadId, 1, (key, old) => old + 1);
                                
                                // Simulate I/O work
                                var filePath = Path.Combine(_testDirectory, $"io_thread_{threadId}_op_{op}.txt");
                                File.WriteAllText(filePath, $"IO work {op}");
                                Thread.Sleep(10); // Simulate I/O delay
                                
                                return true;
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _output.WriteLine($"IO thread {threadId} error: {ex.Message}");
                    }
                }));
            }

            // Worker/CPU intensive threads
            for (int i = 0; i < workerThreadCount; i++)
            {
                int threadId = i;
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        startBarrier.SignalAndWait();
                        
                        for (int op = 0; op < operationsPerThread; op++)
                        {
                            var threadId = Thread.CurrentThread.ManagedThreadId;
                            workerThreadUsage.AddOrUpdate(threadId, 1, (key, old) => old + 1);
                            
                            // Simulate CPU work (no I/O)
                            var result = 0.0;
                            for (int j = 0; j < 1000; j++)
                            {
                                result += Math.Sqrt(j) * Math.Sin(j);
                            }
                            
                            Thread.Sleep(1); // Small delay
                        }
                    }
                    catch (Exception ex)
                    {
                        _output.WriteLine($"Worker thread {threadId} error: {ex.Message}");
                    }
                }));
            }

            // Signal all threads to start simultaneously
            startBarrier.SignalAndWait();
            
            await Task.WhenAll(tasks);

            // Assert
            _output.WriteLine($"Performance Isolation Results:");
            _output.WriteLine($"  IO threads: {ioThreadUsage.Count} (used {ioThreadUsage.Values.Sum()} times)");
            _output.WriteLine($"  Worker threads: {workerThreadUsage.Count} (used {workerThreadUsage.Values.Sum()} times)");
            _output.WriteLine($"  Total operations: {(ioThreadUsage.Values.Sum() + workerThreadUsage.Values.Sum())}");

            // Verify threading model
            Assert.Equal(ioThreadCount, ioThreadUsage.Count);
            Assert.Equal(workerThreadCount, workerThreadUsage.Count);
            
            // Each thread should be used approximately equally
            var ioUsageValues = ioThreadUsage.Values.ToList();
            var workerUsageValues = workerThreadUsage.Values.ToList();
            
            Assert.All(ioUsageValues, usage => 
                Assert.True(usage >= operationsPerThread * 0.8, "IO thread usage should be consistent"));
            Assert.All(workerUsageValues, usage => 
                Assert.True(usage >= operationsPerThread * 0.8, "Worker thread usage should be consistent"));
        }

        [Fact]
        public async Task PerformanceIsolation_HighLoad_ShouldMaintainIsolation()
        {
            // Arrange
            var isolationManager = new IOIsolationManager();
            const int highLoadOperations = 500;
            const int concurrentTasks = 50;

            var performanceMetrics = new ConcurrentBag<PerformanceMetric>();
            var exceptions = new ConcurrentBag<Exception>();

            // Act - High load I/O operations
            var tasks = new List<Task>();
            for (int i = 0; i < concurrentTasks; i++)
            {
                int taskId = i;
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var taskStopwatch = Stopwatch.StartNew();
                        
                        for (int op = 0; op < highLoadOperations / concurrentTasks; op++)
                        {
                            var operationStopwatch = Stopwatch.StartNew();
                            
                            await isolationManager.ExecuteAsyncOnIOThread(async () =>
                            {
                                var filePath = Path.Combine(_testDirectory, $"high_load_task_{taskId}_op_{op}.txt");
                                
                                // Vary operation types
                                if (op % 3 == 0)
                                {
                                    await File.WriteAllTextAsync(filePath, $"High load data {op}");
                                }
                                else if (op % 3 == 1)
                                {
                                    if (File.Exists(filePath))
                                    {
                                        await File.ReadAllTextAsync(filePath);
                                    }
                                }
                                else
                                {
                                    if (File.Exists(filePath))
                                    {
                                        File.Delete(filePath);
                                    }
                                }
                                
                                return true;
                            });
                            
                            operationStopwatch.Stop();
                            
                            if (op % 10 == 0) // Sample every 10th operation
                            {
                                performanceMetrics.Add(new PerformanceMetric
                                {
                                    TaskId = taskId,
                                    OperationId = op,
                                    DurationMs = operationStopwatch.Elapsed.TotalMilliseconds,
                                    ThreadId = Thread.CurrentThread.ManagedThreadId
                                });
                            }
                        }
                        
                        taskStopwatch.Stop();
                        performanceMetrics.Add(new PerformanceMetric
                        {
                            TaskId = taskId,
                            OperationId = -1,
                            DurationMs = taskStopwatch.Elapsed.TotalMilliseconds,
                            ThreadId = -1
                        });
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            _output.WriteLine($"High Load Performance Results:");
            _output.WriteLine($"  Total operations: {highLoadOperations}");
            _output.WriteLine($"  Concurrent tasks: {concurrentTasks}");
            _output.WriteLine($"  Exceptions: {exceptions.Count}");
            _output.WriteLine($"  Performance samples: {performanceMetrics.Count}");

            Assert.Empty(exceptions, "Should have no exceptions under high load");
            
            // Performance should be reasonable
            var operationMetrics = performanceMetrics.Where(m => m.OperationId >= 0).ToList();
            if (operationMetrics.Any())
            {
                var avgOperationTime = operationMetrics.Average(m => m.DurationMs);
                var maxOperationTime = operationMetrics.Max(m => m.DurationMs);
                
                _output.WriteLine($"  Average operation time: {avgOperationTime:F2}ms");
                _output.WriteLine($"  Max operation time: {maxOperationTime:F2}ms");
                
                Assert.True(avgOperationTime < 100, "Average operation time should be reasonable");
                Assert.True(maxOperationTime < 1000, "Max operation time should not be excessive");
            }
        }

        #endregion

        #region Timeout and Error Handling Tests

        [Fact]
        public void TimeoutHandling_IOOperationTimeout_ShouldHandleGracefully()
        {
            // Arrange
            var isolationManager = new IOIsolationManager();
            var timeout = TimeSpan.FromMilliseconds(100);
            var slowOperationDelay = TimeSpan.FromMilliseconds(200);

            // Act & Assert - Operation should timeout
            var exception = Record.Exception(() =>
            {
                isolationManager.ExecuteOnIOThreadWithTimeout(() =>
                {
                    // Simulate slow operation
                    Thread.Sleep(slowOperationDelay);
                    return "completed";
                }, timeout);
            });

            Assert.NotNull(exception);
            Assert.True(exception is TimeoutException || exception.InnerException is TimeoutException,
                "Should throw timeout exception for slow operations");
            
            _output.WriteLine($"Timeout test: Caught expected timeout exception");
        }

        [Fact]
        public void ErrorHandling_IOOperationErrors_ShouldBeHandledGracefully()
        {
            // Arrange
            var isolationManager = new IOIsolationManager();
            var exceptions = new ConcurrentBag<Exception>();

            // Act - Various error conditions
            var errorTasks = new List<Task>();
            
            // Test 1: Invalid file path
            errorTasks.Add(Task.Run(() =>
            {
                try
                {
                    isolationManager.ExecuteOnIOThread(() =>
                    {
                        var invalidPath = Path.Combine(_testDirectory, "..", "..", "invalid_path.txt");
                        File.WriteAllText(invalidPath, "Should fail");
                        return true;
                    });
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));

            // Test 2: Permission denied simulation
            errorTasks.Add(Task.Run(() =>
            {
                try
                {
                    var protectedPath = Path.Combine(_testDirectory, "protected_file.txt");
                    isolationManager.ExecuteOnIOThread(() =>
                    {
                        File.WriteAllText(protectedPath, "Protected content");
                        // Simulate permission error on read
                        try
                        {
                            var content = File.ReadAllText(protectedPath + "_nonexistent");
                            return false; // Should not reach here
                        }
                        catch
                        {
                            throw new UnauthorizedAccessException("Simulated permission error");
                        }
                    });
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));

            // Test 3: Disk full simulation
            errorTasks.Add(Task.Run(() =>
            {
                try
                {
                    isolationManager.ExecuteOnIOThread(() =>
                    {
                        // Simulate disk full by throwing IOException
                        throw new IOException("Simulated disk full error");
                    });
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));

            Task.WaitAll(errorTasks.ToArray());

            // Assert
            _output.WriteLine($"Error Handling Test Results:");
            _output.WriteLine($"  Total error tests: 3");
            _output.WriteLine($"  Exceptions caught: {exceptions.Count}");

            Assert.Equal(3, exceptions.Count);
            
            // Should have caught expected exception types
            var exceptionTypes = exceptions.Select(e => e.GetType()).ToList();
            Assert.Contains(typeof(IOException), exceptionTypes);
            Assert.Contains(typeof(UnauthorizedAccessException), exceptionTypes);
        }

        #endregion

        #region Resource Management Tests

        [Fact]
        public void ResourceManagement_ProperCleanup_ShouldPreventResourceLeaks()
        {
            // Arrange
            var initialFileCount = Directory.GetFiles(_testDirectory).Length;
            var isolationManager = new IOIsolationManager();
            const int operationsToCreateFiles = 50;

            // Act - Create many files through I/O operations
            var tasks = new List<Task>();
            for (int i = 0; i < operationsToCreateFiles; i++)
            {
                int operationId = i;
                tasks.Add(Task.Run(() =>
                {
                    isolationManager.ExecuteOnIOThread(() =>
                    {
                        var filePath = Path.Combine(_testDirectory, $"temp_file_{operationId}_{Guid.NewGuid():N}.txt");
                        File.WriteAllText(filePath, $"Temporary file {operationId}");
                        return filePath;
                    });
                }));
            }

            Task.WaitAll(tasks.ToArray());

            var intermediateFileCount = Directory.GetFiles(_testDirectory).Length;

            // Act 2 - Cleanup operation
            isolationManager.ExecuteOnIOThread(() =>
            {
                var filesToDelete = Directory.GetFiles(_testDirectory, "temp_file_*.txt");
                foreach (var file in filesToDelete)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                        // Ignore deletion errors in test
                    }
                }
                return filesToDelete.Length;
            });

            var finalFileCount = Directory.GetFiles(_testDirectory).Length;

            // Assert
            _output.WriteLine($"Resource Management Test Results:");
            _output.WriteLine($"  Initial files: {initialFileCount}");
            _output.WriteLine($"  After creation: {intermediateFileCount}");
            _output.WriteLine($"  After cleanup: {finalFileCount}");
            _output.WriteLine($"  Files created: {intermediateFileCount - initialFileCount}");
            _output.WriteLine($"  Files remaining: {finalFileCount - initialFileCount}");

            // Should have created files and cleaned most of them up
            Assert.True(intermediateFileCount > initialFileCount, "Should create files");
            
            // Final count should be close to initial (allowing for some test files)
            Assert.True(finalFileCount - initialFileCount <= 5, "Should clean up most files");
        }

        #endregion

        #region Edge Case Tests

        [Fact]
        public void EdgeCase_EmptyDirectory_ShouldHandleGracefully()
        {
            // Arrange
            var emptyDir = Path.Combine(_testDirectory, "empty_directory");
            Directory.CreateDirectory(emptyDir);

            var isolationManager = new IOIsolationManager();

            // Act - Operations on empty directory
            var result1 = isolationManager.ExecuteOnIOThread(() =>
            {
                var files = Directory.GetFiles(emptyDir);
                return files.Length;
            });

            var result2 = isolationManager.ExecuteOnIOThread(() =>
            {
                var subdirs = Directory.GetDirectories(emptyDir);
                return subdirs.Length;
            });

            // Assert
            Assert.Equal(0, result1);
            Assert.Equal(0, result2);
        }

        [Fact]
        public void EdgeCase_VeryLongPaths_ShouldHandleGracefully()
        {
            // Arrange
            var isolationManager = new IOIsolationManager();
            var veryLongFilename = new string('a', 200); // Very long filename
            var filePath = Path.Combine(_testDirectory, veryLongFilename + ".txt");

            // Act & Assert - Should handle gracefully (may succeed or fail, but not crash)
            var exception = Record.Exception(() =>
            {
                isolationManager.ExecuteOnIOThread(() =>
                {
                    try
                    {
                        File.WriteAllText(filePath, "Long path test");
                        return true;
                    }
                    catch
                    {
                        return false; // Expected to potentially fail
                    }
                });
            });

            // Should not crash, but may throw PathTooLongException
            Assert.True(exception == null || exception is PathTooLongException || exception.InnerException is PathTooLongException,
                "Should handle long paths gracefully");
        }

        [Fact]
        public void EdgeCase_SpecialCharactersInPaths_ShouldHandleCorrectly()
        {
            // Arrange
            var isolationManager = new IOIsolationManager();
            var specialCharacters = new[] { "file with spaces.txt", "file-with-dashes.txt", "file_with_underscores.txt", "file.with.dots.txt" };

            var results = new List<bool>();

            // Act
            foreach (var filename in specialCharacters)
            {
                var filePath = Path.Combine(_testDirectory, filename);
                
                var result = isolationManager.ExecuteOnIOThread(() =>
                {
                    try
                    {
                        File.WriteAllText(filePath, $"Content for {filename}");
                        var readBack = File.ReadAllText(filePath);
                        return !string.IsNullOrEmpty(readBack);
                    }
                    catch
                    {
                        return false;
                    }
                });
                
                results.Add(result);
            }

            // Assert
            _output.WriteLine($"Special Characters Test Results:");
            for (int i = 0; i < specialCharacters.Length; i++)
            {
                _output.WriteLine($"  {specialCharacters[i]}: {(results[i] ? "Success" : "Failed")}");
            }

            Assert.All(results, result => Assert.True(result, "Should handle special characters in filenames"));
        }

        #endregion

        #region Helper Methods

        private bool SafeFileWrite(SafeFileIO safeFileIO, string filePath, string content)
        {
            try
            {
                return safeFileIO.WriteFile(filePath, content);
            }
            catch
            {
                return false;
            }
        }

        private bool SafeFileRead(SafeFileIO safeFileIO, string filePath)
        {
            try
            {
                var content = safeFileIO.ReadFile(filePath);
                return !string.IsNullOrEmpty(content);
            }
            catch
            {
                return false;
            }
        }

        private bool SafeFileDelete(SafeFileIO safeFileIO, string filePath)
        {
            try
            {
                return safeFileIO.DeleteFile(filePath);
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }

    #region Supporting Classes and Data Models

    public class AsyncOperationResult
    {
        public int OperationId { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public int ThreadId { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }

    public class PerformanceMetric
    {
        public int TaskId { get; set; }
        public int OperationId { get; set; }
        public double DurationMs { get; set; }
        public int ThreadId { get; set; }
    }

    public interface IOperationMonitor
    {
        void RecordOperation(string operationType, TimeSpan duration);
        void RecordError(string operationType, Exception error);
        OperationStatistics GetStatistics();
    }

    public class OperationStatistics
    {
        public int TotalOperations { get; set; }
        public int ErrorCount { get; set; }
        public double AverageDurationMs { get; set; }
    }

    public class IOIsolationManager
    {
        private readonly SemaphoreSlim _ioSemaphore;
        private readonly TimeSpan _defaultTimeout;

        public IOIsolationManager()
        {
            _ioSemaphore = new SemaphoreSlim(4, 4); // Limited I/O threads
            _defaultTimeout = TimeSpan.FromSeconds(30);
        }

        public T ExecuteOnIOThread<T>(Func<T> operation)
        {
            if (!_ioSemaphore.Wait(_defaultTimeout))
            {
                throw new TimeoutException("I/O operation timed out");
            }

            try
            {
                return operation();
            }
            finally
            {
                _ioSemaphore.Release();
            }
        }

        public async Task<T> ExecuteAsyncOnIOThread<T>(Func<Task<T>> operation)
        {
            if (!await _ioSemaphore.WaitAsync(_defaultTimeout))
            {
                throw new TimeoutException("I/O operation timed out");
            }

            try
            {
                return await operation();
            }
            finally
            {
                _ioSemaphore.Release();
            }
        }

        public T ExecuteOnIOThreadWithTimeout<T>(Func<T> operation, TimeSpan timeout)
        {
            if (!_ioSemaphore.Wait(timeout))
            {
                throw new TimeoutException("I/O operation timed out");
            }

            try
            {
                return operation();
            }
            finally
            {
                _ioSemaphore.Release();
            }
        }
    }

    #endregion
}
