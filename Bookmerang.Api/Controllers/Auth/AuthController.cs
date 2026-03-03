using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        var usuario = await _authService.GetPerfil(supabaseId);
        if (usuario == null) return NotFound("Usuario no encontrado en el sistema.");

        return Ok(usuario.ToDto());
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