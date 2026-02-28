using Bookmerang.Api.Models.Books.Enums;

namespace Bookmerang.Api.Models.DTOs.Books.Requests;

/// <summary>
/// Request para el Paso 2 del flujo: datos bibliográficos.
/// Campos opcionales para permitir guardado incremental
/// (el usuario puede guardar a mitad del paso 2 como borrador).
/// </summary>
public class UpdateBookDataRequest
{
    public string? Isbn { get; set; }
    public string? Titulo { get; set; }
    public string? Autor { get; set; }
    public string? Editorial { get; set; }
    public int? NumPaginas { get; set; }
    public CoverType? Cover { get; set; }

    public List<int> GenreIds { get; set; } = [];
    public List<int> LanguageIds { get; set; } = [];
}