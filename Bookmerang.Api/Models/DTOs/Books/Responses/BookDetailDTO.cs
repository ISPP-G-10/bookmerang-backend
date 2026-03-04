using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Models.DTOs.Books.Responses;

/// <summary>
/// Vista completa de un libro para la pantalla de detalle.
/// Incluye todos los campos.
/// Solo se usa cuando el usuario abre un libro específico,
/// nunca en listas (por rendimiento).
/// </summary>
public class BookDetailDTO
{
    public int Id { get; set; }
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
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Colecciones completas — solo en detalle, nunca en listas
    public List<BookPhotoDTO> Photos { get; set; } = [];

    // Devolvemos nombres, no IDs — el frontend no debería necesitar
    // hacer otra llamada para resolver "genre_id=3" -> "Fantasía"
    public List<string> Genres { get; set; } = [];
    public List<string> Languages { get; set; } = [];
}