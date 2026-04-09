using Bookmerang.Api.Data;
using Bookmerang.Api.Exceptions;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Services.Interfaces.Chats;
using Microsoft.EntityFrameworkCore;

namespace Bookmerang.Api.Services.Implementation.Chats;

public class ChatService(AppDbContext db) : IChatService
{
    private readonly AppDbContext _db = db;

    public async Task<List<ChatDto>> GetUserChats(Guid userId)
    {
        var chatIds = await _db.ChatParticipants
            .Where(cp => cp.UserId == userId)
            .Select(cp => cp.ChatId)
            .ToListAsync();

        var chats = await _db.Chats
            .Include(c => c.Participants)
                .ThenInclude(p => p.User)
                    .ThenInclude(u => u.BaseUser)
            .Where(c => chatIds.Contains(c.Id))
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        var communityChatIds = chats.Where(c => c.Type == ChatType.COMMUNITY).Select(c => c.Id).ToList();
        var communityNames = new Dictionary<Guid, string>();

        if (communityChatIds.Any())
        {
            communityNames = await _db.CommunityChats
                .Include(cc => cc.Community)
                .Where(cc => communityChatIds.Contains(cc.ChatId))
                .ToDictionaryAsync(cc => cc.ChatId, cc => cc.Community.Name);
        }

        var result = new List<ChatDto>();

        foreach (var chat in chats)
        {
            var lastMessage = await _db.Messages
                .Include(m => m.Sender)
                    .ThenInclude(s => s.BaseUser)
                .Where(m => m.ChatId == chat.Id)
                .OrderByDescending(m => m.SentAt)
                .FirstOrDefaultAsync();

            string? name = null;
            if (chat.Type == ChatType.COMMUNITY && communityNames.TryGetValue(chat.Id, out var commName))
            {
                name = commName;
            }

            result.Add(chat.ToDto(lastMessage, name));
        }

        // Ordenar por último mensaje (los chats con mensajes más recientes primero)
        result = result
            .OrderByDescending(c => c.LastMessage?.SentAt ?? c.CreatedAt)
            .ToList();

        return result;
    }

    public async Task<ChatDto> GetChatById(Guid chatId, Guid userId)
    {
        var chatExists = await _db.Chats.AnyAsync(c => c.Id == chatId);
        if (!chatExists)
            throw new NotFoundException("Chat no encontrado.");

        if (!await IsParticipant(chatId, userId))
            throw new ForbiddenException("No tienes acceso a este chat.");

        var chat = await _db.Chats
            .Include(c => c.Participants)
                .ThenInclude(p => p.User)
                    .ThenInclude(u => u.BaseUser)
            .FirstOrDefaultAsync(c => c.Id == chatId);

        if (chat == null)
            throw new NotFoundException("Chat no encontrado.");

        var lastMessage = await _db.Messages
            .Include(m => m.Sender)
                .ThenInclude(s => s.BaseUser)
            .Where(m => m.ChatId == chatId)
            .OrderByDescending(m => m.SentAt)
            .FirstOrDefaultAsync();

        string? name = null;
        Dictionary<Guid, CommunityRole>? userRoles = null;

        if (chat.Type == ChatType.COMMUNITY)
        {
            var commChat = await _db.CommunityChats.Include(cc => cc.Community).FirstOrDefaultAsync(cc => cc.ChatId == chatId);
            if (commChat != null)
            {
                name = commChat.Community.Name;

                // Enriquecer participantes con roles de comunidad
                userRoles = await _db.CommunityMembers
                    .Where(cm => cm.CommunityId == commChat.CommunityId)
                    .ToDictionaryAsync(cm => cm.UserId, cm => cm.Role);
            }
        }

        if (userRoles != null)
        {
            return chat.ToDtoWithRoles(userRoles, lastMessage, name);
        }

        return chat.ToDto(lastMessage, name);
    }

    public async Task<List<MessageDto>> GetMessages(Guid chatId, Guid userId, int page, int pageSize)
    {
        if (!await IsParticipant(chatId, userId))
            return new List<MessageDto>();

        var messages = await _db.Messages
            .Include(m => m.Sender)
                .ThenInclude(s => s.BaseUser)
            .Where(m => m.ChatId == chatId)
            .OrderByDescending(m => m.SentAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return messages.Select(m => m.ToDto()).ToList();
    }

    public async Task<MessageDto?> SendMessage(Guid chatId, Guid senderId, string body)
    {
        if (!await IsParticipant(chatId, senderId))
            return null;

        var chatExists = await _db.Chats.AnyAsync(c => c.Id == chatId);
        if (!chatExists) return null;

        var message = new Message
        {
            ChatId = chatId,
            SenderId = senderId,
            Body = body,
            SentAt = DateTime.UtcNow
        };

        _db.Messages.Add(message);
        await _db.SaveChangesAsync();

        // Recargar con el sender incluido
        var saved = await _db.Messages
            .Include(m => m.Sender)
                .ThenInclude(s => s.BaseUser)
            .FirstAsync(m => m.Id == message.Id);

        return saved.ToDto();
    }

    public async Task<ChatDto?> CreateChat(ChatType type, List<Guid> participantIds)
    {
        if (type != ChatType.COMMUNITY && participantIds.Count < 2)
            return null;

        // Verificar que todos los usuarios existen
        var existingUsers = await _db.RegularUsers
            .Where(u => participantIds.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync();

        if (existingUsers.Count != participantIds.Count)
            return null;

        var chat = new Chat
        {
            Id = Guid.NewGuid(),
            Type = type,
            CreatedAt = DateTime.UtcNow
        };

        _db.Chats.Add(chat);
        await _db.SaveChangesAsync();

        var participants = participantIds.Select(uid => new ChatParticipant
        {
            ChatId = chat.Id,
            UserId = uid,
            JoinedAt = DateTime.UtcNow
        }).ToList();

        _db.ChatParticipants.AddRange(participants);
        await _db.SaveChangesAsync();

        // Recargar con datos completos
        return await GetChatByIdInternal(chat.Id);
    }

    public async Task<bool> IsParticipant(Guid chatId, Guid userId)
    {
        return await _db.ChatParticipants
            .AnyAsync(cp => cp.ChatId == chatId && cp.UserId == userId);
    }

    public async Task<bool> StartTyping(Guid chatId, Guid userId)
    {
        if (!await IsParticipant(chatId, userId))
            return false;

        // Verificar si ya existe un registro de typing
        var existingTyping = await _db.TypingIndicators
            .FirstOrDefaultAsync(t => t.ChatId == chatId && t.UserId == userId);

        if (existingTyping != null)
        {
            // Actualizar el timestamp
            existingTyping.StartedAt = DateTime.UtcNow;
            _db.TypingIndicators.Update(existingTyping);
        }
        else
        {
            // Crear nuevo registro
            var typing = new TypingIndicator
            {
                ChatId = chatId,
                UserId = userId,
                StartedAt = DateTime.UtcNow
            };
            _db.TypingIndicators.Add(typing);
        }

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> StopTyping(Guid chatId, Guid userId)
    {
        var typingIndicator = await _db.TypingIndicators
            .FirstOrDefaultAsync(t => t.ChatId == chatId && t.UserId == userId);

        if (typingIndicator == null)
            return false;

        _db.TypingIndicators.Remove(typingIndicator);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<TypingUserDto>> GetTypingUsers(Guid chatId, Guid userId)
    {
        if (!await IsParticipant(chatId, userId))
            return new List<TypingUserDto>();

        // Obtener usuarios que están escribiendo (excluyendo al usuario actual)
        // También filtrar indicadores antiguos (más de 5 segundos)
        var timeoutThreshold = DateTime.UtcNow.AddSeconds(-5);

        var typingUsers = await _db.TypingIndicators
            .Include(t => t.User)
                .ThenInclude(u => u.BaseUser)
            .Where(t => t.ChatId == chatId
                && t.UserId != userId
                && t.StartedAt > timeoutThreshold)
            .Select(t => t.ToDto())
            .ToListAsync();

        return typingUsers;
    }

    private async Task<ChatDto?> GetChatByIdInternal(Guid chatId)
    {
        var chat = await _db.Chats
            .Include(c => c.Participants)
                .ThenInclude(p => p.User)
                    .ThenInclude(u => u.BaseUser)
            .FirstOrDefaultAsync(c => c.Id == chatId);

        return chat?.ToDto();
    }
}
