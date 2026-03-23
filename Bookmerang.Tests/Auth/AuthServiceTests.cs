using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Data;
using Microsoft.EntityFrameworkCore;
using Bookmerang.Api.Services.Implementation.Auth;
using Bookmerang.Api.Models.Enums;
using NetTopologySuite.Geometries;
using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

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

        return new AuthService(db, config);
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
        db.Users.Add(new BaseUser
        {
            SupabaseId = supabaseId,
            Email = "original@test.com",
            Username = originalUsername,
            Name = originalName,
            ProfilePhoto = "original.jpg",
            UserType = BaseUserType.USER,
            Location = new Point(0, 0) { SRID = 4326 }
        });
        await db.SaveChangesAsync();

        var updatedUser = await service.UpdatePerfil(supabaseId, " ", null, "");

        Assert.NotNull(updatedUser);
        Assert.Equal(originalUsername, updatedUser.Username);
        Assert.Equal(originalName, updatedUser.Name);
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

        db.Matches.Add(new Match
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
            ChatId = 30,
            MatchId = 10,
            Status = ExchangeStatus.ACCEPTED,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<Exception>(() => service.DeletePerfil(supabaseId));
        Assert.Equal("No puedes borrar tu cuenta porque tienes intercambios en proceso.", ex.Message);
    }
}
