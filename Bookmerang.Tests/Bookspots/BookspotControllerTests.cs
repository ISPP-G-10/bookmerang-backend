using Bookmerang.Api.Controllers.Bookspots;
using Bookmerang.Api.Exceptions;
using Bookmerang.Api.Models.DTOs.Bookspots.Requests;
using Bookmerang.Api.Models.DTOs.Bookspots.Responses;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Interfaces.Bookspots;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;
using Xunit;

namespace Bookmerang.Tests.Bookspots;

/// <summary>
/// Tests del BookspotsController y BookspotValidationsController — capa HTTP.
/// Mockean IBookspotService e IBookspotValidationService. No requieren BD.
/// </summary>
public class BookspotControllerTests
{
    private readonly Mock<IBookspotService> _bookspotSvc = new();
    private readonly Mock<IBookspotValidationService> _validationSvc = new();

    private const string TestSupabaseId = "sup-controller-test";

    // ── Helpers ────────────────────────────────────────────────────────

    private BookspotsController CreateBookspotController()
    {
        var controller = new BookspotsController(_bookspotSvc.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = BuildPrincipal(TestSupabaseId)
            }
        };
        return controller;
    }

    private BookspotValidationsController CreateValidationController()
    {
        var controller = new BookspotValidationsController(_validationSvc.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = BuildPrincipal(TestSupabaseId)
            }
        };
        return controller;
    }

    private static ClaimsPrincipal BuildPrincipal(string supabaseId)
    {
        var claims = new List<Claim>
        {
            new("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", supabaseId)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    private static BookspotDTO MakeBookspotDto(int id = 1) => new()
    {
        Id = id,
        Nombre = "Test Bookspot",
        AddressText = "Calle Test",
        Latitude = 40.4168,
        Longitude = -3.7038,
        IsBookdrop = false,
        Status = BookspotStatus.PENDING,
        CreatedAt = DateTime.UtcNow
    };

    private static BookspotValidationDTO MakeValidationDto(int id = 1) => new()
    {
        Id = id,
        KnowsPlace = true,
        SafeForExchange = true,
        CreatedAt = DateTime.UtcNow
    };

    // ════════════════════════════════════════════════
    // BookspotsController — GetActive
    // ════════════════════════════════════════════════

    [Fact]
    public async Task GetActiveAsync_ReturnsOkWithList()
    {
        _bookspotSvc.Setup(s => s.GetActiveAsync(default))
            .ReturnsAsync([MakeBookspotDto()]);

        var result = await CreateBookspotController().GetActiveAsync();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsType<List<BookspotDTO>>(ok.Value);
        Assert.Single(list);
    }

    // ════════════════════════════════════════════════
    // BookspotsController — GetNearby
    // ════════════════════════════════════════════════

    [Fact]
    public async Task GetNearbyAsync_ReturnsOkWithList()
    {
        _bookspotSvc
            .Setup(s => s.GetNearbyActiveAsync(40.4, -3.7, 10, default))
            .ReturnsAsync([(new() { Id = 1, Nombre = "Cerca", Latitude = 40.4, Longitude = -3.7, DistanceKm = 0.5 })]);

        var result = await CreateBookspotController().GetNearbyAsync(40.4, -3.7, 10, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<List<BookspotNearbyDTO>>(ok.Value);
    }

    [Fact]
    public async Task GetNearbyAsync_ValidationException_ReturnsBadRequest()
    {
        _bookspotSvc
            .Setup(s => s.GetNearbyActiveAsync(It.IsAny<double>(), It.IsAny<double>(), 999, default))
            .ThrowsAsync(new ValidationException("Radio máximo 50 km."));

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => CreateBookspotController().GetNearbyAsync(40.4, -3.7, 999, default));

        Assert.Contains("50", ex.Message);
    }

    // ════════════════════════════════════════════════
    // BookspotsController — Create
    // ════════════════════════════════════════════════

    [Fact]
    public async Task CreateAsync_ValidRequest_Returns201()
    {
        var dto = MakeBookspotDto();
        _bookspotSvc
            .Setup(s => s.CreateAsync(TestSupabaseId, It.IsAny<CreateBookspotRequest>(), default))
            .ReturnsAsync(dto);

        var request = new CreateBookspotRequest
        {
            Nombre = "Test",
            AddressText = "Calle",
            Latitude = 40.4,
            Longitude = -3.7,
            IsBookdrop = false
        };

        var result = await CreateBookspotController().CreateAsync(request, default);

        var created = Assert.IsType<CreatedResult>(result.Result);
        Assert.Equal(201, created.StatusCode);
    }

    [Fact]
    public async Task CreateAsync_ValidationException_BubblesUp()
    {
        _bookspotSvc
            .Setup(s => s.CreateAsync(TestSupabaseId, It.IsAny<CreateBookspotRequest>(), default))
            .ThrowsAsync(new ValidationException("Duplicado cercano."));

        var request = new CreateBookspotRequest
        {
            Nombre = "X",
            AddressText = "X",
            Latitude = 40.4,
            Longitude = -3.7
        };

        await Assert.ThrowsAsync<ValidationException>(
            () => CreateBookspotController().CreateAsync(request, default));
    }

    // ════════════════════════════════════════════════
    // BookspotsController — GetById
    // ════════════════════════════════════════════════

    [Fact]
    public async Task GetByIdAsync_Found_ReturnsOk()
    {
        _bookspotSvc.Setup(s => s.GetByIdAsync(1, default))
            .ReturnsAsync(MakeBookspotDto());

        var result = await CreateBookspotController().GetByIdAsync(1, default);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_Returns404()
    {
        _bookspotSvc.Setup(s => s.GetByIdAsync(99, default))
            .ReturnsAsync((BookspotDTO?)null);

        var result = await CreateBookspotController().GetByIdAsync(99, default);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ════════════════════════════════════════════════
    // BookspotsController — GetRandomPendingNearby
    // ════════════════════════════════════════════════

    [Fact]
    public async Task GetRandomPendingNearbyAsync_Found_ReturnsOk()
    {
        _bookspotSvc
            .Setup(s => s.GetRandomPendingNearbyAsync(40.4, -3.7, 10, default))
            .ReturnsAsync(MakeBookspotDto());

        var result = await CreateBookspotController()
            .GetRandomPendingNearbyAsync(40.4, -3.7, 10, default);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetRandomPendingNearbyAsync_NoneFound_Returns204()
    {
        _bookspotSvc
            .Setup(s => s.GetRandomPendingNearbyAsync(40.4, -3.7, 10, default))
            .ReturnsAsync((BookspotDTO?)null);

        var result = await CreateBookspotController()
            .GetRandomPendingNearbyAsync(40.4, -3.7, 10, default);

        Assert.IsType<NoContentResult>(result.Result);
    }

    // ════════════════════════════════════════════════
    // BookspotsController — GetUserPending
    // ════════════════════════════════════════════════

    [Fact]
    public async Task GetUserPendingAsync_ReturnsOkWithList()
    {
        _bookspotSvc
            .Setup(s => s.GetUserPendingWithValidationCountAsync(TestSupabaseId, default))
            .ReturnsAsync([MakeBookspotDto()]);

        var result = await CreateBookspotController().GetUserPendingAsync();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<List<BookspotDTO>>(ok.Value);
    }

    // ════════════════════════════════════════════════
    // BookspotsController — Delete
    // ════════════════════════════════════════════════

    [Fact]
    public async Task DeleteAsync_Owner_Returns204()
    {
        _bookspotSvc
            .Setup(s => s.DeleteAsync(TestSupabaseId, 1, default))
            .Returns(Task.CompletedTask);

        var result = await CreateBookspotController().DeleteAsync(1, default);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteAsync_NotFound_BubblesNotFoundException()
    {
        _bookspotSvc
            .Setup(s => s.DeleteAsync(TestSupabaseId, 99, default))
            .ThrowsAsync(new NotFoundException("Bookspot no encontrado."));

        await Assert.ThrowsAsync<NotFoundException>(
            () => CreateBookspotController().DeleteAsync(99, default));
    }

    [Fact]
    public async Task DeleteAsync_NotOwner_BubblesValidationException()
    {
        _bookspotSvc
            .Setup(s => s.DeleteAsync(TestSupabaseId, 1, default))
            .ThrowsAsync(new ValidationException("No tienes permiso."));

        await Assert.ThrowsAsync<ValidationException>(
            () => CreateBookspotController().DeleteAsync(1, default));
    }

    // ════════════════════════════════════════════════
    // BookspotValidationsController — Create
    // ════════════════════════════════════════════════

    [Fact]
    public async Task Validation_CreateAsync_ValidRequest_Returns201()
    {
        var dto = MakeValidationDto();
        _validationSvc
            .Setup(s => s.CreateAsync(TestSupabaseId, It.IsAny<CreateBookspotValidationRequest>(), default))
            .ReturnsAsync(dto);

        var request = new CreateBookspotValidationRequest
        {
            BookspotId = 1,
            KnowsPlace = true,
            SafeForExchange = true
        };

        var result = await CreateValidationController().CreateAsync(request, default);

        var created = Assert.IsType<CreatedResult>(result.Result);
        Assert.Equal(201, created.StatusCode);
    }

    [Fact]
    public async Task Validation_CreateAsync_NullRequest_ReturnsBadRequest()
    {
        var result = await CreateValidationController().CreateAsync(null!, default);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Validation_CreateAsync_InvalidBookspotId_ReturnsBadRequest()
    {
        var request = new CreateBookspotValidationRequest
        {
            BookspotId = 0,
            KnowsPlace = true,
            SafeForExchange = true
        };

        var result = await CreateValidationController().CreateAsync(request, default);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Validation_CreateAsync_BookspotNotFound_BubblesNotFoundException()
    {
        _validationSvc
            .Setup(s => s.CreateAsync(TestSupabaseId, It.IsAny<CreateBookspotValidationRequest>(), default))
            .ThrowsAsync(new NotFoundException("Bookspot no encontrado."));

        var request = new CreateBookspotValidationRequest
        {
            BookspotId = 1,
            KnowsPlace = true,
            SafeForExchange = true
        };

        await Assert.ThrowsAsync<NotFoundException>(
            () => CreateValidationController().CreateAsync(request, default));
    }

    [Fact]
    public async Task Validation_CreateAsync_SelfValidation_BubblesValidationException()
    {
        _validationSvc
            .Setup(s => s.CreateAsync(TestSupabaseId, It.IsAny<CreateBookspotValidationRequest>(), default))
            .ThrowsAsync(new ValidationException("No puedes validar tu propio bookspot."));

        var request = new CreateBookspotValidationRequest
        {
            BookspotId = 1,
            KnowsPlace = true,
            SafeForExchange = true
        };

        await Assert.ThrowsAsync<ValidationException>(
            () => CreateValidationController().CreateAsync(request, default));
    }

    // ════════════════════════════════════════════════
    // BookspotValidationsController — GetByBookspotId
    // ════════════════════════════════════════════════

    [Fact]
    public async Task Validation_GetByBookspotIdAsync_ReturnsOkWithList()
    {
        _validationSvc
            .Setup(s => s.GetByBookspotIdAsync(1, default))
            .ReturnsAsync([MakeValidationDto()]);

        var result = await CreateValidationController().GetByBookspotIdAsync(1, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<List<BookspotValidationDTO>>(ok.Value);
    }

    [Fact]
    public async Task Validation_GetByBookspotIdAsync_Empty_ReturnsOkWithEmptyList()
    {
        _validationSvc
            .Setup(s => s.GetByBookspotIdAsync(1, default))
            .ReturnsAsync([]);

        var result = await CreateValidationController().GetByBookspotIdAsync(1, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsType<List<BookspotValidationDTO>>(ok.Value);
        Assert.Empty(list);
    }

    // ════════════════════════════════════════════════
    // BookspotValidationsController — GetById
    // ════════════════════════════════════════════════

    [Fact]
    public async Task Validation_GetByIdAsync_Found_ReturnsOk()
    {
        _validationSvc
            .Setup(s => s.GetByIdAsync(1, default))
            .ReturnsAsync(MakeValidationDto());

        var result = await CreateValidationController().GetByIdAsync(1, default);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task Validation_GetByIdAsync_NotFound_BubblesNotFoundException()
    {
        _validationSvc
            .Setup(s => s.GetByIdAsync(99, default))
            .ThrowsAsync(new NotFoundException("Validación no encontrada."));

        await Assert.ThrowsAsync<NotFoundException>(
            () => CreateValidationController().GetByIdAsync(99, default));
    }
}