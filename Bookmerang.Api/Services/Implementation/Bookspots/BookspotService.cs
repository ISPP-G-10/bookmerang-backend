using Bookmerang.Api.Data;
using Bookmerang.Api.Exceptions;
using Bookmerang.Api.Models.DTOs.Bookspots.Requests;
using Bookmerang.Api.Models.DTOs.Bookspots.Responses;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Services.Interfaces.Bookspots;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace Bookmerang.Api.Services.Implementation.Bookspots;

public class BookspotService(
    IBookspotRepository bookspotRepo,
    AppDbContext db
) : IBookspotService
{
    private const double MaxRadiusKm = 50;
    private const int MaxBookspotsPerMonth = 5;
    private const double DuplicateRadiusMeters = 5;

    public async Task<List<BookspotDTO>> GetActiveAsync(CancellationToken ct = default)
    {
        var bookspots = await bookspotRepo.GetActiveAsync(ct);
        return bookspots.Select(MapToDTO).ToList();
    }

    public async Task<List<BookspotDTO>> GetPendingAsync(CancellationToken ct = default)
    {
        var pending = await bookspotRepo.GetPendingAsync(ct);
        return pending.Select(MapToDTO).ToList();
    }

    public async Task<List<BookspotNearbyDTO>> GetNearbyActiveAsync(
    double latitude, double longitude, double radiusKm, CancellationToken ct = default)
    {
        if (radiusKm > MaxRadiusKm)
            throw new ValidationException($"El radio máximo permitido es {MaxRadiusKm} km.");

        var bookspots = await bookspotRepo.GetNearbyActiveAsync(latitude, longitude, radiusKm, ct);

        return bookspots
            .Select(x => MapToNearbyDTO(x.bookspot, x.distanceMeters))
            .OrderBy(b => b.DistanceKm)
            .ToList();
    }

    public async Task<BookspotDTO> CreateAsync(
        string supabaseId,
        CreateBookspotRequest request,
        CancellationToken ct = default)
    {
        var ownerId = await ResolveOwnerIdAsync(supabaseId, ct);

        var createdThisMonth = await bookspotRepo.CountCreatedByUserThisMonthAsync(ownerId, ct);
        if (createdThisMonth >= MaxBookspotsPerMonth)
            throw new ValidationException($"Has alcanzado el límite de {MaxBookspotsPerMonth} bookspots por mes.");

        var isDuplicate = await bookspotRepo.ExistsNearbyAsync(
            request.Latitude, request.Longitude, DuplicateRadiusMeters, ct);
        if (isDuplicate)
            throw new ValidationException("Ya existe un bookspot a menos de 5 metros de esa ubicación.");

        var bookspot = new Bookspot
        {
            Nombre = request.Nombre,
            AddressText = request.AddressText,
            Location = new Point(request.Longitude, request.Latitude) { SRID = 4326 },
            IsBookdrop = request.IsBookdrop,
            CreatedByUserId = ownerId,
            Status = BookspotStatus.PENDING,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await bookspotRepo.CreateAsync(bookspot, ct);
        return MapToDTO(created);
    }

    private async Task<Guid> ResolveOwnerIdAsync(string supabaseId, CancellationToken ct)
    {
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.SupabaseId == supabaseId, ct);

        if (user is null)
            throw new NotFoundException(
                $"No se encontró ningún usuario con supabaseId '{supabaseId}'.");

        return user.Id;
    }

    public async Task<BookspotDTO?> GetByIdAsync(int bookspotId, CancellationToken ct = default)
    {
        var bookspot = await bookspotRepo.GetByIdAsync(bookspotId, ct);
        return bookspot is null ? null : MapToDTO(bookspot);
    }

    public async Task<BookspotDTO?> GetRandomPendingNearbyAsync(
    double latitude, double longitude, double radiusKm, CancellationToken ct = default)
    {
        if (radiusKm > MaxRadiusKm)
            throw new ValidationException($"El radio máximo permitido es {MaxRadiusKm} km.");

        var candidates = await bookspotRepo.GetNearbyPendingAsync(latitude, longitude, radiusKm, ct);

        if (!candidates.Any()) return null;

        // Cogemos solo los que tienen menos validaciones y elegimos uno al azar
        var minValidations = candidates.Min(x => x.validationCount);
        var leastValidated = candidates
            .Where(x => x.validationCount == minValidations)
            .ToList();

        var random = leastValidated[Random.Shared.Next(leastValidated.Count)];
        return MapToDTO(random.bookspot);
    }

    private static BookspotDTO MapToDTO(Bookspot b) => new()
    {
        Id = b.Id,
        Nombre = b.Nombre,
        AddressText = b.AddressText,
        Latitude = b.Location.Y,
        Longitude = b.Location.X,
        IsBookdrop = b.IsBookdrop,
        Status = b.Status
    };

    private static BookspotNearbyDTO MapToNearbyDTO(Bookspot b, double distanceMeters) => new()
    {
        Id = b.Id,
        Nombre = b.Nombre,
        AddressText = b.AddressText,
        Latitude = b.Location.Y,
        Longitude = b.Location.X,
        IsBookdrop = b.IsBookdrop,
        DistanceKm = Math.Round(distanceMeters / 1000, 2)
    };
}