# Weather Integration Example

## How to Add Weather Auto-Population to Catch Creation

Once you've run the SQL script and added your OpenWeatherMap API key, here's how to integrate weather auto-population into your catch creation workflow.

## Example Integration

### 1. In Your Controller (where catches are created)

```csharp
// Add to constructor
private readonly ICatchWeatherService _catchWeatherService;

public YourController(
    ApplicationDbContext context,
    ICatchWeatherService catchWeatherService,
    // ... other dependencies
)
{
    _context = context;
    _catchWeatherService = catchWeatherService;
    // ... other assignments
}

// In your catch creation method
[HttpPost]
public async Task<IActionResult> CreateCatch(/* your parameters */)
{
    // Create your catch as normal
    var newCatch = new Catch
    {
        SessionId = sessionId,
        FishSpeciesId = speciesId,
        Size = size,
        Weight = weight,
        CatchTime = DateTime.Now, // or user-provided time
        // ... other properties
    };

    // Add to database first
    _context.Catches.Add(newCatch);
    await _context.SaveChangesAsync();

    // Now auto-populate weather data
    var session = await _context.FishingSessions
        .FirstOrDefaultAsync(s => s.Id == sessionId);
    
    if (session != null)
    {
        var weatherSuccess = await _catchWeatherService.PopulateWeatherDataAsync(newCatch, session);
        if (weatherSuccess)
        {
            await _context.SaveChangesAsync(); // Save weather data
            _logger.LogInformation("Weather data populated for catch {CatchId}", newCatch.Id);
        }
    }

    return /* your response */;
}
```

### 2. If You Have Specific Coordinates

```csharp
// If you have latitude/longitude from user's device
var weatherSuccess = await _catchWeatherService.PopulateWeatherDataAsync(
    newCatch, 
    latitude, 
    longitude
);
```

### 3. Displaying Weather in Views

```html
@model Catch

@{
    var weather = CatchWeatherService.GetWeatherDisplay(Model);
}

@if (weather.HasWeatherData)
{
    <div class="weather-info">
        <h5>Weather Conditions</h5>
        <p><strong>@weather.Summary</strong></p>
        
        @if (!string.IsNullOrEmpty(weather.Description))
        {
            <p><em>@weather.Description</em></p>
        }
        
        <small class="text-muted">
            Weather captured: @weather.CapturedAt?.ToString("MMM d, yyyy h:mm tt")
        </small>
    </div>
}
else
{
    <p class="text-muted">No weather data available</p>
}
```

### 4. Quick Weather Summary

```csharp
// Get a one-line weather summary
var summary = CatchWeatherService.GetWeatherSummary(catch);
// Returns: "Clear, 75°F, Wind: SW 8 mph, Pressure: 1013.2 hPa"
```

## API Call Efficiency

The weather service is designed to be efficient:
- Only calls API when location data is available
- Handles errors gracefully (won't break catch creation if weather fails)
- Logs all weather API calls for monitoring
- Uses current time if no catch time specified

## Automatic Behavior

Once integrated, weather data will be automatically captured for every catch that has location information. No additional user input required!

## Location Requirements

Weather auto-population requires:
- Session has latitude and longitude coordinates, OR
- Specific coordinates provided to the service

If no location data is available, the catch will still be created successfully, just without weather information.