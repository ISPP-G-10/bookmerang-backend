using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bookmerang.Tests.Helpers;
using Xunit;

using static Bookmerang.Tests.Helpers.AuthTestHelper;

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
}
