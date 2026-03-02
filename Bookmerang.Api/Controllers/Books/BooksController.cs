using Bookmerang.Api.Models.DTOs.Books.Queries;
using Bookmerang.Api.Models.DTOs.Books.Requests;
using Bookmerang.Api.Services.Interfaces.Books;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace Bookmerang.Api.Controllers.Books;

[ApiController]
[Route("api/books")]
[Authorize]
[Tags("Books")]
public class BooksController(IBookService bookService) : ControllerBase
{
    // FIXED: Extraemos el supabaseId (string) del token JWT en lugar del Guid directamente
    // El supabaseId se usará en el servicio para buscar el usuario en la BD
    private string SupabaseId => User.FindFirstValue("sub")
        ?? User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
        ?? throw new Exception("No se encontró el claim 'sub' en el token JWT.");

    // ─────────────────────────────────────────────────────────────────────
    // POST /api/books/draft
    // ─────────────────────────────────────────────────────────────────────
    /// <summary>Crea un borrador nuevo</summary>
    /// <remarks>
    /// Crea un libro con status DRAFT. Ningún campo es obligatorio en este paso,
    /// puedes crear un borrador vacío y rellenarlo después.
    ///
    /// **Ejemplo mínimo:**
    /// ```json
    /// {
    ///   "titulo": "El Señor de los Anillos",
    ///   "autor": "J.R.R. Tolkien",
    ///   "genreIds": [1],
    ///   "languageIds": [1]
    /// }
    /// ```
    ///
    /// **Ejemplo completo:**
    /// ```json
    /// {
    ///   "isbn": "978-84-450-7979-0",
    ///   "titulo": "El Señor de los Anillos",
    ///   "autor": "J.R.R. Tolkien",
    ///   "editorial": "Minotauro",
    ///   "numPaginas": 1200,
    ///   "cover": "Tapa_Dura",
    ///   "condition": "Bueno",
    ///   "observaciones": "Algunas marcas en la portada",
    ///   "genreIds": [1, 2],
    ///   "languageIds": [1]
    /// }
    /// ```
    /// </remarks>
    [HttpPost("draft")]
    [SwaggerResponse(201, "Borrador creado correctamente")]
    [SwaggerResponse(400, "Géneros o idiomas no válidos")]
    [SwaggerResponse(401, "No autenticado — añade el token JWT en Authorize")]
    public async Task<IActionResult> CreateDraft(
        [FromBody] CreateBookDraftRequest request,
        CancellationToken ct)
    {
        var result = await bookService.CreateDraftAsync(SupabaseId, request, ct);
        return CreatedAtAction(nameof(GetById), new { bookId = result.Id }, result);
    }

    // ─────────────────────────────────────────────────────────────────────
    // PUT /api/books/{bookId}/photos
    // ─────────────────────────────────────────────────────────────────────
    /// <summary>Sube o reemplaza las fotos de un libro</summary>
    /// <remarks>
    /// Reemplaza TODAS las fotos del libro. Máximo 5 fotos.
    /// El orden se determina por el campo `order` (empieza en 0).
    ///
    /// **Ejemplo:**
    /// ```json
    /// {
    ///   "photos": [
    ///     { "url": "https://ejemplo.com/foto1.jpg", "order": 0 },
    ///     { "url": "https://ejemplo.com/foto2.jpg", "order": 1 }
    ///   ]
    /// }
    /// ```
    /// </remarks>
    [HttpPut("{bookId:int}/photos")]
    [SwaggerResponse(200, "Fotos actualizadas correctamente")]
    [SwaggerResponse(400, "Más de 5 fotos o datos inválidos")]
    [SwaggerResponse(401, "No autenticado")]
    [SwaggerResponse(403, "No eres el dueño del libro")]
    [SwaggerResponse(404, "Libro no encontrado")]
    public async Task<IActionResult> UpsertPhotos(
        int bookId,
        [FromBody] UpsertBookPhotosRequest request,
        CancellationToken ct)
    {
        var result = await bookService.UpsertPhotosAsync(bookId, SupabaseId, request, ct);
        return Ok(result);
    }

    // ─────────────────────────────────────────────────────────────────────
    // PUT /api/books/{bookId}/data
    // ─────────────────────────────────────────────────────────────────────
    /// <summary>Actualiza los datos bibliográficos del libro</summary>
    /// <remarks>
    /// Actualiza título, autor, editorial, páginas, tipo de portada, géneros e idiomas.
    ///
    /// **Valores válidos para `cover`:** `Tapa_Dura`, `Tapa_Blanda`
    ///
    /// **Ejemplo:**
    /// ```json
    /// {
    ///   "isbn": "978-84-450-7979-0",
    ///   "titulo": "El Señor de los Anillos",
    ///   "autor": "J.R.R. Tolkien",
    ///   "editorial": "Minotauro",
    ///   "numPaginas": 1200,
    ///   "cover": "Tapa_Blanda",
    ///   "genreIds": [1, 2],
    ///   "languageIds": [1]
    /// }
    /// ```
    /// </remarks>
    [HttpPut("{bookId:int}/data")]
    [SwaggerResponse(200, "Datos actualizados correctamente")]
    [SwaggerResponse(401, "No autenticado")]
    [SwaggerResponse(403, "No eres el dueño del libro")]
    [SwaggerResponse(404, "Libro no encontrado")]
    public async Task<IActionResult> UpdateData(
        int bookId,
        [FromBody] UpdateBookDataRequest request,
        CancellationToken ct)
    {
        var result = await bookService.UpdateDraftDataAsync(bookId, SupabaseId, request, ct);
        return Ok(result);
    }

    // ─────────────────────────────────────────────────────────────────────
    // PUT /api/books/{bookId}/details
    // ─────────────────────────────────────────────────────────────────────
    /// <summary>Actualiza el estado físico y observaciones del libro</summary>
    /// <remarks>
    /// Actualiza la condición física y observaciones del libro.
    ///
    /// **Valores válidos para `condition`:** `Nuevo`, `Como_Nuevo`, `Bueno`, `Aceptable`, `Malo`
    ///
    /// **Ejemplo:**
    /// ```json
    /// {
    ///   "condition": "Bueno",
    ///   "observaciones": "Tiene el lomo desgastado pero las páginas perfectas"
    /// }
    /// ```
    /// </remarks>
    [HttpPut("{bookId:int}/details")]
    [SwaggerResponse(200, "Detalles actualizados correctamente")]
    [SwaggerResponse(401, "No autenticado")]
    [SwaggerResponse(403, "No eres el dueño del libro")]
    [SwaggerResponse(404, "Libro no encontrado")]
    public async Task<IActionResult> UpdateDetails(
        int bookId,
        [FromBody] UpdateBookDetailsRequest request,
        CancellationToken ct)
    {
        var result = await bookService.UpdateDraftDetailsAsync(bookId, SupabaseId, request, ct);
        return Ok(result);
    }

    // ─────────────────────────────────────────────────────────────────────
    // POST /api/books/{bookId}/publish
    // ─────────────────────────────────────────────────────────────────────
    /// <summary>Publica un borrador</summary>
    /// <remarks>
    /// Cambia el status del libro de DRAFT a PUBLISHED.
    ///
    /// **Requisitos para publicar (si falta alguno devuelve 400):**
    /// - Título obligatorio
    /// - Autor obligatorio
    /// - Condición física obligatoria
    /// - Al menos 1 género
    /// - Al menos 1 idioma
    /// </remarks>
    [HttpPost("{bookId:int}/publish")]
    [SwaggerResponse(200, "Libro publicado correctamente")]
    [SwaggerResponse(400, "Faltan campos obligatorios para publicar")]
    [SwaggerResponse(401, "No autenticado")]
    [SwaggerResponse(403, "No eres el dueño del libro")]
    [SwaggerResponse(404, "Libro no encontrado")]
    public async Task<IActionResult> Publish(int bookId, CancellationToken ct)
    {
        var result = await bookService.PublishAsync(bookId, SupabaseId, ct);
        return Ok(result);
    }

    // ─────────────────────────────────────────────────────────────────────
    // GET /api/books/my-library
    // ─────────────────────────────────────────────────────────────────────
    /// <summary>Obtiene todos los libros del usuario autenticado</summary>
    /// <remarks>
    /// Devuelve los libros paginados. Excluye siempre los eliminados.
    ///
    /// **Query params opcionales:**
    /// - `page` (default: 1)
    /// - `pageSize` (default: 10)
    /// - `search` — filtra por título o autor
    /// - `status` — filtra por estado: `Draft`, `Published`, `Exchanged`
    ///
    /// **Ejemplo:** `GET /api/books/my-library?page=1&amp;pageSize=5&amp;search=tolkien`
    /// </remarks>
    [HttpGet("my-library")]
    [SwaggerResponse(200, "Lista paginada de libros")]
    [SwaggerResponse(401, "No autenticado")]
    public async Task<IActionResult> GetMyLibrary(
        [FromQuery] LibraryQuery query,
        CancellationToken ct)
    {
        var result = await bookService.GetMyLibraryAsync(SupabaseId, query, ct);
        return Ok(result);
    }

    // ─────────────────────────────────────────────────────────────────────
    // GET /api/books/my-drafts
    // ─────────────────────────────────────────────────────────────────────
    /// <summary>Obtiene los borradores del usuario autenticado</summary>
    /// <remarks>
    /// Devuelve solo los libros con status DRAFT del usuario paginados.
    ///
    /// **Ejemplo:** `GET /api/books/my-drafts?page=1&amp;pageSize=10`
    /// </remarks>
    [HttpGet("my-drafts")]
    [SwaggerResponse(200, "Lista paginada de borradores")]
    [SwaggerResponse(401, "No autenticado")]
    public async Task<IActionResult> GetMyDrafts(
        [FromQuery] DraftsQuery query,
        CancellationToken ct)
    {
        var result = await bookService.GetMyDraftsAsync(SupabaseId, query, ct);
        return Ok(result);
    }

    // ─────────────────────────────────────────────────────────────────────
    // GET /api/books/{bookId}
    // ─────────────────────────────────────────────────────────────────────
    /// <summary>Obtiene el detalle completo de un libro por ID</summary>
    /// <remarks>
    /// Devuelve todos los campos del libro incluyendo fotos, géneros e idiomas.
    /// Solo el dueño puede consultar su propio libro.
    /// </remarks>
    [HttpGet("{bookId:int}")]
    [SwaggerResponse(200, "Detalle completo del libro")]
    [SwaggerResponse(401, "No autenticado")]
    [SwaggerResponse(403, "No eres el dueño del libro")]
    [SwaggerResponse(404, "Libro no encontrado")]
    public async Task<IActionResult> GetById(int bookId, CancellationToken ct)
    {
        var result = await bookService.GetByIdAsync(bookId, SupabaseId, ct);
        return Ok(result);
    }

    // ─────────────────────────────────────────────────────────────────────
    // DELETE /api/books/{bookId}
    // ─────────────────────────────────────────────────────────────────────
    /// <summary>Elimina un libro (soft delete)</summary>
    /// <remarks>
    /// No borra el libro de la base de datos, solo lo marca como DELETED.
    /// No volverá a aparecer en ninguna lista ni búsqueda.
    /// </remarks>
    [HttpDelete("{bookId:int}")]
    [SwaggerResponse(204, "Libro eliminado correctamente")]
    [SwaggerResponse(401, "No autenticado")]
    [SwaggerResponse(403, "No eres el dueño del libro")]
    [SwaggerResponse(404, "Libro no encontrado")]
    public async Task<IActionResult> Delete(int bookId, CancellationToken ct)
    {
        await bookService.DeleteAsync(bookId, SupabaseId, ct);
        return NoContent();
    }
}