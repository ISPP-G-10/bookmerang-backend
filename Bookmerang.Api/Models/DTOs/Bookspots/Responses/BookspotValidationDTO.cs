namespace Bookmerang.Api.Models.DTOs.Bookspots.Responses;

public class BookspotValidationDTO
{
    public int Id { get; set; }
    public bool KnowsPlace { get; set; }
    public bool SafeForExchange { get; set; }
    public DateTime CreatedAt { get; set; }
}