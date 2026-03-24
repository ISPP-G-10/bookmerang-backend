using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Implementation.Bookspots;
using Bookmerang.Api.Services.Interfaces.Bookspots;
using Microsoft.EntityFrameworkCore;
using Moq;
using Npgsql;
using Npgsql.NameTranslation;
using Testcontainers.PostgreSql;
using Xunit;

namespace Bookmerang.Tests.Helpers;

/// <summary>
/// Contenedor PostgreSQL+PostGIS por clase de test para el módulo Bookspots.
/// Necesario porque IBookspotRepository usa PostGIS para cálculos geoespaciales.
/// </summary>
public class PostgresBookspotFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgis/postgis:16-3.4")
        .WithDatabase("bookmerang_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private NpgsqlDataSource _dataSource = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var connectionString = _container.GetConnectionString();

        await RunMigrationsAsync(connectionString);

        _dataSource = BuildDataSource(connectionString);
    }

    public async Task DisposeAsync()
    {
        await _dataSource.DisposeAsync();
        await _container.DisposeAsync();
    }

    // ── Factoría de contextos y servicios ──────────────────────────────

    public AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_dataSource, o => o.UseNetTopologySuite())
            .Options;
        return new AppDbContext(options);
    }

    public BookspotService CreateService(AppDbContext db)
    {
        var repo = new BookspotRepository(db);
        return new BookspotService(repo, db);
    }

    public BookspotValidationService CreateValidationService(AppDbContext db)
    {
        var bookspotRepo = new BookspotRepository(db);
        var validationRepo = new BookspotValidationRepository(db);
        return new BookspotValidationService(validationRepo, bookspotRepo, db);
    }

    /// <summary>
    /// Variante que inyecta un IBookspotRepository mockeado — útil para
    /// tests que quieren controlar el repo sin ir a BD.
    /// </summary>
    public BookspotService CreateServiceWithRepo(
        AppDbContext db,
        IBookspotRepository repo)
        => new BookspotService(repo, db);

    // ── NpgsqlDataSource ───────────────────────────────────────────────

    private static NpgsqlDataSource BuildDataSource(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.UseNetTopologySuite();

        var t = new NpgsqlNullNameTranslator();

        // Enums compartidos con el resto de la app
        builder.MapEnum<BookspotStatus>("bookspot_status", t);

        // Enums del Matcher que el schema también define — mapear todos
        // evita errores si AppDbContext los referencia internamente
        builder.MapEnum<ChatType>("chat_type", t);
        builder.MapEnum<BooksExtension>("books_extension", t);
        builder.MapEnum<BookStatus>("book_status", t);
        builder.MapEnum<BookCondition>("book_condition", t);
        builder.MapEnum<CoverType>("cover_type", t);
        builder.MapEnum<SwipeDirection>("swipe_direction", t);
        builder.MapEnum<MatchStatus>("match_status", t);
        builder.MapEnum<ExchangeStatus>("exchange_status", t);
        builder.MapEnum<ExchangeMode>("exchange_mode", t);
        builder.MapEnum<ExchangeMeetingStatus>("exchange_meeting_status", t);
        builder.MapEnum<BaseUserType>("base_user_type", t);

        return builder.Build();
    }

    // ── Migraciones ────────────────────────────────────────────────────

    private static async Task RunMigrationsAsync(string connectionString)
    {
        var migrationsDir = FindMigrationsDir();

        await using var plainSource = NpgsqlDataSource.Create(connectionString);
        await using var conn = await plainSource.OpenConnectionAsync();

        foreach (var file in new[]
        {
            "20260222163941_0001_extensions.sql",
            "20260222164018_0002_schema.sql"
        })
        {
            var sql = await File.ReadAllTextAsync(Path.Combine(migrationsDir, file));
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static string FindMigrationsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null &&
               !Directory.Exists(Path.Combine(dir.FullName, "supabase", "migrations")))
            dir = dir.Parent;

        if (dir == null)
            throw new DirectoryNotFoundException(
                "No se encontró supabase/migrations subiendo desde el binario de test.");

        return Path.Combine(dir.FullName, "supabase", "migrations");
    }
}