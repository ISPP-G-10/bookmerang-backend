namespace Bookmerang.Api.Models.Entities;

/// Tabla de unión M:N entre books y languages.
public class BookLanguage
{
    public int BookId { get; set; }
    public int LanguageId { get; set; }

    // Navigation properties para los Include() en queries
    public Book Book { get; set; } = null!;
    public Language Language { get; set; } = null!;
}