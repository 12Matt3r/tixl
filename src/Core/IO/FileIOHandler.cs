using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TiXL.Core.Performance;

namespace TiXL.Core.IO
{
    /// <summary>
    /// Enhanced File I/O handler with real async operations and thread isolation
    /// Integrates with AsyncFileOperations and SafeFileIO for comprehensive file handling
    /// </summary>
    public class FileIOHandler : IDisposable
    {
        private readonly AsyncFileOperations _asyncFileOperations;
        private readonly SafeFileIO _safeFileIO;
        private readonly Dictionary<string, FileOperationContext> _activeOperations;
        private readonly SemaphoreSlim _operationSemaphore;
        
        // Real progress tracking for file operations
        private readonly Dictionary<string, FileOperationProgress> _operationProgress;
        private readonly Timer _progressCheckTimer;
        
        // Thread isolation for file operations
        private readonly ThreadPool _dedicatedFileThreadPool;
        private readonly SemaphoreSlim _fileOperationSemaphore;
        
        private volatile bool _isDisposed = false;
        
        public event EventHandler<FileEventProcessed> FileEventProcessed;
        public event EventHandler<FileError> FileErrorOccurred;
        public event EventHandler<FileOperationProgress> ProgressUpdated;
        
        public FileIOHandler(AsyncFileOperations asyncFileOperations = null, SafeFileIO safeFileIO = null)
        {
            _asyncFileOperations = asyncFileOperations ?? new AsyncFileOperations();
            _safeFileIO = safeFileIO ?? SafeFileIO.Instance;
            _activeOperations = new Dictionary<string, FileOperationContext>();
            _operationProgress = new Dictionary<string, FileOperationProgress>();
            _operationSemaphore = new SemaphoreSlim(20, 20);
            _fileOperationSemaphore = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);
            
            // Initialize dedicated thread pool for file I/O
            _dedicatedFileThreadPool = new ThreadPool(Math.Max(4, Environment.ProcessorCount));
            
            // Start progress monitoring timer
            _progressCheckTimer = new Timer(CheckOperationProgress, null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
        }
        
        /// <summary>
        /// Process file event with real async operations and thread isolation
        /// </summary>
        public async Task ProcessFileEventAsync(IOEvent ioEvent)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(FileIOHandler));
                
            using var perfTracker = PerfTracker.Start("FileEventProcessing");
            
            try
            {
                await _operationSemaphore.WaitAsync(ioEvent.CancellationToken);
                
                var context = new FileOperationContext
                {
                    OperationId = ioEvent.Id,
                    FilePath = ioEvent.FilePath,
                    EventType = ioEvent.EventType,
                    StartTime = DateTime.UtcNow,
                    Data = ioEvent.Data,
                    Metadata = new Dictionary<string, string>(ioEvent.Metadata)
                };
                
                lock (_activeOperations)
                {
                    _activeOperations[ioEvent.Id] = context;
                }
                
                var result = ioEvent.EventType switch
                {
                    IOEventType.FileRead => await ProcessFileReadAsync(context),
                    IOEventType.FileWrite => await ProcessFileWriteAsync(context),
                    _ => throw new ArgumentException($"Unsupported file event type: {ioEvent.EventType}")
                };
                
                context.CompletedTime = DateTime.UtcNow;
                context.Success = result.IsSuccess;
                context.ErrorMessage = result.ErrorMessage;
                
                OnFileEventProcessed(new FileEventProcessed
                {
                    EventId = ioEvent.Id,
                    EventType = ioEvent.EventType.ToString(),
                    FilePath = context.FilePath,
                    BytesProcessed = context.Data?.Length ?? 0,
                    ProcessingTimeMs = perfTracker.ElapsedMilliseconds,
                    Success = result.IsSuccess
                });
            }
            catch (OperationCanceledException)
            {
                HandleOperationCancelled(ioEvent);
                throw;
            }
            catch (Exception ex)
            {
                OnFileError(new FileError
                {
                    EventId = ioEvent.Id,
                    EventType = ioEvent.EventType.ToString(),
                    FilePath = ioEvent.FilePath,
                    ErrorMessage = ex.Message,
                    ErrorType = ex.GetType().Name,
                    Timestamp = DateTime.UtcNow
                });
                throw;
            }
            finally
            {
                lock (_activeOperations)
                {
                    _activeOperations.Remove(ioEvent.Id);
                }
                
                _operationSemaphore.Release();
            }
        }
        
        /// <summary>
        /// Process file read with real async operations
        /// </summary>
        private async Task<FileOperationResult> ProcessFileReadAsync(FileOperationContext context)
        {
            try
            {
                // Use SafeFileIO for path validation and basic operations
                var validation = _safeFileIO.ValidateWritePath(context.FilePath);
                if (!validation.IsValid)
                {
                    // For reads, also check read validation
                    validation = ValidateReadPath(context.FilePath);
                    if (!validation.IsValid)
                    {
                        return FileOperationResult.Failed($"Read validation failed: {validation.ErrorMessage}");
                    }
                }
                
                // Execute on dedicated I/O thread for true isolation
                var readResult = await ExecuteOnDedicatedFileThread(() => 
                    _asyncFileOperations.ReadFileAsync(context.FilePath, context.CancellationToken, context.OperationId), 
                    context.CancellationToken);
                
                if (readResult.Success)
                {
                    context.Data = readResult.Data;
                    context.BytesProcessed = readResult.BytesRead;
                    
                    // Update metadata
                    context.Metadata["BytesRead"] = readResult.BytesRead.ToString();
                    context.Metadata["ReadTimeMs"] = readResult.ElapsedTime.TotalMilliseconds.ToString("F2");
                    
                    return FileOperationResult.Success($"Read {readResult.BytesRead} bytes");
                }
                else
                {
                    return FileOperationResult.Failed(readResult.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                return FileOperationResult.Failed($"Read failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Process file write with real async operations
        /// </summary>
        private async Task<FileOperationResult> ProcessFileWriteAsync(FileOperationContext context)
        {
            try
            {
                if (context.Data == null)
                {
                    return FileOperationResult.Failed("No data provided for write operation");
                }
                
                // Use SafeFileIO for path validation
                var validation = _safeFileIO.ValidateWritePath(context.FilePath);
                if (!validation.IsValid)
                {
                    return FileOperationResult.Failed($"Write validation failed: {validation.ErrorMessage}");
                }
                
                // Create backup if requested in metadata
                bool createBackup = false;
                if (context.Metadata.TryGetValue("CreateBackup", out var backupValue) && 
                    bool.TryParse(backupValue, out createBackup))
                {
                    // createBackup is set by the TryParse output parameter
                }
                
                // Execute on dedicated I/O thread for true isolation
                var writeResult = await ExecuteOnDedicatedFileThread(() => 
                    _asyncFileOperations.WriteFileAsync(context.FilePath, context.Data, createBackup, context.CancellationToken, context.OperationId), 
                    context.CancellationToken);
                
                if (writeResult.Success)
                {
                    context.BytesProcessed = writeResult.BytesWritten;
                    
                    // Update metadata
                    context.Metadata["BytesWritten"] = writeResult.BytesWritten.ToString();
                    context.Metadata["WriteTimeMs"] = writeResult.ElapsedTime.TotalMilliseconds.ToString("F2");
                    
                    return FileOperationResult.Success($"Wrote {writeResult.BytesWritten} bytes");
                }
                else
                {
                    return FileOperationResult.Failed(writeResult.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                return FileOperationResult.Failed($"Write failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Execute operation on dedicated file thread for true I/O isolation
        /// </summary>
        private async Task<T> ExecuteOnDedicatedFileThread<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
        {
            await _fileOperationSemaphore.WaitAsync(cancellationToken);
            
            try
            {
                var taskCompletionSource = new TaskCompletionSource<T>();
                
                // Execute on dedicated file thread
                _dedicatedFileThreadPool.QueueTask(async () =>
                {
                    try
                    {
                        var result = await operation();
                        taskCompletionSource.TrySetResult(result);
                    }
                    catch (Exception ex)
                    {
                        taskCompletionSource.TrySetException(ex);
                    }
                }, $"FileOp_{Guid.NewGuid().ToString("N")[..8]}");
                
                // Wait for completion with timeout
                return await taskCompletionSource.Task.WaitAsync(TimeSpan.FromMinutes(5), cancellationToken);
            }
            finally
            {
                _fileOperationSemaphore.Release();
            }
        }
        
        /// <summary>
        /// Execute operation on dedicated file thread (fire and forget)
        /// </summary>
        public async Task ExecuteFileOperationAsync(Func<Task> operation, string operationName = null, Action<Exception> errorHandler = null)
        {
            await _dedicatedFileThreadPool.QueueTask(async () =>
            {
                try
                {
                    await operation();
                }
                catch (Exception ex)
                {
                    errorHandler?.Invoke(ex);
                    OnFileError(new FileError
                    {
                        EventId = Guid.NewGuid().ToString(),
                        EventType = "BackgroundFileOperation",
                        ErrorMessage = $"Background file operation failed: {ex.Message}",
                        ErrorType = ex.GetType().Name,
                        Timestamp = DateTime.UtcNow
                    });
                }
            }, operationName);
        }
        
        /// <summary>
        /// Get file operation progress
        /// </summary>
        public FileOperationProgress GetOperationProgress(string operationId)
        {
            lock (_operationProgress)
            {
                return _operationProgress.TryGetValue(operationId, out var progress) ? progress : null;
            }
        }
        
        /// <summary>
        /// Cancel file operation
        /// </summary>
        public void CancelOperation(string operationId)
        {
            _asyncFileOperations?.CancelOperation(operationId);
            
            lock (_operationProgress)
            {
                _operationProgress.Remove(operationId);
            }
        }
        
        /// <summary>
        /// Get active operation count
        /// </summary>
        public int GetActiveOperationCount()
        {
            return _activeOperations.Count;
        }
        
        /// <summary>
        /// Get file handler statistics
        /// </summary>
        public FileHandlerStatistics GetStatistics()
        {
            var threadPoolStats = _dedicatedFileThreadPool.GetStatistics();
            
            return new FileHandlerStatistics
            {
                ActiveOperations = _activeOperations.Count,
                TotalOperations = _activeOperations.Values.Count(ctx => ctx.Success) + _activeOperations.Values.Count(ctx => !ctx.Success),
                SuccessfulOperations = _activeOperations.Values.Count(ctx => ctx.Success),
                FailedOperations = _activeOperations.Values.Count(ctx => ctx.Success == false),
                AverageProcessingTimeMs = _activeOperations.Values.Any() ? 
                    _activeOperations.Values.Average(ctx => (ctx.CompletedTime - ctx.StartTime).TotalMilliseconds) : 0,
                ThreadPoolActiveThreads = threadPoolStats.ActiveThreads,
                ThreadPoolUtilization = threadPoolStats.ActiveThreads > 0 ? (double)threadPoolStats.ActiveThreads / threadPoolStats.MaxThreads * 100 : 0,
                IsThreadIsolated = true,
                HasDedicatedFileThreads = true
            };
        }
        
        private void CheckOperationProgress(object state)
        {
            if (_isDisposed) return;
            
            try
            {
                lock (_activeOperations)
                {
                    foreach (var kvp in _activeOperations)
                    {
                        var context = kvp.Value;
                        var progress = new FileOperationProgress
                        {
                            OperationId = kvp.Key,
                            FilePath = context.FilePath,
                            StartTime = context.StartTime,
                            ElapsedTime = DateTime.UtcNow - context.StartTime,
                            Status = context.Success ? "Completed" : 
                                    (context.ErrorMessage != null ? "Failed" : "Processing"),
                            BytesProcessed = context.BytesProcessed ?? 0
                        };
                        
                        lock (_operationProgress)
                        {
                            _operationProgress[kvp.Key] = progress;
                        }
                        
                        ProgressUpdated?.Invoke(this, progress);
                    }
                }
            }
            catch (Exception ex)
            {
                OnFileError(new FileError
                {
                    EventId = "ProgressCheck",
                    ErrorMessage = $"Progress check failed: {ex.Message}",
                    ErrorType = ex.GetType().Name,
                    Timestamp = DateTime.UtcNow
                });
            }
        }
        
        private void HandleOperationCancelled(IOEvent ioEvent)
        {
            OnFileError(new FileError
            {
                EventId = ioEvent.Id,
                EventType = ioEvent.EventType.ToString(),
                FilePath = ioEvent.FilePath,
                ErrorMessage = "Operation was cancelled",
                ErrorType = "OperationCanceledException",
                Timestamp = DateTime.UtcNow
            });
        }
        
        private ValidationResult ValidateReadPath(string path)
        {
            var result = new ValidationResult { IsValid = true };
            
            if (string.IsNullOrWhiteSpace(path))
            {
                result.IsValid = false;
                result.ErrorMessage = "Path cannot be null or empty";
                return result;
            }
            
            try
            {
                var fullPath = Path.GetFullPath(path);
                if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
                {
                    result.IsValid = false;
                    result.ErrorMessage = "File or directory does not exist";
                    return result;
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.ErrorMessage = $"Invalid path: {ex.Message}";
                return result;
            }
            
            return result;
        }
        
        protected virtual void OnFileEventProcessed(FileEventProcessed args)
        {
            FileEventProcessed?.Invoke(this, args);
        }
        
        protected virtual void OnFileError(FileError args)
        {
            FileErrorOccurred?.Invoke(this, args);
        }
        
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            
            try
            {
                _progressCheckTimer?.Dispose();
                _operationSemaphore?.Dispose();
                _fileOperationSemaphore?.Dispose();
                _dedicatedFileThreadPool?.Dispose();
                
                lock (_activeOperations)
                {
                    _activeOperations.Clear();
                }
                
                lock (_operationProgress)
                {
                    _operationProgress.Clear();
                }
            }
            catch (Exception ex)
            {
                // Log disposal error (in a real implementation, you'd have proper logging)
                Console.WriteLine($"FileIOHandler disposal error: {ex.Message}");
            }
        }
    }
    
    #region Supporting Classes
    
    public class FileOperationContext
    {
        public string OperationId { get; set; }
        public string FilePath { get; set; }
        public IOEventType EventType { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime CompletedTime { get; set; }
        public byte[] Data { get; set; }
        public long? BytesProcessed { get; set; }
        public bool? Success { get; set; }
        public string ErrorMessage { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
        public CancellationToken CancellationToken { get; set; }
    }
    
    public class FileOperationResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public string ErrorMessage { get; set; }
        public TimeSpan ElapsedTime { get; set; }
        
        public static FileOperationResult Success(string message)
        {
            return new FileOperationResult { IsSuccess = true, Message = message };
        }
        
        public static FileOperationResult Failed(string error)
        {
            return new FileOperationResult { IsSuccess = false, ErrorMessage = error };
        }
    }
    
    public class FileOperationProgress
    {
        public string OperationId { get; set; }
        public string FilePath { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan ElapsedTime { get; set; }
        public string Status { get; set; }
        public long BytesProcessed { get; set; }
        public double Percentage { get; set; }
    }
    
    public class FileHandlerStatistics
    {
        public int ActiveOperations { get; set; }
        public int TotalOperations { get; set; }
        public int SuccessfulOperations { get; set; }
        public int FailedOperations { get; set; }
        public double AverageProcessingTimeMs { get; set; }
        public int ThreadPoolActiveThreads { get; set; }
        public double ThreadPoolUtilization { get; set; }
        public bool IsThreadIsolated { get; set; }
        public bool HasDedicatedFileThreads { get; set; }
        public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
    }
    
    #endregion
}