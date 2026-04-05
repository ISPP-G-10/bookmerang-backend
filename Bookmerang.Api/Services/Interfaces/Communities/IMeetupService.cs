using Bookmerang.Api.Models.DTOs.Communities;

namespace Bookmerang.Api.Services.Interfaces.Communities;

public interface IMeetupService
{
    Task<MeetupDto> CreateMeetupAsync(Guid creatorId, int communityId, CreateMeetupRequest request);
    Task<MeetupDto> UpdateMeetupAsync(Guid userId, int communityId, int meetupId, CreateMeetupRequest request);
    Task DeleteMeetupAsync(Guid userId, int communityId, int meetupId);
    Task<List<MeetupDto>> GetMeetupsByCommunityAsync(int communityId);
    Task<MeetupAttendanceDto> AttendMeetupAsync(Guid userId, int meetupId, AttendMeetupRequest request);
    Task CancelAttendanceAsync(Guid userId, int meetupId);
}