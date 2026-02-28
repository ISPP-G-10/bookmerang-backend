using Bookmerang.Api.Data;
using Bookmerang.Api.Models;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Services.Interfaces.ExchangeInterfaces;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace Bookmerang.Api.Services.Implementation.ExchangeServices;

public class ExchangeMeetingService(AppDbContext db) : IExchangeMeetingService
{
    private readonly AppDbContext _db = db;

    public async Task<ExchangeMeeting?> GetExchangeMeeting(string supabaseId)
    {
        return await _db.ExchangeMeetings.FirstOrDefaultAsync(m => m.SupabaseId == supabaseId);
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

    public async Task<ExchangeMeeting> UpdateExchangeMeeting(string supabaseId, ExchangeMeetingDto dto)
    {
        var meeting = await _db.ExchangeMeetings.FirstOrDefaultAsync(m => m.SupabaseId == supabaseId);
        if (meeting == null)
            throw new InvalidOperationException($"Meeting con id {supabaseId} no encontrado");

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

        if (dto.ProposerId.HasValue)
            meeting.ProposerId = dto.ProposerId.Value;

        if (dto.MarkAsCompletedByUser1.HasValue)
            meeting.MarkAsCompletedByUser1 = dto.MarkAsCompletedByUser1.Value;

        if (dto.MarkAsCompletedByUser2.HasValue)
            meeting.MarkAsCompletedByUser2 = dto.MarkAsCompletedByUser2.Value;

        if (IsCompleted(dto)) {
            meeting.MeetingStatus = ExchangeMeetingStatus.ACCEPTED;
        } else
        {
            if (dto.MeetingStatus.HasValue)
                meeting.MeetingStatus = dto.MeetingStatus.Value;
        }

        _db.ExchangeMeetings.Update(meeting);
        await _db.SaveChangesAsync();

        return meeting;
    }

    private bool IsAllNull(ExchangeMeetingDto dto)
    => dto.ExchangeId == null && dto.ExchangeMode == null && dto.BookspotId == null && 
       dto.CustomLocation == null && dto.ScheduledAt == null && dto.ProposerId == null && 
       dto.MeetingStatus == null && dto.MarkAsCompletedByUser1 == null && 
       dto.MarkAsCompletedByUser2 == null;

    private bool IsCompleted(ExchangeMeetingDto dto)
    => dto.MarkAsCompletedByUser1 == true && dto.MarkAsCompletedByUser2 == true;

    public async Task<bool> DeleteExchangeMeeting(string supabaseId)
    {
        var meeting = await _db.ExchangeMeetings.FindAsync(supabaseId) ?? throw new Exception($"Meeting con id {supabaseId} no encontrado");
        
        _db.ExchangeMeetings.Remove(meeting);
        await _db.SaveChangesAsync();
        
        return meeting == null;
    }
}