using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using TiXL.Core.Validation;
using TiXL.Core.Validation.Attributes;
using Xunit;

namespace TiXL.Tests.Validation
{
    /// <summary>
    /// Tests for ValidationHelpers class
    /// </summary>
    public class ValidationHelpersTests
    {
        #region Argument Validation Tests

        [Fact]
        public void ThrowIfNull_WithNullValue_ThrowsArgumentNullException()
        {
            // Arrange
            string nullString = null;

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => 
                ValidationHelpers.ThrowIfNull(nullString, nameof(nullString)));
            Assert.Equal(nameof(nullString), ex.ParamName);
        }

        [Fact]
        public void ThrowIfNull_WithValidValue_DoesNotThrow()
        {
            // Arrange
            string validString = "test";

            // Act & Assert
            var ex = Record.Exception(() => 
                ValidationHelpers.ThrowIfNull(validString, nameof(validString)));
            Assert.Null(ex);
        }

        [Fact]
        public void ThrowIfNullOrEmpty_WithNullValue_ThrowsArgumentNullException()
        {
            // Arrange
            string nullString = null;

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => 
                ValidationHelpers.ThrowIfNullOrEmpty(nullString, nameof(nullString)));
            Assert.Equal(nameof(nullString), ex.ParamName);
        }

        [Fact]
        public void ThrowIfNullOrEmpty_WithEmptyString_ThrowsArgumentException()
        {
            // Arrange
            string emptyString = "";

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => 
                ValidationHelpers.ThrowIfNullOrEmpty(emptyString, nameof(emptyString)));
            Assert.Equal(nameof(emptyString), ex.ParamName);
            Assert.Contains("cannot be empty", ex.Message);
        }

        [Fact]
        public void ThrowIfNullOrWhiteSpace_WithWhitespaceString_ThrowsArgumentException()
        {
            // Arrange
            string whitespaceString = "   ";

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => 
                ValidationHelpers.ThrowIfNullOrWhiteSpace(whitespaceString, nameof(whitespaceString)));
            Assert.Equal(nameof(whitespaceString), ex.ParamName);
            Assert.Contains("cannot be empty or whitespace", ex.Message);
        }

        [Theory]
        [InlineData("valid string")]
        [InlineData("test123")]
        [InlineData("special!@#$%")]
        public void ValidateString_WithValidStrings_DoesNotThrow(string validString)
        {
            // Act & Assert
            var ex = Record.Exception(() => 
                ValidationHelpers.ValidateString(validString, 256, nameof(validString)));
            Assert.Null(ex);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ValidateString_WithInvalidStrings_ThrowsException(string invalidString)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => 
                ValidationHelpers.ValidateString(invalidString, 256, nameof(invalidString)));
        }

        [Theory]
        [InlineData("too long string that exceeds the maximum allowed length limit", 50)]
        public void ValidateString_WithExceedingLength_ThrowsArgumentException(string longString, int maxLength)
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => 
                ValidationHelpers.ValidateString(longString, maxLength, nameof(longString)));
            Assert.Contains("cannot exceed", ex.Message);
        }

        #endregion

        #region Numeric Validation Tests

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public void ValidatePositive_WithNonPositiveValues_ThrowsArgumentOutOfRangeException(double value)
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => 
                ValidationHelpers.ValidatePositive(value, nameof(value)));
            Assert.Equal(nameof(value), ex.ParamName);
        }

        [Theory]
        [InlineData(0.1)]
        [InlineData(1)]
        [InlineData(100)]
        [InlineData(double.MaxValue)]
        public void ValidatePositive_WithPositiveValues_DoesNotThrow(double value)
        {
            // Act & Assert
            var ex = Record.Exception(() => 
                ValidationHelpers.ValidatePositive(value, nameof(value)));
            Assert.Null(ex);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(-100)]
        [InlineData(double.MinValue)]
        public void ValidateNonNegative_WithNegativeValues_ThrowsArgumentOutOfRangeException(double value)
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => 
                ValidationHelpers.ValidateNonNegative(value, nameof(value)));
            Assert.Equal(nameof(value), ex.ParamName);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(0.1)]
        [InlineData(1)]
        [InlineData(double.MaxValue)]
        public void ValidateNonNegative_WithNonNegativeValues_DoesNotThrow(double value)
        {
            // Act & Assert
            var ex = Record.Exception(() => 
                ValidationHelpers.ValidateNonNegative(value, nameof(value)));
            Assert.Null(ex);
        }

        [Theory]
        [InlineData(0, 10, 5)]
        [InlineData(-5, 5, 0)]
        [InlineData(1.5, 2.5, 2.0)]
        public void ValidateRange_WithValueInRange_DoesNotThrow(double value, double min, double max)
        {
            // Act & Assert
            var ex = Record.Exception(() => 
                ValidationHelpers.ValidateRange(value, min, max, nameof(value)));
            Assert.Null(ex);
        }

        [Theory]
        [InlineData(0, 10, 15)]
        [InlineData(-5, 5, -10)]
        [InlineData(1.5, 2.5, 3.0)]
        public void ValidateRange_WithValueOutOfRange_ThrowsArgumentOutOfRangeException(double value, double min, double max)
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => 
                ValidationHelpers.ValidateRange(value, min, max, nameof(value)));
            Assert.Equal(nameof(value), ex.ParamName);
            Assert.Contains($"must be between {min} and {max}", ex.Message);
        }

        #endregion

        #region File Path Validation Tests

        [Fact]
        public void ValidateFilePath_WithNullPath_ThrowsArgumentNullException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => 
                ValidationHelpers.ValidateFilePath(null, nameof(null)));
            Assert.Equal(nameof(null), ex.ParamName);
        }

        [Fact]
        public void ValidateFilePath_WithEmptyPath_ThrowsArgumentException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => 
                ValidationHelpers.ValidateFilePath("", nameof(string.Empty)));
            Assert.Contains("cannot be empty or whitespace", ex.Message);
        }

        [Theory]
        [InlineData("../malicious/path")]
        [InlineData("..\\malicious\\path")]
        [InlineData("..%2Fmalicious")]
        [InlineData("..%252Fmalicious")]
        public void ValidateFilePath_WithPathTraversalAttempts_ThrowsArgumentException(string maliciousPath)
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => 
                ValidationHelpers.ValidateFilePath(maliciousPath));
            Assert.Contains("Potentially unsafe file path", ex.Message);
        }

        [Fact]
        public void ValidateFilePath_WithValidPath_DoesNotThrow()
        {
            // Arrange
            var validPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "valid", "path.txt");

            // Act & Assert
            var ex = Record.Exception(() => 
                ValidationHelpers.ValidateFilePath(validPath, allowCreate: true));
            Assert.Null(ex);
        }

        #endregion

        #region Input Sanitization Tests

        [Fact]
        public void SanitizeUserInput_WithDangerousCharacters_RemovesThem()
        {
            // Arrange
            var dangerousInput = "<script>alert('xss')</script>";
            var expectedSanitized = "&lt;script&gt;alert(&#x27;xss&#x27;)&lt;/script&gt;";

            // Act
            var sanitized = ValidationHelpers.SanitizeUserInput(dangerousInput);

            // Assert
            Assert.Equal(expectedSanitized, sanitized);
        }

        [Fact]
        public void SanitizeFileName_WithInvalidCharacters_ReplacesThem()
        {
            // Arrange
            var invalidFileName = "test<>:|?*file.txt";

            // Act
            var sanitized = ValidationHelpers.SanitizeFileName(invalidFileName);

            // Assert
            Assert.Contains("test", sanitized);
            Assert.Contains("file.txt", sanitized);
            Assert.DoesNotContain("<", sanitized);
            Assert.DoesNotContain(">", sanitized);
            Assert.DoesNotContain(":", sanitized);
        }

        #endregion

        #region Defensive Programming Tests

        [Fact]
        public void NullSafe_WithNullValue_ReturnsFallback()
        {
            // Arrange
            string nullValue = null;
            string fallback = "fallback";

            // Act
            var result = ValidationHelpers.NullSafe(nullValue, fallback);

            // Assert
            Assert.Equal(fallback, result);
        }

        [Fact]
        public void NullSafe_WithValidValue_ReturnsOriginal()
        {
            // Arrange
            string validValue = "original";
            string fallback = "fallback";

            // Act
            var result = ValidationHelpers.NullSafe(validValue, fallback);

            // Assert
            Assert.Equal(validValue, result);
        }

        [Fact]
        public void SafeExecute_WithThrowingFunction_ReturnsDefaultValue()
        {
            // Arrange
            var defaultValue = "default";

            // Act
            var result = ValidationHelpers.SafeExecute(() => throw new Exception("test"), defaultValue);

            // Assert
            Assert.Equal(defaultValue, result);
        }

        [Fact]
        public void SafeExecute_WithSuccessfulFunction_ReturnsResult()
        {
            // Arrange
            var expectedResult = "success";

            // Act
            var result = ValidationHelpers.SafeExecute(() => expectedResult, "default");

            // Assert
            Assert.Equal(expectedResult, result);
        }

        #endregion
    }
}