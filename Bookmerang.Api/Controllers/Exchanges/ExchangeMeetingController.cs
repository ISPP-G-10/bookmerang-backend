using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Bookmerang.Api.Services.Interfaces.ExchangeInterfaces;

namespace Bookmerang.Api.Controllers.Exchanges;

[ApiController]
[Route("api/[controller]")]
[Authorize]

public class ExchangeMeetingController : ControllerBase
{
    private readonly IExchangeMeetingService _meetingService;
    private readonly AppDbContext _db;

    public ExchangeMeetingController(IExchangeMeetingService meetingService, AppDbContext db)
    {
        _meetingService = meetingService;
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

    /// GET /api/exchangemeeting/{meetingId}
    [HttpGet("{meetingId}")]
    public async Task<IActionResult> GetExchangeMeeting(int meetingId)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();
        
        var meeting = await _meetingService.GetExchangeMeeting(meetingId);
        if (meeting == null) return NotFound($"ExchangeMeeting con id {meetingId} no encontrado.");
        
        return Ok(meeting);
    }

    /// GET /api/exchangemeeting/byUser/{proposerId}
    [HttpGet("byUser/{proposerId:guid}")]
    public async Task<IActionResult> GetMeetingsByUserId(Guid proposerId)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var meetings = await _meetingService.GetMeetingsByUserId(proposerId);
        if (meetings == null || meetings.Count == 0) return NotFound($"No se encontraron ExchangeMeetings para el usuario con id: {proposerId}");
        return Ok(meetings);
    }

    /// POST /api/exchangemeeting
    [HttpPost]
    public async Task<IActionResult> CreateExchangeMeeting([FromBody] ExchangeMeetingDto dto)
    {
        var userId = await GetCurrentUserId(); // Obtener el userId del usuario autenticado, que será el proposer del meeting
        if (userId == null) return Unauthorized();

        if (dto.ExchangeId == 0 || dto.ExchangeId == null || !dto.ExchangeMode.HasValue || dto.ProposerId == Guid.Empty || dto.ProposerId == null || dto.CustomLocation == null)
            return BadRequest("Faltan propiedades para crear un ExchangeMeeting.");

        var meeting = await _meetingService.CreateExchangeMeeting(dto.ExchangeId.Value, dto.ExchangeMode.Value, userId.Value, dto.BookspotId, dto.ScheduledAt, dto.CustomLocation);

        return CreatedAtAction(nameof(GetExchangeMeeting), new { supabaseId = meeting.SupabaseId }, meeting);
    }

    /// PUT /api/exchangemeeting/{meetingId}
    [HttpPut("{meetingId}")]
    public async Task<IActionResult> UpdateExchangeMeeting(int meetingId, [FromBody] UpdateExchangeMeetingDto dto)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var oldMeeting = (await _meetingService.GetExchangeMeeting(meetingId))!; // Significa que el meeting existe, porque si no se encuentra se lanza una excepción ens el servicio y se devuelve un BadRequest con el mensaje de error.

        if(!IsUserAuthorizedToMarkAsCompleted(oldMeeting.ProposerId, userId.Value, dto.MarkAsCompletedByUser1.HasValue, dto.MarkAsCompletedByUser2.HasValue))
        {
            return Forbid();
        }
        
        try
        {
            var updatedMeeting = await _meetingService.UpdateExchangeMeeting(meetingId, dto);
            return Ok(updatedMeeting);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private bool IsUserAuthorizedToMarkAsCompleted(Guid proposerId, Guid userId, bool marked1, bool marked2)
    {
        // Solo el proposer del meeting puede marcarlo como aceptado
        if (userId == proposerId && marked2)
            return false;
        else if (userId != proposerId && marked1)
            return false;
        
        return true;
    }

    [HttpPut("{meetingId}/accept")]
    public async Task<IActionResult> AcceptExchangeMeeting(int meetingId)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var oldMeeting = (await _meetingService.GetExchangeMeeting(meetingId))!; // Significa que el meeting existe, porque si no se encuentra se lanza una excepción en el servicio y se devuelve un BadRequest con el mensaje de error.
        UpdateExchangeMeetingDto dto;
        
        if(oldMeeting.ProposerId == userId)
        {
            // Actualizar markascompletedby1 a true
            dto = new(null, null, null, null, null, true, null);
        }
        else
        {
            // Actualizar markascompletedby2 a true
            dto = new(null, null, null, null, null, null, true);
        }
        
        try
        {
            var updatedMeeting = await _meetingService.UpdateExchangeMeeting(meetingId, dto);
            return Ok(updatedMeeting);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// DELETE /api/exchangemeeting/{meetingId}
    [HttpDelete("{meetingId}")]
    public async Task<IActionResult> DeleteExchangeMeeting(int meetingId)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        try
        {
            var result = await _meetingService.DeleteExchangeMeeting(meetingId);
            if (!result) return BadRequest($"No se pudo eliminar el ExchangeMeeting con id {meetingId}.");
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
}