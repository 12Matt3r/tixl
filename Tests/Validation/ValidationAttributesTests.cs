using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using TiXL.Core.Validation.Attributes;
using Xunit;

namespace TiXL.Tests.Validation
{
    /// <summary>
    /// Tests for validation attributes
    /// </summary>
    public class ValidationAttributesTests
    {
        #region PositiveAttribute Tests

        [Theory]
        [InlineData(1)]
        [InlineData(100)]
        [InlineData(1.5)]
        [InlineData(double.MaxValue)]
        public void PositiveAttribute_WithPositiveValues_IsValid(object positiveValue)
        {
            // Arrange
            var attribute = new PositiveAttribute();

            // Act
            var isValid = attribute.IsValid(positiveValue);

            // Assert
            Assert.True(isValid);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        [InlineData(-1.5)]
        [InlineData(double.MinValue)]
        public void PositiveAttribute_WithNonPositiveValues_IsInvalid(object nonPositiveValue)
        {
            // Arrange
            var attribute = new PositiveAttribute();

            // Act
            var isValid = attribute.IsValid(nonPositiveValue);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void PositiveAttribute_WithNullValue_IsValid()
        {
            // Arrange
            var attribute = new PositiveAttribute();

            // Act
            var isValid = attribute.IsValid(null);

            // Assert
            Assert.True(isValid);
        }

        #endregion

        #region NonNegativeAttribute Tests

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(100)]
        [InlineData(1.5)]
        [InlineData(double.MaxValue)]
        public void NonNegativeAttribute_WithNonNegativeValues_IsValid(object nonNegativeValue)
        {
            // Arrange
            var attribute = new NonNegativeAttribute();

            // Act
            var isValid = attribute.IsValid(nonNegativeValue);

            // Assert
            Assert.True(isValid);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(-100)]
        [InlineData(-1.5)]
        [InlineData(double.MinValue)]
        public void NonNegativeAttribute_WithNegativeValues_IsInvalid(object negativeValue)
        {
            // Arrange
            var attribute = new NonNegativeAttribute();

            // Act
            var isValid = attribute.IsValid(negativeValue);

            // Assert
            Assert.False(isValid);
        }

        #endregion

        #region NotNullOrWhiteSpaceAttribute Tests

        [Theory]
        [InlineData("valid")]
        [InlineData("test123")]
        [InlineData("   non-whitespace   ")]
        public void NotNullOrWhiteSpaceAttribute_WithValidStrings_IsValid(string validString)
        {
            // Arrange
            var attribute = new NotNullOrWhiteSpaceAttribute();

            // Act
            var isValid = attribute.IsValid(validString);

            // Assert
            Assert.True(isValid);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t\n\r")]
        public void NotNullOrWhiteSpaceAttribute_WithInvalidStrings_IsInvalid(object invalidString)
        {
            // Arrange
            var attribute = new NotNullOrWhiteSpaceAttribute();

            // Act
            var isValid = attribute.IsValid(invalidString);

            // Assert
            Assert.False(isValid);
        }

        #endregion

        #region LengthAttribute Tests

        [Theory]
        [InlineData("valid", 5, 10)]
        [InlineData("12345", 5, 10)]
        [InlineData("test", 1, 10)]
        public void LengthAttribute_WithValidLength_IsValid(string value, int minLength, int maxLength)
        {
            // Arrange
            var attribute = new LengthAttribute(maxLength, minLength);

            // Act
            var isValid = attribute.IsValid(value);

            // Assert
            Assert.True(isValid);
        }

        [Theory]
        [InlineData("short", 10, 20)]
        [InlineData("toolongstringthatdefinitelyexceedsmaximum", 10, 20)]
        public void LengthAttribute_WithInvalidLength_IsInvalid(string value, int minLength, int maxLength)
        {
            // Arrange
            var attribute = new LengthAttribute(maxLength, minLength);

            // Act
            var isValid = attribute.IsValid(value);

            // Assert
            Assert.False(isValid);
        }

        #endregion

        #region RangeAttribute Tests

        [Theory]
        [InlineData(5, 0, 10)]
        [InlineData(0, 0, 10)]
        [InlineData(10, 0, 10)]
        [InlineData(7.5, 0, 10)]
        public void RangeAttribute_WithValueInRange_IsValid(double value, double minValue, double maxValue)
        {
            // Arrange
            var attribute = new RangeAttribute(minValue, maxValue);

            // Act
            var isValid = attribute.IsValid(value);

            // Assert
            Assert.True(isValid);
        }

        [Theory]
        [InlineData(-1, 0, 10)]
        [InlineData(11, 0, 10)]
        [InlineData(100, 0, 10)]
        public void RangeAttribute_WithValueOutOfRange_IsInvalid(double value, double minValue, double maxValue)
        {
            // Arrange
            var attribute = new RangeAttribute(minValue, maxValue);

            // Act
            var isValid = attribute.IsValid(value);

            // Assert
            Assert.False(isValid);
        }

        #endregion

        #region ValidFilePathAttribute Tests

        [Theory]
        [InlineData("/tmp/test.txt")]
        [InlineData("C:\\Windows\\System32")]
        [InlineData("/var/log/app.log")]
        public void ValidFilePathAttribute_WithValidPaths_IsValid(string validPath)
        {
            // Arrange
            var attribute = new ValidFilePathAttribute();

            // Act
            var isValid = attribute.IsValid(validPath);

            // Assert
            Assert.True(isValid);
        }

        [Theory]
        [InlineData("../malicious")]
        [InlineData("..\\malicious")]
        [InlineData("rel/path")]
        [InlineData("")]
        [InlineData(null)]
        public void ValidFilePathAttribute_WithInvalidPaths_IsInvalid(string invalidPath)
        {
            // Arrange
            var attribute = new ValidFilePathAttribute();

            // Act
            var isValid = attribute.IsValid(invalidPath);

            // Assert
            Assert.False(isValid);
        }

        #endregion

        #region CollectionSizeAttribute Tests

        [Fact]
        public void CollectionSizeAttribute_WithValidSize_IsValid()
        {
            // Arrange
            var list = new List<int> { 1, 2, 3, 4, 5 };
            var attribute = new CollectionSizeAttribute(10, 1);

            // Act
            var isValid = attribute.IsValid(list);

            // Assert
            Assert.True(isValid);
        }

        [Theory]
        [InlineData(0)] // Empty collection
        [InlineData(11)] // Too many items
        public void CollectionSizeAttribute_WithInvalidSize_IsInvalid(int itemCount)
        {
            // Arrange
            var list = Enumerable.Range(1, itemCount).ToList();
            var attribute = new CollectionSizeAttribute(10, 1);

            // Act
            var isValid = attribute.IsValid(list);

            // Assert
            Assert.False(isValid);
        }

        #endregion

        #region ValidOperationNameAttribute Tests

        [Theory]
        [InlineData("validOperation")]
        [InlineData("test123")]
        [InlineData("operation-name")]
        [InlineData("operation.name")]
        [InlineData("operation name")]
        public void ValidOperationNameAttribute_WithValidNames_IsValid(string validName)
        {
            // Arrange
            var attribute = new ValidOperationNameAttribute();

            // Act
            var isValid = attribute.IsValid(validName);

            // Assert
            Assert.True(isValid);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("operation@name")]
        [InlineData("operation#name")]
        [InlineData("operation!name")]
        public void ValidOperationNameAttribute_WithInvalidNames_IsInvalid(string invalidName)
        {
            // Arrange
            var attribute = new ValidOperationNameAttribute();

            // Act
            var isValid = attribute.IsValid(invalidName);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void ValidOperationNameAttribute_WithExceedingLength_IsInvalid()
        {
            // Arrange
            var longName = new string('a', 300); // Exceeds default max length of 256
            var attribute = new ValidOperationNameAttribute();

            // Act
            var isValid = attribute.IsValid(longName);

            // Assert
            Assert.False(isValid);
        }

        #endregion

        #region MatchesPatternAttribute Tests

        [Theory]
        [InlineData("test123", @"^[a-zA-Z0-9]+$")]
        [InlineData("email@test.com", @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$")]
        [InlineData("123-456-7890", @"^\d{3}-\d{3}-\d{4}$")]
        public void MatchesPatternAttribute_WithMatchingStrings_IsValid(string value, string pattern)
        {
            // Arrange
            var attribute = new MatchesPatternAttribute(pattern);

            // Act
            var isValid = attribute.IsValid(value);

            // Assert
            Assert.True(isValid);
        }

        [Theory]
        [InlineData("test@!", @"^[a-zA-Z0-9]+$")]
        [InlineData("invalid-email", @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$")]
        [InlineData("1234567890", @"^\d{3}-\d{3}-\d{4}$")]
        public void MatchesPatternAttribute_WithNonMatchingStrings_IsInvalid(string value, string pattern)
        {
            // Arrange
            var attribute = new MatchesPatternAttribute(pattern);

            // Act
            var isValid = attribute.IsValid(value);

            // Assert
            Assert.False(isValid);
        }

        #endregion

        #region ReflectionValidator Tests

        [Fact]
        public void Validate_WithValidObject_ReturnsSuccess()
        {
            // Arrange
            var validObject = new TestValidationClass
            {
                Name = "ValidName",
                Value = 5.0,
                Count = 10
            };

            // Act
            var result = ReflectionValidator.Validate(validObject);

            // Assert
            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_WithInvalidObject_ReturnsFailure()
        {
            // Arrange
            var invalidObject = new TestValidationClass
            {
                Name = "", // Empty name violates NotNullOrWhiteSpaceAttribute
                Value = -1.0, // Negative value violates NonNegativeAttribute
                Count = -5 // Negative count violates NonNegativeAttribute
            };

            // Act
            var result = ReflectionValidator.Validate(invalidObject);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Message);
        }

        [Fact]
        public void ValidateProperty_WithValidProperty_ReturnsSuccess()
        {
            // Arrange
            var validObject = new TestValidationClass
            {
                Name = "ValidName",
                Value = 5.0,
                Count = 10
            };

            // Act
            var result = ReflectionValidator.ValidateProperty(validObject, nameof(TestValidationClass.Name));

            // Assert
            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("NonExistentProperty")]
        [InlineData("")]
        [InlineData(null)]
        public void ValidateProperty_WithInvalidPropertyName_ThrowsOrReturnsFailure(string propertyName)
        {
            // Arrange
            var validObject = new TestValidationClass { Name = "Test" };

            // Act
            if (string.IsNullOrEmpty(propertyName))
            {
                // Should handle null/empty gracefully
                var result = ReflectionValidator.ValidateProperty(validObject, propertyName);
                Assert.False(result.IsValid);
            }
            else
            {
                // Should return failure for non-existent property
                var result = ReflectionValidator.ValidateProperty(validObject, propertyName);
                Assert.False(result.IsValid);
                Assert.Contains("not found", result.Message);
            }
        }

        #endregion

        #region Test Classes

        private class TestValidationClass
        {
            [NotNullOrWhiteSpace]
            public string Name { get; set; } = "";

            [NonNegative]
            public double Value { get; set; }

            [NonNegative]
            public int Count { get; set; }
        }

        #endregion
    }
}