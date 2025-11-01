// Correlation ID Middleware and Integration Utilities
// Provides correlation ID propagation and operation tracking

using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;

namespace TiXL.Logging.Correlation
{
    /// <summary>
    /// Correlation ID middleware for HTTP requests
    /// </summary>
    public class CorrelationIdMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ICorrelationIdProvider _correlationIdProvider;
        private readonly CorrelationIdOptions _options;

        public CorrelationIdMiddleware(
            RequestDelegate next,
            ICorrelationIdProvider correlationIdProvider,
            IOptions<CorrelationIdOptions> options)
        {
            _next = next;
            _correlationIdProvider = correlationIdProvider;
            _options = options.Value;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Extract or generate correlation ID
            var correlationId = GetCorrelationId(context);
            
            // Set the correlation ID for this request
            _correlationIdProvider.SetCorrelationId(correlationId);

            // Add correlation ID to response headers
            context.Response.Headers.TryAdd(_options.HeaderName, correlationId);

            // Add correlation ID to response headers (legacy compatibility)
            if (!string.IsNullOrEmpty(_options.ResponseHeaderName) && _options.ResponseHeaderName != _options.HeaderName)
            {
                context.Response.Headers.TryAdd(_options.ResponseHeaderName, correlationId);
            }

            // Add correlation ID to the logging context
            using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
            {
                await _next(context);
            }
        }

        private string GetCorrelationId(HttpContext context)
        {
            // Check header first
            if (context.Request.Headers.TryGetValue(_options.HeaderName, out var headerValue) && 
                !string.IsNullOrEmpty(headerValue))
            {
                return headerValue!;
            }

            // Check query string
            if (context.Request.Query.TryGetValue(_options.QueryStringParameterName, out var queryValue) && 
                !string.IsNullOrEmpty(queryValue))
            {
                return queryValue!;
            }

            // Check for legacy header names
            foreach (var legacyHeader in _options.LegacyHeaderNames)
            {
                if (context.Request.Headers.TryGetValue(legacyHeader, out var legacyValue) && 
                    !string.IsNullOrEmpty(legacyValue))
                {
                    return legacyValue!;
                }
            }

            // Generate new correlation ID
            return _correlationIdProvider.CreateCorrelationId();
        }
    }

    /// <summary>
    /// Options for correlation ID middleware configuration
    /// </summary>
    public class CorrelationIdOptions
    {
        public string HeaderName { get; set; } = "X-Correlation-Id";
        public string ResponseHeaderName { get; set; } = "X-Correlation-Id";
        public string QueryStringParameterName { get; set; } = "correlationId";
        public List<string> LegacyHeaderNames { get; set; } = new()
        {
            "X-Request-ID",
            "X-Trace-ID",
            "Request-ID"
        };
        public bool IncludeInResponse { get; set; } = true;
        public bool CreateIfMissing { get; set; } = true;
    }

    /// <summary>
    /// Operation tracker for correlation-based operation tracking
    /// </summary>
    public interface IOperationTracker
    {
        string StartOperation(string operationName, string? parentOperationId = null, Dictionary<string, object?>? metadata = null);
        void EndOperation(string operationId, bool success = true, string? errorMessage = null);
        void AddOperationMetadata(string operationId, string key, object? value);
        List<TrackedOperation> GetOperations(string? correlationId = null);
        void ClearCompletedOperations(TimeSpan? olderThan = null);
    }

    public class OperationTracker : IOperationTracker
    {
        private readonly ILogger _logger;
        private readonly ICorrelationIdProvider _correlationIdProvider;
        private readonly ConcurrentDictionary<string, TrackedOperation> _operations = new();

        public OperationTracker(
            ILogger logger,
            ICorrelationIdProvider correlationIdProvider)
        {
            _logger = logger;
            _correlationIdProvider = correlationIdProvider;
        }

        public string StartOperation(string operationName, string? parentOperationId = null, Dictionary<string, object?>? metadata = null)
        {
            var operationId = Guid.NewGuid().ToString("N")[..8];
            var correlationId = _correlationIdProvider.GetCorrelationId();

            var operation = new TrackedOperation
            {
                OperationId = operationId,
                CorrelationId = correlationId,
                Name = operationName,
                ParentOperationId = parentOperationId,
                StartTime = DateTimeOffset.UtcNow,
                Status = OperationStatus.Running,
                Metadata = metadata ?? new Dictionary<string, object?>()
            };

            _operations[operationId] = operation;

            _logger.Information("Operation started: {OperationName} (ID: {OperationId}, Parent: {ParentOperationId}, Correlation: {CorrelationId})",
                operationName, operationId, parentOperationId ?? "none", correlationId);

            return operationId;
        }

        public void EndOperation(string operationId, bool success = true, string? errorMessage = null)
        {
            if (_operations.TryGetValue(operationId, out var operation))
            {
                operation.EndTime = DateTimeOffset.UtcNow;
                operation.Status = success ? OperationStatus.Completed : OperationStatus.Failed;
                operation.ErrorMessage = errorMessage;
                operation.Duration = operation.EndTime - operation.StartTime;

                var level = success ? LogEventLevel.Information : LogEventLevel.Error;
                var logMessage = success 
                    ? "Operation completed: {OperationName} (ID: {OperationId}, Duration: {Duration:F3}ms, Correlation: {CorrelationId})"
                    : "Operation failed: {OperationName} (ID: {OperationId}, Duration: {Duration:F3}ms, Error: {ErrorMessage}, Correlation: {CorrelationId})";

                _logger.Write(level, logMessage,
                    operation.Name, operationId, operation.Duration.Value.TotalMilliseconds, 
                    errorMessage ?? "none", operation.CorrelationId);
            }
        }

        public void AddOperationMetadata(string operationId, string key, object? value)
        {
            if (_operations.TryGetValue(operationId, out var operation))
            {
                operation.Metadata[key] = value;
            }
        }

        public List<TrackedOperation> GetOperations(string? correlationId = null)
        {
            var operations = _operations.Values.ToList();
            
            if (!string.IsNullOrEmpty(correlationId))
            {
                operations = operations.Where(op => op.CorrelationId == correlationId).ToList();
            }

            return operations;
        }

        public void ClearCompletedOperations(TimeSpan? olderThan = null)
        {
            var cutoffTime = DateTimeOffset.UtcNow - (olderThan ?? TimeSpan.FromHours(1));
            
            var completedOperations = _operations.Values
                .Where(op => op.Status != OperationStatus.Running && op.EndTime <= cutoffTime)
                .ToList();

            foreach (var operation in completedOperations)
            {
                _operations.TryRemove(operation.OperationId, out _);
            }

            if (completedOperations.Count > 0)
            {
                _logger.Debug("Cleared {Count} completed operations older than {CutoffTime}", 
                    completedOperations.Count, cutoffTime);
            }
        }
    }

    /// <summary>
    /// Transaction logger for tracking transactions across modules
    /// </summary>
    public interface ITransactionLogger
    {
        string StartTransaction(string transactionName, string? parentId = null);
        void LogTransactionEvent(string transactionId, string eventName, Dictionary<string, object?>? data = null);
        void EndTransaction(string transactionId, bool success = true, string? result = null);
        void AddTransactionMetadata(string transactionId, string key, object? value);
        List<TransactionInfo> GetTransactions(string? correlationId = null);
    }

    public class TransactionLogger : ITransactionLogger
    {
        private readonly ILogger _logger;
        private readonly ICorrelationIdProvider _correlationIdProvider;
        private readonly ConcurrentDictionary<string, TransactionInfo> _transactions = new();

        public TransactionLogger(
            ILogger logger,
            ICorrelationIdProvider correlationIdProvider)
        {
            _logger = logger;
            _correlationIdProvider = correlationIdProvider;
        }

        public string StartTransaction(string transactionName, string? parentId = null)
        {
            var transactionId = Guid.NewGuid().ToString("N")[..8];
            var correlationId = _correlationIdProvider.GetCorrelationId();

            var transaction = new TransactionInfo
            {
                TransactionId = transactionId,
                CorrelationId = correlationId,
                Name = transactionName,
                ParentId = parentId,
                StartTime = DateTimeOffset.UtcNow,
                Status = TransactionStatus.Started,
                Events = new List<TransactionEvent>()
            };

            _transactions[transactionId] = transaction;

            _logger.Information("Transaction started: {TransactionName} (ID: {TransactionId}, Parent: {ParentId}, Correlation: {CorrelationId})",
                transactionName, transactionId, parentId ?? "none", correlationId);

            return transactionId;
        }

        public void LogTransactionEvent(string transactionId, string eventName, Dictionary<string, object?>? data = null)
        {
            if (_transactions.TryGetValue(transactionId, out var transaction))
            {
                var transactionEvent = new TransactionEvent
                {
                    EventName = eventName,
                    Timestamp = DateTimeOffset.UtcNow,
                    Data = data ?? new Dictionary<string, object?>()
                };

                transaction.Events.Add(transactionEvent);

                _logger.Information("Transaction event: {TransactionName} (ID: {TransactionId}) - {EventName} {Data}",
                    transaction.Name, transactionId, eventName, data?.Count > 0 ? $"({data.Count} items)" : "");
            }
        }

        public void EndTransaction(string transactionId, bool success = true, string? result = null)
        {
            if (_transactions.TryGetValue(transactionId, out var transaction))
            {
                transaction.EndTime = DateTimeOffset.UtcNow;
                transaction.Status = success ? TransactionStatus.Completed : TransactionStatus.Failed;
                transaction.Result = result;
                transaction.Duration = transaction.EndTime - transaction.StartTime;

                var level = success ? LogEventLevel.Information : LogEventLevel.Error;
                var logMessage = success 
                    ? "Transaction completed: {TransactionName} (ID: {TransactionId}, Duration: {Duration:F3}ms, Result: {Result}, Correlation: {CorrelationId})"
                    : "Transaction failed: {TransactionName} (ID: {TransactionId}, Duration: {Duration:F3}ms, Result: {Result}, Correlation: {CorrelationId})";

                _logger.Write(level, logMessage,
                    transaction.Name, transactionId, transaction.Duration.Value.TotalMilliseconds, 
                    result ?? "none", transaction.CorrelationId);
            }
        }

        public void AddTransactionMetadata(string transactionId, string key, object? value)
        {
            if (_transactions.TryGetValue(transactionId, out var transaction))
            {
                transaction.Metadata[key] = value;
            }
        }

        public List<TransactionInfo> GetTransactions(string? correlationId = null)
        {
            var transactions = _transactions.Values.ToList();
            
            if (!string.IsNullOrEmpty(correlationId))
            {
                transactions = transactions.Where(t => t.CorrelationId == correlationId).ToList();
            }

            return transactions;
        }
    }

    /// <summary>
    /// WebSocket correlation middleware for real-time communication
    /// </summary>
    public class WebSocketCorrelationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ICorrelationIdProvider _correlationIdProvider;

        public WebSocketCorrelationMiddleware(
            RequestDelegate next,
            ICorrelationIdProvider correlationIdProvider)
        {
            _next = next;
            _correlationIdProvider = correlationIdProvider;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                var correlationId = context.Request.Query.TryGetValue("correlationId", out var queryValue) && !string.IsNullOrEmpty(queryValue)
                    ? queryValue.ToString()
                    : _correlationIdProvider.CreateCorrelationId();

                _correlationIdProvider.SetCorrelationId(correlationId);

                using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
                using (Serilog.Context.LogContext.PushProperty("WebSocketRequest", true))
                {
                    await _next(context);
                }
            }
            else
            {
                await _next(context);
            }
        }
    }

    /// <summary>
    /// Service collection extensions for correlation functionality
    /// </summary>
    public static class CorrelationServiceExtensions
    {
        public static IServiceCollection AddTiXLCorrelation(this IServiceCollection services, Action<CorrelationIdOptions>? configure = null)
        {
            if (configure != null)
            {
                services.Configure(configure);
            }
            else
            {
                services.Configure<CorrelationIdOptions>(options => { });
            }

            services.AddSingleton<IOperationTracker, OperationTracker>();
            services.AddSingleton<ITransactionLogger, TransactionLogger>();

            return services;
        }

        public static IServiceCollection AddCorrelationIdMiddleware(this IServiceCollection services)
        {
            services.AddTransient<CorrelationIdMiddleware>();
            services.AddTransient<WebSocketCorrelationMiddleware>();
            return services;
        }
    }

    /// <summary>
    /// Application builder extensions for correlation middleware
    /// </summary>
    public static class CorrelationApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseTiXLCorrelation(this IApplicationBuilder app)
        {
            return app.UseMiddleware<CorrelationIdMiddleware>();
        }

        public static IApplicationBuilder UseWebSocketCorrelation(this IApplicationBuilder app)
        {
            return app.UseMiddleware<WebSocketCorrelationMiddleware>();
        }
    }

    // Supporting data structures

    public class TrackedOperation
    {
        public string OperationId { get; set; } = string.Empty;
        public string CorrelationId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? ParentOperationId { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }
        public TimeSpan? Duration { get; set; }
        public OperationStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, object?> Metadata { get; set; } = new();
    }

    public class TransactionInfo
    {
        public string TransactionId { get; set; } = string.Empty;
        public string CorrelationId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? ParentId { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset? EndTime { get; set; }
        public TimeSpan? Duration { get; set; }
        public TransactionStatus Status { get; set; }
        public string? Result { get; set; }
        public List<TransactionEvent> Events { get; set; } = new();
        public Dictionary<string, object?> Metadata { get; set; } = new();
    }

    public class TransactionEvent
    {
        public string EventName { get; set; } = string.Empty;
        public DateTimeOffset Timestamp { get; set; }
        public Dictionary<string, object?> Data { get; set; } = new();
    }

    public enum OperationStatus
    {
        Running,
        Completed,
        Failed
    }

    public enum TransactionStatus
    {
        Started,
        Completed,
        Failed
    }
}