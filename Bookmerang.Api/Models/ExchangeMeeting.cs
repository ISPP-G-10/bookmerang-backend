using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using NetTopologySuite.Geometries;


namespace Bookmerang.Api.Models;

[Table("exchange_meeting")]
public class ExchangeMeeting {
    //id int [pk, increment]
    [Key]
    [Column("excahnge_meeting_id")]
    public Guid ExchangeMeeetingId { get; set; } = Guid.NewGuid();

    // Atributo de Supabase NO QUITAR
    [Required]
    [Column("supabase_id")]
    public string SupabaseId { get; set; } = string.Empty;

    //exchange_id int [not null, unique, ref: > exchange.id]
    [Required]
    [Column("exchange_id")]
    public Guid ExchangeId { get; set; }  // FK
    [ForeignKey(nameof(ExchangeId))]
    public Exchange Exchange { get; set; } = null!;  // Navigation property

    // mode exchange_mode [not null]
    [Required]
    [Column("exchange_mode")]
    public ExchangeMode ExchangeMode { get; set; } = ExchangeMode.BOOKSPOT;
    
    // bookspot_id int [ref: > bookspots.id]
    [Required]
    [Column("bookspot_id")]
    public Guid BookspotId { get; set; }
    // [ForeignKey(nameof(BookspotId))]
    // public Bookspot Bookspot { get; set; } = null!;
    
    
    // custom_location geography(Point, 4326) [not null]
    [Required]
    [Column("custom_location", TypeName = "geography (point, 4326)")]
    public Point CustomLocation { get; set; } = null!;

    // scheduled_at timestamp [not null]
    [Required]
    [Column("scheduled_at")]
    public DateTime ScheduledAt { get; set; } = DateTime.UtcNow;

    // proposer_id int [not null, ref: > users.id]
    [Required]
    [Column("proposer_id")]
    public Guid ProposerId { get; set; }
    // [ForeignKey(nameof(ProposerId))]
    // public User Proposer { get; set; } = null!;  // Navigation property
    
    // status exchange_meeting_status [not null]
    [Required]
    [Column("exchange_meeting_status")]
    public ExchangeMeetingStatus MeetingStatus { get; set; } = ExchangeMeetingStatus.PROPOSAL;

    // mark_as_completed_by_user1 boolean [default: false]
    [Column("mark_as_completed_by_user1")]
    public bool MarkAsCompletedByUser1 { get; set; } = false;

    // mark_as_completed_by_user2 boolean [default: false]
    [Column("mark_as_completed_by_user2")]
    public bool MarkAsCompletedByUser2 { get; set; } = false;
}
