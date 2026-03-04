using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Models.DTOs.Books.Responses;

/// <summary>
/// Vista resumida de un libro para listas y tarjetas.
/// Contiene SOLO los campos necesarios para renderizar una tarjeta.
/// DTO ligero, para no sobrecargar con tantos datos.
/// </summary>
public class BookListItemDTO
{
    public int Id { get; set; }
    public string? Titulo { get; set; }
    public string? Autor { get; set; }
    public BookStatus Status { get; set; }
    public CoverType? Cover { get; set; }
    public BookCondition? Condition { get; set; }

    /// URL de la primera foto (orden=0) para la miniatura de la tarjeta.
    /// Null si el libro no tiene fotos aún.
    public string? ThumbnailUrl { get; set; }

    public DateTime? UpdatedAt { get; set; }
}