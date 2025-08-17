using Members.Data;
using Members.Models;
using Members.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace Members.Controllers
{
    [Authorize]
    public class CatchPhotoController(ApplicationDbContext context, UserManager<IdentityUser> userManager, IWebHostEnvironment webHostEnvironment) : Controller
    {
        private readonly ApplicationDbContext _context = context;
        private readonly UserManager<IdentityUser> _userManager = userManager;
        private readonly IWebHostEnvironment _webHostEnvironment = webHostEnvironment;

        // GET: CatchPhoto/Upload/5
        public async Task<IActionResult> Upload(int id)
        {
            var userId = _userManager.GetUserId(User);
            var catchItem = await _context.Catches
                .Include(c => c.Session)
                .Include(c => c.Species)
                .FirstOrDefaultAsync(c => c.Id == id && c.Session != null && c.Session.UserId == userId);

            if (catchItem == null) return NotFound();

            ViewBag.Catch = catchItem;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(int id, IFormFile photo, string? returnUrl = null)
        {
            var userId = _userManager.GetUserId(User);
            var catchItem = await _context.Catches
                .Include(c => c.Session)
                .Include(c => c.Species)
                .FirstOrDefaultAsync(c => c.Id == id && c.Session != null && c.Session.UserId == userId);

            if (catchItem == null) return NotFound();

            if (photo != null && photo.Length > 0)
            {
                // Validate file type
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var fileExtension = Path.GetExtension(photo.FileName).ToLowerInvariant();
                
                if (!allowedExtensions.Contains(fileExtension))
                {
                    ModelState.AddModelError("", "Please upload a valid image file (JPG, JPEG, PNG, or GIF).");
                    ViewBag.Catch = catchItem;
                    return View();
                }

                // Validate file size (max 10MB for catch photos)
                if (photo.Length > 10 * 1024 * 1024)
                {
                    ModelState.AddModelError("", "File size must be less than 10MB.");
                    ViewBag.Catch = catchItem;
                    return View();
                }

                try
                {
                    // Create unique filename using safe filename helper
                    var originalName = $"catch_{catchItem.Id}{fileExtension}";
                    var fileName = FileNameHelper.CreateSafeFileName(originalName);
                    var catchesPath = Path.Combine(_webHostEnvironment.WebRootPath, "Images", "Catches");
                    
                    // Ensure directory exists
                    if (!Directory.Exists(catchesPath))
                    {
                        Directory.CreateDirectory(catchesPath);
                    }

                    var filePath = Path.Combine(catchesPath, fileName);

                    // Save and process the image
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await photo.CopyToAsync(stream);
                    }

                    // Generate thumbnail
                    await GenerateCatchThumbnail(filePath);

                    // Remove old photo if exists
                    if (!string.IsNullOrEmpty(catchItem.PhotoUrl))
                    {
                        _ = DeleteCatchPhoto(catchItem.PhotoUrl);
                    }

                    // Update catch photo URL
                    catchItem.PhotoUrl = $"/Images/Catches/{fileName}";
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Catch photo uploaded successfully!";
                    
                    // Return to specified URL or default to catch details
                    if (!string.IsNullOrEmpty(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }
                    return RedirectToAction("Details", "FishingSession", new { id = catchItem.SessionId });
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error uploading photo: {ex.Message}");
                }
            }
            else
            {
                ModelState.AddModelError("", "Please select a photo to upload.");
            }

            ViewBag.Catch = catchItem;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int id, string? returnUrl = null)
        {
            var userId = _userManager.GetUserId(User);
            var catchItem = await _context.Catches
                .Include(c => c.Session)
                .FirstOrDefaultAsync(c => c.Id == id && c.Session != null && c.Session.UserId == userId);

            if (catchItem == null) return NotFound();

            if (!string.IsNullOrEmpty(catchItem.PhotoUrl))
            {
                _ = DeleteCatchPhoto(catchItem.PhotoUrl);

                // Update database
                catchItem.PhotoUrl = null;
                await _context.SaveChangesAsync();

                TempData["Success"] = "Catch photo removed successfully!";
            }

            // Return to specified URL or default
            if (!string.IsNullOrEmpty(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Details", "FishingSession", new { id = catchItem.SessionId });
        }

        private static Task DeleteCatchPhoto(string photoUrl)
        {
            try
            {
                var fileName = Path.GetFileName(photoUrl);
                var webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var filePath = Path.Combine(webRootPath, "Images", "Catches", fileName);
                
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                // Delete thumbnail if it exists
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                var extension = Path.GetExtension(fileName);
                var thumbnailName = $"{fileNameWithoutExt}_thumb{extension}";
                var thumbnailPath = Path.Combine(webRootPath, "Images", "Catches", thumbnailName);
                
                if (System.IO.File.Exists(thumbnailPath))
                {
                    System.IO.File.Delete(thumbnailPath);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't fail the operation
                Console.WriteLine($"Error deleting catch photo: {ex.Message}");
            }
            return Task.CompletedTask;
        }

        private static async Task GenerateCatchThumbnail(string imagePath)
        {
            try
            {
                var directory = Path.GetDirectoryName(imagePath);
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(imagePath);
                var extension = Path.GetExtension(imagePath);
                var thumbnailPath = Path.Combine(directory!, $"{fileNameWithoutExt}_thumb{extension}");

                using var image = await Image.LoadAsync(imagePath);
                
                // Resize to 300x300 for catch thumbnails (square)
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(300, 300),
                    Mode = ResizeMode.Crop
                }));

                await image.SaveAsJpegAsync(thumbnailPath, new JpegEncoder { Quality = 85 });
            }
            catch (Exception ex)
            {
                // Log error but don't fail the upload
                Console.WriteLine($"Error generating thumbnail: {ex.Message}");
            }
        }
    }
}