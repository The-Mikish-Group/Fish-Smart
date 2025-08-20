using Members.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Members.Controllers
{
    [Authorize]
    public class WeatherTestController : Controller
    {
        private readonly IWeatherService _weatherService;
        private readonly ILogger<WeatherTestController> _logger;

        public WeatherTestController(IWeatherService weatherService, ILogger<WeatherTestController> logger)
        {
            _weatherService = weatherService;
            _logger = logger;
        }

        // GET: WeatherTest
        public IActionResult Index()
        {
            return View();
        }

        // AJAX endpoint for weather testing
        [HttpGet]
        public async Task<IActionResult> GetWeatherTest(double lat, double lng)
        {
            try
            {
                _logger.LogInformation("WeatherTest: GetWeatherTest called with lat={Lat}, lng={Lng}", lat, lng);
                Console.WriteLine($"WeatherTest: GetWeatherTest called with lat={lat}, lng={lng}");
                
                var weatherData = await _weatherService.GetCurrentWeatherAsync((decimal)lat, (decimal)lng);
                
                _logger.LogInformation("WeatherTest: Weather service returned: IsSuccessful={IsSuccessful}, Error={Error}", 
                    weatherData?.IsSuccessful, weatherData?.ErrorMessage);
                Console.WriteLine($"WeatherTest: Weather service returned: IsSuccessful={weatherData?.IsSuccessful}, Error={weatherData?.ErrorMessage}");
                
                var result = new
                {
                    isSuccessful = weatherData?.IsSuccessful ?? false,
                    weatherConditions = weatherData?.WeatherConditions,
                    temperature = weatherData?.Temperature,
                    windDirection = weatherData?.WindDirection,
                    windSpeed = weatherData?.WindSpeed,
                    barometricPressure = weatherData?.BarometricPressure,
                    humidity = weatherData?.Humidity,
                    visibility = weatherData?.Visibility,
                    description = weatherData?.Description,
                    errorMessage = weatherData?.ErrorMessage,
                    requestTime = weatherData?.RequestTime,
                    debugInfo = new
                    {
                        inputLat = lat,
                        inputLng = lng,
                        convertedLat = (decimal)lat,
                        convertedLng = (decimal)lng,
                        timestamp = DateTime.Now
                    }
                };
                
                Console.WriteLine($"WeatherTest: Returning JSON: {System.Text.Json.JsonSerializer.Serialize(result)}");
                
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WeatherTest: Exception in GetWeatherTest for {Lat}, {Lng}", lat, lng);
                Console.WriteLine($"WeatherTest: Exception - {ex.Message}");
                Console.WriteLine($"WeatherTest: Stack trace - {ex.StackTrace}");
                
                return Json(new
                {
                    isSuccessful = false,
                    errorMessage = ex.Message,
                    exceptionType = ex.GetType().Name,
                    stackTrace = ex.StackTrace
                });
            }
        }

        // Test specific coordinates
        [HttpPost]
        public async Task<IActionResult> TestCoordinates(decimal latitude, decimal longitude)
        {
            try
            {
                Console.WriteLine($"WeatherTest: TestCoordinates called with lat={latitude}, lng={longitude}");
                
                var weatherData = await _weatherService.GetCurrentWeatherAsync(latitude, longitude);
                
                Console.WriteLine($"WeatherTest: Direct service call returned: IsSuccessful={weatherData?.IsSuccessful}");
                
                ViewBag.TestResult = weatherData;
                ViewBag.TestCoordinates = $"{latitude}, {longitude}";
                ViewBag.TestTime = DateTime.Now;
                
                return View("Index");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WeatherTest: TestCoordinates exception - {ex.Message}");
                ViewBag.TestError = ex.Message;
                ViewBag.TestCoordinates = $"{latitude}, {longitude}";
                ViewBag.TestTime = DateTime.Now;
                
                return View("Index");
            }
        }
    }
}