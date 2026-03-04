using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Services.Interfaces.Books;
using Microsoft.EntityFrameworkCore;

namespace Bookmerang.Api.Services.Implementation.Books;

public class LanguageRepository(AppDbContext db) : ILanguageRepository
{
    public async Task<List<Language>> GetAllAsync(CancellationToken ct = default)
    {
        return await db.Languages
            .OrderBy(l => l.LanguageName)
            .ToListAsync(ct);
    }

    public async Task<bool> AllExistAsync(List<int> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return true;

        var existingCount = await db.Languages
            .Where(l => ids.Contains(l.Id))
            .CountAsync(ct);

        return existingCount == ids.Count;
    }
}