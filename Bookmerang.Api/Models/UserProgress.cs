using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bookmerang.Api.Models;

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

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
