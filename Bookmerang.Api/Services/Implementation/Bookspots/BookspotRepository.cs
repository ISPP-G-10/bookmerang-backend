using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Services.Interfaces.Bookspots;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace Bookmerang.Api.Services.Implementation.Bookspots;

public class BookspotRepository(AppDbContext db) : IBookspotRepository
{
    public async Task<Bookspot?> GetByIdAsync(int bookspotId, CancellationToken ct = default)
        => await db.Bookspots.FirstOrDefaultAsync(b => b.Id == bookspotId, ct);

    public async Task<List<Bookspot>> GetActiveAsync(CancellationToken ct = default)
        => await db.Bookspots
            .Where(b => b.Status == BookspotStatus.ACTIVE)
            .ToListAsync(ct);

    public async Task<List<Bookspot>> GetPendingAsync(CancellationToken ct = default)
        => await db.Bookspots
            .Where(b => b.Status == BookspotStatus.PENDING)
            .ToListAsync(ct);

    public async Task<List<(Bookspot bookspot, double distanceMeters, string creatorUsername)>> GetNearbyActiveAsync(
    double latitude, double longitude, double radiusKm, CancellationToken ct = default)
    {
        var userLocation = new Point(longitude, latitude) { SRID = 4326 };
        var radiusMeters = radiusKm * 1000;

        var results = await (
            from b in db.Bookspots
            join bu in db.Users on b.CreatedByUserId equals bu.Id
            where b.Status == BookspotStatus.ACTIVE
               && b.Location.IsWithinDistance(userLocation, radiusMeters)
            select new
            {
                Bookspot = b,
                Distance = b.Location.Distance(userLocation),
                Username = bu.Username
            }
        ).ToListAsync(ct);

        return results
            .Select(x => (x.Bookspot, x.Distance, x.Username))
            .ToList();
    }

    public async Task<Bookspot> CreateAsync(Bookspot bookspot, CancellationToken ct = default)
    {
        db.Bookspots.Add(bookspot);
        await db.SaveChangesAsync(ct);
        return bookspot;
    }

    public async Task<int> CountCreatedByUserThisMonthAsync(Guid userId, CancellationToken ct = default)
    {
        var firstDayOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        return await db.Bookspots
            .CountAsync(b => b.CreatedByUserId == userId
                          && b.CreatedAt >= firstDayOfMonth, ct);
    }

    public async Task<bool> ExistsNearbyAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        CancellationToken ct = default)
    {
        var point = new Point(longitude, latitude) { SRID = 4326 };

        return await db.Bookspots
            .AnyAsync(b => b.Status != BookspotStatus.REJECTED
                        && b.Location.IsWithinDistance(point, radiusMeters), ct);
    }

    public async Task UpdateStatusAsync(int bookspotId, BookspotStatus status, CancellationToken ct = default)
    {
        var bookspot = await db.Bookspots.FirstOrDefaultAsync(b => b.Id == bookspotId, ct);
        if (bookspot is null) return;

        bookspot.Status = status;
        bookspot.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<(Bookspot bookspot, int validationCount)>> GetNearbyPendingAsync(
    double latitude, double longitude, double radiusKm, CancellationToken ct = default)
    {
        var userLocation = new Point(longitude, latitude) { SRID = 4326 };
        var radiusMeters = radiusKm * 1000;

        return await db.Bookspots
            .Where(b => b.Status == BookspotStatus.PENDING
                     && b.Location.IsWithinDistance(userLocation, radiusMeters))
            .Select(b => new
            {
                Bookspot = b,
                ValidationCount = db.BookspotValidations.Count(v => v.BookspotId == b.Id)
            })
            .ToListAsync(ct)
            .ContinueWith(t => t.Result
                .Select(x => (x.Bookspot, x.ValidationCount))
                .ToList(), ct);
    }

    public async Task<List<(Bookspot bookspot, int validationCount)>> GetUserPendingAsync(
        Guid userId, CancellationToken ct = default)
    {
        // Solo contamos validaciones POSITIVAS (KnowsPlace && SafeForExchange)
        // para que el conteo sea coherente con el umbral de activación del servicio.
        return await db.Bookspots
            .Where(b => b.CreatedByUserId == userId
                     && b.Status == BookspotStatus.PENDING)
            .Select(b => new
            {
                Bookspot = b,
                ValidationCount = db.BookspotValidations
                    .Count(v => v.BookspotId == b.Id && v.KnowsPlace && v.SafeForExchange)
            })
            .ToListAsync(ct)
            .ContinueWith(t => t.Result
                .Select(x => (x.Bookspot, x.ValidationCount))
                .ToList(), ct)
            .ConfigureAwait(false);
    }

    public async Task<List<Bookspot>> GetUserActiveAsync(Guid userId, CancellationToken ct = default)
        => await db.Bookspots
            .Where(b => b.CreatedByUserId == userId && b.Status == BookspotStatus.ACTIVE)
            .OrderByDescending(b => b.UpdatedAt)
            .ToListAsync(ct);

    public async Task UpdateNameAsync(int bookspotId, string nombre, CancellationToken ct = default)
    {
        var bookspot = await db.Bookspots.FirstOrDefaultAsync(b => b.Id == bookspotId, ct);
        if (bookspot is null) return;
        bookspot.Nombre = nombre;
        bookspot.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int bookspotId, CancellationToken ct = default)
    {
        var bookspot = await db.Bookspots.FirstOrDefaultAsync(b => b.Id == bookspotId, ct);
        if (bookspot is null) return;

        var validations = await db.BookspotValidations
            .Where(v => v.BookspotId == bookspotId)
            .ToListAsync(ct);

        if (validations.Any())
            db.BookspotValidations.RemoveRange(validations);

        db.Bookspots.Remove(bookspot);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            // Si hay una concurrencia, el bookspot ya ha sido eliminado otro proceso
        }
    }
}