using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Enums;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Bookmerang.Api.Services.Interfaces.ExchangeInterfaces;

namespace Bookmerang.Api.Controllers.Exchanges;

[ApiController]
[Route("api/[controller]")]
[Authorize]

public class ExchangeController : ControllerBase
{
    private readonly IExchangeService _service;
    private readonly AppDbContext _db;

    public ExchangeController (IExchangeService service, AppDbContext db)
    {
        _service = service;
        _db = db;
    }

    /// Obtiene el Guid del usuario autenticado
    private async Task<Guid?> GetCurrentUserId()
    {
        var supabaseId = User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        if (supabaseId == null) return null;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.SupabaseId == supabaseId);
        return user?.Id;
    }

    /// GET /api/exchange/{exchangeId}
    [HttpGet("{exchangeId}")]
    public async Task<IActionResult> GetExchange(int exchangeId)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var exchange = await _service.GetExchangeById(exchangeId);
        if (exchange == null) return NotFound($"Intercambio con id {exchangeId} no encontrado.");

        return Ok(exchange.ToDto());
    }

    /// GET /api/exchange/byChat/{chatId}
    [HttpGet("byChat/{chatId:int}")]
    public async Task<IActionResult> GetExchangeByChatId(int chatId)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var exchange = await _service.GetExchangeByChatId(chatId);
        if (exchange == null) return NotFound($"El intercambio correspondiente al chat con id {chatId} no se ha encontrado.");

        return Ok(exchange.ToDto());
    }

    /// GET /api/exchange
    [HttpGet]
    public async Task<IActionResult> GetAllExchanges()
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var exchanges = await _service.GetAllExchanges();
        if (exchanges == null || exchanges.Count == 0) return NotFound("No se encontraron intercambios en el sistema.");

        return Ok(exchanges.Select(e => e.ToDto()));
    }

    /// POST /api/exchange
    [HttpPost]
    public async Task<IActionResult> CreateExchange([FromBody] ExchangeDto dto)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        if (dto.ChatId == null || dto.MatchId == null)
            return BadRequest("Se requiere un chat y un match asociados para crear un intercambio.");

        var exchange = await _service.CreateExchange(dto.ChatId.Value, dto.MatchId.Value);
        return CreatedAtAction(nameof(GetExchange), new { exchangeId = exchange.ExchangeId }, exchange.ToDto());
    }

    [HttpPatch("{exchangeId}/accept")]
    public async Task<ActionResult> AcceptExchange(int exchangeId)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var newStatus = ExchangeStatus.ACCEPTED;
        try
        {
            var updated = await _service.UpdateExchangeStatus(exchangeId, newStatus);
            return Ok(updated.ToDto());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPatch("{exchangeId}/complete")]
    public async Task<ActionResult> CompleteExchange(int exchangeId)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var newStatus = ExchangeStatus.COMPLETED;
        try
        {
            var updated = await _service.UpdateExchangeStatus(exchangeId, newStatus);
            return Ok(updated.ToDto());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPatch("{exchangeId}/report")]
    public async Task<ActionResult> ReportExchange(int exchangeId)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var newStatus = ExchangeStatus.INCIDENT;
        try
        {
            var updated = await _service.UpdateExchangeStatus(exchangeId, newStatus);
            return Ok(updated.ToDto());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// DELETE /api/exchange/{exchangeId}
    [HttpDelete("{exchangeId}")]
    public async Task<IActionResult> DeleteExchange(int exchangeId)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        try
        {
            var result = await _service.DeleteExchange(exchangeId);
            if (!result) return BadRequest("No se pudo eliminar el intercambio.");
            return NoContent();
        }
        catch (Exception ex)
        {
            return NotFound(ex.Message);
        }
    }
    
}

