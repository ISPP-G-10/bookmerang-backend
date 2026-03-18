namespace Bookmerang.Api.Models.DTOs.Communities;

public class CreateMeetupRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? OtherBookSpotId { get; set; }
    public double[]? OtherLocation { get; set; } // [x, y]
    public DateTime ScheduledAt { get; set; }
}