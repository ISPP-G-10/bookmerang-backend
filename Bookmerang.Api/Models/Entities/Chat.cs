using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Models.Entities;

[Table("chats")]
public class Chat
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("type")]
    public ChatType Type { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<ChatParticipant> Participants { get; set; } = new List<ChatParticipant>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
