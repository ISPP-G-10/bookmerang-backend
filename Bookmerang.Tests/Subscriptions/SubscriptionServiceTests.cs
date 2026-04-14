using Bookmerang.Api.Data;
using Bookmerang.Api.Exceptions;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Implementation.Subscriptions;
using Bookmerang.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Xunit;

namespace Bookmerang.Tests.Subscriptions;

public class SubscriptionServiceTests : IAsyncLifetime
{
    private AppDbContext _db = null!;
    private SubscriptionService _service = null!;

    public Task InitializeAsync()
    {
        _db = DbContextFactory.CreateInMemory();
        _service = new SubscriptionService(_db);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    private static Point MakePoint(double lon = 0, double lat = 0) =>
        new GeometryFactory(new PrecisionModel(), 4326).CreatePoint(new Coordinate(lon, lat));

    private async Task<Guid> SeedUser(PricingPlan plan = PricingPlan.FREE)
    {
        var id = Guid.NewGuid();
        _db.Users.Add(new BaseUser
        {
            Id = id,
            SupabaseId = $"sup-{id}",
            Email = $"{id}@test.com",
            Username = $"user_{id}",
            Name = "Test",
            Location = MakePoint()
        });
        _db.RegularUsers.Add(new User { Id = id, Plan = plan });
        await _db.SaveChangesAsync();
        return id;
    }

    private async Task<Guid> SeedBookdropUser()
    {
        var id = Guid.NewGuid();
        _db.Users.Add(new BaseUser
        {
            Id = id,
            SupabaseId = $"sup-bd-{id}",
            Email = $"bd_{id}@test.com",
            Username = $"bookdrop_{id}",
            Name = "Bookdrop Test",
            UserType = BaseUserType.BOOKDROP_USER,
            Location = MakePoint()
        });
        await _db.SaveChangesAsync();
        return id;
    }

    private async Task<Subscription> SeedActiveSubscription(Guid userId, DateTime? periodEnd = null)
    {
        var sub = new Subscription
        {
            UserId = userId,
            Platform = SubscriptionPlatform.STRIPE,
            PlatformSubscriptionId = $"sub_{Guid.NewGuid()}",
            Status = SubscriptionStatus.ACTIVE,
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-10),
            CurrentPeriodEnd = periodEnd ?? DateTime.UtcNow.AddDays(20),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Subscriptions.Add(sub);
        await _db.SaveChangesAsync();
        return sub;
    }

    // ── IsPremiumAsync ────────────────────────────────────────────────

    [Fact]
    public async Task IsPremiumAsync_FreeUser_ReturnsFalse()
    {
        var userId = await SeedUser(PricingPlan.FREE);
        Assert.False(await _service.IsPremiumAsync(userId));
    }

    [Fact]
    public async Task IsPremiumAsync_PremiumUser_ReturnsTrue()
    {
        var userId = await SeedUser(PricingPlan.PREMIUM);
        Assert.True(await _service.IsPremiumAsync(userId));
    }

    [Fact]
    public async Task IsPremiumAsync_UnknownUser_ReturnsFalse()
    {
        Assert.False(await _service.IsPremiumAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task HasActiveSubscriptionAsync_ActiveSubscription_ReturnsTrue()
    {
        var userId = await SeedUser(PricingPlan.FREE);
        await SeedActiveSubscription(userId, DateTime.UtcNow.AddDays(10));

        var hasActive = await _service.HasActiveSubscriptionAsync(userId);

        Assert.True(hasActive);
    }

    [Fact]
    public async Task HasActiveSubscriptionAsync_ExpiredSubscription_ReturnsFalse()
    {
        var userId = await SeedUser(PricingPlan.FREE);
        await SeedActiveSubscription(userId, DateTime.UtcNow.AddDays(-1));

        var hasActive = await _service.HasActiveSubscriptionAsync(userId);

        Assert.False(hasActive);
    }

    // ── CreateSubscriptionAsync ───────────────────────────────────────

    [Fact]
    public async Task CreateSubscription_CreatesRecord_AndSetsUserPlanToPremium()
    {
        var userId = await SeedUser(PricingPlan.FREE);
        var start = DateTime.UtcNow;
        var end = start.AddMonths(1);

        var sub = await _service.CreateSubscriptionAsync(
            userId, SubscriptionPlatform.STRIPE, "sub_123", null, start, end);

        Assert.Equal(SubscriptionStatus.ACTIVE, sub.Status);
        Assert.Equal(SubscriptionPlatform.STRIPE, sub.Platform);
        Assert.Equal("sub_123", sub.PlatformSubscriptionId);

        // User.Plan should be updated to PREMIUM
        var user = await _db.RegularUsers.FindAsync(userId);
        Assert.Equal(PricingPlan.PREMIUM, user!.Plan);
        Assert.True(await _service.IsPremiumAsync(userId));
    }

    [Fact]
    public async Task CreateSubscription_UnknownUser_ThrowsInvalidOperation()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateSubscriptionAsync(
                Guid.NewGuid(), SubscriptionPlatform.STRIPE, "sub_abc", null,
                DateTime.UtcNow, DateTime.UtcNow.AddMonths(1)));
    }

    [Fact]
    public async Task CreateSubscription_BookdropUser_CreatesRecord()
    {
        var userId = await SeedBookdropUser();
        var start = DateTime.UtcNow;
        var end = start.AddMonths(1);

        var sub = await _service.CreateSubscriptionAsync(
            userId, SubscriptionPlatform.STRIPE, "sub_bookdrop_123", null, start, end);

        Assert.Equal(SubscriptionStatus.ACTIVE, sub.Status);
        Assert.Equal(userId, sub.UserId);
    }

    // ── GetActiveSubscriptionAsync ────────────────────────────────────

    [Fact]
    public async Task GetActiveSubscription_NoSubscription_ReturnsNull()
    {
        var userId = await SeedUser();
        Assert.Null(await _service.GetActiveSubscriptionAsync(userId));
    }

    [Fact]
    public async Task GetActiveSubscription_ActiveExists_ReturnsIt()
    {
        var userId = await SeedUser();
        var sub = await SeedActiveSubscription(userId);

        var result = await _service.GetActiveSubscriptionAsync(userId);

        Assert.NotNull(result);
        Assert.Equal(sub.Id, result!.Id);
        Assert.Equal(SubscriptionStatus.ACTIVE, result.Status);
    }

    [Fact]
    public async Task GetActiveSubscription_OnlyCancelledExists_ReturnsNull()
    {
        var userId = await SeedUser();
        var sub = await SeedActiveSubscription(userId);

        // Cancel it
        sub.Status = SubscriptionStatus.CANCELLED;
        await _db.SaveChangesAsync();

        Assert.Null(await _service.GetActiveSubscriptionAsync(userId));
    }

    // ── UpdateSubscriptionStatusAsync ────────────────────────────────

    [Fact]
    public async Task UpdateSubscriptionStatus_ToExpired_SyncsUserPlanToFree()
    {
        var userId = await SeedUser(PricingPlan.PREMIUM);
        var sub = await SeedActiveSubscription(userId);

        await _service.UpdateSubscriptionStatusAsync(sub.Id, SubscriptionStatus.EXPIRED);

        var updatedSub = await _db.Subscriptions.FindAsync(sub.Id);
        Assert.Equal(SubscriptionStatus.EXPIRED, updatedSub!.Status);

        var user = await _db.RegularUsers.FindAsync(userId);
        Assert.Equal(PricingPlan.FREE, user!.Plan);
    }

    [Fact]
    public async Task UpdateSubscriptionStatus_UpdatesPeriodEnd_WhenProvided()
    {
        var userId = await SeedUser(PricingPlan.PREMIUM);
        var sub = await SeedActiveSubscription(userId);
        var newEnd = DateTime.UtcNow.AddMonths(2);

        await _service.UpdateSubscriptionStatusAsync(sub.Id, SubscriptionStatus.ACTIVE, newEnd);

        var updated = await _db.Subscriptions.FindAsync(sub.Id);
        Assert.Equal(newEnd.Date, updated!.CurrentPeriodEnd.Date);
    }

    [Fact]
    public async Task UpdateSubscriptionStatus_UnknownId_ThrowsInvalidOperation()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateSubscriptionStatusAsync(999, SubscriptionStatus.EXPIRED));
    }

    // ── SetCancelsAtPeriodEndAsync ────────────────────────────────────

    [Fact]
    public async Task SetCancelsAtPeriodEnd_SetsFlag_UserRemainsActive()
    {
        var userId = await SeedUser(PricingPlan.PREMIUM);
        var sub = await SeedActiveSubscription(userId);

        await _service.SetCancelsAtPeriodEndAsync(sub.Id, true);

        var updated = await _db.Subscriptions.FindAsync(sub.Id);
        Assert.True(updated!.CancelsAtPeriodEnd);

        // User is still PREMIUM until period ends
        var user = await _db.RegularUsers.FindAsync(userId);
        Assert.Equal(PricingPlan.PREMIUM, user!.Plan);
    }

    [Fact]
    public async Task SetCancelsAtPeriodEnd_UnknownId_ThrowsInvalidOperation()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SetCancelsAtPeriodEndAsync(999, true));
    }

    // ── ExtendSubscriptionAsync ───────────────────────────────────────

    [Fact]
    public async Task ExtendSubscription_ActiveSubscription_ExtendsPeriodEnd()
    {
        var userId = await SeedUser(PricingPlan.PREMIUM);
        var sub = await SeedActiveSubscription(userId);
        var originalEnd = sub.CurrentPeriodEnd;

        await _service.ExtendSubscriptionAsync(userId, 1, SubscriptionPlatform.SYSTEM);

        var updated = await _db.Subscriptions.FindAsync(sub.Id);
        Assert.True(updated!.CurrentPeriodEnd > originalEnd);
    }

    [Fact]
    public async Task ExtendSubscription_NoActiveSubscription_CreatesNewOne()
    {
        var userId = await SeedUser(PricingPlan.FREE);

        var result = await _service.ExtendSubscriptionAsync(userId, 1, SubscriptionPlatform.SYSTEM);

        Assert.Equal(SubscriptionStatus.ACTIVE, result.Status);
        Assert.Equal(SubscriptionPlatform.SYSTEM, result.Platform);

        var user = await _db.RegularUsers.FindAsync(userId);
        Assert.Equal(PricingPlan.PREMIUM, user!.Plan);
    }

    // ── SyncUserPlanFromSubscriptionAsync ─────────────────────────────

    [Fact]
    public async Task SyncUserPlan_ActiveSubscription_SetsPremium()
    {
        var userId = await SeedUser(PricingPlan.FREE);
        await SeedActiveSubscription(userId, DateTime.UtcNow.AddDays(30));

        await _service.SyncUserPlanFromSubscriptionAsync(userId);

        var user = await _db.RegularUsers.FindAsync(userId);
        Assert.Equal(PricingPlan.PREMIUM, user!.Plan);
    }

    [Fact]
    public async Task SyncUserPlan_NoActiveSubscription_SetsFree()
    {
        var userId = await SeedUser(PricingPlan.PREMIUM);

        // No subscription seeded
        await _service.SyncUserPlanFromSubscriptionAsync(userId);

        var user = await _db.RegularUsers.FindAsync(userId);
        Assert.Equal(PricingPlan.FREE, user!.Plan);
    }

    // ── HandleExpirationCheckAsync ────────────────────────────────────

    [Fact]
    public async Task HandleExpirationCheck_ExpiredSubscription_MarksExpiredAndSetsFree()
    {
        var userId = await SeedUser(PricingPlan.PREMIUM);
        // Seed already-expired subscription
        await SeedActiveSubscription(userId, periodEnd: DateTime.UtcNow.AddDays(-1));

        await _service.HandleExpirationCheckAsync();

        var sub = (await _db.Subscriptions.FindAsync(
            (await _db.Subscriptions
                .OrderByDescending(s => s.Id)
                .FirstAsync()).Id))!;
        Assert.Equal(SubscriptionStatus.EXPIRED, sub.Status);

        var user = await _db.RegularUsers.FindAsync(userId);
        Assert.Equal(PricingPlan.FREE, user!.Plan);
    }

    [Fact]
    public async Task HandleExpirationCheck_StillActiveSubscription_NotExpired()
    {
        var userId = await SeedUser(PricingPlan.PREMIUM);
        await SeedActiveSubscription(userId, periodEnd: DateTime.UtcNow.AddDays(10));

        await _service.HandleExpirationCheckAsync();

        var user = await _db.RegularUsers.FindAsync(userId);
        Assert.Equal(PricingPlan.PREMIUM, user!.Plan);
    }
}
