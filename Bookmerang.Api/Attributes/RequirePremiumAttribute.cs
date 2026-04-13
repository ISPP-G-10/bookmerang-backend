using Bookmerang.Api.Data;
using Bookmerang.Api.Exceptions;
using Bookmerang.Api.Models.Enums;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace Bookmerang.Api.Attributes;

/// <summary>
/// Authorization filter that requires the user to have a PREMIUM subscription.
/// Must be applied to endpoints that require [Authorize] first.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequirePremiumAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // Get the Supabase ID from JWT claims
        var supabaseIdClaim = context.HttpContext.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");

        if (supabaseIdClaim?.Value == null)
        {
            throw new ForbiddenException("User ID not found in claims");
        }

        // Resolve the backend user ID from the database
        var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var baseUser = await dbContext.Users
            .FirstOrDefaultAsync(u => u.SupabaseId == supabaseIdClaim.Value);

        if (baseUser == null)
        {
            throw new ForbiddenException("User not found");
        }

        var user = await dbContext.RegularUsers.FindAsync(baseUser.Id);

        if (user == null || user.Plan != PricingPlan.PREMIUM)
        {
            throw new ForbiddenException("This feature requires a premium subscription");
        }
    }
}
