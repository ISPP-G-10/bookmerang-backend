using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NetTopologySuite.Geometries;
using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Models.Entities;

[Table("base_users")]
public class BaseUser
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    // Atributo de Supabase NO QUITAR
    [Required]
    [Column("supabase_id")]
    public string SupabaseId { get; set; } = string.Empty;

    [Required]
    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Column("password_hash")]
    public string? PasswordHash { get; set; }

    [Required]
    [Column("username")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [Column("nombre")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Column("foto_perfil_url")]
    public string ProfilePhoto { get; set; } = string.Empty;

    [Required]
    [Column("type")]
    public BaseUserType UserType { get; set; }

    [Required]
    [Column("location", TypeName = "geography (point, 4326)")]
    public Point Location { get; set; } = null!;

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}