using Bookmerang.Api.Controllers.Matcher;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Interfaces.Auth;
using Bookmerang.Api.Services.Interfaces.Matcher;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;
using Xunit;

namespace Bookmerang.Tests.Matcher;

/// <summary>
/// Tests del MatcherController — capa HTTP.
/// Mockean IMatcherService e IAuthService. No requieren BD.
/// </summary>
public class MatcherControllerTests
{
    private readonly Mock<IMatcherService> _matcherSvc = new();
    private readonly Mock<IAuthService> _authSvc = new();

    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly string TestSupabaseId = "sup-test-user";

    private MatcherController CreateController(bool authenticated = true)
    {
        var controller = new MatcherController(_matcherSvc.Object, _authSvc.Object);

        if (authenticated)
        {
            var claims = new List<Claim>
            {
                new("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", TestSupabaseId)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            _authSvc.Setup(a => a.GetPerfil(TestSupabaseId))
                .ReturnsAsync(new ProfileDto { Id = TestUserId, SupabaseId = TestSupabaseId });
        }
        else
        {
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
            };
        }

        return controller;
    }

    // ════════════════════════════════════════════════
    // TEST-C01: size > maxSize → 400 (audit issue #1)
    // ════════════════════════════════════════════════

    [Theory]
    [InlineData(101)]
    [InlineData(500)]
    [InlineData(1000000)]
    public async Task GetFeed_SizeExceedsMaximum_ReturnsBadRequest(int size)
    {
        var controller = CreateController();
        var result = await controller.GetFeed(page: 0, size: size);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ════════════════════════════════════════════════
    // TEST-C02: size = Int32.MaxValue → 400 (audit issue #1)
    // ════════════════════════════════════════════════

    [Fact]
    public async Task GetFeed_SizeIsMaxInt_ReturnsBadRequest()
    {
        var controller = CreateController();
        var result = await controller.GetFeed(page: 0, size: int.MaxValue);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ════════════════════════════════════════════════
    // TEST-C03: page > maxPage → 400 (audit issue #2)
    // ════════════════════════════════════════════════

    [Theory]
    [InlineData(1001)]
    [InlineData(107374182)]
    public async Task GetFeed_PageExceedsMaximum_ReturnsBadRequest(int page)
    {
        var controller = CreateController();
        var result = await controller.GetFeed(page: page, size: 20);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ════════════════════════════════════════════════
    // TEST-C04: size == maxSize (100) → 200 OK
    // ════════════════════════════════════════════════

    [Fact]
    public async Task GetFeed_SizeEqualsMaximum_ReturnsOk()
    {
        _matcherSvc.Setup(s => s.GetFeedAsync(TestUserId, 0, 100))
            .ReturnsAsync(new FeedResultDto { Items = [], Page = 0, PageSize = 100, HasMore = false });

        var controller = CreateController();
        var result = await controller.GetFeed(page: 0, size: 100);
        Assert.IsType<OkObjectResult>(result);
    }

    // ════════════════════════════════════════════════
    // TEST-C05: Parámetros negativos rechazados
    // ════════════════════════════════════════════════

    [Fact]
    public async Task GetFeed_NegativePage_ReturnsBadRequest()
    {
        var controller = CreateController();
        var result = await controller.GetFeed(page: -1, size: 20);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetFeed_ZeroSize_ReturnsBadRequest()
    {
        var controller = CreateController();
        var result = await controller.GetFeed(page: 0, size: 0);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ════════════════════════════════════════════════
    // TEST-C06: Petición sin autenticación → 401
    // ════════════════════════════════════════════════

    [Fact]
    public async Task GetFeed_Unauthenticated_ReturnsUnauthorized()
    {
        var controller = CreateController(authenticated: false);
        var result = await controller.GetFeed(page: 0, size: 20);
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Swipe_Unauthenticated_ReturnsUnauthorized()
    {
        var controller = CreateController(authenticated: false);
        var result = await controller.Swipe(new SwipeRequestDto
        {
            BookId = 1,
            Direction = SwipeDirection.RIGHT
        });
        Assert.IsType<UnauthorizedResult>(result);
    }

    // ════════════════════════════════════════════════
    // TEST-C07: Swipe sobre libro inexistente → 404
    // ════════════════════════════════════════════════

    [Fact]
    public async Task Swipe_BookNotFound_ReturnsNotFound()
    {
        _matcherSvc.Setup(s => s.ProcessSwipeAsync(TestUserId, 999, SwipeDirection.RIGHT))
            .ThrowsAsync(new KeyNotFoundException("No se encontró el libro con ID 999."));

        var controller = CreateController();
        var result = await controller.Swipe(new SwipeRequestDto
        {
            BookId = 999,
            Direction = SwipeDirection.RIGHT
        });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ════════════════════════════════════════════════
    // TEST-C08: Swipe duplicado → 409
    // ════════════════════════════════════════════════

    [Fact]
    public async Task Swipe_DuplicateSwipe_ReturnsConflict()
    {
        _matcherSvc.Setup(s => s.ProcessSwipeAsync(TestUserId, 1, SwipeDirection.LEFT))
            .ThrowsAsync(new DbUpdateException("duplicate key"));

        var controller = CreateController();
        var result = await controller.Swipe(new SwipeRequestDto
        {
            BookId = 1,
            Direction = SwipeDirection.LEFT
        });

        Assert.IsType<ConflictObjectResult>(result);
    }

    // ════════════════════════════════════════════════
    // TEST-C09: Petición válida normal → 200 con datos
    // ════════════════════════════════════════════════

    [Fact]
    public async Task GetFeed_ValidRequest_ReturnsOkWithFeed()
    {
        var feedBooks = new List<FeedBookDto>
        {
            new() { Id = 1, OwnerId = Guid.NewGuid(), OwnerUsername = "test", Score = 0.8 }
        };
        _matcherSvc.Setup(s => s.GetFeedAsync(TestUserId, 0, 20))
            .ReturnsAsync(new FeedResultDto { Items = feedBooks, Page = 0, PageSize = 20, HasMore = false });

        var controller = CreateController();
        var result = await controller.GetFeed(page: 0, size: 20);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedFeed = Assert.IsType<FeedResultDto>(okResult.Value);
        Assert.Single(returnedFeed.Items);
    }

    // ════════════════════════════════════════════════
    // TEST-C10: Swipe válido → 200 con SwipeResultDto
    // ════════════════════════════════════════════════

    [Fact]
    public async Task Swipe_ValidSwipe_ReturnsOk()
    {
        _matcherSvc.Setup(s => s.ProcessSwipeAsync(TestUserId, 1, SwipeDirection.RIGHT))
            .ReturnsAsync(new SwipeResultDto { Outcome = SwipeOutcome.Recorded });

        var controller = CreateController();
        var result = await controller.Swipe(new SwipeRequestDto
        {
            BookId = 1,
            Direction = SwipeDirection.RIGHT
        });

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<SwipeResultDto>(okResult.Value);
        Assert.Equal(SwipeOutcome.Recorded, dto.Outcome);
    }

    // ════════════════════════════════════════════════
    // TEST-C11: Auto-swipe (libro propio) → 400
    // ════════════════════════════════════════════════

    [Fact]
    public async Task Swipe_OwnBook_ReturnsBadRequest()
    {
        _matcherSvc.Setup(s => s.ProcessSwipeAsync(TestUserId, 1, SwipeDirection.RIGHT))
            .ThrowsAsync(new InvalidOperationException("No puedes hacer swipe a tu propio libro."));

        var controller = CreateController();
        var result = await controller.Swipe(new SwipeRequestDto
        {
            BookId = 1,
            Direction = SwipeDirection.RIGHT
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ════════════════════════════════════════════════
    // TEST-C12: Undo válido → 200
    // ════════════════════════════════════════════════

    [Fact]
    public async Task UndoLastSwipe_Success_ReturnsOk()
    {
        _matcherSvc.Setup(s => s.UndoLastSwipeAsync(TestUserId))
            .ReturnsAsync(true);

        var controller = CreateController();
        var result = await controller.UndoLastSwipe();
        Assert.IsType<OkObjectResult>(result);
    }

    // ════════════════════════════════════════════════
    // TEST-C13: Undo imposible → 400
    // ════════════════════════════════════════════════

    [Fact]
    public async Task UndoLastSwipe_NoSwipeOrMatched_ReturnsBadRequest()
    {
        _matcherSvc.Setup(s => s.UndoLastSwipeAsync(TestUserId))
            .ReturnsAsync(false);

        var controller = CreateController();
        var result = await controller.UndoLastSwipe();
        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ════════════════════════════════════════════════
    // TEST-C14: Undo sin autenticación → 401
    // ════════════════════════════════════════════════

    [Fact]
    public async Task UndoLastSwipe_Unauthenticated_ReturnsUnauthorized()
    {
        var controller = CreateController(authenticated: false);
        var result = await controller.UndoLastSwipe();
        Assert.IsType<UnauthorizedResult>(result);
    }
}
