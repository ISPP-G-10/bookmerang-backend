using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Models.DTOs.Communities;

public class MeetupDto
{
    public int Id { get; set; }
    public int CommunityId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? OtherBookSpotId { get; set; }
    public double[]? OtherLocation { get; set; } // [x, y]
    public DateTime ScheduledAt { get; set; }
    public MeetupStatus Status { get; set; }
    public Guid? CreatorId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<MeetupAttendeeDto> Attendees { get; set; } = [];
}