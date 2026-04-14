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

namespace Bookmerang.Tests.Bookdrop;

public class BookdropControllerTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    // ── Bookdrop NO puede acceder a endpoints de usuario ──

    [Theory]
    [InlineData("GET", "/api/auth/me")]
    [InlineData("GET", "/api/auth/perfil")]
    [InlineData("DELETE", "/api/auth/perfil")]
    public async Task BookdropToken_CannotAccessUserEndpoints_Returns403(string method, string url)
    {
        var (token, _) = await RegisterBookdrop(_client,
            $"sec7_{method}_{url.Replace("/", "_")}@test.com",
            $"sec7_{method}_{url.Replace("/", "_")}",
            "Local Sec 7");

        SetAuth(_client, token);

        var request = new HttpRequestMessage(new HttpMethod(method), url);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        ClearAuth(_client);
    }

    // ── Usuario normal NO puede acceder a endpoints de bookdrop ──

    [Theory]
    [InlineData("GET", "/api/bookdrop/perfil")]
    [InlineData("DELETE", "/api/bookdrop/perfil")]
    public async Task UserToken_CannotAccessBookdropEndpoints_Returns403(string method, string url)
    {
        var (token, _) = await RegisterUser(_client,
            $"sec8_{method}_{url.Replace("/", "_")}@test.com",
            $"sec8_{method}_{url.Replace("/", "_")}");

        SetAuth(_client, token);

        var request = new HttpRequestMessage(new HttpMethod(method), url);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        ClearAuth(_client);
    }

    // ── Registro normal NO permite crear admin ──

    [Fact]
    public async Task Register_WithAdminType_CreatesUserTypeInstead()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "hacker_test@test.com",
            password = "Hack1234",
            username = "hacker_test",
            name = "Hacker",
            profilePhoto = "photo.jpg",
            userType = 0, // ADMIN
            latitud = 37.3886,
            longitud = -5.9823
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var userType = body.GetProperty("user").GetProperty("userType").GetString();
        Assert.Equal("USER", userType);
    }

    // ── Bookdrop NO puede acceder a endpoints de admin ──

    [Theory]
    [InlineData("GET", "/api/admin/bookdrops")]
    public async Task BookdropToken_CannotAccessAdminEndpoints_Returns403(string method, string url)
    {
        var (token, _) = await RegisterBookdrop(_client,
            "sec14@test.com", "sec14_user", "Local Sec 14");

        SetAuth(_client, token);

        var request = new HttpRequestMessage(new HttpMethod(method), url);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        ClearAuth(_client);
    }

    // ── Register business devuelve 201 con token ──

    [Fact]
    public async Task RegisterBusiness_ValidRequest_Returns201WithToken()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/business", new
        {
            email = "positive_register@test.com",
            password = "Test1234",
            username = "positive_register",
            name = "Positive Owner",
            profilePhoto = (string?)null,
            nombreEstablecimiento = "Librería Positiva",
            addressText = "Calle Positiva 1, Sevilla",
            latitud = 37.3886,
            longitud = -5.9823
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("accessToken", out var tokenProp));
        Assert.False(string.IsNullOrEmpty(tokenProp.GetString()));

        Assert.True(body.TryGetProperty("user", out var userProp));
        Assert.Equal("BOOKDROP_USER", userProp.GetProperty("userType").GetString());
    }

    // ── CASO POSITIVO: GET /api/bookdrop/perfil con token bookdrop ──

    [Fact]
    public async Task GetBookdropPerfil_WithValidToken_Returns200()
    {
        var (token, _) = await RegisterBookdrop(_client,
            "positive_perfil@test.com", "positive_perfil", "Librería Perfil");

        SetAuth(_client, token);

        var response = await _client.GetAsync("/api/bookdrop/perfil");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Librería Perfil", body.GetProperty("nombreEstablecimiento").GetString());
        Assert.Equal("ACTIVE", body.GetProperty("bookspotStatus").GetString());
        ClearAuth(_client);
    }

    // ── CASO POSITIVO: PATCH /api/bookdrop/perfil ──

    [Fact]
    public async Task PatchBookdropPerfil_WithValidToken_Returns200()
    {
        var (token, _) = await RegisterBookdrop(_client,
            "positive_patch@test.com", "positive_patch", "Librería Patch");

        SetAuth(_client, token);

        var response = await _client.PatchAsJsonAsync("/api/bookdrop/perfil", new
        {
            nombreEstablecimiento = "Librería Actualizada"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Librería Actualizada", body.GetProperty("nombreEstablecimiento").GetString());
        ClearAuth(_client);
    }

    // ── CASO POSITIVO: DELETE /api/bookdrop/perfil ──

    [Fact]
    public async Task DeleteBookdropPerfil_WithValidToken_Returns200()
    {
        var (token, _) = await RegisterBookdrop(_client,
            "positive_delete@test.com", "positive_delete", "Librería Delete");

        SetAuth(_client, token);

        var response = await _client.DeleteAsync("/api/bookdrop/perfil");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verificar que ya no puede acceder
        var secondResponse = await _client.GetAsync("/api/bookdrop/perfil");
        Assert.Equal(HttpStatusCode.NotFound, secondResponse.StatusCode);
        ClearAuth(_client);
    }

    // ── CASO POSITIVO: Login bookdrop y acceso ──

    [Fact]
    public async Task Login_BookdropUser_CanAccessBookdropEndpoints()
    {
        await RegisterBookdrop(_client,
            "login_integ@test.com", "login_integ", "Librería Login");

        // Login por separado
        var token = await Login(_client, "login_integ@test.com");

        SetAuth(_client, token);
        var response = await _client.GetAsync("/api/bookdrop/perfil");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        ClearAuth(_client);
    }

    // ══════════════════════════════════════════════════════════════════
    //  BookDrop Exchange endpoints
    // ══════════════════════════════════════════════════════════════════

    private record ExchangeSeed(string BookdropToken, int MeetingId);

    private async Task<ExchangeSeed> SeedExchangeChain(
        string prefix, BookdropExchangeStatus dropStatus)
    {
        var (bdToken, bdUserId) = await RegisterBookdrop(_client,
            $"{prefix}_bd@t.com", $"{prefix}_bd", $"Local {prefix}");
        var (_, u1Id) = await RegisterUser(_client,
            $"{prefix}_u1@t.com", $"{prefix}_u1");
        var (_, u2Id) = await RegisterUser(_client,
            $"{prefix}_u2@t.com", $"{prefix}_u2");

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var bookdropUser = await db.BookdropUsers.FirstAsync(b => b.Id == bdUserId);
        var bookspotId = bookdropUser.BookSpotId;

        var book1 = new Book { OwnerId = u1Id, Status = BookStatus.PUBLISHED, Titulo = "Libro A" };
        var book2 = new Book { OwnerId = u2Id, Status = BookStatus.PUBLISHED, Titulo = "Libro B" };
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
            ExchangeMode = ExchangeMode.BOOKDROP,
            BookspotId = bookspotId,
            CustomLocation = new Point(-5.98, 37.39) { SRID = 4326 },
            ProposerId = u1Id,
            MeetingStatus = ExchangeMeetingStatus.ACCEPTED,
            Pin = "123456",
            BookDropStatus = dropStatus,
            ScheduledAt = DateTime.UtcNow.AddHours(1)
        };
        db.ExchangeMeetings.Add(meeting);
        await db.SaveChangesAsync();

        return new ExchangeSeed(bdToken, meeting.ExchangeMeetingId);
    }

    [Fact]
    public async Task GetActiveExchanges_Returns200WithList()
    {
        var seed = await SeedExchangeChain("gae", BookdropExchangeStatus.AWAITING_DROP_1);
        SetAuth(_client, seed.BookdropToken);

        var response = await _client.GetAsync("/api/bookdrop/exchanges");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetArrayLength() > 0);
        Assert.Equal("AWAITING_DROP_1", body[0].GetProperty("status").GetString());
        ClearAuth(_client);
    }

    [Fact]
    public async Task ConfirmDrop_Returns200WithBook1Held()
    {
        var seed = await SeedExchangeChain("cd", BookdropExchangeStatus.AWAITING_DROP_1);
        SetAuth(_client, seed.BookdropToken);

        var response = await _client.PostAsJsonAsync(
            $"/api/bookdrop/exchanges/{seed.MeetingId}/confirm-drop",
            new { Pin = "123456" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("BOOK_1_HELD", body.GetProperty("status").GetString());
        ClearAuth(_client);
    }

    [Fact]
    public async Task ConfirmSwap_Returns200WithBook2Held()
    {
        var seed = await SeedExchangeChain("cs", BookdropExchangeStatus.BOOK_1_HELD);
        SetAuth(_client, seed.BookdropToken);

        var response = await _client.PostAsJsonAsync(
            $"/api/bookdrop/exchanges/{seed.MeetingId}/confirm-swap",
            new { Pin = "123456" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("BOOK_2_HELD", body.GetProperty("status").GetString());
        ClearAuth(_client);
    }

    [Fact]
    public async Task ConfirmPickup_Returns200WithCompleted()
    {
        var seed = await SeedExchangeChain("cp", BookdropExchangeStatus.BOOK_2_HELD);
        SetAuth(_client, seed.BookdropToken);

        var response = await _client.PostAsJsonAsync(
            $"/api/bookdrop/exchanges/{seed.MeetingId}/confirm-pickup",
            new { Pin = "123456" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("COMPLETED", body.GetProperty("status").GetString());
        ClearAuth(_client);
    }
}
