using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Interfaces.Auth;
using Bookmerang.Api.Services.Interfaces.Leveling;
using Bookmerang.Api.Services.Interfaces.Inkdrops;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.ComponentModel.DataAnnotations;
using Microsoft.IdentityModel.Tokens;

namespace Bookmerang.Api.Services.Implementation.Auth;

public class AuthService(AppDbContext db, IConfiguration config, ILevelingService levelingService, IInkdropsService inkdropsService) : IAuthService
{
    private static readonly EmailAddressAttribute EmailValidator = new();
    private readonly AppDbContext _db = db;
    private readonly IConfiguration _config = config;
    private readonly ILevelingService _levelingService = levelingService;
    private readonly IInkdropsService _inkdropsService = inkdropsService;

    public async Task<ProfileDto?> GetPerfil(string supabaseId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.SupabaseId == supabaseId);
        if (user == null) return null;

        var regularUser = await _db.RegularUsers.FirstOrDefaultAsync(u => u.Id == user.Id);
        if (regularUser == null) return null;

        var progress = await _db.UserProgresses.FirstOrDefaultAsync(p => p.UserId == user.Id);

        var xp = progress?.XpTotal ?? 0;
        var (level, inksToNextLevel, progressPercent) = _levelingService.CalculateLevelInfo(xp);
        var tier = _levelingService.GetTier(level);

        var streak = progress?.StreakWeeks ?? 0;
        var bonus = Math.Min(streak * 4, 20);

        var inkdropsData = await _inkdropsService.GetUserInkdropsAsync(user.Id);
        var monthlyInkDrops = inkdropsData.Inkdrops;
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
            Bonus = bonus,
            ActiveFrameId = progress?.ActiveFrameId,
            ActiveColorId = progress?.ActiveColorId,
        };
    }

    public async Task<(BaseUser? usuario, bool yaExistia)> Register(string supabaseId, string email, string username, string name, string profilePhoto,
     BaseUserType type, Point location)
    {
        var existe = await _db.Users.AnyAsync(u => u.SupabaseId == supabaseId);
        if (existe) return (null, true);

        var emailExiste = await _db.Users.AnyAsync(u => u.Email == email);
        if (emailExiste) return (null, true);

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
                Id = nuevoUsuario.Id,
                Plan = PricingPlan.FREE,
                RatingMean = 0,
                FinishedExchanges = 0,
                Inkdrops = 0,
                InkdropsLastUpdated = "1970-01"
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

    public async Task<(BaseUser? usuario, bool yaExistia, string? error)> RegisterWithCredentials(
        string email,
        string password,
        string username,
        string name,
        string profilePhoto,
        BaseUserType type,
        Point location)
    {
        if (string.IsNullOrWhiteSpace(email))
            return (null, false, "El email es obligatorio.");

        var normalizedEmail = NormalizeEmail(email);
        if (!IsValidEmail(normalizedEmail))
            return (null, false, "El correo electrónico no es válido.");

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            return (null, false, "La contraseña debe tener al menos 6 caracteres.");

        if (await _db.Users.AnyAsync(u => u.Email == normalizedEmail))
            return (null, true, "El email ya está registrado.");

        if (await _db.Users.AnyAsync(u => u.Username == username))
            return (null, false, "El nombre de usuario ya está en uso.");

        var internalSubject = Guid.NewGuid().ToString("N");
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);

        var (usuario, yaExistia) = await Register(
            internalSubject,
            normalizedEmail,
            username,
            name,
            profilePhoto,
            type,
            location
        );

        if (yaExistia || usuario == null)
            return (null, true, "El usuario ya existe en el sistema.");

        usuario.PasswordHash = hashedPassword;
        usuario.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return (usuario, false, null);
    }

    public async Task<(BaseUser? usuario, string token, string? error)> Login(string email, string password)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (!IsValidEmail(normalizedEmail))
            return (null, string.Empty, "El correo electrónico no es válido.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
        if (user == null)
            return (null, string.Empty, "Credenciales inválidas.");

        if (string.IsNullOrWhiteSpace(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return (null, string.Empty, "Credenciales inválidas.");

        var token = GenerateToken(user);
        return (user, token, null);
    }

    public async Task<BaseUser?> UpdatePerfil(string supabaseId, string? username, string? name, string? profilePhoto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.SupabaseId == supabaseId);

        if (user == null)
            return null;

        if (!string.IsNullOrWhiteSpace(username))
        {
            var usernameEnUso = await _db.Users.AnyAsync(
                u => u.Username == username && u.SupabaseId != supabaseId);

            if (usernameEnUso)
                throw new InvalidOperationException("El nombre de usuario ya está en uso.");

            user.Username = username;
        }

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

    var normalizedEmail = NormalizeEmail(newEmail);
    if (!IsValidEmail(normalizedEmail))
        return (null, "El correo electrónico no es válido.");

    var user = await _db.Users.FirstOrDefaultAsync(u => u.SupabaseId == supabaseId);
    if (user == null)
        return (null, "Usuario no encontrado.");

    var emailExiste = await _db.Users.AnyAsync(u => u.Email == normalizedEmail && u.SupabaseId != supabaseId);
    if (emailExiste)
        return (null, "El email ya está en uso.");

    user.Email = normalizedEmail;
    user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return (user, null);
    }

    public async Task<(BaseUser? usuario, string? error)> PatchEmail(string supabaseId, string newEmail, string currentPassword)
    {
        if (string.IsNullOrWhiteSpace(currentPassword))
            return (null, "La contraseña actual es obligatoria.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.SupabaseId == supabaseId);
        if (user == null)
            return (null, "Usuario no encontrado.");

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            // Usuario legado sin hash local: inicializamos su hash con la contraseña introducida.
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(currentPassword);
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        else if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
        {
            return (null, "Contraseña incorrecta.");
        }

        return await PatchEmail(supabaseId, newEmail);
    }

    public async Task<string?> PatchPassword(string supabaseId, string currentPassword, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(currentPassword))
            return "La contraseña actual es obligatoria.";

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            return "La nueva contraseña debe tener al menos 8 caracteres.";

        var user = await _db.Users.FirstOrDefaultAsync(u => u.SupabaseId == supabaseId);
        if (user == null)
            return "Usuario no encontrado.";

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            // Usuario legado sin hash local: aceptamos la contraseña actual indicada para inicializar hash.
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(currentPassword);
        }
        else if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
        {
            return "Contraseña actual incorrecta.";
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return null;
    }

    public async Task<BaseUser?> DeletePerfil(string supabaseId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.SupabaseId == supabaseId);

        if (user == null)
        {
            return null;
        }

        var userId = user.Id;

        // 1. Validar intercambios activos (Cualquiera que no sea COMPLETED o REJECTED)
        var hasActiveExchanges = await _db.Exchanges
            .Include(e => e.Match)
            .AnyAsync(e => (e.Match.User1Id == userId || e.Match.User2Id == userId) &&
                           e.Status != ExchangeStatus.COMPLETED &&
                           e.Status != ExchangeStatus.REJECTED &&
                           e.Status != ExchangeStatus.INCIDENT);

        if (hasActiveExchanges)
        {
            throw new Exception("No puedes borrar tu cuenta porque tienes intercambios en proceso.");
        }

        await using var tx = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync()
            : null;

        // 2. Borrado en cascada local manual (ya que EF tiene Restrict/NoAction en muchas de estas)

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

        // Participaciones en comunidades y quedadas
        var meetupAttendances = await _db.MeetupAttendances.Where(ma => ma.UserId == userId).ToListAsync();
        if (meetupAttendances.Any()) _db.MeetupAttendances.RemoveRange(meetupAttendances);

        var libraryLikes = await _db.CommunityLibraryLikes.Where(ll => ll.UserId == userId).ToListAsync();
        if (libraryLikes.Any()) _db.CommunityLibraryLikes.RemoveRange(libraryLikes);

        // Transferir rol de creator/admin de comunidades a otro miembro antes de remover membresías
        var communitiesAsCreator = await _db.Communities
            .Include(c => c.Members)
            .Where(c => c.CreatorId == userId)
            .ToListAsync();

        foreach (var community in communitiesAsCreator)
        {
            var otherMemberIds = community.Members
                .Where(m => m.UserId != userId)
                .Select(m => m.UserId)
                .ToList();

            if (otherMemberIds.Count == 0)
            {
                community.CreatorId = null;
            }
            else
            {
                var newCreatorId = otherMemberIds[Random.Shared.Next(otherMemberIds.Count)];
                community.CreatorId = newCreatorId;

                var newCreatorMembership = community.Members.FirstOrDefault(m => m.UserId == newCreatorId);
                if (newCreatorMembership != null)
                {
                    newCreatorMembership.Role = CommunityRole.MODERATOR;
                }
            }
        }

        var communityMembers = await _db.CommunityMembers.Where(cm => cm.UserId == userId).ToListAsync();
        if (communityMembers.Any()) _db.CommunityMembers.RemoveRange(communityMembers);

        // Validaciones de bookspots hechas por el usuario
        var bookspotValidations = await _db.BookspotValidations.Where(bv => bv.ValidatorUserId == userId).ToListAsync();
        if (bookspotValidations.Any()) _db.BookspotValidations.RemoveRange(bookspotValidations);

        // Bookspots y Meetups (Desvincular en lugar de borrar para que la comunidad no los pierda)
        if (_db.Database.IsRelational())
        {
            await _db.Database.ExecuteSqlRawAsync("UPDATE bookspots SET created_by_user_id = NULL WHERE created_by_user_id = {0}", userId);
            await _db.Database.ExecuteSqlRawAsync("UPDATE bookspots SET owner_id = NULL WHERE owner_id = {0}", userId);
            await _db.Database.ExecuteSqlRawAsync("UPDATE meetups SET creator_id = NULL WHERE creator_id = {0}", userId);
        }
        else
        {
            var userCreatedBookspots = await _db.Bookspots.Where(b => b.CreatedByUserId == userId).ToListAsync();
            foreach (var spot in userCreatedBookspots) spot.CreatedByUserId = null;

            var userOwnedBookspots = await _db.Bookspots.Where(b => b.OwnerId == userId).ToListAsync();
            foreach (var spot in userOwnedBookspots) spot.OwnerId = null;

            var userMeetups = await _db.Meetups.Where(m => m.CreatorId == userId).ToListAsync();
            foreach (var meetup in userMeetups) meetup.CreatorId = null;
        }

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

            // Likes de biblioteca hacia los libros del usuario
            var otherLibraryLikes = await _db.CommunityLibraryLikes.Where(ll => bookIds.Contains(ll.BookId)).ToListAsync();
            if (otherLibraryLikes.Any()) _db.CommunityLibraryLikes.RemoveRange(otherLibraryLikes);

            // Asistencias a quedadas usando los libros del usuario (por si acaso)
            var otherMeetupAttendances = await _db.MeetupAttendances.Where(ma => bookIds.Contains(ma.SelectedBookId)).ToListAsync();
            if (otherMeetupAttendances.Any()) _db.MeetupAttendances.RemoveRange(otherMeetupAttendances);

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

        // Tablas sin entidad EF que referencian al usuario (limpieza por SQL crudo)
        if (_db.Database.IsRelational())
        {
            await _db.Database.ExecuteSqlRawAsync("DELETE FROM points_ledgers WHERE user_id = {0}", userId);
            await _db.Database.ExecuteSqlRawAsync("DELETE FROM community_monthly_scores WHERE user_id = {0}", userId);
            await _db.Database.ExecuteSqlRawAsync("DELETE FROM incidents WHERE informer_id = {0} OR informed_id = {0} OR admin_id = {0}", userId);
            await _db.Database.ExecuteSqlRawAsync("DELETE FROM admins WHERE id = {0}", userId);
        }

        // Finalmente, el usuario base — limpiar primero subtipos por si la fila no es de tipo User
        var regularUser = await _db.RegularUsers.FindAsync(userId);
        if (regularUser != null) _db.RegularUsers.Remove(regularUser);

        var bookdropUser = await _db.BookdropUsers.FindAsync(userId);
        int? bookdropBookspotId = bookdropUser?.BookSpotId;
        if (bookdropUser != null) _db.BookdropUsers.Remove(bookdropUser);

        _db.Users.Remove(user);

        await _db.SaveChangesAsync();

        // Cascada del bookspot del bookdrop: cancelar (REJECTED) los intercambios cuyas reuniones
        // referencien al bookspot, borrar comunidades referenciantes (con sus members, chats,
        // likes, scores, meetups y attendances), validaciones, refs en exchange_meetings y
        // meetups, y finalmente el bookspot. Tras SaveChangesAsync para que bookdrop_users ya no
        // referencie al bookspot.
        if (bookdropBookspotId.HasValue)
        {
            var bsId = bookdropBookspotId.Value;

            // Cancelar intercambios asociados a reuniones que apuntan a este bookspot
            // (no se borran: se marcan como REJECTED para preservar histórico)
            var meetingsAtBookspot = await _db.ExchangeMeetings
                .Where(em => em.BookspotId == bsId)
                .ToListAsync();

            if (meetingsAtBookspot.Count > 0)
            {
                var exchangeIdsToCancel = meetingsAtBookspot.Select(em => em.ExchangeId).Distinct().ToList();
                var exchangesToCancel = await _db.Exchanges
                    .Where(e => exchangeIdsToCancel.Contains(e.ExchangeId)
                                && e.Status != ExchangeStatus.REJECTED
                                && e.Status != ExchangeStatus.COMPLETED)
                    .ToListAsync();

                var now = DateTime.UtcNow;
                var cancelledExchangeIds = new HashSet<int>();
                foreach (var ex in exchangesToCancel)
                {
                    ex.Status = ExchangeStatus.REJECTED;
                    ex.UpdatedAt = now;
                    cancelledExchangeIds.Add(ex.ExchangeId);
                }

                // Notificar a los usuarios del intercambio publicando un mensaje del sistema
                // en el chat asociado. Como Message.SenderId no es nullable, se usa el proposer
                // de la reunión (que ya es participante del chat) como remitente.
                const string systemBody = "[Sistema] Este intercambio ha sido cancelado porque el BookDrop asociado fue eliminado.";
                var cancelledExchanges = exchangesToCancel
                    .ToDictionary(e => e.ExchangeId, e => e.ChatId);

                foreach (var em in meetingsAtBookspot)
                {
                    if (cancelledExchangeIds.Contains(em.ExchangeId)
                        && cancelledExchanges.TryGetValue(em.ExchangeId, out var chatId))
                    {
                        _db.Messages.Add(new Message
                        {
                            ChatId = chatId,
                            SenderId = em.ProposerId,
                            Body = systemBody,
                            SentAt = now
                        });
                    }
                }

                // Desvincular reuniones del bookspot para satisfacer la FK al borrar el bookspot
                foreach (var em in meetingsAtBookspot)
                {
                    em.BookspotId = null;
                }

                await _db.SaveChangesAsync();
            }
        }

        if (bookdropBookspotId.HasValue && _db.Database.IsRelational())
        {
            var bsId = bookdropBookspotId.Value;

            await _db.Database.ExecuteSqlRawAsync(@"
                DELETE FROM meetup_attendance WHERE meetup_id IN (
                    SELECT id FROM meetups WHERE community_id IN (
                        SELECT id FROM communities WHERE reference_bookspot_id = {0}
                    )
                )", bsId);

            await _db.Database.ExecuteSqlRawAsync(@"
                DELETE FROM incidents WHERE meetup_id IN (
                    SELECT id FROM meetups WHERE community_id IN (
                        SELECT id FROM communities WHERE reference_bookspot_id = {0}
                    )
                )", bsId);

            await _db.Database.ExecuteSqlRawAsync(@"
                DELETE FROM meetups WHERE community_id IN (
                    SELECT id FROM communities WHERE reference_bookspot_id = {0}
                )", bsId);

            await _db.Database.ExecuteSqlRawAsync(@"
                DELETE FROM community_members WHERE community_id IN (
                    SELECT id FROM communities WHERE reference_bookspot_id = {0}
                )", bsId);

            await _db.Database.ExecuteSqlRawAsync(@"
                DELETE FROM community_library_likes WHERE community_id IN (
                    SELECT id FROM communities WHERE reference_bookspot_id = {0}
                )", bsId);

            await _db.Database.ExecuteSqlRawAsync(@"
                DELETE FROM community_monthly_scores WHERE community_id IN (
                    SELECT id FROM communities WHERE reference_bookspot_id = {0}
                )", bsId);

            await _db.Database.ExecuteSqlRawAsync(@"
                DELETE FROM community_chats WHERE community_id IN (
                    SELECT id FROM communities WHERE reference_bookspot_id = {0}
                )", bsId);

            await _db.Database.ExecuteSqlRawAsync(
                "DELETE FROM communities WHERE reference_bookspot_id = {0}", bsId);

            await _db.Database.ExecuteSqlRawAsync(
                "DELETE FROM bookspot_validations WHERE bookspot_id = {0}", bsId);

            await _db.Database.ExecuteSqlRawAsync(
                "UPDATE meetups SET other_book_spot_id = NULL WHERE other_book_spot_id = {0}", bsId);

            await _db.Database.ExecuteSqlRawAsync(
                "DELETE FROM bookspots WHERE id = {0}", bsId);
        }

        if (tx != null)
        {
            await tx.CommitAsync();
        }

        return user;
    }

    public async Task<(BaseUser? usuario, bool yaExistia, string? error)> RegisterBusiness(
        string email,
        string password,
        string username,
        string name,
        string? profilePhoto,
        Point location,
        string nombreEstablecimiento,
        string addressText)
    {
        if (string.IsNullOrWhiteSpace(email))
            return (null, false, "El email es obligatorio.");

        var normalizedEmail = NormalizeEmail(email);
        if (!IsValidEmail(normalizedEmail))
            return (null, false, "El correo electrónico no es válido.");

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            return (null, false, "La contraseña debe tener al menos 6 caracteres.");

        if (string.IsNullOrWhiteSpace(nombreEstablecimiento))
            return (null, false, "El nombre del establecimiento es obligatorio.");

        if (string.IsNullOrWhiteSpace(addressText))
            return (null, false, "La dirección del establecimiento es obligatoria.");

        if (await _db.Users.AnyAsync(u => u.Email == normalizedEmail))
            return (null, true, "El email ya está registrado.");

        if (await _db.Users.AnyAsync(u => u.Username == username))
            return (null, false, "El nombre de usuario ya está en uso.");

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var internalSubject = Guid.NewGuid().ToString("N");
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);

            // 1. Crear BaseUser con tipo BOOKDROP_USER
            // La localización del base user se fija a (0,0) porque no representa nada -> ubicación real en cada bookstop
            var factory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
            var zeroLocation = factory.CreatePoint(new Coordinate(0, 0));

            var baseUser = new BaseUser
            {
                SupabaseId = internalSubject,
                Email = normalizedEmail,
                Username = username,
                Name = name,
                ProfilePhoto = profilePhoto ?? string.Empty,
                PasswordHash = hashedPassword,
                UserType = BaseUserType.BOOKDROP_USER,
                Location = zeroLocation
            };

            _db.Users.Add(baseUser);
            await _db.SaveChangesAsync();

            // 2. Crear Bookspot sin owner (aún no existe bookdrop_user)
            var bookspot = new Bookspot
            {
                Nombre = nombreEstablecimiento,
                AddressText = addressText,
                Location = location,
                IsBookdrop = true,
                Status = BookspotStatus.ACTIVE
            };

            _db.Bookspots.Add(bookspot);
            await _db.SaveChangesAsync();

            // 3. Crear BookdropUser vinculando base_user y bookspot
            var bookdropUser = new BookdropUser
            {
                Id = baseUser.Id,
                BookSpotId = bookspot.Id
            };

            _db.BookdropUsers.Add(bookdropUser);
            bookspot.OwnerId = baseUser.Id;
            await _db.SaveChangesAsync();

            await transaction.CommitAsync();
            return (baseUser, false, null);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<PricingPlan> GetUserPlan(Guid userId)
    {
        var regularUser = await _db.RegularUsers.FindAsync(userId);
        return regularUser?.Plan ?? PricingPlan.FREE;
    }

    private string GenerateToken(BaseUser user)
    {
        var secret = _config["Auth:JwtSecret"] ?? _config["JWT_SECRET"];
        var issuer = _config["Auth:JwtIssuer"] ?? _config["JWT_ISSUER"] ?? "bookmerang-api";
        var audience = _config["Auth:JwtAudience"] ?? _config["JWT_AUDIENCE"] ?? "bookmerang-client";
        var ttlMinutes = int.TryParse(_config["Auth:JwtAccessTokenMinutes"] ?? _config["JWT_ACCESS_TOKEN_MINUTES"], out var ttl)
            ? ttl
            : 60;

        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("JWT_SECRET no está configurado.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.SupabaseId),
            new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", user.SupabaseId),
            new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", user.Email),
            new Claim("user_id", user.Id.ToString()),
            new Claim("user_type", ((int)user.UserType).ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(ttlMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string NormalizeEmail(string email) =>
        email.Trim().ToLowerInvariant();

    private static bool IsValidEmail(string email) =>
        !string.IsNullOrWhiteSpace(email) && EmailValidator.IsValid(email);

    public async Task<bool> UpdateCosmetics(string supabaseId, string? activeFrameId, string? activeColorId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.SupabaseId == supabaseId);
        if (user == null) return false;

        var progress = await _db.UserProgresses.FirstOrDefaultAsync(p => p.UserId == user.Id);
        if (progress == null) return false;

        if (activeFrameId != null)
            progress.ActiveFrameId = string.IsNullOrEmpty(activeFrameId) ? null : activeFrameId;

        if (activeColorId != null)
            progress.ActiveColorId = string.IsNullOrEmpty(activeColorId) ? null : activeColorId;

        // Normalizar fechas nullable a UTC para que Npgsql no las rechace
        if (progress.LastActiveDate.HasValue && progress.LastActiveDate.Value.Kind == DateTimeKind.Unspecified)
            progress.LastActiveDate = DateTime.SpecifyKind(progress.LastActiveDate.Value, DateTimeKind.Utc);

        if (progress.LastDecrementDate.HasValue && progress.LastDecrementDate.Value.Kind == DateTimeKind.Unspecified)
            progress.LastDecrementDate = DateTime.SpecifyKind(progress.LastDecrementDate.Value, DateTimeKind.Utc);

        if (progress.StreakStartDate.HasValue && progress.StreakStartDate.Value.Kind == DateTimeKind.Unspecified)
            progress.StreakStartDate = DateTime.SpecifyKind(progress.StreakStartDate.Value, DateTimeKind.Utc);

        progress.UpdatedAt = DateTime.UtcNow;
        _db.UserProgresses.Update(progress);
        await _db.SaveChangesAsync();

        return true;
    }

}
