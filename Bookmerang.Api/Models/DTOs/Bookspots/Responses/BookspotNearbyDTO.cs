namespace Bookmerang.Api.Models.DTOs.Bookspots.Responses;

public class BookspotNearbyDTO
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string AddressText { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool IsBookdrop { get; set; }
    public double DistanceKm { get; set; }
    public string? CreatorUsername { get; set; }
}