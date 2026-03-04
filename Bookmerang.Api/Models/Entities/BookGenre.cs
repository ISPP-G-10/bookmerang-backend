namespace Bookmerang.Api.Models.Entities;

///Tabla de unión M:N entre books y genres.
public class BookGenre
{
    public int BookId { get; set; }
    public int GenreId { get; set; }
    
    // Navigation properties para los Include() en queries
    public Book Book { get; set; } = null!;
    public Genre Genre { get; set; } = null!;
}
