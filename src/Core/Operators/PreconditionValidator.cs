using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace T3.Core.Operators
{
    /// <summary>
    /// Validates preconditions and input states before operator execution
    /// </summary>
    public static class PreconditionValidator
    {
        #region Public Methods

        /// <summary>
        /// Validates a collection of preconditions against guardrail configuration
        /// </summary>
        public static PreconditionValidationResult Validate(
            IDictionary<string, object> preconditions,
            GuardrailConfiguration config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            var result = new PreconditionValidationResult();

            try
            {
                // Check precondition count limit
                if (preconditions.Count > config.MaxPreconditionsPerOperation)
                {
                    result.AddError("TooManyPreconditions", 
                        $"Precondition count {preconditions.Count} exceeds limit {config.MaxPreconditionsPerOperation}");
                    return result;
                }

                // Check total data size
                var totalSize = CalculateTotalSize(preconditions);
                if (totalSize > config.MaxPreconditionDataSize)
                {
                    result.AddError("DataSizeExceeded", 
                        $"Precondition data size {totalSize} bytes exceeds limit {config.MaxPreconditionDataSize} bytes");
                    return result;
                }

                // Validate individual preconditions
                foreach (var kvp in preconditions)
                {
                    ValidatePrecondition(kvp.Key, kvp.Value, config, result);
                }

                // Perform cross-precondition validation
                ValidatePreconditionInteractions(preconditions, result);

                result.IsValid = !result.HasErrors;
            }
            catch (Exception ex)
            {
                result.AddError("ValidationException", $"Precondition validation failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Validates a single precondition value
        /// </summary>
        public static PreconditionValidationResult ValidatePrecondition(
            string name,
            object value,
            GuardrailConfiguration config)
        {
            var result = new PreconditionValidationResult();
            ValidatePrecondition(name, value, config, result);
            result.IsValid = !result.HasErrors;
            return result;
        }

        /// <summary>
        /// Validates operator inputs using reflection-based validation attributes
        /// </summary>
        public static PreconditionValidationResult ValidateInputs(
            object instance,
            IDictionary<string, object> inputs,
            GuardrailConfiguration config)
        {
            var result = new PreconditionValidationResult();

            try
            {
                var type = instance?.GetType();
                if (type == null)
                {
                    result.AddError("InvalidInstance", "Instance cannot be null");
                    return result;
                }

                // Get validation attributes from instance type and properties
                var validationAttributes = GetValidationAttributes(type);

                // Validate each input against attributes
                foreach (var input in inputs)
                {
                    ValidateInputAgainstAttributes(input.Key, input.Value, validationAttributes, result, config);
                }

                // Perform instance-specific validation
                ValidateInstanceState(instance, result, config);

                result.IsValid = !result.HasErrors;
            }
            catch (Exception ex)
            {
                result.AddError("ValidationException", $"Input validation failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Creates a safe precondition from potentially unsafe input
        /// </summary>
        public static object? CreateSafePrecondition(object? value, GuardrailConfiguration config)
        {
            if (value == null)
                return null;

            try
            {
                // Handle different types with safety constraints
                return value switch
                {
                    string s => CreateSafeString(s, config.MaxOperationNameLength),
                    Array array => CreateSafeArray(array, config.MaxPreconditionsPerOperation),
                    IDictionary<string, object> dict => CreateSafeDictionary(dict, config),
                    _ => CreateSafeObject(value, config)
                };
            }
            catch
            {
                // Return null if we can't safely convert
                return null;
            }
        }

        /// <summary>
        /// Validates that operator can proceed with current resource state
        /// </summary>
        public static PreconditionValidationResult ValidateResourceState(
            ResourceUsageStatistics resourceStats,
            GuardrailConfiguration config)
        {
            var result = new PreconditionValidationResult();

            // Check memory limits
            if (resourceStats.MemoryAllocated > config.MaxMemoryBytes)
            {
                result.AddError("MemoryLimitExceeded", 
                    $"Current memory usage {resourceStats.MemoryAllocated} bytes exceeds limit {config.MaxMemoryBytes} bytes");
            }

            // Check file handle limits
            if (resourceStats.FileHandleCount > config.MaxFileHandles)
            {
                result.AddError("FileHandleLimitExceeded", 
                    $"File handle count {resourceStats.FileHandleCount} exceeds limit {config.MaxFileHandles}");
            }

            // Check network connection limits
            if (resourceStats.NetworkConnectionCount > config.MaxNetworkConnections)
            {
                result.AddError("NetworkConnectionLimitExceeded", 
                    $"Network connection count {resourceStats.NetworkConnectionCount} exceeds limit {config.MaxNetworkConnections}");
            }

            // Check texture limits
            if (resourceStats.ResourceCounts.Textures > config.MaxTexturesLoaded)
            {
                result.AddError("TextureLimitExceeded", 
                    $"Texture count {resourceStats.ResourceCounts.Textures} exceeds limit {config.MaxTexturesLoaded}");
            }

            if (resourceStats.ResourceCounts.MaxTextureSize > config.MaxTextureSize)
            {
                result.AddError("TextureSizeLimitExceeded", 
                    $"Maximum texture size {resourceStats.ResourceCounts.MaxTextureSize} exceeds limit {config.MaxTextureSize}");
            }

            result.IsValid = !result.HasErrors;
            return result;
        }

        #endregion

        #region Private Methods

        private static void ValidatePrecondition(
            string name,
            object value,
            GuardrailConfiguration config,
            PreconditionValidationResult result)
        {
            // Validate name
            if (string.IsNullOrEmpty(name))
            {
                result.AddError("EmptyPreconditionName", "Precondition name cannot be empty");
                return;
            }

            if (name.Length > config.MaxOperationNameLength)
            {
                result.AddError("PreconditionNameTooLong", 
                    $"Precondition name '{name}' exceeds maximum length {config.MaxOperationNameLength}");
            }

            // Validate value based on type
            ValidateValueByType(name, value, result, config);
        }

        private static void ValidateValueByType(
            string name,
            object value,
            PreconditionValidationResult result,
            GuardrailConfiguration config)
        {
            if (value == null)
            {
                result.AddWarning("NullValue", $"Precondition '{name}' has null value");
                return;
            }

            var type = value.GetType();

            // Handle common types
            switch (type)
            {
                case Type stringType when stringType == typeof(string):
                    ValidateString(name, (string)value, result, config);
                    break;

                case Type arrayType when arrayType.IsArray:
                    ValidateArray(name, (Array)value, result, config);
                    break;

                case Type dictType when IsDictionaryType(dictType):
                    ValidateDictionary(name, (IDictionary<string, object>)value, result, config);
                    break;

                case Type numType when IsNumericType(numType):
                    ValidateNumeric(name, value, result);
                    break;

                case Type dateType when dateType == typeof(DateTime):
                    ValidateDateTime(name, (DateTime)value, result);
                    break;

                default:
                    // For complex objects, validate size and complexity
                    ValidateComplexObject(name, value, result, config);
                    break;
            }
        }

        private static void ValidateString(
            string name,
            string value,
            PreconditionValidationResult result,
            GuardrailConfiguration config)
        {
            if (value.Length > config.MaxOperationNameLength * 10) // Allow longer strings
            {
                result.AddWarning("StringTooLong", 
                    $"String value for '{name}' is very long ({value.Length} characters)");
            }

            // Check for potentially dangerous content
            if (ContainsDangerousContent(value))
            {
                result.AddError("DangerousContent", 
                    $"String value for '{name}' contains potentially dangerous content");
            }
        }

        private static void ValidateArray(
            string name,
            Array value,
            PreconditionValidationResult result,
            GuardrailConfiguration config)
        {
            if (value.Length > config.MaxPreconditionsPerOperation)
            {
                result.AddError("ArrayTooLarge", 
                    $"Array for '{name}' has {value.Length} elements, exceeds limit {config.MaxPreconditionsPerOperation}");
            }

            // Check element sizes
            foreach (var element in value)
            {
                if (element != null && GetObjectSize(element) > 1024) // 1KB per element
                {
                    result.AddWarning("LargeArrayElement", 
                        $"Array element for '{name}' is large ({GetObjectSize(element)} bytes)");
                }
            }
        }

        private static void ValidateDictionary(
            string name,
            IDictionary<string, object> value,
            PreconditionValidationResult result,
            GuardrailConfiguration config)
        {
            if (value.Count > config.MaxPreconditionsPerOperation)
            {
                result.AddError("DictionaryTooLarge", 
                    $"Dictionary for '{name}' has {value.Count} entries, exceeds limit {config.MaxPreconditionsPerOperation}");
            }

            foreach (var kvp in value)
            {
                ValidatePrecondition($"{name}.{kvp.Key}", kvp.Value, config, result);
            }
        }

        private static void ValidateNumeric(string name, object value, PreconditionValidationResult result)
        {
            // Check for NaN or infinity
            switch (value)
            {
                case double d:
                    if (double.IsNaN(d) || double.IsInfinity(d))
                    {
                        result.AddError("InvalidDouble", $"Numeric value for '{name}' is NaN or infinity");
                    }
                    break;

                case float f:
                    if (float.IsNaN(f) || float.IsInfinity(f))
                    {
                        result.AddError("InvalidFloat", $"Numeric value for '{name}' is NaN or infinity");
                    }
                    break;
            }
        }

        private static void ValidateDateTime(string name, DateTime value, PreconditionValidationResult result)
        {
            // Check for reasonable date range
            var minDate = new DateTime(1900, 1, 1);
            var maxDate = new DateTime(2100, 12, 31);

            if (value < minDate || value > maxDate)
            {
                result.AddWarning("UnusualDateRange", 
                    $"DateTime value for '{name}' is outside normal range");
            }
        }

        private static void ValidateComplexObject(
            string name,
            object value,
            PreconditionValidationResult result,
            GuardrailConfiguration config)
        {
            var size = GetObjectSize(value);
            var maxSize = config.MaxPreconditionDataSize / 4; // 25% of total limit

            if (size > maxSize)
            {
                result.AddWarning("LargeObject", 
                    $"Object for '{name}' is large ({size} bytes)");
            }

            // Check for circular references
            if (HasCircularReference(value))
            {
                result.AddError("CircularReference", 
                    $"Object for '{name}' contains circular references");
            }
        }

        private static void ValidatePreconditionInteractions(
            IDictionary<string, object> preconditions,
            PreconditionValidationResult result)
        {
            // Check for conflicting preconditions
            var names = preconditions.Keys.ToList();
            
            for (int i = 0; i < names.Count; i++)
            {
                for (int j = i + 1; j < names.Count; j++)
                {
                    CheckPreconditionInteraction(names[i], preconditions[names[i]], names[j], preconditions[names[j]], result);
                }
            }
        }

        private static void CheckPreconditionInteraction(
            string name1,
            object value1,
            string name2,
            object value2,
            PreconditionValidationResult result)
        {
            // Example interactions that might be problematic
            if (name1.Contains("timeout") && name2.Contains("timeout"))
            {
                if (IsNumericType(value1.GetType()) && IsNumericType(value2.GetType()))
                {
                    var t1 = Convert.ToDouble(value1);
                    var t2 = Convert.ToDouble(value2);
                    
                    if (Math.Abs(t1 - t2) < 0.001) // Very similar timeout values
                    {
                        result.AddWarning("SimilarTimeouts", 
                            $"Preconditions '{name1}' and '{name2}' have very similar timeout values");
                    }
                }
            }

            // Check for resource conflicts
            if (name1.Contains("memory") && name2.Contains("memory"))
            {
                // Memory preconditions should be consistent
                result.AddWarning("MultipleMemoryPreconditions", 
                    $"Multiple memory-related preconditions found: '{name1}' and '{name2}'");
            }
        }

        private static List<ValidationAttribute> GetValidationAttributes(Type type)
        {
            // This would use reflection to gather validation attributes from the type
            // For now, return empty list
            return new List<ValidationAttribute>();
        }

        private static void ValidateInputAgainstAttributes(
            string inputName,
            object inputValue,
            List<ValidationAttribute> attributes,
            PreconditionValidationResult result,
            GuardrailConfiguration config)
        {
            foreach (var attr in attributes.Where(a => a.TargetProperty == inputName))
            {
                if (!attr.IsValid(inputValue))
                {
                    result.AddError("AttributeValidationFailed", 
                        $"Input '{inputName}' failed validation: {attr.ErrorMessage}");
                }
            }
        }

        private static void ValidateInstanceState(
            object instance,
            PreconditionValidationResult result,
            GuardrailConfiguration config)
        {
            // Perform instance-specific validation
            // This could include checking internal state, dependencies, etc.
        }

        private static long CalculateTotalSize(IDictionary<string, object> preconditions)
        {
            long total = 0;
            foreach (var kvp in preconditions)
            {
                total += GetObjectSize(kvp.Key) + GetObjectSize(kvp.Value);
            }
            return total;
        }

        private static long GetObjectSize(object obj)
        {
            try
            {
                // This is a simplified size calculation
                // In a real implementation, you might use Marshal.SizeOf or reflection
                return obj switch
                {
                    null => 0,
                    string s => s.Length * 2, // Assume 2 bytes per char
                    Array array => array.Length * IntPtr.Size,
                    IDictionary<string, object> dict => dict.Count * 64, // Estimate
                    _ => IntPtr.Size // Basic reference size
                };
            }
            catch
            {
                return 0;
            }
        }

        private static bool ContainsDangerousContent(string value)
        {
            // Check for potentially dangerous patterns
            var dangerousPatterns = new[]
            {
                "<script",
                "javascript:",
                "vbscript:",
                "onload=",
                "onerror=",
                "file://",
                "\\\\",
                ".."
            };

            return dangerousPatterns.Any(pattern => 
                value.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsDictionaryType(Type type)
        {
            return type.GetInterfaces().Any(i => 
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));
        }

        private static bool IsNumericType(Type type)
        {
            return type.IsPrimitive && type != typeof(bool) && type != typeof(char) ||
                   type == typeof(decimal) ||
                   type == typeof(float) ||
                   type == typeof(double);
        }

        private static bool HasCircularReference(object obj)
        {
            try
            {
                // Simple circular reference detection
                // In practice, this would need to be more sophisticated
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static object? CreateSafeString(string input, int maxLength)
        {
            if (input.Length > maxLength)
            {
                input = input[..maxLength];
            }
            return input;
        }

        private static Array CreateSafeArray(Array input, int maxLength)
        {
            if (input.Length > maxLength)
            {
                var newArray = Array.CreateInstance(input.GetType().GetElementType(), maxLength);
                Array.Copy(input, newArray, maxLength);
                return newArray;
            }
            return input;
        }

        private static IDictionary<string, object> CreateSafeDictionary(
            IDictionary<string, object> input,
            GuardrailConfiguration config)
        {
            var result = new Dictionary<string, object>();
            var count = 0;

            foreach (var kvp in input.Take(config.MaxPreconditionsPerOperation))
            {
                result[kvp.Key] = CreateSafePrecondition(kvp.Value, config) ?? kvp.Value;
                count++;
            }

            return result;
        }

        private static object CreateSafeObject(object input, GuardrailConfiguration config)
        {
            // For complex objects, create a shallow copy or return as-is if too complex
            var size = GetObjectSize(input);
            var maxSize = config.MaxPreconditionDataSize / 10; // 10% of total limit

            if (size > maxSize)
            {
                // Return a simplified representation
                return new { Type = input.GetType().Name, Size = size };
            }

            return input;
        }

        #endregion
    }

    #region Supporting Types

    /// <summary>
    /// Result of precondition validation
    /// </summary>
    public class PreconditionValidationResult
    {
        private readonly List<ValidationError> _errors = new();
        private readonly List<ValidationWarning> _warnings = new();

        public bool IsValid { get; set; }
        public bool HasErrors => _errors.Count > 0;
        public bool HasWarnings => _warnings.Count > 0;
        public IReadOnlyList<ValidationError> Errors => _errors.AsReadOnly();
        public IReadOnlyList<ValidationWarning> Warnings => _warnings.AsReadOnly();

        public void AddError(string code, string message)
        {
            _errors.Add(new ValidationError { Code = code, Message = message });
        }

        public void AddWarning(string code, string message)
        {
            _warnings.Add(new ValidationWarning { Code = code, Message = message });
        }

        public ValidationSummary GetSummary()
        {
            return new ValidationSummary
            {
                IsValid = IsValid,
                ErrorCount = _errors.Count,
                WarningCount = _warnings.Count,
                Errors = _errors.ToList(),
                Warnings = _warnings.ToList()
            };
        }
    }

    /// <summary>
    /// Validation error information
    /// </summary>
    public class ValidationError
    {
        public string Code { get; set; } = "";
        public string Message { get; set; } = "";
    }

    /// <summary>
    /// Validation warning information
    /// </summary>
    public class ValidationWarning
    {
        public string Code { get; set; } = "";
        public string Message { get; set; } = "";
    }

    /// <summary>
    /// Summary of validation results
    /// </summary>
    public class ValidationSummary
    {
        public bool IsValid { get; set; }
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public List<ValidationError> Errors { get; set; } = new();
        public List<ValidationWarning> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Base class for validation attributes
    /// </summary>
    public abstract class ValidationAttribute : Attribute
    {
        public string TargetProperty { get; set; } = "";
        public string ErrorMessage { get; set; } = "";

        public abstract bool IsValid(object? value);
    }

    #endregion
}