using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using NetTopologySuite.Geometries;
using Bookmerang.Api.Controllers.Exchanges;
using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Services.Interfaces.ExchangeInterfaces;
using Xunit;

using MatchEntity = Bookmerang.Api.Models.Entities.Match;

namespace Bookmerang.Tests.Exchanges;

public class ExchangeControllerTests
{
    private readonly Mock<IExchangeService> _mockService;
    private readonly Mock<IExchangeMeetingService> _mockMeetingService;
    private readonly AppDbContext _db;
    private readonly ExchangeController _controller;
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();
    private readonly string _supabaseId = "test-supabase-exchange";

    public ExchangeControllerTests()
    {
        _mockService = new Mock<IExchangeService>();
        _mockMeetingService = new Mock<IExchangeMeetingService>();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _controller = new ExchangeController(_mockService.Object, _mockMeetingService.Object, _db);

        SetupUserInDb();
        SetupControllerContext();
    }

    private void SetupUserInDb()
    {
        _db.Users.Add(new BaseUser
        {
            Id = _currentUserId,
            SupabaseId = _supabaseId,
            Email = "exchange@test.com",
            Username = "exchangeuser",
            Name = "Exchange User",
            Location = new Point(0, 0) { SRID = 4326 }
        });
        _db.SaveChanges();
    }

    private void SetupControllerContext()
    {
        var claims = new List<Claim>
        {
            new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", _supabaseId)
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    private Exchange BuildExchange(int exchangeId, ExchangeStatus status) => new Exchange
    {
        ExchangeId = exchangeId,
        ChatId = 10,
        MatchId = 1,
        Status = status,
        Match = new MatchEntity
        {
            User1Id = _currentUserId,
            User2Id = _otherUserId,
            Book1Id = 1,
            Book2Id = 2,
            Status = MatchStatus.CHAT_CREATED,
            CreatedAt = DateTime.UtcNow
        }
    };

    // --- GetExchange ---

    [Fact]
    public async Task GetExchange_Exists_ReturnsOkWithDto()
    {
        var exchangeId = 1;
        var exchange = BuildExchange(exchangeId, ExchangeStatus.NEGOTIATING);
        _mockService.Setup(s => s.GetExchangeWithMatch(exchangeId)).ReturnsAsync(exchange);

        var result = await _controller.GetExchange(exchangeId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ExchangeDto>(okResult.Value);
        Assert.Equal(exchangeId, dto.ExchangeId);
    }

    // --- GetExchangeByChatIdWithMatch ---

    [Fact]
    public async Task GetExchangeByChatId_Exists_ReturnsOkWithDto()
    {
        var chatId = 10;
        var exchange = BuildExchange(1, ExchangeStatus.NEGOTIATING);
        _mockService.Setup(s => s.GetExchangeByChatIdWithMatch(chatId)).ReturnsAsync(exchange);

        var result = await _controller.GetExchangeByChatIdWithMatchDetails(chatId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ExchangeWithMatchDto>(okResult.Value);
        Assert.Equal(chatId, dto.ChatId);
    }

    // --- AcceptExchange ---

    [Fact]
    public async Task AcceptExchange_Valid_ReturnsOkWithDto()
    {
        var exchangeId = 1;
        var exchange = BuildExchange(exchangeId, ExchangeStatus.NEGOTIATING);
        var accepted = BuildExchange(exchangeId, ExchangeStatus.ACCEPTED_BY_1);
        _mockService.Setup(s => s.GetExchangeWithMatch(exchangeId)).ReturnsAsync(exchange);
        _mockService.Setup(s => s.AcceptExchange(exchangeId, _currentUserId)).ReturnsAsync(accepted);

        var result = await _controller.AcceptExchange(exchangeId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ExchangeDto>(okResult.Value);
        Assert.Equal(ExchangeStatus.ACCEPTED_BY_1, dto.Status);
    }

    // --- RejectExchange ---

    [Fact]
    public async Task RejectExchange_Negotiating_ReturnsOkWithRejected()
    {
        var exchangeId = 1;
        var exchange = BuildExchange(exchangeId, ExchangeStatus.NEGOTIATING);
        var rejected = BuildExchange(exchangeId, ExchangeStatus.REJECTED);
        _mockService.Setup(s => s.GetExchangeWithMatch(exchangeId)).ReturnsAsync(exchange);
        _mockService.Setup(s => s.UpdateExchangeStatus(It.IsAny<Exchange>(), ExchangeStatus.REJECTED)).ReturnsAsync(rejected);

        var result = await _controller.RejectExchange(exchangeId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ExchangeDto>(okResult.Value);
        Assert.Equal(ExchangeStatus.REJECTED, dto.Status);
    }

    [Fact]
    public async Task RejectExchange_AlreadyAccepted_ReturnsBadRequest()
    {
        var exchangeId = 1;
        var exchange = BuildExchange(exchangeId, ExchangeStatus.ACCEPTED);
        _mockService.Setup(s => s.GetExchangeWithMatch(exchangeId)).ReturnsAsync(exchange);

        var result = await _controller.RejectExchange(exchangeId);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // --- ReportExchange ---

    [Fact]
    public async Task ReportExchange_MeetingAccepted_ReturnsOkWithIncident()
    {
        var exchangeId = 1;
        var exchange = BuildExchange(exchangeId, ExchangeStatus.ACCEPTED);
        var incident = BuildExchange(exchangeId, ExchangeStatus.INCIDENT);
        var meeting = new ExchangeMeeting
        {
            ExchangeId = exchangeId,
            MeetingStatus = ExchangeMeetingStatus.ACCEPTED,
            CustomLocation = null!
        };
        _mockService.Setup(s => s.GetExchangeWithMatch(exchangeId)).ReturnsAsync(exchange);
        _mockMeetingService.Setup(s => s.GetMeetingByExchangeId(exchangeId)).ReturnsAsync(meeting);
        _mockService.Setup(s => s.UpdateExchangeStatus(It.IsAny<Exchange>(), ExchangeStatus.INCIDENT)).ReturnsAsync(incident);

        var result = await _controller.ReportExchange(exchangeId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ExchangeDto>(okResult.Value);
        Assert.Equal(ExchangeStatus.INCIDENT, dto.Status);
    }

    [Fact]
    public async Task ReportExchange_NoAcceptedMeeting_ReturnsBadRequest()
    {
        var exchangeId = 1;
        var exchange = BuildExchange(exchangeId, ExchangeStatus.ACCEPTED);
        _mockService.Setup(s => s.GetExchangeWithMatch(exchangeId)).ReturnsAsync(exchange);
        _mockMeetingService.Setup(s => s.GetMeetingByExchangeId(exchangeId)).ReturnsAsync((ExchangeMeeting?)null);

        var result = await _controller.ReportExchange(exchangeId);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
