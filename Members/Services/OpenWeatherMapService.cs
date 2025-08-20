using Members.Models;
using System.Text.Json;

namespace Members.Services
{
    public class OpenWeatherMapService : IWeatherService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OpenWeatherMapService> _logger;
        private readonly string _apiKey;
        private const string BaseUrl = "https://api.openweathermap.org/data/2.5";

        public OpenWeatherMapService(
            HttpClient httpClient, 
            ILogger<OpenWeatherMapService> logger,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiKey = configuration["Weather:OpenWeatherMap:ApiKey"] ?? 
                     throw new InvalidOperationException("OpenWeatherMap API key not configured");
        }

        public async Task<WeatherData?> GetCurrentWeatherAsync(decimal latitude, decimal longitude)
        {
            try
            {
                var url = $"{BaseUrl}/weather?lat={latitude}&lon={longitude}&appid={_apiKey}&units=metric";
                
                _logger.LogInformation("Fetching current weather for location: {Latitude}, {Longitude}", latitude, longitude);
                
                // Add timeout to prevent hanging - cancel after 30 seconds
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var response = await _httpClient.GetAsync(url, cts.Token);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cts.Token);
                    _logger.LogError("Weather API error: {StatusCode} - {Content}", response.StatusCode, errorContent);
                    
                    return new WeatherData
                    {
                        IsSuccessful = false,
                        ErrorMessage = $"Weather API error: {response.StatusCode}"
                    };
                }

                var jsonContent = await response.Content.ReadAsStringAsync(cts.Token);
                var weatherResponse = JsonSerializer.Deserialize<OpenWeatherCurrentResponse>(jsonContent, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                if (weatherResponse == null)
                {
                    _logger.LogError("Failed to deserialize weather response");
                    return new WeatherData
                    {
                        IsSuccessful = false,
                        ErrorMessage = "Failed to parse weather data"
                    };
                }

                return ConvertToWeatherData(weatherResponse);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Weather request timed out for {Latitude}, {Longitude}", latitude, longitude);
                return new WeatherData
                {
                    IsSuccessful = false,
                    ErrorMessage = "Weather request timed out"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching current weather for {Latitude}, {Longitude}", latitude, longitude);
                return new WeatherData
                {
                    IsSuccessful = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<WeatherData?> GetWeatherForTimeAsync(decimal latitude, decimal longitude, DateTime dateTime)
        {
            // For now, just get current weather. Historical weather requires a paid plan.
            // Could implement with historical weather APIs later if needed.
            _logger.LogInformation("Historical weather requested for {DateTime}, falling back to current weather", dateTime);
            return await GetCurrentWeatherAsync(latitude, longitude);
        }

        private WeatherData ConvertToWeatherData(OpenWeatherCurrentResponse response)
        {
            var main = response.Weather?.FirstOrDefault();
            var windDirection = response.Wind?.Deg.HasValue == true 
                ? WeatherData.DegreesToCompass(response.Wind.Deg.Value) 
                : "Unknown";

            return new WeatherData
            {
                WeatherConditions = main?.Main ?? "Unknown",
                Description = main?.Description ?? "No description available",
                Temperature = WeatherData.CelsiusToFahrenheit(response.Main?.Temp ?? 0),
                FeelsLike = response.Main?.FeelsLike.HasValue == true 
                    ? WeatherData.CelsiusToFahrenheit(response.Main.FeelsLike.Value) 
                    : null,
                WindDirection = windDirection,
                WindSpeed = response.Wind?.Speed.HasValue == true 
                    ? WeatherData.MpsToMph(response.Wind.Speed.Value) 
                    : 0,
                BarometricPressure = response.Main?.Pressure,
                Humidity = response.Main?.Humidity,
                Visibility = response.Visibility.HasValue 
                    ? response.Visibility.Value / 1609m // Convert meters to miles
                    : null,
                IsSuccessful = true,
                RequestTime = DateTime.UtcNow
            };
        }
    }

    // Response models for OpenWeatherMap API
    public class OpenWeatherCurrentResponse
    {
        public WeatherInfo[]? Weather { get; set; }
        public MainWeatherData? Main { get; set; }
        public WindData? Wind { get; set; }
        public decimal? Visibility { get; set; }
        public long Dt { get; set; } // Unix timestamp
    }

    public class WeatherInfo
    {
        public string Main { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class MainWeatherData
    {
        public decimal Temp { get; set; }
        public decimal? FeelsLike { get; set; }
        public decimal? Pressure { get; set; }
        public int? Humidity { get; set; }
    }

    public class WindData
    {
        public decimal? Speed { get; set; }
        public decimal? Deg { get; set; }
    }
}