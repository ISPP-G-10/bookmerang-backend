using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Services.Interfaces.Matcher;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bookmerang.Api.Controllers.Matcher;

[ApiController]
[Route("api/[controller]")]
public class MatcherController(IMatcherService matcherService) : ControllerBase
{
    private readonly IMatcherService _matcherService = matcherService;

    /// <summary>
    /// GET /api/matcher/feed — Devuelve el feed paginado de libros candidatos.
    /// Intercala libros de prioridad (P1) y descubrimiento (P2) según el ratio configurado.
    /// </summary>
    [HttpGet("feed")]
    public async Task<IActionResult> GetFeed(
        [FromQuery] int userId,
        [FromQuery] int page = 0,
        [FromQuery] int size = 20)
    {
        // Validar parámetros de paginación
        if (page < 0 || size <= 0)
            return BadRequest(new { message = "Los parámetros page y size deben ser >= 0 y > 0 respectivamente." });

        try
        {
            var feed = await _matcherService.GetFeedAsync(userId, page, size);
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
        [FromQuery] int userId,
        [FromBody] SwipeRequestDto request)
    {
        try
        {
            var result = await _matcherService.ProcessSwipeAsync(
                userId, request.BookId, request.Direction);
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
