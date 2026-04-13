using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NetTopologySuite.Geometries;
using Xunit;

namespace Bookmerang.Tests.Chats.Integration;

public class ChatsIntegrationTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture = fixture;

    [Fact]
    public async Task ChatType_EnumMapping_WorksCorrectly()
    {
        await using var db = _fixture.CreateDbContext();
        
        var chat = new Chat
        {
            Type = ChatType.EXCHANGE,
            CreatedAt = DateTime.UtcNow
        };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();

        var persisted = await db.Chats.SingleAsync(c => c.Id == chat.Id);
        Assert.Equal(ChatType.EXCHANGE, persisted.Type);

        persisted.Type = ChatType.COMMUNITY;
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var updated = await db.Chats.SingleAsync(c => c.Id == chat.Id);
        Assert.Equal(ChatType.COMMUNITY, updated.Type);
    }

    [Fact]
    public async Task ChatHistory_PersistenceAndOrdering_WorksCorrectly()
    {
        await using var db = _fixture.CreateDbContext();

        var u1Id = Guid.NewGuid();
        var u2Id = Guid.NewGuid();
        await SeedUserAsync(db, u1Id, "s1");
        await SeedUserAsync(db, u2Id, "s2");

        var chat = new Chat { Type = ChatType.EXCHANGE, CreatedAt = DateTime.UtcNow };
        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        var msg1 = new Message { ChatId = chat.Id, SenderId = u1Id, Body = "First", SentAt = DateTime.UtcNow.AddMinutes(-2) };
        var msg2 = new Message { ChatId = chat.Id, SenderId = u2Id, Body = "Second", SentAt = DateTime.UtcNow.AddMinutes(-1) };
        var msg3 = new Message { ChatId = chat.Id, SenderId = u1Id, Body = "Third", SentAt = DateTime.UtcNow };

        db.Messages.AddRange(msg1, msg2, msg3);
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();

        var history = await db.Messages
            .Where(m => m.ChatId == chat.Id)
            .OrderBy(m => m.SentAt)
            .ToListAsync();

        Assert.Equal(3, history.Count);
        Assert.Equal("First", history[0].Body);
        Assert.Equal("Second", history[1].Body);
        Assert.Equal("Third", history[2].Body);
    }

    private static async Task SeedUserAsync(AppDbContext db, Guid id, string supabaseId)
    {
        var baseUserType = db.Model.GetEntityTypes().First(e => e.GetTableName() == "base_users");
        var baseUser = Activator.CreateInstance(baseUserType.ClrType)!;

        SetProperty(baseUser, "Id", id);
        SetProperty(baseUser, "SupabaseId", supabaseId);
        SetProperty(baseUser, "Email", $"{supabaseId}@test.com");
        SetProperty(baseUser, "Username", $"user_{id.ToString()[..8]}");
        SetProperty(baseUser, "Name", "Test User");
        SetProperty(baseUser, "Location", new Point(0, 0) { SRID = 4326 });
        
        FillRequiredDefaults(baseUserType, baseUser);
        db.Add(baseUser);
        await db.SaveChangesAsync();

        var regularUserType = db.Model.GetEntityTypes().First(e => e.GetTableName() == "users");
        var regularUser = Activator.CreateInstance(regularUserType.ClrType)!;
        SetProperty(regularUser, "Id", id);
        FillRequiredDefaults(regularUserType, regularUser);
        db.Add(regularUser);
        await db.SaveChangesAsync();
    }

    private static void SetProperty(object entity, string name, object value)
    {
        entity.GetType().GetProperty(name)?.SetValue(entity, value);
    }

    private static void FillRequiredDefaults(IEntityType entityType, object entity)
    {
        foreach (var p in entityType.GetProperties())
        {
            if (p.IsNullable || p.IsPrimaryKey()) continue;
            var clrProp = entityType.ClrType.GetProperty(p.Name);
            if (clrProp == null || !clrProp.CanWrite || clrProp.GetValue(entity) != null) continue;

            var t = clrProp.PropertyType;
            if (t == typeof(string)) clrProp.SetValue(entity, "test");
            else if (t == typeof(DateTime)) clrProp.SetValue(entity, DateTime.UtcNow);
            else if (t == typeof(Guid)) clrProp.SetValue(entity, Guid.NewGuid());
            else if (t.IsEnum) clrProp.SetValue(entity, Enum.GetValues(t).GetValue(0));
            else if (t.IsValueType) clrProp.SetValue(entity, Activator.CreateInstance(t));
        }
    }
}
