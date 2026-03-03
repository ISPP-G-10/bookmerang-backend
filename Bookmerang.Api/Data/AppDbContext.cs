using Bookmerang.Api.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql.NameTranslation;

namespace Bookmerang.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<BaseUser> Users => Set<BaseUser>();
    public DbSet<Exchange> Exchanges => Set<Exchange>();
    public DbSet<ExchangeMeeting> ExchangeMeetings => Set<ExchangeMeeting>();
    public DbSet<User> RegularUsers => Set<User>();
    public DbSet<Chat> Chats => Set<Chat>();
    public DbSet<ChatParticipant> ChatParticipants => Set<ChatParticipant>();
    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresEnum<ChatType>("public", "chat_type", new NpgsqlNullNameTranslator());

        modelBuilder.Entity<BaseUser>(entity =>
        {
            entity.HasIndex(u => u.SupabaseId).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<Exchange>(entity =>
        {
            entity.HasIndex(e => e.SupabaseId).IsUnique();
            entity.HasIndex(e => e.ChatId).IsUnique();
        });

        modelBuilder.Entity<ExchangeMeeting>(entity =>
        {
            entity.HasIndex(em => em.SupabaseId).IsUnique();
            entity.HasIndex(em => em.ExchangeId).IsUnique();
        });

        // Chat participants: clave compuesta
        modelBuilder.Entity<ChatParticipant>(entity =>
        {
            entity.HasKey(cp => new { cp.ChatId, cp.UserId });

            entity.HasOne(cp => cp.Chat)
                .WithMany(c => c.Participants)
                .HasForeignKey(cp => cp.ChatId);

            entity.HasOne(cp => cp.User)
                .WithMany()
                .HasForeignKey(cp => cp.UserId);
        });

        // Messages
        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasOne(m => m.Chat)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ChatId);

            entity.HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId);
        });
    }
}
