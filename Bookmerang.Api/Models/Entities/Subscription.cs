using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Models.Entities;

[Table("subscriptions")]
public class Subscription
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Required]
    [Column("platform")]
    public SubscriptionPlatform Platform { get; set; }

    [Column("platform_subscription_id")]
    public string? PlatformSubscriptionId { get; set; }

    [Column("original_transaction_id")]
    public string? OriginalTransactionId { get; set; }

    [Required]
    [Column("status")]
    public SubscriptionStatus Status { get; set; }

    [Required]
    [Column("current_period_start")]
    public DateTime CurrentPeriodStart { get; set; }

    [Required]
    [Column("current_period_end")]
    public DateTime CurrentPeriodEnd { get; set; }

    [Column("cancels_at_period_end")]
    public bool CancelsAtPeriodEnd { get; set; } = false;

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    [ForeignKey("UserId")]
    public BaseUser? User { get; set; }

    public ICollection<SubscriptionReceipt> Receipts { get; set; } = new List<SubscriptionReceipt>();
}
