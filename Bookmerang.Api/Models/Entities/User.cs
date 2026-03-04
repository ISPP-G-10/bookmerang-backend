using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Models.Entities;

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
    public decimal RatingMean { get; set; } = 0;

    [Column("finished_exchanges")]
    public int FinishedExchanges { get; set; } = 0;

    [Column("plan")]
    public PricingPlan Plan { get; set; }

    // Navigation properties
    [ForeignKey("Id")]
    public BaseUser BaseUser { get; set; } = null!;
    
    public UserPreference? UserPreference { get; set; }

    // Navigation property inversa hacia los libros del usuario
    public ICollection<Book> Books { get; set; } = [];

    // Propiedades de conveniencia para acceder a BaseUser (compatibilidad con código legacy)
    [NotMapped]
    public string SupabaseId => BaseUser?.SupabaseId ?? string.Empty;
    
    [NotMapped]
    public string Username => BaseUser?.Username ?? string.Empty;
    
    [NotMapped]
    public NetTopologySuite.Geometries.Point? Location => BaseUser?.Location;
}
