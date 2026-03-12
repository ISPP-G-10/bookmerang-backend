using Bookmerang.Api.Models.Entities;

namespace Bookmerang.Api.Models.DTOs;

public record TypingUserDto(
    Guid UserId,
    string Username,
    string ProfilePhoto,
    DateTime StartedAt
);

public static class TypingIndicatorExtensions
{
    public static TypingUserDto ToDto(this TypingIndicator indicator) => new(
        indicator.UserId,
        indicator.User?.BaseUser?.Username ?? string.Empty,
        indicator.User?.BaseUser?.ProfilePhoto ?? string.Empty,
        indicator.StartedAt
    );
}
