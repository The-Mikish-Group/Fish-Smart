using Members.Data;
using Members.Models;
using Members.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using System.Diagnostics;

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
            var sizeReport = await GenerateImageSizeReport();
            
            ViewBag.Report = report;
            ViewBag.SizeReport = sizeReport;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ResizeAllImages()
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                
                var catchResults = await ResizeImagesInFolder("Images/Catches", 1200, "catch photos");
                var albumResults = await ResizeImagesInFolder("Images/Albums", 1200, "album covers");
                var backgroundResults = await ResizeImagesInFolder("Images/Backgrounds", 1920, "backgrounds");
                
                stopwatch.Stop();

                var totalResized = catchResults.ResizedCount + albumResults.ResizedCount + backgroundResults.ResizedCount;
                var totalSaved = catchResults.SpaceSaved + albumResults.SpaceSaved + backgroundResults.SpaceSaved;
                var totalErrors = catchResults.ErrorCount + albumResults.ErrorCount + backgroundResults.ErrorCount;

                TempData["Success"] = $"Resized {totalResized} images total. " +
                                    $"Saved {FormatFileSize(totalSaved)} of storage space. " +
                                    $"Completed in {stopwatch.Elapsed.TotalSeconds:F1} seconds.";
                
                if (totalErrors > 0)
                {
                    TempData["Warning"] = $"{totalErrors} files had errors and were skipped.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resizing all images");
                TempData["Error"] = $"Error resizing images: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> ResizeImages(string folder, int maxWidth, string description)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var results = await ResizeImagesInFolder(folder, maxWidth, description);
                stopwatch.Stop();

                TempData["Success"] = $"Resized {results.ResizedCount} {description}. " +
                                    $"Saved {FormatFileSize(results.SpaceSaved)}. " +
                                    $"Completed in {stopwatch.Elapsed.TotalSeconds:F1} seconds.";
                
                if (results.ErrorCount > 0)
                {
                    TempData["Warning"] = $"{results.ErrorCount} files had errors and were skipped.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resizing images in folder: {Folder}", folder);
                TempData["Error"] = $"Error resizing images: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
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

        [HttpPost]
        public async Task<IActionResult> CleanOrphanedRecords()
        {
            try
            {
                var result = await CleanupOrphanedFileReferences();
                TempData["Success"] = $"Cleanup completed! Processed {result.TotalProcessed} records. Cleared {result.ClearedCount} orphaned references. {result.ErrorCount} errors occurred.";
                
                if (result.Errors.Any())
                {
                    TempData["Errors"] = string.Join("<br>", result.Errors);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning orphaned records");
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

        private async Task<CleanupResult> CleanupOrphanedFileReferences()
        {
            var result = new CleanupResult();

            // Clean up catch photos
            var catches = await _context.Catches.Where(c => c.PhotoUrl != null).ToListAsync();
            var catchesFolder = Path.Combine(_environment.WebRootPath, "Images", "Catches");
            
            foreach (var catchItem in catches)
            {
                try
                {
                    result.TotalProcessed++;
                    
                    if (string.IsNullOrEmpty(catchItem.PhotoUrl))
                        continue;

                    var fileName = catchItem.PhotoUrl.Replace("/Images/Catches/", "").Replace("/Images/Catches", "");
                    if (fileName.StartsWith("/")) fileName = fileName.Substring(1);
                    
                    var filePath = Path.Combine(catchesFolder, fileName);
                    
                    if (!System.IO.File.Exists(filePath))
                    {
                        _logger.LogInformation("Clearing orphaned catch photo reference: Catch {Id}, missing file: {FileName}", catchItem.Id, fileName);
                        catchItem.PhotoUrl = null;
                        result.ClearedCount++;
                    }
                }
                catch (Exception ex)
                {
                    result.ErrorCount++;
                    result.Errors.Add($"Error processing catch {catchItem.Id}: {ex.Message}");
                    _logger.LogError(ex, "Error checking catch {Id}", catchItem.Id);
                }
            }

            // Clean up background images
            var backgrounds = await _context.Backgrounds.Where(b => b.ImageUrl != null).ToListAsync();
            var backgroundsFolder = Path.Combine(_environment.WebRootPath, "Images", "Backgrounds");
            
            foreach (var background in backgrounds)
            {
                try
                {
                    result.TotalProcessed++;
                    
                    if (string.IsNullOrEmpty(background.ImageUrl))
                        continue;

                    var fileName = background.ImageUrl.Replace("/Images/Backgrounds/", "").Replace("/Images/Backgrounds", "");
                    if (fileName.StartsWith("/")) fileName = fileName.Substring(1);
                    
                    var filePath = Path.Combine(backgroundsFolder, fileName);
                    
                    if (!System.IO.File.Exists(filePath))
                    {
                        _logger.LogInformation("Clearing orphaned background reference: Background {Id} '{Name}', missing file: {FileName}", background.Id, background.Name, fileName);
                        background.ImageUrl = null;
                        result.ClearedCount++;
                    }
                }
                catch (Exception ex)
                {
                    result.ErrorCount++;
                    result.Errors.Add($"Error processing background {background.Id}: {ex.Message}");
                    _logger.LogError(ex, "Error checking background {Id}", background.Id);
                }
            }

            // Clean up album images
            var albums = await _context.CatchAlbums.Where(a => a.CoverImageUrl != null).ToListAsync();
            var albumsFolder = Path.Combine(_environment.WebRootPath, "Images", "Albums");
            
            foreach (var album in albums)
            {
                try
                {
                    result.TotalProcessed++;
                    
                    if (string.IsNullOrEmpty(album.CoverImageUrl))
                        continue;

                    var fileName = album.CoverImageUrl.Replace("/Images/Albums/", "").Replace("/Images/Albums", "");
                    if (fileName.StartsWith("/")) fileName = fileName.Substring(1);
                    
                    var filePath = Path.Combine(albumsFolder, fileName);
                    
                    if (!System.IO.File.Exists(filePath))
                    {
                        _logger.LogInformation("Clearing orphaned album image reference: Album {Id} '{Name}', missing file: {FileName}", album.Id, album.Name, fileName);
                        album.CoverImageUrl = null;
                        result.ClearedCount++;
                    }
                }
                catch (Exception ex)
                {
                    result.ErrorCount++;
                    result.Errors.Add($"Error processing album {album.Id}: {ex.Message}");
                    _logger.LogError(ex, "Error checking album {Id}", album.Id);
                }
            }

            if (result.ClearedCount > 0)
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Saved {Count} orphaned reference cleanups to database", result.ClearedCount);
            }

            return result;
        }

        private async Task<string> GenerateImageSizeReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== Image Size Analysis Report ===\n");

            // Analyze each folder
            var catchesAnalysis = await AnalyzeImageSizes("Images/Catches", 1200);
            var albumsAnalysis = await AnalyzeImageSizes("Images/Albums", 1200);
            var backgroundsAnalysis = await AnalyzeImageSizes("Images/Backgrounds", 1920);

            report.AppendLine("CATCH PHOTOS:");
            report.AppendLine($"  Total: {catchesAnalysis.TotalFiles} files");
            report.AppendLine($"  Total Size: {FormatFileSize(catchesAnalysis.TotalSize)}");
            report.AppendLine($"  Oversized (>{catchesAnalysis.TargetMaxWidth}px): {catchesAnalysis.OversizedFiles} files");
            report.AppendLine($"  Oversized Total: {FormatFileSize(catchesAnalysis.OversizedTotalSize)}");
            report.AppendLine($"  Estimated Savings: {FormatFileSize(catchesAnalysis.EstimatedSavings)}\n");

            report.AppendLine("ALBUM COVERS:");
            report.AppendLine($"  Total: {albumsAnalysis.TotalFiles} files");
            report.AppendLine($"  Total Size: {FormatFileSize(albumsAnalysis.TotalSize)}");
            report.AppendLine($"  Oversized (>{albumsAnalysis.TargetMaxWidth}px): {albumsAnalysis.OversizedFiles} files");
            report.AppendLine($"  Oversized Total: {FormatFileSize(albumsAnalysis.OversizedTotalSize)}");
            report.AppendLine($"  Estimated Savings: {FormatFileSize(albumsAnalysis.EstimatedSavings)}\n");

            report.AppendLine("BACKGROUNDS:");
            report.AppendLine($"  Total: {backgroundsAnalysis.TotalFiles} files");
            report.AppendLine($"  Total Size: {FormatFileSize(backgroundsAnalysis.TotalSize)}");
            report.AppendLine($"  Oversized (>{backgroundsAnalysis.TargetMaxWidth}px): {backgroundsAnalysis.OversizedFiles} files");
            report.AppendLine($"  Oversized Total: {FormatFileSize(backgroundsAnalysis.OversizedTotalSize)}");
            report.AppendLine($"  Estimated Savings: {FormatFileSize(backgroundsAnalysis.EstimatedSavings)}\n");

            var totalOversized = catchesAnalysis.OversizedFiles + albumsAnalysis.OversizedFiles + backgroundsAnalysis.OversizedFiles;
            var totalSavings = catchesAnalysis.EstimatedSavings + albumsAnalysis.EstimatedSavings + backgroundsAnalysis.EstimatedSavings;

            report.AppendLine($"TOTAL OVERSIZED FILES: {totalOversized}");
            report.AppendLine($"TOTAL ESTIMATED SAVINGS: {FormatFileSize(totalSavings)}");

            return report.ToString();
        }

        private async Task<ImageSizeAnalysis> AnalyzeImageSizes(string relativePath, int targetMaxWidth)
        {
            var analysis = new ImageSizeAnalysis
            {
                FolderName = relativePath,
                TargetMaxWidth = targetMaxWidth
            };

            try
            {
                var fullPath = Path.Combine(_environment.WebRootPath, relativePath);
                if (!Directory.Exists(fullPath))
                {
                    return analysis;
                }

                var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
                var files = Directory.GetFiles(fullPath)
                    .Where(f => imageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .Where(f => !Path.GetFileName(f).Contains("_thumb")) // Exclude thumbnails
                    .ToArray();

                analysis.TotalFiles = files.Length;
                analysis.TotalSize = files.Sum(f => new FileInfo(f).Length);

                await Task.Run(() =>
                {
                    foreach (var file in files)
                    {
                        try
                        {
                            using var image = Image.Load(file);
                            if (image.Width > targetMaxWidth || image.Height > targetMaxWidth)
                            {
                                analysis.OversizedFiles++;
                                analysis.OversizedTotalSize += new FileInfo(file).Length;
                            }
                        }
                        catch
                        {
                            // Skip files that can't be processed
                        }
                    }
                });

                // Estimate savings (typically 60-80% size reduction)
                analysis.EstimatedSavings = (long)(analysis.OversizedTotalSize * 0.65);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing image sizes in directory: {Path}", relativePath);
            }

            return analysis;
        }

        private async Task<ResizeResults> ResizeImagesInFolder(string relativePath, int maxWidth, string description)
        {
            var results = new ResizeResults();
            var fullPath = Path.Combine(_environment.WebRootPath, relativePath);

            if (!Directory.Exists(fullPath))
                return results;

            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
            var files = Directory.GetFiles(fullPath)
                .Where(f => imageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .Where(f => !Path.GetFileName(f).Contains("_thumb")) // Exclude thumbnails
                .ToArray();

            _logger.LogInformation("Starting resize of {Count} files in {Path} (max width: {MaxWidth})", 
                files.Length, relativePath, maxWidth);

            foreach (var filePath in files)
            {
                try
                {
                    var originalSize = new FileInfo(filePath).Length;
                    
                    using (var image = await Image.LoadAsync(filePath))
                    {
                        if (image.Width > maxWidth || image.Height > maxWidth)
                        {
                            // Create backup
                            var backupPath = filePath + ".backup";
                            if (!System.IO.File.Exists(backupPath))
                            {
                                System.IO.File.Copy(filePath, backupPath);
                            }

                            // Calculate new dimensions maintaining aspect ratio
                            int newWidth, newHeight;
                            if (image.Width > image.Height)
                            {
                                newWidth = Math.Min(maxWidth, image.Width);
                                newHeight = (int)(image.Height * ((float)newWidth / image.Width));
                            }
                            else
                            {
                                newHeight = Math.Min(maxWidth, image.Height);
                                newWidth = (int)(image.Width * ((float)newHeight / image.Height));
                            }

                            // Resize image
                            image.Mutate(x => x.Resize(newWidth, newHeight));

                            // Save resized image with appropriate quality
                            var quality = relativePath.Contains("Background") ? 92 : 90;
                            await image.SaveAsJpegAsync(filePath, new JpegEncoder { Quality = quality });

                            var newSize = new FileInfo(filePath).Length;
                            results.SpaceSaved += originalSize - newSize;
                            results.ResizedCount++;

                            _logger.LogInformation("Resized {File}: {OriginalWidth}x{OriginalHeight} -> {NewWidth}x{NewHeight}, {OriginalSize} -> {NewSize}", 
                                Path.GetFileName(filePath), 
                                image.Width, image.Height, newWidth, newHeight,
                                FormatFileSize(originalSize), 
                                FormatFileSize(newSize));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error resizing file: {File}", filePath);
                    results.ErrorCount++;
                }
            }

            _logger.LogInformation("Completed resizing {Description}: {ResizedCount} files resized, {ErrorCount} errors, {SpaceSaved} saved", 
                description, results.ResizedCount, results.ErrorCount, FormatFileSize(results.SpaceSaved));

            return results;
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes == 0) return "0 B";
            
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }

        public class ImageSizeAnalysis
        {
            public string FolderName { get; set; } = string.Empty;
            public int TotalFiles { get; set; }
            public long TotalSize { get; set; }
            public int OversizedFiles { get; set; }
            public long OversizedTotalSize { get; set; }
            public long EstimatedSavings { get; set; }
            public int TargetMaxWidth { get; set; }
        }

        public class ResizeResults
        {
            public int ResizedCount { get; set; }
            public long SpaceSaved { get; set; }
            public int ErrorCount { get; set; }
        }

        public class FileFixResult
        {
            public int RenamedCount { get; set; }
            public int ErrorCount { get; set; }
            public List<string> Errors { get; set; } = new List<string>();
        }

        public class CleanupResult
        {
            public int TotalProcessed { get; set; }
            public int ClearedCount { get; set; }
            public int ErrorCount { get; set; }
            public List<string> Errors { get; set; } = new List<string>();
        }
    }
}