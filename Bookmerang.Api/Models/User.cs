using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bookmerang.Api.Models;

/// Representa la tabla "users" de Supabase.
/// Patrón TPT (Table Per Type): comparte el mismo UUID con base_users.
/// La FK books.owner_id -> users.id apunta a ESTA entidad.

[Table("users")]
public class User
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("rating_mean")]
    public decimal RatingMean { get; set; }

    [Column("finished_exchanges")]
    public int FinishedExchanges { get; set; }

    [Column("plan")]
    public PricingPlan Plan { get; set; }

    // Navigation property hacia base_users (TPT)
    public BaseUser BaseUser { get; set; } = null!;

    // Navigation property inversa hacia los libros del usuario
    public ICollection<Book> Books { get; set; } = [];
}