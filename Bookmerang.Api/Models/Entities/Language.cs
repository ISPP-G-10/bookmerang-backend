namespace Bookmerang.Api.Models.Entities;

/// Catálogo de idiomas disponibles.
public class Language
{
    public int Id { get; set; }
    public string LanguageName { get; set; } = string.Empty;

    // Navigation property inversa para la relación M:N con books
    public ICollection<BookLanguage> BookLanguages { get; set; } = [];
}
