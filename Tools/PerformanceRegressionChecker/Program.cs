using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TiXL.PerformanceRegressionChecker.Core;
using TiXL.PerformanceRegressionChecker.Data;
using TiXL.PerformanceRegressionChecker.Reporting;
using TiXL.PerformanceRegressionChecker.Services;

namespace TiXL.PerformanceRegressionChecker
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            // Configure command line interface
            var rootCommand = new RootCommand("TiXL Performance Regression Detection Tool")
            {
                new Option<string>(new[] { "--check-regression", "-c" }, "Check for performance regressions"),
                new Option<string>(new[] { "--generate-report", "-r" }, "Generate performance report"),
                new Option<string>(new[] { "--baseline", "-b" }, "Set performance baseline"),
                new Option<string>(new[] { "--threshold", "-t" }, "Regression threshold percentage", () => "10.0"),
                new Option<string>(new[] { "--results-path", "-p" }, "Path to benchmark results", () => "BenchmarkDotNet.Artifacts/results"),
                new Option<string>(new[] { "--config", "-cfg" }, "Configuration file path", () => "appsettings.json"),
                new Option<bool>(new[] { "--verbose", "-v" }, "Verbose output"),
                new Option<bool>(new[] { "--ci-mode", "--ci" }, "CI/CD mode (exit with error code on regression)")
            };

            rootCommand.Handler = CommandHandler.Create(async (string checkRegression, string generateReport, string baseline, string threshold, string resultsPath, string config, bool verbose, bool ciMode) =>
            {
                try
                {
                    // Setup dependency injection
                    var services = new ServiceCollection();
                    ConfigureServices(services, config, verbose);
                    var serviceProvider = services.BuildServiceProvider();

                    // Setup logging
                    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
                    logger.LogInformation("Starting TiXL Performance Regression Checker");
                    logger.LogInformation($"Configuration: {config}");
                    logger.LogInformation($"Results Path: {resultsPath}");
                    logger.LogInformation($"Threshold: {threshold}%");

                    if (!string.IsNullOrEmpty(checkRegression))
                    {
                        await CheckRegressions(serviceProvider, checkRegression, double.Parse(threshold), resultsPath, ciMode);
                    }

                    if (!string.IsNullOrEmpty(generateReport))
                    {
                        await GenerateReport(serviceProvider, generateReport, resultsPath);
                    }

                    if (!string.IsNullOrEmpty(baseline))
                    {
                        await SetBaseline(serviceProvider, baseline, resultsPath);
                    }

                    logger.LogInformation("Performance regression check completed successfully");
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    Console.WriteLine(ex.StackTrace);
                    return 1;
                }
            });

            return await rootCommand.InvokeAsync(args);
        }

        private static void ConfigureServices(IServiceCollection services, string configPath, bool verbose)
        {
            // Add configuration
            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(configPath, optional: true)
                .AddEnvironmentVariables();

            var configuration = configurationBuilder.Build();
            services.AddSingleton<IConfiguration>(configuration);

            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Information);
            });

            // Add services
            services.AddTransient<IBenchmarkResultsParser, BenchmarkDotNetResultsParser>();
            services.AddTransient<IBaselineStore, JsonBaselineStore>();
            services.AddTransient<IRegressionDetector, StatisticalRegressionDetector>();
            services.AddTransient<IAnomalyDetector, ZScoreAnomalyDetector>();
            services.AddTransient<IReportGenerator, HtmlReportGenerator>();
            services.AddTransient<INotificationService, NotificationService>();
            services.AddTransient<IPerformanceGate, PerformanceGate>();

            // Add data store if configured
            var connectionString = configuration.GetConnectionString("PerformanceDatabase");
            if (!string.IsNullOrEmpty(connectionString))
            {
                services.AddSingleton<IDataStore>(provider => new DatabaseDataStore(connectionString, provider.GetRequiredService<ILogger<DatabaseDataStore>>()));
            }
            else
            {
                services.AddSingleton<IDataStore, FileSystemDataStore>();
            }
        }

        private static async Task CheckRegressions(IServiceProvider serviceProvider, string commitHash, double threshold, string resultsPath, bool ciMode)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            var parser = serviceProvider.GetRequiredService<IBenchmarkResultsParser>();
            var detector = serviceProvider.GetRequiredService<IRegressionDetector>();
            var gate = serviceProvider.GetRequiredService<IPerformanceGate>();
            var store = serviceProvider.GetRequiredService<IDataStore>();

            logger.LogInformation($"Checking for regressions with threshold {threshold}%");

            // Parse current benchmark results
            var currentResults = parser.ParseResults(resultsPath);
            logger.LogInformation($"Parsed {currentResults.Benchmarks.Count} benchmark results");

            // Check for regressions
            var regressionReport = await detector.DetectRegressions(currentResults, threshold);
            
            logger.LogInformation($"Found {regressionReport.Regressions.Count} regressions out of {regressionReport.TotalBenchmarks} benchmarks");

            // Generate report
            var reportPath = Path.Combine(resultsPath, $"regression-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.html");
            var reportGenerator = serviceProvider.GetRequiredService<IReportGenerator>();
            await reportGenerator.GenerateReport(regressionReport, reportPath);

            logger.LogInformation($"Regression report generated: {reportPath}");

            // Check performance gates
            var gateResult = gate.EvaluateGates(regressionReport, threshold);
            
            if (gateResult.HasFailures)
            {
                logger.LogError("Performance gates failed!");
                foreach (var failure in gateResult.Failures)
                {
                    logger.LogError($"  {failure.BenchmarkName}: {failure.Message}");
                }

                if (ciMode)
                {
                    Environment.Exit(1);
                }
            }
            else
            {
                logger.LogInformation("All performance gates passed");
            }
        }

        private static async Task GenerateReport(IServiceProvider serviceProvider, string outputFormat, string resultsPath)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            var parser = serviceProvider.GetRequiredService<IBenchmarkResultsParser>();
            var reportGenerator = serviceProvider.GetRequiredService<IReportGenerator>();

            logger.LogInformation($"Generating {outputFormat} report");

            // Parse results
            var currentResults = parser.ParseResults(resultsPath);
            
            // Generate daily performance report
            var report = new DailyPerformanceReport
            {
                Date = DateTime.UtcNow.Date,
                Summary = new PerformanceSummary
                {
                    TotalBenchmarks = currentResults.Benchmarks.Count,
                    PassedBenchmarks = currentResults.Benchmarks.Count, // Assume all passed for daily report
                    AverageFrameTime = currentResults.Benchmarks.Where(b => b.Name.Contains("Frame")).Average(b => b.MeanExecutionTime),
                    MemoryGrowth = currentResults.Benchmarks.Where(b => b.Name.Contains("Memory")).Average(b => b.MemoryAllocated)
                }
            };

            // Get trend data
            var store = serviceProvider.GetRequiredService<IDataStore>();
            var trendAnalyzer = new TrendAnalysisService(store);
            report.Trends = await trendAnalyzer.GetLast30DaysTrends();

            var outputPath = Path.Combine(resultsPath, $"daily-report-{DateTime.UtcNow:yyyyMMdd}.html");
            await reportGenerator.GenerateDailyReport(report, outputPath);

            logger.LogInformation($"Daily report generated: {outputPath}");
        }

        private static async Task SetBaseline(IServiceProvider serviceProvider, string benchmarkName, string resultsPath)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            var parser = serviceProvider.GetRequiredService<IBenchmarkResultsParser>();
            var store = serviceProvider.GetRequiredService<IDataStore>();

            logger.LogInformation($"Setting baseline for {benchmarkName}");

            // Parse current results
            var currentResults = parser.ParseResults(resultsPath);
            
            // Find specific benchmark or use the best performing one
            var baselineResult = string.IsNullOrEmpty(benchmarkName) 
                ? currentResults.Benchmarks.OrderBy(b => b.MeanExecutionTime).First()
                : currentResults.Benchmarks.FirstOrDefault(b => b.Name.Contains(benchmarkName));

            if (baselineResult == null)
            {
                throw new ArgumentException($"Benchmark '{benchmarkName}' not found in results");
            }

            // Save as baseline
            await store.SaveBaseline(baselineResult);
            
            logger.LogInformation($"Baseline set for {baselineResult.Name}: {baselineResult.MeanExecutionTime:F2}ms");
        }
    }

    // Supporting data structures
    public class DailyPerformanceReport
    {
        public DateTime Date { get; set; }
        public PerformanceSummary Summary { get; set; } = new();
        public List<PerformanceTrend> Trends { get; set; } = new();
        public List<PerformanceAlert> Alerts { get; set; } = new();
    }

    public class PerformanceSummary
    {
        public int TotalBenchmarks { get; set; }
        public int PassedBenchmarks { get; set; }
        public double AverageFrameTime { get; set; }
        public long MemoryGrowth { get; set; }
    }

    public class PerformanceTrend
    {
        public string BenchmarkName { get; set; } = "";
        public List<DataPoint> DataPoints { get; set; } = new();
        public TrendDirection Direction { get; set; }
        public double Slope { get; set; }
        public double R2Score { get; set; }
    }

    public class PerformanceAlert
    {
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public AlertLevel Level { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public enum TrendDirection
    {
        Improving,
        Stable,
        Worsening
    }

    public enum AlertLevel
    {
        Low,
        Medium,
        High,
        Critical
    }

    public class DataPoint
    {
        public DateTime Date { get; set; }
        public double Value { get; set; }
        public int X => (int)Date.Subtract(DateTime.MinValue).TotalDays;
        public double Y => Value;
    }
}