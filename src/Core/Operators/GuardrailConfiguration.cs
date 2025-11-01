using System;

namespace T3.Core.Operators
{
    /// <summary>
    /// Configuration for evaluation guardrails and resource limits
    /// </summary>
    public class GuardrailConfiguration
    {
        #region Time Limits

        /// <summary>
        /// Maximum duration for a single evaluation operation (default: 5 seconds)
        /// </summary>
        public TimeSpan MaxEvaluationDuration { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Maximum duration for a single operation within an evaluation (default: 100ms)
        /// </summary>
        public TimeSpan MaxOperationDuration { get; set; } = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// Timeout for resource allocation attempts (default: 30 seconds)
        /// </summary>
        public TimeSpan ResourceAllocationTimeout { get; set; } = TimeSpan.FromSeconds(30);

        #endregion

        #region Memory Limits

        /// <summary>
        /// Maximum memory usage for a single evaluation (default: 100MB)
        /// </summary>
        public long MaxMemoryBytes { get; set; } = 100 * 1024 * 1024; // 100MB

        /// <summary>
        /// Maximum memory that can be allocated in a single operation (default: 10MB)
        /// </summary>
        public long MaxSingleAllocationBytes { get; set; } = 10 * 1024 * 1024; // 10MB

        /// <summary>
        /// Memory threshold for warning (percentage of MaxMemoryBytes, default: 80%)
        /// </summary>
        public double MemoryWarningThreshold { get; set; } = 0.8;

        /// <summary>
        /// Garbage collection pressure threshold (default: 100MB allocated since last GC)
        /// </summary>
        public long MaxGcPressureBytes { get; set; } = 100 * 1024 * 1024; // 100MB

        #endregion

        #region Operation Limits

        /// <summary>
        /// Maximum number of operations within a single evaluation (default: 1000)
        /// </summary>
        public int MaxOperationsPerEvaluation { get; set; } = 1000;

        /// <summary>
        /// Maximum recursion depth for nested operations (default: 10)
        /// </summary>
        public int MaxRecursionDepth { get; set; } = 10;

        /// <summary>
        /// Maximum number of concurrent operations (default: 10)
        /// </summary>
        public int MaxConcurrentOperations { get; set; } = 10;

        /// <summary>
        /// Maximum string length for operations (default: 1000 chars)
        /// </summary>
        public int MaxOperationNameLength { get; set; } = 1000;

        #endregion

        #region Performance Limits

        /// <summary>
        /// Maximum CPU time percentage for evaluation (default: 80%)
        /// </summary>
        public double MaxCpuUsagePercent { get; set; } = 80.0;

        /// <summary>
        /// Maximum number of performance metrics to track (default: 1000)
        /// </summary>
        public int MaxMetricsTracked { get; set; } = 1000;

        /// <summary>
        /// Minimum time between performance warnings (default: 10 seconds)
        /// </summary>
        public TimeSpan PerformanceWarningInterval { get; set; } = TimeSpan.FromSeconds(10);

        #endregion

        #region Resource Limits

        /// <summary>
        /// Maximum number of file handles (default: 100)
        /// </summary>
        public int MaxFileHandles { get; set; } = 100;

        /// <summary>
        /// Maximum number of network connections (default: 10)
        /// </summary>
        public int MaxNetworkConnections { get; set; } = 10;

        /// <summary>
        /// Maximum texture size in pixels (default: 4096x4096)
        /// </summary>
        public int MaxTextureSize { get; set; } = 4096;

        /// <summary>
        /// Maximum number of textures that can be loaded (default: 50)
        /// </summary>
        public int MaxTexturesLoaded { get; set; } = 50;

        #endregion

        #region Safety Limits

        /// <summary>
        /// Enable strict mode where all limits are enforced aggressively (default: false)
        /// </summary>
        public bool StrictMode { get; set; } = false;

        /// <summary>
        /// Enable detailed logging for all guardrail violations (default: false)
        /// </summary>
        public bool DetailedViolationLogging { get; set; } = false;

        /// <summary>
        /// Enable automatic recovery from resource exhaustion (default: true)
        /// </summary>
        public bool EnableAutoRecovery { get; set; } = true;

        /// <summary>
        /// Maximum number of consecutive violations before panic mode (default: 5)
        /// </summary>
        public int MaxConsecutiveViolations { get; set; } = 5;

        #endregion

        #region Precondition Validation

        /// <summary>
        /// Enable precondition validation for all operations (default: true)
        /// </summary>
        public bool EnablePreconditionValidation { get; set; } = true;

        /// <summary>
        /// Maximum size for precondition data (default: 1MB)
        /// </summary>
        public long MaxPreconditionDataSize { get; set; } = 1024 * 1024; // 1MB

        /// <summary>
        /// Maximum number of preconditions per operation (default: 20)
        /// </summary>
        public int MaxPreconditionsPerOperation { get; set; } = 20;

        #endregion

        #region Error Handling

        /// <summary>
        /// Action to take when guardrail is violated (default: CancelOperation)
        /// </summary>
        public GuardrailViolationAction OnViolation { get; set; } = GuardrailViolationAction.CancelOperation;

        /// <summary>
        /// Custom exception factory for guardrail violations
        /// </summary>
        public Func<string, Exception>? ViolationExceptionFactory { get; set; }

        /// <summary>
        /// Maximum number of exceptions to track per evaluation (default: 100)
        /// </summary>
        public int MaxTrackedExceptions { get; set; } = 100;

        #endregion

        #region Factory Methods

        /// <summary>
        /// Default configuration for production use
        /// </summary>
        public static GuardrailConfiguration Default { get; } = new GuardrailConfiguration();

        /// <summary>
        /// Configuration optimized for testing scenarios
        /// </summary>
        public static GuardrailConfiguration ForTesting()
        {
            return new GuardrailConfiguration
            {
                MaxEvaluationDuration = TimeSpan.FromSeconds(1),
                MaxOperationDuration = TimeSpan.FromMilliseconds(10),
                MaxMemoryBytes = 10 * 1024 * 1024, // 10MB
                MaxOperationsPerEvaluation = 100,
                StrictMode = true,
                DetailedViolationLogging = true
            };
        }

        /// <summary>
        /// Configuration for high-performance scenarios with relaxed limits
        /// </summary>
        public static GuardrailConfiguration ForPerformance()
        {
            return new GuardrailConfiguration
            {
                MaxEvaluationDuration = TimeSpan.FromSeconds(30),
                MaxOperationDuration = TimeSpan.FromMilliseconds(500),
                MaxMemoryBytes = 512 * 1024 * 1024, // 512MB
                MaxOperationsPerEvaluation = 10000,
                MaxRecursionDepth = 50,
                EnablePreconditionValidation = false
            };
        }

        /// <summary>
        /// Configuration for development with maximum safety
        /// </summary>
        public static GuardrailConfiguration ForDevelopment()
        {
            return new GuardrailConfiguration
            {
                MaxEvaluationDuration = TimeSpan.FromSeconds(10),
                MaxOperationDuration = TimeSpan.FromMilliseconds(100),
                MaxMemoryBytes = 256 * 1024 * 1024, // 256MB
                MaxOperationsPerEvaluation = 5000,
                StrictMode = true,
                DetailedViolationLogging = true,
                EnableAutoRecovery = true,
                OnViolation = GuardrailViolationAction.LogAndContinue
            };
        }

        /// <summary>
        /// Configuration for debugging with minimal constraints
        /// </summary>
        public static GuardrailConfiguration ForDebugging()
        {
            return new GuardrailConfiguration
            {
                MaxEvaluationDuration = TimeSpan.FromMinutes(5),
                MaxOperationDuration = TimeSpan.FromSeconds(2),
                MaxMemoryBytes = 1024 * 1024 * 1024, // 1GB
                MaxOperationsPerEvaluation = int.MaxValue,
                EnablePreconditionValidation = false,
                OnViolation = GuardrailViolationAction.LogAndContinue
            };
        }

        #endregion

        #region Validation

        /// <summary>
        /// Validates configuration values and throws exceptions for invalid settings
        /// </summary>
        public void Validate()
        {
            if (MaxEvaluationDuration <= TimeSpan.Zero)
                throw new ArgumentException("MaxEvaluationDuration must be positive", nameof(MaxEvaluationDuration));

            if (MaxOperationDuration <= TimeSpan.Zero)
                throw new ArgumentException("MaxOperationDuration must be positive", nameof(MaxOperationDuration));

            if (MaxOperationDuration > MaxEvaluationDuration)
                throw new ArgumentException("MaxOperationDuration cannot exceed MaxEvaluationDuration", nameof(MaxOperationDuration));

            if (MaxMemoryBytes <= 0)
                throw new ArgumentException("MaxMemoryBytes must be positive", nameof(MaxMemoryBytes));

            if (MaxSingleAllocationBytes <= 0)
                throw new ArgumentException("MaxSingleAllocationBytes must be positive", nameof(MaxSingleAllocationBytes));

            if (MaxSingleAllocationBytes > MaxMemoryBytes)
                throw new ArgumentException("MaxSingleAllocationBytes cannot exceed MaxMemoryBytes", nameof(MaxSingleAllocationBytes));

            if (MemoryWarningThreshold < 0 || MemoryWarningThreshold > 1)
                throw new ArgumentException("MemoryWarningThreshold must be between 0 and 1", nameof(MemoryWarningThreshold));

            if (MaxOperationsPerEvaluation <= 0)
                throw new ArgumentException("MaxOperationsPerEvaluation must be positive", nameof(MaxOperationsPerEvaluation));

            if (MaxRecursionDepth <= 0)
                throw new ArgumentException("MaxRecursionDepth must be positive", nameof(MaxRecursionDepth));

            if (MaxConcurrentOperations <= 0)
                throw new ArgumentException("MaxConcurrentOperations must be positive", nameof(MaxConcurrentOperations));

            if (MaxCpuUsagePercent < 0 || MaxCpuUsagePercent > 100)
                throw new ArgumentException("MaxCpuUsagePercent must be between 0 and 100", nameof(MaxCpuUsagePercent));

            if (MaxFileHandles <= 0)
                throw new ArgumentException("MaxFileHandles must be positive", nameof(MaxFileHandles));

            if (MaxNetworkConnections < 0)
                throw new ArgumentException("MaxNetworkConnections must be non-negative", nameof(MaxNetworkConnections));

            if (MaxTextureSize <= 0)
                throw new ArgumentException("MaxTextureSize must be positive", nameof(MaxTextureSize));

            if (MaxTexturesLoaded <= 0)
                throw new ArgumentException("MaxTexturesLoaded must be positive", nameof(MaxTexturesLoaded));
        }

        #endregion
    }

    #region Supporting Types

    /// <summary>
    /// Actions to take when a guardrail violation occurs
    /// </summary>
    public enum GuardrailViolationAction
    {
        /// <summary>
        /// Cancel the current operation
        /// </summary>
        CancelOperation,

        /// <summary>
        /// Log the violation and continue execution
        /// </summary>
        LogAndContinue,

        /// <summary>
        /// Throw an exception
        /// </summary>
        ThrowException,

        /// <summary>
        /// Switch to safe mode with reduced limits
        /// </summary>
        SwitchToSafeMode,

        /// <summary>
        /// Trigger emergency shutdown
        /// </summary>
        EmergencyShutdown
    }

    #endregion
}