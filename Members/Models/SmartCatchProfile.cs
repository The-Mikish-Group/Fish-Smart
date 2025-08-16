using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Members.Models
{
    public class SmartCatchProfile
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [StringLength(100)]
        public string? DisplayName { get; set; }

        [StringLength(20)]
        public string SubscriptionType { get; set; } = "Free";

        [StringLength(20)]
        public string PreferredWaterType { get; set; } = "Both";

        [StringLength(100)]
        public string? DefaultRegion { get; set; }

        public bool VoiceActivationEnabled { get; set; } = false;

        public bool AutoLocationEnabled { get; set; } = false;

        public bool WatermarkEnabled { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties - removed virtual and configured relationships manually
        [ForeignKey("UserId")]
        public IdentityUser? User { get; set; }

        // These will be configured in OnModelCreating instead of using navigation properties
        [NotMapped]
        public ICollection<UserAvatar> UserAvatars { get; set; } = new List<UserAvatar>();

        [NotMapped]
        public ICollection<FishingSession> FishingSessions { get; set; } = new List<FishingSession>();

        [NotMapped]
        public ICollection<CatchAlbum> CatchAlbums { get; set; } = new List<CatchAlbum>();
    }
}