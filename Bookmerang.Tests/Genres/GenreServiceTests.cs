using Bookmerang.Api.Data;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Services.Implementation.Genres;
using Bookmerang.Tests.Helpers;
using Xunit;
using Bookmerang.Api.Models.DTOs.Books.Queries;
using Bookmerang.Api.Models.DTOs.Books.Responses;

namespace Bookmerang.Tests.Genres;

public class GenreServiceTests : IAsyncLifetime
{
    private AppDbContext _db = null!;
    private GenreService _service = null!;

    public Task InitializeAsync()
    {
        _db = DbContextFactory.CreateInMemory();
        _service = new GenreService(_db);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task GetAllAsync_ConGeneros_DevuelveLista()
    {
        _db.Genres.Add(new Genre { Id = 1, Name = "Fantasía" });
        _db.Genres.Add(new Genre { Id = 2, Name = "Ciencia ficción" });
        await _db.SaveChangesAsync();

        var result = await _service.GetAllAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAllAsync_SinGeneros_DevuelveListaVacia()
    {
        var result = await _service.GetAllAsync();

        Assert.Empty(result);
    }
    
}