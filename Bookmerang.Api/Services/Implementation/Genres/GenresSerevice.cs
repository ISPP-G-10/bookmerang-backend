using Bookmerang.Api.Data;
using Bookmerang.Api.Models;
using Bookmerang.Api.Services.Interfaces.Genres;
using Microsoft.EntityFrameworkCore;

namespace Bookmerang.Api.Services.Implementation.Genres;

public class GenreService : IGenreService
{
    private readonly AppDbContext _dbContext;

    public GenreService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Genre>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Genres
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}