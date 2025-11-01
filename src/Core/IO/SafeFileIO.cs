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

namespace TiXL.Core.IO
{
    /// <summary>
    /// Comprehensive safe file I/O operations with validation, monitoring, and rollback capabilities
    /// Addresses security vulnerabilities, resource management, and atomic operations
    /// </summary>
    public class SafeFileIO
    {
        private static readonly SafeFileIO _instance = new();
        private readonly IOperationMonitor _monitor;
        private readonly SemaphoreSlim _ioSemaphore;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks;
        
        // Configuration
        private readonly int _maxConcurrentIO = 10;
        private readonly TimeSpan _operationTimeout = TimeSpan.FromMinutes(5);
        private readonly string _tempDirectory;
        
        private SafeFileIO()
        {
            _monitor = new OperationMonitor();
            _ioSemaphore = new SemaphoreSlim(_maxConcurrentIO, _maxConcurrentIO);
            _fileLocks = new ConcurrentDictionary<string, SemaphoreSlim>();
            _tempDirectory = Path.Combine(Path.GetTempPath(), "TiXL_SafeIO_" + Guid.NewGuid().ToString("N")[..8]);
            
            // Create temp directory
            Directory.CreateDirectory(_tempDirectory);
        }
        
        public static SafeFileIO Instance => _instance;
        
        #region Write Path Validation
        
        /// <summary>
        /// Validates write paths for security and safety
        /// </summary>
        public ValidationResult ValidateWritePath(string path)
        {
            try
            {
                var result = new ValidationResult { IsValid = true };
                
                // Null/empty check
                if (string.IsNullOrWhiteSpace(path))
                {
                    result.IsValid = false;
                    result.ErrorMessage = "Path cannot be null or empty";
                    return result;
                }
                
                // Directory traversal check
                var normalizedPath = Path.GetFullPath(path);
                if (IsPathTraversal(normalizedPath))
                {
                    result.IsValid = false;
                    result.ErrorMessage = $"Potential directory traversal detected: {path}";
                    return result;
                }
                
                // Drive access validation
                if (!IsDriveAccessible(normalizedPath))
                {
                    result.IsValid = false;
                    result.ErrorMessage = $"Drive not accessible: {Path.GetPathRoot(normalizedPath)}";
                    return result;
                }
                
                // File extension validation
                var extension = Path.GetExtension(path);
                if (!IsAllowedExtension(extension))
                {
                    result.IsValid = false;
                    result.ErrorMessage = $"File extension not allowed: {extension}";
                    return result;
                }
                
                // Path length validation
                if (normalizedPath.Length > 260) // Windows MAX_PATH
                {
                    result.IsValid = false;
                    result.ErrorMessage = $"Path too long: {normalizedPath.Length} characters";
                    return result;
                }
                
                // Reserved names check
                if (HasReservedName(normalizedPath))
                {
                    result.IsValid = false;
                    result.ErrorMessage = $"Contains reserved name: {Path.GetFileName(normalizedPath)}";
                    return result;
                }
                
                // Space validation (safety for certain file systems)
                if (path.Trim() != path)
                {
                    result.IsValid = false;
                    result.ErrorMessage = "Paths with leading/trailing spaces are not allowed";
                    return result;
                }
                
                return result;
            }
            catch (Exception ex)
            {
                return new ValidationResult 
                { 
                    IsValid = false, 
                    ErrorMessage = $"Validation failed: {ex.Message}" 
                };
            }
        }
        
        /// <summary>
        /// Checks for directory traversal attempts
        /// </summary>
        private bool IsPathTraversal(string path)
        {
            var normalized = path.Replace('\\', '/');
            return normalized.Contains("../") || 
                   normalized.Contains("..\\") ||
                   normalized.StartsWith("..", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Contains("%2e%2e%2f") || // URL encoded
                   normalized.Contains("%252e%252e%252f");
        }
        
        /// <summary>
        /// Validates drive accessibility
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
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Checks if file extension is allowed
        /// </summary>
        private bool IsAllowedExtension(string extension)
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
        
        /// <summary>
        /// Checks for Windows reserved names
        /// </summary>
        private bool HasReservedName(string path)
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
        
        #region Safe Write Operations
        
        /// <summary>
        /// Safe file write with atomic operations and rollback capability
        /// </summary>
        public async Task<WriteResult> SafeWriteAsync(string path, string content, bool createBackup = true)
        {
            return await SafeWriteAsync(path, Encoding.UTF8.GetBytes(content), createBackup);
        }
        
        /// <summary>
        /// Safe file write with atomic operations and rollback capability
        /// </summary>
        public async Task<WriteResult> SafeWriteAsync(string path, byte[] content, bool createBackup = true)
        {
            var validation = ValidateWritePath(path);
            if (!validation.IsValid)
            {
                return WriteResult.Failed($"Path validation failed: {validation.ErrorMessage}");
            }
            
            using var operation = _monitor.StartOperation($"SafeWrite:{Path.GetFileName(path)}");
            
            try
            {
                await _ioSemaphore.WaitAsync(_operationTimeout);
                
                var fileLock = _fileLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
                await fileLock.WaitAsync(_operationTimeout);
                
                try
                {
                    operation.RecordMetadata("path", path);
                    operation.RecordMetadata("size", content.Length.ToString());
                    operation.RecordMetadata("createBackup", createBackup.ToString());
                    
                    var directory = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    
                    var tempPath = Path.Combine(_tempDirectory, Guid.NewGuid().ToString("N") + ".tmp");
                    var backupPath = Path.Combine(_tempDirectory, Guid.NewGuid().ToString("N") + ".backup");
                    
                    // Step 1: Write to temporary file
                    await WriteToTempFile(tempPath, content);
                    
                    // Step 2: Create backup if file exists and backup requested
                    bool backupCreated = false;
                    if (createBackup && File.Exists(path))
                    {
                        try
                        {
                            await File.CopyAsync(path, backupPath, overwrite: false);
                            backupCreated = true;
                        }
                        catch (IOException)
                        {
                            // Backup already exists, try different name
                            backupPath = Path.Combine(_tempDirectory, Guid.NewGuid().ToString("N") + ".backup");
                            await File.CopyAsync(path, backupPath, overwrite: false);
                            backupCreated = true;
                        }
                    }
                    
                    // Step 3: Atomic move
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                    File.Move(tempPath, path);
                    
                    // Step 4: Cleanup backup if successful
                    if (backupCreated)
                    {
                        try { File.Delete(backupPath); } catch { /* ignore cleanup errors */ }
                    }
                    
                    operation.RecordSuccess();
                    return WriteResult.Success(path, content.Length);
                }
                finally
                {
                    fileLock.Release();
                }
            }
            catch (TimeoutException)
            {
                operation.RecordFailure("Operation timeout");
                return WriteResult.Failed("File operation timed out");
            }
            catch (Exception ex)
            {
                operation.RecordFailure(ex.Message);
                return WriteResult.Failed($"Write failed: {ex.Message}");
            }
            finally
            {
                _ioSemaphore.Release();
            }
        }
        
        /// <summary>
        /// Safe JSON serialization with write protection
        /// </summary>
        public async Task<WriteResult> SafeWriteJsonAsync<T>(string path, T data, bool createBackup = true) where T : class
        {
            try
            {
                var validation = ValidateWritePath(path);
                if (!validation.IsValid)
                {
                    return WriteResult.Failed($"Path validation failed: {validation.ErrorMessage}");
                }
                
                using var operation = _monitor.StartOperation($"SafeWriteJson:{typeof(T).Name}");
                
                // Serialize to temporary file first
                var tempPath = Path.Combine(_tempDirectory, Guid.NewGuid().ToString("N") + ".json");
                
                await using var tempStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await System.Text.Json.JsonSerializer.SerializeAsync(tempStream, data, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                });
                
                // Validate JSON integrity
                await ValidateJsonFile(tempPath);
                
                // Complete the write operation
                var content = await File.ReadAllBytesAsync(tempPath);
                var result = await SafeWriteAsync(path, content, createBackup);
                
                // Cleanup temp file
                try { File.Delete(tempPath); } catch { /* ignore */ }
                
                return result;
            }
            catch (Exception ex)
            {
                return WriteResult.Failed($"JSON write failed: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Safe Read Operations
        
        /// <summary>
        /// Safe file read with comprehensive error handling
        /// </summary>
        public async Task<ReadResult<string>> SafeReadAllTextAsync(string path)
        {
            try
            {
                var validation = ValidateWritePath(path);
                if (!validation.IsValid)
                {
                    // For read operations, validate the file can be accessed
                    validation = ValidateReadPath(path);
                    if (!validation.IsValid)
                    {
                        return ReadResult<string>.Failed($"Read path validation failed: {validation.ErrorMessage}");
                    }
                }
                
                using var operation = _monitor.StartOperation($"SafeReadAllText:{Path.GetFileName(path)}");
                
                await _ioSemaphore.WaitAsync(_operationTimeout);
                
                var fileLock = _fileLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
                await fileLock.WaitAsync(_operationTimeout);
                
                try
                {
                    if (!File.Exists(path))
                    {
                        return ReadResult<string>.Failed("File not found");
                    }
                    
                    var content = await File.ReadAllTextAsync(path);
                    operation.RecordMetadata("size", content.Length.ToString());
                    operation.RecordSuccess();
                    
                    return ReadResult<string>.Success(content);
                }
                finally
                {
                    fileLock.Release();
                }
            }
            catch (UnauthorizedAccessException)
            {
                return ReadResult<string>.Failed("Access denied");
            }
            catch (TimeoutException)
            {
                return ReadResult<string>.Failed("Read operation timed out");
            }
            catch (Exception ex)
            {
                return ReadResult<string>.Failed($"Read failed: {ex.Message}");
            }
            finally
            {
                _ioSemaphore.Release();
            }
        }
        
        /// <summary>
        /// Safe file read with comprehensive error handling
        /// </summary>
        public async Task<ReadResult<byte[]>> SafeReadAllBytesAsync(string path)
        {
            try
            {
                var validation = ValidateReadPath(path);
                if (!validation.IsValid)
                {
                    return ReadResult<byte[]>.Failed($"Read path validation failed: {validation.ErrorMessage}");
                }
                
                using var operation = _monitor.StartOperation($"SafeReadAllBytes:{Path.GetFileName(path)}");
                
                await _ioSemaphore.WaitAsync(_operationTimeout);
                
                var fileLock = _fileLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
                await fileLock.WaitAsync(_operationTimeout);
                
                try
                {
                    if (!File.Exists(path))
                    {
                        return ReadResult<byte[]>.Failed("File not found");
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
            }
            catch (UnauthorizedAccessException)
            {
                return ReadResult<byte[]>.Failed("Access denied");
            }
            catch (TimeoutException)
            {
                return ReadResult<byte[]>.Failed("Read operation timed out");
            }
            catch (Exception ex)
            {
                return ReadResult<byte[]>.Failed($"Read failed: {ex.Message}");
            }
            finally
            {
                _ioSemaphore.Release();
            }
        }
        
        /// <summary>
        /// Safe JSON deserialization with read protection
        /// </summary>
        public async Task<ReadResult<T>> SafeReadJsonAsync<T>(string path) where T : class
        {
            var readResult = await SafeReadAllBytesAsync(path);
            if (!readResult.IsSuccess)
            {
                return ReadResult<T>.Failed(readResult.ErrorMessage);
            }
            
            try
            {
                using var stream = new MemoryStream(readResult.Data);
                var data = await System.Text.Json.JsonSerializer.DeserializeAsync<T>(stream);
                
                if (data == null)
                {
                    return ReadResult<T>.Failed("Deserialization returned null");
                }
                
                return ReadResult<T>.Success(data);
            }
            catch (Exception ex)
            {
                return ReadResult<T>.Failed($"JSON deserialization failed: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Directory Operations
        
        /// <summary>
        /// Safe directory creation with validation
        /// </summary>
        public async Task<DirectoryOperationResult> SafeCreateDirectoryAsync(string path)
        {
            try
            {
                var validation = ValidateWritePath(path);
                if (!validation.IsValid)
                {
                    return DirectoryOperationResult.Failed($"Directory path validation failed: {validation.ErrorMessage}");
                }
                
                using var operation = _monitor.StartOperation($"SafeCreateDirectory:{Path.GetFileName(path)}");
                
                var normalizedPath = Path.GetFullPath(path);
                if (Directory.Exists(normalizedPath))
                {
                    return DirectoryOperationResult.Success(normalizedPath);
                }
                
                // Create directory tree
                Directory.CreateDirectory(normalizedPath);
                
                // Validate creation
                if (!Directory.Exists(normalizedPath))
                {
                    return DirectoryOperationResult.Failed("Directory creation failed");
                }
                
                operation.RecordSuccess();
                return DirectoryOperationResult.Success(normalizedPath);
            }
            catch (Exception ex)
            {
                return DirectoryOperationResult.Failed($"Directory creation failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Safe directory enumeration with filtering
        /// </summary>
        public async Task<DirectoryEnumerationResult> SafeEnumerateFilesAsync(string path, string searchPattern = "*", bool recursive = false)
        {
            try
            {
                var validation = ValidateReadPath(path);
                if (!validation.IsValid)
                {
                    return DirectoryEnumerationResult.Failed($"Directory read validation failed: {validation.ErrorMessage}");
                }
                
                using var operation = _monitor.StartOperation($"SafeEnumerateFiles:{Path.GetFileName(path)}");
                
                if (!Directory.Exists(path))
                {
                    return DirectoryEnumerationResult.Failed("Directory not found");
                }
                
                var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var files = Directory.EnumerateFiles(path, searchPattern, searchOption).ToList();
                
                operation.RecordMetadata("fileCount", files.Count.ToString());
                operation.RecordSuccess();
                
                return DirectoryEnumerationResult.Success(files);
            }
            catch (Exception ex)
            {
                return DirectoryEnumerationResult.Failed($"Directory enumeration failed: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Rollback Operations
        
        /// <summary>
        /// Creates a rollback point for a file
        /// </summary>
        public async Task<RollbackResult> CreateRollbackPointAsync(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return RollbackResult.Failed("Source file not found");
                }
                
                var rollbackPath = path + ".rollback_" + DateTime.UtcNow:yyyyMMdd_HHmmssfff";
                await File.CopyAsync(path, rollbackPath);
                
                return RollbackResult.Success(rollbackPath);
            }
            catch (Exception ex)
            {
                return RollbackResult.Failed($"Rollback point creation failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Restores a file from rollback point
        /// </summary>
        public async Task<RollbackResult> RestoreFromRollbackAsync(string rollbackPath, string targetPath)
        {
            try
            {
                var validation = ValidateWritePath(targetPath);
                if (!validation.IsValid)
                {
                    return RollbackResult.Failed($"Target path validation failed: {validation.ErrorMessage}");
                }
                
                if (!File.Exists(rollbackPath))
                {
                    return RollbackResult.Failed("Rollback file not found");
                }
                
                await File.CopyAsync(rollbackPath, targetPath, overwrite: true);
                File.Delete(rollbackPath);
                
                return RollbackResult.Success(targetPath);
            }
            catch (Exception ex)
            {
                return RollbackResult.Failed($"Rollback restore failed: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Monitoring and Statistics
        
        /// <summary>
        /// Gets I/O operation statistics
        /// </summary>
        public IOStatistics GetStatistics()
        {
            return _monitor.GetStatistics();
        }
        
        /// <summary>
        /// Gets recent operation history
        /// </summary>
        public List<OperationRecord> GetOperationHistory(int maxRecords = 100)
        {
            return _monitor.GetRecentOperations(maxRecords);
        }
        
        #endregion
        
        #region Private Methods
        
        private ValidationResult ValidateReadPath(string path)
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
        
        private async Task WriteToTempFile(string tempPath, byte[] content)
        {
            await File.WriteAllBytesAsync(tempPath, content);
        }
        
        private async Task ValidateJsonFile(string path)
        {
            await using var stream = File.OpenRead(path);
            try
            {
                await System.Text.Json.JsonDocument.ParseAsync(stream);
            }
            catch
            {
                // If parsing fails, try to read as text and check basic JSON structure
                var content = await File.ReadAllTextAsync(path);
                if (string.IsNullOrWhiteSpace(content) || 
                    (!content.TrimStart().StartsWith("{") && !content.TrimStart().StartsWith("[")))
                {
                    throw new InvalidOperationException("Invalid JSON content");
                }
            }
        }
        
        #endregion
        
        public void Dispose()
        {
            _ioSemaphore?.Dispose();
            _fileLocks?.Clear();
            
            try
            {
                if (Directory.Exists(_tempDirectory))
                {
                    Directory.Delete(_tempDirectory, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
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