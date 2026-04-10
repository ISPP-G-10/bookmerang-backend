using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Services.Implementation.Streaks;
using Bookmerang.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookmerang.Tests.Streaks;

public class StreakServiceTests : IAsyncLifetime
{
    private AppDbContext _db = null!;
    private StreakService _service = null!;

    public Task InitializeAsync()
    {
        _db = DbContextFactory.CreateInMemory();
        _service = new StreakService(_db);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    private async Task<UserProgress> CreateProgress(Guid userId, int streakWeeks = 0,
        DateTime? lastActiveDate = null, DateTime? lastDecrementDate = null)
    {
        var progress = new UserProgress
        {
            UserId = userId,
            XpTotal = 0,
            StreakWeeks = streakWeeks,
            LastActiveDate = lastActiveDate,
            LastDecrementDate = lastDecrementDate,
            StreakStartDate = lastActiveDate,
            UpdatedAt = DateTime.UtcNow
        };
        _db.UserProgresses.Add(progress);
        await _db.SaveChangesAsync();
        return progress;
    }

    // --- GetStreakLevelAsync ---

    [Fact]
    public async Task GetStreakLevel_NoProgress_ReturnsZero()
    {
        var result = await _service.GetStreakLevelAsync(Guid.NewGuid());
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetStreakLevel_WithProgress_ReturnsCorrectLevel()
    {
        var userId = Guid.NewGuid();
        await CreateProgress(userId, streakWeeks: 3);

        var result = await _service.GetStreakLevelAsync(userId);

        Assert.Equal(3, result);
    }

    // --- GetStreakMultiplier ---

    [Theory]
    [InlineData(0, 1.00)]
    [InlineData(1, 1.04)]
    [InlineData(2, 1.08)]
    [InlineData(3, 1.12)]
    [InlineData(4, 1.16)]
    [InlineData(5, 1.24)]
    [InlineData(6, 1.36)]
    public void GetStreakMultiplier_KnownLevels_ReturnsCorrectMultiplier(int level, decimal expected)
    {
        var result = _service.GetStreakMultiplier(level);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetStreakMultiplier_AboveMax_ReturnsCapped()
    {
        var result = _service.GetStreakMultiplier(999);
        Assert.Equal(1.36m, result);
    }

    [Fact]
    public void GetStreakMultiplier_NegativeLevel_TreatedAsZero()
    {
        var result = _service.GetStreakMultiplier(-1);
        Assert.Equal(1.00m, result);
    }

    // --- RegisterActiveActionAsync ---

    [Fact]
    public async Task RegisterActiveAction_NoExistingProgress_CreatesWithStreakOne()
    {
        var userId = Guid.NewGuid();

        await _service.RegisterActiveActionAsync(userId);

        var progress = await _db.UserProgresses.FirstOrDefaultAsync(p => p.UserId == userId);
        Assert.NotNull(progress);
        Assert.Equal(1, progress.StreakWeeks);
    }

    [Fact]
    public async Task RegisterActiveAction_AlreadyActiveThisWeek_DoesNotIncrement()
    {
        var userId = Guid.NewGuid();
        // LastActiveDate dentro de la semana actual
        var monday = DateTime.UtcNow.Date.AddDays(-(((int)DateTime.UtcNow.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7));
        await CreateProgress(userId, streakWeeks: 2, lastActiveDate: monday.AddHours(1));

        await _service.RegisterActiveActionAsync(userId);

        var progress = await _db.UserProgresses.FirstAsync(p => p.UserId == userId);
        Assert.Equal(2, progress.StreakWeeks);
    }

    [Fact]
    public async Task RegisterActiveAction_LastActiveLastWeek_IncrementsStreak()
    {
        var userId = Guid.NewGuid();
        await CreateProgress(userId, streakWeeks: 2, lastActiveDate: DateTime.UtcNow.AddDays(-8));

        await _service.RegisterActiveActionAsync(userId);

        var progress = await _db.UserProgresses.FirstAsync(p => p.UserId == userId);
        Assert.Equal(3, progress.StreakWeeks);
    }

    [Fact]
    public async Task RegisterActiveAction_StreakWasZero_ResetsToOne()
    {
        var userId = Guid.NewGuid();
        await CreateProgress(userId, streakWeeks: 0, lastActiveDate: DateTime.UtcNow.AddDays(-14));

        await _service.RegisterActiveActionAsync(userId);

        var progress = await _db.UserProgresses.FirstAsync(p => p.UserId == userId);
        Assert.Equal(1, progress.StreakWeeks);
    }

    [Fact]
    public async Task ProcessDecrements_UserActiveThisWeek_StreakUnchanged()
    {
        var userId = Guid.NewGuid();
        var monday = DateTime.UtcNow.Date.AddDays(-(((int)DateTime.UtcNow.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7));
        await CreateProgress(userId, streakWeeks: 3, lastActiveDate: monday.AddHours(2));

        await _service.ProcessDailyStreakDecrementsAsync();

        var progress = await _db.UserProgresses.FirstAsync(p => p.UserId == userId);
        Assert.Equal(3, progress.StreakWeeks);
    }


    [Fact]
    public async Task ProcessDecrements_StreakAlreadyZero_StaysZero()
    {
        var userId = Guid.NewGuid();
        await CreateProgress(userId, streakWeeks: 0, lastActiveDate: DateTime.UtcNow.AddDays(-10));

        await _service.ProcessDailyStreakDecrementsAsync();

        var progress = await _db.UserProgresses.FirstAsync(p => p.UserId == userId);
        Assert.Equal(0, progress.StreakWeeks);
    }

    [Fact]
    public async Task ProcessDecrements_UserInactiveOneWeek_DecrementsBy1()
    {
        var userId = Guid.NewGuid();
        var lastActive = DateTime.UtcNow.AddDays(-7);
        await CreateProgress(userId, streakWeeks: 5, lastActiveDate: lastActive);

        await _service.ProcessDailyStreakDecrementsAsync();

        var progress = await _db.UserProgresses.FirstAsync(p => p.UserId == userId);
        Assert.Equal(4, progress.StreakWeeks);
    }

    [Fact]
    public async Task ProcessDecrements_UserInactiveTwoWeeks_DecrementsBy2()
    {
        var userId = Guid.NewGuid();
        await CreateProgress(userId, streakWeeks: 5, lastActiveDate: DateTime.UtcNow.AddDays(-14));

        await _service.ProcessDailyStreakDecrementsAsync();

        var progress = await _db.UserProgresses.FirstAsync(p => p.UserId == userId);
        Assert.Equal(3, progress.StreakWeeks);
    }

    [Fact]
    public async Task ProcessDecrements_NeverDecrementsBelow0()
    {
        var userId = Guid.NewGuid();
        await CreateProgress(userId, streakWeeks: 1, lastActiveDate: DateTime.UtcNow.AddDays(-30));

        await _service.ProcessDailyStreakDecrementsAsync();

        var progress = await _db.UserProgresses.FirstAsync(p => p.UserId == userId);
        Assert.Equal(0, progress.StreakWeeks);
    }

    [Fact]
    public async Task ProcessDecrements_UpdatesLastDecrementDate()
    {
        var userId = Guid.NewGuid();
        await CreateProgress(userId, streakWeeks: 3, lastActiveDate: DateTime.UtcNow.AddDays(-8));

        var before = DateTime.UtcNow.AddSeconds(-1);
        await _service.ProcessDailyStreakDecrementsAsync();

        var progress = await _db.UserProgresses.FirstAsync(p => p.UserId == userId);
        Assert.NotNull(progress.LastDecrementDate);
        Assert.True(progress.LastDecrementDate >= before);
    }
}