using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace TiXL.Core.IO
{
    /// <summary>
    /// Monitors I/O operations for performance tracking and debugging
    /// </summary>
    internal interface IOperationMonitor
    {
        IDisposable StartOperation(string operationName);
        void RecordSuccess();
        void RecordFailure(string error);
        void RecordMetadata(string key, string value);
        void Dispose();
    }
    
    /// <summary>
    /// Implementation of IOperationMonitor for tracking I/O operations
    /// </summary>
    internal class OperationMonitor : IOperationMonitor
    {
        private readonly ConcurrentQueue<OperationRecord> _operationHistory;
        private readonly ConcurrentDictionary<string, int> _operationCounts;
        private readonly object _lock = new();
        private const int MaxHistorySize = 10000;
        
        private OperationRecord _currentOperation;
        private long _totalOperations;
        private long _successfulOperations;
        private long _failedOperations;
        private long _totalBytesRead;
        private long _totalBytesWritten;
        private TimeSpan _totalDuration;
        
        public OperationMonitor()
        {
            _operationHistory = new ConcurrentQueue<OperationRecord>();
            _operationCounts = new ConcurrentDictionary<string, int>();
        }
        
        public IDisposable StartOperation(string operationName)
        {
            return new OperationTracker(this, operationName);
        }
        
        public void RecordSuccess()
        {
            if (_currentOperation != null)
            {
                _currentOperation.IsSuccess = true;
                Interlocked.Increment(ref _successfulOperations);
                CompleteOperation();
            }
        }
        
        public void RecordFailure(string error)
        {
            if (_currentOperation != null)
            {
                _currentOperation.IsSuccess = false;
                _currentOperation.ErrorMessage = error;
                Interlocked.Increment(ref _failedOperations);
                CompleteOperation();
            }
        }
        
        public void RecordMetadata(string key, string value)
        {
            if (_currentOperation?.Metadata != null)
            {
                _currentOperation.Metadata[key] = value;
            }
        }
        
        public IOStatistics GetStatistics()
        {
            return new IOStatistics
            {
                TotalOperations = (int)_totalOperations,
                SuccessfulOperations = (int)_successfulOperations,
                FailedOperations = (int)_failedOperations,
                TotalDuration = _totalDuration,
                AverageOperationTime = _totalOperations > 0 ? 
                    TimeSpan.FromTicks(_totalDuration.Ticks / _totalOperations) : TimeSpan.Zero,
                TotalBytesRead = _totalBytesRead,
                TotalBytesWritten = _totalBytesWritten,
                OperationCounts = _operationCounts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };
        }
        
        public List<OperationRecord> GetRecentOperations(int maxRecords)
        {
            lock (_lock)
            {
                return _operationHistory.Take(maxRecords).ToList();
            }
        }
        
        private void StartOperationInternal(string operationName)
        {
            _currentOperation = new OperationRecord
            {
                OperationName = operationName,
                StartTime = DateTime.UtcNow,
                Metadata = new Dictionary<string, string>()
            };
            
            Interlocked.Increment(ref _totalOperations);
            _operationCounts.AddOrUpdate(operationName, 1, (key, value) => value + 1);
        }
        
        private void CompleteOperation()
        {
            if (_currentOperation == null) return;
            
            _currentOperation.EndTime = DateTime.UtcNow;
            var duration = _currentOperation.Duration;
            
            // Update statistics
            _totalDuration += duration;
            
            // Add to history (with thread-safe locking for bounded queue)
            lock (_lock)
            {
                _operationHistory.Enqueue(_currentOperation);
                
                // Trim history if too large
                while (_operationHistory.Count > MaxHistorySize)
                {
                    _operationHistory.TryDequeue(out _);
                }
            }
            
            _currentOperation = null;
        }
        
        public void RecordBytesRead(long bytes)
        {
            Interlocked.Add(ref _totalBytesRead, bytes);
        }
        
        public void RecordBytesWritten(long bytes)
        {
            Interlocked.Add(ref _totalBytesWritten, bytes);
        }
        
        public void Dispose()
        {
            // No final cleanup needed as we're tracking in memory
            // This interface allows for future extensibility with persistent storage
        }
        
        private class OperationTracker : IDisposable
        {
            private readonly OperationMonitor _monitor;
            private readonly string _operationName;
            private readonly Stopwatch _stopwatch;
            private bool _disposed;
            
            public OperationTracker(OperationMonitor monitor, string operationName)
            {
                _monitor = monitor;
                _operationName = operationName;
                _stopwatch = Stopwatch.StartNew();
                
                _monitor.StartOperationInternal(operationName);
            }
            
            public void Dispose()
            {
                if (_disposed) return;
                
                _stopwatch.Stop();
                
                try
                {
                    // The monitor will handle the completion
                    // This tracker just provides the timing
                }
                finally
                {
                    _disposed = true;
                }
            }
        }
    }
}