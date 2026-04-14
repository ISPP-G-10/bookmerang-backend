using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Services.Interfaces.Subscriptions;

public interface ISubscriptionService
{
    /// <summary>
    /// Checks if a user is premium by consulting the cached User.Plan field
    /// </summary>
    Task<bool> IsPremiumAsync(Guid userId);

    /// <summary>
    /// Checks whether a user (regular or bookdrop) has an ACTIVE non-expired subscription.
    /// </summary>
    Task<bool> HasActiveSubscriptionAsync(Guid userId);

    /// <summary>
    /// Gets the active subscription for a user, if any
    /// </summary>
    Task<Subscription?> GetActiveSubscriptionAsync(Guid userId);

    /// <summary>
    /// Creates a new subscription after payment is verified
    /// </summary>
    Task<Subscription> CreateSubscriptionAsync(
        Guid userId,
        SubscriptionPlatform platform,
        string platformSubscriptionId,
        string? originalTransactionId,
        DateTime periodStart,
        DateTime periodEnd
    );

    /// <summary>
    /// Updates the status of a subscription (called by webhooks)
    /// </summary>
    Task UpdateSubscriptionStatusAsync(
        int subscriptionId,
        SubscriptionStatus newStatus,
        DateTime? newPeriodEnd = null
    );

    /// <summary>
    /// Extends a subscription by N months (used for ranking rewards)
    /// </summary>
    Task<Subscription> ExtendSubscriptionAsync(
        Guid userId,
        int months,
        SubscriptionPlatform platform
    );

    /// <summary>
    /// Recalculates User.Plan from the subscription table (source of truth)
    /// Should be called after any subscription status change
    /// </summary>
    Task SyncUserPlanFromSubscriptionAsync(Guid userId);

    /// <summary>
    /// Background job handler: finds expired subscriptions and marks them as EXPIRED
    /// </summary>
    Task HandleExpirationCheckAsync();

    /// <summary>
    /// Sets the CancelsAtPeriodEnd flag for a subscription
    /// </summary>
    Task SetCancelsAtPeriodEndAsync(int subscriptionId, bool cancelsAtPeriodEnd);
}
