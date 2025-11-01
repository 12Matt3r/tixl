// TiXL Logging Enrichers and Configuration Helpers
// Provides additional context and enrichment for structured logging

using Serilog;
using Serilog.Core;
using Serilog.Events;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace TiXL.Logging.Enrichers
{
    /// <summary>
    /// Base interface for log enrichers
    /// </summary>
    public interface ILogEnricher
    {
        void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory);
        string Name { get; }
    }

    /// <summary>
    /// Module context enricher
    /// Adds current module context to all log events
    /// </summary>
    public class ModuleContextEnricher : ILogEnricher
    {
        private readonly AsyncLocal<string?> _currentModule = new();

        public string Name => "ModuleContext";

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            if (!string.IsNullOrEmpty(_currentModule.Value))
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ModuleContext", _currentModule.Value));
            }
        }

        public void SetModuleContext(string moduleName)
        {
            _currentModule.Value = moduleName;
        }

        public void ClearModuleContext()
        {
            _currentModule.Value = null;
        }
    }

    /// <summary>
    /// Operation context enricher
    /// Adds current operation context to all log events
    /// </summary>
    public class OperationContextEnricher : ILogEnricher
    {
        private readonly AsyncLocal<OperationContext?> _currentOperation = new();

        public string Name => "OperationContext";

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var operation = _currentOperation.Value;
            if (operation != null)
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("OperationName", operation.Name));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("OperationId", operation.Id));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("OperationStartTime", operation.StartTime));
                
                if (operation.ParentOperationId != null)
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ParentOperationId", operation.ParentOperationId));
                }

                if (operation.Tags.Any())
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("OperationTags", operation.Tags));
                }
            }
        }

        public void SetOperationContext(string name, string? parentOperationId = null, IEnumerable<string>? tags = null)
        {
            _currentOperation.Value = new OperationContext
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Name = name,
                StartTime = DateTimeOffset.UtcNow,
                ParentOperationId = parentOperationId,
                Tags = tags?.ToList() ?? new List<string>()
            };
        }

        public void ClearOperationContext()
        {
            _currentOperation.Value = null;
        }

        public OperationContext? GetCurrentOperationContext() => _currentOperation.Value;
    }

    /// <summary>
    /// User context enricher
    /// Adds current user context to all log events
    /// </summary>
    public class UserContextEnricher : ILogEnricher
    {
        private readonly AsyncLocal<UserContext?> _currentUser = new();

        public string Name => "UserContext";

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var user = _currentUser.Value;
            if (user != null)
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserId", user.Id));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserName", user.Name));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserRole", user.Role));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserSessionId", user.SessionId));
                
                if (user.Claims.Any())
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserClaims", user.Claims));
                }
            }
        }

        public void SetUserContext(ClaimsPrincipal principal)
        {
            if (principal.Identity?.IsAuthenticated == true)
            {
                _currentUser.Value = new UserContext
                {
                    Id = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous",
                    Name = principal.Identity.Name ?? "Anonymous",
                    Role = principal.FindFirst(ClaimTypes.Role)?.Value ?? "User",
                    SessionId = principal.FindFirst("SessionId")?.Value ?? Guid.NewGuid().ToString("N")[..8],
                    Claims = principal.Claims.Select(c => new { c.Type, c.Value }).ToList()
                };
            }
            else
            {
                _currentUser.Value = new UserContext
                {
                    Id = "Anonymous",
                    Name = "Anonymous",
                    Role = "Guest",
                    SessionId = "Anonymous",
                    Claims = new List<object>()
                };
            }
        }

        public void ClearUserContext()
        {
            _currentUser.Value = null;
        }

        public UserContext? GetCurrentUserContext() => _currentUser.Value;
    }

    /// <summary>
    /// Performance context enricher
    /// Adds performance metrics to all log events
    /// </summary>
    public class PerformanceContextEnricher : ILogEnricher
    {
        private readonly AsyncLocal<PerformanceContext?> _currentPerformance = new();

        public string Name => "PerformanceContext";

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var perf = _currentPerformance.Value;
            if (perf != null)
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("OperationDuration", perf.Duration.TotalMilliseconds));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("MemoryUsed", perf.MemoryUsed));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CpuTime", perf.CpuTime.TotalMilliseconds));
                
                if (perf.CustomMetrics.Any())
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("CustomMetrics", perf.CustomMetrics));
                }
            }
        }

        public void SetPerformanceContext(TimeSpan duration, long memoryUsed, TimeSpan cpuTime, Dictionary<string, double>? customMetrics = null)
        {
            _currentPerformance.Value = new PerformanceContext
            {
                Duration = duration,
                MemoryUsed = memoryUsed,
                CpuTime = cpuTime,
                CustomMetrics = customMetrics ?? new Dictionary<string, double>()
            };
        }

        public void ClearPerformanceContext()
        {
            _currentPerformance.Value = null;
        }

        public PerformanceContext? GetCurrentPerformanceContext() => _currentPerformance.Value;
    }

    /// <summary>
    /// Request context enricher
    /// Adds HTTP request context to all log events
    /// </summary>
    public class RequestContextEnricher : ILogEnricher
    {
        private readonly AsyncLocal<RequestContext?> _currentRequest = new();

        public string Name => "RequestContext";

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var request = _currentRequest.Value;
            if (request != null)
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("RequestId", request.RequestId));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("RequestPath", request.Path));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("RequestMethod", request.Method));
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("RequestStartTime", request.StartTime));
                
                if (!string.IsNullOrEmpty(request.UserAgent))
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserAgent", request.UserAgent));
                }

                if (!string.IsNullOrEmpty(request.IpAddress))
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("IpAddress", request.IpAddress));
                }

                if (request.Headers.Any())
                {
                    logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("RequestHeaders", request.Headers));
                }
            }
        }

        public void SetRequestContext(string path, string method, string? userAgent = null, string? ipAddress = null, Dictionary<string, string>? headers = null)
        {
            _currentRequest.Value = new RequestContext
            {
                RequestId = Guid.NewGuid().ToString("N")[..8],
                Path = path,
                Method = method,
                StartTime = DateTimeOffset.UtcNow,
                UserAgent = userAgent,
                IpAddress = ipAddress,
                Headers = headers ?? new Dictionary<string, string>()
            };
        }

        public void ClearRequestContext()
        {
            _currentRequest.Value = null;
        }

        public RequestContext? GetCurrentRequestContext() => _currentRequest.Value;
    }

    /// <summary>
    /// Environment enricher
    /// Adds environment information to all log events
    /// </summary>
    public class EnvironmentEnricher : ILogEnricher
    {
        private readonly Lazy<EnvironmentInfo> _environmentInfo;

        public string Name => "Environment";

        public EnvironmentEnricher()
        {
            _environmentInfo = new Lazy<EnvironmentInfo>(() => new EnvironmentInfo
            {
                MachineName = Environment.MachineName,
                OSVersion = Environment.OSVersion.ToString(),
                ProcessorCount = Environment.ProcessorCount,
                WorkingSet = Environment.WorkingSet,
                DotNetVersion = Environment.Version.ToString(),
                UserDomainName = Environment.UserDomainName,
                UserName = Environment.UserName,
                Is64BitProcess = Environment.Is64BitProcess,
                Is64BitOperatingSystem = Environment.Is64BitOperatingSystem,
                SystemPageSize = Environment.SystemPageSize,
                CommandLine = Environment.CommandLine,
                CurrentDirectory = Directory.GetCurrentDirectory(),
                Timestamp = DateTimeOffset.UtcNow
            });
        }

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var info = _environmentInfo.Value;
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("MachineName", info.MachineName));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("OSVersion", info.OSVersion));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ProcessorCount", info.ProcessorCount));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("DotNetVersion", info.DotNetVersion));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Is64BitProcess", info.Is64BitProcess));
        }

        public EnvironmentInfo GetEnvironmentInfo() => _environmentInfo.Value;
    }

    /// <summary>
    /// Performance monitoring enricher
    /// Continuously adds performance metrics to log events
    /// </summary>
    public class PerformanceMonitoringEnricher : ILogEnricher
    {
        private readonly Lazy<PerformanceMetrics> _metrics;
        private readonly Timer _metricsTimer;

        public string Name => "PerformanceMonitoring";

        public PerformanceMonitoringEnricher()
        {
            _metrics = new Lazy<PerformanceMetrics>(() => new PerformanceMetrics
            {
                Timestamp = DateTimeOffset.UtcNow,
                WorkingSet = Environment.WorkingSet,
                PrivateMemory = Process.GetCurrentProcess().PrivateMemorySize64,
                VirtualMemory = Process.GetCurrentProcess().VirtualMemorySize64,
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2),
                TotalProcessorTime = Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds
            });

            _metricsTimer = new Timer(UpdateMetrics, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        private void UpdateMetrics(object? state)
        {
            var process = Process.GetCurrentProcess();
            _metrics.Value.WorkingSet = Environment.WorkingSet;
            _metrics.Value.PrivateMemory = process.PrivateMemorySize64;
            _metrics.Value.VirtualMemory = process.VirtualMemorySize64;
            _metrics.Value.TotalProcessorTime = process.TotalProcessorTime.TotalMilliseconds;
            _metrics.Value.Timestamp = DateTimeOffset.UtcNow;
        }

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var metrics = _metrics.Value;
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ProcessWorkingSetMB", metrics.WorkingSet / 1024 / 1024));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ProcessPrivateMemoryMB", metrics.PrivateMemory / 1024 / 1024));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ProcessVirtualMemoryMB", metrics.VirtualMemory / 1024 / 1024));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("GCGen0Collections", metrics.Gen0Collections));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("GCGen1Collections", metrics.Gen1Collections));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("GCGen2Collections", metrics.Gen2Collections));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TotalProcessorTimeMs", metrics.TotalProcessorTime));
        }

        public void Dispose()
        {
            _metricsTimer?.Dispose();
        }
    }

    // Supporting data structures

    public class OperationContext
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTimeOffset StartTime { get; set; }
        public string? ParentOperationId { get; set; }
        public List<string> Tags { get; set; } = new();
    }

    public class UserContext
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public List<object> Claims { get; set; } = new();
    }

    public class PerformanceContext
    {
        public TimeSpan Duration { get; set; }
        public long MemoryUsed { get; set; }
        public TimeSpan CpuTime { get; set; }
        public Dictionary<string, double> CustomMetrics { get; set; } = new();
    }

    public class RequestContext
    {
        public string RequestId { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public DateTimeOffset StartTime { get; set; }
        public string? UserAgent { get; set; }
        public string? IpAddress { get; set; }
        public Dictionary<string, string> Headers { get; set; } = new();
    }

    public class EnvironmentInfo
    {
        public string MachineName { get; set; } = string.Empty;
        public string OSVersion { get; set; } = string.Empty;
        public int ProcessorCount { get; set; }
        public long WorkingSet { get; set; }
        public string DotNetVersion { get; set; } = string.Empty;
        public string UserDomainName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public bool Is64BitProcess { get; set; }
        public bool Is64BitOperatingSystem { get; set; }
        public int SystemPageSize { get; set; }
        public string CommandLine { get; set; } = string.Empty;
        public string CurrentDirectory { get; set; } = string.Empty;
        public DateTimeOffset Timestamp { get; set; }
    }

    public class PerformanceMetrics
    {
        public DateTimeOffset Timestamp { get; set; }
        public long WorkingSet { get; set; }
        public long PrivateMemory { get; set; }
        public long VirtualMemory { get; set; }
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }
        public double TotalProcessorTime { get; set; }
    }

    /// <summary>
    /// Service collection extensions for logging enrichers
    /// </summary>
    public static class LoggingEnricherExtensions
    {
        public static IServiceCollection AddTiXLLoggingEnrichers(this IServiceCollection services)
        {
            services.AddSingleton<ModuleContextEnricher>();
            services.AddSingleton<OperationContextEnricher>();
            services.AddSingleton<UserContextEnricher>();
            services.AddSingleton<PerformanceContextEnricher>();
            services.AddSingleton<RequestContextEnricher>();
            services.AddSingleton<EnvironmentEnricher>();
            services.AddSingleton<PerformanceMonitoringEnricher>();

            return services;
        }

        public static IServiceCollection AddOperationContext(this IServiceCollection services, string initialOperation = "")
        {
            services.AddSingleton<IOperationContextService, OperationContextService>();
            services.AddSingleton<IOperationContextAccessor, OperationContextAccessor>();
            
            if (!string.IsNullOrEmpty(initialOperation))
            {
                services.AddSingleton(new InitialOperationContext(initialOperation));
            }

            return services;
        }
    }

    /// <summary>
    /// Operation context service for dependency injection
    /// </summary>
    public interface IOperationContextService
    {
        void SetOperation(string name, string? parentOperationId = null, IEnumerable<string>? tags = null);
        void ClearOperation();
        OperationContext? GetCurrentOperation();
    }

    public class OperationContextService : IOperationContextService
    {
        private readonly OperationContextEnricher _enricher;

        public OperationContextService(OperationContextEnricher enricher)
        {
            _enricher = enricher;
        }

        public void SetOperation(string name, string? parentOperationId = null, IEnumerable<string>? tags = null)
        {
            _enricher.SetOperationContext(name, parentOperationId, tags);
        }

        public void ClearOperation()
        {
            _enricher.ClearOperationContext();
        }

        public OperationContext? GetCurrentOperation()
        {
            return _enricher.GetCurrentOperationContext();
        }
    }

    /// <summary>
    /// Operation context accessor for components that need access to operation context
    /// </summary>
    public interface IOperationContextAccessor
    {
        OperationContext? Current { get; }
    }

    public class OperationContextAccessor : IOperationContextAccessor
    {
        private readonly OperationContextEnricher _enricher;

        public OperationContextAccessor(OperationContextEnricher enricher)
        {
            _enricher = enricher;
        }

        public OperationContext? Current => _enricher.GetCurrentOperationContext();
    }

    public class InitialOperationContext
    {
        public string OperationName { get; }

        public InitialOperationContext(string operationName)
        {
            OperationName = operationName;
        }
    }
}