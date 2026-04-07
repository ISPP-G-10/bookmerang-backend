using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Models.DTOs.Bookdrop;

/// El establecimiento envia el PIN para confirmar una accion
public record BookDropConfirmRequest(string Pin);

/// Resumen de un intercambio activo visible para el establecimiento
public record BookDropExchangeDto(
    int MeetingId,
    string Pin,
    BookdropExchangeStatus Status,
    string? Book1Title,
    string? Book2Title,
    string? User1Name,
    string? User2Name,
    DateTime? ScheduledAt
);
