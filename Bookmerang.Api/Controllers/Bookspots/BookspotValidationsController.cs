using System.Security.Claims;
using Bookmerang.Api.Models.DTOs.Bookspots.Requests;
using Bookmerang.Api.Models.DTOs.Bookspots.Responses;
using Bookmerang.Api.Services.Interfaces.Bookspots;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Bookmerang.Api.Controllers.Bookspots;

[ApiController]
[Route("api/bookspots/{bookspotId:int}/validations")]
[Authorize]
[Tags("Bookspot Validations")]
public class BookspotValidationsController(IBookspotValidationService validationService) : ControllerBase
{
    // Usamos el mismo criterio que AuthController para evitar inconsistencias
    // entre claims (sub/nameidentifier) y asegurar que resolvemos al mismo usuario.
    private string SupabaseId =>
        User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
        ?? User.FindFirstValue("sub")
        ?? throw new Exception("No se encontró el supabaseId en el token JWT.");

    // 

    // POST /api/bookspots/{bookspotId}/validations
    [HttpPost]
    [SwaggerResponse(201, "Validación creada", typeof(BookspotValidationDTO))]
    [SwaggerResponse(400, "Datos inválidos")]
    [SwaggerResponse(404, "Bookspot no encontrado")]
    public async Task<ActionResult<BookspotValidationDTO>> CreateAsync(
        [FromBody] CreateBookspotValidationRequest request,
        CancellationToken ct)
    {

        if (request is null || request.BookspotId <= 0)
            return BadRequest("Request inválido. Asegúrate de incluir un bookspotId válido.");

        var validation = await validationService.CreateAsync(SupabaseId, request, ct);
        return Created($"api/bookspots/{request.BookspotId}/validations/{validation.Id}", validation);
    }

    // GET /api/bookspots/{bookspotId}/validations
    [HttpGet]
    [SwaggerResponse(200, "Lista de validaciones para el bookspot", typeof(List<BookspotValidationDTO>))]
    [SwaggerResponse(404, "Bookspot no encontrado")]
    public async Task<ActionResult<List<BookspotValidationDTO>>> GetByBookspotIdAsync(
        [FromRoute] int bookspotId,
        CancellationToken ct)
    {
        var validations = await validationService.GetByBookspotIdAsync(bookspotId, ct);
        return Ok(validations);
    }

    // GET /api/bookspots/{bookspotId}/validations/{validationId}
    [HttpGet("{validationId}")]
    [SwaggerResponse(200, "Validación encontrada", typeof(BookspotValidationDTO))]
    [SwaggerResponse(404, "Validación no encontrada")]
    public async Task<ActionResult<BookspotValidationDTO>> GetByIdAsync(
        [FromRoute] int validationId,
        CancellationToken ct)
    {
        var validation = await validationService.GetByIdAsync(validationId, ct);
        if (validation is null)
            return NotFound("Validación no encontrada.");

        return Ok(validation);
    }
}