using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.DTOs;

namespace Bookmerang.Api.Services.Interfaces.ExchangeInterfaces;

public interface IExchangeService
{
    Task<Exchange?> GetExchangeById(int exchangeId);
    Task<Exchange?> GetExchangeByChatId(int chatId);
    Task<Exchange> CreateExchange(int chatId, int matchId);
    Task<Exchange> UpdateExchange(int exchangeId, UpdateExchangeDto dto);
    Task<bool> DeleteExchange(int exchangeId);
}