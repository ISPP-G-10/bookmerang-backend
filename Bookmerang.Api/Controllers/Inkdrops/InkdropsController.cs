using Bookmerang.Api.Data;
using Bookmerang.Api.Services.Interfaces.Inkdrops;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Bookmerang.Api.Controllers.Inkdrops;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InkdropsController(IInkdropsService inkdropsService, AppDbContext db) : ControllerBase
{
    private readonly IInkdropsService _inkdropsService = inkdropsService;
    private readonly AppDbContext _db = db;

    private async Task<Guid?> GetCurrentUserId()
    {
        var supabaseId = User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        if (supabaseId == null) return null;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.SupabaseId == supabaseId);
        return user?.Id;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyInkdrops()
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var result = await _inkdropsService.GetUserInkdropsAsync(userId.Value);
        return Ok(result);
    }

    [HttpGet("community/{communityId}/ranking")]
    public async Task<IActionResult> GetCommunityRanking(int communityId)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        try
        {
            var result = await _inkdropsService.GetCommunityRankingAsync(userId.Value, communityId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("PREMIUM"))
                return new ObjectResult(new { message = ex.Message }) { StatusCode = StatusCodes.Status403Forbidden };
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetInkdropsHistory()
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var result = await _inkdropsService.GetInkdropsHistoryAsync(userId.Value);
        return Ok(result);
    }
}
