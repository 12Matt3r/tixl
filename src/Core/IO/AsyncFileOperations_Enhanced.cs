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
using Microsoft.Extensions.Logging;
using TiXL.Core.ErrorHandling;

namespace TiXL.Core.IO
{
    /// <summary>
    /// Enhanced async file operations with comprehensive error handling, retry patterns, and timeout handling
    /// Provides resilient asynchronous file operations with proper exception propagation and graceful degradation
    /// </summary>
    public class AsyncFileOperationsEnhanced
    {
        private readonly SemaphoreSlim _concurrentFileIO;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks;
        private readonly Channel<AsyncFileOperation> _operationQueue;
        private readonly CancellationTokenSource _globalCancellation;
        private readonly AsyncFileOperationProcessor _processor;
        private readonly ILogger<AsyncFileOperationsEnhanced> _logger;
        
        // Thread pool for heavy I/O operations
        private readonly ThreadPoolEnhanced _backgroundThreadPool;
        private readonly SemaphoreSlim _threadPoolSemaphore;
        
        // Progress tracking
        private readonly ConcurrentDictionary<string, OperationProgress> _progressTrackers;
        
        private readonly int _maxConcurrentOperations;
        private readonly int _maxThreadPoolThreads;
        
        public event EventHandler<AsyncFileProgress> ProgressUpdated;
        public event EventHandler<AsyncFileError> OperationError;
        public event EventHandler<AsyncFileOperationCompleted> OperationCompleted;
        
        public AsyncFileOperationsEnhanced(int maxConcurrentOperations = 20, int maxThreadPoolThreads = 8, ILogger<AsyncFileOperationsEnhanced> logger = null)
        {
            _maxConcurrentOperations = maxConcurrentOperations;
            _maxThreadPoolThreads = maxThreadPoolThreads;
            _logger = logger;
            
            try
            {
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
                
                // Initialize enhanced thread pool for heavy I/O
                _backgroundThreadPool = new ThreadPoolEnhanced(maxThreadPoolThreads, _logger);
                _processor = new AsyncFileOperationProcessor(_operationQueue, ProcessOperationAsync, _globalCancellation.Token);
                
                // Start processing operations
                _ = Task.Run(StartProcessorAsync, _globalCancellation.Token);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize AsyncFileOperationsEnhanced");
                throw new IOSafetyException(
                    IOSafetyErrorCode.InitializationFailed,
                    $"Failed to initialize async file operations: {ex.Message}",
                    ex);
            }
        }
        
        /// <summary>
        /// Enhanced async read file content with progress tracking, retry patterns, and timeout handling
        /// </summary>
        public async Task<AsyncFileReadResult> ReadFileAsync(string filePath, CancellationToken cancellationToken = default, string operationId = null, TimeSpan? timeout = null)
        {
            return await ExecuteResilientAsync(async () =>
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    throw new IOSafetyException(
                        IOSafetyErrorCode.InvalidPath,
                        "File path cannot be null or empty");
                }
                
                ValidationHelpers.ValidateFilePath(filePath, allowCreate: false);
                
                var id = operationId ?? Guid.NewGuid().ToString();
                var operationTimeout = timeout ?? TimeSpan.FromMinutes(5);
                
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
                    
                    return await ExecuteWithTimeoutAsync(async token =>
                    {
                        await fileLock.WaitAsync(token);
                        
                        try
                        {
                            UpdateProgress(op.Id, 0, "Reading file...");
                            
                            var stopwatch = Stopwatch.StartNew();
                            byte[] data;
                            
                            // Use actual async file operations with retry
                            data = await ReadFileWithRetryAsync(filePath, op, token);
                            
                            stopwatch.Stop();
                            UpdateProgress(op.Id, 100, "Read completed");
                            
                            // Fire completion event
                            OperationCompleted?.Invoke(this, new AsyncFileOperationCompleted
                            {
                                OperationId = op.Id,
                                OperationType = AsyncFileOperationType.Read,
                                Success = true,
                                ElapsedTime = stopwatch.Elapsed,
                                Timestamp = DateTime.UtcNow
                            });
                            
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
                            
                            // Fire completion event
                            OperationCompleted?.Invoke(this, new AsyncFileOperationCompleted
                            {
                                OperationId = op.Id,
                                OperationType = AsyncFileOperationType.Read,
                                Success = false,
                                ElapsedTime = TimeSpan.Zero,
                                Timestamp = DateTime.UtcNow
                            });
                            
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
                            _logger?.LogError(ex, "Read operation failed for file: {FilePath}", filePath);
                            UpdateProgress(op.Id, 0, $"Read failed: {ex.Message}");
                            
                            // Fire error event
                            OperationError?.Invoke(this, new AsyncFileError
                            {
                                OperationId = op.Id,
                                ErrorMessage = ex.Message,
                                Exception = ex,
                                Timestamp = DateTime.UtcNow
                            });
                            
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
                    }, 
                    operationTimeout, 
                    cancellationToken, 
                    "ReadFile operation");
                });
            }, 
            maxRetries: 3,
            retryPolicy: RetryPolicyType.ExponentialBackoff,
            cancellationToken: cancellationToken,
            operationName: "ReadFile");
        }
        
        /// <summary>
        /// Enhanced async write file content with progress tracking, backup/restore, and comprehensive error handling
        /// </summary>
        public async Task<AsyncFileWriteResult> WriteFileAsync(string filePath, byte[] data, bool createBackup = false, CancellationToken cancellationToken = default, string operationId = null, TimeSpan? timeout = null)
        {
            return await ExecuteResilientAsync(async () =>
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    throw new IOSafetyException(
                        IOSafetyErrorCode.InvalidPath,
                        "File path cannot be null or empty");
                }
                
                ValidationHelpers.ValidateFilePath(filePath, allowCreate: true);
                ValidationHelpers.ThrowIfNull(data, nameof(data));
                ValidationHelpers.ValidateNonNegative(data.Length, nameof(data));
                
                var id = operationId ?? Guid.NewGuid().ToString();
                var operationTimeout = timeout ?? TimeSpan.FromMinutes(5);
                
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
                    
                    return await ExecuteWithTimeoutAsync(async token =>
                    {
                        await fileLock.WaitAsync(token);
                        
                        string backupPath = null;
                        try
                        {
                            UpdateProgress(op.Id, 0, "Writing file...");
                            
                            var stopwatch = Stopwatch.StartNew();
                            var directory = Path.GetDirectoryName(filePath);
                            
                            // Create directory if it doesn't exist
                            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                            {
                                await SafeCreateDirectoryAsync(directory, token);
                            }
                            
                            // Create backup if requested and file exists
                            if (createBackup && File.Exists(filePath))
                            {
                                backupPath = filePath + ".backup_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff");
                                backupPath = await CreateBackupWithRetryAsync(filePath, backupPath, token);
                            }
                            
                            UpdateProgress(op.Id, 10, "Writing data...");
                            
                            // Write with comprehensive error handling
                            await WriteFileWithRetryAsync(filePath, data, op, token);
                            
                            stopwatch.Stop();
                            UpdateProgress(op.Id, 100, "Write completed");
                            
                            // Clean up backup if successful
                            if (!string.IsNullOrEmpty(backupPath))
                            {
                                await CleanupBackupAsync(backupPath);
                            }
                            
                            // Fire completion event
                            OperationCompleted?.Invoke(this, new AsyncFileOperationCompleted
                            {
                                OperationId = op.Id,
                                OperationType = AsyncFileOperationType.Write,
                                Success = true,
                                ElapsedTime = stopwatch.Elapsed,
                                Timestamp = DateTime.UtcNow
                            });
                            
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
                            if (!string.IsNullOrEmpty(backupPath) && File.Exists(backupPath))
                            {
                                await RestoreBackupAsync(backupPath, filePath);
                            }
                            
                            // Fire completion event
                            OperationCompleted?.Invoke(this, new AsyncFileOperationCompleted
                            {
                                OperationId = op.Id,
                                OperationType = AsyncFileOperationType.Write,
                                Success = false,
                                ElapsedTime = TimeSpan.Zero,
                                Timestamp = DateTime.UtcNow
                            });
                            
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
                            _logger?.LogError(ex, "Write operation failed for file: {FilePath}", filePath);
                            UpdateProgress(op.Id, 0, $"Write failed: {ex.Message}");
                            
                            // Restore backup if write failed
                            if (!string.IsNullOrEmpty(backupPath) && File.Exists(backupPath))
                            {
                                await RestoreBackupAsync(backupPath, filePath);
                            }
                            
                            // Fire error event
                            OperationError?.Invoke(this, new AsyncFileError
                            {
                                OperationId = op.Id,
                                ErrorMessage = ex.Message,
                                Exception = ex,
                                Timestamp = DateTime.UtcNow
                            });
                            
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
                    }, 
                    operationTimeout, 
                    cancellationToken, 
                    "WriteFile operation");
                });
            }, 
            maxRetries: 3,
            retryPolicy: RetryPolicyType.ExponentialBackoff,
            cancellationToken: cancellationToken,
            operationName: "WriteFile");
        }
        
        /// <summary>
        /// Enhanced async file copy with progress tracking and comprehensive error handling
        /// </summary>
        public async Task<AsyncFileCopyResult> CopyFileAsync(string sourcePath, string destinationPath, bool overwrite = false, CancellationToken cancellationToken = default, string operationId = null, TimeSpan? timeout = null)
        {
            return await ExecuteResilientAsync(async () =>
            {
                ValidationHelpers.ValidateFilePath(sourcePath, allowCreate: false);
                ValidationHelpers.ValidateFilePath(destinationPath, allowCreate: true);
                
                if (string.IsNullOrEmpty(sourcePath))
                {
                    throw new IOSafetyException(
                        IOSafetyErrorCode.InvalidPath,
                        "Source path cannot be null or empty");
                }
                
                if (string.IsNullOrEmpty(destinationPath))
                {
                    throw new IOSafetyException(
                        IOSafetyErrorCode.InvalidPath,
                        "Destination path cannot be null or empty");
                }
                
                var id = operationId ?? Guid.NewGuid().ToString();
                var operationTimeout = timeout ?? TimeSpan.FromMinutes(10); // Longer timeout for copy operations
                
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
                    
                    return await ExecuteWithTimeoutAsync(async token =>
                    {
                        await Task.WhenAll(sourceLock.WaitAsync(token), destLock.WaitAsync(token));
                        
                        try
                        {
                            UpdateProgress(op.Id, 0, "Copying file...");
                            
                            if (!File.Exists(sourcePath))
                            {
                                throw new IOSafetyException(
                                    IOSafetyErrorCode.FileNotFound,
                                    $"Source file not found: {sourcePath}");
                            }
                            
                            // Get file info for progress tracking
                            var fileInfo = new FileInfo(sourcePath);
                            var totalSize = fileInfo.Length;
                            
                            var stopwatch = Stopwatch.StartNew();
                            
                            // Create destination directory if needed
                            var destDir = Path.GetDirectoryName(destinationPath);
                            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                            {
                                await SafeCreateDirectoryAsync(destDir, token);
                            }
                            
                            // Copy with progress tracking and retry
                            await CopyFileWithRetryAsync(sourcePath, destinationPath, totalSize, op, token);
                            
                            stopwatch.Stop();
                            UpdateProgress(op.Id, 100, "Copy completed");
                            
                            // Fire completion event
                            OperationCompleted?.Invoke(this, new AsyncFileOperationCompleted
                            {
                                OperationId = op.Id,
                                OperationType = AsyncFileOperationType.Copy,
                                Success = true,
                                ElapsedTime = stopwatch.Elapsed,
                                Timestamp = DateTime.UtcNow
                            });
                            
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
                            
                            // Fire completion event
                            OperationCompleted?.Invoke(this, new AsyncFileOperationCompleted
                            {
                                OperationId = op.Id,
                                OperationType = AsyncFileOperationType.Copy,
                                Success = false,
                                ElapsedTime = TimeSpan.Zero,
                                Timestamp = DateTime.UtcNow
                            });
                            
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
                            _logger?.LogError(ex, "Copy operation failed from {SourcePath} to {DestinationPath}", sourcePath, destinationPath);
                            UpdateProgress(op.Id, 0, $"Copy failed: {ex.Message}");
                            
                            // Clean up partial destination file
                            if (File.Exists(destinationPath))
                            {
                                try { File.Delete(destinationPath); } catch { /* ignore cleanup errors */ }
                            }
                            
                            // Fire error event
                            OperationError?.Invoke(this, new AsyncFileError
                            {
                                OperationId = op.Id,
                                ErrorMessage = ex.Message,
                                Exception = ex,
                                Timestamp = DateTime.UtcNow
                            });
                            
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
                    }, 
                    operationTimeout, 
                    cancellationToken, 
                    "CopyFile operation");
                });
            }, 
            maxRetries: 2,
            retryPolicy: RetryPolicyType.ExponentialBackoff,
            cancellationToken: cancellationToken,
            operationName: "CopyFile");
        }
        
        /// <summary>
        /// Enhanced async file delete with safety checks and comprehensive error handling
        /// </summary>
        public async Task<AsyncFileDeleteResult> DeleteFileAsync(string filePath, bool verifyExists = true, CancellationToken cancellationToken = default, string operationId = null, TimeSpan? timeout = null)
        {
            return await ExecuteResilientAsync(async () =>
            {
                ValidationHelpers.ValidateFilePath(filePath, allowCreate: false);
                
                if (string.IsNullOrEmpty(filePath))
                {
                    throw new IOSafetyException(
                        IOSafetyErrorCode.InvalidPath,
                        "File path cannot be null or empty");
                }
                
                var id = operationId ?? Guid.NewGuid().ToString();
                var operationTimeout = timeout ?? TimeSpan.FromMinutes(2);
                
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
                    
                    return await ExecuteWithTimeoutAsync(async token =>
                    {
                        await fileLock.WaitAsync(token);
                        
                        try
                        {
                            UpdateProgress(op.Id, 0, "Deleting file...");
                            
                            var stopwatch = Stopwatch.StartNew();
                            
                            if (verifyExists && !File.Exists(filePath))
                            {
                                throw new IOSafetyException(
                                    IOSafetyErrorCode.FileNotFound,
                                    $"File not found: {filePath}");
                            }
                            
                            // Get file info before deletion
                            FileInfo fileInfo = null;
                            long fileSize = 0;
                            
                            if (File.Exists(filePath))
                            {
                                fileInfo = new FileInfo(filePath);
                                fileSize = fileInfo.Length;
                            }
                            
                            // Perform deletion with retry
                            await DeleteFileWithRetryAsync(filePath, token);
                            
                            stopwatch.Stop();
                            UpdateProgress(op.Id, 100, "Delete completed");
                            
                            // Fire completion event
                            OperationCompleted?.Invoke(this, new AsyncFileOperationCompleted
                            {
                                OperationId = op.Id,
                                OperationType = AsyncFileOperationType.Delete,
                                Success = true,
                                ElapsedTime = stopwatch.Elapsed,
                                Timestamp = DateTime.UtcNow
                            });
                            
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
                            
                            // Fire completion event
                            OperationCompleted?.Invoke(this, new AsyncFileOperationCompleted
                            {
                                OperationId = op.Id,
                                OperationType = AsyncFileOperationType.Delete,
                                Success = false,
                                ElapsedTime = TimeSpan.Zero,
                                Timestamp = DateTime.UtcNow
                            });
                            
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
                            _logger?.LogError(ex, "Delete operation failed for file: {FilePath}", filePath);
                            UpdateProgress(op.Id, 0, $"Delete failed: {ex.Message}");
                            
                            // Fire error event
                            OperationError?.Invoke(this, new AsyncFileError
                            {
                                OperationId = op.Id,
                                ErrorMessage = ex.Message,
                                Exception = ex,
                                Timestamp = DateTime.UtcNow
                            });
                            
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
                    }, 
                    operationTimeout, 
                    cancellationToken, 
                    "DeleteFile operation");
                });
            }, 
            maxRetries: 3,
            retryPolicy: RetryPolicyType.ExponentialBackoff,
            cancellationToken: cancellationToken,
            operationName: "DeleteFile");
        }
        
        /// <summary>
        /// Enhanced async directory enumeration with progress tracking and comprehensive error handling
        /// </summary>
        public async Task<AsyncDirectoryEnumerationResult> EnumerateDirectoryAsync(string directoryPath, string searchPattern = "*", bool recursive = false, CancellationToken cancellationToken = default, string operationId = null, TimeSpan? timeout = null)
        {
            return await ExecuteResilientAsync(async () =>
            {
                ValidationHelpers.ValidateDirectoryPath(directoryPath, allowCreate: false);
                ValidationHelpers.ValidateNonNull(searchPattern, nameof(searchPattern));
                
                if (string.IsNullOrEmpty(directoryPath))
                {
                    throw new IOSafetyException(
                        IOSafetyErrorCode.InvalidPath,
                        "Directory path cannot be null or empty");
                }
                
                var id = operationId ?? Guid.NewGuid().ToString();
                var operationTimeout = timeout ?? TimeSpan.FromMinutes(3);
                
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
                    return await ExecuteWithTimeoutAsync(async token =>
                    {
                        try
                        {
                            UpdateProgress(op.Id, 0, "Enumerating directory...");
                            
                            if (!Directory.Exists(directoryPath))
                            {
                                throw new IOSafetyException(
                                    IOSafetyErrorCode.DirectoryNotFound,
                                    $"Directory not found: {directoryPath}");
                            }
                            
                            var stopwatch = Stopwatch.StartNew();
                            var files = new List<string>();
                            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                            
                            // Enumerate with retry and progress tracking
                            files = await EnumerateFilesWithRetryAsync(directoryPath, searchPattern, searchOption, op, token);
                            
                            stopwatch.Stop();
                            UpdateProgress(op.Id, 100, $"Enumeration completed ({files.Count} files)");
                            
                            // Fire completion event
                            OperationCompleted?.Invoke(this, new AsyncFileOperationCompleted
                            {
                                OperationId = op.Id,
                                OperationType = AsyncFileOperationType.EnumerateDirectory,
                                Success = true,
                                ElapsedTime = stopwatch.Elapsed,
                                Timestamp = DateTime.UtcNow
                            });
                            
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
                            
                            // Fire completion event
                            OperationCompleted?.Invoke(this, new AsyncFileOperationCompleted
                            {
                                OperationId = op.Id,
                                OperationType = AsyncFileOperationType.EnumerateDirectory,
                                Success = false,
                                ElapsedTime = TimeSpan.Zero,
                                Timestamp = DateTime.UtcNow
                            });
                            
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
                            _logger?.LogError(ex, "Enumeration operation failed for directory: {DirectoryPath}", directoryPath);
                            UpdateProgress(op.Id, 0, $"Enumeration failed: {ex.Message}");
                            
                            // Fire error event
                            OperationError?.Invoke(this, new AsyncFileError
                            {
                                OperationId = op.Id,
                                ErrorMessage = ex.Message,
                                Exception = ex,
                                Timestamp = DateTime.UtcNow
                            });
                            
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
                    }, 
                    operationTimeout, 
                    cancellationToken, 
                    "EnumerateDirectory operation");
                });
            }, 
            maxRetries: 2,
            retryPolicy: RetryPolicyType.ExponentialBackoff,
            cancellationToken: cancellationToken,
            operationName: "EnumerateDirectory");
        }
        
        /// <summary>
        /// Get current operation progress with error handling
        /// </summary>
        public OperationProgress GetOperationProgress(string operationId)
        {
            try
            {
                ValidationHelpers.ValidateNonNull(operationId, nameof(operationId));
                return _progressTrackers.TryGetValue(operationId, out var progress) ? progress : null;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to get operation progress for: {OperationId}", operationId);
                return null;
            }
        }
        
        /// <summary>
        /// Cancel a specific operation with error handling
        /// </summary>
        public void CancelOperation(string operationId)
        {
            try
            {
                ValidationHelpers.ValidateNonNull(operationId, nameof(operationId));
                var progress = GetOperationProgress(operationId);
                progress?.CancellationTokenSource?.Cancel();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to cancel operation: {OperationId}", operationId);
            }
        }
        
        /// <summary>
        /// Get active operation count with error handling
        /// </summary>
        public int GetActiveOperationCount()
        {
            try
            {
                return _progressTrackers.Count;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get active operation count");
                return 0;
            }
        }
        
        /// <summary>
        /// Get enhanced thread pool statistics with error handling
        /// </summary>
        public ThreadPoolStatisticsEnhanced GetThreadPoolStatistics()
        {
            try
            {
                return _backgroundThreadPool?.GetStatistics() ?? new ThreadPoolStatisticsEnhanced();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get thread pool statistics");
                return new ThreadPoolStatisticsEnhanced();
            }
        }
        
        #region Private Methods with Comprehensive Error Handling
        
        private async Task<T> QueueOperationAsync<T>(AsyncFileOperation operation, Func<AsyncFileOperation, Task<T>> processor) where T : AsyncFileOperationResult
        {
            var completionSource = new TaskCompletionSource<T>();
            
            try
            {
                // Set up cancellation
                if (operation.CancellationToken.CanBeCanceled)
                {
                    operation.CancellationToken.Register(() =>
                    {
                        completionSource.TrySetCanceled();
                    });
                }
                
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
                    _logger?.LogError(ex, "Operation processing failed: {OperationId}", operation.Id);
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
                _logger?.LogError(ex, "Queue operation failed: {OperationId}", operation.Id);
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
            try
            {
                if (_progressTrackers.TryGetValue(operationId, out var progress))
                {
                    progress.Percentage = Math.Clamp(percentage, 0, 100);
                    progress.Status = status;
                    progress.LastUpdateTime = DateTime.UtcNow;
                    
                    ProgressUpdated?.Invoke(this, new AsyncFileProgress
                    {
                        OperationId = operationId,
                        Percentage = progress.Percentage,
                        Status = status,
                        Timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to update progress for operation: {OperationId}", operationId);
            }
        }
        
        private async Task<byte[]> ReadFileWithRetryAsync(string filePath, AsyncFileOperation op, CancellationToken cancellationToken)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 8192, useAsync: true);
                var data = new byte[fileStream.Length];
                int bytesRead;
                long totalRead = 0;
                
                while ((bytesRead = await fileStream.ReadAsync(data, (int)totalRead, (int)Math.Min(8192, data.Length - (int)totalRead), cancellationToken)) > 0)
                {
                    totalRead += bytesRead;
                    UpdateProgress(op.Id, (int)((totalRead * 100) / data.Length), $"Reading... {totalRead}/{data.Length} bytes");
                    
                    cancellationToken.ThrowIfCancellationRequested();
                }
                
                return data;
            }, 
            maxRetries: 3,
            retryPolicy: RetryPolicyType.ExponentialBackoff,
            cancellationToken: cancellationToken,
            operationName: "ReadFileWithRetry");
        }
        
        private async Task WriteFileWithRetryAsync(string filePath, byte[] data, AsyncFileOperation op, CancellationToken cancellationToken)
        {
            await ExecuteWithRetryAsync(async () =>
            {
                await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 8192, useAsync: true);
                
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
                    
                    cancellationToken.ThrowIfCancellationRequested();
                }
                
                await fileStream.FlushAsync(cancellationToken);
            }, 
            maxRetries: 3,
            retryPolicy: RetryPolicyType.ExponentialBackoff,
            cancellationToken: cancellationToken,
            operationName: "WriteFileWithRetry");
        }
        
        private async Task<string> CreateBackupWithRetryAsync(string sourcePath, string backupPath, CancellationToken cancellationToken)
        {
            return await ExecuteResilientAsync(async () =>
            {
                try
                {
                    await File.CopyAsync(sourcePath, backupPath, overwrite: false, cancellationToken);
                    return backupPath;
                }
                catch (IOException)
                {
                    // Backup already exists, try different name
                    backupPath = Path.Combine(Path.GetDirectoryName(backupPath), Guid.NewGuid().ToString("N") + ".backup");
                    await File.CopyAsync(sourcePath, backupPath, overwrite: false, cancellationToken);
                    return backupPath;
                }
            }, 
            maxRetries: 2,
            retryPolicy: RetryPolicyType.Linear,
            cancellationToken: cancellationToken,
            operationName: "CreateBackupWithRetry");
        }
        
        private async Task CleanupBackupAsync(string backupPath)
        {
            try
            {
                await Task.Run(() => File.Delete(backupPath));
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to cleanup backup file: {BackupPath}", backupPath);
                // Don't throw - backup cleanup failure shouldn't fail the operation
            }
        }
        
        private async Task RestoreBackupAsync(string backupPath, string targetPath)
        {
            try
            {
                await ExecuteWithRetryAsync(async () =>
                {
                    await File.CopyAsync(backupPath, targetPath, overwrite: true);
                    File.Delete(backupPath);
                }, 
                maxRetries: 2,
                retryPolicy: RetryPolicyType.Linear,
                cancellationToken: default,
                operationName: "RestoreBackup");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to restore backup from {BackupPath} to {TargetPath}", backupPath, targetPath);
            }
        }
        
        private async Task CopyFileWithRetryAsync(string sourcePath, string destinationPath, long totalSize, AsyncFileOperation op, CancellationToken cancellationToken)
        {
            await ExecuteWithRetryAsync(async () =>
            {
                await using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 8192, useAsync: true);
                await using var destStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 8192, useAsync: true);
                
                byte[] buffer = new byte[8192];
                long totalCopied = 0;
                int bytesRead;
                
                while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    await destStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    totalCopied += bytesRead;
                    
                    int progress = (int)((totalCopied * 100) / totalSize);
                    UpdateProgress(op.Id, progress, $"Copying... {totalCopied}/{totalSize} bytes");
                    
                    cancellationToken.ThrowIfCancellationRequested();
                }
                
                await destStream.FlushAsync(cancellationToken);
            }, 
            maxRetries: 2,
            retryPolicy: RetryPolicyType.ExponentialBackoff,
            cancellationToken: cancellationToken,
            operationName: "CopyFileWithRetry");
        }
        
        private async Task DeleteFileWithRetryAsync(string filePath, CancellationToken cancellationToken)
        {
            await ExecuteWithRetryAsync(async () =>
            {
                // Perform deletion on background thread if file is large
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > 10 * 1024 * 1024) // 10MB threshold
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
            }, 
            maxRetries: 3,
            retryPolicy: RetryPolicyType.ExponentialBackoff,
            cancellationToken: cancellationToken,
            operationName: "DeleteFileWithRetry");
        }
        
        private async Task<List<string>> EnumerateFilesWithRetryAsync(string directoryPath, string searchPattern, SearchOption searchOption, AsyncFileOperation op, CancellationToken cancellationToken)
        {
            return await ExecuteResilientAsync(async () =>
            {
                return await Task.Run(async () =>
                {
                    var files = new List<string>();
                    
                    await foreach (var file in Directory.EnumerateFilesAsync(directoryPath, searchPattern, searchOption).WithCancellation(cancellationToken))
                    {
                        files.Add(file);
                        UpdateProgress(op.Id, files.Count % 100 == 0 ? 50 + (files.Count % 50) : 50, $"Found {files.Count} files...");
                        
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    
                    return files;
                }, cancellationToken);
            }, 
            maxRetries: 2,
            retryPolicy: RetryPolicyType.ExponentialBackoff,
            cancellationToken: cancellationToken,
            operationName: "EnumerateFilesWithRetry");
        }
        
        private async Task SafeCreateDirectoryAsync(string directoryPath, CancellationToken cancellationToken)
        {
            await ExecuteWithRetryAsync(async () =>
            {
                Directory.CreateDirectory(directoryPath);
            }, 
            maxRetries: 3,
            retryPolicy: RetryPolicyType.ExponentialBackoff,
            cancellationToken: cancellationToken,
            operationName: "SafeCreateDirectory");
        }
        
        #endregion
        
        #region Resilient Execution Helpers
        
        private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, int maxRetries, RetryPolicyType retryPolicy, CancellationToken cancellationToken, string operationName)
        {
            return await ErrorHandlingUtilities.ExecuteWithRetryAsync(operation, maxRetries, retryPolicy, cancellationToken, operationName);
        }
        
        private async Task ExecuteWithRetryAsync(Func<Task> operation, int maxRetries, RetryPolicyType retryPolicy, CancellationToken cancellationToken, string operationName)
        {
            await ErrorHandlingUtilities.ExecuteWithRetryAsync(operation, maxRetries, retryPolicy, cancellationToken, operationName);
        }
        
        private async Task<T> ExecuteWithTimeoutAsync<T>(Func<CancellationToken, Task<T>> operation, TimeSpan timeout, CancellationToken cancellationToken, string operationName)
        {
            return await ErrorHandlingUtilities.ExecuteWithTimeoutAsync(operation, timeout, cancellationToken, operationName);
        }
        
        private async Task ExecuteWithTimeoutAsync(Func<CancellationToken, Task> operation, TimeSpan timeout, CancellationToken cancellationToken, string operationName)
        {
            await ErrorHandlingUtilities.ExecuteWithTimeoutAsync(operation, timeout, cancellationToken, operationName);
        }
        
        private async Task<T> ExecuteResilientAsync<T>(Func<Task<T>> operation, int maxRetries, RetryPolicyType retryPolicy, CancellationToken cancellationToken, string operationName)
        {
            return await ErrorHandlingUtilities.ExecuteResilientAsync(operation, maxRetries, retryPolicy, cancellationToken, operationName);
        }
        
        #endregion
        
        #region Processing Methods
        
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
                        
                        _backgroundThreadPool.QueueTask(async () =>
                        {
                            try
                            {
                                await Task.Delay(1); // Allow main thread to continue
                            }
                            finally
                            {
                                _threadPoolSemaphore.Release();
                            }
                        }, $"Processor_{operation.Id}");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Operation processor error: {OperationId}", operation.Id);
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
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Processor loop error");
            }
        }
        
        private async Task ProcessOperationAsync(AsyncFileOperation operation)
        {
            // This method processes queued operations
            // In this implementation, operations are handled directly in QueueOperationAsync
            await Task.CompletedTask;
        }
        
        #endregion
        
        public void Dispose()
        {
            try
            {
                _globalCancellation?.Cancel();
                _concurrentFileIO?.Dispose();
                _threadPoolSemaphore?.Dispose();
                
                foreach (var fileLock in _fileLocks.Values)
                {
                    fileLock?.Dispose();
                }
                
                _processor?.Dispose();
                _backgroundThreadPool?.Dispose();
                
                foreach (var progress in _progressTrackers.Values)
                {
                    progress?.CancellationTokenSource?.Dispose();
                }
                
                _globalCancellation?.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during AsyncFileOperations disposal");
            }
        }
    }
    
    #region Enhanced Thread Pool
    
    /// <summary>
    /// Enhanced thread pool for heavy I/O operations with comprehensive error handling
    /// </summary>
    public class ThreadPoolEnhanced
    {
        private readonly List<Thread> _threads;
        private readonly Channel<ThreadPoolTask> _taskQueue;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly int _maxThreads;
        private readonly object _lockObject = new object();
        private readonly ILogger _logger;
        
        private int _activeThreads;
        private int _disposedThreads;
        
        public ThreadPoolEnhanced(int maxThreads = 8, ILogger logger = null)
        {
            _maxThreads = maxThreads;
            _logger = logger;
            
            try
            {
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
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize ThreadPoolEnhanced");
                throw new IOSafetyException(
                    IOSafetyErrorCode.InitializationFailed,
                    $"Failed to initialize thread pool: {ex.Message}",
                    ex);
            }
        }
        
        public void QueueTask(Func<Task> taskAction, string taskId = null)
        {
            try
            {
                var task = new ThreadPoolTask
                {
                    TaskId = taskId ?? Guid.NewGuid().ToString(),
                    TaskAction = taskAction,
                    CreatedTime = DateTime.UtcNow
                };
                
                _taskQueue.Writer.TryWrite(task);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to queue task: {TaskId}", taskId);
            }
        }
        
        public void QueueTask<T>(Func<Task<T>> taskAction, string taskId = null)
        {
            try
            {
                var task = new ThreadPoolTask
                {
                    TaskId = taskId ?? Guid.NewGuid().ToString(),
                    TaskAction = async () => await taskAction(),
                    CreatedTime = DateTime.UtcNow
                };
                
                _taskQueue.Writer.TryWrite(task);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to queue typed task: {TaskId}", taskId);
            }
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
                                _logger?.LogError(ex, "ThreadPool task error: {TaskId}", task.TaskId);
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
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Worker thread error");
            }
            finally
            {
                Interlocked.Increment(ref _disposedThreads);
            }
        }
        
        public int ActiveThreads => Interlocked.CompareExchange(ref _activeThreads, 0, 0);
        public int DisposedThreads => Interlocked.CompareExchange(ref _disposedThreads, 0, 0);
        
        public ThreadPoolStatisticsEnhanced GetStatistics()
        {
            try
            {
                lock (_lockObject)
                {
                    return new ThreadPoolStatisticsEnhanced
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
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get thread pool statistics");
                return new ThreadPoolStatisticsEnhanced();
            }
        }
        
        public void Dispose()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                
                // Wait for threads to finish
                foreach (var thread in _threads)
                {
                    thread.Join(TimeSpan.FromSeconds(5));
                }
                
                _cancellationTokenSource?.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during ThreadPool disposal");
            }
        }
    }
    
    public class ThreadPoolStatisticsEnhanced
    {
        public int TotalThreads { get; set; }
        public int ActiveThreads { get; set; }
        public int DisposedThreads { get; set; }
        public int PendingTasks { get; set; }
        public int MaxThreads { get; set; }
        public bool IsActive { get; set; }
    }
    
    #endregion
    
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
    
    public class ThreadPoolTask
    {
        public string TaskId { get; set; }
        public Func<Task> TaskAction { get; set; }
        public DateTime CreatedTime { get; set; }
    }
    
    /// <summary>
    /// Processor for async file operations with error handling
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