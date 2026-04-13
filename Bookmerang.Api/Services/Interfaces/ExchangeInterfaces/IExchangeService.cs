using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Services.Interfaces.ExchangeInterfaces;

public interface IExchangeService
{
    Task<Exchange?> GetExchangeWithMatch(int exchangeId);
    Task<Exchange?> GetExchangeByChatIdWithMatch(Guid chatId);
    Task<List<Exchange>> GetAllExchanges();
    Task<Exchange> CreateExchange(Guid chatId, int matchId);
    Task<Exchange> UpdateExchangeStatus(Exchange exchange, ExchangeStatus newStatus);
    Task<Exchange> AcceptExchange(int exchangeId, Guid userId);
    Task<bool> DeleteExchange(int exchangeId);
}