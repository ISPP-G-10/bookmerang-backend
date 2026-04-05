using Bookmerang.Api.Models.DTOs.Bookspots.Requests;
using Bookmerang.Api.Models.DTOs.Bookspots.Responses;

namespace Bookmerang.Api.Services.Interfaces.Bookspots;

public interface IBookspotService
{
    Task<List<BookspotDTO>> GetActiveAsync(CancellationToken ct = default);

    Task<List<BookspotDTO>> GetPendingAsync(CancellationToken ct = default);

    Task<List<BookspotNearbyDTO>> GetNearbyActiveAsync(double latitude, double longitude, double radiusKm, CancellationToken ct = default);

    Task<BookspotDTO> CreateAsync(string supabaseId, CreateBookspotRequest request, CancellationToken ct = default);

    Task<BookspotDTO?> GetByIdAsync(int bookspotId, CancellationToken ct = default);

    Task<BookspotDTO?> GetRandomPendingNearbyAsync(double latitude, double longitude, double radiusKm, string supabaseId, CancellationToken ct = default);

    Task<List<BookspotDTO>> GetUserPendingWithValidationCountAsync(string supabaseId, CancellationToken ct = default);

    Task<List<BookspotDTO>> GetUserActiveAsync(string supabaseId, CancellationToken ct = default);

    Task<BookspotDTO> UpdateNameAsync(string supabaseId, int bookspotId, string nombre, CancellationToken ct = default);

    Task DeleteAsync(string supabaseId, int bookspotId, CancellationToken ct = default);
}