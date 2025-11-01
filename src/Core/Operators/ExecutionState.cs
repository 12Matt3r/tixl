using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace T3.Core.Operators
{
    /// <summary>
    /// Tracks the current execution state and enforces guardrail limits
    /// </summary>
    internal class ExecutionState : IDisposable
    {
        #region State Tracking

        private readonly GuardrailConfiguration _config;
        private readonly Stopwatch _executionStopwatch;
        private readonly Stack<string> _operationStack;
        private readonly ConcurrentDictionary<string, OperationMetrics> _operationMetrics;
        private readonly List<Exception> _exceptions;
        private int _operationCount;
        private int _concurrentOperationCount;
        private long _totalMemoryAllocated;
        private int _consecutiveViolations;
        private ExecutionMode _currentMode;
        private readonly object _lock = new();

        /// <summary>
        /// Gets whether the evaluation is currently within limits
        /// </summary>
        public bool IsWithinLimits => !HasExceededAnyLimit;

        /// <summary>
        /// Gets whether any limit has been exceeded
        /// </summary>
        public bool HasExceededAnyLimit { get; private set; }

        /// <summary>
        /// Gets the current execution mode (Normal, Safe, or Panic)
        /// </summary>
        public ExecutionMode CurrentMode => _currentMode;

        /// <summary>
        /// Gets the total number of operations executed
        /// </summary>
        public int TotalOperations => _operationCount;

        /// <summary>
        /// Gets the current operation stack depth
        /// </summary>
        public int CurrentRecursionDepth => _operationStack.Count;

        /// <summary>
        /// Gets the elapsed execution time
        /// </summary>
        public TimeSpan ElapsedTime => _executionStopwatch.Elapsed;

        /// <summary>
        /// Gets the total memory allocated during execution
        /// </summary>
        public long TotalMemoryAllocated => _totalMemoryAllocated;

        #endregion

        #region Constructor

        public ExecutionState(GuardrailConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _config.Validate();

            _executionStopwatch = Stopwatch.StartNew();
            _operationStack = new Stack<string>();
            _operationMetrics = new ConcurrentDictionary<string, OperationMetrics>();
            _exceptions = new List<Exception>();
            _currentMode = ExecutionMode.Normal;

            // Validate initial state
            ValidateConfiguration();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Validates that the current operation can proceed without violating limits
        /// </summary>
        public void ValidateCanProceed(string operationName)
        {
            using (AcquireLock())
            {
                // Check if we're in panic mode
                if (_currentMode == ExecutionMode.Panic)
                {
                    throw CreateGuardrailException($"Cannot proceed: execution is in panic mode");
                }

                // Check time limits
                if (_executionStopwatch.Elapsed > _config.MaxEvaluationDuration)
                {
                    RecordViolation("MaxEvaluationDuration", $"Total execution time {_executionStopwatch.Elapsed} exceeded limit {_config.MaxEvaluationDuration}");
                    HandleViolation("MaxEvaluationDuration");
                    throw CreateGuardrailException($"Maximum evaluation duration exceeded: {_executionStopwatch.Elapsed}");
                }

                // Check operation count limits
                if (_operationCount >= _config.MaxOperationsPerEvaluation)
                {
                    RecordViolation("MaxOperationsPerEvaluation", $"Operation count {_operationCount} exceeded limit {_config.MaxOperationsPerEvaluation}");
                    HandleViolation("MaxOperationsPerEvaluation");
                    throw CreateGuardrailException($"Maximum operations per evaluation exceeded: {_operationCount}");
                }

                // Check recursion depth
                if (_operationStack.Count >= _config.MaxRecursionDepth)
                {
                    RecordViolation("MaxRecursionDepth", $"Recursion depth {_operationStack.Count} exceeded limit {_config.MaxRecursionDepth}");
                    HandleViolation("MaxRecursionDepth");
                    throw CreateGuardrailException($"Maximum recursion depth exceeded: {_operationStack.Count}");
                }

                // Check concurrent operations
                if (_concurrentOperationCount >= _config.MaxConcurrentOperations)
                {
                    RecordViolation("MaxConcurrentOperations", $"Concurrent operations {_concurrentOperationCount} exceeded limit {_config.MaxConcurrentOperations}");
                    HandleViolation("MaxConcurrentOperations");
                    throw CreateGuardrailException($"Maximum concurrent operations exceeded: {_concurrentOperationCount}");
                }

                // Check memory limits
                if (_totalMemoryAllocated >= _config.MaxMemoryBytes)
                {
                    RecordViolation("MaxMemoryBytes", $"Memory usage {_totalMemoryAllocated} bytes exceeded limit {_config.MaxMemoryBytes}");
                    HandleViolation("MaxMemoryBytes");
                    throw CreateGuardrailException($"Maximum memory usage exceeded: {_totalMemoryAllocated} bytes");
                }
            }
        }

        /// <summary>
        /// Begins tracking a new operation
        /// </summary>
        public IDisposable BeginOperation(string operationName)
        {
            ValidateOperationName(operationName);
            
            using (AcquireLock())
            {
                _operationStack.Push(operationName);
                _operationCount++;
                _concurrentOperationCount++;

                var metrics = _operationMetrics.GetOrAdd(operationName, _ => new OperationMetrics());
                metrics.StartOperation();
            }

            return new OperationScope(this, operationName);
        }

        /// <summary>
        /// Ends tracking of an operation
        /// </summary>
        public void EndOperation(string operationName)
        {
            using (AcquireLock())
            {
                // Verify operation name matches stack top
                if (_operationStack.Count > 0 && _operationStack.Peek() != operationName)
                {
                    // Operation stack mismatch - this shouldn't happen in normal operation
                    // but we'll handle it gracefully
                    // TODO: Consider logging this discrepancy
                }

                if (_operationStack.Count > 0)
                {
                    _operationStack.Pop();
                }

                _concurrentOperationCount = Math.Max(0, _concurrentOperationCount - 1);

                if (_operationMetrics.TryGetValue(operationName, out var metrics))
                {
                    metrics.EndOperation();
                }
            }
        }

        /// <summary>
        /// Records memory allocation
        /// </summary>
        public void RecordMemoryAllocation(long bytes)
        {
            using (AcquireLock())
            {
                if (bytes < 0)
                    throw new ArgumentException("Memory allocation cannot be negative", nameof(bytes));

                _totalMemoryAllocated += bytes;

                // Check single allocation limit
                if (bytes > _config.MaxSingleAllocationBytes)
                {
                    RecordViolation("MaxSingleAllocationBytes", $"Single allocation of {bytes} bytes exceeded limit {_config.MaxSingleAllocationBytes}");
                    HandleViolation("MaxSingleAllocationBytes");
                }
            }
        }

        /// <summary>
        /// Records an exception that occurred during execution
        /// </summary>
        public void RecordException(Exception exception)
        {
            using (AcquireLock())
            {
                if (_exceptions.Count < _config.MaxTrackedExceptions)
                {
                    _exceptions.Add(exception);
                }
            }
        }

        /// <summary>
        /// Gets all exceptions recorded during execution
        /// </summary>
        public IReadOnlyList<Exception> GetExceptions()
        {
            using (AcquireLock())
            {
                return _exceptions.ToList().AsReadOnly();
            }
        }

        /// <summary>
        /// Gets current operation metrics
        /// </summary>
        public IReadOnlyDictionary<string, OperationMetrics> GetOperationMetrics()
        {
            return _operationMetrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <summary>
        /// Gets current execution state summary
        /// </summary>
        public ExecutionStateSummary GetSummary()
        {
            using (AcquireLock())
            {
                return new ExecutionStateSummary
                {
                    ElapsedTime = _executionStopwatch.Elapsed,
                    OperationCount = _operationCount,
                    ConcurrentOperations = _concurrentOperationCount,
                    RecursionDepth = _operationStack.Count,
                    MemoryAllocated = _totalMemoryAllocated,
                    CurrentMode = _currentMode,
                    HasExceededLimits = HasExceededAnyLimit,
                    ConsecutiveViolations = _consecutiveViolations,
                    ExceptionCount = _exceptions.Count
                };
            }
        }

        #endregion

        #region Private Methods

        private void ValidateConfiguration()
        {
            _config.Validate();
        }

        private void ValidateOperationName(string operationName)
        {
            if (string.IsNullOrEmpty(operationName))
                throw new ArgumentException("Operation name cannot be null or empty", nameof(operationName));

            if (operationName.Length > _config.MaxOperationNameLength)
                throw new ArgumentException($"Operation name '{operationName}' exceeds maximum length of {_config.MaxOperationNameLength}", nameof(operationName));
        }

        private void RecordViolation(string limitType, string details)
        {
            HasExceededAnyLimit = true;
            _consecutiveViolations++;

            if (_config.DetailedViolationLogging)
            {
                // TODO: Log the violation with detailed information
                // This would integrate with the TiXL logging system
            }

            // Check if we've exceeded the consecutive violation limit
            if (_consecutiveViolations >= _config.MaxConsecutiveViolations)
            {
                _currentMode = ExecutionMode.Panic;
            }
        }

        private void HandleViolation(string limitType)
        {
            switch (_config.OnViolation)
            {
                case GuardrailViolationAction.CancelOperation:
                    // This will throw an exception from ValidateCanProceed
                    break;

                case GuardrailViolationAction.SwitchToSafeMode:
                    if (_currentMode == ExecutionMode.Normal)
                    {
                        _currentMode = ExecutionMode.Safe;
                        // Reduce limits by half in safe mode
                        // TODO: Implement safe mode limits
                    }
                    break;

                case GuardrailViolationAction.EmergencyShutdown:
                    _currentMode = ExecutionMode.Panic;
                    break;

                case GuardrailViolationAction.LogAndContinue:
                    // Just log and continue
                    break;

                case GuardrailViolationAction.ThrowException:
                    throw CreateGuardrailException($"Guardrail violation: {limitType}");
            }
        }

        private Exception CreateGuardrailException(string message)
        {
            if (_config.ViolationExceptionFactory != null)
            {
                return _config.ViolationExceptionFactory(message);
            }

            return new OperationCanceledException($"Guardrail violation: {message}");
        }

        private Lock AcquireLock()
        {
            return new Lock(_lock);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _executionStopwatch?.Stop();
        }

        #endregion

        #region Supporting Types

        private class Lock : IDisposable
        {
            private readonly object _lockObject;

            public Lock(object lockObject)
            {
                _lockObject = lockObject;
                Monitor.Enter(_lockObject);
            }

            public void Dispose()
            {
                Monitor.Exit(_lockObject);
            }
        }

        #endregion
    }

    #region Supporting Types

    /// <summary>
    /// Execution modes indicating current safety level
    /// </summary>
    public enum ExecutionMode
    {
        Normal,
        Safe,
        Panic
    }

    /// <summary>
    /// Metrics for a single operation
    /// </summary>
    public class OperationMetrics
    {
        private int _callCount;
        private long _totalDurationTicks;
        private int _activeOperations;

        public int CallCount => _callCount;
        public long TotalDurationTicks => _totalDurationTicks;
        public double AverageDurationMs => _callCount > 0 ? (_totalDurationTicks / (double)Stopwatch.Frequency * 1000) / _callCount : 0;
        public bool IsActive => _activeOperations > 0;

        public void StartOperation()
        {
            Interlocked.Increment(ref _activeOperations);
        }

        public void EndOperation()
        {
            Interlocked.Decrement(ref _activeOperations);
            Interlocked.Increment(ref _callCount);
        }
    }

    /// <summary>
    /// Summary of current execution state
    /// </summary>
    public class ExecutionStateSummary
    {
        public TimeSpan ElapsedTime { get; set; }
        public int OperationCount { get; set; }
        public int ConcurrentOperations { get; set; }
        public int RecursionDepth { get; set; }
        public long MemoryAllocated { get; set; }
        public ExecutionMode CurrentMode { get; set; }
        public bool HasExceededLimits { get; set; }
        public int ConsecutiveViolations { get; set; }
        public int ExceptionCount { get; set; }
    }

    internal class OperationScope : IDisposable
    {
        private readonly ExecutionState _executionState;
        private readonly string _operationName;
        private bool _disposed;

        public OperationScope(ExecutionState executionState, string operationName)
        {
            _executionState = executionState;
            _operationName = operationName;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _executionState.EndOperation(_operationName);
        }
    }

    #endregion
}