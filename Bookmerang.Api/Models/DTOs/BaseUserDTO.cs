using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Models.DTOs;

public record BaseUserDto(
    Guid Id,
    string SupabaseId,
    string Email,
    string Username,
    string Name,
    string ProfilePhoto,
    BaseUserType UserType,
    double Latitud,
    double Longitud,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public static class BaseUserExtensions
{
    public static BaseUserDto ToDto(this BaseUser user) => new(
        user.Id,
        user.SupabaseId,
        user.Email,
        user.Username,
        user.Name,
        user.ProfilePhoto,
        user.UserType,
        user.Location.Y,
        user.Location.X,
        user.CreatedAt,
        user.UpdatedAt
    );
}