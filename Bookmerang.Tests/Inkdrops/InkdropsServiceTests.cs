using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Implementation.Inkdrops;
using Bookmerang.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookmerang.Tests.Inkdrops;

public class InkdropsServiceTests : IAsyncLifetime
{
    private AppDbContext _db = null!;
    private InkdropsService _service = null!;

    public Task InitializeAsync()
    {
        _db = DbContextFactory.CreateInMemory();
        _service = new InkdropsService(_db);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    private async Task<User> CreateTestUser(Guid? id = null, PricingPlan plan = PricingPlan.FREE)
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
            Plan = plan
        };
        _db.RegularUsers.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task GetUserInkdrops_NewUser_ReturnsZero()
    {
        var user = await CreateTestUser();

        var result = await _service.GetUserInkdropsAsync(user.Id);

        Assert.Equal(0, result.Inkdrops);
        Assert.Equal(user.Id, result.UserId);
    }

    [Fact]
    public async Task GetUserInkdrops_UserNotFound_ThrowsInvalidOperationException()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.GetUserInkdropsAsync(Guid.NewGuid()));

        Assert.Contains("no encontrado", ex.Message.ToLower());
    }

    [Fact]
    public async Task GrantExchangeInkdrops_BothUsersReceive100()
    {
        var user1 = await CreateTestUser();
        var user2 = await CreateTestUser();

        await _service.GrantExchangeInkdropsAsync(user1.Id, user2.Id);

        var u1 = await _db.RegularUsers.FirstAsync(u => u.Id == user1.Id);
        var u2 = await _db.RegularUsers.FirstAsync(u => u.Id == user2.Id);
        Assert.Equal(100, u1.Inkdrops);
        Assert.Equal(100, u2.Inkdrops);
    }

    [Fact]
    public async Task GrantExchangeInkdrops_MultipleExchanges_Accumulates()
    {
        var user1 = await CreateTestUser();
        var user2 = await CreateTestUser();

        await _service.GrantExchangeInkdropsAsync(user1.Id, user2.Id);
        await _service.GrantExchangeInkdropsAsync(user1.Id, user2.Id);

        var u1 = await _db.RegularUsers.FirstAsync(u => u.Id == user1.Id);
        Assert.Equal(200, u1.Inkdrops);
    }

    [Fact]
    public async Task GrantExchangeInkdrops_UpdatesCommunityMonthlyScores()
    {
        var user1 = await CreateTestUser();
        var user2 = await CreateTestUser();

        var bookspot = new Bookspot
        {
            Nombre = "Test Spot",
            Location = new NetTopologySuite.Geometries.Point(0, 0) { SRID = 4326 },
            Status = BookspotStatus.ACTIVE,
            CreatedAt = DateTime.UtcNow
        };
        _db.Bookspots.Add(bookspot);
        await _db.SaveChangesAsync();

        var community = new Community
        {
            Name = "Test Community",
            ReferenceBookspotId = bookspot.Id,
            Status = CommunityStatus.ACTIVE,
            CreatorId = user1.Id,
            CreatedAt = DateTime.UtcNow
        };
        _db.Communities.Add(community);
        await _db.SaveChangesAsync();

        _db.CommunityMembers.Add(new CommunityMember
        {
            CommunityId = community.Id,
            UserId = user1.Id,
            Role = CommunityRole.MEMBER,
            JoinedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        await _service.GrantExchangeInkdropsAsync(user1.Id, user2.Id);

        var currentMonth = DateTime.UtcNow.ToString("yyyy-MM");
        var score = await _db.CommunityMonthlyScores
            .FirstOrDefaultAsync(s => s.CommunityId == community.Id && s.UserId == user1.Id && s.Month == currentMonth);

        Assert.NotNull(score);
        Assert.Equal(100, score.InkdropsThisMonth);
    }

    [Fact]
    public async Task GetCommunityRanking_FreeUser_ThrowsInvalidOperationException()
    {
        var user = await CreateTestUser(plan: PricingPlan.FREE);

        var bookspot = new Bookspot
        {
            Nombre = "Test",
            Location = new NetTopologySuite.Geometries.Point(0, 0) { SRID = 4326 },
            Status = BookspotStatus.ACTIVE,
            CreatedAt = DateTime.UtcNow
        };
        _db.Bookspots.Add(bookspot);
        await _db.SaveChangesAsync();

        var community = new Community
        {
            Name = "C1",
            ReferenceBookspotId = bookspot.Id,
            Status = CommunityStatus.ACTIVE,
            CreatorId = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        _db.Communities.Add(community);
        await _db.SaveChangesAsync();

        _db.CommunityMembers.Add(new CommunityMember
        {
            CommunityId = community.Id,
            UserId = user.Id,
            Role = CommunityRole.MEMBER,
            JoinedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.GetCommunityRankingAsync(user.Id, community.Id));

        Assert.Contains("PREMIUM", ex.Message);
    }

    [Fact]
    public async Task GetCommunityRanking_PremiumUser_ReturnsRanking()
    {
        var user = await CreateTestUser(plan: PricingPlan.PREMIUM);

        var bookspot = new Bookspot
        {
            Nombre = "Test",
            Location = new NetTopologySuite.Geometries.Point(0, 0) { SRID = 4326 },
            Status = BookspotStatus.ACTIVE,
            CreatedAt = DateTime.UtcNow
        };
        _db.Bookspots.Add(bookspot);
        await _db.SaveChangesAsync();

        var community = new Community
        {
            Name = "C1",
            ReferenceBookspotId = bookspot.Id,
            Status = CommunityStatus.ACTIVE,
            CreatorId = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        _db.Communities.Add(community);
        await _db.SaveChangesAsync();

        _db.CommunityMembers.Add(new CommunityMember
        {
            CommunityId = community.Id,
            UserId = user.Id,
            Role = CommunityRole.MEMBER,
            JoinedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var currentMonth = DateTime.UtcNow.ToString("yyyy-MM");
        _db.CommunityMonthlyScores.Add(new CommunityMonthlyScore
        {
            CommunityId = community.Id,
            UserId = user.Id,
            Month = currentMonth,
            InkdropsThisMonth = 300
        });
        await _db.SaveChangesAsync();

        var result = await _service.GetCommunityRankingAsync(user.Id, community.Id);

        Assert.Equal(community.Id, result.CommunityId);
        Assert.Single(result.Ranking);
        Assert.Equal(300, result.Ranking[0].InkdropsThisMonth);
    }

    [Fact]
    public async Task GetCommunityRanking_NotMember_ThrowsInvalidOperationException()
    {
        var user = await CreateTestUser(plan: PricingPlan.PREMIUM);

        var bookspot = new Bookspot
        {
            Nombre = "Test",
            Location = new NetTopologySuite.Geometries.Point(0, 0) { SRID = 4326 },
            Status = BookspotStatus.ACTIVE,
            CreatedAt = DateTime.UtcNow
        };
        _db.Bookspots.Add(bookspot);
        await _db.SaveChangesAsync();

        var community = new Community
        {
            Name = "C1",
            ReferenceBookspotId = bookspot.Id,
            Status = CommunityStatus.ACTIVE,
            CreatedAt = DateTime.UtcNow
        };
        _db.Communities.Add(community);
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.GetCommunityRankingAsync(user.Id, community.Id));

        Assert.Contains("miembro", ex.Message.ToLower());
    }

    [Fact]
    public async Task GetCommunityRanking_OnlyPremiumUsersAppear()
    {
        var premiumUser = await CreateTestUser(plan: PricingPlan.PREMIUM);
        var freeUser = await CreateTestUser(plan: PricingPlan.FREE);

        var bookspot = new Bookspot
        {
            Nombre = "Test",
            Location = new NetTopologySuite.Geometries.Point(0, 0) { SRID = 4326 },
            Status = BookspotStatus.ACTIVE,
            CreatedAt = DateTime.UtcNow
        };
        _db.Bookspots.Add(bookspot);
        await _db.SaveChangesAsync();

        var community = new Community
        {
            Name = "C1",
            ReferenceBookspotId = bookspot.Id,
            Status = CommunityStatus.ACTIVE,
            CreatorId = premiumUser.Id,
            CreatedAt = DateTime.UtcNow
        };
        _db.Communities.Add(community);
        await _db.SaveChangesAsync();

        _db.CommunityMembers.AddRange(
            new CommunityMember { CommunityId = community.Id, UserId = premiumUser.Id, Role = CommunityRole.MEMBER, JoinedAt = DateTime.UtcNow },
            new CommunityMember { CommunityId = community.Id, UserId = freeUser.Id, Role = CommunityRole.MEMBER, JoinedAt = DateTime.UtcNow }
        );
        await _db.SaveChangesAsync();

        var currentMonth = DateTime.UtcNow.ToString("yyyy-MM");
        _db.CommunityMonthlyScores.AddRange(
            new CommunityMonthlyScore { CommunityId = community.Id, UserId = premiumUser.Id, Month = currentMonth, InkdropsThisMonth = 200 },
            new CommunityMonthlyScore { CommunityId = community.Id, UserId = freeUser.Id, Month = currentMonth, InkdropsThisMonth = 300 }
        );
        await _db.SaveChangesAsync();

        var result = await _service.GetCommunityRankingAsync(premiumUser.Id, community.Id);

        Assert.Single(result.Ranking);
        Assert.Equal(premiumUser.Id, result.Ranking[0].UserId);
    }
}
