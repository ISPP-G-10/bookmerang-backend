using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Models.DTOs;

// ===== Response DTOs =====

public record ChatDto(
    Guid Id,
    string Type,
    DateTime CreatedAt,
    List<ChatParticipantDto> Participants,
    MessageDto? LastMessage,
    string? Name = null
);

public record ChatParticipantDto(
    Guid UserId,
    string Username,
    string ProfilePhoto,
    DateTime JoinedAt,
    CommunityRole? Role = null,
    string? ActiveFrameId = null,
    string? ActiveColorId = null
);

public record MessageDto(
    int Id,
    Guid ChatId,
    Guid SenderId,
    string SenderUsername,
    string Body,
    DateTime SentAt,
    string? SenderActiveFrameId = null,
    string? SenderActiveColorId = null
);

// ===== Request DTOs =====

public record SendMessageRequest(
    string Body
);

public record CreateChatRequest(
    ChatType Type,
    List<Guid> ParticipantIds
);

// ===== Extension Methods =====

public static class ChatExtensions
{
    public static ChatDto ToDto(this Chat chat, Message? lastMessage = null, string? name = null) => new(
        chat.Id,
        chat.Type.ToString(),
        chat.CreatedAt,
        chat.Participants.Select(p => p.ToDto()).ToList(),
        lastMessage?.ToDto(),
        name
    );

    public static ChatDto ToDto(this Chat chat, Dictionary<Guid, (string? frameId, string? colorId)> userPersonalization, Message? lastMessage = null, string? name = null) => new(
        chat.Id,
        chat.Type.ToString(),
        chat.CreatedAt,
        chat.Participants.Select(p => {
            userPersonalization.TryGetValue(p.UserId, out var personalization);
            return p.ToDto(personalization.frameId, personalization.colorId);
        }).ToList(),
        lastMessage?.ToDto(),
        name
    );

    public static ChatDto ToDtoWithRoles(this Chat chat, Dictionary<Guid, CommunityRole> userRoles, Message? lastMessage = null, string? name = null) => new(
        chat.Id,
        chat.Type.ToString(),
        chat.CreatedAt,
        chat.Participants.Select(p => p.ToDto(userRoles.TryGetValue(p.UserId, out var role) ? role : null)).ToList(),
        lastMessage?.ToDto(),
        name
    );

    public static ChatDto ToDtoWithRoles(this Chat chat, Dictionary<Guid, CommunityRole> userRoles, Dictionary<Guid, (string? frameId, string? colorId)> userPersonalization, Message? lastMessage = null, string? name = null) => new(
        chat.Id,
        chat.Type.ToString(),
        chat.CreatedAt,
        chat.Participants.Select(p => {
            userPersonalization.TryGetValue(p.UserId, out var personalization);
            return p.ToDto(userRoles.TryGetValue(p.UserId, out var role) ? role : null, personalization.frameId, personalization.colorId);
        }).ToList(),
        lastMessage?.ToDto(),
        name
    );

    public static ChatParticipantDto ToDto(this ChatParticipant participant) => new(
        participant.UserId,
        participant.User?.BaseUser?.Username ?? string.Empty,
        participant.User?.BaseUser?.ProfilePhoto ?? string.Empty,
        participant.JoinedAt
    );

    public static ChatParticipantDto ToDto(this ChatParticipant participant, CommunityRole? role) => new(
        participant.UserId,
        participant.User?.BaseUser?.Username ?? string.Empty,
        participant.User?.BaseUser?.ProfilePhoto ?? string.Empty,
        participant.JoinedAt,
        role
    );

    public static ChatParticipantDto ToDto(this ChatParticipant participant, string? activeFrameId, string? activeColorId) => new(
        participant.UserId,
        participant.User?.BaseUser?.Username ?? string.Empty,
        participant.User?.BaseUser?.ProfilePhoto ?? string.Empty,
        participant.JoinedAt,
        null,
        activeFrameId,
        activeColorId
    );

    public static ChatParticipantDto ToDto(this ChatParticipant participant, CommunityRole? role, string? activeFrameId, string? activeColorId) => new(
        participant.UserId,
        participant.User?.BaseUser?.Username ?? string.Empty,
        participant.User?.BaseUser?.ProfilePhoto ?? string.Empty,
        participant.JoinedAt,
        role,
        activeFrameId,
        activeColorId
    );

    public static MessageDto ToDto(this Message message) => new(
        message.Id,
        message.ChatId,
        message.SenderId,
        message.Sender?.BaseUser?.Username ?? string.Empty,
        message.Body,
        message.SentAt,
        null,
        null
    );
}
