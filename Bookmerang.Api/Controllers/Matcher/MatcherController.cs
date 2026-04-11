using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Services.Interfaces.Auth;
using Bookmerang.Api.Services.Interfaces.Matcher;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
        const int maxSize = 100;
        const int maxPage = 1000;
        if (page < 0 || size <= 0 || size > maxSize)
            return BadRequest(new { message = $"size debe estar entre 1 y {maxSize}." });
        if (page > maxPage)
            return BadRequest(new { message = $"El número de página no puede superar {maxPage}." });

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
    [EnableRateLimiting("swipe")]
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

    /// <summary>
    /// POST /api/matcher/undo — Deshace el último swipe del usuario.
    /// Solo funciona si el swipe no generó un match.
    /// </summary>
    [HttpPost("undo")]
    [EnableRateLimiting("swipe")]
    public async Task<IActionResult> UndoLastSwipe()
    {
        var supabaseId = User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        if (supabaseId == null) return Unauthorized();

        var usuario = await _authService.GetPerfil(supabaseId);
        if (usuario == null) return NotFound(new { message = "Usuario no encontrado en el sistema." });

        var undone = await _matcherService.UndoLastSwipeAsync(usuario.Id);

        return undone
            ? Ok(new { message = "Ultimo movimiento deshecho correctamente." })
            : BadRequest(new { message = "No se pudo deshacer el ultimo movimiento." });
    }
}
