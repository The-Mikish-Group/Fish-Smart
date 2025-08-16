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
    public class ImageViewerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IImageCompositionService _imageCompositionService;
        private readonly ISegmentationService _segmentationService;

        public ImageViewerController(
            ApplicationDbContext context, 
            UserManager<IdentityUser> userManager,
            IImageCompositionService imageCompositionService,
            ISegmentationService segmentationService)
        {
            _context = context;
            _userManager = userManager;
            _imageCompositionService = imageCompositionService;
            _segmentationService = segmentationService;
        }

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
                    return Json(new { success = false, message = "Original image not found" });
                }

                // Create backup of original image first
                var fileName = Path.GetFileNameWithoutExtension(originalImagePath);
                var extension = Path.GetExtension(originalImagePath);
                var backupFileName = $"{fileName}_original_backup{extension}";
                var backupPath = Path.Combine(Path.GetDirectoryName(originalImagePath)!, backupFileName);
                
                // Only create backup if it doesn't already exist
                if (!System.IO.File.Exists(backupPath))
                {
                    System.IO.File.Copy(originalImagePath, backupPath);
                }

                // Generate output path (replace the original)
                var outputPath = originalImagePath; // Replace the original file

                // Get background image path
                var backgroundImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", background.ImageUrl!.TrimStart('/'));

                // Perform background replacement
                var result = await _imageCompositionService.ReplaceBackgroundAsync(
                    originalImagePath, backgroundImagePath, outputPath);

                if (result.Success)
                {
                    // Update the database with the new image URL
                    await UpdateImageUrlAsync(request.ImageType, request.SourceId, result.ProcessedImagePath!);

                    return Json(new { 
                        success = true, 
                        message = result.Message,
                        newImageUrl = result.ProcessedImagePath
                    });
                }
                else
                {
                    return Json(new { success = false, message = result.Message });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error processing image: {ex.Message}" });
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
                return Json(new { isAvailable = isAvailable });
            }
            catch (Exception ex)
            {
                return Json(new { isAvailable = false });
            }
        }

        private Task DeletePhysicalFile(string imageUrl)
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

            // Convert URL to physical path
            var relativePath = imageUrl.TrimStart('/');
            return Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath);
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
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    // Request models for AJAX endpoints
    public class ReplaceBackgroundRequest
    {
        public string ImageType { get; set; } = string.Empty;
        public int SourceId { get; set; }
        public int BackgroundId { get; set; }
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