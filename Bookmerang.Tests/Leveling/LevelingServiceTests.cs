using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Implementation.Leveling;
using Bookmerang.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookmerang.Tests.Leveling;

public class LevelingServiceTests : IAsyncLifetime
{
    private AppDbContext _db = null!;
    private LevelingService _service = null!;

    public Task InitializeAsync()
    {
        _db = DbContextFactory.CreateInMemory();
        _service = new LevelingService(_db);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    private async Task<User> CreateTestUser(Guid? id = null, int xpTotal = 0)
    {
        var userId = id ?? Guid.NewGuid();
        _db.Users.Add(new BaseUser
        {
            Id = userId,
            SupabaseId = $"sup-{userId}",
            Email = $"{userId}@test.com",
            Username = $"user-{userId.ToString()[..8]}",
            Name = "Test User",
            ProfilePhoto = string.Empty,
            UserType = BaseUserType.USER,
            Location = new NetTopologySuite.Geometries.Point(0, 0) { SRID = 4326 },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        var user = new User
        {
            Id = userId,
            Plan = PricingPlan.FREE
        };
        _db.RegularUsers.Add(user);

        _db.UserProgresses.Add(new UserProgress
        {
            UserId = userId,
            XpTotal = xpTotal,
            StreakWeeks = 0,
            UpdatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task GetTotalXpAsync_UserExists_ReturnsXp()
    {
        var user = await CreateTestUser(xpTotal: 500);

        var result = await _service.GetTotalXpAsync(user.Id);

        Assert.Equal(500, result);
    }

    [Fact]
    public async Task GetTotalXpAsync_UserNotFound_ReturnsZero()
    {
        var result = await _service.GetTotalXpAsync(Guid.NewGuid());

        Assert.Equal(0, result);
    }

    [Fact]
    public void CalculateLevelInfo_ZeroXp_Level1()
    {
        var (level, xpToNext, progress) = _service.CalculateLevelInfo(0);

        Assert.Equal(1, level);
        Assert.True(xpToNext > 0);
        Assert.Equal(0.0, progress);
    }

    [Fact]
    public void CalculateLevelInfo_100Xp_Level2()
    {
        var (level, _, _) = _service.CalculateLevelInfo(100);

        Assert.Equal(2, level);
    }

    [Fact]
    public void CalculateLevelInfo_HighXp_CorrectLevel()
    {
        // Calculate XP for level 10
        var xpForLevel10 = _service.GetXpRequiredForLevel(10);
        var (level, _, _) = _service.CalculateLevelInfo(xpForLevel10);

        Assert.Equal(10, level);
    }

    [Fact]
    public void CalculateLevelInfo_ProgressCalculation()
    {
        var xpForLevel2 = _service.GetXpRequiredForLevel(2);
        var xpForLevel3 = _service.GetXpRequiredForLevel(3);
        
        // Midway between level 2 and 3
        int midpointXp = xpForLevel2 + (xpForLevel3 - xpForLevel2) / 2;
        var (level, _, progress) = _service.CalculateLevelInfo(midpointXp);

        Assert.Equal(2, level);
        Assert.True(progress > 0.4 && progress < 0.6); // Around 50%
    }

    [Fact]
    public void GetTier_Level1_Bronze()
    {
        var tier = _service.GetTier(1);

        Assert.Equal("BRONCE", tier);
    }

    [Fact]
    public void GetTier_Level5_Silver()
    {
        var tier = _service.GetTier(5);

        Assert.Equal("PLATA", tier);
    }

    [Fact]
    public void GetTier_Level15_Gold()
    {
        var tier = _service.GetTier(15);

        Assert.Equal("ORO", tier);
    }

    [Fact]
    public void GetTier_Level25_Platinum()
    {
        var tier = _service.GetTier(25);

        Assert.Equal("PLATINO", tier);
    }

    [Fact]
    public void GetTier_Level35_Diamond()
    {
        var tier = _service.GetTier(35);

        Assert.Equal("DIAMANTE", tier);
    }

    [Fact]
    public void GetTier_Level45_PlatinumElite()
    {
        var tier = _service.GetTier(45);

        Assert.Equal("PLATINO_ELITE", tier);
    }

    [Fact]
    public void GetTier_Level50_Legendary()
    {
        var tier = _service.GetTier(50);

        Assert.Equal("LEGENDARIO", tier);
    }

    [Fact]
    public void GetTier_MaxLevel_Legendary()
    {
        var tier = _service.GetTier(100);

        Assert.Equal("LEGENDARIO", tier);
    }

    [Fact]
    public void GetXpRequiredForLevel_Level1_ZeroXp()
    {
        var xp = _service.GetXpRequiredForLevel(1);

        Assert.Equal(0, xp);
    }

    [Fact]
    public void GetXpRequiredForLevel_Level2_100Xp()
    {
        var xp = _service.GetXpRequiredForLevel(2);

        Assert.Equal(100, xp);
    }

    [Fact]
    public void GetXpRequiredForLevel_Decreasing_ThrowsOrReturnsZero()
    {
        var xp = _service.GetXpRequiredForLevel(-5);

        Assert.Equal(0, xp);
    }

    [Fact]
    public void GetXpRequiredForLevel_Progressive()
    {
        var level5 = _service.GetXpRequiredForLevel(5);
        var level10 = _service.GetXpRequiredForLevel(10);
        var level15 = _service.GetXpRequiredForLevel(15);

        Assert.True(level5 < level10);
        Assert.True(level10 < level15);
    }

    [Fact]
    public void CalculateLevelInfo_MaxLevel_ConstrainedTo50()
    {
        // Very high XP beyond level 50
        var (level, _, _) = _service.CalculateLevelInfo(999999);

        Assert.True(level <= 50);
    }

    [Fact]
    public void GetXpRequiredForLevel_Consistency()
    {
        // Level thresholds should be consistent
        var level1 = _service.GetXpRequiredForLevel(1);
        var level2 = _service.GetXpRequiredForLevel(2);
        var level3 = _service.GetXpRequiredForLevel(3);

        // XP increases monotonically
        Assert.True(level1 <= level2);
        Assert.True(level2 < level3);
    }

    [Fact]
    public async Task GetXpRequiredForLevel_ConcurrentAccess_IsThreadSafe()
    {
        var tasks = new List<Task>();

        for (var t = 0; t < 32; t++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (var i = 0; i < 5000; i++)
                {
                    var level = (i % 70) - 10;
                    var xp = _service.GetXpRequiredForLevel(level);
                    Assert.True(xp >= 0);
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Equal(0, _service.GetXpRequiredForLevel(1));
        Assert.Equal(100, _service.GetXpRequiredForLevel(2));
    }
}
