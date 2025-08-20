using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Members.Models
{
    public class CatchAlbum
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [StringLength(500)]
        public string? CoverImageUrl { get; set; }

        public bool IsPublic { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Session-tied album properties
        public int? FishingSessionId { get; set; }
        public bool IsSessionAlbum { get; set; } = false;

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual IdentityUser? User { get; set; }

        [ForeignKey("FishingSessionId")]
        public virtual FishingSession? FishingSession { get; set; }

        public virtual ICollection<AlbumCatches> AlbumCatches { get; set; } = new List<AlbumCatches>();
    }
}