using Members.Data;
using Members.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Members.Controllers
{
    [Authorize(Roles = "Admin")]
    public class BackgroundRemovalTestingController : Controller
    {
        private readonly BackgroundRemovalTestingService _testingService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<BackgroundRemovalTestingController> _logger;

        public BackgroundRemovalTestingController(
            BackgroundRemovalTestingService testingService,
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            ILogger<BackgroundRemovalTestingController> logger)
        {
            _testingService = testingService;
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> TestServices(IFormFile testImage)
        {
            if (testImage == null || testImage.Length == 0)
            {
                TempData["Error"] = "Please select an image to test";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // Save the uploaded test image temporarily
                var tempFileName = $"test_{Guid.NewGuid()}{Path.GetExtension(testImage.FileName)}";
                var tempPath = Path.Combine(Path.GetTempPath(), tempFileName);
                
                using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    await testImage.CopyToAsync(stream);
                }

                // Test all services
                var results = await _testingService.TestAllServicesAsync(tempPath);
                
                // Generate comparison report
                var report = await _testingService.GenerateComparisonReportAsync(results, tempPath);
                
                // Clean up temp file
                System.IO.File.Delete(tempPath);

                // Store results for display
                TempData["TestResults"] = System.Text.Json.JsonSerializer.Serialize(results);
                TempData["ComparisonReport"] = report;
                TempData["Success"] = $"Tested {results.Count} services. {results.Count(r => r.Success)} succeeded.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing background removal services");
                TempData["Error"] = $"Error testing services: {ex.Message}";
            }

            return RedirectToAction(nameof(Results));
        }

        public IActionResult Results()
        {
            if (TempData["TestResults"] is string testResultsJson)
            {
                var results = System.Text.Json.JsonSerializer.Deserialize<List<BackgroundRemovalTestResult>>(testResultsJson);
                ViewBag.TestResults = results;
                ViewBag.ComparisonReport = TempData["ComparisonReport"] as string;
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> TestSpecificImage(string imageType, int sourceId)
        {
            try
            {
                // Get image path from existing system
                string? imagePath = imageType switch
                {
                    "CatchPhoto" => await GetCatchPhotoPath(sourceId),
                    "AlbumCover" => await GetAlbumCoverPath(sourceId),
                    _ => null
                };

                if (string.IsNullOrEmpty(imagePath) || !System.IO.File.Exists(imagePath))
                {
                    return Json(new { success = false, message = "Image not found" });
                }

                // Test all services with this specific image
                var results = await _testingService.TestAllServicesAsync(imagePath);
                var report = await _testingService.GenerateComparisonReportAsync(results, imagePath);

                return Json(new { 
                    success = true, 
                    results = results,
                    report = report,
                    message = $"Tested {results.Count} services successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing specific image");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        private async Task<string?> GetCatchPhotoPath(int catchId)
        {
            var catch_item = await _context.Catches.FindAsync(catchId);
            if (catch_item?.PhotoUrl == null) return null;
            
            var relativePath = catch_item.PhotoUrl.TrimStart('/');
            return Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath);
        }

        private async Task<string?> GetAlbumCoverPath(int albumId)
        {
            var album = await _context.CatchAlbums.FindAsync(albumId);
            if (album?.CoverImageUrl == null) return null;
            
            var relativePath = album.CoverImageUrl.TrimStart('/');
            return Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath);
        }
    }
}