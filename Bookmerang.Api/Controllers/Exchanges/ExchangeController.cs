using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Data;
using Bookmerang.Api.Exceptions;
using Bookmerang.Api.Models.Enums;
using System.Security.Claims;
using Bookmerang.Api.Helpers;
using Microsoft.EntityFrameworkCore;
using Bookmerang.Api.Services.Interfaces.ExchangeInterfaces;

namespace Bookmerang.Api.Controllers.Exchanges;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "UserOnly")]

public class ExchangeController(IExchangeService service, AppDbContext db) : ControllerBase
{
    private readonly IExchangeService _service = service;
    private readonly AppDbContext _db = db;

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

        var exchange = await _service.GetExchangeWithMatch(exchangeId);
        if (exchange == null) return NotFound($"Intercambio con id {exchangeId} no encontrado.");

        if (!ExchangeAuthorizationHelper.IsAdminOrExchangeMember(User, userId.Value, exchange))
            throw new ForbiddenException("No tienes permiso para acceder a este intercambio.");

        return Ok(exchange.ToDto());
    }

    /// GET /api/exchange/byChat/{chatId}/withMatch
    [HttpGet("byChat/{chatId:int}/withMatch")]
    public async Task<IActionResult> GetExchangeByChatIdWithMatchDetails(int chatId)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var exchange = await _service.GetExchangeByChatIdWithMatch(chatId);
        if (exchange == null) return NotFound($"Intercambio con id {chatId} no encontrado.");

        if (!ExchangeAuthorizationHelper.IsAdminOrExchangeMember(User, userId.Value, exchange))
            throw new ForbiddenException("No tienes permiso para acceder a este intercambio.");

        return Ok(exchange.ToWithMatchDto());
    }

    [HttpPatch("{exchangeId}/accept")]
    public async Task<ActionResult> AcceptExchange(int exchangeId)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var exchange = await _service.GetExchangeWithMatch(exchangeId);
        if (exchange == null) return NotFound($"Intercambio con id {exchangeId} no encontrado.");

        if (!ExchangeAuthorizationHelper.IsAdminOrExchangeMember(User, userId.Value, exchange))
            throw new ForbiddenException("No tienes permiso para aceptar este intercambio.");

        var updated = await _service.AcceptExchange(exchangeId, userId.Value);
        return Ok(updated.ToDto());
    }

    [HttpPatch("{exchangeId}/reject")]
    public async Task<ActionResult> RejectExchange(int exchangeId)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var exchange = await _service.GetExchangeWithMatch(exchangeId);
        if (exchange == null) return NotFound($"Intercambio con id {exchangeId} no encontrado.");

        if (!ExchangeAuthorizationHelper.IsAdminOrExchangeMember(User, userId.Value, exchange))
            throw new ForbiddenException("No tienes permiso para rechazar este intercambio.");

        if (exchange.Status is not (ExchangeStatus.NEGOTIATING
                                 or ExchangeStatus.ACCEPTED_BY_1
                                 or ExchangeStatus.ACCEPTED_BY_2))
        {
            return BadRequest("Solo se puede rechazar un intercambio que aún esté en fase de negociación.");
        }

        var updated = await _service.UpdateExchangeStatus(exchange, ExchangeStatus.REJECTED);
        return Ok(updated.ToDto());
    }

    [HttpPatch("{exchangeId}/report")]
    public async Task<ActionResult> ReportExchange(int exchangeId)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var exchange = await _service.GetExchangeWithMatch(exchangeId);
        if (exchange == null) return NotFound($"Intercambio con id {exchangeId} no encontrado.");

        if (!ExchangeAuthorizationHelper.IsAdminOrExchangeMember(User, userId.Value, exchange))
            throw new ForbiddenException("No tienes permiso para reportar este intercambio.");

        if (exchange.Status != ExchangeStatus.ACCEPTED)
            return BadRequest("Solo se puede reportar un intercambio que esté en curso.");

        var updated = await _service.UpdateExchangeStatus(exchange, ExchangeStatus.INCIDENT);
        return Ok(updated.ToDto());
    }

}

