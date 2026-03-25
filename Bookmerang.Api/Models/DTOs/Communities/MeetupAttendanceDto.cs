using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Models.DTOs.Communities;

public class MeetupAttendanceDto
{
    public int MeetupId { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string ProfilePhoto { get; set; } = string.Empty;
    public int SelectedBookId { get; set; }
    public string SelectedBookTitle { get; set; } = string.Empty;
    public MeetupAttendanceStatus Status { get; set; }
}