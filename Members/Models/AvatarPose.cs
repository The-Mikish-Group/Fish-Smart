using System.ComponentModel.DataAnnotations;

namespace Members.Models
{
    public class AvatarPose
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
        public string? Category { get; set; } // "TwoHands", "HangingLine", "CloseUp", "Custom"

        public bool IsPremium { get; set; } = false;

        // Navigation properties
        public virtual ICollection<Catch> Catches { get; set; } = new List<Catch>();
    }
}