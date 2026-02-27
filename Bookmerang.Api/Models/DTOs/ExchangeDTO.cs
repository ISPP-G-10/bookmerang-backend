namespace Bookmerang.Api.Models.DTOs;

public record ExchangeDto(
    Guid ExchangeId,
    string SupabaseId,
    Guid ChatId,
    Guid MatchId,
    ExchangeStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public static class ExchangeExtensions
{
    public static ExchangeDto ToDto(this Exchange exchange) => new(
        exchange.ExchangeId,
        exchange.SupabaseId,
        exchange.ChatId,
        exchange.MatchId,
        exchange.status,
        exchange.CreatedAt,
        exchange.UpdatedAt
    );
}