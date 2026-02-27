using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Services.Interfaces.Matcher;

public interface IMatcherService
{
    Task<List<FeedBookDto>> GetFeedAsync(int userId, int page, int pageSize);
    Task<SwipeResultDto> ProcessSwipeAsync(int userId, int bookId, SwipeDirection direction);
}
