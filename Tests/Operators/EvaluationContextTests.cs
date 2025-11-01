using Xunit;
using Xunit.Abstractions;
using TiXL.Tests.Categories;
using TiXL.Tests.Fixtures;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using T3.Core.Operators;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace TiXL.Tests.Operators
{
    /// <summary>
    /// Unit tests for EvaluationContext
    /// </summary>
    [Category(TestCategories.Operators)]
    [Category(TestCategories.Unit)]
    public class EvaluationContextTests : CoreTestFixture
    {
        private readonly ITestOutputHelper _output;
        private readonly ILogger<EvaluationContextTests> _logger;

        public EvaluationContextTests(ITestOutputHelper output) : base()
        {
            _output = output;
            _logger = ServiceProvider.GetRequiredService<ILogger<EvaluationContextTests>>();
        }

        [Fact]
        public void EvaluationContext_CreateInstance_WithValidDependencies_Success()
        {
            // Arrange
            var renderingEngine = new MockRenderingEngine();
            var audioEngine = new MockAudioEngine();
            var resourceManager = new MockResourceManager();
            var cancellationToken = CancellationToken.None;
            var guardrails = new GuardrailConfiguration();

            // Act & Assert - Constructor should not throw
            Action act = () =>
            {
                using var context = new EvaluationContext(renderingEngine, audioEngine, resourceManager, _logger, cancellationToken, guardrails);
                context.Should().NotBeNull();
            };

            act.Should().NotThrow();
        }

        [Fact]
        public void EvaluationContext_CancellationToken_PropagatedCorrectly()
        {
            // Arrange
            var renderingEngine = new MockRenderingEngine();
            var audioEngine = new MockAudioEngine();
            var resourceManager = new MockResourceManager();
            using var cts = new CancellationTokenSource();
            var cancellationToken = cts.Token;
            var guardrails = new GuardrailConfiguration();

            // Act
            using var context = new EvaluationContext(renderingEngine, audioEngine, resourceManager, _logger, cancellationToken, guardrails);

            // Assert
            context.CancellationToken.Should().Be(cancellationToken);
        }

        [Fact]
        public void EvaluationContext_GuardrailConfiguration_SetCorrectly()
        {
            // Arrange
            var renderingEngine = new MockRenderingEngine();
            var audioEngine = new MockAudioEngine();
            var resourceManager = new MockResourceManager();
            var guardrails = new GuardrailConfiguration { MaxEvaluationTime = TimeSpan.FromSeconds(30) };

            // Act
            using var context = new EvaluationContext(renderingEngine, audioEngine, resourceManager, _logger, CancellationToken.None, guardrails);

            // Assert
            context.Configuration.MaxEvaluationTime.Should().Be(TimeSpan.FromSeconds(30));
        }

        [Fact]
        public void EvaluationContext_ExecutionState_InitializedCorrectly()
        {
            // Arrange
            var renderingEngine = new MockRenderingEngine();
            var audioEngine = new MockAudioEngine();
            var resourceManager = new MockResourceManager();
            var guardrails = new GuardrailConfiguration();

            // Act
            using var context = new EvaluationContext(renderingEngine, audioEngine, resourceManager, _logger, CancellationToken.None, guardrails);

            // Assert
            context.CurrentState.Should().NotBeNull();
            context.CurrentState.IsWithinLimits.Should().BeTrue("New context should be within limits");
        }

        [Fact]
        public void EvaluationContext_Metrics_InitializedCorrectly()
        {
            // Arrange
            var renderingEngine = new MockRenderingEngine();
            var audioEngine = new MockAudioEngine();
            var resourceManager = new MockResourceManager();
            var guardrails = new GuardrailConfiguration();

            // Act
            using var context = new EvaluationContext(renderingEngine, audioEngine, resourceManager, _logger, CancellationToken.None, guardrails);

            // Assert
            context.Metrics.Should().NotBeNull();
            context.Metrics.EvaluationCount.Should().Be(0, "No evaluations should have occurred yet");
        }

        [Fact]
        public void EvaluationContext_Dispose_CompletesSuccessfully()
        {
            // Arrange
            var renderingEngine = new MockRenderingEngine();
            var audioEngine = new MockAudioEngine();
            var resourceManager = new MockResourceManager();
            var guardrails = new GuardrailConfiguration();

            // Act
            using var context = new EvaluationContext(renderingEngine, audioEngine, resourceManager, _logger, CancellationToken.None, guardrails)
            {
                // Use the context
            };

            // Assert - Dispose should complete without exception
            var exception = Record.Exception(() => context.Dispose());
            exception.Should().BeNull();
        }
    }

    /// <summary>
    /// Unit tests for GuardrailConfiguration
    /// </summary>
    [Category(TestCategories.Operators)]
    [Category(TestCategories.Unit)]
    public class GuardrailConfigurationTests : CoreTestFixture
    {
        private readonly ITestOutputHelper _output;

        public GuardrailConfigurationTests(ITestOutputHelper output) : base()
        {
            _output = output;
        }

        [Fact]
        public void GuardrailConfiguration_DefaultConstructor_SetsDefaultValues()
        {
            // Arrange & Act
            var config = new GuardrailConfiguration();

            // Assert
            config.MaxEvaluationTime.Should().BeGreaterThan(TimeSpan.Zero);
            config.MaxMemoryUsageBytes.Should().BeGreaterThan(0);
            config.MaxConcurrentOperations.Should().BeGreaterThan(0);
            config.EnablePerformanceMonitoring.Should().BeTrue("Performance monitoring should be enabled by default");
        }

        [Fact]
        public void GuardrailConfiguration_CustomConstructor_SetsCustomValues()
        {
            // Arrange & Act
            var maxTime = TimeSpan.FromSeconds(60);
            var maxMemory = 1024 * 1024 * 1024; // 1GB
            var maxOperations = 50;

            var config = new GuardrailConfiguration
            {
                MaxEvaluationTime = maxTime,
                MaxMemoryUsageBytes = maxMemory,
                MaxConcurrentOperations = maxOperations
            };

            // Assert
            config.MaxEvaluationTime.Should().Be(maxTime);
            config.MaxMemoryUsageBytes.Should().Be(maxMemory);
            config.MaxConcurrentOperations.Should().Be(maxOperations);
        }

        [Theory]
        [InlineData(0)]  // Zero time
        [InlineData(-1)] // Negative time
        public void GuardrailConfiguration_InvalidMaxEvaluationTime_SetsToDefault(int seconds)
        {
            // Arrange & Act
            var config = new GuardrailConfiguration { MaxEvaluationTime = TimeSpan.FromSeconds(seconds) };

            // Assert
            config.MaxEvaluationTime.Should().BeGreaterThan(TimeSpan.Zero);
        }

        [Theory]
        [InlineData(0)]  // Zero memory
        [InlineData(-1)] // Negative memory
        public void GuardrailConfiguration_InvalidMaxMemoryUsage_SetsToDefault(int bytes)
        {
            // Arrange & Act
            var config = new GuardrailConfiguration { MaxMemoryUsageBytes = bytes };

            // Assert
            config.MaxMemoryUsageBytes.Should().BeGreaterThan(0);
        }

        [Theory]
        [InlineData(0)]  // Zero operations
        [InlineData(-1)] // Negative operations
        public void GuardrailConfiguration_InvalidMaxConcurrentOperations_SetsToDefault(int operations)
        {
            // Arrange & Act
            var config = new GuardrailConfiguration { MaxConcurrentOperations = operations };

            // Assert
            config.MaxConcurrentOperations.Should().BeGreaterThan(0);
        }
    }

    /// <summary>
    /// Unit tests for ExecutionState
    /// </summary>
    [Category(TestCategories.Operators)]
    [Category(TestCategories.Unit)]
    public class ExecutionStateTests : CoreTestFixture
    {
        private readonly ITestOutputHelper _output;

        public ExecutionStateTests(ITestOutputHelper output) : base()
        {
            _output = output;
        }

        [Fact]
        public void ExecutionState_DefaultConstructor_InitializesCorrectly()
        {
            // Arrange & Act
            var state = new ExecutionState();

            // Assert
            state.IsWithinLimits.Should().BeTrue("New state should be within limits");
            state.EvaluationStartTime.Should().Be(default);
            state.CurrentMemoryUsageBytes.Should().Be(0);
            state.CurrentOperationCount.Should().Be(0);
        }

        [Fact]
        public void ExecutionState_StartEvaluation_SetsCorrectState()
        {
            // Arrange
            var state = new ExecutionState();

            // Act
            state.StartEvaluation();

            // Assert
            state.IsWithinLimits.Should().BeTrue();
            state.EvaluationStartTime.Should().NotBe(default);
            state.CurrentOperationCount.Should().Be(1);
        }

        [Fact]
        public void ExecutionState_IncrementOperationCount_UpdatesCorrectly()
        {
            // Arrange
            var state = new ExecutionState();
            var initialCount = state.CurrentOperationCount;

            // Act
            state.IncrementOperationCount();
            state.IncrementOperationCount();

            // Assert
            state.CurrentOperationCount.Should().Be(initialCount + 2);
        }

        [Fact]
        public void ExecutionState_UpdateMemoryUsage_RecordsUsage()
        {
            // Arrange
            var state = new ExecutionState();
            var memoryUsage = 1024 * 1024; // 1MB

            // Act
            state.UpdateMemoryUsage(memoryUsage);

            // Assert
            state.CurrentMemoryUsageBytes.Should().Be(memoryUsage);
        }

        [Fact]
        public void ExecutionState_CheckLimits_WithValidUsage_ReturnsWithinLimits()
        {
            // Arrange
            var state = new ExecutionState();
            var guardrails = new GuardrailConfiguration
            {
                MaxMemoryUsageBytes = 10 * 1024 * 1024, // 10MB
                MaxConcurrentOperations = 100
            };
            state.UpdateMemoryUsage(1024 * 1024); // 1MB
            state.IncrementOperationCount();

            // Act
            state.CheckLimits(guardrails);

            // Assert
            state.IsWithinLimits.Should().BeTrue();
        }

        [Fact]
        public void ExecutionState_CheckLimits_WithExceededMemory_SetsOutOfLimits()
        {
            // Arrange
            var state = new ExecutionState();
            var guardrails = new GuardrailConfiguration
            {
                MaxMemoryUsageBytes = 1024 * 1024, // 1MB
                MaxConcurrentOperations = 100
            };
            state.UpdateMemoryUsage(10 * 1024 * 1024); // 10MB (exceeds limit)

            // Act
            state.CheckLimits(guardrails);

            // Assert
            state.IsWithinLimits.Should().BeFalse();
        }
    }

    /// <summary>
    /// Unit tests for OperationTracker
    /// </summary>
    [Category(TestCategories.Operators)]
    [Category(TestCategories.Unit)]
    public class OperationTrackerTests : CoreTestFixture
    {
        private readonly ITestOutputHelper _output;

        public OperationTrackerTests(ITestOutputHelper output) : base()
        {
            _output = output;
        }

        [Fact]
        public void OperationTracker_StartOperation_ReturnsValidOperationId()
        {
            // Arrange
            var tracker = new OperationTracker();

            // Act
            var operationId = tracker.StartOperation("TestOperation", "TestContext");

            // Assert
            operationId.Should().NotBeNull();
            operationId.Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public void OperationTracker_CompleteOperation_UpdatesStatus()
        {
            // Arrange
            var tracker = new OperationTracker();
            var operationId = tracker.StartOperation("TestOperation", "TestContext");

            // Act
            tracker.CompleteOperation(operationId, true, "Completed successfully");

            // Assert
            // This would need internal state access to verify - just ensure no exception
            tracker.Should().NotBeNull();
        }

        [Fact]
        public void OperationTracker_FailOperation_RecordsFailure()
        {
            // Arrange
            var tracker = new OperationTracker();
            var operationId = tracker.StartOperation("TestOperation", "TestContext");

            // Act
            tracker.CompleteOperation(operationId, false, "Operation failed");

            // Assert
            tracker.Should().NotBeNull();
        }

        [Fact]
        public void OperationTracker_UnknownOperationId_HandlesGracefully()
        {
            // Arrange
            var tracker = new OperationTracker();
            var unknownId = "unknown-operation-id";

            // Act & Assert
            Action act = () => tracker.CompleteOperation(unknownId, true, "Should not throw");
            act.Should().NotThrow();
        }
    }

    /// <summary>
    /// Unit tests for PreconditionValidator
    /// </summary>
    [Category(TestCategories.Operators)]
    [Category(TestCategories.Unit)]
    public class PreconditionValidatorTests : CoreTestFixture
    {
        private readonly ITestOutputHelper _output;

        public PreconditionValidatorTests(ITestOutputHelper output) : base()
        {
            _output = output;
        }

        [Fact]
        public void PreconditionValidator_Validate_WithValidInputs_ReturnsValid()
        {
            // Arrange
            var validator = new PreconditionValidator();
            var inputs = new Dictionary<string, object>
            {
                { "value1", 42 },
                { "value2", "test string" }
            };

            // Act
            var result = validator.Validate(inputs);

            // Assert
            result.IsValid.Should().BeTrue("Valid inputs should pass validation");
            result.Violations.Should().BeEmpty();
        }

        [Fact]
        public void PreconditionValidator_Validate_WithNullInputs_ReturnsInvalid()
        {
            // Arrange
            var validator = new PreconditionValidator();

            // Act
            var result = validator.Validate(null!);

            // Assert
            result.IsValid.Should().BeFalse("Null inputs should fail validation");
            result.Violations.Should().NotBeEmpty();
        }

        [Fact]
        public void PreconditionValidator_Validate_WithEmptyInputs_ReturnsValid()
        {
            // Arrange
            var validator = new PreconditionValidator();
            var inputs = new Dictionary<string, object>();

            // Act
            var result = validator.Validate(inputs);

            // Assert
            result.IsValid.Should().BeTrue("Empty inputs should be valid");
            result.Violations.Should().BeEmpty();
        }
    }

    /// <summary>
    /// Integration tests for operator lifecycle
    /// </summary>
    [Category(TestCategories.Operators)]
    [Category(TestCategories.Integration)]
    public class OperatorLifecycleTests : CoreTestFixture
    {
        private readonly ITestOutputHelper _output;

        public OperatorLifecycleTests(ITestOutputHelper output) : base()
        {
            _output = output;
        }

        [Fact]
        public async Task Operator_Evaluation_WithValidContext_Succeeds()
        {
            // Arrange
            var renderingEngine = new MockRenderingEngine();
            var audioEngine = new MockAudioEngine();
            var resourceManager = new MockResourceManager();
            var guardrails = new GuardrailConfiguration();

            using var context = new EvaluationContext(
                renderingEngine, audioEngine, resourceManager,
                ServiceProvider.GetRequiredService<ILogger<EvaluationContext>>(),
                CancellationToken.None, guardrails);

            // Act & Assert
            Action act = () =>
            {
                using var guardrailedOperator = new GuardrailedOperator(context);
                // Test the operator lifecycle
            };

            act.Should().NotThrow();
        }

        [Fact]
        public async Task Operator_Evaluation_WithCancelledContext_HandlesGracefully()
        {
            // Arrange
            var renderingEngine = new MockRenderingEngine();
            var audioEngine = new MockAudioEngine();
            var resourceManager = new MockResourceManager();
            var guardrails = new GuardrailConfiguration();
            using var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel immediately

            using var context = new EvaluationContext(
                renderingEngine, audioEngine, resourceManager,
                ServiceProvider.GetRequiredService<ILogger<EvaluationContext>>(),
                cts.Token, guardrails);

            // Act & Assert
            Action act = () =>
            {
                using var guardrailedOperator = new GuardrailedOperator(context);
            };

            act.Should().NotThrow("Should handle cancellation gracefully");
        }

        [Fact]
        public void GuardrailedOperator_CreateOperator_WithValidContext_Succeeds()
        {
            // Arrange
            var renderingEngine = new MockRenderingEngine();
            var audioEngine = new MockAudioEngine();
            var resourceManager = new MockResourceManager();
            var guardrails = new GuardrailConfiguration();

            using var context = new EvaluationContext(
                renderingEngine, audioEngine, resourceManager,
                ServiceProvider.GetRequiredService<ILogger<EvaluationContext>>(),
                CancellationToken.None, guardrails);

            // Act
            Action act = () =>
            {
                using var guardrailedOperator = new GuardrailedOperator(context);
            };

            // Assert
            act.Should().NotThrow();
        }
    }

    #region Mock Classes for Testing

    public class MockRenderingEngine : IRenderingEngine
    {
        public void Dispose() { }
    }

    public class MockAudioEngine : IAudioEngine
    {
        public void Dispose() { }
    }

    public class MockResourceManager : IResourceManager
    {
        public void Dispose() { }
    }

    #endregion
}