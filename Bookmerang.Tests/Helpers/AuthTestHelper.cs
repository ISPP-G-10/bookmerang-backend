using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bookmerang.Tests.Helpers;

// Útil para test de integración de controladores que necesitan reutilizar auth
public static class AuthTestHelper
{
    public static async Task<(string token, Guid userId)> RegisterUser(
        HttpClient client, string email, string username)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Test1234",
            username,
            name = "Test User",
            profilePhoto = "photo.jpg",
            userType = 2,
            latitud = 37.3886,
            longitud = -5.9823
        });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("accessToken").GetString()!;
        var userId = Guid.Parse(body.GetProperty("user").GetProperty("id").GetString()!);
        return (token, userId);
    }

    public static async Task<(string token, Guid userId)> RegisterBookdrop(
        HttpClient client, string email, string username, string nombre)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register/business", new
        {
            email,
            password = "Test1234",
            username,
            name = "Test Owner",
            profilePhoto = (string?)null,
            nombreEstablecimiento = nombre,
            addressText = "Calle Test 1, Sevilla",
            latitud = 37.3886,
            longitud = -5.9823
        });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("accessToken").GetString()!;
        var userId = Guid.Parse(body.GetProperty("user").GetProperty("id").GetString()!);
        return (token, userId);
    }

    public static async Task<string> Login(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "Test1234"
        });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    public static void SetAuth(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    public static void ClearAuth(HttpClient client) =>
        client.DefaultRequestHeaders.Authorization = null;
}
