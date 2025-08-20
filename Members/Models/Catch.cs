using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Members.Models
{
    public class Catch
    {
        public int Id { get; set; }

        [Required]
        public int SessionId { get; set; }

        [Required]
        public int FishSpeciesId { get; set; }

        [Required]
        [Range(0, 999.99)]
        public decimal Size { get; set; }

        [Range(0, 999.99)]
        public decimal? Weight { get; set; }

        public DateTime? CatchTime { get; set; }

        // Catch Photo
        [StringLength(500)]
        public string? PhotoUrl { get; set; }

        // Image Composition Settings (Premium Features)
        [StringLength(500)]
        public string? CompositeImageUrl { get; set; }

        [StringLength(500)]
        public string? WatermarkedImageUrl { get; set; }

        public int? AvatarId { get; set; }
        public int? PoseId { get; set; }
        public int? BackgroundId { get; set; }
        public int? OutfitId { get; set; }

        // Premium Features
        public bool ShowSpeciesName { get; set; } = false;
        public bool ShowSize { get; set; } = false;

        public bool IsShared { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Weather Data (captured at time of catch)
        [StringLength(200)]
        public string? WeatherConditions { get; set; } // "Clear", "Clouds", "Rain", etc.

        public decimal? Temperature { get; set; } // Temperature in Fahrenheit

        [StringLength(50)]
        public string? WindDirection { get; set; } // "N", "NE", "E", etc.

        public decimal? WindSpeed { get; set; } // Wind speed in mph

        public decimal? BarometricPressure { get; set; } // Pressure in hPa

        public int? Humidity { get; set; } // Humidity percentage

        [StringLength(500)]
        public string? WeatherDescription { get; set; } // Full weather description

        public DateTime? WeatherCapturedAt { get; set; } // When weather was captured

        // Navigation properties
        [ForeignKey("SessionId")]
        public virtual FishingSession? Session { get; set; }

        [ForeignKey("FishSpeciesId")]
        public virtual FishSpecies? Species { get; set; }

        [ForeignKey("AvatarId")]
        public virtual UserAvatar? Avatar { get; set; }

        [ForeignKey("PoseId")]
        public virtual AvatarPose? Pose { get; set; }

        [ForeignKey("BackgroundId")]
        public virtual Background? Background { get; set; }

        [ForeignKey("OutfitId")]
        public virtual Outfit? Outfit { get; set; }

        public virtual ICollection<AlbumCatches> AlbumCatches { get; set; } = new List<AlbumCatches>();
    }
}