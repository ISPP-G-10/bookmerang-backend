using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Npgsql.NameTranslation;
using Testcontainers.PostgreSql;
using Xunit;

namespace Bookmerang.Tests.Helpers;

public class PostgresChatsFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgis/postgis:16-3.4")
        .WithDatabase("bookmerang_test_chats")
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

    public AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_dataSource, o => o.UseNetTopologySuite())
            .Options;

        return new AppDbContext(options);
    }

    private static NpgsqlDataSource BuildDataSource(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.UseNetTopologySuite();

        var t = new NpgsqlNullNameTranslator();
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
        builder.MapEnum<CommunityStatus>("community_status", t);
        builder.MapEnum<CommunityRole>("community_role", t);
        builder.MapEnum<MeetupStatus>("meetup_status", t);
        builder.MapEnum<MeetupAttendanceStatus>("meetup_attendance_status", t);
        builder.MapEnum<BookspotStatus>("bookspot_status", t);
        builder.MapEnum<PricingPlan>("pricing_plan", t);

        return builder.Build();
    }

    private static async Task RunMigrationsAsync(string connectionString)
    {
        var migrationsDir = FindMigrationsDir();

        await using var plainSource = NpgsqlDataSource.Create(connectionString);
        await using var conn = await plainSource.OpenConnectionAsync();

        var migrationFiles = new[]
        {
            "20260222163941_0001_extensions.sql",
            "20260222164018_0002_schema.sql",
            "20260222165524_0003_indexes.sql",
            "20260317110000_0004_match_pair_unique_index.sql",
            "20260307120000_0005_add_typing_indicators.sql",
            "20260329120000_0008_add_inkdrops.sql",
            "20260408120000_0008_chats_uuid_ids.sql"
        };

        foreach (var file in migrationFiles)
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

        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "supabase", "migrations")))
            dir = dir.Parent;

        if (dir == null)
            throw new DirectoryNotFoundException(
                "No se encontró el directorio supabase/migrations subiendo desde el binario de test.");

        return Path.Combine(dir.FullName, "supabase", "migrations");
    }
}
