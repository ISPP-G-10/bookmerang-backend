using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Npgsql.NameTranslation;
using Testcontainers.PostgreSql;
using Xunit;

namespace Bookmerang.Tests.Helpers;

/// <summary>
/// Fixture PostgreSQL+PostGIS generalizada.
/// Ejecuta todas las migraciones de esquema y mapea todos los enums,
/// por lo que sirve como base para cualquier suite de tests de servicio.
/// </summary>
public class PostgresFixture : IAsyncLifetime
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
        builder.MapEnum<BaseUserType>           ("base_user_type",            t);
        builder.MapEnum<BookCondition>          ("book_condition",            t);
        builder.MapEnum<BookStatus>             ("book_status",               t);
        builder.MapEnum<BookdropExchangeStatus> ("bookdrop_exchange_status",  t);
        builder.MapEnum<BooksExtension>         ("books_extension",           t);
        builder.MapEnum<BookspotStatus>         ("bookspot_status",           t);
        builder.MapEnum<ChatType>               ("chat_type",                 t);
        builder.MapEnum<CommunityRole>          ("community_role",            t);
        builder.MapEnum<CommunityStatus>        ("community_status",          t);
        builder.MapEnum<CoverType>              ("cover_type",                t);
        builder.MapEnum<ExchangeMeetingStatus>  ("exchange_meeting_status",   t);
        builder.MapEnum<ExchangeMode>           ("exchange_mode",             t);
        builder.MapEnum<ExchangeStatus>         ("exchange_status",           t);
        builder.MapEnum<MatchStatus>            ("match_status",              t);
        builder.MapEnum<MeetupAttendanceStatus> ("meetup_attendance_status",  t);
        builder.MapEnum<MeetupStatus>           ("meetup_status",             t);
        builder.MapEnum<PricingPlan>            ("pricing_plan",              t);
        builder.MapEnum<SubscriptionStatus>     ("subscription_status",       t);
        builder.MapEnum<SubscriptionPlatform>   ("subscription_platform",     t);
        builder.MapEnum<SwipeDirection>         ("swipe_direction",           t);
        builder.MapEnum<InkdropsActionType>("inkdrops_action_type", t);

        return builder.Build();
    }

    private static async Task RunMigrationsAsync(string connectionString)
    {
        var migrationsDir = FindMigrationsDir();

        await using var plainSource = NpgsqlDataSource.Create(connectionString);
        await using var conn = await plainSource.OpenConnectionAsync();

        foreach (var file in new[]
        {
            "20260222163941_0001_extensions.sql",
            "20260222164018_0002_schema.sql",
            "20260222165524_0003_indexes.sql",
            "20260307120000_0005_add_typing_indicators.sql",
            "20260317110000_0004_match_pair_unique_index.sql",
            "20260329120000_0008_add_inkdrops.sql",
            "20260329120001_0008_subscriptions.sql",
            "20260405120000_0009_add_inkdrops_history.sql",
            "20260407000000_0008_bookdrop_exchange_status.sql",
            "20260408120000_0008_chats_uuid_ids.sql",
            "20260414110000_0010_subscriptions_base_user_fk.sql",
            "20260414113000_0010_add_tutorial_completed_to_users.sql",
            "20260413083046_add_cosmetics_to_user_progress.sql"
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
                "No se encontro supabase/migrations subiendo desde el binario de test.");

        return Path.Combine(dir.FullName, "supabase", "migrations");
    }
}
