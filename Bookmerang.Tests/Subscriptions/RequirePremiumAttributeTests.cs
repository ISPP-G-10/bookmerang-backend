using Bookmerang.Api.Attributes;
using Bookmerang.Api.Data;
using Bookmerang.Api.Exceptions;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using System.Security.Claims;
using Xunit;

namespace Bookmerang.Tests.Subscriptions;

public class RequirePremiumAttributeTests : IAsyncLifetime
{
    private AppDbContext _db = null!;
    private RequirePremiumAttribute _attribute = null!;

    public Task InitializeAsync()
    {
        _db = DbContextFactory.CreateInMemory();
        _attribute = new RequirePremiumAttribute();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    private static Point MakePoint() =>
        new GeometryFactory(new PrecisionModel(), 4326).CreatePoint(new Coordinate(0, 0));

    private async Task<(Guid userId, string supabaseId)> SeedUser(PricingPlan plan)
    {
        var userId = Guid.NewGuid();
        var supabaseId = $"sup-{userId}";
        _db.Users.Add(new BaseUser
        {
            Id = userId,
            SupabaseId = supabaseId,
            Email = $"{userId}@test.com",
            Username = $"user_{userId}",
            Name = "Test",
            Location = MakePoint()
        });
        _db.RegularUsers.Add(new User { Id = userId, Plan = plan });
        await _db.SaveChangesAsync();
        return (userId, supabaseId);
    }

    private AuthorizationFilterContext BuildContext(string? supabaseId)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_db);
        var serviceProvider = services.BuildServiceProvider();

        var claims = supabaseId != null
            ? new[] { new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", supabaseId) }
            : Array.Empty<Claim>();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider,
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
        };

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());

        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    // ── Allow / Deny ──────────────────────────────────────────────────────

    [Fact]
    public async Task PremiumUser_DoesNotThrow()
    {
        var (_, supabaseId) = await SeedUser(PricingPlan.PREMIUM);
        var context = BuildContext(supabaseId);

        // Should complete without throwing
        await _attribute.OnAuthorizationAsync(context);
    }

    [Fact]
    public async Task FreeUser_ThrowsForbiddenException()
    {
        var (_, supabaseId) = await SeedUser(PricingPlan.FREE);
        var context = BuildContext(supabaseId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _attribute.OnAuthorizationAsync(context));
    }

    [Fact]
    public async Task MissingClaim_ThrowsForbiddenException()
    {
        var context = BuildContext(null);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _attribute.OnAuthorizationAsync(context));
    }

    [Fact]
    public async Task UnknownSupabaseId_ThrowsForbiddenException()
    {
        // No user seeded — unknown ID
        var context = BuildContext("completely-unknown-supabase-id");

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _attribute.OnAuthorizationAsync(context));
    }

    [Fact]
    public async Task BaseUserExistsButNoRegularUser_ThrowsForbiddenException()
    {
        // Only BaseUser seeded, no RegularUser (User) record
        var userId = Guid.NewGuid();
        var supabaseId = $"sup-{userId}";
        _db.Users.Add(new BaseUser
        {
            Id = userId,
            SupabaseId = supabaseId,
            Email = $"{userId}@test.com",
            Username = $"user_{userId}",
            Name = "Test",
            Location = MakePoint()
        });
        await _db.SaveChangesAsync();

        var context = BuildContext(supabaseId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _attribute.OnAuthorizationAsync(context));
    }

    [Fact]
    public async Task PremiumUser_AfterPlanDowngrade_ThrowsForbiddenException()
    {
        var (userId, supabaseId) = await SeedUser(PricingPlan.PREMIUM);

        // Downgrade the user to FREE
        var user = await _db.RegularUsers.FindAsync(userId);
        user!.Plan = PricingPlan.FREE;
        await _db.SaveChangesAsync();

        var context = BuildContext(supabaseId);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _attribute.OnAuthorizationAsync(context));
    }
}
