using Bookmerang.Api.Controllers.Subscriptions;
using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Interfaces.Subscriptions;
using Bookmerang.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NetTopologySuite.Geometries;
using System.Security.Claims;
using Xunit;

namespace Bookmerang.Tests.Subscriptions;

public class SubscriptionControllerTests : IAsyncLifetime
{
    private AppDbContext _db = null!;
    private Mock<ISubscriptionService> _subscriptionService = null!;
    private Mock<IStripeSubscriptionService> _stripeService = null!;
    private SubscriptionsController _controller = null!;

    private static readonly string TestSupabaseId = "test-supabase-id";
    private Guid _userId;

    public async Task InitializeAsync()
    {
        _db = DbContextFactory.CreateInMemory();
        _subscriptionService = new Mock<ISubscriptionService>();
        _stripeService = new Mock<IStripeSubscriptionService>();
        _subscriptionService
            .Setup(s => s.HasActiveSubscriptionAsync(It.IsAny<Guid>()))
            .ReturnsAsync(false);

        _controller = new SubscriptionsController(
            _subscriptionService.Object,
            _stripeService.Object,
            _db,
            NullLogger<SubscriptionsController>.Instance);

        // Seed a user so GetCurrentUserId() can resolve the Supabase ID
        _userId = Guid.NewGuid();
        _db.Users.Add(new BaseUser
        {
            Id = _userId,
            SupabaseId = TestSupabaseId,
            Email = "test@test.com",
            Username = "testuser",
            Name = "Test User",
            Location = new GeometryFactory(new PrecisionModel(), 4326).CreatePoint(new Coordinate(0, 0))
        });
        _db.RegularUsers.Add(new User { Id = _userId, Plan = PricingPlan.FREE });
        await _db.SaveChangesAsync();

        SetControllerUser(TestSupabaseId);
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    private void SetControllerUser(string? supabaseId)
    {
        var claims = supabaseId != null
            ? new[] { new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", supabaseId) }
            : Array.Empty<Claim>();

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };
    }

    // ── GET /status ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetStatus_NoActiveSub_ReturnsFreeUserStatus()
    {
        _subscriptionService.Setup(s => s.GetActiveSubscriptionAsync(_userId)).ReturnsAsync((Subscription?)null);
        _subscriptionService.Setup(s => s.IsPremiumAsync(_userId)).ReturnsAsync(false);

        var result = await _controller.GetSubscriptionStatus();

        var ok = Assert.IsType<OkObjectResult>(result);
        dynamic value = ok.Value!;
        Assert.False((bool)value.isPremium);
        Assert.Null(value.subscription);
    }

    [Fact]
    public async Task GetStatus_ActiveSub_ReturnsPremiumUserStatus()
    {
        var sub = new Subscription
        {
            Id = 1,
            UserId = _userId,
            Platform = SubscriptionPlatform.STRIPE,
            Status = SubscriptionStatus.ACTIVE,
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-10),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(20),
            CancelsAtPeriodEnd = false
        };
        _subscriptionService.Setup(s => s.GetActiveSubscriptionAsync(_userId)).ReturnsAsync(sub);
        _subscriptionService.Setup(s => s.IsPremiumAsync(_userId)).ReturnsAsync(true);

        var result = await _controller.GetSubscriptionStatus();

        var ok = Assert.IsType<OkObjectResult>(result);
        dynamic value = ok.Value!;
        Assert.True((bool)value.isPremium);
        Assert.NotNull(value.subscription);
    }

    [Fact]
    public async Task GetStatus_MissingJwtClaim_ReturnsUnauthorized()
    {
        SetControllerUser(null);

        var result = await _controller.GetSubscriptionStatus();

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetStatus_UnknownSupabaseId_ReturnsUnauthorized()
    {
        SetControllerUser("unknown-supabase-id");

        var result = await _controller.GetSubscriptionStatus();

        Assert.IsType<UnauthorizedResult>(result);
    }

    // ── POST /checkout ────────────────────────────────────────────────────

    [Fact]
    public async Task Checkout_FreeUser_ReturnsCheckoutUrl()
    {
        _subscriptionService.Setup(s => s.IsPremiumAsync(_userId)).ReturnsAsync(false);
        _stripeService.Setup(s => s.CreateCheckoutSessionAsync(_userId)).ReturnsAsync("https://checkout.stripe.com/pay/test");

        var result = await _controller.CreateCheckoutSession();

        var ok = Assert.IsType<OkObjectResult>(result);
        dynamic value = ok.Value!;
        Assert.Equal("https://checkout.stripe.com/pay/test", (string)value.checkoutUrl);
    }

    [Fact]
    public async Task Checkout_PremiumUser_ReturnsBadRequest()
    {
        _subscriptionService.Setup(s => s.IsPremiumAsync(_userId)).ReturnsAsync(true);

        var result = await _controller.CreateCheckoutSession();

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(bad.Value);
    }

    [Fact]
    public async Task Checkout_MissingJwtClaim_ReturnsUnauthorized()
    {
        SetControllerUser(null);

        var result = await _controller.CreateCheckoutSession();

        Assert.IsType<UnauthorizedResult>(result);
    }

    // â”€â”€ POST /checkout/bookdrop â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task BookdropCheckout_NoActiveSubscription_ReturnsCheckoutUrl()
    {
        _subscriptionService.Setup(s => s.HasActiveSubscriptionAsync(_userId)).ReturnsAsync(false);
        _stripeService.Setup(s => s.CreateBookdropCheckoutSessionAsync(_userId))
            .ReturnsAsync("https://checkout.stripe.com/pay/bookdrop");

        var result = await _controller.CreateBookdropCheckoutSession();

        var ok = Assert.IsType<OkObjectResult>(result);
        dynamic value = ok.Value!;
        Assert.Equal("https://checkout.stripe.com/pay/bookdrop", (string)value.checkoutUrl);
    }

    [Fact]
    public async Task BookdropCheckout_WithActiveSubscription_ReturnsBadRequest()
    {
        _subscriptionService.Setup(s => s.HasActiveSubscriptionAsync(_userId)).ReturnsAsync(true);

        var result = await _controller.CreateBookdropCheckoutSession();

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ── POST /cancel ──────────────────────────────────────────────────────

    [Fact]
    public async Task Cancel_ActiveSubscription_ReturnsSuccess()
    {
        _stripeService.Setup(s => s.CancelSubscriptionAsync(_userId)).Returns(Task.CompletedTask);

        var result = await _controller.CancelSubscription();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        _stripeService.Verify(s => s.CancelSubscriptionAsync(_userId), Times.Once);
    }

    [Fact]
    public async Task Cancel_NoActiveSubscription_ReturnsBadRequest()
    {
        _stripeService.Setup(s => s.CancelSubscriptionAsync(_userId))
            .ThrowsAsync(new InvalidOperationException("No active subscription found"));

        var result = await _controller.CancelSubscription();

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Cancel_MissingJwtClaim_ReturnsUnauthorized()
    {
        SetControllerUser(null);

        var result = await _controller.CancelSubscription();

        Assert.IsType<UnauthorizedResult>(result);
    }
}
