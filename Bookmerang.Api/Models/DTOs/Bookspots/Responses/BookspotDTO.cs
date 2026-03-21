using Bookmerang.Api.Models.Entities;

namespace Bookmerang.Api.Models.DTOs.Bookspots.Responses;

public class BookspotDTO
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string AddressText { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool IsBookdrop { get; set; }
    public BookspotStatus Status { get; set; }
    public int ValidationCount { get; set; }
    public int RequiredValidations { get; set; } = 5;
    public DateTime CreatedAt { get; set; }
}