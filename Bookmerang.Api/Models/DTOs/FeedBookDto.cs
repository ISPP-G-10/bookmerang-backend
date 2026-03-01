namespace Bookmerang.Api.Models.DTOs;

// DTO devuelto por get /feed
public class FeedBookDto
{
    public required int Id { get; set; }
    public required Guid OwnerId { get; set; }
    public required string OwnerUsername { get; set; }
    public string? Titulo { get; set; }
    public string? Autor { get; set; }
    public string? Editorial { get; set; }
    public int? NumPaginas { get; set; }
    public string? Cover { get; set; }
    public string? Condition { get; set; }
    public string? Observaciones { get; set; }
    public List<string> Genres { get; set; } = [];
    public List<string> Photos { get; set; } = [];
    public double Score { get; set; }
    public bool IsPriority { get; set; } // P1 vs P2
}
