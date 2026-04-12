using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Interfaces.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace Bookmerang.Api.Services.Implementation.Subscriptions;

public class StripeSubscriptionService(
    ISubscriptionService subscriptionService,
    AppDbContext db,
    IConfiguration config,
    ILogger<StripeSubscriptionService> logger) : IStripeSubscriptionService
{
    private readonly ISubscriptionService _subscriptionService = subscriptionService;
    private readonly AppDbContext _db = db;
    private readonly IConfiguration _config = config;
    private readonly ILogger<StripeSubscriptionService> _logger = logger;

    // ── Checkout Session ──────────────────────────────────────────────

    public async Task<string> CreateCheckoutSessionAsync(Guid userId)
    {
        var priceId = _config["Stripe:PremiumPriceId"]
            ?? throw new InvalidOperationException("Stripe:PremiumPriceId not configured");

        // Get or create a Stripe Customer
        var baseUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        var email = baseUser?.Email ?? "";
        var customerId = await GetOrCreateStripeCustomerAsync(userId, email);

        var options = new SessionCreateOptions
        {
            Customer = customerId,
            PaymentMethodTypes = new List<string> { "card" },
            LineItems = new List<SessionLineItemOptions>
            {
                new() { Price = priceId, Quantity = 1 }
            },
            Mode = "subscription",
            SuccessUrl = _config["Stripe:SuccessUrl"] ?? "https://bookmerang.app/subscription?status=success",
            CancelUrl = _config["Stripe:CancelUrl"] ?? "https://bookmerang.app/subscription?status=cancelled",
            ClientReferenceId = userId.ToString(),
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    { "bookmerang_user_id", userId.ToString() }
                }
            }
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options);

        return session.Url ?? throw new InvalidOperationException("Stripe session URL is null");
    }

    // ── Cancel Subscription ───────────────────────────────────────────

    public async Task CancelSubscriptionAsync(Guid userId)
    {
        var existing = await _subscriptionService.GetActiveSubscriptionAsync(userId);
        if (existing == null)
            throw new InvalidOperationException("No active subscription found");

        // Cancel on Stripe at period end (user retains access until then)
        if (!string.IsNullOrEmpty(existing.PlatformSubscriptionId))
        {
            try
            {
                var stripeSubService = new Stripe.SubscriptionService();
                await stripeSubService.UpdateAsync(existing.PlatformSubscriptionId,
                    new Stripe.SubscriptionUpdateOptions
                    {
                        CancelAtPeriodEnd = true
                    });
                _logger.LogInformation(
                    "Set Stripe subscription {SubId} to cancel at period end for user {UserId}",
                    existing.PlatformSubscriptionId, userId);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex,
                    "Failed to cancel Stripe subscription {SubId}, updating DB anyway",
                    existing.PlatformSubscriptionId);
            }
        }

        // Update our DB to mark cancels_at_period_end
        await _subscriptionService.SetCancelsAtPeriodEndAsync(existing.Id, true);
    }

    // ── Webhooks ──────────────────────────────────────────────────────

    public async Task HandleStripeWebhookAsync(string json, string signature)
    {
        var secret = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET")
            ?? throw new InvalidOperationException("STRIPE_WEBHOOK_SECRET not configured");

        try
        {
            var stripeEvent = EventUtility.ConstructEvent(json, signature, secret);

            _logger.LogInformation("Received Stripe event: {EventType} ({EventId})", stripeEvent.Type, stripeEvent.Id);

            switch (stripeEvent.Type)
            {
                case "checkout.session.completed":
                    await HandleCheckoutSessionCompleted(stripeEvent);
                    break;

                case "customer.subscription.created":
                case "customer.subscription.updated":
                    await HandleSubscriptionEvent(stripeEvent);
                    break;

                case "customer.subscription.deleted":
                    await HandleSubscriptionDeleted(stripeEvent);
                    break;

                default:
                    _logger.LogInformation("Ignoring Stripe event type: {EventType}", stripeEvent.Type);
                    break;
            }
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe webhook validation error");
            throw new InvalidOperationException($"Stripe webhook error: {ex.Message}", ex);
        }
    }

    // ── Webhook handlers ──────────────────────────────────────────────

    private async Task HandleCheckoutSessionCompleted(Event stripeEvent)
    {
        var session = stripeEvent.Data.Object as Session
            ?? throw new InvalidOperationException("Session object not found in event");

        _logger.LogInformation(
            "Checkout session completed: {SessionId}, ClientReferenceId: {ClientRef}, SubscriptionId: {SubId}",
            session.Id, session.ClientReferenceId, session.SubscriptionId);

        if (string.IsNullOrEmpty(session.ClientReferenceId) ||
            !Guid.TryParse(session.ClientReferenceId, out var userId))
            return;

        if (string.IsNullOrEmpty(session.SubscriptionId))
            return;

        await EnsureSubscriptionCreated(userId, session.SubscriptionId);
    }

    private async Task HandleSubscriptionEvent(Event stripeEvent)
    {
        var stripeSubscription = stripeEvent.Data.Object as Stripe.Subscription
            ?? throw new InvalidOperationException("Subscription object not found in event");

        if (string.IsNullOrEmpty(stripeSubscription.Id)) return;

        var userId = ExtractUserId(stripeSubscription);
        if (userId == null) return;

        // Ensure subscription exists or is created
        await EnsureSubscriptionCreated(userId.Value, stripeSubscription.Id, stripeSubscription);

        // Sync CancelsAtPeriodEnd flag from Stripe
        var existingSubscription = await _subscriptionService.GetActiveSubscriptionAsync(userId.Value);
        if (existingSubscription != null && stripeSubscription.CancelAtPeriodEnd != existingSubscription.CancelsAtPeriodEnd)
        {
            await _subscriptionService.SetCancelsAtPeriodEndAsync(
                existingSubscription.Id,
                stripeSubscription.CancelAtPeriodEnd
            );
            _logger.LogInformation(
                "Updated CancelsAtPeriodEnd to {Value} for subscription {SubId}",
                stripeSubscription.CancelAtPeriodEnd, existingSubscription.Id);
        }
    }

    private async Task HandleSubscriptionDeleted(Event stripeEvent)
    {
        var stripeSubscription = stripeEvent.Data.Object as Stripe.Subscription
            ?? throw new InvalidOperationException("Subscription object not found in event");

        var userId = ExtractUserId(stripeSubscription);
        if (userId == null) return;

        var existingSubscription = await _subscriptionService.GetActiveSubscriptionAsync(userId.Value);
        if (existingSubscription != null)
        {
            await _subscriptionService.UpdateSubscriptionStatusAsync(
                existingSubscription.Id,
                SubscriptionStatus.CANCELLED
            );
            _logger.LogInformation("Subscription cancelled for user {UserId}", userId);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private async Task EnsureSubscriptionCreated(Guid userId, string stripeSubscriptionId, Stripe.Subscription? stripeSub = null)
    {
        var existing = await _subscriptionService.GetActiveSubscriptionAsync(userId);
        if (existing != null)
        {
            _logger.LogInformation("User {UserId} already has active subscription, skipping create", userId);
            return;
        }

        if (stripeSub == null)
        {
            var stripeSubService = new Stripe.SubscriptionService();
            stripeSub = await stripeSubService.GetAsync(stripeSubscriptionId,
                new Stripe.SubscriptionGetOptions
                {
                    Expand = new List<string> { "items.data" }
                });
        }

        var firstItem = stripeSub.Items?.Data?.FirstOrDefault();
        var periodStart = firstItem?.CurrentPeriodStart ?? DateTime.UtcNow;
        var periodEnd = firstItem?.CurrentPeriodEnd ?? DateTime.UtcNow.AddMonths(1);

        await _subscriptionService.CreateSubscriptionAsync(
            userId,
            SubscriptionPlatform.STRIPE,
            stripeSubscriptionId,
            null,
            periodStart,
            periodEnd
        );

        _logger.LogInformation("Subscription created for user {UserId} (period: {Start} - {End})",
            userId, periodStart, periodEnd);
    }

    private async Task<string> GetOrCreateStripeCustomerAsync(Guid userId, string email)
    {
        var customerService = new CustomerService();

        var searchResult = await customerService.SearchAsync(new CustomerSearchOptions
        {
            Query = $"metadata['bookmerang_user_id']:'{userId}'"
        });

        if (searchResult.Data.Count > 0)
            return searchResult.Data[0].Id;

        var customer = await customerService.CreateAsync(new CustomerCreateOptions
        {
            Email = email,
            Metadata = new Dictionary<string, string>
            {
                { "bookmerang_user_id", userId.ToString() }
            }
        });

        return customer.Id;
    }

    private static Guid? ExtractUserId(Stripe.Subscription stripeSubscription)
    {
        if (stripeSubscription.Metadata != null &&
            stripeSubscription.Metadata.TryGetValue("bookmerang_user_id", out var userIdStr) &&
            Guid.TryParse(userIdStr, out var userId))
        {
            return userId;
        }

        return null;
    }
}
