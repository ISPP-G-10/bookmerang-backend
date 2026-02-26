using Bookmerang.Api.Models;

namespace Bookmerang.Api.Services.Interfaces.Books;

public interface IBookRepository
{
    Task<IEnumerable<Book>> GetAllAsync();
    Task<IEnumerable<Book>> GetByOwnerIdAsync(Guid ownerId);
    Task<Book?> GetByIdAsync(int id);
    Task<Book> AddAsync(Book book);
    Task UpdateAsync(Book book);
    Task DeleteAsync(int id);
}
