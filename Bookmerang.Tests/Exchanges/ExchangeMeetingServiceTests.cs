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

using MatchEntity = Bookmerang.Api.Models.Entities.Match;

namespace Bookmerang.Tests.Exchanges;

public class ExchangeMeetingServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private AppDbContext _db = null!;
    private Mock<IExchangeService> _mockExchangeService = null!;
    private Mock<IBookRepository> _mockBookRepository = null!;
    private ExchangeMeetingService _service = null!;

    public Task InitializeAsync()
    {
        _db = fixture.CreateDbContext();
        _mockExchangeService = new Mock<IExchangeService>();
        _mockBookRepository = new Mock<IBookRepository>();
        _service = new ExchangeMeetingService(_db, _mockExchangeService.Object, _mockBookRepository.Object);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    // ── Seed helpers ────────────────────────────────────────────────

    private async Task<Guid> SeedUser(string prefix)
    {
        var id = Guid.NewGuid();
        _db.Users.Add(new BaseUser
        {
            Id = id,
            SupabaseId = $"supa_{id:N}",
            Email = $"{prefix}_{id:N}@test.com",
            Username = prefix,
            Name = $"{prefix} User",
            ProfilePhoto = "photo.jpg",
            UserType = BaseUserType.USER,
            Location = new Point(-5.98, 37.39) { SRID = 4326 }
        });
        _db.RegularUsers.Add(new User { Id = id });
        await _db.SaveChangesAsync();
        return id;
    }

    private record SeedData(Guid User1Id, Guid User2Id, Exchange Exchange, Book Book1, Book Book2, MatchEntity Match);

    private async Task<SeedData> SeedExchangeChain(string prefix, ExchangeStatus status = ExchangeStatus.ACCEPTED)
    {
        var u1 = await SeedUser($"{prefix}_u1");
        var u2 = await SeedUser($"{prefix}_u2");

        var book1 = new Book { OwnerId = u1, Status = BookStatus.PUBLISHED };
        var book2 = new Book { OwnerId = u2, Status = BookStatus.PUBLISHED };
        _db.Books.AddRange(book1, book2);
        await _db.SaveChangesAsync();

        var match = new MatchEntity
        {
            User1Id = u1, User2Id = u2,
            Book1Id = book1.Id, Book2Id = book2.Id,
            Status = MatchStatus.CHAT_CREATED, CreatedAt = DateTime.UtcNow
        };
        _db.Matches.Add(match);
        await _db.SaveChangesAsync();

        var chat = new Chat { Type = ChatType.EXCHANGE, CreatedAt = DateTime.UtcNow };
        _db.Chats.Add(chat);
        await _db.SaveChangesAsync();

        var exchange = new Exchange { ChatId = chat.Id, MatchId = match.Id, Status = status };
        _db.Exchanges.Add(exchange);
        await _db.SaveChangesAsync();

        return new SeedData(u1, u2, exchange, book1, book2, match);
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
        var seed = await SeedExchangeChain($"create_{mode}");

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
            ExchangeId: seed.Exchange.ExchangeId,
            ExchangeMode: mode,
            BookspotId: bookspotId,
            Latitud: latitud,
            Longitud: longitud,
            ScheduledAt: DateTime.UtcNow.AddHours(1)
        );

        var result = await _service.CreateExchangeMeeting(dto, seed.User1Id);

        Assert.NotNull(result);
        Assert.Equal(mode, result.ExchangeMode);
        Assert.NotNull(result.CustomLocation);
        Assert.NotNull(result.Proposer);
        Assert.NotNull(result.Proposer.BaseUser);
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
        var seed = await SeedExchangeChain("counter");
        var bookspot = await SeedBookspot(isBookdrop: false);

        var meeting = new ExchangeMeeting
        {
            ExchangeId = seed.Exchange.ExchangeId,
            ExchangeMode = ExchangeMode.CUSTOM,
            ProposerId = seed.User1Id,
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

        var result = await _service.CounterProposeMeeting(meeting, dto, seed.User2Id);

        Assert.Equal(ExchangeMode.BOOKSPOT, result.ExchangeMode);
        Assert.Equal(seed.User2Id, result.ProposerId);
        Assert.Equal(scheduledAt, result.ScheduledAt);
        Assert.NotNull(result.Proposer);
        Assert.NotNull(result.Proposer.BaseUser);
    }

    // --- MarkAsCompleted ---

    [Fact]
    public async Task MarkAsCompleted_OnlyOneUser_SetsOnlyOneFlag()
    {
        var seed = await SeedExchangeChain("mark_one");

        var meeting = new ExchangeMeeting
        {
            ExchangeId = seed.Exchange.ExchangeId,
            ExchangeMode = ExchangeMode.BOOKSPOT,
            ProposerId = seed.User1Id,
            CustomLocation = new Point(0, 0) { SRID = 4326 },
            ScheduledAt = DateTime.UtcNow.AddHours(1),
            MarkAsCompletedByUser1 = false,
            MarkAsCompletedByUser2 = false
        };
        _db.ExchangeMeetings.Add(meeting);
        await _db.SaveChangesAsync();

        await _service.MarkAsCompleted(meeting, seed.User1Id);

        Assert.True(meeting.MarkAsCompletedByUser1);
        Assert.False(meeting.MarkAsCompletedByUser2);
        _mockExchangeService.Verify(s => s.GetExchangeWithMatch(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task MarkAsCompleted_BothUsers_CompletesExchangeAndSwapsBooks()
    {
        var seed = await SeedExchangeChain("mark_both");

        var exchangeWithMatch = new Exchange
        {
            ExchangeId = seed.Exchange.ExchangeId,
            ChatId = seed.Exchange.ChatId,
            MatchId = seed.Match.Id,
            Status = ExchangeStatus.ACCEPTED,
            Match = seed.Match
        };

        _mockExchangeService
            .Setup(s => s.GetExchangeWithMatch(seed.Exchange.ExchangeId))
            .ReturnsAsync(exchangeWithMatch);

        _mockBookRepository
            .Setup(r => r.GetByIdOrThrowAsync(seed.Book1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(seed.Book1);
        _mockBookRepository
            .Setup(r => r.GetByIdOrThrowAsync(seed.Book2.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(seed.Book2);

        var meeting = new ExchangeMeeting
        {
            ExchangeId = seed.Exchange.ExchangeId,
            ExchangeMode = ExchangeMode.BOOKSPOT,
            ProposerId = seed.User1Id,
            CustomLocation = new Point(0, 0) { SRID = 4326 },
            ScheduledAt = DateTime.UtcNow.AddHours(1),
            MarkAsCompletedByUser1 = true,
            MarkAsCompletedByUser2 = false
        };
        _db.ExchangeMeetings.Add(meeting);
        await _db.SaveChangesAsync();

        await _service.MarkAsCompleted(meeting, seed.User2Id);

        Assert.True(meeting.MarkAsCompletedByUser1);
        Assert.True(meeting.MarkAsCompletedByUser2);
        Assert.Equal(ExchangeStatus.COMPLETED, exchangeWithMatch.Status);
        Assert.Equal(seed.User2Id, seed.Book1.OwnerId);
        Assert.Equal(seed.User1Id, seed.Book2.OwnerId);
        Assert.Equal(BookStatus.EXCHANGED, seed.Book1.Status);
        Assert.Equal(BookStatus.EXCHANGED, seed.Book2.Status);
    }

    // --- InvalidateCollateralExchanges ---

    [Fact]
    public async Task InvalidateCollateralExchanges_RejectsAffectedAndRefusesMeetings()
    {
        var u1 = await SeedUser("inv_u1");
        var u2 = await SeedUser("inv_u2");

        var book1 = new Book { OwnerId = u1, Status = BookStatus.PUBLISHED };
        var book2 = new Book { OwnerId = u2, Status = BookStatus.PUBLISHED };
        var book3 = new Book { OwnerId = u2, Status = BookStatus.PUBLISHED };
        var book5 = new Book { OwnerId = u1, Status = BookStatus.PUBLISHED };
        var book6 = new Book { OwnerId = u2, Status = BookStatus.PUBLISHED };
        _db.Books.AddRange(book1, book2, book3, book5, book6);
        await _db.SaveChangesAsync();

        var match1 = new MatchEntity { User1Id = u1, User2Id = u2, Book1Id = book1.Id, Book2Id = book2.Id, Status = MatchStatus.CHAT_CREATED, CreatedAt = DateTime.UtcNow };
        var match2 = new MatchEntity { User1Id = u1, User2Id = u2, Book1Id = book1.Id, Book2Id = book3.Id, Status = MatchStatus.CHAT_CREATED, CreatedAt = DateTime.UtcNow };
        var match3 = new MatchEntity { User1Id = u1, User2Id = u2, Book1Id = book5.Id, Book2Id = book6.Id, Status = MatchStatus.CHAT_CREATED, CreatedAt = DateTime.UtcNow };
        _db.Matches.AddRange(match1, match2, match3);
        await _db.SaveChangesAsync();

        var chat2 = new Chat { Type = ChatType.EXCHANGE, CreatedAt = DateTime.UtcNow };
        var chat3 = new Chat { Type = ChatType.EXCHANGE, CreatedAt = DateTime.UtcNow };
        _db.Chats.AddRange(chat2, chat3);
        await _db.SaveChangesAsync();

        var exchange2 = new Exchange { ChatId = chat2.Id, MatchId = match2.Id, Status = ExchangeStatus.NEGOTIATING };
        var exchange3 = new Exchange { ChatId = chat3.Id, MatchId = match3.Id, Status = ExchangeStatus.NEGOTIATING };
        _db.Exchanges.AddRange(exchange2, exchange3);
        await _db.SaveChangesAsync();

        var meeting2 = new ExchangeMeeting
        {
            ExchangeId = exchange2.ExchangeId,
            ExchangeMode = ExchangeMode.BOOKSPOT,
            ProposerId = u1,
            CustomLocation = new Point(0, 0) { SRID = 4326 },
            ScheduledAt = DateTime.UtcNow.AddHours(1),
            MeetingStatus = ExchangeMeetingStatus.PROPOSAL
        };
        _db.ExchangeMeetings.Add(meeting2);
        await _db.SaveChangesAsync();

        await _service.InvalidateCollateralExchanges(book1Id: book1.Id, book2Id: book2.Id, completedMatchId: match1.Id);

        Assert.Equal(ExchangeStatus.REJECTED, exchange2.Status);
        Assert.Equal(ExchangeMeetingStatus.REFUSED, meeting2.MeetingStatus);
        Assert.Equal(ExchangeStatus.NEGOTIATING, exchange3.Status);
    }

    // --- AcceptMeeting ---

    [Fact]
    public async Task AcceptMeeting_NonBookdrop_SetsAcceptedWithoutPin()
    {
        var seed = await SeedExchangeChain("accept_no_bd");

        var meeting = new ExchangeMeeting
        {
            ExchangeId = seed.Exchange.ExchangeId,
            ExchangeMode = ExchangeMode.BOOKSPOT,
            ProposerId = seed.User1Id,
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
        var seed = await SeedExchangeChain("accept_bd");
        var bookspot = await SeedBookspot(isBookdrop: true);

        var meeting = new ExchangeMeeting
        {
            ExchangeId = seed.Exchange.ExchangeId,
            ExchangeMode = ExchangeMode.BOOKDROP,
            BookspotId = bookspot.Id,
            ProposerId = seed.User1Id,
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
