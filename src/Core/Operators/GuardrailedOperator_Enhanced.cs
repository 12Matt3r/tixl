using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using T3.Core.Logging;
using T3.Core.Rendering;
using T3.Core.Resource;
using TiXL.Core.ErrorHandling;
using TiXL.Core.Validation;

namespace T3.Core.Operators
{
    /// <summary>
    /// Enhanced base class for operators with comprehensive error handling, retry patterns, and resilient execution
    /// Provides robust operator execution with graceful degradation and comprehensive exception handling
    /// </summary>
    public abstract class GuardrailedOperatorEnhanced : IDisposable
    {
        #region Protected Fields

        protected readonly EvaluationContextEnhanced Context;
        protected readonly ILogger Logger;
        protected readonly ILogger<GuardrailedOperatorEnhanced> EnhancedLogger;
        protected readonly string OperatorName;

        #endregion

        #region Constructor

        protected GuardrailedOperatorEnhanced(
            string operatorName,
            IRenderingEngine renderingEngine,
            IAudioEngine audioEngine,
            IResourceManager resourceManager,
            ILogger logger,
            ILogger<GuardrailedOperatorEnhanced> enhancedLogger = null,
            GuardrailConfiguration? guardrails = null)
        {
            try
            {
                OperatorName = operatorName ?? throw new ArgumentNullException(nameof(operatorName));
                Logger = logger ?? throw new ArgumentNullException(nameof(logger));
                EnhancedLogger = enhancedLogger ?? Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<GuardrailedOperatorEnhanced>();

                Context = new EvaluationContextEnhanced(
                    renderingEngine,
                    audioEngine,
                    resourceManager,
                    logger,
                    enhancedLogger,
                    CancellationToken.None,
                    guardrails ?? GuardrailConfiguration.Default
                );

                Logger.Debug($"Created enhanced guarded operator: {operatorName}");
                EnhancedLogger?.LogDebug("GuardrailedOperatorEnhanced initialized: {OperatorName}", operatorName);
            }
            catch (Exception ex)
            {
                EnhancedLogger?.LogError(ex, "Failed to initialize GuardrailedOperatorEnhanced: {OperatorName}", operatorName);
                throw new OperatorException(
                    OperatorErrorCode.InitializationFailed,
                    $"Failed to initialize operator '{operatorName}': {ex.Message}",
                    ex);
            }
        }

        #endregion

        #region Protected Methods with Enhanced Error Handling

        /// <summary>
        /// Executes an operation with full guardrail protection and comprehensive error handling
        /// </summary>
        protected T ExecuteGuarded<T>(string operationName, Func<T> operation)
        {
            return ExecuteResilientAsync(() =>
            {
                return Context.ExecuteWithGuardrails(operationName, () =>
                {
                    try
                    {
                        ValidatePreconditions();
                        var result = operation();
                        ValidatePostconditions(result);
                        return result;
                    }
                    catch (Exception ex)
                    {
                        HandleOperationException(operationName, ex);
                        throw;
                    }
                });
            }, 
            maxRetries: 2,
            retryPolicy: RetryPolicyType.ExponentialBackoff,
            operationName: operationName);
        }

        /// <summary>
        /// Executes an async operation with full guardrail protection and comprehensive error handling
        /// </summary>
        protected async Task<T> ExecuteGuardedAsync<T>(string operationName, Func<CancellationToken, Task<T>> asyncOperation)
        {
            return await ExecuteResilientAsync(async ct =>
            {
                return await Context.ExecuteWithGuardrailsAsync(operationName, async (ctxCt) =>
                {
                    try
                    {
                        using var combinedCt = CancellationTokenSource.CreateLinkedTokenSource(ct, ctxCt);
                        ValidatePreconditions();
                        var result = await asyncOperation(combinedCt.Token);
                        ValidatePostconditions(result);
                        return result;
                    }
                    catch (Exception ex)
                    {
                        HandleOperationException(operationName, ex);
                        throw;
                    }
                });
            }, 
            maxRetries: 2,
            retryPolicy: RetryPolicyType.ExponentialBackoff,
            operationName: operationName);
        }

        /// <summary>
        /// Executes an operation with error boundary protection (doesn't throw) with graceful degradation
        /// </summary>
        protected bool TryExecuteGuarded(string operationName, Action operation, out Exception? exception)
        {
            exception = null;
            try
            {
                return Context.TryExecuteWithErrorBoundary(operationName, () =>
                {
                    try
                    {
                        ValidatePreconditions();
                        operation();
                        ValidatePostconditions(null);
                    }
                    catch (Exception ex)
                    {
                        HandleOperationException(operationName, ex);
                        throw;
                    }
                }, out var caughtException);

                if (caughtException != null)
                {
                    exception = caughtException;
                    EnhancedLogger?.LogWarning("Operation failed with error boundary: {OperationName}", operationName);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                exception = ex;
                EnhancedLogger?.LogError(ex, "Unexpected error in TryExecuteGuarded: {OperationName}", operationName);
                return false;
            }
        }

        /// <summary>
        /// Executes an async operation with error boundary protection (doesn't throw) with graceful degradation
        /// </summary>
        protected async Task<bool> TryExecuteGuardedAsync(string operationName, Func<CancellationToken, Task> asyncOperation, out Exception? exception)
        {
            exception = null;
            try
            {
                var result = await Context.TryExecuteWithErrorBoundaryAsync(operationName, async (ct) =>
                {
                    try
                    {
                        ValidatePreconditions();
                        await asyncOperation(ct);
                        ValidatePostconditions(null);
                    }
                    catch (Exception ex)
                    {
                        HandleOperationException(operationName, ex);
                        throw;
                    }
                }, out var caughtException);

                if (caughtException != null)
                {
                    exception = caughtException;
                    EnhancedLogger?.LogWarning("Async operation failed with error boundary: {OperationName}", operationName);
                }

                return result;
            }
            catch (Exception ex)
            {
                exception = ex;
                EnhancedLogger?.LogError(ex, "Unexpected error in TryExecuteGuardedAsync: {OperationName}", operationName);
                return false;
            }
        }

        /// <summary>
        /// Executes operation with fallback mechanism - tries primary operation, falls back to alternative on failure
        /// </summary>
        protected T ExecuteWithFallback<T>(string operationName, Func<T> primaryOperation, Func<T> fallbackOperation, out Exception? primaryException)
        {
            primaryException = null;
            
            try
            {
                return ExecuteGuarded(operationName, primaryOperation);
            }
            catch (Exception ex)
            {
                primaryException = ex;
                EnhancedLogger?.LogWarning(ex, "Primary operation failed, executing fallback: {OperationName}", operationName);
                
                try
                {
                    return ExecuteGuarded($"{operationName}_Fallback", fallbackOperation);
                }
                catch (Exception fallbackEx)
                {
                    EnhancedLogger?.LogError(fallbackEx, "Fallback operation also failed: {OperationName}", operationName);
                    throw new OperatorException(
                        OperatorErrorCode.FallbackFailed,
                        $"Both primary and fallback operations failed for '{operationName}': {ex.Message}, {fallbackEx.Message}",
                        fallbackEx);
                }
            }
        }

        /// <summary>
        /// Executes operation with circuit breaker pattern for fault tolerance
        /// </summary>
        protected async Task<T> ExecuteWithCircuitBreaker<T>(string operationName, Func<CancellationToken, Task<T>> asyncOperation, int failureThreshold = 5, TimeSpan recoveryTimeout = default)
        {
            if (recoveryTimeout == default)
                recoveryTimeout = TimeSpan.FromMinutes(1);

            return await ExecuteResilientAsync(async ct =>
            {
                // Simple circuit breaker implementation
                var circuitBreaker = GetCircuitBreaker(operationName);
                
                if (circuitBreaker.IsOpen)
                {
                    if (circuitBreaker.LastFailureTime + recoveryTimeout < DateTime.UtcNow)
                    {
                        circuitBreaker.Reset();
                    }
                    else
                    {
                        throw new OperatorException(
                            OperatorErrorCode.CircuitBreakerOpen,
                            $"Circuit breaker is open for operation '{operationName}'");
                    }
                }

                try
                {
                    var result = await asyncOperation(ct);
                    circuitBreaker.OnSuccess();
                    return result;
                }
                catch (Exception ex)
                {
                    circuitBreaker.OnFailure();
                    throw;
                }
            }, 
            maxRetries: 1,
            retryPolicy: RetryPolicyType.Linear,
            operationName: operationName);
        }

        /// <summary>
        /// Tracks resource allocation within the operator with comprehensive error handling
        /// </summary>
        protected void TrackResource(string resourceType, long bytes)
        {
            try
            {
                ValidationHelpers.ValidateNonNullOrEmpty(resourceType, nameof(resourceType));
                ValidationHelpers.ValidateNonNegative(bytes, nameof(bytes));

                Context.TrackResourceAllocation(resourceType, bytes);
                EnhancedLogger?.LogDebug("Tracked resource allocation: {ResourceType} = {Bytes} bytes", resourceType, bytes);
            }
            catch (Exception ex)
            {
                EnhancedLogger?.LogWarning(ex, "Failed to track resource allocation: {ResourceType} = {Bytes} bytes", resourceType, bytes);
                // Don't throw - resource tracking failure shouldn't break execution
            }
        }

        /// <summary>
        /// Records a performance metric with comprehensive error handling
        /// </summary>
        protected void RecordMetric(string metricName, double value, string unit = "")
        {
            try
            {
                ValidationHelpers.ValidateNonNullOrEmpty(metricName, nameof(metricName));
                ValidationHelpers.ValidateNonNegative(value, nameof(value));

                Context.RecordMetric($"{OperatorName}.{metricName}", value, unit);
                EnhancedLogger?.LogDebug("Recorded metric: {MetricName}.{Metric} = {Value}{Unit}", OperatorName, metricName, value, unit);
            }
            catch (Exception ex)
            {
                EnhancedLogger?.LogWarning(ex, "Failed to record metric: {MetricName}.{Metric} = {Value}{Unit}", OperatorName, metricName, value, unit);
                // Don't throw - metric recording failure shouldn't break execution
            }
        }

        /// <summary>
        /// Validates preconditions before operation execution with comprehensive error handling
        /// </summary>
        protected virtual void ValidatePreconditions()
        {
            try
            {
                var preconditions = GetPreconditions();
                var validation = Context.ValidatePreconditions(preconditions);
                
                if (!validation.IsValid)
                {
                    var errorSummary = string.Join(", ", validation.Errors.Select(e => e.Message));
                    var exception = new OperatorException(
                        OperatorErrorCode.PreconditionValidationFailed,
                        $"Precondition validation failed: {errorSummary}");
                    EnhancedLogger?.LogWarning(exception, "Precondition validation failed for {OperatorName}: {ErrorSummary}", OperatorName, errorSummary);
                    throw exception;
                }

                if (validation.HasWarnings)
                {
                    var warningSummary = string.Join(", ", validation.Warnings.Select(w => w.Message));
                    Logger.Warning($"Precondition validation warnings for {OperatorName}: {warningSummary}");
                    EnhancedLogger?.LogWarning("Precondition validation warnings for {OperatorName}: {WarningSummary}", OperatorName, warningSummary);
                }
            }
            catch (OperatorException)
            {
                throw; // Re-throw operator exceptions
            }
            catch (Exception ex)
            {
                EnhancedLogger?.LogError(ex, "Unexpected error during precondition validation for {OperatorName}", OperatorName);
                throw new OperatorException(
                    OperatorErrorCode.PreconditionValidationFailed,
                    $"Precondition validation failed unexpectedly: {ex.Message}",
                    ex);
            }
        }

        /// <summary>
        /// Validates postconditions after operation execution with comprehensive error handling
        /// </summary>
        protected virtual void ValidatePostconditions(object? result)
        {
            try
            {
                var resourceStats = Context.GetResourceUsage();
                var validation = PreconditionValidator.ValidateResourceState(resourceStats, Context.Configuration);
                
                if (!validation.IsValid)
                {
                    var errorSummary = string.Join(", ", validation.Errors.Select(e => e.Message));
                    Logger.Error($"Postcondition validation failed for {OperatorName}: {errorSummary}");
                    EnhancedLogger?.LogError("Postcondition validation failed for {OperatorName}: {ErrorSummary}", OperatorName, errorSummary);
                    throw new OperatorException(
                        OperatorErrorCode.PostconditionValidationFailed,
                        $"Postcondition validation failed: {errorSummary}");
                }

                if (validation.HasWarnings)
                {
                    var warningSummary = string.Join(", ", validation.Warnings.Select(w => w.Message));
                    Logger.Warning($"Postcondition validation warnings for {OperatorName}: {warningSummary}");
                    EnhancedLogger?.LogWarning("Postcondition validation warnings for {OperatorName}: {WarningSummary}", OperatorName, warningSummary);
                }
            }
            catch (OperatorException)
            {
                throw; // Re-throw operator exceptions
            }
            catch (Exception ex)
            {
                EnhancedLogger?.LogError(ex, "Unexpected error during postcondition validation for {OperatorName}", OperatorName);
                throw new OperatorException(
                    OperatorErrorCode.PostconditionValidationFailed,
                    $"Postcondition validation failed unexpectedly: {ex.Message}",
                    ex);
            }
        }

        /// <summary>
        /// Gets preconditions for validation with comprehensive error handling
        /// </summary>
        protected virtual IDictionary<string, object> GetPreconditions()
        {
            try
            {
                return new Dictionary<string, object>
                {
                    { "OperatorName", OperatorName },
                    { "ExecutionTime", DateTime.UtcNow },
                    { "ContextState", Context.CurrentState.GetSummary() }
                };
            }
            catch (Exception ex)
            {
                EnhancedLogger?.LogError(ex, "Failed to get preconditions for {OperatorName}", OperatorName);
                return new Dictionary<string, object>
                {
                    { "OperatorName", OperatorName },
                    { "ExecutionTime", DateTime.UtcNow },
                    { "Error", ex.Message }
                };
            }
        }

        /// <summary>
        /// Handles exceptions that occur during operation execution with comprehensive logging and recovery hints
        /// </summary>
        protected virtual void HandleOperationException(string operationName, Exception exception)
        {
            try
            {
                var metrics = Context.Metrics;
                var exceptionType = exception.GetType().Name;
                var isRecoverable = IsRecoverableException(exception);
                
                Logger.Error(exception, 
                    $"Operation '{operationName}' in operator '{OperatorName}' failed. " +
                    $"Type: {exceptionType}, Duration: {metrics.ElapsedTime}, Memory: {metrics.MemoryUsageBytes} bytes, " +
                    $"CPU: {metrics.CpuUsagePercent:F1}%, Recoverable: {isRecoverable}");

                EnhancedLogger?.LogError(exception, 
                    "Operation execution failed: {OperatorName}.{OperationName} - {ExceptionType}. Duration: {Duration}, Memory: {Memory} bytes, CPU: {Cpu:F1}%",
                    OperatorName, operationName, exceptionType, metrics.ElapsedTime, metrics.MemoryUsageBytes, metrics.CpuUsagePercent);

                // Record operation-specific metrics
                RecordMetric($"ExecutionError_{exceptionType}", 1, "count");
                RecordMetric("ExecutionDuration", metrics.ElapsedTime.TotalMilliseconds, "ms");
                
                // Provide recovery hints based on exception type
                var recoveryHint = GetRecoveryHint(exception);
                if (!string.IsNullOrEmpty(recoveryHint))
                {
                    EnhancedLogger?.LogInformation("Recovery hint for {OperatorName}.{OperationName}: {RecoveryHint}", OperatorName, operationName, recoveryHint);
                }
            }
            catch (Exception handlingEx)
            {
                EnhancedLogger?.LogError(handlingEx, "Failed to handle operation exception for {OperatorName}.{OperationName}", OperatorName, operationName);
            }
        }

        /// <summary>
        /// Gets current operator status including guardrail health with comprehensive error handling
        /// </summary>
        protected OperatorStatusEnhanced GetOperatorStatus()
        {
            try
            {
                var contextStatus = Context.CheckResourceLimits();
                var metrics = Context.Metrics;
                var state = Context.CurrentState;

                return new OperatorStatusEnhanced
                {
                    OperatorName = OperatorName,
                    IsHealthy = contextStatus.IsHealthy && state.IsWithinLimits,
                    GuardrailStatus = contextStatus,
                    ExecutionMetrics = metrics,
                    ExecutionState = state.GetSummary(),
                    LastValidationTime = DateTime.UtcNow,
                    Warnings = state.GetSummary().ExceptionCount > 0 ? new[] { "Exceptions recorded during execution" } : Array.Empty<string>(),
                    CircuitBreakerStatus = GetCircuitBreaker(OperatorName).GetStatus()
                };
            }
            catch (Exception ex)
            {
                EnhancedLogger?.LogError(ex, "Failed to get operator status for {OperatorName}", OperatorName);
                return new OperatorStatusEnhanced
                {
                    OperatorName = OperatorName,
                    IsHealthy = false,
                    ErrorMessage = $"Failed to get status: {ex.Message}",
                    LastValidationTime = DateTime.UtcNow
                };
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Determines if an exception is recoverable (can be retried)
        /// </summary>
        private bool IsRecoverableException(Exception exception)
        {
            return exception switch
            {
                TimeoutException => true,
                TaskCanceledException => false, // User cancellation
                OperationCanceledException => false, // Guardrail timeout
                IOSafetyException ioEx when ioEx.ErrorCode switch
                {
                    IOSafetyErrorCode.TemporaryFileCreationFailed => true,
                    IOSafetyErrorCode.DirectoryCreationFailed => true,
                    IOSafetyErrorCode.AtomicOperationFailed => true,
                    _ => false
                } => true,
                _ => false
            };
        }

        /// <summary>
        /// Provides recovery hints based on exception type
        /// </summary>
        private string GetRecoveryHint(Exception exception)
        {
            return exception switch
            {
                TimeoutException => "Consider increasing timeout values or optimizing operation complexity",
                OutOfMemoryException => "Consider reducing memory usage or increasing available memory",
                IOSafetyException ioEx => ioEx.ErrorCode switch
                {
                    IOSafetyErrorCode.DiskSpaceInsufficient => "Free up disk space or use a different storage location",
                    IOSafetyErrorCode.AccessDenied => "Check file permissions and user access rights",
                    IOSafetyErrorCode.PathTooLong => "Use shorter file paths or move files to shallower directory structures",
                    _ => "Check I/O system configuration and resource availability"
                },
                OperatorException opEx => opEx.ErrorCode switch
                {
                    OperatorErrorCode.GuardrailLimitExceeded => "Review guardrail limits or optimize operation to reduce resource usage",
                    OperatorErrorCode.PreconditionValidationFailed => "Verify input parameters and system state before operation",
                    _ => "Review operator configuration and resource requirements"
                },
                _ => "Check system resources and operation parameters"
            };
        }

        #endregion

        #region Circuit Breaker Implementation

        private readonly Dictionary<string, CircuitBreaker> _circuitBreakers = new();

        private CircuitBreaker GetCircuitBreaker(string operationName)
        {
            lock (_circuitBreakers)
            {
                if (!_circuitBreakers.ContainsKey(operationName))
                {
                    _circuitBreakers[operationName] = new CircuitBreaker();
                }
                return _circuitBreakers[operationName];
            }
        }

        private class CircuitBreaker
        {
            private int _failureCount;
            private DateTime _lastFailureTime;
            private bool _isOpen;
            private readonly object _lock = new();

            public bool IsOpen
            {
                get
                {
                    lock (_lock)
                    {
                        return _isOpen;
                    }
                }
            }

            public DateTime LastFailureTime
            {
                get
                {
                    lock (_lock)
                    {
                        return _lastFailureTime;
                    }
                }
            }

            public void OnFailure()
            {
                lock (_lock)
                {
                    _failureCount++;
                    _lastFailureTime = DateTime.UtcNow;
                    
                    if (_failureCount >= 5) // Threshold
                    {
                        _isOpen = true;
                    }
                }
            }

            public void OnSuccess()
            {
                lock (_lock)
                {
                    _failureCount = 0;
                    _isOpen = false;
                }
            }

            public void Reset()
            {
                lock (_lock)
                {
                    _failureCount = 0;
                    _isOpen = false;
                }
            }

            public string GetStatus()
            {
                lock (_lock)
                {
                    return _isOpen ? $"Open (failures: {_failureCount})" : $"Closed (failures: {_failureCount})";
                }
            }
        }

        #endregion

        #region Resilient Execution Helpers

        private async Task ExecuteResilientAsync(Func<Task> operation, int maxRetries, RetryPolicyType retryPolicy, string operationName)
        {
            await ErrorHandlingUtilities.ExecuteWithRetryAsync(operation, maxRetries, retryPolicy, Context.CancellationToken, operationName);
        }

        private async Task<T> ExecuteResilientAsync<T>(Func<CancellationToken, Task<T>> operation, int maxRetries, RetryPolicyType retryPolicy, string operationName)
        {
            return await ErrorHandlingUtilities.ExecuteWithRetryAsync(operation, maxRetries, retryPolicy, Context.CancellationToken, operationName);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            try
            {
                Logger.Debug($"Disposing enhanced guarded operator: {OperatorName}");
                EnhancedLogger?.LogDebug("Disposing GuardrailedOperatorEnhanced: {OperatorName}", OperatorName);
                
                Context?.Dispose();
                
                // Clear circuit breakers
                lock (_circuitBreakers)
                {
                    _circuitBreakers.Clear();
                }
            }
            catch (Exception ex)
            {
                EnhancedLogger?.LogError(ex, "Error disposing operator {OperatorName}", OperatorName);
            }
        }

        #endregion

        #region Supporting Types

        /// <summary>
        /// Enhanced status information for a guarded operator
        /// </summary>
        public class OperatorStatusEnhanced
        {
            public string OperatorName { get; set; } = "";
            public bool IsHealthy { get; set; }
            public GuardrailStatus GuardrailStatus { get; set; } = new();
            public EvaluationMetrics ExecutionMetrics { get; set; } = new();
            public ExecutionStateSummary ExecutionState { get; set; } = new();
            public DateTime LastValidationTime { get; set; }
            public string[] Warnings { get; set; } = Array.Empty<string>();
            public string? ErrorMessage { get; set; }
            public string CircuitBreakerStatus { get; set; } = "Unknown";
            
            /// <summary>
            /// Gets a comprehensive status summary
            /// </summary>
            public string GetStatusSummary()
            {
                if (!string.IsNullOrEmpty(ErrorMessage))
                    return $"Error: {ErrorMessage}";
                    
                if (!IsHealthy)
                    return $"Unhealthy - {GuardrailStatus.Status}";
                    
                if (Warnings.Length > 0)
                    return $"Healthy with warnings: {string.Join(", ", Warnings)}";
                    
                return "Healthy";
            }
        }

        #endregion
    }
}