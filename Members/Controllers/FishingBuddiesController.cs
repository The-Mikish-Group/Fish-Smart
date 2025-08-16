using Members.Data;
using Members.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Members.Controllers
{
    [Authorize]
    public class FishingBuddiesController(ApplicationDbContext context, UserManager<IdentityUser> userManager) : Controller
    {
        private readonly ApplicationDbContext _context = context;
        private readonly UserManager<IdentityUser> _userManager = userManager;

        // GET: FishingBuddies
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToAction("Login", "Account");

            // Get all buddy relationships where user is involved
            var buddyRelationships = await _context.FishingBuddies
                .Include(fb => fb.OwnerUser)
                .Include(fb => fb.BuddyUser)
                .Where(fb => fb.OwnerUserId == userId || fb.BuddyUserId == userId)
                .OrderByDescending(fb => fb.CreatedAt)
                .ToListAsync();

            // Separate into different categories
            var viewModel = new FishingBuddiesViewModel
            {
                // Buddies I've accepted or who have accepted me
                AcceptedBuddies = buddyRelationships
                    .Where(fb => fb.Status == "Accepted")
                    .ToList(),

                // Requests I've sent that are pending
                PendingOutgoing = buddyRelationships
                    .Where(fb => fb.OwnerUserId == userId && fb.Status == "Pending")
                    .ToList(),

                // Requests sent to me that are pending
                PendingIncoming = buddyRelationships
                    .Where(fb => fb.BuddyUserId == userId && fb.Status == "Pending")
                    .ToList(),

                // Users I've blocked
                BlockedUsers = buddyRelationships
                    .Where(fb => fb.OwnerUserId == userId && fb.Status == "Blocked")
                    .ToList()
            };

            return View(viewModel);
        }

        // GET: FishingBuddies/Search
        public async Task<IActionResult> Search(string searchTerm = "")
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToAction("Login", "Account");

            var users = new List<IdentityUser>();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                // Get existing buddy relationships to exclude
                var existingBuddyIds = await _context.FishingBuddies
                    .Where(fb => fb.OwnerUserId == userId || fb.BuddyUserId == userId)
                    .Select(fb => fb.OwnerUserId == userId ? fb.BuddyUserId : fb.OwnerUserId)
                    .ToListAsync();

                // Search for users by email or username, excluding current user and existing buddies
                users = await _userManager.Users
                    .Where(u => u.Id != userId && 
                               !existingBuddyIds.Contains(u.Id) &&
                               (u.Email!.Contains(searchTerm) || u.UserName!.Contains(searchTerm)))
                    .Take(20)
                    .ToListAsync();
            }

            ViewBag.SearchTerm = searchTerm;
            return View(users);
        }

        // POST: FishingBuddies/SendRequest
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendRequest(string buddyUserId)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToAction("Login", "Account");

            if (string.IsNullOrEmpty(buddyUserId) || buddyUserId == userId)
            {
                TempData["Error"] = "Invalid user selected.";
                return RedirectToAction(nameof(Search));
            }

            // Check if relationship already exists
            var existingRelationship = await _context.FishingBuddies
                .FirstOrDefaultAsync(fb => 
                    (fb.OwnerUserId == userId && fb.BuddyUserId == buddyUserId) ||
                    (fb.OwnerUserId == buddyUserId && fb.BuddyUserId == userId));

            if (existingRelationship != null)
            {
                TempData["Error"] = "A buddy relationship already exists with this user.";
                return RedirectToAction(nameof(Search));
            }

            // Verify the buddy user exists
            var buddyUser = await _userManager.FindByIdAsync(buddyUserId);
            if (buddyUser == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction(nameof(Search));
            }

            // Create new buddy request
            var buddyRequest = new FishingBuddies
            {
                OwnerUserId = userId,
                BuddyUserId = buddyUserId,
                Status = "Pending",
                CreatedAt = DateTime.Now
            };

            _context.FishingBuddies.Add(buddyRequest);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Buddy request sent to {buddyUser.Email}!";
            return RedirectToAction(nameof(Index));
        }

        // POST: FishingBuddies/AcceptRequest
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptRequest(int id)
        {
            var userId = _userManager.GetUserId(User);
            var buddyRequest = await _context.FishingBuddies
                .Include(fb => fb.OwnerUser)
                .FirstOrDefaultAsync(fb => fb.Id == id && fb.BuddyUserId == userId && fb.Status == "Pending");

            if (buddyRequest == null)
            {
                TempData["Error"] = "Buddy request not found.";
                return RedirectToAction(nameof(Index));
            }

            buddyRequest.Status = "Accepted";
            await _context.SaveChangesAsync();

            TempData["Success"] = $"You are now fishing buddies with {buddyRequest.OwnerUser?.Email}!";
            return RedirectToAction(nameof(Index));
        }

        // POST: FishingBuddies/DeclineRequest
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeclineRequest(int id)
        {
            var userId = _userManager.GetUserId(User);
            var buddyRequest = await _context.FishingBuddies
                .FirstOrDefaultAsync(fb => fb.Id == id && fb.BuddyUserId == userId && fb.Status == "Pending");

            if (buddyRequest == null)
            {
                TempData["Error"] = "Buddy request not found.";
                return RedirectToAction(nameof(Index));
            }

            _context.FishingBuddies.Remove(buddyRequest);
            await _context.SaveChangesAsync();

            TempData["Info"] = "Buddy request declined.";
            return RedirectToAction(nameof(Index));
        }

        // POST: FishingBuddies/RemoveBuddy
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveBuddy(int id)
        {
            var userId = _userManager.GetUserId(User);
            var buddyRelationship = await _context.FishingBuddies
                .Include(fb => fb.OwnerUser)
                .Include(fb => fb.BuddyUser)
                .FirstOrDefaultAsync(fb => fb.Id == id && 
                    (fb.OwnerUserId == userId || fb.BuddyUserId == userId));

            if (buddyRelationship == null)
            {
                TempData["Error"] = "Buddy relationship not found.";
                return RedirectToAction(nameof(Index));
            }

            var otherUser = buddyRelationship.OwnerUserId == userId ? 
                buddyRelationship.BuddyUser : buddyRelationship.OwnerUser;

            _context.FishingBuddies.Remove(buddyRelationship);
            await _context.SaveChangesAsync();

            TempData["Info"] = $"Removed {otherUser?.Email} from your fishing buddies.";
            return RedirectToAction(nameof(Index));
        }

        // POST: FishingBuddies/BlockUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BlockUser(int id)
        {
            var userId = _userManager.GetUserId(User);
            var buddyRelationship = await _context.FishingBuddies
                .Include(fb => fb.BuddyUser)
                .FirstOrDefaultAsync(fb => fb.Id == id && fb.OwnerUserId == userId);

            if (buddyRelationship == null)
            {
                TempData["Error"] = "Buddy relationship not found.";
                return RedirectToAction(nameof(Index));
            }

            buddyRelationship.Status = "Blocked";
            await _context.SaveChangesAsync();

            TempData["Info"] = $"Blocked {buddyRelationship.BuddyUser?.Email}.";
            return RedirectToAction(nameof(Index));
        }

        // POST: FishingBuddies/UnblockUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnblockUser(int id)
        {
            var userId = _userManager.GetUserId(User);
            var buddyRelationship = await _context.FishingBuddies
                .Include(fb => fb.BuddyUser)
                .FirstOrDefaultAsync(fb => fb.Id == id && fb.OwnerUserId == userId && fb.Status == "Blocked");

            if (buddyRelationship == null)
            {
                TempData["Error"] = "Blocked user not found.";
                return RedirectToAction(nameof(Index));
            }

            _context.FishingBuddies.Remove(buddyRelationship);
            await _context.SaveChangesAsync();

            TempData["Info"] = $"Unblocked {buddyRelationship.BuddyUser?.Email}.";
            return RedirectToAction(nameof(Index));
        }

        // GET: FishingBuddies/ViewBuddyProfile/5
        public async Task<IActionResult> ViewBuddyProfile(string buddyUserId)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToAction("Login", "Account");

            // Verify they are actually buddies
            var buddyRelationship = await _context.FishingBuddies
                .FirstOrDefaultAsync(fb => 
                    ((fb.OwnerUserId == userId && fb.BuddyUserId == buddyUserId) ||
                     (fb.OwnerUserId == buddyUserId && fb.BuddyUserId == userId)) &&
                    fb.Status == "Accepted");

            if (buddyRelationship == null)
            {
                TempData["Error"] = "You can only view profiles of accepted fishing buddies.";
                return RedirectToAction(nameof(Index));
            }

            // Get buddy's profile
            var buddyProfile = await _context.SmartCatchProfiles
                .FirstOrDefaultAsync(p => p.UserId == buddyUserId);

            if (buddyProfile == null)
            {
                TempData["Error"] = "Buddy profile not found.";
                return RedirectToAction(nameof(Index));
            }

            // Get buddy's recent sessions (last 10)
            var recentSessions = await _context.FishingSessions
                .Where(s => s.UserId == buddyUserId)
                .OrderByDescending(s => s.SessionDate)
                .Take(10)
                .ToListAsync();

            // Get buddy's public albums
            var publicAlbums = await _context.CatchAlbums
                .Where(a => a.UserId == buddyUserId && a.IsPublic)
                .OrderByDescending(a => a.CreatedAt)
                .Take(6)
                .ToListAsync();

            // Get catch stats
            var totalCatches = await _context.Catches
                .CountAsync(c => c.Session != null && c.Session.UserId == buddyUserId);

            var viewModel = new BuddyProfileViewModel
            {
                Profile = buddyProfile,
                RecentSessions = recentSessions,
                PublicAlbums = publicAlbums,
                TotalCatches = totalCatches
            };

            return View(viewModel);
        }

        // Helper method to get buddy list for other controllers
        public async Task<List<FishingBuddies>> GetUserBuddiesAsync(string userId)
        {
            return await _context.FishingBuddies
                .Include(fb => fb.OwnerUser)
                .Include(fb => fb.BuddyUser)
                .Where(fb => (fb.OwnerUserId == userId || fb.BuddyUserId == userId) && 
                            fb.Status == "Accepted")
                .ToListAsync();
        }
    }

    // View Models for FishingBuddies
    public class FishingBuddiesViewModel
    {
        public List<FishingBuddies> AcceptedBuddies { get; set; } = new();
        public List<FishingBuddies> PendingOutgoing { get; set; } = new();
        public List<FishingBuddies> PendingIncoming { get; set; } = new();
        public List<FishingBuddies> BlockedUsers { get; set; } = new();
    }

    public class BuddyProfileViewModel
    {
        public SmartCatchProfile Profile { get; set; } = new();
        public List<FishingSession> RecentSessions { get; set; } = new();
        public List<CatchAlbum> PublicAlbums { get; set; } = new();
        public int TotalCatches { get; set; }
    }
}