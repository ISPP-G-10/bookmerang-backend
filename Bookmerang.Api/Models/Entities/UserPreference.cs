using Bookmerang.Api.Models.Enums;
using NetTopologySuite.Geometries;

namespace Bookmerang.Api.Models.Entities;

public class UserPreference
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required Point Location { get; set; }
    public int RadioKm { get; set; }
    public required BooksExtension Extension { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<UserPreferenceGenre> UserPreferenceGenres { get; set; } = [];
}
