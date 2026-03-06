using Bookmerang.Api.Models.DTOs.Books.Queries;
using Bookmerang.Api.Models.DTOs.Books.Requests;
using Bookmerang.Api.Models.DTOs.Books.Responses;

namespace Bookmerang.Api.Services.Interfaces.Books;

public interface IBookService
{

    Task<BookDetailDTO> CreateDraftAsync(
        string supabaseId,
        CreateBookDraftRequest request,
        CancellationToken ct = default);

    Task<BookDetailDTO> UpsertPhotosAsync(
        int bookId,
        string supabaseId,
        UpsertBookPhotosRequest request,
        CancellationToken ct = default);

    Task<BookDetailDTO> UpdateDraftDataAsync(
        int bookId,
        string supabaseId,
        UpdateBookDataRequest request,
        CancellationToken ct = default);

    Task<BookDetailDTO> UpdateDraftDetailsAsync(
        int bookId,
        string supabaseId,
        UpdateBookDetailsRequest request,
        CancellationToken ct = default);

    Task<BookDetailDTO> PublishAsync(
        int bookId,
        string supabaseId,
        CancellationToken ct = default);

    Task<PagedResult<BookListItemDTO>> GetMyLibraryAsync(
        string supabaseId,
        LibraryQuery query,
        CancellationToken ct = default);

    Task<PagedResult<BookListItemDTO>> GetMyDraftsAsync(
        string supabaseId,
        DraftsQuery query,
        CancellationToken ct = default);

    Task<BookDetailDTO> GetByIdAsync(
        int bookId,
        string supabaseId,
        CancellationToken ct = default);

    Task DeleteAsync(
        int bookId,
        string supabaseId,
        CancellationToken ct = default);
}