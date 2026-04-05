using Bookmerang.Api.Models.DTOs.Bookspots.Requests;
using Bookmerang.Api.Models.DTOs.Bookspots.Responses;

namespace Bookmerang.Api.Services.Interfaces.Bookspots;

public interface IBookspotValidationService
{
    Task<BookspotValidationDTO> CreateAsync(string supabaseId, CreateBookspotValidationRequest request, CancellationToken ct = default);

    Task<List<BookspotValidationDTO>> GetByBookspotIdAsync(int bookspotId, CancellationToken ct = default);

    Task<BookspotValidationDTO> GetByIdAsync(int validationId, CancellationToken ct = default);
}