using System.ComponentModel.DataAnnotations;

namespace Bookmerang.Api.Models.DTOs.Bookspots.Requests;

public class CreateBookspotRequest
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [StringLength(200, MinimumLength = 5)]
    public string AddressText { get; set; } = string.Empty;

    [Required]
    [Range(-90, 90, ErrorMessage = "La latitud debe estar entre -90 y 90.")]
    public double Latitude { get; set; }

    [Required]
    [Range(-180, 180, ErrorMessage = "La longitud debe estar entre -180 y 180.")]
    public double Longitude { get; set; }

    public bool IsBookdrop { get; set; } = false;
}