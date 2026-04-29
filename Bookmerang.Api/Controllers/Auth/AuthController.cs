using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Interfaces.Auth;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using NetTopologySuite.Geometries;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Services.Interfaces.Subscriptions;
using Stripe;

namespace Bookmeran.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IStripeSubscriptionService _stripeSubscriptionService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        IStripeSubscriptionService stripeSubscriptionService,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _stripeSubscriptionService = stripeSubscriptionService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var factory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var location = factory.CreatePoint(new Coordinate(request.Longitud, request.Latitud));

        var (usuario, yaExistia, error) = await _authService.RegisterWithCredentials(
            request.Email,
            request.Password,
            request.Username,
            request.Name,
            request.ProfilePhoto,
            BaseUserType.USER,
            location
        );

        if (!string.IsNullOrWhiteSpace(error)) return BadRequest(error);
        if (yaExistia) return Conflict("El usuario ya existe en el sistema.");

        var (_, token, loginError) = await _authService.Login(request.Email, request.Password);
        if (!string.IsNullOrWhiteSpace(loginError) || string.IsNullOrWhiteSpace(token))
            return StatusCode(StatusCodes.Status500InternalServerError, "No se pudo iniciar sesión tras el registro.");

        return CreatedAtAction(nameof(GetPerfil), new { }, new AuthResponse(token, usuario!.ToDto()));
    }

    [HttpPost("register/business")]
    public async Task<IActionResult> RegisterBusiness([FromBody] RegisterBusinessRequest request)
    {
        var paymentEnabled = _stripeSubscriptionService.IsBookdropPaymentEnabled();

        if (paymentEnabled && string.IsNullOrWhiteSpace(request.StripeSessionId))
        {
            try
            {
                var checkoutUrl = await _stripeSubscriptionService.CreateBookdropRegistrationCheckoutSessionAsync(request.Email);
                return Ok(new BookdropCheckoutInitResponse(true, checkoutUrl));
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex,
                    "Stripe error creating Bookdrop checkout session for email {Email}", request.Email);
                var stripeErrorMessage = ex.StripeError?.Message ?? ex.Message;
                return StatusCode(StatusCodes.Status500InternalServerError,
                    $"No se pudo iniciar el pago con Stripe. {stripeErrorMessage}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error creating Bookdrop checkout session for email {Email}", request.Email);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "No se pudo iniciar el pago con Stripe. Intentalo de nuevo.");
            }
        }

        string? stripeSubscriptionId = null;
        if (paymentEnabled)
        {
            stripeSubscriptionId = await _stripeSubscriptionService
                .ValidateAndGetBookdropRegistrationSubscriptionIdAsync(request.StripeSessionId ?? string.Empty, request.Email);

            if (string.IsNullOrWhiteSpace(stripeSubscriptionId))
                return BadRequest("No se ha podido verificar el pago en Stripe.");
        }

        var factory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var location = factory.CreatePoint(new Coordinate(request.Longitud, request.Latitud));

        var (usuario, yaExistia, error) = await _authService.RegisterBusiness(
            request.Email,
            request.Password,
            request.Username,
            request.Name,
            request.ProfilePhoto,
            location,
            request.NombreEstablecimiento,
            request.AddressText
        );

        if (!string.IsNullOrWhiteSpace(error))
            return yaExistia ? Conflict(error) : BadRequest(error);

        var (_, token, loginError) = await _authService.Login(request.Email, request.Password);
        if (!string.IsNullOrWhiteSpace(loginError) || string.IsNullOrWhiteSpace(token))
            return StatusCode(StatusCodes.Status500InternalServerError, "No se pudo iniciar sesion tras el registro.");

        if (!string.IsNullOrWhiteSpace(stripeSubscriptionId))
        {
            try
            {
                await _stripeSubscriptionService.LinkBookdropSubscriptionToUserAsync(usuario!.Id, stripeSubscriptionId);
            }
            catch
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "La cuenta se ha creado, pero no se ha podido vincular la suscripcion.");
            }
        }

        return CreatedAtAction(nameof(GetPerfil), new { }, new AuthBusinessResponse(token, usuario!.ToDto(), null));
    }
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var (usuario, token, error) = await _authService.Login(request.Email, request.Password);

        if (error != null) return Unauthorized(error);
        if (usuario == null || string.IsNullOrWhiteSpace(token)) return Unauthorized("Credenciales inválidas.");

        return Ok(new AuthResponse(token, usuario.ToDto()));
    }

    [HttpGet("me")]
    [Authorize(Policy = "UserOnly")]
    public async Task<IActionResult> GetMe()
    {
        var supabaseId = User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        if (supabaseId == null) return Unauthorized();

        var usuario = await _authService.GetPerfil(supabaseId);
        if (usuario == null) return NotFound("Usuario no encontrado en el sistema.");

        var plan = await _authService.GetUserPlan(usuario.Id);

        return Ok(new { id = usuario.Id, plan = plan.ToString() });
    }

    [HttpGet("perfil")]
    [Authorize(Policy = "UserOnly")]
    public async Task<IActionResult> GetPerfil()
    {
        var supabaseId = User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        if (supabaseId == null) return Unauthorized();

        var profile = await _authService.GetPerfil(supabaseId);
        if (profile == null) return NotFound("Usuario no encontrado en el sistema.");

        return Ok(profile);
    }

    [HttpPatch("perfil")]
    [Authorize(Policy = "UserOnly")]
    public async Task<IActionResult> PatchPerfil([FromBody] UpdatePerfilRequest request)
    {
        var supabaseId = User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        if (supabaseId == null) return Unauthorized();

        try
        {
            var usuario = await _authService.UpdatePerfil(
                supabaseId,
                request.Username,
                request.Name,
                request.ProfilePhoto
            );

            if (usuario == null) return NotFound("Usuario no encontrado.");

            return Ok(usuario.ToDto());
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPatch("email")]
    [Authorize]
    public async Task<IActionResult> PatchEmail([FromBody] PatchEmailRequest request)
    {
        var supabaseId = User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        if (supabaseId == null) return Unauthorized();

        var (usuario, error) = await _authService.PatchEmail(supabaseId, request.NewEmail, request.CurrentPassword);

        if (error != null) return BadRequest(error);
        if (usuario == null) return NotFound("Usuario no encontrado.");

        return Ok(usuario.ToDto());
    }

    [HttpPatch("password")]
    [Authorize]
    public async Task<IActionResult> PatchPassword([FromBody] PatchPasswordRequest request)
    {
        var supabaseId = User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        if (supabaseId == null) return Unauthorized();

        var error = await _authService.PatchPassword(supabaseId, request.CurrentPassword, request.NewPassword);
        if (error != null) return BadRequest(error);

        return Ok(new { message = "Contraseña actualizada correctamente." });
    }

    [HttpPatch("cosmetics")]
    [Authorize(Policy = "UserOnly")]
    public async Task<IActionResult> PatchCosmetics([FromBody] UpdateCosmeticsRequest request)
    {
        var supabaseId = User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        if (supabaseId == null) return Unauthorized();

        var ok = await _authService.UpdateCosmetics(supabaseId, request.ActiveFrameId, request.ActiveColorId);
        if (!ok) return NotFound("Usuario o progreso no encontrado.");

        return NoContent();
    }

    [HttpDelete("perfil")]
    [Authorize(Policy = "UserOnly")]
    public async Task<IActionResult> DeletePerfil()
    {
        var supabaseId = User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        if (supabaseId == null) return Unauthorized();

        try
        {
            var usuario = await _authService.DeletePerfil(supabaseId);
            if (usuario == null)
            {
                return Ok(new { message = "La cuenta ha sido borrada en Supabase. No se encontró perfil local." });
            }
            return Ok(usuario.ToDto());
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

}

public record RegisterRequest(
    [Required]
    [EmailAddress]
    string Email,
    string Password,
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
    [Required]
    [EmailAddress]
    string NewEmail,
    string CurrentPassword
);

public record PatchPasswordRequest(
    string CurrentPassword,
    string NewPassword
);

public class RegisterBusinessRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    public string? ProfilePhoto { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string NombreEstablecimiento { get; set; } = string.Empty;

    [Required]
    [StringLength(200, MinimumLength = 5)]
    public string AddressText { get; set; } = string.Empty;

    [Required]
    [Range(-90, 90)]
    public double Latitud { get; set; }

    [Required]
    [Range(-180, 180)]
    public double Longitud { get; set; }

    public string? StripeSessionId { get; set; }
}

public record LoginRequest(
    [Required]
    [EmailAddress]
    string Email,
    string Password
);

public record AuthResponse(
    string AccessToken,
    BaseUserDto User
);

public record AuthBusinessResponse(
    string AccessToken,
    BaseUserDto User,
    string? CheckoutUrl
);

public record BookdropCheckoutInitResponse(
    bool RequiresPayment,
    string CheckoutUrl
);

public record UpdateCosmeticsRequest(
    string? ActiveFrameId,
    string? ActiveColorId
);


