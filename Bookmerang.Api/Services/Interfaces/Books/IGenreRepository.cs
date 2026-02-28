using Bookmerang.Api.Models.Books;

namespace Bookmerang.Api.Services.Interfaces.Books;

public interface IGenreRepository
{
    /// Devuelve todos los géneros disponibles.
    /// Se usa para que el frontend muestre el selector de géneros.
    Task<List<Genre>> GetAllAsync(CancellationToken ct = default);

    /// Se usa en validación antes de guardar géneros en un libro.
    Task<bool> AllExistAsync(List<int> ids, CancellationToken ct = default);
}