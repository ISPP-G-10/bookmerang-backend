using Bookmerang.Api.Controllers.Genres;
using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Services.Interfaces.Genres;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Bookmerang.Tests.Genres;

public class GenresControllerTests
{
    [Fact]
    public async Task GetAll_ConDatos_DevuelveOk()
    {
        var service = new Mock<IGenreService>();
        service.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<Genre>
               {
                   new() { Id = 1, Name = "Fantasía" },
                   new() { Id = 2, Name = "Ciencia ficción" }
               });

        var controller = new GenreController(service.Object);

        var result = await controller.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, ok.StatusCode);
        var genres = Assert.IsAssignableFrom<List<Genre>>(ok.Value);
        Assert.Equal(2, genres.Count);
    }

    [Fact]
    public async Task GetAll_SinDatos_DevuelveOkConListaVacia()
    {
        var service = new Mock<IGenreService>();
        service.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new List<Genre>());

        var controller = new GenreController(service.Object);

        var result = await controller.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, ok.StatusCode);
        var genres = Assert.IsAssignableFrom<List<Genre>>(ok.Value);
        Assert.Empty(genres);
    }
}