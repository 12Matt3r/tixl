using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TiXL.PerformanceSuite.Models;

namespace TiXL.PerformanceSuite.Core
{
    /// <summary>
    /// Service responsible for managing performance alerts and notifications
    /// </summary>
    public class AlertService
    {
        private readonly ILogger<AlertService> _logger;
        private readonly Dictionary<string, DateTime> _lastAlertTimes;
        private readonly Dictionary<string, int> _alertCounts;
        private readonly AlertConfiguration _config;

        public AlertService(ILogger<AlertService> logger, AlertConfiguration? config = null)
        {
            _logger = logger;
            _lastAlertTimes = new Dictionary<string, DateTime>();
            _alertCounts = new Dictionary<string, int>();
            _config = config ?? new AlertConfiguration();
        }

        /// <summary>
        /// Check for performance regressions and send alerts if necessary
        /// </summary>
        public async Task<List<PerformanceAlert>> CheckForRegressions(PerformanceAnalysis analysis)
        {
            var alerts = new List<PerformanceAlert>();

            _logger.LogInformation($"🔍 Checking for regressions: {analysis.RegressionCount} found");

            foreach (var regression in analysis.Regressions)
            {
                var alert = await CreateRegressionAlert(regression);
                
                if (await ShouldSendAlert(alert))
                {
                    alerts.Add(alert);
                    await SendAlert(alert);
                }
            }

            // Check for critical performance thresholds
            var thresholdAlerts = await CheckPerformanceThresholds(analysis);
            alerts.AddRange(thresholdAlerts);

            // Check for system-level issues
            var systemAlerts = await CheckSystemHealth();
            alerts.AddRange(systemAlerts);

            return alerts;
        }

        /// <summary>
        /// Create a performance regression alert
        /// </summary>
        private async Task<PerformanceAlert> CreateRegressionAlert(RegressionInfo regression)
        {
            var severity = DetermineSeverity(regression.RegressionPercent);
            var message = $"Performance regression detected in {regression.BenchmarkName}.{regression.MetricName}: " +
                         $"{regression.RegressionPercent:F1}% slower than baseline ({regression.BaselineValue:F3} → {regression.CurrentValue:F3} {regression.Unit})";

            return new PerformanceAlert
            {
                Id = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                Type = AlertType.Regression,
                Severity = severity,
                Title = $"Performance Regression: {regression.BenchmarkName}",
                Message = message,
                Source = $"{regression.BenchmarkName}.{regression.MetricName}",
                Metadata = new Dictionary<string, object>
                {
                    ["BenchmarkName"] = regression.BenchmarkName,
                    ["MetricName"] = regression.MetricName,
                    ["RegressionPercent"] = regression.RegressionPercent,
                    ["BaselineValue"] = regression.BaselineValue,
                    ["CurrentValue"] = regression.CurrentValue,
                    ["Unit"] = regression.Unit
                }
            };
        }

        private async Task<List<PerformanceAlert>> CheckPerformanceThresholds(PerformanceAnalysis analysis)
        {
            var alerts = new List<PerformanceAlert>();

            foreach (var regression in analysis.Regressions)
            {
                // Check frame time thresholds
                if (regression.MetricName.Contains("FrameTime") || regression.MetricName.Contains("Frame"))
                {
                    var frameTimeThreshold = _config.FrameTimeThresholdMs;
                    if (regression.CurrentValue > frameTimeThreshold)
                    {
                        var alert = new PerformanceAlert
                        {
                            Id = Guid.NewGuid().ToString(),
                            Timestamp = DateTime.UtcNow,
                            Type = AlertType.ThresholdExceeded,
                            Severity = AlertSeverity.High,
                            Title = $"Frame Time Threshold Exceeded",
                            Message = $"Frame time ({regression.CurrentValue:F2}ms) exceeds threshold of {frameTimeThreshold}ms",
                            Source = regression.BenchmarkName,
                            Metadata = new Dictionary<string, object>
                            {
                                ["CurrentValue"] = regression.CurrentValue,
                                ["Threshold"] = frameTimeThreshold,
                                ["Unit"] = "ms"
                            }
                        };

                        if (await ShouldSendAlert(alert))
                        {
                            alerts.Add(alert);
                            await SendAlert(alert);
                        }
                    }
                }

                // Check memory usage thresholds
                if (regression.MetricName.Contains("Memory") || regression.MetricName.Contains("Allocation"))
                {
                    var memoryThreshold = _config.MemoryThresholdMB;
                    var memoryMB = regression.CurrentValue / (1024 * 1024); // Convert bytes to MB
                    
                    if (memoryMB > memoryThreshold)
                    {
                        var alert = new PerformanceAlert
                        {
                            Id = Guid.NewGuid().ToString(),
                            Timestamp = DateTime.UtcNow,
                            Type = AlertType.ThresholdExceeded,
                            Severity = AlertSeverity.Medium,
                            Title = $"Memory Usage Threshold Exceeded",
                            Message = $"Memory usage ({memoryMB:F2}MB) exceeds threshold of {memoryThreshold}MB",
                            Source = regression.BenchmarkName,
                            Metadata = new Dictionary<string, object>
                            {
                                ["CurrentValue"] = memoryMB,
                                ["Threshold"] = memoryThreshold,
                                ["Unit"] = "MB"
                            }
                        };

                        if (await ShouldSendAlert(alert))
                        {
                            alerts.Add(alert);
                            await SendAlert(alert);
                        }
                    }
                }
            }

            return alerts;
        }

        private async Task<List<PerformanceAlert>> CheckSystemHealth()
        {
            var alerts = new List<PerformanceAlert>();

            try
            {
                // Check CPU usage
                var cpuUsage = GetCurrentCpuUsage();
                if (cpuUsage > _config.CpuUsageThresholdPercent)
                {
                    var alert = new PerformanceAlert
                    {
                        Id = Guid.NewGuid().ToString(),
                        Timestamp = DateTime.UtcNow,
                        Type = AlertType.SystemResource,
                        Severity = AlertSeverity.Medium,
                        Title = $"High CPU Usage Detected",
                        Message = $"CPU usage ({cpuUsage:F1}%) exceeds threshold of {_config.CpuUsageThresholdPercent}%",
                        Source = "SystemMonitoring",
                        Metadata = new Dictionary<string, object>
                        {
                            ["CurrentValue"] = cpuUsage,
                            ["Threshold"] = _config.CpuUsageThresholdPercent,
                            ["Unit"] = "%"
                        }
                    };

                    if (await ShouldSendAlert(alert))
                    {
                        alerts.Add(alert);
                        await SendAlert(alert);
                    }
                }

                // Check available memory
                var availableMemory = GetAvailableMemoryMB();
                if (availableMemory < _config.MinAvailableMemoryMB)
                {
                    var alert = new PerformanceAlert
                    {
                        Id = Guid.NewGuid().ToString(),
                        Timestamp = DateTime.UtcNow,
                        Type = AlertType.SystemResource,
                        Severity = AlertSeverity.High,
                        Title = $"Low Available Memory",
                        Message = $"Available memory ({availableMemory:F2}MB) is below threshold of {_config.MinAvailableMemoryMB}MB",
                        Source = "SystemMonitoring",
                        Metadata = new Dictionary<string, object>
                        {
                            ["CurrentValue"] = availableMemory,
                            ["Threshold"] = _config.MinAvailableMemoryMB,
                            ["Unit"] = "MB"
                        }
                    };

                    if (await ShouldSendAlert(alert))
                    {
                        alerts.Add(alert);
                        await SendAlert(alert);
                    }
                }

                // Check GC pressure
                var gcPressure = CalculateGCPressure();
                if (gcPressure > _config.GCPressureThreshold)
                {
                    var alert = new PerformanceAlert
                    {
                        Id = Guid.NewGuid().ToString(),
                        Timestamp = DateTime.UtcNow,
                        Type = AlertType.GarbageCollection,
                        Severity = AlertSeverity.Medium,
                        Title = $"High GC Pressure Detected",
                        Message = $"GC pressure score ({gcPressure:F2}) exceeds threshold of {_config.GCPressureThreshold}",
                        Source = "SystemMonitoring",
                        Metadata = new Dictionary<string, object>
                        {
                            ["CurrentValue"] = gcPressure,
                            ["Threshold"] = _config.GCPressureThreshold,
                            ["Unit"] = "score"
                        }
                    };

                    if (await ShouldSendAlert(alert))
                    {
                        alerts.Add(alert);
                        await SendAlert(alert);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking system health");
            }

            return alerts;
        }

        /// <summary>
        /// Send an alert through configured channels
        /// </summary>
        private async Task SendAlert(PerformanceAlert alert)
        {
            _logger.LogWarning($"🚨 ALERT [{alert.Severity}]: {alert.Title}");
            _logger.LogWarning($"   {alert.Message}");

            try
            {
                // Log alert to file
                await LogAlertToFile(alert);

                // Send to console (always enabled)
                await SendConsoleNotification(alert);

                // Send to other configured channels
                if (_config.EnableEmailNotifications)
                {
                    await SendEmailNotification(alert);
                }

                if (_config.EnableSlackNotifications)
                {
                    await SendSlackNotification(alert);
                }

                if (_config.EnableGitHubNotifications)
                {
                    await SendGitHubNotification(alert);
                }

                // Update alert tracking
                var alertKey = GetAlertKey(alert);
                _lastAlertTimes[alertKey] = DateTime.UtcNow;
                _alertCounts[alertKey] = _alertCounts.GetValueOrDefault(alertKey, 0) + 1;

                _logger.LogInformation($"✅ Alert sent successfully: {alert.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send alert: {alert.Id}");
            }
        }

        private async Task<bool> ShouldSendAlert(PerformanceAlert alert)
        {
            var alertKey = GetAlertKey(alert);
            var now = DateTime.UtcNow;

            // Check cooldown period
            if (_lastAlertTimes.TryGetValue(alertKey, out var lastAlertTime))
            {
                var timeSinceLastAlert = now - lastAlertTime;
                if (timeSinceLastAlert < _config.AlertCooldown)
                {
                    _logger.LogDebug($"Alert suppressed due to cooldown: {alert.Title}");
                    return false;
                }
            }

            // Check maximum alerts per hour
            var alertsInLastHour = _alertCounts.GetValueOrDefault(alertKey, 0);
            if (alertsInLastHour >= _config.MaxAlertsPerHour)
            {
                _logger.LogDebug($"Alert suppressed due to rate limit: {alert.Title}");
                return false;
            }

            return true;
        }

        private AlertSeverity DetermineSeverity(double regressionPercent)
        {
            return regressionPercent switch
            {
                >= 50 => AlertSeverity.Critical,
                >= 25 => AlertSeverity.High,
                >= 15 => AlertSeverity.Medium,
                >= 10 => AlertSeverity.Low,
                _ => AlertSeverity.Information
            };
        }

        private static string GetAlertKey(PerformanceAlert alert)
        {
            return $"{alert.Type}_{alert.Source}_{alert.Severity}";
        }

        private double GetCurrentCpuUsage()
        {
            try
            {
                using var process = Process.GetCurrentProcess();
                return process.TotalProcessorTime.TotalMilliseconds / Environment.TickCount * 100;
            }
            catch
            {
                return 0;
            }
        }

        private double GetAvailableMemoryMB()
        {
            try
            {
                using var process = Process.GetCurrentProcess();
                return (process.WorkingSet64 / (1024.0 * 1024.0));
            }
            catch
            {
                return 0;
            }
        }

        private double CalculateGCPressure()
        {
            var gen0Collections = GC.CollectionCount(0);
            var gen1Collections = GC.CollectionCount(1);
            var gen2Collections = GC.CollectionCount(2);
            
            // Simple GC pressure calculation
            return (gen0Collections * 1.0) + (gen1Collections * 2.0) + (gen2Collections * 3.0);
        }

        private async Task LogAlertToFile(PerformanceAlert alert)
        {
            try
            {
                var logPath = Path.Combine("./Logs", $"alerts-{DateTime.UtcNow:yyyyMMdd}.log");
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                
                var logEntry = $"[{alert.Timestamp:yyyy-MM-dd HH:mm:ss}] [{alert.Severity}] {alert.Title}: {alert.Message}\n";
                await File.AppendAllTextAsync(logPath, logEntry);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to log alert to file");
            }
        }

        private async Task SendConsoleNotification(PerformanceAlert alert)
        {
            var color = alert.Severity switch
            {
                AlertSeverity.Critical => ConsoleColor.Red,
                AlertSeverity.High => ConsoleColor.Magenta,
                AlertSeverity.Medium => ConsoleColor.Yellow,
                AlertSeverity.Low => ConsoleColor.Blue,
                _ => ConsoleColor.Gray
            };

            Console.ForegroundColor = color;
            Console.WriteLine($"🚨 [{alert.Severity}] {alert.Title}");
            Console.WriteLine($"   {alert.Message}");
            Console.WriteLine($"   Source: {alert.Source}");
            Console.ResetColor();
            Console.WriteLine();

            await Task.CompletedTask;
        }

        private async Task SendEmailNotification(PerformanceAlert alert)
        {
            // Implementation would depend on email service configuration
            _logger.LogInformation($"📧 Email alert would be sent for: {alert.Title}");
            await Task.CompletedTask;
        }

        private async Task SendSlackNotification(PerformanceAlert alert)
        {
            // Implementation would depend on Slack webhook configuration
            _logger.LogInformation($"💬 Slack alert would be sent for: {alert.Title}");
            await Task.CompletedTask;
        }

        private async Task SendGitHubNotification(PerformanceAlert alert)
        {
            // Implementation would depend on GitHub API configuration
            _logger.LogInformation($"🐙 GitHub issue would be created for: {alert.Title}");
            await Task.CompletedTask;
        }

        /// <summary>
        /// Get alert statistics
        /// </summary>
        public AlertStatistics GetAlertStatistics()
        {
            return new AlertStatistics
            {
                TotalAlertsSent = _alertCounts.Values.Sum(),
                UniqueAlertTypes = _alertCounts.Keys.Count,
                LastAlertTime = _lastAlertTimes.Values.DefaultIfEmpty().Max(),
                AlertRate = CalculateAlertRate()
            };
        }

        private double CalculateAlertRate()
        {
            if (!_lastAlertTimes.Any()) return 0;
            
            var timeSpan = DateTime.UtcNow - _lastAlertTimes.Values.Min();
            return timeSpan.TotalHours > 0 ? _alertCounts.Values.Sum() / timeSpan.TotalHours : 0;
        }
    }

    /// <summary>
    /// Configuration for alert service
    /// </summary>
    public class AlertConfiguration
    {
        public TimeSpan AlertCooldown { get; set; } = TimeSpan.FromMinutes(30);
        public int MaxAlertsPerHour { get; set; } = 10;
        public bool EnableEmailNotifications { get; set; } = true;
        public bool EnableSlackNotifications { get; set; } = false;
        public bool EnableGitHubNotifications { get; set; } = true;
        
        // Performance thresholds
        public double FrameTimeThresholdMs { get; set; } = 20.0;
        public double MemoryThresholdMB { get; set; } = 1024.0;
        public double CpuUsageThresholdPercent { get; set; } = 90.0;
        public double MinAvailableMemoryMB { get; set; } = 512.0;
        public double GCPressureThreshold { get; set; } = 100.0;
    }

    public class AlertStatistics
    {
        public int TotalAlertsSent { get; set; }
        public int UniqueAlertTypes { get; set; }
        public DateTime LastAlertTime { get; set; }
        public double AlertRate { get; set; } // alerts per hour
    }
}