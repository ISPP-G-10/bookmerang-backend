using Bookmerang.Api.Models.Books.Enums;

namespace Bookmerang.Api.Models.DTOs.Books.Requests;

/// <summary>
/// Request para el Paso 3 del flujo: detalles del libro.
/// </summary>
public class UpdateBookDetailsRequest
{
    public BookCondition? Condition { get; set; }
    public string? Observaciones { get; set; }
}