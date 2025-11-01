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
        
        private volatile bool _isActive = true;
        private long _eventsProcessed;
        private long _eventsFailed;
        private long _totalProcessingTimeMs;
        private long _lastActivityTimestamp;
        
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
            
            // Start processing task
            _processingTask = Task.Run(ProcessEventsAsync, _cancellationTokenSource.Token);
            
            // Update activity timestamp
            UpdateActivityTimestamp();
            
            OnWorkerAlert(new WorkerAlert
            {
                Type = AlertType.WorkerStarted,
                Message = $"Worker for {_eventType} started",
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
                    
                    // Process batch
                    await ProcessEventBatch(events, cancellationToken);
                    
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
                const int maxGroupSize = 5;
                for (int i = 0; i < groupList.Count; i += maxGroupSize)
                {
                    var batch = groupList.Skip(i).Take(maxGroupSize).ToList();
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
                    QueueDepth = _queue.Count
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
            try
            {
                _cancellationTokenSource.Cancel();
                _processingTask?.Wait(1000); // Brief wait for graceful shutdown
                
                _cancellationTokenSource?.Dispose();
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
}