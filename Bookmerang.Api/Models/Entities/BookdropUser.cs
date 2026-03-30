using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bookmerang.Api.Models.Entities;

[Table("bookdrop_users")]
public class BookdropUser
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("book_spot_id")]
    public int BookSpotId { get; set; }

    [ForeignKey("Id")]
    public BaseUser BaseUser { get; set; } = null!;

    [ForeignKey("BookSpotId")]
    public Bookspot Bookspot { get; set; } = null!;
}
