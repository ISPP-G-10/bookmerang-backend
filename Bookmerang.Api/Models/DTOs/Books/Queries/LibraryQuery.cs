using Bookmerang.Api.Models.Entities;

namespace Bookmerang.Api.Models.DTOs.Books.Queries;

/// <summary>
/// Parámetros de consulta para "Mi Biblioteca".
/// </summary>
public class LibraryQuery
{
    /// Página actual (1-based).
    public int Page { get; set; } = 1;

    /// Elementos por página.
    public int PageSize { get; set; } = 20;

    /// Filtro opcional por estado.
    public BookStatus? Status { get; set; }

    /// Búsqueda por texto en título o autor (case-insensitive).
    /// Si es null o vacío, no filtra por texto.
    public string? Search { get; set; }
}