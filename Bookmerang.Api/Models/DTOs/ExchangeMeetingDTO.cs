using System.ComponentModel.DataAnnotations;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Models.DTOs;

public record CreateExchangeMeetingDto(
    int ExchangeId,
    ExchangeMode ExchangeMode,
    int? BookspotId,
    [Range(-90, 90)] double? Latitud,
    [Range(-180, 180)] double? Longitud,
    DateTime ScheduledAt
);

public record ExchangeMeetingDto(
    int ExchangeMeetingId,
    int ExchangeId,
    ExchangeMode ExchangeMode,
    double Latitud,
    double Longitud,
    DateTime ScheduledAt,
    Guid ProposerId,
    string ProposerName,
    ExchangeMeetingStatus MeetingStatus,
    bool MarkAsCompletedByUser1,
    bool MarkAsCompletedByUser2,
    int? BookspotId,
    string? Pin,
    BookdropExchangeStatus? BookDropStatus
);

public record CounterProposeMeetingDto(
    ExchangeMode ExchangeMode,
    int? BookspotId,
    [Range(-90, 90)] double? Latitud,
    [Range(-180, 180)] double? Longitud,
    DateTime ScheduledAt
);

public static class ExchangeMeetingExtensions
{
    public static ExchangeMeetingDto ToDto(this ExchangeMeeting meeting) => new(
        meeting.ExchangeMeetingId,
        meeting.ExchangeId,
        meeting.ExchangeMode,
        meeting.CustomLocation.Y,
        meeting.CustomLocation.X,
        meeting.ScheduledAt!.Value,
        meeting.ProposerId,
        meeting.Proposer.BaseUser.Name,
        meeting.MeetingStatus,
        meeting.MarkAsCompletedByUser1,
        meeting.MarkAsCompletedByUser2,
        meeting.BookspotId,
        meeting.Pin,
        meeting.BookDropStatus
    );
}