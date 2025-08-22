namespace Members.Services
{
    /// <summary>
    /// Service for calculating tidal information using harmonic analysis algorithms.
    /// Based on astronomical calculations for lunar and solar gravitational effects.
    /// </summary>
    public interface ITideService
    {
        /// <summary>
        /// Get comprehensive tide information for a specific date, time and location
        /// </summary>
        /// <param name="date">Date and time to calculate tides for</param>
        /// <param name="latitude">Location latitude</param>
        /// <param name="longitude">Location longitude</param>
        /// <returns>Complete tide information</returns>
        TideInfo GetTideInfo(DateTime date, double latitude, double longitude);

        /// <summary>
        /// Get current tide information for a location
        /// </summary>
        /// <param name="latitude">Location latitude</param>
        /// <param name="longitude">Location longitude</param>
        /// <returns>Current tide information</returns>
        TideInfo GetCurrentTideInfo(double latitude, double longitude);

        /// <summary>
        /// Check if location is coastal (has meaningful tidal effects)
        /// </summary>
        /// <param name="latitude">Location latitude</param>
        /// <param name="longitude">Location longitude</param>
        /// <returns>True if location has significant tidal effects</returns>
        bool IsCoastalLocation(double latitude, double longitude);

        /// <summary>
        /// Get next high and low tide times for a location
        /// </summary>
        /// <param name="fromDate">Starting date to search from</param>
        /// <param name="latitude">Location latitude</param>
        /// <param name="longitude">Location longitude</param>
        /// <returns>Next tide event information</returns>
        NextTideInfo GetNextTideEvents(DateTime fromDate, double latitude, double longitude);
    }

    /// <summary>
    /// Complete tide information for a specific time and location
    /// </summary>
    public class TideInfo
    {
        /// <summary>
        /// Date and time for this tide calculation
        /// </summary>
        public DateTime DateTime { get; set; }

        /// <summary>
        /// Current tide height in feet (relative to mean low water)
        /// </summary>
        public double TideHeight { get; set; }

        /// <summary>
        /// Current tide state (High, Low, Rising, Falling)
        /// </summary>
        public string TideState { get; set; } = string.Empty;

        /// <summary>
        /// Tide range description (Spring, Neap, Normal)
        /// </summary>
        public string TideRange { get; set; } = string.Empty;

        /// <summary>
        /// Time until next tide change
        /// </summary>
        public TimeSpan TimeToNextChange { get; set; }

        /// <summary>
        /// Whether this location has significant tidal effects
        /// </summary>
        public bool IsCoastal { get; set; }

        /// <summary>
        /// Fishing recommendation based on tide state
        /// </summary>
        public string FishingRecommendation { get; set; } = string.Empty;

        /// <summary>
        /// Next high tide time
        /// </summary>
        public DateTime? NextHighTide { get; set; }

        /// <summary>
        /// Next low tide time
        /// </summary>
        public DateTime? NextLowTide { get; set; }

        /// <summary>
        /// Tidal coefficient (measure of tide strength, 20-120)
        /// </summary>
        public int TidalCoefficient { get; set; }
    }

    /// <summary>
    /// Information about upcoming tide events
    /// </summary>
    public class NextTideInfo
    {
        /// <summary>
        /// Next high tide time
        /// </summary>
        public DateTime NextHighTide { get; set; }

        /// <summary>
        /// Next low tide time
        /// </summary>
        public DateTime NextLowTide { get; set; }

        /// <summary>
        /// Expected height of next high tide
        /// </summary>
        public double NextHighTideHeight { get; set; }

        /// <summary>
        /// Expected height of next low tide
        /// </summary>
        public double NextLowTideHeight { get; set; }

        /// <summary>
        /// Fishing recommendation for upcoming tides
        /// </summary>
        public string FishingRecommendation { get; set; } = string.Empty;
    }
}