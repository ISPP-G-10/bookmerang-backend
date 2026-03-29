using Bookmerang.Api.Models.DTOs;

namespace Bookmerang.Api.Services.Interfaces.Inkdrops;

public interface IInkdropsService
{
    Task<InkdropsDto> GetUserInkdropsAsync(Guid userId);
    Task GrantExchangeInkdropsAsync(Guid user1Id, Guid user2Id);
    Task<CommunityRankingDto> GetCommunityRankingAsync(Guid requestingUserId, int communityId);
}
