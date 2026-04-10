using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bookmerang.Api.Models.Entities;

[Table("user_progress")]
public class UserProgress
{
    [Key]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("xp_total")]
    public int XpTotal { get; set; }

    [Column("streak_weeks")]
    public int StreakWeeks { get; set; }

    [Column("last_active_date")]
    public DateTime? LastActiveDate { get; set; }

    [Column("streak_start_date")]
    public DateTime? StreakStartDate { get; set; }

    [Column("last_decrement_date")]
    public DateTime? LastDecrementDate { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
