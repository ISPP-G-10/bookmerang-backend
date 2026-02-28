using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bookmerang.Api.Models;

[Table("messages")]
public class Message
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [Column("chat_id")]
    public int ChatId { get; set; }

    [Required]
    [Column("sender_id")]
    public Guid SenderId { get; set; }

    [Required]
    [Column("body")]
    public string Body { get; set; } = string.Empty;

    [Required]
    [Column("sent_at")]
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("ChatId")]
    public Chat Chat { get; set; } = null!;

    [ForeignKey("SenderId")]
    public BaseUser Sender { get; set; } = null!;
}
