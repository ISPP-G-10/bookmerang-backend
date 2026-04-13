using Bookmerang.Api.Data;
using Bookmerang.Api.Exceptions;
using Bookmerang.Api.Models.DTOs.Bookspots.Requests;
using Bookmerang.Api.Models.DTOs.Bookspots.Responses;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
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
    private const int RequiredValidations = 5;
    private const double DuplicateRadiusMeters = 5;

    public async Task<List<BookspotDTO>> GetActiveAsync(CancellationToken ct = default)
    {
        var bookspots = await bookspotRepo.GetActiveAsync(ct);
        return bookspots
            .Select(b => MapToDTO(b))
            .OrderByDescending(b => b.IsBookdrop)
            .ThenBy(b => b.Id)
            .ToList();
    }

    public async Task<List<BookspotDTO>> GetPendingAsync(CancellationToken ct = default)
    {
        var pending = await bookspotRepo.GetPendingAsync(ct);
        return pending.Select(b => MapToDTO(b)).ToList();
    }

    public async Task<List<BookspotNearbyDTO>> GetNearbyActiveAsync(
    double latitude, double longitude, double radiusKm, CancellationToken ct = default)
    {
        if (radiusKm > MaxRadiusKm)
            throw new ValidationException($"El radio máximo permitido es {MaxRadiusKm} km.");

        var bookspots = await bookspotRepo.GetNearbyActiveAsync(latitude, longitude, radiusKm, ct);

        return bookspots
            .Select(x => MapToNearbyDTO(x.bookspot, x.distanceMeters, x.creatorUsername))
            .OrderByDescending(b => b.IsBookdrop)
            .ThenBy(b => b.DistanceKm)
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

    public async Task<BookspotDTO?> GetRandomPendingNearbyAsync(double latitude, double longitude, double radiusKm,
    string supabaseId, CancellationToken ct = default)
    {
        if (radiusKm > MaxRadiusKm)
            throw new ValidationException($"El radio máximo permitido es {MaxRadiusKm} km.");

        // ID del usuario actual
        var userId = await ResolveOwnerIdAsync(supabaseId, ct);

        // Candidatos pendientes cercanos (esto ya trae el validationCount por el Repo)
        var candidates = await bookspotRepo.GetNearbyPendingAsync(latitude, longitude, radiusKm, ct);

        var alreadyValidatedIds = await db.BookspotValidations
            .Where(v => v.ValidatorUserId == userId)
            .Select(v => v.BookspotId)
            .ToListAsync(ct);

        // Filtrado:
        // - Que no sea el creador (no autovalidarse)
        // - Que no esté en la lista de ya validados
        var filtered = candidates
            .Where(x => x.bookspot.CreatedByUserId != userId && !alreadyValidatedIds.Contains(x.bookspot.Id))
            .ToList();

        if (!filtered.Any()) return null;

        // Cogemos el valor mínimo de validaciones que haya en la lista
        var minValidations = filtered.Min(x => x.validationCount);
        var bestCandidates = filtered.Where(x => x.validationCount == minValidations).ToList();
        var randomCandidate = bestCandidates[Random.Shared.Next(bestCandidates.Count)];

        return MapToDTO(randomCandidate.bookspot, randomCandidate.validationCount);
    }

    public async Task<List<BookspotDTO>> GetUserPendingWithValidationCountAsync(
        string supabaseId, CancellationToken ct = default)
    {
        var ownerId = await ResolveOwnerIdAsync(supabaseId, ct);
        var pending = await bookspotRepo.GetUserPendingAsync(ownerId, ct);

        var result = new List<BookspotDTO>();
        foreach (var (bookspot, positiveCount) in pending)
        {
            if (positiveCount >= RequiredValidations)
            {
                await bookspotRepo.UpdateStatusAsync(bookspot.Id, BookspotStatus.ACTIVE, ct);
                var staleValidations = await db.BookspotValidations
                    .Where(v => v.BookspotId == bookspot.Id)
                    .ToListAsync(ct);
                if (staleValidations.Any())
                {
                    db.BookspotValidations.RemoveRange(staleValidations);
                    await db.SaveChangesAsync(ct);
                }
            }
            else
            {
                result.Add(MapToDTO(bookspot, positiveCount));
            }
        }
        return result;
    }

    public async Task<List<BookspotDTO>> GetUserActiveAsync(
        string supabaseId, CancellationToken ct = default)
    {
        var ownerId = await ResolveOwnerIdAsync(supabaseId, ct);
        var active = await bookspotRepo.GetUserActiveAsync(ownerId, ct);
        return active.Select(b => MapToDTO(b)).ToList();
    }

    private static BookspotDTO MapToDTO(Bookspot b) => new()
    {
        Id = b.Id,
        Nombre = b.Nombre,
        AddressText = b.AddressText,
        Latitude = b.Location.Y,
        Longitude = b.Location.X,
        IsBookdrop = b.IsBookdrop,
        Status = b.Status,
        ValidationCount = null,
        RequiredValidations = RequiredValidations,
        CreatedAt = b.CreatedAt,
        ValidatedAt = b.Status == BookspotStatus.ACTIVE ? b.UpdatedAt : null
    };

    private static BookspotDTO MapToDTO(Bookspot b, int validationCount) => new()
    {
        Id = b.Id,
        Nombre = b.Nombre,
        AddressText = b.AddressText,
        Latitude = b.Location.Y,
        Longitude = b.Location.X,
        IsBookdrop = b.IsBookdrop,
        Status = b.Status,
        ValidationCount = validationCount,
        RequiredValidations = RequiredValidations,
        CreatedAt = b.CreatedAt,
        ValidatedAt = b.Status == BookspotStatus.ACTIVE ? b.UpdatedAt : null
    };

    private static BookspotNearbyDTO MapToNearbyDTO(Bookspot b, double distanceMeters, string? creatorUsername = null) => new()
    {
        Id = b.Id,
        Nombre = b.Nombre,
        AddressText = b.AddressText,
        Latitude = b.Location.Y,
        Longitude = b.Location.X,
        IsBookdrop = b.IsBookdrop,
        DistanceKm = Math.Round(distanceMeters / 1000, 2),
        CreatorUsername = creatorUsername
    };

    public async Task<BookspotDTO> UpdateNameAsync(
        string supabaseId, int bookspotId, string nombre, CancellationToken ct = default)
    {
        var ownerId = await ResolveOwnerIdAsync(supabaseId, ct);
        var bookspot = await bookspotRepo.GetByIdAsync(bookspotId, ct);
        if (bookspot is null) throw new NotFoundException("Bookspot no encontrado.");
        if (bookspot.CreatedByUserId != ownerId) throw new ForbiddenException("No tienes permiso para modificar este bookspot.");
        if (bookspot.Status != BookspotStatus.PENDING)
            throw new ValidationException("Solo se puede renombrar un bookspot en estado PENDING.");
        await bookspotRepo.UpdateNameAsync(bookspotId, nombre, ct);
        bookspot.Nombre = nombre;
        return MapToDTO(bookspot);
    }

    public async Task DeleteAsync(string supabaseId, int bookspotId, CancellationToken ct = default)
    {
        var ownerId = await ResolveOwnerIdAsync(supabaseId, ct);

        var bookspot = await bookspotRepo.GetByIdAsync(bookspotId, ct);
        if (bookspot is null) throw new NotFoundException("Bookspot no encontrado.");
        if (bookspot.CreatedByUserId != ownerId) throw new ForbiddenException("No tienes permiso para eliminar este bookspot.");

        await bookspotRepo.DeleteAsync(bookspotId, ct);
    }
}
