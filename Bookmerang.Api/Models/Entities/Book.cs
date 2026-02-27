using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Models.Entities;

public class Book
{
    public int Id { get; set; }
    public int OwnerId { get; set; }
    public string? Isbn { get; set; }
    public string? Titulo { get; set; }
    public string? Autor { get; set; }
    public string? Editorial { get; set; }
    public int? NumPaginas { get; set; }
    public CoverType? Cover { get; set; }
    public BookCondition? Condition { get; set; }
    public string? Observaciones { get; set; }
    public required BookStatus Status { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<BookPhoto> Photos { get; set; } = [];
    public ICollection<BookGenre> BookGenres { get; set; } = [];
    public ICollection<BookLanguage> BookLanguages { get; set; } = [];
}
