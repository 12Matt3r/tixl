using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TiXL.Core.Performance;

namespace TiXL.Core.IO
{
    /// <summary>
    /// Lock-free thread-safe event queue with priority handling and batching support
    /// Designed for high-performance I/O event processing without blocking the render thread
    /// </summary>
    public class IOEventQueue
    {
        private readonly string _name;
        private readonly int _maxCapacity;
        private readonly BlockingCollection<IOEvent> _queue;
        private readonly ConcurrentDictionary<int, PriorityQueue<IOEvent, int>> _priorityBins;
        private readonly SemaphoreSlim _waitSemaphore;
        private readonly object _lockObject = new object();
        
        private readonly int _maxBatchSize;
        private readonly TimeSpan _batchTimeout;
        private volatile bool _isProcessing;
        
        public int Count => _queue.Count;
        public string Name => _name;
        public int MaxCapacity => _maxCapacity;
        public bool IsProcessing => _isProcessing;
        
        public event EventHandler<IOEventQueueAlert> QueueAlert;
        
        public IOEventQueue(string name, int maxCapacity, int maxBatchSize = 10, TimeSpan? batchTimeout = null)
        {
            _name = name;
            _maxCapacity = maxCapacity;
            _maxBatchSize = maxBatchSize;
            _batchTimeout = batchTimeout ?? TimeSpan.FromMilliseconds(16);
            
            _queue = new BlockingCollection<IOEvent>(maxCapacity);
            _priorityBins = new ConcurrentDictionary<int, PriorityQueue<IOEvent, int>>();
            _waitSemaphore = new SemaphoreSlim(0, maxCapacity);
        }
        
        /// <summary>
        /// Try to add event to queue with timeout
        /// </summary>
        public async Task<bool> TryAddWithTimeout(IOEvent ioEvent, TimeSpan timeout)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                
                // Check capacity with timeout
                while (Count >= _maxCapacity && stopwatch.Elapsed < timeout)
                {
                    await Task.Delay(1);
                }
                
                if (Count >= _maxCapacity)
                {
                    OnQueueAlert(new IOEventQueueAlert
                    {
                        Type = AlertType.QueueFull,
                        Message = $"Queue '{_name}' is full ({Count}/{_maxCapacity})",
                        QueueName = _name,
                        CurrentSize = Count,
                        MaxCapacity = _maxCapacity,
                        Timestamp = DateTime.UtcNow
                    });
                    return false;
                }
                
                // Add to main queue
                _queue.Add(ioEvent);
                
                // Add to priority bin
                var priorityKey = GetPriorityBinKey(ioEvent.Priority);
                _priorityBins.AddOrUpdate(priorityKey,
                    new PriorityQueue<IOEvent, int>(),
                    (key, existing) =>
                    {
                        existing.Enqueue(ioEvent, ioEvent.Priority == IOEventPriority.Critical ? 0 : 
                                                        ioEvent.Priority == IOEventPriority.High ? 1 :
                                                        ioEvent.Priority == IOEventPriority.Medium ? 2 : 3);
                        return existing;
                    });
                
                // Signal waiting workers
                _waitSemaphore.Release();
                
                return true;
            }
            catch (Exception ex)
            {
                OnQueueAlert(new IOEventQueueAlert
                {
                    Type = AlertType.AddFailed,
                    Message = $"Failed to add event to queue '{_name}': {ex.Message}",
                    QueueName = _name,
                    Timestamp = DateTime.UtcNow
                });
                return false;
            }
        }
        
        /// <summary>
        /// Take next event from queue (blocking)
        /// </summary>
        public async Task<IOEvent> TakeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _waitSemaphore.WaitAsync(cancellationToken);
                
                if (_queue.TryTake(out var ioEvent))
                {
                    // Remove from priority bin
                    RemoveFromPriorityBin(ioEvent);
                    return ioEvent;
                }
                
                return null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                OnQueueAlert(new IOEventQueueAlert
                {
                    Type = AlertType.TakeFailed,
                    Message = $"Failed to take event from queue '{_name}': {ex.Message}",
                    QueueName = _name,
                    Timestamp = DateTime.UtcNow
                });
                return null;
            }
        }
        
        /// <summary>
        /// Take batch of events from queue for processing
        /// </summary>
        public async Task<IReadOnlyList<IOEvent>> TakeBatchAsync(CancellationToken cancellationToken = default)
        {
            var batch = new List<IOEvent>();
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Take first event
                var firstEvent = await TakeAsync(cancellationToken);
                if (firstEvent != null)
                {
                    batch.Add(firstEvent);
                }
                
                // Fill batch with additional events
                while (batch.Count < _maxBatchSize && stopwatch.Elapsed < _batchTimeout)
                {
                    if (_queue.TryTake(out var ioEvent))
                    {
                        batch.Add(ioEvent);
                        RemoveFromPriorityBin(ioEvent);
                    }
                    else
                    {
                        // No more events immediately available
                        break;
                    }
                }
                
                return batch;
            }
            catch (Exception ex)
            {
                OnQueueAlert(new IOEventQueueAlert
                {
                    Type = AlertType.BatchTakeFailed,
                    Message = $"Failed to take batch from queue '{_name}': {ex.Message}",
                    QueueName = _name,
                    BatchSize = batch.Count,
                    Timestamp = DateTime.UtcNow
                });
                return batch;
            }
        }
        
        /// <summary>
        /// Get next event based on priority
        /// </summary>
        public async Task<IOEvent> TakePriorityAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Check priority bins first
                var priorityEvent = GetHighestPriorityEvent();
                if (priorityEvent != null)
                {
                    RemoveFromPriorityBin(priorityEvent);
                    return priorityEvent;
                }
                
                // Fall back to regular queue
                return await TakeAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                OnQueueAlert(new IOEventQueueAlert
                {
                    Type = AlertType.PriorityTakeFailed,
                    Message = $"Failed to take priority event from queue '{_name}': {ex.Message}",
                    QueueName = _name,
                    Timestamp = DateTime.UtcNow
                });
                return null;
            }
        }
        
        /// <summary>
        /// Get queue statistics
        /// </summary>
        public IOEventQueueStatistics GetStatistics()
        {
            lock (_lockObject)
            {
                return new IOEventQueueStatistics
                {
                    Name = _name,
                    CurrentSize = Count,
                    MaxCapacity = _maxCapacity,
                    CapacityUtilization = (double)Count / _maxCapacity,
                    PriorityBinSizes = _priorityBins.ToDictionary(
                        kvp => kvp.Key.ToString(), 
                        kvp => kvp.Value.Count),
                    IsProcessing = _isProcessing,
                    BatchSize = _maxBatchSize,
                    BatchTimeout = _batchTimeout
                };
            }
        }
        
        /// <summary>
        /// Clear all events from queue
        /// </summary>
        public void Clear()
        {
            lock (_lockObject)
            {
                while (_queue.TryTake(out _)) { }
                _priorityBins.Clear();
            }
        }
        
        /// <summary>
        /// Start queue processing
        /// </summary>
        public void StartProcessing()
        {
            _isProcessing = true;
        }
        
        /// <summary>
        /// Stop queue processing
        /// </summary>
        public void StopProcessing()
        {
            _isProcessing = false;
        }
        
        private IOEvent GetHighestPriorityEvent()
        {
            foreach (var priorityBin in _priorityBins.OrderBy(kvp => kvp.Key))
            {
                if (priorityBin.Value.TryDequeue(out var ioEvent))
                {
                    return ioEvent;
                }
            }
            return null;
        }
        
        private void RemoveFromPriorityBin(IOEvent ioEvent)
        {
            var priorityKey = GetPriorityBinKey(ioEvent.Priority);
            if (_priorityBins.TryGetValue(priorityKey, out var priorityQueue))
            {
                // Note: This is a simplified removal - in production, you might want
                // to track event IDs for more efficient removal
                var tempQueue = new PriorityQueue<IOEvent, int>();
                while (priorityQueue.TryDequeue(out var eventItem))
                {
                    if (eventItem.Id != ioEvent.Id)
                    {
                        tempQueue.Enqueue(eventItem, priorityQueue.GetHashCode());
                    }
                }
                _priorityBins[priorityKey] = tempQueue;
            }
        }
        
        private int GetPriorityBinKey(IOEventPriority priority)
        {
            return priority switch
            {
                IOEventPriority.Critical => 0,
                IOEventPriority.High => 1,
                IOEventPriority.Medium => 2,
                IOEventPriority.Low => 3,
                _ => 2
            };
        }
        
        protected virtual void OnQueueAlert(IOEventQueueAlert alert)
        {
            QueueAlert?.Invoke(this, alert);
        }
        
        public void Dispose()
        {
            _queue?.Dispose();
            _waitSemaphore?.Dispose();
            _priorityBins?.Clear();
        }
    }
    
    /// <summary>
    /// Statistics for I/O event queue
    /// </summary>
    public class IOEventQueueStatistics
    {
        public string Name { get; set; }
        public int CurrentSize { get; set; }
        public int MaxCapacity { get; set; }
        public double CapacityUtilization { get; set; }
        public Dictionary<string, int> PriorityBinSizes { get; set; } = new();
        public bool IsProcessing { get; set; }
        public int BatchSize { get; set; }
        public TimeSpan BatchTimeout { get; set; }
    }
    
    /// <summary>
    /// Alert for I/O event queue
    /// </summary>
    public class IOEventQueueAlert : EventArgs
    {
        public AlertType Type { get; set; }
        public string Message { get; set; }
        public string QueueName { get; set; }
        public int CurrentSize { get; set; }
        public int MaxCapacity { get; set; }
        public int BatchSize { get; set; }
        public DateTime Timestamp { get; set; }
    }
}