namespace Members.Services
{
    /// <summary>
    /// Service for calculating moon phases using astronomical algorithms.
    /// Based on Jean Meeus "Astronomical Algorithms" for precise lunar calculations.
    /// </summary>
    public interface IMoonPhaseService
    {
        /// <summary>
        /// Get comprehensive moon phase information for a specific date and location
        /// </summary>
        /// <param name="date">Date to calculate moon phase for</param>
        /// <param name="latitude">Location latitude (optional, for more precise calculations)</param>
        /// <param name="longitude">Location longitude (optional, for more precise calculations)</param>
        /// <returns>Complete moon phase information</returns>
        MoonPhaseInfo GetMoonPhase(DateTime date, double? latitude = null, double? longitude = null);

        /// <summary>
        /// Get moon phase information for current date/time
        /// </summary>
        /// <param name="latitude">Location latitude (optional)</param>
        /// <param name="longitude">Location longitude (optional)</param>
        /// <returns>Current moon phase information</returns>
        MoonPhaseInfo GetCurrentMoonPhase(double? latitude = null, double? longitude = null);

        /// <summary>
        /// Get moon phase icon class for UI display
        /// </summary>
        /// <param name="illuminationPercentage">Moon illumination percentage (0-100)</param>
        /// <param name="isWaxing">Whether moon is waxing (growing) or waning (shrinking)</param>
        /// <returns>CSS class or unicode symbol for moon phase icon</returns>
        string GetPhaseIcon(double illuminationPercentage, bool isWaxing);

        /// <summary>
        /// Get fishing quality rating based on moon phase
        /// </summary>
        /// <param name="moonPhase">Moon phase information</param>
        /// <returns>Fishing quality rating (Excellent, Good, Fair, Poor)</returns>
        string GetFishingQuality(MoonPhaseInfo moonPhase);

        /// <summary>
        /// Get next major moon phase (New, First Quarter, Full, Last Quarter)
        /// </summary>
        /// <param name="fromDate">Starting date to search from</param>
        /// <returns>Information about the next major moon phase</returns>
        NextMoonPhase GetNextMajorPhase(DateTime fromDate);
    }

    /// <summary>
    /// Complete moon phase information
    /// </summary>
    public class MoonPhaseInfo
    {
        /// <summary>
        /// Date and time for this moon phase calculation
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Moon illumination percentage (0-100)
        /// 0 = New Moon, 50 = Half Moon, 100 = Full Moon
        /// </summary>
        public double IlluminationPercentage { get; set; }

        /// <summary>
        /// Days since new moon (0-29.5)
        /// </summary>
        public double Age { get; set; }

        /// <summary>
        /// Phase name (New Moon, Waxing Crescent, First Quarter, etc.)
        /// </summary>
        public string PhaseName { get; set; } = string.Empty;

        /// <summary>
        /// Short phase description for UI display
        /// </summary>
        public string PhaseDescription { get; set; } = string.Empty;

        /// <summary>
        /// Whether the moon is waxing (growing) or waning (shrinking)
        /// </summary>
        public bool IsWaxing { get; set; }

        /// <summary>
        /// CSS class or unicode symbol for displaying moon phase icon
        /// </summary>
        public string Icon { get; set; } = string.Empty;

        /// <summary>
        /// Fishing quality rating based on moon phase
        /// </summary>
        public string FishingQuality { get; set; } = string.Empty;

        /// <summary>
        /// Fishing tips based on current moon phase
        /// </summary>
        public string FishingTip { get; set; } = string.Empty;

        /// <summary>
        /// Moon rise time (if location provided)
        /// </summary>
        public TimeSpan? MoonRise { get; set; }

        /// <summary>
        /// Moon set time (if location provided)
        /// </summary>
        public TimeSpan? MoonSet { get; set; }
    }

    /// <summary>
    /// Information about upcoming major moon phases
    /// </summary>
    public class NextMoonPhase
    {
        /// <summary>
        /// Date of the next major phase
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Type of moon phase (New, First Quarter, Full, Last Quarter)
        /// </summary>
        public string PhaseType { get; set; } = string.Empty;

        /// <summary>
        /// Days until this phase occurs
        /// </summary>
        public int DaysUntil { get; set; }

        /// <summary>
        /// Fishing recommendation for this phase
        /// </summary>
        public string FishingRecommendation { get; set; } = string.Empty;
    }
}