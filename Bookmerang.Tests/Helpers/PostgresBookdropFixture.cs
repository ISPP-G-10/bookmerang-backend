using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Implementation.Auth;
using Bookmerang.Api.Services.Implementation.Bookdrop;
using Bookmerang.Api.Services.Implementation.Inkdrops;
using Bookmerang.Api.Services.Implementation.Leveling;
using Bookmerang.Api.Services.Interfaces.Streaks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Npgsql.NameTranslation;
using Testcontainers.PostgreSql;
using Moq;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Services.Interfaces.Inkdrops;
using Xunit;

namespace Bookmerang.Tests.Helpers;

public class PostgresBookdropFixture : IAsyncLifetime
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

    public AuthService CreateAuthService(AppDbContext db)
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Auth:JwtSecret", "test-secret-key-at-least-32-characters-long!!" },
                { "Auth:JwtIssuer", "bookmerang-api" },
                { "Auth:JwtAudience", "bookmerang-client" }
            })
            .Build();

        var levelingService = new LevelingService(db);
        
        var inkdropsServiceMock = new Mock<IInkdropsService>();
        inkdropsServiceMock.Setup(s => s.GetUserInkdropsAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new InkdropsDto(Guid.Empty, 0, DateTime.UtcNow.ToString("yyyy-MM")));
            
        return new AuthService(db, config, levelingService, inkdropsServiceMock.Object);
    }

    public BookdropService CreateBookdropService(AppDbContext db)
    {
        return new BookdropService(db);
    }

    public InkdropsService CreateInkdropsService(AppDbContext db, IStreakService streakService)
    {
        return new InkdropsService(db, streakService);
    }

    private static NpgsqlDataSource BuildDataSource(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.UseNetTopologySuite();

        var t = new NpgsqlNullNameTranslator();

        builder.MapEnum<BookspotStatus>("bookspot_status", t);
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
        builder.MapEnum<CommunityStatus>("community_status", t);
        builder.MapEnum<CommunityRole>("community_role", t);
        builder.MapEnum<MeetupStatus>("meetup_status", t);
        builder.MapEnum<MeetupAttendanceStatus>("meetup_attendance_status", t);
        builder.MapEnum<PricingPlan>("pricing_plan", t);
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
