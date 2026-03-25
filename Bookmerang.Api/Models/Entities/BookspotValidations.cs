using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bookmerang.Api.Models.Entities;

public class BookspotValidation
{
    [Required]
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("bookspot_id")]
    public int BookspotId { get; set; }

    [Required]
    [Column("validator_user_id")]
    public Guid ValidatorUserId { get; set; }

    [Required]
    [Column("knows_place")]
    public bool KnowsPlace { get; set; }

    [Required]
    [Column("safe_for_exchange")]
    public bool SafeForExchange { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}