using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Data;
using Bookmerang.Api.Exceptions;
using Bookmerang.Api.Helpers;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Bookmerang.Api.Services.Interfaces.ExchangeInterfaces;

namespace Bookmerang.Api.Controllers.Exchanges;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "UserOnly")]

public class ExchangeMeetingController : ControllerBase
{
    private readonly IExchangeMeetingService _meetingService;
    private readonly IExchangeService _exchangeService;
    private readonly AppDbContext _db;

    public ExchangeMeetingController(IExchangeMeetingService meetingService, AppDbContext db, IExchangeService exchangeService)
    {
        _meetingService = meetingService;
        _exchangeService = exchangeService;
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

    /// GET /api/exchangemeeting/byExchange/{exchangeId}
    [HttpGet("byExchange/{exchangeId:int}")]
    public async Task<IActionResult> GetMeetingByExchangeId(int exchangeId)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var exchange = await _exchangeService.GetExchangeWithMatch(exchangeId);
        if (exchange == null) return NotFound($"Exchange con id {exchangeId} no encontrado.");

        if (!IsUserInExchange(userId.Value, exchange.Match)) return Forbid();

        var meeting = await _meetingService.GetMeetingByExchangeId(exchangeId);
        if (meeting == null) return NotFound($"No existe meeting para exchange con id {exchangeId}.");

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
    public async Task<IActionResult> CreateExchangeMeeting([FromBody] CreateExchangeMeetingDto dto)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var exchange = await _exchangeService.GetExchangeWithMatch(dto.ExchangeId);
        if (exchange == null) return NotFound($"Exchange con id {dto.ExchangeId} no encontrado.");

        if (!ExchangeAuthorizationHelper.IsAdminOrExchangeMember(User, userId.Value, exchange))
            throw new ForbiddenException("No tienes permiso para crear un meeting en este intercambio.");

        if (exchange.Status != ExchangeStatus.ACCEPTED)
            return BadRequest("Solo se puede crear un meeting cuando el intercambio ha sido aceptado.");

        var existingMeeting = await _meetingService.GetMeetingByExchangeId(dto.ExchangeId);
        if (existingMeeting != null)
            return Conflict("Ya existe un meeting para este intercambio.");

        try
        {
            var meeting = await _meetingService.CreateExchangeMeeting(dto, userId.Value);

            return CreatedAtAction(
                nameof(GetExchangeMeeting),
                new { meetingId = meeting.ExchangeMeetingId },
                meeting.ToDto());
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// PUT /api/exchangemeeting/{meetingId}
    [HttpPut("{meetingId}")]
    public async Task<IActionResult> UpdateExchangeMeeting(int meetingId, [FromBody] UpdateExchangeMeetingDto dto)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var oldMeeting = await _meetingService.GetExchangeMeetingWithRelations(meetingId);
        if (oldMeeting == null) return NotFound($"ExchangeMeeting con id {meetingId} no encontrado.");

        if (!IsUserInExchange(userId.Value, oldMeeting.Exchange.Match)) return Forbid();

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

    [HttpPut("{meetingId}/complete")]
    public async Task<IActionResult> CompleteExchange(int meetingId)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var oldMeeting = await _meetingService.GetExchangeMeeting(meetingId);
        if (oldMeeting == null) return NotFound($"ExchangeMeeting con id {meetingId} no encontrado.");

        // los intercambios BookDrop se completan desde el panel del establecimiento
        if (oldMeeting.ExchangeMode == ExchangeMode.BOOKDROP)
            return BadRequest("Los intercambios BookDrop se completan desde el panel del establecimiento.");

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

    private static bool IsUserInExchange(Guid userLoggedId, Match match)
    {
        return userLoggedId == match.User1Id || userLoggedId == match.User2Id;
    }

    [HttpPut("{meetingId}/accept")]
    public async Task<IActionResult> AcceptExchangeMeeting(int meetingId) 
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        UpdateExchangeMeetingDto dto;
        dto = new(null, null, null, null, ExchangeMeetingStatus.ACCEPTED, null, null);

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


    /// DELETE /api/exchangemeeting/{meetingId}
    /// Llamar a este endpoint cuando se rechace una quedada (se borran directamente)
    [HttpDelete("{meetingId}")]
    public async Task<IActionResult> DeleteExchangeMeeting(int meetingId)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var oldMeeting = await _meetingService.GetExchangeMeetingWithRelations(meetingId);

        if (oldMeeting == null) return NotFound($"ExchangeMeeting con id {meetingId} no encontrado.");

        if (!IsUserInExchange(userId.Value, oldMeeting.Exchange.Match)) return Forbid();

        if (oldMeeting.ExchangeMode == ExchangeMode.BOOKDROP
            && oldMeeting.BookDropStatus != null
            && oldMeeting.BookDropStatus != BookdropExchangeStatus.COMPLETED)
            return BadRequest("No se puede eliminar un intercambio BookDrop en curso.");

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