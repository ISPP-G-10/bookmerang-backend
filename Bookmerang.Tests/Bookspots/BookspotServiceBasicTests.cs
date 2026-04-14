// ============================================================
// Cómo ejecutar los tests de Bookspots
// Todos: dotnet test Bookmerang.Tests --filter "FullyQualifiedName~BookspotServiceBasicTests"
// Uno concreto: dotnet test Bookmerang.Tests --filter "FullyQualifiedName~CreateAsync_MonthlyLimitReached_ThrowsValidationException"
// Con logs: dotnet test Bookmerang.Tests --filter "FullyQualifiedName~CreateAsync_MonthlyLimitReached_ThrowsValidationException" --logger "console;verbosity=detailed"
// ============================================================

using Bookmerang.Api.Data;
using Bookmerang.Api.Exceptions;
using Bookmerang.Api.Models.DTOs.Bookspots.Requests;
using Bookmerang.Api.Models.DTOs.Bookspots.Responses;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Implementation.Bookspots;
using Bookmerang.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Xunit;
using Xunit.Abstractions;

namespace Bookmerang.Tests.Bookspots;

/// <summary>
/// Tests de integración del BookspotService contra PostgreSQL+PostGIS real.
/// Cubre creación, validaciones de negocio, geoespacial y borrado.
/// </summary>
public class BookspotServiceBasicTests(
    PostgresFixture fixture,
    ITestOutputHelper output)
    : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private AppDbContext _db = null!;
    private BookspotService _service = null!;

    // ── Coordenadas de referencia ──────────────────────────────────────

    private static readonly Point Madrid = MakePoint(-3.7038, 40.4168);
    private static readonly Point MadridCerca = MakePoint(-3.6938, 40.4168); // ~1 km
    private static readonly Point Lejano = MakePoint(-2.5, 40.4168); // ~100 km

    private static Point MakePoint(double lon, double lat) =>
        new GeometryFactory(new PrecisionModel(), 4326)
            .CreatePoint(new Coordinate(lon, lat));

    // ── IDs de usuarios de prueba ──────────────────────────────────────

    private static readonly Guid UserA = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserB = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private const string SupabaseA = "sup-bookspot-a";
    private const string SupabaseB = "sup-bookspot-b";

    // ── Ciclo de vida ──────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        _db = fixture.CreateDbContext();
        _service = new BookspotService(new BookspotRepository(_db), _db);

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

    private async Task SeedUserRaw(Guid id, string supabaseId, string username, Point location)
    {
        var lon = location.X.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var lat = location.Y.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var wkt = $"SRID=4326;POINT({lon} {lat})";

        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO base_users (id, supabase_id, email, username, nombre, foto_perfil_url, type, location, created_at, updated_at)
            VALUES ({id}, {supabaseId}, {username + "@test.com"}, {username}, {username}, '', 2, ST_GeomFromEWKT({wkt}), NOW(), NOW())");

        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO users (id) VALUES ({id})");
    }

    /// <summary>
    /// Inserta un bookspot por SQL interpolado y devuelve su id generado.
    /// Todo por SQL directo porque la columna type de base_users es int
    /// y EF no puede mapearlo al enum BaseUserType.
    /// </summary>
    private async Task<int> SeedBookspotRaw(
        Guid ownerId,
        Point? location = null,
        BookspotStatus status = BookspotStatus.PENDING,
        bool isBookdrop = false)
    {
        var loc = location ?? Madrid;
        var lon = loc.X.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var lat = loc.Y.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var wkt = $"SRID=4326;POINT({lon} {lat})";
        var nombre = $"Bookspot-{Guid.NewGuid():N}";

        // El cast ::bookspot_status no puede ir como parámetro — lo ponemos
        // como literal fijo en la query usando el ToString() del enum.
        var statusLiteral = status.ToString();

        await _db.Database.ExecuteSqlRawAsync(@$"
            INSERT INTO bookspots (nombre, address_text, location, is_bookdrop, created_by_user_id, status, created_at, updated_at)
            VALUES ('{nombre}', 'Calle Test 1', ST_GeomFromEWKT('{wkt}'), {isBookdrop.ToString().ToLower()}, '{ownerId}', '{statusLiteral}'::bookspot_status, NOW(), NOW())");

        return await _db.Bookspots
            .OrderByDescending(b => b.Id)
            .Select(b => b.Id)
            .FirstAsync();
    }

    private async Task<int> SeedBookdropBookspotRaw(
        Guid bookdropId,
        string supabaseId,
        string username,
        Point? location = null)
    {
        var loc = location ?? Madrid;
        var lon = loc.X.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var lat = loc.Y.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var wkt = $"SRID=4326;POINT({lon} {lat})";
        var nombre = $"Bookdrop-{Guid.NewGuid():N}";

        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO base_users (id, supabase_id, email, username, nombre, foto_perfil_url, type, location, created_at, updated_at)
            VALUES ({bookdropId}, {supabaseId}, {username + "@test.com"}, {username}, {username}, '', 1, ST_GeomFromEWKT({wkt}), NOW(), NOW())");

        await _db.Database.ExecuteSqlRawAsync(@$"
            INSERT INTO bookspots (nombre, address_text, location, is_bookdrop, created_by_user_id, status, created_at, updated_at)
            VALUES ('{nombre}', 'Calle Bookdrop 1', ST_GeomFromEWKT('{wkt}'), true, NULL, 'ACTIVE'::bookspot_status, NOW(), NOW())");

        var bookspotId = await _db.Bookspots
            .OrderByDescending(b => b.Id)
            .Select(b => b.Id)
            .FirstAsync();

        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO bookdrop_users (id, book_spot_id)
            VALUES ({bookdropId}, {bookspotId})");

        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE bookspots
            SET owner_id = {bookdropId}
            WHERE id = {bookspotId}");

        return bookspotId;
    }

    private void Log(IEnumerable<BookspotDTO> items)
    {
        foreach (var b in items)
            output.WriteLine(
                $"  Bookspot={b.Id} | Nombre={b.Nombre} | Status={b.Status} | Lat={b.Latitude} | Lon={b.Longitude}");
    }

    private void Log(IEnumerable<BookspotNearbyDTO> items)
    {
        foreach (var b in items)
            output.WriteLine(
                $"  Bookspot={b.Id} | Nombre={b.Nombre} | DistanceKm={b.DistanceKm}");
    }

    // ════════════════════════════════════════════════
    // TEST-BI01: Creación básica — queda en PENDING
    // ════════════════════════════════════════════════

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesWithStatusPending()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA", Madrid);

        var request = new CreateBookspotRequest
        {
            Nombre = "Mi Bookspot",
            AddressText = "Calle Mayor 1",
            Latitude = 40.4168,
            Longitude = -3.7038,
            IsBookdrop = false
        };

        var result = await _service.CreateAsync(SupabaseA, request);

        Assert.Equal("Mi Bookspot", result.Nombre);
        Assert.Equal(BookspotStatus.PENDING, result.Status);
        Assert.Equal(1, await _db.Bookspots.CountAsync());
    }

    // ════════════════════════════════════════════════
    // TEST-BI02: Límite mensual — 5 ya creados este mes
    // ════════════════════════════════════════════════

    [Fact]
    public async Task CreateAsync_MonthlyLimitReached_ThrowsValidationException()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA", Madrid);
        for (var i = 0; i < 5; i++)
            await SeedBookspotRaw(UserA, MakePoint(-3.70 - i * 0.01, 40.41));

        var request = new CreateBookspotRequest
        {
            Nombre = "Extra",
            AddressText = "Calle Extra",
            Latitude = 40.50,
            Longitude = -3.80,
            IsBookdrop = false
        };

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateAsync(SupabaseA, request));
    }

    // ════════════════════════════════════════════════
    // TEST-BI03: Duplicado geoespacial — mismo punto exacto
    // ════════════════════════════════════════════════

    [Fact]
    public async Task CreateAsync_ExactDuplicate_ThrowsValidationException()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA", Madrid);
        await SeedBookspotRaw(UserA, Madrid);

        var request = new CreateBookspotRequest
        {
            Nombre = "Otro en el mismo sitio",
            AddressText = "Calle Madrid",
            Latitude = Madrid.Y,
            Longitude = Madrid.X,
            IsBookdrop = false
        };

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateAsync(SupabaseA, request));
    }

    // ════════════════════════════════════════════════
    // TEST-BI04: Punto distinto — no es duplicado
    // ════════════════════════════════════════════════

    [Fact]
    public async Task CreateAsync_FarEnoughLocation_DoesNotThrow()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA", Madrid);
        await SeedBookspotRaw(UserA, Madrid);

        var request = new CreateBookspotRequest
        {
            Nombre = "Otro sitio",
            AddressText = "Calle Lejos",
            Latitude = MadridCerca.Y,
            Longitude = MadridCerca.X,
            IsBookdrop = false
        };

        var result = await _service.CreateAsync(SupabaseA, request);
        Assert.Equal(BookspotStatus.PENDING, result.Status);
    }

    // ════════════════════════════════════════════════
    // TEST-BI05: GetActiveAsync — solo devuelve ACTIVE
    // ════════════════════════════════════════════════

    [Fact]
    public async Task GetActiveAsync_OnlyReturnsActiveBookspots()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA", Madrid);
        await SeedBookspotRaw(UserA, status: BookspotStatus.ACTIVE);
        await SeedBookspotRaw(UserA, status: BookspotStatus.PENDING);
        await SeedBookspotRaw(UserA, status: BookspotStatus.REJECTED);

        var result = await _service.GetActiveAsync();
        Log(result);

        Assert.Single(result);
        Assert.Equal(BookspotStatus.ACTIVE, result[0].Status);
    }

    // ════════════════════════════════════════════════
    // TEST-BI06: GetPendingAsync — solo devuelve PENDING
    // ════════════════════════════════════════════════

    [Fact]
    public async Task GetPendingAsync_OnlyReturnsPendingBookspots()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA", Madrid);
        await SeedBookspotRaw(UserA, status: BookspotStatus.ACTIVE);
        await SeedBookspotRaw(UserA, status: BookspotStatus.PENDING);
        await SeedBookspotRaw(UserA, status: BookspotStatus.PENDING);

        var result = await _service.GetPendingAsync();
        Log(result);

        Assert.Equal(2, result.Count);
        Assert.All(result, b => Assert.Equal(BookspotStatus.PENDING, b.Status));
    }

    // ════════════════════════════════════════════════
    // TEST-BI07: GetNearbyActiveAsync — radio geoespacial real
    // ════════════════════════════════════════════════

    [Fact]
    public async Task GetNearbyActiveAsync_BookspotInsideRadius_IsReturned()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA", Madrid);
        await SeedBookspotRaw(UserA, MadridCerca, BookspotStatus.ACTIVE);

        var result = await _service.GetNearbyActiveAsync(Madrid.Y, Madrid.X, radiusKm: 10);
        Log(result);

        Assert.Single(result);
        Assert.True(result[0].DistanceKm < 2,
            $"Debería estar a menos de 2 km, pero está a {result[0].DistanceKm} km");
    }

    [Fact]
    public async Task GetNearbyActiveAsync_BookspotOutsideRadius_IsNotReturned()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA", Madrid);
        await SeedBookspotRaw(UserA, Lejano, BookspotStatus.ACTIVE);

        var result = await _service.GetNearbyActiveAsync(Madrid.Y, Madrid.X, radiusKm: 10);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetNearbyActiveAsync_OrderedByDistance()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA", Madrid);
        await SeedBookspotRaw(UserA, MakePoint(-3.65, 40.4168), BookspotStatus.ACTIVE); // ~4 km
        await SeedBookspotRaw(UserA, MadridCerca, BookspotStatus.ACTIVE); // ~1 km

        var result = await _service.GetNearbyActiveAsync(Madrid.Y, Madrid.X, radiusKm: 10);
        Log(result);

        Assert.Equal(2, result.Count);
        Assert.True(result[0].DistanceKm < result[1].DistanceKm,
            "El resultado debe estar ordenado de más cercano a más lejano");
    }

    [Fact]
    public async Task GetNearbyActiveAsync_BookdropWithoutCreatedBy_IsReturned()
    {
        await SeedBookdropBookspotRaw(
            new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            "sup-bookdrop-c",
            "bookdropC",
            MadridCerca);

        var result = await _service.GetNearbyActiveAsync(Madrid.Y, Madrid.X, radiusKm: 10);
        Log(result);

        Assert.Single(result);
        Assert.True(result[0].IsBookdrop);
        Assert.Equal("bookdropC", result[0].CreatorUsername);
    }

    [Fact]
    public async Task GetNearbyActiveAsync_RadiusExceedsMax_ThrowsValidationException()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => _service.GetNearbyActiveAsync(40.4, -3.7, radiusKm: 51));

        Assert.Contains("50", ex.Message);
    }

    // ════════════════════════════════════════════════
    // TEST-BI08: GetById
    // ════════════════════════════════════════════════

    [Fact]
    public async Task GetByIdAsync_ExistingBookspot_ReturnsDto()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA", Madrid);
        var bookspotId = await SeedBookspotRaw(UserA, Madrid, BookspotStatus.ACTIVE);

        var result = await _service.GetByIdAsync(bookspotId);

        Assert.NotNull(result);
        Assert.Equal(BookspotStatus.ACTIVE, result!.Status);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        var result = await _service.GetByIdAsync(99999);
        Assert.Null(result);
    }

    // ════════════════════════════════════════════════
    // TEST-BI09: DeleteAsync
    // ════════════════════════════════════════════════

    [Fact]
    public async Task DeleteAsync_Owner_RemovesBookspot()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA", Madrid);
        var bookspotId = await SeedBookspotRaw(UserA, Madrid);

        await _service.DeleteAsync(SupabaseA, bookspotId);

        Assert.Equal(0, await _db.Bookspots.CountAsync());
    }

    [Fact]
    public async Task DeleteAsync_NotOwner_ThrowsForbiddenException()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA", Madrid);
        await SeedUserRaw(UserB, SupabaseB, "userB", Madrid);
        var bookspotId = await SeedBookspotRaw(UserA, Madrid);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => _service.DeleteAsync(SupabaseB, bookspotId));
    }

    [Fact]
    public async Task DeleteAsync_NotFound_ThrowsNotFoundException()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA", Madrid);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.DeleteAsync(SupabaseA, 99999));
    }

    // ════════════════════════════════════════════════
    // TEST-BI10: GetRandomPendingNearbyAsync
    // ════════════════════════════════════════════════

    [Fact]
    public async Task GetRandomPendingNearbyAsync_WithCandidates_ReturnsOne()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA", Madrid);
        await SeedUserRaw(UserB, SupabaseB, "userB", Madrid);
        // Los bookspots son de UserA; UserB los valida
        await SeedBookspotRaw(UserA, MadridCerca, BookspotStatus.PENDING);
        await SeedBookspotRaw(UserA, MadridCerca, BookspotStatus.PENDING);

        var result = await _service.GetRandomPendingNearbyAsync(Madrid.Y, Madrid.X, radiusKm: 10, SupabaseB);

        Assert.NotNull(result);
        Assert.Equal(BookspotStatus.PENDING, result!.Status);
    }

    [Fact]
    public async Task GetRandomPendingNearbyAsync_NoCandidates_ReturnsNull()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA", Madrid);

        var result = await _service.GetRandomPendingNearbyAsync(Madrid.Y, Madrid.X, radiusKm: 10, SupabaseA);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetRandomPendingNearbyAsync_IgnoresActiveAndRejected()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA", Madrid);
        await SeedUserRaw(UserB, SupabaseB, "userB", Madrid);
        await SeedBookspotRaw(UserA, MadridCerca, BookspotStatus.ACTIVE);
        await SeedBookspotRaw(UserA, MadridCerca, BookspotStatus.REJECTED);

        var result = await _service.GetRandomPendingNearbyAsync(Madrid.Y, Madrid.X, radiusKm: 10, SupabaseB);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetRandomPendingNearbyAsync_ExcludesOwnBookspots()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA", Madrid);
        // UserA sube su propio bookspot; al pedirlo con su propia cuenta no debe recibirlo
        await SeedBookspotRaw(UserA, MadridCerca, BookspotStatus.PENDING);

        var result = await _service.GetRandomPendingNearbyAsync(Madrid.Y, Madrid.X, radiusKm: 10, SupabaseA);
        Assert.Null(result);
    }

    // ════════════════════════════════════════════════
    // TEST-BI11: GetUserPendingWithValidationCountAsync
    // ════════════════════════════════════════════════

    [Fact]
    public async Task GetUserPendingWithValidationCountAsync_ReturnsOnlyOwnPending()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA", Madrid);
        await SeedUserRaw(UserB, SupabaseB, "userB", Madrid);
        await SeedBookspotRaw(UserA, status: BookspotStatus.PENDING);
        await SeedBookspotRaw(UserA, status: BookspotStatus.ACTIVE);  // no debe aparecer
        await SeedBookspotRaw(UserB, status: BookspotStatus.PENDING); // de otro usuario

        var result = await _service.GetUserPendingWithValidationCountAsync(SupabaseA);
        Log(result);

        Assert.Single(result);
        Assert.Equal(BookspotStatus.PENDING, result[0].Status);
    }

    // ════════════════════════════════════════════════
    // TEST-BI12: GetUserActiveAsync
    // ════════════════════════════════════════════════

    [Fact]
    public async Task GetUserActiveAsync_ReturnsOnlyOwnActiveWithValidatedAt()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA", Madrid);
        await SeedUserRaw(UserB, SupabaseB, "userB", Madrid);
        await SeedBookspotRaw(UserA, status: BookspotStatus.ACTIVE);   // debe aparecer
        await SeedBookspotRaw(UserA, status: BookspotStatus.PENDING);  // no, sigue pending
        await SeedBookspotRaw(UserB, status: BookspotStatus.ACTIVE);   // no, de otro usuario

        var result = await _service.GetUserActiveAsync(SupabaseA);
        Log(result);

        Assert.Single(result);
        Assert.Equal(BookspotStatus.ACTIVE, result[0].Status);
        Assert.NotNull(result[0].ValidatedAt); // bookspots activos deben tener ValidatedAt
    }

    // ════════════════════════════════════════════════
    // TEST-BI13: UpdateNameAsync
    // ════════════════════════════════════════════════

    [Fact]
    public async Task UpdateNameAsync_Owner_PendingBookspot_UpdatesName()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA", Madrid);
        var bookspotId = await SeedBookspotRaw(UserA, status: BookspotStatus.PENDING);

        var result = await _service.UpdateNameAsync(SupabaseA, bookspotId, "Nombre Actualizado");

        Assert.Equal("Nombre Actualizado", result.Nombre);
        var inDb = await _db.Bookspots.FindAsync(bookspotId);
        Assert.Equal("Nombre Actualizado", inDb!.Nombre);
    }

    [Fact]
    public async Task UpdateNameAsync_NotOwner_ThrowsForbiddenException()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA", Madrid);
        await SeedUserRaw(UserB, SupabaseB, "userB", Madrid);
        var bookspotId = await SeedBookspotRaw(UserA, status: BookspotStatus.PENDING);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => _service.UpdateNameAsync(SupabaseB, bookspotId, "Otro Nombre"));
    }

    [Fact]
    public async Task UpdateNameAsync_ActiveBookspot_ThrowsValidationException()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA", Madrid);
        var bookspotId = await SeedBookspotRaw(UserA, status: BookspotStatus.ACTIVE);

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.UpdateNameAsync(SupabaseA, bookspotId, "Nuevo Nombre"));
    }

    // ════════════════════════════════════════════════
    // TEST-BI14: Auto-curación en GetUserPendingWithValidationCountAsync
    // ════════════════════════════════════════════════

    [Fact]
    public async Task GetUserPendingWithValidationCountAsync_AutoHeals_WhenFivePositiveValidations()
    {
        await SeedUserRaw(UserA, SupabaseA, "userA", Madrid);
        var bookspotId = await SeedBookspotRaw(UserA, status: BookspotStatus.PENDING);

        // Crear 5 validadores distintos con sus validaciones positivas
        for (var i = 1; i <= 5; i++)
        {
            var validatorId = new Guid($"dddddddd-dddd-dddd-dddd-{i:D12}");
            await SeedUserRaw(validatorId, $"sup-validator-{i}", $"validator{i}", Madrid);
            await _db.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO bookspot_validations (bookspot_id, validator_user_id, knows_place, safe_for_exchange, created_at)
                VALUES ({bookspotId}, {validatorId}, true, true, NOW())");
        }

        // La llamada debe auto-curar el bookspot: excluirlo de pending y activarlo
        var pending = await _service.GetUserPendingWithValidationCountAsync(SupabaseA);

        Assert.Empty(pending); // Ya no aparece en la lista de pending
        var inDb = await _db.Bookspots.FindAsync(bookspotId);
        Assert.Equal(BookspotStatus.ACTIVE, inDb!.Status); // El bookspot está activo
        // Las validaciones deben haber sido limpiadas tras la activación
        var validationsLeft = await _db.BookspotValidations
            .Where(v => v.BookspotId == bookspotId).ToListAsync();
        Assert.Empty(validationsLeft);
    }
}
