using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Members.Models
{
    public class FishingEquipment
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Type { get; set; } = string.Empty; // "SpinCasting", "BaitCasting", "FlyRod", "Spearfishing"

        [StringLength(100)]
        public string? Brand { get; set; }

        [StringLength(100)]
        public string? Model { get; set; }

        public int? SponsorId { get; set; }

        public bool IsAIGenerated { get; set; } = false;

        public bool IsPremium { get; set; } = false;

        // Navigation properties
        [ForeignKey("SponsorId")]
        public virtual Sponsor? Sponsor { get; set; }

        public virtual ICollection<FishingSession> FishingSessions { get; set; } = new List<FishingSession>();
    }
}