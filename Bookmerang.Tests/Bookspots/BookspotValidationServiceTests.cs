// ============================================================
// Todos: dotnet test Bookmerang.Tests --filter "FullyQualifiedName~BookspotValidationServiceTests"
// Uno concreto: dotnet test Bookmerang.Tests --filter "FullyQualifiedName~CreateAsync_SelfValidation_ThrowsValidationException"
// ============================================================

using Bookmerang.Api.Exceptions;
using Bookmerang.Api.Models.DTOs.Bookspots.Requests;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Implementation.Bookspots;
using Bookmerang.Api.Services.Interfaces.Bookspots;
using Bookmerang.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Bookmerang.Tests.Bookspots;

/// <summary>
/// Tests del BookspotValidationService.
/// Lógica pura con mocks + integración contra PostgreSQL real.
/// </summary>
public class BookspotValidationServiceTests(
    PostgresBookspotFixture fixture)
    : IClassFixture<PostgresBookspotFixture>, IAsyncLifetime
{
    private Bookmerang.Api.Data.AppDbContext _db = null!;
    private BookspotValidationService _service = null!;

    // ── Coordenadas ────────────────────────────────────────────────────

    private static readonly Point Madrid = MakePoint(-3.7038, 40.4168);

    private static Point MakePoint(double lon, double lat) =>
        new GeometryFactory(new PrecisionModel(), 4326)
            .CreatePoint(new Coordinate(lon, lat));

    // ── IDs de usuarios de prueba ──────────────────────────────────────

    private static readonly Guid UserA = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserB = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid UserC = new("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private const string SupabaseA = "sup-val-a";
    private const string SupabaseB = "sup-val-b";
    private const string SupabaseC = "sup-val-c";

    // ── Ciclo de vida ──────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        _db = fixture.CreateDbContext();
        _service = fixture.CreateValidationService(_db);

        await _db.Database.ExecuteSqlRawAsync(@"
            TRUNCATE TABLE
                bookspot_validations, bookspots,
                user_preferences_genres, user_preferences,
                swipes, matches, exchanges, chats, chat_participants, messages,
                book_photos, books_genres, books_languages, books,
                users, base_users, genres
            RESTART IDENTITY CASCADE");
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    // ── Seed helpers ───────────────────────────────────────────────────

    private async Task SeedUserRaw(Guid id, string supabaseId, string username)
    {
        var wkt = $"SRID=4326;POINT(-3.7038 40.4168)";

        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO base_users (id, supabase_id, email, username, nombre, foto_perfil_url, type, location, created_at, updated_at)
            VALUES ({id}, {supabaseId}, {username + "@test.com"}, {username}, {username}, '', 2, ST_GeomFromEWKT({wkt}), NOW(), NOW())");

        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO users (id) VALUES ({id})");
    }

#pragma warning disable EF1002
    private async Task<int> SeedBookspotRaw(
        Guid ownerId,
        BookspotStatus status = BookspotStatus.PENDING)
    {
        var nombre = $"Bookspot-{Guid.NewGuid():N}";
        var statusStr = status.ToString();
        var wkt = "SRID=4326;POINT(-3.7038 40.4168)";

        await _db.Database.ExecuteSqlRawAsync($@"
            INSERT INTO bookspots (nombre, address_text, location, is_bookdrop, created_by_user_id, status, created_at, updated_at)
            VALUES ('{nombre}', 'Calle Test', ST_GeomFromEWKT('{wkt}'), false, '{ownerId}', '{statusStr}'::bookspot_status, NOW(), NOW())");

        return await _db.Bookspots
            .OrderByDescending(b => b.Id)
            .Select(b => b.Id)
            .FirstAsync();
    }
#pragma warning restore EF1002

    private async Task SeedValidationRaw(int bookspotId, Guid validatorId, bool knowsPlace, bool safeForExchange)
    {
        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO bookspot_validations (bookspot_id, validator_user_id, knows_place, safe_for_exchange, created_at)
            VALUES ({bookspotId}, {validatorId}, {knowsPlace}, {safeForExchange}, NOW())");
    }

    private async Task<BookspotStatus> GetBookspotStatus(int bookspotId) =>
        await _db.Bookspots
            .Where(b => b.Id == bookspotId)
            .Select(b => b.Status)
            .FirstAsync();

    // ════════════════════════════════════════════════
    // TEST-BV01: Request nulo o BookspotId <= 0
    // ════════════════════════════════════════════════

    [Fact]
    public async Task CreateAsync_NullRequest_ThrowsValidationException()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA");

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateAsync(SupabaseA, null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateAsync_InvalidBookspotId_ThrowsValidationException(int bookspotId)
    {
        await SeedUserRaw(UserA, SupabaseA, "userA");

        var request = new CreateBookspotValidationRequest
        {
            BookspotId = bookspotId,
            KnowsPlace = true,
            SafeForExchange = true
        };

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateAsync(SupabaseA, request));
    }

    // ════════════════════════════════════════════════
    // TEST-BV02: Bookspot inexistente
    // ════════════════════════════════════════════════

    [Fact]
    public async Task CreateAsync_BookspotNotFound_ThrowsNotFoundException()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA");

        var request = new CreateBookspotValidationRequest
        {
            BookspotId = 99999,
            KnowsPlace = true,
            SafeForExchange = true
        };

        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.CreateAsync(SupabaseA, request));
    }

    // ════════════════════════════════════════════════
    // TEST-BV03: Bookspot no está en PENDING
    // ════════════════════════════════════════════════

    [Theory]
    [InlineData(BookspotStatus.ACTIVE)]
    [InlineData(BookspotStatus.REJECTED)]
    public async Task CreateAsync_BookspotNotPending_ThrowsValidationException(BookspotStatus status)
    {
        await SeedUserRaw(UserA, SupabaseA, "userA");
        await SeedUserRaw(UserB, SupabaseB, "userB");
        var bookspotId = await SeedBookspotRaw(UserA, status);

        var request = new CreateBookspotValidationRequest
        {
            BookspotId = bookspotId,
            KnowsPlace = true,
            SafeForExchange = true
        };

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateAsync(SupabaseB, request));
    }

    // ════════════════════════════════════════════════
    // TEST-BV04: Autovalidación prohibida
    // ════════════════════════════════════════════════

    [Fact]
    public async Task CreateAsync_SelfValidation_ThrowsValidationException()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA");
        var bookspotId = await SeedBookspotRaw(UserA);

        var request = new CreateBookspotValidationRequest
        {
            BookspotId = bookspotId,
            KnowsPlace = true,
            SafeForExchange = true
        };

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateAsync(SupabaseA, request));
    }

    // ════════════════════════════════════════════════
    // TEST-BV05: Validación duplicada
    // ════════════════════════════════════════════════

    [Fact]
    public async Task CreateAsync_DuplicateValidation_ThrowsValidationException()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA");
        await SeedUserRaw(UserB, SupabaseB, "userB");
        var bookspotId = await SeedBookspotRaw(UserA);
        await SeedValidationRaw(bookspotId, UserB, knowsPlace: true, safeForExchange: true);

        var request = new CreateBookspotValidationRequest
        {
            BookspotId = bookspotId,
            KnowsPlace = true,
            SafeForExchange = true
        };

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateAsync(SupabaseB, request));
    }

    // ════════════════════════════════════════════════
    // TEST-BV06: Validación creada correctamente
    // ════════════════════════════════════════════════

    [Fact]
    public async Task CreateAsync_ValidRequest_ReturnsDto()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA");
        await SeedUserRaw(UserB, SupabaseB, "userB");
        var bookspotId = await SeedBookspotRaw(UserA);

        var request = new CreateBookspotValidationRequest
        {
            BookspotId = bookspotId,
            KnowsPlace = true,
            SafeForExchange = true
        };

        var result = await _service.CreateAsync(SupabaseB, request);

        Assert.True(result.KnowsPlace);
        Assert.True(result.SafeForExchange);
        Assert.Equal(1, await _db.BookspotValidations.CountAsync());
    }

    // ════════════════════════════════════════════════
    // TEST-BV07: KnowsPlace = false no cuenta para el umbral
    // ════════════════════════════════════════════════

    [Fact]
    public async Task CreateAsync_KnowsPlaceFalse_DoesNotAffectStatus()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA");
        await SeedUserRaw(UserB, SupabaseB, "userB");
        var bookspotId = await SeedBookspotRaw(UserA);

        var request = new CreateBookspotValidationRequest
        {
            BookspotId = bookspotId,
            KnowsPlace = false,  // no conoce el lugar → no cuenta
            SafeForExchange = true
        };

        await _service.CreateAsync(SupabaseB, request);

        Assert.Equal(BookspotStatus.PENDING, await GetBookspotStatus(bookspotId));
    }

    // ════════════════════════════════════════════════
    // TEST-BV08: 5 validaciones positivas → ACTIVE
    // ════════════════════════════════════════════════

    [Fact]
    public async Task CreateAsync_FivePositiveValidations_ActivatesBookspot()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA");

        // Sembramos 4 validadores previos (B, C, y 2 más)
        var validators = new[]
        {
            (new Guid("11111111-1111-1111-1111-111111111111"), "sup-v1", "v1"),
            (new Guid("22222222-2222-2222-2222-222222222222"), "sup-v2", "v2"),
            (new Guid("33333333-3333-3333-3333-333333333333"), "sup-v3", "v3"),
            (new Guid("44444444-4444-4444-4444-444444444444"), "sup-v4", "v4"),
            (new Guid("55555555-5555-5555-5555-555555555555"), "sup-v5", "v5"),
        };

        foreach (var (id, sup, name) in validators)
            await SeedUserRaw(id, sup, name);

        var bookspotId = await SeedBookspotRaw(UserA);

        // Sembramos 4 validaciones positivas directamente
        foreach (var (id, _, _) in validators[..4])
            await SeedValidationRaw(bookspotId, id, knowsPlace: true, safeForExchange: true);

        // La quinta la hace el servicio → debe activar el bookspot
        var (fifthId, fifthSup, _) = validators[4];
        var request = new CreateBookspotValidationRequest
        {
            BookspotId = bookspotId,
            KnowsPlace = true,
            SafeForExchange = true
        };

        await _service.CreateAsync(fifthSup, request);

        Assert.Equal(BookspotStatus.ACTIVE, await GetBookspotStatus(bookspotId));
        // Las validaciones se borran al activar
        Assert.Equal(0, await _db.BookspotValidations.CountAsync());
    }

    // ════════════════════════════════════════════════
    // TEST-BV09: 5 validaciones negativas → REJECTED
    // ════════════════════════════════════════════════

    [Fact]
    public async Task CreateAsync_FiveNegativeValidations_RejectsBookspot()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA");

        var validators = new[]
        {
            (new Guid("aaaaaaaa-0000-0000-0000-aaaaaaaaaaaa"), "sup-n1", "n1"),
            (new Guid("bbbbbbbb-0000-0000-0000-bbbbbbbbbbbb"), "sup-n2", "n2"),
            (new Guid("cccccccc-0000-0000-0000-cccccccccccc"), "sup-n3", "n3"),
            (new Guid("dddddddd-0000-0000-0000-dddddddddddd"), "sup-n4", "n4"),
            (new Guid("eeeeeeee-0000-0000-0000-eeeeeeeeeeee"), "sup-n5", "n5"),
        };

        foreach (var (id, sup, name) in validators)
            await SeedUserRaw(id, sup, name);

        var bookspotId = await SeedBookspotRaw(UserA);

        foreach (var (id, _, _) in validators[..4])
            await SeedValidationRaw(bookspotId, id, knowsPlace: true, safeForExchange: false);

        var (fifthId, fifthSup, _) = validators[4];
        var request = new CreateBookspotValidationRequest
        {
            BookspotId = bookspotId,
            KnowsPlace = true,
            SafeForExchange = false
        };

        await _service.CreateAsync(fifthSup, request);

        Assert.Equal(BookspotStatus.REJECTED, await GetBookspotStatus(bookspotId));
        Assert.Equal(0, await _db.BookspotValidations.CountAsync());
    }

    // ════════════════════════════════════════════════
    // TEST-BV10: GetByBookspotId
    // ════════════════════════════════════════════════

    [Fact]
    public async Task GetByBookspotIdAsync_ReturnsAllValidations()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA");
        await SeedUserRaw(UserB, SupabaseB, "userB");
        await SeedUserRaw(UserC, SupabaseC, "userC");
        var bookspotId = await SeedBookspotRaw(UserA);
        await SeedValidationRaw(bookspotId, UserB, knowsPlace: true, safeForExchange: true);
        await SeedValidationRaw(bookspotId, UserC, knowsPlace: false, safeForExchange: false);

        var result = await _service.GetByBookspotIdAsync(bookspotId);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetByBookspotIdAsync_NoValidations_ReturnsEmpty()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA");
        var bookspotId = await SeedBookspotRaw(UserA);

        var result = await _service.GetByBookspotIdAsync(bookspotId);

        Assert.Empty(result);
    }

    // ════════════════════════════════════════════════
    // TEST-BV11: GetById
    // ════════════════════════════════════════════════

    [Fact]
    public async Task GetByIdAsync_ExistingValidation_ReturnsDto()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA");
        await SeedUserRaw(UserB, SupabaseB, "userB");
        var bookspotId = await SeedBookspotRaw(UserA);
        await SeedValidationRaw(bookspotId, UserB, knowsPlace: true, safeForExchange: true);

        var validationId = await _db.BookspotValidations
            .OrderByDescending(v => v.Id)
            .Select(v => v.Id)
            .FirstAsync();

        var result = await _service.GetByIdAsync(validationId);

        Assert.True(result.KnowsPlace);
        Assert.True(result.SafeForExchange);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ThrowsNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.GetByIdAsync(99999));
    }

    // ════════════════════════════════════════════════
    // TEST-BV12: Usuario desconocido
    // ════════════════════════════════════════════════

    [Fact]
    public async Task CreateAsync_UnknownSupabaseId_ThrowsNotFoundException()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA");
        var bookspotId = await SeedBookspotRaw(UserA);

        var request = new CreateBookspotValidationRequest
        {
            BookspotId = bookspotId,
            KnowsPlace = true,
            SafeForExchange = true
        };

        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.CreateAsync("sup-desconocido", request));
    }
}