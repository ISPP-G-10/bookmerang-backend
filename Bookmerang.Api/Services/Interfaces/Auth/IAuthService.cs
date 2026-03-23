using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using NetTopologySuite.Geometries;

namespace Bookmerang.Api.Services.Interfaces.Auth;

public interface IAuthService
{
    Task<ProfileDto?> GetPerfil(string supabaseId);
    Task<(BaseUser? usuario, bool yaExistia)> Register(string supabaseId, string email, string username, string name, string profilePhoto,
     BaseUserType type, Point location);
    Task<(BaseUser? usuario, bool yaExistia, string? error)> RegisterWithCredentials(
        string email,
        string password,
        string username,
        string name,
        string profilePhoto,
        BaseUserType type,
        Point location);
    Task<(BaseUser? usuario, string token, string? error)> Login(string email, string password);
    Task <BaseUser?> UpdatePerfil(string supabaseId, string? username, string? name, string? profilePhoto);
    Task <(BaseUser? usuario, string? error)> PatchEmail(string supabaseId, string newEmail);
    Task<(BaseUser? usuario, string? error)> PatchEmail(string supabaseId, string newEmail, string currentPassword);
    Task<string?> PatchPassword(string supabaseId, string currentPassword, string newPassword);
    Task<BaseUser?> DeletePerfil(string supabaseId);
}
