using Members.Data;
using Members.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Members.Controllers
{
    [Authorize]
    public class CatchController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public CatchController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Catch/Details/5
        public async Task<IActionResult> Details(int? id, string? returnUrl = null)
        {
            if (id == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            var catchItem = await _context.Catches
                .Include(c => c.Session)
                .Include(c => c.Species)
                .Include(c => c.AlbumCatches)
                    .ThenInclude(ac => ac.Album)
                .FirstOrDefaultAsync(c => c.Id == id && c.Session != null && c.Session.UserId == userId);

            if (catchItem == null) return NotFound();

            ViewBag.ReturnUrl = returnUrl;
            return View(catchItem);
        }

        // GET: Catch/Edit/5
        public async Task<IActionResult> Edit(int? id, string? returnUrl = null)
        {
            if (id == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            var catchItem = await _context.Catches
                .Include(c => c.Session)
                .Include(c => c.Species)
                .FirstOrDefaultAsync(c => c.Id == id && c.Session != null && c.Session.UserId == userId);

            if (catchItem == null) return NotFound();

            // Load dropdown data
            await LoadDropdownData(catchItem.Session?.WaterType ?? "Fresh");
            
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.Session = catchItem.Session;
            return View(catchItem);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,SessionId,FishSpeciesId,Size,Weight,CatchTime")] Catch catchEntry, string? returnUrl = null)
        {
            if (id != catchEntry.Id) return NotFound();

            var userId = _userManager.GetUserId(User);
            var existingCatch = await _context.Catches
                .Include(c => c.Session)
                .FirstOrDefaultAsync(c => c.Id == id && c.Session != null && c.Session.UserId == userId);

            if (existingCatch == null) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // Preserve system fields
                    catchEntry.CreatedAt = existingCatch.CreatedAt;
                    catchEntry.PhotoUrl = existingCatch.PhotoUrl;
                    catchEntry.CompositeImageUrl = existingCatch.CompositeImageUrl;
                    catchEntry.WatermarkedImageUrl = existingCatch.WatermarkedImageUrl;
                    catchEntry.AvatarId = existingCatch.AvatarId;
                    catchEntry.PoseId = existingCatch.PoseId;
                    catchEntry.BackgroundId = existingCatch.BackgroundId;
                    catchEntry.OutfitId = existingCatch.OutfitId;
                    catchEntry.ShowSpeciesName = existingCatch.ShowSpeciesName;
                    catchEntry.ShowSize = existingCatch.ShowSize;
                    catchEntry.IsShared = existingCatch.IsShared;

                    _context.Entry(existingCatch).CurrentValues.SetValues(catchEntry);
                    await _context.SaveChangesAsync();
                    
                    TempData["Success"] = "Catch updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CatchExists(catchEntry.Id))
                        return NotFound();
                    else
                        throw;
                }

                // Return to specified URL or default to session details
                if (!string.IsNullOrEmpty(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction("Details", "FishingSession", new { id = catchEntry.SessionId });
            }

            // Reload dropdown data on error
            var session = await _context.FishingSessions.FindAsync(catchEntry.SessionId);
            await LoadDropdownData(session?.WaterType ?? "Fresh");
            
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.Session = session;
            return View(catchEntry);
        }

        // GET: Catch/Delete/5
        public async Task<IActionResult> Delete(int? id, string? returnUrl = null)
        {
            if (id == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            var catchItem = await _context.Catches
                .Include(c => c.Session)
                .Include(c => c.Species)
                .Include(c => c.AlbumCatches)
                    .ThenInclude(ac => ac.Album)
                .FirstOrDefaultAsync(c => c.Id == id && c.Session != null && c.Session.UserId == userId);

            if (catchItem == null) return NotFound();

            ViewBag.ReturnUrl = returnUrl;
            return View(catchItem);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, string? returnUrl = null)
        {
            var userId = _userManager.GetUserId(User);
            var catchItem = await _context.Catches
                .Include(c => c.Session)
                .Include(c => c.AlbumCatches)
                .FirstOrDefaultAsync(c => c.Id == id && c.Session != null && c.Session.UserId == userId);

            if (catchItem != null)
            {
                // Remove from all albums first
                _context.AlbumCatches.RemoveRange(catchItem.AlbumCatches);
                
                // Delete photo file if exists
                if (!string.IsNullOrEmpty(catchItem.PhotoUrl))
                {
                    _ = DeleteCatchPhoto(catchItem.PhotoUrl);
                }
                
                // Remove the catch
                _context.Catches.Remove(catchItem);
                await _context.SaveChangesAsync();
                
                TempData["Success"] = "Catch deleted successfully.";
            }

            // Return to specified URL or default to session details
            if (!string.IsNullOrEmpty(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Details", "FishingSession", new { id = catchItem?.SessionId });
        }

        private async Task LoadDropdownData(string waterType)
        {
            ViewBag.FishSpecies = await _context.FishSpecies
                .Where(f => f.IsActive && (f.WaterType == waterType || f.WaterType == "Both"))
                .OrderBy(f => f.CommonName)
                .ToListAsync();
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

        private bool CatchExists(int id)
        {
            return _context.Catches.Any(e => e.Id == id);
        }
    }
}