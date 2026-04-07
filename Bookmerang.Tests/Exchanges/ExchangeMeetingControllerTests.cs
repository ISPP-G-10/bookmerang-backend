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
using NetTopologySuite.Geometries;
using System.Security.Claims;
using Xunit;

namespace Bookmerang.Tests.Exchanges;

public class ExchangeMeetingControllerTests : IAsyncLifetime
{
	private AppDbContext _db = null!;
	private Mock<IExchangeMeetingService> _meetingService = null!;
	private Mock<IExchangeService> _exchangeService = null!;

	public Task InitializeAsync()
	{
		_db = DbContextFactory.CreateInMemory();
		_meetingService = new Mock<IExchangeMeetingService>();
		_exchangeService = new Mock<IExchangeService>();
		return Task.CompletedTask;
	}

	public async Task DisposeAsync()
	{
		await _db.Database.EnsureDeletedAsync();
		await _db.DisposeAsync();
	}

	private ExchangeMeetingController CreateController(Guid userId, string supabaseId = "sup-meeting")
	{
		_db.Users.Add(new BaseUser
		{
			Id = userId,
			SupabaseId = supabaseId,
			Email = "meeting@test.com",
			Username = "meeting-user",
			Name = "Meeting User",
			ProfilePhoto = string.Empty,
			UserType = BaseUserType.USER,
			Location = new Point(0, 0) { SRID = 4326 },
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		});
		_db.SaveChanges();

		var controller = new ExchangeMeetingController(_meetingService.Object, _db, _exchangeService.Object);

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

	// === COMPLETE EXCHANGE TESTS ===

	[Fact]
	public async Task CompleteExchange_CurrentUserIsProposer_MarksCompletedByUser1()
	{
		var proposerId = Guid.NewGuid();
		var controller = CreateController(proposerId);

		_meetingService
			.Setup(s => s.GetExchangeMeeting(70))
			.ReturnsAsync(new ExchangeMeeting
			{
				ExchangeMeetingId = 70,
				ExchangeId = 9,
				ProposerId = proposerId,
				ExchangeMode = ExchangeMode.BOOKSPOT,
				CustomLocation = new Point(0, 0) { SRID = 4326 }
			});

		_meetingService
			.Setup(s => s.UpdateExchangeMeeting(70, It.IsAny<UpdateExchangeMeetingDto>()))
			.ReturnsAsync(new ExchangeMeeting
			{
				ExchangeMeetingId = 70,
				ExchangeId = 9,
				ProposerId = proposerId,
				ExchangeMode = ExchangeMode.BOOKSPOT,
				CustomLocation = new Point(0, 0) { SRID = 4326 },
				MarkAsCompletedByUser1 = true
			});

		var result = await controller.CompleteExchange(70);

		Assert.IsType<OkObjectResult>(result);
		_meetingService.Verify(s => s.UpdateExchangeMeeting(
			70,
			It.Is<UpdateExchangeMeetingDto>(d =>
				d.MarkAsCompletedByUser1 == true && d.MarkAsCompletedByUser2 == null)), Times.Once);
	}

	[Fact]
	public async Task CompleteExchange_CurrentUserIsNotProposer_MarksCompletedByUser2()
	{
		var proposerId = Guid.NewGuid();
		var otherUserId = Guid.NewGuid();
		var controller = CreateController(otherUserId);

		_meetingService
			.Setup(s => s.GetExchangeMeeting(71))
			.ReturnsAsync(new ExchangeMeeting
			{
				ExchangeMeetingId = 71,
				ExchangeId = 9,
				ProposerId = proposerId,
				ExchangeMode = ExchangeMode.BOOKSPOT,
				CustomLocation = new Point(0, 0) { SRID = 4326 }
			});

		_meetingService
			.Setup(s => s.UpdateExchangeMeeting(71, It.IsAny<UpdateExchangeMeetingDto>()))
			.ReturnsAsync(new ExchangeMeeting
			{
				ExchangeMeetingId = 71,
				ExchangeId = 9,
				ProposerId = proposerId,
				ExchangeMode = ExchangeMode.BOOKSPOT,
				CustomLocation = new Point(0, 0) { SRID = 4326 },
				MarkAsCompletedByUser2 = true
			});

		var result = await controller.CompleteExchange(71);

		Assert.IsType<OkObjectResult>(result);
		_meetingService.Verify(s => s.UpdateExchangeMeeting(
			71,
			It.Is<UpdateExchangeMeetingDto>(d =>
				d.MarkAsCompletedByUser2 == true && d.MarkAsCompletedByUser1 == null)), Times.Once);
	}

	[Fact]
	public async Task CompleteExchange_UserNotAuthenticated_ReturnsUnauthorized()
	{
		var controller = new ExchangeMeetingController(_meetingService.Object, _db, _exchangeService.Object);
		controller.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
		};

		var result = await controller.CompleteExchange(70);

		Assert.IsType<UnauthorizedResult>(result);
	}

	// === GET MEETING TESTS ===

	[Fact]
	public async Task GetExchangeMeeting_MeetingExists_ReturnsOk()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);

		_meetingService
			.Setup(s => s.GetExchangeMeeting(100))
			.ReturnsAsync(new ExchangeMeeting
			{
				ExchangeMeetingId = 100,
				ExchangeId = 1,
				ProposerId = userId,
				ExchangeMode = ExchangeMode.CUSTOM,
				CustomLocation = new Point(0, 0) { SRID = 4326 },
				MeetingStatus = ExchangeMeetingStatus.PROPOSAL
			});

		var result = await controller.GetExchangeMeeting(100);

		Assert.IsType<OkObjectResult>(result);
	}

	[Fact]
	public async Task GetExchangeMeeting_MeetingNotFound_ReturnsNotFound()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);

		_meetingService
			.Setup(s => s.GetExchangeMeeting(9999))
			.ReturnsAsync((ExchangeMeeting?)null);

		var result = await controller.GetExchangeMeeting(9999);

		Assert.IsType<NotFoundObjectResult>(result);
	}

	[Fact]
	public async Task GetExchangeMeeting_UserNotAuthenticated_ReturnsUnauthorized()
	{
		var controller = new ExchangeMeetingController(_meetingService.Object, _db, _exchangeService.Object);
		controller.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
		};

		var result = await controller.GetExchangeMeeting(100);

		Assert.IsType<UnauthorizedResult>(result);
	}

	// === GET MEETINGS BY USER TESTS ===

	[Fact]
	public async Task GetMeetingsByUserId_MeetingsExist_ReturnsOk()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);

		_meetingService
			.Setup(s => s.GetMeetingsByUserId(userId))
			.ReturnsAsync(new List<ExchangeMeeting>
			{
				new() { ExchangeMeetingId = 101, ExchangeId = 1, ProposerId = userId, ExchangeMode = ExchangeMode.CUSTOM, CustomLocation = new Point(0, 0) { SRID = 4326 } },
				new() { ExchangeMeetingId = 102, ExchangeId = 2, ProposerId = userId, ExchangeMode = ExchangeMode.BOOKSPOT, BookspotId = 5, CustomLocation = new Point(0, 0) { SRID = 4326 } }
			});

		var result = await controller.GetMeetingsByUserId(userId);

		Assert.IsType<OkObjectResult>(result);
	}

	[Fact]
	public async Task GetMeetingsByUserId_NoMeetings_ReturnsNotFound()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);

		_meetingService
			.Setup(s => s.GetMeetingsByUserId(userId))
			.ReturnsAsync(new List<ExchangeMeeting>());

		var result = await controller.GetMeetingsByUserId(userId);

		Assert.IsType<NotFoundObjectResult>(result);
	}

	[Fact]
	public async Task GetMeetingsByUserId_UserNotAuthenticated_ReturnsUnauthorized()
	{
		var controller = new ExchangeMeetingController(_meetingService.Object, _db, _exchangeService.Object);
		controller.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
		};

		var result = await controller.GetMeetingsByUserId(Guid.NewGuid());

		Assert.IsType<UnauthorizedResult>(result);
	}

	// === GET ALL MEETINGS TESTS ===

	[Fact]
	public async Task GetAllExchangeMeetings_MeetingsExist_ReturnsOk()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);

		_meetingService
			.Setup(s => s.GetAllExchangeMeetings())
			.ReturnsAsync(new List<ExchangeMeeting>
			{
				new() { ExchangeMeetingId = 103, ExchangeId = 3, ProposerId = userId, ExchangeMode = ExchangeMode.CUSTOM, CustomLocation = new Point(0, 0) { SRID = 4326 } },
				new() { ExchangeMeetingId = 104, ExchangeId = 4, ProposerId = Guid.NewGuid(), ExchangeMode = ExchangeMode.BOOKSPOT, BookspotId = 10, CustomLocation = new Point(0, 0) { SRID = 4326 } }
			});

		var result = await controller.GetAllExchangeMeetings();

		Assert.IsType<OkObjectResult>(result);
	}

	[Fact]
	public async Task GetAllExchangeMeetings_NoMeetings_ReturnsNotFound()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);

		_meetingService
			.Setup(s => s.GetAllExchangeMeetings())
			.ReturnsAsync(new List<ExchangeMeeting>());

		var result = await controller.GetAllExchangeMeetings();

		Assert.IsType<NotFoundObjectResult>(result);
	}

	// === CREATE MEETING TESTS ===

	[Fact]
	public async Task CreateExchangeMeeting_ValidDto_ReturnsCreatedAtAction()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);
		var dto = new ExchangeMeetingDto(null, 200, ExchangeMode.CUSTOM, null, new[] { 1.0, 1.0 }, null, null, null, null, null, null, null, null);

		_exchangeService
			.Setup(s => s.GetExchangeWithMatch(200))
			.ReturnsAsync(new Api.Models.Entities.Exchange
			{
				ExchangeId = 200,
				ChatId = 1,
				MatchId = 1,
				Status = ExchangeStatus.NEGOTIATING,
				Match = new Api.Models.Entities.Match
				{
					Id = 1,
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

		_meetingService
			.Setup(s => s.CreateExchangeMeeting(It.IsAny<int>(), It.IsAny<ExchangeMode>(), It.IsAny<Guid>(), It.IsAny<int?>(), It.IsAny<DateTime?>(), It.IsAny<Point>()))
			.ReturnsAsync(new ExchangeMeeting
			{
				ExchangeMeetingId = 200,
				ExchangeId = 200,
				ProposerId = userId,
				ExchangeMode = ExchangeMode.CUSTOM,
				CustomLocation = new Point(1, 1) { SRID = 4326 },
				MeetingStatus = ExchangeMeetingStatus.PROPOSAL
			});

		var result = await controller.CreateExchangeMeeting(dto);

		Assert.IsType<CreatedAtActionResult>(result);
	}

	[Fact]
	public async Task CreateExchangeMeeting_ExchangeNotFound_ReturnsNotFound()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);
		var dto = new ExchangeMeetingDto(null, 9999, ExchangeMode.CUSTOM, null, new[] { 1.0, 1.0 }, null, null, null, null, null, null, null, null);

		_exchangeService
			.Setup(s => s.GetExchangeWithMatch(9999))
			.ReturnsAsync((Api.Models.Entities.Exchange?)null);

		var result = await controller.CreateExchangeMeeting(dto);

		Assert.IsType<NotFoundObjectResult>(result);
	}

	[Fact]
	public async Task CreateExchangeMeeting_UserNotAuthenticated_ReturnsUnauthorized()
	{
		var controller = new ExchangeMeetingController(_meetingService.Object, _db, _exchangeService.Object);
		controller.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
		};

		var dto = new ExchangeMeetingDto(null, 200, ExchangeMode.CUSTOM, null, new[] { 1.0, 1.0 }, null, null, null, null, null, null, null, null);
		var result = await controller.CreateExchangeMeeting(dto);

		Assert.IsType<UnauthorizedResult>(result);
	}

	// === UPDATE MEETING TESTS ===

	[Fact]
	public async Task UpdateExchangeMeeting_ValidUpdate_ReturnsOk()
	{
		var userId = Guid.NewGuid();
		var otherUserId = Guid.NewGuid();
		var controller = CreateController(userId);
		var dto = new UpdateExchangeMeetingDto(ExchangeMode.BOOKSPOT, 5, null, null, null, null, null);

		_meetingService
			.Setup(s => s.GetExchangeMeetingWithRelations(300))
			.ReturnsAsync(new ExchangeMeeting
			{
				ExchangeMeetingId = 300,
				ExchangeId = 50,
				ProposerId = userId,
				ExchangeMode = ExchangeMode.CUSTOM,
				CustomLocation = new Point(0, 0) { SRID = 4326 },
				MeetingStatus = ExchangeMeetingStatus.PROPOSAL,
				Exchange = new Api.Models.Entities.Exchange
				{
					ExchangeId = 50,
					Status = ExchangeStatus.NEGOTIATING,
					Match = new Api.Models.Entities.Match
					{
						Id = 1,
						User1Id = userId,
						User2Id = otherUserId,
						Status = MatchStatus.NEW,
						CreatedAt = DateTime.UtcNow
					},
					CreatedAt = DateTime.UtcNow,
					UpdatedAt = DateTime.UtcNow
				}
			});

		_meetingService
			.Setup(s => s.UpdateExchangeMeeting(300, dto))
			.ReturnsAsync(new ExchangeMeeting
			{
				ExchangeMeetingId = 300,
				ExchangeId = 50,
				ProposerId = userId,
				ExchangeMode = ExchangeMode.BOOKSPOT,
				BookspotId = 5,
				CustomLocation = new Point(0, 0) { SRID = 4326 },
				MeetingStatus = ExchangeMeetingStatus.PROPOSAL
			});

		var result = await controller.UpdateExchangeMeeting(300, dto);

		Assert.IsType<OkObjectResult>(result);
	}

	[Fact]
	public async Task UpdateExchangeMeeting_MeetingNotFound_ReturnsNotFound()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);
		var dto = new UpdateExchangeMeetingDto(ExchangeMode.BOOKSPOT, 5, null, null, null, null, null);

		_meetingService
			.Setup(s => s.GetExchangeMeetingWithRelations(9999))
			.ReturnsAsync((ExchangeMeeting?)null);

		var result = await controller.UpdateExchangeMeeting(9999, dto);

		Assert.IsType<NotFoundObjectResult>(result);
	}

	[Fact]
	public async Task UpdateExchangeMeeting_UserNotAuthenticated_ReturnsUnauthorized()
	{
		var controller = new ExchangeMeetingController(_meetingService.Object, _db, _exchangeService.Object);
		controller.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
		};

		var dto = new UpdateExchangeMeetingDto(ExchangeMode.BOOKSPOT, 5, null, null, null, null, null);
		var result = await controller.UpdateExchangeMeeting(300, dto);

		Assert.IsType<UnauthorizedResult>(result);
	}

	// === DELETE MEETING TESTS ===

	[Fact]
	public async Task DeleteExchangeMeeting_MeetingExists_ReturnsNoContent()
	{
		var userId = Guid.NewGuid();
		var otherUserId = Guid.NewGuid();
		var controller = CreateController(userId);

		_meetingService
			.Setup(s => s.GetExchangeMeetingWithRelations(400))
			.ReturnsAsync(new ExchangeMeeting
			{
				ExchangeMeetingId = 400,
				ExchangeId = 60,
				ProposerId = userId,
				ExchangeMode = ExchangeMode.CUSTOM,
				CustomLocation = new Point(0, 0) { SRID = 4326 },
				Exchange = new Api.Models.Entities.Exchange
				{
					ExchangeId = 60,
					Status = ExchangeStatus.NEGOTIATING,
					Match = new Api.Models.Entities.Match
					{
						Id = 1,
						User1Id = userId,
						User2Id = otherUserId,
						Status = MatchStatus.NEW,
						CreatedAt = DateTime.UtcNow
					},
					CreatedAt = DateTime.UtcNow,
					UpdatedAt = DateTime.UtcNow
				}
			});

		_meetingService
			.Setup(s => s.DeleteExchangeMeeting(400))
			.ReturnsAsync(true);

		var result = await controller.DeleteExchangeMeeting(400);

		Assert.IsType<NoContentResult>(result);
	}

	[Fact]
	public async Task DeleteExchangeMeeting_MeetingNotFound_ReturnsNotFound()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);

		_meetingService
			.Setup(s => s.GetExchangeMeetingWithRelations(9999))
			.ReturnsAsync((ExchangeMeeting?)null);

		var result = await controller.DeleteExchangeMeeting(9999);

		Assert.IsType<NotFoundObjectResult>(result);
	}

	[Fact]
	public async Task DeleteExchangeMeeting_UserNotAuthenticated_ReturnsUnauthorized()
	{
		var controller = new ExchangeMeetingController(_meetingService.Object, _db, _exchangeService.Object);
		controller.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
		};

		var result = await controller.DeleteExchangeMeeting(400);

		Assert.IsType<UnauthorizedResult>(result);
	}

	[Fact]
	public async Task CreateExchangeMeeting_UserNotInExchange_ReturnsForbid()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);
		var dto = new ExchangeMeetingDto(null, 201, ExchangeMode.CUSTOM, null, new[] { 1.0, 1.0 }, null, null, null, null, null, null, null, null);

		_exchangeService
			.Setup(s => s.GetExchangeWithMatch(201))
			.ReturnsAsync(new Api.Models.Entities.Exchange
			{
				ExchangeId = 201,
				ChatId = 1,
				MatchId = 1,
				Status = ExchangeStatus.NEGOTIATING,
				Match = new Api.Models.Entities.Match
				{
					Id = 1,
					User1Id = Guid.NewGuid(),
					User2Id = Guid.NewGuid(),
					Book1Id = 1,
					Book2Id = 2,
					Status = MatchStatus.NEW,
					CreatedAt = DateTime.UtcNow
				},
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			});

		var result = await controller.CreateExchangeMeeting(dto);

		Assert.IsType<ForbidResult>(result);
	}

	[Fact]
	public async Task UpdateExchangeMeeting_UserNotInExchange_ReturnsForbid()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);
		var dto = new UpdateExchangeMeetingDto(ExchangeMode.BOOKSPOT, 5, null, null, null, null, null);

		_meetingService
			.Setup(s => s.GetExchangeMeetingWithRelations(301))
			.ReturnsAsync(new ExchangeMeeting
			{
				ExchangeMeetingId = 301,
				ExchangeId = 51,
				ProposerId = Guid.NewGuid(),
				ExchangeMode = ExchangeMode.CUSTOM,
				CustomLocation = new Point(0, 0) { SRID = 4326 },
				MeetingStatus = ExchangeMeetingStatus.PROPOSAL,
				Exchange = new Api.Models.Entities.Exchange
				{
					ExchangeId = 51,
					Status = ExchangeStatus.NEGOTIATING,
					Match = new Api.Models.Entities.Match
					{
						Id = 1,
						User1Id = Guid.NewGuid(),
						User2Id = Guid.NewGuid(),
						Status = MatchStatus.NEW,
						CreatedAt = DateTime.UtcNow
					},
					CreatedAt = DateTime.UtcNow,
					UpdatedAt = DateTime.UtcNow
				}
			});

		var result = await controller.UpdateExchangeMeeting(301, dto);

		Assert.IsType<ForbidResult>(result);
	}

	[Fact]
	public async Task UpdateExchangeMeeting_MeetingStatusChanged_ReturnsForbid()
	{
		var proposerId = Guid.NewGuid();
		var otherUserId = Guid.NewGuid();
		var controller = CreateController(proposerId);
		var dto = new UpdateExchangeMeetingDto(null, null, null, null, ExchangeMeetingStatus.ACCEPTED, null, null);

		_meetingService
			.Setup(s => s.GetExchangeMeetingWithRelations(303))
			.ReturnsAsync(new ExchangeMeeting
			{
				ExchangeMeetingId = 303,
				ExchangeId = 53,
				ProposerId = proposerId,
				ExchangeMode = ExchangeMode.CUSTOM,
				CustomLocation = new Point(0, 0) { SRID = 4326 },
				MeetingStatus = ExchangeMeetingStatus.PROPOSAL,
				Exchange = new Api.Models.Entities.Exchange
				{
					ExchangeId = 53,
					Status = ExchangeStatus.NEGOTIATING,
					Match = new Api.Models.Entities.Match
					{
						Id = 1,
						User1Id = proposerId,
						User2Id = otherUserId,
						Status = MatchStatus.NEW,
						CreatedAt = DateTime.UtcNow
					},
					CreatedAt = DateTime.UtcNow,
					UpdatedAt = DateTime.UtcNow
				}
			});

		var result = await controller.UpdateExchangeMeeting(303, dto);

		Assert.IsType<ForbidResult>(result);
	}

	[Fact]
	public async Task UpdateExchangeMeeting_ServiceThrowsInvalidOperation_ReturnsBadRequest()
	{
		var userId = Guid.NewGuid();
		var otherUserId = Guid.NewGuid();
		var controller = CreateController(userId);
		var dto = new UpdateExchangeMeetingDto(ExchangeMode.BOOKSPOT, 5, null, null, null, null, null);

		_meetingService
			.Setup(s => s.GetExchangeMeetingWithRelations(304))
			.ReturnsAsync(new ExchangeMeeting
			{
				ExchangeMeetingId = 304,
				ExchangeId = 54,
				ProposerId = userId,
				ExchangeMode = ExchangeMode.CUSTOM,
				CustomLocation = new Point(0, 0) { SRID = 4326 },
				MeetingStatus = ExchangeMeetingStatus.PROPOSAL,
				Exchange = new Api.Models.Entities.Exchange
				{
					ExchangeId = 54,
					Status = ExchangeStatus.NEGOTIATING,
					Match = new Api.Models.Entities.Match
					{
						Id = 1,
						User1Id = userId,
						User2Id = otherUserId,
						Status = MatchStatus.NEW,
						CreatedAt = DateTime.UtcNow
					},
					CreatedAt = DateTime.UtcNow,
					UpdatedAt = DateTime.UtcNow
				}
			});

		_meetingService
			.Setup(s => s.UpdateExchangeMeeting(304, dto))
			.ThrowsAsync(new InvalidOperationException("invalid update"));

		var result = await controller.UpdateExchangeMeeting(304, dto);

		Assert.IsType<BadRequestObjectResult>(result);
	}

	[Fact]
	public async Task DeleteExchangeMeeting_UserNotInExchange_ReturnsForbid()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);

		_meetingService
			.Setup(s => s.GetExchangeMeetingWithRelations(401))
			.ReturnsAsync(new ExchangeMeeting
			{
				ExchangeMeetingId = 401,
				ExchangeId = 61,
				ProposerId = Guid.NewGuid(),
				ExchangeMode = ExchangeMode.CUSTOM,
				CustomLocation = new Point(0, 0) { SRID = 4326 },
				Exchange = new Api.Models.Entities.Exchange
				{
					ExchangeId = 61,
					Status = ExchangeStatus.NEGOTIATING,
					Match = new Api.Models.Entities.Match
					{
						Id = 1,
						User1Id = Guid.NewGuid(),
						User2Id = Guid.NewGuid(),
						Status = MatchStatus.NEW,
						CreatedAt = DateTime.UtcNow
					},
					CreatedAt = DateTime.UtcNow,
					UpdatedAt = DateTime.UtcNow
				}
			});

		var result = await controller.DeleteExchangeMeeting(401);

		Assert.IsType<ForbidResult>(result);
	}

	[Fact]
	public async Task DeleteExchangeMeeting_ServiceReturnsFalse_ReturnsBadRequest()
	{
		var userId = Guid.NewGuid();
		var otherUserId = Guid.NewGuid();
		var controller = CreateController(userId);

		_meetingService
			.Setup(s => s.GetExchangeMeetingWithRelations(402))
			.ReturnsAsync(new ExchangeMeeting
			{
				ExchangeMeetingId = 402,
				ExchangeId = 62,
				ProposerId = userId,
				ExchangeMode = ExchangeMode.CUSTOM,
				CustomLocation = new Point(0, 0) { SRID = 4326 },
				Exchange = new Api.Models.Entities.Exchange
				{
					ExchangeId = 62,
					Status = ExchangeStatus.NEGOTIATING,
					Match = new Api.Models.Entities.Match
					{
						Id = 1,
						User1Id = userId,
						User2Id = otherUserId,
						Status = MatchStatus.NEW,
						CreatedAt = DateTime.UtcNow
					},
					CreatedAt = DateTime.UtcNow,
					UpdatedAt = DateTime.UtcNow
				}
			});

		_meetingService
			.Setup(s => s.DeleteExchangeMeeting(402))
			.ReturnsAsync(false);

		var result = await controller.DeleteExchangeMeeting(402);

		Assert.IsType<BadRequestObjectResult>(result);
	}

	[Fact]
	public async Task CompleteExchange_MeetingNotFound_ReturnsNotFound()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);

		_meetingService
			.Setup(s => s.GetExchangeMeeting(700))
			.ReturnsAsync((ExchangeMeeting?)null);

		var result = await controller.CompleteExchange(700);

		Assert.IsType<NotFoundObjectResult>(result);
	}

	[Fact]
	public async Task AcceptExchangeMeeting_UserNotAuthenticated_ReturnsUnauthorized()
	{
		var controller = new ExchangeMeetingController(_meetingService.Object, _db, _exchangeService.Object);
		controller.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
		};

		var result = await controller.AcceptExchangeMeeting(500);

		Assert.IsType<UnauthorizedResult>(result);
	}

	[Fact]
	public async Task AcceptExchangeMeeting_ServiceThrowsInvalidOperation_ReturnsBadRequest()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);

		_meetingService
			.Setup(s => s.UpdateExchangeMeeting(501, It.IsAny<UpdateExchangeMeetingDto>()))
			.ThrowsAsync(new InvalidOperationException("cannot accept"));

		var result = await controller.AcceptExchangeMeeting(501);

		Assert.IsType<BadRequestObjectResult>(result);
	}

	[Fact]
	public async Task AcceptExchangeMeeting_ValidRequest_ReturnsOk()
	{
		var userId = Guid.NewGuid();
		var controller = CreateController(userId);

		_meetingService
			.Setup(s => s.UpdateExchangeMeeting(502, It.Is<UpdateExchangeMeetingDto>(d => d.MeetingStatus == ExchangeMeetingStatus.ACCEPTED)))
			.ReturnsAsync(new ExchangeMeeting
			{
				ExchangeMeetingId = 502,
				ExchangeId = 90,
				ProposerId = userId,
				ExchangeMode = ExchangeMode.CUSTOM,
				CustomLocation = new Point(0, 0) { SRID = 4326 },
				MeetingStatus = ExchangeMeetingStatus.ACCEPTED
			});

		var result = await controller.AcceptExchangeMeeting(502);

		Assert.IsType<OkObjectResult>(result);
	}
}
