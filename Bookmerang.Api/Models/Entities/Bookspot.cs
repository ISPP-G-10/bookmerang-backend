using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NetTopologySuite.Geometries;

namespace Bookmerang.Api.Models.Entities;

public class Bookspot
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [Column("address_text")]
    public string AddressText { get; set; } = string.Empty;

    [Required]
    [Column("location", TypeName = "geography (point, 4326)")]
    public Point Location { get; set; } = null!;

    // Si no se especifica, se asume que no es un bookdrop
    [Column("is_bookdrop")]
    public bool IsBookdrop { get; set; } = false;

    [Required]
    [Column("created_by_user_id")]
    public Guid CreatedByUserId { get; set; }

    [Column("owner_id")]
    public Guid? OwnerId { get; set; }

    [Column("status")]
    public BookspotStatus Status { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}