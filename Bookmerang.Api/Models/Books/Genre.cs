namespace Bookmerang.Api.Models.Books;

public class Genre
{
    public int Id { get; set; }

    // NOT NULL + UNIQUE en schema
    public string Name { get; set; } = string.Empty;

    // Navigation property inversa para la relación M:N con books
    public ICollection<BookGenre> BookGenres { get; set; } = [];
}