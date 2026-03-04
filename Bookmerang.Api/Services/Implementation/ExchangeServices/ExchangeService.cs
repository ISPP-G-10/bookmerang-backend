using Bookmerang.Api.Data;
using Bookmerang.Api.Services.Interfaces.ExchangeInterfaces;
using Bookmerang.Api.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using Bookmerang.Api.Models.Entities;

namespace Bookmerang.Api.Services.Implementation.ExchangeServices;

public class ExchangeService(AppDbContext db): IExchangeService
{
    private readonly AppDbContext _db = db;

    public async Task<Exchange?> GetExchangeById(int exchangeId)
    {
        return await _db.Exchanges.FirstOrDefaultAsync(e => e.ExchangeId == exchangeId);
    }
    public async Task<Exchange?> GetExchangeByChatId(int chatId)
    {
        return await _db.Exchanges.FirstOrDefaultAsync(e => e.ChatId == chatId);
    }
    public async Task<Exchange> CreateExchange(int chatId, int matchId)
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
    public async Task<Exchange> UpdateExchange(int exchangeId, UpdateExchangeDto dto)
    {
        var exchange = await _db.Exchanges.FirstOrDefaultAsync(e => e.ExchangeId == exchangeId);
        if (exchange == null)
            throw new InvalidOperationException($"Exchange con id {exchangeId} no encontrado");
        if (IsAllNull(dto))
            throw new InvalidOperationException("Al menos un parámetro debe tener un valor");
        
        if (dto.ChatId.HasValue)
            exchange.ChatId = dto.ChatId.Value;

        if (dto.MatchId.HasValue)
            exchange.MatchId = dto.MatchId.Value;

        if (dto.Status.HasValue)
            exchange.Status = dto.Status.Value;

        // Make sure any existing timestamps have UTC kind before saving
        exchange.CreatedAt = DateTime.SpecifyKind(exchange.CreatedAt, DateTimeKind.Utc);
        exchange.UpdatedAt = DateTime.UtcNow; //Siempre se actualiza la fecha de actualización

        _db.Exchanges.Update(exchange);
        await _db.SaveChangesAsync();

        return exchange;
    }

    private bool IsAllNull(UpdateExchangeDto dto)
    => dto.ChatId == null && dto.MatchId == null && dto.Status == null;
    
    public async Task<bool> DeleteExchange(int exchangeId)
    {
        var exchange = await _db.Exchanges.FindAsync(exchangeId) ?? throw new Exception($"Intercambio con id {exchangeId} no encontrado");
        
        _db.Exchanges.Remove(exchange);
        await _db.SaveChangesAsync();
        
        // return true when deletion is successful
        return true;
    }
}