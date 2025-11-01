using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using T3.Core.Logging;
using T3.Core.Resource;
using T3.Core.Rendering;

namespace T3.Core.Operators
{
    /// <summary>
    /// Enhanced EvaluationContext with comprehensive guardrails for safe operator execution.
    /// Prevents runaway evaluations, resource exhaustion, and infinite loops.
    /// </summary>
    public class EvaluationContext : IDisposable
    {
        #region Core Properties

        public IRenderingEngine RenderingEngine { get; }
        public IAudioEngine AudioEngine { get; }
        public IResourceManager ResourceManager { get; }
        public ILogger Logger { get; }
        public CancellationToken CancellationToken { get; }

        #endregion

        #region Guardrail Configuration

        private readonly GuardrailConfiguration _guardrails;
        private readonly ExecutionState _executionState;
        private readonly IPerformanceMonitor _performanceMonitor;

        /// <summary>
        /// Gets current execution metrics for performance tracking
        /// </summary>
        public EvaluationMetrics Metrics => _performanceMonitor.GetCurrentMetrics();

        /// <summary>
        /// Gets current execution state including whether limits are exceeded
        /// </summary>
        public ExecutionState CurrentState => _executionState;

        /// <summary>
        /// Configuration for guardrail limits and thresholds
        /// </summary>
        public GuardrailConfiguration Configuration => _guardrails;

        #endregion

        #region Constructor and Factory Methods

        public EvaluationContext(
            IRenderingEngine renderingEngine,
            IAudioEngine audioEngine,
            IResourceManager resourceManager,
            ILogger logger,
            CancellationToken cancellationToken = default,
            GuardrailConfiguration? guardrails = null)
        {
            RenderingEngine = renderingEngine ?? throw new ArgumentNullException(nameof(renderingEngine));
            AudioEngine = audioEngine ?? throw new ArgumentNullException(nameof(audioEngine));
            ResourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            CancellationToken = cancellationToken;

            _guardrails = guardrails ?? GuardrailConfiguration.Default;
            _executionState = new ExecutionState(_guardrails);
            _performanceMonitor = new PerformanceMonitor(_guardrails);

            LogConstruction();
        }

        /// <summary>
        /// Creates a new EvaluationContext with default guardrails for test scenarios
        /// </summary>
        public static EvaluationContext CreateForTest(
            IRenderingEngine? renderingEngine = null,
            IAudioEngine? audioEngine = null,
            IResourceManager? resourceManager = null,
            ILogger? logger = null,
            GuardrailConfiguration? guardrails = null)
        {
            // Create minimal mock implementations for testing
            renderingEngine ??= new MockRenderingEngine();
            audioEngine ??= new MockAudioEngine();
            resourceManager ??= new MockResourceManager();
            logger ??= new MockLogger();

            return new EvaluationContext(
                renderingEngine,
                audioEngine,
                resourceManager,
                logger,
                CancellationToken.None,
                guardrails ?? GuardrailConfiguration.ForTesting()
            );
        }

        #endregion

        #region Guardrail Methods

        /// <summary>
        /// Validates that evaluation can proceed with current limits
        /// Throws OperationCanceledException if limits are exceeded
        /// </summary>
        public void ValidateCanProceed([CallerMemberName] string operationName = "")
        {
            _executionState.ValidateCanProceed(operationName);
        }

        /// <summary>
        /// Begins tracking a new operation for guardrail monitoring
        /// </summary>
        public IDisposable BeginOperation(string operationName)
        {
            return new OperationTracker(this, operationName);
        }

        /// <summary>
        /// Executes an action with automatic guardrail protection
        /// </summary>
        public void ExecuteWithGuardrails(string operationName, Action action)
        {
            using (BeginOperation(operationName))
            {
                ValidateCanProceed(operationName);
                action();
            }
        }

        /// <summary>
        /// Executes a function with automatic guardrail protection
        /// </summary>
        public T ExecuteWithGuardrails<T>(string operationName, Func<T> func)
        {
            using (BeginOperation(operationName))
            {
                ValidateCanProceed(operationName);
                return func();
            }
        }

        /// <summary>
        /// Executes an async operation with guardrail protection
        /// </summary>
        public async Task<T> ExecuteWithGuardrailsAsync<T>(string operationName, Func<CancellationToken, Task<T>> asyncFunc)
        {
            using (BeginOperation(operationName))
            {
                ValidateCanProceed(operationName);
                return await asyncFunc(CancellationToken);
            }
        }

        /// <summary>
        /// Checks if the current operation respects resource limits
        /// </summary>
        public GuardrailStatus CheckResourceLimits()
        {
            return _performanceMonitor.GetCurrentStatus();
        }

        /// <summary>
        /// Gets pre-validation check results for inputs
        /// </summary>
        public PreconditionValidationResult ValidatePreconditions(IDictionary<string, object> preconditions)
        {
            return PreconditionValidator.Validate(preconditions, _guardrails);
        }

        #endregion

        #region Error Boundaries

        /// <summary>
        /// Executes an action with comprehensive error boundary protection
        /// </summary>
        public bool TryExecuteWithErrorBoundary(string operationName, Action action, out Exception? exception)
        {
            exception = null;
            try
            {
                using (BeginOperation(operationName))
                {
                    ValidateCanProceed(operationName);
                    action();
                    return true;
                }
            }
            catch (OperationCanceledException)
            {
                // Guardrail timeout - this is expected behavior
                Logger.Warning($"Operation '{operationName}' was cancelled due to guardrail limits");
                return false;
            }
            catch (Exception ex)
            {
                exception = ex;
                Logger.Error(ex, $"Operation '{operationName}' failed with exception");
                _executionState.RecordException(ex);
                return false;
            }
        }

        /// <summary>
        /// Executes an async operation with error boundary protection
        /// </summary>
        public async Task<bool> TryExecuteWithErrorBoundaryAsync(
            string operationName,
            Func<CancellationToken, Task> asyncAction,
            out Exception? exception)
        {
            exception = null;
            try
            {
                using (BeginOperation(operationName))
                {
                    ValidateCanProceed(operationName);
                    await asyncAction(CancellationToken);
                    return true;
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Warning($"Async operation '{operationName}' was cancelled due to guardrail limits");
                return false;
            }
            catch (Exception ex)
            {
                exception = ex;
                Logger.Error(ex, $"Async operation '{operationName}' failed with exception");
                _executionState.RecordException(ex);
                return false;
            }
        }

        #endregion

        #region Resource Management

        /// <summary>
        /// Tracks resource allocation for memory limit enforcement
        /// </summary>
        public void TrackResourceAllocation(string resourceType, long bytes)
        {
            _performanceMonitor.TrackResourceAllocation(resourceType, bytes);
        }

        /// <summary>
        /// Gets current resource usage statistics
        /// </summary>
        public ResourceUsageStatistics GetResourceUsage()
        {
            return _performanceMonitor.GetResourceStatistics();
        }

        /// <summary>
        /// Releases tracked resources
        /// </summary>
        public void ReleaseTrackedResources()
        {
            _performanceMonitor.ReleaseAllResources();
        }

        #endregion

        #region Performance Monitoring

        /// <summary>
        /// Records a performance metric
        /// </summary>
        public void RecordMetric(string metricName, double value, string unit = "")
        {
            _performanceMonitor.RecordMetric(metricName, value, unit);
        }

        /// <summary>
        /// Gets detailed performance report
        /// </summary>
        public PerformanceReport GetPerformanceReport()
        {
            return _performanceMonitor.GenerateReport();
        }

        #endregion

        #region Private Methods

        private void LogConstruction()
        {
            Logger.Debug($"Created EvaluationContext with guardrails: " +
                        $"MaxDuration={_guardrails.MaxEvaluationDuration}, " +
                        $"MaxMemory={_guardrails.MaxMemoryBytes}, " +
                        $"MaxOperations={_guardrails.MaxOperationsPerEvaluation}");
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _performanceMonitor?.Dispose();
            _executionState?.Dispose();
        }

        #endregion

        #region Mock Implementations for Testing

        private class MockRenderingEngine : IRenderingEngine { }
        private class MockAudioEngine : IAudioEngine { }
        private class MockResourceManager : IResourceManager { }
        private class MockLogger : ILogger 
        {
            public void Debug(string message) { }
            public void Information(string message) { }
            public void Warning(string message) { }
            public void Error(string message) { }
            public void Error(Exception exception, string message) { }
        }

        #endregion
    }
}