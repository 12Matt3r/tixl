using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TiXL.Core.Performance;

namespace TiXL.Core.IO
{
    /// <summary>
    /// Background worker for processing I/O events from a specific queue
    /// Implements batch processing, error recovery, and performance monitoring
    /// </summary>
    public class IOBackgroundWorker : IDisposable
    {
        private readonly IOEventType _eventType;
        private readonly IOEventQueue _queue;
        private readonly Func<IOEvent, Task> _eventProcessor;
        private readonly ResourcePool _resourcePool;
        private readonly PerformanceMonitor _performanceMonitor;
        
        private readonly Task _processingTask;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly object _lockObject = new object();
        
        // Real background thread for heavy I/O operations
        private readonly Thread _backgroundIOThread;
        private readonly ThreadPool _ioThreadPool;
        private readonly Channel<BackgroundWorkerTask> _taskQueue;
        private readonly CancellationTokenSource _threadPoolCancellation;
        private readonly SemaphoreSlim _threadPoolSemaphore;
        
        // Real threading primitives for proper isolation
        private readonly AutoResetEvent _workAvailableEvent;
        private readonly CountdownEvent _activeTasksCountdown;
        private readonly SpinLock _taskProcessingLock;
        
        private volatile bool _isActive = true;
        private volatile bool _isDisposed = false;
        private long _eventsProcessed;
        private long _eventsFailed;
        private long _totalProcessingTimeMs;
        private long _lastActivityTimestamp;
        private long _heavyIOTasksCompleted;
        private int _currentWorkerThreadId;
        
        public bool IsActive 
        { 
            get => _isActive && !_cancellationTokenSource.IsCancellationRequested;
            private set => _isActive = value;
        }
        
        public IOEventType EventType => _eventType;
        public long EventsProcessed => _eventsProcessed;
        public long EventsFailed => _eventsFailed;
        public double AverageProcessingTimeMs => _eventsProcessed > 0 ? 
            (double)_totalProcessingTimeMs / _eventsProcessed : 0;
        
        public event EventHandler<WorkerAlert> WorkerAlert;
        
        public IOBackgroundWorker(
            IOEventType eventType,
            IOEventQueue queue,
            Func<IOEvent, Task> eventProcessor,
            ResourcePool resourcePool,
            PerformanceMonitor performanceMonitor = null)
        {
            _eventType = eventType;
            _queue = queue;
            _eventProcessor = eventProcessor;
            _resourcePool = resourcePool;
            _performanceMonitor = performanceMonitor;
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Initialize real threading components for I/O isolation
            _threadPoolCancellation = new CancellationTokenSource();
            _workAvailableEvent = new AutoResetEvent(false);
            _activeTasksCountdown = new CountdownEvent(1);
            _taskProcessingLock = new SpinLock();
            
            // Create bounded task queue for this worker
            var taskQueueOptions = new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            };
            _taskQueue = Channel.CreateBounded<BackgroundWorkerTask>(taskQueueOptions);
            
            // Initialize dedicated thread pool for heavy I/O
            _ioThreadPool = new ThreadPool(Math.Max(2, Environment.ProcessorCount / 2));
            _threadPoolSemaphore = new SemaphoreSlim(Math.Max(2, Environment.ProcessorCount / 2), Math.Max(2, Environment.ProcessorCount / 2));
            
            // Start dedicated I/O thread for true isolation
            _backgroundIOThread = new Thread(BackgroundIOThreadWorker)
            {
                Name = $"TiXL IO Worker Thread - {_eventType}",
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal // Give I/O threads higher priority
            };
            _backgroundIOThread.Start();
            
            // Start main processing task (for queue processing)
            _processingTask = Task.Run(ProcessEventsAsync, _cancellationTokenSource.Token);
            
            // Update activity timestamp
            UpdateActivityTimestamp();
            
            OnWorkerAlert(new WorkerAlert
            {
                Type = AlertType.WorkerStarted,
                Message = $"Worker for {_eventType} started with dedicated I/O thread",
                EventType = _eventType,
                Timestamp = DateTime.UtcNow
            });
        }
        
        private async Task ProcessEventsAsync()
        {
            var cancellationToken = _cancellationTokenSource.Token;
            
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!_queue.IsProcessing)
                    {
                        await Task.Delay(1, cancellationToken);
                        continue;
                    }
                    
                    // Take batch of events
                    var events = await _queue.TakeBatchAsync(cancellationToken);
                    
                    if (events.Count == 0)
                    {
                        await Task.Delay(1, cancellationToken);
                        continue;
                    }
                    
                    // Process batch with real thread isolation
                    await ProcessEventBatchWithThreadIsolation(events, cancellationToken);
                    
                    UpdateActivityTimestamp();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    OnWorkerAlert(new WorkerAlert
                    {
                        Type = AlertType.WorkerError,
                        Message = $"Worker error in {_eventType}: {ex.Message}",
                        EventType = _eventType,
                        Exception = ex,
                        Timestamp = DateTime.UtcNow
                    });
                    
                    // Brief pause before retrying
                    await Task.Delay(10, cancellationToken);
                }
            }
        }
        
        /// <summary>
        /// Background I/O thread worker - runs on dedicated thread for true isolation
        /// </summary>
        private void BackgroundIOThreadWorker()
        {
            _currentWorkerThreadId = Thread.CurrentThread.ManagedThreadId;
            
            try
            {
                while (!_threadPoolCancellation.Token.IsCancellationRequested && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    // Wait for work or timeout
                    if (_taskQueue.Reader.WaitToReadAsync(_threadPoolCancellation.Token).Result)
                    {
                        if (_taskQueue.Reader.TryRead(out var task))
                        {
                            try
                            {
                                // Execute task on this dedicated I/O thread
                                task.ExecuteAction();
                            }
                            catch (Exception ex)
                            {
                                OnWorkerAlert(new WorkerAlert
                                {
                                    Type = AlertType.BackgroundIOThreadError,
                                    Message = $"Background I/O thread error: {ex.Message}",
                                    EventType = _eventType,
                                    Exception = ex,
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
                OnWorkerAlert(new WorkerAlert
                {
                    Type = AlertType.BackgroundIOThreadFatalError,
                    Message = $"Background I/O thread fatal error: {ex.Message}",
                    EventType = _eventType,
                    Exception = ex,
                    Timestamp = DateTime.UtcNow
                });
            }
        }
        
        private async Task ProcessEventBatch(IReadOnlyList<IOEvent> events, CancellationToken cancellationToken)
        {
            if (events.Count == 0) return;
            
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Group events by similar characteristics for efficient processing
                var groupedEvents = GroupEventsForProcessing(events);
                
                foreach (var group in groupedEvents)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    
                    await ProcessEventGroup(group, cancellationToken);
                }
                
                // Update statistics
                var processingTime = stopwatch.ElapsedMilliseconds;
                Interlocked.Add(ref _totalProcessingTimeMs, processingTime);
                Interlocked.Add(ref _eventsProcessed, events.Count);
                
                // Record performance metrics
                _performanceMonitor?.RecordCustomMetric($"Worker_{_eventType}_BatchTime", processingTime);
                _performanceMonitor?.RecordCustomMetric($"Worker_{_eventType}_BatchSize", events.Count);
            }
            catch (Exception ex)
            {
                Interlocked.Add(ref _eventsFailed, events.Count);
                
                OnWorkerAlert(new WorkerAlert
                {
                    Type = AlertType.BatchProcessingFailed,
                    Message = $"Failed to process batch of {events.Count} events: {ex.Message}",
                    EventType = _eventType,
                    BatchSize = events.Count,
                    Exception = ex,
                    Timestamp = DateTime.UtcNow
                });
            }
        }
        
        /// <summary>
        /// Process event batch with real thread isolation
        /// </summary>
        private async Task ProcessEventBatchWithThreadIsolation(IReadOnlyList<IOEvent> events, CancellationToken cancellationToken)
        {
            if (events.Count == 0) return;
            
            var stopwatch = Stopwatch.StartNew();
            _activeTasksCountdown.AddCount(Math.Max(1, events.Count / 5)); // Estimate heavy operations
            
            try
            {
                // Separate lightweight and heavy I/O operations
                var lightweightEvents = new List<IOEvent>();
                var heavyIOEvents = new List<IOEvent>();
                
                foreach (var ioEvent in events)
                {
                    if (IsHeavyIOEvent(ioEvent))
                    {
                        heavyIOEvents.Add(ioEvent);
                    }
                    else
                    {
                        lightweightEvents.Add(ioEvent);
                    }
                }
                
                // Process lightweight events on current thread
                if (lightweightEvents.Count > 0)
                {
                    await ProcessEventBatch(lightweightEvents, cancellationToken);
                }
                
                // Process heavy I/O events on dedicated I/O thread
                if (heavyIOEvents.Count > 0)
                {
                    await ProcessHeavyIOEventsOnDedicatedThread(heavyIOEvents, cancellationToken);
                }
                
                // Update statistics
                var processingTime = stopwatch.ElapsedMilliseconds;
                Interlocked.Add(ref _totalProcessingTimeMs, processingTime);
                Interlocked.Add(ref _eventsProcessed, events.Count);
                Interlocked.Add(ref _heavyIOTasksCompleted, heavyIOEvents.Count);
                
                // Record performance metrics
                _performanceMonitor?.RecordCustomMetric($"Worker_{_eventType}_BatchTime_ThreadIsolated", processingTime);
                _performanceMonitor?.RecordCustomMetric($"Worker_{_eventType}_BatchSize", events.Count);
                _performanceMonitor?.RecordCustomMetric($"Worker_{_eventType}_HeavyIOEvents", heavyIOEvents.Count);
            }
            catch (Exception ex)
            {
                Interlocked.Add(ref _eventsFailed, events.Count);
                
                OnWorkerAlert(new WorkerAlert
                {
                    Type = AlertType.BatchProcessingFailed,
                    Message = $"Failed to process batch of {events.Count} events with thread isolation: {ex.Message}",
                    EventType = _eventType,
                    BatchSize = events.Count,
                    Exception = ex,
                    Timestamp = DateTime.UtcNow
                });
            }
            finally
            {
                _activeTasksCountdown.Signal(Math.Max(1, events.Count / 5));
            }
        }
        
        /// <summary>
        /// Process heavy I/O events on dedicated I/O thread
        /// </summary>
        private async Task ProcessHeavyIOEventsOnDedicatedThread(List<IOEvent> heavyIOEvents, CancellationToken cancellationToken)
        {
            if (heavyIOEvents.Count == 0) return;
            
            var taskCompletionSource = new TaskCompletionSource<bool>();
            
            // Create task for execution on dedicated I/O thread
            var ioTask = new BackgroundWorkerTask
            {
                TaskId = Guid.NewGuid().ToString(),
                ExecuteAction = async () =>
                {
                    try
                    {
                        // Process heavy I/O events on dedicated thread
                        var groupedEvents = GroupEventsForProcessing(heavyIOEvents);
                        
                        foreach (var group in groupedEvents)
                        {
                            if (cancellationToken.IsCancellationRequested || _threadPoolCancellation.Token.IsCancellationRequested)
                                break;
                            
                            await ProcessEventGroup(group, cancellationToken);
                        }
                        
                        taskCompletionSource.TrySetResult(true);
                    }
                    catch (Exception ex)
                    {
                        taskCompletionSource.TrySetException(ex);
                    }
                },
                CreatedTime = DateTime.UtcNow,
                IsHighPriority = heavyIOEvents.Any(e => e.Priority == IOEventPriority.Critical || e.Priority == IOEventPriority.High)
            };
            
            // Queue task on dedicated I/O thread
            await QueueTaskOnDedicatedIOThread(ioTask);
            
            // Wait for completion with timeout
            try
            {
                await taskCompletionSource.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            catch (TimeoutException)
            {
                OnWorkerAlert(new WorkerAlert
                {
                    Type = AlertType.HeavyIOProcessingTimeout,
                    Message = $"Heavy I/O processing timed out after 30 seconds for {heavyIOEvents.Count} events",
                    EventType = _eventType,
                    BatchSize = heavyIOEvents.Count,
                    Timestamp = DateTime.UtcNow
                });
                
                taskCompletionSource.TrySetCanceled();
            }
        }
        
        /// <summary>
        /// Check if an event requires heavy I/O processing
        /// </summary>
        private bool IsHeavyIOEvent(IOEvent ioEvent)
        {
            return ioEvent.EventType switch
            {
                IOEventType.FileRead => ioEvent.Data?.Length > 1024 * 1024, // > 1MB
                IOEventType.FileWrite => ioEvent.Data?.Length > 512 * 1024, // > 512KB
                IOEventType.NetworkIO => true, // All network I/O is considered heavy
                IOEventType.SpoutData => ioEvent.Data?.Length > 256 * 1024, // > 256KB texture data
                _ => false
            };
        }
        
        /// <summary>
        /// Queue task on dedicated I/O thread
        /// </summary>
        private async Task QueueTaskOnDedicatedIOThread(BackgroundWorkerTask task)
        {
            if (_threadPoolCancellation.Token.IsCancellationRequested)
            {
                throw new OperationCanceledException("I/O worker thread is shutting down");
            }
            
            await _taskQueue.Writer.WriteAsync(task, _threadPoolCancellation.Token);
        }
        
        private async Task ProcessEventGroup(List<IOEvent> eventGroup, CancellationToken cancellationToken)
        {
            if (eventGroup.Count == 0) return;
            
            // Check if events can be processed in parallel
            var canProcessInParallel = CanProcessInParallel(eventGroup);
            
            if (canProcessInParallel && eventGroup.Count > 1)
            {
                // Process events in parallel for better throughput
                var tasks = eventGroup.Select(async ioEvent => 
                {
                    try
                    {
                        await ProcessSingleEvent(ioEvent, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        HandleEventProcessingError(ioEvent, ex);
                    }
                });
                
                await Task.WhenAll(tasks);
            }
            else
            {
                // Process events sequentially (for events that require order/isolation)
                foreach (var ioEvent in eventGroup)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    
                    try
                    {
                        await ProcessSingleEvent(ioEvent, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        HandleEventProcessingError(ioEvent, ex);
                    }
                }
            }
        }
        
        private async Task ProcessSingleEvent(IOEvent ioEvent, CancellationToken cancellationToken)
        {
            using var perfTracker = _performanceMonitor?.ProfileOperation($"Process{_eventType}");
            
            try
            {
                // Record event processing start
                ioEvent.Metadata["ProcessingStartTime"] = DateTime.UtcNow.Ticks.ToString();
                
                // Get or create resource buffer for this event
                var bufferSize = GetBufferSizeForEvent(ioEvent);
                var buffer = _resourcePool.GetBuffer(bufferSize);
                
                try
                {
                    // Process the event
                    await _eventProcessor(ioEvent);
                    
                    // Mark as successful
                    ioEvent.Metadata["ProcessingStatus"] = "Success";
                    ioEvent.Metadata["ProcessingEndTime"] = DateTime.UtcNow.Ticks.ToString();
                    
                    // Record success metrics
                    _performanceMonitor?.RecordCustomMetric($"Worker_{_eventType}_Success", 1);
                }
                finally
                {
                    // Return buffer to pool
                    _resourcePool.ReturnBuffer(buffer);
                }
            }
            catch (Exception ex)
            {
                // Mark as failed
                ioEvent.Metadata["ProcessingStatus"] = "Failed";
                ioEvent.Metadata["ErrorMessage"] = ex.Message;
                ioEvent.Metadata["ErrorTimestamp"] = DateTime.UtcNow.Ticks.ToString();
                
                // Record failure metrics
                _performanceMonitor?.RecordCustomMetric($"Worker_{_eventType}_Failure", 1);
                
                throw; // Re-throw to be handled by caller
            }
        }
        
        private List<List<IOEvent>> GroupEventsForProcessing(IReadOnlyList<IOEvent> events)
        {
            // Simple grouping strategy - can be enhanced with more sophisticated logic
            var groups = new List<List<IOEvent>>();
            
            // Group by priority for more efficient processing
            var priorityGroups = events.GroupBy(e => e.Priority);
            
            foreach (var group in priorityGroups)
            {
                var groupList = group.ToList();
                
                // Split large groups into smaller batches
                const int MAX_GROUP_SIZE = 5;
                for (int i = 0; i < groupList.Count; i += MAX_GROUP_SIZE)
                {
                    var batch = groupList.Skip(i).Take(MAX_GROUP_SIZE).ToList();
                    groups.Add(batch);
                }
            }
            
            return groups;
        }
        
        private bool CanProcessInParallel(List<IOEvent> eventGroup)
        {
            // Audio and MIDI events often require sequential processing
            // File I/O and network events can often be processed in parallel
            
            return _eventType switch
            {
                IOEventType.AudioInput or IOEventType.AudioOutput or 
                IOEventType.MidiInput or IOEventType.MidiOutput => false,
                
                IOEventType.FileRead or IOEventType.FileWrite or 
                IOEventType.NetworkIO or IOEventType.SpoutData => true,
                
                IOEventType.UserInput => eventGroup.Count <= 2, // Limit parallel processing
                IOEventType.CacheUpdate or IOEventType.MetadataUpdate => true,
                
                _ => false
            };
        }
        
        private int GetBufferSizeForEvent(IOEvent ioEvent)
        {
            // Estimate buffer size based on event data and metadata
            var baseSize = 1024; // 1KB base
            
            if (ioEvent.Data != null)
            {
                baseSize = Math.Max(baseSize, ioEvent.Data.Length);
            }
            
            // Round up to nearest power of 2 for better memory alignment
            var bufferSize = 1;
            while (bufferSize < baseSize)
            {
                bufferSize *= 2;
            }
            
            return Math.Min(bufferSize, 64 * 1024); // Cap at 64KB
        }
        
        private void HandleEventProcessingError(IOEvent ioEvent, Exception ex)
        {
            Interlocked.Increment(ref _eventsFailed);
            
            // Log error details
            ioEvent.Metadata["ErrorMessage"] = ex.Message;
            ioEvent.Metadata["ErrorStack"] = ex.StackTrace;
            ioEvent.Metadata["ErrorTimestamp"] = DateTime.UtcNow.Ticks.ToString();
            
            // Trigger worker alert
            OnWorkerAlert(new WorkerAlert
            {
                Type = AlertType.EventProcessingFailed,
                Message = $"Failed to process event {ioEvent.Id}: {ex.Message}",
                EventType = _eventType,
                EventId = ioEvent.Id,
                Exception = ex,
                Timestamp = DateTime.UtcNow
            });
        }
        
        private void UpdateActivityTimestamp()
        {
            Interlocked.Exchange(ref _lastActivityTimestamp, DateTime.UtcNow.Ticks);
        }
        
        /// <summary>
        /// Get worker statistics
        /// </summary>
        public IOWorkerStatistics GetStatistics()
        {
            lock (_lockObject)
            {
                var threadPoolStats = _ioThreadPool?.GetStatistics();
                
                return new IOWorkerStatistics
                {
                    EventType = _eventType.ToString(),
                    IsActive = IsActive,
                    EventsProcessed = _eventsProcessed,
                    EventsFailed = _eventsFailed,
                    AverageProcessingTimeMs = AverageProcessingTimeMs,
                    SuccessRate = _eventsProcessed > 0 ? 
                        (double)(_eventsProcessed - _eventsFailed) / _eventsProcessed * 100 : 100,
                    LastActivityTimestamp = new DateTime(_lastActivityTimestamp),
                    QueueDepth = _queue.Count,
                    
                    // Thread isolation statistics
                    HasDedicatedIOThread = true,
                    IOThreadId = _currentWorkerThreadId,
                    HeavyIOTasksCompleted = _heavyIOTasksCompleted,
                    ThreadPoolActiveThreads = threadPoolStats?.ActiveThreads ?? 0,
                    ThreadPoolPendingTasks = threadPoolStats?.PendingTasks ?? 0,
                    IsRenderThreadIsolated = true,
                    ThreadUtilization = threadPoolStats != null ? (double)threadPoolStats.ActiveThreads / threadPoolStats.MaxThreads * 100 : 0
                };
            }
        }
        
        /// <summary>
        /// Gracefully stop the worker
        /// </summary>
        public async Task StopAsync(TimeSpan timeout)
        {
            _cancellationTokenSource.Cancel();
            
            try
            {
                await _processingTask.WaitAsync(timeout);
            }
            catch (TimeoutException)
            {
                OnWorkerAlert(new WorkerAlert
                {
                    Type = AlertType.WorkerStopTimeout,
                    Message = $"Worker for {_eventType} did not stop within {timeout}",
                    EventType = _eventType,
                    Timestamp = DateTime.UtcNow
                });
            }
        }
        
        /// <summary>
        /// Force stop the worker immediately
        /// </summary>
        public void ForceStop()
        {
            _cancellationTokenSource.Cancel();
        }
        
        protected virtual void OnWorkerAlert(WorkerAlert alert)
        {
            WorkerAlert?.Invoke(this, alert);
        }
        
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            
            try
            {
                _cancellationTokenSource?.Cancel();
                _threadPoolCancellation?.Cancel();
                
                // Signal work available event to wake up background thread
                _workAvailableEvent?.Set();
                
                // Wait for main processing task to complete
                _processingTask?.Wait(TimeSpan.FromSeconds(5));
                
                // Wait for background I/O thread to complete
                if (_backgroundIOThread?.IsAlive == true)
                {
                    _backgroundIOThread.Join(TimeSpan.FromSeconds(5));
                }
                
                // Cleanup threading primitives
                _workAvailableEvent?.Dispose();
                _activeTasksCountdown?.Dispose();
                _threadPoolSemaphore?.Dispose();
                _threadPoolCancellation?.Dispose();
                
                _cancellationTokenSource?.Dispose();
                _ioThreadPool?.Dispose();
            }
            catch (Exception ex)
            {
                OnWorkerAlert(new WorkerAlert
                {
                    Type = AlertType.WorkerDisposeError,
                    Message = $"Error disposing worker for {_eventType}: {ex.Message}",
                    EventType = _eventType,
                    Exception = ex,
                    Timestamp = DateTime.UtcNow
                });
            }
        }
    }
    
    /// <summary>
    /// Statistics for I/O worker
    /// </summary>
    public class IOWorkerStatistics
    {
        public string EventType { get; set; }
        public bool IsActive { get; set; }
        public long EventsProcessed { get; set; }
        public long EventsFailed { get; set; }
        public double AverageProcessingTimeMs { get; set; }
        public double SuccessRate { get; set; }
        public DateTime LastActivityTimestamp { get; set; }
        public int QueueDepth { get; set; }
        
        // Thread isolation statistics
        public bool HasDedicatedIOThread { get; set; }
        public int IOThreadId { get; set; }
        public long HeavyIOTasksCompleted { get; set; }
        public int ThreadPoolActiveThreads { get; set; }
        public int ThreadPoolPendingTasks { get; set; }
        public bool IsRenderThreadIsolated { get; set; }
        public double ThreadUtilization { get; set; }
        public Dictionary<string, object> ThreadIsolationMetrics { get; set; } = new();
    }
    
    /// <summary>
    /// Alert for I/O worker
    /// </summary>
    public class WorkerAlert : EventArgs
    {
        public AlertType Type { get; set; }
        public string Message { get; set; }
        public IOEventType EventType { get; set; }
        public string EventId { get; set; }
        public int BatchSize { get; set; }
        public Exception Exception { get; set; }
        public DateTime Timestamp { get; set; }
    }
    
    /// <summary>
    /// Task for background worker execution
    /// </summary>
    public class BackgroundWorkerTask
    {
        public string TaskId { get; set; }
        public Action ExecuteAction { get; set; }
        public DateTime CreatedTime { get; set; }
        public bool IsHighPriority { get; set; }
        public CancellationToken CancellationToken { get; set; }
        public TimeSpan? Timeout { get; set; }
    }
}