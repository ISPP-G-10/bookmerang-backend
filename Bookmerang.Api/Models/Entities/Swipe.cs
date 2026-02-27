using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Models.Entities;

public class Swipe
{
    public int Id { get; set; }
    public int SwiperId { get; set; }
    public int BookId { get; set; }
    public required SwipeDirection Direction { get; set; }
    public DateTime CreatedAt { get; set; }

    public Book Book { get; set; } = null!;
}
