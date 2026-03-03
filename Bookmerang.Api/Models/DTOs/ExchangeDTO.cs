namespace Bookmerang.Api.Models.DTOs;

public record ExchangeDto(
    int? ChatId,
    int? MatchId,
    ExchangeStatus? Status,
    DateTime? CreatedAt,
    DateTime? UpdatedAt
);

public record UpdateExchangeDto(
    int? ChatId,
    int? MatchId,
    ExchangeStatus? Status
);

public static class ExchangeExtensions
{
    public static ExchangeDto ToDto(this Exchange exchange) => new(
        exchange.ChatId,
        exchange.MatchId,
        exchange.Status,
        exchange.CreatedAt,
        exchange.UpdatedAt
    );
}