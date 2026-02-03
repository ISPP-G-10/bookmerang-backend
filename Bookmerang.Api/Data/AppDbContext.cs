using Microsoft.EntityFrameworkCore;

namespace Bookmerang.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // public DbSet<YourEntity> Entities => Set<YourEntity>();
}
