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

public record UpdateBookdropProfileRequest(
    string? NombreEstablecimiento,
    string? AddressText,
    string? ProfilePhoto,
    double? Latitud,
    double? Longitud
);
