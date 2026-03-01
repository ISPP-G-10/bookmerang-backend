using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Models.Entities;

public class Exchange
{
    public int Id { get; set; }
    public int ChatId { get; set; }
    public int MatchId { get; set; }
    public required ExchangeStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Chat Chat { get; set; } = null!;
    public Match Match { get; set; } = null!;
}