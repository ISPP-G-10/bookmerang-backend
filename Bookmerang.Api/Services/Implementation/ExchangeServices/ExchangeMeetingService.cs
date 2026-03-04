using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Services.Interfaces.ExchangeInterfaces;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace Bookmerang.Api.Services.Implementation.ExchangeServices;

public class ExchangeMeetingService(AppDbContext db, ExchangeService exchange_service) : IExchangeMeetingService
{
    private readonly AppDbContext _db = db;
    private readonly ExchangeService _exchange_service = exchange_service;

    public async Task<ExchangeMeeting?> GetExchangeMeeting(int meetingId)
    {
        return await _db.ExchangeMeetings.FirstOrDefaultAsync(m => m.ExchangeMeetingId == meetingId);
    }

    // en teoría no hace falta poner los include según el diseño del modelo (navigation property)
    public async Task<List<ExchangeMeeting>> GetMeetingsByUserId(Guid proposerId)
    {
        return await _db.ExchangeMeetings
            // .Include(m => m.Bookspot)
            // .Include(m => m.User)
            .Where(m => m.ProposerId == proposerId)
            .ToListAsync();
    }

    // se supone que no da fallo los valores opcionales
    public async Task<ExchangeMeeting> CreateExchangeMeeting(int exchangeId, ExchangeMode exchangeMode, Guid proposerId, int? bookspotId, DateTime? scheduledAt, Point customLocation)
    {
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
        
        if (exchange == null)
            throw new InvalidOperationException($"Exchange con id {meeting.ExchangeId} no encontrado");

        if (IsAllNull(dto)) 
            throw new InvalidOperationException("Al menos un parámetro debe tener un valor");        

        if (dto.ExchangeMode.HasValue)
            meeting.ExchangeMode = dto.ExchangeMode.Value;

        if (dto.BookspotId.HasValue)
            meeting.BookspotId = dto.BookspotId.Value;

        if (dto.CustomLocation != null)
            meeting.CustomLocation = dto.CustomLocation;

        if (dto.ScheduledAt.HasValue)
            meeting.ScheduledAt = dto.ScheduledAt.Value;

        if (dto.MarkAsCompletedByUser1.HasValue)
            meeting.MarkAsCompletedByUser1 = dto.MarkAsCompletedByUser1.Value;

        if (dto.MarkAsCompletedByUser2.HasValue)
            meeting.MarkAsCompletedByUser2 = dto.MarkAsCompletedByUser2.Value;

        if (IsCompleted(dto)) {
            exchange.Status = ExchangeStatus.COMPLETED;
        }
        else if (IsRefused(dto)) // estaria bien si por defecto fuesen null los campos de marcar como completado
        {
            exchange.Status = ExchangeStatus.REJECTED;
        }
        else
        {
            if (dto.MeetingStatus.HasValue)
                meeting.MeetingStatus = dto.MeetingStatus.Value;
        }

        _db.ExchangeMeetings.Update(meeting);
        _db.Exchanges.Update(exchange);
        
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

    private bool IsRefused(UpdateExchangeMeetingDto dto)
    => dto.MarkAsCompletedByUser1 == false && dto.MarkAsCompletedByUser2 == false;

    public async Task<bool> DeleteExchangeMeeting(int meetingId)
    {
        var meeting = await _db.ExchangeMeetings.FindAsync(meetingId) ?? throw new Exception($"Meeting con id {meetingId} no encontrado");
        
        _db.ExchangeMeetings.Remove(meeting);
        await _db.SaveChangesAsync();
        
        return meeting == null;
    }
}