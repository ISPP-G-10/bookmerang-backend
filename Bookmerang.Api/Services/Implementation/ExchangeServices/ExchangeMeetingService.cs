using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Services.Interfaces.Books;
using Bookmerang.Api.Services.Interfaces.ExchangeInterfaces;
using Bookmerang.Api.Services.Interfaces.Inkdrops;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace Bookmerang.Api.Services.Implementation.ExchangeServices;

public class ExchangeMeetingService(AppDbContext db, IExchangeService exchange_service, IBookRepository bookRepository, IInkdropsService inkdrops_service) : IExchangeMeetingService
{
    private readonly AppDbContext _db = db;
    private readonly IExchangeService _exchange_service = exchange_service;
    private readonly IBookRepository _bookRepository = bookRepository;

    private readonly IInkdropsService _inkdrops_service = inkdrops_service;

    private IQueryable<ExchangeMeeting> MeetingsWithProposer =>
        _db.ExchangeMeetings.Include(m => m.Proposer).ThenInclude(p => p.BaseUser);

    private async Task LoadProposer(ExchangeMeeting meeting)
    {
        meeting.Proposer = await _db.RegularUsers
            .Include(u => u.BaseUser)
            .FirstAsync(u => u.Id == meeting.ProposerId);
    }

    // Helper reutilizable para validar campos y resolver la localización
    private async Task<Point> ValidateAndResolveLocation(ExchangeMode mode, int? bookspotId, double? latitud, double? longitud, DateTime scheduledAt)
    {
        if (scheduledAt < DateTime.UtcNow.AddMinutes(5))
            throw new ArgumentException("La fecha del encuentro no puede ser anterior a la actual, ni demasiado próxima a ella.");

        if (mode is ExchangeMode.BOOKSPOT or ExchangeMode.BOOKDROP && bookspotId == null)
            throw new ArgumentException("Se debe indicar el bookspot en el que se va a producir el encuentro.");

        if (mode == ExchangeMode.CUSTOM && (latitud == null || longitud == null))
            throw new ArgumentException("Se debe indicar la ubicación para un encuentro personalizado.");

        if (mode is ExchangeMode.BOOKSPOT or ExchangeMode.BOOKDROP)
        {
            var bookspot = await _db.Bookspots.FindAsync(bookspotId) ?? throw new ArgumentException("El bookspot indicado no existe.");
            if (mode == ExchangeMode.BOOKDROP && !bookspot.IsBookdrop)
                throw new ArgumentException("El bookspot indicado no es un establecimiento BookDrop.");
            return bookspot.Location;
        }

        return new Point(longitud!.Value, latitud!.Value) { SRID = 4326 };
    }

    public async Task<ExchangeMeeting?> GetExchangeMeeting(int meetingId)
    {
        return await MeetingsWithProposer.FirstOrDefaultAsync(m => m.ExchangeMeetingId == meetingId);
    }

    public async Task<List<ExchangeMeeting>> GetMeetingsByUserId(Guid proposerId)
    {
        return await MeetingsWithProposer.Where(m => m.ProposerId == proposerId).ToListAsync();
    }

    public async Task<List<ExchangeMeeting>> GetAllExchangeMeetings()
    {
        return await MeetingsWithProposer.ToListAsync();
    }

    public async Task<ExchangeMeeting?> GetMeetingByExchangeId(int exchangeId)
    {
        return await MeetingsWithProposer.FirstOrDefaultAsync(m => m.ExchangeId == exchangeId);
    }

    public async Task<ExchangeMeeting> CreateExchangeMeeting(CreateExchangeMeetingDto dto, Guid proposerId)
    {
        var location = await ValidateAndResolveLocation(dto.ExchangeMode, dto.BookspotId, dto.Latitud, dto.Longitud, dto.ScheduledAt);

        var meeting = new ExchangeMeeting
        {
            ExchangeId = dto.ExchangeId,
            ExchangeMode = dto.ExchangeMode,
            BookspotId = dto.BookspotId,
            CustomLocation = location,
            ScheduledAt = dto.ScheduledAt,
            ProposerId = proposerId
        };

        _db.ExchangeMeetings.Add(meeting);
        await _db.SaveChangesAsync();

        await LoadProposer(meeting);
        return meeting;
    }

    public async Task<ExchangeMeeting> CounterProposeMeeting(ExchangeMeeting meeting, CounterProposeMeetingDto dto, Guid newProposerId)
    {
        var location = await ValidateAndResolveLocation(dto.ExchangeMode, dto.BookspotId, dto.Latitud, dto.Longitud, dto.ScheduledAt);

        meeting.ExchangeMode = dto.ExchangeMode;
        meeting.BookspotId = dto.BookspotId;
        meeting.CustomLocation = location;
        meeting.ScheduledAt = dto.ScheduledAt;
        meeting.ProposerId = newProposerId;

        await _db.SaveChangesAsync();

        await LoadProposer(meeting);
        return meeting;
    }

    public async Task<ExchangeMeeting> MarkAsCompleted(ExchangeMeeting meeting, Guid userId)
    {
        if (meeting.ProposerId == userId)
            meeting.MarkAsCompletedByUser1 = true;
        else
            meeting.MarkAsCompletedByUser2 = true;

        if (meeting.MarkAsCompletedByUser1 && meeting.MarkAsCompletedByUser2)
        {
            var exchange = (await _exchange_service.GetExchangeWithMatch(meeting.ExchangeId))!;

            exchange.Status = ExchangeStatus.COMPLETED;
            exchange.UpdatedAt = DateTime.UtcNow;

            var book1 = await _bookRepository.GetByIdOrThrowAsync(exchange.Match!.Book1Id);
            var book2 = await _bookRepository.GetByIdOrThrowAsync(exchange.Match.Book2Id);

            book1.OwnerId = exchange.Match.User2Id;
            book2.OwnerId = exchange.Match.User1Id;

            book1.Status = BookStatus.EXCHANGED;
            book2.Status = BookStatus.EXCHANGED;

            await InvalidateCollateralExchanges(exchange.Match.Book1Id, exchange.Match.Book2Id, exchange.MatchId);
            await _inkdrops_service.GrantExchangeInkdropsAsync(exchange.Match.User1Id, exchange.Match.User2Id);
        }

        await _db.SaveChangesAsync();
        return meeting;
    }

    // Cambia estado de exchanges afectados por libros intercambiados para que se actualice su estado en el front
    public async Task InvalidateCollateralExchanges(int book1Id, int book2Id, int completedMatchId)
    {
        var affectedExchanges = await _db.Exchanges
            .Include(e => e.Match)
            .Where(e => e.MatchId != completedMatchId)
            .Where(e => e.Match.Book1Id == book1Id || e.Match.Book1Id == book2Id
                     || e.Match.Book2Id == book1Id || e.Match.Book2Id == book2Id)
            .Where(e => e.Status != ExchangeStatus.COMPLETED
                     && e.Status != ExchangeStatus.REJECTED
                     && e.Status != ExchangeStatus.INCIDENT)
            .ToListAsync();

        var affectedExchangeIds = affectedExchanges.Select(e => e.ExchangeId).ToList();

        foreach (var exchange in affectedExchanges)
        {
            exchange.Status = ExchangeStatus.REJECTED;
            exchange.UpdatedAt = DateTime.UtcNow;
        }

        var affectedMeetings = await _db.ExchangeMeetings
            .Where(m => affectedExchangeIds.Contains(m.ExchangeId))
            .ToListAsync();

        foreach (var meeting in affectedMeetings)
        {
            meeting.MeetingStatus = ExchangeMeetingStatus.REFUSED;
        }
    }

    public async Task<ExchangeMeeting> AcceptMeeting(ExchangeMeeting meeting)
    {
        meeting.MeetingStatus = ExchangeMeetingStatus.ACCEPTED;

        if (meeting.ExchangeMode == ExchangeMode.BOOKDROP
            && meeting.BookspotId.HasValue
            && meeting.BookDropStatus == null)
        {
            meeting.Pin = await GenerateUniquePin(meeting.BookspotId.Value);
            meeting.BookDropStatus = BookdropExchangeStatus.AWAITING_DROP_1;
        }

        await _db.SaveChangesAsync();

        return meeting;
    }

    /// Genera un PIN de 6 digitos unico entre los intercambios activos del mismo BookDrop
    private async Task<string> GenerateUniquePin(int bookspotId)
    {
        var random = new Random();
        string pin;
        bool exists;

        do
        {
            pin = random.Next(100000, 1000000).ToString();
            exists = await _db.ExchangeMeetings.AnyAsync(m =>
                m.BookspotId == bookspotId &&
                m.Pin == pin &&
                m.BookDropStatus != null &&
                m.BookDropStatus != BookdropExchangeStatus.COMPLETED
            );
        } while (exists);

        return pin;
    }
}