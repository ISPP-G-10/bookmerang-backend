using Bookmerang.Api.Data;
using Bookmerang.Api.Models;
using Bookmerang.Api.Services.Interfaces.Auth;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace Bookmerang.Api.Services.Implementation.Exchanges;

public class ExchangeMeetingService(AppDbContext db) : IExchangeMeetingService
{
    private readonly AppDbContext _db = db;

    public async Task<ExchangeMeeting> GetMeetingByIdAsync(int id)
    {
        return await _db.ExchangeMeetings
            .Include(m => m.Bookspot)
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<List<ExchangeMeeting>> GetMeetingsByUserIdAsync(int userId)
    {
        return await _db.ExchangeMeetings
            .Include(m => m.Bookspot)
            .Include(m => m.User)
            .Where(m => m.UserId == userId)
            .ToListAsync();
    }

    // revisar
    public async Task<ExchangeMeeting> CreateMeetingAsync(int exchangeId, ExchangeMode exchangeMode, Guid proposerId, int bookspotId, DateTime scheduledAt, double latitude, double longitude)
    {
        var meeting = new ExchangeMeeting
        {
            ExchangeId = exchangeId,
            ExchangeMode = exchangeMode,
            BookspotId = bookspotId,
            CustomLocation = new Point(longitude, latitude) { SRID = 4326 },
            ProposerId = proposerId,
            ScheduledAt = scheduledAt,
        };

        _db.ExchangeMeetings.Add(meeting);
        await _db.SaveChangesAsync();

        return meeting;
    }
}