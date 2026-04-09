// ============================================================
// Cómo ejecutar los tests del Matcher
// Todos los tests de esta clase: dotnet test Bookmerang.Tests --filter "FullyQualifiedName~MatcherServiceBasicTests"
// Un test concreto (sin log): dotnet test Bookmerang.Tests --filter "FullyQualifiedName~GetFeed_OwnBook_IsNotReturned"
// Un test concreto (con log visible por consola): dotnet test Bookmerang.Tests --filter "FullyQualifiedName~GetFeed_OwnBook_IsNotReturned" --logger "console;verbosity=detailed"
// ============================================================

using Bookmerang.Api.Configuration;
using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Implementation.Matcher;
using Bookmerang.Api.Services.Interfaces.Chats;
using Bookmerang.Api.Services.Interfaces.ExchangeInterfaces;
using Bookmerang.Tests.Helpers;
using Bookmerang.Api.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NetTopologySuite.Geometries;
using Xunit;
using Xunit.Abstractions;

namespace Bookmerang.Tests.Matcher;

/// <summary>
/// Tests básicos de funcionalidad de MatcherService contra PostgreSQL+PostGIS real, simulando prod.
/// </summary>
public class MatcherServiceBasicTests(PostgresMatcherFixture fixture, ITestOutputHelper output) : IClassFixture<PostgresMatcherFixture>, IAsyncLifetime
{
    private readonly PostgresMatcherFixture _fixture = fixture;
    private readonly ITestOutputHelper _output = output;
    private AppDbContext _db = null!;
    private MatcherService _service = null!;

    // Coordenadas Madrid centro -> punto de referencia
    private static readonly Point Madrid = MakePoint(-3.7038, 40.4168);
    // ~1 km al este de Madrid -> dentro de cualquier radio razonable
    private static readonly Point MadridCercano = MakePoint(-3.6938, 40.4168);
    // ~100 km al este de Madrid -> fuera de un radio de 50 km
    private static readonly Point FueraDeRadio = MakePoint(-2.5, 40.4168);

    private static Point MakePoint(double lon, double lat) =>
        new GeometryFactory(new PrecisionModel(), 4326).CreatePoint(new Coordinate(lon, lat));

    private static readonly Guid UserA = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserB = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid UserC = new("cccccccc-cccc-cccc-cccc-cccccccccccc");

    public async Task InitializeAsync()
    {
        _db      = _fixture.CreateDbContext();
        _service = _fixture.CreateService(_db);

        // Limpiamos todas las tablas de datos antes de cada test.
        await _db.Database.ExecuteSqlRawAsync(@"
            TRUNCATE TABLE
                swipes, matches, exchanges, chats, chat_participants, messages,
                book_photos, books_genres, books_languages, books,
                user_preferences_genres, user_preferences,
                users, base_users, genres
            RESTART IDENTITY CASCADE");
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private void SeedUser(Guid id, string username, Point location)
    {
        _db.Users.Add(new BaseUser
        {
            Id           = id,
            SupabaseId   = $"sup-{id}",
            Email        = $"{username}@test.com",
            Username     = username,
            Name         = username,
            ProfilePhoto = string.Empty,
            UserType     = BaseUserType.USER,
            Location     = location,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow
        });
        _db.RegularUsers.Add(new User { Id = id });
    }

    private void SeedPreferences(Guid userId, Point location, int radioKm = 50)
    {
        _db.UserPreferences.Add(new UserPreference
        {
            UserId    = userId,
            Location  = location,
            RadioKm   = radioKm,
            Extension = BooksExtension.MEDIUM,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
    }

    private Book SeedBook(Guid ownerId, BookStatus status = BookStatus.PUBLISHED, int numPaginas = 300)
    {
        var book = new Book
        {
            OwnerId    = ownerId,
            Status     = status,
            NumPaginas = numPaginas,
            Titulo     = $"Libro-{Guid.NewGuid():N}",
            CreatedAt  = DateTime.UtcNow,
            UpdatedAt  = DateTime.UtcNow
        };
        _db.Books.Add(book);
        return book;
    }

    private void LogFeed(IEnumerable<FeedBookDto> feed)
    {
        foreach (var book in feed)
            _output.WriteLine(
                $"  Book={book.Id} | Owner={book.OwnerId} | Titulo={book.Titulo} " +
                $"| Score={book.Score:F4} | IsPriority={book.IsPriority}");
    }

    /// <summary>
    /// Extrae Items de FeedResultDto para assertions.
    /// </summary>
    private List<FeedBookDto> Feed(FeedResultDto result)
    {
        LogFeed(result.Items);
        return result.Items;
    }

    /// <summary>
    /// El feed NO debe incluir libros propios del usuario que consulta.
    /// </summary>
    [Fact]
    public async Task GetFeed_OwnBook_IsNotReturned()
    {
        SeedUser(UserA, "userA", Madrid);
        SeedUser(UserB, "userB", Madrid);
        SeedPreferences(UserA, Madrid);
        SeedBook(UserA);          
        SeedBook(UserB);           
        await _db.SaveChangesAsync();

        var feed = Feed(await _service.GetFeedAsync(UserA, page: 0, pageSize: 20));

        Assert.Single(feed);
        Assert.Equal(UserB, feed[0].OwnerId);
    }

    /// <summary>
    /// Un libro sobre el que el usuario ya hizo swipe (LEFT o RIGHT) no debe volver a aparecer en el feed.
    /// </summary>
    [Fact]
    public async Task GetFeed_AlreadySwiped_IsNotReturned()
    {
        SeedUser(UserA, "userA", Madrid);
        SeedUser(UserB, "userB", Madrid);
        SeedPreferences(UserA, Madrid);
        var bookB = SeedBook(UserB);
        await _db.SaveChangesAsync();

        _db.Swipes.Add(new Swipe
        {
            SwiperId  = UserA,
            BookId    = bookB.Id,
            Direction = SwipeDirection.LEFT,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var feed = Feed(await _service.GetFeedAsync(UserA, page: 0, pageSize: 20));

        Assert.Empty(feed);
    }

    /// <summary>
    /// Solo los libros PUBLISHED deben aparecer. DRAFT, PAUSED, RESERVED, EXCHANGED y DELETED deben quedar excluidos.
    /// </summary>
    [Fact]
    public async Task GetFeed_NonPublishedBooks_AreNotReturned()
    {
        SeedUser(UserA, "userA", Madrid);
        SeedUser(UserB, "userB", Madrid);
        SeedPreferences(UserA, Madrid);
        SeedBook(UserB, BookStatus.DRAFT);
        SeedBook(UserB, BookStatus.PAUSED);
        SeedBook(UserB, BookStatus.RESERVED);
        SeedBook(UserB, BookStatus.EXCHANGED);
        SeedBook(UserB, BookStatus.DELETED);
        SeedBook(UserB, BookStatus.PUBLISHED); // único visible
        await _db.SaveChangesAsync();

        var feed = Feed(await _service.GetFeedAsync(UserA, page: 0, pageSize: 20));

        Assert.Single(feed);
    }

    /// <summary>
    /// Un libro cuyo owner está FUERA del radio configurado no debe aparecer.
    /// Este test valida el filtro geoespacial real de PostGIS.
    /// </summary>
    [Fact]
    public async Task GetFeed_BookOutsideRadius_IsNotReturned()
    {
        SeedUser(UserA, "userA", Madrid);
        SeedUser(UserB, "userB", FueraDeRadio);
        SeedPreferences(UserA, Madrid, radioKm: 50);
        SeedBook(UserB);
        await _db.SaveChangesAsync();

        var feed = Feed(await _service.GetFeedAsync(UserA, page: 0, pageSize: 20));

        Assert.Empty(feed);
    }

    /// <summary>
    /// Un libro dentro del radio SÍ debe aparecer.
    /// Este test valida el filtro geoespacial real de PostGIS.
    /// </summary>
    [Fact]
    public async Task GetFeed_BookInsideRadius_IsReturned()
    {
        SeedUser(UserA, "userA", Madrid);
        SeedUser(UserB, "userB", MadridCercano); 
        SeedPreferences(UserA, Madrid, radioKm: 50);
        SeedBook(UserB);
        await _db.SaveChangesAsync();

        var feed = Feed(await _service.GetFeedAsync(UserA, page: 0, pageSize: 20));

        Assert.Single(feed);
    }

    /// <summary>
    /// Libros de usuarios que mostraron interés reciente en los libros de UserA
    /// deben marcarse como IsPriority=true (pool P1).
    /// El resto deben ser IsPriority=false (pool P2).
    /// </summary>
    [Fact]
    public async Task GetFeed_InterestedUser_BookIsMarkedP1()
    {
        SeedUser(UserA, "userA", Madrid);
        SeedUser(UserB, "userB", Madrid); // interesado en UserA
        SeedUser(UserC, "userC", Madrid); // no interesado
        SeedPreferences(UserA, Madrid);

        var bookA = SeedBook(UserA); // en el que B hará swipe
        SeedBook(UserB);             // libro de B -> debe ser P1
        SeedBook(UserC);             // libro de C -> debe ser P2
        await _db.SaveChangesAsync();

        // UserB hace RIGHT en un libro de UserA -> es "interesado"
        _db.Swipes.Add(new Swipe
        {
            SwiperId  = UserB,
            BookId    = bookA.Id,
            Direction = SwipeDirection.RIGHT,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var feed = Feed(await _service.GetFeedAsync(UserA, page: 0, pageSize: 20));

        Assert.Equal(2, feed.Count);
        Assert.True(feed.First(f => f.OwnerId == UserB).IsPriority,
            "Libro de usuario interesado debe ser P1");
        Assert.False(feed.First(f => f.OwnerId == UserC).IsPriority,
            "Libro de usuario no interesado debe ser P2");
    }

    /// <summary>
    /// Un swipe RIGHT hecho hace más de SwipeValidDays días no cuenta como interés vigente.
    /// El libro del usuario debe aparecer como P2, no P1.
    /// </summary>
    [Fact]
    public async Task GetFeed_ExpiredInterest_BookIsNotP1()
    {
        SeedUser(UserA, "userA", Madrid);
        SeedUser(UserB, "userB", Madrid);
        SeedPreferences(UserA, Madrid);

        var bookA = SeedBook(UserA);
        SeedBook(UserB);
        await _db.SaveChangesAsync();

        // Swipe de hace 31 días -> fuera del plazo de 30 días
        _db.Swipes.Add(new Swipe
        {
            SwiperId  = UserB,
            BookId    = bookA.Id,
            Direction = SwipeDirection.RIGHT,
            CreatedAt = DateTime.UtcNow.AddDays(-(PostgresMatcherFixture.Settings.Feed.SwipeValidDays + 1))
        });
        await _db.SaveChangesAsync();

        var feed = Feed(await _service.GetFeedAsync(UserA, page: 0, pageSize: 20));

        Assert.Single(feed);
        Assert.False(feed[0].IsPriority, "Interés expirado no debe clasificar el libro como P1");
    }

    /// <summary>
    /// La segunda página no debe contener libros de la primera.
    /// Valida que la paginación por offset funciona correctamente.
    /// </summary>
    [Fact]
    public async Task GetFeed_Pagination_SecondPageDoesNotRepeatFirst()
    {
        SeedUser(UserA, "userA", Madrid);
        SeedUser(UserB, "userB", Madrid);
        SeedPreferences(UserA, Madrid);

        for (var i = 0; i < 5; i++) SeedBook(UserB);
        await _db.SaveChangesAsync();

        var page0 = Feed(await _service.GetFeedAsync(UserA, page: 0, pageSize: 3));
        var page1 = Feed(await _service.GetFeedAsync(UserA, page: 1, pageSize: 3));

        _output.WriteLine("── Página 0 ──");
        _output.WriteLine("── Página 1 ──");

        Assert.Equal(3, page0.Count);
        Assert.Equal(2, page1.Count);
        Assert.Empty(page0.Select(b => b.Id).Intersect(page1.Select(b => b.Id)));
    }

    // ════════════════════════════════════════════════════════════════════
    // TESTS DE AUDITORÍA — Validación de bugs y correcciones
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// TEST-D01: Valida bug de ubicación reportado por el usuario.
    /// Cuando un usuario tiene user_preferences con location (0,0),
    /// el matcher debe usar la ubicación de base_users en su lugar.
    /// </summary>
    [Fact]
    public async Task GetFeed_UserPreferencesWithZeroLocation_UsesBaseUserLocation()
    {
        SeedUser(UserA, "userA", Madrid);
        SeedUser(UserB, "userB", MadridCercano);

        // Crear preferencias con location (0,0) — el bug original
        var zeroPoint = MakePoint(0, 0);
        _db.UserPreferences.Add(new UserPreference
        {
            UserId = UserA,
            Location = zeroPoint,  // Ubicación incorrecta (0,0)
            RadioKm = 50,
            Extension = BooksExtension.MEDIUM,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        SeedBook(UserB);
        await _db.SaveChangesAsync();

        // Con la corrección, el feed debe encontrar el libro de UserB
        // porque usa base_users.location (Madrid) en vez de (0,0)
        var feed = Feed(await _service.GetFeedAsync(UserA, page: 0, pageSize: 20));

        Assert.Single(feed);
        Assert.Equal(UserB, feed[0].OwnerId);
    }

    /// <summary>
    /// TEST-D02: Sin preferencias guardadas, el feed usa base_users.location
    /// como fallback y devuelve libros dentro del radio por defecto (50km).
    /// Valida audit #3 parcialmente (fallback location).
    /// </summary>
    [Fact]
    public async Task GetFeed_UserWithoutPreferences_UsesFallbackLocationAndReturnsBooks()
    {
        SeedUser(UserA, "userA", Madrid);
        SeedUser(UserB, "userB", MadridCercano);
        // No llamamos SeedPreferences(UserA, ...) → fallback
        SeedBook(UserB);
        await _db.SaveChangesAsync();

        var feed = Feed(await _service.GetFeedAsync(UserA, page: 0, pageSize: 20));

        Assert.Single(feed);
    }

    /// <summary>
    /// TEST-D03: GenreMatch para usuarios sin preferencias.
    /// Con la corrección (audit #3), el peso del género se redistribuye 
    /// entre los demás componentes. El score total debe seguir sumando ~1.0.
    /// </summary>
    [Fact]
    public async Task GetFeed_UserWithFallbackPreferences_ScoreIsPositiveAndReasonable()
    {
        SeedUser(UserA, "userA", Madrid);
        SeedUser(UserB, "userB", Madrid); // Misma ubicación → distancia ~0
        // Sin preferencias → fallback
        var book = SeedBook(UserB, numPaginas: 300); // MEDIUM (200-400)
        await _db.SaveChangesAsync();

        var feed = Feed(await _service.GetFeedAsync(UserA, page: 0, pageSize: 20));

        Assert.Single(feed);
        // Score debe ser > 0 (al menos distance + recency + extension contribuyen)
        Assert.True(feed[0].Score > 0, $"Score debería ser > 0, fue {feed[0].Score}");
        // Score no debe ser negativo (audit #4)
        Assert.True(feed[0].Score >= 0, $"Score no debería ser negativo, fue {feed[0].Score}");
    }

    /// <summary>
    /// TEST-D04: Score con genre match activo (usuario con preferencias + géneros).
    /// Libros que coinciden con los géneros preferidos deben tener score mayor.
    /// </summary>
    [Fact]
    public async Task GetFeed_WithGenrePreferences_MatchingBookHasHigherScore()
    {
        SeedUser(UserA, "userA", Madrid);
        SeedUser(UserB, "userB", Madrid);

        // Crear género y preferencia de género
        var genre = new Genre { Name = "Fantasía" };
        _db.Genres.Add(genre);
        await _db.SaveChangesAsync();

        var prefs = new UserPreference
        {
            UserId = UserA,
            Location = MakePoint(0, 0), // Se sobreescribirá con Madrid
            RadioKm = 50,
            Extension = BooksExtension.MEDIUM,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.UserPreferences.Add(prefs);
        await _db.SaveChangesAsync();

        _db.UserPreferenceGenres.Add(new UserPreferenceGenre
        {
            PreferencesId = prefs.Id,
            GenreId = genre.Id
        });

        // Libro con género que coincide
        var matchingBook = SeedBook(UserB, numPaginas: 300);
        // Libro sin género que coincide
        var nonMatchingBook = SeedBook(UserB, numPaginas: 300);
        await _db.SaveChangesAsync();

        _db.BookGenres.Add(new BookGenre { BookId = matchingBook.Id, GenreId = genre.Id });
        await _db.SaveChangesAsync();

        var feed = Feed(await _service.GetFeedAsync(UserA, page: 0, pageSize: 20));
        _output.WriteLine("── Feed con preferencias de género ──");

        Assert.Equal(2, feed.Count);
        // El libro con género coincidente debe tener mayor score
        var matchingFeed = feed.First(f => f.Id == matchingBook.Id);
        var nonMatchingFeed = feed.First(f => f.Id == nonMatchingBook.Id);
        Assert.True(matchingFeed.Score > nonMatchingFeed.Score,
            $"Libro con género debería tener score mayor: {matchingFeed.Score} vs {nonMatchingFeed.Score}");
    }

    /// <summary>
    /// TEST-D05: DistanceKm se incluye en el DTO del feed (audit #9).
    /// </summary>
    [Fact]
    public async Task GetFeed_ReturnsDistanceKmInDto()
    {
        SeedUser(UserA, "userA", Madrid);
        SeedUser(UserB, "userB", MadridCercano); // ~1km de Madrid
        SeedPreferences(UserA, Madrid);
        SeedBook(UserB);
        await _db.SaveChangesAsync();

        var feed = Feed(await _service.GetFeedAsync(UserA, page: 0, pageSize: 20));

        Assert.Single(feed);
        // DistanceKm debe ser > 0 (UserB está a ~1km)
        _output.WriteLine($"DistanceKm: {feed[0].DistanceKm}");
        Assert.True(feed[0].DistanceKm > 0, "DistanceKm debería ser > 0 para usuario cercano");
        Assert.True(feed[0].DistanceKm < 5, "DistanceKm debería ser < 5 para usuario a ~1km");
    }

    /// <summary>
    /// TEST-D06: Score nunca es negativo, ni siquiera en el límite del radio (audit #4).
    /// </summary>
    [Fact]
    public async Task GetFeed_BookAtRadiusLimit_ScoreIsNotNegative()
    {
        // Radio de UserA: 50km. UserB está a ~90km (FueraDeRadio no entra, 
        // así que usamos un punto que esté en el borde)
        var bordeLimite = MakePoint(-3.15, 40.4168); // ~45km al este de Madrid
        SeedUser(UserA, "userA", Madrid);
        SeedUser(UserB, "userB", bordeLimite);
        SeedPreferences(UserA, Madrid, radioKm: 50);
        SeedBook(UserB);
        await _db.SaveChangesAsync();

        var feed = Feed(await _service.GetFeedAsync(UserA, page: 0, pageSize: 20));

        if (feed.Count > 0)
        {
            Assert.True(feed[0].Score >= 0,
                $"Score en el límite del radio debería ser >= 0, fue {feed[0].Score}");
        }
    }

    /// <summary>
    /// TEST-D07: Swipe sobre libro propio lanza excepción.
    /// </summary>
    [Fact]
    public async Task ProcessSwipe_OwnBook_ThrowsInvalidOperationException()
    {
        SeedUser(UserA, "userA", Madrid);
        var book = SeedBook(UserA);
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ProcessSwipeAsync(UserA, book.Id, SwipeDirection.RIGHT));

        Assert.Contains("propio", ex.Message);
    }

    /// <summary>
    /// TEST-D08: Swipe sobre libro inexistente lanza KeyNotFoundException.
    /// </summary>
    [Fact]
    public async Task ProcessSwipe_BookNotFound_ThrowsKeyNotFoundException()
    {
        SeedUser(UserA, "userA", Madrid);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.ProcessSwipeAsync(UserA, 99999, SwipeDirection.RIGHT));
    }

    /// <summary>
    /// TEST-D09: Swipe sobre libro no publicado devuelve BookUnavailable.
    /// </summary>
    [Theory]
    [InlineData(BookStatus.DRAFT)]
    [InlineData(BookStatus.PAUSED)]
    [InlineData(BookStatus.RESERVED)]
    [InlineData(BookStatus.EXCHANGED)]
    public async Task ProcessSwipe_BookNotPublished_ReturnsBookUnavailable(BookStatus status)
    {
        SeedUser(UserA, "userA", Madrid);
        SeedUser(UserB, "userB", Madrid);
        var book = SeedBook(UserB, status);
        await _db.SaveChangesAsync();

        var result = await _service.ProcessSwipeAsync(UserA, book.Id, SwipeDirection.RIGHT);

        Assert.Equal(SwipeOutcome.BookUnavailable, result.Outcome);
    }

    /// <summary>
    /// TEST-D10: Swipe LEFT registra el swipe y no detecta match.
    /// </summary>
    [Fact]
    public async Task ProcessSwipe_LeftSwipe_RecordsSwipeAndReturnsRecorded()
    {
        SeedUser(UserA, "userA", Madrid);
        SeedUser(UserB, "userB", Madrid);
        var book = SeedBook(UserB);
        await _db.SaveChangesAsync();

        var result = await _service.ProcessSwipeAsync(UserA, book.Id, SwipeDirection.LEFT);

        Assert.Equal(SwipeOutcome.Recorded, result.Outcome);
        Assert.Null(result.Match);
        Assert.Equal(1, await _db.Swipes.CountAsync());
        Assert.Equal(0, await _db.Matches.CountAsync());
    }

    /// <summary>
    /// TEST-D11: Swipe RIGHT sin reciprocidad no crea match.
    /// </summary>
    [Fact]
    public async Task ProcessSwipe_RightSwipeNoMutual_ReturnsRecordedNoMatch()
    {
        SeedUser(UserA, "userA", Madrid);
        SeedUser(UserB, "userB", Madrid);
        var book = SeedBook(UserB);
        await _db.SaveChangesAsync();

        var result = await _service.ProcessSwipeAsync(UserA, book.Id, SwipeDirection.RIGHT);

        Assert.Equal(SwipeOutcome.Recorded, result.Outcome);
        Assert.Null(result.Match);
        Assert.Equal(0, await _db.Matches.CountAsync());
    }

    /// <summary>
    /// TEST-D12: Swipe RIGHT con reciprocidad crea Match + Chat + Exchange.
    /// </summary>
    [Fact]
    public async Task ProcessSwipe_RightSwipeMutualExists_ReturnsMatchCreated()
    {
        SeedUser(UserA, "userA", Madrid);
        SeedUser(UserB, "userB", Madrid);
        var bookA = SeedBook(UserA); // Libro de A
        var bookB = SeedBook(UserB); // Libro de B
        await _db.SaveChangesAsync();

        // Configurar mock del ChatService para devolver un chat válido
        var mockChatService = new Mock<IChatService>();
        mockChatService.Setup(c => c.CreateChat(ChatType.EXCHANGE, It.IsAny<List<Guid>>()))
            .ReturnsAsync(new ChatDto(
                Id: 1,
                Type: ChatType.EXCHANGE.ToString(),
                CreatedAt: DateTime.UtcNow,
                Participants: [],
                LastMessage: null
            ));
        var service = _fixture.CreateServiceWithChat(_db, mockChatService.Object);

        // B hace RIGHT en libro de A
        _db.Swipes.Add(new Swipe
        {
            SwiperId = UserB,
            BookId = bookA.Id,
            Direction = SwipeDirection.RIGHT,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        // A hace RIGHT en libro de B → MATCH
        var result = await service.ProcessSwipeAsync(UserA, bookB.Id, SwipeDirection.RIGHT);

        Assert.Equal(SwipeOutcome.MatchCreated, result.Outcome);
        Assert.NotNull(result.Match);
        Assert.Equal(1, await _db.Matches.CountAsync());
        Assert.Equal(1, await _db.Exchanges.CountAsync());
    }

    /// <summary>
    /// TEST-D13: Swipe mutuo fuera del plazo no genera match (audit #5 regresión).
    /// </summary>
    [Fact]
    public async Task ProcessSwipe_MutualSwipeExpired_ReturnsRecordedNoMatch()
    {
        SeedUser(UserA, "userA", Madrid);
        SeedUser(UserB, "userB", Madrid);
        var bookA = SeedBook(UserA);
        var bookB = SeedBook(UserB);
        await _db.SaveChangesAsync();

        // B hizo RIGHT en libro de A hace 31 días (fuera del plazo de 30 días)
        _db.Swipes.Add(new Swipe
        {
            SwiperId = UserB,
            BookId = bookA.Id,
            Direction = SwipeDirection.RIGHT,
            CreatedAt = DateTime.UtcNow.AddDays(-(PostgresMatcherFixture.Settings.Feed.SwipeValidDays + 1))
        });
        await _db.SaveChangesAsync();

        // A hace RIGHT en libro de B hoy → NO match (swipe expirado)
        var result = await _service.ProcessSwipeAsync(UserA, bookB.Id, SwipeDirection.RIGHT);

        Assert.Equal(SwipeOutcome.Recorded, result.Outcome);
        Assert.Null(result.Match);
        Assert.Equal(0, await _db.Matches.CountAsync());
    }

    /// <summary>
    /// TEST-D14: Libro swipeado no vuelve a aparecer en el feed (decisión de diseño #12).
    /// Un swipe LEFT debe eliminar permanentemente el libro del feed.
    /// </summary>
    [Fact]
    public async Task GetFeed_SwipedBook_NeverReturnsAgain()
    {
        SeedUser(UserA, "userA", Madrid);
        SeedUser(UserB, "userB", Madrid);
        SeedPreferences(UserA, Madrid);
        var book = SeedBook(UserB);
        await _db.SaveChangesAsync();

        // Verificar que el libro aparece antes del swipe
        var feedBefore = Feed(await _service.GetFeedAsync(UserA, page: 0, pageSize: 20));
        Assert.Single(feedBefore);

        // Hacer swipe LEFT
        await _service.ProcessSwipeAsync(UserA, book.Id, SwipeDirection.LEFT);

        // El libro no debe volver a aparecer
        var feedAfter = Feed(await _service.GetFeedAsync(UserA, page: 0, pageSize: 20));
        Assert.Empty(feedAfter);
    }

    /// <summary>
    /// TEST-D15: Score con libro cercano vs lejano — el cercano tiene mayor score.
    /// Valida que el componente de distancia del score funciona correctamente.
    /// </summary>
    [Fact]
    public async Task GetFeed_CloserBookHasHigherScore()
    {
        // ~2km de Madrid
        var punto2km = MakePoint(-3.68, 40.4168);
        // ~30km de Madrid  
        var punto30km = MakePoint(-3.35, 40.4168);

        SeedUser(UserA, "userA", Madrid);
        SeedUser(UserB, "userB", punto2km);
        SeedUser(UserC, "userC", punto30km);
        SeedPreferences(UserA, Madrid, radioKm: 50);
        SeedBook(UserB, numPaginas: 300);
        SeedBook(UserC, numPaginas: 300);
        await _db.SaveChangesAsync();

        var feed = Feed(await _service.GetFeedAsync(UserA, page: 0, pageSize: 20));
        _output.WriteLine("── Feed ordenado por score ──");

        Assert.Equal(2, feed.Count);
        var bookCloseScore = feed.First(f => f.OwnerId == UserB).Score;
        var bookFarScore = feed.First(f => f.OwnerId == UserC).Score;
        Assert.True(bookCloseScore > bookFarScore,
            $"Libro cercano debería tener score mayor: {bookCloseScore} vs {bookFarScore}");
    }

    /// <summary>
    /// TEST-D16: Extension match — libro MEDIUM con preferencia MEDIUM tiene mayor score.
    /// </summary>
    [Fact]
    public async Task GetFeed_ExtensionMatch_MatchingBookScoresHigher()
    {
        SeedUser(UserA, "userA", Madrid);
        SeedUser(UserB, "userB", Madrid);
        SeedPreferences(UserA, Madrid); // Extension por defecto: MEDIUM

        var mediumBook = SeedBook(UserB, numPaginas: 300);  // MEDIUM (200-400)
        var longBook = SeedBook(UserB, numPaginas: 600);    // LONG (>400) 
        await _db.SaveChangesAsync();

        var feed = Feed(await _service.GetFeedAsync(UserA, page: 0, pageSize: 20));
        _output.WriteLine("── Feed con extension match ──");

        Assert.Equal(2, feed.Count);
        var mediumScore = feed.First(f => f.Id == mediumBook.Id).Score;
        var longScore = feed.First(f => f.Id == longBook.Id).Score;
        Assert.True(mediumScore > longScore,
            $"Libro MEDIUM debería tener score mayor con preferencia MEDIUM: {mediumScore} vs {longScore}");
    }

    // ════════════════════════════════════════════════════════════════════
    //  TESTS NUEVAS FUNCIONALIDADES
    // ════════════════════════════════════════════════════════════════════

    // ── ValidateWeightsSum ──────────────────────────────────────────────

    /// <summary>
    /// TEST-N01: Si los pesos suman 1.0 el feed funciona normalmente.
    /// </summary>
    [Fact]
    public async Task GetFeed_ValidWeights_DoesNotThrow()
    {
        SeedUser(UserA, "userA", Madrid);
        SeedUser(UserB, "userB", Madrid);
        SeedPreferences(UserA, Madrid);
        SeedBook(UserB);
        await _db.SaveChangesAsync();

        // Los pesos del fixture suman 1.0 (0.40+0.10+0.35+0.15)
        var feed = Feed(await _service.GetFeedAsync(UserA, page: 0, pageSize: 20));
        Assert.Single(feed);
    }

    /// <summary>
    /// TEST-N02: Si los pesos NO suman 1.0, el feed lanza InvalidOperationException.
    /// </summary>
    [Fact]
    public async Task GetFeed_InvalidWeights_ThrowsInvalidOperation()
    {
        SeedUser(UserA, "userA", Madrid);
        SeedPreferences(UserA, Madrid);
        await _db.SaveChangesAsync();

        var badSettings = new MatcherSettings
        {
            Weights = new WeightsSettings
            {
                GenreMatch = 0.50,
                ExtensionMatch = 0.30,
                DistanceScore = 0.30,
                RecencyBonus = 0.10 // suma = 1.20
            },
            Feed = PostgresMatcherFixture.Settings.Feed
        };
        var badService = new MatcherService(
            _db,
            Options.Create(badSettings),
            new Mock<ILogger<MatcherService>>().Object,
            new Mock<IChatService>().Object,
            new Mock<IExchangeService>().Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => badService.GetFeedAsync(UserA, page: 0, pageSize: 20));
        Assert.Contains("1.0", ex.Message);
    }

    // ── FeedResultDto (paginación) ──────────────────────────────────────

    /// <summary>
    /// TEST-N03: FeedResultDto incluye metadatos de paginación correctos.
    /// </summary>
    [Fact]
    public async Task GetFeed_ReturnsCorrectPaginationMetadata()
    {
        SeedUser(UserA, "userA", Madrid);
        SeedUser(UserB, "userB", Madrid);
        SeedPreferences(UserA, Madrid);
        // Crear 5 libros
        for (int i = 0; i < 5; i++) SeedBook(UserB);
        await _db.SaveChangesAsync();

        var result = await _service.GetFeedAsync(UserA, page: 0, pageSize: 3);
        _output.WriteLine($"Items={result.Items.Count} Page={result.Page} PageSize={result.PageSize} HasMore={result.HasMore}");

        Assert.Equal(0, result.Page);
        Assert.Equal(3, result.PageSize);
        Assert.Equal(3, result.Items.Count);
        Assert.True(result.HasMore);
    }

    /// <summary>
    /// TEST-N04: HasMore es false cuando no hay más libros.
    /// </summary>
    [Fact]
    public async Task GetFeed_LastPage_HasMoreIsFalse()
    {
        SeedUser(UserA, "userA", Madrid);
        SeedUser(UserB, "userB", Madrid);
        SeedPreferences(UserA, Madrid);
        SeedBook(UserB);
        SeedBook(UserB);
        await _db.SaveChangesAsync();

        var result = await _service.GetFeedAsync(UserA, page: 0, pageSize: 20);

        Assert.Equal(2, result.Items.Count);
        Assert.False(result.HasMore);
    }

    // ── Partial genre scoring ───────────────────────────────────────────

    /// <summary>
    /// TEST-N05: Un libro con 2/3 géneros preferidos tiene mayor score que uno con 1/3.
    /// Valida que el scoring parcial de géneros funciona (ratio, no binario).
    /// </summary>
    [Fact]
    public async Task GetFeed_PartialGenreMatch_MoreGenresHigherScore()
    {
        SeedUser(UserA, "userA", Madrid);
        SeedUser(UserB, "userB", Madrid);

        // Crear 3 géneros
        var g1 = new Genre { Name = "Fantasía" };
        var g2 = new Genre { Name = "Ciencia ficción" };
        var g3 = new Genre { Name = "Terror" };
        _db.Genres.AddRange(g1, g2, g3);
        await _db.SaveChangesAsync();

        // Preferencias con los 3 géneros
        var prefs = new UserPreference
        {
            UserId = UserA,
            Location = Madrid,
            RadioKm = 50,
            Extension = BooksExtension.MEDIUM,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.UserPreferences.Add(prefs);
        await _db.SaveChangesAsync();
        _db.UserPreferenceGenres.AddRange(
            new UserPreferenceGenre { PreferencesId = prefs.Id, GenreId = g1.Id },
            new UserPreferenceGenre { PreferencesId = prefs.Id, GenreId = g2.Id },
            new UserPreferenceGenre { PreferencesId = prefs.Id, GenreId = g3.Id }
        );

        // Libro con 2 géneros match
        var book2match = SeedBook(UserB, numPaginas: 300);
        // Libro con 1 género match
        var book1match = SeedBook(UserB, numPaginas: 300);
        await _db.SaveChangesAsync();

        _db.BookGenres.AddRange(
            new BookGenre { BookId = book2match.Id, GenreId = g1.Id },
            new BookGenre { BookId = book2match.Id, GenreId = g2.Id },
            new BookGenre { BookId = book1match.Id, GenreId = g1.Id }
        );
        await _db.SaveChangesAsync();

        var feed = Feed(await _service.GetFeedAsync(UserA, page: 0, pageSize: 20));
        _output.WriteLine("── Partial genre scoring ──");

        Assert.Equal(2, feed.Count);
        var score2 = feed.First(f => f.Id == book2match.Id).Score;
        var score1 = feed.First(f => f.Id == book1match.Id).Score;
        Assert.True(score2 > score1,
            $"Libro con 2/3 géneros debería tener score mayor: {score2:F4} vs {score1:F4}");
    }

    // ── Duplicate match prevention ──────────────────────────────────────

    /// <summary>
    /// TEST-N06: Si ya existe un match entre dos usuarios, un nuevo RIGHT swipe
    /// devuelve Recorded (no crea match duplicado).
    /// </summary>
    [Fact]
    public async Task ProcessSwipe_AlreadyMatched_ReturnsRecordedNoDuplicate()
    {
        SeedUser(UserA, "userA", Madrid);
        SeedUser(UserB, "userB", Madrid);
        SeedPreferences(UserA, Madrid);
        SeedPreferences(UserB, Madrid);

        var bookA = SeedBook(UserA);
        var bookB = SeedBook(UserB);
        await _db.SaveChangesAsync();

        // Simular match existente (UserA ya matcheó con UserB)
        var chatMock = new Mock<IChatService>();
        chatMock.Setup(c => c.CreateChat(ChatType.EXCHANGE, It.IsAny<List<Guid>>()))
            .ReturnsAsync(new ChatDto(Id: 100, Type: ChatType.EXCHANGE.ToString(), CreatedAt: DateTime.UtcNow, Participants: [], LastMessage: null));
        var serviceWithChat = _fixture.CreateServiceWithChat(_db, chatMock.Object);

        // B swipea RIGHT a libro de A → registra swipe
        await serviceWithChat.ProcessSwipeAsync(UserB, bookA.Id, SwipeDirection.RIGHT);
        // A swipea RIGHT a libro de B → debería crear match
        var firstResult = await serviceWithChat.ProcessSwipeAsync(UserA, bookB.Id, SwipeDirection.RIGHT);
        Assert.Equal(SwipeOutcome.MatchCreated, firstResult.Outcome);

        // Ahora otro RIGHT de A sobre un 2do libro de B → no debe crear match duplicado
        var bookB2 = SeedBook(UserB);
        await _db.SaveChangesAsync();
        var secondResult = await serviceWithChat.ProcessSwipeAsync(UserA, bookB2.Id, SwipeDirection.RIGHT);

        Assert.Equal(SwipeOutcome.Recorded, secondResult.Outcome);
        Assert.Null(secondResult.Match);
        // Solo un match debe existir
        Assert.Equal(1, await _db.Matches.CountAsync());
    }

    // ── Undo swipe ──────────────────────────────────────────────────────

    /// <summary>
    /// TEST-N07: Undo last swipe elimina el swipe de la BD.
    /// </summary>
    [Fact]
    public async Task UndoLastSwipe_LeftSwipe_RemovesSwipe()
    {
        SeedUser(UserA, "userA", Madrid);
        SeedUser(UserB, "userB", Madrid);
        SeedPreferences(UserA, Madrid);
        var book = SeedBook(UserB);
        await _db.SaveChangesAsync();

        await _service.ProcessSwipeAsync(UserA, book.Id, SwipeDirection.LEFT);
        Assert.Equal(1, await _db.Swipes.CountAsync());

        var undone = await _service.UndoLastSwipeAsync(UserA);

        Assert.True(undone);
        Assert.Equal(0, await _db.Swipes.CountAsync());
    }

    /// <summary>
    /// TEST-N08: Undo con swipe RIGHT que NO generó match → lo elimina.
    /// </summary>
    [Fact]
    public async Task UndoLastSwipe_RightSwipeNoMatch_RemovesSwipe()
    {
        SeedUser(UserA, "userA", Madrid);
        SeedUser(UserB, "userB", Madrid);
        SeedPreferences(UserA, Madrid);
        var book = SeedBook(UserB);
        await _db.SaveChangesAsync();

        var res = await _service.ProcessSwipeAsync(UserA, book.Id, SwipeDirection.RIGHT);
        Assert.Equal(SwipeOutcome.Recorded, res.Outcome);
        Assert.Equal(1, await _db.Swipes.CountAsync());

        var undone = await _service.UndoLastSwipeAsync(UserA);

        Assert.True(undone);
        Assert.Equal(0, await _db.Swipes.CountAsync());
    }

    /// <summary>
    /// TEST-N09: Undo con swipe RIGHT que generó match → devuelve false, no elimina nada.
    /// </summary>
    [Fact]
    public async Task UndoLastSwipe_RightSwipeWithMatch_ReturnsFalse()
    {
        SeedUser(UserA, "userA", Madrid);
        SeedUser(UserB, "userB", Madrid);
        SeedPreferences(UserA, Madrid);
        SeedPreferences(UserB, Madrid);

        var bookA = SeedBook(UserA);
        var bookB = SeedBook(UserB);
        await _db.SaveChangesAsync();

        var chatMock = new Mock<IChatService>();
        chatMock.Setup(c => c.CreateChat(ChatType.EXCHANGE, It.IsAny<List<Guid>>()))
            .ReturnsAsync(new ChatDto(Id: 200, Type: ChatType.EXCHANGE.ToString(), CreatedAt: DateTime.UtcNow, Participants: [], LastMessage: null));
        var serviceWithChat = _fixture.CreateServiceWithChat(_db, chatMock.Object);

        await serviceWithChat.ProcessSwipeAsync(UserB, bookA.Id, SwipeDirection.RIGHT);
        var res = await serviceWithChat.ProcessSwipeAsync(UserA, bookB.Id, SwipeDirection.RIGHT);
        Assert.Equal(SwipeOutcome.MatchCreated, res.Outcome);

        // El último swipe de A generó match → no se puede deshacer
        var undone = await serviceWithChat.UndoLastSwipeAsync(UserA);

        Assert.False(undone);
        // El swipe sigue en la BD
        Assert.True(await _db.Swipes.AnyAsync(s => s.SwiperId == UserA));
    }

    /// <summary>
    /// TEST-N10: Undo sin swipes previos → devuelve false.
    /// </summary>
    [Fact]
    public async Task UndoLastSwipe_NoSwipes_ReturnsFalse()
    {
        SeedUser(UserA, "userA", Madrid);
        await _db.SaveChangesAsync();

        var undone = await _service.UndoLastSwipeAsync(UserA);

        Assert.False(undone);
    }

    /// <summary>
    /// TEST-N11: Después de undo, el libro vuelve a aparecer en el feed.
    /// </summary>
    [Fact]
    public async Task UndoLastSwipe_BookReappearsInFeed()
    {
        SeedUser(UserA, "userA", Madrid);
        SeedUser(UserB, "userB", Madrid);
        SeedPreferences(UserA, Madrid);
        var book = SeedBook(UserB);
        await _db.SaveChangesAsync();

        // Swipe a libro
        await _service.ProcessSwipeAsync(UserA, book.Id, SwipeDirection.LEFT);
        var feedAfterSwipe = Feed(await _service.GetFeedAsync(UserA, page: 0, pageSize: 20));
        Assert.Empty(feedAfterSwipe);

        // Undo
        var undone = await _service.UndoLastSwipeAsync(UserA);
        Assert.True(undone);

        // El libro debe volver a aparecer
        var feedAfterUndo = Feed(await _service.GetFeedAsync(UserA, page: 0, pageSize: 20));
        Assert.Single(feedAfterUndo);
        Assert.Equal(book.Id, feedAfterUndo[0].Id);
    }
}

