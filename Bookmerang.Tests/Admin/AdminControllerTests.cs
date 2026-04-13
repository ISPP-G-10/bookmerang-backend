using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using Xunit;

using static Bookmerang.Tests.Helpers.AuthTestHelper;
using MatchEntity = Bookmerang.Api.Models.Entities.Match;

namespace Bookmerang.Tests.Admin;

public class AdminControllerTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    // ── Helpers ─────────────────────────────────────────────────────

    private async Task<string> SeedAdminAndLogin(string prefix)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var id = Guid.NewGuid();
        var email = $"{prefix}_admin@test.com";
        db.Users.Add(new BaseUser
        {
            Id = id,
            SupabaseId = $"supa_admin_{id:N}",
            Email = email,
            Username = $"{prefix}_admin",
            Name = "Admin User",
            ProfilePhoto = "photo.jpg",
            UserType = BaseUserType.ADMIN,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test1234"),
            Location = new Point(-5.98, 37.39) { SRID = 4326 }
        });
        await db.SaveChangesAsync();

        return await Login(_client, email);
    }

    private record ExchangeSeed(int ExchangeId, int MeetingId, Guid ProposerId);

    private async Task<ExchangeSeed> SeedExchangeWithMeeting(string prefix)
    {
        var (_, u1Id) = await RegisterUser(_client, $"{prefix}_u1@t.com", $"{prefix}_u1");
        var (_, u2Id) = await RegisterUser(_client, $"{prefix}_u2@t.com", $"{prefix}_u2");

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var book1 = new Book { OwnerId = u1Id, Status = BookStatus.PUBLISHED };
        var book2 = new Book { OwnerId = u2Id, Status = BookStatus.PUBLISHED };
        db.Books.AddRange(book1, book2);
        await db.SaveChangesAsync();

        var match = new MatchEntity
        {
            User1Id = u1Id, User2Id = u2Id,
            Book1Id = book1.Id, Book2Id = book2.Id,
            Status = MatchStatus.CHAT_CREATED, CreatedAt = DateTime.UtcNow
        };
        db.Matches.Add(match);
        await db.SaveChangesAsync();

        var chat = new Chat { Type = ChatType.EXCHANGE, CreatedAt = DateTime.UtcNow };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        var exchange = new Exchange { ChatId = chat.Id, MatchId = match.Id, Status = ExchangeStatus.ACCEPTED };
        db.Exchanges.Add(exchange);
        await db.SaveChangesAsync();

        var meeting = new ExchangeMeeting
        {
            ExchangeId = exchange.ExchangeId,
            ExchangeMode = ExchangeMode.CUSTOM,
            CustomLocation = new Point(-5.98, 37.39) { SRID = 4326 },
            ProposerId = u1Id,
            MeetingStatus = ExchangeMeetingStatus.PROPOSAL,
            ScheduledAt = DateTime.UtcNow.AddHours(1)
        };
        db.ExchangeMeetings.Add(meeting);
        await db.SaveChangesAsync();

        return new ExchangeSeed(exchange.ExchangeId, meeting.ExchangeMeetingId, u1Id);
    }

    // ══════════════════════════════════════════════════════════════════
    //  AdminBookdropController
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetAllBookdrops_Returns200WithList()
    {
        await RegisterBookdrop(_client, "adm_bd_list@t.com", "adm_bd_list", "Local List");
        var token = await SeedAdminAndLogin("bd_list");
        SetAuth(_client, token);

        var response = await _client.GetAsync("/api/admin/bookdrops");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetArrayLength() > 0);
        ClearAuth(_client);
    }

    [Fact]
    public async Task DeleteBookdrop_Returns200()
    {
        var (_, bdUserId) = await RegisterBookdrop(_client, "adm_bd_del@t.com", "adm_bd_del", "Local Del");
        var token = await SeedAdminAndLogin("bd_del");
        SetAuth(_client, token);

        var response = await _client.DeleteAsync($"/api/admin/bookdrops/{bdUserId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        ClearAuth(_client);
    }

    // ══════════════════════════════════════════════════════════════════
    //  AdminExchangeController
    // ══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetAllExchanges_Returns200WithList()
    {
        await SeedExchangeWithMeeting("adm_ex_list");
        var token = await SeedAdminAndLogin("ex_list");
        SetAuth(_client, token);

        var response = await _client.GetAsync("/api/admin/exchanges");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetArrayLength() > 0);
        ClearAuth(_client);
    }

    [Fact]
    public async Task DeleteExchange_Returns204()
    {
        var seed = await SeedExchangeWithMeeting("adm_ex_del");
        var token = await SeedAdminAndLogin("ex_del");
        SetAuth(_client, token);

        var response = await _client.DeleteAsync($"/api/admin/exchanges/{seed.ExchangeId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        ClearAuth(_client);
    }

    [Fact]
    public async Task GetAllMeetings_Returns200WithList()
    {
        await SeedExchangeWithMeeting("adm_mt_list");
        var token = await SeedAdminAndLogin("mt_list");
        SetAuth(_client, token);

        var response = await _client.GetAsync("/api/admin/exchanges/meetings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetArrayLength() > 0);
        ClearAuth(_client);
    }

    [Fact]
    public async Task GetMeetingsByUser_Returns200WithList()
    {
        var seed = await SeedExchangeWithMeeting("adm_mt_user");
        var token = await SeedAdminAndLogin("mt_user");
        SetAuth(_client, token);

        var response = await _client.GetAsync($"/api/admin/exchanges/meetings/byUser/{seed.ProposerId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetArrayLength() > 0);
        ClearAuth(_client);
    }
}
