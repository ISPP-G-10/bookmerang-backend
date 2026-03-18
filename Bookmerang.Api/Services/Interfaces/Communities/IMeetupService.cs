using Bookmerang.Api.Models.DTOs.Communities;

namespace Bookmerang.Api.Services.Interfaces.Communities;

public interface IMeetupService
{
    Task<MeetupDto> CreateMeetupAsync(Guid creatorId, int communityId, CreateMeetupRequest request);
    Task<List<MeetupDto>> GetMeetupsByCommunityAsync(int communityId);
    Task<MeetupAttendanceDto> AttendMeetupAsync(Guid userId, int meetupId, AttendMeetupRequest request);
    Task CancelAttendanceAsync(Guid userId, int meetupId);
}