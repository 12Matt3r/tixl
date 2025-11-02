using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;

namespace TiXL.Core.Validation.Attributes
{
    /// <summary>
    /// Base class for TiXL-specific validation attributes
    /// </summary>
    public abstract class TiXLValidationAttribute : ValidationAttribute
    {
        protected TiXLValidationAttribute(string errorMessage = null) : base(errorMessage)
        {
        }

        /// <summary>
        /// Gets the name of the parameter being validated
        /// </summary>
        protected string GetParameterName(ValidationContext validationContext)
        {
            return validationContext?.DisplayName ?? "Value";
        }

        /// <summary>
        /// Gets the object instance being validated
        /// </summary>
        protected object GetInstance(ValidationContext validationContext)
        {
            return validationContext?.ObjectInstance;
        }
    }

    /// <summary>
    /// Validates that a value is positive
    /// </summary>
    public class PositiveAttribute : TiXLValidationAttribute
    {
        public PositiveAttribute(string errorMessage = null) : base(errorMessage)
        {
        }

        public override bool IsValid(object value)
        {
            if (value == null) return true; // Null validation should be handled separately

            switch (value)
            {
                case int intValue:
                    return intValue > 0;
                case double doubleValue:
                    return doubleValue > 0;
                case float floatValue:
                    return floatValue > 0;
                case long longValue:
                    return longValue > 0;
                default:
                    return false;
            }
        }

        public override string FormatErrorMessage(string name)
        {
            return ErrorMessage ?? $"{name} must be a positive number";
        }
    }

    /// <summary>
    /// Validates that a value is non-negative
    /// </summary>
    public class NonNegativeAttribute : TiXLValidationAttribute
    {
        public NonNegativeAttribute(string errorMessage = null) : base(errorMessage)
        {
        }

        public override bool IsValid(object value)
        {
            if (value == null) return true; // Null validation should be handled separately

            switch (value)
            {
                case int intValue:
                    return intValue >= 0;
                case double doubleValue:
                    return doubleValue >= 0;
                case float floatValue:
                    return floatValue >= 0;
                case long longValue:
                    return longValue >= 0;
                default:
                    return false;
            }
        }

        public override string FormatErrorMessage(string name)
        {
            return ErrorMessage ?? $"{name} must be a non-negative number";
        }
    }

    /// <summary>
    /// Validates that a string is not null, empty, or whitespace
    /// </summary>
    public class NotNullOrWhiteSpaceAttribute : TiXLValidationAttribute
    {
        public NotNullOrWhiteSpaceAttribute(string errorMessage = null) : base(errorMessage)
        {
        }

        public override bool IsValid(object value)
        {
            if (value == null) return false;
            return !string.IsNullOrWhiteSpace(value.ToString());
        }

        public override string FormatErrorMessage(string name)
        {
            return ErrorMessage ?? $"{name} cannot be null, empty, or whitespace";
        }
    }

    /// <summary>
    /// Validates the length of a string
    /// </summary>
    public class LengthAttribute : TiXLValidationAttribute
    {
        private readonly int _maxLength;
        private readonly int _minLength;

        public LengthAttribute(int maxLength, int minLength = 1, string errorMessage = null) : base(errorMessage)
        {
            _maxLength = maxLength;
            _minLength = minLength;
        }

        public override bool IsValid(object value)
        {
            if (value == null) return true; // Null validation should be handled separately

            var str = value.ToString();
            return str.Length >= _minLength && str.Length <= _maxLength;
        }

        public override string FormatErrorMessage(string name)
        {
            return ErrorMessage ?? $"{name} must be between {_minLength} and {_maxLength} characters long";
        }
    }

    /// <summary>
    /// Validates that a value is within a specified range
    /// </summary>
    public class RangeAttribute : TiXLValidationAttribute
    {
        private readonly double _minValue;
        private readonly double _maxValue;

        public RangeAttribute(double minValue, double maxValue, string errorMessage = null) : base(errorMessage)
        {
            _minValue = minValue;
            _maxValue = maxValue;
        }

        public override bool IsValid(object value)
        {
            if (value == null) return true; // Null validation should be handled separately

            switch (value)
            {
                case int intValue:
                    return intValue >= _minValue && intValue <= _maxValue;
                case double doubleValue:
                    return doubleValue >= _minValue && doubleValue <= _maxValue;
                case float floatValue:
                    return floatValue >= _minValue && floatValue <= _maxValue;
                default:
                    return false;
            }
        }

        public override string FormatErrorMessage(string name)
        {
            return ErrorMessage ?? $"{name} must be between {_minValue} and {_maxValue}";
        }
    }

    /// <summary>
    /// Validates file paths for security and safety
    /// </summary>
    public class ValidFilePathAttribute : TiXLValidationAttribute
    {
        private readonly bool _allowCreate;

        public ValidFilePathAttribute(bool allowCreate = false, string errorMessage = null) : base(errorMessage)
        {
            _allowCreate = allowCreate;
        }

        public override bool IsValid(object value)
        {
            if (value == null) return true; // Null validation should be handled separately

            try
            {
                var path = value.ToString();
                if (string.IsNullOrWhiteSpace(path)) return false;

                // Basic path validation
                if (path.Contains("../") || path.Contains("..\\"))
                    return false;

                // Check if absolute path
                if (!System.IO.Path.IsPathRooted(path))
                    return false;

                // Check path length
                var fullPath = System.IO.Path.GetFullPath(path);
                if (fullPath.Length > 260)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        public override string FormatErrorMessage(string name)
        {
            return ErrorMessage ?? $"{name} is not a valid file path";
        }
    }

    /// <summary>
    /// Validates collection size
    /// </summary>
    public class CollectionSizeAttribute : TiXLValidationAttribute
    {
        private readonly int _maxSize;
        private readonly int _minSize;

        public CollectionSizeAttribute(int maxSize, int minSize = 0, string errorMessage = null) : base(errorMessage)
        {
            _maxSize = maxSize;
            _minSize = minSize;
        }

        public override bool IsValid(object value)
        {
            if (value == null) return true; // Null validation should be handled separately

            if (value is System.Collections.ICollection collection)
            {
                return collection.Count >= _minSize && collection.Count <= _maxSize;
            }

            if (value is System.Collections.IEnumerable enumerable)
            {
                var count = enumerable.Cast<object>().Count();
                return count >= _minSize && count <= _maxSize;
            }

            return false;
        }

        public override string FormatErrorMessage(string name)
        {
            return ErrorMessage ?? $"{name} must contain between {_minSize} and {_maxSize} items";
        }
    }

    /// <summary>
    /// Validates DirectX object state
    /// </summary>
    public class ValidDirectXObjectAttribute : TiXLValidationAttribute
    {
        public ValidDirectXObjectAttribute(string errorMessage = null) : base(errorMessage)
        {
        }

        public override bool IsValid(object value)
        {
            if (value == null) return false; // DirectX objects should not be null

            try
            {
                // Try to access a property or method to verify the object is not disposed
                var hashCode = value.GetHashCode();
                return hashCode != 0; // Disposed objects often return 0 for GetHashCode()
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch
            {
                // If we can't determine the state, assume it's invalid
                return false;
            }
        }

        public override string FormatErrorMessage(string name)
        {
            return ErrorMessage ?? $"{name} is not a valid DirectX object or has been disposed";
        }
    }

    /// <summary>
    /// Validates operation names and similar identifiers
    /// </summary>
    public class ValidOperationNameAttribute : TiXLValidationAttribute
    {
        private readonly int _maxLength;

        public ValidOperationNameAttribute(int maxLength = 256, string errorMessage = null) : base(errorMessage)
        {
            _maxLength = maxLength;
        }

        public override bool IsValid(object value)
        {
            if (value == null) return false; // Operation names should not be null

            var name = value.ToString();
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (name.Length > _maxLength) return false;

            // Check for invalid characters (alphanumeric, underscore, hyphen, dot, space)
            foreach (char c in name)
            {
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-' && c != '.' && c != ' ')
                {
                    return false;
                }
            }

            return true;
        }

        public override string FormatErrorMessage(string name)
        {
            return ErrorMessage ?? $"{name} contains invalid characters or exceeds maximum length ({_maxLength})";
        }
    }

    /// <summary>
    /// Validates that a value matches a specific pattern
    /// </summary>
    public class MatchesPatternAttribute : TiXLValidationAttribute
    {
        private readonly string _pattern;
        private readonly System.Text.RegularExpressions.RegexOptions _options;

        public MatchesPatternAttribute(string pattern, System.Text.RegularExpressions.RegexOptions options = System.Text.RegularExpressions.RegexOptions.None, string errorMessage = null) : base(errorMessage)
        {
            _pattern = pattern;
            _options = options;
        }

        public override bool IsValid(object value)
        {
            if (value == null) return true; // Null validation should be handled separately

            var str = value.ToString();
            return System.Text.RegularExpressions.Regex.IsMatch(str, _pattern, _options);
        }

        public override string FormatErrorMessage(string name)
        {
            return ErrorMessage ?? $"{name} does not match the required pattern";
        }
    }

    /// <summary>
    /// Validator helper class for reflection-based validation
    /// </summary>
    public static class ReflectionValidator
    {
        /// <summary>
        /// Validates an object using validation attributes
        /// </summary>
        /// <param name="instance">Object to validate</param>
        /// <returns>Validation result with any errors</returns>
        public static ValidationResult Validate(object instance)
        {
            if (instance == null)
                return ValidationResult.Success("Object is null");

            var validationResults = new List<ValidationResult>();
            var validationContext = new ValidationContext(instance);
            Validator.TryValidateObject(instance, validationContext, validationResults, true);

            if (validationResults.Count == 0)
            {
                return ValidationResult.Success("Validation passed");
            }

            var errorMessages = string.Join("; ", validationResults.Select(vr => vr.ErrorMessage));
            return ValidationResult.Failure(errorMessages);
        }

        /// <summary>
        /// Validates a specific property using validation attributes
        /// </summary>
        /// <param name="instance">Object instance</param>
        /// <param name="propertyName">Property name to validate</param>
        /// <returns>Validation result</returns>
        public static ValidationResult ValidateProperty(object instance, string propertyName)
        {
            if (instance == null)
                return ValidationResult.Failure("Object is null");

            var propertyInfo = instance.GetType().GetProperty(propertyName);
            if (propertyInfo == null)
            {
                return ValidationResult.Failure($"Property '{propertyName}' not found");
            }

            var value = propertyInfo.GetValue(instance);
            var validationContext = new ValidationContext(instance)
            {
                DisplayName = propertyName,
                MemberName = propertyName
            };

            var validationResults = new List<ValidationResult>();
            Validator.TryValidateProperty(value, validationContext, validationResults);

            if (validationResults.Count == 0)
            {
                return ValidationResult.Success("Property validation passed");
            }

            var errorMessages = string.Join("; ", validationResults.Select(vr => vr.ErrorMessage));
            return ValidationResult.Failure(errorMessages);
        }

        /// <summary>
        /// Gets all validation attributes for a property
        /// </summary>
        /// <param name="type">Type to inspect</param>
        /// <param name="propertyName">Property name</param>
        /// <returns>Collection of validation attributes</returns>
        public static IEnumerable<ValidationAttribute> GetValidationAttributes(Type type, string propertyName)
        {
            var propertyInfo = type.GetProperty(propertyName);
            if (propertyInfo == null) return Enumerable.Empty<ValidationAttribute>();

            return propertyInfo.GetCustomAttributes<ValidationAttribute>(inherit: true);
        }
    }

    /// <summary>
    /// Validation result class
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; }
        public string Message { get; }

        private ValidationResult(bool isValid, string message)
        {
            IsValid = isValid;
            Message = message;
        }

        public static ValidationResult Success(string message = "Validation passed")
        {
            return new ValidationResult(true, message);
        }

        public static ValidationResult Failure(string message)
        {
            return new ValidationResult(false, message);
        }

        public override string ToString()
        {
            return IsValid ? $"✓ {Message}" : $"✗ {Message}";
        }
    }
}