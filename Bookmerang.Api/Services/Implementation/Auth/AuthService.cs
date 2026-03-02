using Bookmerang.Api.Data;
using Bookmerang.Api.Models;
using Bookmerang.Api.Services.Interfaces.Auth;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace Bookmerang.Api.Services.Implementation.Auth;

public class AuthService(AppDbContext db) : IAuthService
{
    private readonly AppDbContext _db = db;

    public async Task<BaseUser?> GetPerfil(string supabaseId)
    {
        return await _db.BaseUsers.FirstOrDefaultAsync(u => u.SupabaseId == supabaseId);
    }

    public async Task<(BaseUser? usuario, bool yaExistia)> Register(string supabaseId, string email, string username, string name, string profilePhoto,
     BaseUserType type, Point location)
    {
        var existe = await _db.BaseUsers.AnyAsync(u => u.SupabaseId == supabaseId);
        if (existe) return (null, true);

        var nuevoUsuario = new BaseUser
        {
            SupabaseId = supabaseId,
            Email = email,
            Username = username,
            Name = name,
            ProfilePhoto = profilePhoto,
            UserType = type,
            Location = location
        };

        _db.BaseUsers.Add(nuevoUsuario);
        var nuevoUser = new User
        {
            Id = nuevoUsuario.Id
        };
        _db.Users.Add(nuevoUser);
        await _db.SaveChangesAsync();

        return (nuevoUsuario, false);
    }
}