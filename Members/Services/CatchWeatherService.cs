using Members.Models;

namespace Members.Services
{
    public interface ICatchWeatherService
    {
        /// <summary>
        /// Automatically populates weather data for a catch based on session location
        /// </summary>
        /// <param name="catch">The catch to populate weather data for</param>
        /// <param name="session">The fishing session (contains location data)</param>
        /// <returns>True if weather was successfully populated</returns>
        Task<bool> PopulateWeatherDataAsync(Catch catchRecord, FishingSession session);

        /// <summary>
        /// Automatically populates weather data for a catch based on provided coordinates
        /// </summary>
        /// <param name="catch">The catch to populate weather data for</param>
        /// <param name="latitude">Latitude coordinate</param>
        /// <param name="longitude">Longitude coordinate</param>
        /// <returns>True if weather was successfully populated</returns>
        Task<bool> PopulateWeatherDataAsync(Catch catchRecord, decimal latitude, decimal longitude);
    }

    public class CatchWeatherService : ICatchWeatherService
    {
        private readonly IWeatherService _weatherService;
        private readonly IMoonPhaseService _moonPhaseService;
        private readonly ILogger<CatchWeatherService> _logger;

        public CatchWeatherService(IWeatherService weatherService, IMoonPhaseService moonPhaseService, ILogger<CatchWeatherService> logger)
        {
            _weatherService = weatherService;
            _moonPhaseService = moonPhaseService;
            _logger = logger;
        }

        public async Task<bool> PopulateWeatherDataAsync(Catch catchRecord, FishingSession session)
        {
            if (session.Latitude == null || session.Longitude == null)
            {
                _logger.LogWarning("Cannot populate weather data - session {SessionId} has no location data", session.Id);
                return false;
            }

            return await PopulateWeatherDataAsync(catchRecord, session.Latitude.Value, session.Longitude.Value);
        }

        public async Task<bool> PopulateWeatherDataAsync(Catch catchRecord, decimal latitude, decimal longitude)
        {
            try
            {
                _logger.LogInformation("Fetching weather data for catch at location: {Latitude}, {Longitude}", latitude, longitude);

                // Get weather data for the catch time (or current time if no catch time specified)
                var catchTime = catchRecord.CatchTime ?? DateTime.Now;
                var weatherData = await _weatherService.GetWeatherForTimeAsync(latitude, longitude, catchTime);

                if (weatherData == null || !weatherData.IsSuccessful)
                {
                    _logger.LogWarning("Failed to retrieve weather data for catch {CatchId}: {Error}", 
                        catchRecord.Id, weatherData?.ErrorMessage ?? "Unknown error");
                    return false;
                }

                // Populate catch weather fields
                catchRecord.WeatherConditions = weatherData.WeatherConditions;
                catchRecord.Temperature = weatherData.Temperature;
                catchRecord.WindDirection = weatherData.WindDirection;
                catchRecord.WindSpeed = weatherData.WindSpeed;
                catchRecord.BarometricPressure = weatherData.BarometricPressure;
                catchRecord.Humidity = weatherData.Humidity;
                catchRecord.WeatherDescription = weatherData.Description;
                catchRecord.WeatherCapturedAt = DateTime.UtcNow;

                // Calculate and populate moon phase data
                var moonPhase = _moonPhaseService.GetMoonPhase(catchTime, (double)latitude, (double)longitude);
                catchRecord.MoonPhaseName = moonPhase.PhaseName;
                catchRecord.MoonIllumination = moonPhase.IlluminationPercentage;
                catchRecord.MoonAge = moonPhase.Age;
                catchRecord.MoonIcon = moonPhase.Icon;
                catchRecord.FishingQuality = moonPhase.FishingQuality;
                catchRecord.MoonFishingTip = moonPhase.FishingTip;
                catchRecord.MoonDataCapturedAt = DateTime.UtcNow;

                _logger.LogInformation("Successfully populated weather and moon data for catch {CatchId}: {Conditions}, {Temperature}°F, {MoonPhase}", 
                    catchRecord.Id, weatherData.WeatherConditions, weatherData.Temperature, moonPhase.PhaseName);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error populating weather data for catch {CatchId}", catchRecord.Id);
                return false;
            }
        }

        /// <summary>
        /// Creates a summary weather string for display purposes
        /// </summary>
        public static string GetWeatherSummary(Catch catchRecord)
        {
            if (string.IsNullOrEmpty(catchRecord.WeatherConditions))
                return "Weather data not available";

            var summary = catchRecord.WeatherConditions;
            
            if (catchRecord.Temperature.HasValue)
                summary += $", {catchRecord.Temperature:F0}°F";
            
            if (!string.IsNullOrEmpty(catchRecord.WindDirection) && catchRecord.WindSpeed.HasValue)
                summary += $", Wind: {catchRecord.WindDirection} {catchRecord.WindSpeed:F0} mph";
            
            if (catchRecord.BarometricPressure.HasValue)
                summary += $", Pressure: {catchRecord.BarometricPressure:F1} hPa";

            return summary;
        }

        /// <summary>
        /// Gets detailed weather information for display
        /// </summary>
        public static WeatherDisplayInfo GetWeatherDisplay(Catch catchRecord)
        {
            return new WeatherDisplayInfo
            {
                HasWeatherData = !string.IsNullOrEmpty(catchRecord.WeatherConditions),
                Conditions = catchRecord.WeatherConditions ?? "Unknown",
                Temperature = catchRecord.Temperature,
                WindDirection = catchRecord.WindDirection,
                WindSpeed = catchRecord.WindSpeed,
                Pressure = catchRecord.BarometricPressure,
                Humidity = catchRecord.Humidity,
                Description = catchRecord.WeatherDescription,
                CapturedAt = catchRecord.WeatherCapturedAt,
                Summary = GetWeatherSummary(catchRecord)
            };
        }
    }

    public class WeatherDisplayInfo
    {
        public bool HasWeatherData { get; set; }
        public string Conditions { get; set; } = string.Empty;
        public decimal? Temperature { get; set; }
        public string? WindDirection { get; set; }
        public decimal? WindSpeed { get; set; }
        public decimal? Pressure { get; set; }
        public int? Humidity { get; set; }
        public string? Description { get; set; }
        public DateTime? CapturedAt { get; set; }
        public string Summary { get; set; } = string.Empty;
    }
}