using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Bookmerang.Api.Models;
using System.Linq;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Data;

public class UserPreferenceService : IUserPreferenceService
{
    private readonly AppDbContext _dbContext;

    public UserPreferenceService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserPreferenceDto?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var pref = await _dbContext.UserPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        return pref is null ? null : ToDto(pref);
    }

    public async Task<UserPreferenceDto> UpsertAsync(Guid supabaseUserId, UpsertUserPreferenceDto request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

    var user = await _dbContext.Set<BaseUser>()
        .Where(u => u.SupabaseId == supabaseUserId.ToString())
        .Select(u => u.Id)
        .FirstOrDefaultAsync(cancellationToken);

    if (user == Guid.Empty)
        throw new Exception("User not found in base_users");

    var pref = await _dbContext.UserPreferences
        .FirstOrDefaultAsync(x => x.UserId == user, cancellationToken);

    if (pref is null)
    {
        pref = new UserPreference
        {
            UserId = user,   
            CreatedAt = now
        };

        _dbContext.UserPreferences.Add(pref);
    }

        pref.RadioKm = request.RadioKm;
        pref.Extension = request.Extension;
        pref.Location = new Point(request.Longitude, request.Latitude) { SRID = 4326 };
        pref.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Sync genres if provided
        if (request.GenreIds is not null)
        {
            var repo = _dbContext.Set<UserPreferencesGenre>();

            var existing = await repo
                .Where(x => x.PreferencesId == pref.Id)
                .ToListAsync(cancellationToken);

            if (existing.Any())
            {
                repo.RemoveRange(existing);
            }

            var newEntries = request.GenreIds
                .Where(x => x > 0)
                .Distinct()
                .Select(gid => new UserPreferencesGenre { PreferencesId = pref.Id, GenreId = gid })
                .ToList();

            if (newEntries.Any())
            {
                await repo.AddRangeAsync(newEntries, cancellationToken);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return ToDto(pref);
    }

    private static UserPreferenceDto ToDto(UserPreference pref)
    {
        return new UserPreferenceDto
        {
            Id = pref.Id,
            UserId = pref.UserId,
            RadioKm = pref.RadioKm,
            Extension = pref.Extension,
            CreatedAt = pref.CreatedAt,
            UpdatedAt = pref.UpdatedAt,
            Location = new GeoPointDto
            {
                Latitude = pref.Location.Y,
                Longitude = pref.Location.X
            }
        };
    }
}