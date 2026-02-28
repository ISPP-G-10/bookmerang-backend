namespace Bookmerang.Api.DTOs.Books.Requests;

/// <summary>
/// Representa una foto individual
/// El frontend ya ha subido la imagen al storage y nos manda solo la URL.
/// </summary>
public class PhotoUpsertDto
{
    /// URL pública de la imagen
    /// No puede ser vacía — si el frontend manda una foto, debe tener URL.
    public string Url { get; set; } = string.Empty;

    /// Posición en el carrusel (0, 1, 2, 3, 4).
    public int Order { get; set; }
}