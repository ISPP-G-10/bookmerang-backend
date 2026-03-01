using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Bookmerang.Api.Models;
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

    public async Task<UserPreferenceDto> UpsertAsync(Guid userId, UpsertUserPreferenceDto request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var pref = await _dbContext.UserPreferences
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (pref is null)
        {
            pref = new UserPreference
            {
                UserId = userId,
                CreatedAt = now
            };

            _dbContext.UserPreferences.Add(pref);
        }

        pref.RadioKm = request.RadioKm;
        pref.Extension = request.Extension;
        pref.Location = new Point(request.Longitude, request.Latitude) { SRID = 4326 };
        pref.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

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