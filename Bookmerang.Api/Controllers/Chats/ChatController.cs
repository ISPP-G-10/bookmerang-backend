using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bookmerang.Api.Services.Interfaces.Chats;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace Bookmerang.Api.Controllers.Chats;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly AppDbContext _db;

    public ChatController(IChatService chatService, AppDbContext db)
    {
        _chatService = chatService;
        _db = db;
    }

    /// Obtiene el userId (Guid) del usuario autenticado a partir del supabaseId del JWT.
    private async Task<Guid?> GetCurrentUserId()
    {
        var supabaseId = User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        if (supabaseId == null) return null;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.SupabaseId == supabaseId);
        return user?.Id;
    }

    /// Lista todos los chats del usuario autenticado.
    /// GET /api/chat
    [HttpGet]
    public async Task<IActionResult> GetMyChats()
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var chats = await _chatService.GetUserChats(userId.Value);
        return Ok(chats);
    }

    /// Obtiene un chat específico por ID.
    /// GET /api/chat/{chatId}
    [HttpGet("{chatId:guid}")]
    public async Task<IActionResult> GetChat(Guid chatId)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        // NotFoundException y ForbiddenException son capturadas por el ExceptionMiddleware
        var chat = await _chatService.GetChatById(chatId, userId.Value);
        return Ok(chat);
    }

    /// Obtiene los mensajes de un chat con paginación.
    /// GET /api/chat/{chatId}/messages?page=1&pageSize=50
    [HttpGet("{chatId:guid}/messages")]
    public async Task<IActionResult> GetMessages(Guid chatId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        if (!await _chatService.IsParticipant(chatId, userId.Value))
            return Forbid();

        var messages = await _chatService.GetMessages(chatId, userId.Value, page, pageSize);
        return Ok(messages);
    }

    /// Envía un mensaje a un chat.
    /// POST /api/chat/{chatId}/messages
    [HttpPost("{chatId:guid}/messages")]
    public async Task<IActionResult> SendMessage(Guid chatId, [FromBody] SendMessageRequest request)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Body))
            return BadRequest("El mensaje no puede estar vacío.");

        var message = await _chatService.SendMessage(chatId, userId.Value, request.Body);
        if (message == null) return NotFound("Chat no encontrado o no tienes acceso.");

        return CreatedAtAction(nameof(GetMessages), new { chatId }, message);
    }

    /// Crea un nuevo chat.
    /// POST /api/chat
    [HttpPost]
    public async Task<IActionResult> CreateChat([FromBody] CreateChatRequest request)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        // Asegurar que el usuario actual está incluido en los participantes
        var participantIds = request.ParticipantIds.Distinct().ToList();
        if (!participantIds.Contains(userId.Value))
            participantIds.Add(userId.Value);

        if (participantIds.Count < 2)
            return BadRequest("Se necesitan al menos 2 participantes.");

        var chat = await _chatService.CreateChat(request.Type, participantIds);
        if (chat == null) return BadRequest("No se pudo crear el chat. Verifica que los usuarios existan.");

        return CreatedAtAction(nameof(GetChat), new { chatId = chat.Id }, chat);
    }

    /// Marca que el usuario está escribiendo en un chat.
    /// POST /api/chat/{chatId}/typing
    [HttpPost("{chatId:guid}/typing")]
    public async Task<IActionResult> StartTyping(Guid chatId)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var success = await _chatService.StartTyping(chatId, userId.Value);
        if (!success) return Forbid();

        return NoContent();
    }

    /// Marca que el usuario dejó de escribir en un chat.
    /// DELETE /api/chat/{chatId}/typing
    [HttpDelete("{chatId:guid}/typing")]
    public async Task<IActionResult> StopTyping(Guid chatId)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        await _chatService.StopTyping(chatId, userId.Value);
        return NoContent();
    }

    /// Obtiene los usuarios que están escribiendo en un chat.
    /// GET /api/chat/{chatId}/typing
    [HttpGet("{chatId:guid}/typing")]
    public async Task<IActionResult> GetTypingUsers(Guid chatId)
    {
        var userId = await GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var typingUsers = await _chatService.GetTypingUsers(chatId, userId.Value);
        return Ok(typingUsers);
    }
}
