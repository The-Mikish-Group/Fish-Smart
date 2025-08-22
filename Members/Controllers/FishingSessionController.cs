using Members.Data;
using Members.Models;
using Members.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Members.Controllers
{
    [Authorize]
    public class FishingSessionController(ApplicationDbContext context, UserManager<IdentityUser> userManager, IWeatherService weatherService, ISessionAlbumService sessionAlbumService, IMoonPhaseService moonPhaseService, ITideService tideService, ICatchWeatherService catchWeatherService) : Controller
    {
        private readonly ApplicationDbContext _context = context;
        private readonly UserManager<IdentityUser> _userManager = userManager;
        private readonly IWeatherService _weatherService = weatherService;
        private readonly ISessionAlbumService _sessionAlbumService = sessionAlbumService;
        private readonly IMoonPhaseService _moonPhaseService = moonPhaseService;
        private readonly ITideService _tideService = tideService;
        private readonly ICatchWeatherService _catchWeatherService = catchWeatherService;

        // GET: FishingSession
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToAction("Login", "Account");

            var sessions = await _context.FishingSessions
                .Include(s => s.Catches)
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.SessionDate)
                .ToListAsync();

            return View(sessions);
        }

        // GET: FishingSession/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            var session = await _context.FishingSessions
                .Include(s => s.RodReelSetup)
                .Include(s => s.PrimaryBaitLure)
                .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

            if (session == null) return NotFound();

            // Load catches for this session
            var catches = await _context.Catches
                .Include(c => c.Species)
                .Where(c => c.SessionId == id)
                .OrderBy(c => c.CatchTime ?? c.CreatedAt)
                .ToListAsync();

            ViewBag.Catches = catches;
            return View(session);
        }

        // GET: FishingSession/Create
        public async Task<IActionResult> Create()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToAction("Login", "Account");

            // Check if user has SmartCatch profile
            var profile = await _context.SmartCatchProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
            {
                TempData["Info"] = "Please complete your Fish-Smart profile setup first.";
                return RedirectToAction("Setup", "FishSmartProfile");
            }

            // Create new session with default values
            var session = new FishingSession
            {
                UserId = userId,
                SessionDate = DateTime.Now,
                WaterType = profile.PreferredWaterType == "Both" ? "Fresh" : profile.PreferredWaterType,
                LocationName = ""
            };

            // Load dropdown data
            await LoadDropdownData();

            return View(session);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("UserId,SessionDate,WaterType,LocationName,Latitude,Longitude,RodReelSetupId,PrimaryBaitLureId,Notes")] FishingSession session)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return RedirectToAction("Login", "Account");

            // Verify the UserId matches the current user for security
            if (session.UserId != userId)
            {
                session.UserId = userId;
            }
            session.CreatedAt = DateTime.Now;

            // Capture weather data for the session
            if (session.Latitude.HasValue && session.Longitude.HasValue)
            {
                try
                {
                    Console.WriteLine($"DEBUG: Attempting to fetch weather for {session.Latitude.Value}, {session.Longitude.Value}");
                    
                    var weatherData = await _weatherService.GetCurrentWeatherAsync(
                        session.Latitude.Value, 
                        session.Longitude.Value);
                    
                    Console.WriteLine($"DEBUG: Weather service returned: IsSuccessful={weatherData?.IsSuccessful}, Error={weatherData?.ErrorMessage}");
                    
                    if (weatherData != null && weatherData.IsSuccessful)
                    {
                        session.WeatherConditions = weatherData.WeatherConditions;
                        session.Temperature = weatherData.Temperature;
                        session.WindDirection = weatherData.WindDirection;
                        session.WindSpeed = weatherData.WindSpeed;
                        session.BarometricPressure = weatherData.BarometricPressure;
                        
                        Console.WriteLine($"DEBUG: Weather data captured - Conditions: {weatherData.WeatherConditions}, Temp: {weatherData.Temperature}°F");
                    }
                    else
                    {
                        Console.WriteLine($"DEBUG: Weather capture failed - {weatherData?.ErrorMessage}");
                    }

                    // Capture moon phase data for the session
                    try
                    {
                        var moonPhaseData = _moonPhaseService.GetCurrentMoonPhase((double)session.Latitude.Value, (double)session.Longitude.Value);
                        if (moonPhaseData != null)
                        {
                            session.MoonPhase = $"{moonPhaseData.PhaseName} ({moonPhaseData.IlluminationPercentage:F0}%)";
                            Console.WriteLine($"DEBUG: Moon phase data captured - {moonPhaseData.PhaseName} ({moonPhaseData.IlluminationPercentage:F1}%)");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"DEBUG: Moon phase capture exception - {ex.Message}");
                    }

                    // Capture tide data for the session
                    try
                    {
                        var tideData = _tideService.GetCurrentTideInfo((double)session.Latitude.Value, (double)session.Longitude.Value);
                        if (tideData != null)
                        {
                            if (tideData.IsCoastal)
                            {
                                session.TideConditions = $"{tideData.TideState} ({tideData.TideHeight:F1}ft)";
                                Console.WriteLine($"DEBUG: Tide data captured - {tideData.TideState} ({tideData.TideHeight:F1}ft, {tideData.TideRange})");
                            }
                            else
                            {
                                session.TideConditions = "Inland";
                                Console.WriteLine($"DEBUG: Inland location - no significant tidal effects");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"DEBUG: Tide capture exception - {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"DEBUG: Weather capture exception - {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"DEBUG: No GPS coordinates - Lat: {session.Latitude}, Lng: {session.Longitude}");
            }

            if (ModelState.IsValid)
            {
                _context.FishingSessions.Add(session);
                await _context.SaveChangesAsync();

                // Automatically create an album for this session
                try
                {
                    await _sessionAlbumService.CreateSessionAlbumAsync(session);
                    TempData["Success"] = "Fishing session and album created! Ready to log your catches.";
                }
                catch (Exception ex)
                {
                    // Log the error but don't fail the session creation
                    Console.WriteLine($"Failed to create session album: {ex.Message}");
                    TempData["Success"] = "Fishing session started! Ready to log your catches.";
                }

                return RedirectToAction("Index");
            }

            await LoadDropdownData();
            return View(session);
        }

        // GET: FishingSession/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            var session = await _context.FishingSessions
                .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

            if (session == null) return NotFound();

            await LoadDropdownData();
            return View(session);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,SessionDate,WaterType,LocationName,Latitude,Longitude,RodReelSetupId,PrimaryBaitLureId,Notes,WeatherConditions,Temperature,TideConditions,WindDirection,WindSpeed,MoonPhase,BarometricPressure")] FishingSession session)
        {
            if (id != session.Id) return NotFound();

            var userId = _userManager.GetUserId(User);
            var existingSession = await _context.FishingSessions
                .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

            if (existingSession == null) return NotFound();

            // Preserve system fields
            session.UserId = existingSession.UserId;
            session.CreatedAt = existingSession.CreatedAt;

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Entry(existingSession).CurrentValues.SetValues(session);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Session updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!FishingSessionExists(session.Id))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Details), new { id = session.Id });
            }

            await LoadDropdownData();
            return View(session);
        }

        // GET: FishingSession/AddCatch/5
        public async Task<IActionResult> AddCatch(int sessionId)
        {
            var userId = _userManager.GetUserId(User);
            var session = await _context.FishingSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);

            if (session == null) return NotFound();
            
            // Prevent adding catches to completed sessions
            if (session.IsCompleted)
            {
                TempData["Error"] = "Cannot add catches to a completed session.";
                return RedirectToAction("Details", new { id = sessionId });
            }

            // Create new catch for this session
            var newCatch = new Catch
            {
                SessionId = sessionId,
                CatchTime = DateTime.Now,
                Size = 0
            };

            // Load fish species for the session water type
            ViewBag.FishSpecies = await _context.FishSpecies
                .Where(f => f.IsActive && (f.WaterType == session.WaterType || f.WaterType == "Both"))
                .OrderBy(f => f.CommonName)
                .ToListAsync();

            ViewBag.Session = session;
            return View(newCatch);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCatch([Bind("SessionId,FishSpeciesId,Size,Weight,CatchTime,RigUsed,LureUsed")] Catch catchEntry)
        {
            var userId = _userManager.GetUserId(User);
            var session = await _context.FishingSessions
                .FirstOrDefaultAsync(s => s.Id == catchEntry.SessionId && s.UserId == userId);

            if (session == null) return NotFound();
            
            // Prevent adding catches to completed sessions
            if (session.IsCompleted)
            {
                TempData["Error"] = "Cannot add catches to a completed session.";
                return RedirectToAction("Details", new { id = catchEntry.SessionId });
            }

            catchEntry.CreatedAt = DateTime.Now;

            if (ModelState.IsValid)
            {
                _context.Catches.Add(catchEntry);
                
                // Populate environmental data (weather, moon phase, tide) for this catch
                try
                {
                    bool environmentalDataAdded = await _catchWeatherService.PopulateWeatherDataAsync(catchEntry, session);
                    if (environmentalDataAdded)
                    {
                        Console.WriteLine($"DEBUG: Environmental data successfully populated for catch {catchEntry.Id}");
                    }
                    else
                    {
                        Console.WriteLine($"DEBUG: Environmental data population failed for catch {catchEntry.Id}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"DEBUG: Exception populating environmental data for catch: {ex.Message}");
                    // Don't fail the catch creation if environmental data fails
                }
                
                await _context.SaveChangesAsync();

                // Automatically add catch to session album
                try
                {
                    await _sessionAlbumService.AddCatchToSessionAlbumAsync(catchEntry);
                    TempData["Success"] = "Catch logged and added to session album!";
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to add catch to session album: {ex.Message}");
                    TempData["Success"] = "Catch logged successfully!";
                }

                return RedirectToAction("Details", new { id = catchEntry.SessionId });
            }

            // Reload dropdown data on error
            ViewBag.FishSpecies = await _context.FishSpecies
                .Where(f => f.IsActive && (f.WaterType == session.WaterType || f.WaterType == "Both"))
                .OrderBy(f => f.CommonName)
                .ToListAsync();

            ViewBag.Session = session;
            return View(catchEntry);
        }

        // GET: FishingSession/EndSession/5
        public async Task<IActionResult> EndSession(int id)
        {
            var userId = _userManager.GetUserId(User);
            var session = await _context.FishingSessions
                .Include(s => s.RodReelSetup)
                .Include(s => s.PrimaryBaitLure)
                .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

            if (session == null) return NotFound();

            // Load catches for summary
            var catches = await _context.Catches
                .Include(c => c.Species)
                .Where(c => c.SessionId == id)
                .OrderBy(c => c.CatchTime ?? c.CreatedAt)
                .ToListAsync();

            ViewBag.Catches = catches;
            return View(session);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EndSessionConfirmed(int id)
        {
            var userId = _userManager.GetUserId(User);
            var session = await _context.FishingSessions
                .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

            if (session == null) return NotFound();

            // Mark session as completed
            session.IsCompleted = true;
            session.CompletedAt = DateTime.Now;
            
            await _context.SaveChangesAsync();
            
            TempData["Success"] = "Fishing session completed! Great job out there!";
            return RedirectToAction(nameof(Details), new { id = id });
        }

        // DELETE: FishingSession/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            var session = await _context.FishingSessions
                .Include(s => s.RodReelSetup)
                .Include(s => s.PrimaryBaitLure)
                .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

            if (session == null) return NotFound();

            // Load catches for the delete confirmation view
            var catches = await _context.Catches
                .Include(c => c.Species)
                .Where(c => c.SessionId == id)
                .OrderBy(c => c.CatchTime ?? c.CreatedAt)
                .ToListAsync();

            ViewBag.Catches = catches;
            return View(session);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = _userManager.GetUserId(User);
            var session = await _context.FishingSessions
                .Include(s => s.Catches)
                .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

            if (session != null)
            {
                try
                {
                    // Step 1: Delete the associated session album and its relationships first
                    await _sessionAlbumService.DeleteSessionAlbumAsync(id);
                    
                    // Step 2: Manually delete all catches to avoid cascade conflicts
                    if (session.Catches.Any())
                    {
                        foreach (var catchRecord in session.Catches)
                        {
                            // Remove any remaining album-catch relationships for this catch
                            var remainingAlbumCatches = await _context.AlbumCatches
                                .Where(ac => ac.CatchId == catchRecord.Id)
                                .ToListAsync();
                            
                            if (remainingAlbumCatches.Any())
                            {
                                _context.AlbumCatches.RemoveRange(remainingAlbumCatches);
                            }
                        }
                        
                        // Remove catches
                        _context.Catches.RemoveRange(session.Catches);
                    }
                    
                    // Step 3: Remove the session
                    _context.FishingSessions.Remove(session);
                    
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Fishing session and album deleted.";
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error deleting session: {ex.Message}");
                    TempData["Error"] = "Failed to delete session. Please try again.";
                }
            }

            return RedirectToAction(nameof(Index));
        }

        // AJAX endpoint for weather data
        [HttpGet]
        public async Task<IActionResult> GetWeather(double lat, double lng)
        {
            try
            {
                Console.WriteLine($"AJAX: GetWeather called with lat={lat}, lng={lng}");
                
                var weatherData = await _weatherService.GetCurrentWeatherAsync((decimal)lat, (decimal)lng);
                
                Console.WriteLine($"AJAX: Weather service returned: IsSuccessful={weatherData?.IsSuccessful}, Error={weatherData?.ErrorMessage}");
                
                return Json(new
                {
                    isSuccessful = weatherData?.IsSuccessful ?? false,
                    weatherConditions = weatherData?.WeatherConditions,
                    temperature = weatherData?.Temperature,
                    windDirection = weatherData?.WindDirection,
                    windSpeed = weatherData?.WindSpeed,
                    barometricPressure = weatherData?.BarometricPressure,
                    errorMessage = weatherData?.ErrorMessage
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AJAX: GetWeather exception - {ex.Message}");
                return Json(new
                {
                    isSuccessful = false,
                    errorMessage = ex.Message
                });
            }
        }

        // AJAX endpoint for moon phase data
        [HttpGet]
        public IActionResult GetMoonPhase(double lat, double lng)
        {
            try
            {
                Console.WriteLine($"AJAX: GetMoonPhase called with lat={lat}, lng={lng}");
                
                var moonPhaseData = _moonPhaseService.GetCurrentMoonPhase(lat, lng);
                
                Console.WriteLine($"AJAX: Moon phase service returned: {moonPhaseData.PhaseName} ({moonPhaseData.IlluminationPercentage:F1}%)");
                
                return Json(new
                {
                    phaseName = moonPhaseData.PhaseName,
                    phaseDescription = moonPhaseData.PhaseDescription,
                    illuminationPercentage = moonPhaseData.IlluminationPercentage,
                    age = moonPhaseData.Age,
                    isWaxing = moonPhaseData.IsWaxing,
                    icon = moonPhaseData.Icon,
                    fishingQuality = moonPhaseData.FishingQuality,
                    fishingTip = moonPhaseData.FishingTip,
                    moonRise = moonPhaseData.MoonRise?.ToString(@"hh\:mm"),
                    moonSet = moonPhaseData.MoonSet?.ToString(@"hh\:mm")
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AJAX: GetMoonPhase exception - {ex.Message}");
                return Json(new
                {
                    error = true,
                    errorMessage = ex.Message
                });
            }
        }

        // AJAX endpoint for tide data
        [HttpGet]
        public IActionResult GetTideInfo(double lat, double lng)
        {
            try
            {
                Console.WriteLine($"AJAX: GetTideInfo called with lat={lat}, lng={lng}");
                
                var tideData = _tideService.GetCurrentTideInfo(lat, lng);
                
                Console.WriteLine($"AJAX: Tide service returned: {tideData.TideState} ({tideData.TideHeight:F1}ft)");
                
                return Json(new
                {
                    isCoastal = tideData.IsCoastal,
                    tideState = tideData.TideState,
                    tideHeight = tideData.TideHeight,
                    tideRange = tideData.TideRange,
                    tidalCoefficient = tideData.TidalCoefficient,
                    fishingRecommendation = tideData.FishingRecommendation,
                    nextHighTide = tideData.NextHighTide?.ToString("h:mm tt"),
                    nextLowTide = tideData.NextLowTide?.ToString("h:mm tt"),
                    timeToNextChange = $"{(int)tideData.TimeToNextChange.TotalHours}h {tideData.TimeToNextChange.Minutes}m"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"AJAX: GetTideInfo exception - {ex.Message}");
                return Json(new
                {
                    error = true,
                    errorMessage = ex.Message
                });
            }
        }

        // Helper methods
        private async Task LoadDropdownData()
        {
            ViewBag.Equipment = await _context.FishingEquipment
                .Where(e => !e.IsPremium || User.IsInRole("Premium"))
                .OrderBy(e => e.Name)
                .ToListAsync();

            ViewBag.BaitsLures = await _context.BaitsLures
                .Where(bl => !bl.IsPremium || User.IsInRole("Premium"))
                .OrderBy(bl => bl.Name)
                .ToListAsync();
        }

        private bool FishingSessionExists(int id)
        {
            return _context.FishingSessions.Any(e => e.Id == id);
        }
    }
}