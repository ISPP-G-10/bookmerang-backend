using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Models.DTOs;

namespace Bookmerang.Api.Services.Interfaces.ExchangeInterfaces;

public interface IExchangeService
{
    Task<Exchange?> GetExchangeById(int exchangeId);
    Task<Exchange?> GetExchangeByChatId(int chatId);
    Task<List<Exchange>> GetAllExchanges();
    Task<Exchange> CreateExchange(int chatId, int matchId);
    Task<Exchange> UpdateExchangeStatus(int exchangeId, ExchangeStatus newStatus);
    Task<bool> DeleteExchange(int exchangeId);
}