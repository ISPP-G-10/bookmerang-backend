using Bookmerang.Api.Models;
using Bookmerang.Api.Models.DTOs;

namespace Bookmerang.Api.Services.Interfaces.Chats;

public interface IChatService
{
    /// Obtiene todos los chats en los que participa un usuario.
    Task<List<ChatDto>> GetUserChats(Guid userId);

    /// Obtiene un chat por su ID (solo si el usuario es participante).

    Task<ChatDto?> GetChatById(int chatId, Guid userId);

    /// Obtiene los mensajes de un chat con paginación.
    Task<List<MessageDto>> GetMessages(int chatId, Guid userId, int page, int pageSize);

    /// Envía un mensaje a un chat.
    Task<MessageDto?> SendMessage(int chatId, Guid senderId, string body);

    /// Crea un nuevo chat entre participantes.
    Task<ChatDto?> CreateChat(ChatType type, List<Guid> participantIds);

    /// Verifica si un usuario es participante de un chat.
    Task<bool> IsParticipant(int chatId, Guid userId);
}
