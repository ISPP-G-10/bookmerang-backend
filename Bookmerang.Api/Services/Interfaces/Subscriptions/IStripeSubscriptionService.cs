namespace Bookmerang.Api.Services.Interfaces.Subscriptions;

public interface IStripeSubscriptionService
{
    /// <summary>
    /// Returns true when Stripe is configured for mandatory Bookdrop registration payments.
    /// </summary>
    bool IsBookdropPaymentEnabled();

    /// <summary>
    /// Creates a Stripe Checkout session and returns the URL
    /// </summary>
    Task<string> CreateCheckoutSessionAsync(Guid userId);

    /// <summary>
    /// Creates a Stripe Checkout session for the Bookdrop monthly subscription.
    /// </summary>
    Task<string> CreateBookdropCheckoutSessionAsync(Guid userId);

    /// <summary>
    /// Creates a Stripe Checkout session used before creating a Bookdrop account.
    /// </summary>
    Task<string> CreateBookdropRegistrationCheckoutSessionAsync(string email);

    /// <summary>
    /// Validates a Bookdrop registration checkout session and returns its Stripe subscription id.
    /// Returns null if the checkout is invalid, unpaid, or already used.
    /// </summary>
    Task<string?> ValidateAndGetBookdropRegistrationSubscriptionIdAsync(string checkoutSessionId, string expectedEmail);

    /// <summary>
    /// Attaches a Stripe subscription to a local user and ensures local subscription row exists.
    /// </summary>
    Task LinkBookdropSubscriptionToUserAsync(Guid userId, string stripeSubscriptionId);

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
