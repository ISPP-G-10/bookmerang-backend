using Bookmerang.Api.Data;
using Bookmerang.Api.Exceptions;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Implementation.Chats;
using Bookmerang.Api.Services.Implementation.ExchangeServices;
using Bookmerang.Tests.Helpers;
using Xunit;

using MatchEntity = Bookmerang.Api.Models.Entities.Match;

namespace Bookmerang.Tests.Exchanges;

public class ExchangeServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private AppDbContext _db = null!;
    private ExchangeService _service = null!;
    private ChatService _chatService = null!;

    public Task InitializeAsync()
    {
        _db = fixture.CreateDbContext();
        _chatService = new ChatService(_db);
        _service = new ExchangeService(_db, _chatService);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    // ── Seed helpers ────────────────────────────────────────────────

    private async Task<(Exchange exchange, MatchEntity match, Guid user1Id, Guid user2Id)> SeedExchangeWithMatch(
        string prefix, ExchangeStatus status = ExchangeStatus.NEGOTIATING)
    {
        var user1Id = await TestSeedHelper.SeedUser(_db, $"{prefix}_u1");
        var user2Id = await TestSeedHelper.SeedUser(_db, $"{prefix}_u2");

        var book1 = new Book { OwnerId = user1Id, Status = BookStatus.PUBLISHED };
        var book2 = new Book { OwnerId = user2Id, Status = BookStatus.PUBLISHED };
        _db.Books.AddRange(book1, book2);
        await _db.SaveChangesAsync();

        var match = new MatchEntity
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

        var chat = new Chat { Type = ChatType.EXCHANGE, CreatedAt = DateTime.UtcNow };
        _db.Chats.Add(chat);
        await _db.SaveChangesAsync();

        var exchange = new Exchange { ChatId = chat.Id, MatchId = match.Id, Status = status };
        _db.Exchanges.Add(exchange);
        await _db.SaveChangesAsync();

        return (exchange, match, user1Id, user2Id);
    }

    // --- CreateExchange ---

    [Fact]
    public async Task CreateExchange_ValidData_CreatesExchange()
    {
        var u1 = await TestSeedHelper.SeedUser(_db, "ce_valid_u1");
        var u2 = await TestSeedHelper.SeedUser(_db, "ce_valid_u2");

        var book1 = new Book { OwnerId = u1, Status = BookStatus.PUBLISHED };
        var book2 = new Book { OwnerId = u2, Status = BookStatus.PUBLISHED };
        _db.Books.AddRange(book1, book2);
        await _db.SaveChangesAsync();

        var chat = new Chat { Type = ChatType.EXCHANGE, CreatedAt = DateTime.UtcNow };
        _db.Chats.Add(chat);
        var match = new MatchEntity
        {
            User1Id = u1,
            User2Id = u2,
            Book1Id = book1.Id,
            Book2Id = book2.Id,
            Status = MatchStatus.CHAT_CREATED,
            CreatedAt = DateTime.UtcNow
        };
        _db.Matches.Add(match);
        await _db.SaveChangesAsync();

        var result = await _service.CreateExchange(chat.Id, match.Id);

        Assert.NotNull(result);
        Assert.Equal(chat.Id, result.ChatId);
        Assert.Equal(match.Id, result.MatchId);
    }

    [Fact]
    public async Task CreateExchange_ChatNotFound_ThrowsException()
    {
        var u1 = await TestSeedHelper.SeedUser(_db, "ce_nochat_u1");
        var u2 = await TestSeedHelper.SeedUser(_db, "ce_nochat_u2");

        var book1 = new Book { OwnerId = u1, Status = BookStatus.PUBLISHED };
        var book2 = new Book { OwnerId = u2, Status = BookStatus.PUBLISHED };
        _db.Books.AddRange(book1, book2);
        await _db.SaveChangesAsync();

        var match = new MatchEntity
        {
            User1Id = u1,
            User2Id = u2,
            Book1Id = book1.Id,
            Book2Id = book2.Id,
            Status = MatchStatus.CHAT_CREATED,
            CreatedAt = DateTime.UtcNow
        };
        _db.Matches.Add(match);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateExchange(9999, match.Id));
    }

    [Fact]
    public async Task CreateExchange_MatchAlreadyUsed_ThrowsException()
    {
        var (_, match, _, _) = await SeedExchangeWithMatch("ce_dup");

        var newChat = new Chat { Type = ChatType.EXCHANGE, CreatedAt = DateTime.UtcNow };
        _db.Chats.Add(newChat);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateExchange(newChat.Id, match.Id));
    }

    // --- UpdateExchangeStatus ---

    [Fact]
    public async Task UpdateExchangeStatus_UpdatesStatusAndDate()
    {
        var (exchange, _, _, _) = await SeedExchangeWithMatch("upd_status");
        var pastDate = DateTime.UtcNow.AddMinutes(-5);
        exchange.UpdatedAt = pastDate;
        await _db.SaveChangesAsync();

        var result = await _service.UpdateExchangeStatus(exchange, ExchangeStatus.ACCEPTED_BY_1);

        Assert.Equal(ExchangeStatus.ACCEPTED_BY_1, result.Status);
        Assert.True(result.UpdatedAt > pastDate);
    }

    // --- AcceptExchange ---

    [Fact]
    public async Task AcceptExchange_User1FromNegotiating_ChangesToAcceptedBy1()
    {
        var (exchange, _, user1Id, _) = await SeedExchangeWithMatch("acc_u1", ExchangeStatus.NEGOTIATING);

        var result = await _service.AcceptExchange(exchange.ExchangeId, user1Id);

        Assert.Equal(ExchangeStatus.ACCEPTED_BY_1, result.Status);
    }

    [Fact]
    public async Task AcceptExchange_User2FromAcceptedBy1_ChangesToAccepted()
    {
        var (exchange, _, _, user2Id) = await SeedExchangeWithMatch("acc_u2", ExchangeStatus.ACCEPTED_BY_1);

        var result = await _service.AcceptExchange(exchange.ExchangeId, user2Id);

        Assert.Equal(ExchangeStatus.ACCEPTED, result.Status);
    }

    [Fact]
    public async Task AcceptExchange_NotFound_ThrowsNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.AcceptExchange(9999, Guid.NewGuid()));
    }

    [Fact]
    public async Task AcceptExchange_InvalidState_ThrowsValidationException()
    {
        var (exchange, _, user1Id, _) = await SeedExchangeWithMatch("acc_invalid", ExchangeStatus.ACCEPTED_BY_1);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.AcceptExchange(exchange.ExchangeId, user1Id));
    }

    // --- DeleteExchange ---

    [Fact]
    public async Task DeleteExchange_Exists_DeletesAndReturnsTrue()
    {
        var (exchange, _, _, _) = await SeedExchangeWithMatch("del_ok");

        var result = await _service.DeleteExchange(exchange.ExchangeId);

        Assert.True(result);
        Assert.Null(await _db.Exchanges.FindAsync(exchange.ExchangeId));
        Assert.Null(await _db.Chats.FindAsync(exchange.ChatId));
    }

    [Fact]
    public async Task DeleteExchange_NotFound_ThrowsNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.DeleteExchange(9999));
    }
}
