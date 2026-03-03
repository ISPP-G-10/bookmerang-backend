namespace Bookmerang.Api.Models.Entities;

public class UserPreferenceGenre
{
    public int PreferencesId { get; set; }
    public int GenreId { get; set; }

    public UserPreference UserPreference { get; set; } = null!;
    public Genre Genre { get; set; } = null!;
}
