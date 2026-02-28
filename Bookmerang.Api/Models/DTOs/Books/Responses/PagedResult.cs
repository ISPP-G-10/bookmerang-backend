namespace Bookmerang.Api.Models.DTOs.Books.Responses;

/// <summary>
/// Wrapper genérico para cualquier respuesta paginada.
/// 
/// El frontend necesita estos metadatos para:
/// - Saber cuántas páginas hay en total (Total / PageSize)
/// - Saber si hay más páginas (Page * PageSize < Total)
/// - Mostrar "Página X de Y"
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = [];

    /// Página actual devuelta
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }

    /// Calculado: si hay más páginas después de la actual incluido por comodidad
    public bool HasNextPage => Page * PageSize < Total;
}