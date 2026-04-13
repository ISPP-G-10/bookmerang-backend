using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Services.Interfaces.ExchangeInterfaces;

namespace Bookmerang.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/exchanges")]
[Authorize(Policy = "AdminOnly")]
public class AdminExchangeController(IExchangeService exchangeService, IExchangeMeetingService meetingService) : ControllerBase
{
    private readonly IExchangeService _exchangeService = exchangeService;
    private readonly IExchangeMeetingService _meetingService = meetingService;

    /// GET /api/admin/exchanges
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var exchanges = await _exchangeService.GetAllExchanges();
        if (exchanges == null || exchanges.Count == 0)
            return NotFound("No se encontraron intercambios en el sistema.");

        return Ok(exchanges.Select(e => e.ToDto()));
    }

    /// DELETE /api/admin/exchanges/{exchangeId}
    [HttpDelete("{exchangeId}")]
    public async Task<IActionResult> Delete(int exchangeId)
    {
        var exchange = await _exchangeService.GetExchangeWithMatch(exchangeId);
        if (exchange == null) return NotFound($"Intercambio con id {exchangeId} no encontrado.");

        await _exchangeService.DeleteExchange(exchangeId);
        return NoContent();
    }

    /// GET /api/admin/exchanges/meetings
    [HttpGet("meetings")]
    public async Task<IActionResult> GetAllMeetings()
    {
        var meetings = await _meetingService.GetAllExchangeMeetings();
        if (meetings == null || meetings.Count == 0)
            return NotFound("No se encontraron ExchangeMeetings.");

        return Ok(meetings.Select(m => m.ToDto()));
    }

    /// GET /api/admin/exchanges/meetings/byUser/{proposerId}
    [HttpGet("meetings/byUser/{proposerId:guid}")]
    public async Task<IActionResult> GetMeetingsByUserId(Guid proposerId)
    {
        var meetings = await _meetingService.GetMeetingsByUserId(proposerId);
        if (meetings == null || meetings.Count == 0)
            return NotFound($"No se encontraron ExchangeMeetings para el usuario con id: {proposerId}");

        return Ok(meetings.Select(m => m.ToDto()));
    }
}
