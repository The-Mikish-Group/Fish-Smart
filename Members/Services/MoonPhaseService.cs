using System;

namespace Members.Services
{
    /// <summary>
    /// Moon phase calculation service using Jean Meeus astronomical algorithms
    /// Implements precise lunar phase calculations without external API dependencies
    /// </summary>
    public class MoonPhaseService : IMoonPhaseService
    {
        private readonly ILogger<MoonPhaseService> _logger;

        // Astronomical constants
        private const double SynodicMonth = 29.530588853; // Average length of lunar cycle in days
        private const double MeanNewMoonEpoch = 2451550.09766; // Julian day of known new moon (Jan 6, 2000)

        public MoonPhaseService(ILogger<MoonPhaseService> logger)
        {
            _logger = logger;
        }

        public MoonPhaseInfo GetMoonPhase(DateTime date, double? latitude = null, double? longitude = null)
        {
            try
            {
                var julianDay = ToJulianDay(date);
                var moonAge = CalculateMoonAge(julianDay);
                var illumination = CalculateIllumination(moonAge);
                var isWaxing = IsWaxing(moonAge);
                var phaseName = GetPhaseName(illumination, isWaxing);

                var moonPhase = new MoonPhaseInfo
                {
                    Date = date,
                    Age = moonAge,
                    IlluminationPercentage = illumination * 100,
                    IsWaxing = isWaxing,
                    PhaseName = phaseName,
                    PhaseDescription = GetPhaseDescription(phaseName),
                    Icon = GetPhaseIcon(illumination * 100, isWaxing),
                    FishingQuality = GetFishingQuality(illumination, moonAge),
                    FishingTip = GetFishingTip(phaseName, illumination)
                };

                // Calculate moon rise/set times if location is provided
                if (latitude.HasValue && longitude.HasValue)
                {
                    var (moonRise, moonSet) = CalculateMoonRiseSet(julianDay, latitude.Value, longitude.Value);
                    moonPhase.MoonRise = moonRise;
                    moonPhase.MoonSet = moonSet;
                }

                _logger.LogDebug("Calculated moon phase for {Date}: {PhaseName} ({Illumination:F1}%)", 
                    date, phaseName, illumination * 100);

                return moonPhase;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating moon phase for date {Date}", date);
                throw;
            }
        }

        public MoonPhaseInfo GetCurrentMoonPhase(double? latitude = null, double? longitude = null)
        {
            return GetMoonPhase(DateTime.Now, latitude, longitude);
        }

        public string GetPhaseIcon(double illuminationPercentage, bool isWaxing)
        {
            // Return Unicode moon phase symbols or CSS classes
            return illuminationPercentage switch
            {
                < 1 => "🌑", // New Moon
                < 25 when isWaxing => "🌒", // Waxing Crescent
                < 25 => "🌘", // Waning Crescent
                < 49 when isWaxing => "🌓", // First Quarter
                < 49 => "🌗", // Last Quarter
                < 75 when isWaxing => "🌔", // Waxing Gibbous
                < 75 => "🌖", // Waning Gibbous
                < 99 => "🌕", // Full Moon
                _ => "🌑" // Default to New Moon
            };
        }

        public string GetFishingQuality(MoonPhaseInfo moonPhase)
        {
            return GetFishingQuality(moonPhase.IlluminationPercentage / 100, moonPhase.Age);
        }

        public NextMoonPhase GetNextMajorPhase(DateTime fromDate)
        {
            var julianDay = ToJulianDay(fromDate);
            var moonAge = CalculateMoonAge(julianDay);

            // Calculate days to next major phase
            double daysToNext;
            string phaseType;
            string recommendation;

            if (moonAge < 7.4) // Before First Quarter
            {
                daysToNext = 7.4 - moonAge;
                phaseType = "First Quarter";
                recommendation = "Good fishing as moon transitions to higher visibility";
            }
            else if (moonAge < 14.8) // Before Full Moon
            {
                daysToNext = 14.8 - moonAge;
                phaseType = "Full Moon";
                recommendation = "Excellent night fishing conditions";
            }
            else if (moonAge < 22.1) // Before Last Quarter
            {
                daysToNext = 22.1 - moonAge;
                phaseType = "Last Quarter";
                recommendation = "Good early morning fishing";
            }
            else // Before New Moon
            {
                daysToNext = (SynodicMonth - moonAge);
                phaseType = "New Moon";
                recommendation = "Prime time for all fishing - minimal moon interference";
            }

            return new NextMoonPhase
            {
                Date = fromDate.AddDays(daysToNext),
                PhaseType = phaseType,
                DaysUntil = (int)Math.Ceiling(daysToNext),
                FishingRecommendation = recommendation
            };
        }

        #region Private Calculation Methods

        /// <summary>
        /// Convert DateTime to Julian Day Number (Jean Meeus algorithm)
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

        /// <summary>
        /// Calculate moon age in days since last new moon
        /// </summary>
        private static double CalculateMoonAge(double julianDay)
        {
            var daysSinceEpoch = julianDay - MeanNewMoonEpoch;
            var cycles = daysSinceEpoch / SynodicMonth;
            var age = (cycles - Math.Floor(cycles)) * SynodicMonth;
            
            return age < 0 ? age + SynodicMonth : age;
        }

        /// <summary>
        /// Calculate moon illumination percentage (0.0 to 1.0)
        /// </summary>
        private static double CalculateIllumination(double moonAge)
        {
            var phase = moonAge / SynodicMonth * 2 * Math.PI;
            return (1 - Math.Cos(phase)) / 2;
        }

        /// <summary>
        /// Determine if moon is waxing (growing) or waning (shrinking)
        /// </summary>
        private static bool IsWaxing(double moonAge)
        {
            return moonAge < SynodicMonth / 2;
        }

        /// <summary>
        /// Get descriptive phase name based on illumination and waxing state
        /// </summary>
        private static string GetPhaseName(double illumination, bool isWaxing)
        {
            var percentage = illumination * 100;

            return percentage switch
            {
                < 1 => "New Moon",
                < 25 when isWaxing => "Waxing Crescent",
                < 25 => "Waning Crescent",
                >= 25 and < 75 when isWaxing => "First Quarter",
                >= 25 and < 75 => "Last Quarter",
                < 99 when isWaxing => "Waxing Gibbous",
                < 99 => "Waning Gibbous",
                _ => "Full Moon"
            };
        }

        /// <summary>
        /// Get user-friendly phase description
        /// </summary>
        private static string GetPhaseDescription(string phaseName)
        {
            return phaseName switch
            {
                "New Moon" => "Moon is not visible",
                "Waxing Crescent" => "Thin crescent, growing",
                "First Quarter" => "Half moon, growing",
                "Waxing Gibbous" => "Nearly full, growing",
                "Full Moon" => "Moon is fully illuminated",
                "Waning Gibbous" => "Nearly full, shrinking",
                "Last Quarter" => "Half moon, shrinking",
                "Waning Crescent" => "Thin crescent, shrinking",
                _ => "Unknown phase"
            };
        }

        /// <summary>
        /// Calculate fishing quality based on moon phase
        /// </summary>
        private static string GetFishingQuality(double illumination, double moonAge)
        {
            // New moon and full moon periods are generally best for fishing
            if (illumination < 0.1 || illumination > 0.9)
                return "Excellent";
            
            // Quarter moons provide good fishing
            if (Math.Abs(moonAge - 7.4) < 2 || Math.Abs(moonAge - 22.1) < 2)
                return "Good";
            
            // Gibbous phases are fair
            if (illumination > 0.6 || illumination < 0.4)
                return "Fair";
            
            return "Good";
        }

        /// <summary>
        /// Get fishing tip based on moon phase
        /// </summary>
        private static string GetFishingTip(string phaseName, double illumination)
        {
            return phaseName switch
            {
                "New Moon" => "Prime time! Fish are more active with minimal moonlight. Try night fishing.",
                "Waxing Crescent" => "Good evening fishing as moon sets early. Focus on dusk hours.",
                "First Quarter" => "Excellent for afternoon and evening fishing. Moon provides good visibility.",
                "Waxing Gibbous" => "Great for night fishing with bright moonlight. Fish may feed longer.",
                "Full Moon" => "Outstanding night fishing conditions! Fish are very active in bright moonlight.",
                "Waning Gibbous" => "Good early morning fishing as moon sets later. Try dawn hours.",
                "Last Quarter" => "Perfect for early morning fishing with moonlight. Focus on pre-dawn.",
                "Waning Crescent" => "Best morning fishing as moon rises later. Try sunrise hours.",
                _ => "Good fishing conditions regardless of moon phase!"
            };
        }

        /// <summary>
        /// Calculate moon rise and set times for specific location
        /// Simplified calculation - for precise times would need more complex algorithms
        /// </summary>
        private static (TimeSpan? moonRise, TimeSpan? moonSet) CalculateMoonRiseSet(double julianDay, double latitude, double longitude)
        {
            // This is a simplified calculation
            // For production use, implement full Meeus rise/set algorithms
            try
            {
                var moonAge = CalculateMoonAge(julianDay);
                
                // Approximate moon rise/set based on phase
                // New moon rises/sets with sun, full moon is opposite
                var phaseOffset = (moonAge / SynodicMonth) * 24; // Hours offset from sun
                
                // Simplified calculation - would need proper implementation for accuracy
                var baseRise = TimeSpan.FromHours(6 + phaseOffset); // Approximate
                var baseSet = TimeSpan.FromHours(18 + phaseOffset);
                
                // Normalize to 24-hour format
                if (baseRise.TotalHours >= 24) baseRise = baseRise.Subtract(TimeSpan.FromDays(1));
                if (baseSet.TotalHours >= 24) baseSet = baseSet.Subtract(TimeSpan.FromDays(1));
                if (baseRise.TotalHours < 0) baseRise = baseRise.Add(TimeSpan.FromDays(1));
                if (baseSet.TotalHours < 0) baseSet = baseSet.Add(TimeSpan.FromDays(1));
                
                return (baseRise, baseSet);
            }
            catch
            {
                return (null, null);
            }
        }

        #endregion
    }
}