using System.ComponentModel.DataAnnotations;

namespace Members.Models
{
    public class FishSpecies
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string CommonName { get; set; } = string.Empty;

        [StringLength(100)]
        public string? ScientificName { get; set; }

        [Required]
        [StringLength(20)]
        public string WaterType { get; set; } = string.Empty; // "Fresh", "Salt", "Both"

        [StringLength(100)]
        public string? Region { get; set; }

        [Range(0, 999.99)]
        public decimal? MinSize { get; set; }

        [Range(0, 999.99)]
        public decimal? MaxSize { get; set; }

        [Range(1, 12)]
        public int? SeasonStart { get; set; } // Month (1-12)

        [Range(1, 12)]
        public int? SeasonEnd { get; set; } // Month (1-12)

        [StringLength(500)]
        public string? StockImageUrl { get; set; }

        [StringLength(1000)]
        public string? RegulationNotes { get; set; }

        public bool IsActive { get; set; } = true;

        // Navigation properties
        public virtual ICollection<Catch> Catches { get; set; } = new List<Catch>();
    }
}