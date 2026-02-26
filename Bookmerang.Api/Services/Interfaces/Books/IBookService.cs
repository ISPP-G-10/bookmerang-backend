using Bookmerang.Api.Models;

namespace Bookmerang.Api.Services.Interfaces.Books;

public interface IBookService
{
    Task<IEnumerable<Book>> GetAllBooksAsync();
    Task<IEnumerable<Book>> GetBooksByOwnerAsync(Guid ownerId);
    Task<Book?> GetBookByIdAsync(int id);
    Task<Book> CreateBookAsync(Book book);
    Task UpdateBookAsync(Book book);
    Task DeleteBookAsync(int id);
}
