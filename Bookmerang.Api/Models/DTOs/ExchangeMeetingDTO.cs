using NetTopologySuite.Geometries;

namespace Bookmerang.Api.Models.DTOs;

public record ExchangeMeetingDto(
    int ExchangeMeetingId,
    string SupabaseId,
    int ExchangeId,
    ExchangeMode ExchangeMode,
    int? BookspotId,
    Point CustomLocation,
    DateTime? ScheduledAt,
    Guid ProposerId,
    ExchangeMeetingStatus MeetingStatus,
    bool MarkAsCompletedByUser1,
    bool MarkAsCompletedByUser2
);

public static class ExchangeMeetingExtensions
{
    public static ExchangeMeetingDto ToDto(this ExchangeMeeting meeting) => new(
        meeting.ExchangeMeetingId,
        meeting.SupabaseId,
        meeting.ExchangeId,
        meeting.ExchangeMode,
        meeting.BookspotId,
        meeting.CustomLocation,
        meeting.ScheduledAt,
        meeting.ProposerId,
        meeting.MeetingStatus,
        meeting.MarkAsCompletedByUser1,
        meeting.MarkAsCompletedByUser2
    );
}