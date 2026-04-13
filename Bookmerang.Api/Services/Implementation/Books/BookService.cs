using Bookmerang.Api.Data;
using Bookmerang.Api.Exceptions;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Models.DTOs.Books.Queries;
using Bookmerang.Api.Models.DTOs.Books.Requests;
using Bookmerang.Api.Models.DTOs.Books.Responses;
using Bookmerang.Api.Services.Interfaces.Books;
using Microsoft.EntityFrameworkCore;

namespace Bookmerang.Api.Services.Implementation.Books;

public class BookService(
    IBookRepository bookRepo,
    IGenreRepository genreRepo,
    ILanguageRepository languageRepo,
    AppDbContext db 
) : IBookService
{
    private const int RequiredPhotosToPublish = 1;
    private const int MaxPhotosToPublish = 1;

    // CREAR BORRADOR
    public async Task<BookDetailDTO> CreateDraftAsync(
        string supabaseId,
        CreateBookDraftRequest request,
        CancellationToken ct = default)
    {
        var ownerId = await ResolveOwnerIdAsync(supabaseId, ct);

        await ValidateGenreIdsAsync(request.GenreIds, ct);
        await ValidateLanguageIdsAsync(request.LanguageIds, ct);

        var sanitizedIsbn = NormalizeAndValidateIsbn(request.Isbn);

        var book = new Book
        {
            OwnerId = ownerId,
            Isbn = sanitizedIsbn,
            Titulo = request.Titulo?.Trim(),
            Autor = request.Autor?.Trim(),
            Editorial = request.Editorial?.Trim(),
            NumPaginas = request.NumPaginas,
            Cover = request.Cover,
            Condition = request.Condition,
            Observaciones = request.Observaciones,
            Status = BookStatus.DRAFT,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await bookRepo.CreateAsync(book, ct);

        if (request.GenreIds.Count > 0)
            await bookRepo.ReplaceGenresAsync(created.Id, request.GenreIds, ct);
        if (request.LanguageIds.Count > 0)
            await bookRepo.ReplaceLanguagesAsync(created.Id, request.LanguageIds, ct);

        var fullBook = await bookRepo.GetByIdOrThrowAsync(created.Id, ct);
        return MapToDetailDTO(fullBook);
    }

    // GESTIÓN DE FOTOS
    public async Task<BookDetailDTO> UpsertPhotosAsync(
        int bookId,
        string supabaseId,
        UpsertBookPhotosRequest request,
        CancellationToken ct = default)
    {
        var ownerId = await ResolveOwnerIdAsync(supabaseId, ct);
        var book = await bookRepo.GetByIdOrThrowAsync(bookId, ct);
        VerifyOwner(book, ownerId);

        if (request.Photos.Count < RequiredPhotosToPublish || request.Photos.Count > MaxPhotosToPublish)
            throw new ValidationException(
                $"Un libro debe tener exactamente 1 foto. Se han enviado {request.Photos.Count}.");

        if (request.Photos.Any(p => string.IsNullOrWhiteSpace(p.Url)))
            throw new ValidationException("Todas las fotos deben incluir una URL válida.");

        var newPhotos = request.Photos
            .OrderBy(p => p.Order)
            .Select((p, index) => new BookPhoto
            {
                BookId = bookId,
                Url = p.Url,
                Orden = index
            })
            .ToList();

        await bookRepo.ReplacePhotosAsync(bookId, newPhotos, ct);

        var updatedBook = await bookRepo.GetByIdOrThrowAsync(bookId, ct);
        return MapToDetailDTO(updatedBook);
    }

    // ACTUALIZAR DATOS
    public async Task<BookDetailDTO> UpdateDraftDataAsync(
        int bookId,
        string supabaseId,
        UpdateBookDataRequest request,
        CancellationToken ct = default)
    {
        var ownerId = await ResolveOwnerIdAsync(supabaseId, ct);
        var book = await bookRepo.GetByIdOrThrowAsync(bookId, ct);
        VerifyOwner(book, ownerId);

        await ValidateGenreIdsAsync(request.GenreIds, ct);
        await ValidateLanguageIdsAsync(request.LanguageIds, ct);

        book.Isbn = NormalizeAndValidateIsbn(request.Isbn);
        book.Titulo = request.Titulo?.Trim();
        book.Autor = request.Autor?.Trim();
        book.Editorial = request.Editorial?.Trim();
        book.NumPaginas = request.NumPaginas;
        book.Cover = request.Cover;

        await bookRepo.UpdateAsync(book, ct);
        await bookRepo.ReplaceGenresAsync(bookId, request.GenreIds, ct);
        await bookRepo.ReplaceLanguagesAsync(bookId, request.LanguageIds, ct);

        var updatedBook = await bookRepo.GetByIdOrThrowAsync(bookId, ct);
        return MapToDetailDTO(updatedBook);
    }

    // ACTUALIZAR DETALLES
    public async Task<BookDetailDTO> UpdateDraftDetailsAsync(
        int bookId,
        string supabaseId,
        UpdateBookDetailsRequest request,
        CancellationToken ct = default)
    {
        var ownerId = await ResolveOwnerIdAsync(supabaseId, ct);
        var book = await bookRepo.GetByIdOrThrowAsync(bookId, ct);
        VerifyOwner(book, ownerId);

        book.Condition = request.Condition;
        book.Observaciones = request.Observaciones?.Trim();

        await bookRepo.UpdateAsync(book, ct);

        var updatedBook = await bookRepo.GetByIdOrThrowAsync(bookId, ct);
        return MapToDetailDTO(updatedBook);
    }

    // PUBLICAR
    public async Task<BookDetailDTO> PublishAsync(
        int bookId,
        string supabaseId,
        CancellationToken ct = default)
    {
        var ownerId = await ResolveOwnerIdAsync(supabaseId, ct);
        var book = await bookRepo.GetByIdOrThrowAsync(bookId, ct);
        VerifyOwner(book, ownerId);

        var errors = new List<string>();
        if (book.Photos.Count < RequiredPhotosToPublish || book.Photos.Count > MaxPhotosToPublish)
            errors.Add(
                $"Debes subir exactamente 1 foto para publicar. Actualmente hay {book.Photos.Count}.");
        string? normalizedIsbn = null;
        if (string.IsNullOrWhiteSpace(book.Isbn))
            errors.Add("El ISBN es obligatorio para publicar.");
        else
        {
            try
            {
                normalizedIsbn = NormalizeAndValidateIsbn(book.Isbn);
            }
            catch (ValidationException)
            {
                errors.Add("El ISBN debe ser un ISBN-10 o ISBN-13 válido.");
            }
        }
        if (string.IsNullOrWhiteSpace(book.Titulo))
            errors.Add("El título es obligatorio para publicar.");
        if (string.IsNullOrWhiteSpace(book.Autor))
            errors.Add("El autor es obligatorio para publicar.");
        if (book.Cover is null)
            errors.Add("El tipo de tapa es obligatorio para publicar.");
        if (!book.NumPaginas.HasValue || book.NumPaginas.Value <= 0)
            errors.Add("El número de páginas es obligatorio para publicar.");
        if (book.Condition is null)
            errors.Add("La condición es obligatoria para publicar.");
        if (!book.BookGenres.Any())
            errors.Add("Debe tener al menos un género para publicar.");
        if (!book.BookLanguages.Any())
            errors.Add("Debe tener al menos un idioma para publicar.");

        if (errors.Count > 0)
            throw new ValidationException(string.Join(" ", errors));

        book.Isbn = normalizedIsbn;
        book.Status = BookStatus.PUBLISHED;
        await bookRepo.UpdateAsync(book, ct);

        var publishedBook = await bookRepo.GetByIdOrThrowAsync(bookId, ct);
        return MapToDetailDTO(publishedBook);
    }

    // OBTENER MI BIBLIOTECA
    public async Task<PagedResult<BookListItemDTO>> GetMyLibraryAsync(
        string supabaseId,
        LibraryQuery query,
        CancellationToken ct = default)
    {
        var ownerId = await ResolveOwnerIdAsync(supabaseId, ct);
        var (items, total) = await bookRepo.GetByOwnerPagedAsync(ownerId, query, ct);

        return new PagedResult<BookListItemDTO>
        {
            Items = items.Select(MapToListItemDTO).ToList(),
            Page = query.Page,
            PageSize = query.PageSize,
            Total = total
        };
    }

    // OBTENER MIS BORRADORES
    public async Task<PagedResult<BookListItemDTO>> GetMyDraftsAsync(
        string supabaseId,
        DraftsQuery query,
        CancellationToken ct = default)
    {
        var ownerId = await ResolveOwnerIdAsync(supabaseId, ct);
        var (items, total) = await bookRepo.GetDraftsPagedAsync(ownerId, query, ct);

        return new PagedResult<BookListItemDTO>
        {
            Items = items.Select(MapToListItemDTO).ToList(),
            Page = query.Page,
            PageSize = query.PageSize,
            Total = total
        };
    }

    // OBTENER DETALLE
    public async Task<BookDetailDTO> GetByIdAsync(
        int bookId,
        string supabaseId,
        CancellationToken ct = default)
    {
        var ownerId = await ResolveOwnerIdAsync(supabaseId, ct);
        var book = await bookRepo.GetByIdOrThrowAsync(bookId, ct);
        return MapToDetailDTO(book);
    }

    // SOFT DELETE
    public async Task DeleteAsync(
        int bookId,
        string supabaseId,
        CancellationToken ct = default)
    {
        var ownerId = await ResolveOwnerIdAsync(supabaseId, ct);
        var book = await bookRepo.GetByIdOrThrowAsync(bookId, ct);
        VerifyOwner(book, ownerId);

        book.Status = BookStatus.DELETED;
        await bookRepo.UpdateAsync(book, ct);
    }

    // =====================================================================
    // MÉTODOS PRIVADOS DE APOYO
    // =====================================================================

    /// Lanza NotFoundException si el usuario no existe.
    private async Task<Guid> ResolveOwnerIdAsync(string supabaseId, CancellationToken ct)
    {
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.SupabaseId == supabaseId, ct);

        if (user is null)
            throw new NotFoundException(
                $"No se encontró ningún usuario con supabaseId '{supabaseId}'. ¿Has llamado a /api/auth/register?");

        return user.Id;
    }

    private static void VerifyOwner(Book book, Guid ownerId)
    {
        if (book.OwnerId != ownerId)
            throw new ForbiddenException(
                "No tienes permiso para realizar esta acción sobre este libro.");
    }

    private async Task ValidateGenreIdsAsync(List<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return;
        if (!await genreRepo.AllExistAsync(ids, ct))
            throw new ValidationException("Uno o más géneros no existen.");
    }

    private async Task ValidateLanguageIdsAsync(List<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return;
        if (!await languageRepo.AllExistAsync(ids, ct))
            throw new ValidationException("Uno o más idiomas no existen.");
    }

    private static string? NormalizeAndValidateIsbn(string? rawIsbn)
    {
        if (string.IsNullOrWhiteSpace(rawIsbn))
            return null;

        var normalizedChars = rawIsbn
            .Where(character => !char.IsWhiteSpace(character) && character != '-')
            .Select(char.ToUpperInvariant)
            .ToArray();

        var normalizedIsbn = new string(normalizedChars);
        if (IsValidIsbn10(normalizedIsbn) || IsValidIsbn13(normalizedIsbn))
            return normalizedIsbn;

        throw new ValidationException("El ISBN debe ser un ISBN-10 o ISBN-13 válido.");
    }

    private static bool IsValidIsbn10(string isbn)
    {
        if (isbn.Length != 10)
            return false;

        var checksum = 0;
        for (var index = 0; index < isbn.Length; index++)
        {
            var character = isbn[index];
            if (index < 9 && !char.IsDigit(character))
                return false;

            var digit = character switch
            {
                >= '0' and <= '9' => character - '0',
                'X' when index == 9 => 10,
                _ => -1
            };

            if (digit < 0)
                return false;

            checksum += digit * (10 - index);
        }

        return checksum % 11 == 0;
    }

    private static bool IsValidIsbn13(string isbn)
    {
        if (isbn.Length != 13 || !isbn.All(char.IsDigit))
            return false;

        if (!isbn.StartsWith("978") && !isbn.StartsWith("979"))
            return false;

        var checksumBase = 0;
        for (var index = 0; index < 12; index++)
        {
            var digit = isbn[index] - '0';
            checksumBase += digit * (index % 2 == 0 ? 1 : 3);
        }

        var checksum = (10 - (checksumBase % 10)) % 10;
        return checksum == isbn[12] - '0';
    }

    // =====================================================================
    // MAPPERS
    // =====================================================================

    private static BookDetailDTO MapToDetailDTO(Book book) => new()
    {
        Id = book.Id,
        OwnerId = book.OwnerId,
        Isbn = book.Isbn,
        Titulo = book.Titulo,
        Autor = book.Autor,
        Editorial = book.Editorial,
        NumPaginas = book.NumPaginas,
        Cover = book.Cover,
        Condition = book.Condition,
        Observaciones = book.Observaciones,
        Status = book.Status,
        CreatedAt = book.CreatedAt,
        UpdatedAt = book.UpdatedAt,
        Photos = book.Photos
            .OrderBy(p => p.Orden)
            .Select(p => new BookPhotoDTO { Url = p.Url, Order = p.Orden })
            .ToList(),
        Genres = book.BookGenres
            .Select(bg => bg.Genre.Name)
            .OrderBy(n => n)
            .ToList(),
        Languages = book.BookLanguages
            .Select(bl => bl.Language.LanguageName)
            .OrderBy(n => n)
            .ToList()
    };

    private static BookListItemDTO MapToListItemDTO(Book book) => new()
    {
        Id = book.Id,
        Titulo = book.Titulo,
        Autor = book.Autor,
        Status = book.Status,
        Cover = book.Cover,
        Condition = book.Condition,
        ThumbnailUrl = book.Photos.OrderBy(p => p.Orden).FirstOrDefault()?.Url,
        UpdatedAt = book.UpdatedAt
    };
}
