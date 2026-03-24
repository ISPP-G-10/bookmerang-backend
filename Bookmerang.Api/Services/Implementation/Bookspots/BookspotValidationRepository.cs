using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Services.Interfaces.Bookspots;
using Microsoft.EntityFrameworkCore;

namespace Bookmerang.Api.Services.Implementation.Bookspots;

public class BookspotValidationRepository(AppDbContext db) : IBookspotValidationRepository
{
    public async Task<BookspotValidation> CreateAsync(BookspotValidation validation, CancellationToken ct = default)
    {
        db.BookspotValidations.Add(validation);
        await db.SaveChangesAsync(ct);
        return validation;
    }

    public async Task<List<BookspotValidation>> GetByBookspotIdAsync(int bookspotId, CancellationToken ct = default)
    {
        return await db.BookspotValidations
            .Where(v => v.BookspotId == bookspotId)
            .ToListAsync(ct);
    }

    public async Task<BookspotValidation?> GetByIdAsync(int validationId, CancellationToken ct = default)
    {
        return await db.BookspotValidations
            .FirstOrDefaultAsync(v => v.Id == validationId, ct);
    }

    public async Task DeleteByBookspotIdAsync(int bookspotId, CancellationToken ct = default)
    {
        var validations = await db.BookspotValidations
            .Where(v => v.BookspotId == bookspotId)
            .ToListAsync(ct);

        db.BookspotValidations.RemoveRange(validations);
        await db.SaveChangesAsync(ct);
    }

}