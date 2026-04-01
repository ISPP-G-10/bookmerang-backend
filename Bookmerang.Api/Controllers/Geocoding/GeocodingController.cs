using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookmerang.Api.Controllers.Geocoding;

[ApiController]
[Route("api/geocoding")]
[Authorize]
public class GeocodingController(IHttpClientFactory httpClientFactory) : ControllerBase
{
    private const string NominatimBaseUrl = "https://nominatim.openstreetmap.org";

    [HttpGet("search")]
    public async Task<ActionResult<List<LocationSuggestionDto>>> Search([FromQuery] string q, [FromQuery] int limit = 5)
    {
        var query = q?.Trim() ?? string.Empty;
        if (query.Length < 3)
            return Ok(new List<LocationSuggestionDto>());

        var safeLimit = Math.Clamp(limit, 1, 10);
        var url =
            $"{NominatimBaseUrl}/search?format=jsonv2&addressdetails=1&accept-language=es&countrycodes=es&limit={safeLimit}&q={WebUtility.UrlEncode(query)}";

        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Bookmerang/1.0 (contact: support@bookmerang.local)");

        using var response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, "No se pudieron obtener sugerencias de ubicacion.");

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);

        var results = new List<LocationSuggestionDto>();

        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("place_id", out var placeIdProp)) continue;
            if (!item.TryGetProperty("display_name", out var displayNameProp)) continue;
            if (!item.TryGetProperty("lat", out var latProp)) continue;
            if (!item.TryGetProperty("lon", out var lonProp)) continue;

            var displayName = displayNameProp.GetString();
            var latText = latProp.GetString();
            var lonText = lonProp.GetString();

            if (string.IsNullOrWhiteSpace(displayName) ||
                !double.TryParse(latText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lat) ||
                !double.TryParse(lonText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lon))
            {
                continue;
            }

            results.Add(new LocationSuggestionDto(
                placeIdProp.ToString(),
                displayName,
                lat,
                lon
            ));
        }

        return Ok(results);
    }

    [HttpGet("reverse")]
    public async Task<ActionResult<ReverseGeocodingDto>> Reverse([FromQuery] double lat, [FromQuery] double lon)
    {
        var url =
            $"{NominatimBaseUrl}/reverse?format=jsonv2&accept-language=es&lat={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}&lon={lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Bookmerang/1.0 (contact: support@bookmerang.local)");

        using var response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, "No se pudo resolver la direccion.");

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);

        var displayName = document.RootElement.TryGetProperty("display_name", out var displayNameProp)
            ? (displayNameProp.GetString() ?? string.Empty)
            : string.Empty;

        return Ok(new ReverseGeocodingDto(displayName));
    }
}

public record LocationSuggestionDto(string Id, string Label, double Lat, double Lon);
public record ReverseGeocodingDto(string DisplayName);
