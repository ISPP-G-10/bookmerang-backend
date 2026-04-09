using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Services.Interfaces.ExchangeInterfaces;

namespace Bookmerang.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/exchanges")]
[Authorize(Policy = "AdminOnly")]
public class AdminExchangeController(IExchangeService exchangeService) : ControllerBase
{
    private readonly IExchangeService _exchangeService = exchangeService;

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
        var exchange = await _exchangeService.GetExchangeById(exchangeId);
        if (exchange == null) return NotFound($"Intercambio con id {exchangeId} no encontrado.");

        await _exchangeService.DeleteExchange(exchangeId);
        return NoContent();
    }
}
