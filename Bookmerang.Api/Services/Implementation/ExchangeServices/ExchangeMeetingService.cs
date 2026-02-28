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

    public async Task<ExchangeMeeting> UpdateExchangeMeeting(int exchangeId, 
        ExchangeMode exchangeMode, int? bookspotId, Point customLocation, DateTime? scheduledAt,
        Guid proposerId, ExchangeMeetingStatus meetingStatus, bool markAsCompletedByUser1,
        bool markAsCompletedByUser2)
    {
        var meeting = await _db.ExchangeMeetings.FindAsync(exchangeId) ?? throw new Exception("Meeting not found");

        meeting.ExchangeMode = exchangeMode;
        meeting.BookspotId = bookspotId;
        meeting.CustomLocation = customLocation;
        meeting.ScheduledAt = scheduledAt;
        meeting.ProposerId = proposerId;
        meeting.MeetingStatus = meetingStatus;
        meeting.MarkAsCompletedByUser1 = markAsCompletedByUser1;
        meeting.MarkAsCompletedByUser2 = markAsCompletedByUser2;

        await _db.SaveChangesAsync();

        return meeting;
    }

    private bool IsAllNull(ExchangeMeetingDto dto)
    => dto.ExchangeId == null && dto.ExchangeMode == null && dto.BookspotId == null && 
       dto.CustomLocation == null && dto.ScheduledAt == null && dto.ProposerId == null && 
       dto.MeetingStatus == null && dto.MarkAsCompletedByUser1 == null && 
       dto.MarkAsCompletedByUser2 == null;

    public async Task<bool> DeleteExchangeMeeting(string supabaseId)
    {
        var meeting = await _db.ExchangeMeetings.FindAsync(supabaseId) ?? throw new Exception("Meeting not found");
        
        _db.ExchangeMeetings.Remove(meeting);
        await _db.SaveChangesAsync();
        
        return meeting == null;
    }
}