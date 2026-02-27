using NetTopologySuite.Geometries;

namespace Bookmerang.Api.Models.DTOs;

public record ExchangeMeetingDto(
    Guid ExchangeMeetingId,
    string SupabaseId,
    Guid ExchangeId,
    ExchangeMode ExchangeMode,
    Guid BookspotId,
    Point CustomLocation,
    DateTime ScheduledAt,
    Guid ProposerId,
    ExchangeMeetingStatus MeetingStatus,
    bool MarkAsCompletedByUser1,
    bool MarkAsCompletedByUser2
);

public static class ExchangeMeetingExtensions
{
    public static ExchangeMeetingDto ToDto(this ExchangeMeeting meeting) => new(
        meeting.ExchangeMeeetingId,
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