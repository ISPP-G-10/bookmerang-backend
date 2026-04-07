using Bookmerang.Api.Models.DTOs.Bookdrop;

namespace Bookmerang.Api.Services.Interfaces.Bookdrop;

public interface IBookDropExchangeService
{
    /// Devuelve los intercambios activos en el BookDrop del establecimiento
    Task<List<BookDropExchangeDto>> GetActiveExchanges(int bookspotId);

    /// Confirma que un usuario ha dejado el primer libro
    Task<BookDropExchangeDto> ConfirmDrop(int meetingId, string pin, int bookspotId);

    /// Confirma que User 2 ha recogido libro 1 y dejado libro 2
    Task<BookDropExchangeDto> ConfirmSwap(int meetingId, string pin, int bookspotId);

    /// Confirma que User 1 ha recogido libro 2 (finaliza el intercambio)
    Task<BookDropExchangeDto> ConfirmPickup(int meetingId, string pin, int bookspotId);
}
