using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using TiXL.Core.IO;

namespace TiXL.Core.IO.Examples
{
    /// <summary>
    /// Quick start examples for TiXL Safe File I/O system
    /// Minimal code examples for common operations
    /// </summary>
    public static class QuickStartExamples
    {
        /// <summary>
        /// Example 1: Basic safe file write and read
        /// </summary>
        public static async Task BasicFileOperations()
        {
            Console.WriteLine("=== Basic File Operations ===");
            
            var safeFileIO = SafeFileIO.Instance;
            var filePath = "example_data.txt";
            var content = "Hello, Safe File I/O!";
            
            // Safe write with validation and backup
            var writeResult = await safeFileIO.SafeWriteAsync(filePath, content, createBackup: true);
            if (writeResult.IsSuccess)
            {
                Console.WriteLine($"✓ File written successfully: {writeResult.BytesWritten} bytes");
            }
            else
            {
                Console.WriteLine($"✗ Write failed: {writeResult.ErrorMessage}");
                return;
            }
            
            // Safe read
            var readResult = await safeFileIO.SafeReadAllTextAsync(filePath);
            if (readResult.IsSuccess)
            {
                Console.WriteLine($"✓ File read successfully: {readResult.Data}");
            }
            else
            {
                Console.WriteLine($"✗ Read failed: {readResult.ErrorMessage}");
            }
        }
        
        /// <summary>
        /// Example 2: Safe screenshot operations
        /// </summary>
        public static async Task ScreenshotOperations()
        {
            Console.WriteLine("\n=== Screenshot Operations ===");
            
            // Create a simple test screenshot
            using var screenshot = CreateSimpleScreenshot(400, 300);
            var screenshotPath = "test_screenshot.png";
            
            // Safe screenshot save with validation
            var saveResult = await ScreenshotIOSafety.SaveScreenshotAsync(screenshot, screenshotPath);
            if (saveResult.IsSuccess)
            {
                Console.WriteLine($"✓ Screenshot saved: {saveResult.BytesWritten} bytes");
                
                // Create thumbnail
                var thumbResult = await ScreenshotIOSafety.CreateThumbnailAsync(
                    screenshotPath, "test_thumbnail.png", 100, 100);
                
                if (thumbResult.IsSuccess)
                {
                    Console.WriteLine($"✓ Thumbnail created: {thumbResult.Width}x{thumbResult.Height}");
                }
            }
            else
            {
                Console.WriteLine($"✗ Screenshot save failed: {saveResult.ErrorMessage}");
            }
        }
        
        /// <summary>
        /// Example 3: JSON serialization with safety
        /// </summary>
        public static async Task JsonOperations()
        {
            Console.WriteLine("\n=== JSON Operations ===");
            
            var projectData = new
            {
                Name = "Safe I/O Demo",
                Version = "1.0",
                Created = DateTime.UtcNow,
                Settings = new { Theme = "Dark", AutoSave = true }
            };
            
            var jsonPath = "project_settings.json";
            
            // Safe JSON serialization
            var serializeResult = await SafeSerialization.SafeSerializeToJsonAsync(projectData, jsonPath, true, true);
            if (serializeResult.IsSuccess)
            {
                Console.WriteLine($"✓ JSON saved: {serializeResult.BytesWritten} bytes");
            }
            else
            {
                Console.WriteLine($"✗ JSON save failed: {serializeResult.ErrorMessage}");
            }
        }
        
        /// <summary>
        /// Example 4: Project file management
        /// </summary>
        public static async Task ProjectOperations()
        {
            Console.WriteLine("\n=== Project Operations ===");
            
            var metadata = new ProjectMetadata
            {
                Name = "Quick Start Project",
                Id = Guid.NewGuid().ToString(),
                Version = "1.0.0",
                CreatedUtc = DateTime.UtcNow,
                LastModifiedUtc = DateTime.UtcNow
            };
            
            var projectPath = "quick_start_project.tixlproject";
            
            // Create project
            var createResult = await ProjectFileIOSafety.CreateProjectAsync(metadata, projectPath);
            if (createResult.IsSuccess)
            {
                Console.WriteLine($"✓ Project created: {Path.GetFileName(projectPath)}");
                
                // Create backup
                var backupResult = await ProjectFileIOSafety.CreateBackupAsync(projectPath);
                if (backupResult.IsSuccess)
                {
                    Console.WriteLine($"✓ Backup created: {Path.GetFileName(backupResult.BackupPath)}");
                }
            }
            else
            {
                Console.WriteLine($"✗ Project creation failed: {createResult.ErrorMessage}");
            }
        }
        
        /// <summary>
        /// Example 5: Security validation
        /// </summary>
        public static void SecurityValidation()
        {
            Console.WriteLine("\n=== Security Validation ===");
            
            var safeFileIO = SafeFileIO.Instance;
            var dangerousPaths = new[]
            {
                "../../../etc/passwd",
                "..\\..\\windows\\system32",
                "CON.txt",
                ""
            };
            
            foreach (var path in dangerousPaths)
            {
                var validation = safeFileIO.ValidateWritePath(path);
                Console.WriteLine($"Path '{path}': {(validation.IsValid ? "✓ Valid" : $"✗ Invalid - {validation.ErrorMessage}")}");
            }
        }
        
        /// <summary>
        /// Example 6: Monitoring and statistics
        /// </summary>
        public static async Task Monitoring()
        {
            Console.WriteLine("\n=== Monitoring ===");
            
            var safeFileIO = SafeFileIO.Instance;
            
            // Perform some operations first
            await safeFileIO.SafeWriteAsync("monitor_test.txt", "test content");
            await safeFileIO.SafeReadAllTextAsync("monitor_test.txt");
            
            // Get statistics
            var stats = safeFileIO.GetStatistics();
            Console.WriteLine($"Total operations: {stats.TotalOperations}");
            Console.WriteLine($"Success rate: {stats.SuccessfulOperations * 100.0 / Math.Max(1, stats.TotalOperations):F1}%");
            Console.WriteLine($"Average time: {stats.AverageOperationTime.TotalMilliseconds:F2}ms");
        }
        
        /// <summary>
        /// Example 7: Error handling and recovery
        /// </summary>
        public static async Task ErrorHandling()
        {
            Console.WriteLine("\n=== Error Handling ===");
            
            var safeFileIO = SafeFileIO.Instance;
            
            // Test with invalid path
            var result = await safeFileIO.SafeWriteAsync("CON.txt", "test content");
            if (!result.IsSuccess)
            {
                Console.WriteLine($"✓ Properly handled invalid path: {result.ErrorMessage}");
            }
            
            // Test with non-existent file
            var readResult = await safeFileIO.SafeReadAllTextAsync("nonexistent.txt");
            if (!readResult.IsSuccess)
            {
                Console.WriteLine($"✓ Properly handled missing file: {readResult.ErrorMessage}");
            }
        }
        
        /// <summary>
        /// Run all quick start examples
        /// </summary>
        public static async Task RunQuickStart()
        {
            Console.WriteLine("TiXL Safe File I/O - Quick Start Examples");
            Console.WriteLine("==========================================");
            
            try
            {
                await BasicFileOperations();
                await ScreenshotOperations();
                await JsonOperations();
                await ProjectOperations();
                SecurityValidation();
                await Monitoring();
                await ErrorHandling();
                
                Console.WriteLine("\n✓ All quick start examples completed!");
                Console.WriteLine("\nFor detailed examples, see SafeFileIOExamples.cs");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ Quick start failed: {ex.Message}");
            }
            finally
            {
                // Cleanup
                var filesToCleanup = new[]
                {
                    "example_data.txt", "test_screenshot.png", "test_thumbnail.png",
                    "project_settings.json", "quick_start_project.tixlproject",
                    "monitor_test.txt"
                };
                
                foreach (var file in filesToCleanup)
                {
                    try { File.Delete(file); } catch { }
                }
                
                try
                {
                    var backupsDir = "backups";
                    if (Directory.Exists(backupsDir))
                        Directory.Delete(backupsDir, true);
                }
                catch { }
            }
        }
        
        private static Image CreateSimpleScreenshot(int width, int height)
        {
            var bitmap = new Bitmap(width, height);
            using var graphics = Graphics.FromImage(bitmap);
            
            // Simple blue background
            using var brush = new SolidBrush(Color.SkyBlue);
            graphics.FillRectangle(brush, 0, 0, width, height);
            
            return bitmap;
        }
    }
}