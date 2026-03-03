using System;
using NetTopologySuite.Geometries;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bookmerang.Api.Models;

[Table("user_preferences")]
public class UserPreference
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Required]
    [Column("location", TypeName = "geography (Point, 4326)")]
    public Point Location { get; set; } = null!;

    [Required]
    [Column("radio_km")]
    public int RadioKm { get; set; }

    [Required]
    [Column("extension", TypeName = "books_extension")]
    public BooksExtension Extension { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Required]
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<UserPreferencesGenre> Genres { get; set; } = new List<UserPreferencesGenre>();
}