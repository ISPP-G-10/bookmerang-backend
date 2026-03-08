using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bookmerang.Api.Models.Enums;


namespace Bookmerang.Api.Models.Entities;

[Table("exchange")]
public class Exchange {
    //id int [pk, increment]
    [Key]
    [Column("exchange_id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int ExchangeId { get; set; }

    // Atributo de Supabase NO QUITAR
    // [Required]
    // [Column("supabase_id")]
    // public string SupabaseId { get; set; } = string.Empty;

    //chat_id int [not null, unique, note: '1 chat = 1 exchange']
    [Required]
    [Column("chat_id")]
    public int ChatId { get; set; }
    [ForeignKey(nameof(ChatId))]
    public Chat? Chat { get; set; } // Navigation property -> relación 1:1 es la interrrogación
  
    //match_id int [not null, ref: > matches.id]
    [Required]
    [Column("match_id")]
    public int MatchId { get; set; }

    [ForeignKey(nameof(MatchId))]
    public Match Match { get; set; } = null!; // TODO: create Match entity

    //status exchange_status [not null]
    [Required]
    [Column("exchange_status")]
    public ExchangeStatus Status { get; set; } = ExchangeStatus.NEGOTIATING;

    // created_at timestamp [not null]
    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // created_at timestamp [not null]
    [Required]
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set;} = DateTime.UtcNow;
}