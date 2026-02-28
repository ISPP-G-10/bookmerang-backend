using Bookmerang.Api.Models;
using Bookmerang.Api.Models.DTOs;
using NetTopologySuite.Geometries;

namespace Bookmerang.Api.Services.Interfaces.ExchangeInterfaces;

public interface IExchangeMeetingService
{
    Task<ExchangeMeeting?> GetExchangeMeeting(string supabaseId);
    Task<ExchangeMeeting> CreateExchangeMeeting(int exchangeId, ExchangeMode exchangeMode,
        Guid proposerId, int? bookspotId, DateTime? scheduledAt, Point customLocation);
    Task<ExchangeMeeting> UpdateExchangeMeeting(string supabaseId, ExchangeMeetingDto dto);
    Task<bool> DeleteExchangeMeeting(string supabaseId);
}