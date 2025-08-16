using System.ComponentModel.DataAnnotations;

namespace Members.Models
{
    public class Background
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Description { get; set; }

        [StringLength(500)]
        public string? ImageUrl { get; set; }

        [StringLength(50)]
        public string? Category { get; set; } // "Seawall", "Beach", "Pier", "Boat"

        [StringLength(20)]
        public string? WaterType { get; set; } // "Fresh", "Salt", "Both"

        public bool IsPremium { get; set; } = false;

        // Navigation properties
        public virtual ICollection<Catch> Catches { get; set; } = new List<Catch>();
    }
}