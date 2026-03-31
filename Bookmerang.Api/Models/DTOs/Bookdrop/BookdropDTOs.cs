using System.ComponentModel.DataAnnotations;
using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Models.DTOs.Bookdrop;

public record BookdropProfileDto(
    Guid Id,
    string Email,
    string Username,
    string Name,
    string ProfilePhoto,
    string NombreEstablecimiento,
    string AddressText,
    double Latitud,
    double Longitud,
    BookspotStatus BookspotStatus,
    DateTime CreatedAt
);

public class UpdateBookdropProfileRequest
{
    [StringLength(100, MinimumLength = 3)]
    public string? NombreEstablecimiento { get; set; }

    [StringLength(200, MinimumLength = 5)]
    public string? AddressText { get; set; }

    public string? ProfilePhoto { get; set; }

    [Range(-90, 90)]
    public double? Latitud { get; set; }

    [Range(-180, 180)]
    public double? Longitud { get; set; }
}
