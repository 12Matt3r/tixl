using System;
using System.Collections.Generic;
using T3.Core.Logging;
using T3.Core.Rendering;
using T3.Core.Resource;

namespace T3.Core.Operators
{
    /// <summary>
    /// Base class for operators with built-in guardrail protection
    /// </summary>
    public abstract class GuardrailedOperator : IDisposable
    {
        #region Protected Fields

        protected readonly EvaluationContext Context;
        protected readonly ILogger Logger;
        protected readonly string OperatorName;

        #endregion

        #region Constructor

        protected GuardrailedOperator(
            string operatorName,
            IRenderingEngine renderingEngine,
            IAudioEngine audioEngine,
            IResourceManager resourceManager,
            ILogger logger,
            GuardrailConfiguration? guardrails = null)
        {
            OperatorName = operatorName ?? throw new ArgumentNullException(nameof(operatorName));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));

            Context = new EvaluationContext(
                renderingEngine,
                audioEngine,
                resourceManager,
                logger,
                CancellationToken.None,
                guardrails ?? GuardrailConfiguration.Default
            );

            Logger.Debug($"Created guarded operator: {operatorName}");
        }

        #endregion

        #region Protected Methods

        /// <summary>
        /// Executes an operation with full guardrail protection
        /// </summary>
        protected T ExecuteGuarded<T>(string operationName, Func<T> operation)
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
        }

        /// <summary>
        /// Executes an async operation with full guardrail protection
        /// </summary>
        protected async Task<T> ExecuteGuardedAsync<T>(string operationName, Func<CancellationToken, Task<T>> asyncOperation)
        {
            return await Context.ExecuteWithGuardrailsAsync(operationName, async (ct) =>
            {
                try
                {
                    ValidatePreconditions();
                    var result = await asyncOperation(ct);
                    ValidatePostconditions(result);
                    return result;
                }
                catch (Exception ex)
                {
                    HandleOperationException(operationName, ex);
                    throw;
                }
            });
        }

        /// <summary>
        /// Executes an operation with error boundary protection (doesn't throw)
        /// </summary>
        protected bool TryExecuteGuarded(string operationName, Action operation, out Exception? exception)
        {
            exception = null;
            
            using (var tracker = Context.BeginOperation(operationName))
            {
                if (!Context.TryExecuteWithErrorBoundary(operationName, () =>
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
                }, out var caughtException))
                {
                    exception = caughtException;
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Executes an async operation with error boundary protection (doesn't throw)
        /// </summary>
        protected async Task<bool> TryExecuteGuardedAsync(string operationName, Func<CancellationToken, Task> asyncOperation, out Exception? exception)
        {
            exception = null;
            
            using (var tracker = Context.BeginOperation(operationName))
            {
                if (!await Context.TryExecuteWithErrorBoundaryAsync(operationName, async (ct) =>
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
                }, out var caughtException))
                {
                    exception = caughtException;
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Tracks resource allocation within the operator
        /// </summary>
        protected void TrackResource(string resourceType, long bytes)
        {
            Context.TrackResourceAllocation(resourceType, bytes);
            Logger.Debug($"Tracked resource allocation: {resourceType} = {bytes} bytes");
        }

        /// <summary>
        /// Records a performance metric
        /// </summary>
        protected void RecordMetric(string metricName, double value, string unit = "")
        {
            Context.RecordMetric($"{OperatorName}.{metricName}", value, unit);
        }

        /// <summary>
        /// Validates preconditions before operation execution
        /// </summary>
        protected virtual void ValidatePreconditions()
        {
            var preconditions = GetPreconditions();
            var validation = Context.ValidatePreconditions(preconditions);
            
            if (!validation.IsValid)
            {
                var errorSummary = string.Join(", ", validation.Errors.Select(e => e.Message));
                throw new InvalidOperationException($"Precondition validation failed: {errorSummary}");
            }

            if (validation.HasWarnings)
            {
                var warningSummary = string.Join(", ", validation.Warnings.Select(w => w.Message));
                Logger.Warning($"Precondition validation warnings for {OperatorName}: {warningSummary}");
            }
        }

        /// <summary>
        /// Validates postconditions after operation execution
        /// </summary>
        protected virtual void ValidatePostconditions(object? result)
        {
            var resourceStats = Context.GetResourceUsage();
            var validation = PreconditionValidator.ValidateResourceState(resourceStats, Context.Configuration);
            
            if (!validation.IsValid)
            {
                var errorSummary = string.Join(", ", validation.Errors.Select(e => e.Message));
                Logger.Error($"Postcondition validation failed for {OperatorName}: {errorSummary}");
                throw new InvalidOperationException($"Postcondition validation failed: {errorSummary}");
            }

            if (validation.HasWarnings)
            {
                var warningSummary = string.Join(", ", validation.Warnings.Select(w => w.Message));
                Logger.Warning($"Postcondition validation warnings for {OperatorName}: {warningSummary}");
            }
        }

        /// <summary>
        /// Gets preconditions for validation
        /// </summary>
        protected virtual IDictionary<string, object> GetPreconditions()
        {
            return new Dictionary<string, object>
            {
                { "OperatorName", OperatorName },
                { "ExecutionTime", DateTime.UtcNow },
                { "ContextState", Context.CurrentState.GetSummary() }
            };
        }

        /// <summary>
        /// Handles exceptions that occur during operation execution
        /// </summary>
        protected virtual void HandleOperationException(string operationName, Exception exception)
        {
            var metrics = Context.Metrics;
            Logger.Error(exception, 
                $"Operation '{operationName}' in operator '{OperatorName}' failed. " +
                $"Duration: {metrics.ElapsedTime}, Memory: {metrics.MemoryUsageBytes} bytes, " +
                $"CPU: {metrics.CpuUsagePercent:F1}%");
        }

        /// <summary>
        /// Gets current operator status including guardrail health
        /// </summary>
        protected OperatorStatus GetOperatorStatus()
        {
            var contextStatus = Context.CheckResourceLimits();
            var metrics = Context.Metrics;
            var state = Context.CurrentState;

            return new OperatorStatus
            {
                OperatorName = OperatorName,
                IsHealthy = contextStatus.IsHealthy && state.IsWithinLimits,
                GuardrailStatus = contextStatus,
                ExecutionMetrics = metrics,
                ExecutionState = state.GetSummary(),
                LastValidationTime = DateTime.UtcNow,
                Warnings = state.GetSummary().ExceptionCount > 0 ? new[] { "Exceptions recorded during execution" } : Array.Empty<string>()
            };
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            try
            {
                Logger.Debug($"Disposing guarded operator: {OperatorName}");
                Context?.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Error disposing operator {OperatorName}");
            }
        }

        #endregion

        #region Supporting Types

        /// <summary>
        /// Status information for a guarded operator
        /// </summary>
        public class OperatorStatus
        {
            public string OperatorName { get; set; } = "";
            public bool IsHealthy { get; set; }
            public GuardrailStatus GuardrailStatus { get; set; } = new();
            public EvaluationMetrics ExecutionMetrics { get; set; } = new();
            public ExecutionStateSummary ExecutionState { get; set; } = new();
            public DateTime LastValidationTime { get; set; }
            public string[] Warnings { get; set; } = Array.Empty<string>();
        }

        #endregion
    }
}