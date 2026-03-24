using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bookmeran.Controllers;

[ApiController]
[Route("api/bookspots")]
public class BookspotsController(AppDbContext db) : ControllerBase
{
    [HttpGet("active")]
    public async Task<IActionResult> GetActive()
    {
        var spots = await db.Bookspots
            .AsNoTracking()
            .Where(spot => spot.Status == BookspotStatus.ACTIVE)
            .OrderBy(spot => spot.Nombre)
            .ToListAsync();

        var response = spots.Select(spot => new BookspotResponse(
            spot.Id,
            spot.Nombre,
            spot.AddressText ?? string.Empty,
            spot.Location?.Y ?? 0,
            spot.Location?.X ?? 0
        ));

        return Ok(response);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var spot = await db.Bookspots
            .AsNoTracking()
            .Where(item => item.Id == id && item.Status == BookspotStatus.ACTIVE)
            .FirstOrDefaultAsync();

        if (spot is null)
            return NotFound($"Bookspot con id {id} no encontrado.");

        return Ok(new BookspotResponse(
            spot.Id,
            spot.Nombre,
            spot.AddressText ?? string.Empty,
            spot.Location?.Y ?? 0,
            spot.Location?.X ?? 0
        ));
    }
}

public record BookspotResponse(
    int Id,
    string Nombre,
    string AddressText,
    double Latitude,
    double Longitude
);
