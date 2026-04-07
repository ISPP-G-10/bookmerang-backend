using NetTopologySuite.Geometries;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Models.DTOs;

public record ExchangeMeetingDto(
    int? ExchangeMeetingId,
    int? ExchangeId,
    ExchangeMode? ExchangeMode,
    int? BookspotId,
    double[]? CustomLocation, // coordenadas en forma de lista [x, y]
    DateTime? ScheduledAt,
    Guid? ProposerId,
    String? ProposerName,
    ExchangeMeetingStatus? MeetingStatus,
    bool? MarkAsCompletedByUser1,
    bool? MarkAsCompletedByUser2,
    string? Pin,
    BookdropExchangeStatus? BookDropStatus
);

public record UpdateExchangeMeetingDto(
    ExchangeMode? ExchangeMode,
    int? BookspotId,
    double[]? CustomLocation,
    DateTime? ScheduledAt,
    ExchangeMeetingStatus? MeetingStatus,
    bool? MarkAsCompletedByUser1,
    bool? MarkAsCompletedByUser2
);

public static class ExchangeMeetingExtensions
{
    public static ExchangeMeetingDto ToDto(this ExchangeMeeting meeting) => new(
        meeting.ExchangeMeetingId,
        meeting.ExchangeId,
        meeting.ExchangeMode,
        meeting.BookspotId,
        meeting.CustomLocation != null ? [meeting.CustomLocation.X, meeting.CustomLocation.Y] : null,
        meeting.ScheduledAt,
        meeting.ProposerId,
        meeting.Proposer?.BaseUser?.Name, //No siempre se incluyen todas las relaciones del proposer
        meeting.MeetingStatus,
        meeting.MarkAsCompletedByUser1,
        meeting.MarkAsCompletedByUser2,
        meeting.Pin,
        meeting.BookDropStatus
    );
}