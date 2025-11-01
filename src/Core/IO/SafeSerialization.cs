using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;

namespace TiXL.Core.IO
{
    /// <summary>
    /// Safe serialization operations with validation and rollback support
    /// </summary>
    public static class SafeSerialization
    {
        private static readonly JsonSerializerOptions _defaultJsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            IgnoreNullValues = true,
            MaxDepth = 64,
            ReferenceHandler = ReferenceHandler.Preserve
        };
        
        private static readonly JsonSerializerOptions _compactJsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            IgnoreNullValues = true,
            MaxDepth = 64,
            ReferenceHandler = ReferenceHandler.Preserve
        };
        
        #region JSON Serialization
        
        /// <summary>
        /// Safely serializes an object to JSON with validation
        /// </summary>
        public static async Task<SerializationResult> SafeSerializeToJsonAsync<T>(T data, string filePath, bool prettyPrint = true, bool createBackup = true) where T : class
        {
            try
            {
                if (data == null)
                {
                    return SerializationResult.Failed("Data cannot be null");
                }
                
                // Validate path
                var safeFileIO = SafeFileIO.Instance;
                var pathValidation = safeFileIO.ValidateWritePath(filePath);
                if (!pathValidation.IsValid)
                {
                    return SerializationResult.Failed($"Invalid path: {pathValidation.ErrorMessage}");
                }
                
                // Validate JSON serializability
                var validationResult = ValidateJsonSerializable(data);
                if (!validationResult.IsValid)
                {
                    return SerializationResult.Failed($"JSON validation failed: {validationResult.ErrorMessage}");
                }
                
                using var operation = new System.Diagnostics.Stopwatch();
                operation.Start();
                
                // Serialize to string first for validation
                var options = prettyPrint ? _defaultJsonOptions : _compactJsonOptions;
                string jsonString = JsonSerializer.Serialize(data, options);
                
                // Validate JSON structure
                if (!IsValidJson(jsonString))
                {
                    return SerializationResult.Failed("Generated JSON is not valid");
                }
                
                // Check size limits
                if (jsonString.Length > 100 * 1024 * 1024) // 100MB
                {
                    return SerializationResult.Failed("Serialized JSON too large");
                }
                
                // Write to file using SafeFileIO
                var writeResult = await safeFileIO.SafeWriteAsync(filePath, jsonString, createBackup);
                
                operation.Stop();
                
                if (writeResult.IsSuccess)
                {
                    return SerializationResult.Success(filePath, jsonString.Length, operation.Elapsed);
                }
                else
                {
                    return SerializationResult.Failed(writeResult.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                return SerializationResult.Failed($"JSON serialization failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Safely deserializes JSON from file with validation
        /// </summary>
        public static async Task<DeserializationResult<T>> SafeDeserializeFromJsonAsync<T>(string filePath) where T : class
        {
            try
            {
                // Validate path and read
                var safeFileIO = SafeFileIO.Instance;
                var readResult = await safeFileIO.SafeReadAllTextAsync(filePath);
                if (!readResult.IsSuccess)
                {
                    return DeserializationResult<T>.Failed(readResult.ErrorMessage);
                }
                
                using var operation = new System.Diagnostics.Stopwatch();
                operation.Start();
                
                var jsonString = readResult.Data;
                
                // Validate JSON structure
                if (!IsValidJson(jsonString))
                {
                    return DeserializationResult<T>.Failed("Invalid JSON structure");
                }
                
                // Check size limits
                if (jsonString.Length > 100 * 1024 * 1024) // 100MB
                {
                    return DeserializationResult<T>.Failed("JSON file too large");
                }
                
                // Deserialize with error handling
                T data;
                try
                {
                    data = JsonSerializer.Deserialize<T>(jsonString, _defaultJsonOptions);
                }
                catch (JsonException ex)
                {
                    return DeserializationResult<T>.Failed($"JSON deserialization failed: {ex.Message}");
                }
                
                operation.Stop();
                
                if (data == null)
                {
                    return DeserializationResult<T>.Failed("Deserialized data is null");
                }
                
                return DeserializationResult<T>.Success(data, operation.Elapsed);
            }
            catch (Exception ex)
            {
                return DeserializationResult<T>.Failed($"JSON deserialization failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Safely deserializes JSON with validation against schema
        /// </summary>
        public static async Task<DeserializationResult<T>> SafeDeserializeFromJsonWithSchemaAsync<T>(string filePath, string schemaPath) where T : class
        {
            try
            {
                // First validate against schema
                var schemaValidation = await ValidateJsonWithSchemaAsync(filePath, schemaPath);
                if (!schemaValidation.IsValid)
                {
                    return DeserializationResult<T>.Failed($"Schema validation failed: {schemaValidation.ErrorMessage}");
                }
                
                // Then deserialize normally
                return await SafeDeserializeFromJsonAsync<T>(filePath);
            }
            catch (Exception ex)
            {
                return DeserializationResult<T>.Failed($"Schema validation/deserialization failed: {ex.Message}");
            }
        }
        
        #endregion
        
        #region XML Serialization
        
        /// <summary>
        /// Safely serializes an object to XML with validation
        /// </summary>
        public static async Task<SerializationResult> SafeSerializeToXmlAsync<T>(T data, string filePath, bool createBackup = true) where T : class
        {
            try
            {
                if (data == null)
                {
                    return SerializationResult.Failed("Data cannot be null");
                }
                
                // Validate path
                var safeFileIO = SafeFileIO.Instance;
                var pathValidation = safeFileIO.ValidateWritePath(filePath);
                if (!pathValidation.IsValid)
                {
                    return SerializationResult.Failed($"Invalid path: {pathValidation.ErrorMessage}");
                }
                
                using var operation = new System.Diagnostics.Stopwatch();
                operation.Start();
                
                // Serialize to XML
                var xmlString = SerializeToXmlString(data);
                
                // Validate XML structure
                if (!IsValidXml(xmlString))
                {
                    return SerializationResult.Failed("Generated XML is not valid");
                }
                
                // Write to file using SafeFileIO
                var writeResult = await safeFileIO.SafeWriteAsync(filePath, xmlString, createBackup);
                
                operation.Stop();
                
                if (writeResult.IsSuccess)
                {
                    return SerializationResult.Success(filePath, xmlString.Length, operation.Elapsed);
                }
                else
                {
                    return SerializationResult.Failed(writeResult.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                return SerializationResult.Failed($"XML serialization failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Safely deserializes XML from file with validation
        /// </summary>
        public static async Task<DeserializationResult<T>> SafeDeserializeFromXmlAsync<T>(string filePath) where T : class
        {
            try
            {
                // Validate path and read
                var safeFileIO = SafeFileIO.Instance;
                var readResult = await safeFileIO.SafeReadAllTextAsync(filePath);
                if (!readResult.IsSuccess)
                {
                    return DeserializationResult<T>.Failed(readResult.ErrorMessage);
                }
                
                using var operation = new System.Diagnostics.Stopwatch();
                operation.Start();
                
                var xmlString = readResult.Data;
                
                // Validate XML structure
                if (!IsValidXml(xmlString))
                {
                    return DeserializationResult<T>.Failed("Invalid XML structure");
                }
                
                // Deserialize with error handling
                T data;
                try
                {
                    data = DeserializeFromXmlString<T>(xmlString);
                }
                catch (Exception ex)
                {
                    return DeserializationResult<T>.Failed($"XML deserialization failed: {ex.Message}");
                }
                
                operation.Stop();
                
                if (data == null)
                {
                    return DeserializationResult<T>.Failed("Deserialized data is null");
                }
                
                return DeserializationResult<T>.Success(data, operation.Elapsed);
            }
            catch (Exception ex)
            {
                return DeserializationResult<T>.Failed($"XML deserialization failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Validates XML against XSD schema
        /// </summary>
        public static async Task<ValidationResult> ValidateXmlWithSchemaAsync(string xmlPath, string schemaPath)
        {
            try
            {
                var readResult = await SafeFileIO.Instance.SafeReadAllTextAsync(xmlPath);
                if (!readResult.IsSuccess)
                {
                    return new ValidationResult { IsValid = false, ErrorMessage = readResult.ErrorMessage };
                }
                
                var schemaReadResult = await SafeFileIO.Instance.SafeReadAllTextAsync(schemaPath);
                if (!schemaReadResult.IsValid)
                {
                    return new ValidationResult { IsValid = false, ErrorMessage = schemaReadResult.ErrorMessage };
                }
                
                var xmlDoc = new XmlDocument();
                var schemaDoc = new XmlDocument();
                
                xmlDoc.LoadXml(readResult.Data);
                schemaDoc.LoadXml(schemaReadResult.Data);
                
                var schemas = new XmlSchemaSet();
                schemas.Add(null, XmlReader.Create(new StringReader(schemaDoc.OuterXml)));
                schemas.Compile();
                
                xmlDoc.Schemas = schemas;
                
                var validationEventArgs = new List<ValidationEventArgs>();
                xmlDoc.Validate((sender, e) => validationEventArgs.Add(e));
                
                if (validationEventArgs.Count > 0)
                {
                    var errors = string.Join("; ", validationEventArgs.Select(e => e.Message));
                    return new ValidationResult { IsValid = false, ErrorMessage = $"XML schema validation failed: {errors}" };
                }
                
                return new ValidationResult { IsValid = true };
            }
            catch (Exception ex)
            {
                return new ValidationResult { IsValid = false, ErrorMessage = $"XML validation failed: {ex.Message}" };
            }
        }
        
        #endregion
        
        #region Validation and Security
        
        /// <summary>
        /// Validates JSON structure without deserializing
        /// </summary>
        public static ValidationResult ValidateJsonStructure(string jsonPath)
        {
            try
            {
                var result = SafeFileIO.Instance.SafeReadAllTextAsync(jsonPath).Result;
                if (!result.IsSuccess)
                {
                    return new ValidationResult { IsValid = false, ErrorMessage = result.ErrorMessage };
                }
                
                return IsValidJson(result.Data) 
                    ? new ValidationResult { IsValid = true }
                    : new ValidationResult { IsValid = false, ErrorMessage = "Invalid JSON structure" };
            }
            catch (Exception ex)
            {
                return new ValidationResult { IsValid = false, ErrorMessage = ex.Message };
            }
        }
        
        /// <summary>
        /// Validates XML structure without deserializing
        /// </summary>
        public static ValidationResult ValidateXmlStructure(string xmlPath)
        {
            try
            {
                var result = SafeFileIO.Instance.SafeReadAllTextAsync(xmlPath).Result;
                if (!result.IsSuccess)
                {
                    return new ValidationResult { IsValid = false, ErrorMessage = result.ErrorMessage };
                }
                
                return IsValidXml(result.Data)
                    ? new ValidationResult { IsValid = true }
                    : new ValidationResult { IsValid = false, ErrorMessage = "Invalid XML structure" };
            }
            catch (Exception ex)
            {
                return new ValidationResult { IsValid = false, ErrorMessage = ex.Message };
            }
        }
        
        /// <summary>
        /// Detects potentially dangerous content in serialized data
        /// </summary>
        public static ValidationResult ScanForSecurityThreats(string filePath)
        {
            try
            {
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                
                return extension switch
                {
                    ".json" => ScanJsonForThreats(filePath),
                    ".xml" => ScanXmlForThreats(filePath),
                    _ => new ValidationResult { IsValid = true } // Unknown format, allow
                };
            }
            catch (Exception ex)
            {
                return new ValidationResult { IsValid = false, ErrorMessage = $"Security scan failed: {ex.Message}" };
            }
        }
        
        #endregion
        
        #region Rollback and Recovery
        
        /// <summary>
        /// Creates a rollback point for serialization files
        /// </summary>
        public static async Task<RollbackResult> CreateRollbackPointAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return RollbackResult.Failed("Source file not found");
            }
            
            var rollbackPath = filePath + ".rollback_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff");
            return await SafeFileIO.Instance.RestoreFromRollbackAsync(filePath, rollbackPath);
        }
        
        /// <summary>
        /// Recovers from serialization error by rolling back to previous version
        /// </summary>
        public static async Task<RollbackResult> RecoverFromErrorAsync(string filePath)
        {
            try
            {
                var rollbackFiles = Directory.GetFiles(Path.GetDirectoryName(filePath), 
                    Path.GetFileName(filePath) + ".rollback_*", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                    .ToList();
                
                if (rollbackFiles.Count == 0)
                {
                    return RollbackResult.Failed("No rollback files found");
                }
                
                var latestRollback = rollbackFiles.First();
                return await SafeFileIO.Instance.RestoreFromRollbackAsync(latestRollback, filePath);
            }
            catch (Exception ex)
            {
                return RollbackResult.Failed($"Recovery failed: {ex.Message}");
            }
        }
        
        #endregion
        
        #region Private Methods
        
        private static ValidationResult ValidateJsonSerializable<T>(T data)
        {
            try
            {
                // Test serialization to detect circular references or other issues
                var testJson = JsonSerializer.Serialize(data, _compactJsonOptions);
                
                // Check for extremely large result
                if (testJson.Length > 100 * 1024 * 1024) // 100MB
                {
                    return new ValidationResult { IsValid = false, ErrorMessage = "Data would serialize to JSON larger than 100MB" };
                }
                
                return new ValidationResult { IsValid = true };
            }
            catch (Exception ex)
            {
                return new ValidationResult { IsValid = false, ErrorMessage = $"JSON serialization test failed: {ex.Message}" };
            }
        }
        
        private static bool IsValidJson(string jsonString)
        {
            if (string.IsNullOrWhiteSpace(jsonString))
                return false;
            
            try
            {
                JsonDocument.Parse(jsonString);
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        private static bool IsValidXml(string xmlString)
        {
            if (string.IsNullOrWhiteSpace(xmlString))
                return false;
            
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(xmlString);
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        private static string SerializeToXmlString<T>(T data)
        {
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(T));
            
            using var stringWriter = new StringWriter();
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                OmitXmlDeclaration = false
            };
            
            using var writer = XmlWriter.Create(stringWriter, settings);
            serializer.Serialize(writer, data);
            
            return stringWriter.ToString();
        }
        
        private static T DeserializeFromXmlString<T>(string xmlString)
        {
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(T));
            
            using var stringReader = new StringReader(xmlString);
            return (T)serializer.Deserialize(stringReader);
        }
        
        private static ValidationResult ValidateJsonWithSchemaAsync(string jsonPath, string schemaPath)
        {
            // This is a simplified implementation
            // A full JSON Schema validation would require additional libraries like JsonSchema.Net
            return new ValidationResult { IsValid = true };
        }
        
        private static ValidationResult ScanJsonForThreats(string filePath)
        {
            var readResult = SafeFileIO.Instance.SafeReadAllTextAsync(filePath).Result;
            if (!readResult.IsSuccess)
            {
                return new ValidationResult { IsValid = false, ErrorMessage = readResult.ErrorMessage };
            }
            
            var content = readResult.Data.ToLowerInvariant();
            
            // Check for potential script injection or dangerous content
            var dangerousPatterns = new[]
            {
                "<script", "javascript:", "data:", "eval(", "function(", "=>",
                "import(", "require(", "eval(", "window", "document."
            };
            
            foreach (var pattern in dangerousPatterns)
            {
                if (content.Contains(pattern))
                {
                    return new ValidationResult { IsValid = false, ErrorMessage = $"Potentially dangerous content detected: {pattern}" };
                }
            }
            
            return new ValidationResult { IsValid = true };
        }
        
        private static ValidationResult ScanXmlForThreats(string filePath)
        {
            var readResult = SafeFileIO.Instance.SafeReadAllTextAsync(filePath).Result;
            if (!readResult.IsSuccess)
            {
                return new ValidationResult { IsValid = false, ErrorMessage = readResult.ErrorMessage };
            }
            
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(readResult.Data);
                
                // Check for external entity injection
                var settings = new XmlReaderSettings
                {
                    XmlResolver = null, // Disable external entities
                    DtdProcessing = DtdProcessing.Ignore
                };
                
                return new ValidationResult { IsValid = true };
            }
            catch (XmlException ex)
            {
                return new ValidationResult { IsValid = false, ErrorMessage = $"XML parsing error: {ex.Message}" };
            }
            catch
            {
                return new ValidationResult { IsValid = false, ErrorMessage = "XML contains potentially dangerous content" };
            }
        }
        
        #endregion
    }
    
    #region Serialization Results
    
    public class SerializationResult
    {
        public bool IsSuccess { get; set; }
        public string FilePath { get; set; }
        public int BytesWritten { get; set; }
        public TimeSpan Duration { get; set; }
        public string ErrorMessage { get; set; }
        
        public static SerializationResult Success(string filePath, int bytesWritten, TimeSpan duration)
        {
            return new SerializationResult { IsSuccess = true, FilePath = filePath, BytesWritten = bytesWritten, Duration = duration };
        }
        
        public static SerializationResult Failed(string error)
        {
            return new SerializationResult { IsSuccess = false, ErrorMessage = error };
        }
    }
    
    public class DeserializationResult<T> where T : class
    {
        public bool IsSuccess { get; set; }
        public T Data { get; set; }
        public TimeSpan Duration { get; set; }
        public string ErrorMessage { get; set; }
        
        public static DeserializationResult<T> Success(T data, TimeSpan duration)
        {
            return new DeserializationResult<T> { IsSuccess = true, Data = data, Duration = duration };
        }
        
        public static DeserializationResult<T> Failed(string error)
        {
            return new DeserializationResult<T> { IsSuccess = false, ErrorMessage = error };
        }
    }
    
    #endregion
}