using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Models.DTOs;

namespace Bookmerang.Api.Services.Interfaces.ExchangeInterfaces;

public interface IExchangeMeetingService
{
    Task<ExchangeMeeting?> GetExchangeMeeting(int meetingId);
    Task<List<ExchangeMeeting>> GetMeetingsByUserId(Guid proposerId);
    Task<List<ExchangeMeeting>> GetAllExchangeMeetings();
    Task<ExchangeMeeting?> GetMeetingByExchangeId(int exchangeId);
    Task<ExchangeMeeting> CreateExchangeMeeting(CreateExchangeMeetingDto dto, Guid proposerId);
    Task<ExchangeMeeting> CounterProposeMeeting(ExchangeMeeting meeting, CounterProposeMeetingDto dto, Guid newProposerId);
    Task<ExchangeMeeting> AcceptMeeting(ExchangeMeeting meeting);
    Task<ExchangeMeeting> MarkAsCompleted(ExchangeMeeting meeting, Guid userId);
    Task InvalidateCollateralExchanges(int book1Id, int book2Id, int completedMatchId);
}