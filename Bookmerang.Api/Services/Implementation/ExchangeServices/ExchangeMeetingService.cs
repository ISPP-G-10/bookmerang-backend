using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Services.Interfaces.ExchangeInterfaces;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace Bookmerang.Api.Services.Implementation.ExchangeServices;

public class ExchangeMeetingService(AppDbContext db, IExchangeService exchange_service) : IExchangeMeetingService
{
    private readonly AppDbContext _db = db;
    private readonly IExchangeService _exchange_service = exchange_service;

    public async Task<ExchangeMeeting?> GetExchangeMeeting(int meetingId)
    {
        return await _db.ExchangeMeetings.Include(m => m.Proposer).FirstOrDefaultAsync(m => m.ExchangeMeetingId == meetingId);
    }

    public async Task<ExchangeMeeting?> GetExchangeMeetingWithRelations(int meetingId)
    {
        return await _db.ExchangeMeetings
            .Include(m => m.Exchange)
                .ThenInclude(e => e.Match)
            .FirstOrDefaultAsync(m => m.ExchangeMeetingId == meetingId);
    }

    // en teoría no hace falta poner los include según el diseño del modelo (navigation property)
    public async Task<List<ExchangeMeeting>> GetMeetingsByUserId(Guid proposerId)
    {
        return await _db.ExchangeMeetings
            .Include(m => m.Proposer)
            .Where(m => m.ProposerId == proposerId)
            .ToListAsync();
    }

    public async Task<List<ExchangeMeeting>> GetAllExchangeMeetings()
    {
        return await _db.ExchangeMeetings.Include(m => m.Proposer).ToListAsync();
    }

    public async Task<ExchangeMeeting?> GetMeetingByExchangeId(int exchangeId)
    {
        return await _db.ExchangeMeetings.FirstOrDefaultAsync(m => m.ExchangeId == exchangeId);
    }

    // se supone que no da fallo los valores opcionales
    public async Task<ExchangeMeeting> CreateExchangeMeeting(int exchangeId, ExchangeMode exchangeMode, Guid proposerId, int? bookspotId, DateTime? scheduledAt, Point customLocation)
    {

        if(scheduledAt != null && scheduledAt < DateTime.UtcNow.AddMinutes(5))
        {
            throw new ArgumentException("La fecha del encuentro no puede ser anterior a la actual, ni demasiado próximo a ella");
        }
        if((exchangeMode == ExchangeMode.BOOKSPOT || exchangeMode == ExchangeMode.BOOKDROP) && bookspotId == null)
        {
            throw new ArgumentException("Se debe indicar el bookspot en el que se va a producir el encuentro");
        }
        if(exchangeMode == ExchangeMode.BOOKDROP)
        {
            var bookspot = await _db.Bookspots.FindAsync(bookspotId);
            if (bookspot == null || !bookspot.IsBookdrop)
                throw new ArgumentException("El bookspot indicado no es un establecimiento BookDrop.");
        }
        var meeting = new ExchangeMeeting
        {
            ExchangeId = exchangeId,
            ExchangeMode = exchangeMode,
            BookspotId = bookspotId,
            CustomLocation = customLocation,
            ScheduledAt = scheduledAt,
            ProposerId = proposerId
        };

        _db.ExchangeMeetings.Add(meeting);
        await _db.SaveChangesAsync();

        return meeting;
    }

    public async Task<ExchangeMeeting> UpdateExchangeMeeting(int meetingId, UpdateExchangeMeetingDto dto)
    {
        var meeting = await _db.ExchangeMeetings.FirstOrDefaultAsync(m => m.ExchangeMeetingId == meetingId);
        if (meeting == null)
            throw new InvalidOperationException($"Meeting con id {meetingId} no encontrado");
        
        var exchange = await _exchange_service.GetExchangeWithMatch(meeting.ExchangeId);
        
        if (exchange == null)
            throw new InvalidOperationException($"Exchange con id {meeting.ExchangeId} no encontrado");

        var oldStatus = exchange.Status;

        if (IsAllNull(dto)) 
            throw new InvalidOperationException("Al menos un parámetro debe tener un valor");

        if(dto.ScheduledAt != null && dto.ScheduledAt < DateTime.UtcNow.AddMinutes(5))
        {
            throw new ArgumentException("La fecha del encuentro no puede ser anterior a la actual, ni demasiado próxima a ella");
        }
        if((dto.ExchangeMode == ExchangeMode.BOOKSPOT || dto.ExchangeMode == ExchangeMode.BOOKDROP) && dto.BookspotId == null)
        {
            throw new ArgumentException("Se debe indicar el bookspot en el que se va a producir el encuentro");
        }
        if(dto.ExchangeMode == ExchangeMode.BOOKDROP && dto.BookspotId.HasValue)
        {
            var bookspot = await _db.Bookspots.FindAsync(dto.BookspotId.Value);
            if (bookspot == null || !bookspot.IsBookdrop)
                throw new ArgumentException("El bookspot indicado no es un establecimiento BookDrop.");
        }

        if (dto.ExchangeMode.HasValue && meeting.BookDropStatus == null)
            meeting.ExchangeMode = dto.ExchangeMode.Value;

        if (dto.BookspotId.HasValue)
            meeting.BookspotId = dto.BookspotId.Value;

        if (dto.CustomLocation != null && dto.CustomLocation.Length >= 2)
            meeting.CustomLocation = new Point(dto.CustomLocation[0], dto.CustomLocation[1]) { SRID = 4326 };

        if (dto.ScheduledAt.HasValue)
            meeting.ScheduledAt = DateTime.SpecifyKind(dto.ScheduledAt.Value, DateTimeKind.Utc); //Conversión explicita a utc

        meeting.ScheduledAt = meeting.ScheduledAt.HasValue //Si no se entra en el if anterior, la fecha puede quedar como unspecified, eso da fallos en el update
        ? DateTime.SpecifyKind(meeting.ScheduledAt.Value, DateTimeKind.Utc)
        : null;

        // En modo BOOKDROP, el completado lo gestiona el establecimiento, no los usuarios
        if (meeting.ExchangeMode != ExchangeMode.BOOKDROP)
        {
            if (dto.MarkAsCompletedByUser1.HasValue)
                meeting.MarkAsCompletedByUser1 = dto.MarkAsCompletedByUser1.Value;

            if (dto.MarkAsCompletedByUser2.HasValue)
                meeting.MarkAsCompletedByUser2 = dto.MarkAsCompletedByUser2.Value;
        }

        if (IsCompleted(meeting)) {
            exchange.Status = ExchangeStatus.COMPLETED;

            if (exchange.Match == null)
                throw new InvalidOperationException($"Match no encontrado para exchange con id {exchange.ExchangeId}");

            var book1 = await _db.Books.FirstOrDefaultAsync(b => b.Id == exchange.Match.Book1Id);
            var book2 = await _db.Books.FirstOrDefaultAsync(b => b.Id == exchange.Match.Book2Id);

            if (book1 == null || book2 == null)
                throw new InvalidOperationException($"No se han encontrado los libros del match para exchange con id {exchange.ExchangeId}");

            book1.OwnerId = exchange.Match.User2Id;
            book2.OwnerId = exchange.Match.User1Id;
        }
        else
        {
            if (dto.MeetingStatus.HasValue)
            {
                meeting.MeetingStatus = dto.MeetingStatus.Value;

                // Si es BOOKDROP y pasa a ACCEPTED, generar PIN e iniciar ciclo
                if (dto.MeetingStatus.Value == ExchangeMeetingStatus.ACCEPTED
                    && meeting.ExchangeMode == ExchangeMode.BOOKDROP
                    && meeting.BookspotId.HasValue)
                {
                    meeting.Pin = await GenerateUniquePin(meeting.BookspotId.Value);
                    meeting.BookDropStatus = BookdropExchangeStatus.AWAITING_DROP_1;
                }
            }
        }

        if(exchange.Status != oldStatus)
        {
            // Solo actualizamos campos de estado; evitamos tocar created_at.
            exchange.UpdatedAt = DateTime.UtcNow;
        }
        
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            var detail = ex.InnerException?.Message ?? ex.Message;
            throw new InvalidOperationException($"Error al guardar el cierre del intercambio: {detail}", ex);
        }

        return meeting;
    }

    private bool IsAllNull(UpdateExchangeMeetingDto dto)
    => dto.ExchangeMode == null && dto.BookspotId == null && 
       dto.CustomLocation == null && dto.ScheduledAt == null && 
       dto.MeetingStatus == null && dto.MarkAsCompletedByUser1 == null && 
       dto.MarkAsCompletedByUser2 == null;

    private static bool IsCompleted(ExchangeMeeting meeting)
    => meeting.MarkAsCompletedByUser1 && meeting.MarkAsCompletedByUser2;

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

    public async Task<bool> DeleteExchangeMeeting(int meetingId)
    {
        var meeting = await _db.ExchangeMeetings.FindAsync(meetingId) ?? throw new Exception($"Meeting con id {meetingId} no encontrado");
        
        _db.ExchangeMeetings.Remove(meeting);
        await _db.SaveChangesAsync();
        
        return true;
    }
}