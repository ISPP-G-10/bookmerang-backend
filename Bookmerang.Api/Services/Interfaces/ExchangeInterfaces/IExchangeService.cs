using Bookmerang.Api.Models;
using Bookmerang.Api.Models.DTOs;

namespace Bookmerang.Api.Services.Interfaces.ExchangeInterfaces;

public interface IExchangeService
{
    Task<Exchange?> GetExchangeById(string supabaseId);
    Task<Exchange?> GetExchangeByChatId(int chatId);
    Task<Exchange> CreateExchange(string supabase_id, int chatId, int matchId);
    Task<Exchange> UpdateExchange(string supabaseId, ExchangeDto dto);
    Task<bool> DeleteExchange(string supabaseId);
}