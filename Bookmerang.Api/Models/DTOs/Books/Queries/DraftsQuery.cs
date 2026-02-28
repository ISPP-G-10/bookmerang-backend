namespace Bookmerang.Api.Models.DTOs.Books.Queries;

/// <summary>
/// Parámetros de consulta para listar borradores.
/// Más simple que LibraryQuery porque los borradores siempre
/// </summary>
public class DraftsQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// Búsqueda por texto en título o autor.
    public string? Search { get; set; }
}