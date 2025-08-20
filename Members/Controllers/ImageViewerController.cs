using Members.Data;
using Members.Models;
using Members.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Members.Controllers
{
    [Authorize]
    public class ImageViewerController(
        ApplicationDbContext context, 
        UserManager<IdentityUser> userManager,
        IImageCompositionService imageCompositionService,
        ISegmentationService segmentationService,
        IProductionBackgroundRemovalService productionBackgroundRemovalService,
        IWebHostEnvironment environment,
        ILogger<ImageViewerController> logger) : Controller
    {
        private readonly ApplicationDbContext _context = context;
        private readonly UserManager<IdentityUser> _userManager = userManager;
        private readonly IImageCompositionService _imageCompositionService = imageCompositionService;
        private readonly ISegmentationService _segmentationService = segmentationService;
        private readonly IProductionBackgroundRemovalService _productionBackgroundRemovalService = productionBackgroundRemovalService;
        private readonly IWebHostEnvironment _environment = environment;
        private readonly ILogger<ImageViewerController> _logger = logger;

        // GET: ImageViewer/AlbumCover/5
        public async Task<IActionResult> AlbumCover(int id, string? returnUrl = null)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToAction("Login", "Account");

            var album = await _context.CatchAlbums
                .Include(a => a.AlbumCatches)
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (album == null) return NotFound();

            if (string.IsNullOrEmpty(album.CoverImageUrl))
            {
                TempData["Error"] = "This album doesn't have a cover image.";
                return RedirectToAction("Details", "CatchAlbum", new { id = album.Id });
            }

            var viewModel = new ImageViewerViewModel
            {
                ImageUrl = album.CoverImageUrl,
                ImageType = "AlbumCover",
                SourceId = album.Id,
                Title = $"{album.Name} - Cover Photo",
                Description = album.Description,
                ReturnUrl = returnUrl ?? Url.Action("Details", "CatchAlbum", new { id = album.Id }) ?? "/CatchAlbum",
                CanEdit = true, // User owns this album
                Metadata = new Dictionary<string, string>
                {
                    ["Album Name"] = album.Name,
                    ["Created"] = album.CreatedAt.ToString("MMM dd, yyyy"),
                    ["Catches"] = album.AlbumCatches.Count.ToString(),
                    ["Visibility"] = album.IsPublic ? "Public" : "Private"
                }
            };

            return View("ImageViewer", viewModel);
        }

        // GET: ImageViewer/CatchPhoto/5
        public async Task<IActionResult> CatchPhoto(int id, string? returnUrl = null)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToAction("Login", "Account");

            var catchItem = await _context.Catches
                .Include(c => c.Session)
                .Include(c => c.Species)
                .FirstOrDefaultAsync(c => c.Id == id && c.Session != null && c.Session.UserId == userId);

            if (catchItem == null) return NotFound();

            if (string.IsNullOrEmpty(catchItem.PhotoUrl))
            {
                TempData["Error"] = "This catch doesn't have a photo.";
                return RedirectToAction("Details", "FishingSession", new { id = catchItem.SessionId });
            }

            var viewModel = new ImageViewerViewModel
            {
                ImageUrl = catchItem.PhotoUrl,
                ImageType = "CatchPhoto", 
                SourceId = catchItem.Id,
                Title = $"{catchItem.Species?.CommonName ?? "Unknown Fish"} - {catchItem.Size}\"",
                Description = $"Caught during fishing session at {catchItem.Session?.LocationName}",
                ReturnUrl = returnUrl ?? Url.Action("Details", "FishingSession", new { id = catchItem.SessionId }) ?? "/FishingSession",
                CanEdit = true, // User owns this catch
                Metadata = new Dictionary<string, string>
                {
                    ["Species"] = catchItem.Species?.CommonName ?? "Unknown",
                    ["Size"] = $"{catchItem.Size}\"",
                    ["Weight"] = catchItem.Weight?.ToString("F1") + " lbs" ?? "Not recorded",
                    ["Caught"] = catchItem.CatchTime?.ToString("MMM dd, yyyy 'at' h:mm tt") ?? "Time not recorded",
                    ["Location"] = catchItem.Session?.LocationName ?? "Unknown location",
                    ["Water Type"] = catchItem.Session?.WaterType ?? "Unknown"
                }
            };

            return View("ImageViewer", viewModel);
        }

        // GET: ImageViewer/BuddyAlbumCover/5 (for viewing buddy's public albums)
        public async Task<IActionResult> BuddyAlbumCover(int id, string? returnUrl = null)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToAction("Login", "Account");

            var album = await _context.CatchAlbums
                .Include(a => a.AlbumCatches)
                .FirstOrDefaultAsync(a => a.Id == id && a.IsPublic);

            if (album == null) return NotFound();

            // Verify user is actually buddies with the album owner
            var buddyRelationship = await _context.FishingBuddies
                .FirstOrDefaultAsync(fb => 
                    ((fb.OwnerUserId == userId && fb.BuddyUserId == album.UserId) ||
                     (fb.OwnerUserId == album.UserId && fb.BuddyUserId == userId)) &&
                    fb.Status == "Accepted");

            if (buddyRelationship == null)
            {
                TempData["Error"] = "You can only view public albums from your fishing buddies.";
                return RedirectToAction("Index", "FishingBuddies");
            }

            if (string.IsNullOrEmpty(album.CoverImageUrl))
            {
                TempData["Error"] = "This album doesn't have a cover image.";
                return RedirectToAction("ViewBuddyProfile", "FishingBuddies", new { buddyUserId = album.UserId });
            }

            var viewModel = new ImageViewerViewModel
            {
                ImageUrl = album.CoverImageUrl,
                ImageType = "BuddyAlbumCover",
                SourceId = album.Id,
                Title = $"{album.Name} - Cover Photo",
                Description = album.Description,
                ReturnUrl = returnUrl ?? Url.Action("ViewBuddyProfile", "FishingBuddies", new { buddyUserId = album.UserId }) ?? "/FishingBuddies",
                CanEdit = false, // User doesn't own this album
                Metadata = new Dictionary<string, string>
                {
                    ["Album Name"] = album.Name,
                    ["Created"] = album.CreatedAt.ToString("MMM dd, yyyy"),
                    ["Catches"] = album.AlbumCatches.Count.ToString(),
                    ["Visibility"] = "Public"
                }
            };

            return View("ImageViewer", viewModel);
        }

        // POST: ImageViewer/DeleteImage
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteImage(string imageType, int sourceId, string returnUrl)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToAction("Login", "Account");

            try
            {
                switch (imageType)
                {
                    case "AlbumCover":
                        var album = await _context.CatchAlbums
                            .FirstOrDefaultAsync(a => a.Id == sourceId && a.UserId == userId);
                        if (album != null)
                        {
                            // Delete physical file
                            if (!string.IsNullOrEmpty(album.CoverImageUrl))
                            {
                                _ = DeletePhysicalFile(album.CoverImageUrl);
                            }

                            album.CoverImageUrl = null;
                            await _context.SaveChangesAsync();
                            TempData["Success"] = "Album cover photo deleted successfully.";
                        }
                        break;

                    case "CatchPhoto":
                        var catchItem = await _context.Catches
                            .Include(c => c.Session)
                            .FirstOrDefaultAsync(c => c.Id == sourceId && c.Session != null && c.Session.UserId == userId);
                        if (catchItem != null)
                        {
                            // Delete physical file
                            if (!string.IsNullOrEmpty(catchItem.PhotoUrl))
                            {
                                _ = DeletePhysicalFile(catchItem.PhotoUrl);
                            }

                            catchItem.PhotoUrl = null;
                            await _context.SaveChangesAsync();
                            TempData["Success"] = "Catch photo deleted successfully.";
                        }
                        break;

                    default:
                        TempData["Error"] = "Invalid image type.";
                        break;
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting image: {ex.Message}";
            }

            return Redirect(returnUrl);
        }

        // GET: ImageViewer/GetBackgrounds
        [HttpGet]
        public async Task<IActionResult> GetBackgrounds(string? category = null, string? waterType = null)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            // Check if user has premium access
            var userProfile = await _context.SmartCatchProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);
            var isPremiumUser = userProfile?.SubscriptionType == "Premium";

            var backgrounds = await _imageCompositionService.GetAvailableBackgroundsAsync(
                category, isPremiumUser, waterType);

            return Json(backgrounds);
        }

        // POST: ImageViewer/ReplaceBackground
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReplaceBackground([FromBody] ReplaceBackgroundRequest request)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            try
            {
                // Validate ownership of the image
                var hasAccess = await ValidateImageAccessAsync(request.ImageType, request.SourceId, userId);
                if (!hasAccess)
                {
                    return Json(new { success = false, message = "Access denied" });
                }

                // Get the background
                var background = await _context.Backgrounds.FindAsync(request.BackgroundId);
                if (background == null)
                {
                    return Json(new { success = false, message = "Background not found" });
                }

                // Check premium access
                if (background.IsPremium)
                {
                    var userProfile = await _context.SmartCatchProfiles
                        .FirstOrDefaultAsync(p => p.UserId == userId);
                    if (userProfile?.SubscriptionType != "Premium")
                    {
                        return Json(new { success = false, message = "Premium subscription required for this background" });
                    }
                }

                // Get the current image path
                var originalImagePath = await GetImagePathAsync(request.ImageType, request.SourceId);
                if (string.IsNullOrEmpty(originalImagePath))
                {
                    _logger.LogWarning("GetImagePathAsync returned null/empty for ImageType: {ImageType}, SourceId: {SourceId}", request.ImageType, request.SourceId);
                    
                    // Get the database URL for better error message
                    string? dbUrl = request.ImageType switch
                    {
                        "AlbumCover" => await _context.CatchAlbums.Where(a => a.Id == request.SourceId).Select(a => a.CoverImageUrl).FirstOrDefaultAsync(),
                        "CatchPhoto" => await _context.Catches.Where(c => c.Id == request.SourceId).Select(c => c.PhotoUrl).FirstOrDefaultAsync(),
                        _ => null
                    };
                    
                    if (string.IsNullOrEmpty(dbUrl))
                    {
                        return Json(new { success = false, message = $"No image URL found in database for {request.ImageType} ID {request.SourceId}" });
                    }
                    else
                    {
                        return Json(new { success = false, message = $"Source image file not found at any container location. Database URL: {dbUrl}. Tried paths: /app/wwwroot/Images/, /app/Images/, etc. Use Debug Paths for details." });
                    }
                }

                // Get background image path using container-compatible path resolution
                var backgroundImagePath = GetPhysicalPath(background.ImageUrl!);
                
                _logger.LogDebug("Background image resolution - BackgroundId: {BackgroundId}, ImageUrl: {ImageUrl}, PhysicalPath: {PhysicalPath}, Exists: {Exists}", 
                    background.Id, background.ImageUrl, backgroundImagePath, System.IO.File.Exists(backgroundImagePath));

                // Verify the physical file exists
                if (!System.IO.File.Exists(originalImagePath))
                {
                    _logger.LogWarning("Source image file does not exist at path: {Path}", originalImagePath);
                    return Json(new { success = false, message = $"Source image file not found at: {originalImagePath}" });
                }

                // Verify background file exists
                if (!System.IO.File.Exists(backgroundImagePath))
                {
                    _logger.LogWarning("Background image file does not exist at path: {Path}", backgroundImagePath);
                    return Json(new { success = false, message = $"Background image file not found at: {backgroundImagePath}" });
                }

                // Create backup of original image first
                var fileName = Path.GetFileNameWithoutExtension(originalImagePath);
                var extension = Path.GetExtension(originalImagePath);
                var backupFileName = $"{fileName}_original_backup{extension}";
                var backupPath = Path.Combine(Path.GetDirectoryName(originalImagePath)!, backupFileName);
                
                // Always create/update backup before processing
                try
                {
                    if (System.IO.File.Exists(originalImagePath))
                    {
                        System.IO.File.Copy(originalImagePath, backupPath, overwrite: true);
                        _logger.LogInformation("Created backup: {BackupPath}", backupPath);
                    }
                }
                catch (Exception backupEx)
                {
                    _logger.LogError(backupEx, "Failed to create backup");
                    return Json(new { success = false, message = "Failed to create backup before processing" });
                }

                // Choose processing method based on request
                bool useRemoveBg = request.RemovalMethod == "removebg";
                bool useClipdrop = request.RemovalMethod == "clipdrop";
                string tempOutputPath = "";
                bool processingSuccess = false;
                string processingMessage = "";

                if (useRemoveBg)
                {
                    // Use professional Remove.bg service
                    var result = await _productionBackgroundRemovalService.ProcessBackgroundRemovalAsync(userId!, originalImagePath);
                    var success = result.success;
                    var processedImage = result.processedImage;
                    var message = result.message;
                    
                    if (success && processedImage != null)
                    {
                        // Remove.bg gives us the subject with transparent background, now we need to composite it with new background
                        tempOutputPath = Path.Combine(Path.GetDirectoryName(originalImagePath)!, $"{fileName}_removebg_temp{extension}");
                        await System.IO.File.WriteAllBytesAsync(tempOutputPath, processedImage);
                        
                        // Now composite the transparent subject with the new background
                        var finalOutputPath = Path.Combine(Path.GetDirectoryName(originalImagePath)!, $"{fileName}_final_temp{extension}");
                        var (Success, Message) = await _imageCompositionService.CompositeTransparentImageWithBackgroundAsync(
                            tempOutputPath, backgroundImagePath, finalOutputPath);
                        
                        if (Success)
                        {
                            tempOutputPath = finalOutputPath;
                            processingSuccess = true;
                            processingMessage = $"Background replaced successfully using Remove.bg! {message}";
                        }
                        else
                        {
                            processingSuccess = false;
                            processingMessage = $"Remove.bg worked but compositing failed: {Message}";
                        }
                    }
                    else
                    {
                        processingSuccess = false;
                        processingMessage = message;
                    }
                }
                else if (useClipdrop)
                {
                    // Use Clipdrop service for testing
                    var clipdropService = HttpContext.RequestServices.GetRequiredService<ClipdropService>();
                    var clipdropResult = await clipdropService.RemoveBackgroundAsync(originalImagePath);
                    
                    if (clipdropResult.Success && clipdropResult.ProcessedImageBytes != null)
                    {
                        // Clipdrop gives us the subject with transparent background, now we need to composite it with new background
                        tempOutputPath = Path.Combine(Path.GetDirectoryName(originalImagePath)!, $"{fileName}_clipdrop_temp{extension}");
                        await System.IO.File.WriteAllBytesAsync(tempOutputPath, clipdropResult.ProcessedImageBytes);
                        
                        // Now composite the transparent subject with the new background
                        var finalOutputPath = Path.Combine(Path.GetDirectoryName(originalImagePath)!, $"{fileName}_final_temp{extension}");
                        var (Success, Message) = await _imageCompositionService.CompositeTransparentImageWithBackgroundAsync(
                            tempOutputPath, backgroundImagePath, finalOutputPath);
                        
                        if (Success)
                        {
                            tempOutputPath = finalOutputPath;
                            processingSuccess = true;
                            processingMessage = $"Background replaced successfully using Clipdrop! {clipdropResult.Message}";
                        }
                        else
                        {
                            processingSuccess = false;
                            processingMessage = $"Clipdrop worked but compositing failed: {Message}";
                        }
                    }
                    else
                    {
                        processingSuccess = false;
                        processingMessage = clipdropResult.Message;
                    }
                }
                else
                {
                    // Use existing AI/color-based method (free)
                    tempOutputPath = Path.Combine(Path.GetDirectoryName(originalImagePath)!, $"{fileName}_ai_temp{extension}");
                    var result = await _imageCompositionService.ReplaceBackgroundAsync(originalImagePath, backgroundImagePath, tempOutputPath);
                    var Success = result.Success;
                    var Message = result.Message;
                    
                    if (Success)
                    {
                        processingSuccess = true;
                        processingMessage = $"Background replaced successfully using AI method! {Message}";
                    }
                    else
                    {
                        processingSuccess = false;
                        processingMessage = Message;
                    }
                }

                if (processingSuccess)
                {
                    try
                    {
                        // Only replace original if processing was successful
                        if (System.IO.File.Exists(tempOutputPath))
                        {
                            System.IO.File.Copy(tempOutputPath, originalImagePath, overwrite: true);
                            System.IO.File.Delete(tempOutputPath); // Clean up temp file
                            
                            _logger.LogInformation("Successfully replaced background for {ImagePath}", originalImagePath);
                            
                            // No need to update database URL since we kept the same path
                            return Json(new { 
                                success = true, 
                                message = processingMessage,
                                newImageUrl = GetImageUrlFromPath(originalImagePath),
                                method = useRemoveBg ? "premium" : "ai"
                            });
                        }
                        else
                        {
                            return Json(new { success = false, message = "Background replacement failed - no output generated" });
                        }
                    }
                    catch (Exception replaceEx)
                    {
                        _logger.LogError(replaceEx, "Failed to replace original with processed image");
                        return Json(new { success = false, message = "Failed to save processed image" });
                    }
                }
                else
                {
                    // Clean up temp file if it exists
                    if (System.IO.File.Exists(tempOutputPath))
                    {
                        try { System.IO.File.Delete(tempOutputPath); } catch { }
                    }
                    return Json(new { success = false, message = processingMessage });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background replacement failed for ImageType: {ImageType}, SourceId: {SourceId}, BackgroundId: {BackgroundId}", 
                    request.ImageType, request.SourceId, request.BackgroundId);
                
                return Json(new { 
                    success = false, 
                    message = $"Error processing image: {ex.Message}",
                    errorType = ex.GetType().Name,
                    stackTrace = ex.StackTrace?[..Math.Min(500, ex.StackTrace.Length)]
                });
            }
        }

        // POST: ImageViewer/RestoreOriginalImage
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreOriginalImage([FromBody] RestoreImageRequest request)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            try
            {
                var hasAccess = await ValidateImageAccessAsync(request.ImageType, request.SourceId, userId);
                if (!hasAccess)
                {
                    return Json(new { success = false, message = "Access denied" });
                }

                // Get the current image path
                var currentImagePath = await GetImagePathAsync(request.ImageType, request.SourceId);
                if (string.IsNullOrEmpty(currentImagePath))
                {
                    return Json(new { success = false, message = "Current image not found" });
                }

                // Look for backup file
                var fileName = Path.GetFileNameWithoutExtension(currentImagePath);
                var extension = Path.GetExtension(currentImagePath);
                var backupFileName = $"{fileName}_original_backup{extension}";
                var backupPath = Path.Combine(Path.GetDirectoryName(currentImagePath)!, backupFileName);

                if (!System.IO.File.Exists(backupPath))
                {
                    return Json(new { 
                        success = false, 
                        message = "No backup found. Original image was not backed up before modification." 
                    });
                }

                // Restore from backup
                System.IO.File.Copy(backupPath, currentImagePath, true);

                return Json(new { 
                    success = true, 
                    message = "Original image restored successfully"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error restoring image: {ex.Message}" });
            }
        }

        // POST: ImageViewer/CheckBackupExists
        [HttpPost]
        public async Task<IActionResult> CheckBackupExists([FromBody] ValidateImageRequest request)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            try
            {
                var hasAccess = await ValidateImageAccessAsync(request.ImageType, request.SourceId, userId);
                if (!hasAccess)
                {
                    return Json(new { hasBackup = false });
                }

                var currentImagePath = await GetImagePathAsync(request.ImageType, request.SourceId);
                if (string.IsNullOrEmpty(currentImagePath))
                {
                    return Json(new { hasBackup = false });
                }

                // Look for backup file
                var fileName = Path.GetFileNameWithoutExtension(currentImagePath);
                var extension = Path.GetExtension(currentImagePath);
                var backupFileName = $"{fileName}_original_backup{extension}";
                var backupPath = Path.Combine(Path.GetDirectoryName(currentImagePath)!, backupFileName);

                return Json(new { hasBackup = System.IO.File.Exists(backupPath) });
            }
            catch
            {
                return Json(new { hasBackup = false });
            }
        }

        // POST: ImageViewer/ValidateImage
        [HttpPost]
        public async Task<IActionResult> ValidateImage([FromBody] ValidateImageRequest request)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            try
            {
                var hasAccess = await ValidateImageAccessAsync(request.ImageType, request.SourceId, userId);
                if (!hasAccess)
                {
                    return Json(new { success = false, message = "Access denied" });
                }

                var imagePath = await GetImagePathAsync(request.ImageType, request.SourceId);
                if (string.IsNullOrEmpty(imagePath))
                {
                    return Json(new { success = false, message = "Image not found" });
                }

                var validationResult = await _imageCompositionService.ValidateImageForProcessingAsync(imagePath);
                return Json(validationResult);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error validating image: {ex.Message}" });
            }
        }

        [HttpGet]
        public IActionResult CheckAIModelStatus()
        {
            try
            {
                var isAvailable = _segmentationService?.IsAISegmentationAvailable() ?? false;
                return Json(new { isAvailable });
            }
            catch (Exception)
            {
                return Json(new { isAvailable = false });
            }
        }

        // GET: ImageViewer/DownloadImage
        [HttpGet]
        public async Task<IActionResult> DownloadImage(string imageType, int sourceId)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            try
            {
                // Validate ownership
                var hasAccess = await ValidateImageAccessAsync(imageType, sourceId, userId);
                if (!hasAccess)
                {
                    return Forbid();
                }

                // Get image path
                var imagePath = await GetImagePathAsync(imageType, sourceId);
                if (string.IsNullOrEmpty(imagePath) || !System.IO.File.Exists(imagePath))
                {
                    return NotFound("Image not found");
                }

                // Check if user is premium
                var userProfile = await _context.SmartCatchProfiles
                    .FirstOrDefaultAsync(p => p.UserId == userId);
                var isPremiumUser = userProfile?.SubscriptionType == "Premium";

                if (isPremiumUser)
                {
                    // Premium users get the original image
                    var imageBytes = await System.IO.File.ReadAllBytesAsync(imagePath);
                    var fileName = $"{imageType}_{sourceId}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                    return File(imageBytes, "image/jpeg", fileName);
                }
                else
                {
                    // Non-premium users get watermarked image
                    var watermarkedImageBytes = await _imageCompositionService.AddWatermarkToImageAsync(imagePath);
                    if (watermarkedImageBytes == null)
                    {
                        return StatusCode(500, "Error processing image for download");
                    }
                    
                    var fileName = $"{imageType}_{sourceId}_watermarked_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                    return File(watermarkedImageBytes, "image/jpeg", fileName);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error downloading image: {ex.Message}");
            }
        }

        private static Task DeletePhysicalFile(string imageUrl)
        {
            try
            {
                var fileName = Path.GetFileName(imageUrl);
                var imagePath = imageUrl.Contains("/Albums/") ? "Albums" : "Catches";
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Images", imagePath, fileName);

                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                // Delete thumbnail if it exists
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                var extension = Path.GetExtension(fileName);
                var thumbnailName = $"{fileNameWithoutExt}_thumb{extension}";
                var thumbnailPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Images", imagePath, thumbnailName);

                if (System.IO.File.Exists(thumbnailPath))
                {
                    System.IO.File.Delete(thumbnailPath);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't fail the operation
                Console.WriteLine($"Error deleting physical file: {ex.Message}");
            }
            
            return Task.CompletedTask;
        }

        private async Task<bool> ValidateImageAccessAsync(string imageType, int sourceId, string userId)
        {
            return imageType switch
            {
                "AlbumCover" => await _context.CatchAlbums.AnyAsync(a => a.Id == sourceId && a.UserId == userId),
                "CatchPhoto" => await _context.Catches.AnyAsync(c => c.Id == sourceId && c.Session != null && c.Session.UserId == userId),
                _ => false
            };
        }

        private async Task<string?> GetImagePathAsync(string imageType, int sourceId)
        {
            string? imageUrl = imageType switch
            {
                "AlbumCover" => await _context.CatchAlbums
                    .Where(a => a.Id == sourceId)
                    .Select(a => a.CoverImageUrl)
                    .FirstOrDefaultAsync(),
                "CatchPhoto" => await _context.Catches
                    .Where(c => c.Id == sourceId)
                    .Select(c => c.PhotoUrl)
                    .FirstOrDefaultAsync(),
                _ => null
            };

            if (string.IsNullOrEmpty(imageUrl)) return null;

            // CRITICAL: Trim all whitespace from database URLs (database field might be CHAR with padding)
            imageUrl = imageUrl.Trim();
            var relativePath = imageUrl.TrimStart('/');
            
            // Use standard ASP.NET static file path resolution  
            var physicalPath = Path.Combine(_environment.WebRootPath, relativePath);
            
            _logger.LogDebug("Resolving image path - ImageType: {ImageType}, SourceId: {SourceId}, ImageUrl: {ImageUrl}, WebRootPath: {WebRootPath}, PhysicalPath: {PhysicalPath}, Exists: {Exists}", 
                imageType, sourceId, imageUrl, _environment.WebRootPath, physicalPath, System.IO.File.Exists(physicalPath));
            
            if (System.IO.File.Exists(physicalPath))
            {
                return physicalPath;
            }
            
            // Return null to indicate file not found
            _logger.LogWarning("Image file not found at: {PhysicalPath}", physicalPath);
            return null;
        }

        private string GetPhysicalPath(string imageUrl)
        {
            // CRITICAL: Trim all whitespace from database URLs (database field might be CHAR with padding)
            imageUrl = imageUrl.Trim();
            var relativePath = imageUrl.TrimStart('/');
            var physicalPath = Path.Combine(_environment.WebRootPath, relativePath);
            
            _logger.LogDebug("Resolving background path - ImageUrl: {ImageUrl}, WebRootPath: {WebRootPath}, PhysicalPath: {PhysicalPath}, Exists: {Exists}", 
                imageUrl, _environment.WebRootPath, physicalPath, System.IO.File.Exists(physicalPath));
            
            return physicalPath;
        }

        private async Task UpdateImageUrlAsync(string imageType, int sourceId, string newImageUrl)
        {
            switch (imageType)
            {
                case "AlbumCover":
                    var album = await _context.CatchAlbums.FindAsync(sourceId);
                    if (album != null)
                    {
                        album.CoverImageUrl = newImageUrl;
                        await _context.SaveChangesAsync();
                    }
                    break;

                case "CatchPhoto":
                    var catchItem = await _context.Catches.FindAsync(sourceId);
                    if (catchItem != null)
                    {
                        catchItem.PhotoUrl = newImageUrl;
                        await _context.SaveChangesAsync();
                    }
                    break;
            }
        }

        private static string GetImageUrlFromPath(string physicalPath)
        {
            try
            {
                var wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var relativePath = Path.GetRelativePath(wwwrootPath, physicalPath);
                return "/" + relativePath.Replace('\\', '/');
            }
            catch
            {
                return "";
            }
        }

        [HttpGet]
        public async Task<IActionResult> DebugPaths([FromQuery] string imageType, [FromQuery] int sourceId, [FromQuery] int backgroundId)
        {
            try
            {
                // Get source image info
                var sourceImagePath = await GetImagePathAsync(imageType, sourceId);
                
                // Get background info
                var background = await _context.Backgrounds.FindAsync(backgroundId);
                var backgroundImagePath = background?.ImageUrl != null 
                    ? GetPhysicalPath(background.ImageUrl)
                    : null;

                // Get source image URL for path testing
                string? sourceImageUrl = imageType switch
                {
                    "AlbumCover" => await _context.CatchAlbums
                        .Where(a => a.Id == sourceId)
                        .Select(a => a.CoverImageUrl)
                        .FirstOrDefaultAsync(),
                    "CatchPhoto" => await _context.Catches
                        .Where(c => c.Id == sourceId)
                        .Select(c => c.PhotoUrl)
                        .FirstOrDefaultAsync(),
                    _ => null
                };

                // Test all possible paths for source image
                var sourcePathTests = new List<object>();
                if (!string.IsNullOrEmpty(sourceImageUrl))
                {
                    var relativePath = sourceImageUrl.TrimStart('/');
                    var testPaths = new[]
                    {
                        Path.Combine(_environment.WebRootPath, relativePath),
                        Path.Combine(_environment.ContentRootPath, relativePath),
                        Path.Combine("/app", relativePath),
                        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath),
                        Path.Combine(Directory.GetCurrentDirectory(), relativePath)
                    };

                    foreach (var testPath in testPaths)
                    {
                        sourcePathTests.Add(new
                        {
                            Path = testPath,
                            Exists = System.IO.File.Exists(testPath),
                            DirectoryExists = Directory.Exists(Path.GetDirectoryName(testPath))
                        });
                    }
                }

                var debugInfo = new
                {
                    Environment = new
                    {
                        WebRootPath = _environment.WebRootPath,
                        ContentRootPath = _environment.ContentRootPath,
                        CurrentDirectory = Directory.GetCurrentDirectory()
                    },
                    SourceImage = new
                    {
                        ImageType = imageType,
                        SourceId = sourceId,
                        DatabaseUrl = sourceImageUrl,
                        ResolvedPath = sourceImagePath,
                        FinalExists = !string.IsNullOrEmpty(sourceImagePath) && System.IO.File.Exists(sourceImagePath),
                        AllPathTests = sourcePathTests
                    },
                    BackgroundImage = new
                    {
                        BackgroundId = backgroundId,
                        DatabaseUrl = background?.ImageUrl,
                        ResolvedPath = backgroundImagePath,
                        Exists = !string.IsNullOrEmpty(backgroundImagePath) && System.IO.File.Exists(backgroundImagePath),
                        DirectoryExists = !string.IsNullOrEmpty(backgroundImagePath) && Directory.Exists(Path.GetDirectoryName(backgroundImagePath))
                    },
                    DirectoryContents = new
                    {
                        WebRoot = Directory.Exists(_environment.WebRootPath) 
                            ? Directory.GetDirectories(_environment.WebRootPath).Take(10).ToArray()
                            : ["WebRoot not found"],
                        ContentRoot = Directory.Exists(_environment.ContentRootPath)
                            ? Directory.GetDirectories(_environment.ContentRootPath).Take(10).ToArray()
                            : ["ContentRoot not found"],
                        AppRoot = Directory.Exists("/app")
                            ? Directory.GetDirectories("/app").Take(10).ToArray()
                            : ["/app not found"]
                    }
                };

                return Json(debugInfo);
            }
            catch (Exception ex)
            {
                return Json(new { 
                    error = ex.Message, 
                    stackTrace = ex.StackTrace,
                    webRootPath = _environment.WebRootPath 
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> InspectDatabase([FromQuery] string imageType, [FromQuery] int sourceId)
        {
            try
            {
                var result = new
                {
                    Timestamp = DateTime.UtcNow,
                    Request = new { imageType, sourceId }
                };

                if (imageType == "CatchPhoto")
                {
                    var catchInfo = await _context.Catches
                        .Where(c => c.Id == sourceId)
                        .Select(c => new
                        {
                            c.Id,
                            c.PhotoUrl,
                            CommonName = c.Species != null ? c.Species.CommonName : "Unknown",
                            c.Size,
                            c.CatchTime,
                            c.SessionId,
                            UserId = c.Session != null ? c.Session.UserId : string.Empty
                        })
                        .FirstOrDefaultAsync();

                    return Json(new
                    {
                        result.Timestamp,
                        result.Request,
                        DatabaseRecord = catchInfo,
                        Found = catchInfo != null
                    });
                }
                else if (imageType == "AlbumCover")
                {
                    var albumInfo = await _context.CatchAlbums
                        .Where(a => a.Id == sourceId)
                        .Select(a => new
                        {
                            a.Id,
                            a.Name,
                            a.CoverImageUrl,
                            a.CreatedAt,
                            a.UserId,
                            CatchCount = a.AlbumCatches.Count
                        })
                        .FirstOrDefaultAsync();

                    return Json(new
                    {
                        result.Timestamp,
                        result.Request,
                        DatabaseRecord = albumInfo,
                        Found = albumInfo != null
                    });
                }

                return Json(new
                {
                    result.Timestamp,
                    result.Request,
                    Error = "Unsupported image type"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Error = ex.Message,
                    ex.StackTrace
                });
            }
        }
    }

    // View Model for Image Viewer
    public class ImageViewerViewModel
    {
        public string ImageUrl { get; set; } = string.Empty;
        public string ImageType { get; set; } = string.Empty; // "AlbumCover", "CatchPhoto", "BuddyAlbumCover"
        public int SourceId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string ReturnUrl { get; set; } = string.Empty;
        public bool CanEdit { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = [];
    }

    // Request models for AJAX endpoints
    public class ReplaceBackgroundRequest
    {
        public string ImageType { get; set; } = string.Empty;
        public int SourceId { get; set; }
        public int BackgroundId { get; set; }
        public string RemovalMethod { get; set; } = "ai"; // "ai" (current system), "removebg" (Remove.bg), or "clipdrop" (Clipdrop)
    }

    public class ValidateImageRequest
    {
        public string ImageType { get; set; } = string.Empty;
        public int SourceId { get; set; }
    }

    public class RestoreImageRequest
    {
        public string ImageType { get; set; } = string.Empty;
        public int SourceId { get; set; }
    }
}