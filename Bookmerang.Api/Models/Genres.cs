using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bookmerang.Api.Models;

[Table("genres")]
public class Genre
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("name")]
    public string Name { get; set; } = null!;

    public ICollection<UserPreferencesGenre> UserPreferences { get; set; } = new List<UserPreferencesGenre>();
}