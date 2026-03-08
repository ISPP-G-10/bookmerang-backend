using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Models.DTOs;
using NetTopologySuite.Geometries;

namespace Bookmerang.Api.Services.Interfaces.ExchangeInterfaces;

public interface IExchangeMeetingService
{
    Task<ExchangeMeeting?> GetExchangeMeeting(int meetingId);
    Task<ExchangeMeeting?> GetExchangeMeetingWithRelations(int meetingId);
    Task<List<ExchangeMeeting>> GetMeetingsByUserId(Guid proposerId);
    Task<List<ExchangeMeeting>> GetAllExchangeMeetings();
    Task<ExchangeMeeting?> GetMeetingByExchangeId(int exchangeId);
    Task<ExchangeMeeting> CreateExchangeMeeting(int exchangeId, ExchangeMode exchangeMode,
        Guid proposerId, int? bookspotId, DateTime? scheduledAt, Point customLocation);
    Task<ExchangeMeeting> UpdateExchangeMeeting(int meetingId, UpdateExchangeMeetingDto dto);
    Task<bool> DeleteExchangeMeeting(int meetingId);
}