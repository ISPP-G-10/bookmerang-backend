using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Services.Implementation.Chats;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Xunit;

namespace Bookmerang.Tests.Chats;

public class ChatServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static ChatService CreateService(AppDbContext db)
    {
        return new ChatService(db);
    }

    private async Task<(Guid user1Id, Guid user2Id)> SeedUsers(AppDbContext db)
    {
        var u1Id = Guid.NewGuid();
        var u2Id = Guid.NewGuid();

        var bu1 = new BaseUser { Id = u1Id, SupabaseId = "s1", Email = "u1@test.com", Username = "user1", Name = "User 1", Location = new Point(0, 0) { SRID = 4326 } };
        var bu2 = new BaseUser { Id = u2Id, SupabaseId = "s2", Email = "u2@test.com", Username = "user2", Name = "User 2", Location = new Point(0, 0) { SRID = 4326 } };

        db.Users.AddRange(bu1, bu2);
        db.RegularUsers.AddRange(new User { Id = u1Id }, new User { Id = u2Id });
        await db.SaveChangesAsync();

        return (u1Id, u2Id);
    }

    [Fact]
    public async Task GetUserChats_ShouldReturnChats_WhenUserIsParticipant()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var (u1Id, u2Id) = await SeedUsers(db);

        var chat = new Chat { Type = ChatType.EXCHANGE, CreatedAt = DateTime.UtcNow };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        db.ChatParticipants.AddRange(
            new ChatParticipant { ChatId = chat.Id, UserId = u1Id, JoinedAt = DateTime.UtcNow },
            new ChatParticipant { ChatId = chat.Id, UserId = u2Id, JoinedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var result = await service.GetUserChats(u1Id);

        Assert.Single(result);
        Assert.Equal(chat.Id, result[0].Id);
        Assert.Equal(2, result[0].Participants.Count);
    }

    [Fact]
    public async Task GetChatById_ShouldReturnChat_WhenUserIsParticipant()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var (u1Id, _) = await SeedUsers(db);

        var chat = new Chat { Type = ChatType.EXCHANGE, CreatedAt = DateTime.UtcNow };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        db.ChatParticipants.Add(new ChatParticipant { ChatId = chat.Id, UserId = u1Id, JoinedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var result = await service.GetChatById(chat.Id, u1Id);

        Assert.NotNull(result);
        Assert.Equal(chat.Id, result.Id);
    }

    [Fact]
    public async Task GetChatById_ShouldReturnNull_WhenUserIsNotParticipant()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var (u1Id, u2Id) = await SeedUsers(db);

        var chat = new Chat { Type = ChatType.EXCHANGE, CreatedAt = DateTime.UtcNow };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        db.ChatParticipants.Add(new ChatParticipant { ChatId = chat.Id, UserId = u2Id, JoinedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var result = await service.GetChatById(chat.Id, u1Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetMessages_ShouldReturnMessages_WhenUserIsParticipant()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var (u1Id, _) = await SeedUsers(db);

        var chat = new Chat { Type = ChatType.EXCHANGE, CreatedAt = DateTime.UtcNow };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        db.ChatParticipants.Add(new ChatParticipant { ChatId = chat.Id, UserId = u1Id, JoinedAt = DateTime.UtcNow });
        db.Messages.Add(new Message { ChatId = chat.Id, SenderId = u1Id, Body = "Hello", SentAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var result = await service.GetMessages(chat.Id, u1Id, 1, 10);

        Assert.Single(result);
        Assert.Equal("Hello", result[0].Body);
    }

    [Fact]
    public async Task SendMessage_ShouldCreateMessage_WhenUserIsParticipant()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var (u1Id, _) = await SeedUsers(db);

        var chat = new Chat { Type = ChatType.EXCHANGE, CreatedAt = DateTime.UtcNow };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        db.ChatParticipants.Add(new ChatParticipant { ChatId = chat.Id, UserId = u1Id, JoinedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var result = await service.SendMessage(chat.Id, u1Id, "New Message");

        Assert.NotNull(result);
        Assert.Equal("New Message", result.Body);
        Assert.Equal(u1Id, result.SenderId);
        Assert.Single(db.Messages);
    }

    [Fact]
    public async Task SendMessage_ShouldReturnNull_WhenUserIsNotParticipant()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var (u1Id, u2Id) = await SeedUsers(db);

        var chat = new Chat { Type = ChatType.EXCHANGE, CreatedAt = DateTime.UtcNow };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        db.ChatParticipants.Add(new ChatParticipant { ChatId = chat.Id, UserId = u2Id, JoinedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var result = await service.SendMessage(chat.Id, u1Id, "New Message");

        Assert.Null(result);
        Assert.Empty(db.Messages);
    }

    [Fact]
    public async Task CreateChat_ShouldCreateChatAndParticipants()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var (u1Id, u2Id) = await SeedUsers(db);

        var participantIds = new List<Guid> { u1Id, u2Id };
        var result = await service.CreateChat(ChatType.EXCHANGE, participantIds);

        Assert.NotNull(result);
        Assert.Equal(ChatType.EXCHANGE.ToString(), result.Type);
        Assert.Equal(2, db.ChatParticipants.Count());
        // JRP 28/03: Sin mensajes iniciales por el cambio en el manejo de los placeholders de mensajes
        Assert.Equal(0, db.Messages.Count());
    }

    [Fact]
    public async Task StartTyping_ShouldCreateOrUpdateIndicator()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var (u1Id, _) = await SeedUsers(db);

        var chat = new Chat { Type = ChatType.EXCHANGE, CreatedAt = DateTime.UtcNow };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        db.ChatParticipants.Add(new ChatParticipant { ChatId = chat.Id, UserId = u1Id, JoinedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var success = await service.StartTyping(chat.Id, u1Id);

        Assert.True(success);
        Assert.Single(db.TypingIndicators);

        // Update
        var successUpdate = await service.StartTyping(chat.Id, u1Id);
        Assert.True(successUpdate);
        Assert.Single(db.TypingIndicators);
    }

    [Fact]
    public async Task StopTyping_ShouldRemoveIndicator()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var (u1Id, _) = await SeedUsers(db);

        var chat = new Chat { Type = ChatType.EXCHANGE, CreatedAt = DateTime.UtcNow };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        db.TypingIndicators.Add(new TypingIndicator { ChatId = chat.Id, UserId = u1Id, StartedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var success = await service.StopTyping(chat.Id, u1Id);

        Assert.True(success);
        Assert.Empty(db.TypingIndicators);
    }

    [Fact]
    public async Task GetTypingUsers_ShouldReturnOtherUsers_WhenTheyAreTyping()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var (u1Id, u2Id) = await SeedUsers(db);

        var chat = new Chat { Type = ChatType.EXCHANGE, CreatedAt = DateTime.UtcNow };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        db.ChatParticipants.AddRange(
            new ChatParticipant { ChatId = chat.Id, UserId = u1Id, JoinedAt = DateTime.UtcNow },
            new ChatParticipant { ChatId = chat.Id, UserId = u2Id, JoinedAt = DateTime.UtcNow }
        );
        db.TypingIndicators.Add(new TypingIndicator { ChatId = chat.Id, UserId = u2Id, StartedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var result = await service.GetTypingUsers(chat.Id, u1Id);

        Assert.Single(result);
        Assert.Equal(u2Id, result[0].UserId);
    }

    [Fact]
    public async Task GetUserChats_ShouldIncludeCommunityName_WhenChatIsCommunity()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var (u1Id, _) = await SeedUsers(db);

        var chat = new Chat { Type = ChatType.COMMUNITY, CreatedAt = DateTime.UtcNow };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        db.ChatParticipants.Add(new ChatParticipant { ChatId = chat.Id, UserId = u1Id, JoinedAt = DateTime.UtcNow });
        
        var community = new Community { Name = "Test Community", CreatorId = u1Id, ReferenceBookspotId = 1 };
        db.Communities.Add(community);
        await db.SaveChangesAsync();

        db.CommunityChats.Add(new CommunityChat { ChatId = chat.Id, CommunityId = community.Id });
        await db.SaveChangesAsync();

        var result = await service.GetUserChats(u1Id);

        Assert.Single(result);
        Assert.Equal("Test Community", result[0].Name);
    }

    [Fact]
    public async Task CreateChat_ShouldReturnNull_WhenUserDoesNotExist()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        
        var participantIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var result = await service.CreateChat(ChatType.EXCHANGE, participantIds);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetTypingUsers_ShouldExcludeOldIndicators()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var (u1Id, u2Id) = await SeedUsers(db);

        var chat = new Chat { Type = ChatType.EXCHANGE, CreatedAt = DateTime.UtcNow };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        db.ChatParticipants.AddRange(
            new ChatParticipant { ChatId = chat.Id, UserId = u1Id, JoinedAt = DateTime.UtcNow },
            new ChatParticipant { ChatId = chat.Id, UserId = u2Id, JoinedAt = DateTime.UtcNow }
        );
        // Old indicator (10 seconds ago)
        db.TypingIndicators.Add(new TypingIndicator { ChatId = chat.Id, UserId = u2Id, StartedAt = DateTime.UtcNow.AddSeconds(-10) });
        await db.SaveChangesAsync();

        var result = await service.GetTypingUsers(chat.Id, u1Id);

        Assert.Empty(result);
    }
}
