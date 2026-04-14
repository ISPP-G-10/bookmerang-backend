using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bookmerang.Api.Models.Entities;

[Table("users")]
public class User
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("rating_mean")]
    public decimal RatingMean { get; set; } = 0;

    [Required]
    [Column("finished_exchanges")]
    public int FinishedExchanges { get; set; } = 0;

    [Required]
    [Column("plan")]
    public Enums.PricingPlan Plan { get; set; } = Enums.PricingPlan.FREE;

    [Required]
    [Column("inkdrops")]
    public int Inkdrops { get; set; } = 0;

    [Required]
    [Column("inkdrops_last_updated")]
    public string InkdropsLastUpdated { get; set; } = "1970-01";

    [Required]
    [Column("tutorial_completed")]
    public bool TutorialCompleted { get; set; } = false;

    // Navigation properties
    [ForeignKey("Id")]
    public BaseUser BaseUser { get; set; } = null!;
    
    public UserPreference? UserPreference { get; set; }
}
