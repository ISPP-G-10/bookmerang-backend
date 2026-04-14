using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Interfaces.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace Bookmerang.Api.Services.Implementation.Subscriptions;

public class SubscriptionService(AppDbContext db) : ISubscriptionService
{
    private readonly AppDbContext _db = db;

    public async Task<bool> IsPremiumAsync(Guid userId)
    {
        var user = await _db.RegularUsers.FindAsync(userId);
        if (user == null) return false;

        return user.Plan == PricingPlan.PREMIUM;
    }

    public async Task<bool> HasActiveSubscriptionAsync(Guid userId)
    {
        var now = DateTime.UtcNow;
        return await _db.Subscriptions
            .AnyAsync(s => s.UserId == userId &&
                           s.Status == SubscriptionStatus.ACTIVE &&
                           s.CurrentPeriodEnd > now);
    }

    public async Task<Subscription?> GetActiveSubscriptionAsync(Guid userId)
    {
        return await _db.Subscriptions
            .Where(s => s.UserId == userId && s.Status == SubscriptionStatus.ACTIVE)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<Subscription> CreateSubscriptionAsync(
        Guid userId,
        SubscriptionPlatform platform,
        string platformSubscriptionId,
        string? originalTransactionId,
        DateTime periodStart,
        DateTime periodEnd)
    {
        var baseUser = await _db.Users.FindAsync(userId);
        if (baseUser == null)
            throw new InvalidOperationException($"User {userId} not found");

        var subscription = new Subscription
        {
            UserId = userId,
            Platform = platform,
            PlatformSubscriptionId = platformSubscriptionId,
            OriginalTransactionId = originalTransactionId,
            Status = SubscriptionStatus.ACTIVE,
            CurrentPeriodStart = periodStart,
            CurrentPeriodEnd = periodEnd,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Subscriptions.Add(subscription);
        await _db.SaveChangesAsync();

        // Sync plan only for regular users
        var regularUser = await _db.RegularUsers.FindAsync(userId);
        if (regularUser != null)
        {
            regularUser.Plan = PricingPlan.PREMIUM;
            await _db.SaveChangesAsync();
        }

        return subscription;
    }

    public async Task UpdateSubscriptionStatusAsync(
        int subscriptionId,
        SubscriptionStatus newStatus,
        DateTime? newPeriodEnd = null)
    {
        var subscription = await _db.Subscriptions.FindAsync(subscriptionId);
        if (subscription == null)
            throw new InvalidOperationException($"Subscription {subscriptionId} not found");

        subscription.Status = newStatus;
        subscription.UpdatedAt = DateTime.UtcNow;

        if (newPeriodEnd.HasValue)
            subscription.CurrentPeriodEnd = newPeriodEnd.Value;

        await _db.SaveChangesAsync();

        // Sync the user's plan
        await SyncUserPlanFromSubscriptionAsync(subscription.UserId);
    }

    public async Task<Subscription> ExtendSubscriptionAsync(
        Guid userId,
        int months,
        SubscriptionPlatform platform)
    {
        var user = await _db.RegularUsers.FindAsync(userId);
        if (user == null)
            throw new InvalidOperationException($"User {userId} not found");

        var activeSubscription = await GetActiveSubscriptionAsync(userId);

        if (activeSubscription != null)
        {
            // Extend existing subscription
            activeSubscription.CurrentPeriodEnd = activeSubscription.CurrentPeriodEnd.AddMonths(months);
            activeSubscription.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return activeSubscription;
        }
        else
        {
            // Create new subscription (user's previous subscription expired)
            var newPeriodStart = DateTime.UtcNow;
            var newPeriodEnd = newPeriodStart.AddMonths(months);

            var newSubscription = new Subscription
            {
                UserId = userId,
                Platform = platform,
                Status = SubscriptionStatus.ACTIVE,
                CurrentPeriodStart = newPeriodStart,
                CurrentPeriodEnd = newPeriodEnd,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Subscriptions.Add(newSubscription);
            user.Plan = PricingPlan.PREMIUM;
            await _db.SaveChangesAsync();

            return newSubscription;
        }
    }

    public async Task SyncUserPlanFromSubscriptionAsync(Guid userId)
    {
        var user = await _db.RegularUsers.FindAsync(userId);
        if (user == null) return;

        var activeSubscription = await _db.Subscriptions
            .Where(s => s.UserId == userId && s.Status == SubscriptionStatus.ACTIVE && s.CurrentPeriodEnd > DateTime.UtcNow)
            .AnyAsync();

        user.Plan = activeSubscription ? PricingPlan.PREMIUM : PricingPlan.FREE;
        await _db.SaveChangesAsync();
    }

    public async Task HandleExpirationCheckAsync()
    {
        var now = DateTime.UtcNow;
        const int batchSize = 100;

        var expiredSubscriptions = await _db.Subscriptions
            .Where(s => s.Status == SubscriptionStatus.ACTIVE && s.CurrentPeriodEnd < now)
            .Take(batchSize)
            .ToListAsync();

        if (expiredSubscriptions.Count == 0) return;

        foreach (var subscription in expiredSubscriptions)
        {
            subscription.Status = SubscriptionStatus.EXPIRED;
            subscription.UpdatedAt = DateTime.UtcNow;

            // Sync user plan
            await SyncUserPlanFromSubscriptionAsync(subscription.UserId);
        }

        await _db.SaveChangesAsync();
    }

    public async Task SetCancelsAtPeriodEndAsync(int subscriptionId, bool cancelsAtPeriodEnd)
    {
        var subscription = await _db.Subscriptions.FindAsync(subscriptionId);
        if (subscription == null)
            throw new InvalidOperationException($"Subscription {subscriptionId} not found");

        subscription.CancelsAtPeriodEnd = cancelsAtPeriodEnd;
        subscription.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }
}
