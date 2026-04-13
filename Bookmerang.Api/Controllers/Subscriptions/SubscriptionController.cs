using Bookmerang.Api.Data;
using Bookmerang.Api.Services.Interfaces.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bookmerang.Api.Controllers.Subscriptions;

[ApiController]
[Route("api/[controller]")]
public class SubscriptionsController(
    ISubscriptionService subscriptionService,
    IStripeSubscriptionService stripeSubscriptionService,
    AppDbContext db,
    ILogger<SubscriptionsController> logger) : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService = subscriptionService;
    private readonly IStripeSubscriptionService _stripeSubscriptionService = stripeSubscriptionService;
    private readonly AppDbContext _db = db;
    private readonly ILogger<SubscriptionsController> _logger = logger;

    /// <summary>
    /// Get the current subscription status for the authenticated user
    /// </summary>
    [Authorize]
    [HttpGet("status")]
    public async Task<IActionResult> GetSubscriptionStatus()
    {
        try
        {
            var userId = await GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var subscription = await _subscriptionService.GetActiveSubscriptionAsync(userId.Value);
            var isPremium = await _subscriptionService.IsPremiumAsync(userId.Value);

            var response = new
            {
                isPremium,
                subscription = subscription != null ? new
                {
                    subscription.Id,
                    platform = subscription.Platform.ToString(),
                    status = subscription.Status.ToString(),
                    periodEnd = subscription.CurrentPeriodEnd,
                    cancelsAtPeriodEnd = subscription.CancelsAtPeriodEnd
                } : null
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting subscription status");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Cancel the user's active subscription
    /// </summary>
    [Authorize]
    [HttpPost("cancel")]
    public async Task<IActionResult> CancelSubscription()
    {
        try
        {
            var userId = await GetCurrentUserId();
            if (userId == null) return Unauthorized();

            await _stripeSubscriptionService.CancelSubscriptionAsync(userId.Value);

            return Ok(new { success = true, message = "Subscription cancelled" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling subscription");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Create a Stripe Checkout session (web fallback)
    /// </summary>
    [Authorize]
    [HttpPost("checkout")]
    public async Task<IActionResult> CreateCheckoutSession()
    {
        try
        {
            var userId = await GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var isPremium = await _subscriptionService.IsPremiumAsync(userId.Value);
            if (isPremium)
                return BadRequest(new { error = "User is already premium" });

            var checkoutUrl = await _stripeSubscriptionService.CreateCheckoutSessionAsync(userId.Value);

            return Ok(new { checkoutUrl });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating checkout session");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Webhook endpoint for Stripe events
    /// </summary>
    [AllowAnonymous]
    [HttpPost("webhooks/stripe")]
    public async Task<IActionResult> HandleStripeWebhook()
    {
        try
        {
            var json = await new StreamReader(Request.Body).ReadToEndAsync();
            var signature = Request.Headers["Stripe-Signature"].ToString();

            await _stripeSubscriptionService.HandleStripeWebhookAsync(json, signature);

            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid Stripe webhook");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Stripe webhook");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Syncs subscription state directly from the Stripe API.
    /// Useful when webhooks are unavailable (e.g. local dev).
    /// </summary>
    [Authorize]
    [HttpPost("sync")]
    public async Task<IActionResult> SyncSubscription()
    {
        try
        {
            var userId = await GetCurrentUserId();
            if (userId == null) return Unauthorized();

            await _stripeSubscriptionService.SyncSubscriptionFromStripeAsync(userId.Value);

            var subscription = await _subscriptionService.GetActiveSubscriptionAsync(userId.Value);
            var isPremium = await _subscriptionService.IsPremiumAsync(userId.Value);

            var response = new
            {
                isPremium,
                subscription = subscription != null ? new
                {
                    subscription.Id,
                    platform = subscription.Platform.ToString(),
                    status = subscription.Status.ToString(),
                    periodEnd = subscription.CurrentPeriodEnd,
                    cancelsAtPeriodEnd = subscription.CancelsAtPeriodEnd
                } : null
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing subscription from Stripe");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Resolves Supabase ID from JWT → backend User.Id
    /// </summary>
    private async Task<Guid?> GetCurrentUserId()
    {
        var supabaseId = User.FindFirst(
            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

        if (supabaseId == null) return null;

        var baseUser = await _db.Users.FirstOrDefaultAsync(u => u.SupabaseId == supabaseId);
        return baseUser?.Id;
    }
}
