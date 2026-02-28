using Bookmerang.Api.Configuration;
using Bookmerang.Api.Data;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Interfaces.Matcher;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Bookmerang.Api.Services.Implementation.Matcher;

public class MatcherService(AppDbContext db, IOptions<MatcherSettings> settings) : IMatcherService
{
    private readonly AppDbContext _db = db;
    private readonly MatcherSettings _settings = settings.Value;

    public async Task<List<FeedBookDto>> GetFeedAsync(int userId, int page, int pageSize)
    {   
        // 1. Obtenemos las preferencias del usuario
        var prefs = await GetUserPreferencesAsync(userId);

        //2. Obtenemos los usuarios que han dado swipe a uno de nuestros libros en un plazo de 30 días
        var interestedUserIds = GetInterestedUserIds(userId);

        // 3. Obtenemos el ratio de intercalado de libros de la pool 1 (P1) y la pool 2 (P2)
        var ratio = ValidatePriorityToDiscoveryRatio();

        // 4. Calculamos el offset, es decir, cuántos elementos hay que saltarse para devolver los elementos correspondientes a una página concreta
        var skip = page * pageSize; // Si page=1 y pageSize=8, tenemos que saltar los 8 primeros para devolver los libros de page=1

        // 5. Configuramos la cantidad de libros a traer de cada pool, asegurando que hay suficientes de cada pool para cubrir el caso donde uno esté vacío
        // Nos tenemos que traer los anteriores más la cantidad necesaria de la página, sin los anteriores no tienes como cortar
        // Te podrías traer skip + pageSize/2 para cada uno, pero mejor traer más para asegurar que hay suficientes por página
        var fetchCount = skip + pageSize;

        // 6. Nos traemos el pool p1 
        var p1Books = await GetPriorityBooks(userId, prefs, interestedUserIds)
            .Take(fetchCount)
            .ToListAsync();

        // 7. Nos traemos el pool p2
        var p2Books = await GetDiscoveryBooks(userId, prefs, interestedUserIds)
            .Take(fetchCount)
            .ToListAsync();

        // 8. Intercalemos los pool con el ratio configurado
        var interleaved = InterleaveBooks(p1Books, p2Books, ratio);

        // 9. Devolvemos el resultado cortando la lista
        return [.. interleaved.Skip(skip).Take(pageSize)];
    }

    public async Task<SwipeResultDto> ProcessSwipeAsync(int userId, int bookId, SwipeDirection direction)
    {
        // 1. Validar que el libro existe y sigue PUBLISHED
        var book = await ValidateBookIsPublishedAsync(bookId);
        if (book == null)
            return new SwipeResultDto { Outcome = SwipeOutcome.BookUnavailable };

        // 2. Registrar el swipe (SwiperId+BookId ya previene duplicados)
        _db.Swipes.Add(new Swipe
        {
            SwiperId = userId,
            BookId = bookId,
            Direction = direction,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        // 3. Si es LEFT, no hay más que hacer
        if (direction == SwipeDirection.LEFT)
            return new SwipeResultDto { Outcome = SwipeOutcome.Recorded };

        // 4. Si es RIGHT, verificar match bilateral:
        //    ¿El owner del libro ya hizo swipe RIGHT a algún libro MÍO (en los últimos 30 días)?
        var mutualSwipe = await FindMutualSwipeAsync(userId, book.OwnerId);
        if (mutualSwipe == null)
            return new SwipeResultDto { Outcome = SwipeOutcome.Recorded };

        // 5. Match bilateral detectado → crear Match + Chat + Exchange (S-9 pendiente)
        // TODO: Implementar creación atómica de Match + Chat + Exchange con pg_advisory_xact_lock
        return new SwipeResultDto { Outcome = SwipeOutcome.Recorded };
    }

    /// <summary>
    /// Detecta match bilateral: verifica que otherUserId esté entre los usuarios
    /// interesados (reutiliza GetInterestedUserIds con filtro temporal) y devuelve
    /// el swipe RIGHT concreto hacia un libro del usuario actual.
    /// Devuelve null si no hay match bilateral.
    /// </summary>
    private async Task<Swipe?> FindMutualSwipeAsync(int userId, int otherUserId)
    {
        var isInterested = await GetInterestedUserIds(userId)
            .AnyAsync(id => id == otherUserId);

        if (!isInterested)
            return null;

        return await _db.Swipes
            .Where(s => s.SwiperId == otherUserId)
            .Where(s => s.Direction == SwipeDirection.RIGHT)
            .Where(s => _db.Books.Any(b => b.Id == s.BookId && b.OwnerId == userId))
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Valida que el libro existe y tiene status PUBLISHED.
    /// Devuelve el libro si es válido, null si no está PUBLISHED.
    /// Lanza KeyNotFoundException si el libro no existe.
    /// </summary>
    private async Task<Book?> ValidateBookIsPublishedAsync(int bookId)
    {
        var book = await _db.Books.FirstOrDefaultAsync(b => b.Id == bookId)
            ?? throw new KeyNotFoundException($"No se encontró el libro con ID {bookId}.");

        return book.Status == BookStatus.PUBLISHED ? book : null;
    }

    /// <summary>
    /// Valida que el ratio de intercalado P1:P2 sea mayor que 0.
    /// Un ratio <= 0 provocaría un bucle infinito en el intercalado.
    /// </summary>
    private int ValidatePriorityToDiscoveryRatio()
    {
        var ratio = _settings.Feed.PriorityToDiscoveryRatio;
        return ratio > 0
            ? ratio
            : throw new InvalidOperationException(
                "Matcher:Feed:PriorityToDiscoveryRatio debe ser mayor que 0.");
    }

    /// <summary>
    /// Obtiene las preferencias del usuario desde la BD.
    /// Lanza InvalidOperationException si no están configuradas.
    /// </summary>
    private async Task<UserPreference> GetUserPreferencesAsync(int userId)
    {
        return await _db.UserPreferences
            .FirstOrDefaultAsync(up => up.UserId == userId)
            ?? throw new InvalidOperationException(
                "El usuario debe configurar sus preferencias antes de usar el feed.");
    }

    /// <summary>
    /// Intercala libros P1 y P2 con el ratio configurado.
    /// Con ratio=3: [P1, P1, P1, P2, P1, P1, P1, P2, ...].
    /// Si un pool se agota, se rellena con el otro.
    /// </summary>
    private static List<FeedBookDto> InterleaveBooks(
        List<FeedBookDto> p1, List<FeedBookDto> p2, int ratio)
    {
        var result = new List<FeedBookDto>(p1.Count + p2.Count);
        int i1 = 0, i2 = 0;

        while (i1 < p1.Count || i2 < p2.Count)
        {
            for (var r = 0; r < ratio && i1 < p1.Count; r++)
                result.Add(p1[i1++]);

            if (i2 < p2.Count)
                result.Add(p2[i2++]);
        }

        return result;
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
