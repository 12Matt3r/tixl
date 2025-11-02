using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using TiXL.Core.ErrorHandling;
using TiXL.Core.Performance;
using TiXL.Core.Logging;
using TiXL.Tests.Fixtures;

namespace TiXL.Tests.Production
{
    /// <summary>
    /// Test data generators for production readiness validation
    /// Provides comprehensive test scenarios including stress testing and error injection
    /// </summary>
    public class ProductionTestDataGenerator
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _logger;
        private readonly string _tempDirectory;

        public ProductionTestDataGenerator(IServiceProvider serviceProvider, ILogger logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tempDirectory = Path.Combine(Path.GetTempPath(), $"TiXL_ProductionTest_{Guid.NewGuid():N}");
            
            Directory.CreateDirectory(_tempDirectory);
        }

        /// <summary>
        /// Generates large dataset for memory stress testing
        /// </summary>
        public IEnumerable<byte[]> GenerateLargeDataset(int itemCount, int itemSize)
        {
            _logger.LogInformation("Generating large dataset: {ItemCount} items of {ItemSize} bytes", itemCount, itemSize);
            
            for (int i = 0; i < itemCount; i++)
            {
                var data = new byte[itemSize];
                // Fill with some pattern
                for (int j = 0; j < itemSize; j++)
                {
                    data[j] = (byte)(i + j);
                }
                yield return data;
                
                // Allow GC to run periodically
                if (i % 100 == 0)
                {
                    GC.Collect();
                    Thread.Sleep(1);
                }
            }
        }

        /// <summary>
        /// Generates test data for concurrent operations testing
        /// </summary>
        public List<ConcurrentOperationTestCase> GenerateConcurrentTestCases(int concurrencyLevel)
        {
            _logger.LogInformation("Generating {Count} concurrent test cases", concurrencyLevel);
            
            var testCases = new List<ConcurrentOperationTestCase>();
            
            for (int i = 0; i < concurrencyLevel; i++)
            {
                testCases.Add(new ConcurrentOperationTestCase
                {
                    OperationId = i,
                    OperationType = (OperationType)(i % 4), // Cycle through operation types
                    DataSize = (i % 10 + 1) * 1024, // 1KB to 10KB
                    DelayMs = (i % 100), // 0-99ms delay
                    ShouldFail = i % 15 == 0, // ~7% failure rate
                    IsReadOnly = i % 3 != 0 // ~67% read operations
                });
            }
            
            return testCases;
        }

        /// <summary>
        /// Generates error scenarios for error handling validation
        /// </summary>
        public IEnumerable<ErrorScenario> GenerateErrorScenarios()
        {
            _logger.LogInformation("Generating error scenarios for testing");
            
            var scenarios = new List<ErrorScenario>
            {
                // Transient errors
                new ErrorScenario
                {
                    ScenarioName = "TransientIOError",
                    ErrorType = typeof(IOException),
                    IsTransient = true,
                    RetryCount = 3,
                    ExpectedRecovery = true
                },
                new ErrorScenario
                {
                    ScenarioName = "TransientTimeoutError",
                    ErrorType = typeof(TimeoutException),
                    IsTransient = true,
                    RetryCount = 5,
                    ExpectedRecovery = true
                },
                new ErrorScenario
                {
                    ScenarioName = "TransientTaskCancelError",
                    ErrorType = typeof(TaskCanceledException),
                    IsTransient = true,
                    RetryCount = 2,
                    ExpectedRecovery = false // Cancellation should not retry
                },
                
                // Fatal errors
                new ErrorScenario
                {
                    ScenarioName = "FatalArgumentError",
                    ErrorType = typeof(ArgumentException),
                    IsTransient = false,
                    RetryCount = 0,
                    ExpectedRecovery = false
                },
                new ErrorScenario
                {
                    ScenarioName = "FatalInvalidOperationError",
                    ErrorType = typeof(InvalidOperationException),
                    IsTransient = false,
                    RetryCount = 0,
                    ExpectedRecovery = false
                },
                
                // Resource exhaustion errors
                new ErrorScenario
                {
                    ScenarioName = "ResourceExhaustion",
                    ErrorType = typeof(OutOfMemoryException),
                    IsTransient = true,
                    RetryCount = 1,
                    ExpectedRecovery = true
                },
                
                // Custom TiXL errors
                new ErrorScenario
                {
                    ScenarioName = "TiXLOperationTimeout",
                    ErrorType = typeof(TiXLOperationTimeoutException),
                    IsTransient = true,
                    RetryCount = 3,
                    ExpectedRecovery = true
                },
                new ErrorScenario
                {
                    ScenarioName = "TiXLResourceException",
                    ErrorType = typeof(TiXLResourceException),
                    IsTransient = true,
                    RetryCount = 2,
                    ExpectedRecovery = true
                }
            };
            
            return scenarios;
        }

        /// <summary>
        /// Generates configuration scenarios for testing
        /// </summary>
        public IEnumerable<ConfigurationTestCase> GenerateConfigurationTestCases()
        {
            _logger.LogInformation("Generating configuration test cases");
            
            return new[]
            {
                new ConfigurationTestCase
                {
                    CaseName = "ValidConfiguration",
                    ConfigurationData = CreateValidConfiguration(),
                    ShouldLoad = true,
                    ExpectedErrors = 0
                },
                new ConfigurationTestCase
                {
                    CaseName = "InvalidHistorySize",
                    ConfigurationData = CreateInvalidConfiguration("historySize", "-1"),
                    ShouldLoad = false,
                    ExpectedErrors = 1
                },
                new ConfigurationTestCase
                {
                    CaseName = "MissingRequiredFields",
                    ConfigurationData = CreateIncompleteConfiguration(),
                    ShouldLoad = false,
                    ExpectedErrors = 2
                },
                new ConfigurationTestCase
                {
                    CaseName = "ExtremeValues",
                    ConfigurationData = CreateExtremeValueConfiguration(),
                    ShouldLoad = true,
                    ExpectedErrors = 0
                },
                new ConfigurationTestCase
                {
                    CaseName = "EmptyConfiguration",
                    ConfigurationData = CreateEmptyConfiguration(),
                    ShouldLoad = true, // Should use defaults
                    ExpectedErrors = 0
                }
            };
        }

        /// <summary>
        /// Generates performance test scenarios
        /// </summary>
        public IEnumerable<PerformanceTestScenario> GeneratePerformanceScenarios()
        {
            _logger.LogInformation("Generating performance test scenarios");
            
            return new[]
            {
                new PerformanceTestScenario
                {
                    ScenarioName = "HighFrequencyOperations",
                    OperationCount = 10000,
                    ExpectedOperationsPerSecond = 1000,
                    Duration = TimeSpan.FromMinutes(2),
                    MemoryTargetMB = 100
                },
                new PerformanceTestScenario
                {
                    ScenarioName = "MemoryIntensive",
                    OperationCount = 1000,
                    ExpectedOperationsPerSecond = 50,
                    Duration = TimeSpan.FromMinutes(5),
                    MemoryTargetMB = 500
                },
                new PerformanceTestScenario
                {
                    ScenarioName = "SustainedLoad",
                    OperationCount = 50000,
                    ExpectedOperationsPerSecond = 100,
                    Duration = TimeSpan.FromMinutes(10),
                    MemoryTargetMB = 200
                },
                new PerformanceTestScenario
                {
                    ScenarioName = "BurstLoad",
                    OperationCount = 5000,
                    ExpectedOperationsPerSecond = 5000, // Very high for burst
                    Duration = TimeSpan.FromSeconds(30),
                    MemoryTargetMB = 150
                }
            };
        }

        /// <summary>
        /// Generates resource disposal test scenarios
        /// </summary>
        public IEnumerable<ResourceDisposalTestCase> GenerateResourceDisposalCases()
        {
            _logger.LogInformation("Generating resource disposal test cases");
            
            return new[]
            {
                new ResourceDisposalTestCase
                {
                    CaseName = "NormalDisposal",
                    ResourceCount = 10,
                    DisposalDelay = TimeSpan.FromMilliseconds(100),
                    ExpectedSuccess = true,
                    AllowDisposalFailure = false
                },
                new ResourceDisposalTestCase
                {
                    CaseName = "ManyResources",
                    ResourceCount = 1000,
                    DisposalDelay = TimeSpan.FromMilliseconds(10),
                    ExpectedSuccess = true,
                    AllowDisposalFailure = false
                },
                new ResourceDisposalTestCase
                {
                    CaseName = "DisposalFailures",
                    ResourceCount = 50,
                    DisposalDelay = TimeSpan.FromMilliseconds(50),
                    ExpectedSuccess = true,
                    AllowDisposalFailure = true,
                    FailureRate = 0.2
                },
                new ResourceDisposalTestCase
                {
                    CaseName = "ConcurrentDisposal",
                    ResourceCount = 100,
                    DisposalDelay = TimeSpan.FromMilliseconds(1),
                    ExpectedSuccess = true,
                    AllowDisposalFailure = false,
                    ConcurrentDisposal = true
                }
            };
        }

        /// <summary>
        /// Creates test file structure for I/O testing
        /// </summary>
        public async Task<string> CreateTestFileStructureAsync()
        {
            _logger.LogInformation("Creating test file structure");
            
            var structureDir = Path.Combine(_tempDirectory, "FileStructure");
            Directory.CreateDirectory(structureDir);
            
            // Create various file types and sizes
            var testFiles = new Dictionary<string, int>
            {
                { "small.txt", 1024 },
                { "medium.dat", 1024 * 100 },
                { "large.bin", 1024 * 1024 }, // 1MB
                { "very_large.dat", 10 * 1024 * 1024 }, // 10MB
                { "config.json", 4096 },
                { "data.csv", 50 * 1024 }
            };
            
            foreach (var (fileName, size) in testFiles)
            {
                var filePath = Path.Combine(structureDir, fileName);
                await CreateTestFileAsync(filePath, size);
            }
            
            // Create nested directory structure
            var nestedDir = Path.Combine(structureDir, "Nested", "Deep", "Directory");
            Directory.CreateDirectory(nestedDir);
            
            var nestedFile = Path.Combine(nestedDir, "nested_file.txt");
            await CreateTestFileAsync(nestedFile, 8192);
            
            _logger.LogInformation("Created test file structure at: {Path}", structureDir);
            return structureDir;
        }

        private async Task CreateTestFileAsync(string filePath, int size)
        {
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            var buffer = new byte[8192];
            var written = 0;
            var random = new Random(42); // Fixed seed for reproducibility
            
            while (written < size)
            {
                var toWrite = Math.Min(buffer.Length, size - written);
                random.NextBytes(buffer.AsSpan(0, toWrite));
                await fs.WriteAsync(buffer, 0, toWrite);
                written += toWrite;
            }
        }

        private Dictionary<string, string> CreateValidConfiguration()
        {
            return new Dictionary<string, string>
            {
                ["historySize"] = "300",
                ["targetFrameTime"] = "16.67",
                ["maxFrameVariance"] = "5.0",
                ["logLevel"] = "Information",
                ["enablePerformanceMonitoring"] = "true",
                ["retryMaxRetries"] = "3",
                ["retryInitialDelay"] = "100",
                ["timeoutSeconds"] = "300"
            };
        }

        private Dictionary<string, string> CreateInvalidConfiguration(string invalidKey, string invalidValue)
        {
            var config = CreateValidConfiguration();
            config[invalidKey] = invalidValue;
            return config;
        }

        private Dictionary<string, string> CreateIncompleteConfiguration()
        {
            return new Dictionary<string, string>
            {
                ["historySize"] = "300"
                // Missing other required fields
            };
        }

        private Dictionary<string, string> CreateExtremeValueConfiguration()
        {
            return new Dictionary<string, string>
            {
                ["historySize"] = "9999",
                ["targetFrameTime"] = "1.0",
                ["maxFrameVariance"] = "100.0",
                ["logLevel"] = "Verbose",
                ["enablePerformanceMonitoring"] = "true",
                ["retryMaxRetries"] = "100",
                ["retryInitialDelay"] = "10000",
                ["timeoutSeconds"] = "3600"
            };
        }

        private Dictionary<string, string> CreateEmptyConfiguration()
        {
            return new Dictionary<string, string>();
        }

        /// <summary>
        /// Cleanup temporary test data
        /// </summary>
        public async Task CleanupAsync()
        {
            try
            {
                if (Directory.Exists(_tempDirectory))
                {
                    await Task.Run(() => Directory.Delete(_tempDirectory, true));
                    _logger.LogInformation("Cleaned up test data directory: {Path}", _tempDirectory);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup test data directory: {Path}", _tempDirectory);
            }
        }
    }

    #region Test Data Classes

    public class ConcurrentOperationTestCase
    {
        public int OperationId { get; set; }
        public OperationType OperationType { get; set; }
        public int DataSize { get; set; }
        public int DelayMs { get; set; }
        public bool ShouldFail { get; set; }
        public bool IsReadOnly { get; set; }
    }

    public class ErrorScenario
    {
        public string ScenarioName { get; set; } = string.Empty;
        public Type ErrorType { get; set; } = typeof(Exception);
        public bool IsTransient { get; set; }
        public int RetryCount { get; set; }
        public bool ExpectedRecovery { get; set; }
        public Dictionary<string, object>? Parameters { get; set; }
    }

    public class ConfigurationTestCase
    {
        public string CaseName { get; set; } = string.Empty;
        public Dictionary<string, string> ConfigurationData { get; set; } = new();
        public bool ShouldLoad { get; set; }
        public int ExpectedErrors { get; set; }
        public List<string>? ExpectedWarnings { get; set; }
    }

    public class PerformanceTestScenario
    {
        public string ScenarioName { get; set; } = string.Empty;
        public int OperationCount { get; set; }
        public double ExpectedOperationsPerSecond { get; set; }
        public TimeSpan Duration { get; set; }
        public int MemoryTargetMB { get; set; }
        public bool EnableDetailedMetrics { get; set; } = true;
        public bool EnableResourceMonitoring { get; set; } = true;
    }

    public class ResourceDisposalTestCase
    {
        public string CaseName { get; set; } = string.Empty;
        public int ResourceCount { get; set; }
        public TimeSpan DisposalDelay { get; set; }
        public bool ExpectedSuccess { get; set; }
        public bool AllowDisposalFailure { get; set; }
        public double FailureRate { get; set; } = 0.0;
        public bool ConcurrentDisposal { get; set; }
    }

    public enum OperationType
    {
        Read,
        Write,
        Compute,
        Memory
    }

    #endregion
}