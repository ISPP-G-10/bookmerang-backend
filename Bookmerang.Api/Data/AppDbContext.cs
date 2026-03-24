using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
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
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
    public DbSet<UserPreferenceGenre> UserPreferenceGenres => Set<UserPreferenceGenre>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<UserProgress> UserProgresses => Set<UserProgress>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<BookPhoto> BookPhotos => Set<BookPhoto>();
    public DbSet<BookGenre> BookGenres => Set<BookGenre>();
    public DbSet<Language> Languages => Set<Language>();
    public DbSet<BookLanguage> BookLanguages => Set<BookLanguage>();
    public DbSet<Swipe> Swipes => Set<Swipe>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<TypingIndicator> TypingIndicators => Set<TypingIndicator>();
    public DbSet<Bookspot> Bookspots => Set<Bookspot>();
    public DbSet<BookspotValidation> BookspotValidations => Set<BookspotValidation>();
    public DbSet<Community> Communities => Set<Community>();
    public DbSet<CommunityMember> CommunityMembers => Set<CommunityMember>();
    public DbSet<CommunityChat> CommunityChats => Set<CommunityChat>();
    public DbSet<CommunityLibraryLike> CommunityLibraryLikes => Set<CommunityLibraryLike>();
    public DbSet<Meetup> Meetups => Set<Meetup>();
    public DbSet<MeetupAttendance> MeetupAttendances => Set<MeetupAttendance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        // ===== ENUMS =====
        modelBuilder.HasPostgresEnum<BooksExtension>();
        modelBuilder.HasPostgresEnum<CoverType>();
        modelBuilder.HasPostgresEnum<BookCondition>();
        modelBuilder.HasPostgresEnum<BookStatus>();
        modelBuilder.HasPostgresEnum<SwipeDirection>();
        modelBuilder.HasPostgresEnum<MatchStatus>();
        modelBuilder.HasPostgresEnum<ChatType>("public", "chat_type", new NpgsqlNullNameTranslator());
        modelBuilder.HasPostgresEnum<ExchangeStatus>();
        modelBuilder.HasPostgresEnum<BookspotStatus>();
        modelBuilder.HasPostgresEnum<CommunityStatus>();
        modelBuilder.HasPostgresEnum<CommunityRole>();
        modelBuilder.HasPostgresEnum<MeetupStatus>();
        modelBuilder.HasPostgresEnum<MeetupAttendanceStatus>();
        modelBuilder.HasPostgresEnum<BookspotStatus>();
        modelBuilder.HasPostgresEnum<PricingPlan>();

        modelBuilder.Entity<BaseUser>(entity =>
        {
            entity.HasIndex(u => u.SupabaseId).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<Exchange>(entity =>
        {
            entity.HasIndex(e => e.ChatId).IsUnique();
        });

        modelBuilder.Entity<ExchangeMeeting>(entity =>
        {
            entity.HasIndex(em => em.ExchangeId).IsUnique();
        });

        modelBuilder.Entity<CommunityMember>(entity =>
        {
            entity.HasKey(cm => new { cm.CommunityId, cm.UserId });
        });

        modelBuilder.Entity<CommunityChat>(entity =>
        {
            entity.HasKey(cc => new { cc.CommunityId, cc.ChatId });
        });

        modelBuilder.Entity<CommunityLibraryLike>(entity =>
        {
            entity.HasKey(cl => new { cl.CommunityId, cl.UserId, cl.BookId });
        });

        modelBuilder.Entity<MeetupAttendance>(entity =>
        {
            entity.HasKey(ma => new { ma.MeetupId, ma.UserId });
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

        // ===== USERS =====
        modelBuilder.Entity<User>(e =>
        {
            e.HasOne(x => x.UserPreference)
                .WithOne()
                .HasForeignKey<UserPreference>(x => x.UserId);
        });

        // User → UserProgress (1:1)
        modelBuilder.Entity<UserProgress>()
            .HasOne<User>()
            .WithOne()
            .HasForeignKey<UserProgress>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // ===== USER_PREFERENCES =====
        modelBuilder.Entity<UserPreference>(e =>
        {
            e.ToTable("user_preferences");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.Location).HasColumnName("location").HasColumnType("geography(Point,4326)");
            e.Property(x => x.RadioKm).HasColumnName("radio_km");
            e.Property(x => x.Extension).HasColumnName("extension");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            e.HasIndex(x => x.UserId).IsUnique();
        });

        // ===== USER_PREFERENCES_GENRES =====
        modelBuilder.Entity<UserPreferenceGenre>(e =>
        {
            e.ToTable("user_preferences_genres");
            e.HasKey(x => new { x.PreferencesId, x.GenreId });
            e.Property(x => x.PreferencesId).HasColumnName("preferences_id");
            e.Property(x => x.GenreId).HasColumnName("genre_id");

            e.HasOne(x => x.UserPreference)
                .WithMany(x => x.UserPreferenceGenres)
                .HasForeignKey(x => x.PreferencesId);

            e.HasOne(x => x.Genre)
                .WithMany()
                .HasForeignKey(x => x.GenreId);
        });

        // ===== GENRES =====
        modelBuilder.Entity<Genre>(e =>
        {
            e.ToTable("genres");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name");
            e.HasIndex(x => x.Name).IsUnique();
        });

        // ===== BOOKS =====
        modelBuilder.Entity<Book>(e =>
        {
            e.ToTable("books");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.OwnerId).HasColumnName("owner_id");
            e.Property(x => x.Isbn).HasColumnName("isbn");
            e.Property(x => x.Titulo).HasColumnName("titulo");
            e.Property(x => x.Autor).HasColumnName("autor");
            e.Property(x => x.Editorial).HasColumnName("editorial");
            e.Property(x => x.NumPaginas).HasColumnName("num_paginas");
            e.Property(x => x.Cover).HasColumnName("cover");
            e.Property(x => x.Condition).HasColumnName("condition");
            e.Property(x => x.Observaciones).HasColumnName("observaciones");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            e.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.OwnerId);
        });

        // ===== BOOK_PHOTOS =====
        modelBuilder.Entity<BookPhoto>(e =>
        {
            e.ToTable("book_photos");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.BookId).HasColumnName("book_id");
            e.Property(x => x.Url).HasColumnName("url");
            e.Property(x => x.Orden).HasColumnName("orden").HasDefaultValue(0);

            e.HasOne(x => x.Book)
                .WithMany(x => x.Photos)
                .HasForeignKey(x => x.BookId);
        });

        // ===== BOOKS_GENRES =====
        modelBuilder.Entity<BookGenre>(e =>
        {
            e.ToTable("books_genres");
            e.HasKey(x => new { x.BookId, x.GenreId });
            e.Property(x => x.BookId).HasColumnName("book_id");
            e.Property(x => x.GenreId).HasColumnName("genre_id");

            e.HasOne(x => x.Book)
                .WithMany(x => x.BookGenres)
                .HasForeignKey(x => x.BookId);

            e.HasOne(x => x.Genre)
                .WithMany(x => x.BookGenres)
                .HasForeignKey(x => x.GenreId);
        });

        // ===== LANGUAGES =====
        modelBuilder.Entity<Language>(e =>
        {
            e.ToTable("languages");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.LanguageName).HasColumnName("language");
            e.HasIndex(x => x.LanguageName).IsUnique();
        });

        // ===== BOOKS_LANGUAGES =====
        modelBuilder.Entity<BookLanguage>(e =>
        {
            e.ToTable("books_languages");
            e.HasKey(x => new { x.BookId, x.LanguageId });
            e.Property(x => x.BookId).HasColumnName("book_id");
            e.Property(x => x.LanguageId).HasColumnName("language_id");

            e.HasOne(x => x.Book)
                .WithMany(x => x.BookLanguages)
                .HasForeignKey(x => x.BookId);

            e.HasOne(x => x.Language)
                .WithMany(x => x.BookLanguages)
                .HasForeignKey(x => x.LanguageId);
        });

        // ===== SWIPES =====
        modelBuilder.Entity<Swipe>(e =>
        {
            e.ToTable("swipes");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.SwiperId).HasColumnName("swiper_id");
            e.Property(x => x.BookId).HasColumnName("book_id");
            e.Property(x => x.Direction).HasColumnName("direction");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");

            e.HasIndex(x => new { x.SwiperId, x.BookId }).IsUnique();

            e.HasOne(x => x.Book)
                .WithMany()
                .HasForeignKey(x => x.BookId);

            e.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.SwiperId);
        });

        // ===== MATCHES =====
        modelBuilder.Entity<Match>(e =>
        {
            e.ToTable("matches");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.User1Id).HasColumnName("user1_id");
            e.Property(x => x.User2Id).HasColumnName("user2_id");
            e.Property(x => x.Book1Id).HasColumnName("book1_id");
            e.Property(x => x.Book2Id).HasColumnName("book2_id");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");

            e.HasIndex(x => new { x.User1Id, x.User2Id }).IsUnique();

            e.HasOne(x => x.Book1)
                .WithMany()
                .HasForeignKey(x => x.Book1Id)
                .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(x => x.Book2)
                .WithMany()
                .HasForeignKey(x => x.Book2Id)
                .OnDelete(DeleteBehavior.NoAction);

            e.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.User1Id)
                .OnDelete(DeleteBehavior.NoAction);

            e.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.User2Id)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // ===== CHATS =====
        modelBuilder.Entity<Chat>(e =>
        {
            e.ToTable("chats");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Type).HasColumnName("type");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        // ===== EXCHANGES =====
        modelBuilder.Entity<Exchange>(e =>
        {
            e.ToTable("exchanges");
            e.Property(x => x.ExchangeId).HasColumnName("id");
            e.Property(x => x.ChatId).HasColumnName("chat_id");
            e.Property(x => x.MatchId).HasColumnName("match_id");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            e.HasIndex(x => x.ChatId).IsUnique();
        });

        modelBuilder.Entity<ExchangeMeeting>(e =>
        {
            e.ToTable("exchange_meetings");
            e.Property(x => x.ExchangeMeetingId).HasColumnName("id");
            e.Property(x => x.ExchangeId).HasColumnName("exchange_id");
            e.Property(x => x.ExchangeMode).HasColumnName("mode");
            e.Property(x => x.BookspotId).HasColumnName("bookspot_id");
            e.Property(x => x.CustomLocation).HasColumnName("custom_location");
            e.Property(x => x.ScheduledAt).HasColumnName("scheduled_at");
            e.Property(x => x.ProposerId).HasColumnName("proposer_id");
            e.Property(x => x.MeetingStatus).HasColumnName("status");
            e.Property(x => x.MarkAsCompletedByUser1).HasColumnName("mark_as_completed_by_user1");
            e.Property(x => x.MarkAsCompletedByUser2).HasColumnName("mark_as_completed_by_user2");

            e.HasIndex(x => x.ExchangeId).IsUnique();
        });

        modelBuilder.Entity<TypingIndicator>(e =>
        {
            e.ToTable("typing_indicators");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ChatId).HasColumnName("chat_id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.StartedAt).HasColumnName("started_at");

            e.HasIndex(x => new { x.ChatId, x.UserId }).IsUnique();

            e.HasOne(x => x.Chat)
                .WithMany()
                .HasForeignKey(x => x.ChatId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ===== BOOKSPOTS =====
        modelBuilder.Entity<Bookspot>(e =>
        {
            e.ToTable("bookspots");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Nombre).HasColumnName("nombre");
            e.Property(x => x.AddressText).HasColumnName("address_text");
            e.Property(x => x.Location).HasColumnName("location").HasColumnType("geography(Point,4326)");
            e.Property(x => x.IsBookdrop).HasColumnName("is_bookdrop");
            e.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
            e.Property(x => x.OwnerId).HasColumnName("owner_id");
            e.Property(x => x.Status).HasColumnName("status");
        // ===== COMMUNITIES =====
        modelBuilder.Entity<Community>(e =>
        {
            e.ToTable("communities");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.ReferenceBookspotId).HasColumnName("reference_bookspot_id");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.CreatorId).HasColumnName("creator_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<CommunityMember>(e =>
        {
            e.ToTable("community_members");
            e.Property(x => x.CommunityId).HasColumnName("community_id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.Role).HasColumnName("role");
            e.Property(x => x.JoinedAt).HasColumnName("joined_at");
        });

        modelBuilder.Entity<CommunityChat>(e =>
        {
            e.ToTable("community_chats");
            e.Property(x => x.CommunityId).HasColumnName("community_id");
            e.Property(x => x.ChatId).HasColumnName("chat_id");
        });

        modelBuilder.Entity<CommunityLibraryLike>(e =>
        {
            e.ToTable("community_library_likes");
            e.Property(x => x.CommunityId).HasColumnName("community_id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.BookId).HasColumnName("book_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<Meetup>(e =>
        {
            e.ToTable("meetups");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.CommunityId).HasColumnName("community_id");
            e.Property(x => x.Title).HasColumnName("title");
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.OtherBookSpotId).HasColumnName("other_book_spot_id");
            e.Property(x => x.OtherLocation).HasColumnName("other_location").HasColumnType("geography(Point,4326)");
            e.Property(x => x.ScheduledAt).HasColumnName("scheduled_at");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.CreatorId).HasColumnName("creator_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        // ===== BOOKSPOT_VALIDATIONS =====
        modelBuilder.Entity<BookspotValidation>(e =>
        {
            e.ToTable("bookspot_validations");
        modelBuilder.Entity<MeetupAttendance>(e =>
        {
            e.ToTable("meetup_attendance");
            e.Property(x => x.MeetupId).HasColumnName("meetup_id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.SelectedBookId).HasColumnName("selected_book_id");
            e.Property(x => x.Status).HasColumnName("status");
        });
    }
}
