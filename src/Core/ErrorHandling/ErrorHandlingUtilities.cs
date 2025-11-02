using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TiXL.Core.ErrorHandling
{
    /// <summary>
    /// Retry configuration options for transient failure handling
    /// </summary>
    public class RetryPolicy
    {
        public int MaxRetries { get; set; } = 3;
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(100);
        public double BackoffMultiplier { get; set; } = 2.0;
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);
        public Func<Exception, bool> RetryCondition { get; set; } = ex => true;

        public TimeSpan GetDelay(int attempt)
        {
            var delay = TimeSpan.FromTicks((long)(InitialDelay.Ticks * Math.Pow(BackoffMultiplier, attempt)));
            return delay > MaxDelay ? MaxDelay : delay;
        }
    }

    /// <summary>
    /// Timeout configuration for operations
    /// </summary>
    public class TimeoutPolicy
    {
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
        public Action OnTimeout { get; set; } = null;

        public async Task<bool> ExecuteWithTimeoutAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(Timeout);

            try
            {
                await operation(cts.Token);
                return true;
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                OnTimeout?.Invoke();
                throw new TiXLOperationTimeoutException("Operation", Timeout);
            }
        }

        public async Task<T> ExecuteWithTimeoutAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(Timeout);

            try
            {
                return await operation(cts.Token);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                OnTimeout?.Invoke();
                throw new TiXLOperationTimeoutException("Operation", Timeout);
            }
        }
    }

    /// <summary>
    /// Graceful degradation strategy for operation failures
    /// </summary>
    public class GracefulDegradationStrategy
    {
        public enum DegradationLevel
        {
            None,           // Operation continues normally
            Reduced,        // Operation continues with reduced functionality
            Minimal,        // Operation continues with minimal functionality
            Deferred,       // Operation is deferred to later
            Skipped         // Operation is completely skipped
        }

        public DegradationLevel CurrentLevel { get; private set; } = DegradationLevel.None;
        private readonly List<string> _failureReasons = new();

        public void RecordFailure(string reason)
        {
            _failureReasons.Add(reason);
            UpdateDegradationLevel();
        }

        private void UpdateDegradationLevel()
        {
            if (_failureReasons.Count >= 10)
                CurrentLevel = DegradationLevel.Skipped;
            else if (_failureReasons.Count >= 5)
                CurrentLevel = DegradationLevel.Deferred;
            else if (_failureReasons.Count >= 3)
                CurrentLevel = DegradationLevel.Minimal;
            else if (_failureReasons.Count >= 1)
                CurrentLevel = DegradationLevel.Reduced;
            else
                CurrentLevel = DegradationLevel.None;
        }

        public bool CanProceed()
        {
            return CurrentLevel != DegradationLevel.Skipped;
        }

        public void Reset()
        {
            _failureReasons.Clear();
            CurrentLevel = DegradationLevel.None;
        }
    }

    /// <summary>
    /// Retry executor with comprehensive error handling
    /// </summary>
    public static class RetryExecutor
    {
        /// <summary>
        /// Executes an operation with retry logic for transient failures
        /// </summary>
        public static async Task<T> ExecuteWithRetryAsync<T>(
            Func<Task<T>> operation,
            RetryPolicy retryPolicy,
            string operationName,
            ILogger logger = null)
        {
            Exception lastException = null;

            for (int attempt = 0; attempt <= retryPolicy.MaxRetries; attempt++)
            {
                try
                {
                    logger?.LogDebug("Executing {OperationName} (attempt {Attempt}/{MaxRetries})", operationName, attempt + 1, retryPolicy.MaxRetries + 1);
                    return await operation();
                }
                catch (OperationCanceledException) when (attempt < retryPolicy.MaxRetries)
                {
                    logger?.LogWarning("Operation {OperationName} cancelled, attempting retry {Attempt}", operationName, attempt + 1);
                    lastException = null; // Don't retry on cancellation, exit immediately
                    throw;
                }
                catch (Exception ex) when (attempt < retryPolicy.MaxRetries && retryPolicy.RetryCondition(ex))
                {
                    lastException = ex;
                    var delay = retryPolicy.GetDelay(attempt);
                    
                    logger?.LogWarning(ex, "Operation {OperationName} failed on attempt {Attempt}, retrying in {Delay}ms",
                        operationName, attempt + 1, delay.TotalMilliseconds);
                    
                    await Task.Delay(delay);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Operation {OperationName} failed with unretryable exception", operationName);
                    throw;
                }
            }

            // If we get here, all retries failed
            logger?.LogError("Operation {OperationName} failed after {MaxRetries} retries", operationName, retryPolicy.MaxRetries);
            throw new TiXLOperationTimeoutException(operationName, TimeSpan.Zero, lastException);
        }

        /// <summary>
        /// Executes an operation with retry logic (non-generic version)
        /// </summary>
        public static async Task ExecuteWithRetryAsync(
            Func<Task> operation,
            RetryPolicy retryPolicy,
            string operationName,
            ILogger logger = null)
        {
            await ExecuteWithRetryAsync(async () => { await operation(); return 0; }, retryPolicy, operationName, logger);
        }

        /// <summary>
        /// Executes an action with retry logic
        /// </summary>
        public static void ExecuteWithRetry(
            Action action,
            RetryPolicy retryPolicy,
            string operationName,
            ILogger logger = null)
        {
            ExecuteWithRetry(() =>
            {
                action();
                return 0;
            }, retryPolicy, operationName, logger);
        }

        private static T ExecuteWithRetry<T>(Func<T> operation, RetryPolicy retryPolicy, string operationName, ILogger logger)
        {
            Exception lastException = null;

            for (int attempt = 0; attempt <= retryPolicy.MaxRetries; attempt++)
            {
                try
                {
                    logger?.LogDebug("Executing {OperationName} (attempt {Attempt}/{MaxRetries})", operationName, attempt + 1, retryPolicy.MaxRetries + 1);
                    return operation();
                }
                catch (Exception ex) when (attempt < retryPolicy.MaxRetries && retryPolicy.RetryCondition(ex))
                {
                    lastException = ex;
                    var delay = retryPolicy.GetDelay(attempt);
                    
                    logger?.LogWarning(ex, "Operation {OperationName} failed on attempt {Attempt}, retrying in {Delay}ms",
                        operationName, attempt + 1, delay.TotalMilliseconds);
                    
                    Thread.Sleep(delay);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Operation {OperationName} failed with unretryable exception", operationName);
                    throw;
                }
            }

            logger?.LogError("Operation {OperationName} failed after {MaxRetries} retries", operationName, retryPolicy.MaxRetries);
            throw new TiXLOperationTimeoutException(operationName, TimeSpan.Zero, lastException);
        }
    }

    /// <summary>
    /// Comprehensive error handling context for operations
    /// </summary>
    public class OperationContext : IDisposable
    {
        private readonly string _operationName;
        private readonly ILogger _logger;
        private readonly GracefulDegradationStrategy _degradationStrategy;
        private readonly TimeoutPolicy _timeoutPolicy;
        private readonly RetryPolicy _retryPolicy;
        private readonly Stopwatch _stopwatch;
        private bool _disposed;

        public OperationContext(
            string operationName,
            ILogger logger,
            GracefulDegradationStrategy degradationStrategy = null,
            TimeoutPolicy timeoutPolicy = null,
            RetryPolicy retryPolicy = null)
        {
            _operationName = operationName ?? throw new ArgumentNullException(nameof(operationName));
            _logger = logger;
            _degradationStrategy = degradationStrategy ?? new GracefulDegradationStrategy();
            _timeoutPolicy = timeoutPolicy ?? new TimeoutPolicy();
            _retryPolicy = retryPolicy ?? new RetryPolicy();
            _stopwatch = System.Diagnostics.Stopwatch.StartNew();

            _logger?.LogDebug("Starting operation: {OperationName}", _operationName);
        }

        public GracefulDegradationStrategy DegradationStrategy => _degradationStrategy;
        public TimeoutPolicy TimeoutPolicy => _timeoutPolicy;
        public RetryPolicy RetryPolicy => _retryPolicy;
        public TimeSpan Elapsed => _stopwatch.Elapsed;

        public void RecordFailure(string reason)
        {
            _degradationStrategy.RecordFailure(reason);
            _logger?.LogWarning("Operation {OperationName} failure recorded: {Reason}", _operationName, reason);
        }

        public void RecordSuccess()
        {
            _logger?.LogInformation("Operation {OperationName} completed successfully in {Elapsed}ms", _operationName, _stopwatch.ElapsedMilliseconds);
        }

        public void RecordError(Exception ex)
        {
            _logger?.LogError(ex, "Operation {OperationName} failed with error: {Error}", _operationName, ex.Message);
            _degradationStrategy.RecordFailure(ex.Message);
        }

        public void CheckGracefulDegradation()
        {
            if (!_degradationStrategy.CanProceed())
            {
                throw new TiXLOperationTimeoutException(_operationName, _timeoutPolicy.Timeout,
                    new InvalidOperationException("Operation skipped due to repeated failures"));
            }
        }

        public async Task<T> ExecuteWithFullProtectionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
        {
            try
            {
                CheckGracefulDegradation();

                var result = await RetryExecutor.ExecuteWithRetryAsync(
                    () => _timeoutPolicy.ExecuteWithTimeoutAsync(operation, cancellationToken),
                    _retryPolicy,
                    _operationName,
                    _logger);

                RecordSuccess();
                return result;
            }
            catch (Exception ex)
            {
                RecordError(ex);
                throw;
            }
        }

        public async Task ExecuteWithFullProtectionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
        {
            await ExecuteWithFullProtectionAsync(async token => { await operation(token); return 0; }, cancellationToken);
        }

        public void ExecuteWithFullProtection(Action action)
        {
            try
            {
                CheckGracefulDegradation();

                RetryExecutor.ExecuteWithRetry(action, _retryPolicy, _operationName, _logger);

                RecordSuccess();
            }
            catch (Exception ex)
            {
                RecordError(ex);
                throw;
            }
        }

        public T ExecuteWithFullProtection<T>(Func<T> function)
        {
            try
            {
                CheckGracefulDegradation();

                var result = RetryExecutor.ExecuteWithRetry(function, _retryPolicy, _operationName, _logger);

                RecordSuccess();
                return result;
            }
            catch (Exception ex)
            {
                RecordError(ex);
                throw;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _stopwatch.Stop();
                _disposed = true;
                _logger?.LogDebug("Operation context disposed for: {OperationName}", _operationName);
            }
        }
    }

    /// <summary>
    /// Resource cleanup helper with automatic disposal
    /// </summary>
    public static class ResourceCleanup
    {
        public static T ExecuteWithCleanup<T>(Func<T> operation, params IDisposable[] resources)
        {
            try
            {
                return operation();
            }
            finally
            {
                foreach (var resource in resources)
                {
                    try
                    {
                        resource?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        // Log but don't throw during cleanup
                        System.Diagnostics.Debug.WriteLine($"Failed to dispose resource: {ex.Message}");
                    }
                }
            }
        }

        public static async Task<T> ExecuteWithCleanupAsync<T>(Func<Task<T>> operation, params IDisposable[] resources)
        {
            try
            {
                return await operation();
            }
            finally
            {
                foreach (var resource in resources)
                {
                    try
                    {
                        resource?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to dispose resource: {ex.Message}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Exception filtering utilities
    /// </summary>
    public static class ExceptionFilters
    {
        /// <summary>
        /// Checks if an exception represents a transient failure that should be retried
        /// </summary>
        public static bool IsTransientFailure(Exception ex)
        {
            return ex switch
            {
                TimeoutException => true,
                TiXLOperationTimeoutException => true,
                TaskCanceledException => true,
                System.IO.IOException => true,
                System.Net.Http.HttpRequestException => true,
                TiXLResourceException resourceEx when resourceEx.AvailableAmount > 0 => true, // Retry if resources might become available
                _ => false
            };
        }

        /// <summary>
        /// Checks if an exception represents a fatal error that should not be retried
        /// </summary>
        public static bool IsFatalError(Exception ex)
        {
            return ex switch
            {
                ArgumentException => true,
                ArgumentNullException => true,
                ArgumentOutOfRangeException => true,
                InvalidOperationException => true,
                NotSupportedException => true,
                NotImplementedException => true,
                UnauthorizedAccessException => true,
                TiXLValidationException => true,
                _ => false
            };
        }
    }
}