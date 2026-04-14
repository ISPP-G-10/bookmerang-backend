using Bookmerang.Api.Data;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Tests.Helpers;
using NetTopologySuite.Geometries;
using Xunit;

namespace Bookmerang.Tests.UserPreferences;

public class UserPreferencesServiceTutorialTests : IAsyncLifetime
{
    private AppDbContext _db = null!;
    private UserPreferenceService _service = null!;

    public Task InitializeAsync()
    {
        _db = DbContextFactory.CreateInMemory();
        _service = new UserPreferenceService(_db);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    private async Task<Guid> SeedRegularUserAsync(bool tutorialCompleted = false)
    {
        var userId = Guid.NewGuid();

        _db.Users.Add(new BaseUser
        {
            Id = userId,
            SupabaseId = $"sup-{userId:N}",
            Email = $"u-{userId:N}@test.com",
            Username = $"user-{userId:N}",
            Name = "Test User",
            ProfilePhoto = string.Empty,
            UserType = BaseUserType.USER,
            Location = new Point(-5.98, 37.39) { SRID = 4326 },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        _db.RegularUsers.Add(new User
        {
            Id = userId,
            TutorialCompleted = tutorialCompleted
        });

        await _db.SaveChangesAsync();
        return userId;
    }

    [Fact]
    public async Task GetTutorialStatusAsync_UserExists_ReturnsStoredStatus()
    {
        var userId = await SeedRegularUserAsync(tutorialCompleted: true);

        var result = await _service.GetTutorialStatusAsync(userId);

        Assert.NotNull(result);
        Assert.True(result!.TutorialCompleted);
    }

    [Fact]
    public async Task GetTutorialStatusAsync_UserMissing_ReturnsNull()
    {
        var result = await _service.GetTutorialStatusAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task SetTutorialStatusAsync_UserExists_UpdatesPersistedValue()
    {
        var userId = await SeedRegularUserAsync(tutorialCompleted: false);

        var result = await _service.SetTutorialStatusAsync(
            userId,
            new UpdateTutorialStatusDto { TutorialCompleted = true });

        Assert.NotNull(result);
        Assert.True(result!.TutorialCompleted);

        var persisted = await _db.RegularUsers.FindAsync(userId);
        Assert.NotNull(persisted);
        Assert.True(persisted!.TutorialCompleted);
    }

    [Fact]
    public async Task SetTutorialStatusAsync_UserMissing_ReturnsNull()
    {
        var result = await _service.SetTutorialStatusAsync(
            Guid.NewGuid(),
            new UpdateTutorialStatusDto { TutorialCompleted = true });

        Assert.Null(result);
    }
}
