using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bookmerang.Api.Models.Enums;
using NetTopologySuite.Geometries;

namespace Bookmerang.Api.Models.Entities;

[Table("meetups")]
public class Meetup
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [Column("community_id")]
    public int CommunityId { get; set; }

    [Required]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("other_book_spot_id")]
    public int? OtherBookSpotId { get; set; }

    [Column("other_location", TypeName = "geography(Point,4326)")]
    public Point? OtherLocation { get; set; }

    [Required]
    [Column("scheduled_at")]
    public DateTime ScheduledAt { get; set; }

    [Required]
    [Column("status")]
    public MeetupStatus Status { get; set; }

    [Column("creator_id")]
    public Guid? CreatorId { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("CommunityId")]
    public Community Community { get; set; } = null!;

    [ForeignKey("OtherBookSpotId")]
    public Bookspot? OtherBookspot { get; set; }

    [ForeignKey("CreatorId")]
    public User? Creator { get; set; }

    public ICollection<MeetupAttendance> Attendances { get; set; } = new List<MeetupAttendance>();
}