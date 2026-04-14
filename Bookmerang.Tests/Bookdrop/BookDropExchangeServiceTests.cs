using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Implementation.Bookdrop;
using Bookmerang.Api.Services.Implementation.Books;
using Bookmerang.Api.Services.Implementation.Chats;
using Bookmerang.Api.Services.Implementation.ExchangeServices;
using Bookmerang.Api.Services.Implementation.Inkdrops;
using Bookmerang.Api.Services.Implementation.Streaks;
using Bookmerang.Tests.Helpers;
using NetTopologySuite.Geometries;
using Xunit;

using MatchEntity = Bookmerang.Api.Models.Entities.Match;

namespace Bookmerang.Tests.Bookdrop;

public class BookDropExchangeServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private AppDbContext _db = null!;
    private BookDropExchangeService _service = null!;

    public Task InitializeAsync()
    {
        _db = fixture.CreateDbContext();
        var chatService = new ChatService(_db);
        var exchangeService = new ExchangeService(_db, chatService);
        var bookRepository = new BookRepository(_db);
        var streakService = new StreakService(_db);
        var inkdropsService = new InkdropsService(_db, streakService);
        var meetingService = new ExchangeMeetingService(_db, exchangeService, bookRepository, inkdropsService);
        _service = new BookDropExchangeService(_db, meetingService, inkdropsService);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    // ── Seed helpers ────────────────────────────────────────────────

    private record SeedData(
        Guid User1Id, Guid User2Id,
        Book Book1, Book Book2,
        Exchange Exchange, ExchangeMeeting Meeting,
        Bookspot Bookspot);

    private async Task<SeedData> SeedFullChain(
        string prefix,
        BookdropExchangeStatus dropStatus = BookdropExchangeStatus.AWAITING_DROP_1,
        ExchangeStatus exchangeStatus = ExchangeStatus.ACCEPTED)
    {
        var u1 = await TestSeedHelper.SeedUser(_db, $"{prefix}_u1");
        var u2 = await TestSeedHelper.SeedUser(_db, $"{prefix}_u2");

        var book1 = new Book { OwnerId = u1, Status = BookStatus.PUBLISHED, Titulo = "Libro A" };
        var book2 = new Book { OwnerId = u2, Status = BookStatus.PUBLISHED, Titulo = "Libro B" };
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

        var exchange = new Exchange { ChatId = chat.Id, MatchId = match.Id, Status = exchangeStatus };
        _db.Exchanges.Add(exchange);
        await _db.SaveChangesAsync();

        var bookspot = await TestSeedHelper.SeedBookspot(_db, isBookdrop: true);

        var meeting = new ExchangeMeeting
        {
            ExchangeId = exchange.ExchangeId,
            ExchangeMode = ExchangeMode.BOOKDROP,
            BookspotId = bookspot.Id,
            CustomLocation = new Point(-5.98, 37.39) { SRID = 4326 },
            ProposerId = u1,
            MeetingStatus = ExchangeMeetingStatus.ACCEPTED,
            Pin = "123456",
            BookDropStatus = dropStatus,
            ScheduledAt = DateTime.UtcNow.AddHours(1)
        };
        _db.ExchangeMeetings.Add(meeting);
        await _db.SaveChangesAsync();

        return new SeedData(u1, u2, book1, book2, exchange, meeting, bookspot);
    }

    // --- GetActiveExchanges ---

    [Fact]
    public async Task GetActiveExchanges_ReturnsActiveMeetings()
    {
        var seed = await SeedFullChain("gae_ok");

        var result = await _service.GetActiveExchanges(seed.Bookspot.Id);

        var dto = Assert.Single(result);
        Assert.Equal(seed.Meeting.ExchangeMeetingId, dto.MeetingId);
        Assert.Equal(BookdropExchangeStatus.AWAITING_DROP_1, dto.Status);
        Assert.Equal("Libro A", dto.Book1Title);
        Assert.Equal("Libro B", dto.Book2Title);
    }

    [Fact]
    public async Task GetActiveExchanges_BookspotNotFound_Throws()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.GetActiveExchanges(9999));
    }

    // --- ConfirmDrop (validación compartida de GetAndValidateMeeting) ---

    [Fact]
    public async Task ConfirmDrop_HappyPath_TransitionsToBook1Held()
    {
        var seed = await SeedFullChain("cd_ok");

        var dto = await _service.ConfirmDrop(seed.Meeting.ExchangeMeetingId, "123456", seed.Bookspot.Id);

        Assert.Equal(BookdropExchangeStatus.BOOK_1_HELD, dto.Status);
    }

    [Fact]
    public async Task ConfirmDrop_WrongPin_Throws()
    {
        var seed = await SeedFullChain("cd_pin");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ConfirmDrop(seed.Meeting.ExchangeMeetingId, "000000", seed.Bookspot.Id));
    }

    [Fact]
    public async Task ConfirmDrop_WrongStatus_Throws()
    {
        var seed = await SeedFullChain("cd_status", BookdropExchangeStatus.BOOK_1_HELD);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ConfirmDrop(seed.Meeting.ExchangeMeetingId, "123456", seed.Bookspot.Id));
    }

    // --- ConfirmSwap ---

    [Fact]
    public async Task ConfirmSwap_HappyPath_TransitionsToBook2Held()
    {
        var seed = await SeedFullChain("cs_ok", BookdropExchangeStatus.BOOK_1_HELD);

        var dto = await _service.ConfirmSwap(seed.Meeting.ExchangeMeetingId, "123456", seed.Bookspot.Id);

        Assert.Equal(BookdropExchangeStatus.BOOK_2_HELD, dto.Status);
    }

    // --- ConfirmPickup (integración: verifica todos los side effects) ---

    [Fact]
    public async Task ConfirmPickup_HappyPath_CompletesExchangeAndSwapsBooks()
    {
        var seed = await SeedFullChain("cp_ok", BookdropExchangeStatus.BOOK_2_HELD);

        var dto = await _service.ConfirmPickup(seed.Meeting.ExchangeMeetingId, "123456", seed.Bookspot.Id);

        Assert.Equal(BookdropExchangeStatus.COMPLETED, dto.Status);

        // Meeting: status COMPLETED y pin borrado
        var meeting = await _db.ExchangeMeetings.FindAsync(seed.Meeting.ExchangeMeetingId);
        Assert.NotNull(meeting);
        Assert.Null(meeting.Pin);
        Assert.Equal(BookdropExchangeStatus.COMPLETED, meeting.BookDropStatus);

        // Exchange: marcado como completado
        var exchange = await _db.Exchanges.FindAsync(seed.Exchange.ExchangeId);
        Assert.NotNull(exchange);
        Assert.Equal(ExchangeStatus.COMPLETED, exchange.Status);

        // Libros: ownership intercambiado y status EXCHANGED
        var book1 = await _db.Books.FindAsync(seed.Book1.Id);
        var book2 = await _db.Books.FindAsync(seed.Book2.Id);
        Assert.NotNull(book1);
        Assert.NotNull(book2);
        Assert.Equal(seed.User2Id, book1.OwnerId);
        Assert.Equal(seed.User1Id, book2.OwnerId);
        Assert.Equal(BookStatus.EXCHANGED, book1.Status);
        Assert.Equal(BookStatus.EXCHANGED, book2.Status);
    }
}
