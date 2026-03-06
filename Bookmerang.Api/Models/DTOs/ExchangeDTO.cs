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
}