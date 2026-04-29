using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Bookmerang.Api.Models.DTOs.Bookdrop;
using Bookmerang.Api.Services.Interfaces.Bookdrop;

namespace Bookmerang.Api.Controllers.Bookdrop;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "BookdropOnly")]
public class BookdropController(IBookdropService bookdropService, IBookDropExchangeService exchangeService) : ControllerBase
{
    private readonly IBookdropService _bookdropService = bookdropService;
    private readonly IBookDropExchangeService _exchangeService = exchangeService;

    private string? GetSupabaseId() =>
        User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");

    private async Task<int?> GetBookspotId()
    {
        var supabaseId = GetSupabaseId();
        if (supabaseId == null) return null;
        return await _bookdropService.GetBookspotIdBySupabaseId(supabaseId);
    }

    [HttpGet("perfil")]
    public async Task<IActionResult> GetPerfil()
    {
        var supabaseId = GetSupabaseId();
        if (supabaseId == null) return Unauthorized();

        var perfil = await _bookdropService.GetPerfil(supabaseId);
        if (perfil == null) return NotFound("Establecimiento no encontrado.");

        return Ok(perfil);
    }

    [HttpPatch("perfil")]
    public async Task<IActionResult> UpdatePerfil([FromBody] UpdateBookdropProfileRequest request)
    {
        var supabaseId = GetSupabaseId();
        if (supabaseId == null) return Unauthorized();

        var perfil = await _bookdropService.UpdatePerfil(supabaseId, request);
        if (perfil == null) return NotFound("Establecimiento no encontrado.");

        return Ok(perfil);
    }

    [HttpDelete("perfil")]
    public async Task<IActionResult> DeletePerfil()
    {
        var userId = User.FindFirstValue("user_id");
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var targetId))
            return Unauthorized();

        var (found, error) = await _bookdropService.DeleteBookdrop(targetId);

        if (!found) return NotFound("Establecimiento no encontrado.");
        if (error != null) return Conflict(new { error });

        return Ok(new { message = "Establecimiento eliminado correctamente." });
    }

    [HttpGet("exchanges")]
    public async Task<IActionResult> GetActiveExchanges()
    {
        var bookspotId = await GetBookspotId();
        if (bookspotId == null) return Unauthorized();

        try
        {
            var exchanges = await _exchangeService.GetActiveExchanges(bookspotId.Value);
            return Ok(exchanges);
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
    }

    [HttpPost("exchanges/{meetingId}/confirm-drop")]
    public async Task<IActionResult> ConfirmDrop(int meetingId, [FromBody] BookDropConfirmRequest request)
    {
        var bookspotId = await GetBookspotId();
        if (bookspotId == null) return Unauthorized();

        try
        {
            var result = await _exchangeService.ConfirmDrop(meetingId, request.Pin, bookspotId.Value);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("exchanges/{meetingId}/confirm-swap")]
    public async Task<IActionResult> ConfirmSwap(int meetingId, [FromBody] BookDropConfirmRequest request)
    {
        var bookspotId = await GetBookspotId();
        if (bookspotId == null) return Unauthorized();

        try
        {
            var result = await _exchangeService.ConfirmSwap(meetingId, request.Pin, bookspotId.Value);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("exchanges/{meetingId}/confirm-pickup")]
    public async Task<IActionResult> ConfirmPickup(int meetingId, [FromBody] BookDropConfirmRequest request)
    {
        var bookspotId = await GetBookspotId();
        if (bookspotId == null) return Unauthorized();

        try
        {
            var result = await _exchangeService.ConfirmPickup(meetingId, request.Pin, bookspotId.Value);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
