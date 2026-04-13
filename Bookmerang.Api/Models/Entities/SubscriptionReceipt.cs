using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Models.Entities;

[Table("subscription_receipts")]
public class SubscriptionReceipt
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [Column("subscription_id")]
    public int SubscriptionId { get; set; }

    [Required]
    [Column("platform")]
    public SubscriptionPlatform Platform { get; set; }

    [Column("receipt_data")]
    public string? ReceiptData { get; set; }

    [Column("verified_at")]
    public DateTime? VerifiedAt { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    [ForeignKey("SubscriptionId")]
    public Subscription? Subscription { get; set; }
}
