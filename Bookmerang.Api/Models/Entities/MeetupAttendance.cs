using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Models.Entities;

[Table("meetup_attendance")]
public class MeetupAttendance
{
    [Column("meetup_id")]
    public int MeetupId { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Required]
    [Column("selected_book_id")]
    public int SelectedBookId { get; set; }

    [Required]
    [Column("status")]
    public MeetupAttendanceStatus Status { get; set; }

    [ForeignKey("MeetupId")]
    public Meetup Meetup { get; set; } = null!;

    [ForeignKey("UserId")]
    public User User { get; set; } = null!;

    [ForeignKey("SelectedBookId")]
    public Book SelectedBook { get; set; } = null!;
}