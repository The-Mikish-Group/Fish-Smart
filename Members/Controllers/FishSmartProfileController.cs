using Members.Data;
using Members.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Members.Controllers
{
    [Authorize]
    public class FishSmartProfileController(ApplicationDbContext context, UserManager<IdentityUser> userManager) : Controller
    {
        private readonly ApplicationDbContext _context = context;
        private readonly UserManager<IdentityUser> _userManager = userManager;

        // GET: SmartCatchProfile
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToAction("Login", "Account");

            var profile = await _context.SmartCatchProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
            {
                // Create default profile for new user
                profile = new SmartCatchProfile
                {
                    UserId = userId,
                    DisplayName = User.Identity?.Name ?? "Angler",
                    SubscriptionType = "Free",
                    PreferredWaterType = "Both",
                    WatermarkEnabled = true
                };

                _context.SmartCatchProfiles.Add(profile);
                await _context.SaveChangesAsync();
            }

            // Manually load related data
            profile.UserAvatars = await _context.UserAvatars
                .Where(a => a.UserId == userId)
                .ToListAsync();

            profile.FishingSessions = await _context.FishingSessions
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.SessionDate)
                .Take(5)
                .ToListAsync();

            profile.CatchAlbums = await _context.CatchAlbums
                .Where(a => a.UserId == userId)
                .ToListAsync();

            return View(profile);
        }

        // GET: SmartCatchProfile/Setup (for new users)
        public async Task<IActionResult> Setup()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToAction("Login", "Account");

            var existingProfile = await _context.SmartCatchProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (existingProfile != null)
            {
                return RedirectToAction(nameof(Index));
            }

            var profile = new SmartCatchProfile
            {
                UserId = userId,
                DisplayName = User.Identity?.Name ?? ""
            };

            // Get regions for dropdown
            ViewBag.Regions = await _context.FishSpecies
                .Where(f => f.IsActive && !string.IsNullOrEmpty(f.Region))
                .Select(f => f.Region)
                .Distinct()
                .OrderBy(r => r)
                .ToListAsync();

            return View(profile);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Setup([Bind("DisplayName,PreferredWaterType,DefaultRegion,WatermarkEnabled")] SmartCatchProfile profile)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToAction("Login", "Account");

            profile.UserId = userId;
            profile.SubscriptionType = "Free";
            profile.VoiceActivationEnabled = false;
            profile.AutoLocationEnabled = false;

            if (ModelState.IsValid)
            {
                _context.SmartCatchProfiles.Add(profile);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Welcome to Fish-Smart! Your profile has been created.";
                return RedirectToAction(nameof(Index));
            }

            // Reload regions for dropdown on error
            ViewBag.Regions = await _context.FishSpecies
                .Where(f => f.IsActive && !string.IsNullOrEmpty(f.Region))
                .Select(f => f.Region)
                .Distinct()
                .OrderBy(r => r)
                .ToListAsync();

            return View(profile);
        }

        // GET: SmartCatchProfile/Edit
        public async Task<IActionResult> Edit()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToAction("Login", "Account");

            var profile = await _context.SmartCatchProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
            {
                return RedirectToAction(nameof(Setup));
            }

            // Get regions for dropdown
            ViewBag.Regions = await _context.FishSpecies
                .Where(f => f.IsActive && !string.IsNullOrEmpty(f.Region))
                .Select(f => f.Region)
                .Distinct()
                .OrderBy(r => r)
                .ToListAsync();

            return View(profile);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([Bind("Id,DisplayName,PreferredWaterType,DefaultRegion,VoiceActivationEnabled,AutoLocationEnabled,WatermarkEnabled")] SmartCatchProfile profile)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToAction("Login", "Account");

            var existingProfile = await _context.SmartCatchProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (existingProfile == null || existingProfile.Id != profile.Id)
            {
                return NotFound();
            }

            // Preserve system fields
            profile.UserId = existingProfile.UserId;
            profile.SubscriptionType = existingProfile.SubscriptionType;
            profile.CreatedAt = existingProfile.CreatedAt;

            // Premium features check
            if (existingProfile.SubscriptionType != "Premium")
            {
                profile.VoiceActivationEnabled = false;
                profile.AutoLocationEnabled = false;
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Entry(existingProfile).CurrentValues.SetValues(profile);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Profile updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }

            // Reload regions for dropdown on error
            ViewBag.Regions = await _context.FishSpecies
                .Where(f => f.IsActive && !string.IsNullOrEmpty(f.Region))
                .Select(f => f.Region)
                .Distinct()
                .OrderBy(r => r)
                .ToListAsync();

            return View(profile);
        }

        // GET: SmartCatchProfile/Stats
        public async Task<IActionResult> Stats()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToAction("Login", "Account");

            var profile = await _context.SmartCatchProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
            {
                return RedirectToAction(nameof(Setup));
            }

            // Calculate statistics with simpler queries
            var totalSessions = await _context.FishingSessions.CountAsync(s => s.UserId == userId);
            var totalCatches = await _context.Catches
                .Where(c => c.Session!.UserId == userId)
                .CountAsync();
            var uniqueSpecies = await _context.Catches
                .Where(c => c.Session!.UserId == userId)
                .Select(c => c.FishSpeciesId)
                .Distinct()
                .CountAsync();
            var sharedCatches = await _context.Catches
                .Where(c => c.Session!.UserId == userId && c.IsShared)
                .CountAsync();

            // Get recent sessions
            var recentSessions = await _context.FishingSessions
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.SessionDate)
                .Take(5)
                .ToListAsync();

            // Get top species (simplified)
            var topSpeciesData = await _context.Catches
                .Where(c => c.Session!.UserId == userId)
                .Include(c => c.Species)
                .GroupBy(c => c.Species!.CommonName)
                .Select(g => new TopSpeciesData { Species = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToListAsync();

            // Get monthly activity (simplified - get raw data and process in memory)
            var sessions = await _context.FishingSessions
                .Where(s => s.UserId == userId && s.SessionDate >= DateTime.Now.AddMonths(-12))
                .Include(s => s.Catches)
                .ToListAsync();

            var monthlyActivityData = sessions
                .GroupBy(s => new { s.SessionDate.Year, s.SessionDate.Month })
                .Select(g => new MonthlyActivityData
                {
                    Month = $"{g.Key.Year}-{g.Key.Month:00}",
                    Sessions = g.Count(),
                    Catches = g.Sum(s => s.Catches.Count)
                })
                .OrderBy(x => x.Month)
                .ToList();

            // Create strongly-typed view model instead of anonymous object
            var viewModel = new StatsViewModel
            {
                Profile = profile,
                TotalSessions = totalSessions,
                TotalCatches = totalCatches,
                UniqueSpecies = uniqueSpecies,
                SharedCatches = sharedCatches,
                RecentSessions = recentSessions,
                TopSpecies = topSpeciesData,
                MonthlyActivity = monthlyActivityData
            };

            return View(viewModel);
        }

        // Stats View Model
        public class StatsViewModel
        {
            public SmartCatchProfile Profile { get; set; } = null!;
            public int TotalSessions { get; set; }
            public int TotalCatches { get; set; }
            public int UniqueSpecies { get; set; }
            public int SharedCatches { get; set; }
            public List<FishingSession> RecentSessions { get; set; } = new();
            public List<TopSpeciesData> TopSpecies { get; set; } = new();
            public List<MonthlyActivityData> MonthlyActivity { get; set; } = new();
        }

        public class TopSpeciesData
        {
            public string Species { get; set; } = string.Empty;
            public int Count { get; set; }
        }

        public class MonthlyActivityData
        {
            public string Month { get; set; } = string.Empty;
            public int Sessions { get; set; }
            public int Catches { get; set; }
        }

        // GET: SmartCatchProfile/Upgrade (for premium features)
        public async Task<IActionResult> Upgrade()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToAction("Login", "Account");

            var profile = await _context.SmartCatchProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
            {
                return RedirectToAction(nameof(Setup));
            }

            if (profile.SubscriptionType == "Premium")
            {
                TempData["Info"] = "You already have a Premium subscription!";
                return RedirectToAction(nameof(Index));
            }

            return View(profile);
        }

        // POST: Simulate upgrade (in real app, this would integrate with payment processor)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpgradeConfirm()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToAction("Login", "Account");

            var profile = await _context.SmartCatchProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile != null && profile.SubscriptionType != "Premium")
            {
                profile.SubscriptionType = "Premium";
                await _context.SaveChangesAsync();

                TempData["Success"] = "Congratulations! You've upgraded to Fish-Smart Premium!";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}