namespace Bookmerang.Api.Models.Entities;

public class Genre
{
    public int Id { get; set; }
    public required string Name { get; set; }

    public ICollection<BookGenre> BookGenres { get; set; } = [];
}
