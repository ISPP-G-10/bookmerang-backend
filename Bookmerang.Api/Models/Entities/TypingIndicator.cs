using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bookmerang.Api.Models.Entities;

[Table("typing_indicators")]
public class TypingIndicator
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [Column("chat_id")]
    public int ChatId { get; set; }

    [Required]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Required]
    [Column("started_at")]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("ChatId")]
    public Chat Chat { get; set; } = null!;

    [ForeignKey("UserId")]
    public User User { get; set; } = null!;
}
