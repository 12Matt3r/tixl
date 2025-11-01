using System;
using System.Diagnostics;
using T3.Core.Logging;

namespace T3.Core.Operators
{
    /// <summary>
    /// Tracks operator execution with automatic guardrail enforcement and performance monitoring
    /// </summary>
    public class OperationTracker : IDisposable
    {
        #region Fields

        private readonly EvaluationContext _context;
        private readonly string _operationName;
        private readonly Stopwatch _operationStopwatch;
        private bool _disposed;
        private readonly int _startingRecursionDepth;
        private readonly bool _ownsExecutionState;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the name of the tracked operation
        /// </summary>
        public string OperationName => _operationName;

        /// <summary>
        /// Gets whether the operation completed successfully
        /// </summary>
        public bool Completed { get; private set; }

        /// <summary>
        /// Gets whether the operation was cancelled due to guardrail limits
        /// </summary>
        public bool WasCancelled { get; private set; }

        /// <summary>
        /// Gets the duration of the operation
        /// </summary>
        public TimeSpan Duration => _operationStopwatch.Elapsed;

        /// <summary>
        /// Gets the current state of execution limits
        /// </summary>
        public ExecutionStateSummary CurrentState => _context.CurrentState;

        /// <summary>
        /// Gets current performance metrics
        /// </summary>
        public EvaluationMetrics CurrentMetrics => _context.Metrics;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new operation tracker and begins monitoring
        /// </summary>
        /// <param name="context">The evaluation context to use for tracking</param>
        /// <param name="operationName">Name of the operation to track</param>
        public OperationTracker(EvaluationContext context, string operationName)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _operationName = operationName ?? throw new ArgumentNullException(nameof(operationName));
            _operationStopwatch = Stopwatch.StartNew();
            _startingRecursionDepth = _context.CurrentState.CurrentRecursionDepth;

            try
            {
                // Begin tracking with execution state
                _ownsExecutionState = true;
                _context.ValidateCanProceed(operationName);
                
                // Start operation tracking
                BeginTracking();
            }
            catch (OperationCanceledException)
            {
                // Guardrail violation occurred
                WasCancelled = true;
                _operationStopwatch.Stop();
                throw;
            }
            catch (Exception ex)
            {
                // Other exception occurred
                WasCancelled = true;
                _operationStopwatch.Stop();
                
                _context.Logger?.Error(ex, $"Operation '{operationName}' failed during initialization");
                throw;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Executes an action within this operation's guardrail context
        /// </summary>
        public void Execute(Action action)
        {
            if (WasCancelled || _disposed)
                throw new OperationCanceledException($"Operation '{_operationName}' cannot proceed: already cancelled or disposed");

            try
            {
                action();
                Completed = true;
            }
            catch (OperationCanceledException)
            {
                WasCancelled = true;
                throw;
            }
            catch (Exception ex)
            {
                _context.Logger?.Error(ex, $"Operation '{_operationName}' failed during execution");
                Completed = false;
                throw;
            }
        }

        /// <summary>
        /// Executes a function within this operation's guardrail context
        /// </summary>
        public T Execute<T>(Func<T> func)
        {
            if (WasCancelled || _disposed)
                throw new OperationCanceledException($"Operation '{_operationName}' cannot proceed: already cancelled or disposed");

            try
            {
                var result = func();
                Completed = true;
                return result;
            }
            catch (OperationCanceledException)
            {
                WasCancelled = true;
                throw;
            }
            catch (Exception ex)
            {
                _context.Logger?.Error(ex, $"Operation '{_operationName}' failed during execution");
                Completed = false;
                throw;
            }
        }

        /// <summary>
        /// Executes an async action within this operation's guardrail context
        /// </summary>
        public async Task ExecuteAsync(Func<Task> asyncAction)
        {
            if (WasCancelled || _disposed)
                throw new OperationCanceledException($"Operation '{_operationName}' cannot proceed: already cancelled or disposed");

            try
            {
                await asyncAction();
                Completed = true;
            }
            catch (OperationCanceledException)
            {
                WasCancelled = true;
                throw;
            }
            catch (Exception ex)
            {
                _context.Logger?.Error(ex, $"Operation '{_operationName}' failed during async execution");
                Completed = false;
                throw;
            }
        }

        /// <summary>
        /// Executes an async function within this operation's guardrail context
        /// </summary>
        public async Task<T> ExecuteAsync<T>(Func<Task<T>> asyncFunc)
        {
            if (WasCancelled || _disposed)
                throw new OperationCanceledException($"Operation '{_operationName}' cannot proceed: already cancelled or disposed");

            try
            {
                var result = await asyncFunc();
                Completed = true;
                return result;
            }
            catch (OperationCanceledException)
            {
                WasCancelled = true;
                throw;
            }
            catch (Exception ex)
            {
                _context.Logger?.Error(ex, $"Operation '{_operationName}' failed during async execution");
                Completed = false;
                throw;
            }
        }

        /// <summary>
        /// Records a custom metric for this operation
        /// </summary>
        public void RecordMetric(string metricName, double value, string unit = "")
        {
            _context.RecordMetric($"{_operationName}.{metricName}", value, unit);
        }

        /// <summary>
        /// Tracks resource allocation within this operation
        /// </summary>
        public void TrackResource(string resourceType, long bytes)
        {
            _context.TrackResourceAllocation(resourceType, bytes);
        }

        /// <summary>
        /// Checks current guardrail status
        /// </summary>
        public GuardrailStatus CheckStatus()
        {
            return _context.CheckResourceLimits();
        }

        /// <summary>
        /// Gets a summary of this operation's performance
        /// </summary>
        public OperationSummary GetSummary()
        {
            return new OperationSummary
            {
                OperationName = _operationName,
                Duration = Duration,
                Completed = Completed,
                WasCancelled = WasCancelled,
                StartingRecursionDepth = _startingRecursionDepth,
                EndingRecursionDepth = _context.CurrentState.CurrentRecursionDepth,
                FinalState = CurrentState,
                FinalMetrics = CurrentMetrics,
                Timestamp = DateTime.UtcNow
            };
        }

        #endregion

        #region Private Methods

        private void BeginTracking()
        {
            _context.Logger?.Debug($"Starting operation: {_operationName}");

            // Record operation start
            _context.RecordMetric($"{_operationName}.start", 1, "count");

            // Check initial status
            var initialStatus = _context.CheckResourceLimits();
            if (!initialStatus.IsHealthy)
            {
                _context.Logger?.Warning($"Operation '{_operationName}' starting with unhealthy status: {initialStatus}");
            }
        }

        private void EndTracking()
        {
            if (!_disposed)
            {
                _operationStopwatch.Stop();

                // Record operation completion
                _context.RecordMetric($"{_operationName}.duration", _operationStopwatch.ElapsedMilliseconds, "ms");
                _context.RecordMetric($"{_operationName}.completed", Completed ? 1 : 0, "count");

                var summary = GetSummary();
                
                if (WasCancelled)
                {
                    _context.Logger?.Warning($"Operation cancelled: {_operationName} after {Duration}");
                }
                else if (Completed)
                {
                    _context.Logger?.Debug($"Operation completed: {_operationName} in {Duration}");
                }
                else
                {
                    _context.Logger?.Error($"Operation failed: {_operationName} after {Duration}");
                }

                // Check final status
                var finalStatus = _context.CheckResourceLimits();
                if (!finalStatus.IsHealthy)
                {
                    _context.Logger?.Warning($"Operation '{_operationName}' ended with unhealthy status");
                }
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                EndTracking();
            }
            catch (Exception ex)
            {
                // Log but don't rethrow during disposal
                _context.Logger?.Error(ex, $"Error disposing operation tracker for '{_operationName}'");
            }
        }

        #endregion
    }

    #region Supporting Types

    /// <summary>
    /// Summary of an operation's execution
    /// </summary>
    public class OperationSummary
    {
        public string OperationName { get; set; } = "";
        public TimeSpan Duration { get; set; }
        public bool Completed { get; set; }
        public bool WasCancelled { get; set; }
        public int StartingRecursionDepth { get; set; }
        public int EndingRecursionDepth { get; set; }
        public ExecutionStateSummary FinalState { get; set; } = new();
        public EvaluationMetrics FinalMetrics { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    #endregion
}