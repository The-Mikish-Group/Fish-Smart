using Members.Data;
using Members.Models;
using Members.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace Members.Controllers
{
    [Authorize(Roles = "Admin")]
    public class BackgroundsController(ApplicationDbContext context, IWebHostEnvironment environment) : Controller
    {
        private readonly ApplicationDbContext _context = context;
        private readonly IWebHostEnvironment _environment = environment;

        // GET: Backgrounds
        public async Task<IActionResult> Index()
        {
            var backgrounds = await _context.Backgrounds
                .OrderBy(b => b.Category)
                .ThenBy(b => b.Name)
                .ToListAsync();
            return View(backgrounds);
        }

        // GET: Backgrounds/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var background = await _context.Backgrounds
                .Include(b => b.Catches)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (background == null) return NotFound();

            return View(background);
        }

        // GET: Backgrounds/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Backgrounds/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Description,Category,WaterType,IsPremium")] Background background, IFormFile? imageFile)
        {
            if (ModelState.IsValid)
            {
                // Handle image upload
                if (imageFile != null && imageFile.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "Images", "Backgrounds");
                    Directory.CreateDirectory(uploadsFolder);

                    var safeFileName = FileNameHelper.CreateSafeFileName(imageFile.FileName);
                    var filePath = Path.Combine(uploadsFolder, safeFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(fileStream);
                    }

                    background.ImageUrl = FileNameHelper.CreateSafeUrlPath("/Images/Backgrounds", safeFileName);
                }

                _context.Add(background);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Background created successfully.";
                return RedirectToAction(nameof(Index));
            }
            return View(background);
        }

        // GET: Backgrounds/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var background = await _context.Backgrounds.FindAsync(id);
            if (background == null) return NotFound();

            return View(background);
        }

        // POST: Backgrounds/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,Category,WaterType,IsPremium,ImageUrl")] Background background, IFormFile? imageFile)
        {
            if (id != background.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // Handle new image upload
                    if (imageFile != null && imageFile.Length > 0)
                    {
                        // Delete old image if it exists
                        if (!string.IsNullOrEmpty(background.ImageUrl))
                        {
                            var oldImagePath = Path.Combine(_environment.WebRootPath, background.ImageUrl.TrimStart('/'));
                            if (System.IO.File.Exists(oldImagePath))
                            {
                                System.IO.File.Delete(oldImagePath);
                            }
                        }

                        var uploadsFolder = Path.Combine(_environment.WebRootPath, "Images", "Backgrounds");
                        Directory.CreateDirectory(uploadsFolder);

                        var safeFileName = FileNameHelper.CreateSafeFileName(imageFile.FileName);
                        var filePath = Path.Combine(uploadsFolder, safeFileName);

                        // Resize and save background image (max 1920px width for backgrounds)
                        using (var imageStream = imageFile.OpenReadStream())
                        using (var image = await SixLabors.ImageSharp.Image.LoadAsync(imageStream))
                        {
                            // Resize background to reasonable size (backgrounds can be larger than catches)
                            var maxWidth = 1920;
                            if (image.Width > maxWidth)
                            {
                                var aspectRatio = (float)image.Height / image.Width;
                                var newHeight = (int)(maxWidth * aspectRatio);
                                image.Mutate(x => x.Resize(maxWidth, newHeight));
                            }
                            
                            // Save with high quality for backgrounds
                            await image.SaveAsJpegAsync(filePath, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 92 });
                        }

                        background.ImageUrl = FileNameHelper.CreateSafeUrlPath("/Images/Backgrounds", safeFileName);
                    }

                    _context.Update(background);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Background updated successfully.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BackgroundExists(background.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(background);
        }

        // GET: Backgrounds/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var background = await _context.Backgrounds
                .Include(b => b.Catches)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (background == null) return NotFound();

            return View(background);
        }

        // POST: Backgrounds/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var background = await _context.Backgrounds
                .Include(b => b.Catches)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (background != null)
            {
                // Check if background is being used
                if (background.Catches.Any())
                {
                    TempData["Error"] = "Cannot delete background that is being used by catches.";
                    return RedirectToAction(nameof(Index));
                }

                // Delete image file if it exists
                if (!string.IsNullOrEmpty(background.ImageUrl))
                {
                    var imagePath = Path.Combine(_environment.WebRootPath, background.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(imagePath))
                    {
                        System.IO.File.Delete(imagePath);
                    }
                }

                _context.Backgrounds.Remove(background);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Background deleted successfully.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Backgrounds/SeedSampleData
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SeedSampleData()
        {
            try
            {
                // Check if we already have backgrounds
                var existingCount = await _context.Backgrounds.CountAsync();
                if (existingCount > 0)
                {
                    TempData["Warning"] = $"Background database already contains {existingCount} backgrounds.";
                    return RedirectToAction(nameof(Index));
                }

                // Create sample backgrounds (without images - admin needs to upload actual images)
                var sampleBackgrounds = new List<Background>
                {
                    new Background { Name = "Ocean Pier", Description = "Wooden pier extending into blue ocean waters - Upload pier image", Category = "Pier", WaterType = "Salt", IsPremium = false, ImageUrl = null },
                    new Background { Name = "Rocky Seawall", Description = "Natural rock seawall with crashing waves - Upload seawall image", Category = "Seawall", WaterType = "Salt", IsPremium = false, ImageUrl = null },
                    new Background { Name = "Sandy Beach", Description = "Clean sandy beach with gentle waves - Upload beach image", Category = "Beach", WaterType = "Salt", IsPremium = false, ImageUrl = null },
                    new Background { Name = "Fishing Boat Deck", Description = "Clean deck of a fishing boat - Upload boat deck image", Category = "Boat", WaterType = "Salt", IsPremium = true, ImageUrl = null },
                    new Background { Name = "Mountain Lake", Description = "Serene mountain lake with forest backdrop - Upload lake image", Category = "Lake", WaterType = "Fresh", IsPremium = false, ImageUrl = null },
                    new Background { Name = "River Bank", Description = "Peaceful river bank with rocks and trees - Upload river image", Category = "River", WaterType = "Fresh", IsPremium = false, ImageUrl = null },
                    new Background { Name = "Bass Boat", Description = "Modern bass fishing boat interior - Upload boat interior image", Category = "Boat", WaterType = "Fresh", IsPremium = true, ImageUrl = null },
                    new Background { Name = "Sunset Beach", Description = "Golden hour beach scene with warm lighting - Upload sunset beach image", Category = "Beach", WaterType = "Salt", IsPremium = true, ImageUrl = null },
                    new Background { Name = "Studio Gradient", Description = "Professional studio gradient background - Upload gradient image", Category = "Studio", WaterType = "Both", IsPremium = true, ImageUrl = null },
                    new Background { Name = "Dock at Dawn", Description = "Wooden dock in early morning light - Upload dock image", Category = "Pier", WaterType = "Both", IsPremium = false, ImageUrl = null }
                };

                _context.Backgrounds.AddRange(sampleBackgrounds);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Successfully seeded {sampleBackgrounds.Count} sample background templates. Click 'Edit' on each background to upload actual images.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error seeding sample data: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool BackgroundExists(int id)
        {
            return _context.Backgrounds.Any(e => e.Id == id);
        }
    }
}