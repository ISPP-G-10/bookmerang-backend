using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bookmerang.Api.Models.DTOs;
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

/// Gestiona las quedadas (meetings) de un intercambio.
/// Ver ExchangeController para el flujo general de intercambio.
public class ExchangeMeetingController(IExchangeMeetingService meetingService, AppDbContext db, IExchangeService exchangeService) : ControllerBase
{
    private readonly IExchangeMeetingService _meetingService = meetingService;
    private readonly IExchangeService _exchangeService = exchangeService;
    private readonly AppDbContext _db = db;

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

        var exchange = await _exchangeService.GetExchangeWithMatch(meeting.ExchangeId);

        if (!ExchangeAuthorizationHelper.IsAdminOrExchangeMember(User, userId.Value, exchange!))
            throw new ForbiddenException("No tienes permiso para acceder a este meeting.");

        return Ok(meeting.ToDto());
    }

    /// GET /api/exchangemeeting/byExchange/{exchangeId}
    [HttpGet("byExchange/{exchangeId:int}")]
    public async Task<IActionResult> GetMeetingByExchangeId(int exchangeId)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var meeting = await _meetingService.GetMeetingByExchangeId(exchangeId);
        if (meeting == null) return NotFound($"No existe meeting para exchange con id {exchangeId}.");

        var exchange = await _exchangeService.GetExchangeWithMatch(exchangeId);

        if (!ExchangeAuthorizationHelper.IsAdminOrExchangeMember(User, userId.Value, exchange!))
            throw new ForbiddenException("No tienes permiso para acceder a este meeting.");

        return Ok(meeting.ToDto());
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

    /// PATCH /api/exchangemeeting/{meetingId}/counter-propose
    /// Sirve para contraponer una oferta de punto de encuentro
    [HttpPatch("{meetingId}/counter-propose")]
    public async Task<IActionResult> CounterProposeMeeting(int meetingId, [FromBody] CounterProposeMeetingDto dto)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var meeting = await _meetingService.GetExchangeMeeting(meetingId);
        if (meeting == null) return NotFound($"ExchangeMeeting con id {meetingId} no encontrado.");

        var exchange = await _exchangeService.GetExchangeWithMatch(meeting.ExchangeId);

        if (!ExchangeAuthorizationHelper.IsAdminOrExchangeMember(User, userId.Value, exchange!))
            throw new ForbiddenException("No tienes permiso para modificar este meeting.");

        if (meeting.MeetingStatus != ExchangeMeetingStatus.PROPOSAL)
            return BadRequest("Solo se puede contra-proponer un meeting en estado de propuesta.");

        if (meeting.ProposerId == userId.Value)
            return BadRequest("No puedes contra-proponer tu propia propuesta.");

        try
        {
            var updatedMeeting = await _meetingService.CounterProposeMeeting(meeting, dto, userId.Value);
            return Ok(updatedMeeting.ToDto());
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{meetingId}/complete")]
    public async Task<IActionResult> CompleteExchange(int meetingId)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var meeting = await _meetingService.GetExchangeMeeting(meetingId);
        if (meeting == null) return NotFound($"ExchangeMeeting con id {meetingId} no encontrado.");

        var exchange = await _exchangeService.GetExchangeWithMatch(meeting.ExchangeId);

        if (!ExchangeAuthorizationHelper.IsAdminOrExchangeMember(User, userId.Value, exchange!))
            throw new ForbiddenException("No tienes permiso para completar este meeting.");

        if (meeting.MeetingStatus != ExchangeMeetingStatus.ACCEPTED)
            return BadRequest("Solo se puede completar un meeting que haya sido aceptado.");

        if (meeting.ExchangeMode == ExchangeMode.BOOKDROP)
            return BadRequest("Los intercambios BookDrop se completan desde el panel del establecimiento.");

        var isProposer = meeting.ProposerId == userId.Value;
        if ((isProposer && meeting.MarkAsCompletedByUser1) || (!isProposer && meeting.MarkAsCompletedByUser2))
            return BadRequest("Ya has marcado este intercambio como completado.");

        var updatedMeeting = await _meetingService.MarkAsCompleted(meeting, userId.Value);
        return Ok(updatedMeeting.ToDto());
    }

    [HttpPut("{meetingId}/accept")]
    public async Task<IActionResult> AcceptExchangeMeeting(int meetingId)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var meeting = await _meetingService.GetExchangeMeeting(meetingId);
        if (meeting == null) return NotFound($"ExchangeMeeting con id {meetingId} no encontrado.");

        var exchange = await _exchangeService.GetExchangeWithMatch(meeting.ExchangeId);

        if (!ExchangeAuthorizationHelper.IsAdminOrExchangeMember(User, userId.Value, exchange!))
            throw new ForbiddenException("No tienes permiso para aceptar este meeting.");

        if (meeting.MeetingStatus != ExchangeMeetingStatus.PROPOSAL)
            return BadRequest("Solo se puede aceptar un meeting en estado de propuesta.");

        if (meeting.ProposerId == userId.Value)
            return BadRequest("No puedes aceptar tu propia propuesta.");

        var acceptedMeeting = await _meetingService.AcceptMeeting(meeting);
        return Ok(acceptedMeeting.ToDto());
    }
}