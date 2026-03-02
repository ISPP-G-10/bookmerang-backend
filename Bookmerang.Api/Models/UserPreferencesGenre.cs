using System.ComponentModel.DataAnnotations.Schema;

namespace Bookmerang.Api.Models;

[Table("user_preferences_genres")]
public class UserPreferencesGenre
{
    [Column("preferences_id")]
    public int PreferencesId { get; set; }

    [Column("genre_id")]
    public int GenreId { get; set; }

    public UserPreference? Preferences { get; set; }
    public Genre? Genre { get; set; }
}
