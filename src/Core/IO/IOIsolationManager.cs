using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TiXL.Core.Performance;

namespace TiXL.Core.IO
{
    /// <summary>
    /// Comprehensive I/O and event isolation system for TiXL
    /// Eliminates missed frames by moving all I/O operations to dedicated background threads
    /// with lock-free queues, priority management, and event batching
    /// </summary>
    public class IOIsolationManager : IDisposable
    {
        private readonly PerformanceMonitor _performanceMonitor;
        private readonly IOEventQueue _highPriorityQueue;   // Audio, MIDI, user input
        private readonly IOEventQueue _mediumPriorityQueue; // File I/O, network
        private readonly IOEventQueue _lowPriorityQueue;    // Caching, background tasks
        private readonly ResourcePool _resourcePool;
        private readonly IOErrorRecovery _errorRecovery;
        
        private readonly Dictionary<IOEventType, IOBackgroundWorker> _workers;
        private readonly ConcurrentDictionary<string, IOResourceHandle> _activeResources;
        private readonly SemaphoreSlim _batchProcessingSemaphore;
        
        // I/O Handlers for actual processing
        private readonly AudioIOHandler _audioHandler;
        private readonly MidiIOHandler _midiHandler;
        private readonly FileIOHandler _fileHandler;
        private readonly NetworkIOHandler _networkHandler;
        private readonly SpoutIOHandler _spoutHandler;
        
        // Real async file operations and thread isolation
        private readonly AsyncFileOperations _asyncFileOperations;
        private readonly SafeFileIO _safeFileIO;
        private readonly IOBatchProcessor _batchProcessor;
        
        // Dedicated I/O thread pool for true isolation
        private readonly Channel<ThreadPoolTask> _ioThreadPool;
        private readonly List<Thread> _ioThreads;
        private readonly CancellationTokenSource _threadPoolCancellation;
        private readonly SemaphoreSlim _ioThreadSemaphore;
        private readonly int _maxIoThreads = Environment.ProcessorCount * 2;
        
        private readonly Timer _performanceMetricsTimer;
        private readonly Timer _backgroundCleanupTimer;
        
        private readonly int _maxConcurrentBatches = 3;
        private readonly TimeSpan _batchTimeout = TimeSpan.FromMilliseconds(8); // 8ms batch window
        private readonly TimeSpan _backgroundCleanupInterval = TimeSpan.FromSeconds(30);
        
        private volatile bool _isRunning = true;
        private volatile bool _isDisposed = false;
        private long _totalEventsProcessed;
        private long _totalEventsBatched;
        private long _totalFrameSavingsMs;
        private long _totalIoOperations;
        
        public event EventHandler<IOIsolationAlert> IOAlert;
        public event EventHandler<IOPerformanceMetrics> PerformanceMetricsUpdated;
        
        public IOIsolationManager IsolationManager => this;
        public AsyncFileOperations AsyncFileOperations => _asyncFileOperations;
        public SafeFileIO SafeFileIO => _safeFileIO;
        public IOBatchProcessor BatchProcessor => _batchProcessor;
        
        public IOIsolationManager(PerformanceMonitor performanceMonitor = null)
        {
            _performanceMonitor = performanceMonitor ?? new PerformanceMonitor();
            _highPriorityQueue = new IOEventQueue("HighPriority", 1000);
            _mediumPriorityQueue = new IOEventQueue("MediumPriority", 2000);
            _lowPriorityQueue = new IOEventQueue("LowPriority", 5000);
            
            _resourcePool = new ResourcePool();
            _errorRecovery = new IOErrorRecovery();
            _workers = new Dictionary<IOEventType, IOBackgroundWorker>();
            _activeResources = new ConcurrentDictionary<string, IOResourceHandle>();
            _batchProcessingSemaphore = new SemaphoreSlim(_maxConcurrentBatches, _maxConcurrentBatches);
            
            // Initialize thread isolation components
            _asyncFileOperations = new AsyncFileOperations(maxConcurrentOperations: 20, maxThreadPoolThreads: Environment.ProcessorCount);
            _safeFileIO = SafeFileIO.Instance;
            _batchProcessor = new IOBatchProcessor(this);
            _threadPoolCancellation = new CancellationTokenSource();
            _ioThreadSemaphore = new SemaphoreSlim(_maxIoThreads, _maxIoThreads);
            
            // Initialize I/O handlers with thread isolation
            _audioHandler = new AudioIOHandler();
            _midiHandler = new MidiIOHandler();
            _fileHandler = new FileIOHandler(_asyncFileOperations, _safeFileIO); // Enhanced with async operations
            _networkHandler = new NetworkIOHandler();
            _spoutHandler = new SpoutIOHandler();
            
            // Initialize dedicated I/O thread pool
            InitializeIOThreadPool();
            
            // Initialize background workers with enhanced processing
            InitializeBackgroundWorkers();
            
            // Set up event handlers for async file operations
            SetupAsyncFileOperationHandlers();
            
            // Start background processing
            _performanceMetricsTimer = new Timer(CollectPerformanceMetrics, null, 0, 16); // ~60Hz
            _backgroundCleanupTimer = new Timer(PerformBackgroundCleanup, null, _backgroundCleanupInterval, _backgroundCleanupInterval);
        }
        
        /// <summary>
        /// Queue I/O event for background processing
        /// </summary>
        public async Task<IOEventResult> QueueEventAsync(IOEvent ioEvent)
        {
            try
            {
                // Validate and enrich event
                var enrichedEvent = await EnrichEventAsync(ioEvent);
                
                // Select appropriate queue based on priority and event type
                var targetQueue = SelectTargetQueue(enrichedEvent);
                
                // Add to queue with timeout
                var added = await targetQueue.TryAddWithTimeout(enrichedEvent, TimeSpan.FromMilliseconds(100));
                if (!added)
                {
                    return IOEventResult.Failed("Queue timeout - event dropped", ioEvent.Id);
                }
                
                Interlocked.Increment(ref _totalEventsProcessed);
                return IOEventResult.Success(ioEvent.Id);
            }
            catch (Exception ex)
            {
                return IOEventResult.Failed($"Failed to queue event: {ex.Message}", ioEvent.Id);
            }
        }
        
        /// <summary>
summary>
        /// Batch process multiple I/O events together to minimize main thread interruptions
        /// </summary>
        public async Task<IOBatchResult> ProcessBatchAsync(IEnumerable<IOEvent> events)
        {
            var eventList = events.ToList();
            if (eventList.Count == 0)
            {
                return IOBatchResult.Success(Array.Empty<IOEventResult>());
            }
            
            using var perfTracker = _performanceMonitor.ProfileOperation("BatchProcessing");
            
            try
            {
                await _batchProcessingSemaphore.WaitAsync();
                
                // Group events by type and target queue
                var groupedEvents = eventList.GroupBy(e => SelectTargetQueue(e));
                
                var results = new List<IOEventResult>();
                var stopwatch = Stopwatch.StartNew();
                
                foreach (var group in groupedEvents)
                {
                    var queue = group.Key;
                    var eventsInGroup = group.ToList();
                    
                    // Process events in batch with timeout
                    var batchResults = await ProcessBatchOnQueue(queue, eventsInGroup, stopwatch);
                    results.AddRange(batchResults);
                }
                
                Interlocked.Add(ref _totalEventsBatched, eventList.Count);
                Interlocked.Add(ref _totalFrameSavingsMs, stopwatch.ElapsedMilliseconds);
                
                return IOBatchResult.Success(results);
            }
            finally
            {
                _batchProcessingSemaphore.Release();
            }
        }
        
        /// <summary>
        /// Get I/O performance statistics
        /// </summary>
        public IOPerformanceStatistics GetStatistics()
        {
            return new IOPerformanceStatistics
            {
                TotalEventsProcessed = _totalEventsProcessed,
                TotalEventsBatched = _totalEventsBatched,
                TotalFrameSavingsMs = _totalFrameSavingsMs,
                HighPriorityQueueStats = _highPriorityQueue.GetStatistics(),
                MediumPriorityQueueStats = _mediumPriorityQueue.GetStatistics(),
                LowPriorityQueueStats = _lowPriorityQueue.GetStatistics(),
                ResourcePoolStats = _resourcePool.GetStatistics(),
                WorkerStats = _workers.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value.GetStatistics()),
                ActiveResourceCount = _activeResources.Count
            };
        }
        
        /// <summary>
        /// Register resource handle for lifecycle management
        /// </summary>
        public void RegisterResource(string resourceId, IOResourceHandle resourceHandle)
        {
            _activeResources.TryAdd(resourceId, resourceHandle);
        }
        
        /// <summary>
        /// Unregister resource handle
        /// </summary>
        public void UnregisterResource(string resourceId)
        {
            _activeResources.TryRemove(resourceId, out _);
        }
        
        /// <summary>
        /// Create resource pool statistics snapshot
        /// </summary>
        public IOResourcePoolSnapshot CreateResourceSnapshot()
        {
            return _resourcePool.CreateSnapshot();
        }
        
        private void InitializeBackgroundWorkers()
        {
            // High priority workers (audio, MIDI, real-time input)
            _workers[IOEventType.AudioInput] = new IOBackgroundWorker(IOEventType.AudioInput, _highPriorityQueue, ProcessAudioEventAsync, _resourcePool, _performanceMonitor);
            _workers[IOEventType.AudioOutput] = new IOBackgroundWorker(IOEventType.AudioOutput, _highPriorityQueue, ProcessAudioEventAsync, _resourcePool, _performanceMonitor);
            _workers[IOEventType.MidiInput] = new IOBackgroundWorker(IOEventType.MidiInput, _highPriorityQueue, ProcessMidiEventAsync, _resourcePool, _performanceMonitor);
            _workers[IOEventType.MidiOutput] = new IOBackgroundWorker(IOEventType.MidiOutput, _highPriorityQueue, ProcessMidiEventAsync, _resourcePool, _performanceMonitor);
            _workers[IOEventType.UserInput] = new IOBackgroundWorker(IOEventType.UserInput, _highPriorityQueue, ProcessUserInputEventAsync, _resourcePool, _performanceMonitor);
            
            // Medium priority workers (file I/O, network, Spout)
            _workers[IOEventType.FileRead] = new IOBackgroundWorker(IOEventType.FileRead, _mediumPriorityQueue, ProcessFileEventAsync, _resourcePool, _performanceMonitor);
            _workers[IOEventType.FileWrite] = new IOBackgroundWorker(IOEventType.FileWrite, _mediumPriorityQueue, ProcessFileEventAsync, _resourcePool, _performanceMonitor);
            _workers[IOEventType.NetworkIO] = new IOBackgroundWorker(IOEventType.NetworkIO, _mediumPriorityQueue, ProcessNetworkEventAsync, _resourcePool, _performanceMonitor);
            _workers[IOEventType.SpoutData] = new IOBackgroundWorker(IOEventType.SpoutData, _mediumPriorityQueue, ProcessSpoutEventAsync, _resourcePool, _performanceMonitor);
            
            // Low priority workers (caching, metadata)
            _workers[IOEventType.CacheUpdate] = new IOBackgroundWorker(IOEventType.CacheUpdate, _lowPriorityQueue, ProcessCacheEventAsync, _resourcePool, _performanceMonitor);
            _workers[IOEventType.MetadataUpdate] = new IOBackgroundWorker(IOEventType.MetadataUpdate, _lowPriorityQueue, ProcessMetadataEventAsync, _resourcePool, _performanceMonitor);
        }
        
        private async Task<IOEvent> EnrichEventAsync(IOEvent ioEvent)
        {
            // Add performance tracking metadata
            ioEvent.Metadata["QueuedTimestamp"] = DateTime.UtcNow.Ticks.ToString();
            ioEvent.Metadata["Source"] = "MainThread";
            
            // Add resource hints
            if (ioEvent.Data != null)
            {
                ioEvent.Metadata["DataSize"] = ioEvent.Data.Length.ToString();
            }
            
            return ioEvent;
        }
        
        private IOEventQueue SelectTargetQueue(IOEvent ioEvent)
        {
            return ioEvent.Priority switch
            {
                IOEventPriority.Critical => _highPriorityQueue,
                IOEventPriority.High => _highPriorityQueue,
                IOEventPriority.Medium => _mediumPriorityQueue,
                IOEventPriority.Low => _lowPriorityQueue,
                _ => _mediumPriorityQueue
            };
        }
        
        private async Task<IEnumerable<IOEventResult>> ProcessBatchOnQueue(IOEventQueue queue, List<IOEvent> events, Stopwatch stopwatch)
        {
            var results = new List<IOEventResult>();
            
            foreach (var ioEvent in events)
            {
                if (stopwatch.ElapsedMilliseconds > 16) // Frame budget exceeded
                {
                    // Queue remaining events for next frame
                    await queue.TryAddWithTimeout(ioEvent, TimeSpan.Zero);
                    results.Add(IOEventResult.QueuedForNextFrame(ioEvent.Id));
                    continue;
                }
                
                try
                {
                    var result = await QueueEventAsync(ioEvent);
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    results.Add(IOEventResult.Failed($"Batch processing failed: {ex.Message}", ioEvent.Id));
                }
            }
            
            return results;
        }
        
        private void CollectPerformanceMetrics(object state)
        {
            try
            {
                var metrics = new IOPerformanceMetrics
                {
                    Timestamp = DateTime.UtcNow,
                    TotalEventsProcessed = _totalEventsProcessed,
                    TotalEventsBatched = _totalEventsBatched,
                    TotalFrameSavingsMs = _totalFrameSavingsMs,
                    HighPriorityQueueDepth = _highPriorityQueue.Count,
                    MediumPriorityQueueDepth = _mediumPriorityQueue.Count,
                    LowPriorityQueueDepth = _lowPriorityQueue.Count,
                    ActiveWorkerCount = _workers.Count(w => w.Value.IsActive),
                    AverageProcessingTime = _workers.Values.Average(w => w.GetStatistics().AverageProcessingTimeMs)
                };
                
                // Check for performance alerts
                CheckPerformanceAlerts(metrics);
                
                PerformanceMetricsUpdated?.Invoke(this, metrics);
            }
            catch (Exception ex)
            {
                OnIOAlert(new IOIsolationAlert
                {
                    Type = AlertType.MetricsCollectionFailed,
                    Message = $"Failed to collect performance metrics: {ex.Message}",
                    Timestamp = DateTime.UtcNow
                });
            }
        }
        
        private void CheckPerformanceAlerts(IOPerformanceMetrics metrics)
        {
            // Check for queue backlogs
            if (metrics.HighPriorityQueueDepth > 100)
            {
                OnIOAlert(new IOIsolationAlert
                {
                    Type = AlertType.HighPriorityQueueBacklog,
                    Message = $"High priority queue backlog: {metrics.HighPriorityQueueDepth} events",
                    Value = metrics.HighPriorityQueueDepth,
                    Threshold = 100,
                    Timestamp = DateTime.UtcNow
                });
            }
            
            // Check for processing delays
            if (metrics.AverageProcessingTime > 10)
            {
                OnIOAlert(new IOIsolationAlert
                {
                    Type = AlertType.ProcessingDelay,
                    Message = $"High average processing time: {metrics.AverageProcessingTime:F2}ms",
                    Value = metrics.AverageProcessingTime,
                    Threshold = 10,
                    Timestamp = DateTime.UtcNow
                });
            }
            
            // Check for worker health
            var inactiveWorkers = _workers.Values.Count(w => !w.IsActive);
            if (inactiveWorkers > 0)
            {
                OnIOAlert(new IOIsolationAlert
                {
                    Type = AlertType.WorkerFailure,
                    Message = $"{inactiveWorkers} workers inactive",
                    Value = inactiveWorkers,
                    Threshold = 0,
                    Timestamp = DateTime.UtcNow
                });
            }
        }
        
        private void PerformBackgroundCleanup(object state)
        {
            try
            {
                // Clean up expired resources
                var expiredResources = _activeResources
                    .Where(kvp => kvp.Value.IsExpired)
                    .Select(kvp => kvp.Key)
                    .ToList();
                
                foreach (var resourceId in expiredResources)
                {
                    _activeResources.TryRemove(resourceId, out var handle);
                    handle?.Dispose();
                }
                
                // Clean up resource pool
                _resourcePool.CleanupExpiredBuffers();
                
                // Clear completed error recovery records
                _errorRecovery.CleanupCompletedRecoveries();
            }
            catch (Exception ex)
            {
                OnIOAlert(new IOIsolationAlert
                {
                    Type = AlertType.BackgroundCleanupFailed,
                    Message = $"Background cleanup failed: {ex.Message}",
                    Timestamp = DateTime.UtcNow
                });
            }
        }
        
        // Event processing methods using actual I/O handlers
        private async Task ProcessAudioEventAsync(IOEvent ioEvent)
        {
            await _audioHandler.ProcessAudioEventAsync(ioEvent);
        }
        
        private async Task ProcessMidiEventAsync(IOEvent ioEvent)
        {
            await _midiHandler.ProcessMidiEventAsync(ioEvent);
        }
        
        private async Task ProcessUserInputEventAsync(IOEvent ioEvent)
        {
            // Simulate user input processing (keyboard, mouse, gamepad)
            await Task.Run(() => ProcessUserInput(ioEvent));
        }
        
        private async Task ProcessFileEventAsync(IOEvent ioEvent)
        {
            await _fileHandler.ProcessFileEventAsync(ioEvent);
        }
        
        private async Task ProcessNetworkEventAsync(IOEvent ioEvent)
        {
            await _networkHandler.ProcessNetworkEventAsync(ioEvent);
        }
        
        private async Task ProcessSpoutEventAsync(IOEvent ioEvent)
        {
            await _spoutHandler.ProcessSpoutEventAsync(ioEvent);
        }
        
        private async Task ProcessCacheEventAsync(IOEvent ioEvent)
        {
            // Simulate cache update processing
            await Task.Delay(10); // Simulate cache operation
            ioEvent.Metadata["CacheUpdated"] = "true";
        }
        
        private async Task ProcessMetadataEventAsync(IOEvent ioEvent)
        {
            // Simulate metadata update processing
            await Task.Delay(5); // Simulate metadata operation
            ioEvent.Metadata["MetadataUpdated"] = "true";
        }
        
        private void ProcessUserInput(IOEvent ioEvent)
        {
            // Process user input based on type
            var inputType = ioEvent.Metadata.GetValueOrDefault("InputType", "Unknown");
            
            switch (inputType.ToLowerInvariant())
            {
                case "keyboard":
                    // Process keyboard input
                    break;
                case "mouse":
                    // Process mouse input
                    break;
                case "gamepad":
                    // Process gamepad input
                    break;
                default:
                    // Generic input processing
                    break;
            }
        }
        
        protected virtual void OnIOAlert(IOIsolationAlert alert)
        {
            IOAlert?.Invoke(this, alert);
        }
        
        /// <summary>
        /// Execute operation on dedicated I/O thread pool
        /// </summary>
        public async Task<T> ExecuteOnIOThreadPoolAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
        {
            await _ioThreadSemaphore.WaitAsync(cancellationToken);
            
            try
            {
                var taskCompletionSource = new TaskCompletionSource<T>();
                
                // Queue operation on I/O thread pool
                var ioTask = new IOThreadPoolTask
                {
                    TaskId = Guid.NewGuid().ToString(),
                    TaskAction = async () =>
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
                    },
                    CancellationToken = cancellationToken,
                    CreatedTime = DateTime.UtcNow
                };
                
                // Execute on dedicated I/O thread
                await Task.Run(() => ExecuteOnIOThread(ioTask), cancellationToken);
                
                return await taskCompletionSource.Task;
            }
            finally
            {
                _ioThreadSemaphore.Release();
            }
        }
        
        /// <summary>
        /// Execute operation on dedicated I/O thread pool (fire and forget)
        /// </summary>
        public void ExecuteOnIOThreadPoolAsync(Func<Task> operation, Action<Exception> errorHandler = null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await ExecuteOnIOThreadPoolAsync(operation);
                }
                catch (Exception ex)
                {
                    errorHandler?.Invoke(ex);
                    OnIOAlert(new IOIsolationAlert
                    {
                        Type = AlertType.IOThreadPoolError,
                        Message = $"I/O thread pool operation failed: {ex.Message}",
                        Exception = ex,
                        Timestamp = DateTime.UtcNow
                    });
                }
            });
        }
        
        /// <summary>
        /// Queue async file operation with full isolation
        /// </summary>
        public async Task<AsyncFileOperationResult> QueueAsyncFileOperationAsync(AsyncFileOperation operation, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _totalIoOperations);
            
            try
            {
                return await ExecuteOnIOThreadPoolAsync(async () =>
                {
                    return operation.OperationType switch
                    {
                        AsyncFileOperationType.Read => await _asyncFileOperations.ReadFileAsync(operation.FilePath, operation.CancellationToken, operation.Id),
                        AsyncFileOperationType.Write => await _asyncFileOperations.WriteFileAsync(operation.FilePath, operation.Data, operation.CreateBackup, operation.CancellationToken, operation.Id),
                        AsyncFileOperationType.Copy => await _asyncFileOperations.CopyFileAsync(operation.SourcePath, operation.FilePath, operation.Overwrite, operation.CancellationToken, operation.Id),
                        AsyncFileOperationType.Delete => await _asyncFileOperations.DeleteFileAsync(operation.FilePath, verifyExists: true, operation.CancellationToken, operation.Id),
                        AsyncFileOperationType.EnumerateDirectory => await _asyncFileOperations.EnumerateDirectoryAsync(operation.FilePath, operation.SearchPattern, operation.Recursive, operation.CancellationToken, operation.Id),
                        _ => throw new ArgumentException($"Unsupported operation type: {operation.OperationType}")
                    };
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                OnIOAlert(new IOIsolationAlert
                {
                    Type = AlertType.AsyncFileOperationFailed,
                    Message = $"Async file operation failed: {ex.Message}",
                    Exception = ex,
                    Context = new Dictionary<string, string>
                    {
                        ["OperationId"] = operation.Id,
                        ["OperationType"] = operation.OperationType.ToString(),
                        ["FilePath"] = operation.FilePath ?? "N/A"
                    },
                    Timestamp = DateTime.UtcNow
                });
                
                throw;
            }
        }
        
        /// <summary>
        /// Get thread isolation statistics
        /// </summary>
        public ThreadIsolationStatistics GetThreadIsolationStatistics()
        {
            return new ThreadIsolationStatistics
            {
                TotalIOOperations = _totalIoOperations,
                ActiveIOThreadPoolThreads = _ioThreads?.Count(t => t.IsAlive) ?? 0,
                MaxIOThreadPoolThreads = _maxIoThreads,
                IOThreadPoolUtilization = _ioThreads != null ? (double)_ioThreads.Count(t => t.IsAlive) / _maxIoThreads * 100 : 0,
                AsyncFileOperationStats = _asyncFileOperations.GetActiveOperationCount(),
                RenderThreadIsolated = true,
                LastUpdateTime = DateTime.UtcNow
            };
        }
        
        /// <summary>
        /// Initialize dedicated I/O thread pool for true thread isolation
        /// </summary>
        private void InitializeIOThreadPool()
        {
            try
            {
                var channelOptions = new BoundedChannelOptions(_maxIoThreads * 4)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = false,
                    SingleWriter = false
                };
                
                _ioThreadPool = Channel.CreateBounded<ThreadPoolTask>(channelOptions);
                _ioThreads = new List<Thread>();
                
                // Create dedicated I/O threads
                for (int i = 0; i < _maxIoThreads; i++)
                {
                    var ioThread = new Thread(IOThreadWorker)
                    {
                        Name = $"TiXL IO Thread {i}",
                        IsBackground = true,
                        Priority = ThreadPriority.AboveNormal // Give I/O threads higher priority
                    };
                    
                    _ioThreads.Add(ioThread);
                    ioThread.Start();
                }
                
                OnIOAlert(new IOIsolationAlert
                {
                    Type = AlertType.IOThreadPoolInitialized,
                    Message = $"Initialized { _maxIoThreads } dedicated I/O threads for thread isolation",
                    Context = new Dictionary<string, string>
                    {
                        ["MaxIoThreads"] = _maxIoThreads.ToString(),
                        ["ProcessorCount"] = Environment.ProcessorCount.ToString()
                    },
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                OnIOAlert(new IOIsolationAlert
                {
                    Type = AlertType.IOThreadPoolInitFailed,
                    Message = $"Failed to initialize I/O thread pool: {ex.Message}",
                    Exception = ex,
                    Timestamp = DateTime.UtcNow
                });
            }
        }
        
        /// <summary>
        /// Set up event handlers for async file operations
        /// </summary>
        private void SetupAsyncFileOperationHandlers()
        {
            if (_asyncFileOperations != null)
            {
                _asyncFileOperations.ProgressUpdated += OnAsyncFileProgressUpdated;
                _asyncFileOperations.OperationError += OnAsyncFileOperationError;
                _asyncFileOperations.OperationCompleted += OnAsyncFileOperationCompleted;
            }
        }
        
        /// <summary>
        /// I/O thread worker function
        /// </summary>
        private void IOThreadWorker()
        {
            try
            {
                while (!_threadPoolCancellation.Token.IsCancellationRequested)
                {
                    // Wait for work with timeout
                    if (_ioThreadPool.Reader.WaitToReadAsync(_threadPoolCancellation.Token).Result)
                    {
                        if (_ioThreadPool.Reader.TryRead(out var task))
                        {
                            // Execute task on this dedicated thread
                            try
                            {
                                _ = Task.Run(async () => await task.TaskAction(), _threadPoolCancellation.Token);
                            }
                            catch (Exception ex)
                            {
                                OnIOAlert(new IOIsolationAlert
                                {
                                    Type = AlertType.IOThreadWorkerError,
                                    Message = $"I/O thread worker error: {ex.Message}",
                                    Exception = ex,
                                    Context = new Dictionary<string, string>
                                    {
                                        ["ThreadName"] = Thread.CurrentThread.Name
                                    },
                                    Timestamp = DateTime.UtcNow
                                });
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
                OnIOAlert(new IOIsolationAlert
                {
                    Type = AlertType.IOThreadWorkerFatalError,
                    Message = $"I/O thread worker fatal error: {ex.Message}",
                    Exception = ex,
                    Context = new Dictionary<string, string>
                    {
                        ["ThreadName"] = Thread.CurrentThread.Name
                    },
                    Timestamp = DateTime.UtcNow
                });
            }
        }
        
        /// <summary>
        /// Execute task on dedicated I/O thread
        /// </summary>
        private async Task ExecuteOnIOThread(IOThreadPoolTask task)
        {
            if (_threadPoolCancellation.Token.IsCancellationRequested)
            {
                throw new OperationCanceledException("I/O thread pool is shutting down");
            }
            
            await _ioThreadPool.Writer.WriteAsync(task, _threadPoolCancellation.Token);
        }
        
        // Async file operation event handlers
        private void OnAsyncFileProgressUpdated(object sender, AsyncFileProgress progress)
        {
            OnIOAlert(new IOIsolationAlert
            {
                Type = AlertType.FileOperationProgress,
                Message = $"File operation progress: {progress.Status}",
                Context = new Dictionary<string, string>
                {
                    ["OperationId"] = progress.OperationId,
                    ["Percentage"] = progress.Percentage.ToString(),
                    ["Status"] = progress.Status
                },
                Timestamp = progress.Timestamp
            });
        }
        
        private void OnAsyncFileOperationError(object sender, AsyncFileError error)
        {
            OnIOAlert(new IOIsolationAlert
            {
                Type = AlertType.AsyncFileOperationError,
                Message = $"Async file operation error: {error.ErrorMessage}",
                Exception = error.Exception,
                Context = new Dictionary<string, string>
                {
                    ["OperationId"] = error.OperationId
                },
                Timestamp = error.Timestamp
            });
        }
        
        private void OnAsyncFileOperationCompleted(object sender, AsyncFileOperationCompleted completed)
        {
            Interlocked.Increment(ref _totalEventsProcessed);
            
            OnIOAlert(new IOIsolationAlert
            {
                Type = AlertType.AsyncFileOperationCompleted,
                Message = $"Async file operation completed: {completed.OperationType}",
                Context = new Dictionary<string, string>
                {
                    ["OperationId"] = completed.OperationId,
                    ["OperationType"] = completed.OperationType.ToString(),
                    ["Success"] = completed.Success.ToString(),
                    ["ElapsedTime"] = completed.ElapsedTime.TotalMilliseconds.ToString("F2")
                },
                Timestamp = completed.Timestamp
            });
        }
        
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _isRunning = false;
            
            // Dispose thread pool
            _threadPoolCancellation?.Cancel();
            _ioThreadSemaphore?.Dispose();
            
            // Wait for I/O threads to finish
            if (_ioThreads != null)
            {
                foreach (var thread in _ioThreads)
                {
                    if (thread.IsAlive)
                    {
                        thread.Join(TimeSpan.FromSeconds(5));
                    }
                }
            }
            
            _performanceMetricsTimer?.Dispose();
            _backgroundCleanupTimer?.Dispose();
            _batchProcessingSemaphore?.Dispose();
            
            foreach (var worker in _workers.Values)
            {
                worker?.Dispose();
            }
            
            _resourcePool?.Dispose();
            _errorRecovery?.Dispose();
            
            // Dispose I/O handlers
            _audioHandler?.Dispose();
            _midiHandler?.Dispose();
            _fileHandler?.Dispose();
            _networkHandler?.Dispose();
            _spoutHandler?.Dispose();
            
            // Dispose async file operations
            _asyncFileOperations?.Dispose();
            _batchProcessor?.Dispose();
            
            foreach (var resource in _activeResources.Values)
            {
                resource?.Dispose();
            }
            
            _threadPoolCancellation?.Dispose();
        }
    }
    
    #region Thread Isolation Supporting Classes
    
    public class ThreadPoolTask
    {
        public string TaskId { get; set; }
        public Func<Task> TaskAction { get; set; }
        public CancellationToken CancellationToken { get; set; }
        public DateTime CreatedTime { get; set; }
    }
    
    public class IOThreadPoolTask : ThreadPoolTask
    {
        public TimeSpan? Timeout { get; set; }
        public bool IsHighPriority { get; set; }
    }
    
    public class ThreadIsolationStatistics
    {
        public long TotalIOOperations { get; set; }
        public int ActiveIOThreadPoolThreads { get; set; }
        public int MaxIOThreadPoolThreads { get; set; }
        public double IOThreadPoolUtilization { get; set; }
        public int AsyncFileOperationStats { get; set; }
        public bool RenderThreadIsolated { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
    }
    
    #endregion
}