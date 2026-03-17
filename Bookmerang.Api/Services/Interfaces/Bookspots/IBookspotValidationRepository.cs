using Bookmerang.Api.Models.Entities;

namespace Bookmerang.Api.Services.Interfaces.Bookspots;

public interface IBookspotValidationRepository
{
    Task<BookspotValidation> CreateAsync(BookspotValidation validation, CancellationToken ct = default);

    Task<List<BookspotValidation>> GetByBookspotIdAsync(int bookspotId, CancellationToken ct = default);

    Task<BookspotValidation?> GetByIdAsync(int validationId, CancellationToken ct = default);

    Task DeleteByBookspotIdAsync(int bookspotId, CancellationToken ct = default);

}