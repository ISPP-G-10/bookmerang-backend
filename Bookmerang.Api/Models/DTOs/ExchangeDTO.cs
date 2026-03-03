namespace Bookmerang.Api.Models.DTOs;

public record ExchangeDto(
    int? ExchangeId,
    string? SupabaseId,
    int? ChatId,
    int? MatchId,
    ExchangeStatus? Status,
    DateTime? CreatedAt,
    DateTime? UpdatedAt
);

public static class ExchangeExtensions
{
    public static ExchangeDto ToDto(this Exchange exchange) => new(
        exchange.ExchangeId,
        exchange.SupabaseId,
        exchange.ChatId,
        exchange.MatchId,
        exchange.Status,
        exchange.CreatedAt,
        exchange.UpdatedAt
    );
}