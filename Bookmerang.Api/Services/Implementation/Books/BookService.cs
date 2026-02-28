using Bookmerang.Api.Exceptions;
using Bookmerang.Api.Models;
using Bookmerang.Api.Models.Books;
using Bookmerang.Api.Models.Books.Enums;
using Bookmerang.Api.Models.DTOs.Books.Queries;
using Bookmerang.Api.Models.DTOs.Books.Requests;
using Bookmerang.Api.Models.DTOs.Books.Responses;
using Bookmerang.Api.Services.Interfaces.Books;

namespace Bookmerang.Api.Services.Implementation.Books;

public class BookService(
    IBookRepository bookRepo,
    IGenreRepository genreRepo,
    ILanguageRepository languageRepo,
    IMatcherNotifier matcher
) : IBookService
{
    // CREAR BORRADOR
    public async Task<BookDetailDTO> CreateDraftAsync(
        Guid ownerId,
        CreateBookDraftRequest request,
        CancellationToken ct = default)
    {
        await ValidateGenreIdsAsync(request.GenreIds, ct);
        await ValidateLanguageIdsAsync(request.LanguageIds, ct);

        var book = new Book
        {
            OwnerId = ownerId,
            Isbn = request.Isbn,
            Titulo = request.Titulo,
            Autor = request.Autor,
            Editorial = request.Editorial,
            NumPaginas = request.NumPaginas,
            Cover = request.Cover,
            Condition = request.Condition,
            Observaciones = request.Observaciones,
            Status = BookStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await bookRepo.CreateAsync(book, ct);

        if (request.GenreIds.Count > 0)
            await bookRepo.ReplaceGenresAsync(created.Id, request.GenreIds, ct);
        if (request.LanguageIds.Count > 0)
            await bookRepo.ReplaceLanguagesAsync(created.Id, request.LanguageIds, ct);

        var fullBook = await GetBookOrThrowAsync(created.Id, ct);
        return MapToDetailDTO(fullBook);
    }

    // GESTIÓN DE FOTOS
    public async Task<BookDetailDTO> UpsertPhotosAsync(
        int bookId,
        Guid ownerId,
        UpsertBookPhotosRequest request,
        CancellationToken ct = default)
    {
        var book = await GetBookOrThrowAsync(bookId, ct);
        VerifyOwner(book, ownerId);

        // Máximo 5 fotos por libro
        if (request.Photos.Count > 5)
            throw new ValidationException(
                $"Un libro puede tener máximo 5 fotos. Se han enviado {request.Photos.Count}.");

        // Normalizar el orden (0,1,2,3,4) independientemente de lo que mande el frontend
        // Esto evita huecos o valores inesperados en el orden
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

        var updatedBook = await GetBookOrThrowAsync(bookId, ct);
        return MapToDetailDTO(updatedBook);
    }

    // ACTUALIZAR DATOS (PASO 2)
    public async Task<BookDetailDTO> UpdateDraftDataAsync(
        int bookId,
        Guid ownerId,
        UpdateBookDataRequest request,
        CancellationToken ct = default)
    {
        var book = await GetBookOrThrowAsync(bookId, ct);
        VerifyOwner(book, ownerId);

        await ValidateGenreIdsAsync(request.GenreIds, ct);
        await ValidateLanguageIdsAsync(request.LanguageIds, ct);

        book.Isbn = request.Isbn;
        book.Titulo = request.Titulo;
        book.Autor = request.Autor;
        book.Editorial = request.Editorial;
        book.NumPaginas = request.NumPaginas;
        book.Cover = request.Cover;

        await bookRepo.UpdateAsync(book, ct);
        await bookRepo.ReplaceGenresAsync(bookId, request.GenreIds, ct);
        await bookRepo.ReplaceLanguagesAsync(bookId, request.LanguageIds, ct);

        var updatedBook = await GetBookOrThrowAsync(bookId, ct);
        return MapToDetailDTO(updatedBook);
    }

    // ACTUALIZAR DETALLES (PASO 3)
    public async Task<BookDetailDTO> UpdateDraftDetailsAsync(
        int bookId,
        Guid ownerId,
        UpdateBookDetailsRequest request,
        CancellationToken ct = default)
    {
        var book = await GetBookOrThrowAsync(bookId, ct);
        VerifyOwner(book, ownerId);

        book.Condition = request.Condition;
        book.Observaciones = request.Observaciones;

        await bookRepo.UpdateAsync(book, ct);

        var updatedBook = await GetBookOrThrowAsync(bookId, ct);
        return MapToDetailDTO(updatedBook);
    }

    // PUBLICAR
    public async Task<BookDetailDTO> PublishAsync(
        int bookId,
        Guid ownerId,
        CancellationToken ct = default)
    {
        var book = await GetBookOrThrowAsync(bookId, ct);
        VerifyOwner(book, ownerId);

        // Validaciones obligatorias para publicar
        // Un libro publicado debe tener al menos estos campos
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(book.Titulo))
            errors.Add("El título es obligatorio para publicar.");
        if (string.IsNullOrWhiteSpace(book.Autor))
            errors.Add("El autor es obligatorio para publicar.");
        if (book.Condition is null)
            errors.Add("La condición es obligatoria para publicar.");
        if (!book.BookGenres.Any())
            errors.Add("Debe tener al menos un género para publicar.");
        if (!book.BookLanguages.Any())
            errors.Add("Debe tener al menos un idioma para publicar.");

        // Agrupar errores en un mismo mensaje
        if (errors.Count > 0)
            throw new ValidationException(string.Join(" ", errors));

        book.Status = BookStatus.Published;
        await bookRepo.UpdateAsync(book, ct);

        // Cuando el módulo matcher esté listo, este notifier
        // hará la llamada real. Por ahora DummyMatcherNotifier no hace nada.
        await matcher.OnBookPublishedAsync(bookId, ownerId, ct);

        var publishedBook = await GetBookOrThrowAsync(bookId, ct);
        return MapToDetailDTO(publishedBook);
    }

    // OBTENER MI BIBLIOTECA
    public async Task<PagedResult<BookListItemDTO>> GetMyLibraryAsync(
        Guid ownerId,
        LibraryQuery query,
        CancellationToken ct = default)
    {
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
        Guid ownerId,
        DraftsQuery query,
        CancellationToken ct = default)
    {
        var (items, total) = await bookRepo.GetDraftsPagedAsync(ownerId, query, ct);

        return new PagedResult<BookListItemDTO>
        {
            Items = items.Select(MapToListItemDTO).ToList(),
            Page = query.Page,
            PageSize = query.PageSize,
            Total = total
        };
    }

    // OBTENER DETALLES DE UN LIBRO
    public async Task<BookDetailDTO> GetByIdAsync(
        int bookId,
        Guid ownerId,
        CancellationToken ct = default)
    {
        var book = await GetBookOrThrowAsync(bookId, ct);
        VerifyOwner(book, ownerId);
        return MapToDetailDTO(book);
    }

    // SOFT DELETE
    public async Task DeleteAsync(
        int bookId,
        Guid ownerId,
        CancellationToken ct = default)
    {
        var book = await GetBookOrThrowAsync(bookId, ct);
        VerifyOwner(book, ownerId);

        book.Status = BookStatus.Deleted;
        await bookRepo.UpdateAsync(book, ct);
    }

    // =====================================================================
    // MÉTODOS PRIVADOS DE APOYO
    // =====================================================================

    /// Carga el libro con todas sus relaciones o lanza NotFoundException.
    private async Task<Book> GetBookOrThrowAsync(int bookId, CancellationToken ct)
    {
        var book = await bookRepo.GetByIdAsync(bookId, ct);
        if (book is null)
            throw new NotFoundException($"Libro con id {bookId} no encontrado.");
        return book;
    }

    /// Verifica que el usuario es el propietario del libro.
    /// Lanza ForbiddenException si no lo es.
    private static void VerifyOwner(Book book, Guid ownerId)
    {
        if (book.OwnerId != ownerId)
            throw new ForbiddenException(
                $"El usuario {ownerId} no tiene permiso para modificar el libro {book.Id}.");
    }

    /// Valida que todos los genre IDs existen en BD.
    private async Task ValidateGenreIdsAsync(List<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return;
        if (!await genreRepo.AllExistAsync(ids, ct))
            throw new ValidationException(
                "Uno o más géneros no existen.");
    }

    /// Valida que todos los language IDs existen en BD.
    private async Task ValidateLanguageIdsAsync(List<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return;
        if (!await languageRepo.AllExistAsync(ids, ct))
            throw new ValidationException(
                "Uno o más idiomas no existen.");
    }

    // =====================================================================
    // MAPPERS
    // =====================================================================

    /// Convierte Book a BookDetailDTO (vista completa).
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
            .Select(p => new BookPhotoDTO
            {
                Url = p.Url,
                Order = p.Orden
            })
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

    /// Convierte Book a BookListItemDTO (vista resumida para listas).
    private static BookListItemDTO MapToListItemDTO(Book book) => new()
    {
        Id = book.Id,
        Titulo = book.Titulo,
        Autor = book.Autor,
        Status = book.Status,
        Cover = book.Cover,
        Condition = book.Condition,
        ThumbnailUrl = book.Photos
            .OrderBy(p => p.Orden)
            .FirstOrDefault()?.Url,
        UpdatedAt = book.UpdatedAt
    };
}