using Bookmerang.Api.Models;
using Bookmerang.Api.Models.Books.Enums;
using Bookmerang.Api.Services.Interfaces.Books;

namespace Bookmerang.Api.Services.Implementation.Books;

public class BookService : IBookService
{
    private readonly IBookRepository _repository;

    public BookService(IBookRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<Book>> GetAllBooksAsync()
    {
        return await _repository.GetAllAsync();
    }

    public async Task<IEnumerable<Book>> GetBooksByOwnerAsync(Guid ownerId)
    {
        return await _repository.GetByOwnerIdAsync(ownerId);
    }

    public async Task<Book?> GetBookByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<Book> CreateBookAsync(Book book)
    {
        book.Status = BookStatus.Draft;
        return await _repository.AddAsync(book);
    }

    public async Task UpdateBookAsync(Book book)
    {
        await _repository.UpdateAsync(book);
    }

    public async Task DeleteBookAsync(int id)
    {
        await _repository.DeleteAsync(id);
    }
}
