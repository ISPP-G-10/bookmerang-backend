using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Models.Entities;

[Table("inkdrops_history")]
public class InkdropsHistory
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Required]
    [Column("action_type")]
    public InkdropsActionType ActionType { get; set; }

    [Required]
    [Column("points_granted")]
    public int PointsGranted { get; set; }

    [Column("related_id")]
    public int? RelatedId { get; set; } // ID del exchange o meetup

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("UserId")]
    public User User { get; set; } = null!;
}
