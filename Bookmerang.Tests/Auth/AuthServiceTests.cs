using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Data;
using Microsoft.EntityFrameworkCore;
using Bookmerang.Api.Services.Implementation.Auth;
using Bookmerang.Api.Services.Implementation.Leveling;
using Bookmerang.Api.Models.Enums;
using NetTopologySuite.Geometries;
using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Moq;
using Bookmerang.Api.Services.Implementation.Inkdrops;
using Bookmerang.Api.Services.Interfaces.Inkdrops;
using Bookmerang.Api.Services.Interfaces.Streaks;

namespace Bookmerang.Tests.Auth;

public class AuthServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static AuthService CreateService(AppDbContext db)
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var levelingService = new LevelingService(db);
        var streakServiceMock = new Mock<IStreakService>();
        var inkdropsService = new InkdropsService(db, streakServiceMock.Object);
        return new AuthService(db, config, levelingService, inkdropsService);
    }

    [Fact]
    public async Task Register_ShouldCreateBaseUser_WhenUserIsNewAndNotRegularUser()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var supabaseId = "new-supabase-id";
        var location = new Point(0, 0) { SRID = 4326 };

        (BaseUser? newUser, bool alreadyExisted) = await service.Register(
            supabaseId,
            "new@test.com",
            "newuser",
            "New User",
            "photo.jpg",
            BaseUserType.ADMIN,
            location
        );

        Assert.False(alreadyExisted);
        Assert.NotNull(newUser);
        Assert.Equal(supabaseId, newUser.SupabaseId);
        Assert.Single(db.Users);
        Assert.Empty(db.RegularUsers);
        Assert.Empty(db.UserProgresses);
    }

    [Fact]
    public async Task Register_ShouldCreateRegularUserAndProgress_WhenUserIsNewAndRegularUser()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var supabaseId = "new-regular-user-id";
        var location = new Point(0, 0) { SRID = 4326 };

        (BaseUser? newUser, bool alreadyExisted) = await service.Register(
            supabaseId,
            "new-regular@test.com",
            "newregular",
            "New Regular",
            "photo.jpg",
            BaseUserType.USER,
            location
        );

        Assert.False(alreadyExisted);
        Assert.NotNull(newUser);
        Assert.Single(db.Users);
        Assert.Single(db.RegularUsers);
        Assert.Single(db.UserProgresses);
    }

    [Fact]
    public async Task Register_ShouldReturnAlreadyExisted_WhenUserExists()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var supabaseId = "existing-supabase-id";
        db.Users.Add(new BaseUser
        {
            SupabaseId = supabaseId,
            Email = "existing@test.com",
            Username = "existing",
            Name = "Existing",
            ProfilePhoto = "photo.jpg",
            UserType = BaseUserType.USER,
            Location = new Point(0, 0) { SRID = 4326 }
        });
        await db.SaveChangesAsync();

        var location = new Point(0, 0) { SRID = 4326 };

        (BaseUser? newUser, bool alreadyExisted) = await service.Register(
            supabaseId,
            "exist@test.com",
            "exists",
            "Exists",
            "",
            BaseUserType.USER,
            location
        );

        Assert.True(alreadyExisted);
        Assert.Null(newUser);
        Assert.Single(db.Users);
    }

    [Fact]
    public async Task GetPerfil_ShouldReturnNull_WhenUserNotFound()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var supabaseId = "not-found-id";

        var profile = await service.GetPerfil(supabaseId);

        Assert.Null(profile);
    }

    [Fact]
    public async Task UpdatePerfil_ShouldUpdateFields_WhenUserExists()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var supabaseId = "user-to-update";
        db.Users.Add(new BaseUser
        {
            SupabaseId = supabaseId,
            Email = "old@test.com",
            Username = "olduser",
            Name = "Old Name",
            ProfilePhoto = "old.jpg",
            UserType = BaseUserType.USER,
            Location = new Point(0, 0) { SRID = 4326 }
        });
        await db.SaveChangesAsync();

        var newUsername = "newuser";
        var newName = "New Name";
        var newPhoto = "newphoto.jpg";

        var updatedUser = await service.UpdatePerfil(supabaseId, newUsername, newName, newPhoto);

        Assert.NotNull(updatedUser);
        Assert.Equal(newUsername, updatedUser.Username);
        Assert.Equal(newName, updatedUser.Name);
        Assert.Equal(newPhoto, updatedUser.ProfilePhoto);
    }

    [Fact]
    public async Task UpdatePerfil_ShouldIgnoreNullOrWhitespaceFields()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var supabaseId = "user-to-update-partial";
        var originalUsername = "originaluser";
        var originalName = "Original Name";
        var originalPhoto = "original.jpg";
        db.Users.Add(new BaseUser
        {
            SupabaseId = supabaseId,
            Email = "original@test.com",
            Username = originalUsername,
            Name = originalName,
            ProfilePhoto = originalPhoto,
            UserType = BaseUserType.USER,
            Location = new Point(0, 0) { SRID = 4326 }
        });
        await db.SaveChangesAsync();

        var updatedUser = await service.UpdatePerfil(supabaseId, " ", null, null);

        Assert.NotNull(updatedUser);
        Assert.Equal(originalUsername, updatedUser.Username);
        Assert.Equal(originalName, updatedUser.Name);
        Assert.Equal(originalPhoto, updatedUser.ProfilePhoto);
    }

    [Fact]
    public async Task UpdatePerfil_ShouldClearProfilePhoto_WhenEmptyStringIsProvided()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var supabaseId = "user-to-clear-photo";
        db.Users.Add(new BaseUser
        {
            SupabaseId = supabaseId,
            Email = "photo@test.com",
            Username = "photouser",
            Name = "Photo User",
            ProfilePhoto = "original.jpg",
            UserType = BaseUserType.USER,
            Location = new Point(0, 0) { SRID = 4326 }
        });
        await db.SaveChangesAsync();

        var updatedUser = await service.UpdatePerfil(supabaseId, null, null, "");

        Assert.NotNull(updatedUser);
        Assert.Equal(string.Empty, updatedUser.ProfilePhoto);
    }

    [Fact]
    public async Task PatchEmail_ShouldFail_WhenEmailIsInUse()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var supabaseId = "user-patching-email";
        db.Users.AddRange(
            new BaseUser
            {
                SupabaseId = supabaseId,
                Email = "old@email.com",
                Username = "user1",
                Name = "User 1",
                ProfilePhoto = "u1.jpg",
                UserType = BaseUserType.USER,
                Location = new Point(0, 0) { SRID = 4326 }
            },
            new BaseUser
            {
                SupabaseId = "other-user",
                Email = "new@email.com",
                Username = "user2",
                Name = "User 2",
                ProfilePhoto = "u2.jpg",
                UserType = BaseUserType.USER,
                Location = new Point(1, 1) { SRID = 4326 }
            }
        );
        await db.SaveChangesAsync();

        var (resultUser, error) = await service.PatchEmail(supabaseId, "new@email.com");

        Assert.Null(resultUser);
        Assert.Equal("El email ya está en uso.", error);
    }

    [Fact]
    public async Task DeletePerfil_ShouldThrowException_WhenUserHasActiveExchanges()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var userId = Guid.NewGuid();
        var supabaseId = "user-with-active-exchange";
        db.Users.Add(new BaseUser
        {
            Id = userId,
            SupabaseId = supabaseId,
            Email = "active@test.com",
            Username = "active",
            Name = "Active User",
            ProfilePhoto = "active.jpg",
            UserType = BaseUserType.USER,
            Location = new Point(0, 0) { SRID = 4326 }
        });

        db.Matches.Add(new Bookmerang.Api.Models.Entities.Match
        {
            Id = 10,
            User1Id = userId,
            User2Id = Guid.NewGuid(),
            Status = MatchStatus.CHAT_CREATED,
            CreatedAt = DateTime.UtcNow
        });

        db.Exchanges.Add(new Exchange
        {
            ExchangeId = 20,
            ChatId = Guid.NewGuid(),
            MatchId = 10,
            Status = ExchangeStatus.ACCEPTED,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<Exception>(() => service.DeletePerfil(supabaseId));
        Assert.Equal("No puedes borrar tu cuenta porque tienes intercambios en proceso.", ex.Message);
    }

    [Fact]
    public async Task DeletePerfil_ShouldReturnNull_WhenUserNotFound()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var result = await service.DeletePerfil("nonexistent-user-delete");

        Assert.Null(result);
    }

    [Fact]
    public async Task DeletePerfil_ShouldRemoveUserAndDependentEntities_WhenNoActiveExchanges()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var userId = Guid.NewGuid();
        var supabaseId = "user-to-delete-success";

        db.Users.Add(new BaseUser
        {
            Id = userId,
            SupabaseId = supabaseId,
            Email = "del@test.com",
            Username = "deluser",
            Name = "Del User",
            ProfilePhoto = "d.jpg",
            UserType = BaseUserType.USER,
            Location = new Point(0, 0) { SRID = 4326 }
        });

        db.RegularUsers.Add(new User { Id = userId });
        db.UserProgresses.Add(new UserProgress { UserId = userId, XpTotal = 10, StreakWeeks = 1, UpdatedAt = DateTime.UtcNow });

        db.UserPreferences.Add(new UserPreference
        {
            Id = 1,
            UserId = userId,
            Location = new Point(0,0) { SRID = 4326 },
            RadioKm = 10,
            Extension = Bookmerang.Api.Models.Enums.BooksExtension.MEDIUM,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        db.UserPreferenceGenres.Add(new UserPreferenceGenre { PreferencesId = 1, GenreId = 2 });

        var bookId = 123;
        db.Books.Add(new Book { Id = bookId, OwnerId = userId, Titulo = "B", Status = BookStatus.PUBLISHED });
        db.BookPhotos.Add(new BookPhoto { Id = 1, BookId = bookId, Url = "p.jpg", Orden = 0 });
        db.BookGenres.Add(new BookGenre { BookId = bookId, GenreId = 5 });
        db.BookLanguages.Add(new BookLanguage { BookId = bookId, LanguageId = 2 });

        db.Swipes.Add(new Swipe { Id = 1, SwiperId = userId, BookId = 999, Direction = Bookmerang.Api.Models.Enums.SwipeDirection.RIGHT, CreatedAt = DateTime.UtcNow });
        db.Swipes.Add(new Swipe { Id = 2, SwiperId = Guid.NewGuid(), BookId = bookId, Direction = Bookmerang.Api.Models.Enums.SwipeDirection.LEFT, CreatedAt = DateTime.UtcNow });

        var chatId = Guid.NewGuid();
        db.Chats.Add(new Chat { Id = chatId, Type = Bookmerang.Api.Models.Enums.ChatType.EXCHANGE });
        db.Messages.Add(new Message { Id = 1, ChatId = chatId, SenderId = userId, Body = "hi", SentAt = DateTime.UtcNow });
        db.ChatParticipants.Add(new ChatParticipant { ChatId = chatId, UserId = userId, JoinedAt = DateTime.UtcNow });
        db.TypingIndicators.Add(new TypingIndicator { Id = 1, ChatId = chatId, UserId = userId, StartedAt = DateTime.UtcNow });

        var matchId = 10;
        db.Matches.Add(new Bookmerang.Api.Models.Entities.Match { Id = matchId, User1Id = userId, User2Id = Guid.NewGuid(), Status = MatchStatus.CHAT_CREATED, CreatedAt = DateTime.UtcNow });
        db.Exchanges.Add(new Exchange { ExchangeId = 200, ChatId = chatId, MatchId = matchId, Status = ExchangeStatus.COMPLETED, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });

        await db.SaveChangesAsync();

        var deleted = await service.DeletePerfil(supabaseId);

        Assert.NotNull(deleted);

        Assert.False(await db.Users.AnyAsync(u => u.SupabaseId == supabaseId));
        Assert.False(await db.RegularUsers.AnyAsync(r => r.Id == userId));
        Assert.False(await db.UserProgresses.AnyAsync(p => p.UserId == userId));
        Assert.False(await db.Books.AnyAsync(b => b.OwnerId == userId));
        Assert.False(await db.BookPhotos.AnyAsync());
        Assert.False(await db.BookGenres.AnyAsync());
        Assert.False(await db.BookLanguages.AnyAsync());
        Assert.False(await db.Swipes.AnyAsync(s => s.SwiperId == userId || s.BookId == bookId));
        Assert.False(await db.Messages.AnyAsync(m => m.SenderId == userId));
        Assert.False(await db.ChatParticipants.AnyAsync(cp => cp.UserId == userId));
        Assert.False(await db.TypingIndicators.AnyAsync(t => t.UserId == userId));
    }

     [Fact]
    public async Task GetPerfil_ShouldHandleMissingUserProgress_ReturnDefaults()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var userId = Guid.NewGuid();
        var supabaseId = "user-no-progress";
        var location = new Point(1.0, 2.0) { SRID = 4326 };

        db.Users.Add(new BaseUser
        {
            Id = userId,
            SupabaseId = supabaseId,
            Email = "noprog@test.com",
            Username = "noprog",
            Name = "No Prog",
            ProfilePhoto = "np.jpg",
            UserType = BaseUserType.USER,
            Location = location
        });
        
        // Add regularUser (User entity) which is required by GetPerfil
        db.RegularUsers.Add(new User 
        { 
            Id = userId,
            Plan = Bookmerang.Api.Models.Enums.PricingPlan.FREE,
            RatingMean = 0,
            FinishedExchanges = 0,
            Inkdrops = 0,
            InkdropsLastUpdated = "1970-01"
        });
        await db.SaveChangesAsync();

        var dto = await service.GetPerfil(supabaseId);

        Assert.NotNull(dto);
        Assert.Equal(1, dto.Level);
        Assert.Equal(0.0, dto.Progress);
        Assert.Equal(100, dto.InksToNextLevel); // Level 1→2 requires 100 XP
        Assert.Equal(0, dto.Streak);
        Assert.Equal(0, dto.Bonus);
    }

    [Fact]
    public async Task GetPerfil_ShouldMapBasicFields_WhenUserExists()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var userId = Guid.NewGuid();
        var supabaseId = "supabase-1";
        var location = new Point(2.5, 41.4) { SRID = 4326 };

        db.Users.Add(new BaseUser {
            Id = userId,
            SupabaseId = supabaseId,
            Email = "u@test.com",
            Username = "u1",
            Name = "User One",
            ProfilePhoto = "avatar.jpg",
            Location = location,
            UserType = BaseUserType.USER
        });
        
        // Add regularUser (User entity) which is required by GetPerfil
        db.RegularUsers.Add(new User 
        { 
            Id = userId,
            Plan = Bookmerang.Api.Models.Enums.PricingPlan.FREE,
            RatingMean = 0,
            FinishedExchanges = 0,
            Inkdrops = 0,
            InkdropsLastUpdated = "1970-01"
        });
        await db.SaveChangesAsync();

        var dto = await service.GetPerfil(supabaseId);

        Assert.NotNull(dto);
        Assert.Equal(userId, dto.Id);
        Assert.Equal(supabaseId, dto.SupabaseId);
        Assert.Equal("u@test.com", dto.Email);
        Assert.Equal("u1", dto.Username);
        Assert.Equal("User One", dto.Name);
        Assert.Equal("avatar.jpg", dto.Avatar);
        Assert.Equal(location.Y, dto.Latitud);
        Assert.Equal(location.X, dto.Longitud);
    }[Fact]
    public async Task UpdatePerfil_ShouldReturnNull_WhenUserNotFound()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var supabaseId = "nonexistent-user";
        var result = await service.UpdatePerfil(supabaseId, "newuser", "New Name", "newphoto.jpg");
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdatePerfil_ShouldUpdateUpdatedAt_WhenUserExists()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var supabaseId = "user-to-update-timestamp";
        db.Users.Add(new BaseUser
        {
            SupabaseId = supabaseId,
            Email = "test@test.com",
            Username = "testuser",
            Name = "Test User",
            ProfilePhoto = "test.jpg",
            UserType = BaseUserType.USER,
            Location = new Point(0, 0) { SRID = 4326 },
        });
        await db.SaveChangesAsync();
        var before = DateTime.UtcNow;
        var updated = await service.UpdatePerfil(supabaseId, "newuser", "New Name", "newphoto.jpg");

        var updatedAt = (await db.Users.FirstAsync()).UpdatedAt;

        Assert.NotNull(updated);
        Assert.True(updatedAt >= before, "UpdatedAt debe ser posterior al instante antes de la actualización");
        Assert.InRange(updatedAt, before, DateTime.UtcNow.AddSeconds(5));

    }

    [Fact]
    public async Task UpdatePerfil_ShouldNotChangeOtherFields()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var supabaseId = "user-to-update-same-fields";
        var originalEmail = "original@test.com";
        db.Users.Add(new BaseUser
        {
            SupabaseId = supabaseId,
            Email = originalEmail,
            Username = "testuser",
            Name = "Test User",
            ProfilePhoto = "test.jpg",
            UserType = BaseUserType.USER,
            Location = new Point(0, 0) { SRID = 4326 },
        });
        await db.SaveChangesAsync();
        var updated = await service.UpdatePerfil(supabaseId, "testuser", "Test User", "test.jpg");
        var userInDb = await db.Users.FirstAsync();
        Assert.NotNull(updated);
        Assert.Equal(originalEmail, userInDb.Email);
    }

    [Fact]
    public async Task PatchPassword_ShouldFail_WhenCurrentPasswordEmpty()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var supabaseId = "user-empty-currentpw-pass";
        db.Users.Add(new BaseUser
        {
            SupabaseId = supabaseId,
            Email = "e@test.com",
            Username = "euser",
            Name = "Empty PW",
            ProfilePhoto = "e.jpg",
            UserType = BaseUserType.USER,
            Location = new Point(0, 0) { SRID = 4326 }
        });
        await db.SaveChangesAsync();

        var error = await service.PatchPassword(supabaseId, "", "NewPass123!");

        Assert.Equal("La contraseña actual es obligatoria.", error);
    }

    [Fact]
    public async Task PatchPassword_ShouldFail_WhenUserNotFound()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var error = await service.PatchPassword("no-such-user", "any", "NewPass123!");

        Assert.Equal("Usuario no encontrado.", error);
    }

    [Fact]
    public async Task PatchPassword_ShouldFail_WhenNewPasswordTooShort()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var supabaseId = "user-short-newpass";
        var current = "Correct1!";
        db.Users.Add(new BaseUser
        {
            SupabaseId = supabaseId,
            Email = "u@test.com",
            Username = "u",
            Name = "U",
            ProfilePhoto = "p.jpg",
            UserType = BaseUserType.USER,
            Location = new Point(0, 0) { SRID = 4326 },
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(current)
        });
        await db.SaveChangesAsync();

        var error = await service.PatchPassword(supabaseId, current, "short");

        Assert.Equal("La nueva contraseña debe tener al menos 8 caracteres.", error);
    }

    [Fact]
    public async Task PatchPassword_ShouldFail_WhenCurrentPasswordIncorrect()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var supabaseId = "user-wrong-current";
        var correct = "Correct123!";
        var wrong = "Wrong123!";
        db.Users.Add(new BaseUser
        {
            SupabaseId = supabaseId,
            Email = "h@test.com",
            Username = "huser",
            Name = "Hashed User",
            ProfilePhoto = "h.jpg",
            UserType = BaseUserType.USER,
            Location = new Point(0, 0) { SRID = 4326 },
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(correct)
        });
        await db.SaveChangesAsync();

        var error = await service.PatchPassword(supabaseId, wrong, "NewValidPass1!");

        Assert.Equal("Contraseña actual incorrecta.", error);

        var userInDb = await db.Users.FirstAsync(u => u.SupabaseId == supabaseId);
        Assert.True(BCrypt.Net.BCrypt.Verify(correct, userInDb.PasswordHash));
    }

    [Fact]
    public async Task PatchPassword_ShouldInitializeHash_ForLegacyUser()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var supabaseId = "legacy-pass-user";
        var currentPassword = "legacyPass123";
        var newPassword = "NewPass123!";
        db.Users.Add(new BaseUser
        {
            SupabaseId = supabaseId,
            Email = "legacy@test.com",
            Username = "legacy",
            Name = "Legacy User",
            ProfilePhoto = "l.jpg",
            UserType = BaseUserType.USER,
            Location = new Point(0, 0) { SRID = 4326 },
            PasswordHash = null
        });
        await db.SaveChangesAsync();

        var error = await service.PatchPassword(supabaseId, currentPassword, newPassword);

        Assert.Null(error);

        var userInDb = await db.Users.FirstAsync(u => u.SupabaseId == supabaseId);
        Assert.NotNull(userInDb.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify(newPassword, userInDb.PasswordHash));
        Assert.NotEqual(default(DateTime), userInDb.UpdatedAt);
    }

    [Fact]
    public async Task PatchPassword_ShouldSucceed_WhenCurrentPasswordCorrect()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var supabaseId = "user-change-pass";
        var current = "Correct123!";
        var newPassword = "BrandNew123!";
        db.Users.Add(new BaseUser
        {
            SupabaseId = supabaseId,
            Email = "u@test.com",
            Username = "u",
            Name = "U",
            ProfilePhoto = "p.jpg",
            UserType = BaseUserType.USER,
            Location = new Point(0, 0) { SRID = 4326 },
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(current)
        });
        await db.SaveChangesAsync();

        var error = await service.PatchPassword(supabaseId, current, newPassword);

        Assert.Null(error);

        var userInDb = await db.Users.FirstAsync(u => u.SupabaseId == supabaseId);
        Assert.True(BCrypt.Net.BCrypt.Verify(newPassword, userInDb.PasswordHash));
        Assert.NotEqual(default(DateTime), userInDb.UpdatedAt);
    }

    [Fact]
    public async Task PatchPassword_ShouldFail_WhenNewPasswordIsNull()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var supabaseId = "user-newpass-null";
        var current = "Correct123!";
        db.Users.Add(new BaseUser
        {
            SupabaseId = supabaseId,
            Email = "u@test.com",
            Username = "u",
            Name = "U",
            ProfilePhoto = "p.jpg",
            UserType = BaseUserType.USER,
            Location = new Point(0, 0) { SRID = 4326 },
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(current)
        });
        await db.SaveChangesAsync();

        var error = await service.PatchPassword(supabaseId, current, null!);

        Assert.Equal("La nueva contraseña debe tener al menos 8 caracteres.", error);
    }

    [Fact]
    public async Task PatchPassword_ShouldFail_WhenNewPasswordIsWhitespace()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var supabaseId = "user-newpass-ws";
        var current = "Correct123!";
        db.Users.Add(new BaseUser
        {
            SupabaseId = supabaseId,
            Email = "u2@test.com",
            Username = "u2",
            Name = "U2",
            ProfilePhoto = "p2.jpg",
            UserType = BaseUserType.USER,
            Location = new Point(0, 0) { SRID = 4326 },
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(current)
        });
        await db.SaveChangesAsync();

        var error = await service.PatchPassword(supabaseId, current, "   ");

        Assert.Equal("La nueva contraseña debe tener al menos 8 caracteres.", error);
    }

    [Fact]
    public async Task PatchPassword_ShouldFail_WhenNewPasswordLength7()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var supabaseId = "user-newpass-7";
        var current = "Correct123!";
        db.Users.Add(new BaseUser
        {
            SupabaseId = supabaseId,
            Email = "u3@test.com",
            Username = "u3",
            Name = "U3",
            ProfilePhoto = "p3.jpg",
            UserType = BaseUserType.USER,
            Location = new Point(0, 0) { SRID = 4326 },
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(current)
        });
        await db.SaveChangesAsync();

        var error = await service.PatchPassword(supabaseId, current, "Abc1234");

        Assert.Equal("La nueva contraseña debe tener al menos 8 caracteres.", error);
    }

    [Fact]
    public async Task PatchPassword_ShouldSucceed_WhenNewPasswordLength8_WithExistingHash()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var supabaseId = "user-newpass-8-hash";
        var current = "Correct123!";
        var newPassword = "NewP4ss!";
        db.Users.Add(new BaseUser
        {
            SupabaseId = supabaseId,
            Email = "u4@test.com",
            Username = "u4",
            Name = "U4",
            ProfilePhoto = "p4.jpg",
            UserType = BaseUserType.USER,
            Location = new Point(0, 0) { SRID = 4326 },
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(current)
        });
        await db.SaveChangesAsync();

        var error = await service.PatchPassword(supabaseId, current, newPassword);

        Assert.Null(error);
        var userInDb = await db.Users.FirstAsync(u => u.SupabaseId == supabaseId);
        Assert.True(BCrypt.Net.BCrypt.Verify(newPassword, userInDb.PasswordHash));
    }

    [Fact]
    public async Task PatchPassword_ShouldSucceed_WhenNewPasswordLength8_ForLegacyUser()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var supabaseId = "user-newpass-8-legacy";
        var current = "legacyPass1";
        var newPassword = "LegacY88";
        db.Users.Add(new BaseUser
        {
            SupabaseId = supabaseId,
            Email = "u5@test.com",
            Username = "u5",
            Name = "U5",
            ProfilePhoto = "p5.jpg",
            UserType = BaseUserType.USER,
            Location = new Point(0, 0) { SRID = 4326 },
            PasswordHash = null
        });
        await db.SaveChangesAsync();

        var error = await service.PatchPassword(supabaseId, current, newPassword);

        Assert.Null(error);
        var userInDb = await db.Users.FirstAsync(u => u.SupabaseId == supabaseId);
        Assert.True(BCrypt.Net.BCrypt.Verify(newPassword, userInDb.PasswordHash));
    }

    [Fact]
    public async Task PatchEmail_ShouldSucceed_WhenEmailIsValid()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var supabaseId = "user-patching-email-success";
        var originalEmail = "original@test.com";
        db.Users.Add(new BaseUser
        {
            SupabaseId = supabaseId,
            Email = originalEmail,
            Username = "testuser",
            Name = "Test User",
            ProfilePhoto = "test.jpg",
            UserType = BaseUserType.USER,
            Location = new Point(0, 0) { SRID = 4326 }
        });
        await db.SaveChangesAsync();
        var newEmail = "original2@test.com";
        var (resultUser, error) = await service.PatchEmail(supabaseId, newEmail);
        Assert.Null(error);
        Assert.NotNull(resultUser);
        Assert.Equal(newEmail, resultUser.Email);
    }

    [Fact]
    public async Task PatchEmail_ShouldFail_WhenEmailEmptyOrWhitespace()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var supabaseId = "user-patching-email-empty";
        db.Users.Add(new BaseUser
        {
            SupabaseId = supabaseId,
            Email = "original@test.com",
            Username = "testuser",
            Name = "Test User",
            ProfilePhoto = "test.jpg",
            UserType = BaseUserType.USER,
            Location = new Point(0, 0) { SRID = 4326 }
        });
        await db.SaveChangesAsync();
        var (resultUser, error) = await service.PatchEmail(supabaseId, "   ");
        Assert.NotNull(error);
        Assert.Null(resultUser);
    }

    [Fact]
    public async Task PatchEmail_ShouldFail_WhenUserNotFound()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var supabaseId = "nonexistent-user-email-patch";
        var (resultUser, error) = await service.PatchEmail(supabaseId, "new@test.com");
        Assert.NotNull(error);
        Assert.Null(resultUser);
    }

    [Fact]
    public async Task PatchEmail_WithCurrentPassword_ShouldFail_WhenCurrentPasswordEmpty()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var supabaseId = "user-empty-currentpw";
        db.Users.Add(new BaseUser
        {
            SupabaseId = supabaseId,
            Email = "e@test.com",
            Username = "euser",
            Name = "Empty PW",
            ProfilePhoto = "e.jpg",
            UserType = BaseUserType.USER,
            Location = new Point(0, 0) { SRID = 4326 }
        });
        await db.SaveChangesAsync();

        var (resultUser, error) = await service.PatchEmail(supabaseId, "new@test.com", "");

        Assert.Null(resultUser);
        Assert.Equal("La contraseña actual es obligatoria.", error);
    }

    [Fact]
    public async Task PatchEmail_WithCurrentPassword_ShouldFail_WhenUserNotFound()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var supabaseId = "nonexistent-user-pw-patch";

        var (resultUser, error) = await service.PatchEmail(supabaseId, "new@test.com", "currentpw");

        Assert.Null(resultUser);
        Assert.Equal("Usuario no encontrado.", error);
    }

    [Fact]
    public async Task PatchEmail_WithCurrentPassword_ShouldInitializeHash_ForLegacyUser()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var supabaseId = "legacy-user";
        var currentPassword = "legacyPass123";
        db.Users.Add(new BaseUser
        {
            SupabaseId = supabaseId,
            Email = "legacy@test.com",
            Username = "legacy",
            Name = "Legacy User",
            ProfilePhoto = "l.jpg",
            UserType = BaseUserType.USER,
            Location = new Point(0, 0) { SRID = 4326 },
            PasswordHash = null
        });
        await db.SaveChangesAsync();

        var (resultUser, error) = await service.PatchEmail(supabaseId, "updated@test.com", currentPassword);

        Assert.Null(error);
        Assert.NotNull(resultUser);
        Assert.Equal("updated@test.com", resultUser.Email);
        Assert.False(string.IsNullOrWhiteSpace(resultUser.PasswordHash));
        Assert.True(BCrypt.Net.BCrypt.Verify(currentPassword, resultUser.PasswordHash));
        Assert.NotEqual(default(DateTime), resultUser.UpdatedAt);
    }

    [Fact]
    public async Task PatchEmail_WithCurrentPassword_ShouldFail_WhenCurrentPasswordIncorrect()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var supabaseId = "user-with-hash";
        var correctPassword = "Correct123!";
        var wrongPassword = "Wrong123!";
        var originalEmail = "orig@test.com";
        db.Users.Add(new BaseUser
        {
            SupabaseId = supabaseId,
            Email = originalEmail,
            Username = "huser",
            Name = "Hashed User",
            ProfilePhoto = "h.jpg",
            UserType = BaseUserType.USER,
            Location = new Point(0, 0) { SRID = 4326 },
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(correctPassword)
        });
        await db.SaveChangesAsync();

        var (resultUser, error) = await service.PatchEmail(supabaseId, "new@test.com", wrongPassword);

        Assert.Null(resultUser);
        Assert.Equal("Contraseña incorrecta.", error);

        var userInDb = await db.Users.FirstAsync(u => u.SupabaseId == supabaseId);
        Assert.Equal(originalEmail, userInDb.Email);
    }

    [Fact]
    public async Task GetPerfil_Gamification_ComputesLevelTierBonusMonthlyAndDays()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var userId = Guid.NewGuid();
        var supabaseId = "user-gamification";
        var totalXp = 5000; // 5000 XP total accumulated
        var streak = 10;

        db.Users.Add(new BaseUser
        {
            Id = userId,
            SupabaseId = supabaseId,
            Email = "g@test.com",
            Username = "guser",
            Name = "Gamer",
            ProfilePhoto = "g.jpg",
            UserType = BaseUserType.USER,
            Location = new Point(0, 0) { SRID = 4326 }
        });

        var regularUser = new User 
        { 
            Id = userId,
            Inkdrops = 250, // Monthly inkdrops
            InkdropsLastUpdated = DateTime.UtcNow.ToString("yyyy-MM")
        };
        db.RegularUsers.Add(regularUser);

        // Create LevelingService to calculate expected level for this XP
        var levelingService = new LevelingService(db);
        var (expectedLevel, _, expectedProgress) = levelingService.CalculateLevelInfo(totalXp);

        db.UserProgresses.Add(new UserProgress 
        { 
            UserId = userId, 
            XpTotal = totalXp,
            StreakWeeks = streak, 
            UpdatedAt = DateTime.UtcNow 
        });

        await db.SaveChangesAsync();

        var dto = await service.GetPerfil(supabaseId);

        Assert.NotNull(dto);
        Assert.Equal(expectedLevel, dto.Level);
        Assert.Equal(expectedProgress, dto.Progress, 0.01); // Allow small floating point variance
        Assert.Equal(streak, dto.Streak);
        Assert.Equal(Math.Min(streak * 4, 20), dto.Bonus);
        Assert.Equal(250, dto.MonthlyInkDrops);
        var expectedDaysUntilReset = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month) - DateTime.UtcNow.Day;
        Assert.Equal(expectedDaysUntilReset, dto.DaysUntilReset);
    }

    [Fact]
    public async Task GetPerfil_TierThresholds_AssignsCorrectTier()
    {
        var cases = new (int level, string tier)[]
        {
            (4, "BRONCE"),
            (5, "PLATA"),
            (14, "PLATA"),
            (15, "ORO"),
            (24, "ORO"),
            (25, "PLATINO"),
            (34, "PLATINO"),
            (35, "DIAMANTE"),
            (44, "DIAMANTE"),
            (45, "PLATINO_ELITE"),
            (49, "PLATINO_ELITE"),
            (50, "LEGENDARIO")
        };

        foreach (var (level, expectedTier) in cases)
        {
            await using var db = CreateDbContext();
            var service = CreateService(db);
            var userId = Guid.NewGuid();
            var supabaseId = $"tier-{level}";

            db.Users.Add(new BaseUser
            {
                Id = userId,
                SupabaseId = supabaseId,
                Email = $"t{level}@test.com",
                Username = $"t{level}",
                Name = $"Tier {level}",
                ProfilePhoto = "t.jpg",
                UserType = BaseUserType.USER,
                Location = new Point(0, 0) { SRID = 4326 }
            });

            var regularUser = new User { Id = userId };
            db.RegularUsers.Add(regularUser);

            // Use LevelingService to calculate XP needed for exact level
            var levelingService = new LevelingService(db);
            var xpNeeded = levelingService.GetXpRequiredForLevel(level);

            // Set XpTotal to reach that exact level
            db.UserProgresses.Add(new UserProgress 
            { 
                UserId = userId, 
                XpTotal = xpNeeded,
                StreakWeeks = 0, 
                UpdatedAt = DateTime.UtcNow 
            });
            await db.SaveChangesAsync();

            var dto = await service.GetPerfil(supabaseId);
            Assert.NotNull(dto);
            Assert.Equal(level, dto.Level);
            Assert.Equal(expectedTier, dto.Tier);
        }
    }

    [Fact]
    public async Task Register_ShouldReturnAlreadyExisted_WhenEmailIsDuplicate()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var location = new Point(0, 0) { SRID = 4326 };

        // Register the first user
        await service.Register(
            "supabase-first",
            "duplicate@test.com",
            "firstuser",
            "First User",
            "photo.jpg",
            BaseUserType.USER,
            location
        );

        // Attempt to register a second user with the same email but different SupabaseId
        (BaseUser? secondUser, bool alreadyExisted) = await service.Register(
            "supabase-second",
            "duplicate@test.com",
            "seconduser",
            "Second User",
            "photo2.jpg",
            BaseUserType.USER,
            location
        );

        Assert.True(alreadyExisted);
        Assert.Null(secondUser);
        Assert.Single(db.Users);
    }

    [Fact]
    public async Task RegisterWithCredentials_ShouldReturnError_WhenEmailIsDuplicate()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var location = new Point(0, 0) { SRID = 4326 };

        // Register the first user with credentials
        var (firstUser, _, firstError) = await service.RegisterWithCredentials(
            "first@test.com",
            "Password123",
            "firstuser",
            "First User",
            "photo.jpg",
            BaseUserType.USER,
            location
        );

        Assert.Null(firstError);
        Assert.NotNull(firstUser);

        // Attempt to register a second user with the same email
        var (secondUser, yaExistia, secondError) = await service.RegisterWithCredentials(
            "first@test.com",
            "Password456",
            "seconduser",
            "Second User",
            "photo2.jpg",
            BaseUserType.USER,
            location
        );

        Assert.Null(secondUser);
        Assert.True(yaExistia);
        Assert.Equal("El email ya está registrado.", secondError);
        Assert.Single(db.Users);
    }
}
