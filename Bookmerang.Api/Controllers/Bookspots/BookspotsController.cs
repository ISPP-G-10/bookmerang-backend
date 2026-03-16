using System.Security.Claims;
using Bookmerang.Api.Models.DTOs.Bookspots.Requests;
using Bookmerang.Api.Models.DTOs.Bookspots.Responses;
using Bookmerang.Api.Services.Interfaces.Bookspots;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Bookmerang.Api.Controllers.Bookspots;

[ApiController]
[Route("api/bookspots")]
[Authorize]
[Tags("Bookspots")]
public class BookspotsController(IBookspotService bookspotService) : ControllerBase
{
    // Usamos el mismo criterio que AuthController para evitar inconsistencias
    // entre claims (sub/nameidentifier) y asegurar que resolvemos al mismo usuario.
    private string SupabaseId =>
        User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
        ?? User.FindFirstValue("sub")
        ?? throw new Exception("No se encontró el supabaseId en el token JWT.");

    // 

    // GET /api/bookspots/active
    [HttpGet("active")]
    [SwaggerResponse(200, "Lista de bookspots activos", typeof(List<BookspotDTO>))]
    public async Task<ActionResult<List<BookspotDTO>>> GetActiveAsync(CancellationToken ct = default)
    {
        var bookspots = await bookspotService.GetActiveAsync(ct);
        return Ok(bookspots);
    }

    // GET /api/bookspots/nearby?latitude=37.38&longitude=-5.99&radiusKm=10
    [HttpGet("nearby")]
    [SwaggerResponse(200, "Lista de bookspots cercanos", typeof(List<BookspotNearbyDTO>))]
    public async Task<ActionResult<List<BookspotNearbyDTO>>> GetNearbyAsync(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromQuery] double radiusKm,
        CancellationToken ct)
    {
        var bookspots = await bookspotService.GetNearbyActiveAsync(latitude, longitude, radiusKm, ct);
        return Ok(bookspots);
    }

    // POST /api/bookspots
    [HttpPost]
    [SwaggerResponse(201, "Bookspot creado", typeof(BookspotDTO))]
    [SwaggerResponse(400, "Datos inválidos o duplicado cercano")]
    public async Task<ActionResult<BookspotDTO>> CreateAsync(
        [FromBody] CreateBookspotRequest request,
        CancellationToken ct)
    {
        var bookspot = await bookspotService.CreateAsync(SupabaseId, request, ct);
        return Created($"api/bookspots/{bookspot.Id}", bookspot);
    }

    // GET /api/bookspots/{id}
    [HttpGet("{id:int}")]
    [SwaggerResponse(200, "Bookspot por ID", typeof(BookspotDTO))]
    public async Task<ActionResult<BookspotDTO>> GetByIdAsync(int id, CancellationToken ct)
    {
        var bookspot = await bookspotService.GetByIdAsync(id, ct);
        if (bookspot is null) return NotFound();
        return Ok(bookspot);
    }
}