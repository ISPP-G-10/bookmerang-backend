using Bookmerang.Api.Models;

namespace Bookmerang.Api.Services.Interfaces.Genres;

public interface IGenreService
{
    Task<List<Genre>> GetAllAsync(CancellationToken cancellationToken = default);
}