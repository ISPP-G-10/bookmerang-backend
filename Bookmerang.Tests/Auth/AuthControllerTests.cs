using Moq;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Bookmerang.Api.Services.Interfaces.Auth;
using Bookmerang.Api.Services.Interfaces.Subscriptions;
using Bookmerang.Api.Models.DTOs;
using Bookmeran.Controllers;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using NetTopologySuite.Geometries;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bookmerang.Tests.Auth;

public class AuthControllerTests
{
    private readonly Mock<IAuthService> _mockAuthService;
    private readonly Mock<IStripeSubscriptionService> _mockStripeSubscriptionService;
    private readonly AuthController _authController;

    public AuthControllerTests()
    {
        _mockAuthService = new Mock<IAuthService>();
        _mockStripeSubscriptionService = new Mock<IStripeSubscriptionService>();
        _authController = new AuthController(
            _mockAuthService.Object,
            _mockStripeSubscriptionService.Object,
            NullLogger<AuthController>.Instance);
    }

    private void SetupControllerContext(string? supabaseId, string? email)
    {
        var claims = new List<Claim>();
        if (supabaseId != null)
        {
            claims.Add(new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", supabaseId));
        }
        if (email != null)
        {
            claims.Add(new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", email));
        }

        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _authController.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    [Fact]
    public async Task Register_ShouldReturnBadRequest_WhenRegisterServiceReturnsError()
    {
        // Arrange
        SetupControllerContext(null, "test@test.com");
        var request = new RegisterRequest("newuser@test.com", "123456", "newuser", "New User", "photo.jpg", BaseUserType.USER, 0, 0);
        _mockAuthService
            .Setup(s => s.RegisterWithCredentials(
                request.Email,
                request.Password,
                request.Username,
                request.Name,
                request.ProfilePhoto,
                request.UserType,
                It.IsAny<Point>()))
            .ReturnsAsync(((BaseUser?)null, false, "Error en registro"));

        // Act
        var result = await _authController.Register(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Error en registro", badRequestResult.Value);
    }

    [Fact]
    public async Task Register_ShouldReturnCreatedAt_WhenUserIsNew()
    {
        // Arrange
        var supabaseId = "new-user-supabase-id";
        var email = "new@example.com";
        SetupControllerContext(supabaseId, email);

        var request = new RegisterRequest("newuser@test.com", "123456", "newuser", "New User", "photo.jpg", BaseUserType.USER, 0, 0);

        var user = new BaseUser
        {
            Id = Guid.NewGuid(),
            SupabaseId = "supabase-new",
            Email = request.Email,
            Username = request.Username,
            Name = request.Name,
            ProfilePhoto = request.ProfilePhoto,
            UserType = request.UserType,
            Location = new Point(0, 0) { SRID = 4326 }
        };
        _mockAuthService
            .Setup(s => s.RegisterWithCredentials(
                request.Email,
                request.Password,
                request.Username,
                request.Name,
                request.ProfilePhoto,
                request.UserType,
                It.IsAny<Point>()))
            .ReturnsAsync((user, false, (string?)null));
        _mockAuthService
            .Setup(s => s.Login(request.Email, request.Password))
            .ReturnsAsync((user, "token-123", (string?)null));

        // Act
        var result = await _authController.Register(request);

        // Assert
        var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(AuthController.GetPerfil), createdAtActionResult.ActionName);
        var response = Assert.IsType<AuthResponse>(createdAtActionResult.Value);
        Assert.Equal("token-123", response.AccessToken);
        Assert.Equal(user.Id, response.User.Id);
    }

    [Fact]
    public async Task Register_ShouldReturnConflict_WhenUserAlreadyExists()
    {
        // Arrange
        var supabaseId = "existing-user-supabase-id";
        var email = "existing@example.com";
        SetupControllerContext(supabaseId, email);

        var request = new RegisterRequest("existing@test.com", "123456", "existinguser", "Existing User", "", BaseUserType.USER, 0, 0);

        _mockAuthService
            .Setup(s => s.RegisterWithCredentials(
                request.Email,
                request.Password,
                request.Username,
                request.Name,
                request.ProfilePhoto,
                request.UserType,
                It.IsAny<Point>()))
            .ReturnsAsync(((BaseUser?)null, true, (string?)null));

        // Act
        var result = await _authController.Register(request);

        // Assert
        var conflictResult = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("El usuario ya existe en el sistema.", conflictResult.Value);
    }

    [Fact]
    public async Task RegisterBusiness_WhenPaymentEnabledAndNoSessionId_ReturnsCheckoutUrl()
    {
        // Arrange
        var request = new RegisterBusinessRequest
        {
            Email = "bookdrop@test.com",
            Password = "Test1234",
            Username = "bookdrop_user",
            Name = "Bookdrop",
            NombreEstablecimiento = "Bookdrop Centro",
            AddressText = "Calle Test 1",
            Latitud = 37.38,
            Longitud = -5.98
        };

        _mockStripeSubscriptionService.Setup(s => s.IsBookdropPaymentEnabled()).Returns(true);
        _mockStripeSubscriptionService
            .Setup(s => s.CreateBookdropRegistrationCheckoutSessionAsync(request.Email))
            .ReturnsAsync("https://checkout.stripe.com/pay/bookdrop");

        // Act
        var result = await _authController.RegisterBusiness(request);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        dynamic body = ok.Value!;
        Assert.True((bool)body.RequiresPayment);
        Assert.Equal("https://checkout.stripe.com/pay/bookdrop", (string)body.CheckoutUrl);
    }

    [Fact]
    public async Task GetMe_ShouldReturnUnauthorized_WhenSupabaseIdIsMissing()
    {
        // Arrange
        SetupControllerContext(null, null);

        // Act
        var result = await _authController.GetMe();

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetMe_ShouldReturnNotFound_WhenUserNotFound()
    {
        // Arrange
        var supabaseId = "not-found-id";
        SetupControllerContext(supabaseId, "test@test.com");
        _mockAuthService.Setup(s => s.GetPerfil(supabaseId)).ReturnsAsync((ProfileDto?)null);

        // Act
        var result = await _authController.GetMe();

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Usuario no encontrado en el sistema.", notFoundResult.Value);
    }

    [Fact]
    public async Task GetMe_ShouldReturnOkWithId_WhenUserExists()
    {
        // Arrange
        var supabaseId = "user-id";
        var userId = Guid.NewGuid();
        SetupControllerContext(supabaseId, "user@example.com");
        var user = new ProfileDto { Id = userId };
        _mockAuthService.Setup(s => s.GetPerfil(supabaseId)).ReturnsAsync(user);

        // Act
        var result = await _authController.GetMe();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetPerfil_ShouldReturnOk_WhenProfileExists()
    {
        // Arrange
        var supabaseId = "profile-id";
        SetupControllerContext(supabaseId, "profile@test.com");
        var profile = new ProfileDto();
        _mockAuthService.Setup(s => s.GetPerfil(supabaseId)).ReturnsAsync(profile);

        // Act
        var result = await _authController.GetPerfil();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(profile, okResult.Value);
    }

    [Fact]
    public async Task PatchPerfil_ShouldReturnOk_WhenUpdateSucceeds()
    {
        // Arrange
        var supabaseId = "user-to-update";
        SetupControllerContext(supabaseId, "test@test.com");
        var request = new UpdatePerfilRequest("newUsername", "newName", "newPhoto.jpg");
        var updatedUser = new ProfileDto { Id = Guid.NewGuid(), Username = "newUsername" };
        _mockAuthService.Setup(s => s.UpdatePerfil(supabaseId, request.Username, request.Name, request.ProfilePhoto)).ReturnsAsync(new BaseUser { Id = updatedUser.Id, Username = updatedUser.Username, Location = new Point(0,0) });

        // Act
        var result = await _authController.PatchPerfil(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedDto = Assert.IsType<BaseUserDto>(okResult.Value);
        Assert.Equal(updatedUser.Id, returnedDto.Id);
    }

    [Fact]
    public async Task PatchEmail_ShouldReturnBadRequest_WhenServiceReturnsError()
    {
        // Arrange
        var supabaseId = "user-patching-email";
        SetupControllerContext(supabaseId, "test@test.com");
        var request = new PatchEmailRequest("new@email.com", "123456");
        var errorMessage = "Email already in use.";
        _mockAuthService
            .Setup(s => s.PatchEmail(supabaseId, request.NewEmail, request.CurrentPassword))
            .ReturnsAsync(((BaseUser?)null, errorMessage));

        // Act
        var result = await _authController.PatchEmail(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(errorMessage, badRequestResult.Value);
    }

    [Fact]
    public async Task DeletePerfil_ShouldReturnOk_WhenDeletionSucceeds()
    {
        // Arrange
        var supabaseId = "user-to-delete";
        SetupControllerContext(supabaseId, "test@test.com");
        var deletedUser = new BaseUser
        {
            Id = Guid.NewGuid(),
            SupabaseId = supabaseId,
            Email = "delete@test.com",
            Username = "delete-user",
            Name = "Delete User",
            ProfilePhoto = "delete.jpg",
            UserType = BaseUserType.USER,
            Location = new Point(0, 0) { SRID = 4326 }
        };
        _mockAuthService.Setup(s => s.DeletePerfil(supabaseId)).ReturnsAsync(deletedUser);

        // Act
        var result = await _authController.DeletePerfil();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedDto = Assert.IsType<BaseUserDto>(okResult.Value);
        Assert.Equal(deletedUser.Id, returnedDto.Id);
    }

    [Fact]
    public async Task DeletePerfil_ShouldThrowException_WhenServiceThrowsException()
    {
        // Arrange
        var supabaseId = "user-with-active-exchange";
        SetupControllerContext(supabaseId, "test@test.com");
        var exceptionMessage = "Cannot delete user with active exchanges.";
        _mockAuthService.Setup(s => s.DeletePerfil(supabaseId)).ThrowsAsync(new Exception(exceptionMessage));

        // Act
        var result = await _authController.DeletePerfil();

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(exceptionMessage, badRequestResult.Value);
    }

    [Fact]
    public async Task PatchPassword_ShouldReturnUnauthorized_WhenSupabaseIdMissing()
    {
        // Arrange
        SetupControllerContext(null, null);

        // Act
        var result = await _authController.PatchPassword(new PatchPasswordRequest("any", "newpass123"));

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task PatchPassword_ShouldReturnBadRequest_WhenServiceReturnsError_CurrentEmpty()
    {
        // Arrange
        var supabaseId = "user-pw-1";
        SetupControllerContext(supabaseId, "u@test.com");
        var request = new PatchPasswordRequest("", "NewPass123!");
        _mockAuthService.Setup(s => s.PatchPassword(supabaseId, request.CurrentPassword, request.NewPassword))
            .ReturnsAsync("La contraseña actual es obligatoria.");

        // Act
        var result = await _authController.PatchPassword(request);

        // Assert
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("La contraseña actual es obligatoria.", bad.Value);
    }

    [Fact]
    public async Task PatchPassword_ShouldReturnBadRequest_WhenUserNotFound()
    {
        // Arrange
        var supabaseId = "user-pw-2";
        SetupControllerContext(supabaseId, "u@test.com");
        var request = new PatchPasswordRequest("current", "NewPass123!");
        _mockAuthService.Setup(s => s.PatchPassword(supabaseId, request.CurrentPassword, request.NewPassword))
            .ReturnsAsync("Usuario no encontrado.");

        // Act
        var result = await _authController.PatchPassword(request);

        // Assert
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Usuario no encontrado.", bad.Value);
    }

    [Fact]
    public async Task PatchPassword_ShouldReturnBadRequest_WhenCurrentIncorrect()
    {
        // Arrange
        var supabaseId = "user-pw-3";
        SetupControllerContext(supabaseId, "u@test.com");
        var request = new PatchPasswordRequest("wrong", "BrandNew123!");
        _mockAuthService.Setup(s => s.PatchPassword(supabaseId, request.CurrentPassword, request.NewPassword))
            .ReturnsAsync("Contraseña actual incorrecta.");

        // Act
        var result = await _authController.PatchPassword(request);

        // Assert
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Contraseña actual incorrecta.", bad.Value);
    }

    [Fact]
    public async Task PatchPassword_ShouldReturnBadRequest_WhenNewPasswordTooShort()
    {
        // Arrange
        var supabaseId = "user-pw-4";
        SetupControllerContext(supabaseId, "u@test.com");
        var request = new PatchPasswordRequest("Correct1!", "short");
        _mockAuthService.Setup(s => s.PatchPassword(supabaseId, request.CurrentPassword, request.NewPassword))
            .ReturnsAsync("La nueva contraseña debe tener al menos 8 caracteres.");

        // Act
        var result = await _authController.PatchPassword(request);

        // Assert
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("La nueva contraseña debe tener al menos 8 caracteres.", bad.Value);
    }

    [Fact]
    public async Task PatchPassword_ShouldReturnOk_WhenPasswordUpdatedSuccessfully()
    {
        // Arrange
        var supabaseId = "user-pw-5";
        SetupControllerContext(supabaseId, "u@test.com");
        var request = new PatchPasswordRequest("Correct123!", "BrandNew123!");
        _mockAuthService.Setup(s => s.PatchPassword(supabaseId, request.CurrentPassword, request.NewPassword))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _authController.PatchPassword(request);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        dynamic val = ok.Value!;
        Assert.Equal("Contraseña actualizada correctamente.", (string)val.message);
    }

    // ─── ForgotPassword controller tests ───

    [Fact]
    public async Task ForgotPassword_ShouldReturnOk_WhenServiceReturnsNull()
    {
        var request = new ForgotPasswordRequest("user@test.com");
        _mockAuthService.Setup(s => s.RequestPasswordReset(request.Email))
            .ReturnsAsync((string?)null);

        var result = await _authController.ForgotPassword(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        dynamic val = ok.Value!;
        Assert.Equal("Si el email existe, recibirás un correo con instrucciones.", (string)val.message);
    }

    [Fact]
    public async Task ForgotPassword_ShouldReturnBadRequest_WhenServiceReturnsError()
    {
        var request = new ForgotPasswordRequest("");
        _mockAuthService.Setup(s => s.RequestPasswordReset(request.Email))
            .ReturnsAsync("El email es obligatorio.");

        var result = await _authController.ForgotPassword(request);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("El email es obligatorio.", bad.Value);
    }

    [Fact]
    public async Task ForgotPassword_ShouldReturnOk_EvenForNonExistentEmail()
    {
        var request = new ForgotPasswordRequest("nobody@test.com");
        _mockAuthService.Setup(s => s.RequestPasswordReset(request.Email))
            .ReturnsAsync((string?)null);

        var result = await _authController.ForgotPassword(request);

        Assert.IsType<OkObjectResult>(result);
    }

    // ─── ResetPassword controller tests ───

    [Fact]
    public async Task ResetPassword_ShouldReturnOk_WhenServiceReturnsNull()
    {
        var request = new ResetPasswordRequest("ABCDEF", "NewPass123!");
        _mockAuthService.Setup(s => s.ResetPassword(request.Token, request.NewPassword))
            .ReturnsAsync((string?)null);

        var result = await _authController.ResetPassword(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        dynamic val = ok.Value!;
        Assert.Equal("Contraseña actualizada correctamente.", (string)val.message);
    }

    [Fact]
    public async Task ResetPassword_ShouldReturnBadRequest_WhenTokenInvalid()
    {
        var request = new ResetPasswordRequest("XXXXXX", "NewPass123!");
        _mockAuthService.Setup(s => s.ResetPassword(request.Token, request.NewPassword))
            .ReturnsAsync("El código de recuperación no es válido.");

        var result = await _authController.ResetPassword(request);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ResetPassword_ShouldReturnBadRequest_WhenTokenExpired()
    {
        var request = new ResetPasswordRequest("ABCDEF", "NewPass123!");
        _mockAuthService.Setup(s => s.ResetPassword(request.Token, request.NewPassword))
            .ReturnsAsync("El enlace de recuperación ha expirado.");

        var result = await _authController.ResetPassword(request);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ResetPassword_ShouldReturnBadRequest_WhenPasswordTooShort()
    {
        var request = new ResetPasswordRequest("ABCDEF", "short");
        _mockAuthService.Setup(s => s.ResetPassword(request.Token, request.NewPassword))
            .ReturnsAsync("La nueva contraseña debe tener al menos 8 caracteres.");

        var result = await _authController.ResetPassword(request);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
    }

}
