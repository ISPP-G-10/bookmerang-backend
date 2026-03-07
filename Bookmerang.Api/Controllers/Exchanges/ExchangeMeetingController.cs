using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Bookmerang.Api.Services.Interfaces.ExchangeInterfaces;
using NetTopologySuite.Geometries;

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
        
        return Ok(meeting.ToDto());
    }

    /// GET /api/exchangemeeting/byUser/{proposerId}
    [HttpGet("byUser/{proposerId:guid}")]
    public async Task<IActionResult> GetMeetingsByUserId(Guid proposerId)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var meetings = await _meetingService.GetMeetingsByUserId(proposerId);
        if (meetings == null || meetings.Count == 0)
            return NotFound($"No se encontraron ExchangeMeetings para el usuario con id: {proposerId}");

        // Convert entities to DTOs to prevent JSON serialization errors from NetTopologySuite
        var meetingsConverted = meetings.Select(m => m.ToDto()).ToList();
        return Ok(meetingsConverted);
    }

    /// GET /api/exchangemeeting/all
    [HttpGet("all")]
    public async Task<IActionResult> GetAllExchangeMeetings()
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var meetings = await _meetingService.GetAllExchangeMeetings();
        if (meetings == null || meetings.Count == 0)
            return NotFound($"No se encontraron ExchangeMeetings");

        var meetingsConverted = meetings.Select(m => m.ToDto()).ToList();
        return Ok(meetingsConverted);
    }

    /// POST /api/exchangemeeting
    [HttpPost]
    public async Task<IActionResult> CreateExchangeMeeting([FromBody] ExchangeMeetingDto dto)
    {
        var userId = await GetCurrentUserId(); // Obtener el userId del usuario autenticado, que será el proposer del meeting
        if (userId == null) return Unauthorized();

        // require all necessary fields
        if (dto.ExchangeId == 0 || dto.ExchangeId == null || !dto.ExchangeMode.HasValue || (dto.ExchangeMode == ExchangeMode.CUSTOM && dto.CustomLocation == null))
            return BadRequest("Faltan propiedades para crear un ExchangeMeeting.");

        // if custom location was not supplied due to serialization issues, just default to the origin
        Point location;
        if (dto.CustomLocation != null && dto.CustomLocation.Length >= 2)
            location = new Point(dto.CustomLocation[0], dto.CustomLocation[1]) { SRID = 4326 };
        else
            location = new Point(0, 0) { SRID = 4326 };
        var meeting = await _meetingService.CreateExchangeMeeting(dto.ExchangeId.Value, dto.ExchangeMode.Value, userId.Value, dto.BookspotId, dto.ScheduledAt, location);

        // Se asigna el id para que se pueda buscar el exchange meeting para devolverlo
        return CreatedAtAction(
            nameof(GetExchangeMeeting),
            new { meetingId = meeting.ExchangeMeetingId },
            meeting.ToDto());
    }

    /// PUT /api/exchangemeeting/{meetingId}
    [HttpPut("{meetingId}")]
    public async Task<IActionResult> UpdateExchangeMeeting(int meetingId, [FromBody] UpdateExchangeMeetingDto dto)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var oldMeeting = await _meetingService.GetExchangeMeeting(meetingId);
        if (oldMeeting == null) return NotFound($"ExchangeMeeting con id {meetingId} no encontrado.");

        if(!IsUserAuthorizedToMarkAsCompleted(oldMeeting.ProposerId, userId.Value, dto.MarkAsCompletedByUser1 == true, dto.MarkAsCompletedByUser2 == true))
        {
            return Forbid();
        }

        //Desde este update el usuario no debería poder cambiar el estado del meeting
        if (dto.MeetingStatus.HasValue && dto.MeetingStatus != oldMeeting.MeetingStatus)
        {
            return Forbid();
        }
        
        try
        {
            var updatedMeeting = await _meetingService.UpdateExchangeMeeting(meetingId, dto);
            return Ok(updatedMeeting.ToDto());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private static bool IsUserAuthorizedToMarkAsCompleted(Guid proposerId, Guid userId, bool marked1, bool marked2)
    {
        // Solo el proposer del meeting puede marcarlo como aceptado
        if (marked1 && userId != proposerId) return false;
        if (marked2 && userId == proposerId) return false;
        
        return true;
    }

    [HttpPut("{meetingId}/accept")]
    public async Task<IActionResult> AcceptExchangeMeeting(int meetingId)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var oldMeeting = await _meetingService.GetExchangeMeeting(meetingId);
        if (oldMeeting == null) return NotFound($"ExchangeMeeting con id {meetingId} no encontrado.");
        
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
            return Ok(updatedMeeting.ToDto());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{meetingId}/refuse")]
    public async Task<IActionResult> RefuseMeting(int meetingId)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var newStatus = ExchangeMeetingStatus.REFUSED;
        UpdateExchangeMeetingDto dto = new(null, null, null, null, newStatus, null, null);
        try
        {
            var updated = await _meetingService.UpdateExchangeMeeting(meetingId, dto);
            return Ok(updated.ToDto());
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