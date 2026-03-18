using Bookmerang.Api.Models.DTOs.Communities;

namespace Bookmerang.Api.Services.Interfaces.Communities;

public interface ICommunityLibraryService
{
    Task<List<CommunityLibraryBookDto>> GetCommunityLibraryAsync(Guid userId, int communityId, int page = 1, int pageSize = 20);
    Task ToggleLikeAsync(Guid userId, int communityId, int bookId);
    Task<List<CommunityLibraryBookDto>> GetSuggestedBooksForMeetupAsync(Guid userId, int communityId);
}