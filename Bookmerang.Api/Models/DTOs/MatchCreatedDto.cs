namespace Bookmerang.Api.Models.DTOs;

// Información del match creado como resultado de un swipe
public class MatchCreatedDto
{
    public required int MatchId { get; set; }
    public required int ChatId { get; set; }
    public required int OtherUserId { get; set; }
    public required string OtherUsername { get; set; }
}
