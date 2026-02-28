using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Books;
using Bookmerang.Api.Services.Interfaces.Books;
using Microsoft.EntityFrameworkCore;

namespace Bookmerang.Api.Services.Implementation.Books;

public class GenreRepository(AppDbContext db) : IGenreRepository
{
    public async Task<List<Genre>> GetAllAsync(CancellationToken ct = default)
    {
        return await db.Genres
            .OrderBy(g => g.Name)
            .ToListAsync(ct);
    }

    public async Task<bool> AllExistAsync(List<int> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return true;

        var existingCount = await db.Genres
            .Where(g => ids.Contains(g.Id))
            .CountAsync(ct);

        return existingCount == ids.Count;
    }
}