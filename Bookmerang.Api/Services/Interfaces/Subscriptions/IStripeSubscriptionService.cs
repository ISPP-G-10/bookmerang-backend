namespace Bookmerang.Api.Services.Interfaces.Subscriptions;

public interface IStripeSubscriptionService
{
    /// <summary>
    /// Creates a Stripe Checkout session and returns the URL
    /// </summary>
    Task<string> CreateCheckoutSessionAsync(Guid userId);

    /// <summary>
    /// Cancels the user's active Stripe subscription
    /// </summary>
    Task CancelSubscriptionAsync(Guid userId);

    /// <summary>
    /// Processes Stripe webhooks (subscription events)
    /// </summary>
    Task HandleStripeWebhookAsync(string json, string signature);

    /// <summary>
    /// Queries the Stripe API directly to sync the user's active subscription into the DB.
    /// Used as a fallback when webhooks are unavailable (e.g. local dev).
    /// </summary>
    Task SyncSubscriptionFromStripeAsync(Guid userId);
}
