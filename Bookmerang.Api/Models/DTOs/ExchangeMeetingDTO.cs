using NetTopologySuite.Geometries;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Models.DTOs;

public record ExchangeMeetingDto(
    int? ExchangeId,
    ExchangeMode? ExchangeMode,
    int? BookspotId,
    Point? CustomLocation,
    DateTime? ScheduledAt,
    Guid? ProposerId,
    ExchangeMeetingStatus? MeetingStatus,
    bool? MarkAsCompletedByUser1,
    bool? MarkAsCompletedByUser2
);

public record UpdateExchangeMeetingDto(
    ExchangeMode? ExchangeMode,
    int? BookspotId,
    Point? CustomLocation,
    DateTime? ScheduledAt,
    ExchangeMeetingStatus? MeetingStatus,
    bool? MarkAsCompletedByUser1,
    bool? MarkAsCompletedByUser2
);

public static class ExchangeMeetingExtensions
{
    public static ExchangeMeetingDto ToDto(this ExchangeMeeting meeting) => new(
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