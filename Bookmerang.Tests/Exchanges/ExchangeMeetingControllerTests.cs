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

public class ExchangeMeetingControllerTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();
    private readonly WebAppFixture _fixture = fixture;

    // ── Helpers ──────────────────────────────────────────────────────

    private record TestData(
        string Token1, string Token2,
        Guid User1Id, Guid User2Id,
        int ExchangeId, int? MeetingId);

    private async Task<TestData> Seed(
        string prefix,
        ExchangeStatus exchangeStatus = ExchangeStatus.ACCEPTED,
        ExchangeMeetingStatus? meetingStatus = null,
        ExchangeMode meetingMode = ExchangeMode.BOOKSPOT,
        bool proposerIsUser1 = false,
        bool markCompletedByUser2 = false)
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
            Status = MatchStatus.CHAT_CREATED, CreatedAt = DateTime.UtcNow
        };
        db.Matches.Add(match);
        await db.SaveChangesAsync();

        var exchange = new Exchange { ChatId = chat.Id, MatchId = match.Id, Status = exchangeStatus };
        db.Exchanges.Add(exchange);
        await db.SaveChangesAsync();

        int? meetingId = null;
        if (meetingStatus.HasValue)
        {
            var meeting = new ExchangeMeeting
            {
                ExchangeId = exchange.ExchangeId,
                ExchangeMode = meetingMode,
                CustomLocation = new Point(-3.0, 40.0) { SRID = 4326 },
                ScheduledAt = DateTime.UtcNow.AddHours(1),
                ProposerId = proposerIsUser1 ? user1Id : user2Id,
                MeetingStatus = meetingStatus.Value,
                MarkAsCompletedByUser2 = markCompletedByUser2
            };
            db.ExchangeMeetings.Add(meeting);
            await db.SaveChangesAsync();
            meetingId = meeting.ExchangeMeetingId;
        }

        return new TestData(token1, token2, user1Id, user2Id, exchange.ExchangeId, meetingId);
    }

    // ── Happy paths ─────────────────────────────────────────────────

    [Fact]
    public async Task GetExchangeMeeting_Exists_ReturnsOk()
    {
        var d = await Seed("get_mtg", meetingStatus: ExchangeMeetingStatus.PROPOSAL);
        SetAuth(_client, d.Token1);

        var response = await _client.GetAsync($"/api/exchangemeeting/{d.MeetingId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(d.MeetingId, body.GetProperty("exchangeMeetingId").GetInt32());
        ClearAuth(_client);
    }

    [Fact]
    public async Task GetMeetingByExchangeId_Exists_ReturnsOk()
    {
        var d = await Seed("get_mtg_exch", meetingStatus: ExchangeMeetingStatus.PROPOSAL);
        SetAuth(_client, d.Token1);

        var response = await _client.GetAsync($"/api/exchangemeeting/byExchange/{d.ExchangeId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(d.ExchangeId, body.GetProperty("exchangeId").GetInt32());
        ClearAuth(_client);
    }

    [Fact]
    public async Task CreateExchangeMeeting_Valid_ReturnsCreated()
    {
        var d = await Seed("create_mtg");
        SetAuth(_client, d.Token1);

        var response = await _client.PostAsJsonAsync("/api/exchangemeeting", new
        {
            exchangeId = d.ExchangeId,
            exchangeMode = "CUSTOM",
            bookspotId = (int?)null,
            latitud = 40.0,
            longitud = -3.0,
            scheduledAt = DateTime.UtcNow.AddHours(1)
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        ClearAuth(_client);
    }

    [Fact]
    public async Task CounterProposeMeeting_Valid_ReturnsOk()
    {
        // proposer=user2 por defecto -> user1 puede contra-proponer
        var d = await Seed("counter_mtg", meetingStatus: ExchangeMeetingStatus.PROPOSAL);
        SetAuth(_client, d.Token1);

        var response = await _client.PatchAsync(
            $"/api/exchangemeeting/{d.MeetingId}/counter-propose",
            JsonContent.Create(new
            {
                exchangeMode = "CUSTOM",
                bookspotId = (int?)null,
                latitud = 41.0,
                longitud = -4.0,
                scheduledAt = DateTime.UtcNow.AddHours(2)
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        ClearAuth(_client);
    }

    [Fact]
    public async Task CompleteExchange_Valid_ReturnsOk()
    {
        // proposer=user2 por defecto -> user1 marca como user2
        var d = await Seed("complete_mtg", meetingStatus: ExchangeMeetingStatus.ACCEPTED);
        SetAuth(_client, d.Token1);

        var response = await _client.PutAsync($"/api/exchangemeeting/{d.MeetingId}/complete", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        ClearAuth(_client);
    }

    [Fact]
    public async Task AcceptExchangeMeeting_Valid_ReturnsOk()
    {
        // proposer=user2 por defecto -> user1 puede aceptar
        var d = await Seed("accept_mtg", meetingStatus: ExchangeMeetingStatus.PROPOSAL);
        SetAuth(_client, d.Token1);

        var response = await _client.PutAsync($"/api/exchangemeeting/{d.MeetingId}/accept", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        ClearAuth(_client);
    }

    // ── Guardias de seguridad ───────────────────────────────────────

    [Fact]
    public async Task CreateExchangeMeeting_ExchangeNotAccepted_ReturnsBadRequest()
    {
        var d = await Seed("create_neg", exchangeStatus: ExchangeStatus.NEGOTIATING);
        SetAuth(_client, d.Token1);

        var response = await _client.PostAsJsonAsync("/api/exchangemeeting", new
        {
            exchangeId = d.ExchangeId,
            exchangeMode = "CUSTOM",
            bookspotId = (int?)null,
            latitud = 40.0,
            longitud = -3.0,
            scheduledAt = DateTime.UtcNow.AddHours(1)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        ClearAuth(_client);
    }

    [Fact]
    public async Task CreateExchangeMeeting_AlreadyExists_ReturnsConflict()
    {
        var d = await Seed("create_dup", meetingStatus: ExchangeMeetingStatus.PROPOSAL);
        SetAuth(_client, d.Token1);

        var response = await _client.PostAsJsonAsync("/api/exchangemeeting", new
        {
            exchangeId = d.ExchangeId,
            exchangeMode = "CUSTOM",
            bookspotId = (int?)null,
            latitud = 40.0,
            longitud = -3.0,
            scheduledAt = DateTime.UtcNow.AddHours(1)
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        ClearAuth(_client);
    }

    [Fact]
    public async Task CounterProposeMeeting_OwnProposal_ReturnsBadRequest()
    {
        // proposer=user1 -> user1 NO puede contra-proponer lo suyo
        var d = await Seed("counter_own", meetingStatus: ExchangeMeetingStatus.PROPOSAL, proposerIsUser1: true);
        SetAuth(_client, d.Token1);

        var response = await _client.PatchAsync(
            $"/api/exchangemeeting/{d.MeetingId}/counter-propose",
            JsonContent.Create(new
            {
                exchangeMode = "CUSTOM",
                bookspotId = (int?)null,
                latitud = 41.0,
                longitud = -4.0,
                scheduledAt = DateTime.UtcNow.AddHours(2)
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        ClearAuth(_client);
    }

    [Fact]
    public async Task CompleteExchange_BookdropMode_ReturnsBadRequest()
    {
        var d = await Seed("complete_bd",
            meetingStatus: ExchangeMeetingStatus.ACCEPTED,
            meetingMode: ExchangeMode.BOOKDROP);
        SetAuth(_client, d.Token1);

        var response = await _client.PutAsync($"/api/exchangemeeting/{d.MeetingId}/complete", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        ClearAuth(_client);
    }

    [Fact]
    public async Task CompleteExchange_AlreadyMarked_ReturnsBadRequest()
    {
        // user1 no es proposer -> controller comprueba MarkAsCompletedByUser2
        var d = await Seed("complete_dup",
            meetingStatus: ExchangeMeetingStatus.ACCEPTED,
            markCompletedByUser2: true);
        SetAuth(_client, d.Token1);

        var response = await _client.PutAsync($"/api/exchangemeeting/{d.MeetingId}/complete", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        ClearAuth(_client);
    }

    [Fact]
    public async Task AcceptExchangeMeeting_OwnProposal_ReturnsBadRequest()
    {
        // proposer=user1 -> user1 NO puede aceptar lo suyo
        var d = await Seed("accept_own",
            meetingStatus: ExchangeMeetingStatus.PROPOSAL,
            proposerIsUser1: true);
        SetAuth(_client, d.Token1);

        var response = await _client.PutAsync($"/api/exchangemeeting/{d.MeetingId}/accept", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        ClearAuth(_client);
    }
}
