using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Services.Interfaces.Auth;
using Bookmerang.Api.Services.Interfaces.Matcher;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Bookmerang.Api.Controllers.Matcher;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MatcherController(IMatcherService matcherService, IAuthService authService) : ControllerBase
{
    private readonly IMatcherService _matcherService = matcherService;
    private readonly IAuthService _authService = authService;

    /// <summary>
    /// GET /api/matcher/feed — Devuelve el feed paginado de libros candidatos.
    /// Intercala libros de prioridad (P1) y descubrimiento (P2) según el ratio configurado.
    /// </summary>
    [HttpGet("feed")]
    public async Task<IActionResult> GetFeed(
        [FromQuery] int page = 0,
        [FromQuery] int size = 20)
    {
        var supabaseId = User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        if (supabaseId == null) return Unauthorized();

        var usuario = await _authService.GetPerfil(supabaseId);
        if (usuario == null) return NotFound(new { message = "Usuario no encontrado en el sistema." });

        // Validar parámetros de paginación
        if (page < 0 || size <= 0)
            return BadRequest(new { message = "Los parámetros page y size deben ser >= 0 y > 0 respectivamente." });

        try
        {
            var feed = await _matcherService.GetFeedAsync(usuario.Id, page, size);
            return Ok(feed);
        }
        // GetUserPreferencesAsync: preferencias no configuradas
        // ValidatePriorityToDiscoveryRatio: ratio inválido en appsettings
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/matcher/swipe — Registra un swipe sobre un libro.
    /// Si es RIGHT y hay match bilateral, crea Match + Chat + Exchange.
    /// </summary>
    [HttpPost("swipe")]
    public async Task<IActionResult> Swipe(
        [FromBody] SwipeRequestDto request)
    {
        var supabaseId = User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        if (supabaseId == null) return Unauthorized();

        var usuario = await _authService.GetPerfil(supabaseId);
        if (usuario == null) return NotFound(new { message = "Usuario no encontrado en el sistema." });

        try
        {
            var result = await _matcherService.ProcessSwipeAsync(
                usuario.Id, request.BookId, request.Direction);
            return Ok(result);
        }
        // ValidateBookForSwipeAsync: libro no encontrado
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        // ValidateBookForSwipeAsync: auto-swipe (usuario swipea su propio libro)
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        // SaveChangesAsync: unique index (SwiperId, BookId) violado → swipe duplicado
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Ya has realizado un swipe sobre este libro." });
        }
    }
}
