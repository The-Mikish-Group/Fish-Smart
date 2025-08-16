using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Members.Models
{
    public class FishingSession
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public DateTime SessionDate { get; set; }

        [Required]
        [StringLength(20)]
        public string WaterType { get; set; } = string.Empty; // "Fresh", "Salt"

        [StringLength(200)]
        public string? LocationName { get; set; }

        [Range(-90, 90)]
        public decimal? Latitude { get; set; }

        [Range(-180, 180)]
        public decimal? Longitude { get; set; }

        // AI-Populated Environmental Data (Premium)
        [StringLength(200)]
        public string? WeatherConditions { get; set; }

        public decimal? Temperature { get; set; }

        [StringLength(100)]
        public string? TideConditions { get; set; }

        [StringLength(50)]
        public string? WindDirection { get; set; }

        public decimal? WindSpeed { get; set; }

        [StringLength(50)]
        public string? MoonPhase { get; set; }

        public decimal? BarometricPressure { get; set; }

        // Equipment Used
        public int? RodReelSetupId { get; set; }
        public int? PrimaryBaitLureId { get; set; }

        // Session Notes
        [StringLength(1000)]
        public string? Notes { get; set; }

        [StringLength(500)]
        public string? VoiceNotesUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual IdentityUser? User { get; set; }

        [ForeignKey("RodReelSetupId")]
        public virtual FishingEquipment? RodReelSetup { get; set; }

        [ForeignKey("PrimaryBaitLureId")]
        public virtual BaitsLures? PrimaryBaitLure { get; set; }

        public virtual ICollection<Catch> Catches { get; set; } = new List<Catch>();
    }
}