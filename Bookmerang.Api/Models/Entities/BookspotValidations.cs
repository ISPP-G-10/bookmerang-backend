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
    public string Validator_user_id { get; set; } = string.Empty;

    [Required]
    [Column("knows_place")]
    public bool Knows_place { get; set; }

    [Required]
    [Column("safe_for_exchange")]
    public bool Safe_for_exchange { get; set; }

    [Column("created_at")]
    public DateTime Created_at { get; set; } = DateTime.UtcNow;
}