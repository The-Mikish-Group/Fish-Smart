using Members.Models;

namespace Members.Services
{
    public interface IWeatherService
    {
        /// <summary>
        /// Gets current weather data for the specified location
        /// </summary>
        /// <param name="latitude">Latitude coordinate</param>
        /// <param name="longitude">Longitude coordinate</param>
        /// <returns>Weather data or null if unavailable</returns>
        Task<WeatherData?> GetCurrentWeatherAsync(decimal latitude, decimal longitude);

        /// <summary>
        /// Gets weather data for a specific time and location (for historical records)
        /// </summary>
        /// <param name="latitude">Latitude coordinate</param>
        /// <param name="longitude">Longitude coordinate</param>
        /// <param name="dateTime">Date and time to get weather for</param>
        /// <returns>Weather data or null if unavailable</returns>
        Task<WeatherData?> GetWeatherForTimeAsync(decimal latitude, decimal longitude, DateTime dateTime);
    }

    public class WeatherData
    {
        public string WeatherConditions { get; set; } = string.Empty; // "Clear", "Clouds", "Rain", etc.
        public decimal Temperature { get; set; } // Temperature in Fahrenheit
        public decimal? FeelsLike { get; set; } // Feels like temperature
        public string WindDirection { get; set; } = string.Empty; // "N", "NE", "E", etc.
        public decimal WindSpeed { get; set; } // Wind speed in mph
        public decimal? BarometricPressure { get; set; } // Pressure in hPa
        public int? Humidity { get; set; } // Humidity percentage
        public decimal? Visibility { get; set; } // Visibility in miles
        public string Description { get; set; } = string.Empty; // Detailed description
        public DateTime RequestTime { get; set; } = DateTime.UtcNow;
        public bool IsSuccessful { get; set; } = false;
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Converts numeric wind direction to compass direction
        /// </summary>
        public static string DegreesToCompass(decimal degrees)
        {
            var directions = new[] { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW" };
            var index = (int)Math.Round(degrees / 22.5m) % 16;
            return directions[index];
        }

        /// <summary>
        /// Converts Kelvin to Fahrenheit
        /// </summary>
        public static decimal KelvinToFahrenheit(decimal kelvin)
        {
            return (kelvin - 273.15m) * 9 / 5 + 32;
        }

        /// <summary>
        /// Converts Celsius to Fahrenheit
        /// </summary>
        public static decimal CelsiusToFahrenheit(decimal celsius)
        {
            return celsius * 9 / 5 + 32;
        }

        /// <summary>
        /// Converts meters per second to miles per hour
        /// </summary>
        public static decimal MpsToMph(decimal mps)
        {
            return mps * 2.237m;
        }
    }
}