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

public class ExchangeMeetingControllerTests
{
    private readonly Mock<IExchangeMeetingService> _mockMeetingService;
    private readonly Mock<IExchangeService> _mockExchangeService;
    private readonly AppDbContext _db;
    private readonly ExchangeMeetingController _controller;
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();
    private readonly string _supabaseId = "test-supabase-meeting";

    public ExchangeMeetingControllerTests()
    {
        _mockMeetingService = new Mock<IExchangeMeetingService>();
        _mockExchangeService = new Mock<IExchangeService>();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _controller = new ExchangeMeetingController(_mockMeetingService.Object, _db, _mockExchangeService.Object);

        SetupUserInDb();
        SetupControllerContext();
    }

    private void SetupUserInDb()
    {
        _db.Users.Add(new BaseUser
        {
            Id = _currentUserId,
            SupabaseId = _supabaseId,
            Email = "meeting@test.com",
            Username = "meetinguser",
            Name = "Meeting User",
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

    private ExchangeMeeting BuildMeeting(
        int meetingId,
        int exchangeId,
        ExchangeMeetingStatus status = ExchangeMeetingStatus.PROPOSAL,
        ExchangeMode mode = ExchangeMode.BOOKSPOT,
        Guid? proposerId = null) => new ExchangeMeeting
    {
        ExchangeMeetingId = meetingId,
        ExchangeId = exchangeId,
        MeetingStatus = status,
        ExchangeMode = mode,
        ProposerId = proposerId ?? _otherUserId,
        CustomLocation = new Point(0, 0) { SRID = 4326 },
        ScheduledAt = DateTime.UtcNow.AddHours(1),
        Proposer = new User
        {
            Id = proposerId ?? _otherUserId,
            BaseUser = new BaseUser
            {
                Id = proposerId ?? _otherUserId,
                Name = "Test",
                Username = "test",
                Email = "t@t.com",
                SupabaseId = "other",
                Location = new Point(0, 0) { SRID = 4326 }
            }
        }
    };

    // --- Happy paths ---

    [Fact]
    public async Task GetExchangeMeeting_Exists_ReturnsOkWithDto()
    {
        var exchange = BuildExchange(1, ExchangeStatus.ACCEPTED);
        var meeting = BuildMeeting(10, 1);

        _mockMeetingService.Setup(s => s.GetExchangeMeeting(10)).ReturnsAsync(meeting);
        _mockExchangeService.Setup(s => s.GetExchangeWithMatch(1)).ReturnsAsync(exchange);

        var result = await _controller.GetExchangeMeeting(10);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ExchangeMeetingDto>(okResult.Value);
        Assert.Equal(10, dto.ExchangeMeetingId);
    }

    [Fact]
    public async Task GetMeetingByExchangeId_Exists_ReturnsOkWithDto()
    {
        var exchange = BuildExchange(1, ExchangeStatus.ACCEPTED);
        var meeting = BuildMeeting(10, 1);

        _mockMeetingService.Setup(s => s.GetMeetingByExchangeId(1)).ReturnsAsync(meeting);
        _mockExchangeService.Setup(s => s.GetExchangeWithMatch(1)).ReturnsAsync(exchange);

        var result = await _controller.GetMeetingByExchangeId(1);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<ExchangeMeetingDto>(okResult.Value);
    }

    [Fact]
    public async Task CreateExchangeMeeting_Valid_ReturnsCreated()
    {
        var exchange = BuildExchange(1, ExchangeStatus.ACCEPTED);
        var meeting = BuildMeeting(10, 1);

        _mockExchangeService.Setup(s => s.GetExchangeWithMatch(1)).ReturnsAsync(exchange);
        _mockMeetingService.Setup(s => s.GetMeetingByExchangeId(1)).ReturnsAsync((ExchangeMeeting?)null);
        _mockMeetingService
            .Setup(s => s.CreateExchangeMeeting(It.IsAny<CreateExchangeMeetingDto>(), _currentUserId))
            .ReturnsAsync(meeting);

        var dto = new CreateExchangeMeetingDto(1, ExchangeMode.BOOKSPOT, 1, null, null, DateTime.UtcNow.AddHours(1));

        var result = await _controller.CreateExchangeMeeting(dto);

        Assert.IsType<CreatedAtActionResult>(result);
    }

    [Fact]
    public async Task CounterProposeMeeting_Valid_ReturnsOk()
    {
        var exchange = BuildExchange(1, ExchangeStatus.ACCEPTED);
        var meeting = BuildMeeting(10, 1, proposerId: _otherUserId);
        var updatedMeeting = BuildMeeting(10, 1, mode: ExchangeMode.CUSTOM);

        _mockMeetingService.Setup(s => s.GetExchangeMeeting(10)).ReturnsAsync(meeting);
        _mockExchangeService.Setup(s => s.GetExchangeWithMatch(1)).ReturnsAsync(exchange);
        _mockMeetingService
            .Setup(s => s.CounterProposeMeeting(It.IsAny<ExchangeMeeting>(), It.IsAny<CounterProposeMeetingDto>(), _currentUserId))
            .ReturnsAsync(updatedMeeting);

        var dto = new CounterProposeMeetingDto(ExchangeMode.CUSTOM, null, 40.0, -3.0, DateTime.UtcNow.AddHours(2));

        var result = await _controller.CounterProposeMeeting(10, dto);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task CompleteExchange_Valid_ReturnsOk()
    {
        var exchange = BuildExchange(1, ExchangeStatus.ACCEPTED);
        var meeting = BuildMeeting(10, 1, status: ExchangeMeetingStatus.ACCEPTED, proposerId: _otherUserId);
        var completedMeeting = BuildMeeting(10, 1, status: ExchangeMeetingStatus.ACCEPTED, proposerId: _otherUserId);

        _mockMeetingService.Setup(s => s.GetExchangeMeeting(10)).ReturnsAsync(meeting);
        _mockExchangeService.Setup(s => s.GetExchangeWithMatch(1)).ReturnsAsync(exchange);
        _mockMeetingService
            .Setup(s => s.MarkAsCompleted(It.IsAny<ExchangeMeeting>(), _currentUserId))
            .ReturnsAsync(completedMeeting);

        var result = await _controller.CompleteExchange(10);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task AcceptExchangeMeeting_Valid_ReturnsOk()
    {
        var exchange = BuildExchange(1, ExchangeStatus.ACCEPTED);
        var meeting = BuildMeeting(10, 1, proposerId: _otherUserId);
        var acceptedMeeting = BuildMeeting(10, 1, status: ExchangeMeetingStatus.ACCEPTED, proposerId: _otherUserId);

        _mockMeetingService.Setup(s => s.GetExchangeMeeting(10)).ReturnsAsync(meeting);
        _mockExchangeService.Setup(s => s.GetExchangeWithMatch(1)).ReturnsAsync(exchange);
        _mockMeetingService
            .Setup(s => s.AcceptMeeting(It.IsAny<ExchangeMeeting>()))
            .ReturnsAsync(acceptedMeeting);

        var result = await _controller.AcceptExchangeMeeting(10);

        Assert.IsType<OkObjectResult>(result);
    }

    // --- Guardias de seguridad ---

    [Fact]
    public async Task CreateExchangeMeeting_ExchangeNotAccepted_ReturnsBadRequest()
    {
        var exchange = BuildExchange(1, ExchangeStatus.NEGOTIATING);
        _mockExchangeService.Setup(s => s.GetExchangeWithMatch(1)).ReturnsAsync(exchange);

        var dto = new CreateExchangeMeetingDto(1, ExchangeMode.BOOKSPOT, 1, null, null, DateTime.UtcNow.AddHours(1));

        var result = await _controller.CreateExchangeMeeting(dto);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateExchangeMeeting_MeetingAlreadyExists_ReturnsConflict()
    {
        var exchange = BuildExchange(1, ExchangeStatus.ACCEPTED);
        var existingMeeting = BuildMeeting(10, 1);

        _mockExchangeService.Setup(s => s.GetExchangeWithMatch(1)).ReturnsAsync(exchange);
        _mockMeetingService.Setup(s => s.GetMeetingByExchangeId(1)).ReturnsAsync(existingMeeting);

        var dto = new CreateExchangeMeetingDto(1, ExchangeMode.BOOKSPOT, 1, null, null, DateTime.UtcNow.AddHours(1));

        var result = await _controller.CreateExchangeMeeting(dto);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task CounterProposeMeeting_OwnProposal_ReturnsBadRequest()
    {
        var exchange = BuildExchange(1, ExchangeStatus.ACCEPTED);
        var meeting = BuildMeeting(10, 1, proposerId: _currentUserId);

        _mockMeetingService.Setup(s => s.GetExchangeMeeting(10)).ReturnsAsync(meeting);
        _mockExchangeService.Setup(s => s.GetExchangeWithMatch(1)).ReturnsAsync(exchange);

        var dto = new CounterProposeMeetingDto(ExchangeMode.CUSTOM, null, 40.0, -3.0, DateTime.UtcNow.AddHours(2));

        var result = await _controller.CounterProposeMeeting(10, dto);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CompleteExchange_BookdropMode_ReturnsBadRequest()
    {
        var exchange = BuildExchange(1, ExchangeStatus.ACCEPTED);
        var meeting = BuildMeeting(10, 1, status: ExchangeMeetingStatus.ACCEPTED, mode: ExchangeMode.BOOKDROP);

        _mockMeetingService.Setup(s => s.GetExchangeMeeting(10)).ReturnsAsync(meeting);
        _mockExchangeService.Setup(s => s.GetExchangeWithMatch(1)).ReturnsAsync(exchange);

        var result = await _controller.CompleteExchange(10);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CompleteExchange_AlreadyMarked_ReturnsBadRequest()
    {
        var exchange = BuildExchange(1, ExchangeStatus.ACCEPTED);
        var meeting = BuildMeeting(10, 1, status: ExchangeMeetingStatus.ACCEPTED, proposerId: _otherUserId);
        meeting.MarkAsCompletedByUser2 = true;

        _mockMeetingService.Setup(s => s.GetExchangeMeeting(10)).ReturnsAsync(meeting);
        _mockExchangeService.Setup(s => s.GetExchangeWithMatch(1)).ReturnsAsync(exchange);

        var result = await _controller.CompleteExchange(10);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task AcceptExchangeMeeting_OwnProposal_ReturnsBadRequest()
    {
        var exchange = BuildExchange(1, ExchangeStatus.ACCEPTED);
        var meeting = BuildMeeting(10, 1, proposerId: _currentUserId);

        _mockMeetingService.Setup(s => s.GetExchangeMeeting(10)).ReturnsAsync(meeting);
        _mockExchangeService.Setup(s => s.GetExchangeWithMatch(1)).ReturnsAsync(exchange);

        var result = await _controller.AcceptExchangeMeeting(10);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
