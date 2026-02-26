using Bookmerang.Api.Models.Books.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bookmerang.Api.Models;

public class Book
{
    public int Id { get; set; }

    // FK hacia user.id
    public Guid OwnerId { get; set; }

    public string? Isbn { get; set; }
    public string? Titulo { get; set; }
    public string? Autor { get; set; }
    public string? Editorial { get; set; }
    public int? NumPaginas { get; set; }

    public CoverType? Cover { get; set; }
    public BookCondition? Condition { get; set; }
    public string? Observaciones { get; set; }

    public BookStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation property apunta a User (tabla "users")
    public User Owner { get; set; } = null!;

    public ICollection<BookPhoto> Photos { get; set; } = [];
    public ICollection<BookGenre> BookGenres { get; set; } = [];
    public ICollection<BookLanguage> BookLanguages { get; set; } = [];
}