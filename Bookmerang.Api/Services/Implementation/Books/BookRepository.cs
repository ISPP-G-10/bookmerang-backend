using Bookmerang.Api.Data;
using Bookmerang.Api.Models;
using Bookmerang.Api.Models.Books;
using Bookmerang.Api.Models.Books.Enums;
using Bookmerang.Api.Models.DTOs.Books.Queries;
using Bookmerang.Api.Services.Interfaces.Books;
using Microsoft.EntityFrameworkCore;

namespace Bookmerang.Api.Services.Implementation.Books;

public class BookRepository(AppDbContext db) : IBookRepository
{
    public async Task<Book?> GetByIdAsync(int bookId, CancellationToken ct = default)
    {
        return await db.Books
            .Include(b => b.Photos)
            .Include(b => b.BookGenres)
                .ThenInclude(bg => bg.Genre)
            .Include(b => b.BookLanguages)
                .ThenInclude(bl => bl.Language)
            .FirstOrDefaultAsync(b => b.Id == bookId, ct);
    }

    public async Task<(List<Book> Items, int Total)> GetByOwnerPagedAsync(
        Guid ownerId,
        LibraryQuery query,
        CancellationToken ct = default)
    {
        var q = db.Books
            .Where(b => b.OwnerId == ownerId)
            .Where(b => b.Status != BookStatus.Deleted)
            .AsQueryable();

        if (query.Status.HasValue)
            q = q.Where(b => b.Status == query.Status.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.ToLower();
            q = q.Where(b =>
                (b.Titulo != null && b.Titulo.ToLower().Contains(search)) ||
                (b.Autor != null && b.Autor.ToLower().Contains(search)));
        }

        var total = await q.CountAsync(ct);

        // Solo fotos para el thumbnail (no géneros/idiomas en lista)
        var items = await q
            .OrderByDescending(b => b.UpdatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(b => b.Photos)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<(List<Book> Items, int Total)> GetDraftsPagedAsync(
        Guid ownerId,
        DraftsQuery query,
        CancellationToken ct = default)
    {
        var q = db.Books
            .Where(b => b.OwnerId == ownerId)
            // Borradores: siempre status DRAFT
            .Where(b => b.Status == BookStatus.Draft)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.ToLower();
            q = q.Where(b =>
                (b.Titulo != null && b.Titulo.ToLower().Contains(search)) ||
                (b.Autor != null && b.Autor.ToLower().Contains(search)));
        }

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(b => b.UpdatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(b => b.Photos)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<Book> CreateAsync(Book book, CancellationToken ct = default)
    {
        db.Books.Add(book);
        await db.SaveChangesAsync(ct);
        return book;
    }

    public async Task UpdateAsync(Book book, CancellationToken ct = default)
    {
        book.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task ReplacePhotosAsync(
        int bookId,
        List<BookPhoto> newPhotos,
        CancellationToken ct = default)
    {
        // Estrategia REPLACE:
        // 1) Cargar fotos actuales
        var existing = await db.BookPhotos
            .Where(p => p.BookId == bookId)
            .ToListAsync(ct);

        // 2) Borrar todas las actuales
        db.BookPhotos.RemoveRange(existing);

        // 3) Insertar las nuevas
        // (si newPhotos está vacío, solo se borran las actuales)
        if (newPhotos.Count > 0)
            await db.BookPhotos.AddRangeAsync(newPhotos, ct);

        await db.SaveChangesAsync(ct);
    }

    public async Task ReplaceGenresAsync(
        int bookId,
        List<int> genreIds,
        CancellationToken ct = default)
    {
        var existing = await db.BookGenres
            .Where(bg => bg.BookId == bookId)
            .ToListAsync(ct);

        db.BookGenres.RemoveRange(existing);

        if (genreIds.Count > 0)
        {
            var newGenres = genreIds.Select(gId => new BookGenre
            {
                BookId = bookId,
                GenreId = gId
            });
            await db.BookGenres.AddRangeAsync(newGenres, ct);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task ReplaceLanguagesAsync(
        int bookId,
        List<int> languageIds,
        CancellationToken ct = default)
    {
        var existing = await db.BookLanguages
            .Where(bl => bl.BookId == bookId)
            .ToListAsync(ct);

        db.BookLanguages.RemoveRange(existing);

        if (languageIds.Count > 0)
        {
            var newLanguages = languageIds.Select(lId => new BookLanguage
            {
                BookId = bookId,
                LanguageId = lId
            });
            await db.BookLanguages.AddRangeAsync(newLanguages, ct);
        }

        await db.SaveChangesAsync(ct);
    }
}