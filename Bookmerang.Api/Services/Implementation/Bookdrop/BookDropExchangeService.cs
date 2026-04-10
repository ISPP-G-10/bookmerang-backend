using Bookmerang.Api.Data;
using Bookmerang.Api.Models.DTOs.Bookdrop;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Interfaces.Bookdrop;
using Bookmerang.Api.Services.Interfaces.ExchangeInterfaces;
using Microsoft.EntityFrameworkCore;

namespace Bookmerang.Api.Services.Implementation.Bookdrop;

public class BookDropExchangeService(AppDbContext db, IExchangeMeetingService meetingService) : IBookDropExchangeService
{
    private readonly AppDbContext _db = db;
    private readonly IExchangeMeetingService _meetingService = meetingService;

    public async Task<List<BookDropExchangeDto>> GetActiveExchanges(int bookspotId)
    {
        await ValidateBookspotIsActive(bookspotId);

        var meetings = await _db.ExchangeMeetings
            .Include(m => m.Exchange)
                .ThenInclude(e => e.Match)
            .Where(m =>
                m.BookspotId == bookspotId &&
                m.ExchangeMode == ExchangeMode.BOOKDROP &&
                m.BookDropStatus != null &&
                m.BookDropStatus != BookdropExchangeStatus.COMPLETED &&
                m.Exchange.Status != ExchangeStatus.REJECTED &&
                m.Exchange.Status != ExchangeStatus.INCIDENT)
            .ToListAsync();

        var result = new List<BookDropExchangeDto>();

        foreach (var m in meetings)
        {
            var match = m.Exchange.Match;

            var user1 = await _db.Users.FindAsync(match.User1Id);
            var user2 = await _db.Users.FindAsync(match.User2Id);
            var book1 = await _db.Books.FindAsync(match.Book1Id);
            var book2 = await _db.Books.FindAsync(match.Book2Id);

            result.Add(new BookDropExchangeDto(
                m.ExchangeMeetingId,
                m.BookDropStatus!.Value,
                book1?.Titulo,
                book2?.Titulo,
                user1?.Name,
                user2?.Name,
                m.ScheduledAt
            ));
        }

        return result;
    }

    public async Task<BookDropExchangeDto> ConfirmDrop(int meetingId, string pin, int bookspotId)
    {
        var meeting = await GetAndValidateMeeting(meetingId, pin, bookspotId, BookdropExchangeStatus.AWAITING_DROP_1);

        meeting.BookDropStatus = BookdropExchangeStatus.BOOK_1_HELD;

        await _db.SaveChangesAsync();
        return await BuildDto(meeting);
    }

    public async Task<BookDropExchangeDto> ConfirmSwap(int meetingId, string pin, int bookspotId)
    {
        var meeting = await GetAndValidateMeeting(meetingId, pin, bookspotId, BookdropExchangeStatus.BOOK_1_HELD);

        meeting.BookDropStatus = BookdropExchangeStatus.BOOK_2_HELD;
        await _db.SaveChangesAsync();
        return await BuildDto(meeting);
    }

    public async Task<BookDropExchangeDto> ConfirmPickup(int meetingId, string pin, int bookspotId)
    {
        var meeting = await GetAndValidateMeeting(meetingId, pin, bookspotId, BookdropExchangeStatus.BOOK_2_HELD);

        meeting.BookDropStatus = BookdropExchangeStatus.COMPLETED;
        meeting.Pin = null;

        var exchange = await _db.Exchanges
            .Include(e => e.Match)
            .FirstAsync(e => e.ExchangeId == meeting.ExchangeId);

        exchange.Status = ExchangeStatus.COMPLETED;
        exchange.UpdatedAt = DateTime.UtcNow;

        var book1 = await _db.Books.FirstAsync(b => b.Id == exchange.Match.Book1Id);
        var book2 = await _db.Books.FirstAsync(b => b.Id == exchange.Match.Book2Id);

        book1.OwnerId = exchange.Match.User2Id;
        book2.OwnerId = exchange.Match.User1Id;

        book1.Status = BookStatus.EXCHANGED;
        book2.Status = BookStatus.EXCHANGED;

        await _meetingService.InvalidateCollateralExchanges(exchange.Match.Book1Id, exchange.Match.Book2Id, exchange.MatchId);

        await _db.SaveChangesAsync();
        return await BuildDto(meeting);
    }

    // --- Metodos privados ---

    private async Task<Models.Entities.ExchangeMeeting> GetAndValidateMeeting(
        int meetingId, string pin, int bookspotId, BookdropExchangeStatus expectedStatus)
    {
        await ValidateBookspotIsActive(bookspotId);

        var meeting = await _db.ExchangeMeetings
            .Include(m => m.Exchange)
                .ThenInclude(e => e.Match)
            .FirstOrDefaultAsync(m => m.ExchangeMeetingId == meetingId)
            ?? throw new InvalidOperationException("Meeting no encontrado");

        if (meeting.Exchange.Status == ExchangeStatus.REJECTED || meeting.Exchange.Status == ExchangeStatus.INCIDENT)
            throw new InvalidOperationException("Este intercambio ha sido cancelado o reportado.");

        if (meeting.BookspotId != bookspotId)
            throw new UnauthorizedAccessException("Este intercambio no pertenece a tu establecimiento");

        if (meeting.Pin != pin)
            throw new ArgumentException("PIN incorrecto");

        if (meeting.BookDropStatus != expectedStatus)
            throw new InvalidOperationException(
                $"Estado actual: {meeting.BookDropStatus}. Se esperaba: {expectedStatus}");

        return meeting;
    }

    private async Task<BookDropExchangeDto> BuildDto(Models.Entities.ExchangeMeeting meeting)
    {
        var exchange = await _db.Exchanges
            .Include(e => e.Match)
            .FirstAsync(e => e.ExchangeId == meeting.ExchangeId);
        var match = exchange.Match;

        var user1 = await _db.Users.FindAsync(match.User1Id);
        var user2 = await _db.Users.FindAsync(match.User2Id);
        var book1 = await _db.Books.FindAsync(match.Book1Id);
        var book2 = await _db.Books.FindAsync(match.Book2Id);

        return new BookDropExchangeDto(
            meeting.ExchangeMeetingId,
            meeting.BookDropStatus!.Value,
            book1?.Titulo,
            book2?.Titulo,
            user1?.Name,
            user2?.Name,
            meeting.ScheduledAt
        );
    }

    private async Task ValidateBookspotIsActive(int bookspotId)
    {
        var bookspot = await _db.Bookspots.FindAsync(bookspotId)
            ?? throw new UnauthorizedAccessException("Tu establecimiento no está disponible.");

        if (bookspot.Status != BookspotStatus.ACTIVE)
            throw new UnauthorizedAccessException("Tu establecimiento no está activo actualmente.");
    }
}
