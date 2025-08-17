using Members.Data;
using Members.Models;
using Members.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Members.Controllers
{
    [Authorize(Roles = "Admin")]
    public class FileMaintenanceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FileMaintenanceController> _logger;

        public FileMaintenanceController(ApplicationDbContext context, IWebHostEnvironment environment, ILogger<FileMaintenanceController> logger)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var report = await GenerateFileStatusReport();
            ViewBag.Report = report;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> FixLongBackgroundFilenames()
        {
            try
            {
                var result = await FixBackgroundFilenames();
                TempData["Success"] = $"Successfully renamed {result.RenamedCount} background files. {result.ErrorCount} errors occurred.";
                
                if (result.Errors.Any())
                {
                    TempData["Errors"] = string.Join("<br>", result.Errors);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fixing background filenames");
                TempData["Error"] = $"Error occurred: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<FileFixResult> FixBackgroundFilenames()
        {
            var result = new FileFixResult();
            var backgroundsFolder = Path.Combine(_environment.WebRootPath, "Images", "Backgrounds");

            if (!Directory.Exists(backgroundsFolder))
            {
                result.Errors.Add("Backgrounds folder not found");
                return result;
            }

            // Get backgrounds with long URLs
            var problematicBackgrounds = await _context.Backgrounds
                .Where(b => b.ImageUrl != null && b.ImageUrl.Length > 150)
                .ToListAsync();

            _logger.LogInformation("Found {Count} backgrounds with long URLs", problematicBackgrounds.Count);

            foreach (var background in problematicBackgrounds)
            {
                try
                {
                    if (string.IsNullOrEmpty(background.ImageUrl))
                        continue;

                    // Extract current filename from URL
                    var currentFileName = background.ImageUrl.Replace("/Images/Backgrounds/", "").Replace("/Images/Backgrounds", "");
                    if (currentFileName.StartsWith("/"))
                        currentFileName = currentFileName.Substring(1);

                    var currentFilePath = Path.Combine(backgroundsFolder, currentFileName);

                    // Check if physical file exists
                    if (!System.IO.File.Exists(currentFilePath))
                    {
                        result.Errors.Add($"Physical file not found for background '{background.Name}': {currentFileName}");
                        continue;
                    }

                    // Generate new safe filename
                    var extension = Path.GetExtension(currentFileName);
                    var safeName = $"bg_{background.Id}_{background.Name?.Replace(" ", "_") ?? "unnamed"}";
                    var newFileName = FileNameHelper.CreateSafeFileName(safeName + extension);
                    var newFilePath = Path.Combine(backgroundsFolder, newFileName);

                    // Ensure uniqueness
                    int counter = 1;
                    while (System.IO.File.Exists(newFilePath))
                    {
                        var uniqueName = $"bg_{background.Id}_{counter}";
                        newFileName = FileNameHelper.CreateSafeFileName(uniqueName + extension);
                        newFilePath = Path.Combine(backgroundsFolder, newFileName);
                        counter++;
                    }

                    // Create backup entry (for rollback)
                    var backupInfo = $"Background ID {background.Id}: {background.ImageUrl} -> /Images/Backgrounds/{newFileName}";
                    _logger.LogInformation("Renaming: {BackupInfo}", backupInfo);

                    // Step 1: Update database first
                    var oldUrl = background.ImageUrl;
                    background.ImageUrl = $"/Images/Backgrounds/{newFileName}";
                    await _context.SaveChangesAsync();

                    try
                    {
                        // Step 2: Rename physical file
                        System.IO.File.Move(currentFilePath, newFilePath);
                        
                        result.RenamedCount++;
                        _logger.LogInformation("Successfully renamed: {Old} -> {New}", currentFileName, newFileName);
                    }
                    catch (Exception fileEx)
                    {
                        // Rollback database change if file rename fails
                        background.ImageUrl = oldUrl;
                        await _context.SaveChangesAsync();
                        
                        result.ErrorCount++;
                        result.Errors.Add($"Failed to rename file for '{background.Name}': {fileEx.Message}");
                        _logger.LogError(fileEx, "Failed to rename file, rolled back database change");
                    }
                }
                catch (Exception ex)
                {
                    result.ErrorCount++;
                    result.Errors.Add($"Error processing background '{background.Name}': {ex.Message}");
                    _logger.LogError(ex, "Error processing background {Id}", background.Id);
                }
            }

            return result;
        }

        private async Task<string> GenerateFileStatusReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== Background Files Status Report ===\n");

            var backgrounds = await _context.Backgrounds.ToListAsync();
            var longUrlCount = backgrounds.Count(b => !string.IsNullOrEmpty(b.ImageUrl) && b.ImageUrl.Length > 150);

            report.AppendLine($"Total backgrounds in database: {backgrounds.Count}");
            report.AppendLine($"Backgrounds with long URLs (>150 chars): {longUrlCount}");

            if (longUrlCount > 0)
            {
                report.AppendLine("\nBackgrounds with long URLs:");
                var longUrlBackgrounds = backgrounds.Where(b => !string.IsNullOrEmpty(b.ImageUrl) && b.ImageUrl.Length > 150).Take(10);
                foreach (var bg in longUrlBackgrounds)
                {
                    report.AppendLine($"  • {bg.Name}: {bg.ImageUrl?.Length} characters");
                }
                if (longUrlCount > 10)
                {
                    report.AppendLine($"  ... and {longUrlCount - 10} more");
                }
            }

            var backgroundsFolder = Path.Combine(_environment.WebRootPath, "Images", "Backgrounds");
            if (Directory.Exists(backgroundsFolder))
            {
                var physicalFiles = Directory.GetFiles(backgroundsFolder);
                var longFileNames = physicalFiles.Where(f => Path.GetFileName(f).Length > 100).ToList();

                report.AppendLine($"\nPhysical files in backgrounds folder: {physicalFiles.Length}");
                report.AppendLine($"Files with long names (>100 chars): {longFileNames.Count}");

                if (longFileNames.Any())
                {
                    report.AppendLine("\nFiles with long names:");
                    foreach (var file in longFileNames.Take(5))
                    {
                        var fileName = Path.GetFileName(file);
                        report.AppendLine($"  • {fileName.Substring(0, Math.Min(50, fileName.Length))}... ({fileName.Length} chars)");
                    }
                    if (longFileNames.Count > 5)
                    {
                        report.AppendLine($"  ... and {longFileNames.Count - 5} more");
                    }
                }
            }
            else
            {
                report.AppendLine("\nBackgrounds folder not found!");
            }

            return report.ToString();
        }

        public class FileFixResult
        {
            public int RenamedCount { get; set; }
            public int ErrorCount { get; set; }
            public List<string> Errors { get; set; } = new List<string>();
        }
    }
}