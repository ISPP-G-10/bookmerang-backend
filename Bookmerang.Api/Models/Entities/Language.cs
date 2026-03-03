namespace Bookmerang.Api.Models.Entities;

public class Language
{
    public int Id { get; set; }
    public required string LanguageName { get; set; } // Se cambia el nombre del modelo porque C no permite que una propiedad y clase tengan el mismo nombre

    public ICollection<BookLanguage> BookLanguages { get; set; } = [];
}
