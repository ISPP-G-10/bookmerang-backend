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
        return await _db.Users.FirstOrDefaultAsync(u => u.SupabaseId == supabaseId);
    }

    public async Task<(BaseUser? usuario, bool yaExistia)> Register(string supabaseId, string email, string username, string name, string profilePhoto,
     BaseUserType type, Point location)
    {
        var existe = await _db.Users.AnyAsync(u => u.SupabaseId == supabaseId);
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

        _db.Users.Add(nuevoUsuario);
        await _db.SaveChangesAsync();

        // Si el tipo es USER, crear también la fila en la tabla "users"
        if (type == BaseUserType.USER)
        {
            var regularUser = new User
            {
                Id = nuevoUsuario.Id
            };
            _db.RegularUsers.Add(regularUser);
            await _db.SaveChangesAsync();
        }

        return (nuevoUsuario, false);
    }

    public async Task<BaseUser?> UpdatePerfil(string supabaseId,string? username, string? name, string? profilePhoto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.SupabaseId == supabaseId);

        if (user == null)
            return null;

        if (!string.IsNullOrWhiteSpace(username))
            user.Username = username;

        if (!string.IsNullOrWhiteSpace(name))
            user.Name = name;

        if (!string.IsNullOrWhiteSpace(profilePhoto))
            user.ProfilePhoto = profilePhoto;

        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return user;
    }

    public async Task<(BaseUser? usuario, string? error)> PatchEmail(string supabaseId, string newEmail)
{
    if (string.IsNullOrWhiteSpace(newEmail))
        return (null, "El email no puede estar vacío.");

    var user = await _db.Users.FirstOrDefaultAsync(u => u.SupabaseId == supabaseId);
    if (user == null)
        return (null, "Usuario no encontrado.");

    var emailExiste = await _db.Users.AnyAsync(u => u.Email == newEmail && u.SupabaseId != supabaseId);
    if (emailExiste)
        return (null, "El email ya está en uso.");

    user.Email = newEmail;
    user.UpdatedAt = DateTime.UtcNow;

    await _db.SaveChangesAsync();

    return (user, null);
}
}