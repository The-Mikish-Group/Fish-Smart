using Members.Data;
using Members.Models;
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
    public class CatchAlbumController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public CatchAlbumController(ApplicationDbContext context, UserManager<IdentityUser> userManager, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: CatchAlbum
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToAction("Login", "Account");

            var albums = await _context.CatchAlbums
                .Where(a => a.UserId == userId)
                .Include(a => a.AlbumCatches)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return View(albums);
        }

        // GET: CatchAlbum/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            var album = await _context.CatchAlbums
                .Include(a => a.AlbumCatches)
                    .ThenInclude(ac => ac.Catch!)
                        .ThenInclude(c => c.Species)
                .Include(a => a.AlbumCatches)
                    .ThenInclude(ac => ac.Catch!)
                        .ThenInclude(c => c.Session)
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (album == null) return NotFound();

            return View(album);
        }

        // GET: CatchAlbum/Create
        public async Task<IActionResult> Create()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToAction("Login", "Account");

            // Check if user has SmartCatch profile
            var profile = await _context.SmartCatchProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
            {
                TempData["Info"] = "Please complete your Fish-Smart profile setup first.";
                return RedirectToAction("Setup", "SmartCatchProfile");
            }

            var album = new CatchAlbum
            {
                UserId = userId
            };

            return View(album);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("UserId,Name,Description,IsPublic")] CatchAlbum album)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToAction("Login", "Account");

            // Verify the UserId matches the current user for security
            if (album.UserId != userId)
            {
                album.UserId = userId;
            }
            album.CreatedAt = DateTime.Now;

            if (ModelState.IsValid)
            {
                _context.CatchAlbums.Add(album);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Catch album created successfully!";
                return RedirectToAction(nameof(Details), new { id = album.Id });
            }

            return View(album);
        }

        // GET: CatchAlbum/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            var album = await _context.CatchAlbums
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (album == null) return NotFound();

            return View(album);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,UserId,Name,Description,IsPublic")] CatchAlbum album)
        {
            if (id != album.Id) return NotFound();

            var userId = _userManager.GetUserId(User);
            var existingAlbum = await _context.CatchAlbums
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (existingAlbum == null) return NotFound();

            // Preserve system fields
            album.UserId = existingAlbum.UserId;
            album.CreatedAt = existingAlbum.CreatedAt;
            album.CoverImageUrl = existingAlbum.CoverImageUrl;

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Entry(existingAlbum).CurrentValues.SetValues(album);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Album updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CatchAlbumExists(album.Id))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Details), new { id = album.Id });
            }

            return View(album);
        }

        // GET: CatchAlbum/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            var album = await _context.CatchAlbums
                .Include(a => a.AlbumCatches)
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (album == null) return NotFound();

            return View(album);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = _userManager.GetUserId(User);
            var album = await _context.CatchAlbums
                .Include(a => a.AlbumCatches)
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (album != null)
            {
                // Remove all album-catch relationships first
                _context.AlbumCatches.RemoveRange(album.AlbumCatches);
                
                // Remove the album
                _context.CatchAlbums.Remove(album);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Catch album deleted successfully.";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: CatchAlbum/AddCatch/5
        public async Task<IActionResult> AddCatch(int id)
        {
            var userId = _userManager.GetUserId(User);
            var album = await _context.CatchAlbums
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (album == null) return NotFound();

            // Get user's catches that aren't already in this album
            var existingCatchIds = await _context.AlbumCatches
                .Where(ac => ac.AlbumId == id)
                .Select(ac => ac.CatchId)
                .ToListAsync();

            var availableCatches = await _context.Catches
                .Include(c => c.Species)
                .Include(c => c.Session)
                .Where(c => c.Session != null && c.Session.UserId == userId && !existingCatchIds.Contains(c.Id))
                .OrderByDescending(c => c.CatchTime ?? c.CreatedAt)
                .ToListAsync();

            ViewBag.Album = album;
            ViewBag.AvailableCatches = availableCatches;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCatch(int albumId, int catchId)
        {
            var userId = _userManager.GetUserId(User);
            var album = await _context.CatchAlbums
                .FirstOrDefaultAsync(a => a.Id == albumId && a.UserId == userId);

            if (album == null) return NotFound();

            // Verify the catch belongs to the user
            var catchItem = await _context.Catches
                .Include(c => c.Session)
                .FirstOrDefaultAsync(c => c.Id == catchId && c.Session != null && c.Session.UserId == userId);

            if (catchItem == null) return NotFound();

            // Check if already in album
            var existingRelation = await _context.AlbumCatches
                .FirstOrDefaultAsync(ac => ac.AlbumId == albumId && ac.CatchId == catchId);

            if (existingRelation == null)
            {
                var albumCatch = new AlbumCatches
                {
                    AlbumId = albumId,
                    CatchId = catchId,
                    AddedAt = DateTime.Now
                };

                _context.AlbumCatches.Add(albumCatch);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Catch added to album successfully!";
            }
            else
            {
                TempData["Info"] = "This catch is already in the album.";
            }

            return RedirectToAction(nameof(Details), new { id = albumId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveCatch(int albumId, int catchId)
        {
            var userId = _userManager.GetUserId(User);
            var album = await _context.CatchAlbums
                .FirstOrDefaultAsync(a => a.Id == albumId && a.UserId == userId);

            if (album == null) return NotFound();

            var albumCatch = await _context.AlbumCatches
                .FirstOrDefaultAsync(ac => ac.AlbumId == albumId && ac.CatchId == catchId);

            if (albumCatch != null)
            {
                _context.AlbumCatches.Remove(albumCatch);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Catch removed from album.";
            }

            return RedirectToAction(nameof(Details), new { id = albumId });
        }

        // GET: CatchAlbum/UploadPhoto/5
        public async Task<IActionResult> UploadPhoto(int id)
        {
            var userId = _userManager.GetUserId(User);
            var album = await _context.CatchAlbums
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (album == null) return NotFound();

            ViewBag.Album = album;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadPhoto(int id, IFormFile photo)
        {
            var userId = _userManager.GetUserId(User);
            var album = await _context.CatchAlbums
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (album == null) return NotFound();

            if (photo != null && photo.Length > 0)
            {
                // Validate file type
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var fileExtension = Path.GetExtension(photo.FileName).ToLowerInvariant();
                
                if (!allowedExtensions.Contains(fileExtension))
                {
                    ModelState.AddModelError("", "Please upload a valid image file (JPG, JPEG, PNG, or GIF).");
                    ViewBag.Album = album;
                    return View();
                }

                // Validate file size (max 5MB)
                if (photo.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError("", "File size must be less than 5MB.");
                    ViewBag.Album = album;
                    return View();
                }

                try
                {
                    // Create unique filename
                    var fileName = $"album_{album.Id}_{Guid.NewGuid()}{fileExtension}";
                    var albumsPath = Path.Combine(_webHostEnvironment.WebRootPath, "Images", "Albums");
                    
                    // Ensure directory exists
                    if (!Directory.Exists(albumsPath))
                    {
                        Directory.CreateDirectory(albumsPath);
                    }

                    var filePath = Path.Combine(albumsPath, fileName);

                    // Save and process the image
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await photo.CopyToAsync(stream);
                    }

                    // Generate thumbnail
                    await GenerateAlbumThumbnail(filePath);

                    // Update album cover image
                    album.CoverImageUrl = $"/Images/Albums/{fileName}";
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Album photo uploaded successfully!";
                    return RedirectToAction(nameof(Details), new { id = album.Id });
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

            ViewBag.Album = album;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemovePhoto(int id)
        {
            var userId = _userManager.GetUserId(User);
            var album = await _context.CatchAlbums
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (album == null) return NotFound();

            if (!string.IsNullOrEmpty(album.CoverImageUrl))
            {
                // Delete the physical file
                var fileName = Path.GetFileName(album.CoverImageUrl);
                var filePath = Path.Combine(_webHostEnvironment.WebRootPath, "Images", "Albums", fileName);
                
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                // Delete thumbnail if it exists
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                var extension = Path.GetExtension(fileName);
                var thumbnailName = $"{fileNameWithoutExt}_thumb{extension}";
                var thumbnailPath = Path.Combine(_webHostEnvironment.WebRootPath, "Images", "Albums", thumbnailName);
                
                if (System.IO.File.Exists(thumbnailPath))
                {
                    System.IO.File.Delete(thumbnailPath);
                }

                // Update database
                album.CoverImageUrl = null;
                await _context.SaveChangesAsync();

                TempData["Success"] = "Album photo removed successfully!";
            }

            return RedirectToAction(nameof(Details), new { id = album.Id });
        }

        private static async Task GenerateAlbumThumbnail(string imagePath)
        {
            try
            {
                var directory = Path.GetDirectoryName(imagePath);
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(imagePath);
                var extension = Path.GetExtension(imagePath);
                var thumbnailPath = Path.Combine(directory!, $"{fileNameWithoutExt}_thumb{extension}");

                using var image = await Image.LoadAsync(imagePath);
                
                // Resize to 400x300 for album thumbnails
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(400, 300),
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

        private bool CatchAlbumExists(int id)
        {
            return _context.CatchAlbums.Any(e => e.Id == id);
        }
    }
}