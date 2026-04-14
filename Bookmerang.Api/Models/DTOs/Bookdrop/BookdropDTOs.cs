using System.ComponentModel.DataAnnotations;
using Bookmerang.Api.Models.Enums;
using System.Text.Json.Serialization;

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
    private double? _latitud;
    private double? _longitud;

    [StringLength(100, MinimumLength = 3)]
    public string? NombreEstablecimiento { get; set; }

    [StringLength(200, MinimumLength = 5)]
    public string? AddressText { get; set; }

    public string? ProfilePhoto { get; set; }

    [Range(-90, 90)]
    [JsonPropertyName("latitud")]
    public double? Latitud
    {
        get => _latitud;
        set => _latitud = value;
    }

    [Range(-180, 180)]
    [JsonPropertyName("longitud")]
    public double? Longitud
    {
        get => _longitud;
        set => _longitud = value;
    }

    // Backward-compatible aliases used by some clients.
    [JsonPropertyName("latitude")]
    public double? Latitude
    {
        set => _latitud = value;
    }

    [JsonPropertyName("longitude")]
    public double? Longitude
    {
        set => _longitud = value;
    }
}
