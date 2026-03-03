using Bookmerang.Api.Models;
using Bookmerang.Api.Models.DTOs;
using NetTopologySuite.Geometries;

namespace Bookmerang.Api.Services.Interfaces.ExchangeInterfaces;

public interface IExchangeMeetingService
{
    Task<ExchangeMeeting?> GetExchangeMeeting(int meetingId);
    Task<List<ExchangeMeeting>> GetMeetingsByUserId(Guid proposerId);
    Task<ExchangeMeeting> CreateExchangeMeeting(int exchangeId, ExchangeMode exchangeMode,
        Guid proposerId, int? bookspotId, DateTime? scheduledAt, Point customLocation);
    Task<ExchangeMeeting> UpdateExchangeMeeting(int meetingId, UpdateExchangeMeetingDto dto);
    Task<bool> DeleteExchangeMeeting(int meetingId);
}