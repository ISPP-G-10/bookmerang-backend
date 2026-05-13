using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bookmerang.Api.Services.Interfaces.Bookdrop;

namespace Bookmerang.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/bookdrops")]
[Authorize(Policy = "AdminOnly")]
public class AdminBookdropController(IBookdropService bookdropService) : ControllerBase
{
    private readonly IBookdropService _bookdropService = bookdropService;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var bookdrops = await _bookdropService.GetAll();
        return Ok(bookdrops);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var (found, error) = await _bookdropService.DeleteBookdrop(id);

        if (!found) return NotFound("Establecimiento no encontrado.");
        if (error != null) return Conflict(new { error });

        return Ok(new { message = "Establecimiento eliminado correctamente." });
    }
}
