namespace Bookmerang.Api.Models.Entities;

public class BookPhoto
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public required string Url { get; set; }
    public int Orden { get; set; }

    public Book Book { get; set; } = null!;
}
