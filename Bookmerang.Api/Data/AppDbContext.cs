using Bookmerang.Api.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql.NameTranslation;

namespace Bookmerang.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<BaseUser> Users => Set<BaseUser>();
    public DbSet<User> RegularUsers => Set<User>();
    public DbSet<Chat> Chats => Set<Chat>();
    public DbSet<ChatParticipant> ChatParticipants => Set<ChatParticipant>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<UserPreferencesGenre> UserPreferencesGenres => Set<UserPreferencesGenre>();
    public DbSet<UserProgress> UserProgresses => Set<UserProgress>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresEnum<ChatType>("public", "chat_type", new NpgsqlNullNameTranslator());

        modelBuilder.Entity<BaseUser>(entity =>
        {
            entity.HasIndex(u => u.SupabaseId).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<UserPreferencesGenre>(entity =>
        {
            entity.HasKey(e => new { e.PreferencesId, e.GenreId });

            entity.HasOne<UserPreference>()
                .WithMany()
                .HasForeignKey(e => e.PreferencesId);

            entity.HasOne<Genre>()
                .WithMany()
                .HasForeignKey(e => e.GenreId);
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
