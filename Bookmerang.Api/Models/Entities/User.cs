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

    // Navigation properties
    [ForeignKey("Id")]
    public BaseUser BaseUser { get; set; } = null!;
    
    public UserPreference? UserPreference { get; set; }
}
