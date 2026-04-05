using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Services.Interfaces.Bookspots;

public interface IBookspotRepository
{
    Task<Bookspot?> GetByIdAsync(int bookspotId, CancellationToken ct = default);

    Task<List<Bookspot>> GetActiveAsync(CancellationToken ct = default);

    Task<List<Bookspot>> GetPendingAsync(CancellationToken ct = default);

    Task<List<(Bookspot bookspot, double distanceMeters, string creatorUsername)>> GetNearbyActiveAsync(double latitude, double longitude, double radiusKm, CancellationToken ct = default);

    Task<Bookspot> CreateAsync(Bookspot bookspot, CancellationToken ct = default);

    Task<int> CountCreatedByUserThisMonthAsync(Guid userId, CancellationToken ct = default);

    Task<bool> ExistsNearbyAsync(double latitude, double longitude, double radiusMeters, CancellationToken ct = default);

    Task UpdateStatusAsync(int bookspotId, BookspotStatus status, CancellationToken ct = default);

    Task<List<(Bookspot bookspot, int validationCount)>> GetNearbyPendingAsync(double latitude, double longitude, double radiusKm, CancellationToken ct = default);

    Task<List<(Bookspot bookspot, int validationCount)>> GetUserPendingAsync(Guid userId, CancellationToken ct = default);

    Task<List<Bookspot>> GetUserActiveAsync(Guid userId, CancellationToken ct = default);

    Task UpdateNameAsync(int bookspotId, string nombre, CancellationToken ct = default);

    Task DeleteAsync(int bookspotId, CancellationToken ct = default);
}