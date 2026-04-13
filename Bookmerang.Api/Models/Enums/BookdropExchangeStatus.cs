namespace Bookmerang.Api.Models.Enums;

public enum BookdropExchangeStatus
{
    AWAITING_DROP_1,  // Esperando que el primer usuario deje su libro
    BOOK_1_HELD,      // Libro 1 en custodia del establecimiento
    BOOK_2_HELD,      // User 2 recogio libro 1 y dejo libro 2. Falta que User 1 recoja
    COMPLETED         // Intercambio finalizado
}
