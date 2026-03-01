using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Services.Interfaces.Matcher;

public interface IMatcherService
{
    Task<List<FeedBookDto>> GetFeedAsync(Guid userId, int page, int pageSize);
    Task<SwipeResultDto> ProcessSwipeAsync(Guid userId, int bookId, SwipeDirection direction);
}
