using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Members.Models
{
    public class Outfit
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Description { get; set; }

        [StringLength(500)]
        public string? ImageUrl { get; set; }

        public int? SponsorId { get; set; }

        [StringLength(100)]
        public string? BrandName { get; set; }

        public bool IsAIGenerated { get; set; } = false;

        public bool IsPremium { get; set; } = false;

        // Navigation properties
        [ForeignKey("SponsorId")]
        public virtual Sponsor? Sponsor { get; set; }

        public virtual ICollection<Catch> Catches { get; set; } = new List<Catch>();
    }
}