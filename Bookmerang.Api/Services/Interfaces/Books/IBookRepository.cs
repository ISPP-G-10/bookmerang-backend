using Bookmerang.Api.Models;
using Bookmerang.Api.Models.Books;
using Bookmerang.Api.Models.Books.Enums;
using Bookmerang.Api.Models.DTOs.Books.Queries;

namespace Bookmerang.Api.Services.Interfaces.Books;

public interface IBookRepository
{
    /// Obtiene un libro por ID incluyendo fotos, géneros e idiomas.
    /// Devuelve null si no existe.
    Task<Book?> GetByIdAsync(int bookId, CancellationToken ct = default);

    /// Obtiene libros del usuario paginados con filtros opcionales.
    /// Excluye siempre los libros con status DELETED.
    /// Devuelve la lista de libros Y el total sin paginar (para PagedResult).
    Task<(List<Book> Items, int Total)> GetByOwnerPagedAsync(
        Guid ownerId,
        LibraryQuery query,
        CancellationToken ct = default);

    /// Obtiene borradores del usuario paginados.
    /// Solo devuelve libros con status DRAFT.
    Task<(List<Book> Items, int Total)> GetDraftsPagedAsync(
        Guid ownerId,
        DraftsQuery query,
        CancellationToken ct = default);

    /// Crea un libro nuevo en BD y devuelve la entidad con Id generado.
    Task<Book> CreateAsync(Book book, CancellationToken ct = default);

    /// Actualiza un libro existente.
    /// La entidad debe estar tracked por EF (cargada previamente con GetByIdAsync).
    Task UpdateAsync(Book book, CancellationToken ct = default);

    /// Reemplaza TODAS las fotos del libro con las nuevas.
    Task ReplacePhotosAsync(
        int bookId,
        List<BookPhoto> newPhotos,
        CancellationToken ct = default);

    /// Reemplaza TODOS los géneros del libro con los nuevos IDs.
    Task ReplaceGenresAsync(
        int bookId,
        List<int> genreIds,
        CancellationToken ct = default);

    /// Reemplaza TODOS los idiomas del libro con los nuevos IDs.
    Task ReplaceLanguagesAsync(
        int bookId,
        List<int> languageIds,
        CancellationToken ct = default);
}