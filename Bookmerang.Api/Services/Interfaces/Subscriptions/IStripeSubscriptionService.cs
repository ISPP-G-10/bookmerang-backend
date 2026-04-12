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
}
