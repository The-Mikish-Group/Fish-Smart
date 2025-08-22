using System;

namespace Members.Services
{
    /// <summary>
    /// Tide calculation service using harmonic analysis algorithms
    /// Implements simplified tidal calculations based on lunar and solar positions
    /// </summary>
    public class TideService : ITideService
    {
        private readonly ILogger<TideService> _logger;

        // Tidal harmonic constants (simplified for basic calculation)
        private const double LunarDay = 24.84; // Lunar day in hours
        private const double SemiDiurnalPeriod = 12.42; // Semi-diurnal tide period in hours

        public TideService(ILogger<TideService> logger)
        {
            _logger = logger;
        }

        public TideInfo GetTideInfo(DateTime date, double latitude, double longitude)
        {
            try
            {
                var isCoastal = IsCoastalLocation(latitude, longitude);
                
                if (!isCoastal)
                {
                    return new TideInfo
                    {
                        DateTime = date,
                        TideHeight = 0,
                        TideState = "Inland",
                        TideRange = "N/A",
                        IsCoastal = false,
                        FishingRecommendation = "Tidal effects minimal - focus on weather and moon phase",
                        TidalCoefficient = 0
                    };
                }

                var tideHeight = CalculateTideHeight(date, latitude, longitude);
                var tideState = DetermineTideState(date, latitude, longitude);
                var tideRange = DetermineTideRange(date, latitude, longitude);
                var nextEvents = GetNextTideEvents(date, latitude, longitude);
                var tidalCoefficient = CalculateTidalCoefficient(date);

                return new TideInfo
                {
                    DateTime = date,
                    TideHeight = tideHeight,
                    TideState = tideState,
                    TideRange = tideRange,
                    IsCoastal = true,
                    FishingRecommendation = GetFishingRecommendation(tideState, tideRange),
                    NextHighTide = nextEvents.NextHighTide,
                    NextLowTide = nextEvents.NextLowTide,
                    TidalCoefficient = tidalCoefficient,
                    TimeToNextChange = CalculateTimeToNextChange(date, latitude, longitude)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating tide information for {Date} at {Latitude}, {Longitude}", 
                    date, latitude, longitude);
                throw;
            }
        }

        public TideInfo GetCurrentTideInfo(double latitude, double longitude)
        {
            return GetTideInfo(DateTime.Now, latitude, longitude);
        }

        public bool IsCoastalLocation(double latitude, double longitude)
        {
            // Simplified coastal detection based on known geographic constraints
            // More sophisticated versions would use coastline databases
            
            // Continental US coastal approximations
            var isUSEastCoast = longitude > -85 && longitude < -65 && latitude > 25 && latitude < 45;
            var isUSWestCoast = longitude > -125 && longitude < -115 && latitude > 32 && latitude < 49;
            var isUSGulfCoast = longitude > -98 && longitude < -80 && latitude > 25 && latitude < 31;
            var isGreatLakes = longitude > -93 && longitude < -76 && latitude > 41 && latitude < 49;
            
            // European coasts
            var isEuropeanCoast = longitude > -15 && longitude < 15 && latitude > 35 && latitude < 72;
            
            // Other major coastal regions (simplified)
            var isOtherCoastal = Math.Abs(latitude) < 70; // Exclude polar regions
            
            return isUSEastCoast || isUSWestCoast || isUSGulfCoast || isGreatLakes || 
                   isEuropeanCoast || (isOtherCoastal && IsNearCoast(latitude, longitude));
        }

        public NextTideInfo GetNextTideEvents(DateTime fromDate, double latitude, double longitude)
        {
            if (!IsCoastalLocation(latitude, longitude))
            {
                return new NextTideInfo
                {
                    NextHighTide = fromDate.AddHours(6),
                    NextLowTide = fromDate.AddHours(12),
                    NextHighTideHeight = 0,
                    NextLowTideHeight = 0,
                    FishingRecommendation = "Inland location - tides not applicable"
                };
            }

            var nextHigh = CalculateNextTideEvent(fromDate, latitude, longitude, true);
            var nextLow = CalculateNextTideEvent(fromDate, latitude, longitude, false);

            return new NextTideInfo
            {
                NextHighTide = nextHigh,
                NextLowTide = nextLow,
                NextHighTideHeight = CalculateTideHeight(nextHigh, latitude, longitude),
                NextLowTideHeight = CalculateTideHeight(nextLow, latitude, longitude),
                FishingRecommendation = GetTideEventFishingRecommendation(nextHigh, nextLow, fromDate)
            };
        }

        #region Private Calculation Methods

        /// <summary>
        /// Calculate tide height using simplified harmonic analysis
        /// </summary>
        private double CalculateTideHeight(DateTime date, double latitude, double longitude)
        {
            // Convert to Julian day for astronomical calculations
            var julianDay = ToJulianDay(date);
            
            // Calculate lunar position influence
            var lunarInfluence = CalculateLunarInfluence(julianDay, latitude);
            
            // Calculate solar influence
            var solarInfluence = CalculateSolarInfluence(julianDay, latitude);
            
            // Calculate base tide amplitude based on location
            var baseAmplitude = CalculateBaseAmplitude(latitude, longitude);
            
            // Combine influences
            var totalInfluence = lunarInfluence + (solarInfluence * 0.46); // Solar influence is ~46% of lunar
            
            // Calculate current tide height
            var tideHeight = baseAmplitude * Math.Sin(totalInfluence);
            
            return Math.Round(tideHeight, 2);
        }

        /// <summary>
        /// Determine current tide state (High, Low, Rising, Falling)
        /// </summary>
        private string DetermineTideState(DateTime date, double latitude, double longitude)
        {
            var currentHeight = CalculateTideHeight(date, latitude, longitude);
            var futureHeight = CalculateTideHeight(date.AddMinutes(30), latitude, longitude);
            var pastHeight = CalculateTideHeight(date.AddMinutes(-30), latitude, longitude);
            
            var trend = futureHeight - pastHeight;
            
            if (Math.Abs(trend) < 0.1)
            {
                return currentHeight > 0 ? "High Tide" : "Low Tide";
            }
            
            return trend > 0 ? "Rising" : "Falling";
        }

        /// <summary>
        /// Determine tide range (Spring, Neap, Normal)
        /// </summary>
        private string DetermineTideRange(DateTime date, double latitude, double longitude)
        {
            var tidalCoefficient = CalculateTidalCoefficient(date);
            
            if (tidalCoefficient > 95)
                return "Spring Tide";
            else if (tidalCoefficient < 45)
                return "Neap Tide";
            else
                return "Normal Tide";
        }

        /// <summary>
        /// Calculate tidal coefficient (20-120 scale)
        /// </summary>
        private int CalculateTidalCoefficient(DateTime date)
        {
            var julianDay = ToJulianDay(date);
            
            // Calculate moon phase influence on tide strength
            var daysSinceNewMoon = ((julianDay - 2451550.09766) % 29.530588853);
            var moonPhaseInfluence = Math.Cos(2 * Math.PI * daysSinceNewMoon / 29.530588853);
            
            // Calculate sun-moon alignment influence
            var solarPosition = (julianDay % 365.25) / 365.25 * 2 * Math.PI;
            var solarInfluence = Math.Cos(solarPosition);
            
            // Combine influences to get coefficient (20-120 range)
            var coefficient = 70 + (moonPhaseInfluence * 25) + (solarInfluence * 25);
            
            return Math.Max(20, Math.Min(120, (int)Math.Round(coefficient)));
        }

        /// <summary>
        /// Calculate time until next tide change
        /// </summary>
        private TimeSpan CalculateTimeToNextChange(DateTime date, double latitude, double longitude)
        {
            // Semi-diurnal tides change approximately every 6 hours 12 minutes
            var currentTidePhase = (ToJulianDay(date) * 24) % SemiDiurnalPeriod;
            var timeToNextHalf = (SemiDiurnalPeriod / 2) - (currentTidePhase % (SemiDiurnalPeriod / 2));
            
            return TimeSpan.FromHours(timeToNextHalf);
        }

        /// <summary>
        /// Calculate lunar influence on tides
        /// </summary>
        private double CalculateLunarInfluence(double julianDay, double latitude)
        {
            // Simplified lunar position calculation
            var lunarPhase = ((julianDay - 2451550.09766) / 29.530588853) * 2 * Math.PI;
            var lunarDistance = 1 + 0.055 * Math.Cos(lunarPhase); // Approximate distance variation
            
            // Latitude effect (tides are stronger near equator)
            var latitudeEffect = Math.Cos(latitude * Math.PI / 180);
            
            return (lunarPhase / Math.Pow(lunarDistance, 3)) * latitudeEffect;
        }

        /// <summary>
        /// Calculate solar influence on tides
        /// </summary>
        private double CalculateSolarInfluence(double julianDay, double latitude)
        {
            // Solar position (simplified)
            var solarPhase = ((julianDay % 365.25) / 365.25) * 2 * Math.PI;
            var latitudeEffect = Math.Cos(latitude * Math.PI / 180);
            
            return solarPhase * latitudeEffect;
        }

        /// <summary>
        /// Calculate base tide amplitude for location
        /// </summary>
        private double CalculateBaseAmplitude(double latitude, double longitude)
        {
            // Simplified amplitude calculation based on geographic factors
            // Real implementation would use detailed bathymetry and coastal geometry
            
            if (!IsCoastalLocation(latitude, longitude))
                return 0;
            
            // Atlantic coast tends to have larger tides
            if (longitude > -85 && longitude < -65 && latitude > 25 && latitude < 45)
                return 4.0; // Atlantic coast
            
            // Pacific coast
            if (longitude > -125 && longitude < -115 && latitude > 32 && latitude < 49)
                return 3.0; // Pacific coast
            
            // Gulf coast
            if (longitude > -98 && longitude < -80 && latitude > 25 && latitude < 31)
                return 2.0; // Gulf coast
            
            // Great Lakes
            if (longitude > -93 && longitude < -76 && latitude > 41 && latitude < 49)
                return 0.5; // Great Lakes (minimal tides)
            
            // Default coastal amplitude
            return 2.5;
        }

        /// <summary>
        /// Check if location is near coast (simplified)
        /// </summary>
        private bool IsNearCoast(double latitude, double longitude)
        {
            // Very simplified - real implementation would use detailed coastline data
            // This is just a placeholder for demonstration
            return Math.Abs(latitude) < 60; // Exclude polar regions
        }

        /// <summary>
        /// Calculate next tide event (high or low)
        /// </summary>
        private DateTime CalculateNextTideEvent(DateTime fromDate, double latitude, double longitude, bool isHighTide)
        {
            // Start checking from current time
            var checkTime = fromDate;
            var currentState = DetermineTideState(checkTime, latitude, longitude);
            
            // Look ahead up to 24 hours
            for (int minutes = 15; minutes < 1440; minutes += 15)
            {
                checkTime = fromDate.AddMinutes(minutes);
                var newState = DetermineTideState(checkTime, latitude, longitude);
                
                if (isHighTide && newState == "High Tide" && currentState != "High Tide")
                    return checkTime;
                
                if (!isHighTide && newState == "Low Tide" && currentState != "Low Tide")
                    return checkTime;
                
                currentState = newState;
            }
            
            // Fallback - return estimated time based on semi-diurnal period
            var hoursToAdd = isHighTide ? SemiDiurnalPeriod / 2 : SemiDiurnalPeriod;
            return fromDate.AddHours(hoursToAdd);
        }

        /// <summary>
        /// Get fishing recommendation based on tide state
        /// </summary>
        private string GetFishingRecommendation(string tideState, string tideRange)
        {
            return tideState switch
            {
                "High Tide" => $"Excellent fishing! High tide brings fish closer to shore. {GetRangeAdvice(tideRange)}",
                "Low Tide" => $"Good for structure fishing. Fish concentrate in deeper channels. {GetRangeAdvice(tideRange)}",
                "Rising" => $"Prime fishing time! Moving water activates feeding. {GetRangeAdvice(tideRange)}",
                "Falling" => $"Great fishing! Outgoing tide moves baitfish, triggering strikes. {GetRangeAdvice(tideRange)}",
                "Inland" => "Focus on weather patterns and moon phase for best fishing times.",
                _ => "Good fishing conditions regardless of tide state."
            };
        }

        /// <summary>
        /// Get advice based on tide range
        /// </summary>
        private string GetRangeAdvice(string tideRange)
        {
            return tideRange switch
            {
                "Spring Tide" => "Strong tides - fish very active!",
                "Neap Tide" => "Gentle tides - consistent fishing.",
                _ => "Normal tide strength."
            };
        }

        /// <summary>
        /// Get fishing recommendation for upcoming tide events
        /// </summary>
        private string GetTideEventFishingRecommendation(DateTime nextHigh, DateTime nextLow, DateTime fromDate)
        {
            var timeToHigh = nextHigh - fromDate;
            var timeToLow = nextLow - fromDate;
            
            if (timeToHigh < timeToLow)
            {
                var hours = (int)timeToHigh.TotalHours;
                var minutes = timeToHigh.Minutes;
                return $"High tide in {hours}h {minutes}m - excellent fishing opportunity!";
            }
            else
            {
                var hours = (int)timeToLow.TotalHours;
                var minutes = timeToLow.Minutes;
                return $"Low tide in {hours}h {minutes}m - focus on deeper structures.";
            }
        }

        /// <summary>
        /// Convert DateTime to Julian Day Number
        /// </summary>
        private static double ToJulianDay(DateTime date)
        {
            var year = date.Year;
            var month = date.Month;
            var day = date.Day + (date.Hour + date.Minute / 60.0 + date.Second / 3600.0) / 24.0;

            if (month <= 2)
            {
                year--;
                month += 12;
            }

            var a = year / 100;
            var b = 2 - a + (a / 4);

            return Math.Floor(365.25 * (year + 4716)) + Math.Floor(30.6001 * (month + 1)) + day + b - 1524.5;
        }

        #endregion
    }
}