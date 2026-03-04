using Bookmerang.Api.Models.Entities;

namespace Bookmerang.Api.Models.DTOs.Books.Requests;

/// <summary>
/// Request para crear un borrador nuevo.
/// TODOS los campos son opcionales porque el usuario puede crear
/// un borrador vacío y rellenarlo después en los 3 pasos del flujo.
/// El status siempre será DRAFT al crear — no lo decide el frontend.
/// </summary>
public class CreateBookDraftRequest
{
    public string? Isbn { get; set; }
    public string? Titulo { get; set; }
    public string? Autor { get; set; }
    public string? Editorial { get; set; }
    public int? NumPaginas { get; set; }
    public CoverType? Cover { get; set; }
    public BookCondition? Condition { get; set; }
    public string? Observaciones { get; set; }

    // IDs de géneros e idiomas seleccionados — pueden venir vacíos
    public List<int> GenreIds { get; set; } = [];
    public List<int> LanguageIds { get; set; } = [];
}