using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Data;
using Bookmerang.Api.Models;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Interfaces.Auth;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using NetTopologySuite.Geometries;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;

namespace Bookmerang.Api.Services.Implementation.Auth;

public class AuthService(AppDbContext db, IConfiguration config) : IAuthService
{
    private readonly AppDbContext _db = db;
    private readonly IConfiguration _config = config;

    public async Task<ProfileDto?> GetPerfil(string supabaseId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.SupabaseId == supabaseId);
        if (user == null) return null;

        var progress = await _db.UserProgresses.FirstOrDefaultAsync(p => p.UserId == user.Id);

        // Lógica de gamificación base
        var xp = progress?.XpTotal ?? 0;
        var level = (xp / 1000) + 1; // 1000 XP por nivel
        var inksToNextLevel = 1000 - (xp % 1000);
        var progressPercent = (double)(xp % 1000) / 1000.0;

        // Tier basado en el nivel
        string tier = "BRONCE";
        if (level >= 50) tier = "DIAMANTE";
        else if (level >= 25) tier = "PLATINO";
        else if (level >= 10) tier = "ORO";
        else if (level >= 5) tier = "PLATA";

        // Bonus basado en racha
        var streak = progress?.StreakWeeks ?? 0;
        var bonus = Math.Min(streak * 4, 20); // 4% por semana, máx 20%

        // Simulamos MonthlyInkDrops y DaysUntilReset por ahora o los podemos derivar
        var monthlyInkDrops = xp % 500; // Solo para mostrar un valor
        var daysUntilReset = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month) - DateTime.UtcNow.Day;

        return new ProfileDto
        {
            Id = user.Id,
            SupabaseId = user.SupabaseId,
            Email = user.Email,
            Username = user.Username,
            Name = user.Name,
            Avatar = user.ProfilePhoto,
            Latitud = user.Location.Y,
            Longitud = user.Location.X,
            Level = level,
            Tier = tier,
            MonthlyInkDrops = monthlyInkDrops,
            DaysUntilReset = daysUntilReset,
            InksToNextLevel = inksToNextLevel,
            Progress = progressPercent,
            Streak = streak,
            Bonus = bonus
        };
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
            var userProgress = new UserProgress
            {
                UserId = regularUser.Id,
                XpTotal = 0,
                StreakWeeks = 0,
                UpdatedAt = DateTime.UtcNow
            };

            _db.UserProgresses.Add(userProgress);
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

        if (profilePhoto != null)
            user.ProfilePhoto = string.IsNullOrWhiteSpace(profilePhoto) ? string.Empty : profilePhoto;

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

    public async Task<BaseUser?> DeletePerfil(string supabaseId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.SupabaseId == supabaseId);
        
        if (user == null)
        {
            // El usuario no está en la base de datos local, pero intentamos limpiarlo de Supabase por si acaso
            await DeleteFromSupabaseAuth(supabaseId);
            return null;
        }

        var userId = user.Id;

        // 1. Validar intercambios activos (Cualquiera que no sea COMPLETED o REJECTED)
        var hasActiveExchanges = await _db.Exchanges
            .Include(e => e.Match)
            .AnyAsync(e => (e.Match.User1Id == userId || e.Match.User2Id == userId) && 
                           e.Status != ExchangeStatus.COMPLETED && 
                           e.Status != ExchangeStatus.REJECTED);

        if (hasActiveExchanges)
        {
            throw new Exception("No puedes borrar tu cuenta porque tienes intercambios en proceso.");
        }

        // 2. Borrar de Supabase Auth PRIMERO para evitar inconsistencias si falla la conexión
        await DeleteFromSupabaseAuth(supabaseId);

        // 3. Borrado en cascada local manual (ya que EF tiene Restrict/NoAction en muchas de estas)

        // TypingIndicators
        var typingIndicators = await _db.TypingIndicators.Where(t => t.UserId == userId).ToListAsync();
        if (typingIndicators.Any()) _db.TypingIndicators.RemoveRange(typingIndicators);

        // Messages
        var messages = await _db.Messages.Where(m => m.SenderId == userId).ToListAsync();
        if (messages.Any()) _db.Messages.RemoveRange(messages);

        // ChatPart
        // icipants
        var participants = await _db.ChatParticipants.Where(cp => cp.UserId == userId).ToListAsync();
        if (participants
        .Any()) _db.ChatParticipants.RemoveRange(participants);

        // Matches, Exchanges, ExchangeMeetings
        var userMatches = await _db.Matches.Where(m => m.User1Id == userId || m.User2Id == userId).ToListAsync();
        var matchIds = userMatches.Select(m => m.Id).ToList();

        var exchanges = await _db.Exchanges.Where(e => matchIds.Contains(e.MatchId)).ToListAsync();
        var exchangeIds = exchanges.Select(e => e.ExchangeId).ToList();
        var chatIds = exchanges.Select(e => e.ChatId).ToList();

        var exchangeMeetings = await _db.ExchangeMeetings.Where(em => exchangeIds.Contains(em.ExchangeId)).ToListAsync();
        if (exchangeMeetings.Any()) _db.ExchangeMeetings.RemoveRange(exchangeMeetings);

        if (exchanges.Any()) _db.Exchanges.RemoveRange(exchanges);
        if (userMatches.Any()) _db.Matches.RemoveRange(userMatches);

        // Delete chats linked to exchanges
        var chats = await _db.Chats.Where(c => chatIds.Contains(c.Id)).ToListAsync();
        if (chats.Any())
        {
            // Also remove remaining messages & participants of these chats from the other user
            var otherMessages = await _db.Messages.Where(m => chatIds.Contains(m.ChatId)).ToListAsync();
            if (otherMessages.Any()) _db.Messages.RemoveRange(otherMessages);
            var otherParticipants = await _db.ChatParticipants.Where(cp => chatIds.Contains(cp.ChatId)).ToListAsync();
            if (otherParticipants.Any()) _db.ChatParticipants.RemoveRange(otherParticipants);
            var otherTyping = await _db.TypingIndicators.Where(t => chatIds.Contains(t.ChatId)).ToListAsync();
            if (otherTyping.Any()) _db.TypingIndicators.RemoveRange(otherTyping);

            _db.Chats.RemoveRange(chats);
        }

        // Swipes hechos por el usuario
        var userSwipes = await _db.Swipes.Where(s => s.SwiperId == userId).ToListAsync();
        if (userSwipes.Any()) _db.Swipes.RemoveRange(userSwipes);

        // Libros y sus dependencias
        var userBooks = await _db.Books.Where(b => b.OwnerId == userId).ToListAsync();
        if (userBooks.Any())
        {
            var bookIds = userBooks.Select(b => b.Id).ToList();

            var bookPhotos = await _db.BookPhotos.Where(bp => bookIds.Contains(bp.BookId)).ToListAsync();
            if (bookPhotos.Any()) _db.BookPhotos.RemoveRange(bookPhotos);

            var bookGenres = await _db.BookGenres.Where(bg => bookIds.Contains(bg.BookId)).ToListAsync();
            if (bookGenres.Any()) _db.BookGenres.RemoveRange(bookGenres);

            var bookLanguages = await _db.BookLanguages.Where(bl => bookIds.Contains(bl.BookId)).ToListAsync();
            if (bookLanguages.Any()) _db.BookLanguages.RemoveRange(bookLanguages);

            // Swipes recibidos hacia los libros del usuario
            var swipesToUserBooks = await _db.Swipes.Where(s => bookIds.Contains(s.BookId)).ToListAsync();
            if (swipesToUserBooks.Any()) _db.Swipes.RemoveRange(swipesToUserBooks);

            _db.Books.RemoveRange(userBooks);
        }

        // Progreso y Preferencias
        var userProgress = await _db.UserProgresses.FirstOrDefaultAsync(p => p.UserId == userId);
        if (userProgress != null) _db.UserProgresses.Remove(userProgress);

        var preferences = await _db.UserPreferences.FirstOrDefaultAsync(p => p.UserId == userId);
        if (preferences != null)
        {
            var prefGenres = await _db.UserPreferenceGenres.Where(pg => pg.PreferencesId == preferences.Id).ToListAsync();
            if (prefGenres.Any()) _db.UserPreferenceGenres.RemoveRange(prefGenres);
            _db.UserPreferences.Remove(preferences);
        }

        // Finalmente, el usuario base
        var regularUser = await _db.RegularUsers.FindAsync(userId);
        if (regularUser != null) _db.RegularUsers.Remove(regularUser);

        _db.Users.Remove(user);

        await _db.SaveChangesAsync();

        return user;
    }

    private async Task DeleteFromSupabaseAuth(string supabaseId)
    {
        var supabaseUrl = _config["Supabase:Url"] ?? _config["SUPABASE_URL"];
        var serviceRoleKey = _config["Supabase:ServiceRoleKey"] ?? _config["SUPABASE_SERVICE_ROLE_KEY"];

        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(serviceRoleKey))
            throw new InvalidOperationException("Supabase configuration missing: SUPABASE_URL and SUPABASE_SERVICE_ROLE_KEY must be set.");

        using var http = new HttpClient();
        http.BaseAddress = new Uri(supabaseUrl);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", serviceRoleKey);
        http.DefaultRequestHeaders.Add("apikey", serviceRoleKey);

        var response = await http.DeleteAsync($"/auth/v1/admin/users/{supabaseId}");

        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Error borrando usuario en Supabase Auth: {error}");
        }
    }

}
