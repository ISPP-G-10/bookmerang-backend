namespace Bookmerang.Api.Models.Books;

/// Estrategia de fotos: REPLACE completo.
/// Cuando el usuario gestiona fotos, se eliminan todas las existentes
/// y se insertan las nuevas en el servicio. Esto simplifica la lógica
/// y evita conflictos de orden/sincronización con el frontend.
public class BookPhoto
{
    public int Id { get; set; }

    // FK hacia books.id
    public int BookId { get; set; }

    // URL de la imagen en el storage (Supabase Storage, S3, etc.)
    // NOT NULL en schema
    public string Url { get; set; } = string.Empty;

    /// Posición de la foto en el carrusel. 0-based o 1-based según frontend.
    /// NOT NULL, default 0 en Postgres.
    /// El servicio normaliza el orden antes de guardar (0,1,2,3,4).
    public int Orden { get; set; }

    // Navigation property hacia el libro padre
    public Book Book { get; set; } = null!;
}