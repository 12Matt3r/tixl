using System;
using System.Runtime.Serialization;

namespace TiXL.Core.ErrorHandling
{
    /// <summary>
    /// Base exception class for all TiXL-specific errors
    /// </summary>
    [Serializable]
    public abstract class TiXLException : Exception
    {
        protected TiXLException(string message) : base(message) { }
        protected TiXLException(string message, Exception inner) : base(message, inner) { }
        protected TiXLException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }

    /// <summary>
    /// Exception thrown when operation is aborted due to timeout
    /// </summary>
    [Serializable]
    public class TiXLOperationTimeoutException : TiXLException
    {
        public TimeSpan OperationTimeout { get; }
        public string OperationName { get; }

        public TiXLOperationTimeoutException(string operationName, TimeSpan timeout, Exception inner = null)
            : base($"Operation '{operationName}' timed out after {timeout.TotalMilliseconds}ms", inner)
        {
            OperationTimeout = timeout;
            OperationName = operationName;
        }

        protected TiXLOperationTimeoutException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    /// <summary>
    /// Exception thrown when GPU operation fails
    /// </summary>
    [Serializable]
    public class TiXLGpuOperationException : TiXLException
    {
        public string GpuOperation { get; }
        public int ErrorCode { get; }

        public TiXLGpuOperationException(string operation, int errorCode, string message)
            : base($"GPU operation '{operation}' failed with error code {errorCode}: {message}")
        {
            GpuOperation = operation;
            ErrorCode = errorCode;
        }

        protected TiXLGpuOperationException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    /// <summary>
    /// Exception thrown when resource allocation fails
    /// </summary>
    [Serializable]
    public class TiXLResourceException : TiXLException
    {
        public string ResourceType { get; }
        public long RequestedAmount { get; }
        public long AvailableAmount { get; }

        public TiXLResourceException(string resourceType, long requested, long available = 0)
            : base($"Failed to allocate {requested} bytes of {resourceType}. Available: {available} bytes")
        {
            ResourceType = resourceType;
            RequestedAmount = requested;
            AvailableAmount = available;
        }

        protected TiXLResourceException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    /// <summary>
    /// Exception thrown when I/O operation fails
    /// </summary>
    [Serializable]
    public class TiXLIoException : TiXLException
    {
        public string FilePath { get; }
        public string Operation { get; }

        public TiXLIoException(string operation, string filePath, string message, Exception inner = null)
            : base($"I/O operation '{operation}' failed for file '{filePath}': {message}", inner)
        {
            Operation = operation;
            FilePath = filePath;
        }

        protected TiXLIoException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    /// <summary>
    /// Exception thrown when guardrail limits are exceeded
    /// </summary>
    [Serializable]
    public class TiXLGuardrailException : TiXLException
    {
        public string LimitType { get; }
        public long CurrentValue { get; }
        public long LimitValue { get; }

        public TiXLGuardrailException(string limitType, long current, long limit)
            : base($"Guardrail '{limitType}' exceeded: {current} (limit: {limit})")
        {
            LimitType = limitType;
            CurrentValue = current;
            LimitValue = limit;
        }

        protected TiXLGuardrailException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    /// <summary>
    /// Exception thrown when performance monitoring fails
    /// </summary>
    [Serializable]
    public class TiXLPerformanceException : TiXLException
    {
        public string MetricName { get; }
        public double Value { get; }

        public TiXLPerformanceException(string metricName, double value, string message)
            : base($"Performance metric '{metricName}' with value {value} failed: {message}")
        {
            MetricName = metricName;
            Value = value;
        }

        protected TiXLPerformanceException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    /// <summary>
    /// Exception thrown when validation fails
    /// </summary>
    [Serializable]
    public class TiXLValidationException : TiXLException
    {
        public string ValidationRule { get; }
        public object Value { get; }

        public TiXLValidationException(string rule, object value, string message)
            : base($"Validation rule '{rule}' failed for value '{value}': {message}")
        {
            ValidationRule = rule;
            Value = value;
        }

        protected TiXLValidationException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    /// <summary>
    /// Exception thrown when PSO cache operation fails
    /// </summary>
    [Serializable]
    public class TiXLPSOException : TiXLException
    {
        public string MaterialName { get; }
        public string Operation { get; }

        public TiXLPSOException(string materialName, string operation, string message, Exception inner = null)
            : base($"PSO cache operation '{operation}' failed for material '{materialName}': {message}", inner)
        {
            MaterialName = materialName;
            Operation = operation;
        }

        protected TiXLPSOException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    /// <summary>
    /// Exception thrown when shader compilation fails
    /// </summary>
    [Serializable]
    public class TiXLShaderException : TiXLException
    {
        public string ShaderType { get; }
        public string ShaderPath { get; }

        public TiXLShaderException(string shaderType, string shaderPath, string message, Exception inner = null)
            : base($"Shader compilation failed - Type: {shaderType}, Path: {shaderPath}. {message}", inner)
        {
            ShaderType = shaderType;
            ShaderPath = shaderPath;
        }

        protected TiXLShaderException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    /// <summary>
    /// Exception thrown when audio-visual synchronization fails
    /// </summary>
    [Serializable]
    public class TiXLAudioVisualSyncException : TiXLException
    {
        public TimeSpan Latency { get; }
        public double ExpectedLatency { get; }

        public TiXLAudioVisualSyncException(TimeSpan actualLatency, double expectedLatency, string message)
            : base($"Audio-visual sync failed. Actual latency: {actualLatency.TotalMilliseconds}ms, Expected: {expectedLatency}ms. {message}")
        {
            Latency = actualLatency;
            ExpectedLatency = expectedLatency;
        }

        protected TiXLAudioVisualSyncException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    /// <summary>
    /// Exception thrown when incremental evaluation fails
    /// </summary>
    [Serializable]
    public class TiXLIncrementalEvaluationException : TiXLException
    {
        public string NodeId { get; }
        public int DirtyNodeCount { get; }

        public TiXLIncrementalEvaluationException(string nodeId, int dirtyNodeCount, string message, Exception inner = null)
            : base($"Incremental evaluation failed for node '{nodeId}' with {dirtyNodeCount} dirty nodes: {message}", inner)
        {
            NodeId = nodeId;
            DirtyNodeCount = dirtyNodeCount;
        }

        protected TiXLIncrementalEvaluationException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}