using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Models.Entities;

[Table("communities")]
public class Community
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Column("reference_bookspot_id")]
    public int ReferenceBookspotId { get; set; }

    [Required]
    [Column("status")]
    public CommunityStatus Status { get; set; }

    [Column("creator_id")]
    public Guid? CreatorId { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("ReferenceBookspotId")]
    public Bookspot ReferenceBookspot { get; set; } = null!;

    [ForeignKey("CreatorId")]
    public User? Creator { get; set; }

    public ICollection<CommunityMember> Members { get; set; } = new List<CommunityMember>();
    public CommunityChat? CommunityChat { get; set; }
}