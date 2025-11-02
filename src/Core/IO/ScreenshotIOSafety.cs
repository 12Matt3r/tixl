using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace TiXL.Core.IO
{
    /// <summary>
    /// Safe screenshot I/O operations with validation and optimization
    /// </summary>
    public static class ScreenshotIOSafety
    {
        private const int MAX_SCREENSHOT_SIZE = 100 * 1024 * 1024; // 100MB
        private const int MIN_SCREENSHOT_WIDTH = 100;
        private const int MIN_SCREENSHOT_HEIGHT = 100;
        private const int MAX_SCREENSHOT_WIDTH = 8192;
        private const int MAX_SCREENSHOT_HEIGHT = 8192;
        
        /// <summary>
        /// Safely saves a screenshot with validation and optimization
        /// </summary>
        public static async Task<ScreenshotSaveResult> SaveScreenshotAsync(Image screenshot, string filePath, ImageFormat format = null, int quality = 95)
        {
            try
            {
                // Validate input
                if (screenshot == null)
                {
                    return ScreenshotSaveResult.Failed("Screenshot cannot be null");
                }
                
                // Validate dimensions
                if (screenshot.Width < MIN_SCREENSHOT_WIDTH || screenshot.Height < MIN_SCREENSHOT_HEIGHT)
                {
                    return ScreenshotSaveResult.Failed($"Screenshot too small: {screenshot.Width}x{screenshot.Height}");
                }
                
                if (screenshot.Width > MAX_SCREENSHOT_WIDTH || screenshot.Height > MAX_SCREENSHOT_HEIGHT)
                {
                    return ScreenshotSaveResult.Failed($"Screenshot too large: {screenshot.Width}x{screenshot.Height}");
                }
                
                // Validate output path
                var safeFileIO = SafeFileIO.Instance;
                var pathValidation = safeFileIO.ValidateWritePath(filePath);
                if (!pathValidation.IsValid)
                {
                    return ScreenshotSaveResult.Failed($"Invalid path: {pathValidation.ErrorMessage}");
                }
                
                // Determine format and validate extension
                format ??= GetFormatFromPath(filePath);
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                
                if (!IsSupportedImageExtension(extension))
                {
                    return ScreenshotSaveResult.Failed($"Unsupported image format: {extension}");
                }
                
                // For formats that support quality (JPEG, PNG can use compression levels)
                var encoderQuality = format == ImageFormat.Jpeg ? quality : -1;
                
                using var operation = new System.Diagnostics.Stopwatch();
                operation.Start();
                
                // Save screenshot using appropriate method based on format
                var result = await SaveWithFormatAsync(screenshot, filePath, format, encoderQuality);
                
                operation.Stop();
                
                if (result.IsSuccess)
                {
                    // Verify the saved file
                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.Length > MAX_SCREENSHOT_SIZE)
                    {
                        // File too large, try with higher compression
                        if (format == ImageFormat.Jpeg)
                        {
                            return await SaveWithCompressionAsync(screenshot, filePath, Math.Max(50, quality - 20));
                        }
                        else if (format == ImageFormat.Png)
                        {
                            return await SaveWithCompressionAsync(screenshot, filePath, 6); // PNG compression level
                        }
                    }
                }
                
                return result;
            }
            catch (Exception ex)
            {
                return ScreenshotSaveResult.Failed($"Screenshot save failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Safely saves multiple screenshots in batch
        /// </summary>
        public static async Task<List<ScreenshotSaveResult>> SaveScreenshotsBatchAsync(
            Dictionary<Image, (string filePath, ImageFormat format)> screenshots, 
            int maxConcurrency = 3)
        {
            var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
            var results = new List<ScreenshotSaveResult>();
            
            var tasks = screenshots.Select(async kvp =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var result = await SaveScreenshotAsync(kvp.Key, kvp.Value.filePath, kvp.Value.format);
                    results.Add(result);
                }
                finally
                {
                    semaphore.Release();
                }
            });
            
            await Task.WhenAll(tasks);
            return results;
        }
        
        /// <summary>
        /// Safely loads a screenshot with validation
        /// </summary>
        public static async Task<ScreenshotLoadResult> LoadScreenshotAsync(string filePath)
        {
            try
            {
                // Validate file path
                var safeFileIO = SafeFileIO.Instance;
                var readResult = await safeFileIO.SafeReadAllBytesAsync(filePath);
                if (!readResult.IsSuccess)
                {
                    return ScreenshotLoadResult.Failed(readResult.ErrorMessage);
                }
                
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > MAX_SCREENSHOT_SIZE)
                {
                    return ScreenshotLoadResult.Failed("Screenshot file too large");
                }
                
                using var operation = new System.Diagnostics.Stopwatch();
                operation.Start();
                
                Image screenshot;
                
                try
                {
                    // Load image from bytes
                    using var stream = new MemoryStream(readResult.Data);
                    screenshot = Image.FromStream(stream);
                }
                catch (ArgumentException)
                {
                    return ScreenshotLoadResult.Failed("Invalid image format");
                }
                catch (OutOfMemoryException)
                {
                    return ScreenshotLoadResult.Failed("Image too large to load into memory");
                }
                
                // Validate loaded image
                if (screenshot.Width < MIN_SCREENSHOT_WIDTH || screenshot.Height < MIN_SCREENSHOT_HEIGHT)
                {
                    screenshot.Dispose();
                    return ScreenshotLoadResult.Failed($"Screenshot too small: {screenshot.Width}x{screenshot.Height}");
                }
                
                operation.Stop();
                
                return ScreenshotLoadResult.Success(screenshot);
            }
            catch (Exception ex)
            {
                return ScreenshotLoadResult.Failed($"Screenshot load failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Creates thumbnail with safe I/O
        /// </summary>
        public static async Task<ThumbnailResult> CreateThumbnailAsync(string sourcePath, string thumbnailPath, int maxWidth = 200, int maxHeight = 200, ImageFormat format = null)
        {
            try
            {
                var loadResult = await LoadScreenshotAsync(sourcePath);
                if (!loadResult.IsSuccess)
                {
                    return ThumbnailResult.Failed(loadResult.ErrorMessage);
                }
                
                using var original = loadResult.Screenshot;
                
                // Calculate thumbnail dimensions maintaining aspect ratio
                var scale = Math.Min((double)maxWidth / original.Width, (double)maxHeight / original.Height);
                var thumbWidth = Math.Max(1, (int)(original.Width * scale));
                var thumbHeight = Math.Max(1, (int)(original.Height * scale));
                
                // Create thumbnail
                using var thumbnail = new Bitmap(thumbWidth, thumbHeight);
                using var graphics = Graphics.FromImage(thumbnail);
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                
                graphics.DrawImage(original, 0, 0, thumbWidth, thumbHeight);
                
                // Save thumbnail
                format ??= GetFormatFromPath(thumbnailPath);
                var saveResult = await SaveScreenshotAsync(thumbnail, thumbnailPath, format);
                
                if (!saveResult.IsSuccess)
                {
                    return ThumbnailResult.Failed(saveResult.ErrorMessage);
                }
                
                return ThumbnailResult.Success(thumbnailPath, thumbWidth, thumbHeight);
            }
            catch (Exception ex)
            {
                return ThumbnailResult.Failed($"Thumbnail creation failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Validates screenshot file integrity
        /// </summary>
        public static async Task<ValidationResult> ValidateScreenshotFileAsync(string filePath)
        {
            try
            {
                var loadResult = await LoadScreenshotAsync(filePath);
                if (!loadResult.IsSuccess)
                {
                    return new ValidationResult { IsValid = false, ErrorMessage = loadResult.ErrorMessage };
                }
                
                loadResult.Screenshot.Dispose();
                return new ValidationResult { IsValid = true };
            }
            catch (Exception ex)
            {
                return new ValidationResult { IsValid = false, ErrorMessage = ex.Message };
            }
        }
        
        #region Private Methods
        
        private static async Task<ScreenshotSaveResult> SaveWithFormatAsync(Image screenshot, string filePath, ImageFormat format, int quality)
        {
            if (format == ImageFormat.Jpeg && quality > 0)
            {
                return await SaveWithCompressionAsync(screenshot, filePath, quality);
            }
            
            var safeFileIO = SafeFileIO.Instance;
            var content = ImageToBytes(screenshot, format);
            var result = await safeFileIO.SafeWriteAsync(filePath, content, createBackup: true);
            
            if (result.IsSuccess)
            {
                return ScreenshotSaveResult.Success(filePath, content.Length);
            }
            else
            {
                return ScreenshotSaveResult.Failed(result.ErrorMessage);
            }
        }
        
        private static async Task<ScreenshotSaveResult> SaveWithCompressionAsync(Image screenshot, string filePath, int quality)
        {
            try
            {
                var encoderParams = new EncoderParameters(1);
                encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);
                
                var jpegCodecInfo = GetEncoder(ImageFormat.Jpeg);
                
                var safeFileIO = SafeFileIO.Instance;
                var content = ImageToBytesWithEncoder(screenshot, jpegCodecInfo, encoderParams);
                var result = await safeFileIO.SafeWriteAsync(filePath, content, createBackup: true);
                
                if (result.IsSuccess)
                {
                    return ScreenshotSaveResult.Success(filePath, content.Length);
                }
                else
                {
                    return ScreenshotSaveResult.Failed(result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                return ScreenshotSaveResult.Failed($"Compressed save failed: {ex.Message}");
            }
        }
        
        private static byte[] ImageToBytes(Image image, ImageFormat format)
        {
            using var stream = new MemoryStream();
            image.Save(stream, format);
            return stream.ToArray();
        }
        
        private static byte[] ImageToBytesWithEncoder(Image image, ImageCodecInfo encoder, EncoderParameters encoderParams)
        {
            using var stream = new MemoryStream();
            image.Save(stream, encoder, encoderParams);
            return stream.ToArray();
        }
        
        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            var codecs = ImageCodecInfo.GetImageEncoders();
            foreach (var codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                    return codec;
            }
            return codecs.First(); // fallback
        }
        
        private static ImageFormat GetFormatFromPath(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => ImageFormat.Jpeg,
                ".png" => ImageFormat.Png,
                ".bmp" => ImageFormat.Bmp,
                ".gif" => ImageFormat.Gif,
                ".tiff" => ImageFormat.Tiff,
                ".wmf" => ImageFormat.Wmf,
                ".emf" => ImageFormat.Emf,
                ".ico" => ImageFormat.Icon,
                _ => ImageFormat.Png // default
            };
        }
        
        private static bool IsSupportedImageExtension(string extension)
        {
            var supportedExtensions = new HashSet<string>
            {
                ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".ico"
            };
            return supportedExtensions.Contains(extension);
        }
        
        #endregion
    }
    
    #region Screenshot Results
    
    public class ScreenshotSaveResult
    {
        public bool IsSuccess { get; set; }
        public string FilePath { get; set; }
        public int BytesWritten { get; set; }
        public string ErrorMessage { get; set; }
        public TimeSpan Duration { get; set; }
        
        public static ScreenshotSaveResult Success(string filePath, int bytesWritten)
        {
            return new ScreenshotSaveResult { IsSuccess = true, FilePath = filePath, BytesWritten = bytesWritten };
        }
        
        public static ScreenshotSaveResult Failed(string error)
        {
            return new ScreenshotSaveResult { IsSuccess = false, ErrorMessage = error };
        }
    }
    
    public class ScreenshotLoadResult
    {
        public bool IsSuccess { get; set; }
        public Image Screenshot { get; set; }
        public string ErrorMessage { get; set; }
        public TimeSpan Duration { get; set; }
        
        public static ScreenshotLoadResult Success(Image screenshot)
        {
            return new ScreenshotLoadResult { IsSuccess = true, Screenshot = screenshot };
        }
        
        public static ScreenshotLoadResult Failed(string error)
        {
            return new ScreenshotLoadResult { IsSuccess = false, ErrorMessage = error };
        }
    }
    
    public class ThumbnailResult
    {
        public bool IsSuccess { get; set; }
        public string ThumbnailPath { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string ErrorMessage { get; set; }
        
        public static ThumbnailResult Success(string thumbnailPath, int width, int height)
        {
            return new ThumbnailResult { IsSuccess = true, ThumbnailPath = thumbnailPath, Width = width, Height = height };
        }
        
        public static ThumbnailResult Failed(string error)
        {
            return new ThumbnailResult { IsSuccess = false, ErrorMessage = error };
        }
    }
    
    #endregion
}