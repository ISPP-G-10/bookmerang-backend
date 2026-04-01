using Bookmerang.Api.Data;
using Bookmerang.Api.Models.DTOs.Communities;
using Bookmerang.Api.Services.Interfaces.Communities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Bookmerang.Api.Controllers.Communities;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CommunitiesController(
    ICommunityService communityService,
    IMeetupService meetupService,
    ICommunityLibraryService libraryService,
    AppDbContext db) : ControllerBase
{
    private readonly ICommunityService _communityService = communityService;
    private readonly IMeetupService _meetupService = meetupService;
    private readonly ICommunityLibraryService _libraryService = libraryService;
    private readonly AppDbContext _db = db;

    private async Task<Guid?> GetCurrentUserId()
    {
        var supabaseId = User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        if (supabaseId == null) return null;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.SupabaseId == supabaseId);
        return user?.Id;
    }

    [HttpGet("explore")]
    public async Task<IActionResult> Explore([FromQuery] double latitude, [FromQuery] double longitude, [FromQuery] int radiusKm = 50)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var result = await _communityService.ExploreCommunitiesAsync(userId.Value, latitude, longitude, radiusKm);
        return Ok(result);
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyCommunities()
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var result = await _communityService.GetMyCommunitiesAsync(userId.Value);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetCommunityDetails(int id)
    {
        var result = await _communityService.GetCommunityDetailsAsync(id);
        return Ok(result);
    }

    [HttpGet("{id}/members")]
    public async Task<IActionResult> GetCommunityMembers(int id)
    {
        var result = await _communityService.GetCommunityMembersAsync(id);
        return Ok(result);
    }

    [HttpPost("{id}/kick/{memberId}")]
    public async Task<IActionResult> KickMember(int id, Guid memberId)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        try
        {
            await _communityService.KickMemberAsync(userId.Value, id, memberId);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateCommunity([FromBody] CreateCommunityRequest request)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var result = await _communityService.CreateCommunityAsync(userId.Value, request);
        return CreatedAtAction(nameof(GetCommunityDetails), new { id = result.Id }, result);
    }

    [HttpPost("{id}/join")]
    public async Task<IActionResult> JoinCommunity(int id)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var result = await _communityService.JoinCommunityAsync(userId.Value, id);
        return Ok(result);
    }

    [HttpPost("{id}/leave")]
    public async Task<IActionResult> LeaveCommunity(int id)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        await _communityService.LeaveCommunityAsync(userId.Value, id);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCommunity(int id)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        await _communityService.DeleteCommunityAsync(userId.Value, id);
        return NoContent();
    }

    // --- LIBRARY ---

    [HttpGet("{id}/library")]
    public async Task<IActionResult> GetLibrary(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var result = await _libraryService.GetCommunityLibraryAsync(userId.Value, id, page, pageSize);
        return Ok(result);
    }

    [HttpPost("{id}/library/{bookId}/like")]
    public async Task<IActionResult> ToggleLike(int id, int bookId)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        await _libraryService.ToggleLikeAsync(userId.Value, id, bookId);
        return NoContent();
    }

    [HttpGet("{id}/library/suggest")]
    public async Task<IActionResult> GetSuggestedBooksForMeetup(int id)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var result = await _libraryService.GetSuggestedBooksForMeetupAsync(userId.Value, id);
        return Ok(result);
    }

    // --- MEETUPS ---

    [HttpGet("{id}/meetups")]
    public async Task<IActionResult> GetMeetups(int id)
    {
        var result = await _meetupService.GetMeetupsByCommunityAsync(id);
        return Ok(result);
    }

    [HttpPost("{id}/meetups")]
    public async Task<IActionResult> CreateMeetup(int id, [FromBody] CreateMeetupRequest request)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var result = await _meetupService.CreateMeetupAsync(userId.Value, id, request);
        return Ok(result); // Using OK instead of CreatedAtAction since no specific GET meetup endpoint is defined yet
    }

    [HttpPost("{id}/meetups/{meetupId}/attend")]
    public async Task<IActionResult> AttendMeetup(int id, int meetupId, [FromBody] AttendMeetupRequest request)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var result = await _meetupService.AttendMeetupAsync(userId.Value, meetupId, request);
        return Ok(result);
    }

    [HttpDelete("{id}/meetups/{meetupId}/attend")]
    public async Task<IActionResult> CancelAttendance(int id, int meetupId)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        await _meetupService.CancelAttendanceAsync(userId.Value, meetupId);
        return NoContent();
    }
}