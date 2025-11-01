using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TiXL.PerformanceSuite.Models;

namespace TiXL.PerformanceSuite.Core
{
    /// <summary>
    /// Service responsible for managing performance baselines
    /// </summary>
    public class BaselineManager
    {
        private readonly ILogger<BaselineManager> _logger;
        private readonly string _baselinesDirectory;
        private readonly string _defaultBaselinePath;

        public BaselineManager(ILogger<BaselineManager> logger)
        {
            _logger = logger;
            _baselinesDirectory = "./Baselines";
            _defaultBaselinePath = Path.Combine(_baselinesDirectory, "default.json");
            
            // Ensure baselines directory exists
            Directory.CreateDirectory(_baselinesDirectory);
        }

        /// <summary>
        /// Save current benchmark results as a performance baseline
        /// </summary>
        public async Task SaveCurrentResultsAsBaseline(string baselineName, string? customPath = null)
        {
            var path = customPath ?? Path.Combine(_baselinesDirectory, $"{baselineName}.json");
            
            _logger.LogInformation($"💾 Saving performance baseline: {baselineName} to {path}");

            try
            {
                // Capture current performance metrics
                var baselineData = await CaptureCurrentMetrics(baselineName);
                
                // Serialize and save
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(baselineData, options);
                await File.WriteAllTextAsync(path, json);

                // Update default baseline if this is the default
                if (baselineName.Equals("Default", StringComparison.OrdinalIgnoreCase))
                {
                    await File.WriteAllTextAsync(_defaultBaselinePath, json);
                }

                _logger.LogInformation($"✅ Performance baseline saved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to save baseline: {baselineName}");
                throw;
            }
        }

        /// <summary>
        /// Load a performance baseline
        /// </summary>
        public async Task<BaselineData?> LoadBaseline(string baselineName)
        {
            var path = GetBaselinePath(baselineName);
            
            _logger.LogInformation($"📂 Loading performance baseline: {baselineName} from {path}");

            try
            {
                if (!File.Exists(path))
                {
                    _logger.LogWarning($"Baseline not found: {path}");
                    return null;
                }

                var json = await File.ReadAllTextAsync(path);
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var baseline = JsonSerializer.Deserialize<BaselineData>(json, options);
                return baseline;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to load baseline: {baselineName}");
                return null;
            }
        }

        /// <summary>
        /// List all available baselines
        /// </summary>
        public async Task<List<BaselineInfo>> ListBaselines()
        {
            var baselines = new List<BaselineInfo>();

            try
            {
                if (!Directory.Exists(_baselinesDirectory))
                {
                    return baselines;
                }

                var files = Directory.GetFiles(_baselinesDirectory, "*.json");
                
                foreach (var file in files)
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(file);
                        var options = new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        };

                        var baseline = JsonSerializer.Deserialize<BaselineData>(json, options);
                        if (baseline != null)
                        {
                            baselines.Add(new BaselineInfo
                            {
                                Name = Path.GetFileNameWithoutExtension(file),
                                Path = file,
                                CreatedAt = baseline.CreatedAt,
                                ModifiedAt = File.GetLastWriteTime(file),
                                MetricCount = baseline.Metrics.Count,
                                Version = baseline.Version
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to read baseline file: {file}");
                    }
                }

                // Sort by creation date, newest first
                baselines = baselines.OrderByDescending(b => b.CreatedAt).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list baselines");
            }

            return baselines;
        }

        /// <summary>
        /// Delete a baseline
        /// </summary>
        public async Task<bool> DeleteBaseline(string baselineName)
        {
            var path = GetBaselinePath(baselineName);
            
            _logger.LogInformation($"🗑️ Deleting performance baseline: {baselineName}");

            try
            {
                if (!File.Exists(path))
                {
                    _logger.LogWarning($"Baseline not found: {path}");
                    return false;
                }

                File.Delete(path);
                
                // If this was the default baseline, create a new one from the most recent
                if (baselineName.Equals("Default", StringComparison.OrdinalIgnoreCase))
                {
                    var baselines = await ListBaselines();
                    var latest = baselines.OrderByDescending(b => b.CreatedAt).FirstOrDefault();
                    
                    if (latest != null && !latest.Name.Equals("Default"))
                    {
                        var latestData = await LoadBaseline(latest.Name);
                        if (latestData != null)
                        {
                            latestData.Name = "Default";
                            await SaveCurrentResultsAsBaseline("Default");
                        }
                    }
                }

                _logger.LogInformation("✅ Baseline deleted successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to delete baseline: {baselineName}");
                return false;
            }
        }

        /// <summary>
        /// Create a baseline from a specific commit or tag
        /// </summary>
        public async Task CreateBaselineFromHistoricalData(string commitHash, string baselineName)
        {
            _logger.LogInformation($"🔄 Creating baseline from historical data: {commitHash}");

            try
            {
                // This would typically involve querying a performance database
                // or historical benchmark results. For now, we'll create an empty baseline
                // with metadata indicating it was created from historical data.
                
                var baselineData = new BaselineData
                {
                    Name = baselineName,
                    CreatedAt = DateTime.UtcNow,
                    Version = "1.0",
                    GitCommit = commitHash,
                    Metadata = new Dictionary<string, object>
                    {
                        ["CreatedFromHistoricalData"] = true,
                        ["SourceCommit"] = commitHash,
                        ["CreatedBy"] = "BaselineManager"
                    }
                };

                var path = GetBaselinePath(baselineName);
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(baselineData, options);
                await File.WriteAllTextAsync(path, json);

                _logger.LogInformation($"✅ Historical baseline created: {baselineName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to create historical baseline: {baselineName}");
                throw;
            }
        }

        /// <summary>
        /// Update an existing baseline with current metrics
        /// </summary>
        public async Task UpdateBaseline(string baselineName)
        {
            var existing = await LoadBaseline(baselineName);
            if (existing == null)
            {
                throw new ArgumentException($"Baseline not found: {baselineName}");
            }

            _logger.LogInformation($"🔄 Updating baseline: {baselineName}");

            try
            {
                var currentMetrics = await CaptureCurrentMetrics(baselineName);
                
                // Merge with existing metrics, updating current values
                foreach (var newMetric in currentMetrics.Metrics)
                {
                    var existingMetric = existing.Metrics.FirstOrDefault(m => 
                        m.BenchmarkName == newMetric.BenchmarkName && 
                        m.MetricName == newMetric.MetricName);

                    if (existingMetric != null)
                    {
                        // Update with new values
                        existingMetric.Mean = newMetric.Mean;
                        existingMetric.Min = newMetric.Min;
                        existingMetric.Max = newMetric.Max;
                        existingMetric.StandardDeviation = newMetric.StandardDeviation;
                        existingMetric.SampleCount = newMetric.SampleCount;
                    }
                    else
                    {
                        // Add new metric
                        existing.Metrics.Add(newMetric);
                    }
                }

                existing.UpdatedAt = DateTime.UtcNow;
                existing.Version = IncrementVersion(existing.Version);

                var path = GetBaselinePath(baselineName);
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(existing, options);
                await File.WriteAllTextAsync(path, json);

                _logger.LogInformation($"✅ Baseline updated: {baselineName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to update baseline: {baselineName}");
                throw;
            }
        }

        private async Task<BaselineData> CaptureCurrentMetrics(string baselineName)
        {
            var baseline = new BaselineData
            {
                Name = baselineName,
                CreatedAt = DateTime.UtcNow,
                Version = "1.0",
                Metadata = new Dictionary<string, object>
                {
                    ["CreatedBy"] = "PerformanceMonitorService",
                    ["Environment"] = Environment.OSVersion.ToString(),
                    [.NETVersion] = Environment.Version.ToString(),
                    ["MachineName"] = Environment.MachineName,
                    ["ProcessorCount"] = Environment.ProcessorCount
                }
            };

            // Note: In a real implementation, this would capture the actual
            // benchmark results that were just run. For now, we'll capture
            // some system-level metrics as placeholders.
            
            try
            {
                // Capture current system metrics
                baseline.Metrics.Add(new BaselineMetric
                {
                    BenchmarkName = "SystemMonitoring",
                    MetricName = "CPUUsage",
                    Mean = 0, // Would be actual measured value
                    Min = 0,
                    Max = 0,
                    StandardDeviation = 0,
                    SampleCount = 1,
                    Unit = "%",
                    Category = "System"
                });

                baseline.Metrics.Add(new BaselineMetric
                {
                    BenchmarkName = "SystemMonitoring",
                    MetricName = "AvailableMemory",
                    Mean = GC.GetTotalMemory(false) / (1024 * 1024), // MB
                    Min = 0,
                    Max = 0,
                    StandardDeviation = 0,
                    SampleCount = 1,
                    Unit = "MB",
                    Category = "System"
                });

                // Add a placeholder comment
                baseline.Metadata["Note"] = "This is a placeholder baseline. Run actual benchmarks to populate real metrics.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error capturing current metrics for baseline");
            }

            return baseline;
        }

        private string GetBaselinePath(string baselineName)
        {
            if (baselineName.Equals("Default", StringComparison.OrdinalIgnoreCase))
            {
                return _defaultBaselinePath;
            }

            var safeName = string.Join("_", baselineName.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(_baselinesDirectory, $"{safeName}.json");
        }

        private static string IncrementVersion(string version)
        {
            var parts = version.Split('.');
            if (parts.Length == 0) return "1.0";

            if (int.TryParse(parts[^1], out var patch))
            {
                parts[^1] = (patch + 1).ToString();
                return string.Join(".", parts);
            }

            return version + ".1";
        }

        // Add missing constant for .NET version
        private const string .NETVersion = ".NETVersion";
    }
}