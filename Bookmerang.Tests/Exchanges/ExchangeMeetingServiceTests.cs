using Bookmerang.Api.Data;
using Bookmerang.Api.Models.DTOs;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Implementation.ExchangeServices;
using Bookmerang.Api.Services.Interfaces.Books;
using Bookmerang.Api.Services.Interfaces.ExchangeInterfaces;
using Bookmerang.Tests.Helpers;
using Moq;
using NetTopologySuite.Geometries;
using Xunit;

namespace Bookmerang.Tests.Exchanges;

public class ExchangeMeetingServiceTests : IAsyncLifetime
{
    private AppDbContext _db = null!;
    private Mock<IExchangeService> _mockExchangeService = null!;
    private Mock<IBookRepository> _mockBookRepository = null!;
    private ExchangeMeetingService _service = null!;

    public Task InitializeAsync()
    {
        _db = DbContextFactory.CreateInMemory();
        _mockExchangeService = new Mock<IExchangeService>();
        _mockBookRepository = new Mock<IBookRepository>();
        _service = new ExchangeMeetingService(_db, _mockExchangeService.Object, _mockBookRepository.Object);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    private async Task<Bookspot> SeedBookspot(bool isBookdrop = false)
    {
        var bookspot = new Bookspot
        {
            Nombre = "Test Bookspot",
            AddressText = "Test Address",
            Location = new Point(0, 0) { SRID = 4326 },
            IsBookdrop = isBookdrop,
            Status = BookspotStatus.ACTIVE
        };
        _db.Bookspots.Add(bookspot);
        await _db.SaveChangesAsync();
        return bookspot;
    }

    // --- CreateExchangeMeeting ---

    [Theory]
    [InlineData(ExchangeMode.BOOKSPOT, false)]
    [InlineData(ExchangeMode.BOOKDROP, true)]
    [InlineData(ExchangeMode.CUSTOM, false)]
    public async Task CreateExchangeMeeting_HappyPath_ThreeModes(ExchangeMode mode, bool isBookdrop)
    {
        int? bookspotId = null;
        double? latitud = null;
        double? longitud = null;

        if (mode is ExchangeMode.BOOKSPOT or ExchangeMode.BOOKDROP)
        {
            var bookspot = await SeedBookspot(isBookdrop);
            bookspotId = bookspot.Id;
        }
        else
        {
            latitud = 40.0;
            longitud = -3.0;
        }

        var dto = new CreateExchangeMeetingDto(
            ExchangeId: 1,
            ExchangeMode: mode,
            BookspotId: bookspotId,
            Latitud: latitud,
            Longitud: longitud,
            ScheduledAt: DateTime.UtcNow.AddHours(1)
        );

        var result = await _service.CreateExchangeMeeting(dto, Guid.NewGuid());

        Assert.NotNull(result);
        Assert.Equal(mode, result.ExchangeMode);
        Assert.NotNull(result.CustomLocation);
        Assert.Single(_db.ExchangeMeetings);
    }

    [Fact]
    public async Task CreateExchangeMeeting_PastDate_ThrowsArgumentException()
    {
        var dto = new CreateExchangeMeetingDto(
            ExchangeId: 1,
            ExchangeMode: ExchangeMode.CUSTOM,
            BookspotId: null,
            Latitud: 40.0,
            Longitud: -3.0,
            ScheduledAt: DateTime.UtcNow.AddMinutes(-10)
        );

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateExchangeMeeting(dto, Guid.NewGuid()));

        Assert.Contains("fecha", ex.Message);
    }

    [Fact]
    public async Task CreateExchangeMeeting_BookspotModeWithoutId_ThrowsArgumentException()
    {
        var dto = new CreateExchangeMeetingDto(
            ExchangeId: 1,
            ExchangeMode: ExchangeMode.BOOKSPOT,
            BookspotId: null,
            Latitud: null,
            Longitud: null,
            ScheduledAt: DateTime.UtcNow.AddHours(1)
        );

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateExchangeMeeting(dto, Guid.NewGuid()));

        Assert.Contains("bookspot", ex.Message);
    }

    [Fact]
    public async Task CreateExchangeMeeting_BookdropOnNormalBookspot_ThrowsArgumentException()
    {
        var bookspot = await SeedBookspot(isBookdrop: false);

        var dto = new CreateExchangeMeetingDto(
            ExchangeId: 1,
            ExchangeMode: ExchangeMode.BOOKDROP,
            BookspotId: bookspot.Id,
            Latitud: null,
            Longitud: null,
            ScheduledAt: DateTime.UtcNow.AddHours(1)
        );

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateExchangeMeeting(dto, Guid.NewGuid()));

        Assert.Contains("BookDrop", ex.Message);
    }

    // --- CounterProposeMeeting ---

    [Fact]
    public async Task CounterProposeMeeting_HappyPath_UpdatesMeeting()
    {
        var bookspot = await SeedBookspot(isBookdrop: false);
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();

        var meeting = new ExchangeMeeting
        {
            ExchangeId = 1,
            ExchangeMode = ExchangeMode.CUSTOM,
            ProposerId = user1Id,
            CustomLocation = new Point(-3.0, 40.0) { SRID = 4326 },
            ScheduledAt = DateTime.UtcNow.AddHours(2)
        };
        _db.ExchangeMeetings.Add(meeting);
        await _db.SaveChangesAsync();

        var scheduledAt = DateTime.UtcNow.AddHours(3);
        var dto = new CounterProposeMeetingDto(
            ExchangeMode: ExchangeMode.BOOKSPOT,
            BookspotId: bookspot.Id,
            Latitud: null,
            Longitud: null,
            ScheduledAt: scheduledAt
        );

        var result = await _service.CounterProposeMeeting(meeting, dto, user2Id);

        Assert.Equal(ExchangeMode.BOOKSPOT, result.ExchangeMode);
        Assert.Equal(user2Id, result.ProposerId);
        Assert.Equal(scheduledAt, result.ScheduledAt);
    }

    // --- MarkAsCompleted ---

    [Fact]
    public async Task MarkAsCompleted_OnlyOneUser_SetsOnlyOneFlag()
    {
        var user1Id = Guid.NewGuid();

        var meeting = new ExchangeMeeting
        {
            ExchangeId = 1,
            ExchangeMode = ExchangeMode.BOOKSPOT,
            ProposerId = user1Id,
            CustomLocation = new Point(0, 0) { SRID = 4326 },
            ScheduledAt = DateTime.UtcNow.AddHours(1),
            MarkAsCompletedByUser1 = false,
            MarkAsCompletedByUser2 = false
        };
        _db.ExchangeMeetings.Add(meeting);
        await _db.SaveChangesAsync();

        await _service.MarkAsCompleted(meeting, user1Id);

        Assert.True(meeting.MarkAsCompletedByUser1);
        Assert.False(meeting.MarkAsCompletedByUser2);
        _mockExchangeService.Verify(s => s.GetExchangeWithMatch(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task MarkAsCompleted_BothUsers_CompletesExchangeAndSwapsBooks()
    {
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();

        var book1 = new Book { OwnerId = user1Id, Status = BookStatus.PUBLISHED };
        var book2 = new Book { OwnerId = user2Id, Status = BookStatus.PUBLISHED };
        _db.Books.AddRange(book1, book2);
        await _db.SaveChangesAsync();

        var match = new Api.Models.Entities.Match
        {
            User1Id = user1Id,
            User2Id = user2Id,
            Book1Id = book1.Id,
            Book2Id = book2.Id,
            Status = MatchStatus.CHAT_CREATED,
            CreatedAt = DateTime.UtcNow
        };
        _db.Matches.Add(match);
        await _db.SaveChangesAsync();

        var exchange = new Exchange
        {
            ChatId = 1,
            MatchId = match.Id,
            Status = ExchangeStatus.ACCEPTED
        };
        _db.Exchanges.Add(exchange);
        await _db.SaveChangesAsync();

        // Objeto con Match cargado que devuelve el mock
        var exchangeWithMatch = new Exchange
        {
            ExchangeId = exchange.ExchangeId,
            ChatId = 1,
            MatchId = match.Id,
            Status = ExchangeStatus.ACCEPTED,
            Match = match
        };

        _mockExchangeService
            .Setup(s => s.GetExchangeWithMatch(exchange.ExchangeId))
            .ReturnsAsync(exchangeWithMatch);

        _mockBookRepository
            .Setup(r => r.GetByIdOrThrowAsync(book1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(book1);
        _mockBookRepository
            .Setup(r => r.GetByIdOrThrowAsync(book2.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(book2);

        var meeting = new ExchangeMeeting
        {
            ExchangeId = exchange.ExchangeId,
            ExchangeMode = ExchangeMode.BOOKSPOT,
            ProposerId = user1Id,
            CustomLocation = new Point(0, 0) { SRID = 4326 },
            ScheduledAt = DateTime.UtcNow.AddHours(1),
            MarkAsCompletedByUser1 = true,
            MarkAsCompletedByUser2 = false
        };
        _db.ExchangeMeetings.Add(meeting);
        await _db.SaveChangesAsync();

        await _service.MarkAsCompleted(meeting, user2Id);

        Assert.True(meeting.MarkAsCompletedByUser1);
        Assert.True(meeting.MarkAsCompletedByUser2);
        Assert.Equal(ExchangeStatus.COMPLETED, exchangeWithMatch.Status);
        Assert.Equal(user2Id, book1.OwnerId);
        Assert.Equal(user1Id, book2.OwnerId);
        Assert.Equal(BookStatus.EXCHANGED, book1.Status);
        Assert.Equal(BookStatus.EXCHANGED, book2.Status);
    }

    // --- InvalidateCollateralExchanges ---

    [Fact]
    public async Task InvalidateCollateralExchanges_RejectsAffectedAndRefusesMeetings()
    {
        var match1 = new Api.Models.Entities.Match { User1Id = Guid.NewGuid(), User2Id = Guid.NewGuid(), Book1Id = 1, Book2Id = 2, Status = MatchStatus.CHAT_CREATED, CreatedAt = DateTime.UtcNow };
        var match2 = new Api.Models.Entities.Match { User1Id = Guid.NewGuid(), User2Id = Guid.NewGuid(), Book1Id = 1, Book2Id = 3, Status = MatchStatus.CHAT_CREATED, CreatedAt = DateTime.UtcNow };
        var match3 = new Api.Models.Entities.Match { User1Id = Guid.NewGuid(), User2Id = Guid.NewGuid(), Book1Id = 5, Book2Id = 6, Status = MatchStatus.CHAT_CREATED, CreatedAt = DateTime.UtcNow };
        _db.Matches.AddRange(match1, match2, match3);
        await _db.SaveChangesAsync();

        var exchange2 = new Exchange { ChatId = 1, MatchId = match2.Id, Status = ExchangeStatus.NEGOTIATING };
        var exchange3 = new Exchange { ChatId = 2, MatchId = match3.Id, Status = ExchangeStatus.NEGOTIATING };
        _db.Exchanges.AddRange(exchange2, exchange3);
        await _db.SaveChangesAsync();

        var meeting2 = new ExchangeMeeting
        {
            ExchangeId = exchange2.ExchangeId,
            ExchangeMode = ExchangeMode.BOOKSPOT,
            ProposerId = Guid.NewGuid(),
            CustomLocation = new Point(0, 0) { SRID = 4326 },
            ScheduledAt = DateTime.UtcNow.AddHours(1),
            MeetingStatus = ExchangeMeetingStatus.PROPOSAL
        };
        _db.ExchangeMeetings.Add(meeting2);
        await _db.SaveChangesAsync();

        await _service.InvalidateCollateralExchanges(book1Id: 1, book2Id: 2, completedMatchId: match1.Id);

        Assert.Equal(ExchangeStatus.REJECTED, exchange2.Status);
        Assert.Equal(ExchangeMeetingStatus.REFUSED, meeting2.MeetingStatus);
        Assert.Equal(ExchangeStatus.NEGOTIATING, exchange3.Status);
    }

    // --- AcceptMeeting ---

    [Fact]
    public async Task AcceptMeeting_NonBookdrop_SetsAcceptedWithoutPin()
    {
        var meeting = new ExchangeMeeting
        {
            ExchangeId = 1,
            ExchangeMode = ExchangeMode.BOOKSPOT,
            ProposerId = Guid.NewGuid(),
            CustomLocation = new Point(0, 0) { SRID = 4326 },
            ScheduledAt = DateTime.UtcNow.AddHours(1),
            MeetingStatus = ExchangeMeetingStatus.PROPOSAL,
            BookDropStatus = null
        };
        _db.ExchangeMeetings.Add(meeting);
        await _db.SaveChangesAsync();

        var result = await _service.AcceptMeeting(meeting);

        Assert.Equal(ExchangeMeetingStatus.ACCEPTED, result.MeetingStatus);
        Assert.Null(result.Pin);
        Assert.Null(result.BookDropStatus);
    }

    [Fact]
    public async Task AcceptMeeting_BookdropMode_GeneratesPinAndSetsStatus()
    {
        var bookspot = await SeedBookspot(isBookdrop: true);

        var meeting = new ExchangeMeeting
        {
            ExchangeId = 1,
            ExchangeMode = ExchangeMode.BOOKDROP,
            BookspotId = bookspot.Id,
            ProposerId = Guid.NewGuid(),
            CustomLocation = new Point(0, 0) { SRID = 4326 },
            ScheduledAt = DateTime.UtcNow.AddHours(1),
            MeetingStatus = ExchangeMeetingStatus.PROPOSAL,
            BookDropStatus = null
        };
        _db.ExchangeMeetings.Add(meeting);
        await _db.SaveChangesAsync();

        var result = await _service.AcceptMeeting(meeting);

        Assert.Equal(ExchangeMeetingStatus.ACCEPTED, result.MeetingStatus);
        Assert.NotNull(result.Pin);
        Assert.Equal(6, result.Pin.Length);
        Assert.Equal(BookdropExchangeStatus.AWAITING_DROP_1, result.BookDropStatus);
    }
}
