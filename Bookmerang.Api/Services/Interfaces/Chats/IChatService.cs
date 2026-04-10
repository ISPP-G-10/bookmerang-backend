using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Models.DTOs;

namespace Bookmerang.Api.Services.Interfaces.Chats;

public interface IChatService
{
    /// Obtiene todos los chats en los que participa un usuario.
    Task<List<ChatDto>> GetUserChats(Guid userId);

    /// Obtiene un chat por su ID (solo si el usuario es participante).
    /// Lanza NotFoundException si el chat no existe.
    /// Lanza ForbiddenException si el usuario no es participante.
    Task<ChatDto> GetChatById(Guid chatId, Guid userId);

    /// Obtiene los mensajes de un chat con paginación.
    Task<List<MessageDto>> GetMessages(Guid chatId, Guid userId, int page, int pageSize);

    /// Envía un mensaje a un chat.
    Task<MessageDto?> SendMessage(Guid chatId, Guid senderId, string body);

    /// Crea un nuevo chat entre participantes.
    Task<ChatDto?> CreateChat(ChatType type, List<Guid> participantIds);

    /// Verifica si un usuario es participante de un chat.
    Task<bool> IsParticipant(Guid chatId, Guid userId);

    /// Marca que un usuario está escribiendo en un chat.
    Task<bool> StartTyping(Guid chatId, Guid userId);

    /// Marca que un usuario dejó de escribir en un chat.
    Task<bool> StopTyping(Guid chatId, Guid userId);

    /// Obtiene la lista de usuarios que están escribiendo en un chat.
    Task<List<TypingUserDto>> GetTypingUsers(Guid chatId, Guid userId);

    /// Elimina un chat junto con sus mensajes y participantes.
    Task<bool> DeleteChat(int chatId);
}
