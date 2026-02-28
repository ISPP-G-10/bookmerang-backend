using Bookmerang.Api.Configuration;
using Bookmerang.Api.Data;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Models.Entities;
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

    /// <summary>
    /// P1 — Libros de usuarios que ya mostraron interés en los libros del usuario actual.
    /// Filtrados por: status PUBLISHED, dentro del radio, no swipeados ya, no propios.
    /// Solo incluye libros cuyos owners están en interestedUserIds.
    /// Ordenados por score descendente.
    /// </summary>
    private IQueryable<FeedBookDto> GetPriorityBooks(
        int userId, UserPreference prefs, IQueryable<int> interestedUserIds)
    {
        var candidates = GetBaseCandidates(userId, prefs)
            .Where(b => interestedUserIds.Contains(b.OwnerId));

        return ProjectWithScore(candidates, prefs, isPriority: true);
    }

    /// <summary>
    /// P2 — Libros de descubrimiento: libros dentro del radio del usuario,
    /// excluyendo los de P1 (interested_users) y los ya swipeados.
    /// Ordenados por score descendente.
    /// </summary>
    private IQueryable<FeedBookDto> GetDiscoveryBooks(
        int userId, UserPreference prefs, IQueryable<int> interestedUserIds)
    {
        var candidates = GetBaseCandidates(userId, prefs)
            .Where(b => !interestedUserIds.Contains(b.OwnerId));

        return ProjectWithScore(candidates, prefs, isPriority: false);
    }

    /// <summary>
    /// Filtros base compartidos por P1 y P2: status PUBLISHED, no propios,
    /// no swipeados ya y dentro del radio del usuario.
    /// </summary>
    private IQueryable<Book> GetBaseCandidates(int userId, UserPreference prefs)
    {
        return _db.Books
            .Where(b => b.Status == BookStatus.PUBLISHED)
            .Where(b => b.OwnerId != userId)
            .Where(b => !_db.Swipes.Any(s => s.SwiperId == userId && s.BookId == b.Id))
            .Where(b => _db.BaseUsers
                .Any(bu => bu.Id == b.OwnerId
                    && bu.Location.IsWithinDistance(prefs.Location, prefs.RadioKm * 1000.0)));
    }

    /// <summary>
    /// Calcula el score de cada libro candidato y proyecta a FeedBookDto.
    /// Componentes del score: genre_match, extension_match, distance_score, recency_bonus.
    /// </summary>
    private IQueryable<FeedBookDto> ProjectWithScore(
        IQueryable<Book> candidates, UserPreference prefs, bool isPriority)
    {
        var w = _settings.Weights;
        var decayDays = (double)_settings.Feed.RecencyDecayDays;
        var now = DateTime.UtcNow;

        var preferredGenreIds = _db.UserPreferenceGenres
            .Where(upg => upg.PreferencesId == prefs.Id)
            .Select(upg => upg.GenreId);

        return candidates
            .Select(b => new
            {
                Book = b,
                OwnerUsername = _db.BaseUsers
                    .Where(bu => bu.Id == b.OwnerId)
                    .Select(bu => bu.Username)
                    .FirstOrDefault()!,
                GenreMatch = b.BookGenres.Any(bg => preferredGenreIds.Contains(bg.GenreId))
                    ? 1.0 : 0.0,
                ExtensionMatch = b.NumPaginas != null
                    && ((prefs.Extension == BooksExtension.SHORT && b.NumPaginas <= 200)
                     || (prefs.Extension == BooksExtension.MEDIUM && b.NumPaginas > 200 && b.NumPaginas <= 400)
                     || (prefs.Extension == BooksExtension.LONG && b.NumPaginas > 400))
                    ? 1.0 : 0.0,
                Distance = _db.BaseUsers
                    .Where(bu => bu.Id == b.OwnerId)
                    .Select(bu => bu.Location.Distance(prefs.Location))
                    .FirstOrDefault(),
                DaysSincePublished = (now - b.CreatedAt!.Value).TotalDays
            })
            .Select(x => new
            {
                x.Book,
                x.OwnerUsername,
                Score = w.GenreMatch * x.GenreMatch
                      + w.ExtensionMatch * x.ExtensionMatch
                      + w.DistanceScore * (1.0 - x.Distance / (prefs.RadioKm * 1000.0))
                      + w.RecencyBonus * (1.0 / (1.0 + x.DaysSincePublished / decayDays))
            })
            .OrderByDescending(x => x.Score)
            .Select(x => new FeedBookDto
            {
                Id = x.Book.Id,
                OwnerId = x.Book.OwnerId,
                OwnerUsername = x.OwnerUsername,
                Titulo = x.Book.Titulo,
                Autor = x.Book.Autor,
                Editorial = x.Book.Editorial,
                NumPaginas = x.Book.NumPaginas,
                Cover = x.Book.Cover != null ? x.Book.Cover.ToString() : null,
                Condition = x.Book.Condition != null ? x.Book.Condition.ToString() : null,
                Observaciones = x.Book.Observaciones,
                Genres = x.Book.BookGenres.Select(bg => bg.Genre.Name).ToList(),
                Photos = x.Book.Photos.OrderBy(p => p.Orden).Select(p => p.Url).ToList(),
                Score = x.Score,
                IsPriority = isPriority
            });
    }
}
