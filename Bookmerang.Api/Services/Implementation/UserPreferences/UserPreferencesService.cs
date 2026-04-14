using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
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
            .Include(x => x.UserPreferenceGenres)
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        return pref is null ? null : ToDto(pref);
    }

    public async Task<UserPreferenceDto> UpsertAsync(Guid userId, UpsertUserPreferenceDto request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

    var userExists = await _dbContext.Set<BaseUser>()
        .AnyAsync(u => u.Id == userId, cancellationToken);

    if (!userExists)
        throw new Exception("User not found in base_users");

    var pref = await _dbContext.UserPreferences
        .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

    if (pref is null)
    {
        pref = new UserPreference
        {
            UserId = userId,
            CreatedAt = now,
            Location = new Point(request.Longitude, request.Latitude) { SRID = 4326 },
            Extension = request.Extension
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
            var repo = _dbContext.Set<UserPreferenceGenre>();

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
                .Select(gid => new UserPreferenceGenre { PreferencesId = pref.Id, GenreId = gid })
                .ToList();

            if (newEntries.Any())
            {
                await repo.AddRangeAsync(newEntries, cancellationToken);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // Always reload genres for the response
        pref.UserPreferenceGenres = await _dbContext.Set<UserPreferenceGenre>()
            .Where(x => x.PreferencesId == pref.Id)
            .ToListAsync(cancellationToken);

        return ToDto(pref);
    }

    public async Task<TutorialStatusDto?> GetTutorialStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.RegularUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        return user is null
            ? null
            : new TutorialStatusDto { TutorialCompleted = user.TutorialCompleted };
    }

    public async Task<TutorialStatusDto?> SetTutorialStatusAsync(Guid userId, UpdateTutorialStatusDto request, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.RegularUsers
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
            return null;

        user.TutorialCompleted = request.TutorialCompleted;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new TutorialStatusDto { TutorialCompleted = user.TutorialCompleted };
    }

    private static UserPreferenceDto ToDto(UserPreference pref)
    {
        return new UserPreferenceDto
        {
            Id = pref.Id,
            UserId = pref.UserId,
            RadioKm = pref.RadioKm,
            Extension = pref.Extension,
            GenreIds = pref.UserPreferenceGenres?.Select(x => x.GenreId).ToList() ?? new(),
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