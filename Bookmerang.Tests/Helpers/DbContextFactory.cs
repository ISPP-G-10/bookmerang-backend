using Bookmerang.Api.Data;
using Microsoft.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Bookmerang.Tests.Helpers;

/// <summary>
/// Factoría para crear AppDbContext con EF Core InMemory.
/// Útil para tests que no requieren PostGIS.
/// </summary>
public static class DbContextFactory
{
    public static AppDbContext CreateInMemory(string? dbName = null)
    {
        dbName ??= Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }
}
