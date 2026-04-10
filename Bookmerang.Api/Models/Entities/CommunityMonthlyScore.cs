using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bookmerang.Api.Models.Entities;

[Table("community_monthly_scores")]
public class CommunityMonthlyScore
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [Column("community_id")]
    public int CommunityId { get; set; }

    [Required]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Required]
    [Column("month")]
    public string Month { get; set; } = string.Empty;

    [Required]
    [Column("inkdrops_this_month")]
    public int InkdropsThisMonth { get; set; } = 0;

    [ForeignKey("CommunityId")]
    public Community Community { get; set; } = null!;

    [ForeignKey("UserId")]
    public User User { get; set; } = null!;
}
