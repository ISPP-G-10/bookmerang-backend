using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Interfaces.Auth;
using System.Security.Claims;
using NetTopologySuite.Geometries;
using Bookmerang.Api.Models.DTOs;

namespace Bookmeran.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    [Authorize]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var supabaseId = User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        if (supabaseId == null) return Unauthorized();

        var email = User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress") ?? string.Empty;

        var factory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var location = factory.CreatePoint(new Coordinate(request.Longitud, request.Latitud));

        var (usuario, yaExistia) = await _authService.Register(
            supabaseId,
            email,
            request.Username,
            request.Name,
            request.ProfilePhoto,
            request.UserType,
            location
        );

        if (yaExistia) return Conflict("El usuario ya existe en el sistema.");

        return CreatedAtAction(nameof(GetPerfil), new { }, usuario!.ToDto());
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMe()
    {
        var supabaseId = User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        if (supabaseId == null) return Unauthorized();

        var usuario = await _authService.GetPerfil(supabaseId);
        if (usuario == null) return NotFound("Usuario no encontrado en el sistema.");

        return Ok(new { id = usuario.Id });
    }

    [HttpGet("perfil")]
    [Authorize]
    public async Task<IActionResult> GetPerfil()
    {
        var supabaseId = User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        if (supabaseId == null) return Unauthorized();

        var profile = await _authService.GetPerfil(supabaseId);
        if (profile == null) return NotFound("Usuario no encontrado en el sistema.");

        return Ok(profile);
    }

    [HttpPatch("perfil")]
    [Authorize]
    public async Task<IActionResult> PatchPerfil([FromBody] UpdatePerfilRequest request)
    {
        var supabaseId = User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        if (supabaseId == null) return Unauthorized();

        var usuario = await _authService.UpdatePerfil(
            supabaseId,
            request.Username,
            request.Name,
            request.ProfilePhoto
        );

        if (usuario == null) return NotFound("Usuario no encontrado.");

        return Ok(usuario.ToDto());
    }

    [HttpPatch("email")]
    [Authorize]
    public async Task<IActionResult> PatchEmail([FromBody] PatchEmailRequest request)
    {
        var supabaseId = User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        if (supabaseId == null) return Unauthorized();

        var (usuario, error) = await _authService.PatchEmail(supabaseId, request.NewEmail);

        if (error != null) return BadRequest(error);
        if (usuario == null) return NotFound("Usuario no encontrado.");

        return Ok(usuario.ToDto());
    }

    [HttpDelete("perfil")]
    [Authorize]
    public async Task<IActionResult> DeletePerfil()
    {
        var supabaseId = User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        if (supabaseId == null) return Unauthorized();

        try
        {
            var usuario = await _authService.DeletePerfil(supabaseId);
            return Ok(usuario.ToDto());
        }
        catch (Exception ex)
        {
            return NotFound(ex.Message);
        }
    }
    
}

public record RegisterRequest(
    string Username,
    string Name,
    string ProfilePhoto,
    BaseUserType UserType,
    double Latitud,
    double Longitud
);

public record UpdatePerfilRequest(
    string? Username,
    string? Name,
    string? ProfilePhoto
);

public record PatchEmailRequest(
    string NewEmail
);