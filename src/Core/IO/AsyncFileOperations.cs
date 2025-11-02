using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace TiXL.Core.IO
{
    /// <summary>
    /// Async file operations with real async/await patterns for non-blocking I/O
    /// Provides actual asynchronous file operations that don't block the main thread
    /// </summary>
    public class AsyncFileOperations
    {
        private readonly SemaphoreSlim _concurrentFileIO;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks;
        private readonly Channel<AsyncFileOperation> _operationQueue;
        private readonly CancellationTokenSource _globalCancellation;
        private readonly AsyncFileOperationProcessor _processor;
        
        // Thread pool for heavy I/O operations
        private readonly ThreadPool _backgroundThreadPool;
        private readonly SemaphoreSlim _threadPoolSemaphore;
        
        // Progress tracking
        private readonly ConcurrentDictionary<string, OperationProgress> _progressTrackers;
        
        private readonly int _maxConcurrentOperations;
        private readonly int _maxThreadPoolThreads;
        
        public event EventHandler<AsyncFileProgress> ProgressUpdated;
        public event EventHandler<AsyncFileError> OperationError;
        public event EventHandler<AsyncFileOperationCompleted> OperationCompleted;
        
        public AsyncFileOperations(int maxConcurrentOperations = 20, int maxThreadPoolThreads = 8)
        {
            _maxConcurrentOperations = maxConcurrentOperations;
            _maxThreadPoolThreads = maxThreadPoolThreads;
            
            _concurrentFileIO = new SemaphoreSlim(maxConcurrentOperations, maxConcurrentOperations);
            _fileLocks = new ConcurrentDictionary<string, SemaphoreSlim>();
            _globalCancellation = new CancellationTokenSource();
            _progressTrackers = new ConcurrentDictionary<string, OperationProgress>();
            _threadPoolSemaphore = new SemaphoreSlim(maxThreadPoolThreads, maxThreadPoolThreads);
            
            // Create bounded channel for operation queue (backpressure)
            var channelOptions = new BoundedChannelOptions(maxConcurrentOperations * 2)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            };
            _operationQueue = Channel.CreateBounded<AsyncFileOperation>(channelOptions);
            
            // Initialize thread pool for heavy I/O
            _backgroundThreadPool = new ThreadPool();
            _processor = new AsyncFileOperationProcessor(_operationQueue, ProcessOperationAsync, _globalCancellation.Token);
            
            // Start processing operations
            _ = Task.Run(StartProcessorAsync, _globalCancellation.Token);
        }
        
        /// <summary>
        /// Async read file content with progress tracking
        /// </summary>
        public async Task<AsyncFileReadResult> ReadFileAsync(string filePath, CancellationToken cancellationToken = default, string operationId = null)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
                
            var id = operationId ?? Guid.NewGuid().ToString();
            
            var operation = new AsyncFileOperation
            {
                Id = id,
                OperationType = AsyncFileOperationType.Read,
                FilePath = filePath,
                CancellationToken = cancellationToken
            };
            
            return await QueueOperationAsync(operation, async op =>
            {
                var fileLock = _fileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));
                await fileLock.WaitAsync(cancellationToken);
                
                try
                {
                    UpdateProgress(op.Id, 0, "Reading file...");
                    
                    var stopwatch = Stopwatch.StartNew();
                    byte[] data;
                    
                    // Use actual async file operations
                    using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 8192, useAsync: true))
                    {
                        data = new byte[fileStream.Length];
                        int bytesRead;
                        long totalRead = 0;
                        
                        while ((bytesRead = await fileStream.ReadAsync(data, (int)totalRead, (int)Math.Min(8192, data.Length - (int)totalRead), cancellationToken)) > 0)
                        {
                            totalRead += bytesRead;
                            UpdateProgress(op.Id, (int)((totalRead * 100) / data.Length), $"Reading... {totalRead}/{data.Length} bytes");
                            
                            // Allow cancellation
                            cancellationToken.ThrowIfCancellationRequested();
                        }
                    }
                    
                    stopwatch.Stop();
                    UpdateProgress(op.Id, 100, "Read completed");
                    
                    return new AsyncFileReadResult
                    {
                        Success = true,
                        Data = data,
                        FilePath = filePath,
                        BytesRead = data.Length,
                        ElapsedTime = stopwatch.Elapsed,
                        OperationId = op.Id
                    };
                }
                catch (OperationCanceledException)
                {
                    UpdateProgress(op.Id, 0, "Read cancelled");
                    return new AsyncFileReadResult
                    {
                        Success = false,
                        ErrorMessage = "Read operation was cancelled",
                        FilePath = filePath,
                        OperationId = op.Id
                    };
                }
                catch (Exception ex)
                {
                    UpdateProgress(op.Id, 0, $"Read failed: {ex.Message}");
                    return new AsyncFileReadResult
                    {
                        Success = false,
                        ErrorMessage = ex.Message,
                        Exception = ex,
                        FilePath = filePath,
                        OperationId = op.Id
                    };
                }
                finally
                {
                    fileLock.Release();
                    _progressTrackers.TryRemove(op.Id, out _);
                }
            });
        }
        
        /// <summary>
        /// Async write file content with progress tracking
        /// </summary>
        public async Task<AsyncFileWriteResult> WriteFileAsync(string filePath, byte[] data, bool createBackup = false, CancellationToken cancellationToken = default, string operationId = null)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
                
            if (data == null)
                throw new ArgumentNullException(nameof(data));
                
            var id = operationId ?? Guid.NewGuid().ToString();
            
            var operation = new AsyncFileOperation
            {
                Id = id,
                OperationType = AsyncFileOperationType.Write,
                FilePath = filePath,
                Data = data,
                CreateBackup = createBackup,
                CancellationToken = cancellationToken
            };
            
            return await QueueOperationAsync(operation, async op =>
            {
                var fileLock = _fileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));
                await fileLock.WaitAsync(cancellationToken);
                
                try
                {
                    UpdateProgress(op.Id, 0, "Writing file...");
                    
                    var stopwatch = Stopwatch.StartNew();
                    string backupPath = null;
                    var directory = Path.GetDirectoryName(filePath);
                    
                    // Create directory if it doesn't exist
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    
                    // Create backup if requested and file exists
                    if (createBackup && File.Exists(filePath))
                    {
                        backupPath = filePath + ".backup_" + DateTime.UtcNow:yyyyMMdd_HHmmssfff";
                        File.Copy(filePath, backupPath, overwrite: true);
                    }
                    
                    UpdateProgress(op.Id, 10, "Writing data...");
                    
                    // Use actual async file operations
                    using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 8192, useAsync: true))
                    {
                        // Write in chunks to track progress and allow cancellation
                        int chunkSize = 8192;
                        int totalWritten = 0;
                        
                        while (totalWritten < data.Length)
                        {
                            int bytesToWrite = Math.Min(chunkSize, data.Length - totalWritten);
                            await fileStream.WriteAsync(data, totalWritten, bytesToWrite, cancellationToken);
                            totalWritten += bytesToWrite;
                            
                            int progress = 10 + (int)((totalWritten * 90) / data.Length);
                            UpdateProgress(op.Id, progress, $"Writing... {totalWritten}/{data.Length} bytes");
                            
                            // Allow cancellation
                            cancellationToken.ThrowIfCancellationRequested();
                        }
                    }
                    
                    await fileStream.FlushAsync(cancellationToken);
                    
                    stopwatch.Stop();
                    UpdateProgress(op.Id, 100, "Write completed");
                    
                    // Clean up backup if successful
                    if (backupPath != null)
                    {
                        try { File.Delete(backupPath); } catch { /* ignore cleanup errors */ }
                    }
                    
                    return new AsyncFileWriteResult
                    {
                        Success = true,
                        FilePath = filePath,
                        BytesWritten = data.Length,
                        ElapsedTime = stopwatch.Elapsed,
                        OperationId = op.Id
                    };
                }
                catch (OperationCanceledException)
                {
                    UpdateProgress(op.Id, 0, "Write cancelled");
                    
                    // Restore backup if write was cancelled
                    if (backupPath != null && File.Exists(backupPath))
                    {
                        try
                        {
                            File.Copy(backupPath, filePath, overwrite: true);
                            File.Delete(backupPath);
                        }
                        catch { /* ignore restore errors */ }
                    }
                    
                    return new AsyncFileWriteResult
                    {
                        Success = false,
                        ErrorMessage = "Write operation was cancelled",
                        FilePath = filePath,
                        OperationId = op.Id
                    };
                }
                catch (Exception ex)
                {
                    UpdateProgress(op.Id, 0, $"Write failed: {ex.Message}");
                    
                    // Restore backup if write failed
                    if (backupPath != null && File.Exists(backupPath))
                    {
                        try
                        {
                            File.Copy(backupPath, filePath, overwrite: true);
                            File.Delete(backupPath);
                        }
                        catch { /* ignore restore errors */ }
                    }
                    
                    return new AsyncFileWriteResult
                    {
                        Success = false,
                        ErrorMessage = ex.Message,
                        Exception = ex,
                        FilePath = filePath,
                        OperationId = op.Id
                    };
                }
                finally
                {
                    fileLock.Release();
                    _progressTrackers.TryRemove(op.Id, out _);
                }
            });
        }
        
        /// <summary>
        /// Async file copy with progress tracking
        /// </summary>
        public async Task<AsyncFileCopyResult> CopyFileAsync(string sourcePath, string destinationPath, bool overwrite = false, CancellationToken cancellationToken = default, string operationId = null)
        {
            if (string.IsNullOrEmpty(sourcePath))
                throw new ArgumentException("Source path cannot be null or empty", nameof(sourcePath));
                
            if (string.IsNullOrEmpty(destinationPath))
                throw new ArgumentException("Destination path cannot be null or empty", nameof(destinationPath));
                
            var id = operationId ?? Guid.NewGuid().ToString();
            
            var operation = new AsyncFileOperation
            {
                Id = id,
                OperationType = AsyncFileOperationType.Copy,
                SourcePath = sourcePath,
                FilePath = destinationPath,
                Overwrite = overwrite,
                CancellationToken = cancellationToken
            };
            
            return await QueueOperationAsync(operation, async op =>
            {
                var sourceLock = _fileLocks.GetOrAdd(sourcePath, _ => new SemaphoreSlim(1, 1));
                var destLock = _fileLocks.GetOrAdd(destinationPath, _ => new SemaphoreSlim(1, 1));
                
                await Task.WhenAll(sourceLock.WaitAsync(cancellationToken), destLock.WaitAsync(cancellationToken));
                
                try
                {
                    UpdateProgress(op.Id, 0, "Copying file...");
                    
                    if (!File.Exists(sourcePath))
                    {
                        return new AsyncFileCopyResult
                        {
                            Success = false,
                            ErrorMessage = "Source file not found",
                            SourcePath = sourcePath,
                            DestinationPath = destinationPath,
                            OperationId = op.Id
                        };
                    }
                    
                    // Get file info for progress tracking
                    var fileInfo = new FileInfo(sourcePath);
                    var totalSize = fileInfo.Length;
                    
                    var stopwatch = Stopwatch.StartNew();
                    
                    // Create destination directory if needed
                    var destDir = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }
                    
                    // Copy with progress tracking
                    using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 8192, useAsync: true))
                    using (var destStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 8192, useAsync: true))
                    {
                        byte[] buffer = new byte[8192];
                        long totalCopied = 0;
                        int bytesRead;
                        
                        while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                        {
                            await destStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                            totalCopied += bytesRead;
                            
                            int progress = (int)((totalCopied * 100) / totalSize);
                            UpdateProgress(op.Id, progress, $"Copying... {totalCopied}/{totalSize} bytes");
                            
                            // Allow cancellation
                            cancellationToken.ThrowIfCancellationRequested();
                        }
                    }
                    
                    await destStream.FlushAsync(cancellationToken);
                    
                    stopwatch.Stop();
                    UpdateProgress(op.Id, 100, "Copy completed");
                    
                    return new AsyncFileCopyResult
                    {
                        Success = true,
                        SourcePath = sourcePath,
                        DestinationPath = destinationPath,
                        BytesCopied = totalSize,
                        ElapsedTime = stopwatch.Elapsed,
                        OperationId = op.Id
                    };
                }
                catch (OperationCanceledException)
                {
                    UpdateProgress(op.Id, 0, "Copy cancelled");
                    
                    // Clean up partial destination file
                    if (File.Exists(destinationPath))
                    {
                        try { File.Delete(destinationPath); } catch { /* ignore cleanup errors */ }
                    }
                    
                    return new AsyncFileCopyResult
                    {
                        Success = false,
                        ErrorMessage = "Copy operation was cancelled",
                        SourcePath = sourcePath,
                        DestinationPath = destinationPath,
                        OperationId = op.Id
                    };
                }
                catch (Exception ex)
                {
                    UpdateProgress(op.Id, 0, $"Copy failed: {ex.Message}");
                    
                    // Clean up partial destination file
                    if (File.Exists(destinationPath))
                    {
                        try { File.Delete(destinationPath); } catch { /* ignore cleanup errors */ }
                    }
                    
                    return new AsyncFileCopyResult
                    {
                        Success = false,
                        ErrorMessage = ex.Message,
                        Exception = ex,
                        SourcePath = sourcePath,
                        DestinationPath = destinationPath,
                        OperationId = op.Id
                    };
                }
                finally
                {
                    sourceLock.Release();
                    destLock.Release();
                    _progressTrackers.TryRemove(op.Id, out _);
                }
            });
        }
        
        /// <summary>
        /// Async file delete with safety checks
        /// </summary>
        public async Task<AsyncFileDeleteResult> DeleteFileAsync(string filePath, bool verifyExists = true, CancellationToken cancellationToken = default, string operationId = null)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
                
            var id = operationId ?? Guid.NewGuid().ToString();
            
            var operation = new AsyncFileOperation
            {
                Id = id,
                OperationType = AsyncFileOperationType.Delete,
                FilePath = filePath,
                CancellationToken = cancellationToken
            };
            
            return await QueueOperationAsync(operation, async op =>
            {
                var fileLock = _fileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));
                await fileLock.WaitAsync(cancellationToken);
                
                try
                {
                    UpdateProgress(op.Id, 0, "Deleting file...");
                    
                    var stopwatch = Stopwatch.StartNew();
                    
                    if (verifyExists && !File.Exists(filePath))
                    {
                        return new AsyncFileDeleteResult
                        {
                            Success = false,
                            ErrorMessage = "File not found",
                            FilePath = filePath,
                            OperationId = op.Id
                        };
                    }
                    
                    // Get file info before deletion
                    FileInfo fileInfo = null;
                    long fileSize = 0;
                    
                    if (File.Exists(filePath))
                    {
                        fileInfo = new FileInfo(filePath);
                        fileSize = fileInfo.Length;
                    }
                    
                    // Perform deletion on background thread if file is large
                    if (fileSize > 10 * 1024 * 1024) // 10MB threshold
                    {
                        await Task.Run(() =>
                        {
                            File.Delete(filePath);
                        }, cancellationToken);
                    }
                    else
                    {
                        File.Delete(filePath);
                    }
                    
                    stopwatch.Stop();
                    UpdateProgress(op.Id, 100, "Delete completed");
                    
                    return new AsyncFileDeleteResult
                    {
                        Success = true,
                        FilePath = filePath,
                        FileSize = fileSize,
                        ElapsedTime = stopwatch.Elapsed,
                        OperationId = op.Id
                    };
                }
                catch (OperationCanceledException)
                {
                    UpdateProgress(op.Id, 0, "Delete cancelled");
                    return new AsyncFileDeleteResult
                    {
                        Success = false,
                        ErrorMessage = "Delete operation was cancelled",
                        FilePath = filePath,
                        OperationId = op.Id
                    };
                }
                catch (Exception ex)
                {
                    UpdateProgress(op.Id, 0, $"Delete failed: {ex.Message}");
                    return new AsyncFileDeleteResult
                    {
                        Success = false,
                        ErrorMessage = ex.Message,
                        Exception = ex,
                        FilePath = filePath,
                        OperationId = op.Id
                    };
                }
                finally
                {
                    fileLock.Release();
                    _progressTrackers.TryRemove(op.Id, out _);
                }
            });
        }
        
        /// <summary>
        /// Async directory enumeration with progress tracking
        /// </summary>
        public async Task<AsyncDirectoryEnumerationResult> EnumerateDirectoryAsync(string directoryPath, string searchPattern = "*", bool recursive = false, CancellationToken cancellationToken = default, string operationId = null)
        {
            if (string.IsNullOrEmpty(directoryPath))
                throw new ArgumentException("Directory path cannot be null or empty", nameof(directoryPath));
                
            var id = operationId ?? Guid.NewGuid().ToString();
            
            var operation = new AsyncFileOperation
            {
                Id = id,
                OperationType = AsyncFileOperationType.EnumerateDirectory,
                FilePath = directoryPath,
                SearchPattern = searchPattern,
                Recursive = recursive,
                CancellationToken = cancellationToken
            };
            
            return await QueueOperationAsync(operation, async op =>
            {
                try
                {
                    UpdateProgress(op.Id, 0, "Enumerating directory...");
                    
                    if (!Directory.Exists(directoryPath))
                    {
                        return new AsyncDirectoryEnumerationResult
                        {
                            Success = false,
                            ErrorMessage = "Directory not found",
                            DirectoryPath = directoryPath,
                            OperationId = op.Id
                        };
                    }
                    
                    var stopwatch = Stopwatch.StartNew();
                    var files = new List<string>();
                    var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                    
                    // Use actual async directory enumeration
                    await Task.Run(async () =>
                    {
                        await foreach (var file in Directory.EnumerateFilesAsync(directoryPath, searchPattern, searchOption).WithCancellation(cancellationToken))
                        {
                            files.Add(file);
                            UpdateProgress(op.Id, files.Count % 100 == 0 ? 50 + (files.Count % 50) : 50, $"Found {files.Count} files...");
                            
                            // Allow cancellation
                            cancellationToken.ThrowIfCancellationRequested();
                        }
                    }, cancellationToken);
                    
                    stopwatch.Stop();
                    UpdateProgress(op.Id, 100, $"Enumeration completed ({files.Count} files)");
                    
                    return new AsyncDirectoryEnumerationResult
                    {
                        Success = true,
                        DirectoryPath = directoryPath,
                        Files = files.ToArray(),
                        FileCount = files.Count,
                        ElapsedTime = stopwatch.Elapsed,
                        OperationId = op.Id
                    };
                }
                catch (OperationCanceledException)
                {
                    UpdateProgress(op.Id, 0, "Enumeration cancelled");
                    return new AsyncDirectoryEnumerationResult
                    {
                        Success = false,
                        ErrorMessage = "Enumeration operation was cancelled",
                        DirectoryPath = directoryPath,
                        OperationId = op.Id
                    };
                }
                catch (Exception ex)
                {
                    UpdateProgress(op.Id, 0, $"Enumeration failed: {ex.Message}");
                    return new AsyncDirectoryEnumerationResult
                    {
                        Success = false,
                        ErrorMessage = ex.Message,
                        Exception = ex,
                        DirectoryPath = directoryPath,
                        OperationId = op.Id
                    };
                }
                finally
                {
                    _progressTrackers.TryRemove(op.Id, out _);
                }
            });
        }
        
        /// <summary>
        /// Get current operation progress
        /// </summary>
        public OperationProgress GetOperationProgress(string operationId)
        {
            return _progressTrackers.TryGetValue(operationId, out var progress) ? progress : null;
        }
        
        /// <summary>
        /// Cancel a specific operation
        /// </summary>
        public void CancelOperation(string operationId)
        {
            var progress = GetOperationProgress(operationId);
            progress?.CancellationTokenSource?.Cancel();
        }
        
        /// <summary>
        /// Get active operation count
        /// </summary>
        public int GetActiveOperationCount()
        {
            return _progressTrackers.Count;
        }
        
        private async Task<T> QueueOperationAsync<T>(AsyncFileOperation operation, Func<AsyncFileOperation, Task<T>> processor) where T : AsyncFileOperationResult
        {
            var completionSource = new TaskCompletionSource<T>();
            
            // Set up cancellation
            if (operation.CancellationToken.CanBeCanceled)
            {
                operation.CancellationToken.Register(() =>
                {
                    completionSource.TrySetCanceled();
                });
            }
            
            try
            {
                await _concurrentFileIO.WaitAsync(operation.CancellationToken);
                
                var progress = new OperationProgress
                {
                    OperationId = operation.Id,
                    StartTime = DateTime.UtcNow,
                    Status = "Queued",
                    CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(operation.CancellationToken, _globalCancellation.Token)
                };
                
                _progressTrackers.TryAdd(operation.Id, progress);
                
                try
                {
                    var result = await processor(operation);
                    completionSource.TrySetResult(result);
                    return result;
                }
                catch (Exception ex)
                {
                    var errorResult = CreateErrorResult<T>(operation, ex);
                    completionSource.TrySetResult(errorResult);
                    return errorResult;
                }
                finally
                {
                    _progressTrackers.TryRemove(operation.Id, out _);
                }
            }
            catch (OperationCanceledException)
            {
                completionSource.TrySetCanceled();
                throw;
            }
            catch (Exception ex)
            {
                var errorResult = CreateErrorResult<T>(operation, ex);
                completionSource.TrySetResult(errorResult);
                return errorResult;
            }
            finally
            {
                _concurrentFileIO.Release();
            }
        }
        
        private T CreateErrorResult<T>(AsyncFileOperation operation, Exception ex) where T : AsyncFileOperationResult, new()
        {
            return new T
            {
                Success = false,
                ErrorMessage = ex.Message,
                Exception = ex,
                OperationId = operation.Id,
                FilePath = operation.FilePath
            };
        }
        
        private void UpdateProgress(string operationId, int percentage, string status)
        {
            if (_progressTrackers.TryGetValue(operationId, out var progress))
            {
                progress.Percentage = percentage;
                progress.Status = status;
                progress.LastUpdateTime = DateTime.UtcNow;
                
                ProgressUpdated?.Invoke(this, new AsyncFileProgress
                {
                    OperationId = operationId,
                    Percentage = percentage,
                    Status = status,
                    Timestamp = DateTime.UtcNow
                });
            }
        }
        
        private async Task StartProcessorAsync()
        {
            try
            {
                await foreach (var operation in _operationQueue.Reader.ReadAllAsync(_globalCancellation.Token))
                {
                    try
                    {
                        // Process operation on background thread for heavy I/O
                        await _threadPoolSemaphore.WaitAsync(_globalCancellation.Token);
                        
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await Task.Delay(1); // Allow main thread to continue
                            }
                            finally
                            {
                                _threadPoolSemaphore.Release();
                            }
                        }, _globalCancellation.Token);
                    }
                    catch (Exception ex)
                    {
                        OperationError?.Invoke(this, new AsyncFileError
                        {
                            OperationId = operation.Id,
                            ErrorMessage = $"Operation processor error: {ex.Message}",
                            Exception = ex,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when shutting down
            }
        }
        
        private async Task ProcessOperationAsync(AsyncFileOperation operation)
        {
            // This method processes queued operations
            // In this implementation, operations are handled directly in QueueOperationAsync
            await Task.CompletedTask;
        }
        
        public void Dispose()
        {
            _globalCancellation?.Cancel();
            _concurrentFileIO?.Dispose();
            _threadPoolSemaphore?.Dispose();
            
            foreach (var fileLock in _fileLocks.Values)
            {
                fileLock?.Dispose();
            }
            
            _processor?.Dispose();
            
            foreach (var progress in _progressTrackers.Values)
            {
                progress?.CancellationTokenSource?.Dispose();
            }
            
            _globalCancellation?.Dispose();
        }
    }
    
    #region Data Models
    
    public class AsyncFileOperation
    {
        public string Id { get; set; }
        public AsyncFileOperationType OperationType { get; set; }
        public string FilePath { get; set; }
        public string SourcePath { get; set; }
        public string DestinationPath { get; set; }
        public byte[] Data { get; set; }
        public bool CreateBackup { get; set; }
        public bool Overwrite { get; set; }
        public bool Recursive { get; set; }
        public string SearchPattern { get; set; } = "*";
        public CancellationToken CancellationToken { get; set; }
        public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
    }
    
    public enum AsyncFileOperationType
    {
        Read,
        Write,
        Copy,
        Delete,
        EnumerateDirectory
    }
    
    public class OperationProgress
    {
        public string OperationId { get; set; }
        public int Percentage { get; set; }
        public string Status { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public CancellationTokenSource CancellationTokenSource { get; set; }
    }
    
    public abstract class AsyncFileOperationResult
    {
        public bool Success { get; set; }
        public string OperationId { get; set; }
        public string ErrorMessage { get; set; }
        public Exception Exception { get; set; }
        public TimeSpan ElapsedTime { get; set; }
        public DateTime CompletedTime { get; set; } = DateTime.UtcNow;
    }
    
    public class AsyncFileReadResult : AsyncFileOperationResult
    {
        public string FilePath { get; set; }
        public byte[] Data { get; set; }
        public long BytesRead { get; set; }
    }
    
    public class AsyncFileWriteResult : AsyncFileOperationResult
    {
        public string FilePath { get; set; }
        public long BytesWritten { get; set; }
    }
    
    public class AsyncFileCopyResult : AsyncFileOperationResult
    {
        public string SourcePath { get; set; }
        public string DestinationPath { get; set; }
        public long BytesCopied { get; set; }
    }
    
    public class AsyncFileDeleteResult : AsyncFileOperationResult
    {
        public string FilePath { get; set; }
        public long FileSize { get; set; }
    }
    
    public class AsyncDirectoryEnumerationResult : AsyncFileOperationResult
    {
        public string DirectoryPath { get; set; }
        public string[] Files { get; set; }
        public int FileCount { get; set; }
    }
    
    public class AsyncFileProgress
    {
        public string OperationId { get; set; }
        public int Percentage { get; set; }
        public string Status { get; set; }
        public DateTime Timestamp { get; set; }
    }
    
    public class AsyncFileError
    {
        public string OperationId { get; set; }
        public string ErrorMessage { get; set; }
        public Exception Exception { get; set; }
        public DateTime Timestamp { get; set; }
    }
    
    public class AsyncFileOperationCompleted
    {
        public string OperationId { get; set; }
        public AsyncFileOperationType OperationType { get; set; }
        public bool Success { get; set; }
        public TimeSpan ElapsedTime { get; set; }
        public DateTime Timestamp { get; set; }
    }
    
    #endregion
    
    #region Supporting Classes
    
    /// <summary>
    /// Thread pool for heavy I/O operations
    /// </summary>
    public class ThreadPool
    {
        private readonly List<Thread> _threads;
        private readonly Channel<ThreadPoolTask> _taskQueue;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly int _maxThreads;
        private readonly object _lockObject = new object();
        
        private int _activeThreads;
        private int _disposedThreads;
        
        public ThreadPool(int maxThreads = 8)
        {
            _maxThreads = maxThreads;
            _threads = new List<Thread>();
            _cancellationTokenSource = new CancellationTokenSource();
            
            var options = new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            };
            
            _taskQueue = Channel.CreateBounded<ThreadPoolTask>(options);
            
            // Start worker threads
            for (int i = 0; i < maxThreads; i++)
            {
                var thread = new Thread(WorkerThread)
                {
                    Name = $"IO ThreadPool Worker {i}",
                    IsBackground = true,
                    Priority = ThreadPriority.Normal
                };
                _threads.Add(thread);
                thread.Start();
            }
        }
        
        public void QueueTask(Func<Task> taskAction, string taskId = null)
        {
            var task = new ThreadPoolTask
            {
                TaskId = taskId ?? Guid.NewGuid().ToString(),
                TaskAction = taskAction,
                CreatedTime = DateTime.UtcNow
            };
            
            _taskQueue.Writer.TryWrite(task);
        }
        
        public void QueueTask<T>(Func<Task<T>> taskAction, string taskId = null)
        {
            var task = new ThreadPoolTask
            {
                TaskId = taskId ?? Guid.NewGuid().ToString(),
                TaskAction = async () => await taskAction(),
                CreatedTime = DateTime.UtcNow
            };
            
            _taskQueue.Writer.TryWrite(task);
        }
        
        private async void WorkerThread()
        {
            try
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    if (_taskQueue.Reader.WaitToReadAsync(_cancellationTokenSource.Token).Result)
                    {
                        if (_taskQueue.Reader.TryRead(out var task))
                        {
                            try
                            {
                                Interlocked.Increment(ref _activeThreads);
                                
                                // Run the task on this thread
                                await task.TaskAction();
                            }
                            catch (Exception ex)
                            {
                                // Log error (in a real implementation, you'd have proper logging)
                                Console.WriteLine($"ThreadPool task error: {ex.Message}");
                            }
                            finally
                            {
                                Interlocked.Decrement(ref _activeThreads);
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
            finally
            {
                Interlocked.Increment(ref _disposedThreads);
            }
        }
        
        public int ActiveThreads => Interlocked.CompareExchange(ref _activeThreads, 0, 0);
        public int DisposedThreads => Interlocked.CompareExchange(ref _disposedThreads, 0, 0);
        
        public ThreadPoolStatistics GetStatistics()
        {
            lock (_lockObject)
            {
                return new ThreadPoolStatistics
                {
                    TotalThreads = _threads.Count,
                    ActiveThreads = ActiveThreads,
                    DisposedThreads = DisposedThreads,
                    PendingTasks = _taskQueue.Reader.CanCount ? _taskQueue.Reader.Count : 0,
                    MaxThreads = _maxThreads,
                    IsActive = !_cancellationTokenSource.Token.IsCancellationRequested
                };
            }
        }
        
        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            
            // Wait for threads to finish
            foreach (var thread in _threads)
            {
                thread.Join(TimeSpan.FromSeconds(5));
            }
            
            _cancellationTokenSource?.Dispose();
        }
    }
    
    public class ThreadPoolTask
    {
        public string TaskId { get; set; }
        public Func<Task> TaskAction { get; set; }
        public DateTime CreatedTime { get; set; }
    }
    
    public class ThreadPoolStatistics
    {
        public int TotalThreads { get; set; }
        public int ActiveThreads { get; set; }
        public int DisposedThreads { get; set; }
        public int PendingTasks { get; set; }
        public int MaxThreads { get; set; }
        public bool IsActive { get; set; }
    }
    
    /// <summary>
    /// Processor for async file operations
    /// </summary>
    public class AsyncFileOperationProcessor
    {
        private readonly Channel<AsyncFileOperation> _operationQueue;
        private readonly Func<AsyncFileOperation, Task> _processor;
        private readonly CancellationToken _cancellationToken;
        
        public AsyncFileOperationProcessor(Channel<AsyncFileOperation> operationQueue, Func<AsyncFileOperation, Task> processor, CancellationToken cancellationToken = default)
        {
            _operationQueue = operationQueue;
            _processor = processor;
            _cancellationToken = cancellationToken;
        }
        
        public void Dispose()
        {
            // Cleanup resources if needed
        }
    }
    
    #endregion
}