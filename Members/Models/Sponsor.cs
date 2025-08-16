using System.ComponentModel.DataAnnotations;

namespace Members.Models
{
    public class Sponsor
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Category { get; set; } // "Clothing", "Equipment", "BaitsLures"

        [StringLength(500)]
        public string? LogoUrl { get; set; }

        [StringLength(500)]
        public string? WebsiteUrl { get; set; }

        public bool IsActive { get; set; } = true;

        // Navigation properties
        public virtual ICollection<Outfit> Outfits { get; set; } = new List<Outfit>();
        public virtual ICollection<FishingEquipment> FishingEquipment { get; set; } = new List<FishingEquipment>();
        public virtual ICollection<BaitsLures> BaitsLures { get; set; } = new List<BaitsLures>();
    }
}