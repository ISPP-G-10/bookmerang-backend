using Bookmerang.Api.Models;
using NetTopologySuite.Geometries;

namespace Bookmerang.Api.Services.Interfaces.Auth;

public interface IAuthService
{
    Task<BaseUser?> GetPerfil(string supabaseId);
    Task<(BaseUser? usuario, bool yaExistia)> Register(string supabaseId, string email, string username, string name, string profilePhoto,
     BaseUserType type, Point location);
    Task <BaseUser?> UpdatePerfil(string supabaseId, string? username, string? name, string? profilePhoto);
    Task <(BaseUser? usuario, string? error)> PatchEmail(string supabaseId, string newEmail);
}
