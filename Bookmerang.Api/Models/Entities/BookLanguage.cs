namespace Bookmerang.Api.Models.Entities;

public class BookLanguage
{
    public int BookId { get; set; }
    public int LanguageId { get; set; }

    public Book Book { get; set; } = null!;
    public Language Language { get; set; } = null!;
}
