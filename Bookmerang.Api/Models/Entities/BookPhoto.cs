namespace Bookmerang.Api.Models.Entities;

public class BookPhoto
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public string Url { get; set; } = string.Empty;
    public int Orden { get; set; }

    public Book Book { get; set; } = null!;
}
