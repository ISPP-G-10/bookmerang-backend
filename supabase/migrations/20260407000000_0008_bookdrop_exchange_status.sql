-- Crear el tipo enum para el ciclo de vida de intercambios BookDrop
CREATE TYPE bookdrop_exchange_status AS ENUM (
    'AWAITING_DROP_1',
    'BOOK_1_HELD',
    'BOOK_2_HELD',
    'COMPLETED'
);

-- Añadir columnas a exchange_meetings
ALTER TABLE exchange_meetings
    ADD COLUMN pin VARCHAR(6),
    ADD COLUMN bookdrop_status bookdrop_exchange_status;
