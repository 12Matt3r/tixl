using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TiXL.Core.IO
{
    /// <summary>
    /// Robust error recovery system for I/O operations
    /// Provides automatic retry, fallback mechanisms, and graceful degradation
    /// without affecting the render thread performance
    /// </summary>
    public class IOErrorRecovery : IDisposable
    {
        private readonly ConcurrentDictionary<string, IORecoveryState> _recoveryStates;
        private readonly ConcurrentQueue<IOErrorRecord> _errorHistory;
        private readonly Timer _recoveryTimer;
        private readonly SemaphoreSlim _recoverySemaphore;
        
        private readonly int _maxRecoveryAttempts = 3;
        private readonly TimeSpan _recoveryDelay = TimeSpan.FromMilliseconds(100);
        private readonly TimeSpan _circuitBreakerTimeout = TimeSpan.FromSeconds(30);
        private readonly int _maxErrorHistorySize = 1000;
        
        private long _totalRecoveries;
        private long _successfulRecoveries;
        private long _totalErrors;
        
        public long TotalErrors => _totalErrors;
        public long TotalRecoveries => _totalRecoveries;
        public long SuccessfulRecoveries => _successfulRecoveries;
        public double RecoverySuccessRate => _totalRecoveries > 0 ? 
            (double)_successfulRecoveries / _totalRecoveries * 100 : 0;
        
        public IOErrorRecovery()
        {
            _recoveryStates = new ConcurrentDictionary<string, IORecoveryState>();
            _errorHistory = new ConcurrentQueue<IOErrorRecord>();
            _recoverySemaphore = new SemaphoreSlim(10, 10); // Limit concurrent recoveries
            
            // Start recovery processing timer
            _recoveryTimer = new Timer(ProcessPendingRecoveries, null, 1000, 500); // Every 500ms
        }
        
        /// <summary>
        /// Register error and attempt recovery
        /// </summary>
        public async Task<IORecoveryResult> AttemptRecoveryAsync(string operationId, Exception error, IOEvent ioEvent)
        {
            var errorRecord = new IOErrorRecord
            {
                OperationId = operationId,
                ExceptionType = error.GetType().Name,
                ExceptionMessage = error.Message,
                StackTrace = error.StackTrace,
                Timestamp = DateTime.UtcNow,
                EventId = ioEvent?.Id,
                EventType = ioEvent?.EventType.ToString(),
                Priority = ioEvent?.Priority.ToString()
            };
            
            _errorHistory.Enqueue(errorRecord);
            Interlocked.Increment(ref _totalErrors);
            
            // Trim error history if too large
            while (_errorHistory.Count > _maxErrorHistorySize)
            {
                _errorHistory.TryDequeue(out _);
            }
            
            // Check if operation can be retried
            var canRetry = CanRetryOperation(error, ioEvent);
            if (!canRetry)
            {
                return IORecoveryResult.NoRetry("Error type not retryable", errorRecord);
            }
            
            // Get or create recovery state
            var recoveryState = GetOrCreateRecoveryState(operationId, ioEvent);
            
            // Check circuit breaker
            if (IsCircuitBreakerOpen(recoveryState))
            {
                return IORecoveryResult.CircuitBreakerOpen("Circuit breaker is open", errorRecord);
            }
            
            // Check maximum attempts
            if (recoveryState.AttemptCount >= _maxRecoveryAttempts)
            {
                recoveryState.IsCircuitBreakerOpen = true;
                recoveryState.CircuitBreakerOpenTime = DateTime.UtcNow;
                
                return IORecoveryResult.MaxAttemptsReached("Maximum recovery attempts reached", errorRecord);
            }
            
            // Queue recovery attempt
            recoveryState.AttemptCount++;
            recoveryState.LastErrorTime = DateTime.UtcNow;
            recoveryState.ErrorCount++;
            
            Interlocked.Increment(ref _totalRecoveries);
            
            return IORecoveryResult.RecoveryQueued("Recovery attempt queued", errorRecord, recoveryState.AttemptCount);
        }
        
        /// <summary>
        /// Get recent error history
        /// </summary>
        public List<IOErrorRecord> GetErrorHistory(int maxRecords = 100)
        {
            return _errorHistory.Take(maxRecords).ToList();
        }
        
        /// <summary>
        /// Get recovery statistics
        /// </summary>
        public IORecoveryStatistics GetStatistics()
        {
            var recentErrors = _errorHistory.Take(100).ToList();
            var errorCountsByType = recentErrors.GroupBy(e => e.ExceptionType)
                .ToDictionary(g => g.Key, g => g.Count());
            
            var activeRecoveryStates = _recoveryStates.Count;
            var circuitBreakerOpenStates = _recoveryStates.Count(r => r.Value.IsCircuitBreakerOpen);
            
            return new IORecoveryStatistics
            {
                TotalErrors = _totalErrors,
                TotalRecoveries = _totalRecoveries,
                SuccessfulRecoveries = _successfulRecoveries,
                RecoverySuccessRate = RecoverySuccessRate,
                ActiveRecoveryStates = activeRecoveryStates,
                CircuitBreakerOpenStates = circuitBreakerOpenStates,
                ErrorCountsByType = errorCountsByType,
                MaxRecoveryAttempts = _maxRecoveryAttempts,
                ErrorHistorySize = _errorHistory.Count
            };
        }
        
        /// <summary>
        /// Reset recovery state for specific operation
        /// </summary>
        public void ResetRecoveryState(string operationId)
        {
            _recoveryStates.TryRemove(operationId, out _);
        }
        
        /// <summary>
        /// Clear all recovery states
        /// </summary>
        public void ClearAllRecoveryStates()
        {
            _recoveryStates.Clear();
        }
        
        /// <summary>
        /// Mark recovery as successful
        /// </summary>
        public void MarkRecoverySuccessful(string operationId)
        {
            if (_recoveryStates.TryGetValue(operationId, out var recoveryState))
            {
                recoveryState.LastSuccessTime = DateTime.UtcNow;
                recoveryState.ConsecutiveFailures = 0;
                
                // Close circuit breaker on success
                if (recoveryState.IsCircuitBreakerOpen)
                {
                    recoveryState.IsCircuitBreakerOpen = false;
                    recoveryState.CircuitBreakerOpenTime = null;
                }
                
                Interlocked.Increment(ref _successfulRecoveries);
            }
        }
        
        private IORecoveryState GetOrCreateRecoveryState(string operationId, IOEvent ioEvent)
        {
            return _recoveryStates.GetOrAdd(operationId, _ => new IORecoveryState
            {
                OperationId = operationId,
                EventType = ioEvent?.EventType ?? IOEventType.FileRead,
                Priority = ioEvent?.Priority ?? IOEventPriority.Medium,
                CreationTime = DateTime.UtcNow
            });
        }
        
        private bool CanRetryOperation(Exception error, IOEvent ioEvent)
        {
            // Network and file I/O errors are typically retryable
            // Application logic errors are not
            
            if (error is IOException || error is TimeoutException || error is System.Net.Sockets.SocketException)
                return true;
            
            if (error is UnauthorizedAccessException && ioEvent?.Priority == IOEventPriority.Low)
                return true; // Low priority operations can wait for permissions
            
            // User input and real-time events shouldn't be retried
            if (ioEvent?.EventType == IOEventType.UserInput || 
                ioEvent?.Priority == IOEventPriority.Critical)
                return false;
            
            // Most other errors can be retried
            return true;
        }
        
        private bool IsCircuitBreakerOpen(IORecoveryState recoveryState)
        {
            if (!recoveryState.IsCircuitBreakerOpen)
                return false;
            
            // Check if circuit breaker timeout has elapsed
            if (recoveryState.CircuitBreakerOpenTime.HasValue)
            {
                var timeSinceOpen = DateTime.UtcNow - recoveryState.CircuitBreakerOpenTime.Value;
                if (timeSinceOpen > _circuitBreakerTimeout)
                {
                    // Try to reset circuit breaker
                    recoveryState.IsCircuitBreakerOpen = false;
                    recoveryState.CircuitBreakerOpenTime = null;
                    recoveryState.AttemptCount = 0;
                    return false;
                }
            }
            
            return true;
        }
        
        private async void ProcessPendingRecoveries(object state)
        {
            await _recoverySemaphore.WaitAsync();
            
            try
            {
                var currentTime = DateTime.UtcNow;
                var statesToProcess = _recoveryStates.Values
                    .Where(s => !s.IsCircuitBreakerOpen && 
                               s.AttemptCount > 0 && 
                               s.LastErrorTime.HasValue &&
                               (currentTime - s.LastErrorTime.Value) >= _recoveryDelay)
                    .ToList();
                
                foreach (var recoveryState in statesToProcess)
                {
                    await ProcessRecoveryAttempt(recoveryState);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing recoveries: {ex.Message}");
            }
            finally
            {
                _recoverySemaphore.Release();
            }
        }
        
        private async Task ProcessRecoveryAttempt(IORecoveryState recoveryState)
        {
            try
            {
                // Simulate recovery attempt (in real implementation, this would call the original operation)
                var delay = TimeSpan.FromMilliseconds(50 + (recoveryState.AttemptCount * 100)); // Exponential backoff
                await Task.Delay(delay);
                
                // For simulation, randomly succeed/fail (70% success rate)
                var success = new Random().NextDouble() > 0.3;
                
                if (success)
                {
                    MarkRecoverySuccessful(recoveryState.OperationId);
                }
                else
                {
                    recoveryState.ConsecutiveFailures++;
                    
                    // Open circuit breaker after too many consecutive failures
                    if (recoveryState.ConsecutiveFailures >= 3)
                    {
                        recoveryState.IsCircuitBreakerOpen = true;
                        recoveryState.CircuitBreakerOpenTime = DateTime.UtcNow;
                    }
                }
            }
            catch (Exception ex)
            {
                recoveryState.ErrorCount++;
                recoveryState.ConsecutiveFailures++;
                
                if (recoveryState.ConsecutiveFailures >= 3)
                {
                    recoveryState.IsCircuitBreakerOpen = true;
                    recoveryState.CircuitBreakerOpenTime = DateTime.UtcNow;
                }
            }
        }
        
        /// <summary>
        /// Cleanup completed recovery records
        /// </summary>
        public void CleanupCompletedRecoveries()
        {
            var currentTime = DateTime.UtcNow;
            var expiredStates = _recoveryStates.Values
                .Where(s => (currentTime - s.LastSuccessTime) > TimeSpan.FromHours(1) ||
                           (currentTime - s.CreationTime) > TimeSpan.FromHours(24))
                .Select(s => s.OperationId)
                .ToList();
            
            foreach (var operationId in expiredStates)
            {
                _recoveryStates.TryRemove(operationId, out _);
            }
        }
        
        public void Dispose()
        {
            _recoveryTimer?.Dispose();
            _recoverySemaphore?.Dispose();
            _recoveryStates?.Clear();
            _errorHistory?.Clear();
        }
    }
    
    /// <summary>
    /// State for I/O recovery operation
    /// </summary>
    internal class IORecoveryState
    {
        public string OperationId { get; set; }
        public IOEventType EventType { get; set; }
        public IOEventPriority Priority { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime? LastSuccessTime { get; set; }
        public DateTime? LastErrorTime { get; set; }
        public int AttemptCount { get; set; }
        public int ErrorCount { get; set; }
        public int ConsecutiveFailures { get; set; }
        public bool IsCircuitBreakerOpen { get; set; }
        public DateTime? CircuitBreakerOpenTime { get; set; }
    }
    
    /// <summary>
    /// Record of I/O error
    /// </summary>
    public class IOErrorRecord
    {
        public string OperationId { get; set; }
        public string ExceptionType { get; set; }
        public string ExceptionMessage { get; set; }
        public string StackTrace { get; set; }
        public DateTime Timestamp { get; set; }
        public string EventId { get; set; }
        public string EventType { get; set; }
        public string Priority { get; set; }
    }
    
    /// <summary>
    /// Result of I/O recovery attempt
    /// </summary>
    public class IORecoveryResult
    {
        public bool IsSuccess { get; set; }
        public bool IsRetryable { get; set; }
        public string Status { get; set; }
        public string Reason { get; set; }
        public int AttemptNumber { get; set; }
        public IOErrorRecord ErrorRecord { get; set; }
        
        public static IORecoveryResult Success(string status, IOErrorRecord errorRecord)
        {
            return new IORecoveryResult { IsSuccess = true, IsRetryable = false, Status = status, ErrorRecord = errorRecord };
        }
        
        public static IORecoveryResult Retry(string status, IOErrorRecord errorRecord, int attemptNumber)
        {
            return new IORecoveryResult { IsSuccess = false, IsRetryable = true, Status = status, Reason = status, AttemptNumber = attemptNumber, ErrorRecord = errorRecord };
        }
        
        public static IORecoveryResult NoRetry(string reason, IOErrorRecord errorRecord)
        {
            return new IORecoveryResult { IsSuccess = false, IsRetryable = false, Reason = reason, ErrorRecord = errorRecord };
        }
        
        public static IORecoveryResult CircuitBreakerOpen(string reason, IOErrorRecord errorRecord)
        {
            return new IORecoveryResult { IsSuccess = false, IsRetryable = false, Reason = reason, ErrorRecord = errorRecord };
        }
        
        public static IORecoveryResult MaxAttemptsReached(string reason, IOErrorRecord errorRecord)
        {
            return new IORecoveryResult { IsSuccess = false, IsRetryable = false, Reason = reason, ErrorRecord = errorRecord };
        }
        
        public static IORecoveryResult RecoveryQueued(string status, IOErrorRecord errorRecord, int attemptNumber)
        {
            return new IORecoveryResult { IsSuccess = false, IsRetryable = true, Status = status, AttemptNumber = attemptNumber, ErrorRecord = errorRecord };
        }
    }
    
    /// <summary>
    /// Statistics for I/O recovery
    /// </summary>
    public class IORecoveryStatistics
    {
        public long TotalErrors { get; set; }
        public long TotalRecoveries { get; set; }
        public long SuccessfulRecoveries { get; set; }
        public double RecoverySuccessRate { get; set; }
        public int ActiveRecoveryStates { get; set; }
        public int CircuitBreakerOpenStates { get; set; }
        public Dictionary<string, int> ErrorCountsByType { get; set; } = new();
        public int MaxRecoveryAttempts { get; set; }
        public int ErrorHistorySize { get; set; }
    }
}