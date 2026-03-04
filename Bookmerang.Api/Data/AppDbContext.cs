using Bookmerang.Api.Models;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql.NameTranslation;

namespace Bookmerang.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<BaseUser> BaseUsers => Set<BaseUser>();
    public DbSet<User> Users => Set<User>();
    public DbSet<User> RegularUsers => Set<User>(); // Alias para compatibilidad con develop
    public DbSet<Models.Exchange> Exchanges => Set<Models.Exchange>();
    public DbSet<ExchangeMeeting> ExchangeMeetings => Set<ExchangeMeeting>();
    public DbSet<Chat> Chats => Set<Chat>();
    public DbSet<ChatParticipant> ChatParticipants => Set<ChatParticipant>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
    public DbSet<UserPreferenceGenre> UserPreferenceGenres => Set<UserPreferenceGenre>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<BookPhoto> BookPhotos => Set<BookPhoto>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<Language> Languages => Set<Language>();
    public DbSet<BookGenre> BookGenres => Set<BookGenre>();
    public DbSet<BookLanguage> BookLanguages => Set<BookLanguage>();
    public DbSet<Swipe> Swipes => Set<Swipe>();
    public DbSet<Match> Matches => Set<Match>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ===== ENUMS =====
        modelBuilder.HasPostgresEnum<BooksExtension>();
        modelBuilder.HasPostgresEnum<CoverType>();
        modelBuilder.HasPostgresEnum<BookCondition>();
        modelBuilder.HasPostgresEnum<BookStatus>();
        modelBuilder.HasPostgresEnum<PricingPlan>();
        modelBuilder.HasPostgresEnum<SwipeDirection>();
        modelBuilder.HasPostgresEnum<MatchStatus>();
        modelBuilder.HasPostgresEnum<ChatType>("public", "chat_type", new NpgsqlNullNameTranslator());
        modelBuilder.HasPostgresEnum<ExchangeStatus>();

        // ===== BASE USERS =====
        modelBuilder.Entity<BaseUser>(entity =>
        {
            entity.HasIndex(u => u.SupabaseId).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();
        });

        // ===== USERS =====
        // User -> BaseUser (TPT pattern: mismo UUID)
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasOne(u => u.BaseUser)
                .WithOne()
                .HasForeignKey<User>(u => u.Id);
            
            entity.Property(u => u.Plan)
                .HasColumnType("pricing_plan");

            entity.HasOne(x => x.UserPreference)
                .WithOne()
                .HasForeignKey<UserPreference>(x => x.UserId);
        });

        // ===== EXCHANGES =====
        modelBuilder.Entity<Models.Exchange>(entity =>
        {
            entity.HasIndex(e => e.SupabaseId).IsUnique();
            entity.HasIndex(e => e.ChatId).IsUnique();
        });

        // ===== EXCHANGE MEETINGS =====
        modelBuilder.Entity<ExchangeMeeting>(entity =>
        {
            entity.HasIndex(em => em.SupabaseId).IsUnique();
            entity.HasIndex(em => em.ExchangeId).IsUnique();
        });

        // ===== CHATS =====
        modelBuilder.Entity<Chat>(entity =>
        {
            entity.ToTable("chats");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Type).HasColumnName("type");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        // ===== CHAT PARTICIPANTS =====
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

        // ===== MESSAGES =====
        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasOne(m => m.Chat)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ChatId);

            entity.HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId);
        });

        // ===== USER PREFERENCES =====
        modelBuilder.Entity<UserPreference>(entity =>
        {
            entity.ToTable("user_preferences");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.Location).HasColumnName("location").HasColumnType("geography(Point,4326)");
            entity.Property(x => x.RadioKm).HasColumnName("radio_km");
            entity.Property(x => x.Extension).HasColumnName("extension");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(x => x.UserId).IsUnique();
        });

        // ===== USER PREFERENCES GENRES =====
        modelBuilder.Entity<UserPreferenceGenre>(entity =>
        {
            entity.ToTable("user_preferences_genres");
            entity.HasKey(x => new { x.PreferencesId, x.GenreId });
            entity.Property(x => x.PreferencesId).HasColumnName("preferences_id");
            entity.Property(x => x.GenreId).HasColumnName("genre_id");

            entity.HasOne(x => x.UserPreference)
                .WithMany(x => x.UserPreferenceGenres)
                .HasForeignKey(x => x.PreferencesId);

            entity.HasOne(x => x.Genre)
                .WithMany()
                .HasForeignKey(x => x.GenreId);
        });

        // ===== GENRES =====
        modelBuilder.Entity<Genre>(entity =>
        {
            entity.ToTable("genres");
            entity.Property(g => g.Id).HasColumnName("id");
            entity.Property(g => g.Name).HasColumnName("name");
            entity.HasIndex(x => x.Name).IsUnique();
        });

        // ===== BOOKS =====
        modelBuilder.Entity<Book>(entity =>
        {
            entity.ToTable("books");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.OwnerId).HasColumnName("owner_id");
            entity.Property(x => x.Isbn).HasColumnName("isbn");
            entity.Property(x => x.Titulo).HasColumnName("titulo");
            entity.Property(x => x.Autor).HasColumnName("autor");
            entity.Property(x => x.Editorial).HasColumnName("editorial");
            entity.Property(x => x.NumPaginas).HasColumnName("num_paginas");
            entity.Property(b => b.Cover).HasColumnName("cover").HasColumnType("cover_type");
            entity.Property(b => b.Condition).HasColumnName("condition").HasColumnType("book_condition");
            entity.Property(b => b.Status).HasColumnName("status").HasColumnType("book_status");
            entity.Property(x => x.Observaciones).HasColumnName("observaciones");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(b => b.Owner)
                .WithMany(u => u.Books)
                .HasForeignKey(b => b.OwnerId);
        });

        // ===== BOOK PHOTOS =====
        modelBuilder.Entity<BookPhoto>(entity =>
        {
            entity.ToTable("book_photos");
            entity.Property(p => p.Id).HasColumnName("id");
            entity.Property(p => p.BookId).HasColumnName("book_id");
            entity.Property(p => p.Url).HasColumnName("url");
            entity.Property(x => x.Orden).HasColumnName("orden").HasDefaultValue(0);

            entity.HasOne(x => x.Book)
                .WithMany(x => x.Photos)
                .HasForeignKey(x => x.BookId);
        });

        // ===== BOOK GENRES =====
        modelBuilder.Entity<BookGenre>(entity =>
        {
            entity.ToTable("books_genres");
            entity.HasKey(bg => new { bg.BookId, bg.GenreId });
            entity.Property(bg => bg.BookId).HasColumnName("book_id");
            entity.Property(bg => bg.GenreId).HasColumnName("genre_id");
            
            entity.HasOne(bg => bg.Book)
                .WithMany(b => b.BookGenres)
                .HasForeignKey(bg => bg.BookId);
            
            entity.HasOne(bg => bg.Genre)
                .WithMany(g => g.BookGenres)
                .HasForeignKey(bg => bg.GenreId);
        });

        // ===== LANGUAGES =====
        modelBuilder.Entity<Language>(entity =>
        {
            entity.ToTable("languages");
            entity.Property(l => l.Id).HasColumnName("id");
            entity.Property(l => l.LanguageName).HasColumnName("language");
            entity.HasIndex(x => x.LanguageName).IsUnique();
        });

        // ===== BOOK LANGUAGES =====
        modelBuilder.Entity<BookLanguage>(entity =>
        {
            entity.ToTable("books_languages");
            entity.HasKey(bl => new { bl.BookId, bl.LanguageId });
            entity.Property(bl => bl.BookId).HasColumnName("book_id");
            entity.Property(bl => bl.LanguageId).HasColumnName("language_id");
            
            entity.HasOne(bl => bl.Book)
                .WithMany(b => b.BookLanguages)
                .HasForeignKey(bl => bl.BookId);
            
            entity.HasOne(bl => bl.Language)
                .WithMany(l => l.BookLanguages)
                .HasForeignKey(bl => bl.LanguageId);
        });

        // ===== SWIPES =====
        modelBuilder.Entity<Swipe>(entity =>
        {
            entity.ToTable("swipes");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.SwiperId).HasColumnName("swiper_id");
            entity.Property(x => x.BookId).HasColumnName("book_id");
            entity.Property(x => x.Direction).HasColumnName("direction");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");

            entity.HasIndex(x => new { x.SwiperId, x.BookId }).IsUnique();

            entity.HasOne(x => x.Book)
                .WithMany()
                .HasForeignKey(x => x.BookId);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.SwiperId);
        });

        // ===== MATCHES =====
        modelBuilder.Entity<Match>(entity =>
        {
            entity.ToTable("matches");
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.User1Id).HasColumnName("user1_id");
            entity.Property(x => x.User2Id).HasColumnName("user2_id");
            entity.Property(x => x.Book1Id).HasColumnName("book1_id");
            entity.Property(x => x.Book2Id).HasColumnName("book2_id");
            entity.Property(x => x.Status).HasColumnName("status");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");

            entity.HasOne(x => x.Book1)
                .WithMany()
                .HasForeignKey(x => x.Book1Id)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Book2)
                .WithMany()
                .HasForeignKey(x => x.Book2Id)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.User1Id)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.User2Id)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
