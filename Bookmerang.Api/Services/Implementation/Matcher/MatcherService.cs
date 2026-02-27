using Bookmerang.Api.Configuration;
using Bookmerang.Api.Data;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Interfaces.Matcher;
using Microsoft.Extensions.Options;

namespace Bookmerang.Api.Services.Implementation.Matcher;

public class MatcherService(AppDbContext db, IOptions<MatcherSettings> settings) : IMatcherService
{
    private readonly AppDbContext _db = db;
    private readonly MatcherSettings _settings = settings.Value;

    public Task<List<FeedBookDto>> GetFeedAsync(int userId, int page, int pageSize)
    {
        throw new NotImplementedException();
    }

    public Task<SwipeResultDto> ProcessSwipeAsync(int userId, int bookId, SwipeDirection direction)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Obtiene los IDs de usuarios que dieron swipe RIGHT a libros del usuario actual
    /// en los últimos SwipeValidDays días.
    /// Los libros ya intercambiados no aparecen porque su status deja de ser PUBLISHED,
    /// pero el usuario sí puede volver a aparecer con otros libros.
    /// </summary>
    private IQueryable<int> GetInterestedUserIds(int userId)
    {
        var cutoff = DateTime.UtcNow.AddDays(-_settings.Feed.SwipeValidDays);

        return _db.Swipes
            .Where(s => s.Direction == SwipeDirection.RIGHT)
            .Where(s => s.CreatedAt >= cutoff)
            .Where(s => _db.Books.Any(b => b.Id == s.BookId && b.OwnerId == userId))
            .Select(s => s.SwiperId)
            .Distinct();
    }
}
