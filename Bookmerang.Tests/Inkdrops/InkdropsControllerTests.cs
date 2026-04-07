using Bookmerang.Api.Controllers.Inkdrops;
using Bookmerang.Api.Data;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Interfaces.Inkdrops;
using Bookmerang.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;
using Xunit;

namespace Bookmerang.Tests.Inkdrops;

public class InkdropsControllerTests : IAsyncLifetime
{
    private AppDbContext _db = null!;
    private Mock<IInkdropsService> _inkdropsService = null!;

    public Task InitializeAsync()
    {
        _db = DbContextFactory.CreateInMemory();
        _inkdropsService = new Mock<IInkdropsService>();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    private InkdropsController CreateController(Guid userId, string supabaseId = "sup-test")
    {
        _db.Users.Add(new BaseUser
        {
            Id = userId,
            SupabaseId = supabaseId,
            Email = $"inkdrops-{userId}@test.com",
            Username = $"inkdrops-{userId.ToString()[..8]}",
            Name = "Inkdrops User",
            ProfilePhoto = string.Empty,
            UserType = BaseUserType.USER,
            Location = new NetTopologySuite.Geometries.Point(0, 0) { SRID = 4326 },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        _db.SaveChanges();

        var controller = new InkdropsController(_inkdropsService.Object, _db);

        var claims = new List<Claim>
        {
            new("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", supabaseId)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        return controller;
    }

    [Fact]
    public async Task GetMyInkdrops_AuthenticatedUser_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var controller = CreateController(userId);
        var currentMonth = DateTime.UtcNow.ToString("yyyy-MM");

        _inkdropsService
            .Setup(s => s.GetUserInkdropsAsync(userId))
            .ReturnsAsync(new InkdropsDto(userId, 0, currentMonth));

        var result = await controller.GetMyInkdrops();

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetMyInkdrops_NotAuthenticated_ReturnsUnauthorized()
    {
        var controller = new InkdropsController(_inkdropsService.Object, _db);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
        };

        var result = await controller.GetMyInkdrops();

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetCommunityRanking_PremiumUser_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var controller = CreateController(userId);
        var currentMonth = DateTime.UtcNow.ToString("yyyy-MM");

        _inkdropsService
            .Setup(s => s.GetCommunityRankingAsync(userId, 1))
            .ReturnsAsync(new CommunityRankingDto(1, currentMonth, new List<CommunityRankingEntryDto>()));

        var result = await controller.GetCommunityRanking(1);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetCommunityRanking_FreeUser_ReturnsForbidden()
    {
        var userId = Guid.NewGuid();
        var controller = CreateController(userId);

        _inkdropsService
            .Setup(s => s.GetCommunityRankingAsync(userId, 1))
            .ThrowsAsync(new InvalidOperationException("Solo usuarios PREMIUM pueden ver el ranking"));

        var result = await controller.GetCommunityRanking(1);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetCommunityRanking_NotMember_ReturnsBadRequest()
    {
        var userId = Guid.NewGuid();
        var controller = CreateController(userId);

        _inkdropsService
            .Setup(s => s.GetCommunityRankingAsync(userId, 1))
            .ThrowsAsync(new InvalidOperationException("No eres miembro de esta comunidad"));

        var result = await controller.GetCommunityRanking(1);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetCommunityRanking_NotAuthenticated_ReturnsUnauthorized()
    {
        var controller = new InkdropsController(_inkdropsService.Object, _db);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
        };

        var result = await controller.GetCommunityRanking(1);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetInkdropsHistory_AuthenticatedUser_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var controller = CreateController(userId);

        _inkdropsService
            .Setup(s => s.GetInkdropsHistoryAsync(userId))
            .ReturnsAsync(new List<InkdropsHistoryDto>());

        var result = await controller.GetInkdropsHistory();

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetInkdropsHistory_NotAuthenticated_ReturnsUnauthorized()
    {
        var controller = new InkdropsController(_inkdropsService.Object, _db);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
        };

        var result = await controller.GetInkdropsHistory();

        Assert.IsType<UnauthorizedResult>(result);
    }
}
