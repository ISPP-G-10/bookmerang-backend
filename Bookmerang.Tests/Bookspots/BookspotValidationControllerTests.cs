// ============================================================
// Todos: dotnet test Bookmerang.Tests --filter "FullyQualifiedName~BookspotValidationControllerTests"
// ============================================================

using Bookmerang.Api.Controllers.Bookspots;
using Bookmerang.Api.Exceptions;
using Bookmerang.Api.Models.DTOs.Bookspots.Requests;
using Bookmerang.Api.Models.DTOs.Bookspots.Responses;
using Bookmerang.Api.Services.Interfaces.Bookspots;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;
using Xunit;

namespace Bookmerang.Tests.Bookspots;

/// <summary>
/// Tests del BookspotValidationsController — capa HTTP.
/// Mockea IBookspotValidationService. No requiere BD.
/// </summary>
public class BookspotValidationControllerTests
{
    private readonly Mock<IBookspotValidationService> _validationSvc = new();

    private const string TestSupabaseId = "sup-validation-controller-test";

    // ── Helpers ────────────────────────────────────────────────────────

    private BookspotValidationsController CreateController()
    {
        var controller = new BookspotValidationsController(_validationSvc.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(
                        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
                        TestSupabaseId)
                }, "TestAuth"))
            }
        };
        return controller;
    }

    private static BookspotValidationDTO MakeDto(int id = 1) => new()
    {
        Id = id,
        KnowsPlace = true,
        SafeForExchange = true,
        CreatedAt = DateTime.UtcNow
    };

    // ════════════════════════════════════════════════
    // TEST-BVC01: Create — request nulo → 400
    // ════════════════════════════════════════════════

    [Fact]
    public async Task CreateAsync_NullRequest_ReturnsBadRequest()
    {
        var result = await CreateController().CreateAsync(null!, default);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // ════════════════════════════════════════════════
    // TEST-BVC02: Create — BookspotId inválido → 400
    // ════════════════════════════════════════════════

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateAsync_InvalidBookspotId_ReturnsBadRequest(int bookspotId)
    {
        var request = new CreateBookspotValidationRequest
        {
            BookspotId = bookspotId,
            KnowsPlace = true,
            SafeForExchange = true
        };

        var result = await CreateController().CreateAsync(request, default);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // ════════════════════════════════════════════════
    // TEST-BVC03: Create — válido → 201
    // ════════════════════════════════════════════════

    [Fact]
    public async Task CreateAsync_ValidRequest_Returns201()
    {
        _validationSvc
            .Setup(s => s.CreateAsync(TestSupabaseId, It.IsAny<CreateBookspotValidationRequest>(), default))
            .ReturnsAsync(MakeDto());

        var request = new CreateBookspotValidationRequest
        {
            BookspotId = 1,
            KnowsPlace = true,
            SafeForExchange = true
        };

        var result = await CreateController().CreateAsync(request, default);

        var created = Assert.IsType<CreatedResult>(result.Result);
        Assert.Equal(201, created.StatusCode);
        Assert.IsType<BookspotValidationDTO>(created.Value);
    }

    // ════════════════════════════════════════════════
    // TEST-BVC04: Create — bookspot no encontrado → burbujea NotFoundException
    // ════════════════════════════════════════════════

    [Fact]
    public async Task CreateAsync_BookspotNotFound_BubblesNotFoundException()
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
            () => CreateController().CreateAsync(request, default));
    }

    // ════════════════════════════════════════════════
    // TEST-BVC05: Create — autovalidación → burbujea ValidationException
    // ════════════════════════════════════════════════

    [Fact]
    public async Task CreateAsync_SelfValidation_BubblesValidationException()
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
            () => CreateController().CreateAsync(request, default));
    }

    // ════════════════════════════════════════════════
    // TEST-BVC06: Create — duplicado → burbujea ValidationException
    // ════════════════════════════════════════════════

    [Fact]
    public async Task CreateAsync_DuplicateValidation_BubblesValidationException()
    {
        _validationSvc
            .Setup(s => s.CreateAsync(TestSupabaseId, It.IsAny<CreateBookspotValidationRequest>(), default))
            .ThrowsAsync(new ValidationException("Ya has validado este bookspot."));

        var request = new CreateBookspotValidationRequest
        {
            BookspotId = 1,
            KnowsPlace = true,
            SafeForExchange = true
        };

        await Assert.ThrowsAsync<ValidationException>(
            () => CreateController().CreateAsync(request, default));
    }

    // ════════════════════════════════════════════════
    // TEST-BVC07: GetByBookspotId — devuelve lista → 200
    // ════════════════════════════════════════════════

    [Fact]
    public async Task GetByBookspotIdAsync_ReturnsOkWithList()
    {
        _validationSvc
            .Setup(s => s.GetByBookspotIdAsync(1, default))
            .ReturnsAsync([MakeDto(1), MakeDto(2)]);

        var result = await CreateController().GetByBookspotIdAsync(1, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsType<List<BookspotValidationDTO>>(ok.Value);
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public async Task GetByBookspotIdAsync_Empty_ReturnsOkWithEmptyList()
    {
        _validationSvc
            .Setup(s => s.GetByBookspotIdAsync(1, default))
            .ReturnsAsync([]);

        var result = await CreateController().GetByBookspotIdAsync(1, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsType<List<BookspotValidationDTO>>(ok.Value);
        Assert.Empty(list);
    }

    // ════════════════════════════════════════════════
    // TEST-BVC08: GetById — encontrado → 200
    // ════════════════════════════════════════════════

    [Fact]
    public async Task GetByIdAsync_Found_ReturnsOk()
    {
        _validationSvc
            .Setup(s => s.GetByIdAsync(1, default))
            .ReturnsAsync(MakeDto());

        var result = await CreateController().GetByIdAsync(1, default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<BookspotValidationDTO>(ok.Value);
    }

    // ════════════════════════════════════════════════
    // TEST-BVC09: GetById — no encontrado → burbujea NotFoundException
    // ════════════════════════════════════════════════

    [Fact]
    public async Task GetByIdAsync_NotFound_BubblesNotFoundException()
    {
        _validationSvc
            .Setup(s => s.GetByIdAsync(99, default))
            .ThrowsAsync(new NotFoundException("Validación no encontrada."));

        await Assert.ThrowsAsync<NotFoundException>(
            () => CreateController().GetByIdAsync(99, default));
    }

    // ════════════════════════════════════════════════
    // TEST-BVC10: Create — bookspot no PENDING → burbujea ValidationException
    // ════════════════════════════════════════════════

    [Fact]
    public async Task CreateAsync_BookspotNotPending_BubblesValidationException()
    {
        _validationSvc
            .Setup(s => s.CreateAsync(TestSupabaseId, It.IsAny<CreateBookspotValidationRequest>(), default))
            .ThrowsAsync(new ValidationException("Solo se pueden validar bookspots en estado PENDING."));

        var request = new CreateBookspotValidationRequest
        {
            BookspotId = 1,
            KnowsPlace = true,
            SafeForExchange = true
        };

        await Assert.ThrowsAsync<ValidationException>(
            () => CreateController().CreateAsync(request, default));
    }
}