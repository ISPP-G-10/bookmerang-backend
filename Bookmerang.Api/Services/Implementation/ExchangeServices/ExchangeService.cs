using Bookmerang.Api.Data;
using Bookmerang.Api.Exceptions;
using Bookmerang.Api.Services.Interfaces.Chats;
using Bookmerang.Api.Services.Interfaces.ExchangeInterfaces;
using Microsoft.EntityFrameworkCore;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Services.Implementation.ExchangeServices;

public class ExchangeService(AppDbContext db, IChatService chatService): IExchangeService
{
    private readonly AppDbContext _db = db;
    private readonly IChatService _chatService = chatService;
    
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

    public async Task<Exchange> UpdateExchangeStatus(Exchange exchange, ExchangeStatus newStatus)
    {
        exchange.Status = newStatus;
        exchange.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return exchange;
    }

    public async Task<Exchange> AcceptExchange(int exchangeId, Guid userId)
    {
        var exchange = await GetExchangeWithMatch(exchangeId)
            ?? throw new NotFoundException($"Intercambio con id {exchangeId} no encontrado.");

        var isUser1 = exchange.Match.User1Id == userId;
        var isUser2 = exchange.Match.User2Id == userId;

        var newStatus = (exchange.Status, isUser1, isUser2) switch
        {
            (ExchangeStatus.NEGOTIATING,   true,  _)   => ExchangeStatus.ACCEPTED_BY_1,
            (ExchangeStatus.NEGOTIATING,   _,    true) => ExchangeStatus.ACCEPTED_BY_2,
            (ExchangeStatus.ACCEPTED_BY_1, _,    true) => ExchangeStatus.ACCEPTED,
            (ExchangeStatus.ACCEPTED_BY_2, true, _)    => ExchangeStatus.ACCEPTED,
            _ => throw new ValidationException(
                "No se puede aceptar el intercambio en su estado actual.")
        };

        return await UpdateExchangeStatus(exchange, newStatus);
    }

    // TODO: A futuro, el borrado debería iniciarse desde el Match (Match -> Exchange -> Chat)
    // para liberar también el match y sus swipes asociados. Requiere MatchService + AdminMatchController.
    public async Task<bool> DeleteExchange(int exchangeId)
    {
        var exchange = await _db.Exchanges.FindAsync(exchangeId)
            ?? throw new NotFoundException($"Intercambio con id {exchangeId} no encontrado.");

        var meetings = await _db.ExchangeMeetings
            .Where(em => em.ExchangeId == exchangeId)
            .ToListAsync();
        _db.ExchangeMeetings.RemoveRange(meetings);

        await _chatService.DeleteChat(exchange.ChatId);

        _db.Exchanges.Remove(exchange);
        await _db.SaveChangesAsync();
        return true;
    }
}