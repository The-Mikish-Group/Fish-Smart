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
            // Clear any cached entities to ensure fresh data
            _context.ChangeTracker.Clear();
            
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

        [HttpPost]
        public async Task<IActionResult> FixLongCatchPhotoFilenames()
        {
            try
            {
                var result = await FixCatchPhotoFilenames();
                TempData["Success"] = $"Successfully renamed {result.RenamedCount} catch photo files. {result.ErrorCount} errors occurred.";
                
                if (result.Errors.Any())
                {
                    TempData["Errors"] = string.Join("<br>", result.Errors);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fixing catch photo filenames");
                TempData["Error"] = $"Error occurred: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> FixLongAlbumImageFilenames()
        {
            try
            {
                var result = await FixAlbumImageFilenames();
                TempData["Success"] = $"Successfully renamed {result.RenamedCount} album image files. {result.ErrorCount} errors occurred.";
                
                if (result.Errors.Any())
                {
                    TempData["Errors"] = string.Join("<br>", result.Errors);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fixing album image filenames");
                TempData["Error"] = $"Error occurred: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> FixAllLongFilenames()
        {
            try
            {
                var bgResult = await FixBackgroundFilenames();
                var catchResult = await FixCatchPhotoFilenames();
                var albumResult = await FixAlbumImageFilenames();

                var totalRenamed = bgResult.RenamedCount + catchResult.RenamedCount + albumResult.RenamedCount;
                var totalErrors = bgResult.ErrorCount + catchResult.ErrorCount + albumResult.ErrorCount;

                TempData["Success"] = $"Fixed all long filenames! Renamed {totalRenamed} files total. Backgrounds: {bgResult.RenamedCount}, Catch Photos: {catchResult.RenamedCount}, Albums: {albumResult.RenamedCount}. {totalErrors} errors occurred.";
                
                var allErrors = bgResult.Errors.Concat(catchResult.Errors).Concat(albumResult.Errors);
                if (allErrors.Any())
                {
                    TempData["Errors"] = string.Join("<br>", allErrors);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fixing all long filenames");
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

            // Get backgrounds with long filenames (extract filename from URL path)
            var allBackgrounds = await _context.Backgrounds
                .Where(b => b.ImageUrl != null)
                .ToListAsync();
            
            var problematicBackgrounds = allBackgrounds
                .Where(b => {
                    var filename = b.ImageUrl?.Replace("/Images/Backgrounds/", "").Replace("/Images/Backgrounds", "") ?? "";
                    if (filename.StartsWith("/")) filename = filename.Substring(1);
                    return filename.Length > 100; // Focus on filename length, not full URL
                })
                .ToList();

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

        private async Task<FileFixResult> FixCatchPhotoFilenames()
        {
            var result = new FileFixResult();
            var catchesFolder = Path.Combine(_environment.WebRootPath, "Images", "Catches");

            if (!Directory.Exists(catchesFolder))
            {
                result.Errors.Add("Catches folder not found");
                return result;
            }

            // Get catches with long filenames (extract filename from URL path)
            var allCatches = await _context.Catches
                .Where(c => c.PhotoUrl != null)
                .ToListAsync();
            
            var problematicCatches = allCatches
                .Where(c => {
                    var filename = c.PhotoUrl?.Replace("/Images/Catches/", "").Replace("/Images/Catches", "") ?? "";
                    if (filename.StartsWith("/")) filename = filename.Substring(1);
                    return filename.Length > 100; // Focus on filename length, not full URL
                })
                .ToList();

            _logger.LogInformation("Found {Count} catches with long PhotoUrl", problematicCatches.Count);

            foreach (var catchItem in problematicCatches)
            {
                try
                {
                    if (string.IsNullOrEmpty(catchItem.PhotoUrl))
                        continue;

                    var currentFileName = catchItem.PhotoUrl.Replace("/Images/Catches/", "").Replace("/Images/Catches", "");
                    if (currentFileName.StartsWith("/"))
                        currentFileName = currentFileName.Substring(1);

                    var currentFilePath = Path.Combine(catchesFolder, currentFileName);

                    if (!System.IO.File.Exists(currentFilePath))
                    {
                        result.Errors.Add($"Physical file not found for catch {catchItem.Id}: {currentFileName}");
                        continue;
                    }

                    var extension = Path.GetExtension(currentFileName);
                    var safeName = $"catch_{catchItem.Id}";
                    var newFileName = FileNameHelper.CreateSafeFileName(safeName + extension);
                    var newFilePath = Path.Combine(catchesFolder, newFileName);

                    int counter = 1;
                    while (System.IO.File.Exists(newFilePath))
                    {
                        var uniqueName = $"catch_{catchItem.Id}_{counter}";
                        newFileName = FileNameHelper.CreateSafeFileName(uniqueName + extension);
                        newFilePath = Path.Combine(catchesFolder, newFileName);
                        counter++;
                    }

                    var oldUrl = catchItem.PhotoUrl;
                    catchItem.PhotoUrl = $"/Images/Catches/{newFileName}";
                    await _context.SaveChangesAsync();

                    try
                    {
                        System.IO.File.Move(currentFilePath, newFilePath);
                        result.RenamedCount++;
                        _logger.LogInformation("Successfully renamed catch photo: {Old} -> {New}", currentFileName, newFileName);
                    }
                    catch (Exception fileEx)
                    {
                        catchItem.PhotoUrl = oldUrl;
                        await _context.SaveChangesAsync();
                        result.ErrorCount++;
                        result.Errors.Add($"Failed to rename catch photo {catchItem.Id}: {fileEx.Message}");
                        _logger.LogError(fileEx, "Failed to rename catch photo file, rolled back database change");
                    }
                }
                catch (Exception ex)
                {
                    result.ErrorCount++;
                    result.Errors.Add($"Error processing catch {catchItem.Id}: {ex.Message}");
                    _logger.LogError(ex, "Error processing catch {Id}", catchItem.Id);
                }
            }

            return result;
        }

        private async Task<FileFixResult> FixAlbumImageFilenames()
        {
            var result = new FileFixResult();
            var albumsFolder = Path.Combine(_environment.WebRootPath, "Images", "Albums");

            if (!Directory.Exists(albumsFolder))
            {
                result.Errors.Add("Albums folder not found");
                return result;
            }

            // Get albums with long filenames (extract filename from URL path)
            var allAlbums = await _context.CatchAlbums
                .Where(a => a.CoverImageUrl != null)
                .ToListAsync();
            
            var problematicAlbums = allAlbums
                .Where(a => {
                    var filename = a.CoverImageUrl?.Replace("/Images/Albums/", "").Replace("/Images/Albums", "") ?? "";
                    if (filename.StartsWith("/")) filename = filename.Substring(1);
                    return filename.Length > 100; // Focus on filename length, not full URL
                })
                .ToList();

            _logger.LogInformation("Found {Count} albums with long CoverImageUrl", problematicAlbums.Count);

            foreach (var album in problematicAlbums)
            {
                try
                {
                    if (string.IsNullOrEmpty(album.CoverImageUrl))
                        continue;

                    var currentFileName = album.CoverImageUrl.Replace("/Images/Albums/", "").Replace("/Images/Albums", "");
                    if (currentFileName.StartsWith("/"))
                        currentFileName = currentFileName.Substring(1);

                    var currentFilePath = Path.Combine(albumsFolder, currentFileName);

                    if (!System.IO.File.Exists(currentFilePath))
                    {
                        result.Errors.Add($"Physical file not found for album {album.Id}: {currentFileName}");
                        continue;
                    }

                    var extension = Path.GetExtension(currentFileName);
                    var safeName = $"album_{album.Id}_{album.Name?.Replace(" ", "_") ?? "unnamed"}";
                    var newFileName = FileNameHelper.CreateSafeFileName(safeName + extension);
                    var newFilePath = Path.Combine(albumsFolder, newFileName);

                    int counter = 1;
                    while (System.IO.File.Exists(newFilePath))
                    {
                        var uniqueName = $"album_{album.Id}_{counter}";
                        newFileName = FileNameHelper.CreateSafeFileName(uniqueName + extension);
                        newFilePath = Path.Combine(albumsFolder, newFileName);
                        counter++;
                    }

                    var oldUrl = album.CoverImageUrl;
                    album.CoverImageUrl = $"/Images/Albums/{newFileName}";
                    await _context.SaveChangesAsync();

                    try
                    {
                        System.IO.File.Move(currentFilePath, newFilePath);
                        result.RenamedCount++;
                        _logger.LogInformation("Successfully renamed album photo: {Old} -> {New}", currentFileName, newFileName);
                    }
                    catch (Exception fileEx)
                    {
                        album.CoverImageUrl = oldUrl;
                        await _context.SaveChangesAsync();
                        result.ErrorCount++;
                        result.Errors.Add($"Failed to rename album photo {album.Id}: {fileEx.Message}");
                        _logger.LogError(fileEx, "Failed to rename album photo file, rolled back database change");
                    }
                }
                catch (Exception ex)
                {
                    result.ErrorCount++;
                    result.Errors.Add($"Error processing album {album.Id}: {ex.Message}");
                    _logger.LogError(ex, "Error processing album {Id}", album.Id);
                }
            }

            return result;
        }

        private async Task<string> GenerateFileStatusReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== Long Filename Status Report ===\n");
            report.AppendLine("NOTE: This checks actual FILENAME length (>100 chars), not full URL length.\n");

            // Backgrounds
            var backgrounds = await _context.Backgrounds.ToListAsync();
            var longBgCount = backgrounds.Count(b => {
                if (string.IsNullOrEmpty(b.ImageUrl)) return false;
                var filename = b.ImageUrl?.Replace("/Images/Backgrounds/", "").Replace("/Images/Backgrounds", "") ?? "";
                if (filename.StartsWith("/")) filename = filename.Substring(1);
                return filename.Length > 100;
            });
            report.AppendLine($"BACKGROUNDS:");
            report.AppendLine($"  Total: {backgrounds.Count}, Long Filenames: {longBgCount}");

            // Catch Photos
            var catches = await _context.Catches.ToListAsync();
            var longCatchCount = catches.Count(c => {
                if (string.IsNullOrEmpty(c.PhotoUrl)) return false;
                var filename = c.PhotoUrl?.Replace("/Images/Catches/", "").Replace("/Images/Catches", "") ?? "";
                if (filename.StartsWith("/")) filename = filename.Substring(1);
                return filename.Length > 100;
            });
            report.AppendLine($"CATCH PHOTOS:");
            report.AppendLine($"  Total: {catches.Count}, Long Filenames: {longCatchCount}");

            // Albums
            var albums = await _context.CatchAlbums.ToListAsync();
            var longAlbumCount = albums.Count(a => {
                if (string.IsNullOrEmpty(a.CoverImageUrl)) return false;
                var filename = a.CoverImageUrl?.Replace("/Images/Albums/", "").Replace("/Images/Albums", "") ?? "";
                if (filename.StartsWith("/")) filename = filename.Substring(1);
                return filename.Length > 100;
            });
            report.AppendLine($"ALBUM IMAGES:");
            report.AppendLine($"  Total: {albums.Count}, Long Filenames: {longAlbumCount}");

            var totalLongFilenames = longBgCount + longCatchCount + longAlbumCount;
            report.AppendLine($"\nTOTAL FILES WITH LONG FILENAMES: {totalLongFilenames}");

            if (totalLongFilenames > 0)
            {
                report.AppendLine("\n--- PROBLEMATIC FILES ---");
                
                if (longBgCount > 0)
                {
                    report.AppendLine($"\nBackgrounds with long filenames ({longBgCount}):");
                    var longBgs = backgrounds.Where(b => {
                        if (string.IsNullOrEmpty(b.ImageUrl)) return false;
                        var filename = b.ImageUrl?.Replace("/Images/Backgrounds/", "").Replace("/Images/Backgrounds", "") ?? "";
                        if (filename.StartsWith("/")) filename = filename.Substring(1);
                        return filename.Length > 100;
                    }).Take(3);
                    foreach (var bg in longBgs)
                    {
                        var filename = bg.ImageUrl?.Replace("/Images/Backgrounds/", "").Replace("/Images/Backgrounds", "") ?? "";
                        if (filename.StartsWith("/")) filename = filename.Substring(1);
                        report.AppendLine($"  • {bg.Name}: {filename.Length} chars (filename: {filename.Substring(0, Math.Min(30, filename.Length))}...)");
                    }
                    if (longBgCount > 3) report.AppendLine($"  ... and {longBgCount - 3} more");
                }

                if (longCatchCount > 0)
                {
                    report.AppendLine($"\nCatch photos with long filenames ({longCatchCount}):");
                    var longCatches = catches.Where(c => {
                        if (string.IsNullOrEmpty(c.PhotoUrl)) return false;
                        var filename = c.PhotoUrl?.Replace("/Images/Catches/", "").Replace("/Images/Catches", "") ?? "";
                        if (filename.StartsWith("/")) filename = filename.Substring(1);
                        return filename.Length > 100;
                    }).Take(3);
                    foreach (var catchItem in longCatches)
                    {
                        var filename = catchItem.PhotoUrl?.Replace("/Images/Catches/", "").Replace("/Images/Catches", "") ?? "";
                        if (filename.StartsWith("/")) filename = filename.Substring(1);
                        report.AppendLine($"  • Catch {catchItem.Id}: {filename.Length} chars (filename: {filename.Substring(0, Math.Min(30, filename.Length))}...)");
                    }
                    if (longCatchCount > 3) report.AppendLine($"  ... and {longCatchCount - 3} more");
                }

                if (longAlbumCount > 0)
                {
                    report.AppendLine($"\nAlbum images with long filenames ({longAlbumCount}):");
                    var longAlbums = albums.Where(a => {
                        if (string.IsNullOrEmpty(a.CoverImageUrl)) return false;
                        var filename = a.CoverImageUrl?.Replace("/Images/Albums/", "").Replace("/Images/Albums", "") ?? "";
                        if (filename.StartsWith("/")) filename = filename.Substring(1);
                        return filename.Length > 100;
                    }).Take(3);
                    foreach (var album in longAlbums)
                    {
                        var filename = album.CoverImageUrl?.Replace("/Images/Albums/", "").Replace("/Images/Albums", "") ?? "";
                        if (filename.StartsWith("/")) filename = filename.Substring(1);
                        report.AppendLine($"  • {album.Name}: {filename.Length} chars (filename: {filename.Substring(0, Math.Min(30, filename.Length))}...)");
                    }
                    if (longAlbumCount > 3) report.AppendLine($"  ... and {longAlbumCount - 3} more");
                }
            }

            // Physical file check
            report.AppendLine("\n--- PHYSICAL FILE STATUS ---");
            CheckPhysicalFiles(report, "Backgrounds", Path.Combine(_environment.WebRootPath, "Images", "Backgrounds"));
            CheckPhysicalFiles(report, "Catches", Path.Combine(_environment.WebRootPath, "Images", "Catches"));
            CheckPhysicalFiles(report, "Albums", Path.Combine(_environment.WebRootPath, "Images", "Albums"));

            return report.ToString();
        }

        private void CheckPhysicalFiles(System.Text.StringBuilder report, string folderName, string folderPath)
        {
            if (Directory.Exists(folderPath))
            {
                var physicalFiles = Directory.GetFiles(folderPath);
                var longFileNames = physicalFiles.Where(f => Path.GetFileName(f).Length > 100).ToList();
                report.AppendLine($"{folderName}: {physicalFiles.Length} files, {longFileNames.Count} with long names");
            }
            else
            {
                report.AppendLine($"{folderName}: Folder not found");
            }
        }

        public class FileFixResult
        {
            public int RenamedCount { get; set; }
            public int ErrorCount { get; set; }
            public List<string> Errors { get; set; } = new List<string>();
        }
    }
}