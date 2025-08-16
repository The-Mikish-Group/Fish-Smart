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
        public async Task<IActionResult> Create([Bind("CommonName,ScientificName,WaterType,Region,MinSize,MaxSize,SeasonStart,SeasonEnd,StockImageUrl,RegulationNotes,IsActive")] FishSpecies fishSpecies)
        {
            if (ModelState.IsValid)
            {
                _context.Add(fishSpecies);
                await _context.SaveChangesAsync();
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
        public async Task<IActionResult> Edit(int id, [Bind("Id,CommonName,ScientificName,WaterType,Region,MinSize,MaxSize,SeasonStart,SeasonEnd,StockImageUrl,RegulationNotes,IsActive")] FishSpecies fishSpecies)
        {
            if (id != fishSpecies.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(fishSpecies);
                    await _context.SaveChangesAsync();
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
    }
}