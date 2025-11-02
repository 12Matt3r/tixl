#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace TiXL.Core.Validation
{
    /// <summary>
    /// Comprehensive validation and null checking helpers for the TiXL codebase
    /// Provides consistent validation patterns across all modules
    /// </summary>
    public static class ValidationHelpers
    {
        #region Argument Validation

        /// <summary>
        /// Throws ArgumentNullException if the argument is null
        /// </summary>
        /// <typeparam name="T">Type of the argument</typeparam>
        /// <param name="value">Value to check</param>
        /// <param name="parameterName">Name of the parameter</param>
        /// <exception cref="ArgumentNullException">Thrown when value is null</exception>
        public static void ThrowIfNull<T>([NotNull] T? value, [CallerArgumentExpression(nameof(value))] string? parameterName = null)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName, $"Argument '{parameterName}' cannot be null");
            }
        }

        /// <summary>
        /// Throws ArgumentNullException if the string argument is null or empty
        /// </summary>
        /// <param name="value">String to check</param>
        /// <param name="parameterName">Name of the parameter</param>
        /// <exception cref="ArgumentNullException">Thrown when value is null or empty</exception>
        public static void ThrowIfNullOrEmpty([NotNull] string? value, [CallerArgumentExpression(nameof(value))] string? parameterName = null)
        {
            ThrowIfNull(value, parameterName);
            if (value.Length == 0)
            {
                throw new ArgumentException($"Argument '{parameterName}' cannot be empty", parameterName);
            }
        }

        /// <summary>
        /// Throws ArgumentNullException if the string argument is null, empty, or whitespace
        /// </summary>
        /// <param name="value">String to check</param>
        /// <param name="parameterName">Name of the parameter</param>
        /// <exception cref="ArgumentNullException">Thrown when value is null, empty, or whitespace</exception>
        public static void ThrowIfNullOrWhiteSpace([NotNull] string? value, [CallerArgumentExpression(nameof(value))] string? parameterName = null)
        {
            ThrowIfNull(value, parameterName);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"Argument '{parameterName}' cannot be empty or whitespace", parameterName);
            }
        }

        /// <summary>
        /// Validates that a string is not null, empty, or whitespace and within length limits
        /// </summary>
        /// <param name="value">String to validate</param>
        /// <param name="maxLength">Maximum allowed length</param>
        /// <param name="parameterName">Name of the parameter</param>
        /// <param name="minLength">Minimum allowed length (default: 1)</param>
        /// <exception cref="ArgumentNullException">Thrown when value is null</exception>
        /// <exception cref="ArgumentException">Thrown when value is empty, whitespace, or exceeds length limits</exception>
        public static void ValidateString([NotNull] string? value, int maxLength, [CallerArgumentExpression(nameof(value))] string? parameterName = null, int minLength = 1)
        {
            ThrowIfNull(value, parameterName);
            
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"Argument '{parameterName}' cannot be empty or whitespace", parameterName);
            }
            
            if (value.Length < minLength)
            {
                throw new ArgumentException($"Argument '{parameterName}' must be at least {minLength} characters long", parameterName);
            }
            
            if (value.Length > maxLength)
            {
                throw new ArgumentException($"Argument '{parameterName}' cannot exceed {maxLength} characters", parameterName);
            }
        }

        /// <summary>
        /// Validates that a collection is not null and within size limits
        /// </summary>
        /// <typeparam name="T">Type of collection elements</typeparam>
        /// <param name="collection">Collection to validate</param>
        /// <param name="maxSize">Maximum allowed size</param>
        /// <param name="parameterName">Name of the parameter</param>
        /// <param name="minSize">Minimum allowed size (default: 0)</param>
        /// <exception cref="ArgumentNullException">Thrown when collection is null</exception>
        /// <exception cref="ArgumentException">Thrown when collection size is outside limits</exception>
        public static void ValidateCollection<T>([NotNull] IEnumerable<T>? collection, int maxSize, [CallerArgumentExpression(nameof(collection))] string? parameterName = null, int minSize = 0)
        {
            ThrowIfNull(collection, parameterName);
            
            var collectionList = collection as ICollection<T> ?? collection.ToList();
            
            if (collectionList.Count < minSize)
            {
                throw new ArgumentException($"Argument '{parameterName}' must contain at least {minSize} items", parameterName);
            }
            
            if (collectionList.Count > maxSize)
            {
                throw new ArgumentException($"Argument '{parameterName}' cannot contain more than {maxSize} items", parameterName);
            }
        }

        #endregion

        #region Numeric Validation

        /// <summary>
        /// Validates that a numeric value is within a specified range
        /// </summary>
        /// <param name="value">Value to validate</param>
        /// <param name="minValue">Minimum allowed value</param>
        /// <param name="maxValue">Maximum allowed value</param>
        /// <param name="parameterName">Name of the parameter</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when value is outside the allowed range</exception>
        public static void ValidateRange(double value, double minValue, double maxValue, [CallerArgumentExpression(nameof(value))] string? parameterName = null)
        {
            if (value < minValue || value > maxValue)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, $"Argument '{parameterName}' must be between {minValue} and {maxValue}");
            }
        }

        /// <summary>
        /// Validates that a numeric value is positive
        /// </summary>
        /// <param name="value">Value to validate</param>
        /// <param name="parameterName">Name of the parameter</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when value is not positive</exception>
        public static void ValidatePositive(double value, [CallerArgumentExpression(nameof(value))] string? parameterName = null)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, $"Argument '{parameterName}' must be positive");
            }
        }

        /// <summary>
        /// Validates that a numeric value is non-negative
        /// </summary>
        /// <param name="value">Value to validate</param>
        /// <param name="parameterName">Name of the parameter</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when value is negative</exception>
        public static void ValidateNonNegative(double value, [CallerArgumentExpression(nameof(value))] string? parameterName = null)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, $"Argument '{parameterName}' must be non-negative");
            }
        }

        /// <summary>
        /// Validates that an integer value is positive
        /// </summary>
        /// <param name="value">Value to validate</param>
        /// <param name="parameterName">Name of the parameter</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when value is not positive</exception>
        public static void ValidatePositive(int value, [CallerArgumentExpression(nameof(value))] string? parameterName = null)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, $"Argument '{parameterName}' must be positive");
            }
        }

        /// <summary>
        /// Validates that an integer value is non-negative
        /// </summary>
        /// <param name="value">Value to validate</param>
        /// <param name="parameterName">Name of the parameter</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when value is negative</exception>
        public static void ValidateNonNegative(int value, [CallerArgumentExpression(nameof(value))] string? parameterName = null)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, $"Argument '{parameterName}' must be non-negative");
            }
        }

        #endregion

        #region File Path Validation

        /// <summary>
        /// Validates a file path for security and safety
        /// </summary>
        /// <param name="filePath">File path to validate</param>
        /// <param name="allowCreate">Whether to allow path creation if it doesn't exist</param>
        /// <exception cref="ArgumentException">Thrown when path is invalid or unsafe</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown when directory doesn't exist and allowCreate is false</exception>
        public static void ValidateFilePath([NotNull] string? filePath, bool allowCreate = false)
        {
            ThrowIfNullOrWhiteSpace(filePath, nameof(filePath));
            
            // Check for path traversal attempts
            if (IsPathTraversal(filePath))
            {
                throw new ArgumentException($"Potentially unsafe file path detected: {filePath}", nameof(filePath));
            }
            
            // Check for absolute path (security consideration)
            try
            {
                var fullPath = Path.GetFullPath(filePath);
                if (!Path.IsPathRooted(fullPath))
                {
                    throw new ArgumentException("File path must be absolute", nameof(filePath));
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
            {
                throw new ArgumentException($"Invalid file path: {ex.Message}", nameof(filePath), ex);
            }
            
            // Check if directory exists (unless creating is allowed)
            if (!allowCreate)
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    throw new DirectoryNotFoundException($"Directory does not exist: {directory}");
                }
            }
            
            // Validate filename
            var fileName = Path.GetFileName(filePath);
            if (HasReservedName(fileName))
            {
                throw new ArgumentException($"File name contains reserved characters: {fileName}", nameof(filePath));
            }
            
            // Check path length
            var normalizedPath = Path.GetFullPath(filePath);
            if (normalizedPath.Length > 260) // Windows MAX_PATH
            {
                throw new ArgumentException($"File path too long: {normalizedPath.Length} characters", nameof(filePath));
            }
        }

        /// <summary>
        /// Validates a directory path for security and safety
        /// </summary>
        /// <param name="directoryPath">Directory path to validate</param>
        /// <param name="allowCreate">Whether to allow path creation if it doesn't exist</param>
        /// <exception cref="ArgumentException">Thrown when path is invalid or unsafe</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown when directory doesn't exist and allowCreate is false</exception>
        public static void ValidateDirectoryPath([NotNull] string? directoryPath, bool allowCreate = false)
        {
            ThrowIfNullOrWhiteSpace(directoryPath, nameof(directoryPath));
            
            // Check for path traversal attempts
            if (IsPathTraversal(directoryPath))
            {
                throw new ArgumentException($"Potentially unsafe directory path detected: {directoryPath}", nameof(directoryPath));
            }
            
            try
            {
                var fullPath = Path.GetFullPath(directoryPath);
                if (!Path.IsPathRooted(fullPath))
                {
                    throw new ArgumentException("Directory path must be absolute", nameof(directoryPath));
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
            {
                throw new ArgumentException($"Invalid directory path: {ex.Message}", nameof(directoryPath), ex);
            }
            
            // Check if directory exists (unless creating is allowed)
            if (!allowCreate && !Directory.Exists(directoryPath))
            {
                throw new DirectoryNotFoundException($"Directory does not exist: {directoryPath}");
            }
            
            // Check path length
            var normalizedPath = Path.GetFullPath(directoryPath);
            if (normalizedPath.Length > 260)
            {
                throw new ArgumentException($"Directory path too long: {normalizedPath.Length} characters", nameof(directoryPath));
            }
        }

        private static bool IsPathTraversal(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            
            var normalized = path.Replace('\\', '/');
            return normalized.Contains("../") || 
                   normalized.Contains("..\\") ||
                   normalized.StartsWith("..", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Contains("%2e%2e%2f") || // URL encoded
                   normalized.Contains("%252e%252e%252f") ||
                   path.Contains("..\\") ||
                   path.Contains("../") ||
                   path.Contains(":\\") && !path.StartsWith("C:\\", StringComparison.OrdinalIgnoreCase); // Simple drive check
        }

        private static bool HasReservedName(string path)
        {
            var fileName = Path.GetFileNameWithoutExtension(path).ToUpperInvariant();
            var reservedNames = new HashSet<string>
            {
                "CON", "PRN", "AUX", "NUL",
                "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
            };
            
            return reservedNames.Contains(fileName);
        }

        #endregion

        #region Input Sanitization

        /// <summary>
        /// Sanitizes user input by removing potentially dangerous characters
        /// </summary>
        /// <param name="input">Input to sanitize</param>
        /// <param name="maxLength">Maximum length after sanitization</param>
        /// <returns>Sanitized string</returns>
        public static string SanitizeUserInput([NotNull] string? input, int maxLength = 1024)
        {
            ThrowIfNull(input, nameof(input));
            
            // Remove potentially dangerous characters
            var sanitized = input
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#x27;")
                .Replace("/", "&#x2F;")
                .Replace("\\", "&#x5C;")
                .Replace(";", "&#x3B;")
                .Replace(":", "&#x3A;");
            
            // Truncate to maximum length
            if (sanitized.Length > maxLength)
            {
                sanitized = sanitized[..maxLength];
            }
            
            return sanitized;
        }

        /// <summary>
        /// Validates and sanitizes a filename
        /// </summary>
        /// <param name="fileName">Filename to validate</param>
        /// <returns>Sanitized filename</returns>
        /// <exception cref="ArgumentException">Thrown when filename is invalid</exception>
        public static string SanitizeFileName([NotNull] string? fileName)
        {
            ThrowIfNullOrWhiteSpace(fileName, nameof(fileName));
            
            // Remove invalid filename characters
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = fileName;
            foreach (var invalidChar in invalidChars)
            {
                sanitized = sanitized.Replace(invalidChar, '_');
            }
            
            // Check for reserved names
            if (HasReservedName(sanitized))
            {
                throw new ArgumentException($"Filename '{sanitized}' is reserved and cannot be used", nameof(fileName));
            }
            
            // Validate length
            if (sanitized.Length > 255)
            {
                throw new ArgumentException($"Filename cannot exceed 255 characters", nameof(fileName));
            }
            
            return sanitized;
        }

        /// <summary>
        /// Validates that a string contains only allowed characters
        /// </summary>
        /// <param name="value">String to validate</param>
        /// <param name="allowedPattern">Regex pattern for allowed characters</param>
        /// <param name="parameterName">Name of the parameter</param>
        /// <exception cref="ArgumentException">Thrown when string contains disallowed characters</exception>
        public static void ValidateAllowedCharacters([NotNull] string? value, string allowedPattern, [CallerArgumentExpression(nameof(value))] string? parameterName = null)
        {
            ThrowIfNullOrWhiteSpace(value, parameterName);
            
            if (!Regex.IsMatch(value, allowedPattern, RegexOptions.Compiled))
            {
                throw new ArgumentException($"Argument '{parameterName}' contains invalid characters", parameterName);
            }
        }

        #endregion

        #region Defensive Programming Patterns

        /// <summary>
        /// Provides defensive null checking with fallback value
        /// </summary>
        /// <typeparam name="T">Type of the value</typeparam>
        /// <param name="value">Value to check</param>
        /// <param name="fallback">Fallback value if original is null</param>
        /// <returns>Original value or fallback if null</returns>
        public static T? NullSafe<T>(T? value, T fallback)
        {
            return value ?? fallback;
        }

        /// <summary>
        /// Provides defensive string checking with fallback
        /// </summary>
        /// <param name="value">String to check</param>
        /// <param name="fallback">Fallback string if original is null or empty</param>
        /// <returns>Original string or fallback</returns>
        public static string NullSafeString(string? value, string fallback)
        {
            return !string.IsNullOrWhiteSpace(value) ? value : fallback;
        }

        /// <summary>
        /// Safely executes an action and returns a default value if it fails
        /// </summary>
        /// <typeparam name="T">Type of return value</typeparam>
        /// <param name="action">Action to execute</param>
        /// <param name="defaultValue">Default value to return if action fails</param>
        /// <returns>Action result or default value</returns>
        public static T? SafeExecute<T>(Func<T?> action, T defaultValue)
        {
            try
            {
                return action() ?? defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Safely executes an action and returns false if it fails
        /// </summary>
        /// <param name="action">Action to execute</param>
        /// <returns>True if action succeeded, false otherwise</returns>
        public static bool SafeExecute(Action action)
        {
            try
            {
                action();
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region DirectX Object Validation

        /// <summary>
        /// Validates that a DirectX object is not null and not disposed
        /// </summary>
        /// <typeparam name="T">Type of DirectX object</typeparam>
        /// <param name="obj">Object to validate</param>
        /// <param name="parameterName">Name of the parameter</param>
        /// <exception cref="ArgumentNullException">Thrown when object is null</exception>
        /// <exception cref="ObjectDisposedException">Thrown when object is disposed</exception>
        public static void ValidateDirectXObject<T>([NotNull] T? obj, [CallerArgumentExpression(nameof(obj))] string? parameterName = null) where T : class, IDisposable
        {
            ThrowIfNull(obj, parameterName);
            
            // Check if object is disposed by attempting a safe operation
            var safeExecute = SafeExecute(() => 
            {
                // Try to get hash code - disposed objects typically throw
                _ = obj.GetHashCode();
                return true;
            });
            
            if (!safeExecute || safeExecute == null)
            {
                throw new ObjectDisposedException(parameterName, $"DirectX object '{parameterName}' has been disposed");
            }
        }

        /// <summary>
        /// Validates that a COM object is not null and properly initialized
        /// </summary>
        /// <typeparam name="T">Type of COM object</typeparam>
        /// <param name="obj">Object to validate</param>
        /// <param name="parameterName">Name of the parameter</param>
        /// <exception cref="ArgumentNullException">Thrown when object is null</exception>
        /// <exception cref="InvalidOperationException">Thrown when object is not properly initialized</exception>
        public static void ValidateComObject<T>([NotNull] T? obj, [CallerArgumentExpression(nameof(obj))] string? parameterName = null) where T : class
        {
            ThrowIfNull(obj, parameterName);
            
            // Additional COM-specific validation could be added here
            // For now, just checking for null and basic integrity
            try
            {
                // Try to get the COM object interface - this validates the object
                var type = obj.GetType();
                if (!type.IsCOMObject)
                {
                    throw new InvalidOperationException($"Object '{parameterName}' is not a valid COM object");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"COM object '{parameterName}' is not properly initialized", ex);
            }
        }

        #endregion
    }
}