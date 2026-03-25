using Bookmerang.Api.Models.Enums;
using Bookmerang.Api.Models.Entities;

namespace Bookmerang.Api.Models.DTOs;

public record ExchangeDto(
    int? ExchangeId,
    int? ChatId,
    int? MatchId,
    ExchangeStatus? Status,
    DateTime? CreatedAt,
    DateTime? UpdatedAt
);

public record ExchangeWithMatchDto(
    int? ExchangeId,
    int? ChatId,
    int? MatchId,
    Guid? User1Id,
    Guid? User2Id,
    int? Book1Id,
    int? Book2Id,
    ExchangeStatus? Status,
    DateTime? CreatedAt,
    DateTime? UpdatedAt
);

public static class ExchangeExtensions
{
    public static ExchangeDto ToDto(this Exchange exchange) => new(
        exchange.ExchangeId,
        exchange.ChatId,
        exchange.MatchId,
        exchange.Status,
        exchange.CreatedAt,
        exchange.UpdatedAt
    );

    public static ExchangeWithMatchDto ToWithMatchDto(this Exchange exchange) => new(
        exchange.ExchangeId,
        exchange.ChatId,
        exchange.MatchId,
        exchange.Match?.User1Id,
        exchange.Match?.User2Id,
        exchange.Match?.Book1Id,
        exchange.Match?.Book2Id,
        exchange.Status,
        exchange.CreatedAt,
        exchange.UpdatedAt
    );
}