using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Npgsql.NameTranslation;
using Testcontainers.PostgreSql;
using Xunit;

namespace Bookmerang.Tests.Helpers;

public class WebAppFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgis/postgis:16-3.4")
        .WithDatabase("bookmerang_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    internal WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var connectionString = _container.GetConnectionString();

        await RunMigrationsAsync(connectionString);
        RegisterNpgsqlEnums();

        // Program.cs lee env vars directamente, hay que setearlas antes de crear el factory
        Environment.SetEnvironmentVariable("CONNECTION_STRING", connectionString);
        Environment.SetEnvironmentVariable("JWT_SECRET", "test-secret-key-at-least-32-characters-long!!");
        Environment.SetEnvironmentVariable("JWT_ISSUER", "bookmerang-api");
        Environment.SetEnvironmentVariable("JWT_AUDIENCE", "bookmerang-client");
        Environment.SetEnvironmentVariable("JWT_ACCESS_TOKEN_MINUTES", "60");
        Environment.SetEnvironmentVariable("SUPABASE_JWT_SECRET", "fake-supabase-secret");
        Environment.SetEnvironmentVariable("SUPABASE_URL", "https://fake.supabase.co");
        Environment.SetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY", "fake-key");

        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
                builder.UseSetting("Auth:JwtSecret", "test-secret-key-at-least-32-characters-long!!");
                builder.UseSetting("Auth:JwtIssuer", "bookmerang-api");
                builder.UseSetting("Auth:JwtAudience", "bookmerang-client");
                builder.UseSetting("JWT_SECRET", "test-secret-key-at-least-32-characters-long!!");

                builder.ConfigureServices(services =>
                {
                    // Eliminar el DbContext existente y reemplazar con el del contenedor
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                    if (descriptor != null) services.Remove(descriptor);

                    var dataSourceDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(NpgsqlDataSource));
                    if (dataSourceDescriptor != null) services.Remove(dataSourceDescriptor);

                    services.AddDbContext<AppDbContext>(options =>
                        options.UseNpgsql(connectionString, o => o.UseNetTopologySuite()));
                });

                builder.UseEnvironment("Development");
            });
    }

    public async Task DisposeAsync()
    {
        Factory?.Dispose();
        await _container.DisposeAsync();
    }

    private static void RegisterNpgsqlEnums()
    {
        var t = new NpgsqlNullNameTranslator();

#pragma warning disable CS0618
        Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<ChatType>("chat_type", t);
        Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<BooksExtension>("books_extension", t);
        Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<BookStatus>("book_status", t);
        Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<BookCondition>("book_condition", t);
        Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<CoverType>("cover_type", t);
        Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<SwipeDirection>("swipe_direction", t);
        Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<MatchStatus>("match_status", t);
        Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<ExchangeStatus>("exchange_status", t);
        Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<ExchangeMode>("exchange_mode", t);
        Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<ExchangeMeetingStatus>("exchange_meeting_status", t);
        Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<BookspotStatus>("bookspot_status", t);
        Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<BaseUserType>("base_user_type", t);
        Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<CommunityStatus>("community_status", t);
        Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<CommunityRole>("community_role", t);
        Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<MeetupStatus>("meetup_status", t);
        Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<MeetupAttendanceStatus>("meetup_attendance_status", t);
        Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<PricingPlan>("pricing_plan", t);
        Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<BookdropExchangeStatus>("bookdrop_exchange_status", t);
        Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<SubscriptionStatus>("subscription_status", t);
        Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<SubscriptionPlatform>("subscription_platform", t);
        Npgsql.NpgsqlConnection.GlobalTypeMapper.MapEnum<InkdropsActionType>("inkdrops_action_type", t);
#pragma warning restore CS0618
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
            "20260413083046_add_cosmetics_to_user_progress.sql",
            "20260414110000_0010_subscriptions_base_user_fk.sql",
            "20260414113000_0010_add_tutorial_completed_to_users.sql",
            "20260414120001_0011_allow_multiple_matches_per_pair.sql",
            "20260501000000_0014_add_password_reset_fields.sql"
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
