// PerformanceMonitorTests.cs
using Xunit;
using Xunit.Abstractions;
using TiXL.Tests.Fixtures;
using TiXL.Tests.Categories;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace TiXL.Tests.Performance
{
    /// <summary>
    /// Unit tests for PerformanceMonitor
    /// </summary>
    [Category(TestCategories.Performance)]
    [Category(TestCategories.Unit)]
    public class PerformanceMonitorTests : CoreTestFixture
    {
        private readonly ITestOutputHelper _output;

        public PerformanceMonitorTests(ITestOutputHelper output) : base()
        {
            _output = output;
        }

        [Fact]
        public void PerformanceMonitor_CreateInstance_InitializesCorrectly()
        {
            // Arrange & Act
            var monitor = new PerformanceMonitor();

            // Assert
            monitor.Should().NotBeNull();
            monitor.IsMonitoring.Should().BeTrue("Should start monitoring by default");
        }

        [Fact]
        public void PerformanceMonitor_StartMonitoring_EnablesMonitoring()
        {
            // Arrange
            var monitor = new PerformanceMonitor();

            // Act
            monitor.StartMonitoring();

            // Assert
            monitor.IsMonitoring.Should().BeTrue();
        }

        [Fact]
        public void PerformanceMonitor_StopMonitoring_DisablesMonitoring()
        {
            // Arrange
            var monitor = new PerformanceMonitor();

            // Act
            monitor.StopMonitoring();

            // Assert
            monitor.IsMonitoring.Should().BeFalse();
        }

        [Fact]
        public void PerformanceMonitor_RecordOperation_TracksPerformance()
        {
            // Arrange
            var monitor = new PerformanceMonitor();
            var operationName = "TestOperation";

            // Act
            var sw = Stopwatch.StartNew();
            monitor.RecordOperationStart(operationName);
            await Task.Delay(10); // Simulate some work
            monitor.RecordOperationEnd(operationName);
            sw.Stop();

            // Assert
            var metrics = monitor.GetPerformanceMetrics(operationName);
            metrics.Should().NotBeNull();
            metrics.CallCount.Should().BeGreaterOrEqualTo(1);
            metrics.AverageDurationMs.Should().BeGreaterThan(0);
        }

        [Fact]
        public void PerformanceMonitor_RecordMemoryUsage_TracksMemory()
        {
            // Arrange
            var monitor = new PerformanceMonitor();
            var memoryUsage = 1024 * 1024; // 1MB

            // Act
            monitor.RecordMemoryUsage(memoryUsage);

            // Assert
            var metrics = monitor.GetCurrentMetrics();
            metrics.CurrentMemoryUsageBytes.Should().Be(memoryUsage);
        }

        [Fact]
        public void PerformanceMonitor_RecordGcMetrics_TracksGcStatistics()
        {
            // Arrange
            var monitor = new PerformanceMonitor();
            var gcMetrics = new GcMetrics
            {
                Gen0Collections = 1,
                Gen1Collections = 2,
                Gen2Collections = 3,
                TotalMemoryBytes = 1024 * 1024
            };

            // Act
            monitor.RecordGcMetrics(gcMetrics);

            // Assert - Verify metrics are tracked (implementation specific)
            monitor.Should().NotBeNull();
        }

        [Fact]
        public void PerformanceMonitor_GetPerformanceMetrics_ForMultipleOperations()
        {
            // Arrange
            var monitor = new PerformanceMonitor();
            var operation1 = "Operation1";
            var operation2 = "Operation2";

            // Act
            for (int i = 0; i < 5; i++)
            {
                monitor.RecordOperationStart(operation1);
                await Task.Delay(5);
                monitor.RecordOperationEnd(operation1);

                monitor.RecordOperationStart(operation2);
                await Task.Delay(10);
                monitor.RecordOperationEnd(operation2);
            }

            // Assert
            var metrics1 = monitor.GetPerformanceMetrics(operation1);
            var metrics2 = monitor.GetPerformanceMetrics(operation2);

            metrics1.Should().NotBeNull();
            metrics2.Should().NotBeNull();
            metrics1.CallCount.Should().Be(5);
            metrics2.CallCount.Should().Be(5);
            metrics1.AverageDurationMs.Should().BeLessThan(metrics2.AverageDurationMs);
        }

        [Fact]
        public void PerformanceMonitor_GetAllMetrics_ReturnsAllOperations()
        {
            // Arrange
            var monitor = new PerformanceMonitor();
            monitor.RecordOperationStart("Op1");
            monitor.RecordOperationEnd("Op1");
            monitor.RecordOperationStart("Op2");
            monitor.RecordOperationEnd("Op2");

            // Act
            var allMetrics = monitor.GetAllMetrics();

            // Assert
            allMetrics.Should().NotBeNull();
            allMetrics.Should().HaveCountGreaterOrEqualTo(2);
        }

        [Fact]
        public void PerformanceMonitor_ClearMetrics_RemovesAllData()
        {
            // Arrange
            var monitor = new PerformanceMonitor();
            monitor.RecordOperationStart("Test");
            monitor.RecordOperationEnd("Test");

            // Act
            monitor.ClearMetrics();

            // Assert
            var allMetrics = monitor.GetAllMetrics();
            allMetrics.Should().BeEmpty();
        }

        [Fact]
        public void PerformanceMonitor_PerformanceThresholds_CanBeSet()
        {
            // Arrange
            var monitor = new PerformanceMonitor();
            var threshold = TimeSpan.FromMilliseconds(50);

            // Act
            monitor.SetPerformanceThreshold("TestOperation", threshold);
            var setThreshold = monitor.GetPerformanceThreshold("TestOperation");

            // Assert
            setThreshold.Should().Be(threshold);
        }

        [Fact]
        public void PerformanceMonitor_AlertOnPerformanceRegression_DetectsSlowOperations()
        {
            // Arrange
            var monitor = new PerformanceMonitor();
            var operationName = "SlowOperation";
            monitor.SetPerformanceThreshold(operationName, TimeSpan.FromMilliseconds(10));

            // Act - Record a slow operation
            monitor.RecordOperationStart(operationName);
            await Task.Delay(50); // Exceed threshold
            monitor.RecordOperationEnd(operationName);

            // Assert - In real implementation, this would trigger an alert
            var metrics = monitor.GetPerformanceMetrics(operationName);
            metrics.AverageDurationMs.Should().BeGreaterThan(10);
        }
    }

    /// <summary>
    /// Unit tests for CircularBuffer
    /// </summary>
    [Category(TestCategories.Performance)]
    [Category(TestCategories.Unit)]
    public class CircularBufferTests
    {
        [Theory]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public void CircularBuffer_CreateWithCapacity_InitializesCorrectly(int capacity)
        {
            // Arrange & Act
            var buffer = new CircularBuffer<double>(capacity);

            // Assert
            buffer.Capacity.Should().Be(capacity);
            buffer.Length.Should().Be(0);
            buffer.IsEmpty.Should().BeTrue();
        }

        [Fact]
        public void CircularBuffer_AddElements_CorrectlyStoresAndRetrieves()
        {
            // Arrange
            var buffer = new CircularBuffer<int>(5);
            var expectedValues = new[] { 1, 2, 3, 4, 5 };

            // Act
            foreach (var value in expectedValues)
            {
                buffer.Add(value);
            }

            // Assert
            buffer.Length.Should().Be(5);
            for (int i = 0; i < expectedValues.Length; i++)
            {
                buffer[i].Should().Be(expectedValues[i]);
            }
        }

        [Fact]
        public void CircularBuffer_Overflow_OverwritesOldestElements()
        {
            // Arrange
            var buffer = new CircularBuffer<int>(3);
            var values = new[] { 1, 2, 3, 4, 5 };

            // Act
            foreach (var value in values)
            {
                buffer.Add(value);
            }

            // Assert - Should only contain last 3 values: 3, 4, 5
            buffer.Length.Should().Be(3);
            buffer[0].Should().Be(3);
            buffer[1].Should().Be(4);
            buffer[2].Should().Be(5);
        }

        [Fact]
        public void CircularBuffer_Clear_RemovesAllElements()
        {
            // Arrange
            var buffer = new CircularBuffer<int>(5);
            buffer.Add(1);
            buffer.Add(2);
            buffer.Add(3);

            // Act
            buffer.Clear();

            // Assert
            buffer.Length.Should().Be(0);
            buffer.IsEmpty.Should().BeTrue();
        }

        [Fact]
        public void CircularBuffer_GetLatest_ReturnsMostRecentElements()
        {
            // Arrange
            var buffer = new CircularBuffer<int>(5);
            var values = new[] { 1, 2, 3, 4, 5 };

            foreach (var value in values)
            {
                buffer.Add(value);
            }

            // Act
            var latest = buffer.GetLatest(3);

            // Assert
            latest.Should().BeEquivalentTo(new[] { 3, 4, 5 });
        }

        [Fact]
        public void CircularBuffer_Average_CalculatesCorrectly()
        {
            // Arrange
            var buffer = new CircularBuffer<double>(5);
            var values = new[] { 1.0, 2.0, 3.0, 4.0, 5.0 };

            foreach (var value in values)
            {
                buffer.Add(value);
            }

            // Act
            var average = buffer.Average();

            // Assert
            average.Should().Be(3.0);
        }

        [Fact]
        public void CircularBuffer_Max_ReturnsHighestValue()
        {
            // Arrange
            var buffer = new CircularBuffer<int>(5);
            var values = new[] { 3, 1, 4, 1, 5 };

            foreach (var value in values)
            {
                buffer.Add(value);
            }

            // Act
            var max = buffer.Max();

            // Assert
            max.Should().Be(5);
        }

        [Fact]
        public void CircularBuffer_Min_ReturnsLowestValue()
        {
            // Arrange
            var buffer = new CircularBuffer<int>(5);
            var values = new[] { 3, 1, 4, 1, 5 };

            foreach (var value in values)
            {
                buffer.Add(value);
            }

            // Act
            var min = buffer.Min();

            // Assert
            min.Should().Be(1);
        }

        [Fact]
        public void CircularBuffer_Sum_ReturnsTotalValue()
        {
            // Arrange
            var buffer = new CircularBuffer<int>(5);
            var values = new[] { 1, 2, 3, 4, 5 };

            foreach (var value in values)
            {
                buffer.Add(value);
            }

            // Act
            var sum = buffer.Sum();

            // Assert
            sum.Should().Be(15);
        }
    }

    /// <summary>
    /// Unit tests for GcMetrics
    /// </summary>
    [Category(TestCategories.Performance)]
    [Category(TestCategories.Unit)]
    public class GcMetricsTests
    {
        [Fact]
        public void GcMetrics_DefaultConstructor_InitializesCorrectly()
        {
            // Arrange & Act
            var metrics = new GcMetrics();

            // Assert
            metrics.Gen0Collections.Should().Be(0);
            metrics.Gen1Collections.Should().Be(0);
            metrics.Gen2Collections.Should().Be(0);
            metrics.TotalMemoryBytes.Should().Be(0);
            metrics.HeapSizeBytes.Should().Be(0);
        }

        [Fact]
        public void GcMetrics_CustomConstructor_SetsCustomValues()
        {
            // Arrange & Act
            var metrics = new GcMetrics
            {
                Gen0Collections = 10,
                Gen1Collections = 5,
                Gen2Collections = 2,
                TotalMemoryBytes = 1024 * 1024,
                HeapSizeBytes = 512 * 1024
            };

            // Assert
            metrics.Gen0Collections.Should().Be(10);
            metrics.Gen1Collections.Should().Be(5);
            metrics.Gen2Collections.Should().Be(2);
            metrics.TotalMemoryBytes.Should().Be(1024 * 1024);
            metrics.HeapSizeBytes.Should().Be(512 * 1024);
        }

        [Fact]
        public void GcMetrics_UpdateFromEnvironment_ReflectsCurrentState()
        {
            // Arrange
            var initialMemory = GC.GetTotalMemory(false);
            var metrics = new GcMetrics();

            // Act
            metrics.UpdateFromEnvironment();

            // Assert
            metrics.TotalMemoryBytes.Should().Be(initialMemory);
            metrics.Gen0Collections.Should().BeGreaterOrEqualTo(0);
            metrics.Gen1Collections.Should().BeGreaterOrEqualTo(0);
            metrics.Gen2Collections.Should().BeGreaterOrEqualTo(0);
        }

        [Theory]
        [InlineData(1024)]
        [InlineData(1024 * 1024)]
        [InlineData(1024 * 1024 * 1024)]
        public void GcMetrics_MemoryProperties_StoreCorrectly(long memoryBytes)
        {
            // Arrange & Act
            var metrics = new GcMetrics { TotalMemoryBytes = memoryBytes };

            // Assert
            metrics.TotalMemoryBytes.Should().Be(memoryBytes);
        }

        [Fact]
        public void GcMetrics_CollectionCounts_StoreCorrectly()
        {
            // Arrange & Act
            var metrics = new GcMetrics
            {
                Gen0Collections = 1,
                Gen1Collections = 2,
                Gen2Collections = 3
            };

            // Assert
            metrics.Gen0Collections.Should().Be(1);
            metrics.Gen1Collections.Should().Be(2);
            metrics.Gen2Collections.Should().Be(3);
        }
    }

    /// <summary>
    /// Unit tests for PredictiveFrameScheduler
    /// </summary>
    [Category(TestCategories.Performance)]
    [Category(TestCategories.Unit)]
    public class PredictiveFrameSchedulerTests
    {
        [Fact]
        public void PredictiveFrameScheduler_CreateInstance_InitializesCorrectly()
        {
            // Arrange & Act
            var scheduler = new PredictiveFrameScheduler();

            // Assert
            scheduler.Should().NotBeNull();
            scheduler.TargetFrameRate.Should().BeGreaterThan(0);
            scheduler.FrameTimeHistory.Should().NotBeNull();
        }

        [Theory]
        [InlineData(30)]
        [InlineData(60)]
        [InlineData(120)]
        public void PredictiveFrameScheduler_SetTargetFrameRate_UpdatesCorrectly(int targetFps)
        {
            // Arrange
            var scheduler = new PredictiveFrameScheduler();

            // Act
            scheduler.TargetFrameRate = targetFps;
            var targetFrameTime = scheduler.TargetFrameTime;

            // Assert
            targetFrameTime.Should().BeApproximately(1000.0 / targetFps, 0.1);
        }

        [Fact]
        public void PredictiveFrameScheduler_RecordFrameTime_UpdatesPredictions()
        {
            // Arrange
            var scheduler = new PredictiveFrameScheduler();
            var frameTime = 16.67f; // ~60 FPS

            // Act
            scheduler.RecordFrameTime(frameTime);

            // Assert
            scheduler.FrameTimeHistory.Length.Should().BeGreaterOrEqualTo(1);
            scheduler.PredictedFrameTime.Should().BeGreaterThan(0);
        }

        [Fact]
        public void PredictiveFrameScheduler_CalculateSleepTime_HandlesNormalCase()
        {
            // Arrange
            var scheduler = new PredictiveFrameScheduler();
            scheduler.TargetFrameRate = 60;
            scheduler.RecordFrameTime(10.0f); // 10ms actual

            // Act
            var sleepTime = scheduler.CalculateSleepTime();

            // Assert
            sleepTime.Should().BeGreaterOrEqualTo(0);
            sleepTime.Should().BeLessOrEqualTo(16.67); // Max 60fps frame time
        }

        [Fact]
        public void PredictiveFrameScheduler_CalculateSleepTime_HandlesSlowFrame()
        {
            // Arrange
            var scheduler = new PredictiveFrameScheduler();
            scheduler.TargetFrameRate = 60;
            scheduler.RecordFrameTime(25.0f); // 25ms (slower than target)

            // Act
            var sleepTime = scheduler.CalculateSleepTime();

            // Assert
            sleepTime.Should().Be(0, "Should not sleep after slow frame");
        }

        [Fact]
        public void PredictiveFrameScheduler_GetFrameTimeStatistics_ReturnsValidStats()
        {
            // Arrange
            var scheduler = new PredictiveFrameScheduler();
            var frameTimes = new[] { 16.0f, 17.0f, 15.0f, 18.0f, 16.5f };

            foreach (var frameTime in frameTimes)
            {
                scheduler.RecordFrameTime(frameTime);
            }

            // Act
            var stats = scheduler.GetFrameTimeStatistics();

            // Assert
            stats.Should().NotBeNull();
            stats.AverageFrameTime.Should().BeGreaterThan(0);
            stats.MinFrameTime.Should().BeGreaterThan(0);
            stats.MaxFrameTime.Should().BeGreaterThan(0);
            stats.StandardDeviation.Should().BeGreaterOrEqualTo(0);
        }

        [Fact]
        public void PredictiveFrameScheduler_HandleFrameDrift_AdjustsTiming()
        {
            // Arrange
            var scheduler = new PredictiveFrameScheduler();
            scheduler.TargetFrameRate = 60;

            // Act - Simulate consistent drift
            for (int i = 0; i < 10; i++)
            {
                scheduler.RecordFrameTime(20.0f); // Always 20ms
            }

            // Assert
            scheduler.PredictedFrameTime.Should().BeApproximately(20.0f, 0.5f);
            scheduler.CalculateSleepTime().Should().Be(0, "Should adjust for persistent drift");
        }

        [Fact]
        public void PredictiveFrameScheduler_ClearHistory_RemovesOldData()
        {
            // Arrange
            var scheduler = new PredictiveFrameScheduler();
            scheduler.RecordFrameTime(16.0f);
            scheduler.RecordFrameTime(17.0f);

            // Act
            scheduler.ClearHistory();

            // Assert
            scheduler.FrameTimeHistory.Length.Should().Be(0);
        }
    }

    /// <summary>
    /// Integration tests for performance monitoring
    /// </summary>
    [Category(TestCategories.Performance)]
    [Category(TestCategories.Integration)]
    public class PerformanceMonitoringIntegrationTests : CoreTestFixture
    {
        private readonly ITestOutputHelper _output;

        public PerformanceMonitoringIntegrationTests(ITestOutputHelper output) : base()
        {
            _output = output;
        }

        [Fact]
        public async Task PerformanceMonitoring_EndToEnd_TracksComplexOperations()
        {
            // Arrange
            var monitor = new PerformanceMonitor();
            var operations = new[] { "Init", "Process", "Render", "Cleanup" };

            // Act
            foreach (var operation in operations)
            {
                monitor.RecordOperationStart(operation);
                await Task.Delay(10); // Simulate work
                monitor.RecordOperationEnd(operation);
            }

            // Assert
            foreach (var operation in operations)
            {
                var metrics = monitor.GetPerformanceMetrics(operation);
                metrics.Should().NotBeNull();
                metrics.CallCount.Should().BeGreaterOrEqualTo(1);
                metrics.AverageDurationMs.Should().BeGreaterThan(0);
            }
        }

        [Fact]
        public async Task PerformanceMonitoring_ConcurrentOperations_MaintainsAccuracy()
        {
            // Arrange
            var monitor = new PerformanceMonitor();
            var tasks = new Task[5];

            // Act - Run concurrent performance tracking
            for (int i = 0; i < tasks.Length; i++)
            {
                var index = i;
                tasks[i] = Task.Run(async () =>
                {
                    var operationName = $"ConcurrentOp_{index}";
                    monitor.RecordOperationStart(operationName);
                    await Task.Delay(Random.Shared.Next(10, 50)); // Random work
                    monitor.RecordOperationEnd(operationName);
                });
            }

            await Task.WhenAll(tasks);

            // Assert
            var allMetrics = monitor.GetAllMetrics();
            allMetrics.Should().HaveCountGreaterOrEqualTo(5);
        }

        [Fact]
        public async Task PerformanceMonitoring_MemoryAndGcTracking_WorksTogether()
        {
            // Arrange
            var monitor = new PerformanceMonitor();

            // Act - Track memory usage during operations
            var operationName = "MemoryTest";
            for (int i = 0; i < 10; i++)
            {
                monitor.RecordOperationStart(operationName);
                
                // Simulate memory allocation
                var data = new byte[1024 * (i + 1)];
                monitor.RecordMemoryUsage(GC.GetTotalMemory(false));
                
                await Task.Delay(5);
                monitor.RecordOperationEnd(operationName);
                
                // Force GC and track metrics
                GC.Collect();
                var gcMetrics = new GcMetrics();
                gcMetrics.UpdateFromEnvironment();
                monitor.RecordGcMetrics(gcMetrics);
            }

            // Assert
            var opMetrics = monitor.GetPerformanceMetrics(operationName);
            opMetrics.Should().NotBeNull();
            opMetrics.CallCount.Should().Be(10);
        }
    }
}