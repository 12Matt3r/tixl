using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;

namespace TiXL.Core.IO.Examples
{
    /// <summary>
    /// Comprehensive examples showing safe file I/O operations vs unsafe patterns
    /// Demonstrates the benefits and usage of TiXL's safe file I/O system
    /// </summary>
    public static class SafeFileIOExamples
    {
        #region Basic File Operations
        
        /// <summary>
        /// Example: Safe file writing vs unsafe pattern
        /// </summary>
        public static async Task DemonstrateSafeFileWriting()
        {
            Console.WriteLine("=== Safe File Writing Examples ===");
            
            // UNSAFE PATTERN (Old Way)
            Console.WriteLine("\n1. UNSAFE PATTERN (DO NOT USE):");
            Console.WriteLine("```csharp");
            Console.WriteLine("// This is DANGEROUS - no validation, no error handling");
            Console.WriteLine("var content = \"Hello World\";");
            Console.WriteLine("var path = userInput.Text; // Could be anything!");
            Console.WriteLine("await File.WriteAllTextAsync(path, content); // BOMBs if path invalid!");
            Console.WriteLine("```");
            
            // SAFE PATTERN (New Way)
            Console.WriteLine("\n2. SAFE PATTERN (RECOMMENDED):");
            
            var safeFileIO = SafeFileIO.Instance;
            var testContent = "Hello, this is a safe file operation!";
            var testPath = Path.Combine(Path.GetTempPath(), "tixl_safe_test.txt");
            
            Console.WriteLine("```csharp");
            Console.WriteLine("// Step 1: Validate the path");
            var pathValidation = safeFileIO.ValidateWritePath(testPath);
            Console.WriteLine($"Path Validation: {(pathValidation.IsValid ? "Valid" : $"Invalid: {pathValidation.ErrorMessage}")}");
            
            Console.WriteLine("\n// Step 2: Use safe write with atomic operations");
            var writeResult = await safeFileIO.SafeWriteAsync(testPath, testContent, createBackup: true);
            Console.WriteLine($"Write Result: {(writeResult.IsSuccess ? $"Success ({writeResult.BytesWritten} bytes)" : $"Failed: {writeResult.ErrorMessage}")}");
            
            Console.WriteLine("\n// Step 3: Verify read back");
            if (writeResult.IsSuccess)
            {
                var readResult = await safeFileIO.SafeReadAllTextAsync(testPath);
                Console.WriteLine($"Read Result: {(readResult.IsSuccess ? $"Success: '{readResult.Data}'" : $"Failed: {readResult.ErrorMessage}")}");
            }
            Console.WriteLine("```");
            
            // Cleanup
            try { File.Delete(testPath); } catch { }
        }
        
        /// <summary>
        /// Example: Directory traversal attack prevention
        /// </summary>
        public static async Task DemonstrateSecurityValidation()
        {
            Console.WriteLine("\n=== Security Validation Examples ===");
            
            var safeFileIO = SafeFileIO.Instance;
            var dangerousPaths = new[]
            {
                "../../../etc/passwd",           // Unix style traversal
                "..\\..\\Windows\\System32",     // Windows style traversal
                "file.txt%2e%2e%2f%2e%2f",      // URL encoded traversal
                "CON.txt",                       // Reserved name
                "",                              // Empty path
                "   file.txt   ",                // Path with spaces
                "/very/long/path/that/exceeds/260/characters/" + new string('a', 300) // Too long
            };
            
            Console.WriteLine("\nTesting dangerous paths:");
            foreach (var dangerousPath in dangerousPaths)
            {
                var validation = safeFileIO.ValidateWritePath(dangerousPath);
                Console.WriteLine($"  Path: '{dangerousPath}'");
                Console.WriteLine($"  Result: {(validation.IsValid ? "PASSED (unexpected!)" : $"BLOCKED: {validation.ErrorMessage}")}");
                Console.WriteLine();
            }
        }
        
        #endregion
        
        #region Screenshot Operations
        
        /// <summary>
        /// Example: Safe screenshot saving with validation
        /// </summary>
        public static async Task DemonstrateSafeScreenshotOperations()
        {
            Console.WriteLine("\n=== Safe Screenshot Operations ===");
            
            // Create a test screenshot
            using var testScreenshot = CreateTestScreenshot(800, 600);
            var screenshotPath = Path.Combine(Path.GetTempPath(), "test_screenshot.png");
            
            Console.WriteLine("1. Creating test screenshot (800x600)...");
            
            // SAFE SCREENSHOT SAVE
            Console.WriteLine("\n2. SAFE SCREENSHOT SAVE:");
            Console.WriteLine("```csharp");
            Console.WriteLine("var saveResult = await ScreenshotIOSafety.SaveScreenshotAsync(");
            Console.WriteLine("    screenshot, screenshotPath, ImageFormat.Png, quality: 95);");
            Console.WriteLine("```");
            
            var saveResult = await ScreenshotIOSafety.SaveScreenshotAsync(testScreenshot, screenshotPath);
            Console.WriteLine($"Save Result: {(saveResult.IsSuccess ? $"Success: {saveResult.BytesWritten} bytes" : $"Failed: {saveResult.ErrorMessage}")}");
            
            // SAFE SCREENSHOT LOAD
            if (saveResult.IsSuccess)
            {
                Console.WriteLine("\n3. SAFE SCREENSHOT LOAD:");
                Console.WriteLine("```csharp");
                Console.WriteLine("var loadResult = await ScreenshotIOSafety.LoadScreenshotAsync(screenshotPath);");
                Console.WriteLine("```");
                
                var loadResult = await ScreenshotIOSafety.LoadScreenshotAsync(screenshotPath);
                Console.WriteLine($"Load Result: {(loadResult.IsSuccess ? $"Success: {loadResult.Screenshot.Width}x{loadResult.Screenshot.Height}" : $"Failed: {loadResult.ErrorMessage}")}");
                
                // SAFE THUMBNAIL CREATION
                if (loadResult.IsSuccess)
                {
                    Console.WriteLine("\n4. SAFE THUMBNAIL CREATION:");
                    var thumbnailPath = Path.Combine(Path.GetTempPath(), "test_thumbnail.png");
                    Console.WriteLine("```csharp");
                    Console.WriteLine("var thumbResult = await ScreenshotIOSafety.CreateThumbnailAsync(");
                    Console.WriteLine("    screenshotPath, thumbnailPath, maxWidth: 200, maxHeight: 200);");
                    Console.WriteLine("```");
                    
                    var thumbResult = await ScreenshotIOSafety.CreateThumbnailAsync(screenshotPath, thumbnailPath, 200, 200);
                    Console.WriteLine($"Thumbnail Result: {(thumbResult.IsSuccess ? $"Success: {thumbResult.Width}x{thumbResult.Height}" : $"Failed: {thumbResult.ErrorMessage}")}");
                    
                    try { File.Delete(thumbnailPath); } catch { }
                    loadResult.Screenshot.Dispose();
                }
            }
            
            // Cleanup
            try { File.Delete(screenshotPath); } catch { }
        }
        
        #endregion
        
        #region Serialization Operations
        
        /// <summary>
        /// Example: Safe JSON serialization with rollback
        /// </summary>
        public static async Task DemonstrateSafeSerialization()
        {
            Console.WriteLine("\n=== Safe Serialization Operations ===");
            
            // Create test data
            var testData = new ProjectData
            {
                Metadata = new ProjectMetadata
                {
                    Name = "Test Project",
                    Id = Guid.NewGuid().ToString(),
                    Version = "1.0.0",
                    Description = "A test project for safe serialization",
                    CreatedUtc = DateTime.UtcNow,
                    LastModifiedUtc = DateTime.UtcNow
                },
                Version = "1.0",
                SchemaVersion = 1,
                CreatedUtc = DateTime.UtcNow,
                LastModifiedUtc = DateTime.UtcNow,
                Settings = new Dictionary<string, object>
                {
                    { "Theme", "Dark" },
                    { "AutoSave", true },
                    { "MaxUndoSteps", 50 }
                }
            };
            
            var projectPath = Path.Combine(Path.GetTempPath(), "test_project.json");
            
            Console.WriteLine("1. SAFE JSON SERIALIZATION:");
            Console.WriteLine("```csharp");
            Console.WriteLine("var serializationResult = await SafeSerialization.SafeSerializeToJsonAsync(");
            Console.WriteLine("    testData, projectPath, prettyPrint: true, createBackup: true);");
            Console.WriteLine("```");
            
            var serializationResult = await SafeSerialization.SafeSerializeToJsonAsync(testData, projectPath, true, true);
            Console.WriteLine($"Serialization Result: {(serializationResult.IsSuccess ? $"Success: {serializationResult.BytesWritten} bytes in {serializationResult.Duration.TotalMilliseconds:F1}ms" : $"Failed: {serializationResult.ErrorMessage}")}");
            
            // SAFE JSON DESERIALIZATION
            if (serializationResult.IsSuccess)
            {
                Console.WriteLine("\n2. SAFE JSON DESERIALIZATION:");
                Console.WriteLine("```csharp");
                Console.WriteLine("var deserializationResult = await SafeSerialization.SafeDeserializeFromJsonAsync<ProjectData>(projectPath);");
                Console.WriteLine("```");
                
                var deserializationResult = await SafeSerialization.SafeDeserializeFromJsonAsync<ProjectData>(projectPath);
                Console.WriteLine($"Deserialization Result: {(deserializationResult.IsSuccess ? $"Success: {deserializationResult.Data.Metadata.Name}" : $"Failed: {deserializationResult.ErrorMessage}")}");
                
                // CREATE ROLLBACK POINT
                Console.WriteLine("\n3. CREATE ROLLBACK POINT:");
                Console.WriteLine("```csharp");
                Console.WriteLine("var rollbackResult = await SafeSerialization.CreateRollbackPointAsync(projectPath);");
                Console.WriteLine("```");
                
                var rollbackResult = await SafeSerialization.CreateRollbackPointAsync(projectPath);
                Console.WriteLine($"Rollback Creation: {(rollbackResult.IsSuccess ? $"Success: {rollbackResult.Path}" : $"Failed: {rollbackResult.ErrorMessage}")}");
                
                // Simulate corruption
                if (rollbackResult.IsSuccess)
                {
                    Console.WriteLine("\n4. SIMULATING FILE CORRUPTION...");
                    await File.WriteAllTextAsync(projectPath, "{ invalid json content }");
                    
                    Console.WriteLine("\n5. RECOVERING FROM ROLLBACK:");
                    Console.WriteLine("```csharp");
                    Console.WriteLine("var recoveryResult = await SafeSerialization.RecoverFromErrorAsync(projectPath);");
                    Console.WriteLine("```");
                    
                    var recoveryResult = await SafeSerialization.RecoverFromErrorAsync(projectPath);
                    Console.WriteLine($"Recovery Result: {(recoveryResult.IsSuccess ? $"Success: Data restored" : $"Failed: {recoveryResult.ErrorMessage}")}");
                }
            }
            
            // Cleanup
            try { File.Delete(projectPath); } catch { }
            try 
            {
                var backupsDir = Path.Combine(Path.GetTempPath(), "backups");
                if (Directory.Exists(backupsDir))
                    Directory.Delete(backupsDir, true);
            } catch { }
        }
        
        #endregion
        
        #region Project Management
        
        /// <summary>
        /// Example: Safe project file management
        /// </summary>
        public static async Task DemonstrateSafeProjectManagement()
        {
            Console.WriteLine("\n=== Safe Project Management ===");
            
            // Create test project metadata
            var metadata = new ProjectMetadata
            {
                Name = "Safe I/O Demo Project",
                Id = Guid.NewGuid().ToString(),
                Version = "1.0.0",
                Description = "Demonstrating safe project file I/O operations",
                CreatedUtc = DateTime.UtcNow,
                LastModifiedUtc = DateTime.UtcNow,
                Tags = new List<string> { "demo", "safe-io", "tixl" },
                Properties = new Dictionary<string, string>
                {
                    { "Author", "TiXL Developer" },
                    { "LastOpened", DateTime.UtcNow.ToString("O") }
                }
            };
            
            var projectData = new ProjectData
            {
                Metadata = metadata,
                Version = "1.0",
                SchemaVersion = 1,
                CreatedUtc = DateTime.UtcNow,
                LastModifiedUtc = DateTime.UtcNow,
                Settings = new Dictionary<string, object>
                {
                    { "AutoSave", true },
                    { "BackupInterval", 300 },
                    { "MaxBackups", 10 }
                },
                Dependencies = new List<string> { "Core", "Graphics", "UI" }
            };
            
            var projectPath = Path.Combine(Path.GetTempPath(), "demo_project.tixlproject");
            
            // CREATE PROJECT
            Console.WriteLine("1. CREATE PROJECT:");
            Console.WriteLine("```csharp");
            Console.WriteLine("var createResult = await ProjectFileIOSafety.CreateProjectAsync(");
            Console.WriteLine("    metadata, projectPath, createBackup: true);");
            Console.WriteLine("```");
            
            var createResult = await ProjectFileIOSafety.CreateProjectAsync(metadata, projectPath, true);
            Console.WriteLine($"Create Result: {(createResult.IsSuccess ? $"Success: {createResult.ProjectPath}" : $"Failed: {createResult.ErrorMessage}")}");
            
            // LOAD PROJECT
            if (createResult.IsSuccess)
            {
                Console.WriteLine("\n2. LOAD PROJECT:");
                Console.WriteLine("```csharp");
                Console.WriteLine("var loadResult = await ProjectFileIOSafety.LoadProjectAsync(projectPath);");
                Console.WriteLine("```");
                
                var loadResult = await ProjectFileIOSafety.LoadProjectAsync(projectPath);
                Console.WriteLine($"Load Result: {(loadResult.IsSuccess ? $"Success: {loadResult.Metadata.Name}" : $"Failed: {loadResult.ErrorMessage}")}");
                
                // SAVE PROJECT
                Console.WriteLine("\n3. SAVE PROJECT:");
                Console.WriteLine("```csharp");
                Console.WriteLine("var saveResult = await ProjectFileIOSafety.SaveProjectAsync(");
                Console.WriteLine("    metadata, projectData, projectPath, createBackup: true);");
                Console.WriteLine("```");
                
                // Modify the project data
                projectData.Settings["AutoSave"] = false;
                projectData.Settings["BackupInterval"] = 600;
                metadata.LastModifiedUtc = DateTime.UtcNow;
                
                var saveResult = await ProjectFileIOSafety.SaveProjectAsync(metadata, projectData, projectPath, true);
                Console.WriteLine($"Save Result: {(saveResult.IsSuccess ? $"Success: Updated" : $"Failed: {saveResult.ErrorMessage}")}");
                
                // CREATE BACKUP
                Console.WriteLine("\n4. CREATE MANUAL BACKUP:");
                Console.WriteLine("```csharp");
                Console.WriteLine("var backupResult = await ProjectFileIOSafety.CreateBackupAsync(projectPath);");
                Console.WriteLine("```");
                
                var backupResult = await ProjectFileIOSafety.CreateBackupAsync(projectPath);
                Console.WriteLine($"Backup Result: {(backupResult.IsSuccess ? $"Success: {Path.GetFileName(backupResult.BackupPath)}" : $"Failed: {backupResult.ErrorMessage}")}");
            }
            
            // CLEANUP
            try 
            {
                File.Delete(projectPath);
                var backupsDir = Path.Combine(Path.GetTempPath(), "backups");
                if (Directory.Exists(backupsDir))
                    Directory.Delete(backupsDir, true);
            } catch { }
        }
        
        #endregion
        
        #region Monitoring and Statistics
        
        /// <summary>
        /// Example: I/O operation monitoring and statistics
        /// </summary>
        public static async Task DemonstrateMonitoring()
        {
            Console.WriteLine("\n=== I/O Operation Monitoring ===");
            
            var safeFileIO = SafeFileIO.Instance;
            
            // Perform some operations
            var testFiles = new[]
            {
                Path.Combine(Path.GetTempPath(), "monitor_test1.txt"),
                Path.Combine(Path.GetTempPath(), "monitor_test2.txt"),
                Path.Combine(Path.GetTempPath(), "monitor_test3.json")
            };
            
            Console.WriteLine("Performing test operations...");
            
            for (int i = 0; i < testFiles.Length; i++)
            {
                var content = $"Test content {i + 1}: {DateTime.Now}";
                await safeFileIO.SafeWriteAsync(testFiles[i], content);
                
                var readResult = await safeFileIO.SafeReadAllTextAsync(testFiles[i]);
                Console.WriteLine($"Operation {i + 1}: {(readResult.IsSuccess ? "Success" : "Failed")}");
            }
            
            // GET STATISTICS
            Console.WriteLine("\nI/O STATISTICS:");
            Console.WriteLine("```csharp");
            Console.WriteLine("var stats = safeFileIO.GetStatistics();");
            Console.WriteLine("```");
            
            var stats = safeFileIO.GetStatistics();
            Console.WriteLine($"Total Operations: {stats.TotalOperations}");
            Console.WriteLine($"Successful: {stats.SuccessfulOperations}");
            Console.WriteLine($"Failed: {stats.FailedOperations}");
            Console.WriteLine($"Average Duration: {stats.AverageOperationTime.TotalMilliseconds:F2}ms");
            Console.WriteLine($"Total Bytes Read: {stats.TotalBytesRead:N0}");
            Console.WriteLine($"Total Bytes Written: {stats.TotalBytesWritten:N0}");
            
            // GET RECENT HISTORY
            Console.WriteLine("\nRECENT OPERATIONS (last 5):");
            Console.WriteLine("```csharp");
            Console.WriteLine("var history = safeFileIO.GetOperationHistory(5);");
            Console.WriteLine("```");
            
            var history = safeFileIO.GetOperationHistory(5);
            foreach (var operation in history.Take(5))
            {
                Console.WriteLine($"  {operation.OperationName}: {(operation.IsSuccess ? "Success" : $"Failed: {operation.ErrorMessage}")} ({operation.Duration.TotalMilliseconds:F1}ms)");
            }
            
            // CLEANUP
            foreach (var testFile in testFiles)
            {
                try { File.Delete(testFile); } catch { }
            }
        }
        
        #endregion
        
        #region Error Handling Examples
        
        /// <summary>
        /// Example: Comprehensive error handling
        /// </summary>
        public static async Task DemonstrateErrorHandling()
        {
            Console.WriteLine("\n=== Error Handling Examples ===");
            
            var safeFileIO = SafeFileIO.Instance;
            var errorScenarios = new[]
            {
                new { Path = "C:\\Restricted\\file.txt", Description = "Permission denied path" },
                new { Path = "", Description = "Empty path" },
                new { Path = "../../../secret.txt", Description = "Directory traversal attempt" },
                new { Path = "CON.txt", Description = "Reserved name" },
                new { Path = "C:\\fake\\path\\that\\doesnt\\exist\\file.txt", Description = "Non-existent directory" }
            };
            
            foreach (var scenario in errorScenarios)
            {
                Console.WriteLine($"\nScenario: {scenario.Description}");
                Console.WriteLine($"Path: '{scenario.Path}'");
                
                // Try validation
                var validation = safeFileIO.ValidateWritePath(scenario.Path);
                Console.WriteLine($"Validation: {(validation.IsValid ? "Valid" : $"Invalid: {validation.ErrorMessage}")}");
                
                // Try operation if validation somehow passes
                if (validation.IsValid)
                {
                    var writeResult = await safeFileIO.SafeWriteAsync(scenario.Path, "test content");
                    Console.WriteLine($"Operation: {(writeResult.IsSuccess ? "Success" : $"Failed: {writeResult.ErrorMessage}")}");
                }
            }
        }
        
        #endregion
        
        #region Performance Examples
        
        /// <summary>
        /// Example: Performance comparison
        /// </summary>
        public static async Task DemonstratePerformance()
        {
            Console.WriteLine("\n=== Performance Comparison ===");
            
            var safeFileIO = SafeFileIO.Instance;
            var testPath = Path.Combine(Path.GetTempPath(), "performance_test.dat");
            var largeContent = new string('A', 1024 * 1024); // 1MB
            
            Console.WriteLine("Testing performance with 1MB content...");
            
            // SAFE OPERATION
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var safeResult = await safeFileIO.SafeWriteAsync(testPath, largeContent);
            sw.Stop();
            var safeTime = sw.ElapsedMilliseconds;
            
            Console.WriteLine($"Safe Write: {safeTime}ms");
            
            if (safeResult.IsSuccess)
            {
                sw.Restart();
                var safeRead = await safeFileIO.SafeReadAllTextAsync(testPath);
                sw.Stop();
                var safeReadTime = sw.ElapsedMilliseconds;
                Console.WriteLine($"Safe Read: {safeReadTime}ms");
            }
            
            // Cleanup
            try { File.Delete(testPath); } catch { }
        }
        
        #endregion
        
        #region Helper Methods
        
        private static Image CreateTestScreenshot(int width, int height)
        {
            var bitmap = new Bitmap(width, height);
            using var graphics = Graphics.FromImage(bitmap);
            
            // Clear with a gradient
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                new Rectangle(0, 0, width, height),
                Color.Blue, Color.Cyan, 45f);
            graphics.FillRectangle(brush, 0, 0, width, height);
            
            // Add some text
            using var font = new Font("Arial", 24, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.White);
            graphics.DrawString("TiXL Safe I/O Test", font, textBrush, 50, 50);
            
            // Add timestamp
            using var smallFont = new Font("Arial", 12);
            graphics.DrawString($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", smallFont, textBrush, 50, 90);
            
            return bitmap;
        }
        
        #endregion
        
        #region Demo Runner
        
        /// <summary>
        /// Runs all safe I/O examples
        /// </summary>
        public static async Task RunAllExamples()
        {
            Console.WriteLine("======================================================");
            Console.WriteLine("TiXL Safe File I/O Examples");
            Console.WriteLine("Demonstrating comprehensive file I/O safety features");
            Console.WriteLine("======================================================");
            
            try
            {
                await DemonstrateSafeFileWriting();
                await DemonstrateSecurityValidation();
                await DemonstrateSafeScreenshotOperations();
                await DemonstrateSafeSerialization();
                await DemonstrateSafeProjectManagement();
                await DemonstrateMonitoring();
                await DemonstrateErrorHandling();
                await DemonstratePerformance();
                
                Console.WriteLine("\n======================================================");
                Console.WriteLine("All examples completed successfully!");
                Console.WriteLine("======================================================");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nExample execution failed: {ex.Message}");
            }
        }
        
        #endregion
    }
}