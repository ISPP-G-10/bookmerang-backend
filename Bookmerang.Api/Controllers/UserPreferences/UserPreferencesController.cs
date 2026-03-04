using Microsoft.AspNetCore.Mvc;
using Bookmerang.Api.Models.DTOs;

[ApiController]
[Route("api/users/{userId:guid}/preferences")]
public class UserPreferencesController : ControllerBase
{
    private readonly IUserPreferenceService _userPreferenceService;

    public UserPreferencesController(IUserPreferenceService userPreferenceService)
    {
        _userPreferenceService = userPreferenceService;
    }

    [HttpGet]
    public async Task<ActionResult<UserPreferenceDto>> GetByUserId(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken)
    {
        var result = await _userPreferenceService.GetByUserIdAsync(userId, cancellationToken);

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    [HttpPut]
    public async Task<ActionResult<UserPreferenceDto>> Upsert(
        [FromRoute] Guid userId,
        [FromBody] UpsertUserPreferenceDto request,
        CancellationToken cancellationToken)
    {
        var result = await _userPreferenceService.UpsertAsync(userId, request, cancellationToken);
        return Ok(result);
    }
}