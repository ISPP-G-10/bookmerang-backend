using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Implementation.ExchangeServices;
using Bookmerang.Api.Services.Interfaces.Chats;
using Bookmerang.Tests.Helpers;
using Moq;
using Xunit;

namespace Bookmerang.Tests.Exchanges;

public class AdminExchangeServiceTests : IAsyncLifetime
{
    private AppDbContext _db = null!;
    private ExchangeService _service = null!;

    public Task InitializeAsync()
    {
        _db = DbContextFactory.CreateInMemory();
        var mockChatService = new Mock<IChatService>();
        _service = new ExchangeService(_db, mockChatService.Object);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task GetAllExchanges_WithExchanges_ReturnsList()
    {
        var chat1 = new Chat { Type = ChatType.EXCHANGE, CreatedAt = DateTime.UtcNow };
        var chat2 = new Chat { Type = ChatType.EXCHANGE, CreatedAt = DateTime.UtcNow };
        _db.Chats.AddRange(chat1, chat2);
        await _db.SaveChangesAsync();

        var match1 = new Api.Models.Entities.Match { User1Id = Guid.NewGuid(), User2Id = Guid.NewGuid(), Book1Id = 1, Book2Id = 2, Status = MatchStatus.CHAT_CREATED, CreatedAt = DateTime.UtcNow };
        var match2 = new Api.Models.Entities.Match { User1Id = Guid.NewGuid(), User2Id = Guid.NewGuid(), Book1Id = 3, Book2Id = 4, Status = MatchStatus.CHAT_CREATED, CreatedAt = DateTime.UtcNow };
        _db.Matches.AddRange(match1, match2);
        await _db.SaveChangesAsync();

        _db.Exchanges.AddRange(
            new Exchange { ChatId = chat1.Id, MatchId = match1.Id },
            new Exchange { ChatId = chat2.Id, MatchId = match2.Id }
        );
        await _db.SaveChangesAsync();

        var result = await _service.GetAllExchanges();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAllExchanges_Empty_ReturnsEmptyList()
    {
        var result = await _service.GetAllExchanges();

        Assert.Empty(result);
    }
}
