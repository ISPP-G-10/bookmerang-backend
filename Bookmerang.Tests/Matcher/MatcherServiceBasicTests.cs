// ============================================================
// Cómo ejecutar los tests del Matcher
// Todos los tests de esta clase: dotnet test Bookmerang.Tests --filter "FullyQualifiedName~MatcherServiceBasicTests"
// Un test concreto (sin log): dotnet test Bookmerang.Tests --filter "FullyQualifiedName~GetFeed_OwnBook_IsNotReturned"
// Un test concreto (con log visible por consola): dotnet test Bookmerang.Tests --filter "FullyQualifiedName~GetFeed_OwnBook_IsNotReturned" --logger "console;verbosity=detailed"
// ============================================================

using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Implementation.Matcher;
using Bookmerang.Tests.Helpers;
using Bookmerang.Api.Models.DTOs;
using Microsoft.EntityFrameworkCore;
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
                users, base_users
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

        var feed = await _service.GetFeedAsync(UserA, page: 0, pageSize: 20);
        LogFeed(feed);

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

        var feed = await _service.GetFeedAsync(UserA, page: 0, pageSize: 20);
        LogFeed(feed);

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

        var feed = await _service.GetFeedAsync(UserA, page: 0, pageSize: 20);
        LogFeed(feed);

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

        var feed = await _service.GetFeedAsync(UserA, page: 0, pageSize: 20);
        LogFeed(feed);

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

        var feed = await _service.GetFeedAsync(UserA, page: 0, pageSize: 20);
        LogFeed(feed);

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

        var feed = await _service.GetFeedAsync(UserA, page: 0, pageSize: 20);
        LogFeed(feed);

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

        var feed = await _service.GetFeedAsync(UserA, page: 0, pageSize: 20);
        LogFeed(feed);

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

        var page0 = await _service.GetFeedAsync(UserA, page: 0, pageSize: 3);
        var page1 = await _service.GetFeedAsync(UserA, page: 1, pageSize: 3);

        _output.WriteLine("── Página 0 ──");
        LogFeed(page0);
        _output.WriteLine("── Página 1 ──");
        LogFeed(page1);

        Assert.Equal(3, page0.Count);
        Assert.Equal(2, page1.Count);
        Assert.Empty(page0.Select(b => b.Id).Intersect(page1.Select(b => b.Id)));
    }
}
