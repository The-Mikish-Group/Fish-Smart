using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace Members.Models
{
    public class BackgroundRemovalUsage
    {
        [Key]
        public int UsageId { get; set; }

        [Required]
        [StringLength(450)]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        public virtual IdentityUser User { get; set; } = null!;

        [Required]
        public DateTime UsageDate { get; set; } = DateTime.UtcNow;

        [Required]
        public int UsageMonth { get; set; } // Store month separately for easy querying

        [Required]
        public int UsageYear { get; set; } // Store year separately for easy querying

        [Required]
        [StringLength(50)]
        public string ServiceUsed { get; set; } = string.Empty; // "Remove.bg", "Clipdrop", etc.

        [Required]
        [Column(TypeName = "decimal(10, 3)")]
        public decimal Cost { get; set; } // Actual API cost (e.g., $0.20)

        [Required]
        [Column(TypeName = "decimal(10, 2)")]
        public decimal ChargeAmount { get; set; } // Amount charged to user (e.g., $0.50)

        [Required]
        public bool IsWithinFreeLimit { get; set; } = false; // True for first 5 per month

        [Required]
        public bool HasBeenInvoiced { get; set; } = false; // True when invoice is created

        public int? InvoiceId { get; set; } // Reference to the created invoice

        [ForeignKey("InvoiceId")]
        public virtual Invoice? Invoice { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; } // Optional notes about the usage

        [Required]
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        public DateTime? DateInvoiced { get; set; } // When the invoice was created
    }
}