using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bookmerang.Api.Models;

[Table("chat_participants")]
public class ChatParticipant
{
    [Column("chat_id")]
    public int ChatId { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Required]
    [Column("joined_at")]
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("ChatId")]
    public Chat Chat { get; set; } = null!;

    [ForeignKey("UserId")]
    public BaseUser User { get; set; } = null!;
}
