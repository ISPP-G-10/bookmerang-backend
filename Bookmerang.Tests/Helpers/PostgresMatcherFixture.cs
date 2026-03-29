using Bookmerang.Api.Configuration;
using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Implementation.Matcher;
using Bookmerang.Api.Services.Interfaces.Chats;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;
using Npgsql.NameTranslation;
using Testcontainers.PostgreSql;
using Xunit;

namespace Bookmerang.Tests.Helpers;

/// <summary>
/// Contenedor PostgreSQL+PostGIS por clase de test.
/// Es necesario para las pruebas del matcher ya que una BD en memoria calcula
/// la distancia de forma diferente.
/// </summary>
public class PostgresMatcherFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgis/postgis:16-3.4")
        .WithDatabase("bookmerang_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private NpgsqlDataSource _dataSource = null!;

    public static readonly MatcherSettings Settings = new()
    {
        Weights = new WeightsSettings
        {
            GenreMatch     = 0.40,
            ExtensionMatch = 0.10,
            DistanceScore  = 0.35,
            RecencyBonus   = 0.15
        },
        Feed = new FeedSettings
        {
            PriorityToDiscoveryRatio = 3,
            DefaultPageSize          = 20,
            RecencyDecayDays         = 30,
            SwipeValidDays           = 30
        }
    };

    // Ciclo de vida

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var connectionString = _container.GetConnectionString();

        // Las migraciones deben ejecutarse ANTES de construir el NpgsqlDataSource
        // con enum mappings.
        await RunMigrationsAsync(connectionString);

        _dataSource = BuildDataSource(connectionString);
    }

    public async Task DisposeAsync()
    {
        await _dataSource.DisposeAsync();
        await _container.DisposeAsync();
    }

    // Factoría de contextos y servicios

    public AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_dataSource, o => o.UseNetTopologySuite())
            .Options;
        return new AppDbContext(options);
    }

    public MatcherService CreateService(AppDbContext db) =>
        new(db,
            Options.Create(Settings),
            new Mock<ILogger<MatcherService>>().Object,
            new Mock<IChatService>().Object);

    public MatcherService CreateServiceWithChat(AppDbContext db, IChatService chatService) =>
        new(db,
            Options.Create(Settings),
            new Mock<ILogger<MatcherService>>().Object,
            chatService);

    // Construcción del NpgsqlDataSource

    private static NpgsqlDataSource BuildDataSource(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.UseNetTopologySuite();

        // Replicamos el mapeo de Program.cs
        var t = new NpgsqlNullNameTranslator();
        builder.MapEnum<ChatType>           ("chat_type",              t);
        builder.MapEnum<BooksExtension>     ("books_extension",        t);
        builder.MapEnum<BookStatus>         ("book_status",            t);
        builder.MapEnum<BookCondition>      ("book_condition",         t);
        builder.MapEnum<CoverType>          ("cover_type",             t);
        builder.MapEnum<SwipeDirection>     ("swipe_direction",        t);
        builder.MapEnum<MatchStatus>        ("match_status",           t);
        builder.MapEnum<ExchangeStatus>     ("exchange_status",        t);
        builder.MapEnum<ExchangeMode>       ("exchange_mode",          t);
        builder.MapEnum<ExchangeMeetingStatus>("exchange_meeting_status", t);
        builder.MapEnum<CommunityStatus>("community_status", t);
        builder.MapEnum<CommunityRole>("community_role", t);
        builder.MapEnum<MeetupStatus>("meetup_status", t);
        builder.MapEnum<MeetupAttendanceStatus>("meetup_attendance_status", t);
        builder.MapEnum<BookspotStatus>("bookspot_status", t);
        builder.MapEnum<PricingPlan>("pricing_plan", t);

        return builder.Build();
    }

    // Ejecución de migraciones

    private static async Task RunMigrationsAsync(string connectionString)
    {
        var migrationsDir = FindMigrationsDir();

        await using var plainSource = NpgsqlDataSource.Create(connectionString);
        await using var conn = await plainSource.OpenConnectionAsync();

        foreach (var file in new[]
        {
            "20260222163941_0001_extensions.sql",   // postgis, pgcrypto
            "20260222164018_0002_schema.sql",        // tablas y tipos enum
            "20260317110000_0004_match_pair_unique_index.sql",
            "20260329120000_0008_add_inkdrops.sql"
        })
        {
            var sql = await File.ReadAllTextAsync(Path.Combine(migrationsDir, file));
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Sube por el árbol de directorios desde el binario de test hasta encontrar
    /// la carpeta que contiene "supabase/migrations".
    /// </summary>
    private static string FindMigrationsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "supabase", "migrations")))
            dir = dir.Parent;

        if (dir == null)
            throw new DirectoryNotFoundException(
                "No se encontró el directorio supabase/migrations subiendo desde el binario de test.");

        return Path.Combine(dir.FullName, "supabase", "migrations");
    }
}
