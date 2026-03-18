using Bookmerang.Api.Models.DTOs.Communities;

namespace Bookmerang.Api.Services.Interfaces.Communities;

public interface ICommunityService
{
    Task<List<CommunityDto>> ExploreCommunitiesAsync(Guid userId, double latitude, double longitude, int radiusKm = 50);
    Task<CommunityDto> CreateCommunityAsync(Guid creatorId, CreateCommunityRequest request);
    Task<CommunityDto> JoinCommunityAsync(Guid userId, int communityId);
    Task LeaveCommunityAsync(Guid userId, int communityId);
    Task DeleteCommunityAsync(Guid userId, int communityId);
    Task<CommunityDto> GetCommunityDetailsAsync(int communityId);
    Task<List<CommunityDto>> GetMyCommunitiesAsync(Guid userId);
}