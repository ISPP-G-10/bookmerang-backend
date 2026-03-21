using Moq;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Bookmerang.Api.Services.Interfaces.Auth;
using Bookmerang.Api.Models.DTOs;
using Bookmeran.Controllers;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using NetTopologySuite.Geometries;
using Microsoft.AspNetCore.Http;
using Xunit;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bookmerang.Tests.Auth;

public class AuthControllerTests
{
    private readonly Mock<IAuthService> _mockAuthService;
    private readonly AuthController _authController;

    public AuthControllerTests()
    {
        _mockAuthService = new Mock<IAuthService>();
        _authController = new AuthController(_mockAuthService.Object);
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
    public async Task Register_ShouldReturnUnauthorized_WhenSupabaseIdIsMissing()
    {
        // Arrange
        SetupControllerContext(null, "test@test.com");
        var request = new RegisterRequest("newuser", "New User", "photo.jpg", BaseUserType.USER, 0, 0);

        // Act
        var result = await _authController.Register(request);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Register_ShouldReturnCreatedAt_WhenUserIsNew()
    {
        // Arrange
        var supabaseId = "new-user-supabase-id";
        var email = "new@example.com";
        SetupControllerContext(supabaseId, email);

        var request = new RegisterRequest("newuser", "New User", "photo.jpg", BaseUserType.USER, 0, 0);

        var user = new ProfileDto { Id = Guid.NewGuid(), Username = request.Username };
        _mockAuthService.Setup(s => s.Register(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BaseUserType>(), It.IsAny<Point>()))
            .ReturnsAsync((new BaseUser { Id = user.Id, Username = user.Username, Location = new Point(0,0) }, false));

        // Act
        var result = await _authController.Register(request);

        // Assert
        var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(AuthController.GetPerfil), createdAtActionResult.ActionName);
        var returnedDto = Assert.IsType<BaseUserDto>(createdAtActionResult.Value);
        Assert.Equal(user.Id, returnedDto.Id);
    }

    [Fact]
    public async Task Register_ShouldReturnConflict_WhenUserAlreadyExists()
    {
        // Arrange
        var supabaseId = "existing-user-supabase-id";
        var email = "existing@example.com";
        SetupControllerContext(supabaseId, email);

        var request = new RegisterRequest("existinguser", "Existing User", null, BaseUserType.USER, 0, 0);

        _mockAuthService.Setup(s => s.Register(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BaseUserType>(), It.IsAny<Point>()))
            .ReturnsAsync(((BaseUser?)null, true));

        // Act
        var result = await _authController.Register(request);

        // Assert
        var conflictResult = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal("El usuario ya existe en el sistema.", conflictResult.Value);
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
        var request = new PatchEmailRequest("new@email.com");
        var errorMessage = "Email already in use.";
        _mockAuthService.Setup(s => s.PatchEmail(supabaseId, request.NewEmail)).ReturnsAsync(((BaseUser?)null, errorMessage));

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

}
