using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using Xunit;

using static Bookmerang.Tests.Helpers.AuthTestHelper;
using MatchEntity = Bookmerang.Api.Models.Entities.Match;

namespace Bookmerang.Tests.Exchanges;

public class ExchangeControllerTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();
    private readonly WebAppFixture _fixture = fixture;

    // ── Helpers ──────────────────────────────────────────────────────

    private record ExchangeTestData(
        string Token1, string Token2,
        Guid User1Id, Guid User2Id,
        int ExchangeId, int ChatId);

    private async Task<ExchangeTestData> SeedExchangeData(
        string prefix, ExchangeStatus status = ExchangeStatus.NEGOTIATING)
    {
        var (token1, user1Id) = await RegisterUser(_client, $"{prefix}_u1@test.com", $"{prefix}_u1");
        var (token2, user2Id) = await RegisterUser(_client, $"{prefix}_u2@test.com", $"{prefix}_u2");

        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var book1 = new Book { OwnerId = user1Id, Status = BookStatus.PUBLISHED };
        var book2 = new Book { OwnerId = user2Id, Status = BookStatus.PUBLISHED };
        db.Books.AddRange(book1, book2);
        await db.SaveChangesAsync();

        var chat = new Chat { Type = ChatType.EXCHANGE, CreatedAt = DateTime.UtcNow };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        var match = new MatchEntity
        {
            User1Id = user1Id, User2Id = user2Id,
            Book1Id = book1.Id, Book2Id = book2.Id,
            Status = MatchStatus.CHAT_CREATED,
            CreatedAt = DateTime.UtcNow
        };
        db.Matches.Add(match);
        await db.SaveChangesAsync();

        var exchange = new Exchange { ChatId = chat.Id, MatchId = match.Id, Status = status };
        db.Exchanges.Add(exchange);
        await db.SaveChangesAsync();

        return new ExchangeTestData(token1, token2, user1Id, user2Id, exchange.ExchangeId, chat.Id);
    }

    // ── GetExchange ─────────────────────────────────────────────────

    [Fact]
    public async Task GetExchange_Exists_ReturnsOkWithDto()
    {
        var data = await SeedExchangeData("get_exch");
        SetAuth(_client, data.Token1);

        var response = await _client.GetAsync($"/api/exchange/{data.ExchangeId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(data.ExchangeId, body.GetProperty("exchangeId").GetInt32());
        ClearAuth(_client);
    }

    // ── GetExchangeByChatId ─────────────────────────────────────────

    [Fact]
    public async Task GetExchangeByChatId_Exists_ReturnsOkWithDto()
    {
        var data = await SeedExchangeData("get_by_chat");
        SetAuth(_client, data.Token1);

        var response = await _client.GetAsync($"/api/exchange/byChat/{data.ChatId}/withMatch");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(data.ChatId, body.GetProperty("chatId").GetInt32());
        ClearAuth(_client);
    }

    // ── AcceptExchange ──────────────────────────────────────────────

    [Fact]
    public async Task AcceptExchange_Valid_ReturnsOkWithAcceptedBy1()
    {
        var data = await SeedExchangeData("accept_exch", ExchangeStatus.NEGOTIATING);
        SetAuth(_client, data.Token1);

        var response = await _client.PatchAsync($"/api/exchange/{data.ExchangeId}/accept", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ACCEPTED_BY_1", body.GetProperty("status").GetString());
        ClearAuth(_client);
    }

    // ── RejectExchange ──────────────────────────────────────────────

    [Fact]
    public async Task RejectExchange_Negotiating_ReturnsOkWithRejected()
    {
        var data = await SeedExchangeData("reject_exch", ExchangeStatus.NEGOTIATING);
        SetAuth(_client, data.Token1);

        var response = await _client.PatchAsync($"/api/exchange/{data.ExchangeId}/reject", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("REJECTED", body.GetProperty("status").GetString());
        ClearAuth(_client);
    }

    [Fact]
    public async Task RejectExchange_AlreadyAccepted_ReturnsBadRequest()
    {
        var data = await SeedExchangeData("reject_accepted", ExchangeStatus.ACCEPTED);
        SetAuth(_client, data.Token1);

        var response = await _client.PatchAsync($"/api/exchange/{data.ExchangeId}/reject", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        ClearAuth(_client);
    }

    // ── ReportExchange ──────────────────────────────────────────────

    [Fact]
    public async Task ReportExchange_MeetingAccepted_ReturnsOkWithIncident()
    {
        var data = await SeedExchangeData("report_exch", ExchangeStatus.ACCEPTED);

        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ExchangeMeetings.Add(new ExchangeMeeting
        {
            ExchangeId = data.ExchangeId,
            ExchangeMode = ExchangeMode.BOOKSPOT,
            CustomLocation = new Point(0, 0) { SRID = 4326 },
            ScheduledAt = DateTime.UtcNow.AddHours(1),
            ProposerId = data.User1Id,
            MeetingStatus = ExchangeMeetingStatus.ACCEPTED
        });
        await db.SaveChangesAsync();

        SetAuth(_client, data.Token1);
        var response = await _client.PatchAsync($"/api/exchange/{data.ExchangeId}/report", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("INCIDENT", body.GetProperty("status").GetString());
        ClearAuth(_client);
    }

    [Fact]
    public async Task ReportExchange_NoAcceptedMeeting_ReturnsBadRequest()
    {
        var data = await SeedExchangeData("report_no_mtg", ExchangeStatus.ACCEPTED);
        SetAuth(_client, data.Token1);

        var response = await _client.PatchAsync($"/api/exchange/{data.ExchangeId}/report", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        ClearAuth(_client);
    }
}
