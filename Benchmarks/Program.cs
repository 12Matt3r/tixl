using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Reports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TiXL.PerformanceSuite.Benchmarks;
using TiXL.PerformanceSuite.Core;
using TiXL.PerformanceSuite.Models;
using TiXL.PerformanceSuite.Services;

namespace TiXL.PerformanceSuite
{
    /// <summary>
    /// Comprehensive Performance Benchmarking Suite for TiXL
    /// 
    /// This suite provides:
    /// 1. Frame time benchmarking for real-time performance
    /// 2. Memory usage profiling and leak detection
    /// 3. Project load time analysis for realistic scenarios
    /// 4. Operator execution performance measurement
    /// 5. Automated regression detection
    /// 6. Performance trend analysis and reporting
    /// 7. Baseline establishment and management
    /// 8. Alert system for performance degradation
    /// </summary>
    public class Program
    {
        private static IHost? _host;
        private static ILogger<Program>? _logger;
        private static PerformanceMonitorService? _monitorService;
        private static BaselineManager? _baselineManager;
        private static ReportGenerator? _reportGenerator;

        static async Task<int> Main(string[] args)
        {
            try
            {
                // Setup dependency injection and configuration
                _host = CreateHostBuilder(args).Build();
                _logger = _host.Services.GetRequiredService<ILogger<Program>>();
                _monitorService = _host.Services.GetRequiredService<PerformanceMonitorService>();
                _baselineManager = _host.Services.GetRequiredService<BaselineManager>();
                _reportGenerator = _host.Services.GetRequiredService<ReportGenerator>();

                _logger.LogInformation("🚀 TiXL Performance Benchmarking Suite Starting");
                _logger.LogInformation($"Environment: {Environment.OSVersion}");
                _logger.LogInformation($".NET Version: {Environment.Version}");
                _logger.LogInformation($"Processor Count: {Environment.ProcessorCount}");
                _logger.LogInformation($"Working Directory: {Environment.CurrentDirectory}");

                // Parse command line arguments
                var config = ParseCommandLineArgs(args);
                if (config.ShowHelp)
                {
                    ShowHelp();
                    return 0;
                }

                // Execute performance benchmarks
                var result = await ExecuteBenchmarks(config);
                
                _logger.LogInformation("✅ TiXL Performance Suite completed successfully");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Fatal error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        private static BenchmarkConfig ParseCommandLineArgs(string[] args)
        {
            var config = new BenchmarkConfig();
            
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "--scenes":
                    case "-s":
                        if (i + 1 < args.Length)
                        {
                            config.ScenePatterns = args[++i].Split(',');
                        }
                        break;
                    case "--categories":
                    case "-c":
                        if (i + 1 < args.Length)
                        {
                            config.Categories = args[++i].Split(',').Select(s => s.Trim()).ToArray();
                        }
                        break;
                    case "--baseline":
                    case "-b":
                        config.Mode = BenchmarkMode.Baseline;
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            config.BaselineName = args[++i];
                        }
                        break;
                    case "--regression":
                    case "-r":
                        config.Mode = BenchmarkMode.Regression;
                        config.CiMode = true;
                        break;
                    case "--report":
                    case "-p":
                        config.Mode = BenchmarkMode.Report;
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            config.ReportPath = args[++i];
                        }
                        break;
                    case "--threshold":
                        if (i + 1 < args.Length)
                        {
                            if (double.TryParse(args[++i], out var threshold))
                            {
                                config.RegressionThreshold = threshold;
                            }
                        }
                        break;
                    case "--ci":
                    case "--ci-mode":
                        config.CiMode = true;
                        break;
                    case "--help":
                    case "-h":
                        config.ShowHelp = true;
                        break;
                    case "--verbose":
                    case "-v":
                        config.Verbose = true;
                        break;
                }
            }

            return config;
        }

        private static void ShowHelp()
        {
            Console.WriteLine(@"
TiXL Performance Benchmarking Suite (TIXL-054)
===============================================

Usage: TiXL.PerformanceSuite [options]

Options:
  --scenes, -s <patterns>       Benchmark specific scene patterns (comma-separated)
  --categories, -c <categories> Benchmark specific categories (comma-separated)
  --baseline, -b [name]         Create performance baseline
  --regression, -r             Run regression detection
  --report, -p [path]          Generate performance report
  --threshold <percent>        Regression threshold (default: 10.0%)
  --ci, --ci-mode              CI/CD mode (fail on regressions)
  --verbose, -v                Verbose output
  --help, -h                   Show this help

Categories:
  FrameTime      - Real-time frame rate performance
  MemoryUsage    - Memory allocation and GC pressure
  ProjectLoad    - Project loading and initialization
  OperatorExec   - Operator execution performance
  GraphicsPerf   - Graphics pipeline performance
  AudioLatency   - Audio processing latency

Examples:
  # Run all benchmarks
  dotnet run

  # Create baseline for specific categories
  dotnet run --baseline --categories FrameTime,MemoryUsage

  # Run regression detection in CI mode
  dotnet run --regression --threshold 15.0 --ci

  # Generate performance report
  dotnet run --report ./reports/monthly-report.html

  # Benchmark specific scenes
  dotnet run --scenes ""*Rendering*,""*NodeEditor*""

For more information: https://github.com/tixl3d/tixl/wiki/Performance-Benchmarks
");
        }

        private static async Task<int> ExecuteBenchmarks(BenchmarkConfig config)
        {
            var stopwatch = Stopwatch.StartNew();
            var exitCode = 0;

            try
            {
                switch (config.Mode)
                {
                    case BenchmarkMode.Baseline:
                        await CreateBaseline(config);
                        break;
                    case BenchmarkMode.Regression:
                        exitCode = await RunRegressionDetection(config);
                        break;
                    case BenchmarkMode.Report:
                        await GenerateReport(config);
                        break;
                    default:
                        await RunFullBenchmark(config);
                        break;
                }

                stopwatch.Stop();
                _logger.LogInformation($"Total execution time: {stopwatch.Elapsed:mm\\:ss\\.fff}");
                
                return exitCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during benchmark execution");
                return 1;
            }
        }

        private static async Task RunFullBenchmark(BenchmarkConfig config)
        {
            _logger.LogInformation("🔄 Running full TiXL performance benchmarks");

            // Setup performance monitoring
            await _monitorService!.StartMonitoring();

            // Create custom BenchmarkDotNet configuration
            var benchmarkConfig = CreateBenchmarkConfig(config);

            // Run benchmarks based on selected categories
            var selectedCategories = GetSelectedBenchmarkTypes(config.Categories);
            
            foreach (var category in selectedCategories)
            {
                _logger.LogInformation($"📊 Running {category} benchmarks...");
                
                try
                {
                    switch (category)
                    {
                        case "FrameTime":
                            BenchmarkRunner.Run<FrameTimeBenchmarks>(benchmarkConfig);
                            break;
                        case "MemoryUsage":
                            BenchmarkRunner.Run<MemoryBenchmarks>(benchmarkConfig);
                            break;
                        case "ProjectLoad":
                            BenchmarkRunner.Run<ProjectLoadBenchmarks>(benchmarkConfig);
                            break;
                        case "OperatorExec":
                            BenchmarkRunner.Run<OperatorExecutionBenchmarks>(benchmarkConfig);
                            break;
                        case "GraphicsPerf":
                            BenchmarkRunner.Run<GraphicsBenchmarks>(benchmarkConfig);
                            break;
                        case "AudioLatency":
                            BenchmarkRunner.Run<AudioBenchmarks>(benchmarkConfig);
                            break;
                        default:
                            _logger.LogWarning($"Unknown benchmark category: {category}");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to run {category} benchmarks");
                }
            }

            // Stop monitoring and generate report
            await _monitorService.StopMonitoring();
            await GenerateReport(config);

            _logger.LogInformation("✅ Full benchmark suite completed");
        }

        private static async Task CreateBaseline(BenchmarkConfig config)
        {
            _logger.LogInformation($"🎯 Creating performance baseline: {config.BaselineName ?? "Default"}");

            // Run benchmarks and capture results
            await RunFullBenchmark(config);

            // Save as baseline
            var baselinePath = $"./Baselines/{config.BaselineName ?? "default"}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
            await _baselineManager!.SaveCurrentResultsAsBaseline(config.BaselineName ?? "Default", baselinePath);

            _logger.LogInformation($"✅ Baseline saved to: {baselinePath}");
        }

        private static async Task<int> RunRegressionDetection(BenchmarkConfig config)
        {
            _logger.LogInformation($"🔍 Running performance regression detection (threshold: {config.RegressionThreshold}%)");

            // Run current benchmarks
            await RunFullBenchmark(config);

            // Load baseline and compare
            var baselinePath = $"./Baselines/{config.BaselineName ?? "default"}.json";
            if (!System.IO.File.Exists(baselinePath))
            {
                _logger.LogWarning($"Baseline not found: {baselinePath}");
                _logger.LogInformation("Creating default baseline from current results...");
                await CreateBaseline(config);
            }

            // Analyze for regressions
            var analysis = await _monitorService!.AnalyzeRegressions(baselinePath, config.RegressionThreshold);
            
            if (analysis.HasRegressions)
            {
                _logger.LogWarning($"⚠️  Found {analysis.RegressionCount} performance regressions");
                
                foreach (var regression in analysis.Regressions)
                {
                    _logger.LogWarning($"  {regression.BenchmarkName}: {regression.RegressionPercent:F1}% slower");
                }

                if (config.CiMode)
                {
                    _logger.LogError("❌ CI mode: Failing due to performance regressions");
                    return 1;
                }
            }
            else
            {
                _logger.LogInformation("✅ No performance regressions detected");
            }

            await GenerateReport(config);
            return 0;
        }

        private static async Task GenerateReport(BenchmarkConfig config)
        {
            _logger.LogInformation("📈 Generating performance report");

            var reportPath = config.ReportPath ?? $"./Reports/performance-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.html";
            
            // Generate comprehensive report
            var reportData = await _monitorService!.GetReportData();
            await _reportGenerator!.GenerateHtmlReport(reportData, reportPath);

            _logger.LogInformation($"📄 Performance report generated: {reportPath}");
        }

        private static IConfig CreateBenchmarkConfig(BenchmarkConfig config)
        {
            return DefaultConfig.Instance
                .AddJob(Job.Default
                    .WithWarmupCount(3)
                    .WithIterationCount(10)
                    .WithLaunchCount(1)
                    .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessToolchain.Instance))
                .AddLogger(ConsoleLogger.Default)
                .AddColumn(StatisticColumn.Mean, StatisticColumn.StdDev, StatisticColumn.Median, 
                          StatisticColumn.Min, StatisticColumn.Max, StatisticColumn.OperationsPerSecond)
                .AddExporter(JsonExporter.Brief)
                .AddExporter(JsonExporter.Full)
                .AddExporter(CsvExporter.Default)
                .WithSummaryStyle(SummaryStyle.Default.WithTimeUnit(Perfolizer.Horology.TimeUnit.Millisecond))
                .WithOptions(ConfigOptions.JoinSummary);
        }

        private static List<string> GetSelectedBenchmarkTypes(string[]? categories)
        {
            if (categories == null || categories.Length == 0)
            {
                return new List<string> { "FrameTime", "MemoryUsage", "ProjectLoad", "OperatorExec", "GraphicsPerf", "AudioLatency" };
            }

            var availableCategories = new[] { "FrameTime", "MemoryUsage", "ProjectLoad", "OperatorExec", "GraphicsPerf", "AudioLatency" };
            return categories.Where(c => availableCategories.Contains(c.Trim())).ToList();
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.SetBasePath(Directory.GetCurrentDirectory())
                          .AddJsonFile("config/benchmarksettings.json", optional: true)
                          .AddJsonFile("config/baselines.json", optional: true)
                          .AddEnvironmentVariables();
                })
                .ConfigureServices((context, services) =>
                {
                    // Register performance monitoring services
                    services.AddSingleton<PerformanceMonitorService>();
                    services.AddSingleton<BaselineManager>();
                    services.AddSingleton<ReportGenerator>();
                    services.AddSingleton<AlertService>();
                    services.AddSingleton<TrendAnalyzer>();
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.SetMinimumLevel(LogLevel.Information);
                });
    }

    // Supporting data structures
    public enum BenchmarkMode
    {
        Full,
        Baseline,
        Regression,
        Report
    }

    public class BenchmarkConfig
    {
        public BenchmarkMode Mode { get; set; } = BenchmarkMode.Full;
        public string[]? Categories { get; set; }
        public string[]? ScenePatterns { get; set; }
        public string? BaselineName { get; set; }
        public string? ReportPath { get; set; }
        public double RegressionThreshold { get; set; } = 10.0;
        public bool CiMode { get; set; }
        public bool Verbose { get; set; }
        public bool ShowHelp { get; set; }
    }
}