using Bookmerang.Api.Data;
using Bookmerang.Api.Services.Interfaces.ExchangeInterfaces;
using Bookmerang.Api.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using Bookmerang.Api.Models;

namespace Bookmerang.Api.Services.Implementation.ExchangeServices;

public class ExchangeService(AppDbContext db): IExchangeService
{
    private readonly AppDbContext _db = db;

    public async Task<Exchange?> GetExchangeById(string supabaseId)
    {
        return await _db.Exchanges.FirstOrDefaultAsync(e => e.SupabaseId == supabaseId);
    }
    public async Task<Exchange?> GetExchangeByChatId(int chatId)
    {
        return await _db.Exchanges.FirstOrDefaultAsync(e => e.ChatId == chatId);
    }
    public async Task<Exchange> CreateExchange(string supabase_id, int chatId, int matchId)
    {
        var exchange = new Exchange
        {
            ChatId = chatId,
            MatchId = matchId,
        };

        _db.Exchanges.Add(exchange);
        await _db.SaveChangesAsync();

        return exchange;
    }

    // Update que actualiza solo los campos que se proporcionen.
    // Los campos null que sean en el DTO se ignoran.
    public async Task<Exchange> UpdateExchange(string supabaseId, ExchangeDto dto)
    {
        var exchange = await _db.Exchanges.FirstOrDefaultAsync(e => e.SupabaseId == supabaseId);
        if (exchange == null)
            throw new InvalidOperationException($"Exchange con id {supabaseId} no encontrado");
        if (IsAllNull(dto))
            throw new InvalidOperationException("Al menos un parámetro debe tener un valor");
        
        if (dto.ChatId.HasValue)
            exchange.ChatId = dto.ChatId.Value;

        if (dto.MatchId.HasValue)
            exchange.MatchId = dto.MatchId.Value;

        if (dto.Status.HasValue)
            exchange.Status = dto.Status.Value;

        if (dto.CreatedAt.HasValue)
            exchange.CreatedAt = dto.CreatedAt.Value;

        if (dto.UpdatedAt.HasValue)
            exchange.UpdatedAt = dto.UpdatedAt.Value;

        _db.Exchanges.Update(exchange);
        await _db.SaveChangesAsync();

        return exchange;
    }

    private bool IsAllNull(ExchangeDto dto)
    => dto.ChatId == null && dto.MatchId == null && dto.Status == null 
       && dto.CreatedAt == null && dto.UpdatedAt == null;
    
    public async Task<bool> DeleteExchange(string supabaseId)
    {
        var exchange = await _db.Exchanges.FindAsync(supabaseId) ?? throw new Exception($"Exchange con id {supabaseId} no encontrado");
        
        _db.Exchanges.Remove(exchange);
        await _db.SaveChangesAsync();
        
        return exchange == null;
    }
}