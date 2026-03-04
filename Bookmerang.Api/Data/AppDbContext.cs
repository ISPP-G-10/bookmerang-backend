using Bookmerang.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bookmerang.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<BaseUser> BaseUsers => Set<BaseUser>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<BookPhoto> BookPhotos => Set<BookPhoto>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<Language> Languages => Set<Language>();
    public DbSet<BookGenre> BookGenres => Set<BookGenre>();
    public DbSet<BookLanguage> BookLanguages => Set<BookLanguage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // BaseUser configuration
        modelBuilder.Entity<BaseUser>(entity =>
        {
            entity.HasIndex(u => u.SupabaseId).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();
        });

        // User -> BaseUser (TPT pattern: mismo UUID)
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasOne(u => u.BaseUser)
                .WithOne()
                .HasForeignKey<User>(u => u.Id);
            
            entity.Property(u => u.Plan)
                .HasColumnType("pricing_plan");
        });

        // Book configuration - usar tipos enum nativos de PostgreSQL
        modelBuilder.Entity<Book>(entity =>
        {
            entity.Property(b => b.Cover)
                .HasColumnType("cover_type");
            entity.Property(b => b.Condition)
                .HasColumnType("book_condition");
            entity.Property(b => b.Status)
                .HasColumnType("book_status");
        });

        // BookPhoto configuration
        modelBuilder.Entity<BookPhoto>(entity =>
        {
            entity.ToTable("book_photos");
            entity.Property(p => p.Id).HasColumnName("id");
            entity.Property(p => p.BookId).HasColumnName("book_id");
            entity.Property(p => p.Url).HasColumnName("url");
            entity.Property(p => p.Orden).HasColumnName("orden");
        });

        // Genre configuration
        modelBuilder.Entity<Genre>(entity =>
        {
            entity.ToTable("genres");
            entity.Property(g => g.Id).HasColumnName("id");
            entity.Property(g => g.Name).HasColumnName("name");
        });

        // Language configuration
        modelBuilder.Entity<Language>(entity =>
        {
            entity.ToTable("languages");
            entity.Property(l => l.Id).HasColumnName("id");
            entity.Property(l => l.LanguageName).HasColumnName("language");
        });

        // BookGenre (M:N join table) configuration
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

        // BookLanguage (M:N join table) configuration
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
    }
}
