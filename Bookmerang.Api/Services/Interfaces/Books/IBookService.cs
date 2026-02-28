using Bookmerang.Api.Models.DTOs.Books.Queries;
using Bookmerang.Api.Models.DTOs.Books.Requests;
using Bookmerang.Api.Models.DTOs.Books.Responses;

namespace Bookmerang.Api.Services.Interfaces.Books;

public interface IBookService
{
    /// Crea un borrador nuevo con status DRAFT.
    Task<BookDetailDto> CreateDraftAsync(
        Guid ownerId,
        CreateBookDraftRequest request,
        CancellationToken ct = default);

    /// Reemplaza todas las fotos del libro.
    Task<BookDetailDto> UpsertPhotosAsync(
        int bookId,
        Guid ownerId,
        UpsertBookPhotosRequest request,
        CancellationToken ct = default);

    /// Actualiza los datos bibliográficos del libro.
    Task<BookDetailDto> UpdateDraftDataAsync(
        int bookId,
        Guid ownerId,
        UpdateBookDataRequest request,
        CancellationToken ct = default);

    /// Actualiza los detalles del libro.
    Task<BookDetailDto> UpdateDraftDetailsAsync(
        int bookId,
        Guid ownerId,
        UpdateBookDetailsRequest request,
        CancellationToken ct = default);

    /// Publica el libro cambiando status a PUBLISHED.
    Task<BookDetailDto> PublishAsync(
        int bookId,
        Guid ownerId,
        CancellationToken ct = default);

    /// Obtiene los libros del usuario paginados con filtros.
    Task<PagedResult<BookListItemDto>> GetMyLibraryAsync(
        Guid ownerId,
        LibraryQuery query,
        CancellationToken ct = default);

    /// Obtiene los borradores del usuario paginados.
    Task<PagedResult<BookListItemDto>> GetMyDraftsAsync(
        Guid ownerId,
        DraftsQuery query,
        CancellationToken ct = default);

    /// Obtiene el detalle completo de un libro.
    Task<BookDetailDto> GetByIdAsync(
        int bookId,
        Guid ownerId,
        CancellationToken ct = default);

    /// Soft delete: cambia status a DELETED.
    /// Posible cambio a HARD delete en el futuro si se decide que no es necesario conservar el historial.
    Task DeleteAsync(
        int bookId,
        Guid ownerId,
        CancellationToken ct = default);
}