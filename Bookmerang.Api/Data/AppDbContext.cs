using Bookmerang.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Bookmerang.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<BaseUser> Users => Set<BaseUser>();
    public DbSet<Exchange> Exchanges => Set<Exchange>();
    public DbSet<ExchangeMeeting> ExchangeMeetings => Set<ExchangeMeeting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
    }
}
