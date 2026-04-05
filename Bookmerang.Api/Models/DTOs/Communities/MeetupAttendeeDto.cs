namespace Bookmerang.Api.Models.DTOs.Communities;

public class MeetupAttendeeDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int SelectedBookId { get; set; }
    public string SelectedBookTitle { get; set; } = string.Empty;
}