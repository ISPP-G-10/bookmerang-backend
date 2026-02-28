using Bookmerang.Api.Models;
using NetTopologySuite.Geometries;

namespace Bookmerang.Api.Services.Interfaces.ExchangeInterfaces;

public interface IExchangeMeetingService
{
    Task<ExchangeMeeting?> GetExchangeMeeting(string supabaseId);
    Task<ExchangeMeeting> CreateExchangeMeeting(int exchangeId, ExchangeMode exchangeMode,
        Guid proposerId, int? bookspotId, DateTime? scheduledAt, Point customLocation);
    Task<ExchangeMeeting> UpdateExchangeMeeting(int exchangeId, 
        ExchangeMode exchangeMode, int? bookspotId, Point customLocation, DateTime? scheduledAt,
    Guid proposerId, ExchangeMeetingStatus meetingStatus, bool markAsCompletedByUser1,
        bool markAsCompletedByUser2);
    Task<bool> DeleteExchangeMeeting(string supabaseId);
}