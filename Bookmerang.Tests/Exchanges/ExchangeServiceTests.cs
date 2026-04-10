using Bookmerang.Api.Data;
using Bookmerang.Api.Exceptions;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Implementation.ExchangeServices;
using Bookmerang.Api.Services.Interfaces.Chats;
using Bookmerang.Tests.Helpers;
using Moq;
using Xunit;

namespace Bookmerang.Tests.Exchanges;

public class ExchangeServiceTests : IAsyncLifetime
{
    private AppDbContext _db = null!;
    private ExchangeService _service = null!;
    private Mock<IChatService> _mockChatService = null!;

    public Task InitializeAsync()
    {
        _db = DbContextFactory.CreateInMemory();
        _mockChatService = new Mock<IChatService>();
        _service = new ExchangeService(_db, _mockChatService.Object);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    private async Task<(Exchange exchange, Api.Models.Entities.Match match, Guid user1Id, Guid user2Id)> SeedExchangeWithMatch(
        ExchangeStatus status = ExchangeStatus.NEGOTIATING)
    {
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();

        var chat = new Chat { Type = ChatType.EXCHANGE, CreatedAt = DateTime.UtcNow };
        _db.Chats.Add(chat);
        await _db.SaveChangesAsync();

        var match = new Api.Models.Entities.Match
        {
            User1Id = user1Id,
            User2Id = user2Id,
            Book1Id = 1,
            Book2Id = 2,
            Status = MatchStatus.CHAT_CREATED,
            CreatedAt = DateTime.UtcNow
        };
        _db.Matches.Add(match);
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
        var chat = new Chat { Type = ChatType.EXCHANGE, CreatedAt = DateTime.UtcNow };
        _db.Chats.Add(chat);
        var match = new Api.Models.Entities.Match { User1Id = Guid.NewGuid(), User2Id = Guid.NewGuid(), Book1Id = 1, Book2Id = 2, Status = MatchStatus.CHAT_CREATED, CreatedAt = DateTime.UtcNow };
        _db.Matches.Add(match);
        await _db.SaveChangesAsync();

        var result = await _service.CreateExchange(chat.Id, match.Id);

        Assert.NotNull(result);
        Assert.Equal(chat.Id, result.ChatId);
        Assert.Equal(match.Id, result.MatchId);
        Assert.Single(_db.Exchanges);
    }

    [Fact]
    public async Task CreateExchange_ChatNotFound_ThrowsException()
    {
        var match = new Api.Models.Entities.Match { User1Id = Guid.NewGuid(), User2Id = Guid.NewGuid(), Book1Id = 1, Book2Id = 2, Status = MatchStatus.CHAT_CREATED, CreatedAt = DateTime.UtcNow };
        _db.Matches.Add(match);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateExchange(9999, match.Id));
    }

    [Fact]
    public async Task CreateExchange_MatchAlreadyUsed_ThrowsException()
    {
        var (_, match, _, _) = await SeedExchangeWithMatch();

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
        var (exchange, _, _, _) = await SeedExchangeWithMatch();
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
        var (exchange, _, user1Id, _) = await SeedExchangeWithMatch(ExchangeStatus.NEGOTIATING);

        var result = await _service.AcceptExchange(exchange.ExchangeId, user1Id);

        Assert.Equal(ExchangeStatus.ACCEPTED_BY_1, result.Status);
    }

    [Fact]
    public async Task AcceptExchange_User2FromAcceptedBy1_ChangesToAccepted()
    {
        var (exchange, _, _, user2Id) = await SeedExchangeWithMatch(ExchangeStatus.ACCEPTED_BY_1);

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
        var (exchange, _, user1Id, _) = await SeedExchangeWithMatch(ExchangeStatus.ACCEPTED_BY_1);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.AcceptExchange(exchange.ExchangeId, user1Id));
    }

    // --- DeleteExchange ---

    [Fact]
    public async Task DeleteExchange_Exists_DeletesAndReturnsTrue()
    {
        var (exchange, _, _, _) = await SeedExchangeWithMatch();
        _mockChatService.Setup(s => s.DeleteChat(exchange.ChatId)).ReturnsAsync(true);

        var result = await _service.DeleteExchange(exchange.ExchangeId);

        Assert.True(result);
        Assert.Empty(_db.Exchanges);
        _mockChatService.Verify(s => s.DeleteChat(exchange.ChatId), Times.Once);
    }

    [Fact]
    public async Task DeleteExchange_NotFound_ThrowsNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.DeleteExchange(9999));
    }
}
