using Bookmerang.Api.Models.DTOs.Books.Queries;
using Bookmerang.Api.Models.DTOs.Books.Requests;
using Bookmerang.Api.Models.DTOs.Books.Responses;

namespace Bookmerang.Api.Services.Interfaces.Books;

public interface IBookService
{
    /// Crea un borrador nuevo con status DRAFT.
    Task<BookDetailDTO> CreateDraftAsync(
        Guid ownerId,
        CreateBookDraftRequest request,
        CancellationToken ct = default);

    /// Reemplaza todas las fotos del libro.
    Task<BookDetailDTO> UpsertPhotosAsync(
        int bookId,
        Guid ownerId,
        UpsertBookPhotosRequest request,
        CancellationToken ct = default);

    /// Actualiza los datos bibliográficos del libro.
    Task<BookDetailDTO> UpdateDraftDataAsync(
        int bookId,
        Guid ownerId,
        UpdateBookDataRequest request,
        CancellationToken ct = default);

    /// Actualiza los detalles del libro.
    Task<BookDetailDTO> UpdateDraftDetailsAsync(
        int bookId,
        Guid ownerId,
        UpdateBookDetailsRequest request,
        CancellationToken ct = default);

    /// Publica el libro cambiando status a PUBLISHED.
    Task<BookDetailDTO> PublishAsync(
        int bookId,
        Guid ownerId,
        CancellationToken ct = default);

    /// Obtiene los libros del usuario paginados con filtros.
    Task<PagedResult<BookListItemDTO>> GetMyLibraryAsync(
        Guid ownerId,
        LibraryQuery query,
        CancellationToken ct = default);

    /// Obtiene los borradores del usuario paginados.
    Task<PagedResult<BookListItemDTO>> GetMyDraftsAsync(
        Guid ownerId,
        DraftsQuery query,
        CancellationToken ct = default);

    /// Obtiene el detalle completo de un libro.
    Task<BookDetailDTO> GetByIdAsync(
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