using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Models.Entities;

public class Match
{
    public int Id { get; set; }
    public Guid User1Id { get; set; }
    public Guid User2Id { get; set; }
    public int Book1Id { get; set; }
    public int Book2Id { get; set; }
    public required MatchStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }

    public Book Book1 { get; set; } = null!;
    public Book Book2 { get; set; } = null!;
}
