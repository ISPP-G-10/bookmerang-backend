using Bookmerang.Api.Data;
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
            .Where(c => chatIds.Contains(c.Id))
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        var result = new List<ChatDto>();

        foreach (var chat in chats)
        {
            var lastMessage = await _db.Messages
                .Include(m => m.Sender)
                .Where(m => m.ChatId == chat.Id)
                .OrderByDescending(m => m.SentAt)
                .FirstOrDefaultAsync();

            result.Add(chat.ToDto(lastMessage));
        }

        // Ordenar por último mensaje (los chats con mensajes más recientes primero)
        result = result
            .OrderByDescending(c => c.LastMessage?.SentAt ?? c.CreatedAt)
            .ToList();

        return result;
    }

    public async Task<ChatDto?> GetChatById(int chatId, Guid userId)
    {
        if (!await IsParticipant(chatId, userId))
            return null;

        var chat = await _db.Chats
            .Include(c => c.Participants)
                .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(c => c.Id == chatId);

        if (chat == null) return null;

        var lastMessage = await _db.Messages
            .Include(m => m.Sender)
            .Where(m => m.ChatId == chatId)
            .OrderByDescending(m => m.SentAt)
            .FirstOrDefaultAsync();

        return chat.ToDto(lastMessage);
    }

    public async Task<List<MessageDto>> GetMessages(int chatId, Guid userId, int page, int pageSize)
    {
        if (!await IsParticipant(chatId, userId))
            return new List<MessageDto>();

        var messages = await _db.Messages
            .Include(m => m.Sender)
            .Where(m => m.ChatId == chatId)
            .OrderByDescending(m => m.SentAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return messages.Select(m => m.ToDto()).ToList();
    }

    public async Task<MessageDto?> SendMessage(int chatId, Guid senderId, string body)
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
            .FirstAsync(m => m.Id == message.Id);

        return saved.ToDto();
    }

    public async Task<ChatDto?> CreateChat(ChatType type, List<Guid> participantIds)
    {
        if (participantIds.Count < 2)
            return null;

        // Verificar que todos los usuarios existen
        var existingUsers = await _db.Users
            .Where(u => participantIds.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync();

        if (existingUsers.Count != participantIds.Count)
            return null;

        var chat = new Chat
        {
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

    public async Task<bool> IsParticipant(int chatId, Guid userId)
    {
        return await _db.ChatParticipants
            .AnyAsync(cp => cp.ChatId == chatId && cp.UserId == userId);
    }

    private async Task<ChatDto?> GetChatByIdInternal(int chatId)
    {
        var chat = await _db.Chats
            .Include(c => c.Participants)
                .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(c => c.Id == chatId);

        return chat?.ToDto();
    }
}
