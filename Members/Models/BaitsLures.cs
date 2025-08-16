using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Members.Models
{
    public class BaitsLures
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Type { get; set; } // "Bait", "Lure"

        [StringLength(100)]
        public string? Brand { get; set; }

        [StringLength(50)]
        public string? Color { get; set; }

        [StringLength(50)]
        public string? Size { get; set; }

        public int? SponsorId { get; set; }

        public bool IsAIGenerated { get; set; } = false;

        public bool IsPremium { get; set; } = false;

        // Navigation properties
        [ForeignKey("SponsorId")]
        public virtual Sponsor? Sponsor { get; set; }

        public virtual ICollection<FishingSession> FishingSessions { get; set; } = new List<FishingSession>();
    }
}