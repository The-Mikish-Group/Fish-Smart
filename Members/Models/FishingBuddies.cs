using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Members.Models
{
    public class FishingBuddies
    {
        public int Id { get; set; }

        [Required]
        public string OwnerUserId { get; set; } = string.Empty;

        [Required]
        public string BuddyUserId { get; set; } = string.Empty;

        [StringLength(20)]
        public string Status { get; set; } = "Pending"; // "Pending", "Accepted", "Blocked"

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        [ForeignKey("OwnerUserId")]
        public virtual IdentityUser? OwnerUser { get; set; }

        [ForeignKey("BuddyUserId")]
        public virtual IdentityUser? BuddyUser { get; set; }
    }
}