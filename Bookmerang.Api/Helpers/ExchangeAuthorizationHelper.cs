using System.Security.Claims;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Helpers;

public static class ExchangeAuthorizationHelper
{
    public static bool IsAdminOrExchangeMember(ClaimsPrincipal user, Guid userId, Exchange exchange)
    {
        var isAdmin = user.HasClaim("user_type", ((int)BaseUserType.ADMIN).ToString());
        var isMember = exchange.Match is not null &&
                       (exchange.Match.User1Id == userId || exchange.Match.User2Id == userId);
        return isAdmin || isMember;
    }
}
