using Bookmerang.Api.Models.DTOs;

public interface IUserPreferenceService
{
    Task<UserPreferenceDto?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserPreferenceDto> UpsertAsync(Guid userId, UpsertUserPreferenceDto request, CancellationToken cancellationToken = default);
    Task<TutorialStatusDto?> GetTutorialStatusAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<TutorialStatusDto?> SetTutorialStatusAsync(Guid userId, UpdateTutorialStatusDto request, CancellationToken cancellationToken = default);
}
