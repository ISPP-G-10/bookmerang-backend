using Bookmerang.Api.Models.Entities;
using Bookmerang.Api.Services.Interfaces.Genres;
using Microsoft.AspNetCore.Mvc;

namespace Bookmerang.Api.Controllers.Genres;

[ApiController]
[Route("api/genres")]
public class GenreController : ControllerBase
{
    private readonly IGenreService _genreService;

    public GenreController(IGenreService genreService)
    {
        _genreService = genreService;
    }

    [HttpGet]
    public async Task<ActionResult<List<Genre>>> GetAll(CancellationToken cancellationToken)
    {
        var genres = await _genreService.GetAllAsync(cancellationToken);
        return Ok(genres);
    }
}