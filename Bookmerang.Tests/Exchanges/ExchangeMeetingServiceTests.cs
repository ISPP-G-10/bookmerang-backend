using Bookmerang.Api.Data;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Implementation.ExchangeServices;
using Bookmerang.Api.Services.Interfaces.ExchangeInterfaces;
using Bookmerang.Api.Services.Interfaces.Inkdrops;
using Bookmerang.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;
using NetTopologySuite.Geometries;
using Xunit;

namespace Bookmerang.Tests.Exchanges;

public class ExchangeMeetingServiceTests : IAsyncLifetime
{
	private AppDbContext _db = null!;
	private Mock<IExchangeService> _exchangeService = null!;
	private Mock<IInkdropsService> _inkdropsService = null!;
	private ExchangeMeetingService _service = null!;

	public Task InitializeAsync()
	{
		_db = DbContextFactory.CreateInMemory();
		_exchangeService = new Mock<IExchangeService>();
		_inkdropsService = new Mock<IInkdropsService>();
		_inkdropsService
			.Setup(s => s.GrantExchangeInkdropsAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
			.Returns(Task.CompletedTask);
		_service = new ExchangeMeetingService(_db, _exchangeService.Object, _inkdropsService.Object);
		return Task.CompletedTask;
	}

	public async Task DisposeAsync()
	{
		await _db.Database.EnsureDeletedAsync();
		await _db.DisposeAsync();
	}

	[Fact]
	public async Task UpdateExchangeMeeting_BothCompletionFlagsTrue_SetsExchangeCompleted()
	{
		var user1Id = Guid.NewGuid();
		var user2Id = Guid.NewGuid();
		var match = new Api.Models.Entities.Match
		{
			Id = 30,
			User1Id = user1Id,
			User2Id = user2Id,
			Book1Id = 301,
			Book2Id = 302,
			Status = MatchStatus.NEW,
			CreatedAt = DateTime.UtcNow
		};

		_db.Matches.Add(match);
		_db.Books.AddRange(
			new Book { Id = 301, OwnerId = user1Id, Status = BookStatus.PUBLISHED },
			new Book { Id = 302, OwnerId = user2Id, Status = BookStatus.PUBLISHED }
		);

		var exchange = new Api.Models.Entities.Exchange
		{
			ExchangeId = 10,
			ChatId = 20,
			MatchId = 30,
			Match = match,
			Status = ExchangeStatus.NEGOTIATING,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};

		var meeting = new ExchangeMeeting
		{
			ExchangeMeetingId = 11,
			ExchangeId = 10,
			ExchangeMode = ExchangeMode.CUSTOM,
			CustomLocation = new Point(0, 0) { SRID = 4326 },
			ProposerId = Guid.NewGuid(),
			MeetingStatus = ExchangeMeetingStatus.PROPOSAL
		};

		_db.Exchanges.Add(exchange);
		_db.ExchangeMeetings.Add(meeting);
		await _db.SaveChangesAsync();

		_exchangeService
			.Setup(s => s.GetExchangeWithMatch(10))
			.ReturnsAsync(exchange);

		var dto = new UpdateExchangeMeetingDto(null, null, null, null, null, true, true);

		var updatedMeeting = await _service.UpdateExchangeMeeting(11, dto);

		Assert.True(updatedMeeting.MarkAsCompletedByUser1);
		Assert.True(updatedMeeting.MarkAsCompletedByUser2);
		Assert.Equal(ExchangeStatus.COMPLETED, exchange.Status);
	}

	[Fact]
	public async Task UpdateExchangeMeeting_AlreadyCompleted_DoesNotDuplicateInkdrops()
	{
		var user1Id = Guid.NewGuid();
		var user2Id = Guid.NewGuid();
		var match = new Api.Models.Entities.Match
		{
			Id = 32,
			User1Id = user1Id,
			User2Id = user2Id,
			Book1Id = 321,
			Book2Id = 322,
			Status = MatchStatus.NEW,
			CreatedAt = DateTime.UtcNow
		};

		_db.Matches.Add(match);
		_db.Books.AddRange(
			new Book { Id = 321, OwnerId = user1Id, Status = BookStatus.PUBLISHED },
			new Book { Id = 322, OwnerId = user2Id, Status = BookStatus.PUBLISHED }
		);

		var exchange = new Api.Models.Entities.Exchange
		{
			ExchangeId = 12,
			ChatId = 22,
			MatchId = 32,
			Match = match,
			Status = ExchangeStatus.COMPLETED,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};

		var meeting = new ExchangeMeeting
		{
			ExchangeMeetingId = 13,
			ExchangeId = 12,
			ExchangeMode = ExchangeMode.CUSTOM,
			CustomLocation = new Point(0, 0) { SRID = 4326 },
			ProposerId = Guid.NewGuid(),
			MeetingStatus = ExchangeMeetingStatus.PROPOSAL,
			MarkAsCompletedByUser1 = true,
			MarkAsCompletedByUser2 = true
		};

		_db.Exchanges.Add(exchange);
		_db.ExchangeMeetings.Add(meeting);
		await _db.SaveChangesAsync();

		_exchangeService
			.Setup(s => s.GetExchangeWithMatch(12))
			.ReturnsAsync(exchange);

		var dto = new UpdateExchangeMeetingDto(null, null, null, null, null, true, true);

		var updatedMeeting = await _service.UpdateExchangeMeeting(13, dto);

		_inkdropsService.Verify(s => s.GrantExchangeInkdropsAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
	}

	[Fact]
	public async Task CreateExchangeMeeting_ScheduledAtTooSoon_ThrowsArgumentException()
	{
		var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
			_service.CreateExchangeMeeting(
				exchangeId: 1,
				exchangeMode: ExchangeMode.CUSTOM,
				proposerId: Guid.NewGuid(),
				bookspotId: 100,
				scheduledAt: DateTime.UtcNow.AddMinutes(3),
				customLocation: new Point(0, 0) { SRID = 4326 }));

		Assert.Contains("La fecha del encuentro", ex.Message);
	}

	// GET TESTS
	[Fact]
	public async Task GetExchangeMeeting_MeetingExists_ReturnsMeeting()
	{
		var proposer = new BaseUser
		{
			Id = Guid.NewGuid(),
			SupabaseId = "sup-123",
			Email = "proposer@test.com",
			Username = "proposer",
			Name = "Proposer User",
			ProfilePhoto = string.Empty,
			UserType = BaseUserType.USER,
			Location = new Point(0, 0) { SRID = 4326 },
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};
		_db.Users.Add(proposer);
		_db.RegularUsers.Add(new User { Id = proposer.Id });

		var meeting = new ExchangeMeeting
		{
			ExchangeMeetingId = 100,
			ExchangeId = 1,
			ExchangeMode = ExchangeMode.CUSTOM,
			CustomLocation = new Point(5, 5) { SRID = 4326 },
			ProposerId = proposer.Id,
			ScheduledAt = DateTime.UtcNow.AddDays(1),
			MeetingStatus = ExchangeMeetingStatus.PROPOSAL
		};
		_db.ExchangeMeetings.Add(meeting);
		await _db.SaveChangesAsync();

		var result = await _service.GetExchangeMeeting(100);

		Assert.NotNull(result);
		Assert.Equal(100, result.ExchangeMeetingId);
		Assert.NotNull(result.Proposer);
	}

	[Fact]
	public async Task GetExchangeMeeting_MeetingDoesNotExist_ReturnsNull()
	{
		var result = await _service.GetExchangeMeeting(9999);

		Assert.Null(result);
	}

	[Fact]
	public async Task GetExchangeMeetingWithRelations_MeetingExists_ReturnsMeetingWithRelations()
	{
		var match = new Api.Models.Entities.Match
		{
			Id = 1,
			User1Id = Guid.NewGuid(),
			User2Id = Guid.NewGuid(),
			Book1Id = 1,
			Book2Id = 2,
			Status = MatchStatus.NEW,
			CreatedAt = DateTime.UtcNow
		};
		_db.Matches.Add(match);

		var exchange = new Api.Models.Entities.Exchange
		{
			ExchangeId = 200,
			ChatId = 1,
			MatchId = 1,
			Status = ExchangeStatus.NEGOTIATING,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};
		_db.Exchanges.Add(exchange);

		var meeting = new ExchangeMeeting
		{
			ExchangeMeetingId = 101,
			ExchangeId = 200,
			ExchangeMode = ExchangeMode.CUSTOM,
			CustomLocation = new Point(0, 0) { SRID = 4326 },
			ProposerId = Guid.NewGuid(),
			ScheduledAt = DateTime.UtcNow.AddDays(1),
			MeetingStatus = ExchangeMeetingStatus.PROPOSAL
		};
		_db.ExchangeMeetings.Add(meeting);
		await _db.SaveChangesAsync();

		var result = await _service.GetExchangeMeetingWithRelations(101);

		Assert.NotNull(result);
		Assert.NotNull(result.Exchange);
		Assert.NotNull(result.Exchange.Match);
	}

	[Fact]
	public async Task GetMeetingsByUserId_MultipleMeetings_ReturnsAll()
	{
		var proposerId = Guid.NewGuid();
		var proposer = new BaseUser
		{
			Id = proposerId,
			SupabaseId = "sup-456",
			Email = "user@test.com",
			Username = "testuser",
			Name = "Test User",
			ProfilePhoto = string.Empty,
			UserType = BaseUserType.USER,
			Location = new Point(0, 0) { SRID = 4326 },
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};
		_db.Users.Add(proposer);
		_db.RegularUsers.Add(new User { Id = proposer.Id });

		_db.ExchangeMeetings.AddRange(
			new ExchangeMeeting
			{
				ExchangeMeetingId = 102,
				ExchangeId = 2,
				ExchangeMode = ExchangeMode.CUSTOM,
				CustomLocation = new Point(0, 0) { SRID = 4326 },
				ProposerId = proposerId,
				MeetingStatus = ExchangeMeetingStatus.PROPOSAL
			},
			new ExchangeMeeting
			{
				ExchangeMeetingId = 103,
				ExchangeId = 3,
				ExchangeMode = ExchangeMode.BOOKSPOT,
				BookspotId = 5,
				CustomLocation = new Point(0, 0) { SRID = 4326 },
				ProposerId = proposerId,
				MeetingStatus = ExchangeMeetingStatus.ACCEPTED,
			}
		);
		await _db.SaveChangesAsync();

		var result = await _service.GetMeetingsByUserId(proposerId);

		Assert.NotEmpty(result);
		Assert.True(result.Count >= 2);
		Assert.All(result, m => Assert.Equal(proposerId, m.ProposerId));
	}

	[Fact]
	public async Task GetMeetingsByUserId_NoMeetings_ReturnsEmpty()
	{
		var result = await _service.GetMeetingsByUserId(Guid.NewGuid());

		Assert.Empty(result);
	}

	[Fact]
	public async Task GetAllExchangeMeetings_MultipleMeetings_ReturnsAll()
	{
		var proposerId = Guid.NewGuid();
		var proposer = new BaseUser
		{
			Id = proposerId,
			SupabaseId = "sup-789",
			Email = "user2@test.com",
			Username = "testuser2",
			Name = "Test User 2",
			ProfilePhoto = string.Empty,
			UserType = BaseUserType.USER,
			Location = new Point(0, 0) { SRID = 4326 },
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};
		_db.Users.Add(proposer);
		_db.RegularUsers.Add(new User { Id = proposer.Id });

		_db.ExchangeMeetings.AddRange(
			new ExchangeMeeting
			{
				ExchangeMeetingId = 104,
				ExchangeId = 4,
				ExchangeMode = ExchangeMode.CUSTOM,
				CustomLocation = new Point(0, 0) { SRID = 4326 },
				ProposerId = proposerId,
				MeetingStatus = ExchangeMeetingStatus.PROPOSAL
			},
			new ExchangeMeeting
			{
				ExchangeMeetingId = 105,
				ExchangeId = 5,
				ExchangeMode = ExchangeMode.BOOKSPOT,
				BookspotId = 10,
				CustomLocation = new Point(0, 0) { SRID = 4326 },
				ProposerId = proposerId,
				MeetingStatus = ExchangeMeetingStatus.ACCEPTED
			}
		);
		await _db.SaveChangesAsync();

		var result = await _service.GetAllExchangeMeetings();

		Assert.NotEmpty(result);
		Assert.True(result.Count >= 2);
	}

	[Fact]
	public async Task GetMeetingByExchangeId_MeetingExists_ReturnsMeeting()
	{
		var meeting = new ExchangeMeeting
		{
			ExchangeMeetingId = 106,
			ExchangeId = 50,
			ExchangeMode = ExchangeMode.CUSTOM,
			CustomLocation = new Point(0, 0) { SRID = 4326 },
			ProposerId = Guid.NewGuid(),
			MeetingStatus = ExchangeMeetingStatus.PROPOSAL
		};
		_db.ExchangeMeetings.Add(meeting);
		await _db.SaveChangesAsync();

		var result = await _service.GetMeetingByExchangeId(50);

		Assert.NotNull(result);
		Assert.Equal(50, result.ExchangeId);
	}

	[Fact]
	public async Task GetMeetingByExchangeId_NoMeetingForExchange_ReturnsNull()
	{
		var result = await _service.GetMeetingByExchangeId(9999);

		Assert.Null(result);
	}

	// CREATE TESTS
	[Fact]
	public async Task CreateExchangeMeeting_ValidCustomLocation_CreatesSuccessfully()
	{
		var proposerId = Guid.NewGuid();
		var location = new Point(2.5, 3.5) { SRID = 4326 };

		var result = await _service.CreateExchangeMeeting(
			exchangeId: 60,
			exchangeMode: ExchangeMode.CUSTOM,
			proposerId: proposerId,
			bookspotId: null,
			scheduledAt: DateTime.UtcNow.AddDays(2),
			customLocation: location);

		Assert.NotNull(result);
		Assert.Equal(60, result.ExchangeId);
		Assert.Equal(ExchangeMode.CUSTOM, result.ExchangeMode);
		Assert.Null(result.BookspotId);
		Assert.Equal(proposerId, result.ProposerId);
	}

	[Fact]
	public async Task CreateExchangeMeeting_BookspotModeWithoutBookspotId_ThrowsArgumentException()
	{
		var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
			_service.CreateExchangeMeeting(
				exchangeId: 61,
				exchangeMode: ExchangeMode.BOOKSPOT,
				proposerId: Guid.NewGuid(),
				bookspotId: null,
				scheduledAt: DateTime.UtcNow.AddDays(1),
				customLocation: new Point(0, 0) { SRID = 4326 }));

		Assert.Contains("Se debe indicar el bookspot", ex.Message);
	}

	[Fact]
	public async Task CreateExchangeMeeting_ValidBookspotMode_CreatesSuccessfully()
	{
		var proposerId = Guid.NewGuid();

		var result = await _service.CreateExchangeMeeting(
			exchangeId: 62,
			exchangeMode: ExchangeMode.BOOKSPOT,
			proposerId: proposerId,
			bookspotId: 20,
			scheduledAt: DateTime.UtcNow.AddDays(1),
			customLocation: new Point(0, 0) { SRID = 4326 });

		Assert.NotNull(result);
		Assert.Equal(ExchangeMode.BOOKSPOT, result.ExchangeMode);
		Assert.Equal(20, result.BookspotId);
	}

	[Fact]
	public async Task CreateExchangeMeeting_PastDateTime_ThrowsArgumentException()
	{
		var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
			_service.CreateExchangeMeeting(
				exchangeId: 63,
				exchangeMode: ExchangeMode.CUSTOM,
				proposerId: Guid.NewGuid(),
				bookspotId: null,
				scheduledAt: DateTime.UtcNow.AddMinutes(-10),
				customLocation: new Point(0, 0) { SRID = 4326 }));

		Assert.Contains("La fecha del encuentro", ex.Message);
	}

	// UPDATE TESTS
	[Fact]
	public async Task UpdateExchangeMeeting_AllNull_ThrowsInvalidOperationException()
	{
		var exchange = new Api.Models.Entities.Exchange
		{
			ExchangeId = 70,
			ChatId = 7,
			MatchId = 7,
			Status = ExchangeStatus.NEGOTIATING,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};

		var meeting = new ExchangeMeeting
		{
			ExchangeMeetingId = 110,
			ExchangeId = 70,
			ExchangeMode = ExchangeMode.CUSTOM,
			CustomLocation = new Point(0, 0) { SRID = 4326 },
			ProposerId = Guid.NewGuid(),
			MeetingStatus = ExchangeMeetingStatus.PROPOSAL
		};

		_db.Exchanges.Add(exchange);
		_db.ExchangeMeetings.Add(meeting);
		await _db.SaveChangesAsync();

		_exchangeService
			.Setup(s => s.GetExchangeWithMatch(70))
			.ReturnsAsync(exchange);

		var dto = new UpdateExchangeMeetingDto(null, null, null, null, null, null, null);

		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			_service.UpdateExchangeMeeting(110, dto));

		Assert.Contains("Al menos un parámetro", ex.Message);
	}

	[Fact]
	public async Task UpdateExchangeMeeting_ScheduledAtTooSoon_ThrowsArgumentException()
	{
		var exchange = new Api.Models.Entities.Exchange
		{
			ExchangeId = 71,
			ChatId = 8,
			MatchId = 8,
			Status = ExchangeStatus.NEGOTIATING,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};

		var meeting = new ExchangeMeeting
		{
			ExchangeMeetingId = 111,
			ExchangeId = 71,
			ExchangeMode = ExchangeMode.CUSTOM,
			CustomLocation = new Point(0, 0) { SRID = 4326 },
			ProposerId = Guid.NewGuid(),
			MeetingStatus = ExchangeMeetingStatus.PROPOSAL,
			ScheduledAt = DateTime.UtcNow.AddDays(1)
		};

		_db.Exchanges.Add(exchange);
		_db.ExchangeMeetings.Add(meeting);
		await _db.SaveChangesAsync();

		_exchangeService
			.Setup(s => s.GetExchangeWithMatch(71))
			.ReturnsAsync(exchange);

		var dto = new UpdateExchangeMeetingDto(null, null, null, DateTime.UtcNow.AddMinutes(2), null, null, null);

		var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
			_service.UpdateExchangeMeeting(111, dto));

		Assert.Contains("La fecha del encuentro", ex.Message);
	}

	[Fact]
	public async Task UpdateExchangeMeeting_BookspotModeWithoutBookspotId_ThrowsArgumentException()
	{
		var exchange = new Api.Models.Entities.Exchange
		{
			ExchangeId = 72,
			ChatId = 9,
			MatchId = 9,
			Status = ExchangeStatus.NEGOTIATING,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};

		var meeting = new ExchangeMeeting
		{
			ExchangeMeetingId = 112,
			ExchangeId = 72,
			ExchangeMode = ExchangeMode.CUSTOM,
			CustomLocation = new Point(0, 0) { SRID = 4326 },
			ProposerId = Guid.NewGuid(),
			MeetingStatus = ExchangeMeetingStatus.PROPOSAL
		};

		_db.Exchanges.Add(exchange);
		_db.ExchangeMeetings.Add(meeting);
		await _db.SaveChangesAsync();

		_exchangeService
			.Setup(s => s.GetExchangeWithMatch(72))
			.ReturnsAsync(exchange);

		var dto = new UpdateExchangeMeetingDto(ExchangeMode.BOOKSPOT, null, null, null, null, null, null);

		var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
			_service.UpdateExchangeMeeting(112, dto));

		Assert.Contains("Se debe indicar el bookspot", ex.Message);
	}

	[Fact]
	public async Task UpdateExchangeMeeting_UpdateMode_UpdatesSuccessfully()
	{
		var exchange = new Api.Models.Entities.Exchange
		{
			ExchangeId = 73,
			ChatId = 10,
			MatchId = 10,
			Status = ExchangeStatus.NEGOTIATING,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};

		var meeting = new ExchangeMeeting
		{
			ExchangeMeetingId = 113,
			ExchangeId = 73,
			ExchangeMode = ExchangeMode.CUSTOM,
			CustomLocation = new Point(0, 0) { SRID = 4326 },
			ProposerId = Guid.NewGuid(),
			MeetingStatus = ExchangeMeetingStatus.PROPOSAL,
			ScheduledAt = DateTime.UtcNow.AddDays(1)
		};

		_db.Exchanges.Add(exchange);
		_db.ExchangeMeetings.Add(meeting);
		await _db.SaveChangesAsync();

		_exchangeService
			.Setup(s => s.GetExchangeWithMatch(73))
			.ReturnsAsync(exchange);

		var dto = new UpdateExchangeMeetingDto(ExchangeMode.BOOKSPOT, 15, null, null, ExchangeMeetingStatus.ACCEPTED, null, null);

		var result = await _service.UpdateExchangeMeeting(113, dto);

		Assert.Equal(ExchangeMode.BOOKSPOT, result.ExchangeMode);
		Assert.Equal(15, result.BookspotId);
		Assert.Equal(ExchangeMeetingStatus.ACCEPTED, result.MeetingStatus);
	}

	[Fact]
	public async Task UpdateExchangeMeeting_MeetingDoesNotExist_ThrowsInvalidOperationException()
	{
		_exchangeService
			.Setup(s => s.GetExchangeWithMatch(It.IsAny<int>()))
			.ReturnsAsync((Api.Models.Entities.Exchange?)null);

		var dto = new UpdateExchangeMeetingDto(null, null, null, null, null, null, null);

		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			_service.UpdateExchangeMeeting(9999, dto));

		Assert.Contains("Meeting con id", ex.Message);
	}

	[Fact]
	public async Task UpdateExchangeMeeting_UpdateCustomLocation_UpdatesSuccessfully()
	{
		var exchange = new Api.Models.Entities.Exchange
		{
			ExchangeId = 74,
			ChatId = 11,
			MatchId = 11,
			Status = ExchangeStatus.NEGOTIATING,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};

		var meeting = new ExchangeMeeting
		{
			ExchangeMeetingId = 114,
			ExchangeId = 74,
			ExchangeMode = ExchangeMode.CUSTOM,
			CustomLocation = new Point(0, 0) { SRID = 4326 },
			ProposerId = Guid.NewGuid(),
			MeetingStatus = ExchangeMeetingStatus.PROPOSAL
		};

		_db.Exchanges.Add(exchange);
		_db.ExchangeMeetings.Add(meeting);
		await _db.SaveChangesAsync();

		_exchangeService
			.Setup(s => s.GetExchangeWithMatch(74))
			.ReturnsAsync(exchange);

		var dto = new UpdateExchangeMeetingDto(null, null, new[] { 5.5, 6.5 }, null, null, null, null);

		var result = await _service.UpdateExchangeMeeting(114, dto);

		Assert.NotNull(result.CustomLocation);
		Assert.Equal(5.5, result.CustomLocation.X);
		Assert.Equal(6.5, result.CustomLocation.Y);
	}

	[Fact]
	public async Task UpdateExchangeMeeting_MarkBothCompleted_SetsExchangeCompleted()
	{
		var user1Id = Guid.NewGuid();
		var user2Id = Guid.NewGuid();
		var match = new Api.Models.Entities.Match
		{
			Id = 12,
			User1Id = user1Id,
			User2Id = user2Id,
			Book1Id = 1201,
			Book2Id = 1202,
			Status = MatchStatus.NEW,
			CreatedAt = DateTime.UtcNow
		};

		_db.Matches.Add(match);
		_db.Books.AddRange(
			new Book { Id = 1201, OwnerId = user1Id, Status = BookStatus.PUBLISHED },
			new Book { Id = 1202, OwnerId = user2Id, Status = BookStatus.PUBLISHED }
		);

		var exchange = new Api.Models.Entities.Exchange
		{
			ExchangeId = 75,
			ChatId = 12,
			MatchId = 12,
			Match = match,
			Status = ExchangeStatus.ACCEPTED,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};

		var meeting = new ExchangeMeeting
		{
			ExchangeMeetingId = 115,
			ExchangeId = 75,
			ExchangeMode = ExchangeMode.CUSTOM,
			CustomLocation = new Point(0, 0) { SRID = 4326 },
			ProposerId = Guid.NewGuid(),
			MeetingStatus = ExchangeMeetingStatus.ACCEPTED,
			MarkAsCompletedByUser1 = false,
			MarkAsCompletedByUser2 = false
		};

		_db.Exchanges.Add(exchange);
		_db.ExchangeMeetings.Add(meeting);
		await _db.SaveChangesAsync();

		_exchangeService
			.Setup(s => s.GetExchangeWithMatch(75))
			.ReturnsAsync(exchange);

		var dto = new UpdateExchangeMeetingDto(null, null, null, null, null, true, true);

		var result = await _service.UpdateExchangeMeeting(115, dto);

		Assert.True(result.MarkAsCompletedByUser1);
		Assert.True(result.MarkAsCompletedByUser2);
		Assert.Equal(ExchangeStatus.COMPLETED, exchange.Status);
	}

	[Fact]
	public async Task UpdateExchangeMeeting_PartialCompletion_DoesNotSetCompleted()
	{
		var exchange = new Api.Models.Entities.Exchange
		{
			ExchangeId = 76,
			ChatId = 13,
			MatchId = 13,
			Status = ExchangeStatus.ACCEPTED,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow
		};

		var meeting = new ExchangeMeeting
		{
			ExchangeMeetingId = 116,
			ExchangeId = 76,
			ExchangeMode = ExchangeMode.CUSTOM,
			CustomLocation = new Point(0, 0) { SRID = 4326 },
			ProposerId = Guid.NewGuid(),
			MeetingStatus = ExchangeMeetingStatus.ACCEPTED,
			MarkAsCompletedByUser1 = false,
			MarkAsCompletedByUser2 = false
		};

		_db.Exchanges.Add(exchange);
		_db.ExchangeMeetings.Add(meeting);
		await _db.SaveChangesAsync();

		_exchangeService
			.Setup(s => s.GetExchangeWithMatch(76))
			.ReturnsAsync(exchange);

		var dto = new UpdateExchangeMeetingDto(null, null, null, null, null, true, false);

		var result = await _service.UpdateExchangeMeeting(116, dto);

		Assert.True(result.MarkAsCompletedByUser1);
		Assert.False(result.MarkAsCompletedByUser2);
		Assert.Equal(ExchangeStatus.ACCEPTED, exchange.Status);
	}

	// DELETE TESTS
	[Fact]
	public async Task DeleteExchangeMeeting_MeetingExists_DeletesSuccessfully()
	{
		var meeting = new ExchangeMeeting
		{
			ExchangeMeetingId = 120,
			ExchangeId = 80,
			ExchangeMode = ExchangeMode.CUSTOM,
			CustomLocation = new Point(0, 0) { SRID = 4326 },
			ProposerId = Guid.NewGuid(),
			MeetingStatus = ExchangeMeetingStatus.PROPOSAL
		};
		_db.ExchangeMeetings.Add(meeting);
		await _db.SaveChangesAsync();

		var result = await _service.DeleteExchangeMeeting(120);

		Assert.True(result);
		var deletedMeeting = await _db.ExchangeMeetings.FirstOrDefaultAsync(m => m.ExchangeMeetingId == 120);
		await _db.SaveChangesAsync();
		deletedMeeting = await _db.ExchangeMeetings.FirstOrDefaultAsync(m => m.ExchangeMeetingId == 120);
		Assert.Null(deletedMeeting);
	}

	[Fact]
	public async Task DeleteExchangeMeeting_MeetingDoesNotExist_ReturnsException()
	{
		await Assert.ThrowsAsync<Exception>(() => _service.DeleteExchangeMeeting(9999));
	}
}
