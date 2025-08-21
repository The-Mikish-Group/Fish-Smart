using Members.Data;
using Members.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Members.Controllers
{
    [Authorize]
    public class FishSpeciesController(ApplicationDbContext context) : Controller
    {
        private readonly ApplicationDbContext _context = context;

        // GET: FishSpecies
        public async Task<IActionResult> Index(string waterType = "All", string region = "All", string search = "")
        {
            var query = _context.FishSpecies.Where(f => f.IsActive);

            // Filter by water type
            if (!string.IsNullOrEmpty(waterType) && waterType != "All")
            {
                query = query.Where(f => f.WaterType == waterType || f.WaterType == "Both");
            }

            // Filter by region
            if (!string.IsNullOrEmpty(region) && region != "All")
            {
                query = query.Where(f => f.Region == region);
            }

            // Search by name
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(f => f.CommonName.Contains(search) ||
                                        f.ScientificName!.Contains(search));
            }

            var fishSpecies = await query.OrderBy(f => f.CommonName).ToListAsync();

            // Pass filter values to view
            ViewBag.WaterType = waterType;
            ViewBag.Region = region;
            ViewBag.Search = search;

            // Get unique regions for dropdown
            ViewBag.Regions = await _context.FishSpecies
                .Where(f => f.IsActive && !string.IsNullOrEmpty(f.Region))
                .Select(f => f.Region)
                .Distinct()
                .OrderBy(r => r)
                .ToListAsync();

            return View(fishSpecies);
        }

        // GET: FishSpecies/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var fishSpecies = await _context.FishSpecies
                .FirstOrDefaultAsync(m => m.Id == id);

            if (fishSpecies == null)
            {
                return NotFound();
            }

            return View(fishSpecies);
        }

        // GET: FishSpecies for AJAX (used by catch logging)
        [HttpGet]
        public async Task<IActionResult> GetByWaterType(string waterType)
        {
            var fishSpecies = await _context.FishSpecies
                .Where(f => f.IsActive && (f.WaterType == waterType || f.WaterType == "Both"))
                .OrderBy(f => f.CommonName)
                .Select(f => new {
                    id = f.Id,
                    name = f.CommonName,
                    minSize = f.MinSize,
                    maxSize = f.MaxSize,
                    regulations = f.RegulationNotes
                })
                .ToListAsync();

            return Json(fishSpecies);
        }

        // GET: FishSpecies/Export - Export all species data for image collection
        [HttpGet]
        public async Task<IActionResult> Export()
        {
            var fishSpecies = await _context.FishSpecies
                .Where(f => f.IsActive)
                .OrderBy(f => f.WaterType)
                .ThenBy(f => f.CommonName)
                .Select(f => new {
                    id = f.Id,
                    commonName = f.CommonName,
                    scientificName = f.ScientificName,
                    waterType = f.WaterType,
                    region = f.Region,
                    minSize = f.MinSize,
                    maxSize = f.MaxSize,
                    stockImageUrl = f.StockImageUrl,
                    hasImage = !string.IsNullOrEmpty(f.StockImageUrl),
                    regulationNotes = f.RegulationNotes
                })
                .ToListAsync();

            // Group by water type for easy viewing
            var grouped = new
            {
                totalSpecies = fishSpecies.Count,
                speciesWithImages = fishSpecies.Count(s => s.hasImage),
                speciesNeedingImages = fishSpecies.Count(s => !s.hasImage),
                freshwaterSpecies = fishSpecies.Where(s => s.waterType == "Fresh").ToList(),
                saltwaterSpecies = fishSpecies.Where(s => s.waterType == "Salt").ToList(),
                bothWaterSpecies = fishSpecies.Where(s => s.waterType == "Both").ToList(),
                allSpecies = fishSpecies
            };

            return Json(grouped);
        }

        // GET: FishSpecies/Search (for autocomplete)
        [HttpGet]
        public async Task<IActionResult> Search(string term, string waterType = "Both")
        {
            if (string.IsNullOrEmpty(term))
            {
                return Json(new List<object>());
            }

            var fishSpecies = await _context.FishSpecies
                .Where(f => f.IsActive &&
                           (f.WaterType == waterType || f.WaterType == "Both") &&
                           (f.CommonName.Contains(term) || f.ScientificName!.Contains(term)))
                .OrderBy(f => f.CommonName)
                .Take(10)
                .Select(f => new {
                    id = f.Id,
                    label = f.CommonName,
                    value = f.CommonName,
                    scientificName = f.ScientificName,
                    minSize = f.MinSize,
                    maxSize = f.MaxSize
                })
                .ToListAsync();

            return Json(fishSpecies);
        }

        // Admin and Manager only actions for managing fish species data
        [Authorize(Roles = "Admin, Manager")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Manager")]
        public async Task<IActionResult> Create([Bind("CommonName,ScientificName,WaterType,Region,MinSize,MaxSize,SeasonStart,SeasonEnd,StockImageUrl,RegulationNotes,IsActive")] FishSpecies fishSpecies, IFormFile? ImageFile)
        {
            if (ModelState.IsValid)
            {
                // Handle image upload if provided
                if (ImageFile != null && ImageFile.Length > 0)
                {
                    var result = await SaveFishSpeciesImageAsync(ImageFile, fishSpecies.CommonName);
                    if (result.Success)
                    {
                        fishSpecies.StockImageUrl = result.ImageUrl;
                    }
                    else
                    {
                        ModelState.AddModelError("ImageFile", result.ErrorMessage);
                        return View(fishSpecies);
                    }
                }

                _context.Add(fishSpecies);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Fish species '{fishSpecies.CommonName}' has been added successfully!";
                return RedirectToAction(nameof(Index));
            }
            return View(fishSpecies);
        }

        [Authorize(Roles = "Admin, Manager")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var fishSpecies = await _context.FishSpecies.FindAsync(id);
            if (fishSpecies == null)
            {
                return NotFound();
            }
            return View(fishSpecies);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Manager")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,CommonName,ScientificName,WaterType,Region,MinSize,MaxSize,SeasonStart,SeasonEnd,StockImageUrl,RegulationNotes,IsActive")] FishSpecies fishSpecies, IFormFile? ImageFile, bool RemoveImage = false)
        {
            if (id != fishSpecies.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingSpecies = await _context.FishSpecies.FindAsync(id);
                    if (existingSpecies == null)
                    {
                        return NotFound();
                    }

                    // Handle image removal
                    if (RemoveImage)
                    {
                        if (!string.IsNullOrEmpty(existingSpecies.StockImageUrl))
                        {
                            await DeleteFishSpeciesImageAsync(existingSpecies.StockImageUrl);
                        }
                        fishSpecies.StockImageUrl = null;
                    }
                    // Handle new image upload
                    else if (ImageFile != null && ImageFile.Length > 0)
                    {
                        // Delete old image if exists
                        if (!string.IsNullOrEmpty(existingSpecies.StockImageUrl))
                        {
                            await DeleteFishSpeciesImageAsync(existingSpecies.StockImageUrl);
                        }

                        var result = await SaveFishSpeciesImageAsync(ImageFile, fishSpecies.CommonName);
                        if (result.Success)
                        {
                            fishSpecies.StockImageUrl = result.ImageUrl;
                        }
                        else
                        {
                            ModelState.AddModelError("ImageFile", result.ErrorMessage);
                            return View(fishSpecies);
                        }
                    }
                    else
                    {
                        // Keep existing image if no new upload and not removing
                        fishSpecies.StockImageUrl = existingSpecies.StockImageUrl;
                    }

                    // Update the entity
                    _context.Entry(existingSpecies).CurrentValues.SetValues(fishSpecies);
                    await _context.SaveChangesAsync();
                    
                    TempData["Success"] = $"Fish species '{fishSpecies.CommonName}' has been updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!FishSpeciesExists(fishSpecies.Id))
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
            return View(fishSpecies);
        }

        [Authorize(Roles = "Admin, Manager")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var fishSpecies = await _context.FishSpecies
                .FirstOrDefaultAsync(m => m.Id == id);
            if (fishSpecies == null)
            {
                return NotFound();
            }

            return View(fishSpecies);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Manager")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var fishSpecies = await _context.FishSpecies.FindAsync(id);
            if (fishSpecies != null)
            {
                // Soft delete - just mark as inactive
                fishSpecies.IsActive = false;
                _context.Update(fishSpecies);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private bool FishSpeciesExists(int id)
        {
            return _context.FishSpecies.Any(e => e.Id == id);
        }

        private async Task<(bool Success, string ImageUrl, string ErrorMessage)> SaveFishSpeciesImageAsync(IFormFile imageFile, string fishName)
        {
            try
            {
                if (imageFile == null || imageFile.Length == 0)
                    return (false, string.Empty, "No image file provided");

                // Validate file type
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var fileExtension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
                
                if (!allowedExtensions.Contains(fileExtension))
                    return (false, string.Empty, "Invalid file type. Only JPG, PNG, and GIF files are allowed.");

                // Validate file size (5MB max)
                if (imageFile.Length > 5 * 1024 * 1024)
                    return (false, string.Empty, "File size too large. Maximum size is 5MB.");

                // Create images/fishspecies directory if it doesn't exist
                var imagesPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "fishspecies");
                Directory.CreateDirectory(imagesPath);

                // Generate unique filename
                var sanitizedFishName = string.Join("", fishName.Split(Path.GetInvalidFileNameChars()));
                var fileName = $"{sanitizedFishName}_{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(imagesPath, fileName);

                // Save the file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }

                // Return the URL path
                var imageUrl = $"/images/fishspecies/{fileName}";
                return (true, imageUrl, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, string.Empty, $"Error saving image: {ex.Message}");
            }
        }

        private Task<bool> DeleteFishSpeciesImageAsync(string imageUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(imageUrl))
                    return Task.FromResult(true);

                // Extract filename from URL
                var fileName = Path.GetFileName(imageUrl);
                if (string.IsNullOrEmpty(fileName))
                    return Task.FromResult(true);

                // Build full file path
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "fishspecies", fileName);

                // Delete file if it exists
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                return Task.FromResult(true);
            }
            catch (Exception)
            {
                // Log the error but don't fail the operation
                return Task.FromResult(false);
            }
        }
    }
}