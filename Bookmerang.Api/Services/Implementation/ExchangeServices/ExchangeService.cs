using Bookmerang.Api.Data;
using Bookmerang.Api.Services.Interfaces.ExchangeInterfaces;
using Microsoft.EntityFrameworkCore;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Services.Implementation.ExchangeServices;

public class ExchangeService(AppDbContext db): IExchangeService
{
    private readonly AppDbContext _db = db;

    public async Task<Exchange?> GetExchangeById(int exchangeId)
    {
        return await _db.Exchanges.FirstOrDefaultAsync(e => e.ExchangeId == exchangeId);
    }
    
    public async Task<Exchange?> GetExchangeByChatIdWithMatch(int chatId)
    {
        return await _db.Exchanges
            .Include(e => e.Match)
            .FirstOrDefaultAsync(e => e.ChatId == chatId);
    }

    public async Task<Exchange?> GetExchangeWithMatch(int exchangeId)
    {
        return await _db.Exchanges
            .Include(e => e.Match)
            .FirstOrDefaultAsync(e => e.ExchangeId == exchangeId);
    }
     
    public async Task<List<Exchange>> GetAllExchanges()
    {
        return await _db.Exchanges.ToListAsync();
    }

    public async Task<Exchange> CreateExchange(int chatId, int matchId)
    {
        await ValidateChatAndMatchExist(chatId, matchId);
        await ValidateExchangeUniqueness(chatId, matchId);

        var exchange = new Exchange
        {
            ChatId = chatId,
            MatchId = matchId,
        };

        _db.Exchanges.Add(exchange);
        await _db.SaveChangesAsync();

        return exchange;
    }

    private async Task ValidateChatAndMatchExist(int chatId, int matchId)
    {
        if (!await _db.Chats.AnyAsync(c => c.Id == chatId))
        {
            throw new InvalidOperationException($"Chat con id {chatId} no existe.");
        }

        if (!await _db.Matches.AnyAsync(m => m.Id == matchId))
        {
            throw new InvalidOperationException($"Match con id {matchId} no existe.");
        }
    }

    private async Task ValidateExchangeUniqueness(int chatId, int matchId)
    {
        // Verificar si ya existe un exchange con el mismo chatId o matchId
        if (await _db.Exchanges.AnyAsync(e => e.ChatId == chatId && e.MatchId == matchId))
        {
            throw new InvalidOperationException($"Chat con id {chatId} y Match con id {matchId} ya usado en otro exchange.");
        }
        else if (await _db.Exchanges.AnyAsync(e => e.ChatId == chatId))
        {
            throw new InvalidOperationException($"Chat con id {chatId} ya usado en otro exchange.");
        }
        else if (await _db.Exchanges.AnyAsync(e => e.MatchId == matchId))
        {
            throw new InvalidOperationException($"Match con id {matchId} ya usado en otro exchange.");
        }
    }

    // Update que actualiza solo los campos que se proporcionen.
    public async Task<Exchange> UpdateExchangeStatus(int exchangeId, ExchangeStatus newStatus)
    {
        var exchange = await _db.Exchanges.FirstOrDefaultAsync(e => e.ExchangeId == exchangeId);
        if (exchange == null)
            throw new InvalidOperationException($"Exchange con id {exchangeId} no encontrado");
        if(exchange.Status == ExchangeStatus.REJECTED)
        {
            throw new InvalidOperationException("No se puede modificar un intercambio ya rechazado");
        }

        exchange.Status = newStatus;

        exchange.CreatedAt = DateTime.SpecifyKind(exchange.CreatedAt, DateTimeKind.Utc); // Aseguramos que CreatedAt se mantenga igual en UTC
        exchange.UpdatedAt = DateTime.UtcNow; //Siempre se actualiza la fecha de actualización

        _db.Exchanges.Update(exchange);
        await _db.SaveChangesAsync();

        return exchange;
    }
    
    public async Task<bool> DeleteExchange(int exchangeId)
    {
        var exchange = await _db.Exchanges.FindAsync(exchangeId) ?? throw new Exception($"Intercambio con id {exchangeId} no encontrado");
        
        // Eliminar primero los ExchangeMeetings asociados para evitar restricciones de clave foránea
        var exchangeMeetings = await _db.ExchangeMeetings.Where(em => em.ExchangeId == exchangeId).ToListAsync();
        _db.ExchangeMeetings.RemoveRange(exchangeMeetings);
        
        _db.Exchanges.Remove(exchange);
        await _db.SaveChangesAsync();
        
        return true;
    }
}