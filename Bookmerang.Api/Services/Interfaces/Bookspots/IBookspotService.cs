using Bookmerang.Api.Models.DTOs.Bookspots.Requests;
using Bookmerang.Api.Models.DTOs.Bookspots.Responses;

namespace Bookmerang.Api.Services.Interfaces.Bookspots;

public interface IBookspotService
{
    Task<List<BookspotDTO>> GetActiveAsync(CancellationToken ct = default);

    Task<List<BookspotNearbyDTO>> GetNearbyActiveAsync(double latitude, double longitude, double radiusKm, CancellationToken ct = default);

    Task<BookspotDTO> CreateAsync(string supabaseId, CreateBookspotRequest request, CancellationToken ct = default);

    Task<BookspotDTO?> GetByIdAsync(int bookspotId, CancellationToken ct = default);
}