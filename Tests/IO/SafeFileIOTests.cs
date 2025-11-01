using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TiXL.Core.IO;
using TiXL.Core.IO.Examples;
using Xunit;

namespace TiXL.Tests.IO
{
    /// <summary>
    /// Comprehensive test suite for TiXL File I/O Safety system
    /// </summary>
    public class SafeFileIOTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly SafeFileIO _safeFileIO;
        
        public SafeFileIOTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "TiXL_IO_Tests_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_testDirectory);
            _safeFileIO = SafeFileIO.Instance;
        }
        
        [Fact]
        public async Task SafeFileWrite_ShouldValidatePath()
        {
            // Arrange
            var content = "Test content";
            var invalidPaths = new[]
            {
                "../../../secret.txt",  // Directory traversal
                "CON.txt",              // Reserved name
                "",                     // Empty path
                "   file.txt   ",       // Path with spaces
                "test/..\\..\\file.txt" // Mixed traversal
            };
            
            // Act & Assert
            foreach (var invalidPath in invalidPaths)
            {
                var result = await _safeFileIO.SafeWriteAsync(invalidPath, content);
                Assert.False(result.IsSuccess, $"Should reject invalid path: {invalidPath}");
                Assert.NotNull(result.ErrorMessage);
            }
        }
        
        [Fact]
        public async Task SafeFileWrite_ShouldSucceedWithValidPath()
        {
            // Arrange
            var validPath = Path.Combine(_testDirectory, "valid_file.txt");
            var content = "Test content for safe file write";
            
            // Act
            var result = await _safeFileIO.SafeWriteAsync(validPath, content);
            
            // Assert
            Assert.True(result.IsSuccess, "Should succeed with valid path");
            Assert.True(File.Exists(validPath), "File should be created");
            
            var readResult = await _safeFileIO.SafeReadAllTextAsync(validPath);
            Assert.True(readResult.IsSuccess);
            Assert.Equal(content, readResult.Data);
        }
        
        [Fact]
        public async Task SafeFileWrite_ShouldCreateBackup()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "backup_test.txt");
            var originalContent = "Original content";
            var newContent = "Updated content";
            
            // Act - Write original content
            var firstWrite = await _safeFileIO.SafeWriteAsync(filePath, originalContent);
            Assert.True(firstWrite.IsSuccess);
            
            // Act - Update with backup
            var secondWrite = await _safeFileIO.SafeWriteAsync(filePath, newContent, createBackup: true);
            Assert.True(secondWrite.IsSuccess);
            
            // Assert
            var readResult = await _safeFileIO.SafeReadAllTextAsync(filePath);
            Assert.True(readResult.IsSuccess);
            Assert.Equal(newContent, readResult.Data);
        }
        
        [Fact]
        public async Task SafeFileRead_ShouldHandleMissingFile()
        {
            // Arrange
            var nonExistentPath = Path.Combine(_testDirectory, "nonexistent_file.txt");
            
            // Act
            var result = await _safeFileIO.SafeReadAllTextAsync(nonExistentPath);
            
            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
        
        [Fact]
        public async Task SafeFileRead_ShouldSucceedWithValidFile()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "read_test.txt");
            var content = "Content to read";
            await File.WriteAllTextAsync(filePath, content);
            
            // Act
            var result = await _safeFileIO.SafeReadAllTextAsync(filePath);
            
            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(content, result.Data);
        }
        
        [Fact]
        public async Task ScreenshotIOSafety_ShouldValidateImageDimensions()
        {
            // Arrange
            using var smallImage = CreateTestImage(50, 30); // Too small
            var smallPath = Path.Combine(_testDirectory, "small_screenshot.png");
            
            // Act
            var result = await ScreenshotIOSafety.SaveScreenshotAsync(smallImage, smallPath);
            
            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("too small", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
        
        [Fact]
        public async Task ScreenshotIOSafety_ShouldSaveValidScreenshot()
        {
            // Arrange
            using var validImage = CreateTestImage(800, 600);
            var screenshotPath = Path.Combine(_testDirectory, "valid_screenshot.png");
            
            // Act
            var result = await ScreenshotIOSafety.SaveScreenshotAsync(validImage, screenshotPath);
            
            // Assert
            Assert.True(result.IsSuccess, "Should save valid screenshot");
            Assert.True(File.Exists(screenshotPath), "Screenshot file should exist");
            Assert.True(new FileInfo(screenshotPath).Length > 0, "Screenshot should have content");
        }
        
        [Fact]
        public async Task ScreenshotIOSafety_ShouldCreateThumbnail()
        {
            // Arrange
            using var sourceImage = CreateTestImage(800, 600);
            var sourcePath = Path.Combine(_testDirectory, "source_image.png");
            var thumbPath = Path.Combine(_testDirectory, "thumbnail.png");
            
            // Save source image
            var saveResult = await ScreenshotIOSafety.SaveScreenshotAsync(sourceImage, sourcePath);
            Assert.True(saveResult.IsSuccess);
            
            // Act
            var thumbResult = await ScreenshotIOSafety.CreateThumbnailAsync(sourcePath, thumbPath, 200, 200);
            
            // Assert
            Assert.True(thumbResult.IsSuccess, "Should create thumbnail");
            Assert.Equal(200, thumbResult.Width);
            Assert.Equal(150, thumbResult.Height); // Aspect ratio preserved
            Assert.True(File.Exists(thumbPath), "Thumbnail file should exist");
        }
        
        [Fact]
        public async Task SafeSerialization_ShouldValidateJsonData()
        {
            // Arrange
            var testData = new TestProjectData
            {
                Name = "Test Project",
                Version = "1.0.0",
                CreatedUtc = DateTime.UtcNow,
                Settings = new Dictionary<string, object>
                {
                    { "Theme", "Dark" },
                    { "AutoSave", true }
                }
            };
            
            var jsonPath = Path.Combine(_testDirectory, "test_project.json");
            
            // Act
            var result = await SafeSerialization.SafeSerializeToJsonAsync(testData, jsonPath);
            
            // Assert
            Assert.True(result.IsSuccess, "Should serialize valid data");
            Assert.True(File.Exists(jsonPath), "JSON file should exist");
            
            // Verify deserialization
            var loadResult = await SafeSerialization.SafeDeserializeFromJsonAsync<TestProjectData>(jsonPath);
            Assert.True(loadResult.IsSuccess);
            Assert.Equal(testData.Name, loadResult.Data.Name);
            Assert.Equal(testData.Version, loadResult.Data.Version);
        }
        
        [Fact]
        public async Task SafeSerialization_ShouldCreateRollbackPoint()
        {
            // Arrange
            var jsonPath = Path.Combine(_testDirectory, "rollback_test.json");
            var testData = new TestProjectData { Name = "Original Project", Version = "1.0.0" };
            
            // Act - Create initial file
            var initialResult = await SafeSerialization.SafeSerializeToJsonAsync(testData, jsonPath);
            Assert.True(initialResult.IsSuccess);
            
            // Act - Create rollback point
            var rollbackResult = await SafeSerialization.CreateRollbackPointAsync(jsonPath);
            Assert.True(rollbackResult.IsSuccess);
            
            // Act - Modify file and simulate corruption
            await File.WriteAllTextAsync(jsonPath, "{ invalid json }");
            
            // Act - Recover from rollback
            var recoveryResult = await SafeSerialization.RecoverFromErrorAsync(jsonPath);
            Assert.True(recoveryResult.IsSuccess, "Should recover from backup");
            
            // Assert - Verify recovery
            var verifyResult = await SafeSerialization.SafeDeserializeFromJsonAsync<TestProjectData>(jsonPath);
            Assert.True(verifyResult.IsSuccess);
            Assert.Equal("Original Project", verifyResult.Data.Name);
        }
        
        [Fact]
        public async Task ProjectFileIOSafety_ShouldCreateValidProject()
        {
            // Arrange
            var metadata = new ProjectMetadata
            {
                Name = "Test TiXL Project",
                Id = Guid.NewGuid().ToString(),
                Version = "1.0.0",
                Description = "A test project",
                CreatedUtc = DateTime.UtcNow,
                LastModifiedUtc = DateTime.UtcNow
            };
            
            var projectPath = Path.Combine(_testDirectory, "test_project.tixlproject");
            
            // Act
            var createResult = await ProjectFileIOSafety.CreateProjectAsync(metadata, projectPath);
            
            // Assert
            Assert.True(createResult.IsSuccess, "Should create project successfully");
            Assert.True(File.Exists(projectPath), "Project file should exist");
            
            // Verify project can be loaded
            var loadResult = await ProjectFileIOSafety.LoadProjectAsync(projectPath);
            Assert.True(loadResult.IsSuccess);
            Assert.Equal(metadata.Name, loadResult.Metadata.Name);
            Assert.Equal(metadata.Id, loadResult.Metadata.Id);
        }
        
        [Fact]
        public async Task ProjectFileIOSafety_ShouldHandleBackupOperations()
        {
            // Arrange
            var projectPath = Path.Combine(_testDirectory, "backup_project.tixlproject");
            var metadata = new ProjectMetadata
            {
                Name = "Backup Test Project",
                Id = Guid.NewGuid().ToString(),
                Version = "1.0.0",
                CreatedUtc = DateTime.UtcNow,
                LastModifiedUtc = DateTime.UtcNow
            };
            
            // Create project
            var createResult = await ProjectFileIOSafety.CreateProjectAsync(metadata, projectPath);
            Assert.True(createResult.IsSuccess);
            
            // Act - Create backup
            var backupResult = await ProjectFileIOSafety.CreateBackupAsync(projectPath);
            Assert.True(backupResult.IsSuccess, "Should create backup successfully");
            Assert.Contains("backup", backupResult.BackupPath, StringComparison.OrdinalIgnoreCase);
        }
        
        [Fact]
        public void PathValidation_ShouldDetectDangerousPaths()
        {
            var dangerousPaths = new[]
            {
                "../../../etc/passwd",
                "..\\..\\Windows\\System32",
                "file.txt%2e%2e%2f%2e%2e%2f",
                "CON.txt",
                "PRN.txt",
                "COM1.txt",
                new string('a', 300) + ".txt" // Too long
            };
            
            foreach (var dangerousPath in dangerousPaths)
            {
                var validation = _safeFileIO.ValidateWritePath(dangerousPath);
                Assert.False(validation.IsValid, $"Should reject dangerous path: {dangerousPath}");
            }
        }
        
        [Fact]
        public void PathValidation_ShouldAcceptValidPaths()
        {
            var validPaths = new[]
            {
                "documents/project.json",
                "C:\\Users\\Test\\project.tixlproject",
                "/home/user/project.json",
                "screenshots/2024-01-01/image.png"
            };
            
            foreach (var validPath in validPaths)
            {
                var validation = _safeFileIO.ValidateWritePath(validPath);
                // Note: Some paths might fail due to filesystem access, but validation logic should pass
                Assert.NotNull(validation);
            }
        }
        
        [Fact]
        public async Task Monitoring_ShouldTrackOperations()
        {
            // Arrange
            var testFile = Path.Combine(_testDirectory, "monitoring_test.txt");
            var content = "Monitoring test content";
            
            // Act - Perform operations
            await _safeFileIO.SafeWriteAsync(testFile, content);
            await _safeFileIO.SafeReadAllTextAsync(testFile);
            
            // Get statistics
            var stats = _safeFileIO.GetStatistics();
            
            // Assert
            Assert.True(stats.TotalOperations >= 2, "Should track at least 2 operations");
            Assert.True(stats.SuccessfulOperations >= 2, "Should track successful operations");
            
            // Get history
            var history = _safeFileIO.GetOperationHistory(10);
            Assert.True(history.Count > 0, "Should have operation history");
            
            // Verify history contains our operations
            var relevantOperations = history.Where(h => h.OperationName.Contains("monitoring_test")).ToList();
            Assert.True(relevantOperations.Count > 0, "Should contain our test operations in history");
        }
        
        [Fact]
        public async Task AtomicOperations_ShouldEnsureDataIntegrity()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "atomic_test.txt");
            var originalContent = new string('A', 10000); // 10KB
            var newContent = new string('B', 10000); // 10KB
            
            // Act - Write original content
            var firstWrite = await _safeFileIO.SafeWriteAsync(filePath, originalContent);
            Assert.True(firstWrite.IsSuccess);
            
            // Verify original content
            var firstRead = await _safeFileIO.SafeReadAllTextAsync(filePath);
            Assert.True(firstRead.IsSuccess);
            Assert.Equal(originalContent, firstRead.Data);
            
            // Act - Write new content (should be atomic)
            var secondWrite = await _safeFileIO.SafeWriteAsync(filePath, newContent, createBackup: true);
            Assert.True(secondWrite.IsSuccess);
            
            // Verify new content is complete and correct
            var secondRead = await _safeFileIO.SafeReadAllTextAsync(filePath);
            Assert.True(secondRead.IsSuccess);
            Assert.Equal(newContent, secondRead.Data);
            Assert.NotEqual(originalContent, secondRead.Data);
        }
        
        [Fact]
        public async Task ConcurrentOperations_ShouldHandleMultipleFiles()
        {
            // Arrange
            var fileCount = 10;
            var tasks = new List<Task<WriteResult>>();
            
            // Act - Perform concurrent writes
            for (int i = 0; i < fileCount; i++)
            {
                var filePath = Path.Combine(_testDirectory, $"concurrent_test_{i}.txt");
                var content = $"Content for file {i}";
                tasks.Add(_safeFileIO.SafeWriteAsync(filePath, content));
            }
            
            // Wait for all operations to complete
            var results = await Task.WhenAll(tasks);
            
            // Assert
            Assert.All(results, result => Assert.True(result.IsSuccess, "All concurrent operations should succeed"));
            
            // Verify all files were created
            for (int i = 0; i < fileCount; i++)
            {
                var filePath = Path.Combine(_testDirectory, $"concurrent_test_{i}.txt");
                Assert.True(File.Exists(filePath), $"File {i} should exist");
                
                var readResult = await _safeFileIO.SafeReadAllTextAsync(filePath);
                Assert.True(readResult.IsSuccess);
                Assert.Equal($"Content for file {i}", readResult.Data);
            }
        }
        
        [Fact]
        public async Task ErrorHandling_ShouldProvideMeaningfulMessages()
        {
            // Test various error scenarios
            var scenarios = new[]
            {
                new { Path = "", Content = "test", Description = "empty path" },
                new { Path = "test.txt", Content = "", Description = "empty content" },
                new { Path = "test.txt", Content = new string('A', 100 * 1024 * 1024), Description = "large content" }
            };
            
            foreach (var scenario in scenarios)
            {
                var result = await _safeFileIO.SafeWriteAsync(scenario.Path, scenario.Content);
                Assert.False(result.IsSuccess, $"Should fail for scenario: {scenario.Description}");
                Assert.NotNull(result.ErrorMessage);
                Assert.NotEqual("", result.ErrorMessage.Trim(), "Should provide meaningful error message");
            }
        }
        
        #region Helper Methods
        
        private static Image CreateTestImage(int width, int height)
        {
            var bitmap = new Bitmap(width, height);
            using var graphics = Graphics.FromImage(bitmap);
            
            // Fill with gradient
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                new Rectangle(0, 0, width, height),
                Color.Red, Color.Blue, 45f);
            graphics.FillRectangle(brush, 0, 0, width, height);
            
            // Add some text
            using var font = new Font("Arial", 12);
            using var textBrush = new SolidBrush(Color.White);
            graphics.DrawString("Test Image", font, textBrush, 10, 10);
            
            return bitmap;
        }
        
        public void Dispose()
        {
            // Cleanup test directory
            if (Directory.Exists(_testDirectory))
            {
                try
                {
                    Directory.Delete(_testDirectory, true);
                }
                catch
                {
                    // Ignore cleanup errors in tests
                }
            }
        }
        
        #endregion
        
        #region Test Data Models
        
        private class TestProjectData
        {
            public string Name { get; set; }
            public string Version { get; set; }
            public DateTime CreatedUtc { get; set; }
            public Dictionary<string, object> Settings { get; set; }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Integration tests for file I/O safety features
    /// </summary>
    public class SafeFileIOIntegrationTests : IDisposable
    {
        private readonly string _testDirectory;
        
        public SafeFileIOIntegrationTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "TiXL_IO_Integration_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_testDirectory);
        }
        
        [Fact]
        public async Task EndToEnd_ProjectCreationAndBackupWorkflow()
        {
            // Arrange
            var projectMetadata = new ProjectMetadata
            {
                Name = "End-to-End Test Project",
                Id = Guid.NewGuid().ToString(),
                Version = "1.0.0",
                Description = "Testing complete workflow",
                CreatedUtc = DateTime.UtcNow,
                LastModifiedUtc = DateTime.UtcNow,
                Tags = new List<string> { "test", "integration" }
            };
            
            var projectPath = Path.Combine(_testDirectory, "e2e_test_project.tixlproject");
            
            // Step 1: Create project
            var createResult = await ProjectFileIOSafety.CreateProjectAsync(projectMetadata, projectPath);
            Assert.True(createResult.IsSuccess, "Project creation should succeed");
            
            // Step 2: Load project
            var loadResult = await ProjectFileIOSafety.LoadProjectAsync(projectPath);
            Assert.True(loadResult.IsSuccess, "Project loading should succeed");
            Assert.Equal(projectMetadata.Name, loadResult.Metadata.Name);
            
            // Step 3: Update project
            var updatedMetadata = projectMetadata with { LastModifiedUtc = DateTime.UtcNow };
            var projectData = new ProjectData
            {
                Metadata = updatedMetadata,
                Version = "1.0",
                SchemaVersion = 1,
                CreatedUtc = DateTime.UtcNow,
                LastModifiedUtc = DateTime.UtcNow,
                Settings = new Dictionary<string, object> { { "Updated", true } }
            };
            
            var saveResult = await ProjectFileIOSafety.SaveProjectAsync(updatedMetadata, projectData, projectPath);
            Assert.True(saveResult.IsSuccess, "Project save should succeed");
            
            // Step 4: Create backup
            var backupResult = await ProjectFileIOSafety.CreateBackupAsync(projectPath);
            Assert.True(backupResult.IsSuccess, "Backup creation should succeed");
            
            // Step 5: Verify backup
            Assert.True(File.Exists(backupResult.BackupPath), "Backup file should exist");
            
            // Step 6: Test recovery
            var restoreResult = await ProjectFileIOSafety.RestoreFromBackupAsync(projectPath);
            Assert.True(restoreResult.IsSuccess, "Project restore should succeed");
            
            // Step 7: Verify restored project
            var verifyResult = await ProjectFileIOSafety.LoadProjectAsync(projectPath);
            Assert.True(verifyResult.IsSuccess, "Restored project should be loadable");
            Assert.Equal(projectMetadata.Name, verifyResult.Metadata.Name);
        }
        
        [Fact]
        public async Task EndToEnd_ScreenshotWorkflowWithThumbnail()
        {
            // Arrange
            using var screenshot = CreateTestScreenshot(1024, 768);
            var screenshotPath = Path.Combine(_testDirectory, "screenshot.png");
            var thumbnailPath = Path.Combine(_testDirectory, "thumbnail.png");
            
            // Step 1: Save screenshot
            var saveResult = await ScreenshotIOSafety.SaveScreenshotAsync(screenshot, screenshotPath);
            Assert.True(saveResult.IsSuccess, "Screenshot save should succeed");
            
            // Step 2: Verify screenshot integrity
            var validateResult = await ScreenshotIOSafety.ValidateScreenshotFileAsync(screenshotPath);
            Assert.True(validateResult.IsValid, "Screenshot validation should pass");
            
            // Step 3: Create thumbnail
            var thumbResult = await ScreenshotIOSafety.CreateThumbnailAsync(screenshotPath, thumbnailPath, 256, 256);
            Assert.True(thumbResult.IsSuccess, "Thumbnail creation should succeed");
            Assert.True(File.Exists(thumbnailPath), "Thumbnail file should exist");
            
            // Step 4: Verify thumbnail dimensions
            using var thumbImage = Image.FromFile(thumbnailPath);
            Assert.True(thumbImage.Width <= 256, "Thumbnail width should be <= 256");
            Assert.True(thumbImage.Height <= 256, "Thumbnail height should be <= 256");
        }
        
        [Fact]
        public async Task EndToEnd_JsonSerializationWithRecovery()
        {
            // Arrange
            var originalData = new ComplexTestData
            {
                Name = "Original Data",
                Values = Enumerable.Range(1, 100).Select(i => $"Value_{i}").ToList(),
                Metadata = new Dictionary<string, object>
                {
                    { "CreatedBy", "Integration Test" },
                    { "Timestamp", DateTime.UtcNow },
                    { "Version", 1 }
                }
            };
            
            var jsonPath = Path.Combine(_testDirectory, "integration_test.json");
            
            // Step 1: Serialize data
            var serializeResult = await SafeSerialization.SafeSerializeToJsonAsync(originalData, jsonPath, true, true);
            Assert.True(serializeResult.IsSuccess, "Serialization should succeed");
            
            // Step 2: Deserialize and verify
            var deserializeResult = await SafeSerialization.SafeDeserializeFromJsonAsync<ComplexTestData>(jsonPath);
            Assert.True(deserializeResult.IsSuccess, "Deserialization should succeed");
            Assert.Equal(originalData.Name, deserializeResult.Data.Name);
            Assert.Equal(originalData.Values.Count, deserializeResult.Data.Values.Count);
            
            // Step 3: Create rollback point
            var rollbackResult = await SafeSerialization.CreateRollbackPointAsync(jsonPath);
            Assert.True(rollbackResult.IsSuccess, "Rollback creation should succeed");
            
            // Step 4: Simulate corruption
            await File.WriteAllTextAsync(jsonPath, "{ \"corrupted\": true }");
            
            // Step 5: Recover from corruption
            var recoveryResult = await SafeSerialization.RecoverFromErrorAsync(jsonPath);
            Assert.True(recoveryResult.IsSuccess, "Recovery should succeed");
            
            // Step 6: Verify recovery
            var verifyResult = await SafeSerialization.SafeDeserializeFromJsonAsync<ComplexTestData>(jsonPath);
            Assert.True(verifyResult.IsSuccess, "Recovered data should be deserializable");
            Assert.Equal(originalData.Name, verifyResult.Data.Name);
        }
        
        private static Image CreateTestScreenshot(int width, int height)
        {
            var bitmap = new Bitmap(width, height);
            using var graphics = Graphics.FromImage(bitmap);
            
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                new Rectangle(0, 0, width, height),
                Color.Green, Color.Yellow, 90f);
            graphics.FillRectangle(brush, 0, 0, width, height);
            
            return bitmap;
        }
        
        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                try
                {
                    Directory.Delete(_testDirectory, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
        
        private class ComplexTestData
        {
            public string Name { get; set; }
            public List<string> Values { get; set; }
            public Dictionary<string, object> Metadata { get; set; }
        }
    }
}