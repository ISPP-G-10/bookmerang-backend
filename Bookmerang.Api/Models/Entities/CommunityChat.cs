using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bookmerang.Api.Models.Entities;

[Table("community_chats")]
public class CommunityChat
{
    [Column("community_id")]
    public int CommunityId { get; set; }

    [Column("chat_id")]
    public Guid ChatId { get; set; }

    [ForeignKey("CommunityId")]
    public Community Community { get; set; } = null!;

    [ForeignKey("ChatId")]
    public Chat Chat { get; set; } = null!;
}