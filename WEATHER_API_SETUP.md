# Weather API Setup Instructions

## Getting OpenWeatherMap API Key

1. **Sign up for free**: Go to https://openweathermap.org/api
2. **Create account**: Register for a free account
3. **Get API key**: After registration, go to API keys section
4. **Copy API key**: Copy your free API key

## Free Tier Limits
- **1,000 API calls per day** (plenty for fishing app usage)
- **Current weather data** (perfect for catch recording)
- **5 day / 3 hour forecast** (could be useful for trip planning)

## Configuration

Replace `YOUR_OPENWEATHERMAP_API_KEY_HERE` in appsettings.json with your actual API key:

```json
"Weather": {
  "OpenWeatherMap": {
    "ApiKey": "your_actual_api_key_here"
  }
}
```

## Environment Variable (Recommended for Production)

For production, set the API key as an environment variable:
- Environment variable name: `WEATHER_OPENWEATHERMAP_APIKEY`
- This will override the appsettings.json value

## Testing the Integration

Once you have:
1. ✅ Run the SQL script to add weather fields to Catch table
2. ✅ Added your OpenWeatherMap API key to configuration
3. ✅ Built and run the application

The weather service will automatically populate weather data when catches are recorded with location information.

## Weather Fields Captured

For each catch, the system will capture:
- **Weather Conditions**: Clear, Clouds, Rain, Snow, etc.
- **Temperature**: In Fahrenheit 
- **Wind Direction**: N, NE, E, SE, S, SW, W, NW
- **Wind Speed**: In miles per hour
- **Barometric Pressure**: In hPa (hectopascals)
- **Humidity**: Percentage
- **Weather Description**: Detailed description (e.g., "light rain", "scattered clouds")
- **Weather Captured At**: Timestamp when weather was retrieved

## How It Works

1. User records a catch with location (latitude/longitude)
2. System automatically calls OpenWeatherMap API
3. Weather data is captured and stored with the catch record
4. No additional user input required - fully automatic!