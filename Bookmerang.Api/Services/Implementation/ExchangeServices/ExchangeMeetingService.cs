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
        if(exchangeMode == ExchangeMode.BOOKSPOT && bookspotId == null)
        {
            throw new ArgumentException("Se debe indicar el bookspot en el que se va a producir el encuentro");
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
        
        var exchange = await _exchange_service.GetExchangeById(meeting.ExchangeId);
        var oldStatus = exchange!.Status;
        
        if (exchange == null)
            throw new InvalidOperationException($"Exchange con id {meeting.ExchangeId} no encontrado");

        if (IsAllNull(dto)) 
            throw new InvalidOperationException("Al menos un parámetro debe tener un valor");

        if(dto.ScheduledAt != null && dto.ScheduledAt < DateTime.UtcNow.AddMinutes(5))
        {
            throw new ArgumentException("La fecha del encuentro no puede ser anterior a la actual, ni demasiado próximo a ella");
        }
        if(dto.ExchangeMode == ExchangeMode.BOOKSPOT && dto.BookspotId == null)
        {
            throw new ArgumentException("Se debe indicar el bookspot en el que se va a producir el encuentro");
        }        

        if (dto.ExchangeMode.HasValue)
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

        if (dto.MarkAsCompletedByUser1.HasValue)
            meeting.MarkAsCompletedByUser1 = dto.MarkAsCompletedByUser1.Value;

        if (dto.MarkAsCompletedByUser2.HasValue)
            meeting.MarkAsCompletedByUser2 = dto.MarkAsCompletedByUser2.Value;

        if (IsCompleted(dto)) {
            exchange.Status = ExchangeStatus.COMPLETED;
        }
        else
        {
            if (dto.MeetingStatus.HasValue)
                meeting.MeetingStatus = dto.MeetingStatus.Value;
        }

        if(exchange.Status != oldStatus)
        {
            // ensure the exchange's timestamps have UTC kind before saving
            exchange.CreatedAt = DateTime.SpecifyKind(exchange.CreatedAt, DateTimeKind.Utc);
            exchange.UpdatedAt = DateTime.UtcNow;
            _db.Exchanges.Update(exchange);
        }

        
        
        _db.ExchangeMeetings.Update(meeting);
        
        await _db.SaveChangesAsync();

        return meeting;
    }

    private bool IsAllNull(UpdateExchangeMeetingDto dto)
    => dto.ExchangeMode == null && dto.BookspotId == null && 
       dto.CustomLocation == null && dto.ScheduledAt == null && 
       dto.MeetingStatus == null && dto.MarkAsCompletedByUser1 == null && 
       dto.MarkAsCompletedByUser2 == null;

    private bool IsCompleted(UpdateExchangeMeetingDto dto)
    => dto.MarkAsCompletedByUser1 == true && dto.MarkAsCompletedByUser2 == true;

    public async Task<bool> DeleteExchangeMeeting(int meetingId)
    {
        var meeting = await _db.ExchangeMeetings.FindAsync(meetingId) ?? throw new Exception($"Meeting con id {meetingId} no encontrado");
        
        _db.ExchangeMeetings.Remove(meeting);
        await _db.SaveChangesAsync();
        
        return true;
    }
}