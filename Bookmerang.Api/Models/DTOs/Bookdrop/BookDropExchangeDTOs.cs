using Bookmerang.Api.Models.Enums;

namespace Bookmerang.Api.Models.DTOs.Bookdrop;

/// El establecimiento envia el PIN para confirmar una accion
public record BookDropConfirmRequest(string Pin);

/// Resumen de un intercambio activo visible para el establecimiento
/// El pin nunca se expone al establecimiento, lo introduce el usuario verbalmente allí
public record BookDropExchangeDto(
    int MeetingId,
    BookdropExchangeStatus Status,
    string? Book1Title,
    string? Book2Title,
    string? User1Name,
    string? User2Name,
    DateTime? ScheduledAt
);
