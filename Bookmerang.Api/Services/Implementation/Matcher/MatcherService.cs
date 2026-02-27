using Bookmerang.Api.Configuration;
using Bookmerang.Api.Data;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Interfaces.Matcher;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;

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
    /// P1 — Libros de usuarios que ya mostraron interés en los libros del usuario actual ( obtenidos con GetInterestedUserIds).
    /// Filtrados por: status PUBLISHED, dentro del radio, no swipeados ya, no propios.
    /// Ordenados por score descendente.
    /// </summary>
    private IQueryable<FeedBookDto> GetPriorityBooks(
        int userId, UserPreference prefs, IQueryable<int> interestedUserIds)
    {
        var w = _settings.Weights; // cargamos pesos configurados
        var decayDays = (double)_settings.Feed.RecencyDecayDays;
        var now = DateTime.UtcNow;

        // IDs de géneros preferidos del usuario
        var preferredGenreIds = _db.UserPreferenceGenres
            .Where(upg => upg.PreferencesId == prefs.Id)
            .Select(upg => upg.GenreId);

        return _db.Books
            // Filtros base
            .Where(b => b.Status == BookStatus.PUBLISHED) // solo libros disponibles
            .Where(b => b.OwnerId != userId)
            // Solo libros de usuarios interesados en mí
            .Where(b => interestedUserIds.Contains(b.OwnerId))
            // Excluir libros que ya he swipeado
            .Where(b => !_db.Swipes.Any(s => s.SwiperId == userId && s.BookId == b.Id))
            // Dentro del radio de mi radio (distancia en metros)
            .Where(b => _db.BaseUsers
                .Any(bu => bu.Id == b.OwnerId
                    && bu.Location.IsWithinDistance(prefs.Location, prefs.RadioKm * 1000.0)))
            // Proyección con score (calculamos las componentes del score para cada libro candidato)
            .Select(b => new
            {
                Book = b,
                // Nombre del dueño del libro
                OwnerUsername = _db.BaseUsers 
                    .Where(bu => bu.Id == b.OwnerId)
                    .Select(bu => bu.Username)
                    .FirstOrDefault()!,
                // 1.0 si algún género del libro coincide con los de mis preferencias
                GenreMatch = b.BookGenres.Any(bg => preferredGenreIds.Contains(bg.GenreId))
                    ? 1.0 : 0.0,
                // 1.0 si las páginas del libro encajan con mis preferencias de extensión
                ExtensionMatch = b.NumPaginas != null
                    && ((prefs.Extension == BooksExtension.SHORT && b.NumPaginas <= 200)
                     || (prefs.Extension == BooksExtension.MEDIUM && b.NumPaginas > 200 && b.NumPaginas <= 400)
                     || (prefs.Extension == BooksExtension.LONG && b.NumPaginas > 400))
                    ? 1.0 : 0.0,
                // Distancia en metros entre el owner del libro y yo
                Distance = _db.BaseUsers
                    .Where(bu => bu.Id == b.OwnerId)
                    .Select(bu => bu.Location.Distance(prefs.Location))
                    .FirstOrDefault(),
                // Días desde que se publicó el libro
                DaysSincePublished = (now - b.CreatedAt!.Value).TotalDays
            })
            // Cálculo del score final
            .Select(x => new
            {
                x.Book,
                x.OwnerUsername,
                Score = w.GenreMatch * x.GenreMatch // 0.4 x (0 o 1)
                      + w.ExtensionMatch * x.ExtensionMatch // 0.10 x (0 o 1)
                      + w.DistanceScore * (1.0 - x.Distance / (prefs.RadioKm * 1000.0)) // 0.35 * (0.0 a 1.0) -> cuánto más cerca, mayor puntuación
                      + w.RecencyBonus * (1.0 / (1.0 + x.DaysSincePublished / decayDays)) // 0.15 * (0.0 a 1.0) -> cuánto más nueva la publicación, más puntuación, para evitar que libros nuevos se acumulen
            })
            .OrderByDescending(x => x.Score) // ordenamos por scroe
            .Select(x => new FeedBookDto // devolvemos la info de cada libro candidato con su puntuación y flag de P1
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
                IsPriority = true
            });
    }
}
