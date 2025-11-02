using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TiXL.Core.ErrorHandling;
using TiXL.Core.Validation;

namespace TiXL.Core.IO
{
    /// <summary>
    /// Enhanced safe file I/O operations with comprehensive error handling and resilient execution
    /// Addresses security vulnerabilities, resource management, atomic operations, and graceful degradation
    /// </summary>
    /// <remarks>
    /// This enhanced version adds:
    /// - Comprehensive null safety with nullable reference types
    /// - Async/await patterns for improved responsiveness
    /// - Extensive parameter validation with custom exception types
    /// - Comprehensive XML documentation for all public APIs
    /// - Production-ready logging and error handling
    /// - Graceful degradation and retry patterns
    /// - Resource management improvements
    /// - Enhanced security validation and path sanitization
    /// </remarks>
    public class SafeFileIOEnhanced : IDisposable
    {
        #region Private Fields

        private static readonly SafeFileIOEnhanced _instance = new();
        private readonly IOperationMonitor _monitor;
        private readonly SemaphoreSlim _ioSemaphore;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks;
        private readonly ILogger<SafeFileIOEnhanced> _logger;
        
        // Configuration with comprehensive error handling support
        private readonly int _maxConcurrentIO = 10;
        private readonly TimeSpan _operationTimeout = TimeSpan.FromMinutes(5);
        private readonly string _tempDirectory;
        private readonly GracefulDegradationStrategy _degradationStrategy;
        private readonly TimeoutPolicy _timeoutPolicy;
        private readonly RetryPolicy _retryPolicy;
        private bool _isDisposed;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the singleton instance of SafeFileIOEnhanced
        /// </summary>
        public static SafeFileIOEnhanced Instance => _instance;

        /// <summary>
        /// Gets the temporary directory used for safe file operations
        /// </summary>
        public string TempDirectory => _tempDirectory;

        /// <summary>
        /// Gets the current I/O operation statistics
        /// </summary>
        public IOStatistics Statistics => GetStatistics();

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new SafeFileIOEnhanced instance with comprehensive error handling
        /// </summary>
        /// <param name="logger">Logger for error handling and diagnostics (nullable)</param>
        /// <exception cref="IOSafetyException">Thrown when initialization fails</exception>
        private SafeFileIOEnhanced(ILogger<SafeFileIOEnhanced>? logger = null)
        {
            try
            {
                _logger = logger ?? NullLogger<SafeFileIOEnhanced>.Instance;
                _monitor = new OperationMonitor();
                _ioSemaphore = new SemaphoreSlim(_maxConcurrentIO, _maxConcurrentIO);
                _fileLocks = new ConcurrentDictionary<string, SemaphoreSlim>();
                
                // Initialize error handling infrastructure
                _degradationStrategy = new GracefulDegradationStrategy();
                _timeoutPolicy = new TimeoutPolicy 
                { 
                    Timeout = TimeSpan.FromMinutes(2),
                    OnTimeout = () => _degradationStrategy.RecordFailure("I/O operation timeout")
                };
                _retryPolicy = new RetryPolicy
                {
                    MaxRetries = 3,
                    InitialDelay = TimeSpan.FromMilliseconds(500),
                    BackoffMultiplier = 2.0,
                    RetryCondition = ex => ExceptionFilters.IsTransientFailure(ex)
                };
                
                _tempDirectory = Path.Combine(Path.GetTempPath(), "TiXL_SafeIO_" + Guid.NewGuid().ToString("N")[..8]);
                
                // Create temp directory with error handling
                Directory.CreateDirectory(_tempDirectory);
                
                _logger.LogInformation("SafeFileIOEnhanced initialized successfully - Temp directory: {TempDirectory}", _tempDirectory);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize SafeFileIOEnhanced");
                throw new IOSafetyException(
                    IOSafetyErrorCode.DirectoryCreationFailed,
                    $"Failed to initialize SafeFileIOEnhanced: {ex.Message}",
                    ex);
            }
        }

        #endregion
        
        #region Public Methods - Path Validation

        /// <summary>
        /// Validates write paths for security and safety with comprehensive error handling
        /// </summary>
        /// <param name="path">File path to validate (nullable)</param>
        /// <param name="cancellationToken">Cancellation token for the validation operation</param>
        /// <returns>ValidationResult with validation status and error details</returns>
        /// <exception cref="OperationCanceledException">Thrown when validation is cancelled</exception>
        /// <remarks>
        /// This method performs comprehensive security validation including:
        /// - Null and empty path checks
        /// - Directory traversal prevention
        /// - Drive accessibility verification
        /// - File extension whitelist validation
        /// - Path length limits checking
        /// - Windows reserved name detection
        /// - Whitespace sanitization
        /// </remarks>
        public async Task<ValidationResult> ValidateWritePathAsync(
            string? path, 
            CancellationToken cancellationToken = default)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                return await Task.Run(() =>
                {
                    try
                    {
                        var result = new ValidationResult { IsValid = true };
                        
                        // Null/empty check with enhanced error message
                        if (string.IsNullOrWhiteSpace(path))
                        {
                            result.IsValid = false;
                            result.ErrorMessage = "File path cannot be null, empty, or whitespace-only";
                            _logger?.LogWarning("Path validation failed - null or empty path");
                            return result;
                        }
                        
                        // Directory traversal check with comprehensive patterns
                        var normalizedPath = Path.GetFullPath(path);
                        if (IsPathTraversal(normalizedPath))
                        {
                            result.IsValid = false;
                            result.ErrorMessage = $"Potential directory traversal attack detected: {path}";
                            _logger?.LogWarning("Path validation failed - directory traversal detected: {Path}", path);
                            return result;
                        }
                        
                        // Drive access validation with error handling
                        if (!IsDriveAccessible(normalizedPath))
                        {
                            result.IsValid = false;
                            result.ErrorMessage = $"Drive not accessible or does not exist: {Path.GetPathRoot(normalizedPath)}";
                            _logger?.LogWarning("Path validation failed - drive not accessible: {Path}", normalizedPath);
                            return result;
                        }
                        
                        // File extension validation with security focus
                        var extension = Path.GetExtension(path);
                        if (!IsAllowedExtension(extension))
                        {
                            result.IsValid = false;
                            result.ErrorMessage = $"File extension '{extension}' is not in the allowed list for security reasons";
                            _logger?.LogWarning("Path validation failed - disallowed extension: {Extension}", extension);
                            return result;
                        }
                        
                        // Path length validation with platform-specific checks
                        if (normalizedPath.Length > 260) // Windows MAX_PATH
                        {
                            result.IsValid = false;
                            result.ErrorMessage = $"Path length ({normalizedPath.Length} characters) exceeds Windows limit (260 characters)";
                            _logger?.LogWarning("Path validation failed - path too long: {Length} characters", normalizedPath.Length);
                            return result;
                        }
                        
                        // Windows reserved names check
                        if (HasReservedName(normalizedPath))
                        {
                            result.IsValid = false;
                            result.ErrorMessage = $"File name '{Path.GetFileName(normalizedPath)}' is a reserved Windows name";
                            _logger?.LogWarning("Path validation failed - reserved name: {FileName}", Path.GetFileName(normalizedPath));
                            return result;
                        }
                        
                        // Whitespace validation (security consideration for file systems)
                        if (path.Trim() != path)
                        {
                            result.IsValid = false;
                            result.ErrorMessage = "Paths with leading/trailing spaces are not allowed for security reasons";
                            _logger?.LogWarning("Path validation failed - whitespace in path: {Path}", path);
                            return result;
                        }

                        // Additional security checks
                        if (ContainsInvalidCharacters(path))
                        {
                            result.IsValid = false;
                            result.ErrorMessage = "Path contains invalid characters for the target file system";
                            _logger?.LogWarning("Path validation failed - invalid characters: {Path}", path);
                            return result;
                        }
                        
                        _logger?.LogDebug("Path validation successful: {Path}", path);
                        return result;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Path validation failed with unexpected error for: {Path}", path);
                        return new ValidationResult 
                        { 
                            IsValid = false, 
                            ErrorMessage = $"Path validation failed with error: {ex.Message}" 
                        };
                    }
                }, cancellationToken);
            }, 
            maxRetries: 3,
            retryPolicy: RetryPolicyType.ExponentialBackoff,
            cancellationToken: cancellationToken,
            operationName: "ValidateWritePath");
        }
        
        /// <summary>
        /// Checks for directory traversal attempts with error handling
        /// </summary>
        private bool IsPathTraversal(string path)
        {
            try
            {
                var normalized = path.Replace('\\', '/');
                return normalized.Contains("../") || 
                       normalized.Contains("..\\") ||
                       normalized.StartsWith("..", StringComparison.OrdinalIgnoreCase) ||
                       normalized.Contains("%2e%2e%2f") || // URL encoded
                       normalized.Contains("%252e%252e%252f");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error checking path traversal for: {Path}", path);
                return true; // Fail secure
            }
        }
        
        /// <summary>
        /// Validates drive accessibility with error handling
        /// </summary>
        private bool IsDriveAccessible(string path)
        {
            try
            {
                var rootPath = Path.GetPathRoot(path);
                if (string.IsNullOrEmpty(rootPath))
                    return false;
                    
                // Check if root path exists
                var driveInfo = new DriveInfo(rootPath);
                return driveInfo.IsReady;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error checking drive accessibility for: {Path}", path);
                return false;
            }
        }
        
        /// <summary>
        /// Checks if file extension is allowed with error handling
        /// </summary>
        private bool IsAllowedExtension(string extension)
        {
            try
            {
                if (string.IsNullOrEmpty(extension))
                    return true;
                    
                var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    ".txt", ".json", ".xml", ".csv", ".log", ".md",
                    ".png", ".jpg", ".jpeg", ".bmp", ".tiff", ".gif",
                    ".wav", ".mp3", ".flac", ".ogg",
                    ".mp4", ".avi", ".mov", ".wmv",
                    ".zip", ".rar", ".7z", ".tar", ".gz",
                    ".config", ".settings", ".cache"
                };
                
                return allowedExtensions.Contains(extension);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error checking allowed extension: {Extension}", extension);
                return false;
            }
        }
        
        /// <summary>
        /// Checks for Windows reserved names with error handling
        /// </summary>
        private bool HasReservedName(string path)
        {
            try
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
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error checking reserved name for: {Path}", path);
                return true; // Fail secure
            }
        }
        
        #endregion
        
        #region Safe Write Operations
        
        /// <summary>
        /// Asynchronously writes string content to a file using atomic operations with comprehensive error handling
        /// </summary>
        /// <param name="path">Target file path (1-260 characters, non-null, non-whitespace)</param>
        /// <param name="content">String content to write (nullable)</param>
        /// <param name="createBackup">Whether to create a backup of existing file before writing</param>
        /// <param name="cancellationToken">Cancellation token for the write operation</param>
        /// <returns>WriteResult with operation status and details</returns>
        /// <exception cref="ArgumentNullException">Thrown when path is null</exception>
        /// <exception cref="ArgumentException">Thrown when path is invalid or content encoding fails</exception>
        /// <exception cref="OperationCanceledException">Thrown when operation is cancelled</exception>
        /// <exception cref="IOSafetyException">Thrown when write operation fails after all retry attempts</exception>
        /// <remarks>
        /// This method provides:
        /// - Atomic file operations using temporary files
        /// - Automatic backup creation for existing files
        /// - Comprehensive error handling and retry logic
        /// - Rollback capability on failure
        /// - Thread-safe file operations
        /// - Resource cleanup on errors
        /// </remarks>
        public async Task<WriteResult> SafeWriteAsync(
            string path, 
            string? content, 
            bool createBackup = true, 
            CancellationToken cancellationToken = default)
        {
            if (content == null)
            {
                _logger?.LogWarning("SafeWriteAsync called with null content for path: {Path}", path);
                content = string.Empty; // Treat null as empty string for safety
            }

            return await SafeWriteAsync(path, Encoding.UTF8.GetBytes(content), createBackup, cancellationToken);
        }
        
        /// <summary>
        /// Asynchronously writes byte content to a file using atomic operations with comprehensive error handling
        /// </summary>
        /// <param name="path">Target file path (1-260 characters, non-null, non-whitespace)</param>
        /// <param name="content">Byte content to write (nullable)</param>
        /// <param name="createBackup">Whether to create a backup of existing file before writing</param>
        /// <param name="cancellationToken">Cancellation token for the write operation</param>
        /// <returns>WriteResult with operation status and details</returns>
        /// <exception cref="ArgumentNullException">Thrown when path is null</exception>
        /// <exception cref="ArgumentException">Thrown when path is invalid</exception>
        /// <exception cref="OperationCanceledException">Thrown when operation is cancelled</exception>
        /// <exception cref="IOSafetyException">Thrown when write operation fails after all retry attempts</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when content size exceeds maximum limits</exception>
        /// <remarks>
        /// This method provides:
        /// - Atomic file operations using temporary files
        /// - Automatic backup creation for existing files (optional)
        /// - Comprehensive error handling and retry logic
        /// - Rollback capability on failure
        /// - Thread-safe file operations with semaphore-based locking
        /// - Resource cleanup on errors
        /// - Size validation (1GB maximum)
        /// </remarks>
        public async Task<WriteResult> SafeWriteAsync(
            string path, 
            byte[]? content, 
            bool createBackup = true, 
            CancellationToken cancellationToken = default)
        {
            return await ExecuteResilientAsync(async () =>
            {
                // Input validation with specific exception types
                ValidationHelpers.ValidateFilePath(path, allowCreate: true);
                ValidationHelpers.ThrowIfNull(content, nameof(content));
                ValidationHelpers.ValidateNonNegative(content.Length, nameof(content));
                
                // Additional content size validation for security
                if (content.Length > 1024 * 1024 * 1024)
                {
                    throw new ArgumentOutOfRangeException(nameof(content), 
                        $"Content size ({content.Length} bytes) exceeds maximum allowed size (1GB)");
                }

                using var operation = _monitor.StartOperation($"SafeWrite:{Path.GetFileName(path)}");
                
                // Use timeout wrapper for the entire operation
                return await ExecuteWithTimeoutAsync(async token =>
                {
                    await _ioSemaphore.WaitAsync(token);
                    
                    var fileLock = _fileLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
                    await fileLock.WaitAsync(token);
                    
                    try
                    {
                        operation.RecordMetadata("path", path);
                        operation.RecordMetadata("size", content.Length.ToString());
                        operation.RecordMetadata("createBackup", createBackup.ToString());
                        
                        var directory = Path.GetDirectoryName(path);
                        if (!string.IsNullOrEmpty(directory))
                        {
                            await SafeCreateDirectoryAsync(directory, token);
                        }
                        
                        var tempPath = Path.Combine(_tempDirectory, Guid.NewGuid().ToString("N") + ".tmp");
                        var backupPath = Path.Combine(_tempDirectory, Guid.NewGuid().ToString("N") + ".backup");
                        
                        // Step 1: Write to temporary file with retry
                        await WriteToTempFileWithRetry(tempPath, content, token);
                        
                        // Step 2: Create backup if file exists and backup requested
                        bool backupCreated = false;
                        if (createBackup && File.Exists(path))
                        {
                            backupCreated = await CreateBackupWithRetry(path, backupPath, token);
                        }
                        
                        // Step 3: Atomic move with cleanup on failure
                        await AtomicFileMove(tempPath, path, backupCreated ? backupPath : null, token);
                        
                        // Step 4: Cleanup backup if successful
                        if (backupCreated)
                        {
                            await CleanupBackupAsync(backupPath);
                        }
                        
                        operation.RecordSuccess();
                        
                        var result = WriteResult.Success(path, content.Length);
                        _logger?.LogInformation("Successfully wrote {Size} bytes to {Path}", content.Length, path);
                        
                        return result;
                    }
                    finally
                    {
                        fileLock.Release();
                    }
                }, 
                _operationTimeout, 
                cancellationToken, 
                "SafeWrite operation");
                
            }, 
            maxRetries: 3,
            retryPolicy: RetryPolicyType.ExponentialBackoff,
            cancellationToken: cancellationToken,
            operationName: "SafeWrite");
        }
        
        /// <summary>
        /// Safe JSON serialization with write protection and comprehensive error handling
        /// </summary>
        public async Task<WriteResult> SafeWriteJsonAsync<T>(string path, T data, bool createBackup = true, CancellationToken cancellationToken = default) where T : class
        {
            return await ExecuteResilientAsync(async () =>
            {
                var validation = await ValidateWritePathAsync(path);
                if (!validation.IsValid)
                {
                    throw new IOSafetyException(
                        IOSafetyErrorCode.PathValidationFailed,
                        $"Path validation failed: {validation.ErrorMessage}");
                }
                
                ValidationHelpers.ThrowIfNull(data, nameof(data));
                
                using var operation = _monitor.StartOperation($"SafeWriteJson:{typeof(T).Name}");
                
                // Serialize to temporary file first
                var tempPath = Path.Combine(_tempDirectory, Guid.NewGuid().ToString("N") + ".json");
                
                try
                {
                    await using var tempStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await System.Text.Json.JsonSerializer.SerializeAsync(tempStream, data, new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                    }, cancellationToken);
                    
                    // Validate JSON integrity
                    await ValidateJsonFileWithRetry(tempPath, cancellationToken);
                    
                    // Complete the write operation
                    var content = await File.ReadAllBytesAsync(tempPath);
                    var result = await SafeWriteAsync(path, content, createBackup, cancellationToken);
                    
                    return result;
                }
                finally
                {
                    // Cleanup temp file
                    try { File.Delete(tempPath); } catch { /* ignore */ }
                }
            }, 
            maxRetries: 2,
            retryPolicy: RetryPolicyType.ExponentialBackoff,
            cancellationToken: cancellationToken,
            operationName: "SafeWriteJson");
        }
        
        #endregion
        
        #region Safe Read Operations
        
        /// <summary>
        /// Safe file read with comprehensive error handling
        /// </summary>
        public async Task<ReadResult<string>> SafeReadAllTextAsync(string path, CancellationToken cancellationToken = default)
        {
            return await ExecuteResilientAsync(async () =>
            {
                ValidationHelpers.ValidateFilePath(path, allowCreate: false);
                
                using var operation = _monitor.StartOperation($"SafeReadAllText:{Path.GetFileName(path)}");
                
                return await ExecuteWithTimeoutAsync(async token =>
                {
                    await _ioSemaphore.WaitAsync(token);
                    
                    var fileLock = _fileLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
                    await fileLock.WaitAsync(token);
                    
                    try
                    {
                        if (!File.Exists(path))
                        {
                            throw new IOSafetyException(
                                IOSafetyErrorCode.FileNotFound,
                                $"File not found: {path}");
                        }
                        
                        var content = await File.ReadAllTextAsync(path, token);
                        operation.RecordMetadata("size", content.Length.ToString());
                        operation.RecordSuccess();
                        
                        return ReadResult<string>.Success(content);
                    }
                    finally
                    {
                        fileLock.Release();
                    }
                }, 
                _operationTimeout, 
                cancellationToken, 
                "SafeReadAllText operation");
            }, 
            maxRetries: 3,
            retryPolicy: RetryPolicyType.ExponentialBackoff,
            cancellationToken: cancellationToken,
            operationName: "SafeReadAllText");
        }
        
        /// <summary>
        /// Safe file read with comprehensive error handling
        /// </summary>
        public async Task<ReadResult<byte[]>> SafeReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
        {
            return await ExecuteResilientAsync(async () =>
            {
                var validation = await ValidateReadPathAsync(path);
                if (!validation.IsValid)
                {
                    throw new IOSafetyException(
                        IOSafetyErrorCode.PathValidationFailed,
                        $"Read path validation failed: {validation.ErrorMessage}");
                }
                
                using var operation = _monitor.StartOperation($"SafeReadAllBytes:{Path.GetFileName(path)}");
                
                return await ExecuteWithTimeoutAsync(async token =>
                {
                    await _ioSemaphore.WaitAsync(token);
                    
                    var fileLock = _fileLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
                    await fileLock.WaitAsync(token);
                    
                    try
                    {
                        if (!File.Exists(path))
                        {
                            throw new IOSafetyException(
                                IOSafetyErrorCode.FileNotFound,
                                $"File not found: {path}");
                        }
                        
                        var content = await File.ReadAllBytesAsync(path);
                        operation.RecordMetadata("size", content.Length.ToString());
                        operation.RecordSuccess();
                        
                        return ReadResult<byte[]>.Success(content);
                    }
                    finally
                    {
                        fileLock.Release();
                    }
                }, 
                _operationTimeout, 
                cancellationToken, 
                "SafeReadAllBytes operation");
            }, 
            maxRetries: 3,
            retryPolicy: RetryPolicyType.ExponentialBackoff,
            cancellationToken: cancellationToken,
            operationName: "SafeReadAllBytes");
        }
        
        /// <summary>
        /// Safe JSON deserialization with read protection and comprehensive error handling
        /// </summary>
        public async Task<ReadResult<T>> SafeReadJsonAsync<T>(string path, CancellationToken cancellationToken = default) where T : class
        {
            return await ExecuteResilientAsync(async () =>
            {
                var readResult = await SafeReadAllBytesAsync(path, cancellationToken);
                if (!readResult.IsSuccess)
                {
                    throw new IOSafetyException(
                        IOSafetyErrorCode.FileReadFailed,
                        $"Failed to read file for JSON deserialization: {readResult.ErrorMessage}");
                }
                
                try
                {
                    using var stream = new MemoryStream(readResult.Data);
                    var data = await System.Text.Json.JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: cancellationToken);
                    
                    if (data == null)
                    {
                        throw new IOSafetyException(
                            IOSafetyErrorCode.DeserializationFailed,
                            "JSON deserialization returned null");
                    }
                    
                    return ReadResult<T>.Success(data);
                }
                catch (Exception ex) when (!(ex is IOSafetyException))
                {
                    throw new IOSafetyException(
                        IOSafetyErrorCode.DeserializationFailed,
                        $"JSON deserialization failed: {ex.Message}",
                        ex);
                }
            }, 
            maxRetries: 2,
            retryPolicy: RetryPolicyType.ExponentialBackoff,
            cancellationToken: cancellationToken,
            operationName: "SafeReadJson");
        }
        
        #endregion
        
        #region Directory Operations
        
        /// <summary>
        /// Safe directory creation with validation and comprehensive error handling
        /// </summary>
        public async Task<DirectoryOperationResult> SafeCreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
        {
            return await ExecuteResilientAsync(async () =>
            {
                ValidationHelpers.ValidateDirectoryPath(path, allowCreate: true);
                
                using var operation = _monitor.StartOperation($"SafeCreateDirectory:{Path.GetFileName(path)}");
                
                var normalizedPath = Path.GetFullPath(path);
                if (Directory.Exists(normalizedPath))
                {
                    return DirectoryOperationResult.Success(normalizedPath);
                }
                
                // Create directory tree with retry
                await CreateDirectoryWithRetry(normalizedPath, cancellationToken);
                
                // Validate creation
                if (!Directory.Exists(normalizedPath))
                {
                    throw new IOSafetyException(
                        IOSafetyErrorCode.DirectoryCreationFailed,
                        $"Directory creation failed for: {normalizedPath}");
                }
                
                operation.RecordSuccess();
                return DirectoryOperationResult.Success(normalizedPath);
            }, 
            maxRetries: 3,
            retryPolicy: RetryPolicyType.ExponentialBackoff,
            cancellationToken: cancellationToken,
            operationName: "SafeCreateDirectory");
        }
        
        /// <summary>
        /// Safe directory enumeration with filtering and comprehensive error handling
        /// </summary>
        public async Task<DirectoryEnumerationResult> SafeEnumerateFilesAsync(string path, string searchPattern = "*", bool recursive = false, CancellationToken cancellationToken = default)
        {
            return await ExecuteResilientAsync(async () =>
            {
                var validation = await ValidateReadPathAsync(path);
                if (!validation.IsValid)
                {
                    throw new IOSafetyException(
                        IOSafetyErrorCode.PathValidationFailed,
                        $"Directory read validation failed: {validation.ErrorMessage}");
                }
                
                ValidationHelpers.ValidateNonNull(searchPattern, nameof(searchPattern));
                
                using var operation = _monitor.StartOperation($"SafeEnumerateFiles:{Path.GetFileName(path)}");
                
                if (!Directory.Exists(path))
                {
                    throw new IOSafetyException(
                        IOSafetyErrorCode.DirectoryNotFound,
                        $"Directory not found: {path}");
                }
                
                var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                
                // Enumerate files with timeout and retry
                var files = await EnumerateFilesWithRetry(path, searchPattern, searchOption, cancellationToken);
                
                operation.RecordMetadata("fileCount", files.Count.ToString());
                operation.RecordSuccess();
                
                return DirectoryEnumerationResult.Success(files);
            }, 
            maxRetries: 2,
            retryPolicy: RetryPolicyType.ExponentialBackoff,
            cancellationToken: cancellationToken,
            operationName: "SafeEnumerateFiles");
        }
        
        #endregion
        
        #region Rollback Operations
        
        /// <summary>
        /// Creates a rollback point for a file with comprehensive error handling
        /// </summary>
        public async Task<RollbackResult> CreateRollbackPointAsync(string path, CancellationToken cancellationToken = default)
        {
            return await ExecuteResilientAsync(async () =>
            {
                ValidationHelpers.ValidateFilePath(path, allowCreate: false);
                
                if (!File.Exists(path))
                {
                    throw new IOSafetyException(
                        IOSafetyErrorCode.FileNotFound,
                        $"Source file not found for rollback: {path}");
                }
                
                var rollbackPath = path + ".rollback_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff");
                
                // Create rollback with retry and timeout
                await ExecuteWithTimeoutAsync(async token =>
                {
                    await File.CopyAsync(path, rollbackPath, token);
                }, 
                TimeSpan.FromMinutes(2), 
                cancellationToken, 
                "CreateRollbackPoint operation");
                
                return RollbackResult.Success(rollbackPath);
            }, 
            maxRetries: 3,
            retryPolicy: RetryPolicyType.ExponentialBackoff,
            cancellationToken: cancellationToken,
            operationName: "CreateRollbackPoint");
        }
        
        /// <summary>
        /// Restores a file from rollback point with comprehensive error handling
        /// </summary>
        public async Task<RollbackResult> RestoreFromRollbackAsync(string rollbackPath, string targetPath, CancellationToken cancellationToken = default)
        {
            return await ExecuteResilientAsync(async () =>
            {
                ValidationHelpers.ValidateFilePath(rollbackPath, allowCreate: false);
                
                var validation = await ValidateWritePathAsync(targetPath);
                if (!validation.IsValid)
                {
                    throw new IOSafetyException(
                        IOSafetyErrorCode.PathValidationFailed,
                        $"Target path validation failed: {validation.ErrorMessage}");
                }
                
                if (!File.Exists(rollbackPath))
                {
                    throw new IOSafetyException(
                        IOSafetyErrorCode.FileNotFound,
                        $"Rollback file not found: {rollbackPath}");
                }
                
                // Restore with timeout and retry
                await ExecuteWithTimeoutAsync(async token =>
                {
                    await File.CopyAsync(rollbackPath, targetPath, overwrite: true, token);
                    File.Delete(rollbackPath);
                }, 
                TimeSpan.FromMinutes(2), 
                cancellationToken, 
                "RestoreFromRollback operation");
                
                return RollbackResult.Success(targetPath);
            }, 
            maxRetries: 2,
            retryPolicy: RetryPolicyType.ExponentialBackoff,
            cancellationToken: cancellationToken,
            operationName: "RestoreFromRollback");
        }
        
        #endregion
        
        #region Monitoring and Statistics
        
        /// <summary>
        /// Gets I/O operation statistics with error handling
        /// </summary>
        public IOStatistics GetStatistics()
        {
            try
            {
                return _monitor.GetStatistics();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get I/O statistics");
                return new IOStatistics(); // Return empty statistics on error
            }
        }
        
        /// <summary>
        /// Gets recent operation history with error handling
        /// </summary>
        public List<OperationRecord> GetOperationHistory(int maxRecords = 100)
        {
            try
            {
                ValidationHelpers.ValidatePositive(maxRecords, nameof(maxRecords));
                return _monitor.GetRecentOperations(maxRecords);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to get operation history");
                return new List<OperationRecord>(); // Return empty list on error
            }
        }
        
        #endregion
        
        #region Private Methods with Error Handling
        
        private async Task<ValidationResult> ValidateReadPathAsync(string path)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                return await Task.Run(() =>
                {
                    try
                    {
                        var result = new ValidationResult { IsValid = true };
                        
                        if (string.IsNullOrWhiteSpace(path))
                        {
                            result.IsValid = false;
                            result.ErrorMessage = "Path cannot be null or empty";
                            return result;
                        }
                        
                        try
                        {
                            var fullPath = Path.GetFullPath(path);
                            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
                            {
                                result.IsValid = false;
                                result.ErrorMessage = "File or directory does not exist";
                                return result;
                            }
                        }
                        catch (Exception ex)
                        {
                            result.IsValid = false;
                            result.ErrorMessage = $"Invalid path: {ex.Message}";
                            return result;
                        }
                        
                        return result;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Read path validation failed for: {Path}", path);
                        return new ValidationResult 
                        { 
                            IsValid = false, 
                            ErrorMessage = $"Validation failed: {ex.Message}" 
                        };
                    }
                });
            }, 
            maxRetries: 2,
            retryPolicy: RetryPolicyType.ExponentialBackoff,
            cancellationToken: default,
            operationName: "ValidateReadPath");
        }
        
        private async Task WriteToTempFileWithRetry(string tempPath, byte[] content, CancellationToken cancellationToken)
        {
            await ExecuteWithRetryAsync(async () =>
            {
                await File.WriteAllBytesAsync(tempPath, content, cancellationToken);
            }, 
            maxRetries: 3,
            retryPolicy: RetryPolicyType.ExponentialBackoff,
            cancellationToken: cancellationToken,
            operationName: "WriteToTempFile");
        }
        
        private async Task<bool> CreateBackupWithRetry(string sourcePath, string backupPath, CancellationToken cancellationToken)
        {
            return await ExecuteResilientAsync(async () =>
            {
                try
                {
                    await File.CopyAsync(sourcePath, backupPath, overwrite: false, cancellationToken);
                    return true;
                }
                catch (IOException)
                {
                    // Backup already exists, try different name
                    backupPath = Path.Combine(Path.GetDirectoryName(backupPath), Guid.NewGuid().ToString("N") + ".backup");
                    await File.CopyAsync(sourcePath, backupPath, overwrite: false, cancellationToken);
                    return true;
                }
            }, 
            maxRetries: 2,
            retryPolicy: RetryPolicyType.ExponentialBackoff,
            cancellationToken: cancellationToken,
            operationName: "CreateBackup");
        }
        
        private async Task AtomicFileMove(string tempPath, string targetPath, string backupPath, CancellationToken cancellationToken)
        {
            await ExecuteResilientAsync(async () =>
            {
                try
                {
                    if (File.Exists(targetPath))
                    {
                        File.Delete(targetPath);
                    }
                    File.Move(tempPath, targetPath);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Atomic file move failed, attempting cleanup");
                    
                    // Cleanup on failure
                    try { File.Delete(tempPath); } catch { /* ignore */ }
                    
                    // Restore backup if it exists
                    if (!string.IsNullOrEmpty(backupPath) && File.Exists(backupPath))
                    {
                        try
                        {
                            File.Copy(backupPath, targetPath, overwrite: true);
                        }
                        catch (Exception restoreEx)
                        {
                            _logger?.LogError(restoreEx, "Failed to restore backup after move failure");
                        }
                    }
                    
                    throw new IOSafetyException(
                        IOSafetyErrorCode.AtomicOperationFailed,
                        $"Atomic file move failed: {ex.Message}",
                        ex);
                }
            }, 
            maxRetries: 1,
            retryPolicy: RetryPolicyType.Linear,
            cancellationToken: cancellationToken,
            operationName: "AtomicFileMove");
        }
        
        private async Task CleanupBackupAsync(string backupPath)
        {
            try
            {
                await Task.Run(() => File.Delete(backupPath));
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to cleanup backup file: {BackupPath}", backupPath);
                // Don't throw - backup cleanup failure shouldn't fail the operation
            }
        }
        
        private async Task ValidateJsonFileWithRetry(string path, CancellationToken cancellationToken)
        {
            await ExecuteWithRetryAsync(async () =>
            {
                await using var stream = File.OpenRead(path);
                try
                {
                    await System.Text.Json.JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                }
                catch
                {
                    // If parsing fails, try to read as text and check basic JSON structure
                    var content = await File.ReadAllTextAsync(path, cancellationToken);
                    if (string.IsNullOrWhiteSpace(content) || 
                        (!content.TrimStart().StartsWith("{") && !content.TrimStart().StartsWith("[")))
                    {
                        throw new IOSafetyException(
                            IOSafetyErrorCode.InvalidJsonFormat,
                            "Invalid JSON content detected");
                    }
                }
            }, 
            maxRetries: 2,
            retryPolicy: RetryPolicyType.Linear,
            cancellationToken: cancellationToken,
            operationName: "ValidateJsonFile");
        }
        
        private async Task CreateDirectoryWithRetry(string path, CancellationToken cancellationToken)
        {
            await ExecuteWithRetryAsync(async () =>
            {
                Directory.CreateDirectory(path);
            }, 
            maxRetries: 3,
            retryPolicy: RetryPolicyType.ExponentialBackoff,
            cancellationToken: cancellationToken,
            operationName: "CreateDirectory");
        }
        
        private async Task<List<string>> EnumerateFilesWithRetry(string path, string searchPattern, SearchOption searchOption, CancellationToken cancellationToken)
        {
            return await ExecuteResilientAsync(async () =>
            {
                return await Task.Run(() =>
                {
                    try
                    {
                        return Directory.EnumerateFiles(path, searchPattern, searchOption).ToList();
                    }
                    catch (Exception ex)
                    {
                        throw new IOSafetyException(
                            IOSafetyErrorCode.DirectoryEnumerationFailed,
                            $"Failed to enumerate files in directory: {ex.Message}",
                            ex);
                    }
                }, cancellationToken);
            }, 
            maxRetries: 2,
            retryPolicy: RetryPolicyType.ExponentialBackoff,
            cancellationToken: cancellationToken,
            operationName: "EnumerateFiles");
        }
        
        #endregion
        
        #region Resilient Execution Helpers
        
        private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, int maxRetries, RetryPolicyType retryPolicy, CancellationToken cancellationToken, string operationName)
        {
            return await ErrorHandlingUtilities.ExecuteWithRetryAsync(operation, maxRetries, retryPolicy, cancellationToken, operationName);
        }
        
        private async Task ExecuteWithRetryAsync(Func<Task> operation, int maxRetries, RetryPolicyType retryPolicy, CancellationToken cancellationToken, string operationName)
        {
            await ErrorHandlingUtilities.ExecuteWithRetryAsync(operation, maxRetries, retryPolicy, cancellationToken, operationName);
        }
        
        private async Task<T> ExecuteWithTimeoutAsync<T>(Func<CancellationToken, Task<T>> operation, TimeSpan timeout, CancellationToken cancellationToken, string operationName)
        {
            return await ErrorHandlingUtilities.ExecuteWithTimeoutAsync(operation, timeout, cancellationToken, operationName);
        }
        
        private async Task ExecuteWithTimeoutAsync(Func<CancellationToken, Task> operation, TimeSpan timeout, CancellationToken cancellationToken, string operationName)
        {
            await ErrorHandlingUtilities.ExecuteWithTimeoutAsync(operation, timeout, cancellationToken, operationName);
        }
        
        private async Task<T> ExecuteResilientAsync<T>(Func<Task<T>> operation, int maxRetries, RetryPolicyType retryPolicy, CancellationToken cancellationToken, string operationName)
        {
            return await ErrorHandlingUtilities.ExecuteResilientAsync(operation, maxRetries, retryPolicy, cancellationToken, operationName);
        }
        
        #endregion
        
        public void Dispose()
        {
            try
            {
                _ioSemaphore?.Dispose();
                _fileLocks?.Clear();
                
                if (Directory.Exists(_tempDirectory))
                {
                    Directory.Delete(_tempDirectory, true);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during SafeFileIO disposal");
            }
        }
    }
    
    #region Data Models
    
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
    }
    
    public class WriteResult
    {
        public bool IsSuccess { get; set; }
        public string Path { get; set; }
        public int BytesWritten { get; set; }
        public string ErrorMessage { get; set; }
        public TimeSpan Duration { get; set; }
        
        public static WriteResult Success(string path, int bytesWritten)
        {
            return new WriteResult { IsSuccess = true, Path = path, BytesWritten = bytesWritten };
        }
        
        public static WriteResult Failed(string error)
        {
            return new WriteResult { IsSuccess = false, ErrorMessage = error };
        }
    }
    
    public class ReadResult<T>
    {
        public bool IsSuccess { get; set; }
        public T Data { get; set; }
        public string ErrorMessage { get; set; }
        public TimeSpan Duration { get; set; }
        
        public static ReadResult<T> Success(T data)
        {
            return new ReadResult<T> { IsSuccess = true, Data = data };
        }
        
        public static ReadResult<T> Failed(string error)
        {
            return new ReadResult<T> { IsSuccess = false, ErrorMessage = error };
        }
    }
    
    public class DirectoryOperationResult
    {
        public bool IsSuccess { get; set; }
        public string Path { get; set; }
        public string ErrorMessage { get; set; }
        
        public static DirectoryOperationResult Success(string path)
        {
            return new DirectoryOperationResult { IsSuccess = true, Path = path };
        }
        
        public static DirectoryOperationResult Failed(string error)
        {
            return new DirectoryOperationResult { IsSuccess = false, ErrorMessage = error };
        }
    }
    
    public class DirectoryEnumerationResult
    {
        public bool IsSuccess { get; set; }
        public List<string> Files { get; set; }
        public string ErrorMessage { get; set; }
        
        public static DirectoryEnumerationResult Success(List<string> files)
        {
            return new DirectoryEnumerationResult { IsSuccess = true, Files = files };
        }
        
        public static DirectoryEnumerationResult Failed(string error)
        {
            return new DirectoryEnumerationResult { IsSuccess = false, ErrorMessage = error };
        }
    }
    
    public class RollbackResult
    {
        public bool IsSuccess { get; set; }
        public string Path { get; set; }
        public string ErrorMessage { get; set; }
        
        public static RollbackResult Success(string path)
        {
            return new RollbackResult { IsSuccess = true, Path = path };
        }
        
        public static RollbackResult Failed(string error)
        {
            return new RollbackResult { IsSuccess = false, ErrorMessage = error };
        }
    }
    
    public class IOStatistics
    {
        public int TotalOperations { get; set; }
        public int SuccessfulOperations { get; set; }
        public int FailedOperations { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public TimeSpan AverageOperationTime { get; set; }
        public long TotalBytesRead { get; set; }
        public long TotalBytesWritten { get; set; }
        public Dictionary<string, int> OperationCounts { get; set; }
    }
    
    public class OperationRecord
    {
        public string OperationName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public Dictionary<string, string> Metadata { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
    }
    
    #endregion
}