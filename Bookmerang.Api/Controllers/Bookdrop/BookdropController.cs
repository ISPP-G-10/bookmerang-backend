using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Bookmerang.Api.Models.DTOs.Bookdrop;
using Bookmerang.Api.Services.Interfaces.Bookdrop;

namespace Bookmerang.Api.Controllers.Bookdrop;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "BookdropOnly")]
public class BookdropController(IBookdropService bookdropService) : ControllerBase
{
    private readonly IBookdropService _bookdropService = bookdropService;

    private string? GetSupabaseId() =>
        User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");

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
        if (error != null) return BadRequest(error);

        return Ok(new { message = "Establecimiento eliminado correctamente." });
    }
}
