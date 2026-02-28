namespace Bookmerang.Api.Models.DTOs.Books.Requests;

/// <summary>
/// Request para gestionar las fotos de un libro.
/// Estrategia REPLACE COMPLETO:
///   - El frontend manda la lista COMPLETA de fotos que quiere guardar.
///   - El servicio borra TODAS las fotos actuales del libro.
///   - El servicio inserta las nuevas fotos de esta lista.
/// </summary>
public class UpsertBookPhotosRequest
{
    public List<PhotoUpsertDto> Photos { get; set; } = [];
}