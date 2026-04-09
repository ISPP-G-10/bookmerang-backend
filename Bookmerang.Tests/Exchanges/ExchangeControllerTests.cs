using Bookmerang.Api.Controllers.Exchanges;
using Bookmerang.Api.Data;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Interfaces.ExchangeInterfaces;
using Bookmerang.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;
using Xunit;

namespace Bookmerang.Tests.Exchanges;

public class ExchangeControllerTests : IAsyncLifetime
{
	private AppDbContext _db = null!;
	private Mock<IExchangeService> _exchangeService = null!;
	private Mock<IExchangeMeetingService> _meetingService = null!;

	public Task InitializeAsync()
	{
		_db = DbContextFactory.CreateInMemory();
		_exchangeService = new Mock<IExchangeService>();
		_meetingService = new Mock<IExchangeMeetingService>();
		return Task.CompletedTask;
	}

	public async Task DisposeAsync()
	{
		await _db.Database.EnsureDeletedAsync();
		await _db.DisposeAsync();
	}

	private ExchangeController CreateController(Guid userId, string supabaseId = "sup-test")
	{
		_db.Users.Add(new BaseUser
		{
			Id = userId,
			SupabaseId = supabaseId,
			Email = "exchange@test.com",
			Username = "exchange-user",
			Name = "Exchange User",
			ProfilePhoto = string.Empty,
			UserType = BaseUserType.USER,
			Location = new NetTopologySuite.Geometries.Point(0, 0) { SRID = 4326 },
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		});
		_db.SaveChanges();

		var controller = new ExchangeController(_exchangeService.Object, _db, _meetingService.Object);

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
	public async Task CreateExchange_ServiceRejectsInvalidMatch_ThrowsInvalidOperationException()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);
		var chatId = Guid.NewGuid();
		var dto = new ExchangeDto(null, chatId, 999, null, null, null);

		_exchangeService
			.Setup(s => s.CreateExchange(chatId, 999))
			.ThrowsAsync(new InvalidOperationException("Match con id 999 no existe."));

		await Assert.ThrowsAsync<InvalidOperationException>(() => controller.CreateExchange(dto));
	}

	[Fact]
	public async Task AcceptExchange_User1FromNegotiating_TransitionsToAcceptedBy1()
	{
		var user1 = Guid.NewGuid();
		var user2 = Guid.NewGuid();
		var controller = CreateController(user1);

		_exchangeService
			.Setup(s => s.GetExchangeWithMatch(100))
			.ReturnsAsync(new Exchange
			{
				ExchangeId = 100,
				ChatId = Guid.NewGuid(),
				MatchId = 10,
				Status = ExchangeStatus.NEGOTIATING,
				Match = new Api.Models.Entities.Match
				{
					Id = 10,
					User1Id = user1,
					User2Id = user2,
					Book1Id = 1,
					Book2Id = 2,
					Status = MatchStatus.NEW,
					CreatedAt = DateTime.UtcNow
				}
			});

		_exchangeService
			.Setup(s => s.UpdateExchangeStatus(100, ExchangeStatus.ACCEPTED_BY_1))
			.ReturnsAsync(new Exchange
			{
				ExchangeId = 100,
				ChatId = Guid.NewGuid(),
				MatchId = 10,
				Status = ExchangeStatus.ACCEPTED_BY_1,
				Match = new Api.Models.Entities.Match
				{
					Id = 10,
					User1Id = user1,
					User2Id = user2,
					Book1Id = 1,
					Book2Id = 2,
					Status = MatchStatus.NEW,
					CreatedAt = DateTime.UtcNow
				}
			});

		var result = await controller.AcceptExchange(100);

		Assert.IsType<OkObjectResult>(result);
		_exchangeService.Verify(s => s.UpdateExchangeStatus(100, ExchangeStatus.ACCEPTED_BY_1), Times.Once);
	}

	[Fact]
	public async Task AcceptExchange_SecondAcceptance_TransitionsToAccepted()
	{
		var user1 = Guid.NewGuid();
		var user2 = Guid.NewGuid();
		var controller = CreateController(user1);

		_exchangeService
			.Setup(s => s.GetExchangeWithMatch(200))
			.ReturnsAsync(new Exchange
			{
				ExchangeId = 200,
				ChatId = Guid.NewGuid(),
				MatchId = 10,
				Status = ExchangeStatus.ACCEPTED_BY_2,
				Match = new Api.Models.Entities.Match
				{
					Id = 10,
					User1Id = user1,
					User2Id = user2,
					Book1Id = 1,
					Book2Id = 2,
					Status = MatchStatus.NEW,
					CreatedAt = DateTime.UtcNow
				}
			});

		_exchangeService
			.Setup(s => s.UpdateExchangeStatus(200, ExchangeStatus.ACCEPTED))
			.ReturnsAsync(new Exchange
			{
				ExchangeId = 200,
				ChatId = Guid.NewGuid(),
				MatchId = 10,
				Status = ExchangeStatus.ACCEPTED
			});

		var result = await controller.AcceptExchange(200);

		Assert.IsType<OkObjectResult>(result);
		_exchangeService.Verify(s => s.UpdateExchangeStatus(200, ExchangeStatus.ACCEPTED), Times.Once);
	}

	[Fact]
	public async Task AcceptExchange_UserAlreadyAccepted_ReturnsForbidden()
	{
		var user1 = Guid.NewGuid();
		var user2 = Guid.NewGuid();
		var controller = CreateController(user1);

		_exchangeService
			.Setup(s => s.GetExchangeWithMatch(300))
			.ReturnsAsync(new Exchange
			{
				ExchangeId = 300,
				ChatId = Guid.NewGuid(),
				MatchId = 10,
				Status = ExchangeStatus.ACCEPTED_BY_1,
				Match = new Api.Models.Entities.Match
				{
					Id = 10,
					User1Id = user1,
					User2Id = user2,
					Book1Id = 1,
					Book2Id = 2,
					Status = MatchStatus.NEW,
					CreatedAt = DateTime.UtcNow
				}
			});

		var result = await controller.AcceptExchange(300);

		var objectResult = Assert.IsType<ObjectResult>(result);
		Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
		_exchangeService.Verify(s => s.UpdateExchangeStatus(It.IsAny<int>(), It.IsAny<ExchangeStatus>()), Times.Never);
	}

	// GET EXCHANGE TESTS
	[Fact]
	public async Task GetExchange_ExchangeExists_ReturnsOkWithExchange()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);

		_exchangeService
			.Setup(s => s.GetExchangeById(50))
			.ReturnsAsync(new Exchange
			{
				ExchangeId = 50,
				ChatId = Guid.NewGuid(),
				MatchId = 5,
				Status = ExchangeStatus.NEGOTIATING,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			});

		var result = await controller.GetExchange(50);

		Assert.IsType<OkObjectResult>(result);
		_exchangeService.Verify(s => s.GetExchangeById(50), Times.Once);
	}

	[Fact]
	public async Task GetExchange_ExchangeDoesNotExist_ReturnsNotFound()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);

		_exchangeService
			.Setup(s => s.GetExchangeById(9999))
			.ReturnsAsync((Exchange?)null);

		var result = await controller.GetExchange(9999);

		Assert.IsType<NotFoundObjectResult>(result);
	}

	[Fact]
	public async Task GetExchange_UserNotAuthenticated_ReturnsUnauthorized()
	{
		var controller = new ExchangeController(_exchangeService.Object, _db, _meetingService.Object);
		controller.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
		};

		var result = await controller.GetExchange(50);

		Assert.IsType<UnauthorizedResult>(result);
	}

	// GET EXCHANGE BY CHAT TESTS
	[Fact]
	public async Task GetExchangeByChatIdWithMatchDetails_ExchangeExists_ReturnsOk()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);
		var chatId = Guid.NewGuid();

		_exchangeService
			.Setup(s => s.GetExchangeByChatIdWithMatch(chatId))
			.ReturnsAsync(new Exchange
			{
				ExchangeId = 60,
				ChatId = chatId,
				MatchId = 60,
				Status = ExchangeStatus.NEGOTIATING,
				Match = new Api.Models.Entities.Match
				{
					Id = 60,
					User1Id = userId,
					User2Id = Guid.NewGuid(),
					Book1Id = 1,
					Book2Id = 2,
					Status = MatchStatus.NEW,
					CreatedAt = DateTime.UtcNow
				},
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			});

		var result = await controller.GetExchangeByChatIdWithMatchDetails(chatId);

		Assert.IsType<OkObjectResult>(result);
	}

	[Fact]
	public async Task GetExchangeByChatIdWithMatchDetails_ExchangeNotFound_ReturnsNotFound()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);
		var chatId = Guid.NewGuid();

		_exchangeService
			.Setup(s => s.GetExchangeByChatIdWithMatch(chatId))
			.ReturnsAsync((Exchange?)null);

		var result = await controller.GetExchangeByChatIdWithMatchDetails(chatId);

		Assert.IsType<NotFoundObjectResult>(result);
	}

	// GET ALL EXCHANGES TESTS
	[Fact]
	public async Task GetAllExchanges_ExchangesExist_ReturnsOkWithList()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);

		_exchangeService
			.Setup(s => s.GetAllExchanges())
			.ReturnsAsync(new List<Exchange>
			{
				new() { ExchangeId = 70, ChatId = Guid.NewGuid(), MatchId = 70, Status = ExchangeStatus.NEGOTIATING, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
				new() { ExchangeId = 71, ChatId = Guid.NewGuid(), MatchId = 71, Status = ExchangeStatus.NEGOTIATING, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
			});

		var result = await controller.GetAllExchanges();

		Assert.IsType<OkObjectResult>(result);
	}

	[Fact]
	public async Task GetAllExchanges_NoExchanges_ReturnsNotFound()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);

		_exchangeService
			.Setup(s => s.GetAllExchanges())
			.ReturnsAsync(new List<Exchange>());

		var result = await controller.GetAllExchanges();

		Assert.IsType<NotFoundObjectResult>(result);
	}

	[Fact]
	public async Task GetAllExchanges_UserNotAuthenticated_ReturnsUnauthorized()
	{
		var controller = new ExchangeController(_exchangeService.Object, _db, _meetingService.Object);
		controller.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
		};

		var result = await controller.GetAllExchanges();

		Assert.IsType<UnauthorizedResult>(result);
	}

	// CREATE EXCHANGE TESTS
	[Fact]
	public async Task CreateExchange_ValidDto_ReturnsCreatedAtAction()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);
		var chatId = Guid.NewGuid();
		var dto = new ExchangeDto(null, chatId, 100, null, null, null);

		_exchangeService
			.Setup(s => s.CreateExchange(chatId, 100))
			.ReturnsAsync(new Exchange
			{
				ExchangeId = 100,
				ChatId = chatId,
				MatchId = 100,
				Status = ExchangeStatus.NEGOTIATING,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			});

		var result = await controller.CreateExchange(dto);

		Assert.IsType<CreatedAtActionResult>(result);
	}

	[Fact]
	public async Task CreateExchange_MissingChatId_ReturnsBadRequest()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);
		var dto = new ExchangeDto(null, null, 100, null, null, null);

		var result = await controller.CreateExchange(dto);

		Assert.IsType<BadRequestObjectResult>(result);
	}

	[Fact]
	public async Task CreateExchange_MissingMatchId_ReturnsBadRequest()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);
		var dto = new ExchangeDto(null, Guid.NewGuid(), null, null, null, null);

		var result = await controller.CreateExchange(dto);

		Assert.IsType<BadRequestObjectResult>(result);
	}

	[Fact]
	public async Task CreateExchange_UserNotAuthenticated_ReturnsUnauthorized()
	{
		var controller = new ExchangeController(_exchangeService.Object, _db, _meetingService.Object);
		controller.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
		};
		var dto = new ExchangeDto(null, Guid.NewGuid(), 100, null, null, null);

		var result = await controller.CreateExchange(dto);

		Assert.IsType<UnauthorizedResult>(result);
	}

	// REJECT EXCHANGE TESTS
	[Fact]
	public async Task RejectExchange_NoMeetings_ReturnsOk()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);

		_meetingService
			.Setup(s => s.GetMeetingByExchangeId(110))
			.ReturnsAsync((ExchangeMeeting?)null);

		_exchangeService
			.Setup(s => s.GetExchangeById(110))
			.ReturnsAsync(new Exchange
			{
				ExchangeId = 110,
				ChatId = Guid.NewGuid(),
				MatchId = 110,
				Status = ExchangeStatus.REJECTED,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			});

		_exchangeService
			.Setup(s => s.DeleteExchange(110))
			.ReturnsAsync(true);

		var result = await controller.RejectExchange(110);

		Assert.IsType<OkObjectResult>(result);
	}

	[Fact]
	public async Task RejectExchange_MeetingExists_ReturnsBadRequest()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);

		_meetingService
			.Setup(s => s.GetMeetingByExchangeId(111))
			.ReturnsAsync(new ExchangeMeeting
			{
				ExchangeMeetingId = 111,
				ExchangeId = 111,
				ScheduledAt = DateTime.UtcNow.AddDays(1),
				ExchangeMode = Bookmerang.Api.Models.Enums.ExchangeMode.BOOKSPOT,
				ProposerId = userId,
				CustomLocation = new NetTopologySuite.Geometries.Point(0, 0) { SRID = 4326 },
				MeetingStatus = Bookmerang.Api.Models.Enums.ExchangeMeetingStatus.PROPOSAL
			});

		var result = await controller.RejectExchange(111);

		Assert.IsType<BadRequestObjectResult>(result);
	}

	[Fact]
	public async Task RejectExchange_ServiceThrowsException_ReturnsBadRequest()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);

		_meetingService
			.Setup(s => s.GetMeetingByExchangeId(112))
			.ReturnsAsync((ExchangeMeeting?)null);

		_exchangeService
			.Setup(s => s.GetExchangeById(112))
			.ReturnsAsync(new Exchange
			{
				ExchangeId = 112,
				ChatId = Guid.NewGuid(),
				MatchId = 112,
				Status = ExchangeStatus.NEGOTIATING,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			});

		_exchangeService
			.Setup(s => s.DeleteExchange(112))
			.ThrowsAsync(new InvalidOperationException("Exchange not found"));

		var result = await controller.RejectExchange(112);

		Assert.IsType<BadRequestObjectResult>(result);
	}

	[Fact]
	public async Task RejectExchange_UserNotAuthenticated_ReturnsUnauthorized()
	{
		var controller = new ExchangeController(_exchangeService.Object, _db, _meetingService.Object);
		controller.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
		};

		var result = await controller.RejectExchange(110);

		Assert.IsType<UnauthorizedResult>(result);
	}

	// REPORT EXCHANGE TESTS
	[Fact]
	public async Task ReportExchange_ValidExchange_ReturnsOk()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);

		_exchangeService
			.Setup(s => s.UpdateExchangeStatus(120, ExchangeStatus.INCIDENT))
			.ReturnsAsync(new Exchange
			{
				ExchangeId = 120,
				ChatId = Guid.NewGuid(),
				MatchId = 120,
				Status = ExchangeStatus.INCIDENT,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			});

		var result = await controller.ReportExchange(120);

		Assert.IsType<OkObjectResult>(result);
		_exchangeService.Verify(s => s.UpdateExchangeStatus(120, ExchangeStatus.INCIDENT), Times.Once);
	}

	[Fact]
	public async Task ReportExchange_ServiceThrowsException_ReturnsBadRequest()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);

		_exchangeService
			.Setup(s => s.UpdateExchangeStatus(121, ExchangeStatus.INCIDENT))
			.ThrowsAsync(new InvalidOperationException("Exchange already rejected"));

		var result = await controller.ReportExchange(121);

		Assert.IsType<BadRequestObjectResult>(result);
	}

	[Fact]
	public async Task ReportExchange_UserNotAuthenticated_ReturnsUnauthorized()
	{
		var controller = new ExchangeController(_exchangeService.Object, _db, _meetingService.Object);
		controller.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
		};

		var result = await controller.ReportExchange(120);

		Assert.IsType<UnauthorizedResult>(result);
	}

	// DELETE EXCHANGE TESTS
	[Fact]
	public async Task DeleteExchange_ExchangeExists_ReturnsNoContent()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);

		_exchangeService
			.Setup(s => s.DeleteExchange(130))
			.ReturnsAsync(true);

		var result = await controller.DeleteExchange(130);

		Assert.IsType<NoContentResult>(result);
	}

	[Fact]
	public async Task DeleteExchange_DeletionFails_ReturnsBadRequest()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);

		_exchangeService
			.Setup(s => s.DeleteExchange(131))
			.ReturnsAsync(false);

		var result = await controller.DeleteExchange(131);

		Assert.IsType<BadRequestObjectResult>(result);
	}

	[Fact]
	public async Task DeleteExchange_ServiceThrowsException_ReturnsNotFound()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);

		_exchangeService
			.Setup(s => s.DeleteExchange(132))
			.ThrowsAsync(new Exception("Exchange not found"));

		var result = await controller.DeleteExchange(132);

		Assert.IsType<NotFoundObjectResult>(result);
	}

	[Fact]
	public async Task DeleteExchange_UserNotAuthenticated_ReturnsUnauthorized()
	{
		var controller = new ExchangeController(_exchangeService.Object, _db, _meetingService.Object);
		controller.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
		};

		var result = await controller.DeleteExchange(130);

		Assert.IsType<UnauthorizedResult>(result);
	}

	// ADDITIONAL TEST CASES FOR EDGE CONDITIONS
	[Fact]
	public async Task AcceptExchange_User2FromNegotiating_TransitionsToAcceptedBy2()
	{
		var user1 = Guid.NewGuid();
		var user2 = Guid.NewGuid();
		var controller = CreateController(user2);

		_exchangeService
			.Setup(s => s.GetExchangeWithMatch(400))
			.ReturnsAsync(new Exchange
			{
				ExchangeId = 400,
				ChatId = Guid.NewGuid(),
				MatchId = 10,
				Status = ExchangeStatus.NEGOTIATING,
				Match = new Api.Models.Entities.Match
				{
					Id = 10,
					User1Id = user1,
					User2Id = user2,
					Book1Id = 1,
					Book2Id = 2,
					Status = MatchStatus.NEW,
					CreatedAt = DateTime.UtcNow
				}
			});

		_exchangeService
			.Setup(s => s.UpdateExchangeStatus(400, ExchangeStatus.ACCEPTED_BY_2))
			.ReturnsAsync(new Exchange
			{
				ExchangeId = 400,
				ChatId = Guid.NewGuid(),
				MatchId = 10,
				Status = ExchangeStatus.ACCEPTED_BY_2
			});

		var result = await controller.AcceptExchange(400);

		Assert.IsType<OkObjectResult>(result);
		_exchangeService.Verify(s => s.UpdateExchangeStatus(400, ExchangeStatus.ACCEPTED_BY_2), Times.Once);
	}

	[Fact]
	public async Task AcceptExchange_User2CannotAcceptWhenAlreadyAcceptedBy2_ReturnsForbidden()
	{
		var user1 = Guid.NewGuid();
		var user2 = Guid.NewGuid();
		var controller = CreateController(user2);

		_exchangeService
			.Setup(s => s.GetExchangeWithMatch(401))
			.ReturnsAsync(new Exchange
			{
				ExchangeId = 401,
				ChatId = Guid.NewGuid(),
				MatchId = 10,
				Status = ExchangeStatus.ACCEPTED_BY_2,
				Match = new Api.Models.Entities.Match
				{
					Id = 10,
					User1Id = user1,
					User2Id = user2,
					Book1Id = 1,
					Book2Id = 2,
					Status = MatchStatus.NEW,
					CreatedAt = DateTime.UtcNow
				}
			});

		var result = await controller.AcceptExchange(401);

		var objectResult = Assert.IsType<ObjectResult>(result);
		Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
	}

	[Fact]
	public async Task AcceptExchange_BothUsersAccepted_TransitionsToAccepted()
	{
		var user1 = Guid.NewGuid();
		var user2 = Guid.NewGuid();
		var controller = CreateController(user1);

		_exchangeService
			.Setup(s => s.GetExchangeWithMatch(402))
			.ReturnsAsync(new Exchange
			{
				ExchangeId = 402,
				ChatId = Guid.NewGuid(),
				MatchId = 10,
				Status = ExchangeStatus.ACCEPTED_BY_2,
				Match = new Api.Models.Entities.Match
				{
					Id = 10,
					User1Id = user1,
					User2Id = user2,
					Book1Id = 1,
					Book2Id = 2,
					Status = MatchStatus.NEW,
					CreatedAt = DateTime.UtcNow
				}
			});

		_exchangeService
			.Setup(s => s.UpdateExchangeStatus(402, ExchangeStatus.ACCEPTED))
			.ReturnsAsync(new Exchange
			{
				ExchangeId = 402,
				ChatId = Guid.NewGuid(),
				MatchId = 10,
				Status = ExchangeStatus.ACCEPTED
			});

		var result = await controller.AcceptExchange(402);

		Assert.IsType<OkObjectResult>(result);
		_exchangeService.Verify(s => s.UpdateExchangeStatus(402, ExchangeStatus.ACCEPTED), Times.Once);
	}

	[Fact]
	public async Task AcceptExchange_TransitionsFromAcceptedBy1_ToAccepted()
	{
		var user1 = Guid.NewGuid();
		var user2 = Guid.NewGuid();
		var controller = CreateController(user2);

		_exchangeService
			.Setup(s => s.GetExchangeWithMatch(403))
			.ReturnsAsync(new Exchange
			{
				ExchangeId = 403,
				ChatId = Guid.NewGuid(),
				MatchId = 10,
				Status = ExchangeStatus.ACCEPTED_BY_1,
				Match = new Api.Models.Entities.Match
				{
					Id = 10,
					User1Id = user1,
					User2Id = user2,
					Book1Id = 1,
					Book2Id = 2,
					Status = MatchStatus.NEW,
					CreatedAt = DateTime.UtcNow
				}
			});

		_exchangeService
			.Setup(s => s.UpdateExchangeStatus(403, ExchangeStatus.ACCEPTED))
			.ReturnsAsync(new Exchange
			{
				ExchangeId = 403,
				ChatId = Guid.NewGuid(),
				MatchId = 10,
				Status = ExchangeStatus.ACCEPTED
			});

		var result = await controller.AcceptExchange(403);

		Assert.IsType<OkObjectResult>(result);
		_exchangeService.Verify(s => s.UpdateExchangeStatus(403, ExchangeStatus.ACCEPTED), Times.Once);
	}
}
