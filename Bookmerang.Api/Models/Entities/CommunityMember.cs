using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Models.Entities;

[Table("community_members")]
public class CommunityMember
{
    [Column("community_id")]
    public int CommunityId { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Required]
    [Column("role")]
    public CommunityRole Role { get; set; }

    [Required]
    [Column("joined_at")]
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("CommunityId")]
    public Community Community { get; set; } = null!;

    [ForeignKey("UserId")]
    public User User { get; set; } = null!;
}