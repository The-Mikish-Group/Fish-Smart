using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using System.Text.Json;

namespace Members.Controllers
{
    [Authorize(Roles = "Admin")]
    public class GalleryOptimizationController : Controller
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<GalleryOptimizationController> _logger;
        
        // Match the settings from ImageController
        private const int MaxGalleryImageWidth = 1200;
        private const int ThumbnailWidth = 400;
        private const int ThumbnailHeight = 300;
        
        // Supported image extensions
        private readonly string[] _supportedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };

        public GalleryOptimizationController(IWebHostEnvironment webHostEnvironment, ILogger<GalleryOptimizationController> logger)
        {
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
        }

        // GET: Admin interface for gallery optimization
        public IActionResult Index()
        {
            var galleriesPath = Path.Combine(_webHostEnvironment.WebRootPath, "Galleries");
            
            if (!Directory.Exists(galleriesPath))
            {
                ViewBag.NoGalleries = true;
                return View();
            }

            var galleries = Directory.GetDirectories(galleriesPath)
                .Select(dir => new GalleryOptimizationInfo
                {
                    Name = new DirectoryInfo(dir).Name,
                    Path = dir,
                    ImageCount = GetImageFiles(dir).Count(),
                    TotalSizeMB = GetDirectorySizeMB(dir),
                    HasLargeImages = HasOversizedImages(dir)
                })
                .OrderBy(g => g.Name)
                .ToList();

            return View(galleries);
        }

        // POST: Optimize all galleries
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OptimizeAllGalleries()
        {
            try
            {
                var galleriesPath = Path.Combine(_webHostEnvironment.WebRootPath, "Galleries");
                
                if (!Directory.Exists(galleriesPath))
                {
                    TempData["ErrorMessage"] = "No galleries directory found.";
                    return RedirectToAction(nameof(Index));
                }

                var optimizationResult = await OptimizeAllGalleriesInternal();
                
                if (optimizationResult.ProcessedImages > 0)
                {
                    TempData["SuccessMessage"] = $"Optimization complete! Processed {optimizationResult.ProcessedImages} images across {optimizationResult.ProcessedGalleries} galleries. " +
                        $"Saved approximately {optimizationResult.SpaceSavedMB:F1} MB of storage space.";
                }
                else
                {
                    TempData["SuccessMessage"] = $"All images are already optimized! No processing needed across {optimizationResult.ProcessedGalleries} galleries.";
                }
                
                if (optimizationResult.SkippedImages > 0)
                {
                    TempData["WarningMessage"] = $"{optimizationResult.SkippedImages} images were skipped due to processing errors.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during gallery optimization");
                TempData["ErrorMessage"] = $"Optimization failed: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Optimize specific gallery
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OptimizeGallery(string galleryName)
        {
            if (string.IsNullOrEmpty(galleryName))
            {
                TempData["ErrorMessage"] = "Gallery name is required.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var galleryPath = Path.Combine(_webHostEnvironment.WebRootPath, "Galleries", Path.GetFileName(galleryName));
                
                if (!Directory.Exists(galleryPath))
                {
                    TempData["ErrorMessage"] = $"Gallery '{galleryName}' not found.";
                    return RedirectToAction(nameof(Index));
                }

                var result = await OptimizeGalleryInternal(galleryPath);
                
                if (result.ProcessedImages > 0)
                {
                    TempData["SuccessMessage"] = $"Gallery '{galleryName}' optimized! Processed {result.ProcessedImages} images, " +
                        $"saved approximately {result.SpaceSavedMB:F1} MB.";
                }
                else
                {
                    TempData["SuccessMessage"] = $"Gallery '{galleryName}' is already optimized! All images are within size limits.";
                }
                
                if (result.SkippedImages > 0)
                {
                    TempData["WarningMessage"] = $"{result.SkippedImages} images in '{galleryName}' were skipped due to processing errors.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error optimizing gallery {GalleryName}", galleryName);
                TempData["ErrorMessage"] = $"Failed to optimize gallery '{galleryName}': {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Get optimization progress (for AJAX polling)
        [HttpGet]
        public IActionResult GetOptimizationProgress()
        {
            // In a real implementation, you'd store progress in a cache or database
            // For now, return a simple response
            return Json(new { isComplete = true, progress = 100 });
        }

        #region Private Helper Methods

        private async Task<OptimizationResult> OptimizeAllGalleriesInternal()
        {
            var galleriesPath = Path.Combine(_webHostEnvironment.WebRootPath, "Galleries");
            var galleries = Directory.GetDirectories(galleriesPath);
            
            var totalResult = new OptimizationResult();
            
            foreach (var galleryPath in galleries)
            {
                var galleryResult = await OptimizeGalleryInternal(galleryPath);
                totalResult.ProcessedGalleries++;
                totalResult.ProcessedImages += galleryResult.ProcessedImages;
                totalResult.SkippedImages += galleryResult.SkippedImages;
                totalResult.SpaceSavedMB += galleryResult.SpaceSavedMB;
            }
            
            return totalResult;
        }

        private async Task<OptimizationResult> OptimizeGalleryInternal(string galleryPath)
        {
            var result = new OptimizationResult();
            var imageFiles = GetImageFiles(galleryPath).ToList();
            
            foreach (var imagePath in imageFiles)
            {
                try
                {
                    // Check if image needs optimization first
                    using var imageToCheck = await Image.LoadAsync(imagePath);
                    if (imageToCheck.Width <= MaxGalleryImageWidth)
                    {
                        // Image is already optimized, skip but don't count as error
                        continue;
                    }
                    
                    var sizeBefore = new FileInfo(imagePath).Length;
                    
                    // Create backup filename
                    var backupPath = imagePath + ".backup";
                    
                    // Backup original if it doesn't exist
                    if (!System.IO.File.Exists(backupPath))
                    {
                        System.IO.File.Copy(imagePath, backupPath);
                    }
                    
                    // Optimize the image
                    var optimized = await OptimizeImage(imagePath);
                    
                    if (optimized)
                    {
                        var sizeAfter = new FileInfo(imagePath).Length;
                        var spaceSaved = (sizeBefore - sizeAfter) / (1024.0 * 1024.0); // MB
                        result.SpaceSavedMB += spaceSaved;
                        result.ProcessedImages++;
                        
                        // Regenerate thumbnail with new dimensions
                        await RegenerateThumbnail(imagePath);
                    }
                    else
                    {
                        result.SkippedImages++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to optimize image: {ImagePath}", imagePath);
                    result.SkippedImages++;
                }
            }
            
            return result;
        }

        private async Task<bool> OptimizeImage(string imagePath)
        {
            try
            {
                using var image = await Image.LoadAsync(imagePath);
                
                // Check if image needs resizing
                if (image.Width <= MaxGalleryImageWidth)
                {
                    return false; // No optimization needed
                }
                
                // Resize image
                var aspectRatio = (float)image.Height / image.Width;
                var newHeight = (int)(MaxGalleryImageWidth * aspectRatio);
                image.Mutate(x => x.Resize(MaxGalleryImageWidth, newHeight));
                
                // Save optimized image
                await image.SaveAsJpegAsync(imagePath, new JpegEncoder { Quality = 85 });
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error optimizing image {ImagePath}", imagePath);
                return false;
            }
        }

        private async Task RegenerateThumbnail(string imagePath)
        {
            try
            {
                var fileName = Path.GetFileName(imagePath);
                var directory = Path.GetDirectoryName(imagePath);
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                var extension = Path.GetExtension(fileName);
                
                var thumbnailPath = Path.Combine(directory!, $"{fileNameWithoutExtension}_thumb{extension}");
                
                using var image = await Image.LoadAsync(imagePath);
                
                // Resize to thumbnail dimensions with crop
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(ThumbnailWidth, ThumbnailHeight),
                    Mode = ResizeMode.Crop
                }));
                
                // Save thumbnail
                await image.SaveAsJpegAsync(thumbnailPath, new JpegEncoder { Quality = 85 });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to regenerate thumbnail for {ImagePath}", imagePath);
            }
        }

        private IEnumerable<string> GetImageFiles(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
                return Enumerable.Empty<string>();
            
            return Directory.GetFiles(directoryPath)
                .Where(f => !f.Contains("_thumb", StringComparison.OrdinalIgnoreCase) &&
                           !f.EndsWith(".backup", StringComparison.OrdinalIgnoreCase) &&
                           _supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));
        }

        private double GetDirectorySizeMB(string directoryPath)
        {
            try
            {
                var totalBytes = GetImageFiles(directoryPath)
                    .Sum(file => new FileInfo(file).Length);
                return totalBytes / (1024.0 * 1024.0);
            }
            catch
            {
                return 0;
            }
        }

        private bool HasOversizedImages(string directoryPath)
        {
            try
            {
                foreach (var imagePath in GetImageFiles(directoryPath))
                {
                    using var image = Image.Load(imagePath);
                    if (image.Width > MaxGalleryImageWidth)
                        return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }

    #region Helper Classes

    public class GalleryOptimizationInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public int ImageCount { get; set; }
        public double TotalSizeMB { get; set; }
        public bool HasLargeImages { get; set; }
    }

    public class OptimizationResult
    {
        public int ProcessedGalleries { get; set; }
        public int ProcessedImages { get; set; }
        public int SkippedImages { get; set; }
        public double SpaceSavedMB { get; set; }
    }

    #endregion
}