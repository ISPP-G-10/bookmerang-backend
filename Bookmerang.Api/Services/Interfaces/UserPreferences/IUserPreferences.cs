using Bookmerang.Api.Models.DTOs;

public interface IUserPreferenceService
{
    Task<UserPreferenceDto?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserPreferenceDto> UpsertAsync(Guid userId, UpsertUserPreferenceDto request, CancellationToken cancellationToken = default);
}